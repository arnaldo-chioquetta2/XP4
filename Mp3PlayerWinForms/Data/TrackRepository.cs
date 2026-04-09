using System;
using System.IO;
using XP3.Models;
using System.Diagnostics;
using System.Data.SQLite;
using System.Collections.Generic;

namespace XP3.Data
{
    public class TrackRepository
    {

        public string GetPlaylistName(int playlistId)
        {
            try
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
            catch
            {
                return "Erro ao carregar";
            }
        }

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

        // --- CORREÇÃO CRÍTICA 1: IMPEDIR CRIAÇÃO DE ID DUPLICADO ---
        public int AddTrack(Track track)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();

                // 1. Verifica se esse arquivo JÁ ESTÁ CADASTRADO
                using (var checkCmd = conn.CreateCommand())
                {
                    checkCmd.CommandText = "SELECT ID FROM Musica WHERE Lugar = @lugar";
                    checkCmd.Parameters.AddWithValue("@lugar", track.FilePath);
                    var existingId = checkCmd.ExecuteScalar();

                    if (existingId != null)
                    {
                        // Se já existe, NÃO cria novo. Retorna o ID existente.
                        Debug.WriteLine($"[REPO] Arquivo já existe no banco (ID {existingId}). Reutilizando.");
                        return Convert.ToInt32(existingId);
                    }
                }

                // 2. Se não existe, cria novo
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

                // Verifica vínculo existente
                string checkSql = "SELECT COUNT(*) FROM LisMus WHERE Lista = @lista AND Musica = @musica";
                using (var checkCmd = new SQLiteCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@lista", playlistId);
                    checkCmd.Parameters.AddWithValue("@musica", trackId);
                    if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0) return;
                }

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

        // --- CORREÇÃO CRÍTICA 2: LIMPEZA PROFUNDA ---
        public void LimparDuplicatasNoBanco()
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // ETAPA A: Limpar duplicatas na tabela de Vínculos (LisMus)
                        // (Mesma música na mesma lista várias vezes)
                        string sqlLink = @"
                            DELETE FROM LisMus 
                            WHERE rowid NOT IN (
                                SELECT MIN(rowid) 
                                FROM LisMus 
                                GROUP BY Lista, Musica
                            )";
                        using (var cmd = new SQLiteCommand(sqlLink, conn, transaction)) { cmd.ExecuteNonQuery(); }

                        // ETAPA B: Limpar duplicatas na tabela de Arquivos (Musica)
                        // (Mesmo arquivo cadastrado com IDs diferentes)
                        // ATENÇÃO: Mantemos o MENOR ID (o mais antigo) e deletamos os novos duplicados
                        string sqlMusica = @"
                            DELETE FROM Musica 
                            WHERE ID NOT IN (
                                SELECT MIN(ID) 
                                FROM Musica 
                                GROUP BY Lugar
                            )";
                        using (var cmd = new SQLiteCommand(sqlMusica, conn, transaction)) { cmd.ExecuteNonQuery(); }

                        // ETAPA C: Limpar Vínculos Órfãos
                        // (Links na playlist que apontavam para os IDs que acabamos de deletar na Etapa B)
                        string sqlOrf = "DELETE FROM LisMus WHERE Musica NOT IN (SELECT ID FROM Musica)";
                        using (var cmd = new SQLiteCommand(sqlOrf, conn, transaction)) { cmd.ExecuteNonQuery(); }

                        transaction.Commit();
                        Debug.WriteLine("[REPO] Limpeza Completa (Links e Arquivos) executada.");
                    }
                    catch
                    {
                        transaction.Rollback();
                    }
                }
            }
        }

        public List<Track> GetTracksByPlaylist(int playlistId)
        {
            var tracks = new List<Track>();
            var progRepo = new ProgrammingRepository();
            var config = progRepo.ObterConfiguracao();

            try
            {
                using (var conn = Database.GetConnection())
                {
                    conn.Open();

                    // 1. Descobrimos o nome da lista para tratar a exceção da AESCOLHER
                    string nomeLista = "";
                    using (var cmdName = new SQLiteCommand("SELECT Nome FROM Lista WHERE ID = @id", conn))
                    {
                        cmdName.Parameters.AddWithValue("@id", playlistId);
                        nomeLista = cmdName.ExecuteScalar()?.ToString() ?? "";
                    }

                    // Regra da Ordenação:
                    bool usarOrdenacaoOriginal = (!config.ProgramacaoAtiva && nomeLista.ToUpper() == "AESCOLHER");

                    string sql;
                    DateTime dataLimite = DateTime.Now.AddMinutes(-config.TempoMudaLista);

                    if (usarOrdenacaoOriginal)
                    {
                        // MODO MANUAL: AESCOLHER
                        sql = @"
                    SELECT 
                        m.ID, m.Nome, m.Lugar, m.Tempo, 
                        b.ID as BandId, b.Nome as BandName
                    FROM Musica m
                    LEFT JOIN Banda b ON m.Banda = b.ID
                    JOIN LisMus lm ON m.ID = lm.Musica
                    WHERE lm.Lista = @listaId
                    GROUP BY m.ID
                    ORDER BY b.Nome ASC, m.Nome ASC";
                    }
                    else
                    {
                        // MODO INTELIGENTE: Pular / Pulado + Filtro de Tempo
                        string filtroTempo = config.ProgramacaoAtiva
                            ? "AND (m.TocadoEmG IS NULL OR m.TocadoEmG <= @dataLimite)"
                            : "";

                        sql = $@"
                    SELECT 
                        m.ID, m.Nome, m.Lugar, m.Tempo, 
                        b.ID as BandId, b.Nome as BandName
                    FROM Musica m
                    LEFT JOIN Banda b ON m.Banda = b.ID
                    JOIN LisMus lm ON m.ID = lm.Musica
                    WHERE lm.Lista = @listaId
                    {filtroTempo}
                    
                    -- AQUI ENTRA A REGRA DO PULAR: 
                    -- Toca se não tem restrição (Pular=0) OU se já cumpriu a cota (Pular=Pulado)
                    AND (COALESCE(m.Pular, 0) = 0 OR COALESCE(m.Pular, 0) = COALESCE(m.Pulado, 0))
                    
                    GROUP BY m.ID
                    ORDER BY m.vez ASC, m.TocadoEmG ASC";
                    }

                    // EXECUTAR A LEITURA DAS MÚSICAS
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.Parameters.AddWithValue("@listaId", playlistId);
                        if (sql.Contains("@dataLimite"))
                        {
                            cmd.Parameters.AddWithValue("@dataLimite", dataLimite.ToString("yyyy-MM-dd HH:mm:ss"));
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var t = new Track();
                                t.Id = reader.GetInt32(0);
                                t.Title = reader.IsDBNull(1) ? "Sem Título" : reader.GetString(1);
                                t.FilePath = reader.IsDBNull(2) ? "" : reader.GetString(2);

                                string tempoStr = reader.IsDBNull(3) ? "00:00:00" : reader.GetString(3);
                                if (TimeSpan.TryParse(tempoStr, out TimeSpan ts)) t.Duration = ts;
                                else t.Duration = TimeSpan.Zero;

                                t.BandId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                                t.BandName = reader.IsDBNull(5) ? "Desconhecida" : reader.GetString(5);

                                tracks.Add(t);
                            }
                        }
                    }

                    // ------------------------------------------------------------------
                    // PÓS-PROCESSAMENTO: INCREMENTAR OS PULOS E ZERAR QUEM TOCOU
                    // ------------------------------------------------------------------
                    if (!usarOrdenacaoOriginal && tracks.Count > 0)
                    {
                        string filtroTempoUpdate = config.ProgramacaoAtiva
                            ? "AND (m.TocadoEmG IS NULL OR m.TocadoEmG <= @dataLimite)"
                            : "";

                        // Ação A: Incrementar +1 no 'Pulado' de quem estava elegível mas ficou de fora
                        string sqlIncrementar = $@"
                    UPDATE Musica 
                    SET Pulado = COALESCE(Pulado, 0) + 1
                    WHERE ID IN (
                        SELECT m.ID FROM Musica m
                        JOIN LisMus lm ON m.ID = lm.Musica
                        WHERE lm.Lista = @listaId
                        {filtroTempoUpdate}
                    )
                    AND COALESCE(Pular, 0) > 0 
                    AND COALESCE(Pulado, 0) < COALESCE(Pular, 0)";

                        // Ação B: Zerar o 'Pulado' de quem acabou de entrar na lista (cota atingida)
                        string sqlZerar = $@"
                    UPDATE Musica 
                    SET Pulado = 0
                    WHERE ID IN (
                        SELECT m.ID FROM Musica m
                        JOIN LisMus lm ON m.ID = lm.Musica
                        WHERE lm.Lista = @listaId
                        {filtroTempoUpdate}
                    )
                    AND COALESCE(Pular, 0) > 0 
                    AND COALESCE(Pulado, 0) = COALESCE(Pular, 0)";

                        using (var cmdInc = conn.CreateCommand())
                        {
                            cmdInc.CommandText = sqlIncrementar;
                            cmdInc.Parameters.AddWithValue("@listaId", playlistId);
                            if (sqlIncrementar.Contains("@dataLimite"))
                                cmdInc.Parameters.AddWithValue("@dataLimite", dataLimite.ToString("yyyy-MM-dd HH:mm:ss"));

                            cmdInc.ExecuteNonQuery();
                        }

                        using (var cmdZerar = conn.CreateCommand())
                        {
                            cmdZerar.CommandText = sqlZerar;
                            cmdZerar.Parameters.AddWithValue("@listaId", playlistId);
                            if (sqlZerar.Contains("@dataLimite"))
                                cmdZerar.Parameters.AddWithValue("@dataLimite", dataLimite.ToString("yyyy-MM-dd HH:mm:ss"));

                            cmdZerar.ExecuteNonQuery();
                        }
                    }

                    string modo = usarOrdenacaoOriginal ? "MANUAL" : "INTELIGENTE (Rodízio)";
                    System.Diagnostics.Debug.WriteLine($"[REPO] Lista '{nomeLista}' lida. Músicas: {tracks.Count}. Modo: {modo}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[REPO_ERRO] GetTracksByPlaylist: {ex.Message}");
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
                                        VALUES (@nome, 0, 0, 0, 100, 0, 0, 0, 0); SELECT last_insert_rowid();";
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

        public void Tocou(int id)
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                string sql = "Update Musica Set vez = vez + 1 where ID = @ID";
                using (var command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@ID", id);
                    command.ExecuteNonQuery();
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
                int bandaId = -1;

                // 1. Busca Banda ID
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
                        // 2. Remove Links
                        using (var cmd1 = connection.CreateCommand())
                        {
                            cmd1.Transaction = transaction;
                            cmd1.CommandText = "DELETE FROM LisMus WHERE Musica = @Id";
                            cmd1.Parameters.AddWithValue("@Id", trackId);
                            cmd1.ExecuteNonQuery();
                        }

                        // 3. Remove Musica
                        using (var cmd2 = connection.CreateCommand())
                        {
                            cmd2.Transaction = transaction;
                            cmd2.CommandText = "DELETE FROM Musica WHERE ID = @Id";
                            cmd2.Parameters.AddWithValue("@Id", trackId);
                            cmd2.ExecuteNonQuery();
                        }

                        // 4. Limpeza de Banda e Pasta Vazia
                        if (bandaId != -1)
                        {
                            using (var cmdCheck = new SQLiteCommand("SELECT COUNT(*) FROM Musica WHERE Banda = @BandaId", connection, transaction))
                            {
                                cmdCheck.Parameters.AddWithValue("@BandaId", bandaId);
                                long restante = (long)cmdCheck.ExecuteScalar();

                                if (restante == 0)
                                {
                                    string caminhoPastaBanda = string.Empty;
                                    using (var cmdPath = new SQLiteCommand("SELECT Lugar FROM Banda WHERE ID = @BandaId", connection, transaction))
                                    {
                                        cmdPath.Parameters.AddWithValue("@BandaId", bandaId);
                                        caminhoPastaBanda = cmdPath.ExecuteScalar()?.ToString();
                                    }

                                    using (var cmdDelBanda = new SQLiteCommand("DELETE FROM Banda WHERE ID = @BandaId", connection, transaction))
                                    {
                                        cmdDelBanda.Parameters.AddWithValue("@BandaId", bandaId);
                                        cmdDelBanda.ExecuteNonQuery();
                                    }

                                    transaction.Commit();
                                    TentarApagarPastaBanda(caminhoPastaBanda);
                                    return;
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
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
                if (Directory.GetFileSystemEntries(caminho).Length == 0) Directory.Delete(caminho);
            }
            catch { }
        }
        #endregion

        #region Copiar/Mover
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