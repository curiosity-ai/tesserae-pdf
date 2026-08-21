using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// The viewer with a toolbar around it - which is the shape a host app actually ships, and the
    /// reason the package draws no toolbar of its own. Everything above the document here is ordinary
    /// Tesserae, calling ordinary methods on the component.
    /// </summary>
    [SampleDetails(Group = "Viewer", Order = 10, Icon = UIcons.FilePdf)]
    public class DocumentViewerSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public DocumentViewerSample()
        {
            var pageLabel = TextBlock("-").Small().W(110);
            var zoomLabel = TextBlock("-").Small().W(60);

            var viewer = PdfJs.Viewer();

            viewer
               .Url(OUTLINE_PDF)
               .FitWidth()
               .OnPageChanged(page => pageLabel.Text = $"Page {page} of {viewer.PageCount}")
               .OnScaleChanged(scale => zoomLabel.Text = (scale * 100).ToString("0") + "%")
               .OnDocumentLoaded(document => pageLabel.Text = $"Page 1 of {document.PageCount}")
               .OnError(error => pageLabel.Text = error.Kind + ": " + error.Message);

            var toolbar = HStack().WS().Gap(4.px()).Children(
                Button().SetIcon(UIcons.AngleLeft).Tooltip("Previous page").OnClick(() => viewer.PreviousPage()),
                Button().SetIcon(UIcons.AngleRight).Tooltip("Next page").OnClick(() => viewer.NextPage()),
                pageLabel,
                Button().SetIcon(UIcons.ZoomOut).Tooltip("Zoom out").OnClick(() => viewer.ZoomOut()),
                Button().SetIcon(UIcons.ZoomIn).Tooltip("Zoom in").OnClick(() => viewer.ZoomIn()),
                zoomLabel,
                Button("Fit width").OnClick(() => viewer.FitWidth()),
                Button("Fit page").OnClick(() => viewer.FitPage()),
                Button().SetIcon(UIcons.RotateRight).Tooltip("Rotate").OnClick(() => viewer.Rotate()));

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(DocumentViewerSample), UIcons.FilePdf, "The viewer, and a toolbar driving it")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("PdfJs.Viewer() builds pdf.js's viewer stack - an event bus, a link service, a find controller and the viewer itself - inside a scroll host of the shape pdf.js insists on. Give it a URL and it shows a document."),
                        TextBlock("What it does not build is a toolbar. Every control above this document is plain Tesserae calling plain methods, because a toolbar is the part that has to look like the rest of your application - and because the same viewer is asked for by a full-page reader, a preview pane and a modal, which want three different sets of buttons.").MT(8),
                        TextBlock("If a reader is what you want rather than a viewer to build one from, PdfJs.ViewerChrome() is this component with a toolbar already on it - see the Viewer Chrome page. It calls the same methods this toolbar does.").MT(8),
                        TextBlock("Pages, links, text selection, the annotation layer, keyboard scrolling and search all work with no further wiring. The document's outline, its metadata and its text are on the Document the viewer hands to OnDocumentLoaded.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("The viewer owns the document it opened and releases it when it is torn down - including the teardown that happens when it leaves the DOM. Navigate away from this page and back: the component rebuilds, and comes back on the page and zoom it was on."),
                        TextBlock("Give it a fixed height, or a parent that has one. The viewer fills its container and scrolls inside it; in a container of no height it renders nothing at all, which looks like a failure to load. This page gives it 600px.").MT(8),
                        TextBlock("Prefer the fit modes over an explicit zoom where you can. FitWidth and its siblings are re-applied as the container resizes, so a viewer in a resizable pane keeps fitting; an explicit Zoom(1.4) is a number and stays one.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        toolbar,
                        viewer.H(600).WS().MT(8),
                        SampleHint("Select some text, click a link in the outline document, or scroll with the arrow keys - all of that is pdf.js's, with nothing wired up here.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(ViewerChromeSample), typeof(ZoomAndFitSample), typeof(SearchSample), typeof(OutlineAndNavigationSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
