using System;
using System.Threading.Tasks;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Pdf
{
    /// <summary>
    /// pdf.js's display API, as the compiler sees it. These are declarations of what pdf.js already
    /// is - nothing is emitted for them, so a member added here costs nothing at runtime and a
    /// mistyped one is a build error.
    ///
    /// Read-only payloads are interfaces of getters; anything this package constructs and hands to
    /// pdf.js is an <c>[ObjectLiteral]</c> in <c>src/Types/</c> instead.
    ///
    /// Every method that resolves asynchronously is typed <see cref="IPromise"/>, and every one of
    /// them goes through <see cref="PromiseHelper"/> rather than being awaited directly - see the
    /// warning there.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    public interface IPdfDocumentLoadingTask
    {
        /// <summary>Resolves with the <see cref="IPdfDocumentProxy"/>, or rejects with a pdf.js error.</summary>
        IPromise promise { get; }

        /// <summary>True once <see cref="destroy"/> has been called.</summary>
        bool destroyed { get; }

        /// <summary>pdf.js's own id for this load, unique per page.</summary>
        string docId { get; }

        /// <summary>
        /// Called as bytes arrive. <c>percent</c> can be <c>NaN</c> when the server sends no
        /// content length, so derive it from <c>loaded</c> and <c>total</c> instead.
        /// </summary>
        Action<IOnProgressParameters> onProgress { set; }

        /// <summary>
        /// Called when the document is encrypted, with a callback to hand the password back through
        /// and a reason from <see cref="PasswordReason"/>. Calling the callback with a wrong password
        /// calls this again with <see cref="PasswordReason.IncorrectPassword"/>; not calling it at all
        /// leaves the load pending until the task is destroyed.
        /// </summary>
        Action<Action<string>, int> onPassword { set; }

        /// <summary>
        /// Tears the load down and releases the worker's copy of the document.
        ///
        /// This is the whole teardown in pdf.js 6: <c>PDFDocumentProxy.destroy()</c> was removed, so
        /// the loading task is what owns the document's lifetime.
        /// </summary>
        IPromise destroy();

        /// <summary>The document's bytes as fetched, before any edits. Resolves with a Uint8Array.</summary>
        IPromise getData();
    }

    /// <summary>What pdf.js reports as a document downloads.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IOnProgressParameters
    {
        double loaded  { get; }
        double total   { get; }
        double percent { get; }
    }

    /// <summary>A loaded document. Pages are 1-based, everywhere in pdf.js.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IPdfDocumentProxy
    {
        int numPages { get; }

        /// <summary>
        /// The document's identity: its <c>/ID</c> pair, the second entry null for a document that
        /// has never been modified. Useful as a cache key.
        /// </summary>
        string[] fingerprints { get; }

        /// <summary>The 1-based page. Resolves with an <see cref="IPdfPageProxy"/>.</summary>
        IPromise getPage(int pageNumber);

        /// <summary>Resolves with the named destination's explicit destination array, or null.</summary>
        IPromise getDestination(string id);

        /// <summary>Resolves with a JavaScript <c>Map</c> of every named destination.</summary>
        IPromise getDestinations();

        /// <summary>Resolves with the document's page labels ("i", "ii", "1", ...), or null.</summary>
        IPromise getPageLabels();

        /// <summary>Resolves with the document's preferred page layout, e.g. <c>"TwoPageLeft"</c>.</summary>
        IPromise getPageLayout();

        /// <summary>Resolves with the document's preferred opening mode, e.g. <c>"UseOutlines"</c>.</summary>
        IPromise getPageMode();

        /// <summary>Resolves with the bookmark tree as an array of <see cref="IOutlineNode"/>, or null.</summary>
        IPromise getOutline();

        /// <summary>Resolves with <c>{ info, metadata }</c> - see <see cref="IMetadataResult"/>.</summary>
        IPromise getMetadata();

        /// <summary>Resolves with a JavaScript <c>Map</c> of embedded files, or null when there are none.</summary>
        IPromise getAttachments();

        /// <summary>
        /// Resolves with the permissions the document grants, as an array of
        /// <see cref="PdfPermission"/> values - or null when it places no restrictions at all, which
        /// is not the same as granting nothing.
        /// </summary>
        IPromise getPermissions();

        /// <summary>Resolves with <c>{ Marked, UserProperties, Suspects }</c>, or null.</summary>
        IPromise getMarkInfo();

        /// <summary>Resolves with the document's bytes as pdf.js holds them, as a Uint8Array.</summary>
        IPromise getData();

        /// <summary>
        /// Resolves with the document's bytes including anything written into form fields, as a
        /// Uint8Array. This is what "save a filled form" means.
        /// </summary>
        IPromise saveDocument();

        /// <summary>Resolves with the form fields keyed by name, or null when there is no AcroForm.</summary>
        IPromise getFieldObjects();

        /// <summary>Resolves true when the document carries embedded JavaScript.</summary>
        IPromise hasJSActions();

        /// <summary>
        /// Releases the worker-side caches for pages that are no longer displayed. Safe to call at
        /// any time; pdf.js re-fetches what it needs.
        /// </summary>
        IPromise cleanup(bool keepLoadedFonts);

        /// <summary>Where form-field values live, and what <c>saveDocument</c> writes out.</summary>
        IAnnotationStorage annotationStorage { get; }
    }

    /// <summary>
    /// pdf.js's in-memory record of what the user typed into a form. Reachable so a host can read a
    /// field's value without saving the whole document.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    public interface IAnnotationStorage
    {
        object getValue(string key, object defaultValue);
        void   setValue(string key, object value);
        int    size { get; }
    }

    /// <summary>One page of a loaded document.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IPdfPageProxy
    {
        int pageNumber { get; }

        /// <summary>The page's own rotation in degrees, from the PDF - 0, 90, 180 or 270.</summary>
        int rotate { get; }

        /// <summary>
        /// The page's <c>/UserUnit</c>, i.e. how many PDF points one unit of its content is. Almost
        /// always 1; a large-format drawing is where it is not.
        /// </summary>
        double userUnit { get; }

        /// <summary>The visible box in PDF units: <c>[x1, y1, x2, y2]</c>.</summary>
        double[] view { get; }

        /// <summary>The page's size and transform at a given scale and rotation.</summary>
        IPageViewport getViewport(ViewportParameters parameters);

        /// <summary>
        /// Paints the page into a canvas. Returns immediately; the paint finishes on the task's
        /// promise, and cancelling it rejects that promise with a
        /// <c>RenderingCancelledException</c>.
        /// </summary>
        IRenderTask render(RenderParameters parameters);

        /// <summary>Resolves with an <see cref="ITextContent"/> - the page's text runs and their positions.</summary>
        IPromise getTextContent(GetTextContentParameters parameters);

        /// <summary>The same content as a stream, for a page too large to hold at once.</summary>
        ReadableStream streamTextContent(GetTextContentParameters parameters);

        /// <summary>Resolves with the page's annotations: links, widgets, popups, stamps.</summary>
        IPromise getAnnotations(GetAnnotationsParameters parameters);

        /// <summary>Resolves with the page's tagged-PDF structure tree, for accessibility.</summary>
        IPromise getStructTree();

        /// <summary>Resolves with the page's embedded JavaScript actions, keyed by trigger.</summary>
        IPromise getJSActions();

        /// <summary>Releases this page's caches. Returns false when a render is still running.</summary>
        bool cleanup(bool resetStats);
    }

    /// <summary>
    /// A page's geometry at one scale and rotation: the pixel size to give a canvas, and the matrix
    /// that maps PDF coordinates into it.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    public interface IPageViewport
    {
        double   width     { get; }
        double   height    { get; }
        double   scale     { get; }
        int      rotation  { get; }
        double[] transform { get; }
        double[] viewBox   { get; }

        /// <summary>The same viewport at a different scale or rotation.</summary>
        IPageViewport clone(ViewportParameters parameters);

        /// <summary>Maps a point in PDF units to a point in this viewport, as <c>[x, y]</c>.</summary>
        double[] convertToViewportPoint(double x, double y);

        /// <summary>Maps a point in this viewport back to PDF units, as <c>[x, y]</c>.</summary>
        double[] convertToPdfPoint(double x, double y);
    }

    /// <summary>An in-progress page render.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IRenderTask
    {
        /// <summary>Resolves when the page has been painted, rejects when it was cancelled.</summary>
        IPromise promise { get; }

        /// <summary>
        /// Stops the render. The promise rejects with a <c>RenderingCancelledException</c>, which is
        /// an expected outcome rather than a failure - see <see cref="PdfErrorKind.RenderingCancelled"/>.
        /// </summary>
        void cancel(int extraDelay);

        /// <summary>
        /// Called between chunks, with a continuation to call when the caller is ready for the next
        /// one. Leaving it unset lets pdf.js paint as fast as it can.
        /// </summary>
        Action<Action> onContinue { set; }
    }

    /// <summary>A page's text, as extracted rather than as painted.</summary>
    [External]
    [Convention(Notation.None)]
    public interface ITextContent
    {
        /// <summary>
        /// The text runs, in content order. With <c>includeMarkedContent</c> set, marked-content
        /// markers are interleaved - those carry <c>type</c> and no <c>str</c>.
        /// </summary>
        ITextItem[] items { get; }

        /// <summary>The fonts the runs reference, keyed by the <c>fontName</c> on each item.</summary>
        object styles { get; }

        /// <summary>The document's language, when it declares one.</summary>
        string lang { get; }
    }

    /// <summary>One run of text on a page.</summary>
    [External]
    [Convention(Notation.None)]
    public interface ITextItem
    {
        /// <summary>The characters. Null on a marked-content marker.</summary>
        string str { get; }

        /// <summary>Writing direction, <c>"ltr"</c> or <c>"rtl"</c>.</summary>
        string dir { get; }

        /// <summary>The run's placement matrix, in PDF units.</summary>
        double[] transform { get; }

        double width  { get; }
        double height { get; }

        /// <summary>The key into <see cref="ITextContent.styles"/>.</summary>
        string fontName { get; }

        /// <summary>True when a line break follows this run - the only paragraph signal pdf.js gives.</summary>
        bool hasEOL { get; }

        /// <summary>Set on a marked-content marker instead of <see cref="str"/>.</summary>
        string type { get; }
    }

    /// <summary>One bookmark in the document's outline. <c>items</c> makes it a tree.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IOutlineNode
    {
        string title  { get; }
        bool   bold   { get; }
        bool   italic { get; }

        /// <summary>The bookmark's colour as RGB bytes, or null.</summary>
        es5.Uint8ClampedArray color { get; }

        /// <summary>
        /// Where it points: a named destination (a string) or an explicit destination (an array).
        /// Either is handed back to the link service as-is.
        /// </summary>
        object dest { get; }

        /// <summary>Set instead of <see cref="dest"/> when the bookmark is an external link.</summary>
        string url { get; }

        /// <summary>Whether the PDF asks for the link to open in a new window.</summary>
        bool newWindow { get; }

        /// <summary>
        /// The child count as the PDF declares it - negative when the branch should start collapsed.
        /// </summary>
        double count { get; }

        IOutlineNode[] items { get; }
    }

    /// <summary>What <c>getMetadata</c> resolves with.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IMetadataResult
    {
        /// <summary>
        /// The document information dictionary, with PDF's own PascalCase keys - <c>Title</c>,
        /// <c>Author</c>, <c>Producer</c>, <c>CreationDate</c>, <c>PDFFormatVersion</c>, ... - plus
        /// pdf.js's own <c>IsLinearized</c> / <c>IsAcroFormPresent</c> / <c>IsXFAPresent</c> flags.
        /// </summary>
        IDocumentInfo info { get; }

        /// <summary>The XMP metadata stream, when the document carries one.</summary>
        IMetadata metadata { get; }
    }

    /// <summary>
    /// The document information dictionary. Every entry is optional - a PDF is free to carry none of
    /// them - so each of these can come back null.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    public interface IDocumentInfo
    {
        string Title    { get; }
        string Author   { get; }
        string Subject  { get; }
        string Keywords { get; }
        string Creator  { get; }
        string Producer { get; }

        /// <summary>A PDF date string, e.g. <c>"D:20260501120000Z"</c>.</summary>
        string CreationDate { get; }

        /// <summary>A PDF date string.</summary>
        string ModDate { get; }

        string PDFFormatVersion  { get; }
        string Language          { get; }
        bool   IsLinearized      { get; }
        bool   IsAcroFormPresent { get; }
        bool   IsXFAPresent      { get; }
        bool   IsCollectionPresent { get; }
        bool   IsSignaturesPresent { get; }
    }

    /// <summary>The parsed XMP metadata stream.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IMetadata
    {
        /// <summary>One XMP property by name, e.g. <c>"dc:title"</c>.</summary>
        object get(string name);

        /// <summary>The stream as it was, unparsed.</summary>
        string getRaw();

        bool has(string name);
    }

    /// <summary>
    /// A pdf.js error, for reading the fields that say what went wrong.
    ///
    /// pdf.js's exception classes derive from a pseudo-class rather than <c>Error</c>, so they cannot
    /// be told apart with <c>instanceof</c> from outside the bundle - the <c>name</c> string is the
    /// discriminator, which is what <see cref="PdfError.FromJs"/> switches on.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    public interface IPdfJsError
    {
        /// <summary>e.g. <c>"PasswordException"</c>, <c>"ResponseException"</c>, <c>"InvalidPDFException"</c>.</summary>
        string name { get; }

        string message { get; }

        /// <summary>On a <c>PasswordException</c>: a <see cref="PasswordReason"/>.</summary>
        int code { get; }

        /// <summary>On a <c>ResponseException</c>: the HTTP status the fetch came back with.</summary>
        int status { get; }

        /// <summary>On a <c>ResponseException</c>: true when the status means the document is absent.</summary>
        bool missing { get; }

        /// <summary>On an <c>UnknownErrorException</c>: pdf.js's own diagnostic string.</summary>
        string details { get; }
    }
}
