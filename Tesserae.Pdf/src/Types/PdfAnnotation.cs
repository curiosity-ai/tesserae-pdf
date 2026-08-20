using Transpose;

namespace Tesserae.Pdf
{
    /// <summary>
    /// One annotation on a page, as pdf.js reports it - a link, a form widget, a popup note, a stamp.
    ///
    /// pdf.js's own shape is a plain object with a different set of keys per annotation type, so only
    /// the members that are common, or that this package needs, are declared. Anything else is
    /// reachable on the same object with <c>Script.Get</c>.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    public interface IPdfAnnotation
    {
        /// <summary>pdf.js's own id for the annotation, e.g. <c>"12R"</c>.</summary>
        string id { get; }

        /// <summary>The PDF annotation subtype: <c>"Link"</c>, <c>"Widget"</c>, <c>"Text"</c>, ...</summary>
        string subtype { get; }

        /// <summary>The annotation's box in PDF units: <c>[x1, y1, x2, y2]</c>.</summary>
        double[] rect { get; }

        /// <summary>On a link: the URL it points to.</summary>
        string url { get; }

        /// <summary>On a link: the in-document destination it points to, in pdf.js's own form.</summary>
        object dest { get; }

        /// <summary>On a form widget: the field's name.</summary>
        string fieldName { get; }

        /// <summary>On a form widget: <c>"Tx"</c> (text), <c>"Btn"</c> (button), <c>"Ch"</c> (choice), <c>"Sig"</c>.</summary>
        string fieldType { get; }

        /// <summary>
        /// On a form widget: its value. A string for a text field or a button's state, an array for a
        /// multi-select choice - which is why it is not typed more tightly here.
        /// </summary>
        object fieldValue { get; }

        /// <summary>Whether the document asks for this annotation to be hidden.</summary>
        bool hidden { get; }

        /// <summary>On a form widget: whether the field refuses input.</summary>
        bool readOnly { get; }

        /// <summary>On a form widget: whether the document marks it required.</summary>
        bool required { get; }
    }

    /// <summary>
    /// One annotation, in terms a host can branch on. Built from <see cref="IPdfAnnotation"/>, and
    /// keeping the raw object on <see cref="Instance"/> for the members that vary by type.
    /// </summary>
    public sealed class PdfAnnotation
    {
        internal PdfAnnotation(IPdfAnnotation annotation)
        {
            Instance    = annotation;
            Id          = annotation.id;
            Subtype     = annotation.subtype;
            Url         = annotation.url;
            Destination = annotation.dest;
            FieldName   = annotation.fieldName;
            FieldType   = annotation.fieldType;
            IsHidden    = annotation.hidden;
            IsReadOnly  = annotation.readOnly;
            IsRequired  = annotation.required;

            var value = annotation.fieldValue;

            // A choice field's value is an array and a text field's is a string. Both are shown the
            // same way, so the array is joined rather than handed back as an object nobody can print.
            if (value is object[] many) FieldValue = string.Join(", ", System.Array.ConvertAll(many, item => item is object ? item.ToString() : ""));
            else if (value is object)   FieldValue = value.ToString();
        }

        /// <summary>The raw pdf.js annotation, for the members that vary by type.</summary>
        public IPdfAnnotation Instance { get; }

        /// <summary>pdf.js's own id, e.g. <c>"12R"</c> - also the key into the document's annotation storage.</summary>
        public string Id { get; }

        /// <summary>The PDF subtype: <c>"Link"</c>, <c>"Widget"</c>, <c>"Text"</c>, ...</summary>
        public string Subtype { get; }

        /// <summary>Whether this is a form field.</summary>
        public bool IsFormField => Subtype == "Widget";

        /// <summary>Whether this is a link, internal or external.</summary>
        public bool IsLink => Subtype == "Link";

        /// <summary>On a link: the URL. Null for an internal one.</summary>
        public string Url { get; }

        /// <summary>On an internal link: the destination, to hand to <c>GoToDestination</c>.</summary>
        public object Destination { get; }

        /// <summary>On a form field: its name.</summary>
        public string FieldName { get; }

        /// <summary>On a form field: <c>"Tx"</c>, <c>"Btn"</c>, <c>"Ch"</c> or <c>"Sig"</c>.</summary>
        public string FieldType { get; }

        /// <summary>On a form field: its current value as text. Null when it has none.</summary>
        public string FieldValue { get; }

        /// <summary>Whether the document asks for it to be hidden.</summary>
        public bool IsHidden { get; }

        /// <summary>On a form field: whether it refuses input.</summary>
        public bool IsReadOnly { get; }

        /// <summary>On a form field: whether the document marks it required.</summary>
        public bool IsRequired { get; }
    }
}
