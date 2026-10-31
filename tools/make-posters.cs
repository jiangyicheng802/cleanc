using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace CleanC
{
    /// <summary>宣传海报生成器：左边一个 C 字 Logo，右边写“C盘清理工具”，其余留白。</summary>
    public static class PosterTool
    {
        private static readonly Color Paper = Color.FromArgb(250, 248, 242);
        private static readonly Color Ink = Color.FromArgb(24, 24, 26);
        private static readonly Color Gray = Color.FromArgb(108, 104, 98);
        private static readonly Color Hair = Color.FromArgb(229, 223, 212);
        private static readonly Color Soft = Color.FromArgb(160, 154, 144);

        private const string Title = "C盘清理工具";
        private const string Repo = "github.com/jiangyicheng802/cleanc";

        private static string _main;
        private static string _map;
        private static string _qr;

        public static int Main(string[] args)
        {
            try
            {
                Theme.Set(ThemeMode.Light, false);
                _main = Path.GetFullPath(@"docs\main.png");
                _map = Path.GetFullPath(@"docs\filemode.png");
                _qr = Path.GetFullPath(@"assets\github-qr.png");

                Portrait(@"CleanC-poster-3x4.png", 1200, 1600);
                Landscape(@"CleanC-poster-4x3.png", 1600, 1200);
                Square(@"CleanC-poster-1x1.png", 1200, 1200);
                Cover(@"CleanC-github-cover.png", 1280, 640);
                Console.WriteLine("posters saved");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("failed: " + ex);
                return 1;
            }
        }

        // ------------------------------------------------------------ 3:4 竖版

        private static void Portrait(string path, int w, int h)
        {
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Prep(g);
                g.Clear(Paper);
                Blob(g, w - 30, -30, 400, 20);
                Mark(g, w - 250, h - 190, 340);

                Brand(g, 110, 170, 240, 104);

                Shot(g, _main, 110, 520, 980);

                Qr(g, 110, 1160, 150);
                Sans(g, Repo, 21, false, Theme.Blue, 290, 1216);
            }
            bmp.Save(path, ImageFormat.Png);
            bmp.Dispose();
        }

        // ------------------------------------------------------------ 4:3 横版

        private static void Landscape(string path, int w, int h)
        {
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Prep(g);
                g.Clear(Paper);
                Blob(g, w - 30, -40, 460, 20);
                Mark(g, w - 300, h - 150, 300);

                Brand(g, 90, 120, 210, 90);

                Shot(g, _main, 90, 420, 690);
                Shot(g, _map, 840, 420, 670);

                Qr(g, 90, 850, 130);
                Sans(g, Repo, 19, false, Theme.Blue, 250, 896);
            }
            bmp.Save(path, ImageFormat.Png);
            bmp.Dispose();
        }

        // ------------------------------------------------------------ 1:1 方版

        private static void Square(string path, int w, int h)
        {
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Prep(g);
                g.Clear(Paper);
                Blob(g, w - 20, -20, 340, 20);

                Brand(g, 80, 90, 190, 82);

                Shot(g, _main, 80, 350, 1040);

                Qr(g, 80, 950, 140);
                Sans(g, Repo, 21, false, Theme.Blue, 250, 998);
            }
            bmp.Save(path, ImageFormat.Png);
            bmp.Dispose();
        }

        // ------------------------------------------------------------ GitHub 封面

        private static void Cover(string path, int w, int h)
        {
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Prep(g);
                g.Clear(Paper);
                Blob(g, w - 20, -30, 360, 20);
                Mark(g, w - 190, h - 130, 230);

                Brand(g, 72, 96, 140, 52);

                Shot(g, _map, 626, 96, 584);

                Qr(g, 72, 470, 100);
                Sans(g, Repo, 18, false, Theme.Blue, 200, 508);
            }
            bmp.Save(path, ImageFormat.Png);
            bmp.Dispose();
        }

        // ------------------------------------------------------------ 组件

        /// <summary>左边 C 字 Logo，右边产品名。</summary>
        private static void Brand(Graphics g, float x, float y, float logoSize, float titlePx)
        {
            Logo(g, x, y, logoSize);
            using (Font f = Sans(titlePx, true))
            using (SolidBrush b = new SolidBrush(Ink))
            {
                float tw = g.MeasureString(Title, f).Width;
                g.DrawString(Title, f, b, x + logoSize + logoSize * 0.28f,
                    y + (logoSize - f.GetHeight(g)) * 0.5f);
            }
        }

        private static void Prep(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        private static Font Sans(float px, bool bold)
        {
            return new Font("Microsoft YaHei UI", px, bold ? FontStyle.Bold : FontStyle.Regular,
                GraphicsUnit.Pixel);
        }

        private static void Sans(Graphics g, string text, float px, bool bold, Color color,
            float x, float y)
        {
            using (Font f = Sans(px, bold))
            using (SolidBrush b = new SolidBrush(color))
            {
                g.DrawString(text, f, b, x, y);
            }
        }

        /// <summary>真实截图：白边、细描边、落下一点点影子。</summary>
        private static void Shot(Graphics g, string img, float x, float y, float w)
        {
            float inner = w - 36f;
            float ih = inner / 1.8154f;
            float h = ih + 36f;
            RectangleF frame = new RectangleF(x, y, w, h);

            for (int i = 3; i >= 1; i--)
            {
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(4, 60, 50, 40)))
                {
                    g.FillRectangle(sb, frame.X - 2, frame.Y + 8 + i * 4, frame.Width + 4,
                        frame.Height + 2);
                }
            }
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillRectangle(b, frame);
            using (Pen p = new Pen(Hair, 1.2f)) g.DrawRectangle(p, frame.X, frame.Y, frame.Width, frame.Height);

            if (File.Exists(img))
            {
                using (Image src = Image.FromFile(img))
                {
                    g.DrawImage(src, new RectangleF(x + 18, y + 18, inner, ih));
                }
            }
            using (Pen p = new Pen(Color.FromArgb(236, 231, 222), 1f))
            {
                g.DrawRectangle(p, x + 18, y + 18, inner, ih);
            }
        }

        /// <summary>二维码：白底圆角卡片 + 细描边。</summary>
        private static void Qr(Graphics g, float x, float y, float size)
        {
            RectangleF card = new RectangleF(x, y, size, size);
            using (GraphicsPath p = Round(card, 14f))
            {
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, p);
                using (Pen pen = new Pen(Hair, 1.2f)) g.DrawPath(pen, p);
            }
            if (File.Exists(_qr))
            {
                using (Image src = Image.FromFile(_qr))
                {
                    g.DrawImage(src, new RectangleF(x + 11, y + 11, size - 22, size - 22));
                }
            }
        }

        /// <summary>角落一抹几乎看不见的蓝色光晕，只让纸面不空。</summary>
        private static void Blob(Graphics g, float cx, float cy, float radius, int alpha)
        {
            RectangleF big = new RectangleF(cx - radius, cy - radius, radius * 2f, radius * 2f);
            using (GraphicsPath p = Round(big, radius))
            using (PathGradientBrush b = new PathGradientBrush(p))
            {
                b.CenterPoint = new PointF(cx, cy);
                b.CenterColor = Color.FromArgb(alpha, 30, 110, 255);
                b.SurroundColors = new Color[] { Color.FromArgb(0, 30, 110, 255) };
                g.FillPath(b, p);
            }
        }

        /// <summary>角落一个淡到几乎看不见的 C 水印。</summary>
        private static void Mark(Graphics g, float x, float y, float size)
        {
            Theme.DrawCLogo(g, new RectangleF(x, y, size, size), Color.FromArgb(16, 30, 110, 255));
        }

        private static void Logo(Graphics g, float x, float y, float size)
        {
            RectangleF r = new RectangleF(x, y, size, size);
            using (GraphicsPath p = Round(r, size * 0.26f))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, Theme.Blue,
                           Color.FromArgb(58, 68, 232), 40f))
                {
                    g.FillPath(b, p);
                }
            }
            Theme.DrawCLogo(g, new RectangleF(r.X + size * 0.18f, r.Y + size * 0.17f,
                size * 0.64f, size * 0.64f), Color.White);
        }

        private static GraphicsPath Round(RectangleF r, float rad)
        {
            float d = Math.Min(rad * 2f, Math.Min(r.Width, r.Height));
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
