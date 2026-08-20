using System.Collections.Generic;
using TNT;
using static TNT.T;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// The text pdf.js writes into the DOM - aria labels, annotation tooltips, editor buttons - going
    /// through Tesserae's TNT translation table alongside the application's own strings.
    /// </summary>
    [SampleDetails(Group = "Runtime and hosting", Order = 80, Icon = UIcons.Globe)]
    public class LocalizationSample : IComponent, ISample
    {
        private readonly IComponent _content;

        /// <summary>
        /// A German dictionary for the four strings this page shows - two of the package's l10n keys
        /// and two of the page's own, to make the point that they share one table.
        ///
        /// A real application loads this from wherever its translations live and calls
        /// <c>TNT.T.SetTranslation</c> once, before building any UI.
        /// </summary>
        private static readonly Dictionary<string, string> German = new Dictionary<string, string>
        {
            // Two of pdf.js's own message ids, as the package's English keys.
            { "Page {0}",             "Seite {0}" },
            { "[{0} Annotation]",     "[Anmerkung: {0}]" },
            { "Highlight",            "Hervorheben" },
            { "Add comment",          "Kommentar hinzufügen" },

            // And two of this page's, to show it is the same table.
            { "The page landmark below is what a screen reader announces.", "Die Seitenmarkierung unten ist, was ein Screenreader vorliest." },
            { "Language",             "Sprache" },
        };

        public LocalizationSample()
        {
            var report = VStack().WS().Gap(2.px());
            var host   = VStack().WS().H(420);

            // Held so Rebuild can re-run their .t() lookups. A TextBlock built once keeps the text it
            // was built with: TNT reads its table at each call, not at each render, so a language
            // change means asking again - which is why a real application reloads the page.
            var languageLabel = TextBlock("").Small().SemiBold();
            var explanation   = TextBlock("").Small();

            Rebuild();

            var toGerman = Button("Deutsch").SetIcon(UIcons.Globe).OnClick(() =>
            {
                T.SetTranslation(German);
                Rebuild();
            });

            var toEnglish = Button("English").SetIcon(UIcons.Globe).OnClick(() =>
            {
                // null is how TNT is told to stop translating: every key falls back to its own text.
                T.SetTranslation(null);
                Rebuild();
            });

            // The viewer is rebuilt rather than refreshed, because TNT takes a snapshot per lookup and
            // pdf.js has already written its labels into the DOM. That is also the convention a
            // Tesserae app follows for a language change - reload rather than re-translate in place.
            void Rebuild()
            {
                host.Clear();
                report.Clear();

                languageLabel.Text = "Language".t();
                explanation.Text   = "The page landmark below is what a screen reader announces.".t();

                var viewer = PdfJs.Viewer();

                viewer
                   .Url(OUTLINE_PDF)
                   .FitWidth()
                   .OnPageRendered(_ => ShowLandmarks(report));

                host.Add(viewer.S());
            }

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(LocalizationSample), UIcons.Globe, "pdf.js's own strings, through Tesserae's translation table")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("pdf.js puts data-l10n-id attributes on the elements it builds and expects something to turn them into text. Left alone it uses an English-only bundle inlined into its viewer; the package replaces that with a bridge that answers the same 50 message ids through TNT - the translation table Tesserae itself uses."),
                        TextBlock("So a German application gets a German viewer, from the same dictionary that translates its own buttons, with nothing to configure. L10n(customObject) replaces the bridge, and WithoutOwnLocalization() falls back to pdf.js's English.").MT(8),
                        TextBlock("Most of what it covers is invisible until it matters: page landmarks a screen reader announces, alt text on annotation icons, tooltips on the editor's buttons. This page shows the landmark, because it is the one you can read back out of the DOM.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("There is a gap a package cannot close by itself: a host's own tnt extract scans its own source, and this package's strings live in a NuGet package it never sees. So these keys will not appear in your translation file on their own - the README lists all 50, to be pasted into whatever feeds SetTranslation."),
                        TextBlock("The package deliberately exposes no language API of its own. TNT's table is process-global and singular; a package that called SetTranslation would clobber the host's. PdfJs.Language only tells pdf.js which language it is looking at, which decides text direction and how dates inside annotations are formatted.").MT(8),
                        TextBlock("Placeholders follow TNT's convention rather than Fluent's: \"Page {0}\", not \"Page { $page }\". That is what t($\"Page {n}\") produces, so a translator sees the same shape here as everywhere else in the application.").MT(8),
                        TextBlock("Changing language rebuilds. TNT reads its table at each lookup, but pdf.js has already written its labels into the DOM by then - so the viewer is rebuilt, which is also what a Tesserae app does for a language change.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        languageLabel,
                        HStack().WS().Gap(8.px()).Children(toEnglish, toGerman),
                        explanation.MT(8),
                        report.MT(4),
                        host.MT(8),
                        SampleHint("Switch to Deutsch: the landmark becomes \"Seite 1\", and the line above it changes too - one dictionary, both sets of strings.")
                    )).SetTitle("Usage")))
               ;
        }

        /// <summary>
        /// Reads the aria-labels pdf.js wrote back out of the DOM, which is where the bridge's work
        /// actually lands.
        /// </summary>
        private static void ShowLandmarks(Stack report)
        {
            report.Clear();

            var landmarks = document.querySelectorAll("[data-l10n-id='pdfjs-page-landmark']");

            for (var i = 0; i < landmarks.length && i < 4; i++)
            {
                var element = (HTMLElement)landmarks[i];

                report.Add(TextBlock($"aria-label = \"{element.getAttribute("aria-label")}\"").Tiny().Secondary());
            }

            if (landmarks.length == 0) report.Add(TextBlock("No page landmarks in the DOM yet.").Tiny().Secondary());
        }

        public HTMLElement Render() => _content.Render();
    }
}
