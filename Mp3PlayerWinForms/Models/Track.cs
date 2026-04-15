using System;

namespace XP3.Models
{
    public class Track
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int BandId { get; set; }
        public string BandName { get; set; }
        public string FilePath { get; set; }
        public TimeSpan Duration { get; set; }

        public string DurationFormatted => Duration.ToString(@"mm\:ss");

        public int CutIni { get; set; } = -1;
        public int CutFim { get; set; } = -1;

        // --- MUDANÇA AQUI: De 'object' para 'int' ---
        // Agora o C# permite fazer Pular++ e Pulado++
        public int Pular { get; set; }
        public int Pulado { get; set; }
    }
}