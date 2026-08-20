using System;
using System.Threading.Tasks;
using Transpose;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Pdf
{
    /// <summary>
    /// One page of a document, painted into a canvas - a thumbnail, a preview tile, a page in a
    /// contact sheet. No scrolling, no text layer, no annotation layer unless asked for: the cheapest
    /// way to put a page on screen.
    ///
    /// <code>
    /// PdfJs.PageCanvas().Document(shared).Page(3).FitWidth()
    /// </code>
    ///
    /// <b>Where the document comes from decides who owns it.</b> Given a
    /// <see cref="Source(PdfSource)"/> the component opens its own and releases it on teardown; given
    /// a <see cref="Document(PdfDocument)"/> it borrows one and leaves it alone. A rail of thumbnails
    /// wants the second: one document and twenty canvases, rather than twenty documents each with a
    /// worker-side copy of the same file.
    /// </summary>
    public sealed class PdfPageCanvas : PdfComponent
    {
        private readonly HTMLElement _host = DIV();

        private PdfSource   _source;
        private PdfDocument _document;
        private bool        _ownsDocument;
        private PdfRender   _render;
        private PdfPage     _page;

        private int    _pageNumber = 1;
        private double _scale;
        private bool   _fitWidth = true;
        private int    _rotation;
        private int    _renderGeneration;

        private PageColors           _pageColors;
        private AnnotationMode       _annotationMode = AnnotationMode.Disable;
        private Action<int>          _onRendered;
        private Action<PdfError>     _onError;

        internal PdfPageCanvas()
        {
            _host.style.width    = "100%";
            _host.style.height   = "100%";
            _host.style.overflow = "hidden";

            // A canvas is an inline element, so without this the host is three pixels taller than the
            // page it holds and a grid of them never lines up.
            _host.style.display        = "flex";
            _host.style.alignItems     = "flex-start";
            _host.style.justifyContent = "center";
        }

        /// <summary>Opens a document of its own, and releases it when the component is torn down.</summary>
        public PdfPageCanvas Source(PdfSource source)
        {
            _source       = source;
            _ownsDocument = true;

            if (IsCreated) ReloadAsync().FireAndForget();

            return this;
        }

        /// <summary>Shows a page of a document of its own, by URL.</summary>
        public PdfPageCanvas Url(string url) => Source(PdfSource.FromUrl(url));

        /// <summary>
        /// Borrows a document somebody else owns, and does not release it.
        ///
        /// This is what a rail of thumbnails wants: open the document once, hand it to every canvas,
        /// and release it yourself when the rail goes away.
        /// </summary>
        public PdfPageCanvas Document(PdfDocument document)
        {
            _source       = null;
            _ownsDocument = false;
            _document     = document;

            if (IsCreated) ReloadAsync().FireAndForget();

            return this;
        }

        /// <summary>Which page to show, 1-based.</summary>
        public PdfPageCanvas Page(int pageNumber)
        {
            _pageNumber = pageNumber;

            if (IsCreated) RenderAsync().FireAndForget();

            return this;
        }

        /// <summary>
        /// Paints at a fixed scale, where 1 is one PDF point per CSS pixel. Turns off fitting.
        /// </summary>
        public PdfPageCanvas Scale(double scale)
        {
            _scale    = scale;
            _fitWidth = false;

            if (IsCreated) RenderAsync().FireAndForget();

            return this;
        }

        /// <summary>
        /// Fits the page to the container's width, and re-fits it when the container resizes. The
        /// default.
        /// </summary>
        public PdfPageCanvas FitWidth()
        {
            _fitWidth = true;

            if (IsCreated) RenderAsync().FireAndForget();

            return this;
        }

        /// <summary>Extra rotation in degrees, on top of the page's own.</summary>
        public PdfPageCanvas Rotation(int degrees)
        {
            _rotation = ((degrees % 360) + 360) % 360;

            if (IsCreated) RenderAsync().FireAndForget();

            return this;
        }

        /// <summary>
        /// Draws the page's annotations as well as its content. Off by default - a thumbnail rarely
        /// wants them, and they cost a layer per page.
        ///
        /// <see cref="AnnotationMode.EnableStorage"/> is the useful one here: a canvas has no inputs
        /// to make interactive, and that mode is what includes values a user has typed into a viewer
        /// elsewhere.
        /// </summary>
        public PdfPageCanvas Annotations(AnnotationMode mode)
        {
            _annotationMode = mode;

            if (IsCreated) RenderAsync().FireAndForget();

            return this;
        }

        /// <summary>Remaps the page's black and white, for a dark UI. Both colours are needed.</summary>
        public PdfPageCanvas PageColors(string background, string foreground)
        {
            _pageColors = new PageColors { background = background, foreground = foreground };

            if (IsCreated) RenderAsync().FireAndForget();

            return this;
        }

        /// <summary>Called with the page number each time a paint finishes.</summary>
        public PdfPageCanvas OnRendered(Action<int> handler)
        {
            _onRendered = handler;

            return this;
        }

        /// <summary>
        /// Called when the document could not be opened or the page could not be painted. A cancelled
        /// render is not a failure and does not reach this.
        /// </summary>
        public PdfPageCanvas OnError(Action<PdfError> handler)
        {
            _onError = handler;

            return this;
        }

        protected override void CreateCore(HTMLElement container)
        {
            container.appendChild(_host);

            ReloadAsync().FireAndForget();
        }

        protected override void OnResized()
        {
            // Only a fit needs re-painting on resize; a fixed scale is a fixed scale.
            if (_fitWidth) RenderAsync().FireAndForget();
        }

        protected override void BeforeDispose()
        {
            _render?.Cancel();
            _render = null;
            _page   = null;
        }

        protected override void DisposeCore()
        {
            // Only what this component opened. A borrowed document belongs to whoever handed it over,
            // and releasing it here would take the rest of a thumbnail rail down with it.
            if (_ownsDocument && _document is object)
            {
                _document.DestroyAsync().FireAndForget();
                _document = null;
            }

            _host.innerHTML = "";

            if (_host.parentElement is object) _host.parentElement.removeChild(_host);
        }

        private async Task ReloadAsync()
        {
            if (_source is object)
            {
                var previous = _document;

                _document = null;

                if (_ownsDocument && previous is object) await previous.DestroyAsync();

                try
                {
                    _document = await PdfJs.OpenAsync(_source);
                }
                catch (Exception exception)
                {
                    Report(PdfError.FromJs(exception));

                    return;
                }
            }

            await RenderAsync();
        }

        private async Task RenderAsync()
        {
            if (_document is null || IsDisposed || !IsCreated) return;

            // Every re-render supersedes the one before it: a rail being scrolled, or a container
            // being dragged, produces far more requests than paints.
            var generation = ++_renderGeneration;

            _render?.Cancel();

            try
            {
                var pageNumber = Math.Max(1, Math.Min(_pageNumber, _document.PageCount));

                if (_page is null || _page.PageNumber != pageNumber)
                {
                    _page = await _document.GetPageAsync(pageNumber);

                    if (generation != _renderGeneration || !IsCreated) return;
                }

                var scale = _fitWidth ? FitScale(_page) : (_scale > 0 ? _scale : 1);

                if (scale <= 0) return;

                var viewport = _page.GetViewport(scale, _rotation);
                var canvas   = document.createElement("canvas").As<HTMLCanvasElement>();

                // Device pixels in the canvas, CSS pixels on screen: without this a thumbnail is
                // painted at 1x and stretched, which is exactly where it shows.
                var painted = viewport.ForDevicePixelRatio();

                canvas.style.width   = viewport.Width  + "px";
                canvas.style.height  = viewport.Height + "px";
                canvas.style.display = "block";

                _render = _page.Render(canvas, painted, _pageColors, _annotationMode);

                if (!await _render.CompletedAsync()) return;

                // Superseded while painting, or torn down.
                if (generation != _renderGeneration || !IsCreated) return;

                // Swapped in only once the new canvas is complete, so a resize never blanks what is
                // already on screen.
                _host.innerHTML = "";
                _host.appendChild(canvas);

                _onRendered?.Invoke(_page.PageNumber);
            }
            catch (Exception exception)
            {
                var error = PdfError.FromJs(exception);

                if (error.IsCancellation) return;

                Report(error);
            }
        }

        /// <summary>The scale that makes the page as wide as the container.</summary>
        private double FitScale(PdfPage page)
        {
            var available = _host.clientWidth;

            // A container that has not been laid out yet measures zero. Returning zero skips the
            // paint; the ResizeObserver fires as soon as it has a size and brings us back.
            if (available <= 0) return 0;

            var unscaled = page.GetViewport(1, _rotation);

            return unscaled.Width > 0 ? available / unscaled.Width : 0;
        }

        private void Report(PdfError error)
        {
            if (_onError is object)
            {
                _onError(error);

                return;
            }

            console.error("Tesserae.Pdf: could not render the page", error.Kind.ToString(), error.Message);
        }
    }
}
