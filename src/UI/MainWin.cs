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

// ---------------------------------------------------------------- the NJ-Sort window
public class SortRow {
    public string Id { get; set; }
    public string Title { get; set; }
    public string Detail { get; set; }
    public W.Visibility ActiveVis { get; set; }
}

partial class MainWin {
    // the layout and styles: MainWinXaml.cs
    internal W.Window win; Func<string, object> n;
    WC.ListBox history; WC.TextBlock activeLine, empty, saveHk, rearrangeHk, undoHk, volText;
    WC.DockPanel renamePanel; WC.TextBox renameBox;
    WC.Button useBtn, renameBtn, deleteBtn;
    WC.CheckBox cAuto, cExtras, cAnim, cSound, cLogon; WC.Slider vol;
    WC.TextBox[] hkBox = new WC.TextBox[4]; WC.TextBlock[] hkErr = new WC.TextBlock[4];
    bool loading, retired;

    // the XAML carries @key@ where text goes; the table fills it in the language in force
    static string Words(string x) { return System.Text.RegularExpressions.Regex.Replace(x, "@([a-z0-9_.]+)@", m => Tx.Xml(Tx.T(m.Groups[1].Value))); }

    T N<T>(string name) where T : class { return (T)n(name); }

    public MainWin() {
        win = (W.Window)W.Markup.XamlReader.Parse(Theme.Apply(Words(Xaml)));
        n = win.FindName;
        win.FlowDirection = Tx.Flow;                                                    // Arabic: the window mirrors; the logo, key caps and numbers stay left to right
        if (Tx.Ar) win.Language = W.Markup.XmlLanguage.GetLanguage("ar");
        try {
            var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("nj-sort.png");
            var bi = new WM.Imaging.BitmapImage(); bi.BeginInit(); bi.StreamSource = st; bi.CacheOption = WM.Imaging.BitmapCacheOption.OnLoad; bi.EndInit();
            N<WC.Image>("Logo").Source = bi; win.Icon = bi;
        } catch { }
        win.SourceInitialized += delegate {   // dark title bar
            var h = new WIn.WindowInteropHelper(win).Handle;
            int on = 1; Native.DwmSetWindowAttribute(h, 20, ref on, 4);                       // dark title bar
            int cap = Theme.Colorref(Theme.Bg), txt = Theme.Colorref(Theme.Muted);             // Windows 11: caption in the window colour (ignored on older builds)
            Native.DwmSetWindowAttribute(h, 35, ref cap, 4); Native.DwmSetWindowAttribute(h, 34, ref cap, 4); Native.DwmSetWindowAttribute(h, 36, ref txt, 4);
        };
        win.Closing += (s, e) => { if (Core.Quitting || retired) return; e.Cancel = true; win.Hide(); Core.HiddenToTray(); };

        history = N<WC.ListBox>("History"); activeLine = N<WC.TextBlock>("ActiveLine"); empty = N<WC.TextBlock>("Empty");
        saveHk = N<WC.TextBlock>("SaveHk"); rearrangeHk = N<WC.TextBlock>("RearrangeHk"); undoHk = N<WC.TextBlock>("UndoHk");
        renamePanel = N<WC.DockPanel>("RenamePanel"); renameBox = N<WC.TextBox>("RenameBox");
        useBtn = N<WC.Button>("UseBtn"); renameBtn = N<WC.Button>("RenameBtn"); deleteBtn = N<WC.Button>("DeleteBtn");
        cAuto = N<WC.CheckBox>("CAuto"); cExtras = N<WC.CheckBox>("CExtras"); cAnim = N<WC.CheckBox>("CAnim");
        cSound = N<WC.CheckBox>("CSound"); cLogon = N<WC.CheckBox>("CLogon"); vol = N<WC.Slider>("Vol"); volText = N<WC.TextBlock>("VolText");

        N<WC.Button>("SaveBtn").Click += delegate { Core.SaveNow(); };
        N<WC.Button>("RearrangeBtn").Click += delegate { Core.RearrangeActive(); };
        N<WC.Button>("UndoBtn").Click += delegate { Core.Undo(); };
        N<WC.Button>("CloseBtn").Click += delegate { win.Close(); };
        N<WC.Button>("QuitBtn").Click += delegate { Core.Quit(); };
        useBtn.Click += delegate { Core.UseSort(Selected()); };
        history.MouseDoubleClick += delegate { if (Selected() != null) Core.UseSort(Selected()); };
        history.SelectionChanged += delegate { UpdateButtons(); renamePanel.Visibility = W.Visibility.Collapsed; };
        deleteBtn.Click += delegate { Core.DeleteSort(Selected()); };
        renameBtn.Click += delegate {
            var s = Selected(); if (s == null) return;
            renameBox.Text = s.Name; renamePanel.Visibility = W.Visibility.Visible; renameBox.Focus(); renameBox.SelectAll();
        };
        Action commit = () => {
            var s = Selected(); if (s == null) return;
            Store.Rename(s, renameBox.Text); renamePanel.Visibility = W.Visibility.Collapsed; Refresh();
        };
        N<WC.Button>("RenameOk").Click += delegate { commit(); };
        renameBox.KeyDown += (s, e) => {
            if (e.Key == WI.Key.Enter) commit();
            else if (e.Key == WI.Key.Escape) renamePanel.Visibility = W.Visibility.Collapsed;
        };

        N<WC.TextBlock>("Ver").Text = Tx.Bidi("NJ-Sort " + App.Version);
        if (Tx.Ar) {   // a Latin run with a space or a % inside is read backwards by a right-to-left TextBlock, so these two read left to right
            N<WC.TextBlock>("Ver").FlowDirection = W.FlowDirection.LeftToRight;
            volText.FlowDirection = W.FlowDirection.LeftToRight; volText.TextAlignment = W.TextAlignment.Left;
        }
        if (Tx.Ar) N<WC.TextBlock>("VolLabel").Width = 96;   // "مستوى الصوت" is longer than "Volume"
        BuildHotkeyRows(); BuildLangRow();

        W.RoutedEventHandler opt = delegate {
            if (loading) return;
            var c = Core.Cfg;
            c.AutoRestore = cAuto.IsChecked == true; c.SortExtras = cExtras.IsChecked == true;
            c.Animate = cAnim.IsChecked == true; c.Sound = cSound.IsChecked == true;
            vol.IsEnabled = c.Sound;
            c.Write();
        };
        foreach (var cb in new[] { cAuto, cExtras, cAnim, cSound }) { cb.Checked += opt; cb.Unchecked += opt; }
        W.RoutedEventHandler logon = delegate {
            if (loading) return;
            try { Startup.Set(cLogon.IsChecked == true); } catch (Exception e) { App.Log("startup shortcut: " + e.Message); }
        };
        cLogon.Checked += logon; cLogon.Unchecked += logon;
        vol.ValueChanged += delegate {
            volText.Text = Tx.Pct((int)vol.Value);
            if (loading) return;
            Core.Cfg.Volume = (int)vol.Value; Core.Cfg.Write();
        };
        vol.PreviewMouseUp += delegate { Sfx.Play("sort"); };   // hear the new volume
    }

    void BuildHotkeyRows() {
        var rows = N<WC.StackPanel>("HkRows");
        var fieldStyle = (W.Style)win.FindResource("HkBox"); var iconStyle = (W.Style)win.FindResource("IconBtn");
        for (int i = 0; i < 4; i++) {
            int id = i;
            var g = new WC.Grid { Margin = new W.Thickness(0, 2, 0, 2) };
            g.ColumnDefinitions.Add(new WC.ColumnDefinition());
            g.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = new W.GridLength(176) });
            g.ColumnDefinitions.Add(new WC.ColumnDefinition { Width = W.GridLength.Auto });
            var label = new WC.TextBlock { Text = Tx.KeyName(i), FontSize = 14, VerticalAlignment = W.VerticalAlignment.Center };
            var box = new WC.TextBox { Style = fieldStyle, ToolTip = Tx.T("hk.click"), FlowDirection = W.FlowDirection.LeftToRight };
            var clear = new WC.Button { Style = iconStyle, Content = "\u00D7", Margin = new W.Thickness(4, 0, 0, 0), ToolTip = Tx.T("hk.clear") };
            WC.Grid.SetColumn(box, 1); WC.Grid.SetColumn(clear, 2);
            g.Children.Add(label); g.Children.Add(box); g.Children.Add(clear);
            var err = new WC.TextBlock { Foreground = Theme.Brush(Theme.Crit), FontSize = 12, TextWrapping = W.TextWrapping.Wrap,
                                         Visibility = W.Visibility.Collapsed, Margin = new W.Thickness(0, 0, 0, 4), TextAlignment = W.TextAlignment.Right };
            rows.Children.Add(g); rows.Children.Add(err);
            hkBox[i] = box; hkErr[i] = err;

            box.GotKeyboardFocus += delegate { Core.SuspendHotkeys(); SetBox(id, Tx.T("hk.press"), false); err.Visibility = W.Visibility.Collapsed; };
            box.LostKeyboardFocus += delegate { Core.RegisterAll(); Refresh(); };
            box.PreviewKeyDown += (s, e) => Capture(id, e);
            clear.Click += delegate { Core.Cfg.Keys[id] = new Hk(); Core.Cfg.Write(); Core.RegisterAll(); Refresh(); };
        }
    }

    static readonly WI.Key[] ModKeys = { WI.Key.LeftCtrl, WI.Key.RightCtrl, WI.Key.LeftAlt, WI.Key.RightAlt, WI.Key.LeftShift, WI.Key.RightShift, WI.Key.LWin, WI.Key.RWin };

    void Capture(int id, WI.KeyEventArgs e) {
        e.Handled = true;
        var box = hkBox[id];
        var k = e.Key == WI.Key.System ? e.SystemKey : e.Key;
        uint mods = (uint)WI.Keyboard.Modifiers & 7;
        if (WI.Keyboard.IsKeyDown(WI.Key.LWin) || WI.Keyboard.IsKeyDown(WI.Key.RWin)) mods |= 8;
        if (k == WI.Key.Escape) { Leave(); return; }
        if (ModKeys.Contains(k)) { SetBox(id, ModText(mods) + "\u2026", true); return; }
        if ((k == WI.Key.Back || k == WI.Key.Delete) && mods == 0) { Core.Cfg.Keys[id] = new Hk(); Core.Cfg.Write(); Leave(); return; }
        int vk = WI.KeyInterop.VirtualKeyFromKey(k);
        bool fkey = vk >= 0x70 && vk <= 0x87;
        if (mods == 0 && !fkey) { SetBox(id, Tx.T("hk.add_mod"), false); return; }
        var hk = new Hk { Mods = mods, Vk = vk };
        for (int j = 0; j < 4; j++)
            if (j != id && Core.Cfg.Keys[j].Same(hk)) { SetBox(id, Tx.F("hk.used", Tx.KeyShort(j)), false); return; }
        Core.Cfg.Keys[id] = hk; Core.Cfg.Write();
        Leave();
    }
    static string ModText(uint m) {
        var p = new List<string>();
        if ((m & 8) != 0) p.Add("Win"); if ((m & 2) != 0) p.Add("Ctrl"); if ((m & 1) != 0) p.Add("Alt"); if ((m & 4) != 0) p.Add("Shift");
        return p.Count == 0 ? "" : string.Join("+", p) + "+";
    }
    // a shortcut box shows key caps left to right, and its notes in the language's direction
    void SetBox(int i, string text, bool keys) { hkBox[i].FlowDirection = keys ? W.FlowDirection.LeftToRight : Tx.Flow; hkBox[i].Text = text; }
    static void KeyLine(WC.TextBlock tb, Hk h) { tb.Text = h.Empty ? Tx.T("hk.nokey") : h.ToString(); tb.FlowDirection = h.Empty ? Tx.Flow : W.FlowDirection.LeftToRight; }

    // the three language buttons in the Behaviour card (Auto, English, العربية)
    void BuildLangRow() {
        var row = N<WC.StackPanel>("LangRow"); var ghost = (W.Style)win.FindResource("Ghost");
        foreach (var code in new[] { "auto", "en", "ar" }) {
            string c = code; bool on = Core.Cfg.Language == c;
            var b = new WC.Button { Style = ghost, Content = Tx.T("lang." + c), MinHeight = 36, Margin = new W.Thickness(0, 0, 8, 0), FontSize = 13 };
            if (on) { b.BorderBrush = Theme.Brush(Theme.Acc); b.Background = Theme.Brush(Theme.Raised); b.FontWeight = W.FontWeights.Bold; }
            b.Click += delegate { Core.SetLanguage(c); };
            row.Children.Add(b);
        }
    }

    // the language changed: this window takes the place of the old one (same spot), which then closes
    public void TakeOver(MainWin old) {
        win.WindowStartupLocation = W.WindowStartupLocation.Manual;
        var ow = old.win; win.Left = ow.Left; win.Top = ow.Top;
        bool wasShown = ow.IsVisible;
        if (wasShown) ShowUp(); else Refresh();
        old.retired = true;
        win.Dispatcher.BeginInvoke(new Action(delegate { try { ow.Close(); } catch (Exception) { } }));
    }

    void Leave() { WI.Keyboard.ClearFocus(); win.Focus(); }

    Sort Selected() { var r = history.SelectedItem as SortRow; return r == null ? null : Store.Find(r.Id); }
    void UpdateButtons() { bool on = history.SelectedItem != null; useBtn.IsEnabled = on; renameBtn.IsEnabled = on; deleteBtn.IsEnabled = on; }

    public void Refresh() {
        loading = true;
        try {
            var c = Core.Cfg; var act = Core.Active;
            string keepId = history.SelectedItem is SortRow ? ((SortRow)history.SelectedItem).Id : null;
            var rows = Store.All.Select(s => new SortRow {
                Id = s.Id, Title = s.Title, Detail = Tx.F("hist.detail", Tx.When(s.Saved), Tx.Plural("item", s.Icons.Count)),
                ActiveVis = act != null && s.Id == act.Id ? W.Visibility.Visible : W.Visibility.Collapsed }).ToList();
            history.ItemsSource = rows;
            history.SelectedItem = rows.FirstOrDefault(r => r.Id == keepId);
            empty.Visibility = rows.Count == 0 ? W.Visibility.Visible : W.Visibility.Collapsed;
            UpdateButtons();

            if (act == null) activeLine.Text = Tx.T("desk.none");
            else {
                int extra = 0;
                if (Desktop.Valid) { var keys = new HashSet<string>(act.Icons.Select(i => i.Key)); extra = Desktop.Read().Count(i => !keys.Contains(i.Key)); }
                activeLine.Text = Tx.F("desk.active", act.Title, Tx.Plural("item", act.Icons.Count)) + " " + (extra == 0 ? Tx.T("desk.all") : Tx.Plural("notin", extra));
            }
            KeyLine(saveHk, c.Keys[0]); KeyLine(rearrangeHk, c.Keys[1]); KeyLine(undoHk, c.Keys[2]);
            for (int i = 0; i < 4; i++) {
                if (!hkBox[i].IsKeyboardFocused) SetBox(i, c.Keys[i].Empty ? Tx.T("hk.none") : c.Keys[i].ToString(), !c.Keys[i].Empty);
                hkErr[i].Text = Core.HkError[i] ?? ""; hkErr[i].Visibility = Core.HkError[i] == null ? W.Visibility.Collapsed : W.Visibility.Visible;
            }
            cAuto.IsChecked = c.AutoRestore; cExtras.IsChecked = c.SortExtras; cAnim.IsChecked = c.Animate; cSound.IsChecked = c.Sound;
            cLogon.IsChecked = Startup.IsOn(); vol.Value = c.Volume; vol.IsEnabled = c.Sound; volText.Text = Tx.Pct(c.Volume);
        } finally { loading = false; }
    }

    // README images: a row selected with the rename box open, and the "taken by another program" note under a shortcut
    public void RenderStates() {
        history.SelectedIndex = 1; renameBox.Text = Selected() == null ? "" : Selected().Title; renamePanel.Visibility = W.Visibility.Visible;
        Core.HkError[1] = Tx.F("hk.taken", Core.Cfg.Keys[1]); hkErr[1].Text = Core.HkError[1]; hkErr[1].Visibility = W.Visibility.Visible;
        SetBox(3, Tx.F("hk.used", Tx.KeyShort(0)), false);
        win.UpdateLayout();
    }

    // selftest: apply every template off-screen and flip a switch so its storyboard runs
    public void LayoutTest() {
        win.WindowStartupLocation = W.WindowStartupLocation.Manual; win.Left = -20000; win.Top = 0; win.ShowActivated = false;
        win.Show(); win.UpdateLayout();
        bool was = cSound.IsChecked == true; loading = true; cSound.IsChecked = !was; win.UpdateLayout(); cSound.IsChecked = was; loading = false;
        if (history.Items.Count > 0) { history.SelectedIndex = 0; win.UpdateLayout(); }
        Core.QuittingForTest(); win.Close();
    }

    public void ShowUp() {
        Refresh();
        if (!win.IsVisible) win.Show();
        if (win.WindowState == W.WindowState.Minimized) win.WindowState = W.WindowState.Normal;
        win.Activate(); win.Topmost = true; win.Topmost = false;
    }
}
