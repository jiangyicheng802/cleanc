using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CleanC
{
    public static class Cleaner
    {
        private const uint FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOCONFIRMATION = 0x0010;
        private const ushort FOF_SILENT = 0x0004;
        private const ushort FOF_NOERRORUI = 0x0400;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        private class WalkItem
        {
            public DirectoryInfo Dir;
            public string Norm;
        }

        public static CleanResult Clean(RuleEngine engine, List<Category> selected, bool isAdmin,
            Action<CleanProgress> onProgress, Func<bool> canceled)
        {
            return Clean(engine, selected, isAdmin, false, onProgress, canceled);
        }

        public static CleanResult Clean(RuleEngine engine, List<Category> selected, bool isAdmin,
            bool registryConsent, Action<CleanProgress> onProgress, Func<bool> canceled)
        {
            CleanResult result = new CleanResult();
            long expected = 0;
            for (int i = 0; i < selected.Count; i++) expected += selected[i].Bytes;

            string logPath = BuildLogPath();
            StreamWriter log = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                log = new StreamWriter(logPath, false, Encoding.UTF8);
                log.WriteLine("CleanC log  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                log.WriteLine("admin=" + isAdmin + "  registryConsent=" + registryConsent +
                    "  selected=" + selected.Count);
                log.WriteLine("--------------------------------------------------");
            }
            catch
            {
                log = null;
            }

            int start = Environment.TickCount;
            int lastReport = start;
            long processed = 0;

            for (int ci = 0; ci < selected.Count; ci++)
            {
                if (canceled != null && canceled())
                {
                    result.Canceled = true;
                    break;
                }
                Category cat = selected[ci];
                if (!cat.Actionable && !cat.Registry) continue;

                if (cat.Registry)
                {
                    if (!isAdmin || !registryConsent)
                    {
                        result.Skipped++;
                        AddFailure(result, "注册表清理未获授权：" + cat.Name);
                        WriteLog(log, "SKIP [registry] no consent or not admin");
                        continue;
                    }
                    if (IsCanceled(canceled)) { result.Canceled = true; break; }
                    string backup = Uninstaller.BackupDir();
                    result.BackupDir = backup;
                    WriteLog(log, "REG  backup -> " + backup);
                    PushProgress(onProgress, processed, expected, result, Loc.T("clean.stepRegistry"),
                        unchecked(Environment.TickCount - start), Loc.T("clean.stepRegistry"));
                    int before = result.Deleted;
                    RegistryJunk.Clean(cat.RegItems, backup, log, result, canceled);
                    WriteLog(log, "REG  removed items=" + (result.Deleted - before) +
                        " keys=" + result.RegKeys + " values=" + result.RegValues);
                    if (result.Canceled) break;
                    continue;
                }

                if (cat.NeedsAdmin && !isAdmin)
                {
                    result.Skipped++;
                    AddFailure(result, "需要管理员权限：" + cat.Name);
                    WriteLog(log, "FAIL [category] " + cat.Name + " -> needs administrator");
                    continue;
                }

                if (cat.Mode == CleanMode.EmptyRecycle)
                {
                    int hr = SHEmptyRecycleBin(IntPtr.Zero, @"C:\", 0x00000001 | 0x00000002 | 0x00000004);
                    if (hr == 0)
                    {
                        result.FreedBytes += cat.Bytes;
                        result.Deleted += cat.FileCount;
                        WriteLog(log, "OK   [recyclebin] emptied, approx " + cat.Bytes + " bytes");
                    }
                    else
                    {
                        result.Skipped++;
                        AddFailure(result, "清空回收站失败，错误码 " + hr);
                        WriteLog(log, "FAIL [recyclebin] hr=" + hr);
                    }
                    continue;
                }

                for (int ri = 0; ri < cat.Roots.Count; ri++)
                {
                    if (canceled != null && canceled())
                    {
                        result.Canceled = true;
                        break;
                    }
                    string root = cat.Roots[ri];
                    if ((cat.Id == "app_cache" || cat.Id == "browser_cache") && ProcessGuard.IsCacheOwnerRunning(root))
                    {
                        result.Skipped++;
                        AddFailure(result, Loc.F("clean.skipRunning", root));
                        WriteLog(log, "SKIP [app running] " + root);
                        continue;
                    }
                    if (File.Exists(root))
                    {
                        string full = root;
                        if (engine.IsForbidden(full))
                        {
                            result.Skipped++;
                            continue;
                        }
                        if (FileJudge.RiskOf(full, cat) == RiskLevel.Protected)
                        {
                            result.Skipped++;
                            WriteLog(log, "SKIP [personal] " + full);
                            continue;
                        }
                        long fs = 0;
                        try { fs = new FileInfo(full).Length; } catch { }
                        processed += fs;
                        bool ok = DeleteFile(full, cat.Mode, log);
                        if (ok)
                        {
                            Apply(result, cat.Mode, fs);
                        }
                        else
                        {
                            result.Skipped++;
                            AddFailure(result, "无法删除：" + full);
                        }
                        continue;
                    }
                    if (!Directory.Exists(root)) continue;

                    List<WalkItem> stack = new List<WalkItem>();
                    WalkItem wi = new WalkItem();
                    wi.Dir = new DirectoryInfo(root);
                    string nr = wi.Dir.FullName.ToLowerInvariant();
                    if (nr.Length > 3) nr = nr.TrimEnd('\\');
                    wi.Norm = nr;
                    stack.Add(wi);

                    while (stack.Count > 0)
                    {
                        if (canceled != null && canceled())
                        {
                            result.Canceled = true;
                            break;
                        }
                        WalkItem node = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        FileSystemInfo[] infos;
                        try
                        {
                            List<FileSystemInfo> l = new List<FileSystemInfo>();
                            foreach (FileSystemInfo fsi in node.Dir.EnumerateFileSystemInfos()) l.Add(fsi);
                            infos = l.ToArray();
                        }
                        catch
                        {
                            continue;
                        }
                        for (int i = 0; i < infos.Length; i++)
                        {
                            FileSystemInfo info = infos[i];
                            FileAttributes attrs;
                            try { attrs = info.Attributes; } catch { continue; }
                            if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                            if ((attrs & FileAttributes.Directory) != 0)
                            {
                                WalkItem child = new WalkItem();
                                child.Dir = (DirectoryInfo)info;
                                child.Norm = node.Norm + "\\" + info.Name.ToLowerInvariant();
                                stack.Add(child);
                                continue;
                            }

                            string full = info.FullName;
                            Category fc;
                            try { fc = engine.ClassifyFile(full); } catch { fc = null; }
                            if (fc == null || fc.Id != cat.Id || engine.IsForbidden(full))
                            {
                                result.Skipped++;
                                continue;
                            }
                            if (FileJudge.RiskOf(full, cat) == RiskLevel.Protected)
                            {
                                result.Skipped++;
                                WriteLog(log, "SKIP [personal] " + full);
                                continue;
                            }
                            long size = 0;
                            try { size = ((FileInfo)info).Length; } catch { size = 0; }
                            processed += size;
                            bool ok = DeleteFile(full, cat.Mode, log);
                            if (ok)
                            {
                                Apply(result, cat.Mode, size);
                            }
                            else
                            {
                                result.Skipped++;
                                AddFailure(result, "无法删除：" + full);
                            }

                            int now = Environment.TickCount;
                            if (onProgress != null && unchecked(now - lastReport) >= 90)
                            {
                                lastReport = now;
                                PushProgress(onProgress, processed, expected, result, full,
                                    unchecked(now - start), cat.Name);
                            }
                        }
                    }

                    if (result.Canceled) break;

                    if (cat.Mode == CleanMode.Delete)
                    {
                        PruneEmpty(new DirectoryInfo(root), 0);
                    }
                }
            }

            if (onProgress != null)
            {
                PushProgress(onProgress, processed, expected, result, "",
                    unchecked(Environment.TickCount - start), "");
            }
            if (log != null)
            {
                log.WriteLine("--------------------------------------------------");
                log.WriteLine("freed=" + result.FreedBytes + "  recycled=" + result.RecycledBytes +
                    "  deleted=" + result.Deleted + "  skipped=" + result.Skipped +
                    "  regKeys=" + result.RegKeys + "  regValues=" + result.RegValues +
                    "  canceled=" + result.Canceled);
                log.Dispose();
            }
            result.LogPath = logPath;
            return result;
        }

        private static bool IsCanceled(Func<bool> canceled)
        {
            return canceled != null && canceled();
        }

        private static void Apply(CleanResult r, CleanMode mode, long size)
        {
            r.Deleted++;
            if (mode == CleanMode.Recycle) r.RecycledBytes += size;
            else r.FreedBytes += size;
        }

        private static bool DeleteFile(string path, CleanMode mode, StreamWriter log)
        {
            if (mode == CleanMode.Recycle)
            {
                SHFILEOPSTRUCT op = new SHFILEOPSTRUCT();
                op.hwnd = IntPtr.Zero;
                op.wFunc = FO_DELETE;
                op.pFrom = path + "\0\0";
                op.pTo = null;
                op.fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI);
                op.fAnyOperationsAborted = false;
                int hr;
                try { hr = SHFileOperation(ref op); } catch { hr = -1; }
                bool ok = hr == 0 && !op.fAnyOperationsAborted;
                WriteLog(log, (ok ? "OK   " : "FAIL ") + path + (ok ? "" : " hr=" + hr));
                return ok;
            }
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            catch
            {
            }
            try
            {
                File.Delete(path);
                WriteLog(log, "OK   " + path);
                return true;
            }
            catch (Exception ex)
            {
                WriteLog(log, "FAIL " + path + " -> " + Short(ex.Message));
                return false;
            }
        }

        private static void PruneEmpty(DirectoryInfo dir, int depth)
        {
            if (depth > 6) return;
            FileSystemInfo[] infos;
            try
            {
                List<FileSystemInfo> l = new List<FileSystemInfo>();
                foreach (FileSystemInfo fsi in dir.EnumerateFileSystemInfos()) l.Add(fsi);
                infos = l.ToArray();
            }
            catch
            {
                return;
            }
            for (int i = 0; i < infos.Length; i++)
            {
                FileAttributes attrs;
                try { attrs = infos[i].Attributes; } catch { continue; }
                if ((attrs & FileAttributes.Directory) == 0) continue;
                if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                PruneEmpty((DirectoryInfo)infos[i], depth + 1);
            }
            if (depth == 0) return;
            try
            {
                if (dir.GetFileSystemInfos().Length == 0) dir.Delete();
            }
            catch
            {
            }
        }

        private static void PushProgress(Action<CleanProgress> cb, long processed, long expected,
            CleanResult r, string path, int elapsed, string stage)
        {
            CleanProgress p = new CleanProgress();
            p.Processed = processed;
            p.Expected = expected;
            p.Freed = r.FreedBytes;
            p.Recycled = r.RecycledBytes;
            p.Deleted = r.Deleted;
            p.Skipped = r.Skipped;
            p.CurrentPath = path;
            p.ElapsedMs = elapsed;
            p.Stage = stage;
            cb(p);
        }

        private static void AddFailure(CleanResult r, string message)
        {
            if (r.Failures.Count < 200) r.Failures.Add(message);
        }

        private static string BuildLogPath()
        {
            string dir;
            try { dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
            catch { dir = Path.GetTempPath(); }
            dir = Path.Combine(dir, "CleanC");
            dir = Path.Combine(dir, "logs");
            string name = "clean_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
            return Path.Combine(dir, name);
        }

        private static void WriteLog(StreamWriter log, string line)
        {
            if (log == null) return;
            try { log.WriteLine(line); } catch { }
        }

        private static string Short(string s)
        {
            if (s == null) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            if (s.Length > 80) s = s.Substring(0, 80);
            return s;
        }

        public static bool TryDeleteProbe(string path)
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
