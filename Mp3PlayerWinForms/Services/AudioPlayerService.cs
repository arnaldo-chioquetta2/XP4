using System;
using System.Collections.Generic;
using NAudio.Wave; // Essencial
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
        private IWavePlayer _waveOut;

        // ALTERAÇÃO 1: Mudamos de 'AudioFileReader' para 'WaveStream'
        // 'WaveStream' é o pai genérico que aceita tanto MP3 puro quanto arquivos do MediaFoundation
        private WaveStream _audioFile;
        private MediaPlayer _mediaPlayer;

        private List<Track> _playlist;
        private int _currentIndex = -1;

        public event EventHandler<Track> TrackChanged;
        //public event EventHandler PlaybackStopped;
        public event EventHandler<float[]> FftDataReceived;
        public event EventHandler<Tuple<Track, string>> PlaybackError;
        private SampleAggregator _aggregator;

        // NOVO EVENTO: Esse é o segredo para o Spectrum funcionar!
        // Ele avisa: "Ei, carreguei um áudio novo, quem quiser desenhar o gráfico, pega aqui!"
        //public event EventHandler<WaveStream> AudioSourceCreated;
        // Propriedades para ler o tempo
        public TimeSpan CurrentTime => _audioFile?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalTime => _audioFile?.TotalTime ?? TimeSpan.Zero;

        public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
        public Track CurrentTrack => (_currentIndex >= 0 && _currentIndex < _playlist.Count) ? _playlist[_currentIndex] : null;

        public AudioPlayerService()
        {
            _mediaPlayer = new MediaPlayer();
            _playlist = new List<Track>();
            _mediaPlayer.MediaEnded += _mediaPlayer_MediaEnded;
        }

        #region Inicializacao

        public void SetPosition(double percentage)
        {
            if (_audioFile != null)
            {
                // Calcula o novo tempo baseada na porcentagem
                double totalSeconds = _audioFile.TotalTime.TotalSeconds;
                double newSeconds = totalSeconds * percentage;

                // Aplica no arquivo
                _audioFile.CurrentTime = TimeSpan.FromSeconds(newSeconds);
            }
        }


        #endregion


        private void _mediaPlayer_MediaEnded(object sender, EventArgs e)
        {
            Next();
        }

        public void SetPlaylist(List<Track> tracks)
        {
            _playlist = tracks;
        }

        public void Play(int index)
        {
            if (index < 0 || index >= _playlist.Count) return;

            Stop();
            _currentIndex = index;
            var track = _playlist[_currentIndex];

            try
            {
                // 1. Cria o Leitor (MediaFoundationReader para compatibilidade)
                var reader = new MediaFoundationReader(track.FilePath);
                _audioFile = reader; // Guarda referência para dispose

                // 2. ENVOLVE o leitor com o nosso SampleAggregator
                // O SampleProvider precisa ser convertido para float (ToSampleProvider)
                // _aggregator = new SampleAggregator(reader.ToSampleProvider());
                // _aggregator = new SampleAggregator(reader.ToSampleProvider(), 512);
                _aggregator = new SampleAggregator(reader.ToSampleProvider(), 256);

                // 3. Assina o evento do FFT
                _aggregator.FftCalculated += (s, args) =>
                {
                    // Repassa os dados para quem estiver ouvindo (MainForm)
                    FftDataReceived?.Invoke(this, args.Result);
                };

                // 4. Inicia o WaveOut usando o AGGREGATOR como fonte, não o reader direto
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_aggregator); // O áudio passa por dentro do aggregator agora!

                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Play();

                TrackChanged?.Invoke(this, track);
            }
            catch (Exception ex)
            {
                RegistrarLogErro(track, ex);
                // Agora passamos a Track E a Mensagem
                PlaybackError?.Invoke(this, new Tuple<Track, string>(track, $"Erro: {ex.Message}"));
            }
        }

        private void RegistrarLogErro(Track track, Exception ex)
        {
            try
            {
                string arquivoLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log_Erros_Playback.txt");
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("==================================================");
                sb.AppendLine($"DATA/HORA: {DateTime.Now}");
                sb.AppendLine($"MÚSICA ID: {track.Id}");
                sb.AppendLine($"TÍTULO:    {track.Title}");
                sb.AppendLine($"BANDA:     {track.BandName}");
                sb.AppendLine($"CAMINHO:   {track.FilePath}");

                // Verifica detalhes físicos do arquivo
                if (File.Exists(track.FilePath))
                {
                    var info = new FileInfo(track.FilePath);
                    sb.AppendLine($"TAMANHO:   {info.Length} bytes");
                    sb.AppendLine($"CRIADO EM: {info.CreationTime}");

                    if (info.Length == 0)
                        sb.AppendLine("ALERTA:    O ARQUIVO ESTÁ VAZIO (0 BYTES).");
                }
                else
                {
                    sb.AppendLine("ALERTA:    ARQUIVO NÃO ENCONTRADO NO DISCO.");
                }

                sb.AppendLine("--------------------------------------------------");
                sb.AppendLine($"MENSAGEM:  {ex.Message}");

                // Pega o código Hexadecimal do erro (ex: 0xC00D36C4)
                sb.AppendLine($"HRESULT:   0x{ex.HResult:X}");
                sb.AppendLine($"SOURCE:    {ex.Source}");
                sb.AppendLine($"STACK:     {ex.StackTrace}");
                sb.AppendLine("==================================================");
                sb.AppendLine(""); // Linha em branco

                // Grava no arquivo (Append para não apagar os anteriores)
                File.AppendAllText(arquivoLog, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Se der erro ao gravar o log, não podemos fazer nada para não travar o app
            }
        }

        public void TogglePlayPause()
        {
            if (_waveOut == null)
            {
                if (_playlist.Count > 0) Play(0);
                return;
            }

            if (_waveOut.PlaybackState == PlaybackState.Playing)
                _waveOut.Pause();
            else
                _waveOut.Play();
        }

        public void Stop()
        {
            if (_waveOut != null)
            {
                // IMPORTANTE: Removemos o evento para ele não achar que a música acabou sozinha
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
        }

        public void Next()
        {
            if (_playlist.Count == 0) return;

            // Verifica se não é a última
            if (_currentIndex < _playlist.Count - 1)
            {
                Play(_currentIndex + 1);
            }
            else
            {
                // Se for a última, volta para a primeira (Loop da lista)
                // Se não quiser loop, basta remover esta linha
                Play(0);
            }
        }
        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            // Se houver exceção, paramos por erro
            if (e.Exception != null)
            {
                PlaybackError?.Invoke(this, new Tuple<Track, string>(CurrentTrack, $"Erro na reprodução: {e.Exception.Message}"));
                return;
            }

            // LÓGICA DE AUTO-PRÓXIMA:
            // Se o WaveOut parou e não foi porque nós chamamos o Stop() manualmente para trocar de música,
            // então significa que a música chegou ao fim.

            // Verificamos se ainda temos playlist
            if (_playlist != null && _currentIndex < _playlist.Count - 1)
            {
                // Toca a próxima
                Next();
            }
            else if (_playlist != null && _currentIndex >= _playlist.Count - 1)
            {
                // Se era a última, volta para a primeira (Loop)
                Play(0);
            }
        }

        public void Dispose()
        {
            Stop();
        }

        #region Apagar


        // 2. Insere na tabela de Apagar Futuramente (Caso falhe agora)
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

        // 3. Remove a música de TODAS as playlists e do cadastro principal
        public void RemoverMusicaDefinitivamente(int trackId)
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Remove das playlists (Tabela de ligação)
                        var cmd1 = new SQLiteCommand("DELETE FROM PlaylistTracks WHERE TrackId = @Id", connection, transaction);
                        cmd1.Parameters.AddWithValue("@Id", trackId);
                        cmd1.ExecuteNonQuery();

                        // Remove do cadastro de faixas
                        var cmd2 = new SQLiteCommand("DELETE FROM Tracks WHERE Id = @Id", connection, transaction);
                        cmd2.Parameters.AddWithValue("@Id", trackId);
                        cmd2.ExecuteNonQuery();

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw; // Repassa o erro se der
                    }
                }
            }
        }
        #endregion

    }
}