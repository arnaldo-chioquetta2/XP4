using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace XP3.Services
{
    public class GlobalHotkeyService
    {
        // Importações da API do Windows (user32.dll)
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private IntPtr _windowHandle;
        private int _currentId = 0;

        public event EventHandler<int> HotkeyPressed;

        public GlobalHotkeyService(IntPtr handle)
        {
            _windowHandle = handle;
        }

        // Modificadores: 0=Nenhum, 1=Alt, 2=Control, 4=Shift, 8=Windows
        // No GlobalHotkeyService.cs
        public bool Register(Keys key, uint modifiers = 0)
        {
            _currentId++;
            // Agora capturamos o retorno da API do Windows
            bool sucesso = RegisterHotKey(_windowHandle, _currentId, modifiers, (uint)key);
            return sucesso;
        }

        public void UnregisterAll()
        {
            for (int i = 1; i <= _currentId; i++)
            {
                UnregisterHotKey(_windowHandle, i);
            }
        }

        // Método que o Form vai chamar quando receber uma mensagem do Windows
        public void ProcessMessage(Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY)
            {
                // O WParam contém o ID da Hotkey que definimos
                int id = m.WParam.ToInt32();
                HotkeyPressed?.Invoke(this, id);
            }
        }
    }
}