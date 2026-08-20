using System.Threading.Tasks;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// What the package loads, from where, and how a host moves it. Also the page to open first when
    /// a viewer will not render at all: everything on it is read back out of the loaded pdf.js rather
    /// than printed from C# constants, so a wrong <c>AssetsPath</c> or a missing worker shows up here
    /// before it shows up as a blank page somewhere else.
    /// </summary>
    [SampleDetails(Group = "Runtime and hosting", Order = 10, Icon = UIcons.CloudDownloadAlt)]
    public class LoadingAndAssetsSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public LoadingAndAssetsSample()
        {
            var report = VStack().WS().Gap(4.px());

            // Nothing on this page builds a viewer, and it is a component mounting that normally
            // starts the loader - so this page has to ask for pdf.js itself, or the report below
            // would describe a bundle that was never fetched.
            LoadAndReportAsync(report).FireAndForget();

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(LoadingAndAssetsSample), UIcons.CloudDownloadAlt, "Where pdf.js and its assets are loaded from")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("The package ships pdf.js as one bundled script plus the asset directories its worker fetches at runtime - character maps for CJK text, the 14 standard fonts, the wasm image and colour decoders, an ICC profile, and the icons the annotation layer draws. The build copies all of it into your app's output under assets/js/pdf, and PdfJs derives every URL it hands pdf.js from that one folder."),
                        TextBlock("Nothing is fetched until the first component mounts. PdfJs.LoadAsync() is what does it, at most once per page; PdfJs.WhenLoaded(action) runs something as soon as the bundle is up, immediately if it already is.").MT(8),
                        TextBlock("A page whose only content is headless - a report like this one, a text extraction, a canvas render started from a button - has no component mounting to trigger that, so it calls LoadAsync() itself.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Set PdfJs.AssetsPath before the first viewer is built to serve pdf.js from somewhere else - a CDN, a shared static host, a different folder. It is resolved by the browser against the document's base URI, so a relative value follows the page and an absolute one is passed straight through."),
                        TextBlock("Moving it moves everything: the worker is located by the bundle from its own script URL, and the five asset URLs below are derived from the same folder, so there is no second setting to keep in sync.").MT(8),
                        TextBlock("The one failure worth recognising is the worker. pdf.js has no browser default for it, and getting it wrong does not throw - pdf.js imports the worker on the main thread instead, which parses documents correctly and freezes the UI while it does. If a large document locks the page, look for \"Setting up fake worker\" in the console.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("What loaded"),
                        TextBlock("Read back from the running pdf.js rather than printed from C# constants, so this is the configuration your documents are actually opened with."),
                        report.MT(8),
                        SampleHint("Open the network tab and reload: pdf.js is one request, and the worker is a second one that only happens once a document is opened.")
                    )).SetTitle("Usage")))
               ;
        }

        private static async Task LoadAndReportAsync(Stack report)
        {
            report.Add(TextBlock("Loading pdf.js...").Small().Secondary());

            await PdfJs.LoadAsync();

            report.Clear();

            Row("pdf.js version", PdfJs.Version);
            Row("pdf.js build",   PdfJs.Build);
            Row("Assets path",    PdfJs.AssetsPath);
            Row("Base URL",       PdfJs.BaseUrl);
            Row("Worker",         PdfJs.WorkerSrc);
            Row("Character maps", PdfJs.CMapUrl);
            Row("Standard fonts", PdfJs.StandardFontDataUrl);
            Row("Wasm decoders",  PdfJs.WasmUrl);
            Row("ICC profiles",   PdfJs.IccUrl);
            Row("Annotation icons", PdfJs.ImageResourcesPath);
            Row("Scripting sandbox", PdfJs.SandboxUrl);
            Row("Language",       PdfJs.Language);

            void Row(string label, string value)
            {
                report.Add(HStack().WS().Gap(8.px()).Children(
                    TextBlock(label).Small().SemiBold().W(140),
                    TextBlock(value ?? "(not set)").Small().Style(s => s.wordBreak = "break-all").Grow()));
            }
        }

        public HTMLElement Render() => _content.Render();
    }
}
