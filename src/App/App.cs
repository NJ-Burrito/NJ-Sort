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
// CLI: NJ-Sort.exe --tray (start in the hidden icons, used at sign-in) | list | --selftest | --render <folder> (README images) | --strings <file>
// Language: settings.ini language=auto|en|ar (Text/Strings.cs); NJSORT_LANG=ar|en overrides it, for off-screen renders only.
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
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

static class App {
    public const string Version = "1.1.0";
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
                var faults = Tx.Check();   // the string table: both sides filled, placeholders match, Arabic clean, plural forms for 0..120
                if (faults.Count > 0) { File.WriteAllText(Path.Combine(Dir, "selftest.err"), string.Join(Environment.NewLine, faults)); return 1; }
                foreach (bool ar in new[] { false, true }) {   // both windows and both toasts lay out in both languages
                    Tx.Ar = ar; Toast.win = null; var mw = new MainWin(); mw.Refresh(); mw.LayoutTest(); Toast.SelfTest();
                }
                Console.Out.Flush(); return 0;
            } catch (Exception e) { File.WriteAllText(Path.Combine(Dir, "selftest.err"), e.ToString()); return 1; }
        }
        if (a == "--strings" && args.Length > 1) { Tx.Export(args[1]); return 0; }   // the string table as markdown (for the proofread pass)
        if (a == "--render" && args.Length > 1) return Render.Run(args[1]);
        if (a == "--render-motion" && args.Length > 1) return RenderMotion.Run(args[1]);   // the icon animation on a fake desktop, off screen
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
