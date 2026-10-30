using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CleanC
{
    /// <summary>
    /// 文件地图画布：Squarified treemap + 面包屑 + 悬停评价卡。
    /// 左键打开（延迟 220ms），双击钻入，右键交给上层弹菜单。
    /// </summary>
    public class FileMapView : Control, IThemed
    {
        private const int CrumbH = 34;
        private const int IntroMs = 1000;
        private MapNode _scanRoot;
        private MapNode _viewRoot;
        private readonly List<MapRect> _rects = new List<MapRect>();
        private readonly List<CrumbSeg> _crumbs = new List<CrumbSeg>();
        private int _hover = -1;
        private MapNode _hoverNode;
        private Point _mouse;
        private Point _lastMouse;
        private long _mouseStamp;
        private RectangleF _refreshRect;
        private RectangleF _backRect;
        private bool _hoverRefresh;
        private bool _hoverBack;
        private int _crumbHover = -2;
        private MapNode _openPending;
        private Timer _clickTimer;
        private readonly string _introKey;
        private int _introStart = -1;
        private int _frozenIntro = -1;
        private SolidBrush[,] _fills;
        private Pen[] _borders;
        private readonly string _hoverKey;
        private float _hoverAmt;

        public Action<MapNode> OpenRequested;
        public Action<MapNode> ZoomRequested;
        public Action<MapNode> RefreshRequested;
        public Action<MapNode> HoverChanged;
        public Action<MapNode, Point> MenuRequested;

        private class CrumbSeg
        {
            public MapNode Node;
            public RectangleF Rect;
        }

        public FileMapView()
        {
            _introKey = Ui.Key("mapIntro");
            _hoverKey = Ui.Key("mapHover");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
            BackColor = Theme.Bg;
            _clickTimer = new Timer();
            _clickTimer.Interval = 300;
            _clickTimer.Tick += delegate { FlushPendingOpen(); };
        }

        public MapNode ScanRoot
        {
            get { return _scanRoot; }
        }

        public MapNode ViewRoot
        {
            get { return _viewRoot; }
        }

        public MapNode HoverNode
        {
            get { return _hoverNode; }
        }

        public void SetMap(MapNode scanRoot)
        {
            _scanRoot = scanRoot;
            _viewRoot = scanRoot;
            _hover = -1;
            _hoverNode = null;
            _crumbHover = -2;
            BeginIntro();
            Rebuild();
        }

        public void SetViewRoot(MapNode node)
        {
            if (node == null || !node.IsDir || node == _viewRoot) return;
            _viewRoot = node;
            _hover = -1;
            _hoverNode = null;
            _crumbHover = -2;
            Rebuild();
        }

        public void BeginIntro()
        {
            _introStart = Environment.TickCount;
            Anim.Loop(_introKey, delegate(float ms)
            {
                Invalidate();
                if (ms > 1100f) Anim.Stop(_introKey);
            });
        }

        /// <summary>截图调试用：把入场动画冻结在指定毫秒，便于核对中途帧。</summary>
        public void DemoFreezeIntro(int ms)
        {
            _frozenIntro = Math.Max(0, ms);
            Invalidate();
        }

        public void RefreshView()
        {
            Rebuild();
            Invalidate();
        }

        /// <summary>截图调试用：不移动鼠标，直接把悬停卡固定显示在指定逻辑坐标。</summary>
        public void DemoHover(MapNode node, Point at)
        {
            if (node == null) return;
            _mouse = at;
            _hoverNode = node;
            _hoverAmt = 1f;
            _hover = -1;
            for (int i = 0; i < _rects.Count; i++)
            {
                if (_rects[i].Node == node)
                {
                    _hover = i;
                    break;
                }
            }
            Invalidate();
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Bg;
            DisposeBrushes();
            Rebuild();
            Invalidate();
        }

        public void GoUp()
        {
            if (_viewRoot != null && _viewRoot.Parent != null)
            {
                MapNode target = _viewRoot.Parent;
                SetViewRoot(target);
                if (ZoomRequested != null) ZoomRequested(target);
            }
        }

        // ---------------------------------------------------------------- layout

        private float LW
        {
            get { return Width / Theme.Scale; }
        }

        private float LH
        {
            get { return Height / Theme.Scale; }
        }

        private RectangleF ContentRect
        {
            get
            {
                return new RectangleF(1f, CrumbH + 2f, LW - 2f, LH - CrumbH - 4f);
            }
        }

        private void Rebuild()
        {
            _rects.Clear();
            _crumbs.Clear();
            _hover = -1;
            _hoverNode = null;
            if (_viewRoot == null) return;
            RectangleF content = ContentRect;
            if (content.Width <= 4f || content.Height <= 4f) return;
            MapLayout.Build(_viewRoot, content, _rects);
            BuildCrumbs();
            ClampHover();
        }

        private void BuildCrumbs()
        {
            _crumbs.Clear();
            float crumbY = 5f;
            float crumbH = 24f;
            float x = 10f;
            _backRect = new RectangleF(x, crumbY, 24f, crumbH);
            if (_viewRoot != _scanRoot) x += 30f;

            List<MapNode> chain = new List<MapNode>();
            MapNode n = _viewRoot;
            while (n != null)
            {
                chain.Insert(0, n);
                n = n.Parent;
            }
            int start = 0;
            bool ellipsis = false;
            if (chain.Count > 3)
            {
                start = chain.Count - 2;
                ellipsis = true;
            }
            if (ellipsis)
            {
                {
                    Font f = Theme.Font(8.4f, FontStyle.Bold);
                    SizeF sz = TextRenderer.MeasureText("…", f);
                    RectangleF r = new RectangleF(x, crumbY, sz.Width + 2, crumbH);
                    CrumbSeg seg = new CrumbSeg();
                    seg.Node = chain[start - 1];
                    seg.Rect = r;
                    _crumbs.Add(seg);
                    x = r.Right + 4f;
                }
            }
            for (int i = start; i < chain.Count; i++)
            {
                string label = i == 0 ? chain[i].Name : chain[i].Name;
                Font f = Theme.Font(8.6f, i == chain.Count - 1 ? FontStyle.Bold : FontStyle.Regular);
                SizeF sz = TextRenderer.MeasureText(label, f);
                RectangleF r = new RectangleF(x, crumbY, sz.Width + 10f, crumbH);
                CrumbSeg seg = new CrumbSeg();
                seg.Node = chain[i];
                seg.Rect = r;
                _crumbs.Add(seg);
                x = r.Right + 2f;
            }

            _refreshRect = new RectangleF(LW - 72f, crumbY, 62f, crumbH);
        }

        private void ClampHover()
        {
            if (_rects.Count == 0)
            {
                _hover = -1;
                _hoverNode = null;
            }
        }

        private int HitTest(float mx, float my)
        {
            for (int i = _rects.Count - 1; i >= 0; i--)
            {
                if (_rects[i].Rect.Contains(mx, my)) return i;
            }
            return -1;
        }

        private MapNode NodeAt(float mx, float my)
        {
            int idx = HitTest(mx, my);
            return idx < 0 ? null : _rects[idx].Node;
        }

        // ---------------------------------------------------------------- input

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point lp = new Point((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
            _mouse = lp;
            int now = Environment.TickCount;
            if (unchecked(now - _mouseStamp) > 40)
            {
                _mouseStamp = now;
                _lastMouse = lp;
            }
            if (lp.Y < CrumbH + 2)
            {
                bool refreshHover = _refreshRect.Contains(lp.X, lp.Y);
                bool backHover = _viewRoot != _scanRoot && _backRect.Contains(lp.X, lp.Y);
                int crumb = -1;
                for (int i = 0; i < _crumbs.Count; i++)
                {
                    if (_crumbs[i].Rect.Contains(lp.X, lp.Y)) crumb = i;
                }
                bool changed = refreshHover != _hoverRefresh || backHover != _hoverBack || crumb != _crumbHover;
                _hoverRefresh = refreshHover;
                _hoverBack = backHover;
                _crumbHover = crumb;
                Cursor = (refreshHover || backHover || crumb >= 0) ? Cursors.Hand : Cursors.Default;
                if (changed || _hover >= 0) Invalidate();
                if (_hover >= 0)
                {
                    _hover = -1;
                    _hoverNode = null;
                }
                return;
            }

            int idx = HitTest(lp.X, lp.Y);
            if (idx != _hover)
            {
                _hover = idx;
                _hoverNode = idx < 0 ? null : _rects[idx].Node;
                Anim.Tween(_hoverKey, _hoverAmt, idx < 0 ? 0f : 1f, 160,
                    delegate(float v) { _hoverAmt = v; Invalidate(); }, null);
                Cursor = idx < 0 ? Cursors.Default : Cursors.Hand;
                if (HoverChanged != null) HoverChanged(_hoverNode);
            }
            else if (_hover >= 0)
            {
                int dx = Math.Abs(lp.X - _lastMouse.X);
                int dy = Math.Abs(lp.Y - _lastMouse.Y);
                if (dx + dy >= 6) Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            _hoverNode = null;
            _hoverRefresh = false;
            _hoverBack = false;
            _crumbHover = -2;
            Cursor = Cursors.Default;
            if (HoverChanged != null) HoverChanged(null);
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            Point lp = new Point((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));

            if (lp.Y < CrumbH + 2)
            {
                if (_refreshRect.Contains(lp.X, lp.Y))
                {
                    if (RefreshRequested != null) RefreshRequested(_scanRoot);
                    return;
                }
                if (_viewRoot != _scanRoot && _backRect.Contains(lp.X, lp.Y))
                {
                    GoUp();
                    return;
                }
                for (int i = 0; i < _crumbs.Count; i++)
                {
                    if (_crumbs[i].Rect.Contains(lp.X, lp.Y))
                    {
                        MapNode target = _crumbs[i].Node;
                        if (target != _viewRoot)
                        {
                            SetViewRoot(target);
                            if (ZoomRequested != null) ZoomRequested(target);
                        }
                        return;
                    }
                }
                return;
            }

            MapNode node = NodeAt(lp.X, lp.Y);
            if (node == null) return;
            _openPending = node;
            _clickTimer.Stop();
            _clickTimer.Start();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            _clickTimer.Stop();
            MapNode pending = _openPending;
            _openPending = null;
            Point lp = new Point((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
            if (lp.Y < CrumbH + 2) return;
            MapNode node = NodeAt(lp.X, lp.Y);
            if (node == null) node = pending;
            if (node == null) return;
            if (node.IsDir && node != _viewRoot)
            {
                SetViewRoot(node);
                if (ZoomRequested != null) ZoomRequested(node);
            }
            else if (!node.IsDir)
            {
                if (OpenRequested != null) OpenRequested(node);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Right) return;
            Point lp = new Point((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
            if (lp.Y < CrumbH + 2) return;
            MapNode node = NodeAt(lp.X, lp.Y);
            if (node == null) return;
            if (MenuRequested != null) MenuRequested(node, PointToScreen(new Point(e.X, e.Y)));
        }

        private void FlushPendingOpen()
        {
            _clickTimer.Stop();
            MapNode node = _openPending;
            _openPending = null;
            if (node == null) return;
            if (OpenRequested != null) OpenRequested(node);
        }

        // ---------------------------------------------------------------- paint

        private void EnsureBrushes()
        {
            if (_fills != null) return;
            _fills = new SolidBrush[4, MapLayout.MaxDepth + 1];
            for (int r = 0; r < 4; r++)
            {
                for (int d = 0; d <= MapLayout.MaxDepth; d++)
                {
                    _fills[r, d] = new SolidBrush(MapRisk.Fill((FileRisk)r, d));
                }
            }
            _borders = new Pen[4];
            for (int r = 0; r < 4; r++) _borders[r] = new Pen(MapRisk.Border((FileRisk)r), 1f);
        }

        private void DisposeBrushes()
        {
            if (_fills != null)
            {
                for (int r = 0; r < 4; r++)
                {
                    for (int d = 0; d <= MapLayout.MaxDepth; d++) _fills[r, d].Dispose();
                }
                _fills = null;
            }
            if (_borders != null)
            {
                for (int r = 0; r < 4; r++) _borders[r].Dispose();
                _borders = null;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.ScaleTransform(Theme.Scale, Theme.Scale);
            if (_viewRoot == null)
            {
                PaintCrumbs(g);
                Font f = Theme.Font(9f, FontStyle.Regular);
                RectangleF area = ContentRect;
                Theme.DrawText(g, Loc.T("map.empty"), f, Theme.Muted,
                    new RectangleF(area.X, area.Y + area.Height / 2f - 12, area.Width, 24),
                    StringAlignment.Center, StringAlignment.Center);
                return;
            }
            EnsureBrushes();
            PaintCrumbs(g);
            int elapsed = _frozenIntro >= 0 ? _frozenIntro
                : (_introStart >= 0 ? unchecked(Environment.TickCount - _introStart) : IntroMs);
            PaintRects(g, elapsed);
            if (_frozenIntro < 0 && elapsed >= IntroMs) _introStart = -1;
            if (_hoverNode != null && _hoverAmt > 0.05f) PaintTip(g, _hoverNode);
        }

        private void PaintCrumbs(Graphics g)
        {
            _refreshRect = new RectangleF(LW - 72f, 5f, 62f, 24f);
            RectangleF bar = new RectangleF(0, 0, LW, CrumbH);
            Theme.FillRounded(g, Theme.Surface, bar, 10f);
            Theme.StrokeRounded(g, Theme.Divider, bar, 10f, 1f);

            if (_viewRoot != _scanRoot)
            {
                if (_hoverBack) Theme.FillRounded(g, Theme.Hover, _backRect, 7f);
                using (Pen p = new Pen(_hoverBack ? Theme.Blue : Theme.SubText, 1.4f))
                {
                    float cx = _backRect.X + _backRect.Width / 2f;
                    float cy = _backRect.Y + _backRect.Height / 2f;
                    g.DrawLine(p, cx + 4, cy - 5, cx - 1, cy);
                    g.DrawLine(p, cx - 1, cy, cx + 4, cy + 5);
                    g.DrawLine(p, cx - 1, cy, cx + 8, cy);
                }
            }

            for (int i = 0; i < _crumbs.Count; i++)
            {
                RectangleF r = _crumbs[i].Rect;
                bool last = i == _crumbs.Count - 1;
                if (_crumbHover == i)
                {
                    Theme.FillRounded(g, Theme.Hover, new RectangleF(r.X - 1, r.Y + 2, r.Width + 2, r.Height - 4), 6f);
                }
                {
                    Font f = Theme.Font(8.6f, last ? FontStyle.Bold : FontStyle.Regular);
                    Theme.DrawText(g, _crumbs[i].Node.Name, f, last ? Theme.Text : Theme.SubText,
                        new RectangleF(r.X + 5, r.Y, r.Width - 6, r.Height), StringAlignment.Near,
                        StringAlignment.Center);
                }
                if (!last)
                {
                    using (SolidBrush b = new SolidBrush(Theme.Faint))
                    {
                        Font f = Theme.Font(7.5f, FontStyle.Regular);
                        StringFormat sf = new StringFormat();
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString("›", f, b, new RectangleF(r.Right - 2, r.Y, 10, r.Height), sf);
                        sf.Dispose();
                    }
                }
            }

            Color accent = _hoverRefresh ? Theme.Blue : Theme.SubText;
            Theme.FillRounded(g, _hoverRefresh ? Theme.BlueSoft : Theme.Card, _refreshRect, 7f);
            Theme.StrokeRounded(g, _hoverRefresh ? Theme.Blue : Theme.Divider, _refreshRect, 7f, 1f);
            using (SolidBrush b = new SolidBrush(accent))
            using (StringFormat sf = new StringFormat())
            {
                Font gf = Theme.IconFont(9f);
                sf.Alignment = StringAlignment.Near;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("\uE72C", gf, b, new RectangleF(_refreshRect.X + 7, _refreshRect.Y, 14, _refreshRect.Height), sf);
                sf.Dispose();
            }
            {
                Font f = Theme.Font(8f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("map.refresh"), f, accent,
                    new RectangleF(_refreshRect.X + 21, _refreshRect.Y, _refreshRect.Width - 22, _refreshRect.Height),
                    StringAlignment.Near, StringAlignment.Center);
            }
        }

        private void PaintRects(Graphics g, int elapsed)
        {
            using (Pen hoverPen = new Pen(Theme.Mix(MapRisk.Base(_hoverNode == null ? FileRisk.Risky : _hoverNode.Risk),
                Color.White, 0.35f), 2f))
            {
                for (int i = 0; i < _rects.Count; i++)
                {
                    MapRect mr = _rects[i];
                    float local = LocalIntro(elapsed, mr.Depth, i);
                    RectangleF r = ScaledIn(mr.Rect, local);
                    if (r.Width < 1.5f || r.Height < 1.5f) continue;
                    int risk = (int)mr.Node.Risk;
                    int depth = mr.Depth <= MapLayout.MaxDepth ? mr.Depth : MapLayout.MaxDepth;
                    g.FillRectangle(_fills[risk, depth], r);
                    g.DrawRectangle(_borders[risk], r.X, r.Y, r.Width, r.Height);
                    if (i == _hover && local >= 0.95f)
                    {
                        g.DrawRectangle(hoverPen, r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f);
                    }
                }
            }

            float sweep = elapsed < IntroMs ? elapsed / (float)IntroMs : 1f;
            if (sweep < 1f) PaintSweep(g, ContentRect, sweep);

            // 标签：有子块的目录用顶部标题条，叶子块直接画在块内。标题条在布局时已从子块区域扣除。
            for (int i = 0; i < _rects.Count; i++)
            {
                MapRect mr = _rects[i];
                if (mr.Depth == 0) continue;
                float local = LocalIntro(elapsed, mr.Depth, i);
                if (local < 0.7f) continue;
                float textT = (local - 0.7f) / 0.3f;
                RectangleF r = mr.Rect;
                bool hasKids = mr.Node.IsDir && mr.Node.Children != null && mr.Node.Children.Count > 0;
                if (hasKids && mr.Header <= 0) continue;
                Color ink = MapRisk.Text(mr.Node.Risk);
                if (textT < 1f) ink = Theme.A(ink, (int)(255 * textT));
                if (mr.Header > 0)
                {
                    Font f = Theme.Font(8.2f, FontStyle.Bold);
                    Theme.DrawText(g, mr.Node.Name, f, ink,
                        new RectangleF(r.X + 6f, r.Y + 1f, r.Width - 12f, 15f), StringAlignment.Near,
                        StringAlignment.Center);
                    if (mr.Header >= 30)
                    {
                        Font sf = Theme.Font(7.4f, FontStyle.Regular);
                        Color c = mr.Node.Risk == FileRisk.Forbidden
                            ? Color.FromArgb(205, Color.White) : Theme.SubText;
                        if (textT < 1f) c = Theme.A(c, (int)(255 * textT));
                        Theme.DrawText(g, Theme.Bytes(mr.Node.Size), sf, c,
                            new RectangleF(r.X + 6f, r.Y + 16f, r.Width - 12f, 14f), StringAlignment.Near,
                            StringAlignment.Center);
                    }
                    using (Pen line = new Pen(mr.Node.Risk == FileRisk.Forbidden
                        ? Color.FromArgb(34, Color.White) : Theme.A(Theme.Text, 24), 1f))
                    {
                        g.DrawLine(line, r.X + 4f, r.Y + mr.Header - 1f, r.Right - 4f, r.Y + mr.Header - 1f);
                    }
                    continue;
                }
                if (r.Width < 52f || r.Height < 18f) continue;
                {
                    Font f = Theme.Font(8.2f, FontStyle.Bold);
                    Theme.DrawText(g, mr.Node.Name, f, ink,
                        new RectangleF(r.X + 5f, r.Y + 3f, r.Width - 11f, 15f), StringAlignment.Near,
                        StringAlignment.Center);
                }
                if (r.Height >= 36f && r.Width >= 74f)
                {
                    Font sf = Theme.Font(7.4f, FontStyle.Regular);
                    Color c = mr.Node.Risk == FileRisk.Forbidden
                        ? Color.FromArgb(215, Color.White) : Theme.SubText;
                    if (textT < 1f) c = Theme.A(c, (int)(255 * textT));
                    Theme.DrawText(g, Theme.Bytes(mr.Node.Size), sf, c,
                        new RectangleF(r.X + 5f, r.Y + 18f, r.Width - 10f, 15f), StringAlignment.Near,
                        StringAlignment.Center);
                }
            }
        }

        /// <summary>区块入场进度：按层级错峰，父块先落位、子块后展开。</summary>
        private static float LocalIntro(int elapsed, int depth, int index)
        {
            float delay = depth * 52f + (index % 4) * 12f;
            float t = (elapsed - delay) / 380f;
            if (t < 0f) return 0f;
            if (t > 1f) return 1f;
            return t;
        }

        private static RectangleF ScaledIn(RectangleF r, float local)
        {
            if (local >= 1f) return r;
            float sc = 0.55f + 0.45f * Anim.EaseOut(local);
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            return new RectangleF(cx - r.Width * sc / 2f, cy - r.Height * sc / 2f,
                r.Width * sc, r.Height * sc);
        }

        /// <summary>入场扫光：一道斜向高光从左扫到右，同时左边缘带一圈青色光晕。</summary>
        private void PaintSweep(Graphics g, RectangleF area, float t)
        {
            float p = Math.Min(1f, t * 1.25f);
            if (p <= 0f || p >= 1f) return;
            GraphicsState st = g.Save();
            g.SetClip(area);
            float bandW = 130f;
            float x = area.X - bandW + (area.Width + bandW * 2f) * Anim.EaseInOut(p);
            Color shine = Theme.Mix(Theme.Mix(Theme.Cyan, Color.White, 0.45f), Color.White, 0.2f);
            using (LinearGradientBrush b = new LinearGradientBrush(
                new RectangleF(x, area.Y, bandW, area.Height),
                Color.FromArgb(0, shine), Color.FromArgb(0, shine), 0f))
            {
                ColorBlend blend = new ColorBlend(3);
                blend.Colors = new Color[]
                {
                    Color.FromArgb(0, shine), Color.FromArgb(78, shine), Color.FromArgb(0, shine)
                };
                blend.Positions = new float[] { 0f, 0.5f, 1f };
                b.InterpolationColors = blend;
                g.FillRectangle(b, x, area.Y, bandW, area.Height);
            }
            int edgeA = (int)(110 * (1f - p));
            if (edgeA > 0)
            {
                using (LinearGradientBrush b2 = new LinearGradientBrush(
                    new RectangleF(area.X, area.Y, 44f, area.Height),
                    Color.FromArgb(edgeA, Theme.Cyan), Color.FromArgb(0, Theme.Cyan), 0f))
                {
                    g.FillRectangle(b2, area.X, area.Y, 44f, area.Height);
                }
            }
            g.Restore(st);
        }

        private void PaintTip(Graphics g, MapNode node)
        {
            float w = 322f;
            float h = 172f;
            float x = _mouse.X + 18f;
            float y = _mouse.Y + 18f;
            if (x + w > LW - 6f) x = _mouse.X - w - 14f;
            if (y + h > LH - 6f) y = _mouse.Y - h - 14f;
            if (x < 4f) x = 4f;
            if (y < CrumbH + 4f) y = CrumbH + 4f;
            RectangleF card = new RectangleF(x, y, w, h);
            Theme.Shadow(g, new RectangleF(card.X + 2, card.Y + 4, card.Width - 4, card.Height - 4), 14f, 30, 6f);
            Theme.FillRounded(g, Theme.Card, card, 14f);
            Theme.StrokeRounded(g, Theme.Border, card, 14f, 1f);

            Color baseC = MapRisk.Base(node.Risk);
            Theme.FillRounded(g, Theme.A(baseC, 18), new RectangleF(card.X + 1, card.Y + 10, 4, 22), 2f);

            string name = node.Name;
            {
                Font f = Theme.Font(10f, FontStyle.Bold);
                Theme.DrawText(g, name, f, Theme.Text,
                    new RectangleF(card.X + 14, card.Y + 10, w - 120, 22), StringAlignment.Near,
                    StringAlignment.Center);
            }
            string riskText = Loc.T(MapRisk.Key(node.Risk));
            Font chipFont = Theme.Font(8.5f, FontStyle.Regular);
            SizeF chipSize = TextRenderer.MeasureText(riskText, chipFont);
            float chipW = Math.Max(56f, chipSize.Width + 16f);
            Theme.Chip(g, riskText, baseC,
                new RectangleF(card.Right - chipW - 14, card.Y + 11, chipW, 20));

            {
                Font f = Theme.Font(7.8f, FontStyle.Regular);
                Theme.DrawText(g, node.Path, f, Theme.Muted,
                    new RectangleF(card.X + 14, card.Y + 33, w - 28, 15), StringAlignment.Near,
                    StringAlignment.Center);
            }

            long parentSize = node.Parent == null ? node.Size : node.Parent.Size;
            string share = parentSize > 0 ? ((node.Size * 100.0 / parentSize).ToString("0.0") + "%") : "—";
            {
                Font f = Theme.Font(8.4f, FontStyle.Bold);
                string line = Theme.Bytes(node.Size) + "  ·  " + Loc.F("map.tip.share", share) +
                    "  ·  " + Loc.F("map.tip.files", Theme.Count(node.FileCount));
                Theme.DrawText(g, line, f, Theme.Text,
                    new RectangleF(card.X + 14, card.Y + 50, w - 28, 17), StringAlignment.Near,
                    StringAlignment.Center);
            }

            {
                float ax = card.X + 14;
                float ay = card.Y + 73;
                string[] labels =
                {
                    Loc.T("map.legend.safe"), Loc.T("map.legend.caution"),
                    Loc.T("map.legend.risky"), Loc.T("map.legend.forbidden")
                };
                Font f = Theme.Font(7.4f, FontStyle.Regular);
                for (int i = 0; i < 4; i++)
                {
                    using (SolidBrush b = new SolidBrush(MapRisk.Base((FileRisk)i)))
                    {
                        g.FillEllipse(b, ax, ay + 6, 6, 6);
                    }
                    string v = Theme.Bytes(node.Agg[i]);
                    Font vf = Theme.Font(7.4f, FontStyle.Bold);
                    Theme.DrawText(g, labels[i] + " " + v, f, Theme.SubText,
                        new RectangleF(ax + 10, ay, 72, 16), StringAlignment.Near, StringAlignment.Center);
                    ax += 74f;
                }
            }

            string eval;
            if (node.Risk == FileRisk.Forbidden)
            {
                eval = Loc.T("map.eval.forbidden");
            }
            else if (node.CatId != null && node.CatId.Length > 0)
            {
                eval = Loc.CatDesc(node.CatId);
            }
            else
            {
                eval = Loc.T(MapRisk.EvalKey(node.Risk));
            }
            if (node.Risk == FileRisk.Risky && node.CatId == "other")
                eval = Loc.T("map.eval.risky");
            {
                Font f = Theme.Font(8f, FontStyle.Regular);
                DrawWrap(g, eval, f, Theme.SubText,
                    new RectangleF(card.X + 14, card.Y + 93, w - 28, 38), 2);
            }

            {
                Font f = Theme.Font(7.6f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("map.tip.hint"), f, Theme.Faint,
                    new RectangleF(card.X + 14, card.Bottom - 22, w - 28, 14), StringAlignment.Near,
                    StringAlignment.Center);
            }
        }

        private static void DrawWrap(Graphics g, string text, Font f, Color c, RectangleF r, int lines)
        {
            if (string.IsNullOrEmpty(text)) return;
            using (StringFormat sf = new StringFormat())
            {
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.Alignment = StringAlignment.Near;
                using (SolidBrush b = new SolidBrush(c))
                {
                    g.DrawString(text, f, b, r, sf);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Anim.Stop(_introKey);
                Anim.Stop(_hoverKey);
                if (_clickTimer != null) _clickTimer.Dispose();
                DisposeBrushes();
            }
            base.Dispose(disposing);
        }
    }
}
