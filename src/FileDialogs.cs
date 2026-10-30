using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CleanC
{
    /// <summary>文件地图右键菜单：仿语言菜单的自绘弹出面板。</summary>
    public class FileMenuForm : Form
    {
        private readonly string[] _labels;
        private readonly string[] _glyphs;
        private readonly bool[] _enabled;
        private readonly bool[] _danger;
        private readonly Action<int> _pick;
        private int _hover = -1;
        private int _openAt;
        private const int RowH = 36;

        public FileMenuForm(string[] labels, string[] glyphs, bool[] enabled, bool[] danger, Action<int> pick)
        {
            _labels = labels;
            _glyphs = glyphs;
            _enabled = enabled;
            _danger = danger;
            _pick = pick;
            Text = "FileMenu";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(Px(232), Px(labels.Length * RowH + 14));
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            ImeMode = ImeMode.Disable;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        private static int Px(float v)
        {
            return (int)Math.Round(v * Theme.Scale);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle = cp.ClassStyle | 0x00020000;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Native.Round(Handle);
            Native.Blend(Handle, 160);
            _openAt = Environment.TickCount;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (MainForm.MapDialog == "menu") return;
            if (Environment.TickCount - _openAt > 200) Close();
        }

        private float LW
        {
            get { return Width / Theme.Scale; }
        }

        private RectangleF RowRect(int i)
        {
            return new RectangleF(7, 7 + i * RowH, LW - 14, RowH - 2);
        }

        private int Hit(Point p)
        {
            for (int i = 0; i < _labels.Length; i++)
            {
                if (RowRect(i).Contains(p.X, p.Y)) return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point lp = new Point((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
            int idx = Hit(lp);
            if (idx >= 0 && !_enabled[idx]) idx = -1;
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
            if (_hover != -1)
            {
                _hover = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Point lp = new Point((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
            int idx = Hit(lp);
            if (idx < 0 || !_enabled[idx]) return;
            Close();
            if (_pick != null) _pick(idx);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.ScaleTransform(Theme.Scale, Theme.Scale);
            float lw = LW;
            float lh = Height / Theme.Scale;
            RectangleF card = new RectangleF(0, 0, lw - 1, lh - 1);
            Theme.FillRounded(g, Theme.Card, card, 14f);
            Theme.StrokeRounded(g, Theme.Border, card, 14f, 1f);

            for (int i = 0; i < _labels.Length; i++)
            {
                RectangleF row = RowRect(i);
                bool on = _enabled[i];
                bool danger = _danger != null && _danger.Length > i && _danger[i];
                Color ink = on ? (danger ? Theme.Red : Theme.Text) : Theme.DisabledText;
                if (i == _hover)
                {
                    Theme.FillRounded(g, danger ? Theme.DangerSoft : Theme.Surface, row, 9f);
                }
                if (_glyphs != null && _glyphs.Length > i && _glyphs[i].Length > 0)
                {
                    using (SolidBrush b = new SolidBrush(on ? (danger ? Theme.Red : Theme.Blue) : Theme.DisabledText))
                    {
                        Font gf = Theme.IconFont(11.5f);
                        g.DrawString(_glyphs[i], gf, b, row.X + 12, row.Y + 10);
                    }
                }
                {
                    Font f = Theme.Font(9f, FontStyle.Bold);
                    Theme.DrawText(g, _labels[i], f, ink,
                        new RectangleF(row.X + 40, row.Y, row.Width - 48, row.Height), StringAlignment.Near,
                        StringAlignment.Center);
                }
            }
        }
    }

    /// <summary>通用确认 / 消息弹窗：支持单按钮（只知道了）与危险确认。</summary>
    public class MapConfirmDialog : DialogBase
    {
        private readonly string[] _lines;
        private readonly bool _danger;
        private readonly bool _onlyOk;
        private readonly string _okText;

        public MapConfirmDialog(string title, string[] lines, bool danger, string okText, bool onlyOk)
            : base(title, 540, 196 + (lines == null ? 0 : lines.Length * 22))
        {
            _lines = lines == null ? new string[0] : lines;
            _danger = danger;
            _onlyOk = onlyOk;
            _okText = okText == null || okText.Length == 0 ? Loc.T("btn.ok") : okText;

            int by = LHeight - 62;
            if (!_onlyOk)
            {
                UiButton cancel = new UiButton();
                cancel.Primary = false;
                cancel.Glyph = "";
                cancel.Text = Loc.T("btn.cancel");
                MakeButton(cancel, LWidth - 232, by, 100, 42);
                cancel.Click += delegate { Close(); };
            }
            UiButton go = new UiButton();
            go.Primary = true;
            go.Glyph = _danger ? "\uE7BA" : "\uE73E";
            go.Text = _okText;
            MakeButton(go, _onlyOk ? LWidth - 132 : LWidth - 122, by, 106, 42);
            go.Click += delegate
            {
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        protected override void PaintBody(Graphics g)
        {
            float y = 76;
            {
                Font f = Theme.Font(9.6f, FontStyle.Bold);
                Theme.DrawText(g, _danger ? Loc.T("map.dlg.dangerHead") : Loc.T("map.dlg.head"), f,
                    _danger ? Theme.Red : Theme.Text, new RectangleF(26, y, LWidth - 52, 24),
                    StringAlignment.Near, StringAlignment.Center);
            }
            y += 32;
            RectangleF card = new RectangleF(24, y, LWidth - 48, Math.Max(46, _lines.Length * 22 + 16));
            Theme.FillRounded(g, _danger ? Ui.Alpha(Theme.Red, 14) : Theme.Surface, card, 12f);
            Theme.StrokeRounded(g, _danger ? Ui.Alpha(Theme.Red, 60) : Theme.Border, card, 12f, 1f);
            for (int i = 0; i < _lines.Length; i++)
            {
                Font f = Theme.Font(8.8f, FontStyle.Regular);
                Theme.DrawText(g, _lines[i], f, _danger ? Theme.Text : Theme.Text,
                    new RectangleF(card.X + 14, card.Y + 8 + i * 22, card.Width - 28, 20),
                    StringAlignment.Near, StringAlignment.Center);
            }
        }
    }

    /// <summary>垃圾清理排查报告：只读分析 + 勾选分类后清理。</summary>
    public class JunkReportDialog : DialogBase
    {
        private readonly MapNode _node;
        private readonly FileOps.JunkReport _report;
        private readonly List<FileOps.JunkGroup> _groups = new List<FileOps.JunkGroup>();
        private readonly List<bool> _checked = new List<bool>();
        private int _hover = -1;
        public List<string> SelectedCatIds = new List<string>();
        public bool CleanRequested;

        public JunkReportDialog(MapNode node, FileOps.JunkReport report)
            : base(Loc.T("junk.title"), 640, 568)
        {
            _node = node;
            _report = report;
            for (int i = 0; i < report.Groups.Count; i++)
            {
                if (report.Groups[i].Bytes <= 0) continue;
                if (_groups.Count >= 5) break;
                _groups.Add(report.Groups[i]);
                _checked.Add(report.Groups[i].Risk == FileRisk.Safe);
            }

            UiButton close = new UiButton();
            close.Primary = false;
            close.Glyph = "";
            close.Text = Loc.T("btn.close");
            MakeButton(close, 396, 510, 100, 42);
            close.Click += delegate { Close(); };

            UiButton clean = new UiButton();
            clean.Primary = true;
            clean.Glyph = "\uE74D";
            clean.Text = Loc.T("junk.clean");
            MakeButton(clean, 508, 510, 106, 42);
            clean.Click += delegate
            {
                SelectedCatIds.Clear();
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_checked[i]) SelectedCatIds.Add(_groups[i].CatId);
                }
                if (SelectedCatIds.Count == 0) return;
                CleanRequested = true;
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        private RectangleF GroupCard(int i)
        {
            return new RectangleF(26, 214 + i * 26, LWidth - 52, 24);
        }

        private int HitGroup(Point p)
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                if (GroupCard(i).Contains(p.X, p.Y)) return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitGroup(LogicalPoint(e));
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
            if (_hover != -1)
            {
                _hover = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int idx = HitGroup(LogicalPoint(e));
            if (idx < 0) return;
            _checked[idx] = !_checked[idx];
            Invalidate();
        }

        private long SelectedBytes()
        {
            long b = 0;
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_checked[i]) b += _groups[i].Bytes;
            }
            return b;
        }

        protected override void PaintBody(Graphics g)
        {
            {
                Font f = Theme.Font(11f, FontStyle.Bold);
                Theme.DrawText(g, _node.Name, f, Theme.Text, new RectangleF(26, 76, 380, 24),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font f = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, _node.Path, f, Theme.Muted, new RectangleF(26, 100, LWidth - 52, 16),
                    StringAlignment.Near, StringAlignment.Center);
            }

            DrawStat(g, 22, 126, Loc.T("junk.total"), Theme.Bytes(_report.Total), Theme.Blue);
            DrawStat(g, 170, 126, Loc.T("junk.reclaim"), Theme.Bytes(_report.Reclaim), Theme.Green);
            DrawStat(g, 318, 126, Loc.T("junk.notCleanable"), Theme.Bytes(_report.Risky), Theme.Red);
            DrawStat(g, 466, 126, Loc.T("junk.forbidden"), Theme.Bytes(_report.Forbidden),
                MapRisk.Base(FileRisk.Forbidden));

            RectangleF card = new RectangleF(24, 166, LWidth - 48, 204);
            Theme.FillRounded(g, Theme.Surface, card, 12f);
            Theme.StrokeRounded(g, Theme.Border, card, 12f, 1f);
            {
                Font f = Theme.Font(8.6f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("junk.cleanableTitle"), f, Theme.Text,
                    new RectangleF(card.X + 14, card.Y + 8, 320, 20), StringAlignment.Near,
                    StringAlignment.Center);
                Theme.DrawText(g, Loc.F("junk.selected", Theme.Bytes(SelectedBytes())), f, Theme.Blue,
                    new RectangleF(card.Right - 214, card.Y + 8, 200, 20), StringAlignment.Far,
                    StringAlignment.Center);
            }
            if (_groups.Count == 0)
            {
                Font f = Theme.Font(8.6f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("junk.none"), f, Theme.SubText,
                    new RectangleF(card.X + 14, GroupCard(0).Y, card.Width - 28, 24),
                    StringAlignment.Near, StringAlignment.Center);
            }
            for (int i = 0; i < _groups.Count; i++)
            {
                RectangleF row = GroupCard(i);
                if (i == _hover) Theme.FillRounded(g, Theme.Hover, row, 8f);
                RectangleF box = new RectangleF(row.X + 12, row.Y + 5.5f, 14, 14);
                Theme.FillRounded(g, _checked[i] ? Theme.Blue : Theme.Card, box, 4f);
                if (!_checked[i]) Theme.StrokeRounded(g, Theme.BorderStrong, box, 4f, 1f);
                else
                {
                    using (Pen p = new Pen(Color.White, 1.2f))
                    {
                        g.DrawLine(p, box.X + 3, box.Y + 7, box.X + 6, box.Y + 10);
                        g.DrawLine(p, box.X + 6, box.Y + 10, box.X + 11, box.Y + 4);
                    }
                }
                using (SolidBrush b = new SolidBrush(MapRisk.Base(_groups[i].Risk)))
                {
                    g.FillEllipse(b, row.X + 34, row.Y + 9, 7, 7);
                }
                {
                    Font f = Theme.Font(8.4f, FontStyle.Bold);
                    Theme.DrawText(g, Loc.CatName(_groups[i].CatId), f, Theme.Text,
                        new RectangleF(row.X + 46, row.Y, 240, row.Height), StringAlignment.Near,
                        StringAlignment.Center);
                }
                {
                    Font f = Theme.Font(7.8f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.F("junk.fileCount", _groups[i].Files), f, Theme.SubText,
                        new RectangleF(row.X + 296, row.Y, 90, row.Height), StringAlignment.Near,
                        StringAlignment.Center);
                }
                {
                    Font f = Theme.Font(8.4f, FontStyle.Bold);
                    Theme.DrawText(g, Theme.Bytes(_groups[i].Bytes), f, Theme.Text,
                        new RectangleF(row.Right - 100, row.Y, 84, row.Height), StringAlignment.Far,
                        StringAlignment.Center);
                }
            }
            if (_report.Groups.Count > _groups.Count)
            {
                Font f = Theme.Font(7.6f, FontStyle.Regular);
                Theme.DrawText(g, Loc.F("junk.more", _report.Groups.Count - _groups.Count), f, Theme.Muted,
                    new RectangleF(card.X + 14, card.Bottom - 22, 300, 18), StringAlignment.Near,
                    StringAlignment.Center);
            }

            RectangleF largest = new RectangleF(24, 380, LWidth - 48, 118);
            Theme.FillRounded(g, Theme.Card, largest, 12f);
            Theme.StrokeRounded(g, Theme.Border, largest, 12f, 1f);
            {
                Font f = Theme.Font(8.6f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("junk.largest"), f, Theme.Text,
                    new RectangleF(largest.X + 14, largest.Y + 6, 260, 18), StringAlignment.Near,
                    StringAlignment.Center);
            }
            int shown = 0;
            for (int i = 0; i < _report.Largest.Count && shown < 4; i++, shown++)
            {
                Font f = Theme.Font(7.6f, FontStyle.Regular);
                Theme.DrawText(g, _report.Largest[i].Path, f, Theme.SubText,
                    new RectangleF(largest.X + 14, largest.Y + 28 + shown * 19, largest.Width - 150, 18),
                    StringAlignment.Near, StringAlignment.Center);
                Font vf = Theme.Font(7.6f, FontStyle.Bold);
                Theme.DrawText(g, Theme.Bytes(_report.Largest[i].Size), vf, Theme.Text,
                    new RectangleF(largest.Right - 120, largest.Y + 28 + shown * 19, 106, 18),
                    StringAlignment.Far, StringAlignment.Center);
            }

            {
                Font f = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("junk.readonly"), f, Theme.Amber,
                    new RectangleF(26, 502, 340, 18), StringAlignment.Near, StringAlignment.Center);
            }
        }

        private void DrawStat(Graphics g, float x, float y, string label, string value, Color color)
        {
            RectangleF r = new RectangleF(x, y, 146, 30);
            Theme.FillRounded(g, Ui.Alpha(color, 14), r, 8f);
            using (SolidBrush b = new SolidBrush(color))
            {
                g.FillEllipse(b, r.X + 9, r.Y + 12, 6, 6);
            }
            {
                Font f = Theme.Font(7.2f, FontStyle.Regular);
                Theme.DrawText(g, label, f, Theme.SubText, new RectangleF(r.X + 18, r.Y, 70, 30),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font f = Theme.Font(7.8f, FontStyle.Bold);
                Theme.DrawText(g, value, f, Theme.Text, new RectangleF(r.X + 56, r.Y, 84, 30),
                    StringAlignment.Far, StringAlignment.Center);
            }
        }
    }
}
