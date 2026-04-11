using System;

namespace XP3.Models
{
    public class Track
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int BandId { get; set; }
        public string BandName { get; set; } // Helper property for UI
        public string FilePath { get; set; }
        public TimeSpan Duration { get; set; }
        
        public string DurationFormatted => Duration.ToString(@"mm\:ss");

        /// <summary>
        /// -1: Não avaliado (analisar silêncio no início)
        ///  0: Tocar do início absoluto
        /// >0: Segundo exato para iniciar a música
        /// </summary>
        public int CutIni { get; set; } = -1;

        /// <summary>
        /// -1: Não avaliado (analisar silêncio no fim)
        ///  0: Tocar até o fim absoluto
        /// >0: Segundo exato para interromper a música e passar para a próxima
        /// </summary>
        public int CutFim { get; set; } = -1;
        public object Pular { get; internal set; }
        public object Pulado { get; internal set; }
    }
}
