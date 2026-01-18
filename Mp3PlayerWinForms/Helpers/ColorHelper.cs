using System;
using System.Drawing;

namespace XP3.Helpers // Ajuste o namespace se necessário para o do seu projeto
{
    public static class ColorHelper
    {
        // Definição das cores-chave
        private static readonly Color CorBaixa = Color.FromArgb(255, 255, 0);   // Amarelo (R=255, G=255, B=0)
        private static readonly Color CorMedia = Color.FromArgb(255, 128, 0);   // Laranja (R=255, G=128, B=0) - O meio exato
        private static readonly Color CorAlta = Color.FromArgb(255, 0, 0);      // Vermelho (R=255, G=0, B=0)

        /// <summary>
        /// Retorna uma cor no gradiente Amarelo->Laranja->Vermelho baseada na intensidade (0.0 a 1.0)
        /// </summary>
        public static Color GetSpectrumColor(float intensidade)
        {
            // Garante que está entre 0 e 1
            if (intensidade < 0f) intensidade = 0f;
            if (intensidade > 1f) intensidade = 1f;

            if (intensidade <= 0.5f)
            {
                // PRIMEIRA METADE: Transição do Amarelo para o Laranja
                // Precisamos mapear a intensidade de (0.0 até 0.5) para um fator de (0.0 até 1.0)
                float fatorLocal = intensidade * 2.0f;
                return BlendColors(CorBaixa, CorMedia, fatorLocal);
            }
            else
            {
                // SEGUNDA METADE: Transição do Laranja para o Vermelho
                // Precisamos mapear a intensidade de (0.5 até 1.0) para um fator de (0.0 até 1.0)
                float fatorLocal = (intensidade - 0.5f) * 2.0f;
                return BlendColors(CorMedia, CorAlta, fatorLocal);
            }
        }

        // Função auxiliar para misturar duas cores linearmente
        private static Color BlendColors(Color corA, Color corB, float fator)
        {
            int r = (int)(corA.R + (corB.R - corA.R) * fator);
            int g = (int)(corA.G + (corB.G - corA.G) * fator);
            int b = (int)(corA.B + (corB.B - corA.B) * fator);

            return Color.FromArgb(r, g, b);
        }
    }
}