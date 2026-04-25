using System;

namespace XP3.Models
{
    public class EqualizerPreset
    {
        public const int BandCount = 10;
        public static readonly int[] FrequenciasPadrao = { 60, 170, 310, 600, 1000, 3000, 6000, 8000, 10000, 12000 };

        public int Id { get; set; }
        public string Nome { get; set; }
        public int IdPerfil { get; set; }
        public int Eq0 { get; set; }
        public int Eq1 { get; set; }
        public int Eq2 { get; set; }
        public int Eq3 { get; set; }
        public int Eq4 { get; set; }
        public int Eq5 { get; set; }
        public int Eq6 { get; set; }
        public int Eq7 { get; set; }
        public int Eq8 { get; set; }
        public int Eq9 { get; set; }

        public int[] ToBands()
        {
            return new[] { Eq0, Eq1, Eq2, Eq3, Eq4, Eq5, Eq6, Eq7, Eq8, Eq9 };
        }

        public void SetBands(int[] bands)
        {
            if (bands == null || bands.Length != BandCount)
            {
                throw new ArgumentException("O preset precisa ter 10 bandas.", nameof(bands));
            }

            Eq0 = bands[0];
            Eq1 = bands[1];
            Eq2 = bands[2];
            Eq3 = bands[3];
            Eq4 = bands[4];
            Eq5 = bands[5];
            Eq6 = bands[6];
            Eq7 = bands[7];
            Eq8 = bands[8];
            Eq9 = bands[9];
        }

        public static int[] CreateFlatBands()
        {
            return new int[BandCount];
        }
    }
}
