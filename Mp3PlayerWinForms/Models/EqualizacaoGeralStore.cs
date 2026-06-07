using XP3.Models;
using XP3.Services;

namespace XP3.Models
{
    public static class EqualizacaoGeralStore
    {
        public static bool Ativa { get; set; } = false;
        public static int[] Bandas { get; set; } = EqualizerPreset.CreateFlatBands();

        public static void Carregar(IniFileService iniService)
        {
            if (iniService == null) return;

            Ativa = iniService.Read("EqualizacaoGeral", "Ativa", "false").Equals("true", System.StringComparison.OrdinalIgnoreCase);

            string bandasStr = iniService.Read("EqualizacaoGeral", "Bandas", string.Empty);
            if (!string.IsNullOrEmpty(bandasStr))
            {
                var partes = bandasStr.Split(',');
                if (partes.Length == EqualizerPreset.BandCount)
                {
                    var bandas = new int[EqualizerPreset.BandCount];
                    for (int i = 0; i < EqualizerPreset.BandCount; i++)
                    {
                        int.TryParse(partes[i], out bandas[i]);
                    }

                    Bandas = bandas;
                }
            }
        }

        public static void Salvar()
        {
            var iniService = new IniFileService();
            iniService.Write("EqualizacaoGeral", "Ativa", Ativa.ToString());
            var bandasStr = string.Join(",", Bandas);
            iniService.Write("EqualizacaoGeral", "Bandas", bandasStr);
        }
    }
}
