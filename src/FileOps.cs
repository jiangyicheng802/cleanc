using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CleanC
{
    /// <summary>
    /// 文件地图里的具体文件操作：剪贴板、粘贴、永久删除、系统属性框、垃圾排查与批量清理。
    /// 所有破坏性操作都在这里再做一次黑名单与个人文件校验，UI 置灰只是第一道防线。
    /// </summary>
    public static class FileOps
    {
        private const uint FO_MOVE = 0x0001;
        private const uint FO_COPY = 0x0002;
        private const ushort FOF_ALLOWUNDO = 0x0040;
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpVerb;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpParameters;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIconOrMonitor;
            public IntPtr hProcess;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        // ---------------------------------------------------------------- 剪贴板

        public static void CopyToClipboard(string path, bool cut)
        {
            try
            {
                DataObject data = new DataObject();
                data.SetData(DataFormats.FileDrop, new string[] { path });
                if (cut)
                {
                    byte[] raw = BitConverter.GetBytes(2);
                    MemoryStream ms = new MemoryStream();
                    ms.Write(raw, 0, raw.Length);
                    data.SetData("Preferred DropEffect", ms);
                }
                Clipboard.SetDataObject(data, true);
            }
            catch
            {
            }
        }

        public static bool ClipboardHasFiles(out bool cut)
        {
            cut = false;
            try
            {
                IDataObject d = Clipboard.GetDataObject();
                if (d == null || !d.GetDataPresent(DataFormats.FileDrop)) return false;
                object eff = d.GetData("Preferred DropEffect");
                MemoryStream ms = eff as MemoryStream;
                if (ms != null)
                {
                    byte[] raw = new byte[4];
                    ms.Position = 0;
                    ms.Read(raw, 0, 4);
                    int v = BitConverter.ToInt32(raw, 0);
                    cut = (v & 2) != 0;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- 粘贴

        public class PasteResult
        {
            public bool Ok;
            public int Count;
            public string Error = "";
            public bool Cut;
        }

        public static PasteResult PasteInto(string targetDir)
        {
            PasteResult r = new PasteResult();
            try
            {
                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                {
                    r.Error = Loc.T("map.paste.noTarget");
                    return r;
                }
                IDataObject d = Clipboard.GetDataObject();
                if (d == null || !d.GetDataPresent(DataFormats.FileDrop))
                {
                    r.Error = Loc.T("map.paste.empty");
                    return r;
                }
                string[] files = d.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0)
                {
                    r.Error = Loc.T("map.paste.empty");
                    return r;
                }
                bool cut;
                ClipboardHasFiles(out cut);
                r.Cut = cut;
                for (int i = 0; i < files.Length; i++)
                {
                    string src = files[i];
                    if (!File.Exists(src) && !Directory.Exists(src)) continue;
                    SHFILEOPSTRUCT op = new SHFILEOPSTRUCT();
                    op.hwnd = IntPtr.Zero;
                    op.wFunc = cut ? FO_MOVE : FO_COPY;
                    op.pFrom = src + "\0\0";
                    op.pTo = targetDir + "\0\0";
                    op.fFlags = (ushort)(FOF_ALLOWUNDO | FOF_NOERRORUI);
                    op.fAnyOperationsAborted = false;
                    op.lpszProgressTitle = Loc.T(cut ? "map.paste.moving" : "map.paste.copying");
                    int hr = SHFileOperation(ref op);
                    if (hr != 0 || op.fAnyOperationsAborted)
                    {
                        r.Error = Loc.F("map.paste.fail", hr);
                        return r;
                    }
                    r.Count++;
                }
                r.Ok = true;
                return r;
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
                return r;
            }
        }

        // ---------------------------------------------------------------- 删除

        public static bool DeletePath(string path, out string error)
        {
            error = "";
            try
            {
                if (File.Exists(path))
                {
                    ClearAttributes(path);
                    File.Delete(path);
                    return true;
                }
                if (Directory.Exists(path))
                {
                    DeleteDir(new DirectoryInfo(path), ref error, 0);
                    return error.Length == 0;
                }
                error = Loc.T("map.op.missing");
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void DeleteDir(DirectoryInfo dir, ref string error, int depth)
        {
            if (depth > 64) return;
            FileSystemInfo[] infos;
            try
            {
                List<FileSystemInfo> list = new List<FileSystemInfo>();
                foreach (FileSystemInfo f in dir.EnumerateFileSystemInfos()) list.Add(f);
                infos = list.ToArray();
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return;
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
                if ((attrs & FileAttributes.Directory) != 0)
                {
                    DeleteDir((DirectoryInfo)info, ref error, depth + 1);
                    continue;
                }
                try
                {
                    ClearAttributes(info.FullName);
                    File.Delete(info.FullName);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
            }
            try
            {
                ClearAttributes(dir.FullName);
                dir.Delete();
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        private static void ClearAttributes(string path)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            catch
            {
            }
        }

        // ---------------------------------------------------------------- 属性

        public static bool ShowProperties(string path)
        {
            try
            {
                SHELLEXECUTEINFO sei = new SHELLEXECUTEINFO();
                sei.cbSize = Marshal.SizeOf(typeof(SHELLEXECUTEINFO));
                sei.fMask = 0x0000000C;
                sei.lpVerb = "properties";
                sei.lpFile = path;
                sei.nShow = 1;
                ShellExecuteEx(ref sei);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- 垃圾排查

        public class JunkGroup
        {
            public string CatId = "other";
            public FileRisk Risk = FileRisk.Safe;
            public long Bytes;
            public long Files;
        }

        public class JunkReport
        {
            public MapNode Root;
            public long Total;
            public long Reclaim;
            public long Risky;
            public long Forbidden;
            public List<JunkGroup> Groups = new List<JunkGroup>();
            public List<MapNode> Largest = new List<MapNode>();
            public List<MapNode> Cleanable = new List<MapNode>();
        }

        public static JunkReport Analyze(MapNode node)
        {
            JunkReport r = new JunkReport();
            r.Root = node;
            r.Total = node.Size;
            r.Risky = node.Agg[(int)FileRisk.Risky];
            r.Forbidden = node.Agg[(int)FileRisk.Forbidden];
            r.Reclaim = node.Agg[(int)FileRisk.Safe] + node.Agg[(int)FileRisk.Caution];
            if (node.IsDir && node.Children != null)
            {
                List<MapNode> stack = new List<MapNode>();
                stack.Add(node);
                while (stack.Count > 0)
                {
                    MapNode dir = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    for (int i = 0; i < dir.Children.Count; i++)
                    {
                        MapNode c = dir.Children[i];
                        if (c.IsDir)
                        {
                            stack.Add(c);
                            continue;
                        }
                        if (c.Risk != FileRisk.Safe && c.Risk != FileRisk.Caution) continue;
                        AddCleanable(r, c);
                    }
                }
            }
            else if (!node.IsDir && (node.Risk == FileRisk.Safe || node.Risk == FileRisk.Caution))
            {
                AddCleanable(r, node);
            }

            r.Groups.Sort(delegate(JunkGroup a, JunkGroup b)
            {
                if (b.Bytes > a.Bytes) return 1;
                if (b.Bytes < a.Bytes) return -1;
                return string.Compare(a.CatId, b.CatId, StringComparison.OrdinalIgnoreCase);
            });
            r.Largest.Sort(delegate(MapNode a, MapNode b)
            {
                if (b.Size > a.Size) return 1;
                if (b.Size < a.Size) return -1;
                return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
            });
            if (r.Largest.Count > 6) r.Largest.RemoveRange(6, r.Largest.Count - 6);
            return r;
        }

        private static void AddCleanable(JunkReport r, MapNode f)
        {
            r.Cleanable.Add(f);
            JunkGroup g = null;
            for (int i = 0; i < r.Groups.Count; i++)
            {
                if (r.Groups[i].CatId == f.CatId)
                {
                    g = r.Groups[i];
                    break;
                }
            }
            if (g == null)
            {
                g = new JunkGroup();
                g.CatId = f.CatId;
                g.Risk = f.Risk;
                r.Groups.Add(g);
            }
            g.Bytes += f.Size;
            g.Files++;
            r.Largest.Add(f);
        }

        public class JunkOutcome
        {
            public int Deleted;
            public long Bytes;
            public int Skipped;
            public string LogPath = "";
            public List<string> Errors = new List<string>();
            public List<MapNode> DeletedNodes = new List<MapNode>();
            public bool Canceled;
        }

        public static string LogDir
        {
            get
            {
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(Path.Combine(dir, "CleanC"), "logs");
            }
        }

        public static JunkOutcome DeleteJunk(RuleEngine engine, JunkReport report, List<string> catIds,
            Action<long, long> progress, Func<bool> canceled)
        {
            JunkOutcome o = new JunkOutcome();
            if (report == null || report.Cleanable.Count == 0) return o;
            long expected = 0;
            List<MapNode> targets = new List<MapNode>();
            for (int i = 0; i < report.Cleanable.Count; i++)
            {
                MapNode f = report.Cleanable[i];
                if (catIds == null || !catIds.Contains(f.CatId)) continue;
                targets.Add(f);
                expected += f.Size;
            }

            StreamWriter log = null;
            try
            {
                Directory.CreateDirectory(LogDir);
                o.LogPath = Path.Combine(LogDir, "mapclean-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                log = new StreamWriter(o.LogPath, false, Encoding.UTF8);
                log.WriteLine("CleanC file-map junk clean " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                log.WriteLine("root=" + report.Root.Path + "  files=" + targets.Count + "  bytes=" + expected);
            }
            catch
            {
                log = null;
            }

            long processed = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                if (canceled != null && canceled())
                {
                    o.Canceled = true;
                    break;
                }
                MapNode f = targets[i];
                string path = f.Path;
                processed += f.Size;
                if (!File.Exists(path))
                {
                    o.Skipped++;
                    continue;
                }
                if (engine.IsForbidden(path))
                {
                    o.Skipped++;
                    if (log != null) log.WriteLine("SKIP [forbidden] " + path);
                    continue;
                }
                Category cat = engine.ClassifyFile(path);
                if (cat != null && FileJudge.RiskOf(path, cat) == RiskLevel.Protected)
                {
                    o.Skipped++;
                    if (log != null) log.WriteLine("SKIP [personal] " + path);
                    continue;
                }
                if (cat != null && (cat.Id == "app_cache" || cat.Id == "browser_cache") &&
                    ProcessGuard.IsCacheOwnerRunning(path))
                {
                    o.Skipped++;
                    if (log != null) log.WriteLine("SKIP [app running] " + path);
                    continue;
                }
                string error;
                if (DeletePath(path, out error))
                {
                    o.Deleted++;
                    o.Bytes += f.Size;
                    o.DeletedNodes.Add(f);
                    if (log != null) log.WriteLine("OK   " + path);
                }
                else
                {
                    o.Skipped++;
                    o.Errors.Add(path + "  " + error);
                    if (log != null) log.WriteLine("FAIL " + path + " -> " + error);
                }
                if (progress != null) progress(processed, expected);
            }
            if (log != null)
            {
                try
                {
                    log.WriteLine("done deleted=" + o.Deleted + " skipped=" + o.Skipped + " bytes=" + o.Bytes);
                    log.Dispose();
                }
                catch
                {
                }
            }
            return o;
        }

        // ---------------------------------------------------------------- 辅助

        public static string TargetDir(MapNode node)
        {
            if (node == null) return "";
            if (node.IsDir) return node.Path;
            return node.Parent == null ? "" : node.Parent.Path;
        }
    }
}
