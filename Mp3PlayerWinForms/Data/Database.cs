using System.Data.SQLite;
using XP3.Models;

namespace XP3.Data
{
    public static class Database
    {
        // public static string ConnectionString => $"Data Source={AppConfig.DatabasePath};Version=3;";
        public static string ConnectionString => $"Data Source={AppConfig.DatabasePath};Version=3;Busy Timeout=5000;";

        public static SQLiteConnection GetConnection()
        {
            System.Diagnostics.Debug.WriteLine("[DB] ConnectionString: " + ConnectionString);
            System.Diagnostics.Debug.WriteLine("[DB] DatabasePath: " + AppConfig.DatabasePath);
            return new SQLiteConnection(ConnectionString);
        }
    }
}
