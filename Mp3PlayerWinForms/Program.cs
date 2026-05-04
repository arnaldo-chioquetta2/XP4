using System;
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

            BrowserFeatureControl.ConfigureForCurrentProcess();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new Inicial());
        }
    }
}
