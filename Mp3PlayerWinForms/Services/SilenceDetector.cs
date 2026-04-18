using System;
using NAudio.Wave;

namespace XP3.Services
{
    public class SilenceDetector
    {
        private const float SilenceThreshold = 0.01f; // Aprox -40dB

        /// <summary>
        /// Varredura do início para encontrar o primeiro som audível.
        /// </summary>
        public int AnalisarCutIni(string caminhoArquivo)
        {
            try
            {
                using (var reader = new AudioFileReader(caminhoArquivo))
                {
                    float[] buffer = new float[reader.WaveFormat.SampleRate / 10]; // Blocos de 100ms
                    int samplesLidos;
                    long totalSamplesProcessados = 0;

                    while ((samplesLidos = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        for (int i = 0; i < samplesLidos; i++)
                        {
                            if (Math.Abs(buffer[i]) > SilenceThreshold)
                            {
                                double segundoExato = (double)(totalSamplesProcessados + i)
                                                      / reader.WaveFormat.SampleRate
                                                      / reader.WaveFormat.Channels;

                                // Se o silêncio for insignificante (menos de 0.2s), grava 0
                                return (segundoExato < 0.2) ? 0 : (int)Math.Floor(segundoExato);
                            }
                        }
                        totalSamplesProcessados += samplesLidos;

                        // Segurança: não analisa mais que 30s de introdução
                        if (reader.CurrentTime.TotalSeconds > 30) break;
                    }
                }
            }
            catch { /* Log de erro se necessário */ }
            return 0;
        }

        /// <summary>
        /// Varredura do final para encontrar onde o som "morre".
        /// </summary>
        public int AnalisarCutFim(string caminhoArquivo)
        {
            try
            {
                using (var reader = new AudioFileReader(caminhoArquivo))
                {
                    int segundosParaAnalisar = 20; // Analisa os últimos 20 segundos
                    if (reader.TotalTime.TotalSeconds > segundosParaAnalisar)
                    {
                        reader.CurrentTime = reader.TotalTime.Add(TimeSpan.FromSeconds(-segundosParaAnalisar));
                    }

                    float[] buffer = new float[reader.WaveFormat.SampleRate / 10];
                    int samplesLidos;
                    long amostraDoUltimoSom = 0;
                    long amostraBase = (long)(reader.CurrentTime.TotalSeconds * reader.WaveFormat.SampleRate * reader.WaveFormat.Channels);
                    long contadorLocal = 0;

                    while ((samplesLidos = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        for (int i = 0; i < samplesLidos; i++)
                        {
                            if (Math.Abs(buffer[i]) > SilenceThreshold)
                            {
                                amostraDoUltimoSom = amostraBase + contadorLocal + i;
                            }
                        }
                        contadorLocal += samplesLidos;
                    }

                    if (amostraDoUltimoSom > 0)
                    {
                        double tempoFimReal = (double)amostraDoUltimoSom
                                             / reader.WaveFormat.SampleRate
                                             / reader.WaveFormat.Channels;

                        // Se o fim real for muito perto do fim do arquivo (menos de 0.5s de diferença), 
                        // significa que não tem silêncio no final. Gravamos 0.
                        if (reader.TotalTime.TotalSeconds - tempoFimReal < 0.5)
                        {
                            return 0;
                        }

                        return (int)Math.Ceiling(tempoFimReal);
                    }
                }
            }
            catch { /* Log de erro */ }
            return 0;
        }
    }
}