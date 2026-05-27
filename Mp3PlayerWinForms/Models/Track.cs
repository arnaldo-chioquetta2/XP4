using System;
using System.Linq;

namespace XP3.Models
{
    public class Track
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int BandId { get; set; }
        public string BandName { get; set; }
        public string FilePath { get; set; }
        public string VideoPath { get; set; }
        public TimeSpan Duration { get; set; }
        public string DurationFormatted => Duration.ToString(@"mm\:ss");
        public int CutIni { get; set; } = -1;
        public int CutFim { get; set; } = -1;
        public int EqualizacaoPresetId { get; set; }
        public int[] EqualizacaoBandas { get; set; } = EqualizerPreset.CreateFlatBands();
        public bool EqualizacaoAtiva { get; set; } = true;
        public bool PossuiBandasEqualizacao => (EqualizacaoBandas != null && EqualizacaoBandas.Any(v => v != 0)) || EqualizacaoPresetId > 0;
        public bool TemEqualizacao => EqualizacaoAtiva && PossuiBandasEqualizacao;
        public int Vez { get; set; }
        public DateTime? LastPlayedAt { get; set; }
        public int Pular { get; set; }
        public int Pulado { get; set; }

        public double RandomTieBreaker { get; set; } = 0.0;
    }
}
