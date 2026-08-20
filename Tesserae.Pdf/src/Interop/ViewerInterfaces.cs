using System;
using System.Threading.Tasks;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Pdf
{
    /// <summary>
    /// pdf.js's viewer components - the half of pdf.js that turns a document into a scrollable,
    /// searchable, linkable view, as opposed to the display API that turns a page into pixels.
    ///
    /// The two viewers pdf.js ships have the same surface and differ only in how they lay pages out,
    /// so that surface is declared once here and the classes below carry nothing but a constructor.
    /// <see cref="PdfViewer"/> holds whichever it built through this interface.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    public interface IPdfViewerInstance
    {
        /// <summary>
        /// Hands the viewer a document to show, or null to let go of the one it has. Everything else
        /// on this interface is meaningless until this has been called and <c>pagesinit</c> has
        /// fired.
        /// </summary>
        void setDocument(IPdfDocumentProxy document);

        /// <summary>The document being shown, or undefined.</summary>
        IPdfDocumentProxy pdfDocument { get; }

        /// <summary>How many pages the view has.</summary>
        int pagesCount { get; }

        /// <summary>
        /// The page in view, 1-based. Setting it scrolls there. Reading it during a scroll gives the
        /// page pdf.js considers current, which is the one occupying most of the viewport.
        /// </summary>
        int currentPageNumber { get; set; }

        /// <summary>
        /// The current page's label, when the document supplies labels ("iv", "A-3"). Null when it
        /// does not. Setting it navigates to the page carrying that label.
        /// </summary>
        string currentPageLabel { get; set; }

        /// <summary>The zoom as a number, where 1 is 100%.</summary>
        double currentScale { get; set; }

        /// <summary>
        /// The zoom as pdf.js's string form: a number, or one of <c>"auto"</c>,
        /// <c>"page-width"</c>, <c>"page-fit"</c>, <c>"page-height"</c>, <c>"page-actual"</c>.
        ///
        /// The named values are the ones worth setting: they are re-evaluated by pdf.js against the
        /// container, which a number is not.
        /// </summary>
        string currentScaleValue { get; set; }

        /// <summary>Extra rotation applied to every page, in degrees. A multiple of 90.</summary>
        int pagesRotation { get; set; }

        /// <summary>How pages are laid out. See <see cref="Pdf.ScrollMode"/>.</summary>
        int scrollMode { get; set; }

        /// <summary>Whether pages are paired, and on which side. See <see cref="Pdf.SpreadMode"/>.</summary>
        int spreadMode { get; set; }

        /// <summary>
        /// The annotation editor's state. Asymmetric: reading gives an object carrying a
        /// <c>mode</c>, writing takes one - which is why this is typed loosely here and wrapped in
        /// <see cref="PdfViewer"/>.
        /// </summary>
        object annotationEditorMode { get; set; }

        /// <summary>Resolves once the first page has been laid out.</summary>
        IPromise firstPagePromise { get; }

        /// <summary>Resolves once every page has been laid out.</summary>
        IPromise pagesPromise { get; }

        /// <summary>Scrolls to the next page. False when already on the last.</summary>
        bool nextPage();

        /// <summary>Scrolls to the previous page. False when already on the first.</summary>
        bool previousPage();

        /// <summary>
        /// Scrolls to a page, optionally to a destination within it. This is what a link, an outline
        /// entry and a search hit all go through.
        /// </summary>
        void scrollPageIntoView(ScrollPageIntoViewParameters parameters);

        /// <summary>Zooms in by a step, or by the factor the options name.</summary>
        void increaseScale(ScaleChangeParameters parameters);

        /// <summary>Zooms out by a step, or by the factor the options name.</summary>
        void decreaseScale(ScaleChangeParameters parameters);

        /// <summary>Re-lays out every page. Needed after something outside pdf.js changed its container.</summary>
        void update();

        /// <summary>Rebuilds every page's layers - after a theme change, or a page-colours change.</summary>
        void refresh(bool noUpdate);

        /// <summary>Releases the caches of pages that are no longer visible.</summary>
        void cleanup();

        /// <summary>Moves keyboard focus into the view, so the arrow keys scroll it.</summary>
        void focus();

        /// <summary>Every page's text, in one string. Resolves with null before the document is ready.</summary>
        IPromise getAllText();

        /// <summary>The page number carrying a label, or null when no page does.</summary>
        int pageLabelToPageNumber(string label);

        /// <summary>The scrollable element the viewer was given.</summary>
        HTMLElement container { get; }
    }

    /// <summary>
    /// The default viewer: every page in one scrolling column (or row, or wrapped grid - see
    /// <see cref="Pdf.ScrollMode"/>).
    ///
    /// Carries only its constructor, because the whole member surface is on
    /// <see cref="IPdfViewerInstance"/> and a cast between two <c>[External]</c> types is erased
    /// rather than emitted - so <c>(IPdfViewerInstance)(object)new PdfViewerJs(...)</c> compiles to
    /// nothing but the <c>new</c>.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    [Name("pdfjsViewer.PDFViewer")]
    internal class PdfViewerJs
    {
        public extern PdfViewerJs(PdfViewerOptions options);
    }

    /// <summary>
    /// The single-page viewer: one page at a time, with no scrolling between them.
    ///
    /// A different class rather than an option, because pdf.js implements it by overriding the
    /// layout half of <c>PDFViewer</c>. <c>ScrollMode.Page</c> on the ordinary viewer is the closest
    /// equivalent and is switchable at runtime, which this is not.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    [Name("pdfjsViewer.PDFSinglePageViewer")]
    internal class PdfSinglePageViewerJs
    {
        public extern PdfSinglePageViewerJs(PdfViewerOptions options);
    }

    /// <summary>
    /// pdf.js's own event bus. Everything the viewer stack tells anyone goes through one of these,
    /// and the pieces find each other by sharing it - which is why one is built per
    /// <see cref="PdfViewer"/> rather than one per page.
    ///
    /// Listeners are removed by <c>off</c> with the same function, so a subscription has to keep
    /// hold of the delegate it registered. <see cref="DisposableBag"/> is where those closures live.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    [Name("pdfjsViewer.EventBus")]
    public class EventBus
    {
        public extern EventBus();

        /// <summary>
        /// Subscribes to an event. pdf.js's own listeners always run before an external one, so the
        /// viewer has already reacted by the time a handler here is called.
        /// </summary>
        public extern void on(string eventName, Action<object> listener);

        /// <summary>Unsubscribes the same delegate that was passed to <see cref="on"/>.</summary>
        public extern void off(string eventName, Action<object> listener);

        /// <summary>
        /// Raises an event. This is how a host asks the stack to do something it has no method for -
        /// notably <c>"find"</c>, which is the only way to drive the find controller.
        /// </summary>
        public extern void dispatch(string eventName, object data);
    }

    /// <summary>
    /// Resolves the links and destinations inside a document: an outline entry, a link annotation, a
    /// named destination, a page label.
    ///
    /// It needs the viewer as well as the document, and in that order - <c>setViewer</c> before
    /// <c>setDocument</c> - because a destination is resolved against the document and then scrolled
    /// to through the viewer.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    [Name("pdfjsViewer.PDFLinkService")]
    public class PdfLinkServiceJs
    {
        public extern PdfLinkServiceJs(PdfLinkServiceOptions options);

        public extern void setViewer(object viewer);

        /// <summary>
        /// The document, plus the base URL relative links inside it resolve against. Pass null for
        /// the base URL to let the document's own <c>docBaseUrl</c> decide.
        /// </summary>
        public extern void setDocument(IPdfDocumentProxy document, string baseUrl);

        /// <summary>
        /// Navigates to a destination - either a name (a string) or an explicit destination array.
        /// This is what an outline entry's <c>Destination</c> is handed to unchanged.
        /// </summary>
        public extern IPromise goToDestination(object destination);

        /// <summary>Navigates to a page by number or by label.</summary>
        public extern void goToPage(object pageNumberOrLabel);

        /// <summary>Where external links open. See <see cref="Pdf.LinkTarget"/>.</summary>
        public extern int externalLinkTarget { get; set; }

        /// <summary>The <c>rel</c> attribute put on external links. Defaults to <c>"noopener noreferrer nofollow"</c>.</summary>
        public extern string externalLinkRel { get; set; }
    }

    /// <summary>
    /// The search engine. It reads the text of every page through the document and reports what it
    /// found on the event bus; it has no methods to start a search, because a search is started by
    /// dispatching <c>"find"</c>.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    [Name("pdfjsViewer.PDFFindController")]
    public class PdfFindControllerJs
    {
        public extern PdfFindControllerJs(PdfFindControllerOptions options);

        public extern void setDocument(IPdfDocumentProxy document);

        /// <summary>Which match is selected, as a page index and a match index within it.</summary>
        public extern ISelectedMatch selected { get; }

        /// <summary>Whether matches are currently highlighted.</summary>
        public extern bool highlightMatches { get; }
    }

    /// <summary>Which search match is current.</summary>
    [External]
    [Convention(Notation.None)]
    public interface ISelectedMatch
    {
        /// <summary>The 0-based page index. Note pdf.js is 1-based nearly everywhere else.</summary>
        int pageIdx { get; }

        /// <summary>The 0-based match index within that page.</summary>
        int matchIdx { get; }
    }

    /// <summary>
    /// pdf.js's built-in localization: an English Fluent bundle inlined into the viewer, with no
    /// network access.
    ///
    /// Used when a host opts out of the package's own TNT-backed bridge with <c>L10n(false)</c>.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    [Name("pdfjsViewer.GenericL10n")]
    internal class GenericL10nJs
    {
        public extern GenericL10nJs();
    }
}
