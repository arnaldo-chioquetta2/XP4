using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Windows.Media;
using Mp3PlayerWinForms.Services;
using XP3.Models;
using System.IO;
using System.Text;
using System.Data.SQLite;
using XP3.Data;

namespace XP3.Services
{
    public class AudioPlayerService : IDisposable
    {
        private float _volume = AppSettings.InitialVolume;

        // Voltamos para WaveOutEvent, que é a API mais compatível
        private WaveOutEvent _waveOut;

        private WaveStream _audioFile;
        private MediaPlayer _mediaPlayer;
        private VolumeSampleProvider _volumeProvider;
        private List<Track> _playlist;
        private int _currentIndex = -1;

        public event EventHandler<Track> TrackChanged;
        public event EventHandler<float[]> FftDataReceived;
        public event EventHandler<Tuple<Track, string>> PlaybackError;
        private SampleAggregator _aggregator;

        public AudioPlayerService()
        {
            _mediaPlayer = new MediaPlayer();
            _playlist = new List<Track>();
            _mediaPlayer.MediaEnded += _mediaPlayer_MediaEnded;

            try { File.Delete("debug_audio_log.txt"); } catch { }
            GravarLog("=== INICIANDO SERVIÇO DE ÁUDIO (MÓDULO WAVEOUT LEGACY) ===");
        }

        private void GravarLog(string mensagem)
        {
            try
            {
                string caminho = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_audio_log.txt");
                File.AppendAllText(caminho, $"{DateTime.Now:HH:mm:ss.fff}: {mensagem}{Environment.NewLine}");
            }
            catch { }
        }

        public void SetVolume(float volume)
        {
            _volume = volume;
            if (_volumeProvider != null) _volumeProvider.Volume = _volume;
        }

        public TimeSpan CurrentTime => _audioFile?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalTime => _audioFile?.TotalTime ?? TimeSpan.Zero;
        public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
        public Track CurrentTrack => (_currentIndex >= 0 && _currentIndex < _playlist.Count) ? _playlist[_currentIndex] : null;

        private void _mediaPlayer_MediaEnded(object sender, EventArgs e) => Next();
        public void SetPlaylist(List<Track> tracks) => _playlist = tracks;

        public void SetPosition(double percentage)
        {
            if (_audioFile != null)
            {
                _audioFile.CurrentTime = TimeSpan.FromSeconds(_audioFile.TotalTime.TotalSeconds * percentage);
            }
        }

        // --- NOVA LÓGICA: WAVEOUT (Busca por Índice) ---
        private int ObterIndiceDispositivoWaveOut()
        {
            GravarLog("--- Listando Dispositivos WaveOut (Legado) ---");

            int waveOutCount = WaveOut.DeviceCount;
            int dispositivoEscolhido = -1; // -1 = Mapper (Padrão do Windows)

            for (int i = 0; i < waveOutCount; i++)
            {
                try
                {
                    var caps = WaveOut.GetCapabilities(i);
                    string nome = caps.ProductName;
                    string nomeLower = nome.ToLower();

                    GravarLog($"ID {i}: {nome}");

                    // Lógica de Detecção
                    // A WaveOut muitas vezes corta o nome, então procuramos partes menores
                    // "high def" ou algo similar
                    if ((nomeLower.Contains("high definition") || nomeLower.Contains("high def"))
                        && !nomeLower.Contains("nvidia"))
                    {
                        GravarLog($" -> ALVO DETECTADO (ID {i}): {nome}");
                        dispositivoEscolhido = i;
                        // break; // Se quiser garantir o primeiro
                    }
                }
                catch (Exception ex)
                {
                    GravarLog($"Erro ao ler caps do device {i}: {ex.Message}");
                }
            }

            if (dispositivoEscolhido != -1)
            {
                GravarLog($"*** USANDO DISPOSITIVO ID {dispositivoEscolhido} ***");
                return dispositivoEscolhido;
            }
            else
            {
                GravarLog("*** NENHUM ESPECÍFICO ENCONTRADO. USANDO MAPPER (-1) ***");
                return -1;
            }
        }

        public void Play(int index)
        {
            if (index < 0 || index >= _playlist.Count) return;

            Stop();
            _currentIndex = index;
            var track = _playlist[_currentIndex];

            GravarLog($"Iniciando Play (WaveOut + 16bit): {track.Title}");

            try
            {
                // 1. Leitor
                var reader = new MediaFoundationReader(track.FilePath);
                _audioFile = reader;

                // 2. Aggregator (Visualizer continua recebendo dados em alta definição)
                _aggregator = new SampleAggregator(reader.ToSampleProvider(), 256);
                _aggregator.FftCalculated += (s, args) => FftDataReceived?.Invoke(this, args.Result);

                // 3. Volume
                _volumeProvider = new VolumeSampleProvider(_aggregator);
                _volumeProvider.Volume = _volume;

                // --- O PULO DO GATO PARA DRIVERS GENÉRICOS ---
                // Convertemos de volta para 16-bit (Qualidade de CD padrão).
                // Isso força o áudio a um formato que qualquer placa de som aceita.
                // Sem isso, drivers antigos correm (aceleram) quando recebem Float 32-bit.
                var finalWaveProvider = new SampleToWaveProvider16(_volumeProvider);

                // 4. Seleção do Dispositivo
                int deviceId = ObterIndiceDispositivoWaveOut();

                // 5. Inicialização WaveOutEvent
                _waveOut = new WaveOutEvent();
                _waveOut.DeviceNumber = deviceId;

                // Aumentar buffers previne engasgos
                _waveOut.DesiredLatency = 200;
                _waveOut.NumberOfBuffers = 2;

                _waveOut.Init(finalWaveProvider); // Passamos o provider de 16-bit!
                GravarLog("WaveOut Init OK.");

                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Play();
                GravarLog("Playback Iniciado.");

                TrackChanged?.Invoke(this, track);
            }
            catch (Exception ex)
            {
                GravarLog($"ERRO FATAL: {ex.Message}\n{ex.StackTrace}");
                RegistrarLogErro(track, ex);
                PlaybackError?.Invoke(this, new Tuple<Track, string>(track, $"Erro: {ex.Message}"));
            }
        }

        public void TogglePlayPause()
        {
            if (_waveOut == null) { if (_playlist.Count > 0) Play(0); return; }
            if (_waveOut.PlaybackState == PlaybackState.Playing) _waveOut.Pause();
            else _waveOut.Play();
        }

        public void Stop()
        {
            try
            {
                if (_waveOut != null)
                {
                    _waveOut.PlaybackStopped -= OnPlaybackStopped;
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                }
                if (_audioFile != null)
                {
                    _audioFile.Dispose();
                    _audioFile = null;
                }
                _volumeProvider = null;
            }
            catch (Exception ex)
            {
                GravarLog($"Erro ao parar: {ex.Message}");
            }
        }

        public void Next()
        {
            if (_playlist.Count == 0) return;
            if (_currentIndex < _playlist.Count - 1) Play(_currentIndex + 1);
            else Play(0);
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                GravarLog($"Parada com erro: {e.Exception.Message}");
                PlaybackError?.Invoke(this, new Tuple<Track, string>(CurrentTrack, $"Erro: {e.Exception.Message}"));
                return;
            }
            if (_playlist != null && _currentIndex < _playlist.Count - 1) Next();
            else if (_playlist != null && _currentIndex >= _playlist.Count - 1) Play(0);
        }

        public void Dispose() => Stop();
        public void AtualizarIndiceAposRemocao(int novoIndice) => this._currentIndex = novoIndice;

        private void RegistrarLogErro(Track track, Exception ex)
        {
            try
            {
                string arquivoLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log_Erros_Playback.txt");
                File.AppendAllText(arquivoLog, $"{DateTime.Now} - {track.Title} - {ex.Message}\n");
            }
            catch { }
        }

        public void AdicionarParaApagarDepois(string caminho, string banda)
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                string sql = "INSERT INTO ApagarMusicas (Lugar, Banda) VALUES (@Lugar, @Banda)";
                using (var command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Lugar", caminho);
                    command.Parameters.AddWithValue("@Banda", banda);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void RemoverMusicaDefinitivamente(int trackId)
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var cmd1 = new SQLiteCommand("DELETE FROM PlaylistTracks WHERE TrackId = @Id", connection, transaction);
                        cmd1.Parameters.AddWithValue("@Id", trackId);
                        cmd1.ExecuteNonQuery();

                        var cmd2 = new SQLiteCommand("DELETE FROM Tracks WHERE Id = @Id", connection, transaction);
                        cmd2.Parameters.AddWithValue("@Id", trackId);
                        cmd2.ExecuteNonQuery();

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}