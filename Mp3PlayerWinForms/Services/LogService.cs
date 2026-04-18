using System;
using System.IO;
using XP3.Models;

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
                System.Diagnostics.Debug.WriteLine(conteudo);
            }
            catch { }
        }

        public static void GravarInfo(string contexto, string mensagem)
        {
            try
            {
                string conteudo = $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] INFO ({contexto}): {mensagem}\r\n";
                File.AppendAllText(caminhoLog, conteudo);
                System.Diagnostics.Debug.WriteLine(conteudo);
            }
            catch { }
        }

    }
}