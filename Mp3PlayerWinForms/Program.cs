using System;
using XP3.Data;
using System.Windows.Forms;
//using Mp3PlayerWinForms.Forms;

//using XP3.Forms;

namespace XP3.Forms
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Initialize Database
            //Database.Initialize();
            
            Application.Run(new Inicial());
        }
    }
}
