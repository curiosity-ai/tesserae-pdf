using System;
using Transpose;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.Pdf.PdfChromeElements;
using static TNT.T;

namespace Tesserae.Pdf
{
    /// <summary>
    /// The chrome's controls: the two toolbar arrangements, the icon rail, and the zoom menu.
    ///
    /// Every handler here calls a public method on <see cref="PdfViewer"/> and reads its state back
    /// from the event bus rather than from the control - so a zoom changed by a keyboard shortcut, by
    /// a host, or by pdf.js resolving a fit mode after a resize moves the label just the same. No
    /// control in this file is the source of truth for anything.
    /// </summary>
    public sealed partial class PdfViewerChrome
    {
        private HTMLElement _outlineToggle;
        private HTMLElement _thumbnailToggle;

        private HTMLButtonElement _previousPage;
        private HTMLButtonElement _nextPage;
        private HTMLInputElement  _pageBox;
        private HTMLElement       _pageTotal;

        private HTMLButtonElement _zoomField;
        private HTMLElement       _zoomValue;
        private HTMLElement       _zoomMenu;
        private HTMLElement       _railZoom;

        private HTMLButtonElement _fitPageControl;
        private HTMLButtonElement _fitWidthControl;
        private HTMLButtonElement _spreadToggle;

        private HTMLElement _documentNameText;

        /* --------------------------------------------------------- single toolbar */

        /// <summary>
        /// <see cref="PdfChromeLayout.SingleToolbar"/>: one 40px row holding everything, with the
        /// search box pushed to the right by a spring so it takes whatever width is left.
        /// </summary>
        private HTMLElement BuildSingleToolbar()
        {
            var toolbar = Box("tsspdf-toolbar");

            var wroteGroup = false;

            if (_showPanelToggles && (_showOutlineTab || _showThumbnailTab))
            {
                AppendPanelToggles(toolbar);

                wroteGroup = true;
            }

            if (_showPageControls)
            {
                if (wroteGroup) toolbar.appendChild(Separator());

                toolbar.appendChild(BuildPreviousPageButton());
                toolbar.appendChild(BuildPageBox());
                toolbar.appendChild(BuildNextPageButton());

                wroteGroup = true;
            }

            if (_showZoom)
            {
                if (wroteGroup) toolbar.appendChild(Separator());

                toolbar.appendChild(IconButton(PdfChromeIcons.ZOOM_OUT, "Zoom out".t(), () => _viewer.ZoomOut()));
                toolbar.appendChild(BuildZoomField());
                toolbar.appendChild(IconButton(PdfChromeIcons.ZOOM_IN, "Zoom in".t(), () => _viewer.ZoomIn()));

                wroteGroup = true;
            }

            if (_showFitModes)
            {
                if (wroteGroup) toolbar.appendChild(Separator());

                toolbar.appendChild(BuildFitSegments());

                wroteGroup = true;
            }

            if (_showRotate || _showSpread)
            {
                if (wroteGroup) toolbar.appendChild(Separator());

                if (_showRotate) toolbar.appendChild(BuildRotateButton());
                if (_showSpread) toolbar.appendChild(BuildSpreadToggle());
            }

            toolbar.appendChild(Spring());

            if (_showSearch) toolbar.appendChild(BuildSearchBox());

            return toolbar;
        }

        /* ----------------------------------------------------------- split toolbar */

        /// <summary>
        /// <see cref="PdfChromeLayout.IconRail"/>'s top bar: the document's name, the page controls
        /// and the search box. The view controls are on <see cref="BuildRail"/>.
        ///
        /// The name is the only thing in this chrome allowed to shrink to nothing - it is the one
        /// piece that can be elided without taking a control away.
        /// </summary>
        private HTMLElement BuildSplitToolbar()
        {
            var toolbar = Box("tsspdf-toolbar tsspdf-toolbar-split");

            if (_showDocumentName)
            {
                var title = Box("tsspdf-doctitle");

                title.appendChild(Glyph("", PdfChromeIcons.FILE_PDF));

                _documentNameText             = Text("tsspdf-doctitle-text", _effectiveDocumentName ?? "");
                _documentNameText.title       = _effectiveDocumentName ?? "";

                title.appendChild(_documentNameText);
                toolbar.appendChild(title);
            }

            if (_showPageControls)
            {
                if (_showDocumentName) toolbar.appendChild(Separator());

                var group = Box("tsspdf-group");

                group.appendChild(BuildPreviousPageButton());
                group.appendChild(BuildPageBox());
                group.appendChild(BuildNextPageButton());

                toolbar.appendChild(group);
            }

            toolbar.appendChild(Spring());

            if (_showSearch) toolbar.appendChild(BuildSearchBox());

            return toolbar;
        }

        /* ------------------------------------------------------------------- rail */

        /// <summary>
        /// The 48px column of view controls: the panel toggles, the zoom stepper with the percentage
        /// between its two buttons, the two fit modes, rotate and spread.
        ///
        /// Zoom in is above zoom out, and the percentage sits between them, because that is the
        /// direction the buttons point: a vertical stepper reads upwards.
        /// </summary>
        private HTMLElement BuildRail()
        {
            var rail = Box("tsspdf-rail");

            if (_showPanelToggles && (_showOutlineTab || _showThumbnailTab))
            {
                AppendPanelToggles(rail);
            }

            if (_showZoom)
            {
                rail.appendChild(Box("tsspdf-rail-sep"));

                rail.appendChild(IconButton(PdfChromeIcons.ZOOM_IN, "Zoom in".t(), () => _viewer.ZoomIn()));

                _railZoom = Text("tsspdf-rail-zoom", "-");

                rail.appendChild(_railZoom);
                rail.appendChild(IconButton(PdfChromeIcons.ZOOM_OUT, "Zoom out".t(), () => _viewer.ZoomOut()));
            }

            if (_showFitModes)
            {
                rail.appendChild(Box("tsspdf-rail-sep"));

                _fitPageControl  = IconButton(PdfChromeIcons.FIT_PAGE_16,  "Fit page".t(),    () => _viewer.FitPage());
                _fitWidthControl = IconButton(PdfChromeIcons.FIT_WIDTH_16, "Fit content".t(), () => _viewer.FitWidth());

                rail.appendChild(_fitPageControl);
                rail.appendChild(_fitWidthControl);
            }

            if (_showRotate || _showSpread)
            {
                rail.appendChild(Box("tsspdf-rail-sep"));

                if (_showRotate) rail.appendChild(BuildRotateButton());
                if (_showSpread) rail.appendChild(BuildSpreadToggle());
            }

            return rail;
        }

        /* --------------------------------------------------------------- controls */

        private void AppendPanelToggles(HTMLElement host)
        {
            if (_showOutlineTab)
            {
                _outlineToggle = IconButton(PdfChromeIcons.OUTLINE, "Document outline".t(),
                    () => TogglePanel(PdfChromePanel.Outline));

                host.appendChild(_outlineToggle);
            }

            if (_showThumbnailTab)
            {
                _thumbnailToggle = IconButton(PdfChromeIcons.THUMBNAILS, "Thumbnails".t(),
                    () => TogglePanel(PdfChromePanel.Thumbnails));

                host.appendChild(_thumbnailToggle);
            }
        }

        private HTMLButtonElement BuildPreviousPageButton()
        {
            _previousPage = IconButton(PdfChromeIcons.CHEVRON_UP_16, "Previous page".t(), () => _viewer.PreviousPage());

            return _previousPage;
        }

        private HTMLButtonElement BuildNextPageButton()
        {
            _nextPage = IconButton(PdfChromeIcons.CHEVRON_DOWN_16, "Next page".t(), () => _viewer.NextPage());

            return _nextPage;
        }

        /// <summary>
        /// The page number, editable, with the total beside it.
        ///
        /// <b>It takes a page label as readily as a number.</b> A reader typing into this box has just
        /// read a number off the page in front of them, and in a document with front matter that is
        /// "iv" rather than "4". So the label is tried first and the page number second - not the
        /// other way round, or a document whose labels are <c>1..n</c> offset by its front matter
        /// would answer every entry with the wrong page.
        /// </summary>
        private HTMLElement BuildPageBox()
        {
            var group = Box("tsspdf-group");

            _pageBox = document.createElement("input").As<HTMLInputElement>();

            _pageBox.className = "tsspdf-pagebox";
            _pageBox.type      = "text";

            _pageBox.setAttribute("aria-label", "Page".t());

            _pageBox.addEventListener("keydown", new Action<KeyboardEvent>(e =>
            {
                if (e.key == "Enter")
                {
                    CommitPageBox();
                }
                else if (e.key == "Escape")
                {
                    UpdatePageState();

                    _pageBox.blur();
                }

                // Both keys and every other one are left to bubble no further than this box: the
                // viewer scrolls on the arrow keys, and a reader editing a page number is not
                // scrolling.
                e.stopPropagation();
            }));

            _pageBox.addEventListener("change", new Action<Event>(_ => CommitPageBox()));
            _pageBox.addEventListener("input",  new Action<Event>(_ => _pageBoxEdited = true));

            _pageBox.addEventListener("focus", new Action<Event>(_ =>
            {
                _pageBoxEdited = false;

                _pageBox.select();
            }));

            _pageTotal = Text("tsspdf-pagetotal", "");

            group.appendChild(_pageBox);
            group.appendChild(_pageTotal);

            return group;
        }

        /// <summary>
        /// Whether the reader has typed into the page box since it last took focus or committed.
        ///
        /// What stops the box being overwritten mid-edit while the document scrolls under them - and,
        /// equally, what stops it going stale afterwards: once a value is committed it is no longer an
        /// edit in progress, so the next page change writes to it again even though it still has focus.
        /// </summary>
        private bool _pageBoxEdited;

        private void CommitPageBox()
        {
            _pageBoxEdited = false;

            var typed = (_pageBox.value ?? "").Trim();

            if (typed.Length == 0)
            {
                UpdatePageState();

                return;
            }

            var instance = _viewer.ViewerInstance;

            if (instance is object)
            {
                var byLabel = instance.pageLabelToPageNumber(typed);

                if (byLabel > 0)
                {
                    _viewer.GoToPage(byLabel);

                    return;
                }
            }

            int page;

            if (int.TryParse(typed, out page) && page >= 1)
            {
                _viewer.GoToPage(page);
            }

            // Neither a label nor a number: put back what the document is actually showing, rather
            // than leaving a rejected value sitting in the box looking accepted.
            UpdatePageState();
        }

        private HTMLButtonElement BuildRotateButton()
            => IconButton(PdfChromeIcons.ROTATE, "Rotate right".t(), () => _viewer.Rotate());

        /// <summary>
        /// The spread toggle: off, or pairs starting on odd pages, which is how a book falls open.
        ///
        /// Its state comes from pdf.js's <c>spreadmodechanged</c> rather than from a field here,
        /// because a document can ask for a spread itself through its <c>/PageLayout</c> and the
        /// button should show what the viewer is doing, not what was last clicked.
        /// </summary>
        private HTMLButtonElement BuildSpreadToggle()
        {
            _spreadToggle = IconButton(PdfChromeIcons.SPREAD, "Two-page spread".t(), () =>
                _viewer.Spread(_spreadMode == SpreadMode.None ? SpreadMode.Odd : SpreadMode.None));

            return _spreadToggle;
        }

        /// <summary>
        /// The <c>Fit page | Fit content</c> control - as a labelled segmented control in the single
        /// toolbar, and as two icon buttons on the rail.
        ///
        /// <b>"Fit content" is pdf.js's <c>page-width</c></b>, and the wording is deliberate: what a
        /// reader means by it is "make the text as wide as the pane", which is fitting the width.
        /// "Fit width" describes the mechanism rather than the outcome.
        /// </summary>
        private HTMLElement BuildFitSegments()
        {
            var group = Box("tsspdf-seg");

            _fitPageControl = Segment(PdfChromeIcons.FIT_PAGE_14, "Fit page".t(), "Fit the whole page".t(),
                () => _viewer.FitPage());

            _fitWidthControl = Segment(PdfChromeIcons.FIT_WIDTH_14, "Fit content".t(), "Fit the page width".t(),
                () => _viewer.FitWidth());

            group.appendChild(_fitPageControl);
            group.appendChild(_fitWidthControl);

            return group;
        }

        /* ------------------------------------------------------------- zoom menu */

        private HTMLElement BuildZoomField()
        {
            _zoomField = Button("tsspdf-field", "Zoom".t(), ToggleZoomMenu);

            _zoomValue = Text("tsspdf-field-value", "-");

            _zoomField.appendChild(_zoomValue);
            _zoomField.appendChild(Glyph("", PdfChromeIcons.CHEVRON_DOWN_12_FAINT));

            _zoomField.setAttribute("aria-haspopup", "true");

            return _zoomField;
        }

        private void ToggleZoomMenu()
        {
            if (_zoomMenu is object)
            {
                CloseZoomMenu();

                return;
            }

            OpenZoomMenu();
        }

        /// <summary>
        /// Opens the zoom menu under its button.
        ///
        /// Positioned against the chrome's own box rather than the page's, so it moves with a chrome
        /// inside a scrolling pane and cannot be clipped by one - the chrome is
        /// <c>position:relative</c> and this is <c>position:absolute</c> inside it. It is also flipped
        /// to stay inside the chrome's right edge, which is what a chrome narrower than its toolbar
        /// needs.
        /// </summary>
        private void OpenZoomMenu()
        {
            var menu = Box("tsspdf-menu");

            AppendZoomMenuItem(menu, "Fit page".t(),    IsPresetInForce("page-fit"),    () => _viewer.FitPage());
            AppendZoomMenuItem(menu, "Fit content".t(), IsPresetInForce("page-width"),  () => _viewer.FitWidth());
            AppendZoomMenuItem(menu, "Actual size".t(), IsPresetInForce("page-actual"), () => _viewer.ActualSize());
            AppendZoomMenuItem(menu, "Automatic".t(),   IsPresetInForce("auto"),        () => _viewer.AutoZoom());

            menu.appendChild(Box("tsspdf-menu-sep"));

            foreach (var level in _zoomLevels)
            {
                var captured = level;

                AppendZoomMenuItem(menu, FormatPercent(captured), IsZoomInForce(captured), () => _viewer.Zoom(captured));
            }

            _zoomMenu = menu;

            _root.appendChild(menu);

            var anchor = _zoomField.getBoundingClientRect().As<DOMRect>();
            var host   = _root.getBoundingClientRect().As<DOMRect>();

            var left = anchor.left - host.left;

            // Keep it inside the chrome. 8px of margin so it never sits flush against the edge.
            var overflow = left + menu.offsetWidth - (host.width - 8);

            if (overflow > 0) left -= overflow;

            menu.style.left = (left < 8 ? 8 : left) + "px";
            menu.style.top  = (anchor.bottom - host.top + 5) + "px";

            _zoomField.classList.add(PdfChromeStyles.OPEN);

            // Both handlers are captured in fields so the same delegate can be removed again: a
            // document listener is removed by identity, and a second lambda would not match.
            _dismissZoomMenu = new Action<Event>(e =>
            {
                var target = e.target.As<HTMLElement>();

                if (target is object && _zoomMenu  is object && _zoomMenu.contains(target))  return;
                if (target is object && _zoomField is object && _zoomField.contains(target)) return;

                CloseZoomMenu();
            });

            _escapeZoomMenu = new Action<Event>(e =>
            {
                if (e.As<KeyboardEvent>().key == "Escape") CloseZoomMenu();
            });

            document.addEventListener("pointerdown", _dismissZoomMenu);
            document.addEventListener("keydown", _escapeZoomMenu);
        }

        private Action<Event> _dismissZoomMenu;
        private Action<Event> _escapeZoomMenu;

        private void CloseZoomMenu()
        {
            if (_dismissZoomMenu is object)
            {
                document.removeEventListener("pointerdown", _dismissZoomMenu);

                _dismissZoomMenu = null;
            }

            if (_escapeZoomMenu is object)
            {
                document.removeEventListener("keydown", _escapeZoomMenu);

                _escapeZoomMenu = null;
            }

            if (_zoomMenu is object)
            {
                if (_zoomMenu.parentElement is object) _zoomMenu.parentElement.removeChild(_zoomMenu);

                _zoomMenu = null;
            }

            if (_zoomField is object) _zoomField.classList.remove(PdfChromeStyles.OPEN);
        }

        private void AppendZoomMenuItem(HTMLElement menu, string label, bool current, Action apply)
        {
            var item = Button("tsspdf-menu-item", null, () =>
            {
                CloseZoomMenu();

                apply();
            });

            item.appendChild(Glyph("tsspdf-menu-check", PdfChromeIcons.CHECK_13));
            item.appendChild(Text("", label));

            if (current) item.classList.add(PdfChromeStyles.ON);

            item.setAttribute("aria-checked", current ? "true" : "false");

            menu.appendChild(item);
        }

        /// <summary>Whether a named fit mode is the one in force.</summary>
        private bool IsPresetInForce(string preset) => _scalePreset == preset;

        /// <summary>
        /// Whether a fixed step is the zoom in force.
        ///
        /// Compared with a tolerance, and only when no fit mode is active: pdf.js keeps the scale as a
        /// double and resolves it through its own arithmetic, so a viewer asked for 2 does not
        /// necessarily report exactly 2 afterwards.
        /// </summary>
        private bool IsZoomInForce(double factor)
            => string.IsNullOrEmpty(_scalePreset) && Math.Abs(_scale - factor) < 0.005;

        private static string FormatPercent(double scale)
            => Math.Round(scale * 100) + "%";

        /* ------------------------------------------------------------ state → DOM */

        /// <summary>Reflects the panel, spread and fit state onto whichever controls are drawn.</summary>
        private void UpdateToolbarState()
        {
            Toggle(_outlineToggle,   PdfChromeStyles.ON, _panel == PdfChromePanel.Outline);
            Toggle(_thumbnailToggle, PdfChromeStyles.ON, _panel == PdfChromePanel.Thumbnails);
            Toggle(_spreadToggle,    PdfChromeStyles.ON, _spreadMode != SpreadMode.None);

            SetPressed(_outlineToggle,   _panel == PdfChromePanel.Outline);
            SetPressed(_thumbnailToggle, _panel == PdfChromePanel.Thumbnails);
            SetPressed(_spreadToggle,    _spreadMode != SpreadMode.None);

            UpdateZoomState();
        }

        private void UpdateZoomState()
        {
            var percent = _scale > 0 ? FormatPercent(_scale) : "-";

            if (_zoomValue is object) _zoomValue.textContent = percent;
            if (_railZoom  is object) _railZoom.textContent  = percent;

            var fitPage  = _scalePreset == "page-fit";
            var fitWidth = _scalePreset == "page-width";

            Toggle(_fitPageControl,  PdfChromeStyles.ON, fitPage);
            Toggle(_fitWidthControl, PdfChromeStyles.ON, fitWidth);

            SetPressed(_fitPageControl,  fitPage);
            SetPressed(_fitWidthControl, fitWidth);

            // The menu shows a tick against the current zoom, so it has to be rebuilt or closed when
            // that changes underneath it. Closing is the honest option: a zoom that moved while the
            // menu was open moved because something else asked, and silently re-ticking a different
            // row under the pointer is how a mis-click happens.
            if (_zoomMenu is object) CloseZoomMenu();
        }

        private void UpdatePageState()
        {
            if (_pageBox is object)
            {
                var label = CurrentPageLabel();

                // Not while it is being edited: replacing what somebody is halfway through typing is
                // maddening, and pagechanging fires as the document scrolls under them.
                if (!(_pageBoxEdited && document.activeElement == _pageBox))
                {
                    _pageBox.value = _pageCount > 0 ? (label ?? _page.ToString()) : "";
                }

                _pageBox.disabled = _pageCount == 0;
                _pageBox.title = _pageCount > 0 ? $"Page {_page} of {_pageCount}".t() : "";
            }

            if (_pageTotal is object)
            {
                // With page labels the box holds a label, not a number, so "9 of 12" would be a lie
                // about both halves. The parenthesised form is pdf.js's own answer to that, and it is
                // the right one: the box says what is printed on the page, and this says where in the
                // document that is. A document with no labels - most of them - reads "of 12".
                _pageTotal.textContent = _pageCount == 0 ? ""
                                       : HasPageLabel()  ? $"({_page} of {_pageCount})".t()
                                                         : $"of {_pageCount}".t();
            }

            if (_previousPage is object) _previousPage.disabled = _pageCount == 0 || _page <= 1;
            if (_nextPage     is object) _nextPage.disabled     = _pageCount == 0 || _page >= _pageCount;
        }

        /// <summary>
        /// The label the document wants the current page called, or null when it uses plain numbers.
        ///
        /// Read off the viewer rather than remembered from the <c>pagechanging</c> event: labels are
        /// fetched and handed over after the document loads, so the first event of a document carries
        /// none even for a document that has them.
        /// </summary>
        private string CurrentPageLabel()
        {
            var instance = _viewer.ViewerInstance;

            return instance is object ? instance.currentPageLabel : null;
        }

        /// <summary>
        /// Whether the document labels its pages as something other than their numbers.
        ///
        /// A document whose labels happen to be "1", "2", ... counts as having none: the labels are
        /// real, but "(3 of 12)" beside a box reading 3 tells the reader nothing.
        /// </summary>
        private bool HasPageLabel()
        {
            var label = CurrentPageLabel();

            return !string.IsNullOrEmpty(label) && label != _page.ToString();
        }

        /// <summary>
        /// Mirrors a toggle's visual state into <c>aria-pressed</c>. Separate from the class because a
        /// button that is only visually selected is selected for exactly the people who cannot see it.
        /// </summary>
        private static void SetPressed(HTMLElement element, bool pressed)
        {
            if (element is null) return;

            element.setAttribute("aria-pressed", pressed ? "true" : "false");
        }
    }
}
