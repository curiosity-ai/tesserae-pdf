namespace Tesserae.Pdf
{
    /// <summary>
    /// The chrome's glyphs, as SVG source.
    ///
    /// <b>Why not Tesserae's icon font.</b> <c>UIcons</c> is a webfont, and a font glyph cannot be
    /// stroked at 1.75px on a 16px box - which is what makes these read as one set rather than as
    /// twelve unrelated pictograms. The set here is small, fixed, and sized to the control it sits in
    /// (16px in a 32px button, 14px in a 28px field, 13px in a 22px stepper, 11px on an outline
    /// twisty), so it is drawn rather than typed.
    ///
    /// Every glyph paints with <c>currentColor</c> and inherits its colour from the button, which is
    /// what lets one rule set the hover, disabled and selected colours for all of them.
    ///
    /// <b>These strings are assigned to <c>innerHTML</c></b>, by <see cref="PdfChromeElements"/>.
    /// That is safe here in the way it is not in general: they are compile-time constants in this
    /// file with no interpolation, so there is no untrusted text anywhere near them. The alternative
    /// is roughly two hundred <c>createElementNS</c> calls for the same pixels.
    /// </summary>
    internal static class PdfChromeIcons
    {
        private const string STROKE  = @"fill=""none"" stroke=""currentColor"" stroke-linecap=""round"" stroke-linejoin=""round""";
        private const string SIZE_16 = @"width=""16"" height=""16"" viewBox=""0 0 24 24"" stroke-width=""1.75""";
        private const string SIZE_14 = @"width=""14"" height=""14"" viewBox=""0 0 24 24"" stroke-width=""1.75""";
        private const string SIZE_13 = @"width=""13"" height=""13"" viewBox=""0 0 24 24"" stroke-width=""2""";
        private const string SIZE_12 = @"width=""12"" height=""12"" viewBox=""0 0 24 24"" stroke-width=""2""";

        /// <summary>Two columns of rules - the outline panel's toggle.</summary>
        internal const string OUTLINE = "<svg " + SIZE_16 + " " + STROKE + "><path d=\"M4 6h5M4 12h5M4 18h5M13 6h7M13 12h7M13 18h7\"/></svg>";

        /// <summary>A two-by-two grid - the thumbnails panel's toggle.</summary>
        internal const string THUMBNAILS = "<svg " + SIZE_16 + " " + STROKE + "><rect x=\"3.5\" y=\"3.5\" width=\"7\" height=\"7\" rx=\"1\"/><rect x=\"13.5\" y=\"3.5\" width=\"7\" height=\"7\" rx=\"1\"/><rect x=\"3.5\" y=\"13.5\" width=\"7\" height=\"7\" rx=\"1\"/><rect x=\"13.5\" y=\"13.5\" width=\"7\" height=\"7\" rx=\"1\"/></svg>";

        /// <summary>Chevron up, at button size. Previous page - the document scrolls downwards.</summary>
        internal const string CHEVRON_UP_16 = "<svg " + SIZE_16 + " " + STROKE + "><path d=\"m18 15-6-6-6 6\"/></svg>";

        /// <summary>Chevron down, at button size. Next page.</summary>
        internal const string CHEVRON_DOWN_16 = "<svg " + SIZE_16 + " " + STROKE + "><path d=\"m6 9 6 6 6-6\"/></svg>";

        /// <summary>Chevron up, at stepper size. Previous search match.</summary>
        internal const string CHEVRON_UP_13 = "<svg " + SIZE_13 + " " + STROKE + "><path d=\"m18 15-6-6-6 6\"/></svg>";

        /// <summary>Chevron down, at stepper size. Next search match.</summary>
        internal const string CHEVRON_DOWN_13 = "<svg " + SIZE_13 + " " + STROKE + "><path d=\"m6 9 6 6 6-6\"/></svg>";

        /// <summary>
        /// The disclosure chevron on the zoom button. Drawn in the faint grey rather than
        /// <c>currentColor</c>, so it stays quieter than the percentage beside it.
        /// </summary>
        internal const string CHEVRON_DOWN_12_FAINT = "<svg " + SIZE_12 + " fill=\"none\" stroke=\"var(--tsspdf-fg-faint)\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"m6 9 6 6 6-6\"/></svg>";

        /// <summary>A magnifier with a minus in it.</summary>
        internal const string ZOOM_OUT = "<svg " + SIZE_16 + " fill=\"none\" stroke=\"currentColor\" stroke-linecap=\"round\"><circle cx=\"10.5\" cy=\"10.5\" r=\"6.75\"/><path d=\"m19.5 19.5-4-4M7.75 10.5h5.5\"/></svg>";

        /// <summary>A magnifier with a plus in it.</summary>
        internal const string ZOOM_IN = "<svg " + SIZE_16 + " fill=\"none\" stroke=\"currentColor\" stroke-linecap=\"round\"><circle cx=\"10.5\" cy=\"10.5\" r=\"6.75\"/><path d=\"m19.5 19.5-4-4M7.75 10.5h5.5M10.5 7.75v5.5\"/></svg>";

        /// <summary>A portrait page with arrows top and bottom - fit the whole page.</summary>
        internal const string FIT_PAGE_14 = "<svg " + SIZE_14 + " " + STROKE + "><rect x=\"5\" y=\"3\" width=\"14\" height=\"18\" rx=\"2\"/><path d=\"m9.5 9 2.5-2.5L14.5 9M9.5 15l2.5 2.5L14.5 15\"/></svg>";

        /// <summary>A landscape page with arrows left and right - fit the width.</summary>
        internal const string FIT_WIDTH_14 = "<svg " + SIZE_14 + " " + STROKE + "><rect x=\"3\" y=\"5\" width=\"18\" height=\"14\" rx=\"2\"/><path d=\"m9 9-2.5 3L9 15M15 9l2.5 3L15 15\"/></svg>";

        /// <summary>The same two, at button size, for the icon rail.</summary>
        internal const string FIT_PAGE_16 = "<svg " + SIZE_16 + " " + STROKE + "><rect x=\"5\" y=\"3\" width=\"14\" height=\"18\" rx=\"2\"/><path d=\"m9.5 9 2.5-2.5L14.5 9M9.5 15l2.5 2.5L14.5 15\"/></svg>";

        internal const string FIT_WIDTH_16 = "<svg " + SIZE_16 + " " + STROKE + "><rect x=\"3\" y=\"5\" width=\"18\" height=\"14\" rx=\"2\"/><path d=\"m9 9-2.5 3L9 15M15 9l2.5 3L15 15\"/></svg>";

        /// <summary>A circular arrow, open at the top right.</summary>
        internal const string ROTATE = "<svg " + SIZE_16 + " " + STROKE + "><path d=\"M20.5 12a8.5 8.5 0 1 1-2.9-6.4\"/><path d=\"M20.5 4v5h-5\"/></svg>";

        /// <summary>Two pages side by side - the spread toggle.</summary>
        internal const string SPREAD = "<svg " + SIZE_16 + " fill=\"none\" stroke=\"currentColor\" stroke-linejoin=\"round\"><rect x=\"3.5\" y=\"4.5\" width=\"7.5\" height=\"15\" rx=\"1\"/><rect x=\"13\" y=\"4.5\" width=\"7.5\" height=\"15\" rx=\"1\"/></svg>";

        /// <summary>A magnifier, at field size - the search box's leading glyph.</summary>
        internal const string SEARCH_14 = "<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\"><circle cx=\"11\" cy=\"11\" r=\"7\"/><path d=\"m20 20-3.5-3.5\"/></svg>";

        /// <summary>A cross - clear the search.</summary>
        internal const string CLOSE_12 = "<svg " + SIZE_12 + " fill=\"none\" stroke=\"currentColor\" stroke-linecap=\"round\"><path d=\"M6 6l12 12M18 6 6 18\"/></svg>";

        /// <summary>A tick, for the selected entry in the zoom menu.</summary>
        internal const string CHECK_13 = "<svg width=\"13\" height=\"13\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"m5 12 5 5L20 7\"/></svg>";

        /// <summary>An info circle, for the line explaining what Precise means.</summary>
        internal const string INFO_12 = "<svg " + SIZE_12 + " fill=\"none\" stroke=\"var(--tsspdf-accent)\" stroke-linecap=\"round\"><circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 8h.01M12 11v5\"/></svg>";

        /// <summary>
        /// A dog-eared page, stroked in the danger red - the file glyph beside the document's name.
        /// Red because that is what a PDF is drawn in everywhere else, not because anything is wrong.
        /// </summary>
        internal const string FILE_PDF = "<svg " + SIZE_16 + " fill=\"none\" stroke=\"var(--tsspdf-danger)\" stroke-linejoin=\"round\"><path d=\"M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z\"/><path d=\"M14 3v5h5\"/></svg>";

        /// <summary>A filled triangle - the outline tree's twisty. Rotated by CSS when open.</summary>
        internal const string TWISTY = "<svg width=\"11\" height=\"11\" viewBox=\"0 0 24 24\" fill=\"currentColor\"><path d=\"M9 5l7 7-7 7z\"/></svg>";
    }
}
