using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// Searching the document, and the thing about pdf.js's search that shapes every UI built on it:
    /// results arrive over time rather than being returned, because it reads the pages as it goes.
    /// </summary>
    [SampleDetails(Group = "Viewer", Order = 30, Icon = UIcons.Search)]
    public class SearchSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public SearchSample()
        {
            var result = TextBlock("Nothing searched yet.").Small().Secondary();

            var viewer = PdfJs.Viewer();

            viewer
               .Url(OUTLINE_PDF)
               .FitWidth()
               .OnSearchResults(found =>
               {
                   // Pending means the document is still being read and the counts are a running
                   // total - which is exactly what a "3 of 17" indicator should show while it grows.
                   var state = found.State == FindState.Pending  ? "searching"
                             : found.State == FindState.NotFound ? "no matches"
                             : found.State == FindState.Wrapped  ? "wrapped around"
                             : "found";

                   result.Text = found.Total > 0
                       ? $"{state}: {found.Current} of {found.Total}"
                       : $"{state}.";
               });

            var query = TextBox("tesserae").SetPlaceholder("Search this document").Width(220.px());

            var caseSensitive = CheckBox("Match case");
            var wholeWords    = CheckBox("Whole words");
            var diacritics    = CheckBox("Match diacritics");

            FindOptions BuildOptions() => new FindOptions
            {
                CaseSensitive   = caseSensitive.IsChecked,
                EntireWord      = wholeWords.IsChecked,
                MatchDiacritics = diacritics.IsChecked,
                HighlightAll    = true,
            };

            var search = Button("Search").SetIcon(UIcons.Search).Primary()
               .OnClick(() => viewer.Search(query.Text, BuildOptions()));

            var multiTerm = Button("Search several terms").SetIcon(UIcons.Layers)
               .OnClick(() => viewer.Search(new[] { "tesserae", "Colophon", "Scripting" }, BuildOptions()));

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(SearchSample), UIcons.Search, "Full-text search, and how its results arrive")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Search(query) searches the whole document, highlights every match and selects the first. FindNext() and FindPrevious() walk them, ClearSearch() drops the highlighting. The overload taking an array searches several terms at once, each matched independently - which is what a space-separated search box wants."),
                        TextBlock("The matching options - case, whole words, diacritics - are on FindOptions. Diacritics is off by default, so \"cafe\" finds \"café\", which is usually what a person searching means.").MT(8),
                        TextBlock("There is no method to start a search on pdf.js's find controller: a search is started by dispatching an event on the bus. The component does that for you, and it is why Search() takes effect even though it returns immediately.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Results arrive through OnSearchResults rather than being returned, and they arrive more than once. pdf.js reads the text of each page as it reaches it, so on a long document the count grows: the first reports carry FindState.Pending and a running total, and the last carries the outcome."),
                        TextBlock("Show the running count. A UI that waits for the final answer looks frozen on a hundred-page document, and one that treats the first Pending report as \"1 match\" is wrong a moment later. State tells you which kind of report you have; IsComplete is the same question asked the other way."),
                        TextBlock("FindState.Wrapped is worth surfacing rather than folding into Found. It is why the view suddenly jumped backwards, and a user who is not told assumes something broke.").MT(8),
                        TextBlock("Searching needs a text layer: with TextSelection(TextLayerMode.Disable) there is nothing to highlight, and matches are found but never shown.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        HStack().WS().Gap(8.px()).Wrap().Children(query, search, multiTerm,
                            Button("Next").OnClick(() => viewer.FindNext()),
                            Button("Previous").OnClick(() => viewer.FindPrevious()),
                            Button("Clear").OnClick(() => viewer.ClearSearch())),
                        HStack().WS().Gap(16.px()).MT(8).Children(caseSensitive, wholeWords, diacritics),
                        result.MT(8),
                        viewer.H(520).WS().MT(8),
                        SampleHint("\"tesserae\" appears on exactly three pages of this document - 3, 7 and 11 - so the count should settle on 3.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(DocumentViewerSample), typeof(TextExtractionSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
