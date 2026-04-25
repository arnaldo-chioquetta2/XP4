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
            BrowserFeatureControl.ConfigureForCurrentProcess();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new Inicial());
        }
    }
}
