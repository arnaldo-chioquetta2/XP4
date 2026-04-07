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

        public void SalvarProgramacao(List<ProgramacaoModel> programacoes)
        {
            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // Limpa a programação atual para sobrescrever com a nova (do editor visual)
                        using (var delCmd = new SQLiteCommand("DELETE FROM Prog", conn, trans))
                        {
                            delCmd.ExecuteNonQuery();
                        }

                        foreach (var p in programacoes)
                        {
                            string sql = @"INSERT INTO Prog (HorIn, Lista, Periodicidade) 
                                           VALUES (@hor, @lista, @per)";
                            using (var cmd = new SQLiteCommand(sql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@hor", p.HorarioInicio);
                                cmd.Parameters.AddWithValue("@lista", p.PlaylistId);
                                cmd.Parameters.AddWithValue("@per", p.Periodicidade);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        trans.Commit();
                    }
                    catch
                    {
                        trans.Rollback();
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
                using (var cmd = new SQLiteCommand("SELECT Progr, UltLista FROM Config LIMIT 1", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new ConfigModel
                        {
                            ProgramacaoAtiva = reader.GetBoolean(0),
                            UltimaPlaylistId = reader.GetInt32(1)
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

    }
}