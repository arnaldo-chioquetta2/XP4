using System;
using System.Runtime.InteropServices;

namespace XP3.Forms
{
    /// <summary>
    /// API de AppBar do Windows via P/Invoke (shell32/user32).
    /// </summary>
    internal static class AppBarAPI
    {
        // Comandos ABM_*
        public const int ABM_NEW = 0x00000000;
        public const int ABM_REMOVE = 0x00000001;
        public const int ABM_QUERYPOS = 0x00000002;
        public const int ABM_SETPOS = 0x00000003;
        public const int ABM_GETSTATE = 0x00000004;
        public const int ABM_GETTASKBARPOS = 0x00000005;
        public const int ABM_ACTIVATE = 0x00000006;
        public const int ABM_GETAUTOHIDEBAR = 0x00000007;
        public const int ABM_SETAUTOHIDEBAR = 0x00000008;
        public const int ABM_WINDOWPOSCHANGED = 0x00000009;
        public const int ABM_SETSTATE = 0x0000000A;

        // Bordas ABE_*
        public const int ABE_LEFT = 0;
        public const int ABE_TOP = 1;
        public const int ABE_RIGHT = 2;
        public const int ABE_BOTTOM = 3;

        // Notificações ABN_* (recebidas via uCallbackMessage)
        public const int ABN_STATECHANGE = 0x00000000;
        public const int ABN_POSCHANGED = 0x00000001;
        public const int ABN_FULLSCREENAPP = 0x00000002;
        public const int ABN_WINDOWARRANGE = 0x00000003;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uCallbackMessage;
            public int uEdge;
            public RECT rc;
            public int lParam;
        }

        [DllImport("shell32.dll")]
        public static extern IntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern uint RegisterWindowMessage(string lpString);
    }
}