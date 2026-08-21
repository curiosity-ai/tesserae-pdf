using System;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.PdfChromeElements;
using static TNT.T;

namespace Tesserae.Pdf
{
    /// <summary>
    /// The chrome's controls: the two toolbar arrangements, the icon rail, and the zoom menu.
    ///
    /// Every control here is a Tesserae component, and every handler calls a public method on
    /// <see cref="PdfViewer"/> and reads its state back from the event bus rather than from the
    /// control - so a zoom changed by a keyboard shortcut, by a host, or by pdf.js resolving a fit
    /// mode after a resize moves the label just the same. No control in this file is the source of
    /// truth for anything.
    /// </summary>
    public sealed partial class PdfViewerChrome
    {
        private Button _outlineToggle;
        private Button _thumbnailToggle;
        private Button _previousPage;
        private Button _nextPage;
        private Button _zoomButton;
        private Button _fitPageControl;
        private Button _fitWidthControl;
        private Button _spreadToggle;

        private TextBox   _pageBox;
        private TextBlock _pageTotal;
        private TextBlock _railZoom;
        private TextBlock _documentNameText;

        private ContextMenu _zoomMenu;
        private Button      _overflowButton;

        /* --------------------------------------------------------- single toolbar */

        /// <summary>
        /// <see cref="PdfChromeLayout.SingleToolbar"/>: one 40px row holding everything, with the
        /// search box pushed to the right by the page controls' growth so it takes what is left.
        /// </summary>
        private IComponent BuildSingleToolbar()
        {
            var toolbar = HStack().WS().Class("tsspdf-toolbar").AlignItems(ItemAlign.Center).Gap(2.px());

            var wroteGroup = false;

            if (_showPanelToggles && (_showOutlineTab || _showThumbnailTab))
            {
                AppendPanelToggles(toolbar);

                wroteGroup = true;
            }

            if (_showPageControls)
            {
                if (wroteGroup) toolbar.Add(Separator());

                toolbar.Add(BuildPreviousPageButton());
                toolbar.Add(BuildPageBox());
                toolbar.Add(BuildNextPageButton());

                wroteGroup = true;
            }

            if (_showZoom)
            {
                if (wroteGroup) toolbar.Add(Separator());

                // One group, so the band that cannot fit it hides it with one rule - and so the
                // overflow menu has one thing to stand in for.
                toolbar.Add(HStack().Class("tsspdf-zoomgroup").AlignItems(ItemAlign.Center).Gap(2.px()).Children(
                    IconButton(UIcons.ZoomOut, "Zoom out".t(), () => _viewer.ZoomOut()),
                    BuildZoomButton(),
                    IconButton(UIcons.ZoomIn, "Zoom in".t(), () => _viewer.ZoomIn())));

                wroteGroup = true;
            }

            // <b>No fit control on the toolbar.</b> Fit page and Fit content are two of the four
            // entries at the top of the zoom menu, next to the percentages they compete with, and a
            // segmented pill repeating them in the row was the same choice twice - the widest thing
            // in the toolbar, spent on a mode a reader sets once. The rail keeps its two icon
            // buttons: that layout has no zoom menu to put them in.
            if (_showRotate || _showSpread)
            {
                if (wroteGroup) toolbar.Add(Separator());

                if (_showRotate) toolbar.Add(BuildRotateButton());
                if (_showSpread) toolbar.Add(BuildSpreadToggle());
            }

            toolbar.Add(BuildOverflowButton());

            if (_showSearch)
            {
                // Grow rather than a spacer: the search row takes the width nothing else claimed,
                // which is what pushes it to the right and what makes it the thing that shrinks.
                toolbar.Add(BuildSearchRow().Grow());
            }
            else
            {
                toolbar.Add(VStack().Grow());
            }

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
        private IComponent BuildSplitToolbar()
        {
            var toolbar = HStack().WS().Class("tsspdf-toolbar tsspdf-toolbar-split")
               .AlignItems(ItemAlign.Center).Gap(8.px());

            if (_showDocumentName)
            {
                _documentNameText = TextBlock(_effectiveDocumentName ?? "").Class("tsspdf-doctitle-text");

                toolbar.Add(HStack().Class("tsspdf-doctitle").AlignItems(ItemAlign.Center).Gap(8.px())
                   .Children(Icon(UIcons.FilePdf).Foreground("var(--tsspdf-danger)"), _documentNameText));
            }

            if (_showPageControls)
            {
                if (_showDocumentName) toolbar.Add(Separator());

                toolbar.Add(HStack().AlignItems(ItemAlign.Center).Gap(2.px()).Children(
                    BuildPreviousPageButton(), BuildPageBox(), BuildNextPageButton()));
            }

            if (_showSearch)
            {
                toolbar.Add(BuildSearchRow().Grow());
            }
            else
            {
                toolbar.Add(VStack().Grow());
            }

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
        private IComponent BuildRail()
        {
            var rail = VStack().HS().Class("tsspdf-rail").AlignItems(ItemAlign.Center).Gap(2.px());

            if (_showPanelToggles && (_showOutlineTab || _showThumbnailTab)) AppendPanelToggles(rail);

            if (_showZoom)
            {
                rail.Add(Raw(Box("tsspdf-rail-sep")));
                rail.Add(IconButton(UIcons.ZoomIn, "Zoom in".t(), () => _viewer.ZoomIn()));

                _railZoom = TextBlock("-").Class("tsspdf-rail-zoom");

                rail.Add(_railZoom);
                rail.Add(IconButton(UIcons.ZoomOut, "Zoom out".t(), () => _viewer.ZoomOut()));
            }

            if (_showFitModes)
            {
                rail.Add(Raw(Box("tsspdf-rail-sep")));

                // <b>"Fit content" is pdf.js's <c>page-width</c></b>, and the wording is deliberate:
                // what a reader means by it is "make the text as wide as the pane", which is fitting
                // the width. "Fit width" describes the mechanism rather than the outcome.
                _fitPageControl  = IconButton(UIcons.Compress, "Fit page".t(),    () => _viewer.FitPage());
                _fitWidthControl = IconButton(UIcons.ArrowsH,  "Fit content".t(), () => _viewer.FitWidth());

                rail.Add(_fitPageControl);
                rail.Add(_fitWidthControl);
            }

            if (_showRotate || _showSpread)
            {
                rail.Add(Raw(Box("tsspdf-rail-sep")));

                if (_showRotate) rail.Add(BuildRotateButton());
                if (_showSpread) rail.Add(BuildSpreadToggle());
            }

            return rail;
        }

        /* --------------------------------------------------------------- controls */

        private void AppendPanelToggles(Stack host)
        {
            if (_showOutlineTab)
            {
                _outlineToggle = IconButton(UIcons.ListTree, "Document outline".t(),
                    () => TogglePanel(PdfChromePanel.Outline));

                host.Add(_outlineToggle);
            }

            if (_showThumbnailTab)
            {
                _thumbnailToggle = IconButton(UIcons.Apps, "Thumbnails".t(),
                    () => TogglePanel(PdfChromePanel.Thumbnails));

                host.Add(_thumbnailToggle);
            }
        }

        private Button BuildPreviousPageButton()
        {
            _previousPage = IconButton(UIcons.AngleUp, "Previous page".t(), () => _viewer.PreviousPage());

            return _previousPage;
        }

        private Button BuildNextPageButton()
        {
            _nextPage = IconButton(UIcons.AngleDown, "Next page".t(), () => _viewer.NextPage());

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
        private IComponent BuildPageBox()
        {
            _pageBox = TextBox().NoSpellCheck().Class("tsspdf-pagebox");

            var input = Find(_pageBox, "input").As<HTMLInputElement>();

            if (input is object)
            {
                input.setAttribute("aria-label", "Page".t());

                input.addEventListener("keydown", new Action<KeyboardEvent>(e =>
                {
                    if (e.key == "Enter")
                    {
                        CommitPageBox();
                    }
                    else if (e.key == "Escape")
                    {
                        UpdatePageState();

                        input.blur();
                    }

                    // Neither key, nor any other, is left to bubble past this box: the viewer scrolls
                    // on the arrow keys and the chrome takes Ctrl+F, and a reader editing a page
                    // number is doing neither.
                    e.stopPropagation();
                }));

                input.addEventListener("input", new Action<Event>(_ => _pageBoxEdited = true));

                input.addEventListener("focus", new Action<Event>(_ =>
                {
                    _pageBoxEdited = false;

                    input.select();
                }));
            }

            _pageBox.Attach(_ => CommitPageBox());

            _pageTotal = TextBlock("").Class("tsspdf-pagetotal");

            return HStack().AlignItems(ItemAlign.Center).Gap(5.px()).Children(_pageBox, _pageTotal);
        }

        /// <summary>
        /// Whether the reader has typed into the page box since it last took focus or committed.
        ///
        /// What stops the box being overwritten mid-edit while the document scrolls under them - and,
        /// equally, what stops it going stale afterwards: once a value is committed it is no longer an
        /// edit in progress, so the next page change writes to it again even though it still has focus.
        /// </summary>
        private bool _pageBoxEdited;

        /// <summary>
        /// Set while the chrome is writing into the page box.
        ///
        /// <b>A Tesserae input raises the same event for a programmatic write as for a keystroke</b>,
        /// so putting the current page into the box from inside the handler for that event is a loop.
        /// The guard is on the write rather than on the handler, so the handler stays the single place
        /// that reacts to a change.
        /// </summary>
        private bool _writingPageBox;

        private void WritePageBox(string value)
        {
            if (_pageBox is null) return;

            _writingPageBox = true;

            try
            {
                _pageBox.Text = value;
            }
            finally
            {
                _writingPageBox = false;
            }
        }

        private void CommitPageBox()
        {
            if (_writingPageBox) return;

            _pageBoxEdited = false;

            var typed = (_pageBox.Text ?? "").Trim();

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

        private Button BuildRotateButton()
            => IconButton(UIcons.RotateRight, "Rotate right".t(), () => _viewer.Rotate()).Class("tsspdf-rotate");

        /// <summary>
        /// The spread toggle: off, or pairs starting on odd pages, which is how a book falls open.
        ///
        /// Its state comes from pdf.js's <c>spreadmodechanged</c> rather than from a field here,
        /// because a document can ask for a spread itself through its <c>/PageLayout</c> and the
        /// button should show what the viewer is doing, not what was last clicked. Which is also why
        /// it is a <see cref="Button"/> and not a <c>ToggleButton</c>: the state lives in the viewer,
        /// and a control with its own copy of it would be a second source of truth.
        /// </summary>
        private Button BuildSpreadToggle()
        {
            _spreadToggle = IconButton(UIcons.TableColumns, "Two-page spread".t(), () =>
                    _viewer.Spread(_spreadMode == SpreadMode.None ? SpreadMode.Odd : SpreadMode.None))
               .Class("tsspdf-spread");

            return _spreadToggle;
        }

        /// <summary>
        /// The overflow menu: where the controls a narrow toolbar cannot show go.
        ///
        /// Built on open rather than kept, because what is in it depends on the width band in force -
        /// and because the fit modes need a tick against the one that is active, which moves.
        /// </summary>
        private Button BuildOverflowButton()
        {
            _overflowButton = IconButton(UIcons.GripDots, "More controls".t(), ShowOverflowMenu);

            Show(_overflowButton, false);

            return _overflowButton;
        }

        private void ShowOverflowMenu()
        {
            var menu = ContextMenu();

            // <b>A flag, not <c>menu.Render()</c>.</b> A ContextMenu is a Layer, and Layer.Render
            // throws NotImplementedException - it is shown, never rendered in place - so asking
            // "does it have anything in it yet" to decide on a divider threw the moment a band put
            // two groups in the menu, which is every band that puts anything in it. Counting what
            // was written is the question anyway.
            var wrote = false;

            if (_showZoom && ZoomInOverflow)
            {
                menu.Add(MenuRow("Zoom in".t(),  UIcons.ZoomIn,  false, () => _viewer.ZoomIn()));
                menu.Add(MenuRow("Zoom out".t(), UIcons.ZoomOut, false, () => _viewer.ZoomOut()));

                wrote = true;
            }

            if (_showFitModes && FitModesInOverflow)
            {
                if (wrote) menu.Add(ContextMenuItem("").Divider());

                menu.Add(MenuRow("Fit page".t(),    UIcons.Compress, IsPresetInForce("page-fit"),    () => _viewer.FitPage()));
                menu.Add(MenuRow("Fit content".t(), UIcons.ArrowsH,  IsPresetInForce("page-width"),  () => _viewer.FitWidth()));
                menu.Add(MenuRow("Actual size".t(), null,            IsPresetInForce("page-actual"), () => _viewer.ActualSize()));

                wrote = true;
            }

            if (ViewControlsInOverflow && (_showRotate || _showSpread))
            {
                if (wrote) menu.Add(ContextMenuItem("").Divider());

                if (_showRotate)
                {
                    menu.Add(MenuRow("Rotate right".t(), UIcons.RotateRight, false, () => _viewer.Rotate()));
                }

                if (_showSpread)
                {
                    menu.Add(MenuRow("Two-page spread".t(), UIcons.TableColumns, _spreadMode != SpreadMode.None,
                        () => _viewer.Spread(_spreadMode == SpreadMode.None ? SpreadMode.Odd : SpreadMode.None)));
                }

                wrote = true;
            }

            if (_showSearch && SearchModeInOverflow)
            {
                if (wrote) menu.Add(ContextMenuItem("").Divider());

                menu.Add(MenuRow("Fuzzy".t(), null, _searchMode == PdfSearchMode.Fuzzy,
                    () => SearchMode(PdfSearchMode.Fuzzy)));

                menu.Add(MenuRow("Precise".t(), null, _searchMode == PdfSearchMode.Precise,
                    () => SearchMode(PdfSearchMode.Precise)));
            }

            menu.ShowFor(_overflowButton, 0, 4);
        }

        /// <summary>
        /// One row of a menu: an optional tick, an icon, and a label.
        ///
        /// The tick column is always there, occupied or not, so the labels line up whether anything is
        /// selected or not - a menu whose text moves when a tick appears is the same shifting the rest
        /// of this chrome is careful to avoid.
        /// </summary>
        private static ContextMenu.Item MenuRow(string label, UIcons? icon, bool current, Action apply)
        {
            var row = HStack().AlignItems(ItemAlign.Center).Gap(8.px());

            row.Add(current
                ? (IComponent)Icon(UIcons.Check).Foreground("var(--tsspdf-accent)")
                : TextBlock("").W(13));

            if (icon.HasValue) row.Add(Icon(icon.Value).Foreground("var(--tsspdf-fg-muted)"));

            row.Add(current ? TextBlock(label).SemiBold().Foreground("var(--tsspdf-accent)") : TextBlock(label));

            return ContextMenuItem(row).OnClick(apply);
        }

        /* ------------------------------------------------------------- zoom menu */

        private Button BuildZoomButton()
        {
            _zoomButton = Button("-")
               .SetIcon(UIcons.AngleDown)
               .NoMargin()
               .NoMinSize()
               .NoPadding()
               .Class("tsspdf-zoom")
               .OnClick(ShowZoomMenu);

            _zoomButton = PdfChromeElements.Tip(_zoomButton, "Zoom and fit".t());

            return _zoomButton;
        }

        /// <summary>
        /// Opens the zoom menu under its button.
        ///
        /// A Tesserae <see cref="ContextMenu"/>, which brings the positioning, the outside-click
        /// dismissal, the Escape handling and the layer stacking with it - all of which the chrome
        /// previously did itself. Rebuilt on every open rather than kept, because the tick beside the
        /// entry in force moves.
        /// </summary>
        private void ShowZoomMenu()
        {
            _zoomMenu = ContextMenu();

            AppendZoomMenuItem("Fit page".t(),    IsPresetInForce("page-fit"),    () => _viewer.FitPage());
            AppendZoomMenuItem("Fit content".t(), IsPresetInForce("page-width"),  () => _viewer.FitWidth());
            AppendZoomMenuItem("Actual size".t(), IsPresetInForce("page-actual"), () => _viewer.ActualSize());
            AppendZoomMenuItem("Automatic".t(),   IsPresetInForce("auto"),        () => _viewer.AutoZoom());

            _zoomMenu.Add(ContextMenuItem("").Divider());

            foreach (var level in _zoomLevels)
            {
                var captured = level;

                AppendZoomMenuItem(FormatPercent(captured), IsZoomInForce(captured), () => _viewer.Zoom(captured));
            }

            _zoomMenu.ShowFor(_zoomButton, 0, 4);
        }

        private void AppendZoomMenuItem(string label, bool current, Action apply)
            => _zoomMenu.Add(MenuRow(label, null, current, apply));

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

        private static string FormatPercent(double scale) => Math.Round(scale * 100) + "%";

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

            if (_zoomButton is object) _zoomButton.Text = percent;
            if (_railZoom   is object) _railZoom.Text   = percent;

            var fitPage  = _scalePreset == "page-fit";
            var fitWidth = _scalePreset == "page-width";

            Toggle(_fitPageControl,  PdfChromeStyles.ON, fitPage);
            Toggle(_fitWidthControl, PdfChromeStyles.ON, fitWidth);

            SetPressed(_fitPageControl,  fitPage);
            SetPressed(_fitWidthControl, fitWidth);
        }

        private void UpdatePageState()
        {
            if (_pageBox is object)
            {
                var label = CurrentPageLabel();
                var input = Find(_pageBox, "input").As<HTMLInputElement>();

                // Not while it is being edited: replacing what somebody is halfway through typing is
                // maddening, and pagechanging fires as the document scrolls under them.
                if (!(_pageBoxEdited && input is object && document.activeElement == input))
                {
                    WritePageBox(_pageCount > 0 ? (label ?? _page.ToString()) : "");
                }

                _pageBox.Disabled(_pageCount == 0);

                if (input is object) input.title = _pageCount > 0 ? $"Page {_page} of {_pageCount}".t() : "";
            }

            if (_pageTotal is object)
            {
                // With page labels the box holds a label, not a number, so "9 of 12" would be a lie
                // about both halves: the box says what is printed on the page, and this says where in
                // the document that is. A document with no labels - most of them - reads "of 12".
                //
                // Which form it is, is decided by the document rather than by the page: asking per
                // page whether the label differs from the number makes the text - and so the width of
                // everything after it - flip as the reader scrolls.
                _pageTotal.Text = _pageCount == 0   ? ""
                                : _documentHasLabels ? $"{_page} of {_pageCount}".t()
                                                     : $"of {_pageCount}".t();
            }

            if (_previousPage is object) _previousPage.Disabled(_pageCount == 0 || _page <= 1);
            if (_nextPage     is object) _nextPage.Disabled(_pageCount == 0 || _page >= _pageCount);
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
        /// Whether the document labels its pages as something other than their numbers - decided once
        /// per document, from the labels themselves.
        ///
        /// A document whose labels happen to be "1", "2", ... counts as having none: the labels are
        /// real, but "3 of 12" beside a box reading 3 tells the reader nothing. Asking that question
        /// of the whole array rather than of the current page is what keeps the answer - and the width
        /// of the text - the same on every page of the document.
        /// </summary>
        private bool _documentHasLabels;

        private void ApplyPageLabels(string[] labels)
        {
            _documentHasLabels = false;
            

            if (labels is object)
            {
                for (var i = 0; i < labels.Length; i++)
                {
                    if (string.IsNullOrEmpty(labels[i]) || labels[i] == (i + 1).ToString()) continue;

                    _documentHasLabels = true;

                    break;
                }
            }

            UpdatePageState();
        }
    }
}
