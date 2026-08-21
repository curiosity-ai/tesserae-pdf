using System.Collections.Generic;
using System.Threading.Tasks;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// The outline sidebar, drawn by the host from what the document says - bold, italic, colour,
    /// nesting and the collapsed-by-default flag all included. The package hands over the tree; what
    /// it looks like is not its business.
    /// </summary>
    [SampleDetails(Group = "Viewer", Order = 40, Icon = UIcons.ListTree)]
    public class OutlineAndNavigationSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public OutlineAndNavigationSample()
        {
            var outlineHost = VStack().WS().Gap(1.px());
            var status      = TextBlock("-").Small().Secondary();

            var viewer = PdfJs.Viewer();

            viewer
               .Url(OUTLINE_PDF)
               .FitWidth()
               .OnPageChanged(page => status.Text = $"Page {page}, label \"{PageLabel(viewer)}\"")
               .OnDocumentLoaded(document => BuildOutlineAsync(document, outlineHost, viewer, status).FireAndForget());

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(OutlineAndNavigationSample), UIcons.ListTree, "The document's own table of contents")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("document.GetOutlineAsync() gives the document's bookmark tree, or an empty list - which most PDFs have, so empty is the common case rather than a failure. Each entry carries its title, whether the document asks for it bold or italic, its colour, its children, and whether the branch should start collapsed."),
                        TextBlock("GoToDestination(entry.Destination) navigates to it. The destination is pdf.js's own value and is passed through untouched, because it is either a name or an array of page, zoom mode and coordinates - and re-encoding either loses something.").MT(8),
                        TextBlock("An entry can also be an external link (Url) rather than a place in the document, and some are headings with no target at all - HasTarget is the question to ask before making one clickable.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Colour is reported as null when the document names none, rather than as black. That distinction matters in a dark theme: an entry with no colour of its own should use your foreground, and one that really is black should be black."),
                        TextBlock("StartsCollapsed comes from the PDF's own negative child count. The children are present either way, so it is a hint about the initial state of your tree, not about what you were given.").MT(8),
                        TextBlock("Navigation has three routes and they are not interchangeable. A destination is the precise one. GoToPage is a page number. GoToPageLabel takes the numbering the document wants shown - \"iv\", \"A-3\" - which is what a user reading the page footer will type into a \"go to page\" box, and is not the page's index.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        HStack().WS().Gap(8.px()).Wrap().Children(
                            Button("Named destination: introduction").OnClick(() => viewer.GoToNamedDestination("introduction")),
                            Button("Named destination: forms").OnClick(() => viewer.GoToNamedDestination("forms")),
                            Button("Page label \"ii\"").OnClick(() => viewer.GoToPageLabel("ii")),
                            Button("Page label \"7\"").OnClick(() => viewer.GoToPageLabel("7"))),
                        status.MT(8),
                        HStack().WS().H(520).Gap(12.px()).MT(8).Children(
                            VStack().W(260).HS().ScrollY().Children(outlineHost),
                            viewer.HS().Grow()),
                        SampleHint("This document's first two pages are labelled i and ii, so page label \"7\" is the ninth page - which is what the status line shows.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(ViewerChromeSample), typeof(MetadataSample), typeof(ThumbnailsSample));
        }

        private static string PageLabel(PdfViewer viewer)
        {
            var instance = viewer.ViewerInstance;

            return instance is object ? (instance.currentPageLabel ?? "-") : "-";
        }

        private static async Task BuildOutlineAsync(PdfDocument document, Stack host, PdfViewer viewer, TextBlock status)
        {
            var outline = await document.GetOutlineAsync();

            host.Clear();

            if (outline.Count == 0)
            {
                host.Add(TextBlock("This document has no outline.").Small().Secondary());

                return;
            }

            Add(outline, 0);

            void Add(IReadOnlyList<PdfOutlineItem> items, int depth)
            {
                foreach (var item in items)
                {
                    var label = TextBlock(item.Title ?? "(untitled)").Small().PL(depth * 12 + 4);

                    // The document's own styling, applied rather than ignored: an outline that
                    // renders every entry the same way loses the structure its author put in it.
                    if (item.Bold)   label = label.SemiBold();
                    if (item.Italic) label = label.Style(s => s.fontStyle = "italic");

                    if (item.Color is object) label = label.Foreground(item.Color);

                    if (item.HasTarget)
                    {
                        label = label.Style(s => s.cursor = "pointer").OnClick((_, __) =>
                        {
                            if (!string.IsNullOrEmpty(item.Url))
                            {
                                window.open(item.Url, item.NewWindow ? "_blank" : "_self");

                                return;
                            }

                            viewer.GoToDestination(item.Destination);
                        });
                    }
                    else
                    {
                        label = label.Secondary();
                    }

                    host.Add(label);

                    if (item.Children.Count > 0) Add(item.Children, depth + 1);
                }
            }

            status.Text = $"{outline.Count} top-level entries.";
        }

        public HTMLElement Render() => _content.Render();
    }
}
