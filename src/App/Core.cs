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

// ---------------------------------------------------------------- the program
static partial class Core {
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
}
