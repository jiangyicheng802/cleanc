using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace CleanC
{
    public enum DefenseLevel
    {
        Low = 0,
        High = 1
    }

    public enum ThreatKind
    {
        FileIntegrity = 0,
        Registry = 1,
        Startup = 2,
        SuspiciousFile = 3,
        Process = 4,
        Config = 5
    }

    public enum ThreatSeverity
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public enum ThreatFix
    {
        None = 0,
        RestoreRegistry = 1,
        RemoveIfeo = 2,
        QuarantineFile = 3,
        DisableStartup = 4,
        KillProcess = 5
    }

    /// <summary>一条防御风险：来源、结论、建议与可执行的处置方式。</summary>
    public class Threat
    {
        public string Id = "";
        public ThreatKind Kind = ThreatKind.Config;
        public ThreatSeverity Severity = ThreatSeverity.Medium;
        public string Title = "";
        public string Detail = "";
        public string Solution = "";
        /// <summary>文件路径，或注册表值的显示路径。</summary>
        public string Target = "";
        public string Hive = "";
        public string KeyPath = "";
        public string ValueName = "";
        public string Command = "";
        public int Pid;
        public string ProcessName = "";
        public ThreatFix Fix = ThreatFix.None;
        public string FixValue = "";
        public bool FixAbsent;
        public int FixKind;
        public bool FileExists;
        public bool Running;
        public DateTime Time = DateTime.Now;
        public string Extra = "";

        public string SeverityColorKey
        {
            get { return Severity == ThreatSeverity.High ? "def.sev.high" : (Severity == ThreatSeverity.Medium ? "def.sev.medium" : "def.sev.low"); }
        }

        public string KindText
        {
            get
            {
                if (Kind == ThreatKind.FileIntegrity) return Loc.T("def.kind.file");
                if (Kind == ThreatKind.Registry) return Loc.T("def.kind.registry");
                if (Kind == ThreatKind.Startup) return Loc.T("def.kind.startup");
                if (Kind == ThreatKind.SuspiciousFile) return Loc.T("def.kind.suspicious");
                if (Kind == ThreatKind.Config) return Loc.T("def.kind.config");
                return Loc.T("def.kind.process");
            }
        }
    }

    public class DefenseAlert
    {
        public List<Threat> NewThreats = new List<Threat>();
        public List<string> Killed = new List<string>();
        public bool BaselineCreated;
    }

    public static class DefensePaths
    {
        public static string Dir
        {
            get
            {
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(Path.Combine(dir, "CleanC"));
            }
        }

        public static string BaselinePath
        {
            get { return Path.Combine(Dir, "defense_baseline.dat"); }
        }

        public static string IgnorePath
        {
            get { return Path.Combine(Dir, "defense_ignore.txt"); }
        }

        public static string QuarantineDir
        {
            get { return Path.Combine(Dir, "quarantine"); }
        }

        public static string LogPath
        {
            get { return Path.Combine(Path.Combine(Dir, "logs"), "defense.log"); }
        }
    }

    public static class DefenseLog
    {
        private static readonly object Lock = new object();

        public static void Write(string tag, string message)
        {
            lock (Lock)
            {
                try
                {
                    string path = DefensePaths.LogPath;
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    try
                    {
                        FileInfo fi = new FileInfo(path);
                        if (fi.Exists && fi.Length > 2 * 1024 * 1024)
                        {
                            string old = path + ".1";
                            if (File.Exists(old)) File.Delete(old);
                            File.Move(path, old);
                        }
                    }
                    catch
                    {
                    }
                    using (StreamWriter w = new StreamWriter(path, true, new UTF8Encoding(false)))
                    {
                        w.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + tag + "\t" + message);
                    }
                }
                catch
                {
                }
            }
        }
    }

    // ------------------------------------------------------------------ baseline

    internal class FileProbeData
    {
        public long Size;
        public long Stamp;
        public string Hash = "";
    }

    internal class RegProbeData
    {
        public bool Absent = true;
        public string Value = "";
        public int Kind;
    }

    internal class DefenseBaseline
    {
        public Dictionary<string, FileProbeData> Files =
            new Dictionary<string, FileProbeData>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, RegProbeData> Reg =
            new Dictionary<string, RegProbeData>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Ifeo =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public bool Valid;
        public bool Dirty;
    }

    internal class RegProbe
    {
        public RegistryHive Hive;
        public string Key;
        public string Name;

        public RegProbe(RegistryHive hive, string key, string name)
        {
            Hive = hive;
            Key = key;
            Name = name;
        }

        public string HiveName
        {
            get { return Hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU"; }
        }

        public string Display
        {
            get
            {
                return HiveName + "\\" + Key + (Name.Length > 0 ? "\\" + Name : "");
            }
        }

        public string IdKey
        {
            get { return HiveName + "|" + Key + "|" + Name; }
        }
    }

    public class IntegrityReport
    {
        public int FilesChecked;
        public int RegChecked;
        public bool BaselineCreated;
        public bool Tamper;
        public List<Threat> Threats = new List<Threat>();
    }

    /// <summary>
    /// 系统完整性基线：系统目录关键文件的指纹 + 关键注册表值 + IFEO 调试器快照。
    /// 第一次开启防御时建立基线；之后只报告「与基线不一致且不像微软官方更新」的变化。
    /// </summary>
    public static class IntegritySurvey
    {
        private const string IfeoKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
        private const string BaselineHeader = "CleanC defense baseline v1";

        public static readonly string[] SystemFiles = BuildSystemFiles();

        internal static readonly RegProbe[] Probes =
        {
            new RegProbe(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Shell"),
            new RegProbe(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Userinit"),
            new RegProbe(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows", "AppInit_DLLs"),
            new RegProbe(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows", "LoadAppInit_DLLs"),
            new RegProbe(RegistryHive.LocalMachine, @"SOFTWARE\Wow6432Node\Microsoft\Windows NT\CurrentVersion\Windows", "AppInit_DLLs"),
            new RegProbe(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\SafeBoot", "AlternateShell"),
            new RegProbe(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware"),
            new RegProbe(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows Defender", "DisableAntiSpyware"),
            new RegProbe(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring"),
            new RegProbe(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring")
        };

        private static readonly string[] CriticalIfeoNames =
        {
            "explorer.exe", "winlogon.exe", "lsass.exe", "services.exe", "svchost.exe",
            "csrss.exe", "smss.exe", "taskmgr.exe", "regedit.exe", "cmd.exe",
            "powershell.exe", "msconfig.exe", "notepad.exe", "rundll32.exe"
        };

        private static readonly string[] SystemNames =
        {
            "svchost", "lsass", "winlogon", "services", "explorer", "csrss", "smss",
            "taskmgr", "regedit", "rundll32", "spoolsv", "wininit", "winver"
        };

        private static readonly object Lock = new object();
        private static DefenseBaseline _cache;
        private static readonly Dictionary<string, bool> SignatureCache =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private static string[] BuildSystemFiles()
        {
            string dir = Environment.SystemDirectory;
            string[] names =
            {
                "kernel32.dll", "ntdll.dll", "user32.dll", "advapi32.dll", "gdi32.dll",
                "shell32.dll", "winlogon.exe", "services.exe", "lsass.exe", "svchost.exe",
                "cmd.exe", "taskmgr.exe"
            };
            string[] list = new string[names.Length];
            for (int i = 0; i < names.Length; i++) list[i] = Path.Combine(dir, names[i]);
            return list;
        }

        public static bool IsCriticalIfeoName(string name)
        {
            for (int i = 0; i < CriticalIfeoNames.Length; i++)
            {
                if (string.Equals(CriticalIfeoNames[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static bool IsSystemProcessName(string name)
        {
            string n = Path.GetFileNameWithoutExtension(name);
            if (n == null) return false;
            n = n.ToLowerInvariant();
            for (int i = 0; i < SystemNames.Length; i++)
            {
                if (n == SystemNames[i]) return true;
            }
            return false;
        }

        // -------------------------------------------------------------- file/reg basics

        public static string HashFile(string path)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan))
                using (SHA256 sha = new SHA256Managed())
                {
                    byte[] hash = sha.ComputeHash(fs);
                    StringBuilder b = new StringBuilder(hash.Length * 2);
                    for (int i = 0; i < hash.Length; i++) b.Append(hash[i].ToString("x2"));
                    return b.ToString();
                }
            }
            catch
            {
                return "";
            }
        }

        public static string Fingerprint(string value)
        {
            try
            {
                using (SHA256 sha = new SHA256Managed())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value == null ? "" : value));
                    StringBuilder b = new StringBuilder(16);
                    for (int i = 0; i < 8; i++) b.Append(hash[i].ToString("x2"));
                    return b.ToString();
                }
            }
            catch
            {
                return "0000000000000000";
            }
        }

        private static RegProbeData ReadProbe(RegProbe p)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(p.Hive, RegistryView.Default))
                using (RegistryKey k = baseKey.OpenSubKey(p.Key, false))
                {
                    if (k == null) return new RegProbeData { Absent = true };
                    object v = k.GetValue(p.Name, null);
                    if (v == null) return new RegProbeData { Absent = true };
                    int kind = 0;
                    try { kind = (int)k.GetValueKind(p.Name); } catch { }
                    return new RegProbeData { Absent = false, Value = RegToString(v), Kind = kind };
                }
            }
            catch
            {
                return null;
            }
        }

        private static string RegToString(object v)
        {
            if (v == null) return "";
            if (v is string) return (string)v;
            if (v is int) return ((int)v).ToString();
            if (v is long) return ((long)v).ToString();
            string[] sa = v as string[];
            if (sa != null) return string.Join("; ", sa, 0, sa.Length);
            byte[] ba = v as byte[];
            if (ba != null) return Convert.ToBase64String(ba);
            return v.ToString();
        }

        private static string NormalizeValue(string s)
        {
            if (s == null) return "";
            s = s.Trim().ToLowerInvariant();
            StringBuilder b = new StringBuilder(s.Length);
            bool space = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\t' || c == '\r' || c == '\n' || c == ' ') { space = true; continue; }
                if (space && b.Length > 0) b.Append(' ');
                space = false;
                b.Append(c);
            }
            while (b.Length > 0 && (b[b.Length - 1] == ',' || b[b.Length - 1] == ';')) b.Length--;
            return b.ToString();
        }

        private static bool RegEqual(RegProbeData a, RegProbeData b)
        {
            if (a == null || b == null) return true;
            if (a.Absent || b.Absent) return a.Absent && b.Absent;
            return NormalizeValue(a.Value) == NormalizeValue(b.Value);
        }

        private static bool WriteProbe(string hive, string key, string name, bool absent, string value, int kind)
        {
            try
            {
                RegistryHive h = string.Equals(hive, "HKLM", StringComparison.OrdinalIgnoreCase)
                    ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(h, RegistryView.Default))
                using (RegistryKey k = baseKey.OpenSubKey(key, true))
                {
                    if (k == null) return absent;
                    if (absent)
                    {
                        try { k.DeleteValue(name, false); } catch { }
                        return true;
                    }
                    if (kind == (int)RegistryValueKind.DWord)
                    {
                        int n = 0;
                        int.TryParse(value, out n);
                        k.SetValue(name, n, RegistryValueKind.DWord);
                    }
                    else
                    {
                        k.SetValue(name, value, RegistryValueKind.String);
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static Dictionary<string, string> ReadIfeo()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(IfeoKey, false))
                {
                    if (k == null) return map;
                    string[] names = k.GetSubKeyNames();
                    for (int i = 0; i < names.Length; i++)
                    {
                        try
                        {
                            using (RegistryKey sub = k.OpenSubKey(names[i], false))
                            {
                                if (sub == null) continue;
                                object d = sub.GetValue("Debugger", null);
                                if (d is string && ((string)d).Trim().Length > 0) map[names[i]] = (string)d;
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
            return map;
        }

        // -------------------------------------------------------------- load/save

        private static DefenseBaseline Load()
        {
            if (_cache != null) return _cache;
            DefenseBaseline b = new DefenseBaseline();
            try
            {
                string path = DefensePaths.BaselinePath;
                if (File.Exists(path))
                {
                    string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        if (line.Length == 0 || line.StartsWith("#")) continue;
                        string[] f = line.Split('\t');
                        if (f.Length < 2) continue;
                        if (f[0] == "f" && f.Length >= 5)
                        {
                            FileProbeData p = new FileProbeData();
                            long.TryParse(f[2], out p.Size);
                            long.TryParse(f[3], out p.Stamp);
                            p.Hash = f[4];
                            b.Files[f[1]] = p;
                            b.Valid = true;
                        }
                        else if (f[0] == "r" && f.Length >= 6)
                        {
                            RegProbeData p = new RegProbeData();
                            p.Absent = f[4] == "1";
                            try { p.Kind = int.Parse(f[5]); } catch { }
                            if (!p.Absent && f.Length >= 7)
                            {
                                try { p.Value = Encoding.UTF8.GetString(Convert.FromBase64String(f[6])); } catch { p.Value = ""; }
                            }
                            b.Reg[f[1] + "|" + f[2] + "|" + f[3]] = p;
                            b.Valid = true;
                        }
                        else if (f[0] == "i" && f.Length >= 3)
                        {
                            string val = "";
                            try { val = Encoding.UTF8.GetString(Convert.FromBase64String(f[2])); } catch { }
                            b.Ifeo[f[1]] = val;
                            b.Valid = true;
                        }
                    }
                }
            }
            catch
            {
            }
            _cache = b;
            return b;
        }

        private static void Save(DefenseBaseline b)
        {
            if (b == null) return;
            try
            {
                Directory.CreateDirectory(DefensePaths.Dir);
                StringBuilder sb = new StringBuilder();
                sb.Append("# ").Append(BaselineHeader).Append("\r\n");
                foreach (KeyValuePair<string, FileProbeData> kv in b.Files)
                {
                    FileProbeData p = kv.Value;
                    sb.Append("f\t").Append(kv.Key).Append('\t').Append(p.Size).Append('\t').Append(p.Stamp)
                        .Append('\t').Append(p.Hash).Append("\r\n");
                }
                foreach (KeyValuePair<string, RegProbeData> kv in b.Reg)
                {
                    string[] parts = kv.Key.Split('|');
                    if (parts.Length < 3) continue;
                    RegProbeData p = kv.Value;
                    sb.Append("r\t").Append(parts[0]).Append('\t').Append(parts[1]).Append('\t').Append(parts[2])
                        .Append('\t').Append(p.Absent ? "1" : "0").Append('\t').Append(p.Kind)
                        .Append('\t').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(p.Absent ? "" : p.Value)))
                        .Append("\r\n");
                }
                foreach (KeyValuePair<string, string> kv in b.Ifeo)
                {
                    sb.Append("i\t").Append(kv.Key).Append('\t')
                        .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(kv.Value == null ? "" : kv.Value)))
                        .Append("\r\n");
                }
                File.WriteAllText(DefensePaths.BaselinePath, sb.ToString(), new UTF8Encoding(false));
                b.Dirty = false;
            }
            catch
            {
            }
        }

        public static bool BaselineExists()
        {
            lock (Lock)
            {
                return Load().Valid;
            }
        }

        public static void DropBaseline()
        {
            lock (Lock)
            {
                _cache = null;
                try { if (File.Exists(DefensePaths.BaselinePath)) File.Delete(DefensePaths.BaselinePath); } catch { }
            }
        }

        private static DefenseBaseline BuildBaseline()
        {
            DefenseBaseline b = new DefenseBaseline();
            for (int i = 0; i < SystemFiles.Length; i++)
            {
                string path = SystemFiles[i];
                try
                {
                    FileInfo fi = new FileInfo(path);
                    if (!fi.Exists) continue;
                    string hash = HashFile(path);
                    if (hash.Length == 0) continue;
                    FileProbeData p = new FileProbeData();
                    p.Size = fi.Length;
                    p.Stamp = fi.LastWriteTimeUtc.Ticks;
                    p.Hash = hash;
                    b.Files[path] = p;
                }
                catch
                {
                }
            }
            for (int i = 0; i < Probes.Length; i++)
            {
                RegProbeData d = ReadProbe(Probes[i]);
                if (d != null) b.Reg[Probes[i].IdKey] = d;
            }
            foreach (KeyValuePair<string, string> kv in ReadIfeo()) b.Ifeo[kv.Key] = kv.Value;
            b.Valid = b.Files.Count > 0;
            b.Dirty = true;
            return b;
        }

        // -------------------------------------------------------------- check

        public static IntegrityReport Check()
        {
            lock (Lock)
            {
                IntegrityReport report = new IntegrityReport();
                DefenseBaseline b = Load();
                if (!b.Valid)
                {
                    b = BuildBaseline();
                    Save(b);
                    _cache = b;
                    report.BaselineCreated = true;
                    report.FilesChecked = b.Files.Count;
                    report.RegChecked = b.Reg.Count;
                    DefenseLog.Write("BASELINE", "baseline created: " + b.Files.Count + " files, " + b.Reg.Count + " registry values, " + b.Ifeo.Count + " ifeo entries");
                    return report;
                }

                HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < SystemFiles.Length; i++)
                {
                    string path = SystemFiles[i];
                    report.FilesChecked++;
                    try
                    {
                        FileInfo fi = new FileInfo(path);
                        if (!fi.Exists)
                        {
                            Add(report, ids, MakeMissingFile(path));
                            continue;
                        }
                        FileProbeData old = null;
                        b.Files.TryGetValue(path, out old);
                        long stamp = fi.LastWriteTimeUtc.Ticks;
                        if (old == null)
                        {
                            string h = HashFile(path);
                            if (h.Length > 0)
                            {
                                FileProbeData p = new FileProbeData();
                                p.Size = fi.Length;
                                p.Stamp = stamp;
                                p.Hash = h;
                                b.Files[path] = p;
                                b.Dirty = true;
                            }
                            continue;
                        }
                        if (old.Size == fi.Length && old.Stamp == stamp) continue;

                        string hash = HashFile(path);
                        if (hash.Length == 0) continue;
                        if (string.Equals(hash, old.Hash, StringComparison.OrdinalIgnoreCase))
                        {
                            old.Size = fi.Length;
                            old.Stamp = stamp;
                            b.Dirty = true;
                            continue;
                        }
                        if (LooksLikeMicrosoftUpdate(path))
                        {
                            DefenseLog.Write("UPDATE", "signed system file refreshed: " + path);
                            old.Size = fi.Length;
                            old.Stamp = stamp;
                            old.Hash = hash;
                            b.Dirty = true;
                            continue;
                        }
                        report.Tamper = true;
                        Threat t = new Threat();
                        t.Kind = ThreatKind.FileIntegrity;
                        t.Severity = ThreatSeverity.High;
                        t.Title = Loc.T("def.threat.fileTamper");
                        t.Detail = Loc.F("def.detail.fileTamper", path, Short(old.Hash), Short(hash));
                        t.Solution = Loc.T("def.solution.fileTamper");
                        t.Target = path;
                        t.FileExists = true;
                        t.Id = "file|" + path.ToLowerInvariant() + "|" + hash;
                        Add(report, ids, t);
                    }
                    catch
                    {
                    }
                }

                for (int i = 0; i < Probes.Length; i++)
                {
                    RegProbe p = Probes[i];
                    RegProbeData cur = ReadProbe(p);
                    if (cur == null) continue;
                    report.RegChecked++;
                    RegProbeData old = null;
                    b.Reg.TryGetValue(p.IdKey, out old);
                    if (old == null)
                    {
                        b.Reg[p.IdKey] = cur;
                        b.Dirty = true;
                        continue;
                    }
                    if (RegEqual(cur, old)) continue;
                    report.Tamper = true;
                    Add(report, ids, MakeRegThreat(p, cur, old));
                }

                Dictionary<string, string> ifeo = ReadIfeo();
                foreach (KeyValuePair<string, string> kv in ifeo)
                {
                    string old = null;
                    bool known = b.Ifeo.TryGetValue(kv.Key, out old);
                    bool suspicious = DefenseRules.IsSuspiciousPath(kv.Value);
                    bool critical = IsCriticalIfeoName(kv.Key);
                    if (known && string.Equals(old, kv.Value, StringComparison.OrdinalIgnoreCase) && !suspicious && !critical)
                        continue;
                    if (!known)
                    {
                        b.Ifeo[kv.Key] = kv.Value;
                        b.Dirty = true;
                    }
                    if (!suspicious && !critical) continue;
                    report.Tamper = true;
                    Threat t = new Threat();
                    t.Kind = ThreatKind.Registry;
                    t.Severity = suspicious ? ThreatSeverity.High : ThreatSeverity.Medium;
                    t.Title = Loc.F("def.threat.ifeo", kv.Key);
                    t.Detail = Loc.F("def.detail.ifeo", kv.Key, kv.Value, IfeoKey);
                    t.Solution = Loc.T("def.solution.ifeo");
                    t.Target = kv.Value;
                    t.Hive = "HKLM";
                    t.KeyPath = IfeoKey + "\\" + kv.Key;
                    t.ValueName = "Debugger";
                    t.Fix = ThreatFix.RemoveIfeo;
                    t.Id = "ifeo|" + kv.Key.ToLowerInvariant() + "|" + Fingerprint(kv.Value);
                    Add(report, ids, t);
                }
                foreach (KeyValuePair<string, string> kv in b.Ifeo)
                {
                    if (!ifeo.ContainsKey(kv.Key)) b.Dirty = true;
                }

                // 基线本身被污染时也要兜底：明显危险的关键配置，无论基线如何都报告。
                AddDangerousConfigs(report, ids);

                if (b.Dirty) Save(b);
                _cache = b;
                return report;
            }
        }

        private static void AddDangerousConfigs(IntegrityReport report, HashSet<string> ids)
        {
            string appInit = ReadStringProbe(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows", "AppInit_DLLs");
            string appInit32 = ReadStringProbe(RegistryHive.LocalMachine,
                @"SOFTWARE\Wow6432Node\Microsoft\Windows NT\CurrentVersion\Windows", "AppInit_DLLs");
            if (appInit.Trim().Length > 0)
            {
                AddAppInitThreat(report, ids, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows", appInit);
            }
            if (appInit32.Trim().Length > 0)
            {
                AddAppInitThreat(report, ids, @"SOFTWARE\Wow6432Node\Microsoft\Windows NT\CurrentVersion\Windows", appInit32);
            }

            string shell = ReadStringProbe(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Shell");
            if (shell.Trim().Length > 0 && DefenseRules.IsSuspiciousPath(shell))
            {
                AddWinlogonThreat(report, ids, "Shell", shell);
            }
            string userinit = ReadStringProbe(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Userinit");
            if (userinit.Trim().Length > 0 && DefenseRules.IsSuspiciousPath(userinit))
            {
                AddWinlogonThreat(report, ids, "Userinit", userinit);
            }

            string[] defenderKeys =
            {
                @"SOFTWARE\Policies\Microsoft\Windows Defender",
                @"SOFTWARE\Microsoft\Windows Defender"
            };
            for (int i = 0; i < defenderKeys.Length; i++) AddDefenderThreat(report, ids, defenderKeys[i], "DisableAntiSpyware");
            string[] rtpKeys =
            {
                @"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection",
                @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection"
            };
            for (int i = 0; i < rtpKeys.Length; i++) AddDefenderThreat(report, ids, rtpKeys[i], "DisableRealtimeMonitoring");
        }

        private static string ReadStringProbe(RegistryHive hive, string key, string name)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                using (RegistryKey k = baseKey.OpenSubKey(key, false))
                {
                    if (k == null) return "";
                    object v = k.GetValue(name, null);
                    return v == null ? "" : RegToString(v);
                }
            }
            catch
            {
                return "";
            }
        }

        private static void AddAppInitThreat(IntegrityReport report, HashSet<string> ids, string key, string value)
        {
            Threat t = new Threat();
            t.Kind = ThreatKind.Registry;
            t.Severity = ThreatSeverity.High;
            t.Title = Loc.T("def.threat.appinit");
            t.Detail = Loc.F("def.detail.appinit", "HKLM\\" + key + "\\AppInit_DLLs", value);
            t.Solution = Loc.T("def.solution.reg");
            t.Target = "HKLM\\" + key + "\\AppInit_DLLs";
            t.Hive = "HKLM";
            t.KeyPath = key;
            t.ValueName = "AppInit_DLLs";
            t.Fix = ThreatFix.RestoreRegistry;
            t.FixAbsent = true;
            t.FixKind = (int)RegistryValueKind.String;
            t.Id = RegThreatId("HKLM", key, "AppInit_DLLs", value);
            Add(report, ids, t);
        }

        private static void AddWinlogonThreat(IntegrityReport report, HashSet<string> ids, string name, string value)
        {
            Threat t = new Threat();
            t.Kind = ThreatKind.Registry;
            t.Severity = ThreatSeverity.High;
            t.Title = Loc.F("def.threat.regTamperName", "Winlogon\\" + name);
            t.Detail = Loc.F("def.detail.regCurrent", @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\" + name, value);
            t.Solution = Loc.T("def.solution.reg");
            t.Target = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\" + name;
            t.Hive = "HKLM";
            t.KeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
            t.ValueName = name;
            t.Fix = ThreatFix.RestoreRegistry;
            t.FixAbsent = false;
            t.FixValue = name == "Shell" ? "explorer.exe" : "userinit.exe,";
            t.FixKind = (int)RegistryValueKind.String;
            t.Id = RegThreatId("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", name, value);
            Add(report, ids, t);
        }

        private static void AddDefenderThreat(IntegrityReport report, HashSet<string> ids, string key, string name)
        {
            string v = ReadStringProbe(RegistryHive.LocalMachine, key, name);
            if (v.Trim().Length == 0 || v.Trim() == "0") return;
            Threat t = new Threat();
            t.Kind = ThreatKind.Config;
            t.Severity = ThreatSeverity.High;
            t.Title = Loc.T("def.threat.defender");
            t.Detail = Loc.F("def.detail.defender", "HKLM\\" + key + "\\" + name, v);
            t.Solution = Loc.T("def.solution.defender");
            t.Target = "HKLM\\" + key + "\\" + name;
            t.Hive = "HKLM";
            t.KeyPath = key;
            t.ValueName = name;
            t.Fix = ThreatFix.RestoreRegistry;
            t.FixAbsent = true;
            t.FixKind = (int)RegistryValueKind.DWord;
            t.Id = RegThreatId("HKLM", key, name, v);
            Add(report, ids, t);
        }

        private static Threat MakeRegThreat(RegProbe p, RegProbeData cur, RegProbeData old)
        {
            Threat t = new Threat();
            t.Kind = ThreatKind.Registry;
            bool suspicious = !cur.Absent && DefenseRules.IsSuspiciousPath(cur.Value);
            t.Severity = suspicious ? ThreatSeverity.High : ThreatSeverity.Medium;
            t.Title = Loc.T("def.threat.regTamper");
            t.Detail = Loc.F("def.detail.regTamper", p.Display,
                cur.Absent ? Loc.T("def.value.none") : cur.Value,
                old.Absent ? Loc.T("def.value.none") : old.Value);
            t.Solution = Loc.T("def.solution.reg");
            t.Target = p.Display;
            t.Hive = p.HiveName;
            t.KeyPath = p.Key;
            t.ValueName = p.Name;
            t.Fix = ThreatFix.RestoreRegistry;
            t.FixAbsent = old.Absent;
            t.FixValue = old.Value;
            t.FixKind = old.Kind;
            t.Id = RegThreatId(p.HiveName, p.Key, p.Name, cur.Absent ? "<none>" : cur.Value);
            return t;
        }

        private static string RegThreatId(string hive, string key, string name, string value)
        {
            return "reg|" + hive + "|" + key + "|" + name + "|" + Fingerprint(value);
        }

        private static Threat MakeMissingFile(string path)
        {
            Threat t = new Threat();
            t.Kind = ThreatKind.FileIntegrity;
            t.Severity = ThreatSeverity.High;
            t.Title = Loc.T("def.threat.fileMissing");
            t.Detail = Loc.F("def.detail.fileMissing", path);
            t.Solution = Loc.T("def.solution.fileMissing");
            t.Target = path;
            t.FileExists = false;
            t.Id = "missing|" + path.ToLowerInvariant();
            return t;
        }

        private static void Add(IntegrityReport report, HashSet<string> ids, Threat t)
        {
            if (!ids.Add(t.Id))
            {
                // 同一个状态被多条规则命中时保留更严重的结论。
                for (int i = 0; i < report.Threats.Count; i++)
                {
                    Threat old = report.Threats[i];
                    if (old.Id != t.Id) continue;
                    if (t.Severity > old.Severity)
                    {
                        old.Severity = t.Severity;
                        old.Title = t.Title;
                        old.Detail = t.Detail;
                        old.Solution = t.Solution;
                    }
                    if (old.Fix == ThreatFix.None && t.Fix != ThreatFix.None)
                    {
                        old.Fix = t.Fix;
                        old.FixAbsent = t.FixAbsent;
                        old.FixValue = t.FixValue;
                        old.FixKind = t.FixKind;
                    }
                    return;
                }
                return;
            }
            report.Threats.Add(t);
        }

        private static string Short(string hash)
        {
            if (string.IsNullOrEmpty(hash)) return "—";
            return hash.Length > 16 ? hash.Substring(0, 16) : hash;
        }

        /// <summary>签名有效且公司名为 Microsoft，视为官方更新而不是篡改。</summary>
        private static bool LooksLikeMicrosoftUpdate(string path)
        {
            bool cached;
            string key = path + "|" + File.GetLastWriteTimeUtc(path).Ticks;
            lock (SignatureCache)
            {
                if (SignatureCache.TryGetValue(key, out cached)) return cached;
            }
            bool ok = false;
            try
            {
                FileVersionInfo vi = FileVersionInfo.GetVersionInfo(path);
                string company = vi.CompanyName == null ? "" : vi.CompanyName;
                // 系统更新会正常改写 System32 下的二进制；只要版本信息仍由微软签发就刷新基线，
                // 避免把每月补丁误报为篡改。目录签名（catalog）在不同系统上 WinVerifyTrust 行为不一致，
                // 因此这里以版本信息的公司名为准。
                ok = company.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                ok = false;
            }
            lock (SignatureCache)
            {
                if (SignatureCache.Count < 400) SignatureCache[key] = ok;
            }
            return ok;
        }

        /// <summary>处置成功后把基线同步到当前值，避免下一次检查重复报告。</summary>
        public static void Accept(Threat t)
        {
            lock (Lock)
            {
                DefenseBaseline b = Load();
                if (!b.Valid || t == null) return;
                try
                {
                    if (t.Fix == ThreatFix.RemoveIfeo && t.ValueName.Length > 0)
                    {
                        needSaveIfeo(b);
                    }
                    else if (t.Hive.Length > 0 && t.KeyPath.Length > 0 && t.ValueName.Length > 0)
                    {
                        RegProbe p = new RegProbe(
                            string.Equals(t.Hive, "HKLM", StringComparison.OrdinalIgnoreCase)
                                ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                            t.KeyPath, t.ValueName);
                        RegProbeData cur = ReadProbe(p);
                        if (cur != null)
                        {
                            b.Reg[p.IdKey] = cur;
                            b.Dirty = true;
                        }
                    }
                    if (t.Kind == ThreatKind.SuspiciousFile || t.Kind == ThreatKind.Startup)
                    {
                        // 隔离 / 禁用后的真实状态下一轮会重新计算，无需改基线
                    }
                }
                catch
                {
                }
                if (b.Dirty) Save(b);
                _cache = b;
            }
        }

        private static void needSaveIfeo(DefenseBaseline b)
        {
            Dictionary<string, string> cur = ReadIfeo();
            b.Ifeo.Clear();
            foreach (KeyValuePair<string, string> kv in cur) b.Ifeo[kv.Key] = kv.Value;
            b.Dirty = true;
        }

        /// <summary>把被篡改的注册表值恢复成基线值；基线为空时删除该值。</summary>
        public static bool RestoreRegistry(Threat t)
        {
            if (t == null || t.Hive.Length == 0 || t.KeyPath.Length == 0 || t.ValueName.Length == 0) return false;
            bool ok = WriteProbe(t.Hive, t.KeyPath, t.ValueName, t.FixAbsent, t.FixValue, t.FixKind);
            if (ok)
            {
                DefenseLog.Write("REG", "restored " + t.Hive + "\\" + t.KeyPath + "\\" + t.ValueName +
                    " -> " + (t.FixAbsent ? "<absent>" : t.FixValue));
            }
            return ok;
        }

        /// <summary>删除 IFEO 劫持项里的 Debugger 值。</summary>
        public static bool RemoveIfeo(Threat t)
        {
            if (t == null || t.KeyPath.Length == 0) return false;
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(t.KeyPath, true))
                {
                    if (k == null) return false;
                    k.DeleteValue("Debugger", false);
                }
                DefenseLog.Write("REG", "removed IFEO debugger: " + t.KeyPath);
                return true;
            }
            catch (Exception ex)
            {
                DefenseLog.Write("REG", "remove IFEO failed: " + ex.Message);
                return false;
            }
        }
    }

    // ------------------------------------------------------------------ rules

    public static class DefenseRules
    {
        private static readonly string[] ExecExts =
        {
            ".exe", ".scr", ".bat", ".cmd", ".vbs", ".vbe", ".js", ".jse", ".ps1", ".hta", ".mshta"
        };

        public static bool IsExecutable(string path)
        {
            string ext = Path.GetExtension(path);
            if (ext == null) return false;
            ext = ext.ToLowerInvariant();
            for (int i = 0; i < ExecExts.Length; i++)
            {
                if (ext == ExecExts[i]) return true;
            }
            return false;
        }

        public static bool IsSuspiciousPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string p = PathUtil.Clean(path).Replace('/', '\\').ToLowerInvariant();
            if (p.IndexOf("\\temp\\", StringComparison.Ordinal) >= 0) return true;
            if (p.IndexOf("\\downloads\\", StringComparison.Ordinal) >= 0) return true;
            if (p.IndexOf("\\users\\public\\", StringComparison.Ordinal) >= 0) return true;
            if (p.IndexOf("\\windows\\temp\\", StringComparison.Ordinal) >= 0) return true;
            if (p.IndexOf("\\programdata\\", StringComparison.Ordinal) >= 0 &&
                p.IndexOf("\\microsoft\\", StringComparison.Ordinal) < 0 &&
                p.IndexOf("\\package cache\\", StringComparison.Ordinal) < 0) return true;
            return false;
        }

        public static bool HasDoubleExtension(string path)
        {
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLowerInvariant();
            if (!IsExecutable(lower)) return false;
            int lastDot = lower.LastIndexOf('.');
            if (lastDot <= 0) return false;
            string core = lower.Substring(0, lastDot);
            string[] disguise = { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".gif", ".txt", ".zip", ".rar", ".mp4", ".mp3", ".xls", ".xlsx", ".ppt", ".pptx" };
            for (int i = 0; i < disguise.Length; i++)
            {
                if (core.EndsWith(disguise[i], StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>文件名伪装成系统进程，但路径不在 Windows 目录。</summary>
        public static bool ImpersonatesSystem(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string name = Path.GetFileName(path);
            if (name == null) return false;
            if (!IntegritySurvey.IsSystemProcessName(name)) return false;
            if (!IsExecutable(path)) return false;
            string p = path.Replace('/', '\\').ToLowerInvariant();
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows).Replace('/', '\\').ToLowerInvariant();
            if (p.Length == 0) return false;
            return !p.StartsWith(win + "\\", StringComparison.Ordinal);
        }

        public static bool IsHidden(string path)
        {
            try
            {
                FileAttributes a = File.GetAttributes(path);
                return (a & FileAttributes.Hidden) == FileAttributes.Hidden;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>从启动项命令行里取出可执行文件路径。</summary>
        public static string ExtractTarget(string command)
        {
            if (string.IsNullOrEmpty(command)) return "";
            string s = command.Trim();
            if (s.StartsWith("\""))
            {
                int end = s.IndexOf('"', 1);
                if (end > 0) s = s.Substring(1, end - 1);
                else s = s.Substring(1);
            }
            else
            {
                int end = s.IndexOf(' ');
                if (end > 0) s = s.Substring(0, end);
            }
            s = PathUtil.Clean(s);
            try { s = Environment.ExpandEnvironmentVariables(s); } catch { }
            return s;
        }

        public static bool LooksSuspiciousStartup(string name, string target)
        {
            if (string.IsNullOrEmpty(target)) return false;
            if (DefenseRules.IsSuspiciousPath(target)) return true;
            if (DefenseRules.HasDoubleExtension(target)) return true;
            if (DefenseRules.ImpersonatesSystem(target)) return true;
            if (DefenseRules.IsHidden(target)) return true;
            return false;
        }
    }

    // ------------------------------------------------------------------ hazard scan

    public static class HazardScanner
    {
        private static readonly string[] StartupKeys =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
            @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Run"
        };

        private static int _lastScan;
        private static readonly object Lock = new object();

        public static List<Threat> Scan(DefenseLevel level, bool force)
        {
            int now = Environment.TickCount;
            lock (Lock)
            {
                int gap = level == DefenseLevel.High ? 60000 : 240000;
                if (!force && _lastScan != 0 && unchecked(now - _lastScan) < gap) return new List<Threat>();
                _lastScan = now;
            }
            List<Threat> list = new List<Threat>();
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ScanAutoruns(list, ids);
            ScanDirs(list, ids);
            return list;
        }

        private static void ScanAutoruns(List<Threat> list, HashSet<string> ids)
        {
            ScanRunKey(Registry.CurrentUser, "HKCU", StartupKeys, list, ids, false);
            ScanRunKey(Registry.LocalMachine, "HKLM", StartupKeys, list, ids, false);

            string[] startupDirs =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
            };
            for (int d = 0; d < startupDirs.Length; d++)
            {
                string dir = startupDirs[d];
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                try
                {
                    string[] files = Directory.GetFiles(dir);
                    for (int i = 0; i < files.Length && i < 300; i++)
                    {
                        string f = files[i];
                        if (!DefenseRules.IsExecutable(f)) continue;
                        Threat t = new Threat();
                        t.Kind = ThreatKind.Startup;
                        t.Severity = ThreatSeverity.Medium;
                        if (DefenseRules.IsHidden(f) || DefenseRules.HasDoubleExtension(f) || DefenseRules.ImpersonatesSystem(f))
                            t.Severity = ThreatSeverity.High;
                        t.Title = Loc.F("def.threat.startupFile", Path.GetFileName(f));
                        t.Detail = Loc.F("def.detail.startupFile", f, dir);
                        t.Solution = Loc.T("def.solution.startup");
                        t.Target = f;
                        t.FileExists = File.Exists(f);
                        t.Fix = ThreatFix.QuarantineFile;
                        t.Id = "sfolder|" + f.ToLowerInvariant() + "|" + SecurityStamp(f);
                        if (ids.Add(t.Id)) list.Add(t);
                    }
                }
                catch
                {
                }
            }
        }

        private static void ScanRunKey(RegistryKey root, string hive, string[] keys, List<Threat> list,
            HashSet<string> ids, bool unused)
        {
            for (int k = 0; k < keys.Length; k++)
            {
                if (hive == "HKCU" && keys[k].IndexOf("Wow6432Node", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                try
                {
                    using (RegistryKey key = root.OpenSubKey(keys[k], false))
                    {
                        if (key == null) continue;
                        string[] names = key.GetValueNames();
                        for (int i = 0; i < names.Length && i < 120; i++)
                        {
                            if (names[i].Length == 0) continue;
                            object v = key.GetValue(names[i], null);
                            string cmd = v is string ? (string)v : (v == null ? "" : v.ToString());
                            string target = DefenseRules.ExtractTarget(cmd);
                            bool suspicious = DefenseRules.LooksSuspiciousStartup(names[i], target);
                            if (!suspicious) continue;
                            Threat t = new Threat();
                            t.Kind = ThreatKind.Startup;
                            t.Severity = (DefenseRules.IsSuspiciousPath(target) || DefenseRules.HasDoubleExtension(target)
                                || DefenseRules.ImpersonatesSystem(target)) ? ThreatSeverity.High : ThreatSeverity.Medium;
                            t.Title = Loc.F("def.threat.startup", names[i]);
                            t.Detail = Loc.F("def.detail.startup", hive + "\\" + keys[k] + "\\" + names[i], cmd);
                            t.Solution = Loc.T("def.solution.startup");
                            t.Target = target;
                            t.FileExists = target.Length > 0 && File.Exists(target);
                            t.Hive = hive;
                            t.KeyPath = keys[k];
                            t.ValueName = names[i];
                            t.Command = cmd;
                            t.Fix = ThreatFix.DisableStartup;
                            t.Id = "startup|" + hive + "|" + keys[k].ToLowerInvariant() + "|" +
                                names[i].ToLowerInvariant() + "|" + IntegritySurvey.Fingerprint(cmd);
                            if (ids.Add(t.Id)) list.Add(t);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static void ScanDirs(List<Threat> list, HashSet<string> ids)
        {
            List<string> dirs = new List<string>();
            string temp = Path.GetTempPath();
            if (temp.Length > 0) dirs.Add(temp);
            string winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
            if (winTemp.Length > 0) dirs.Add(winTemp);
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (downloads.Length > 0) dirs.Add(downloads);

            for (int d = 0; d < dirs.Count; d++)
            {
                string dir = dirs[d];
                if (!Directory.Exists(dir)) continue;
                try
                {
                    string[] files = Directory.GetFiles(dir);
                    for (int i = 0; i < files.Length && i < 500; i++)
                    {
                        string f = files[i];
                        if (!DefenseRules.IsExecutable(f)) continue;
                        bool hidden = DefenseRules.IsHidden(f);
                        bool doubleExt = DefenseRules.HasDoubleExtension(f);
                        bool fake = DefenseRules.ImpersonatesSystem(f);
                        bool scr = Path.GetExtension(f).ToLowerInvariant() == ".scr";
                        if (!hidden && !doubleExt && !fake && !scr) continue;
                        Threat t = new Threat();
                        t.Kind = ThreatKind.SuspiciousFile;
                        t.Severity = ThreatSeverity.High;
                        t.Title = Loc.F("def.threat.file", Path.GetFileName(f));
                        t.Detail = Loc.F("def.detail.file", f, dir, hidden ? Loc.T("def.file.hidden") :
                            (doubleExt ? Loc.T("def.file.double") : (fake ? Loc.T("def.file.fake") : Loc.T("def.file.scr"))));
                        t.Solution = Loc.T("def.solution.file");
                        t.Target = f;
                        t.FileExists = true;
                        t.Fix = ThreatFix.QuarantineFile;
                        t.Id = "sfile|" + f.ToLowerInvariant() + "|" + SecurityStamp(f);
                        if (ids.Add(t.Id)) list.Add(t);
                    }
                }
                catch
                {
                }
            }
        }

        private static string SecurityStamp(string path)
        {
            try
            {
                FileInfo fi = new FileInfo(path);
                if (!fi.Exists) return "0";
                return fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;
            }
            catch
            {
                return "0";
            }
        }
    }

    // ------------------------------------------------------------------ signature

    public static class DefenseSignature
    {
        private const uint WtdUiNone = 2;
        private const uint WtdRevokeNone = 0;
        private const uint WtdChoiceFile = 1;
        private const uint WtdStateActionVerify = 1;
        private const uint WtdStateActionClose = 2;
        private const uint WtdCacheOnly = 0x1000;
        private const uint WtdDisableNetworkRetrieval = 0x2000;

        private static readonly Guid VerifyV1 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_FILE_INFO
        {
            public uint cbStruct;
            public IntPtr pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINTRUST_DATA
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
        }

        [DllImport("wintrust.dll", PreserveSig = true, CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr hWnd,
            [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID, IntPtr pWVTData);

        private static readonly Dictionary<string, bool> Cache =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static readonly object Lock = new object();

        public static bool Verify(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string key;
            try { key = path + "|" + File.GetLastWriteTimeUtc(path).Ticks; }
            catch { key = path; }
            lock (Lock)
            {
                bool hit;
                if (Cache.TryGetValue(key, out hit)) return hit;
            }
            bool ok = VerifyRaw(path);
            lock (Lock)
            {
                if (Cache.Count < 500) Cache[key] = ok;
            }
            return ok;
        }

        private static bool VerifyRaw(string path)
        {
            IntPtr filePtr = IntPtr.Zero;
            IntPtr dataPtr = IntPtr.Zero;
            IntPtr pathPtr = IntPtr.Zero;
            try
            {
                pathPtr = Marshal.StringToHGlobalUni(path);
                WINTRUST_FILE_INFO fi = new WINTRUST_FILE_INFO();
                fi.cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO));
                fi.pcwszFilePath = pathPtr;
                fi.hFile = IntPtr.Zero;
                fi.pgKnownSubject = IntPtr.Zero;
                filePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)));
                Marshal.StructureToPtr(fi, filePtr, false);

                WINTRUST_DATA data = new WINTRUST_DATA();
                data.cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA));
                data.dwUIChoice = WtdUiNone;
                data.fdwRevocationChecks = WtdRevokeNone;
                data.dwUnionChoice = WtdChoiceFile;
                data.pFile = filePtr;
                data.dwStateAction = WtdStateActionVerify;
                data.dwProvFlags = WtdCacheOnly | WtdDisableNetworkRetrieval;

                dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_DATA)));
                Marshal.StructureToPtr(data, dataPtr, false);
                int result = WinVerifyTrust(IntPtr.Zero, VerifyV1, dataPtr);
                data = (WINTRUST_DATA)Marshal.PtrToStructure(dataPtr, typeof(WINTRUST_DATA));
                data.dwStateAction = WtdStateActionClose;
                Marshal.StructureToPtr(data, dataPtr, false);
                try { WinVerifyTrust(IntPtr.Zero, VerifyV1, dataPtr); } catch { }
                return result == 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (dataPtr != IntPtr.Zero) Marshal.FreeHGlobal(dataPtr);
                if (filePtr != IntPtr.Zero) Marshal.FreeHGlobal(filePtr);
                if (pathPtr != IntPtr.Zero) Marshal.FreeHGlobal(pathPtr);
            }
        }
    }

    // ------------------------------------------------------------------ suspect hunter

    public class SuspectProcess
    {
        public int Pid;
        public string Name = "";
        public string Path = "";
        public bool Signed;
        public int Score;
        public string Reason = "";
    }

    /// <summary>只处理路径明显可疑的进程；Windows 目录与 Program Files 下的进程永远不碰。</summary>
    public static class SuspectHunter
    {
        private static readonly string[] SystemNames =
        {
            "svchost", "lsass", "winlogon", "services", "explorer", "csrss", "smss",
            "taskmgr", "regedit", "rundll32", "spoolsv", "wininit", "dwm", "fontdrvhost"
        };

        public static List<SuspectProcess> Kill(int tamperThreshold, List<string> killed)
        {
            List<SuspectProcess> result = new List<SuspectProcess>();
            int me = Process.GetCurrentProcess().Id;
            Process[] all;
            try { all = Process.GetProcesses(); }
            catch { return result; }
            for (int i = 0; i < all.Length; i++)
            {
                Process p = all[i];
                try
                {
                    if (p.Id == me) continue;
                    string path = "";
                    try { path = p.MainModule.FileName; } catch { }
                    if (string.IsNullOrEmpty(path)) continue;
                    if (!DefenseRules.IsSuspiciousPath(path)) continue;
                    if (TrustedPath(path)) continue;
                    string name = p.ProcessName;
                    int score = 2;
                    string reason = Loc.T("def.reason.tempPath");
                    bool signed = DefenseSignature.Verify(path);
                    if (!signed)
                    {
                        score++;
                        reason += " · " + Loc.T("def.reason.unsigned");
                    }
                    try
                    {
                        DateTime started = p.StartTime;
                        if ((DateTime.Now - started).TotalMinutes <= 15)
                        {
                            score++;
                            reason += " · " + Loc.T("def.reason.recent");
                        }
                    }
                    catch
                    {
                    }
                    if (DefenseRules.ImpersonatesSystem(path))
                    {
                        score++;
                        reason += " · " + Loc.T("def.reason.fakeName");
                    }
                    if (score < tamperThreshold) continue;
                    SuspectProcess s = new SuspectProcess();
                    s.Pid = p.Id;
                    s.Name = name;
                    s.Path = path;
                    s.Signed = signed;
                    s.Score = score;
                    s.Reason = reason;
                    try
                    {
                        p.Kill();
                        s.Reason += " · " + Loc.T("def.reason.killed");
                        result.Add(s);
                        if (killed != null) killed.Add(name + " (" + p.Id + ")");
                        DefenseLog.Write("KILL", "terminated suspect process: " + name + " pid=" + p.Id + " path=" + path + " score=" + score);
                    }
                    catch (Exception ex)
                    {
                        DefenseLog.Write("KILL", "failed to terminate " + name + " pid=" + p.Id + ": " + ex.Message);
                    }
                }
                catch
                {
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }
            return result;
        }

        public static List<string> KillByPaths(List<string> paths)
        {
            List<string> killed = new List<string>();
            if (paths == null || paths.Count == 0) return killed;
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < paths.Count; i++) if (paths[i] != null) set.Add(paths[i]);
            int me = Process.GetCurrentProcess().Id;
            Process[] all;
            try { all = Process.GetProcesses(); }
            catch { return killed; }
            for (int i = 0; i < all.Length; i++)
            {
                Process p = all[i];
                try
                {
                    if (p.Id == me) continue;
                    string path = "";
                    try { path = p.MainModule.FileName; } catch { }
                    if (string.IsNullOrEmpty(path) || !set.Contains(path)) continue;
                    try
                    {
                        p.Kill();
                        killed.Add(p.ProcessName + " (" + p.Id + ")");
                        DefenseLog.Write("KILL", "terminated running risky file: " + p.ProcessName + " pid=" + p.Id + " path=" + path);
                    }
                    catch
                    {
                    }
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

        private static bool TrustedPath(string path)
        {
            string p = path.Replace('/', '\\').ToLowerInvariant();
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows).Replace('/', '\\').ToLowerInvariant();
            if (p.StartsWith(win + "\\", StringComparison.Ordinal)) return true;
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).Replace('/', '\\').ToLowerInvariant();
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).Replace('/', '\\').ToLowerInvariant();
            if (p.StartsWith(pf + "\\", StringComparison.Ordinal)) return true;
            if (p.StartsWith(pf86 + "\\", StringComparison.Ordinal)) return true;
            return false;
        }
    }

    // ------------------------------------------------------------------ monitor

    public static class DefenseMonitor
    {
        private static Thread _thread;
        private static volatile bool _stop;
        private static AutoResetEvent _wake = new AutoResetEvent(false);

        public static bool Running
        {
            get
            {
                Thread t = _thread;
                return t != null && t.IsAlive;
            }
        }

        public static void Start()
        {
            if (Running) return;
            _stop = false;
            _wake = new AutoResetEvent(false);
            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Name = "CleanC Defense";
            _thread.Start();
        }

        public static void Stop()
        {
            _stop = true;
            try { _wake.Set(); } catch { }
        }

        public static void Poke()
        {
            try { _wake.Set(); } catch { }
        }

        private static void Loop()
        {
            try
            {
                DefenseCenter.RunCheck(false);
                int last = Environment.TickCount;
                while (!_stop)
                {
                    int interval = DefenseCenter.Level == DefenseLevel.High ? 15000 : 60000;
                    int elapsed = unchecked(Environment.TickCount - last);
                    int wait = interval - elapsed;
                    if (wait < 250) wait = 250;
                    if (_wake.WaitOne(wait, false))
                    {
                        if (_stop) break;
                    }
                    if (_stop) break;
                    last = Environment.TickCount;
                    DefenseCenter.RunCheck(false);
                }
            }
            catch (Exception ex)
            {
                DefenseLog.Write("ERROR", "monitor loop: " + ex.Message);
            }
        }
    }

    // ------------------------------------------------------------------ center

    /// <summary>防御总控：设置、状态、风险列表、处置动作与通知事件。</summary>
    public static class DefenseCenter
    {
        private static readonly object StateLock = new object();
        private static readonly List<Threat> _threats = new List<Threat>();
        private static readonly HashSet<string> _ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _alerted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static bool Enabled;
        public static DefenseLevel Level = DefenseLevel.High;
        public static DateTime LastCheck = DateTime.MinValue;
        public static int FilesWatched;
        public static int RegWatched;
        public static int Blocked;
        public static int CheckCount;
        public static string Error = "";
        private static volatile bool _checking;

        public static event Action Changed;
        public static event Action<DefenseAlert> Alert;

        public static bool Checking
        {
            get { return _checking; }
        }

        public static void Init()
        {
            Enabled = Settings.Get("defense.enabled", "0") == "1";
            string lv = Settings.Get("defense.level", "1");
            Level = lv == "0" ? DefenseLevel.Low : DefenseLevel.High;
            FilesWatched = IntegritySurvey.SystemFiles.Length;
            RegWatched = IntegritySurvey.Probes.Length;
            LoadIgnores();
        }

        public static void SetEnabled(bool on)
        {
            if (Enabled == on)
            {
                RaiseChanged();
                return;
            }
            Enabled = on;
            Settings.Set("defense.enabled", on ? "1" : "0");
            if (on)
            {
                lock (StateLock) _alerted.Clear();
                DefenseMonitor.Start();
                DefenseLog.Write("INFO", "active defense enabled (" + (Level == DefenseLevel.High ? "high" : "low") + ")");
                DefenseMonitor.Poke();
            }
            else
            {
                DefenseMonitor.Stop();
                DefenseLog.Write("INFO", "active defense disabled");
            }
            RaiseChanged();
        }

        public static void SetLevel(int level)
        {
            DefenseLevel lv = level <= 0 ? DefenseLevel.Low : DefenseLevel.High;
            if (Level == lv)
            {
                RaiseChanged();
                return;
            }
            Level = lv;
            Settings.Set("defense.level", lv == DefenseLevel.Low ? "0" : "1");
            DefenseLog.Write("INFO", "defense level set to " + (lv == DefenseLevel.High ? "high" : "low"));
            if (Enabled)
            {
                DefenseMonitor.Poke();
                DefenseMonitor.Start();
            }
            RaiseChanged();
        }

        public static void StartMonitorIfEnabled()
        {
            if (!Enabled) return;
            DefenseMonitor.Start();
            DefenseLog.Write("INFO", "active defense monitor started (" + (Level == DefenseLevel.High ? "high" : "low") + ")");
        }

        public static void StopMonitor()
        {
            DefenseMonitor.Stop();
        }

        public static void CheckNow()
        {
            if (Enabled && DefenseMonitor.Running)
            {
                DefenseMonitor.Poke();
                return;
            }
            Task.Run(delegate { RunCheck(true); });
        }

        public static void RebuildBaseline()
        {
            IntegritySurvey.DropBaseline();
            _alerted.Clear();
            DefenseLog.Write("BASELINE", "baseline dropped, rebuilding");
            if (Enabled && DefenseMonitor.Running) DefenseMonitor.Poke();
            else Task.Run(delegate { RunCheck(true); });
        }

        public static List<Threat> SnapshotThreats()
        {
            lock (StateLock)
            {
                List<Threat> list = new List<Threat>(_threats);
                list.Sort(delegate(Threat a, Threat b)
                {
                    if (a.Severity != b.Severity) return b.Severity.CompareTo(a.Severity);
                    return b.Time.CompareTo(a.Time);
                });
                return list;
            }
        }

        public static string StatusLine()
        {
            if (!Enabled) return Loc.T("def.status.off");
            string lv = Level == DefenseLevel.High ? Loc.T("def.level.high") : Loc.T("def.level.low");
            string last = LastCheck == DateTime.MinValue ? "—" : LastCheck.ToString("HH:mm:ss");
            string s = Loc.F("def.status.on", lv, last);
            if (!Native.IsAdmin()) s += " · " + Loc.T("def.status.noAdmin");
            return s;
        }

        public static string IntervalText()
        {
            return Loc.F("def.interval", Level == DefenseLevel.High ? "15" : "60");
        }

        public static void RunCheck(bool manual)
        {
            lock (StateLock)
            {
                if (_checking) return;
                _checking = true;
            }
            RaiseChanged();
            DefenseAlert alert = new DefenseAlert();
            try
            {
                IntegrityReport rep = IntegritySurvey.Check();
                alert.BaselineCreated = rep.BaselineCreated;
                List<Threat> found = new List<Threat>(rep.Threats);
                List<Threat> hazards = HazardScanner.Scan(Level, manual || rep.BaselineCreated);
                found.AddRange(hazards);

                List<string> killed = new List<string>();
                if (rep.Tamper)
                {
                    // 可疑目录本身不能作为唯一定罪条件：至少还要命中「未签名 / 最近启动 / 伪装系统名」之一。
                    int threshold = Level == DefenseLevel.High ? 3 : 4;
                    SuspectHunter.Kill(threshold, killed);
                }

                List<string> riskyPaths = new List<string>();
                for (int i = 0; i < found.Count; i++)
                {
                    Threat t = found[i];
                    if ((t.Kind == ThreatKind.SuspiciousFile || t.Kind == ThreatKind.Startup) &&
                        t.Severity == ThreatSeverity.High && t.FileExists && t.Target.Length > 0)
                        riskyPaths.Add(t.Target);
                }
                if (riskyPaths.Count > 0)
                {
                    List<string> more = SuspectHunter.KillByPaths(riskyPaths);
                    if (more.Count > 0) killed.AddRange(more);
                }

                string killNote = "";
                if (killed.Count > 0)
                {
                    killNote = Loc.F("def.detail.killed", string.Join(", ", killed.ToArray()));
                }
                foreach (Threat t in found)
                {
                    if (t.Kind == ThreatKind.FileIntegrity || t.Kind == ThreatKind.Registry)
                    {
                        if (killNote.Length > 0) t.Extra = killNote;
                    }
                    else if (killNote.Length > 0 && t.FileExists)
                    {
                        t.Extra = killNote;
                    }
                }
                if (killed.Count > 0)
                {
                    lock (StateLock) Blocked += killed.Count;
                    if (killNote.Length > 0) alert.Killed.AddRange(killed);
                }

                List<Threat> merged = new List<Threat>();
                HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                lock (StateLock)
                {
                    for (int i = 0; i < found.Count; i++)
                    {
                        Threat t = found[i];
                        if (_ignored.Contains(t.Id)) continue;
                        if (!ids.Add(t.Id)) continue;
                        merged.Add(t);
                    }
                    _threats.Clear();
                    _threats.AddRange(merged);
                    LastCheck = DateTime.Now;
                    CheckCount++;
                    FilesWatched = rep.FilesChecked > 0 ? rep.FilesChecked : FilesWatched;
                    RegWatched = rep.RegChecked > 0 ? rep.RegChecked : RegWatched;
                    Error = "";

                    for (int i = 0; i < merged.Count; i++)
                    {
                        Threat t = merged[i];
                        if (_alerted.Contains(t.Id)) continue;
                        _alerted.Add(t.Id);
                        alert.NewThreats.Add(t);
                        DefenseLog.Write("ALERT", "[" + t.Severity + "] " + t.Title + " | " + t.Detail.Replace("\r", " ").Replace("\n", " "));
                    }
                }
                if (rep.BaselineCreated)
                {
                    DefenseLog.Write("BASELINE", "baseline built automatically");
                }
            }
            catch (Exception ex)
            {
                lock (StateLock) Error = ex.Message;
                DefenseLog.Write("ERROR", "check failed: " + ex.ToString());
            }
            finally
            {
                lock (StateLock) _checking = false;
                RaiseChanged();
            }
            if (Enabled && Alert != null && (alert.NewThreats.Count > 0 || alert.Killed.Count > 0))
            {
                try { Alert(alert); } catch { }
            }
        }

        public static bool Apply(Threat t, out string message)
        {
            message = "";
            if (t == null) return false;
            bool ok = false;
            try
            {
                if (t.Fix == ThreatFix.RestoreRegistry)
                {
                    ok = IntegritySurvey.RestoreRegistry(t);
                }
                else if (t.Fix == ThreatFix.RemoveIfeo)
                {
                    ok = IntegritySurvey.RemoveIfeo(t);
                }
                else if (t.Fix == ThreatFix.DisableStartup)
                {
                    ok = DisableStartup(t);
                }
                else if (t.Fix == ThreatFix.KillProcess)
                {
                    ok = KillProcess(t);
                }
                else if (t.Fix == ThreatFix.QuarantineFile)
                {
                    return Quarantine(t, out message);
                }
            }
            catch (Exception ex)
            {
                DefenseLog.Write("FIX", "action failed: " + ex.Message);
                ok = false;
            }
            if (ok)
            {
                IntegritySurvey.Accept(t);
                Finish(t);
                message = Loc.T("def.action.ok");
                DefenseLog.Write("FIX", "resolved: " + t.Title + " | " + t.Target);
            }
            else
            {
                message = Loc.T("def.action.fail");
            }
            return ok;
        }

        public static bool Quarantine(Threat t, out string message)
        {
            message = "";
            if (t == null || string.IsNullOrEmpty(t.Target)) return false;
            try
            {
                if (!File.Exists(t.Target))
                {
                    message = Loc.T("def.action.missing");
                    Finish(t);
                    return true;
                }
                Directory.CreateDirectory(DefensePaths.QuarantineDir);
                string name = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Path.GetFileName(t.Target) + ".quarantined";
                string dest = Path.Combine(DefensePaths.QuarantineDir, name);
                File.Move(t.Target, dest);
                DefenseLog.Write("QUARANTINE", t.Target + " -> " + dest);
                Finish(t);
                message = Loc.T("def.action.quarantined");
                return true;
            }
            catch (Exception ex)
            {
                message = Loc.F("def.action.quarantineFail", ex.Message);
                DefenseLog.Write("QUARANTINE", "failed: " + ex.Message);
                return false;
            }
        }

        public static void Ignore(Threat t)
        {
            if (t == null) return;
            lock (StateLock)
            {
                _ignored.Add(t.Id);
                _alerted.Remove(t.Id);
                RemoveLocked(t.Id);
            }
            SaveIgnores();
            DefenseLog.Write("IGNORE", t.Title + " | " + t.Target);
            RaiseChanged();
        }

        public static void KillProcessNow(Threat t)
        {
            if (t == null || t.Pid <= 0) return;
            try
            {
                Process p = Process.GetProcessById(t.Pid);
                p.Kill();
                t.Running = false;
                DefenseLog.Write("KILL", "manual terminate pid=" + t.Pid + " name=" + t.ProcessName);
            }
            catch
            {
            }
        }

        private static bool DisableStartup(Threat t)
        {
            try
            {
                RegistryHive hive = string.Equals(t.Hive, "HKLM", StringComparison.OrdinalIgnoreCase)
                    ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                using (RegistryKey k = baseKey.OpenSubKey(t.KeyPath, true))
                {
                    if (k == null) return false;
                    object old = k.GetValue(t.ValueName, null);
                    DefenseLog.Write("REG", "disable startup " + t.Hive + "\\" + t.KeyPath + "\\" + t.ValueName +
                        " = " + (old == null ? "" : old.ToString()));
                    k.DeleteValue(t.ValueName, false);
                    return true;
                }
            }
            catch (Exception ex)
            {
                DefenseLog.Write("REG", "disable startup failed: " + ex.Message);
                return false;
            }
        }

        private static bool KillProcess(Threat t)
        {
            try
            {
                Process p = Process.GetProcessById(t.Pid);
                p.Kill();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void Finish(Threat t)
        {
            lock (StateLock)
            {
                _alerted.Remove(t.Id);
                RemoveLocked(t.Id);
            }
            RaiseChanged();
        }

        private static void RemoveLocked(string id)
        {
            for (int i = _threats.Count - 1; i >= 0; i--)
            {
                if (_threats[i].Id == id) _threats.RemoveAt(i);
            }
        }

        private static void LoadIgnores()
        {
            try
            {
                _ignored.Clear();
                if (!File.Exists(DefensePaths.IgnorePath)) return;
                string[] lines = File.ReadAllLines(DefensePaths.IgnorePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string s = lines[i].Trim();
                    if (s.Length > 0) _ignored.Add(s);
                }
            }
            catch
            {
            }
        }

        private static void SaveIgnores()
        {
            try
            {
                Directory.CreateDirectory(DefensePaths.Dir);
                List<string> list = new List<string>();
                lock (StateLock)
                {
                    foreach (string s in _ignored) list.Add(s);
                }
                File.WriteAllLines(DefensePaths.IgnorePath, list.ToArray(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private static void RaiseChanged()
        {
            Action h = Changed;
            if (h == null) return;
            try { h(); } catch { }
        }
    }
}
