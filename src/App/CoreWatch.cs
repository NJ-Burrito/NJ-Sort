// NJ-Sort: watching the desktop and putting the active sort back.
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
}
