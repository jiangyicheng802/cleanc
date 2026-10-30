using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CleanC
{
    /// <summary>
    /// 对应清理原则里的「正在运行的软件缓存不要删」：
    /// 用进程名与缓存路径中 AppData\Local 之后的目录段做匹配，命中即视为该应用仍在运行。
    /// </summary>
    public static class ProcessGuard
    {
        private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static int _stamp;
        private static bool _ready;

        private static readonly HashSet<string> SkipSegments = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "appdata", "local", "roaming", "locallow", "users", "programdata", "temp", "tmp",
            "cache", "cache2", "caches", "codecache", "gpucache", "shadercache", "grshadercache",
            "dawncache", "d3dscache", "cachestorage", "user", "data", "default", "profile"
        };

        private static void Refresh()
        {
            int now = Environment.TickCount;
            if (_ready && unchecked(now - _stamp) < 3000) return;
            _ready = true;
            _stamp = now;
            Names.Clear();
            try
            {
                Process[] all = Process.GetProcesses();
                for (int i = 0; i < all.Length; i++)
                {
                    try
                    {
                        string n = Normalize(all[i].ProcessName);
                        if (n.Length >= 3) Names.Add(n);
                    }
                    catch
                    {
                    }
                    finally
                    {
                        try { all[i].Dispose(); } catch { }
                    }
                }
            }
            catch
            {
            }
        }

        private static string Normalize(string s)
        {
            if (s == null) return "";
            StringBuilder b = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c)) b.Append(char.ToLowerInvariant(c));
            }
            return b.ToString();
        }

        /// <summary>缓存路径的归属应用是否正在运行。</summary>
        public static bool IsCacheOwnerRunning(string cachePath)
        {
            Refresh();
            if (Names.Count == 0 || string.IsNullOrEmpty(cachePath)) return false;
            string p = cachePath.ToLowerInvariant();
            int cut = p.IndexOf("\\appdata\\local\\", StringComparison.Ordinal);
            string tail = cut >= 0 ? p.Substring(cut + "\\appdata\\local\\".Length) : p;
            string[] seg = tail.Split('\\');
            for (int i = 0; i < seg.Length; i++)
            {
                string n = Normalize(seg[i]);
                if (n.Length < 3 || SkipSegments.Contains(n)) continue;
                if (Names.Contains(n)) return true;
            }
            return false;
        }
    }
}
