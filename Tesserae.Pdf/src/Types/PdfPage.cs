using System;
using System.Text;
using System.Threading.Tasks;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Pdf
{
    /// <summary>
    /// One page of a <see cref="PdfDocument"/> - its size, its text, and the ability to paint it into
    /// a canvas.
    ///
    /// A page holds worker-side state, so it is worth releasing with <see cref="Cleanup"/> when a
    /// long-lived host is done with one. Reaching for the same page again is cheap; pdf.js caches it.
    /// </summary>
    public sealed class PdfPage
    {
        private readonly IPdfPageProxy _page;

        internal PdfPage(IPdfPageProxy page)
        {
            _page = page;
        }

        /// <summary>The 1-based page number.</summary>
        public int PageNumber => _page.pageNumber;

        /// <summary>
        /// The page's own rotation in degrees, as the PDF declares it. Already accounted for by
        /// <see cref="GetViewport"/> - this is only worth reading to show the user what it is.
        /// </summary>
        public int Rotation => _page.rotate;

        /// <summary>The page's width in CSS pixels at scale 1.</summary>
        public double Width => GetViewport(1).Width;

        /// <summary>The page's height in CSS pixels at scale 1.</summary>
        public double Height => GetViewport(1).Height;

        /// <summary>The underlying pdf.js page, for anything this wrapper does not cover.</summary>
        public IPdfPageProxy Instance => _page;

        /// <summary>
        /// The page's geometry at a scale, with an optional extra rotation on top of the page's own.
        ///
        /// Scale 1 is one PDF point per CSS pixel, which is about 25% smaller than the page's paper
        /// size. <see cref="ActualSizeScale"/> is the factor that makes them agree.
        /// </summary>
        public PdfViewport GetViewport(double scale, int extraRotation = 0)
        {
            var parameters = new ViewportParameters { scale = scale };

            if (extraRotation != 0) parameters.rotation = extraRotation + _page.rotate;

            return new PdfViewport(_page.getViewport(parameters));
        }

        /// <summary>
        /// The scale at which one CSS pixel is one PDF point of paper - 96/72, i.e. 4/3. Multiply a
        /// zoom percentage by this to get the scale a viewport wants.
        /// </summary>
        public static double ActualSizeScale => PdfJs.IsLoaded ? PdfJsLib.PixelsPerInch.PDF_TO_CSS_UNITS : 4d / 3d;

        /// <summary>
        /// Paints the page into a canvas, and hands back the in-flight render so it can be cancelled.
        ///
        /// The canvas is sized to the viewport by this call. Use this overload when the render has to
        /// be interruptible - a thumbnail rail being scrolled, a page being zoomed - and
        /// <see cref="RenderAsync"/> when it does not.
        /// </summary>
        public PdfRender Render(HTMLCanvasElement canvas, PdfViewport viewport, PageColors pageColors = null, AnnotationMode annotationMode = AnnotationMode.Enable)
        {
            if (canvas is null || viewport is null) return null;

            canvas.width  = (uint)Math.Round(viewport.Width);
            canvas.height = (uint)Math.Round(viewport.Height);

            var parameters = new RenderParameters
            {
                canvas         = canvas,
                viewport       = viewport.Instance,
                annotationMode = annotationMode,
            };

            if (pageColors is object) parameters.pageColors = pageColors;

            return new PdfRender(_page.render(parameters));
        }

        /// <summary>
        /// Paints the page into a canvas and waits for it.
        ///
        /// A cancelled render completes rather than faulting: cancellation is what happens when the
        /// view moves on, and a host that has to catch an exception for it ends up catching real
        /// failures too. The result says which it was.
        /// </summary>
        public async Task<bool> RenderAsync(HTMLCanvasElement canvas, PdfViewport viewport, PageColors pageColors = null, AnnotationMode annotationMode = AnnotationMode.Enable)
        {
            var render = Render(canvas, viewport, pageColors, annotationMode);

            if (render is null) return false;

            return await render.CompletedAsync();
        }

        /// <summary>The page's text runs and where they sit, for building a text layer or a search index.</summary>
        public Task<ITextContent> GetTextContentAsync(bool includeMarkedContent = false, bool disableNormalization = false)
        {
            var parameters = new GetTextContentParameters();

            if (includeMarkedContent) parameters.includeMarkedContent = true;
            if (disableNormalization) parameters.disableNormalization = true;

            return PromiseHelper.ToTask<ITextContent>(_page.getTextContent(parameters));
        }

        /// <summary>
        /// The page's text as one string, with the line breaks pdf.js reports.
        ///
        /// PDF stores text as positioned runs, not as prose: this concatenates them in content order,
        /// which is the order they were drawn in, and that is not always reading order - a
        /// multi-column layout interleaves. Good enough for search and indexing; not a substitute for
        /// a layout-aware extractor.
        /// </summary>
        public async Task<string> GetTextAsync()
        {
            var content = await GetTextContentAsync();

            if (content?.items is null) return "";

            var text = new StringBuilder();

            foreach (var item in content.items)
            {
                // A marked-content marker has no str; with includeMarkedContent off there should be
                // none, but a null here would concatenate as "undefined".
                if (item.str is null) continue;

                text.Append(item.str);

                if (item.hasEOL) text.Append("\n");
            }

            return text.ToString();
        }

        /// <summary>
        /// The page's annotations - links, form widgets, popups, stamps.
        ///
        /// A form field's current value is here, which makes this the way to read a filled form
        /// without saving it. There is one entry per widget, so a field drawn on two pages appears
        /// twice.
        /// </summary>
        /// <param name="intent">
        /// <c>"display"</c> (the default) or <c>"print"</c>. A PDF can hide an annotation from one of
        /// them, so the two answers legitimately differ.
        /// </param>
        public async Task<PdfAnnotation[]> GetAnnotationsAsync(string intent = null)
        {
            var parameters = new GetAnnotationsParameters();

            if (!string.IsNullOrWhiteSpace(intent)) parameters.intent = intent;

            // Awaited as object and cast: see the warning on PromiseHelper.ToTask about arrays of
            // [External] types as type arguments.
            var resolved = await PromiseHelper.ToTask<object>(_page.getAnnotations(parameters));
            var raw      = (IPdfAnnotation[])resolved;

            if (raw is null) return new PdfAnnotation[0];

            var annotations = new PdfAnnotation[raw.Length];

            for (var i = 0; i < raw.Length; i++)
            {
                annotations[i] = new PdfAnnotation(raw[i]);
            }

            return annotations;
        }

        /// <summary>
        /// Releases this page's worker-side caches. Answers false when a render is still running, in
        /// which case nothing was released - cancel it first.
        /// </summary>
        public bool Cleanup() => _page.cleanup(false);
    }

    /// <summary>
    /// A page's geometry at one scale and rotation: what size to make a canvas, and how PDF
    /// coordinates map into it.
    /// </summary>
    public sealed class PdfViewport
    {
        internal PdfViewport(IPageViewport viewport)
        {
            Instance = viewport;
        }

        /// <summary>The underlying pdf.js viewport, which is what a render or a text layer wants.</summary>
        public IPageViewport Instance { get; }

        /// <summary>The width in CSS pixels.</summary>
        public double Width => Instance.width;

        /// <summary>The height in CSS pixels.</summary>
        public double Height => Instance.height;

        /// <summary>The scale this was measured at.</summary>
        public double Scale => Instance.scale;

        /// <summary>The total rotation applied, in degrees.</summary>
        public int Rotation => Instance.rotation;

        /// <summary>The same page at a different scale.</summary>
        public PdfViewport AtScale(double scale) => new PdfViewport(Instance.clone(new ViewportParameters { scale = scale }));

        /// <summary>
        /// The same geometry scaled for a high-density display, so the canvas holds real device
        /// pixels rather than being upscaled and blurry. Set the canvas's CSS size to
        /// <see cref="Width"/>/<see cref="Height"/> of the original and its pixel size to this one's.
        /// </summary>
        public PdfViewport ForDevicePixelRatio()
        {
            var ratio = window.devicePixelRatio;

            return ratio > 1 ? AtScale(Scale * ratio) : this;
        }
    }

    /// <summary>
    /// A page render in flight. Held so it can be cancelled - which is the normal way a render ends
    /// when the user scrolls or zooms while one is still painting.
    /// </summary>
    public sealed class PdfRender
    {
        private readonly IRenderTask _task;
        private          bool        _cancelled;

        internal PdfRender(IRenderTask task)
        {
            _task = task;
        }

        /// <summary>Whether <see cref="Cancel"/> has been called on this render.</summary>
        public bool IsCancelled => _cancelled;

        /// <summary>
        /// Stops the render. Idempotent, and safe to call on one that has already finished.
        /// </summary>
        /// <param name="extraDelay">
        /// How long pdf.js should hold the page's resources before releasing them, in ms. Non-zero is
        /// worth it when the same page is about to be re-rendered at a different scale.
        /// </param>
        public void Cancel(int extraDelay = 0)
        {
            if (_cancelled) return;

            _cancelled = true;
            _task.cancel(extraDelay);
        }

        /// <summary>
        /// Waits for the render. Answers true when the page was painted and false when the render was
        /// cancelled; anything else throws a <see cref="PdfError"/>.
        /// </summary>
        public async Task<bool> CompletedAsync()
        {
            try
            {
                await PromiseHelper.ToTask(_task.promise);

                return true;
            }
            catch (Exception exception)
            {
                var error = PdfError.FromJs(exception);

                if (error.IsCancellation) return false;

                throw error;
            }
        }
    }
}
