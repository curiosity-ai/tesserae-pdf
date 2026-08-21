using System;
using System.Threading.Tasks;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// Everything a document says about itself: its information dictionary, its page labels, its
    /// permissions, its attachments, its named destinations. The document picker matters here - the
    /// interesting answers differ per document, and two of them are only non-trivial on the
    /// encrypted one.
    /// </summary>
    [SampleDetails(Group = "Pages", Order = 40, Icon = UIcons.Info)]
    public class MetadataSample : IComponent, ISample
    {
        private readonly IComponent  _content;
        private readonly HTMLElement _host = DIV();
        private readonly Stack       _report;

        private PdfDocument _document;

        public MetadataSample()
        {
            _report = VStack().WS().Gap(4.px());

            var documents = new[]
            {
                new { Label = "Outline sample (12 pages, unrestricted)", Source = OUTLINE_PDF,   Password = (string)null },
                new { Label = "Encrypted sample (print and copy denied)", Source = PROTECTED_PDF, Password = PROTECTED_PDF_PASSWORD },
                new { Label = "AcroForm sample",                          Source = FORMS_PDF,     Password = (string)null },
                new { Label = "CJK sample",                               Source = CJK_PDF,       Password = (string)null },
            };

            var picker = Dropdown().Width(320.px());

            foreach (var item in documents)
            {
                var captured = item;

                picker.AddItems(DropdownItem(captured.Label).SelectedIf(captured.Source == OUTLINE_PDF)
                   .OnSelected(_ => ShowAsync(captured.Source, captured.Password).FireAndForget()));
            }

            ShowAsync(OUTLINE_PDF, null).FireAndForget();

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(MetadataSample), UIcons.Info, "What a document says about itself")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("GetMetadataAsync() flattens PDF's two metadata systems into one object: the information dictionary (Title, Author, Producer, the dates) and the XMP stream, which is the modern replacement and is reachable through GetXmp(name). Where the two disagree, XMP is meant to win - a document rewritten by a tool that only updated one of them will show it here."),
                        TextBlock("Alongside those: GetPageLabelsAsync() for the numbering the document wants shown, GetPermissionsAsync() for what it allows, GetAttachmentsAsync() for embedded files, and GetNamedDestinationsAsync() for the places it can be linked to by name.").MT(8),
                        TextBlock("Every field is optional in PDF. Most documents carry a handful, so null is the normal answer rather than a sign anything failed.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Permissions have a third state, and it matters. A document that restricts nothing reports no permissions at all - null, not an empty list - while one that forbids everything reports an empty list. Collapsing the two makes an unrestricted document look maximally locked down. GetPermissionsAsync keeps them apart, and IsAllowedAsync answers true for the null case."),
                        TextBlock("None of it is enforcement. A PDF's permission bits are a request to the viewer, readable by anyone with the file; a document that denies copying can still be read with GetAllTextAsync. Honour them because your users expect it, not because they are a control.").MT(8),
                        TextBlock("Dates come back as PDF date strings (\"D:20260501120000Z\") rather than parsed - the format has optional halves and its own timezone syntax, and guessing at a malformed one silently is worse than handing it over.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        picker,
                        _report.MT(8),
                        Raw(_host),
                        SampleHint("The encrypted document is the only one here whose permissions come back as a list - and note it opens without a prompt, because this page knows its password up front.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(TextExtractionSample), typeof(OutlineAndNavigationSample), typeof(BytesAndPasswordsSample));

            DomObserver.WhenRemoved(_host, () => _document?.DestroyAsync().FireAndForget());
        }

        private async Task ShowAsync(string source, string password)
        {
            _report.Clear();
            _report.Add(TextBlock("Loading...").Small().Secondary());

            var previous = _document;

            _document = null;

            if (previous is object) await previous.DestroyAsync();

            try
            {
                var pdfSource = PdfSource.FromUrl(source);

                if (!string.IsNullOrEmpty(password)) pdfSource = pdfSource.WithPassword(password);

                _document = await PdfJs.OpenAsync(pdfSource);
            }
            catch (PdfError error)
            {
                _report.Clear();
                _report.Add(TextBlock($"{error.Kind}: {error.Message}").Small());

                return;
            }

            var metadata     = await _document.GetMetadataAsync();
            var labels       = await _document.GetPageLabelsAsync();
            var permissions  = await _document.GetPermissionsAsync();
            var attachments  = await _document.GetAttachmentsAsync();
            var destinations = await _document.GetNamedDestinationsAsync();
            var hasScripts   = await _document.HasEmbeddedJavaScriptAsync();

            _report.Clear();

            Row("Pages",         _document.PageCount.ToString());
            Row("Fingerprint",   _document.Fingerprints is object && _document.Fingerprints.Length > 0 ? _document.Fingerprints[0] : null);
            Row("Title",         metadata.Title);
            Row("Author",        metadata.Author);
            Row("Subject",       metadata.Subject);
            Row("Keywords",      metadata.Keywords);
            Row("Creator",       metadata.Creator);
            Row("Producer",      metadata.Producer);
            Row("Created",       metadata.CreationDate);
            Row("Modified",      metadata.ModifiedDate);
            Row("PDF version",   metadata.PdfVersion);
            Row("Language",      metadata.Language);
            Row("Linearized",    metadata.IsLinearized.ToString());
            Row("AcroForm",      metadata.HasAcroForm.ToString());
            Row("XFA",           metadata.HasXfa.ToString());
            Row("Signatures",    metadata.HasSignatures.ToString());
            Row("Embedded JS",   hasScripts.ToString());

            Row("Page labels",   labels is null ? "(none - pages are numbered)" : string.Join(", ", labels));

            // The three-state answer this page exists to show: null is not the same as empty.
            Row("Permissions", permissions is null
                ? "(unrestricted - the document names no restrictions at all)"
                : permissions.Length == 0
                    ? "(nothing is permitted)"
                    : string.Join(", ", System.Array.ConvertAll(permissions, p => p.ToString())));

            Row("Attachments", attachments.Count == 0 ? "(none)" : attachments.Count.ToString());

            foreach (var attachment in attachments)
            {
                Row("  " + attachment.Key, attachment.FileName + " (" + attachment.Length + " bytes)");
            }

            Row("Named destinations", destinations.Count == 0 ? "(none)" : string.Join(", ", destinations.Keys));

            void Row(string label, string value)
            {
                _report.Add(HStack().WS().Gap(8.px()).Children(
                    TextBlock(label).Small().SemiBold().W(150),
                    TextBlock(string.IsNullOrEmpty(value) ? "-" : value).Small().Style(s => s.wordBreak = "break-all").Grow()));
            }
        }

        public HTMLElement Render() => _content.Render();
    }
}
