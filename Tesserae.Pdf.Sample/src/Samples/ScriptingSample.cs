using System.Threading.Tasks;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// A document that computes its own total. pdf.js runs a PDF's embedded JavaScript inside a
    /// QuickJS interpreter compiled to WebAssembly - not in the page - and this is what it takes to
    /// turn that on.
    /// </summary>
    [SampleDetails(Group = "Runtime and hosting", Order = 70, Icon = UIcons.CodeEditing)]
    public class ScriptingSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public ScriptingSample()
        {
            var status = TextBlock("Loading...").Small().Secondary();
            var events = VStack().WS().Gap(2.px());

            var viewer = PdfJs.Viewer();

            viewer
               .Url(SCRIPTING_PDF)
               .FitWidth()

                // The switch. A document with no scripts costs nothing - pdf.js starts no sandbox for
                // one - so this is safe to leave on for a viewer that shows arbitrary documents.
               .EnableScripting()
               .OnDocumentLoaded(document => ReportAsync(document, status).FireAndForget())

                // The readiness signal. A sandbox that fails to start writes to the console and
                // leaves the form inert; it does not throw, so this is the only way to know.
               .OnSandboxCreated(() => events.Add(TextBlock("sandboxcreated - the sandbox is running").Tiny().Secondary()));

            // Every field the document's own JavaScript changes is reported on the event bus. Read
            // through the raw bus rather than a wrapper, because the payload is pdf.js's own and
            // shaped per field type.
            viewer.Configure(_ => viewer.Events.on(PdfViewerEvents.UpdateFromSandbox, _2 =>
                events.Add(TextBlock("updatefromsandbox - a field was changed by the document").Tiny().Secondary())));

            var noScripts = PdfJs.Viewer();

            noScripts
               .Url(OUTLINE_PDF)
               .FitWidth()
               .EnableScripting()
               .OnSandboxCreated(() => events.Add(TextBlock("UNEXPECTED: sandboxcreated for a document with no form and no actions").Tiny()));

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(ScriptingSample), UIcons.CodeEditing, "A PDF running its own JavaScript")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("A PDF form can carry JavaScript: actions that compute a total, format a currency field, validate a date, or run when the document opens. EnableScripting() turns that on, and needs nothing else - the sandbox module and its WebAssembly ship with the package and the component points pdf.js at both."),
                        TextBlock("The scripts run inside QuickJS compiled to WebAssembly, not in the page. They cannot reach the DOM, the network, or anything else of yours; what they can do is read and write the document's own form fields, which is the point.").MT(8),
                        TextBlock("The document below has two amounts and a total with an AFSimple_Calculate action, plus a document-level script that runs on open. Change either amount and the total follows.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Failure here is quiet, which is the thing to plan for. A sandbox that cannot start - a missing WebAssembly file, a blocked dynamic import - writes to the console and leaves the form inert. Nothing throws, and the viewer looks fine. OnSandboxCreated is the signal that it actually came up."),
                        TextBlock("What pdf.js actually decides on is narrower than \"does this document have scripts\": it starts a sandbox for any document with form fields or document-level actions, and skips it entirely for one with neither. So sandboxcreated fires for an ordinary AcroForm as well - it means the sandbox is running, not that anything will use it. HasEmbeddedJavaScriptAsync is the question to ask for that, and the second viewer below is a document with no form at all, which should produce no event.").MT(8),
                        TextBlock("The three URLs involved resolve against three different bases - the sandbox module against the importing module, the WebAssembly directory against the page, and the QuickJS glue relative to itself - so the component hands pdf.js absolute URLs for all of them. That is the one detail worth knowing if scripting works in development and not behind a path prefix.").MT(8),
                        TextBlock("DispatchWillPrint waits for the document's own script to answer, so a WillPrint action that never returns leaves it pending. The component never calls it for you.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        status,
                        viewer.H(460).WS().MT(8),
                        SampleSubTitle("What the sandbox reported"),
                        events,
                        SampleSubTitle("The negative control"),
                        TextBlock("The same viewer settings on a document with no form and no actions. No sandbox is started for it, so nothing should appear above, and the console should stay clean.").Small(),
                        noScripts.H(280).WS().MT(4),
                        SampleHint("Change the subtotal to 200: the total should become 212.5 on its own. The console also carries a line printed by the document's own open script.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(FormsAndAnnotationsSample), typeof(DownloadAndSaveSample), typeof(LoadingAndAssetsSample));
        }

        private static async Task ReportAsync(PdfDocument document, TextBlock status)
        {
            var hasScripts = await document.HasEmbeddedJavaScriptAsync();

            status.Text = hasScripts
                ? "This document carries embedded JavaScript - the sandbox should start."
                : "This document has no embedded JavaScript, so no sandbox will start.";
        }

        public HTMLElement Render() => _content.Render();
    }
}
