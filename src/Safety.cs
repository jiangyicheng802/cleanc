using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CleanC
{
    /// <summary>
    /// 破坏性操作前的安全垫：创建系统还原点、导出注册表分支备份、复制文件备份。
    /// 对应清理原则里的「先还原点 + 先备份，再动手」。
    /// </summary>
    public static class Safety
    {
        public static string LastMessage = "";

        /// <summary>创建系统还原点。系统保护关闭时会失败，返回 false 但不阻断流程。</summary>
        public static bool TryCreateRestorePoint(string description, StreamWriter log)
        {
            try
            {
                string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Path.Combine(@"WindowsPowerShell\v1.0", "powershell.exe"));
                if (!File.Exists(ps)) ps = "powershell.exe";
                string safe = (description ?? "CleanC").Replace("'", "");
                string args = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " +
                    "\"Checkpoint-Computer -Description '" + safe + "' -RestorePointType 'MODIFY_SETTINGS'\"";
                string output;
                int code = Cmd.Run(ps, args, log, out output, 120000);
                LastMessage = code == 0 ? "created" : ("failed(" + code + ") " + Trim(output));
                if (log != null) log.WriteLine("RESTOREPOINT " + code + " " + Trim(output));
                return code == 0;
            }
            catch (Exception ex)
            {
                LastMessage = "error " + ex.Message;
                if (log != null) log.WriteLine("RESTOREPOINT-ERR " + ex.Message);
                return false;
            }
        }

        private static string Trim(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length > 160 ? s.Substring(0, 160) : s;
        }
    }
}
