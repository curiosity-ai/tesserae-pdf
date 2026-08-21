using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// A viewer inside a modal - which used to lock up the entire browser tab, and is here as the
    /// regression check for the fix. Open it, interact with it, close it, and watch the console: if
    /// this page ever stops responding, the wait in <c>PdfComponent</c> has been broken.
    /// </summary>
    [SampleDetails(Group = "Runtime and hosting", Order = 30, Icon = UIcons.WindowMaximize)]
    public class ModalSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public ModalSample()
        {
            var status = TextBlock("The modal has not been opened yet.").Small().Secondary();

            var open = Button("Open a viewer in a modal").SetIcon(UIcons.WindowMaximize).Primary().OnClick(() =>
            {
                var viewer = PdfJs.Viewer();

                viewer
                   .Url(OUTLINE_PDF)
                   .FitWidth()
                   .OnDocumentLoaded(document => status.Text = $"Opened in a modal: {document.PageCount} pages.")
                   .OnPageChanged(page => status.Text = $"Page {page} in the modal.");

                Modal("A document in a modal")
                   .Content(VStack().W(760).H(560).Children(viewer.S()))
                   .Show();
            });

            // The chrome is the same component with a toolbar around it, so it goes through the same
            // wait - and it puts more inside the animating ancestor (a panel, a thumbnail rail) than
            // the bare viewer does, which makes it the harder half of this regression check.
            var openChrome = Button("Open the chrome in a modal").SetIcon(UIcons.LayoutFluid).OnClick(() =>
            {
                var chrome = PdfJs.ViewerChrome()
                   .Url(OUTLINE_PDF)
                   .Panel(PdfChromePanel.Thumbnails)
                   .OnPanelChanged(panel => status.Text = $"Panel {panel} in the modal.");

                chrome.Viewer.FitWidth();

                Modal("A reader in a modal")
                   .Content(VStack().W(1040).H(620).Children(chrome.S()))
                   .Show();
            });

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(ModalSample), UIcons.WindowMaximize, "A viewer inside a modal, and the stall it used to cause")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("A viewer in a modal, a drawer, a popover or a panel that animates open is the ordinary case, and it needs nothing special from a host - the component handles it. This page exists because getting there took some finding out."),
                        TextBlock("What happened: opening a modal containing a pdf.js view froze the whole browser tab. requestAnimationFrame stopped firing, document.timeline stopped advancing, and every keystroke, click and screenshot hung waiting for a frame that never came, until Chromium killed the tab.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("The cause is a compositing one, and none of it is pdf.js's fault or Tesserae's. pdf.js sizes a page's scroll layer to millions of pixels square. Chromium rasters a layer that is running a composited transform animation over its whole subtree rather than the part in view, and picks the raster scale from the animation. A modal animation that starts at scale(0) - a singular matrix - with a layer that big inside it makes the raster work unbounded, and the renderer gives up producing frames for the entire page."),
                        TextBlock("The component's fix is to wait: it holds the view back until no ancestor is mid-animation. One frame is enough, because the animation is out of the dangerous scale range by its second - and waiting for the animation to finish also means pdf.js measures a container whose getBoundingClientRect is not being scaled, which is what its sizing reads. Bounded on both sides: an animation that never ends is ignored, and the wait gives up after a second.").MT(8),
                        TextBlock("The lesson worth keeping is diagnostic rather than technical. A frozen page is not necessarily a busy main thread: script answered instantly throughout this, which ruled out every main-thread hypothesis - an observer loop, DOM thrash, a dispose cycle - and none of them was it.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        HStack().WS().Gap(8.px()).Wrap().Children(open, openChrome),
                        status.MT(8),
                        SampleHint("Open either one, scroll it, select some text, close it, and open it again. The page should stay responsive throughout, and the console should stay clean. The second is the harder case: a panel of thumbnails is a dozen more pdf.js views inside the animating ancestor.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(RemountSample), typeof(ViewerChromeSample), typeof(SeveralDocumentsSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
