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
    static F.ToolStripMenuItem trayOpen, traySave, trayRe, trayUndo, trayQuit; static F.ContextMenuStrip trayMenu;
    static void BuildTray() {
        D.Icon icon;
        try { icon = new D.Icon(D.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location), F.SystemInformation.SmallIconSize); }
        catch { icon = D.SystemIcons.Application; }
        tray = new F.NotifyIcon { Icon = icon, Text = "NJ-Sort", Visible = true };
        var menu = trayMenu = new F.ContextMenuStrip();
        var open = trayOpen = new F.ToolStripMenuItem("", null, delegate { OpenWindow(); }) { Font = new D.Font(F.SystemInformation.MenuFont, D.FontStyle.Bold) };
        var save = traySave = new F.ToolStripMenuItem("", null, delegate { SaveNow(); });
        var re = trayRe = new F.ToolStripMenuItem("", null, delegate { RearrangeActive(); });
        var undo = trayUndo = new F.ToolStripMenuItem("", null, delegate { Undo(); });
        var quit = trayQuit = new F.ToolStripMenuItem("", null, delegate { Quit(); });
        menu.Items.AddRange(new F.ToolStripItem[] { open, new F.ToolStripSeparator(), save, re, undo, new F.ToolStripSeparator(), quit });
        trayItems = new[] { save, re, undo, open };
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (o, e) => { if (e.Button == F.MouseButtons.Left) OpenWindow(); };
        UpdateTray();
    }
    static void UpdateTray() {
        if (tray == null) return;
        // the menu and the tooltip in the language in force (also called when the language changes)
        trayOpen.Text = Tx.T("tray.open"); traySave.Text = Tx.T("tray.save"); trayRe.Text = Tx.T("tray.rearrange");
        trayUndo.Text = Tx.T("tray.undo"); trayQuit.Text = Tx.T("tray.quit");
        trayMenu.RightToLeft = Tx.Ar ? F.RightToLeft.Yes : F.RightToLeft.No;
        for (int i = 0; i < 4; i++) trayItems[i].ShortcutKeyDisplayString = Cfg.Keys[i].Empty ? "" : Cfg.Keys[i].ToString();
        string t = Cfg.Keys[0].Empty || Cfg.Keys[1].Empty ? "NJ-Sort" : Tx.F("tray.tip", Cfg.Keys[0], Cfg.Keys[1]);
        tray.Text = t.Length > 63 ? t.Substring(0, 63) : t;
    }

    // the language setting changed: the tray menu and tooltip at once, the window rebuilt in place, the next toast follows
    public static void SetLanguage(string code) {
        if (Cfg.Language == code) return;
        Cfg.Language = code; Cfg.Write();
        Tx.Resolve();
        RegisterAll();   // re-words the "taken by another program" notes and the tray
        if (main != null) { var old = main; main = new MainWin(); main.TakeOver(old); }
    }

    public static void OpenWindow() {
        if (quitting) return;
        if (main == null) main = new MainWin();
        main.ShowUp();
    }

    public static void HiddenToTray() {
        if (Cfg.TipShown) return;
        Cfg.TipShown = true; Cfg.Write();
        tray.ShowBalloonTip(5000, Tx.T("tray.still.h"), Tx.T("tray.still.b"), F.ToolTipIcon.Info);
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
