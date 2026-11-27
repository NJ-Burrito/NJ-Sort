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

// ---------------------------------------------------------------- README images (sample data, nothing read from this PC's desktop)
static class Render {
    public static int Run(string outDir) {
        try {
            Directory.CreateDirectory(outDir);
            Core.Cfg = new Settings();
            var now = DateTime.Now;
            Func<string, DateTime, int, Sort> mk = (name, t, n) => { var s = new Sort { Id = t.ToString("yyyyMMdd-HHmmss"), Name = name, Saved = t };
                for (int i = 0; i < n; i++) s.Icons.Add(new DIcon { Name = "item " + i, N = 1 }); return s; };
            Store.All = new List<Sort> {
                mk("Work week", now.AddMinutes(-12), 24), mk("", now.Date.AddDays(-1).AddHours(18).AddMinutes(40), 22),
                mk("Clean desktop", now.Date.AddDays(-3).AddHours(9).AddMinutes(5), 12), mk("Before the big tidy-up", now.Date.AddDays(-9).AddHours(21).AddMinutes(17), 41) };
            Core.Cfg.Active = Store.All[0].Id;
            var mw = new MainWin(); mw.Refresh();
            var w = mw.win; w.WindowStartupLocation = W.WindowStartupLocation.Manual; w.Left = -20000; w.ShowActivated = false; w.Show(); w.UpdateLayout();
            Save(w, Path.Combine(outDir, "window.png"));
            Toast.RenderPose("sort", "Rearranged", "22 in their saved place, 2 more sorted by name after them."); Save(Toast.win, Path.Combine(outDir, "toast-sort.png"));
            Toast.RenderPose("save", "Sort saved", "24 items. It is now the active sort."); Save(Toast.win, Path.Combine(outDir, "toast-save.png"));
            return 0;
        } catch (Exception e) { File.WriteAllText(Path.Combine(outDir, "render.err"), e.ToString()); return 1; }
    }
    // renders a whole window's client area (background, shadows) the same size it has on screen, at 2x
    static void Save(W.Window w, string path) {
        w.UpdateLayout();
        var c = (W.FrameworkElement)w.Content; var m = c.Margin; const double scale = 2;
        double wd = c.ActualWidth + m.Left + m.Right, ht = c.ActualHeight + m.Top + m.Bottom;
        var bmp = new WM.Imaging.RenderTargetBitmap((int)Math.Ceiling(wd * scale), (int)Math.Ceiling(ht * scale), 96 * scale, 96 * scale, WM.PixelFormats.Pbgra32);
        bmp.Render(w);
        var enc = new WM.Imaging.PngBitmapEncoder(); enc.Frames.Add(WM.Imaging.BitmapFrame.Create(bmp));
        using (var fs = File.Create(path)) enc.Save(fs);
    }}
