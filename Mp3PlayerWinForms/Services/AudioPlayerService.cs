using Mp3PlayerWinForms.Services;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Windows.Media;
using XP3.Data;
using XP3.Features.Programming;
using XP3.Models;

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

        private readonly ProgrammingRepository _progRepo;
        private bool _programacaoAtiva;
        public int CurrentPlaylistId { get; set; } = -1;

        public TimeSpan CurrentTime => _audioFile?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalTime => _audioFile?.TotalTime ?? TimeSpan.Zero;
        public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
        public Track CurrentTrack => (_currentIndex >= 0 && _currentIndex < _playlist.Count) ? _playlist[_currentIndex] : null;

        private void _mediaPlayer_MediaEnded(object sender, EventArgs e) => Next();
        public void SetPlaylist(List<Track> tracks) => _playlist = tracks;

        private readonly ProgrammingService _progService = new ProgrammingService();

        public event EventHandler<int> SolicitarTrocaDePlaylist;

        public bool ProgramacaoAtiva
        {
            get => _programacaoAtiva;
            set
            {
                _programacaoAtiva = value;
                _progRepo.SalvarEstadoProgramacao(value);
                GravarLog($"[PLAYER] Programação alterada para: {(value ? "LIGADA" : "DESLIGADA")}");
            }
        }

        public AudioPlayerService()
        {
            _mediaPlayer = new MediaPlayer();
            _playlist = new List<Track>();
            _mediaPlayer.MediaEnded += _mediaPlayer_MediaEnded;

            // --- INICIALIZAÇÃO FASE 3.1 ---
            _progRepo = new ProgrammingRepository();
            SincronizarConfiguracoesIniciais();

            try { File.Delete("debug_audio_log.txt"); } catch { }
            GravarLog("=== INICIANDO SERVIÇO DE ÁUDIO (MÓDULO WAVEOUT LEGACY) ===");
        }

        private void SincronizarConfiguracoesIniciais()
        {
            try
            {
                var config = _progRepo.ObterConfiguracao();
                _programacaoAtiva = config.ProgramacaoAtiva;
                GravarLog($"[PLAYER] Configuração inicial carregada: {(_programacaoAtiva ? "Ativa" : "Inativa")}");
            }
            catch (Exception ex)
            {
                GravarLog($"[ERRO] Falha ao sincronizar config inicial: {ex.Message}");
                _programacaoAtiva = false;
            }
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

        // METODO: ForcarVerificacaoProgramacao
        // VERSÃO: 1.0
        // MOTIVO: Usado no startup ou ao ligar o botão Auto para carregar a lista correta imediatamente.
        public void ForcarVerificacaoProgramacao()
        {
            if (!_programacaoAtiva) return;

            var todasProgramacoes = _progRepo.ListarProgramacao();
            int? idPlaylistProgramada = _progService.SugerirPlaylistPorHorario(todasProgramacoes);

            // Se existe uma lista ideal para agora, e ela for diferente da que está aberta
            if (idPlaylistProgramada.HasValue && idPlaylistProgramada.Value != CurrentPlaylistId)
            {
                GravarLog($"[AGENDADOR] Correção Imediata! Carregando a lista {idPlaylistProgramada.Value} apropriada para agora.");

                // Dispara o evento para a tela Inicial carregar as músicas
                SolicitarTrocaDePlaylist?.Invoke(this, idPlaylistProgramada.Value);
            }
        }

        public void SetPosition(double percentage)
        {
            if (_audioFile != null)
            {
                _audioFile.CurrentTime = TimeSpan.FromSeconds(_audioFile.TotalTime.TotalSeconds * percentage);
            }
        }

        private int ObterIndiceDispositivoWaveOut()
        {
            GravarLog("--- Listando Dispositivos WaveOut (Legado) ---");

            int waveOutCount = WaveOut.DeviceCount;
            int dispositivoEscolhido = -1;

            for (int i = 0; i < waveOutCount; i++)
            {
                try
                {
                    var caps = WaveOut.GetCapabilities(i);
                    string nome = caps.ProductName;
                    string nomeLower = nome.ToLower();

                    GravarLog($"ID {i}: {nome}");

                    if ((nomeLower.Contains("high definition") ||
                         nomeLower.Contains("high def") ||
                         nomeLower.Contains("usb") ||
                         nomeLower.Contains("pnp"))
                        && !nomeLower.Contains("nvidia"))
                    {
                        GravarLog($" -> ALVO DETECTADO (ID {i}): {nome}");
                        dispositivoEscolhido = i;
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


        // METODO: Play
        // VERSÃO: 5.0
        // MOTIVO: Uso do Mp3FileReader (Modo ACM/VB6) para compatibilidade com placa USB.
        public void Play(int index)
        {
            if (index < 0 || index >= _playlist.Count) return;

            Stop();
            _currentIndex = index;
            var track = _playlist[_currentIndex];

            GravarLog($"Iniciando Play (Modo Legado VB6 / ACM): {track.Title}");

            try
            {
                WaveStream reader;

                if (track.FilePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    reader = new Mp3FileReader(track.FilePath);
                }
                else
                {
                    reader = new MediaFoundationReader(track.FilePath);
                }

                _audioFile = reader;

                _aggregator = new SampleAggregator(reader.ToSampleProvider(), 256);
                _aggregator.FftCalculated += (s, args) => FftDataReceived?.Invoke(this, args.Result);

                _volumeProvider = new VolumeSampleProvider(_aggregator);

#if DEBUG
                // Em modo Debug, o som de saída é limitado a 1% do volume selecionado
                _volumeProvider.Volume = _volume * 0.01f;
                GravarLog($"[DEBUG] Volume de saída limitado por segurança: {_volumeProvider.Volume}");
#else
                _volumeProvider.Volume = _volume;
#endif

                var finalWaveProvider = new SampleToWaveProvider16(_volumeProvider);

                _waveOut = new WaveOutEvent();
                _waveOut.DeviceNumber = -1;
                _waveOut.DesiredLatency = 200;
                _waveOut.NumberOfBuffers = 2;

                _waveOut.Init(finalWaveProvider);
                GravarLog("WaveOut Init OK (Modo ACM).");

                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Play();
                GravarLog("Playback Iniciado com sucesso.");

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

        // METODO: OnPlaybackStopped
        // VERSÃO: 2.0
        // MOTIVO: Intercepta o fim da faixa para verificar se há uma troca de playlist agendada antes de tocar a próxima música.
        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                GravarLog($"Parada com erro: {e.Exception.Message}");
                return;
            }

            // GATILHO DA PROGRAMAÇÃO (Requisito 2.1)
            if (_programacaoAtiva)
            {
                var todasProgramacoes = _progRepo.ListarProgramacao();
                int? idPlaylistProgramada = _progService.SugerirPlaylistPorHorario(todasProgramacoes);

                // Se o agendamento diz que devemos estar em uma playlist DIFERENTE da atual
                if (idPlaylistProgramada.HasValue && idPlaylistProgramada.Value != CurrentPlaylistId)
                {
                    GravarLog($"[AGENDADOR] Mudança detectada: Saindo de {CurrentPlaylistId} para {idPlaylistProgramada.Value}");
                    SolicitarTrocaDePlaylist?.Invoke(this, idPlaylistProgramada.Value);
                    return;
                }

            }

            // Fluxo normal caso não haja troca agendada
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