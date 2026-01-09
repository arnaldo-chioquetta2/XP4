using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Mp3PlayerWinForms.Services
{
    public class GlobalHotkeyService : IMessageFilter
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private readonly IntPtr _hWnd;
        private readonly int _id;
        
        public event Action HotkeyPressed;

        public GlobalHotkeyService(IntPtr hWnd, int id = 1)
        {
            _hWnd = hWnd;
            _id = id;
            Application.AddMessageFilter(this);
        }

        public void Register(Keys key)
        {
            // No modifiers for simplicity as requested
            RegisterHotKey(_hWnd, _id, 0, (int)key);
        }

        public void Unregister()
        {
            UnregisterHotKey(_hWnd, _id);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == _id)
            {
                HotkeyPressed?.Invoke();
                return true;
            }
            return false;
        }
    }
}
