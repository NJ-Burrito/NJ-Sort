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

// ---------------------------------------------------------------- sound: real recordings (Mixkit, see sfx/CREDITS.md), embedded, quiet
// The five tracks are mixed by make_sfx.py (peak 10% of full scale) and embedded as sort.wav, save.wav, undo.wav, info.wav, error.wav.
static class Sfx {
    static System.Media.SoundPlayer player;
    static readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();
    public static void Play(string kind) {
        var c = Core.Cfg;
        if (c == null || !c.Sound || c.Volume <= 0) return;
        try {
            if (kind != "save" && kind != "undo" && kind != "error" && kind != "info") kind = "sort";
            byte[] wav = Load(kind, c.Volume / 100.0);
            if (wav == null) return;
            if (player != null) player.Stop();
            player = new System.Media.SoundPlayer(new MemoryStream(wav));
            player.Play();
        } catch (Exception e) { App.Log("sound: " + e.Message); }
    }
    // the embedded 16-bit mono wav, its samples scaled by the volume setting (the file itself is already quiet)
    static byte[] Load(string kind, double vol) {
        byte[] src;
        if (!cache.TryGetValue(kind, out src)) {
            using (var st = Assembly.GetExecutingAssembly().GetManifestResourceStream(kind + ".wav")) {
                if (st == null) { cache[kind] = null; return null; }
                var ms = new MemoryStream(); st.CopyTo(ms); src = ms.ToArray(); cache[kind] = src;
            }
        }
        if (src == null) return null;
        var b = (byte[])src.Clone();
        int at = 12;                                    // find the "data" chunk
        while (at + 8 <= b.Length && !(b[at] == 'd' && b[at + 1] == 'a' && b[at + 2] == 't' && b[at + 3] == 'a')) at += 8 + BitConverter.ToInt32(b, at + 4);
        for (int i = at + 8; i + 1 < b.Length; i += 2) {
            short v = BitConverter.ToInt16(b, i); int o = (int)Math.Round(v * vol);
            b[i] = (byte)(o & 0xFF); b[i + 1] = (byte)((o >> 8) & 0xFF);
        }
        return b;
    }
}
