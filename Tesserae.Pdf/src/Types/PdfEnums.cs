using Transpose;

namespace Tesserae.Pdf
{
    /// <summary>
    /// How much of a page's annotation layer to build.
    ///
    /// The default across this package is <see cref="Enable"/> for a bare page render and
    /// <see cref="EnableForms"/> for the viewer, matching pdf.js: links work everywhere, and form
    /// fields are only interactive where there is a viewer to hold their state.
    /// </summary>
    [Enum(Emit.Value)]
    public enum AnnotationMode
    {
        /// <summary>No annotation layer at all - not even links.</summary>
        Disable = 0,

        /// <summary>Annotations are drawn and links work; form fields are painted, not editable.</summary>
        Enable = 1,

        /// <summary>Form fields become real inputs the user can type into.</summary>
        EnableForms = 2,

        /// <summary>
        /// As <see cref="EnableForms"/>, and field values are read from and written back to the
        /// document's annotation storage - which is what makes them survive a re-render and reach
        /// <c>SaveAsync</c>.
        /// </summary>
        EnableStorage = 3,
    }

    /// <summary>
    /// Which annotation-editing tool the viewer's editor layer is in.
    ///
    /// <see cref="Disable"/> is not "no tool selected" - it takes the editor layer out entirely, and
    /// is the package default. <see cref="None"/> builds the layer with no tool active, which is what
    /// a host wants before the user has picked one.
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
