using System;
using System.Collections.Generic;
using System.IO;

namespace CleanC
{
    /// <summary>settings.ini 读写：%LOCALAPPDATA%\CleanC\settings.ini，改一个键不会冲掉其他键。</summary>
    public static class Settings
    {
        private const string FileName = "settings.ini";

        public static string FilePath
        {
            get
            {
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return System.IO.Path.Combine(System.IO.Path.Combine(dir, "CleanC"), FileName);
            }
        }

        public static string Get(string key, string fallback)
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return fallback;
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    if (!string.Equals(line.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase)) continue;
                    string v = line.Substring(eq + 1).Trim();
                    if (v.Length > 0) return v;
                }
            }
            catch
            {
            }
            return fallback;
        }

        public static void Set(string key, string value)
        {
            try
            {
                string path = FilePath;
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                List<string> lines = new List<string>();
                bool replaced = false;
                if (File.Exists(path))
                {
                    string[] old = File.ReadAllLines(path);
                    for (int i = 0; i < old.Length; i++)
                    {
                        string line = old[i].Trim();
                        if (line.Length == 0) continue;
                        int eq = line.IndexOf('=');
                        if (eq > 0 && string.Equals(line.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                        {
                            if (replaced) continue;
                            lines.Add(key + "=" + value);
                            replaced = true;
                            continue;
                        }
                        lines.Add(line);
                    }
                }
                if (!replaced) lines.Add(key + "=" + value);
                File.WriteAllText(path, string.Join("\r\n", lines.ToArray()) + "\r\n");
            }
            catch
            {
            }
        }
    }
}
