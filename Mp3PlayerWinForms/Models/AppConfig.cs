using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XP3.Models
{
    public static class AppConfig
    {
        // Valores padrão caso o INI falhe
        public static string DatabasePath { get; set; } = @"D:\Prog\XP3\Mp3PlayerWinForms_Project\Mp3PlayerWinForms\player.db";
        // public static string DatabasePath { get; set; } = "player.db";
        public static string PastaBase { get; set; } = @"D:\MP3";
    }
}
