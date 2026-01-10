using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XP3.Services
{
    public class KeyPollingService
    {
        // Importação da DLL do Windows (Igual ao Private Declare Function... Lib "user32")
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private bool _monitorando = false;
        private Task _taskMonitoramento;

        // Evento para avisar o Form que a tecla foi apertada
        public event Action KeyPausePressed;

        // Se quiser implementar o volume depois igual ao VB6
        // public event Action KeyVolumeUp; 
        // public event Action KeyVolumeDown;

        public void Start()
        {
            if (_monitorando) return; // Já está rodando

            _monitorando = true;

            // Cria uma Thread separada (substituto moderno do DoEvents)
            _taskMonitoramento = Task.Run(() => LoopDeMonitoramento());
        }

        public void Stop()
        {
            _monitorando = false;
        }

        private void LoopDeMonitoramento()
        {
            while (_monitorando)
            {
                // Verifica a tecla PAUSE (Código 19 no VB6 e C#)
                short estadoPause = GetAsyncKeyState((int)Keys.Pause);

                // No VB6: If Rc = -32767 Then
                // -32767 em binário significa que o bit mais significativo (tecla pressionada)
                // e o bit menos significativo (pressionada recentemente) estão ativos.
                // Isso evita que o comando dispare 1000 vezes se você segurar a tecla.
                if (estadoPause == -32767)
                {
                    KeyPausePressed?.Invoke();
                }

                // Aqui você pode adicionar o 107 e 109 futuramente se quiser
                // short estadoVolUp = GetAsyncKeyState(107); ...

                // Pausa de 50ms igual ao VB6 (Sleep 50) para não fritar o processador
                Thread.Sleep(50);
            }
        }
    }
}