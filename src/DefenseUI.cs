using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace CleanC
{
    /// <summary>主动防御开关：胶囊轨道 + 滑动圆点，开启时切换为蓝青渐变。</summary>
    public class ToggleSwitch : Control, IThemed
    {
        private readonly string _key;
        private bool _on;
        private float _v;
        private float _hover;

        public Action Changed;

        public ToggleSwitch()
        {
            _key = Ui.Key("toggle");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            BackColor = Theme.Card;
            Size = new Size(Px(100), Px(32));
        }

        private static int Px(float v)
        {
            return (int)Math.Round(v * Theme.Scale);
        }

        public bool On
        {
            get { return _on; }
        }

        public void SetOn(bool on, bool animate)
        {
            if (_on == on && (animate || Math.Abs(_v - (on ? 1f : 0f)) < 0.01f))
            {
                _on = on;
                Invalidate();
                return;
            }
            _on = on;
            float target = on ? 1f : 0f;
            if (animate)
            {
                Anim.Tween(_key, _v, target, 240, delegate(float v) { _v = v; Invalidate(); }, null);
            }
            else
            {
                _v = target;
                Invalidate();
            }
        }

        public void ApplyTheme()
        {
            BackColor = Parent != null ? Parent.BackColor : Theme.Card;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Anim.Tween(_key + "h", _hover, 1f, 150, delegate(float v) { _hover = v; Invalidate(); }, null);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Anim.Tween(_key + "h", _hover, 0f, 180, delegate(float v) { _hover = v; Invalidate(); }, null);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!Enabled) return;
            SetOn(!_on, true);
            if (Changed != null) Changed();
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
            float v = _v;
            Color on = Theme.Mix(Theme.Blue, Theme.Cyan, 0.25f);
            Color off = Theme.Mix(Theme.Track, Theme.Border, 0.6f);
            Color track = Theme.Mix(off, on, v);
            if (_hover > 0.02f) track = Theme.Mix(track, Theme.Blue, 0.15f * _hover);
            Theme.FillRoundedGradient(g, Theme.Mix(track, Color.White, 0.12f * v), track,
                new RectangleF(0.5f, 3f, w - 1f, h - 6f), (h - 6f) / 2f);
            Theme.StrokeRounded(g, Theme.Mix(Theme.Border, Theme.Blue, 0.4f * v + 0.2f * _hover),
                new RectangleF(0.5f, 3f, w - 1f, h - 6f), (h - 6f) / 2f, 1f);
            float kr = h - 12f;
            float kx = 6f + (w - 12f - kr) * v;
            if (v > 0.05f)
            {
                using (SolidBrush glow = new SolidBrush(Color.FromArgb((int)(36 * v), Theme.Cyan)))
                {
                    g.FillEllipse(glow, kx - 3 + kr / 2f - kr / 2f - 4, 3f, kr + 10, kr + 6);
                }
            }
            using (SolidBrush knob = new SolidBrush(Color.White))
            {
                g.FillEllipse(knob, kx, 6f + 1f, kr, kr);
            }
            using (Pen edge = new Pen(Color.FromArgb(120, Theme.Mix(Theme.Border, Theme.Blue, 0.3f)), 1f))
            {
                g.DrawEllipse(edge, kx, 6f + 1f, kr, kr);
            }
        }
    }

    /// <summary>防护页签右侧内容：总开关、档位、统计、风险列表与处置按钮。</summary>
    public class DefensePanel : Control, IThemed
    {
        private const int LW = 788;
        private const int LH = 484;

        private readonly ToggleSwitch _toggle;
        private readonly Segmented _level;
        private readonly UiButton _checkBtn;
        private readonly UiButton _baselineBtn;
        private readonly UiButton _logBtn;
        private readonly ThreatList _list;
        private List<Threat> _threats = new List<Threat>();
        private bool _hoverToggle;

        public DefensePanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;

            _toggle = new ToggleSwitch();
            _toggle.Bounds = new Rectangle(Px(654), Px(22), Px(102), Px(32));
            _toggle.Changed = delegate { DefenseCenter.SetEnabled(_toggle.On); };
            Controls.Add(_toggle);

            _level = new Segmented();
            _level.Filled = true;
            _level.FontSize = 8.8f;
            _level.Radius = 9f;
            _level.Accent = Theme.Blue;
            _level.ItemColors = new Color[] { Theme.Teal, Theme.Rose };
            _level.Bounds = new Rectangle(Px(112), Px(72), Px(190), Px(30));
            _level.BackColor = Theme.Card;
            _level.SetItems(new string[] { Loc.T("def.level.low"), Loc.T("def.level.high") }, (int)DefenseCenter.Level);
            _level.Changed = delegate { DefenseCenter.SetLevel(_level.SelectedIndex); };
            Controls.Add(_level);

            _checkBtn = new UiButton();
            _checkBtn.Primary = true;
            _checkBtn.Text = Loc.T("def.btn.check");
            _checkBtn.Glyph = "\uE721";
            _checkBtn.Bounds = new Rectangle(Px(628), Px(432), Px(160), Px(40));
            _checkBtn.BackColor = Theme.Bg;
            _checkBtn.Click += delegate { DefenseCenter.CheckNow(); };
            Controls.Add(_checkBtn);

            _baselineBtn = new UiButton();
            _baselineBtn.Primary = false;
            _baselineBtn.Text = Loc.T("def.btn.baseline");
            _baselineBtn.Glyph = "";
            _baselineBtn.Bounds = new Rectangle(Px(496), Px(432), Px(124), Px(40));
            _baselineBtn.BackColor = Theme.Bg;
            _baselineBtn.Click += delegate { Rebuild(); };
            Controls.Add(_baselineBtn);

            _logBtn = new UiButton();
            _logBtn.Primary = false;
            _logBtn.Text = Loc.T("def.btn.log");
            _logBtn.Glyph = "";
            _logBtn.Bounds = new Rectangle(Px(382), Px(432), Px(106), Px(40));
            _logBtn.BackColor = Theme.Bg;
            _logBtn.Click += delegate { Native.OpenLog(DefensePaths.LogPath); };
            Controls.Add(_logBtn);

            _list = new ThreatList();
            _list.Bounds = new Rectangle(Px(0), Px(232), Px(LW), Px(190));
            _list.BackColor = Theme.Bg;
            _list.OpenRequested = OpenThreat;
            Controls.Add(_list);
            _list.Visible = false;
        }

        private static int Px(float v)
        {
            return (int)Math.Round(v * Theme.Scale);
        }

        private Form Host
        {
            get { return FindForm(); }
        }

        public void Sync()
        {
            _threats = DefenseCenter.SnapshotThreats();
            _list.SetThreats(_threats);
            _list.Visible = _threats.Count > 0;
            _toggle.SetOn(DefenseCenter.Enabled, false);
            _level.SetSelected((int)DefenseCenter.Level, false);
            _checkBtn.Enabled = !DefenseCenter.Checking;
            _checkBtn.Text = DefenseCenter.Checking ? Loc.T("def.btn.checking") : Loc.T("def.btn.check");
            Invalidate();
        }

        public void Relocalize()
        {
            _level.SetItems(new string[] { Loc.T("def.level.low"), Loc.T("def.level.high") }, (int)DefenseCenter.Level);
            _checkBtn.Text = DefenseCenter.Checking ? Loc.T("def.btn.checking") : Loc.T("def.btn.check");
            _baselineBtn.Text = Loc.T("def.btn.baseline");
            _logBtn.Text = Loc.T("def.btn.log");
            _list.RefreshView();
            Invalidate();
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Bg;
            _toggle.BackColor = Theme.Card;
            _level.BackColor = Theme.Card;
            _level.Accent = Theme.Blue;
            _level.ItemColors = new Color[] { Theme.Teal, Theme.Rose };
            _checkBtn.BackColor = Theme.Bg;
            _baselineBtn.BackColor = Theme.Bg;
            _logBtn.BackColor = Theme.Bg;
            _list.BackColor = Theme.Bg;
            _list.Invalidate();
            Invalidate();
        }

        private void Rebuild()
        {
            Form host = Host;
            bool ok = true;
            if (host != null)
            {
                MapConfirmDialog d = new MapConfirmDialog(Loc.T("def.baseline.confirmTitle"),
                    new string[] { Loc.T("def.baseline.confirm1"), Loc.T("def.baseline.confirm2") },
                    false, Loc.T("def.baseline.confirmOk"), false);
                ok = d.ShowDialog(host) == DialogResult.OK;
            }
            if (ok) DefenseCenter.RebuildBaseline();
        }

        private void OpenThreat(Threat t)
        {
            Form host = Host;
            if (host == null) return;
            ThreatDialog d = new ThreatDialog(t);
            d.ShowDialog(host);
            Sync();
        }

        // -------------------------------------------------------------- paint

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.ScaleTransform(Theme.Scale, Theme.Scale);
            float w = Width / Theme.Scale;
            float h = Height / Theme.Scale;
            if (w < 100f) w = LW;
            if (h < 100f) h = LH;

            bool on = DefenseCenter.Enabled;
            bool busy = DefenseCenter.Checking;

            RectangleF card = new RectangleF(0, 0, w, 118);
            Theme.FillRounded(g, Theme.Card, card, 16f);
            Theme.StrokeRounded(g, Theme.PanelBorder, card, 16f, 1f);
            using (SolidBrush glow = new SolidBrush(Ui.Alpha(Theme.Blue, 12)))
            {
                g.FillEllipse(glow, w - 180, -46, 220, 160);
            }
            using (SolidBrush glow2 = new SolidBrush(Ui.Alpha(Theme.Cyan, 8)))
            {
                g.FillEllipse(glow2, w - 280, 22, 160, 120);
            }

            // 盾牌
            RectangleF shield = new RectangleF(24, 22, 44, 50);
            DrawShield(g, shield, on, busy);

            {
                Font tf = Theme.Font(13f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("def.overview"), tf, Theme.Text, new RectangleF(82, 20, 300, 24),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Color dot = busy ? Theme.Amber : (on ? Theme.Green : Theme.Muted);
                using (SolidBrush b = new SolidBrush(dot))
                {
                    g.FillEllipse(b, 84, 50, 7, 7);
                }
                if (busy)
                {
                    using (SolidBrush b = new SolidBrush(Ui.Alpha(Theme.Amber, 60)))
                    {
                        g.FillEllipse(b, 80, 46, 15, 15);
                    }
                }
                Font sf = Theme.Font(8.2f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("def.coverage"), sf, Theme.SubText,
                    new RectangleF(98, 44, w - 240, 20), StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font lf = Theme.Font(8.4f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("def.level.label"), lf, Theme.Muted, new RectangleF(76, 72, 36, 30),
                    StringAlignment.Near, StringAlignment.Center);
                Font hf = Theme.Font(7.8f, FontStyle.Regular);
                string hint = DefenseCenter.Level == DefenseLevel.High ? Loc.T("def.level.highHint") : Loc.T("def.level.lowHint");
                Theme.DrawText(g, hint, hf, Theme.Muted, new RectangleF(312, 72, w - 372, 30),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font tf = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, on ? Loc.T("def.toggleOn") : Loc.T("def.toggleOff"), tf,
                    on ? Theme.Blue : Theme.Muted, new RectangleF(654, 58, 104, 16),
                    StringAlignment.Center, StringAlignment.Center);
            }

            // 三张统计卡
            DrawStatCard(g, new RectangleF(0, 130, 250, 58), Theme.Blue, Loc.T("def.stat.files"),
                DefenseCenter.FilesWatched.ToString());
            DrawStatCard(g, new RectangleF(269, 130, 250, 58), Theme.Violet, Loc.T("def.stat.registry"),
                DefenseCenter.RegWatched.ToString());
            DrawStatCard(g, new RectangleF(538, 130, 250, 58), Theme.Rose, Loc.T("def.stat.blocked"),
                DefenseCenter.Blocked.ToString());

            // 风险区标题
            {
                Font hf = Theme.Font(10f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("def.threats.title"), hf, Theme.Text, new RectangleF(0, 202, 180, 26),
                    StringAlignment.Near, StringAlignment.Center);
                string count = _threats.Count.ToString();
                RectangleF chip = new RectangleF(104, 206, 34, 18);
                Color chipColor = _threats.Count == 0 ? Theme.Green : Theme.Red;
                Theme.FillRounded(g, Ui.Alpha(chipColor, 20), chip, 9f);
                Font cf = Theme.Font(8f, FontStyle.Bold);
                Theme.DrawText(g, count, cf, chipColor, chip, StringAlignment.Center, StringAlignment.Center);
                Font hint = Theme.Font(7.8f, FontStyle.Regular);
                Theme.DrawText(g, _threats.Count > 0 ? Loc.T("def.threats.hint") : Loc.T("def.threats.hintEmpty"),
                    hint, Theme.Muted, new RectangleF(w - 420, 202, 420, 26), StringAlignment.Far, StringAlignment.Center);
            }

            if (_threats.Count == 0)
            {
                RectangleF empty = new RectangleF(0, 236, w, 186);
                Theme.FillRounded(g, Theme.Surface, empty, 14f);
                Theme.StrokeRounded(g, Theme.Divider, empty, 14f, 1f);
                using (SolidBrush b = new SolidBrush(Ui.Alpha(on ? Theme.Green : Theme.Muted, 16)))
                {
                    g.FillEllipse(b, w / 2f - 26, 264, 52, 52);
                }
                DrawShield(g, new RectangleF(w / 2f - 16, 272, 32, 36), on, busy);
                Font tf = Theme.Font(9.4f, FontStyle.Bold);
                Theme.DrawText(g, on ? Loc.T("def.threats.empty") : Loc.T("def.threats.offTitle"), tf, Theme.Text,
                    new RectangleF(0, 330, w, 20), StringAlignment.Center, StringAlignment.Center);
                Font sf = Theme.Font(8.2f, FontStyle.Regular);
                Theme.DrawText(g, on ? Loc.T("def.threats.emptySub") : Loc.T("def.threats.offSub"), sf, Theme.SubText,
                    new RectangleF(0, 352, w, 20), StringAlignment.Center, StringAlignment.Center);
                if (!on)
                {
                    Font af = Theme.Font(8f, FontStyle.Bold);
                    Theme.DrawText(g, Loc.T("def.threats.offHint"), af, Theme.Blue,
                        new RectangleF(0, 376, w, 20), StringAlignment.Center, StringAlignment.Center);
                }
            }

            // 底部状态
            {
                Font sf = Theme.Font(7.8f, FontStyle.Regular);
                string baseline = IntegritySurvey.BaselineExists() ? Loc.T("def.baseline.ready") : Loc.T("def.baseline.notReady");
                string text = baseline + " · " + Loc.F("def.checks", DefenseCenter.CheckCount);
                Theme.DrawText(g, text, sf, Theme.Muted, new RectangleF(0, 432, 360, 40),
                    StringAlignment.Near, StringAlignment.Center);
                if (DefenseCenter.Error.Length > 0)
                {
                    Font ef = Theme.Font(7.6f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.T("def.error") + " " + DefenseCenter.Error, ef, Theme.Red,
                        new RectangleF(0, 410, 360, 18), StringAlignment.Near, StringAlignment.Center);
                }
            }
        }

        private void DrawShield(Graphics g, RectangleF r, bool on, bool busy)
        {
            Color c1 = on ? Theme.Blue : Theme.Muted;
            Color c2 = on ? Theme.Cyan : Theme.Mix(Theme.Muted, Theme.Border, 0.5f);
            if (busy) c2 = Theme.Amber;
            float x = r.X;
            float y = r.Y;
            float w = r.Width;
            float h = r.Height;
            PointF[] pts =
            {
                new PointF(x + w * 0.5f, y),
                new PointF(x + w * 0.94f, y + h * 0.16f),
                new PointF(x + w * 0.88f, y + h * 0.62f),
                new PointF(x + w * 0.5f, y + h),
                new PointF(x + w * 0.12f, y + h * 0.62f),
                new PointF(x + w * 0.06f, y + h * 0.16f)
            };
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddPolygon(pts);
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new PointF(x, y), new PointF(x + w, y + h), c1, c2))
                {
                    g.FillPath(b, p);
                }
                using (Pen pen = new Pen(on ? Theme.BlueDeep : Theme.BorderStrong, 1.2f))
                {
                    g.DrawPath(pen, p);
                }
            }
            using (Pen check = new Pen(Color.White, Math.Max(1.6f, w * 0.075f)))
            {
                float cx = x + w * 0.5f;
                float cy = y + h * 0.44f;
                float s = w * 0.22f;
                g.DrawLine(check, cx - s, cy, cx - s * 0.15f, cy + s * 0.8f);
                g.DrawLine(check, cx - s * 0.15f, cy + s * 0.8f, cx + s, cy - s * 0.55f);
            }
        }

        private void DrawStatCard(Graphics g, RectangleF r, Color dot, string label, string value)
        {
            Theme.FillRounded(g, Theme.Card, r, 13f);
            Theme.StrokeRounded(g, Theme.Divider, r, 13f, 1f);
            using (SolidBrush b = new SolidBrush(Ui.Alpha(dot, 18)))
            {
                g.FillEllipse(b, r.X + 14, r.Y + 16, 26, 26);
            }
            using (SolidBrush b = new SolidBrush(dot))
            {
                g.FillEllipse(b, r.X + 21, r.Y + 23, 12, 12);
            }
            {
                Font lf = Theme.Font(8.4f, FontStyle.Regular);
                Theme.DrawText(g, label, lf, Theme.SubText, new RectangleF(r.X + 48, r.Y + 8, r.Width - 60, 18),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font vf = Theme.Font(14.5f, FontStyle.Bold);
                Theme.DrawText(g, value, vf, Theme.Text, new RectangleF(r.X + 48, r.Y + 24, r.Width - 60, 28),
                    StringAlignment.Near, StringAlignment.Center);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _hoverToggle = _toggle.Bounds.Contains(e.Location);
            Cursor = _hoverToggle ? Cursors.Hand : Cursors.Default;
        }
    }

    /// <summary>风险列表：每行一条，点击进入处置对话框。</summary>
    public class ThreatList : Control, IThemed
    {
        private List<Threat> _items = new List<Threat>();
        private int _scroll;
        private int _maxScroll;
        private int _hover = -1;
        private readonly string _hoverKey;
        private float _hoverAmt;

        public Action<Threat> OpenRequested;

        private const int RowH = 62;
        private const int Gap = 4;

        public ThreatList()
        {
            _hoverKey = Ui.Key("threatHover");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            BackColor = Theme.Bg;
        }

        public void SetThreats(List<Threat> list)
        {
            _items = list == null ? new List<Threat>() : new List<Threat>(list);
            _scroll = 0;
            _hover = -1;
            Invalidate();
        }

        public void RefreshView()
        {
            Invalidate();
        }

        public void ApplyTheme()
        {
            BackColor = Parent != null ? Parent.BackColor : Theme.Bg;
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

        private int RowTop(int i)
        {
            return i * (RowH + Gap) - _scroll;
        }

        private int Hit(Point pt)
        {
            float y = pt.Y / Theme.Scale + _scroll;
            int idx = (int)(y / (RowH + Gap));
            if (idx < 0 || idx >= _items.Count) return -1;
            float localY = y - idx * (RowH + Gap);
            if (localY > RowH) return -1;
            return idx;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point lp = new Point((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
            int idx = Hit(lp);
            if (idx != _hover)
            {
                _hover = idx;
                Anim.Tween(_hoverKey, _hoverAmt, idx >= 0 ? 1f : 0f, 140,
                    delegate(float v) { _hoverAmt = v; Invalidate(); }, null);
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Anim.Tween(_hoverKey, _hoverAmt, 0f, 160,
                delegate(float v) { _hoverAmt = v; Invalidate(); }, null);
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Point lp = new Point((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
            int idx = Hit(lp);
            if (idx < 0 || idx >= _items.Count) return;
            if (OpenRequested != null) OpenRequested(_items[idx]);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int content = _items.Count * (RowH + Gap);
            _maxScroll = Math.Max(0, content - (int)LH);
            _scroll -= e.Delta / 3;
            if (_scroll < 0) _scroll = 0;
            if (_scroll > _maxScroll) _scroll = _maxScroll;
            _hover = -1;
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
            float lw = LW;
            float lh = LH;
            _maxScroll = Math.Max(0, _items.Count * (RowH + Gap) - (int)lh);
            for (int i = 0; i < _items.Count; i++)
            {
                int top = RowTop(i);
                if (top > lh || top + RowH < 0) continue;
                RectangleF r = new RectangleF(0, top, lw - 8, RowH);
                Threat t = _items[i];
                Color sev = SeverityColor(t.Severity);
                Color fill = i == _hover ? Theme.CardHover : Theme.Card;
                Theme.FillRounded(g, fill, r, 12f);
                Theme.StrokeRounded(g, i == _hover ? Theme.Mix(Theme.Border, sev, 0.35f * _hoverAmt) : Theme.Divider, r, 12f, 1f);
                RectangleF bar = new RectangleF(r.X + 8, r.Y + 10, 3, RowH - 20);
                Theme.FillRounded(g, sev, bar, 1.5f);
                if (i == _hover)
                {
                    using (SolidBrush glow = new SolidBrush(Ui.Alpha(sev, 10)))
                    {
                        g.FillEllipse(glow, r.Right - 90, r.Y + 6, 80, 52);
                    }
                }
                Color kindColor = KindColor(t.Kind);
                RectangleF kindChip = new RectangleF(r.X + 20, r.Y + 10, 64, 18);
                Theme.FillRounded(g, Ui.Alpha(kindColor, 18), kindChip, 9f);
                {
                    Font kf = Theme.Font(7.6f, FontStyle.Bold);
                    Theme.DrawText(g, t.KindText, kf, kindColor, kindChip, StringAlignment.Center, StringAlignment.Center);
                }
                {
                    Font tf = Theme.Font(9.2f, FontStyle.Bold);
                    Theme.DrawText(g, t.Title, tf, Theme.Text, new RectangleF(r.X + 94, r.Y + 7, r.Width - 210, 22),
                        StringAlignment.Near, StringAlignment.Center);
                }
                RectangleF sevChip = new RectangleF(r.Right - 84, r.Y + 9, 68, 18);
                Theme.FillRounded(g, Ui.Alpha(sev, 22), sevChip, 9f);
                {
                    Font sf = Theme.Font(7.6f, FontStyle.Bold);
                    string label = t.Severity == ThreatSeverity.High ? Loc.T("def.sev.high")
                        : (t.Severity == ThreatSeverity.Medium ? Loc.T("def.sev.medium") : Loc.T("def.sev.low"));
                    Theme.DrawText(g, label, sf, sev, sevChip, StringAlignment.Center, StringAlignment.Center);
                }
                {
                    Font df = Theme.Font(8f, FontStyle.Regular);
                    Theme.DrawText(g, t.Detail, df, Theme.SubText, new RectangleF(r.X + 94, r.Y + 31, r.Width - 128, 20),
                        StringAlignment.Near, StringAlignment.Center);
                }
                using (SolidBrush b = new SolidBrush(Theme.Muted))
                {
                    Font gf = Theme.Font(10f, FontStyle.Regular);
                    g.DrawString("›", gf, b, r.Right - 22, r.Y + RowH / 2f - 8);
                }
            }

            if (_items.Count * (RowH + Gap) > lh)
            {
                float trackH = lh;
                float thumbH = Math.Max(28f, trackH * lh / (_items.Count * (RowH + Gap)));
                float maxScroll = Math.Max(1f, _items.Count * (RowH + Gap) - lh);
                float thumbY = (trackH - thumbH) * (_scroll / maxScroll);
                RectangleF track = new RectangleF(lw - 4, 0, 3, trackH);
                Theme.FillRounded(g, Ui.Alpha(Theme.Border, 60), track, 1.5f);
                Theme.FillRounded(g, Theme.Muted, new RectangleF(lw - 4, thumbY, 3, thumbH), 1.5f);
            }
            if (_items.Count == 0)
            {
                Font f = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("def.threats.empty"), f, Theme.Muted, new RectangleF(0, 0, lw, lh),
                    StringAlignment.Center, StringAlignment.Center);
            }
        }

        public static Color SeverityColor(ThreatSeverity s)
        {
            if (s == ThreatSeverity.High) return Theme.Red;
            if (s == ThreatSeverity.Medium) return Theme.Amber;
            return Theme.Gray;
        }

        public static Color KindColor(ThreatKind k)
        {
            if (k == ThreatKind.FileIntegrity) return Theme.Red;
            if (k == ThreatKind.Registry) return Theme.Amber;
            if (k == ThreatKind.Startup) return Theme.Violet;
            if (k == ThreatKind.SuspiciousFile) return Theme.Rose;
            return Theme.Blue;
        }
    }

    /// <summary>风险详情：说明、处理建议、目标路径与可执行处置按钮。</summary>
    public class ThreatDialog : DialogBase
    {
        private readonly Threat _t;
        private readonly UiButton _primary;
        private readonly UiButton _quarantine;
        private readonly UiButton _ignore;
        private readonly UiButton _close;
        private string _note = "";
        private Color _noteColor = Theme.Red;
        private readonly RectangleF _openRect;
        private bool _openHover;

        public ThreatDialog(Threat t)
            : base(Loc.T("def.dialog.title"), 580, 500)
        {
            _t = t;
            int by = LHeight - 58;

            string primaryText = null;
            if (t.Fix == ThreatFix.RestoreRegistry || t.Fix == ThreatFix.RemoveIfeo) primaryText = Loc.T("def.btn.fix");
            else if (t.Fix == ThreatFix.DisableStartup) primaryText = Loc.T("def.btn.disableStartup");
            else if (t.Fix == ThreatFix.QuarantineFile) primaryText = Loc.T("def.btn.quarantine");

            if (primaryText != null)
            {
                _primary = new UiButton();
                _primary.Primary = true;
                _primary.Text = primaryText;
                _primary.Glyph = t.Fix == ThreatFix.QuarantineFile ? "" : "\uE73E";
                MakeButton(_primary, 26, by, 150, 40);
                _primary.Click += delegate { RunPrimary(); };
            }

            if (t.Fix != ThreatFix.QuarantineFile && t.FileExists &&
                (t.Kind == ThreatKind.SuspiciousFile || t.Kind == ThreatKind.Startup))
            {
                _quarantine = new UiButton();
                _quarantine.Primary = false;
                _quarantine.Danger = false;
                _quarantine.Text = Loc.T("def.btn.quarantine");
                _quarantine.Glyph = "";
                MakeButton(_quarantine, primaryText != null ? 188 : 26, by, 130, 40);
                _quarantine.Click += delegate { RunQuarantine(); };
            }

            int ix = primaryText != null ? 330 : (_quarantine != null ? 168 : 26);
            _ignore = new UiButton();
            _ignore.Primary = false;
            _ignore.Text = Loc.T("def.btn.ignore");
            _ignore.Glyph = "";
            MakeButton(_ignore, ix, by, 96, 40);
            _ignore.Click += delegate
            {
                DefenseCenter.Ignore(_t);
                Close();
            };
            _close = new UiButton();
            _close.Primary = false;
            _close.Text = Loc.T("btn.close");
            _close.Glyph = "";
            MakeButton(_close, ix + 108, by, 96, 40);
            _close.Click += delegate { Close(); };

            _openRect = new RectangleF(LWidth - 142, 386, 116, 26);
        }

        private void RunPrimary()
        {
            string msg;
            bool ok = DefenseCenter.Apply(_t, out msg);
            if (ok)
            {
                Close();
            }
            else
            {
                _note = msg;
                _noteColor = Theme.Red;
                Invalidate();
            }
        }

        private void RunQuarantine()
        {
            string msg;
            bool ok = DefenseCenter.Quarantine(_t, out msg);
            if (ok)
            {
                Close();
            }
            else
            {
                _note = msg;
                _noteColor = Theme.Red;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point lp = LogicalPoint(e);
            bool hover = _t.FileExists && _openRect.Contains(lp.X, lp.Y);
            if (hover != _openHover)
            {
                _openHover = hover;
                Cursor = hover ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Point lp = LogicalPoint(e);
            if (_t.FileExists && _openRect.Contains(lp.X, lp.Y) && _t.Target.Length > 0)
            {
                Native.ShowInExplorer(_t.Target);
                return;
            }
        }

        protected override void PaintBody(Graphics g)
        {
            Color sev = ThreatList.SeverityColor(_t.Severity);
            Color kind = ThreatList.KindColor(_t.Kind);

            RectangleF kindChip = new RectangleF(26, 70, 74, 20);
            Theme.FillRounded(g, Ui.Alpha(kind, 18), kindChip, 10f);
            {
                Font f = Theme.Font(8f, FontStyle.Bold);
                Theme.DrawText(g, _t.KindText, f, kind, kindChip, StringAlignment.Center, StringAlignment.Center);
            }
            RectangleF sevChip = new RectangleF(108, 70, 74, 20);
            Theme.FillRounded(g, Ui.Alpha(sev, 22), sevChip, 10f);
            {
                Font f = Theme.Font(8f, FontStyle.Bold);
                string label = _t.Severity == ThreatSeverity.High ? Loc.T("def.sev.high")
                    : (_t.Severity == ThreatSeverity.Medium ? Loc.T("def.sev.medium") : Loc.T("def.sev.low"));
                Theme.DrawText(g, label, f, sev, sevChip, StringAlignment.Center, StringAlignment.Center);
            }
            {
                Font tf = Theme.Font(12.5f, FontStyle.Bold);
                Theme.DrawText(g, _t.Title, tf, Theme.Text, new RectangleF(26, 100, LWidth - 52, 28),
                    StringAlignment.Near, StringAlignment.Center);
            }

            RectangleF detailCard = new RectangleF(24, 136, LWidth - 48, 96);
            Theme.FillRounded(g, Theme.Surface, detailCard, 12f);
            Theme.StrokeRounded(g, Theme.Divider, detailCard, 12f, 1f);
            DrawWrap(g, _t.Detail, Theme.Font(8.4f, FontStyle.Regular), Theme.Text,
                new RectangleF(detailCard.X + 14, detailCard.Y + 10, detailCard.Width - 28, detailCard.Height - 20),
                StringTrimming.EllipsisWord);

            {
                Font sf = Theme.Font(9.2f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("def.dialog.solutionTitle"), sf, Theme.Text,
                    new RectangleF(26, 246, 180, 22), StringAlignment.Near, StringAlignment.Center);
            }
            RectangleF solutionCard = new RectangleF(24, 272, LWidth - 48, 98);
            Theme.FillRounded(g, Ui.Alpha(Theme.Amber, 16), solutionCard, 12f);
            Theme.StrokeRounded(g, Ui.Alpha(Theme.Amber, 50), solutionCard, 12f, 1f);
            using (SolidBrush b = new SolidBrush(Theme.Amber))
            {
                Font gf = Theme.IconFont(12f);
                g.DrawString("\uE7BA", gf, b, solutionCard.X + 12, solutionCard.Y + 12);
            }
            DrawWrap(g, _t.Solution, Theme.Font(8.4f, FontStyle.Regular), Theme.Text,
                new RectangleF(solutionCard.X + 34, solutionCard.Y + 10, solutionCard.Width - 48, solutionCard.Height - 18),
                StringTrimming.EllipsisWord);

            RectangleF targetCard = new RectangleF(24, 382, LWidth - 48, 32);
            Theme.FillRounded(g, Theme.Sunken, targetCard, 10f);
            Theme.StrokeRounded(g, Theme.Divider, targetCard, 10f, 1f);
            using (SolidBrush b = new SolidBrush(Theme.Muted))
            {
                Font gf = Theme.IconFont(11f);
                g.DrawString("\uE8B7", gf, b, targetCard.X + 10, targetCard.Y + 9);
            }
            {
                Font pf = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, _t.Target, pf, Theme.SubText,
                    new RectangleF(targetCard.X + 30, targetCard.Y, targetCard.Width - ( _t.FileExists ? 156 : 44), targetCard.Height),
                    StringAlignment.Near, StringAlignment.Center);
            }
            if (_t.FileExists)
            {
                if (_openHover)
                {
                    Theme.FillRounded(g, Ui.Alpha(Theme.Blue, 14), _openRect, 8f);
                }
                Font of = Theme.Font(8.2f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("def.dialog.open"), of, _openHover ? Theme.BlueDeep : Theme.Blue,
                    _openRect, StringAlignment.Center, StringAlignment.Center);
            }

            if (_t.Extra.Length > 0)
            {
                Font ef = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, _t.Extra, ef, Theme.Red, new RectangleF(26, 416, LWidth - 52, 20),
                    StringAlignment.Near, StringAlignment.Center);
            }
            if (_note.Length > 0)
            {
                Font nf = Theme.Font(8.2f, FontStyle.Regular);
                Theme.DrawText(g, _note, nf, _noteColor, new RectangleF(26, LHeight - 78, LWidth - 52, 18),
                    StringAlignment.Near, StringAlignment.Center);
            }
        }

        private static void DrawWrap(Graphics g, string text, Font f, Color c, RectangleF r, StringTrimming trim)
        {
            using (SolidBrush b = new SolidBrush(c))
            using (StringFormat sf = new StringFormat())
            {
                sf.Trimming = trim;
                sf.FormatFlags = StringFormatFlags.LineLimit;
                g.DrawString(text, f, b, r, sf);
            }
        }
    }

    /// <summary>右下角拦截提示：滑入、置顶、可点击查看处理方案。</summary>
    public class ToastForm : Form
    {
        private static ToastForm _current;
        private static readonly int TW = 386;
        private static readonly int TH = 148;

        private string _title;
        private string _body;
        private Action _view;
        private readonly string _key;
        private float _t;
        private bool _closing;
        private Timer _timer;
        private UiButton _viewBtn;
        private RectangleF _closeRect;
        private bool _closeHover;
        private int _targetX;
        private int _targetY;

        private ToastForm(string title, string body, Action view)
        {
            _title = title;
            _body = body;
            _view = view;
            _key = Ui.Key("toast");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            MinimizeBox = false;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.None;
            ImeMode = ImeMode.Disable;
            ClientSize = new Size(Px(TW), Px(TH));
            BackColor = Theme.Card;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            _closeRect = new RectangleF(TW - 38, 12, 26, 26);

            _viewBtn = new UiButton();
            _viewBtn.Primary = false;
            _viewBtn.Text = Loc.T("def.toast.view");
            _viewBtn.Glyph = "";
            _viewBtn.Bounds = new Rectangle(Px(TW - 138), Px(TH - 44), Px(122), Px(30));
            _viewBtn.BackColor = Theme.Card;
            _viewBtn.Click += delegate { View(); };
            Controls.Add(_viewBtn);

            _timer = new Timer();
            _timer.Interval = 9000;
            _timer.Tick += delegate { CloseAnimated(); };
        }

        private static int Px(float v)
        {
            return (int)Math.Round(v * Theme.Scale);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle = cp.ClassStyle | 0x00020000;
                cp.ExStyle = cp.ExStyle | 0x00000080 | 0x08000000;
                return cp;
            }
        }

        public static void CloseAny()
        {
            ToastForm f = _current;
            _current = null;
            if (f != null && !f.IsDisposed)
            {
                try { f.Close(); } catch { }
            }
        }

        public static void ShowAlert(Control owner, string title, string body, Action view)
        {
            if (_current != null && !_current.IsDisposed)
            {
                _current.UpdateContent(title, body, view);
                _current.BringToFront();
                return;
            }
            ToastForm f = new ToastForm(title, body, view);
            _current = f;
            f.ShowToast();
        }

        private void UpdateContent(string title, string body, Action view)
        {
            _title = title;
            _body = body;
            _view = view;
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Start();
            }
            Invalidate();
        }

        private void ShowToast()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            _targetX = wa.Right - Width - Px(16);
            _targetY = wa.Bottom - Height - Px(16);
            // 先放在靠右的可见位置，再做 24px 的滑入；即使动画被系统节流也不会完全跑出屏幕。
            Location = new Point(_targetX + Px(24), _targetY);
            Show();
            _timer.Start();
            Anim.Tween(_key, 0f, 1f, 380, delegate(float v)
            {
                _t = v;
                int x = _targetX + (int)(Px(24) * (1f - Anim.EaseOut(v)));
                Location = new Point(x, _targetY);
            }, delegate { Location = new Point(_targetX, _targetY); });
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Native.Round(Handle);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (_current == this) _current = null;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (_timer != null) _timer.Stop();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_timer != null && !_closing) _timer.Start();
        }

        private void CloseAnimated()
        {
            if (_closing) return;
            _closing = true;
            if (_timer != null) _timer.Stop();
            Anim.Tween(_key + "out", 0f, 1f, 220, delegate(float v)
            {
                Location = new Point((int)(_targetX + Px(20) * v), _targetY);
            }, delegate { Close(); });
        }

        private void View()
        {
            Action act = _view;
            CloseAnimated();
            act();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point lp = new Point((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
            bool hover = _closeRect.Contains(lp.X, lp.Y);
            if (hover != _closeHover)
            {
                _closeHover = hover;
                Cursor = hover ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Point lp = new Point((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
            if (_closeRect.Contains(lp.X, lp.Y))
            {
                CloseAnimated();
                return;
            }
            if (lp.Y < TH - 52) View();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Card);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.ScaleTransform(Theme.Scale, Theme.Scale);
            float w = Width / Theme.Scale;
            float h = Height / Theme.Scale;

            using (Pen border = new Pen(Theme.Mix(Theme.Border, Theme.Blue, 0.2f), 1f))
            {
                g.DrawRectangle(border, 0.5f, 0.5f, w - 1f, h - 1f);
            }
            using (SolidBrush accent = new SolidBrush(Theme.Red))
            {
                g.FillRectangle(accent, 0, 0, 4, h);
            }
            using (SolidBrush bg = new SolidBrush(Ui.Alpha(Theme.Red, 10)))
            {
                g.FillEllipse(bg, 14, 14, 40, 40);
            }
            using (SolidBrush b = new SolidBrush(Theme.Red))
            {
                Font gf = Theme.IconFont(17f);
                g.DrawString("\uE7BA", gf, b, 23, 22);
            }
            {
                Font tf = Theme.Font(10f, FontStyle.Bold);
                Theme.DrawText(g, _title, tf, Theme.Text, new RectangleF(62, 12, w - 108, 22),
                    StringAlignment.Near, StringAlignment.Center);
            }
            using (SolidBrush b = new SolidBrush(Theme.SubText))
            using (StringFormat sf = new StringFormat())
            {
                sf.Trimming = StringTrimming.EllipsisWord;
                sf.FormatFlags = StringFormatFlags.LineLimit;
                g.DrawString(_body, Theme.Font(8.6f, FontStyle.Regular), b,
                    new RectangleF(62, 38, w - 82, 58), sf);
            }
            if (_closeHover)
            {
                Theme.FillRounded(g, Theme.Hover, _closeRect, 8f);
            }
            using (Pen p = new Pen(_closeHover ? Theme.Blue : Theme.Muted, 1.2f))
            {
                float x = _closeRect.X + 8;
                float y = _closeRect.Y + 8;
                g.DrawLine(p, x, y, x + 10, y + 10);
                g.DrawLine(p, x + 10, y, x, y + 10);
            }
        }
    }
}
