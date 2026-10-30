using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CleanC
{
    public class MainForm : Form, IThemed
    {
        private const int W = 1180;
        private const int H = 650;
        private const int TitleH = 56;
        private const int LeftW = 330;
        private const int LeftX = 22;
        private const int LeftY = 68;
        private const int RightX = 370;
        private const int RightW = 788;
        private const int TitleY = 64;
        private const int SubY = 92;
        private const int RowY = 118;
        private const int ListY = 160;
        private const int ListH = 366;

        private readonly RuleEngine _engine = new RuleEngine();
        private CategoryList _list;
        private ProgramList _plist;
        private RegList _rlist;
        private List<RegItem> _regItems = new List<RegItem>();
        private bool _regLoading;
        private bool _regCleaning;
        private bool _regScanned;
        private bool _verifyAfterScan;
        private bool _verifying;
        private DonutChart _donut;
        private UiButton _scanBtn;
        private UiButton _cleanBtn;
        private UiButton _cancelBtn;
        private UiButton _verifyBtn;
        private SplashOverlay _splash;
        private Segmented _tabSeg;
        private Segmented _levelSeg;
        private Segmented _modeSeg;

        private List<Category> _display = new List<Category>();
        private List<ProgramEntry> _programs = new List<ProgramEntry>();
        private ScanResult _lastScan;
        private CleanResult _lastClean;
        private volatile ScanProgress _liveScan;
        private volatile CleanProgress _liveClean;
        private volatile bool _cancelFlag;
        private bool _hasScan;
        private bool _scanning;
        private bool _cleaning;
        private bool _loadingPrograms;
        private bool _uninstalling;
        private volatile string _uninstallStep;
        private volatile int _uninstallIndex;
        private int _uninstallTotal;
        private bool _uninstallForce;
        private bool _elevating;
        private int _opStart;
        private int _spinMs;
        private int _lastSpinPaint;
        private string _spinKey;
        private bool _isAdmin;
        private readonly bool _demo;

        private int _tab;
        private ScanLevel _level = ScanLevel.Low;
        private UninstallMode _umode = UninstallMode.Normal;

        private long _diskUsed;
        private long _diskTotal;
        private string _rightSub = Loc.T("right.sub");
        private string _scanSummary = "";

        private Rectangle _minRect;
        private Rectangle _closeRect;
        private Rectangle _selectRect;
        private Rectangle _elevateRect;
        private Rectangle _langRect;
        private Rectangle _themeRect;
        private Rectangle _logoRect;
        private bool _hoverMin;
        private bool _hoverClose;
        private bool _hoverSelect;
        private bool _hoverElevate;
        private bool _hoverLang;
        private bool _hoverTheme;
        private bool _hoverLogo;
        private float _langHover;
        private float _themeHover;
        private float _logoHover;
        private string _langHoverKey;
        private string _themeHoverKey;
        private string _logoHoverKey;
        private LangMenuForm _langMenu;
        private ToolTip _tip;

        public static string DemoDialog = "";
        public static bool AutoScan = false;
        /// <summary>帧率探针：启动动画结束后连续重绘 4 秒，统计实际 FPS 后退出。</summary>
        public static bool FpsProbe = false;
        public static string FpsOut = "fpsprobe.json";
        /// <summary>截图用：启动后指定毫秒自动切换一次明暗，验证运行时切换链路。</summary>
        public static int AutoThemeSwitch = 0;
        public static bool ScrollDemo = false;
        public static int StartTab = 0;
        public static int StartLevel = 0;
        /// <summary>启动时的视图：-1 跟随 settings.ini，0 普通模式，1 文件模式。</summary>
        public static int StartView = -1;
        /// <summary>截图调试用：文件地图建好后自动演示 hover / menu / junk / delete。</summary>
        public static string MapDialog = "";

        private Segmented _viewSeg;
        private UiButton _settingsBtn;
        private MigratePanel _migrate;
        private FileMapView _map;
        private int _view;
        private bool _mapShown;
        private MapNode _mapRoot;
        private volatile ScanProgress _liveMap;
        private bool _mapScanning;
        private bool _mapRefreshing;
        private bool _mapCleaning;
        private long _mapCleanDone;
        private long _mapCleanTotal;
        private string _mapSub = "";
        private FileMenuForm _fileMenu;
        private MapNode _clipCutNode;
        private string _viewSlideKey;
        private Timer _demoTimer;
        private bool _fpsRunning;
        private int _fpsFrames;
        private int _fpsStart;
        private long _paintTicks;
        private int _paintSamples;
        private long _tickStart;
        private long _profBg;
        private long _profTitle;
        private long _profLeft;
        private long _profBody;

        public MainForm()
        {
            _demo = false;
            Initialize();
        }

        public MainForm(bool demo)
        {
            _demo = demo;
            Initialize();
        }

        private void Initialize()
        {
            _spinKey = Ui.Key("spin");
            _langHoverKey = Ui.Key("langHover");
            _themeHoverKey = Ui.Key("themeHover");
            _logoHoverKey = Ui.Key("logoHover");
            _viewSlideKey = Ui.Key("viewSlide");
            _isAdmin = Native.IsAdmin();
            _tab = StartTab < 0 || StartTab > 3 ? 0 : StartTab;
            _level = (ScanLevel)Math.Max(0, Math.Min(3, StartLevel));
            _view = StartView == 1 ? 1 : 0; // 默认始终进入普通模式，CLI --mode file 可直达文件模式
            Text = Loc.T("app.name");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Px(W), Px(H));
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            ImeMode = ImeMode.Disable;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            _minRect = new Rectangle(W - 104, 8, 46, 40);
            _closeRect = new Rectangle(W - 58, 8, 46, 40);
            _selectRect = new Rectangle(RightX + RightW - 158, 66, 158, 28);
            _elevateRect = new Rectangle(LeftX + LeftW - 94, LeftY + 344, 66, 22);
            _langRect = new Rectangle(W - 246, 12, 104, 32);
            _themeRect = new Rectangle(W - 296, 12, 40, 32);
            _logoRect = new Rectangle(24, 12, 32, 32);

            _tip = new ToolTip();
            _tip.InitialDelay = 380;
            _tip.ReshowDelay = 120;

            _tabSeg = new Segmented();
            _tabSeg.Filled = false;
            _tabSeg.ShowTrack = true;
            _tabSeg.FontSize = 9.2f;
            _tabSeg.Radius = 10f;
            _tabSeg.Bounds = PxRect(298, 11, 446, 34);
            _tabSeg.BackColor = Theme.Bg;
            _tabSeg.SetItems(TabLabels(), _tab);
            _tabSeg.Changed = delegate { SwitchTab(_tabSeg.SelectedIndex); };
            Controls.Add(_tabSeg);

            _levelSeg = new Segmented();
            _levelSeg.Filled = true;
            _levelSeg.FontSize = 8.8f;
            _levelSeg.Accent = Theme.Blue;
            _levelSeg.ItemColors = new Color[]
            {
                Theme.Teal, Theme.Blue, Theme.Violet, Theme.Rose
            };
            _levelSeg.Bounds = PxRect(420, RowY, 358, 32);
            _levelSeg.BackColor = Theme.Bg;
            _levelSeg.SetItems(new string[]
            {
                Loc.T("level.low"), Loc.T("level.medium"), Loc.T("level.high"), Loc.T("level.extreme")
            }, (int)_level);
            _levelSeg.Changed = delegate { OnLevelChanged(); };
            Controls.Add(_levelSeg);

            _modeSeg = new Segmented();
            _modeSeg.Filled = true;
            _modeSeg.FontSize = 8.8f;
            _modeSeg.Accent = Theme.Blue;
            _modeSeg.ItemColors = new Color[] { Theme.Blue, Theme.Rose };
            _modeSeg.Bounds = PxRect(420, RowY, 276, 32);
            _modeSeg.BackColor = Theme.Bg;
            _modeSeg.SetItems(new string[] { Loc.T("uninstall.mode.normal"), Loc.T("uninstall.mode.force") }, 0);
            _modeSeg.Changed = delegate { OnModeChanged(); };
            _modeSeg.Visible = false;
            Controls.Add(_modeSeg);

            _donut = new DonutChart();
            _donut.Bounds = PxRect(LeftX + 63, 110, 204, 204);
            _donut.BackColor = Theme.Panel;
            Controls.Add(_donut);

            _list = new CategoryList();
            _list.Bounds = PxRect(RightX, ListY, RightW, ListH);
            _list.SelectionChanged = OnSelectionChanged;
            _list.DetailsRequested = ShowDetails;
            Controls.Add(_list);
            _list.Visible = false;

            _plist = new ProgramList();
            _plist.Bounds = PxRect(RightX, ListY, RightW, ListH);
            _plist.SelectionChanged = OnSelectionChanged;
            _plist.DetailsRequested = ShowProgramDetails;
            Controls.Add(_plist);
            _plist.Visible = false;

            _rlist = new RegList();
            _rlist.Bounds = PxRect(RightX, ListY, RightW, ListH);
            _rlist.SelectionChanged = OnSelectionChanged;
            Controls.Add(_rlist);
            _rlist.Visible = false;

            _viewSeg = new Segmented();
            _viewSeg.Filled = true;
            _viewSeg.FontSize = 9.2f;
            _viewSeg.Radius = 12f;
            _viewSeg.Accent = Theme.Blue;
            _viewSeg.ItemColors = new Color[] { Theme.Blue, Theme.Cyan };
            _viewSeg.Bounds = PxRect(W / 2 - 138, 598, 180, 30);
            _viewSeg.BackColor = Theme.Bg;
            _viewSeg.SetItems(new string[] { Loc.T("view.normal"), Loc.T("view.file") }, _view);
            _viewSeg.Changed = delegate { SwitchView(_viewSeg.SelectedIndex); };
            Controls.Add(_viewSeg);

            _settingsBtn = new UiButton();
            _settingsBtn.Primary = false;
            _settingsBtn.Glyph = "\uE713";
            _settingsBtn.Text = Loc.T("settings.btn");
            _settingsBtn.Radius = 10f;
            _settingsBtn.Bounds = PxRect(W / 2 + 52, 598, 88, 30);
            _settingsBtn.BackColor = Theme.Bg;
            _settingsBtn.Click += delegate { OpenSettings(); };
            Controls.Add(_settingsBtn);
            _tip.SetToolTip(_settingsBtn, Loc.T("settings.tip"));

            _scanBtn = new UiButton();
            _scanBtn.Primary = true;
            _scanBtn.Text = Loc.T("btn.scan");
            _scanBtn.Glyph = "\uE721";
            _scanBtn.Bounds = PxRect(LeftX + 22, 474, LeftW - 44, 50);
            _scanBtn.BackColor = Theme.Panel;
            _scanBtn.Click += delegate { OnPrimaryClick(); };
            Controls.Add(_scanBtn);

            _cleanBtn = new UiButton();
            _cleanBtn.Primary = true;
            _cleanBtn.Text = Loc.T("btn.clean");
            _cleanBtn.Glyph = "\uE74D";
            _cleanBtn.Bounds = PxRect(RightX + RightW - 210, 544, 210, 44);
            _cleanBtn.BackColor = Theme.Bg;
            _cleanBtn.Enabled = false;
            _cleanBtn.Visible = false;
            _cleanBtn.Click += delegate { OnActionClick(); };
            Controls.Add(_cleanBtn);

            _cancelBtn = new UiButton();
            _cancelBtn.Primary = false;
            _cancelBtn.Text = Loc.T("btn.cancel");
            _cancelBtn.Glyph = "\uE711";
            _cancelBtn.Bounds = PxRect(RightX + RightW / 2 - 70, 506, 140, 42);
            _cancelBtn.BackColor = Theme.Bg;
            _cancelBtn.Visible = false;
            _cancelBtn.Click += delegate { RequestCancel(); };
            Controls.Add(_cancelBtn);

            _verifyBtn = new UiButton();
            _verifyBtn.Primary = false;
            _verifyBtn.Text = Loc.T("btn.verify");
            _verifyBtn.Glyph = "\uE9D5";
            _verifyBtn.Bounds = PxRect(RightX + RightW - 210 - 138, 551, 126, 32);
            _verifyBtn.BackColor = Theme.Bg;
            _verifyBtn.Visible = false;
            _verifyBtn.Click += delegate { RunVerification(); };
            Controls.Add(_verifyBtn);

            _migrate = new MigratePanel();
            _migrate.Bounds = PxRect(RightX, 112, RightW, 484);
            _migrate.BackColor = Theme.Bg;
            _migrate.Visible = false;
            _migrate.Changed = delegate
            {
                if (_tab == 3)
                {
                    _rightSub = _migrate.StatusLine();
                    ApplyTabChrome();
                    Invalidate();
                }
            };
            Controls.Add(_migrate);

            _map = new FileMapView();
            _map.Bounds = PxRect(22, 108, 1136, 458);
            _map.BackColor = Theme.Bg;
            _map.Visible = false;
            _map.OpenRequested = OpenMapNode;
            _map.ZoomRequested = OnMapZoom;
            _map.RefreshRequested = delegate { StartMapScan(); };
            _map.MenuRequested = ShowFileMenu;
            Controls.Add(_map);

            _splash = new SplashOverlay(OnSplashDone);
            _splash.Dock = DockStyle.Fill;
            Controls.Add(_splash);
            _splash.BringToFront();

            ApplyTabChrome();
            ApplyViewChrome();
        }

        /// <summary>四个页签的文案。初始化与切换语言共用，避免只重建一部分导致页签丢失。</summary>
        private static string[] TabLabels()
        {
            return new string[] { Loc.T("tab.clean"), Loc.T("tab.uninstall"), Loc.T("tab.registry"), Loc.T("tab.migrate") };
        }

        /// <summary>切换页签时同步分段控件与主按钮状态（不触发加载）。</summary>
        private void ApplyTabChrome()
        {
            _levelSeg.Visible = _tab == 0;
            _modeSeg.Visible = _tab == 1;
            if (_tab == 0)
            {
                _scanBtn.Text = _hasScan ? Loc.T("btn.rescan") : Loc.T("btn.scan");
                _scanBtn.Glyph = "\uE721";
            }
            else if (_tab == 1)
            {
                _scanBtn.Text = Loc.T("uninstall.refresh");
                _scanBtn.Glyph = "\uE72C";
            }
            else if (_tab == 2)
            {
                _scanBtn.Text = _regItems.Count > 0 ? Loc.T("regtab.rescan") : Loc.T("regtab.scan");
                _scanBtn.Glyph = "\uE721";
            }
            else
            {
                _scanBtn.Text = _migrate.HasScanned ? Loc.T("migrate.rescan") : Loc.T("migrate.scan");
                _scanBtn.Glyph = "\uE8B7";
            }
        }

        // ---------------------------------------------------------------- view mode

        /// <summary>普通模式 / 文件模式的控件显隐。文件模式占满整个窗口（左面板隐藏）。</summary>
        private void ApplyViewChrome()
        {
            bool file = _view == 1;
            _viewSeg.Visible = true;
            _settingsBtn.Visible = true;
            _tabSeg.Visible = !file;
            _levelSeg.Visible = !file && _tab == 0;
            _modeSeg.Visible = !file && _tab == 1;
            _scanBtn.Visible = !file;
            _donut.Visible = !file && _tab == 0;
            _list.Visible = !file && _tab == 0 && _display.Count > 0 && !_loadingPrograms;
            _plist.Visible = !file && _tab == 1 && _programs.Count > 0 && !_loadingPrograms;
            _rlist.Visible = !file && _tab == 2 && _regItems.Count > 0;
            _migrate.Visible = !file && _tab == 3;
            if (file)
            {
                _cleanBtn.Visible = false;
                _verifyBtn.Visible = false;
            }
            bool mapBusy = _mapScanning || _mapRefreshing || _mapCleaning;
            _map.Visible = _mapShown && !mapBusy;
            if (_map.Visible) _map.BringToFront();
            if (!file) RefreshActionButton();
        }

        private void SwitchView(int view)
        {
            if (view < 0 || view > 1) return;
            if (view == _view)
            {
                _viewSeg.SetSelected(_view, true);
                return;
            }
            bool busy = _scanning || _cleaning || _uninstalling || _loadingPrograms || _regLoading ||
                _regCleaning || _verifying || _mapScanning || _mapRefreshing || _mapCleaning;
            if (busy)
            {
                _viewSeg.SetSelected(_view, true);
                return;
            }
            _view = view;
            _viewSeg.Flash();
            if (view == 1)
            {
                _mapShown = true;
                ApplyViewChrome();
                if (_mapRoot == null)
                {
                    StartMapScan();
                }
                else
                {
                    _mapSub = BuildMapSub();
                    _map.Visible = true;
                    _map.BringToFront();
                    _map.BeginIntro();
                    AnimateMapSlide(true);
                }
            }
            else
            {
                _mapShown = _mapRoot != null;
                ApplyViewChrome();
                AnimateMapSlide(false);
            }
            Invalidate();
        }

        private void AnimateMapSlide(bool slideIn)
        {
            Anim.Stop(_viewSlideKey);
            float from = slideIn ? 148f : 0f;
            float to = slideIn ? 0f : 148f;
            int duration = slideIn ? 620 : 300;
            SetMapSlide(from);
            Anim.Loop(_viewSlideKey, delegate(float ms)
            {
                float t = Math.Min(1f, ms / duration);
                float v;
                if (slideIn)
                {
                    // 滑入带轻微回弹：先冲过目标位置一点，再退回。
                    v = from + (to - from) * Anim.EaseOutBack(t);
                }
                else
                {
                    v = from + (to - from) * Anim.EaseInOut(t);
                }
                SetMapSlide(v);
                if (t >= 1f)
                {
                    Anim.Stop(_viewSlideKey);
                    if (!slideIn)
                    {
                        _mapShown = false;
                        _map.Visible = false;
                        _mapSub = "";
                    }
                    SetMapSlide(to);
                    ApplyViewChrome();
                    Invalidate();
                }
            });
        }

        private void SetMapSlide(float off)
        {
            _map.Bounds = PxRect(22 + (int)Math.Round(off), 108, 1136, 458);
            _map.Invalidate();
            Invalidate();
        }

        /// <summary>扫描完 / 钻入后副标题：当前区块的文件数、体积、受限目录。</summary>
        private string BuildMapSub()
        {
            MapNode v = _map.ViewRoot != null ? _map.ViewRoot : _mapRoot;
            if (v == null) return Loc.T("map.scanning");
            string s = Loc.F("map.subDone", Theme.Count(v.FileCount), Theme.Bytes(v.Size));
            if (_mapRoot != null && _mapRoot.Denied > 0) s += Loc.F("map.denied", _mapRoot.Denied);
            if (_mapRoot != null && _mapRoot.Capped) s += " · " + Loc.T("map.truncated");
            return s;
        }

        private void StartMapScan()
        {
            if (_mapScanning || _mapRefreshing || _mapCleaning) return;
            _mapScanning = true;
            _mapShown = true;
            _cancelFlag = false;
            _liveMap = null;
            _mapSub = Loc.T("map.scanning");
            ApplyViewChrome();
            _cancelBtn.Visible = true;
            _cancelBtn.Enabled = true;
            _cancelBtn.Text = Loc.T("btn.cancelScan");
            _cancelBtn.Glyph = "\uE711";
            _viewSeg.Enabled = false;
            _settingsBtn.Enabled = false;
            _opStart = Environment.TickCount;
            StartSpinner();
            Invalidate();

            Task.Run(delegate
            {
                MapNode root = null;
                Exception error = null;
                try
                {
                    root = FileMapScanner.Scan(@"C:\", _engine, OnMapProgress,
                        delegate { return _cancelFlag; });
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                try
                {
                    BeginInvoke(new Action(delegate { FinishMapScan(root, error); }));
                }
                catch
                {
                }
            });
        }

        private void OnMapProgress(ScanProgress p)
        {
            _liveMap = p;
        }

        private void FinishMapScan(MapNode root, Exception error)
        {
            _mapScanning = false;
            StopSpinner();
            _cancelBtn.Visible = false;
            _viewSeg.Enabled = true;
            _settingsBtn.Enabled = true;
            if (error != null)
            {
                _mapSub = Loc.F("map.scanError", error.Message);
                _mapShown = true;
                _map.RefreshView();
                ApplyViewChrome();
                Invalidate();
                return;
            }
            if (root == null)
            {
                _mapSub = _mapRoot != null ? BuildMapSub() : Loc.T("map.empty");
                _mapShown = true;
                _map.RefreshView();
                ApplyViewChrome();
                Invalidate();
                return;
            }
            _mapRoot = root;
            _map.SetMap(root);
            _mapSub = BuildMapSub();
            ApplyViewChrome();
            if (_view == 1) AnimateMapSlide(true);
            Invalidate();
            MaybeDemoMapDialog(root);
        }

        /// <summary>截图调试入口：真实扫描完成后演示一个交互状态，不影响正常运行。</summary>
        private void MaybeDemoMapDialog(MapNode root)
        {
            if (MapDialog.Length == 0) return;
            MapNode pick = null;
            List<MapNode> stack = new List<MapNode>();
            stack.Add(root);
            while (stack.Count > 0)
            {
                MapNode n = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                if (n.IsDir && n.Children != null)
                {
                    for (int i = 0; i < n.Children.Count; i++) stack.Add(n.Children[i]);
                }
                if (MapDialog == "junk")
                {
                    if (pick == null && n != root && n.IsDir && n.Agg[(int)FileRisk.Safe] >= 1048576L) pick = n;
                }
                else if (pick == null && n != root && n.IsDir && n.Risk == FileRisk.Risky && n.Size >= 2147483648L)
                {
                    pick = n;
                }
            }
            if (pick == null) pick = root;
            MapNode target = pick;
            if (MapDialog == "intro")
            {
                _map.DemoFreezeIntro(300);
                return;
            }
            if (MapDialog == "slide")
            {
                _map.BeginIntro();
                AnimateMapSlide(true);
                _demoTimer = new Timer();
                _demoTimer.Interval = 1500;
                _demoTimer.Tick += delegate
                {
                    _map.BeginIntro();
                    AnimateMapSlide(true);
                };
                _demoTimer.Start();
                return;
            }
            Timer t = new Timer();
            t.Interval = 700;
            t.Tick += delegate
            {
                t.Stop();
                t.Dispose();
                if (target == _mapRoot && !target.IsDir) return;
                if (MapDialog == "hover") _map.DemoHover(target, new Point(660, 300));
                else if (MapDialog == "menu") ShowFileMenu(target,
                    PointToScreen(new Point(Px(620), Px(300))));
                else if (MapDialog == "junk") DoMapJunk(target);
                else if (MapDialog == "delete") DoMapDelete(target);
                Invalidate();
            };
            t.Start();
        }

        private void StartMapRefresh(MapNode node)
        {
            if (node == null || _mapRefreshing || _mapScanning || _mapCleaning) return;
            _mapRefreshing = true;
            _cancelFlag = false;
            _liveMap = null;
            _mapSub = Loc.T("map.refreshing");
            ApplyViewChrome();
            _cancelBtn.Visible = true;
            _cancelBtn.Enabled = true;
            _cancelBtn.Text = Loc.T("btn.cancelScan");
            _cancelBtn.Glyph = "\uE711";
            _viewSeg.Enabled = false;
            _settingsBtn.Enabled = false;
            _opStart = Environment.TickCount;
            StartSpinner();
            Invalidate();

            Task.Run(delegate
            {
                bool ok = false;
                Exception error = null;
                try
                {
                    ok = FileMapScanner.RescanInto(node, _engine, OnMapProgress,
                        delegate { return _cancelFlag; });
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                try
                {
                    BeginInvoke(new Action(delegate { FinishMapRefresh(node, ok, error); }));
                }
                catch
                {
                }
            });
        }

        private void FinishMapRefresh(MapNode node, bool ok, Exception error)
        {
            _mapRefreshing = false;
            StopSpinner();
            _cancelBtn.Visible = false;
            _viewSeg.Enabled = true;
            _settingsBtn.Enabled = true;
            if (ok && error == null)
            {
                _map.RefreshView();
                _mapSub = BuildMapSub();
            }
            else if (error != null)
            {
                _mapSub = Loc.F("map.scanError", error.Message);
            }
            else
            {
                _mapSub = BuildMapSub();
            }
            ApplyViewChrome();
            if (_view == 1 && ok && error == null) AnimateMapSlide(true);
            Invalidate();
        }

        private void StartMapClean(MapNode node, FileOps.JunkReport report, List<string> cats)
        {
            if (_mapCleaning || report == null) return;
            _mapCleaning = true;
            _cancelFlag = false;
            _mapCleanDone = 0;
            long total = 0;
            for (int i = 0; i < report.Groups.Count; i++)
            {
                if (cats.Contains(report.Groups[i].CatId)) total += report.Groups[i].Bytes;
            }
            _mapCleanTotal = total;
            ApplyViewChrome();
            _cancelBtn.Visible = true;
            _cancelBtn.Enabled = true;
            _cancelBtn.Text = Loc.T("btn.cancelClean");
            _cancelBtn.Glyph = "\uE711";
            _viewSeg.Enabled = false;
            _settingsBtn.Enabled = false;
            _opStart = Environment.TickCount;
            StartSpinner();
            Invalidate();

            Task.Run(delegate
            {
                FileOps.JunkOutcome outcome = null;
                Exception error = null;
                try
                {
                    outcome = FileOps.DeleteJunk(_engine, report, cats,
                        delegate(long done, long tot) { _mapCleanDone = done; _mapCleanTotal = tot; },
                        delegate { return _cancelFlag; });
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                try
                {
                    BeginInvoke(new Action(delegate { FinishMapClean(node, outcome, error); }));
                }
                catch
                {
                }
            });
        }

        private void FinishMapClean(MapNode node, FileOps.JunkOutcome outcome, Exception error)
        {
            _mapCleaning = false;
            StopSpinner();
            _cancelBtn.Visible = false;
            _viewSeg.Enabled = true;
            _settingsBtn.Enabled = true;
            if (outcome != null)
            {
                for (int i = 0; i < outcome.DeletedNodes.Count; i++)
                {
                    MapTree.RemoveAndPrune(outcome.DeletedNodes[i]);
                }
                if (node != null) MapTree.PruneEmpty(node);
                _map.RefreshView();
                _mapSub = BuildMapSub();
            }
            ApplyViewChrome();
            Invalidate();
            if (error != null)
            {
                MessageFile(Loc.T("map.op.failTitle"), new string[] { error.Message });
                return;
            }
            if (outcome == null) return;
            string[] lines =
            {
                Loc.F("junk.done", outcome.Deleted, Theme.Bytes(outcome.Bytes), outcome.Skipped),
                outcome.LogPath.Length > 0 ? Loc.F("junk.log", outcome.LogPath) : ""
            };
            MessageFile(Loc.T("junk.doneTitle"), lines);
        }

        private void OpenMapNode(MapNode node)
        {
            if (node == null) return;
            if (!Directory.Exists(node.Path) && !File.Exists(node.Path)) return;
            if (node.IsDir) Native.OpenFolder(node.Path);
            else Native.ShowInExplorer(node.Path);
        }

        private void OnMapZoom(MapNode node)
        {
            _mapSub = BuildMapSub();
            Invalidate();
        }

        private void ShowFileMenu(MapNode node, Point screen)
        {
            if (node == null) return;
            if (_fileMenu != null && !_fileMenu.IsDisposed) _fileMenu.Close();
            bool top = node == _mapRoot || node.Parent == _mapRoot;
            bool black = node.Risk == FileRisk.Forbidden;
            bool canModify = !black && !top;
            bool cutFlag;
            bool hasClip = FileOps.ClipboardHasFiles(out cutFlag);
            string target = FileOps.TargetDir(node);
            bool canPaste = hasClip && !string.IsNullOrEmpty(target) && Directory.Exists(target) &&
                !_engine.IsHardBlocked(target);
            string[] labels =
            {
                Loc.T("menu.delete"), Loc.T("menu.cut"), Loc.T("menu.paste"),
                Loc.T("menu.copy"), Loc.T("menu.props"), Loc.T("menu.junk")
            };
            string[] glyphs =
            {
                "\uE74D", "\uE8C6", "\uE77F", "\uE8C8", "\uE946", "\uE9D5"
            };
            bool[] enabled = { canModify, canModify, canPaste, true, true, true };
            bool[] danger = { true, false, false, false, false, false };
            FileMenuForm f = new FileMenuForm(labels, glyphs, enabled, danger,
                delegate(int i) { OnFileMenuPick(node, i); });
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            int x = screen.X;
            int y = screen.Y;
            if (MapDialog != "menu")
            {
                if (x + f.Width > wa.Right) x = wa.Right - f.Width - 4;
                if (y + f.Height > wa.Bottom) y = wa.Bottom - f.Height - 4;
                if (x < wa.Left) x = wa.Left;
                if (y < wa.Top) y = wa.Top;
            }
            f.Location = new Point(x, y);
            f.FormClosed += delegate { _fileMenu = null; };
            f.Show(this);
            _fileMenu = f;
        }

        private void OnFileMenuPick(MapNode node, int index)
        {
            if (index == 0) DoMapDelete(node);
            else if (index == 1) DoMapCut(node);
            else if (index == 2) DoMapPaste(node);
            else if (index == 3) DoMapCopy(node);
            else if (index == 4) DoMapProps(node);
            else if (index == 5) DoMapJunk(node);
        }

        private bool ConfirmFile(string title, string[] lines, bool danger, string okText)
        {
            MapConfirmDialog d = new MapConfirmDialog(title, lines, danger, okText, false);
            d.ShowDialog(this);
            return d.DialogResult == DialogResult.OK;
        }

        private void MessageFile(string title, string[] lines)
        {
            MapConfirmDialog d = new MapConfirmDialog(title, lines, false, Loc.T("btn.ok"), true);
            d.ShowDialog(this);
        }

        private void DoMapDelete(MapNode node)
        {
            if (node == null) return;
            if (node == _mapRoot || node.Parent == _mapRoot) return;
            if (node.Risk == FileRisk.Forbidden) return;
            bool danger = node.Risk == FileRisk.Risky;
            string[] lines =
            {
                node.Path,
                Loc.F("map.del.size", Theme.Bytes(node.Size), Theme.Count(node.FileCount)),
                Loc.T("map.del.permanent"),
                danger ? Loc.T("map.del.risky")
                    : (node.Risk == FileRisk.Caution ? Loc.T("map.del.caution") : Loc.T("map.del.safe"))
            };
            if (!ConfirmFile(Loc.T("menu.delete"), lines, danger, Loc.T("map.del.ok"))) return;
            string error;
            if (!FileOps.DeletePath(node.Path, out error))
            {
                MessageFile(Loc.T("map.op.failTitle"), new string[] { node.Path, error });
                return;
            }
            MapNode parent = node.Parent;
            if (node == _map.ViewRoot && parent != null) _map.SetViewRoot(parent);
            MapTree.RemoveAndPrune(node);
            if (parent != null) MapTree.PruneEmpty(parent);
            _map.RefreshView();
            _mapSub = BuildMapSub();
            Invalidate();
        }

        private void DoMapCut(MapNode node)
        {
            if (node == null) return;
            if (node == _mapRoot || node.Parent == _mapRoot) return;
            if (node.Risk == FileRisk.Forbidden) return;
            if (node.Risk == FileRisk.Risky)
            {
                string[] lines = { node.Path, Loc.T("map.cut.risky") };
                if (!ConfirmFile(Loc.T("menu.cut"), lines, true, Loc.T("map.cut.ok"))) return;
            }
            FileOps.CopyToClipboard(node.Path, true);
            _clipCutNode = node;
        }

        private void DoMapCopy(MapNode node)
        {
            if (node == null) return;
            FileOps.CopyToClipboard(node.Path, false);
        }

        private void DoMapPaste(MapNode node)
        {
            if (node == null) return;
            MapNode target = node.IsDir ? node : node.Parent;
            if (target == null) return;
            if (_engine.IsHardBlocked(target.Path))
            {
                MessageFile(Loc.T("menu.paste"), new string[] { Loc.T("map.paste.black") });
                return;
            }
            FileOps.PasteResult pr = FileOps.PasteInto(target.Path);
            if (!pr.Ok)
            {
                MessageFile(Loc.T("map.op.failTitle"), new string[] { pr.Error });
                return;
            }
            if (pr.Cut && _clipCutNode != null)
            {
                MapNode cut = _clipCutNode;
                if (!Directory.Exists(cut.Path) && !File.Exists(cut.Path))
                {
                    MapTree.RemoveAndPrune(cut);
                    _clipCutNode = null;
                }
            }
            StartMapRefresh(target);
        }

        private void DoMapProps(MapNode node)
        {
            if (node == null) return;
            FileOps.ShowProperties(node.Path);
        }

        private void DoMapJunk(MapNode node)
        {
            if (node == null) return;
            FileOps.JunkReport report = FileOps.Analyze(node);
            JunkReportDialog dlg = new JunkReportDialog(node, report);
            dlg.ShowDialog(this);
            if (dlg.DialogResult != DialogResult.OK || !dlg.CleanRequested) return;
            List<string> cats = new List<string>(dlg.SelectedCatIds);
            if (cats.Count == 0) return;
            long bytes = 0;
            long files = 0;
            for (int i = 0; i < report.Groups.Count; i++)
            {
                if (!cats.Contains(report.Groups[i].CatId)) continue;
                bytes += report.Groups[i].Bytes;
                files += report.Groups[i].Files;
            }
            string[] lines =
            {
                Loc.F("junk.confirm", Theme.Count(files), Theme.Bytes(bytes)),
                Loc.T("junk.confirmPermanent"),
                node.Path
            };
            if (!ConfirmFile(Loc.T("menu.junk"), lines, true, Loc.T("junk.confirmOk"))) return;
            StartMapClean(node, report, cats);
        }

        // ---------------------------------------------------------------- theme

        /// <summary>主题切换后重排底色与缓存了颜色的分段控件。</summary>
        public void ApplyTheme()
        {
            BackColor = Theme.Bg;
            _tabSeg.BackColor = Theme.Bg;
            _levelSeg.BackColor = Theme.Bg;
            _modeSeg.BackColor = Theme.Bg;
            _viewSeg.BackColor = Theme.Bg;
            _cleanBtn.BackColor = Theme.Bg;
            _cancelBtn.BackColor = Theme.Bg;
            _verifyBtn.BackColor = Theme.Bg;
            _scanBtn.BackColor = Theme.Panel;
            _levelSeg.Accent = Theme.Blue;
            _levelSeg.ItemColors = new Color[] { Theme.Teal, Theme.Blue, Theme.Violet, Theme.Rose };
            _modeSeg.Accent = Theme.Blue;
            _modeSeg.ItemColors = new Color[] { Theme.Blue, Theme.Rose };
            _viewSeg.Accent = Theme.Blue;
            _viewSeg.ItemColors = new Color[] { Theme.Blue, Theme.Cyan };
            if (_map != null) _map.ApplyTheme();
            if (_migrate != null) _migrate.ApplyTheme();
            _engine.ApplyThemeAccents();
            if (_display.Count > 0) _list.RefreshView();
            UpdateThemeTip();
            Invalidate();
        }

        private void ToggleTheme()
        {
            Theme.Toggle();
            Invalidate();
        }

        private void UpdateThemeTip()
        {
            if (_tip == null) return;
            string key = Theme.Mode == ThemeMode.Dark ? "theme.toGold" :
                (Theme.Mode == ThemeMode.Gold ? "theme.toLight" : "theme.toDark");
            _tip.SetToolTip(this, _hoverTheme ? Loc.T(key) : "");
        }

        private void PaintThemeButton(Graphics g)
        {
            RectangleF r = new RectangleF(_themeRect.X, _themeRect.Y, _themeRect.Width, _themeRect.Height);
            float hv = _themeHover;
            Color accent = Theme.Mode == ThemeMode.Dark ? Theme.Gold :
                (Theme.Mode == ThemeMode.Gold ? Theme.Blue : Theme.Blue);
            Theme.FillRounded(g, Theme.Mix(Theme.Card, Theme.Mix(Theme.BlueSoft, accent, 0.1f), 0.5f + 0.5f * hv),
                r, 10f);
            Theme.StrokeRounded(g, Theme.Mix(Theme.Border, accent, 0.28f + hv * 0.62f), r, 10f, 1f);

            Color ink = Theme.Mix(Theme.SubText, accent, 0.55f + hv * 0.45f);
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            GraphicsState st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(-14f + 14f * hv);
            g.ScaleTransform(0.9f + 0.1f * hv, 0.9f + 0.1f * hv);
            using (SolidBrush b = new SolidBrush(ink))
            using (StringFormat sf = new StringFormat())
            {
                Font f = Theme.IconFont(13.5f);
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                // 月亮 = 切到深色，星形 = 切到金色，太阳 = 切回浅色
                string glyph = Theme.Mode == ThemeMode.Dark ? "\uE734" :
                    (Theme.Mode == ThemeMode.Gold ? "\uE708" : "\uE706");
                g.DrawString(glyph, f, b, new RectangleF(-12, -12, 24, 24), sf);
            }
            g.Restore(st);
        }

        private static int Px(float v)
        {
            return (int)Math.Round(v * Theme.Scale);
        }

        private static Rectangle PxRect(int x, int y, int w, int h)
        {
            return new Rectangle(Px(x), Px(y), Px(w), Px(h));
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
            if (_splash != null) _splash.Play();
        }

        private void OnSplashDone()
        {
            _splash = null;
            RefreshDisk(true);
            if (!_demo && !FpsProbe) ShowLetter();
            if (_view == 1)
            {
                _mapShown = true;
                ApplyViewChrome();
                if (_mapRoot == null) StartMapScan();
            }
            else if (_demo)
            {
                if (_tab == 1) LoadDemoPrograms();
                else LoadDemo();
                ShowDemoDialog();
            }
            else if (AutoScan)
            {
                if (_tab == 1) StartLoadPrograms();
                else if (_tab == 2) StartRegScan();
                else if (_tab == 3) _migrate.Rescan();
                else StartScan();
            }
            else if (_tab == 1)
            {
                SwitchTab(1);
            }
            else if (_tab == 2)
            {
                SwitchTab(2);
            }
            else if (_tab == 3)
            {
                SwitchTab(3);
            }
            if (AutoThemeSwitch > 0)
            {
                Timer t = new Timer();
                t.Interval = AutoThemeSwitch;
                t.Tick += delegate
                {
                    t.Stop();
                    t.Dispose();
                    ToggleTheme();
                };
                t.Start();
            }
            if (FpsProbe) StartFpsProbe();
            Invalidate();
        }

        /// <summary>测量主窗口在持续动画时的真实重绘帧率，用于判断是否被 30 帧限制。</summary>
        private void StartFpsProbe()
        {
            _fpsFrames = 0;
            _fpsStart = Environment.TickCount;
            _paintTicks = 0;
            _paintSamples = 0;
            _tickStart = Anim.TickCount;
            _profBg = _profTitle = _profLeft = _profBody = 0;
            _fpsRunning = true;
            Anim.Loop(_spinKey, delegate(float ms)
            {
                _spinMs = (int)ms;
                Invalidate();
                if (ms >= 4000f)
                {
                    _fpsRunning = false;
                    Anim.Stop(_spinKey);
                    int elapsed = Math.Max(1, unchecked(Environment.TickCount - _fpsStart));
                    float fps = _fpsFrames * 1000f / elapsed;
                    float paintMs = _paintSamples == 0 ? 0f :
                        _paintTicks * 1000f / Stopwatch.Frequency / _paintSamples;
                    float fq = Stopwatch.Frequency * (float)Math.Max(1, _paintSamples);
                    float bgMs = _profBg * 1000f / fq;
                    float titleMs = _profTitle * 1000f / fq;
                    float leftMs = _profLeft * 1000f / fq;
                    float bodyMs = _profBody * 1000f / fq;
                    int splashMs = Math.Max(1, SplashOverlay.PlayEnd - SplashOverlay.PlayStart);
                    float splashFps = SplashOverlay.FrameCount * 1000f / splashMs;
                    float splashPaint = SplashOverlay.FrameCount == 0 ? 0f :
                        SplashOverlay.PaintTicks * 1000f / Stopwatch.Frequency / SplashOverlay.FrameCount;
                    float sfq = Stopwatch.Frequency * (float)Math.Max(1, SplashOverlay.FrameCount);
                    float sBg = SplashOverlay.ProfBg * 1000f / sfq;
                    float sC = SplashOverlay.ProfC * 1000f / sfq;
                    float sBox = SplashOverlay.ProfBox * 1000f / sfq;
                    float sFx = SplashOverlay.ProfFx * 1000f / sfq;
                    float sText = SplashOverlay.ProfText * 1000f / sfq;
                    try
                    {
                        string body = "{\"frames\":" + _fpsFrames + ",\"elapsedMs\":" + elapsed +
                            ",\"fps\":" + fps.ToString("0.0") + ",\"paintMs\":" + paintMs.ToString("0.00") +
                            ",\"ticks\":" + (Anim.TickCount - _tickStart) +
                            ",\"bgMs\":" + bgMs.ToString("0.00") + ",\"titleMs\":" + titleMs.ToString("0.00") +
                            ",\"leftMs\":" + leftMs.ToString("0.00") + ",\"bodyMs\":" + bodyMs.ToString("0.00") +
                            ",\"splashFrames\":" + SplashOverlay.FrameCount +
                            ",\"splashFps\":" + splashFps.ToString("0.0") +
                            ",\"splashPaintMs\":" + splashPaint.ToString("0.00") +
                            ",\"splashBgMs\":" + sBg.ToString("0.00") +
                            ",\"splashCMs\":" + sC.ToString("0.00") +
                            ",\"splashBoxMs\":" + sBox.ToString("0.00") +
                            ",\"splashFxMs\":" + sFx.ToString("0.00") +
                            ",\"splashTextMs\":" + sText.ToString("0.00") +
                            "}\n";
                        File.WriteAllText(FpsOut, body, Encoding.UTF8);
                    }
                    catch
                    {
                    }
                    Close();
                }
            });
        }

        // ---------------------------------------------------------------- tabs

        private void SwitchTab(int tab)
        {
            if (_scanning || _cleaning || _uninstalling || _loadingPrograms || _regLoading || _regCleaning)
            {
                _tabSeg.SetSelected(_tab, true);
                return;
            }
            _tab = tab;
            _tabSeg.SetSelected(tab, true);
            _levelSeg.Visible = tab == 0;
            _modeSeg.Visible = tab == 1;
            _list.Visible = tab == 0 && _display.Count > 0 && !_loadingPrograms;
            _plist.Visible = tab == 1 && _programs.Count > 0 && !_loadingPrograms;
            _rlist.Visible = tab == 2 && _regItems.Count > 0;
            _migrate.Visible = tab == 3;
            if (tab == 3) _migrate.Sync();
            _cleanBtn.Visible = false;
            _cancelBtn.Visible = false;
            _verifyBtn.Visible = false;
            _levelSeg.Enabled = true;
            _modeSeg.Enabled = true;
            _donut.Visible = tab == 0;

            if (tab == 0)
            {
                _rightSub = _hasScan ? _scanSummary : Loc.T("right.sub");
            }
            else if (tab == 1)
            {
                _rightSub = _programs.Count > 0 ? BuildProgramSub() : Loc.T("uninstall.subScan");
            }
            else if (tab == 2)
            {
                _rightSub = _regItems.Count > 0 ? BuildRegSub() : Loc.T("regtab.sub");
            }
            else
            {
                _rightSub = _migrate.StatusLine();
            }
            ApplyTabChrome();
            if (tab == 1 && _programs.Count == 0) StartLoadPrograms();
            if (tab == 2 && _regItems.Count == 0 && !_regLoading) StartRegScan();
            if (tab == 3 && !_migrate.HasScanned) _migrate.Rescan();
            ApplyViewChrome();
            Invalidate();
        }

        private void OnLevelChanged()
        {
            _level = (ScanLevel)_levelSeg.SelectedIndex;
            if (_lastScan != null && !_scanning)
            {
                _hasScan = false;
                _display.Clear();
                _list.Visible = false;
                _rightSub = Loc.F("scan.levelChanged", Loc.LevelName(_level));
                _cleanBtn.Visible = false;
            }
            else if (!_scanning && !_hasScan)
            {
                _rightSub = Loc.LevelHint(_level);
            }
            Invalidate();
        }

        private void OnModeChanged()
        {
            _umode = (UninstallMode)_modeSeg.SelectedIndex;
            RefreshActionButton();
            Invalidate();
        }

        private string BuildProgramSub()
        {
            long bytes = 0;
            int prot = 0;
            for (int i = 0; i < _programs.Count; i++)
            {
                if (_programs[i].Protected) prot++;
                else bytes += _programs[i].Bytes;
            }
            return Loc.F("uninstall.sub", _programs.Count, Theme.Bytes(bytes)) +
                (prot > 0 ? Loc.F("uninstall.subProtect", prot) : "");
        }

        private void RefreshDisk(bool animate)
        {
            try
            {
                DriveInfo di = new DriveInfo("C:");
                _diskTotal = di.TotalSize;
                _diskUsed = di.TotalSize - di.TotalFreeSpace;
                _donut.SetData(_diskUsed, _diskTotal, animate);
            }
            catch
            {
            }
        }

        // ---------------------------------------------------------------- clean scan

        private void OnPrimaryClick()
        {
            if (_scanning || _cleaning || _uninstalling || _regLoading || _regCleaning) return;
            if (_loadingPrograms) return;
            if (_tab == 0) StartScan();
            else if (_tab == 1) StartLoadPrograms();
            else if (_tab == 2) StartRegScan();
            else _migrate.Rescan();
        }

        private void StartScan()
        {
            if (_level == ScanLevel.Extreme && !_isAdmin)
            {
                RequestAdminFor(ScanLevel.Extreme);
                if (_elevating) return;
                // 用户选择不提权：仍然继续只读扫描，注册表项标记为「需要管理员」
            }
            _scanning = true;
            _hasScan = false;
            _cancelFlag = false;
            _liveScan = null;
            _lastScan = null;
            _scanSummary = "";
            _rightSub = Loc.T("scan.preparing");
            _display.Clear();
            _list.Visible = false;
            _cleanBtn.Visible = false;
            _scanBtn.Enabled = false;
            _scanBtn.Text = Loc.T("btn.scanning");
            _scanBtn.Glyph = "\uE895";
            _cancelBtn.Visible = true;
            _cancelBtn.Enabled = true;
            _cancelBtn.Text = Loc.T("btn.cancelScan");
            _cancelBtn.Glyph = "\uE711";
            _levelSeg.Enabled = false;
            _opStart = Environment.TickCount;
            StartSpinner();
            Invalidate();

            ScanLevel level = _level;
            bool admin = _isAdmin;
            Task.Run(delegate
            {
                ScanResult r = null;
                List<RegItem> junk = null;
                Exception error = null;
                try
                {
                    r = Scanner.ScanByLevel(_engine, level, admin, OnScanProgress, delegate { return _cancelFlag; });
                    if (level == ScanLevel.Extreme && !_cancelFlag)
                    {
                        junk = RegistryJunk.Scan(OnJunkStep, delegate { return _cancelFlag; });
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                try
                {
                    BeginInvoke(new Action(delegate { FinishScan(r, junk, error); }));
                }
                catch
                {
                }
            });
        }

        private void OnScanProgress(ScanProgress p)
        {
            _liveScan = p;
        }

        private void OnJunkStep(int step, string stage)
        {
            ScanProgress p = new ScanProgress();
            p.CurrentPath = stage;
            p.Stage = stage;
            p.Passes = 1;
            p.ElapsedMs = Environment.TickCount - _opStart;
            _liveScan = p;
        }

        private void FinishScan(ScanResult r, List<RegItem> junk, Exception error)
        {
            _scanning = false;
            StopSpinner();
            _cancelBtn.Visible = false;
            _scanBtn.Enabled = true;
            _levelSeg.Enabled = true;
            _scanBtn.Text = Loc.T("btn.rescan");
            _scanBtn.Glyph = "\uE721";

            if (error != null)
            {
                _rightSub = Loc.F("sum.scanError", error.Message);
                _hasScan = false;
                Invalidate();
                return;
            }

            _lastScan = r;
            _hasScan = r != null;
            if (r == null) return;

            Category reg = _engine.Get("reg_junk");
            if (reg != null)
            {
                reg.RegItems.Clear();
                if (junk != null) reg.RegItems.AddRange(junk);
                // 极限档始终显示「注册表垃圾」这一行，即便结果是 0 项，用户才知道检查过
                reg.Present = r.Level == ScanLevel.Extreme;
                reg.FileCount = reg.RegItems.Count;
                reg.Bytes = 0;
                reg.Selected = reg.RegItems.Count > 0;
                reg.Restricted = !_isAdmin;
            }

            _scanSummary = BuildScanSummary(r);
            if (reg != null && reg.RegItems.Count > 0)
            {
                _scanSummary += Loc.F("sum.regJunk", reg.RegItems.Count);
            }
            _rightSub = _scanSummary;
            BuildDisplay();
            _list.SetCategories(_display);
            _list.Visible = _tab == 0;
            _plist.Visible = false;
            RefreshActionButton();
            RefreshDisk(true);
            UpdateSelection();
            Invalidate();
        }

        private static string BuildScanSummary(ScanResult r)
        {
            string s = Loc.F("sum.level", Loc.LevelName(r.Level));
            s += Loc.F("sum.scan", Theme.Count(r.TotalFiles));
            if (r.Canceled) s += Loc.T("sum.canceled");
            else s += Loc.F("sum.time", Theme.Duration(r.ElapsedMs));
            if (r.Passes > 1) s += Loc.F("sum.passes", r.Passes);
            if (r.DeniedDirs > 0) s += Loc.F("sum.denied", r.DeniedDirs);
            if (r.Restricted > 0) s += Loc.F("sum.restricted", r.Restricted);
            return s;
        }

        private void BuildDisplay()
        {
            List<Category> all = _engine.Categories;
            List<Category> temp = new List<Category>();
            for (int i = 0; i < all.Count; i++)
            {
                Category c = all[i];
                if (!c.Present) continue;
                if (!c.Registry && c.Bytes <= 0 && c.FileCount <= 0 && c.Busy <= 0) continue;
                if (c.Id == "recycle_bin" && c.FileCount <= 0) continue;
                temp.Add(c);
            }
            temp.Sort(delegate(Category a, Category b)
            {
                if (_level == ScanLevel.Extreme && a.Stage != b.Stage) return a.Stage - b.Stage;
                int ra = RiskRank(a.Risk);
                int rb = RiskRank(b.Risk);
                if (ra != rb) return ra - rb;
                if (b.Bytes > a.Bytes) return 1;
                if (b.Bytes < a.Bytes) return -1;
                return 0;
            });
            _display = temp;
            _list.ShowOrder = _level == ScanLevel.Extreme;
        }

        private static int RiskRank(RiskLevel r)
        {
            if (r == RiskLevel.Safe) return 0;
            if (r == RiskLevel.Caution) return 1;
            if (r == RiskLevel.Protected) return 2;
            return 3;
        }

        private void OnSelectionChanged()
        {
            UpdateSelection();
            Invalidate();
        }

        private void UpdateSelection()
        {
            RefreshActionButton();
        }

        private int SelectedCount()
        {
            if (_tab == 2)
            {
                int r = 0;
                for (int i = 0; i < _regItems.Count; i++)
                {
                    if (_regItems[i].Selected) r++;
                }
                return r;
            }
            if (_tab == 1)
            {
                int n = 0;
                for (int i = 0; i < _programs.Count; i++)
                {
                    if (_programs[i].Selected && !_programs[i].Protected) n++;
                }
                return n;
            }
            int c = 0;
            for (int i = 0; i < _display.Count; i++)
            {
                Category x = _display[i];
                if (!x.Actionable || !x.Selected) continue;
                if (x.Registry ? x.RegItems.Count > 0 : x.Bytes > 0) c++;
            }
            return c;
        }

        private long SelectedBytes()
        {
            long b = 0;
            for (int i = 0; i < _display.Count; i++)
            {
                Category x = _display[i];
                if (x.Actionable && x.Selected && !x.Registry && x.Bytes > 0) b += x.Bytes;
            }
            return b;
        }

        private void RefreshActionButton()
        {
            if (_view == 1)
            {
                _cleanBtn.Visible = false;
                _verifyBtn.Visible = false;
                return;
            }
            bool busy = _scanning || _cleaning || _uninstalling || _loadingPrograms || _regLoading || _regCleaning || _verifying;
            if (_tab == 2)
            {
                _cleanBtn.Visible = _regItems.Count > 0 && !busy;
                _cleanBtn.Danger = false;
                _cleanBtn.Text = Loc.T("regtab.btnClean");
                _cleanBtn.Glyph = "\uE74D";
                _cleanBtn.Enabled = !busy && SelectedCount() > 0;
                _verifyBtn.Visible = _regScanned && !busy;
                _verifyBtn.Enabled = !busy;
                _verifyBtn.Text = Loc.T("btn.verify");
                return;
            }
            _verifyBtn.Visible = false;
            if (_tab == 0)
            {
                _cleanBtn.Visible = _hasScan && !busy;
                _cleanBtn.Danger = false;
                _cleanBtn.Text = Loc.T("btn.clean");
                _cleanBtn.Glyph = "\uE74D";
                _cleanBtn.Enabled = !busy && SelectedCount() > 0;
            }
            else
            {
                _cleanBtn.Visible = _programs.Count > 0 && !busy;
                bool force = _umode == UninstallMode.Force;
                _cleanBtn.Danger = force;
                _cleanBtn.Text = force ? Loc.T("uninstall.btnForce") : Loc.T("uninstall.btn");
                _cleanBtn.Glyph = force ? "\uE74D" : "\uE8B8";
                _cleanBtn.Enabled = !busy && SelectedCount() > 0;
            }
        }

        private void OnActionClick()
        {
            if (_tab == 0) StartClean();
            else if (_tab == 1) StartUninstall();
            else StartRegClean();
        }

        // ---------------------------------------------------------------- clean

        private void StartClean()
        {
            if (_scanning || _cleaning || !_hasScan) return;
            List<Category> sel = new List<Category>();
            for (int i = 0; i < _display.Count; i++)
            {
                Category c = _display[i];
                if (!c.Actionable || !c.Selected) continue;
                if (c.Registry)
                {
                    if (c.RegItems.Count > 0) sel.Add(c);
                }
                else if (c.Bytes > 0)
                {
                    sel.Add(c);
                }
            }
            if (sel.Count == 0) return;
            sel.Sort(delegate(Category a, Category b) { return a.Stage.CompareTo(b.Stage); });

            bool hasJunk = false;
            for (int i = 0; i < sel.Count; i++) if (sel[i].Registry) hasJunk = true;
            if (hasJunk && !_isAdmin)
            {
                RequestAdminFor(ScanLevel.Extreme);
                return;
            }

            ConfirmDialog dlg = new ConfirmDialog(sel);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            if (hasJunk)
            {
                Category reg = _engine.Get("reg_junk");
                int n = reg == null ? 0 : reg.RegItems.Count;
                string[] items =
                {
                    Loc.F("consent.itemRegDelete", n),
                    Loc.F("consent.itemBackup", Uninstaller.BackupDir()),
                    Loc.T("consent.itemRegScope"),
                    Loc.T("consent.itemNoSystem")
                };
                ConsentDialog cd = new ConsentDialog(Loc.T("dlg.consent"), Loc.T("consent.lead"), items,
                    Loc.T("consent.warn"), Loc.T("consent.check"), Loc.T("consent.ok"), true);
                if (cd.ShowDialog(this) != DialogResult.OK) return;
            }

            _cleaning = true;
            _cancelFlag = false;
            _liveClean = null;
            _opStart = Environment.TickCount;
            _list.Visible = false;
            _cleanBtn.Visible = false;
            _scanBtn.Enabled = false;
            _scanBtn.Text = Loc.T("clean.title");
            _cancelBtn.Visible = true;
            _cancelBtn.Enabled = true;
            _cancelBtn.Text = Loc.T("btn.cancelClean");
            _levelSeg.Enabled = false;
            StartSpinner();
            Invalidate();

            bool admin = _isAdmin;
            bool consent = hasJunk;
            Task.Run(delegate
            {
                CleanResult cr = null;
                Exception error = null;
                try
                {
                    cr = Cleaner.Clean(_engine, sel, admin, consent, OnCleanProgress, delegate { return _cancelFlag; });
                    for (int i = 0; i < sel.Count; i++)
                    {
                        if (sel[i].Registry) continue;
                        Scanner.MeasureCategory(_engine, sel[i]);
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                try
                {
                    BeginInvoke(new Action(delegate { FinishClean(cr, sel, error); }));
                }
                catch
                {
                }
            });
        }

        private void OnCleanProgress(CleanProgress p)
        {
            _liveClean = p;
        }

        private void FinishClean(CleanResult cr, List<Category> sel, Exception error)
        {
            _cleaning = false;
            StopSpinner();
            _cancelBtn.Visible = false;
            _scanBtn.Enabled = true;
            _levelSeg.Enabled = true;
            _scanBtn.Text = Loc.T("btn.rescan");
            _cleanBtn.Visible = true;
            if (error != null)
            {
                _rightSub = Loc.F("sum.cleanError", error.Message);
                _list.Visible = true;
                UpdateSelection();
                Invalidate();
                return;
            }
            _lastClean = cr;
            long totalFree = cr.FreedBytes + cr.RecycledBytes;
            _rightSub = Loc.F("sum.lastClean", Theme.Bytes(totalFree), Theme.Count(cr.Deleted));
            Category reg = _engine.Get("reg_junk");
            if (reg != null)
            {
                reg.Present = false;
                reg.RegItems.Clear();
                reg.FileCount = 0;
            }
            BuildDisplay();
            _list.SetCategories(_display);
            _list.Visible = true;
            RefreshDisk(true);
            UpdateSelection();
            Invalidate();

            ResultDialog rd = new ResultDialog(cr, _diskUsed);
            rd.ShowDialog(this);
        }

        // ---------------------------------------------------------------- uninstall

        private void StartLoadPrograms()
        {
            if (_loadingPrograms || _uninstalling) return;
            _loadingPrograms = true;
            _cancelFlag = false;
            _opStart = Environment.TickCount;
            _rightSub = Loc.T("uninstall.subScan");
            _programs = new List<ProgramEntry>();
            _plist.Visible = false;
            _cleanBtn.Visible = false;
            _scanBtn.Enabled = false;
            _scanBtn.Text = Loc.T("uninstall.loading");
            _cancelBtn.Visible = true;
            _cancelBtn.Enabled = true;
            _cancelBtn.Text = Loc.T("btn.cancel");
            _modeSeg.Enabled = false;
            StartSpinner();
            Invalidate();

            Task.Run(delegate
            {
                List<ProgramEntry> items = null;
                Exception error = null;
                try
                {
                    items = Uninstaller.Enumerate(OnProgramProgress, delegate { return _cancelFlag; });
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                try
                {
                    BeginInvoke(new Action(delegate { FinishLoadPrograms(items, error); }));
                }
                catch
                {
                }
            });
        }

        private void OnProgramProgress(int done, int total)
        {
            ScanProgress p = new ScanProgress();
            p.Files = done;
            p.Passes = 1;
            p.CurrentPath = total > 0 ? done + " / " + total : "";
            p.ElapsedMs = Environment.TickCount - _opStart;
            _liveScan = p;
        }

        private void FinishLoadPrograms(List<ProgramEntry> items, Exception error)
        {
            _loadingPrograms = false;
            StopSpinner();
            _cancelBtn.Visible = false;
            _scanBtn.Enabled = true;
            _modeSeg.Enabled = true;
            _scanBtn.Text = Loc.T("uninstall.refresh");
            _scanBtn.Glyph = "\uE72C";
            if (error != null)
            {
                _rightSub = Loc.F("sum.scanError", error.Message);
                Invalidate();
                return;
            }
            _programs = items == null ? new List<ProgramEntry>() : items;
            _rightSub = _programs.Count > 0 ? BuildProgramSub() : Loc.T("uninstall.emptySub");
            _plist.SetPrograms(_programs);
            _plist.Visible = _programs.Count > 0;
            RefreshActionButton();
            Invalidate();
        }

        private void ShowProgramDetails(ProgramEntry p)
        {
            if (_uninstalling || _loadingPrograms) return;
            ProgramDialog d = new ProgramDialog(p);
            d.ShowDialog(this);
        }

        private void StartUninstall()
        {
            if (_uninstalling || _loadingPrograms) return;
            List<ProgramEntry> sel = new List<ProgramEntry>();
            for (int i = 0; i < _programs.Count; i++)
            {
                if (_programs[i].Selected && !_programs[i].Protected) sel.Add(_programs[i]);
            }
            if (sel.Count == 0) return;

            bool force = _umode == UninstallMode.Force;
            if (force && !_isAdmin)
            {
                if (!RequestAdminForUninstall()) return;
                return;
            }

            if (force && sel.Count == 1)
            {
                UninstallDialog d = new UninstallDialog(sel[0], true);
                if (d.ShowDialog(this) != DialogResult.OK) return;
            }
            else if (!force && sel.Count == 1)
            {
                UninstallDialog d = new UninstallDialog(sel[0], false);
                if (d.ShowDialog(this) != DialogResult.OK) return;
            }
            else
            {
                List<string> items = new List<string>();
                for (int i = 0; i < sel.Count && i < 5; i++)
                {
                    items.Add(sel[i].Name + "  ·  " + Theme.Bytes(sel[i].Bytes));
                }
                if (sel.Count > 5) items.Add(Loc.F("confirm.more", sel.Count - 5));
                items.Add(force ? Loc.T("uconfirm.warnForce") : Loc.T("uconfirm.warnNormal"));
                ConsentDialog cd = new ConsentDialog(force ? Loc.T("dlg.forceUninstall") : Loc.T("dlg.uninstall"),
                    Loc.T("consent.lead"), items.ToArray(),
                    force ? Loc.T("uconfirm.warnForce") : Loc.T("uconfirm.warnNormal"),
                    Loc.T("uconfirm.check"), force ? Loc.T("uninstall.btnForce") : Loc.T("uconfirm.ok"), force);
                if (cd.ShowDialog(this) != DialogResult.OK) return;
            }

            _uninstalling = true;
            _cancelFlag = false;
            _uninstallForce = force;
            _uninstallTotal = sel.Count;
            _uninstallIndex = 0;
            _uninstallStep = Loc.T("uninstall.stepFiles");
            _opStart = Environment.TickCount;
            _plist.Visible = false;
            _cleanBtn.Visible = false;
            _scanBtn.Enabled = false;
            _scanBtn.Text = Loc.T("dlg.uninstall");
            _cancelBtn.Visible = true;
            _cancelBtn.Enabled = true;
            _cancelBtn.Text = Loc.T("btn.cancel");
            _modeSeg.Enabled = false;
            StartSpinner();
            Invalidate();

            Task.Run(delegate
            {
                List<UninstallReport> reports = new List<UninstallReport>();
                Exception error = null;
                string logPath = "";
                StreamWriter log = null;
                try
                {
                    logPath = UninstallLogPath();
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                    log = new StreamWriter(logPath, false, Encoding.UTF8);
                    log.WriteLine("CleanC uninstall  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    log.WriteLine("mode=" + (force ? "force" : "normal") + "  admin=" + _isAdmin);
                    log.WriteLine("--------------------------------------------------");
                    for (int i = 0; i < sel.Count; i++)
                    {
                        if (_cancelFlag) break;
                        _uninstallIndex = i;
                        ProgramEntry p = sel[i];
                        log.WriteLine("### " + p.Name + "  " + p.InstallDir);
                        UninstallReport rep;
                        if (force)
                        {
                            rep = Uninstaller.ForceRemove(p, _isAdmin, log, OnUninstallStep, delegate { return _cancelFlag; });
                        }
                        else
                        {
                            rep = Uninstaller.NormalRemove(p, _isAdmin, log, OnUninstallStep, delegate { return _cancelFlag; });
                        }
                        reports.Add(rep);
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    if (log != null)
                    {
                        try
                        {
                            log.WriteLine("--------------------------------------------------");
                            log.Dispose();
                        }
                        catch
                        {
                        }
                    }
                }
                try
                {
                    BeginInvoke(new Action(delegate { FinishUninstall(reports, sel, logPath, error); }));
                }
                catch
                {
                }
            });
        }

        private void OnUninstallStep(int step, string label)
        {
            _uninstallStep = label;
        }

        private void FinishUninstall(List<UninstallReport> reports, List<ProgramEntry> sel, string logPath, Exception error)
        {
            _uninstalling = false;
            StopSpinner();
            _cancelBtn.Visible = false;
            _scanBtn.Enabled = true;
            _modeSeg.Enabled = true;
            _scanBtn.Text = Loc.T("uninstall.refresh");
            _scanBtn.Glyph = "\uE72C";

            if (error != null)
            {
                _rightSub = Loc.F("sum.cleanError", error.Message);
                _plist.Visible = true;
                RefreshActionButton();
                Invalidate();
                return;
            }

            CleanResult cr = new CleanResult();
            cr.LogPath = logPath;
            for (int i = 0; i < reports.Count; i++)
            {
                UninstallReport r = reports[i];
                cr.FreedBytes += r.BytesFreed;
                cr.Deleted += r.FilesDeleted;
                cr.Skipped += r.Failures;
                cr.RegKeys += r.RegKeys;
                if (r.BackupDir.Length > 0) cr.BackupDir = r.BackupDir;
                for (int m = 0; m < r.Messages.Count && cr.Failures.Count < 200; m++)
                {
                    cr.Failures.Add(r.Messages[m]);
                }
            }
            cr.Canceled = _cancelFlag;

            _rightSub = Loc.F("uninstall.selected", sel.Count);
            for (int i = 0; i < sel.Count; i++)
            {
                ProgramEntry p = sel[i];
                p.Selected = false;
                UninstallReport rep = i < reports.Count ? reports[i] : null;
                bool filesGone = rep != null && !rep.Refused && rep.FilesDeleted > 0;
                if (!_uninstallForce && filesGone)
                {
                    // 普通删除只清文件，保留条目，方便立刻切换到强力删除清理注册表
                    p.FilesRemoved = true;
                    p.Bytes = 0;
                    p.FileCount = 0;
                    p.Measured = true;
                }
                else
                {
                    _programs.Remove(p);
                }
            }
            _plist.SetPrograms(_programs);
            _plist.Visible = _programs.Count > 0;
            RefreshActionButton();
            Invalidate();

            string bodySub = _uninstallForce
                ? Loc.F("uninstall.resultSub", Theme.Count(cr.Deleted))
                : Loc.T("uninstall.resultKeepReg");
            ResultDialog rd = new ResultDialog(cr, _diskUsed, Loc.T("uninstall.finished"), bodySub);
            rd.ShowDialog(this);
        }

        private static string UninstallLogPath()
        {
            string dir;
            try { dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
            catch { dir = Path.GetTempPath(); }
            return Path.Combine(Path.Combine(Path.Combine(dir, "CleanC"), "logs"),
                "uninstall_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
        }

        // ---------------------------------------------------------------- registry tab

        private string BuildRegSub()
        {
            int sel = 0;
            int reboot = 0;
            for (int i = 0; i < _regItems.Count; i++)
            {
                if (_regItems[i].Selected) sel++;
                if (_regItems[i].NeedsReboot) reboot++;
            }
            string s = Loc.F("regtab.subCount", _regItems.Count, sel);
            if (reboot > 0) s += Loc.F("regtab.subReboot", reboot);
            return s;
        }

        private void StartRegScan()
        {
            if (_regLoading || _regCleaning) return;
            _regLoading = true;
            _cancelFlag = false;
            _liveScan = null;
            _regItems = new List<RegItem>();
            _rlist.SetItems(_regItems);
            _rlist.Visible = false;
            _cleanBtn.Visible = false;
            _scanBtn.Enabled = false;
            _scanBtn.Text = Loc.T("btn.scanning");
            _scanBtn.Glyph = "\uE895";
            _cancelBtn.Visible = true;
            _cancelBtn.Enabled = true;
            _cancelBtn.Text = Loc.T("btn.cancelScan");
            _cancelBtn.Glyph = "\uE711";
            _rightSub = Loc.T("regtab.scanning");
            _opStart = Environment.TickCount;
            StartSpinner();
            Invalidate();

            Task.Run(delegate
            {
                List<RegItem> items = null;
                Exception err = null;
                try
                {
                    items = RegistryJunk.Scan(OnJunkStep, delegate { return _cancelFlag; });
                }
                catch (Exception ex)
                {
                    err = ex;
                }
                try
                {
                    BeginInvoke(new Action(delegate { FinishRegScan(items, err); }));
                }
                catch
                {
                }
            });
        }

        private void FinishRegScan(List<RegItem> items, Exception err)
        {
            _regLoading = false;
            StopSpinner();
            _cancelBtn.Visible = false;
            _scanBtn.Enabled = true;
            _scanBtn.Text = _regItems != null && _regItems.Count > 0 ? Loc.T("regtab.rescan") : Loc.T("regtab.scan");
            _scanBtn.Glyph = "\uE721";
            if (err != null)
            {
                _rightSub = Loc.F("sum.scanError", err.Message);
                Invalidate();
                return;
            }
            _regItems = items == null ? new List<RegItem>() : items;
            _regScanned = true;
            _rlist.SetItems(_regItems);
            _rlist.Visible = _regItems.Count > 0 && _tab == 2;
            _rightSub = _regItems.Count > 0 ? BuildRegSub() : Loc.T("regtab.none");
            _scanBtn.Text = Loc.T("regtab.rescan");
            RefreshActionButton();
            Invalidate();
            if (_verifyAfterScan)
            {
                _verifyAfterScan = false;
                RunVerification();
            }
        }

        private void StartRegClean()
        {
            List<RegItem> sel = new List<RegItem>();
            for (int i = 0; i < _regItems.Count; i++)
            {
                if (_regItems[i].Selected) sel.Add(_regItems[i]);
            }
            if (sel.Count == 0) return;

            if (!_isAdmin)
            {
                RequestAdminForRegistry();
                return;
            }

            string[] items =
            {
                Loc.F("consent.itemRegCount", sel.Count),
                Loc.F("consent.itemRegBackup", Uninstaller.BackupDir()),
                Loc.T("consent.itemRestorePoint"),
                Loc.T("consent.itemRegCmd"),
                Loc.T("consent.itemRegReboot")
            };
            ConsentDialog cd = new ConsentDialog(Loc.T("consent.regTitle"), Loc.T("consent.regLead"), items,
                Loc.T("consent.warn"), Loc.T("consent.check"), Loc.T("consent.ok"), false);
            if (cd.ShowDialog(this) != DialogResult.OK) return;

            RunRegClean(sel);
        }

        private void RunRegClean(List<RegItem> sel)
        {
            _regCleaning = true;
            _cancelFlag = false;
            _cleanBtn.Visible = false;
            _scanBtn.Enabled = false;
            _scanBtn.Text = Loc.T("regtab.running");
            _rightSub = Loc.T("regtab.runningSub");
            _opStart = Environment.TickCount;
            StartSpinner();
            Invalidate();

            Task.Run(delegate
            {
                RegRunResult rr = null;
                Exception err = null;
                string backup = "";
                bool restorePoint = false;
                try
                {
                    backup = Uninstaller.BackupDir();
                    string logDir = Path.Combine(Path.Combine(Uninstaller.BackupDir(), ".."), "logs");
                    Directory.CreateDirectory(logDir);
                    string logPath = Path.Combine(logDir, "reg_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
                    using (StreamWriter log = new StreamWriter(logPath, false, Encoding.UTF8))
                    {
                        restorePoint = Safety.TryCreateRestorePoint("CleanC 残留清理", log);
                        rr = RegRunner.Run(sel, backup, true, log);
                        log.WriteLine("BAT " + rr.BatPath);
                        if (rr.DriverBatPath.Length > 0) log.WriteLine("DRIVER-BAT " + rr.DriverBatPath);
                        log.WriteLine("deleted=" + rr.Deleted + " failed=" + rr.Failed + " skipped=" + rr.Skipped);
                    }
                }
                catch (Exception ex)
                {
                    err = ex;
                }
                try
                {
                    bool rp = restorePoint;
                    BeginInvoke(new Action(delegate { FinishRegClean(rr, err, backup, sel.Count, rp); }));
                }
                catch
                {
                }
            });
        }

        private void FinishRegClean(RegRunResult rr, Exception err, string backup, int total, bool restorePoint)
        {
            _regCleaning = false;
            StopSpinner();
            _scanBtn.Enabled = true;
            _scanBtn.Text = Loc.T("regtab.rescan");
            _cleanBtn.Visible = false;

            if (err != null)
            {
                _rightSub = Loc.F("sum.scanError", err.Message);
                Invalidate();
                return;
            }

            CleanResult cr = new CleanResult();
            if (rr != null)
            {
                cr.Deleted = rr.Deleted;
                cr.RegKeys = rr.Keys;
                cr.RegValues = rr.Values;
                cr.Skipped = rr.Failed + rr.Skipped;
            }
            cr.BackupDir = backup;
            string sub = Loc.F("regtab.doneSub", rr == null ? 0 : rr.Deleted, rr == null ? total : rr.Failed)
                + (restorePoint ? Loc.T("regtab.rpOk") : Loc.T("regtab.rpFail"));
            ResultDialog rd = new ResultDialog(cr, _diskUsed, Loc.T("regtab.done"), sub);
            rd.ShowDialog(this);

            _verifyAfterScan = true;
            StartRegScan();
        }

        /// <summary>第 16 步：清理后验证。快速复查任务 / 防火墙 / 服务 / 事件日志 / 隐藏设备。</summary>
        private void RunVerification()
        {
            if (_verifying) return;
            _verifying = true;
            _verifyBtn.Enabled = false;
            RefreshActionButton();
            Invalidate();
            int remaining = _regItems.Count;
            Task.Run(delegate
            {
                List<VerifyCheck> checks = null;
                Exception err = null;
                try
                {
                    string dir = Path.Combine(Path.Combine(Uninstaller.BackupDir(), ".."), "logs");
                    Directory.CreateDirectory(dir);
                    string logPath = Path.Combine(dir, "verify_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
                    using (StreamWriter log = new StreamWriter(logPath, false, Encoding.UTF8))
                    {
                        log.WriteLine("CleanC verify " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        log.WriteLine("remaining=" + remaining);
                        checks = ResidualVerify.Quick(remaining, log);
                    }
                }
                catch (Exception ex)
                {
                    err = ex;
                }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        _verifying = false;
                        RefreshActionButton();
                        Invalidate();
                        if (err != null)
                        {
                            _rightSub = Loc.F("sum.scanError", err.Message);
                            Invalidate();
                            return;
                        }
                        VerifyDialog d = new VerifyDialog(checks);
                        d.ShowDialog(this);
                    }));
                }
                catch
                {
                }
            });
        }

        // ---------------------------------------------------------------- admin / consent

        private bool RequestAdminFor(ScanLevel level)
        {
            string[] items =
            {
                Loc.T("consent.itemElevate"),
                Loc.T("consent.itemScan3"),
                Loc.T("consent.itemRegistryScan"),
                Loc.T("consent.itemRegistryLater")
            };
            ConsentDialog cd = new ConsentDialog(Loc.T("consent.adminTitle"), Loc.T("consent.lead"), items,
                Loc.T("consent.warn"), Loc.T("consent.check"), Loc.T("consent.ok"), false);
            if (cd.ShowDialog(this) != DialogResult.OK)
            {
                // 不提权也允许只读扫描，只是注册表项会显示“需要管理员”
                return false;
            }
            string args = "--elevated --tab 0 --level " + (int)level + " --autoscan";
            if (Native.Elevate(args))
            {
                _elevating = true;
                Close();
                return false;
            }
            MessageBox.Show(Loc.T("consent.elevateFail"), Loc.T("consent.adminTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private bool RequestAdminForRegistry()
        {
            string[] items =
            {
                Loc.T("consent.itemElevate"),
                Loc.T("consent.itemRegScan"),
                Loc.T("consent.itemRegBackup"),
                Loc.T("consent.itemRegCmd")
            };
            ConsentDialog cd = new ConsentDialog(Loc.T("consent.adminTitle"), Loc.T("consent.lead"), items,
                Loc.T("consent.warn"), Loc.T("consent.check"), Loc.T("consent.ok"), false);
            if (cd.ShowDialog(this) != DialogResult.OK)
            {
                _rightSub = Loc.T("reg.needAdmin");
                Invalidate();
                return false;
            }
            if (Native.Elevate("--elevated --tab 2 --autoscan"))
            {
                _elevating = true;
                Close();
                return false;
            }
            MessageBox.Show(Loc.T("consent.elevateFail"), Loc.T("consent.adminTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private bool RequestAdminForUninstall()
        {
            string[] items =
            {
                Loc.T("consent.itemElevate"),
                Loc.T("consent.itemKill"),
                Loc.T("consent.itemForceFiles"),
                Loc.T("consent.itemRegDeleteShort")
            };
            ConsentDialog cd = new ConsentDialog(Loc.T("consent.adminTitle"), Loc.T("consent.lead"), items,
                Loc.T("consent.warn"), Loc.T("consent.check"), Loc.T("consent.ok"), true);
            if (cd.ShowDialog(this) != DialogResult.OK)
            {
                _rightSub = Loc.T("uninstall.elevateHint");
                Invalidate();
                return false;
            }
            if (Native.Elevate("--elevated --tab 1"))
            {
                _elevating = true;
                Close();
                return false;
            }
            MessageBox.Show(Loc.T("consent.elevateFail"), Loc.T("consent.adminTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // ---------------------------------------------------------------- dialogs

        /// <summary>启动动画结束后弹出“给用户的一封信”。</summary>
        private void ShowLetter()
        {
            WelcomeDialog d = new WelcomeDialog();
            d.ShowDialog(this);
        }

        private void ShowDemoDialog()
        {
            if (DemoDialog.Length == 0) return;
            if (DemoDialog == "detail")
            {
                Category c = _engine.Get("user_temp");
                if (c != null)
                {
                    DetailDialog d = new DetailDialog(c);
                    d.ShowDialog(this);
                }
            }
            else if (DemoDialog == "confirm")
            {
                List<Category> sel = new List<Category>();
                for (int i = 0; i < _display.Count; i++)
                {
                    if (_display[i].Actionable && _display[i].Selected && _display[i].Bytes > 0) sel.Add(_display[i]);
                }
                if (sel.Count > 0)
                {
                    ConfirmDialog d = new ConfirmDialog(sel);
                    d.ShowDialog(this);
                }
            }
            else if (DemoDialog == "result")
            {
                CleanResult r = new CleanResult();
                r.FreedBytes = 2102L * 1024 * 1024;
                r.RecycledBytes = 92L * 1024 * 1024;
                r.Deleted = 8213;
                r.Skipped = 3;
                r.LogPath = Path.Combine(Path.GetTempPath(), "cleanc-demo.log");
                ResultDialog d = new ResultDialog(r, 0);
                d.ShowDialog(this);
            }
            else if (DemoDialog == "consent")
            {
                string[] items =
                {
                    Loc.T("consent.itemElevate"),
                    Loc.T("consent.itemScan3"),
                    Loc.T("consent.itemRegistryScan"),
                    Loc.T("consent.itemRegistryLater")
                };
                ConsentDialog d = new ConsentDialog(Loc.T("consent.adminTitle"), Loc.T("consent.lead"), items,
                    Loc.T("consent.warn"), Loc.T("consent.check"), Loc.T("consent.ok"), false);
                d.ShowDialog(this);
            }
            else if (DemoDialog == "uninstall" && _programs.Count > 0)
            {
                UninstallDialog d = new UninstallDialog(_programs[0], false);
                d.ShowDialog(this);
            }
            else if (DemoDialog == "force" && _programs.Count > 0)
            {
                UninstallDialog d = new UninstallDialog(_programs[0], true);
                d.ShowDialog(this);
            }
            else if (DemoDialog == "settings")
            {
                SettingsDialog d = new SettingsDialog(delegate(string code) { ChangeLanguage(code); });
                d.ShowDialog(this);
            }
            else if (DemoDialog == "letter")
            {
                WelcomeDialog d = new WelcomeDialog();
                d.ShowDialog(this);
            }
            else if (DemoDialog == "verify")
            {
                List<VerifyCheck> checks = new List<VerifyCheck>();
                VerifyCheck a = new VerifyCheck();
                a.Label = Loc.T("verify.checkLeftovers");
                a.Value = Loc.T("verify.stateOk");
                checks.Add(a);
                VerifyCheck b = new VerifyCheck();
                b.Label = Loc.T("verify.checkTasks");
                b.Value = Loc.T("verify.stateOk");
                checks.Add(b);
                VerifyCheck c = new VerifyCheck();
                c.Label = Loc.T("verify.checkFirewall");
                c.Value = Loc.T("verify.stateOk");
                checks.Add(c);
                VerifyCheck e = new VerifyCheck();
                e.Label = Loc.T("verify.checkServices");
                e.Value = Loc.T("verify.stateOk");
                checks.Add(e);
                VerifyCheck f = new VerifyCheck();
                f.Label = Loc.T("verify.checkEventLog");
                f.Value = Loc.T("verify.stateOk");
                checks.Add(f);
                VerifyCheck g = new VerifyCheck();
                g.Label = Loc.T("verify.checkDevices");
                g.Value = Loc.T("verify.stateUnknown");
                g.State = 2;
                checks.Add(g);
                VerifyDialog d = new VerifyDialog(checks);
                d.ShowDialog(this);
            }
        }

        private void LoadDemo()
        {
            DemoCategory("user_temp", 1972L * 1024 * 1024, 8213);
            DemoCategory("wu_cache", 432L * 1024 * 1024, 10);
            DemoCategory("browser_cache", 781L * 1024 * 1024, 3834);
            DemoCategory("app_cache", 624L * 1024 * 1024, 2640);
            DemoCategory("thumb_cache", 12L * 1024 * 1024, 30);
            DemoCategory("crash_dump", 74L * 1024, 1);
            DemoCategory("recycle_bin", 1260L * 1024 * 1024, 42);
            DemoCategory("sys_logs", 92L * 1024 * 1024, 587);
            DemoCategory("system_core", 27392L * 1024 * 1024, 148645);
            DemoCategory("programs", 7373L * 1024 * 1024, 7791);
            DemoCategory("user_files", 20451L * 1024 * 1024, 209672);
            DemoCategory("installer", 826L * 1024 * 1024, 214);
            DemoCategory("pagefile", 2960L * 1024 * 1024, 1);
            DemoCategory("other", 300L * 1024 * 1024, 12);

            Category reg = _engine.Get("reg_junk");
            if (reg != null)
            {
                reg.Present = true;
                reg.FileCount = 12;
                reg.RegItems.Clear();
                DemoReg(reg, "夸克网盘", Loc.T("reg.reasonOrphan"), RegKind.Uninstall, 8, true, "夸克网盘");
                DemoReg(reg, "Kimi.exe", Loc.F("reg.reasonRun", @"C:\Users\Sample\AppData\Local\Programs\Kimi\Kimi.exe"),
                    RegKind.Run, 5, true, "Run");
                DemoReg(reg, "MuMuDownloader", Loc.F("reg.reasonTask", @"C:\Users\Sample\AppData\Local\Temp\7z5\MuMuDownloader.exe"),
                    RegKind.Task, 4, true, "\\MuMu\\MuMuDownloader");
                DemoReg(reg, "FanQieClean", Loc.F("reg.reasonFirewall", @"E:\FanQieClean\FanQieClean.exe"),
                    RegKind.Firewall, 6, true, "FanQieClean\u0001E:\\FanQieClean\\FanQieClean.exe\u0001in");
                DemoReg(reg, "QuarkDriveSvc", Loc.F("reg.reasonService", @"C:\Program Files\Quark\QuarkDriveSvc.exe"),
                    RegKind.Service, 7, false, "QuarkDriveSvc");
                DemoReg(reg, "AlibabaProtectDT", Loc.F("reg.reasonFolder", @"C:\ProgramData\Alibaba\AlibabaProtectDT"),
                    RegKind.Folder, 2, false, "");
                DemoReg(reg, "Quark", Loc.T("reg.reasonOrphanKey"), RegKind.MainKey, 9, true, "");
                DemoReg(reg, "{B8453BE8-4D2E-4165-B37D-38EE104BAE84}",
                    Loc.F("reg.reasonMenu", "{B8453BE8-4D2E-4165-B37D-38EE104BAE84}"),
                    RegKind.ContextMenu, 10, false, "");
                DemoReg(reg, "oem47.inf", Loc.F("reg.reasonDriverPkg", "oem47.inf"), RegKind.Driver, 12, false, "oem47.inf");
                DemoReg(reg, "ihmafllikibpmigkcoadcmckbfhibefp",
                    Loc.F("reg.reasonBrowser", "ihmafllikibpmigcoadcmckbfhibefp"), RegKind.BrowserExt, 13, false, "");
                DemoReg(reg, "Kimi.lnk", Loc.F("reg.reasonShortcut", @"C:\Program Files\Kimi\Kimi.exe"),
                    RegKind.Shortcut, 14, false, "");
                DemoReg(reg, "Kimi", Loc.F("reg.reasonRegisteredApp", @"Software\Classes\Applications\Kimi.exe\Capabilities"),
                    RegKind.OpenWith, 14, false, "");
            }

            _hasScan = true;
            _rightSub = Loc.F("sum.scan", "378,796") + Loc.F("sum.time", "37.9 s");
            BuildDisplay();
            _list.SetCategories(_display);
            if (ScrollDemo) _list.ScrollToBottom();
            _list.Visible = _tab == 0;
            if (_tab == 2 && reg != null)
            {
                _regItems = reg.RegItems;
                _regScanned = true;
                _rlist.SetItems(_regItems);
                _rlist.Visible = true;
                _rightSub = BuildRegSub();
            }
            RefreshDisk(false);
            RefreshActionButton();
            UpdateSelection();
            Invalidate();
        }

        private void LoadDemoPrograms()
        {
            DemoProgram("Google Chrome", "Google LLC", "129.0.6668", @"C:\Program Files\Google\Chrome\Application", 1284L * 1024 * 1024, 3120, false, Theme.Blue, "G");
            DemoProgram("Visual Studio Code", "Microsoft Corporation", "1.94.2", @"C:\Program Files\Microsoft VS Code", 486L * 1024 * 1024, 2210, false, Theme.Indigo, "V");
            DemoProgram("Microsoft Visual C++ 2015-2022", "Microsoft Corporation", "14.38.33135", @"C:\Program Files (x86)\Microsoft Visual Studio", 92L * 1024 * 1024, 320, true, Theme.Gray, "M");
            DemoProgram("7-Zip 24.08", "Igor Pavlov", "24.08", @"C:\Program Files\7-Zip", 5L * 1024 * 1024, 24, false, Theme.Teal, "7");
            DemoProgram("Adobe Photoshop 2024", "Adobe Inc.", "25.11.0", @"C:\Program Files\Adobe\Adobe Photoshop 2024", 3841L * 1024 * 1024, 9120, false, Theme.Violet, "A");
            DemoProgram("Spotify", "Spotify AB", "1.2.45", @"C:\Users\Sample\AppData\Roaming\Spotify", 412L * 1024 * 1024, 880, false, Theme.Green, "S");
            DemoProgram("Notepad++", "Notepad++ Team", "8.6.9", @"C:\Program Files\Notepad++", 14L * 1024 * 1024, 42, false, Theme.Cyan, "N");
            DemoProgram("Windows Defender", "Microsoft Corporation", "4.18", @"C:\Program Files\Windows Defender", 206L * 1024 * 1024, 640, true, Theme.Rose, "W");

            _rightSub = BuildProgramSub();
            _plist.SetPrograms(_programs);
            _plist.Visible = true;
            RefreshActionButton();
            RefreshDisk(false);
            Invalidate();
        }

        private void DemoProgram(string name, string pub, string ver, string dir, long bytes, int files,
            bool prot, Color accent, string glyph)
        {
            ProgramEntry p = new ProgramEntry();
            p.Name = name;
            p.Publisher = pub;
            p.Version = ver;
            p.InstallDir = dir;
            p.Bytes = bytes;
            p.FileCount = files;
            p.Measured = true;
            p.Protected = prot;
            p.ProtectedReason = prot ? Loc.T("uninstall.protectMicrosoft") : "";
            p.Accent = accent;
            p.Glyph = glyph;
            p.MachineWide = !dir.StartsWith(@"C:\Users", StringComparison.OrdinalIgnoreCase);
            p.RegKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + name;
            _programs.Add(p);
        }

        private void DemoCategory(string id, long bytes, int files)
        {
            Category c = _engine.Get(id);
            if (c == null) return;
            c.Present = true;
            c.Bytes = bytes;
            c.FileCount = files;

            if (c.Actionable && c.Files.Count == 0)
            {
                string root = c.Roots.Count > 0 ? c.Roots[0] : @"C:\Windows\Temp";
                DemoFile(c, root, "f_000001", 48L * 1024 * 1024);
                DemoFile(c, root, "data_0", 12L * 1024 * 1024);
                DemoFile(c, root, "old.tmp", 3L * 1024 * 1024);
                DemoFile(c, root, "trace.log", 2L * 1024 * 1024);
                DemoFile(c, root, "setup.exe", 9L * 1024 * 1024);
                DemoFile(c, root, "settings.json", 1024 * 1024);
                DemoFile(c, root, "report.pdf", 5L * 1024 * 1024);
            }
        }

        private static void DemoFile(Category c, string root, string name, long size)
        {
            FileEntry e = new FileEntry(Path.Combine(root, name), size);
            FileJudge.Apply(e, c);
            c.Files.Add(e);
        }

        /// <summary>截图用示例残留项：覆盖 16 步流程里的各类条目。</summary>
        private static void DemoReg(Category reg, string label, string reason, RegKind kind,
            int step, bool selected, string aux)
        {
            RegItem it = new RegItem();
            it.Label = label;
            it.Reason = reason;
            it.Risk = RiskLevel.Caution;
            it.Kind = kind;
            it.Step = step;
            it.Selected = selected;
            it.Aux = aux;
            switch (kind)
            {
                case RegKind.Uninstall:
                    it.Hive = "HKEY_CURRENT_USER";
                    it.KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Quark";
                    break;
                case RegKind.Run:
                    it.Hive = "HKEY_CURRENT_USER";
                    it.KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                    it.ValueName = label;
                    break;
                case RegKind.MainKey:
                    it.Hive = "HKEY_CURRENT_USER";
                    it.KeyPath = @"SOFTWARE\Quark";
                    break;
                case RegKind.Service:
                    it.Hive = "HKEY_LOCAL_MACHINE";
                    it.KeyPath = @"SYSTEM\CurrentControlSet\Services\QuarkDriveSvc";
                    break;
                case RegKind.ContextMenu:
                    it.Hive = "HKEY_LOCAL_MACHINE";
                    it.KeyPath = @"SOFTWARE\Classes\FunctionDiscoveryItemBluetooth\shellex\ContextMenuHandlers\{B8453BE8}";
                    break;
                case RegKind.OpenWith:
                    it.Hive = "HKEY_LOCAL_MACHINE";
                    it.KeyPath = @"SOFTWARE\RegisteredApplications";
                    it.ValueName = label;
                    break;
                case RegKind.Folder:
                    it.FilePath = @"C:\ProgramData\Alibaba\AlibabaProtectDT";
                    break;
                case RegKind.Shortcut:
                    it.FilePath = @"C:\Users\Sample\Desktop\Kimi.lnk";
                    break;
                case RegKind.BrowserExt:
                    it.FilePath = @"C:\Users\Sample\AppData\Local\Microsoft\Edge\User Data\Default\Storage\ext\ihmafllikibpmigkcoadcmckbfhibefp";
                    break;
                case RegKind.Driver:
                    it.FilePath = @"C:\Windows\INF\oem47.inf";
                    break;
            }
            reg.RegItems.Add(it);
        }

        private void RequestCancel()
        {
            if (!_scanning && !_cleaning && !_uninstalling && !_loadingPrograms &&
                !_mapScanning && !_mapRefreshing && !_mapCleaning) return;
            _cancelFlag = true;
            _cancelBtn.Text = Loc.T("btn.cancelWait");
            _cancelBtn.Enabled = false;
            Invalidate();
        }

        private void StartSpinner()
        {
            _spinMs = 0;
            _lastSpinPaint = -100;
            Anim.Loop(_spinKey, delegate(float ms)
            {
                _spinMs = (int)ms;
                if (ms - _lastSpinPaint > 32)
                {
                    _lastSpinPaint = (int)ms;
                    Invalidate();
                }
            });
        }

        private void StopSpinner()
        {
            Anim.Stop(_spinKey);
        }

        private void ShowDetails(Category c)
        {
            if (_scanning || _cleaning) return;
            DetailDialog d = new DetailDialog(c);
            d.ShowDialog(this);
        }

        // ---------------------------------------------------------------- paint

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_fpsRunning) _fpsFrames++;
            Stopwatch sw = _fpsRunning ? Stopwatch.StartNew() : null;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = Theme.TextHint;
            g.Clear(Theme.Bg);
            g.ScaleTransform(Theme.Scale, Theme.Scale);

            long profT = _fpsRunning ? Stopwatch.GetTimestamp() : 0;
            using (SolidBrush glow = new SolidBrush(Ui.Alpha(Theme.Blue, 9)))
            {
                float phase = Environment.TickCount / 900f;
                g.FillEllipse(glow, W - 220 + (float)Math.Sin(phase) * 12, -140, 480, 420);
                g.FillEllipse(glow, W - 420, -220 + (float)Math.Cos(phase) * 10, 620, 420);
            }
            using (SolidBrush violet = new SolidBrush(Ui.Alpha(Theme.Violet, 7)))
            {
                g.FillEllipse(violet, W - 300, -200, 520, 440);
            }
            using (SolidBrush pink = new SolidBrush(Ui.Alpha(Theme.Pink, 5)))
            {
                g.FillEllipse(pink, W - 520, -280, 620, 400);
            }
            if (_fpsRunning)
            {
                _profBg += Stopwatch.GetTimestamp() - profT;
                profT = Stopwatch.GetTimestamp();
            }

            PaintTitleBar(g);
            if (_fpsRunning)
            {
                _profTitle += Stopwatch.GetTimestamp() - profT;
                profT = Stopwatch.GetTimestamp();
            }
            if (_view == 1) PaintFileChrome(g);
            else PaintLeft(g);

            if (_fpsRunning)
            {
                _profLeft += Stopwatch.GetTimestamp() - profT;
                profT = Stopwatch.GetTimestamp();
            }

            bool busy = _scanning || _cleaning || _uninstalling || _loadingPrograms || _regLoading ||
                _regCleaning || _mapScanning || _mapRefreshing || _mapCleaning;
            if (busy) PaintBusy(g);
            else if (_view == 1)
            {
                // 文件地图由子控件自绘
            }
            else if (_tab == 0 && !_hasScan) PaintEmpty(g);
            else if (_tab == 1 && _programs.Count == 0) PaintProgramEmpty(g);
            else if (_tab == 2 && _regItems.Count == 0) PaintRegEmpty(g);

            if (!busy && _view == 0)
            {
                PaintRightHeader(g);
                PaintFooter(g);
            }
            if (_fpsRunning)
            {
                _profBody += Stopwatch.GetTimestamp() - profT;
                profT = Stopwatch.GetTimestamp();
            }
            if (sw != null)
            {
                _paintTicks += sw.ElapsedTicks;
                _paintSamples++;
            }
        }

        private void PaintTitleBar(Graphics g)
        {
            float lh = _logoHover;
            if (lh > 0.01f)
            {
                using (SolidBrush glow = new SolidBrush(Color.FromArgb((int)(18 * lh), Theme.Blue)))
                {
                    g.FillEllipse(glow, 18, 6, 44, 44);
                }
            }
            GraphicsState ls = g.Save();
            g.TranslateTransform(40f, 28f);
            g.RotateTransform(-4f * lh);
            float sc = 1f + 0.05f * lh;
            g.ScaleTransform(sc, sc);
            Theme.DrawLogo(g, new RectangleF(-16, -16, 32, 32));
            g.Restore(ls);

            {
                Font tf = Theme.Font(13f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("app.name"), tf, Theme.Text, new RectangleF(66, 10, 260, 24),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font sf = Theme.Font(8.2f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("app.tagline"), sf, Theme.Muted, new RectangleF(66, 30, 280, 18),
                    StringAlignment.Near, StringAlignment.Center);
            }

            using (Pen line = new Pen(Theme.Divider, 1f))
            {
                g.DrawLine(line, 0, TitleH + 0.5f, W, TitleH + 0.5f);
            }

            PaintLanguagePill(g);
            PaintThemeButton(g);
            DrawWindowButton(g, _minRect, _hoverMin, false);
            DrawWindowButton(g, _closeRect, _hoverClose, true);
        }

        private bool LangMenuOpen
        {
            get { return _langMenu != null && !_langMenu.IsDisposed && _langMenu.Visible; }
        }

        private void PaintLanguagePill(Graphics g)
        {
            RectangleF r = new RectangleF(_langRect.X, _langRect.Y, _langRect.Width, _langRect.Height);
            Color tint = LangMenuOpen ? Theme.Violet : Theme.Blue;
            Theme.FillRounded(g, Theme.Mix(Theme.Card, Theme.Mix(Theme.BlueSoft, Theme.Violet, _langHover * 0.2f), 0.7f * _langHover),
                r, 10f);
            Theme.StrokeRounded(g, Theme.Mix(Theme.Border, tint, 0.35f + _langHover * 0.65f), r, 10f, 1f);

            Color ic = Theme.Mix(Theme.SubText, tint, 0.6f + _langHover * 0.4f);
            float gx = r.X + 10;
            float gy = r.Y + 8;
            using (Pen p = new Pen(ic, 1.2f))
            {
                g.DrawEllipse(p, gx, gy, 16, 16);
                g.DrawEllipse(p, gx + 5, gy, 6, 16);
                g.DrawLine(p, gx, gy + 8, gx + 16, gy + 8);
            }
            {
                Font lf = Theme.Font(8.6f, FontStyle.Bold);
                Theme.DrawText(g, Loc.LangShort(), lf, Theme.Mix(Theme.Text, tint, 0.4f + _langHover * 0.4f),
                    new RectangleF(gx + 22, r.Y, 48, r.Height), StringAlignment.Near, StringAlignment.Center);
            }
            using (Pen p = new Pen(Theme.Mix(Theme.Muted, tint, _langHover), 1.4f))
            {
                float cx = r.Right - 15;
                float cy = r.Y + r.Height / 2f;
                bool open = LangMenuOpen;
                if (open)
                {
                    g.DrawLine(p, cx - 4, cy + 2, cx, cy - 2);
                    g.DrawLine(p, cx, cy - 2, cx + 4, cy + 2);
                }
                else
                {
                    g.DrawLine(p, cx - 4, cy - 2, cx, cy + 2);
                    g.DrawLine(p, cx, cy + 2, cx + 4, cy - 2);
                }
            }
        }

        private void DrawWindowButton(Graphics g, Rectangle r, bool hover, bool close)
        {
            if (hover)
            {
                Theme.FillRounded(g, close ? Theme.DangerSoft : Theme.Hover,
                    new RectangleF(r.X, r.Y, r.Width, r.Height), 10f);
            }
            Color c = hover ? (close ? Color.FromArgb(220, 80, 80) : Theme.Blue) : Theme.SubText;
            using (Pen p = new Pen(c, 1.3f))
            {
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                if (close)
                {
                    g.DrawLine(p, cx - 6, cy - 6, cx + 6, cy + 6);
                    g.DrawLine(p, cx + 6, cy - 6, cx - 6, cy + 6);
                }
                else
                {
                    g.DrawLine(p, cx - 7, cy, cx + 7, cy);
                }
            }
        }

        private void PaintLeft(Graphics g)
        {
            RectangleF panel = new RectangleF(LeftX, LeftY, LeftW, H - LeftY - 22);
            Theme.FillRounded(g, Theme.Panel, panel, 18f);
            Theme.StrokeRounded(g, Theme.PanelBorder, panel, 18f, 1f);

            using (SolidBrush blob = new SolidBrush(Ui.Alpha(Theme.Blue, 14)))
            {
                g.FillEllipse(blob, panel.Right - 120, panel.Y + 10, 150, 120);
            }
            using (SolidBrush blob2 = new SolidBrush(Ui.Alpha(Theme.Violet, 10)))
            {
                g.FillEllipse(blob2, panel.Right - 150, panel.Y + 44, 160, 130);
            }

            {
                Font hf = Theme.Font(11f, FontStyle.Bold);
                string head = _tab == 0 ? Loc.T("left.disk")
                    : (_tab == 1 ? Loc.T("left.appsTitle")
                    : (_tab == 2 ? Loc.T("regtab.title") : Loc.T("migrate.leftTitle")));
                Theme.DrawText(g, head, hf, Theme.Text, new RectangleF(LeftX + 22, LeftY + 18, 220, 24),
                    StringAlignment.Near, StringAlignment.Center);
            }
            if (_tab == 3)
            {
                RectangleF pill = new RectangleF(panel.Right - 70, LeftY + 18, 46, 22);
                Color pc = _migrate.TargetRoot.Length > 0 ? Theme.Green : Theme.Amber;
                Theme.FillRounded(g, Ui.Alpha(pc, 18), pill, 11f);
                {
                    Font pf = Theme.Font(8.2f, FontStyle.Bold);
                    string txt = _migrate.TargetDrive;
                    Theme.DrawText(g, txt, pf, pc, pill, StringAlignment.Center, StringAlignment.Center);
                }
            }
            else if (_tab != 2)
            {
                RectangleF pill = new RectangleF(panel.Right - 64, LeftY + 18, 40, 22);
                Theme.FillRounded(g, Theme.BlueSoft, pill, 11f);
                {
                    Font pf = Theme.Font(8.6f, FontStyle.Bold);
                    Theme.DrawText(g, "C:", pf, Theme.Blue, pill, StringAlignment.Center, StringAlignment.Center);
                }
            }

            if (_tab == 2)
            {
                string[] lines =
                {
                    Loc.T("regtab.checks1"),
                    Loc.T("regtab.checks2"),
                    Loc.T("regtab.checks3"),
                    Loc.T("regtab.checks4")
                };
                float ly = LeftY + 70;
                for (int i = 0; i < lines.Length; i++)
                {
                    using (SolidBrush dot = new SolidBrush(Ui.Alpha(Theme.Blue, 40)))
                    {
                        g.FillEllipse(dot, LeftX + 24, ly + 6, 8, 8);
                    }
                    {
                        Font lf = Theme.Font(8.4f, FontStyle.Regular);
                        Theme.DrawText(g, lines[i], lf, Theme.SubText,
                            new RectangleF(LeftX + 40, ly - 4, LeftW - 60, 40), StringAlignment.Near, StringAlignment.Near);
                    }
                    ly += 34;
                }
            }

            if (_tab == 0)
            {
                long safe = 0;
                long caution = 0;
                long protect = 0;
                for (int i = 0; i < _engine.Categories.Count; i++)
                {
                    Category c = _engine.Categories[i];
                    if (c.Registry) continue;
                    if (c.Risk == RiskLevel.Safe) safe += c.Bytes;
                    else if (c.Risk == RiskLevel.Caution) caution += c.Bytes;
                    else if (c.Risk == RiskLevel.Protected) protect += c.Bytes;
                }
                bool cautionMeasured = _hasScan && (_demo || _level != ScanLevel.Low);
                bool protectMeasured = _hasScan && (_demo || _level == ScanLevel.High || _level == ScanLevel.Extreme);
                DrawStat(g, LeftX + 24, 330, Theme.Green, Loc.T("left.safe"), _hasScan ? Theme.Bytes(safe) : "—");
                DrawStat(g, LeftX + 24, 356, Theme.Amber, Loc.T("left.caution"),
                    cautionMeasured ? Theme.Bytes(caution) : "—");
                DrawStat(g, LeftX + 24, 382, Theme.Violet, Loc.T("left.protected"),
                    protectMeasured ? Theme.Bytes(protect) : "—");
            }
            else if (_tab == 1)
            {
                long bytes = 0;
                int prot = 0;
                for (int i = 0; i < _programs.Count; i++)
                {
                    if (_programs[i].Protected) prot++;
                    else bytes += _programs[i].Bytes;
                }
                DrawStat(g, LeftX + 24, 330, Theme.Blue, Loc.T("left.apps"), _programs.Count.ToString());
                DrawStat(g, LeftX + 24, 356, Theme.Green, Loc.T("left.appSize"), Theme.Bytes(bytes));
                DrawStat(g, LeftX + 24, 382, Theme.Rose, Loc.T("left.appProtected"), prot.ToString());
            }
            else if (_tab == 2)
            {
                int caution = 0;
                int reboot = 0;
                for (int i = 0; i < _regItems.Count; i++)
                {
                    if (_regItems[i].Risk != RiskLevel.Safe) caution++;
                    if (_regItems[i].NeedsReboot) reboot++;
                }
                DrawStat(g, LeftX + 24, 330, Theme.Blue, Loc.T("left.regTotal"), _regItems.Count.ToString());
                DrawStat(g, LeftX + 24, 356, Theme.Amber, Loc.T("left.regCaution"), caution.ToString());
                DrawStat(g, LeftX + 24, 382, Theme.Violet, Loc.T("left.regReboot"), reboot.ToString());
            }
            else
            {
                DrawStat(g, LeftX + 24, 330, Theme.Blue, Loc.T("migrate.stat.count"),
                    _migrate.MovableCount.ToString());
                DrawStat(g, LeftX + 24, 356, Theme.Green, Loc.T("migrate.stat.moved"),
                    _migrate.MovedCount.ToString());
                DrawStat(g, LeftX + 24, 382, Theme.Teal, Loc.T("migrate.stat.freed"),
                    Theme.Bytes(_migrate.MovedBytes));
            }

            if (_isAdmin)
            {
                RectangleF admin = new RectangleF(LeftX + 22, 408, LeftW - 44, 40);
                Theme.FillRounded(g, Ui.Alpha(Theme.Green, 16), admin, 12f);
                using (SolidBrush b = new SolidBrush(Theme.Green))
                {
                    Font gf = Theme.IconFont(13f);
                    g.DrawString("\uE72E", gf, b, admin.X + 12, admin.Y + 11);
                }
                {
                    Font af = Theme.Font(8.8f, FontStyle.Bold);
                    Theme.DrawText(g, Loc.T("left.admin"), af, Theme.Green,
                        new RectangleF(admin.X + 34, admin.Y + 8, 200, 24), StringAlignment.Near, StringAlignment.Center);
                }
            }
            else
            {
                RectangleF admin = new RectangleF(LeftX + 22, 402, LeftW - 44, 48);
                Theme.FillRounded(g, Ui.Alpha(Theme.Blue, 16), admin, 12f);
                using (SolidBrush b = new SolidBrush(Theme.Blue))
                {
                    Font gf = Theme.IconFont(13f);
                    g.DrawString("\uE7BA", gf, b, admin.X + 12, admin.Y + 16);
                }
                {
                    Font af = Theme.Font(8.6f, FontStyle.Bold);
                    Theme.DrawText(g, Loc.T("left.normal"), af, Theme.Text,
                        new RectangleF(admin.X + 34, admin.Y + 6, 160, 20), StringAlignment.Near, StringAlignment.Center);
                }
                {
                    Font sf = Theme.Font(7.8f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.T("left.normalSub"), sf, Theme.SubText,
                        new RectangleF(admin.X + 34, admin.Y + 24, 190, 18), StringAlignment.Near, StringAlignment.Center);
                }
                _elevateRect = new Rectangle((int)(admin.Right - 74), (int)(admin.Y + 14), 62, 22);
                if (_hoverElevate)
                {
                    Theme.FillRounded(g, Ui.Alpha(Theme.Blue, 18), new RectangleF(_elevateRect.X, _elevateRect.Y, 62, 22), 8f);
                }
                {
                    Font lf = Theme.Font(8.2f, FontStyle.Bold);
                    Theme.DrawText(g, Loc.T("left.elevate"), lf, Theme.Blue,
                        new RectangleF(_elevateRect.X, _elevateRect.Y, 62, 22), StringAlignment.Center, StringAlignment.Center);
                }
            }

            {
                Font sf = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("left.note1"), sf, Theme.Muted,
                    new RectangleF(LeftX + 22, 546, LeftW - 44, 18), StringAlignment.Near, StringAlignment.Center);
                Theme.DrawText(g, Loc.T("left.note2"), sf, Theme.Faint,
                    new RectangleF(LeftX + 22, 564, LeftW - 44, 18), StringAlignment.Near, StringAlignment.Center);
                Theme.DrawText(g, Loc.T("left.note3"), sf, Theme.Faint,
                    new RectangleF(LeftX + 22, 592, LeftW - 44, 18), StringAlignment.Near, StringAlignment.Center);
            }
        }

        // ---------------------------------------------------------------- file mode paint

        /// <summary>文件模式的标题、副标题与底部四色图例（画布由 FileMapView 自绘）。</summary>
        private void PaintFileChrome(Graphics g)
        {
            {
                Font tf = Theme.Font(15f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("map.title"), tf, Theme.Text, new RectangleF(22, TitleY, 360, 30),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font sf = Theme.Font(8.6f, FontStyle.Regular);
                string sub = _mapSub.Length > 0 ? _mapSub : Loc.T("map.sub");
                Theme.DrawText(g, sub, sf, Theme.SubText, new RectangleF(22, SubY, W - 44, 20),
                    StringAlignment.Near, StringAlignment.Center);
            }
            if (_mapRoot == null)
            {
                if (!_mapScanning && !_mapRefreshing && !_mapCleaning)
                {
                    Font hf = Theme.Font(8f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.T("map.hint"), hf, Theme.Muted,
                        new RectangleF(W - 460, TitleY, 440, 30), StringAlignment.Far, StringAlignment.Center);
                }
                return;
            }
            {
                Font hf = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("map.hint"), hf, Theme.Muted,
                    new RectangleF(W - 460, TitleY, 440, 30), StringAlignment.Far, StringAlignment.Center);
            }

            string[] labels =
            {
                Loc.T("map.legend.safe"), Loc.T("map.legend.caution"),
                Loc.T("map.legend.risky"), Loc.T("map.legend.forbidden")
            };
            float x = 22f;
            float y = 572f;
            for (int i = 0; i < 4; i++)
            {
                RectangleF r = new RectangleF(x, y, 168f, 22f);
                Color baseC = MapRisk.Base((FileRisk)i);
                Theme.FillRounded(g, Ui.Alpha(baseC, 16), r, 8f);
                using (SolidBrush b = new SolidBrush(baseC))
                {
                    g.FillEllipse(b, r.X + 10, r.Y + 8, 7, 7);
                }
                {
                    Font lf = Theme.Font(7.8f, FontStyle.Regular);
                    Theme.DrawText(g, labels[i], lf, Theme.SubText,
                        new RectangleF(r.X + 22, r.Y, 60, r.Height), StringAlignment.Near, StringAlignment.Center);
                }
                {
                    Font vf = Theme.Font(8f, FontStyle.Bold);
                    Theme.DrawText(g, Theme.Bytes(_mapRoot.Agg[i]), vf, Theme.Text,
                        new RectangleF(r.X + 80, r.Y, 82, r.Height), StringAlignment.Far, StringAlignment.Center);
                }
                x += 176f;
            }
            {
                Font sf = Theme.Font(7.8f, FontStyle.Regular);
                string hint = Loc.T("map.opHint");
                Theme.DrawText(g, hint, sf, Theme.Faint,
                    new RectangleF(730, y, W - 22 - 730, 22), StringAlignment.Far, StringAlignment.Center);
            }
        }

        private void DrawStat(Graphics g, int x, int y, Color dot, string label, string value)
        {
            using (SolidBrush b = new SolidBrush(dot))
            {
                g.FillEllipse(b, x, y + 5, 8, 8);
            }
            {
                Font lf = Theme.Font(8.6f, FontStyle.Regular);
                Theme.DrawText(g, label, lf, Theme.SubText, new RectangleF(x + 16, y, 120, 18),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font vf = Theme.Font(9.2f, FontStyle.Bold);
                Theme.DrawText(g, value, vf, Theme.Text, new RectangleF(x + 110, y, LeftW - 160, 18),
                    StringAlignment.Far, StringAlignment.Center);
            }
        }

        private void PaintRightHeader(Graphics g)
        {
            string title = _tab == 0 ? Loc.T("right.title")
                : (_tab == 1 ? Loc.T("uninstall.title")
                : (_tab == 2 ? Loc.T("regtab.title") : Loc.T("migrate.title")));
            {
                Font tf = Theme.Font(15f, FontStyle.Bold);
                Theme.DrawText(g, title, tf, Theme.Text, new RectangleF(RightX, TitleY, 240, 30),
                    StringAlignment.Near, StringAlignment.Center);
            }
            {
                Font sf = Theme.Font(8.5f, FontStyle.Regular);
                Theme.DrawText(g, _rightSub, sf, Theme.SubText, new RectangleF(RightX, SubY, 560, 20),
                    StringAlignment.Near, StringAlignment.Center);
            }

            if (_tab == 2)
            {
                if (_regItems.Count > 0)
                {
                    {
                        Font lf = Theme.Font(8.5f, FontStyle.Bold);
                        if (_hoverSelect)
                        {
                            Theme.FillRounded(g, Theme.Hover,
                                new RectangleF(_selectRect.X - 8, _selectRect.Y, _selectRect.Width + 8, _selectRect.Height), 10f);
                        }
                        RectangleF box = new RectangleF(_selectRect.Right - 150, _selectRect.Y + 6, 15, 15);
                        Theme.FillRounded(g, Theme.Blue, box, 4f);
                        using (Pen p = new Pen(Color.White, 1.3f))
                        {
                            g.DrawLine(p, box.X + 3.5f, box.Y + 7.5f, box.X + 6.5f, box.Y + 10.5f);
                            g.DrawLine(p, box.X + 6.5f, box.Y + 10.5f, box.X + 11.5f, box.Y + 4.5f);
                        }
                        Theme.DrawText(g, _tab == 2 ? Loc.T("regtab.selectAll") : Loc.T("right.selectAll"), lf, Theme.Blue,
                            new RectangleF(box.Right + 6, _selectRect.Y, 110, _selectRect.Height), StringAlignment.Near,
                            StringAlignment.Center);
                    }
                }
                else
                {
                    {
                        Font hf = Theme.Font(8f, FontStyle.Regular);
                        Theme.DrawText(g, Loc.T("regtab.hint"), hf, Theme.Muted,
                            new RectangleF(760, RowY, RightW + RightX - 760, 32), StringAlignment.Far, StringAlignment.Center);
                    }
                }
            }
            else if (_tab == 0)
            {
                {
                    Font lf = Theme.Font(8.4f, FontStyle.Bold);
                    Theme.DrawText(g, Loc.T("level.label"), lf, Theme.Muted,
                        new RectangleF(RightX, RowY, 48, 32), StringAlignment.Near, StringAlignment.Center);
                }
                {
                    Font hf = Theme.Font(8f, FontStyle.Regular);
                    Color hint = _level == ScanLevel.Extreme ? Theme.Rose : Theme.Muted;
                    Theme.DrawText(g, Loc.LevelHint(_level), hf, hint,
                        new RectangleF(790, RowY, RightW + RightX - 790, 32), StringAlignment.Far, StringAlignment.Center);
                }
                if (_hasScan)
                {
                    {
                        Font lf = Theme.Font(8.5f, FontStyle.Bold);
                        if (_hoverSelect)
                        {
                            Theme.FillRounded(g, Theme.Hover,
                                new RectangleF(_selectRect.X - 8, _selectRect.Y, _selectRect.Width + 8, _selectRect.Height), 10f);
                        }
                        RectangleF box = new RectangleF(_selectRect.Right - 150, _selectRect.Y + 6, 15, 15);
                        Theme.FillRounded(g, Theme.Blue, box, 4f);
                        using (Pen p = new Pen(Color.White, 1.3f))
                        {
                            g.DrawLine(p, box.X + 3.5f, box.Y + 7.5f, box.X + 6.5f, box.Y + 10.5f);
                            g.DrawLine(p, box.X + 6.5f, box.Y + 10.5f, box.X + 11.5f, box.Y + 4.5f);
                        }
                        Theme.DrawText(g, Loc.T("right.selectAll"), lf, Theme.Blue,
                            new RectangleF(box.Right + 6, _selectRect.Y, 110, _selectRect.Height), StringAlignment.Near,
                            StringAlignment.Center);
                    }
                }
            }
            else if (_tab == 1)
            {
                {
                    Font hf = Theme.Font(8f, FontStyle.Regular);
                    Theme.DrawText(g, _umode == UninstallMode.Force ? Loc.T("uninstall.mode.force.hint")
                            : Loc.T("uninstall.mode.normal.hint"), hf,
                        _umode == UninstallMode.Force ? Theme.Rose : Theme.Muted,
                        new RectangleF(690, RowY, RightW + RightX - 690, 32), StringAlignment.Far, StringAlignment.Center);
                }
            }
        }

        private void PaintFooter(Graphics g)
        {
            if (_tab == 3) return;
            using (Pen line = new Pen(Theme.Divider, 1f))
            {
                g.DrawLine(line, RightX, 530, RightX + RightW, 530);
            }

            int n = SelectedCount();
            string head;
            if (_tab == 0)
            {
                head = n > 0 ? Loc.F("footer.selected", n) : (_hasScan ? Loc.T("footer.none") : Loc.T("footer.notScanned"));
            }
            else if (_tab == 2)
            {
                head = n > 0 ? Loc.F("regtab.selected", n) : Loc.T("regtab.noSelection");
            }
            else
            {
                head = n > 0 ? Loc.F("uninstall.selected", n) : Loc.T("uninstall.noSelection");
            }
            {
                Font hf = Theme.Font(9.4f, FontStyle.Bold);
                Theme.DrawText(g, head, hf, n > 0 ? Theme.Text : Theme.SubText,
                    new RectangleF(RightX, 544, 300, 22), StringAlignment.Near, StringAlignment.Center);
            }
            if (n > 0 && _tab == 0)
            {
                SizeF sz;
                {
                    Font mf = Theme.Font(9.4f, FontStyle.Bold);
                    sz = TextRenderer.MeasureText(head, mf);
                }
                {
                    Font sf = Theme.Font(8.8f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.F("footer.estimate", Theme.Bytes(SelectedBytes())), sf, Theme.SubText,
                        new RectangleF(RightX + sz.Width + 6, 544, 360, 22), StringAlignment.Near, StringAlignment.Center);
                }
            }
            {
                Font note = Theme.Font(7.8f, FontStyle.Regular);
                string noteText = _tab == 0 ? Loc.T("footer.note")
                    : (_tab == 1 ? Loc.T("footer.noteUninstall") : Loc.T("footer.noteRegistry"));
                if (_tab == 0 && _level == ScanLevel.Extreme && _hasScan) noteText = Loc.T("footer.order");
                Theme.DrawText(g, noteText, note, Theme.Muted,
                    new RectangleF(RightX, 574, 620, 18), StringAlignment.Near, StringAlignment.Center);
            }
        }

        private void PaintRegEmpty(Graphics g)
        {
            float cx = RightX + RightW / 2f;
            float cy = ListY + 120;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(cx - 56, cy - 56, 112, 112);
                using (PathGradientBrush b = new PathGradientBrush(p))
                {
                    b.CenterColor = Ui.Alpha(Theme.Blue, 22);
                    b.SurroundColors = new Color[] { Ui.Alpha(Theme.Blue, 0) };
                    b.FocusScales = new PointF(0.2f, 0.2f);
                    g.FillPath(b, p);
                }
            }
            using (SolidBrush b = new SolidBrush(Theme.Blue))
            {
                Font gf = Theme.IconFont(30f);
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("\uE9D9", gf, b, new RectangleF(cx - 40, cy - 40, 80, 80), sf);
                sf.Dispose();
            }
            {
                Font tf = Theme.Font(15f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("regtab.emptyTitle"), tf, Theme.Text,
                    new RectangleF(cx - 260, cy + 72, 520, 30), StringAlignment.Center, StringAlignment.Center);
            }
            {
                Font sf = Theme.Font(8.8f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("regtab.emptySub"), sf, Theme.SubText,
                    new RectangleF(cx - 300, cy + 108, 600, 22), StringAlignment.Center, StringAlignment.Center);
            }
        }

        private void PaintEmpty(Graphics g)
        {
            float cx = RightX + RightW / 2f;
            using (SolidBrush glow = new SolidBrush(Ui.Alpha(Theme.Blue, 14)))
            {
                g.FillEllipse(glow, cx - 118, 186, 236, 236);
            }
            using (SolidBrush b = new SolidBrush(Ui.Alpha(Theme.Blue, 150)))
            using (StringFormat sf = new StringFormat())
            {
                Font gf = Theme.IconFont(44f);
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("\uE74D", gf, b, new RectangleF(cx - 60, 234, 120, 70), sf);
            }
            {
                Font tf = Theme.Font(15f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("empty.title"), tf, Theme.Text,
                    new RectangleF(cx - 260, 350, 520, 28), StringAlignment.Center, StringAlignment.Center);
            }
            {
                Font sf = Theme.Font(8.8f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("empty.sub") + " · " + Loc.LevelHint(_level), sf, Theme.SubText,
                    new RectangleF(cx - 300, 382, 600, 22), StringAlignment.Center, StringAlignment.Center);
            }

            float y = 436;
            float w1 = 168;
            float w2 = 156;
            float w3 = 176;
            float gap = 12;
            float total = w1 + w2 + w3 + gap * 2;
            float x = cx - total / 2f;
            RuleChip(g, x, y, w1, Loc.T("empty.chip1"), Theme.Gray);
            RuleChip(g, x + w1 + gap, y, w2, Loc.T("empty.chip2"), Theme.Green);
            RuleChip(g, x + w1 + w2 + gap * 2, y, w3, Loc.T("empty.chip3"), Theme.Violet);

            {
                Font hint = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("empty.hint"), hint, Theme.Muted,
                    new RectangleF(RightX, 486, RightW, 18), StringAlignment.Center, StringAlignment.Center);
            }
        }

        private void PaintProgramEmpty(Graphics g)
        {
            float cx = RightX + RightW / 2f;
            using (SolidBrush glow = new SolidBrush(Ui.Alpha(Theme.Violet, 14)))
            {
                g.FillEllipse(glow, cx - 118, 196, 236, 236);
            }
            using (SolidBrush b = new SolidBrush(Ui.Alpha(Theme.Violet, 150)))
            using (StringFormat sf = new StringFormat())
            {
                Font gf = Theme.IconFont(44f);
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("\uE7B8", gf, b, new RectangleF(cx - 60, 244, 120, 70), sf);
            }
            {
                Font tf = Theme.Font(15f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("uninstall.empty"), tf, Theme.Text,
                    new RectangleF(cx - 280, 360, 560, 28), StringAlignment.Center, StringAlignment.Center);
            }
            {
                Font sf = Theme.Font(8.8f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("uninstall.emptySub"), sf, Theme.SubText,
                    new RectangleF(cx - 300, 392, 600, 22), StringAlignment.Center, StringAlignment.Center);
            }
        }

        private void RuleChip(Graphics g, float x, float y, float w, string text, Color dot)
        {
            RectangleF r = new RectangleF(x, y, w, 30);
            Theme.FillRounded(g, Ui.Alpha(dot, 13), r, 15f);
            Theme.StrokeRounded(g, Ui.Alpha(dot, 34), r, 15f, 1f);
            using (SolidBrush b = new SolidBrush(dot))
            {
                g.FillEllipse(b, r.X + 12, r.Y + 12, 6, 6);
            }
            {
                Font f = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, text, f, Theme.Mix(dot, Color.Black, 0.25f),
                    new RectangleF(r.X + 24, r.Y, r.Width - 30, r.Height), StringAlignment.Near, StringAlignment.Center);
            }
        }

        private void PaintBusy(Graphics g)
        {
            PaintOverlayBackground(g);
            float cx = _view == 1 ? W / 2f : RightX + RightW / 2f;

            if (_mapScanning || _mapRefreshing || _mapCleaning)
            {
                DrawSpinner(g, cx, 206);
                string title = _mapCleaning ? Loc.T("map.clean.title")
                    : (_mapRefreshing ? Loc.T("map.refresh.title") : Loc.T("map.scan.title"));
                string sub = _mapCleaning ? Loc.T("map.clean.sub")
                    : (_mapRefreshing ? Loc.T("map.refresh.sub") : Loc.T("map.scan.sub"));
                {
                    Font tf = Theme.Font(15f, FontStyle.Bold);
                    Theme.DrawText(g, title, tf, Theme.Text, new RectangleF(cx - 320, 262, 640, 28),
                        StringAlignment.Center, StringAlignment.Center);
                }
                {
                    Font sf = Theme.Font(8.8f, FontStyle.Regular);
                    Theme.DrawText(g, sub, sf, Theme.SubText, new RectangleF(cx - 360, 296, 720, 20),
                        StringAlignment.Center, StringAlignment.Center);
                }
                ScanProgress mp = _liveMap;
                if (_mapCleaning)
                {
                    long total = Math.Max(1, _mapCleanTotal);
                    float ratio = Math.Min(1f, _mapCleanDone / (float)total);
                    RatioBar(g, cx - 230, 350, 460, ratio);
                    {
                        Font pf = Theme.Font(10f, FontStyle.Bold);
                        Theme.DrawText(g, ((int)Math.Round(ratio * 100)) + "%", pf, Theme.Blue,
                            new RectangleF(cx - 30, 366, 60, 22), StringAlignment.Center, StringAlignment.Center);
                    }
                    DrawCounter(g, cx - 200, 404, Loc.T("map.clean.done"), Theme.Bytes(_mapCleanDone));
                    DrawCounter(g, cx + 200, 404, Loc.T("map.clean.total"), Theme.Bytes(_mapCleanTotal));
                }
                else
                {
                    long mapFiles = mp == null ? 0 : mp.Files;
                    long mapBytes = mp == null ? 0 : mp.Bytes;
                    DrawCounter(g, cx - 220, 344, Loc.T("scan.files"), Theme.Count(mapFiles));
                    DrawCounter(g, cx, 344, Loc.T("scan.bytes"), Theme.Bytes(mapBytes));
                    DrawCounter(g, cx + 220, 344, Loc.T("scan.elapsed"),
                        Theme.Duration(Environment.TickCount - _opStart));
                }
                DrawPathPill(g, mp == null || string.IsNullOrEmpty(mp.CurrentPath)
                    ? Loc.T("scan.reading") : mp.CurrentPath);
                {
                    Font nf = Theme.Font(8f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.T("map.busy.note"), nf, Theme.Muted,
                        new RectangleF(cx - 300, 480, 600, 20), StringAlignment.Center, StringAlignment.Center);
                }
                return;
            }

            if (_loadingPrograms)
            {
                DrawSpinner(g, cx, 206);
                {
                    Font sf = Theme.Font(9f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.T("uninstall.subScan"), sf, Theme.SubText,
                        new RectangleF(cx - 320, 262, 640, 22), StringAlignment.Center, StringAlignment.Center);
                }
                ScanProgress lp = _liveScan;
                DrawPathPill(g, lp == null || string.IsNullOrEmpty(lp.CurrentPath)
                    ? Loc.T("scan.reading") : Loc.F("uninstall.reading", lp.CurrentPath));
                return;
            }

            if (_regLoading || _regCleaning)
            {
                DrawSpinner(g, cx, 206);
                {
                    Font sf = Theme.Font(9f, FontStyle.Regular);
                    Theme.DrawText(g, _regCleaning ? Loc.T("regtab.runningSub") : Loc.T("regtab.scanning"), sf,
                        Theme.SubText, new RectangleF(cx - 320, 262, 640, 22), StringAlignment.Center, StringAlignment.Center);
                }
                ScanProgress lp2 = _liveScan;
                if (!_regCleaning)
                {
                    DrawPathPill(g, lp2 == null || string.IsNullOrEmpty(lp2.CurrentPath)
                        ? Loc.T("scan.reading") : lp2.CurrentPath);
                }
                else
                {
                    {
                        Font pf = Theme.Font(8f, FontStyle.Regular);
                        Theme.DrawText(g, Loc.T("regtab.cmdHint"), pf, Theme.Muted,
                            new RectangleF(cx - 320, 300, 640, 20), StringAlignment.Center, StringAlignment.Center);
                    }
                }
                return;
            }

            if (_uninstalling)
            {
                {
                    Font tf = Theme.Font(16f, FontStyle.Bold);
                    Theme.DrawText(g, Loc.T("dlg.uninstall"), tf, Theme.Text, new RectangleF(cx - 250, 214, 500, 30),
                        StringAlignment.Center, StringAlignment.Center);
                }
                {
                    Font sf = Theme.Font(8.8f, FontStyle.Regular);
                    string sub = _umode == UninstallMode.Force ? Loc.T("uconfirm.warnForce") : Loc.T("uninstall.mode.normal.hint");
                    Theme.DrawText(g, sub, sf, Theme.SubText, new RectangleF(cx - 320, 246, 640, 22),
                        StringAlignment.Center, StringAlignment.Center);
                }
                float ratio = _uninstallTotal > 0 ? (_uninstallIndex + 0.5f) / _uninstallTotal : 0f;
                RatioBar(g, cx - 230, 300, 460, ratio);
                {
                    Font pf = Theme.Font(10f, FontStyle.Bold);
                    Theme.DrawText(g, ((int)Math.Round(ratio * 100)) + "%", pf, Theme.Blue,
                        new RectangleF(cx - 30, 316, 60, 22), StringAlignment.Center, StringAlignment.Center);
                }
                DrawCounter(g, cx - 160, 362, Loc.T("clean.processed"), (_uninstallIndex + 1) + " / " + _uninstallTotal);
                DrawCounter(g, cx + 160, 362, Loc.T("clean.title"), _uninstallStep == null ? "" : _uninstallStep);
                DrawPathPill(g, _uninstallStep == null ? Loc.T("uninstall.stepFiles") : _uninstallStep);
                return;
            }

            if (_cleaning)
            {
                {
                    Font tf = Theme.Font(16f, FontStyle.Bold);
                    Theme.DrawText(g, Loc.T("clean.title"), tf, Theme.Text, new RectangleF(cx - 250, 214, 500, 30),
                        StringAlignment.Center, StringAlignment.Center);
                }
                {
                    Font sf = Theme.Font(8.8f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.T("clean.sub"), sf, Theme.SubText,
                        new RectangleF(cx - 320, 246, 640, 22), StringAlignment.Center, StringAlignment.Center);
                }

                CleanProgress p = _liveClean;
                long expected = p == null ? 0 : p.Expected;
                long processed = p == null ? 0 : p.Processed;
                float ratio = expected > 0 ? Math.Min(1f, processed / (float)expected) : 0f;
                RatioBar(g, cx - 230, 300, 460, ratio);

                {
                    Font pf = Theme.Font(10f, FontStyle.Bold);
                    Theme.DrawText(g, ((int)Math.Round(ratio * 100)) + "%", pf, Theme.Blue,
                        new RectangleF(cx - 30, 316, 60, 22), StringAlignment.Center, StringAlignment.Center);
                }

                DrawCounter(g, cx - 220, 362, Loc.T("clean.freed"), Theme.Bytes(p == null ? 0 : p.Freed));
                DrawCounter(g, cx, 362, Loc.T("clean.recycled"), Theme.Bytes(p == null ? 0 : p.Recycled));
                DrawCounter(g, cx + 220, 362, Loc.T("clean.processed"), Theme.Count(p == null ? 0 : p.Deleted));

                string path = p == null ? Loc.T("clean.preparing") : p.CurrentPath;
                DrawPathPill(g, p != null && p.Stage != null && p.Stage.Length > 0 ? p.Stage + "  ·  " + path : path);

                {
                    Font f = Theme.Font(8f, FontStyle.Regular);
                    Theme.DrawText(g, Loc.T("clean.note"), f, Theme.Muted,
                        new RectangleF(cx - 260, 470, 520, 20), StringAlignment.Center, StringAlignment.Center);
                }
                return;
            }

            {
                Font tf = Theme.Font(16f, FontStyle.Bold);
                Theme.DrawText(g, Loc.T("scan.title"), tf, Theme.Text, new RectangleF(cx - 250, 264, 500, 30),
                    StringAlignment.Center, StringAlignment.Center);
            }
            {
                Font sf = Theme.Font(8.8f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("scan.sub"), sf, Theme.SubText,
                    new RectangleF(cx - 320, 296, 640, 22), StringAlignment.Center, StringAlignment.Center);
            }
            DrawSpinner(g, cx, 198);

            ScanProgress sp = _liveScan;
            long files = sp == null ? 0 : sp.Files;
            long bytes = sp == null ? 0 : sp.Bytes;
            int elapsed = Environment.TickCount - _opStart;
            int passes = sp == null ? 1 : sp.Passes;
            int pass = sp == null ? 1 : sp.Pass;
            DrawCounter(g, cx - 220, 350, Loc.T("scan.files"), Theme.Count(files));
            DrawCounter(g, cx, 350, Loc.T("scan.bytes"), Theme.Bytes(bytes));
            DrawCounter(g, cx + 220, 350, passes > 1 ? Loc.F("scan.pass", pass, passes) : Loc.T("scan.elapsed"),
                passes > 1 ? pass + " / " + passes : Theme.Duration(elapsed));

            string scanPath = sp == null ? Loc.T("scan.reading") : sp.CurrentPath;
            DrawPathPill(g, scanPath);

            {
                Font f = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, Loc.T("scan.note"), f, Theme.Muted,
                    new RectangleF(cx - 240, 470, 480, 20), StringAlignment.Center, StringAlignment.Center);
            }
        }

        private void PaintOverlayBackground(Graphics g)
        {
            using (SolidBrush b = new SolidBrush(Theme.Bg))
            {
                if (_view == 1) g.FillRectangle(b, 0, TitleH + 1, W, H - TitleH - 2);
                else g.FillRectangle(b, RightX - 12, TitleH + 1, RightW + 24, H - TitleH - 2);
            }
            if (_view == 1) PaintFileChrome(g);
            else PaintRightHeader(g);
        }

        private void DrawSpinner(Graphics g, float cx, float cy)
        {
            float angle = _spinMs * 0.28f;
            RectangleF ring = new RectangleF(cx - 32, cy - 32, 64, 64);
            using (Pen track = new Pen(Theme.Track, 3f))
            {
                g.DrawEllipse(track, ring);
            }
            using (Pen arc = new Pen(Theme.Blue, 3f))
            {
                g.DrawArc(arc, ring, angle, 110f);
            }
            using (Pen arc2 = new Pen(Color.FromArgb(150, Theme.Cyan), 3f))
            {
                g.DrawArc(arc2, ring, angle + 130f, 80f);
            }
            using (Pen arc3 = new Pen(Color.FromArgb(180, Theme.Violet), 3f))
            {
                g.DrawArc(arc3, ring, angle + 220f, 60f);
            }
            using (SolidBrush dot = new SolidBrush(Theme.Blue))
            {
                g.FillEllipse(dot, cx - 4, cy - 4, 8, 8);
            }
        }

        private void DrawCounter(Graphics g, float cx, float y, string label, string value)
        {
            {
                Font vf = Theme.Font(13f, FontStyle.Bold);
                Theme.DrawText(g, value, vf, Theme.Text, new RectangleF(cx - 110, y, 220, 26),
                    StringAlignment.Center, StringAlignment.Center);
            }
            {
                Font lf = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, label, lf, Theme.SubText, new RectangleF(cx - 110, y + 26, 220, 18),
                    StringAlignment.Center, StringAlignment.Center);
            }
        }

        private void DrawPathPill(Graphics g, string path)
        {
            float px = _view == 1 ? 40f : RightX + 40;
            float pw = _view == 1 ? W - 80f : RightW - 80;
            RectangleF pill = new RectangleF(px, 414, pw, 38);
            Theme.FillRounded(g, Theme.Sunken, pill, 10f);
            Theme.StrokeRounded(g, Theme.Divider, pill, 10f, 1f);
            using (SolidBrush b = new SolidBrush(Theme.Muted))
            {
                Font gf = Theme.IconFont(12f);
                g.DrawString("\uE8B7", gf, b, pill.X + 12, pill.Y + 11);
            }
            {
                Font pf = Theme.Font(8f, FontStyle.Regular);
                Theme.DrawText(g, path, pf, Theme.SubText,
                    new RectangleF(pill.X + 34, pill.Y, pill.Width - 48, pill.Height), StringAlignment.Near,
                    StringAlignment.Center);
            }
        }

        private void RatioBar(Graphics g, float x, float y, float w, float ratio)
        {
            RectangleF track = new RectangleF(x, y, w, 8);
            Theme.FillRounded(g, Theme.Track, track, 4f);
            if (ratio > 0.005f)
            {
                Theme.FillRoundedGradient(g, Theme.Blue, Theme.Cyan,
                    new RectangleF(x, y, Math.Max(8f, w * ratio), 8), 4f);
            }
        }

        // ---------------------------------------------------------------- input

        private Point LogicalPoint(MouseEventArgs e)
        {
            return new Point((int)(e.X / Theme.Scale), (int)(e.Y / Theme.Scale));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point lp = LogicalPoint(e);
            bool close = _closeRect.Contains(lp);
            bool min = _minRect.Contains(lp);
            bool select = (_view == 0 && _tab == 0 && _hasScan && !_scanning && !_cleaning && _selectRect.Contains(lp))
                || (_view == 0 && _tab == 2 && _regItems.Count > 0 && !_regLoading && !_regCleaning && _selectRect.Contains(lp));
            bool elevate = _view == 0 && !_isAdmin && _elevateRect.Contains(lp) && !_scanning && !_cleaning && !_uninstalling;
            bool lang = _langRect.Contains(lp);
            bool theme = _themeRect.Contains(lp);
            bool logo = _logoRect.Contains(lp);
            if (lang != _hoverLang)
            {
                _hoverLang = lang;
                Anim.Tween(_langHoverKey, _langHover, lang ? 1f : 0f, 180,
                    delegate(float v) { _langHover = v; Invalidate(); }, null);
            }
            if (theme != _hoverTheme)
            {
                _hoverTheme = theme;
                Anim.Tween(_themeHoverKey, _themeHover, theme ? 1f : 0f, 200,
                    delegate(float v) { _themeHover = v; Invalidate(); }, null);
                UpdateThemeTip();
                Cursor = (theme || lang || close || min || select || elevate) ? Cursors.Hand : Cursors.Default;
            }
            if (logo != _hoverLogo)
            {
                _hoverLogo = logo;
                Anim.Tween(_logoHoverKey, _logoHover, logo ? 1f : 0f, 220,
                    delegate(float v) { _logoHover = v; Invalidate(); }, null);
            }
            if (close != _hoverClose || min != _hoverMin || select != _hoverSelect || elevate != _hoverElevate)
            {
                _hoverClose = close;
                _hoverMin = min;
                _hoverSelect = select;
                _hoverElevate = elevate;
                Cursor = (close || min || select || elevate || lang || theme) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverClose = false;
            _hoverMin = false;
            _hoverSelect = false;
            _hoverElevate = false;
            if (_hoverLang)
            {
                _hoverLang = false;
                Anim.Tween(_langHoverKey, _langHover, 0f, 180,
                    delegate(float v) { _langHover = v; Invalidate(); }, null);
            }
            if (_hoverLogo)
            {
                _hoverLogo = false;
                Anim.Tween(_logoHoverKey, _logoHover, 0f, 220,
                    delegate(float v) { _logoHover = v; Invalidate(); }, null);
            }
            if (_hoverTheme)
            {
                _hoverTheme = false;
                Anim.Tween(_themeHoverKey, _themeHover, 0f, 200,
                    delegate(float v) { _themeHover = v; Invalidate(); }, null);
                UpdateThemeTip();
            }
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Point lp = LogicalPoint(e);
            if (lp.Y < TitleH && !_closeRect.Contains(lp) && !_minRect.Contains(lp) && !_langRect.Contains(lp) &&
                !_themeRect.Contains(lp))
            {
                Native.Drag(Handle);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Point lp = LogicalPoint(e);
            if (_closeRect.Contains(lp))
            {
                if (_scanning || _cleaning || _uninstalling) _cancelFlag = true;
                Close();
                return;
            }
            if (_minRect.Contains(lp))
            {
                WindowState = FormWindowState.Minimized;
                return;
            }
            if (_langRect.Contains(lp))
            {
                ToggleLanguageMenu();
                return;
            }
            if (_themeRect.Contains(lp))
            {
                ToggleTheme();
                return;
            }
            if ((_view == 0 && _tab == 0 && _hasScan && !_scanning && !_cleaning && _selectRect.Contains(lp)) ||
                (_view == 0 && _tab == 2 && _regItems.Count > 0 && !_regLoading && !_regCleaning && _selectRect.Contains(lp)))
            {
                if (_tab == 2) SelectAllReg();
                else SelectAllSafe();
                return;
            }
            if (_view == 0 && !_isAdmin && !_scanning && !_cleaning && _elevateRect.Contains(lp))
            {
                if (Native.Elevate("--elevated --tab " + _tab + " --level " + (int)_level))
                {
                    Close();
                }
            }
        }

        private void ToggleLanguageMenu()
        {
            if (LangMenuOpen)
            {
                _langMenu.Close();
                return;
            }
            LangMenuForm f = new LangMenuForm(delegate(string code) { ChangeLanguage(code); });
            Point below = PointToScreen(new Point(Px(_langRect.X), Px(_langRect.Bottom + 4)));
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            if (below.X + f.Width > wa.Right) below.X = wa.Right - f.Width - Px(8);
            if (below.Y + f.Height > wa.Bottom)
            {
                Point top = PointToScreen(new Point(Px(_langRect.X), Px(_langRect.Y)));
                below.Y = top.Y - f.Height - Px(6);
            }
            f.Location = below;
            f.FormClosed += delegate { _langMenu = null; Invalidate(); };
            f.Show(this);
            _langMenu = f;
            Invalidate();
        }

        private void OpenSettings()
        {
            SettingsDialog f = new SettingsDialog(delegate(string code) { ChangeLanguage(code); });
            f.ShowDialog(this);
            Invalidate();
        }

        private void ChangeLanguage(string code)
        {
            if (code == Loc.Lang) return;
            Loc.SetLang(code);
            Text = Loc.T("app.name");
            _engine.Relocalize();
            // 页签先重建：文案变了但数量必须仍是 4 个，否则最后一个页签会被钳掉
            _tabSeg.SetItems(TabLabels(), _tab);
            _levelSeg.SetItems(new string[]
            {
                Loc.T("level.low"), Loc.T("level.medium"), Loc.T("level.high"), Loc.T("level.extreme")
            }, (int)_level);
            _modeSeg.SetItems(new string[] { Loc.T("uninstall.mode.normal"), Loc.T("uninstall.mode.force") },
                (int)_umode);
            _viewSeg.SetItems(new string[] { Loc.T("view.normal"), Loc.T("view.file") }, _view);
            UpdateSubtitleForLanguage();
            RefreshButtonTexts();
            if (_display.Count > 0) _list.SetCategories(_display);
            if (_programs.Count > 0) _plist.SetPrograms(_programs);
            if (_regItems.Count > 0) _rlist.RefreshView();
            if (_map != null && _mapRoot != null)
            {
                _map.RefreshView();
                _mapSub = BuildMapSub();
            }
            if (_migrate != null)
            {
                _migrate.Relocalize();
                _migrate.Sync();
            }
            _scanSummary = _lastScan != null ? BuildScanSummary(_lastScan) : "";
            if (_tab == 0 && _lastScan != null) _rightSub = _scanSummary;
            Invalidate();
        }

        /// <summary>切换语言后按当前页签重算副标题；注册表页有自己的统计与空状态文案。</summary>
        private void UpdateSubtitleForLanguage()
        {
            if (_tab == 3)
            {
                _rightSub = _migrate.StatusLine();
                return;
            }
            if (_tab == 2)
            {
                if (_regLoading) _rightSub = Loc.T("regtab.scanning");
                else if (_regCleaning) _rightSub = Loc.T("regtab.runningSub");
                else if (_regItems.Count > 0) _rightSub = BuildRegSub();
                else _rightSub = Loc.T("regtab.sub");
                return;
            }
            if (_tab == 1)
            {
                if (_uninstalling) _rightSub = Loc.F("uninstall.selected", SelectedCount());
                else if (_loadingPrograms) _rightSub = Loc.T("uninstall.refresh");
                else _rightSub = _programs.Count > 0 ? BuildProgramSub() : Loc.T("uninstall.subScan");
                return;
            }
            if (_scanning) _rightSub = Loc.T("scan.preparing");
            else if (_cleaning) _rightSub = Loc.T("clean.title");
            else if (_lastScan != null) _rightSub = _scanSummary;
            else if (_demo) _rightSub = Loc.F("sum.scan", "378,796") + Loc.F("sum.time", "37.9 s");
            else _rightSub = Loc.T("right.sub");
        }

        private void RefreshButtonTexts()
        {
            if (_scanning)
            {
                _scanBtn.Text = Loc.T("btn.scanning");
                if (_cancelBtn.Visible) _cancelBtn.Text = _cancelFlag ? Loc.T("btn.cancelWait") : Loc.T("btn.cancelScan");
            }
            else if (_cleaning)
            {
                _scanBtn.Text = Loc.T("clean.title");
                if (_cancelBtn.Visible) _cancelBtn.Text = _cancelFlag ? Loc.T("btn.cancelWait") : Loc.T("btn.cancelClean");
            }
            else if (_regLoading)
            {
                _scanBtn.Text = Loc.T("btn.scanning");
                if (_cancelBtn.Visible) _cancelBtn.Text = _cancelFlag ? Loc.T("btn.cancelWait") : Loc.T("btn.cancelScan");
            }
            else if (_regCleaning)
            {
                _scanBtn.Text = Loc.T("regtab.running");
                if (_cancelBtn.Visible) _cancelBtn.Text = Loc.T("btn.cancel");
            }
            else if (_uninstalling || _loadingPrograms)
            {
                _scanBtn.Text = Loc.T("dlg.uninstall");
                if (_cancelBtn.Visible) _cancelBtn.Text = _cancelFlag ? Loc.T("btn.cancelWait") : Loc.T("btn.cancel");
            }
            else if (_tab == 1)
            {
                _scanBtn.Text = Loc.T("uninstall.refresh");
            }
            else if (_tab == 2)
            {
                _scanBtn.Text = _regItems.Count > 0 ? Loc.T("regtab.rescan") : Loc.T("regtab.scan");
            }
            else if (_tab == 3)
            {
                _scanBtn.Text = _migrate.HasScanned ? Loc.T("migrate.rescan") : Loc.T("migrate.scan");
            }
            else
            {
                _scanBtn.Text = _hasScan ? Loc.T("btn.rescan") : Loc.T("btn.scan");
            }
            if (_tab == 1)
            {
                _cleanBtn.Text = _umode == UninstallMode.Force ? Loc.T("uninstall.btnForce") : Loc.T("uninstall.btn");
            }
            else if (_tab == 2)
            {
                _cleanBtn.Text = Loc.T("regtab.btnClean");
            }
            else
            {
                _cleanBtn.Text = Loc.T("btn.clean");
            }
            _verifyBtn.Text = Loc.T("btn.verify");
            _settingsBtn.Text = Loc.T("settings.btn");
            _tip.SetToolTip(_settingsBtn, Loc.T("settings.tip"));
        }

        private void SelectAllReg()
        {
            bool all = true;
            for (int i = 0; i < _regItems.Count; i++)
            {
                if (!_regItems[i].Selected)
                {
                    all = false;
                    break;
                }
            }
            for (int i = 0; i < _regItems.Count; i++) _regItems[i].Selected = !all;
            _rlist.RefreshView();
            UpdateSelection();
            Invalidate();
        }

        private void SelectAllSafe()
        {
            bool all = true;
            for (int i = 0; i < _display.Count; i++)
            {
                Category c = _display[i];
                if (c.Risk == RiskLevel.Safe && c.Actionable && c.Bytes > 0 && !c.Selected)
                {
                    all = false;
                    break;
                }
            }
            for (int i = 0; i < _display.Count; i++)
            {
                Category c = _display[i];
                if (c.Risk == RiskLevel.Safe && c.Actionable && c.Bytes > 0) c.Selected = !all;
            }
            _list.RefreshView();
            UpdateSelection();
            Invalidate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cancelFlag = true;
            Anim.Stop(_spinKey);
            if (_langMenu != null && !_langMenu.IsDisposed) _langMenu.Close();
            if (_migrate != null) _migrate.CancelWork();
            base.OnFormClosing(e);
        }
    }
}
