using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace CleanC
{
    public static class Ui
    {
        private static int _counter;

        public static string Key(string prefix)
        {
            _counter++;
            return prefix + "_" + _counter;
        }

        public static Color Alpha(Color c, int alpha)
        {
            return Theme.A(c, alpha);
        }
    }

    public class UiButton : Control, IThemed
    {
        public bool Primary = true;
        public bool Ghost = false;
        public bool Danger = false;
        public string Glyph = "";
        public float Radius = 13f;

        private readonly string _key;
        private float _hover;
        private bool _down;

        public UiButton()
        {
            _key = Ui.Key("btn");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Height = 46;
        }

        public void ApplyTheme()
        {
            BackColor = Parent != null ? Parent.BackColor : Theme.Bg;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Anim.Tween(_key, _hover, 1f, 140, delegate(float v) { _hover = v; Invalidate(); }, null);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _down = false;
            Anim.Tween(_key, _hover, 0f, 180, delegate(float v) { _hover = v; Invalidate(); }, null);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _down = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _down = false;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush bg = new SolidBrush(BackColor))
            {
                g.FillRectangle(bg, ClientRectangle);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.ScaleTransform(Theme.Scale, Theme.Scale);
            float w = Width / Theme.Scale;
            float h = Height / Theme.Scale;
            RectangleF r = new RectangleF(0.5f, 0.5f, w - 1f, h - 1f);
            bool enabled = Enabled;

            if (!enabled)
            {
                Theme.FillRounded(g, Theme.Disabled, r, Radius);
                DrawContent(g, Theme.DisabledText, r);
                return;
            }

            if (Primary)
            {
                Color a = Danger ? Theme.Mix(Theme.Red, Theme.Coral, _hover * 0.45f) : Theme.Mix(Theme.Blue, Theme.Indigo, _hover * 0.35f);
                Color b = Danger ? Theme.Mix(Theme.Rose, Theme.Amber, _hover * 0.3f) : Theme.Mix(Theme.Cyan, Theme.Violet, _hover * 0.5f);
                Theme.Shadow(g, new RectangleF(2, 3, w - 4, h - 5), Radius, 22, 4f, a);
                Theme.FillRoundedGradient(g, a, b, r, Radius);
                if (_down)
                {
                    using (SolidBrush ov = new SolidBrush(Color.FromArgb(28, 0, 0, 0)))
                    using (GraphicsPath p = Theme.RoundRect(r, Radius))
                    {
                        g.FillPath(ov, p);
                    }
                }
                DrawContent(g, Color.White, r);
            }
            else
            {
                Color tint = Danger ? Theme.Red : Theme.Blue;
                Color border = Theme.Mix(Theme.Border, Theme.Mix(tint, Theme.Violet, _hover * 0.6f),
                    0.45f + _hover * 0.55f);
                Color fill = Theme.Mix(Theme.Card, Danger ? Theme.DangerSoft : Theme.BlueSoft, _hover * 0.55f);
                Theme.FillRounded(g, fill, r, Radius);
                Theme.StrokeRounded(g, border, r, Radius, 1f);
                DrawContent(g, Theme.Mix(tint, Theme.Violet, _hover * 0.65f), r);
            }
        }

        private void DrawContent(Graphics g, Color color, RectangleF r)
        {
            {
                Font f = Theme.Font(10f, FontStyle.Bold);
                float iconW = 0;
                if (!string.IsNullOrEmpty(Glyph))
                {
                    iconW = 16f;
                }
                SizeF textSize = TextRenderer.MeasureText(Text, f);
                float total = textSize.Width + (iconW > 0 ? iconW + 7f : 0f);
                float x = r.X + (r.Width - total) / 2f;
                float shift = _down ? 1f : 0f;
                shift -= 1.4f * _hover;
                if (iconW > 0)
                {
                    using (SolidBrush b = new SolidBrush(color))
                    {
                        Font gf = Theme.IconFont(15f);
                        g.DrawString(Glyph, gf, b, x, r.Y + (r.Height - 17f) / 2f + shift);
                    }
                    x += iconW + 7f;
                }
                using (SolidBrush b = new SolidBrush(color))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(Text, f, b, new RectangleF(x, r.Y + shift, r.Right - x, r.Height), sf);
                }
            }
        }
    }

    /// <summary>分段选择器：页签、清理范围、卸载方式共用。</summary>
    public class Segmented : Control, IThemed
    {
        private string[] _items = new string[0];
        private int _index;
        private float _pill;
        private int _hover = -1;
        private readonly string _key;
        private readonly string _hoverKey;
        private readonly string _flashKey;
        private float _hoverAmt;
        private float _flash;
        private const int Gap = 3;

        public Action Changed;
        public bool Filled = true;
        public bool ShowTrack = true;
        public Color Accent = Theme.Blue;
        public Color[] ItemColors;
        public float Radius = 10f;
        public float FontSize = 8.8f;
        public int Pad = 3;

        public Segmented()
        {
            _key = Ui.Key("seg");
            _hoverKey = Ui.Key("segHover");
            _flashKey = Ui.Key("segFlash");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Cursor = Cursors.Hand;
        }

        /// <summary>切换时给选中胶囊一次高亮闪动（模式开关专用特效）。</summary>
        public void Flash()
        {
            Anim.Tween(_flashKey, 1f, 0f, 520,
                delegate(float v) { _flash = v; Invalidate(); }, null);
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Bg;
            Invalidate();
        }

        public int SelectedIndex
        {
            get { return _index; }
        }

        public void SetItems(string[] items, int index)
        {
            _items = items == null ? new string[0] : items;
            if (index < 0) index = 0;
            if (index >= _items.Length) index = Math.Max(0, _items.Length - 1);
            _index = index;
            _pill = index;
            Invalidate();
        }

        public void SetSelected(int index, bool animate)
        {
            if (_items.Length == 0) return;
            if (index < 0) index = 0;
            if (index >= _items.Length) index = _items.Length - 1;
            if (index == _index)
            {
                Invalidate();
                return;
            }
            _index = index;
            if (animate)
            {
                Anim.Tween(_key, _pill, index, 260, delegate(float v) { _pill = v; Invalidate(); }, null);
            }
            else
            {
                _pill = index;
                Invalidate();
            }
        }

        public void SetAccent(Color c)
        {
            Accent = c;
            Invalidate();
        }

        private float LW
        {
            get { return Width / Theme.Scale; }
        }

        private float LH
        {
            get { return Height / Theme.Scale; }
        }

        private float SegWidth
        {
            get
            {
                if (_items.Length == 0) return 0f;
                return (LW - Pad * 2 - Gap * (_items.Length - 1)) / _items.Length;
            }
        }

        private RectangleF SegRect(int i)
        {
            float w = SegWidth;
            return new RectangleF(Pad + i * (w + Gap), Pad, w, LH - Pad * 2);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!Enabled)
            {
                if (_hover != -1)
                {
                    _hover = -1;
                    Cursor = Cursors.Default;
                    Invalidate();
                }
                return;
            }
            float x = e.X / Theme.Scale;
            float y = e.Y / Theme.Scale;
            int idx = -1;
            for (int i = 0; i < _items.Length; i++)
            {
                if (SegRect(i).Contains(x, y))
                {
                    idx = i;
                    break;
                }
            }
            if (idx != _hover)
            {
                _hover = idx;
                Anim.Tween(_hoverKey, _hoverAmt, idx >= 0 ? 1f : 0f, 140,
                    delegate(float v) { _hoverAmt = v; Invalidate(); }, null);
                Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Anim.Tween(_hoverKey, _hoverAmt, 0f, 140, delegate(float v) { _hoverAmt = v; Invalidate(); }, null);
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!Enabled) return;
            float x = e.X / Theme.Scale;
            float y = e.Y / Theme.Scale;
            for (int i = 0; i < _items.Length; i++)
            {
                if (!SegRect(i).Contains(x, y)) continue;
                if (i == _index) return;
                SetSelected(i, true);
                if (Changed != null) Changed();
                return;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush bg = new SolidBrush(BackColor))
            {
                g.FillRectangle(bg, ClientRectangle);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.ScaleTransform(Theme.Scale, Theme.Scale);
            if (_items.Length == 0) return;

            RectangleF all = new RectangleF(0.5f, 0.5f, LW - 1f, LH - 1f);
            if (ShowTrack)
            {
                Theme.FillRounded(g, Theme.Sunken, all, Radius);
                Theme.StrokeRounded(g, Theme.Divider, all, Radius, 1f);
            }

            Color sel = Accent;
            if (ItemColors != null && _index < ItemColors.Length) sel = ItemColors[_index];
            float flash = _flash;

            float segW = SegWidth;
            float pillX = Pad + _pill * (segW + Gap);
            RectangleF pill = new RectangleF(pillX, Pad, segW, LH - Pad * 2);
            if (Filled)
            {
                Theme.Shadow(g, new RectangleF(pill.X + 1, pill.Y + 2, pill.Width - 2, pill.Height - 2),
                    Radius - 2f, 18, 2.4f, sel);
                Color flashSel = flash > 0.01f ? Theme.Mix(sel, Color.White, 0.55f * flash) : sel;
                Theme.FillRoundedGradient(g, flashSel, Theme.Mix(flashSel, Color.White, 0.28f), pill, Radius - 2f);
            }
            else
            {
                Theme.Shadow(g, new RectangleF(pill.X + 1, pill.Y + 2, pill.Width - 2, pill.Height - 2),
                    Radius - 2f, 16, 2.2f, Theme.Mix(Theme.Blue, Theme.Border, 0.4f));
                Theme.FillRounded(g, Theme.Card, pill, Radius - 2f);
                Theme.StrokeRounded(g, Theme.Divider, pill, Radius - 2f, 1f);
            }
            if (flash > 0.01f)
            {
                Theme.StrokeRounded(g, Color.FromArgb((int)(170 * flash), Color.White),
                    new RectangleF(pill.X - 1f, pill.Y - 1f, pill.Width + 2f, pill.Height + 2f),
                    Radius, 1.6f);
            }

            if (_hover >= 0 && _hover != _index && _hoverAmt > 0.02f)
            {
                RectangleF hr = SegRect(_hover);
                Theme.FillRounded(g, Ui.Alpha(Theme.Mix(Theme.Card, Accent, 0.35f), (int)(140 * _hoverAmt)), hr, Radius - 3f);
            }

            {
                Font f = Theme.Font(FontSize, FontStyle.Bold);
                for (int i = 0; i < _items.Length; i++)
                {
                    RectangleF r = SegRect(i);
                    bool on = i == _index;
                    Color tc;
                    if (on) tc = Filled ? Color.White : sel;
                    else tc = Theme.Mix(Theme.SubText, Theme.Text, 0.25f);
                    if (!on && i == _hover) tc = Theme.Mix(tc, Accent, 0.45f * _hoverAmt);
                    Theme.DrawText(g, _items[i], f, tc, r, StringAlignment.Center, StringAlignment.Center);
                }
            }
        }
    }

    public class DonutChart : Control, IThemed
    {
        private readonly string _key;
        private readonly string _hoverKey;
        private float _value;
        private float _target;
        private float _hover;
        private long _used;
        private long _total;

        public DonutChart()
        {
            _key = Ui.Key("donut");
            _hoverKey = Ui.Key("donutHover");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel;
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Panel;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Anim.Tween(_hoverKey, _hover, 1f, 220, delegate(float v) { _hover = v; Invalidate(); }, null);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Anim.Tween(_hoverKey, _hover, 0f, 260, delegate(float v) { _hover = v; Invalidate(); }, null);
        }

        public void SetData(long used, long total, bool animate)
        {
            _used = used;
            _total = total;
            float target = total > 0 ? (float)((double)used / total) : 0f;
            target = Math.Max(0f, Math.Min(1f, target));
            if (!animate)
            {
                _value = target;
                _target = target;
                Invalidate();
                return;
            }
            _target = target;
            float from = _value;
            Anim.Tween(_key, from, target, 900, delegate(float v) { _value = v; Invalidate(); }, null);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.ScaleTransform(Theme.Scale, Theme.Scale);
            float lw = Width / Theme.Scale;
            float lh = Height / Theme.Scale;
            float lcx = lw / 2f;
            float lcy = lh / 2f;
            g.Clear(BackColor);
            float size = Math.Min(lw, lh);
            float cx = lcx;
            float cy = lcy;
            float ring = size * 0.085f + _hover * 2.2f;
            if (ring < 10f) ring = 10f;
            float radius = size / 2f - ring / 2f - 4f;
            RectangleF oval = new RectangleF(cx - radius, cy - radius, radius * 2, radius * 2);

            using (Pen track = new Pen(Theme.Track, ring))
            {
                g.DrawEllipse(track, oval);
            }

            float sweep = _value * 360f;
            if (sweep > 0.5f)
            {
                using (Pen glow = new Pen(Color.FromArgb((int)(24 + 18 * _hover), Theme.Violet), ring + 6f))
                {
                    g.DrawArc(glow, oval, -90f, sweep);
                }
                int steps = Math.Max(2, (int)(sweep / 5f));
                using (Pen pen = new Pen(Theme.Blue, ring))
                {
                    float step = sweep / steps;
                    for (int i = 0; i < steps; i++)
                    {
                        float a0 = -90f + step * i;
                        float t = i / (float)Math.Max(1, steps - 1);
                        pen.Color = t < 0.5f
                            ? Theme.Mix(Theme.Blue, Theme.Cyan, t * 2f)
                            : Theme.Mix(Theme.Cyan, Theme.Violet, (t - 0.5f) * 2f);
                        g.DrawArc(pen, oval, a0, step + 0.4f);
                    }
                }
                using (SolidBrush cap = new SolidBrush(Theme.Violet))
                {
                    double a = -90.0 + sweep;
                    float rad = (float)(Math.PI * a / 180.0);
                    g.FillEllipse(cap, cx + radius * (float)Math.Cos(rad) - ring / 2f,
                        cy + radius * (float)Math.Sin(rad) - ring / 2f, ring, ring);
                }
            }

            string pct = ((int)Math.Round(_value * 100)).ToString() + "%";
            {
                Font pf = Theme.Font(25f, FontStyle.Bold);
                Theme.DrawText(g, pct, pf, Theme.Text, new RectangleF(cx - 80, cy - 44, 160, 44),
                    StringAlignment.Center, StringAlignment.Center);
            }
            {
                Font sf = Theme.Font(8.75f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("left.used") + " " + Theme.Bytes(_used), sf, Theme.SubText,
                    new RectangleF(cx - 100, cy + 2, 200, 22), StringAlignment.Center, StringAlignment.Center);
                Theme.DrawText(g, Loc.T("left.total") + " " + Theme.Bytes(_total), sf, Theme.Muted,
                    new RectangleF(cx - 100, cy + 24, 200, 22), StringAlignment.Center, StringAlignment.Center);
            }
        }
    }

    public class CategoryList : Control, IThemed
    {
        private List<Category> _data = new List<Category>();
        private int _scroll;
        private int _scrollTarget;
        private int _hover = -1;
        private float _hoverLift;
        private int _introStart = -1;
        private readonly string _scrollKey;
        private readonly string _introKey;
        private readonly string _hoverKey;
        private readonly int RowH = 66;
        private readonly int Gap = 9;

        public Action SelectionChanged;
        public Action<Category> DetailsRequested;
        /// <summary>极限档：按安全清理顺序显示编号徽标。</summary>
        public bool ShowOrder;

        public CategoryList()
        {
            _scrollKey = Ui.Key("scroll");
            _introKey = Ui.Key("cards");
            _hoverKey = Ui.Key("cardHover");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Bg;
            Invalidate();
        }

        public void SetCategories(List<Category> cats)
        {
            _data = cats;
            _scroll = 0;
            _scrollTarget = 0;
            _hover = -1;
            _hoverLift = 0f;
            Anim.Stop(_hoverKey);
            _introStart = Environment.TickCount;
            Anim.Stop(_scrollKey);
            Anim.Loop(_introKey, delegate(float ms)
            {
                Invalidate();
                if (ms > 1000f) Anim.Stop(_introKey);
            });
            Invalidate();
        }

        public void RefreshView()
        {
            Invalidate();
        }

        /// <summary>调试/截图用：直接滚到底部。</summary>
        public void ScrollToBottom()
        {
            _scrollTarget = Math.Max(0, ContentHeight - (int)LH);
            _scroll = _scrollTarget;
            Invalidate();
        }

        private int ContentHeight
        {
            get { return _data.Count * (RowH + Gap) + 8; }
        }

        private float LW
        {
            get { return Width / Theme.Scale; }
        }

        private float LH
        {
            get { return Height / Theme.Scale; }
        }

        private void ClampScroll()
        {
            int max = Math.Max(0, ContentHeight - (int)LH);
            if (_scrollTarget > max) _scrollTarget = max;
            if (_scrollTarget < 0) _scrollTarget = 0;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (ContentHeight <= LH) return;
            _scrollTarget -= e.Delta;
            ClampScroll();
            int from = _scroll;
            Anim.Stop(_scrollKey);
            Anim.Tween(_scrollKey, from, _scrollTarget, 220, delegate(float v)
            {
                _scroll = (int)v;
                Invalidate();
            }, null);
            int idx = HitTest((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
            if (idx != _hover)
            {
                _hover = idx;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
            if (idx != _hover)
            {
                _hover = idx;
                _hoverLift = 0f;
                Anim.Stop(_hoverKey);
                Anim.Tween(_hoverKey, 0f, 1f, 180, delegate(float v) { _hoverLift = v; Invalidate(); }, null);
                Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != -1)
            {
                _hover = -1;
                _hoverLift = 0f;
                Anim.Stop(_hoverKey);
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int mx = (int)(e.X / Theme.Scale);
            int my = (int)(e.Y / Theme.Scale);
            int idx = HitTest(mx, my);
            if (idx < 0) return;
            Category c = _data[idx];
            RectangleF row = RowRect(idx);
            RectangleF check = CheckRect(row);
            if (idx == _hover) check.Y -= 4.5f * _hoverLift;
            bool emptyJunk = c.Registry && c.RegItems.Count == 0;
            if (c.Actionable && check.Contains(mx, my) && !emptyJunk)
            {
                c.Selected = !c.Selected;
                Invalidate();
                if (SelectionChanged != null) SelectionChanged();
            }
            else
            {
                if (DetailsRequested != null) DetailsRequested(c);
            }
        }

        private int HitTest(int x, int y)
        {
            for (int i = 0; i < _data.Count; i++)
            {
                RectangleF r = RowRect(i);
                if (r.Contains(x, y)) return i;
            }
            return -1;
        }

        private RectangleF RowRect(int i)
        {
            return new RectangleF(0, 4 + i * (RowH + Gap) - _scroll, LW - 8, RowH);
        }

        private RectangleF CheckRect(RectangleF row)
        {
            return new RectangleF(row.X + 18, row.Y + (row.Height - 20) / 2f, 20, 20);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.ScaleTransform(Theme.Scale, Theme.Scale);
            float lw = LW;
            float lh = LH;
            int now = Environment.TickCount;

            for (int i = 0; i < _data.Count; i++)
            {
                RectangleF r = RowRect(i);
                if (r.Bottom < -30) continue;
                if (r.Y > lh + 30) break;

                float alpha = 1f;
                float slide = 0f;
                if (_introStart >= 0)
                {
                    float t = Math.Min(1f, Math.Max(0f, (now - _introStart - i * 55) / 360f));
                    float eased = Anim.EaseOut(t);
                    alpha = eased;
                    slide = (1f - eased) * 14f;
                }
                RectangleF rr = new RectangleF(r.X, r.Y + slide, r.Width, r.Height);
                float hoverAmt = (i == _hover) ? _hoverLift : 0f;
                DrawRow(g, rr, _data[i], alpha, hoverAmt);
            }

            if (ContentHeight > lh)
            {
                int max = ContentHeight - (int)lh;
                float thumbH = Math.Max(36f, lh * (lh / (float)ContentHeight));
                float pos = max > 0 ? _scroll / (float)max : 0f;
                float y = (lh - thumbH) * pos;
                Theme.FillRounded(g, Color.FromArgb(24, Theme.Blue), new RectangleF(lw - 5, 4, 4, lh - 8), 2f);
                Theme.FillRounded(g, Color.FromArgb(120, Theme.Blue), new RectangleF(lw - 5, 4 + y, 4, thumbH), 2f);
            }
        }

        private void DrawRow(Graphics g, RectangleF row, Category c, float alpha, float hoverAmt)
        {
            int a = (int)(255 * alpha);
            if (a < 6) return;
            float lift = 4.5f * hoverAmt;
            RectangleF card = new RectangleF(row.X, row.Y - lift, row.Width, row.Height);
            float radius = 14f;
            Color accent = c.Accent;
            if (accent.A == 0) accent = Theme.Blue;

            Theme.Shadow(g, new RectangleF(card.X + 1, card.Y + 3, card.Width - 2, card.Height - 2), radius,
                (int)((10 + 10 * hoverAmt) * alpha), 3.2f + 1.2f * hoverAmt,
                Theme.Mix(Theme.ShadowTint, accent, 0.55f * hoverAmt));

            Color fill = Theme.Card;
            Color border = Theme.Border;
            if (c.Actionable && c.Selected)
            {
                fill = Theme.Mix(Theme.Card, accent, 0.075f);
                border = Theme.Mix(Theme.Border, accent, 0.55f);
            }
            if (hoverAmt > 0.01f)
            {
                fill = Theme.Mix(fill, Theme.Mix(Theme.Card, accent, 0.09f), hoverAmt * 0.85f);
                border = Theme.Mix(border, accent, hoverAmt * 0.7f);
            }
            Theme.FillRounded(g, Ui.Alpha(fill, a), card, radius);
            Theme.StrokeRounded(g, Ui.Alpha(border, a), card, radius, 1f);

            if (c.Actionable && c.Selected)
            {
                Theme.FillRounded(g, Ui.Alpha(accent, a), new RectangleF(card.X + 1, card.Y + 17, 3, 32), 1.5f);
            }

            RectangleF check = CheckRect(card);
            bool emptyJunk = c.Registry && c.RegItems.Count == 0;
            if (emptyJunk)
            {
                Theme.FillRounded(g, Ui.Alpha(Theme.Green, (int)(26 * alpha)),
                    new RectangleF(check.X, check.Y, 20, 20), 10f);
                using (Pen p = new Pen(Ui.Alpha(Theme.Green, a), 1.6f))
                {
                    g.DrawLine(p, check.X + 5, check.Y + 10.5f, check.X + 8.4f, check.Y + 14);
                    g.DrawLine(p, check.X + 8.4f, check.Y + 14, check.X + 15, check.Y + 6.5f);
                }
            }
            else if (c.Actionable)
            {
                if (c.Selected)
                {
                    Theme.FillRoundedGradient(g, Ui.Alpha(accent, a), Ui.Alpha(Theme.Mix(accent, Color.White, 0.3f), a),
                        new RectangleF(check.X, check.Y, 20, 20), 6.5f);
                    using (Pen p = new Pen(Ui.Alpha(Color.White, a), 1.7f))
                    {
                        g.DrawLine(p, check.X + 5, check.Y + 10.5f, check.X + 8.6f, check.Y + 14);
                        g.DrawLine(p, check.X + 8.6f, check.Y + 14, check.X + 15, check.Y + 6.2f);
                    }
                }
                else
                {
                    Theme.FillRounded(g, Ui.Alpha(Theme.Card, a), new RectangleF(check.X, check.Y, 20, 20), 6.5f);
                    Theme.StrokeRounded(g, Ui.Alpha(Theme.BorderStrong, a),
                        new RectangleF(check.X + 0.5f, check.Y + 0.5f, 19, 19), 6.5f, 1.2f);
                }
            }
            else
            {
                Theme.DrawLock(g, new RectangleF(check.X + 2, check.Y + 1, 15, 18), Ui.Alpha(Theme.Muted, a));
            }

            Color risk = Theme.RiskColor(c.Risk);
            RectangleF icon = new RectangleF(card.X + 54, card.Y + 14 - 2 * hoverAmt, 38, 38);
            Theme.FillRounded(g, Ui.Alpha(accent, (int)(18 * alpha)), icon, 12f);
            GraphicsState st = g.Save();
            g.TranslateTransform(icon.X + 19f, icon.Y + 19f);
            g.RotateTransform(-7f * hoverAmt);
            float iconScale = 1f + 0.09f * hoverAmt;
            g.ScaleTransform(iconScale, iconScale);
            using (SolidBrush b = new SolidBrush(Ui.Alpha(accent, a)))
            using (StringFormat sf = CenterFormat())
            {
                Font gf = Theme.Font(10.5f, FontStyle.Bold);
                g.DrawString(c.Glyph, gf, b, new RectangleF(-19, -19, 38, 38), sf);
            }
            g.Restore(st);

            if (ShowOrder && c.Stage > 0)
            {
                float br = 18f;
                RectangleF badge = new RectangleF(icon.X - 7, icon.Y - 7, br, br);
                Theme.FillRounded(g, Ui.Alpha(Theme.Card, a), badge, br / 2f);
                Theme.StrokeRounded(g, Ui.Alpha(accent, a), badge, br / 2f, 1.3f);
                {
                    Font bf = Theme.Font(7.4f, FontStyle.Bold);
                    Theme.DrawText(g, c.Stage.ToString(), bf, Ui.Alpha(accent, a), badge,
                        StringAlignment.Center, StringAlignment.Center);
                }
            }

            float textX = card.X + 106;
            float textW = card.Width - 106 - 150;
            {
                Font nf = Theme.Font(11f, FontStyle.Bold);
                Theme.DrawText(g, c.Name, nf, Ui.Alpha(Theme.Text, a),
                    new RectangleF(textX, card.Y + 12, textW, 22), StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font df = Theme.Font(8.5f, FontStyle.Regular);
                string desc;
                if (emptyJunk) desc = Loc.T("reg.none");
                else if (c.Registry && c.Restricted) desc = Loc.T("reg.needAdmin");
                else if (c.Restricted) desc = Loc.T("scan.needAdmin");
                else if (c.Busy > 0) desc = Loc.F("scan.appBusy", c.Busy);
                else desc = c.Desc;
                Theme.DrawText(g, desc, df, Ui.Alpha(emptyJunk ? Theme.Green : (c.Restricted ? Theme.Amber : Theme.SubText), a),
                    new RectangleF(textX, card.Y + 35, textW + 20, 22), StringAlignment.Near, StringAlignment.Center);
            }

            float chipW = emptyJunk ? 56f : ChipWidth(c.Risk);
            RectangleF chip = new RectangleF(card.Right - 20 - chipW, card.Y + 35, chipW, 21);
            Theme.Chip(g, emptyJunk ? Loc.T("reg.checked") : Theme.RiskText(c.Risk),
                emptyJunk ? Theme.Green : risk, chip);
            {
                Font sf = Theme.Font(10.5f, FontStyle.Bold);
                string value = c.Registry ? Loc.F("reg.itemCount", c.RegItems.Count) : Theme.Bytes(c.Bytes);
                Theme.DrawText(g, value, sf, Ui.Alpha(Theme.Text, a),
                    new RectangleF(card.Right - 160, card.Y + 12, 140, 20), StringAlignment.Far, StringAlignment.Center);
            }
        }

        private float ChipWidth(RiskLevel risk)
        {
            if (risk == RiskLevel.Safe) return 92f;
            if (risk == RiskLevel.Caution) return 72f;
            if (risk == RiskLevel.Protected) return 72f;
            return 48f;
        }

        private static StringFormat CenterFormat()
        {
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            return sf;
        }
    }

    /// <summary>已安装软件列表。</summary>
    public class ProgramList : Control, IThemed
    {
        private List<ProgramEntry> _data = new List<ProgramEntry>();
        private int _scroll;
        private int _scrollTarget;
        private int _hover = -1;
        private float _hoverLift;
        private int _introStart = -1;
        private readonly string _scrollKey;
        private readonly string _introKey;
        private readonly string _hoverKey;
        private readonly int RowH = 66;
        private readonly int Gap = 8;

        public Action SelectionChanged;
        public Action<ProgramEntry> DetailsRequested;

        public ProgramList()
        {
            _scrollKey = Ui.Key("pscroll");
            _introKey = Ui.Key("pcards");
            _hoverKey = Ui.Key("pcardHover");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Bg;
            Invalidate();
        }

        public void SetPrograms(List<ProgramEntry> items)
        {
            _data = items;
            _scroll = 0;
            _scrollTarget = 0;
            _hover = -1;
            _introStart = Environment.TickCount;
            Anim.Stop(_hoverKey);
            Anim.Stop(_scrollKey);
            Anim.Loop(_introKey, delegate(float ms)
            {
                Invalidate();
                if (ms > 900f) Anim.Stop(_introKey);
            });
            Invalidate();
        }

        public void RefreshView()
        {
            Invalidate();
        }

        private int ContentHeight
        {
            get { return _data.Count * (RowH + Gap) + 8; }
        }

        private float LW
        {
            get { return Width / Theme.Scale; }
        }

        private float LH
        {
            get { return Height / Theme.Scale; }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (ContentHeight <= LH) return;
            _scrollTarget -= e.Delta;
            int max = Math.Max(0, ContentHeight - (int)LH);
            if (_scrollTarget > max) _scrollTarget = max;
            if (_scrollTarget < 0) _scrollTarget = 0;
            Anim.Stop(_scrollKey);
            Anim.Tween(_scrollKey, _scroll, _scrollTarget, 220, delegate(float v)
            {
                _scroll = (int)v;
                Invalidate();
            }, null);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
            if (idx != _hover)
            {
                _hover = idx;
                _hoverLift = 0f;
                Anim.Stop(_hoverKey);
                Anim.Tween(_hoverKey, 0f, 1f, 170, delegate(float v) { _hoverLift = v; Invalidate(); }, null);
                Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            _hoverLift = 0f;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int mx = (int)(e.X / Theme.Scale);
            int my = (int)(e.Y / Theme.Scale);
            int idx = HitTest(mx, my);
            if (idx < 0) return;
            ProgramEntry p = _data[idx];
            RectangleF row = RowRect(idx);
            RectangleF check = CheckRect(row);
            if (idx == _hover) check.Y -= 4.5f * _hoverLift;
            if (check.Contains(mx, my) || p.Protected)
            {
                if (p.Protected) return;
                p.Selected = !p.Selected;
                Invalidate();
                if (SelectionChanged != null) SelectionChanged();
                return;
            }
            if (DetailsRequested != null) DetailsRequested(p);
        }

        private int HitTest(int x, int y)
        {
            for (int i = 0; i < _data.Count; i++)
            {
                if (RowRect(i).Contains(x, y)) return i;
            }
            return -1;
        }

        private RectangleF RowRect(int i)
        {
            return new RectangleF(0, 4 + i * (RowH + Gap) - _scroll, LW - 8, RowH);
        }

        private static RectangleF CheckRect(RectangleF row)
        {
            return new RectangleF(row.X + 18, row.Y + (row.Height - 20) / 2f, 20, 20);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.ScaleTransform(Theme.Scale, Theme.Scale);
            float lw = LW;
            float lh = LH;
            int now = Environment.TickCount;

            for (int i = 0; i < _data.Count; i++)
            {
                RectangleF r = RowRect(i);
                if (r.Bottom < -30) continue;
                if (r.Y > lh + 30) break;
                float alpha = 1f;
                float slide = 0f;
                if (_introStart >= 0)
                {
                    float t = Math.Min(1f, Math.Max(0f, (now - _introStart - i * 25) / 320f));
                    float eased = Anim.EaseOut(t);
                    alpha = eased;
                    slide = (1f - eased) * 12f;
                }
                DrawRow(g, new RectangleF(r.X, r.Y + slide, r.Width, r.Height), _data[i], alpha,
                    i == _hover ? _hoverLift : 0f);
            }

            if (ContentHeight > lh)
            {
                int max = ContentHeight - (int)lh;
                float thumbH = Math.Max(36f, lh * (lh / (float)ContentHeight));
                float pos = max > 0 ? _scroll / (float)max : 0f;
                float y = (lh - thumbH) * pos;
                Theme.FillRounded(g, Color.FromArgb(24, Theme.Blue), new RectangleF(lw - 5, 4, 4, lh - 8), 2f);
                Theme.FillRounded(g, Color.FromArgb(120, Theme.Blue), new RectangleF(lw - 5, 4 + y, 4, thumbH), 2f);
            }
        }

        private void DrawRow(Graphics g, RectangleF row, ProgramEntry p, float alpha, float hoverAmt)
        {
            int a = (int)(255 * alpha);
            if (a < 6) return;
            float lift = 4.5f * hoverAmt;
            RectangleF card = new RectangleF(row.X, row.Y - lift, row.Width, row.Height);
            Color accent = p.Accent;
            if (accent.A == 0) accent = Theme.Blue;
            float radius = 13f;

            Theme.Shadow(g, new RectangleF(card.X + 1, card.Y + 3, card.Width - 2, card.Height - 2), radius,
                (int)((8 + 10 * hoverAmt) * alpha), 3f + 1.2f * hoverAmt,
                Theme.Mix(Theme.ShadowTint, accent, 0.5f * hoverAmt));

            Color fill = Theme.Card;
            Color border = Theme.Border;
            if (p.Selected)
            {
                fill = Theme.Mix(Theme.Card, accent, 0.07f);
                border = Theme.Mix(Theme.Border, accent, 0.5f);
            }
            if (hoverAmt > 0.01f)
            {
                fill = Theme.Mix(fill, Theme.Mix(Theme.Card, accent, 0.08f), hoverAmt * 0.85f);
                border = Theme.Mix(border, accent, hoverAmt * 0.65f);
            }
            Theme.FillRounded(g, Ui.Alpha(fill, a), card, radius);
            Theme.StrokeRounded(g, Ui.Alpha(border, a), card, radius, 1f);
            if (p.Selected)
            {
                Theme.FillRounded(g, Ui.Alpha(accent, a), new RectangleF(card.X + 1, card.Y + 15, 3, 32), 1.5f);
            }

            RectangleF check = CheckRect(card);
            if (p.Protected)
            {
                Theme.DrawLock(g, new RectangleF(check.X + 2, check.Y + 1, 15, 18), Ui.Alpha(Theme.Muted, a));
            }
            else if (p.Selected)
            {
                Theme.FillRoundedGradient(g, Ui.Alpha(accent, a), Ui.Alpha(Theme.Mix(accent, Color.White, 0.3f), a),
                    new RectangleF(check.X, check.Y, 20, 20), 6.5f);
                using (Pen pen = new Pen(Ui.Alpha(Color.White, a), 1.7f))
                {
                    g.DrawLine(pen, check.X + 5, check.Y + 10.5f, check.X + 8.6f, check.Y + 14);
                    g.DrawLine(pen, check.X + 8.6f, check.Y + 14, check.X + 15, check.Y + 6.2f);
                }
            }
            else
            {
                Theme.FillRounded(g, Ui.Alpha(Theme.Card, a), new RectangleF(check.X, check.Y, 20, 20), 6.5f);
                Theme.StrokeRounded(g, Ui.Alpha(Theme.BorderStrong, a),
                    new RectangleF(check.X + 0.5f, check.Y + 0.5f, 19, 19), 6.5f, 1.2f);
            }

            RectangleF icon = new RectangleF(card.X + 52, card.Y + 14 - 2 * hoverAmt, 38, 38);
            Theme.FillRounded(g, Ui.Alpha(accent, (int)(16 * alpha)), icon, 12f);
            GraphicsState st = g.Save();
            g.TranslateTransform(icon.X + 19f, icon.Y + 19f);
            g.RotateTransform(-6f * hoverAmt);
            float sc = 1f + 0.08f * hoverAmt;
            g.ScaleTransform(sc, sc);
            using (SolidBrush b = new SolidBrush(Ui.Alpha(accent, a)))
            using (StringFormat sf = new StringFormat())
            {
                Font gf = Theme.Font(12.5f, FontStyle.Bold);
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(p.Glyph, gf, b, new RectangleF(-19, -19, 38, 38), sf);
            }
            g.Restore(st);

            float textX = card.X + 102;
            float textW = card.Width - 102 - 200;
            {
                Font nf = Theme.Font(10.2f, FontStyle.Bold);
                Theme.DrawText(g, p.Name, nf, Ui.Alpha(p.Protected ? Theme.Mix(Theme.Text, Theme.Muted, 0.3f) : Theme.Text, a),
                    new RectangleF(textX, card.Y + 9, textW, 20), StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font sf = Theme.Font(7.9f, FontStyle.Regular);
                Theme.DrawText(g, p.SubText, sf, Ui.Alpha(Theme.SubText, a),
                    new RectangleF(textX, card.Y + 29, textW, 16), StringAlignment.Near, StringAlignment.Center);
                string path = p.InstallDir.Length > 0 ? p.InstallDir : Loc.T("uninstall.noPath");
                Theme.DrawText(g, path, sf, Ui.Alpha(Theme.Muted, a),
                    new RectangleF(textX, card.Y + 46, textW, 16), StringAlignment.Near, StringAlignment.Center);
            }

            {
                Font vf = Theme.Font(9.6f, FontStyle.Bold);
                Theme.DrawText(g, p.SizeText, vf, Ui.Alpha(Theme.SubText, a),
                    new RectangleF(card.Right - 186, card.Y + 9, 166, 20), StringAlignment.Far, StringAlignment.Center);
            }
            if (p.Protected)
            {
                float cw = 62f;
                Theme.Chip(g, Loc.T("uninstall.protectedChip"), Theme.Gray,
                    new RectangleF(card.Right - 18 - cw, card.Y + 30, cw, 20));
            }
            else if (p.FilesRemoved)
            {
                float cw = 74f;
                Theme.Chip(g, Loc.T("uninstall.removedChip"), Theme.Amber,
                    new RectangleF(card.Right - 18 - cw, card.Y + 30, cw, 20));
            }
            else
            {
                {
                    Font pf = Theme.Font(7.6f, FontStyle.Regular);
                    Theme.DrawText(g, p.MachineWide ? Loc.T("uninstall.allUsers") : Loc.T("uninstall.thisUser"), pf,
                        Ui.Alpha(Theme.Muted, a), new RectangleF(card.Right - 186, card.Y + 30, 166, 16),
                        StringAlignment.Far, StringAlignment.Center);
                }
            }
        }
    }

    /// <summary>注册表清理页的条目列表：勾选、类型、风险、键路径。</summary>
    public class RegList : Control, IThemed
    {
        private List<RegItem> _data = new List<RegItem>();
        private int _scroll;
        private int _scrollTarget;
        private int _hover = -1;
        private int _introStart = -1;
        private readonly string _scrollKey;
        private readonly string _introKey;
        private readonly int RowH = 62;
        private readonly int Gap = 8;

        public Action SelectionChanged;

        public RegList()
        {
            _scrollKey = Ui.Key("rscroll");
            _introKey = Ui.Key("rcards");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Bg;
            Invalidate();
        }

        public void SetItems(List<RegItem> items)
        {
            _data = items;
            _scroll = 0;
            _scrollTarget = 0;
            _hover = -1;
            _introStart = Environment.TickCount;
            Anim.Stop(_scrollKey);
            Anim.Loop(_introKey, delegate(float ms)
            {
                Invalidate();
                if (ms > 900f) Anim.Stop(_introKey);
            });
            Invalidate();
        }

        public void RefreshView()
        {
            Invalidate();
        }

        private int ContentHeight
        {
            get { return _data.Count * (RowH + Gap) + 8; }
        }

        private float LW { get { return Width / Theme.Scale; } }
        private float LH { get { return Height / Theme.Scale; } }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (ContentHeight <= LH) return;
            _scrollTarget -= e.Delta;
            int max = Math.Max(0, ContentHeight - (int)LH);
            if (_scrollTarget > max) _scrollTarget = max;
            if (_scrollTarget < 0) _scrollTarget = 0;
            Anim.Stop(_scrollKey);
            Anim.Tween(_scrollKey, _scroll, _scrollTarget, 220, delegate(float v)
            {
                _scroll = (int)v;
                Invalidate();
            }, null);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
            if (idx != _hover)
            {
                _hover = idx;
                Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int mx = (int)(e.X / Theme.Scale);
            int my = (int)(e.Y / Theme.Scale);
            int idx = HitTest(mx, my);
            if (idx < 0) return;
            _data[idx].Selected = !_data[idx].Selected;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged();
        }

        private int HitTest(int x, int y)
        {
            for (int i = 0; i < _data.Count; i++)
            {
                if (RowRect(i).Contains(x, y)) return i;
            }
            return -1;
        }

        private RectangleF RowRect(int i)
        {
            return new RectangleF(0, 4 + i * (RowH + Gap) - _scroll, LW - 8, RowH);
        }

        private static string FirstPart(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int cut = s.IndexOf('\u0001');
            return cut > 0 ? s.Substring(0, cut) : s;
        }

        private static string KindText(RegKind k)
        {
            switch (k)
            {
                case RegKind.Uninstall: return Loc.T("reg.kind.uninstall");
                case RegKind.AppPath: return Loc.T("reg.kind.apppath");
                case RegKind.Run: return Loc.T("reg.kind.run");
                case RegKind.MainKey: return Loc.T("reg.kind.main");
                case RegKind.Folder: return Loc.T("reg.kind.folder");
                case RegKind.Shortcut: return Loc.T("reg.kind.shortcut");
                case RegKind.Task: return Loc.T("reg.kind.task");
                case RegKind.Firewall: return Loc.T("reg.kind.firewall");
                case RegKind.ComClass: return Loc.T("reg.kind.com");
                case RegKind.ContextMenu: return Loc.T("reg.kind.menu");
                case RegKind.ProgId: return Loc.T("reg.kind.progid");
                case RegKind.OpenWith: return Loc.T("reg.kind.openwith");
                case RegKind.Installer: return Loc.T("reg.kind.installer");
                case RegKind.Driver: return Loc.T("reg.kind.driver");
                case RegKind.Device: return Loc.T("reg.kind.device");
                case RegKind.EventLog: return Loc.T("reg.kind.eventlog");
                case RegKind.BrowserExt: return Loc.T("reg.kind.browser");
                case RegKind.TempFile: return Loc.T("reg.kind.temp");
                default: return Loc.T("reg.kind.service");
            }
        }

        private static Color KindColor(RegKind k)
        {
            switch (k)
            {
                case RegKind.Uninstall: return Theme.Amber;
                case RegKind.AppPath: return Theme.Blue;
                case RegKind.Run: return Theme.Violet;
                case RegKind.MainKey: return Theme.Rose;
                case RegKind.Folder: return Theme.Green;
                case RegKind.Shortcut: return Theme.Blue;
                case RegKind.Task: return Theme.Violet;
                case RegKind.Firewall: return Theme.Rose;
                case RegKind.ComClass: return Theme.Cyan;
                case RegKind.ContextMenu: return Theme.Teal;
                case RegKind.ProgId: return Theme.Teal;
                case RegKind.OpenWith: return Theme.Blue;
                case RegKind.Installer: return Theme.Amber;
                case RegKind.Driver: return Theme.Gold;
                case RegKind.Device: return Theme.Violet;
                case RegKind.EventLog: return Theme.Muted;
                case RegKind.BrowserExt: return Theme.Cyan;
                case RegKind.TempFile: return Theme.Green;
                default: return Theme.Cyan;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.ScaleTransform(Theme.Scale, Theme.Scale);
            float lw = LW;
            float lh = LH;
            int now = Environment.TickCount;

            for (int i = 0; i < _data.Count; i++)
            {
                RectangleF r = RowRect(i);
                if (r.Bottom < -30) continue;
                if (r.Y > lh + 30) break;
                float alpha = 1f;
                float slide = 0f;
                if (_introStart >= 0)
                {
                    float t = Math.Min(1f, Math.Max(0f, (now - _introStart - i * 22) / 300f));
                    alpha = Anim.EaseOut(t);
                    slide = (1f - alpha) * 10f;
                }
                DrawRow(g, new RectangleF(r.X, r.Y + slide, r.Width, r.Height), _data[i], alpha, i == _hover);
            }

            if (ContentHeight > lh + 4)
            {
                float trackH = lh - 12;
                float thumbH = Math.Max(36f, trackH * (lh / ContentHeight));
                float max = Math.Max(1, ContentHeight - (int)lh);
                float p = _scroll / max;
                RectangleF track = new RectangleF(lw - 5, 6, 3.5f, trackH);
                Theme.FillRounded(g, Theme.Track, track, 2f);
                RectangleF thumb = new RectangleF(lw - 5, 6 + (trackH - thumbH) * p, 3.5f, thumbH);
                Theme.FillRounded(g, Theme.Muted, thumb, 2f);
            }
        }

        private void DrawRow(Graphics g, RectangleF r, RegItem it, float alpha, bool hover)
        {
            int a = (int)(255 * alpha);
            Color accent = KindColor(it.Kind);
            float lift = hover ? 1.5f : 0f;
            RectangleF card = new RectangleF(r.X, r.Y - lift, r.Width, r.Height);
            bool sel = it.Selected;

            Theme.FillRounded(g, Ui.Alpha(sel ? Theme.BlueSoft : Theme.Card, a), card, 12f);
            Theme.StrokeRounded(g, Ui.Alpha(sel ? Theme.BorderStrong : Theme.Border, a), card, 12f, 1f);
            if (hover)
            {
                Theme.StrokeRounded(g, Ui.Alpha(Theme.BorderStrong, a), card, 12f, 1.4f);
            }
            using (SolidBrush bar = new SolidBrush(Ui.Alpha(accent, (int)(150 * alpha))))
            {
                g.FillRectangle(bar, card.X, card.Y + 14, 3.2f, card.Height - 28);
            }

            RectangleF check = new RectangleF(card.X + 18, card.Y + (card.Height - 20) / 2f, 20, 20);
            if (sel)
            {
                Theme.FillRoundedGradient(g, Theme.Blue, Theme.Cyan, check, 7f);
                using (Pen p = new Pen(Ui.Alpha(Color.White, a), 1.9f))
                {
                    g.DrawLine(p, check.X + 5, check.Y + 10.2f, check.X + 8.4f, check.Y + 13.6f);
                    g.DrawLine(p, check.X + 8.4f, check.Y + 13.6f, check.X + 15, check.Y + 6.4f);
                }
            }
            else
            {
                Theme.FillRounded(g, Ui.Alpha(Theme.Card, a), check, 7f);
                Theme.StrokeRounded(g, Ui.Alpha(Theme.BorderStrong, a), check, 7f, 1.4f);
            }

            RectangleF kind = new RectangleF(card.X + 48, card.Y + 10, 54, 20);
            Theme.Chip(g, KindText(it.Kind), accent, kind);

            float textX = card.X + 112;
            float textW = card.Width - 112 - 150;
            {
                Font nf = Theme.Font(9.9f, FontStyle.Bold);
                Theme.DrawText(g, it.Label, nf, Ui.Alpha(Theme.Text, a),
                    new RectangleF(textX, card.Y + 7, textW, 20), StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font df = Theme.Font(8.2f, FontStyle.Regular);
                Theme.DrawText(g, it.Reason, df, Ui.Alpha(Theme.SubText, a),
                    new RectangleF(textX, card.Y + 27, textW, 17), StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font kf = Theme.Font(7.6f, FontStyle.Regular);
                string key;
                if (it.Kind == RegKind.Folder || it.Kind == RegKind.Shortcut ||
                    it.Kind == RegKind.BrowserExt || it.Kind == RegKind.TempFile)
                {
                    key = it.FilePath;
                }
                else if (it.Kind == RegKind.Task)
                {
                    key = "计划任务\\" + it.Aux;
                }
                else if (it.Kind == RegKind.Firewall)
                {
                    key = "防火墙规则 · " + FirstPart(it.Aux);
                }
                else if (it.Kind == RegKind.Driver)
                {
                    key = it.ValueName.Length > 0
                        ? "筛选器 " + it.Aux
                        : "驱动包 " + it.Aux;
                }
                else
                {
                    key = it.Hive;
                    if (key.StartsWith("HKEY_LOCAL_MACHINE")) key = "HKLM";
                    else if (key.StartsWith("HKEY_CURRENT_USER")) key = "HKCU";
                    key += "\\" + it.KeyPath;
                    if (!it.DeleteKey) key += "  > " + it.ValueName;
                }
                Theme.DrawText(g, key, kf, Ui.Alpha(Theme.Muted, a),
                    new RectangleF(textX, card.Y + 44, card.Width - 112 - 24, 14), StringAlignment.Near, StringAlignment.Center);
            }

            if (it.NeedsReboot)
            {
                Theme.Chip(g, Loc.T("reg.needReboot"), Theme.Violet,
                    new RectangleF(card.Right - 20 - 56, card.Y + 8, 56, 19));
            }
            Color risk = it.Risk == RiskLevel.Safe ? Theme.Green : Theme.Amber;
            Theme.Chip(g, Theme.RiskText(it.Risk), risk,
                new RectangleF(card.Right - 20 - 62, card.Y + 30, 62, 19));
        }
    }

    /// <summary>
    /// 开机动画：C 从左边蹦蹦跳跳过来 → 跳上地面 → 一个纸箱落下盖住 C → 白色闪光中纸箱变成 logo 并放大展示。
    /// </summary>
    public class SplashOverlay : Control, IThemed
    {
        private readonly Action _onDone;
        private readonly string _key;
        private float _t;
        private bool _finished;

        private const float TEnd = 3100f;
        private const float HopEnd = 900f;
        private const float FallStart = 1060f;
        private const float FallEnd = 1400f;
        private const float Reveal = 1500f;
        private const float ZoomStart = 1700f;
        private const float ZoomEnd = 2120f;

        /// <summary>调试用时间缩放：小于 1 表示放慢，便于逐帧截图。</summary>
        public static float TimeScale = 1f;

        /// <summary>调试用：启动动画的真实帧数与绘制耗时。</summary>
        public static int FrameCount;
        public static long PaintTicks;
        public static int PlayStart;
        public static int PlayEnd;
        private string _titleCache = "";
        private float[] _titleWidths = new float[0];
        private float _titleTotal;
        private Bitmap _splashBg;
        private string _splashBgKey = "";
        public static long ProfBg;
        public static long ProfC;
        public static long ProfBox;
        public static long ProfFx;
        public static long ProfText;

        public SplashOverlay(Action onDone)
        {
            _onDone = onDone;
            _key = Ui.Key("splash");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Cursor = Cursors.Hand;
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Bg;
            if (_splashBg != null)
            {
                _splashBg.Dispose();
                _splashBg = null;
                _splashBgKey = "";
            }
            Invalidate();
        }

        public void Play()
        {
            _t = 0f;
            FrameCount = 0;
            PaintTicks = 0;
            ProfBg = ProfC = ProfBox = ProfFx = ProfText = 0;
            PlayStart = Environment.TickCount;
            PlayEnd = 0;
            Anim.Loop(_key, delegate(float ms)
            {
                _t = ms * (TimeScale > 0f ? TimeScale : 1f);
                Invalidate();
                if (_t >= TEnd) Finish();
            });
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            PlayEnd = Environment.TickCount;
            Anim.Stop(_key);
            Action done = _onDone;
            if (Parent != null)
            {
                Parent.Controls.Remove(this);
                Dispose();
            }
            if (done != null) done();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Finish();
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        private float Seg(float a, float b)
        {
            return Clamp01((_t - a) / (b - a));
        }

        /// <summary>
        /// 快速径向光晕：多层同心半透明圆叠加。GDI+ 的 PathGradient / 位图拉伸单次要 20–50ms，
        /// 启动动画每帧画 3 个，导致实际只有 8 帧左右。
        /// </summary>
        private static void RadialFast(Graphics g, float cx, float cy, float r, Color c, int alpha)
        {
            if (r <= 1f || alpha <= 0) return;
            int layers = alpha > 120 ? 20 : 24;
            for (int i = layers - 1; i >= 0; i--)
            {
                float t = (i + 1f) / layers;
                int a = (int)(alpha * (1f - (float)i / layers) / 2f);
                if (a < 2) continue;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(a, c)))
                {
                    float rr = r * t;
                    g.FillEllipse(b, cx - rr, cy - rr, rr * 2f, rr * 2f);
                }
            }
        }

        /// <summary>把背景光晕烘焙成不透明位图，每帧只做一次 SourceCopy（避免三层大椭圆反复抗锯齿）。</summary>
        private void PaintSplashBg(Graphics g)
        {
            string key = Width + "x" + Height + "@" + Theme.Scale + (Theme.IsDark ? "d" : "l");
            if (_splashBg == null || _splashBgKey != key)
            {
                if (_splashBg != null) _splashBg.Dispose();
                _splashBg = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                using (Graphics bg = Graphics.FromImage(_splashBg))
                {
                    bg.Clear(Theme.Bg);
                    bg.SmoothingMode = SmoothingMode.AntiAlias;
                    bg.ScaleTransform(Theme.Scale, Theme.Scale);
                    float lw = Width / Theme.Scale;
                    float lh = Height / Theme.Scale;
                    RadialFast(bg, lw / 2f, lh / 2f - 40, 300f, Theme.Blue, 22);
                    RadialFast(bg, lw / 2f - 170, lh / 2f - 130, 220f, Theme.Violet, 14);
                    RadialFast(bg, lw / 2f + 180, lh / 2f + 90, 230f, Theme.Cyan, 12);
                }
                _splashBgKey = key;
            }
            CompositingMode old = g.CompositingMode;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(_splashBg, new Rectangle(0, 0, Width, Height));
            g.CompositingMode = old;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            System.Diagnostics.Stopwatch paintSw = System.Diagnostics.Stopwatch.StartNew();
            FrameCount++;
            Graphics g = e.Graphics;
            PaintSplashBg(g);
            ProfBg += paintSw.ElapsedTicks;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.ScaleTransform(Theme.Scale, Theme.Scale);
            float lw = Width / Theme.Scale;
            float lh = Height / Theme.Scale;
            float cx = lw / 2f;
            float groundY = lh / 2f + 10f;

            float fade = Seg(2850f, 3100f);
            float zoomOut = 1f + 0.035f * fade;
            g.TranslateTransform(cx, lh / 2f);
            g.ScaleTransform(zoomOut, zoomOut);
            g.TranslateTransform(-cx, -lh / 2f);

            long mark = System.Diagnostics.Stopwatch.GetTimestamp();

            // ---- 阶段 1：C 蹦跳入场
            float hp = Seg(0f, HopEnd);
            float restFade = Seg(HopEnd, HopEnd + 220f);
            float cSize = 68f * (0.86f + 0.14f * Anim.EaseOut(hp));
            float cX = -80f + (cx + 80f) * Anim.EaseOut(hp);
            float lift = Math.Abs((float)Math.Sin(hp * 3f * Math.PI)) * 96f * (1f - hp * 0.45f);
            if (hp >= 1f)
            {
                lift = Math.Abs((float)Math.Sin((_t - HopEnd) / 320f * Math.PI)) * 5f * (1f - restFade);
            }
            float cY = groundY - cSize / 2f - lift;
            float ground = Math.Min(1f, lift / 30f);
            float squash = 1f;
            if (hp < 1f) squash = 1f - 0.15f * (1f - Math.Min(1f, lift / 26f));
            float rot = (float)Math.Sin(hp * 6.0 * Math.PI) * 7f * (1f - hp);

            // 地面阴影
            if (hp > 0.02f)
            {
                float sw = cSize * (0.92f - 0.22f * ground);
                int sa = (int)((46 - 26 * ground) * Math.Min(1f, hp * 3f));
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(Math.Max(0, sa), Theme.Blue)))
                {
                    g.FillEllipse(sh, cX - sw / 2f, groundY - 5f, sw, 10f);
                }
            }

            if (hp > 0f && _t < Reveal + 80f)
            {
                GraphicsState cs = g.Save();
                g.TranslateTransform(cX, cY);
                g.RotateTransform(rot);
                g.ScaleTransform(1f / squash, squash);
                Theme.DrawCLogo(g, new RectangleF(-cSize / 2f, -cSize / 2f, cSize, cSize), Theme.Blue);
                g.Restore(cs);
            }

            // ---- 阶段 2：纸箱落下
            ProfC += StopwatchTimestamp() - mark;
            mark = System.Diagnostics.Stopwatch.GetTimestamp();
            float bp = Seg(FallStart, FallEnd);
            float fallEase = bp * bp * (3f - 2f * bp);
            float tileSize = 104f;
            float impact = Seg(FallEnd, FallEnd + 150f);

            float zp = Anim.EaseOut(Seg(ZoomStart, ZoomEnd));
            float zoom = 1f + 0.32f * zp;
            float tileCy = (groundY - tileSize / 2f) + ((lh / 2f - 74f) - (groundY - tileSize / 2f)) * zp;
            float drawnSize = tileSize * zoom;
            if (bp > 0f)
            {
                RectangleF box = new RectangleF(cx - drawnSize / 2f,
                    tileCy - drawnSize / 2f - (1f - fallEase) * 280f * zoom, drawnSize, drawnSize);
                float sqY = 1f;
                if (impact > 0f && impact < 1f) sqY = 1f - 0.12f * (1f - Anim.EaseOut(impact));

                if (_t < Reveal)
                {
                    // 纸箱：浅灰蓝卡纸 + 顶盖线 + 描边
                    using (SolidBrush sh = new SolidBrush(Color.FromArgb((int)(30 * fallEase), Theme.Blue)))
                    {
                        g.FillEllipse(sh, box.X + box.Width * 0.1f, groundY - 6, box.Width * 0.8f, 11f);
                    }
                    GraphicsState bs = g.Save();
                    g.TranslateTransform(box.X + box.Width / 2f, box.Bottom);
                    g.ScaleTransform(1f, sqY);
                    g.TranslateTransform(-(box.X + box.Width / 2f), -box.Bottom);
                    RectangleF br = new RectangleF(box.X, box.Y, box.Width, box.Height);
                    Theme.FillRoundedGradient(g, Theme.Box1, Theme.Box2, br, box.Width * 0.18f);
                    Theme.StrokeRounded(g, Theme.BoxEdge, br, box.Width * 0.18f, 2f);
                    using (Pen p = new Pen(Theme.Mix(Theme.BoxEdge, Theme.Muted, 0.6f), Math.Max(1.4f, box.Width * 0.018f)))
                    {
                        g.DrawLine(p, br.X + 6, br.Y + br.Height * 0.24f, br.Right - 6, br.Y + br.Height * 0.24f);
                    }
                    using (SolidBrush lid = new SolidBrush(Color.FromArgb(46, Theme.Blue)))
                    {
                        g.FillRectangle(lid, br.X + br.Width * 0.14f, br.Y + br.Height * 0.24f,
                            br.Width * 0.72f, br.Height * 0.06f);
                    }
                    g.Restore(bs);
                }
                else
                {
                    // logo 展示
                    float appear = Anim.EaseOutBack(Seg(Reveal, Reveal + 260f));
                    float s = Math.Min(1f, appear);
                    GraphicsState ls = g.Save();
                    g.TranslateTransform(box.X + box.Width / 2f, box.Y + box.Height / 2f);
                    g.ScaleTransform(0.92f + 0.08f * s, 0.92f + 0.08f * s);
                    RectangleF lr = new RectangleF(-box.Width / 2f, -box.Height / 2f, box.Width, box.Height);
                    RadialFast(g, 0, 0, box.Width * 1.25f, Theme.Blue, (int)(48 + 26 * (float)Math.Sin(_t / 420.0)));
                    Theme.DrawLogo(g, lr);
                    g.Restore(ls);
                }
            }

            // ---- 冲击波与尘土
            ProfBox += StopwatchTimestamp() - mark;
            mark = System.Diagnostics.Stopwatch.GetTimestamp();
            float ring = Seg(FallEnd, FallEnd + 340f);
            if (ring > 0f && ring < 1f)
            {
                float rr = 42f + 140f * Anim.EaseOut(ring);
                int ra = (int)(80 * (1f - ring) * (1f - ring));
                using (Pen p = new Pen(Color.FromArgb(ra, Theme.Blue), 2.2f - 1.5f * ring))
                {
                    g.DrawEllipse(p, cx - rr, groundY - rr * 0.16f, rr * 2f, rr * 0.32f);
                }
            }
            for (int i = 0; i < 8; i++)
            {
                float dp = Seg(FallEnd, FallEnd + 400f);
                if (dp <= 0f || dp >= 1f) continue;
                int side = (i % 2 == 0) ? -1 : 1;
                float spread = 8f + (i / 2) * 9f;
                float dx = cx + side * (46f + spread + 52f * Anim.EaseOut(dp));
                float dy = groundY - 6f - (4f + (i % 3) * 4f) * Anim.EaseOut(dp);
                int da = (int)(110 * (1f - dp));
                using (SolidBrush b = new SolidBrush(Color.FromArgb(da, Theme.Blue)))
                {
                    float ds = 3f + 2.2f * (1f - dp);
                    g.FillEllipse(b, dx - ds / 2f, dy - ds / 2f, ds, ds);
                }
            }

            // 白闪（遮住纸箱到 logo 的切换）
            float flash = Seg(Reveal - 40f, Reveal + 240f);
            if (flash > 0f && flash < 1f)
            {
                RadialFast(g, cx, lh / 2f - 60, 200f + 120f * flash, Theme.Flash, (int)(235 * (1f - flash)));
            }

            // ---- 阶段 3：轨道卫星
            if (_t > Reveal)
            {
                float orb = Seg(Reveal + 220f, Reveal + 640f);
                float rr = drawnSize * 0.82f;
                float orbY = tileCy;
                using (Pen orbit = new Pen(Ui.Alpha(Theme.Blue, (int)(40 * orb)), 1f))
                {
                    g.DrawEllipse(orbit, cx - rr, orbY - rr * 0.6f, rr * 2f, rr * 1.2f);
                }
                for (int i = 0; i < 2; i++)
                {
                    double ang = _t * 0.0018 + i * Math.PI;
                    float x = cx + (float)Math.Cos(ang) * rr;
                    float y = orbY + (float)Math.Sin(ang) * rr * 0.6f;
                    Color oc = i == 0 ? Theme.Cyan : Theme.Violet;
                    using (SolidBrush b = new SolidBrush(Ui.Alpha(oc, (int)(225 * orb))))
                    {
                        g.FillEllipse(b, x - 3.4f, y - 3.4f, 6.8f, 6.8f);
                    }
                }
            }

            // ---- 文字
            ProfFx += StopwatchTimestamp() - mark;
            mark = System.Diagnostics.Stopwatch.GetTimestamp();
            string title = Loc.T("app.name");
            float titleY = lh / 2f + 42f;
            StringFormat typo = StringFormat.GenericTypographic;
            typo.FormatFlags = typo.FormatFlags | StringFormatFlags.MeasureTrailingSpaces;
            using (typo)
            {
                Font tf = Theme.Font(20f, FontStyle.Bold);
                if (_titleCache != title)
                {
                    _titleCache = title;
                    _titleWidths = new float[title.Length];
                    _titleTotal = 0f;
                    for (int i = 0; i < title.Length; i++)
                    {
                        _titleWidths[i] = g.MeasureString(title[i].ToString(), tf, PointF.Empty, typo).Width;
                        _titleTotal += _titleWidths[i];
                    }
                }
                float startX = cx - _titleTotal / 2f;
                float cursor = 0f;
                for (int i = 0; i < title.Length; i++)
                {
                    float begin = 2010f + i * 58f;
                    float p = Seg(begin, begin + 380f);
                    float x = startX + cursor;
                    cursor += _titleWidths[i];
                    if (p <= 0f) continue;
                    float y = titleY + (1f - Anim.EaseOut(p)) * 12f;
                    using (SolidBrush b = new SolidBrush(Ui.Alpha(Theme.Text, (int)(255 * p))))
                    {
                        g.DrawString(title[i].ToString(), tf, b, x, y, typo);
                    }
                }
            }

            float sp = Seg(2200f, 2520f);
            {
                Font sf = Theme.Font(9f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("app.tagline"), sf, Ui.Alpha(Theme.SubText, (int)(235 * sp)),
                    new RectangleF(cx - 220, titleY + 36, 440, 20), StringAlignment.Center, StringAlignment.Center);
            }

            float pr = Anim.EaseOut(Seg(2180f, 2680f));
            float barW = 190f;
            RectangleF track = new RectangleF(cx - barW / 2f, titleY + 74, barW, 3.5f);
            if (pr > 0f) Theme.FillRounded(g, Ui.Alpha(Theme.Track, 210), track, 2f);
            if (pr > 0.005f)
            {
                RectangleF fill = new RectangleF(track.X, track.Y, Math.Max(4f, barW * pr), 3.5f);
                Theme.FillRoundedGradient(g, Theme.Blue, Theme.Cyan, fill, 2f);
                float shimmer = Seg(2300f, 2760f);
                if (shimmer > 0f && shimmer < 1f)
                {
                    GraphicsState ss = g.Save();
                    g.SetClip(fill);
                    float sx = fill.X - 40f + (fill.Width + 80f) * shimmer;
                    using (LinearGradientBrush lb = new LinearGradientBrush(
                        new PointF(sx - 26, 0), new PointF(sx + 26, 0),
                        Color.FromArgb(0, Color.White), Color.FromArgb(210, Color.White)))
                    {
                        lb.SetBlendTriangularShape(0.5f);
                        g.FillRectangle(lb, new RectangleF(sx - 30, track.Y, 60, 4f));
                    }
                    g.Restore(ss);
                }
            }

            string[] lines = { Loc.T("splash.line1"), Loc.T("splash.line2"), Loc.T("splash.line3") };
            for (int i = 0; i < lines.Length; i++)
            {
                float begin = 2260f + i * 110f;
                float p = Seg(begin, begin + 320f);
                if (p <= 0f) continue;
                float y = lh - 132 + i * 24 + (1f - Anim.EaseOut(p)) * 6f;
                float x = cx - 168f;
                using (SolidBrush b = new SolidBrush(Ui.Alpha(Theme.Blue, (int)(36 * p))))
                {
                    g.FillEllipse(b, x, y + 2, 14, 14);
                }
                using (Pen pen = new Pen(Ui.Alpha(Theme.Blue, (int)(235 * p)), 1.4f))
                {
                    float k = Clamp01((p - 0.35f) / 0.65f);
                    g.DrawLine(pen, x + 4, y + 9, x + 4 + 2.4f * k, y + 9 + 2.4f * k);
                    if (k > 0.5f)
                    {
                        float k2 = (k - 0.5f) / 0.5f;
                        g.DrawLine(pen, x + 6.4f, y + 11.4f, x + 6.4f + 3.6f * k2, y + 11.4f - 3.6f * k2);
                    }
                }
                {
                    Font lf = Theme.Font(8f, FontStyle.Regular);
                    Theme.DrawText(g, lines[i], lf, Ui.Alpha(Theme.SubText, (int)(225 * p)),
                        new RectangleF(x + 22, y - 2, 300, 18), StringAlignment.Near, StringAlignment.Center);
                }
            }

            {
                Font ff = Theme.Font(7.8f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("splash.foot"), ff, Ui.Alpha(Theme.Muted, (int)(200 * Seg(2400f, 2750f))),
                    new RectangleF(cx - 240, lh - 40, 480, 18), StringAlignment.Center, StringAlignment.Center);
                Theme.DrawText(g, Loc.T("splash.skip"), ff, Ui.Alpha(Theme.Muted, (int)(120 * Seg(500f, 900f))),
                    new RectangleF(lw - 110, lh - 40, 92, 18), StringAlignment.Far, StringAlignment.Center);
                float madeP = Seg(2520f, 2860f);
                if (madeP > 0f)
                {
                    Font mf = Theme.Font(8f, FontStyle.Bold);
                    float y = lh - 62 + (1f - Anim.EaseOut(madeP)) * 7f;
                    Theme.DrawText(g, Loc.T("splash.made"), mf,
                        Ui.Alpha(Theme.SubText, (int)(230 * madeP)),
                        new RectangleF(cx - 220, y, 440, 18), StringAlignment.Center, StringAlignment.Center);
                }
            }

            if (fade > 0f)
            {
                int a = (int)(255 * fade);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(a, Theme.Bg)))
                {
                    g.FillRectangle(b, -8, -8, lw + 16, lh + 16);
                }
            }
            PaintTicks += paintSw.ElapsedTicks;
            ProfText += StopwatchTimestamp() - mark;
        }

        private static long StopwatchTimestamp()
        {
            // 占位：由下面的 Stopwatch 差值逻辑替换
            return System.Diagnostics.Stopwatch.GetTimestamp();
        }
    }
}
