using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace RegAttackSim
{
    /// <summary>
    /// 独立的注册表攻击模拟器：只对当前用户写一个可自动撤销的 Run 启动项，
    /// 指向临时目录里的隐藏测试载荷。用于验证 CleanC 主动防御的启动项检测与进程终止，
    /// 不修改系统级注册表，也不具备持久化能力。
    /// </summary>
    internal static class Program
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "CleanC_AttackSim";
        private const string ToolName = "CleanC_RegAttackSim";

        private static readonly string RootDir = Path.Combine(Path.GetTempPath(), ToolName);
        private static readonly string PayloadPath = Path.Combine(RootDir, "payload.exe");
        private static readonly string StartedMarker = Path.Combine(RootDir, "payload_started.txt");
        private static readonly string ErrorMarker = Path.Combine(RootDir, "payload_error.txt");
        private static readonly string WatchdogPath = Path.Combine(RootDir, "restore.bat");
        private static readonly string ResultPath = Path.Combine(RootDir, "last_result.txt");

        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }

            if (Has(args, "--payload")) return RunPayload();
            if (Has(args, "--cleanup")) return CleanupOnly();
            return RunController(args);
        }

        private static int RunController(string[] args)
        {
            Banner();
            bool yes = Has(args, "--yes");
            int wait = ParseWait(args);

            bool running = IsCleanCRunning();
            bool defenseOn;
            int defenseLevel;
            ReadDefenseSettings(out defenseOn, out defenseLevel);
            Console.WriteLine("CleanC 进程：" + (running ? "运行中" : "未运行"));
            Console.WriteLine("主动防御：" + (defenseOn
                ? ("已开启（" + (defenseLevel == 0 ? "低档" : "高档") + "）")
                : "未开启"));
            Console.WriteLine("检查方式：" + (defenseOn && running
                ? "等待自动检查；也可以立刻在 CleanC 里点「立即检查」"
                : "需要在 CleanC 里开启主动防御并点「立即检查」"));
            Console.WriteLine();

            string existing = ReadRunValue();
            if (existing != null && !existing.Contains(ToolName))
            {
                Console.WriteLine("已存在同名启动项，且不是本工具写入的，为避免覆盖真实软件，已中止：");
                Console.WriteLine("  " + RunValueName + " = " + existing);
                Pause(yes);
                return 1;
            }
            if (existing != null)
            {
                Console.WriteLine("发现上次运行残留，先清理旧启动项…");
                DeleteRunValue();
            }

            if (!yes)
            {
                Console.WriteLine("将写入：HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\\" + RunValueName);
                Console.WriteLine("将指向：" + PayloadPath + "（隐藏的临时测试程序）");
                Console.WriteLine("攻击进程会被主动防御终止，随后自动删除启动项；异常退出时由看门狗恢复。");
                Console.Write("输入 ATTACK 开始模拟（其他任意输入取消）：");
                string answer = Console.ReadLine();
                if (answer == null || answer.Trim().ToUpperInvariant() != "ATTACK")
                {
                    Console.WriteLine("已取消，未做任何修改。");
                    return 0;
                }
            }

            Console.WriteLine();
            Console.WriteLine("[1/5] 准备测试目录与隐藏载荷…");
            Prepare(wait);

            Console.WriteLine("[2/5] 启动恢复看门狗（" + (wait + 30) + " 秒后强制删除启动项）…");
            Process watchdog = StartWatchdog();

            Console.WriteLine("[3/5] 启动攻击载荷（PID 即将写入启动项）…");
            Process payload = StartPayload();

            Console.WriteLine("[4/5] 等待 CleanC 检测并终止攻击进程（最多 " + wait + " 秒）…");
            Console.WriteLine("      在 CleanC 中点击「立即检查」可以马上触发检测。按 Enter 提前结束等待。");
            bool exited;
            bool started = false;
            string error = "";
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < wait)
            {
                if (payload.HasExited)
                {
                    exited = true;
                    break;
                }
                if (ConsumeEnter())
                {
                    Console.WriteLine();
                    Console.WriteLine("已提前结束等待。");
                    break;
                }
                if (File.Exists(StartedMarker)) started = true;
                if (File.Exists(ErrorMarker)) error = ReadText(ErrorMarker);
                int left = wait - (int)sw.Elapsed.TotalSeconds;
                Console.Write("\r      剩余 " + left + " 秒   ");
                Thread.Sleep(1000);
            }
            exited = payload.HasExited;
            started = started || File.Exists(StartedMarker);
            error = File.Exists(ErrorMarker) ? ReadText(ErrorMarker) : "";
            Console.WriteLine();

            string current = ReadRunValue();
            bool valueExists = current != null;
            string verdict;
            if (!started)
            {
                verdict = "攻击载荷未能写入启动项" + (error.Length > 0 ? "：" + error : "。");
            }
            else if (exited)
            {
                verdict = valueExists
                    ? "检测到攻击进程已被终止（很可能是 CleanC 主动防御），启动项仍在，准备恢复。"
                    : "CleanC 已删除启动项并终止攻击进程。";
            }
            else
            {
                verdict = "在 " + wait + " 秒内没有检测到终止，可能是主动防御未开启或还没到检查时间。"
                    + "请启动 CleanC、开启高档主动防御后再点「立即检查」。";
            }

            Console.WriteLine("[5/5] 恢复现场…");
            bool restored = DeleteRunValue();
            try { if (!payload.HasExited) payload.Kill(); } catch { }
            try { if (watchdog != null && !watchdog.HasExited) watchdog.Kill(); } catch { }
            CleanupFiles();
            WriteResult(verdict + "\r\n启动项已恢复：" + (restored ? "是" : "否"));
            Console.WriteLine();
            Console.WriteLine(restored ? "✔ 启动项已删除，现场已恢复。" : "✘ 启动项删除失败，请手动检查 HKCU Run。");
            Console.WriteLine("结果：" + verdict);
            Console.WriteLine("日志：" + ResultPath);
            Pause(yes);
            return restored && started && exited ? 0 : (started ? 2 : 3);
        }

        private static int RunPayload()
        {
            try
            {
                Directory.CreateDirectory(RootDir);
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey, true))
                {
                    if (key == null) throw new Exception("无法打开 HKCU Run 键");
                    string command = "\"" + Process.GetCurrentProcess().MainModule.FileName + "\" --payload";
                    key.SetValue(RunValueName, command, RegistryValueKind.String);
                }
                File.WriteAllText(StartedMarker, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), new UTF8Encoding(false));
                Thread.Sleep(600000);
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    Directory.CreateDirectory(RootDir);
                    File.WriteAllText(ErrorMarker, ex.Message, new UTF8Encoding(false));
                }
                catch { }
                return 2;
            }
        }

        private static int CleanupOnly()
        {
            Console.WriteLine("清理残留…");
            bool ok = DeleteRunValue();
            CleanupFiles();
            Console.WriteLine(ok ? "已删除启动项。" : "启动项不存在或删除失败。");
            return 0;
        }

        // ---------------------------------------------------------------- setup

        private static void Prepare(int waitSeconds)
        {
            Directory.CreateDirectory(RootDir);
            try
            {
                if (File.Exists(PayloadPath)) File.Delete(PayloadPath);
                File.Copy(Process.GetCurrentProcess().MainModule.FileName, PayloadPath, true);
                File.SetAttributes(PayloadPath, FileAttributes.Hidden);
            }
            catch (Exception ex)
            {
                Console.WriteLine("复制载荷失败：" + ex.Message);
                throw;
            }
            try { if (File.Exists(StartedMarker)) File.Delete(StartedMarker); } catch { }
            try { if (File.Exists(ErrorMarker)) File.Delete(ErrorMarker); } catch { }
            WriteWatchdog(waitSeconds + 30);
        }

        private static Process StartWatchdog()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                psi.Arguments = "/c call \"" + WatchdogPath + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                return Process.Start(psi);
            }
            catch
            {
                return null;
            }
        }

        private static Process StartPayload()
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = PayloadPath;
            psi.Arguments = "--payload";
            psi.WorkingDirectory = RootDir;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            return Process.Start(psi);
        }

        /// <summary>生成看门狗脚本：等待若干秒后强制删除启动项。</summary>
        private static void WriteWatchdog(int waitSeconds)
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("@echo off");
            b.AppendLine("ping -n " + (waitSeconds + 1) + " 127.0.0.1 >nul");
            b.AppendLine("reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v " + RunValueName + " /f >nul 2>&1");
            b.AppendLine("echo %date% %time% watchdog restored >> \"" + Path.Combine(RootDir, "watchdog.log") + "\"");
            b.AppendLine("del \"%~f0\" >nul 2>&1");
            File.WriteAllText(WatchdogPath, b.ToString(), new UTF8Encoding(false));
        }

        // ---------------------------------------------------------------- registry

        private static string ReadRunValue()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return null;
                    object v = key.GetValue(RunValueName, null);
                    return v == null ? null : v.ToString();
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool DeleteRunValue()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return true;
                    object v = key.GetValue(RunValueName, null);
                    if (v == null) return true;
                    key.DeleteValue(RunValueName, false);
                }
                return ReadRunValue() == null;
            }
            catch
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- info

        private static void ReadDefenseSettings(out bool enabled, out int level)
        {
            enabled = false;
            level = 1;
            try
            {
                string path = Path.Combine(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CleanC"), "settings.ini");
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    if (string.Equals(k, "defense.enabled", StringComparison.OrdinalIgnoreCase)) enabled = v == "1";
                    else if (string.Equals(k, "defense.level", StringComparison.OrdinalIgnoreCase)) level = v == "0" ? 0 : 1;
                }
            }
            catch
            {
            }
        }

        private static bool IsCleanCRunning()
        {
            try
            {
                Process[] all = Process.GetProcesses();
                for (int i = 0; i < all.Length; i++)
                {
                    try
                    {
                        string n = all[i].ProcessName;
                        if (n.IndexOf("cleanc", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("清理", StringComparison.Ordinal) >= 0) return true;
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

        private static int ParseWait(string[] args)
        {
            string s = Arg(args, "--wait");
            int n;
            if (int.TryParse(s, out n) && n >= 10 && n <= 600) return n;
            return 90;
        }

        private static bool Has(string[] args, string flag)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string Arg(string[] args, string flag)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return "";
        }

        private static void Banner()
        {
            Console.WriteLine("==============================================================");
            Console.WriteLine("  CleanC 注册表攻击模拟器（独立安全测试工具）");
            Console.WriteLine("==============================================================");
            Console.WriteLine("这个程序只做一件事：写一个当前用户的 Run 启动项，指向临时目录里的");
            Console.WriteLine("隐藏测试载荷，用来触发 CleanC 的启动项检测与可疑进程终止。");
            Console.WriteLine("不会修改 Winlogon / Defender / 服务 / 系统级注册表，也不会持久化。");
            Console.WriteLine("攻击结束后删除启动项；使用 --cleanup 可单独清理残留。");
            Console.WriteLine();
        }

        private static bool ConsumeEnter()
        {
            try
            {
                if (!Console.KeyAvailable) return false;
                ConsoleKeyInfo k = Console.ReadKey(true);
                return k.Key == ConsoleKey.Enter;
            }
            catch
            {
                return false;
            }
        }

        private static void CleanupFiles()
        {
            try { if (File.Exists(PayloadPath)) File.Delete(PayloadPath); } catch { }
            try { if (File.Exists(StartedMarker)) File.Delete(StartedMarker); } catch { }
            try { if (File.Exists(ErrorMarker)) File.Delete(ErrorMarker); } catch { }
        }

        private static void WriteResult(string text)
        {
            try
            {
                Directory.CreateDirectory(RootDir);
                File.WriteAllText(ResultPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + text + "\r\n",
                    new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private static string ReadText(string path)
        {
            try { return File.ReadAllText(path, Encoding.UTF8).Trim(); } catch { return ""; }
        }

        private static void Pause(bool skip)
        {
            if (skip) return;
            Console.WriteLine("按 Enter 退出…");
            try { Console.ReadLine(); } catch { }
        }
    }
}
