using static Transpose.Core.dom;

namespace Tesserae.Pdf
{
    /// <summary>
    /// The chrome's stylesheet, injected once into <c>&lt;head&gt;</c> the first time a
    /// <see cref="PdfViewerChrome"/> is built.
    ///
    /// <b>Why a stylesheet and not inline styles.</b> Every control in this chrome has a hover, a
    /// focus and a disabled appearance, and three of them have a "selected" one. Inline styles cannot
    /// express any of that without a mousemove handler per element, so the whole thing is one sheet
    /// of rules against <c>tsspdf-</c> classes and the elements carry classes rather than styles.
    ///
    /// <b>Why it is not a Transpose resource.</b> Transpose emits a <c>&lt;link&gt;</c> for a CSS
    /// resource, which would fetch a second file for a component most pages never mount - and
    /// <c>tps.json</c>'s <c>files</c> globs plus <c>"outputFormatting": "Both"</c> make a stylesheet
    /// awkward to declare (see the note there). It is a few kilobytes of text; inlining it keeps the
    /// package's asset story to "pdf.js and nothing else".
    ///
    /// <b>The colours are Tesserae's.</b> Every surface, border and accent below resolves to a
    /// <c>--tss-*</c> theme variable, so the chrome follows <c>UI.Theme.Dark()</c> and a host's
    /// <c>Theme.Build()</c> with no work and no second palette to keep in step. The handful of values
    /// with no theme equivalent - the icon-button hover wash, the faint glyph grey, the segmented
    /// track - are declared as this sheet's own variables at the top, once, so a host can override
    /// them on <c>.tsspdf-chrome</c> without reaching into rules.
    /// </summary>
    internal static class PdfChromeStyles
    {
        internal const string ROOT       = "tsspdf-chrome";
        internal const string ON         = "tsspdf-on";
        internal const string OPEN       = "tsspdf-open";
        internal const string FOCUS      = "tsspdf-focus";
        internal const string NO_MATCHES = "tsspdf-nomatches";
        internal const string SECTION    = "tsspdf-section";

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
            // sheet on the rare selector they share, and a later sheet wins a tie.
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
  --tsspdf-fg-disabled:var(--tss-disabled-foreground-color);
  --tsspdf-accent:var(--tss-link-color);
  --tsspdf-accent-soft:rgba(var(--tss-link-color-root),.10);
  --tsspdf-accent-ring:rgba(var(--tss-link-color-root),.18);
  --tsspdf-hover:rgb(240,241,242);
  --tsspdf-pressed:var(--tss-default-background-active-color);
  --tsspdf-track:rgb(240,241,242);
  --tsspdf-danger:var(--tss-danger-border-color);
  --tsspdf-danger-ring:rgba(var(--tss-danger-border-color-root),.14);
  --tsspdf-shadow-sm:0 1px 2px 0 rgba(0,0,0,.05);
  --tsspdf-shadow-md:0 4px 6px -1px rgba(0,0,0,.1),0 2px 4px -1px rgba(0,0,0,.06);
  --tsspdf-shadow-lift:0 4px 6px -1px rgba(0,0,0,.1);
  --tsspdf-panel-width:264px;
  --tsspdf-search-width:430px;
  display:flex;flex-direction:column;width:100%;height:100%;position:relative;overflow:hidden;
  background:var(--tsspdf-canvas);color:var(--tsspdf-fg);
  font-family:var(--tss-sansserif-font-family);font-size:13px;line-height:1.35;
}
.tsspdf-chrome,.tsspdf-chrome *,.tsspdf-chrome *::before,.tsspdf-chrome *::after{box-sizing:border-box}

/* Form controls do not inherit typography, so this is the reset that stops every button in the
   chrome coming out in the browser's 13.333px system font. Written through :where() on purpose:
   that contributes no specificity, so `.tsspdf-chrome :where(button)` scores zero and every rule
   below - each of which is a single class - wins against it. Written the obvious way, the reset
   scores one class plus one type and silently beats them all, which shows up as a toolbar whose
   labels are one pixel too big and one shade too dark. */
:where(.tsspdf-chrome) :where(button,input,select,textarea)
  {font:inherit;line-height:inherit;color:inherit;letter-spacing:inherit}

.tss-dark-mode .tsspdf-chrome{
  --tsspdf-accent-soft:rgba(var(--tss-link-color-root),.16);
  --tsspdf-hover:rgba(255,255,255,.10);
  --tsspdf-track:rgba(255,255,255,.06);
  --tsspdf-fg-faint:rgb(120,128,144);
  --tsspdf-shadow-sm:none;
  --tsspdf-shadow-md:0 4px 10px -2px rgba(0,0,0,.55);
  --tsspdf-shadow-lift:0 4px 10px -2px rgba(0,0,0,.55);
}

/* ---------------------------------------------------------------- toolbar */

/* overflow-x:auto, not the hidden the chrome uses elsewhere: a toolbar narrower than its controls
   has to give the reader a way to reach the ones past the edge, and every control in here is the
   only way to do the thing it does. The scrollbar is suppressed because a 40px bar has no room for
   one - PdfChromeLayout.IconRail is the answer for a container this narrow, and this is what keeps
   the other layout usable rather than quietly clipped. */
.tsspdf-toolbar{display:flex;align-items:center;gap:2px;height:40px;padding:0 8px;
  background:var(--tsspdf-surface);border-bottom:1px solid var(--tsspdf-border);flex-shrink:0;
  overflow-x:auto;scrollbar-width:none;-ms-overflow-style:none}
.tsspdf-toolbar::-webkit-scrollbar{display:none}
.tsspdf-toolbar-split{gap:8px;padding:0 10px 0 8px}
.tsspdf-sep{width:1px;height:20px;background:var(--tsspdf-border);margin:0 6px;flex-shrink:0}
.tsspdf-toolbar-split .tsspdf-sep{margin:0}
.tsspdf-spring{flex:1;min-width:16px}
.tsspdf-group{display:flex;align-items:center;gap:2px;flex-shrink:0}

.tsspdf-doctitle{display:flex;align-items:center;gap:8px;min-width:0;flex-shrink:1;overflow:hidden}
.tsspdf-doctitle-text{font-size:13px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}

/* ------------------------------------------------------------- icon button */

.tsspdf-iconbtn{display:inline-flex;align-items:center;justify-content:center;width:32px;height:32px;
  border-radius:6px;border:0;padding:0;background:transparent;color:var(--tsspdf-fg-muted);
  cursor:pointer;flex-shrink:0}
.tsspdf-iconbtn:hover:not(:disabled){background:var(--tsspdf-hover);color:var(--tsspdf-fg-strong)}
.tsspdf-iconbtn:active:not(:disabled){background:var(--tsspdf-pressed);color:var(--tsspdf-fg-strong);
  box-shadow:inset 0 2px 4px 0 rgba(0,0,0,.06)}
.tsspdf-iconbtn.tsspdf-on{background:var(--tsspdf-accent-soft);color:var(--tsspdf-accent)}
.tsspdf-iconbtn:disabled{color:var(--tsspdf-fg-disabled);cursor:not-allowed}
.tsspdf-iconbtn:focus-visible{outline:none;box-shadow:0 0 0 2px var(--tsspdf-accent-ring)}
.tsspdf-chrome svg{display:block;pointer-events:none}

/* ------------------------------------------------------------- page number */

.tsspdf-pagebox{width:38px;height:28px;padding:0;flex-shrink:0;text-align:center;font-size:12px;
  border:1px solid var(--tsspdf-border);border-radius:6px;background:var(--tsspdf-surface);
  color:var(--tsspdf-fg);box-shadow:var(--tsspdf-shadow-sm)}
.tsspdf-pagebox:focus{outline:none;border-color:var(--tsspdf-accent);
  box-shadow:0 0 0 2px var(--tsspdf-accent-ring)}
.tsspdf-pagebox:disabled{color:var(--tsspdf-fg-disabled)}
.tsspdf-pagetotal{font-size:12px;color:var(--tsspdf-fg-muted);white-space:nowrap}

/* ------------------------------------------------------- field-shaped button */

.tsspdf-field{display:inline-flex;align-items:center;gap:6px;height:28px;padding:0 8px;flex-shrink:0;
  border-radius:6px;border:1px solid var(--tsspdf-border);background:var(--tsspdf-surface);
  color:var(--tsspdf-fg);font-size:12px;cursor:pointer;box-shadow:var(--tsspdf-shadow-sm)}
.tsspdf-field:hover{background:var(--tsspdf-hover)}
.tsspdf-field.tsspdf-open,.tsspdf-field:focus-visible{outline:none;border-color:var(--tsspdf-accent);
  box-shadow:0 0 0 2px var(--tsspdf-accent-ring)}
.tsspdf-field-value{min-width:34px;text-align:left}

/* --------------------------------------------------------- segmented control */

.tsspdf-seg{display:inline-flex;background:var(--tsspdf-track);border-radius:6px;padding:2px;gap:2px;
  flex-shrink:0}
.tsspdf-seg-item{display:inline-flex;align-items:center;gap:6px;height:24px;padding:0 9px;border:0;
  border-radius:4px;background:transparent;color:var(--tsspdf-fg-muted);font-size:12px;
  font-weight:600;cursor:pointer;white-space:nowrap}
.tsspdf-seg-item:hover{color:var(--tsspdf-fg-strong)}
.tsspdf-seg-item.tsspdf-on{background:var(--tsspdf-surface);color:var(--tsspdf-accent);
  box-shadow:var(--tsspdf-shadow-sm)}
.tsspdf-seg-item:focus-visible{outline:none;box-shadow:0 0 0 2px var(--tsspdf-accent-ring)}
.tss-dark-mode .tsspdf-chrome .tsspdf-seg-item.tsspdf-on{background:rgba(255,255,255,.14);box-shadow:none}
.tsspdf-seg-sm{margin:0 3px 0 2px}
.tsspdf-seg-sm .tsspdf-seg-item{height:22px;padding:0 8px;font-size:11px}

/* ---------------------------------------------------------------- omnibox */

/* The search box is the one control in the toolbar allowed to shrink, and the min-width is where it
   stops. Not a round number: everything beside the field is fixed-width - the magnifier, the count,
   the two match steppers, the clear button and the Fuzzy|Precise pill come to about 250px - so a
   smaller floor does not make the box smaller, it makes the field inside it zero wide. A search box
   with nowhere to type is worse than a toolbar that scrolls, which is what happens instead. */
.tsspdf-omni{display:flex;align-items:center;height:28px;width:var(--tsspdf-search-width);
  min-width:310px;flex-shrink:1;overflow:hidden;border-radius:6px;
  border:1px solid var(--tsspdf-border);background:var(--tsspdf-surface);
  box-shadow:var(--tsspdf-shadow-sm)}
.tsspdf-omni.tsspdf-focus{border-color:var(--tsspdf-accent);box-shadow:0 0 0 2px var(--tsspdf-accent-ring)}
.tsspdf-omni.tsspdf-nomatches{border-color:var(--tsspdf-danger);box-shadow:0 0 0 2px var(--tsspdf-danger-ring)}
.tsspdf-omni-icon{display:inline-flex;align-items:center;justify-content:center;width:28px;
  flex-shrink:0;color:var(--tsspdf-fg-faint)}
.tsspdf-omni.tsspdf-focus .tsspdf-omni-icon{color:var(--tsspdf-accent)}
.tsspdf-omni.tsspdf-nomatches .tsspdf-omni-icon{color:var(--tsspdf-danger)}
.tsspdf-omni-input{flex:1;min-width:56px;height:100%;padding:0;border:0;background:transparent;
  color:var(--tsspdf-fg);font-size:13px}
.tsspdf-omni-input:focus{outline:none}
.tsspdf-omni-input::placeholder{color:var(--tsspdf-fg-faint)}
.tsspdf-omni-count{padding:0 6px;white-space:nowrap;font-size:11px;color:var(--tsspdf-fg-muted);
  font-family:var(--tss-monospace-font-family)}
.tsspdf-omni-hint{padding:0 8px;white-space:nowrap;font-size:10px;color:var(--tsspdf-fg-faint);
  font-family:var(--tss-monospace-font-family)}
.tsspdf-omni-note{padding:0 8px;white-space:nowrap;font-size:11px;color:var(--tsspdf-danger)}
.tsspdf-omni-step{display:inline-flex;align-items:center;justify-content:center;width:22px;height:22px;
  padding:0;flex-shrink:0;border:0;border-radius:4px;background:transparent;
  color:var(--tsspdf-fg-muted);cursor:pointer}
.tsspdf-omni-step:hover:not(:disabled){background:var(--tsspdf-hover)}
.tsspdf-omni-step:disabled{color:var(--tsspdf-fg-disabled);cursor:not-allowed}
.tsspdf-omni-clear{display:inline-flex;align-items:center;justify-content:center;width:24px;height:26px;
  padding:0;flex-shrink:0;margin-left:4px;border:0;border-left:1px solid var(--tsspdf-border);
  background:transparent;color:var(--tsspdf-fg-faint);cursor:pointer}
.tsspdf-omni-clear:hover{color:var(--tsspdf-danger)}
.tsspdf-omni-help{display:flex;align-items:center;gap:6px;padding:4px 0 0 2px;font-size:11px;
  color:var(--tsspdf-fg-muted)}

/* -------------------------------------------------------------- popup menu */

.tsspdf-menu{position:absolute;z-index:30;min-width:150px;padding:4px;
  background:var(--tsspdf-surface);border:1px solid var(--tsspdf-border);border-radius:6px;
  box-shadow:var(--tsspdf-shadow-md)}
.tsspdf-menu-item{display:flex;align-items:center;gap:8px;width:100%;padding:6px 8px;border:0;
  border-radius:4px;background:transparent;color:var(--tsspdf-fg);font-size:13px;text-align:left;
  cursor:pointer;white-space:nowrap}
.tsspdf-menu-item:hover{background:var(--tsspdf-hover)}
.tsspdf-menu-item.tsspdf-on{background:var(--tsspdf-accent-soft);color:var(--tsspdf-accent);font-weight:600}
.tsspdf-menu-item:focus-visible{outline:none;box-shadow:0 0 0 2px var(--tsspdf-accent-ring)}
.tsspdf-menu-check{display:inline-flex;width:13px;flex-shrink:0}
.tsspdf-menu-item:not(.tsspdf-on) .tsspdf-menu-check{visibility:hidden}
.tsspdf-menu-sep{height:1px;margin:4px 6px;background:var(--tsspdf-separator)}

/* ------------------------------------------------------------- body / rail */

.tsspdf-body{display:flex;flex:1;min-height:0}
.tsspdf-view{position:relative;flex:1;min-width:0;min-height:0;background:var(--tsspdf-canvas)}

.tsspdf-rail{display:flex;flex-direction:column;align-items:center;gap:2px;width:48px;padding:6px 0;
  flex-shrink:0;background:var(--tsspdf-canvas);border-right:1px solid var(--tsspdf-border);
  overflow:hidden}
.tsspdf-rail-sep{width:24px;height:1px;margin:5px 0;background:var(--tsspdf-border);flex-shrink:0}
.tsspdf-rail-zoom{padding:1px 0;font-size:10px;color:var(--tsspdf-fg-muted);
  font-family:var(--tss-monospace-font-family)}

/* ------------------------------------------------------------------ panel */

.tsspdf-panel{display:flex;flex-direction:column;width:var(--tsspdf-panel-width);min-width:0;
  flex-shrink:0;background:var(--tsspdf-surface);border-right:1px solid var(--tsspdf-border)}
.tsspdf-panel-tabs{display:flex;gap:18px;padding:0 16px;flex-shrink:0;
  border-bottom:1px solid var(--tsspdf-border)}
.tsspdf-tab{padding:10px 0 8px;border:0;border-bottom:2px solid transparent;background:none;
  font-size:13px;font-weight:400;color:var(--tsspdf-fg-muted);cursor:pointer}
.tsspdf-tab:hover{color:var(--tsspdf-fg-strong)}
.tsspdf-tab.tsspdf-on{font-weight:600;color:var(--tsspdf-accent);border-bottom-color:var(--tsspdf-accent)}
.tsspdf-tab:focus-visible{outline:none;box-shadow:0 0 0 2px var(--tsspdf-accent-ring)}
/* Positioned so a row's offsetTop is measured against the scroller, which is what
   scrolling the current page or outline entry into view reads. */
.tsspdf-panel-body{position:relative;flex:1;min-height:0;overflow:auto}
.tsspdf-panel-foot{display:flex;align-items:center;gap:8px;padding:8px 12px;flex-shrink:0;
  border-top:1px solid var(--tsspdf-separator);font-size:11px;color:var(--tsspdf-fg-muted)}
.tsspdf-panel-count{font-family:var(--tss-monospace-font-family)}
.tsspdf-panel-action{margin-left:auto;padding:0;border:0;background:none;font-size:11px;
  color:var(--tsspdf-accent);cursor:pointer}
.tsspdf-panel-action:disabled{color:var(--tsspdf-fg-disabled);cursor:default}
.tsspdf-panel-empty{padding:16px;font-size:12px;color:var(--tsspdf-fg-muted)}

/* ---------------------------------------------------------------- outline */

.tsspdf-outline{padding:8px}
.tsspdf-outline-item{display:flex;align-items:center;gap:6px;width:100%;padding:5px 8px;
  margin-left:-3px;border:0;border-left:3px solid transparent;border-radius:4px;background:none;
  font-size:13px;color:inherit;text-align:left;cursor:pointer}
.tsspdf-outline-item:hover{background:var(--tsspdf-hover)}
.tsspdf-outline-item.tsspdf-section{background:var(--tsspdf-pressed);
  border-left-color:var(--tsspdf-accent);font-weight:600}
.tsspdf-outline-item.tsspdf-on{color:var(--tsspdf-accent);font-weight:600}
.tsspdf-outline-item:focus-visible{outline:none;box-shadow:0 0 0 2px var(--tsspdf-accent-ring)}
.tsspdf-outline-twisty{display:inline-flex;justify-content:center;width:14px;flex-shrink:0;
  color:var(--tsspdf-fg-faint);cursor:pointer}
.tsspdf-outline-twisty.tsspdf-open svg{transform:rotate(90deg)}
.tsspdf-outline-title{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.tsspdf-outline-page{flex-shrink:0;font-size:11px;color:var(--tsspdf-fg-faint);
  font-family:var(--tss-monospace-font-family)}
.tsspdf-outline-children{padding-left:20px}
.tsspdf-outline-children.tsspdf-collapsed{display:none}

/* ------------------------------------------------------------- thumbnails */

.tsspdf-thumbs{display:grid;grid-template-columns:1fr 1fr;gap:12px;align-content:start;padding:12px}
.tsspdf-thumb{display:flex;flex-direction:column;align-items:center;gap:4px;width:100%;padding:0;
  border:0;background:none;cursor:pointer}
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
.tsspdf-thumb:focus-visible .tsspdf-thumb-frame{box-shadow:0 0 0 2px var(--tsspdf-accent-ring)}
";
    }
}
