using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Pdf
{
    /// <summary>
    /// What pdf.js's <c>PDFViewer</c> (and <c>PDFSinglePageViewer</c>) takes.
    ///
    /// <see cref="PdfViewer"/> fills this in and lets a host amend it through <c>Options(...)</c>.
    /// pdf.js has no equivalent of an editor's <c>updateOptions</c>, so every field here is decided
    /// once, when the viewer is built - which is why a change to one is replayed by rebuilding rather
    /// than by patching.
    ///
    /// An <c>[ObjectLiteral]</c> emits only the fields actually assigned, so an option left alone
    /// keeps pdf.js's default rather than being overwritten with a C# zero.
    /// </summary>
    [ObjectLiteral]
    public class PdfViewerOptions
    {
        /// <summary>
        /// The scrollable element. pdf.js requires it to be positioned - it reads
        /// <c>offsetParent</c> off its pages - and to be the thing that scrolls, so
        /// <see cref="PdfViewer"/> builds it rather than taking one.
        /// </summary>
        public HTMLElement container;

        /// <summary>
        /// The element pages are appended to, inside the container. Must carry the
        /// <c>pdfViewer</c> class for pdf.js's stylesheet to reach it.
        /// </summary>
        public HTMLElement viewer;

        /// <summary>Required. How the whole stack talks to itself and to the host.</summary>
        public EventBus eventBus;

        /// <summary>Resolves links, destinations and page labels. Without it, links do nothing.</summary>
        public PdfLinkServiceJs linkService;

        /// <summary>Runs searches. Without it, dispatching <c>"find"</c> does nothing.</summary>
        public PdfFindControllerJs findController;

        /// <summary>Runs the document's embedded JavaScript. Supplying one is what enables scripting.</summary>
        public PdfScriptingManagerJs scriptingManager;

        /// <summary>
        /// Localizes the strings pdf.js puts in the DOM. Left unset, pdf.js builds its own
        /// English-only implementation.
        /// </summary>
        public object l10n;

        /// <summary>Whether text is selectable, and whether the document's permissions get a say. See <see cref="TextLayerMode"/>.</summary>
        public TextLayerMode textLayerMode;

        /// <summary>How much of the annotation layer to build. See <see cref="AnnotationMode"/>.</summary>
        public AnnotationMode annotationMode;

        /// <summary>Which annotation-editing tool is active. See <see cref="AnnotationEditorMode"/>.</summary>
        public AnnotationEditorMode annotationEditorMode;

        /// <summary>
        /// Where the annotation layer loads its icons from - the note, comment and attachment
        /// glyphs. Filled in from <see cref="PdfJs.ImageResourcesPath"/>; wrong, and those
        /// annotations render as broken images.
        /// </summary>
        public string imageResourcesPath;

        /// <summary>Drops the shadow and margin pdf.js draws around each page.</summary>
        public bool removePageBorders;

        /// <summary>
        /// The largest canvas pdf.js will paint a page into, in pixels. Above it, pages are painted
        /// smaller and scaled up - which is how a very large page stays renderable at high zoom.
        /// </summary>
        public double maxCanvasPixels;

        /// <summary>
        /// Let the document's own permissions decide whether text can be selected and copied.
        ///
        /// Off by default, which is deliberate rather than lax: this is the flag that turns a
        /// document's "no copying" request into a real restriction on your users, and a viewer that
        /// silently refuses to let people select text is a support ticket.
        /// </summary>
        public bool enablePermissions;

        /// <summary>Remaps the page's black and white. See <see cref="Pdf.PageColors"/>.</summary>
        public PageColors pageColors;

        /// <summary>Whether a pinch gesture zooms. On by default in pdf.js.</summary>
        public bool supportsPinchToZoom;

        /// <summary>Turn bare URLs in the text into working links, as well as real link annotations.</summary>
        public bool enableAutoLinking;
    }

    /// <summary>What pdf.js's <c>PDFLinkService</c> takes.</summary>
    [ObjectLiteral]
    public class PdfLinkServiceOptions
    {
        public EventBus eventBus;

        /// <summary>Where external links open. See <see cref="Pdf.LinkTarget"/>.</summary>
        public LinkTarget externalLinkTarget;

        /// <summary>The <c>rel</c> put on external links.</summary>
        public string externalLinkRel;

        /// <summary>
        /// Navigate to a destination's page without applying the zoom it asks for. Worth setting
        /// when the host controls the zoom: a document's destinations can otherwise yank the view to
        /// 400% on a click.
        /// </summary>
        public bool ignoreDestinationZoom;
    }

    /// <summary>What pdf.js's <c>PDFFindController</c> takes.</summary>
    [ObjectLiteral]
    public class PdfFindControllerOptions
    {
        public PdfLinkServiceJs linkService;
        public EventBus         eventBus;

        /// <summary>How long to wait after the query changes before searching, in ms. pdf.js uses 250.</summary>
        public int delay;

        /// <summary>
        /// Report the running match count as pages are scanned, rather than only when the whole
        /// document has been read. Makes a long document's counter move instead of appearing at the
        /// end.
        /// </summary>
        public bool updateMatchesCountOnProgress;
    }

    /// <summary>What <c>scrollPageIntoView</c> takes.</summary>
    [ObjectLiteral]
    public class ScrollPageIntoViewParameters
    {
        /// <summary>The 1-based page.</summary>
        public int pageNumber;

        /// <summary>
        /// An explicit destination array, to land somewhere specific on the page. pdf.js's own form,
        /// as it comes out of an outline entry.
        /// </summary>
        public object[] destArray;

        /// <summary>Ignore the zoom the destination asks for and keep the current one.</summary>
        public bool ignoreDestinationZoom;
    }

    /// <summary>What <c>increaseScale</c> / <c>decreaseScale</c> take.</summary>
    [ObjectLiteral]
    public class ScaleChangeParameters
    {
        /// <summary>
        /// Multiply the current scale by this. Mutually exclusive with <see cref="steps"/> - pdf.js
        /// uses whichever it finds, and prefers this one.
        /// </summary>
        public double scaleFactor;

        /// <summary>Move this many steps along pdf.js's own zoom ladder.</summary>
        public int steps;

        /// <summary>Hold the page still under this point, as <c>[x, y]</c> in client coordinates.</summary>
        public double[] origin;

        /// <summary>Wait this many ms before repainting sharply, so a fast sequence of zooms coalesces.</summary>
        public int drawingDelay;
    }

    /// <summary>
    /// The payload of the <c>"find"</c> event - the only way to drive pdf.js's find controller,
    /// which has no method for it.
    /// </summary>
    [ObjectLiteral]
    public class FindEventPayload
    {
        /// <summary>
        /// What kind of search this is. Empty or unset starts a new one; <c>"again"</c> moves to the
        /// next or previous match of the current one; <c>"highlightallchange"</c> re-applies the
        /// highlighting without re-searching.
        /// </summary>
        public string type;

        /// <summary>
        /// The query. A string, or an array of strings for a multi-term search where every term is
        /// matched independently.
        /// </summary>
        public object query;

        public bool caseSensitive;
        public bool entireWord;
        public bool highlightAll;

        /// <summary>With <c>type: "again"</c>, search backwards.</summary>
        public bool findPrevious;

        /// <summary>
        /// Treat accented and unaccented letters as different. pdf.js defaults to false, so "cafe"
        /// finds "café" - which is usually what a user means.
        /// </summary>
        public bool matchDiacritics;
    }
}
