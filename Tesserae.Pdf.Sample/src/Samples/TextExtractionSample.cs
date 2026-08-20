using System;
using System.Threading.Tasks;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// Reading a document's words out, and seeing what PDF actually stores - positioned runs of
    /// glyphs, not paragraphs. The run table is the honest view; the concatenated string above it is
    /// the convenience.
    /// </summary>
    [SampleDetails(Group = "Pages", Order = 30, Icon = UIcons.AlignLeft)]
    public class TextExtractionSample : IComponent, ISample
    {
        private readonly IComponent  _content;
        private readonly HTMLElement _host = DIV();

        private PdfDocument _document;

        public TextExtractionSample()
        {
            var pageText = TextBlock("Loading...").Small();
            var runs     = VStack().WS().Gap(2.px());
            var summary  = TextBlock("").Small().Secondary();

            var pageNumber = new SettableObservable<int>(3);
            var pageSlider = Slider(3, 1, 12, 1).Bind(pageNumber).Width(200.px());

            pageNumber.ObserveFutureChanges(n => ShowAsync(n, pageText, runs, summary).FireAndForget());

            var readAll = Button("Read the whole document").SetIcon(UIcons.File)
               .OnClick(() => ShowAllAsync(pageText, runs, summary).FireAndForget());

            OpenAsync(pageText, runs, summary).FireAndForget();

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(TextExtractionSample), UIcons.AlignLeft, "A page's words, and the runs they came from")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("page.GetTextContentAsync() gives the page's text as pdf.js found it: a list of runs, each with the characters, the font, the size, and a matrix saying where on the page it sits. page.GetTextAsync() concatenates those into a string, and document.GetAllTextAsync() does it for every page with a form feed between them."),
                        TextBlock("Text extraction needs no viewer and no canvas, which makes it the cheapest thing in the package: indexing an upload, showing a snippet in a search result, feeding a document to a model.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("A PDF does not store paragraphs. It stores instructions to draw glyphs at coordinates, so \"the text of this page\" is a reconstruction, and the order the runs come in is the order they were drawn - which is not always reading order. A two-column layout can interleave; a table can come out row-first or column-first depending on how it was generated."),
                        TextBlock("hasEOL on a run is the only line-break signal pdf.js offers, and it is a guess from the geometry rather than something the document says. Good enough for search, indexing and a rough preview; not a substitute for a layout-aware extractor if you need the structure back.").MT(8),
                        TextBlock("Leave normalisation on. pdf.js folds ligatures and combining marks into their plain forms, which is what makes a search for \"file\" match a document that drew \"fi\" as one glyph. disableNormalization gives you the raw code points for when you are doing that yourself.").MT(8),
                        TextBlock("For structure rather than words, page.Instance.getStructTree() gives the tagged-PDF tree - present only in documents that were produced accessibly.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        HStack().WS().Gap(16.px()).Children(
                            VStack().Children(TextBlock("Page").Small().SemiBold(), pageSlider),
                            VStack().Children(TextBlock("Or all of it").Small().SemiBold(), readAll)),
                        summary.MT(8),
                        SampleSubTitle("As one string"),
                        pageText,
                        SampleSubTitle("The runs it was built from"),
                        runs,
                        Raw(_host),
                        SampleHint("Pages 3, 7 and 11 mention \"tesserae\" once each - which is what the Search page counts.")
                    )).SetTitle("Usage")))
               ;

            DomObserver.WhenRemoved(_host, () => _document?.DestroyAsync().FireAndForget());
        }

        private async Task OpenAsync(TextBlock pageText, Stack runs, TextBlock summary)
        {
            try
            {
                _document = await PdfJs.OpenAsync(OUTLINE_PDF);

                await ShowAsync(3, pageText, runs, summary);
            }
            catch (PdfError error)
            {
                pageText.Text = "Could not open the document: " + error.Message;
            }
        }

        private async Task ShowAsync(int pageNumber, TextBlock pageText, Stack runs, TextBlock summary)
        {
            if (_document is null) return;

            var page    = await _document.GetPageAsync(Math.Max(1, Math.Min(pageNumber, _document.PageCount)));
            var content = await page.GetTextContentAsync();

            pageText.Text = await page.GetTextAsync();

            runs.Clear();

            var shown = Math.Min(content.items.Length, 12);

            for (var i = 0; i < shown; i++)
            {
                var item = content.items[i];

                if (item.str is null) continue;

                // transform[4] and [5] are the run's x and y in PDF units - bottom-left origin, so a
                // larger y is further up the page.
                runs.Add(HStack().WS().Gap(8.px()).Children(
                    TextBlock($"{item.transform[4]:0}, {item.transform[5]:0}").Tiny().Secondary().W(70),
                    TextBlock(item.fontName ?? "?").Tiny().Secondary().W(60),
                    TextBlock(item.hasEOL ? "EOL" : "").Tiny().Secondary().W(30),
                    TextBlock(item.str).Small().Grow()));
            }

            summary.Text = $"Page {page.PageNumber}: {content.items.Length} runs, language {content.lang ?? "unstated"}. Showing the first {shown}.";
        }

        private async Task ShowAllAsync(TextBlock pageText, Stack runs, TextBlock summary)
        {
            if (_document is null) return;

            summary.Text = "Reading all " + _document.PageCount + " pages...";

            var text = await _document.GetAllTextAsync();

            runs.Clear();

            pageText.Text = text;
            summary.Text  = $"All {_document.PageCount} pages: {text.Length} characters, pages separated by a form feed.";
        }

        public HTMLElement Render() => _content.Render();
    }
}
