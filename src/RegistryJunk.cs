using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace CleanC
{
    /// <summary>
    /// 残留清理扫描的编排器。按用户要求的 16 步流程依次执行：
    /// 1–3 目录、4 计划任务、5 启动项、6 防火墙、7 服务、8 卸载项、
    /// 9–12 注册表全量 / Classes / Installer / 驱动与设备、13 浏览器、
    /// 14 快捷方式与应用列表、15 事件日志与临时缓存（16 为清理后验证，见 ResidualVerify）。
    /// 每条结果在删除前都会先 reg export 备份；目录只改名不直接删除。
    /// </summary>
    public static class RegistryJunk
    {
        private const int MaxItems = ResidualScan.MaxItems;

        private static readonly string[] UninstallRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        private static readonly string[] AppPathRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths"
        };

        private static readonly string[] RunRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce"
        };

        private static readonly string[] StartupApprovedRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder"
        };

        public static List<RegItem> Scan(Action<int, string> onStep, Func<bool> canceled)
        {
            List<RegItem> list = new List<RegItem>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ResidualCatalog cat = ResidualCatalog.Build(canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 1, "reg.stepS1");
            ResidualScan.ScanInstallDirs(list, seen, cat, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 2, "reg.stepS2");
            ResidualScan.ScanProgramData(list, seen, cat, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 3, "reg.stepS3");
            ResidualScan.ScanUserData(list, seen, cat, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 4, "reg.stepS4");
            ResidualScan.ScanTasks(list, seen, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 5, "reg.stepS5");
            ScanStartup(list, seen, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 6, "reg.stepS6");
            ResidualScan.ScanFirewall(list, seen, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 7, "reg.stepS7");
            ScanServices(list, seen, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 8, "reg.stepS8");
            ScanUninstall(cat, list, seen, canceled);
            ScanAppPaths(RegistryHive.LocalMachine, list, seen, canceled);
            ScanAppPaths(RegistryHive.CurrentUser, list, seen, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 9, "reg.stepS9");
            ScanMainKeys(cat, list, seen, canceled);
            ResidualScan.ScanSoftwareDeep(list, seen, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 10, "reg.stepS10");
            ResidualScan.ScanClasses(list, seen, cat, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 11, "reg.stepS11");
            ResidualScan.ScanInstaller(list, seen, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 12, "reg.stepS12");
            ResidualScan.ScanDrivers(list, seen, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 13, "reg.stepS13");
            ResidualScan.ScanBrowsers(list, seen, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 14, "reg.stepS14");
            ScanShortcuts(list, seen, canceled);
            ResidualScan.ScanRegisteredApps(list, seen, canceled);
            if (canceled != null && canceled()) return list;

            Step(onStep, 15, "reg.stepS15");
            ResidualScan.ScanEventLog(list, seen, canceled);
            ResidualScan.ScanTempAndUpdate(list, seen, canceled);

            list.Sort(delegate(RegItem a, RegItem b)
            {
                if (a.Step != b.Step) return a.Step - b.Step;
                return string.Compare(a.Label, b.Label, StringComparison.CurrentCultureIgnoreCase);
            });
            return list;
        }

        private static void Step(Action<int, string> onStep, int step, string key)
        {
            if (onStep != null) onStep(step, Loc.T(key));
        }

        // ------------------------------------------------------------ 步骤 8 卸载项

        private static void ScanUninstall(ResidualCatalog cat, List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            for (int i = 0; i < cat.Orphan.Count && list.Count < MaxItems; i++)
            {
                if (canceled != null && canceled()) return;
                UninstallRecord rec = cat.Orphan[i];
                RegItem it = new RegItem();
                it.Hive = HiveName(rec.Hive);
                it.KeyPath = rec.RootPath + "\\" + rec.KeyName;
                it.ValueName = "";
                it.Label = rec.Name;
                it.Reason = Loc.T("reg.reasonOrphan");
                it.Risk = RiskLevel.Caution;
                it.Kind = RegKind.Uninstall;
                it.Selected = true;
                it.Step = 8;
                string sig = it.Hive + "\\" + it.KeyPath.ToLowerInvariant();
                if (seen.Add(sig)) list.Add(it);
            }
        }

        private static void ScanAppPaths(RegistryHive hive, List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            for (int r = 0; r < AppPathRoots.Length; r++)
            {
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                    using (RegistryKey root = baseKey.OpenSubKey(AppPathRoots[r]))
                    {
                        if (root == null) continue;
                        string[] subs = root.GetSubKeyNames();
                        for (int i = 0; i < subs.Length && list.Count < MaxItems; i++)
                        {
                            if (canceled != null && canceled()) return;
                            try
                            {
                                using (RegistryKey sk = root.OpenSubKey(subs[i]))
                                {
                                    if (sk == null) continue;
                                    string target = Str(sk, "");
                                    if (target.Length == 0) continue;
                                    string p = PathUtil.Clean(target);
                                    int comma = p.LastIndexOf(',');
                                    if (comma > 2) p = PathUtil.Clean(p.Substring(0, comma));
                                    if (p.Length == 0) continue;
                                    if (File.Exists(p) || Directory.Exists(p)) continue;

                                    RegItem it = new RegItem();
                                    it.Hive = HiveName(hive);
                                    it.KeyPath = AppPathRoots[r] + "\\" + subs[i];
                                    it.ValueName = "";
                                    it.Label = subs[i];
                                    it.Reason = Loc.F("reg.reasonAppPath", PathUtil.Display(target));
                                    it.Risk = RiskLevel.Safe;
                                    it.Kind = RegKind.AppPath;
                                    it.Selected = true;
                                    it.Step = 8;
                                    string sig = it.Hive + "\\" + it.KeyPath.ToLowerInvariant();
                                    if (seen.Add(sig)) list.Add(it);
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        // ------------------------------------------------------------ 步骤 5 启动项

        private static void ScanStartup(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            RegistryHive[] hives = { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
            for (int h = 0; h < hives.Length && list.Count < MaxItems; h++)
            {
                for (int r = 0; r < RunRoots.Length && list.Count < MaxItems; r++)
                {
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hives[h], RegistryView.Default))
                        using (RegistryKey root = baseKey.OpenSubKey(RunRoots[r]))
                        {
                            if (root == null) continue;
                            string[] names = root.GetValueNames();
                            for (int i = 0; i < names.Length && list.Count < MaxItems; i++)
                            {
                                if (canceled != null && canceled()) return;
                                try
                                {
                                    string raw = Str(root, names[i]);
                                    if (raw.Length == 0) continue;
                                    string exe = Uninstaller.ExtractExe(PathUtil.Clean(raw));
                                    if (exe.Length == 0) continue;
                                    if (File.Exists(exe)) continue;
                                    if (exe.IndexOf("\\", StringComparison.Ordinal) < 0) continue;

                                    RegItem it = new RegItem();
                                    it.Hive = HiveName(hives[h]);
                                    it.KeyPath = RunRoots[r];
                                    it.ValueName = names[i];
                                    it.Label = names[i];
                                    it.Reason = Loc.F("reg.reasonRun", PathUtil.Display(raw));
                                    it.Risk = RiskLevel.Caution;
                                    it.Kind = RegKind.Run;
                                    it.Selected = true;
                                    it.Step = 5;
                                    string sig = it.Hive + "\\" + it.KeyPath.ToLowerInvariant() + "|" + names[i].ToLowerInvariant();
                                    if (seen.Add(sig)) list.Add(it);
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                for (int a = 0; a < StartupApprovedRoots.Length && list.Count < MaxItems; a++)
                {
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hives[h], RegistryView.Default))
                        using (RegistryKey root = baseKey.OpenSubKey(StartupApprovedRoots[a]))
                        {
                            if (root == null) continue;
                            string[] names = root.GetValueNames();
                            for (int i = 0; i < names.Length && list.Count < MaxItems; i++)
                            {
                                if (canceled != null && canceled()) return;
                                string name = names[i];
                                if (name.Length < 3) continue;
                                bool isFolder = StartupApprovedRoots[a].EndsWith("StartupFolder", StringComparison.OrdinalIgnoreCase);
                                bool known = isFolder ? StartupLinkExists(name) : RunValueExists(hives[h], name);
                                if (known) continue;

                                RegItem it = new RegItem();
                                it.Hive = HiveName(hives[h]);
                                it.KeyPath = StartupApprovedRoots[a];
                                it.ValueName = name;
                                it.Label = name;
                                it.Reason = Loc.F("reg.reasonStartupApproved", name);
                                it.Risk = RiskLevel.Safe;
                                it.Kind = RegKind.Run;
                                it.Selected = true;
                                it.Step = 5;
                                string sig = "approved|" + it.Hive + "|" + it.KeyPath.ToLowerInvariant() + "|" + name.ToLowerInvariant();
                                if (seen.Add(sig)) list.Add(it);
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static bool RunValueExists(RegistryHive hive, string name)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                {
                    for (int r = 0; r < RunRoots.Length; r++)
                    {
                        using (RegistryKey k = baseKey.OpenSubKey(RunRoots[r]))
                        {
                            if (k != null && k.GetValue(name) != null) return true;
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool StartupLinkExists(string name)
        {
            List<string> dirs = new List<string>();
            try { dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup))); } catch { }
            try { dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup))); } catch { }
            for (int i = 0; i < dirs.Count; i++)
            {
                if (string.IsNullOrEmpty(dirs[i]) || !Directory.Exists(dirs[i])) continue;
                try
                {
                    string[] files = Directory.GetFiles(dirs[i]);
                    for (int f = 0; f < files.Length; f++)
                    {
                        if (string.Equals(Path.GetFileName(files[f]), name, StringComparison.OrdinalIgnoreCase)) return true;
                        if (string.Equals(Path.GetFileNameWithoutExtension(files[f]), name, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
                catch
                {
                }
            }
            return false;
        }

        // ------------------------------------------------------------ 步骤 9 软件主键

        private static readonly HashSet<string> MainKeySkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "microsoft", "windows", "classes", "policies", "wow6432node", "clients", "registeredapplications",
            "intel", "amd", "nvidia", "realtek", "google", "mozilla", "adobe", "apple", "oracle", "java",
            "odbc", "system", "secure", "speech", "synaptics", "dell", "lenovo", "hp"
        };

        private static void ScanMainKeys(ResidualCatalog cat, List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            HashSet<string> tokens = cat.OrphanTokens;
            if (tokens.Count == 0) return;

            RegistryHive[] hives = { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
            for (int h = 0; h < hives.Length; h++)
            {
                for (int r = 0; r < 2; r++)
                {
                    if (canceled != null && canceled()) return;
                    string rootPath = r == 0 ? "SOFTWARE" : @"SOFTWARE\WOW6432Node";
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hives[h], RegistryView.Default))
                        using (RegistryKey root = baseKey.OpenSubKey(rootPath))
                        {
                            if (root == null) continue;
                            string[] subs = root.GetSubKeyNames();
                            for (int i = 0; i < subs.Length && list.Count < MaxItems; i++)
                            {
                                string leaf = subs[i];
                                string nl = Normalize(leaf);
                                if (MainKeySkip.Contains(leaf)) continue;
                                if (nl.Length < 4) continue;

                                bool matched = false;
                                foreach (string token in tokens)
                                {
                                    if (token.Length >= 4 && (nl.IndexOf(token, StringComparison.Ordinal) >= 0 ||
                                        (token.IndexOf(nl, StringComparison.Ordinal) >= 0 && nl.Length >= 6)))
                                    {
                                        matched = true;
                                        break;
                                    }
                                }
                                if (matched)
                                {
                                    AddMainKey(list, seen, hives[h], rootPath + "\\" + leaf, leaf, "reg.reasonOrphanKey", null, false);
                                    continue;
                                }

                                using (RegistryKey sk = root.OpenSubKey(leaf))
                                {
                                    if (sk == null) continue;
                                    string bad = DeadInstallPath(sk);
                                    if (bad.Length == 0) continue;
                                    AddMainKey(list, seen, hives[h], rootPath + "\\" + leaf, leaf,
                                        "reg.reasonDeadPath", PathUtil.Display(bad), false);
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static string DeadInstallPath(RegistryKey sk)
        {
            string[] names = { "InstallPath", "InstallLocation", "InstallDir", "InstallDirectory", "ExePath", "ApplicationPath" };
            for (int i = 0; i < names.Length; i++)
            {
                string v = Str(sk, names[i]);
                if (v.Length == 0) continue;
                string p = PathUtil.Clean(v);
                if (p.Length < 8) continue;
                if (p.IndexOf('\\') < 0) continue;
                if (Directory.Exists(p) || File.Exists(p)) continue;
                return p;
            }
            return "";
        }

        private static void AddMainKey(List<RegItem> list, HashSet<string> seen, RegistryHive hive,
            string path, string label, string reasonKey, string arg, bool selected)
        {
            if (!RegGuard.AllowedJunkPath(path, true, "")) return;
            RegItem it = new RegItem();
            it.Hive = HiveName(hive);
            it.KeyPath = path;
            it.ValueName = "";
            it.Label = label;
            it.Reason = arg == null ? Loc.T(reasonKey) : Loc.F(reasonKey, arg);
            it.Risk = RiskLevel.Caution;
            it.Kind = RegKind.MainKey;
            it.Selected = selected;
            it.Step = 9;
            string sig = it.Hive + "\\" + path.ToLowerInvariant();
            if (seen.Add(sig) && list.Count < MaxItems) list.Add(it);
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

        // ------------------------------------------------------------ 步骤 7 服务与驱动

        private static void ScanServices(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                using (RegistryKey root = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services"))
                {
                    if (root == null) return;
                    string[] subs = root.GetSubKeyNames();
                    for (int i = 0; i < subs.Length && list.Count < MaxItems; i++)
                    {
                        if (canceled != null && canceled()) return;
                        try
                        {
                            using (RegistryKey sk = root.OpenSubKey(subs[i]))
                            {
                                if (sk == null) continue;
                                string img = Str(sk, "ImagePath");
                                if (img.Length == 0) continue;
                                string file = ServiceFile(img);
                                if (file.Length == 0) continue;
                                if (File.Exists(file)) continue;
                                string keyPath = @"SYSTEM\CurrentControlSet\Services\" + subs[i];
                                if (!RegGuard.AllowedJunkPath(keyPath, true, "")) continue;

                                RegItem it = new RegItem();
                                it.Hive = "HKEY_LOCAL_MACHINE";
                                it.KeyPath = keyPath;
                                it.Aux = subs[i];
                                it.Label = Str(sk, "DisplayName");
                                if (it.Label.Length == 0 || it.Label[0] == '@') it.Label = subs[i];
                                it.Reason = Loc.F("reg.reasonService", PathUtil.Display(file));
                                it.Risk = RiskLevel.Caution;
                                it.Kind = RegKind.Service;
                                it.NeedsReboot = true;
                                it.Selected = false;
                                it.Step = 7;
                                string sig = it.Hive + "\\" + it.KeyPath.ToLowerInvariant();
                                if (seen.Add(sig)) list.Add(it);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static string ServiceFile(string imagePath)
        {
            string p = imagePath.Trim();
            if (p.StartsWith("\\??\\", StringComparison.Ordinal)) p = p.Substring(4);
            p = Environment.ExpandEnvironmentVariables(p);
            bool quoted = p.StartsWith("\"");
            if (quoted)
            {
                int end = p.IndexOf('"', 1);
                p = end > 1 ? p.Substring(1, end - 1) : p.Trim('"');
                return ResolveServicePath(p);
            }
            string whole = ResolveServicePath(p);
            if (whole.Length > 0 && File.Exists(whole)) return whole;
            int sp = p.IndexOf(' ');
            if (sp > 0)
            {
                string cut = ResolveServicePath(p.Substring(0, sp));
                if (cut.Length > 0) return cut;
            }
            return whole;
        }

        private static string ResolveServicePath(string p)
        {
            p = p.Trim();
            if (p.Length == 0 || p.IndexOf('\\') < 0) return "";
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (p.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(win, p.Substring(@"\SystemRoot\".Length));
            if (p.StartsWith(@"\DosDevices\", StringComparison.OrdinalIgnoreCase))
                return p.Substring(@"\DosDevices\".Length);
            if (p.StartsWith(@"SystemRoot\", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(win, p.Substring(@"SystemRoot\".Length));
            if (p.StartsWith(@"system32\", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(win, p);
            if (p.Length > 1 && p[1] == ':') return p;
            if (p.StartsWith("\\", StringComparison.Ordinal)) return p;
            return "";
        }

        // ------------------------------------------------------------ 步骤 14 快捷方式

        private static void ScanShortcuts(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            List<string> dirs = new List<string>();
            AddBase(dirs, Environment.SpecialFolder.StartMenu);
            AddBase(dirs, Environment.SpecialFolder.CommonStartMenu);
            AddBase(dirs, Environment.SpecialFolder.Desktop);
            AddBase(dirs, Environment.SpecialFolder.CommonDesktopDirectory);
            AddBase(dirs, Environment.SpecialFolder.Startup);
            AddBase(dirs, Environment.SpecialFolder.CommonStartup);
            try
            {
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string quick = Path.Combine(roaming, @"Microsoft\Internet Explorer\Quick Launch");
                if (Directory.Exists(quick)) dirs.Add(quick);
                string pinned = Path.Combine(quick, @"User Pinned\TaskBar");
                if (Directory.Exists(pinned)) dirs.Add(pinned);
                string pinnedStart = Path.Combine(quick, @"User Pinned\StartMenu");
                if (Directory.Exists(pinnedStart)) dirs.Add(pinnedStart);
            }
            catch
            {
            }

            for (int d = 0; d < dirs.Count && list.Count < MaxItems; d++)
            {
                List<string> files = new List<string>();
                CollectLinks(dirs[d], files, 0, 3);
                for (int i = 0; i < files.Count && list.Count < MaxItems; i++)
                {
                    if (canceled != null && canceled()) return;
                    string target = ResolveLink(files[i]);
                    if (target.Length == 0) continue;
                    if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
                    if (File.Exists(target) || Directory.Exists(target)) continue;

                    RegItem it = new RegItem();
                    it.Hive = "";
                    it.KeyPath = "";
                    it.FilePath = files[i];
                    it.Label = Path.GetFileName(files[i]);
                    it.Reason = Loc.F("reg.reasonShortcut", PathUtil.Display(target));
                    it.Risk = RiskLevel.Safe;
                    it.Kind = RegKind.Shortcut;
                    it.Selected = false;
                    it.Step = 14;
                    string sig = "lnk|" + files[i].ToLowerInvariant();
                    if (seen.Add(sig)) list.Add(it);
                }
            }
        }

        private static void AddBase(List<string> roots, Environment.SpecialFolder f)
        {
            try
            {
                string p = Environment.GetFolderPath(f);
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) roots.Add(p);
            }
            catch
            {
            }
        }

        private static void CollectLinks(string dir, List<string> outp, int depth, int maxDepth)
        {
            try
            {
                string[] files = Directory.GetFiles(dir, "*.lnk");
                for (int i = 0; i < files.Length && outp.Count < 200; i++) outp.Add(files[i]);
                if (depth >= maxDepth) return;
                string[] subs = Directory.GetDirectories(dir);
                for (int i = 0; i < subs.Length && outp.Count < 200; i++) CollectLinks(subs[i], outp, depth + 1, maxDepth);
            }
            catch
            {
            }
        }

        private static string ResolveLink(string path)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return "";
                object shell = Activator.CreateInstance(t);
                object lnk = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod,
                    null, shell, new object[] { path });
                object target = lnk.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty,
                    null, lnk, null);
                return target as string ?? "";
            }
            catch
            {
                return "";
            }
        }

        // ------------------------------------------------------------ 执行

        public static void Clean(List<RegItem> items, string backupDir, StreamWriter log,
            CleanResult result, Func<bool> canceled)
        {
            Clean(items, backupDir, log, result, canceled, true);
        }

        public static void Clean(List<RegItem> items, string backupDir, StreamWriter log,
            CleanResult result, Func<bool> canceled, bool visible)
        {
            RegRunResult r = RegRunner.Run(items, backupDir, visible, log);
            result.Deleted += r.Deleted;
            result.RegKeys += r.Keys;
            result.RegValues += r.Values;
            result.Skipped += r.Skipped + r.Failed;
            if (!string.IsNullOrEmpty(backupDir)) result.BackupDir = backupDir;
            for (int i = 0; i < r.FailedItems.Count && result.Failures.Count < 200; i++)
            {
                result.Failures.Add(Loc.F("reg.failItem", r.FailedItems[i]));
            }
        }

        private static string HiveName(RegistryHive hive)
        {
            return hive == RegistryHive.LocalMachine ? "HKEY_LOCAL_MACHINE" : "HKEY_CURRENT_USER";
        }

        private static string Str(RegistryKey k, string name)
        {
            try
            {
                object o = k.GetValue(name);
                if (o == null) return "";
                string s = o as string;
                if (s == null) s = o.ToString();
                return Environment.ExpandEnvironmentVariables(s).Trim();
            }
            catch
            {
                return "";
            }
        }
    }
}
