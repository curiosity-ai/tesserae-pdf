using System.Collections.Generic;
using Transpose.Core;

namespace Tesserae.Pdf
{
    /// <summary>
    /// One entry in a document's outline - what a PDF reader shows as its bookmark tree.
    ///
    /// The raw pdf.js node is kept in <see cref="Destination"/> and handed back to the viewer's link
    /// service untouched, because a destination is either a name or an explicit array of page,
    /// zoom mode and coordinates, and re-encoding either of those loses information.
    /// </summary>
    public sealed class PdfOutlineItem
    {
        internal PdfOutlineItem(IOutlineNode node)
        {
            Title       = node.title;
            Bold        = node.bold;
            Italic      = node.italic;
            Url         = node.url;
            NewWindow   = node.newWindow;
            Destination = node.dest;
            Color       = ToCssColor(node.color);

            // pdf.js reports the PDF's own child count, which is negative when the branch is meant to
            // open collapsed. The children are present either way.
            StartsCollapsed = node.count < 0;

            var children = new List<PdfOutlineItem>();

            if (node.items is object)
            {
                foreach (var child in node.items)
                {
                    children.Add(new PdfOutlineItem(child));
                }
            }

            Children = children;
        }

        /// <summary>The label to show.</summary>
        public string Title { get; }

        /// <summary>Whether the PDF asks for the label in bold.</summary>
        public bool Bold { get; }

        /// <summary>Whether the PDF asks for the label in italics.</summary>
        public bool Italic { get; }

        /// <summary>The label's colour as a CSS <c>rgb(...)</c>, or null when the PDF names none.</summary>
        public string Color { get; }

        /// <summary>An external link, when this entry is one instead of a place in the document.</summary>
        public string Url { get; }

        /// <summary>Whether an external link asks to open in a new window.</summary>
        public bool NewWindow { get; }

        /// <summary>
        /// Where in the document this points, in pdf.js's own form. Pass it to
        /// <c>PdfViewer.GoToDestination</c>; there is nothing useful to read out of it directly.
        /// </summary>
        public object Destination { get; }

        /// <summary>Whether the PDF asks for this branch to start closed.</summary>
        public bool StartsCollapsed { get; }

        /// <summary>Nested entries. Empty rather than null for a leaf.</summary>
        public IReadOnlyList<PdfOutlineItem> Children { get; }

        /// <summary>Whether this entry points anywhere at all - some are headings with no target.</summary>
        public bool HasTarget => Destination is object || !string.IsNullOrEmpty(Url);

        private static string ToCssColor(es5.Uint8ClampedArray color)
        {
            if (color is null || color.length < 3) return null;

            // Black is what pdf.js reports for an entry that names no colour of its own, and saying
            // "no colour" lets the caller use its own foreground - which is what a dark theme needs.
            if (color[0] == 0 && color[1] == 0 && color[2] == 0) return null;

            return "rgb(" + color[0] + ", " + color[1] + ", " + color[2] + ")";
        }
    }
}
