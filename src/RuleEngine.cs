using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace CleanC
{
    public class Rule
    {
        public string Prefix;
        public Category Category;
        public bool ExactFile;

        public Rule(string prefix, Category category, bool exactFile)
        {
            Prefix = prefix;
            Category = category;
            ExactFile = exactFile;
        }
    }

    public class RuleEngine
    {
        public List<Category> Categories;
        private Dictionary<string, Category> byId;
        private List<Rule> rules;
        private List<Rule> fileRules;
        private Dictionary<string, Category> dirCache;
        private HashSet<string> hardBlock;
        private HashSet<string> exactBlock;
        private string[] rootSystemFiles = { "pagefile.sys", "hiberfil.sys", "swapfile.sys", "dumpstack.log.tmp" };

        public RuleEngine()
        {
            Categories = new List<Category>();
            byId = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
            rules = new List<Rule>();
            fileRules = new List<Rule>();
            dirCache = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
            hardBlock = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            exactBlock = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            BuildCategories();
            BuildRules();
        }

        public Category Get(string id)
        {
            Category c;
            if (byId.TryGetValue(id, out c)) return c;
            return null;
        }

        private Category Create(string id, string glyph, RiskLevel risk,
            CleanMode mode, bool needsAdmin, bool defaultSelected)
        {
            Category c = new Category(id, Loc.CatName(id), Loc.CatDesc(id), glyph, risk, mode, needsAdmin, defaultSelected);
            c.Accent = AccentFor(id);
            Categories.Add(c);
            byId[id] = c;
            return c;
        }

        public void Relocalize()
        {
            for (int i = 0; i < Categories.Count; i++)
            {
                Category c = Categories[i];
                c.Name = Loc.CatName(c.Id);
                c.Desc = Loc.CatDesc(c.Id);
            }
        }

        /// <summary>主题切换后重算分类强调色（金色外观下蓝色系换成暖金色系）。</summary>
        public void ApplyThemeAccents()
        {
            for (int i = 0; i < Categories.Count; i++)
            {
                Category c = Categories[i];
                c.Accent = AccentFor(c.Id);
            }
        }

        private static Color AccentFor(string id)
        {
            if (Theme.IsGold)
            {
                if (id == "user_temp") return Color.FromArgb(198, 148, 32);
                if (id == "system_temp") return Color.FromArgb(176, 126, 52);
                if (id == "wu_cache") return Color.FromArgb(156, 120, 48);
                if (id == "browser_cache") return Color.FromArgb(218, 170, 76);
                if (id == "app_cache") return Color.FromArgb(214, 164, 66);
                if (id == "thumb_cache") return Color.FromArgb(150, 150, 62);
                if (id == "crash_dump") return Color.FromArgb(214, 126, 80);
                if (id == "font_cache") return Color.FromArgb(191, 119, 0);
                if (id == "delivery_opt") return Color.FromArgb(218, 170, 76);
                if (id == "recycle_bin") return Color.FromArgb(111, 139, 57);
                if (id == "windows_old") return Color.FromArgb(174, 118, 28);
                if (id == "prefetch") return Color.FromArgb(198, 112, 42);
                if (id == "sys_logs") return Color.FromArgb(139, 124, 96);
                if (id == "package_cache") return Color.FromArgb(176, 126, 52);
                if (id == "reg_junk") return Color.FromArgb(190, 82, 60);
                if (id == "system_core") return Color.FromArgb(120, 90, 40);
                if (id == "programs") return Color.FromArgb(100, 86, 60);
                if (id == "installer") return Color.FromArgb(139, 124, 96);
                if (id == "user_files") return Color.FromArgb(176, 126, 52);
                if (id == "pagefile") return Color.FromArgb(120, 108, 60);
                return Color.FromArgb(169, 145, 100);
            }
            if (id == "user_temp") return Color.FromArgb(47, 123, 255);
            if (id == "system_temp") return Color.FromArgb(99, 102, 241);
            if (id == "wu_cache") return Color.FromArgb(139, 92, 246);
            if (id == "browser_cache") return Color.FromArgb(34, 199, 232);
            if (id == "app_cache") return Color.FromArgb(56, 189, 248);
            if (id == "thumb_cache") return Color.FromArgb(20, 184, 166);
            if (id == "crash_dump") return Color.FromArgb(251, 113, 133);
            if (id == "font_cache") return Color.FromArgb(245, 158, 11);
            if (id == "delivery_opt") return Color.FromArgb(14, 165, 233);
            if (id == "recycle_bin") return Color.FromArgb(16, 185, 129);
            if (id == "windows_old") return Color.FromArgb(217, 119, 6);
            if (id == "prefetch") return Color.FromArgb(249, 115, 22);
            if (id == "sys_logs") return Color.FromArgb(100, 116, 139);
            if (id == "package_cache") return Color.FromArgb(167, 139, 250);
            if (id == "reg_junk") return Color.FromArgb(244, 114, 182);
            if (id == "system_core") return Color.FromArgb(109, 40, 217);
            if (id == "programs") return Color.FromArgb(71, 85, 105);
            if (id == "installer") return Color.FromArgb(100, 116, 139);
            if (id == "user_files") return Color.FromArgb(139, 92, 246);
            if (id == "pagefile") return Color.FromArgb(15, 118, 110);
            return Color.FromArgb(148, 163, 184);
        }

        private void BuildCategories()
        {
            Create("user_temp", "临", RiskLevel.Safe, CleanMode.Delete, false, true);
            Create("system_temp", "临", RiskLevel.Safe, CleanMode.Delete, true, true);
            Create("wu_cache", "更", RiskLevel.Safe, CleanMode.Delete, true, true);
            Create("browser_cache", "缓", RiskLevel.Safe, CleanMode.Delete, false, true);
            Category appCache = Create("app_cache", "应", RiskLevel.Safe, CleanMode.Delete, false, true);
            appCache.FileLimit = 20000;
            Create("thumb_cache", "图", RiskLevel.Safe, CleanMode.Delete, false, true);
            Create("crash_dump", "错", RiskLevel.Safe, CleanMode.Delete, false, true);
            Create("font_cache", "字", RiskLevel.Safe, CleanMode.Delete, true, true);
            Create("delivery_opt", "优", RiskLevel.Safe, CleanMode.Delete, true, true);
            Create("recycle_bin", "回", RiskLevel.Safe, CleanMode.EmptyRecycle, false, false);

            Create("windows_old", "旧", RiskLevel.Caution, CleanMode.Delete, true, false);
            Create("prefetch", "读", RiskLevel.Caution, CleanMode.Recycle, true, false);
            Create("sys_logs", "日", RiskLevel.Caution, CleanMode.Recycle, true, false);
            Create("package_cache", "包", RiskLevel.Caution, CleanMode.Recycle, false, false);

            Category reg = Create("reg_junk", "表", RiskLevel.Caution, CleanMode.Delete, true, false);
            reg.Registry = true;
            reg.FileLimit = 500;
            Create("system_core", "核", RiskLevel.Protected, CleanMode.Delete, true, false);
            Create("programs", "程", RiskLevel.Protected, CleanMode.Delete, false, false);
            Create("installer", "卸", RiskLevel.Protected, CleanMode.Delete, true, false);
            Create("user_files", "个", RiskLevel.Protected, CleanMode.Delete, false, false);
            Create("pagefile", "存", RiskLevel.Protected, CleanMode.Delete, true, false);

            Create("other", "他", RiskLevel.Info, CleanMode.Delete, false, false);

            // 清理顺序（对应 AppData 清理原则）：
            // 1 系统与临时缓存 → 2 软件自带缓存 → 3 卸载残留与旧系统 → 4 Local 手动缓存 → 5 回收站 → 6 注册表
            Stage("system_temp", 1);
            Stage("user_temp", 1);
            Stage("wu_cache", 1);
            Stage("delivery_opt", 1);
            Stage("font_cache", 1);
            Stage("prefetch", 1);
            Stage("sys_logs", 1);
            Stage("thumb_cache", 1);
            Stage("crash_dump", 1);
            Stage("browser_cache", 2);
            Stage("package_cache", 3);
            Stage("windows_old", 3);
            Stage("app_cache", 4);
            Stage("recycle_bin", 5);
            Stage("reg_junk", 6);
        }

        private void Stage(string id, int stage)
        {
            Category c = Get(id);
            if (c != null) c.Stage = stage;
        }

        private void AddRule(Category c, string path)
        {
            AddRule(c, path, false);
        }

        private void AddRule(Category c, string path, bool exactFile)
        {
            string norm = PathUtil.Norm(path);
            if (norm.Length == 0) return;
            Rule r = new Rule(norm, c, exactFile);
            if (exactFile) fileRules.Add(r);
            else rules.Add(r);
            if (!c.Roots.Contains(norm)) c.Roots.Add(norm);
        }

        private void AddHard(string path)
        {
            hardBlock.Add(PathUtil.Norm(path));
        }

        private void AddExact(string path)
        {
            exactBlock.Add(PathUtil.Norm(path));
        }

        private void BuildRules()
        {
            Category win = Get("system_core");
            Category programs = Get("programs");
            Category users = Get("user_files");

            AddRule(win, @"C:\Windows");
            AddRule(programs, @"C:\Program Files");
            AddRule(programs, @"C:\Program Files (x86)");
            AddRule(programs, @"C:\ProgramData");
            AddRule(users, @"C:\Users");
            AddRule(Get("installer"), @"C:\Windows\Installer");

            AddRule(Get("system_temp"), @"C:\Windows\Temp");
            AddRule(Get("system_temp"), @"C:\Windows\SystemTemp");
            AddRule(Get("wu_cache"), @"C:\Windows\SoftwareDistribution\Download");
            AddRule(Get("font_cache"), @"C:\Windows\ServiceProfiles\LocalService\AppData\Local\FontCache");
            AddRule(Get("delivery_opt"), @"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization");
            AddRule(Get("prefetch"), @"C:\Windows\Prefetch");
            AddRule(Get("sys_logs"), @"C:\Windows\Logs");
            AddRule(Get("sys_logs"), @"C:\Windows\Panther");
            AddRule(Get("windows_old"), @"C:\Windows.old");
            AddRule(Get("package_cache"), @"C:\ProgramData\Package Cache");
            AddRule(Get("recycle_bin"), @"C:\$Recycle.Bin");

            Category dumps = Get("crash_dump");
            AddRule(dumps, @"C:\Windows\Minidump");
            AddRule(dumps, @"C:\Windows\LiveKernelReports");
            AddRule(dumps, @"C:\ProgramData\Microsoft\Windows\WER");
            AddRule(dumps, @"C:\Windows\MEMORY.DMP", true);

            Category page = Get("pagefile");
            AddRule(page, @"C:\pagefile.sys", true);
            AddRule(page, @"C:\hiberfil.sys", true);
            AddRule(page, @"C:\swapfile.sys", true);
            AddRule(page, @"C:\DumpStack.log.tmp", true);

            Category temp = Get("user_temp");
            Category browser = Get("browser_cache");
            Category thumbs = Get("thumb_cache");
            thumbs.FileFilter = "thumb_cache";
            Category dumpUser = Get("crash_dump");

            List<string> profiles = FindProfiles();
            for (int i = 0; i < profiles.Count; i++)
            {
                string p = profiles[i];
                AddRule(temp, Path.Combine(p, @"AppData\Local\Temp"));
                AddRule(dumpUser, Path.Combine(p, @"AppData\Local\CrashDumps"));
                AddRule(dumpUser, Path.Combine(p, @"AppData\Local\Microsoft\Windows\WER"));
                AddRule(thumbs, Path.Combine(p, @"AppData\Local\Microsoft\Windows\Explorer"));
                AddBrowserCaches(p, browser);
                AddAppCaches(p, Get("app_cache"));
            }

            rules.Sort(delegate(Rule a, Rule b)
            {
                if (b.Prefix.Length != a.Prefix.Length) return b.Prefix.Length - a.Prefix.Length;
                // 同前缀长度时浏览器缓存优先于通用应用缓存，避免 Edge/Chrome 的 Cache 被归到 app_cache
                int ra = a.Category != null && a.Category.Id == "browser_cache" ? 0 : 1;
                int rb = b.Category != null && b.Category.Id == "browser_cache" ? 0 : 1;
                if (ra != rb) return ra - rb;
                return string.Compare(a.Prefix, b.Prefix, StringComparison.OrdinalIgnoreCase);
            });
            fileRules.Sort(delegate(Rule a, Rule b) { return b.Prefix.Length.CompareTo(a.Prefix.Length); });

            AddHard(@"C:\Windows\System32");
            AddHard(@"C:\Windows\SysWOW64");
            AddHard(@"C:\Windows\WinSxS");
            AddHard(@"C:\Windows\Boot");
            AddHard(@"C:\Windows\servicing");
            AddHard(@"C:\Windows\Fonts");
            AddHard(@"C:\Windows\Installer");
            AddHard(@"C:\Program Files");
            AddHard(@"C:\Program Files (x86)");
            AddHard(@"C:\pagefile.sys");
            AddHard(@"C:\hiberfil.sys");
            AddHard(@"C:\swapfile.sys");
            AddHard(@"C:\DumpStack.log.tmp");

            // AppData 红线：Roaming / LocalLow 下的任何内容都不允许删除，Local 与 AppData 根目录本身也不行
            for (int i = 0; i < profiles.Count; i++)
            {
                string appData = Path.Combine(profiles[i], @"AppData");
                AddHard(Path.Combine(appData, "Roaming"));
                AddHard(Path.Combine(appData, "LocalLow"));
                AddExact(appData);
                AddExact(Path.Combine(appData, "Local"));
            }
        }

        private static List<string> FindProfiles()
        {
            List<string> list = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string[] dirs = Directory.GetDirectories(@"C:\Users");
                for (int i = 0; i < dirs.Length; i++)
                {
                    string n = Path.GetFileName(dirs[i]);
                    if (n.Equals("Default", StringComparison.OrdinalIgnoreCase)) continue;
                    if (n.Equals("Default User", StringComparison.OrdinalIgnoreCase)) continue;
                    if (n.Equals("All Users", StringComparison.OrdinalIgnoreCase)) continue;
                    if (seen.Add(dirs[i])) list.Add(dirs[i]);
                }
            }
            catch
            {
            }
            try
            {
                string me = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(me) && seen.Add(me)) list.Add(me);
            }
            catch
            {
            }
            return list;
        }

        private static readonly HashSet<string> CacheNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cache", "cache2", "caches", "code cache", "gpucache", "shadercache",
            "grshadercache", "shadercache2", "dawncache", "d3dscache", "cachestorage"
        };

        private static bool IsCacheFolderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Trim().ToLowerInvariant();
            if (CacheNames.Contains(n)) return true;
            // 只接受“明确命名”的缓存目录，带 data / profile / settings / session 的一律不碰
            if (n.IndexOf("data", StringComparison.Ordinal) >= 0) return false;
            if (n.IndexOf("profile", StringComparison.Ordinal) >= 0) return false;
            if (n.IndexOf("setting", StringComparison.Ordinal) >= 0) return false;
            if (n.IndexOf("session", StringComparison.Ordinal) >= 0) return false;
            if (n.IndexOf("login", StringComparison.Ordinal) >= 0) return false;
            return false;
        }

        /// <summary>
        /// 只收集 AppData\Local 下“明确命名为 Cache / Code Cache / GPUCache / ShaderCache”的目录。
        /// Roaming、LocalLow 永不进入；不进入缓存目录内部；有时间与数量上限。
        /// </summary>
        private void AddAppCaches(string profile, Category appCache)
        {
            if (appCache == null) return;
            string local;
            try { local = Path.Combine(profile, @"AppData\Local"); }
            catch { return; }
            if (!Directory.Exists(local)) return;

            int visited = 0;
            int start = Environment.TickCount;
            List<string> stack = new List<string>();
            stack.Add(local);
            while (stack.Count > 0)
            {
                if (visited > 4000) break;
                if (unchecked(Environment.TickCount - start) > 450) break;
                if (appCache.Roots.Count >= 300) break;
                string cur = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                visited++;
                string[] subs;
                try { subs = Directory.GetDirectories(cur); }
                catch { continue; }
                for (int i = 0; i < subs.Length; i++)
                {
                    string name = Path.GetFileName(subs[i]);
                    string lower = name.ToLowerInvariant();
                    if (IsCacheFolderName(lower))
                    {
                        AddRule(appCache, subs[i]);
                        continue;
                    }
                    if (lower == "temp" || lower == "tmp" || lower == "packages") continue;
                    try
                    {
                        if ((File.GetAttributes(subs[i]) & FileAttributes.ReparsePoint) != 0) continue;
                    }
                    catch
                    {
                        continue;
                    }
                    string rel = subs[i].Substring(local.Length).TrimStart('\\');
                    if (rel.Split('\\').Length > 5) continue;
                    stack.Add(subs[i]);
                }
            }
        }

        private void AddBrowserCaches(string profile, Category browser)        {
            AddBrowserUserData(Path.Combine(profile, @"AppData\Local\Google\Chrome\User Data"), browser);
            AddBrowserUserData(Path.Combine(profile, @"AppData\Local\Microsoft\Edge\User Data"), browser);
            try
            {
                string ff = Path.Combine(profile, @"AppData\Local\Mozilla\Firefox\Profiles");
                if (Directory.Exists(ff))
                {
                    string[] profs = Directory.GetDirectories(ff);
                    for (int i = 0; i < profs.Length; i++)
                    {
                        string c = Path.Combine(profs[i], "cache2");
                        if (Directory.Exists(c)) AddRule(browser, c);
                    }
                }
            }
            catch
            {
            }
        }

        private void AddBrowserUserData(string userData, Category browser)
        {
            try
            {
                if (!Directory.Exists(userData)) return;
                string[] profs = Directory.GetDirectories(userData);
                for (int i = 0; i < profs.Length; i++)
                {
                    string p = profs[i];
                    string[] names = { "Cache", "Code Cache", "GPUCache", "GrShaderCache", @"Service Worker\CacheStorage" };
                    for (int j = 0; j < names.Length; j++)
                    {
                        string c = Path.Combine(p, names[j]);
                        if (Directory.Exists(c)) AddRule(browser, c);
                    }
                }
            }
            catch
            {
            }
        }

        public void ResetAggregates()
        {
            for (int i = 0; i < Categories.Count; i++) Categories[i].Reset();
        }

        public Category ClassifyDirectory(string dirPath)
        {
            string norm = PathUtil.Norm(dirPath);
            Category cached;
            if (dirCache.TryGetValue(norm, out cached)) return cached;

            Category result = Get("other");
            for (int i = 0; i < rules.Count; i++)
            {
                Rule r = rules[i];
                if (PathUtil.Under(norm, r.Prefix))
                {
                    result = r.Category;
                    break;
                }
            }
            dirCache[norm] = result;
            return result;
        }

        public Category ClassifyFile(string fullPath)
        {
            string norm = PathUtil.Norm(fullPath);
            for (int i = 0; i < fileRules.Count; i++)
            {
                Rule r = fileRules[i];
                if (norm == r.Prefix) return r.Category;
            }
            string dir = "";
            string name = "";
            int cut = norm.LastIndexOf('\\');
            if (cut >= 0)
            {
                dir = norm.Substring(0, cut);
                name = norm.Substring(cut + 1);
            }
            if (dir.Length == 2 && dir.EndsWith(":"))
            {
                for (int i = 0; i < rootSystemFiles.Length; i++)
                {
                    if (name == rootSystemFiles[i]) return Get("pagefile");
                }
                return Get("other");
            }
            return ClassifyFileIn(dir, name);
        }

        public Category ClassifyFileIn(string normalizedDir, string fileName)
        {
            if (normalizedDir == @"c:\" || normalizedDir == "c:")
            {
                for (int i = 0; i < rootSystemFiles.Length; i++)
                {
                    if (fileName.ToLowerInvariant() == rootSystemFiles[i]) return Get("pagefile");
                }
                return Get("other");
            }
            Category cat = ClassifyDirectory(normalizedDir);
            string filter = cat.FileFilter;
            if (filter != null)
            {
                string n = fileName.ToLowerInvariant();
                if (n.StartsWith("thumbcache_") && n.EndsWith(".db")) return cat;
                if (n.StartsWith("iconcache_") && n.EndsWith(".db")) return cat;
                return Get("other");
            }
            return cat;
        }

        /// <summary>
        /// 绝对不可触碰的路径（System32 / WinSxS / Program Files / AppData 红线等）。
        /// 文件地图用它把「黑区」和「红色高风险」区分开；IsForbidden 复用同一份判定。
        /// </summary>
        public bool IsHardBlocked(string fullPath)
        {
            string norm = PathUtil.Norm(fullPath);
            if (norm.Length == 0) return true;

            if (exactBlock.Contains(norm)) return true;

            for (int i = 0; i < rootSystemFiles.Length; i++)
            {
                if (norm == @"c:\" + rootSystemFiles[i]) return true;
            }

            foreach (string blocked in hardBlock)
            {
                if (PathUtil.Under(norm, blocked)) return true;
            }
            return false;
        }

        public bool IsForbidden(string fullPath)
        {
            string norm = PathUtil.Norm(fullPath);
            if (norm.Length == 0) return true;
            if (IsHardBlocked(norm)) return true;

            Category cat;
            cat = ClassifyFile(norm);

            if (cat == null) return true;
            if (!cat.Actionable) return true;
            if (cat.Id == "recycle_bin") return true;
            return false;
        }

        public List<Category> ActionableCategories()
        {
            List<Category> list = new List<Category>();
            for (int i = 0; i < Categories.Count; i++)
            {
                if (Categories[i].Actionable) list.Add(Categories[i]);
            }
            return list;
        }
    }
}
