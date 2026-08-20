using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Pdf
{
    /// <summary>
    /// What <c>pdfjsLib.getDocument</c> takes. Built by <see cref="PdfSource.ToInitParameters"/>
    /// rather than by hand in most cases.
    ///
    /// An <c>[ObjectLiteral]</c> emits <b>only the fields actually assigned</b>, so this one type can
    /// carry pdf.js's whole option set without an unmentioned option ever overriding a pdf.js
    /// default with a C# zero. <c>new DocumentInitParameters { url = "a.pdf" }</c> is exactly
    /// <c>{ url: "a.pdf" }</c>.
    ///
    /// The field names are pdf.js's, not C#'s: this crosses the boundary as-is, so it is the one
    /// place in the package where JavaScript naming wins.
    /// </summary>
    [ObjectLiteral]
    public class DocumentInitParameters
    {
        /// <summary>The URL to fetch. Relative to the page unless absolute.</summary>
        public string url;

        /// <summary>
        /// The document's bytes, instead of a URL.
        ///
        /// A typed array is <b>transferred</b> to the worker, which takes ownership of it - the
        /// caller's view of it is detached and unusable afterwards. That is why
        /// <see cref="PdfSource.ToInitParameters"/> builds a fresh object on every call rather than
        /// caching one.
        /// </summary>
        public es5.Uint8Array data;

        /// <summary>Extra request headers, as a plain object of name to value.</summary>
        public object httpHeaders;

        /// <summary>
        /// Send cookies and authorization headers with a cross-origin fetch. Not needed
        /// same-origin, where the browser sends them anyway.
        /// </summary>
        public bool withCredentials;

        /// <summary>The password for an encrypted document, when it is known up front.</summary>
        public string password;

        /// <summary>
        /// Bytes per range request. pdf.js defaults to 65536; a larger value trades requests for
        /// latency on a big document over a slow link.
        /// </summary>
        public int rangeChunkSize;

        /// <summary>How much pdf.js logs. See <see cref="PdfVerbosity"/>.</summary>
        public PdfVerbosity verbosity;

        /// <summary>
        /// The base for resolving relative URLs found inside the document - in link annotations and
        /// outline entries that (incorrectly) carry a relative target.
        /// </summary>
        public string docBaseUrl;

        /// <summary>Where the CJK character maps are. Filled in from <see cref="PdfJs.CMapUrl"/>.</summary>
        public string cMapUrl;

        /// <summary>The shipped character maps are binary-packed, so this is always true.</summary>
        public bool cMapPacked;

        /// <summary>Where the standard fonts are. Filled in from <see cref="PdfJs.StandardFontDataUrl"/>.</summary>
        public string standardFontDataUrl;

        /// <summary>Where the wasm decoders are. Filled in from <see cref="PdfJs.WasmUrl"/>.</summary>
        public string wasmUrl;

        /// <summary>Where the ICC profiles are. Filled in from <see cref="PdfJs.IccUrl"/>.</summary>
        public string iccUrl;

        /// <summary>Substitute a system font for one the document does not embed. pdf.js defaults to true.</summary>
        public bool useSystemFonts;

        /// <summary>
        /// Let the worker fetch the asset directories itself, rather than routing them through the
        /// main thread. pdf.js defaults to true, which is what the asset URLs above assume.
        /// </summary>
        public bool useWorkerFetch;

        /// <summary>Use the wasm image decoders. pdf.js defaults to true and falls back on its own.</summary>
        public bool useWasm;

        /// <summary>
        /// Reject rather than recover when the document cannot be parsed. Off by default, which is
        /// why a damaged PDF usually still renders most of itself.
        /// </summary>
        public bool stopAtErrors;

        /// <summary>Skip images above this many pixels (width x height).</summary>
        public int maxImageSize;

        /// <summary>Draw text with canvas paths instead of loading its fonts as @font-face rules.</summary>
        public bool disableFontFace;

        /// <summary>Report extra font properties on the operator list. Diagnostic only.</summary>
        public bool fontExtraProperties;

        /// <summary>Render XFA forms, for the documents that use them instead of AcroForm.</summary>
        public bool enableXfa;

        /// <summary>Ask the browser for a hardware-accelerated canvas.</summary>
        public bool enableHWA;

        /// <summary>Expose pdf.js's internal timing on the page. Diagnostic only.</summary>
        public bool pdfBug;

        /// <summary>
        /// Fetch the whole document in one request rather than in ranges. Set this when the server
        /// does not honour <c>Range</c> - pdf.js discovers that itself, but only after a wasted
        /// round trip.
        /// </summary>
        public bool disableRange;

        /// <summary>Fetch the whole document before parsing, rather than streaming it.</summary>
        public bool disableStream;

        /// <summary>Fetch only what is needed to display the current page, and stop.</summary>
        public bool disableAutoFetch;
    }

    /// <summary>The scale and rotation to measure a page at.</summary>
    [ObjectLiteral]
    public class ViewportParameters
    {
        /// <summary>
        /// 1 means one PDF point per CSS pixel, which renders about 25% smaller than the page's
        /// paper size - a PDF point is 1/72 inch and a CSS pixel 1/96. Multiply by
        /// <c>PixelsPerInch.PDF_TO_CSS_UNITS</c> (4/3) for actual size.
        /// </summary>
        public double scale;

        /// <summary>Extra rotation in degrees, on top of the page's own. A multiple of 90.</summary>
        public int rotation;

        public double offsetX;
        public double offsetY;

        /// <summary>
        /// Keep PDF's y-axis (up) instead of flipping to the screen's (down). Almost never wanted -
        /// a viewport built this way paints the page upside down.
        /// </summary>
        public bool dontFlip;
    }

    /// <summary>What <c>PDFPageProxy.render</c> takes.</summary>
    [ObjectLiteral]
    public class RenderParameters
    {
        /// <summary>
        /// The canvas to paint into. pdf.js 6 takes the canvas itself and gets its own context;
        /// passing a context instead is the older shape.
        /// </summary>
        public HTMLCanvasElement canvas;

        /// <summary>The geometry to paint at. Its width and height are what the canvas should be.</summary>
        public IPageViewport viewport;

        /// <summary>
        /// <c>"display"</c> (the default) or <c>"print"</c>. Print intent renders annotations as
        /// they should appear on paper, which is not always how they appear on screen.
        /// </summary>
        public string intent;

        /// <summary>How much of the annotation layer to paint. See <see cref="AnnotationMode"/>.</summary>
        public AnnotationMode annotationMode;

        /// <summary>An extra transform applied before painting, as a 6-element matrix.</summary>
        public double[] transform;

        /// <summary>
        /// What to paint behind the page. Defaults to white; set it to <c>"transparent"</c> to
        /// composite the page over something else.
        /// </summary>
        public string background;

        /// <summary>Remaps the page's black and white. See <see cref="PageColors"/>.</summary>
        public PageColors pageColors;

        /// <summary>True while an annotation editor is active, which changes how widgets are drawn.</summary>
        public bool isEditing;
    }

    /// <summary>
    /// The two colours pdf.js will remap a page's black and white to, for rendering a document into
    /// a dark UI without inverting its images.
    ///
    /// Both are needed: pdf.js only applies the remapping when it has both, and only to content the
    /// document draws in pure black or white.
    /// </summary>
    [ObjectLiteral]
    public class PageColors
    {
        /// <summary>What the page's white becomes. Any CSS colour.</summary>
        public string background;

        /// <summary>What the page's black becomes. Any CSS colour.</summary>
        public string foreground;
    }

    /// <summary>What <c>PDFPageProxy.getTextContent</c> takes.</summary>
    [ObjectLiteral]
    public class GetTextContentParameters
    {
        /// <summary>
        /// Interleave marked-content markers with the text runs. Needed to reconstruct the
        /// document's structure; noise if you only want the words.
        /// </summary>
        public bool includeMarkedContent;

        /// <summary>
        /// Hand back the characters exactly as the document stores them, rather than pdf.js's
        /// normalised form. The normalised form is what makes search work across ligatures and
        /// combining marks, so leave this alone unless you are doing your own normalisation.
        /// </summary>
        public bool disableNormalization;
    }

    /// <summary>What <c>PDFPageProxy.getAnnotations</c> takes.</summary>
    [ObjectLiteral]
    public class GetAnnotationsParameters
    {
        /// <summary><c>"display"</c> or <c>"print"</c>; a PDF can hide an annotation from one of them.</summary>
        public string intent;
    }
}
