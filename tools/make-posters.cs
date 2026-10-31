using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace CleanC
{
    /// <summary>宣传海报生成器：米白纸面 + 真实截图 + 开发者口吻文案。</summary>
    public static class PosterTool
    {
        private static readonly Color Paper = Color.FromArgb(250, 248, 242);
        private static readonly Color Ink = Color.FromArgb(24, 24, 26);
        private static readonly Color Gray = Color.FromArgb(108, 104, 98);
        private static readonly Color Hair = Color.FromArgb(229, 223, 212);
        private static readonly Color Soft = Color.FromArgb(160, 154, 144);

        private static readonly Color RiskSafe = Color.FromArgb(22, 163, 74);
        private static readonly Color RiskCaution = Color.FromArgb(217, 119, 6);
        private static readonly Color RiskRisky = Color.FromArgb(220, 38, 38);
        private static readonly Color RiskForbidden = Color.FromArgb(30, 34, 42);

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
                Blob(g, w - 40, -30, 420, 22);

                Logo(g, 80f, 76f, 58f);
                Sans(g, "CleanC", 26, true, Ink, 152, 82);
                Sans(g, "v2.6", 17, false, Gray, 154, 116);

                Headline(g, "C 盘又红了？", 82, 80, 208, 46);
                Sans(g, "能删什么、不能碰什么，都写得明明白白。", 30, false, Ink, 80, 300);
                Sans(g, "除了清理，还多了一个 SpaceSniffer 式文件地图。", 22, false, Gray, 80, 352);

                Shot(g, _map, 80, 410, 1040);
                Sans(g, "文件模式：面积就是体积，绿 / 黄 / 红 / 黑 四级风险一目了然", 21, false, Gray, 80, 1020);

                Legend(g, 80, 1062, 1040);

                Sans(g, "四档清理 · 强力卸载 · 16 步残留 · AppData 搬家", 26, false, Ink, 80, 1164);
                Sans(g, "浅色 / 深色 / 金色三套外观 · 简体中文 / 繁體 / English", 21, false, Gray, 80, 1206);

                Rule(g, 80, 1258, 1040);

                Qr(g, 80, 1286, 150);
                Sans(g, "开源免费 · 单文件 exe，免安装", 25, true, Ink, 262, 1290);
                Sans(g, Repo, 22, false, Theme.Blue, 262, 1330);
                Sans(g, "扫一扫直接下载 · B 站搜索：lemonvia76", 19, false, Gray, 262, 1366);

                Script(g, "lemonvia76", 42, Theme.Blue, 80, 1462);
                Sans(g, "免费 · 开源 · 不上传任何数据", 19, false, Gray, 86, 1520);
                Script(g, "make for lemonvia76", 20, Soft, w - 84 - 320, 1512, true);
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
                Blob(g, w - 40, -40, 460, 20);

                Logo(g, 80f, 70f, 54f);
                Sans(g, "CleanC", 24, true, Ink, 148, 76);
                Sans(g, "v2.6", 16, false, Gray, 150, 108);

                Headline(g, "C 盘又红了？", 70, 80, 196, 40);
                Sans(g, "能删什么、不能碰什么，", 27, false, Ink, 80, 272);
                Sans(g, "都写得明明白白。", 27, false, Ink, 80, 314);

                string[] bullets =
                {
                    "四档扫描范围，从秒级到全盘三轮",
                    "卸载软件：普通 / 强力，逐项回查",
                    "16 步残留扫描，注册表先备份再删",
                    "AppData 目录联接搬家，C 盘立刻腾地方",
                    "文件模式：地图右键直接删 / 剪切 / 排查垃圾"
                };
                for (int i = 0; i < bullets.Length; i++)
                {
                    Bullet(g, bullets[i], 80, 380 + i * 44, 22, Theme.Blue);
                }

                Qr(g, 80, 620, 132);
                Sans(g, "开源免费 · 免安装", 23, true, Ink, 240, 624);
                Sans(g, Repo, 20, false, Theme.Blue, 240, 660);
                Sans(g, "B 站搜索：lemonvia76", 18, false, Gray, 240, 694);

                Script(g, "lemonvia76", 38, Theme.Blue, 80, 826);
                Sans(g, "免费 · 开源 · 不上传任何数据", 18, false, Gray, 86, 882);

                Shot(g, _map, 680, 86, 840);
                Sans(g, "文件模式 · 四级风险地图", 19, false, Gray, 680, 580);
                Shot(g, _main, 680, 618, 840);
                Sans(g, "普通模式 · 四档清理与残留扫描", 19, false, Gray, 680, 1112);

                Script(g, "make for lemonvia76", 20, Soft, 680, 1146);
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
                Blob(g, w - 30, -30, 380, 22);

                Logo(g, 60f, 56f, 52f);
                Sans(g, "CleanC", 23, true, Ink, 124, 62);
                Sans(g, "v2.6", 16, false, Gray, 126, 94);

                Headline(g, "C 盘又红了？", 62, 60, 168, 36);
                Sans(g, "能删什么、不能碰什么，都写得明明白白。", 25, false, Ink, 60, 244);

                Shot(g, _main, 60, 306, 1080);
                Sans(g, "真实界面截图 · 本地运行 · 不上传任何文件", 19, false, Gray, 60, 928);
                Sans(g, "四档清理 · 强力卸载 · 16 步残留 · AppData 搬家 · 文件地图", 23, false, Ink, 60, 964);

                Rule(g, 60, 1012, 1080);

                Script(g, "lemonvia76", 36, Theme.Blue, 60, 1040);
                Sans(g, "免费 · 开源 · 免安装", 19, false, Gray, 66, 1096);
                Sans(g, Repo, 21, false, Theme.Blue, 66, 1130);
                Qr(g, 1010, 1006, 130);
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
                Blob(g, w - 30, -40, 400, 20);

                Logo(g, 72f, 62f, 52f);
                Sans(g, "CleanC", 24, true, Ink, 138, 68);
                Sans(g, "v2.6 · 免费开源", 16, false, Gray, 140, 100);

                Headline(g, "C 盘又红了？", 56, 72, 172, 32);
                Sans(g, "能删什么、不能碰什么，", 24, false, Ink, 72, 240);
                Sans(g, "都写得明明白白。", 24, false, Ink, 72, 276);

                string[] bullets =
                {
                    "四档清理 · 强力卸载 · 16 步残留",
                    "AppData 搬家 · SpaceSniffer 式文件地图",
                    "单文件 exe · 免安装 · 本地不上传"
                };
                for (int i = 0; i < bullets.Length; i++)
                {
                    Bullet(g, bullets[i], 72, 328 + i * 38, 20, Theme.Blue);
                }

                Sans(g, Repo, 20, false, Theme.Blue, 72, 462);
                Qr(g, 72, 490, 100);

                Shot(g, _map, 548, 86, 668);
                Sans(g, "文件模式 · 面积即体积 · 绿 / 黄 / 红 / 黑 四级风险", 18, false, Gray, 548, 486);

                Script(g, "lemonvia76", 30, Theme.Blue, 548, 526);
                Sans(g, "免费 · 开源 · 免安装", 18, false, Gray, 790, 534);
            }
            bmp.Save(path, ImageFormat.Png);
            bmp.Dispose();
        }

        // ------------------------------------------------------------ 组件

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

        private static Font ScriptFont(float px)
        {
            return new Font("Segoe Script", px, FontStyle.Regular, GraphicsUnit.Pixel);
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

        private static void Script(Graphics g, string text, float px, Color color, float x, float y,
            bool rightAlign = false)
        {
            using (Font f = ScriptFont(px))
            using (SolidBrush b = new SolidBrush(color))
            {
                if (!rightAlign)
                {
                    g.DrawString(text, f, b, x, y);
                }
                else
                {
                    g.DrawString(text, f, b, new RectangleF(x, y, 320, px * 1.6f), Fmt(StringAlignment.Far));
                }
            }
        }

        /// <summary>带小圆点的列表项。</summary>
        private static void Bullet(Graphics g, string text, float x, float y, float size, Color dot)
        {
            using (SolidBrush b = new SolidBrush(dot))
            {
                float r = size * 0.19f;
                g.FillEllipse(b, x + 2, y + size * 0.36f, r, r);
            }
            Sans(g, text, size, false, Ink, x + size * 0.62f, y);
        }

        /// <summary>手写感标题：正文用雅黑，下方压一条略带起伏的蓝线。</summary>
        private static void Headline(Graphics g, string text, float px, float x, float y, float gap)
        {
            using (Font f = Sans(px, true))
            using (SolidBrush b = new SolidBrush(Ink))
            {
                g.DrawString(text, f, b, x, y - gap);
            }
            using (Font f = Sans(px, true))
            {
                float head = g.MeasureString("C 盘", f).Width - 6f;
                float mark = g.MeasureString("又红了", f).Width - 4f;
                float baseY = y - gap + f.GetHeight(g) - 4f;
                using (Pen p = new Pen(Theme.Blue, Math.Max(3f, px * 0.045f)))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        float x0 = x + head;
                        float x1 = x0 + mark;
                        float amp = px * 0.035f;
                        path.AddBezier(x0, baseY, x0 + mark * 0.3f, baseY - amp,
                            x0 + mark * 0.7f, baseY + amp, x1, baseY - amp * 0.4f);
                        g.DrawPath(p, path);
                    }
                }
            }
        }

        /// <summary>真实截图：白边、细描边、落下一点点影子。</summary>
        private static void Shot(Graphics g, string img, float x, float y, float w)
        {
            float inner = w - 40f;
            float ih = inner / 1.8154f;
            float h = ih + 40f;
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
                    g.DrawImage(src, new RectangleF(x + 20, y + 20, inner, ih));
                }
            }
            using (Pen p = new Pen(Color.FromArgb(236, 231, 222), 1f))
            {
                g.DrawRectangle(p, x + 20, y + 20, inner, ih);
            }
        }

        /// <summary>四级风险图例。</summary>
        private static void Legend(Graphics g, float x, float y, float w)
        {
            string[] names = { "可清理", "谨慎", "勿动", "绝对禁区" };
            string[] descs = { "规则判定安全", "建议先看再删", "个人资料 / 程序", "System32 等禁区" };
            Color[] colors = { RiskSafe, RiskCaution, RiskRisky, RiskForbidden };
            float cw = w / 4f;
            for (int i = 0; i < 4; i++)
            {
                float cx = x + i * cw;
                using (SolidBrush b = new SolidBrush(colors[i]))
                {
                    g.FillEllipse(b, cx + 2, y + 9, 14, 14);
                }
                Sans(g, names[i], 21, true, Ink, cx + 24, y);
                Sans(g, descs[i], 17, false, Gray, cx + 24, y + 30);
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
                    g.DrawImage(src, new RectangleF(x + 12, y + 12, size - 24, size - 24));
                }
            }
        }

        /// <summary>细分隔线。</summary>
        private static void Rule(Graphics g, float x, float y, float w)
        {
            using (Pen p = new Pen(Hair, 1.2f))
            {
                g.DrawLine(p, x, y, x + w, y);
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

        private static StringFormat Fmt(StringAlignment h)
        {
            StringFormat sf = new StringFormat();
            sf.Alignment = h;
            sf.LineAlignment = StringAlignment.Near;
            sf.FormatFlags = StringFormatFlags.NoWrap;
            return sf;
        }
    }
}
