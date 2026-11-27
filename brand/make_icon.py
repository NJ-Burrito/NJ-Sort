"""Builds the NJ-Sort icon set from the vector masters in brand/.

  logo.svg        full mark (40 px and up)
  logo-small.svg  simplified, bolder variant (16, 20, 24, 32 px)

assets/nj-sort.png (1024 px), assets/nj-sort.ico (9 frames).
Run:  uv run --with pillow --with playwright python brand/make_icon.py
Needs Chromium from PLAYWRIGHT_BROWSERS_PATH (headless render of the SVG). See brand/README.md.
"""
import io
from pathlib import Path
from PIL import Image
from playwright.sync_api import sync_playwright

here = Path(__file__).resolve().parent
root = here.parent
SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
SMALL_MAX = 32


def render(page, svg_text, size):
    body = svg_text.split("?>", 1)[-1].replace('width="1024" height="1024"', f'width="{size}" height="{size}"', 1)
    page.set_viewport_size({"width": size, "height": size})
    page.set_content(f'<html><body style="margin:0;background:transparent">{body}</body></html>')
    return Image.open(io.BytesIO(page.screenshot(omit_background=True))).convert("RGBA")


def render_all():
    full = (here / "logo.svg").read_text(encoding="utf-8")
    small = (here / "logo-small.svg").read_text(encoding="utf-8")
    out = {}
    with sync_playwright() as p:
        b = p.chromium.launch()
        pg = b.new_page()
        for s in SIZES:
            out[s] = render(pg, small if s <= SMALL_MAX else full, s)
        out[1024] = render(pg, full, 1024)
        b.close()
    return out


if __name__ == "__main__":
    f = render_all()
    f[1024].save(root / "assets" / "nj-sort.png")  # same 1024 px size as before
    # Pillow writes each requested size from append_images only when passed explicitly
    f[256].save(root / "assets" / "nj-sort.ico", format="ICO", sizes=[(s, s) for s in SIZES], append_images=[f[s] for s in SIZES if s != 256])
    print("ok", sorted(Image.open(root / "assets" / "nj-sort.ico").info.get("sizes", [])))
