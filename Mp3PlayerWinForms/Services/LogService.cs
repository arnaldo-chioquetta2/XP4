using System;
using System.IO;

namespace XP3.Services
{
    public static class LogService
    {
        private static string caminhoLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "erro_visualizador.txt");

        public static void GravarErro(string contexto, Exception ex)
        {
            try
            {
                string conteudo = $"\r\n[{DateTime.Now}] ERRO EM {contexto}:\r\n" +
                                 $"Mensagem: {ex.Message}\r\n" +
                                 $"Inner: {ex.InnerException?.Message}\r\n" +
                                 $"{ex.StackTrace}\r\n";
                File.AppendAllText(caminhoLog, conteudo);
            }
            catch { }
        }
    }
}