// NJ-Sort design system, shared by the window and the toast: one palette, one type scale, one spacing scale, one set of radii.
//   Colour   indigo-navy surfaces (Bg > Surface > Tile / Raised), 1 px hairlines, text Fg / Muted / Dim; one identity colour: amber (Acc),
//            used only for the primary action, the active sort, the focus ring and "on"; green only for "running", red only for danger.
//   Type     Thmanyah Sans only. Caption 11, Small 12, Body 13, Strong 14, Title 16, Page 24 (px).
//   Space    a 4 px grid: S1 4, S2 8, S3 12, S4 16, S5 24, S6 32. Radii: small 6 (chips), medium 8 (buttons, fields, rows), large 12 (cards, toast).
//   Motion   150-260 ms, ease-out, none on frequent actions; all of it drops to ~0 when Windows animations are off (Dur).
using System;
using WM = System.Windows.Media;

static class Theme {
    public const string Bg = "#11132B", Surface = "#181B3C", Tile = "#13152F", Raised = "#22265A", Field = "#0E1026",
        Line = "#2A2E62", LineStrong = "#3D4282",
        Fg = "#ECEEFF", Muted = "#A3A8D3", Dim = "#7479AE", Acc = "#FFB02E", Ink = "#181A3A",
        Ok = "#4ADE80", OkBg = "#173A33", OkFg = "#B6F2C9", Crit = "#FF8F8F", Info = "#8E95E6", Bad = "#F87171";
    public const double Caption = 11, Small = 12, Body = 13, Strong = 14, Title = 16, Page = 24;
    public const double S1 = 4, S2 = 8, S3 = 12, S4 = 16, S5 = 24, S6 = 32;
    public const double RSm = 6, RMd = 8, RLg = 12;

    public static WM.Color Col(string hex) { return (WM.Color)WM.ColorConverter.ConvertFromString(hex); }
    public static WM.SolidColorBrush Brush(string hex) { var b = new WM.SolidColorBrush(Col(hex)); b.Freeze(); return b; }
    // Windows COLORREF (0x00BBGGRR) for the title bar
    public static int Colorref(string hex) { var c = Col(hex); return c.R | (c.G << 8) | (c.B << 16); }

    // animations on at all? (Windows setting; the toast also asks the user's own Animate switch)
    public static bool Motion { get { return System.Windows.SystemParameters.ClientAreaAnimation; } }
    static string Dur(int ms) { return "0:0:0." + (Motion ? ms : 1).ToString("000"); }

    // fills the ~TOKEN~ placeholders of a XAML string
    public static string Apply(string x) {
        return x.Replace("~BG~", Bg).Replace("~SURFACE~", Surface).Replace("~TILE~", Tile).Replace("~RAISED~", Raised).Replace("~FIELD~", Field)
            .Replace("~LINE2~", LineStrong).Replace("~LINE~", Line).Replace("~FG~", Fg).Replace("~MUTED~", Muted).Replace("~DIM~", Dim)
            .Replace("~ACC~", Acc).Replace("~INK~", Ink).Replace("~OKBG~", OkBg).Replace("~OKFG~", OkFg).Replace("~OK~", Ok).Replace("~CRIT~", Crit)
            .Replace("~D150~", Dur(150)).Replace("~D200~", Dur(200));
    }
}
