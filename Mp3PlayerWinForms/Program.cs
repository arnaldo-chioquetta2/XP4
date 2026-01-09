using System;
using System.Windows.Forms;
using Mp3PlayerWinForms.Forms;
using Mp3PlayerWinForms.Data;

namespace Mp3PlayerWinForms
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Initialize Database
            Database.Initialize();
            
            Application.Run(new MainForm());
        }
    }
}
