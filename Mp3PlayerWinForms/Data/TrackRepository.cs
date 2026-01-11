using System;
using System.Collections.Generic;
using System.Data.SQLite; // Biblioteca correta
using System.IO;
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

        public Track GetTrackById(int id)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT ID, Nome, Lugar, Banda FROM Musica WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Track
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                FilePath = reader.GetString(2),
                                BandId = reader.GetInt32(3)
                            };
                        }
                    }
                }
            }
            return null;
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

        #region Apagar

        public void AdicionarParaApagarDepois(string caminho, string banda)
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                string sql = "INSERT INTO ApagarMusicas (Lugar, Banda) VALUES (@Lugar, @Banda)";

                using (var command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Lugar", caminho);
                    command.Parameters.AddWithValue("@Banda", banda);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void RemoverMusicaDefinitivamente(int trackId)
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();

                // 1. Antes de apagar, guardamos o ID da Banda
                int bandaId = -1;
                using (var cmdBusca = new SQLiteCommand("SELECT Banda FROM Musica WHERE ID = @Id", connection))
                {
                    cmdBusca.Parameters.AddWithValue("@Id", trackId);
                    var result = cmdBusca.ExecuteScalar();
                    if (result != null && result != DBNull.Value) bandaId = Convert.ToInt32(result);
                }

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 2. Remove da tabela de ligação (LisMus) - Coluna 'Musica' é o ID da faixa
                        using (var cmd1 = connection.CreateCommand())
                        {
                            cmd1.Transaction = transaction;
                            cmd1.CommandText = "DELETE FROM LisMus WHERE Musica = @Id";
                            cmd1.Parameters.AddWithValue("@Id", trackId);
                            cmd1.ExecuteNonQuery();
                        }

                        // 3. Remove da tabela principal (Musica)
                        using (var cmd2 = connection.CreateCommand())
                        {
                            cmd2.Transaction = transaction;
                            cmd2.CommandText = "DELETE FROM Musica WHERE ID = @Id";
                            cmd2.Parameters.AddWithValue("@Id", trackId);
                            cmd2.ExecuteNonQuery();
                        }

                        // 4. Verifica se ainda restam músicas dessa banda
                        if (bandaId != -1)
                        {
                            using (var cmdCheck = new SQLiteCommand("SELECT COUNT(*) FROM Musica WHERE Banda = @BandaId", connection, transaction))
                            {
                                cmdCheck.Parameters.AddWithValue("@BandaId", bandaId);
                                long restante = (long)cmdCheck.ExecuteScalar();

                                if (restante == 0)
                                {
                                    // Tenta obter o caminho da pasta antes de apagar o registro da banda
                                    string caminhoPastaBanda = string.Empty;
                                    using (var cmdPath = new SQLiteCommand("SELECT Lugar FROM Banda WHERE ID = @BandaId", connection, transaction))
                                    {
                                        cmdPath.Parameters.AddWithValue("@BandaId", bandaId);
                                        caminhoPastaBanda = cmdPath.ExecuteScalar()?.ToString();
                                    }

                                    // Apaga o registro da banda (Supondo que a tabela se chama 'Banda')
                                    using (var cmdDelBanda = new SQLiteCommand("DELETE FROM Banda WHERE ID = @BandaId", connection, transaction))
                                    {
                                        cmdDelBanda.Parameters.AddWithValue("@BandaId", bandaId);
                                        cmdDelBanda.ExecuteNonQuery();
                                    }

                                    // 5. Tentativa de apagar a pasta física (Fora da transação SQL, após o commit)
                                    transaction.Commit();
                                    TentarApagarPastaBanda(caminhoPastaBanda);
                                    return; // Sai pois já deu commit
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        System.Windows.Forms.MessageBox.Show($"Erro ao processar exclusão: {ex.Message}");
                        throw;
                    }
                }
            }
        }

        private void TentarApagarPastaBanda(string caminho)
        {
            if (string.IsNullOrWhiteSpace(caminho) || !Directory.Exists(caminho)) return;

            try
            {
                // Só apaga se a pasta estiver vazia
                if (Directory.GetFileSystemEntries(caminho).Length == 0)
                {
                    Directory.Delete(caminho);
                }
            }
            catch
            {
                // Silencioso conforme solicitado se não der para apagar
            }
        }


        #endregion

        #region Copiar/Mover

        // Retorna todas as listas cadastradas ordenadas por nome
        public List<Playlist> GetAllPlaylists()
        {
            var list = new List<Playlist>();
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT ID, Nome FROM Lista ORDER BY Nome", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new Playlist { Id = reader.GetInt32(0), Name = reader.GetString(1) });
                    }
                }
            }
            return list;
        }

        // Retorna as listas às quais uma música específica já pertence
        public List<Playlist> GetPlaylistsByMusicaId(int musicaId)
        {
            var list = new List<Playlist>();
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                string sql = @"SELECT l.ID, l.Nome FROM Lista l 
                       JOIN LisMus lm ON l.ID = lm.Lista 
                       WHERE lm.Musica = @musId";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@musId", musicaId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Playlist { Id = reader.GetInt32(0), Name = reader.GetString(1) });
                        }
                    }
                }
            }
            return list;
        }

        public void LimparMusicaDeTodasPlaylists(int musicaId)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("DELETE FROM LisMus WHERE Musica = @musId", conn))
                {
                    cmd.Parameters.AddWithValue("@musId", musicaId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void RemoverMusicaDaLista(int musicaId, int listaId)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("DELETE FROM LisMus WHERE Musica = @musId AND Lista = @listId", conn))
                {
                    cmd.Parameters.AddWithValue("@musId", musicaId);
                    cmd.Parameters.AddWithValue("@listId", listaId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion

    }
}