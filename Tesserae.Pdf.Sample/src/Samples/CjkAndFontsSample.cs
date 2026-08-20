using System.Threading.Tasks;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// The asset directories, and the failure they exist to prevent. A CID font with no embedded font
    /// file needs a character map fetched from <c>cMapUrl</c>; get that wrong and the page renders
    /// blanks while the console shows a 404 nobody connects to the symptom.
    /// </summary>
    [SampleDetails(Group = "Pages", Order = 50, Icon = UIcons.Language)]
    public class CjkAndFontsSample : IComponent, ISample
    {
        private readonly IComponent  _content;
        private readonly HTMLElement _host = DIV();

        public CjkAndFontsSample()
        {
            var extracted = TextBlock("-").Small();
            var report    = VStack().WS().Gap(2.px());

            var viewer = PdfJs.Viewer();

            viewer
               .Url(CJK_PDF)
               .FitWidth()
               .OnDocumentLoaded(document => ExtractAsync(document, extracted).FireAndForget());

            Row("Character maps", PdfJs.CMapUrl);
            Row("Standard fonts", PdfJs.StandardFontDataUrl);
            Row("Wasm decoders",  PdfJs.WasmUrl);
            Row("ICC profiles",   PdfJs.IccUrl);

            void Row(string label, string value)
            {
                report.Add(HStack().WS().Gap(8.px()).Children(
                    TextBlock(label).Tiny().SemiBold().W(120),
                    TextBlock(value).Tiny().Style(s => s.wordBreak = "break-all").Grow()));
            }

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(CjkAndFontsSample), UIcons.Language, "Character maps, standard fonts and the other asset directories")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("pdf.js needs four directories of data at runtime, and fetches them from the worker rather than from the page: character maps for CJK encodings, the 14 standard PDF fonts for documents that do not embed them, WebAssembly decoders for JPEG 2000, JBIG2 and ICC colour, and an ICC profile for CMYK. The build copies all four into your app's output and the package points pdf.js at them."),
                        TextBlock("The document below is the reason this page exists. Its text is set in a CID font it does not embed, so rendering it requires cmaps/UniGB-UCS2-H.bcmap and a substitute font - both fetched, both from the paths listed further down.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("These URLs are absolute, and that is not tidiness. pdf.js resolves them inside the worker, against the worker's own location rather than the page's - so a relative \"assets/js/pdf/cmaps/\" is fetched from assets/js/pdf/assets/js/pdf/cmaps/ and 404s from a path nobody wrote. Every asset URL the package hands over is absolute for that reason."),
                        TextBlock("Getting one wrong does not throw. pdf.js warns and renders what it can, which means a CJK document comes out as blank boxes and an unembedded-font document comes out in the wrong typeface - symptoms that look like a broken document rather than a misconfigured path. If a document renders oddly, check the network tab for a 404 under the asset directories first.").MT(8),
                        TextBlock("Text extraction needs the character maps too, not just rendering: without them the CIDs cannot be mapped back to Unicode, and GetTextAsync returns nothing useful. The extraction below is the check for that.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        SampleSubTitle("Where the assets are"),
                        report,
                        SampleSubTitle("The extracted text"),
                        extracted,
                        viewer.H(520).WS().MT(8),
                        Raw(_host),
                        SampleHint("The Chinese lines should render as characters and come out of extraction as characters. Watch the network tab: UniGB-UCS2-H.bcmap and Adobe-GB1-UCS2.bcmap are both fetched."),
                        SampleHint("Expected console noise: \"Cannot load system font: STSong-Light\". The document names a font it does not embed and this machine does not have, so pdf.js substitutes one - which is the whole point of the standard-font directory, and is not a failure.")
                    )).SetTitle("Usage")))
               ;
        }

        private static async Task ExtractAsync(PdfDocument document, TextBlock extracted)
        {
            var page = await document.GetPageAsync(1);

            extracted.Text = await page.GetTextAsync();
        }

        public HTMLElement Render() => _content.Render();
    }
}
