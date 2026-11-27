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

// ---------------------------------------------------------------- icons glide into place
static class Animator {
    public static bool Busy;
    public static void Run(Plan plan, bool animate, Action done) {
        Busy = true;
        var moves = plan.Moves;
        var mover = new Desktop.Mover();
        Action finish = () => { Settle(plan, mover); mover.Dispose(); Busy = false; done(); };
        if (!mover.Ok) { finish(); return; }
        if (!animate || moves.Count == 0) { foreach (var m in moves) mover.Move(m.Index, m.TX, m.TY); finish(); return; }

        int count0 = Desktop.Count();
        // cascade in reading order of the destination: column by column, top to bottom
        var order = moves.OrderBy(m => m.TX).ThenBy(m => m.TY).ToList();
        double step = Math.Min(16.0, 260.0 / order.Count), dur = 460;
        var lastX = order.Select(m => m.FX).ToArray(); var lastY = order.Select(m => m.FY).ToArray();
        var clock = Stopwatch.StartNew();
        var timer = new WT.DispatcherTimer(WT.DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(12) };
        timer.Tick += delegate {
            if (!Desktop.Valid || Desktop.Count() != count0) { timer.Stop(); App.Log("animation stopped: the desktop changed"); plan.EX = null; finish(); return; }
            double t = clock.Elapsed.TotalMilliseconds; bool all = true;
            for (int k = 0; k < order.Count; k++) {
                double p = (t - k * step) / dur;
                if (p < 0) { all = false; continue; }
                if (p < 1) all = false; else p = 1;
                double e = 1 - Math.Pow(1 - p, 3);   // ease-out cubic: quick start, soft landing
                var m = order[k];
                int x = (int)Math.Round(m.FX + (m.TX - m.FX) * e), y = (int)Math.Round(m.FY + (m.TY - m.FY) * e);
                if (x != lastX[k] || y != lastY[k]) { mover.Move(m.Index, x, y); lastX[k] = x; lastY[k] = y; }
            }
            if (all) { timer.Stop(); foreach (var m in order) mover.Move(m.Index, m.TX, m.TY); finish(); }
        };
        timer.Start();
    }

    // With "Align icons to grid" on, an icon landing on a cell before its occupant has left pushes a neighbour aside.
    // So at the end every icon is checked against where it must be, and any pushed one is put back.
    static void Settle(Plan plan, Desktop.Mover mover) {
        if (plan.EX == null || !mover.Ok) return;
        int fixedTotal = 0;
        for (int pass = 0; pass < 3; pass++) {
            var now = Desktop.Read();
            if (now.Count != plan.EX.Length) return;
            int f = 0;
            for (int i = 0; i < now.Count; i++)
                if (now[i].X != plan.EX[i] || now[i].Y != plan.EY[i]) { mover.Move(i, plan.EX[i], plan.EY[i]); f++; }
            fixedTotal += f;
            if (f == 0) break;
        }
        if (fixedTotal > 0) App.Log("settled " + fixedTotal + " pushed icon(s) back");
    }
}
