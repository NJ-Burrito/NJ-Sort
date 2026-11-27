// NJ-Sort: the global shortcuts.
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
}
