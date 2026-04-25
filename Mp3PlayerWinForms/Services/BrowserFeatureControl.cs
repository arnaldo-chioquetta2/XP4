using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace XP3.Services
{
    internal static class BrowserFeatureControl
    {
        public static void ConfigureForCurrentProcess()
        {
            string exeName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            if (string.IsNullOrWhiteSpace(exeName)) return;

            TrySetFeature("FEATURE_BROWSER_EMULATION", exeName, 11001);
            TrySetFeature("FEATURE_96DPI_PIXEL", exeName, 1);
            TrySetFeature("FEATURE_GPU_RENDERING", exeName, 1);
        }

        private static void TrySetFeature(string featureName, string exeName, int value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                    $@"Software\Microsoft\Internet Explorer\Main\FeatureControl\{featureName}"))
                {
                    if (key == null) return;

                    object currentValue = key.GetValue(exeName);
                    if (currentValue is int intValue && intValue == value) return;

                    key.SetValue(exeName, value, RegistryValueKind.DWord);
                }
            }
            catch
            {
            }
        }
    }
}
