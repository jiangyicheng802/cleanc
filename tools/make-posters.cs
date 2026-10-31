using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace CleanC
{
    /// <summary>宣传图生成器：左边一个程序 Logo，右边一行产品名，别的一律不要。</summary>
    public static class PosterTool
    {
        private static readonly Color Paper = Color.FromArgb(250, 248, 242);
        private static readonly Color Ink = Color.FromArgb(24, 24, 26);

        private const string Title = "磁盘清理工具";

        public static int Main(string[] args)
        {
            try
            {
                Theme.Set(ThemeMode.Light, false);
                Poster(@"CleanC-poster-3x4.png", 1200, 1600, 210, 112);
                Poster(@"CleanC-poster-4x3.png", 1600, 1200, 230, 122);
                Poster(@"CleanC-poster-1x1.png", 1200, 1200, 190, 100);
                Poster(@"CleanC-github-cover.png", 1280, 640, 150, 80);
                Console.WriteLine("posters saved");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("failed: " + ex);
                return 1;
            }
        }

        /// <summary>纸面正中偏上：左边 Logo，右边标题。</summary>
        private static void Poster(string path, int w, int h, float logoSize, float titlePx)
        {
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Paper);

                using (Font f = new Font("Microsoft YaHei UI", titlePx, FontStyle.Bold,
                           GraphicsUnit.Pixel))
                {
                    float tw = g.MeasureString(Title, f).Width;
                    float gap = logoSize * 0.26f;
                    float total = logoSize + gap + tw;
                    float x = (w - total) * 0.5f;
                    float y = (h - logoSize) * 0.5f - h * 0.06f;

                    Theme.DrawLogo(g, new RectangleF(x, y, logoSize, logoSize));

                    using (SolidBrush b = new SolidBrush(Ink))
                    {
                        g.DrawString(Title, f, b, x + logoSize + gap,
                            y + (logoSize - f.GetHeight(g)) * 0.5f);
                    }
                }
            }
            bmp.Save(path, ImageFormat.Png);
            bmp.Dispose();
        }
    }
}
