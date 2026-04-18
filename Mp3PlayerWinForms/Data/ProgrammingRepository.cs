// CLASSE: ProgrammingRepository
// VERSÃO: 1.0
// DATA: 2026-04-03
// MOTIVO: Implementação dos métodos de persistência para a tabela 'Prog' e configurações.

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using XP3.Models;

namespace XP3.Data
{
    public class ProgrammingRepository
    {
        public List<ProgramacaoModel> ListarProgramacao()
        {
            var lista = new List<ProgramacaoModel>();
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                string sql = @"
                    SELECT p.ID, p.HorIn, p.Lista, p.Periodicidade, l.Nome 
                    FROM Prog p
                    INNER JOIN Lista l ON p.Lista = l.ID
                    ORDER BY p.HorIn ASC";

                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new ProgramacaoModel
                        {
                            Id = reader.GetInt32(0),
                            HorarioInicio = reader.GetDateTime(1),
                            PlaylistId = reader.GetInt32(2),
                            Periodicidade = reader.GetInt32(3),
                            NomePlaylist = reader.GetString(4)
                        });
                    }
                }
            }
            return lista;
        }

        // METODO: SalvarProgramacao
        // VERSÃO: 3.0
        // MOTIVO: Agora salva os horários (Prog) e a configuração de espera (Config) 
        // em uma única transação atômica.
        public void SalvarProgramacao(List<ProgramacaoModel> programacoes, int tempoMudaLista)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Limpa a programação atual (tabela Prog)
                        using (var delCmd = new SQLiteCommand("DELETE FROM Prog", conn, trans))
                        {
                            delCmd.ExecuteNonQuery();
                        }

                        // 2. Insere os novos botões/horários
                        foreach (var p in programacoes)
                        {
                            string sqlProg = @"INSERT INTO Prog (HorIn, Lista, Periodicidade) 
                                     VALUES (@hor, @lista, @per)";
                            using (var cmd = new SQLiteCommand(sqlProg, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@hor", p.HorarioInicio);
                                cmd.Parameters.AddWithValue("@lista", p.PlaylistId);
                                cmd.Parameters.AddWithValue("@per", p.Periodicidade);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 3. Atualiza a configuração global (tabela Config)
                        // Aqui salvamos o valor que veio do seu novo Combo
                        string sqlConfig = "UPDATE Config SET TempoMudaLista = @tempo";
                        using (var cmdCfg = new SQLiteCommand(sqlConfig, conn, trans))
                        {
                            cmdCfg.Parameters.AddWithValue("@tempo", tempoMudaLista);
                            cmdCfg.ExecuteNonQuery();
                        }

                        // Se chegou aqui sem erros, confirma tudo no arquivo de banco
                        trans.Commit();
                        System.Diagnostics.Debug.WriteLine($"[REPO] Programação e Tempo de Espera ({tempoMudaLista} min) salvos com sucesso.");
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        System.Diagnostics.Debug.WriteLine($"[REPO - ERRO] Falha crítica ao salvar: {ex.Message}");
                        throw;
                    }
                }
            }
        }

        public ConfigModel ObterConfiguracao()
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT Progr, UltLista, TempoMudaLista FROM Config LIMIT 1", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new ConfigModel
                        {
                            ProgramacaoAtiva = reader.GetBoolean(0),
                            UltimaPlaylistId = reader.GetInt32(1),
                            TempoMudaLista = Convert.ToInt32(reader["TempoMudaLista"])
                        };
                    }
                }
            }
            return new ConfigModel { ProgramacaoAtiva = false, UltimaPlaylistId = 1 };
        }

        public void SalvarEstadoProgramacao(bool ativo)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("UPDATE Config SET Progr = @ativo", conn))
                {
                    cmd.Parameters.AddWithValue("@ativo", ativo ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<Playlist> ObterTodasAsPlaylists()
        {
            var listaRetorno = new List<Playlist>();

            using (var connection = Database.GetConnection())
            {
                connection.Open();
                // No VB6 você usava a tabela "Lista". Aqui seguimos o mesmo padrão.
                string sql = "SELECT ID, Nome FROM Lista ORDER BY Nome";

                using (var command = new SQLiteCommand(sql, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        listaRetorno.Add(new Playlist
                        {
                            Id = Convert.ToInt32(reader["ID"]),
                            Name = reader["Nome"].ToString()
                        });
                    }
                }
            }

            return listaRetorno;
        }

    }
}