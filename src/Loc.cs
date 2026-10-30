using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CleanC
{
    public static class Loc
    {
        public const string ZhCn = "zh-CN";
        public const string ZhTw = "zh-TW";
        public const string En = "en";
        public static readonly string[] Langs = { ZhCn, ZhTw, En };

        private static readonly Dictionary<string, string> Zh = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> Tw = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> Eng = new Dictionary<string, string>();
        private static string _lang = ZhCn;

        static Loc()
        {
            Build();
        }

        public static string Lang
        {
            get { return _lang; }
        }

        public static void Init()
        {
            string v = Settings.Get("lang", "");
            if (v == ZhCn || v == ZhTw || v == En)
            {
                _lang = v;
                return;
            }
            // 首次运行跟随系统语言：中文系统用简/繁中文，其他系统用英文。
            _lang = SystemLang();
        }

        public static string SystemLang()
        {
            try
            {
                string name = CultureInfo.CurrentUICulture.Name;
                if (name.Length > 0 && name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    if (name.IndexOf("TW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("HK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("MO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Hant", StringComparison.OrdinalIgnoreCase) >= 0)
                        return ZhTw;
                    return ZhCn;
                }
                return En;
            }
            catch
            {
                return ZhCn;
            }
        }

        public static void SetLang(string lang)
        {
            if (lang != ZhCn && lang != ZhTw && lang != En) return;
            _lang = lang;
            Settings.Set("lang", lang);
        }

        public static string T(string key)
        {
            Dictionary<string, string> d = CurrentDict();
            string v;
            if (d.TryGetValue(key, out v)) return v;
            if (Zh.TryGetValue(key, out v)) return v;
            if (Eng.TryGetValue(key, out v)) return v;
            return key;
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        public static string CatName(string id)
        {
            return T("cat." + id + ".name");
        }

        public static string CatDesc(string id)
        {
            return T("cat." + id + ".desc");
        }

        public static string RiskText(RiskLevel r)
        {
            if (r == RiskLevel.Safe) return T("risk.safe");
            if (r == RiskLevel.Caution) return T("risk.caution");
            if (r == RiskLevel.Protected) return T("risk.protected");
            return T("risk.other");
        }

        public static string LangName(string code)
        {
            if (code == ZhTw) return T("lang.zhtw");
            if (code == En) return T("lang.en");
            return T("lang.zhcn");
        }

        public static string LangShort()
        {
            if (_lang == ZhTw) return T("lang.shortZhtw");
            if (_lang == En) return T("lang.shortEn");
            return T("lang.shortZhcn");
        }

        public static string LevelName(ScanLevel level)
        {
            if (level == ScanLevel.Low) return T("level.low");
            if (level == ScanLevel.Medium) return T("level.medium");
            if (level == ScanLevel.High) return T("level.high");
            return T("level.extreme");
        }

        public static string LevelHint(ScanLevel level)
        {
            if (level == ScanLevel.Low) return T("level.low.hint");
            if (level == ScanLevel.Medium) return T("level.medium.hint");
            if (level == ScanLevel.High) return T("level.high.hint");
            return T("level.extreme.hint");
        }

        private static Dictionary<string, string> CurrentDict()
        {
            if (_lang == ZhTw) return Tw;
            if (_lang == En) return Eng;
            return Zh;
        }

        private static void Add(string key, string zh, string zhTw, string en)
        {
            Zh[key] = zh;
            Tw[key] = zhTw;
            Eng[key] = en;
        }

        private static void Build()
        {
            Add("app.name", "C 盘清理", "C 槽清理", "C Drive Cleaner");
            Add("app.tagline", "CleanC · 本地安全清理", "CleanC · 本機安全清理", "CleanC · Local, safe cleanup");

            Add("left.disk", "磁盘空间", "磁碟空間", "Disk space");
            Add("left.used", "已使用", "已使用", "Used");
            Add("left.total", "总容量", "總容量", "Total");
            Add("left.apps", "已安装程序", "已安裝程式", "Installed apps");
            Add("left.appSize", "可清理体积", "可清理大小", "Removable size");
            Add("left.appProtected", "受保护程序", "受保護程式", "Protected apps");
            Add("left.safe", "可安全清理", "可安全清理", "Safe to clean");
            Add("left.caution", "谨慎项目", "謹慎項目", "Review first");
            Add("left.protected", "系统保护", "系統保護", "Protected");
            Add("left.admin", "管理员模式", "管理員模式", "Administrator");
            Add("left.normal", "普通权限运行", "一般權限執行", "Standard user");
            Add("left.normalSub", "系统级项目需要管理员权限", "系統層級項目需要管理員權限", "Admin needed for system items");
            Add("left.elevate", "立即提权", "立即提權", "Elevate");
            Add("left.note1", "所有扫描与清理均在本机完成", "所有掃描與清理均在本機完成", "Everything runs on this PC");
            Add("left.note2", "规则引擎 v2.5 · 不上传任何数据", "規則引擎 v2.5 · 不上傳任何資料", "Rule engine v2.5 · nothing uploaded");
            Add("left.note3", "本地规则引擎 · 全程操作日志", "本機規則引擎 · 全程操作記錄", "Local rules · full audit log");

            Add("right.title", "清理项目", "清理項目", "Cleanup items");
            Add("right.sub", "自动识别可清理项，删除前二次校验", "自動辨識可清理項目，刪除前再次驗證", "Finds cleanable items and re-checks every delete");
            Add("right.selectAll", "全选安全项", "全選安全項目", "Select safe items");

            Add("empty.title", "准备好清理 C 盘了吗", "準備好清理 C 槽了嗎", "Ready to clean your C drive?");
            Add("empty.sub", "扫描 C 盘全部文件，按安全规则判断哪些可以删除", "掃描 C 槽全部檔案，依安全規則判斷哪些可以刪除", "Scan every file on C: and apply the safety rules");
            Add("empty.chip1", "System32 · 禁止删除", "System32 · 禁止刪除", "System32 · never delete");
            Add("empty.chip2", "Temp · 可安全清理", "Temp · 可安全清理", "Temp · safe to clean");
            Add("empty.chip3", "用户资料 · 永不触碰", "使用者資料 · 永不觸碰", "Personal files · untouched");
            Add("empty.hint", "点击左侧「开始扫描」查看每个项目的判断结果", "點擊左側「開始掃描」查看每個項目的判斷結果", "Click “Start scan” to see every verdict");

            Add("btn.scan", "开始扫描", "開始掃描", "Start scan");
            Add("btn.scanning", "正在扫描", "正在掃描", "Scanning");
            Add("btn.rescan", "重新扫描", "重新掃描", "Scan again");
            Add("btn.clean", "清理选中项", "清理選取項目", "Clean selected");
            Add("btn.cancelScan", "取消扫描", "取消掃描", "Cancel scan");
            Add("btn.cancelClean", "取消清理", "取消清理", "Cancel cleaning");
            Add("btn.cancel", "取消", "取消", "Cancel");
            Add("btn.cancelWait", "正在取消…", "正在取消…", "Canceling…");
            Add("btn.done", "完成", "完成", "Done");
            Add("btn.close", "关闭", "關閉", "Close");
            Add("btn.openFolder", "在资源管理器中打开", "在檔案總管中開啟", "Open in Explorer");
            Add("btn.startClean", "开始清理", "開始清理", "Clean now");

            Add("scan.preparing", "正在读取 C 盘文件列表…", "正在讀取 C 槽檔案清單…", "Reading the C: file list…");
            Add("scan.title", "正在扫描 C 盘", "正在掃描 C 槽", "Scanning C:");
            Add("scan.sub", "逐个文件判断可清理性，不会删除任何内容", "逐一判斷可清理性，不會刪除任何內容", "Checking each file; nothing is deleted");
            Add("scan.files", "已扫描文件", "已掃描檔案", "Files scanned");
            Add("scan.bytes", "已读取体积", "已讀取大小", "Data read");
            Add("scan.elapsed", "已用时间", "已用時間", "Elapsed");
            Add("scan.reading", "正在读取目录…", "正在讀取目錄…", "Reading folders…");
            Add("scan.note", "可随时取消，已扫描的结果会保留", "可隨時取消，已掃描結果會保留", "Cancel anytime; partial results are kept");

            Add("clean.title", "正在清理", "正在清理", "Cleaning");
            Add("clean.sub", "每个文件都会经过规则引擎二次校验", "每個檔案都會經過規則引擎再次驗證", "Every file is verified again");
            Add("clean.freed", "永久释放", "永久釋放", "Permanently freed");
            Add("clean.recycled", "移入回收站", "移至資源回收筒", "To Recycle Bin");
            Add("clean.processed", "已处理文件", "已處理檔案", "Files processed");
            Add("clean.preparing", "准备清理…", "準備清理…", "Preparing…");
            Add("clean.note", "被占用的文件会自动跳过，并记入清理日志", "被占用的檔案會自動跳過，並記入清理記錄", "Locked files are skipped and logged");

            Add("footer.selected", "已选 {0} 项", "已選 {0} 項", "{0} selected");
            Add("footer.none", "未选择清理项目", "未選擇清理項目", "No items selected");
            Add("footer.notScanned", "尚未扫描", "尚未掃描", "Not scanned yet");
            Add("footer.estimate", "· 预计释放 {0}", "· 預計釋放 {0}", "· about {0} to free");
            Add("footer.note", "删除前会逐文件判定类型与风险；双击类目可查看明细，系统保护与个人文件永不触碰",
                "刪除前會逐檔案判定類型與風險；雙擊類目可查看明細，系統保護與個人檔案永不觸碰",
                "Every file is judged by type and risk; double-click a category for details; protected and personal files are never touched");
            Add("footer.noteUninstall", "普通删除只清理安装目录；强力删除会额外处理命令与注册表，均需勾选免责声明", "一般刪除只清理安裝目錄；強力刪除會額外處理命令與登錄檔，均需勾選免責聲明",
                "Normal removal only clears the install folder. Force also runs commands and edits the registry, behind a consent check");

            Add("sum.scan", "扫描 {0} 个文件", "掃描 {0} 個檔案", "Scanned {0} files");
            Add("sum.time", " · 用时 {0}", " · 用時 {0}", " · {0}");
            Add("sum.canceled", " · 已取消，仅显示部分结果", " · 已取消，僅顯示部分結果", " · canceled, partial results");
            Add("sum.denied", " · 受限目录 {0}", " · 受限目錄 {0}", " · {0} restricted folders");
            Add("sum.scanError", "扫描失败：{0}", "掃描失敗：{0}", "Scan failed: {0}");
            Add("sum.lastClean", "上次清理 {0} · {1} 个文件", "上次清理 {0} · {1} 個檔案", "Last cleanup {0} · {1} files");
            Add("sum.cleanError", "清理失败：{0}", "清理失敗：{0}", "Cleanup failed: {0}");

            Add("dlg.confirm", "确认清理", "確認清理", "Confirm cleanup");
            Add("confirm.startJunk", "清理并备份", "清理並備份", "Clean with backup");
            Add("confirm.noteJunk", "包含注册表垃圾：删除前会逐项导出 .reg 备份到本地，需要管理员权限，删除后可用备份文件还原。",
                "包含登錄檔垃圾：刪除前會逐項匯出 .reg 備份到本機，需要管理員權限，刪除後可用備份檔案還原。",
                "Includes registry junk. Each key is exported to a .reg backup first and can be restored. Administrator rights required.");
            Add("result.subRegistry", " · 注册表 {0} 项", " · 登錄檔 {0} 項", " · {0} registry items");
            Add("dlg.detail", "清理详情", "清理詳情", "Cleanup details");
            Add("dlg.result", "清理结果", "清理結果", "Cleanup results");
            Add("confirm.summary", "本次将清理 {0} 个项目，共 {1}", "本次將清理 {0} 個項目，共 {1}", "Cleaning {0} items · {1}");
            Add("confirm.permanent", "永久删除", "永久刪除", "Delete permanently");
            Add("confirm.recycle", "移入回收站", "移至資源回收筒", "Move to Recycle Bin");
            Add("confirm.emptyRecycle", "清空回收站", "清空資源回收筒", "Empty Recycle Bin");
            Add("confirm.more", "另有 {0} 个项目", "另有 {0} 個項目", "+{0} more");
            Add("confirm.note", "安全项目将被永久删除（均为缓存与临时文件）；谨慎项目会移入回收站，可在回收站中还原。",
                "安全項目將被永久刪除（均為快取與暫存檔案）；謹慎項目會移至資源回收筒，可從資源回收筒還原。",
                "Safe items are deleted permanently (cache and temp only). Review items go to the Recycle Bin and can be restored.");
            Add("confirm.noteOld", "Windows.old 将被永久删除且无法恢复；其余谨慎项目会移入回收站，可在回收站中还原。",
                "Windows.old 將被永久刪除且無法復原；其餘謹慎項目會移至資源回收筒，可從資源回收筒還原。",
                "Windows.old is deleted permanently with no rollback. Other review items go to the Recycle Bin and can be restored.");
            Add("confirm.totals", "永久删除 {0} · 回收站 {1}", "永久刪除 {0} · 資源回收筒 {1}", "Permanent {0} · Recycle Bin {1}");

            Add("detail.files", "共 {0} 个文件 · {1}", "共 {0} 個檔案 · {1}", "{0} files · {1}");
            Add("detail.hint", "双击任意文件可在资源管理器中定位", "雙擊任一檔案可在檔案總管中定位", "Double-click a file to reveal it");
            Add("detail.onlyTop", " · 仅展示最大的 {0} 项", " · 僅顯示最大的 {0} 項", " · showing the largest {0}");
            Add("detail.fileSummary", "安全 {0} · 需确认 {1} · 个人文件 {2}（个人文件不会被删除）",
                "安全 {0} · 需確認 {1} · 個人檔案 {2}（個人檔案不會被刪除）",
                "Safe {0} · Review {1} · Personal {2} (personal files are never deleted)");
            Add("file.kind.cache", "缓存数据", "快取資料", "Cache data");
            Add("file.kind.temp", "临时文件", "暫存檔案", "Temp file");
            Add("file.kind.log", "日志/诊断", "記錄/診斷", "Log/diagnostics");
            Add("file.kind.dump", "崩溃转储", "當機傾印", "Crash dump");
            Add("file.kind.thumb", "缩略图/图标库", "縮圖/圖示資料庫", "Thumbnail/icon DB");
            Add("file.kind.font", "字体缓存", "字型快取", "Font cache");
            Add("file.kind.prefetch", "预读取文件", "預讀取檔案", "Prefetch data");
            Add("file.kind.update", "更新包", "更新套件", "Update package");
            Add("file.kind.installer", "安装包缓存", "安裝包快取", "Installer cache");
            Add("file.kind.recycle", "回收站文件", "資源回收筒檔案", "Recycle Bin file");
            Add("file.kind.backup", "备份/旧系统", "備份/舊系統", "Backup/old system");
            Add("file.kind.binary", "可执行文件", "執行檔", "Executable");
            Add("file.kind.document", "个人文档/媒体", "個人文件/媒體", "Personal document/media");
            Add("file.kind.config", "配置/数据库", "設定/資料庫", "Config/database");
            Add("file.kind.other", "普通文件", "一般檔案", "Other file");
            Add("detail.empty", "暂无可展示的文件明细", "暫無可顯示的檔案明細", "No file details available");
            Add("detail.emptySub", "该类别暂未取得文件列表，可直接在主界面勾选清理", "該類別暫未取得檔案清單，可直接在主介面勾選清理", "No file list for this category; clean it from the main screen");
            Add("detail.emptyProtected", "此项目仅统计体积，不提供删除入口", "此項目僅統計大小，不提供刪除入口", "Size only; deletion is not available");
            Add("detail.emptyProtectedSub", "被规则引擎标记为系统保护，任何情况下都不会删除", "被規則引擎標記為系統保護，任何情況下都不會刪除", "Marked protected by the rule engine; never deleted");
            Add("detail.emptyOtherSub", "未归类文件，不参与自动清理", "未歸類檔案，不參與自動清理", "Uncategorized files; not cleaned automatically");
            Add("detail.regHint", "双击任意项可在注册表编辑器中查看路径", "雙擊任一項可在登錄檔編輯器中查看路徑", "Double-click an item to see its full key path");

            Add("explain.safe", "这些文件属于缓存或临时数据，删除后系统或应用会自动重建，不会影响个人文件与账号登录。",
                "這些檔案屬於快取或暫存資料，刪除後系統或應用程式會自動重建，不會影響個人檔案與帳號登入。",
                "These are cache or temporary files. Windows or the app rebuilds them automatically; personal files and sign-ins are unaffected.");
            Add("explain.caution", "清理前请确认；除 Windows.old 外，本项目会移入回收站，可在回收站中还原。",
                "清理前請確認；除 Windows.old 外，本項目會移至資源回收筒，可從資源回收筒還原。",
                "Please review first. Except for Windows.old, items go to the Recycle Bin and can be restored.");
            Add("explain.protected", "路径被规则引擎列入硬性保护清单，本程序不会提供删改入口，也不会在清理流程中触碰任何文件。",
                "路徑被規則引擎列入強制保護清單，本程式不會提供刪改入口，也不會在清理流程中觸碰任何檔案。",
                "This path is on the hard-protection list. The app never offers deletion and never touches these files.");
            Add("explain.other", "该分类仅用于展示 C 盘的完整占用情况，不参与自动清理。",
                "此分類僅用於顯示 C 槽的完整使用情況，不參與自動清理。",
                "This category only accounts for disk usage and is never cleaned automatically.");
            Add("explain.junk", "这些注册表项指向的文件或程序已经不存在，删除前会逐项导出 .reg 备份，可随时还原。",
                "這些登錄檔項目指向的檔案或程式已不存在，刪除前會逐項匯出 .reg 備份，可隨時還原。",
                "These keys point to files that no longer exist. Each is exported to a .reg backup before deletion.");
            Add("explain.sep", "。", "。", " ");

            Add("result.title", "清理完成", "清理完成", "Cleanup complete");
            Add("result.canceled", "清理已取消", "清理已取消", "Cleanup canceled");
            Add("result.subFreed", "永久释放", "永久釋放", "Permanently freed");
            Add("result.subRecycled", " · 另有 {0} 已移入回收站", " · 另有 {0} 已移至資源回收筒", " · {0} moved to Recycle Bin");
            Add("result.subRecycleOnly", "已移入回收站，可在回收站中还原", "已移至資源回收筒，可從資源回收筒還原", "Moved to the Recycle Bin; restore from there");
            Add("result.subNone", "没有需要清理的内容", "沒有需要清理的內容", "Nothing needed cleaning");
            Add("result.statFreed", "永久释放", "永久釋放", "Freed");
            Add("result.statRecycled", "移入回收站", "移至資源回收筒", "Recycled");
            Add("result.statDeleted", "已删除文件", "已刪除檔案", "Files deleted");
            Add("result.statSkipped", "跳过失败", "跳過失敗", "Skipped");
            Add("result.viewLog", "查看清理日志", "查看清理記錄", "View cleanup log");
            Add("unit.items", "个", "個", "files");

            Add("risk.safe", "安全可清理", "安全可清理", "Safe");
            Add("risk.caution", "建议确认", "建議確認", "Review");
            Add("risk.protected", "系统保护", "系統保護", "Protected");
            Add("risk.other", "其他", "其他", "Other");

            Add("lang.zhcn", "简体中文", "简体中文", "Simplified Chinese");
            Add("lang.zhtw", "繁體中文", "繁體中文", "Traditional Chinese");
            Add("lang.en", "English", "English", "English");
            Add("lang.shortZhcn", "简体", "简体", "简");
            Add("lang.shortZhtw", "繁體", "繁體", "繁");
            Add("lang.shortEn", "EN", "EN", "EN");

            Add("crash.title", "界面异常", "介面異常", "Interface error");
            Add("crash.body", "界面出现异常，详细信息已写入日志：", "介面發生異常，詳細資訊已寫入記錄：", "An error occurred. Details were written to:");

            Add("cat.user_temp.name", "用户临时文件", "使用者暫存檔案", "User temp files");
            Add("cat.user_temp.desc", "应用与系统运行产生的临时文件，可放心删除", "應用程式與系統產生的暫存檔案，可放心刪除", "Temp data from apps and Windows; safe to remove");
            Add("cat.system_temp.name", "系统临时文件", "系統暫存檔案", "System temp files");
            Add("cat.system_temp.desc", "Windows 临时目录中的系统级残留，需要管理员权限", "Windows 暫存目錄中的系統殘留，需要管理員權限", "Windows temp folders; needs administrator rights");
            Add("cat.wu_cache.name", "Windows 更新缓存", "Windows 更新快取", "Windows Update cache");
            Add("cat.wu_cache.desc", "已下载的更新安装包，清理后可自动重新获取", "已下載的更新安裝封裝，清除後可自動重新取得", "Downloaded update packages; fetched again when needed");
            Add("cat.browser_cache.name", "浏览器缓存", "瀏覽器快取", "Browser cache");
            Add("cat.browser_cache.desc", "网页图片与脚本缓存，清理不会退出账号", "網頁圖片與指令碼快取，清除不會登出帳號", "Cached page assets; clearing does not sign you out");
            Add("cat.app_cache.name", "应用缓存（Local）", "應用程式快取（Local）", "App caches (Local)");
            Add("cat.app_cache.desc", "仅 AppData\\Local 下明确命名的 Cache 目录；Roaming 一律不碰", "僅 AppData\\Local 下明確命名的 Cache 目錄；Roaming 一律不碰",
                "Only explicitly named Cache folders under AppData\\Local; Roaming is never touched");
            Add("cat.thumb_cache.name", "缩略图缓存", "縮圖快取", "Thumbnail cache");
            Add("cat.thumb_cache.desc", "资源管理器缩略图与图标数据库，会自动重建", "檔案總管縮圖與圖示資料庫，會自動重建", "Explorer thumbnail and icon database; rebuilt automatically");
            Add("cat.crash_dump.name", "崩溃转储", "當機傾印", "Crash dumps");
            Add("cat.crash_dump.desc", "错误报告与内存转储文件，仅用于故障排查", "錯誤報告與記憶體傾印檔案，僅供疑難排解", "Error reports and memory dumps used for troubleshooting");
            Add("cat.font_cache.name", "字体缓存", "字型快取", "Font cache");
            Add("cat.font_cache.desc", "字体渲染缓存，删除后系统会自动重建", "字型轉譯快取，刪除後系統會自動重建", "Font rendering cache; rebuilt automatically");
            Add("cat.delivery_opt.name", "交付优化缓存", "傳遞最佳化快取", "Delivery Optimization cache");
            Add("cat.delivery_opt.desc", "Windows 更新点对点分发缓存，需要管理员权限", "Windows 更新點對點傳遞快取，需要管理員權限", "Windows update peer cache; needs administrator rights");
            Add("cat.recycle_bin.name", "回收站", "資源回收筒", "Recycle Bin");
            Add("cat.recycle_bin.desc", "已删除文件的暂存区，清空后不可恢复", "已刪除檔案的暫存區，清空後無法復原", "Deleted files are held here; emptying is irreversible");
            Add("cat.windows_old.name", "旧系统备份", "舊系統備份", "Previous Windows installation");
            Add("cat.windows_old.desc", "系统升级前的备份，删除后无法回滚（不可恢复）", "系統升級前的備份，刪除後無法復原（不可回復）", "Backup from an upgrade; deleting it removes rollback");
            Add("cat.prefetch.name", "预读取文件", "預讀取檔案", "Prefetch data");
            Add("cat.prefetch.desc", "启动预读数据，删除后首次启动会稍慢", "開機預讀資料，刪除後首次啟動會稍慢", "Startup prefetch data; the first launch may be slower");
            Add("cat.sys_logs.name", "系统安装与日志", "系統安裝與記錄", "Setup and system logs");
            Add("cat.sys_logs.desc", "系统安装与诊断日志，删除会丢失排障信息", "系統安裝與診斷記錄，刪除會遺失除錯資訊", "Setup and diagnostic logs; troubleshooting data is lost");
            Add("cat.package_cache.name", "安装包缓存", "安裝程式快取", "Installer cache");
            Add("cat.package_cache.desc", "部分程序的修复与卸载所需缓存", "部分程式的修復與解除安裝所需快取", "Cache used by some apps for repair and uninstall");
            Add("cat.system_core.name", "系统核心", "系統核心", "System core");
            Add("cat.system_core.desc", "System32、WinSxS 等系统组件，删除会导致系统损坏", "System32、WinSxS 等系統元件，刪除會導致系統損毀", "System32, WinSxS and other core components; deleting breaks Windows");
            Add("cat.programs.name", "程序文件", "程式檔案", "Program files");
            Add("cat.programs.desc", "已安装程序与程序数据，请通过卸载程序移除", "已安裝程式與程式資料，請透過解除安裝移除", "Installed apps and data; remove them via uninstall");
            Add("cat.installer.name", "卸载信息", "解除安裝資訊", "Uninstall information");
            Add("cat.installer.desc", "Windows Installer 卸载数据库，删除后程序无法卸载", "Windows Installer 解除安裝資料庫，刪除後程式無法解除安裝", "Windows Installer database; apps may become uninstallable");
            Add("cat.user_files.name", "用户资料", "使用者資料", "Personal files");
            Add("cat.user_files.desc", "文档、桌面、下载等个人文件，绝不删除", "文件、桌面、下載等個人檔案，絕不刪除", "Documents, desktop, downloads and other personal data; never deleted");
            Add("cat.pagefile.name", "虚拟内存与休眠", "虛擬記憶體與休眠", "Paging and hibernation");
            Add("cat.pagefile.desc", "页面文件与休眠文件，由系统管理，禁止删除", "分頁檔與休眠檔，由系統管理，禁止刪除", "pagefile.sys and hiberfil.sys are managed by Windows; never delete");
            Add("cat.other.name", "其他文件", "其他檔案", "Other files");
            Add("cat.other.desc", "未归类的文件，仅统计体积，不参与清理", "未歸類的檔案，僅統計大小，不參與清理", "Uncategorized files; counted only, never cleaned");

            Add("tab.clean", "C 盘清理", "C 槽清理", "Disk cleanup");
            Add("tab.uninstall", "卸载软件", "解除安裝", "Uninstall");
            Add("tab.registry", "残留清理", "殘留清理", "Leftovers");
            Add("regtab.title", "残留清理", "殘留清理", "Leftover cleanup");
            Add("regtab.sub", "16 步全量扫描：目录 / 计划任务 / 启动项 / 防火墙 / 服务 / 注册表 / 驱动 / 浏览器 / 日志", "16 步全量掃描：目錄 / 排程工作 / 啟動項 / 防火牆 / 服務 / 登錄檔 / 驅動 / 瀏覽器 / 記錄",
                "16-step sweep: folders, tasks, startups, firewall, services, registry, drivers, browsers, logs");
            Add("regtab.subCount", "共 {0} 项 · 已选 {1} 项", "共 {0} 項 · 已選 {1} 項", "{0} items · {1} selected");
            Add("regtab.subReboot", " · {0} 项需重启", " · {0} 項需重啟", " · {0} need a reboot");
            Add("regtab.none", "没有发现残留项", "沒有發現殘留項", "No leftovers found");
            Add("regtab.scanning", "正在执行 16 步残留扫描…", "正在執行 16 步殘留掃描…", "Running the 16-step leftover sweep…");
            Add("regtab.scan", "扫描残留", "掃描殘留", "Scan leftovers");
            Add("regtab.rescan", "重新扫描", "重新掃描", "Rescan");
            Add("regtab.btnClean", "清理选中项", "清理選取項", "Clean selected");
            Add("regtab.running", "正在执行命令", "正在執行命令", "Running commands");
            Add("regtab.runningSub", "正在通过命令提示符 / COM 逐条删除残留项…", "正在透過命令提示字元 / COM 逐條刪除殘留項…",
                "Deleting leftovers one by one via command prompt / COM…");
            Add("regtab.cmdHint", "命令提示符窗口已弹出，删除过程可在其中看到", "命令提示字元視窗已彈出，刪除過程可在其中看到",
                "A command prompt window is open showing each command");
            Add("regtab.done", "残留清理完成", "殘留清理完成", "Leftover cleanup finished");
            Add("regtab.doneSub", "已删除 {0} 项 · 失败 {1} 项", "已刪除 {0} 項 · 失敗 {1} 項", "{0} removed · {1} failed");
            Add("regtab.hint", "驱动包会打开管理员 cmd 强制删除（pnputil + takeown/icacls），服务先 sc stop 再 sc delete",
                "驅動套件會開啟管理員 cmd 強制刪除（pnputil + takeown/icacls），服務先 sc stop 再 sc delete",
                "Driver packages open an admin cmd and force-delete (pnputil + takeown/icacls); services are stopped then deleted");
            Add("regtab.selectAll", "全选", "全選", "Select all");
            Add("regtab.checks1", "目录：Program Files / ProgramData / AppData / LocalLow 中已无程序的残留目录",
                "目錄：Program Files / ProgramData / AppData / LocalLow 中已無程式的殘留目錄",
                "Folders: leftovers under Program Files / ProgramData / AppData / LocalLow");
            Add("regtab.checks2", "系统：计划任务、启动项、防火墙规则、服务与驱动、设备类 GUID",
                "系統：排程工作、啟動項、防火牆規則、服務與驅動、裝置類 GUID",
                "System: tasks, startups, firewall rules, services/drivers, device class GUIDs");
            Add("regtab.checks3", "注册表：全量扫描 SOFTWARE / Classes / Installer / 事件日志源，含 WOW6432Node 与 HKCU",
                "登錄檔：全量掃描 SOFTWARE / Classes / Installer / 事件記錄來源，含 WOW6432Node 與 HKCU",
                "Registry: full SOFTWARE / Classes / Installer / event-log sweep, incl. WOW6432Node and HKCU");
            Add("regtab.checks4", "每项都有备份与回退；目录改名观察；清理后自动验证并支持系统还原回退",
                "每項都有備份與回退；目錄改名觀察；清理後自動驗證並支援系統還原回退",
                "Each item is backed up and reversible; folders are renamed; verification runs afterwards");
            Add("regtab.selected", "已选 {0} 项残留项目", "已選 {0} 項殘留項目", "{0} leftovers selected");
            Add("regtab.noSelection", "勾选要删除的残留项目", "勾選要刪除的殘留項目", "Tick the leftovers to delete");
            Add("regtab.emptyTitle", "没有发现残留项", "沒有發現殘留項", "No leftovers found");
            Add("regtab.emptySub", "已完成 16 步扫描：安装目录、ProgramData、AppData、计划任务、启动项、防火墙、服务、全量注册表、Classes、Installer、驱动、浏览器扩展、快捷方式、事件日志与临时缓存",
                "已完成 16 步掃描：安裝目錄、ProgramData、AppData、排程工作、啟動項、防火牆、服務、全量登錄檔、Classes、Installer、驅動、瀏覽器擴充、捷徑、事件記錄與暫存快取",
                "The 16-step sweep checked install folders, ProgramData, AppData, tasks, startups, firewall, services, the full registry, Classes, Installer, drivers, browser extensions, shortcuts, event logs and temp caches");
            Add("left.appsTitle", "已安装软件", "已安裝軟體", "Installed apps");
            Add("left.regTotal", "发现项", "發現項", "Found");
            Add("left.regCaution", "需确认", "需確認", "Confirm");
            Add("left.regReboot", "需重启", "需重啟", "Reboot");
            Add("footer.noteRegistry", "16 步全量扫描；清理时弹出命令提示符逐条执行，目录只改名不删除，清理后自动验证",
                "16 步全量掃描；清理時彈出命令提示字元逐條執行，目錄只改名不刪除，清理後自動驗證",
                "Full 16-step sweep; cleanup runs in a command prompt; folders are renamed; verification follows");
            Add("consent.regTitle", "注册表清理确认", "登錄檔清理確認", "Confirm registry cleanup");
            Add("consent.regLead", "即将执行以下操作，请确认后继续：", "即將執行以下操作，請確認後繼續：", "The following will be performed:");
            Add("consent.itemRegCount", "将删除 {0} 项注册表垃圾", "將刪除 {0} 項登錄檔垃圾", "Delete {0} registry items");
            Add("consent.itemRegBackup", "先导出备份到：{0}", "先匯出備份到：{0}", "Back up to: {0}");
            Add("consent.itemRegCmd", "通过命令提示符执行 reg export / reg delete；驱动包会单独弹出管理员 cmd，用 pnputil 加 takeown / icacls / rd 强制删除，服务先 sc stop 再 sc delete；删除后逐项回查",
                "透過命令提示字元執行 reg export / reg delete；驅動套件會單獨彈出管理員 cmd，用 pnputil 加 takeown / icacls / rd 強制刪除，服務先 sc stop 再 sc delete；刪除後逐項回查",
                "reg export / reg delete in a command prompt; driver packages open a separate admin cmd with pnputil plus takeown / icacls / rd; services stop then delete; each item re-checked");
            Add("consent.itemRegReboot", "服务/驱动类项目删除后需要重启才会彻底生效", "服務/驅動類項目刪除後需要重啟才會徹底生效",
                "Service/driver items take effect after a reboot");
            Add("consent.itemRestorePoint", "删除前尝试创建系统还原点（系统保护关闭时会跳过并记录）",
                "刪除前嘗試建立系統還原點（系統保護關閉時會略過並記錄）",
                "A system restore point will be created first (skipped and logged if System Protection is off)");
            Add("regtab.rpOk", " · 已创建还原点", " · 已建立還原點", " · restore point created");
            Add("regtab.rpFail", " · 还原点创建失败（系统保护可能已关闭）", " · 還原點建立失敗（系統保護可能已關閉）",
                " · restore point failed (System Protection may be off)");

            Add("level.label", "范围", "範圍", "Scope");
            Add("level.low", "低", "低", "Low");
            Add("level.medium", "中", "中", "Mid");
            Add("level.high", "高", "高", "High");
            Add("level.extreme", "极限", "極限", "Max");
            Add("level.low.hint", "只读关键缓存与临时目录 · 秒级完成", "只讀關鍵快取與暫存目錄 · 秒級完成", "Key caches and temp folders only · instant");
            Add("level.medium.hint", "关键项 + 系统临时与更新缓存 · 需要管理员", "關鍵項 + 系統暫存與更新快取 · 需要管理員", "Key items + system temp and update cache · admin");
            Add("level.high.hint", "全盘逐文件排查 · 耗时较长", "全盤逐一檔案排查 · 耗時較長", "Full disk, file by file · takes a while");
            Add("level.extreme.hint", "全盘三轮 · 按顺序清理缓存 · 注册表最后", "全盤三輪 · 按順序清理快取 · 登錄檔最後", "3 passes · ordered cache cleanup · registry last");

            Add("scan.pass", "第 {0}/{1} 轮", "第 {0}/{1} 輪", "Pass {0}/{1}");
            Add("scan.levelChanged", "清理范围已切换为「{0}」，请重新扫描", "清理範圍已切換為「{0}」，請重新掃描", "Scope changed to {0}; please scan again");
            Add("scan.needAdmin", "需要管理员权限，已跳过", "需要管理員權限，已略過", "Needs administrator; skipped");
            Add("scan.appBusy", "{0} 个缓存正被程序占用，已跳过", "{0} 個快取正被程式佔用，已略過", "{0} caches are in use and were skipped");
            Add("scan.junk", "正在检查注册表垃圾…", "正在檢查登錄檔垃圾…", "Checking registry junk…");
            Add("sum.passes", " · 全盘 {0} 轮", " · 全盤 {0} 輪", " · {0} passes");
            Add("sum.restricted", " · {0} 项需要管理员", " · {0} 項需要管理員", " · {0} need admin");
            Add("sum.level", "{0} · ", "{0} · ", "{0} · ");

            Add("uninstall.title", "已安装软件", "已安裝軟體", "Installed apps");
            Add("uninstall.sub", "共 {0} 个程序 · 已统计 {1}", "共 {0} 個程式 · 已統計 {1}", "{0} apps · {1} measured");
            Add("uninstall.subScan", "正在读取注册表中的程序列表…", "正在讀取登錄檔中的程式清單…", "Reading the installed app list…");
            Add("uninstall.empty", "没有检测到可卸载的程序", "沒有偵測到可解除安裝的程式", "No removable apps detected");
            Add("uninstall.emptySub", "系统组件、运行库与更新补丁已按安全规则隐藏", "系統元件、執行階段與更新修補已依安全規則隱藏", "System components, runtimes and updates are hidden");
            Add("uninstall.noPublisher", "未知发布者", "未知發行者", "Unknown publisher");
            Add("uninstall.sizeUnknown", "未知", "未知", "unknown");
            Add("uninstall.mode.normal", "普通删除", "一般刪除", "Normal");
            Add("uninstall.mode.force", "强力删除", "強力刪除", "Force");
            Add("uninstall.mode.normal.hint", "删除安装目录内全部文件与快捷方式，注册表保留", "刪除安裝目錄內全部檔案與捷徑，登錄檔保留", "Removes install folder files and shortcuts; registry kept");
            Add("uninstall.mode.force.hint", "结束进程 + 命令删除 + 注册表清理，需要授权", "結束處理程序 + 命令刪除 + 登錄檔清理，需要授權", "Kills processes, runs commands, cleans registry; needs consent");
            Add("uninstall.btn", "卸载选中", "解除安裝選取", "Uninstall selected");
            Add("uninstall.btnForce", "强力删除选中", "強力刪除選取", "Force remove selected");
            Add("uninstall.refresh", "重新检测", "重新偵測", "Rescan");
            Add("uninstall.loading", "读取中", "讀取中", "Loading");
            Add("uninstall.reading", "已读取 {0} 个程序", "已讀取 {0} 個程式", "{0} apps read");
            Add("uninstall.protectedChip", "受保护", "受保護", "Protected");
            Add("uninstall.noPath", "未找到安装目录", "找不到安裝目錄", "No install folder");
            Add("uninstall.count", "{0} 项", "{0} 項", "{0} items");
            Add("uninstall.subProtect", " · {0} 个受保护", " · {0} 個受保護", " · {0} protected");
            Add("uninstall.allUsers", "所有用户", "所有使用者", "All users");
            Add("uninstall.thisUser", "当前用户", "目前使用者", "This user");
            Add("uninstall.selected", "已选 {0} 个程序", "已選 {0} 個程式", "{0} apps selected");
            Add("uninstall.noSelection", "请先选择要卸载的软件", "請先選擇要解除安裝的軟體", "Select an app first");
            Add("uninstall.elevateHint", "强力删除需要管理员权限，确认后将重新以管理员身份启动", "強力刪除需要管理員權限，確認後將以管理員身分重新啟動", "Force removal needs admin; the app will restart elevated");
            Add("uninstall.refused", "已阻止：{0}", "已阻止：{0}", "Blocked: {0}");
            Add("uninstall.failFile", "无法删除：{0}", "無法刪除：{0}", "Could not delete: {0}");
            Add("uninstall.takeownPartial", "已尝试获取所有权，仍有文件被占用", "已嘗試取得所有權，仍有檔案被佔用", "Ownership taken; some files are still locked");
            Add("uninstall.doneNormal", "已删除 {0} 个文件 · {1} 个快捷方式", "已刪除 {0} 個檔案 · {1} 個捷徑", "{0} files · {1} shortcuts removed");
            Add("uninstall.doneForce", "已删除 {0} 个文件 · {1} 个注册表项", "已刪除 {0} 個檔案 · {1} 個登錄檔項目", "{0} files · {1} registry keys removed");
            Add("uninstall.stepKill", "结束相关进程", "結束相關處理程序", "Stopping processes");
            Add("uninstall.stepFiles", "删除安装目录文件", "刪除安裝目錄檔案", "Deleting install folder");
            Add("uninstall.stepTakeown", "获取目录所有权", "取得目錄所有權", "Taking ownership");
            Add("uninstall.stepRegistry", "备份并删除注册表项", "備份並刪除登錄檔項目", "Backing up and deleting registry");
            Add("uninstall.stepShortcut", "清理快捷方式", "清理捷徑", "Cleaning shortcuts");
            Add("uninstall.finished", "卸载完成", "解除安裝完成", "Uninstall complete");
            Add("uninstall.removedChip", "文件已删除", "檔案已刪除", "Files removed");
            Add("uninstall.resultKeepReg", "安装目录已清空，注册表条目保留；可切换到强力删除继续清理",
                "安裝目錄已清空，登錄檔項目保留；可切換到強力刪除繼續清理",
                "Install folder cleared. Registry entries remain; switch to Force to finish");
            Add("uninstall.resultSub", "已处理 {0} 个文件", "已處理 {0} 個檔案", "{0} files processed");
            Add("uninstall.protectRuntime", "系统运行库或组件，禁止强制删除", "系統執行階段或元件，禁止強制刪除", "System runtime or component; force delete blocked");
            Add("uninstall.protectMicrosoft", "Microsoft 系统组件，禁止强制删除", "Microsoft 系統元件，禁止強制刪除", "Microsoft system component; force delete blocked");
            Add("uninstall.protectSystemPath", "安装目录位于系统保护路径", "安裝目錄位於系統保護路徑", "Install folder is inside a protected path");
            Add("uninstall.protectTooShallow", "安装目录层级过浅，疑似根目录", "安裝目錄層級過淺，疑似根目錄", "Install folder is too close to the drive root");
            Add("uninstall.protectNoPath", "未找到安装目录，无法安全删除", "找不到安裝目錄，無法安全刪除", "No install folder found; deletion blocked");

            Add("dlg.consent", "权限与风险确认", "權限與風險確認", "Permission and risk");
            Add("consent.lead", "即将执行以下操作，请逐项确认：", "即將執行以下操作，請逐項確認：", "The following actions will run. Please review:");
            Add("consent.warn", "程序不会修改系统关键文件、驱动、分区与启动配置；每条命令都限定在上面列出的目标范围内，并写入本地日志。",
                "程式不會修改系統關鍵檔案、驅動程式、磁碟分割與開機設定；每條命令都限定在上方列出的目標範圍內，並寫入本機記錄。",
                "System files, drivers, partitions and boot settings are never modified. Every command is limited to the targets listed above and written to the local log.");
            Add("consent.check", "我已阅读并了解上述风险，并同意以管理员权限执行", "我已閱讀並了解上述風險，並同意以管理員權限執行",
                "I understand the risks above and agree to run these actions as administrator");
            Add("consent.ok", "同意并继续", "同意並繼續", "Agree and continue");
            Add("consent.adminTitle", "管理员权限确认", "管理員權限確認", "Administrator consent");
            Add("consent.itemElevate", "以管理员身份重新启动本程序", "以管理員身分重新啟動本程式", "Restart this app as administrator");
            Add("consent.itemScan3", "对 C 盘执行 3 轮只读全盘扫描（不修改任何文件）", "對 C 槽執行 3 輪唯讀全盤掃描（不修改任何檔案）", "Run 3 read-only full scans of C: (no files changed)");
            Add("consent.itemRegistryScan", "按 16 步流程读取目录、计划任务、启动项、防火墙、服务、全量注册表（含 SOFTWARE / Classes / Installer / 事件日志源）、驱动与浏览器扩展",
                "依 16 步流程讀取目錄、排程工作、啟動項、防火牆、服務、全量登錄檔（含 SOFTWARE / Classes / Installer / 事件記錄來源）、驅動與瀏覽器擴充",
                "Read folders, tasks, startups, firewall, services, the full registry (SOFTWARE / Classes / Installer / event logs), drivers and browser extensions in 16 steps");
            Add("consent.itemRegistryLater", "注册表删除会单独再次确认，并自动导出 .reg 备份", "登錄檔刪除會單獨再次確認，並自動匯出 .reg 備份", "Registry deletion is confirmed separately and backed up to .reg first");
            Add("consent.itemRegScan", "以只读方式执行 16 步残留扫描（任务 / 防火墙 / 服务 / 全量注册表 / 驱动 / 浏览器）",
                "以唯讀方式執行 16 步殘留掃描（工作 / 防火牆 / 服務 / 全量登錄檔 / 驅動 / 瀏覽器）",
                "Run the 16-step leftover sweep read-only (tasks / firewall / services / full registry / drivers / browsers)");
            Add("consent.itemKill", "结束该软件正在运行的进程（taskkill /F /PID）", "結束該軟體正在執行的處理程序（taskkill /F /PID）", "Stop the app's running processes (taskkill /F /PID)");
            Add("consent.itemForceFiles", "删除安装目录中的文件，必要时获取目录所有权（takeown / icacls）", "刪除安裝目錄中的檔案，必要時取得目錄所有權（takeown / icacls）",
                "Delete files in the install folder, taking ownership if needed (takeown / icacls)");
            Add("consent.itemRegDeleteShort", "备份并删除该软件自身的注册表项（reg export / reg delete）", "備份並刪除該軟體自身的登錄檔項目（reg export / reg delete）",
                "Back up and delete the app's own registry keys (reg export / reg delete)");
            Add("consent.itemRegDelete", "备份并删除选中的 {0} 项注册表垃圾", "備份並刪除選取的 {0} 項登錄檔垃圾", "Back up and delete {0} selected registry items");
            Add("consent.itemBackup", "备份目录：{0}", "備份目錄：{0}", "Backup folder: {0}");
            Add("consent.itemRegScope", "白名单只放行可验证的残留：Uninstall / App Paths / Run / RunOnce / StartupApproved / SOFTWARE 深层失效路径值 / Classes / Installer / 服务 / 事件日志源 / 设备类筛选器 / Enum 隐藏设备；系统与硬件关键键一律拒绝",
                "白名單只放行可驗證的殘留：Uninstall / App Paths / Run / RunOnce / StartupApproved / SOFTWARE 深層失效路徑值 / Classes / Installer / 服務 / 事件記錄來源 / 裝置類篩選器 / Enum 隱藏裝置；系統與硬體關鍵鍵一律拒絕",
                "Only verifiable leftovers are allowed: Uninstall / App Paths / Run / RunOnce / StartupApproved / dead path values deep in SOFTWARE / Classes / Installer / services / event-log sources / class filters / Enum hidden devices; critical system and hardware keys are always refused");
            Add("consent.itemNoSystem", "不会修改系统关键文件、驱动、分区与启动配置", "不會修改系統關鍵檔案、驅動程式、磁碟分割與開機設定", "System files, drivers, partitions and boot settings are never modified");
            Add("consent.elevateFail", "提权被取消或失败，操作未执行。", "提權被取消或失敗，操作未執行。", "Elevation was canceled; nothing was executed.");

            Add("dlg.uninstall", "确认卸载", "確認解除安裝", "Confirm uninstall");
            Add("dlg.forceUninstall", "确认强力删除", "確認強力刪除", "Confirm force removal");
            Add("uconfirm.files", "目录内 {0} 个文件 · {1}", "目錄內 {0} 個檔案 · {1}", "{0} files · {1}");
            Add("uconfirm.registryEmpty", "未找到该软件的独立注册表项", "找不到該軟體的獨立登錄檔項目", "No app-specific registry keys found");
            Add("uconfirm.regMore", "另有 {0} 个注册表项", "另有 {0} 個登錄檔項目", "+{0} more registry keys");
            Add("uconfirm.app", "目标程序", "目標程式", "Target app");
            Add("uconfirm.path", "安装目录", "安裝目錄", "Install folder");
            Add("uconfirm.registry", "注册表", "登錄檔", "Registry");
            Add("uconfirm.registryNone", "不修改注册表（普通删除）", "不修改登錄檔（一般刪除）", "Registry untouched (normal)");
            Add("uconfirm.commands", "将执行的命令", "將執行的命令", "Commands");
            Add("uconfirm.noCommands", "无，仅文件删除", "無，僅刪除檔案", "None; file deletion only");
            Add("uconfirm.backup", "删除前会先备份到 {0}", "刪除前會先備份到 {0}", "Backed up to {0} first");
            Add("uconfirm.warnNormal", "普通删除只清除安装目录中的文件与快捷方式，注册表与卸载条目会保留，之后可用强力删除继续清理。",
                "一般刪除只清除安裝目錄中的檔案與捷徑，登錄檔與解除安裝項目會保留，之後可用強力刪除繼續清理。",
                "Normal removal only clears the install folder and shortcuts. Registry entries remain and can be cleaned later with Force.");
            Add("uconfirm.warnForce", "强力删除不会调用软件自带的卸载程序，删除后无法还原，请确认该软件不再需要。",
                "強力刪除不會呼叫軟體內建的解除安裝程式，刪除後無法還原，請確認該軟體已不再需要。",
                "Force removal does not run the vendor uninstaller. It cannot be undone; make sure the app is no longer needed.");
            Add("uconfirm.check", "我已确认要删除该软件，并了解此操作不可撤销", "我已確認要刪除該軟體，並了解此操作不可復原",
                "I confirm this app should be removed and understand it cannot be undone");
            Add("uconfirm.ok", "开始卸载", "開始解除安裝", "Remove now");

            Add("cat.reg_junk.name", "注册表垃圾", "登錄檔垃圾", "Registry junk");
            Add("cat.reg_junk.desc", "按 16 步流程全量扫描软件残留：目录、任务、防火墙、服务、注册表、驱动、浏览器与日志",
                "依 16 步流程全量掃描軟體殘留：目錄、工作、防火牆、服務、登錄檔、驅動、瀏覽器與記錄",
                "Full 16-step sweep: folders, tasks, firewall, services, registry, drivers, browsers and logs");
            Add("reg.stepUninstall", "检查孤立卸载项…", "檢查孤立解除安裝項目…", "Checking orphaned uninstall entries…");
            Add("reg.stepAppPaths", "检查失效的 App Paths…", "檢查失效的 App Paths…", "Checking dead App Paths…");
            Add("reg.stepRun", "检查失效的启动项…", "檢查失效的啟動項…", "Checking dead startup items…");
            Add("reg.stepMainKeys", "检查软件残留主键…", "檢查軟體殘留主鍵…", "Checking leftover software keys…");
            Add("reg.stepServices", "检查失效的服务与驱动…", "檢查失效的服務與驅動…", "Checking dead services and drivers…");
            Add("reg.reasonOrphanKey", "卸载项已失效，对应的软件主键仍然残留", "解除安裝項目已失效，對應的軟體主鍵仍然殘留",
                "Uninstall entry is gone but the software key is still here");
            Add("reg.reasonDeadPath", "安装路径指向不存在的目录：{0}", "安裝路徑指向不存在的目錄：{0}", "Install path points to a missing folder: {0}");
            Add("reg.reasonService", "服务/驱动的文件已不存在：{0}", "服務/驅動的檔案已不存在：{0}", "Service/driver file is missing: {0}");
            Add("reg.kind.uninstall", "卸载项", "解除安裝項", "Uninstall");
            Add("reg.kind.apppath", "路径", "路徑", "App Path");
            Add("reg.kind.run", "启动项", "啟動項", "Startup");
            Add("reg.kind.main", "主键", "主鍵", "Main key");
            Add("reg.kind.service", "服务", "服務", "Service");
            Add("reg.kind.folder", "目录", "目錄", "Folder");
            Add("reg.kind.shortcut", "快捷方式", "捷徑", "Shortcut");
            Add("reg.stepLeftovers", "检查残留目录…", "檢查殘留目錄…", "Checking leftover folders…");
            Add("reg.stepShortcuts", "检查失效快捷方式…", "檢查失效捷徑…", "Checking dead shortcuts…");
            Add("reg.reasonFolder", "软件已卸载，目录里已没有任何可执行文件：{0}（将改名观察，可随时改回）",
                "軟體已解除安裝，目錄裡已沒有任何可執行檔：{0}（將改名觀察，可隨時改回）",
                "App is gone and the folder has no executables left: {0} (renamed, reversible)");
            Add("reg.reasonShortcut", "快捷方式指向的文件已不存在：{0}", "捷徑指向的檔案已不存在：{0}", "Shortcut points to a missing file: {0}");
            Add("reg.needReboot", "需重启", "需重啟", "Reboot");
            Add("reg.reasonOrphan", "卸载信息指向的程序已不存在", "解除安裝資訊指向的程式已不存在", "The referenced program no longer exists");
            Add("reg.reasonAppPath", "App Paths 指向的文件不存在：{0}", "App Paths 指向的檔案不存在：{0}", "App Paths points to a missing file: {0}");
            Add("reg.reasonRun", "启动项指向的文件不存在：{0}", "啟動項指向的檔案不存在：{0}", "Startup item points to a missing file: {0}");

            Add("reg.stepS1", "第 1 步 · 检查 Program Files 安装目录…", "第 1 步 · 檢查 Program Files 安裝目錄…", "Step 1 · Program Files install folders…");
            Add("reg.stepS2", "第 2 步 · 检查 ProgramData 公共配置与缓存…", "第 2 步 · 檢查 ProgramData 公用設定與快取…", "Step 2 · ProgramData config and caches…");
            Add("reg.stepS3", "第 3 步 · 检查 AppData / LocalLow 用户配置…", "第 3 步 · 檢查 AppData / LocalLow 使用者設定…", "Step 3 · AppData / LocalLow user data…");
            Add("reg.stepS4", "第 4 步 · 检查计划任务…", "第 4 步 · 檢查排程工作…", "Step 4 · Scheduled tasks…");
            Add("reg.stepS5", "第 5 步 · 检查 Run / RunOnce 启动项…", "第 5 步 · 檢查 Run / RunOnce 啟動項…", "Step 5 · Run / RunOnce startup items…");
            Add("reg.stepS6", "第 6 步 · 检查防火墙僵尸规则…", "第 6 步 · 檢查防火牆殭屍規則…", "Step 6 · Dead firewall rules…");
            Add("reg.stepS7", "第 7 步 · 检查残留服务与驱动…", "第 7 步 · 檢查殘留服務與驅動…", "Step 7 · Leftover services and drivers…");
            Add("reg.stepS8", "第 8 步 · 检查卸载注册表项…", "第 8 步 · 檢查解除安裝登錄檔項目…", "Step 8 · Uninstall registry entries…");
            Add("reg.stepS9", "第 9 步 · 全量扫描 SOFTWARE 软件主键…", "第 9 步 · 全量掃描 SOFTWARE 軟體主鍵…", "Step 9 · Full SOFTWARE registry sweep…");
            Add("reg.stepS10", "第 10 步 · 全量扫描 Classes 关联与右键菜单…", "第 10 步 · 全量掃描 Classes 關聯與右鍵選單…", "Step 10 · Full Classes associations and context menus…");
            Add("reg.stepS11", "第 11 步 · 检查 Installer 安装缓存…", "第 11 步 · 檢查 Installer 安裝快取…", "Step 11 · Installer caches…");
            Add("reg.stepS12", "第 12 步 · 检查驱动与设备类 GUID…", "第 12 步 · 檢查驅動與裝置類 GUID…", "Step 12 · Drivers and device class GUIDs…");
            Add("reg.stepS13", "第 13 步 · 检查浏览器扩展残留…", "第 13 步 · 檢查瀏覽器擴充功能殘留…", "Step 13 · Browser extension leftovers…");
            Add("reg.stepS14", "第 14 步 · 检查快捷方式与应用列表…", "第 14 步 · 檢查捷徑與應用程式清單…", "Step 14 · Shortcuts and app registrations…");
            Add("reg.stepS15", "第 15 步 · 检查事件日志源与临时/更新缓存…", "第 15 步 · 檢查事件記錄來源與暫存/更新快取…", "Step 15 · Event log sources, temp and update caches…");
            Add("reg.stepS16", "第 16 步 · 清理后验证…", "第 16 步 · 清理後驗證…", "Step 16 · Post-clean verification…");

            Add("reg.reasonDirNoExe", "目录里没有可执行文件，也没有匹配的安装记录：{0}（将改名观察，可随时改回）",
                "目錄裡沒有執行檔，也沒有相符的安裝記錄：{0}（將改名觀察，可隨時改回）",
                "No executables and no matching install record: {0} (renamed, reversible)");
            Add("reg.reasonTask", "计划任务指向的程序已不存在：{0}", "排程工作指向的程式已不存在：{0}", "Scheduled task points to a missing program: {0}");
            Add("reg.reasonStartupApproved", "启动项已删除，启动审批记录仍然残留：{0}", "啟動項已刪除，啟動核准記錄仍然殘留：{0}",
                "Startup item is gone but its approval record remains: {0}");
            Add("reg.reasonFirewall", "防火墙规则指向的程序已不存在：{0}", "防火牆規則指向的程式已不存在：{0}",
                "Firewall rule points to a missing program: {0}");
            Add("reg.reasonCom", "COM 注册指向的文件已不存在：{0}", "COM 註冊指向的檔案已不存在：{0}", "COM registration points to a missing file: {0}");
            Add("reg.reasonMenu", "右键菜单处理程序的组件已不存在：{0}", "右鍵選單處理程式的元件已不存在：{0}",
                "Context-menu handler component is missing: {0}");
            Add("reg.reasonProgId", "文件关联命令指向的程序已不存在：{0}", "檔案關聯命令指向的程式已不存在：{0}",
                "File-association command points to a missing program: {0}");
            Add("reg.reasonOpenWith", "打开方式记录的 ProgID 已不存在：{0}", "開啟方式記錄的 ProgID 已不存在：{0}",
                "Open-with entry references a missing ProgID: {0}");
            Add("reg.reasonOpenWithExe", "打开方式列表指向的程序已不存在：{0}", "開啟方式清單指向的程式已不存在：{0}",
                "Open-with list points to a missing program: {0}");
            Add("reg.reasonInstaller", "安装缓存指向的文件已不存在：{0}（先备份再删除）", "安裝快取指向的檔案已不存在：{0}（先備份再刪除）",
                "Installer cache points to a missing file: {0} (backed up first)");
            Add("reg.reasonDriverFilter", "筛选器驱动的文件已不存在：{0}", "篩選器驅動的檔案已不存在：{0}", "Filter driver file is missing: {0}");
            Add("reg.reasonDriverPkg", "驱动包未被任何设备或服务引用：{0}（会打开管理员 cmd 强制删除，需重启）",
                "驅動套件未被任何裝置或服務引用：{0}（會開啟管理員 cmd 強制刪除，需重啟）",
                "Driver package is not referenced by any device or service: {0} (admin cmd force-delete, reboot)");
            Add("reg.reasonDevice", "隐藏设备引用的服务已不存在：{0}", "隱藏裝置引用的服務已不存在：{0}",
                "Hidden device references a missing service: {0}");
            Add("reg.reasonEventLog", "事件日志源引用的消息文件已不存在：{0}", "事件記錄來源引用的訊息檔案已不存在：{0}",
                "Event log source references a missing message file: {0}");
            Add("reg.reasonBrowser", "浏览器扩展已移除，扩展数据仍然残留：{0}", "瀏覽器擴充功能已移除，擴充資料仍然殘留：{0}",
                "Browser extension is gone but its data remains: {0}");
            Add("reg.reasonTemp", "临时文件，7 天以上未修改：{0}", "暫存檔案，7 天以上未修改：{0}", "Temporary file untouched for over 7 days: {0}");
            Add("reg.reasonWU", "Windows 更新缓存，可重新下载：{0}", "Windows 更新快取，可重新下載：{0}", "Windows Update cache, downloadable again: {0}");
            Add("reg.reasonRegisteredApp", "已注册应用指向的能力键已不存在：{0}", "已註冊應用程式指向的能力機碼已不存在：{0}",
                "Registered app points to a missing capability key: {0}");

            Add("reg.kind.task", "任务", "工作", "Task");
            Add("reg.kind.firewall", "防火墙", "防火牆", "Firewall");
            Add("reg.kind.com", "COM", "COM", "COM");
            Add("reg.kind.menu", "菜单", "選單", "Menu");
            Add("reg.kind.progid", "关联", "關聯", "Assoc");
            Add("reg.kind.openwith", "打开方式", "開啟方式", "Open with");
            Add("reg.kind.installer", "安装缓存", "安裝快取", "Installer");
            Add("reg.kind.driver", "驱动", "驅動", "Driver");
            Add("reg.kind.device", "设备", "裝置", "Device");
            Add("reg.kind.eventlog", "日志", "記錄", "Event log");
            Add("reg.kind.browser", "扩展", "擴充", "Extension");
            Add("reg.kind.temp", "临时", "暫存", "Temp");

            Add("verify.title", "清理后验证", "清理後驗證", "Post-clean verification");
            Add("verify.lead", "已按第 16 步重新检查关键位置：", "已依第 16 步重新檢查關鍵位置：", "Key locations were re-checked as step 16:");
            Add("verify.checkLeftovers", "残留项总数", "殘留項總數", "Remaining leftovers");
            Add("verify.checkTasks", "计划任务", "排程工作", "Scheduled tasks");
            Add("verify.checkFirewall", "防火墙僵尸规则", "防火牆殭屍規則", "Dead firewall rules");
            Add("verify.checkServices", "服务与驱动", "服務與驅動", "Services and drivers");
            Add("verify.checkEventLog", "事件日志源", "事件記錄來源", "Event log sources");
            Add("verify.checkDevices", "隐藏设备", "隱藏裝置", "Hidden devices");
            Add("verify.stateOk", "正常", "正常", "OK");
            Add("verify.stateFound", "仍有 {0} 项", "仍有 {0} 項", "{0} remaining");
            Add("verify.stateUnknown", "无法检查（需要管理员）", "無法檢查（需要管理員）", "Unavailable (admin required)");
            Add("verify.btnSfc", "运行 sfc /scannow", "執行 sfc /scannow", "Run sfc /scannow");
            Add("verify.btnDism", "DISM 健康检查", "DISM 健康檢查", "DISM health check");
            Add("verify.btnEventLog", "打开事件查看器", "開啟事件檢視器", "Open Event Viewer");
            Add("verify.btnRestore", "系统还原", "系統還原", "System Restore");
            Add("verify.note", "sfc / DISM 会在新窗口运行；若发现异常，可用系统还原回退到清理前的还原点。",
                "sfc / DISM 會在新視窗執行；若發現異常，可用系統還原回退到清理前的還原點。",
                "sfc / DISM open in a new window. If something breaks, roll back with System Restore.");
            Add("verify.rpNote", "清理前创建的还原点：{0}", "清理前建立的還原點：{0}", "Restore point created before cleaning: {0}");
            Add("btn.verify", "清理后验证", "清理後驗證", "Verify");
            Add("regtab.verifyTip", "重新检查计划任务、防火墙、服务、事件日志与隐藏设备",
                "重新檢查排程工作、防火牆、服務、事件記錄與隱藏裝置",
                "Re-check tasks, firewall, services, event logs and hidden devices");
            Add("reg.failItem", "删除失败：{0}", "刪除失敗：{0}", "Delete failed: {0}");
            Add("reg.hint", "共 {0} 项 · 删除前逐项备份为 .reg 文件", "共 {0} 項 · 刪除前逐項備份為 .reg 檔案", "{0} items · each backed up as a .reg file first");
            Add("reg.itemCount", "{0} 项", "{0} 項", "{0} items");
            Add("reg.checked", "已检查", "已檢查", "Checked");
            Add("reg.none", "未发现残留项（已完成 16 步扫描：目录、任务、启动项、防火墙、服务、全量注册表、Classes、Installer、驱动、浏览器扩展、快捷方式、事件日志与临时缓存）",
                "未發現殘留項（已完成 16 步掃描：目錄、工作、啟動項、防火牆、服務、全量登錄檔、Classes、Installer、驅動、瀏覽器擴充、捷徑、事件記錄與暫存快取）",
                "No leftovers found (16-step sweep completed: folders, tasks, startups, firewall, services, full registry, Classes, Installer, drivers, browser extensions, shortcuts, event logs and temp caches)");
            Add("reg.needAdmin", "已找到注册表垃圾，但删除需要管理员权限", "已找到登錄檔垃圾，但刪除需要管理員權限",
                "Registry junk found; administrator rights are required to delete it");
            Add("sum.regJunk", " · 注册表垃圾 {0} 项", " · 登錄檔垃圾 {0} 項", " · {0} registry items");

            Add("clean.stepRegistry", "清理注册表垃圾", "清理登錄檔垃圾", "Cleaning registry junk");
            Add("clean.skipRunning", "程序正在运行，已跳过：{0}", "程式正在執行，已略過：{0}", "App is running, skipped: {0}");
            Add("footer.order", "清理顺序 ① 系统缓存 → ② 软件缓存 → ③ 卸载残留 → ④ Local 缓存 → ⑤ 回收站 → ⑥ 注册表",
                "清理順序 ① 系統快取 → ② 軟體快取 → ③ 解除安裝殘留 → ④ Local 快取 → ⑤ 資源回收筒 → ⑥ 登錄檔",
                "Order ① system cache → ② app cache → ③ leftovers → ④ Local cache → ⑤ Recycle Bin → ⑥ registry");
            Add("result.statRegistry", "注册表项", "登錄檔項目", "Registry keys");
            Add("result.backup", "注册表已备份至 {0}", "登錄檔已備份至 {0}", "Registry backed up to {0}");

            Add("splash.line1", "加载本地规则引擎", "載入本機規則引擎", "Loading local rule engine");
            Add("splash.line2", "读取磁盘与注册表结构", "讀取磁碟與登錄檔結構", "Reading disk and registry layout");
            Add("splash.line3", "准备界面与安全拦截", "準備介面與安全攔截", "Preparing UI and safety guards");
            Add("splash.foot", "本地规则引擎 · 不上传任何文件", "本機規則引擎 · 不上傳任何檔案", "Local rules · nothing is uploaded");
            Add("splash.skip", "点击任意处跳过", "點擊任意處跳過", "Click anywhere to skip");

            Add("pdetail.title", "软件信息", "軟體資訊", "App details");
            Add("pdetail.path", "安装目录", "安裝目錄", "Install folder");
            Add("pdetail.size", "占用体积", "佔用大小", "Size");
            Add("pdetail.registry", "卸载项", "解除安裝項目", "Uninstall key");
            Add("pdetail.uninstall", "卸载命令", "解除安裝命令", "Uninstall command");
            Add("pdetail.none", "未提供", "未提供", "not provided");
            Add("pdetail.protected", "受保护：", "受保護：", "Protected: ");
            Add("pdetail.forceNote", "普通删除会清空安装目录；强力删除会额外结束进程、调用命令并删除注册表项，均需勾选免责声明后执行。",
                "一般刪除會清空安裝目錄；強力刪除會額外結束處理程序、呼叫命令並刪除登錄檔項目，均需勾選免責聲明後執行。",
                "Normal removal clears the install folder. Force removal also stops processes, runs commands and deletes registry keys, all behind a consent check.");

            Add("theme.toDark", "切换深色模式", "切換深色模式", "Switch to dark mode");
            Add("theme.toLight", "切换浅色模式", "切換淺色模式", "Switch to light mode");

            // ------------------------------------------------------------ 文件模式
            Add("view.normal", "普通模式", "普通模式", "Normal");
            Add("view.file", "文件模式", "檔案模式", "File map");
            Add("btn.ok", "知道了", "知道了", "OK");

            Add("map.title", "文件地图", "檔案地圖", "File map");
            Add("map.sub", "只读扫描 C 盘，区块面积与体积成正比", "唯讀掃描 C 槽，區塊面積與大小成正比",
                "Read-only scan of C:; block area matches size");
            Add("map.hint", "左键打开 · 双击钻入 · 右键操作", "左鍵開啟 · 雙擊鑽入 · 右鍵操作",
                "Click to open · double-click to zoom · right-click for actions");
            Add("map.opHint", "删除永久生效 · 黑色区块为系统禁区", "刪除永久生效 · 黑色區塊為系統禁區",
                "Deletes are permanent · black blocks are untouchable");
            Add("map.scanning", "正在建立文件地图…", "正在建立檔案地圖…", "Building file map…");
            Add("map.refreshing", "正在刷新该区块…", "正在重新整理該區塊…", "Refreshing block…");
            Add("map.scanError", "建图失败：{0}", "建立地圖失敗：{0}", "File map failed: {0}");
            Add("map.subDone", "{0} 个文件 · {1}", "{0} 個檔案 · {1}", "{0} files · {1}");
            Add("map.denied", " · 受限目录 {0}", " · 受限目錄 {0}", " · {0} restricted folders");
            Add("map.truncated", "节点已达上限，部分目录未展开", "節點已達上限，部分目錄未展開",
                "Node cap reached; some folders collapsed");
            Add("map.empty", "没有可显示的区块", "沒有可顯示的區塊", "No blocks to show");
            Add("map.refresh", "刷新", "重新整理", "Refresh");

            Add("map.risk.safe", "可清理", "可清理", "Safe");
            Add("map.risk.caution", "谨慎", "謹慎", "Caution");
            Add("map.risk.risky", "勿动", "勿動", "Do not touch");
            Add("map.risk.forbidden", "禁区", "禁區", "Forbidden");
            Add("map.eval.safe", "已确认的缓存与临时文件，可安全清理", "已確認的快取與暫存檔案，可安全清理",
                "Verified cache and temp files; safe to clean");
            Add("map.eval.caution", "清理会影响部分功能（旧系统回退、排障日志），建议确认",
                "清理會影響部分功能（舊系統回退、排錯記錄），建議確認",
                "Cleaning may affect rollback or diagnostics; review first");
            Add("map.eval.risky", "个人资料 / 程序 / 未分类内容，删除后不可恢复，请人工确认",
                "個人資料 / 程式 / 未分類內容，刪除後無法復原，請人工確認",
                "Personal data, programs or uncategorized content; deletion cannot be undone");
            Add("map.eval.forbidden", "系统禁区：绝对不可触碰，删除与剪切已禁用",
                "系統禁區：絕對不可觸碰，刪除與剪下已停用",
                "System zone: untouchable; delete and cut are disabled");

            Add("map.tip.share", "占父级 {0}", "佔上層 {0}", "{0} of parent");
            Add("map.tip.files", "{0} 个文件", "{0} 個檔案", "{0} files");
            Add("map.tip.hint", "左键打开 · 双击钻入 · 右键更多", "左鍵開啟 · 雙擊鑽入 · 右鍵更多",
                "Click to open · double-click to zoom · right-click for more");

            Add("map.legend.safe", "可清理", "可清理", "Safe");
            Add("map.legend.caution", "谨慎", "謹慎", "Caution");
            Add("map.legend.risky", "勿动", "勿動", "Risky");
            Add("map.legend.forbidden", "禁区", "禁區", "Forbidden");

            Add("menu.delete", "删除", "刪除", "Delete");
            Add("menu.cut", "剪切", "剪下", "Cut");
            Add("menu.paste", "粘贴", "貼上", "Paste");
            Add("menu.copy", "复制", "複製", "Copy");
            Add("menu.props", "属性", "屬性", "Properties");
            Add("menu.junk", "垃圾清理排查", "垃圾清理排查", "Junk scan");

            Add("map.dlg.head", "请确认以下操作", "請確認以下操作", "Confirm the following action");
            Add("map.dlg.dangerHead", "高风险操作", "高風險操作", "High-risk action");
            Add("map.del.size", "体积 {0} · {1} 个文件", "大小 {0} · {1} 個檔案", "{0} · {1} files");
            Add("map.del.permanent", "删除为永久删除，不进回收站", "刪除為永久刪除，不進資源回收筒",
                "Deletion is permanent (no Recycle Bin)");
            Add("map.del.risky", "该区块可能是个人资料或程序文件，删除后无法恢复",
                "該區塊可能是個人資料或程式檔案，刪除後無法復原",
                "This block may contain personal data or programs; it cannot be restored");
            Add("map.del.caution", "该区块为谨慎项，删除可能影响回退或排障",
                "該區塊為謹慎項，刪除可能影響回退或排錯",
                "Caution: deleting may affect rollback or diagnostics");
            Add("map.del.safe", "该区块为已确认的缓存 / 临时内容", "該區塊為已確認的快取 / 暫存內容",
                "Verified cache or temp content");
            Add("map.del.ok", "永久删除", "永久刪除", "Delete permanently");
            Add("map.cut.risky", "该区块可能是个人资料，剪切会移动它；确认继续？",
                "該區塊可能是個人資料，剪下會移動它；確認繼續？",
                "This block may be personal data and will be moved. Continue?");
            Add("map.cut.ok", "剪切", "剪下", "Cut");

            Add("map.paste.empty", "剪贴板里没有文件", "剪貼簿裡沒有檔案", "Clipboard has no files");
            Add("map.paste.noTarget", "目标目录不存在", "目標目錄不存在", "Target folder does not exist");
            Add("map.paste.fail", "粘贴失败（错误码 {0}）", "貼上失敗（錯誤碼 {0}）", "Paste failed (code {0})");
            Add("map.paste.moving", "正在移动", "正在移動", "Moving");
            Add("map.paste.copying", "正在复制", "正在複製", "Copying");
            Add("map.paste.black", "目标目录属于系统禁区，禁止写入", "目標目錄屬於系統禁區，禁止寫入",
                "Target folder is a system zone; writing is blocked");
            Add("map.op.failTitle", "操作失败", "操作失敗", "Action failed");
            Add("map.op.missing", "路径已不存在", "路徑已不存在", "Path no longer exists");

            Add("map.scan.title", "正在建立文件地图", "正在建立檔案地圖", "Building file map");
            Add("map.scan.sub", "只读遍历 C 盘的目录与文件，不改动任何内容",
                "唯讀遍歷 C 槽的目錄與檔案，不改動任何內容",
                "Read-only walk of C:; nothing is modified");
            Add("map.refresh.title", "正在刷新区块", "正在重新整理區塊", "Refreshing block");
            Add("map.refresh.sub", "重新统计该区块的体积与文件数", "重新統計該區塊的大小與檔案數",
                "Re-counting this block's size and files");
            Add("map.clean.title", "正在清理所选垃圾", "正在清理所選垃圾", "Cleaning selected junk");
            Add("map.clean.sub", "仅删除勾选的可清理项，其余内容保持不变", "僅刪除勾選的可清理項，其餘內容保持不變",
                "Only checked cleanable items are deleted");
            Add("map.clean.done", "已清理", "已清理", "Cleaned");
            Add("map.clean.total", "计划清理", "計畫清理", "Planned");
            Add("map.busy.note", "全程只读或仅删除已勾选项，系统禁区不会被动到",
                "全程唯讀或僅刪除已勾選項，系統禁區不會被動到",
                "Read-only, or only checked items; system zones are never touched");

            Add("junk.title", "垃圾清理排查", "垃圾清理排查", "Junk scan");
            Add("junk.total", "区块体积", "區塊大小", "Block size");
            Add("junk.reclaim", "可回收", "可回收", "Reclaimable");
            Add("junk.notCleanable", "勿动", "勿動", "Keep");
            Add("junk.forbidden", "禁区", "禁區", "Forbidden");
            Add("junk.cleanableTitle", "可清理项（按分类勾选）", "可清理項（依分類勾選）",
                "Cleanable items (check by category)");
            Add("junk.selected", "已选 {0}", "已選 {0}", "{0} selected");
            Add("junk.none", "没有发现可清理的垃圾", "沒有發現可清理的垃圾", "No cleanable junk found");
            Add("junk.fileCount", "{0} 个文件", "{0} 個檔案", "{0} files");
            Add("junk.more", "还有 {0} 类未显示", "還有 {0} 類未顯示", "{0} more categories");
            Add("junk.largest", "最大的可清理文件", "最大的可清理檔案", "Largest cleanable files");
            Add("junk.readonly", "排查本身只读；点「清理选中」才删除", "排查本身唯讀；點「清理選中」才刪除",
                "The scan is read-only; only \"Clean selected\" deletes");
            Add("junk.clean", "清理选中", "清理選中", "Clean selected");
            Add("junk.confirm", "将永久删除 {0} 个文件，释放 {1}", "將永久刪除 {0} 個檔案，釋放 {1}",
                "Permanently delete {0} files, freeing {1}");
            Add("junk.confirmPermanent", "删除不可恢复，系统禁区与个人文档会自动跳过",
                "刪除無法復原，系統禁區與個人文件會自動略過",
                "Cannot be undone; system zones and personal documents are skipped");
            Add("junk.confirmOk", "开始清理", "開始清理", "Clean now");
            Add("junk.done", "已删除 {0} 个文件，释放 {1}，跳过 {2}", "已刪除 {0} 個檔案，釋放 {1}，略過 {2}",
                "Deleted {0} files, freed {1}, skipped {2}");
            Add("junk.log", "日志：{0}", "記錄：{0}", "Log: {0}");
            Add("junk.doneTitle", "垃圾清理完成", "垃圾清理完成", "Junk cleanup finished");

            // ---- 主动防御
            Add("tab.defense", "防护设置", "防護設定", "Protection");
            Add("def.title", "主动防御", "主動防禦", "Active defense");
            Add("def.leftTitle", "防护状态", "防護狀態", "Protection");
            Add("def.overview", "实时监控", "即時監控", "Real-time monitoring");
            Add("def.coverage", "系统文件 · 关键注册表 · 启动项 · 危害文件", "系統檔案 · 關鍵登錄檔 · 啟動項 · 危害檔案",
                "System files · critical registry · startup · risky files");
            Add("def.stat.level", "防御档位", "防禦檔位", "Defense level");
            Add("def.stat.checks", "检查次数", "檢查次數", "Checks");
            Add("def.toggleOn", "已开启", "已開啟", "On");
            Add("def.toggleOff", "已关闭", "已關閉", "Off");
            Add("def.level.label", "档位", "檔位", "Level");
            Add("def.level.low", "低", "低", "Low");
            Add("def.level.high", "高", "高", "High");
            Add("def.level.lowHint", "每 60 秒检查 · 系统文件与关键注册表", "每 60 秒檢查 · 系統檔案與關鍵登錄檔",
                "Every 60 s · system files and critical registry");
            Add("def.level.highHint", "每 15 秒检查 · 含启动项与危害文件扫描", "每 15 秒檢查 · 含啟動項與危害檔案掃描",
                "Every 15 s · adds startup and risky-file scans");
            Add("def.status.off", "主动防御未开启，系统当前不受监控", "主動防禦未開啟，系統目前不受監控",
                "Active defense is off; the system is not monitored");
            Add("def.status.on", "运行中 · {0}档 · 上次检查 {1}", "執行中 · {0}檔 · 上次檢查 {1}",
                "Running · {0} · last check {1}");
            Add("def.status.noAdmin", "普通权限（部分处置需要管理员）", "一般權限（部分處置需要管理員）",
                "Standard user (some actions need admin)");
            Add("def.status.offShort", "未开启", "未開啟", "Off");
            Add("def.stat.files", "系统文件", "系統檔案", "System files");
            Add("def.stat.registry", "注册表项", "登錄檔項", "Registry keys");
            Add("def.stat.blocked", "已终止进程", "已終止處理程序", "Blocked processes");
            Add("def.threats.title", "风险项", "風險項", "Risks");
            Add("def.threats.hint", "点击任意风险查看说明与解决方案", "點擊任一風險查看說明與解決方案",
                "Open a risk to see details and solutions");
            Add("def.threats.hintEmpty", "每轮检查覆盖系统文件、关键注册表与启动项", "每輪檢查涵蓋系統檔案、關鍵登錄檔與啟動項",
                "Every round checks system files, registry and startup items");
            Add("def.threats.empty", "未发现风险", "未發現風險", "No risks found");
            Add("def.threats.emptySub", "系统完整性与启动项均正常", "系統完整性與啟動項均正常",
                "System integrity and startup items look clean");
            Add("def.threats.offTitle", "主动防御未开启", "主動防禦未開啟", "Active defense is off");
            Add("def.threats.offSub", "开启后会持续检查系统完整性并拦截篡改", "開啟後會持續檢查系統完整性並攔截篡改",
                "Turn it on to keep checking system integrity and block tampering");
            Add("def.threats.offHint", "点击右上角开关立即开启", "點擊右上角開關立即開啟",
                "Use the switch at the top-right to enable it");
            Add("def.btn.check", "立即检查", "立即檢查", "Check now");
            Add("def.btn.checking", "检查中…", "檢查中…", "Checking…");
            Add("def.btn.baseline", "重建基线", "重建基準", "Rebuild baseline");
            Add("def.btn.log", "打开日志", "開啟記錄", "Open log");
            Add("def.btn.fix", "立即修复", "立即修復", "Fix now");
            Add("def.btn.quarantine", "隔离文件", "隔離檔案", "Quarantine");
            Add("def.btn.disableStartup", "禁用启动项", "停用啟動項", "Disable startup");
            Add("def.btn.ignore", "忽略", "忽略", "Ignore");
            Add("def.baseline.ready", "基线已建立", "基準已建立", "Baseline ready");
            Add("def.baseline.notReady", "基线未建立", "基準未建立", "No baseline yet");
            Add("def.checks", "已检查 {0} 次", "已檢查 {0} 次", "{0} checks done");
            Add("def.baseline.confirmTitle", "重建完整性基线", "重建完整性基準", "Rebuild integrity baseline");
            Add("def.baseline.confirm1", "将把当前系统文件与关键注册表状态作为新的基线。",
                "將把目前系統檔案與關鍵登錄檔狀態作為新的基準。",
                "Current system files and critical registry values will become the new baseline.");
            Add("def.baseline.confirm2", "如果系统已被篡改，重建会把篡改后的状态当作正常状态，请先确认系统干净。",
                "如果系統已被篡改，重建會把篡改後的狀態當作正常狀態，請先確認系統乾淨。",
                "If the system is already tampered with, rebuilding will treat that state as normal. Make sure the system is clean first.");
            Add("def.baseline.confirmOk", "重建基线", "重建基準", "Rebuild");
            Add("def.error", "错误：", "錯誤：", "Error: ");
            Add("def.dialog.title", "风险详情", "風險詳情", "Risk details");
            Add("def.dialog.solutionTitle", "处理建议", "處理建議", "Suggested fix");
            Add("def.dialog.open", "打开位置", "開啟位置", "Open location");
            Add("def.toast.title", "主动防御拦截", "主動防禦攔截", "Active defense alert");
            Add("def.toast.body", "发现 {0} 项风险，已终止 {1} 个可疑进程", "發現 {0} 項風險，已終止 {1} 個可疑處理程序",
                "{0} risks found, {1} suspicious processes terminated");
            Add("def.toast.killedOnly", "已终止 {0} 个可疑进程", "已終止 {0} 個可疑處理程序",
                "{0} suspicious processes terminated");
            Add("def.toast.bodyOnly", "发现 {0} 项风险，点击查看解决方案", "發現 {0} 項風險，點擊查看解決方案",
                "{0} risks found. Click to see solutions");
            Add("def.toast.view", "查看方案", "查看方案", "View");
            Add("def.kind.file", "系统文件", "系統檔案", "System file");
            Add("def.kind.registry", "注册表", "登錄檔", "Registry");
            Add("def.kind.startup", "启动项", "啟動項", "Startup");
            Add("def.kind.suspicious", "可疑文件", "可疑檔案", "Suspicious file");
            Add("def.kind.config", "安全配置", "安全設定", "Security config");
            Add("def.kind.process", "进程", "處理程序", "Process");
            Add("def.sev.low", "低风险", "低風險", "Low");
            Add("def.sev.medium", "中风险", "中風險", "Medium");
            Add("def.sev.high", "高风险", "高風險", "High");
            Add("def.value.none", "（空）", "（空）", "(empty)");
            Add("def.threat.fileTamper", "系统文件被篡改", "系統檔案被篡改", "System file was tampered with");
            Add("def.detail.fileTamper", "{0} 的指纹与基线不一致：{1} → {2}", "{0} 的指紋與基準不一致：{1} → {2}",
                "Fingerprint of {0} no longer matches the baseline: {1} → {2}");
            Add("def.solution.fileTamper", "建议：先用系统文件检查器修复（管理员命令 sfc /scannow）；若仍异常，请从微软官方渠道修复系统组件，并保留日志用于排查。",
                "建議：先用系統檔案檢查器修復（管理員命令 sfc /scannow）；若仍異常，請從微軟官方管道修復系統元件，並保留記錄用於排查。",
                "Run sfc /scannow as administrator to repair the file. If it still looks wrong, repair system components from Microsoft, and keep the log for investigation.");
            Add("def.threat.fileMissing", "系统文件缺失", "系統檔案缺失", "System file is missing");
            Add("def.detail.fileMissing", "{0} 不存在，可能导致系统不稳定或被替换。", "{0} 不存在，可能導致系統不穩定或被取代。",
                "{0} does not exist; Windows may be unstable or the file was replaced.");
            Add("def.solution.fileMissing", "建议：运行 sfc /scannow 修复；若文件位于 System32，请勿从第三方网站下载同名文件。",
                "建議：執行 sfc /scannow 修復；若檔案位於 System32，請勿從第三方網站下載同名檔案。",
                "Run sfc /scannow to repair it. Never download a same-name file from third-party sites into System32.");
            Add("def.threat.regTamper", "关键注册表被修改", "關鍵登錄檔被修改", "Critical registry value changed");
            Add("def.threat.regTamperName", "关键注册表被修改：{0}", "關鍵登錄檔被修改：{0}", "Critical registry value changed: {0}");
            Add("def.detail.regTamper", "{0} 当前为 {1}，基线为 {2}", "{0} 目前為 {1}，基準為 {2}", "{0} is now {1}; baseline was {2}");
            Add("def.detail.regCurrent", "{0} 当前为 {1}", "{0} 目前為 {1}", "{0} is now {1}");
            Add("def.solution.reg", "建议：点击「立即修复」恢复基线值；若反复被篡改，请断开网络并运行完整杀毒扫描。",
                "建議：點擊「立即修復」還原基準值；若反覆被篡改，請斷開網路並執行完整防毒掃描。",
                "Click Fix now to restore the baseline value. If it keeps changing, disconnect from the network and run a full antivirus scan.");
            Add("def.threat.ifeo", "镜像劫持项：{0}", "映像劫持項：{0}", "Image hijack entry: {0}");
            Add("def.detail.ifeo", "{0} 的调试器被指向 {1}（位于 {2}），这是常见的劫持手法。",
                "{0} 的偵錯器被指向 {1}（位於 {2}），這是常見的劫持手法。",
                "The debugger of {0} points to {1} (under {2}); this is a common hijacking trick.");
            Add("def.solution.ifeo", "建议：立即删除 Debugger 值，并检查该路径对应的文件是否已被隔离。",
                "建議：立即刪除 Debugger 值，並檢查該路徑對應的檔案是否已被隔離。",
                "Remove the Debugger value now, and check whether the file at that path has been quarantined.");
            Add("def.threat.appinit", "AppInit_DLLs 被注入", "AppInit_DLLs 被注入", "AppInit_DLLs injection");
            Add("def.detail.appinit", "{0} 的值为 {1}，会在几乎所有进程启动时加载该 DLL。",
                "{0} 的值為 {1}，會在幾乎所有處理程序啟動時載入該 DLL。",
                "{0} is set to {1}; this DLL would be loaded into almost every process.");
            Add("def.threat.defender", "系统安全功能被关闭", "系統安全功能被關閉", "Windows security was disabled");
            Add("def.detail.defender", "{0} 当前为 {1}，恶意软件常以此关闭实时保护。", "{0} 目前為 {1}，惡意程式常以此關閉即時保護。",
                "{0} is now {1}; malware often disables real-time protection this way.");
            Add("def.solution.defender", "建议：点击「立即修复」移除该策略，重启后在 Windows 安全中心重新开启实时保护。",
                "建議：點擊「立即修復」移除該原則，重新啟動後在 Windows 安全性中心重新開啟即時保護。",
                "Click Fix now to remove the policy, then restart and re-enable real-time protection in Windows Security.");
            Add("def.threat.startup", "可疑启动项：{0}", "可疑啟動項：{0}", "Suspicious startup: {0}");
            Add("def.detail.startup", "{0} 指向 {1}", "{0} 指向 {1}", "{0} points to {1}");
            Add("def.threat.startupFile", "启动目录里的可执行文件：{0}", "啟動目錄裡的執行檔：{0}", "Executable in Startup folder: {0}");
            Add("def.detail.startupFile", "{0} 位于启动目录 {1}", "{0} 位於啟動目錄 {1}", "{0} sits in the Startup folder {1}");
            Add("def.solution.startup", "建议：点击「禁用启动项」；确认文件来源不可信后再隔离。若为自定义软件，请先向软件作者确认。",
                "建議：點擊「停用啟動項」；確認檔案來源不可信後再隔離。若為自訂軟體，請先向軟體作者確認。",
                "Disable the startup entry. Quarantine only after confirming the file is untrusted; for custom software, check with its vendor first.");
            Add("def.threat.file", "可疑可执行文件：{0}", "可疑執行檔：{0}", "Suspicious executable: {0}");
            Add("def.detail.file", "{0}（目录 {1}，判定：{2}）", "{0}（目錄 {1}，判定：{2}）", "{0} (folder {1}; reason: {2})");
            Add("def.file.hidden", "隐藏文件", "隱藏檔案", "hidden file");
            Add("def.file.double", "双扩展名伪装", "雙副檔名偽裝", "double extension");
            Add("def.file.fake", "伪装系统文件名", "偽裝系統檔名", "fake system name");
            Add("def.file.scr", "屏幕保护程序格式", "螢幕保護程式格式", "screensaver format");
            Add("def.solution.file", "建议：点击「隔离文件」移入隔离区；若是自己下载的文件，请从官网重新获取并先做病毒扫描。",
                "建議：點擊「隔離檔案」移入隔離區；若是自己下載的檔案，請從官網重新取得並先做病毒掃描。",
                "Quarantine the file. If you downloaded it yourself, re-download from the official site and scan it first.");
            Add("def.reason.tempPath", "从临时目录运行", "從暫存目錄執行", "runs from a temp folder");
            Add("def.reason.unsigned", "未签名", "未簽章", "unsigned");
            Add("def.reason.recent", "最近启动", "最近啟動", "started recently");
            Add("def.reason.fakeName", "伪装系统进程名", "偽裝系統處理程序名", "fake system process name");
            Add("def.reason.killed", "已终止", "已終止", "terminated");
            Add("def.detail.killed", "已终止可疑进程：{0}", "已終止可疑處理程序：{0}", "Suspicious processes terminated: {0}");
            Add("def.action.ok", "处置完成", "處置完成", "Done");
            Add("def.action.fail", "处置失败，可能需要管理员权限", "處置失敗，可能需要管理員權限",
                "Action failed; administrator rights may be required");
            Add("def.action.missing", "文件已不存在", "檔案已不存在", "The file no longer exists");
            Add("def.action.quarantined", "已移入隔离区", "已移入隔離區", "Moved to quarantine");
            Add("def.action.quarantineFail", "隔离失败：{0}", "隔離失敗：{0}", "Quarantine failed: {0}");
        }
    }
}
