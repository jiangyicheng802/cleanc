using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;

namespace CleanC
{
    public static class Native
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool AnimateWindow(IntPtr hwnd, int time, int flags);

        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseCapture();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint ms);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint ms);

        /// <summary>
        /// 把系统定时器分辨率临时提升到 1ms。默认分辨率约 15.6ms，
        /// WinForms Timer 设 16ms 会被向上取整到 ~31ms，动画看起来像被锁在 30 帧。
        /// </summary>
        public static void BeginHighResolution()
        {
            try { timeBeginPeriod(1); } catch { }
        }

        public static void EndHighResolution()
        {
            try { timeEndPeriod(1); } catch { }
        }

        public static float SystemScale()
        {
            try
            {
                IntPtr dc = GetDC(IntPtr.Zero);
                int dpi = GetDeviceCaps(dc, 88);
                ReleaseDC(IntPtr.Zero, dc);
                if (dpi <= 0) dpi = 96;
                float s = dpi / 96f;
                if (s < 1f) s = 1f;
                return s;
            }
            catch
            {
                return 1f;
            }
        }

        public static void Round(IntPtr hwnd)
        {
            try
            {
                if (Environment.OSVersion.Version.Major >= 10)
                {
                    int pref = 2;
                    DwmSetWindowAttribute(hwnd, 33, ref pref, 4);
                }
            }
            catch
            {
            }
        }

        public static void Blend(IntPtr hwnd, int ms)
        {
            try
            {
                AnimateWindow(hwnd, ms, 0x00080000 | 0x00020000);
            }
            catch
            {
            }
        }

        public static void Drag(IntPtr hwnd)
        {
            try
            {
                ReleaseCapture();
                SendMessage(hwnd, 0x00A1, 0x0002, 0);
            }
            catch
            {
            }
        }

        public static bool IsAdmin()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal p = new WindowsPrincipal(id);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static bool Elevate(string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Arguments = args;
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ShowInExplorer(string path)
        {
            try
            {
                Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch
            {
            }
        }

        public static void OpenFolder(string path)
        {
            try
            {
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch
            {
            }
        }

        public static void OpenLog(string path)
        {
            try
            {
                if (System.IO.File.Exists(path)) Process.Start("notepad.exe", "\"" + path + "\"");
            }
            catch
            {
            }
        }
    }
}
