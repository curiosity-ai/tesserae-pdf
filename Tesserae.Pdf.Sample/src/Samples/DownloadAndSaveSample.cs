using System.Threading.Tasks;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// Getting the bytes back out - the document as fetched, or the document including what the user
    /// typed into its form. The round trip is the check: type something, save, reopen the saved bytes,
    /// and it should still be there.
    /// </summary>
    [SampleDetails(Group = "Runtime and hosting", Order = 60, Icon = UIcons.Disk)]
    public class DownloadAndSaveSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public DownloadAndSaveSample()
        {
            var status   = TextBlock("-").Small().Secondary();
            var reopened = PdfJs.Viewer();

            var viewer = PdfJs.Viewer();

            viewer
               .Url(FORMS_PDF)
               .FitWidth()
               .Annotations(AnnotationMode.EnableForms)
               .OnDocumentLoaded(document => status.Text = "Loaded. Type into a field, then save.");

            reopened.FitWidth().Annotations(AnnotationMode.EnableForms);

            var original = Button("Get the original bytes").SetIcon(UIcons.FileDownload)
               .OnClick(() => ShowAsync(viewer, status, save: false, into: null).FireAndForget());

            var save = Button("Save with the form values").SetIcon(UIcons.Disk).Primary()
               .OnClick(() => ShowAsync(viewer, status, save: true, into: null).FireAndForget());

            var roundTrip = Button("Save, then reopen the saved bytes").SetIcon(UIcons.Refresh)
               .OnClick(() => ShowAsync(viewer, status, save: true, into: reopened).FireAndForget());

            var download = Button("Download it").SetIcon(UIcons.Download)
               .OnClick(() => DownloadAsync(viewer, status).FireAndForget());

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(DownloadAndSaveSample), UIcons.Disk, "The bytes back out, with and without the form values")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("document.GetDataAsync() gives the document exactly as it was fetched. document.SaveAsync() gives it with whatever the user has typed into its form fields written in - two different answers, and the difference is the whole point of having both."),
                        TextBlock("Both hand back a Uint8Array, which is what a Blob wants, which is what a download or an upload wants. The package deliberately does not do the download itself: how a file reaches the user is the host's decision, and pdf.js's own download manager assumes a browser page rather than an application.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Saving needs the values to have been kept, which means the viewer's annotation mode has to be EnableForms - the default. A viewer showing a form with Enable renders it as a picture, and SaveAsync then honestly reports that nothing was filled in."),
                        TextBlock("The round-trip button is the check worth having: it saves, opens the saved bytes in a second viewer, and shows them side by side. If a form value survives that, the whole chain worked - the annotation storage, the save, and the reopen.").MT(8),
                        TextBlock("Bytes given to a viewer are transferred to its worker, so the array cannot be reused: the round trip below saves again rather than keeping the array from the first save. Revoke the object URL a download creates once the click has happened, or the blob stays in memory for the life of the page.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        HStack().WS().Gap(8.px()).Wrap().Children(original, save, roundTrip, download),
                        status.MT(8),
                        viewer.H(460).WS().MT(8),
                        SampleSubTitle("The saved bytes, reopened"),
                        reopened.H(320).WS(),
                        SampleHint("Type a name into the first field, then \"Save, then reopen\": the lower viewer shows the saved document with your text in it.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(FormsAndAnnotationsSample), typeof(ScriptingSample), typeof(BytesAndPasswordsSample));
        }

        private static async Task ShowAsync(PdfViewer viewer, TextBlock status, bool save, PdfViewer into)
        {
            var document = viewer.Document;

            if (document is null)
            {
                status.Text = "No document loaded.";

                return;
            }

            var bytes = save ? await document.SaveAsync() : await document.GetDataAsync();

            status.Text = save
                ? $"Saved {bytes.length} bytes, form values included."
                : $"The original is {bytes.length} bytes.";

            if (into is null) return;

            // FromBytes(Uint8Array) hands the array straight over, and pdf.js transfers it to its
            // worker - so this array is spent afterwards, which is why the button saves again rather
            // than reusing one.
            into.Source(PdfSource.FromBytes(bytes));

            status.Text += " Reopened in the viewer below.";
        }

        private static async Task DownloadAsync(PdfViewer viewer, TextBlock status)
        {
            // Named pdfDocument rather than document: the local would otherwise shadow the DOM's
            // `document`, and the error that produces names the wrong type entirely.
            var pdfDocument = viewer.Document;

            if (pdfDocument is null) return;

            var bytes = await pdfDocument.SaveAsync();

            // A Blob and an object URL: how any file reaches a user from a browser application, with
            // nothing pdf.js-specific about it.
            var blob = new Blob(new object[] { bytes }, new BlobPropertyBag { type = "application/pdf" });
            var url  = URL.createObjectURL(blob);
            var link = (HTMLAnchorElement)document.createElement("a");

            link.href     = url;
            link.download = "filled-form.pdf";
            link.click();

            // Released once the click has been dispatched: an object URL keeps its blob alive for the
            // life of the page otherwise.
            URL.revokeObjectURL(url);

            status.Text = $"Downloaded {bytes.length} bytes as filled-form.pdf.";
        }

        public HTMLElement Render() => _content.Render();
    }
}
