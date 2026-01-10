using System;
using System.Collections.Generic;
using System.Data.SQLite; // Biblioteca correta
using XP3.Models;

namespace XP3.Data
{
    public class TrackRepository
    {
        // --- MÉTODO NOVO QUE ESTAVA FALTANDO ---
        public string GetPlaylistName(int playlistId)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Nome FROM Lista WHERE ID = @id";
                    cmd.Parameters.AddWithValue("@id", playlistId);

                    var result = cmd.ExecuteScalar();
                    return result != null ? result.ToString() : "Lista Desconhecida";
                }
            }
        }
        // ----------------------------------------

        public int GetOrInsertBand(string bandName)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ID FROM Banda WHERE Nome = @name";
                    cmd.Parameters.AddWithValue("@name", bandName);
                    var result = cmd.ExecuteScalar();
                    if (result != null) return Convert.ToInt32(result);
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO Banda (Nome, Lugar) VALUES (@name, ''); 
                                        SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@name", bandName);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public int AddTrack(Track track)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO Musica 
                        (Nome, Lugar, Banda, Tempo, Tamanho, BitRate, VezErro, MaxVol, Equalizacao, Album, Unid, Pular, Pulado, NaoAchou, CutIni, CutFim) 
                        VALUES 
                        (@nome, @lugar, @banda, @tempo, 0, 0, 0, 100, 0, 0, 0, 0, 0, 0, 0, 0); 
                        SELECT last_insert_rowid();";

                    cmd.Parameters.AddWithValue("@nome", track.Title);
                    cmd.Parameters.AddWithValue("@lugar", track.FilePath);
                    cmd.Parameters.AddWithValue("@banda", track.BandId);
                    cmd.Parameters.AddWithValue("@tempo", track.Duration.ToString());

                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void AddTrackToPlaylist(int playlistId, int trackId)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO LisMus (Lista, Musica, JaTocou, PosLista) 
                                        VALUES (@lista, @musica, 0, 0)";

                    cmd.Parameters.AddWithValue("@lista", playlistId);
                    cmd.Parameters.AddWithValue("@musica", trackId);

                    try { cmd.ExecuteNonQuery(); } catch { }
                }
            }
        }

        public List<Track> GetTracksByPlaylist(int playlistId)
        {
            var tracks = new List<Track>();
            using (var conn = Database.GetConnection())
            {
                conn.Open();

                string sql = @"
                    SELECT 
                        m.ID, 
                        m.Nome, 
                        m.Lugar, 
                        m.Tempo, 
                        b.ID as BandId, 
                        b.Nome as BandName
                    FROM Musica m
                    LEFT JOIN Banda b ON m.Banda = b.ID
                    JOIN LisMus lm ON m.ID = lm.Musica
                    WHERE lm.Lista = @listaId";

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@listaId", playlistId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var t = new Track();
                            t.Id = reader.GetInt32(0);
                            t.Title = reader.IsDBNull(1) ? "Sem Título" : reader.GetString(1);
                            t.FilePath = reader.IsDBNull(2) ? "" : reader.GetString(2);

                            string tempoStr = reader.IsDBNull(3) ? "00:00:00" : reader.GetString(3);
                            TimeSpan ts;
                            if (TimeSpan.TryParse(tempoStr, out ts))
                                t.Duration = ts;
                            else
                                t.Duration = TimeSpan.Zero;

                            t.BandId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                            t.BandName = reader.IsDBNull(5) ? "Desconhecida" : reader.GetString(5);

                            tracks.Add(t);
                        }
                    }
                }
            }
            return tracks;
        }

        public int GetOrCreatePlaylist(string nomeLista)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ID FROM Lista WHERE Nome = @nome";
                    cmd.Parameters.AddWithValue("@nome", nomeLista);
                    var result = cmd.ExecuteScalar();
                    if (result != null) return Convert.ToInt32(result);
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO Lista (Nome, AutoDel, SempreRandom, NaoRepetir, MaxVol, ProxLista, Usu, MenosTocadasPrimeiro, DesabProg) 
                                        VALUES (@nome, 0, 0, 0, 100, 0, 0, 0, 0);
                                        SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@nome", nomeLista);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void ResetarBancoDeDados()
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        // Deleta as associações e as músicas. 
                        // As bandas podem ficar ou ser deletadas também.
                        cmd.CommandText = "DELETE FROM LisMus; DELETE FROM Musica; DELETE FROM Banda;";
                        cmd.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }
        }

    }
}