// CLASSE: ConfigModel
// VERSÃO: 1.0
// DATA: 2026-04-03
// MOTIVO: Modelo inicial para representar as configurações globais do sistema, como o estado da programação automática e a persistência da última playlist.

namespace XP3.Models
{
    public class ConfigModel
    {
        // Corresponde ao campo 'Progr' (0 ou 1 no SQLite)
        public bool ProgramacaoAtiva { get; set; }

        // Corresponde ao campo 'UltLista'
        public int UltimaPlaylistId { get; set; }
    }
}