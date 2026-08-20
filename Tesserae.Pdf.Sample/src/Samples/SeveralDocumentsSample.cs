using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// One viewer, several documents - the shape a file browser or a search-result preview has. The
    /// point of the page is the race: swapping documents faster than they load has to end on the one
    /// that was asked for last, not the one that happened to arrive last.
    /// </summary>
    [SampleDetails(Group = "Runtime and hosting", Order = 50, Icon = UIcons.Folders)]
    public class SeveralDocumentsSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public SeveralDocumentsSample()
        {
            var log    = VStack().WS().Gap(2.px());
            var viewer = PdfJs.Viewer();

            viewer
               .FitWidth()
               .Url(OUTLINE_PDF)
               .OnDocumentLoaded(document => Log($"loaded: {document.PageCount} pages, fingerprint {Short(document)}"))
               .OnError(error => Log($"failed: {error.Kind} - {error.Message}"));

            void Log(string message) => log.Add(TextBlock(message).Tiny().Secondary());

            string Short(PdfDocument document)
            {
                var fingerprint = document.Fingerprints is object && document.Fingerprints.Length > 0 ? document.Fingerprints[0] : "";

                return fingerprint.Length > 12 ? fingerprint.Substring(0, 12) : fingerprint;
            }

            var documents = new[]
            {
                new { Label = "Outline (12 pages)", Source = OUTLINE_PDF },
                new { Label = "Images (3 pages)",   Source = IMAGES_PDF },
                new { Label = "CJK (1 page)",       Source = CJK_PDF },
                new { Label = "AcroForm (1 page)",  Source = FORMS_PDF },
            };

            var buttons = HStack().WS().Gap(4.px()).Wrap();

            foreach (var item in documents)
            {
                var captured = item;

                buttons.Add(Button(captured.Label).OnClick(() =>
                {
                    Log("asked for " + captured.Label);
                    viewer.Url(captured.Source);
                }));
            }

            // Four swaps in a row, with no waiting: exactly the race the generation counter exists
            // for. Only the last one may end up on screen.
            buttons.Add(Button("Swap all four, fast").SetIcon(UIcons.Bolt).OnClick(() =>
            {
                Log("--- asking for all four in one go ---");

                foreach (var item in documents)
                {
                    viewer.Url(item.Source);
                }

                Log("asked for four; only " + documents[documents.Length - 1].Label + " should load");
            }));

            buttons.Add(Button("Clear").OnClick(() =>
            {
                Log("cleared");
                viewer.Clear();
            }));

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(SeveralDocumentsSample), UIcons.Folders, "One viewer, several documents, and the race between them")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Url(...) or Source(...) on a viewer that is already showing something replaces it: the previous document is released, its worker copy freed, and the new one loaded. Clear() releases without loading anything."),
                        TextBlock("This is a file browser's viewer, or a search result's preview pane - one component, whatever the user clicked on.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Swapping faster than documents load is normal, not an edge case: it is what holding down the arrow key in a file list does. The component counts the requests and drops the result of any load that has been superseded, so the viewer ends up showing what was asked for last rather than what arrived last."),
                        TextBlock("A superseded load is also not reported as an error, even though pdf.js rejects its promise. The host asked for the swap, so telling it that the document it abandoned failed to load is noise - the failure handler only hears about the load that is still wanted.").MT(8),
                        TextBlock("Each swap releases the previous document properly, in the order pdf.js needs: the viewer is told to let go first, then the loading task is destroyed. The other order leaves the viewer holding pages of a document that has gone.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        buttons,
                        viewer.H(480).WS().MT(8),
                        SampleSubTitle("What happened"),
                        log,
                        SampleHint("\"Swap all four, fast\" asks for four documents without waiting. The log should show exactly one load - the AcroForm - and no failures.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(RemountSample), typeof(ProgressAndErrorsSample), typeof(DownloadAndSaveSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
