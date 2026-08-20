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
    }
}
