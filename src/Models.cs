using System;
using System.Collections.Generic;
using System.Drawing;

namespace CleanC
{
    public enum RiskLevel
    {
        Safe = 0,
        Caution = 1,
        Protected = 2,
        Info = 3
    }

    public enum CleanMode
    {
        Delete,
        Recycle,
        EmptyRecycle
    }

    /// <summary>清理范围：低 / 中只做定向关键目录，高做全盘，极限做三轮全盘 + 注册表垃圾。</summary>
    public enum ScanLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Extreme = 3
    }

    public enum UninstallMode
    {
        Normal = 0,
        Force = 1
    }

    public class FileEntry
    {
        public string Path;
        public long Size;
        public string Label;
        public string Note;
        /// <summary>这到底是什么文件：缓存 / 临时 / 日志 / 转储 / 个人文档……（本地化 key）。</summary>
        public string KindKey = "";
        /// <summary>逐个文件的独立风险值。</summary>
        public RiskLevel Risk = RiskLevel.Info;

        public FileEntry(string path, long size)
        {
            Path = path;
            Size = size;
        }

        public FileEntry(string path, long size, string label, string note)
        {
            Path = path;
            Size = size;
            Label = label;
            Note = note;
        }
    }

    /// <summary>一条残留待删除项：ValueName 为空表示删除整个键。</summary>
    public enum RegKind
    {
        Uninstall = 0,
        AppPath = 1,
        Run = 2,
        MainKey = 3,
        Service = 4,
        Folder = 5,
        Shortcut = 6,
        Task = 7,
        Firewall = 8,
        ComClass = 9,
        ContextMenu = 10,
        ProgId = 11,
        OpenWith = 12,
        Installer = 13,
        Driver = 14,
        Device = 15,
        EventLog = 16,
        BrowserExt = 17,
        TempFile = 18
    }

    public class RegItem
    {
        public string Hive = "";
        public string KeyPath = "";
        public string ValueName = "";
        public string Label = "";
        public string Reason = "";
        public RiskLevel Risk;
        public bool Selected;
        public RegKind Kind;
        public bool NeedsReboot;
        /// <summary>文件类残留（目录 / 快捷方式 / 临时文件）的路径。</summary>
        public string FilePath = "";
        /// <summary>特殊动作的辅助信息：计划任务名、防火墙规则签名、驱动包 inf、筛选器名。</summary>
        public string Aux = "";
        /// <summary>对应 16 步清理流程的步骤号，用于列表排序。</summary>
        public int Step = 8;

        public bool DeleteKey
        {
            get { return string.IsNullOrEmpty(ValueName); }
        }

        public string FullKey
        {
            get
            {
                string h = Hive;
                if (h.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)) h = "HKLM";
                else if (h.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)) h = "HKCU";
                else if (h.Equals("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase)) h = "HKCR";
                return h + "\\" + KeyPath;
            }
        }
    }

    public class Category
    {
        public string Id;
        public string Name;
        public string Desc;
        public string Glyph;
        public Color Accent;
        public RiskLevel Risk;
        public CleanMode Mode;
        public bool NeedsAdmin;
        public bool DefaultSelected;
        public bool Present;
        public bool Selected;
        public bool Restricted;
        public bool Registry;
        public int Stage = 1;
        public int Busy;
        public long Bytes;
        public int FileCount;
        public List<string> Roots;
        public List<FileEntry> Files;
        public List<RegItem> RegItems;
        public string FileFilter;
        public int FileLimit;

        public Category(string id, string name, string desc, string glyph, RiskLevel risk,
            CleanMode mode, bool needsAdmin, bool defaultSelected)
        {
            Id = id;
            Name = name;
            Desc = desc;
            Glyph = glyph;
            Risk = risk;
            Mode = mode;
            NeedsAdmin = needsAdmin;
            DefaultSelected = defaultSelected;
            Roots = new List<string>();
            Files = new List<FileEntry>();
            RegItems = new List<RegItem>();
            FileLimit = 12000;
            Selected = defaultSelected;
        }

        public bool Actionable
        {
            get { return Risk == RiskLevel.Safe || Risk == RiskLevel.Caution; }
        }

        public void Reset()
        {
            Bytes = 0;
            FileCount = 0;
            Present = false;
            Restricted = false;
            Busy = 0;
            Files.Clear();
            RegItems.Clear();
            Selected = DefaultSelected;
        }
    }

    public class ScanProgress
    {
        public long Files;
        public long Bytes;
        public string CurrentPath;
        public int DeniedDirs;
        public int SkippedLinks;
        public int ElapsedMs;
        public int Pass = 1;
        public int Passes = 1;
        public string Stage;
    }

    public class ScanResult
    {
        public long TotalFiles;
        public long TotalBytes;
        public int DeniedDirs;
        public int SkippedLinks;
        public bool Canceled;
        public bool Truncated;
        public int ElapsedMs;
        public int Passes = 1;
        public int Restricted;
        public ScanLevel Level = ScanLevel.High;
        public List<Category> Categories;
    }

    public class CleanResult
    {
        public long FreedBytes;
        public long RecycledBytes;
        public int Deleted;
        public int Skipped;
        public bool Canceled;
        public int RegKeys;
        public int RegValues;
        public string BackupDir;
        public List<string> Failures;
        public string LogPath;

        public CleanResult()
        {
            Failures = new List<string>();
        }
    }

    public class CleanProgress
    {
        public long Processed;
        public long Expected;
        public long Freed;
        public long Recycled;
        public int Deleted;
        public int Skipped;
        public string CurrentPath;
        public int ElapsedMs;
        public string Stage;
    }

    /// <summary>已安装软件条目。</summary>
    public class ProgramEntry
    {
        public string Name = "";
        public string Publisher = "";
        public string Version = "";
        public string InstallDir = "";
        public string UninstallString = "";
        public string RegKey = "";
        public string KeyName = "";
        public string Glyph = "";
        public Color Accent;
        public long Bytes;
        public int FileCount;
        public bool Measured;
        public bool Selected;
        public bool FilesRemoved;
        public bool Protected;
        public string ProtectedReason = "";
        public bool MachineWide;

        public string SizeText
        {
            get
            {
                if (FilesRemoved) return "—";
                if (Bytes > 0) return Theme.Bytes(Bytes);
                return Measured ? Loc.T("uninstall.sizeUnknown") : "…";
            }
        }

        public string SubText
        {
            get
            {
                string v = Version.Length > 0 ? "v" + Version : "";
                string p = Publisher.Length > 0 ? Publisher : Loc.T("uninstall.noPublisher");
                if (v.Length > 0) return p + " · " + v;
                return p;
            }
        }
    }

    public class UninstallReport
    {
        public int FilesDeleted;
        public long BytesFreed;
        public int Failures;
        public int RegKeys;
        public int RegValues;
        public int Shortcuts;
        public int Processes;
        public bool Refused;
        public string RefuseReason = "";
        public bool Canceled;
        public string BackupDir = "";
        public List<string> Messages = new List<string>();

        public void Note(string m)
        {
            if (Messages.Count < 120) Messages.Add(m);
        }
    }

    public static class PathUtil
    {
        public static string Norm(string path)
        {
            if (path == null) return "";
            string p = path.Trim();
            if (p.Length == 0) return p;
            p = p.Replace('/', '\\');
            while (p.Length > 3 && p.EndsWith("\\")) p = p.Substring(0, p.Length - 1);
            return p.ToLowerInvariant();
        }

        public static bool Under(string normalizedPath, string normalizedRoot)
        {
            if (normalizedPath.Length == 0 || normalizedRoot.Length == 0) return false;
            if (normalizedPath == normalizedRoot) return true;
            return normalizedPath.StartsWith(normalizedRoot + "\\", StringComparison.Ordinal);
        }

        public static string Display(string path)
        {
            return path == null ? "" : path.Replace("\\\\?\\", "");
        }

        public static string Clean(string path)
        {
            if (path == null) return "";
            string p = Environment.ExpandEnvironmentVariables(path).Trim();
            if (p.StartsWith("\"") && p.EndsWith("\"") && p.Length > 1) p = p.Substring(1, p.Length - 2);
            p = p.Trim();
            while (p.Length > 3 && (p.EndsWith("\\") || p.EndsWith("/"))) p = p.Substring(0, p.Length - 1);
            return p;
        }
    }
}
