using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CleanC
{
    /// <summary>一条卸载注册表记录，用于建立“已安装 / 已失效”目录与名称目录。</summary>
    public class UninstallRecord
    {
        public RegistryHive Hive;
        public string RootPath = "";
        public string KeyName = "";
        public string Name = "";
        public string Publisher = "";
        public string Version = "";
        public string InstallDir = "";
        public string UninstallString = "";
        public bool Live;
        public bool Ignored;
        public bool Orphan;
    }

    /// <summary>软件名录：已安装词表（避免误判）、失效词表（用于残留匹配）。</summary>
    public class ResidualCatalog
    {
        public List<UninstallRecord> All = new List<UninstallRecord>();
        public List<UninstallRecord> Orphan = new List<UninstallRecord>();
        public HashSet<string> InstalledTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> OrphanTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> LiveNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> LiveDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] UninstallRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        public static ResidualCatalog Build(Func<bool> canceled)
        {
            ResidualCatalog c = new ResidualCatalog();
            RegistryHive[] hives = { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
            for (int h = 0; h < hives.Length; h++)
            {
                for (int r = 0; r < UninstallRoots.Length; r++)
                {
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hives[h], RegistryView.Default))
                        using (RegistryKey root = baseKey.OpenSubKey(UninstallRoots[r]))
                        {
                            if (root == null) continue;
                            string[] subs = root.GetSubKeyNames();
                            for (int i = 0; i < subs.Length; i++)
                            {
                                if (canceled != null && canceled()) return c;
                                try
                                {
                                    using (RegistryKey sk = root.OpenSubKey(subs[i]))
                                    {
                                        if (sk == null) continue;
                                        UninstallRecord rec = Read(hives[h], UninstallRoots[r], subs[i], sk);
                                        if (rec != null) c.All.Add(rec);
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

            for (int i = 0; i < c.All.Count; i++)
            {
                UninstallRecord rec = c.All[i];
                if (rec.Live)
                {
                    string n = Normalize(rec.Name);
                    if (n.Length >= 3) c.LiveNames.Add(n);
                    if (rec.InstallDir.Length > 0) c.LiveDirs.Add(PathUtil.Norm(rec.InstallDir));
                    AddTokens(c.InstalledTokens, rec.Name, 4);
                    string leaf = Path.GetFileName(rec.InstallDir);
                    if (leaf.Length > 0) AddTokens(c.InstalledTokens, leaf, 4);
                    AddTokens(c.InstalledTokens, rec.Publisher, 6);
                }
            }

            for (int i = 0; i < c.All.Count; i++)
            {
                UninstallRecord rec = c.All[i];
                if (rec.Live || rec.Ignored) continue;
                string n = Normalize(rec.Name);
                if (n.Length < 3) continue;
                // 同名软件在别处仍然安装 → 视为重装留下的旧键，不当作残留
                if (c.LiveNames.Contains(n)) continue;
                rec.Orphan = true;
                c.Orphan.Add(rec);
                AddTokens(c.OrphanTokens, rec.Name, 4);
                if (rec.Publisher.Length > 0) AddTokens(c.OrphanTokens, rec.Publisher, 6);
            }
            return c;
        }

        private static UninstallRecord Read(RegistryHive hive, string rootPath, string keyName, RegistryKey sk)
        {
            string name = Str(sk, "DisplayName");
            if (name.Length == 0) return null;
            if (Str(sk, "SystemComponent") == "1") return null;
            if (Str(sk, "NoRemove") == "1") return null;
            if (Str(sk, "ParentKeyName").Length > 0) return null;

            string rt = Str(sk, "ReleaseType");
            if (rt.IndexOf("update", StringComparison.OrdinalIgnoreCase) >= 0) return null;
            if (rt.IndexOf("hotfix", StringComparison.OrdinalIgnoreCase) >= 0) return null;
            if (rt.IndexOf("security", StringComparison.OrdinalIgnoreCase) >= 0) return null;
            if (name.StartsWith("Update for ", StringComparison.OrdinalIgnoreCase)) return null;
            if (name.StartsWith("Security Update", StringComparison.OrdinalIgnoreCase)) return null;
            if (IsKbName(name)) return null;

            string uninstall = Str(sk, "UninstallString");
            if (uninstall.Length == 0) uninstall = Str(sk, "QuietUninstallString");
            string loc = Str(sk, "InstallLocation");
            string icon = Str(sk, "DisplayIcon");
            if (uninstall.Length == 0 && loc.Length == 0) return null;

            UninstallRecord rec = new UninstallRecord();
            rec.Hive = hive;
            rec.RootPath = rootPath;
            rec.KeyName = keyName;
            rec.Name = name;
            rec.Publisher = Str(sk, "Publisher");
            rec.Version = Str(sk, "DisplayVersion");
            rec.UninstallString = uninstall;
            rec.InstallDir = Uninstaller.ResolveInstallDir(loc, uninstall, icon);

            bool hasDir = rec.InstallDir.Length > 0 && Directory.Exists(rec.InstallDir);
            string exe = uninstall.Length > 0 ? Uninstaller.ExtractExe(PathUtil.Clean(uninstall)) : "";
            bool hasExe = exe.Length > 0 && File.Exists(exe);
            bool msiexec = uninstall.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0;
            rec.Ignored = msiexec && !hasDir;
            rec.Live = hasDir || hasExe ||
                (icon.Length > 0 && File.Exists(StripIconIndex(PathUtil.Clean(icon))));
            return rec;
        }

        private static string StripIconIndex(string icon)
        {
            int comma = icon.LastIndexOf(',');
            if (comma > 2) return PathUtil.Clean(icon.Substring(0, comma));
            return icon;
        }

        private static bool IsKbName(string name)
        {
            if (name.Length < 5 || name.Length > 12) return false;
            if (char.ToUpperInvariant(name[0]) != 'K' || char.ToUpperInvariant(name[1]) != 'B') return false;
            for (int i = 2; i < name.Length; i++)
            {
                if (!char.IsDigit(name[i])) return false;
            }
            return true;
        }

        private static void AddTokens(HashSet<string> set, string text, int minLen)
        {
            if (string.IsNullOrEmpty(text)) return;
            string full = Normalize(text);
            if (full.Length >= minLen) set.Add(full);
            string[] parts = text.Split(new char[] { ' ', '-', '_', '.', '(', ')', '+', '/', '\\', ':' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = Normalize(parts[i]);
                if (p.Length >= minLen && p.Length <= 24) set.Add(p);
            }
        }

        public bool MatchesOrphan(string name)
        {
            return Match(OrphanTokens, name);
        }

        public bool MatchesInstalled(string name)
        {
            return Match(InstalledTokens, name);
        }

        private static bool Match(HashSet<string> tokens, string name)
        {
            string n = Normalize(name);
            if (n.Length < 4) return false;
            foreach (string t in tokens)
            {
                if (t.Length < 4) continue;
                if (n.IndexOf(t, StringComparison.Ordinal) >= 0) return true;
                if (t.IndexOf(n, StringComparison.Ordinal) >= 0 && n.Length >= 6) return true;
            }
            return false;
        }

        public bool IsLiveDir(string dir)
        {
            return LiveDirs.Contains(PathUtil.Norm(dir));
        }

        public static string Normalize(string s)
        {
            if (s == null) return "";
            StringBuilder b = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = char.ToLowerInvariant(s[i]);
                if (char.IsLetterOrDigit(c)) b.Append(c);
            }
            return b.ToString();
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

    /// <summary>
    /// 16 步残留扫描。第 1–3 步查目录，第 4–7 步查计划任务 / 启动项 / 防火墙 / 服务，
    /// 第 8–12 步全量查注册表与驱动，第 13–15 步查浏览器、快捷方式、日志与临时缓存。
    /// </summary>
    public static class ResidualScan
    {
        public const int MaxItems = 2000;

        /// <summary>最近一次驱动包扫描收集到的 InfPath 引用数量，测试模式用于核对。</summary>
        public static int LastInfReferenceCount;

        private const int SoftwareTimeBudgetMs = 12000;
        private const int ClassesTimeBudgetMs = 16000;
        private const int MaxSoftwareValues = 300;
        private const int MaxClassItems = 300;
        private const int MaxMiscItems = 150;

        private static readonly Regex TaskCommand = new Regex(@"<Command>([^<]+)</Command>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] ProtectedDirNames =
        {
            "microsoft", "windows", "common files", "windowsapps", "package cache", "installer",
            "temp", "tmp", "intel", "nvidia", "amd", "realtek", "google", "mozilla", "dotnet", ".net",
            "vc", "redistributable", "cleanc", "desktop.ini", "internet explorer", "windows nt",
            "windows defender", "windows mail", "modifiablewindowsapps", "packages", "programs",
            "systemapps", "systems", "assembly", "reference assemblies", "uninstall information",
            "connecteddevicesplatform", "appdata", "localappdata", "roaming",
            "softwaredistribution", "usoprivate", "usoshared", "whesvc", "providers",
            "expeditedappregistrations", "apprepository", "setup", "logs", "wdi", "diagtrack"
        };

        private static readonly string[] SystemPathPrefix =
        {
            @"c:\windows", @"c:\program files\windowsapps", @"c:\program files\windows defender",
            @"c:\program files\windows mail", @"c:\system volume information", @"c:\$",
            @"c:\programdata\package cache",
            @"c:\program files\common files", @"c:\program files (x86)\common files",
            @"c:\program files\microsoft", @"c:\program files (x86)\microsoft"
        };

        // ------------------------------------------------------------ 步骤 1–3 目录

        public static void ScanInstallDirs(List<RegItem> list, HashSet<string> seen,
            ResidualCatalog cat, Func<bool> canceled)
        {
            ScanDirRoot(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), 1, list, seen, cat, canceled, false);
            ScanDirRoot(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), 1, list, seen, cat, canceled, false);
        }

        public static void ScanProgramData(List<RegItem> list, HashSet<string> seen,
            ResidualCatalog cat, Func<bool> canceled)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            ScanDirRoot(root, 2, list, seen, cat, canceled, true);
        }

        public static void ScanUserData(List<RegItem> list, HashSet<string> seen,
            ResidualCatalog cat, Func<bool> canceled)
        {
            ScanDirRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 3, list, seen, cat, canceled, true);
            ScanDirRoot(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 3, list, seen, cat, canceled, true);
            try
            {
                string localLow = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"AppData\LocalLow");
                ScanDirRoot(localLow, 3, list, seen, cat, canceled, true);
            }
            catch
            {
            }
        }

        private static void ScanDirRoot(string root, int step, List<RegItem> list, HashSet<string> seen,
            ResidualCatalog cat, Func<bool> canceled, bool nested)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
            string[] subs;
            try { subs = Directory.GetDirectories(root); }
            catch { return; }
            for (int i = 0; i < subs.Length && list.Count < MaxItems; i++)
            {
                if (canceled != null && canceled()) return;
                EvaluateDir(subs[i], step, list, seen, cat, false);
                if (!nested) continue;
                string name = Path.GetFileName(subs[i]);
                if (ProtectedName(name)) continue;
                string[] inner;
                try { inner = Directory.GetDirectories(subs[i]); }
                catch { continue; }
                for (int k = 0; k < inner.Length && list.Count < MaxItems; k++)
                {
                    EvaluateDir(inner[k], step, list, seen, cat, true);
                }
            }
        }

        private static void EvaluateDir(string dir, int step, List<RegItem> list, HashSet<string> seen,
            ResidualCatalog cat, bool nested)
        {
            string name = Path.GetFileName(dir);
            if (name.Length < 3) return;
            if (ProtectedName(name)) return;
            try
            {
                if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) return;
            }
            catch
            {
                return;
            }
            if (cat.IsLiveDir(dir)) return;

            bool orphan = cat.MatchesOrphan(name);
            bool installed = cat.MatchesInstalled(name);
            if (!orphan && installed) return;
            // 只有安装目录（Program Files）才用“没有 exe + 没有安装记录”的弱证据；
            // ProgramData / AppData 只认能和失效卸载项对上的目录，避免误伤系统与应用数据。
            if (!orphan)
            {
                if (step > 1) return;
                if (!LooksLikeProduct(name)) return;
            }
            if (HasBinary(dir)) return;

            RegItem it = new RegItem();
            it.Hive = "";
            it.KeyPath = "";
            it.FilePath = dir;
            it.Label = name;
            it.Reason = orphan ? Loc.F("reg.reasonFolder", PathUtil.Display(dir))
                : Loc.F("reg.reasonDirNoExe", PathUtil.Display(dir));
            it.Risk = RiskLevel.Caution;
            it.Kind = RegKind.Folder;
            it.Selected = false;
            it.Step = step;
            string sig = "dir|" + dir.ToLowerInvariant();
            if (seen.Add(sig) && list.Count < MaxItems) list.Add(it);
        }

        private static bool ProtectedName(string name)
        {
            string n = name.Trim().ToLowerInvariant();
            for (int i = 0; i < ProtectedDirNames.Length; i++)
            {
                if (n == ProtectedDirNames[i]) return true;
            }
            return n.StartsWith("microsoft.", StringComparison.Ordinal);
        }

        private static bool LooksLikeProduct(string name)
        {
            if (name.Length < 4) return false;
            string n = name.ToLowerInvariant();
            if (n.IndexOf("cache", StringComparison.Ordinal) >= 0) return false;
            if (n.IndexOf("crash", StringComparison.Ordinal) >= 0) return false;
            if (n.IndexOf("log", StringComparison.Ordinal) >= 0) return false;
            // 纯十六进制 / 纯字母数字的 UWP 包名片段不是产品目录
            int letters = 0;
            int digits = 0;
            for (int i = 0; i < n.Length; i++)
            {
                if (char.IsDigit(n[i])) digits++;
                else if (char.IsLetter(n[i])) letters++;
            }
            if (digits > 0 && letters > 0 && n.Length >= 12 && n.IndexOf(' ') < 0 &&
                n.IndexOf('-') < 0 && n.IndexOf('_') < 0) return false;
            return true;
        }

        /// <summary>目录中只要还有 exe 或 dll，就认为仍可能被某个程序使用，不列为残留。</summary>
        private static bool HasBinary(string dir)
        {
            int guard = 0;
            List<string> stack = new List<string>();
            stack.Add(dir);
            while (stack.Count > 0 && guard < 1200)
            {
                string cur = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                try
                {
                    string[] exes = Directory.GetFiles(cur, "*.exe");
                    if (exes.Length > 0) return true;
                    guard += exes.Length;
                    string[] dlls = Directory.GetFiles(cur, "*.dll");
                    if (dlls.Length > 0) return true;
                    guard += dlls.Length;
                    string[] subs = Directory.GetDirectories(cur);
                    for (int i = 0; i < subs.Length && stack.Count < 50; i++)
                    {
                        try
                        {
                            if ((File.GetAttributes(subs[i]) & FileAttributes.ReparsePoint) != 0) continue;
                        }
                        catch
                        {
                            continue;
                        }
                        stack.Add(subs[i]);
                    }
                }
                catch
                {
                }
            }
            return false;
        }

        // ------------------------------------------------------------ 步骤 4 计划任务

        public static void ScanTasks(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            ScanTasksCom(list, seen, canceled);
            ScanTasksFiles(list, seen, canceled);
        }

        /// <summary>通过 Schedule.Service COM 读取任务 XML（非管理员也能读到自己的任务）。</summary>
        private static void ScanTasksCom(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            object svc = Com.Create("Schedule.Service");
            if (svc == null) return;
            object root;
            try
            {
                object[] none = new object[0];
                svc.GetType().InvokeMember("Connect", BindingFlags.InvokeMethod, null, svc, none);
                root = svc.GetType().InvokeMember("GetFolder", BindingFlags.InvokeMethod, null, svc, new object[] { "\\" });
            }
            catch
            {
                return;
            }
            WalkTaskFolder(root, "", list, seen, canceled, 0);
        }

        private static void WalkTaskFolder(object folder, string prefix, List<RegItem> list,
            HashSet<string> seen, Func<bool> canceled, int depth)
        {
            if (folder == null || depth > 3) return;
            if (canceled != null && canceled()) return;
            try
            {
                object tasks = folder.GetType().InvokeMember("GetTasks",
                    BindingFlags.InvokeMethod, null, folder, new object[] { 1 });
                if (tasks is IEnumerable)
                {
                    foreach (object task in (IEnumerable)tasks)
                    {
                        if (canceled != null && canceled()) return;
                        if (list.Count >= MaxItems) return;
                        string path = Com.Str(task, "Path");
                        if (path.Length == 0) continue;
                        if (path.StartsWith(@"\Microsoft\Windows", StringComparison.OrdinalIgnoreCase)) continue;
                        string xml;
                        try { xml = Com.Str(task, "Xml"); } catch { xml = ""; }
                        AddTaskItems(xml, path, list, seen);
                    }
                }
            }
            catch
            {
            }
            try
            {
                object subs = folder.GetType().InvokeMember("GetFolders",
                    BindingFlags.InvokeMethod, null, folder, new object[] { 1 });
                if (subs is IEnumerable)
                {
                    foreach (object sub in (IEnumerable)subs)
                    {
                        if (canceled != null && canceled()) return;
                        string name = Com.Str(sub, "Name");
                        if (name.Length == 0) continue;
                        WalkTaskFolder(sub, prefix + "\\" + name, list, seen, canceled, depth + 1);
                    }
                }
            }
            catch
            {
            }
        }

        private static void ScanTasksFiles(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            string root;
            try { root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\Tasks"); }
            catch { return; }
            if (!Directory.Exists(root)) return;
            List<string> files = new List<string>();
            CollectFiles(root, files, 0, 6, 400);
            for (int i = 0; i < files.Count; i++)
            {
                if (canceled != null && canceled()) return;
                string rel = files[i].Substring(root.Length).TrimStart('\\');
                if (rel.StartsWith("Microsoft\\Windows", StringComparison.OrdinalIgnoreCase)) continue;
                string text;
                try
                {
                    if (new FileInfo(files[i]).Length > 262144) continue;
                    text = File.ReadAllText(files[i], Encoding.UTF8);
                }
                catch
                {
                    continue;
                }
                AddTaskItems(text, "\\" + rel, list, seen);
            }
        }

        private static void AddTaskItems(string xml, string rel, List<RegItem> list, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(xml)) return;
            if (list.Count >= MaxItems) return;
            Match m = TaskCommand.Match(xml);
            int guard = 0;
            while (m.Success && guard++ < 8)
            {
                string cmd = XmlDecode(m.Groups[1].Value).Trim();
                m = m.NextMatch();
                if (cmd.Length < 3) continue;
                string target = TargetFromValue(cmd);
                if (target.Length < 4) continue;
                if (IsSystemPath(target)) continue;
                if (File.Exists(target) || Directory.Exists(target)) continue;

                RegItem it = new RegItem();
                it.Hive = "";
                it.KeyPath = "";
                it.Aux = rel;
                it.Label = rel.TrimStart('\\');
                it.Reason = Loc.F("reg.reasonTask", PathUtil.Display(target));
                it.Risk = RiskLevel.Caution;
                it.Kind = RegKind.Task;
                it.Selected = false;
                it.Step = 4;
                string sig = "task|" + rel.ToLowerInvariant() + "|" + target.ToLowerInvariant();
                if (seen.Add(sig))
                {
                    list.Add(it);
                }
            }
        }

        private static void CollectFiles(string dir, List<string> outp, int depth, int maxDepth, int cap)
        {
            if (outp.Count >= cap) return;
            try
            {
                string[] files = Directory.GetFiles(dir);
                for (int i = 0; i < files.Length && outp.Count < cap; i++) outp.Add(files[i]);
                if (depth >= maxDepth) return;
                string[] subs = Directory.GetDirectories(dir);
                for (int i = 0; i < subs.Length && outp.Count < cap; i++) CollectFiles(subs[i], outp, depth + 1, maxDepth, cap);
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------ 步骤 6 防火墙

        public static void ScanFirewall(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            object policy;
            object rules;
            if (!Firewall.TryOpen(out policy, out rules)) return;
            int added = 0;
            try
            {
                foreach (object rule in (IEnumerable)rules)
                {
                    if (canceled != null && canceled()) return;
                    if (added >= MaxMiscItems) break;
                    try
                    {
                        string name = Com.Str(rule, "Name");
                        string app = Com.Str(rule, "ApplicationName");
                        if (app.Length == 0) continue;
                        string target = TargetFromValue(app);
                        if (target.Length == 0) continue;
                        if (IsSystemPath(target)) continue;
                        if (File.Exists(target)) continue;
                        int dir = Com.Int(rule, "Direction");
                        string dirText = dir == 2 ? "out" : "in";
                        RegItem it = new RegItem();
                        it.Hive = "";
                        it.KeyPath = "";
                        it.Aux = name + "\u0001" + target + "\u0001" + dirText;
                        it.Label = name.Length > 0 ? name : Path.GetFileName(target);
                        it.Reason = Loc.F("reg.reasonFirewall", PathUtil.Display(target));
                        it.Risk = RiskLevel.Caution;
                        it.Kind = RegKind.Firewall;
                        it.Selected = false;
                        it.Step = 6;
                        string sig = "fw|" + name.ToLowerInvariant() + "|" + target.ToLowerInvariant() + "|" + dirText;
                        if (seen.Add(sig))
                        {
                            list.Add(it);
                            added++;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------ 步骤 9 SOFTWARE 全量

        public static void ScanSoftwareDeep(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            ScanSoftwareHive(RegistryHive.LocalMachine, @"SOFTWARE", list, seen, canceled);
            ScanSoftwareHive(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node", list, seen, canceled);
            ScanSoftwareHive(RegistryHive.CurrentUser, @"SOFTWARE", list, seen, canceled);
        }

        private static void ScanSoftwareHive(RegistryHive hive, string rootPath, List<RegItem> list,
            HashSet<string> seen, Func<bool> canceled)
        {
            int start = Environment.TickCount;
            int visited = 0;
            int found = 0;
            List<string> stack = new List<string>();
            stack.Add(rootPath);
            while (stack.Count > 0 && found < MaxSoftwareValues)
            {
                if (canceled != null && canceled()) return;
                if (unchecked(Environment.TickCount - start) > SoftwareTimeBudgetMs) break;
                if (++visited > 140000) break;
                string path = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                RegistryKey key;
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                    using (key = baseKey.OpenSubKey(path))
                    {
                        if (key == null) continue;
                        string[] subs = null;
                        try { subs = key.GetSubKeyNames(); } catch { }
                        if (subs != null)
                        {
                            for (int i = 0; i < subs.Length; i++)
                            {
                                string child = path + "\\" + subs[i];
                                if (IsSkippedSoftwarePath(child, rootPath)) continue;
                                stack.Add(child);
                            }
                        }
                        string[] values = null;
                        try { values = key.GetValueNames(); } catch { }
                        if (values != null)
                        {
                            for (int i = 0; i < values.Length; i++)
                            {
                                string vn = values[i];
                                if (!IsPathValueName(vn)) continue;
                                object raw;
                                try { raw = key.GetValue(vn); } catch { continue; }
                                string s = raw as string;
                                if (s == null) continue;
                                string target = TargetFromValue(s);
                                if (target.Length == 0) continue;
                                if (FileExistsOrDir(target)) continue;
                                if (IsSystemPath(target)) continue;
                                string norm = PathUtil.Norm(path);
                                if (norm.IndexOf(@"\classes", StringComparison.Ordinal) >= 0) continue;
                                if (!RegGuard.AllowedSoftwareValue(path, vn)) continue;

                                RegItem it = new RegItem();
                                it.Hive = HiveName(hive);
                                it.KeyPath = path;
                                it.ValueName = vn;
                                it.Label = LastSeg(path) + " · " + vn;
                                it.Reason = Loc.F("reg.reasonDeadPath", PathUtil.Display(target));
                                it.Risk = RiskLevel.Caution;
                                it.Kind = RegKind.MainKey;
                                it.Selected = false;
                                it.Step = 9;
                                string sig = "swv|" + it.Hive + "|" + path.ToLowerInvariant() + "|" + vn.ToLowerInvariant();
                                if (seen.Add(sig))
                                {
                                    list.Add(it);
                                    found++;
                                }
                                if (found >= MaxSoftwareValues || list.Count >= MaxItems) return;
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static bool IsSkippedSoftwarePath(string path, string rootPath)
        {
            string rel = path;
            if (rel.Length > rootPath.Length) rel = rel.Substring(rootPath.Length).TrimStart('\\');
            string[] seg = rel.Split('\\');
            if (seg.Length == 0) return false;
            string first = seg[0].ToLowerInvariant();
            if (seg.Length == 1)
            {
                if (first == "microsoft" || first == "classes" || first == "policies" || first == "clients"
                    || first == "registeredapplications") return true;
                if (rootPath.Equals("SOFTWARE", StringComparison.OrdinalIgnoreCase))
                {
                    if (first == "wow6432node") return true;
                    if (first == "windows") return true;
                }
            }
            return false;
        }

        private static bool IsPathValueName(string name)
        {
            string n = name.ToLowerInvariant();
            return n == "installpath" || n == "installlocation" || n == "installdir"
                || n == "installdirectory" || n == "exepath" || n == "applicationpath"
                || n == "executablepath" || n == "servicepath" || n == "binpath"
                || n == "iconpath" || n == "iconfile" || n == "displayicon" || n == "localpackage";
        }

        // ------------------------------------------------------------ 步骤 10 Classes 全量

        public static void ScanClasses(List<RegItem> list, HashSet<string> seen, ResidualCatalog cat, Func<bool> canceled)
        {
            WalkClasses(RegistryHive.LocalMachine, @"SOFTWARE\Classes", list, seen, canceled);
            WalkClasses(RegistryHive.CurrentUser, @"SOFTWARE\Classes", list, seen, canceled);
            ScanOpenWithProgids(list, seen, canceled);
            ScanOpenWithList(list, seen, cat, canceled);
        }

        /// <summary>
        /// FileExts\&lt;扩展名&gt;\OpenWithList 里指向“已失效卸载项同名程序”的条目。
        /// 只认和失效卸载记录对得上的名字，避免把 msedge.exe / SnippingTool.exe 这类系统组件误判为残留。
        /// </summary>
        private static void ScanOpenWithList(List<RegItem> list, HashSet<string> seen, ResidualCatalog cat,
            Func<bool> canceled)
        {
            if (cat == null || cat.OrphanTokens.Count == 0) return;
            const string rootPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts";
            int added = 0;
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
                using (RegistryKey root = baseKey.OpenSubKey(rootPath))
                {
                    if (root == null) return;
                    string[] exts = root.GetSubKeyNames();
                    for (int i = 0; i < exts.Length && added < 100; i++)
                    {
                        if (canceled != null && canceled()) return;
                        string ow = rootPath + "\\" + exts[i] + "\\OpenWithList";
                        using (RegistryKey k = baseKey.OpenSubKey(ow))
                        {
                            if (k == null) continue;
                            string[] names = k.GetValueNames();
                            for (int v = 0; v < names.Length && added < 100; v++)
                            {
                                string letter = names[v];
                                if (letter.Length != 1) continue;
                                string app;
                                try
                                {
                                    object o = k.GetValue(letter);
                                    app = o == null ? "" : o.ToString().Trim();
                                }
                                catch
                                {
                                    continue;
                                }
                                if (app.Length < 3) continue;
                                if (!cat.MatchesOrphan(Path.GetFileNameWithoutExtension(app))) continue;

                                RegItem it = new RegItem();
                                it.Hive = "HKEY_CURRENT_USER";
                                it.KeyPath = ow;
                                it.ValueName = letter;
                                it.Label = exts[i] + " · " + app;
                                it.Reason = Loc.F("reg.reasonOpenWithExe", app);
                                it.Risk = RiskLevel.Caution;
                                it.Kind = RegKind.OpenWith;
                                it.Selected = false;
                                it.Step = 10;
                                string sig = "owl|" + ow.ToLowerInvariant() + "|" + letter;
                                if (seen.Add(sig))
                                {
                                    list.Add(it);
                                    added++;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void WalkClasses(RegistryHive hive, string rootPath, List<RegItem> list,
            HashSet<string> seen, Func<bool> canceled)
        {
            int start = Environment.TickCount;
            int visited = 0;
            List<string> stack = new List<string>();
            stack.Add(rootPath);
            while (stack.Count > 0)
            {
                if (canceled != null && canceled()) return;
                if (unchecked(Environment.TickCount - start) > ClassesTimeBudgetMs) break;
                if (++visited > 200000) break;
                string path = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                if (list.Count >= MaxItems) return;
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                    using (RegistryKey key = baseKey.OpenSubKey(path))
                    {
                        if (key == null) continue;
                        string name = LastSeg(path);
                        string[] seg = path.Split('\\');
                        string parent = seg.Length >= 2 ? seg[seg.Length - 2] : "";
                        string grand = seg.Length >= 3 ? seg[seg.Length - 3] : "";

                        if (name.Equals("ContextMenuHandlers", StringComparison.OrdinalIgnoreCase))
                        {
                            CheckMenuHandlers(hive, path, list, seen);
                        }
                        if (name.Equals("command", StringComparison.OrdinalIgnoreCase) &&
                            grand.Equals("shell", StringComparison.OrdinalIgnoreCase))
                        {
                            CheckVerbCommand(hive, path, parent, list, seen);
                        }
                        if ((name.Equals("InprocServer32", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("LocalServer32", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("InprocHandler32", StringComparison.OrdinalIgnoreCase)) &&
                            grand.Equals("CLSID", StringComparison.OrdinalIgnoreCase) && IsGuid(parent))
                        {
                            CheckClsidServer(hive, path, parent, list, seen);
                        }

                        if (seg.Length <= 7)
                        {
                            string[] subs = null;
                            try { subs = key.GetSubKeyNames(); } catch { }
                            if (subs != null)
                            {
                                for (int i = 0; i < subs.Length; i++) stack.Add(path + "\\" + subs[i]);
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static void CheckMenuHandlers(RegistryHive hive, string handlersPath,
            List<RegItem> list, HashSet<string> seen)
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
            using (RegistryKey key = baseKey.OpenSubKey(handlersPath))
            {
                if (key == null) return;
                string[] subs;
                try { subs = key.GetSubKeyNames(); } catch { return; }
                for (int i = 0; i < subs.Length; i++)
                {
                    string handlerPath = handlersPath + "\\" + subs[i];
                    string clsid = "";
                    try
                    {
                        using (RegistryKey hk = key.OpenSubKey(subs[i]))
                        {
                            if (hk == null) continue;
                            object v = hk.GetValue("");
                            if (v != null) clsid = v.ToString().Trim();
                        }
                    }
                    catch
                    {
                        continue;
                    }
                    if (clsid.Length == 0) clsid = subs[i];
                    clsid = clsid.Trim();
                    if (!clsid.StartsWith("{", StringComparison.Ordinal))
                    {
                        int b = clsid.IndexOf('{');
                        int e = clsid.IndexOf('}');
                        if (b >= 0 && e > b) clsid = clsid.Substring(b, e - b + 1);
                    }
                    if (!IsGuid(clsid)) continue;
                    if (ClsidUsable(clsid)) continue;
                    RegItem it = new RegItem();
                    it.Hive = HiveName(hive);
                    it.KeyPath = handlerPath;
                    it.Label = subs[i];
                    it.Reason = Loc.F("reg.reasonMenu", clsid);
                    it.Risk = RiskLevel.Caution;
                    it.Kind = RegKind.ContextMenu;
                    it.Selected = false;
                    it.Step = 10;
                    string sig = "menu|" + it.Hive + "\\" + handlerPath.ToLowerInvariant();
                    if (seen.Add(sig) && list.Count < MaxItems) list.Add(it);
                }
            }
        }

        private static void CheckVerbCommand(RegistryHive hive, string commandPath, string verb,
            List<RegItem> list, HashSet<string> seen)
        {
            string raw;
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                using (RegistryKey key = baseKey.OpenSubKey(commandPath))
                {
                    if (key == null) return;
                    object o = key.GetValue("");
                    raw = o == null ? "" : o.ToString().Trim();
                }
            }
            catch
            {
                return;
            }
            if (raw.Length == 0) return;
            string exe = Uninstaller.ExtractExe(PathUtil.Clean(raw));
            if (exe.Length == 0) return;
            if (exe.IndexOf(".exe", StringComparison.OrdinalIgnoreCase) < 0) return;
            if (File.Exists(exe)) return;
            if (!IsSoftwarePath(exe)) return;

            // command 的上两级：shell\verb\command → 删除 verb；Applications\x\shell\verb\command → 删除 Applications\x
            string verbPath = commandPath;
            int cut = verbPath.LastIndexOf('\\');
            if (cut > 0) verbPath = verbPath.Substring(0, cut);
            string deletePath = verbPath;
            string[] seg = commandPath.Split('\\');
            int appAt = -1;
            for (int i = 0; i < seg.Length; i++)
            {
                if (seg[i].Equals("Applications", StringComparison.OrdinalIgnoreCase))
                {
                    appAt = i;
                    break;
                }
            }
            if (appAt >= 0 && appAt + 1 < seg.Length)
            {
                StringBuilder b = new StringBuilder();
                for (int i = 0; i <= appAt + 1; i++)
                {
                    if (i > 0) b.Append('\\');
                    b.Append(seg[i]);
                }
                deletePath = b.ToString();
            }
            if (!RegGuard.IsClassJunkKey(deletePath)) return;

            RegItem it = new RegItem();
            it.Hive = HiveName(hive);
            it.KeyPath = deletePath;
            it.Label = verb;
            it.Reason = Loc.F("reg.reasonProgId", PathUtil.Display(exe));
            it.Risk = RiskLevel.Caution;
            it.Kind = RegKind.ProgId;
            it.Selected = false;
            it.Step = 10;
            string sig = "verb|" + it.Hive + "\\" + deletePath.ToLowerInvariant();
            if (seen.Add(sig) && list.Count < MaxItems) list.Add(it);
        }

        private static void CheckClsidServer(RegistryHive hive, string serverPath, string guid,
            List<RegItem> list, HashSet<string> seen)
        {
            string raw;
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                using (RegistryKey key = baseKey.OpenSubKey(serverPath))
                {
                    if (key == null) return;
                    object o = key.GetValue("");
                    raw = o == null ? "" : o.ToString().Trim();
                }
            }
            catch
            {
                return;
            }
            if (raw.Length == 0) return;
            if (raw.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0) return;
            string target = TargetFromValue(raw);
            if (target.Length == 0) return;
            if (FileExistsOrDir(target)) return;
            if (!IsSoftwarePath(target)) return;

            string clsidPath = serverPath.Substring(0, serverPath.LastIndexOf('\\'));
            RegItem it = new RegItem();
            it.Hive = HiveName(hive);
            it.KeyPath = clsidPath;
            it.Label = guid;
            it.Reason = Loc.F("reg.reasonCom", PathUtil.Display(target));
            it.Risk = RiskLevel.Caution;
            it.Kind = RegKind.ComClass;
            it.Selected = false;
            it.Step = 10;
            string sig = "clsid|" + it.Hive + "\\" + clsidPath.ToLowerInvariant();
            if (seen.Add(sig) && list.Count < MaxItems) list.Add(it);
        }

        private static bool ClsidUsable(string guid)
        {
            try
            {
                using (RegistryKey k = Registry.ClassesRoot.OpenSubKey(@"CLSID\" + guid))
                {
                    if (k == null) return false;
                    string[] servers = { "InprocServer32", "LocalServer32", "InprocHandler32", "InprocHandler" };
                    for (int i = 0; i < servers.Length; i++)
                    {
                        using (RegistryKey s = k.OpenSubKey(servers[i]))
                        {
                            if (s == null) continue;
                            object o = s.GetValue("");
                            if (o == null) return true;
                            string target = TargetFromValue(o.ToString());
                            if (target.Length == 0) return true;
                            if (FileExistsOrDir(target)) return true;
                        }
                    }
                    return false;
                }
            }
            catch
            {
                return true;
            }
        }

        private static void ScanOpenWithProgids(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            const string rootPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts";
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
                using (RegistryKey root = baseKey.OpenSubKey(rootPath))
                {
                    if (root == null) return;
                    string[] exts = root.GetSubKeyNames();
                    for (int i = 0; i < exts.Length && list.Count < MaxItems; i++)
                    {
                        if (canceled != null && canceled()) return;
                        string owp = rootPath + "\\" + exts[i] + "\\OpenWithProgids";
                        using (RegistryKey k = baseKey.OpenSubKey(owp))
                        {
                            if (k == null) continue;
                            string[] names = k.GetValueNames();
                            for (int v = 0; v < names.Length; v++)
                            {
                                string prog = names[v];
                                if (prog.Length == 0) continue;
                                if (prog.IndexOf('.') < 0 || prog.IndexOf(' ') >= 0) continue;
                                if (prog.StartsWith("AppX", StringComparison.OrdinalIgnoreCase)) continue;
                                if (ClassExists(prog)) continue;
                                RegItem it = new RegItem();
                                it.Hive = "HKEY_CURRENT_USER";
                                it.KeyPath = owp;
                                it.ValueName = prog;
                                it.Label = exts[i] + " · " + prog;
                                it.Reason = Loc.F("reg.reasonOpenWith", prog);
                                it.Risk = RiskLevel.Safe;
                                it.Kind = RegKind.OpenWith;
                                it.Selected = true;
                                it.Step = 10;
                                string sig = "owp|" + owp.ToLowerInvariant() + "|" + prog.ToLowerInvariant();
                                if (seen.Add(sig)) list.Add(it);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static bool ClassExists(string progId)
        {
            try
            {
                using (RegistryKey k = Registry.ClassesRoot.OpenSubKey(progId)) return k != null;
            }
            catch
            {
                return true;
            }
        }

        // ------------------------------------------------------------ 步骤 11 Installer

        public static void ScanInstaller(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            const string rootPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData";
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                using (RegistryKey root = baseKey.OpenSubKey(rootPath))
                {
                    if (root == null) return;
                    string[] sids = root.GetSubKeyNames();
                    int added = 0;
                    for (int s = 0; s < sids.Length && added < 100; s++)
                    {
                        string products = rootPath + "\\" + sids[s] + "\\Products";
                        using (RegistryKey pk = baseKey.OpenSubKey(products))
                        {
                            if (pk == null) continue;
                            string[] guids = pk.GetSubKeyNames();
                            for (int g = 0; g < guids.Length && added < 100; g++)
                            {
                                if (canceled != null && canceled()) return;
                                string propsPath = products + "\\" + guids[g] + "\\InstallProperties";
                                using (RegistryKey ip = baseKey.OpenSubKey(propsPath))
                                {
                                    if (ip == null) continue;
                                    string display = Str(ip, "DisplayName");
                                    if (display.Length == 0) continue;
                                    if (IsSystemProduct(display)) continue;
                                    string loc = Str(ip, "InstallLocation");
                                    string pkg = Str(ip, "LocalPackage");
                                    string dead = "";
                                    if (loc.Length > 0)
                                    {
                                        string t = TargetFromValue(loc);
                                        if (t.Length > 0 && !FileExistsOrDir(t)) dead = t;
                                    }
                                    if (dead.Length == 0 && pkg.Length > 0)
                                    {
                                        string t = TargetFromValue(pkg);
                                        if (t.Length > 0 && File.Exists(t) == false && IsInstallerCachePath(t)) dead = t;
                                    }
                                    if (dead.Length == 0) continue;
                                    if (!RegGuard.IsInstallerKey(propsPath)) continue;
                                    RegItem it = new RegItem();
                                    it.Hive = "HKEY_LOCAL_MACHINE";
                                    it.KeyPath = propsPath;
                                    it.Label = display;
                                    it.Reason = Loc.F("reg.reasonInstaller", PathUtil.Display(dead));
                                    it.Risk = RiskLevel.Caution;
                                    it.Kind = RegKind.Installer;
                                    it.Selected = false;
                                    it.Step = 11;
                                    string sig = "inst|" + propsPath.ToLowerInvariant();
                                    if (seen.Add(sig))
                                    {
                                        list.Add(it);
                                        added++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static bool IsSystemProduct(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.IndexOf("microsoft visual c++", StringComparison.Ordinal) >= 0) return true;
            if (n.IndexOf("microsoft .net", StringComparison.Ordinal) >= 0) return true;
            if (n.IndexOf("windows sdk", StringComparison.Ordinal) >= 0) return true;
            if (n.IndexOf("update", StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static bool IsInstallerCachePath(string p)
        {
            return PathUtil.Norm(p).StartsWith(@"c:\windows\installer", StringComparison.Ordinal);
        }

        // ------------------------------------------------------------ 步骤 12 驱动与设备

        public static void ScanDrivers(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            ScanClassFilters(list, seen, canceled);
            ScanDriverPackages(list, seen, canceled);
            ScanHiddenDevices(list, seen, canceled);
        }

        private static void ScanClassFilters(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            const string rootPath = @"SYSTEM\CurrentControlSet\Control\Class";
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                using (RegistryKey root = baseKey.OpenSubKey(rootPath))
                {
                    if (root == null) return;
                    string[] guids = root.GetSubKeyNames();
                    int added = 0;
                    for (int i = 0; i < guids.Length && added < 100; i++)
                    {
                        if (canceled != null && canceled()) return;
                        if (!IsGuid(guids[i])) continue;
                        using (RegistryKey k = root.OpenSubKey(guids[i]))
                        {
                            if (k == null) continue;
                            for (int f = 0; f < 2; f++)
                            {
                                string valueName = f == 0 ? "UpperFilters" : "LowerFilters";
                                object o;
                                try { o = k.GetValue(valueName); } catch { continue; }
                                string[] arr = o as string[];
                                if (arr == null)
                                {
                                    string s = o as string;
                                    if (s != null) arr = new string[] { s };
                                }
                                if (arr == null) continue;
                                for (int e = 0; e < arr.Length; e++)
                                {
                                    string filter = arr[e] == null ? "" : arr[e].Trim();
                                    if (filter.Length < 3) continue;
                                    string bad = FilterProblem(filter);
                                    if (bad.Length == 0) continue;
                                    string keyPath = rootPath + "\\" + guids[i];
                                    if (!RegGuard.AllowedJunkPath(keyPath, false, valueName)) continue;
                                    RegItem it = new RegItem();
                                    it.Hive = "HKEY_LOCAL_MACHINE";
                                    it.KeyPath = keyPath;
                                    it.ValueName = valueName;
                                    it.Aux = filter;
                                    it.Label = filter;
                                    it.Reason = Loc.F("reg.reasonDriverFilter", PathUtil.Display(bad));
                                    it.Risk = RiskLevel.Caution;
                                    it.Kind = RegKind.Driver;
                                    it.Selected = false;
                                    it.NeedsReboot = true;
                                    it.Step = 12;
                                    string sig = "flt|" + keyPath.ToLowerInvariant() + "|" + valueName + "|" + filter.ToLowerInvariant();
                                    if (seen.Add(sig))
                                    {
                                        list.Add(it);
                                        added++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static string FilterProblem(string filter)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                using (RegistryKey svc = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + filter))
                {
                    if (svc == null) return "服务 " + filter + " 不存在";
                    object o = svc.GetValue("ImagePath");
                    if (o == null) return "";
                    string img = Environment.ExpandEnvironmentVariables(o.ToString().Trim());
                    string file = Uninstaller.ExtractExe(PathUtil.Clean(img));
                    if (file.Length == 0) return "";
                    if (File.Exists(file)) return "";
                    if (!PathUtil.Norm(file).StartsWith(@"c:\", StringComparison.Ordinal)) return "";
                    return file;
                }
            }
            catch
            {
                return "";
            }
        }

        private static void ScanDriverPackages(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            string inf;
            try { inf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF"); }
            catch { return; }
            if (!Directory.Exists(inf)) return;
            HashSet<string> referenced = CollectInfReferences(canceled);
            LastInfReferenceCount = referenced.Count;
            string[] files;
            try { files = Directory.GetFiles(inf, "oem*.inf"); }
            catch { return; }
            int added = 0;
            for (int i = 0; i < files.Length && added < 100; i++)
            {
                if (canceled != null && canceled()) return;
                string name = Path.GetFileName(files[i]);
                if (referenced.Contains(name)) continue;
                RegItem it = new RegItem();
                it.Hive = "";
                it.KeyPath = "";
                it.FilePath = files[i];
                it.Aux = name;
                it.Label = name;
                it.Reason = Loc.F("reg.reasonDriverPkg", name);
                it.Risk = RiskLevel.Caution;
                it.Kind = RegKind.Driver;
                it.Selected = false;
                it.NeedsReboot = true;
                it.Step = 12;
                string sig = "drv|" + name.ToLowerInvariant();
                if (seen.Add(sig))
                {
                    list.Add(it);
                    added++;
                }
            }
        }

        private static HashSet<string> CollectInfReferences(Func<bool> canceled)
        {
            HashSet<string> refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectInfFrom(@"SYSTEM\CurrentControlSet\Control\Class", refs, canceled);
            CollectInfFrom(@"SYSTEM\CurrentControlSet\Services", refs, canceled);
            return refs;
        }

        private static void CollectInfFrom(string rootPath, HashSet<string> refs, Func<bool> canceled)
        {
            int start = Environment.TickCount;
            int visited = 0;
            List<string> stack = new List<string>();
            stack.Add(rootPath);
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                {
                    while (stack.Count > 0)
                    {
                        if (canceled != null && canceled()) return;
                        if (unchecked(Environment.TickCount - start) > 6000) return;
                        if (++visited > 30000) return;
                        string path = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        try
                        {
                            using (RegistryKey key = baseKey.OpenSubKey(path))
                            {
                                if (key == null) continue;
                                object o;
                                try { o = key.GetValue("InfPath"); } catch { o = null; }
                                if (o != null)
                                {
                                    string s = o.ToString().Trim();
                                    if (s.Length > 0) refs.Add(Path.GetFileName(s));
                                }
                                string[] subs;
                                try { subs = key.GetSubKeyNames(); } catch { subs = null; }
                                if (subs == null) continue;
                                if (path.Split('\\').Length > 8) continue;
                                for (int i = 0; i < subs.Length; i++) stack.Add(path + "\\" + subs[i]);
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

        private static void ScanHiddenDevices(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            const string rootPath = @"SYSTEM\CurrentControlSet\Enum";
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                using (RegistryKey root = baseKey.OpenSubKey(rootPath))
                {
                    if (root == null) return;
                    string[] buses = root.GetSubKeyNames();
                    int added = 0;
                    for (int b = 0; b < buses.Length && added < 100; b++)
                    {
                        if (canceled != null && canceled()) return;
                        string bus = buses[b].ToLowerInvariant();
                        if (bus == "root" || bus == "htree") continue;
                        string busPath = rootPath + "\\" + buses[b];
                        using (RegistryKey bk = baseKey.OpenSubKey(busPath))
                        {
                            if (bk == null) continue;
                            string[] insts;
                            try { insts = bk.GetSubKeyNames(); } catch { continue; }
                            for (int i = 0; i < insts.Length && added < 100; i++)
                            {
                                string instPath = busPath + "\\" + insts[i];
                                using (RegistryKey ik = baseKey.OpenSubKey(instPath))
                                {
                                    if (ik == null) continue;
                                    string svc = Str(ik, "Service");
                                    if (svc.Length == 0) continue;
                                    if (ServiceExists(svc)) continue;
                                    if (!RegGuard.IsEnumKey(instPath)) continue;
                                    RegItem it = new RegItem();
                                    it.Hive = "HKEY_LOCAL_MACHINE";
                                    it.KeyPath = instPath;
                                    it.Label = insts[i];
                                    it.Reason = Loc.F("reg.reasonDevice", svc);
                                    it.Risk = RiskLevel.Caution;
                                    it.Kind = RegKind.Device;
                                    it.Selected = false;
                                    it.NeedsReboot = true;
                                    it.Step = 12;
                                    string sig = "dev|" + instPath.ToLowerInvariant();
                                    if (seen.Add(sig))
                                    {
                                        list.Add(it);
                                        added++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static bool ServiceExists(string name)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                using (RegistryKey k = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name))
                {
                    return k != null;
                }
            }
            catch
            {
                return true;
            }
        }

        // ------------------------------------------------------------ 步骤 13 浏览器

        public static void ScanBrowsers(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            List<string> roots = new List<string>();
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            AddIfExists(roots, Path.Combine(local, @"Google\Chrome\User Data"));
            AddIfExists(roots, Path.Combine(local, @"Microsoft\Edge\User Data"));
            AddIfExists(roots, Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data"));
            AddIfExists(roots, Path.Combine(local, @"Chromium\User Data"));
            AddIfExists(roots, Path.Combine(local, @"Vivaldi\User Data"));

            int added = 0;
            Dictionary<string, string> prefCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int r = 0; r < roots.Count && added < 200; r++)
            {
                string[] profiles;
                try { profiles = Directory.GetDirectories(roots[r]); }
                catch { continue; }
                for (int p = 0; p < profiles.Length && added < 200; p++)
                {
                    if (canceled != null && canceled()) return;
                    string pname = Path.GetFileName(profiles[p]);
                    if (!pname.Equals("Default", StringComparison.OrdinalIgnoreCase) &&
                        !pname.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)) continue;
                    string prefText = ReadPrefs(profiles[p], prefCache);

                    string[] storageRoots =
                    {
                        "Local Extension Settings", "Sync Extension Settings", "Extension State", @"Storage\ext"
                    };
                    for (int s = 0; s < storageRoots.Length; s++)
                    {
                        string store = Path.Combine(profiles[p], storageRoots[s]);
                        string[] ids;
                        try { ids = Directory.GetDirectories(store); }
                        catch { continue; }
                        for (int i = 0; i < ids.Length && added < 200; i++)
                        {
                            string id = Path.GetFileName(ids[i]);
                            if (id.Length < 8) continue;
                            if (RegisteredInPrefs(prefText, id)) continue;
                            if (HasExtension(profiles[p], id)) continue;
                            if (!AddBrowserItem(list, seen, ids[i], id, storageRoots[s])) continue;
                            added++;
                        }
                    }

                    string extRoot = Path.Combine(profiles[p], "Extensions");
                    string[] extIds;
                    try { extIds = Directory.GetDirectories(extRoot); }
                    catch { continue; }
                    for (int i = 0; i < extIds.Length && added < 200; i++)
                    {
                        string id = Path.GetFileName(extIds[i]);
                        if (id.Length < 8) continue;
                        if (RegisteredInPrefs(prefText, id)) continue;
                        if (HasManifest(extIds[i])) continue;
                        if (!AddBrowserItem(list, seen, extIds[i], id, "Extensions")) continue;
                        added++;
                    }

                    // IndexedDB 里按扩展来源命名的数据库目录
                    string idbRoot = Path.Combine(profiles[p], "IndexedDB");
                    string[] idbDirs;
                    try { idbDirs = Directory.GetDirectories(idbRoot); }
                    catch { idbDirs = null; }
                    if (idbDirs != null)
                    {
                        for (int i = 0; i < idbDirs.Length && added < 200; i++)
                        {
                            string dname = Path.GetFileName(idbDirs[i]);
                            if (!dname.StartsWith("chrome-extension_", StringComparison.OrdinalIgnoreCase)) continue;
                            if (!dname.EndsWith(".indexeddb.leveldb", StringComparison.OrdinalIgnoreCase)) continue;
                            string id = dname.Substring("chrome-extension_".Length,
                                dname.Length - "chrome-extension_".Length - ".indexeddb.leveldb".Length);
                            if (id.Length < 8) continue;
                            if (id.EndsWith("_0", StringComparison.Ordinal)) id = id.Substring(0, id.Length - 2);
                            if (RegisteredInPrefs(prefText, id)) continue;
                            if (HasExtension(profiles[p], id)) continue;
                            if (!AddBrowserItem(list, seen, idbDirs[i], id, "IndexedDB")) continue;
                            added++;
                        }
                    }
                }
            }
        }

        /// <summary>扩展仍在 Preferences / Secure Preferences 里注册（含 Edge 内置组件扩展）时不当作残留。</summary>
        private static string ReadPrefs(string profile, Dictionary<string, string> cache)
        {
            string text;
            if (cache.TryGetValue(profile, out text)) return text;
            StringBuilder b = new StringBuilder();
            string[] names = { "Preferences", "Secure Preferences" };
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    string p = Path.Combine(profile, names[i]);
                    if (!File.Exists(p)) continue;
                    FileInfo fi = new FileInfo(p);
                    if (fi.Length > 32L * 1024 * 1024) continue;
                    b.Append(File.ReadAllText(p, Encoding.UTF8));
                }
                catch
                {
                }
            }
            text = b.ToString();
            cache[profile] = text;
            return text;
        }

        private static bool RegisteredInPrefs(string prefText, string id)
        {
            if (prefText.Length == 0 || id.Length < 8) return false;
            return prefText.IndexOf("\"" + id + "\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool AddBrowserItem(List<RegItem> list, HashSet<string> seen, string dir, string id, string area)
        {
            if (list.Count >= MaxItems) return false;
            RegItem it = new RegItem();
            it.Hive = "";
            it.KeyPath = "";
            it.FilePath = dir;
            it.Aux = area;
            it.Label = id;
            it.Reason = Loc.F("reg.reasonBrowser", id);
            it.Risk = RiskLevel.Caution;
            it.Kind = RegKind.BrowserExt;
            it.Selected = false;
            it.Step = 13;
            string sig = "ext|" + dir.ToLowerInvariant();
            if (!seen.Add(sig)) return false;
            list.Add(it);
            return true;
        }

        private static bool HasExtension(string profile, string id)
        {
            try
            {
                string dir = Path.Combine(profile, "Extensions", id);
                if (!Directory.Exists(dir)) return false;
                return HasManifest(dir);
            }
            catch
            {
                return true;
            }
        }

        private static bool HasManifest(string extDir)
        {
            try
            {
                string[] versions = Directory.GetDirectories(extDir);
                for (int i = 0; i < versions.Length; i++)
                {
                    if (File.Exists(Path.Combine(versions[i], "manifest.json"))) return true;
                }
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static void AddIfExists(List<string> list, string path)
        {
            try
            {
                if (Directory.Exists(path)) list.Add(path);
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------ 步骤 14 应用列表

        public static void ScanRegisteredApps(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            RegistryHive[] hives = { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
            for (int h = 0; h < hives.Length; h++)
            {
                const string path = @"SOFTWARE\RegisteredApplications";
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hives[h], RegistryView.Default))
                    using (RegistryKey root = baseKey.OpenSubKey(path))
                    {
                        if (root == null) continue;
                        string[] names = root.GetValueNames();
                        for (int i = 0; i < names.Length && list.Count < MaxItems; i++)
                        {
                            if (canceled != null && canceled()) return;
                            string target;
                            try
                            {
                                object o = root.GetValue(names[i]);
                                target = o == null ? "" : o.ToString().Trim();
                            }
                            catch
                            {
                                continue;
                            }
                            if (target.Length < 6) continue;
                            if (names[i].StartsWith("AppX", StringComparison.OrdinalIgnoreCase)) continue;
                            if (target.IndexOf(@"Classes\Local Settings", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                            if (SoftwareKeyExists(target)) continue;
                            RegItem it = new RegItem();
                            it.Hive = HiveName(hives[h]);
                            it.KeyPath = path;
                            it.ValueName = names[i];
                            it.Label = names[i];
                            it.Reason = Loc.F("reg.reasonRegisteredApp", PathUtil.Display(target));
                            it.Risk = RiskLevel.Caution;
                            it.Kind = RegKind.OpenWith;
                            it.Selected = false;
                            it.Step = 14;
                            string sig = "rapp|" + it.Hive + "|" + names[i].ToLowerInvariant();
                            if (seen.Add(sig)) list.Add(it);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static bool SoftwareKeyExists(string subPath)
        {
            string p = subPath == null ? "" : subPath.Trim();
            if (p.Length == 0) return false;
            if (p.StartsWith("Software\\", StringComparison.OrdinalIgnoreCase))
                p = p.Substring("Software\\".Length);
            RegistryHive[] hives = { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
            for (int h = 0; h < hives.Length; h++)
            {
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hives[h], RegistryView.Default))
                    using (RegistryKey k = baseKey.OpenSubKey(@"SOFTWARE\" + p))
                    {
                        if (k != null) return true;
                    }
                }
                catch
                {
                }
            }
            try
            {
                using (RegistryKey k = Registry.ClassesRoot.OpenSubKey(p))
                {
                    if (k != null) return true;
                }
            }
            catch
            {
            }
            return false;
        }

        // ------------------------------------------------------------ 步骤 15 事件日志与临时缓存

        public static void ScanEventLog(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            string[] logs = { "Application", "System" };
            int added = 0;
            for (int l = 0; l < logs.Length && added < 100; l++)
            {
                const string pre = @"SYSTEM\CurrentControlSet\Services\EventLog\";
                string rootPath = pre + logs[l];
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                    using (RegistryKey root = baseKey.OpenSubKey(rootPath))
                    {
                        if (root == null) continue;
                        string[] sources = root.GetSubKeyNames();
                        for (int i = 0; i < sources.Length && added < 100; i++)
                        {
                            if (canceled != null && canceled()) return;
                            string sourcePath = rootPath + "\\" + sources[i];
                            using (RegistryKey sk = root.OpenSubKey(sources[i]))
                            {
                                if (sk == null) continue;
                                string bad = MessageFileProblem(sk);
                                if (bad.Length == 0) continue;
                                if (!RegGuard.IsEventLogKey(sourcePath)) continue;
                                RegItem it = new RegItem();
                                it.Hive = "HKEY_LOCAL_MACHINE";
                                it.KeyPath = sourcePath;
                                it.Label = sources[i];
                                it.Reason = Loc.F("reg.reasonEventLog", PathUtil.Display(bad));
                                it.Risk = RiskLevel.Caution;
                                it.Kind = RegKind.EventLog;
                                it.Selected = false;
                                it.NeedsReboot = true;
                                it.Step = 15;
                                string sig = "evt|" + sourcePath.ToLowerInvariant();
                                if (seen.Add(sig))
                                {
                                    list.Add(it);
                                    added++;
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static string MessageFileProblem(RegistryKey sk)
        {
            string[] names = { "EventMessageFile", "CategoryMessageFile", "ParameterMessageFile" };
            for (int n = 0; n < names.Length; n++)
            {
                string raw = Str(sk, names[n]);
                if (raw.Length == 0) continue;
                string[] parts = raw.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    string p = TargetFromValue(parts[i]);
                    if (p.Length == 0) continue;
                    if (FileExistsOrDir(p)) continue;
                    if (!IsSoftwarePath(p)) continue;
                    return p;
                }
            }
            return "";
        }

        public static void ScanTempAndUpdate(List<RegItem> list, HashSet<string> seen, Func<bool> canceled)
        {
            string temp;
            try { temp = Path.GetTempPath(); }
            catch { temp = ""; }
            if (temp.Length > 0 && Directory.Exists(temp))
            {
                List<KeyValuePair<long, string>> entries = new List<KeyValuePair<long, string>>();
                string[] items;
                try { items = Directory.GetFileSystemEntries(temp); }
                catch { items = new string[0]; }
                for (int i = 0; i < items.Length; i++)
                {
                    if (canceled != null && canceled()) return;
                    try
                    {
                        DateTime t = Directory.Exists(items[i]) ? Directory.GetLastWriteTime(items[i]) : File.GetLastWriteTime(items[i]);
                        if (unchecked((DateTime.Now - t).TotalDays) < 7) continue;
                        long size = 0;
                        if (File.Exists(items[i]))
                        {
                            size = new FileInfo(items[i]).Length;
                            if (size < 65536) continue;
                        }
                        else
                        {
                            size = DirSize(items[i], 800);
                        }
                        entries.Add(new KeyValuePair<long, string>(size, items[i]));
                    }
                    catch
                    {
                    }
                }
                entries.Sort(delegate(KeyValuePair<long, string> a, KeyValuePair<long, string> b)
                {
                    return b.Key.CompareTo(a.Key);
                });
                for (int i = 0; i < entries.Count && i < 60 && list.Count < MaxItems; i++)
                {
                    string path = entries[i].Value;
                    RegItem it = new RegItem();
                    it.Hive = "";
                    it.KeyPath = "";
                    it.FilePath = path;
                    it.Label = Path.GetFileName(path);
                    it.Reason = Loc.F("reg.reasonTemp", PathUtil.Display(path));
                    it.Risk = RiskLevel.Safe;
                    it.Kind = Directory.Exists(path) ? RegKind.Folder : RegKind.TempFile;
                    it.Selected = true;
                    it.Step = 15;
                    string sig = "tmp|" + path.ToLowerInvariant();
                    if (seen.Add(sig)) list.Add(it);
                }
            }

            AddUpdateCache(list, seen, Environment.GetFolderPath(Environment.SpecialFolder.Windows) +
                @"\SoftwareDistribution\Download");
            try
            {
                AddUpdateCache(list, seen, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache"));
            }
            catch
            {
            }
        }

        private static void AddUpdateCache(List<RegItem> list, HashSet<string> seen, string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            if (list.Count >= MaxItems) return;
            RegItem it = new RegItem();
            it.Hive = "";
            it.KeyPath = "";
            it.FilePath = dir;
            it.Label = Path.GetFileName(dir);
            it.Reason = Loc.F("reg.reasonWU", PathUtil.Display(dir));
            it.Risk = RiskLevel.Safe;
            it.Kind = RegKind.Folder;
            it.Selected = false;
            it.Step = 15;
            string sig = "wu|" + dir.ToLowerInvariant();
            if (seen.Add(sig)) list.Add(it);
        }

        private static long DirSize(string dir, int cap)
        {
            long total = 0;
            int guard = 0;
            List<string> stack = new List<string>();
            stack.Add(dir);
            while (stack.Count > 0 && guard < cap)
            {
                string cur = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                try
                {
                    string[] files = Directory.GetFiles(cur);
                    for (int i = 0; i < files.Length && guard < cap; i++)
                    {
                        guard++;
                        try { total += new FileInfo(files[i]).Length; } catch { }
                    }
                    string[] subs = Directory.GetDirectories(cur);
                    for (int i = 0; i < subs.Length; i++) stack.Add(subs[i]);
                }
                catch
                {
                }
            }
            return total;
        }

        // ------------------------------------------------------------ 通用工具

        public static string TargetFromValue(string raw)
        {
            string p = raw == null ? "" : raw.Trim();
            if (p.Length == 0) return "";
            if (p.StartsWith("%", StringComparison.Ordinal) || p.IndexOf('%') >= 0)
            {
                p = Environment.ExpandEnvironmentVariables(p);
                if (p.IndexOf('%') >= 0) return "";
            }
            if (p.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("https:", StringComparison.OrdinalIgnoreCase)) return "";
            if (p.StartsWith("\\\\", StringComparison.Ordinal)) return "";
            bool quoted = p.StartsWith("\"", StringComparison.Ordinal);
            if (quoted)
            {
                int end = p.IndexOf('"', 1);
                p = end > 1 ? p.Substring(1, end - 1) : p.Trim('"');
            }
            int comma = p.LastIndexOf(',');
            if (comma > 3 && comma == p.Length - 2 && char.IsDigit(p[p.Length - 1])) p = p.Substring(0, comma);

            if (!quoted)
            {
                int exe = p.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (exe > 0) p = p.Substring(0, exe + 4);
                else
                {
                    int dash = p.IndexOf(" -", StringComparison.Ordinal);
                    if (dash > 6) p = p.Substring(0, dash);
                    int slash = p.IndexOf(" /", StringComparison.Ordinal);
                    if (slash > 6 && (dash < 0 || slash < dash)) p = p.Substring(0, slash);
                }
            }
            p = PathUtil.Clean(p);
            if (p.Length < 4) return "";
            if (p.IndexOf("\\", StringComparison.Ordinal) < 0) return "";
            if (p.Length > 1 && p[1] != ':') return "";
            return p;
        }

        private static bool FileExistsOrDir(string p)
        {
            try { return File.Exists(p) || Directory.Exists(p); }
            catch { return true; }
        }

        public static bool IsSystemPath(string p)
        {
            string n = PathUtil.Norm(p);
            for (int i = 0; i < SystemPathPrefix.Length; i++)
            {
                if (n.StartsWith(SystemPathPrefix[i], StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static bool IsSoftwarePath(string p)
        {
            string n = PathUtil.Norm(p);
            if (n.Length < 6) return false;
            if (IsSystemPath(p)) return false;
            if (n.StartsWith(@"c:\", StringComparison.Ordinal) ||
                (n.Length > 1 && n[1] == ':')) return true;
            return false;
        }

        private static bool IsGuid(string s)
        {
            if (s.Length != 38 || s[0] != '{' || s[37] != '}') return false;
            for (int i = 1; i < 37; i++)
            {
                char c = s[i];
                if (i == 9 || i == 14 || i == 19 || i == 24)
                {
                    if (c != '-') return false;
                }
                else if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    return false;
                }
            }
            return true;
        }

        private static string LastSeg(string path)
        {
            int cut = path.LastIndexOf('\\');
            return cut >= 0 ? path.Substring(cut + 1) : path;
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

        private static string XmlDecode(string s)
        {
            if (s == null) return "";
            return s.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
                .Replace("&quot;", "\"").Replace("&apos;", "'");
        }
    }

    /// <summary>COM 反射与防火墙对象访问。</summary>
    internal static class Com
    {
        public static object Create(string progId)
        {
            try
            {
                Type t = Type.GetTypeFromProgID(progId);
                if (t == null) return null;
                return Activator.CreateInstance(t);
            }
            catch
            {
                return null;
            }
        }

        public static object Get(object o, string name)
        {
            if (o == null) return null;
            try
            {
                return o.GetType().InvokeMember(name, BindingFlags.GetProperty, null, o, null);
            }
            catch
            {
                return null;
            }
        }

        public static string Str(object o, string name)
        {
            object v = Get(o, name);
            return v == null ? "" : v.ToString().Trim();
        }

        public static int Int(object o, string name)
        {
            object v = Get(o, name);
            if (v == null) return 0;
            try { return Convert.ToInt32(v); } catch { return 0; }
        }

        public static bool Call(object o, string name, object[] args)
        {
            if (o == null) return false;
            try
            {
                o.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, o, args);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class Firewall
    {
        public static bool TryOpen(out object policy, out object rules)
        {
            policy = null;
            rules = null;
            try
            {
                policy = Com.Create("HNetCfg.FwPolicy2");
                if (policy == null) return false;
                rules = Com.Get(policy, "Rules");
                if (rules == null || !(rules is IEnumerable)) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Aux = 规则名 \u0001 程序路径 \u0001 in/out。</summary>
        public static bool Remove(string aux, StreamWriter log)
        {
            string[] parts = (aux == null ? "" : aux).Split('\u0001');
            if (parts.Length < 3) return false;
            string name = parts[0];
            string app = parts[1];
            string dir = parts[2];
            object policy;
            object rules;
            if (!TryOpen(out policy, out rules)) return false;
            bool removed = false;
            try
            {
                foreach (object rule in (IEnumerable)rules)
                {
                    try
                    {
                        string n = Com.Str(rule, "Name");
                        string a = PathUtil.Clean(Com.Str(rule, "ApplicationName"));
                        int d = Com.Int(rule, "Direction");
                        string dt = d == 2 ? "out" : "in";
                        if (!string.Equals(n, name, StringComparison.Ordinal)) continue;
                        if (!string.Equals(a, app, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!string.Equals(dt, dir, StringComparison.OrdinalIgnoreCase)) continue;
                        if (Com.Call(rules, "Remove", new object[] { rule }))
                        {
                            removed = true;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            if (log != null) log.WriteLine((removed ? "FW-REMOVED " : "FW-FAIL ") + name + " " + app);
            return removed;
        }

        public static bool Exists(string aux)
        {
            string[] parts = (aux == null ? "" : aux).Split('\u0001');
            if (parts.Length < 3) return true;
            object policy;
            object rules;
            if (!TryOpen(out policy, out rules)) return true;
            try
            {
                foreach (object rule in (IEnumerable)rules)
                {
                    try
                    {
                        string n = Com.Str(rule, "Name");
                        string a = PathUtil.Clean(Com.Str(rule, "ApplicationName"));
                        int d = Com.Int(rule, "Direction");
                        string dt = d == 2 ? "out" : "in";
                        if (string.Equals(n, parts[0], StringComparison.Ordinal) &&
                            string.Equals(a, parts[1], StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(dt, parts[2], StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            return false;
        }
    }

    /// <summary>特殊残留项的实际执行动作：防火墙、筛选器、驱动包与存在性回查。</summary>
    public static class ResidualActions
    {
        /// <summary>从 UpperFilters / LowerFilters 中移除一个失效项，删除前先导出备份。</summary>
        public static bool RemoveFilter(string hive, string keyPath, string valueName, string filter,
            string backupDir, StreamWriter log)
        {
            try
            {
                if (!string.IsNullOrEmpty(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                    string safe = keyPath.Replace('\\', '_');
                    if (safe.Length > 80) safe = safe.Substring(safe.Length - 80);
                    string o;
                    Cmd.Run(Cmd.SysExe("reg.exe"), "export \"HKLM\\" + keyPath + "\" \"" +
                        Path.Combine(backupDir, safe + ".reg") + "\" /y", log, out o, 20000);
                }
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                using (RegistryKey k = baseKey.OpenSubKey(keyPath, true))
                {
                    if (k == null) return false;
                    object raw = k.GetValue(valueName);
                    string[] arr = raw as string[];
                    if (arr == null)
                    {
                        string s = raw as string;
                        if (s == null) return false;
                        arr = new string[] { s };
                    }
                    List<string> keep = new List<string>();
                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (arr[i] != null && arr[i].Trim().Length > 0 &&
                            !string.Equals(arr[i].Trim(), filter, StringComparison.OrdinalIgnoreCase)) keep.Add(arr[i].Trim());
                    }
                    if (keep.Count == arr.Length) return false;
                    if (keep.Count == 0) k.DeleteValue(valueName, false);
                    else k.SetValue(valueName, keep.ToArray(), RegistryValueKind.MultiString);
                }
                if (log != null) log.WriteLine("FILTER-REMOVED " + valueName + " -> " + filter);
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) log.WriteLine("FILTER-FAIL " + filter + " -> " + ex.Message);
                return false;
            }
        }

        public static bool DeleteDriverPackage(string infName, StreamWriter log)
        {
            string o;
            int code = Cmd.Run(Cmd.SysExe("pnputil.exe"),
                "/delete-driver " + infName + " /uninstall /force", log, out o, 60000);
            return code == 0;
        }

        /// <summary>从 DriverDatabase 注册表读取 oem*.inf 对应的 DriverStore\FileRepository 目录。</summary>
        public static string DriverStoreFolder(string infName)
        {
            if (string.IsNullOrEmpty(infName)) return "";
            if (infName.IndexOf('\\') >= 0 || infName.IndexOf('/') >= 0 || infName.IndexOf("..", StringComparison.Ordinal) >= 0)
                return "";
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                using (RegistryKey k = baseKey.OpenSubKey(@"SYSTEM\DriverDatabase\DriverInfFiles\" + infName))
                {
                    if (k == null) return "";
                    object o = k.GetValue("");
                    string folder;
                    string[] arr = o as string[];
                    if (arr != null) folder = arr.Length > 0 ? arr[0].Trim() : "";
                    else folder = o == null ? "" : o.ToString().Trim();
                    if (folder.Length == 0) return "";
                    if (folder.IndexOf('\\') >= 0 || folder.IndexOf('/') >= 0 || folder.IndexOf("..", StringComparison.Ordinal) >= 0)
                        return "";
                    string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        @"System32\DriverStore\FileRepository");
                    string full = Path.Combine(root, folder);
                    return Directory.Exists(full) ? full : "";
                }
            }
            catch
            {
                return "";
            }
        }

        public static bool DeleteTask(string taskName, StreamWriter log)
        {
            string o;
            int code = Cmd.Run(Cmd.SysExe("schtasks.exe"),
                "/Delete /TN \"" + taskName + "\" /F", log, out o, 20000);
            return code == 0;
        }

        public static bool TaskExists(string taskName)
        {
            string o;
            int code = Cmd.Run(Cmd.SysExe("schtasks.exe"), "/Query /TN \"" + taskName + "\"", null, out o, 10000);
            if (code >= 0) return code == 0;
            try
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\Tasks");
                return File.Exists(Path.Combine(root, taskName.Replace('\\', Path.DirectorySeparatorChar)));
            }
            catch
            {
                return true;
            }
        }

        public static bool FilterStillPresent(string strHive, string keyPath, string valueName, string filter)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                using (RegistryKey k = baseKey.OpenSubKey(keyPath))
                {
                    if (k == null) return false;
                    object raw = k.GetValue(valueName);
                    string[] arr = raw as string[];
                    if (arr == null)
                    {
                        string s = raw as string;
                        if (s == null) return false;
                        arr = new string[] { s };
                    }
                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (arr[i] != null && string.Equals(arr[i].Trim(), filter, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    return false;
                }
            }
            catch
            {
                return true;
            }
        }
    }

    /// <summary>第 16 步：清理后的快速验证。</summary>
    public class VerifyCheck
    {
        public string Label = "";
        public string Value = "";
        public int State; // 0 = 正常，1 = 仍有残留，2 = 无法检查
    }

    public static class ResidualVerify
    {
        public static List<VerifyCheck> Quick(int remainingItems, StreamWriter log)
        {
            List<VerifyCheck> checks = new List<VerifyCheck>();

            VerifyCheck leftovers = new VerifyCheck();
            leftovers.Label = Loc.T("verify.checkLeftovers");
            leftovers.Value = remainingItems <= 0 ? Loc.T("verify.stateOk")
                : Loc.F("verify.stateFound", remainingItems);
            leftovers.State = remainingItems <= 0 ? 0 : 1;
            checks.Add(leftovers);

            VerifyCheck tasks = new VerifyCheck();
            tasks.Label = Loc.T("verify.checkTasks");
            try
            {
                List<RegItem> list = new List<RegItem>();
                ResidualScan.ScanTasks(list, new HashSet<string>(StringComparer.OrdinalIgnoreCase), null);
                tasks.Value = list.Count == 0 ? Loc.T("verify.stateOk") : Loc.F("verify.stateFound", list.Count);
                tasks.State = list.Count == 0 ? 0 : 1;
            }
            catch
            {
                tasks.Value = Loc.T("verify.stateUnknown");
                tasks.State = 2;
            }
            checks.Add(tasks);

            VerifyCheck fw = new VerifyCheck();
            fw.Label = Loc.T("verify.checkFirewall");
            try
            {
                List<RegItem> list = new List<RegItem>();
                ResidualScan.ScanFirewall(list, new HashSet<string>(StringComparer.OrdinalIgnoreCase), null);
                fw.Value = list.Count == 0 ? Loc.T("verify.stateOk") : Loc.F("verify.stateFound", list.Count);
                fw.State = list.Count == 0 ? 0 : 1;
            }
            catch
            {
                fw.Value = Loc.T("verify.stateUnknown");
                fw.State = 2;
            }
            checks.Add(fw);

            VerifyCheck svc = new VerifyCheck();
            svc.Label = Loc.T("verify.checkServices");
            try
            {
                int n = CountDeadServices();
                svc.Value = n == 0 ? Loc.T("verify.stateOk") : Loc.F("verify.stateFound", n);
                svc.State = n == 0 ? 0 : 1;
            }
            catch
            {
                svc.Value = Loc.T("verify.stateUnknown");
                svc.State = 2;
            }
            checks.Add(svc);

            VerifyCheck evt = new VerifyCheck();
            evt.Label = Loc.T("verify.checkEventLog");
            try
            {
                List<RegItem> list = new List<RegItem>();
                ResidualScan.ScanEventLog(list, new HashSet<string>(StringComparer.OrdinalIgnoreCase), null);
                evt.Value = list.Count == 0 ? Loc.T("verify.stateOk") : Loc.F("verify.stateFound", list.Count);
                evt.State = list.Count == 0 ? 0 : 1;
            }
            catch
            {
                evt.Value = Loc.T("verify.stateUnknown");
                evt.State = 2;
            }
            checks.Add(evt);

            VerifyCheck dev = new VerifyCheck();
            dev.Label = Loc.T("verify.checkDevices");
            try
            {
                int n = CountHiddenDevices();
                dev.Value = n == 0 ? Loc.T("verify.stateOk") : Loc.F("verify.stateFound", n);
                dev.State = n == 0 ? 0 : 1;
            }
            catch
            {
                dev.Value = Loc.T("verify.stateUnknown");
                dev.State = 2;
            }
            checks.Add(dev);

            if (log != null)
            {
                for (int i = 0; i < checks.Count; i++)
                    log.WriteLine("VERIFY " + checks[i].State + " " + checks[i].Label + " = " + checks[i].Value);
            }
            return checks;
        }

        private static int CountDeadServices()
        {
            int n = 0;
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                using (RegistryKey root = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services"))
                {
                    if (root == null) return 0;
                    string[] subs = root.GetSubKeyNames();
                    for (int i = 0; i < subs.Length; i++)
                    {
                        using (RegistryKey sk = root.OpenSubKey(subs[i]))
                        {
                            if (sk == null) continue;
                            object o = sk.GetValue("ImagePath");
                            if (o == null) continue;
                            string img = Environment.ExpandEnvironmentVariables(o.ToString().Trim());
                            if (img.StartsWith("\\??\\", StringComparison.Ordinal)) img = img.Substring(4);
                            string file = Uninstaller.ExtractExe(PathUtil.Clean(img));
                            if (file.Length == 0) continue;
                            if (!file.Contains("\\")) continue;
                            if (File.Exists(file)) continue;
                            if (!PathUtil.Norm(file).StartsWith(@"c:\", StringComparison.Ordinal)) continue;
                            n++;
                        }
                    }
                }
            }
            catch
            {
            }
            return n;
        }

        private static int CountHiddenDevices()
        {
            int n = 0;
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                using (RegistryKey root = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum"))
                {
                    if (root == null) return 0;
                    string[] buses = root.GetSubKeyNames();
                    for (int b = 0; b < buses.Length; b++)
                    {
                        string bus = buses[b].ToLowerInvariant();
                        if (bus == "root" || bus == "htree") continue;
                        using (RegistryKey bk = root.OpenSubKey(buses[b]))
                        {
                            if (bk == null) continue;
                            string[] insts = bk.GetSubKeyNames();
                            for (int i = 0; i < insts.Length; i++)
                            {
                                using (RegistryKey ik = bk.OpenSubKey(insts[i]))
                                {
                                    if (ik == null) continue;
                                    object o = ik.GetValue("Service");
                                    if (o == null) continue;
                                    string svc = o.ToString().Trim();
                                    if (svc.Length == 0) continue;
                                    using (RegistryKey sk2 = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + svc))
                                    {
                                        if (sk2 == null) n++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return n;
        }
    }
}
