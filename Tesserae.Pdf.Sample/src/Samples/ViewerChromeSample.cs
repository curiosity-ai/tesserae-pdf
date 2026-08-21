using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// The viewer with the toolbar already on it. Everything on this page is one call and a couple of
    /// setters - which is the point: the pages after it drive <c>PdfJs.Viewer()</c> directly and build
    /// their own controls, and this one is what a host gets for not wanting to.
    /// </summary>
    [SampleDetails(Group = "Viewer", Order = 10, Icon = UIcons.LayoutFluid)]
    public class ViewerChromeSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public ViewerChromeSample()
        {
            var status = TextBlock("-").Small().Secondary();

            var chrome = PdfJs.ViewerChrome();

            chrome
               .Url(OUTLINE_PDF)
               .Panel(PdfChromePanel.Outline)
               .OnPanelChanged(panel => status.Text = $"Panel: {panel}, search: {chrome.CurrentSearchMode}")
               .OnSearchModeChanged(mode => status.Text = $"Panel: {chrome.CurrentPanel}, search: {mode}");

            chrome.Viewer.FitWidth();

            // The same chrome in its other arrangement, on a second copy of the same document - the
            // point being that the layout is the only difference between them.
            var rail = PdfJs.ViewerChrome()
               .Layout(PdfChromeLayout.IconRail)
               .Url(OUTLINE_PDF)
               .DocumentName("Capacity-Planning-Guide-2026.pdf")
               .Panel(PdfChromePanel.Thumbnails);

            rail.Viewer.FitPage();

            // Pared back to what a preview pane needs: pages and search, one panel tab, no zoom
            // controls and no spread. Each of those is one setter rather than a second component.
            var compact = PdfJs.ViewerChrome()
               .Url(IMAGES_PDF)
               // A fixed-size box sitting on its container's surface rather than filling it, so the
               // only thing that says where the document ends is the frame it draws itself.
               .Border()
               .ShowZoom(false)
               .ShowSpread(false)
               .ShowRotate(false)
               .Tabs(thumbnails: false)
               .SearchWidth(240)
               .PanelWidth(200);

            compact.Viewer.FitWidth();

            var borderChoice = ChoiceGroup("Frame").Horizontal().Choices(
                Choice("No border").Selected().OnSelected(_ => chrome.Border(false)),
                Choice("Border").OnSelected(_ => chrome.Border().CornerRadius(6)),
                Choice("Border, square").OnSelected(_ => chrome.Border().CornerRadius(0)));

            var layoutChoice = ChoiceGroup("Layout").Horizontal().Choices(
                Choice("Single toolbar").Selected().OnSelected(_ => chrome.Layout(PdfChromeLayout.SingleToolbar)),
                Choice("Icon rail").OnSelected(_ => chrome.Layout(PdfChromeLayout.IconRail)));

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(ViewerChromeSample), UIcons.LayoutFluid, "The viewer, with a toolbar already on it")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("PdfJs.ViewerChrome() is PdfJs.Viewer() with the chrome a reader expects around it: panel toggles, page controls, a zoom stepper whose menu holds the fit modes, rotate and spread, an always-visible search box, and a side panel showing the outline or the page thumbnails."),
                        TextBlock("PdfJs.Viewer() on its own draws no toolbar, and that is deliberate: a toolbar is the part that has to look like the rest of your application, and the same viewer is asked for by a full-page reader, a preview pane and a modal, which want three different sets of buttons. Every other page in this group builds its own controls out of ordinary Tesserae calling ordinary methods on the component. This page is the other end of that choice - an application that wants a document reader and no opinion about it should not have to build twelve buttons first.").MT(8),
                        TextBlock("Nothing is hidden behind it. Every control here calls a public method on the component underneath, and chrome.Viewer hands that component back with its whole surface: Options, Configure, the annotation editor, scripting, password handling. Starting with the chrome and replacing it later costs the toolbar and nothing else.").MT(8),
                        TextBlock("It follows the theme. Every colour resolves to a --tss- variable, so the toolbar you see here is the light theme and UI.Theme.Dark() is the whole of the dark one - try the theme switch in the top bar.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Give it a height, or a parent that has one. The chrome is a column - toolbar, then body - and the viewer inside it takes what is left, so in a container of no height it draws a toolbar above nothing."),
                        TextBlock("Search is two modes rather than three checkboxes. Fuzzy is pdf.js's defaults - case, accents and word boundaries all ignored - and Precise turns all three on at once, because a reader who wants one of them wants all of them. FindOptions is still there on the viewer for a host that needs them separately.").MT(8),
                        TextBlock("The panel earns its keep on long documents. Thumbnails are built as they scroll into view, so a 248-page document costs 248 empty frames and about a dozen renders; and the outline resolves each entry to a page number, which is what lets it show which section the reader is currently inside.").MT(8),
                        TextBlock("The panel has no tab strip. The two toolbar toggles already say which pane is open and are what a reader reaches for, so a strip under them was the same answer twice - and the width it took is the outline\u0027s now. Panel() and TogglePanel() are the same switch from code.").MT(8),
                        TextBlock("Border() when nothing else draws the edge. The chrome\u0027s own lines are internal - under the toolbar, beside the panel - because a viewer filling a window has nothing to frame, and inside a modal or a Card a second line just inside the container\u0027s own reads as a seam. On a page\u0027s bare background it is the other way round, and the frame is what says where the document ends. CornerRadius() squares it off or rounds it further.").MT(8),
                        TextBlock("Turn off what you do not want rather than rebuilding. ShowZoom(false), ShowSpread(false), Tabs(thumbnails: false) and their siblings each drop a control and re-close the gap, which is usually what a preview pane wants instead of a second component.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        HStack().WS().Children(layoutChoice, borderChoice.ML(24)),
                        status.MT(8),
                        chrome.H(620).WS().MT(8),
                        SampleHint("Type \"tesserae\" into the search box - it is on three pages of this document, so the count should settle on 3 / 3. Switch to Precise and it still finds them; search \"Tesserae\" in Precise and it goes red. Click an entry in the outline to jump to its section, and watch the entry you are inside stay marked as the document scrolls.")
                    )).SetTitle("Usage")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("The icon rail, and a document name"),
                        TextBlock("The same controls, arranged for a narrower container: the top bar keeps the document's name, the page controls and search, and the view controls move onto a 48px rail. The name comes from the URL unless DocumentName gives it one.").Small().Secondary(),
                        rail.H(560).WS().MT(8),
                        SampleHint("Click a thumbnail, then scroll the document - the selected tile follows the page, and scrolls itself into view when it has to.")
                    )).SetTitle("Layouts")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Pared back for a narrow pane"),
                        TextBlock("The same chrome with ShowZoom(false), ShowRotate(false), ShowSpread(false) and Tabs(thumbnails: false). Dropping a control re-closes the gap it left, so the result reads as a smaller toolbar rather than a toolbar with holes in it - which is usually what a preview pane wants instead of a second component.").Small().Secondary(),
                        TextBlock("Everything the fuller toolbar can do is still reachable, because it was never the toolbar doing it: compact.Viewer is the same component, and the chrome is only the buttons.").Small().Secondary().MT(8),
                        TextBlock("This one also has Border(). It is 760x420 on a surface rather than filling its container, so nothing around it draws the edge and the frame is what says where the document ends - where the chrome above fills its own box and does not need one. Try the Frame switch on it to see the difference.").Small().Secondary().MT(8),
                        // Its own row rather than beside anything: as a flex item next to a growing
                        // sibling the width would be a starting point rather than a width, and the
                        // toolbar would be squeezed by whatever it was sharing the row with.
                        compact.W(760).H(420).MT(8),
                        SampleHint("The search box shrinks before anything else in the toolbar does, so the controls survive a narrow container and the search field gives up width for them.")
                    )).SetTitle("Paring it back")))
               .SeeAlso(typeof(SearchSample), typeof(OutlineAndNavigationSample), typeof(ZoomAndFitSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
