using System;
using System.Collections.Generic;
using TNT;
using static TNT.T;

namespace Tesserae.Pdf
{
    /// <summary>
    /// The English text behind every message id pdf.js can ask a localization implementation for -
    /// all 50 of them, taken from pdf.js's own inlined en-US bundle - each run through Tesserae's TNT
    /// translation table.
    ///
    /// <b>Why the strings are written out as literals</b>, rather than read from a dictionary: TNT
    /// extracts its translatable strings by scanning source for <c>.t()</c> applied to a literal, so
    /// a key built at run time is a key no translator ever sees. The switch below is therefore a
    /// little repetitive on purpose - every English string in it is a compile-time literal at its own
    /// <c>.t()</c> call site, which is what puts it in a host's <c>.tnt</c> file.
    ///
    /// <b>What a host has to do about that.</b> A host's own <c>tnt extract</c> never sees this
    /// source - it lives in a NuGet package - so these keys will not appear in its translation file
    /// on their own. The README lists them; add them to your translation source, or supply them
    /// through whatever merges into <c>TNT.T.SetTranslation</c>.
    ///
    /// <b>Placeholders.</b> Fluent writes them <c>{ $page }</c>; TNT's own convention for a
    /// formattable key is <c>{0}</c>, which is what <c>t($"Page {page}")</c> produces. The five
    /// messages that take an argument are written the TNT way, so a translator sees the same shape
    /// here as everywhere else in a Tesserae app.
    /// </summary>
    internal static class PdfL10nStrings
    {
        /// <summary>
        /// The message for an id, or null when pdf.js asks for one this table does not carry - in
        /// which case <see cref="PdfL10n"/> leaves the element as pdf.js left it.
        /// </summary>
        internal static PdfL10nMessage Get(string id)
        {
            switch (id)
            {
                // The one message that is not a string: Fluent's DATETIME function, formatting a Date
                // the annotation layer passes in. Handled by PdfL10n, which has the argument to
                // format - see FormatDateTime there.
                case DATE_TIME_ID: return null;

                case "pdfjs-comment-floating-button-label":
                    return Message(Value("Comment".t()));

                case "pdfjs-editor-add-comment-button":
                    return Message(Attribute("title", "Add comment".t()));

                case "pdfjs-editor-alt-text-button":
                    return Message(Attribute("aria-label", "Alt text".t()));

                case "pdfjs-editor-alt-text-button-label":
                    return Message(Value("Alt text".t()));

                case "pdfjs-editor-alt-text-decorative-tooltip":
                    return Message(Value("Marked as decorative".t()));

                case "pdfjs-editor-alt-text-edit-button":
                    return Message(Attribute("aria-label", "Edit alt text".t()));

                case "pdfjs-editor-color-picker-free-text-input":
                    return Message(Attribute("title", "Change text color".t()));

                case "pdfjs-editor-color-picker-ink-input":
                    return Message(Attribute("title", "Change drawing color".t()));

                case "pdfjs-editor-colorpicker-blue":
                    return Message(Attribute("title", "Blue".t()));

                case "pdfjs-editor-colorpicker-button":
                    return Message(Attribute("title", "Change color".t()));

                case "pdfjs-editor-colorpicker-dropdown":
                    return Message(Attribute("aria-label", "Color choices".t()));

                case "pdfjs-editor-colorpicker-green":
                    return Message(Attribute("title", "Green".t()));

                case "pdfjs-editor-colorpicker-pink":
                    return Message(Attribute("title", "Pink".t()));

                case "pdfjs-editor-colorpicker-red":
                    return Message(Attribute("title", "Red".t()));

                case "pdfjs-editor-colorpicker-yellow":
                    return Message(Attribute("title", "Yellow".t()));

                case "pdfjs-editor-freetext-added-alert":
                    return Message(Value("Text added".t()));

                case "pdfjs-editor-highlight-added-alert":
                    return Message(Value("Highlight added".t()));

                case "pdfjs-editor-highlight-editor":
                    return Message(Attribute("aria-label", "Highlight editor".t()));

                case "pdfjs-editor-ink-added-alert":
                    return Message(Value("Drawing added".t()));

                case "pdfjs-editor-ink-editor":
                    return Message(Attribute("aria-label", "Drawing editor".t()));

                case "pdfjs-editor-new-alt-text-added-button":
                    return Message(Attribute("aria-label", "Alt text added".t()));

                case "pdfjs-editor-new-alt-text-added-button-label":
                    return Message(Value("Alt text added".t()));

                case "pdfjs-editor-new-alt-text-generated-alt-text-with-disclaimer":
                    return Message(Value("Created automatically: {0}".t()));

                case "pdfjs-editor-new-alt-text-missing-button":
                    return Message(Attribute("aria-label", "Missing alt text".t()));

                case "pdfjs-editor-new-alt-text-missing-button-label":
                    return Message(Value("Missing alt text".t()));

                case "pdfjs-editor-new-alt-text-to-review-button":
                    return Message(Attribute("aria-label", "Review alt text".t()));

                case "pdfjs-editor-new-alt-text-to-review-button-label":
                    return Message(Value("Review alt text".t()));

                case "pdfjs-editor-remove-freetext-button":
                    return Message(Attribute("title", "Remove text".t()));

                case "pdfjs-editor-remove-highlight-button":
                    return Message(Attribute("title", "Remove highlight".t()));

                case "pdfjs-editor-remove-ink-button":
                    return Message(Attribute("title", "Remove drawing".t()));

                case "pdfjs-editor-remove-signature-button":
                    return Message(Attribute("title", "Remove signature".t()));

                case "pdfjs-editor-remove-stamp-button":
                    return Message(Attribute("title", "Remove image".t()));

                case "pdfjs-editor-resizer-bottom-left":
                    return Message(Attribute("aria-label", "Bottom left corner — resize".t()));

                case "pdfjs-editor-resizer-bottom-middle":
                    return Message(Attribute("aria-label", "Bottom middle — resize".t()));

                case "pdfjs-editor-resizer-bottom-right":
                    return Message(Attribute("aria-label", "Bottom right corner — resize".t()));

                case "pdfjs-editor-resizer-middle-left":
                    return Message(Attribute("aria-label", "Middle left — resize".t()));

                case "pdfjs-editor-resizer-middle-right":
                    return Message(Attribute("aria-label", "Middle right — resize".t()));

                case "pdfjs-editor-resizer-top-left":
                    return Message(Attribute("aria-label", "Top left corner — resize".t()));

                case "pdfjs-editor-resizer-top-middle":
                    return Message(Attribute("aria-label", "Top middle — resize".t()));

                case "pdfjs-editor-resizer-top-right":
                    return Message(Attribute("aria-label", "Top right corner — resize".t()));

                case "pdfjs-editor-signature-added-alert":
                    return Message(Value("Signature added".t()));

                case "pdfjs-editor-signature-editor1":
                    return Message(Attribute("aria-description", "Signature editor: {0}".t()));

                case "pdfjs-editor-stamp-added-alert":
                    return Message(Value("Image added".t()));

                case "pdfjs-editor-stamp-editor":
                    return Message(Attribute("aria-label", "Image editor".t()));

                case "pdfjs-free-text2":
                    return Message(Attribute("aria-label", "Text Editor".t()));

                case "pdfjs-highlight-floating-button-label":
                    return Message(Value("Highlight".t()));

                case "pdfjs-page-landmark":
                    return Message(Attribute("aria-label", "Page {0}".t()));

                case "pdfjs-show-comment-button":
                    return Message(Attribute("title", "Show comment".t()));

                case "pdfjs-text-annotation-type":
                    return Message(Attribute("alt", "[{0} Annotation]".t()));
                default: return null;
            }
        }

        /// <summary>
        /// The id whose value is a Fluent <c>DATETIME</c> call rather than text, so it is formatted
        /// from the argument instead of being looked up.
        /// </summary>
        internal const string DATE_TIME_ID = "pdfjs-annotation-date-time-string";

        private static PdfL10nMessage Message(params PdfL10nPart[] parts)
        {
            var message = new PdfL10nMessage();

            foreach (var part in parts)
            {
                if (part.Attribute is null) message.Value = part.Text;
                else                        message.Attributes[part.Attribute] = part.Text;
            }

            return message;
        }

        private static PdfL10nPart Value(string text) => new PdfL10nPart(null, text);

        private static PdfL10nPart Attribute(string attribute, string text) => new PdfL10nPart(attribute, text);
    }

    /// <summary>One piece of a localized message: its text, or one of its attributes.</summary>
    internal sealed class PdfL10nPart
    {
        internal PdfL10nPart(string attribute, string text)
        {
            Attribute = attribute;
            Text      = text;
        }

        /// <summary>The attribute this text belongs on, or null when it is the message's own text.</summary>
        internal string Attribute { get; }

        internal string Text { get; }
    }

    /// <summary>
    /// A localized message: text, attributes, or both.
    ///
    /// A Fluent message legitimately has no value of its own - 36 of pdf.js's 50 are attribute-only,
    /// because they exist to label a button rather than to fill it - so a null
    /// <see cref="Value"/> means "leave the element's text alone", not "translate to nothing".
    /// </summary>
    internal sealed class PdfL10nMessage
    {
        /// <summary>The element's text, or null when the message only carries attributes.</summary>
        internal string Value { get; set; }

        /// <summary>Attributes to set, keyed by attribute name.</summary>
        internal Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>();
    }
}
