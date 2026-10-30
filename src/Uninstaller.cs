using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace CleanC
{
    /// <summary>只允许执行固定的几条系统命令，参数由调用方按严格规则拼装。</summary>
    public static class Cmd
    {
        public static string SysExe(string name)
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (string.IsNullOrEmpty(dir)) dir = @"C:\Windows\System32";
            return Path.Combine(dir, name);
        }

        public static int Run(string exe, string args, StreamWriter log, out string output, int timeoutMs)
        {
            output = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process p = Process.Start(psi))
                {
                    if (p == null) return -1;
                    output = p.StandardOutput.ReadToEnd();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        if (log != null) log.WriteLine("CMD-TIMEOUT " + Path.GetFileName(exe) + " " + args);
                        return -1;
                    }
                    if (log != null) log.WriteLine("CMD " + Path.GetFileName(exe) + " " + args + "  -> " + p.ExitCode);
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                if (log != null) log.WriteLine("CMD-FAIL " + Path.GetFileName(exe) + " " + args + " -> " + ex.Message);
                return -1;
            }
        }
    }

    /// <summary>注册表路径白名单：任何删除动作都必须先过这里。</summary>
    public static class RegGuard
    {
        private static readonly string[] UninstallRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\"
        };

        private static readonly string[] AppPathRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\"
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

        private static readonly string[] PathValueNames =
        {
            "installpath", "installlocation", "installdir", "installdirectory",
            "exepath", "applicationpath", "executablepath", "servicepath",
            "binpath", "iconpath", "iconfile", "displayicon", "localpackage"
        };

        private static readonly string[] ProtectedLeaf =
        {
            "microsoft", "windows", "classes", "policies", "clients", "registeredapplications",
            "wow6432node", "currentversion", "uninstall", "run", "services", "system",
            "environment", "secured", "authentication", "cryptography", "internet explorer",
            "windows nt", "windows defender", "windowsapps"
        };

        public static bool IsHive(string hive)
        {
            return string.Equals(hive, "HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(hive, "HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsUninstallKey(string keyPath)
        {
            for (int i = 0; i < UninstallRoots.Length; i++)
            {
                if (keyPath.StartsWith(UninstallRoots[i], StringComparison.OrdinalIgnoreCase))
                {
                    string leaf = keyPath.Substring(UninstallRoots[i].Length);
                    if (leaf.Length > 0 && leaf.IndexOf('\\') < 0) return true;
                }
            }
            return false;
        }

        /// <summary>注册表残留清理允许的位置：卸载项、App Paths、启动项、软件主键、失效服务、Classes、Installer、筛选器、设备与事件日志。</summary>
        public static bool AllowedJunkPath(string keyPath, bool deleteKey, string valueName)
        {
            string leaf;
            if (IsUninstallKey(keyPath)) return deleteKey;
            for (int i = 0; i < AppPathRoots.Length; i++)
            {
                if (keyPath.StartsWith(AppPathRoots[i], StringComparison.OrdinalIgnoreCase))
                {
                    leaf = keyPath.Substring(AppPathRoots[i].Length);
                    if (leaf.Length > 0 && leaf.IndexOf('\\') < 0) return deleteKey;
                }
            }
            for (int i = 0; i < RunRoots.Length; i++)
            {
                if (string.Equals(keyPath, RunRoots[i], StringComparison.OrdinalIgnoreCase))
                {
                    return !deleteKey && !string.IsNullOrEmpty(valueName);
                }
            }
            for (int i = 0; i < StartupApprovedRoots.Length; i++)
            {
                if (string.Equals(keyPath, StartupApprovedRoots[i], StringComparison.OrdinalIgnoreCase))
                {
                    return !deleteKey && !string.IsNullOrEmpty(valueName);
                }
            }
            if (IsSoftwareLeaf(keyPath)) return deleteKey;
            if (IsServiceKey(keyPath)) return deleteKey;
            if (IsEventLogKey(keyPath)) return deleteKey;
            if (IsClassJunkKey(keyPath)) return deleteKey;
            if (IsInstallerKey(keyPath)) return deleteKey;
            if (IsEnumKey(keyPath)) return deleteKey;
            if (IsClassFilterValue(keyPath, valueName)) return !deleteKey;
            if (IsOpenWithValue(keyPath)) return !deleteKey && !string.IsNullOrEmpty(valueName);
            if (IsRegisteredAppValue(keyPath)) return !deleteKey && !string.IsNullOrEmpty(valueName);
            if (!deleteKey && AllowedSoftwareValue(keyPath, valueName)) return true;
            return false;
        }

        /// <summary>SOFTWARE 深层键里“指向已删除路径”的值允许删除，但键本身不动。</summary>
        public static bool AllowedSoftwareValue(string keyPath, string valueName)
        {
            if (string.IsNullOrEmpty(valueName)) return false;
            string p = keyPath;
            if (p.StartsWith(@"SOFTWARE\WOW6432Node\", StringComparison.OrdinalIgnoreCase))
                p = p.Substring(@"SOFTWARE\WOW6432Node\".Length);
            else if (p.StartsWith(@"SOFTWARE\", StringComparison.OrdinalIgnoreCase))
                p = p.Substring(@"SOFTWARE\".Length);
            else
                return false;
            string[] seg = p.Split('\\');
            if (seg.Length < 1 || seg.Length > 6) return false;
            string first = seg[0].ToLowerInvariant();
            if (first == "microsoft" || first == "windows" || first == "classes" || first == "policies"
                || first == "clients" || first == "registeredapplications" || first == "wow6432node") return false;
            string v = valueName.Trim().ToLowerInvariant();
            for (int i = 0; i < PathValueNames.Length; i++)
            {
                if (v == PathValueNames[i]) return true;
            }
            return false;
        }

        /// <summary>HKCU / HKLM 的 FileExts\...\OpenWithProgids 值，或 SOFTWARE\RegisteredApplications 值。</summary>
        private static bool IsOpenWithValue(string keyPath)
        {
            if (keyPath.EndsWith(@"\OpenWithProgids", StringComparison.OrdinalIgnoreCase)) return true;
            return keyPath.EndsWith(@"\OpenWithList", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRegisteredAppValue(string keyPath)
        {
            return string.Equals(keyPath, @"SOFTWARE\RegisteredApplications", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Classes 下允许删除的残留位置：CLSID 分支、右键菜单处理程序、shell 动词、
        /// Applications\&lt;exe&gt;、以及一层 ProgID。
        /// </summary>
        public static bool IsClassJunkKey(string keyPath)
        {
            string p = StripClassesRoot(keyPath);
            if (p.Length == 0) return false;
            string[] seg = p.Split('\\');
            if (seg.Length == 0) return false;
            string first = seg[0].ToLowerInvariant();
            if (first == "wow6432node" && seg.Length > 1)
            {
                p = p.Substring(seg[0].Length + 1);
                seg = p.Split('\\');
                first = seg[0].ToLowerInvariant();
            }
            if (first == "clsid" && seg.Length == 2 && IsGuid(seg[1])) return true;
            if (first == "applications" && seg.Length == 2 && seg[1].Length >= 3) return true;
            if (first == "installer") return false;
            if (p.EndsWith(@"\shellex\ContextMenuHandlers", StringComparison.OrdinalIgnoreCase)) return false;
            if (p.IndexOf(@"\shellex\ContextMenuHandlers\", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (seg.Length >= 2)
            {
                string last = seg[seg.Length - 1];
                string prev = seg[seg.Length - 2];
                if (prev.Equals("shell", StringComparison.OrdinalIgnoreCase) && last.Length >= 2)
                {
                    string verb = last.ToLowerInvariant();
                    if (verb == "openas" || verb == "printto") return true;
                    return true;
                }
                if (prev.Equals("shell", StringComparison.OrdinalIgnoreCase)) return true;
                if (seg.Length == 3 && seg[1].Equals("shell", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string StripClassesRoot(string keyPath)
        {
            if (keyPath.StartsWith(@"SOFTWARE\Classes\", StringComparison.OrdinalIgnoreCase))
                return keyPath.Substring(@"SOFTWARE\Classes\".Length);
            if (keyPath.StartsWith(@"SOFTWARE\WOW6432Node\Classes\", StringComparison.OrdinalIgnoreCase))
                return keyPath.Substring(@"SOFTWARE\WOW6432Node\Classes\".Length);
            return "";
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

        /// <summary>Installer\UserData\&lt;SID&gt;\Products\&lt;GUID&gt;\InstallProperties，删除前已备份。</summary>
        public static bool IsInstallerKey(string keyPath)
        {
            const string pre = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\";
            if (!keyPath.StartsWith(pre, StringComparison.OrdinalIgnoreCase)) return false;
            string[] seg = keyPath.Substring(pre.Length).Split('\\');
            if (seg.Length != 4) return false;
            if (!seg[1].Equals("Products", StringComparison.OrdinalIgnoreCase)) return false;
            return seg[3].Equals("InstallProperties", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>设备类 GUID 下的 UpperFilters / LowerFilters 值。</summary>
        private static bool IsClassFilterValue(string keyPath, string valueName)
        {
            const string pre = @"SYSTEM\CurrentControlSet\Control\Class\";
            if (!keyPath.StartsWith(pre, StringComparison.OrdinalIgnoreCase)) return false;
            string leaf = keyPath.Substring(pre.Length);
            if (leaf.IndexOf('\\') >= 0 || !IsGuid(leaf)) return false;
            string v = valueName == null ? "" : valueName.ToLowerInvariant();
            return v == "upperfilters" || v == "lowerfilters";
        }

        /// <summary>Enum\&lt;总线&gt;\&lt;设备&gt;\&lt;实例&gt; 下的隐藏设备实例。</summary>
        public static bool IsEnumKey(string keyPath)
        {
            const string pre = @"SYSTEM\CurrentControlSet\Enum\";
            if (!keyPath.StartsWith(pre, StringComparison.OrdinalIgnoreCase)) return false;
            string[] seg = keyPath.Substring(pre.Length).Split('\\');
            if (seg.Length < 3 || seg.Length > 4) return false;
            string bus = seg[0].ToLowerInvariant();
            if (bus == "root" || bus == "htree") return false;
            return seg[seg.Length - 1].Length >= 3;
        }

        /// <summary>事件日志源：Services\EventLog\&lt;日志&gt;\&lt;来源&gt;。</summary>
        public static bool IsEventLogKey(string keyPath)
        {
            const string pre = @"SYSTEM\CurrentControlSet\Services\EventLog\";
            if (!keyPath.StartsWith(pre, StringComparison.OrdinalIgnoreCase)) return false;
            string[] seg = keyPath.Substring(pre.Length).Split('\\');
            if (seg.Length != 2) return false;
            string log = seg[0].ToLowerInvariant();
            if (log == "security") return false;
            return seg[1].Length >= 2;
        }

        private static readonly string[] VendorLeaf =
        {
            "microsoft", "windows", "classes", "policies", "wow6432node", "clients", "registeredapplications",
            "intel", "amd", "nvidia", "realtek", "google", "mozilla", "adobe", "apple", "oracle", "java",
            "odbc", "system", "secure", "speech", "synaptics", "dell", "lenovo", "hp", "realtek"
        };

        /// <summary>SOFTWARE\&lt;产品&gt; 或 SOFTWARE\WOW6432Node\&lt;产品&gt;，只允许一层且不在厂商保护名单。</summary>
        private static bool IsSoftwareLeaf(string keyPath)
        {
            string p = keyPath;
            if (p.StartsWith(@"SOFTWARE\WOW6432Node\", StringComparison.OrdinalIgnoreCase))
                p = p.Substring(@"SOFTWARE\WOW6432Node\".Length);
            else if (p.StartsWith(@"SOFTWARE\", StringComparison.OrdinalIgnoreCase))
                p = p.Substring(@"SOFTWARE\".Length);
            else
                return false;
            if (p.Length < 3 || p.IndexOf('\\') >= 0) return false;
            string l = p.ToLowerInvariant();
            for (int i = 0; i < VendorLeaf.Length; i++)
            {
                if (l == VendorLeaf[i]) return false;
            }
            return !IsProtectedLeaf(p);
        }

        private static readonly string[] ProtectedService =
        {
            "tcpip", "tcpip6", "disk", "ntfs", "volsnap", "volmgr", "partmgr", "mountmgr", "beep", "afd",
            "http", "dnscache", "lanmanworkstation", "lanmanserver", "winmgmt", "eventlog", "rpcss",
            "termservice", "wuauserv", "bfe", "mpssvc", "cryptsvc", "trustedinstaller", "msiserver",
            "wudfsvc", "profSvc", "schedule", "themes", "audioSrv", "audiosrv", "plugplay", "power"
        };

        /// <summary>SYSTEM\CurrentControlSet\Services\&lt;name&gt;，只允许一层且不在系统关键服务名单。</summary>
        private static bool IsServiceKey(string keyPath)
        {
            const string pre = @"SYSTEM\CurrentControlSet\Services\";
            if (!keyPath.StartsWith(pre, StringComparison.OrdinalIgnoreCase)) return false;
            string leaf = keyPath.Substring(pre.Length);
            if (leaf.Length < 3 || leaf.IndexOf('\\') >= 0) return false;
            string l = leaf.ToLowerInvariant();
            for (int i = 0; i < ProtectedService.Length; i++)
            {
                if (l == ProtectedService[i].ToLowerInvariant()) return false;
            }
            if (l.StartsWith("win") || l.StartsWith("ms") || l.StartsWith("wudf")) return false;
            return true;
        }

        private static bool IsProtectedLeaf(string leaf)
        {
            string l = leaf.Trim().ToLowerInvariant();
            for (int i = 0; i < ProtectedLeaf.Length; i++)
            {
                if (l == ProtectedLeaf[i]) return true;
            }
            return false;
        }

        /// <summary>强制卸载时允许删除的软件自身键：SOFTWARE\{厂商}\{产品} 或 SOFTWARE\{产品}。</summary>
        public static bool AllowedAppKey(string keyPath, ProgramEntry p)
        {
            if (!keyPath.StartsWith("SOFTWARE\\", StringComparison.OrdinalIgnoreCase)) return false;
            if (IsUninstallKey(keyPath)) return true;

            string rest = keyPath.Substring("SOFTWARE\\".Length);
            if (rest.StartsWith("WOW6432Node\\", StringComparison.OrdinalIgnoreCase))
                rest = rest.Substring("WOW6432Node\\".Length);
            string[] seg = rest.Split('\\');
            if (seg.Length < 1 || seg.Length > 2) return false;
            string leaf = seg[seg.Length - 1];
            if (leaf.Length < 2) return false;
            if (IsProtectedLeaf(leaf)) return false;

            string token = Uninstaller.ProductToken(p.Name, p.Publisher);
            string nt = Uninstaller.NormalizeName(leaf);
            if (token.Length >= 3 && nt.IndexOf(token, StringComparison.Ordinal) >= 0) return true;
            string nn = Uninstaller.NormalizeName(p.Name);
            if (nn.Length >= 4 && (nn.IndexOf(nt, StringComparison.Ordinal) >= 0 || nt.IndexOf(nn, StringComparison.Ordinal) >= 0))
                return true;
            return false;
        }
    }

    public static class Uninstaller
    {
        private static readonly string[] UninstallRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        private static readonly string[] ProtectedPathPrefix =
        {
            @"c:\windows",
            @"c:\program files\windowsapps",
            @"c:\program files\windows defender",
            @"c:\program files\windows mail",
            @"c:\program files\common files",
            @"c:\program files (x86)\common files",
            @"c:\program files\microsoft",
            @"c:\program files (x86)\microsoft",
            @"c:\programdata\microsoft",
            @"c:\programdata\package cache",
            @"c:\$windows.~bt",
            @"c:\$windows.~ws",
            @"c:\system volume information"
        };

        private static readonly string[] ProtectedNameHint =
        {
            "microsoft visual c++", "microsoft vc++", "microsoft .net", ".net runtime",
            "windows sdk", "windows driver", "windows feature", "servicing stack",
            "microsoft edge", "windows defender", "directx", "microsoft update",
            "windows update", "visual studio installer", "windows app runtime",
            "microsoft store", "security update", "windows kit",
            "nvidia", "amd chipset", "intel graphics", "图形驱动", "显卡驱动", "显示驱动",
            "驱动程序", "驱动包", "driver package"
        };

        private static readonly Color[] Palette =
        {
            Color.FromArgb(47, 123, 255), Color.FromArgb(99, 102, 241), Color.FromArgb(139, 92, 246),
            Color.FromArgb(34, 199, 232), Color.FromArgb(20, 184, 166), Color.FromArgb(245, 158, 11),
            Color.FromArgb(251, 113, 133), Color.FromArgb(14, 165, 233)
        };

        // ---------------------------------------------------------------- enumerate

        public static List<ProgramEntry> Enumerate(Action<int, int> progress, Func<bool> canceled)
        {
            List<ProgramEntry> list = new List<ProgramEntry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Collect(RegistryHive.LocalMachine, list, seen, canceled);
            Collect(RegistryHive.CurrentUser, list, seen, canceled);
            list.Sort(delegate(ProgramEntry a, ProgramEntry b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            int total = list.Count;
            int done = 0;
            int startTick = Environment.TickCount;
            for (int i = 0; i < list.Count; i++)
            {
                if (canceled != null && canceled()) break;
                if (!list[i].Measured)
                {
                    if (list[i].Protected)
                    {
                        // 受保护的程序不可删除，不必遍历它的目录
                        list[i].Measured = true;
                    }
                    else if (unchecked(Environment.TickCount - startTick) < 3000)
                    {
                        Measure(list[i]);
                    }
                    else
                    {
                        list[i].Measured = true;
                    }
                }
                done++;
                if (progress != null) progress(done, total);
            }
            return list;
        }

        private static void Collect(RegistryHive hive, List<ProgramEntry> list, HashSet<string> seen, Func<bool> canceled)
        {
            for (int r = 0; r < UninstallRoots.Length; r++)
            {
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                    using (RegistryKey root = baseKey.OpenSubKey(UninstallRoots[r]))
                    {
                        if (root == null) continue;
                        string[] subs = root.GetSubKeyNames();
                        for (int i = 0; i < subs.Length; i++)
                        {
                            if (canceled != null && canceled()) return;
                            try
                            {
                                using (RegistryKey sk = root.OpenSubKey(subs[i]))
                                {
                                    if (sk == null) continue;
                                    ProgramEntry e = Read(hive, UninstallRoots[r], subs[i], sk);
                                    if (e == null) continue;
                                    string key = (e.Name + "|" + e.InstallDir).ToLowerInvariant();
                                    if (!seen.Add(key)) continue;
                                    list.Add(e);
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

        private static ProgramEntry Read(RegistryHive hive, string rootPath, string keyName, RegistryKey sk)
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
            string quiet = Str(sk, "QuietUninstallString");
            string loc = Str(sk, "InstallLocation");
            string icon = Str(sk, "DisplayIcon");
            string dir = ResolveInstallDir(loc, uninstall, icon);
            if (dir.Length == 0 && uninstall.Length == 0) return null;

            ProgramEntry e = new ProgramEntry();
            e.Name = name;
            e.Publisher = Str(sk, "Publisher");
            e.Version = Str(sk, "DisplayVersion");
            e.InstallDir = dir;
            e.UninstallString = uninstall.Length > 0 ? uninstall : quiet;
            e.RegKey = HiveName(hive) + "\\" + rootPath + "\\" + keyName;
            e.KeyName = keyName;
            e.MachineWide = hive == RegistryHive.LocalMachine;
            e.Glyph = GlyphFor(name);
            e.Accent = Palette[Math.Abs(name.GetHashCode()) % Palette.Length];

            long est = 0;
            try
            {
                object o = sk.GetValue("EstimatedSize");
                if (o != null) est = Convert.ToInt64(o) * 1024L;
            }
            catch
            {
            }
            if (est > 0)
            {
                e.Bytes = est;
                e.Measured = true;
            }

            MarkProtected(e);
            return e;
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

        private static string GlyphFor(string name)
        {
            string t = name.Trim();
            if (t.Length == 0) return "?";
            char c = t[0];
            if (c >= 'a' && c <= 'z') return ((char)(c - 32)).ToString();
            return c.ToString();
        }

        public static string ResolveInstallDir(string installLocation, string uninstallString, string displayIcon)
        {
            string d = PathUtil.Clean(installLocation);
            if (d.Length > 0 && Directory.Exists(d)) return d;
            if (d.Length > 0 && File.Exists(d)) return Path.GetDirectoryName(d);

            string u = PathUtil.Clean(uninstallString);
            if (u.Length > 0)
            {
                string exe = ExtractExe(u);
                if (exe.Length > 0 && File.Exists(exe)) return Path.GetDirectoryName(exe);
            }
            string icon = PathUtil.Clean(displayIcon);
            if (icon.Length > 0)
            {
                int comma = icon.LastIndexOf(',');
                if (comma > 2) icon = PathUtil.Clean(icon.Substring(0, comma));
                if (File.Exists(icon)) return Path.GetDirectoryName(icon);
            }
            if (d.Length > 0) return d;
            return "";
        }

        public static string ExtractExe(string command)
        {
            string s = command.Trim();
            if (s.Length == 0) return "";
            if (s[0] == '"')
            {
                int q = s.IndexOf('"', 1);
                if (q > 1) return s.Substring(1, q - 1);
                return "";
            }
            if (s.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0) return "";
            int exe = s.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exe > 0) return s.Substring(0, exe + 4);
            int sp = s.IndexOf(' ');
            if (sp > 0) return s.Substring(0, sp);
            return s;
        }

        public static void Measure(ProgramEntry e)
        {
            e.Measured = true;
            string dir = e.InstallDir;
            if (dir.Length == 0 || !Directory.Exists(dir)) return;
            long bytes = 0;
            int files = 0;
            int guard = 0;
            List<string> stack = new List<string>();
            stack.Add(dir);
            while (stack.Count > 0 && guard < 24000)
            {
                string cur = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                try
                {
                    string[] fs = Directory.GetFiles(cur);
                    for (int i = 0; i < fs.Length && guard < 24000; i++)
                    {
                        guard++;
                        try
                        {
                            bytes += new FileInfo(fs[i]).Length;
                            files++;
                        }
                        catch
                        {
                        }
                    }
                    string[] ds = Directory.GetDirectories(cur);
                    for (int i = 0; i < ds.Length; i++)
                    {
                        try
                        {
                            if ((File.GetAttributes(ds[i]) & FileAttributes.ReparsePoint) != 0) continue;
                        }
                        catch
                        {
                            continue;
                        }
                        stack.Add(ds[i]);
                    }
                }
                catch
                {
                }
            }
            if (bytes > 0)
            {
                e.Bytes = bytes;
                e.FileCount = files;
            }
        }

        public static void EvaluateProtection(ProgramEntry e)
        {
            e.Protected = false;
            e.ProtectedReason = "";
            MarkProtected(e);
        }

        private static void MarkProtected(ProgramEntry e)
        {
            string n = NormalizeName(e.Name);
            for (int i = 0; i < ProtectedNameHint.Length; i++)
            {
                if (n.IndexOf(NormalizeName(ProtectedNameHint[i]), StringComparison.Ordinal) >= 0)
                {
                    e.Protected = true;
                    e.ProtectedReason = Loc.T("uninstall.protectRuntime");
                    return;
                }
            }
            if (e.Publisher.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (n.IndexOf("runtime", StringComparison.Ordinal) >= 0 ||
                    n.IndexOf("redistributable", StringComparison.Ordinal) >= 0 ||
                    n.IndexOf("framework", StringComparison.Ordinal) >= 0 ||
                    n.IndexOf("sdk", StringComparison.Ordinal) >= 0 ||
                    n.IndexOf("driver", StringComparison.Ordinal) >= 0 ||
                    n.IndexOf("defender", StringComparison.Ordinal) >= 0 ||
                    n.IndexOf("edge", StringComparison.Ordinal) >= 0 ||
                    n.IndexOf("windows", StringComparison.Ordinal) >= 0)
                {
                    e.Protected = true;
                    e.ProtectedReason = Loc.T("uninstall.protectMicrosoft");
                    return;
                }
            }
            string dir = PathUtil.Norm(e.InstallDir);
            if (dir.Length == 0)
            {
                e.Protected = true;
                e.ProtectedReason = Loc.T("uninstall.protectNoPath");
                return;
            }
            for (int i = 0; i < ProtectedPathPrefix.Length; i++)
            {
                if (PathUtil.Under(dir, ProtectedPathPrefix[i]))
                {
                    e.Protected = true;
                    e.ProtectedReason = Loc.T("uninstall.protectSystemPath");
                    return;
                }
            }
            if (dir.Length <= @"c:\a\b".Length)
            {
                e.Protected = true;
                e.ProtectedReason = Loc.T("uninstall.protectTooShallow");
            }
        }

        public static string NormalizeName(string s)
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

        public static string ProductToken(string name)
        {
            return ProductToken(name, "");
        }

        /// <summary>取产品名中最具辨识度的词，跳过属于发行商名的词，避免误删厂商公共键。</summary>
        public static string ProductToken(string name, string publisher)
        {
            string n = (name == null ? "" : name).ToLowerInvariant();
            int cut = n.IndexOfAny(new char[] { '(', '[', '-' });
            if (cut > 3) n = n.Substring(0, cut);
            string[] parts = n.Split(new char[] { ' ', '_', '.', ',', ':', '/' }, StringSplitOptions.RemoveEmptyEntries);
            string pub = NormalizeName(publisher);
            string best = "";
            string fallback = "";
            for (int i = 0; i < parts.Length; i++)
            {
                string p = NormalizeName(parts[i]);
                if (p.Length > fallback.Length) fallback = p;
                if (p.Length < 3) continue;
                if (pub.Length > 0 && pub.IndexOf(p, StringComparison.Ordinal) >= 0) continue;
                if (p.Length > best.Length) best = p;
            }
            if (best.Length == 0) best = fallback;
            return best;
        }

        // ---------------------------------------------------------------- normal remove

        public static UninstallReport NormalRemove(ProgramEntry e, bool isAdmin, StreamWriter log,
            Action<int, string> onStep, Func<bool> canceled)
        {
            UninstallReport rep = new UninstallReport();
            if (e.Protected)
            {
                rep.Refused = true;
                rep.RefuseReason = e.ProtectedReason;
                return rep;
            }
            if (onStep != null) onStep(1, Loc.T("uninstall.stepFiles"));
            DeleteInstallTree(e, rep, log, canceled);
            if (rep.Canceled) return rep;

            if (onStep != null) onStep(2, Loc.T("uninstall.stepShortcut"));
            rep.Shortcuts = DeleteShortcuts(e, log);
            rep.Note(Loc.F("uninstall.doneNormal", rep.FilesDeleted, rep.Shortcuts));
            return rep;
        }

        private static void DeleteInstallTree(ProgramEntry e, UninstallReport rep, StreamWriter log, Func<bool> canceled)
        {
            string dir = e.InstallDir;
            if (dir.Length == 0 || !Directory.Exists(dir)) return;
            try
            {
                RemoveDir(new DirectoryInfo(dir), rep, log, canceled, 0);
            }
            catch
            {
                rep.Failures++;
            }
        }

        private static void RemoveDir(DirectoryInfo d, UninstallReport rep, StreamWriter log, Func<bool> canceled, int depth)
        {
            if (depth > 24) return;
            if (canceled != null && canceled())
            {
                rep.Canceled = true;
                return;
            }
            FileInfo[] files;
            try
            {
                files = d.GetFiles();
            }
            catch
            {
                rep.Failures++;
                return;
            }
            for (int i = 0; i < files.Length; i++)
            {
                if (canceled != null && canceled())
                {
                    rep.Canceled = true;
                    return;
                }
                DeleteFile(files[i], rep, log);
            }
            DirectoryInfo[] subs;
            try
            {
                subs = d.GetDirectories();
            }
            catch
            {
                subs = new DirectoryInfo[0];
            }
            for (int i = 0; i < subs.Length; i++)
            {
                try
                {
                    if ((subs[i].Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        try { subs[i].Delete(); } catch { }
                        continue;
                    }
                }
                catch
                {
                    continue;
                }
                RemoveDir(subs[i], rep, log, canceled, depth + 1);
            }
            try
            {
                if (d.GetFileSystemInfos().Length == 0 && depth > 0) d.Delete();
            }
            catch
            {
            }
        }

        private static bool DeleteFile(FileInfo f, UninstallReport rep, StreamWriter log)
        {
            long len = 0;
            try { len = f.Length; } catch { }
            try
            {
                try { f.Attributes = FileAttributes.Normal; } catch { }
                f.Delete();
                rep.FilesDeleted++;
                rep.BytesFreed += len;
                return true;
            }
            catch
            {
                rep.Failures++;
                return false;
            }
        }

        // ---------------------------------------------------------------- force remove

        public static UninstallReport ForceRemove(ProgramEntry e, bool isAdmin, StreamWriter log,
            Action<int, string> onStep, Func<bool> canceled)
        {
            UninstallReport rep = new UninstallReport();
            if (e.Protected)
            {
                rep.Refused = true;
                rep.RefuseReason = e.ProtectedReason;
                return rep;
            }

            if (onStep != null) onStep(1, Loc.T("uninstall.stepKill"));
            rep.Processes = KillProcesses(e.InstallDir, log);
            if (canceled != null && canceled())
            {
                rep.Canceled = true;
                return rep;
            }

            if (onStep != null) onStep(2, Loc.T("uninstall.stepFiles"));
            DeleteInstallTree(e, rep, log, canceled);
            if (rep.Canceled) return rep;

            if (rep.Failures > 0 && isAdmin && e.InstallDir.Length > 0 && Directory.Exists(e.InstallDir))
            {
                if (onStep != null) onStep(3, Loc.T("uninstall.stepTakeown"));
                TakeOwnership(e.InstallDir, log);
                int before = rep.Failures;
                DeleteInstallTree(e, rep, log, canceled);
                if (rep.Failures > before) rep.Note(Loc.T("uninstall.takeownPartial"));
            }

            if (onStep != null) onStep(4, Loc.T("uninstall.stepRegistry"));
            string backup = BackupDir();
            rep.BackupDir = backup;
            List<RegTarget> targets = RegistryTargets(e);
            for (int i = 0; i < targets.Count; i++)
            {
                if (canceled != null && canceled())
                {
                    rep.Canceled = true;
                    return rep;
                }
                RegTarget t = targets[i];
                if (DeleteRegKey(t.Hive, t.Path, backup, log))
                {
                    rep.RegKeys++;
                }
                else
                {
                    rep.Failures++;
                }
            }

            if (onStep != null) onStep(5, Loc.T("uninstall.stepShortcut"));
            rep.Shortcuts = DeleteShortcuts(e, log);

            rep.Note(Loc.F("uninstall.doneForce", rep.FilesDeleted, rep.RegKeys));
            return rep;
        }

        public class RegTarget
        {
            public string Hive;
            public string Path;
            public RegTarget(string hive, string path)
            {
                Hive = hive;
                Path = path;
            }
        }

        public static List<RegTarget> RegistryTargets(ProgramEntry e)
        {
            List<RegTarget> list = new List<RegTarget>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddTarget(list, seen, e.RegKey);
            string token = Uninstaller.ProductToken(e.Name, e.Publisher);
            string vendor = ProductToken(e.Publisher);

            if (token.Length >= 3)
            {
                string[] hives = { "HKEY_CURRENT_USER", "HKEY_LOCAL_MACHINE" };
                for (int h = 0; h < hives.Length; h++)
                {
                    string prefix = hives[h] + "\\SOFTWARE\\";
                    TryAddByLeaf(list, seen, hives[h], prefix + token, e);
                    if (vendor.Length >= 3 && vendor != token)
                    {
                        TryAddByLeaf(list, seen, hives[h], prefix + vendor + "\\" + token, e);
                    }
                    if (hives[h] == "HKEY_LOCAL_MACHINE")
                    {
                        TryAddByLeaf(list, seen, hives[h], "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\" + token, e);
                    }
                }
            }
            return list;
        }

        private static void AddTarget(List<RegTarget> list, HashSet<string> seen, string full)
        {
            if (string.IsNullOrEmpty(full)) return;
            int cut = full.IndexOf('\\');
            if (cut <= 0) return;
            string hive = full.Substring(0, cut);
            string path = full.Substring(cut + 1);
            if (!RegGuard.IsHive(hive)) return;
            string sig = hive + "\\" + path.ToLowerInvariant();
            if (!seen.Add(sig)) return;
            list.Add(new RegTarget(hive, path));
        }

        private static void TryAddByLeaf(List<RegTarget> list, HashSet<string> seen, string hive,
            string fullKey, ProgramEntry e)
        {
            int cut = fullKey.IndexOf('\\');
            if (cut <= 0) return;
            string path = fullKey.Substring(cut + 1);
            if (!RegGuard.AllowedAppKey(path, e)) return;
            if (!KeyExists(hive, path)) return;
            AddTarget(list, seen, fullKey);
        }

        private static bool KeyExists(string hive, string path)
        {
            try
            {
                RegistryHive h = string.Equals(hive, "HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                    ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(h, RegistryView.Default))
                using (RegistryKey k = baseKey.OpenSubKey(path))
                {
                    return k != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private static int KillProcesses(string dir, StreamWriter log)
        {
            if (dir.Length == 0) return 0;
            string prefix = PathUtil.Norm(dir);
            int killed = 0;
            Process[] all;
            try
            {
                all = Process.GetProcesses();
            }
            catch
            {
                return 0;
            }
            for (int i = 0; i < all.Length; i++)
            {
                Process p = all[i];
                try
                {
                    int pid = p.Id;
                    if (pid <= 4) continue;
                    string exe = "";
                    try { exe = p.MainModule.FileName; } catch { }
                    if (exe.Length == 0) continue;
                    if (!PathUtil.Under(PathUtil.Norm(exe), prefix)) continue;
                    if (string.Equals(Path.GetFileName(exe), "explorer.exe", StringComparison.OrdinalIgnoreCase)) continue;
                    string o;
                    Cmd.Run(Cmd.SysExe("taskkill.exe"), "/F /PID " + pid, log, out o, 8000);
                    killed++;
                }
                catch
                {
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }
            return killed;
        }

        private static void TakeOwnership(string dir, StreamWriter log)
        {
            string o;
            Cmd.Run(Cmd.SysExe("takeown.exe"), "/F \"" + dir + "\" /R /D Y", log, out o, 120000);
            Cmd.Run(Cmd.SysExe("icacls.exe"), "\"" + dir + "\" /grant *S-1-5-32-544:F /T /C /Q", log, out o, 120000);
        }

        public static bool DeleteRegKey(string hive, string path, string backupDir, StreamWriter log)
        {
            if (!RegGuard.IsHive(hive)) return false;
            if (path.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
            string shortHive = string.Equals(hive, "HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase) ? "HKCU" : "HKLM";
            string full = shortHive + "\\" + path;
            try
            {
                if (!string.IsNullOrEmpty(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                    string safe = path.Replace('\\', '_');
                    if (safe.Length > 90) safe = safe.Substring(safe.Length - 90);
                    string file = Path.Combine(backupDir, safe + ".reg");
                    string o;
                    Cmd.Run(Cmd.SysExe("reg.exe"), "export \"" + full + "\" \"" + file + "\" /y", log, out o, 20000);
                }
                string output;
                int code = Cmd.Run(Cmd.SysExe("reg.exe"), "delete \"" + full + "\" /f", log, out output, 20000);
                return code == 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool DeleteRegValue(string hive, string path, string value, string backupDir, StreamWriter log)
        {
            if (!RegGuard.IsHive(hive) || string.IsNullOrEmpty(value)) return false;
            string shortHive = string.Equals(hive, "HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase) ? "HKCU" : "HKLM";
            string full = shortHive + "\\" + path;
            try
            {
                if (!string.IsNullOrEmpty(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                    string safe = path.Replace('\\', '_') + "_" + value.Replace('\\', '_');
                    if (safe.Length > 90) safe = safe.Substring(safe.Length - 90);
                    string o;
                    Cmd.Run(Cmd.SysExe("reg.exe"),
                        "export \"" + full + "\" \"" + Path.Combine(backupDir, safe + ".reg") + "\" /y", log, out o, 20000);
                }
                string output;
                int code = Cmd.Run(Cmd.SysExe("reg.exe"),
                    "delete \"" + full + "\" /v \"" + value + "\" /f", log, out output, 20000);
                return code == 0;
            }
            catch
            {
                return false;
            }
        }

        public static string BackupDir()
        {
            string root;
            try { root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
            catch { root = Path.GetTempPath(); }
            return Path.Combine(root, "CleanC", "regbackup", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        }

        // ---------------------------------------------------------------- shortcuts

        public static int DeleteShortcuts(ProgramEntry e, StreamWriter log)
        {
            int n = 0;
            List<string> dirs = new List<string>();
            try { dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs")); } catch { }
            try { dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs")); } catch { }
            try { dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)); } catch { }
            try { dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)); } catch { }
            try { dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch")); } catch { }

            for (int i = 0; i < dirs.Count; i++)
            {
                string d = dirs[i];
                if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) continue;
                List<string> files = new List<string>();
                try
                {
                    string[] sub = Directory.GetDirectories(d);
                    files.AddRange(Directory.GetFiles(d, "*.lnk"));
                    for (int s = 0; s < sub.Length && s < 60; s++)
                    {
                        try { files.AddRange(Directory.GetFiles(sub[s], "*.lnk")); } catch { }
                    }
                }
                catch
                {
                    continue;
                }
                for (int f = 0; f < files.Count; f++)
                {
                    string lnk = files[f];
                    if (!ShortcutMatches(lnk, e)) continue;
                    try
                    {
                        File.Delete(lnk);
                        n++;
                        if (log != null) log.WriteLine("LNK  " + lnk);
                    }
                    catch
                    {
                    }
                }
            }
            return n;
        }

        private static bool ShortcutMatches(string lnk, ProgramEntry e)
        {
            string fileName = Path.GetFileNameWithoutExtension(lnk);
            string a = NormalizeName(fileName);
            string b = NormalizeName(e.Name);
            if (a.Length >= 3 && b.Length >= 4)
            {
                if (a.IndexOf(b, StringComparison.Ordinal) >= 0) return true;
                if (b.IndexOf(a, StringComparison.Ordinal) >= 0 && a.Length >= 6) return true;
            }
            string dir = PathUtil.Norm(e.InstallDir);
            if (dir.Length < 6) return false;
            try
            {
                byte[] bytes = File.ReadAllBytes(lnk);
                if (bytes.Length > 65536) return false;
                string ascii = Encoding.Default.GetString(bytes).ToLowerInvariant();
                if (ascii.IndexOf(dir, StringComparison.Ordinal) >= 0) return true;
                string uni = Encoding.Unicode.GetString(bytes).ToLowerInvariant();
                if (uni.IndexOf(dir, StringComparison.Ordinal) >= 0) return true;
            }
            catch
            {
            }
            return false;
        }
    }
}
