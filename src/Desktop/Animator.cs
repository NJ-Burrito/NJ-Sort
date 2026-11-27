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
// The motion of one icon is a pure function of time (Pose), shared with the off-screen renderer (RenderMotion), so the
// render shows exactly what the desktop gets. Per icon, over Dur ms:
//   1. anticipation (first Wind of the time): a tiny lift, up and a touch back against the direction of travel;
//   2. the move: along a gentle arc (bowing to the right of the travel, a clockwise swirl), the lift fading out as it goes;
//   3. follow-through: a small overshoot past the target (at most ~14 px) that settles back; the last frame is exactly the target.
static class Animator {
    public static bool Busy;
    public const double Dur = 460, Wind = 0.14;

    // the cascade: reading order of the destination (column by column, top to bottom), one icon every Step ms
    public static List<Move> Order(List<Move> moves) { return moves.OrderBy(m => m.TX).ThenBy(m => m.TY).ToList(); }
    public static double Step(int n) { return Math.Min(16.0, 260.0 / Math.Max(1, n)); }
    public static double Total(int n) { return n == 0 ? 0 : Dur + (n - 1) * Step(n); }

    // where an icon is at progress p (0 = start, 1 = done); p >= 1 is exactly the target
    public static void Pose(int fx, int fy, int tx, int ty, double p, out int x, out int y) {
        if (p >= 1) { x = tx; y = ty; return; }
        if (p <= 0) { x = fx; y = fy; return; }
        double dx = tx - fx, dy = ty - fy, d = Math.Sqrt(dx * dx + dy * dy);
        if (d < 1) { x = tx; y = ty; return; }
        double ux = dx / d, uy = dy / d, nx = uy, ny = -ux;                  // direction of travel, and its right-hand side
        double back = Math.Min(4.0, 0.05 * d), lift = Math.Min(3.0, 0.04 * d);
        double px = -ux * back, py = -uy * back - lift;                      // the wind-up pose
        double s, pull;
        if (p < Wind) { s = 0; pull = Math.Sin(p / Wind * Math.PI / 2); }
        else {
            double r = (p - Wind) / (1 - Wind), q = 1 - r;
            double f = Math.Min(0.05, 14.0 / d), c1 = f / 0.0585;            // overshoot: 5% of the way, never more than ~14 px
            s = 1 + (c1 + 1) * q * q * q * -1 + c1 * q * q;                  // ease-out-back
            pull = q * q * q;
        }
        double arc = Math.Min(0.10 * d, 36.0) * Math.Sin(Math.PI * Math.Max(0, Math.Min(1, s)));
        x = (int)Math.Round(fx + dx * s + nx * arc + px * pull);
        y = (int)Math.Round(fy + dy * s + ny * arc + py * pull);
    }

    public static void Run(Plan plan, bool animate, Action done) {
        Busy = true;
        var moves = plan.Moves;
        var mover = new Desktop.Mover();
        Action finish = () => { Settle(plan, mover); mover.Dispose(); Busy = false; done(); };
        if (!mover.Ok) { finish(); return; }
        if (!animate || moves.Count == 0) { foreach (var m in moves) mover.Move(m.Index, m.TX, m.TY); finish(); return; }

        int count0 = Desktop.Count();
        var order = Order(moves);
        double step = Step(order.Count);
        var lastX = order.Select(m => m.FX).ToArray(); var lastY = order.Select(m => m.FY).ToArray();
        var clock = Stopwatch.StartNew();
        var timer = new WT.DispatcherTimer(WT.DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(12) };
        timer.Tick += delegate {
            if (!Desktop.Valid || Desktop.Count() != count0) { timer.Stop(); App.Log("animation stopped: the desktop changed"); plan.EX = null; finish(); return; }
            double t = clock.Elapsed.TotalMilliseconds; bool all = true;
            for (int k = 0; k < order.Count; k++) {
                double p = (t - k * step) / Dur;
                if (p < 0) { all = false; continue; }
                if (p < 1) all = false; else p = 1;
                var m = order[k]; int x, y;
                Pose(m.FX, m.FY, m.TX, m.TY, p, out x, out y);
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
