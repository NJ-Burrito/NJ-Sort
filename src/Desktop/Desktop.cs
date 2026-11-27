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
