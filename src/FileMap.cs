using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace CleanC
{
    /// <summary>文件地图的四级风险：绿 / 黄 / 红 / 黑（黑 = 绝对不可触碰）。</summary>
    public enum FileRisk
    {
        Safe = 0,
        Caution = 1,
        Risky = 2,
        Forbidden = 3
    }

    /// <summary>文件地图的一个区块：目录（体积为子树合计）或文件。</summary>
    public class MapNode
    {
        public string Name;
        public string Path;
        public bool IsDir;
        public long Size;
        public long FileCount;
        public FileRisk Risk = FileRisk.Risky;
        public string CatId = "other";
        /// <summary>归一化路径（小写、反斜杠），扫描时复用，避免逐文件重复转换。</summary>
        public string Norm = "";
        /// <summary>该目录的分类对象（文件分类直接复用，省掉逐文件查表）。</summary>
        public Category Cat;
        public MapNode Parent;
        public List<MapNode> Children;
        /// <summary>子树里四类风险各自的体积，下标 = FileRisk。</summary>
        public long[] Agg = new long[4];
        /// <summary>扫描节点数触顶，部分目录未展开。</summary>
        public bool Capped;
        /// <summary>根节点记录的受限目录数。</summary>
        public int Denied;

        public MapNode(string name, string path, bool isDir)
        {
            Name = name;
            Path = path;
            IsDir = isDir;
            if (isDir) Children = new List<MapNode>();
        }

        public MapNode Root
        {
            get
            {
                MapNode n = this;
                while (n.Parent != null) n = n.Parent;
                return n;
            }
        }

        /// <summary>距离扫描根（C:\）的层级，根为 0。</summary>
        public int Depth
        {
            get
            {
                int d = 0;
                MapNode n = Parent;
                while (n != null)
                {
                    d++;
                    n = n.Parent;
                }
                return d;
            }
        }
    }

    public class MapRect
    {
        public MapNode Node;
        public RectangleF Rect;
        public int Depth;
        /// <summary>父区块顶部为标题预留的高度；0 表示没有子块或太小。</summary>
        public int Header;
    }

    /// <summary>分类结果 → 四级风险色；同时给出区块填充 / 描边 / 文字颜色。</summary>
    public static class MapRisk
    {
        public static FileRisk Of(RuleEngine engine, string path, bool isDir, Category cat)
        {
            if (engine != null && engine.IsHardBlocked(path)) return FileRisk.Forbidden;
            if (cat == null) return FileRisk.Risky;
            if (cat.Id == "system_core" || cat.Id == "installer" || cat.Id == "pagefile")
                return FileRisk.Forbidden;
            if (cat.Risk == RiskLevel.Safe) return FileRisk.Safe;
            if (cat.Risk == RiskLevel.Caution) return FileRisk.Caution;
            return FileRisk.Risky;
        }

        /// <summary>文件的四级风险：目录命中黑名单时整棵子树为黑，其余按分类。</summary>
        public static FileRisk OfFile(Category cat, MapNode dir)
        {
            if (cat == null) return FileRisk.Risky;
            if (cat.Id == "system_core" || cat.Id == "installer" || cat.Id == "pagefile")
                return FileRisk.Forbidden;
            if (cat.Risk == RiskLevel.Safe) return FileRisk.Safe;
            if (cat.Risk == RiskLevel.Caution) return FileRisk.Caution;
            if (dir != null && dir.Risk == FileRisk.Forbidden) return FileRisk.Forbidden;
            return FileRisk.Risky;
        }

        public static Color Base(FileRisk r)
        {
            if (r == FileRisk.Safe) return Theme.Green;
            if (r == FileRisk.Caution) return Theme.Amber;
            if (r == FileRisk.Risky) return Theme.Red;
            return Theme.IsDark ? Color.FromArgb(8, 10, 14) : Color.FromArgb(38, 43, 52);
        }

        public static Color Text(FileRisk r)
        {
            return r == FileRisk.Forbidden ? Color.White : Theme.Text;
        }

        public static string Key(FileRisk r)
        {
            if (r == FileRisk.Safe) return "map.risk.safe";
            if (r == FileRisk.Caution) return "map.risk.caution";
            if (r == FileRisk.Risky) return "map.risk.risky";
            return "map.risk.forbidden";
        }

        public static string EvalKey(FileRisk r)
        {
            if (r == FileRisk.Safe) return "map.eval.safe";
            if (r == FileRisk.Caution) return "map.eval.caution";
            if (r == FileRisk.Risky) return "map.eval.risky";
            return "map.eval.forbidden";
        }

        /// <summary>区块填充色：越深越亮，形成嵌套层次；黑色禁区保持近黑的实色。</summary>
        public static Color Fill(FileRisk r, int depth)
        {
            if (r == FileRisk.Forbidden)
            {
                int step = depth > 5 ? 5 : depth;
                return Theme.IsDark
                    ? Color.FromArgb(6 + step * 5, 7 + step * 5, 10 + step * 6)
                    : Color.FromArgb(30 + step * 8, 34 + step * 8, 42 + step * 9);
            }
            float t = Theme.IsDark ? 0.62f : 0.52f;
            float boost = Math.Min(0.2f, depth * 0.045f);
            return Theme.Mix(Theme.Card, Base(r), Math.Min(1f, t + boost));
        }

        public static Color Border(FileRisk r)
        {
            if (r == FileRisk.Forbidden)
            {
                return Theme.IsDark ? Color.FromArgb(92, 102, 118) : Color.FromArgb(66, 74, 88);
            }
            return Theme.Mix(Base(r), Theme.Border, 0.1f);
        }
    }

    /// <summary>把 C:\ 目录树扫成一棵 MapNode 树；体积自底向上累加。</summary>
    public static class FileMapScanner
    {
        public const int MaxNodes = 400000;

        public static MapNode Scan(string rootPath, RuleEngine engine,
            Action<ScanProgress> onProgress, Func<bool> canceled)
        {
            string rootName = Path.GetFileName(rootPath);
            if (rootName.Length == 0) rootName = rootPath;
            MapNode root = new MapNode(rootName, rootPath, true);
            root.Norm = PathUtil.Norm(rootPath);
            Category rc = engine.ClassifyDirectory(rootPath);
            root.Cat = rc;
            root.CatId = rc == null ? "other" : rc.Id;
            root.Risk = MapRisk.Of(engine, rootPath, true, rc);

            List<MapNode> order = new List<MapNode>();
            List<MapNode> stack = new List<MapNode>();
            stack.Add(root);
            int start = Environment.TickCount;
            int lastReport = start;
            long files = 0;
            long bytes = 0;
            int denied = 0;
            bool capped = false;

            while (stack.Count > 0)
            {
                if (canceled != null && canceled()) return null;
                if (order.Count >= MaxNodes)
                {
                    capped = true;
                    break;
                }
                MapNode dir = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);

                FileSystemInfo[] infos = ListInfos(dir.Path, ref denied);
                for (int i = 0; i < infos.Length; i++)
                {
                    if (order.Count >= MaxNodes)
                    {
                        capped = true;
                        break;
                    }
                    FileSystemInfo info = infos[i];
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
                    if ((attrs & FileAttributes.ReparsePoint) != 0) continue;
                    string full = info.FullName;
                    if ((attrs & FileAttributes.Directory) != 0)
                    {
                        MapNode child = new MapNode(info.Name, full, true);
                        child.Norm = dir.Norm.EndsWith("\\", StringComparison.Ordinal)
                            ? dir.Norm + info.Name.ToLowerInvariant()
                            : dir.Norm + "\\" + info.Name.ToLowerInvariant();
                        Category dc = engine.ClassifyDirectory(full);
                        child.Cat = dc;
                        child.CatId = dc == null ? "other" : dc.Id;
                        child.Risk = MapRisk.Of(engine, full, true, dc);
                        child.Parent = dir;
                        dir.Children.Add(child);
                        order.Add(child);
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
                        size = 0;
                    }
                    MapNode leaf = new MapNode(info.Name, full, false);
                    leaf.Norm = dir.Norm;
                    Category fc;
                    if (dir.Parent == null)
                    {
                        fc = engine.ClassifyFileIn(dir.Norm, info.Name);
                    }
                    else if (dir.Cat != null && dir.Cat.FileFilter != null)
                    {
                        fc = MatchFilter(dir.Cat, info.Name, engine);
                    }
                    else
                    {
                        fc = dir.Cat;
                    }
                    leaf.Cat = fc;
                    leaf.CatId = fc == null ? "other" : fc.Id;
                    leaf.Risk = MapRisk.OfFile(fc, dir);
                    leaf.Size = size;
                    leaf.FileCount = 1;
                    leaf.Agg[(int)leaf.Risk] = size;
                    leaf.Parent = dir;
                    dir.Children.Add(leaf);
                    order.Add(leaf);
                    files++;
                    bytes += size;
                }

                int now = Environment.TickCount;
                if (onProgress != null && unchecked(now - lastReport) >= 100)
                {
                    lastReport = now;
                    ScanProgress p = new ScanProgress();
                    p.Files = files;
                    p.Bytes = bytes;
                    p.CurrentPath = dir.Path;
                    p.DeniedDirs = denied;
                    p.ElapsedMs = unchecked(now - start);
                    onProgress(p);
                }
            }

            // 逆序把子节点体积并入父节点：创建顺序保证后代一定排在祖先后面。
            for (int i = order.Count - 1; i >= 0; i--)
            {
                MapNode n = order[i];
                MapNode p = n.Parent;
                if (p == null) continue;
                p.Size += n.Size;
                p.FileCount += n.FileCount;
                p.Agg[0] += n.Agg[0];
                p.Agg[1] += n.Agg[1];
                p.Agg[2] += n.Agg[2];
                p.Agg[3] += n.Agg[3];
            }
            root.Capped = capped;
            root.Denied = denied;

            if (onProgress != null)
            {
                ScanProgress p = new ScanProgress();
                p.Files = files;
                p.Bytes = bytes;
                p.DeniedDirs = denied;
                p.ElapsedMs = unchecked(Environment.TickCount - start);
                p.CurrentPath = "";
                onProgress(p);
            }
            return root;
        }

        private static FileSystemInfo[] ListInfos(string path, ref int denied)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(path);
                List<FileSystemInfo> list = new List<FileSystemInfo>();
                foreach (FileSystemInfo f in di.EnumerateFileSystemInfos()) list.Add(f);
                return list.ToArray();
            }
            catch
            {
                denied++;
                return new FileSystemInfo[0];
            }
        }

        /// <summary>缩略图目录的文件过滤规则，与 RuleEngine.ClassifyFileIn 保持一致。</summary>
        private static Category MatchFilter(Category cat, string fileName, RuleEngine engine)
        {
            string n = fileName.ToLowerInvariant();
            if (n.StartsWith("thumbcache_", StringComparison.Ordinal) && n.EndsWith(".db", StringComparison.Ordinal)) return cat;
            if (n.StartsWith("iconcache_", StringComparison.Ordinal) && n.EndsWith(".db", StringComparison.Ordinal)) return cat;
            return engine.Get("other");
        }

        /// <summary>重新扫描某节点的子树并替换，同时把体积增量回传到祖先。</summary>
        public static bool RescanInto(MapNode node, RuleEngine engine,
            Action<ScanProgress> onProgress, Func<bool> canceled)
        {
            if (node == null || !node.IsDir) return false;
            MapNode fresh = Scan(node.Path, engine, onProgress, canceled);
            if (fresh == null) return false;

            long oldSize = node.Size;
            long oldCount = node.FileCount;
            long[] oldAgg = new long[4];
            Array.Copy(node.Agg, oldAgg, 4);

            node.Children.Clear();
            for (int i = 0; i < fresh.Children.Count; i++)
            {
                MapNode c = fresh.Children[i];
                c.Parent = node;
                node.Children.Add(c);
            }
            node.Size = fresh.Size;
            node.FileCount = fresh.FileCount;
            node.Agg = fresh.Agg;

            long dSize = node.Size - oldSize;
            long dCount = node.FileCount - oldCount;
            MapNode p = node.Parent;
            while (p != null)
            {
                p.Size += dSize;
                p.FileCount += dCount;
                for (int k = 0; k < 4; k++) p.Agg[k] += node.Agg[k] - oldAgg[k];
                p = p.Parent;
            }
            return true;
        }
    }

    public static class MapTree
    {
        /// <summary>删除 / 剪切成功后把节点从树上摘掉，并逐级回退体积。</summary>
        public static void RemoveAndPrune(MapNode node)
        {
            if (node == null) return;
            MapNode p = node.Parent;
            if (p != null && p.Children != null) p.Children.Remove(node);
            while (p != null)
            {
                p.Size -= node.Size;
                p.FileCount -= node.FileCount;
                for (int k = 0; k < 4; k++) p.Agg[k] -= node.Agg[k];
                p = p.Parent;
            }
            node.Parent = null;
        }

        /// <summary>清理垃圾后把空目录从树上摘掉（体积为 0，不影响统计）。</summary>
        public static void PruneEmpty(MapNode dir)
        {
            if (dir == null || !dir.IsDir || dir.Children == null) return;
            for (int i = dir.Children.Count - 1; i >= 0; i--)
            {
                MapNode c = dir.Children[i];
                if (c.IsDir) PruneEmpty(c);
                if (c.IsDir && c.Size == 0 && c.FileCount == 0)
                {
                    c.Parent = null;
                    dir.Children.RemoveAt(i);
                }
            }
        }

        public static MapNode Find(MapNode root, string path)
        {
            if (root == null || path == null) return null;
            string norm = PathUtil.Norm(path);
            List<MapNode> stack = new List<MapNode>();
            stack.Add(root);
            while (stack.Count > 0)
            {
                MapNode n = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                if (PathUtil.Norm(n.Path) == norm) return n;
                if (n.Children == null) continue;
                for (int i = 0; i < n.Children.Count; i++) stack.Add(n.Children[i]);
            }
            return null;
        }
    }

    /// <summary>Squarified treemap：按体积降序、按最差长宽比成行，面积正比于体积。</summary>
    public static class MapLayout
    {
        public const int MaxDepth = 8;
        private const float MinNestedSide = 46f;

        private class Cell
        {
            public MapNode Node;
            public RectangleF Rect;
        }

        public static void Build(MapNode root, RectangleF bounds, List<MapRect> output)
        {
            output.Clear();
            BuildNode(root, bounds, 0, output);
        }

        private static void BuildNode(MapNode node, RectangleF rect, int depth, List<MapRect> output)
        {
            List<MapNode> kids = null;
            if (node.IsDir && node.Children != null && node.Children.Count > 0 &&
                depth < MaxDepth && rect.Width >= MinNestedSide && rect.Height >= MinNestedSide)
            {
                kids = new List<MapNode>();
                for (int i = 0; i < node.Children.Count; i++)
                {
                    MapNode c = node.Children[i];
                    if (c.Size > 0) kids.Add(c);
                }
                if (kids.Count == 0) kids = null;
            }

            int header = 0;
            if (kids != null && depth > 0) header = HeaderFor(rect);

            MapRect item = new MapRect();
            item.Node = node;
            item.Rect = rect;
            item.Depth = depth;
            item.Header = header;
            output.Add(item);
            if (kids == null) return;

            kids.Sort(delegate(MapNode a, MapNode b)
            {
                if (b.Size > a.Size) return 1;
                if (b.Size < a.Size) return -1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            RectangleF inner = new RectangleF(rect.X + 1f, rect.Y + 1f + header,
                rect.Width - 2f, rect.Height - 2f - header);
            if (inner.Width <= 3f || inner.Height <= 3f) return;

            List<Cell> cells = Squarify(kids, inner);
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].Rect.Width < 2f || cells[i].Rect.Height < 2f) continue;
                BuildNode(cells[i].Node, cells[i].Rect, depth + 1, output);
            }
        }

        /// <summary>大区块留出标题条，父级名字不会被整片铺满的子块盖住。</summary>
        private static int HeaderFor(RectangleF rect)
        {
            if (rect.Width >= 96f && rect.Height >= 54f) return 30;
            if (rect.Width >= 80f && rect.Height >= 38f) return 18;
            return 0;
        }

        private static List<Cell> Squarify(List<MapNode> items, RectangleF rect)
        {
            List<Cell> result = new List<Cell>();
            if (items.Count == 0 || rect.Width <= 0 || rect.Height <= 0) return result;

            long totalSize = 0;
            for (int i = 0; i < items.Count; i++) totalSize += items[i].Size;
            if (totalSize <= 0) return result;

            double x = rect.X;
            double y = rect.Y;
            double w = rect.Width;
            double h = rect.Height;
            long remaining = totalSize;
            int index = 0;

            while (index < items.Count && w > 0.6 && h > 0.6)
            {
                double side = Math.Min(w, h);
                double scale = (w * h) / Math.Max(1.0, (double)remaining);
                int rowStart = index;
                List<double> rowAreas = new List<double>();
                List<int> rowIndexes = new List<int>();
                double rowArea = 0;
                double worst = double.MaxValue;
                int j = index;
                while (j < items.Count)
                {
                    double a = Math.Max(0.0001, items[j].Size * scale);
                    double newWorst = Worst(rowAreas, rowArea + a, side);
                    if (rowAreas.Count > 0 && newWorst > worst) break;
                    worst = newWorst;
                    rowAreas.Add(a);
                    rowIndexes.Add(j);
                    rowArea += a;
                    j++;
                }
                if (rowIndexes.Count == 0) break;

                long rowBytes = 0;
                for (int k = 0; k < rowIndexes.Count; k++) rowBytes += items[rowIndexes[k]].Size;

                double notch = rowArea / Math.Max(0.0001, side);
                if (notch <= 0.0001) break;
                double offset = 0;
                if (w >= h)
                {
                    if (notch > w) notch = w;
                    for (int k = 0; k < rowAreas.Count; k++)
                    {
                        double hh = rowAreas[k] / notch;
                        if (k == rowAreas.Count - 1) hh = Math.Max(0.0001, h - offset);
                        RectangleF r = new RectangleF((float)x, (float)(y + offset), (float)notch,
                            (float)Math.Max(0.0001, hh - 0.6));
                        AddCell(result, items[rowIndexes[k]], r);
                        offset += hh;
                    }
                    x += notch;
                    w -= notch;
                }
                else
                {
                    if (notch > h) notch = h;
                    for (int k = 0; k < rowAreas.Count; k++)
                    {
                        double ww = rowAreas[k] / notch;
                        if (k == rowAreas.Count - 1) ww = Math.Max(0.0001, w - offset);
                        RectangleF r = new RectangleF((float)(x + offset), (float)y,
                            (float)Math.Max(0.0001, ww - 0.6), (float)notch);
                        AddCell(result, items[rowIndexes[k]], r);
                        offset += ww;
                    }
                    y += notch;
                    h -= notch;
                }

                index = j;
                remaining -= rowBytes;
                if (remaining <= 0) break;
            }
            return result;
        }

        private static double Worst(List<double> areas, double candidateArea, double side)
        {
            if (candidateArea <= 0 || side <= 0) return double.MaxValue;
            double max = candidateArea;
            double min = candidateArea;
            for (int i = 0; i < areas.Count; i++)
            {
                if (areas[i] > max) max = areas[i];
                if (areas[i] < min) min = areas[i];
            }
            double s2 = candidateArea * candidateArea;
            double w2 = side * side;
            double a = w2 * max / s2;
            double b = s2 / (w2 * min);
            return Math.Max(a, b);
        }

        private static void AddCell(List<Cell> list, MapNode node, RectangleF rect)
        {
            Cell c = new Cell();
            c.Node = node;
            c.Rect = rect;
            list.Add(c);
        }
    }
}
