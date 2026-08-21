using System;
using Transpose;
using static Transpose.Core.dom;

namespace Tesserae.Pdf
{
    /// <summary>
    /// The handful of element shapes the chrome is built out of.
    ///
    /// Plain DOM rather than Tesserae components, for the same reason <see cref="PdfViewer"/> builds
    /// its own scroll host: these are fixed-size pieces of a fixed-size toolbar - a 32px square
    /// button, a 28px field, a 24px segment - and a component that carries its own margins, focus
    /// ring and font sizing has to be argued out of all three before it will sit in a 40px row. What
    /// is left after that argument is what is written out here.
    /// </summary>
    internal static class PdfChromeElements
    {
        /// <summary>A div with a class and nothing else.</summary>
        internal static HTMLElement Box(string className)
        {
            var element = document.createElement("div").As<HTMLElement>();

            element.className = className;

            return element;
        }

        /// <summary>A span carrying text.</summary>
        internal static HTMLElement Text(string className, string text)
        {
            var element = document.createElement("span").As<HTMLElement>();

            element.className   = className;
            element.textContent = text;

            return element;
        }

        /// <summary>A span carrying one of <see cref="PdfChromeIcons"/>.</summary>
        internal static HTMLElement Glyph(string className, string svg)
        {
            var element = document.createElement("span").As<HTMLElement>();

            element.className = className;
            element.innerHTML = svg;

            return element;
        }

        /// <summary>
        /// A button. Always <c>type="button"</c>: the chrome can be dropped inside a host's form, and
        /// the default type is <c>submit</c>, which would post it.
        /// </summary>
        internal static HTMLButtonElement Button(string className, string tooltip, Action click)
        {
            var button = document.createElement("button").As<HTMLButtonElement>();

            button.className = className;
            button.type      = "button";

            if (!string.IsNullOrEmpty(tooltip))
            {
                button.title = tooltip;

                // The glyph is the only content of an icon button, and an <svg> is not a label - so
                // without this the button is announced as "button" and nothing else.
                button.setAttribute("aria-label", tooltip);
            }

            if (click is object)
            {
                button.addEventListener("click", new Action<Event>(_ => click()));
            }

            return button;
        }

        /// <summary>An icon button: a 32px square carrying one glyph.</summary>
        internal static HTMLButtonElement IconButton(string svg, string tooltip, Action click)
        {
            var button = Button("tsspdf-iconbtn", tooltip, click);

            button.innerHTML = svg;

            return button;
        }

        /// <summary>
        /// One segment of a segmented control - an icon and a label, or just a label.
        ///
        /// The label is the accessible name, so the tooltip repeats it rather than adding to it;
        /// which is why it is not also set as <c>aria-label</c> here.
        /// </summary>
        internal static HTMLButtonElement Segment(string svg, string label, string tooltip, Action click)
        {
            var button = document.createElement("button").As<HTMLButtonElement>();

            button.className = "tsspdf-seg-item";
            button.type      = "button";

            if (!string.IsNullOrEmpty(tooltip)) button.title = tooltip;

            if (!string.IsNullOrEmpty(svg)) button.appendChild(Glyph("", svg));

            button.appendChild(Text("", label));

            if (click is object)
            {
                button.addEventListener("click", new Action<Event>(_ => click()));
            }

            return button;
        }

        /// <summary>A vertical hairline between groups of controls.</summary>
        internal static HTMLElement Separator() => Box("tsspdf-sep");

        /// <summary>The flexible gap that pushes everything after it to the right.</summary>
        internal static HTMLElement Spring() => Box("tsspdf-spring");

        /// <summary>Adds or removes a class, from a bool.</summary>
        internal static void Toggle(HTMLElement element, string className, bool on)
        {
            if (element is null) return;

            if (on)
            {
                element.classList.add(className);
            }
            else
            {
                element.classList.remove(className);
            }
        }

        /// <summary>Removes every child of an element, without going through <c>innerHTML</c>.</summary>
        internal static void Empty(HTMLElement element)
        {
            if (element is null) return;

            while (element.firstChild is object)
            {
                element.removeChild(element.firstChild);
            }
        }

        /// <summary>
        /// Sets the <c>hidden</c>-ness of an element by display, not by the attribute: pdf.js's
        /// stylesheet and Tesserae's both set <c>display</c> on things, and <c>[hidden]</c> loses to
        /// a class rule where <c>display:none</c> inline does not.
        /// </summary>
        internal static void Show(HTMLElement element, bool visible)
        {
            if (element is null) return;

            element.style.display = visible ? "" : "none";
        }
    }
}
