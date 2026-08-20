using System.Threading.Tasks;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// Two things that are usually needed together, because both come up when a document is not just
    /// a public URL: opening bytes you already hold, and answering a password prompt.
    /// </summary>
    [SampleDetails(Group = "Viewer", Order = 80, Icon = UIcons.Lock)]
    public class BytesAndPasswordsSample : IComponent, ISample
    {
        private readonly IComponent _content;

        // A one-page PDF, small enough to be a string. Base64 rather than a URL so this page has
        // something to open that never touches the network.
        private const string TINY_PDF_BASE64 =
            "JVBERi0xLjQKMSAwIG9iago8PCAvVHlwZSAvQ2F0YWxvZyAvUGFnZXMgMiAwIFIgPj4KZW5k" +
            "b2JqCjIgMCBvYmoKPDwgL1R5cGUgL1BhZ2VzIC9Db3VudCAxIC9LaWRzIFszIDAgUl0gPj4K" +
            "ZW5kb2JqCjMgMCBvYmoKPDwgL1R5cGUgL1BhZ2UgL1BhcmVudCAyIDAgUiAvTWVkaWFCb3gg" +
            "WzAgMCA0MjAgMTQ0XSAvUmVzb3VyY2VzIDw8IC9Gb250IDw8IC9GMSA0IDAgUiA+PiA+PiAv" +
            "Q29udGVudHMgNSAwIFIgPj4KZW5kb2JqCjQgMCBvYmoKPDwgL1R5cGUgL0ZvbnQgL1N1YnR5" +
            "cGUgL1R5cGUxIC9CYXNlRm9udCAvSGVsdmV0aWNhID4+CmVuZG9iago1IDAgb2JqCjw8IC9M" +
            "ZW5ndGggMTI5ID4+CnN0cmVhbQpCVCAvRjEgMTYgVGYgMjAgOTAgVGQgKE9wZW5lZCBmcm9t" +
            "IGJ5dGVzLikgVGogRVQKQlQgL0YxIDEwIFRmIDIwIDYwIFRkIChObyBuZXR3b3JrIC0gdGhl" +
            "c2UgYnl0ZXMgYXJlIGEgc3RyaW5nIGluIHRoZSBwYWdlLikgVGogRVQKZW5kc3RyZWFtCmVu" +
            "ZG9iagp4cmVmCjAgNgowMDAwMDAwMDAwIDY1NTM1IGYgCjAwMDAwMDAwMDkgMDAwMDAgbiAK" +
            "MDAwMDAwMDA1OCAwMDAwMCBuIAowMDAwMDAwMTE1IDAwMDAwIG4gCjAwMDAwMDAyNDEgMDAw" +
            "MDAgbiAKMDAwMDAwMDMxMSAwMDAwMCBuIAp0cmFpbGVyCjw8IC9TaXplIDYgL1Jvb3QgMSAw" +
            "IFIgPj4Kc3RhcnR4cmVmCjQ5MQolJUVPRgo=";

        public BytesAndPasswordsSample()
        {
            var status = TextBlock("-").Small().Secondary();
            var prompt = TextBox("").Password().SetPlaceholder("The password is \"tesserae\"").Width(220.px());

            var viewer = PdfJs.Viewer();

            viewer
               .FitWidth()
               .OnDocumentLoaded(document => status.Text = $"Opened: {document.PageCount} page(s).")
               .OnError(error => status.Text = $"{error.Kind}: {error.Message}")

                // pdf.js does not wait for this: it hands over a callback and leaves the load pending
                // until it is called, which is what makes a real dialog possible here. Answering with
                // nothing abandons the load rather than hanging it.
               .OnPassword(async reason =>
               {
                   status.Text = reason == PasswordReason.IncorrectPassword
                       ? "That password was wrong - asked again."
                       : "This document is encrypted - waiting for a password.";

                   return await AskAsync(prompt);
               });

            var openTiny = Button("Open from bytes").SetIcon(UIcons.Binary)
               .OnClick(() =>
               {
                   status.Text = "Opening from a base64 string, with no request.";
                   viewer.Source(PdfSource.FromBase64(TINY_PDF_BASE64));
               });

            var openProtected = Button("Open the encrypted document").SetIcon(UIcons.Lock)
               .OnClick(() =>
               {
                   status.Text = "Opening the encrypted document.";
                   viewer.Url(PROTECTED_PDF);
               });

            var openWithKnown = Button("Open it with the password supplied").SetIcon(UIcons.Key)
               .OnClick(() =>
               {
                   status.Text = "Opening with the password already on the source - no prompt.";
                   viewer.Source(PdfSource.FromUrl(PROTECTED_PDF).WithPassword(PROTECTED_PDF_PASSWORD));
               });

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(BytesAndPasswordsSample), UIcons.Lock, "Opening bytes, and answering a password prompt")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("PdfSource is where a document comes from, and it is more than a URL: FromBytes for something already in memory, FromBase64 for a document embedded in a JSON response, and on any of them WithPassword, WithCredentials, WithHttpHeader and the range-request switches."),
                        TextBlock("An encrypted document without a password fails with PdfErrorKind.Password. With OnPassword wired up it asks instead - and asks again, with IncorrectPassword, if the answer was wrong.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("A typed array handed to pdf.js is transferred to the worker, which takes ownership of it: the caller's view is detached afterwards, so the same array cannot be opened twice. FromBytes(byte[]) copies into a fresh native array for that reason, and FromBytes(Uint8Array) does not - so pass the latter a copy if the document might be reopened, including by a remount."),
                        TextBlock("Prefer a URL over bytes when you have the choice. A URL lets pdf.js fetch ranges, so a 200-page document shows its first page without downloading the rest; bytes mean the whole file is in memory on the main thread before anything renders.").MT(8),
                        TextBlock("OnPassword is asynchronous because a real password prompt is. pdf.js hands over a callback and does not wait, so the load simply stays pending until it is answered - which also means never answering is a hang. Returning null ends the load properly instead.").MT(8),
                        TextBlock("WithCredentials is only needed cross-origin. On a same-origin request the browser sends cookies anyway, and setting it there is a common cause of a CORS failure that looks like a missing file.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        HStack().WS().Gap(8.px()).Wrap().Children(openTiny, openProtected, openWithKnown),
                        HStack().WS().Gap(8.px()).MT(8).Children(prompt),
                        status.MT(8),
                        viewer.H(460).WS().MT(8),
                        SampleHint("Type a wrong password first: the prompt comes back with IncorrectPassword. Leave it empty and press the button again to see the load give up cleanly.")
                    )).SetTitle("Usage")))
               ;
        }

        /// <summary>
        /// Waits for the password already typed into the box, or for one to be typed. A real host
        /// would show a modal; this keeps the page readable.
        /// </summary>
        private static async Task<string> AskAsync(TextBox prompt)
        {
            for (var waited = 0; waited < 30000; waited += 250)
            {
                if (!string.IsNullOrEmpty(prompt.Text)) return prompt.Text;

                await Task.Delay(250);
            }

            return null;
        }

        public HTMLElement Render() => _content.Render();
    }
}
