using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace CleanC
{
    /// <summary>一套完整的配色。明暗各一份，界面只读 Theme.Xxx，切换时整体替换。</summary>
    public sealed class Palette
    {
        public bool Dark;

        public Color Bg;
        public Color Panel;
        public Color PanelBorder;
        public Color Card;
        public Color CardHover;
        public Color Border;
        public Color Text;
        public Color SubText;
        public Color Muted;

        public Color Blue;
        public Color BlueDeep;
        public Color Cyan;
        public Color BlueSoft;
        public Color Green;
        public Color Amber;
        public Color Gray;
        public Color Red;
        public Color Track;

        public Color Violet;
        public Color Indigo;
        public Color Pink;
        public Color Sky;
        public Color Teal;
        public Color Coral;
        public Color Rose;
        public Color Gold;

        /// <summary>卡片内部的次级底色（列表行、统计块、说明块）。</summary>
        public Color Surface;
        /// <summary>凹陷底色（轨道、路径药丸）。</summary>
        public Color Sunken;
        /// <summary>悬停底色。</summary>
        public Color Hover;
        /// <summary>分隔线 / 极淡描边。</summary>
        public Color Divider;
        /// <summary>勾选框等控件的边框。</summary>
        public Color BorderStrong;
        /// <summary>最弱的说明文字。</summary>
        public Color Faint;
        /// <summary>禁用态填充与文字。</summary>
        public Color Disabled;
        public Color DisabledText;
        /// <summary>关闭按钮的浅红悬停底。</summary>
        public Color DangerSoft;

        public Color ShadowTint;
        public Color LogoBase1;
        public Color LogoBase2;
        public Color LogoInk;
        public Color Flash;
        public Color Box1;
        public Color Box2;
        public Color BoxEdge;

        /// <summary>阴影强度系数：深色背景需要更重的影子。</summary>
        public float ShadowMul;
        /// <summary>淡色底纹的透明度系数：深色下提亮，否则看不见。</summary>
        public float TintBoost;
    }

    /// <summary>需要在主题切换时重排底色的自绘控件。</summary>
    public interface IThemed
    {
        void ApplyTheme();
    }

    public static class Theme
    {
        public static readonly Palette Light = new Palette
        {
            Dark = false,
            Bg = C(255, 255, 255),
            Panel = C(244, 248, 255),
            PanelBorder = C(227, 237, 251),
            Card = C(255, 255, 255),
            CardHover = C(247, 250, 255),
            Border = C(229, 237, 248),
            Text = C(15, 27, 45),
            SubText = C(107, 124, 149),
            Muted = C(148, 163, 184),
            Blue = C(47, 123, 255),
            BlueDeep = C(29, 95, 224),
            Cyan = C(34, 199, 232),
            BlueSoft = C(234, 242, 255),
            Green = C(14, 163, 107),
            Amber = C(199, 119, 0),
            Gray = C(107, 124, 149),
            Red = C(225, 70, 70),
            Track = C(232, 240, 252),
            Violet = C(139, 92, 246),
            Indigo = C(99, 102, 241),
            Pink = C(236, 72, 153),
            Sky = C(14, 165, 233),
            Teal = C(20, 184, 166),
            Coral = C(251, 113, 133),
            Rose = C(244, 63, 94),
            Gold = C(234, 179, 8),
            Surface = C(250, 252, 255),
            Sunken = C(249, 251, 255),
            Hover = C(242, 247, 255),
            Divider = C(236, 243, 251),
            BorderStrong = C(199, 214, 235),
            Faint = C(176, 188, 205),
            Disabled = C(239, 243, 249),
            DisabledText = C(163, 176, 194),
            DangerSoft = C(255, 242, 242),
            ShadowTint = C(47, 100, 190),
            LogoBase1 = C(255, 255, 255),
            LogoBase2 = C(238, 246, 255),
            LogoInk = C(47, 123, 255),
            Flash = C(255, 255, 255),
            Box1 = C(240, 245, 253),
            Box2 = C(205, 220, 242),
            BoxEdge = C(150, 175, 212),
            ShadowMul = 1f,
            TintBoost = 1f
        };

        public static readonly Palette Dark = new Palette
        {
            Dark = true,
            Bg = C(13, 19, 29),
            Panel = C(21, 30, 45),
            PanelBorder = C(34, 46, 64),
            Card = C(24, 33, 49),
            CardHover = C(30, 41, 59),
            Border = C(40, 53, 74),
            Text = C(233, 239, 248),
            SubText = C(152, 167, 190),
            Muted = C(118, 133, 155),
            Blue = C(84, 150, 255),
            BlueDeep = C(52, 112, 232),
            Cyan = C(60, 212, 240),
            BlueSoft = C(31, 48, 78),
            Green = C(48, 192, 134),
            Amber = C(235, 168, 68),
            Gray = C(140, 154, 176),
            Red = C(240, 98, 98),
            Track = C(35, 47, 67),
            Violet = C(158, 124, 255),
            Indigo = C(124, 128, 255),
            Pink = C(244, 114, 182),
            Sky = C(56, 189, 248),
            Teal = C(45, 212, 191),
            Coral = C(253, 150, 160),
            Rose = C(251, 113, 133),
            Gold = C(250, 204, 21),
            Surface = C(29, 39, 56),
            Sunken = C(17, 25, 38),
            Hover = C(34, 46, 66),
            Divider = C(39, 52, 72),
            BorderStrong = C(68, 87, 114),
            Faint = C(110, 126, 148),
            Disabled = C(30, 39, 54),
            DisabledText = C(98, 112, 134),
            DangerSoft = C(58, 30, 34),
            ShadowTint = C(0, 0, 0),
            LogoBase1 = C(26, 36, 52),
            LogoBase2 = C(15, 22, 34),
            LogoInk = C(228, 238, 250),
            Flash = C(42, 60, 88),
            Box1 = C(44, 58, 80),
            Box2 = C(27, 37, 54),
            BoxEdge = C(74, 96, 126),
            ShadowMul = 1.9f,
            TintBoost = 1.35f
        };

        private static Palette _p = Light;

        public static float Scale = 1f;

        /// <summary>主题切换后触发（用于重设那些缓存了旧颜色的控件属性）。</summary>
        public static event Action Changed;

        private static Color C(int r, int gg, int b)
        {
            return Color.FromArgb(255, r, gg, b);
        }

        public static bool IsDark
        {
            get { return _p.Dark; }
        }

        public static Color Bg { get { return _p.Bg; } }
        public static Color Panel { get { return _p.Panel; } }
        public static Color PanelBorder { get { return _p.PanelBorder; } }
        public static Color Card { get { return _p.Card; } }
        public static Color CardHover { get { return _p.CardHover; } }
        public static Color Border { get { return _p.Border; } }
        public static Color Text { get { return _p.Text; } }
        public static Color SubText { get { return _p.SubText; } }
        public static Color Muted { get { return _p.Muted; } }

        public static Color Blue { get { return _p.Blue; } }
        public static Color BlueDeep { get { return _p.BlueDeep; } }
        public static Color Cyan { get { return _p.Cyan; } }
        public static Color BlueSoft { get { return _p.BlueSoft; } }
        public static Color Green { get { return _p.Green; } }
        public static Color Amber { get { return _p.Amber; } }
        public static Color Gray { get { return _p.Gray; } }
        public static Color Red { get { return _p.Red; } }
        public static Color Track { get { return _p.Track; } }

        public static Color Violet { get { return _p.Violet; } }
        public static Color Indigo { get { return _p.Indigo; } }
        public static Color Pink { get { return _p.Pink; } }
        public static Color Sky { get { return _p.Sky; } }
        public static Color Teal { get { return _p.Teal; } }
        public static Color Coral { get { return _p.Coral; } }
        public static Color Rose { get { return _p.Rose; } }
        public static Color Gold { get { return _p.Gold; } }

        public static Color Surface { get { return _p.Surface; } }
        public static Color Sunken { get { return _p.Sunken; } }
        public static Color Hover { get { return _p.Hover; } }
        public static Color Divider { get { return _p.Divider; } }
        public static Color BorderStrong { get { return _p.BorderStrong; } }
        public static Color Faint { get { return _p.Faint; } }
        public static Color Disabled { get { return _p.Disabled; } }
        public static Color DisabledText { get { return _p.DisabledText; } }
        public static Color DangerSoft { get { return _p.DangerSoft; } }

        public static Color ShadowTint { get { return _p.ShadowTint; } }
        public static Color LogoBase1 { get { return _p.LogoBase1; } }
        public static Color LogoBase2 { get { return _p.LogoBase2; } }
        public static Color LogoInk { get { return _p.LogoInk; } }
        public static Color Flash { get { return _p.Flash; } }
        public static Color Box1 { get { return _p.Box1; } }
        public static Color Box2 { get { return _p.Box2; } }
        public static Color BoxEdge { get { return _p.BoxEdge; } }

        /// <summary>深色底上用 ClearType 会出现彩边，换成灰度抗锯齿。</summary>
        public static TextRenderingHint TextHint
        {
            get { return _p.Dark ? TextRenderingHint.AntiAliasGridFit : TextRenderingHint.ClearTypeGridFit; }
        }

        /// <summary>给颜色加透明度，并按主题提亮淡色底纹。</summary>
        public static Color A(Color c, int alpha)
        {
            int a = (int)Math.Round(alpha * _p.TintBoost);
            if (a > 255) a = 255;
            if (a < 0) a = 0;
            return Color.FromArgb(a, c.R, c.G, c.B);
        }

        // ---------------------------------------------------------------- 切换

        /// <summary>启动时读取偏好；没有存过则跟随系统。</summary>
        public static void Init()
        {
            string saved = Settings.Get("theme", "");
            if (string.Equals(saved, "dark", StringComparison.OrdinalIgnoreCase)) Set(true, false);
            else if (string.Equals(saved, "light", StringComparison.OrdinalIgnoreCase)) Set(false, false);
            else Set(SystemPrefersDark(), false);
        }

        public static bool SystemPrefersDark()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k == null) return false;
                    object v = k.GetValue("AppsUseLightTheme");
                    if (v is int) return (int)v == 0;
                }
            }
            catch
            {
            }
            return false;
        }

        public static void Toggle()
        {
            Set(!_p.Dark, true);
        }

        public static void Set(bool dark, bool persist)
        {
            Palette next = dark ? Dark : Light;
            if (!ReferenceEquals(next, _p))
            {
                _p = next;
                ApplyAll();
            }
            if (persist) Settings.Set("theme", dark ? "dark" : "light");
            if (Changed != null) Changed();
        }

        /// <summary>把所有打开的窗口和控件重排底色并重绘。</summary>
        public static void ApplyAll()
        {
            FormCollection forms = Application.OpenForms;
            List<Form> list = new List<Form>();
            for (int i = 0; i < forms.Count; i++) list.Add(forms[i]);
            for (int i = 0; i < list.Count; i++)
            {
                Form f = list[i];
                if (f == null || f.IsDisposed) continue;
                // 先自上而下统一底色，再自下而上让控件修正自己的特殊底色（面板、按钮等）
                Prep(f);
                Self(f);
                f.Invalidate(true);
            }
        }

        private static void Prep(Control c)
        {
            c.BackColor = Bg;
            Control.ControlCollection kids = c.Controls;
            for (int i = 0; i < kids.Count; i++) Prep(kids[i]);
        }

        private static void Self(Control c)
        {
            Control.ControlCollection kids = c.Controls;
            for (int i = 0; i < kids.Count; i++) Self(kids[i]);
            IThemed t = c as IThemed;
            if (t != null) t.ApplyTheme();
            c.Invalidate();
        }

        // ---------------------------------------------------------------- 绘制

        /// <summary>字体缓存：GDI+ Font 创建成本高，逐帧创建会造成明显掉帧；缓存后不再释放。</summary>
        private static readonly Dictionary<string, Font> FontCache =
            new Dictionary<string, Font>(StringComparer.Ordinal);

        private static Font GetFont(string family, float px, FontStyle style)
        {
            string key = family + "|" + ((int)Math.Round(px * 100)).ToString() + "|" + style;
            Font f;
            if (FontCache.TryGetValue(key, out f)) return f;
            f = new Font(family, px, style, GraphicsUnit.Pixel);
            FontCache[key] = f;
            return f;
        }

        public static Font Font(float pt, FontStyle style)
        {
            return GetFont("Microsoft YaHei UI", pt * (96f / 72f), style);
        }

        public static Font IconFont(float px)
        {
            return GetFont("Segoe MDL2 Assets", px, FontStyle.Regular);
        }

        public static Font MonoFont(float px)
        {
            return GetFont("Consolas", px, FontStyle.Regular);
        }

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            if (d <= 0.5f)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.X + r.Width - d, r.Y, d, d, 270, 90);
            p.AddArc(r.X + r.Width - d, r.Y + r.Height - d, d, d, 0, 90);
            p.AddArc(r.X, r.Y + r.Height - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRounded(Graphics g, Color color, RectangleF r, float radius)
        {
            using (GraphicsPath p = RoundRect(r, radius))
            using (SolidBrush b = new SolidBrush(color))
            {
                g.FillPath(b, p);
            }
        }

        public static void FillRoundedGradient(Graphics g, Color c1, Color c2, RectangleF r, float radius)
        {
            if (r.Width <= 1 || r.Height <= 1) return;
            using (GraphicsPath p = RoundRect(r, radius))
            using (LinearGradientBrush b = new LinearGradientBrush(
                new PointF(r.X, r.Y), new PointF(r.X + r.Width, r.Y + r.Height), c1, c2))
            {
                g.FillPath(b, p);
            }
        }

        public static void StrokeRounded(Graphics g, Color color, RectangleF r, float radius, float width)
        {
            using (GraphicsPath p = RoundRect(r, radius))
            using (Pen pen = new Pen(color, width))
            {
                pen.Alignment = PenAlignment.Inset;
                g.DrawPath(pen, p);
            }
        }

        public static void Shadow(Graphics g, RectangleF r, float radius, int alpha, float spread)
        {
            Shadow(g, r, radius, alpha, spread, ShadowTint);
        }

        public static void Shadow(Graphics g, RectangleF r, float radius, int alpha, float spread, Color tint)
        {
            int a = (int)(alpha * _p.ShadowMul);
            for (int i = 4; i >= 1; i--)
            {
                float e = spread * i / 4f;
                RectangleF rr = new RectangleF(r.X - e, r.Y - e + spread * 0.6f, r.Width + e * 2, r.Height + e * 2);
                using (GraphicsPath p = RoundRect(rr, radius + e))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(Math.Max(1, a / i), tint)))
                {
                    g.FillPath(b, p);
                }
            }
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, RectangleF r,
            StringAlignment align, StringAlignment line)
        {
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = align;
                sf.LineAlignment = line;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.DrawString(text, font, b, r, sf);
                }
            }
        }

        public static void DrawTextNoTrim(Graphics g, string text, Font font, Color color, RectangleF r,
            StringAlignment align, StringAlignment line)
        {
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = align;
                sf.LineAlignment = line;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                using (SolidBrush b = new SolidBrush(color))
                {
                    g.DrawString(text, font, b, r, sf);
                }
            }
        }

        /// <summary>Logo：圆角方块沿对角线一分为二，左下浅底、右上蓝，C 字形在两半上反相配色。</summary>
        public static void DrawLogo(Graphics g, RectangleF r)
        {
            float radius = r.Width * 0.24f;
            GraphicsState st = g.Save();
            using (GraphicsPath tile = RoundRect(r, radius))
            {
                g.SetClip(tile, CombineMode.Replace);

                using (LinearGradientBrush bg = new LinearGradientBrush(
                    new PointF(r.X, r.Bottom), new PointF(r.Right, r.Y),
                    LogoBase1, LogoBase2))
                {
                    g.FillRectangle(bg, r);
                }

                PointF[] tri =
                {
                    new PointF(r.X, r.Y), new PointF(r.Right, r.Y), new PointF(r.Right, r.Bottom)
                };
                using (GraphicsPath tp = new GraphicsPath())
                {
                    tp.AddPolygon(tri);
                    using (LinearGradientBrush b = new LinearGradientBrush(
                        new PointF(r.Right, r.Y), new PointF(r.X + r.Width * 0.12f, r.Bottom),
                        Color.FromArgb(47, 123, 255), Color.FromArgb(34, 199, 232)))
                    {
                        g.FillPath(b, tp);
                    }
                }

                // 浅色一半与页面同色，需要一圈极淡的描边保证轮廓
                StrokeRounded(g, _p.Dark ? Color.FromArgb(90, 120, 160, 214) : Color.FromArgb(72, 120, 160, 214),
                    r, radius, 1f);

                DrawCLogo(g, r, LogoInk);
                GraphicsState s2 = g.Save();
                using (GraphicsPath blue = new GraphicsPath())
                {
                    blue.AddPolygon(tri);
                    using (Region reg = new Region(blue))
                    {
                        g.IntersectClip(reg);
                    }
                }
                DrawCLogo(g, r, Color.White);
                g.Restore(s2);
                g.Restore(st);
            }
        }

        /// <summary>只画 C 字形（开口圆环 + 两个圆头），供 Logo 与启动动画复用。</summary>
        public static void DrawCLogo(Graphics g, RectangleF r, Color color)
        {
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            float rad = r.Width * 0.29f;
            float penW = Math.Max(1.6f, r.Width * 0.155f);
            RectangleF ring = new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2);
            using (Pen pen = new Pen(color, penW))
            using (SolidBrush cap = new SolidBrush(color))
            {
                g.DrawArc(pen, ring, 42f, 276f);
                float cr = penW / 2f;
                double a1 = Math.PI * 42.0 / 180.0;
                double a2 = Math.PI * 318.0 / 180.0;
                g.FillEllipse(cap, cx + rad * (float)Math.Cos(a1) - cr, cy + rad * (float)Math.Sin(a1) - cr, penW, penW);
                g.FillEllipse(cap, cx + rad * (float)Math.Cos(a2) - cr, cy + rad * (float)Math.Sin(a2) - cr, penW, penW);
            }
        }

        public static void DrawLock(Graphics g, RectangleF r, Color color)
        {
            using (Pen p = new Pen(color, 1.4f))
            {
                float bw = r.Width * 0.62f;
                float bh = r.Height * 0.42f;
                float bx = r.X + (r.Width - bw) / 2f;
                float by = r.Y + r.Height * 0.5f;
                RectangleF body = new RectangleF(bx, by, bw, bh);
                FillRounded(g, A(color, 16), body, 3f);
                StrokeRounded(g, color, body, 3f, 1.2f);
                RectangleF arc = new RectangleF(r.X + r.Width * 0.28f, r.Y + r.Height * 0.22f,
                    r.Width * 0.44f, r.Height * 0.46f);
                g.DrawArc(p, arc, 180f, 180f);
                g.DrawLine(p, arc.X, arc.Y + arc.Height / 2f, arc.X, arc.Y + arc.Height);
                g.DrawLine(p, arc.Right, arc.Y + arc.Height / 2f, arc.Right, arc.Y + arc.Height);
            }
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            int r = (int)(a.R + (b.R - a.R) * t);
            int gg = (int)(a.G + (b.G - a.G) * t);
            int bb = (int)(a.B + (b.B - a.B) * t);
            int aa = (int)(a.A + (b.A - a.A) * t);
            return Color.FromArgb(aa, r, gg, bb);
        }

        public static string Bytes(long bytes)
        {
            if (bytes < 0) bytes = 0;
            if (bytes < 1024) return bytes.ToString() + " B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return kb.ToString("0.0") + " KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return mb.ToString("0.0") + " MB";
            double gb = mb / 1024.0;
            if (gb < 1024) return gb.ToString(gb < 100 ? "0.00" : "0.0") + " GB";
            return (gb / 1024.0).ToString("0.00") + " TB";
        }

        public static string Count(long n)
        {
            return n.ToString("N0");
        }

        public static string Duration(long ms)
        {
            if (ms < 1000) return ms + " ms";
            double s = ms / 1000.0;
            if (s < 60) return s.ToString("0.0") + " s";
            return ((int)(s / 60)) + " min " + ((int)(s % 60)) + " s";
        }

        public static Color RiskColor(RiskLevel risk)
        {
            if (risk == RiskLevel.Safe) return Green;
            if (risk == RiskLevel.Caution) return Amber;
            if (risk == RiskLevel.Protected) return Gray;
            return Gray;
        }

        public static string RiskText(RiskLevel risk)
        {
            return Loc.RiskText(risk);
        }

        public static void Chip(Graphics g, string text, Color color, RectangleF r)
        {
            FillRounded(g, A(color, 20), r, r.Height / 2f);
            StrokeRounded(g, A(color, 48), r, r.Height / 2f, 1f);
            Font f = Font(8.5f, FontStyle.Regular);
            DrawText(g, text, f, color,
                new RectangleF(r.X, r.Y + 1, r.Width, r.Height - 1), StringAlignment.Center, StringAlignment.Center);
        }

        public static float Dpi(Control c)
        {
            return c.DeviceDpi / 96f;
        }
    }

    public static class Anim
    {
        private class Item
        {
            public string Key;
            public float From;
            public float To;
            public int Duration;
            public int Start;
            public Action<float> Update;
            public Action Done;
            public bool Loop;
        }

        private static readonly List<Item> Items = new List<Item>();
        private static Timer Timer;
        /// <summary>调试用：动画定时器实际触发的次数。</summary>
        public static long TickCount;

        private static void EnsureTimer()
        {
            if (Timer == null)
            {
                Timer = new Timer();
                Timer.Interval = 15;
                Timer.Tick += delegate { Tick(); };
            }
            if (!Timer.Enabled) Timer.Start();
        }

        public static void Tween(string key, float from, float to, int duration, Action<float> update, Action done)
        {
            Stop(key);
            Item it = new Item();
            it.Key = key;
            it.From = from;
            it.To = to;
            it.Duration = duration;
            it.Start = Environment.TickCount;
            it.Update = update;
            it.Done = done;
            it.Loop = false;
            Items.Add(it);
            EnsureTimer();
        }

        public static void Loop(string key, Action<float> update)
        {
            Stop(key);
            Item it = new Item();
            it.Key = key;
            it.Loop = true;
            it.Start = Environment.TickCount;
            it.Update = update;
            Items.Add(it);
            EnsureTimer();
        }

        public static void Stop(string key)
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (Items[i].Key == key) Items.RemoveAt(i);
            }
            if (Items.Count == 0 && Timer != null) Timer.Stop();
        }

        public static void StopAll()
        {
            Items.Clear();
            if (Timer != null) Timer.Stop();
        }

        private static void Tick()
        {
            TickCount++;
            int now = Environment.TickCount;
            List<Action> dones = null;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                Item it = Items[i];
                if (it.Loop)
                {
                    it.Update((float)unchecked(now - it.Start));
                    continue;
                }
                float t = it.Duration <= 0 ? 1f : Math.Min(1f, (float)unchecked(now - it.Start) / it.Duration);
                float v = it.From + (it.To - it.From) * t;
                it.Update(v);
                if (t >= 1f)
                {
                    Items.RemoveAt(i);
                    if (it.Done != null)
                    {
                        if (dones == null) dones = new List<Action>();
                        dones.Add(it.Done);
                    }
                }
            }
            if (dones != null)
            {
                for (int i = 0; i < dones.Count; i++) dones[i]();
            }
            if (Items.Count == 0 && Timer != null) Timer.Stop();
        }

        public static float EaseOut(float t)
        {
            float u = 1f - Math.Max(0f, Math.Min(1f, t));
            return 1f - u * u * u;
        }

        public static float EaseInOut(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
        }

        public static float EaseOutBack(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1f, 3) + c1 * (float)Math.Pow(t - 1f, 2);
        }
    }
}
