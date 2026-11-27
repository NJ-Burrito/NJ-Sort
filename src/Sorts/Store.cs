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

// ---------------------------------------------------------------- saved sorts (history)
public class Sort {
    public string Id, Name = "";
    public DateTime Saved;
    public List<DIcon> Icons = new List<DIcon>();
    public string Title { get { return string.IsNullOrEmpty(Name) ? Tx.SortTitle(Saved) : Name; } }
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
        Add(icons, Tx.T("imported"), File.GetLastWriteTime(old));
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
