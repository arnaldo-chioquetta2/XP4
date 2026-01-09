using System.Collections.Generic;

namespace Mp3PlayerWinForms.Models
{
    public class Playlist
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Track> Tracks { get; set; } = new List<Track>();
    }
}
