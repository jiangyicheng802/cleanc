using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace CleanC
{
    public abstract class DialogBase : Form
    {
        protected readonly string Headline;
        protected readonly int LWidth;
        protected readonly int LHeight;
        private RectangleF _closeRect;
        private bool _closeHover;

        protected DialogBase(string title, int width, int height)
        {
            Headline = title;
            LWidth = width;
            LHeight = height;
            Text = title;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(Px(width), Px(height));
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            ImeMode = ImeMode.Disable;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            _closeRect = new RectangleF(width - 52, 14, 34, 34);
        }

        protected static int Px(float v)
        {
            return (int)Math.Round(v * Theme.Scale);
        }

        protected static Rectangle PxRect(int x, int y, int w, int h)
        {
            return new Rectangle(Px(x), Px(y), Px(w), Px(h));
        }

        protected Point LogicalPoint(MouseEventArgs e)
        {
            return new Point((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
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
            Native.Blend(Handle, 180);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.Clear(Theme.Bg);
            g.ScaleTransform(Theme.Scale, Theme.Scale);
            {
                Font f = Theme.Font(13f, FontStyle.Bold);
                Theme.DrawText(g, Headline, f, Theme.Text, new RectangleF(26, 18, LWidth - 96, 30),
                    StringAlignment.Near, StringAlignment.Center);
            }
            if (_closeHover)
            {
                Theme.FillRounded(g, Theme.Hover, _closeRect, 10f);
            }
            using (Pen p = new Pen(_closeHover ? Theme.Blue : Theme.Muted, 1.3f))
            {
                float x = _closeRect.X + 11;
                float y = _closeRect.Y + 11;
                g.DrawLine(p, x, y, x + 12, y + 12);
                g.DrawLine(p, x + 12, y, x, y + 12);
            }
            PaintBody(g);
        }

        protected abstract void PaintBody(Graphics g);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point lp = LogicalPoint(e);
            bool hover = _closeRect.Contains(lp.X, lp.Y);
            if (hover != _closeHover)
            {
                _closeHover = hover;
                Cursor = hover ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_closeHover)
            {
                _closeHover = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Point lp = LogicalPoint(e);
            if (_closeRect.Contains(lp.X, lp.Y))
            {
                Close();
                return;
            }
            if (lp.Y < 60)
            {
                Native.Drag(Handle);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
        }

        protected void MakeButton(UiButton b, int x, int y, int w, int h)
        {
            b.Bounds = PxRect(x, y, w, h);
            b.BackColor = Theme.Bg;
            Controls.Add(b);
        }
    }

    public class ConfirmDialog : DialogBase
    {
        private readonly List<Category> _cats;
        private readonly long _total;
        private readonly long _permanent;
        private readonly long _recycled;
        private readonly bool _hasOld;
        private readonly bool _hasJunk;

        public ConfirmDialog(List<Category> cats) : base(Loc.T("dlg.confirm"), 540, 438)
        {
            _cats = cats;
            long total = 0;
            long permanent = 0;
            long recycled = 0;
            bool hasOld = false;
            bool hasJunk = false;
            for (int i = 0; i < cats.Count; i++)
            {
                Category c = cats[i];
                total += c.Bytes;
                if (c.Registry) hasJunk = true;
                else if (c.Mode == CleanMode.Recycle) recycled += c.Bytes;
                else permanent += c.Bytes;
                if (c.Id == "windows_old") hasOld = true;
            }
            _total = total;
            _permanent = permanent;
            _recycled = recycled;
            _hasOld = hasOld;
            _hasJunk = hasJunk;
            UiButton cancel = new UiButton();
            cancel.Primary = false;
            cancel.Text = Loc.T("btn.cancel");
            cancel.Glyph = "";
            MakeButton(cancel, 300, 376, 100, 42);
            cancel.Click += delegate { Close(); };

            UiButton go = new UiButton();
            go.Primary = true;
            go.Glyph = "\uE74D";
            go.Text = hasJunk ? Loc.T("confirm.startJunk") : Loc.T("btn.startClean");
            MakeButton(go, 412, 376, 104, 42);
            go.Click += delegate
            {
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        protected override void PaintBody(Graphics g)
        {
            {
                Font f = Theme.Font(12f, FontStyle.Bold);
                Theme.DrawText(g, Loc.F("confirm.summary", _cats.Count, Theme.Bytes(_total)), f,
                    Theme.Text, new RectangleF(26, 78, LWidth - 52, 26), StringAlignment.Near,
                    StringAlignment.Center);
            }

            RectangleF card = new RectangleF(24, 112, LWidth - 48, 172);
            Theme.FillRounded(g, Theme.Surface, card, 12f);
            Theme.StrokeRounded(g, Theme.Border, card, 12f, 1f);

            int shown = Math.Min(4, _cats.Count);
            float y = card.Y + 8;
            for (int i = 0; i < shown; i++)
            {
                Category c = _cats[i];
                {
                    Font nf = Theme.Font(9.5f, FontStyle.Bold);
                    Theme.DrawText(g, c.Name, nf, Theme.Text,
                        new RectangleF(card.X + 16, y, 230, 32), StringAlignment.Near, StringAlignment.Center);
                }
                string mode;
                Color modeColor;
                if (c.Registry)
                {
                    mode = Loc.F("reg.itemCount", c.RegItems.Count);
                    modeColor = Theme.Rose;
                }
                else
                {
                    mode = c.Mode == CleanMode.Recycle ? Loc.T("confirm.recycle")
                        : (c.Mode == CleanMode.EmptyRecycle ? Loc.T("confirm.emptyRecycle") : Loc.T("confirm.permanent"));
                    modeColor = c.Mode == CleanMode.Recycle ? Theme.Blue : (c.Risk == RiskLevel.Caution ? Theme.Red : Theme.Gray);
                }
                {
                    Font mf = Theme.Font(8.2f, FontStyle.Regular);
                    Theme.DrawText(g, mode, mf, modeColor,
                        new RectangleF(card.X + 250, y, 140, 32), StringAlignment.Near, StringAlignment.Center);
                }
                {
                    Font sf = Theme.Font(9f, FontStyle.Bold);
                    string value = c.Registry ? Loc.F("reg.itemCount", c.RegItems.Count) : Theme.Bytes(c.Bytes);
                    Theme.DrawText(g, value, sf, Theme.Text,
                        new RectangleF(card.Right - 150, y, 134, 32), StringAlignment.Far, StringAlignment.Center);
                }
                y += 34;
            }
            if (_cats.Count > shown)
            {
                {
                    Font mf = Theme.Font(8.5f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.F("confirm.more", _cats.Count - shown), mf, Theme.SubText,
                        new RectangleF(card.X + 16, card.Y + 148, 260, 18), StringAlignment.Near, StringAlignment.Center);
                }
            }

            RectangleF note = new RectangleF(24, 296, LWidth - 48, 62);
            Color noteColor = _hasOld ? Theme.Amber : (_hasJunk ? Theme.Rose : Theme.Blue);
            Theme.FillRounded(g, Ui.Alpha(noteColor, 18), note, 12f);
            using (SolidBrush b = new SolidBrush(Theme.Mix(noteColor, Color.Black, 0.15f)))
            {
                Font nf = Theme.Font(8.6f, FontStyle.Regular);
                string text;
                if (_hasJunk) text = Loc.T("confirm.noteJunk");
                else if (_hasOld) text = Loc.T("confirm.noteOld");
                else text = Loc.T("confirm.note");
                using (StringFormat sf = new StringFormat())
                {
                    sf.Trimming = StringTrimming.EllipsisWord;
                    g.DrawString(text, nf, b, new RectangleF(note.X + 14, note.Y + 11, note.Width - 28, note.Height - 18), sf);
                }
            }

            {
                Font sf = Theme.Font(8.2f, FontStyle.Regular);
                Theme.DrawText(g, Loc.F("confirm.totals", Theme.Bytes(_permanent), Theme.Bytes(_recycled)),
                    sf, Theme.SubText, new RectangleF(26, 366, 280, 20), StringAlignment.Near, StringAlignment.Center);
            }
        }
    }

    /// <summary>通用免责声明 / 权限确认弹窗，必须勾选才能继续。</summary>
    public class ConsentDialog : DialogBase
    {
        private readonly string _lead;
        private readonly string[] _items;
        private readonly string _warning;
        private readonly string _checkText;
        private readonly bool _danger;
        private readonly float _checkY;
        private RectangleF _checkRect;
        private bool _checked;
        private bool _hoverCheck;
        private UiButton _ok;

        private static int HeightFor(int items)
        {
            return 278 + items * 30;
        }

        public ConsentDialog(string headline, string lead, string[] items, string warning,
            string checkText, string confirmText, bool danger)
            : base(headline, 620, HeightFor(items == null ? 0 : items.Length))
        {
            _lead = lead;
            _items = items == null ? new string[0] : items;
            _warning = warning;
            _checkText = checkText;
            _danger = danger;

            int n = _items.Length;
            _checkY = 104 + n * 30 + 6 + 64 + 14;
            _checkRect = new RectangleF(30, _checkY, 18, 18);

            UiButton cancel = new UiButton();
            cancel.Primary = false;
            cancel.Text = Loc.T("btn.cancel");
            MakeButton(cancel, 358, LHeight - 56, 110, 42);
            cancel.Click += delegate { Close(); };

            _ok = new UiButton();
            _ok.Primary = true;
            _ok.Danger = danger;
            _ok.Text = confirmText;
            _ok.Enabled = false;
            MakeButton(_ok, 480, LHeight - 56, 112, 42);
            _ok.Click += delegate
            {
                if (!_checked) return;
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        protected override void PaintBody(Graphics g)
        {
            Color tint = _danger ? Theme.Rose : Theme.Blue;
            {
                Font f = Theme.Font(9.4f, FontStyle.Regular);
                Theme.DrawText(g, _lead, f, Theme.SubText, new RectangleF(30, 74, LWidth - 60, 20),
                    StringAlignment.Near, StringAlignment.Center);
            }

            float y = 104;
            for (int i = 0; i < _items.Length; i++)
            {
                using (SolidBrush b = new SolidBrush(Ui.Alpha(tint, 24)))
                {
                    g.FillEllipse(b, 31, y + 9, 7, 7);
                }
                {
                    Font f = Theme.Font(9f, FontStyle.Regular);
                    Theme.DrawText(g, _items[i], f, Theme.Text,
                        new RectangleF(48, y, LWidth - 80, 26), StringAlignment.Near, StringAlignment.Center);
                }
                y += 30;
            }

            RectangleF warn = new RectangleF(28, 104 + _items.Length * 30 + 6, LWidth - 56, 64);
            Theme.FillRounded(g, Ui.Alpha(tint, 16), warn, 12f);
            Theme.StrokeRounded(g, Ui.Alpha(tint, 40), warn, 12f, 1f);
            using (SolidBrush b = new SolidBrush(tint))
            {
                Font gf = Theme.IconFont(13f);
                g.DrawString("\uE7BA", gf, b, warn.X + 14, warn.Y + 12);
            }
            using (SolidBrush b = new SolidBrush(Theme.Mix(tint, Color.Black, 0.25f)))
            using (StringFormat sf = new StringFormat())
            {
                Font f = Theme.Font(8.4f, FontStyle.Regular);
                sf.Trimming = StringTrimming.EllipsisWord;
                g.DrawString(_warning, f, b, new RectangleF(warn.X + 40, warn.Y + 10, warn.Width - 56, warn.Height - 16), sf);
            }

            RectangleF box = _checkRect;
            if (_checked)
            {
                Theme.FillRoundedGradient(g, tint, Theme.Mix(tint, Color.White, 0.35f),
                    new RectangleF(box.X, box.Y, 18, 18), 5.5f);
                using (Pen p = new Pen(Color.White, 1.7f))
                {
                    g.DrawLine(p, box.X + 4, box.Y + 9.5f, box.X + 7.4f, box.Y + 13f);
                    g.DrawLine(p, box.X + 7.4f, box.Y + 13f, box.X + 13.6f, box.Y + 5.6f);
                }
            }
            else
            {
                Theme.FillRounded(g, Theme.Card, new RectangleF(box.X, box.Y, 18, 18), 5.5f);
                Theme.StrokeRounded(g, _hoverCheck ? tint : Theme.BorderStrong,
                    new RectangleF(box.X + 0.5f, box.Y + 0.5f, 17, 17), 5.5f, 1.3f);
            }
            {
                Font f = Theme.Font(8.7f, FontStyle.Regular);
                Theme.DrawText(g, _checkText, f, Theme.Text,
                    new RectangleF(box.X + 26, _checkY - 2, LWidth - 80, 24), StringAlignment.Near, StringAlignment.Center);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point lp = LogicalPoint(e);
            RectangleF hit = new RectangleF(_checkRect.X - 6, _checkRect.Y - 8, _checkRect.Width + 560, _checkRect.Height + 16);
            bool h = hit.Contains(lp.X, lp.Y);
            if (h != _hoverCheck)
            {
                _hoverCheck = h;
                Cursor = h ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Point lp = LogicalPoint(e);
            RectangleF hit = new RectangleF(_checkRect.X - 6, _checkRect.Y - 8, _checkRect.Width + 560, _checkRect.Height + 16);
            if (!hit.Contains(lp.X, lp.Y)) return;
            _checked = !_checked;
            _ok.Enabled = _checked;
            Invalidate();
        }
    }

    /// <summary>卸载确认：列出目录、注册表项、将执行命令与备份位置。</summary>
    public class UninstallDialog : DialogBase
    {
        private readonly ProgramEntry _p;
        private readonly bool _force;
        private readonly List<Uninstaller.RegTarget> _regs;
        private bool _checked;
        private bool _hoverCheck;
        private RectangleF _checkRect;
        private UiButton _ok;

        public UninstallDialog(ProgramEntry p, bool force)
            : base(force ? Loc.T("dlg.forceUninstall") : Loc.T("dlg.uninstall"), 620, force ? 520 : 448)
        {
            _p = p;
            _force = force;
            _regs = force ? Uninstaller.RegistryTargets(p) : new List<Uninstaller.RegTarget>();

            _checkRect = new RectangleF(30, force ? 420 : 348, 18, 18);

            UiButton cancel = new UiButton();
            cancel.Primary = false;
            cancel.Text = Loc.T("btn.cancel");
            MakeButton(cancel, 354, LHeight - 56, 110, 42);
            cancel.Click += delegate { Close(); };

            _ok = new UiButton();
            _ok.Primary = true;
            _ok.Danger = force;
            _ok.Text = force ? Loc.T("uninstall.btnForce") : Loc.T("uconfirm.ok");
            _ok.Enabled = false;
            MakeButton(_ok, 476, LHeight - 56, 116, 42);
            _ok.Click += delegate
            {
                if (!_checked) return;
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        protected override void PaintBody(Graphics g)
        {
            Color tint = _force ? Theme.Rose : Theme.Blue;

            RectangleF card = new RectangleF(26, 74, LWidth - 52, 62);
            Theme.FillRounded(g, Theme.Surface, card, 12f);
            Theme.StrokeRounded(g, Theme.Border, card, 12f, 1f);
            RectangleF icon = new RectangleF(card.X + 14, card.Y + 11, 40, 40);
            Theme.FillRounded(g, Ui.Alpha(_p.Accent, 18), icon, 12f);
            using (SolidBrush b = new SolidBrush(_p.Accent))
            using (StringFormat sf = new StringFormat())
            {
                Font gf = Theme.Font(13f, FontStyle.Bold);
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(_p.Glyph, gf, b, icon, sf);
            }
            {
                Font nf = Theme.Font(11f, FontStyle.Bold);
                Theme.DrawText(g, _p.Name, nf, Theme.Text, new RectangleF(card.X + 64, card.Y + 8, card.Width - 90, 22),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font sf = Theme.Font(8.2f, FontStyle.Regular);
                Theme.DrawText(g, _p.SubText + " · " + _p.SizeText, sf, Theme.SubText,
                    new RectangleF(card.X + 64, card.Y + 32, card.Width - 90, 20), StringAlignment.Near, StringAlignment.Center);
            }

            {
                Font lf = Theme.Font(8f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("uconfirm.path"), lf, Theme.Muted, new RectangleF(30, 148, 100, 18),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font pf = Theme.Font(8.6f, FontStyle.Regular);
                Theme.DrawText(g, _p.InstallDir.Length > 0 ? _p.InstallDir : Loc.T("uninstall.noPath"), pf, Theme.Text,
                    new RectangleF(30, 166, LWidth - 60, 20), StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font sf = Theme.Font(8f, FontStyle.Regular);
                string files = Loc.F("uconfirm.files", Theme.Count(_p.FileCount), Theme.Bytes(_p.Bytes));
                Theme.DrawText(g, files, sf, Theme.SubText, new RectangleF(30, 188, 300, 18),
                    StringAlignment.Near, StringAlignment.Center);
            }

            {
                Font lf = Theme.Font(8f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("uconfirm.registry"), lf, Theme.Muted, new RectangleF(30, 212, 100, 18),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font rf = Theme.Font(8f, FontStyle.Regular);
                if (!_force)
                {
                    Theme.DrawText(g, Loc.T("uconfirm.registryNone"), rf, Theme.SubText,
                        new RectangleF(30, 230, LWidth - 60, 18), StringAlignment.Near, StringAlignment.Center);
                }
                else
                {
                    if (_regs.Count == 0)
                    {
                        Theme.DrawText(g, Loc.T("uconfirm.registryEmpty"), rf, Theme.SubText,
                            new RectangleF(30, 230, LWidth - 60, 18), StringAlignment.Near, StringAlignment.Center);
                    }
                    else
                    {
                        for (int i = 0; i < _regs.Count && i < 2; i++)
                        {
                            Theme.DrawText(g, ShortKey(_regs[i]), rf, Theme.SubText,
                                new RectangleF(30, 230 + i * 16, LWidth - 60, 16), StringAlignment.Near, StringAlignment.Center);
                        }
                        if (_regs.Count > 2)
                        {
                            Theme.DrawText(g, Loc.F("uconfirm.regMore", _regs.Count - 2), rf, Theme.Muted,
                                new RectangleF(30, 230 + 2 * 16, LWidth - 60, 16), StringAlignment.Near, StringAlignment.Center);
                        }
                    }
                }
            }

            if (_force)
            {
                {
                    Font lf = Theme.Font(8f, FontStyle.Bold);
                    Theme.DrawText(g, Loc.T("uconfirm.commands"), lf, Theme.Muted, new RectangleF(30, 286, 100, 18),
                        StringAlignment.Near, StringAlignment.Center);
                }
                {
                    Font mf = Theme.MonoFont(7.8f);
                    Color mc = Theme.Mix(Theme.Text, Theme.Muted, 0.3f);
                    Theme.DrawTextNoTrim(g, "taskkill /F /PID <pid>", mf, mc, new RectangleF(30, 304, LWidth - 60, 15),
                        StringAlignment.Near, StringAlignment.Center);
                    Theme.DrawTextNoTrim(g, "takeown /F \"<dir>\" /R /D Y", mf, mc, new RectangleF(30, 319, LWidth - 60, 15),
                        StringAlignment.Near, StringAlignment.Center);
                    Theme.DrawTextNoTrim(g, "reg export <key> <backup.reg> /Y  →  reg delete <key> /F", mf, mc,
                        new RectangleF(30, 334, LWidth - 60, 15), StringAlignment.Near, StringAlignment.Center);
                }
            }

            RectangleF warn = new RectangleF(26, _force ? 358 : 224, LWidth - 52, 52);
            Theme.FillRounded(g, Ui.Alpha(tint, 16), warn, 12f);
            using (SolidBrush b = new SolidBrush(tint))
            {
                Font gf = Theme.IconFont(13f);
                g.DrawString("\uE7BA", gf, b, warn.X + 14, warn.Y + 14);
            }
            using (SolidBrush b = new SolidBrush(Theme.Mix(tint, Color.Black, 0.25f)))
            using (StringFormat sf = new StringFormat())
            {
                Font f = Theme.Font(8.4f, FontStyle.Regular);
                sf.Trimming = StringTrimming.EllipsisWord;
                g.DrawString(_force ? Loc.T("uconfirm.warnForce") : Loc.T("uconfirm.warnNormal"), f, b,
                    new RectangleF(warn.X + 40, warn.Y + 8, warn.Width - 56, warn.Height - 14), sf);
            }

            RectangleF box = _checkRect;
            if (_checked)
            {
                Theme.FillRoundedGradient(g, tint, Theme.Mix(tint, Color.White, 0.35f),
                    new RectangleF(box.X, box.Y, 18, 18), 5.5f);
                using (Pen p = new Pen(Color.White, 1.7f))
                {
                    g.DrawLine(p, box.X + 4, box.Y + 9.5f, box.X + 7.4f, box.Y + 13f);
                    g.DrawLine(p, box.X + 7.4f, box.Y + 13f, box.X + 13.6f, box.Y + 5.6f);
                }
            }
            else
            {
                Theme.FillRounded(g, Theme.Card, new RectangleF(box.X, box.Y, 18, 18), 5.5f);
                Theme.StrokeRounded(g, _hoverCheck ? tint : Theme.BorderStrong,
                    new RectangleF(box.X + 0.5f, box.Y + 0.5f, 17, 17), 5.5f, 1.3f);
            }
            {
                Font f = Theme.Font(8.7f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("uconfirm.check"), f, Theme.Text,
                    new RectangleF(box.X + 26, _checkRect.Y - 2, LWidth - 80, 24), StringAlignment.Near, StringAlignment.Center);
            }
        }

        private static string ShortKey(Uninstaller.RegTarget t)
        {
            string full = t.Hive + "\\" + t.Path;
            if (full.Length > 96) return full.Substring(0, 46) + " … " + full.Substring(full.Length - 44);
            return full;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point lp = LogicalPoint(e);
            RectangleF hit = new RectangleF(_checkRect.X - 6, _checkRect.Y - 8, _checkRect.Width + 540, _checkRect.Height + 16);
            bool h = hit.Contains(lp.X, lp.Y);
            if (h != _hoverCheck)
            {
                _hoverCheck = h;
                Cursor = h ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Point lp = LogicalPoint(e);
            RectangleF hit = new RectangleF(_checkRect.X - 6, _checkRect.Y - 8, _checkRect.Width + 540, _checkRect.Height + 16);
            if (!hit.Contains(lp.X, lp.Y)) return;
            _checked = !_checked;
            _ok.Enabled = _checked;
            Invalidate();
        }
    }

    public class ResultDialog : DialogBase
    {
        private readonly CleanResult _result;
        private readonly string _keyCheck;
        private readonly string _keyCount;
        private float _checkProgress;
        private float _shown;
        private bool _logHover;
        private RectangleF _logRect;
        private long _baseFree;
        private readonly string _bodyTitle;
        private readonly string _bodySub;

        public ResultDialog(CleanResult result, long baseFree)
            : this(result, baseFree, null, null)
        {
        }

        public ResultDialog(CleanResult result, long baseFree, string bodyTitle, string bodySub)
            : base(bodyTitle == null ? Loc.T("dlg.result") : bodyTitle, 460, 424)
        {
            _result = result;
            _baseFree = baseFree;
            _bodyTitle = bodyTitle;
            _bodySub = bodySub;
            _keyCheck = Ui.Key("resCheck");
            _keyCount = Ui.Key("resCount");
            _logRect = new RectangleF(28, result.RegKeys + result.RegValues > 0 ? 340 : 336, 220, 22);

            UiButton done = new UiButton();
            done.Primary = true;
            done.Text = Loc.T("btn.done");
            MakeButton(done, 286, 358, 144, 44);
            done.Click += delegate { Close(); };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Anim.Tween(_keyCheck, 0f, 1f, 700, delegate(float v) { _checkProgress = v; Invalidate(); }, null);
            float target = (float)_result.FreedBytes;
            if (target <= 0 && _result.RecycledBytes > 0) target = (float)_result.RecycledBytes;
            Anim.Tween(_keyCount, 0f, target, 800, delegate(float v) { _shown = v; Invalidate(); }, null);
        }

        protected override void PaintBody(Graphics g)
        {
            float cx = LWidth / 2f;
            float e = Anim.EaseOutBack(Math.Min(1f, _checkProgress));
            float r = 44f * (0.75f + 0.25f * e);
            RectangleF circle = new RectangleF(cx - r, 84 - r, r * 2, r * 2);
            if (_checkProgress > 0.02f)
            {
                Theme.FillRoundedGradient(g, Theme.Blue, Theme.Cyan, circle, r);
                using (Pen p = new Pen(Color.White, 3.2f))
                {
                    float k = Math.Min(1f, Math.Max(0f, (_checkProgress - 0.25f) / 0.55f));
                    float x0 = circle.X + r * 0.45f;
                    float y0 = circle.Y + r * 0.55f;
                    float x1 = circle.X + r * 0.72f;
                    float y1 = circle.Y + r * 0.82f;
                    float x2 = circle.X + r * 1.45f;
                    float y2 = circle.Y + r * 0.4f;
                    if (k < 0.5f)
                    {
                        float t = k / 0.5f;
                        g.DrawLine(p, x0, y0, x0 + (x1 - x0) * t, y0 + (y1 - y0) * t);
                    }
                    else
                    {
                        g.DrawLine(p, x0, y0, x1, y1);
                        float t = (k - 0.5f) / 0.5f;
                        g.DrawLine(p, x1, y1, x1 + (x2 - x1) * t, y1 + (y2 - y1) * t);
                    }
                }
            }

            {
                Font tf = Theme.Font(15f, FontStyle.Bold);
                string head = _result.Canceled ? Loc.T("result.canceled")
                    : (_bodyTitle == null ? Loc.T("result.title") : _bodyTitle);
                Theme.DrawText(g, head, tf, Theme.Text,
                    new RectangleF(0, 138, LWidth, 30), StringAlignment.Center, StringAlignment.Center);
            }

            string freed = "+ " + Theme.Bytes((long)_shown);
            {
                Font bf = Theme.Font(23f, FontStyle.Bold);
                Theme.DrawText(g, freed, bf, Theme.Blue,
                    new RectangleF(0, 174, LWidth, 40), StringAlignment.Center, StringAlignment.Center);
            }
            {
                Font sf = Theme.Font(8.5f, FontStyle.Regular);
                string sub;
                if (_bodySub != null)
                {
                    sub = _bodySub;
                }
                else if (_result.FreedBytes > 0)
                {
                    sub = Loc.T("result.subFreed") + (_result.RecycledBytes > 0
                        ? Loc.F("result.subRecycled", Theme.Bytes(_result.RecycledBytes)) : "");
                }
                else if (_result.RecycledBytes > 0)
                {
                    sub = Loc.T("result.subRecycleOnly");
                }
                else
                {
                    sub = Loc.T("result.subNone");
                }
                if (_bodySub == null && _result.RegKeys + _result.RegValues > 0)
                {
                    sub += Loc.F("result.subRegistry", _result.RegKeys + _result.RegValues);
                }
                Theme.DrawText(g, sub, sf, Theme.SubText,
                    new RectangleF(0, 214, LWidth, 20), StringAlignment.Center, StringAlignment.Center);
            }

            RectangleF stats = new RectangleF(34, 242, LWidth - 68, 74);
            Theme.FillRounded(g, Theme.Sunken, stats, 12f);
            Theme.StrokeRounded(g, Theme.Border, stats, 12f, 1f);
            DrawStat(g, stats.X + 14, stats.Y + 12, Loc.T("result.statFreed"), Theme.Bytes(_result.FreedBytes), Theme.Green);
            DrawStat(g, stats.X + stats.Width / 2 + 8, stats.Y + 12, Loc.T("result.statRecycled"), Theme.Bytes(_result.RecycledBytes), Theme.Violet);
            DrawStat(g, stats.X + 14, stats.Y + 44, Loc.T("result.statDeleted"), Theme.Count(_result.Deleted) + " " + Loc.T("unit.items"), Theme.Text);
            DrawStat(g, stats.X + stats.Width / 2 + 8, stats.Y + 44, _result.RegKeys + _result.RegValues > 0
                    ? Loc.T("result.statRegistry") : Loc.T("result.statSkipped"),
                _result.RegKeys + _result.RegValues > 0
                    ? Theme.Count(_result.RegKeys + _result.RegValues)
                    : Theme.Count(_result.Skipped) + " " + Loc.T("unit.items"),
                _result.RegKeys + _result.RegValues > 0 ? Theme.Rose : (_result.Skipped > 0 ? Theme.Amber : Theme.Muted));

            if (!string.IsNullOrEmpty(_result.BackupDir))
            {
                {
                    Font bf = Theme.Font(7.4f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.F("result.backup", _result.BackupDir), bf, Theme.Muted,
                        new RectangleF(28, 320, LWidth - 56, 16), StringAlignment.Near, StringAlignment.Center);
                }
            }

            {
                Font lf = Theme.Font(8.3f, FontStyle.Regular);
                SizeF tw = TextRenderer.MeasureText(Loc.T("result.viewLog"), lf);
                Theme.DrawText(g, Loc.T("result.viewLog"), lf, _logHover ? Theme.Blue : Theme.SubText,
                    _logRect, StringAlignment.Near, StringAlignment.Center);
                using (Pen p = new Pen(_logHover ? Theme.Blue : Theme.Muted, 1f))
                {
                    g.DrawLine(p, _logRect.X, _logRect.Y + 20, _logRect.X + tw.Width, _logRect.Y + 20);
                }
            }
        }

        private void DrawStat(Graphics g, float x, float y, string label, string value, Color color)
        {
            {
                Font lf = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, label, lf, Theme.SubText, new RectangleF(x, y, 100, 16),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font vf = Theme.Font(9.2f, FontStyle.Bold);
                Theme.DrawText(g, value, vf, color, new RectangleF(x + 92, y, 140, 16),
                    StringAlignment.Near, StringAlignment.Center);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point lp = LogicalPoint(e);
            bool h = _logRect.Contains(lp.X, lp.Y);
            if (h != _logHover)
            {
                _logHover = h;
                Cursor = h ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Point lp = LogicalPoint(e);
            if (_logRect.Contains(lp.X, lp.Y)) Native.OpenLog(_result.LogPath);
        }
    }

    public class DetailDialog : DialogBase
    {
        private readonly Category _cat;
        private readonly FileListView _list;
        private readonly string _folder;

        public DetailDialog(Category cat) : base(Loc.T("dlg.detail"), 820, 600)
        {
            _cat = cat;
            _folder = FindFolder(cat);
            _list = new FileListView();
            _list.Bounds = PxRect(28, 244, 764, 268);
            List<FileEntry> files = cat.Registry ? MapRegistry(cat) : cat.Files;
            _list.SetFiles(files, 800);
            _list.Visible = files.Count > 0;
            Controls.Add(_list);

            UiButton close = new UiButton();
            close.Primary = true;
            close.Text = Loc.T("btn.close");
            MakeButton(close, 674, 536, 118, 42);
            close.Click += delegate { Close(); };

            UiButton open = new UiButton();
            open.Primary = false;
            open.Text = Loc.T("btn.openFolder");
            open.Glyph = "\uE8B7";
            MakeButton(open, 28, 536, 196, 42);
            open.Enabled = _folder.Length > 0;
            open.Click += delegate
            {
                if (_folder.Length > 0) Native.OpenFolder(_folder);
            };
        }

        private static List<FileEntry> MapRegistry(Category cat)
        {
            List<FileEntry> list = new List<FileEntry>();
            for (int i = 0; i < cat.RegItems.Count; i++)
            {
                RegItem it = cat.RegItems[i];
                string key = it.FullKey + (it.DeleteKey ? "" : "\\" + it.ValueName);
                list.Add(new FileEntry(key, 0, it.Label, it.Reason));
            }
            return list;
        }

        private static string FindFolder(Category cat)
        {
            for (int i = 0; i < cat.Roots.Count; i++)
            {
                if (Directory.Exists(cat.Roots[i])) return cat.Roots[i];
            }
            if (cat.Files.Count > 0) return Path.GetDirectoryName(cat.Files[0].Path);
            return "";
        }

        protected override void PaintBody(Graphics g)
        {
            Color risk = Theme.RiskColor(_cat.Risk);
            Color accent = _cat.Accent;
            if (accent.A == 0) accent = risk;
            RectangleF icon = new RectangleF(28, 80, 54, 54);
            Theme.FillRounded(g, Ui.Alpha(accent, 18), icon, 16f);
            using (SolidBrush b = new SolidBrush(accent))
            using (StringFormat sf = new StringFormat())
            {
                Font gf = Theme.Font(15f, FontStyle.Bold);
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(_cat.Glyph, gf, b, icon, sf);
            }

            {
                Font nf = Theme.Font(15f, FontStyle.Bold);
                Theme.DrawText(g, _cat.Name, nf, Theme.Text, new RectangleF(98, 82, 380, 26),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font mf = Theme.Font(8.8f, FontStyle.Regular);
                string head = _cat.Registry
                    ? Loc.F("reg.hint", _cat.RegItems.Count)
                    : Loc.F("detail.files", Theme.Count(_cat.FileCount), Theme.Bytes(_cat.Bytes));
                Theme.DrawText(g, head, mf, Theme.SubText, new RectangleF(98, 110, 520, 22),
                    StringAlignment.Near, StringAlignment.Center);
            }

            float chipW = _cat.Risk == RiskLevel.Safe ? 92f : (_cat.Risk == RiskLevel.Caution ? 72f : 64f);
            if (_cat.Risk == RiskLevel.Info) chipW = 48f;
            RectangleF chip = new RectangleF(LWidth - 28 - chipW, 94, chipW, 22);
            Theme.Chip(g, Theme.RiskText(_cat.Risk), risk, chip);

            if (!_cat.Registry && _cat.Files.Count > 0)
            {
                int safe = 0;
                int caution = 0;
                int personal = 0;
                for (int i = 0; i < _cat.Files.Count; i++)
                {
                    RiskLevel fr = _cat.Files[i].Risk;
                    if (fr == RiskLevel.Safe) safe++;
                    else if (fr == RiskLevel.Protected) personal++;
                    else caution++;
                }
                {
                    Font sf = Theme.Font(7.9f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.F("detail.fileSummary", Theme.Count(safe), Theme.Count(caution), Theme.Count(personal)),
                        sf, Theme.Muted, new RectangleF(98, 133, LWidth - 170, 16), StringAlignment.Near, StringAlignment.Center);
                }
            }

            RectangleF note = new RectangleF(28, 152, LWidth - 56, 70);
            Theme.FillRounded(g, Ui.Alpha(risk, 13), note, 12f);
            using (SolidBrush b = new SolidBrush(Theme.Text))
            using (StringFormat sf = new StringFormat())
            {
                Font nf = Theme.Font(8.8f, FontStyle.Regular);
                string why = Explain(_cat);
                g.DrawString(why, nf, b, new RectangleF(note.X + 16, note.Y + 12, note.Width - 32, note.Height - 20), sf);
            }

            int count = _cat.Registry ? _cat.RegItems.Count : _cat.Files.Count;
            if (count == 0)
            {
                RectangleF empty = new RectangleF(28, 244, 764, 268);
                Theme.FillRounded(g, Theme.Surface, empty, 12f);
                Theme.StrokeRounded(g, Theme.Border, empty, 12f, 1f);
                if (_cat.Actionable)
                {
                    RectangleF badge = new RectangleF(empty.X + empty.Width / 2f - 26, empty.Y + 68, 52, 52);
                    Theme.FillRounded(g, Ui.Alpha(risk, 18), badge, 16f);
                    using (SolidBrush b = new SolidBrush(risk))
                    using (StringFormat sf = new StringFormat())
                    {
                        Font gf = Theme.Font(15f, FontStyle.Bold);
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString(_cat.Glyph, gf, b, badge, sf);
                    }
                }
                else
                {
                    Theme.DrawLock(g, new RectangleF(empty.X + empty.Width / 2f - 18, empty.Y + 74, 36, 40),
                        Theme.Muted);
                }
                {
                    Font tf = Theme.Font(10f, FontStyle.Bold);
                    Theme.DrawText(g, _cat.Actionable ? Loc.T("detail.empty") : Loc.T("detail.emptyProtected"),
                        tf, Theme.Text,
                        new RectangleF(empty.X, empty.Y + 126, empty.Width, 24), StringAlignment.Center, StringAlignment.Center);
                }
                {
                    Font sf = Theme.Font(8.5f, FontStyle.Regular);
                    string sub;
                    if (_cat.Actionable) sub = Loc.T("detail.emptySub");
                    else if (_cat.Risk == RiskLevel.Protected) sub = Loc.T("detail.emptyProtectedSub");
                    else sub = Loc.T("detail.emptyOtherSub");
                    Theme.DrawText(g, sub, sf, Theme.SubText,
                        new RectangleF(empty.X, empty.Y + 154, empty.Width, 22), StringAlignment.Center, StringAlignment.Center);
                }
            }
            else
            {
                {
                    Font sf = Theme.Font(8f, FontStyle.Regular);
                    string extra = _cat.Registry ? Loc.T("detail.regHint") : (count > 800 ? Loc.F("detail.onlyTop", 800) : Loc.T("detail.hint"));
                    Theme.DrawText(g, extra, sf, Theme.Muted,
                        new RectangleF(30, 226, 620, 16), StringAlignment.Near, StringAlignment.Center);
                }
            }
        }

        private static string Explain(Category c)
        {
            if (c.Registry) return c.Desc + Loc.T("explain.sep") + Loc.T("explain.junk");
            if (c.Risk == RiskLevel.Safe) return c.Desc + Loc.T("explain.sep") + Loc.T("explain.safe");
            if (c.Risk == RiskLevel.Caution) return c.Desc + Loc.T("explain.sep") + Loc.T("explain.caution");
            if (c.Risk == RiskLevel.Protected) return c.Desc + Loc.T("explain.sep") + Loc.T("explain.protected");
            return c.Desc + Loc.T("explain.sep") + Loc.T("explain.other");
        }
    }

    /// <summary>单个已安装软件的信息面板。</summary>
    public class ProgramDialog : DialogBase
    {
        private readonly ProgramEntry _p;
        private bool _hoverOpen;
        private RectangleF _openRect;

        public ProgramDialog(ProgramEntry p) : base(Loc.T("pdetail.title"), 620, 380)
        {
            _p = p;
            _openRect = new RectangleF(300, 318, 160, 40);

            UiButton close = new UiButton();
            close.Primary = true;
            close.Text = Loc.T("btn.close");
            MakeButton(close, 476, 318, 116, 40);
            close.Click += delegate { Close(); };
        }

        protected override void PaintBody(Graphics g)
        {
            RectangleF icon = new RectangleF(28, 74, 46, 46);
            Theme.FillRounded(g, Ui.Alpha(_p.Accent, 18), icon, 14f);
            using (SolidBrush b = new SolidBrush(_p.Accent))
            using (StringFormat sf = new StringFormat())
            {
                Font gf = Theme.Font(15f, FontStyle.Bold);
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(_p.Glyph, gf, b, icon, sf);
            }
            {
                Font nf = Theme.Font(13f, FontStyle.Bold);
                Theme.DrawText(g, _p.Name, nf, Theme.Text, new RectangleF(88, 76, LWidth - 130, 24),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font sf = Theme.Font(8.4f, FontStyle.Regular);
                Theme.DrawText(g, _p.SubText, sf, Theme.SubText, new RectangleF(88, 100, LWidth - 130, 20),
                    StringAlignment.Near, StringAlignment.Center);
            }

            float y = 140;
            Row(g, Loc.T("pdetail.path"), _p.InstallDir.Length > 0 ? _p.InstallDir : Loc.T("uninstall.noPath"), y);
            Row(g, Loc.T("pdetail.size"), Theme.Count(_p.FileCount) + " " + Loc.T("unit.items") + " · " + Theme.Bytes(_p.Bytes), y + 34);
            Row(g, Loc.T("pdetail.registry"), _p.RegKey, y + 68);
            Row(g, Loc.T("pdetail.uninstall"), _p.UninstallString.Length > 0 ? _p.UninstallString : Loc.T("pdetail.none"), y + 102);

            if (_p.Protected)
            {
                RectangleF warn = new RectangleF(26, 252, LWidth - 52, 46);
                Theme.FillRounded(g, Ui.Alpha(Theme.Rose, 16), warn, 10f);
                {
                    Font f = Theme.Font(8.4f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.T("pdetail.protected") + _p.ProtectedReason, f, Theme.Rose,
                        new RectangleF(warn.X + 14, warn.Y, warn.Width - 28, warn.Height), StringAlignment.Near,
                        StringAlignment.Center);
                }
            }
            else
            {
                {
                    Font f = Theme.Font(8.4f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.T("pdetail.forceNote"), f, Theme.SubText,
                        new RectangleF(28, 256, LWidth - 56, 40), StringAlignment.Near, StringAlignment.Center);
                }
            }

            RectangleF open = _openRect;
            if (_hoverOpen) Theme.FillRounded(g, Theme.Hover, open, 10f);
            {
                Font f = Theme.Font(8.8f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("btn.openFolder"), f, Theme.Blue, open, StringAlignment.Center, StringAlignment.Center);
            }
        }

        private static void Row(Graphics g, string label, string value, float y)
        {
            {
                Font lf = Theme.Font(8f, FontStyle.Bold);
                Theme.DrawText(g, label, lf, Theme.Muted, new RectangleF(28, y, 100, 18),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font vf = Theme.Font(8.4f, FontStyle.Regular);
                Theme.DrawText(g, value, vf, Theme.Text, new RectangleF(120, y, 470, 18),
                    StringAlignment.Near, StringAlignment.Center);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point lp = LogicalPoint(e);
            bool h = _openRect.Contains(lp.X, lp.Y) && _p.InstallDir.Length > 0 && Directory.Exists(_p.InstallDir);
            if (h != _hoverOpen)
            {
                _hoverOpen = h;
                Cursor = h ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Point lp = LogicalPoint(e);
            if (_openRect.Contains(lp.X, lp.Y) && _p.InstallDir.Length > 0) Native.OpenFolder(_p.InstallDir);
        }
    }

    /// <summary>第 16 步验证窗口：复查结果 + sfc / DISM / 事件查看器 / 系统还原入口。</summary>
    public class VerifyDialog : DialogBase
    {
        private readonly List<VerifyCheck> _checks;

        public VerifyDialog(List<VerifyCheck> checks) : base(Loc.T("verify.title"), 560, 500)
        {
            _checks = checks == null ? new List<VerifyCheck>() : checks;

            UiButton sfc = new UiButton();
            sfc.Primary = false;
            sfc.Text = Loc.T("verify.btnSfc");
            MakeButton(sfc, 28, 346, 162, 40);
            sfc.Click += delegate { Launch("cmd.exe", "/k sfc /scannow", true); };

            UiButton dism = new UiButton();
            dism.Primary = false;
            dism.Text = Loc.T("verify.btnDism");
            MakeButton(dism, 196, 346, 148, 40);
            dism.Click += delegate { Launch("cmd.exe", "/k DISM /Online /Cleanup-Image /CheckHealth", true); };

            UiButton evt = new UiButton();
            evt.Primary = false;
            evt.Text = Loc.T("verify.btnEventLog");
            MakeButton(evt, 350, 346, 154, 40);
            evt.Click += delegate { Launch("eventvwr.msc", "", false); };

            UiButton restore = new UiButton();
            restore.Primary = false;
            restore.Text = Loc.T("verify.btnRestore");
            MakeButton(restore, 28, 396, 154, 38);
            restore.Click += delegate { Launch("rstrui.exe", "", false); };

            UiButton done = new UiButton();
            done.Primary = true;
            done.Text = Loc.T("btn.done");
            MakeButton(done, 370, 396, 162, 38);
            done.Click += delegate { Close(); };
        }

        private static void Launch(string file, string args, bool elevate)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = true;
                if (elevate) psi.Verb = "runas";
                Process.Start(psi);
            }
            catch
            {
            }
        }

        protected override void PaintBody(Graphics g)
        {
            {
                Font lf = Theme.Font(8.6f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("verify.lead"), lf, Theme.SubText,
                    new RectangleF(28, 64, LWidth - 56, 20), StringAlignment.Near, StringAlignment.Center);
            }

            for (int i = 0; i < _checks.Count && i < 6; i++)
            {
                VerifyCheck c = _checks[i];
                RectangleF row = new RectangleF(28, 92 + i * 39, LWidth - 56, 33);
                Theme.FillRounded(g, Theme.Sunken, row, 10f);
                Theme.StrokeRounded(g, Theme.Border, row, 10f, 1f);
                Color dot = c.State == 0 ? Theme.Green : (c.State == 1 ? Theme.Amber : Theme.Muted);
                using (SolidBrush b = new SolidBrush(dot))
                {
                    g.FillEllipse(b, row.X + 14, row.Y + 12, 9, 9);
                }
                {
                    Font nf = Theme.Font(8.8f, FontStyle.Bold);
                    Theme.DrawText(g, c.Label, nf, Theme.Text,
                        new RectangleF(row.X + 34, row.Y, 190, 33), StringAlignment.Near, StringAlignment.Center);
                }
                {
                    Font vf = Theme.Font(8.6f, FontStyle.Regular);
                    Theme.DrawText(g, c.Value, vf, c.State == 0 ? Theme.Green : (c.State == 1 ? Theme.Amber : Theme.Muted),
                        new RectangleF(row.X + 230, row.Y, row.Width - 246, 33), StringAlignment.Far, StringAlignment.Center);
                }
            }

            {
                Font nf = Theme.Font(7.6f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("verify.note"), nf, Theme.Muted,
                    new RectangleF(28, 448, LWidth - 56, 40), StringAlignment.Near, StringAlignment.Center);
            }
        }
    }

    public class LangMenuForm : Form
    {
        private readonly Action<string> _pick;
        private int _hover = -1;

        public LangMenuForm(Action<string> pick)
        {
            _pick = pick;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(Px(210), Px(3 * 38 + 16));
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            ImeMode = ImeMode.Disable;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            _openAt = Environment.TickCount;
        }

        private int _openAt;

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
            if (Environment.TickCount - _openAt > 200) Close();
        }

        private float LW
        {
            get { return Width / Theme.Scale; }
        }

        private RectangleF RowRect(int i)
        {
            return new RectangleF(8, 8 + i * 38, LW - 16, 34);
        }

        private int Hit(Point p)
        {
            for (int i = 0; i < Loc.Langs.Length; i++)
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
            if (idx < 0) return;
            string code = Loc.Langs[idx];
            Close();
            if (_pick != null) _pick(code);
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

            for (int i = 0; i < Loc.Langs.Length; i++)
            {
                RectangleF row = RowRect(i);
                Color dot = i == 0 ? Theme.Blue : (i == 1 ? Theme.Violet : Theme.Cyan);
                if (i == _hover)
                {
                    Theme.FillRounded(g, Theme.Surface, row, 10f);
                }
                using (SolidBrush b = new SolidBrush(dot))
                {
                    g.FillEllipse(b, row.X + 12, row.Y + 13, 8, 8);
                }
                {
                    Font f = Theme.Font(9f, FontStyle.Bold);
                    Theme.DrawText(g, Loc.LangName(Loc.Langs[i]), f, Theme.Text,
                        new RectangleF(row.X + 30, row.Y, 140, row.Height), StringAlignment.Near, StringAlignment.Center);
                }
                if (Loc.Langs[i] == Loc.Lang)
                {
                    using (Pen p = new Pen(dot, 1.6f))
                    {
                        g.DrawLine(p, row.Right - 24, row.Y + 17, row.Right - 20, row.Y + 21);
                        g.DrawLine(p, row.Right - 20, row.Y + 21, row.Right - 13, row.Y + 12);
                    }
                }
            }
        }
    }

    public class FileListView : Control
    {
        private List<FileEntry> _files = new List<FileEntry>();
        private int _scroll;
        private int _target;
        private int _hover = -1;
        private readonly string _key;
        private readonly int RowH = 52;

        public FileListView()
        {
            _key = Ui.Key("files");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        public void SetFiles(List<FileEntry> files, int max)
        {
            _files = new List<FileEntry>();
            for (int i = 0; i < files.Count && i < max; i++) _files.Add(files[i]);
            _scroll = 0;
            _target = 0;
            Invalidate();
        }

        private int MaxScroll
        {
            get { return Math.Max(0, _files.Count * RowH + 10 - (int)(Height / Theme.Scale)); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (MaxScroll <= 0) return;
            _target -= e.Delta;
            if (_target > MaxScroll) _target = MaxScroll;
            if (_target < 0) _target = 0;
            int from = _scroll;
            Anim.Stop(_key);
            Anim.Tween(_key, from, _target, 200, delegate(float v) { _scroll = (int)v; Invalidate(); }, null);
        }

        private int Hit(int y)
        {
            int idx = (y + _scroll - 5) / RowH;
            if (idx < 0 || idx >= _files.Count) return -1;
            return idx;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = Hit((int)(e.Y / Theme.Scale));
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

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int idx = Hit((int)(e.Y / Theme.Scale));
            if (idx < 0) return;
            FileEntry f = _files[idx];
            if (string.IsNullOrEmpty(f.Label)) Native.ShowInExplorer(f.Path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.Clear(Theme.Bg);
            g.ScaleTransform(Theme.Scale, Theme.Scale);
            float lw = Width / Theme.Scale;
            float lh = Height / Theme.Scale;
            RectangleF card = new RectangleF(0, 0, lw - 1, lh - 1);
            Theme.FillRounded(g, Theme.Surface, card, 12f);
            Theme.StrokeRounded(g, Theme.Border, card, 12f, 1f);

            g.SetClip(new RectangleF(card.X + 1, card.Y + 1, card.Width - 2, card.Height - 2));
            for (int i = 0; i < _files.Count; i++)
            {
                float y = 5 + i * RowH - _scroll;
                if (y < -RowH || y > lh) continue;
                RectangleF row = new RectangleF(8, y, lw - 20, RowH - 1);
                if (i == _hover)
                {
                    Theme.FillRounded(g, Theme.Surface, row, 9f);
                }
                if (i > 0)
                {
                    using (Pen p = new Pen(Theme.Divider, 1f))
                    {
                        g.DrawLine(p, row.X + 8, y, row.Right - 8, y);
                    }
                }
                FileEntry f = _files[i];
                bool labelled = !string.IsNullOrEmpty(f.Label);
                bool judged = !labelled && f.KindKey.Length > 0;
                string name = labelled ? f.Label : Path.GetFileName(f.Path);
                string sub = labelled ? f.Path : Path.GetDirectoryName(f.Path);
                float textW = row.Width - (judged ? 340 : 250);
                {
                    Font nf = Theme.Font(8.8f, FontStyle.Bold);
                    Theme.DrawText(g, name, nf, Theme.Text, new RectangleF(row.X + 10, y + 6, textW, 20),
                        StringAlignment.Near, StringAlignment.Center);
                }
                {
                    Font df = Theme.Font(7.8f, FontStyle.Regular);
                    Theme.DrawText(g, sub, df, Theme.Muted, new RectangleF(row.X + 10, y + 26, textW, 16),
                        StringAlignment.Near, StringAlignment.Center);
                }
                if (judged)
                {
                    RectangleF kindChip = new RectangleF(row.Right - 306, y + 16, 92, 20);
                    Theme.Chip(g, Loc.T(f.KindKey), KindColor(f.KindKey), kindChip);
                    RectangleF riskChip = new RectangleF(row.Right - 206, y + 16, 82, 20);
                    Theme.Chip(g, Theme.RiskText(f.Risk), FileRiskColor(f.Risk), riskChip);
                    {
                        Font sf = Theme.Font(8.5f, FontStyle.Bold);
                        Theme.DrawText(g, Theme.Bytes(f.Size), sf, Theme.SubText,
                            new RectangleF(row.Right - 118, y + 6, 108, 40), StringAlignment.Far, StringAlignment.Center);
                    }
                }
                else
                {
                    {
                        Font sf = Theme.Font(8.5f, FontStyle.Bold);
                        string right = labelled ? f.Note : Theme.Bytes(f.Size);
                        Theme.DrawText(g, right, sf, Theme.SubText,
                            new RectangleF(row.Right - 230, y + 6, 220, 40), StringAlignment.Far, StringAlignment.Center);
                    }
                }
            }
            g.ResetClip();

            if (MaxScroll > 0)
            {
                float thumbH = Math.Max(30f, lh * (lh / (float)(_files.Count * RowH + 10)));
                float y = (lh - thumbH) * (_scroll / (float)MaxScroll);
                Theme.FillRounded(g, Color.FromArgb(24, Theme.Blue), new RectangleF(lw - 8, 8, 4, lh - 16), 2f);
                Theme.FillRounded(g, Color.FromArgb(120, Theme.Blue), new RectangleF(lw - 8, 8 + y, 4, thumbH), 2f);
            }
        }

        private static Color KindColor(string kindKey)
        {
            if (kindKey == "file.kind.document") return Theme.Rose;
            if (kindKey == "file.kind.binary") return Theme.Violet;
            if (kindKey == "file.kind.config") return Theme.Amber;
            if (kindKey == "file.kind.installer" || kindKey == "file.kind.update") return Theme.Violet;
            if (kindKey == "file.kind.dump" || kindKey == "file.kind.recycle") return Theme.Rose;
            if (kindKey == "file.kind.thumb" || kindKey == "file.kind.font") return Theme.Green;
            if (kindKey == "file.kind.backup") return Theme.Amber;
            if (kindKey == "file.kind.prefetch") return Theme.Cyan;
            if (kindKey == "file.kind.other") return Theme.Muted;
            return Theme.Blue;
        }

        private static Color FileRiskColor(RiskLevel risk)
        {
            if (risk == RiskLevel.Safe) return Theme.Green;
            if (risk == RiskLevel.Caution) return Theme.Amber;
            if (risk == RiskLevel.Protected) return Theme.Rose;
            return Theme.Muted;
        }
    }
}
