"""Builds NJ-Sort's marketing media (hero EN/AR, stills, OG cards, banner, logos) into docs/.

Run:  uv run --with pillow --with playwright python media/make_media.py [--rerender] [stage ...]
Stages: frames logos hero banner   (default: all)

Frames come from the product's own off-screen renderers (dist\\NJ-Sort.exe --render-motion and --render), never a
screen recording. The exe is always started with NJSORT_HOME pointing at a scratch folder, so it can never touch
a real NJ-Sort setup. Work files go to NJSORT_MEDIA_WORK (default: media-work/ beside the repo). The Thmanyah font
files are read from NJ_FONTS (a folder you are licensed to use) for the HTML templates only; no font file is copied
into the repo.
"""
import os, subprocess, sys, shutil
from pathlib import Path
from PIL import Image, ImageFilter, ImageDraw

os.environ.setdefault("PYTHONUTF8", "1")
ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "docs"
TPL = ROOT / "media"
WORK = Path(os.environ.get("NJSORT_MEDIA_WORK") or (ROOT.parent / "media-work"))
FONTS = Path(os.environ.get("NJ_FONTS") or (ROOT / "fonts-not-in-repo"))   # set NJ_FONTS to your licensed Thmanyah folder
EXE = ROOT / "dist" / "NJ-Sort.exe"
SRC_FPS = 60       # the renderer's frame rate for the motion (it caps at 60)
OUT_FPS = 30
SLOW = 0.4         # the real motion lasts 0.7 s: shown 2.5x slower so the glide can be seen
LASTSRC = 42       # last motion frame that still changes (checked by hashing the 60 fps frames)
W, H = 1440, 1080
DESK = (60, 150, 1320, 739)   # the fake desktop's place on the hero canvas: x, y, w, h

T = {
    "en": dict(lang="en", dir="ltr", tagline="Keeps your desktop icons in the sort you saved.",
               chip="1.1 &nbsp;·&nbsp; FREE FOR WINDOWS",
               foot="Save your sort. <em>Rearrange</em> glides every icon back.",
               messy="Messy desktop", done="Rearranged", rearr="Rearrange",
               line="Save your desktop sort. <em>Rearrange</em> glides every icon back.",
               chips=["Save sort", "Rearrange", "Undo"]),
    "ar": dict(lang="ar", dir="rtl", tagline="يُبقي أيقونات سطح المكتب على الترتيب الذي حفظته.",
               chip="1.1 &nbsp;·&nbsp; مجاني لويندوز",
               foot="احفظ ترتيبك. و<em>إعادة الترتيب</em> تُعيد كل أيقونة بانسيابية.",
               messy="سطح مكتب مبعثر", done="أُعيد الترتيب", rearr="إعادة الترتيب",
               line="احفظ ترتيب سطح مكتبك. و<em>إعادة الترتيب</em> تُعيد كل أيقونة بانسيابية.",
               chips=["حفظ الترتيب", "إعادة الترتيب", "تراجع"]),
}


def sh(*a, **k):
    k.setdefault("check", True)
    return subprocess.run([str(x) for x in a], **k)


def log(*a):
    print(*a, flush=True)


# ---------------------------------------------------------------- frames (off-screen renders of the product)
def render(args, out, env_extra):
    out.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, NJSORT_HOME=str(WORK / "home"), **env_extra)   # never the real NJ-Sort data
    (WORK / "home").mkdir(parents=True, exist_ok=True)
    p = subprocess.run([str(EXE)] + args + [str(out)], env=env, timeout=300)   # waits for exit
    if p.returncode != 0:
        raise SystemExit(f"NJ-Sort {' '.join(args)} exit {p.returncode}")


def stage_frames(rerender):
    need = [("motion", ["--render-motion"], {"NJSORT_RENDER_FPS": str(SRC_FPS)}),
            ("en", ["--render"], {"NJSORT_LANG": "en"}), ("ar", ["--render"], {"NJSORT_LANG": "ar"})]
    for name, args, env in need:
        d = WORK / name
        if d.exists() and any(d.glob("*.png")) and not rerender:
            continue
        log("render", name)
        render(args, d, env)


def motion(i):
    return Image.open(WORK / "motion" / f"motion-f{int(i):05d}.png").convert("RGB")


# ---------------------------------------------------------------- html
class Shooter:
    def __init__(self):
        from playwright.sync_api import sync_playwright
        self._pw = sync_playwright().start()
        self._b = self._pw.chromium.launch()
        self.fonts = FONTS.as_uri()

    def shot(self, html, out, w, h, transparent=False, clip=None):
        f = WORK / "_page.html"
        f.write_text(html, encoding="utf-8")
        pg = self._b.new_page(viewport={"width": w, "height": h})
        pg.goto(f.as_uri())
        pg.evaluate("document.fonts.ready")
        pg.wait_for_timeout(300)
        pg.screenshot(path=str(out), omit_background=transparent, **({"clip": clip} if clip else {}))
        pg.close()

    def close(self):
        self._b.close()
        self._pw.stop()


def hero_html(s, L, mode, body, ww=804, wh=780):
    t = (TPL / "hero.html").read_text(encoding="utf-8")
    for k, v in (("FONTS", s.fonts), ("LANG", L["lang"]), ("DIR", "ltr" if mode == "win" else L["dir"]), ("MODE", mode), ("BODY", body),
                 ("WW", ww), ("WH", wh)):
        t = t.replace("{{" + k + "}}", str(v))
    return t


def over(base, png, xy=(0, 0), alpha=1.0):
    o = png if isinstance(png, Image.Image) else Image.open(png)
    o = o.convert("RGBA")
    if alpha < 1:
        o.putalpha(o.getchannel("A").point(lambda x: int(x * alpha)))
    b = base.convert("RGBA")
    b.alpha_composite(o, xy)
    return b.convert("RGB")


def ease(t):
    t = max(0.0, min(1.0, t))
    return 1 - (1 - t) ** 3


def rounded_mask(w, h, r):
    m = Image.new("L", (w * 2, h * 2), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, w * 2 - 1, h * 2 - 1), r * 2, fill=255)
    return m.resize((w, h), Image.LANCZOS)


# ---------------------------------------------------------------- hero
def hero_assets(s, lang):
    L = T[lang]
    ov = WORK / f"ov-{lang}"
    ov.mkdir(exist_ok=True)
    logo = (WORK / "logo.png").as_uri()
    s.shot(hero_html(s, L, "title", f'<div class="bg"></div><div class="wrap"><img class="logo" src="{logo}"><h1>NJ-<b>Sort</b></h1>'
                     f'<p>{L["tagline"]}</p><div class="chip">{L["chip"]}</div></div>'), ov / "title.png", W, H)
    s.shot(hero_html(s, L, "stage", f'<div class="bg"></div><div class="head"><img src="{logo}"><b>NJ-<i>Sort</i></b></div>'
                     f'<div class="shadow"></div><div class="foot"><span>{L["foot"]}</span></div>'), ov / "stage.png", W, H)
    s.shot(hero_html(s, L, "ring", '<div class="ring"></div><div class="bar"></div>'), ov / "ring.png", W, H, transparent=True)
    kb = lambda t, on="": f'<kbd class="{on}">{t}</kbd>'
    keys = lambda on: f'<span dir="ltr" style="display:inline-flex;gap:8px;align-items:center">{kb("Ctrl")}+{kb("Alt")}+{kb("R", on)}</span>'
    chips = {"messy": f'<div class="st"><span class="dot" style="opacity:.6"></span>{L["messy"]}</div>',
             "key": f'<div class="st hot">{keys("")}&nbsp;{L["rearr"]}</div>',
             "keyon": f'<div class="st hot">{keys("on")}&nbsp;{L["rearr"]}</div>',
             "ok": f'<div class="st ok"><span class="dot"></span>{L["done"]}</div>'}
    for k, v in chips.items():
        s.shot(hero_html(s, L, "chip", f'<div class="chipbox">{v}</div>'), ov / f"chip-{k}.png", W, H, transparent=True)
    shutil.copy(WORK / lang / "window.png", ov / "window.png")
    wf = (ov / "window.png").as_uri()
    s.shot(hero_html(s, L, "win", f'<div class="winc"><img src="{wf}"></div>'), ov / "win.png", 864, 840, transparent=True,
           clip={"x": 0, "y": 0, "width": 864, "height": 840})
    toast = Image.open(WORK / lang / "toast-sort.png").convert("RGBA")
    tw = 660
    toast = toast.resize((tw, round(toast.height * tw / toast.width)), Image.LANCZOS)
    toast.save(ov / "toast.png")
    return ov


class Hero:
    def __init__(self, lang, ov):
        self.lang, self.ov = lang, ov
        self.stage = Image.open(ov / "stage.png").convert("RGB")
        self.title = Image.open(ov / "title.png").convert("RGB")
        self.ring = Image.open(ov / "ring.png").convert("RGBA")
        self.chips = {k: Image.open(ov / f"chip-{k}.png").convert("RGBA") for k in ("messy", "key", "keyon", "ok")}
        self.win = Image.open(ov / "win.png").convert("RGBA")
        self.toast = Image.open(ov / "toast.png").convert("RGBA")
        self.mask = rounded_mask(DESK[2], DESK[3], 26)
        self._d = {}
        self._dim = None

    def desk(self, pos):
        lo = int(pos)
        k = (lo, round(pos - lo, 2))
        if k not in self._d:
            im = motion(lo)
            f = pos - lo
            if f > 0.01 and lo < LASTSRC:   # a touch of blend between two 60 fps frames reads as motion blur
                im = Image.blend(im, motion(lo + 1), f)
            self._d[k] = im.resize(DESK[2:], Image.LANCZOS)
        return self._d[k]

    def scene(self, pos, chip, toast=0.0):
        b = self.stage.copy()
        b.paste(self.desk(pos), DESK[:2], self.mask)
        b = over(b, self.ring)
        if chip:
            b = over(b, self.chips[chip])
        if toast > 0:
            p = ease(toast)
            x, y = DESK[0] + DESK[2] - self.toast.width - 4, DESK[1] + DESK[3] - self.toast.height + 10 + round(34 * (1 - p))
            b = over(b, self.toast, (x, y), alpha=p)
        return b

    def with_window(self, p):
        if self._dim is None:
            base = self.scene(LASTSRC, None)
            d = base.filter(ImageFilter.GaussianBlur(7))
            self._dim = Image.blend(d, Image.new("RGB", d.size, (8, 9, 24)), 0.45)
        e = ease(p)
        b = Image.blend(self.scene(LASTSRC, None), self._dim, e)
        w = self.win
        sc = 0.93 + 0.07 * e
        w2 = w.resize((round(w.width * sc), round(w.height * sc)), Image.LANCZOS)
        x, y = (W - w2.width) // 2, (H - w2.height) // 2 + round(26 * (1 - e)) - 0
        return over(b, w2, (x, y), alpha=e)

    def frames(self):
        """258 frames (8.6 s at 30 fps); frame 0 is the title card and the last frames dissolve back into it."""
        out = []
        A, B, C, D, E, F = 34, 46, 76, 130, 190, 244   # title end, scene settled, motion start, motion end, window in, window out
        n_total = 258
        n_motion = int((LASTSRC / SLOW) / (SRC_FPS / OUT_FPS)) + 1
        D = C + n_motion
        for i in range(n_total):
            if i < A:
                fr = self.title
            elif i < B:   # title -> messy desktop
                fr = Image.blend(self.title, self.scene(0, "messy"), ease((i - A + 1) / (B - A)))
            elif i < C:
                chip = "messy" if i < 64 else ("key" if i < 70 else "keyon")
                fr = self.scene(0, chip)
            elif i < D:
                pos = min(LASTSRC, (i - C) * SLOW * (SRC_FPS / OUT_FPS))
                tp = (i - (D - 8)) / 12 if i >= D - 8 else 0
                fr = self.scene(pos, "ok" if i >= D - 14 else "keyon", tp)
            elif i < E:
                fr = self.scene(LASTSRC, "ok", (i - (D - 8)) / 12 if i < D + 4 else 1.0)
            elif i < E + 12:   # toast out, window pops in
                fr = self.with_window((i - E + 1) / 12)
            elif i < F:
                fr = self.with_window(1.0)
            else:   # window dissolves into the title card, which is frame 0
                p = ease((i - F + 1) / (n_total - F))
                fr = Image.blend(self.with_window(1.0), self.title, p)
            out.append(fr)
        return out, dict(title=A, settled=B, motion=C, motion_end=D, window=E, close=F)


def encode(seq_dir, dst, mp4_crf=22, webm_crf=32):
    pat = str(seq_dir / "f%04d.png")
    vf = f"scale={W}:{H}:flags=lanczos,format=yuv420p"
    sh("ffmpeg", "-v", "error", "-y", "-framerate", OUT_FPS, "-i", pat, "-vf", vf, "-c:v", "libx264", "-preset", "veryslow",
       "-crf", mp4_crf, "-movflags", "+faststart", "-an", str(dst) + ".mp4")
    sh("ffmpeg", "-v", "error", "-y", "-framerate", OUT_FPS, "-i", pat, "-vf", vf, "-c:v", "libvpx-vp9", "-crf", webm_crf,
       "-b:v", "0", "-row-mt", "1", "-cpu-used", "1", "-deadline", "good", "-an", str(dst) + ".webm")


def make_gif(seq_dir, dst, w=600, fps=15, colors=128):
    h = w * 3 // 4
    pal = WORK / "pal.png"
    pat = str(seq_dir / "f%04d.png")
    sh("ffmpeg", "-v", "error", "-y", "-framerate", OUT_FPS, "-i", pat,
       "-vf", f"fps={fps},scale={w}:{h}:flags=lanczos,palettegen=max_colors={colors}:stats_mode=diff", str(pal))
    sh("ffmpeg", "-v", "error", "-y", "-framerate", OUT_FPS, "-i", pat, "-i", str(pal), "-lavfi",
       f"fps={fps},scale={w}:{h}:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle",
       "-loop", "0", str(dst))


def stage_hero(s):
    for lang, name in (("en", "hero"), ("ar", "hero-ar")):
        ov = hero_assets(s, lang)
        h = Hero(lang, ov)
        seq, marks = h.frames()
        sd = WORK / f"seq-{lang}"
        sd.mkdir(exist_ok=True)
        for f in sd.glob("*.png"):
            f.unlink()
        for i, fr in enumerate(seq):
            fr.save(sd / f"f{i:04d}.png", compress_level=1)
        log(lang, "frames", len(seq), f"{len(seq) / OUT_FPS:.2f}s", marks)
        encode(sd, DOCS / name)
        make_gif(sd, DOCS / f"{name}.gif")
        # poster: the glide in mid-air; still (reduced motion): everything tidy with the toast up
        k = marks["motion"] + 20
        seq[k].save(DOCS / f"{name}-poster.jpg", quality=88, optimize=True)
        h.scene(LASTSRC, "ok", 1.0).save(DOCS / f"{name}-still.jpg", quality=88, optimize=True)
        for p in sorted(DOCS.glob(f"{name}*")):
            log("  ", p.name, p.stat().st_size)
        # contact sheet (not committed): every 12th frame
        sheet_dir = WORK / "sheets"
        sheet_dir.mkdir(exist_ok=True)
        ids = list(range(0, len(seq), 12)) + [len(seq) - 1]
        tw, th = 360, 270
        cols = 6
        rows = (len(ids) + cols - 1) // cols
        sheet = Image.new("RGB", (cols * tw, rows * th), (0, 0, 0))
        for n, i in enumerate(ids):
            sheet.paste(seq[i].resize((tw, th), Image.LANCZOS), ((n % cols) * tw, (n // cols) * th))
        sheet.save(sheet_dir / f"{name}-sheet.png")


# ---------------------------------------------------------------- banner / OG
def stage_banner(s):
    shutil.copy(WORK / "logo.png", WORK / "logo-b.png")
    desk = WORK / "desk-tidy.png"
    motion(LASTSRC).crop((0, 0, 680, 560)).save(desk)
    t = (TPL / "banner.html").read_text(encoding="utf-8")
    for lang, og in (("en", "og-card"), ("ar", "og-card-ar")):
        L = T[lang]
        rtl = lang == "ar"
        pos = dict(COPYSIDE="right" if rtl else "left", GLOWX=340 if rtl else 940, GLOWX2=300 if rtl else 980,
                   DX=1280 - 600 - 560 if rtl else 600, WX=1280 - 880 - 380 if rtl else 880, TX=1280 - 585 - 430 if rtl else 585,
                   R1=3 if rtl else -3, R2=-3 if rtl else 3, R3=2 if rtl else -2)
        # the window card is only shown on the near side: keep it clear of the desktop's corner
        chips = "".join(f"<span>{c}</span>" for c in L["chips"])
        h = t
        for k, v in dict(FONTS=s.fonts, LANG=L["lang"], DIR=L["dir"], LOGO=(WORK / "logo-b.png").as_uri(),
                         LINE=L["line"], CHIPS=chips, DESK=desk.as_uri(), WIN=(WORK / lang / "window.png").as_uri(),
                         TOAST=(WORK / lang / "toast-sort.png").as_uri(), **pos).items():
            h = h.replace("{{" + k + "}}", str(v))
        h = h.replace("NJ-<b>Sort</b>", "NJ-<b>Sort</b>")
        sizes = [("banner" if lang == "en" else None, 1280, 640), (og, 1200, 630)]
        for name, Wd, Ht in sizes:
            if not name:
                continue
            sc = Wd / 1280
            top = (Ht - 640 * sc) / 2
            page = h.replace("{{W}}", str(Wd)).replace("{{H}}", str(Ht)).replace("{{S}}", f"{sc:.5f}").replace("{{TOP}}", f"{top:.1f}")
            if name == "banner":
                s.shot(page, DOCS / "banner.png", Wd, Ht)
            else:
                tmp = WORK / f"{name}.png"
                s.shot(page, tmp, Wd, Ht)
                Image.open(tmp).convert("RGB").save(DOCS / f"{name}.jpg", quality=90, optimize=True)
            log(name, Wd, Ht)


def stage_logos():
    WORK.mkdir(parents=True, exist_ok=True)
    im = Image.open(ROOT / "assets" / "nj-sort.png").convert("RGBA")
    im.save(DOCS / "logo-1024.png", optimize=True)
    im.resize((256, 256), Image.LANCZOS).save(DOCS / "logo.png", optimize=True)
    im.resize((512, 512), Image.LANCZOS).save(WORK / "logo.png")


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    stages = args or ["frames", "logos", "hero", "banner"]
    WORK.mkdir(parents=True, exist_ok=True)
    if "frames" in stages:
        stage_frames("--rerender" in sys.argv)
    stage_logos()
    if any(x in stages for x in ("hero", "banner")):
        s = Shooter()
        try:
            if "hero" in stages:
                stage_hero(s)
            if "banner" in stages:
                stage_banner(s)
        finally:
            s.close()


if __name__ == "__main__":
    main()
