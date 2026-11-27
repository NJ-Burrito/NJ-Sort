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

// ---------------------------------------------------------------- the toast that pops up after a sort
static class Toast {
    const string Xaml = @"
<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        WindowStyle='None' AllowsTransparency='True' Background='Transparent' Topmost='True' ShowInTaskbar='False'
        ShowActivated='False' Focusable='False' SizeToContent='WidthAndHeight' ResizeMode='NoResize' IsHitTestVisible='False'>
  <Grid x:Name='Root' Margin='30' Opacity='0' RenderTransformOrigin='0.5,1'>
    <Grid.RenderTransform><TransformGroup>
      <ScaleTransform x:Name='Sc' ScaleX='0.92' ScaleY='0.92'/><TranslateTransform x:Name='Ty' Y='18'/><TranslateTransform x:Name='Sx'/>
    </TransformGroup></Grid.RenderTransform>
    <Border CornerRadius='18' Background='#F5161838' BorderBrush='#40FFB02E' BorderThickness='1' Padding='14,12,24,12' MinWidth='300'>
      <Border.Effect><DropShadowEffect BlurRadius='30' ShadowDepth='8' Direction='270' Opacity='0.55'/></Border.Effect>
      <StackPanel Orientation='Horizontal'>
        <Grid Width='42' Height='42'>
          <Ellipse x:Name='Disc' Fill='#FFB02E' RenderTransformOrigin='0.5,0.5'>
            <Ellipse.RenderTransform><ScaleTransform x:Name='DiscSc'/></Ellipse.RenderTransform>
          </Ellipse>
          <Path x:Name='Mark' Stroke='#161838' StrokeThickness='3.2' StrokeStartLineCap='Round' StrokeEndLineCap='Round'
                StrokeLineJoin='Round' StrokeDashArray='24 24' StrokeDashOffset='24' Width='42' Height='42'/>
        </Grid>
        <StackPanel Margin='14,0,0,0' VerticalAlignment='Center'>
          <TextBlock x:Name='Title' FontFamily='Thmanyah Sans, Segoe UI' FontSize='17' FontWeight='Bold' Foreground='#ECEEFF'/>
          <TextBlock x:Name='Sub' FontFamily='Thmanyah Sans, Segoe UI' FontSize='13' Foreground='#AAAED6' Margin='0,2,0,0' MaxWidth='460' TextWrapping='Wrap'/>
        </StackPanel>
      </StackPanel>
    </Border>
  </Grid>
</Window>";
    static readonly Dictionary<string, string> Glyphs = new Dictionary<string, string> {
        { "save",  "M13,21.5 L18.5,27 L29,15.5" },                                  // check
        { "sort",  "M12,14.5 L30,14.5 M12,21 L25,21 M12,27.5 L20,27.5" },           // sorted lines
        { "undo",  "M16,13.5 L11,18.5 L16,23.5 M11,18.5 L24,18.5 C34,18.5 34,29 24,29 L18,29" },
        { "info",  "M21,13 L21,13.2 M21,19 L21,29" },
        { "error", "M21,12.5 L21,23 M21,29 L21,29.2" } };
    internal static W.Window win; static WT.DispatcherTimer hold; static int gen;
    static W.UIElement root; static WM.ScaleTransform sc, discSc; static WM.TranslateTransform ty, sx;
    static W.Shapes.Path mark; static W.Shapes.Ellipse disc; static WC.TextBlock title, sub;

    static void Build() {
        win = (W.Window)W.Markup.XamlReader.Parse(Xaml);
        Func<string, object> n = win.FindName;
        root = (W.UIElement)n("Root"); sc = (WM.ScaleTransform)n("Sc"); ty = (WM.TranslateTransform)n("Ty"); sx = (WM.TranslateTransform)n("Sx");
        discSc = (WM.ScaleTransform)n("DiscSc"); disc = (W.Shapes.Ellipse)n("Disc"); mark = (W.Shapes.Path)n("Mark");
        title = (WC.TextBlock)n("Title"); sub = (WC.TextBlock)n("Sub");
        win.SourceInitialized += delegate {   // click-through, never takes focus, not in Alt+Tab
            var h = new WIn.WindowInteropHelper(win).Handle;
            Native.SetWindowLong(h, -20, Native.GetWindowLong(h, -20) | 0x20 | 0x80 | 0x08000000);
        };
        hold = new WT.DispatcherTimer();
        hold.Tick += delegate { hold.Stop(); Out(); };
    }

    static WA.DoubleAnimation A(double? from, double to, int ms, WA.IEasingFunction e, int delay = 0) {
        var a = new WA.DoubleAnimation { To = to, Duration = TimeSpan.FromMilliseconds(ms), EasingFunction = e, BeginTime = TimeSpan.FromMilliseconds(delay) };
        if (from.HasValue) a.From = from.Value;
        return a;
    }
    static void S2(WM.ScaleTransform s, WA.AnimationTimeline a) { s.BeginAnimation(WM.ScaleTransform.ScaleXProperty, a); s.BeginAnimation(WM.ScaleTransform.ScaleYProperty, a); }

    public static void Show(string kind, string head, string detail) {
        try {
            if (win == null) Build();
            gen++;
            title.Text = head; sub.Text = detail ?? ""; sub.Visibility = string.IsNullOrEmpty(detail) ? W.Visibility.Collapsed : W.Visibility.Visible;
            string g; if (!Glyphs.TryGetValue(kind, out g)) g = Glyphs["info"];
            mark.Data = WM.Geometry.Parse(g);
            bool bad = kind == "error";
            disc.Fill = new WM.SolidColorBrush(bad ? WM.Color.FromRgb(0xF8, 0x71, 0x71) : kind == "info" ? WM.Color.FromRgb(0x8E, 0x95, 0xE6) : WM.Color.FromRgb(0xFF, 0xB0, 0x2E));
            mark.Stroke = new WM.SolidColorBrush(WM.Color.FromRgb(0x16, 0x18, 0x38));
            if (!win.IsVisible) win.Show();
            win.UpdateLayout();
            var wa = W.SystemParameters.WorkArea;
            win.Left = wa.Left + (wa.Width - win.ActualWidth) / 2; win.Top = wa.Bottom - win.ActualHeight - 8;

            bool motion = W.SystemParameters.ClientAreaAnimation && Core.Cfg.Animate;
            var back = new WA.BackEase { Amplitude = 0.45, EasingMode = WA.EasingMode.EaseOut };
            var cout = new WA.CubicEase { EasingMode = WA.EasingMode.EaseOut };
            int d = motion ? 1 : 0;
            root.BeginAnimation(W.UIElement.OpacityProperty, A(null, 1, 160 * d + 1, null));
            ty.BeginAnimation(WM.TranslateTransform.YProperty, A(motion ? 18 : 0, 0, 420 * d + 1, back));
            S2(sc, A(motion ? 0.92 : 1, 1, 420 * d + 1, back));
            S2(discSc, A(motion ? 0.3 : 1, 1, 460 * d + 1, new WA.BackEase { Amplitude = 0.8, EasingMode = WA.EasingMode.EaseOut }, 60 * d));
            mark.BeginAnimation(W.Shapes.Shape.StrokeDashOffsetProperty, A(motion ? 24 : 0, 0, 300 * d + 1, cout, 230 * d));
            if (bad && motion)
                sx.BeginAnimation(WM.TranslateTransform.XProperty, new WA.DoubleAnimation(-6, 6, TimeSpan.FromMilliseconds(55)) {
                    AutoReverse = true, RepeatBehavior = new WA.RepeatBehavior(3), BeginTime = TimeSpan.FromMilliseconds(180), FillBehavior = WA.FillBehavior.Stop });
            hold.Stop(); hold.Interval = TimeSpan.FromMilliseconds(bad ? 3600 : 2200); hold.Start();
        } catch (Exception e) { App.Log("toast: " + e.Message); }
    }

    // README images: the toast at rest, rendered off-screen
    public static W.UIElement RenderPose(string kind, string head, string detail) {
        if (win == null) Build();
        title.Text = head; sub.Text = detail ?? ""; mark.Data = WM.Geometry.Parse(Glyphs[kind]);
        disc.Fill = new WM.SolidColorBrush(WM.Color.FromRgb(0xFF, 0xB0, 0x2E));
        root.Opacity = 1; sc.ScaleX = sc.ScaleY = 1; ty.Y = 0; discSc.ScaleX = discSc.ScaleY = 1; mark.StrokeDashOffset = 0;
        win.Left = -20000; win.Show(); win.UpdateLayout();
        return (W.UIElement)win.Content;
    }

    public static void SelfTest() {
        Build(); foreach (var g in Glyphs.Values) WM.Geometry.Parse(g);
        win.Left = -20000; win.Show(); win.UpdateLayout(); win.Close();
    }

    static void Out() {
        int my = gen;
        bool motion = W.SystemParameters.ClientAreaAnimation && Core.Cfg.Animate;
        var fade = A(null, 0, motion ? 260 : 1, new WA.QuadraticEase { EasingMode = WA.EasingMode.EaseIn });
        fade.Completed += delegate { if (my == gen) win.Hide(); };
        root.BeginAnimation(W.UIElement.OpacityProperty, fade);
        if (motion) ty.BeginAnimation(WM.TranslateTransform.YProperty, A(null, 10, 260, new WA.QuadraticEase { EasingMode = WA.EasingMode.EaseIn }));
    }
}
