using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace CleanC
{
    /// <summary>把启动动画第一段“C 蹦跳入场”单独导出为循环 GIF。</summary>
    public static class BounceGif
    {
        private static int W = 480;
        private static int H = 480;
        private static float S = 1f;
        private const float Duration = 1280f;
        private const int StepMs = 20;
        private static int HoldFrames = 8;

        public static int Main(string[] args)
        {
            string outPath = args != null && args.Length > 0 ? args[0] : @"_artifacts\C-bounce.gif";
            bool pngMode = args != null && args.Length > 1 && args[1] == "png";
            try
            {
                Theme.Set(ThemeMode.Light, false);
                if (pngMode)
                {
                    W = 1080;
                    H = 1080;
                    S = W / 480f;
                    HoldFrames = 20;
                }
                Bitmap bg = RenderBackground();

                int count = (int)(Duration / StepMs) + 1;
                List<Bitmap> frames = new List<Bitmap>();
                for (int i = 0; i < count; i++)
                {
                    frames.Add(RenderFrame(i * StepMs, bg));
                }
                for (int i = 0; i < HoldFrames; i++)
                {
                    frames.Add(RenderFrame(Duration, bg));
                }

                if (pngMode)
                {
                    string dir = outPath;
                    Directory.CreateDirectory(dir);
                    for (int i = 0; i < frames.Count; i++)
                    {
                        frames[i].Save(Path.Combine(dir, "f" + i.ToString("D3") + ".png"),
                            ImageFormat.Png);
                    }
                    Console.WriteLine("saved " + frames.Count + " png frames to " + Path.GetFullPath(dir));
                }
                else
                {
                    int[] palette = BuildPalette(frames, 256);
                    int[] map = BuildColorMap(palette);
                    WriteGif(outPath, frames, palette, map);
                    Console.WriteLine("saved " + Path.GetFullPath(outPath) + " (" + frames.Count + " frames)");
                }

                for (int i = 0; i < frames.Count; i++) frames[i].Dispose();
                bg.Dispose();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("failed: " + ex);
                return 1;
            }
        }

        private static float Seg(float t, float a, float b)
        {
            float v = (t - a) / (b - a);
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        /// <summary>与启动页一致的浅色背景光晕，整段动画只计算一次。</summary>
        private static Bitmap RenderBackground()
        {
            Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Theme.Bg);
                Glow(g, W / 2f, H / 2f - 30 * S, 190 * S, Theme.Blue, 20);
                Glow(g, W / 2f - 110 * S, H / 2f - 90 * S, 150 * S, Theme.Violet, 12);
                Glow(g, W / 2f + 120 * S, H / 2f + 70 * S, 160 * S, Theme.Cyan, 10);
            }
            return bmp;
        }

        /// <summary>平滑径向光晕（路径渐变，避免同心圆色阶）。</summary>
        private static void Glow(Graphics g, float cx, float cy, float r, Color c, int alpha)
        {
            if (r <= 1f || alpha <= 0) return;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(cx - r, cy - r, r * 2f, r * 2f);
                using (PathGradientBrush b = new PathGradientBrush(path))
                {
                    b.CenterPoint = new PointF(cx, cy);
                    b.CenterColor = Color.FromArgb(alpha, c);
                    b.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                    b.FocusScales = new PointF(0f, 0f);
                    g.FillPath(b, path);
                }
            }
        }

        /// <summary>按 SplashOverlay 阶段一的原始节奏放大重绘：三连跳入场、挤压回弹、落地小跳。</summary>
        private static Bitmap RenderFrame(float t, Bitmap bg)
        {
            Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawImageUnscaled(bg, 0, 0);

                const float K0 = 2.15f;
                float K = K0 * S;
                float cx = W / 2f;
                float groundY = H * 0.70f;
                float hp = Seg(t, 0f, 900f);
                float restFade = Seg(t, 900f, 1120f);
                float cSize = 68f * K * (0.86f + 0.14f * Anim.EaseOut(hp));
                float startX = -cSize;
                float cX = startX + (cx - startX) * Anim.EaseOut(hp);
                float lift = Math.Abs((float)Math.Sin(hp * 3f * (float)Math.PI)) * 96f * K * (1f - hp * 0.45f);
                if (hp >= 1f)
                {
                    lift = Math.Abs((float)Math.Sin((t - 900f) / 320f * (float)Math.PI)) * 5f * K * (1f - restFade);
                }
                float cY = groundY - cSize / 2f - lift;
                float ground = Math.Min(1f, lift / (30f * K));
                float squash = 1f;
                if (hp < 1f) squash = 1f - 0.15f * (1f - Math.Min(1f, lift / (26f * K)));
                float rot = (float)Math.Sin(hp * 6.0 * (float)Math.PI) * 7f * (1f - hp);

                if (hp > 0.02f)
                {
                    float sw = cSize * (0.92f - 0.22f * ground);
                    int sa = (int)((46 - 26 * ground) * Math.Min(1f, hp * 3f));
                    using (SolidBrush sh = new SolidBrush(Color.FromArgb(Math.Max(0, sa), Theme.Blue)))
                    {
                        g.FillEllipse(sh, cX - sw / 2f, groundY - 5 * S, sw, 10 * S);
                    }
                }
                if (hp > 0f)
                {
                    GraphicsState cs = g.Save();
                    g.TranslateTransform(cX, cY);
                    g.RotateTransform(rot);
                    g.ScaleTransform(1f / squash, squash);
                    Theme.DrawCLogo(g, new RectangleF(-cSize / 2f, -cSize / 2f, cSize, cSize), Theme.Blue);
                    g.Restore(cs);
                }
            }
            return bmp;
        }

        // ------------------------------------------------------------ 调色板

        private static int BinOf(Color c)
        {
            return ((c.R >> 3) << 10) | ((c.G >> 3) << 5) | (c.B >> 3);
        }

        private static int[] BuildPalette(List<Bitmap> frames, int max)
        {
            int[] bins = new int[32768];
            for (int f = 0; f < frames.Count; f++)
            {
                Bitmap bmp = frames[f];
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppRgb);
                unsafe
                {
                    byte* p = (byte*)data.Scan0;
                    int stride = data.Stride;
                    for (int y = 0; y < H; y++)
                    {
                        byte* row = p + y * stride;
                        for (int x = 0; x < W; x++)
                        {
                            int b = row[x * 4];
                            int g = row[x * 4 + 1];
                            int r = row[x * 4 + 2];
                            bins[((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3)]++;
                        }
                    }
                }
                bmp.UnlockBits(data);
            }

            List<int> used = new List<int>();
            for (int i = 0; i < bins.Length; i++)
            {
                if (bins[i] > 0) used.Add(i);
            }
            List<int[]> boxes = new List<int[]>();
            boxes.Add(used.ToArray());
            while (boxes.Count < max)
            {
                int pick = -1;
                long best = -1;
                for (int i = 0; i < boxes.Count; i++)
                {
                    int[] box = boxes[i];
                    if (box.Length < 2) continue;
                    long pop = 0;
                    for (int k = 0; k < box.Length; k++) pop += bins[box[k]];
                    if (pop > best)
                    {
                        best = pop;
                        pick = i;
                    }
                }
                if (pick < 0) break;
                int[] target = boxes[pick];
                int axis = -1;
                int range = -1;
                for (int a = 0; a < 3; a++)
                {
                    int minV = 255, maxV = 0;
                    for (int k = 0; k < target.Length; k++)
                    {
                        int v = Channel(target[k], a);
                        if (v < minV) minV = v;
                        if (v > maxV) maxV = v;
                    }
                    if (maxV - minV > range)
                    {
                        range = maxV - minV;
                        axis = a;
                    }
                }
                if (axis < 0 || range <= 0) break;
                int ax = axis;
                Array.Sort(target, delegate(int p, int q) { return Channel(p, ax).CompareTo(Channel(q, ax)); });
                long total = 0;
                for (int k = 0; k < target.Length; k++) total += bins[target[k]];
                long acc = 0;
                int split = 1;
                for (int k = 0; k < target.Length - 1; k++)
                {
                    acc += bins[target[k]];
                    if (acc * 2 >= total)
                    {
                        split = k + 1;
                        break;
                    }
                }
                if (split < 1) split = 1;
                if (split > target.Length - 1) split = target.Length - 1;
                int[] left = new int[split];
                int[] right = new int[target.Length - split];
                Array.Copy(target, 0, left, 0, split);
                Array.Copy(target, split, right, 0, right.Length);
                boxes[pick] = left;
                boxes.Add(right);
            }

            List<int> pal = new List<int>();
            for (int i = 0; i < boxes.Count; i++)
            {
                int[] box = boxes[i];
                long pop = 0, r = 0, g = 0, b = 0;
                for (int k = 0; k < box.Length; k++)
                {
                    long c = bins[box[k]];
                    pop += c;
                    r += (long)Channel(box[k], 2) * c;
                    g += (long)Channel(box[k], 1) * c;
                    b += (long)Channel(box[k], 0) * c;
                }
                if (pop == 0) continue;
                pal.Add(Color.FromArgb((int)(r / pop), (int)(g / pop), (int)(b / pop)).ToArgb());
            }
            while (pal.Count < max) pal.Add(Color.FromArgb(0, 0, 0).ToArgb());
            return pal.ToArray();
        }

        private static int Channel(int bin, int axis)
        {
            if (axis == 2) return ((bin >> 10) & 31) * 8 + 4;
            if (axis == 1) return ((bin >> 5) & 31) * 8 + 4;
            return (bin & 31) * 8 + 4;
        }

        private static int[] BuildColorMap(int[] palette)
        {
            int[] map = new int[32768];
            for (int i = 0; i < map.Length; i++)
            {
                int r = Channel(i, 2);
                int g = Channel(i, 1);
                int b = Channel(i, 0);
                map[i] = Nearest(palette, Color.FromArgb(r, g, b));
            }
            return map;
        }

        private static int Clamp(int v)
        {
            return v < 0 ? 0 : (v > 255 ? 255 : v);
        }

        private static int Nearest(int[] palette, Color c)
        {
            int best = 0;
            long bestD = long.MaxValue;
            for (int i = 0; i < 256; i++)
            {
                int pr = (palette[i] >> 16) & 0xFF;
                int pg = (palette[i] >> 8) & 0xFF;
                int pb = palette[i] & 0xFF;
                long dr = pr - c.R, dg = pg - c.G, db = pb - c.B;
                long d = dr * dr + dg * dg + db * db;
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Floyd–Steinberg 抖动量化，让光晕渐变在 256 色 GIF 里依然平滑。</summary>
        private static byte[] Quantize(Bitmap bmp, int[] palette, int[] map)
        {
            byte[] idx = new byte[W * H];
            float[] cr = new float[W + 2];
            float[] cg = new float[W + 2];
            float[] cb = new float[W + 2];
            float[] nr = new float[W + 2];
            float[] ng = new float[W + 2];
            float[] nb = new float[W + 2];
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly,
                PixelFormat.Format32bppRgb);
            unsafe
            {
                byte* p = (byte*)data.Scan0;
                int stride = data.Stride;
                for (int y = 0; y < H; y++)
                {
                    for (int i = 0; i < W + 2; i++)
                    {
                        nr[i] = 0f;
                        ng[i] = 0f;
                        nb[i] = 0f;
                    }
                    byte* row = p + y * stride;
                    for (int x = 0; x < W; x++)
                    {
                        int b = Clamp((int)(row[x * 4] + cb[x + 1]));
                        int g = Clamp((int)(row[x * 4 + 1] + cg[x + 1]));
                        int r = Clamp((int)(row[x * 4 + 2] + cr[x + 1]));
                        int v = map[((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3)];
                        idx[y * W + x] = (byte)v;
                        int pr = (palette[v] >> 16) & 0xFF;
                        int pg = (palette[v] >> 8) & 0xFF;
                        int pb = palette[v] & 0xFF;
                        float er = r - pr;
                        float eg = g - pg;
                        float eb = b - pb;
                        cr[x + 2] += er * 0.4375f;
                        cg[x + 2] += eg * 0.4375f;
                        cb[x + 2] += eb * 0.4375f;
                        nr[x] += er * 0.1875f;
                        ng[x] += eg * 0.1875f;
                        nb[x] += eb * 0.1875f;
                        nr[x + 1] += er * 0.3125f;
                        ng[x + 1] += eg * 0.3125f;
                        nb[x + 1] += eb * 0.3125f;
                        nr[x + 2] += er * 0.0625f;
                        ng[x + 2] += eg * 0.0625f;
                        nb[x + 2] += eb * 0.0625f;
                    }
                    float[] t0 = cr; cr = nr; nr = t0;
                    t0 = cg; cg = ng; ng = t0;
                    t0 = cb; cb = nb; nb = t0;
                }
            }
            bmp.UnlockBits(data);
            return idx;
        }

        // ------------------------------------------------------------ GIF 写出

        private static void WriteGif(string path, List<Bitmap> frames, int[] palette, int[] map)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                WriteHeader(fs, palette);
                for (int i = 0; i < frames.Count; i++)
                {
                    int delay = i < frames.Count - HoldFrames ? 2 : 5;
                    byte[] idx = Quantize(frames[i], palette, map);
                    WriteFrame(fs, idx, delay);
                    if ((i + 1) % 10 == 0) Console.WriteLine("frame " + (i + 1) + "/" + frames.Count);
                }
                fs.WriteByte(0x3B);
            }
        }

        private static void WriteHeader(Stream s, int[] palette)
        {
            s.WriteByte(0x47); s.WriteByte(0x49); s.WriteByte(0x46); s.WriteByte(0x38); s.WriteByte(0x39);
            s.WriteByte(0x61);
            WriteU16(s, W);
            WriteU16(s, H);
            s.WriteByte(0xF7); // 全局色表 256 色
            s.WriteByte(0);
            s.WriteByte(0);
            for (int i = 0; i < 256; i++)
            {
                s.WriteByte((byte)((palette[i] >> 16) & 0xFF));
                s.WriteByte((byte)((palette[i] >> 8) & 0xFF));
                s.WriteByte((byte)(palette[i] & 0xFF));
            }
            // NETSCAPE2.0 无限循环
            s.WriteByte(0x21); s.WriteByte(0xFF); s.WriteByte(0x0B);
            byte[] app = { 0x4E, 0x45, 0x54, 0x53, 0x43, 0x41, 0x50, 0x45, 0x32, 0x2E, 0x30 };
            for (int i = 0; i < app.Length; i++) s.WriteByte(app[i]);
            s.WriteByte(0x03); s.WriteByte(0x01); s.WriteByte(0x00); s.WriteByte(0x00); s.WriteByte(0x00);
        }

        private static void WriteFrame(Stream s, byte[] idx, int delay)
        {
            s.WriteByte(0x21); s.WriteByte(0xF9); s.WriteByte(0x04); s.WriteByte(0x04);
            WriteU16(s, (ushort)delay);
            s.WriteByte(0x00); s.WriteByte(0x00);
            s.WriteByte(0x2C);
            WriteU16(s, 0); WriteU16(s, 0); WriteU16(s, W); WriteU16(s, H);
            s.WriteByte(0x00);
            s.WriteByte(0x08);
            byte[] packed = Lzw(idx);
            for (int i = 0; i < packed.Length; i += 255)
            {
                int n = Math.Min(255, packed.Length - i);
                s.WriteByte((byte)n);
                for (int k = 0; k < n; k++) s.WriteByte(packed[i + k]);
            }
            s.WriteByte(0x00);
        }

        private static void WriteU16(Stream s, int v)
        {
            s.WriteByte((byte)(v & 0xFF));
            s.WriteByte((byte)((v >> 8) & 0xFF));
        }

        private static byte[] Lzw(byte[] pixels)
        {
            const int Clear = 256;
            const int End = 257;
            int codeSize = 9;
            int next = 258;
            Dictionary<int, int> dict = new Dictionary<int, int>();
            List<byte> outBytes = new List<byte>();
            int bitBuf = 0;
            int bitCount = 0;

            Action<int> emit = delegate(int code)
            {
                bitBuf |= code << bitCount;
                bitCount += codeSize;
                while (bitCount >= 8)
                {
                    outBytes.Add((byte)(bitBuf & 0xFF));
                    bitBuf >>= 8;
                    bitCount -= 8;
                }
            };

            emit(Clear);
            int prefix = pixels[0];
            for (int i = 1; i < pixels.Length; i++)
            {
                int c = pixels[i];
                int key = (prefix << 8) | c;
                int code;
                if (dict.TryGetValue(key, out code))
                {
                    prefix = code;
                    continue;
                }
                emit(prefix);
                if (next < 4096)
                {
                    dict[key] = next;
                    next++;
                    if (next > (1 << codeSize) && codeSize < 12) codeSize++;
                }
                else
                {
                    emit(Clear);
                    dict.Clear();
                    codeSize = 9;
                    next = 258;
                }
                prefix = c;
            }
            emit(prefix);
            emit(End);
            if (bitCount > 0) outBytes.Add((byte)(bitBuf & 0xFF));
            return outBytes.ToArray();
        }
    }
}
