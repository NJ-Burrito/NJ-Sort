"""Check sheet: the icon frames read back from NJ-Sort.ico (what Windows will show) plus nj-sort.png,
at 16/24/32/48/256/1024 on dark, light and a Windows 11 taskbar-like strip.
Run: uv run --with pillow python brand/make_sheet.py <out.png>"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent.parent
out = Path(sys.argv[1])
ico = Image.open(root / "assets" / "nj-sort.ico")
def frame(s):
    if s == 1024:
        return Image.open(root / "assets" / "nj-sort.png").convert("RGBA")
    ico.size = (s, s)
    return ico.convert("RGBA")
sizes = [16, 24, 32, 48, 256, 1024]
W = 1700
def band(bg, h=340):
    im = Image.new("RGBA", (W, h), bg)
    x = 40
    for s in sizes:
        f = frame(s)
        if s == 1024:
            f = f.resize((300, 300), Image.LANCZOS)  # shown scaled
            im.alpha_composite(f, (x, (h - 300) // 2))
            x += 330
        else:
            y = h - 40 - s if s < 256 else (h - s) // 2
            y = (h - s) // 2
            im.alpha_composite(f, (x, y)); x += s + 40
    return im
def zoom_band(bg):
    im = Image.new("RGBA", (W, 260), bg)
    x = 40
    for s in (16, 24, 32):
        f = frame(s).resize((s * 6, s * 6), Image.NEAREST)  # pixel zoom
        im.alpha_composite(f, (x, (260 - s * 6) // 2)); x += s * 6 + 40
    return im
def taskbar(bg, fg_hi, w=W, h=96):
    im = Image.new("RGBA", (w, h), bg)
    d = ImageDraw.Draw(im)
    x = w // 2 - 260
    for i, s in enumerate((32, 32, 32, 32)):
        if i == 1:
            d.rounded_rectangle((x - 10, h // 2 - 26, x + 42, h // 2 + 26), 8, fill=fg_hi)
            im.alpha_composite(frame(32), (x + 0, h // 2 - 16))
        else:
            d.rounded_rectangle((x + 2, h // 2 - 14, x + 30, h // 2 + 14), 6, fill=(128, 128, 128, 150))
        x += 64
    # tray: 16 px icon among dots
    tx = w - 300
    for j in range(3):
        d.ellipse((tx + j * 36, h // 2 - 7, tx + j * 36 + 14, h // 2 + 7), fill=(160, 160, 160, 255))
    im.alpha_composite(frame(16), (tx + 3 * 36, h // 2 - 8))
    im.alpha_composite(frame(24), (tx + 3 * 36 + 50, h // 2 - 12))
    return im
parts = [band((15, 23, 35, 255)), band((248, 250, 252, 255)),
         zoom_band((15, 23, 35, 255)), zoom_band((248, 250, 252, 255)),
         taskbar((32, 32, 32, 255), (70, 70, 70, 255)), taskbar((243, 243, 243, 255), (255, 255, 255, 255))]
sheet = Image.new("RGBA", (W, sum(p.height for p in parts)))
y = 0
for p in parts:
    sheet.paste(p, (0, y)); y += p.height
sheet.convert("RGB").save(out)
print("wrote", out)
