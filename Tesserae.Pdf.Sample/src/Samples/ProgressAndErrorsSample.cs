using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// What a load reports while it happens, and what it reports when it fails. The failure half is
    /// the point: pdf.js's error types cannot be told apart with a type test, so the package sorts
    /// them into a small enum a host can branch on.
    /// </summary>
    [SampleDetails(Group = "Viewer", Order = 90, Icon = UIcons.TriangleWarning)]
    public class ProgressAndErrorsSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public ProgressAndErrorsSample()
        {
            var progress = TextBlock("-").Small().Secondary();
            var outcome  = TextBlock("-").Small();

            var viewer = PdfJs.Viewer();

            viewer
               .FitWidth()
               .OnProgress((loaded, total) =>
               {
                   // pdf.js's own percent is NaN when the response has no content length, so the two
                   // numbers are handed over raw and the decision about what to show is the host's.
                   progress.Text = total > 0
                       ? $"{loaded:0} of {total:0} bytes ({loaded / total * 100:0}%)"
                       : $"{loaded:0} bytes, total unknown - the server sent no content length";
               })
               .OnDocumentLoaded(document => outcome.Text = $"Loaded: {document.PageCount} page(s).")
               .OnError(error =>
               {
                   outcome.Text = error.Kind == PdfErrorKind.Response
                       ? $"{error.Kind}: HTTP {error.Status}, missing = {error.Missing} - {error.Message}"
                       : $"{error.Kind} ({error.Name}): {error.Message}";
               });

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(ProgressAndErrorsSample), UIcons.TriangleWarning, "Load progress, and typed failures")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("OnProgress reports bytes loaded and bytes total as a document downloads. OnError reports a PdfError, which carries a Kind - Password, InvalidPdf, Response, Aborted, RenderingCancelled or Unknown - plus the HTTP status and a missing flag when the failure was a fetch."),
                        TextBlock("Without an error handler, failures go to console.error. That is the right default for a component, and not one any user ever sees, so wire it up.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("pdf.js's exception classes derive from a pseudo-class rather than from Error, so they cannot be told apart with a type test from outside its bundle - a test reads metadata that is not there and throws instead of answering false. Their name string is the discriminator, and PdfError.FromJs is the single place this package reads it."),
                        TextBlock("The names moved in pdf.js 5: MissingPDFException and UnexpectedResponseException both became ResponseException, which carries the status instead of encoding it in the type. Both old names are still recognised, so a Kind means the same thing across versions.").MT(8),
                        TextBlock("Total is 0 more often than you would expect - any response without a Content-Length, which includes most chunked ones. That is also when pdf.js's own percent field comes back as NaN, which is why this API hands over the two numbers rather than a percentage.").MT(8),
                        TextBlock("Two Kinds are not really failures. RenderingCancelled is what happens when the user scrolls away mid-paint, and Aborted is a load that was superseded; PdfError.IsCancellation covers both, and neither is worth showing anybody.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        HStack().WS().Gap(8.px()).Wrap().Children(
                            Button("Load a real document").SetIcon(UIcons.FilePdf).OnClick(() => viewer.Url(IMAGES_PDF)),
                            Button("404: no such file").SetIcon(UIcons.TriangleWarning).OnClick(() => viewer.Url(PDFS + "does-not-exist.pdf")),
                            Button("Not a PDF at all").SetIcon(UIcons.TriangleWarning).OnClick(() => viewer.Url("index.html")),
                            Button("Encrypted, no password").SetIcon(UIcons.Lock).OnClick(() => viewer.Url(PROTECTED_PDF))),
                        SampleSubTitle("Progress"),
                        progress,
                        SampleSubTitle("Outcome"),
                        outcome,
                        viewer.H(420).WS().MT(8),
                        SampleHint("The 404 should report Response with status 404 and missing = True. \"Not a PDF\" should report InvalidPdf. The encrypted one reports Password, because this page has no OnPassword handler."),
                        SampleHint("\"Not a PDF at all\" hands pdf.js this page's own index.html, so it logs a run of \"getHexString - ignoring invalid character\" warnings while trying to make sense of it before giving up. That noise is the test working.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(BytesAndPasswordsSample), typeof(SeveralDocumentsSample), typeof(LoadingAndAssetsSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
