using Transpose;

namespace Tesserae.Pdf
{
    /// <summary>
    /// The global <c>pdfjsLib</c> object - pdf.js's display API - declared to the compiler instead of
    /// reached through <c>Script.Write</c>. Every member below is an <c>[External]</c> declaration:
    /// nothing is emitted for it, a call site compiles straight to the JavaScript it names, and a typo
    /// or a wrong argument becomes a build error rather than a runtime one.
    ///
    /// <c>[Convention(Notation.None)]</c> is what keeps the C# names identical to the JavaScript ones -
    /// without it the compiler camel-cases members, and <c>pdfjsLib.GlobalWorkerOptions</c> would be
    /// emitted as <c>pdfjsLib.globalWorkerOptions</c>.
    ///
    /// Only what this package needs is declared. pdf.js's surface is far larger; add to these
    /// declarations as needed rather than reaching back for a raw script string.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    [Name("pdfjsLib")]
    internal static class PdfJsLib
    {
        /// <summary>The pdf.js version string, e.g. <c>"6.2.108"</c>.</summary>
        public static extern string version { get; }

        /// <summary>The pdf.js build hash.</summary>
        public static extern string build { get; }

        /// <summary>
        /// <c>pdfjsLib.GlobalWorkerOptions</c> - where the worker is configured. pdf.js has no
        /// browser default and throws when asked for a worker with neither member set; the bundle's
        /// epilogue fills <c>workerSrc</c> in from its own script URL.
        /// </summary>
        public static extern IGlobalWorkerOptions GlobalWorkerOptions { get; }
    }

    /// <summary>
    /// Where pdf.js looks for its worker. Both members are static on the JavaScript class, so this
    /// interface types the single instance <see cref="PdfJsLib.GlobalWorkerOptions"/> hands back.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    internal interface IGlobalWorkerOptions
    {
        /// <summary>
        /// The URL of the worker script. Must name a real ES module: pdf.js constructs it with
        /// <c>new Worker(src, { type: "module" })</c>. A cross-origin URL is wrapped in a same-origin
        /// blob by pdf.js itself.
        /// </summary>
        string workerSrc { get; set; }
    }

    /// <summary>
    /// <c>window.pdfjsLib</c> and <c>window.pdfjsViewer</c>, for asking whether pdf.js has loaded yet.
    ///
    /// Deliberately reached through <c>window</c>: a bare <c>pdfjsLib</c> reference throws a
    /// <c>ReferenceError</c> before the bundle's script has run, while a missing property on
    /// <c>window</c> is simply <c>undefined</c>.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    [Name("window")]
    internal static class JsWindow
    {
        public static extern IPdfJsLibNamespace    pdfjsLib    { get; }
        public static extern IPdfJsViewerNamespace pdfjsViewer { get; }
    }

    /// <summary>Just enough of <c>pdfjsLib</c> to tell a loaded bundle from an absent one.</summary>
    [External]
    [Convention(Notation.None)]
    internal interface IPdfJsLibNamespace
    {
        object getDocument { get; }
    }

    /// <summary>
    /// Just enough of <c>pdfjsViewer</c> to tell a loaded bundle from an absent one.
    ///
    /// Both globals are checked, not only the first: they are set by the two halves of the bundle, and
    /// only <c>pdfjsViewer</c> being present proves the viewer components evaluated as well as the
    /// display API.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    internal interface IPdfJsViewerNamespace
    {
        object EventBus { get; }
    }
}
