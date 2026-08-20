using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// The four page layouts and the three spread modes, plus the one that is not a mode at all:
    /// <c>PdfJs.Viewer(singlePage: true)</c> uses a different pdf.js class, which is why it is decided
    /// when the component is built and cannot be switched afterwards.
    /// </summary>
    [SampleDetails(Group = "Viewer", Order = 60, Icon = UIcons.Table)]
    public class ScrollAndSpreadSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public ScrollAndSpreadSample()
        {
            var status = TextBlock("-").Small().Secondary();

            var viewer = PdfJs.Viewer();

            viewer
               .Url(OUTLINE_PDF)
               .FitPage()
               .OnPageChanged(page => status.Text = $"Page {page} of {viewer.PageCount}");

            // A second component, built as a single-page viewer. Two components rather than a
            // setting, because pdf.js implements the single-page layout as a subclass.
            var single = PdfJs.Viewer(singlePage: true);

            single.Url(OUTLINE_PDF).FitPage();

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(ScrollAndSpreadSample), UIcons.Table, "Page layout, spreads, and the single-page viewer")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Scroll(ScrollMode) decides how pages are laid out: one column scrolling down (the default), one row scrolling right, a grid that wraps, or one page at a time. Spread(SpreadMode) pairs them like an open book, starting on odd or even pages."),
                        TextBlock("Both can be changed at any time, and both survive a remount. Together they cover every reading layout a PDF reader offers.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("ScrollMode.Page and PdfJs.Viewer(singlePage: true) look the same and are not. The first is a layout mode on the ordinary viewer, switchable at runtime. The second is pdf.js's PDFSinglePageViewer, a different class that overrides the layout half of the viewer - so it is decided when the component is built, and reaching for it means committing."),
                        TextBlock("Prefer ScrollMode.Page unless you have a reason not to: it does the same job and can be turned off again.").MT(8),
                        TextBlock("Spread modes and Wrapped scroll interact with the zoom. In a spread, two pages share the width, so FitWidth fits a pair rather than a page - which is correct, and surprising the first time.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        HStack().WS().Gap(4.px()).Wrap().Children(
                            Button("Vertical").OnClick(() => viewer.Scroll(ScrollMode.Vertical)),
                            Button("Horizontal").OnClick(() => viewer.Scroll(ScrollMode.Horizontal)),
                            Button("Wrapped").OnClick(() => viewer.Scroll(ScrollMode.Wrapped)),
                            Button("Page at a time").OnClick(() => viewer.Scroll(ScrollMode.Page))),
                        HStack().WS().Gap(4.px()).Wrap().MT(4).Children(
                            Button("No spreads").OnClick(() => viewer.Spread(SpreadMode.None)),
                            Button("Odd spreads").OnClick(() => viewer.Spread(SpreadMode.Odd)),
                            Button("Even spreads").OnClick(() => viewer.Spread(SpreadMode.Even))),
                        status.MT(8),
                        viewer.H(520).WS().MT(8),
                        SampleSubTitle("The single-page viewer, for comparison"),
                        TextBlock("The same document in PdfJs.Viewer(singlePage: true). Note there is nothing to scroll between pages - the buttons below are the only way through it.").Small(),
                        HStack().WS().Gap(4.px()).MT(4).Children(
                            Button("Previous").OnClick(() => single.PreviousPage()),
                            Button("Next").OnClick(() => single.NextPage())),
                        single.H(420).WS().MT(8),
                        SampleHint("Try Wrapped with fit-page, then Odd spreads: the first page sits alone on the right, as an open book's does.")
                    )).SetTitle("Usage")))
               ;
        }

        public HTMLElement Render() => _content.Render();
    }
}
