using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// The five zoom modes, and the one thing about them worth knowing: pdf.js resolves a fit mode
    /// into a number once, so a viewer that fitted its width in a narrow pane keeps that zoom in a
    /// wide one unless somebody re-applies it. The component does; the slider below lets you watch it.
    /// </summary>
    [SampleDetails(Group = "Viewer", Order = 20, Icon = UIcons.ZoomIn)]
    public class ZoomAndFitSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public ZoomAndFitSample()
        {
            var report = TextBlock("-").Small().Secondary();

            var viewer = PdfJs.Viewer();

            viewer
               .Url(OUTLINE_PDF)
               .FitWidth()
               .OnScaleChanged(scale => report.Text = $"scale {scale:0.###} - reported preset \"{viewer.ScaleValue}\"");

            // The container the viewer sits in, narrowed and widened by the slider. Resizing this is
            // what a resizable pane, a collapsing sidebar or a rotating phone does to a viewer, and
            // it is the case the fit modes have to survive.
            var widthPercent = new SettableObservable<int>(100);
            var widthSlider  = Slider(100, 30, 100, 5).Bind(widthPercent).Width(240.px());

            var frame = VStack().H(520).W(100.percent()).Children(viewer.S());

            widthPercent.ObserveFutureChanges(percent => frame.Width(percent.percent()));

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(ZoomAndFitSample), UIcons.ZoomIn, "Fit modes, and what a resize does to them")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("FitWidth, FitPage, FitHeight, ActualSize and AutoZoom are pdf.js's five named zoom modes. Each is resolved against the container: fitting the width means \"whatever scale makes this page as wide as the space it has\"."),
                        TextBlock("Zoom(1.4) sets an explicit scale instead, and ZoomIn / ZoomOut step relative to whatever is current. Scale reads the number back, ScaleValue reads pdf.js's string form - which is either a number or the name of the mode that produced it.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("pdf.js resolves a fit mode once, into a number, and does not re-resolve it. So a viewer told to fit its width at 600px wide is still at that scale when its pane is dragged to 1200px - the pages just stop filling it. The component watches its container and re-applies the mode for you, which is what KeepFitOnResize(false) turns off."),
                        TextBlock("Only a named mode is re-applied. An explicit Zoom(1.4), or a zoom the user reached with the zoom buttons, is left alone - re-applying that would undo what they did. The component tells the two apart by the presetValue pdf.js reports alongside every scale change.").MT(8),
                        TextBlock("Reach for a fit mode rather than a number wherever you can. A number is right when the user picked it and wrong the moment the layout changes.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        HStack().WS().Gap(4.px()).Wrap().Children(
                            Button("Fit width").OnClick(() => viewer.FitWidth()),
                            Button("Fit page").OnClick(() => viewer.FitPage()),
                            Button("Fit height").OnClick(() => viewer.FitHeight()),
                            Button("Actual size").OnClick(() => viewer.ActualSize()),
                            Button("Auto").OnClick(() => viewer.AutoZoom()),
                            Button("140%").OnClick(() => viewer.Zoom(1.4))),
                        HStack().WS().Gap(16.px()).MT(8).Children(
                            VStack().Children(TextBlock("Container width (%)").Small().SemiBold(), widthSlider)),
                        report.MT(8),
                        frame.MT(8),
                        SampleHint("Pick \"Fit width\", then drag the width slider: the scale follows. Pick \"140%\" and drag it again: the scale stays put, because you asked for that number.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(ViewerChromeSample), typeof(ScrollAndSpreadSample), typeof(RemountSample));
        }

        public HTMLElement Render() => _content.Render();
    }
}
