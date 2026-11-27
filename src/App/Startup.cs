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

// ---------------------------------------------------------------- start with Windows
static class Startup {
    static string Lnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "NJ-Sort.lnk"); } }
    public static bool IsOn() { return File.Exists(Lnk); }
    public static void Set(bool on) {
        if (!on) { if (File.Exists(Lnk)) File.Delete(Lnk); return; }
        var t = Type.GetTypeFromProgID("WScript.Shell");
        object sh = Activator.CreateInstance(t);
        object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { Lnk });
        var lt = lnk.GetType();
        lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { App.HomeExe });
        lt.InvokeMember("Arguments", BindingFlags.SetProperty, null, lnk, new object[] { "--tray" });
        lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { App.Dir });
        lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
    }
}
