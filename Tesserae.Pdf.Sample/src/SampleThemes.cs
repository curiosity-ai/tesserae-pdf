using System.Collections.Generic;
using System.Threading.Tasks;
using Tesserae.Themes.Blaupause;
using Tesserae.Themes.Curiosity;
using Tesserae.Themes.Glas;
using Tesserae.Themes.Graustufe;
using Tesserae.Themes.Kontrast;
using Tesserae.Themes.Kristall;
using Tesserae.Themes.Memphis;
using Tesserae.Themes.Phosphor;
using Tesserae.Themes.Rechenwerk;
using Tesserae.Themes.RetroOS;
using Tesserae.Themes.Stratus;
using Tesserae.Themes.Washi;
using Tesserae.Themes.Weniger;
using Tesserae.Themes.Zeitung;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// The Tesserae custom themes, as a nav at the foot of the gallery's sidebar. The viewer and the chrome
    /// are drawn with Tesserae's tokens and components, so every theme restyles them; this is what lets a
    /// change be checked in each one. The choice is kept in <c>localStorage["tss-pdf-sample-theme"]</c>
    /// under the same ids as Tesserae's own gallery, so a browser check can pick a theme before load.
    /// </summary>
    internal static class SampleThemes
    {
        private const string THEME_KEY       = "tss-pdf-sample-theme";
        private const string DEFAULT_THEME_ID = "default";

        private sealed class Entry
        {
            public readonly string       Id;
            public readonly string       Label;
            public readonly UIcons       Icon;
            public readonly ICustomTheme Theme;

            public Entry(string id, string label, UIcons icon, ICustomTheme theme)
            {
                Id    = id;
                Label = label;
                Icon  = icon;
                Theme = theme;
            }
        }

        private static readonly Entry[] _entries =
        {
            new Entry(DEFAULT_THEME_ID, "Default",    UIcons.Square,          null),
            new Entry("curiosity",      "Curiosity",  UIcons.SquareSmall,     CuriosityTheme.Instance),
            new Entry("stratus",        "Stratus",    UIcons.Plane,           StratusTheme.Instance),
            new Entry("retroos",        "RetroOS",    UIcons.ComputerClassic, RetroOSTheme.Instance),
            new Entry("rechenwerk",     "Rechenwerk", UIcons.Calculator,      RechenwerkTheme.Instance),
            new Entry("weniger",        "Weniger",    UIcons.Radio,           WenigerTheme.Instance),
            new Entry("zeitung",        "Zeitung",    UIcons.Newspaper,       ZeitungTheme.Instance),
            new Entry("blaupause",      "Blaupause",  UIcons.DraftingCompass, BlaupauseTheme.Instance),
            new Entry("phosphor",       "Phosphor",   UIcons.TvRetro,         PhosphorTheme.Instance),
            new Entry("glas",           "Glas",       UIcons.Glass,           GlasTheme.Instance),
            new Entry("kristall",       "Kristall",   UIcons.Gem,             KristallTheme.Instance),
            new Entry("kontrast",       "Kontrast",   UIcons.Contrast,        KontrastTheme.Instance),
            new Entry("memphis",        "Memphis",    UIcons.Triangle,        MemphisTheme.Instance),
            new Entry("washi",          "Washi",      UIcons.Stamp,           WashiTheme.Instance),
            new Entry("graustufe",      "Graustufe",  UIcons.BookAlt,         GraustufeTheme.Instance),
        };

        private static Entry Find(string id)
        {
            foreach (var entry in _entries)
            {
                if (entry.Id == id) return entry;
            }

            return null;
        }

        /// <summary>
        /// Activates the saved theme before the first paint. The page stays invisible until the theme's
        /// stylesheet has arrived, so it never flashes the default look for a frame.
        /// </summary>
        public static async Task ActivateSavedThemeAsync()
        {
            var saved = Find(localStorage.getItem(THEME_KEY));

            if (saved is null || saved.Theme is null) return;

            document.body.style.visibility = "hidden";

            try
            {
                await Theme.SetCustomTheme(saved.Theme);
            }
            finally
            {
                document.body.style.visibility = "";
            }
        }

        public static SidebarNav CreateNav()
        {
            var nav     = new SidebarNav("THEME", UIcons.Palette, "Theme", initiallyCollapsed: true).NotSortable().KeepCollapsedOnSelection();
            var buttons = new List<(Entry Entry, SidebarButton Button)>();
            var current = Find(localStorage.getItem(THEME_KEY)) ?? _entries[0];

            foreach (var entry in _entries)
            {
                var button = new SidebarButton("THEME_" + entry.Id.ToUpper(), entry.Icon, entry.Label + " Theme");

                button.IsSelected = entry == current;

                button.OnClick(() =>
                {
                    localStorage.setItem(THEME_KEY, entry.Id);

                    foreach (var other in buttons)
                    {
                        other.Button.IsSelected = other.Entry == entry;
                    }

                    Theme.SetCustomTheme(entry.Theme).FireAndForget();
                });

                buttons.Add((entry, button));
                nav.Add(button);
            }

            nav.OnClick(() => nav.Toggle());

            return nav;
        }
    }
}
