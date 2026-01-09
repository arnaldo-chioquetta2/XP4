using System;
using System.Data.SQLite;
using System.IO;

namespace Mp3PlayerWinForms.Data
{
    public static class Database
    {
        private const string DbName = @"D:\Prog\XP3\Mp3PlayerWinForms_Project\Mp3PlayerWinForms\player.db";
        public static string ConnectionString => $"Data Source={DbName};Version=3;";

        public static void Initialize()
        {
            if (!File.Exists(DbName))
            {
                SQLiteConnection.CreateFile(DbName);
                using (var connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();
                    string sql = @"
                        CREATE TABLE Playlists (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL
                        );

                        CREATE TABLE Bands (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL UNIQUE
                        );

                        CREATE TABLE Tracks (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Title TEXT NOT NULL,
                            BandId INTEGER,
                            FilePath TEXT NOT NULL,
                            Duration INTEGER,
                            FOREIGN KEY (BandId) REFERENCES Bands(Id)
                        );

                        CREATE TABLE PlaylistTracks (
                            PlaylistId INTEGER,
                            TrackId INTEGER,
                            PRIMARY KEY (PlaylistId, TrackId),
                            FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id),
                            FOREIGN KEY (TrackId) REFERENCES Tracks(Id)
                        );
                        
                        -- Insert a default playlist
                        INSERT INTO Playlists (Name) VALUES ('Minha Playlist');
                    ";
                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
