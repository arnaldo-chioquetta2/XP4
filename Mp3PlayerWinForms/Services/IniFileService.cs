using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace XP3.Services // ou XP3.Services, verifique seu namespace atual
{
    public class IniFileService
    {
        private string Path;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        // Construtor que aceita o caminho
        public IniFileService(string iniPath = null)
        {
            // Se não passar caminho, pega o diretório do executável + config.ini
            Path = new FileInfo(iniPath ?? System.IO.Path.Combine(Application.StartupPath, "config.ini")).FullName;
        }

        public string Read(string section, string key, string defaultValue = "")
        {
            var retVal = new StringBuilder(255);
            GetPrivateProfileString(section, key, defaultValue, retVal, 255, Path);

            // Se retornou vazio, usa o default
            string valor = retVal.ToString();
            return string.IsNullOrWhiteSpace(valor) ? defaultValue : valor;
        }

        public void Write(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, Path);
        }

        // Método útil para ler inteiros direto
        public int ReadInt(string section, string key, int defaultValue = 0)
        {
            string valor = Read(section, key, defaultValue.ToString());
            if (int.TryParse(valor, out int result))
            {
                return result;
            }
            return defaultValue;
        }
    }
}