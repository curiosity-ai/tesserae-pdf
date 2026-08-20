using System.Threading.Tasks;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// A rail of page thumbnails beside a viewer - and the reason PdfPageCanvas can borrow a document
    /// rather than open one: twelve canvases off one document, not twelve documents.
    /// </summary>
    [SampleDetails(Group = "Pages", Order = 20, Icon = UIcons.Apps)]
    public class ThumbnailsSample : IComponent, ISample
    {
        private readonly IComponent  _content;
        private readonly HTMLElement _host = DIV();
        private readonly Stack       _rail;

        private PdfDocument _document;

        public ThumbnailsSample()
        {
            var status = TextBlock("Loading...").Small().Secondary();

            _rail = VStack().WS().Gap(8.px());

            var viewer = PdfJs.Viewer();

            viewer
               .Url(OUTLINE_PDF)
               .FitWidth()
               .OnDocumentLoaded(document => BuildRailAsync(document, viewer, status).FireAndForget());

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(ThumbnailsSample), UIcons.Apps, "A thumbnail rail off one shared document")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("PdfJs.PageCanvas() paints one page into a canvas. Given a Url it opens its own document and releases it on teardown; given a Document it borrows one and leaves it alone."),
                        TextBlock("That second form is what a rail wants. A document holds a worker-side copy of the whole file, so a twelve-page rail built the first way costs twelve copies of the same PDF - and this page's rail shares the document the viewer beside it already opened, so the count of documents is one.").MT(8),
                        TextBlock("Each canvas fits its own container's width by default and re-fits when that container resizes, so a rail in a resizable pane needs no arithmetic from the host.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Who owns the document is the thing to get right. A canvas releases only what it opened itself, because releasing a borrowed document would take the rest of the rail down with it - so whoever opened it has to release it, which on this page is the viewer that owns it."),
                        TextBlock("Every re-render supersedes the one before it. Scrolling a rail, or dragging its pane, produces far more render requests than paints, and each canvas cancels its in-flight render before starting the next - a cancelled render being an ordinary outcome rather than a failure.").MT(8),
                        TextBlock("Thumbnails deliberately draw no annotation layer and no text layer. Nobody selects text in a 160px preview, and a layer per page costs DOM nodes proportional to the words on it.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        status,
                        HStack().WS().H(560).Gap(12.px()).MT(8).Children(
                            VStack().W(180).HS().ScrollY().Children(_rail),
                            viewer.HS().Grow()),
                        Raw(_host),
                        SampleHint("Click a thumbnail to jump to its page. Watch the network tab: the document is fetched once, not once per thumbnail.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(PageRenderSample), typeof(DocumentViewerSample), typeof(OutlineAndNavigationSample));
        }

        private async Task BuildRailAsync(PdfDocument document, PdfViewer viewer, TextBlock status)
        {
            // Already built - the viewer reloads its document on a remount, and the rail should not
            // be rebuilt on top of itself.
            if (_document == document) return;

            _document = document;

            _rail.Clear();

            for (var pageNumber = 1; pageNumber <= document.PageCount; pageNumber++)
            {
                var captured = pageNumber;

                // Borrowed, not opened: the viewer owns this document and will release it.
                var thumbnail = PdfJs.PageCanvas()
                   .Document(document)
                   .Page(captured)
                   .FitWidth();

                var tile = VStack().WS().Gap(2.px()).Style(style => style.cursor = "pointer")
                   .Children(
                        thumbnail.WS().H(200),
                        TextBlock("Page " + captured).Tiny().Secondary());

                // A Stack is not clickable, so the listener goes on its element - which is also the
                // honest place for it: nothing about a thumbnail rail is a Tesserae button.
                tile.Render().addEventListener("click", new System.Action<Event>(_ => viewer.GoToPage(captured)));

                _rail.Add(tile);
            }

            status.Text = $"{document.PageCount} thumbnails, all painted from the one document the viewer opened.";

            await Task.CompletedTask;
        }

        public HTMLElement Render() => _content.Render();
    }
}
