using System;
using System.Threading.Tasks;
using Transpose;

namespace Tesserae.Pdf
{
    /// <summary>
    /// pdf.js's runner for a document's own embedded JavaScript - the calculate, format and validate
    /// actions an AcroForm carries.
    ///
    /// The scripts run inside a QuickJS interpreter compiled to WebAssembly, not in the page: they
    /// cannot reach the DOM, the network, or anything else of the host's. What they can do is read
    /// and write the document's own form fields, which is the whole point.
    ///
    /// Three URLs have to line up for that, and they resolve against <b>different</b> bases, which is
    /// why <see cref="PdfJs"/> hands over absolute ones:
    /// <c>sandboxBundleSrc</c> is a module specifier, resolved by the browser's own
    /// <c>import()</c> against the importing module; <c>wasmUrl</c> is resolved against the page; and
    /// the QuickJS glue finds its <c>.wasm</c> relative to itself.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    [Name("pdfjsViewer.PDFScriptingManager")]
    public class PdfScriptingManagerJs
    {
        public extern PdfScriptingManagerJs(PdfScriptingManagerOptions options);

        /// <summary>
        /// The viewer whose form fields the scripts will drive. Must be called <b>before</b> the
        /// viewer is given a document - pdf.js wires the two together inside <c>setDocument</c>.
        /// </summary>
        public extern void setViewer(object viewer);

        /// <summary>
        /// Starts the sandbox for a document. Called by <c>PDFViewer.setDocument</c> itself, so a
        /// host does not normally call it; passing null tears the sandbox down.
        /// </summary>
        public extern IPromise setDocument(IPdfDocumentProxy document);

        /// <summary>Whether the sandbox came up. False when a document has no scripts, and also when it failed.</summary>
        public extern bool ready { get; }

        /// <summary>
        /// Tells the document its data is about to be saved, so a <c>WillSave</c> action can update
        /// a field first.
        /// </summary>
        public extern IPromise dispatchWillSave();

        /// <summary>Tells the document the save finished.</summary>
        public extern IPromise dispatchDidSave();

        /// <summary>
        /// Tells the document it is about to be printed.
        ///
        /// This one <b>waits</b> for the sandbox to answer, so a document whose <c>WillPrint</c>
        /// action never returns leaves it pending for good. Worth a timeout at the call site if the
        /// documents are not yours.
        /// </summary>
        public extern IPromise dispatchWillPrint();

        /// <summary>Tells the document the print finished.</summary>
        public extern IPromise dispatchDidPrint();

        /// <summary>Resolves once the sandbox has been torn down.</summary>
        public extern IPromise destroyPromise { get; }
    }

    /// <summary>What pdf.js's <c>PDFScriptingManager</c> takes.</summary>
    [ObjectLiteral]
    public class PdfScriptingManagerOptions
    {
        /// <summary>Required. The sandbox reports every field it changes through this.</summary>
        public EventBus eventBus;

        /// <summary>
        /// The sandbox module. There is no usable default in the components build, so leaving this
        /// unset means scripting silently does nothing.
        ///
        /// Must be absolute: pdf.js loads it with a native <c>import()</c>, which resolves a relative
        /// specifier against the module doing the importing rather than against the page.
        /// </summary>
        public string sandboxBundleSrc;

        /// <summary>
        /// Where the QuickJS WebAssembly lives - the same <c>wasm/</c> directory the image decoders
        /// come from, with a trailing slash.
        ///
        /// Absent from pdf.js's own type declarations but read by the components build, so a viewer
        /// built without it comes up with an inert sandbox and no error.
        /// </summary>
        public string wasmUrl;

        /// <summary>
        /// Answers the document's questions about itself - its URL, its filename, its length. The
        /// components build's own default answers with empty strings, so a script reading
        /// <c>this.URL</c> or <c>this.documentFileName</c> gets nothing unless this is supplied.
        /// </summary>
        public Func<IPdfDocumentProxy, IPromise> docProperties;
    }
}
