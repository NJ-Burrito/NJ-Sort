# NJ-Sort brand

**Concept.** A neat 3x3 grid of glossy icon tiles on an indigo tile (the site's #181a3a to #262a5a family), the last
one in amber #FFB02E sliding into its dashed slot: icons snapping back into the saved sort. Light from the top-left,
layered gradients, a specular sheen per tile, soft contact shadows, an amber glow and rim line. No text.

| File | Use |
|---|---|
| `logo.svg` | master, 1024 viewBox, full detail (frames 40 px and up, `assets/nj-sort.png`) |
| `logo-small.svg` | simplified 2x2 variant, no blur, for the 16/20/24/32 px frames |
| `make_icon.py` | renders both SVGs headless (Chromium via Playwright) to `assets/nj-sort.ico` (16, 20, 24, 32, 40, 48, 64, 128, 256)and `assets/nj-sort.png` (1024) |
| `make_sheet.py` | check sheet from the .ico on dark, light, pixel zoom and taskbar/tray strips |

## Regenerate

```
uv run --with pillow --with playwright python brand/make_icon.py
uv run --with pillow python brand/make_sheet.py <folder>/sheet.png
```

`build.cmd` embeds the .ico (`/win32icon`) and the PNG.

## Credits

All art is original. Pillow (HPND) and Playwright (Apache-2.0) are build tools only.
