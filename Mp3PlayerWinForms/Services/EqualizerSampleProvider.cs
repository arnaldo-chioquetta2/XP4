using NAudio.Dsp;
using NAudio.Wave;
using System;
using XP3.Models;

namespace XP3.Services
{
    public class EqualizerSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly object _sync = new object();
        private readonly int _sampleRate;
        private readonly int _channels;
        private BiQuadFilter[][] _filters;
        private int[] _bandValues;

        public EqualizerSampleProvider(ISampleProvider source, int[] bandValues)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _sampleRate = source.WaveFormat.SampleRate;
            _channels = source.WaveFormat.Channels;
            _bandValues = NormalizarBandas(bandValues);
            RecriarFiltros();
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            if (read <= 0)
            {
                return read;
            }

            lock (_sync)
            {
                for (int n = 0; n < read; n++)
                {
                    int channel = n % _channels;
                    float sample = buffer[offset + n];

                    for (int band = 0; band < EqualizerPreset.BandCount; band++)
                    {
                        sample = _filters[channel][band].Transform(sample);
                    }

                    buffer[offset + n] = sample;
                }
            }

            return read;
        }

        public void UpdateBands(int[] bandValues)
        {
            lock (_sync)
            {
                _bandValues = NormalizarBandas(bandValues);
                RecriarFiltros();
            }
        }

        private void RecriarFiltros()
        {
            _filters = new BiQuadFilter[_channels][];

            for (int channel = 0; channel < _channels; channel++)
            {
                _filters[channel] = new BiQuadFilter[EqualizerPreset.BandCount];

                for (int band = 0; band < EqualizerPreset.BandCount; band++)
                {
                    float freq = Math.Min(EqualizerPreset.FrequenciasPadrao[band], (_sampleRate / 2f) - 100f);
                    if (freq < 10f)
                    {
                        freq = 10f;
                    }

                    float q = band >= 7 ? 0.55f : 0.9f;
                    _filters[channel][band] = BiQuadFilter.PeakingEQ(_sampleRate, freq, q, _bandValues[band]);
                }
            }
        }

        private static int[] NormalizarBandas(int[] bandValues)
        {
            var result = EqualizerPreset.CreateFlatBands();
            if (bandValues == null)
            {
                return result;
            }

            for (int i = 0; i < result.Length && i < bandValues.Length; i++)
            {
                result[i] = Math.Max(-12, Math.Min(12, bandValues[i]));
            }

            return result;
        }
    }
}
