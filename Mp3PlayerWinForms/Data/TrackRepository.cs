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

                    // 1. Buscamos o nome da lista para a regra da AESCOLHER
                    string nomeLista = "";
                    using (var cmdName = new SQLiteCommand("SELECT Nome FROM Lista WHERE ID = @id", conn))
                    {
                        cmdName.Parameters.AddWithValue("@id", playlistId);
                        nomeLista = cmdName.ExecuteScalar()?.ToString() ?? "";
                    }

                    bool usarOrdenacaoOriginal = (!config.ProgramacaoAtiva && nomeLista.ToUpper() == "AESCOLHER");
                    DateTime dataLimite = DateTime.Now.AddMinutes(-config.TempoMudaLista);

                    // 2. ATUALIZAMOS O SELECT: Incluímos m.CutIni e m.CutFim no final
                    string colunas = "m.ID, m.Nome, m.Lugar, m.Tempo, b.ID as BandId, b.Nome as BandName, m.CutIni, m.CutFim";
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
                                t.Title = reader.IsDBNull(1) ? "Sem Título" : reader.GetString(1);
                                t.FilePath = reader.IsDBNull(2) ? "" : reader.GetString(2);

                                // Tempo
                                string tempoStr = reader.IsDBNull(3) ? "00:00:00" : reader.GetString(3);
                                if (TimeSpan.TryParse(tempoStr, out TimeSpan ts)) t.Duration = ts;

                                // Banda
                                t.BandId = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                                t.BandName = reader.IsDBNull(5) ? "Desconhecida" : reader.GetString(5);

                                // --- NOVOS CAMPOS: CutIni (Índice 6) e CutFim (Índice 7) ---
                                // Usamos -1 como fallback caso o banco retorne NULL por algum motivo
                                t.CutIni = reader.IsDBNull(6) ? -1 : Convert.ToInt32(reader["CutIni"]);
                                t.CutFim = reader.IsDBNull(7) ? -1 : Convert.ToInt32(reader["CutFim"]);

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
                        System.Diagnostics.Debug.WriteLine($"[REPO] Cortes atualizados para Música ID {musicaId}: Ini={cutIni}, Fim={cutFim}");
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

        /// <summary>
        /// Obtm todas as bandas cadastradas, ordenadas alfabeticamente
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
        /// Atualiza a banda de uma m£sica espec¡fica no banco de dados
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

        /// <summary>
        /// Obtm o nome da banda pelo ID
        /// </summary>
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
                // 1. Prepara os caminhos físicos
                string diretorio = System.IO.Path.GetDirectoryName(track.FilePath);
                string extensao = System.IO.Path.GetExtension(track.FilePath);

                // Remove caracteres inválidos que o usuário possa ter digitado (ex: ? \ / : *)
                string nomeLimpo = string.Join("_", novoNome.Split(System.IO.Path.GetInvalidFileNameChars()));

                string novoCaminhoFisico = System.IO.Path.Combine(diretorio, nomeLimpo + extensao);

                // 2. Renomeia fisicamente no Windows
                if (track.FilePath != novoCaminhoFisico)
                {
                    if (System.IO.File.Exists(novoCaminhoFisico))
                    {
                        throw new Exception("Já existe uma música com este nome na pasta.");
                    }
                    System.IO.File.Move(track.FilePath, novoCaminhoFisico);
                }

                // 3. Atualiza no Banco de Dados (Nome da música e o novo Caminho/Lugar)
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

                // 4. Atualiza o objeto em memória para não precisar recarregar o banco
                track.Title = novoNome;
                track.FilePath = novoCaminhoFisico;

                return true; // Sucesso!
            }
            catch (System.IO.IOException)
            {
                // Erro clássico: O arquivo está em uso (ex: sendo tocado agora mesmo)
                System.Windows.Forms.MessageBox.Show("Não é possível renomear a música enquanto ela está tocando.", "Arquivo em uso");
                return false;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Erro ao renomear: {ex.Message}", "Erro");
                return false;
            }
        }

        // MÉTODO 1: Chamado quando o usuário dá ENTER na Grid
        public void AgendarRenomeacao(int trackId, string novoNome)
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();

                // 1. Atualiza o NOME no banco principal para a interface do rádio já refletir a mudança
                string sqlUpdate = "UPDATE Musica SET Nome = @Nome WHERE ID = @Id";
                using (var cmd = new SQLiteCommand(sqlUpdate, connection))
                {
                    cmd.Parameters.AddWithValue("@Nome", novoNome);
                    cmd.Parameters.AddWithValue("@Id", trackId);
                    cmd.ExecuteNonQuery();
                }

                // 2. Coloca na fila para o arquivo físico ser renomeado no próximo boot
                string sqlInsert = "INSERT INTO Renomear (ID, Nome) VALUES (@Id, @Nome)";
                using (var cmd = new SQLiteCommand(sqlInsert, connection))
                {
                    cmd.Parameters.AddWithValue("@Id", trackId);
                    cmd.Parameters.AddWithValue("@Nome", novoNome);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // MÉTODO 2: Chamado quando o rádio abre (equivalente ao RenomearArquivos do VB6)
        public void ProcessarRenomeacoesPendentes()
        {
            using (var connection = Database.GetConnection())
            {
                connection.Open();

                // Fazemos um JOIN para pegar o Lugar atual da música
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
                                // Prepara o novo caminho físico
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
                                // Se falhar (ex: bloqueado), ele NÃO entra na lista de concluídos
                                // e tenta de novo no próximo boot.
                            }
                        }
                        else
                        {
                            // Se o arquivo original não existe mais no disco, tira da fila para não travar
                            idsConcluidos.Add(id);
                        }
                    }
                }

                // Limpa as tarefas concluídas da tabela Renomear
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

    }
}