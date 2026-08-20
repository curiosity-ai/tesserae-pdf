using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// The text layer: what makes a rendered page selectable, searchable and readable by a screen
    /// reader. Also the one place a document's permissions can be turned into a real restriction on
    /// your users, which is why that is opt-in.
    /// </summary>
    [SampleDetails(Group = "Viewer", Order = 50, Icon = UIcons.TextSize)]
    public class TextSelectionSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public TextSelectionSample()
        {
            var selected = TextBlock("Nothing selected.").Small().Secondary();

            var withText = PdfJs.Viewer();
            withText.Url(OUTLINE_PDF).FitWidth().TextSelection(TextLayerMode.Enable);

            var withoutText = PdfJs.Viewer();
            withoutText.Url(OUTLINE_PDF).FitWidth().TextSelection(TextLayerMode.Disable);

            // Reading the selection is the browser's job, not the package's: a text layer is real
            // DOM text, so the ordinary selection API sees it.
            document.addEventListener("selectionchange", new System.Action<Event>(_ =>
            {
                var text = window.getSelection()?.ToString() ?? "";

                selected.Text = string.IsNullOrWhiteSpace(text)
                    ? "Nothing selected."
                    : $"{text.Length} characters selected: \"{(text.Length > 80 ? text.Substring(0, 80) + "..." : text)}\"";
            }));

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(TextSelectionSample), UIcons.TextSize, "The text layer, and what turns it off")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("A rendered page is a canvas - pixels, with no text in it. What makes a PDF selectable is a second layer of transparent, positioned text drawn over the top, and that is what TextSelection(TextLayerMode) controls."),
                        TextBlock("It is not only about selecting. Search highlighting needs somewhere to put its highlights, a screen reader needs something to read, and browser find-in-page needs something to find. Turning the text layer off turns all four off together.").MT(8),
                        TextBlock("Because it is real DOM text, the ordinary selection API sees it - the readout below is window.getSelection(), with nothing from this package involved.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("TextLayerMode.EnableIfPermitted honours the document's own copy permission, and it is deliberately not the default. It is the one setting here that turns a PDF's request into a restriction on the person using your application - and a viewer that silently refuses to let someone select a line of text reads as broken rather than as protective."),
                        TextBlock("It is also not enforcement. The text is still extractable through GetTextAsync, by anyone with the file; hiding the text layer only affects the person reading it in your UI. Use it when your users expect that behaviour, not as a control.").MT(8),
                        TextBlock("Disable is worth reaching for on a thumbnail or a preview tile: a text layer per page costs DOM nodes proportional to the words on it, and nobody selects text in a 120px-wide preview.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        selected,
                        SampleSubTitle("With a text layer"),
                        withText.H(360).WS(),
                        SampleSubTitle("Without one"),
                        withoutText.H(360).WS().MT(4),
                        SampleHint("Drag across a line in each. The first selects; the second cannot, because there is no text over the pixels.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(SearchSample), typeof(TextExtractionSample), typeof(LocalizationSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
