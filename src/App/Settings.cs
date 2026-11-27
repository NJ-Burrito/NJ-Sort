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

class Settings {
    public static readonly string[] KeyIds = { "hk_save", "hk_rearrange", "hk_undo", "hk_open" };
    public static readonly string[] KeyNames = { "Save sort now", "Rearrange to the active sort", "Undo the last move", "Open NJ-Sort" };   // English: for the log
    public static string KeyName(int i) { return Tx.KeyName(i); }   // the shortcut names live in Text/Strings.cs (kn.0 .. kn.3)
    static readonly string[] Defaults = { "Alt+S", "Ctrl+Alt+R", "Ctrl+Alt+Z", "Ctrl+Alt+N" };
    public Hk[] Keys = Defaults.Select(Hk.Parse).ToArray();
    public bool AutoRestore = true, SortExtras = true, Animate = true, Sound = true, TipShown;
    public int Volume = 70;
    public string Active = "";
    public string Language = "auto";   // auto | en | ar (see Text/Strings.cs)
    static string FilePath { get { return Path.Combine(App.Dir, "settings.ini"); } }

    public static Settings Load() {
        var s = new Settings();
        try {
            if (!File.Exists(FilePath)) return s;
            uint oldMods = 0; string oldKey = null;
            foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8)) {
                int eq = line.IndexOf('='); if (eq < 0 || line.StartsWith("#")) continue;
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                int ki = Array.IndexOf(KeyIds, k);
                if (ki >= 0) s.Keys[ki] = Hk.Parse(v);
                else if (k == "auto_restore") s.AutoRestore = v != "0";
                else if (k == "sort_extras") s.SortExtras = v != "0";
                else if (k == "animate") s.Animate = v != "0";
                else if (k == "sound") s.Sound = v != "0";
                else if (k == "tip_shown") s.TipShown = v != "0";
                else if (k == "volume") { int x; if (int.TryParse(v, out x)) s.Volume = Math.Max(0, Math.Min(100, x)); }
                else if (k == "active") s.Active = v;
                else if (k == "language") { string lg = Tx.ParseLang(v); if (lg != null) s.Language = lg; }
                else if (k == "save_mods") uint.TryParse(v, out oldMods);          // the old NJ-Sort save hotkey
                else if (k == "save_key") oldKey = v;
            }
            if (oldMods != 0 && oldKey != null) { F.Keys kk; if (Enum.TryParse(oldKey, out kk)) s.Keys[0] = new Hk { Mods = oldMods, Vk = (int)kk }; }
        } catch (Exception e) { App.Log("settings: " + e.Message); }
        return s;
    }

    public void Write() {
        var lines = new List<string> { "# NJ-Sort settings (changed from the NJ-Sort window)" };
        for (int i = 0; i < KeyIds.Length; i++) lines.Add(KeyIds[i] + "=" + Keys[i]);
        lines.Add("auto_restore=" + (AutoRestore ? 1 : 0)); lines.Add("sort_extras=" + (SortExtras ? 1 : 0));
        lines.Add("animate=" + (Animate ? 1 : 0)); lines.Add("sound=" + (Sound ? 1 : 0)); lines.Add("volume=" + Volume);
        lines.Add("tip_shown=" + (TipShown ? 1 : 0)); lines.Add("active=" + Active); lines.Add("language=" + Language);
        try { App.WriteAtomic(FilePath, lines); } catch (Exception e) { App.Log("settings write: " + e.Message); }
    }
}
