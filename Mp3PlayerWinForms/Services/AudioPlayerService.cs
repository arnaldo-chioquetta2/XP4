using Mp3PlayerWinForms.Services;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        private EqualizerSampleProvider _equalizerProvider;
        private List<Track> _playlist;
        private int _currentIndex = -1;
        private bool _avancandoAutomaticamente;

        public event EventHandler<Track> TrackChanged;
        public event EventHandler<Track> TrackFinishedNaturally;
        public event EventHandler<float[]> FftDataReceived;
        public event EventHandler<Tuple<Track, string>> PlaybackError;
        private SampleAggregator _aggregator;

        private readonly ProgrammingRepository _progRepo;
        private bool _programacaoAtiva;

        // --- ADICIONAR ESTES CAMPOS ---
        private SilenceDetector _silenceDetector;
        private TrackRepository _trackRepo;

        // --- ADICIONAR ESTE EVENTO ---
        public event Action<string> OnStatusCueChanged;

        public int CurrentPlaylistId { get; set; } = -1;

        public TimeSpan CurrentTime => _audioFile?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalTime => _audioFile?.TotalTime ?? TimeSpan.Zero;
        public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
        public Track CurrentTrack => (_currentIndex >= 0 && _currentIndex < _playlist.Count) ? _playlist[_currentIndex] : null;

        private void _mediaPlayer_MediaEnded(object sender, EventArgs e) => Next();
        public void SetPlaylist(List<Track> tracks) => _playlist = tracks;

        private readonly ProgrammingService _progService = new ProgrammingService();

        public event EventHandler<int> SolicitarTrocaDePlaylist;

        //private readonly SilenceDetector _silenceDetector = new SilenceDetector();
        //private readonly TrackRepository _trackRepo = new TrackRepository();

        // Limiar de silêncio: 0.01f costuma ser excelente para ignorar chiados e 
        // detectar o início real da música.
        private const float SilenceThreshold = 0.01f;

        //public event Action<string> OnStatusCueChanged;

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
            // 1. O que já era seu (Git)
            _mediaPlayer = new MediaPlayer();
            _playlist = new List<Track>();
            _mediaPlayer.MediaEnded += _mediaPlayer_MediaEnded;

            // 2. Os motores para o AUTO-CUE (Essenciais para o que fizemos hoje)
            _silenceDetector = new SilenceDetector(); // O "ouvido" do programa
            _trackRepo = new TrackRepository();       // O "escritor" do banco

            // 3. Sua lógica de programação (Fase 3.1)
            _progRepo = new ProgrammingRepository();
            SincronizarConfiguracoesIniciais();

            // 4. Logs e Debug
            try { File.Delete("debug_audio_log.txt"); } catch { }
            GravarLog("=== INICIANDO SERVIÇO DE ÁUDIO (WAVEOUT + AUTO-CUE) ===");
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
            GravarLog("--- Buscando Caixas de Som Definitivas ---");
            int waveOutCount = WaveOut.DeviceCount;

            // TENTATIVA 1: O Tiro Certo (Procura estritamente por USB ou Alto-falantes)
            for (int i = 0; i < waveOutCount; i++)
            {
                try
                {
                    var caps = WaveOut.GetCapabilities(i);
                    string nomeLower = caps.ProductName.ToLower();

                    // Se tem "usb" ou "alto-falante" no nome, não tem erro, é a sua caixa de som!
                    if (nomeLower.Contains("usb") || nomeLower.Contains("alto-falante"))
                    {
                        GravarLog($" -> ALVO DETECTADO COM SUCESSO (ID {i}): {caps.ProductName}");
                        return i;
                    }
                }
                catch { }
            }

            // TENTATIVA 2: Se por acaso o USB for desconectado, pega qualquer coisa que NÃO seja TV
            for (int i = 0; i < waveOutCount; i++)
            {
                try
                {
                    var caps = WaveOut.GetCapabilities(i);
                    string nomeLower = caps.ProductName.ToLower();

                    // Rejeita ativamente Philips, placas de vídeo (Nvidia/AMD) e cabos Display/HDMI
                    if (!nomeLower.Contains("nvidia") && !nomeLower.Contains("philips")
                        && !nomeLower.Contains("amd") && !nomeLower.Contains("display"))
                    {
                        GravarLog($" -> USANDO POR ELIMINAÇÃO (ID {i}): {caps.ProductName}");
                        return i;
                    }
                }
                catch { }
            }

            // TENTATIVA 3: Se der pane total, usa o Padrão do Windows
            GravarLog("*** CAIXAS NÃO ENCONTRADAS. USANDO PADRÃO DO WINDOWS (-1) ***");
            return -1;
        }

        public void Play(int index)
        {
            if (index < 0 || index >= _playlist.Count) return;

            Stop();
            _currentIndex = index;
            var track = _playlist[_currentIndex];

            GravarLog($"Iniciando Play (WaveOut + AutoCue): {track.Title}");

            try
            {
                // 1. Leitor
                var reader = new MediaFoundationReader(track.FilePath);
                _audioFile = reader;

                // --- 2. LÓGICA DO AUTO-CUE ---
                if (track.CutIni == -1)
                {
                    NotificarStatusCue("Analisando Silêncio...");
                    GravarLog($"[AUTO-CUE] Analisando silêncio para: {track.Title}");

                    track.CutIni = _silenceDetector.AnalisarCutIni(track.FilePath);

                    Task.Run(() =>
                    {
                        try
                        {
                            track.CutFim = _silenceDetector.AnalisarCutFim(track.FilePath);
                            _trackRepo.AtualizarCortesMusica(track.Id, track.CutIni, track.CutFim);

                            string feedback = $"Auto-Cue: Início {track.CutIni}s | Fim {track.CutFim}s";
                            NotificarStatusCue(feedback);
                            GravarLog($"[AUTO-CUE] " + feedback);
                        }
                        catch (Exception exTask)
                        {
                            NotificarStatusCue("Erro na análise de fim.");
                            GravarLog($"[AUTO-CUE_ERRO] {exTask.Message}");
                        }
                    });
                }
                else
                {
                    string msg = (track.CutIni == 0 && track.CutFim == 0)
                        ? "Sem cortes"
                        : $"Auto-Cue Ativo: {track.CutIni}s / {track.CutFim}s";
                    NotificarStatusCue(msg);
                }

                // Aplicação do Corte Inicial
                if (track.CutIni > 0)
                {
                    _audioFile.CurrentTime = TimeSpan.FromSeconds(track.CutIni);
                    GravarLog($"[AUDIO] Saltando para {track.CutIni}s");
                }
                // ---------------------------------

                var sampleProvider = reader.ToSampleProvider();
                _equalizerProvider = new EqualizerSampleProvider(sampleProvider, ObterBandasDaTrack(track));

                // 3. Aggregator
                _aggregator = new SampleAggregator(_equalizerProvider, 256);
                _aggregator.FftCalculated += (s, args) => NotificarFft(args.Result);

                // 4. Volume e Proteção do Visual Studio
                _volumeProvider = new VolumeSampleProvider(_aggregator);

                if (System.Diagnostics.Debugger.IsAttached)
                {
                    _volumeProvider.Volume = _volume * 0.02f; // Baixinho enquanto programa
                    GravarLog($"[DEBUG] Volume IDE limitado.");
                }
                else
                {
                    _volumeProvider.Volume = _volume;
                }

                // 5. O pulo do gato para drivers genéricos (Retorno para 16-bit)
                var finalWaveProvider = new SampleToWaveProvider16(_volumeProvider);

                // 6. Seleção Dinâmica do Dispositivo (A sua inteligência do Git)
                int deviceId = ObterIndiceDispositivoWaveOut();

                // 7. Inicialização WaveOutEvent
                _waveOut = new WaveOutEvent();
                _waveOut.DeviceNumber = deviceId;
                _waveOut.DesiredLatency = 200;
                _waveOut.NumberOfBuffers = 2;

                _waveOut.Init(finalWaveProvider);
                GravarLog("WaveOut Init OK.");

                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Play();
                GravarLog("Playback Iniciado.");

                NotificarTrackChanged(track);
            }
            catch (Exception ex)
            {
                GravarLog($"ERRO FATAL: {ex.Message}\n{ex.StackTrace}");
                RegistrarLogErro(track, ex);
                NotificarPlaybackError(track, $"Erro: {ex.Message}");
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
                _equalizerProvider = null;
                _volumeProvider = null;
            }
            catch (Exception ex)
            {
                GravarLog($"Erro ao parar: {ex.Message}");
            }
        }

        public void PreviewEqualizerBands(int[] bandValues, bool ativa)
        {
            _equalizerProvider?.UpdateBands(ativa ? bandValues : EqualizerPreset.CreateFlatBands());
        }

        public void RestaurarEqualizacaoDaTrackAtual()
        {
            if (CurrentTrack == null)
            {
                return;
            }

            _equalizerProvider?.UpdateBands(ObterBandasDaTrack(CurrentTrack));
        }

        public void AplicarEqualizacaoDaTrack(Track track)
        {
            if (track == null || CurrentTrack == null || track.Id != CurrentTrack.Id)
            {
                return;
            }

            _equalizerProvider?.UpdateBands(ObterBandasDaTrack(track));
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
            try
            {
                if (e.Exception != null)
                {
                    GravarLog($"Parada com erro: {e.Exception.Message}");
                    NotificarPlaybackError(CurrentTrack, $"Erro no audio: {e.Exception.Message}");
                    return;
                }

                if (_avancandoAutomaticamente)
                {
                    GravarLog("Ignorando PlaybackStopped reentrante.");
                    return;
                }

                _avancandoAutomaticamente = true;
                var faixaFinalizada = CurrentTrack;
                if (faixaFinalizada != null)
                {
                    NotificarTrackFinishedNaturally(faixaFinalizada);
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
                        NotificarTrocaPlaylist(idPlaylistProgramada.Value);
                        return;
                    }
                }

                // Fluxo normal caso não haja troca agendada
                if (_playlist != null && _currentIndex < _playlist.Count - 1) Next();
                else if (_playlist != null && _currentIndex >= _playlist.Count - 1) Play(0);
            }
            catch (Exception ex)
            {
                GravarLog($"Erro em OnPlaybackStopped: {ex.Message}\n{ex.StackTrace}");
                NotificarPlaybackError(CurrentTrack, $"Erro ao trocar musica: {ex.Message}");
            }
            finally
            {
                _avancandoAutomaticamente = false;
            }
        }

        public void Dispose() => Stop();
        public void AtualizarIndiceAposRemocao(int novoIndice) => this._currentIndex = novoIndice;

        private int[] ObterBandasDaTrack(Track track)
        {
            if (track == null || !track.EqualizacaoAtiva)
            {
                return EqualizerPreset.CreateFlatBands();
            }

            if (track.EqualizacaoBandas != null && track.EqualizacaoBandas.Any(v => v != 0))
            {
                return track.EqualizacaoBandas;
            }

            if (track.EqualizacaoPresetId <= 0)
            {
                return EqualizerPreset.CreateFlatBands();
            }

            var preset = _trackRepo.ObterPresetEqualizacao(track.EqualizacaoPresetId);
            return preset != null ? preset.ToBands() : EqualizerPreset.CreateFlatBands();
        }

        private void RegistrarLogErro(Track track, Exception ex)
        {
            try
            {
                string arquivoLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log_Erros_Playback.txt");
                File.AppendAllText(arquivoLog, $"{DateTime.Now} - {track?.Title ?? "Sem faixa"} - {ex.Message}\n{ex.StackTrace}\n");
            }
            catch { }
        }

        private void NotificarStatusCue(string mensagem)
        {
            try { OnStatusCueChanged?.Invoke(mensagem); }
            catch (Exception ex) { GravarLog($"Erro em OnStatusCueChanged: {ex.Message}"); }
        }

        private void NotificarFft(float[] data)
        {
            try { FftDataReceived?.Invoke(this, data); }
            catch (Exception ex) { GravarLog($"Erro em FftDataReceived: {ex.Message}"); }
        }

        private void NotificarTrackChanged(Track track)
        {
            try { TrackChanged?.Invoke(this, track); }
            catch (Exception ex) { GravarLog($"Erro em TrackChanged: {ex.Message}"); }
        }

        private void NotificarTrackFinishedNaturally(Track track)
        {
            try { TrackFinishedNaturally?.Invoke(this, track); }
            catch (Exception ex) { GravarLog($"Erro em TrackFinishedNaturally: {ex.Message}"); }
        }

        private void NotificarPlaybackError(Track track, string mensagem)
        {
            try { PlaybackError?.Invoke(this, new Tuple<Track, string>(track, mensagem)); }
            catch (Exception ex) { GravarLog($"Erro em PlaybackError: {ex.Message}"); }
        }

        private void NotificarTrocaPlaylist(int playlistId)
        {
            try { SolicitarTrocaDePlaylist?.Invoke(this, playlistId); }
            catch (Exception ex) { GravarLog($"Erro em SolicitarTrocaDePlaylist: {ex.Message}"); }
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
