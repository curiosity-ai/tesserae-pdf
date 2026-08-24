using System;
using System.Threading.Tasks;
using Tesserae;

namespace Tesserae.Pdf
{
    /// <summary>
    /// One control the host application added to the chrome - a download, a print, an "open in the
    /// workspace". See <see cref="PdfViewerChrome.AddAction(UIcons, string, Action)"/>.
    ///
    /// The chrome draws these as ordinary icon buttons in a group of their own and moves them into the
    /// overflow menu on the bands that shed controls, so a host action is never less reachable than
    /// the chrome's own. It is a description rather than a control on purpose: the toolbar is rebuilt
    /// whenever the layout or the visible set changes, and a button kept here would be a second copy
    /// of what the rebuild draws.
    /// </summary>
    internal sealed class PdfChromeAction
    {
        internal PdfChromeAction(UIcons icon, string label, Func<Task> run, bool spinWhileRunning)
        {
            Icon             = icon;
            Label            = label;
            Run              = run;
            SpinWhileRunning = spinWhileRunning;
        }

        internal UIcons Icon { get; }

        /// <summary>The tooltip on the button, and the row's text in the overflow menu.</summary>
        internal string Label { get; }

        internal Func<Task> Run { get; }

        /// <summary>
        /// Whether the button spins for as long as the handler runs. Only for the asynchronous
        /// overload: a synchronous handler has returned by the time the spinner could be drawn.
        /// </summary>
        internal bool SpinWhileRunning { get; }
    }
}
