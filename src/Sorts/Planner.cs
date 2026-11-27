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

// ---------------------------------------------------------------- planning where every icon goes
class Move { public int Index, FX, FY, TX, TY; }

class Plan { public List<Move> Moves = new List<Move>(); public int Kept, Sorted; public int[] EX, EY; }   // EX/EY: where every icon must end

class Grid {
    public int Cx, Cy, X0, Y0, Cols, Rows;
    public int Cells { get { return Cols * Rows; } }
    public Grid(List<DIcon> sample, D.Rectangle area) {
        Desktop.Spacing(out Cx, out Cy);
        int ox = Mode(sample.Select(i => Mod(i.X - area.Left, Cx)), 14), oy = Mode(sample.Select(i => Mod(i.Y - area.Top, Cy)), 2);
        X0 = area.Left + ox; Y0 = area.Top + oy;
        Cols = Math.Max(1, (area.Right - X0) / Cx); Rows = Math.Max(1, (area.Bottom - Y0) / Cy);
    }
    static int Mod(int a, int m) { return ((a % m) + m) % m; }
    static int Mode(IEnumerable<int> v, int dflt) { var l = v.ToList(); return l.Count == 0 ? dflt : l.GroupBy(x => x).OrderByDescending(x => x.Count()).First().Key; }
    public int CellOf(int x, int y) {
        int c = (int)Math.Round((x - X0) / (double)Cx), r = (int)Math.Round((y - Y0) / (double)Cy);
        return c >= 0 && c < Cols && r >= 0 && r < Rows ? c * Rows + r : -1;
    }
    public void At(int cell, out int x, out int y) { x = X0 + (cell / Rows) * Cx; y = Y0 + (cell % Rows) * Cy; }
}

static class Planner {
    // full: every item of the sort goes back to its place. Otherwise only items that just appeared go to their saved place.
    // Items that are not in the sort (or whose saved place is on a screen that is gone) flow, in name order, into the free
    // cells of the main screen, column by column; with sortExtras off they stay where they are unless full.
    public static Plan Make(Sort sort, List<DIcon> cur, bool full, HashSet<string> appeared, bool sortExtras) {
        var plan = new Plan();
        if (cur.Count == 0) return plan;
        var saved = new Dictionary<string, DIcon>();
        if (sort != null) foreach (var s in sort.Icons) saved[s.Key] = s;
        var primary = Desktop.ToClient(F.Screen.PrimaryScreen.WorkingArea);
        var screens = F.Screen.AllScreens.Select(sc => Desktop.ToClient(sc.WorkingArea)).ToList();
        var present = Desktop.Keys(cur);
        var sample = saved.Values.Where(s => present.Contains(s.Key) && primary.Contains(s.X, s.Y)).ToList();
        if (sample.Count == 0) sample = cur.Where(c => primary.Contains(c.X, c.Y)).ToList();
        var g = new Grid(sample, primary);

        int[] tx = new int[cur.Count], ty = new int[cur.Count];
        var taken = new HashSet<int>(); var flow = new List<int>();
        for (int i = 0; i < cur.Count; i++) {
            var c = cur[i]; DIcon s;
            tx[i] = c.X; ty[i] = c.Y;
            bool inSort = saved.TryGetValue(c.Key, out s) && screens.Any(r => r.Contains(s.X + g.Cx / 2, s.Y + g.Cy / 2));
            if (inSort) {
                if (full || (appeared != null && appeared.Contains(c.Key))) { tx[i] = s.X; ty[i] = s.Y; }
                taken.Add(g.CellOf(tx[i], ty[i])); plan.Kept++;
            } else if (full || sortExtras) flow.Add(i);
            else taken.Add(g.CellOf(c.X, c.Y));
        }
        taken.Remove(-1);

        var kinds = Kinds();
        flow.Sort((a, b) => {
            int ka = Kind(kinds, cur[a].Name), kb = Kind(kinds, cur[b].Name);
            if (ka != kb) return ka.CompareTo(kb);
            int n = Native.StrCmpLogicalW(cur[a].Name, cur[b].Name);
            return n != 0 ? n : cur[a].N.CompareTo(cur[b].N);
        });
        int cell = 0;
        foreach (int i in flow) {
            while (cell < g.Cells && taken.Contains(cell)) cell++;
            if (cell >= g.Cells) break;                       // the main screen is full: the rest stay where they are
            g.At(cell, out tx[i], out ty[i]); taken.Add(cell); plan.Sorted++;
        }
        for (int i = 0; i < cur.Count; i++)
            if (tx[i] != cur[i].X || ty[i] != cur[i].Y)
                plan.Moves.Add(new Move { Index = i, FX = cur[i].X, FY = cur[i].Y, TX = tx[i], TY = ty[i] });
        plan.EX = tx; plan.EY = ty;
        return plan;
    }

    // name -> 1 folder, 2 file; items found on neither desktop folder (Recycle Bin, This PC) are 0 and come first
    static Dictionary<string, int> Kinds() {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) }) {
            try {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetDirectories(dir)) d[Path.GetFileName(f)] = 1;
                foreach (var f in Directory.GetFiles(dir)) {
                    string a = Path.GetFileName(f), b = Path.GetFileNameWithoutExtension(f);
                    if (!d.ContainsKey(a)) d[a] = 2; if (!d.ContainsKey(b)) d[b] = 2;
                }
            } catch { }
        }
        return d;
    }
    static int Kind(Dictionary<string, int> k, string name) { int v; return k.TryGetValue(name, out v) ? v : 0; }
}
