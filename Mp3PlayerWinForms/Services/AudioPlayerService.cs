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
        private float _volumeManual = AppSettings.InitialVolume;
        private float _fatorNormalizacaoAtual = 1.0f;
        private bool _normalizacaoAtiva;

        // Voltamos para WaveOutEvent, que é a API mais compatível
        private WaveOutEvent _waveOut;

        private WaveStream _audioFile;
        private MediaPlayer _mediaPlayer;
        private VolumeSampleProvider _volumeProvider;
        private EqualizerSampleProvider _equalizerProvider;
        private List<Track> _playlist;
        private int _currentIndex = -1;
        private bool _isNextCallInitiated = false;
        private bool _handlingPlaybackStopped = false;
        public bool AplicarRegraPularPulado { get; set; } = true;

        public event EventHandler<Track> TrackChanged;
        public event EventHandler<Track> TrackFinishedNaturally;
        public event EventHandler<float[]> FftDataReceived;
        public event Action<int, double> TrackMaxVolMeasured;
        public event Action<string> StatusVolumeChanged;
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
        public Track CurrentTrack => (_playlist != null && _currentIndex >= 0 && _currentIndex < _playlist.Count) ? _playlist[_currentIndex] : null;

        // NOVO: Propriedade de controle de volume
        public float Volume
        {
            get => _volumeManual;
            set
            {
                _volumeManual = Math.Max(0.1f, Math.Min(1.0f, value));
                AplicarVolumeEfetivo();
            }
        }

        public bool NormalizacaoAtiva
        {
            get => _normalizacaoAtiva;
            set
            {
                if (_normalizacaoAtiva == value)
                    return;

                _normalizacaoAtiva = value;
                AplicarVolumeEfetivo();
            }
        }

        private void _mediaPlayer_MediaEnded(object sender, EventArgs e) => Next();
        public void SetPlaylist(List<Track> tracks) => _playlist = tracks ?? new List<Track>();

        private readonly ProgrammingService _progService = new ProgrammingService();

        public event EventHandler<int> SolicitarTrocaDePlaylist;

        //private readonly SilenceDetector _silenceDetector = new SilenceDetector();
        //private readonly TrackRepository _trackRepo = new TrackRepository();

        // Limiar de silêncio: 0.01f costuma ser excelente para ignorar chiados e 
        // detectar o início real da música.
        private const float SilenceThreshold = 0.01f;

        // --- NOVIDADE: Variáveis de controle de agendamento ---
        private int? _lastKnownScheduledPlaylistId = null;
        private bool _userOverriddenProgrammedPlaylist = false;
        // ----------------------------------------------------
        private bool _medindoMaxVolAtual;
        private double _maxVolMedidoAtual;
        private int? _trackIdMedindoMaxVol;
        private DateTime _ultimaNotificacaoMaxVol = DateTime.MinValue;
        private DateTime _ultimaLogPeakRecebido = DateTime.MinValue;
        private const double MaxVolInvalidLegacyThreshold = 10d;

        private bool ListaAtualEhAEscolher()
        {
            try
            {
                if (_trackRepo == null || CurrentPlaylistId <= 0)
                    return false;

                string nomeLista = _trackRepo.GetPlaylistName(CurrentPlaylistId);
                return string.Equals(nomeLista, "AESCOLHER", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private bool MusicaJaTemCueDefinido(Track track)
        {
            if (track == null)
                return false;

            return track.CutIni >= 0 && track.CutFim >= 0;
        }

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
            _lastKnownScheduledPlaylistId = idPlaylistProgramada;

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

        public void Play(int index) => Play(index, false, true); // Atualizar esta linha

        public void PlayAutomatico(int index, bool ignorarBloqueio24Horas = false)
        {
            GravarLog($"[PLAY_AUTO] Solicitado index={index}; ignorar24h={ignorarBloqueio24Horas}; aplicarPular={AplicarRegraPularPulado}; playlistCount={_playlist?.Count ?? 0}");

            if (_playlist == null || _playlist.Count == 0)
            {
                GravarLog("[PLAY_AUTO] Ignorado: playlist vazia ou nula.");
                return;
            }

            if (AplicarRegraPularPulado)
            {
                TocarProximaFaixaValida(index);
                return;
            }

            Play(index, ignorarBloqueio24Horas, false);
        }

        public void Play(int index, bool ignorarBloqueio24Horas = false, bool isUserInitiated = false) // Modificar esta linha
        {
            GravarLog($"[PLAY] Solicitado index={index}; ignorar24h={ignorarBloqueio24Horas}; usuario={isUserInitiated}; playlistCount={_playlist?.Count ?? 0}; currentIndex={_currentIndex}");
            NotificarStatusVolume(null);
            System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL] Play entrou index={index} playlist={CurrentPlaylistId}");

            if (_playlist == null || _playlist.Count == 0)
            {
                GravarLog("[PLAY] Ignorado: playlist vazia ou nula.");
                System.Diagnostics.Debug.WriteLine("[NORM/MAXVOL] Play abortado: playlist vazia ou nula");
                return;
            }

            if (isUserInitiated)
            {
                _userOverriddenProgrammedPlaylist = true;
                GravarLog("[PLAYER] Usuário iniciou playback. Programação automática temporariamente desativada.");
            }

            if (!TryEncontrarFaixaTocavel(index, ignorarBloqueio24Horas, out int indiceTocavel, out Track track, out string motivo))
            {
                GravarLog(motivo);
                System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL] Play abortado: {motivo}");
                Stop();
                NotificarPlaybackError(CurrentTrack, motivo);
                return;
            }

            if (MaxVolEhInvalidoOuLegado(track.MaxVol))
                track.MaxVol = null;

            GravarLog($"[PLAY] Faixa selecionada indexReal={indiceTocavel}; ID={track.Id}; Titulo={track.Title}; Arquivo={track.FilePath}");
            System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL] CurrentTrack id={track.Id} titulo={track.Title} MaxVol={(track.MaxVol.HasValue ? track.MaxVol.Value.ToString("0.###") : "null")}");

            if (!isUserInitiated && AplicarRegraPularPulado && track.Pular > 0 && track.Pulado < track.Pular)
            {
                int puladoAntes = track.Pulado;
                int novoPulado = _trackRepo.IncrementarPulado(track.Id);
                AtualizarPuladoEmMemoria(track.Id, novoPulado);
                GravarLog($"[PULAR] BLOQUEADO ANTES DE TOCAR id={track.Id}; titulo={track.Title}; pular={track.Pular}; puladoAntes={puladoAntes}; puladoDepois={novoPulado}; origem=automatico");
                TocarProximaFaixaValida(indiceTocavel + 1);
                return;
            }

            if (!isUserInitiated)
            {
                GravarLog($"[PULAR] VAI TOCAR id={track.Id} titulo={track.Title} pular={track.Pular} pulado={track.Pulado} aplicar={AplicarRegraPularPulado} origem=automatico");
            }
            else
            {
                GravarLog($"[PULAR] VAI TOCAR id={track.Id} titulo={track.Title} pular={track.Pular} pulado={track.Pulado} aplicar={AplicarRegraPularPulado} origem=manual");
            }

            Stop();
            _currentIndex = indiceTocavel;

            GravarLog($"Iniciando Play (WaveOut + AutoCue): {track.Title}");

            try
            {
                // 1. Leitor
                var reader = new MediaFoundationReader(track.FilePath);
                _audioFile = reader;

                // --- 2. LÓGICA DO AUTO-CUE ---
                bool deveExecutarAutoCue = !MusicaJaTemCueDefinido(track);
                if (deveExecutarAutoCue)
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
                    NotificarStatusCue(null);
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
                System.Diagnostics.Debug.WriteLine("[NORM/MAXVOL] SampleAggregator criado");
                _aggregator.FftCalculated += (s, args) => NotificarFft(args.Result);
                _aggregator.PeakMeasured += Aggregator_PeakMeasured;
                System.Diagnostics.Debug.WriteLine("[NORM/MAXVOL] PeakMeasured assinado");

                IniciarMedicaoMaxVolAtual(track);

                // 4. Volume e Proteção do Visual Studio
                _volumeProvider = new VolumeSampleProvider(_aggregator);

                AtualizarFatorNormalizacaoAtual(track);

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
                GravarLog($"[PLAY] TrackChanged notificado: ID={track.Id}; Posicao={_audioFile?.CurrentTime}; Total={_audioFile?.TotalTime}");
            }
            catch (Exception ex)
            {
                GravarLog($"ERRO FATAL: {ex.Message}\n{ex.StackTrace}");
                RegistrarLogErro(track, ex);

                if (ArquivoNaoEncontrado(ex))
                {
                    GravarLog($"[AUDIO] Faixa ausente ao abrir. Pulando para a próxima valida.");
                    TocarProximaFaixaValida(_currentIndex + 1);
                    return;
                }

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
                FinalizarMedicaoMaxVolAtual();
                GravarLog($"[STOP] Solicitado; waveOutState={_waveOut?.PlaybackState.ToString() ?? "null"}; audioFile={_audioFile?.GetType().Name ?? "null"}");
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
                GravarLog("[STOP] Concluido.");
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
            GravarLog($"[NEXT] Solicitado; playlistCount={_playlist?.Count ?? 0}; currentIndex={_currentIndex}; waveOutState={_waveOut?.PlaybackState.ToString() ?? "null"}");
            if (_playlist == null || _playlist.Count == 0)
            {
                GravarLog("[NEXT] Ignorado: playlist vazia ou nula.");
                return;
            }
            _isNextCallInitiated = true;

            if (_waveOut != null)
            {
                _waveOut.Stop();
                return;
            }

            TocarProximaFaixaValida(_currentIndex + 1);
        }

        // METODO: OnPlaybackStopped
        // VERSÃO: 2.0
        // MOTIVO: Intercepta o fim da faixa para verificar se há uma troca de playlist agendada antes de tocar a próxima música.
        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            try
            {
                FinalizarMedicaoMaxVolAtual();
                GravarLog($"[STOPPED] Entrou; handling={_handlingPlaybackStopped}; next={_isNextCallInitiated}; currentIndex={_currentIndex}; exception={e.Exception?.Message ?? "null"}");

                if (_handlingPlaybackStopped)
                {
                    GravarLog("Ignorando PlaybackStopped reentrante.");
                    return;
                }

                _handlingPlaybackStopped = true;

                if (e.Exception != null)
                {
                    GravarLog($"Parada com erro: {e.Exception.Message}");
                    NotificarPlaybackError(CurrentTrack, $"Erro no audio: {e.Exception.Message}");
                    _handlingPlaybackStopped = false;
                    return;
                }

                var faixaFinalizada = CurrentTrack;
                bool finishedNaturally = false;

                if (faixaFinalizada != null && _audioFile != null)
                {
                    TimeSpan intendedEndTime = faixaFinalizada.CutFim > 0
                        ? TimeSpan.FromSeconds(faixaFinalizada.CutFim)
                        : _audioFile.TotalTime;

                    finishedNaturally = Math.Abs((_audioFile.CurrentTime - intendedEndTime).TotalSeconds) <= 1;
                    GravarLog($"[STOPPED] Faixa={faixaFinalizada.Id}; atual={_audioFile.CurrentTime}; fimPrevisto={intendedEndTime}; natural={finishedNaturally}; next={_isNextCallInitiated}");

                    if (finishedNaturally || _isNextCallInitiated)
                    {
                        DateTime playedAt = DateTime.Now;
                        _trackRepo.AtualizarUltimaReproducao(faixaFinalizada.Id, playedAt);
                        faixaFinalizada.LastPlayedAt = playedAt;
                        NotificarTrackFinishedNaturally(faixaFinalizada);
                    }
                }

                // GATILHO DA PROGRAMAÇÃO (Requisito 2.1)
                if (finishedNaturally && ListaAtualEhAEscolher() && !ExisteProximaFaixaValidaSemWrap())
                {
                    var todasProgramacoesAEscolher = _progRepo.ListarProgramacao();
                    int? idPlaylistProgramadaAEscolher = _progService.SugerirPlaylistPorHorario(todasProgramacoesAEscolher);

                    if (idPlaylistProgramadaAEscolher.HasValue && idPlaylistProgramadaAEscolher.Value != CurrentPlaylistId)
                    {
                        GravarLog($"[AESCOLHER] Fim natural detectado. Carregando lista programada {idPlaylistProgramadaAEscolher.Value} mesmo com programação {(_programacaoAtiva ? "LIGADA" : "DESLIGADA")}.");
                        NotificarTrocaPlaylist(idPlaylistProgramadaAEscolher.Value);
                        _handlingPlaybackStopped = false;
                        return;
                    }

                    GravarLog("[AESCOLHER] Fim natural detectado, mas nenhuma lista programada diferente foi encontrada.");
                    return;
                }

                if (_programacaoAtiva && !ListaAtualEhAEscolher())
                {
                    var todasProgramacoes = _progRepo.ListarProgramacao();
                    int? idPlaylistProgramada = _progService.SugerirPlaylistPorHorario(todasProgramacoes);

           // Lógica para resetar override do usuário e atualizar o último ID de playlist agendada conhecida
                    if (idPlaylistProgramada.HasValue)
                    {
                        if (!_lastKnownScheduledPlaylistId.HasValue || idPlaylistProgramada.Value != _lastKnownScheduledPlaylistId.Value)
                        {
                            _userOverriddenProgrammedPlaylist = false; // A playlist agendada mudou, resetar override
                            _lastKnownScheduledPlaylistId = idPlaylistProgramada.Value;
                            GravarLog($"[AGENDADOR] Playlist agendada mudou para {idPlaylistProgramada.Value}. Override de usuário resetado.");
                        }
                    }
                    else // Não há playlist programada para o horário atual
                    {
                        if (_lastKnownScheduledPlaylistId.HasValue) // Se havia uma playlist programada antes, mas agora não há
                        {
                            _userOverriddenProgrammedPlaylist = false; // Resetar override
                            _lastKnownScheduledPlaylistId = null; // Limpar o último ID de playlist agendada conhecida
                            GravarLog($"[AGENDADOR] Nenhuma playlist programada detectada. Override de usuário resetado.");
                        }
                    }

                    // Lógica para decidir se deve haver uma troca programada
                    if (idPlaylistProgramada.HasValue && !_userOverriddenProgrammedPlaylist)
                    {
                        // Existe uma playlist programada e o usuário NÃO a sobrepôs (ou a sobreposição foi resetada)
                        // Agora, verificamos se a playlist programada é diferente da que está tocando no momento
                        if (idPlaylistProgramada.Value != CurrentPlaylistId)
                        {
                            GravarLog($"[AGENDADOR] Mudança programada detectada: Saindo de {CurrentPlaylistId} para {idPlaylistProgramada.Value}");
                            NotificarTrocaPlaylist(idPlaylistProgramada.Value);
                            _handlingPlaybackStopped = false;
                            return; // Sai após agendar a troca
                        }
                        else
                        {
                            GravarLog($"[AGENDADOR] Playlist programada já é a atual ({CurrentPlaylistId}). Nenhuma ação necessária.");
                        }
                    }
                    else if (_userOverriddenProgrammedPlaylist)
                    {
                        // Existe uma playlist programada, mas o usuário a sobrepôs manualmente.
                        // A programação será ignorada até que um novo bloco programado inicie.
                        GravarLog($"[AGENDADOR] Mudança programada ignorada. Usuário sobrepôs a programação.");
                    }
                    // Se idPlaylistProgramada.HasValue é false, não há playlist programada para o horário, então nenhuma ação é necessária aqui.
                    // O fluxo continua para TocarProximaFaixaValida.
                }

                // Fluxo normal caso não haja troca agendada ou override
                // Substituir o if/else por TocarProximaFaixaValida para manter isUserInitiated=false
                TocarProximaFaixaValidaComSeguranca(_currentIndex + 1);
            }
            catch (Exception ex)
            {
                GravarLog($"Erro em OnPlaybackStopped: {ex.Message}\n{ex.StackTrace}");
                NotificarPlaybackError(CurrentTrack, $"Erro ao trocar musica: {ex.Message}");
                _handlingPlaybackStopped = false;
            }
            finally
            {
                _isNextCallInitiated = false;
                GravarLog("[STOPPED] Saiu do callback.");
            }
        }

        private void TocarProximaFaixaValidaComSeguranca(int indiceInicial)
        {
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(120).ConfigureAwait(false);
                    GravarLog($"[NEXT_SAFE] Iniciando proxima a partir do indice {indiceInicial}.");
                    TocarProximaFaixaValida(indiceInicial);
                }
                catch (Exception ex)
                {
                    GravarLog($"Erro ao iniciar proxima faixa com seguranca: {ex.Message}\n{ex.StackTrace}");
                    NotificarPlaybackError(CurrentTrack, $"Erro ao iniciar proxima musica: {ex.Message}");
                }
                finally
                {
                    _handlingPlaybackStopped = false;
                }
            });
        }

        private bool ExisteProximaFaixaValidaSemWrap()
        {
            if (_playlist == null || _playlist.Count == 0)
                return false;

            int inicio = _currentIndex + 1;
            if (inicio < 0)
                inicio = 0;

            for (int i = inicio; i < _playlist.Count; i++)
            {
                var track = _playlist[i];
                if (track == null || string.IsNullOrWhiteSpace(track.FilePath) || !File.Exists(track.FilePath))
                    continue;

                if (DevePularPorPularPulado(track))
                    continue;

                if (track.Pular > 0 && track.Pulado >= track.Pular)
                    continue;

                return true;
            }

            return false;
        }

        public void Dispose() => Stop();
        public void AtualizarIndiceAposRemocao(int novoIndice) => this._currentIndex = novoIndice;

        private int[] ObterBandasDaTrack(Track track)
        {
            if (track != null && track.EqualizacaoAtiva)
            {
                if (track.EqualizacaoBandas != null && track.EqualizacaoBandas.Any(v => v != 0))
                {
                    return track.EqualizacaoBandas;
                }

                if (track.EqualizacaoPresetId > 0)
                {
                    var presetMusica = _trackRepo.ObterPresetEqualizacao(track.EqualizacaoPresetId);
                    if (presetMusica != null)
                    {
                        return presetMusica.ToBands();
                    }
                }
            }

            if (!EqualizacaoGeralStore.Ativa)
            {
                return EqualizerPreset.CreateFlatBands();
            }

            if (EqualizacaoGeralStore.Bandas != null && EqualizacaoGeralStore.Bandas.Length == EqualizerPreset.BandCount)
            {
                return EqualizacaoGeralStore.Bandas;
            }

            return EqualizerPreset.CreateFlatBands();
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

        private bool TryEncontrarFaixaTocavel(int indiceInicial, bool ignorarBloqueio24Horas, out int indiceTocavel, out Track track, out string motivo)
        {
            indiceTocavel = -1;
            track = null;
            motivo = string.Empty;
            GravarLog($"[BUSCA] Procurando faixa tocavel a partir de {indiceInicial}; ignorar24h={ignorarBloqueio24Horas}");

            if (_playlist == null || _playlist.Count == 0)
            {
                motivo = "[AUDIO] Playlist vazia.";
                return false;
            }

            int total = _playlist.Count;
            int inicio = indiceInicial < 0 ? 0 : indiceInicial % total;

            for (int i = 0; i < total; i++)
            {
                int candidato = (inicio + i) % total;
                var faixa = _playlist[candidato];
                if (faixa == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(faixa.FilePath))
                {
                    GravarLog($"[AUDIO] Faixa sem caminho ignorada: {faixa.Title ?? $"#{candidato}"}");
                    continue;
                }

                if (!File.Exists(faixa.FilePath))
                {
                    GravarLog($"[AUDIO] Arquivo ausente ignorado: {faixa.FilePath}");
                    continue;
                }

                if (!ignorarBloqueio24Horas && faixa.LastPlayedAt.HasValue && faixa.LastPlayedAt.Value > DateTime.Now.AddHours(-24))
                {
                    GravarLog($"[AUDIO] Faixa tocada ha menos de 24h ignorada: {faixa.Title}");
                    continue;
                }

                indiceTocavel = candidato;
                track = faixa;
                GravarLog($"[BUSCA] Faixa aceita index={candidato}; ID={faixa.Id}; Titulo={faixa.Title}");
                return true;
            }

            motivo = "[AUDIO] Nenhuma faixa válida encontrada na playlist.";
            return false;
        }

        private void TocarProximaFaixaValida(int indiceInicial)
        {
            if (_playlist == null || _playlist.Count == 0)
            {
                GravarLog("[NEXT] Ignorado: playlist vazia ou nula.");
                Stop();
                return;
            }

            if (!AplicarRegraPularPulado)
            {
                Play(indiceInicial, false, false);
                return;
            }

            int total = _playlist.Count;
            int inicio = indiceInicial < 0 ? 0 : indiceInicial % total;

            for (int i = 0; i < total; i++)
            {
                int candidato = (inicio + i) % total;
                var track = _playlist[candidato];
                if (track == null || string.IsNullOrWhiteSpace(track.FilePath) || !File.Exists(track.FilePath))
                {
                    continue;
                }

                GravarLog($"[PULAR] candidato id={track.Id}; titulo={track.Title}; pular={track.Pular}; pulado={track.Pulado}; aplicar={AplicarRegraPularPulado}");

                if (DevePularPorPularPulado(track))
                {
                    int novoPulado = _trackRepo.IncrementarPulado(track.Id);
                    AtualizarPuladoEmMemoria(track.Id, novoPulado);
                    GravarLog($"[PULAR] pulando id={track.Id}; puladoAntes={track.Pulado}; puladoDepois={novoPulado}; pular={track.Pular}");
                    continue;
                }

                if (track.Pular > 0 && track.Pulado >= track.Pular)
                {
                    int novoPulado = _trackRepo.ResetarPulado(track.Id);
                    AtualizarPuladoEmMemoria(track.Id, novoPulado);
                    GravarLog($"[PULAR] tocando id={track.Id}; resetando pulado para {novoPulado}; titulo={track.Title}");
                }

                Play(candidato, false, false);
                return;
            }

            GravarLog("[NEXT] Nenhuma faixa elegível encontrada após varrer a playlist.");
            Stop();
            NotificarPlaybackError(CurrentTrack, "[AUDIO] Nenhuma faixa elegível encontrada na playlist.");
        }

        private bool DevePularPorPularPulado(Track track)
        {
            return track != null && track.Pular > 0 && track.Pulado < track.Pular;
        }

        private void AtualizarPuladoEmMemoria(int trackId, int novoPulado)
        {
            if (_playlist == null) return;

            foreach (var item in _playlist.Where(t => t != null && t.Id == trackId))
            {
                item.Pulado = novoPulado;
            }
        }

        private void IniciarMedicaoMaxVolAtual(Track track)
        {
            if (!MusicaPrecisaMedirMaxVol(track))
            {
                _medindoMaxVolAtual = false;
                _maxVolMedidoAtual = 0d;
                _trackIdMedindoMaxVol = null;
                _ultimaNotificacaoMaxVol = DateTime.MinValue;
                System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL] MedicaoLigada=False motivo={(track == null ? "TrackNull" : $"MaxVolJaExiste valor={track.MaxVol.Value:0.###}")}");
                return;
            }

            if (MaxVolEhInvalidoOuLegado(track.MaxVol))
                track.MaxVol = null;

            _medindoMaxVolAtual = true;
            _maxVolMedidoAtual = 0d;
            _trackIdMedindoMaxVol = track.Id;
            _ultimaNotificacaoMaxVol = DateTime.MinValue;
            System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL] MedicaoLigada=True trackId={track.Id} motivo=MaxVolNullOuInvalido");
        }

        private bool MusicaPrecisaMedirMaxVol(Track track)
        {
            return track != null && (!track.MaxVol.HasValue || MaxVolEhInvalidoOuLegado(track.MaxVol));
        }

        private bool MaxVolEhInvalidoOuLegado(double? maxVol)
        {
            return maxVol.HasValue && maxVol.Value >= MaxVolInvalidLegacyThreshold;
        }

        private void FinalizarMedicaoMaxVolAtual()
        {
            if (!_medindoMaxVolAtual || !_trackIdMedindoMaxVol.HasValue)
                return;

            int trackId = _trackIdMedindoMaxVol.Value;
            double picoMedido = _maxVolMedidoAtual;

            _medindoMaxVolAtual = false;
            _maxVolMedidoAtual = 0d;
            _trackIdMedindoMaxVol = null;

            if (picoMedido <= 0d)
                return;

            System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL] FinalizarMedicao trackId={trackId} max={picoMedido:0.###}");
            NotificarStatusVolume($"Máximo detectado: {picoMedido.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}");

            var trackEmMemoria = CurrentTrack != null && CurrentTrack.Id == trackId
                ? CurrentTrack
                : (_playlist != null ? _playlist.FirstOrDefault(t => t != null && t.Id == trackId) : null);

            if (trackEmMemoria != null && !trackEmMemoria.MaxVol.HasValue)
            {
                _trackRepo.AtualizarMusicaMaxVolSeNulo(trackId, picoMedido);
                trackEmMemoria.MaxVol = picoMedido;
                TrackMaxVolMeasured?.Invoke(trackId, picoMedido);

                if (CurrentPlaylistId > 0)
                {
                    _trackRepo.RecalcularListaMinMaxVol(CurrentPlaylistId);
                }
            }
        }

        private void Aggregator_PeakMeasured(float peak)
        {
            try
            {
                if ((DateTime.Now - _ultimaLogPeakRecebido).TotalSeconds >= 1)
                {
                    _ultimaLogPeakRecebido = DateTime.Now;
                    System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL] Peak recebido peak={peak:0.###} medindo={_medindoMaxVolAtual} atual={_maxVolMedidoAtual:0.###}");
                }

                if (!_medindoMaxVolAtual || !_trackIdMedindoMaxVol.HasValue)
                    return;

                if (peak <= 0.000001f)
                    return;

                if (peak <= _maxVolMedidoAtual)
                    return;

                _maxVolMedidoAtual = peak;
                System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL] Maximo atualizado trackId={_trackIdMedindoMaxVol.Value} max={_maxVolMedidoAtual:0.###}");

                DateTime agora = DateTime.Now;
                if ((agora - _ultimaNotificacaoMaxVol).TotalMilliseconds < 250)
                    return;

                _ultimaNotificacaoMaxVol = agora;
                string mensagem = $"Máximo detectado: {_maxVolMedidoAtual.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}";
                System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL] Emitindo status='{mensagem}'");
                NotificarStatusVolume(mensagem);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL ERRO] Aggregator_PeakMeasured: {ex}");
            }
        }

        private void AtualizarFatorNormalizacaoAtual(Track track)
        {
            _fatorNormalizacaoAtual = 1.0f;

            double? trackMaxVol = track?.MaxVol;
            if (MaxVolEhInvalidoOuLegado(trackMaxVol))
                trackMaxVol = null;

            double? listaMinMaxVol = null;
            string statusNormalizacao = null;

            if (NormalizacaoAtiva && track != null && trackMaxVol.HasValue && trackMaxVol.Value > 0d && CurrentPlaylistId > 0)
            {
                try
                {
                    listaMinMaxVol = _trackRepo?.ObterListaMinMaxVol(CurrentPlaylistId);
                    if (MaxVolEhInvalidoOuLegado(listaMinMaxVol))
                        listaMinMaxVol = null;

                    if (listaMinMaxVol.HasValue && listaMinMaxVol.Value > 0d && trackMaxVol.Value > listaMinMaxVol.Value)
                    {
                        _fatorNormalizacaoAtual = (float)(listaMinMaxVol.Value / trackMaxVol.Value);
                        int reducao = (int)Math.Round((1.0f - _fatorNormalizacaoAtual) * 100.0f);
                        if (reducao > 0 && _fatorNormalizacaoAtual < 0.999f)
                        {
                            statusNormalizacao = $"Ajuste no volume: -{reducao}%";
                        }
                    }
                }
                catch (Exception ex)
                {
                    GravarLog($"[NORM] Falha ao obter Lista.MinMaxVol: {ex.Message}");
                    _fatorNormalizacaoAtual = 1.0f;
                }
            }

            float volumeEfetivoCalculado = _volumeManual * (NormalizacaoAtiva ? _fatorNormalizacaoAtual : 1.0f);
            if (System.Diagnostics.Debugger.IsAttached)
            {
                volumeEfetivoCalculado *= 0.02f;
            }

            if (volumeEfetivoCalculado < 0f)
                volumeEfetivoCalculado = 0f;

            if (volumeEfetivoCalculado > 1f)
                volumeEfetivoCalculado = 1f;

            GravarLog(
                "[NORM] ativa=" + NormalizacaoAtiva +
                "; trackId=" + (track != null ? track.Id.ToString() : "null") +
                "; trackMaxVol=" + (trackMaxVol.HasValue ? trackMaxVol.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "null") +
                "; listaMinMaxVol=" + (listaMinMaxVol.HasValue ? listaMinMaxVol.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "null") +
                "; fator=" + _fatorNormalizacaoAtual.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                "; volumeManual=" + _volumeManual.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                "; volumeEfetivo=" + volumeEfetivoCalculado.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

            System.Diagnostics.Debug.WriteLine(
                "[NORM/MAXVOL] Fator calculado ativa=" + NormalizacaoAtiva +
                " trackId=" + (track != null ? track.Id.ToString() : "null") +
                " trackMaxVol=" + (trackMaxVol.HasValue ? trackMaxVol.Value.ToString("0.###") : "null") +
                " listaMinMaxVol=" + (listaMinMaxVol.HasValue ? listaMinMaxVol.Value.ToString("0.###") : "null") +
                " fator=" + _fatorNormalizacaoAtual.ToString("0.###") +
                " status=" + (statusNormalizacao ?? "null"));

            if (!string.IsNullOrWhiteSpace(statusNormalizacao) || !_medindoMaxVolAtual)
            {
                NotificarStatusVolume(statusNormalizacao);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[NORM/MAXVOL] Status nulo de normalização ignorado porque medição MaxVol está ativa.");
            }

            AplicarVolumeEfetivo();
        }

        public void RecalcularNormalizacaoAtual()
        {
            AtualizarFatorNormalizacaoAtual(CurrentTrack);
        }

        private void AplicarVolumeEfetivo()
        {
            if (_volumeProvider == null)
                return;

            float fator = NormalizacaoAtiva ? _fatorNormalizacaoAtual : 1.0f;
            float volumeEfetivo = _volumeManual * fator;
            float volumeEfetivoFinal = volumeEfetivo;

            if (System.Diagnostics.Debugger.IsAttached)
            {
                volumeEfetivoFinal *= 0.02f; // Baixinho enquanto programa
                GravarLog("[DEBUG] Volume IDE limitado.");
            }

            if (volumeEfetivoFinal < 0f)
                volumeEfetivoFinal = 0f;

            if (volumeEfetivoFinal > 1f)
                volumeEfetivoFinal = 1f;

            _volumeProvider.Volume = volumeEfetivoFinal;
            GravarLog($"[NORM] AplicarVolumeEfetivo fator={fator:0.###}; volumeManual={_volumeManual:0.###}; volumeEfetivo={volumeEfetivoFinal:0.###}");
        }

        private bool ArquivoNaoEncontrado(Exception ex)
        {
            if (ex == null) return false;

            if (ex.HResult == unchecked((int)0x80070002))
            {
                return true;
            }

            return ex.Message != null &&
                   ex.Message.IndexOf("arquivo especificado", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void NotificarStatusCue(string mensagem)
        {
            try { OnStatusCueChanged?.Invoke(mensagem); }
            catch (Exception ex) { GravarLog($"Erro em OnStatusCueChanged: {ex.Message}"); }
        }

        private void NotificarStatusVolume(string mensagem)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL] NotificarStatusVolume='{mensagem ?? "null"}'");
                StatusVolumeChanged?.Invoke(mensagem);
            }
            catch (Exception ex)
            {
                GravarLog($"Erro em StatusVolumeChanged: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL ERRO] StatusVolumeChanged: {ex}");
            }
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
