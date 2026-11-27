// NJ-Sort: the icon in the hidden icons.
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

static partial class Core {
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
