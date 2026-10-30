using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CleanC
{
    public class RegRunResult
    {
        public int Deleted;
        public int Keys;
        public int Values;
        public int Folders;
        public int Shortcuts;
        public int Files;
        public int Failed;
        public int Skipped;
        public int Total;
        public bool Canceled;
        public string LogPath = "";
        public string BatPath = "";
        public string DriverBatPath = "";
        public List<string> FailedItems = new List<string>();
    }

    /// <summary>
    /// 用真实命令执行残留删除：注册表项写入 reg export / reg delete，
    /// 计划任务用 schtasks，服务先 sc stop 再 sc delete，驱动包用 pnputil，
    /// 防火墙与筛选器通过 COM / 注册表 API 直接处理；跑完由程序逐项回查。
    /// </summary>
    public static class RegRunner
    {
        private static readonly Regex DriverInfPattern = new Regex(@"^oem\d+\.inf$", RegexOptions.IgnoreCase);

        public static RegRunResult Run(List<RegItem> items, string backupDir, bool visible, StreamWriter log)
        {
            RegRunResult res = new RegRunResult();
            List<RegItem> todo = new List<RegItem>();
            for (int i = 0; i < items.Count; i++)
            {
                RegItem it = items[i];
                if (!it.Selected) continue;
                res.Total++;
                if (!Acceptable(it, log))
                {
                    res.Skipped++;
                    continue;
                }
                todo.Add(it);
            }
            if (todo.Count == 0) return res;

            string dir = CommandDir();
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string bat = Path.Combine(dir, "reg_" + stamp + ".bat");
            res.BatPath = bat;
            res.LogPath = Path.Combine(dir, "reg_" + stamp + ".log");

            try
            {
                Directory.CreateDirectory(dir);
                Directory.CreateDirectory(backupDir);
            }
            catch
            {
            }

            // 需要直接操作的项：防火墙规则、驱动筛选器（批处理无法安全改写 MULTI_SZ）
            bool[] directOk = new bool[todo.Count];
            for (int i = 0; i < todo.Count; i++)
            {
                RegItem it = todo[i];
                if (it.Kind == RegKind.Firewall)
                {
                    directOk[i] = Firewall.Remove(it.Aux, log);
                }
                else if (it.Kind == RegKind.Driver && !string.IsNullOrEmpty(it.ValueName))
                {
                    directOk[i] = ResidualActions.RemoveFilter(it.Hive, it.KeyPath, it.ValueName, it.Aux, backupDir, log);
                }
            }

            // 驱动包单独走“管理员 cmd 强删”，普通项留在主批处理
            List<RegItem> batItems = new List<RegItem>();
            List<RegItem> driverItems = new List<RegItem>();
            for (int i = 0; i < todo.Count; i++)
            {
                RegItem it = todo[i];
                if (it.Kind == RegKind.Driver && string.IsNullOrEmpty(it.ValueName))
                {
                    driverItems.Add(it);
                }
                else if (it.Kind != RegKind.Firewall &&
                    !(it.Kind == RegKind.Driver && !string.IsNullOrEmpty(it.ValueName)))
                {
                    batItems.Add(it);
                }
            }

            StringBuilder b = new StringBuilder();
            b.AppendLine("@echo off");
            b.AppendLine("title CleanC - 残留清理");
            b.AppendLine("echo ==================================================");
            b.AppendLine("echo   CleanC 残留清理（16 步流程）");
            b.AppendLine("echo   共 " + batItems.Count + " 项，删除前会先导出 .reg 备份");
            b.AppendLine("echo   备份目录: " + backupDir);
            if (driverItems.Count > 0)
            {
                b.AppendLine("echo   另有 " + driverItems.Count + " 个驱动包将在单独的管理员窗口强制删除");
            }
            b.AppendLine("echo ==================================================");
            b.AppendLine("echo.");

            for (int i = 0; i < batItems.Count; i++)
            {
                RegItem it = batItems[i];
                b.AppendLine("echo [" + (i + 1) + "/" + batItems.Count + "] " + Safe(it.Label));
                switch (it.Kind)
                {
                    case RegKind.Folder:
                    case RegKind.BrowserExt:
                        if (Directory.Exists(it.FilePath))
                        {
                            // 原则：不确定的先改名观察，不直接删
                            string bak = it.FilePath + ".CleanC_bak";
                            b.AppendLine("if exist \"" + bak + "\" rd /s /q \"" + bak + "\"");
                            b.AppendLine("move \"" + it.FilePath + "\" \"" + bak + "\"");
                        }
                        break;
                    case RegKind.Shortcut:
                    case RegKind.TempFile:
                        if (File.Exists(it.FilePath))
                        {
                            b.AppendLine("attrib -h -r -s \"" + it.FilePath + "\" >nul 2>&1");
                            b.AppendLine("del /f /q \"" + it.FilePath + "\"");
                        }
                        break;
                    case RegKind.Task:
                        b.AppendLine("schtasks /delete /tn \"" + it.Aux + "\" /f");
                        break;
                    case RegKind.Firewall:
                        b.AppendLine("echo   （防火墙规则已通过 COM 直接删除）");
                        break;
                    case RegKind.Driver:
                        b.AppendLine("echo   （筛选器值已通过注册表 API 直接移除）");
                        break;
                    case RegKind.Service:
                        string svc = string.IsNullOrEmpty(it.Aux) ? LastSeg(it.KeyPath) : it.Aux;
                        b.AppendLine("sc stop \"" + svc + "\" >nul 2>&1");
                        b.AppendLine("timeout /t 1 /nobreak >nul");
                        b.AppendLine("sc delete \"" + svc + "\" >nul 2>&1");
                        AppendRegDelete(b, it, backupDir);
                        break;
                    default:
                        AppendRegDelete(b, it, backupDir);
                        break;
                }
                b.AppendLine("echo.");
            }

            b.AppendLine("echo ==================================================");
            b.AppendLine("echo   命令执行完毕，窗口将在 2 秒后关闭");
            b.AppendLine("timeout /t 2 /nobreak >nul");
            b.AppendLine("exit");

            if (batItems.Count > 0)
            {
                try
                {
                    File.WriteAllText(bat, b.ToString(), Encoding.Default);
                }
                catch (Exception ex)
                {
                    if (log != null) log.WriteLine("BAT-FAIL " + ex.Message);
                    batItems.Clear();
                }

                if (batItems.Count > 0)
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(Cmd.SysExe("cmd.exe"), "/c \"" + bat + "\"");
                        psi.UseShellExecute = visible;
                        psi.WorkingDirectory = dir;
                        if (visible)
                        {
                            psi.WindowStyle = ProcessWindowStyle.Normal;
                        }
                        else
                        {
                            psi.CreateNoWindow = true;
                        }
                        using (Process p = Process.Start(psi))
                        {
                            if (p != null)
                            {
                                if (!p.WaitForExit(180000))
                                {
                                    try { p.Kill(); } catch { }
                                    res.Canceled = true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (log != null) log.WriteLine("RUN-FAIL " + ex.Message);
                    }
                }
            }

            if (driverItems.Count > 0)
            {
                RunDriverBat(driverItems, dir, stamp, log, res);
            }

            // 以真实状态为准，不依赖批处理输出
            for (int i = 0; i < todo.Count; i++)
            {
                RegItem it = todo[i];
                bool alive = Alive(it, directOk[i]);
                if (!alive)
                {
                    res.Deleted++;
                    if (it.Kind == RegKind.Folder || it.Kind == RegKind.BrowserExt) res.Folders++;
                    else if (it.Kind == RegKind.Shortcut) res.Shortcuts++;
                    else if (it.Kind == RegKind.TempFile) res.Files++;
                    else if (it.DeleteKey) res.Keys++;
                    else res.Values++;
                }
                else
                {
                    res.Failed++;
                    if (res.FailedItems.Count < 200) res.FailedItems.Add(it.Label);
                }
                if (log != null)
                {
                    string rollback = (it.Kind == RegKind.Folder || it.Kind == RegKind.BrowserExt)
                        ? "  (回退: move \"" + it.FilePath + ".CleanC_bak\" \"" + it.FilePath + "\")"
                        : "";
                    log.WriteLine((alive ? "FAIL " : "OK   ") + Describe(it) + rollback);
                }
            }
            return res;
        }

        private static bool Acceptable(RegItem it, StreamWriter log)
        {
            switch (it.Kind)
            {
                case RegKind.Folder:
                case RegKind.BrowserExt:
                    if (string.IsNullOrEmpty(it.FilePath) || !Directory.Exists(it.FilePath)) return false;
                    break;
                case RegKind.Shortcut:
                case RegKind.TempFile:
                    if (string.IsNullOrEmpty(it.FilePath) || !File.Exists(it.FilePath)) return false;
                    break;
                case RegKind.Task:
                    if (string.IsNullOrEmpty(it.Aux)) return false;
                    break;
                case RegKind.Firewall:
                    if (string.IsNullOrEmpty(it.Aux)) return false;
                    break;
                case RegKind.Driver:
                    if (string.IsNullOrEmpty(it.ValueName))
                    {
                        if (string.IsNullOrEmpty(it.Aux)) return false;
                        if (!DriverInfPattern.IsMatch(it.Aux))
                        {
                            if (log != null) log.WriteLine("SKIP [driver-inf] " + it.Aux);
                            return false;
                        }
                        string infPath = Path.Combine(Environment.GetFolderPath(
                            Environment.SpecialFolder.Windows), "INF", it.Aux);
                        if (!File.Exists(infPath)) return false;
                    }
                    else
                    {
                        if (!RegGuard.AllowedJunkPath(it.KeyPath, false, it.ValueName)) return false;
                    }
                    break;
                default:
                    if (!RegGuard.AllowedJunkPath(it.KeyPath, it.DeleteKey, it.ValueName))
                    {
                        if (log != null) log.WriteLine("SKIP [guard] " + it.FullKey);
                        return false;
                    }
                    break;
            }
            return true;
        }

        private static void AppendRegDelete(StringBuilder b, RegItem it, string backupDir)
        {
            string full = it.FullKey;
            if (it.DeleteKey)
            {
                string safe = it.KeyPath.Replace('\\', '_');
                if (safe.Length > 80) safe = safe.Substring(safe.Length - 80);
                string bak = Path.Combine(backupDir, safe + ".reg");
                b.AppendLine("reg export \"" + full + "\" \"" + bak + "\" /y >nul 2>&1");
                b.AppendLine("reg delete \"" + full + "\" /f");
            }
            else
            {
                string safe = it.KeyPath.Replace('\\', '_') + "_" + it.ValueName.Replace('\\', '_');
                if (safe.Length > 80) safe = safe.Substring(safe.Length - 80);
                string bak = Path.Combine(backupDir, safe + ".reg");
                b.AppendLine("reg export \"" + full + "\" \"" + bak + "\" /y >nul 2>&1");
                b.AppendLine("reg delete \"" + full + "\" /v \"" + it.ValueName + "\" /f");
            }
        }

        private static bool Alive(RegItem it, bool directOk)
        {
            switch (it.Kind)
            {
                case RegKind.Folder:
                case RegKind.BrowserExt:
                    return Directory.Exists(it.FilePath);
                case RegKind.Shortcut:
                case RegKind.TempFile:
                    return File.Exists(it.FilePath);
                case RegKind.Task:
                    return ResidualActions.TaskExists(it.Aux);
                case RegKind.Firewall:
                    return Firewall.Exists(it.Aux);
                case RegKind.Driver:
                    if (!string.IsNullOrEmpty(it.ValueName))
                        return ResidualActions.FilterStillPresent(it.Hive, it.KeyPath, it.ValueName, it.Aux);
                    if (!string.IsNullOrEmpty(it.Aux))
                    {
                        string inf = Path.Combine(Environment.GetFolderPath(
                            Environment.SpecialFolder.Windows), "INF", it.Aux);
                        if (File.Exists(inf)) return true;
                        string store = ResidualActions.DriverStoreFolder(it.Aux);
                        return store.Length > 0 && Directory.Exists(store);
                    }
                    return File.Exists(it.FilePath);
                default:
                    return Exists(it);
            }
        }

        /// <summary>
        /// 驱动包用“管理员模式 cmd”强制删除：pnputil /uninstall /force 失败时，
        /// 回退到 takeown + icacls + del 发布 INF，再删除 DriverStore FileRepository 目录。
        /// </summary>
        private static void RunDriverBat(List<RegItem> drivers, string dir, string stamp, StreamWriter log, RegRunResult res)
        {
            string bat = Path.Combine(dir, "driver_" + stamp + ".bat");
            res.DriverBatPath = bat;
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            StringBuilder b = BuildDriverBat(drivers, win);

            try
            {
                File.WriteAllText(bat, b.ToString(), Encoding.Default);
            }
            catch (Exception ex)
            {
                if (log != null) log.WriteLine("DRIVER-BAT-FAIL " + ex.Message);
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Cmd.SysExe("cmd.exe"), "/c \"" + bat + "\"");
                psi.UseShellExecute = true;
                if (!Native.IsAdmin()) psi.Verb = "runas";
                psi.WindowStyle = ProcessWindowStyle.Normal;
                psi.WorkingDirectory = dir;
                using (Process p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        if (!p.WaitForExit(300000))
                        {
                            try { p.Kill(); } catch { }
                            res.Canceled = true;
                        }
                        if (log != null) log.WriteLine("DRIVER-CMD exit=" + p.ExitCode + " bat=" + bat);
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log.WriteLine("DRIVER-ELEVATE-FAIL " + ex.Message + " bat=" + bat);
            }
        }

        private static StringBuilder BuildDriverBat(List<RegItem> drivers, string win)
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("@echo off");
            b.AppendLine("title CleanC - 管理员强制删除驱动");
            b.AppendLine("echo ==================================================");
            b.AppendLine("echo   CleanC 驱动强制删除（管理员权限）");
            b.AppendLine("echo   共 " + drivers.Count + " 个驱动包");
            b.AppendLine("echo   pnputil 失败时会回退为强制删除 INF 与 DriverStore 目录");
            b.AppendLine("echo ==================================================");
            b.AppendLine("echo.");
            for (int i = 0; i < drivers.Count; i++)
            {
                RegItem it = drivers[i];
                string inf = it.Aux;
                string infPath = Path.Combine(win, "INF", inf);
                string store = ResidualActions.DriverStoreFolder(inf);
                b.AppendLine("echo [" + (i + 1) + "/" + drivers.Count + "] " + Safe(it.Label));
                b.AppendLine("pnputil /delete-driver " + inf + " /uninstall /force");
                b.AppendLine("if exist \"" + infPath + "\" (");
                b.AppendLine("    takeown /f \"" + infPath + "\" /a >nul 2>&1");
                b.AppendLine("    icacls \"" + infPath + "\" /grant *S-1-5-32-544:F >nul 2>&1");
                b.AppendLine("    del /f /q \"" + infPath + "\"");
                if (!string.IsNullOrEmpty(store))
                {
                    b.AppendLine("    if exist \"" + store + "\" (");
                    b.AppendLine("        takeown /f \"" + store + "\" /r /a /d y >nul 2>&1");
                    b.AppendLine("        icacls \"" + store + "\" /grant *S-1-5-32-544:F /t /c /q >nul 2>&1");
                    b.AppendLine("        rd /s /q \"" + store + "\"");
                    b.AppendLine("    )");
                }
                b.AppendLine("    echo   已强制删除文件，重启后生效");
                b.AppendLine(")");
                b.AppendLine("echo.");
            }
            b.AppendLine("echo ==================================================");
            b.AppendLine("echo   驱动删除结束，窗口将在 3 秒后关闭");
            b.AppendLine("timeout /t 3 /nobreak >nul");
            b.AppendLine("exit");
            return b;
        }

        /// <summary>测试用：生成某个驱动包的强制删除脚本，不执行。</summary>
        public static string PreviewDriverScript(string infName)
        {
            RegItem it = new RegItem();
            it.Kind = RegKind.Driver;
            it.Aux = infName;
            it.Label = infName;
            it.Selected = true;
            List<RegItem> list = new List<RegItem>();
            list.Add(it);
            return BuildDriverBat(list, Environment.GetFolderPath(Environment.SpecialFolder.Windows)).ToString();
        }

        private static string Describe(RegItem it)
        {
            if (it.Kind == RegKind.Folder || it.Kind == RegKind.BrowserExt ||
                it.Kind == RegKind.Shortcut || it.Kind == RegKind.TempFile) return it.FilePath;
            if (it.Kind == RegKind.Task) return "TASK " + it.Aux;
            if (it.Kind == RegKind.Firewall) return "FIREWALL " + it.Label;
            if (it.Kind == RegKind.Driver) return string.IsNullOrEmpty(it.ValueName) ? "DRIVER " + it.Aux : it.FullKey + " > " + it.Aux;
            return it.FullKey;
        }

        private static string LastSeg(string path)
        {
            int cut = path.LastIndexOf('\\');
            return cut >= 0 ? path.Substring(cut + 1) : path;
        }

        private static string CommandDir()
        {
            string dir;
            try { dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
            catch { dir = Path.GetTempPath(); }
            return Path.Combine(Path.Combine(dir, "CleanC"), "regcmd");
        }

        private static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder b = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                b.Append(c == '&' || c == '|' || c == '<' || c == '>' || c == '^' || c == '%' ? '_' : c);
            }
            return b.ToString();
        }

        public static bool Exists(RegItem it)
        {
            try
            {
                RegistryHive hive = string.Equals(it.Hive, "HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                    ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                using (RegistryKey k = baseKey.OpenSubKey(it.KeyPath))
                {
                    if (k == null) return false;
                    if (it.DeleteKey) return true;
                    return k.GetValue(it.ValueName) != null;
                }
            }
            catch
            {
                return true;
            }
        }
    }
}
