using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using W = System.Windows;
using WC = System.Windows.Controls;
using WM = System.Windows.Media;
using WA = System.Windows.Media.Animation;
using WI = System.Windows.Input;
using WT = System.Windows.Threading;
using WIn = System.Windows.Interop;
using F = System.Windows.Forms;
using D = System.Drawing;

// NJ-Sort: the icon animation drawn on a fake desktop, off screen (NJ-Sort.exe --render-motion <folder>). It plays the exact motion
// Animator plays on the real desktop (Animator.Order / Step / Pose are shared, nothing is copied) on a made-up desktop of
// labelled tiles, so the motion can be designed without moving anyone's icons. Writes motion-1..9.png, motion-sheet.png (3x3),
// motion-paths.png (the arcs), motion-check.txt (the numbers); NJSORT_RENDER_FPS=15 also writes motion-f00000.png ... one per frame.
static class RenderMotion {
    const double CW = 1000, CH = 560;      // the fake desktop
    const double CellW = 96, CellH = 104;  // one grid cell
    static readonly string[] Names = { "Documents", "Downloads", "Invoices", "Photos", "Budget 2026", "Notes", "Recycle Bin", "Chrome", "Mail",
        "Projects", "Music", "Backup", "Scans", "Resume", "Travel", "Receipts", "Slides", "Spotify", "Terminal", "Contracts", "Screenshots",
        "Videos", "Archive", "Setup", "Todo", "Reports", "Sketches", "Readme" };

    sealed class Tile { public W.FrameworkElement El; public Move M; }

    static WM.Color Col(string hex) { return (WM.Color)WM.ColorConverter.ConvertFromString(hex); }
    static WM.Brush Brush(string hex) { return new WM.SolidColorBrush(Col(hex)); }
    static WM.Brush Grad(string a, string b) { return new WM.LinearGradientBrush(Col(a), Col(b), 90); }
    static void Put(WC.Canvas c, W.UIElement e, double x, double y) { WC.Canvas.SetLeft(e, x); WC.Canvas.SetTop(e, y); c.Children.Add(e); }

    static W.FrameworkElement MakeTile(int i) {
        var g = new WC.Grid { Width = CellW, Height = CellH };
        var art = new WC.Canvas { Width = 56, Height = 56, HorizontalAlignment = W.HorizontalAlignment.Center, VerticalAlignment = W.VerticalAlignment.Top, Margin = new W.Thickness(0, 10, 0, 0) };
        int kind = i % 3;
        string[] hues = { "#FFD36B|#F2A33A", "#7FC8FF|#3A86E0", "#C9A6FF|#7E57E0", "#8CE3B0|#32B27A", "#FF9DB0|#E0536F", "#FFB27A|#E8702E" };
        var h = hues[(i * 5 + kind) % hues.Length].Split('|');
        if (kind == 0) {          // a folder
            Put(art, new W.Shapes.Rectangle { Width = 26, Height = 14, RadiusX = 4, RadiusY = 4, Fill = Brush(h[1]) }, 3, 8);
            Put(art, new W.Shapes.Rectangle { Width = 54, Height = 38, RadiusX = 7, RadiusY = 7, Fill = Grad(h[0], h[1]) }, 1, 15);
        } else if (kind == 1) {   // a document
            Put(art, new W.Shapes.Rectangle { Width = 40, Height = 52, RadiusX = 5, RadiusY = 5, Fill = Brush("#F4F6FF"), Stroke = Brush("#C9CEEA"), StrokeThickness = 1 }, 8, 2);
            for (int k = 0; k < 4; k++) Put(art, new W.Shapes.Rectangle { Width = k == 3 ? 16 : 26, Height = 3, RadiusX = 1.5, RadiusY = 1.5, Fill = Brush(h[1]) }, 15, 14 + k * 8);
        } else {                  // an app
            Put(art, new W.Shapes.Rectangle { Width = 52, Height = 52, RadiusX = 14, RadiusY = 14, Fill = Grad(h[0], h[1]) }, 2, 2);
            Put(art, new W.Shapes.Ellipse { Width = 22, Height = 22, Fill = Brush("#FFFFFF"), Opacity = 0.85 }, 17, 17);
        }
        art.Effect = new WM.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 3, Opacity = 0.45, Color = WM.Colors.Black };
        g.Children.Add(art);
        g.Children.Add(new WC.TextBlock { Text = Names[i % Names.Length], Foreground = WM.Brushes.White, FontFamily = new WM.FontFamily("Thmanyah Sans, Segoe UI"), FontSize = 12.5,
            TextAlignment = W.TextAlignment.Center, TextTrimming = W.TextTrimming.CharacterEllipsis, MaxWidth = 88, HorizontalAlignment = W.HorizontalAlignment.Center,
            VerticalAlignment = W.VerticalAlignment.Top, Margin = new W.Thickness(0, 72, 0, 0),
            Effect = new WM.Effects.DropShadowEffect { BlurRadius = 3, ShadowDepth = 1, Opacity = 0.9, Color = WM.Colors.Black } });
        return g;
    }

    static WM.Imaging.BitmapSource Shot(W.FrameworkElement fe) {
        fe.UpdateLayout();
        var bmp = new WM.Imaging.RenderTargetBitmap((int)CW, (int)CH, 96, 96, WM.PixelFormats.Pbgra32);
        bmp.Render(fe); bmp.Freeze(); return bmp;
    }
    static void SavePng(WM.Imaging.BitmapSource b, string file) {
        var enc = new WM.Imaging.PngBitmapEncoder(); enc.Frames.Add(WM.Imaging.BitmapFrame.Create(b));
        using (var f = File.Create(file)) enc.Save(f);
    }

    public static int Run(string outDir) {
        try {
            Directory.CreateDirectory(outDir);
            int fps; if (!int.TryParse(Environment.GetEnvironmentVariable("NJSORT_RENDER_FPS"), out fps) || fps < 1 || fps > 60) fps = 0;

            // the made-up desktop: 28 items in a scrambled layout, going to a tidy column-by-column grid
            var rnd = new Random(11);
            int n = 28, rows = (int)((CH - 24) / CellH);
            var moves = new List<Move>();
            for (int i = 0; i < n; i++)
                moves.Add(new Move { Index = i, TX = 20 + (i / rows) * (int)CellW, TY = 12 + (i % rows) * (int)CellH,
                    FX = rnd.Next(10, (int)(CW - CellW - 10)), FY = rnd.Next(8, (int)(CH - CellH - 8)) });
            var win = new W.Window { WindowStyle = W.WindowStyle.None, ResizeMode = W.ResizeMode.NoResize, ShowInTaskbar = false, ShowActivated = false,
                WindowStartupLocation = W.WindowStartupLocation.Manual, Left = -32000, Top = 0, SizeToContent = W.SizeToContent.WidthAndHeight };
            var bg = new WM.LinearGradientBrush(Col("#1B1E4A"), Col("#3C8FA0"), 35);
            bg.GradientStops.Insert(1, new WM.GradientStop(Col("#2E4E86"), 0.6));
            var root = new WC.Canvas { Width = CW, Height = CH, ClipToBounds = true, Background = bg };
            win.Content = root; win.Show(); win.UpdateLayout();

            var order = Animator.Order(moves);                   // the very same order, step and pose Animator uses
            double step = Animator.Step(order.Count), total = Animator.Total(order.Count);
            var tiles = new List<Tile>();
            foreach (var m in order) { var t = new Tile { El = MakeTile(m.Index), M = m }; tiles.Add(t); root.Children.Add(t.El); }
            Action<double> at = delegate(double ms) {            // the desktop at ms after the start
                for (int k = 0; k < tiles.Count; k++) {
                    double p = (ms - k * step) / Animator.Dur; var m = tiles[k].M; int x, y;
                    Animator.Pose(m.FX, m.FY, m.TX, m.TY, p < 0 ? 0 : p, out x, out y);
                    WC.Canvas.SetLeft(tiles[k].El, x); WC.Canvas.SetTop(tiles[k].El, y);
                }
            };

            // the check: the last frame is the target, the overshoot is small, no jump between two 12 ms ticks is wild
            var log = new List<string>(); int off = 0; double maxOver = 0, maxJump = 0, maxLift = 0;
            for (int k = 0; k < order.Count; k++) {
                var m = order[k]; int px = m.FX, py = m.FY; double d = Math.Sqrt((double)(m.TX - m.FX) * (m.TX - m.FX) + (double)(m.TY - m.FY) * (m.TY - m.FY));
                for (double t = k * step; t <= k * step + Animator.Dur + 24; t += 12) {
                    int x, y; Animator.Pose(m.FX, m.FY, m.TX, m.TY, (t - k * step) / Animator.Dur, out x, out y);
                    double j = Math.Sqrt((double)(x - px) * (x - px) + (double)(y - py) * (y - py)); if (j > maxJump) maxJump = j; px = x; py = y;
                    double along = ((x - m.TX) * (m.TX - m.FX) + (y - m.TY) * (m.TY - m.FY)) / Math.Max(1, d);
                    if (along > maxOver) maxOver = along;
                    if (t < k * step + Animator.Wind * Animator.Dur) { double back = -(((x - m.FX) * (m.TX - m.FX) + (y - m.FY) * (m.TY - m.FY)) / Math.Max(1, d)); if (back > maxLift) maxLift = back; }
                }
                if (px != m.TX || py != m.TY) off++;
            }
            log.Add("icons " + order.Count + "  step " + step.ToString("0.0") + " ms  per icon " + Animator.Dur + " ms  total " + total.ToString("0") + " ms");
            log.Add("icons not on target at the last frame: " + off);
            log.Add("max overshoot past the target: " + maxOver.ToString("0.0") + " px   max pull-back in the wind-up: " + maxLift.ToString("0.0") + " px   max step in one 12 ms tick: " + maxJump.ToString("0.0") + " px");
            File.WriteAllLines(Path.Combine(outDir, "motion-check.txt"), log.ToArray());

            // 9 frames across the motion; the 9th is the finished desktop
            double[] marks = { 0, 0.08, 0.18, 0.30, 0.44, 0.58, 0.72, 0.86, 1.0 };
            var shots = new List<WM.Imaging.BitmapSource>();
            for (int i = 0; i < marks.Length; i++) { at(total * marks[i]); var b = Shot(root); shots.Add(b); SavePng(b, Path.Combine(outDir, "motion-" + (i + 1) + ".png")); }
            int w = shots[0].PixelWidth, h = shots[0].PixelHeight;
            var dv = new WM.DrawingVisual();
            using (var dc = dv.RenderOpen()) for (int i = 0; i < shots.Count; i++) dc.DrawImage(shots[i], new W.Rect((i % 3) * w, (i / 3) * h, w, h));
            var sheet = new WM.Imaging.RenderTargetBitmap(w * 3, h * 3, 96, 96, WM.PixelFormats.Pbgra32); sheet.Render(dv);
            SavePng(sheet, Path.Combine(outDir, "motion-sheet.png"));

            // the arcs: every icon's whole path (sampled finely) over the first frame
            at(0);
            var paths = new List<W.UIElement>();
            foreach (var t in tiles) {
                var pl = new W.Shapes.Polyline { Stroke = Brush("#FFE9A8"), StrokeThickness = 1.5, Opacity = 0.8 };
                var m = t.M;
                for (double p = 0; p <= 1.0001; p += 0.002) { int x, y; Animator.Pose(m.FX, m.FY, m.TX, m.TY, p, out x, out y); pl.Points.Add(new W.Point(x + CellW / 2, y + 38)); }
                var dotA = new W.Shapes.Ellipse { Width = 7, Height = 7, Fill = Brush("#FF8A8A") };
                var dotB = new W.Shapes.Ellipse { Width = 7, Height = 7, Fill = Brush("#8CFFB4") };
                root.Children.Add(pl); Put(root, dotA, m.FX + CellW / 2 - 3.5, m.FY + 38 - 3.5); Put(root, dotB, m.TX + CellW / 2 - 3.5, m.TY + 38 - 3.5);
                paths.Add(pl); paths.Add(dotA); paths.Add(dotB);
            }
            SavePng(Shot(root), Path.Combine(outDir, "motion-paths.png"));
            foreach (var e in paths) root.Children.Remove(e);

            if (fps > 0) {   // a frame sequence for video: one file per frame time
                int count = (int)Math.Ceiling((total + 400) * fps / 1000.0);
                for (int idx = 0; idx <= count; idx++) { at(idx * 1000.0 / fps); SavePng(Shot(root), Path.Combine(outDir, "motion-f" + idx.ToString("00000") + ".png")); }
            }
            win.Close();
            return off == 0 ? 0 : 1;
        } catch (Exception e) { File.WriteAllText(Path.Combine(outDir, "render.err"), e.ToString()); return 1; }
    }
}
