using System;
using System.Collections.Generic;
using System.Data.SQLite;
using Mp3PlayerWinForms.Models;

namespace Mp3PlayerWinForms.Data
{
    public class TrackRepository
    {
        public int GetOrInsertBand(string bandName)
        {
            using (var conn = new SQLiteConnection(Database.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT Id FROM Bands WHERE Name = @name", conn))
                {
                    cmd.Parameters.AddWithValue("@name", bandName);
                    var result = cmd.ExecuteScalar();
                    if (result != null) return Convert.ToInt32(result);
                }

                using (var cmd = new SQLiteCommand("INSERT INTO Bands (Name) VALUES (@name); SELECT last_insert_rowid();", conn))
                {
                    cmd.Parameters.AddWithValue("@name", bandName);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public int AddTrack(Track track)
        {
            using (var conn = new SQLiteConnection(Database.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(
                    "INSERT INTO Tracks (Title, BandId, FilePath, Duration) VALUES (@title, @bandId, @path, @duration); SELECT last_insert_rowid();", conn))
                {
                    cmd.Parameters.AddWithValue("@title", track.Title);
                    cmd.Parameters.AddWithValue("@bandId", track.BandId);
                    cmd.Parameters.AddWithValue("@path", track.FilePath);
                    cmd.Parameters.AddWithValue("@duration", (int)track.Duration.TotalSeconds);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void AddTrackToPlaylist(int playlistId, int trackId)
        {
            using (var conn = new SQLiteConnection(Database.ConnectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(
                    "INSERT OR IGNORE INTO PlaylistTracks (PlaylistId, TrackId) VALUES (@pId, @tId)", conn))
                {
                    cmd.Parameters.AddWithValue("@pId", playlistId);
                    cmd.Parameters.AddWithValue("@tId", trackId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<Track> GetTracksByPlaylist(int playlistId)
        {
            var tracks = new List<Track>();
            using (var conn = new SQLiteConnection(Database.ConnectionString))
            {
                conn.Open();
                string sql = @"
                    SELECT t.Id, t.Title, t.FilePath, t.Duration, b.Id as BandId, b.Name as BandName
                    FROM Tracks t
                    JOIN Bands b ON t.BandId = b.Id
                    JOIN PlaylistTracks pt ON t.Id = pt.TrackId
                    WHERE pt.PlaylistId = @pId";
                
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@pId", playlistId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tracks.Add(new Track
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                FilePath = reader.GetString(2),
                                Duration = TimeSpan.FromSeconds(reader.GetInt32(3)),
                                BandId = reader.GetInt32(4),
                                BandName = reader.GetString(5)
                            });
                        }
                    }
                }
            }
            return tracks;
        }
    }
}
