// NJ-Sort: keeps the desktop icons in the sort you saved.
//   Runs in the hidden icons (notification area). Its shortcuts work only while it runs; Quit stops them.
//   Save sort now      saves every icon position as a new sort in the history (newest on top) and makes it the active sort.
//   Rearrange          puts every item of the active sort back in its place; items that are not in the sort follow it on the
//                      main screen, sorted by name (folders first), column by column like Windows "Sort by name".
//   Undo               moves the icons back to where they were before the last rearrange.
//   History            any saved sort can be made active again, renamed, or deleted (to the Recycle Bin).
// Saved sorts are never changed by the program itself: a desktop that empties or reloads (Explorer restart by NJ-Flush,
// updates, sign-in, screen changes) is waited out until its item count is steady, then the active sort is put back.
// Only the desktop list view is ever touched. NJ-Sort installs itself in %LOCALAPPDATA%\NJ-Sort (or the folder in the NJSORT_HOME
// environment variable) and keeps its data there: sorts\, settings.ini, undo.tsv, nj-sort.log.
// CLI: NJ-Sort.exe --tray (start in the hidden icons, used at sign-in) | list | --selftest | --render <folder> (README images)
// MIT licence, Nader Alzghoul (NJ-Burrito). https://github.com/NJ-Burrito/NJ-Sort
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

[assembly: AssemblyTitle("NJ-Sort")]
[assembly: AssemblyProduct("NJ-Sort")]
[assembly: AssemblyCompany("NJ-Burrito")]
[assembly: AssemblyDescription("Keeps your desktop icons in the sort you saved.")]
[assembly: AssemblyCopyright("MIT licence, Nader Alzghoul (NJ-Burrito)")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

static class Native {
    public delegate void WinEventProc(IntPtr h, uint ev, IntPtr hwnd, int idObj, int idChild, uint th, uint t);
    [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint a, uint b, IntPtr m, WinEventProc p, uint pid, uint tid, uint f);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string w);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string w);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string s);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint a, bool i, uint pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] public static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, UIntPtr s, uint t, uint pr);
    [DllImport("kernel32.dll")] public static extern bool VirtualFreeEx(IntPtr p, IntPtr a, UIntPtr s, uint t);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr p, IntPtr a, byte[] b, IntPtr s, out IntPtr r);
    [DllImport("kernel32.dll")] public static extern bool WriteProcessMemory(IntPtr p, IntPtr a, byte[] b, IntPtr s, out IntPtr r);
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)] public static extern int StrCmpLogicalW(string a, string b);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
}

// ---------------------------------------------------------------- desktop list view
public class DIcon {
    public string Name; public int N, X, Y;
    public string Key { get { return Name + "\t" + N; } }
}

static class Desktop {
    const int LVM_GETITEMCOUNT = 0x1004, LVM_GETITEMPOSITION = 0x1010, LVM_SETITEMPOSITION32 = 0x1031,
              LVM_GETITEMTEXTW = 0x1073, LVM_GETITEMSPACING = 0x1033;
    public static IntPtr Lv;

    // The desktop's own list view (Progman or a WorkerW > SHELLDLL_DefView > SysListView32 "FolderView"). Nothing else is touched.
    public static IntPtr Find() {
        IntPtr def = Native.FindWindowEx(Native.FindWindow("Progman", null), IntPtr.Zero, "SHELLDLL_DefView", null), w = IntPtr.Zero;
        while (def == IntPtr.Zero) {
            w = Native.FindWindowEx(IntPtr.Zero, w, "WorkerW", null);
            if (w == IntPtr.Zero) return IntPtr.Zero;
            def = Native.FindWindowEx(w, IntPtr.Zero, "SHELLDLL_DefView", null);
        }
        return Native.FindWindowEx(def, IntPtr.Zero, "SysListView32", "FolderView");
    }
    public static bool Valid { get { return Lv != IntPtr.Zero && Native.IsWindow(Lv); } }
    public static int Count() { return Valid ? (int)Native.SendMessage(Lv, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero) : 0; }
    public static bool AutoArrange { get { return Valid && (Native.GetWindowLong(Lv, -16) & 0x0100) != 0; } }
    public static void Spacing(out int cx, out int cy) {
        int sp = Valid ? (int)Native.SendMessage(Lv, LVM_GETITEMSPACING, IntPtr.Zero, IntPtr.Zero) : 0;
        cx = sp & 0xFFFF; cy = (sp >> 16) & 0xFFFF; if (cx < 20) cx = 76; if (cy < 20) cy = 98;
    }

    public static List<DIcon> Read() {
        var list = new List<DIcon>();
        if (!Valid) return list;
        var seen = new Dictionary<string, int>();
        uint pid; Native.GetWindowThreadProcessId(Lv, out pid);
        IntPtr proc = Native.OpenProcess(0x0038, false, pid), n;
        if (proc == IntPtr.Zero) return list;
        IntPtr mem = Native.VirtualAllocEx(proc, IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x04);
        if (mem == IntPtr.Zero) { Native.CloseHandle(proc); return list; }
        try {
            int count = Count();
            for (int i = 0; i < count; i++) {
                byte[] item = new byte[88];
                BitConverter.GetBytes(1).CopyTo(item, 0);                 // LVIF_TEXT
                BitConverter.GetBytes(i).CopyTo(item, 4);
                BitConverter.GetBytes((long)mem + 512).CopyTo(item, 24);  // pszText
                BitConverter.GetBytes(260).CopyTo(item, 32);              // cchTextMax
                Native.WriteProcessMemory(proc, mem, item, (IntPtr)88, out n);
                Native.SendMessage(Lv, LVM_GETITEMTEXTW, (IntPtr)i, mem);
                byte[] txt = new byte[520]; Native.ReadProcessMemory(proc, mem + 512, txt, (IntPtr)520, out n);
                string name = Encoding.Unicode.GetString(txt); int z = name.IndexOf('\0'); if (z >= 0) name = name.Substring(0, z);
                Native.SendMessage(Lv, LVM_GETITEMPOSITION, (IntPtr)i, mem + 2048);
                byte[] pt = new byte[8]; Native.ReadProcessMemory(proc, mem + 2048, pt, (IntPtr)8, out n);
                int c; seen.TryGetValue(name, out c); seen[name] = ++c;
                list.Add(new DIcon { Name = name, N = c, X = BitConverter.ToInt32(pt, 0), Y = BitConverter.ToInt32(pt, 4) });
            }
        } finally { Native.VirtualFreeEx(proc, mem, UIntPtr.Zero, 0x8000); Native.CloseHandle(proc); }
        return list;
    }

    public static HashSet<string> Keys(List<DIcon> icons) { return new HashSet<string>(icons.Select(i => i.Key)); }

    public sealed class Mover : IDisposable {
        IntPtr proc, mem; readonly byte[] pt = new byte[8];
        public Mover() {
            if (!Valid) return;
            uint pid; Native.GetWindowThreadProcessId(Lv, out pid);
            proc = Native.OpenProcess(0x0038, false, pid);
            if (proc != IntPtr.Zero) mem = Native.VirtualAllocEx(proc, IntPtr.Zero, (UIntPtr)64, 0x3000, 0x04);
        }
        public bool Ok { get { return proc != IntPtr.Zero && mem != IntPtr.Zero; } }
        public void Move(int index, int x, int y) {
            IntPtr n;
            BitConverter.GetBytes(x).CopyTo(pt, 0); BitConverter.GetBytes(y).CopyTo(pt, 4);
            Native.WriteProcessMemory(proc, mem, pt, (IntPtr)8, out n);
            Native.SendMessage(Lv, LVM_SETITEMPOSITION32, (IntPtr)index, mem);
        }
        public void Dispose() {
            if (mem != IntPtr.Zero) Native.VirtualFreeEx(proc, mem, UIntPtr.Zero, 0x8000);
            if (proc != IntPtr.Zero) Native.CloseHandle(proc);
            mem = proc = IntPtr.Zero;
        }
    }

    // screen rectangles in the list view's client coordinates
    public static D.Rectangle ToClient(D.Rectangle r) {
        var tl = new Native.POINT { X = r.Left, Y = r.Top }; var br = new Native.POINT { X = r.Right, Y = r.Bottom };
        Native.ScreenToClient(Lv, ref tl); Native.ScreenToClient(Lv, ref br);
        return D.Rectangle.FromLTRB(tl.X, tl.Y, br.X, br.Y);
    }
}

// ---------------------------------------------------------------- settings
public class Hk {
    public uint Mods; public int Vk;   // Mods: Alt 1, Ctrl 2, Shift 4, Win 8 (same bits as RegisterHotKey and WPF ModifierKeys)
    public bool Empty { get { return Vk == 0; } }
    public bool Same(Hk o) { return o != null && !Empty && o.Mods == Mods && o.Vk == Vk; }
    public override string ToString() {
        if (Empty) return "None";
        var p = new List<string>();
        if ((Mods & 8) != 0) p.Add("Win"); if ((Mods & 2) != 0) p.Add("Ctrl");
        if ((Mods & 1) != 0) p.Add("Alt"); if ((Mods & 4) != 0) p.Add("Shift");
        var k = (F.Keys)Vk;
        string name = k >= F.Keys.D0 && k <= F.Keys.D9 ? ((int)(k - F.Keys.D0)).ToString()
                    : k >= F.Keys.NumPad0 && k <= F.Keys.NumPad9 ? "Num " + (int)(k - F.Keys.NumPad0) : k.ToString();
        p.Add(name);
        return string.Join("+", p);
    }
    public static Hk Parse(string s) {
        var h = new Hk();
        if (string.IsNullOrWhiteSpace(s) || s.Trim() == "None") return h;
        var parts = s.Split('+').Select(x => x.Trim()).ToList();
        for (int i = 0; i < parts.Count - 1; i++) {
            string m = parts[i].ToLowerInvariant();
            if (m == "win") h.Mods |= 8; else if (m == "ctrl") h.Mods |= 2; else if (m == "alt") h.Mods |= 1; else if (m == "shift") h.Mods |= 4;
        }
        string key = parts[parts.Count - 1]; F.Keys k;
        if (key.Length == 1 && char.IsDigit(key[0])) h.Vk = (int)F.Keys.D0 + (key[0] - '0');
        else if (key.StartsWith("Num ")) { int d; if (int.TryParse(key.Substring(4), out d)) h.Vk = (int)F.Keys.NumPad0 + d; }
        else if (Enum.TryParse(key, true, out k)) h.Vk = (int)k;
        return h;
    }
}

class Settings {
    public static readonly string[] KeyIds = { "hk_save", "hk_rearrange", "hk_undo", "hk_open" };
    public static readonly string[] KeyNames = { "Save sort now", "Rearrange to the active sort", "Undo the last move", "Open NJ-Sort" };
    static readonly string[] Defaults = { "Alt+S", "Ctrl+Alt+R", "Ctrl+Alt+Z", "Ctrl+Alt+N" };
    public Hk[] Keys = Defaults.Select(Hk.Parse).ToArray();
    public bool AutoRestore = true, SortExtras = true, Animate = true, Sound = true, TipShown;
    public int Volume = 70;
    public string Active = "";
    static string FilePath { get { return Path.Combine(App.Dir, "settings.ini"); } }

    public static Settings Load() {
        var s = new Settings();
        try {
            if (!File.Exists(FilePath)) return s;
            uint oldMods = 0; string oldKey = null;
            foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8)) {
                int eq = line.IndexOf('='); if (eq < 0 || line.StartsWith("#")) continue;
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                int ki = Array.IndexOf(KeyIds, k);
                if (ki >= 0) s.Keys[ki] = Hk.Parse(v);
                else if (k == "auto_restore") s.AutoRestore = v != "0";
                else if (k == "sort_extras") s.SortExtras = v != "0";
                else if (k == "animate") s.Animate = v != "0";
                else if (k == "sound") s.Sound = v != "0";
                else if (k == "tip_shown") s.TipShown = v != "0";
                else if (k == "volume") { int x; if (int.TryParse(v, out x)) s.Volume = Math.Max(0, Math.Min(100, x)); }
                else if (k == "active") s.Active = v;
                else if (k == "save_mods") uint.TryParse(v, out oldMods);          // the old NJ-Sort save hotkey
                else if (k == "save_key") oldKey = v;
            }
            if (oldMods != 0 && oldKey != null) { F.Keys kk; if (Enum.TryParse(oldKey, out kk)) s.Keys[0] = new Hk { Mods = oldMods, Vk = (int)kk }; }
        } catch (Exception e) { App.Log("settings: " + e.Message); }
        return s;
    }

    public void Write() {
        var lines = new List<string> { "# NJ-Sort settings (changed from the NJ-Sort window)" };
        for (int i = 0; i < KeyIds.Length; i++) lines.Add(KeyIds[i] + "=" + Keys[i]);
        lines.Add("auto_restore=" + (AutoRestore ? 1 : 0)); lines.Add("sort_extras=" + (SortExtras ? 1 : 0));
        lines.Add("animate=" + (Animate ? 1 : 0)); lines.Add("sound=" + (Sound ? 1 : 0)); lines.Add("volume=" + Volume);
        lines.Add("tip_shown=" + (TipShown ? 1 : 0)); lines.Add("active=" + Active);
        try { App.WriteAtomic(FilePath, lines); } catch (Exception e) { App.Log("settings write: " + e.Message); }
    }
}

// ---------------------------------------------------------------- saved sorts (history)
public class Sort {
    public string Id, Name = "";
    public DateTime Saved;
    public List<DIcon> Icons = new List<DIcon>();
    public string Title { get { return string.IsNullOrEmpty(Name) ? Saved.ToString("ddd d MMM, HH:mm") : Name; } }
}

static class Store {
    static string SortsDir { get { return Path.Combine(App.Dir, "sorts"); } }
    static string UndoFile { get { return Path.Combine(App.Dir, "undo.tsv"); } }
    public static List<Sort> All = new List<Sort>();

    public static void Reload() {
        var list = new List<Sort>();
        if (Directory.Exists(SortsDir))
            foreach (var f in Directory.GetFiles(SortsDir, "*.tsv")) {
                try { list.Add(Parse(f)); } catch (Exception e) { App.Log("unreadable sort " + Path.GetFileName(f) + ": " + e.Message); }
            }
        All = list.OrderByDescending(s => s.Saved).ToList();
    }
    public static Sort Find(string id) { return All.FirstOrDefault(s => s.Id == id); }

    static Sort Parse(string file) {
        var s = new Sort { Id = Path.GetFileNameWithoutExtension(file), Saved = File.GetLastWriteTime(file) };
        foreach (var line in File.ReadAllLines(file, Encoding.UTF8)) {
            var p = line.Split('\t');
            if (line.StartsWith("#name\t")) s.Name = line.Substring(6);
            else if (line.StartsWith("#saved\t")) { DateTime t; if (DateTime.TryParse(p[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out t)) s.Saved = t; }
            else if (p.Length == 4) s.Icons.Add(new DIcon { Name = p[0], N = int.Parse(p[1]), X = int.Parse(p[2]), Y = int.Parse(p[3]) });
        }
        return s;
    }

    public static void Write(Sort s) {
        Directory.CreateDirectory(SortsDir);
        var lines = new List<string> { "#name\t" + s.Name, "#saved\t" + s.Saved.ToString("o") };
        lines.AddRange(s.Icons.Select(i => i.Name + "\t" + i.N + "\t" + i.X + "\t" + i.Y));
        App.WriteAtomic(Path.Combine(SortsDir, s.Id + ".tsv"), lines);
    }

    public static Sort Add(List<DIcon> icons, string name, DateTime when) {
        string id = when.ToString("yyyyMMdd-HHmmss");
        while (File.Exists(Path.Combine(SortsDir, id + ".tsv"))) id += "x";
        var s = new Sort { Id = id, Name = name, Saved = when, Icons = icons.Select(i => new DIcon { Name = i.Name, N = i.N, X = i.X, Y = i.Y }).ToList() };
        Write(s); Reload();
        return Find(id);
    }

    public static void Rename(Sort s, string name) { s.Name = (name ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim(); Write(s); }

    // deleted sorts go to the Recycle Bin, so they can be restored
    public static void Delete(Sort s) {
        string f = Path.Combine(SortsDir, s.Id + ".tsv");
        if (File.Exists(f)) Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(f, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        Reload();
    }

    // the single save of the old NJ-Sort becomes the first entry of the history
    public static void Migrate() {
        string old = Path.Combine(App.Dir, "layout.tsv");
        if (Directory.Exists(SortsDir) && Directory.GetFiles(SortsDir, "*.tsv").Length > 0) return;
        if (!File.Exists(old)) return;
        var icons = new List<DIcon>();
        foreach (var line in File.ReadAllLines(old, Encoding.UTF8)) {
            var p = line.Split('\t'); int n, x, y;
            if (p.Length == 4 && int.TryParse(p[1], out n) && int.TryParse(p[2], out x) && int.TryParse(p[3], out y))
                icons.Add(new DIcon { Name = p[0], N = n, X = x, Y = y });
        }
        if (icons.Count == 0) return;
        Add(icons, "Imported from the old NJ-Sort", File.GetLastWriteTime(old));
        App.Log("imported layout.tsv as the first sort (" + icons.Count + " items)");
    }

    public static void WriteUndo(List<DIcon> icons) {
        try { App.WriteAtomic(UndoFile, icons.Select(i => i.Name + "\t" + i.N + "\t" + i.X + "\t" + i.Y)); } catch (Exception e) { App.Log("undo write: " + e.Message); }
    }
    public static Dictionary<string, DIcon> ReadUndo() {
        if (!File.Exists(UndoFile)) return null;
        var d = new Dictionary<string, DIcon>();
        foreach (var line in File.ReadAllLines(UndoFile, Encoding.UTF8)) {
            var p = line.Split('\t'); int n, x, y;
            if (p.Length == 4 && int.TryParse(p[1], out n) && int.TryParse(p[2], out x) && int.TryParse(p[3], out y)) {
                var i = new DIcon { Name = p[0], N = n, X = x, Y = y }; d[i.Key] = i;
            }
        }
        return d.Count == 0 ? null : d;
    }
}

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

// ---------------------------------------------------------------- sound (synthesised, quiet)
static class Sfx {
    const int R = 44100;
    static System.Media.SoundPlayer player;
    public static void Play(string kind) {
        var c = Core.Cfg;
        if (c == null || !c.Sound || c.Volume <= 0) return;
        try {
            float[] s = kind == "save" ? SaveSnd() : kind == "undo" ? UndoSnd() : kind == "error" ? ErrSnd() : kind == "info" ? InfoSnd() : SortSnd();
            if (player != null) player.Stop();
            player = new System.Media.SoundPlayer(new MemoryStream(Wav(s, c.Volume / 100.0)));
            player.Play();
        } catch (Exception e) { App.Log("sound: " + e.Message); }
    }
    static float[] Buf(double sec) { return new float[(int)(R * sec)]; }
    // a soft mallet note: fundamental + quickly fading overtones
    static void Note(float[] b, double at, double f, double amp, double decay) {
        int s0 = (int)(at * R);
        for (int i = s0; i < b.Length; i++) {
            double t = (i - s0) / (double)R, env = Math.Min(1, t / 0.003) * Math.Exp(-t / decay);
            if (t > 0.02 && env < 1e-4) break;
            b[i] += (float)(amp * env * (Math.Sin(2 * Math.PI * f * t) + 0.3 * Math.Sin(4 * Math.PI * f * t) * Math.Exp(-t / (decay * 0.35))
                                         + 0.12 * Math.Sin(2 * Math.PI * 3.98 * f * t) * Math.Exp(-t / (decay * 0.2))));
        }
    }
    // filtered noise swelling and fading: the icons moving
    static void Whoosh(float[] b, double at, double dur, double amp, int seed) {
        var rnd = new Random(seed); double l1 = 0, l2 = 0, h = 0;
        int s0 = (int)(at * R), n = (int)(dur * R);
        for (int i = 0; i < n && s0 + i < b.Length; i++) {
            double p = i / (double)n, cut = 0.03 + 0.16 * Math.Sin(Math.PI * p);
            double x = rnd.NextDouble() * 2 - 1; l1 += cut * (x - l1); l2 += cut * (l1 - l2); h += 0.02 * (l2 - h);
            b[s0 + i] += (float)(amp * Math.Pow(Math.Sin(Math.PI * p), 2) * (l2 - h) * 3);
        }
    }
    static void Tick(float[] b, double at, double f, double amp) {
        int s0 = (int)(at * R);
        for (int i = 0; i < R * 0.03 && s0 + i < b.Length; i++) { double t = i / (double)R; b[s0 + i] += (float)(amp * Math.Exp(-t / 0.006) * Math.Sin(2 * Math.PI * f * t)); }
    }
    static float[] SortSnd() {
        var b = Buf(1.1);
        Whoosh(b, 0, 0.46, 0.9, 7);
        for (int k = 0; k < 5; k++) Tick(b, 0.07 + k * 0.055, 1700 + k * 160, 0.16);   // little landings
        Note(b, 0.34, 1318.5, 0.32, 0.22); Note(b, 0.43, 1975.5, 0.30, 0.32);           // E6, B6
        return b;
    }
    static float[] SaveSnd() {
        var b = Buf(0.9);
        Tick(b, 0, 2400, 0.35); Note(b, 0.03, 1046.5, 0.30, 0.2); Note(b, 0.11, 1568.0, 0.30, 0.36);   // C6, G6
        return b;
    }
    static float[] UndoSnd() {
        var b = Buf(0.9);
        Whoosh(b, 0, 0.32, 0.6, 11); Note(b, 0.16, 1568.0, 0.26, 0.18); Note(b, 0.25, 1046.5, 0.28, 0.3);
        return b;
    }
    static float[] InfoSnd() { var b = Buf(0.5); Note(b, 0, 1318.5, 0.22, 0.18); return b; }
    static float[] ErrSnd() { var b = Buf(0.7); Note(b, 0, 523.3, 0.3, 0.14); Note(b, 0.13, 466.2, 0.3, 0.22); return b; }

    static byte[] Wav(float[] s, double vol) {
        double peak = s.Max(x => Math.Abs(x)); if (peak < 1e-6) peak = 1;
        double g = 0.42 * vol / peak;                // never louder than 42% of full scale, scaled by the volume setting
        var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + s.Length * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(R); w.Write(R * 2); w.Write((short)2); w.Write((short)16);
        w.Write(Encoding.ASCII.GetBytes("data")); w.Write(s.Length * 2);
        foreach (var x in s) w.Write((short)Math.Max(-32767, Math.Min(32767, x * g * 32767)));
        w.Flush();
        return ms.ToArray();
    }
}

// ---------------------------------------------------------------- the toast that pops up after a sort
static class Toast {
    const string Xaml = @"
<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        WindowStyle='None' AllowsTransparency='True' Background='Transparent' Topmost='True' ShowInTaskbar='False'
        ShowActivated='False' Focusable='False' SizeToContent='WidthAndHeight' ResizeMode='NoResize' IsHitTestVisible='False'>
  <Grid x:Name='Root' Margin='30' Opacity='0' RenderTransformOrigin='0.5,1'>
    <Grid.RenderTransform><TransformGroup>
      <ScaleTransform x:Name='Sc' ScaleX='0.92' ScaleY='0.92'/><TranslateTransform x:Name='Ty' Y='18'/><TranslateTransform x:Name='Sx'/>
    </TransformGroup></Grid.RenderTransform>
    <Border CornerRadius='18' Background='#F5161838' BorderBrush='#40FFB02E' BorderThickness='1' Padding='14,12,24,12' MinWidth='300'>
      <Border.Effect><DropShadowEffect BlurRadius='30' ShadowDepth='8' Direction='270' Opacity='0.55'/></Border.Effect>
      <StackPanel Orientation='Horizontal'>
        <Grid Width='42' Height='42'>
          <Ellipse x:Name='Disc' Fill='#FFB02E' RenderTransformOrigin='0.5,0.5'>
            <Ellipse.RenderTransform><ScaleTransform x:Name='DiscSc'/></Ellipse.RenderTransform>
          </Ellipse>
          <Path x:Name='Mark' Stroke='#161838' StrokeThickness='3.2' StrokeStartLineCap='Round' StrokeEndLineCap='Round'
                StrokeLineJoin='Round' StrokeDashArray='24 24' StrokeDashOffset='24' Width='42' Height='42'/>
        </Grid>
        <StackPanel Margin='14,0,0,0' VerticalAlignment='Center'>
          <TextBlock x:Name='Title' FontFamily='Thmanyah Sans, Segoe UI' FontSize='17' FontWeight='Bold' Foreground='#ECEEFF'/>
          <TextBlock x:Name='Sub' FontFamily='Thmanyah Sans, Segoe UI' FontSize='13' Foreground='#AAAED6' Margin='0,2,0,0' MaxWidth='460' TextWrapping='Wrap'/>
        </StackPanel>
      </StackPanel>
    </Border>
  </Grid>
</Window>";
    static readonly Dictionary<string, string> Glyphs = new Dictionary<string, string> {
        { "save",  "M13,21.5 L18.5,27 L29,15.5" },                                  // check
        { "sort",  "M12,14.5 L30,14.5 M12,21 L25,21 M12,27.5 L20,27.5" },           // sorted lines
        { "undo",  "M16,13.5 L11,18.5 L16,23.5 M11,18.5 L24,18.5 C34,18.5 34,29 24,29 L18,29" },
        { "info",  "M21,13 L21,13.2 M21,19 L21,29" },
        { "error", "M21,12.5 L21,23 M21,29 L21,29.2" } };
    internal static W.Window win; static WT.DispatcherTimer hold; static int gen;
    static W.UIElement root; static WM.ScaleTransform sc, discSc; static WM.TranslateTransform ty, sx;
    static W.Shapes.Path mark; static W.Shapes.Ellipse disc; static WC.TextBlock title, sub;

    static void Build() {
        win = (W.Window)W.Markup.XamlReader.Parse(Xaml);
        Func<string, object> n = win.FindName;
        root = (W.UIElement)n("Root"); sc = (WM.ScaleTransform)n("Sc"); ty = (WM.TranslateTransform)n("Ty"); sx = (WM.TranslateTransform)n("Sx");
        discSc = (WM.ScaleTransform)n("DiscSc"); disc = (W.Shapes.Ellipse)n("Disc"); mark = (W.Shapes.Path)n("Mark");
        title = (WC.TextBlock)n("Title"); sub = (WC.TextBlock)n("Sub");
        win.SourceInitialized += delegate {   // click-through, never takes focus, not in Alt+Tab
            var h = new WIn.WindowInteropHelper(win).Handle;
            Native.SetWindowLong(h, -20, Native.GetWindowLong(h, -20) | 0x20 | 0x80 | 0x08000000);
        };
        hold = new WT.DispatcherTimer();
        hold.Tick += delegate { hold.Stop(); Out(); };
    }

    static WA.DoubleAnimation A(double? from, double to, int ms, WA.IEasingFunction e, int delay = 0) {
        var a = new WA.DoubleAnimation { To = to, Duration = TimeSpan.FromMilliseconds(ms), EasingFunction = e, BeginTime = TimeSpan.FromMilliseconds(delay) };
        if (from.HasValue) a.From = from.Value;
        return a;
    }
    static void S2(WM.ScaleTransform s, WA.AnimationTimeline a) { s.BeginAnimation(WM.ScaleTransform.ScaleXProperty, a); s.BeginAnimation(WM.ScaleTransform.ScaleYProperty, a); }

    public static void Show(string kind, string head, string detail) {
        try {
            if (win == null) Build();
            gen++;
            title.Text = head; sub.Text = detail ?? ""; sub.Visibility = string.IsNullOrEmpty(detail) ? W.Visibility.Collapsed : W.Visibility.Visible;
            string g; if (!Glyphs.TryGetValue(kind, out g)) g = Glyphs["info"];
            mark.Data = WM.Geometry.Parse(g);
            bool bad = kind == "error";
            disc.Fill = new WM.SolidColorBrush(bad ? WM.Color.FromRgb(0xF8, 0x71, 0x71) : kind == "info" ? WM.Color.FromRgb(0x8E, 0x95, 0xE6) : WM.Color.FromRgb(0xFF, 0xB0, 0x2E));
            mark.Stroke = new WM.SolidColorBrush(WM.Color.FromRgb(0x16, 0x18, 0x38));
            if (!win.IsVisible) win.Show();
            win.UpdateLayout();
            var wa = W.SystemParameters.WorkArea;
            win.Left = wa.Left + (wa.Width - win.ActualWidth) / 2; win.Top = wa.Bottom - win.ActualHeight - 8;

            bool motion = W.SystemParameters.ClientAreaAnimation && Core.Cfg.Animate;
            var back = new WA.BackEase { Amplitude = 0.45, EasingMode = WA.EasingMode.EaseOut };
            var cout = new WA.CubicEase { EasingMode = WA.EasingMode.EaseOut };
            int d = motion ? 1 : 0;
            root.BeginAnimation(W.UIElement.OpacityProperty, A(null, 1, 160 * d + 1, null));
            ty.BeginAnimation(WM.TranslateTransform.YProperty, A(motion ? 18 : 0, 0, 420 * d + 1, back));
            S2(sc, A(motion ? 0.92 : 1, 1, 420 * d + 1, back));
            S2(discSc, A(motion ? 0.3 : 1, 1, 460 * d + 1, new WA.BackEase { Amplitude = 0.8, EasingMode = WA.EasingMode.EaseOut }, 60 * d));
            mark.BeginAnimation(W.Shapes.Shape.StrokeDashOffsetProperty, A(motion ? 24 : 0, 0, 300 * d + 1, cout, 230 * d));
            if (bad && motion)
                sx.BeginAnimation(WM.TranslateTransform.XProperty, new WA.DoubleAnimation(-6, 6, TimeSpan.FromMilliseconds(55)) {
                    AutoReverse = true, RepeatBehavior = new WA.RepeatBehavior(3), BeginTime = TimeSpan.FromMilliseconds(180), FillBehavior = WA.FillBehavior.Stop });
            hold.Stop(); hold.Interval = TimeSpan.FromMilliseconds(bad ? 3600 : 2200); hold.Start();
        } catch (Exception e) { App.Log("toast: " + e.Message); }
    }

    // README images: the toast at rest, rendered off-screen
    public static W.UIElement RenderPose(string kind, string head, string detail) {
        if (win == null) Build();
        title.Text = head; sub.Text = detail ?? ""; mark.Data = WM.Geometry.Parse(Glyphs[kind]);
        disc.Fill = new WM.SolidColorBrush(WM.Color.FromRgb(0xFF, 0xB0, 0x2E));
        root.Opacity = 1; sc.ScaleX = sc.ScaleY = 1; ty.Y = 0; discSc.ScaleX = discSc.ScaleY = 1; mark.StrokeDashOffset = 0;
        win.Left = -20000; win.Show(); win.UpdateLayout();
        return (W.UIElement)win.Content;
    }

    public static void SelfTest() {
        Build(); foreach (var g in Glyphs.Values) WM.Geometry.Parse(g);
        win.Left = -20000; win.Show(); win.UpdateLayout(); win.Close();
    }

    static void Out() {
        int my = gen;
        bool motion = W.SystemParameters.ClientAreaAnimation && Core.Cfg.Animate;
        var fade = A(null, 0, motion ? 260 : 1, new WA.QuadraticEase { EasingMode = WA.EasingMode.EaseIn });
        fade.Completed += delegate { if (my == gen) win.Hide(); };
        root.BeginAnimation(W.UIElement.OpacityProperty, fade);
        if (motion) ty.BeginAnimation(WM.TranslateTransform.YProperty, A(null, 10, 260, new WA.QuadraticEase { EasingMode = WA.EasingMode.EaseIn }));
    }
}

// ---------------------------------------------------------------- the program
static class Core {
    public static Settings Cfg;
    public static string[] HkError = new string[4];
    public static Sort Active { get { var s = Store.Find(Cfg.Active); return s ?? Store.All.FirstOrDefault(); } }

    static WIn.HwndSource msgWin; static F.NotifyIcon tray; static F.ToolStripMenuItem[] trayItems;
    static MainWin main;
    static IntPtr hook; static Native.WinEventProc hookProc;
    static WT.DispatcherTimer watch, debounce;
    static HashSet<string> lastKeys;
    static bool pending; static string pendingWhy = "";
    static int lastCount = -1, stable;
    static DateTime startedAt, suppressUntil = DateTime.MinValue, recheckAt = DateTime.MaxValue;
    static uint taskbarCreated;
    static bool quitting;
    static EventWaitHandle showEvt, quitEvt;

    public static void Start(bool showWindow) {
        Cfg = Settings.Load();
        try { Store.Migrate(); } catch (Exception e) { App.Log("migrate: " + e.Message); }
        Store.Reload();
        if (Store.Find(Cfg.Active) == null && Store.All.Count > 0) { Cfg.Active = Store.All[0].Id; Cfg.Write(); }
        startedAt = DateTime.Now;

        // a hidden top-level window: shortcuts, Explorer restarts (TaskbarCreated), screen changes and resume reach it
        var p = new WIn.HwndSourceParameters("NJ-Sort messages") { Width = 0, Height = 0, WindowStyle = unchecked((int)0x80000000) };
        msgWin = new WIn.HwndSource(p); msgWin.AddHook(WndProc);
        taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");

        BuildTray();
        RegisterAll();
        hookProc = OnWinEvent;
        Hook();
        lastKeys = Desktop.Valid ? Desktop.Keys(Desktop.Read()) : null;
        // at sign-in (--tray) Explorer may still be laying out the desktop: wait until it is steady, then put the sort back
        if (!showWindow) MarkPending("after sign-in");

        debounce = new WT.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        debounce.Tick += delegate { debounce.Stop(); Safe(OnItemsSettled); };
        watch = new WT.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        watch.Tick += delegate { Safe(Watchdog); };
        watch.Start();

        showEvt = new EventWaitHandle(false, EventResetMode.AutoReset, "NJSort.Show");
        ThreadPool.RegisterWaitForSingleObject(showEvt, delegate { W.Application.Current.Dispatcher.BeginInvoke(new Action(OpenWindow)); }, null, -1, false);
        quitEvt = new EventWaitHandle(false, EventResetMode.AutoReset, "NJSort.Quit");   // a newer copy is being installed
        ThreadPool.RegisterWaitForSingleObject(quitEvt, delegate { W.Application.Current.Dispatcher.BeginInvoke(new Action(Quit)); }, null, -1, true);

        App.Log("NJ-Sort " + App.Version + " started" + (showWindow ? "" : " in the hidden icons") + ", " + Store.All.Count + " sorts, active: " + (Active == null ? "none" : Active.Title));
        if (showWindow) OpenWindow();
    }

    static void Safe(Action a) { try { a(); } catch (Exception e) { App.Log("error: " + e); } }

    // ---------- watching the desktop
    static void Hook() {
        if (hook != IntPtr.Zero) { Native.UnhookWinEvent(hook); hook = IntPtr.Zero; }
        Desktop.Lv = Desktop.Find();
        if (Desktop.Lv == IntPtr.Zero) return;
        uint pid; Native.GetWindowThreadProcessId(Desktop.Lv, out pid);
        hook = Native.SetWinEventHook(0x8000, 0x8004, IntPtr.Zero, hookProc, pid, 0, 0);  // create/destroy/show/hide/reorder
    }

    static void MarkPending(string why) {
        if (!pending) App.Log("waiting for the desktop to settle (" + why + ")");
        pending = true; pendingWhy = why; stable = 0; lastCount = -1; recheckAt = DateTime.MaxValue;
        debounce_Stop();
    }
    static void debounce_Stop() { if (debounce != null) debounce.Stop(); }

    static void OnWinEvent(IntPtr h, uint ev, IntPtr hwnd, int idObj, int idChild, uint th, uint t) {
        if (hwnd != Desktop.Lv || Animator.Busy || DateTime.Now < suppressUntil || pending) return;
        debounce.Stop(); debounce.Start();   // wait until the burst is over
    }

    static void Watchdog() {
        if (!Desktop.Valid) {
            if (Desktop.Lv != IntPtr.Zero) { App.Log("desktop gone (Explorer is restarting)"); Desktop.Lv = IntPtr.Zero; }
            Hook();
            if (!Desktop.Valid) return;
            App.Log("desktop is back");
            MarkPending("after Explorer restarted");
        }
        int c = Desktop.Count();
        if (c != lastCount) { lastCount = c; stable = 0; } else stable++;
        if (Animator.Busy) return;
        if (pending && c > 0 && stable >= 4 && (DateTime.Now - startedAt).TotalSeconds >= 3) {
            pending = false;
            var cur = Desktop.Read(); lastKeys = Desktop.Keys(cur);
            App.Log("desktop settled, " + cur.Count + " items (" + pendingWhy + ")");
            if (Cfg.AutoRestore && Active != null) RearrangeTo(Active, true, pendingWhy);
            return;
        }
        // Explorer sometimes re-applies its own positions shortly after it reloads: check once more and fix quietly
        if (!pending && DateTime.Now >= recheckAt) {
            recheckAt = DateTime.MaxValue;
            var s = Active; if (s == null || !Cfg.AutoRestore) return;
            var plan = Planner.Make(s, Desktop.Read(), true, null, true);
            if (plan.Moves.Count > 0) { App.Log("re-check after restore: moving " + plan.Moves.Count + " again"); Run(plan, null); }
        }
    }

    static void OnItemsSettled() {
        if (pending || !Desktop.Valid || Animator.Busy) return;
        var cur = Desktop.Read(); var keys = Desktop.Keys(cur);
        if (lastKeys == null) { lastKeys = keys; return; }
        if (keys.SetEquals(lastKeys)) return;
        var appeared = new HashSet<string>(keys.Except(lastKeys));
        int removed = lastKeys.Count(k => !keys.Contains(k));
        // a desktop that empties or loses most of its items is Explorer reloading, not the user deleting: wait it out
        if (cur.Count == 0 || removed > Math.Max(5, lastKeys.Count / 2)) { MarkPending("after the desktop reloaded"); return; }
        lastKeys = keys;
        App.Log("items changed: +" + appeared.Count + " -" + removed);
        var s = Active; if (s == null) return;
        var plan = Planner.Make(s, cur, false, appeared, Cfg.SortExtras);
        if (plan.Moves.Count > 0) Run(plan, null);
    }

    static void Run(Plan plan, Action after) {
        suppressUntil = DateTime.MaxValue;
        Animator.Run(plan, Cfg.Animate && W.SystemParameters.ClientAreaAnimation, delegate {
            suppressUntil = DateTime.Now.AddMilliseconds(900);
            if (Desktop.Valid) lastKeys = Desktop.Keys(Desktop.Read());
            if (after != null) after();
            if (main != null) main.Refresh();
        });
    }

    static bool Ready(bool auto) {
        if (!Desktop.Valid || (pending && !auto)) { Toast.Show("error", "The desktop is still loading", "Try again in a moment."); Sfx.Play("error"); return false; }
        if (Desktop.AutoArrange) { Toast.Show("error", "Auto arrange is on", "Right-click the desktop, View, and turn off Auto arrange icons."); Sfx.Play("error"); return false; }
        return true;
    }

    // ---------- actions
    public static void SaveNow() {
        if (Animator.Busy || !Ready(false)) return;
        var cur = Desktop.Read();
        if (cur.Count == 0) { Toast.Show("error", "Nothing to save", "The desktop has no items."); Sfx.Play("error"); return; }
        var newest = Store.All.FirstOrDefault();
        if (newest != null && SameLayout(newest.Icons, cur)) {
            if (Cfg.Active != newest.Id) { Cfg.Active = newest.Id; Cfg.Write(); }
            Toast.Show("info", "Already saved", "Nothing moved since \u201C" + newest.Title + "\u201D."); Sfx.Play("info");
            if (main != null) main.Refresh();
            return;
        }
        var s = Store.Add(cur, "", DateTime.Now);
        Cfg.Active = s.Id; Cfg.Write();
        lastKeys = Desktop.Keys(cur);
        App.Log("saved sort " + s.Id + " (" + cur.Count + " items)");
        Toast.Show("save", "Sort saved", cur.Count + " items. It is now the active sort.");
        Sfx.Play("save");
        if (main != null) main.Refresh();
    }

    static bool SameLayout(List<DIcon> a, List<DIcon> b) {
        if (a.Count != b.Count) return false;
        var d = a.ToDictionary(i => i.Key, i => i);
        foreach (var i in b) { DIcon o; if (!d.TryGetValue(i.Key, out o) || o.X != i.X || o.Y != i.Y) return false; }
        return true;
    }

    public static void RearrangeActive() { RearrangeTo(Active, false, null); }

    public static void RearrangeTo(Sort s, bool auto, string why) {
        if (Animator.Busy) return;
        if (s == null) {
            if (!auto) { Toast.Show("error", "No sort saved yet", "Arrange your icons, then press " + Cfg.Keys[0] + " or Save sort now."); Sfx.Play("error"); }
            return;
        }
        if (!Ready(auto)) return;
        var cur = Desktop.Read();
        if (cur.Count == 0) return;
        var plan = Planner.Make(s, cur, true, null, true);
        string detail = plan.Kept + " in their saved place" + (plan.Sorted > 0 ? ", " + plan.Sorted + " more sorted by name after them" : "");
        App.Log((auto ? "auto " : "") + "rearrange to " + s.Id + ": moving " + plan.Moves.Count + " (" + detail + ")" + (why == null ? "" : " " + why));
        if (plan.Moves.Count == 0) {
            if (!auto) { Toast.Show("info", "Already in order", detail + "."); Sfx.Play("info"); }
            return;
        }
        Store.WriteUndo(cur);
        Run(plan, delegate { if (auto) recheckAt = DateTime.Now.AddSeconds(4); });
        Toast.Show("sort", auto ? "Icons put back" : "Rearranged", auto ? (Capital(why) + ". " + detail + ".") : detail + ".");
        Sfx.Play("sort");
    }
    static string Capital(string s) { return string.IsNullOrEmpty(s) ? "" : char.ToUpper(s[0]) + s.Substring(1); }

    public static void Undo() {
        if (Animator.Busy || !Ready(false)) return;
        var u = Store.ReadUndo();
        if (u == null) { Toast.Show("info", "Nothing to undo", null); Sfx.Play("info"); return; }
        var cur = Desktop.Read(); var plan = new Plan { EX = cur.Select(c => c.X).ToArray(), EY = cur.Select(c => c.Y).ToArray() };
        for (int i = 0; i < cur.Count; i++) {
            DIcon o;
            if (u.TryGetValue(cur[i].Key, out o) && (o.X != cur[i].X || o.Y != cur[i].Y)) {
                plan.Moves.Add(new Move { Index = i, FX = cur[i].X, FY = cur[i].Y, TX = o.X, TY = o.Y });
                plan.EX[i] = o.X; plan.EY[i] = o.Y;
            }
        }
        if (plan.Moves.Count == 0) { Toast.Show("info", "Nothing to undo", "The icons are already there."); Sfx.Play("info"); return; }
        Store.WriteUndo(cur);   // undo again = redo
        App.Log("undo: moving " + plan.Moves.Count);
        Run(plan, null);
        Toast.Show("undo", "Moved back", plan.Moves.Count + " items are where they were. Undo again to redo.");
        Sfx.Play("undo");
    }

    public static void UseSort(Sort s) {
        if (s == null) return;
        Cfg.Active = s.Id; Cfg.Write();
        if (main != null) main.Refresh();
        RearrangeTo(s, false, null);
    }

    public static void DeleteSort(Sort s) {
        if (s == null) return;
        try { Store.Delete(s); } catch (Exception e) { Toast.Show("error", "Could not delete it", e.Message); return; }
        if (Cfg.Active == s.Id) { Cfg.Active = Store.All.Count > 0 ? Store.All[0].Id : ""; Cfg.Write(); }
        App.Log("deleted sort " + s.Id);
        Toast.Show("info", "Sort deleted", "\u201C" + s.Title + "\u201D is in the Recycle Bin.");
        if (main != null) main.Refresh();
    }

    // ---------- shortcuts
    public static void SuspendHotkeys() { for (int i = 0; i < 4; i++) Native.UnregisterHotKey(msgWin.Handle, i + 1); }
    public static void RegisterAll() {
        SuspendHotkeys();
        for (int i = 0; i < 4; i++) {
            HkError[i] = null;
            var h = Cfg.Keys[i];
            if (h.Empty) continue;
            if (!Native.RegisterHotKey(msgWin.Handle, i + 1, h.Mods | 0x4000, (uint)h.Vk)) {
                HkError[i] = h + " is taken by another program. Pick another.";
                App.Log("shortcut " + h + " (" + Settings.KeyNames[i] + ") is taken by another program");
            }
        }
        UpdateTray();
    }

    static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled) {
        try {
            if (msg == 0x0312) {   // WM_HOTKEY
                int id = (int)w;
                if (id == 1) SaveNow(); else if (id == 2) RearrangeActive(); else if (id == 3) Undo(); else if (id == 4) OpenWindow();
                handled = true;
            } else if (msg == taskbarCreated && taskbarCreated != 0) {
                App.Log("Explorer restarted (taskbar)");
                Hook(); MarkPending("after Explorer restarted");
            } else if (msg == 0x007E) {   // WM_DISPLAYCHANGE
                MarkPending("after the screen changed");
            } else if (msg == 0x0218 && (int)w == 0x12) {   // WM_POWERBROADCAST, PBT_APMRESUMEAUTOMATIC
                MarkPending("after the PC woke up");
            }
        } catch (Exception e) { App.Log("error: " + e.Message); }
        return IntPtr.Zero;
    }

    // ---------- hidden icons (tray)
    static void BuildTray() {
        D.Icon icon;
        try { icon = new D.Icon(D.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location), F.SystemInformation.SmallIconSize); }
        catch { icon = D.SystemIcons.Application; }
        tray = new F.NotifyIcon { Icon = icon, Text = "NJ-Sort", Visible = true };
        var menu = new F.ContextMenuStrip();
        var open = new F.ToolStripMenuItem("Open NJ-Sort", null, delegate { OpenWindow(); }) { Font = new D.Font(F.SystemInformation.MenuFont, D.FontStyle.Bold) };
        var save = new F.ToolStripMenuItem("Save sort now", null, delegate { SaveNow(); });
        var re = new F.ToolStripMenuItem("Rearrange", null, delegate { RearrangeActive(); });
        var undo = new F.ToolStripMenuItem("Undo the last move", null, delegate { Undo(); });
        var quit = new F.ToolStripMenuItem("Quit NJ-Sort (shortcuts stop)", null, delegate { Quit(); });
        menu.Items.AddRange(new F.ToolStripItem[] { open, new F.ToolStripSeparator(), save, re, undo, new F.ToolStripSeparator(), quit });
        trayItems = new[] { save, re, undo, open };
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (o, e) => { if (e.Button == F.MouseButtons.Left) OpenWindow(); };
        UpdateTray();
    }
    static void UpdateTray() {
        if (tray == null) return;
        for (int i = 0; i < 4; i++) trayItems[i].ShortcutKeyDisplayString = Cfg.Keys[i].Empty ? "" : Cfg.Keys[i].ToString();
        string t = "NJ-Sort: " + Cfg.Keys[0] + " saves, " + Cfg.Keys[1] + " rearranges";
        tray.Text = t.Length > 63 ? t.Substring(0, 63) : t;
    }

    public static void OpenWindow() {
        if (quitting) return;
        if (main == null) main = new MainWin();
        main.ShowUp();
    }

    public static void HiddenToTray() {
        if (Cfg.TipShown) return;
        Cfg.TipShown = true; Cfg.Write();
        tray.ShowBalloonTip(5000, "NJ-Sort is still running", "It sits in the hidden icons, and its shortcuts keep working. Right-click it to quit.", F.ToolTipIcon.Info);
    }

    public static bool Quitting { get { return quitting; } }
    public static void QuittingForTest() { quitting = true; }
    public static void Quit() {
        quitting = true;
        App.Log("quit (shortcuts off)");
        try { SuspendHotkeys(); } catch { }
        if (hook != IntPtr.Zero) Native.UnhookWinEvent(hook);
        if (watch != null) watch.Stop();
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
        W.Application.Current.Shutdown();
    }
}

// ---------------------------------------------------------------- the NJ-Sort window
public class SortRow {
    public string Id { get; set; }
    public string Title { get; set; }
    public string Detail { get; set; }
    public W.Visibility ActiveVis { get; set; }
}

class MainWin {
    const string Xaml = @"
<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Title='NJ-Sort' Width='1010' SizeToContent='Height' ResizeMode='CanMinimize' WindowStartupLocation='CenterScreen'
        Background='#181A3A' Foreground='#ECEEFF' FontFamily='Thmanyah Sans, Segoe UI' FontSize='14' UseLayoutRounding='True'>
  <Window.Resources>
    <SolidColorBrush x:Key='Muted' Color='#AAAED6'/>
    <Style TargetType='ScrollBar'>
      <Setter Property='Width' Value='8'/><Setter Property='MinWidth' Value='8'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='ScrollBar'>
          <Track x:Name='PART_Track' IsDirectionReversed='True' Width='8'>
            <Track.Thumb><Thumb Cursor='Hand'><Thumb.Template><ControlTemplate TargetType='Thumb'>
              <Border x:Name='T' CornerRadius='4' Background='#3A3F80'/>
              <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='T' Property='Background' Value='#5158A8'/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
          </Track>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
    <Style x:Key='Card' TargetType='Border'>
      <Setter Property='Background' Value='#1F2250'/><Setter Property='BorderBrush' Value='#2C3068'/><Setter Property='BorderThickness' Value='1'/>
      <Setter Property='CornerRadius' Value='16'/><Setter Property='Padding' Value='20,18'/>
    </Style>
    <Style x:Key='H' TargetType='TextBlock'><Setter Property='FontSize' Value='17'/><Setter Property='FontWeight' Value='Bold'/></Style>
    <Style x:Key='Sub' TargetType='TextBlock'><Setter Property='Foreground' Value='#AAAED6'/><Setter Property='FontSize' Value='13'/><Setter Property='TextWrapping' Value='Wrap'/><Setter Property='LineHeight' Value='19'/></Style>
    <Style x:Key='Btn' TargetType='Button'>
      <Setter Property='Foreground' Value='#ECEEFF'/><Setter Property='Background' Value='#2D3168'/><Setter Property='Padding' Value='16,9'/>
      <Setter Property='Cursor' Value='Hand'/><Setter Property='FontSize' Value='14'/><Setter Property='Focusable' Value='True'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='{TemplateBinding Background}' CornerRadius='10' Padding='{TemplateBinding Padding}' RenderTransformOrigin='0.5,0.5'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Opacity' Value='0.86'/></Trigger>
            <Trigger Property='IsPressed' Value='True'><Setter TargetName='B' Property='RenderTransform'><Setter.Value><ScaleTransform ScaleX='0.97' ScaleY='0.97'/></Setter.Value></Setter></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter TargetName='B' Property='Opacity' Value='0.38'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
    <Style x:Key='Primary' TargetType='Button' BasedOn='{StaticResource Btn}'>
      <Setter Property='Background' Value='#FFB02E'/><Setter Property='Foreground' Value='#181A3A'/><Setter Property='FontWeight' Value='Bold'/>
    </Style>
    <Style x:Key='Danger' TargetType='Button' BasedOn='{StaticResource Btn}'>
      <Setter Property='Background' Value='#00000000'/><Setter Property='Foreground' Value='#FF8F8F'/>
    </Style>
    <Style x:Key='IconBtn' TargetType='Button' BasedOn='{StaticResource Btn}'>
      <Setter Property='Padding' Value='10,5'/><Setter Property='Background' Value='#00000000'/><Setter Property='Foreground' Value='#AAAED6'/><Setter Property='FontSize' Value='16'/>
    </Style>
    <Style x:Key='Field' TargetType='TextBox'>
      <Setter Property='Foreground' Value='#ECEEFF'/><Setter Property='CaretBrush' Value='#ECEEFF'/><Setter Property='FontSize' Value='14'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='TextBox'>
          <Border x:Name='B' Background='#141631' BorderBrush='#353A75' BorderThickness='1.5' CornerRadius='9' Padding='10,7'>
            <ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#4A50A0'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#FFB02E'/><Setter TargetName='B' Property='Background' Value='#1A1C40'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
    <Style x:Key='HkBox' TargetType='TextBox' BasedOn='{StaticResource Field}'>
      <Setter Property='IsReadOnly' Value='True'/><Setter Property='IsReadOnlyCaretVisible' Value='False'/><Setter Property='Cursor' Value='Hand'/>
      <Setter Property='TextAlignment' Value='Center'/><Setter Property='FontWeight' Value='SemiBold'/><Setter Property='IsUndoEnabled' Value='False'/>
    </Style>
    <Style x:Key='Switch' TargetType='CheckBox'>
      <Setter Property='Foreground' Value='#ECEEFF'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Margin' Value='0,7'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='CheckBox'>
          <DockPanel Background='Transparent' LastChildFill='True'>
            <Border x:Name='Track' DockPanel.Dock='Right' Width='42' Height='24' CornerRadius='12' Background='#353A75' Margin='16,0,0,0' VerticalAlignment='Center'>
              <Ellipse x:Name='Thumb' Width='18' Height='18' Fill='#C9CCEB' HorizontalAlignment='Left' Margin='3,0,0,0'>
                <Ellipse.RenderTransform><TranslateTransform/></Ellipse.RenderTransform>
              </Ellipse>
            </Border>
            <ContentPresenter VerticalAlignment='Center'/>
          </DockPanel>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Trigger.EnterActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName='Thumb' Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)' To='18' Duration='0:0:0.18'><DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseOut'/></DoubleAnimation.EasingFunction></DoubleAnimation>
              </Storyboard></BeginStoryboard></Trigger.EnterActions>
              <Trigger.ExitActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName='Thumb' Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)' To='0' Duration='0:0:0.18'><DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseOut'/></DoubleAnimation.EasingFunction></DoubleAnimation>
              </Storyboard></BeginStoryboard></Trigger.ExitActions>
              <Setter TargetName='Track' Property='Background' Value='#FFB02E'/><Setter TargetName='Thumb' Property='Fill' Value='#181A3A'/>
            </Trigger>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Track' Property='Opacity' Value='0.88'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
  </Window.Resources>

  <Grid Margin='28,22,28,22'>
    <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>

    <DockPanel Grid.Row='0' Margin='0,0,0,18'>
      <Border DockPanel.Dock='Right' VerticalAlignment='Center' Background='#1B3A33' CornerRadius='13' Padding='12,6'>
        <StackPanel Orientation='Horizontal'>
          <Ellipse Width='8' Height='8' Fill='#4ADE80' VerticalAlignment='Center' Margin='0,0,8,0'/>
          <TextBlock Text='Running in the hidden icons' Foreground='#B6F2C9' FontSize='12.5'/>
        </StackPanel>
      </Border>
      <Image x:Name='Logo' Width='54' Height='54' Margin='0,0,16,0'/>
      <StackPanel VerticalAlignment='Center'>
        <TextBlock Text='NJ-Sort' FontSize='26' FontWeight='Bold'/>
        <TextBlock Text='Keeps your desktop icons in the sort you saved.' Foreground='{StaticResource Muted}'/>
      </StackPanel>
    </DockPanel>

    <Grid Grid.Row='1'>
      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='18'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
      <StackPanel Grid.Column='0'>
        <Border Style='{StaticResource Card}'>
          <StackPanel>
            <TextBlock Style='{StaticResource H}' Text='Desktop'/>
            <TextBlock x:Name='ActiveLine' Style='{StaticResource Sub}' Margin='0,5,0,14'/>
            <UniformGrid Columns='3'>
              <Button x:Name='SaveBtn' Style='{StaticResource Primary}' Margin='0,0,6,0' ToolTip='Saves where every icon is now as a new sort, and makes it the active one.'>
                <StackPanel><TextBlock Text='Save sort now' HorizontalAlignment='Center'/><TextBlock x:Name='SaveHk' FontSize='12' FontWeight='Normal' Opacity='0.75' HorizontalAlignment='Center'/></StackPanel>
              </Button>
              <Button x:Name='RearrangeBtn' Style='{StaticResource Btn}' Margin='3,0,3,0' ToolTip='Puts the icons back in the active sort. Items that are not in it follow, sorted by name.'>
                <StackPanel><TextBlock Text='Rearrange' HorizontalAlignment='Center'/><TextBlock x:Name='RearrangeHk' FontSize='12' Opacity='0.7' HorizontalAlignment='Center'/></StackPanel>
              </Button>
              <Button x:Name='UndoBtn' Style='{StaticResource Btn}' Margin='6,0,0,0' ToolTip='Moves the icons back to where they were before the last rearrange.'>
                <StackPanel><TextBlock Text='Undo' HorizontalAlignment='Center'/><TextBlock x:Name='UndoHk' FontSize='12' Opacity='0.7' HorizontalAlignment='Center'/></StackPanel>
              </Button>
            </UniformGrid>
          </StackPanel>
        </Border>

        <Border Style='{StaticResource Card}' Margin='0,16,0,0'>
          <StackPanel>
            <TextBlock Style='{StaticResource H}' Text='Sort history'/>
            <TextBlock Style='{StaticResource Sub}' Margin='0,5,0,12' Text='Newest on top. Pick a sort and use it to go back to that point. Double-click works too.'/>
            <Grid>
              <ListBox x:Name='History' Height='214' Background='Transparent' BorderThickness='0' Padding='0'
                       ScrollViewer.HorizontalScrollBarVisibility='Disabled'>
                <ListBox.ItemContainerStyle>
                  <Style TargetType='ListBoxItem'>
                    <Setter Property='Cursor' Value='Hand'/>
                    <Setter Property='Template'><Setter.Value>
                      <ControlTemplate TargetType='ListBoxItem'>
                        <Border x:Name='B' Background='#252859' BorderBrush='#00000000' BorderThickness='1.5' CornerRadius='11' Padding='14,9' Margin='0,0,4,6'>
                          <ContentPresenter/>
                        </Border>
                        <ControlTemplate.Triggers>
                          <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Background' Value='#2C3068'/></Trigger>
                          <Trigger Property='IsSelected' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#FFB02E'/><Setter TargetName='B' Property='Background' Value='#2C3068'/></Trigger>
                        </ControlTemplate.Triggers>
                      </ControlTemplate>
                    </Setter.Value></Setter>
                  </Style>
                </ListBox.ItemContainerStyle>
                <ListBox.ItemTemplate>
                  <DataTemplate>
                    <DockPanel>
                      <Border DockPanel.Dock='Right' Visibility='{Binding ActiveVis}' Background='#33FFB02E' CornerRadius='9' Padding='9,2' VerticalAlignment='Center' Margin='10,0,0,0'>
                        <TextBlock Text='Active' Foreground='#FFB02E' FontSize='12' FontWeight='Bold'/>
                      </Border>
                      <StackPanel>
                        <TextBlock Text='{Binding Title}' Foreground='#ECEEFF' FontSize='14.5' FontWeight='SemiBold' TextTrimming='CharacterEllipsis'/>
                        <TextBlock Text='{Binding Detail}' Foreground='#AAAED6' FontSize='12.5' Margin='0,1,0,0'/>
                      </StackPanel>
                    </DockPanel>
                  </DataTemplate>
                </ListBox.ItemTemplate>
              </ListBox>
              <TextBlock x:Name='Empty' Style='{StaticResource Sub}' HorizontalAlignment='Center' VerticalAlignment='Center' TextAlignment='Center'
                         Text='No sorts yet. Arrange your icons, then press Save sort now.'/>
            </Grid>
            <DockPanel x:Name='RenamePanel' Visibility='Collapsed' Margin='0,8,0,0'>
              <Button x:Name='RenameOk' DockPanel.Dock='Right' Style='{StaticResource Primary}' Content='Save name' Margin='8,0,0,0'/>
              <TextBox x:Name='RenameBox' Style='{StaticResource Field}'/>
            </DockPanel>
            <StackPanel Orientation='Horizontal' Margin='0,10,0,0'>
              <Button x:Name='UseBtn' Style='{StaticResource Btn}' Content='Use this sort' Margin='0,0,8,0'/>
              <Button x:Name='RenameBtn' Style='{StaticResource Btn}' Content='Rename' Margin='0,0,8,0'/>
              <Button x:Name='DeleteBtn' Style='{StaticResource Danger}' Content='Delete' ToolTip='Sends the sort to the Recycle Bin.'/>
            </StackPanel>
          </StackPanel>
        </Border>
      </StackPanel>

      <StackPanel Grid.Column='2'>
        <Border Style='{StaticResource Card}'>
          <StackPanel>
            <TextBlock Style='{StaticResource H}' Text='Shortcuts'/>
            <TextBlock Style='{StaticResource Sub}' Margin='0,5,0,10'
                       Text='Click a box and press the new keys; Backspace removes it. Shortcuts work while NJ-Sort runs in the hidden icons, and stop when you quit it.'/>
            <StackPanel x:Name='HkRows'/>
          </StackPanel>
        </Border>

        <Border Style='{StaticResource Card}' Margin='0,16,0,0'>
          <StackPanel>
            <TextBlock Style='{StaticResource H}' Text='Behaviour' Margin='0,0,0,6'/>
            <CheckBox x:Name='CAuto' Style='{StaticResource Switch}'>
              <StackPanel><TextBlock Text='Put icons back by themselves'/>
                <TextBlock Style='{StaticResource Sub}' FontSize='12.5' Text='After Explorer restarts (NJ-Flush, updates), after sign-in and restarts, and after screen changes. It waits until the desktop is steady first.'/></StackPanel>
            </CheckBox>
            <CheckBox x:Name='CExtras' Style='{StaticResource Switch}'>
              <StackPanel><TextBlock Text='Keep new items sorted by name'/>
                <TextBlock Style='{StaticResource Sub}' FontSize='12.5' Text='Items that are not in the sort go after it on the main screen, folders first, like Windows sort by name.'/></StackPanel>
            </CheckBox>
            <CheckBox x:Name='CAnim' Style='{StaticResource Switch}'>
              <StackPanel><TextBlock Text='Animate'/>
                <TextBlock Style='{StaticResource Sub}' FontSize='12.5' Text='Icons glide into place. Off when Windows animations are off.'/></StackPanel>
            </CheckBox>
            <CheckBox x:Name='CSound' Style='{StaticResource Switch}'><TextBlock Text='Play sounds'/></CheckBox>
            <DockPanel Margin='0,0,0,6'>
              <TextBlock x:Name='VolText' DockPanel.Dock='Right' Style='{StaticResource Sub}' Width='44' TextAlignment='Right' VerticalAlignment='Center'/>
              <TextBlock Text='Volume' Style='{StaticResource Sub}' Width='70' VerticalAlignment='Center'/>
              <Slider x:Name='Vol' Minimum='0' Maximum='100' IsMoveToPointEnabled='True' VerticalAlignment='Center' Cursor='Hand'/>
            </DockPanel>
            <CheckBox x:Name='CLogon' Style='{StaticResource Switch}'>
              <StackPanel><TextBlock Text='Start with Windows'/>
                <TextBlock Style='{StaticResource Sub}' FontSize='12.5' Text='Starts in the hidden icons when you sign in.'/></StackPanel>
            </CheckBox>
          </StackPanel>
        </Border>
      </StackPanel>
    </Grid>

    <DockPanel Grid.Row='2' Margin='0,18,0,0'>
      <Button x:Name='CloseBtn' DockPanel.Dock='Right' Style='{StaticResource Btn}' Content='Close (keeps running)'/>
      <TextBlock x:Name='Ver' DockPanel.Dock='Right' Foreground='#6E73A8' FontSize='12.5' VerticalAlignment='Center' Margin='0,0,16,0'/>
      <Button x:Name='QuitBtn' Style='{StaticResource Danger}' Content='Quit NJ-Sort' HorizontalAlignment='Left' ToolTip='Stops NJ-Sort. Its shortcuts stop working until you open it again.'/>
    </DockPanel>
  </Grid>
</Window>";

    internal W.Window win; Func<string, object> n;
    WC.ListBox history; WC.TextBlock activeLine, empty, saveHk, rearrangeHk, undoHk, volText;
    WC.DockPanel renamePanel; WC.TextBox renameBox;
    WC.Button useBtn, renameBtn, deleteBtn;
    WC.CheckBox cAuto, cExtras, cAnim, cSound, cLogon; WC.Slider vol;
    WC.TextBox[] hkBox = new WC.TextBox[4]; WC.TextBlock[] hkErr = new WC.TextBlock[4];
    bool loading;

    T N<T>(string name) where T : class { return (T)n(name); }

    public MainWin() {
        win = (W.Window)W.Markup.XamlReader.Parse(Xaml);
        n = win.FindName;
        try {
            var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("nj-sort.png");
            var bi = new WM.Imaging.BitmapImage(); bi.BeginInit(); bi.StreamSource = st; bi.CacheOption = WM.Imaging.BitmapCacheOption.OnLoad; bi.EndInit();
            N<WC.Image>("Logo").Source = bi; win.Icon = bi;
        } catch { }
        win.SourceInitialized += delegate {   // dark title bar
            int on = 1; Native.DwmSetWindowAttribute(new WIn.WindowInteropHelper(win).Handle, 20, ref on, 4);
        };
        win.Closing += (s, e) => { if (Core.Quitting) return; e.Cancel = true; win.Hide(); Core.HiddenToTray(); };

        history = N<WC.ListBox>("History"); activeLine = N<WC.TextBlock>("ActiveLine"); empty = N<WC.TextBlock>("Empty");
        saveHk = N<WC.TextBlock>("SaveHk"); rearrangeHk = N<WC.TextBlock>("RearrangeHk"); undoHk = N<WC.TextBlock>("UndoHk");
        renamePanel = N<WC.DockPanel>("RenamePanel"); renameBox = N<WC.TextBox>("RenameBox");
        useBtn = N<WC.Button>("UseBtn"); renameBtn = N<WC.Button>("RenameBtn"); deleteBtn = N<WC.Button>("DeleteBtn");
        cAuto = N<WC.CheckBox>("CAuto"); cExtras = N<WC.CheckBox>("CExtras"); cAnim = N<WC.CheckBox>("CAnim");
        cSound = N<WC.CheckBox>("CSound"); cLogon = N<WC.CheckBox>("CLogon"); vol = N<WC.Slider>("Vol"); volText = N<WC.TextBlock>("VolText");

        N<WC.Button>("SaveBtn").Click += delegate { Core.SaveNow(); };
        N<WC.Button>("RearrangeBtn").Click += delegate { Core.RearrangeActive(); };
        N<WC.Button>("UndoBtn").Click += delegate { Core.Undo(); };
        N<WC.Button>("CloseBtn").Click += delegate { win.Close(); };
        N<WC.Button>("QuitBtn").Click += delegate { Core.Quit(); };
        useBtn.Click += delegate { Core.UseSort(Selected()); };
        history.MouseDoubleClick += delegate { if (Selected() != null) Core.UseSort(Selected()); };
        history.SelectionChanged += delegate { UpdateButtons(); renamePanel.Visibility = W.Visibility.Collapsed; };
        deleteBtn.Click += delegate { Core.DeleteSort(Selected()); };
        renameBtn.Click += delegate {
            var s = Selected(); if (s == null) return;
            renameBox.Text = s.Name; renamePanel.Visibility = W.Visibility.Visible; renameBox.Focus(); renameBox.SelectAll();
        };
        Action commit = () => {
            var s = Selected(); if (s == null) return;
            Store.Rename(s, renameBox.Text); renamePanel.Visibility = W.Visibility.Collapsed; Refresh();
        };
        N<WC.Button>("RenameOk").Click += delegate { commit(); };
        renameBox.KeyDown += (s, e) => {
            if (e.Key == WI.Key.Enter) commit();
            else if (e.Key == WI.Key.Escape) renamePanel.Visibility = W.Visibility.Collapsed;
        };

        N<WC.TextBlock>("Ver").Text = "NJ-Sort " + App.Version;
        BuildHotkeyRows();

        W.RoutedEventHandler opt = delegate {
            if (loading) return;
            var c = Core.Cfg;
            c.AutoRestore = cAuto.IsChecked == true; c.SortExtras = cExtras.IsChecked == true;
            c.Animate = cAnim.IsChecked == true; c.Sound = cSound.IsChecked == true;
            vol.IsEnabled = c.Sound;
            c.Write();
        };
        foreach (var cb in new[] { cAuto, cExtras, cAnim, cSound }) { cb.Checked += opt; cb.Unchecked += opt; }
        W.RoutedEventHandler logon = delegate {
            if (loading) return;
            try { Startup.Set(cLogon.IsChecked == true); } catch (Exception e) { App.Log("startup shortcut: " + e.Message); }
        };
        cLogon.Checked += logon; cLogon.Unchecked += logon;
        vol.ValueChanged += delegate {
            volText.Text = (int)vol.Value + "%";
            if (loading) return;
            Core.Cfg.Volume = (int)vol.Value; Core.Cfg.Write();
        };
        vol.PreviewMouseUp += delegate { Sfx.Play("sort"); };   // hear the new volume
    }

    void BuildHotkeyRows() {
        var rows = N<WC.StackPanel>("HkRows");
        var fieldStyle = (W.Style)win.FindResource("HkBox"); var iconStyle = (W.Style)win.FindResource("IconBtn");
        for (int i = 0; i < 4; i++) {
            int id = i;
            var g = new WC.Grid { Margin = new W.Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new WC.ColumnDefinition());
            g.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = new W.GridLength(190) });
            g.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = W.GridLength.Auto });
            var label = new WC.TextBlock { Text = Settings.KeyNames[i], VerticalAlignment = W.VerticalAlignment.Center };
            var box = new WC.TextBox { Style = fieldStyle, ToolTip = "Click, then press the keys" };
            var clear = new WC.Button { Style = iconStyle, Content = "\u00D7", Margin = new W.Thickness(6, 0, 0, 0), ToolTip = "No shortcut" };
            WC.Grid.SetColumn(box, 1); WC.Grid.SetColumn(clear, 2);
            g.Children.Add(label); g.Children.Add(box); g.Children.Add(clear);
            var err = new WC.TextBlock { Foreground = new WM.SolidColorBrush(WM.Color.FromRgb(0xFF, 0x8F, 0x8F)), FontSize = 12.5, TextWrapping = W.TextWrapping.Wrap,
                                         Visibility = W.Visibility.Collapsed, Margin = new W.Thickness(0, 0, 0, 4), TextAlignment = W.TextAlignment.Right };
            rows.Children.Add(g); rows.Children.Add(err);
            hkBox[i] = box; hkErr[i] = err;

            box.GotKeyboardFocus += delegate { Core.SuspendHotkeys(); box.Text = "Press the new keys\u2026"; err.Visibility = W.Visibility.Collapsed; };
            box.LostKeyboardFocus += delegate { Core.RegisterAll(); Refresh(); };
            box.PreviewKeyDown += (s, e) => Capture(id, e);
            clear.Click += delegate { Core.Cfg.Keys[id] = new Hk(); Core.Cfg.Write(); Core.RegisterAll(); Refresh(); };
        }
    }

    static readonly WI.Key[] ModKeys = { WI.Key.LeftCtrl, WI.Key.RightCtrl, WI.Key.LeftAlt, WI.Key.RightAlt, WI.Key.LeftShift, WI.Key.RightShift, WI.Key.LWin, WI.Key.RWin };

    void Capture(int id, WI.KeyEventArgs e) {
        e.Handled = true;
        var box = hkBox[id];
        var k = e.Key == WI.Key.System ? e.SystemKey : e.Key;
        uint mods = (uint)WI.Keyboard.Modifiers & 7;
        if (WI.Keyboard.IsKeyDown(WI.Key.LWin) || WI.Keyboard.IsKeyDown(WI.Key.RWin)) mods |= 8;
        if (k == WI.Key.Escape) { Leave(); return; }
        if (ModKeys.Contains(k)) { box.Text = ModText(mods) + "\u2026"; return; }
        if ((k == WI.Key.Back || k == WI.Key.Delete) && mods == 0) { Core.Cfg.Keys[id] = new Hk(); Core.Cfg.Write(); Leave(); return; }
        int vk = WI.KeyInterop.VirtualKeyFromKey(k);
        bool fkey = vk >= 0x70 && vk <= 0x87;
        if (mods == 0 && !fkey) { box.Text = "Add Ctrl, Alt, Shift or Win"; return; }
        var hk = new Hk { Mods = mods, Vk = vk };
        for (int j = 0; j < 4; j++)
            if (j != id && Core.Cfg.Keys[j].Same(hk)) { box.Text = "Used by " + Settings.KeyNames[j]; return; }
        Core.Cfg.Keys[id] = hk; Core.Cfg.Write();
        Leave();
    }
    static string ModText(uint m) {
        var p = new List<string>();
        if ((m & 8) != 0) p.Add("Win"); if ((m & 2) != 0) p.Add("Ctrl"); if ((m & 1) != 0) p.Add("Alt"); if ((m & 4) != 0) p.Add("Shift");
        return p.Count == 0 ? "" : string.Join("+", p) + "+";
    }
    void Leave() { WI.Keyboard.ClearFocus(); win.Focus(); }

    Sort Selected() { var r = history.SelectedItem as SortRow; return r == null ? null : Store.Find(r.Id); }
    void UpdateButtons() { bool on = history.SelectedItem != null; useBtn.IsEnabled = on; renameBtn.IsEnabled = on; deleteBtn.IsEnabled = on; }

    static string When(DateTime t) {
        var now = DateTime.Now; var ago = now - t;
        if (ago.TotalMinutes < 1) return "just now";
        if (ago.TotalMinutes < 60) return (int)ago.TotalMinutes + " min ago";
        if (t.Date == now.Date) return "today, " + t.ToString("HH:mm");
        if (t.Date == now.Date.AddDays(-1)) return "yesterday, " + t.ToString("HH:mm");
        return t.ToString("d MMM yyyy, HH:mm");
    }

    public void Refresh() {
        loading = true;
        try {
            var c = Core.Cfg; var act = Core.Active;
            string keepId = history.SelectedItem is SortRow ? ((SortRow)history.SelectedItem).Id : null;
            var rows = Store.All.Select(s => new SortRow {
                Id = s.Id, Title = s.Title, Detail = When(s.Saved) + "  \u00B7  " + s.Icons.Count + " items",
                ActiveVis = act != null && s.Id == act.Id ? W.Visibility.Visible : W.Visibility.Collapsed }).ToList();
            history.ItemsSource = rows;
            history.SelectedItem = rows.FirstOrDefault(r => r.Id == keepId);
            empty.Visibility = rows.Count == 0 ? W.Visibility.Visible : W.Visibility.Collapsed;
            UpdateButtons();

            if (act == null) activeLine.Text = "No sort saved yet. Arrange your icons the way you like, then press Save sort now.";
            else {
                int extra = 0;
                if (Desktop.Valid) { var keys = new HashSet<string>(act.Icons.Select(i => i.Key)); extra = Desktop.Read().Count(i => !keys.Contains(i.Key)); }
                activeLine.Text = "Active sort: \u201C" + act.Title + "\u201D, " + act.Icons.Count + " items." +
                    (extra == 0 ? " Every item on the desktop is in it." : " " + extra + (extra == 1 ? " item is" : " items are") + " not in it; they follow it, sorted by name.");
            }
            saveHk.Text = c.Keys[0].Empty ? "no shortcut" : c.Keys[0].ToString();
            rearrangeHk.Text = c.Keys[1].Empty ? "no shortcut" : c.Keys[1].ToString();
            undoHk.Text = c.Keys[2].Empty ? "no shortcut" : c.Keys[2].ToString();
            for (int i = 0; i < 4; i++) {
                if (!hkBox[i].IsKeyboardFocused) hkBox[i].Text = c.Keys[i].ToString();
                hkErr[i].Text = Core.HkError[i] ?? ""; hkErr[i].Visibility = Core.HkError[i] == null ? W.Visibility.Collapsed : W.Visibility.Visible;
            }
            cAuto.IsChecked = c.AutoRestore; cExtras.IsChecked = c.SortExtras; cAnim.IsChecked = c.Animate; cSound.IsChecked = c.Sound;
            cLogon.IsChecked = Startup.IsOn(); vol.Value = c.Volume; vol.IsEnabled = c.Sound; volText.Text = c.Volume + "%";
        } finally { loading = false; }
    }

    // selftest: apply every template off-screen and flip a switch so its storyboard runs
    public void LayoutTest() {
        win.WindowStartupLocation = W.WindowStartupLocation.Manual; win.Left = -20000; win.Top = 0; win.ShowActivated = false;
        win.Show(); win.UpdateLayout();
        bool was = cSound.IsChecked == true; loading = true; cSound.IsChecked = !was; win.UpdateLayout(); cSound.IsChecked = was; loading = false;
        if (history.Items.Count > 0) { history.SelectedIndex = 0; win.UpdateLayout(); }
        Core.QuittingForTest(); win.Close();
    }

    public void ShowUp() {
        Refresh();
        if (!win.IsVisible) win.Show();
        if (win.WindowState == W.WindowState.Minimized) win.WindowState = W.WindowState.Normal;
        win.Activate(); win.Topmost = true; win.Topmost = false;
    }
}

// ---------------------------------------------------------------- start with Windows
static class Startup {
    static string Lnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "NJ-Sort.lnk"); } }
    public static bool IsOn() { return File.Exists(Lnk); }
    public static void Set(bool on) {
        if (!on) { if (File.Exists(Lnk)) File.Delete(Lnk); return; }
        var t = Type.GetTypeFromProgID("WScript.Shell");
        object sh = Activator.CreateInstance(t);
        object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { Lnk });
        var lt = lnk.GetType();
        lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { App.HomeExe });
        lt.InvokeMember("Arguments", BindingFlags.SetProperty, null, lnk, new object[] { "--tray" });
        lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { App.Dir });
        lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
    }
}

static class App {
    public const string Version = "1.0.0";
    // where NJ-Sort lives and keeps its data: NJSORT_HOME when set, else %LOCALAPPDATA%\NJ-Sort
    public static readonly string Dir = HomeDir();
    static string HomeDir() {
        string env = Environment.GetEnvironmentVariable("NJSORT_HOME");
        string d = !string.IsNullOrWhiteSpace(env) ? env.Trim() : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NJ-Sort");
        try { Directory.CreateDirectory(d); } catch { }
        return d;
    }
    public static string HomeExe { get { return Path.Combine(Dir, "NJ-Sort.exe"); } }
    static string LogFile { get { return Path.Combine(Dir, "nj-sort.log"); } }

    public static void Log(string msg) {
        try {
            if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 300000) File.Delete(LogFile);
            File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8);
        } catch { }
    }

    public static void WriteAtomic(string path, IEnumerable<string> lines) {
        string tmp = path + ".tmp";
        File.WriteAllLines(tmp, lines, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
    }

    [STAThread]
    static int Main(string[] args) {
        string a = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        bool tray = a == "--tray" || a == "--keeper";   // --keeper: the old startup shortcut
        if (a == "list") {
            Desktop.Lv = Desktop.Find();
            var o = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)); o.AutoFlush = true;
            foreach (var i in Desktop.Read()) o.WriteLine(i.X + "\t" + i.Y + "\t" + i.Name);
            return 0;
        }
        if (a == "--selftest") {   // gate: both windows parse, bind and lay out; exit 0 or 1, nothing is moved
            try {
                Core.Cfg = Settings.Load(); Store.Reload(); Desktop.Lv = Desktop.Find();
                var mw = new MainWin(); mw.Refresh(); mw.LayoutTest(); Toast.SelfTest();
                Console.Out.Flush(); return 0;
            } catch (Exception e) { File.WriteAllText(Path.Combine(Dir, "selftest.err"), e.ToString()); return 1; }
        }
        if (a == "--render" && args.Length > 1) return Render.Run(args[1]);
        // A copy anywhere else (Downloads, the Desktop) installs or updates the one in the home folder, then hands over to it,
        // so only one NJ-Sort ever runs and the sign-in shortcut always points at the same file.
        string me = Assembly.GetExecutingAssembly().Location;
        if (!string.Equals(Path.GetFullPath(me), Path.GetFullPath(HomeExe), StringComparison.OrdinalIgnoreCase)) {
            try { Install(me); } catch (Exception e) { Log("install: " + e.Message); }
            if (File.Exists(HomeExe)) {
                Process.Start(new ProcessStartInfo(HomeExe, tray ? "--tray" : "") { WorkingDirectory = Dir, UseShellExecute = false });
                return 0;
            }
        }
        bool first; var mutex = new Mutex(true, "NJSort.App", out first);
        if (!first) {   // already running: open its window
            if (!tray) try { EventWaitHandle.OpenExisting("NJSort.Show").Set(); } catch { }
            return 0;
        }
        StopOldKeeper();
        Native.SetProcessDPIAware();
        F.Application.EnableVisualStyles();
        var app = new W.Application { ShutdownMode = W.ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (s, e) => { Log("unhandled: " + e.Exception); e.Handled = true; };
        app.Startup += delegate { Core.Start(!tray); };
        app.Run();
        GC.KeepAlive(mutex);
        return 0;
    }

    // copies this exe to the home folder when it is missing or different; a running older copy is asked to quit first
    static void Install(string me) {
        if (File.Exists(HomeExe) && SameFile(me, HomeExe)) return;
        bool running = false; Mutex m;
        if (Mutex.TryOpenExisting("NJSort.App", out m)) { m.Dispose(); running = true; }
        if (running) {
            try { EventWaitHandle.OpenExisting("NJSort.Quit").Set(); } catch { }
            for (int i = 0; i < 50; i++) { Thread.Sleep(100); if (!Mutex.TryOpenExisting("NJSort.App", out m)) break; m.Dispose(); }
        }
        File.Copy(me, HomeExe, true);
        Log("installed " + Version + " in " + Dir);
    }
    static bool SameFile(string a, string b) {
        if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(a))) == Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(b)));
    }

    // the old NJ-Sort keeper (separate process, mutex "DesktopLayoutKeeper") would fight over the desktop and the shortcut
    static void StopOldKeeper() {
        Mutex m;
        if (!Mutex.TryOpenExisting("DesktopLayoutKeeper", out m)) return;
        m.Dispose();
        int self = Process.GetCurrentProcess().Id;
        foreach (var name in new[] { "NJ-Sort", "DesktopLayout" })
            foreach (var p in Process.GetProcessesByName(name)) {
                if (p.Id == self) continue;
                try { p.Kill(); p.WaitForExit(3000); Log("stopped the old keeper (pid " + p.Id + ")"); } catch { }
            }
    }
}

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