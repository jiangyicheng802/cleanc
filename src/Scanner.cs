using System;
using System.Collections.Generic;
using System.IO;

namespace CleanC
{
    public static class Scanner
    {
        private const int MaxFiles = 4000000;

        /// <summary>低：只读取关键缓存 / 临时目录，秒级完成。</summary>
        public static readonly string[] LowIds =
        {
            "user_temp", "browser_cache", "thumb_cache", "crash_dump", "recycle_bin"
        };

        /// <summary>中：在低的基础上加入系统临时、更新缓存等需要管理员的关键目录。</summary>
        public static readonly string[] MediumIds =
        {
            "user_temp", "browser_cache", "thumb_cache", "crash_dump", "recycle_bin",
            "system_temp", "wu_cache", "font_cache", "delivery_opt",
            "prefetch", "sys_logs", "package_cache", "windows_old"
        };

        private class DirNode
        {
            public DirectoryInfo Dir;
            public string Norm;
        }

        public static ScanResult ScanByLevel(RuleEngine engine, ScanLevel level, bool isAdmin,
            Action<ScanProgress> onProgress, Func<bool> canceled)
        {
            if (level == ScanLevel.High || level == ScanLevel.Extreme)
            {
                int passes = level == ScanLevel.Extreme ? 3 : 1;
                ScanResult last = null;
                int elapsed = 0;
                int totalFiles = 0;
                for (int p = 1; p <= passes; p++)
                {
                    if (canceled != null && canceled()) break;
                    int pass = p;
                    ScanResult r = Scan(engine, delegate(ScanProgress sp)
                    {
                        sp.Pass = pass;
                        sp.Passes = passes;
                        if (onProgress != null) onProgress(sp);
                    }, canceled);
                    if (r != null)
                    {
                        last = r;
                        elapsed += r.ElapsedMs;
                        totalFiles += (int)Math.Min(int.MaxValue, r.TotalFiles);
                        if (r.Canceled) break;
                    }
                }
                if (last == null)
                {
                    last = new ScanResult();
                    last.Categories = engine.Categories;
                }
                last.ElapsedMs = elapsed;
                last.Passes = passes;
                last.Level = level;
                last.TotalFiles = totalFiles > 0 ? totalFiles : last.TotalFiles;

                // 缓存类目按“正在运行的应用不删”原则重新统计，避免把占用中的缓存算进去
                RemeasureCaches(engine);
                return last;
            }

            engine.ResetAggregates();
            ScanResult res = new ScanResult();
            res.Categories = engine.Categories;
            res.Level = level;
            int start = Environment.TickCount;
            string[] ids = level == ScanLevel.Low ? LowIds : MediumIds;
            int restricted = 0;
            long files = 0;
            long bytes = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                if (canceled != null && canceled())
                {
                    res.Canceled = true;
                    break;
                }
                Category cat = engine.Get(ids[i]);
                if (cat == null) continue;
                if (cat.NeedsAdmin && !isAdmin)
                {
                    cat.Restricted = true;
                    restricted++;
                    if (onProgress != null)
                    {
                        ScanProgress tp = new ScanProgress();
                        tp.Stage = cat.Name;
                        tp.CurrentPath = Loc.T("scan.needAdmin");
                        tp.ElapsedMs = unchecked(Environment.TickCount - start);
                        tp.Passes = 1;
                        onProgress(tp);
                    }
                    continue;
                }
                if (onProgress != null)
                {
                    ScanProgress tp = new ScanProgress();
                    tp.Stage = cat.Name;
                    tp.CurrentPath = cat.Name;
                    tp.ElapsedMs = unchecked(Environment.TickCount - start);
                    tp.Passes = 1;
                    onProgress(tp);
                }
                MeasureCategory(engine, cat);
                files += cat.FileCount;
                bytes += cat.Bytes;
                if (onProgress != null)
                {
                    ScanProgress ap = new ScanProgress();
                    ap.Stage = cat.Name;
                    ap.CurrentPath = cat.Name;
                    ap.Files = files;
                    ap.Bytes = bytes;
                    ap.ElapsedMs = unchecked(Environment.TickCount - start);
                    ap.Passes = 1;
                    onProgress(ap);
                }
            }
            res.TotalFiles = files;
            res.TotalBytes = bytes;
            res.Restricted = restricted;
            res.ElapsedMs = unchecked(Environment.TickCount - start);
            if (onProgress != null)
            {
                ScanProgress fp = new ScanProgress();
                fp.Files = files;
                fp.Bytes = bytes;
                fp.ElapsedMs = res.ElapsedMs;
                fp.Passes = 1;
                onProgress(fp);
            }
            return res;
        }

        private static void RemeasureCaches(RuleEngine engine)
        {
            Category ac = engine.Get("app_cache");
            if (ac != null && ac.Roots.Count > 0) MeasureCategory(engine, ac);
            Category bc = engine.Get("browser_cache");
            if (bc != null && bc.Roots.Count > 0) MeasureCategory(engine, bc);
        }

        public static ScanResult Scan(RuleEngine engine, Action<ScanProgress> onProgress, Func<bool> canceled)
        {
            engine.ResetAggregates();
            ScanResult result = new ScanResult();
            result.Categories = engine.Categories;

            List<DirNode> stack = new List<DirNode>();
            DirectoryInfo root = new DirectoryInfo(@"C:\");
            if (!root.Exists)
            {
                result.Categories = engine.Categories;
                return result;
            }
            DirNode dn = new DirNode();
            dn.Dir = root;
            dn.Norm = @"c:\";
            stack.Add(dn);

            long totalFiles = 0;
            long totalBytes = 0;
            int denied = 0;
            int links = 0;
            int start = Environment.TickCount;
            int lastReport = start;
            bool wasCanceled = false;
            bool truncated = false;

            while (stack.Count > 0)
            {
                if (totalFiles >= MaxFiles)
                {
                    truncated = true;
                    break;
                }
                DirNode node = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                if (canceled != null && canceled())
                {
                    wasCanceled = true;
                    break;
                }

                DirectoryInfo dir = node.Dir;
                string norm = node.Norm;

                try
                {
                    FileSystemInfo[] infos = GetInfos(dir);
                    for (int i = 0; i < infos.Length; i++)
                    {
                        FileSystemInfo info = infos[i];
                        string name = info.Name;
                        string full = info.FullName;
                        bool isDir;
                        long size = 0;
                        FileAttributes attrs;
                        try
                        {
                            attrs = info.Attributes;
                        }
                        catch
                        {
                            denied++;
                            continue;
                        }
                        if ((attrs & FileAttributes.ReparsePoint) != 0)
                        {
                            links++;
                            continue;
                        }
                        if ((attrs & FileAttributes.Directory) != 0)
                        {
                            isDir = true;
                        }
                        else
                        {
                            isDir = false;
                            try
                            {
                                size = ((FileInfo)info).Length;
                            }
                            catch
                            {
                                size = 0;
                            }
                        }

                        if (isDir)
                        {
                            DirNode child = new DirNode();
                            child.Dir = (DirectoryInfo)info;
                            child.Norm = norm.EndsWith("\\") ? norm + name.ToLowerInvariant() : norm + "\\" + name.ToLowerInvariant();
                            stack.Add(child);
                            continue;
                        }

                        Category fileCat = engine.ClassifyFileIn(norm, name);
                        fileCat.Bytes += size;
                        fileCat.FileCount++;
                        totalFiles++;
                        totalBytes += size;

                        if (fileCat.Actionable && fileCat.Files.Count < fileCat.FileLimit)
                        {
                            FileEntry fe = new FileEntry(full, size);
                            FileJudge.Apply(fe, fileCat);
                            fileCat.Files.Add(fe);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    denied++;
                }
                catch (IOException)
                {
                    denied++;
                }
                catch (System.Security.SecurityException)
                {
                    denied++;
                }

                int now = Environment.TickCount;
                if (onProgress != null && unchecked(now - lastReport) >= 80)
                {
                    lastReport = now;
                    ScanProgress p = new ScanProgress();
                    p.Files = totalFiles;
                    p.Bytes = totalBytes;
                    p.CurrentPath = dir.FullName;
                    p.DeniedDirs = denied;
                    p.SkippedLinks = links;
                    p.ElapsedMs = unchecked(now - start);
                    onProgress(p);
                }
            }

            int elapsed = unchecked(Environment.TickCount - start);
            for (int i = 0; i < engine.Categories.Count; i++)
            {
                Category c = engine.Categories[i];
                c.Present = c.FileCount > 0 || c.Bytes > 0;
                c.Files.Sort(delegate(FileEntry a, FileEntry b)
                {
                    if (b.Size > a.Size) return 1;
                    if (b.Size < a.Size) return -1;
                    return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
                });
            }

            result.TotalFiles = totalFiles;
            result.TotalBytes = totalBytes;
            result.DeniedDirs = denied;
            result.SkippedLinks = links;
            result.Canceled = wasCanceled;
            result.Truncated = truncated;
            result.ElapsedMs = elapsed;

            if (onProgress != null)
            {
                ScanProgress p = new ScanProgress();
                p.Files = totalFiles;
                p.Bytes = totalBytes;
                p.CurrentPath = "";
                p.DeniedDirs = denied;
                p.SkippedLinks = links;
                p.ElapsedMs = elapsed;
                onProgress(p);
            }
            return result;
        }

        private static FileSystemInfo[] GetInfos(DirectoryInfo dir)
        {
            List<FileSystemInfo> list = new List<FileSystemInfo>();
            foreach (FileSystemInfo f in dir.EnumerateFileSystemInfos())
            {
                list.Add(f);
            }
            return list.ToArray();
        }

        public static void MeasureCategory(RuleEngine engine, Category cat)
        {
            cat.Bytes = 0;
            cat.FileCount = 0;
            cat.Files.Clear();
            for (int i = 0; i < cat.Roots.Count; i++)
            {
                string root = cat.Roots[i];
                try
                {
                    if (File.Exists(root))
                    {
                        FileInfo fi = new FileInfo(root);
                        Category fc = engine.ClassifyFile(root);
                        if (fc != null && fc.Id == cat.Id)
                        {
                            cat.Bytes += fi.Length;
                            cat.FileCount++;
                            if (cat.Files.Count < cat.FileLimit)
                            {
                                FileEntry fe = new FileEntry(fi.FullName, fi.Length);
                                FileJudge.Apply(fe, cat);
                                cat.Files.Add(fe);
                            }
                        }
                        continue;
                    }
                    if (!Directory.Exists(root)) continue;
                    if ((cat.Id == "app_cache" || cat.Id == "browser_cache") && ProcessGuard.IsCacheOwnerRunning(root))
                    {
                        cat.Busy++;
                        continue;
                    }
                    MeasureTree(engine, cat, new DirectoryInfo(root));
                }
                catch
                {
                }
            }
            cat.Present = cat.FileCount > 0 || cat.Bytes > 0;
            cat.Files.Sort(delegate(FileEntry a, FileEntry b)
            {
                if (b.Size > a.Size) return 1;
                if (b.Size < a.Size) return -1;
                return 0;
            });
        }

        private static void MeasureTree(RuleEngine engine, Category cat, DirectoryInfo root)
        {
            List<DirNode> stack = new List<DirNode>();
            DirNode dn = new DirNode();
            dn.Dir = root;
            string nr = root.FullName.ToLowerInvariant();
            if (nr.Length > 3) nr = nr.TrimEnd('\\');
            dn.Norm = nr;
            stack.Add(dn);

            while (stack.Count > 0)
            {
                DirNode node = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                try
                {
                    FileSystemInfo[] infos = GetInfos(node.Dir);
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
                            DirNode child = new DirNode();
                            child.Dir = (DirectoryInfo)info;
                            child.Norm = node.Norm + "\\" + info.Name.ToLowerInvariant();
                            stack.Add(child);
                            continue;
                        }
                        long size = 0;
                        try
                        {
                            size = ((FileInfo)info).Length;
                        }
                        catch
                        {
                            continue;
                        }
                        Category fc = engine.ClassifyFileIn(node.Norm, info.Name);
                        if (fc != null && fc.Id == cat.Id)
                        {
                            cat.Bytes += size;
                            cat.FileCount++;
                            if (cat.Files.Count < cat.FileLimit)
                            {
                                FileEntry fe = new FileEntry(info.FullName, size);
                                FileJudge.Apply(fe, cat);
                                cat.Files.Add(fe);
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
}
