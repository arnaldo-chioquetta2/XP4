using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using XP3.Services;
//using Mp3PlayerWinForms.Forms;

//using XP3.Forms;

namespace XP3.Forms
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => LogService.GravarErro("Thread UI", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                LogService.GravarErro("Erro fatal nao tratado", ex ?? new Exception(e.ExceptionObject?.ToString() ?? "Erro desconhecido"));
            };
            AppDomain.CurrentDomain.ProcessExit += (s, e) => LogService.GravarInfo("ProcessExit", "Processo XP3 encerrando.");
            Application.ApplicationExit += (s, e) => LogService.GravarInfo("ApplicationExit", "Application.Exit disparado.");
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogService.GravarErro("Task nao observada", e.Exception);
                e.SetObserved();
            };

            try
            {
                BrowserFeatureControl.ConfigureForCurrentProcess();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.Run(new Inicial());
            }
            catch (Exception ex)
            {
                LogService.GravarErro("Application.Run", ex);
                MessageBox.Show("Ocorreu um erro inesperado. O detalhe foi gravado no log.", "XP3", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
