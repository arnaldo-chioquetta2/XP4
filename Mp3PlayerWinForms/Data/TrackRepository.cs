using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using XP3.Models;
using XP3.Services;

namespace XP3.Data
{
    public class TrackRepository
    {
        private static bool _youtubeColumnChecked = false;
        private static bool _videoColumnChecked = false;
        private static bool _equalizacaoColumnChecked = false;
        private static bool _equalizacaoAtivaColumnChecked = false;
        private static bool _equalizacaoBandasChecked = false;
        private static bool _prefetsTableChecked = false;

        public TrackRepository()
        {
            EnsureEqualizacaoColumn();
            EnsureEqualizacaoAtivaColumn();
            EnsureEqualizacaoBandasColumns();
            EnsureYoutubeColumn();
            EnsureVideoColumn();
            EnsurePrefetsTable();
        }

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

        // --- CORREÃ‡ÃƒO CRÃTICA 1: IMPEDIR CRIAÃ‡ÃƒO DE ID DUPLICADO ---
        public int AddTrack(Track track)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();

                // 1. Verifica se esse arquivo JÃ ESTÃ CADASTRADO
                using (var checkCmd = conn.CreateCommand())
                {
                    checkCmd.CommandText = "SELECT ID FROM Musica WHERE Lugar = @lugar";
                    checkCmd.Parameters.AddWithValue("@lugar", track.FilePath);
                    var existingId = checkCmd.ExecuteScalar();

                    if (existingId != null)
                    {
                        // Se jÃ¡ existe, NÃƒO cria novo. Retorna o ID existente.
                        Debug.WriteLine($"[REPO] Arquivo jÃ¡ existe no banco (ID {existingId}). Reutilizando.");
                        return Convert.ToInt32(existingId);
                    }
                }

                // 2. Se nÃ£o existe, cria novo
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO Musica 
                        (Nome, Lugar, Banda, Tempo, Tamanho, BitRate, VezErro, MaxVol, Equalizacao, EqualizacaoAtiva, EqMus0, EqMus1, EqMus2, EqMus3, EqMus4, EqMus5, EqMus6, EqMus7, EqMus8, EqMus9, Album, Unid, Pular, Pulado, NaoAchou, CutIni, CutFim) 
                        VALUES 
                        (@nome, @lugar, @banda, @tempo, 0, 0, 0, 100, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0); 
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

                // Verifica vÃ­nculo existente
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

        // --- CORREÃ‡ÃƒO CRÃTICA 2: LIMPEZA PROFUNDA ---
        public void LimparDuplicatasNoBanco()
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // ETAPA A: Limpar duplicatas na tabela de VÃ­nculos (LisMus)
                        // (Mesma mÃºsica na mesma lista vÃ¡rias vezes)
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
                        // ATENÃ‡ÃƒO: Mantemos o MENOR ID (o mais antigo) e deletamos os novos duplicados
                        string sqlMusica = @"
                            DELETE FROM Musica 
                            WHERE ID NOT IN (
                                SELECT MIN(ID) 
                                FROM Musica 
                                GROUP BY Lugar
                            )";
                        using (var cmd = new SQLiteCommand(sqlMusica, conn, transaction)) { cmd.ExecuteNonQuery(); }

                        // ETAPA C: Limpar VÃ­nculos Ã“rfÃ£os
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

                    // 1. Buscamos o nome da lista para a regra da AESCOLHER
                    string nomeLista = "";
                    using (var cmdName = new SQLiteCommand("SELECT Nome FROM Lista WHERE ID = @id", conn))
                    {
                        cmdName.Parameters.AddWithValue("@id", playlistId);
                        nomeLista = cmdName.ExecuteScalar()?.ToString() ?? "";
                    }

                    bool usarOrdenacaoOriginal = (!config.ProgramacaoAtiva && nomeLista.ToUpper() == "AESCOLHER");
                    DateTime dataLimite = DateTime.Now.AddMinutes(-config.TempoMudaLista);

                    // 2. ATUALIZAMOS O SELECT: IncluÃ­mos m.CutIni e m.CutFim no final
                    string colunas = "m.ID, m.Nome, m.Lugar, m.Tempo, b.ID as BandId, b.Nome as BandName, m.CutIni, m.CutFim, m.VideoPath, COALESCE(m.Equalizacao, 0) as Equalizacao, COALESCE(m.EqualizacaoAtiva, 1) as EqualizacaoAtiva, COALESCE(m.EqMus0, 0) as EqMus0, COALESCE(m.EqMus1, 0) as EqMus1, COALESCE(m.EqMus2, 0) as EqMus2, COALESCE(m.EqMus3, 0) as EqMus3, COALESCE(m.EqMus4, 0) as EqMus4, COALESCE(m.EqMus5, 0) as EqMus5, COALESCE(m.EqMus6, 0) as EqMus6, COALESCE(m.EqMus7, 0) as EqMus7, COALESCE(m.EqMus8, 0) as EqMus8, COALESCE(m.EqMus9, 0) as EqMus9";
                    string sql;

                    if (usarOrdenacaoOriginal)
                    {
                        sql = $@"SELECT {colunas} FROM Musica m 
                        LEFT JOIN Banda b ON m.Banda = b.ID 
                        JOIN LisMus lm ON m.ID = lm.Musica 
                        WHERE lm.Lista = @listaId 
                        GROUP BY m.ID ORDER BY b.Nome ASC, m.Nome ASC";
                    }
                    else
                    {
                        string filtroTempo = config.ProgramacaoAtiva ? "AND (m.TocadoEmG IS NULL OR m.TocadoEmG <= @dataLimite)" : "";
                        sql = $@"SELECT {colunas} FROM Musica m 
                        LEFT JOIN Banda b ON m.Banda = b.ID 
                        JOIN LisMus lm ON m.ID = lm.Musica 
                        WHERE lm.Lista = @listaId {filtroTempo}
                        AND (COALESCE(m.Pular, 0) = 0 OR COALESCE(m.Pular, 0) = COALESCE(m.Pulado, 0))
                        GROUP BY m.ID ORDER BY m.vez ASC, m.TocadoEmG ASC";
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.Parameters.AddWithValue("@listaId", playlistId);
                        if (sql.Contains("@dataLimite")) cmd.Parameters.AddWithValue("@dataLimite", dataLimite.ToString("yyyy-MM-dd HH:mm:ss"));

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var t = new Track();
                                t.Id = reader.GetInt32(0);
                                t.Title = reader.IsDBNull(1) ? "Sem TÃ­tulo" : reader.GetString(1);
                                t.FilePath = reader.IsDBNull(2) ? "" : reader.GetString(2);

                                // Tempo
                                string tempoStr = reader.IsDBNull(3) ? "00:00:00" : reader.GetString(3);
                                if (TimeSpan.TryParse(tempoStr, out TimeSpan ts)) t.Duration = ts;

                                // Banda
                                t.BandId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                                t.BandName = reader.IsDBNull(5) ? "Desconhecida" : reader.GetString(5);

                                // --- NOVOS CAMPOS: CutIni (Ãndice 6) e CutFim (Ãndice 7) ---
                                // Usamos -1 como fallback caso o banco retorne NULL por algum motivo
                                t.CutIni = reader.IsDBNull(6) ? -1 : Convert.ToInt32(reader["CutIni"]);
                                t.CutFim = reader.IsDBNull(7) ? -1 : Convert.ToInt32(reader["CutFim"]);
                                t.VideoPath = reader.IsDBNull(8) ? null : reader["VideoPath"].ToString();
                                t.EqualizacaoPresetId = reader.IsDBNull(9) ? 0 : Convert.ToInt32(reader["Equalizacao"]);
                                t.EqualizacaoAtiva = reader.IsDBNull(10) || Convert.ToInt32(reader["EqualizacaoAtiva"]) != 0;
                                t.EqualizacaoBandas = LerBandasMusica(reader, 11);

                                tracks.Add(t);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[REPO_ERRO] GetTracksByPlaylist: {ex.Message}");
            }

            return tracks;
        }

        // Dentro do seu arquivo TrackRepository.cs

        public void AtualizarCortesMusica(int musicaId, int cutIni, int cutFim)
        {
            try
            {
                using (var conn = Database.GetConnection())
                {
                    conn.Open();
                    string sql = "UPDATE Musica SET CutIni = @cutIni, CutFim = @cutFim WHERE ID = @id";

                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@cutIni", cutIni);
                        cmd.Parameters.AddWithValue("@cutFim", cutFim);
                        cmd.Parameters.AddWithValue("@id", musicaId);

                        cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine($"[REPO] Cortes atualizados para MÃºsica ID {musicaId}: Ini={cutIni}, Fim={cutFim}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[REPO_ERRO] Falha ao atualizar cortes: {ex.Message}");
            }
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
                using (var cmd = new SQLiteCommand("SELECT ID, Nome, Lugar, Banda, VideoPath, COALESCE(Equalizacao, 0), COALESCE(EqualizacaoAtiva, 1), COALESCE(EqMus0, 0), COALESCE(EqMus1, 0), COALESCE(EqMus2, 0), COALESCE(EqMus3, 0), COALESCE(EqMus4, 0), COALESCE(EqMus5, 0), COALESCE(EqMus6, 0), COALESCE(EqMus7, 0), COALESCE(EqMus8, 0), COALESCE(EqMus9, 0) FROM Musica WHERE ID = @id", conn))
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
                                BandId = reader.GetInt32(3),
                                VideoPath = reader.IsDBNull(4) ? null : reader.GetString(4),
                                EqualizacaoPresetId = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                EqualizacaoAtiva = reader.IsDBNull(6) || reader.GetInt32(6) != 0,
                                EqualizacaoBandas = LerBandasMusica(reader, 7)
                            };
                        }
                    }
                }
            }
            return null;
        }

        public void AtualizarEqualizacaoMusica(int musicaId, int presetId, bool ativa)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("UPDATE Musica SET Equalizacao = @presetId, EqualizacaoAtiva = @ativa WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@presetId", presetId);
                    cmd.Parameters.AddWithValue("@ativa", ativa ? 1 : 0);
                    cmd.Parameters.AddWithValue("@id", musicaId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void AtualizarBandasEqualizacaoMusica(int musicaId, int[] bandas, bool ativa)
        {
            bandas = NormalizarBandas(bandas);

            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(@"UPDATE Musica 
                                                     SET Equalizacao = 0,
                                                         EqualizacaoAtiva = @ativa,
                                                         EqMus0 = @eq0,
                                                         EqMus1 = @eq1,
                                                         EqMus2 = @eq2,
                                                         EqMus3 = @eq3,
                                                         EqMus4 = @eq4,
                                                         EqMus5 = @eq5,
                                                         EqMus6 = @eq6,
                                                         EqMus7 = @eq7,
                                                         EqMus8 = @eq8,
                                                         EqMus9 = @eq9
                                                     WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@ativa", ativa ? 1 : 0);
                    cmd.Parameters.AddWithValue("@eq0", bandas[0]);
                    cmd.Parameters.AddWithValue("@eq1", bandas[1]);
                    cmd.Parameters.AddWithValue("@eq2", bandas[2]);
                    cmd.Parameters.AddWithValue("@eq3", bandas[3]);
                    cmd.Parameters.AddWithValue("@eq4", bandas[4]);
                    cmd.Parameters.AddWithValue("@eq5", bandas[5]);
                    cmd.Parameters.AddWithValue("@eq6", bandas[6]);
                    cmd.Parameters.AddWithValue("@eq7", bandas[7]);
                    cmd.Parameters.AddWithValue("@eq8", bandas[8]);
                    cmd.Parameters.AddWithValue("@eq9", bandas[9]);
                    cmd.Parameters.AddWithValue("@id", musicaId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<EqualizerPreset> ListarPresetsEqualizacao()
        {
            var lista = new List<EqualizerPreset>();

            using (var conn = Database.GetConnection())
            {
                conn.Open();
                string sql = @"SELECT ID, Nome, eq0, eq1, eq2, eq3, eq4, eq5, eq6, eq7, eq8, eq9, COALESCE(idPerfil, 0)
                               FROM Prefets
                               ORDER BY Nome";

                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new EqualizerPreset
                        {
                            Id = reader.GetInt32(0),
                            Nome = reader.IsDBNull(1) ? "Preset" : reader.GetString(1),
                            Eq0 = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                            Eq1 = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                            Eq2 = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                            Eq3 = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                            Eq4 = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                            Eq5 = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                            Eq6 = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                            Eq7 = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                            Eq8 = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                            Eq9 = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                            IdPerfil = reader.IsDBNull(12) ? 0 : reader.GetInt32(12)
                        });
                    }
                }
            }

            return lista;
        }

        public EqualizerPreset ObterPresetEqualizacao(int presetId)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                string sql = @"SELECT ID, Nome, eq0, eq1, eq2, eq3, eq4, eq5, eq6, eq7, eq8, eq9, COALESCE(idPerfil, 0)
                               FROM Prefets
                               WHERE ID = @id";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", presetId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new EqualizerPreset
                            {
                                Id = reader.GetInt32(0),
                                Nome = reader.IsDBNull(1) ? "Preset" : reader.GetString(1),
                                Eq0 = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                Eq1 = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                Eq2 = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                Eq3 = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                Eq4 = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                                Eq5 = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                                Eq6 = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                                Eq7 = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                Eq8 = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                                Eq9 = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                                IdPerfil = reader.IsDBNull(12) ? 0 : reader.GetInt32(12)
                            };
                        }
                    }
                }
            }

            return null;
        }

        public int InserirPresetEqualizacao(string nome, int[] bandas, int idPerfil)
        {
            if (bandas == null || bandas.Length != EqualizerPreset.BandCount)
            {
                throw new ArgumentException("O preset precisa ter 10 bandas.", nameof(bandas));
            }

            using (var conn = Database.GetConnection())
            {
                conn.Open();
                string sql = @"INSERT INTO Prefets
                               (Nome, eq0, eq1, eq2, eq3, eq4, eq5, eq6, eq7, eq8, eq9, idPerfil)
                               VALUES
                               (@nome, @eq0, @eq1, @eq2, @eq3, @eq4, @eq5, @eq6, @eq7, @eq8, @eq9, @idPerfil);
                               SELECT last_insert_rowid();";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@nome", nome);
                    cmd.Parameters.AddWithValue("@eq0", bandas[0]);
                    cmd.Parameters.AddWithValue("@eq1", bandas[1]);
                    cmd.Parameters.AddWithValue("@eq2", bandas[2]);
                    cmd.Parameters.AddWithValue("@eq3", bandas[3]);
                    cmd.Parameters.AddWithValue("@eq4", bandas[4]);
                    cmd.Parameters.AddWithValue("@eq5", bandas[5]);
                    cmd.Parameters.AddWithValue("@eq6", bandas[6]);
                    cmd.Parameters.AddWithValue("@eq7", bandas[7]);
                    cmd.Parameters.AddWithValue("@eq8", bandas[8]);
                    cmd.Parameters.AddWithValue("@eq9", bandas[9]);
                    cmd.Parameters.AddWithValue("@idPerfil", idPerfil);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void DeletarPresetEqualizacao(int presetId)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    using (var cmdReset = new SQLiteCommand("UPDATE Musica SET Equalizacao = 0 WHERE Equalizacao = @id", conn, trans))
                    {
                        cmdReset.Parameters.AddWithValue("@id", presetId);
                        cmdReset.ExecuteNonQuery();
                    }

                    using (var cmdDelete = new SQLiteCommand("DELETE FROM Prefets WHERE ID = @id", conn, trans))
                    {
                        cmdDelete.Parameters.AddWithValue("@id", presetId);
                        cmdDelete.ExecuteNonQuery();
                    }

                    trans.Commit();
                }
            }
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

        public bool PlaylistNameExists(string nomePlaylist, int? ignorePlaylistId = null)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                string sql = "SELECT COUNT(*) FROM Lista WHERE Nome = @nome";
                if (ignorePlaylistId.HasValue)
                {
                    sql += " AND ID <> @id";
                }

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@nome", nomePlaylist);
                    if (ignorePlaylistId.HasValue)
                    {
                        cmd.Parameters.AddWithValue("@id", ignorePlaylistId.Value);
                    }

                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        public List<Track> GetAllTracksForPlaylistEditor()
        {
            var tracks = new List<Track>();
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                string sql = @"SELECT m.ID, m.Nome, m.Lugar, b.ID as BandId, b.Nome as BandName
                               FROM Musica m
                               LEFT JOIN Banda b ON m.Banda = b.ID
                               ORDER BY b.Nome ASC, m.Nome ASC";

                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tracks.Add(new Track
                        {
                            Id = reader.GetInt32(0),
                            Title = reader.IsDBNull(1) ? "Sem TÃ­tulo" : reader.GetString(1),
                            FilePath = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            BandId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                            BandName = reader.IsDBNull(4) ? "Desconhecida" : reader.GetString(4)
                        });
                    }
                }
            }

            return tracks;
        }

        public HashSet<int> GetTrackIdsByPlaylist(int playlistId)
        {
            var ids = new HashSet<int>();
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT Musica FROM LisMus WHERE Lista = @listaId", conn))
                {
                    cmd.Parameters.AddWithValue("@listaId", playlistId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ids.Add(reader.GetInt32(0));
                        }
                    }
                }
            }

            return ids;
        }

        public List<Track> GetTracksByPlaylistForManagement(int playlistId)
        {
            var tracks = new List<Track>();
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                string sql = @"SELECT m.ID, m.Nome, m.Lugar, b.ID as BandId, b.Nome as BandName
                               FROM Musica m
                               LEFT JOIN Banda b ON m.Banda = b.ID
                               JOIN LisMus lm ON m.ID = lm.Musica
                               WHERE lm.Lista = @listaId
                               ORDER BY b.Nome ASC, m.Nome ASC";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@listaId", playlistId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tracks.Add(new Track
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.IsDBNull(1) ? "Sem TÃ­tulo" : reader.GetString(1),
                                FilePath = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                BandId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                BandName = reader.IsDBNull(4) ? "Desconhecida" : reader.GetString(4)
                            });
                        }
                    }
                }
            }

            return tracks;
        }

        public void ReplaceTracksInPlaylist(int playlistId, IEnumerable<int> trackIds)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    using (var deleteCmd = new SQLiteCommand("DELETE FROM LisMus WHERE Lista = @listaId", conn, transaction))
                    {
                        deleteCmd.Parameters.AddWithValue("@listaId", playlistId);
                        deleteCmd.ExecuteNonQuery();
                    }

                    var idsUnicos = new HashSet<int>(trackIds);
                    foreach (int trackId in idsUnicos)
                    {
                        using (var insertCmd = new SQLiteCommand("INSERT INTO LisMus (Lista, Musica, JaTocou, PosLista) VALUES (@lista, @musica, 0, 0)", conn, transaction))
                        {
                            insertCmd.Parameters.AddWithValue("@lista", playlistId);
                            insertCmd.Parameters.AddWithValue("@musica", trackId);
                            insertCmd.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }
        }

        public void DeletePlaylist(int playlistId)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    using (var deleteLinksCmd = new SQLiteCommand("DELETE FROM LisMus WHERE Lista = @listaId", conn, transaction))
                    {
                        deleteLinksCmd.Parameters.AddWithValue("@listaId", playlistId);
                        deleteLinksCmd.ExecuteNonQuery();
                    }

                    using (var deletePlaylistCmd = new SQLiteCommand("DELETE FROM Lista WHERE ID = @listaId", conn, transaction))
                    {
                        deletePlaylistCmd.Parameters.AddWithValue("@listaId", playlistId);
                        deletePlaylistCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
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

        /// <summary>
        /// ObtÂ‚m todas as bandas cadastradas, ordenadas alfabeticamente
        /// </summary>
        public List<Band> GetAllBands()
        {
            var bands = new List<Band>();
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT ID, Nome FROM Banda ORDER BY Nome", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        bands.Add(new Band 
                        { 
                            Id = reader.GetInt32(0), 
                            Name = reader.IsDBNull(1) ? "Desconhecida" : reader.GetString(1) 
                        });
                    }
                }
            }
            return bands;
        }

        /// <summary>
        /// Atualiza a banda de uma mÂ£sica especÂ¡fica no banco de dados
        /// </summary>
        public void UpdateTrackBand(int trackId, int newBandId)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("UPDATE Musica SET Banda = @banda WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@banda", newBandId);
                    cmd.Parameters.AddWithValue("@id", trackId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public string GetTrackYouTubeUrl(int trackId)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT YouTubeUrl FROM Musica WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", trackId);
                    var result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ? null : result.ToString();
                }
            }
        }

        public void UpdateTrackYouTubeUrl(int trackId, string youtubeUrl)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("UPDATE Musica SET YouTubeUrl = @url WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@url", string.IsNullOrWhiteSpace(youtubeUrl) ? (object)DBNull.Value : youtubeUrl.Trim());
                    cmd.Parameters.AddWithValue("@id", trackId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public string GetTrackVideoPath(int trackId)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT VideoPath FROM Musica WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", trackId);
                    var result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ? null : result.ToString();
                }
            }
        }

        public void UpdateTrackVideoPath(int trackId, string videoPath)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("UPDATE Musica SET VideoPath = @path WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@path", string.IsNullOrWhiteSpace(videoPath) ? (object)DBNull.Value : videoPath.Trim());
                    cmd.Parameters.AddWithValue("@id", trackId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// ObtÂŽm o nome da banda pelo ID
        /// </summary>
        public void DeleteBandIfUnused(int bandId)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();

                using (var checkCmd = new SQLiteCommand("SELECT COUNT(*) FROM Musica WHERE Banda = @id", conn))
                {
                    checkCmd.Parameters.AddWithValue("@id", bandId);
                    long total = (long)checkCmd.ExecuteScalar();
                    if (total > 0) return;
                }

                using (var deleteCmd = new SQLiteCommand("DELETE FROM Banda WHERE ID = @id", conn))
                {
                    deleteCmd.Parameters.AddWithValue("@id", bandId);
                    deleteCmd.ExecuteNonQuery();
                }
            }
        }

        public string GetBandNameById(int bandId)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT Nome FROM Banda WHERE ID = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", bandId);
                    var result = cmd.ExecuteScalar();
                    return result != null ? result.ToString() : "Desconhecida";
                }
            }
        }

        private void EnsureEqualizacaoColumn()
        {
            if (_equalizacaoColumnChecked) return;

            using (var conn = Database.GetConnection())
            {
                conn.Open();
                if (!ColumnExists(conn, "Musica", "Equalizacao"))
                {
                    using (var alterCmd = new SQLiteCommand("ALTER TABLE Musica ADD COLUMN Equalizacao INTEGER NULL DEFAULT 0", conn))
                    {
                        alterCmd.ExecuteNonQuery();
                    }
                }
            }

            _equalizacaoColumnChecked = true;
        }

        private void EnsureEqualizacaoAtivaColumn()
        {
            if (_equalizacaoAtivaColumnChecked) return;

            using (var conn = Database.GetConnection())
            {
                conn.Open();
                if (!ColumnExists(conn, "Musica", "EqualizacaoAtiva"))
                {
                    using (var alterCmd = new SQLiteCommand("ALTER TABLE Musica ADD COLUMN EqualizacaoAtiva INTEGER NOT NULL DEFAULT 1", conn))
                    {
                        alterCmd.ExecuteNonQuery();
                    }
                }
            }

            _equalizacaoAtivaColumnChecked = true;
        }

        private void EnsureEqualizacaoBandasColumns()
        {
            if (_equalizacaoBandasChecked) return;

            using (var conn = Database.GetConnection())
            {
                conn.Open();
                for (int i = 0; i < EqualizerPreset.BandCount; i++)
                {
                    string nomeColuna = "EqMus" + i;
                    if (!ColumnExists(conn, "Musica", nomeColuna))
                    {
                        using (var alterCmd = new SQLiteCommand($"ALTER TABLE Musica ADD COLUMN {nomeColuna} INTEGER NOT NULL DEFAULT 0", conn))
                        {
                            alterCmd.ExecuteNonQuery();
                        }
                    }
                }
            }

            _equalizacaoBandasChecked = true;
        }

        private static int[] LerBandasMusica(SQLiteDataReader reader, int startIndex)
        {
            var bandas = EqualizerPreset.CreateFlatBands();
            for (int i = 0; i < EqualizerPreset.BandCount; i++)
            {
                bandas[i] = reader.IsDBNull(startIndex + i) ? 0 : reader.GetInt32(startIndex + i);
            }

            return bandas;
        }

        private static int[] NormalizarBandas(int[] bandas)
        {
            var normalizado = EqualizerPreset.CreateFlatBands();
            if (bandas == null)
            {
                return normalizado;
            }

            for (int i = 0; i < normalizado.Length && i < bandas.Length; i++)
            {
                normalizado[i] = Math.Max(-12, Math.Min(12, bandas[i]));
            }

            return normalizado;
        }

        private void EnsurePrefetsTable()
        {
            if (_prefetsTableChecked) return;

            using (var conn = Database.GetConnection())
            {
                conn.Open();
                string sql = @"CREATE TABLE IF NOT EXISTS Prefets (
                                   ID INTEGER PRIMARY KEY,
                                   Nome TEXT,
                                   eq0 INTEGER,
                                   eq1 INTEGER,
                                   eq2 INTEGER,
                                   eq3 INTEGER,
                                   eq4 INTEGER,
                                   eq5 INTEGER,
                                   eq6 INTEGER,
                                   eq7 INTEGER,
                                   eq8 INTEGER,
                                   eq9 INTEGER,
                                   idPerfil INTEGER
                               )";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            _prefetsTableChecked = true;
        }

        private bool ColumnExists(SQLiteConnection conn, string tableName, string columnName)
        {
            using (var cmd = new SQLiteCommand("PRAGMA table_info(" + tableName + ")", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string nomeColuna = reader["name"]?.ToString();
                    if (string.Equals(nomeColuna, columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void EnsureYoutubeColumn()
        {
            if (_youtubeColumnChecked) return;

            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("PRAGMA table_info(Musica)", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string nomeColuna = reader["name"]?.ToString();
                        if (string.Equals(nomeColuna, "YouTubeUrl", StringComparison.OrdinalIgnoreCase))
                        {
                            _youtubeColumnChecked = true;
                            return;
                        }
                    }
                }

                using (var alterCmd = new SQLiteCommand("ALTER TABLE Musica ADD COLUMN YouTubeUrl TEXT NULL", conn))
                {
                    alterCmd.ExecuteNonQuery();
                }
            }

            _youtubeColumnChecked = true;
        }

        private void EnsureVideoColumn()
        {
            if (_videoColumnChecked) return;

            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("PRAGMA table_info(Musica)", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string nomeColuna = reader["name"]?.ToString();
                        if (string.Equals(nomeColuna, "VideoPath", StringComparison.OrdinalIgnoreCase))
                        {
                            _videoColumnChecked = true;
                            return;
                        }
                    }
                }

                using (var alterCmd = new SQLiteCommand("ALTER TABLE Musica ADD COLUMN VideoPath TEXT NULL", conn))
                {
                    alterCmd.ExecuteNonQuery();
                }
            }

            _videoColumnChecked = true;
        }

        #endregion

        public void TocaMenos(int trackId)
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();
                string sql = "UPDATE Musica SET Pular = COALESCE(Pular, 0) + 10 WHERE ID = @Id";
                using (var command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Id", trackId);
                    command.ExecuteNonQuery();
                }
            }
        }

        public bool RenomearMusica(Track track, string novoNome)
        {
            try
            {
                // 1. Prepara os caminhos fÃ­sicos
                string diretorio = System.IO.Path.GetDirectoryName(track.FilePath);
                string extensao = System.IO.Path.GetExtension(track.FilePath);

                // Remove caracteres invÃ¡lidos que o usuÃ¡rio possa ter digitado (ex: ? \ / : *)
                string nomeLimpo = string.Join("_", novoNome.Split(System.IO.Path.GetInvalidFileNameChars()));

                string novoCaminhoFisico = System.IO.Path.Combine(diretorio, nomeLimpo + extensao);

                // 2. Renomeia fisicamente no Windows
                if (track.FilePath != novoCaminhoFisico)
                {
                    if (System.IO.File.Exists(novoCaminhoFisico))
                    {
                        throw new Exception("JÃ¡ existe uma mÃºsica com este nome na pasta.");
                    }
                    System.IO.File.Move(track.FilePath, novoCaminhoFisico);
                }

                // 3. Atualiza no Banco de Dados (Nome da mÃºsica e o novo Caminho/Lugar)
                using (var connection = Database.GetConnection())
                {
                    connection.Open();
                    string sql = "UPDATE Musica SET Nome = @Nome, Lugar = @Lugar WHERE ID = @Id";

                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@Nome", novoNome);
                        command.Parameters.AddWithValue("@Lugar", novoCaminhoFisico);
                        command.Parameters.AddWithValue("@Id", track.Id);
                        command.ExecuteNonQuery();
                    }
                }

                // 4. Atualiza o objeto em memÃ³ria para nÃ£o precisar recarregar o banco
                track.Title = novoNome;
                track.FilePath = novoCaminhoFisico;

                return true; // Sucesso!
            }
            catch (System.IO.IOException)
            {
                // Erro clÃ¡ssico: O arquivo estÃ¡ em uso (ex: sendo tocado agora mesmo)
                System.Windows.Forms.MessageBox.Show("NÃ£o Ã© possÃ­vel renomear a mÃºsica enquanto ela estÃ¡ tocando.", "Arquivo em uso");
                return false;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Erro ao renomear: {ex.Message}", "Erro");
                return false;
            }
        }

        // MÃ‰TODO 1: Chamado quando o usuÃ¡rio dÃ¡ ENTER na Grid
        public void AgendarRenomeacao(int trackId, string novoNome)
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();

                // 1. Atualiza o NOME no banco principal para a interface do rÃ¡dio jÃ¡ refletir a mudanÃ§a
                string sqlUpdate = "UPDATE Musica SET Nome = @Nome WHERE ID = @Id";
                using (var cmd = new SQLiteCommand(sqlUpdate, connection))
                {
                    cmd.Parameters.AddWithValue("@Nome", novoNome);
                    cmd.Parameters.AddWithValue("@Id", trackId);
                    cmd.ExecuteNonQuery();
                }

                // 2. Coloca na fila para o arquivo fÃ­sico ser renomeado no prÃ³ximo boot
                string sqlInsert = "INSERT INTO Renomear (ID, Nome) VALUES (@Id, @Nome)";
                using (var cmd = new SQLiteCommand(sqlInsert, connection))
                {
                    cmd.Parameters.AddWithValue("@Id", trackId);
                    cmd.Parameters.AddWithValue("@Nome", novoNome);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // MÃ‰TODO 2: Chamado quando o rÃ¡dio abre (equivalente ao RenomearArquivos do VB6)
        public void ProcessarRenomeacoesPendentes()
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();

                // Fazemos um JOIN para pegar o Lugar atual da mÃºsica
                string sqlSelect = "SELECT r.ID, r.Nome, m.Lugar FROM Renomear r INNER JOIN Musica m ON r.ID = m.ID";
                var idsConcluidos = new System.Collections.Generic.List<int>();

                using (var command = new SQLiteCommand(sqlSelect, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int id = Convert.ToInt32(reader["ID"]);
                        string novoNome = reader["Nome"].ToString();
                        string lugarAtual = reader["Lugar"].ToString();

                        if (System.IO.File.Exists(lugarAtual))
                        {
                            try
                            {
                                // Prepara o novo caminho fÃ­sico
                                string diretorio = System.IO.Path.GetDirectoryName(lugarAtual);
                                string extensao = System.IO.Path.GetExtension(lugarAtual);
                                string nomeLimpo = string.Join("_", novoNome.Split(System.IO.Path.GetInvalidFileNameChars()));
                                string novoCaminho = System.IO.Path.Combine(diretorio, nomeLimpo + extensao);

                                if (lugarAtual != novoCaminho && !System.IO.File.Exists(novoCaminho))
                                {
                                    // Move o arquivo fisicamente
                                    System.IO.File.Move(lugarAtual, novoCaminho);

                                    // Atualiza o novo 'Lugar' na tabela Musica
                                    string sqlUpdate = "UPDATE Musica SET Lugar = @Lugar WHERE ID = @Id";
                                    using (var cmdUpdate = new SQLiteCommand(sqlUpdate, connection))
                                    {
                                        cmdUpdate.Parameters.AddWithValue("@Lugar", novoCaminho);
                                        cmdUpdate.Parameters.AddWithValue("@Id", id);
                                        cmdUpdate.ExecuteNonQuery();
                                    }
                                }

                                // Marca como sucesso para deletar da fila
                                idsConcluidos.Add(id);
                            }
                            catch (Exception ex)
                            {
                                LogService.GravarErro($"Processar Fila Renomear (ID: {id})", ex);
                                // Se falhar (ex: bloqueado), ele NÃƒO entra na lista de concluÃ­dos
                                // e tenta de novo no prÃ³ximo boot.
                            }
                        }
                        else
                        {
                            // Se o arquivo original nÃ£o existe mais no disco, tira da fila para nÃ£o travar
                            idsConcluidos.Add(id);
                        }
                    }
                }

                // Limpa as tarefas concluÃ­das da tabela Renomear
                foreach (int id in idsConcluidos)
                {
                    string sqlDelete = "DELETE FROM Renomear WHERE ID = @Id";
                    using (var cmdDel = new SQLiteCommand(sqlDelete, connection))
                    {
                        cmdDel.Parameters.AddWithValue("@Id", id);
                        cmdDel.ExecuteNonQuery();
                    }
                }
            }
        }

        public bool ExistePorCaminho(string filePath)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                string sql = "SELECT COUNT(1) FROM Musica WHERE Lugar = @path";
                using (var cmd = new System.Data.SQLite.SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@path", filePath);
                    long count = (long)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

    }
}
