using System;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;

namespace Mp3PlayerWinForms.Services
{
    public class IniFileService
    {
        private readonly string _path;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern long WritePrivateProfileString(string section, string key, string value, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder retVal, int size, string filePath);

        public IniFileService(string fileName = "config.ini")
        {
            _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        }

        public void Write(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, _path);
        }

        public string Read(string section, string key, string defaultValue = "")
        {
            var res = new StringBuilder(255);
            GetPrivateProfileString(section, key, defaultValue, res, 255, _path);
            return res.ToString();
        }

        public int ReadInt(string section, string key, int defaultValue = 0)
        {
            string val = Read(section, key, defaultValue.ToString());
            return int.TryParse(val, out int result) ? result : defaultValue;
        }
    }
}
