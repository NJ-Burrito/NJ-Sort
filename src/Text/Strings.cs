// NJ-Sort strings, English and Arabic (Nader 2026-11-28: "add an Arabic version, the app itself, fully, right-to-left", as NJ-Flush 1.3 got).
// One table: key -> (English, Arabic). Every word the user can read goes through Tx.T(key) / Tx.F(key, args); nothing else holds copy.
//
// Language: settings.ini `language=auto|en|ar` (auto = Arabic when the Windows display language is Arabic, else English), chosen in the
//   Behaviour card of the window ("Language / اللغة"), applied live: the window is rebuilt, the tray menu is reset, the next toast follows.
//   The environment variable NJSORT_LANG=ar|en|auto wins over settings.ini; it exists only so off-screen renders (--render) can show a
//   language without touching the owner's live settings file.
// Numbers: always Western digits (0-9), formatted with the invariant culture, with Arabic text around them. Units stay Latin (%, HH:mm).
// Counts: Arabic needs one / two / 3-10 / 11+ forms, so a counted noun goes through Tx.Plural(noun, n); elsewhere copy uses "label: N".
// Dates: English "29 Nov 2026, 14:30"; Arabic "29 تشرين الثاني 2026، 14:30" (Levantine month names, Arabic day names, Western digits).
// Glossary (the Arabic side; one word per thing, used everywhere; it matches docs/ar/index.html):
//   sort (a saved layout) = ترتيب            active sort = الترتيب النشط           save sort now = حفظ الترتيب الآن
//   rearrange = إعادة الترتيب                undo = تراجع                         sort history = سجل الترتيبات
//   desktop = سطح المكتب                     icon = أيقونة                        item = عنصر
//   hidden icons (tray) = الأيقونات المخفية  shortcut = اختصار                    Explorer = مستكشف الملفات
//   Recycle Bin = سلة المحذوفات              quit (stop the program) = إنهاء       close (window) = إغلاق
//   Windows = ويندوز                         Auto arrange icons = ترتيب الأيقونات تلقائيًا (the Windows menu item)
// Product names (NJ-Sort, NJ-Flush) and key caps (Ctrl+Alt+R, Backspace) stay Latin.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using W = System.Windows;

static class Tx {
    public const string EnvVar = "NJSORT_LANG";
    public static bool Ar;   // the language in force, set by Resolve()

    // "auto" | "en" | "ar", or null when the text is none of them
    public static string ParseLang(string v) {
        v = (v ?? "").Trim().ToLowerInvariant();
        return v == "auto" || v == "en" || v == "ar" ? v : null;
    }
    public static bool ChoosesArabic(string lang, bool windowsIsArabic) { return lang == "ar" || (lang == "auto" && windowsIsArabic); }
    // the environment variable first, then settings.ini; call after Settings.Load() and whenever the setting changes
    public static void Resolve() {
        bool ui = false;
        try { ui = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ar"; } catch (Exception) { }
        Ar = ChoosesArabic(ParseLang(Environment.GetEnvironmentVariable(EnvVar)) ?? (Core.Cfg == null ? "auto" : Core.Cfg.Language), ui);
    }

    static Dictionary<string, string[]> map;
    static Dictionary<string, string[]> Map { get { return map ?? (map = Rows.ToDictionary(r => r[0], r => r)); } }
    public static IEnumerable<string[]> All { get { return Rows; } }

    public static string T(string key) {
        string[] r;
        if (!Map.TryGetValue(key, out r)) return key;
        return Ar ? Bidi(r[2]) : r[1];
    }
    public static string F(string key, params object[] a) {
        string[] r;
        string f = Map.TryGetValue(key, out r) ? (Ar ? r[2] : r[1]) : key;
        return Bidi(string.Format(CultureInfo.InvariantCulture, f, a));
    }
    public static string Pct(double v) { return Bidi(string.Format(CultureInfo.InvariantCulture, "{0:0}%", v)); }

    // a counted noun or sentence: "1 item" / "3 items"; Arabic one, two, 3-10, 11+ ({0} is the number)
    public static string Plural(string noun, int n) {
        string[] r;
        if (!PluralMap.TryGetValue(noun, out r)) return n + " " + noun;
        string f;
        if (!Ar) f = n == 1 ? r[1] : r[2];
        else { int m = n % 100; f = n == 1 ? r[3] : n == 2 ? r[4] : (m >= 3 && m <= 10) || n == 0 ? r[5] : r[6]; }
        return Bidi(string.Format(CultureInfo.InvariantCulture, f, n));
    }

    // Arabic text keeps every Latin word, number and unit as one left-to-right run (NJ-Sort 1.0.0, 70%, Ctrl+Alt+R, 14:30), whatever sits next to it:
    // a bare right-to-left paragraph would show "%70" and "Alt+Ctrl". LRM on both sides (WPF ignores the embedding and isolate marks, but every
    // engine honours a strong left-to-right mark; the mark on both sides keeps a trailing % with its number). Idempotent: old marks are dropped first.
    static readonly System.Text.RegularExpressions.Regex LatinRun = new System.Text.RegularExpressions.Regex(@"[A-Za-z0-9](?:(?: ?[A-Za-z0-9.,:/+%\-])*[A-Za-z0-9%])?");
    static readonly System.Text.RegularExpressions.Regex Gap = new System.Text.RegularExpressions.Regex(@"‎([^\p{L}\p{N}‎‏]+)‎");
    public static string Bidi(string s) {
        if (!Ar || string.IsNullOrEmpty(s)) return s;
        s = LatinRun.Replace(s.Replace("\u200E", "").Replace("\u200F", ""), "\u200E$0\u200E");
        // two left-to-right runs with only neutrals between them ("2026\u060C 09:05 \u00B7 12") would fuse into one run and read backwards:
        // a right-to-left mark on each side of the gap keeps them apart
        return Gap.Replace(s, "\u200E\u200F$1\u200F\u200E");
    }
    static Dictionary<string, string[]> pmap;
    static Dictionary<string, string[]> PluralMap { get { return pmap ?? (pmap = PluralRows.ToDictionary(r => r[0], r => r)); } }
    public static IEnumerable<string[]> AllPlural { get { return PluralRows; } }

    public static W.FlowDirection Flow { get { return Ar ? W.FlowDirection.RightToLeft : W.FlowDirection.LeftToRight; } }
    public static bool HasArabic(string s) { return s != null && s.Any(c => c >= 0x0600 && c <= 0x06FF); }
    // text that must be escaped to sit inside a single-quoted XAML attribute
    public static string Xml(string s) { return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;").Replace("\"", "&quot;"); }

    public static string KeyName(int i) { return T("kn." + i); }
    public static string KeyShort(int i) { return T("kns." + i); }   // short, for the note inside a shortcut box

    // ---- dates and times (invariant digits; Arabic names from the tables below) ----
    static readonly string[] MonthsAr = { "كانون الثاني", "شباط", "آذار", "نيسان", "أيار", "حزيران", "تموز", "آب", "أيلول", "تشرين الأول", "تشرين الثاني", "كانون الأول" };
    static readonly string[] DaysAr = { "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };   // DayOfWeek order, Sunday first
    static string Hm(DateTime t) { return t.ToString("HH:mm", CultureInfo.InvariantCulture); }
    // "29 Nov 2026" / "29 تشرين الثاني 2026"
    public static string DateLong(DateTime t) {
        return Ar ? t.Day + " " + MonthsAr[t.Month - 1] + " " + t.Year : t.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    }
    // the name of a sort that has none: "Sun 29 Nov, 14:30" / "الأحد 29 تشرين الثاني، 14:30"
    public static string SortTitle(DateTime t) {
        return Ar ? Bidi(DaysAr[(int)t.DayOfWeek] + " " + t.Day + " " + MonthsAr[t.Month - 1] + "، " + Hm(t)) : t.ToString("ddd d MMM, HH:mm", CultureInfo.InvariantCulture);
    }
    // "just now", "5 min ago", "today, 14:30", "yesterday, 14:30", "29 Nov 2026, 14:30"
    public static string When(DateTime t) {
        var now = DateTime.Now; var ago = now - t;
        if (ago.TotalMinutes < 1) return T("when.now");
        if (ago.TotalMinutes < 60) return Plural("ago", (int)ago.TotalMinutes);
        if (t.Date == now.Date) return F("when.today", Hm(t));
        if (t.Date == now.Date.AddDays(-1)) return F("when.yesterday", Hm(t));
        return F("when.date", DateLong(t), Hm(t));
    }
    // the reason a restore ran ("after sign-in" ...) in the language in force; the English phrase is also the log text
    public static string Why(string why) { return why == null ? "" : T("why." + why); }
    public static string Capital(string s) { return string.IsNullOrEmpty(s) || Ar ? s : char.ToUpper(s[0]) + s.Substring(1); }

    static string[] R(string key, string en, string ar) { return new[] { key, en, ar }; }
    // noun, English one, English other, Arabic one / two / 3-10 / 11+ ({0} is the number)
    static string[] P(string noun, string en1, string enN, string ar1, string ar2, string arFew, string arMany) { return new[] { noun, en1, enN, ar1, ar2, arFew, arMany }; }

    static readonly string[][] PluralRows = {
        P("item", "{0} item", "{0} items", "عنصر واحد", "عنصران", "{0} عناصر", "{0} عنصرًا"),
        P("ago", "{0} min ago", "{0} min ago", "قبل دقيقة", "قبل دقيقتين", "قبل {0} دقائق", "قبل {0} دقيقة"),
        P("notin", "{0} item is not in it; it follows it, sorted by name.", "{0} items are not in it; they follow it, sorted by name.",
                   "العناصر خارج الترتيب: {0}؛ تأتي بعده مرتبة بالاسم.", "العناصر خارج الترتيب: {0}؛ تأتي بعده مرتبة بالاسم.",
                   "العناصر خارج الترتيب: {0}؛ تأتي بعده مرتبة بالاسم.", "العناصر خارج الترتيب: {0}؛ تأتي بعده مرتبة بالاسم."),
        P("undone", "{0} item is where it was. Undo again to redo.", "{0} items are where they were. Undo again to redo.",
                    "عاد عنصر واحد إلى مكانه. اضغط «تراجع» مرة أخرى لإعادة ما تراجعت عنه.", "عاد عنصران إلى مكانيهما. اضغط «تراجع» مرة أخرى لإعادة ما تراجعت عنه.",
                    "عادت {0} عناصر إلى أماكنها. اضغط «تراجع» مرة أخرى لإعادة ما تراجعت عنه.", "عاد {0} عنصرًا إلى أماكنه. اضغط «تراجع» مرة أخرى لإعادة ما تراجعت عنه."),
    };

    static readonly string[][] Rows = {
        // ---- language ----
        R("lang.label", "Language / اللغة", "اللغة / Language"),
        R("lang.auto", "Auto", "تلقائي"),
        R("lang.en", "English", "English"),
        R("lang.ar", "العربية", "العربية"),
        R("lang.note", "Auto follows the Windows display language.", "الخيار التلقائي يتبع لغة العرض في ويندوز."),
        R("sep", ", ", "، "),

        // ---- window: header ----
        R("badge", "Running in the hidden icons", "يعمل في الأيقونات المخفية"),
        R("tagline", "Keeps your desktop icons in the sort you saved.", "يُبقي أيقونات سطح المكتب على الترتيب الذي حفظته."),

        // ---- window: Desktop card ----
        R("h.desktop", "Desktop", "سطح المكتب"),
        R("desk.none", "No sort saved yet. Arrange your icons the way you like, then press Save sort now.", "لم يُحفظ أي ترتيب بعد. رتّب أيقوناتك كما تحب، ثم اضغط «حفظ الترتيب الآن»."),
        R("desk.active", "Active sort: \u201C{0}\u201D, {1}.", "الترتيب النشط: «{0}»، {1}."),
        R("desk.all", "Every item on the desktop is in it.", "كل عناصر سطح المكتب موجودة فيه."),
        R("btn.save", "Save sort now", "حفظ الترتيب الآن"),
        R("btn.rearrange", "Rearrange", "إعادة الترتيب"),
        R("btn.undo", "Undo", "تراجع"),
        R("tip.save", "Saves where every icon is now as a new sort, and makes it the active one.", "يحفظ مكان كل أيقونة الآن كترتيب جديد، ويجعله الترتيب النشط."),
        R("tip.rearrange", "Puts the icons back in the active sort. Items that are not in it follow, sorted by name.", "يعيد الأيقونات إلى الترتيب النشط. أما العناصر غير الموجودة فيه فتأتي بعده مرتبة بالاسم."),
        R("tip.undo", "Moves the icons back to where they were before the last rearrange.", "يعيد الأيقونات إلى أماكنها قبل آخر إعادة ترتيب."),

        // ---- window: history card ----
        R("h.history", "Sort history", "سجل الترتيبات"),
        R("history.note", "Newest on top. Pick a sort and use it to go back to that point. Double-click works too.", "الأحدث في الأعلى. اختر ترتيبًا ثم استخدمه للرجوع إلى تلك اللحظة. والنقر المزدوج يفعل الشيء نفسه."),
        R("chip.active", "Active", "نشط"),
        R("history.empty", "No sorts yet. Arrange your icons, then press Save sort now.", "لا توجد ترتيبات بعد. رتّب أيقوناتك ثم اضغط «حفظ الترتيب الآن»."),
        R("btn.savename", "Save name", "حفظ الاسم"),
        R("btn.use", "Use this sort", "استخدام هذا الترتيب"),
        R("btn.rename", "Rename", "إعادة التسمية"),
        R("btn.delete", "Delete", "حذف"),
        R("tip.delete", "Sends the sort to the Recycle Bin.", "ينقل الترتيب إلى سلة المحذوفات."),
        R("when.now", "just now", "الآن"),
        R("when.today", "today, {0}", "اليوم، {0}"),
        R("when.yesterday", "yesterday, {0}", "أمس، {0}"),
        R("when.date", "{0}, {1}", "{0}، {1}"),
        R("hist.detail", "{0}  \u00B7  {1}", "{0}  \u00B7  {1}"),
        R("imported", "Imported from the old NJ-Sort", "مستورد من NJ-Sort القديم"),

        // ---- window: shortcuts card ----
        R("h.shortcuts", "Shortcuts", "الاختصارات"),
        R("shortcuts.note", "Click a box and press the new keys; Backspace removes it. Shortcuts work while NJ-Sort runs in the hidden icons, and stop when you quit it.",
                            "اضغط على مربع ثم اضغط المفاتيح الجديدة؛ ويحذف Backspace الاختصار. تعمل الاختصارات ما دام NJ-Sort في الأيقونات المخفية، وتتوقف حين تنهيه."),
        R("kn.0", "Save sort now", "حفظ الترتيب الآن"),
        R("kn.1", "Rearrange to the active sort", "إعادة الترتيب إلى النشط"),
        R("kn.2", "Undo the last move", "التراجع عن آخر نقل"),
        R("kn.3", "Open NJ-Sort", "فتح NJ-Sort"),
        R("kns.0", "Save sort now", "الحفظ"),
        R("kns.1", "Rearrange to the active sort", "إعادة الترتيب"),
        R("kns.2", "Undo the last move", "التراجع"),
        R("kns.3", "Open NJ-Sort", "الفتح"),
        R("hk.none", "None", "بلا"),
        R("hk.nokey", "no shortcut", "بلا اختصار"),
        R("hk.click", "Click, then press the keys", "اضغط على المربع ثم على المفاتيح"),
        R("hk.clear", "No shortcut", "بلا اختصار"),
        R("hk.press", "Press the new keys\u2026", "اضغط المفاتيح الجديدة\u2026"),
        R("hk.add_mod", "Add Ctrl, Alt, Shift or Win", "أضف Ctrl أو Alt أو Shift أو Win"),
        R("hk.used", "Used by {0}", "مستخدم في «{0}»"),
        R("hk.taken", "{0} is taken by another program. Pick another.", "{0} مستخدم في برنامج آخر. اختر غيره."),

        // ---- window: behaviour card ----
        R("h.behaviour", "Behaviour", "السلوك"),
        R("sw.auto", "Put icons back by themselves", "إعادة الأيقونات تلقائيًا"),
        R("sw.auto_note", "After Explorer restarts (NJ-Flush, updates), after sign-in and restarts, and after screen changes. It waits until the desktop is steady first.",
                          "بعد إعادة تشغيل مستكشف الملفات (بسبب NJ-Flush أو التحديثات)، وبعد تسجيل الدخول وإعادة التشغيل، وبعد تغيّر الشاشة. وينتظر أولًا حتى يستقر سطح المكتب."),
        R("sw.extras", "Keep new items sorted by name", "إبقاء العناصر الجديدة مرتبة بالاسم"),
        R("sw.extras_note", "Items that are not in the sort go after it on the main screen, folders first, like Windows sort by name.",
                            "العناصر غير الموجودة في الترتيب تأتي بعده على الشاشة الرئيسية، والمجلدات أولًا، مثل «الترتيب حسب الاسم» في ويندوز."),
        R("sw.anim", "Animate", "الحركة"),
        R("sw.anim_note", "Icons glide into place. Off when Windows animations are off.", "تنساب الأيقونات إلى أماكنها. ويتوقف ذلك إذا كانت الحركات متوقفة في ويندوز."),
        R("sw.sound", "Play sounds", "تشغيل الأصوات"),
        R("vol.label", "Volume", "مستوى الصوت"),
        R("sw.logon", "Start with Windows", "التشغيل مع ويندوز"),
        R("sw.logon_note", "Starts in the hidden icons when you sign in.", "يبدأ في الأيقونات المخفية عند تسجيل الدخول."),

        // ---- window: footer ----
        R("btn.close", "Close (keeps running)", "إغلاق (يبقى يعمل)"),
        R("btn.quit", "Quit NJ-Sort", "إنهاء NJ-Sort"),
        R("tip.quit", "Stops NJ-Sort. Its shortcuts stop working until you open it again.", "يوقف NJ-Sort. تتوقف اختصاراته عن العمل حتى تفتحه مجددًا."),

        // ---- toasts ----
        R("t.nosave.h", "Nothing to save", "لا شيء للحفظ"),
        R("t.nosave.b", "The desktop has no items.", "لا توجد عناصر على سطح المكتب."),
        R("t.already.h", "Already saved", "محفوظ مسبقًا"),
        R("t.already.b", "Nothing moved since \u201C{0}\u201D.", "لم يتحرك شيء منذ «{0}»."),
        R("t.saved.h", "Sort saved", "تم حفظ الترتيب"),
        R("t.saved.b", "{0}. It is now the active sort.", "{0}. وهو الآن الترتيب النشط."),
        R("t.nosort.h", "No sort saved yet", "لا يوجد ترتيب محفوظ بعد"),
        R("t.nosort.b_key", "Arrange your icons, then press {0} or Save sort now.", "رتّب أيقوناتك ثم اضغط {0} أو «حفظ الترتيب الآن»."),
        R("t.nosort.b", "Arrange your icons, then press Save sort now.", "رتّب أيقوناتك ثم اضغط «حفظ الترتيب الآن»."),
        R("t.rearranged.h", "Rearranged", "أُعيد الترتيب"),
        R("t.autoput.h", "Icons put back", "أُعيدت الأيقونات إلى أماكنها"),
        R("t.inorder.h", "Already in order", "الأيقونات في أماكنها بالفعل"),
        R("t.kept", "{0} in their saved place", "في أماكنها المحفوظة: {0}"),
        R("t.kept_sorted", "{0} in their saved place, {1} more sorted by name after them", "في أماكنها المحفوظة: {0}، وبعدها مرتبة بالاسم: {1}"),
        R("t.sentence", "{0}.", "{0}."),
        R("t.reason", "{0}. {1}.", "{0}. {1}."),
        R("t.noundo.h", "Nothing to undo", "لا شيء للتراجع عنه"),
        R("t.noundo.b", "The icons are already there.", "الأيقونات في أماكنها بالفعل."),
        R("t.undo.h", "Moved back", "أُعيدت إلى أماكنها"),
        R("t.delfail.h", "Could not delete it", "تعذّر الحذف"),
        R("t.deleted.h", "Sort deleted", "تم حذف الترتيب"),
        R("t.deleted.b", "\u201C{0}\u201D is in the Recycle Bin.", "«{0}» في سلة المحذوفات."),
        R("t.loading.h", "The desktop is still loading", "سطح المكتب ما زال قيد التحميل"),
        R("t.loading.b", "Try again in a moment.", "حاول مجددًا بعد لحظات."),
        R("t.autoarr.h", "Auto arrange is on", "«ترتيب الأيقونات تلقائيًا» مفعّل"),
        R("t.autoarr.b", "Right-click the desktop, View, and turn off Auto arrange icons.", "انقر بزر الفأرة الأيمن على سطح المكتب، ثم «عرض»، وأوقف «ترتيب الأيقونات تلقائيًا»."),

        // ---- why a restore ran (the key is the English log text) ----
        R("why.after sign-in", "after sign-in", "بعد تسجيل الدخول"),
        R("why.after Explorer restarted", "after Explorer restarted", "بعد إعادة تشغيل مستكشف الملفات"),
        R("why.after the screen changed", "after the screen changed", "بعد تغيّر الشاشة"),
        R("why.after the PC woke up", "after the PC woke up", "بعد استيقاظ الجهاز"),
        R("why.after the desktop reloaded", "after the desktop reloaded", "بعد إعادة تحميل سطح المكتب"),

        // ---- hidden icons (tray) ----
        R("tray.open", "Open NJ-Sort", "فتح NJ-Sort"),
        R("tray.save", "Save sort now", "حفظ الترتيب الآن"),
        R("tray.rearrange", "Rearrange", "إعادة الترتيب"),
        R("tray.undo", "Undo the last move", "التراجع عن آخر نقل"),
        R("tray.quit", "Quit NJ-Sort (shortcuts stop)", "إنهاء NJ-Sort (تتوقف الاختصارات)"),
        R("tray.tip", "NJ-Sort: {0} saves, {1} rearranges", "NJ-Sort: {0} للحفظ، و{1} لإعادة الترتيب"),
        R("tray.still.h", "NJ-Sort is still running", "NJ-Sort ما زال يعمل"),
        R("tray.still.b", "It sits in the hidden icons, and its shortcuts keep working. Right-click it to quit.", "يبقى في الأيقونات المخفية وتظل اختصاراته تعمل. انقر عليه بزر الفأرة الأيمن لإنهائه."),

        // ---- README render samples ----
        R("demo.sort.b", "22 in their saved place, 2 more sorted by name after them.", "في أماكنها المحفوظة: 22، وبعدها مرتبة بالاسم: 2"),
        R("demo.name1", "Work week", "أسبوع العمل"),
        R("demo.name3", "Clean desktop", "سطح مكتب مرتّب"),
        R("demo.name4", "Before the big tidy-up", "قبل الترتيب الكبير"),
    };

    // ---- string-table checks for --selftest: returns the faults, empty when the table is sound ----
    public static List<string> Check() {
        var bad = new List<string>();
        var keys = new HashSet<string>();
        Func<string, string> holes = s => string.Join(",", System.Text.RegularExpressions.Regex.Matches(s, @"\{(\d+)[^}]*\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x).ToArray());
        Action<string, string, string> one = (key, en, ar) => {
            if (string.IsNullOrWhiteSpace(en) || string.IsNullOrWhiteSpace(ar)) bad.Add(key + ": empty side");
            if (holes(en) != holes(ar) && !key.StartsWith("p:notin") && !key.StartsWith("p:ago") && !key.StartsWith("p:item") && !key.StartsWith("p:undone")) bad.Add(key + ": placeholders differ (" + holes(en) + " vs " + holes(ar) + ")");
            if (ar.IndexOf('\u0640') >= 0) bad.Add(key + ": tatweel");
            if (ar.IndexOf('\u06A9') >= 0 || ar.IndexOf('\u06CC') >= 0) bad.Add(key + ": Persian letter");
            if (ar.IndexOf('\u200E') >= 0 || ar.IndexOf('\u200F') >= 0) bad.Add(key + ": direction mark in the table (Bidi adds them)");
            if (HasArabic(ar) && (ar.IndexOf(';') >= 0 || ar.IndexOf('?') >= 0 || System.Text.RegularExpressions.Regex.IsMatch(ar, "[\u0621-\u064A],"))) bad.Add(key + ": Latin punctuation in Arabic");
            if (System.Text.RegularExpressions.Regex.IsMatch(ar, "[\u0660-\u0669]")) bad.Add(key + ": Arabic-Indic digits");
            if (ar.IndexOf("  ") >= 0 && key != "hist.detail") bad.Add(key + ": double space");
            try { string.Format(CultureInfo.InvariantCulture, en, "a", "b"); string.Format(CultureInfo.InvariantCulture, ar, "a", "b"); } catch (FormatException) { bad.Add(key + ": bad format string"); }
        };
        foreach (var r in Rows) { if (!keys.Add(r[0])) bad.Add(r[0] + ": duplicate key"); one(r[0], r[1], r[2]); }
        foreach (var r in PluralRows) {
            one("p:" + r[0] + ".en", r[1], r[3]); one("p:" + r[0] + ".enN", r[2], r[6]);
            if (!HasArabic(r[3]) || !HasArabic(r[4]) || !HasArabic(r[5]) || !HasArabic(r[6])) bad.Add(r[0] + ": an Arabic plural form has no Arabic");
        }
        // every counted form formats for 0..120 without throwing, and a Latin run is always wrapped on both sides in Arabic
        bool was = Ar;
        try {
            Ar = true;
            foreach (var r in PluralRows) for (int n = 0; n <= 120; n++) { string s = Plural(r[0], n); if (s.IndexOf(n.ToString(CultureInfo.InvariantCulture)) < 0 && n > 2) bad.Add(r[0] + ": " + n + " missing in the Arabic form"); }
            string b = Bidi("NJ-Sort 1.0.0 يحفظ Alt+S عند 14:30 (70%)");
            if (Bidi(b) != b) bad.Add("Bidi is not idempotent");
            if (b.Replace("\u200E", "").Replace("\u200F", "") != "NJ-Sort 1.0.0 يحفظ Alt+S عند 14:30 (70%)" || b.IndexOf("\u200F") < 0) bad.Add("Bidi changed the text or left a gap between runs unguarded: " + b);
            foreach (var m in Enumerable.Range(1, 12)) if (SortTitle(new DateTime(2026, m, 5, 9, 7, 0)).IndexOf("09:07") < 0) bad.Add("SortTitle month " + m);
            foreach (var d in Enumerable.Range(5, 7)) if (SortTitle(new DateTime(2026, 11, d)).Length < 8) bad.Add("SortTitle day " + d);
        } finally { Ar = was; }
        return bad;
    }

    // --strings <file>: the Arabic side as a markdown table for the proofread pass (no direction marks: they are added at run time)
    public static void Export(string path) {
        var sb = new StringBuilder();
        sb.AppendLine("| key | English | Arabic |"); sb.AppendLine("| --- | --- | --- |");
        Func<string, string> cell = s => s.Replace("|", "\\|").Replace("\n", " ");
        foreach (var r in Rows) sb.AppendLine("| " + r[0] + " | " + cell(r[1]) + " | " + cell(r[2]) + " |");
        foreach (var r in PluralRows) {
            sb.AppendLine("| " + r[0] + " (1) | " + cell(r[1]) + " | " + cell(r[3]) + " |");
            sb.AppendLine("| " + r[0] + " (2) | " + cell(r[2]) + " | " + cell(r[4]) + " |");
            sb.AppendLine("| " + r[0] + " (3-10) | " + cell(r[2]) + " | " + cell(r[5]) + " |");
            sb.AppendLine("| " + r[0] + " (11+) | " + cell(r[2]) + " | " + cell(r[6]) + " |");
        }
        System.IO.File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
}
