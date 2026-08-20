using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// Detaching a viewer and putting it back. The interesting part is what survives: the document,
    /// the page, the zoom mode and the rotation, all restored by the component rather than by the
    /// host.
    /// </summary>
    [SampleDetails(Group = "Runtime and hosting", Order = 40, Icon = UIcons.Refresh)]
    public class RemountSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public RemountSample()
        {
            var log    = VStack().WS().Gap(2.px());
            var host   = VStack().WS().H(480);
            var viewer = PdfJs.Viewer();

            viewer
               .Url(OUTLINE_PDF)
               .FitWidth()
               .OnDocumentLoaded(document => Log($"document loaded: {document.PageCount} pages"))
               .OnPageChanged(page => Log($"page changed to {page}"));

            host.Add(viewer.S());

            var detached = false;

            var toggle = Button("Detach the viewer").SetIcon(UIcons.Refresh);

            toggle.OnClick(() =>
            {
                if (detached)
                {
                    host.Add(viewer.S());
                    toggle.SetText("Detach the viewer");
                    Log("re-attached - the viewer rebuilds and restores its page, zoom and rotation");
                }
                else
                {
                    host.Clear();
                    toggle.SetText("Re-attach the viewer");
                    Log("detached - the viewer tore down and released its document");
                }

                detached = !detached;
            });

            void Log(string message)
            {
                log.Add(TextBlock(message).Tiny().Secondary());
            }

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(RemountSample), UIcons.Refresh, "Leaving the DOM, and coming back")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("A component is remountable. Leaving the DOM tears the viewer down - it has to, because a detached viewer holds a document and a worker thread - but the component re-arms itself, so being added back builds a new one and replays everything that was configured."),
                        TextBlock("That is what a component moved between containers needs, and what a parent that detaches rather than hides does to its children. It is also what the sample gallery does on every navigation, which is why every page here exercises this whether it means to or not.").MT(8),
                        TextBlock("Dispose() is the one-way door. It tears the viewer down and stops it being rebuilt, for a component that is genuinely finished with.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Restored across a remount: the document, the page in view, the zoom mode, the rotation, the scroll mode and the spread mode. Not restored: the pixel scroll offset within a page, and any search highlighting."),
                        TextBlock("The scroll offset is left out deliberately rather than forgotten. It means nothing at a different container size, and restoring it fights the fit mode that is about to be re-applied - so the page is restored and the position within it is not.").MT(8),
                        TextBlock("The document is re-fetched on the way back, which the browser will usually serve from cache. A viewer given bytes rather than a URL is the exception: a typed array is transferred to the worker and cannot be re-read, so pass PdfSource.FromBytes a fresh copy if a remount has to work.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        toggle,
                        host.MT(8),
                        SampleSubTitle("What happened"),
                        log,
                        SampleHint("Scroll to page 4 and zoom in, then detach and re-attach: the page comes back, and so does the zoom mode.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(ModalSample), typeof(SeveralDocumentsSample), typeof(ZoomAndFitSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
