using System.Data.SQLite;
using XP3.Models;

namespace XP3.Data
{
    public static class Database
    {
        // Adicionei ";Version=3;" que é o padrão para System.Data.SQLite
        // AppConfig.DatabasePath = "";
        public static string ConnectionString => $"Data Source={AppConfig.DatabasePath};Version=3;";

        public static SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(ConnectionString);
        }
    }
}