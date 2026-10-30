using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CleanC
{
    /// <summary>「应用搬家」页：扫描 AppData 一级目录、选目标盘、迁移 / 还原，全部在后台线程执行。</summary>
    public class MigratePanel : Control, IThemed
    {
        private readonly List<MigrateEntry> _entries = new List<MigrateEntry>();
        private int _scroll;
        private int _scrollTarget;
        private int _hover = -1;
        private int _scope;
        private bool _busy;
        private bool _isMoving;
        private bool _scanned;
        private bool _cancelWork;
        private string _workName = "";
        private string _status = "";
        private string _lastStatus = "";
        private long _progressDone;
        private long _progressTotal;
        private string _progressStage = "";
        private int _lastPaintTick;
        private int _lastProgressTick;
        private string _targetRoot = "";
        private readonly string _tickKey;

        private Segmented _scopeSeg;
        private UiButton _btnChange;
        private UiButton _btnLog;
        private UiButton _btnAll;
        private UiButton _btnMove;
        private UiButton _btnRestore;

        private const float HeaderH = 38f;
        private const float RowH = 60f;
        private const float RowGap = 6f;
        private const float ListY = 42f;
        private const float ListH = 340f;
        private const float StatusY = 386f;

        public Action Changed;

        public MigratePanel()
        {
            _tickKey = Ui.Key("migrateTick");
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;

            _targetRoot = MigrateStore.TargetRoot;
            if (_targetRoot.Length == 0) _targetRoot = MigrateOps.DefaultTargetRoot();

            _scopeSeg = new Segmented();
            _scopeSeg.Filled = true;
            _scopeSeg.FontSize = 8.6f;
            _scopeSeg.Radius = 10f;
            _scopeSeg.Accent = Theme.Blue;
            _scopeSeg.Bounds = PxRect(0, 4, 196, 30);
            _scopeSeg.BackColor = Theme.Bg;
            _scopeSeg.SetItems(ScopeLabels(), 0);
            _scopeSeg.Changed = delegate
            {
                _scope = _scopeSeg.SelectedIndex;
                Rescan();
            };
            Controls.Add(_scopeSeg);

            _btnChange = new UiButton();
            _btnChange.Primary = false;
            _btnChange.Glyph = "";
            _btnChange.Text = Loc.T("migrate.targetChange");
            _btnChange.Bounds = PxRect(620, 4, 74, 30);
            _btnChange.BackColor = Theme.Bg;
            _btnChange.Click += delegate { PickTarget(); };
            Controls.Add(_btnChange);

            _btnLog = new UiButton();
            _btnLog.Primary = false;
            _btnLog.Glyph = "";
            _btnLog.Text = Loc.T("migrate.btnLog");
            _btnLog.Bounds = PxRect(700, 4, 88, 30);
            _btnLog.BackColor = Theme.Bg;
            _btnLog.Click += delegate { Native.OpenLog(MigrateLog.LogPath); };
            Controls.Add(_btnLog);

            _btnAll = new UiButton();
            _btnAll.Primary = false;
            _btnAll.Glyph = "";
            _btnAll.Text = Loc.T("migrate.selectAll");
            _btnAll.Bounds = PxRect(0, 436, 94, 38);
            _btnAll.BackColor = Theme.Bg;
            _btnAll.Click += delegate { SelectAll(); };
            Controls.Add(_btnAll);

            _btnRestore = new UiButton();
            _btnRestore.Primary = false;
            _btnRestore.Glyph = "\uE777";
            _btnRestore.Text = Loc.T("migrate.btnRestore");
            _btnRestore.Bounds = PxRect(424, 436, 160, 40);
            _btnRestore.BackColor = Theme.Bg;
            _btnRestore.Click += delegate { StartRestore(); };
            Controls.Add(_btnRestore);

            _btnMove = new UiButton();
            _btnMove.Primary = true;
            _btnMove.Glyph = "\uE8B7";
            _btnMove.Text = Loc.T("migrate.btnMove");
            _btnMove.Bounds = PxRect(592, 436, 196, 42);
            _btnMove.BackColor = Theme.Bg;
            _btnMove.Click += delegate { StartMove(); };
            Controls.Add(_btnMove);

            UpdateButtons();
        }

        private static int Px(float v)
        {
            return (int)Math.Round(v * Theme.Scale);
        }

        private static Rectangle PxRect(int x, int y, int w, int h)
        {
            return new Rectangle(Px(x), Px(y), Px(w), Px(h));
        }

        private static string[] ScopeLabels()
        {
            return new string[]
            {
                Loc.T("migrate.scope.local"), Loc.T("migrate.scope.roaming"), Loc.T("migrate.scope.all")
            };
        }

        public bool HasScanned
        {
            get { return _scanned; }
        }

        public int Count
        {
            get { return _entries.Count; }
        }

        public int MovableCount
        {
            get { return _movableCount(); }
        }

        public int MovedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _entries.Count; i++)
                    if (_entries[i].State == MigrateState.Moved) n++;
                return n;
            }
        }

        public long MovedBytes
        {
            get
            {
                long n = 0;
                List<MigrateStore.Record> recs = MigrateStore.Items;
                for (int i = 0; i < recs.Count; i++) n += recs[i].Bytes;
                return n;
            }
        }

        public string TargetDrive
        {
            get
            {
                try
                {
                    string root = Path.GetPathRoot(TargetRoot);
                    return string.IsNullOrEmpty(root) ? "—" : root.TrimEnd('\\');
                }
                catch
                {
                    return "—";
                }
            }
        }

        public string TargetRoot
        {
            get
            {
                if (_targetRoot.Length == 0) _targetRoot = MigrateOps.DefaultTargetRoot();
                return _targetRoot;
            }
        }

        public string StatusLine()
        {
            if (_busy) return _status.Length > 0 ? _status : Loc.T("migrate.scanning");
            if (_lastStatus.Length > 0) return _lastStatus;
            if (!_scanned) return Loc.T("migrate.sub");
            if (_entries.Count == 0) return Loc.T("migrate.none");
            return Loc.F("migrate.scanDone", _entries.Count, _movableCount());
        }

        private int _movableCount()
        {
            int n = 0;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].CanMove) n++;
            return n;
        }

        private int SelectedMoveCount()
        {
            int n = 0;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Selected && _entries[i].CanMove) n++;
            return n;
        }

        private int SelectedRestoreCount()
        {
            int n = 0;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Selected && _entries[i].CanRestore) n++;
            return n;
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Bg;
            _scopeSeg.BackColor = Theme.Bg;
            _scopeSeg.Accent = Theme.Blue;
            _btnChange.BackColor = Theme.Bg;
            _btnLog.BackColor = Theme.Bg;
            _btnAll.BackColor = Theme.Bg;
            _btnMove.BackColor = Theme.Bg;
            _btnRestore.BackColor = Theme.Bg;
            Invalidate();
        }

        public void Relocalize()
        {
            _scopeSeg.SetItems(ScopeLabels(), _scope);
            _btnChange.Text = Loc.T("migrate.targetChange");
            _btnLog.Text = Loc.T("migrate.btnLog");
            _btnAll.Text = Loc.T("migrate.selectAll");
            _btnMove.Text = Loc.T("migrate.btnMove");
            _btnRestore.Text = Loc.T("migrate.btnRestore");
            if (_lastStatus.Length == 0) _status = "";
            Invalidate();
        }

        public void Sync()
        {
            UpdateButtons();
            Invalidate();
        }

        public void CancelWork()
        {
            _cancelWork = true;
        }

        private void UpdateButtons()
        {
            int mv = SelectedMoveCount();
            int rs = SelectedRestoreCount();
            // 按钮文案统一为「搬家所选 · n」
            _btnMove.Text = Loc.T("migrate.btnMove") + (mv > 0 ? " · " + mv : "");
            _btnRestore.Text = Loc.T("migrate.btnRestore") + (rs > 0 ? " · " + rs : "");
            _btnMove.Enabled = !_busy && mv > 0;
            _btnRestore.Enabled = !_busy && rs > 0;
            _btnAll.Enabled = !_busy && _entries.Count > 0;
            _btnChange.Enabled = !_busy;
            _btnLog.Enabled = true;
        }

        // ---------------------------------------------------------------- data

        public void Rescan()
        {
            if (_busy) return;
            _busy = true;
            _cancelWork = false;
            _isMoving = false;
            _entries.Clear();
            _hover = -1;
            _scroll = 0;
            _scrollTarget = 0;
            _status = Loc.T("migrate.scanning");
            _progressDone = 0;
            _progressTotal = 0;
            _progressStage = "measure";
            _workName = "";
            _scanned = true;
            UpdateButtons();
            StartTick();
            Invalidate();

            bool includeRoaming = _scope >= 1;
            Thread t = new Thread(delegate()
            {
                List<MigrateEntry> list = null;
                string error = "";
                try
                {
                    list = MigrateOps.Scan(includeRoaming, delegate(MigrateProgress p)
                    {
                        _progressDone = p.Done;
                        _progressTotal = 0;
                        _progressStage = "measure";
                        _workName = p.Current;
                    }, delegate { return _cancelWork; });
                    if (_cancelWork && list == null) error = Loc.T("btn.cancel");
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                BeginInvoke(new Action(delegate
                {
                    OnScanDone(list, error);
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        private void OnScanDone(List<MigrateEntry> list, string error)
        {
            _busy = false;
            Anim.Stop(_tickKey);
            if (list != null)
            {
                _entries.Clear();
                _entries.AddRange(list);
            }
            if (error.Length > 0)
            {
                _lastStatus = Loc.F("migrate.error", error);
            }
            else if (_entries.Count == 0)
            {
                _lastStatus = Loc.T("migrate.none");
            }
            else
            {
                _lastStatus = Loc.F("migrate.scanDone", _entries.Count, _movableCount());
            }
            _status = _lastStatus;
            _progressTotal = 0;
            _progressDone = 0;
            UpdateButtons();
            Invalidate();
            if (Changed != null) Changed();
        }

        private void PickTarget()
        {
            if (_busy) return;
            using (MigrateTargetDialog d = new MigrateTargetDialog(TargetRoot))
            {
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                _targetRoot = d.SelectedRoot;
                MigrateStore.TargetRoot = _targetRoot;
                MigrateStore.Save();
                _lastStatus = Loc.T("migrate.target") + ": " + _targetRoot;
                UpdateButtons();
                Invalidate();
                if (Changed != null) Changed();
            }
        }

        private void SelectAll()
        {
            if (_busy) return;
            bool all = true;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!_entries[i].CanMove && !_entries[i].CanRestore) continue;
                if (!_entries[i].Selected) { all = false; break; }
            }
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].CanMove || _entries[i].CanRestore) _entries[i].Selected = !all;
            }
            UpdateButtons();
            Invalidate();
        }

        public void StartMove()
        {
            if (_busy) return;
            List<MigrateEntry> todo = new List<MigrateEntry>();
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Selected && _entries[i].CanMove) todo.Add(_entries[i]);
            if (todo.Count == 0) return;
            string root = TargetRoot;
            if (root.Length == 0)
            {
                Msg(Loc.T("migrate.title"), new string[] { Loc.T("migrate.noDisk") }, true);
                return;
            }
            string firstTarget = MigrateOps.TargetPathFor(root, todo[0].Scope, todo[0].Name);
            string[] lines =
            {
                Loc.F("migrate.confirm1", todo[0].Source, firstTarget),
                Loc.T("migrate.confirm2"),
                Loc.T("migrate.target") + ": " + root
            };
            MapConfirmDialog d = new MapConfirmDialog(Loc.T("migrate.confirmTitle"), lines, false,
                Loc.T("migrate.confirmOk"), false);
            if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
            RunWork(todo, false);
        }

        public void StartRestore()
        {
            if (_busy) return;
            List<MigrateEntry> todo = new List<MigrateEntry>();
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Selected && _entries[i].CanRestore) todo.Add(_entries[i]);
            if (todo.Count == 0) return;
            string[] lines =
            {
                Loc.F("migrate.restoreConfirm1", todo[0].Target),
                Loc.T("migrate.confirm2")
            };
            MapConfirmDialog d = new MapConfirmDialog(Loc.T("migrate.restoreConfirmTitle"), lines, false,
                Loc.T("migrate.restoreConfirmOk"), false);
            if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
            RunWork(todo, true);
        }

        private void RunWork(List<MigrateEntry> todo, bool restore)
        {
            _busy = true;
            _isMoving = !restore;
            _cancelWork = false;
            _progressDone = 0;
            _progressTotal = 0;
            _progressStage = restore ? "restore" : "copy";
            _workName = todo[0].Source;
            _status = Loc.F(restore ? "migrate.restoring" : "migrate.moving", todo[0].Name);
            UpdateButtons();
            StartTick();
            Invalidate();

            Thread t = new Thread(delegate()
            {
                int okCount = 0;
                long bytes = 0;
                List<string> errors = new List<string>();
                for (int i = 0; i < todo.Count; i++)
                {
                    if (_cancelWork) break;
                    MigrateEntry e = todo[i];
                    _workName = e.Name;
                    _status = Loc.F(restore ? "migrate.restoring" : "migrate.moving", e.Name);
                    MigrateResult r = restore
                        ? MigrateOps.Restore(e, OnProgress, delegate { return _cancelWork; })
                        : MigrateOps.Move(e, TargetRoot, OnProgress, delegate { return _cancelWork; });
                    if (r.Ok)
                    {
                        okCount++;
                        bytes += r.Bytes;
                        e.Selected = false;
                        e.State = restore ? MigrateState.Ready : MigrateState.Moved;
                        if (!restore && r.BackupLeft)
                            e.Note = Loc.T("migrate.progress.clean") + " " + r.BackupPath;
                    }
                    else
                    {
                        if (r.Canceled) break;
                        errors.Add(e.Name + ": " + r.Error);
                        MigrateLog.Write(restore ? "RESTORE" : "MOVE", e.Name + " failed: " + r.Error);
                    }
                }
                BeginInvoke(new Action(delegate
                {
                    _busy = false;
                    Anim.Stop(_tickKey);
                    if (errors.Count > 0)
                    {
                        string[] lines = new string[Math.Min(6, errors.Count + 2)];
                        lines[0] = Loc.F(restore ? "migrate.restoreDone" : "migrate.done", okCount, Theme.Bytes(bytes));
                        lines[1] = Loc.T("migrate.log") + " " + MigrateLog.LogPath;
                        for (int i = 0; i < lines.Length - 2; i++) lines[i + 2] = errors[i];
                        Msg(Loc.T("migrate.title"), lines, true);
                    }
                    _lastStatus = errors.Count == 0
                        ? Loc.F(restore ? "migrate.restoreDone" : "migrate.done", okCount, Theme.Bytes(bytes))
                        : Loc.F("migrate.error", errors[0]);
                    _status = _lastStatus;
                    _progressDone = 0;
                    _progressTotal = 0;
                    UpdateButtons();
                    Invalidate();
                    if (Changed != null) Changed();
                    if (errors.Count == 0 && okCount > 0) Rescan();
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        private void OnProgress(MigrateProgress p)
        {
            _progressDone = p.Done;
            _progressTotal = p.Total;
            _progressStage = p.Stage.Length > 0 ? p.Stage : _progressStage;
            if (p.Current.Length > 0) _workName = p.Current;
            int now = Environment.TickCount;
            if (unchecked(now - _lastProgressTick) > 160)
            {
                _lastProgressTick = now;
                try
                {
                    if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(delegate { Invalidate(); }));
                }
                catch
                {
                }
            }
        }

        private void StartTick()
        {
            Anim.Loop(_tickKey, delegate(float ms)
            {
                _lastPaintTick = (int)ms;
                Invalidate();
                if (!_busy) Anim.Stop(_tickKey);
            });
        }

        private void Msg(string title, string[] lines, bool danger)
        {
            using (MapConfirmDialog d = new MapConfirmDialog(title, lines, danger, Loc.T("btn.ok"), true))
            {
                d.ShowDialog(FindForm());
            }
        }

        // ---------------------------------------------------------------- input

        private RectangleF ListRect
        {
            get { return new RectangleF(0, ListY, LW, ListH); }
        }

        private float LW
        {
            get { return Width / Theme.Scale; }
        }

        private float LH
        {
            get { return Height / Theme.Scale; }
        }

        private int ContentHeight
        {
            get { return _entries.Count * (int)(RowH + RowGap) + 6; }
        }

        private RectangleF RowRect(int i)
        {
            return new RectangleF(0, ListY + 4 + i * (RowH + RowGap) - _scroll, LW - 8, RowH);
        }

        private int HitTest(float x, float y)
        {
            if (!ListRect.Contains(x, y)) return -1;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (RowRect(i).Contains(x, y)) return i;
            }
            return -1;
        }

        private void ClampScroll()
        {
            int max = Math.Max(0, ContentHeight - (int)ListH);
            if (_scrollTarget > max) _scrollTarget = max;
            if (_scrollTarget < 0) _scrollTarget = 0;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_busy || ContentHeight <= ListH) return;
            _scrollTarget -= e.Delta;
            ClampScroll();
            Anim.Tween(_tickKey + "_scroll", _scroll, _scrollTarget, 180,
                delegate(float v) { _scroll = (int)v; Invalidate(); }, null);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            float x = e.X / Theme.Scale;
            float y = e.Y / Theme.Scale;
            int idx = HitTest(x, y);
            if (idx != _hover)
            {
                _hover = idx;
                Cursor = idx < 0 ? Cursors.Default : Cursors.Hand;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover >= 0)
            {
                _hover = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left || _busy) return;
            float x = e.X / Theme.Scale;
            float y = e.Y / Theme.Scale;
            int idx = HitTest(x, y);
            if (idx < 0) return;
            MigrateEntry item = _entries[idx];
            if (item.State == MigrateState.ExternalLink)
            {
                Native.OpenFolder(item.Source);
                return;
            }
            if (e.Clicks >= 2)
            {
                Native.OpenFolder(item.State == MigrateState.Moved && Directory.Exists(item.Target)
                    ? item.Target : item.Source);
                return;
            }
            if (!item.CanMove && !item.CanRestore) return;
            item.Selected = !item.Selected;
            UpdateButtons();
            Invalidate();
        }

        // ---------------------------------------------------------------- paint

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.ScaleTransform(Theme.Scale, Theme.Scale);

            float lw = LW;
            PaintHeader(g, lw);

            Region old = g.Clip;
            g.SetClip(ListRect);
            for (int i = 0; i < _entries.Count; i++)
            {
                RectangleF r = RowRect(i);
                if (r.Bottom < ListY - 20) continue;
                if (r.Y > ListY + ListH + 20) break;
                DrawRow(g, r, _entries[i], i == _hover);
            }
            g.Clip = old;

            if (_entries.Count == 0 && !_busy)
            {
                Font f = Theme.Font(9f, FontStyle.Regular);
                Theme.DrawText(g, _scanned ? Loc.T("migrate.none") : Loc.T("migrate.subEmpty"), f, Theme.Muted,
                    new RectangleF(0, ListY + ListH / 2f - 10, lw, 20),
                    StringAlignment.Center, StringAlignment.Center);
            }
            else if (_entries.Count == 0 && _busy)
            {
                Font f = Theme.Font(9f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("migrate.scanning"), f, Theme.Muted,
                    new RectangleF(0, ListY + ListH / 2f - 10, lw, 20),
                    StringAlignment.Center, StringAlignment.Center);
            }

            PaintScroll(g, lw);
            PaintStatus(g, lw);
        }

        private void PaintHeader(Graphics g, float lw)
        {
            string text = TargetRoot.Length == 0 ? Loc.T("migrate.noDisk") : Loc.T("migrate.target") + "  " + TargetRoot;
            Font f = Theme.Font(8.6f, FontStyle.Bold);
            Theme.DrawText(g, text, f, TargetRoot.Length == 0 ? Theme.Red : Theme.SubText,
                new RectangleF(204, 4, 408, 30), StringAlignment.Near, StringAlignment.Center);
            if (TargetRoot.Length == 0)
            {
                Font sf = Theme.Font(7.6f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("migrate.noDisk"), sf, Theme.Red,
                    new RectangleF(204, 4, 408, 30), StringAlignment.Near, StringAlignment.Center);
            }
        }

        private void DrawRow(Graphics g, RectangleF row, MigrateEntry item, bool hover)
        {
            RectangleF card = row;
            Color accent = item.State == MigrateState.Moved ? Theme.Green :
                (item.State == MigrateState.Busy ? Theme.Amber :
                (item.State == MigrateState.ExternalLink ? Theme.Muted : Theme.Blue));
            Color fill = Theme.Card;
            Color border = Theme.Border;
            if (item.Selected && (item.CanMove || item.CanRestore))
            {
                fill = Theme.Mix(Theme.Card, accent, 0.08f);
                border = Theme.Mix(Theme.Border, accent, 0.5f);
            }
            if (hover)
            {
                fill = Theme.Mix(fill, Theme.Mix(Theme.Card, accent, 0.1f), 0.8f);
                border = Theme.Mix(border, accent, 0.65f);
            }
            if (hover) Theme.Shadow(g, new RectangleF(card.X + 1, card.Y + 3, card.Width - 2, card.Height - 2), 12f, 10, 3f);
            Theme.FillRounded(g, fill, card, 12f);
            Theme.StrokeRounded(g, border, card, 12f, 1f);

            bool selectable = item.CanMove || item.CanRestore;
            RectangleF box = new RectangleF(card.X + 14, card.Y + 20, 20, 20);
            if (!selectable)
            {
                Theme.DrawLock(g, new RectangleF(box.X + 2, box.Y + 1, 15, 18), Theme.Muted);
            }
            else if (item.Selected)
            {
                Theme.FillRoundedGradient(g, accent, Theme.Mix(accent, Color.White, 0.3f), box, 6.5f);
                using (Pen p = new Pen(Color.White, 1.7f))
                {
                    g.DrawLine(p, box.X + 5, box.Y + 10.5f, box.X + 8.6f, box.Y + 14);
                    g.DrawLine(p, box.X + 8.6f, box.Y + 14, box.X + 15, box.Y + 6.2f);
                }
            }
            else
            {
                Theme.FillRounded(g, Theme.Card, box, 6.5f);
                Theme.StrokeRounded(g, Theme.BorderStrong, new RectangleF(box.X + 0.5f, box.Y + 0.5f, 19, 19), 6.5f, 1.2f);
            }

            RectangleF icon = new RectangleF(card.X + 48, card.Y + 12, 38, 38);
            Theme.FillRounded(g, Theme.A(accent, 18), icon, 12f);
            using (SolidBrush b = new SolidBrush(accent))
            using (StringFormat sf = Center())
            {
                Font gf = Theme.IconFont(13f);
                g.DrawString("\uE8B7", gf, b, icon, sf);
            }

            float textX = card.X + 98;
            float rightW = 168;
            {
                Font nf = Theme.Font(10f, FontStyle.Bold);
                Theme.DrawText(g, item.Name, nf, Theme.Text,
                    new RectangleF(textX, card.Y + 7, card.Width - 98 - rightW, 20), StringAlignment.Near,
                    StringAlignment.Center);
            }
            RectangleF scopeChip = new RectangleF(textX, card.Y + 31, 54, 18);
            Theme.Chip(g, item.ScopeName, Theme.SubText, scopeChip);
            {
                string sub = item.State == MigrateState.Moved && item.Target.Length > 0
                    ? Loc.F("migrate.linkTo", item.Target) : item.Source;
                if (item.Note.Length > 0) sub += "  ·  " + item.Note;
                Font sf = Theme.Font(7.6f, FontStyle.Regular);
                Theme.DrawText(g, sub, sf, item.State == MigrateState.Moved ? Theme.SubText : Theme.Muted,
                    new RectangleF(scopeChip.Right + 8, card.Y + 31, card.Width - 98 - rightW - 70, 18),
                    StringAlignment.Near, StringAlignment.Center);
            }

            {
                Font vf = Theme.Font(9.4f, FontStyle.Bold);
                string size = item.State == MigrateState.Moved
                    ? (item.Bytes > 0 ? Theme.Bytes(item.Bytes) : "—")
                    : (item.Bytes > 0 ? Theme.Bytes(item.Bytes) : "—");
                Theme.DrawText(g, size, vf, Theme.Text,
                    new RectangleF(card.Right - rightW, card.Y + 7, rightW - 14, 20), StringAlignment.Far,
                    StringAlignment.Center);
            }
            {
                string state = Loc.T(item.StateKey);
                Font sf = Theme.Font(7.8f, FontStyle.Bold);
                float w = TextRenderer.MeasureText(state, sf).Width + 16;
                Theme.Chip(g, state, accent,
                    new RectangleF(card.Right - 14 - w, card.Y + 30, w, 19));
            }
            if (item.FileCount > 0)
            {
                Font sf = Theme.Font(7.4f, FontStyle.Regular);
                Theme.DrawText(g, Loc.F("migrate.files", Theme.Count(item.FileCount)), sf, Theme.Faint,
                    new RectangleF(card.Right - rightW - 60, card.Y + 31, 110, 18), StringAlignment.Far,
                    StringAlignment.Center);
            }
        }

        private static StringFormat Center()
        {
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            return sf;
        }

        private void PaintScroll(Graphics g, float lw)
        {
            if (ContentHeight <= ListH) return;
            float max = ContentHeight - ListH;
            float thumbH = Math.Max(36f, ListH * (ListH / (float)ContentHeight));
            float pos = max > 0 ? _scroll / max : 0f;
            float y = ListY + (ListH - thumbH) * pos;
            Theme.FillRounded(g, Theme.A(Theme.Blue, 22), new RectangleF(lw - 6, ListY + 3, 4, ListH - 6), 2f);
            Theme.FillRounded(g, Theme.A(Theme.Blue, 120), new RectangleF(lw - 6, y, 4, thumbH), 2f);
        }

        private void PaintStatus(Graphics g, float lw)
        {
            string status = _busy ? _status : (_lastStatus.Length > 0 ? _lastStatus : StatusLine());
            if (_busy && _workName.Length > 0)
            {
                string stage = _progressStage == "measure" ? Loc.T("migrate.scanning") : Loc.T(_progressStage == "verify" ?
                    "migrate.progress.verify" : (_progressStage == "link" ? "migrate.progress.link" :
                    (_progressStage == "clean" ? "migrate.progress.clean" :
                    (_progressStage == "restore" ? Loc.T("migrate.progress.restore") : Loc.T("migrate.progress.copy")))));
                Font sf = Theme.Font(7.8f, FontStyle.Regular);
                Theme.DrawText(g, stage + "  ·  " + _workName, sf, Theme.SubText,
                    new RectangleF(0, StatusY, lw, 18), StringAlignment.Near, StringAlignment.Center);
            }
            else
            {
                Font sf = Theme.Font(8.2f, FontStyle.Regular);
                Theme.DrawText(g, status, sf, Theme.SubText, new RectangleF(0, StatusY, lw, 18),
                    StringAlignment.Near, StringAlignment.Center);
            }

            float barY = StatusY + 22;
            RectangleF track = new RectangleF(0, barY, lw, 4);
            Theme.FillRounded(g, Theme.Track, track, 2f);
            float ratio = _progressTotal > 0 ? Math.Min(1f, _progressDone / (float)_progressTotal) : 0f;
            if (_busy && _progressStage == "measure" && _progressDone > 0)
            {
                ratio = 0f;
            }
            if (_busy && (_progressDone > 0 || _progressTotal > 0))
            {
                if (ratio <= 0f) ratio = 0.94f;
                RectangleF fill = new RectangleF(0, barY, Math.Max(4f, lw * ratio), 4);
                Theme.FillRoundedGradient(g, Theme.Blue, Theme.Cyan, fill, 2f);
            }
            if (_busy && _workName.Length > 0)
            {
                Font pf = Theme.Font(7.4f, FontStyle.Bold);
                Theme.DrawText(g, Loc.F("migrate.selected", SelectedMoveCount() + SelectedRestoreCount()), pf,
                    Theme.SubText, new RectangleF(108, 438, 180, 36), StringAlignment.Near, StringAlignment.Center);
            }
        }
    }

    /// <summary>目标盘选择：列出非 C 盘的固定磁盘，也可以手动选择文件夹。</summary>
    public class MigrateTargetDialog : DialogBase
    {
        private readonly List<string> _drives = MigrateOps.OtherFixedDrives();
        private readonly List<string> _targets = new List<string>();
        private int _hover = -1;
        private int _selected;
        private string _custom = "";

        public string SelectedRoot = "";

        public MigrateTargetDialog(string current) : base(Loc.T("migrate.dlg.title"), 540, 386)
        {
            for (int i = 0; i < _drives.Count; i++)
            {
                _targets.Add(Path.Combine(_drives[i] + Path.DirectorySeparatorChar, "CleanC-Moved"));
            }
            _selected = 0;
            if (!string.IsNullOrEmpty(current))
            {
                bool matched = false;
                for (int i = 0; i < _targets.Count; i++)
                {
                    if (string.Equals(PathUtil.Norm(_targets[i]), PathUtil.Norm(current), StringComparison.Ordinal))
                    {
                        _selected = i;
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                {
                    _custom = current;
                    _selected = -1;
                }
            }

            UiButton cancel = new UiButton();
            cancel.Primary = false;
            cancel.Glyph = "";
            cancel.Text = Loc.T("btn.cancel");
            MakeButton(cancel, LWidth - 236, LHeight - 60, 96, 42);
            cancel.Click += delegate { Close(); };

            UiButton ok = new UiButton();
            ok.Primary = true;
            ok.Glyph = "\uE73E";
            ok.Text = Loc.T("migrate.dlg.ok");
            MakeButton(ok, LWidth - 128, LHeight - 60, 104, 42);
            ok.Click += delegate
            {
                if (_selected >= 0 && _selected < _targets.Count) SelectedRoot = _targets[_selected];
                else if (_custom.Length > 0) SelectedRoot = _custom;
                if (SelectedRoot.Length == 0) return;
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        private RectangleF RowRect(int i)
        {
            return new RectangleF(24, 86 + i * 46, LWidth - 48, 42);
        }

        private RectangleF CustomRect
        {
            get { return new RectangleF(24, 86 + _targets.Count * 46 + 8, LWidth - 48, 38); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = LogicalPoint(e);
            int idx = -1;
            for (int i = 0; i < _targets.Count; i++)
                if (RowRect(i).Contains(p.X, p.Y)) idx = i;
            if (CustomRect.Contains(p.X, p.Y)) idx = 100;
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
            Point p = LogicalPoint(e);
            for (int i = 0; i < _targets.Count; i++)
            {
                if (RowRect(i).Contains(p.X, p.Y))
                {
                    _selected = i;
                    _custom = "";
                    Invalidate();
                    return;
                }
            }
            if (CustomRect.Contains(p.X, p.Y))
            {
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = Loc.T("migrate.dlg.custom");
                    d.ShowNewFolderButton = true;
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        _custom = d.SelectedPath.TrimEnd('\\');
                        _selected = -1;
                        Invalidate();
                    }
                }
            }
        }

        protected override void PaintBody(Graphics g)
        {
            if (_targets.Count == 0)
            {
                Font f = Theme.Font(9f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("migrate.dlg.needDisk"), f, Theme.Red,
                    new RectangleF(26, 82, LWidth - 52, 24), StringAlignment.Near, StringAlignment.Center);
            }
            for (int i = 0; i < _targets.Count; i++)
            {
                RectangleF row = RowRect(i);
                bool on = _selected == i;
                Theme.FillRounded(g, on ? Theme.BlueSoft : (i == _hover ? Theme.Hover : Theme.Surface), row, 10f);
                Theme.StrokeRounded(g, on ? Theme.Blue : Theme.Border, row, 10f, 1f);
                RectangleF radio = new RectangleF(row.X + 12, row.Y + 12, 18, 18);
                Theme.FillRounded(g, on ? Theme.Blue : Theme.Card, radio, 9f);
                if (on)
                {
                    using (SolidBrush b = new SolidBrush(Color.White))
                    {
                        g.FillEllipse(b, radio.X + 6, radio.Y + 6, 6, 6);
                    }
                }
                else
                {
                    Theme.StrokeRounded(g, Theme.BorderStrong, radio, 9f, 1f);
                }
                {
                    Font f = Theme.Font(10f, FontStyle.Bold);
                    Theme.DrawText(g, _drives[i], f, Theme.Text,
                        new RectangleF(row.X + 40, row.Y, 80, row.Height), StringAlignment.Near,
                        StringAlignment.Center);
                }
                {
                    string free = "—";
                    try
                    {
                        DriveInfo di = new DriveInfo(_drives[i]);
                        free = Loc.F("migrate.dlg.free", Theme.Bytes(di.AvailableFreeSpace));
                    }
                    catch
                    {
                    }
                    Font f = Theme.Font(8.2f, FontStyle.Regular);
                    Theme.DrawText(g, free + "  ·  " + _targets[i], f, Theme.SubText,
                        new RectangleF(row.X + 110, row.Y, row.Width - 124, row.Height), StringAlignment.Near,
                        StringAlignment.Center);
                }
            }

            RectangleF cr = CustomRect;
            bool customOn = _selected < 0 && _custom.Length > 0;
            Theme.FillRounded(g, customOn ? Theme.BlueSoft : (_hover == 100 ? Theme.Hover : Theme.Surface), cr, 10f);
            Theme.StrokeRounded(g, customOn ? Theme.Blue : Theme.Border, cr, 10f, 1f);
            {
                Font f = Theme.Font(8.6f, FontStyle.Bold);
                string txt = _custom.Length > 0 ? Loc.T("migrate.dlg.custom") + "  " + _custom : Loc.T("migrate.dlg.custom");
                Theme.DrawText(g, txt, f, customOn ? Theme.Blue : Theme.SubText,
                    new RectangleF(cr.X + 14, cr.Y, cr.Width - 28, cr.Height), StringAlignment.Near,
                    StringAlignment.Center);
            }
            {
                Font f = Theme.Font(7.8f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("migrate.dlg.hint"), f, Theme.Muted,
                    new RectangleF(26, LHeight - 110, LWidth - 52, 40), StringAlignment.Near,
                    StringAlignment.Near);
            }
        }
    }

}
