using Transpose;

namespace Tesserae.Pdf
{
    /// <summary>
    /// How much of a page's annotation layer to build.
    ///
    /// <b>The last two do not mean what their order suggests, and they mean different things in a
    /// viewer and in a page render.</b> pdf.js decides whether to build interactive form controls
    /// with an exact equality test against <see cref="EnableForms"/> - so in a viewer,
    /// <see cref="EnableStorage"/> is not "forms, plus storage": it is a mode in which the form is
    /// <i>not</i> interactive, and it fails silently, leaving an empty annotation layer and no error
    /// anywhere.
    ///
    /// The right choice by surface:
    /// <list type="bullet">
    /// <item><b>A viewer</b> wants <see cref="EnableForms"/>. Its fields are interactive, and what
    /// the user types is written into the document's annotation storage anyway - which is what makes
    /// it survive a re-render and reach <c>SaveAsync</c>. This is the package's default.</item>
    /// <item><b>A page render</b> wants <see cref="EnableStorage"/> when it should include values
    /// the user has already entered, and <see cref="Enable"/> otherwise. A canvas has no inputs to
    /// make interactive, so <see cref="EnableForms"/> buys it nothing.</item>
    /// </list>
    /// </summary>
    [Enum(Emit.Value)]
    public enum AnnotationMode
    {
        /// <summary>No annotation layer at all - not even links.</summary>
        Disable = 0,

        /// <summary>Annotations are drawn and links work; form fields are painted, not editable.</summary>
        Enable = 1,

        /// <summary>
        /// Form fields become real inputs the user can type into, and what they type is kept in the
        /// document's annotation storage. <b>The mode a viewer wants</b>, and the one pdf.js's own
        /// viewer uses.
        /// </summary>
        EnableForms = 2,

        /// <summary>
        /// A page render includes whatever is in the document's annotation storage - i.e. the values
        /// a user has already typed into a viewer.
        ///
        /// <b>Not a superset of <see cref="EnableForms"/>, and wrong for a viewer</b>: pdf.js builds
        /// interactive controls only for exactly <see cref="EnableForms"/>, so a viewer set to this
        /// renders an annotation layer with nothing in it.
        /// </summary>
        EnableStorage = 3,
    }

    /// <summary>
    /// Which annotation-editing tool the viewer's editor layer is in.
    ///
    /// <see cref="Disable"/> is not "no tool selected" - it takes the editor layer out entirely, and
    /// is the package default. <see cref="None"/> builds the layer with no tool active, which is what
    /// a host wants before the user has picked one, and is also how you turn editing off again once
    /// it is on.
    ///
    /// <b><see cref="Disable"/> is decided once, before the viewer is built, and cannot be set
    /// afterwards.</b> pdf.js only creates its editor machinery when the viewer is constructed with
    /// something other than <see cref="Disable"/>, and its own mode setter rejects
    /// <see cref="Disable"/> outright - so a viewer built without the editor throws when asked for a
    /// tool later, and one built with it can never go back to having no editor layer at all. Enable
    /// it with <see cref="None"/> up front if the user might ever want to annotate.
    /// </summary>
    [Enum(Emit.Value)]
    public enum AnnotationEditorMode
    {
        /// <summary>No editor layer. The default.</summary>
        Disable = -1,

        /// <summary>The editor layer exists; no tool is active.</summary>
        None = 0,

        /// <summary>Free-text notes.</summary>
        FreeText = 3,

        /// <summary>Text highlighting.</summary>
        Highlight = 9,

        /// <summary>Stamps, i.e. pasted images.</summary>
        Stamp = 13,

        /// <summary>Freehand ink.</summary>
        Ink = 15,

        /// <summary>Popup notes.</summary>
        Popup = 16,

        /// <summary>Signatures.</summary>
        Signature = 101,

        /// <summary>Comments.</summary>
        Comment = 102,
    }

    /// <summary>How much pdf.js writes to the console.</summary>
    [Enum(Emit.Value)]
    public enum PdfVerbosity
    {
        /// <summary>Errors only.</summary>
        Errors = 0,

        /// <summary>Errors and warnings. pdf.js's own default, and this package's.</summary>
        Warnings = 1,

        /// <summary>Everything, including per-document timing.</summary>
        Infos = 5,
    }

    /// <summary>Why pdf.js is asking for a password.</summary>
    [Enum(Emit.Value)]
    public enum PasswordReason
    {
        /// <summary>The document is encrypted and no password was supplied.</summary>
        NeedPassword = 1,

        /// <summary>The password supplied was wrong. pdf.js will ask again.</summary>
        IncorrectPassword = 2,
    }

    /// <summary>
    /// What a document permits. pdf.js reports these as a list rather than a bitfield, and reports
    /// <b>nothing at all</b> for a document that places no restrictions - so an empty list means
    /// "everything is forbidden" and a null one means "everything is allowed". See
    /// <c>PdfDocument.GetPermissionsAsync</c>, which keeps that distinction.
    ///
    /// None of this is enforcement: a PDF's permission flags are a request to the viewer, not a lock.
    /// </summary>
    [Enum(Emit.Value)]
    public enum PdfPermission
    {
        Print                = 0x04,
        ModifyContents       = 0x08,
        Copy                 = 0x10,
        ModifyAnnotations    = 0x20,
        FillInteractiveForms = 0x100,
        CopyForAccessibility = 0x200,
        Assemble             = 0x400,
        PrintHighQuality     = 0x800,
    }

    /// <summary>How a bitmap inside a PDF is stored.</summary>
    [Enum(Emit.Value)]
    public enum PdfImageKind
    {
        Grayscale1Bpp = 1,
        Rgb24Bpp      = 2,
        Rgba32Bpp     = 3,
    }
}
