using System;
using System.Collections.Generic;
using NAudio.Wave; // Essencial
using Mp3PlayerWinForms.Models;
using Mp3PlayerWinForms.Services;

namespace Mp3PlayerWinForms.Services
{
    public class AudioPlayerService : IDisposable
    {
        private IWavePlayer _waveOut;

        // ALTERAÇÃO 1: Mudamos de 'AudioFileReader' para 'WaveStream'
        // 'WaveStream' é o pai genérico que aceita tanto MP3 puro quanto arquivos do MediaFoundation
        private WaveStream _audioFile;

        private List<Track> _playlist;
        private int _currentIndex = -1;

        public event EventHandler<Track> TrackChanged;
        public event EventHandler PlaybackStopped;
        public event EventHandler<float[]> FftDataReceived;
        private SampleAggregator _aggregator;

        // NOVO EVENTO: Esse é o segredo para o Spectrum funcionar!
        // Ele avisa: "Ei, carreguei um áudio novo, quem quiser desenhar o gráfico, pega aqui!"
        public event EventHandler<WaveStream> AudioSourceCreated;

        public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
        public Track CurrentTrack => (_currentIndex >= 0 && _currentIndex < _playlist.Count) ? _playlist[_currentIndex] : null;

        public AudioPlayerService()
        {
            _playlist = new List<Track>();
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
                System.Windows.Forms.MessageBox.Show($"Erro: {ex.Message}");
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
            _waveOut?.Stop();

            // É importante desvincular o evento para não causar vazamento de memória ou chamadas duplas
            if (_waveOut != null) _waveOut.PlaybackStopped -= OnPlaybackStopped;

            _audioFile?.Dispose();
            _waveOut?.Dispose();
            _audioFile = null;
            _waveOut = null;
        }

        public void Next()
        {
            if (_playlist.Count == 0) return;
            int nextIndex = (_currentIndex + 1) % _playlist.Count;
            Play(nextIndex);
        }

        // ALTERAÇÃO 4: Corrigido o tipo do evento (StoppedEventArgs é do NAudio, não do System.Management)
        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            // Se parou porque a música acabou (e não porque o usuário clicou em Stop)
            if (_audioFile != null && _audioFile.Position >= _audioFile.Length)
            {
                Next();
            }
            else if (e.Exception != null)
            {
                // Se parou por erro
                System.Windows.Forms.MessageBox.Show("Erro na reprodução: " + e.Exception.Message);
            }

            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}