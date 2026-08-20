using System.Threading.Tasks;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// The annotation layer: links, form fields, and the editing tools. The interesting part is the
    /// annotation mode, because it is what decides whether a form field is a picture of an input or a
    /// real one whose value survives.
    /// </summary>
    [SampleDetails(Group = "Viewer", Order = 70, Icon = UIcons.Pencil)]
    public class FormsAndAnnotationsSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public FormsAndAnnotationsSample()
        {
            var status = TextBlock("-").Small().Secondary();
            var values = VStack().WS().Gap(2.px());

            var viewer = PdfJs.Viewer();

            viewer
               .Url(FORMS_PDF)
               .FitWidth()
               // EnableForms, not EnableStorage: pdf.js tests for exactly this value when deciding
               // whether to build real inputs, so the "higher" mode silently produces an empty
               // annotation layer. See the remarks on AnnotationMode.
               .Annotations(AnnotationMode.EnableForms)

               // Set before the component mounts, which is the only point the editor layer can be
               // built at all. None means "no tool active".
               .AnnotationEditor(AnnotationEditorMode.None)
               .OnDocumentLoaded(document => status.Text = $"{document.PageCount} page, annotation mode EnableForms, editor enabled.")
               .OnAnnotationEditorModeChanged(mode => status.Text = "Annotation editor: " + mode);

            var read = Button("Read the field values back").SetIcon(UIcons.ListCheck)
               .OnClick(() => ReadFieldsAsync(viewer, values).FireAndForget());

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(FormsAndAnnotationsSample), UIcons.Pencil, "Form fields, and the annotation editor")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("Annotations(AnnotationMode) decides how much of a page's annotation layer is built. Disable draws none - not even links. Enable draws them and makes links work, but form fields are pictures of inputs. EnableForms makes them real inputs, and what the user types is kept in the document's annotation storage - which is what makes it survive a re-render and reach SaveAsync. EnableForms is the default, and the mode a viewer wants."),
                        TextBlock("AnnotationEditor(AnnotationEditorMode) is separate: it turns on pdf.js's editing tools for highlights, free text, ink and stamps. Whether the editor exists is decided before the viewer is built - Disable, the default, leaves the layer out - and after that the tools switch freely, with None meaning \"no tool active\".").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("EnableStorage is the trap, and it is a good one. Its name and its position in the enum both suggest \"EnableForms and then some\", and pdf.js tests for exactly EnableForms when deciding whether to build interactive controls - so a viewer set to EnableStorage renders an annotation layer with nothing in it, logs nothing, and throws nothing. If a form looks like it lost its fields, that is the first thing to check. EnableStorage belongs on a page render, where there are no inputs to build and it means \"include the values already entered\"."),
                        TextBlock("Reading the values back is page.GetAnnotationsAsync(): one entry per widget, each with its field name, type and current value - which is what the button below uses. Writing the whole document out with SaveAsync() bakes them in.").MT(8),
                        TextBlock("Two limits on the editor, both pdf.js's. It only builds its editor machinery when the viewer is constructed with the editor enabled, and its own setter rejects Disable - so \"is there an editor\" is decided up front and \"which tool\" afterwards. The component turns both into errors that say so. Changes the user makes with the keyboard arrive through OnAnnotationEditorModeChanged rather than being something you set.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        HStack().WS().Gap(4.px()).Wrap().Children(
                            Button("No tool").OnClick(() => viewer.AnnotationEditor(AnnotationEditorMode.None)),
                            Button("Highlight").OnClick(() => viewer.AnnotationEditor(AnnotationEditorMode.Highlight)),
                            Button("Free text").OnClick(() => viewer.AnnotationEditor(AnnotationEditorMode.FreeText)),
                            Button("Ink").OnClick(() => viewer.AnnotationEditor(AnnotationEditorMode.Ink)),
                            read),
                        status.MT(8),
                        viewer.H(560).WS().MT(8),
                        SampleSubTitle("What the fields hold"),
                        values,
                        SampleHint("Type into the name field, then read the values back: what you typed is there. The document starts with \"Ada Lovelace\" in it and \"Team\" selected.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(DownloadAndSaveSample), typeof(ScriptingSample), typeof(MetadataSample));
        }

        private static async Task ReadFieldsAsync(PdfViewer viewer, Stack values)
        {
            values.Clear();

            var document = viewer.Document;

            if (document is null)
            {
                values.Add(TextBlock("No document loaded.").Small().Secondary());

                return;
            }

            // Read off the page's annotations rather than the document's field table: one entry per
            // widget, each carrying the value the user has actually typed.
            var page        = await document.GetPageAsync(1);
            var annotations = await page.GetAnnotationsAsync();
            var any         = false;

            foreach (var annotation in annotations)
            {
                if (!annotation.IsFormField) continue;

                any = true;

                values.Add(HStack().WS().Gap(8.px()).Children(
                    TextBlock(annotation.FieldName ?? "(unnamed)").Tiny().SemiBold().W(90),
                    TextBlock(annotation.FieldType ?? "?").Tiny().Secondary().W(40),
                    TextBlock(annotation.FieldValue ?? "(empty)").Tiny().Grow(),
                    TextBlock(annotation.IsReadOnly ? "read-only" : "").Tiny().Secondary().W(70)));
            }

            if (!any) values.Add(TextBlock("This page has no form fields.").Small().Secondary());
        }

        public HTMLElement Render() => _content.Render();
    }
}
