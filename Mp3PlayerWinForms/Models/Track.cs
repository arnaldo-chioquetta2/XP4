using System;

namespace Mp3PlayerWinForms.Models
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
    }
}
