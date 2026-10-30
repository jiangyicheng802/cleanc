using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CleanC
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Loc.Init();
            string themeArg = ArgValue(args, "--theme");
            if (string.Equals(themeArg, "dark", StringComparison.OrdinalIgnoreCase)) Theme.Set(true, false);
            else if (string.Equals(themeArg, "light", StringComparison.OrdinalIgnoreCase)) Theme.Set(false, false);
            else Theme.Init();
            float dpiScale = Native.SystemScale();
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            float fit = Math.Min((wa.Width - 24) / 1180f, (wa.Height - 24) / 650f);
            float scale = Math.Min(dpiScale, fit);
            if (scale < 0.8f) scale = 0.8f;
            Theme.Scale = scale;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                LogCrash(e.Exception);
                MessageBox.Show(Loc.T("crash.body") + "\r\n" + CrashPath() + "\r\n\r\n" + e.Exception.Message,
                    Loc.T("crash.title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                if (ex != null) LogCrash(ex);
            };

            if (Has(args, "--selftest"))
            {
                Environment.Exit(RunSelfTest(OutPath(args, "selftest.json")));
            }
            if (Has(args, "--safetytest"))
            {
                Environment.Exit(RunSafetyTest(OutPath(args, "safetytest.json")));
            }
            if (Has(args, "--cleantest"))
            {
                Environment.Exit(RunCleanTest(OutPath(args, "cleantest.json")));
            }
            if (Has(args, "--uninstalltest"))
            {
                Environment.Exit(RunUninstallTest(OutPath(args, "uninstalltest.json")));
            }
            if (Has(args, "--removetest"))
            {
                Environment.Exit(RunRemoveTest(OutPath(args, "removetest.json")));
            }
            if (Has(args, "--regcleantest"))
            {
                Environment.Exit(RunRegCleanTest(OutPath(args, "regcleantest.json")));
            }
            if (Has(args, "--regscan"))
            {
                Environment.Exit(RunRegScan(OutPath(args, "regscan.json")));
            }
            if (Has(args, "--verifytest"))
            {
                Environment.Exit(RunVerifyTest(OutPath(args, "verifytest.json")));
            }
            if (Has(args, "--tasktest"))
            {
                Environment.Exit(RunTaskTest(OutPath(args, "tasktest.json")));
            }
            if (Has(args, "--filetest"))
            {
                Environment.Exit(RunFileTest(OutPath(args, "filetest.json")));
            }
            if (Has(args, "--defensetest"))
            {
                Environment.Exit(RunDefenseTest(OutPath(args, "defensetest.json")));
            }
            string modeArg = ArgValue(args, "--mode");
            if (string.Equals(modeArg, "file", StringComparison.OrdinalIgnoreCase)) MainForm.StartView = 1;
            else if (string.Equals(modeArg, "normal", StringComparison.OrdinalIgnoreCase)) MainForm.StartView = 0;
            MainForm.MapDialog = ArgValue(args, "--mapdialog");
            MainForm.DemoDialog = ArgValue(args, "--uidialog");
            MainForm.AutoScan = Has(args, "--autoscan");
            MainForm.ScrollDemo = Has(args, "--scrolldemo");
            MainForm.FpsProbe = Has(args, "--fpsprobe");
            if (MainForm.FpsProbe)
            {
                string fp = ArgValue(args, "--out");
                MainForm.FpsOut = fp.Length > 0 ? fp : "fpsprobe.json";
            }
            int level = 0;
            string lv = ArgValue(args, "--level");
            if (lv.Length > 0) int.TryParse(lv, out level);
            MainForm.StartLevel = level;
            string themeSwitch = ArgValue(args, "--autotheme");
            if (themeSwitch.Length > 0) int.TryParse(themeSwitch, out MainForm.AutoThemeSwitch);
            string defenseLv = ArgValue(args, "--defenselevel");
            if (defenseLv.Length > 0)
            {
                int dl = 1;
                int.TryParse(defenseLv, out dl);
                Settings.Set("defense.level", dl <= 0 ? "0" : "1");
            }
            if (Has(args, "--defensenable")) Settings.Set("defense.enabled", "1");
            MainForm.StartTab = TabArg(args);
            if (Has(args, "--slowsplash")) SplashOverlay.TimeScale = 0.3f;
            Native.BeginHighResolution();
            Application.Run(new MainForm(Has(args, "--uidemo")));
            Native.EndHighResolution();
        }

        private static int TabArg(string[] args)
        {
            string v = ArgValue(args, "--tab");
            int t;
            if (!int.TryParse(v, out t) || t < 0 || t > 3) t = 0;
            return t;
        }

        private static string ArgValue(string[] args, string flag)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return "";
        }

        private static void LogCrash(Exception ex)
        {
            try
            {
                File.WriteAllText(CrashPath(), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + ex.ToString(),
                    new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private static string CrashPath()
        {
            return Path.Combine(Path.GetTempPath(), "CleanC_crash.log");
        }

        private static bool Has(string[] args, string flag)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string OutPath(string[] args, string fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return fallback;
        }

        private class Check
        {
            public string Path;
            public string ExpectId;
            public bool ExpectDeletable;
            public string ActualId;
            public bool ActualDeletable;
            public bool Ok;
        }

        private static int RunSelfTest(string outPath)
        {
            int code = 0;
            StringBuilder json = new StringBuilder();
            try
            {
                RuleEngine engine = new RuleEngine();
                ScanResult res = Scanner.Scan(engine, null, delegate { return false; });
                List<Check> checks = new List<Check>();
                AddCheck(engine, checks, @"C:\Windows\System32\kernel32.dll", "system_core", false);
                AddCheck(engine, checks, @"C:\Windows\WinSxS\x64_foo\a.dll", "system_core", false);
                AddCheck(engine, checks, @"C:\Windows\Temp\sample.tmp", "system_temp", true);
                AddCheck(engine, checks, @"C:\Windows\SoftwareDistribution\Download\a.cab", "wu_cache", true);
                AddCheck(engine, checks, @"C:\Windows\Prefetch\a.pf", "prefetch", true);
                AddCheck(engine, checks, @"C:\Windows\MEMORY.DMP", "crash_dump", true);
                AddCheck(engine, checks, @"C:\Windows.old\Windows\System32\a.dll", "windows_old", true);
                AddCheck(engine, checks, @"C:\Program Files\App\a.exe", "programs", false);
                AddCheck(engine, checks, @"C:\Windows\Installer\a.msi", "installer", false);
                AddCheck(engine, checks, @"C:\pagefile.sys", "pagefile", false);
                string docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "a.docx");
                AddCheck(engine, checks, docs, "user_files", false);
                string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "a.zip");
                AddCheck(engine, checks, downloads, "user_files", false);
                string temp = Path.Combine(Path.GetTempPath(), "sample.tmp");
                AddCheck(engine, checks, temp, "user_temp", true);

                Category browser = engine.Get("browser_cache");
                if (browser.Roots.Count > 0)
                {
                    AddCheck(engine, checks, Path.Combine(browser.Roots[0], "data_1"), "browser_cache", true);
                }
                Category thumbs = engine.Get("thumb_cache");
                if (thumbs.Roots.Count > 0)
                {
                    AddCheck(engine, checks, Path.Combine(thumbs.Roots[0], "thumbcache_96.db"), "thumb_cache", true);
                    AddCheck(engine, checks, Path.Combine(thumbs.Roots[0], "notes.txt"), "other", false);
                }

                for (int i = 0; i < checks.Count; i++) if (!checks[i].Ok) code = 1;

                StringBuilder fj = new StringBuilder();
                int fjBad = 0;
                AddJudge(engine, fj, ref fjBad, "user_temp",
                    @"C:\Users\Sample\AppData\Local\Temp\a.tmp", "file.kind.temp", RiskLevel.Safe);
                AddJudge(engine, fj, ref fjBad, "user_temp",
                    @"C:\Users\Sample\AppData\Local\Temp\report.pdf", "file.kind.document", RiskLevel.Protected);
                AddJudge(engine, fj, ref fjBad, "thumb_cache",
                    @"C:\Users\Sample\AppData\Local\Microsoft\Windows\Explorer\thumbcache_96.db", "file.kind.thumb", RiskLevel.Safe);
                AddJudge(engine, fj, ref fjBad, "sys_logs",
                    @"C:\Windows\Logs\CBS\cbs.log", "file.kind.log", RiskLevel.Caution);
                AddJudge(engine, fj, ref fjBad, "package_cache",
                    @"C:\ProgramData\Package Cache\setup.msi", "file.kind.installer", RiskLevel.Caution);
                AddJudge(engine, fj, ref fjBad, "user_temp",
                    @"C:\Users\Sample\AppData\Local\Temp\setup.exe", "file.kind.binary", RiskLevel.Caution);
                AddJudge(engine, fj, ref fjBad, "user_temp",
                    @"C:\Users\Sample\AppData\Local\Temp\settings.json", "file.kind.config", RiskLevel.Caution);
                AddJudge(engine, fj, ref fjBad, "crash_dump",
                    @"C:\Windows\Minidump\123456.dmp", "file.kind.dump", RiskLevel.Safe);
                AddJudge(engine, fj, ref fjBad, "prefetch",
                    @"C:\Windows\Prefetch\CLEANC.pf", "file.kind.prefetch", RiskLevel.Caution);
                if (fjBad > 0) code = 1;
                string fjText = fj.ToString();
                if (fjText.EndsWith(",\n", StringComparison.Ordinal)) fjText = fjText.Substring(0, fjText.Length - 2);
                else if (fjText.EndsWith(",", StringComparison.Ordinal)) fjText = fjText.Substring(0, fjText.Length - 1);

                json.Append("{\n");
                json.Append("  \"mode\": \"selftest\",\n");
                json.Append("  \"admin\": ").Append(Native.IsAdmin() ? "true" : "false").Append(",\n");
                json.Append("  \"totalFiles\": ").Append(res.TotalFiles).Append(",\n");
                json.Append("  \"totalBytes\": ").Append(res.TotalBytes).Append(",\n");
                json.Append("  \"deniedDirs\": ").Append(res.DeniedDirs).Append(",\n");
                json.Append("  \"skippedLinks\": ").Append(res.SkippedLinks).Append(",\n");
                json.Append("  \"canceled\": ").Append(res.Canceled ? "true" : "false").Append(",\n");
                json.Append("  \"elapsedMs\": ").Append(res.ElapsedMs).Append(",\n");
                json.Append("  \"fileJudgeBad\": ").Append(fjBad).Append(",\n");
                json.Append("  \"fileJudge\": [\n").Append(fjText).Append("\n  ],\n");
                json.Append("  \"categories\": [\n");
                int first = 1;
                for (int i = 0; i < engine.Categories.Count; i++)
                {
                    Category c = engine.Categories[i];
                    if (!c.Present) continue;
                    if (first == 0) json.Append(",\n");
                    first = 0;
                    json.Append("    {\"id\": \"").Append(c.Id).Append("\", \"name\": \"")
                        .Append(Escape(c.Name)).Append("\", \"risk\": \"").Append(c.Risk.ToString())
                        .Append("\", \"bytes\": ").Append(c.Bytes).Append(", \"files\": ").Append(c.FileCount)
                        .Append(", \"selectedByDefault\": ").Append(c.DefaultSelected ? "true" : "false").Append("}");
                }
                json.Append("\n  ],\n  \"checks\": [\n");
                for (int i = 0; i < checks.Count; i++)
                {
                    Check c = checks[i];
                    json.Append("    {\"path\": \"").Append(Escape(c.Path)).Append("\", \"expect\": \"")
                        .Append(c.ExpectId).Append("\", \"actual\": \"").Append(string.IsNullOrEmpty(c.ActualId) ? "" : c.ActualId)
                        .Append("\", \"deletable\": ").Append(c.ActualDeletable ? "true" : "false")
                        .Append(", \"ok\": ").Append(c.Ok ? "true" : "false").Append("}");
                    if (i < checks.Count - 1) json.Append(",");
                    json.Append("\n");
                }
                json.Append("  ]\n}\n");
            }
            catch (Exception ex)
            {
                code = 1;
                json.Length = 0;
                json.Append("{\"mode\":\"selftest\",\"error\":\"").Append(Escape(ex.ToString())).Append("\"}");
            }
            WriteJson(outPath, json.ToString());
            return code;
        }

        /// <summary>逐文件判定断言：类型与风险都必须符合预期。</summary>
        private static void AddJudge(RuleEngine engine, StringBuilder json, ref int bad,
            string catId, string path, string kind, RiskLevel risk)
        {
            Category c = engine.Get(catId);
            FileJudge.Verdict v = FileJudge.Judge(path, c);
            bool ok = v.KindKey == kind && v.Risk == risk;
            if (!ok) bad++;
            json.Append("    {\"path\": \"").Append(Escape(path))
                .Append("\", \"expectKind\": \"").Append(kind)
                .Append("\", \"actualKind\": \"").Append(v.KindKey)
                .Append("\", \"expectRisk\": \"").Append(risk.ToString())
                .Append("\", \"actualRisk\": \"").Append(v.Risk.ToString())
                .Append("\", \"ok\": ").Append(ok ? "true" : "false").Append("},\n");
        }

        private static int RunCleanTest(string outPath)
        {
            int code = 0;
            StringBuilder json = new StringBuilder();
            string root = Path.Combine(Path.GetTempPath(), "CleanC_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                string sub = Path.Combine(root, "subdir");
                Directory.CreateDirectory(sub);
                long total = 0;
                for (int i = 0; i < 3; i++)
                {
                    string f = Path.Combine(i == 0 ? sub : root, "probe_" + i + ".tmp");
                    File.WriteAllText(f, new string('x', 1024 * (i + 1)));
                    total += new FileInfo(f).Length;
                }
                // 逐文件判定探针：个人文档应保留，可执行文件按“建议确认”正常清理
                string pdf = Path.Combine(root, "report.pdf");
                string exe = Path.Combine(root, "setup.exe");
                File.WriteAllText(pdf, new string('p', 2048));
                File.WriteAllText(exe, new string('e', 4096));

                RuleEngine engine = new RuleEngine();
                Category cat = engine.Get("user_temp");
                cat.Roots.Clear();
                cat.Roots.Add(root);
                Scanner.MeasureCategory(engine, cat);
                cat.Selected = true;
                List<Category> sel = new List<Category>();
                sel.Add(cat);
                CleanResult cr = Cleaner.Clean(engine, sel, Native.IsAdmin(), null, delegate { return false; });

                bool tmpGone = Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories).Length == 0;
                bool subPruned = !Directory.Exists(sub);
                bool pdfKept = File.Exists(pdf);
                bool exeGone = !File.Exists(exe);
                bool logOk = !string.IsNullOrEmpty(cr.LogPath) && File.Exists(cr.LogPath);
                bool freedOk = cr.FreedBytes >= total;
                bool skipOk = cr.Skipped == 1;
                if (!tmpGone || !subPruned || !pdfKept || !exeGone || !logOk || !freedOk || !skipOk) code = 1;

                json.Append("{\n  \"mode\": \"cleantest\",\n");
                json.Append("  \"filesCreated\": 5,\n");
                json.Append("  \"measuredBytes\": ").Append(cat.Bytes).Append(",\n");
                json.Append("  \"freedBytes\": ").Append(cr.FreedBytes).Append(",\n");
                json.Append("  \"deleted\": ").Append(cr.Deleted).Append(",\n");
                json.Append("  \"skipped\": ").Append(cr.Skipped).Append(",\n");
                json.Append("  \"tempFilesGone\": ").Append(tmpGone ? "true" : "false").Append(",\n");
                json.Append("  \"pdfKept\": ").Append(pdfKept ? "true" : "false").Append(",\n");
                json.Append("  \"exeDeleted\": ").Append(exeGone ? "true" : "false").Append(",\n");
                json.Append("  \"emptySubdirPruned\": ").Append(subPruned ? "true" : "false").Append(",\n");
                json.Append("  \"logWritten\": ").Append(logOk ? "true" : "false").Append(",\n");
                json.Append("  \"logPath\": \"").Append(Escape(cr.LogPath)).Append("\"\n}\n");
            }
            catch (Exception ex)
            {
                code = 1;
                json.Length = 0;
                json.Append("{\"mode\":\"cleantest\",\"error\":\"").Append(Escape(ex.ToString())).Append("\"}");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                }
                catch
                {
                }
            }
            WriteJson(outPath, json.ToString());
            return code;
        }

        private static int RunSafetyTest(string outPath)
        {
            int code = 0;
            StringBuilder json = new StringBuilder();
            bool forbidSystem = false;
            bool forbidDocs = false;
            bool forbidPage = false;
            bool probeClassified = false;
            bool probeDeleted = false;
            bool tempAllowed = false;
            try
            {
                RuleEngine engine = new RuleEngine();
                forbidSystem = engine.IsForbidden(@"C:\Windows\System32\kernel32.dll");
                forbidDocs = engine.IsForbidden(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "a.docx"));
                forbidPage = engine.IsForbidden(@"C:\pagefile.sys");
                tempAllowed = !engine.IsForbidden(@"C:\Windows\Temp\probe.tmp");

                string probe = Path.Combine(Path.GetTempPath(), "cleanc_probe_" + Guid.NewGuid().ToString("N") + ".txt");
                File.WriteAllText(probe, "probe");
                Category pc = engine.ClassifyFile(probe);
                probeClassified = pc != null && pc.Id == "user_temp" && !engine.IsForbidden(probe);
                Cleaner.TryDeleteProbe(probe);
                probeDeleted = !File.Exists(probe);

                // AppData 红线：Roaming / LocalLow / Local 根目录一律禁止，Local 子目录仍可清理
                string me = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string roaming = Path.Combine(me, @"AppData\Roaming\Slack\Cache\probe.bin");
                string low = Path.Combine(me, @"AppData\LocalLow\Foo\Cache\probe.bin");
                string localRoot = Path.Combine(me, @"AppData\Local");
                string localTemp = Path.Combine(me, @"AppData\Local\Temp\probe.tmp");
                bool roamingForbidden = engine.IsForbidden(roaming);
                bool localLowForbidden = engine.IsForbidden(low);
                bool localRootForbidden = engine.IsForbidden(localRoot);
                bool localTempAllowed = !engine.IsForbidden(localTemp);
                Category ac = engine.Get("app_cache");
                bool appCacheRoots = ac != null && ac.Roots.Count > 0;
                bool appCacheClassified = appCacheRoots &&
                    engine.ClassifyFile(Path.Combine(ac.Roots[0], "probe.bin")).Id == "app_cache";

                if (!forbidSystem || !forbidDocs || !forbidPage || !probeClassified || !probeDeleted || !tempAllowed
                    || !roamingForbidden || !localLowForbidden || !localRootForbidden || !localTempAllowed
                    || !appCacheRoots || !appCacheClassified)
                    code = 1;

                json.Append("{\n  \"mode\": \"safetytest\",\n");
                json.Append("  \"system32Forbidden\": ").Append(forbidSystem ? "true" : "false").Append(",\n");
                json.Append("  \"documentsForbidden\": ").Append(forbidDocs ? "true" : "false").Append(",\n");
                json.Append("  \"pagefileForbidden\": ").Append(forbidPage ? "true" : "false").Append(",\n");
                json.Append("  \"windowTempAllowed\": ").Append(tempAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"probeClassifiedSafe\": ").Append(probeClassified ? "true" : "false").Append(",\n");
                json.Append("  \"probeDeleted\": ").Append(probeDeleted ? "true" : "false").Append(",\n");
                json.Append("  \"roamingForbidden\": ").Append(roamingForbidden ? "true" : "false").Append(",\n");
                json.Append("  \"localLowForbidden\": ").Append(localLowForbidden ? "true" : "false").Append(",\n");
                json.Append("  \"localRootForbidden\": ").Append(localRootForbidden ? "true" : "false").Append(",\n");
                json.Append("  \"localTempAllowed\": ").Append(localTempAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"appCacheRoots\": ").Append(ac == null ? 0 : ac.Roots.Count).Append(",\n");
                json.Append("  \"appCacheClassified\": ").Append(appCacheClassified ? "true" : "false").Append("\n}\n");
            }
            catch (Exception ex)
            {
                code = 1;
                json.Length = 0;
                json.Append("{\"mode\":\"safetytest\",\"error\":\"").Append(Escape(ex.Message)).Append("\"}");
            }
            WriteJson(outPath, json.ToString());
            return code;
        }

        private static int RunUninstallTest(string outPath)
        {
            int code = 0;
            StringBuilder json = new StringBuilder();
            try
            {
                RuleEngine engine = new RuleEngine();
                Category junk = engine.Get("reg_junk");
                bool junkCat = junk != null && junk.Registry;

                bool runKeyDenied = !RegGuard.AllowedJunkPath(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true, "");
                bool runValueAllowed = RegGuard.AllowedJunkPath(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false, "SomeApp");
                bool systemDenied = !RegGuard.AllowedJunkPath(
                    @"SYSTEM\CurrentControlSet\Control\Session Manager", true, "");
                bool serviceDenied = !RegGuard.AllowedJunkPath(
                    @"SYSTEM\CurrentControlSet\Services\Tcpip", true, "");
                bool serviceAllowed = RegGuard.AllowedJunkPath(
                    @"SYSTEM\CurrentControlSet\Services\QuarkDriveSvc", true, "");
                bool softwareLeafAllowed = RegGuard.AllowedJunkPath(@"SOFTWARE\Quark", true, "");
                bool softwareLeafDenied = !RegGuard.AllowedJunkPath(@"SOFTWARE\Microsoft", true, "");
                bool uninstallAllowed = RegGuard.AllowedJunkPath(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{11111111-2222}", true, "");
                bool appPathAllowed = RegGuard.AllowedJunkPath(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\foo.exe", true, "");

                bool classesClsidAllowed = RegGuard.AllowedJunkPath(
                    @"SOFTWARE\Classes\CLSID\{11111111-2222-3333-4444-555555555555}", true, "");
                bool classesMenuAllowed = RegGuard.AllowedJunkPath(
                    @"SOFTWARE\Classes\Directory\Background\shellex\ContextMenuHandlers\Foo", true, "");
                bool classesAppAllowed = RegGuard.AllowedJunkPath(
                    @"SOFTWARE\Classes\Applications\foo.exe", true, "");
                bool installerAllowed = RegGuard.AllowedJunkPath(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\ABC\InstallProperties", true, "");
                bool eventLogAllowed = RegGuard.AllowedJunkPath(
                    @"SYSTEM\CurrentControlSet\Services\EventLog\Application\CleanCProbe", true, "");
                bool enumAllowed = RegGuard.AllowedJunkPath(
                    @"SYSTEM\CurrentControlSet\Enum\USB\VID_0000&PID_0000\INSTANCE", true, "");
                bool filterValueAllowed = RegGuard.AllowedJunkPath(
                    @"SYSTEM\CurrentControlSet\Control\Class\{11111111-2222-3333-4444-555555555555}", false, "UpperFilters");
                bool softwareDeepValueAllowed = RegGuard.AllowedSoftwareValue(
                    @"SOFTWARE\SomeVendor\SomeProduct\Settings", "InstallPath");
                bool softwareDeepValueDenied = !RegGuard.AllowedSoftwareValue(
                    @"SOFTWARE\Microsoft\Windows\Foo", "InstallPath");

                // 驱动强制删除脚本：必须包含管理员 pnputil + takeown / icacls 回退
                string driverScript = RegRunner.PreviewDriverScript("oem12.inf");
                string driverStore = ResidualActions.DriverStoreFolder("oem12.inf");
                bool driverStoreMapped = driverStore.Length > 0;
                bool driverBatOk = driverScript.IndexOf("pnputil /delete-driver", StringComparison.Ordinal) >= 0
                    && driverScript.IndexOf("takeown /f", StringComparison.Ordinal) >= 0
                    && driverScript.IndexOf("icacls", StringComparison.Ordinal) >= 0
                    && (!driverStoreMapped || driverScript.IndexOf("rd /s /q", StringComparison.Ordinal) >= 0);

                ProgramEntry chrome = new ProgramEntry();
                chrome.Name = "Google Chrome";
                chrome.Publisher = "Google LLC";
                bool appKeyAllowed = RegGuard.AllowedAppKey(@"SOFTWARE\Google\Chrome", chrome);
                bool appKeyDenied = !RegGuard.AllowedAppKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", chrome);
                bool appKeyDenied2 = !RegGuard.AllowedAppKey(@"SOFTWARE\Microsoft", chrome);

                List<ProgramEntry> progs = Uninstaller.Enumerate(null, null);
                int protectedCount = 0;
                string sampleProtected = "";
                for (int i = 0; i < progs.Count; i++)
                {
                    if (!progs[i].Protected) continue;
                    protectedCount++;
                    if (sampleProtected.Length == 0) sampleProtected = progs[i].Name;
                }

                List<RegItem> items = RegistryJunk.Scan(null, null);
                int safeJunk = 0;
                for (int i = 0; i < items.Count; i++) if (items[i].Risk == RiskLevel.Safe) safeJunk++;

                bool ok = junkCat && runKeyDenied && runValueAllowed && systemDenied && serviceDenied
                    && serviceAllowed && softwareLeafAllowed && softwareLeafDenied && uninstallAllowed
                    && appPathAllowed && appKeyAllowed && appKeyDenied && appKeyDenied2 && progs.Count > 0
                    && classesClsidAllowed && classesMenuAllowed && classesAppAllowed && installerAllowed
                    && eventLogAllowed && enumAllowed && filterValueAllowed && softwareDeepValueAllowed
                    && softwareDeepValueDenied && driverBatOk;
                if (!ok) code = 1;

                json.Append("{\n  \"mode\": \"uninstalltest\",\n");
                json.Append("  \"admin\": ").Append(Native.IsAdmin() ? "true" : "false").Append(",\n");
                json.Append("  \"regJunkCategory\": ").Append(junkCat ? "true" : "false").Append(",\n");
                json.Append("  \"guardRunKeyDeleteDenied\": ").Append(runKeyDenied ? "true" : "false").Append(",\n");
                json.Append("  \"guardRunValueAllowed\": ").Append(runValueAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"guardSystemKeyDenied\": ").Append(systemDenied ? "true" : "false").Append(",\n");
                json.Append("  \"guardServiceDenied\": ").Append(serviceDenied ? "true" : "false").Append(",\n");
                json.Append("  \"guardServiceAllowed\": ").Append(serviceAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"guardSoftwareLeafAllowed\": ").Append(softwareLeafAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"guardSoftwareLeafDenied\": ").Append(softwareLeafDenied ? "true" : "false").Append(",\n");
                json.Append("  \"guardUninstallKeyAllowed\": ").Append(uninstallAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"guardAppPathAllowed\": ").Append(appPathAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"guardClassesClsidAllowed\": ").Append(classesClsidAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"guardClassesMenuAllowed\": ").Append(classesMenuAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"guardClassesAppAllowed\": ").Append(classesAppAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"guardInstallerAllowed\": ").Append(installerAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"guardEventLogAllowed\": ").Append(eventLogAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"guardEnumAllowed\": ").Append(enumAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"guardFilterValueAllowed\": ").Append(filterValueAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"guardSoftwareDeepValueAllowed\": ").Append(softwareDeepValueAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"guardSoftwareDeepValueDenied\": ").Append(softwareDeepValueDenied ? "true" : "false").Append(",\n");
                json.Append("  \"driverStoreMapped\": ").Append(driverStoreMapped ? "true" : "false").Append(",\n");
                json.Append("  \"driverStorePath\": \"").Append(Escape(driverStore)).Append("\",\n");
                json.Append("  \"driverForceBat\": ").Append(driverBatOk ? "true" : "false").Append(",\n");
                json.Append("  \"appKeyAllowed\": ").Append(appKeyAllowed ? "true" : "false").Append(",\n");
                json.Append("  \"appKeyDenied\": ").Append(appKeyDenied ? "true" : "false").Append(",\n");
                json.Append("  \"appKeyDeniedMicrosoft\": ").Append(appKeyDenied2 ? "true" : "false").Append(",\n");
                json.Append("  \"programs\": ").Append(progs.Count).Append(",\n");
                json.Append("  \"protectedPrograms\": ").Append(protectedCount).Append(",\n");
                json.Append("  \"protectedSample\": \"").Append(Escape(sampleProtected)).Append("\",\n");
                json.Append("  \"registryJunkItems\": ").Append(items.Count).Append(",\n");
                json.Append("  \"registryJunkSafe\": ").Append(safeJunk).Append(",\n");
                json.Append("  \"ok\": ").Append(ok ? "true" : "false").Append("\n}\n");
            }
            catch (Exception ex)
            {
                code = 1;
                json.Length = 0;
                json.Append("{\"mode\":\"uninstalltest\",\"error\":\"").Append(Escape(ex.ToString())).Append("\"}");
            }
            WriteJson(outPath, json.ToString());
            return code;
        }

        /// <summary>第 16 步验证的无界面测试：输出六项检查的状态。</summary>
        private static int RunVerifyTest(string outPath)
        {
            StringBuilder json = new StringBuilder();
            try
            {
                List<VerifyCheck> checks = ResidualVerify.Quick(0, null);
                json.Append("{\n  \"mode\": \"verifytest\",\n  \"checks\": [\n");
                for (int i = 0; i < checks.Count; i++)
                {
                    json.Append("    {\"label\": \"").Append(Escape(checks[i].Label))
                        .Append("\", \"value\": \"").Append(Escape(checks[i].Value))
                        .Append("\", \"state\": ").Append(checks[i].State).Append("}")
                        .Append(i + 1 < checks.Count ? ",\n" : "\n");
                }
                json.Append("  ]\n}\n");
            }
            catch (Exception ex)
            {
                json.Length = 0;
                json.Append("{\"error\":\"").Append(Escape(ex.Message)).Append("\"}");
            }
            WriteJson(outPath, json.ToString());
            return 0;
        }

        /// <summary>计划任务残留真机验证：建任务 → 扫描命中 → RegRunner 删除 → 回查。</summary>
        private static int RunTaskTest(string outPath)
        {
            int code = 0;
            StringBuilder json = new StringBuilder();
            string tag = Guid.NewGuid().ToString("N").Substring(0, 6);
            string taskName = @"\CleanCProbeTask_" + tag;
            bool created = false;
            bool found = false;
            bool deleted = false;
            bool gone = false;
            string error = "";
            try
            {
                string o;
                int cc = Cmd.Run(Cmd.SysExe("schtasks.exe"),
                    "/Create /TN \"" + taskName + "\" /TR \"C:\\__cleanc_nonexistent__\\probe.exe\" /SC ONCE /ST 23:59 /F",
                    null, out o, 20000);
                created = cc == 0;
                if (!created) throw new Exception("schtasks create failed, code=" + cc);

                List<RegItem> list = new List<RegItem>();
                ResidualScan.ScanTasks(list, new HashSet<string>(StringComparer.OrdinalIgnoreCase), null);
                RegItem hit = null;
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].Aux, taskName, StringComparison.OrdinalIgnoreCase)) hit = list[i];
                }
                found = hit != null;

                if (hit != null)
                {
                    hit.Selected = true;
                    string backup = Path.Combine(Path.GetTempPath(), "cleanc_tasktest_" + tag);
                    RegRunResult r = RegRunner.Run(new List<RegItem> { hit }, backup, false, null);
                    deleted = r.Deleted == 1;
                    try { if (Directory.Exists(backup)) Directory.Delete(backup, true); } catch { }
                }
                gone = !ResidualActions.TaskExists(taskName);
                if (!created || !found || !deleted || !gone) code = 1;
            }
            catch (Exception ex)
            {
                code = 1;
                error = ex.Message;
            }
            finally
            {
                try
                {
                    string o;
                    Cmd.Run(Cmd.SysExe("schtasks.exe"), "/Delete /TN \"" + taskName + "\" /F", null, out o, 20000);
                }
                catch
                {
                }
            }
            json.Append("{\n  \"mode\": \"tasktest\",\n");
            json.Append("  \"created\": ").Append(created ? "true" : "false").Append(",\n");
            json.Append("  \"scanFound\": ").Append(found ? "true" : "false").Append(",\n");
            json.Append("  \"runnerDeleted\": ").Append(deleted ? "true" : "false").Append(",\n");
            json.Append("  \"taskGone\": ").Append(gone ? "true" : "false").Append(",\n");
            json.Append("  \"ok\": ").Append(code == 0 ? "true" : "false");
            if (error.Length > 0) json.Append(",\n  \"error\": \"").Append(Escape(error)).Append("\"");
            json.Append("\n}\n");
            WriteJson(outPath, json.ToString());
            return code;
        }

        /// <summary>把注册表扫描结果原样导出，便于核对都扫到了什么。</summary>
        private static int RunRegScan(string outPath)
        {
            StringBuilder json = new StringBuilder();
            try
            {
                List<RegItem> items = RegistryJunk.Scan(null, null);
                json.Append("{\n  \"total\": ").Append(items.Count).Append(",\n  \"infRefs\": ").Append(ResidualScan.LastInfReferenceCount).Append(",\n  \"items\": [\n");
                for (int i = 0; i < items.Count; i++)
                {
                    RegItem it = items[i];
                    json.Append("    {\"kind\": \"").Append(it.Kind.ToString())
                        .Append("\", \"step\": ").Append(it.Step)
                        .Append(", \"hive\": \"").Append(Escape(it.Hive))
                        .Append("\", \"key\": \"").Append(Escape(it.KeyPath))
                        .Append("\", \"value\": \"").Append(Escape(it.ValueName == null ? "" : it.ValueName))
                        .Append("\", \"aux\": \"").Append(Escape(it.Aux == null ? "" : it.Aux))
                        .Append("\", \"file\": \"").Append(Escape(it.FilePath == null ? "" : it.FilePath))
                        .Append("\", \"label\": \"").Append(Escape(it.Label))
                        .Append("\", \"reason\": \"").Append(Escape(it.Reason))
                        .Append("\", \"selected\": ").Append(it.Selected ? "true" : "false")
                        .Append("}").Append(i + 1 < items.Count ? ",\n" : "\n");
                }
                json.Append("  ]\n}\n");
            }
            catch (Exception ex)
            {
                json.Length = 0;
                json.Append("{\"error\":\"").Append(Escape(ex.Message)).Append("\"}");
            }
            WriteJson(outPath, json.ToString());
            return 0;
        }

        /// <summary>
        /// 注册表删除真机验证：造一条假的孤立卸载项 → 扫描 → 删除 → 回查键是否真的消失。
        /// </summary>
        private static int RunRegCleanTest(string outPath)
        {
            int code = 0;
            StringBuilder json = new StringBuilder();
            string tag = Guid.NewGuid().ToString("N").Substring(0, 8);
            string uninstall = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CleanC_Probe_" + tag;
            string backup = Path.Combine(Path.GetTempPath(), "CleanC_regtest_" + tag);
            string name = "CleanCProbe" + tag;
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), name);
            string folderBak = folder + ".CleanC_bak";
            string tempProbe = Path.Combine(Path.GetTempPath(), "CleanC_tmpprobe_" + tag + ".tmp");
            bool created = false;
            bool found = false;
            bool gone = false;
            bool folderFound = false;
            bool folderRenamed = false;
            bool tempFound = false;
            bool tempGone = false;
            int scanned = 0;
            int deletedCount = 0;
            int skipped = 0;
            int regKeys = 0;
            string error = "";
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(uninstall))
                {
                    k.SetValue("DisplayName", name);
                    k.SetValue("Publisher", "CleanC Test");
                    k.SetValue("UninstallString", @"C:\__cleanc_nonexistent__\probe.exe");
                    k.SetValue("InstallLocation", @"C:\__cleanc_nonexistent__");
                }
                created = true;

                // 残留目录：在 LocalAppData 下造一个没有 exe 的同名目录
                try
                {
                    if (Directory.Exists(folder)) Directory.Delete(folder, true);
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(Path.Combine(folder, "leftover.txt"), "probe");
                }
                catch
                {
                }

                // 临时文件探针：10 MB、30 天未修改，第 15 步应能扫到并按 TempFile 删除
                try
                {
                    byte[] buf = new byte[10 * 1024 * 1024];
                    File.WriteAllBytes(tempProbe, buf);
                    File.SetLastWriteTime(tempProbe, DateTime.Now.AddDays(-30));
                }
                catch
                {
                }

                List<RegItem> items = RegistryJunk.Scan(null, null);
                scanned = items.Count;
                RegItem hit = null;
                RegItem dirHit = null;
                RegItem tmpHit = null;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].Hive == "HKEY_CURRENT_USER" &&
                        items[i].KeyPath.EndsWith("CleanC_Probe_" + tag, StringComparison.OrdinalIgnoreCase))
                    {
                        hit = items[i];
                    }
                    if (items[i].Kind == RegKind.Folder &&
                        string.Equals(items[i].FilePath, folder, StringComparison.OrdinalIgnoreCase))
                    {
                        dirHit = items[i];
                    }
                    if (items[i].Kind == RegKind.TempFile &&
                        string.Equals(items[i].FilePath, tempProbe, StringComparison.OrdinalIgnoreCase))
                    {
                        tmpHit = items[i];
                    }
                }
                found = hit != null;
                folderFound = dirHit != null;
                tempFound = tmpHit != null;

                List<RegItem> todo = new List<RegItem>();
                if (hit != null) todo.Add(hit);
                if (dirHit != null) todo.Add(dirHit);
                if (tmpHit != null) todo.Add(tmpHit);

                if (todo.Count > 0)
                {
                    for (int i = 0; i < todo.Count; i++) todo[i].Selected = true;
                    CleanResult cr = new CleanResult();
                    Directory.CreateDirectory(backup);
                    using (StreamWriter log = new StreamWriter(Path.Combine(backup, "clean.log"), false, Encoding.UTF8))
                    {
                        RegistryJunk.Clean(todo, backup, log, cr, null, false);
                    }
                    deletedCount = cr.Deleted;
                    skipped = cr.Skipped;
                    regKeys = cr.RegKeys;
                }

                using (RegistryKey chk = Registry.CurrentUser.OpenSubKey(uninstall))
                {
                    gone = chk == null;
                }
                folderRenamed = !Directory.Exists(folder) && Directory.Exists(folderBak);
                tempGone = !File.Exists(tempProbe);

                if (!created || !found || !gone || !folderFound || !folderRenamed || !tempFound || !tempGone) code = 1;
            }
            catch (Exception ex)
            {
                code = 1;
                error = ex.Message;
            }
            finally
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(uninstall, false); } catch { }
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
                try { if (Directory.Exists(folderBak)) Directory.Delete(folderBak, true); } catch { }
                try { if (File.Exists(tempProbe)) File.Delete(tempProbe); } catch { }
                try { if (Directory.Exists(backup)) Directory.Delete(backup, true); } catch { }
            }

            json.Append("{\n  \"mode\": \"regcleantest\",\n");
            json.Append("  \"probeCreated\": ").Append(created ? "true" : "false").Append(",\n");
            json.Append("  \"scannedItems\": ").Append(scanned).Append(",\n");
            json.Append("  \"probeFound\": ").Append(found ? "true" : "false").Append(",\n");
            json.Append("  \"folderFound\": ").Append(folderFound ? "true" : "false").Append(",\n");
            json.Append("  \"folderRenamed\": ").Append(folderRenamed ? "true" : "false").Append(",\n");
            json.Append("  \"tempFileFound\": ").Append(tempFound ? "true" : "false").Append(",\n");
            json.Append("  \"tempFileDeleted\": ").Append(tempGone ? "true" : "false").Append(",\n");
            json.Append("  \"deleted\": ").Append(deletedCount).Append(",\n");
            json.Append("  \"regKeys\": ").Append(regKeys).Append(",\n");
            json.Append("  \"skipped\": ").Append(skipped).Append(",\n");
            json.Append("  \"keyGone\": ").Append(gone ? "true" : "false").Append(",\n");
            json.Append("  \"ok\": ").Append(code == 0 ? "true" : "false");
            if (error.Length > 0) json.Append(",\n  \"error\": \"").Append(Escape(error)).Append("\"");
            json.Append("\n}\n");
            WriteJson(outPath, json.ToString());
            return code;
        }

        private static int RunRemoveTest(string outPath)        {
            int code = 0;
            StringBuilder json = new StringBuilder();
            string root = Path.Combine(Path.GetTempPath(), "CleanC_rm_" + Guid.NewGuid().ToString("N"));
            bool normalGone = false;
            bool forceGone = false;
            bool protectedRefused = false;
            long normalFreed = 0;
            long forceFreed = 0;
            int normalFail = 0;
            int forceFail = 0;
            int regTouched = 0;
            try
            {
                long total = MakeProbeTree(root);

                ProgramEntry probe = new ProgramEntry();
                probe.Name = "CleanC Probe " + Guid.NewGuid().ToString("N").Substring(0, 6);
                probe.Publisher = "CleanC Test";
                probe.InstallDir = root;
                probe.Bytes = total;
                probe.FileCount = 5;

                UninstallReport n = Uninstaller.NormalRemove(probe, Native.IsAdmin(), null, null, null);
                normalFreed = n.BytesFreed;
                normalFail = n.Failures;
                normalGone = !Directory.Exists(root) ||
                    Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length == 0;

                long total2 = MakeProbeTree(root);
                probe.Bytes = total2;
                UninstallReport f = Uninstaller.ForceRemove(probe, Native.IsAdmin(), null, null, null);
                forceFreed = f.BytesFreed;
                forceFail = f.Failures;
                regTouched = f.RegKeys;
                forceGone = !Directory.Exists(root) ||
                    Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length == 0;

                ProgramEntry prot = new ProgramEntry();
                prot.Name = "Microsoft Visual C++ Redistributable";
                prot.Publisher = "Microsoft Corporation";
                prot.InstallDir = @"C:\Program Files\Microsoft\__CleanC_Probe_DoesNotExist__";
                prot.Bytes = 1;
                Uninstaller.EvaluateProtection(prot);
                UninstallReport pf = Uninstaller.ForceRemove(prot, Native.IsAdmin(), null, null, null);
                protectedRefused = pf.Refused && prot.Protected;

                bool ok = normalGone && forceGone && normalFreed > 0 && forceFreed > 0 &&
                    normalFail == 0 && forceFail == 0 && regTouched == 0 && protectedRefused;
                if (!ok) code = 1;

                json.Append("{\n  \"mode\": \"removetest\",\n");
                json.Append("  \"normalFilesGone\": ").Append(normalGone ? "true" : "false").Append(",\n");
                json.Append("  \"normalFreedBytes\": ").Append(normalFreed).Append(",\n");
                json.Append("  \"normalFailures\": ").Append(normalFail).Append(",\n");
                json.Append("  \"forceFilesGone\": ").Append(forceGone ? "true" : "false").Append(",\n");
                json.Append("  \"forceFreedBytes\": ").Append(forceFreed).Append(",\n");
                json.Append("  \"forceFailures\": ").Append(forceFail).Append(",\n");
                json.Append("  \"forceRegKeysTouched\": ").Append(regTouched).Append(",\n");
                json.Append("  \"protectedRefused\": ").Append(protectedRefused ? "true" : "false").Append(",\n");
                json.Append("  \"ok\": ").Append(ok ? "true" : "false").Append("\n}\n");
            }
            catch (Exception ex)
            {
                code = 1;
                json.Length = 0;
                json.Append("{\"mode\":\"removetest\",\"error\":\"").Append(Escape(ex.ToString())).Append("\"}");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                }
                catch
                {
                }
            }
            WriteJson(outPath, json.ToString());
            return code;
        }

        private static long MakeProbeTree(string root)
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(root);
            string sub = Path.Combine(root, "bin");
            Directory.CreateDirectory(sub);
            long total = 0;
            for (int i = 0; i < 4; i++)
            {
                string f = Path.Combine(i < 2 ? root : sub, "probe_" + i + ".dat");
                File.WriteAllText(f, new string('x', 2048 * (i + 1)));
                total += new FileInfo(f).Length;
            }
            return total;
        }

        private static void AddCheck(RuleEngine engine, List<Check> checks, string path, string expectId, bool expectDeletable)
        {
            Check c = new Check();
            c.Path = path;
            c.ExpectId = expectId;
            c.ExpectDeletable = expectDeletable;
            Category cat = engine.ClassifyFile(path);
            c.ActualId = cat == null ? "" : cat.Id;
            c.ActualDeletable = !engine.IsForbidden(path);
            c.Ok = c.ActualId == expectId && c.ActualDeletable == expectDeletable;
            checks.Add(c);
        }

        /// <summary>主动防御自检：哈希、规则判定、启动项扫描、签名探针。</summary>
        private static int RunDefenseTest(string outPath)
        {
            int code = 0;
            StringBuilder json = new StringBuilder();
            string root = Path.Combine(Path.GetTempPath(), "CleanC_defense_" + Guid.NewGuid().ToString("N"));
            json.Append("{\n  \"mode\": \"defensetest\",\n");
            try
            {
                Directory.CreateDirectory(root);
                string probe = Path.Combine(root, "probe.bin");
                File.WriteAllText(probe, "baseline");
                string h1 = IntegritySurvey.HashFile(probe);
                File.WriteAllText(probe, "tampered");
                string h2 = IntegritySurvey.HashFile(probe);
                bool hashOk = h1.Length == 64 && h2.Length == 64 && !string.Equals(h1, h2, StringComparison.Ordinal);

                bool doubleExt = DefenseRules.HasDoubleExtension(Path.Combine(root, "invoice.pdf.exe"));
                bool plainExe = !DefenseRules.HasDoubleExtension(Path.Combine(root, "invoice.exe"));
                bool suspicious = DefenseRules.IsSuspiciousPath(@"C:\Users\Sample\AppData\Local\Temp\evil.exe");
                bool programFiles = !DefenseRules.IsSuspiciousPath(@"C:\Program Files\App\app.exe");
                string cmd = "\"C:\\Users\\Sample\\AppData\\Local\\Temp\\evil.exe\" -silent";
                string target = DefenseRules.ExtractTarget(cmd);
                bool targetOk = PathUtil.Norm(target) == PathUtil.Norm(@"C:\Users\Sample\AppData\Local\Temp\evil.exe");
                bool fake = DefenseRules.ImpersonatesSystem(@"C:\Users\Sample\AppData\Local\Temp\svchost.exe");
                bool realSys = !DefenseRules.ImpersonatesSystem(Path.Combine(Environment.SystemDirectory, "svchost.exe"));
                bool signed = DefenseSignature.Verify(Path.Combine(Environment.SystemDirectory, "kernel32.dll"));
                bool msUpdate = MsUpdateProbe();

                int startup = 0;
                bool scanOk = true;
                string scanError = "";
                try { startup = HazardScanner.Scan(DefenseLevel.High, true).Count; }
                catch (Exception ex) { scanOk = false; scanError = ex.Message; }

                // 处置链路：隔离文件 → 源文件消失 → 清理隔离区探针
                bool quarantineOk = false;
                try
                {
                    string qsrc = Path.Combine(root, "CleanC_qprobe.exe");
                    File.WriteAllText(qsrc, "probe");
                    Threat qt = new Threat();
                    qt.Kind = ThreatKind.SuspiciousFile;
                    qt.Target = qsrc;
                    qt.Fix = ThreatFix.QuarantineFile;
                    qt.Id = "qtest";
                    qt.Title = "quarantine probe";
                    string qmsg;
                    quarantineOk = DefenseCenter.Quarantine(qt, out qmsg) && !File.Exists(qsrc);
                    string[] qfiles = Directory.GetFiles(DefensePaths.QuarantineDir, "*CleanC_qprobe.exe.quarantined");
                    for (int i = 0; i < qfiles.Length; i++) File.Delete(qfiles[i]);
                }
                catch
                {
                    quarantineOk = false;
                }

                bool baselineExists = IntegritySurvey.BaselineExists();
                bool ok = hashOk && doubleExt && plainExe && suspicious && programFiles && targetOk && fake
                    && realSys && msUpdate && scanOk && quarantineOk;
                if (!ok) code = 1;

                json.Append("  \"hashChanged\": ").Append(hashOk ? "true" : "false").Append(",\n");
                json.Append("  \"doubleExtension\": ").Append(doubleExt ? "true" : "false").Append(",\n");
                json.Append("  \"plainExeClean\": ").Append(plainExe ? "true" : "false").Append(",\n");
                json.Append("  \"tempSuspicious\": ").Append(suspicious ? "true" : "false").Append(",\n");
                json.Append("  \"programFilesTrusted\": ").Append(programFiles ? "true" : "false").Append(",\n");
                json.Append("  \"extractTarget\": \"").Append(Escape(target)).Append("\",\n");
                json.Append("  \"targetOk\": ").Append(targetOk ? "true" : "false").Append(",\n");
                json.Append("  \"fakeSystemName\": ").Append(fake ? "true" : "false").Append(",\n");
                json.Append("  \"realSystemName\": ").Append(realSys ? "true" : "false").Append(",\n");
                json.Append("  \"kernel32Signed\": ").Append(signed ? "true" : "false").Append(",\n");
                json.Append("  \"microsoftUpdateProbe\": ").Append(msUpdate ? "true" : "false").Append(",\n");
                json.Append("  \"startupItems\": ").Append(startup).Append(",\n");
                json.Append("  \"startupScanOk\": ").Append(scanOk ? "true" : "false").Append(",\n");
                json.Append("  \"quarantineOk\": ").Append(quarantineOk ? "true" : "false").Append(",\n");
                json.Append("  \"baselineExists\": ").Append(baselineExists ? "true" : "false").Append(",\n");
                json.Append("  \"ok\": ").Append(ok ? "true" : "false");
                if (scanError.Length > 0) json.Append(",\n  \"scanError\": \"").Append(Escape(scanError)).Append("\"");
                json.Append("\n}\n");
            }
            catch (Exception ex)
            {
                code = 1;
                json.Length = 0;
                json.Append("{\"mode\":\"defensetest\",\"error\":\"").Append(Escape(ex.ToString())).Append("\"}");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                }
                catch
                {
                }
            }
            WriteJson(outPath, json.ToString());
            return code;
        }

        /// <summary>用系统目录里的 kernel32.dll 版本信息验证「微软更新」白名单逻辑。</summary>
        private static bool MsUpdateProbe()
        {
            try
            {
                string path = Path.Combine(Environment.SystemDirectory, "kernel32.dll");
                if (!File.Exists(path)) return false;
                System.Diagnostics.FileVersionInfo vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
                string company = vi.CompanyName == null ? "" : vi.CompanyName;
                return company.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>文件地图自检：目录树体积、四级风险映射、squarified 布局覆盖。</summary>
        private static int RunFileTest(string outPath)
        {
            int code = 0;
            StringBuilder json = new StringBuilder();
            json.Append("{\n  \"mode\": \"filetest\",\n");
            string root = Path.Combine(Path.GetTempPath(), "CleanC_filetest");
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(root);
                string a = Path.Combine(root, "a");
                string b = Path.Combine(a, "b");
                Directory.CreateDirectory(b);
                File.WriteAllText(Path.Combine(root, "one.bin"), new string('x', 1024));
                File.WriteAllText(Path.Combine(a, "two.bin"), new string('x', 2048));
                File.WriteAllText(Path.Combine(b, "three.bin"), new string('x', 4096));
                File.WriteAllText(Path.Combine(root, "report.docx"), new string('d', 2048));
                long expected = 1024 + 2048 + 4096;
                long total = expected + 2048;

                RuleEngine engine = new RuleEngine();
                MapNode mapRoot = FileMapScanner.Scan(root, engine, null, null);
                bool sizeOk = mapRoot != null && mapRoot.Size == total && mapRoot.FileCount == 4;
                MapNode bNode = mapRoot == null ? null : MapTree.Find(mapRoot, b);
                bool childOk = bNode != null && bNode.Size == 4096 && bNode.FileCount == 1;

                bool r1 = RiskOfPath(engine, @"C:\Windows\System32\kernel32.dll") == FileRisk.Forbidden;
                bool r2 = RiskOfPath(engine, @"C:\Windows\Temp\a.tmp") == FileRisk.Safe;
                bool r3 = RiskOfPath(engine, @"C:\Windows\Logs\a.log") == FileRisk.Caution;
                bool r4 = RiskOfPath(engine, @"C:\Users\Sample\Documents\a.docx") == FileRisk.Risky;
                bool r5 = RiskOfPath(engine, @"C:\ProgramData\Package Cache\a.exe") == FileRisk.Caution;
                bool r6 = RiskOfPath(engine, @"C:\Program Files\a.exe") == FileRisk.Forbidden;
                bool riskOk = r1 && r2 && r3 && r4 && r5 && r6;

                MapNode syn = new MapNode("C:\\t", @"C:\t", true);
                long[] sizes = { 4096, 3072, 2048, 1024 };
                for (int i = 0; i < 4; i++)
                {
                    MapNode c = new MapNode("f" + i, Path.Combine(@"C:\t", "f" + i), false);
                    c.Size = sizes[i];
                    c.FileCount = 1;
                    c.Parent = syn;
                    syn.Children.Add(c);
                }
                List<MapRect> rects = new List<MapRect>();
                MapLayout.Build(syn, new RectangleF(0, 0, 400, 300), rects);
                bool inside = rects.Count >= 5;
                double area = 0;
                for (int i = 0; i < rects.Count; i++)
                {
                    RectangleF r = rects[i].Rect;
                    if (r.X < -0.6f || r.Y < -0.6f || r.Right > 400.6f || r.Bottom > 300.6f) inside = false;
                    if (i > 0) area += (double)r.Width * r.Height;
                }
                bool areaOk = area >= 400 * 300 * 0.8 && area <= 400 * 300 * 1.02;
                bool overlap = false;
                for (int i = 1; i < rects.Count && !overlap; i++)
                {
                    for (int j = i + 1; j < rects.Count; j++)
                    {
                        RectangleF inter = RectangleF.Intersect(rects[i].Rect, rects[j].Rect);
                        if (inter.Width > 0.6f && inter.Height > 0.6f)
                        {
                            overlap = true;
                            break;
                        }
                    }
                }
                bool layoutOk = inside && !overlap && areaOk;
                bool ok = sizeOk && childOk && riskOk && layoutOk;

                FileOps.JunkReport report = FileOps.Analyze(mapRoot);
                bool analyzeOk = report.Cleanable.Count == 4 && report.Reclaim == total &&
                    report.Groups.Count == 1 && report.Groups[0].CatId == "user_temp";
                List<string> cats = new List<string>();
                cats.Add("user_temp");
                FileOps.JunkOutcome outcome = FileOps.DeleteJunk(engine, report, cats, null, null);
                bool cleanOk = outcome.Deleted == 3 && outcome.Skipped == 1 && outcome.Bytes == expected &&
                    File.Exists(Path.Combine(root, "report.docx"));
                ok = ok && analyzeOk && cleanOk;

                json.Append("  \"rootBytes\": ").Append(mapRoot == null ? 0 : mapRoot.Size).Append(",\n");
                json.Append("  \"rootFiles\": ").Append(mapRoot == null ? 0 : mapRoot.FileCount).Append(",\n");
                json.Append("  \"sizeOk\": ").Append(sizeOk ? "true" : "false").Append(",\n");
                json.Append("  \"childOk\": ").Append(childOk ? "true" : "false").Append(",\n");
                json.Append("  \"riskOk\": ").Append(riskOk ? "true" : "false").Append(",\n");
                json.Append("  \"risk\": [").Append(r1 ? "true" : "false").Append(",")
                    .Append(r2 ? "true" : "false").Append(",")
                    .Append(r3 ? "true" : "false").Append(",")
                    .Append(r4 ? "true" : "false").Append(",")
                    .Append(r5 ? "true" : "false").Append(",")
                    .Append(r6 ? "true" : "false").Append("],\n");
                json.Append("  \"blocks\": ").Append(rects.Count).Append(",\n");
                json.Append("  \"inside\": ").Append(inside ? "true" : "false").Append(",\n");
                json.Append("  \"overlap\": ").Append(overlap ? "true" : "false").Append(",\n");
                json.Append("  \"areaOk\": ").Append(areaOk ? "true" : "false").Append(",\n");
                json.Append("  \"analyzeOk\": ").Append(analyzeOk ? "true" : "false").Append(",\n");
                json.Append("  \"cleanOk\": ").Append(cleanOk ? "true" : "false").Append(",\n");
                json.Append("  \"cleanDeleted\": ").Append(outcome.Deleted).Append(",\n");
                json.Append("  \"cleanSkipped\": ").Append(outcome.Skipped).Append(",\n");
                json.Append("  \"ok\": ").Append(ok ? "true" : "false").Append("\n}\n");
                if (!ok) code = 1;
            }
            catch (Exception ex)
            {
                code = 1;
                json.Length = 0;
                json.Append("{\"mode\":\"filetest\",\"error\":\"").Append(Escape(ex.ToString())).Append("\"}");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                }
                catch
                {
                }
            }
            WriteJson(outPath, json.ToString());
            return code;
        }

        private static FileRisk RiskOfPath(RuleEngine engine, string path)
        {
            Category cat = engine.ClassifyFile(path);
            return MapRisk.Of(engine, path, false, cat);
        }

        private static void WriteJson(string path, string json)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, json, new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            StringBuilder b = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' || c == '"') b.Append('\\').Append(c);
                else if (c == '\r') b.Append("\\r");
                else if (c == '\n') b.Append("\\n");
                else if (c == '\t') b.Append("\\t");
                else if (c < 32) b.Append("\\u").Append(((int)c).ToString("x4"));
                else b.Append(c);
            }
            return b.ToString();
        }
    }
}
