using System;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Pdf
{
    /// <summary>
    /// The chrome's shared control shapes, built from Tesserae's components.
    ///
    /// <b>Everything interactive in this chrome is a <see cref="Button"/>, a <see cref="TextBox"/>, a
    /// <see cref="SearchBox"/>, a <see cref="Tree"/>, a <see cref="Pivot"/> or a
    /// <see cref="ContextMenu"/>.</b> What is left here is the trimming: Tesserae sizes its controls
    /// for a form - a 36px button carrying a 4px margin - and a toolbar is 40px tall, so each one is
    /// asked to give up its margin, its minimum size and its padding before the sheet gives it the
    /// design's exact number.
    ///
    /// The glyphs are <see cref="UIcons"/>, Tesserae's own icon set, rather than the mockup's drawn
    /// SVG. A font glyph cannot be stroked at 1.75px on a 16px box, so they are a little heavier than
    /// the comp - and they are the icons the rest of a Tesserae application is already using, which
    /// matters more in a toolbar that sits inside one.
    /// </summary>
    internal static class PdfChromeElements
    {
        /// <summary>A 32px square icon button - the toolbar's and the rail's unit.</summary>
        internal static Button IconButton(UIcons icon, string tooltip, Action click)
        {
            var button = Button()
               .SetIcon(icon)
               .NoMargin()
               .NoMinSize()
               .NoPadding()
               .Class("tsspdf-iconbtn");

            return Wire(button, tooltip, click);
        }

        /// <summary>A 22px icon button - the search box's match steppers and its clear.</summary>
        internal static Button StepButton(UIcons icon, string tooltip, Action click)
        {
            var button = Button()
               .SetIcon(icon)
               .NoMargin()
               .NoMinSize()
               .NoPadding()
               .Class("tsspdf-step");

            return Wire(button, tooltip, click);
        }

        /// <summary>One half of a segmented control: an icon and a label, or just a label.</summary>
        internal static Button Segment(string label, UIcons? icon, string tooltip, Action click)
        {
            var button = Button(label)
               .NoMargin()
               .NoMinSize()
               .NoPadding()
               .NoBorder()
               .Class("tsspdf-seg-item");

            if (icon.HasValue) button = button.SetIcon(icon.Value);

            return Wire(button, tooltip, click);
        }

        private static Button Wire(Button button, string tooltip, Action click)
        {
            if (!string.IsNullOrEmpty(tooltip)) button = Tip(button, tooltip);

            if (click is object) button = button.OnClick(click);

            return button;
        }

        /// <summary>
        /// Names a control that has no visible label - a tooltip a reader sees, plus the accessible
        /// name a screen reader reads.
        ///
        /// <b>A Tesserae tooltip rather than the native <c>title</c>.</b> The chrome used titles at
        /// first, on the grounds that Tippy is a lot of machinery for a row of twelve icon buttons.
        /// The panel toggles settled it: they are the one pair whose glyphs carry <i>state</i> - which
        /// pane is open - and "hold still for a second and the operating system may tell you" is not
        /// an answer to "which of these two is the outline". Once one button in a row has a real
        /// tooltip the rest need one too, or the row reads as broken.
        ///
        /// Not both: Tippy is given the text explicitly, so a <c>title</c> left in place would show
        /// the operating system's tooltip underneath Tesserae's. The accessible name goes on
        /// <c>aria-label</c> instead, which is where it belonged anyway.
        ///
        /// Tesserae attaches the tooltip on the first hover, so a toolbar that is never hovered costs
        /// one event handler per button and nothing else.
        /// </summary>
        internal static Button Tip(Button button, string tooltip)
            => button.AriaLabel(tooltip).Tooltip(tooltip, placement: TooltipPlacement.Bottom, delayShow: 350);

        /// <summary>
        /// A vertical hairline between groups of controls.
        ///
        /// The one piece of furniture with no Tesserae equivalent: <c>HorizontalSeparator</c> is a
        /// full-width rule with optional centred text, which is a different thing.
        /// </summary>
        internal static IComponent Separator() => Raw(Box("tsspdf-sep"));

        /// <summary>A div with a class and nothing else, for the few places that need one.</summary>
        internal static HTMLElement Box(string className)
        {
            var element = document.createElement("div").As<HTMLElement>();

            element.className = className;

            return element;
        }

        /// <summary>Adds or removes a class on a component's element, from a bool.</summary>
        internal static void Toggle(IComponent component, string className, bool on)
        {
            if (component is null) return;

            Toggle(component.Render(), className, on);
        }

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

        /// <summary>
        /// Shows or hides a component by display, not by the <c>hidden</c> attribute: Tesserae's own
        /// sheet sets <c>display</c> on most things, and <c>[hidden]</c> loses to a class rule where
        /// an inline <c>display:none</c> does not.
        /// </summary>
        internal static void Show(IComponent component, bool visible)
        {
            if (component is null) return;

            component.Render().style.display = visible ? "" : "none";
        }

        /// <summary>Mirrors a toggle's visual state into <c>aria-pressed</c>.</summary>
        internal static void SetPressed(IComponent component, bool pressed)
        {
            if (component is null) return;

            component.Render().setAttribute("aria-pressed", pressed ? "true" : "false");
        }

        /// <summary>
        /// The first descendant matching a selector, or null.
        ///
        /// <c>querySelector</c> is typed as a union of the element and null, which needs unwrapping at
        /// every call site; this does it once.
        /// </summary>
        internal static HTMLElement Find(HTMLElement root, string selector)
        {
            if (root is null) return null;

            return root.querySelector<HTMLElement>(selector).As<HTMLElement>();
        }

        /// <summary>The first descendant matching a selector, inside a component's element.</summary>
        internal static HTMLElement Find(IComponent component, string selector)
            => component is null ? null : Find(component.Render(), selector);

        /// <summary>Removes every child of an element.</summary>
        internal static void Empty(HTMLElement element)
        {
            if (element is null) return;

            while (element.firstChild is object)
            {
                element.removeChild(element.firstChild);
            }
        }
    }
}
