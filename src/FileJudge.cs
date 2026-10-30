using System;
using System.Collections.Generic;
using System.IO;

namespace CleanC
{
    /// <summary>
    /// 逐文件判断：这个文件到底是什么（缓存 / 临时 / 日志 / 转储 / 更新包 / 个人文档……），
    /// 并给出独立于分类的风险值。个人文档与媒体一律标记为受保护，清理时跳过。
    /// </summary>
    public static class FileJudge
    {
        public class Verdict
        {
            public string KindKey = "file.kind.other";
            public RiskLevel Risk = RiskLevel.Caution;
        }

        private static readonly HashSet<string> DocExt = MakeSet(
            "doc", "docx", "xls", "xlsx", "ppt", "pptx", "pdf", "rtf", "odt", "ods", "odp",
            "txt", "md", "csv", "jpg", "jpeg", "png", "gif", "bmp", "webp", "tif", "tiff",
            "heic", "svg", "psd", "ai", "sketch", "fig",
            "mp3", "wav", "flac", "m4a", "aac", "mp4", "mkv", "avi", "mov", "wmv", "flv",
            "zip", "rar", "7z", "tar", "gz", "iso");

        private static readonly HashSet<string> BinaryExt = MakeSet(
            "exe", "dll", "sys", "ocx", "drv", "cpl", "scr", "com", "bat", "cmd", "ps1",
            "vbs", "vbe", "js", "jse", "wsf", "wsh", "jar", "node", "msix", "appx");

        private static readonly HashSet<string> ConfigExt = MakeSet(
            "ini", "cfg", "conf", "json", "xml", "yaml", "yml", "toml", "dat", "db",
            "sqlite", "sqlite3", "mdb", "reg", "props", "plist", "lock", "prefs");

        private static readonly HashSet<string> InstallerExt = MakeSet("msi", "msp", "msu");

        private static readonly HashSet<string> ArchiveExt = MakeSet("cab", "zip", "7z", "rar", "tar", "gz", "exe");

        private static readonly HashSet<string> LogExt = MakeSet("log", "etl", "evtx", "wer");

        private static readonly string[] CacheSegs =
        {
            "\\cache\\", "\\cache2\\", "\\caches\\", "\\code cache\\", "\\gpucache\\",
            "\\shadercache\\", "\\shadercache2\\", "\\grshadercache\\", "\\dawncache\\",
            "\\d3dscache\\", "\\cachestorage\\"
        };

        private static HashSet<string> MakeSet(params string[] items)
        {
            HashSet<string> s = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < items.Length; i++) s.Add(items[i]);
            return s;
        }

        public static Verdict Judge(string fullPath, Category cat)
        {
            string name = Path.GetFileName(fullPath);
            string ext = Path.GetExtension(name);
            if (!string.IsNullOrEmpty(ext) && ext[0] == '.') ext = ext.Substring(1);
            string norm = PathUtil.Norm(fullPath);
            string nameLower = name.ToLowerInvariant();
            string kind = KindOf(norm, nameLower, ext);

            Verdict v = new Verdict();
            v.KindKey = kind;
            v.Risk = RiskFor(kind, cat);
            return v;
        }

        public static void Apply(FileEntry f, Category cat)
        {
            if (f == null) return;
            Verdict v = Judge(f.Path, cat);
            f.KindKey = v.KindKey;
            f.Risk = v.Risk;
        }

        public static RiskLevel RiskOf(string fullPath, Category cat)
        {
            return Judge(fullPath, cat).Risk;
        }

        /// <summary>把 file.kind.xxx 映射到本地化键，供文件模式悬停卡展示。</summary>
        public static string NameKey(string kind)
        {
            return "filekind.name." + Suffix(kind);
        }

        public static string DescKey(string kind)
        {
            return "filekind.desc." + Suffix(kind);
        }

        private static string Suffix(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return "other";
            if (kind.StartsWith("file.kind.", StringComparison.Ordinal)) return kind.Substring(10);
            return kind;
        }

        private static string KindOf(string norm, string nameLower, string ext)
        {
            if (norm.IndexOf(@"\$recycle.bin\", StringComparison.Ordinal) >= 0) return "file.kind.recycle";

            bool packagePath = norm.IndexOf(@"\package cache\", StringComparison.Ordinal) >= 0 ||
                norm.IndexOf(@"\windows\installer\", StringComparison.Ordinal) >= 0;
            if (packagePath && ArchiveExt.Contains(ext)) return "file.kind.installer";

            if (DocExt.Contains(ext)) return "file.kind.document";
            if (InstallerExt.Contains(ext)) return "file.kind.installer";

            if (nameLower == "memory.dmp" || nameLower.EndsWith(".dmp", StringComparison.Ordinal) ||
                nameLower.EndsWith(".mdmp", StringComparison.Ordinal) ||
                norm.IndexOf(@"\minidump\", StringComparison.Ordinal) >= 0 ||
                norm.IndexOf(@"\crashdumps\", StringComparison.Ordinal) >= 0)
                return "file.kind.dump";

            if (LogExt.Contains(ext)) return "file.kind.log";

            if (nameLower.StartsWith("thumbcache_", StringComparison.Ordinal) ||
                nameLower.StartsWith("iconcache_", StringComparison.Ordinal))
                return "file.kind.thumb";

            if (nameLower.StartsWith("fontcache", StringComparison.Ordinal) ||
                norm.IndexOf("\\fontcache", StringComparison.Ordinal) >= 0)
                return "file.kind.font";

            if (ext == "pf" && norm.IndexOf(@"\prefetch\", StringComparison.Ordinal) >= 0)
                return "file.kind.prefetch";

            if (norm.IndexOf(@"\softwaredistribution\download", StringComparison.Ordinal) >= 0 ||
                norm.IndexOf(@"\deliveryoptimization\", StringComparison.Ordinal) >= 0)
                return "file.kind.update";

            if (ContainsAny(norm, CacheSegs) || norm.EndsWith("\\cache", StringComparison.Ordinal))
                return "file.kind.cache";

            if (norm.IndexOf(@"\windows.old", StringComparison.Ordinal) >= 0 ||
                ext == "bak" || ext == "old" || ext == "orig")
                return "file.kind.backup";

            if (BinaryExt.Contains(ext)) return "file.kind.binary";
            if (ConfigExt.Contains(ext)) return "file.kind.config";

            if (ext == "tmp" || ext == "temp" || nameLower.StartsWith("~$", StringComparison.Ordinal) ||
                norm.IndexOf(@"\temp\", StringComparison.Ordinal) >= 0 ||
                norm.EndsWith("\\temp", StringComparison.Ordinal) ||
                norm.IndexOf(@"\tmp\", StringComparison.Ordinal) >= 0 ||
                norm.IndexOf(@"\systemtemp\", StringComparison.Ordinal) >= 0)
                return "file.kind.temp";

            return "file.kind.other";
        }

        private static bool ContainsAny(string norm, string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (norm.IndexOf(needles[i], StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        private static RiskLevel RiskFor(string kind, Category cat)
        {
            if (kind == "file.kind.document") return RiskLevel.Protected;
            if (cat == null) return RiskLevel.Info;
            if (cat.Risk == RiskLevel.Protected) return RiskLevel.Protected;
            if (cat.Risk == RiskLevel.Info) return RiskLevel.Info;
            if (cat.Risk == RiskLevel.Safe)
            {
                if (kind == "file.kind.cache" || kind == "file.kind.temp" || kind == "file.kind.log" ||
                    kind == "file.kind.dump" || kind == "file.kind.thumb" || kind == "file.kind.font" ||
                    kind == "file.kind.update" || kind == "file.kind.recycle" || kind == "file.kind.prefetch")
                    return RiskLevel.Safe;
                return RiskLevel.Caution;
            }
            return RiskLevel.Caution;
        }
    }
}
