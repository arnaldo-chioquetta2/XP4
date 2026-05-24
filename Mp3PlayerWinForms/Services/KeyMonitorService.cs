using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mp3PlayerWinForms.Services
{
    public class KeyMonitorService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_ADD = 0x6B;
        private const int VK_SUBTRACT = 0x6D;
        private const int VK_OEM_PLUS = 0xBB; // Tecla '+' do teclado principal
        private const int VK_OEM_MINUS = 0xBD; // Tecla '-' do teclado principal

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public event Action OnVolumeUp;
        public event Action OnVolumeDown;

        public void StartMonitoring()
        {
            Task.Run(() => MonitorKeys(_cts.Token));
        }

        private void MonitorKeys(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (GetAsyncKeyState(VK_ADD) != 0 || GetAsyncKeyState(VK_OEM_PLUS) != 0)
                {
                    OnVolumeUp?.Invoke();
                    Thread.Sleep(200);
                }
                else if (GetAsyncKeyState(VK_SUBTRACT) != 0 || GetAsyncKeyState(VK_OEM_MINUS) != 0)
                {
                    OnVolumeDown?.Invoke();
                    Thread.Sleep(200);
                }
                Thread.Sleep(10);
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
