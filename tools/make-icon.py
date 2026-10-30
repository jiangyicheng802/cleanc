"""Generate assets/app.ico: diagonal split logo (white lower-left, blue upper-right) with a two-tone C.

Pure standard library (zlib + struct), no Pillow required.
Run: python tools/make-icon.py
"""

import math
import os
import struct
import sys
import zlib

SIZES = [16, 24, 32, 48, 64, 128, 256]
SS = 4  # supersampling factor

BLUE = (47, 123, 255)
CYAN = (34, 199, 232)
WHITE = (255, 255, 255)
BORDER = (120, 160, 214)


def rounded_rect_alpha(x, y, w, h, r):
    """Point-in-rounded-rect via clamped distance."""
    if x < 0 or y < 0 or x > w or y > h:
        return False
    qx = min(max(x, r), w - r)
    qy = min(max(y, r), h - r)
    dx = x - qx
    dy = y - qy
    return dx * dx + dy * dy <= r * r


def render(size):
    n = size * SS
    pad = n * 0.04
    w = n - pad * 2.0
    r = w * 0.24
    cx = n / 2.0
    cy = n / 2.0

    rad = w * 0.29
    pen = w * 0.155
    inner = rad - pen / 2.0
    outer = rad + pen / 2.0
    a0, a1 = 42.0, 318.0

    buf = bytearray(n * n * 4)
    for py in range(n):
        fy = py + 0.5
        for px in range(n):
            fx = px + 0.5
            i = (py * n + px) * 4
            lx = fx - pad
            ly = fy - pad
            if not rounded_rect_alpha(lx, ly, w, w, r):
                continue

            # 背景：左下白 -> 右上蓝（对角线为界）
            u = (lx / w + (w - ly) / w) / 2.0  # 0 at lower-left, 1 at upper-right
            if ly < lx:
                # 蓝色一侧：蓝 -> 青 渐变
                t = max(0.0, min(1.0, (lx / w + (w - ly) / w - 1.0)))
                col = (
                    int(BLUE[0] + (CYAN[0] - BLUE[0]) * t),
                    int(BLUE[1] + (CYAN[1] - BLUE[1]) * t),
                    int(BLUE[2] + (CYAN[2] - BLUE[2]) * t),
                )
                blue_side = True
            else:
                # 白色一侧：左下纯白，靠近对角线略带一点冷色
                k = u
                col = (
                    int(255 - 17 * k),
                    int(255 - 9 * k),
                    255,
                )
                blue_side = False

            # 细描边
            edge = min(lx, ly, w - lx, w - ly)
            if edge < max(0.6, w * 0.006):
                col = BORDER

            # C 字形
            dx = fx - cx
            dy = fy - cy
            dist = math.hypot(dx, dy)
            inside = False
            if inner <= dist <= outer:
                ang = math.degrees(math.atan2(dy, dx))
                if ang < 0:
                    ang += 360.0
                if a0 <= ang <= a1:
                    inside = True
            if not inside:
                for a in (a0, a1):
                    ra = math.radians(a)
                    ex = cx + rad * math.cos(ra)
                    ey = cy + rad * math.sin(ra)
                    if math.hypot(fx - ex, fy - ey) <= pen / 2.0:
                        inside = True
                        break
            if inside:
                col = WHITE if blue_side else BLUE

            buf[i] = col[0]
            buf[i + 1] = col[1]
            buf[i + 2] = col[2]
            buf[i + 3] = 255

    # 4x 超采样盒式降采样
    out = bytearray(size * size * 4)
    area = SS * SS
    for y in range(size):
        for x in range(size):
            rr = gg = bb = aa = 0
            for sy in range(SS):
                row = (y * SS + sy) * n
                for sx in range(SS):
                    i = (row + x * SS + sx) * 4
                    a = buf[i + 3]
                    rr += buf[i] * a
                    gg += buf[i + 1] * a
                    bb += buf[i + 2] * a
                    aa += a
            o = (y * size + x) * 4
            if aa == 0:
                out[o:o + 4] = b"\x00\x00\x00\x00"
            else:
                out[o] = rr // aa
                out[o + 1] = gg // aa
                out[o + 2] = bb // aa
                out[o + 3] = aa // area
    return bytes(out)


def png(size, rgba):
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    raw = bytearray()
    stride = size * 4
    for y in range(size):
        raw.append(0)
        raw.extend(rgba[y * stride:(y + 1) * stride])
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
            + chunk(b"IEND", b""))


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    assets = os.path.join(root, "assets")
    os.makedirs(assets, exist_ok=True)

    images = []
    for s in SIZES:
        images.append((s, png(s, render(s))))
        sys.stdout.write("rendered %d\n" % s)

    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries = b""
    data = b""
    for s, blob in images:
        entries += struct.pack("<BBBBHHII", 0 if s >= 256 else s, 0 if s >= 256 else s, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
        data += blob

    out = os.path.join(assets, "app.ico")
    with open(out, "wb") as f:
        f.write(header + entries + data)
    print("ICO written: %s (%d bytes)" % (out, os.path.getsize(out)))


if __name__ == "__main__":
    main()
