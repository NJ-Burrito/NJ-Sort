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

// ---------------------------------------------------------------- sound (synthesised, quiet)
static class Sfx {
    const int R = 44100;
    static System.Media.SoundPlayer player;
    public static void Play(string kind) {
        var c = Core.Cfg;
        if (c == null || !c.Sound || c.Volume <= 0) return;
        try {
            float[] s = kind == "save" ? SaveSnd() : kind == "undo" ? UndoSnd() : kind == "error" ? ErrSnd() : kind == "info" ? InfoSnd() : SortSnd();
            if (player != null) player.Stop();
            player = new System.Media.SoundPlayer(new MemoryStream(Wav(s, c.Volume / 100.0)));
            player.Play();
        } catch (Exception e) { App.Log("sound: " + e.Message); }
    }
    static float[] Buf(double sec) { return new float[(int)(R * sec)]; }
    // a soft mallet note: fundamental + quickly fading overtones
    static void Note(float[] b, double at, double f, double amp, double decay) {
        int s0 = (int)(at * R);
        for (int i = s0; i < b.Length; i++) {
            double t = (i - s0) / (double)R, env = Math.Min(1, t / 0.003) * Math.Exp(-t / decay);
            if (t > 0.02 && env < 1e-4) break;
            b[i] += (float)(amp * env * (Math.Sin(2 * Math.PI * f * t) + 0.3 * Math.Sin(4 * Math.PI * f * t) * Math.Exp(-t / (decay * 0.35))
                                         + 0.12 * Math.Sin(2 * Math.PI * 3.98 * f * t) * Math.Exp(-t / (decay * 0.2))));
        }
    }
    // filtered noise swelling and fading: the icons moving
    static void Whoosh(float[] b, double at, double dur, double amp, int seed) {
        var rnd = new Random(seed); double l1 = 0, l2 = 0, h = 0;
        int s0 = (int)(at * R), n = (int)(dur * R);
        for (int i = 0; i < n && s0 + i < b.Length; i++) {
            double p = i / (double)n, cut = 0.03 + 0.16 * Math.Sin(Math.PI * p);
            double x = rnd.NextDouble() * 2 - 1; l1 += cut * (x - l1); l2 += cut * (l1 - l2); h += 0.02 * (l2 - h);
            b[s0 + i] += (float)(amp * Math.Pow(Math.Sin(Math.PI * p), 2) * (l2 - h) * 3);
        }
    }
    static void Tick(float[] b, double at, double f, double amp) {
        int s0 = (int)(at * R);
        for (int i = 0; i < R * 0.03 && s0 + i < b.Length; i++) { double t = i / (double)R; b[s0 + i] += (float)(amp * Math.Exp(-t / 0.006) * Math.Sin(2 * Math.PI * f * t)); }
    }
    static float[] SortSnd() {
        var b = Buf(1.1);
        Whoosh(b, 0, 0.46, 0.9, 7);
        for (int k = 0; k < 5; k++) Tick(b, 0.07 + k * 0.055, 1700 + k * 160, 0.16);   // little landings
        Note(b, 0.34, 1318.5, 0.32, 0.22); Note(b, 0.43, 1975.5, 0.30, 0.32);           // E6, B6
        return b;
    }
    static float[] SaveSnd() {
        var b = Buf(0.9);
        Tick(b, 0, 2400, 0.35); Note(b, 0.03, 1046.5, 0.30, 0.2); Note(b, 0.11, 1568.0, 0.30, 0.36);   // C6, G6
        return b;
    }
    static float[] UndoSnd() {
        var b = Buf(0.9);
        Whoosh(b, 0, 0.32, 0.6, 11); Note(b, 0.16, 1568.0, 0.26, 0.18); Note(b, 0.25, 1046.5, 0.28, 0.3);
        return b;
    }
    static float[] InfoSnd() { var b = Buf(0.5); Note(b, 0, 1318.5, 0.22, 0.18); return b; }
    static float[] ErrSnd() { var b = Buf(0.7); Note(b, 0, 523.3, 0.3, 0.14); Note(b, 0.13, 466.2, 0.3, 0.22); return b; }

    static byte[] Wav(float[] s, double vol) {
        double peak = s.Max(x => Math.Abs(x)); if (peak < 1e-6) peak = 1;
        double g = 0.42 * vol / peak;                // never louder than 42% of full scale, scaled by the volume setting
        var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + s.Length * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(R); w.Write(R * 2); w.Write((short)2); w.Write((short)16);
        w.Write(Encoding.ASCII.GetBytes("data")); w.Write(s.Length * 2);
        foreach (var x in s) w.Write((short)Math.Max(-32767, Math.Min(32767, x * g * 32767)));
        w.Flush();
        return ms.ToArray();
    }
}
