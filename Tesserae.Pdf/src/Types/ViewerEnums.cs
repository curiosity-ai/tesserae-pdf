using Transpose;

namespace Tesserae.Pdf
{
    /// <summary>How the viewer lays its pages out.</summary>
    [Enum(Emit.Value)]
    public enum ScrollMode
    {
        /// <summary>pdf.js has not decided yet. Not worth setting.</summary>
        Unknown = -1,

        /// <summary>One column, scrolling down. The default.</summary>
        Vertical = 0,

        /// <summary>One row, scrolling right.</summary>
        Horizontal = 1,

        /// <summary>A grid that wraps - as many pages per row as fit.</summary>
        Wrapped = 2,

        /// <summary>
        /// One page at a time, with no scrolling between them. The closest thing the ordinary viewer
        /// has to <c>SinglePage()</c>, and unlike that one it can be switched at runtime.
        /// </summary>
        Page = 3,
    }

    /// <summary>Whether pages are shown in pairs, like an open book.</summary>
    [Enum(Emit.Value)]
    public enum SpreadMode
    {
        /// <summary>pdf.js has not decided yet. Not worth setting.</summary>
        Unknown = -1,

        /// <summary>One page per row. The default.</summary>
        None = 0,

        /// <summary>Pairs starting on odd pages, so page 1 is alone on the right.</summary>
        Odd = 1,

        /// <summary>Pairs starting on even pages, so pages 1 and 2 are together.</summary>
        Even = 2,
    }

    /// <summary>Whether the viewer builds a selectable text layer over each page.</summary>
    [Enum(Emit.Value)]
    public enum TextLayerMode
    {
        /// <summary>No text layer: nothing is selectable, and search cannot highlight.</summary>
        Disable = 0,

        /// <summary>A text layer on every page. The default.</summary>
        Enable = 1,

        /// <summary>
        /// A text layer, unless the document's permissions forbid copying.
        ///
        /// This is the mode that turns a PDF's "no copying" request into a real restriction on your
        /// users, so it is opt-in: a viewer that silently will not let people select text looks
        /// broken rather than protective.
        /// </summary>
        EnableIfPermitted = 2,
    }

    /// <summary>How a search ended.</summary>
    [Enum(Emit.Value)]
    public enum FindState
    {
        /// <summary>At least one match, and one of them is selected.</summary>
        Found = 0,

        /// <summary>Nothing matched.</summary>
        NotFound = 1,

        /// <summary>
        /// A match, found by continuing past the end of the document (or before its start). Worth
        /// telling the user about - it is why the view jumped backwards.
        /// </summary>
        Wrapped = 2,

        /// <summary>Still searching. Long documents report this before they report a result.</summary>
        Pending = 3,
    }

    /// <summary>Where a link inside a document opens.</summary>
    [Enum(Emit.Value)]
    public enum LinkTarget
    {
        /// <summary>No target attribute; the browser's default, which is the same tab.</summary>
        None = 0,

        /// <summary>The same tab, explicitly.</summary>
        Self = 1,

        /// <summary>A new tab. What a viewer embedded in an app almost always wants.</summary>
        Blank = 2,

        /// <summary>The parent frame.</summary>
        Parent = 3,

        /// <summary>The top-level frame, escaping every iframe.</summary>
        Top = 4,
    }
}
