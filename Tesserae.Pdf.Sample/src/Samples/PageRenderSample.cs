using System;
using System.Threading.Tasks;
using Transpose;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// One page, painted into a canvas of the host's own, with no viewer involved. This is the
    /// headless half of the package: the whole of <c>PdfJs.OpenAsync</c> to
    /// <c>page.RenderAsync(canvas, viewport)</c>, and nothing else.
    /// </summary>
    [SampleDetails(Group = "Pages", Order = 10, Icon = UIcons.Picture)]
    public class PageRenderSample : IComponent, ISample
    {
        private readonly IComponent  _content;
        private readonly HTMLElement _canvasHost = DIV();
        private readonly TextBlock   _status     = TextBlock("Loading...").Small().Secondary();

        private PdfDocument _document;
        private PdfRender   _render;
        private int         _page  = 1;
        private double      _scale = 1;

        public PageRenderSample()
        {
            _canvasHost.style.overflow = "auto";
            _canvasHost.style.maxWidth = "100%";

            // A slider bound to an observable rather than an input with a change handler: the
            // observable is what the render reads, so a drag that outruns the renders still ends up
            // painting the value it landed on.
            var scalePercent = new SettableObservable<int>(100);
            var scaleSlider  = Slider(100, 25, 300, 25).Bind(scalePercent).Width(200.px());

            scalePercent.ObserveFutureChanges(percent =>
            {
                _scale = percent / 100d;

                RenderAsync().FireAndForget();
            });

            var previous = Button("Previous").SetIcon(UIcons.AngleLeft).OnClick(() => Turn(-1));
            var next     = Button("Next").SetIcon(UIcons.AngleRight).OnClick(() => Turn(+1));

            void Turn(int by)
            {
                _page += by;

                RenderAsync().FireAndForget();
            }

            OpenAsync().FireAndForget();

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(PageRenderSample), UIcons.Picture, "A page painted into a canvas, with no viewer")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("PdfJs.OpenAsync(source) loads a document without putting anything on screen. From it, GetPageAsync(n) gives a page, GetViewport(scale) gives that page's size at a scale, and RenderAsync(canvas, viewport) paints it. Four calls, and the host owns the canvas."),
                        TextBlock("This is what a thumbnail, a preview tile, a print sheet or an image export is built on - anything that needs pixels rather than a scrollable document.").MT(8),
                        TextBlock("Scale 1 is one PDF point per CSS pixel, which comes out about a quarter smaller than the page's paper size: a point is 1/72 inch and a CSS pixel 1/96. PdfPage.ActualSizeScale is the 4/3 that makes them agree.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Open the document once and take pages off it. A document holds a worker-side copy of the whole file, so opening one per page costs a copy per page - and the caller owns every one of them until DestroyAsync releases it. This page opens one document and re-renders from it as you change the controls."),
                        TextBlock("Render(...) hands back the in-flight paint so it can be cancelled, which is what you want when the view is still moving: this page cancels the previous render before starting the next, and a cancelled render is a false rather than an exception. RenderAsync(...) is the same thing when there is nothing to interrupt.").MT(8),
                        TextBlock("On a high-density display, viewport.ForDevicePixelRatio() gives the canvas real device pixels while the CSS size stays the same - without it the page is painted at 1x and then upscaled, which looks soft at exactly the sizes people zoom into.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        HStack().WS().Gap(16.px()).Children(
                            VStack().Children(TextBlock("Page").Small().SemiBold(), HStack().Gap(4.px()).Children(previous, next)),
                            VStack().Children(TextBlock("Scale (%)").Small().SemiBold(), scaleSlider)),
                        _status.MT(8),
                        Raw(_canvasHost).MT(8),
                        SampleHint("Every change cancels the render in flight and starts another. The canvas is this page's own element - the package never created one.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(ThumbnailsSample), typeof(TextExtractionSample), typeof(DocumentViewerSample));

            // The document is this page's to release: the gallery tears a page down when you navigate
            // away, and a worker-side copy of the file would otherwise outlive every visit.
            DomObserver.WhenRemoved(_canvasHost, () => _document?.DestroyAsync().FireAndForget());
        }

        private async Task OpenAsync()
        {
            try
            {
                _document = await PdfJs.OpenAsync(IMAGES_PDF);

                await RenderAsync();
            }
            catch (PdfError error)
            {
                _status.Text = "Could not open the document: " + error.Message;
            }
        }

        private async Task RenderAsync()
        {
            if (_document is null) return;

            // Whatever is still painting is for a page or a scale nobody is looking at any more.
            _render?.Cancel();

            var page     = await _document.GetPageAsync(Math.Max(1, Math.Min(_page, _document.PageCount)));
            var viewport = page.GetViewport(_scale);
            var canvas   = document.createElement("canvas").As<HTMLCanvasElement>();

            // The canvas holds device pixels and is displayed at CSS pixels, which is what keeps a
            // page sharp on a retina display instead of being painted at 1x and stretched.
            var painted = viewport.ForDevicePixelRatio();

            canvas.style.width  = viewport.Width  + "px";
            canvas.style.height = viewport.Height + "px";
            canvas.style.border = "1px solid " + Theme.Default.Border;

            _render = page.Render(canvas, painted);

            _status.Text = $"Page {page.PageNumber} of {_document.PageCount} at scale {_scale:0.##} - {viewport.Width:0} x {viewport.Height:0} CSS px, canvas {painted.Width:0} x {painted.Height:0}";

            if (!await _render.CompletedAsync()) return;

            // Swapped in only once it is painted, so a cancelled render never blanks what is showing.
            _canvasHost.innerHTML = "";
            _canvasHost.appendChild(canvas);
        }

        public HTMLElement Render() => _content.Render();
    }
}
