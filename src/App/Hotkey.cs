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
    // what the window shows: key caps stay Latin, "no shortcut" follows the language
    public string Show() { return Empty ? Tx.T("hk.none") : ToString(); }
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
