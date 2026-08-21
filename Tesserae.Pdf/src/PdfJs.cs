namespace Tesserae.Pdf
{
    /// <summary>
    /// Static entry point for the pdf.js-backed components, mirroring Tesserae's <c>UI</c> class.
    ///
    /// Also owns everything that is global to pdf.js rather than per-viewer: loading the bundle, the
    /// asset URLs the worker fetches character maps, fonts and decoders from, the worker location,
    /// and the language the localization bridge reports. See <c>PdfJs.Runtime.cs</c>.
    ///
    /// Named <c>PdfJs</c> rather than <c>Pdf</c> because the namespace is already
    /// <c>Tesserae.Pdf</c>, and a type of the same name as its namespace is a lifetime of ambiguous
    /// error messages.
    /// </summary>
    public static partial class PdfJs
    {
        /// <summary>
        /// A scrollable, searchable, linkable document viewer - pdf.js's viewer components as a
        /// Tesserae component. The toolbar around it is yours; see <see cref="PdfViewer"/>.
        /// </summary>
        /// <param name="singlePage">
        /// Show one page at a time with no scrolling between pages, using pdf.js's
        /// <c>PDFSinglePageViewer</c>. Decided here rather than settable later, because it is a
        /// different pdf.js class - <see cref="Pdf.ScrollMode.Page"/> on the ordinary viewer is the
        /// closest equivalent that can be switched at runtime.
        /// </param>
        public static PdfViewer Viewer(bool singlePage = false) => new PdfViewer(singlePage);

        /// <summary>
        /// The same viewer with a toolbar already around it: page and zoom controls, fit modes,
        /// rotate and spread, an always-visible search box, and a tabbed outline / thumbnails panel.
        ///
        /// For an application that wants a document reader and does not want to have an opinion about
        /// what one looks like. <see cref="Viewer"/> is still the right call when the toolbar has to
        /// be yours - the chrome is a composition of that component's public surface and nothing
        /// more, and <c>PdfViewerChrome.Viewer</c> hands it back. See <see cref="PdfViewerChrome"/>.
        /// </summary>
        /// <param name="singlePage">
        /// As <see cref="Viewer"/>: one page at a time, with no scrolling between pages.
        /// </param>
        public static PdfViewerChrome ViewerChrome(bool singlePage = false) => new PdfViewerChrome(singlePage);

        /// <summary>
        /// One page of a document, painted into a canvas - a thumbnail, a preview tile, a page in a
        /// contact sheet. No scrolling and no text layer: the cheapest way to put a page on screen.
        /// See <see cref="PdfPageCanvas"/>.
        /// </summary>
        public static PdfPageCanvas PageCanvas() => new PdfPageCanvas();
    }
}
