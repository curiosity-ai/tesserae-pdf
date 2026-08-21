using Transpose;

namespace Tesserae.Pdf
{
    /// <summary>
    /// How <see cref="PdfViewerChrome"/> arranges its controls. Both layouts carry the same set of
    /// them and differ only in where they sit, so switching is a visual decision rather than a
    /// functional one.
    /// </summary>
    [Enum(Emit.Value)]
    public enum PdfChromeLayout
    {
        /// <summary>
        /// Everything in one 40px row above the document: panel toggles, page, zoom, fit, rotate,
        /// spread, and the search box pushed to the right. The default, and what a full-width reader
        /// wants.
        ///
        /// The search box gives up width before anything else does, and past that the row scrolls
        /// sideways rather than dropping a control - so a container narrower than about 900px is
        /// usable but cramped, and <see cref="IconRail"/> is the better answer there.
        /// </summary>
        SingleToolbar = 0,

        /// <summary>
        /// A slim top bar carrying the document's name, the page controls and search, with the view
        /// controls moved onto a 48px icon rail down the left.
        ///
        /// Worth choosing when the chrome has to survive a narrow container: the top row holds four
        /// things instead of a dozen, so it stops eliding much later.
        /// </summary>
        IconRail = 1,
    }

    /// <summary>Which side panel is showing.</summary>
    [Enum(Emit.Value)]
    public enum PdfChromePanel
    {
        /// <summary>None - the document has the full width.</summary>
        None = 0,

        /// <summary>The document's own outline, as a tree.</summary>
        Outline = 1,

        /// <summary>A grid of page thumbnails.</summary>
        Thumbnails = 2,
    }

    /// <summary>
    /// How strictly the search box matches - the <c>Fuzzy | Precise</c> pill.
    ///
    /// Two named modes rather than three checkboxes because that is the choice a person searching
    /// actually makes: "find roughly this" or "find exactly this". <see cref="FindOptions"/> is still
    /// there for a host that wants the individual switches.
    /// </summary>
    [Enum(Emit.Value)]
    public enum PdfSearchMode
    {
        /// <summary>
        /// Case-insensitive, substring, and diacritic-insensitive - so <c>cafe</c> finds
        /// <c>Café</c>. pdf.js's defaults, and the default here.
        /// </summary>
        Fuzzy = 0,

        /// <summary>Match case, whole words only, and diacritics respected.</summary>
        Precise = 1,
    }
}
