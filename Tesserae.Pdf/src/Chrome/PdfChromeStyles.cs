using static Transpose.Core.dom;

namespace Tesserae.Pdf
{
    /// <summary>
    /// The chrome's stylesheet, injected once into <c>&lt;head&gt;</c> the first time a
    /// <see cref="PdfViewerChrome"/> is built.
    ///
    /// <b>What is left in here after the controls became Tesserae's.</b> Three kinds of rule, and
    /// nothing else:
    /// <list type="bullet">
    /// <item><b>Sizing.</b> Tesserae's controls are sized for a form - a 36px button with a 4px
    /// margin, a 36px field - and this is a 40px toolbar. The components carry their own trims
    /// (<c>NoMargin</c>, <c>NoMinSize</c>, <c>NoPadding</c>), which do most of it; what is left is the
    /// handful of exact numbers the design calls for: 32px square buttons, a 28px field, a 24px
    /// segment.</item>
    /// <item><b>The pieces Tesserae has no component for</b> - the 1px group separator, the segmented
    /// track, the thumbnail tile, the search box's trailing controls.</item>
    /// <item><b>The chrome itself</b> - toolbar, rail, panel and body: surfaces and borders rather
    /// than controls.</item>
    /// </list>
    ///
    /// <b>The colours are Tesserae's.</b> Every surface, border and accent resolves to a
    /// <c>--tss-*</c> theme variable, so the chrome follows <c>UI.Theme.Dark()</c> and a host's
    /// <c>Theme.Build()</c> with no second palette to keep in step. The handful of values with no
    /// theme equivalent - the icon-button hover wash, the faint glyph grey, the segmented track - are
    /// declared as this sheet's own variables at the top, once, so a host can override them on
    /// <c>.tsspdf-chrome</c> without reaching into rules.
    ///
    /// <b>Rules that name a <c>tss-</c> class are a coupling</b>, and are marked as such below. They
    /// are the price of using the components rather than redrawing them, and they are all of the
    /// "make it 4px shorter" kind - none of them changes how a control behaves, so the worst a
    /// Tesserae change can do is put a control back at its default size.
    /// </summary>
    internal static class PdfChromeStyles
    {
        internal const string ROOT       = "tsspdf-chrome";
        internal const string BORDERED   = "tsspdf-bordered";
        internal const string ON         = "tsspdf-on";
        internal const string OPEN       = "tsspdf-open";
        internal const string NO_MATCHES = "tsspdf-nomatches";
        internal const string SECTION    = "tsspdf-section";
        internal const string CURRENT    = "tsspdf-current";

        private const string STYLE_ELEMENT_ID = "tsspdf-chrome-styles";

        private static bool _injected;

        /// <summary>
        /// Adds the sheet to the document, at most once per page.
        ///
        /// Also checks for the element by id rather than trusting the flag alone: two copies of this
        /// assembly on one page (a host and a library that both reference the package) each have their
        /// own static, and the second would otherwise add a duplicate sheet.
        /// </summary>
        internal static void Ensure()
        {
            if (_injected) return;

            _injected = true;

            if (document.getElementById(STYLE_ELEMENT_ID) is object) return;

            var style = document.createElement("style");

            style.id = STYLE_ELEMENT_ID;

            style.appendChild(document.createTextNode(CSS));

            // Appended rather than prepended: these rules are meant to win against Tesserae's own
            // sheet on the selectors they share, and a later sheet wins a tie.
            document.head.appendChild(style);
        }

        private const string CSS = @"
.tsspdf-chrome{
  --tsspdf-surface:var(--tss-default-background-color);
  --tsspdf-canvas:var(--tss-secondary-background-color);
  --tsspdf-border:var(--tss-default-border-color);
  --tsspdf-separator:var(--tss-default-separator-color);
  --tsspdf-fg:var(--tss-default-foreground-color);
  --tsspdf-fg-strong:var(--tss-default-foreground-hover-color);
  --tsspdf-fg-muted:var(--tss-secondary-foreground-color);
  --tsspdf-fg-faint:rgb(140,143,151);
  --tsspdf-accent:var(--tss-link-color);
  --tsspdf-accent-soft:rgba(var(--tss-link-color-root),.10);
  --tsspdf-hover:rgb(240,241,242);
  --tsspdf-track:rgb(240,241,242);
  --tsspdf-danger:var(--tss-danger-border-color);
  --tsspdf-shadow-sm:0 1px 2px 0 rgba(0,0,0,.05);
  --tsspdf-shadow-lift:0 4px 6px -1px rgba(0,0,0,.1);
  --tsspdf-panel-width:264px;
  --tsspdf-search-width:430px;
  --tsspdf-radius:6px;
  display:flex;flex-direction:column;width:100%;height:100%;position:relative;overflow:hidden;
  background:var(--tsspdf-canvas);color:var(--tsspdf-fg);
}
.tsspdf-chrome,.tsspdf-chrome *,.tsspdf-chrome *::before,.tsspdf-chrome *::after{box-sizing:border-box}

/* The optional frame - PdfViewerChrome.Border(). Off by default, because the common case has an edge
   already: a chrome filling a window, a modal or a Tesserae Card is inside something that draws its
   own line, and a second one just inside it is a seam. It earns its keep when the chrome sits on a
   page's own background with nothing to say where the reader's document ends.

   No rule for the corners of what is inside it: the root already clips (overflow:hidden), so the
   toolbar's square top corners and the panel's bottom-left one are rounded by the radius here rather
   than by a rule each. */
.tsspdf-chrome.tsspdf-bordered{border:1px solid var(--tsspdf-border);
  border-radius:var(--tsspdf-radius)}

.tss-dark-mode .tsspdf-chrome{
  --tsspdf-accent-soft:rgba(var(--tss-link-color-root),.16);
  --tsspdf-hover:rgba(255,255,255,.10);
  --tsspdf-track:rgba(255,255,255,.06);
  --tsspdf-fg-faint:rgb(120,128,144);
  --tsspdf-shadow-sm:none;
  --tsspdf-shadow-lift:0 4px 10px -2px rgba(0,0,0,.55);
}

/* ---------------------------------------------------------------- toolbar */

/* overflow-x:auto, not the hidden the chrome uses elsewhere: a toolbar narrower than its controls
   has to give the reader a way to reach the ones past the edge, and every control in here is the
   only way to do the thing it does. The scrollbar is suppressed because a 40px bar has no room for
   one - PdfChromeLayout.IconRail is the answer for a container this narrow, and this is what keeps
   the other layout usable rather than quietly clipped. */
.tsspdf-toolbar{height:40px;padding:0 8px;flex-shrink:0;
  background:var(--tsspdf-surface);border-bottom:1px solid var(--tsspdf-border);
  overflow-x:auto;scrollbar-width:none;-ms-overflow-style:none}
.tsspdf-toolbar::-webkit-scrollbar{display:none}
.tsspdf-sep{width:1px;height:20px;background:var(--tsspdf-border);flex-shrink:0;margin:0 6px}
.tsspdf-toolbar-split .tsspdf-sep{margin:0}
.tsspdf-doctitle-text{font-size:13px;font-weight:600;white-space:nowrap;overflow:hidden;
  text-overflow:ellipsis;min-width:0}

/* -- coupling: Tesserae sizes a button for a form. These are the design's exact numbers. */
.tsspdf-chrome .tsspdf-iconbtn.tss-btn{width:32px;height:32px;min-width:32px;min-height:32px;
  padding:0;border:0;background:transparent;box-shadow:none;border-radius:6px;
  color:var(--tsspdf-fg-muted);flex-shrink:0;justify-content:center}
.tsspdf-chrome .tsspdf-iconbtn.tss-btn:hover:not(:disabled){background:var(--tsspdf-hover);
  color:var(--tsspdf-fg-strong)}
.tsspdf-chrome .tsspdf-iconbtn.tss-btn.tsspdf-on{background:var(--tsspdf-accent-soft);
  color:var(--tsspdf-accent)}
.tsspdf-chrome .tsspdf-iconbtn.tss-btn i{font-size:16px;line-height:1}

/* The zoom value and its chevron: a field-shaped button rather than an icon-shaped one. */
.tsspdf-chrome .tsspdf-zoom.tss-btn{height:28px;min-height:28px;min-width:70px;padding:0 8px;
  border-radius:6px;font-size:12px;color:var(--tsspdf-fg);background:var(--tsspdf-surface);
  border:1px solid var(--tsspdf-border);box-shadow:var(--tsspdf-shadow-sm);flex-shrink:0;gap:6px}
.tsspdf-chrome .tsspdf-zoom.tss-btn:hover{background:var(--tsspdf-hover)}
.tsspdf-chrome .tsspdf-zoom.tss-btn{flex-direction:row-reverse;justify-content:flex-end}
.tsspdf-chrome .tsspdf-zoom.tss-btn i{font-size:12px;color:var(--tsspdf-fg-faint)}
/* A fixed width for the value, so 96% and 140% do not move the buttons either side of it. */
.tsspdf-chrome .tsspdf-zoom.tss-btn span{min-width:34px;text-align:left}

/* -- coupling: the page box. Tesserae's text field is 36px tall and padded for a form. */
.tsspdf-chrome .tsspdf-pagebox.tss-textbox-container{width:38px;min-width:38px;flex-shrink:0}
.tsspdf-chrome .tsspdf-pagebox .tss-textbox{height:28px;padding:0 2px;font-size:12px;
  text-align:center;border-radius:6px;box-shadow:var(--tsspdf-shadow-sm)}
.tsspdf-chrome .tsspdf-pagebox .tss-textbox-error{display:none}
/* A reserved width, because this text changes as the reader moves through the document - of 9 to
   of 10, 1 of 12 to 12 of 12 - and everything after it would move with it. */
.tsspdf-pagetotal{font-size:12px;color:var(--tsspdf-fg-muted);white-space:nowrap;flex-shrink:0;
  min-width:52px}
.tsspdf-chrome.tsspdf-labelled .tsspdf-pagetotal{min-width:64px}

/* --------------------------------------------------------- segmented control */

/* One pill is left - Fuzzy | Precise, in the search row. No Tesserae component for it:
   SegmentedPivot is a scrollable tab strip that also hosts a content pane, and PivotSelector
   collapses to a dropdown. The track is three declarations and the two things inside it are
   ordinary Tesserae buttons. */
.tsspdf-seg{background:var(--tsspdf-track);border-radius:6px;padding:2px;flex-shrink:0}
.tsspdf-chrome .tsspdf-seg-item.tss-btn{height:24px;min-height:24px;padding:0 9px;border-radius:4px;
  font-size:12px;font-weight:600;color:var(--tsspdf-fg-muted);background:transparent;gap:6px}
.tsspdf-chrome .tsspdf-seg-item.tss-btn:hover{color:var(--tsspdf-fg-strong);background:transparent}
.tsspdf-chrome .tsspdf-seg-item.tss-btn.tsspdf-on{background:var(--tsspdf-surface);
  color:var(--tsspdf-accent);box-shadow:var(--tsspdf-shadow-sm)}
.tsspdf-chrome .tsspdf-seg-item.tss-btn i{font-size:14px}
.tss-dark-mode .tsspdf-chrome .tsspdf-seg-item.tss-btn.tsspdf-on{background:rgba(255,255,255,.14);
  box-shadow:none}
.tsspdf-seg-sm{margin:0 3px 0 2px}
.tsspdf-chrome .tsspdf-seg-sm .tsspdf-seg-item.tss-btn{height:22px;padding:0 8px;font-size:11px}

/* ---------------------------------------------------------------- search */

/* -- coupling: Tesserae's search box is 36px tall. Everything else about it - the magnifier, the
   keyboard-shortcut chip, search-as-you-type, the invalid state - is the component's own. */
.tsspdf-chrome .tsspdf-search.tss-searchbox-container{height:28px;
  width:var(--tsspdf-search-width);min-width:310px;flex-shrink:1}
.tsspdf-chrome .tsspdf-search .tss-searchbox{height:28px;font-size:13px}
.tsspdf-chrome .tsspdf-search.tss-searchbox-container.tsspdf-nomatches{border-color:var(--tsspdf-danger);
  box-shadow:0 0 0 2px rgba(var(--tss-danger-border-color-root),.14)}
.tsspdf-chrome .tsspdf-search.tsspdf-nomatches .tss-searchbox-icon{color:var(--tsspdf-danger)}

.tsspdf-searchrow{flex-shrink:1;min-width:0}
/* Reserved and right-aligned: 1 / 3 becoming 10 / 30 must not move the buttons beside it. */
.tsspdf-count{padding:0 6px;white-space:nowrap;font-size:11px;color:var(--tsspdf-fg-muted);
  font-family:var(--tss-monospace-font-family);flex-shrink:0;min-width:58px;text-align:right}
.tsspdf-note{padding:0 6px;white-space:nowrap;font-size:11px;color:var(--tsspdf-danger);flex-shrink:0}
.tsspdf-chrome .tsspdf-step.tss-btn{width:22px;height:22px;min-width:22px;min-height:22px;
  padding:0;border:0;background:transparent;box-shadow:none;border-radius:4px;
  color:var(--tsspdf-fg-muted);flex-shrink:0;justify-content:center}
.tsspdf-chrome .tsspdf-step.tss-btn:hover:not(:disabled){background:var(--tsspdf-hover)}
.tsspdf-chrome .tsspdf-step.tss-btn i{font-size:13px}

/* ------------------------------------------------------- responsive + mobile */

/* The bands are measured on the chrome's own box and published as a class by ApplyWidthClass, not
   taken from a media query: the same page can hold one of these full-width and another in a 360px
   pane. Nothing disappears without somewhere else to reach it - what leaves the toolbar arrives in
   the overflow menu, and the fit modes are in the zoom menu at every width.

   There is no narrow band rule any more: what it used to do was strip the labels off the fit pill,
   and the fit pill is gone from the toolbar. The class is still published, because the next control
   that wants a first step out has somewhere to put it. */

/* tight: the document's name leaves the toolbar, and the search box gives up its shortcut chip -
   which is a hint, not a control. */
.tsspdf-tight .tsspdf-doctitle,
.tsspdf-mini  .tsspdf-doctitle{display:none}
.tsspdf-chrome.tsspdf-tight .tsspdf-search.tss-searchbox-container,
.tsspdf-chrome.tsspdf-mini  .tsspdf-search.tss-searchbox-container{min-width:210px}
/* Two classes plus a type, because Tesserae's own rule for the chip
   (.tss-searchbox-container.tss-searchbox-has-shortcut > .tss-searchbox-shortcut) scores two
   classes plus one and would otherwise win on order. A one-class rule here loses silently. */
.tsspdf-chrome.tsspdf-tight .tsspdf-search .tss-searchbox-shortcut,
.tsspdf-chrome.tsspdf-mini  .tsspdf-search .tss-searchbox-shortcut{display:none}

/* tight: the Fuzzy | Precise pill is 108px of a row that no longer has it, and the mode moves to the
   overflow menu with everything else that left. */
.tsspdf-tight .tsspdf-seg-sm,
.tsspdf-mini  .tsspdf-seg-sm{display:none}

/* mini: the search box takes the second line.
   A phone leaves about 330px for the toolbar, the controls that are left take 270 of it, and the
   search box has a floor of 210 - so on one line it does not fit at any floor a reader could type
   in, and the toolbar's horizontal scroll (the last resort, below) put the field off-screen at rest
   on the one band whose whole point is that search is what a reader on a phone uses. So the row
   wraps instead: controls above, search across the full width beneath, nothing scrolling and
   nothing hidden. Which is what every reader on a phone does. */
.tsspdf-chrome.tsspdf-mini .tsspdf-toolbar{height:auto;min-height:40px;flex-wrap:wrap;
  padding-bottom:6px;align-content:flex-start}
.tsspdf-chrome.tsspdf-mini .tsspdf-searchrow{flex-basis:100%;min-width:0;order:9}
.tsspdf-chrome.tsspdf-mini .tsspdf-search.tss-searchbox-container{width:100%;min-width:0}

/* mini: rotate, spread and the whole zoom stepper leave the toolbar, and the page total goes with
   them - the box still says which page, and its tooltip still says of how many. What is left is the
   panel toggle, the page controls and search, which is what a reader on a phone actually uses. */
.tsspdf-mini .tsspdf-rotate,
.tsspdf-mini .tsspdf-spread,
.tsspdf-mini .tsspdf-zoomgroup,
.tsspdf-mini .tsspdf-pagetotal{display:none}
.tsspdf-chrome.tsspdf-mini .tsspdf-count{min-width:0}

/* mini: the panel stops taking width from a document that has none to give, and covers it instead.
   Positioned against the body, which is why the body is relative. */
.tsspdf-body{position:relative}
.tsspdf-chrome.tsspdf-mini .tsspdf-panel{position:absolute;top:0;left:0;bottom:0;z-index:5;
  width:min(320px,86%);min-width:0;box-shadow:0 8px 24px -6px rgba(0,0,0,.28)}

/* Touch: the design's 32px squares are a mouse target. A finger wants 40, and the toolbar grows to
   suit rather than the buttons overlapping. */
@media (pointer:coarse){
  .tsspdf-toolbar{height:48px}
  .tsspdf-chrome.tsspdf-mini .tsspdf-toolbar{height:auto;min-height:48px}
  .tsspdf-chrome .tsspdf-iconbtn.tss-btn{width:40px;height:40px;min-width:40px;min-height:40px}
  .tsspdf-chrome .tsspdf-iconbtn.tss-btn i{font-size:18px}
  .tsspdf-rail{width:56px}
  .tsspdf-chrome .tsspdf-step.tss-btn{width:28px;height:28px;min-width:28px;min-height:28px}
  .tsspdf-chrome .tsspdf-pagebox .tss-textbox,
  .tsspdf-chrome .tsspdf-zoom.tss-btn,
  .tsspdf-chrome .tsspdf-search.tss-searchbox-container,
  .tsspdf-chrome .tsspdf-search .tss-searchbox{height:34px}
  .tsspdf-chrome .tsspdf-seg-item.tss-btn{height:30px}
  .tsspdf-seg{padding:3px}
  .tsspdf-chrome .tsspdf-outline .tss-tree-item-content{padding:9px 8px}
}

/* ------------------------------------------------------------- body / rail */

.tsspdf-body{display:flex;flex:1;min-height:0}
.tsspdf-view{position:relative;flex:1;min-width:0;min-height:0;background:var(--tsspdf-canvas)}
.tsspdf-rail{width:48px;padding:6px 0;flex-shrink:0;background:var(--tsspdf-canvas);
  border-right:1px solid var(--tsspdf-border);overflow:hidden}
.tsspdf-rail-sep{width:24px;height:1px;margin:5px 0;background:var(--tsspdf-border);flex-shrink:0}
.tsspdf-rail-zoom{padding:1px 0;font-size:10px;color:var(--tsspdf-fg-muted);
  font-family:var(--tss-monospace-font-family);min-width:34px;text-align:center}

/* ------------------------------------------------------------------ panel */

.tsspdf-chrome .tsspdf-panel{width:var(--tsspdf-panel-width);min-width:var(--tsspdf-panel-width);
  flex-shrink:0;background:var(--tsspdf-surface);border-right:1px solid var(--tsspdf-border)}

/* The one scroller, holding whichever pane the toolbar's toggles chose. There is no tab strip: the
   toggles are the switch, and a strip repeating them was one control too many. */
.tsspdf-chrome .tsspdf-panel-body{flex:1;min-height:0;overflow:auto;padding:0}

.tsspdf-panel-foot{padding:8px 12px;flex-shrink:0;border-top:1px solid var(--tsspdf-separator);
  font-size:11px;color:var(--tsspdf-fg-muted)}
.tsspdf-panel-count{font-family:var(--tss-monospace-font-family);font-size:11px;
  color:var(--tsspdf-fg-muted)}
.tsspdf-chrome .tsspdf-panel-action.tss-btn{min-height:0;padding:0;border:0;background:none;
  box-shadow:none;font-size:11px;color:var(--tsspdf-accent)}
.tsspdf-chrome .tsspdf-panel-action.tss-btn:hover{text-decoration:underline;background:none}
.tsspdf-panel-empty{padding:16px;font-size:12px;color:var(--tsspdf-fg-muted)}

/* ---------------------------------------------------------------- outline */

/* -- coupling: a Tesserae Tree, at the design's density. The checkbox column is hidden rather than
   configured away because selection is what the tree is for here and the box is not. */
.tsspdf-chrome .tsspdf-outline .tss-tree-checkbox{display:none}
.tsspdf-chrome .tsspdf-outline{padding:8px}
.tsspdf-chrome .tsspdf-outline .tss-tree-item-content{min-height:0;padding:5px 8px;border-radius:4px;
  border-left:3px solid transparent;margin-left:-3px;gap:6px;font-size:13px;line-height:1.35}
/* An icon font brings its own line box, which is what made these rows 32px rather than the design's
   27.5 - the glyph is 11px but the line it sits on was not. */
.tsspdf-chrome .tsspdf-outline .tss-tree-chevron{font-size:11px;width:14px;height:16px;flex-shrink:0;
  line-height:16px}
.tsspdf-chrome .tsspdf-outline .tss-tree-commands{margin-left:auto;flex-shrink:0;line-height:16px}
.tsspdf-chrome .tsspdf-outline .tss-tree-item-content:hover{background:var(--tsspdf-hover)}
/* No line-height here on purpose. Tesserae sets 22px on this element, which makes an outline row
   32px rather than the design's 27.5 - and that is the component's density, the same as every other
   tree in the host application. Overriding it would make this one panel the odd one out, which is the
   opposite of why the tree is a Tesserae Tree. */
.tsspdf-chrome .tsspdf-outline .tss-tree-text{flex:1;min-width:0;overflow:hidden;
  text-overflow:ellipsis;white-space:nowrap}
.tsspdf-chrome .tsspdf-outline .tsspdf-section > .tss-tree-item-content{background:var(--tss-default-background-active-color);
  border-left-color:var(--tsspdf-accent);font-weight:600}
.tsspdf-chrome .tsspdf-outline .tsspdf-current > .tss-tree-item-content .tss-tree-text{
  color:var(--tsspdf-accent);font-weight:600}
.tsspdf-outline-page{font-family:var(--tss-monospace-font-family);font-size:11px;
  color:var(--tsspdf-fg-faint);flex-shrink:0}

/* ------------------------------------------------------------- thumbnails */

.tsspdf-thumbs{padding:12px}
.tsspdf-chrome .tsspdf-thumb.tss-btn{flex-direction:column;gap:4px;width:100%;height:auto;
  min-height:0;padding:0;background:none;border:0;box-shadow:none}
.tsspdf-thumb-frame{position:relative;display:flex;width:100%;min-height:72px;overflow:hidden;
  background:#fff;border:1px solid var(--tsspdf-border);box-shadow:var(--tsspdf-shadow-sm)}
.tsspdf-thumb.tsspdf-on .tsspdf-thumb-frame{border:2px solid var(--tsspdf-accent);
  box-shadow:var(--tsspdf-shadow-lift)}
.tsspdf-thumb-num{font-size:10px;color:var(--tsspdf-fg-muted);
  font-family:var(--tss-monospace-font-family)}
.tsspdf-thumb.tsspdf-on .tsspdf-thumb-num{color:var(--tsspdf-accent);font-weight:600}
/* z-index, because the page that lands in this frame arrives inside a positioned container of its
   own (PdfComponent makes it relative for pdf.js's sake) and is a later sibling - so with both at
   z-index auto the canvas paints over the dot, and the mark saying a page has a match on it is
   invisible on exactly the tiles that have one. */
.tsspdf-thumb-match{position:absolute;top:3px;right:3px;z-index:1;width:7px;height:7px;
  border-radius:50%;background:var(--tsspdf-accent);box-shadow:0 0 0 1.5px #fff}
";
    }
}
