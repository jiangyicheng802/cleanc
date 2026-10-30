using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CleanC
{
    public enum MigrateState
    {
        Ready = 0,
        Moved = 1,
        ExternalLink = 2,
        Busy = 3
    }

    /// <summary>AppData 下可直接搬家的一项（Local 或 Roaming 的一级子目录）。</summary>
    public class MigrateEntry
    {
        public string Name = "";
        public string Scope = "Local";
        public string Source = "";
        public string Target = "";
        public long Bytes;
        public long FileCount;
        public MigrateState State = MigrateState.Ready;
        public bool Selected;
        public bool Locked;
        public string Note = "";

        public bool CanMove
        {
            get { return State == MigrateState.Ready || State == MigrateState.Busy; }
        }

        public bool CanRestore
        {
            get { return State == MigrateState.Moved; }
        }

        public string StateKey
        {
            get
            {
                if (State == MigrateState.Moved) return "migrate.state.moved";
                if (State == MigrateState.ExternalLink) return "migrate.state.link";
                if (State == MigrateState.Busy) return "migrate.state.busy";
                return "migrate.state.normal";
            }
        }

        public string ScopeName
        {
            get { return Scope == "Roaming" ? "Roaming" : "Local"; }
        }
    }

    public class MigrateProgress
    {
        public long Done;
        public long Total;
        public long Files;
        public string Current = "";
        public string Stage = "";
        public bool Canceled;
    }

    public class MigrateResult
    {
        public bool Ok;
        public bool Canceled;
        public string Error = "";
        public long Bytes;
        public long Files;
        public bool BackupLeft;
        public string BackupPath = "";
        public string LogPath = "";
    }

    /// <summary>目录联接（junction）读写：不依赖管理员权限，也不需要符号链接特权。</summary>
    public static class Junction
    {
        private const uint FsctlGetReparsePoint = 0x000900A8;
        private const int MountPointTag = unchecked((int)0xA0000003);
        private const uint FileFlagBackupSemantics = 0x02000000;
        private const uint FileFlagOpenReparsePoint = 0x00200000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security,
            uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(IntPtr device, uint control, IntPtr input, uint inputSize,
            IntPtr output, uint outputSize, out uint returned, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        public static bool IsJunction(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return false;
                FileAttributes attrs = File.GetAttributes(path);
                return (attrs & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetTarget(string path, out string target)
        {
            target = "";
            if (!IsJunction(path)) return false;
            IntPtr h = IntPtr.Zero;
            try
            {
                // 以 OPEN_REPARSE_POINT 打开目录本身，避免跟随联接进入目标盘
                h = CreateFileW(path, 0, 0, IntPtr.Zero, 3,
                    FileFlagBackupSemantics | FileFlagOpenReparsePoint, IntPtr.Zero);
                if (h == IntPtr.Zero) return false;
                int size = 16 * 1024;
                IntPtr buffer = Marshal.AllocHGlobal(size);
                try
                {
                    uint returned;
                    if (!DeviceIoControl(h, FsctlGetReparsePoint, IntPtr.Zero, 0, buffer, (uint)size,
                        out returned, IntPtr.Zero)) return false;
                    if (returned < 20) return false;
                    byte[] data = new byte[returned];
                    Marshal.Copy(buffer, data, 0, data.Length);
                    if (BitConverter.ToInt32(data, 0) != MountPointTag) return false;
                    ushort offset = BitConverter.ToUInt16(data, 8);
                    ushort length = BitConverter.ToUInt16(data, 10);
                    if (length < 4 || offset + 20 + length > data.Length) return false;
                    string raw = Encoding.Unicode.GetString(data, 20 + offset, length);
                    // mklink / PowerShell 生成的联接前缀可能是 \??\ 、\\?\ 或 ?\，
                    // 统一从盘符位置截取，避免前缀差异影响路径比较。
                    int drive = raw.IndexOf(@":\", StringComparison.Ordinal);
                    if (drive >= 1) raw = raw.Substring(drive - 1);
                    else if (raw.StartsWith(@"\??\", StringComparison.Ordinal)) raw = raw.Substring(4);
                    else if (raw.StartsWith(@"\\?\", StringComparison.Ordinal)) raw = raw.Substring(4);
                    // mklink 的替身名长度可能把结尾的 NUL 与打印名首字符一起带上，
                    // 只取第一个 NUL 之前的部分才是真实目标路径。
                    int nul = raw.IndexOf('\0');
                    if (nul >= 0) raw = raw.Substring(0, nul);
                    target = raw.TrimEnd('\0');
                    return target.Length > 0;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                if (h != IntPtr.Zero) CloseHandle(h);
            }
        }

        public static bool Create(string linkPath, string targetPath, out string error)
        {
            error = "";
            try
            {
                if (Directory.Exists(linkPath) || File.Exists(linkPath))
                {
                    error = Loc.T("migrate.error").Replace("{0}", linkPath + " already exists");
                    return false;
                }
                if (!Directory.Exists(targetPath))
                {
                    error = targetPath + " does not exist";
                    return false;
                }
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
                    "/c mklink /J \"" + linkPath + "\" \"" + targetPath + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    string err = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(30000))
                    {
                        try { p.Kill(); } catch { }
                        error = "mklink timeout";
                        return false;
                    }
                    if (p.ExitCode != 0)
                    {
                        error = (err + output).Trim();
                        if (error.Length == 0) error = "mklink exit " + p.ExitCode;
                        return false;
                    }
                }
                string resolved;
                if (!TryGetTarget(linkPath, out resolved) ||
                    !string.Equals(PathUtil.Norm(resolved), PathUtil.Norm(targetPath), StringComparison.Ordinal))
                {
                    error = "junction verification failed: exists=" + Directory.Exists(linkPath) +
                        " attrs=" + (Directory.Exists(linkPath) ? File.GetAttributes(linkPath).ToString() : "-") +
                        " resolved=[" + resolved + "] expect=[" + targetPath + "]";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>rmdir 只删除联接本身，不会递归删除目标盘上的数据。</summary>
        public static bool Delete(string linkPath, out string error)
        {
            error = "";
            try
            {
                if (!IsJunction(linkPath))
                {
                    error = "not a junction: " + linkPath;
                    return false;
                }
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c rmdir \"" + linkPath + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    string err = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(30000))
                    {
                        try { p.Kill(); } catch { }
                        error = "rmdir timeout";
                        return false;
                    }
                    if (p.ExitCode != 0)
                    {
                        error = (err + output).Trim();
                        if (error.Length == 0) error = "rmdir exit " + p.ExitCode;
                        return false;
                    }
                }
                return !Directory.Exists(linkPath);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    /// <summary>搬家记录：只记录由本程序创建成功的联接，用于识别「外部联接」与安全还原。</summary>
    public static class MigrateStore
    {
        public class Record
        {
            public string Scope = "Local";
            public string Name = "";
            public string Source = "";
            public string Target = "";
            public long Bytes;
        }

        private static List<Record> _items;
        public static string TargetRoot = "";

        public static string StorePath
        {
            get
            {
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(Path.Combine(dir, "CleanC"), "migrate.ini");
            }
        }

        public static List<Record> Items
        {
            get
            {
                if (_items == null) Load();
                return _items;
            }
        }

        public static void Load()
        {
            _items = new List<Record>();
            TargetRoot = "";
            try
            {
                string path = StorePath;
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    if (line.StartsWith("target=", StringComparison.OrdinalIgnoreCase))
                    {
                        TargetRoot = line.Substring(7);
                        continue;
                    }
                    if (!line.StartsWith("entry|", StringComparison.OrdinalIgnoreCase)) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length < 5) continue;
                    Record r = new Record();
                    r.Scope = parts[1];
                    r.Name = parts[2];
                    r.Source = parts[3];
                    r.Target = parts[4];
                    if (parts.Length > 5) long.TryParse(parts[5], out r.Bytes);
                    _items.Add(r);
                }
            }
            catch
            {
            }
        }

        public static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(StorePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# CleanC AppData relocation records");
                if (!string.IsNullOrEmpty(TargetRoot)) sb.AppendLine("target=" + TargetRoot);
                for (int i = 0; i < Items.Count; i++)
                {
                    Record r = Items[i];
                    sb.AppendLine("entry|" + r.Scope + "|" + r.Name + "|" + r.Source + "|" + r.Target + "|" + r.Bytes);
                }
                File.WriteAllText(StorePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        public static void Add(string scope, string name, string source, string target, long bytes)
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (string.Equals(PathUtil.Norm(Items[i].Source), PathUtil.Norm(source), StringComparison.Ordinal))
                    Items.RemoveAt(i);
            }
            Record rec = new Record();
            rec.Scope = scope;
            rec.Name = name;
            rec.Source = source;
            rec.Target = target;
            rec.Bytes = bytes;
            Items.Add(rec);
            Save();
        }

        public static Record Find(string source)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (string.Equals(PathUtil.Norm(Items[i].Source), PathUtil.Norm(source), StringComparison.Ordinal))
                    return Items[i];
            }
            return null;
        }

        public static void Remove(string source)
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (string.Equals(PathUtil.Norm(Items[i].Source), PathUtil.Norm(source), StringComparison.Ordinal))
                    Items.RemoveAt(i);
            }
            Save();
        }

        public static bool Contains(string source)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (string.Equals(PathUtil.Norm(Items[i].Source), PathUtil.Norm(source), StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public static string FindTarget(string source)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (string.Equals(PathUtil.Norm(Items[i].Source), PathUtil.Norm(source), StringComparison.Ordinal))
                    return Items[i].Target;
            }
            return "";
        }
    }

    public static class MigrateLog
    {
        public static string Dir
        {
            get
            {
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(Path.Combine(dir, "CleanC"), "logs");
            }
        }

        public static string LogPath
        {
            get { return Path.Combine(Dir, "migrate.log"); }
        }

        public static void Write(string tag, string text)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string path = LogPath;
                try
                {
                    if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024)
                    {
                        string old = path + ".1";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                }
                catch
                {
                }
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + tag + "] " + text + "\r\n",
                    Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    /// <summary>搬家核心：复制 → 校验 → 改名 → 建联接 → 删原目录；还原反向执行。</summary>
    public static class MigrateOps
    {
        private static readonly HashSet<string> Blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft", "Packages", "Programs", "Temp", "TempState", "CleanC",
            "ConnectedDevicesPlatform", "Comms", "D3DSCache", "NVIDIA", "AMD", "Intel",
            "VirtualStore", "PlaceholderTileLogoFolder", "Publishers", "ElevatedDiagnostics",
            "OneDrive", "Windows", "Desktop", "My Documents", "Application Data",
            "History", "Temporary Internet Files", "Programs and Settings"
        };

        private static readonly HashSet<string> GenericNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cache", "cache2", "caches", "codecache", "gpucache", "shadercache", "data",
            "userdata", "user", "users", "default", "profile", "profiles", "temp", "tmp",
            "logs", "log", "settings", "config", "local", "roaming", "common", "update",
            "updates", "storage", "database", "index", "content", "files", "bin", "lib",
            "modules", "plugins", "extensions", "resources", "assets", "docs", "session"
        };

        private static readonly HashSet<string> ProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static int _processStamp;
        private static bool _processReady;

        public static string LocalRoot
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
        }

        public static string RoamingRoot
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); }
        }

        public static bool IsBlockedName(string name)
        {
            return string.IsNullOrEmpty(name) || Blocked.Contains(name);
        }

        public static List<string> OtherFixedDrives()
        {
            List<string> list = new List<string>();
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                for (int i = 0; i < drives.Length; i++)
                {
                    DriveInfo d = drives[i];
                    try
                    {
                        if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                        if (string.Equals(d.Name.Substring(0, 2), "C:", StringComparison.OrdinalIgnoreCase)) continue;
                        list.Add(d.Name.Substring(0, 2));
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            return list;
        }

        public static string DefaultTargetRoot()
        {
            List<string> drives = OtherFixedDrives();
            if (drives.Count == 0) return "";
            return Path.Combine(drives[0] + Path.DirectorySeparatorChar, "CleanC-Moved");
        }

        public static string TargetPathFor(string targetRoot, string scope, string name)
        {
            return Path.Combine(Path.Combine(targetRoot, scope == "Roaming" ? "Roaming" : "Local"), name);
        }

        public static long Measure(string path, Action<MigrateProgress> progress, Func<bool> canceled,
            out long files, out long bytes)
        {
            files = 0;
            bytes = 0;
            if (!Directory.Exists(path)) return 0;
            List<string> stack = new List<string>();
            stack.Add(path);
            int lastReport = Environment.TickCount;
            while (stack.Count > 0)
            {
                if (canceled != null && canceled()) return bytes;
                string dir = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                try
                {
                    DirectoryInfo di = new DirectoryInfo(dir);
                    foreach (FileSystemInfo info in di.EnumerateFileSystemInfos())
                    {
                        try
                        {
                            FileAttributes attrs = info.Attributes;
                            if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                            if ((attrs & FileAttributes.Directory) != 0)
                            {
                                stack.Add(info.FullName);
                            }
                            else
                            {
                                bytes += ((FileInfo)info).Length;
                                files++;
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
                int now = Environment.TickCount;
                if (progress != null && unchecked(now - lastReport) >= 120)
                {
                    lastReport = now;
                    MigrateProgress p = new MigrateProgress();
                    p.Current = dir;
                    p.Files = files;
                    p.Done = bytes;
                    p.Stage = "measure";
                    progress(p);
                }
            }
            return bytes;
        }

        public static List<MigrateEntry> Scan(bool includeRoaming, Action<MigrateProgress> progress, Func<bool> canceled)
        {
            List<MigrateEntry> list = new List<MigrateEntry>();
            AppendRoot(LocalRoot, "Local", list, progress, canceled);
            if (includeRoaming) AppendRoot(RoamingRoot, "Roaming", list, progress, canceled);
            list.Sort(delegate(MigrateEntry a, MigrateEntry b)
            {
                if (b.Bytes > a.Bytes) return 1;
                if (b.Bytes < a.Bytes) return -1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            if (list.Count > 240) list.RemoveRange(240, list.Count - 240);
            return list;
        }

        private static void AppendRoot(string root, string scope, List<MigrateEntry> list,
            Action<MigrateProgress> progress, Func<bool> canceled)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(root);
            }
            catch
            {
                return;
            }
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dirs.Length; i++)
            {
                if (canceled != null && canceled()) return;
                string dir = dirs[i];
                string name = Path.GetFileName(dir);
                if (IsBlockedName(name)) continue;
                MigrateEntry e = new MigrateEntry();
                e.Name = name;
                e.Scope = scope;
                e.Source = dir;
                string linkTarget;
                bool link = Junction.TryGetTarget(dir, out linkTarget);
                e.Target = link ? linkTarget : "";
                long files = 0;
                long bytes = 0;
                if (link)
                {
                    MigrateStore.Record rec = MigrateStore.Find(dir);
                    e.State = rec != null ? MigrateState.Moved : MigrateState.ExternalLink;
                    if (rec != null)
                    {
                        bytes = rec.Bytes;
                        e.FileCount = 0;
                    }
                }
                else
                {
                    Measure(dir, progress, canceled, out files, out bytes);
                }
                e.Bytes = bytes;
                e.FileCount = files;
                if (!link)
                {
                    e.Locked = IsLikelyBusy(dir);
                    e.State = e.Locked ? MigrateState.Busy : MigrateState.Ready;
                }
                if (e.State != MigrateState.Ready || e.Bytes >= 20L * 1024 * 1024)
                    list.Add(e);
            }
        }

        public static bool ValidateMove(MigrateEntry e, string targetRoot, out string error)
        {
            error = "";
            if (e == null) return false;
            if (!Directory.Exists(e.Source))
            {
                error = Loc.T("map.op.missing");
                return false;
            }
            if (Junction.IsJunction(e.Source))
            {
                error = Loc.T("migrate.state.moved");
                return false;
            }
            if (string.IsNullOrEmpty(targetRoot) || !Path.IsPathRooted(targetRoot))
            {
                error = Loc.T("migrate.noDisk");
                return false;
            }
            string root = Path.GetPathRoot(targetRoot);
            if (string.Equals((root ?? "").Substring(0, 2), "C:", StringComparison.OrdinalIgnoreCase))
            {
                error = Loc.T("migrate.sameDrive");
                return false;
            }
            string target = TargetPathFor(targetRoot, e.Scope, e.Name);
            if (Directory.Exists(target) || File.Exists(target))
            {
                error = target + " already exists";
                return false;
            }
            try
            {
                string drive = Path.GetPathRoot(target);
                DriveInfo di = new DriveInfo(drive);
                long need = e.Bytes + 256L * 1024 * 1024;
                if (di.AvailableFreeSpace < need)
                {
                    error = Loc.F("migrate.notEnough", Theme.Bytes(need));
                    return false;
                }
            }
            catch
            {
                error = Loc.T("migrate.noDisk");
                return false;
            }
            return true;
        }

        public static MigrateResult Move(MigrateEntry e, string targetRoot,
            Action<MigrateProgress> progress, Func<bool> canceled)
        {
            MigrateResult r = new MigrateResult();
            r.LogPath = MigrateLog.LogPath;
            string error;
            if (!ValidateMove(e, targetRoot, out error))
            {
                r.Error = error;
                return r;
            }
            string target = TargetPathFor(targetRoot, e.Scope, e.Name);
            string backup = e.Source + ".CleanC_old_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            MigrateLog.Write("MOVE", "start " + e.Source + " -> " + target);

            long copiedFiles = 0;
            long copiedBytes = 0;
            if (!CopyTree(e.Source, target, progress, canceled, out copiedFiles, out copiedBytes, out error, r))
            {
                DeleteTree(target);
                r.Error = r.Canceled ? Loc.T("btn.cancel") : error;
                MigrateLog.Write("MOVE", "copy failed: " + r.Error);
                return r;
            }

            Report(progress, copiedBytes, copiedBytes, copiedFiles, e.Source, "verify");
            long srcFiles = 0;
            long srcBytes = 0;
            Measure(e.Source, null, null, out srcFiles, out srcBytes);
            long dstFiles = 0;
            long dstBytes = 0;
            Measure(target, null, null, out dstFiles, out dstBytes);
            if (dstFiles != srcFiles || dstBytes != srcBytes)
            {
                DeleteTree(target);
                r.Error = "copy verification failed: " + srcFiles + "/" + srcBytes + " vs " + dstFiles + "/" + dstBytes;
                MigrateLog.Write("MOVE", r.Error);
                return r;
            }

            Report(progress, copiedBytes, copiedBytes, copiedFiles, e.Source, "link");
            try
            {
                Directory.Move(e.Source, backup);
            }
            catch (Exception ex)
            {
                DeleteTree(target);
                r.Error = ex.Message;
                MigrateLog.Write("MOVE", "rename source failed: " + ex.Message);
                return r;
            }

            if (!Junction.Create(e.Source, target, out error))
            {
                try { Directory.Move(backup, e.Source); } catch { }
                DeleteTree(target);
                r.Error = error;
                MigrateLog.Write("MOVE", "junction failed: " + error);
                return r;
            }

            MigrateStore.Add(e.Scope, e.Name, e.Source, target, copiedBytes);
            Report(progress, copiedBytes, copiedBytes, copiedFiles, e.Source, "clean");
            if (!DeleteTree(backup))
            {
                r.BackupLeft = true;
                r.BackupPath = backup;
                MigrateLog.Write("MOVE", "backup left: " + backup);
            }

            r.Ok = true;
            r.Bytes = copiedBytes;
            r.Files = copiedFiles;
            e.Target = target;
            e.State = MigrateState.Moved;
            MigrateLog.Write("MOVE", "done files=" + copiedFiles + " bytes=" + copiedBytes);
            return r;
        }

        public static bool ValidateRestore(MigrateEntry e, out string error)
        {
            error = "";
            if (e == null) return false;
            string target;
            if (!Junction.TryGetTarget(e.Source, out target))
            {
                error = Loc.T("migrate.state.normal");
                return false;
            }
            if (!MigrateStore.Contains(e.Source))
            {
                error = Loc.T("migrate.state.link");
                return false;
            }
            if (!Directory.Exists(target))
            {
                error = Loc.T("map.op.missing");
                return false;
            }
            try
            {
                long files;
                long bytes = 0;
                Measure(target, null, null, out files, out bytes);
                DriveInfo di = new DriveInfo("C:");
                if (di.AvailableFreeSpace < bytes + 256L * 1024 * 1024)
                {
                    error = Loc.F("migrate.notEnough", Theme.Bytes(bytes + 256L * 1024 * 1024));
                    return false;
                }
            }
            catch
            {
            }
            return true;
        }

        public static MigrateResult Restore(MigrateEntry e, Action<MigrateProgress> progress, Func<bool> canceled)
        {
            MigrateResult r = new MigrateResult();
            r.LogPath = MigrateLog.LogPath;
            string error;
            if (!ValidateRestore(e, out error))
            {
                r.Error = error;
                return r;
            }
            string target;
            Junction.TryGetTarget(e.Source, out target);
            MigrateLog.Write("RESTORE", "start " + e.Source + " <- " + target);

            if (!Junction.Delete(e.Source, out error))
            {
                r.Error = error;
                MigrateLog.Write("RESTORE", "remove junction failed: " + error);
                return r;
            }

            long files = 0;
            long bytes = 0;
            if (!CopyTree(target, e.Source, progress, canceled, out files, out bytes, out error, r))
            {
                string copyError = error;
                DeleteTree(e.Source);
                string relinkError;
                Junction.Create(e.Source, target, out relinkError);
                r.Error = r.Canceled ? Loc.T("btn.cancel") :
                    copyError + (relinkError.Length > 0 ? "  |  relink: " + relinkError : "");
                MigrateLog.Write("RESTORE", "copy back failed: " + r.Error);
                return r;
            }

            Report(progress, bytes, bytes, files, target, "verify");
            long srcFiles = 0, srcBytes = 0, dstFiles = 0, dstBytes = 0;
            Measure(target, null, null, out srcFiles, out srcBytes);
            Measure(e.Source, null, null, out dstFiles, out dstBytes);
            if (dstFiles != srcFiles || dstBytes != srcBytes)
            {
                DeleteTree(e.Source);
                Junction.Create(e.Source, target, out error);
                r.Error = "restore verification failed";
                MigrateLog.Write("RESTORE", r.Error);
                return r;
            }

            DeleteTree(target);
            try
            {
                string parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent) &&
                    Directory.GetFileSystemEntries(parent).Length == 0) Directory.Delete(parent);
                string root = Path.GetDirectoryName(parent ?? "");
                if (!string.IsNullOrEmpty(root) && Directory.Exists(root) &&
                    Directory.GetFileSystemEntries(root).Length == 0) Directory.Delete(root);
            }
            catch
            {
            }
            MigrateStore.Remove(e.Source);
            e.State = MigrateState.Ready;
            e.Target = "";
            r.Ok = true;
            r.Bytes = bytes;
            r.Files = files;
            MigrateLog.Write("RESTORE", "done files=" + files + " bytes=" + bytes);
            return r;
        }

        private static void Report(Action<MigrateProgress> progress, long done, long total, long files,
            string current, string stage)
        {
            if (progress == null) return;
            MigrateProgress p = new MigrateProgress();
            p.Done = done;
            p.Total = total;
            p.Files = files;
            p.Current = current;
            p.Stage = stage;
            progress(p);
        }

        private static bool CopyTree(string source, string target, Action<MigrateProgress> progress,
            Func<bool> canceled, out long files, out long bytes, out string error, MigrateResult result)
        {
            files = 0;
            bytes = 0;
            error = "";
            try
            {
                Directory.CreateDirectory(target);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            long total = 0;
            try
            {
                long tf, tb;
                total = Measure(source, null, null, out tf, out tb);
            }
            catch
            {
            }
            List<string[]> stack = new List<string[]>();
            stack.Add(new string[] { source, target });
            int lastReport = Environment.TickCount;
            while (stack.Count > 0)
            {
                if (canceled != null && canceled())
                {
                    result.Canceled = true;
                    error = Loc.T("btn.cancel");
                    return false;
                }
                string[] pair = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                FileSystemInfo[] infos;
                try
                {
                    DirectoryInfo di = new DirectoryInfo(pair[0]);
                    List<FileSystemInfo> all = new List<FileSystemInfo>();
                    foreach (FileSystemInfo f in di.EnumerateFileSystemInfos()) all.Add(f);
                    infos = all.ToArray();
                }
                catch (Exception ex)
                {
                    error = pair[0] + "  " + ex.Message;
                    return false;
                }
                for (int i = 0; i < infos.Length; i++)
                {
                    FileSystemInfo info = infos[i];
                    FileAttributes attrs;
                    try
                    {
                        attrs = info.Attributes;
                    }
                    catch
                    {
                        continue;
                    }
                    if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                    string dst = Path.Combine(pair[1], info.Name);
                    if ((attrs & FileAttributes.Directory) != 0)
                    {
                        try
                        {
                            Directory.CreateDirectory(dst);
                            Directory.SetLastWriteTimeUtc(dst, info.LastWriteTimeUtc);
                        }
                        catch (Exception ex)
                        {
                            error = dst + "  " + ex.Message;
                            return false;
                        }
                        stack.Add(new string[] { info.FullName, dst });
                    }
                    else
                    {
                        long size = 0;
                        try { size = ((FileInfo)info).Length; } catch { }
                        try
                        {
                            File.Copy(info.FullName, dst, false);
                            try { File.SetLastWriteTimeUtc(dst, info.LastWriteTimeUtc); } catch { }
                        }
                        catch (Exception ex)
                        {
                            error = info.FullName + "  " + ex.Message;
                            return false;
                        }
                        bytes += size;
                        files++;
                        int now = Environment.TickCount;
                        if (progress != null && unchecked(now - lastReport) >= 120)
                        {
                            lastReport = now;
                            Report(progress, bytes, total, files, info.FullName, "copy");
                        }
                    }
                }
            }
            Report(progress, bytes, Math.Max(total, bytes), files, "", "verify");
            return true;
        }

        /// <summary>删除目录树；遇到联接只删联接本身，绝不进入目标盘。</summary>
        public static bool DeleteTree(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
            try
            {
                if (File.Exists(path)) { File.Delete(path); return true; }
                if (!Directory.Exists(path)) return true;
                if (Junction.IsJunction(path))
                {
                    string err;
                    return Junction.Delete(path, out err);
                }
                List<string> stack = new List<string>();
                List<string> dirs = new List<string>();
                stack.Add(path);
                while (stack.Count > 0)
                {
                    string dir = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    dirs.Add(dir);
                    FileSystemInfo[] infos;
                    try
                    {
                        DirectoryInfo di = new DirectoryInfo(dir);
                        List<FileSystemInfo> all = new List<FileSystemInfo>();
                        foreach (FileSystemInfo f in di.EnumerateFileSystemInfos()) all.Add(f);
                        infos = all.ToArray();
                    }
                    catch
                    {
                        continue;
                    }
                    for (int i = 0; i < infos.Length; i++)
                    {
                        FileSystemInfo info = infos[i];
                        try
                        {
                            FileAttributes attrs = info.Attributes;
                            if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                            if ((attrs & FileAttributes.Directory) != 0)
                            {
                                stack.Add(info.FullName);
                            }
                            else
                            {
                                if ((attrs & FileAttributes.ReadOnly) != 0) File.SetAttributes(info.FullName, FileAttributes.Normal);
                                File.Delete(info.FullName);
                            }
                        }
                        catch
                        {
                            return false;
                        }
                    }
                }
                for (int i = dirs.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        Directory.Delete(dirs[i], false);
                    }
                    catch
                    {
                        return false;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void RefreshProcesses()
        {
            int now = Environment.TickCount;
            if (_processReady && unchecked(now - _processStamp) < 5000) return;
            _processReady = true;
            _processStamp = now;
            ProcessNames.Clear();
            try
            {
                Process[] all = Process.GetProcesses();
                for (int i = 0; i < all.Length; i++)
                {
                    try
                    {
                        string n = Normalize(all[i].ProcessName);
                        if (n.Length >= 3) ProcessNames.Add(n);
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

        public class MigrateTestReport
        {
            public bool Ok;
            public bool CopyOk;
            public bool JunctionOk;
            public bool LinkReadOk;
            public bool RestoreOk;
            public long Bytes;
            public string Error = "";
        }

        /// <summary>自检：造树 → 复制 → 建联接 → 经联接读回 → 删联接 → 复制还原。</summary>
        public static MigrateTestReport SelfTest(string root)
        {
            MigrateTestReport r = new MigrateTestReport();
            string src = Path.Combine(Path.Combine(root, "src"), "ProbeApp");
            string target = Path.Combine(Path.Combine(root, "target"), "ProbeApp");
            string backup = src + ".old";
            try
            {
                if (Directory.Exists(root)) DeleteTree(root);
                Directory.CreateDirectory(Path.Combine(src, "sub"));
                Directory.CreateDirectory(Path.Combine(src, "empty"));
                File.WriteAllText(Path.Combine(src, "a.txt"), new string('a', 2048));
                File.WriteAllText(Path.Combine(src, "sub", "b.txt"), new string('b', 4096));
                File.WriteAllText(Path.Combine(src, "c.dat"), new string('c', 1024));

                long files = 0;
                long bytes = 0;
                MigrateResult copy = new MigrateResult();
                string error = "";
                if (!CopyTree(src, target, null, null, out files, out bytes, out error, copy))
                {
                    r.Error = "copy: " + error;
                    return r;
                }
                r.Bytes = bytes;
                long tf, tb, sf, sb;
                Measure(target, null, null, out tf, out tb);
                Measure(src, null, null, out sf, out sb);
                r.CopyOk = tf == sf && tb == sb && tb == 7168;

                Directory.Move(src, backup);
                if (!Junction.Create(src, target, out error))
                {
                    r.Error = "junction: " + error;
                    try { Directory.Move(backup, src); } catch { }
                    return r;
                }
                string resolved;
                r.JunctionOk = true;
                r.LinkReadOk = Junction.TryGetTarget(src, out resolved) &&
                    string.Equals(PathUtil.Norm(resolved), PathUtil.Norm(target), StringComparison.Ordinal) &&
                    File.Exists(Path.Combine(src, "sub", "b.txt")) &&
                    new FileInfo(Path.Combine(src, "a.txt")).Length == 2048;

                if (!Junction.Delete(src, out error))
                {
                    r.Error = "remove junction: " + error;
                    return r;
                }
                if (!CopyTree(target, src, null, null, out files, out bytes, out error, copy))
                {
                    r.Error = "restore copy: " + error;
                    return r;
                }
                long rf, rb;
                Measure(src, null, null, out rf, out rb);
                r.RestoreOk = File.Exists(Path.Combine(src, "a.txt")) &&
                    Directory.Exists(Path.Combine(src, "empty")) &&
                    File.Exists(Path.Combine(src, "sub", "b.txt")) && rb == 7168;

                DeleteTree(target);
                DeleteTree(backup);
                DeleteTree(src);
                r.Ok = r.CopyOk && r.JunctionOk && r.LinkReadOk && r.RestoreOk;
            }
            catch (Exception ex)
            {
                r.Error = ex.ToString();
            }
            finally
            {
                try { if (Directory.Exists(root)) DeleteTree(root); } catch { }
            }
            return r;
        }

        /// <summary>用一级子目录名与进程名做模糊匹配，提示「可能正在使用」，不阻止用户继续。</summary>
        public static bool IsLikelyBusy(string dir)
        {
            RefreshProcesses();
            if (ProcessNames.Count == 0) return false;
            List<string> candidates = new List<string>();
            candidates.Add(Path.GetFileName(dir));
            try
            {
                string[] kids = Directory.GetDirectories(dir);
                for (int i = 0; i < kids.Length && i < 24; i++) candidates.Add(Path.GetFileName(kids[i]));
            }
            catch
            {
            }
            for (int i = 0; i < candidates.Count; i++)
            {
                string n = Normalize(candidates[i]);
                if (n.Length < 4 || GenericNames.Contains(n)) continue;
                foreach (string p in ProcessNames)
                {
                    if (p.Length < 4 || GenericNames.Contains(p)) continue;
                    if (n.Contains(p) || p.Contains(n)) return true;
                }
            }
            return false;
        }
    }
}
