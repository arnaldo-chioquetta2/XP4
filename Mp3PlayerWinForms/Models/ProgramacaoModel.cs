// CLASSE: ProgramacaoModel
// VERSÃO: 1.1
// DATA: 2026-04-03
// MOTIVO: Correção de erro de sintaxe na propriedade HorarioInicio.

using System;

namespace XP3.Models
{
    public class ProgramacaoModel
    {
        public int Id { get; set; }

        // Representa o HorIn do banco de dados (Horário de Início)
        public DateTime HorarioInicio { get; set; }

        // ID da Playlist (Lista)
        public int PlaylistId { get; set; }

        // 1=Diário, 2=Dias Úteis, 3=Sábado, 4=Domingo
        public int Periodicidade { get; set; }

        // Propriedade auxiliar para exibir o nome na interface
        public string NomePlaylist { get; set; }
    }
}