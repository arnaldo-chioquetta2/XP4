using System;
using NAudio.Dsp;
using NAudio.Wave;

namespace Mp3PlayerWinForms.Services
{
    // Esta classe "escuta" o áudio passando e calcula o gráfico
    public class SampleAggregator : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly Complex[] _fftBuffer;
        private readonly float[] _fftArgs;
        private int _fftPos;
        private readonly int _fftLength;
        private readonly int _m; // log2(fftLength)
        private DateTime _lastReadLog = DateTime.MinValue;
        private DateTime _lastPeakEventLog = DateTime.MinValue;

        // Evento que avisa quando o cálculo está pronto
        public event EventHandler<FftEventArgs> FftCalculated;
        public event Action<float> PeakMeasured;

        public SampleAggregator(ISampleProvider source, int fftLength = 1024)
        {
            _source = source;
            _fftLength = fftLength;
            _m = (int)Math.Log(fftLength, 2.0);
            _fftBuffer = new Complex[fftLength];
            _fftArgs = new float[fftLength];
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);
            float peak = 0f;

            for (int n = 0; n < samplesRead; n += _source.WaveFormat.Channels)
            {
                // Pega o sample (se for estéreo, soma os canais pra virar mono)
                float sample = buffer[offset + n];
                if (_source.WaveFormat.Channels == 2) sample += buffer[offset + n + 1];
                sample /= _source.WaveFormat.Channels;
                peak = Math.Max(peak, Math.Abs(sample));

                Add(sample);
            }

            if ((DateTime.Now - _lastReadLog).TotalSeconds >= 1)
            {
                _lastReadLog = DateTime.Now;
                //System.Diagnostics.Debug.WriteLine($"[SAMPLE] Read chamado count={count} samplesRead={samplesRead} peak={peak:0.###}");
            }

            if (peak > 0f)
            {
                if ((DateTime.Now - _lastPeakEventLog).TotalSeconds >= 1)
                {
                    _lastPeakEventLog = DateTime.Now;
                    //System.Diagnostics.Debug.WriteLine($"[SAMPLE] PeakMeasured disparando peak={peak:0.###}");
                }

                PeakMeasured?.Invoke(peak);
            }

            return samplesRead;
        }

        private void Add(float value)
        {
            if (_fftPos >= _fftLength)
            {
                // Buffer cheio, hora de calcular o FFT
                _fftPos = 0;
                FastFourierTransform.FFT(true, _m, _fftBuffer);
                CalculateFftGraph();
            }
            else
            {
                // Adiciona e aplica janela de Hanning para suavizar
                _fftBuffer[_fftPos].X = (float)(value * FastFourierTransform.HammingWindow(_fftPos, _fftLength));
                _fftBuffer[_fftPos].Y = 0;
                _fftPos++;
            }
        }

        private void CalculateFftGraph()
        {
            for (int i = 0; i < _fftLength / 2; i++)
            {
                // Calcula a magnitude (hipotenusa) do número complexo
                double intensity = Math.Sqrt(_fftBuffer[i].X * _fftBuffer[i].X + _fftBuffer[i].Y * _fftBuffer[i].Y);
                // Multiplicador para ficar visível no gráfico
                _fftArgs[i] = (float)intensity * 100;
            }

            FftCalculated?.Invoke(this, new FftEventArgs(_fftArgs));
        }
    }

    public class FftEventArgs : EventArgs
    {
        public float[] Result { get; }
        public FftEventArgs(float[] result)
        {
            Result = result;
        }
    }
}
