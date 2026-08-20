using Transpose;

namespace Tesserae.Pdf
{
    /// <summary>
    /// The payloads pdf.js's viewer puts on the event bus.
    ///
    /// Every one of them also carries a <c>source</c> - the object that raised it - which is how
    /// pdf.js's own pieces tell their events apart. Only the fields this package reads are declared;
    /// the events themselves are named as constants on <see cref="PdfViewerEvents"/>.
    ///
    /// A handler is registered as <c>Action&lt;object&gt;</c> and casts, because the bus is untyped
    /// on the JavaScript side. A cast between <c>[External]</c> types is erased, so the cast costs
    /// nothing and buys the field names.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    public interface IPageChangingEvent
    {
        /// <summary>The 1-based page now in view.</summary>
        int pageNumber { get; }

        /// <summary>Its label, when the document supplies labels. Null otherwise.</summary>
        string pageLabel { get; }

        /// <summary>The page that was in view before.</summary>
        int previous { get; }
    }

    /// <summary>Raised whenever the zoom changes, however it changed.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IScaleChangingEvent
    {
        /// <summary>The new scale as a number, where 1 is 100%.</summary>
        double scale { get; }

        /// <summary>
        /// The named preset that produced it - <c>"page-width"</c>, <c>"auto"</c>, ... - or null when
        /// the zoom was set to a plain number.
        ///
        /// Worth keeping: it is the difference between "fit the width" and "112%", and only the
        /// former should be re-applied when the container resizes.
        /// </summary>
        string presetValue { get; }
    }

    /// <summary>Raised after every page has been rotated.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IRotationChangingEvent
    {
        int pagesRotation { get; }
        int pageNumber    { get; }
    }

    /// <summary>Raised once per page, each time that page finishes painting.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IPageRenderedEvent
    {
        /// <summary>The 1-based page that was painted.</summary>
        int pageNumber { get; }

        /// <summary>
        /// True when the page was scaled with a CSS transform rather than repainted - what happens
        /// during a zoom, before the sharp render catches up.
        /// </summary>
        bool cssTransform { get; }

        /// <summary>Set when the page failed to paint. Null on success.</summary>
        object error { get; }
    }

    /// <summary>
    /// Raised as the find controller works through the document, and again when it finishes. Carries
    /// the running count, which is what a "3 of 17" indicator shows.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    public interface IUpdateFindMatchesCountEvent
    {
        IMatchesCount matchesCount { get; }
    }

    /// <summary>
    /// Raised when a search finishes, or when its state changes. This is the one that says whether
    /// anything was found.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    public interface IUpdateFindControlStateEvent
    {
        /// <summary>A <see cref="FindState"/>: found, not found, wrapped around, or still searching.</summary>
        int state { get; }

        /// <summary>True when this is a report about the previous search rather than the current one.</summary>
        bool previous { get; }

        /// <summary>
        /// What was searched for, as the caller wrote it. pdf.js 6 puts this on the event directly;
        /// earlier versions only had it behind <c>source.state.query</c>.
        /// </summary>
        object rawQuery { get; }

        IMatchesCount matchesCount { get; }
    }

    /// <summary>How many matches there are, and which one is selected.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IMatchesCount
    {
        /// <summary>The 1-based index of the selected match, or 0 when there is none.</summary>
        int current { get; }

        /// <summary>How many matches there are in the whole document.</summary>
        int total { get; }
    }

    /// <summary>Raised when the annotation editor's active tool changes.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IAnnotationEditorModeChangedEvent
    {
        /// <summary>The new mode, as an <see cref="AnnotationEditorMode"/> value.</summary>
        int mode { get; }
    }

    /// <summary>
    /// The event names on pdf.js's bus that this package listens to or raises.
    ///
    /// Constants rather than strings at each call site: a typo in an event name is silent - the
    /// listener is simply never called - which is the single most annoying way for viewer wiring to
    /// fail.
    /// </summary>
    public static class PdfViewerEvents
    {
        /// <summary>
        /// The first page has been laid out. <b>The only safe point to apply an initial page, zoom,
        /// rotation or layout mode</b>: before it the viewer has no pages to apply them to and
        /// silently keeps its defaults.
        /// </summary>
        public const string PagesInit = "pagesinit";

        /// <summary>Every page has been laid out.</summary>
        public const string PagesLoaded = "pagesloaded";

        /// <summary>The page in view changed.</summary>
        public const string PageChanging = "pagechanging";

        /// <summary>A page finished painting.</summary>
        public const string PageRendered = "pagerendered";

        /// <summary>The zoom changed.</summary>
        public const string ScaleChanging = "scalechanging";

        /// <summary>The rotation changed.</summary>
        public const string RotationChanging = "rotationchanging";

        /// <summary>A search's running match count.</summary>
        public const string UpdateFindMatchesCount = "updatefindmatchescount";

        /// <summary>A search's outcome.</summary>
        public const string UpdateFindControlState = "updatefindcontrolstate";

        /// <summary>The annotation editor's tool changed.</summary>
        public const string AnnotationEditorModeChanged = "annotationeditormodechanged";

        /// <summary>The scripting sandbox came up. The readiness signal for embedded JavaScript.</summary>
        public const string SandboxCreated = "sandboxcreated";

        /// <summary>A form field was changed by the document's own JavaScript.</summary>
        public const string UpdateFromSandbox = "updatefromsandbox";

        /// <summary>Raised <b>by</b> a host to start, repeat or clear a search.</summary>
        public const string Find = "find";

        /// <summary>Raised <b>by</b> a host to drop the highlights and forget the search.</summary>
        public const string FindBarClose = "findbarclose";
    }
}
