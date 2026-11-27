// NJ-Sort: the actions: save, rearrange, undo, use a sort.
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
    // ---------- actions
    public static void SaveNow() {
        if (Animator.Busy || !Ready(false)) return;
        var cur = Desktop.Read();
        if (cur.Count == 0) { Toast.Show("error", Tx.T("t.nosave.h"), Tx.T("t.nosave.b")); Sfx.Play("error"); return; }
        var newest = Store.All.FirstOrDefault();
        if (newest != null && SameLayout(newest.Icons, cur)) {
            if (Cfg.Active != newest.Id) { Cfg.Active = newest.Id; Cfg.Write(); }
            Toast.Show("info", Tx.T("t.already.h"), Tx.F("t.already.b", newest.Title)); Sfx.Play("info");
            if (main != null) main.Refresh();
            return;
        }
        var s = Store.Add(cur, "", DateTime.Now);
        Cfg.Active = s.Id; Cfg.Write();
        lastKeys = Desktop.Keys(cur);
        App.Log("saved sort " + s.Id + " (" + cur.Count + " items)");
        Toast.Show("save", Tx.T("t.saved.h"), Tx.F("t.saved.b", Tx.Plural("item", cur.Count)));
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
            if (!auto) { Toast.Show("error", Tx.T("t.nosort.h"), Cfg.Keys[0].Empty ? Tx.T("t.nosort.b") : Tx.F("t.nosort.b_key", Cfg.Keys[0])); Sfx.Play("error"); }
            return;
        }
        if (!Ready(auto)) return;
        var cur = Desktop.Read();
        if (cur.Count == 0) return;
        var plan = Planner.Make(s, cur, true, null, true);
        string detail = plan.Sorted > 0 ? Tx.F("t.kept_sorted", plan.Kept, plan.Sorted) : Tx.F("t.kept", plan.Kept);
        App.Log((auto ? "auto " : "") + "rearrange to " + s.Id + ": moving " + plan.Moves.Count + " (" + plan.Kept + " in their saved place" + (plan.Sorted > 0 ? ", " + plan.Sorted + " more sorted by name after them" : "") + ")" + (why == null ? "" : " " + why));
        if (plan.Moves.Count == 0) {
            if (!auto) { Toast.Show("info", Tx.T("t.inorder.h"), Tx.F("t.sentence", detail)); Sfx.Play("info"); }
            return;
        }
        Store.WriteUndo(cur);
        Run(plan, delegate { if (auto) recheckAt = DateTime.Now.AddSeconds(4); });
        Toast.Show("sort", auto ? Tx.T("t.autoput.h") : Tx.T("t.rearranged.h"), auto ? Tx.F("t.reason", Tx.Capital(Tx.Why(why)), detail) : Tx.F("t.sentence", detail));
        Sfx.Play("sort");
    }

    public static void Undo() {
        if (Animator.Busy || !Ready(false)) return;
        var u = Store.ReadUndo();
        if (u == null) { Toast.Show("info", Tx.T("t.noundo.h"), null); Sfx.Play("info"); return; }
        var cur = Desktop.Read(); var plan = new Plan { EX = cur.Select(c => c.X).ToArray(), EY = cur.Select(c => c.Y).ToArray() };
        for (int i = 0; i < cur.Count; i++) {
            DIcon o;
            if (u.TryGetValue(cur[i].Key, out o) && (o.X != cur[i].X || o.Y != cur[i].Y)) {
                plan.Moves.Add(new Move { Index = i, FX = cur[i].X, FY = cur[i].Y, TX = o.X, TY = o.Y });
                plan.EX[i] = o.X; plan.EY[i] = o.Y;
            }
        }
        if (plan.Moves.Count == 0) { Toast.Show("info", Tx.T("t.noundo.h"), Tx.T("t.noundo.b")); Sfx.Play("info"); return; }
        Store.WriteUndo(cur);   // undo again = redo
        App.Log("undo: moving " + plan.Moves.Count);
        Run(plan, null);
        Toast.Show("undo", Tx.T("t.undo.h"), Tx.Plural("undone", plan.Moves.Count));
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
        try { Store.Delete(s); } catch (Exception e) { Toast.Show("error", Tx.T("t.delfail.h"), e.Message); return; }
        if (Cfg.Active == s.Id) { Cfg.Active = Store.All.Count > 0 ? Store.All[0].Id : ""; Cfg.Write(); }
        App.Log("deleted sort " + s.Id);
        Toast.Show("info", Tx.T("t.deleted.h"), Tx.F("t.deleted.b", s.Title));
        if (main != null) main.Refresh();
    }
}
