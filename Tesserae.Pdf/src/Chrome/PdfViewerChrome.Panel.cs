using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Transpose;
using Transpose.Core;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.PdfChromeElements;
using static TNT.T;

namespace Tesserae.Pdf
{
    /// <summary>
    /// The side panel: a <see cref="Tesserae.Tree"/> of the document's outline or a
    /// <see cref="Grid"/> of page thumbnails, over a footer that says what a search found.
    ///
    /// <b>One pane, no tab strip.</b> The panel used to be a <see cref="Pivot"/>, which was a tab
    /// strip and the thing that swapped the panes - and the tab strip was the second control saying
    /// which pane was open, under two toolbar toggles that already said it and are what a reader
    /// reaches for. So the toggles are the only switch now, the panel shows whichever pane they
    /// chose, and the width the strip took goes to the outline. The Tree stays: it brings the
    /// twisties, the indentation, the keyboard handling and <c>role="tree"</c> with it, and has a
    /// commands slot on every row - which is where the page number goes.
    ///
    /// Two things here are more than they look.
    ///
    /// <b>The outline knows which page each entry is on</b>, which pdf.js does not tell anybody. A
    /// destination is a name or an array whose first element is a page reference, and turning that
    /// into a number takes a round trip to the worker per entry - see
    /// <see cref="PdfDocument.GetDestinationPageAsync"/>. It buys the page number beside each entry
    /// and, more usefully, "which section am I in": the entry the reader is currently inside is the
    /// last one whose page is at or before the page on screen.
    ///
    /// <b>The thumbnails are built as they come into view.</b> A tile is an empty frame until the
    /// scroller brings it close, and only then does it get a <see cref="PdfPageCanvas"/>. A 248-page
    /// document is 248 frames and about a dozen renders, rather than 248 renders of which the reader
    /// sees six.
    /// </summary>
    public sealed partial class PdfViewerChrome
    {
        private Stack       _panelElement;
        private HTMLElement _panelRendered;
        private Stack       _panelBody;
        private HTMLElement _panelBodyElement;
        private Stack       _outlineHost;
        private Stack       _thumbnailHost;
        private HTMLElement _outlineElement;
        private HTMLElement _thumbnailElement;
        private TextBlock   _panelSummary;
        private Button      _panelAction;

        /* -------------------------------------------------------------- assembly */

        /// <summary>
        /// Builds, rebuilds or removes the panel to match <see cref="CurrentPanel"/>.
        ///
        /// Inserted before the view, never after, so the view element itself is never moved - see
        /// <c>BuildChrome</c> on why that matters.
        /// </summary>
        private void BuildPanel()
        {
            if (_panelElement is object)
            {
                ResetThumbnails();

                // The element that was inserted, not a fresh Render(): see _toolbarElement.
                if (_panelRendered is object && _panelRendered.parentElement is object)
                {
                    _panelRendered.parentElement.removeChild(_panelRendered);
                }

                _panelElement     = null;
                _panelRendered    = null;
                _panelBody        = null;
                _panelBodyElement = null;
                _outlineHost      = null;
                _thumbnailHost    = null;
                _outlineElement   = null;
                _thumbnailElement = null;
                _panelSummary     = null;
                _panelAction      = null;
            }

            if (_panel == PdfChromePanel.None) return;

            // The scroller, and the only thing between the panel and its pane. It is a Stack rather
            // than a bare div so the pane goes in through Add and is rendered exactly once by it -
            // see ShowPanelPane on why the element is read back out of the DOM afterwards.
            _panelBody = VStack().WS().Class("tsspdf-panel-body");

            _panelSummary = TextBlock("").Class("tsspdf-panel-count");

            _panelAction = Button("Show in outline".t())
               .NoMargin()
               .NoMinSize()
               .NoPadding()
               .NoBorder()
               .NoBackground()
               .Class("tsspdf-panel-action")
               .OnClick(RevealCurrentPage);

            var footer = HStack().WS().Class("tsspdf-panel-foot").AlignItems(ItemAlign.Center).Gap(8.px())
               .Children(_panelSummary, VStack().Grow(), _panelAction);

            _panelElement = VStack().HS().Class("tsspdf-panel").Children(_panelBody.Grow(), footer);

            _panelRendered = _panelElement.Render();

            _panelBodyElement = Find(_panelRendered, ".tsspdf-panel-body");

            _body.insertBefore(_panelRendered, _view);

            ShowPanelPane();
        }

        /// <summary>
        /// Puts the pane the toolbar's toggles chose into the panel, and takes the other one out.
        ///
        /// <b>Only ever one pane.</b> Keeping the other mounted would keep its thumbnail canvases
        /// rendering pages nobody is looking at, which is the cost the panel is built to avoid.
        ///
        /// <b>Added first, then read back out of the DOM.</b> <c>Render()</c> on a Stack is not
        /// idempotent - asked twice it can hand back a different element - so the host is added to a
        /// scroller that is already mounted, which renders it exactly once, and the element that
        /// actually went in is found afterwards. Rendering it here and adding it would be two calls
        /// and, sooner or later, two elements.
        /// </summary>
        private void ShowPanelPane()
        {
            if (_panelBody is null) return;

            ResetThumbnails();

            _panelBody.Clear();

            _outlineHost      = null;
            _outlineElement   = null;
            _thumbnailHost    = null;
            _thumbnailElement = null;

            if (_panel == PdfChromePanel.Thumbnails)
            {
                _thumbnailHost = VStack().WS().Class("tsspdf-thumbs");

                _panelBody.Add(_thumbnailHost);

                _thumbnailElement = Find(_panelBodyElement, ".tsspdf-thumbs");

                BuildThumbnails();
            }
            else
            {
                _outlineHost = VStack().WS().Class("tsspdf-outline");

                _panelBody.Add(_outlineHost);

                _outlineElement = Find(_panelBodyElement, ".tsspdf-outline");

                BuildOutlineTree();
            }

            UpdatePanelFooter();
        }

        /// <summary>
        /// The footer line: what a search found, or what the document has when nothing is being
        /// searched for.
        ///
        /// The thumbnails tab names the pages the matches are on, because it is showing pages and a
        /// reader can go straight to one. The outline tab offers to reveal the entry instead, because
        /// a list of page numbers means nothing next to a list of section titles.
        /// </summary>
        private void UpdatePanelFooter()
        {
            if (_panelSummary is null) return;

            var searching = _query.Length > 0;

            if (!searching)
            {
                _panelSummary.Text = _pageCount > 0 ? $"{_pageCount} pages".t() : "";
            }
            else if (_matchTotal > 0)
            {
                var summary = $"{_matchTotal} matches".t();

                if (_panel == PdfChromePanel.Thumbnails)
                {
                    var pages = FormatMatchPages();

                    if (pages is object) summary = summary + " · " + pages;
                }

                _panelSummary.Text = summary;
            }
            else
            {
                _panelSummary.Text = _searchSettled ? "No matches".t() : "Searching...".t();
            }

            Show(_panelAction, _panel == PdfChromePanel.Outline && _outlineRows.Count > 0);

            if (_panelAction is object) _panelAction.Disabled(_pageCount == 0);
        }

        /// <summary>
        /// The matched pages, as "pages 12, 15, 19, 24", or null when there are none.
        ///
        /// Capped: a query matching a hundred pages produces a footer nobody reads and a layout that
        /// wraps, so past the cap it says how many more there are.
        /// </summary>
        private string FormatMatchPages()
        {
            if (_matchPages.Count == 0) return null;

            var text  = "";
            var shown = 0;

            foreach (var page in _matchPages)
            {
                if (shown == MATCH_PAGES_SHOWN)
                {
                    return $"pages {text} +{_matchPages.Count - shown} more".t();
                }

                text = shown == 0 ? page.ToString() : text + ", " + page;

                shown++;
            }

            return $"pages {text}".t();
        }

        private const int MATCH_PAGES_SHOWN = 8;

        /* -------------------------------------------------------------- outline */

        private sealed class OutlineRow
        {
            internal PdfOutlineItem Item;
            internal Tree.Item      Node;
            internal HTMLElement    Element;
            internal HTMLElement    PageText;
            internal OutlineRow     Parent;
            internal int            Ordinal;
            internal int            Page;
        }

        private readonly List<OutlineRow> _outlineRows = new List<OutlineRow>();

        /// <summary>
        /// The outline flattened in the order the tree is walked, and beside it the page each entry
        /// resolved to and whether its branch is open.
        ///
        /// <b>Why any of this is kept outside the tree.</b> The tree gets rebuilt - by closing and
        /// reopening the panel, by switching layout - and page numbers that lived on the row objects
        /// would go with it: a rebuilt tree would show no page numbers at all and would not know which
        /// section the reader is in, because the resolution has already happened and does not run
        /// twice. Resolution also has to work when the panel is <b>closed</b>, so it cannot walk rows
        /// that do not exist yet. Both problems go away by keying the answers to a position in the
        /// walk, which is stable for as long as the document is.
        /// </summary>
        private readonly List<PdfOutlineItem> _outlineFlat     = new List<PdfOutlineItem>();
        private readonly List<int>            _outlinePages    = new List<int>();
        private readonly List<bool>           _outlineExpanded = new List<bool>();

        private IReadOnlyList<PdfOutlineItem> _outline;
        private int                           _outlineGeneration;

        private void ResetOutline()
        {
            _outline = null;

            _outlineGeneration++;

            _outlineRows.Clear();
            _outlineFlat.Clear();
            _outlinePages.Clear();
            _outlineExpanded.Clear();
        }

        /// <summary>The outline in the order <see cref="AppendOutlineLevel"/> draws it: entry, then its children.</summary>
        private static void Flatten(IReadOnlyList<PdfOutlineItem> items, List<PdfOutlineItem> into)
        {
            foreach (var item in items)
            {
                into.Add(item);

                Flatten(item.Children, into);
            }
        }

        /// <summary>
        /// Fetches the outline and resolves each entry to a page.
        ///
        /// The tree is drawn as soon as the outline arrives and the page numbers appear afterwards, one
        /// by one: resolving 200 entries is 200 worker round trips, and a panel that waits for all of
        /// them looks broken for as long as they take. Generation-checked, because a document can be
        /// replaced - or the viewer torn down - part way through.
        /// </summary>
        private async Task LoadOutlineAsync()
        {
            var generation = ++_outlineGeneration;
            var document_  = _viewer.Document;

            if (document_ is null) return;

            IReadOnlyList<PdfOutlineItem> outline;

            try
            {
                outline = await document_.GetOutlineAsync();
            }
            catch (Exception)
            {
                // Most documents have no outline at all, and a malformed one is not worth taking the
                // viewer down for. An empty panel says the same thing.
                return;
            }

            if (generation != _outlineGeneration) return;

            _outline = outline;

            _outlineFlat.Clear();
            _outlinePages.Clear();
            _outlineExpanded.Clear();

            Flatten(outline, _outlineFlat);

            foreach (var item in _outlineFlat)
            {
                _outlinePages.Add(0);
                _outlineExpanded.Add(!item.StartsCollapsed);
            }

            if (_panel == PdfChromePanel.Outline) BuildOutlineTree();

            for (var ordinal = 0; ordinal < _outlineFlat.Count; ordinal++)
            {
                var item = _outlineFlat[ordinal];

                if (!item.HasTarget || item.Destination is null) continue;

                int page;

                try
                {
                    page = await document_.GetDestinationPageAsync(item.Destination);
                }
                catch (Exception)
                {
                    continue;
                }

                if (generation != _outlineGeneration) return;

                _outlinePages[ordinal] = page;

                ApplyOutlinePage(ordinal, page);
            }

            if (generation != _outlineGeneration) return;

            HighlightCurrentPage();
            UpdatePanelFooter();
        }

        /// <summary>Puts a resolved page number onto the row showing that entry, if the tree is up.</summary>
        private void ApplyOutlinePage(int ordinal, int page)
        {
            foreach (var row in _outlineRows)
            {
                if (row.Ordinal != ordinal) continue;

                row.Page = page;

                if (row.PageText is object && page > 0) row.PageText.textContent = page.ToString();

                return;
            }
        }

        private void BuildOutlineTree()
        {
            if (_outlineHost is null) return;

            _outlineHost.Clear();
            _outlineRows.Clear();

            if (_outline is null || _outline.Count == 0)
            {
                _outlineHost.Add(TextBlock(_pageCount == 0
                        ? "No document.".t()
                        : "This document has no outline.".t())
                   .Class("tsspdf-panel-empty").Small().Secondary());

                UpdatePanelFooter();

                return;
            }

            var tree = new Tree().Compact(true);

            AppendOutlineLevel(node => tree.Add(node), _outline, null);

            _outlineHost.Add(tree);

            HighlightCurrentPage();
            UpdatePanelFooter();
        }

        /// <summary>
        /// Builds one level of the tree.
        ///
        /// <paramref name="attach"/> rather than a parent parameter, because a <see cref="Tree"/> and a
        /// <see cref="Tree.Item"/> take children through different methods and this way the recursion
        /// does not have to know which it is talking to.
        /// </summary>
        private void AppendOutlineLevel(Action<Tree.Item> attach, IReadOnlyList<PdfOutlineItem> items, OutlineRow parent)
        {
            foreach (var item in items)
            {
                // The position in the walk, which is what the resolved page numbers and the open
                // branches are keyed to - see _outlineFlat. Taken before the row is added, so it
                // matches the order Flatten produced.
                var ordinal = _outlineRows.Count;

                var row = new OutlineRow
                {
                    Item    = item,
                    Parent  = parent,
                    Ordinal = ordinal,
                    Page    = ordinal < _outlinePages.Count ? _outlinePages[ordinal] : 0,
                };

                // The page number lives in the tree's commands slot, and a command is a hover
                // affordance by default - which for a number is wrong: it is information, not an
                // action, and a column that appears under the pointer is a column that shifts.
                var node = new Tree.Item(item.Title ?? "").CommandsAlwaysVisible(true);

                row.Node    = node;
                row.Element = node.Render();

                _outlineRows.Add(row);

                // <b><c>OnClick</c>, not <c>OnSelected</c>.</b> A Tree.Item's click handler toggles
                // the branch and raises OnClick; it only touches selection when the tree has
                // SelectionEnabled *and* the click carried shift, or when the click landed on the
                // checkbox - which this panel hides. So a row wired to OnSelected does nothing at
                // all when a reader clicks it, which is exactly what the outline did.
                //
                // Selection is left off deliberately. The chrome already marks the current entry
                // with a class of its own, because Tree's own Selected() scrolls the row into view
                // with the native scrollIntoView - which walks every scrollable ancestor and takes
                // the host page's scrollbar with it, the same bug PdfViewer.ScrollMatchIntoView
                // exists to avoid. Turning selection on would put that back for shift-clicks and
                // buy a second highlight competing with the one that tracks the page.
                node.OnClick((_, __) => OpenOutlineEntry(row));

                if (item.Children.Count > 0)
                {
                    var expanded = IsOutlineExpanded(row);

                    node.Expanded(expanded);

                    node.OnExpanded(_ => SetOutlineExpanded(row, true));
                    node.OnCollapsed(_ => SetOutlineExpanded(row, false));

                    AppendOutlineLevel(child => node.Add(child), item.Children, row);
                }

                attach(node);

                DecorateOutlineRow(row);
            }
        }

        /// <summary>
        /// Adds the page number to a row, and applies the styling the document asks for.
        ///
        /// <b>The page number goes in the tree's own commands slot</b> - the element every row already
        /// has for trailing content - rather than being appended wherever there was room. That is a
        /// coupling to a <c>tss-</c> class and the one place the chrome reaches into a component's DOM;
        /// it is guarded, so a Tesserae change that renames the slot costs the numbers rather than the
        /// panel.
        /// </summary>
        private void DecorateOutlineRow(OutlineRow row)
        {
            var element = row.Element;
            var content = Find(element, ".tss-tree-item-content");

            if (content is null) return;

            var label = Find(content, ".tss-tree-text");

            if (label is object)
            {
                // The document decides how its own entries look. Colour is deliberately left alone
                // when the PDF names none - PdfOutlineItem reports null rather than black for that
                // exactly so the row can inherit a theme's foreground instead.
                if (row.Item.Bold)            label.style.fontWeight = "600";
                if (row.Item.Italic)          label.style.fontStyle  = "italic";
                if (row.Item.Color is object) label.style.color      = row.Item.Color;

                label.title = row.Item.Title ?? "";
            }

            var commands = Find(content, ".tss-tree-commands");

            if (commands is null) return;

            row.PageText = document.createElement("span").As<HTMLElement>();

            row.PageText.className   = "tsspdf-outline-page";
            row.PageText.textContent = row.Page > 0 ? row.Page.ToString() : "";

            commands.appendChild(row.PageText);
        }

        /// <summary>
        /// Whether a branch is open. Remembered per entry rather than per row, so closing and
        /// reopening the panel does not re-collapse an outline the reader has just opened up.
        /// </summary>
        private bool IsOutlineExpanded(OutlineRow row)
        {
            if (row is null) return false;

            return row.Ordinal < _outlineExpanded.Count
                ? _outlineExpanded[row.Ordinal]
                : !row.Item.StartsCollapsed;
        }

        private void SetOutlineExpanded(OutlineRow row, bool expanded)
        {
            if (row is null) return;

            if (row.Ordinal < _outlineExpanded.Count) _outlineExpanded[row.Ordinal] = expanded;

            if (row.Node is object && row.Node.IsExpanded != expanded) row.Node.Expanded(expanded);
        }

        /// <summary>
        /// Follows an outline entry: to a place in the document, or out to a URL.
        ///
        /// An entry with no target is a heading rather than a link - some outlines are structured that
        /// way - and the tree has already expanded it by the time this runs, which is the right thing
        /// for a heading to do.
        /// </summary>
        private void OpenOutlineEntry(OutlineRow row)
        {
            if (row is null) return;

            if (!string.IsNullOrEmpty(row.Item.Url))
            {
                window.open(row.Item.Url, row.Item.NewWindow ? "_blank" : "_self");

                return;
            }

            if (row.Item.Destination is object) _viewer.GoToDestination(row.Item.Destination);
        }

        /// <summary>
        /// Expands every branch down to the entry covering the page on screen and scrolls it into
        /// view - the footer's "Show in outline".
        ///
        /// Worth being an explicit action rather than something the panel does by itself: a reader who
        /// collapsed a branch collapsed it on purpose, and an outline that re-opens as the document
        /// scrolls fights them.
        /// </summary>
        private void RevealCurrentPage()
        {
            var current = FindCurrentOutlineRow();

            if (current is null) return;

            for (var ancestor = current.Parent; ancestor is object; ancestor = ancestor.Parent)
            {
                SetOutlineExpanded(ancestor, true);
            }

            ScrollPanelTo(current.Element);
        }

        /// <summary>
        /// The entry the reader is currently inside: the last one, in document order, whose page is at
        /// or before the page on screen.
        ///
        /// Document order rather than nearest page, because an outline is a sequence and section 4.2
        /// on page 12 covers pages 12 to 17 even though section 4.3 on page 18 is a closer number for
        /// a reader on page 17.
        /// </summary>
        private OutlineRow FindCurrentOutlineRow()
        {
            if (_page <= 0) return null;

            OutlineRow current = null;

            foreach (var row in _outlineRows)
            {
                if (row.Page <= 0 || row.Page > _page) continue;

                current = row;
            }

            return current;
        }

        /* ----------------------------------------------------------- thumbnails */

        private sealed class ThumbnailTile
        {
            internal Button        Button;
            internal HTMLElement   Element;
            internal HTMLElement   Frame;
            internal HTMLElement   MatchDot;
            internal PdfPageCanvas Canvas;
            internal int           Page;
        }

        private readonly List<ThumbnailTile> _thumbnails = new List<ThumbnailTile>();

        private IntersectionObserver _thumbnailObserver;

        /// <summary>
        /// The first page's width over its height, as a CSS ratio, or null before it is known.
        ///
        /// <b>Why the grid needs this.</b> A frame with no page in it yet has no height of its own, so
        /// without a placeholder ratio every unrendered tile is the same 72px stub - and a panel whose
        /// tiles are the wrong height cannot be scrolled to the right one. Opening the panel on page
        /// 200 of 248 would scroll to wherever 200 stubs end up, which is nowhere near page 200; and
        /// because the tiles that then come into view are the wrong ones, it never converges.
        ///
        /// Taken from page 1 and applied to every frame, then dropped from each frame as its own page
        /// arrives - so a document of mixed page sizes is laid out on a guess and then corrected,
        /// rather than cropped to fit the guess.
        ///
        /// Kept as the two measurements rather than as their quotient, because CSS takes a ratio as
        /// <c>612 / 792</c> and writing a computed decimal out would go through a locale - which is
        /// how a viewer in a comma-decimal locale ends up handing CSS <c>0,77</c> and getting nothing.
        /// </summary>
        private string _thumbnailAspect;

        /// <summary>
        /// Reads the first page's proportions, for the grid to lay its frames out with.
        ///
        /// One page, off the document the viewer already has, and pdf.js caches it - so this costs
        /// nothing next to the renders it makes land in the right place.
        /// </summary>
        private async Task MeasureThumbnailAspectAsync()
        {
            var generation = _outlineGeneration;
            var document_  = _viewer.Document;

            if (document_ is null || document_.PageCount == 0) return;

            PdfPage page;

            try
            {
                page = await document_.GetPageAsync(1);
            }
            catch (Exception)
            {
                return;
            }

            if (generation != _outlineGeneration || page.Width <= 0 || page.Height <= 0) return;

            _thumbnailAspect = Math.Round(page.Width) + " / " + Math.Round(page.Height);

            foreach (var tile in _thumbnails)
            {
                if (tile.Canvas is null) ApplyThumbnailAspect(tile);
            }
        }

        private void ApplyThumbnailAspect(ThumbnailTile tile)
        {
            if (tile.Frame is null || _thumbnailAspect is null) return;

            tile.Frame.style.setProperty("aspect-ratio", _thumbnailAspect);
        }

        private void ResetThumbnails()
        {
            if (_thumbnailObserver is object)
            {
                _thumbnailObserver.disconnect();

                _thumbnailObserver = null;
            }

            foreach (var tile in _thumbnails)
            {
                // Dispose rather than just dropping the element: leaving the DOM tears a
                // PdfPageCanvas down but re-arms it, and these are not coming back.
                if (tile.Canvas is object) tile.Canvas.Dispose();
            }

            _thumbnails.Clear();
        }

        /// <summary>
        /// Builds the grid: one empty frame per page, and an observer that fills a frame in when the
        /// scroller brings it close.
        /// </summary>
        private void BuildThumbnails()
        {
            if (_thumbnailHost is null) return;

            ResetThumbnails();

            _thumbnailHost.Clear();

            var document_ = _viewer.Document;

            if (document_ is null || _pageCount == 0)
            {
                _thumbnailHost.Add(TextBlock("No document.".t()).Class("tsspdf-panel-empty").Small().Secondary());

                return;
            }

            var grid = Grid(new[] { 1.fr(), 1.fr() }).Gap(12.px()).AlignContent(ItemAlign.Start);

            for (var pageNumber = 1; pageNumber <= _pageCount; pageNumber++)
            {
                var captured = pageNumber;
                var tile     = new ThumbnailTile { Page = captured };

                tile.Frame    = Box("tsspdf-thumb-frame");
                tile.MatchDot = Box("tsspdf-thumb-match");

                tile.MatchDot.style.display = "none";

                tile.Frame.appendChild(tile.MatchDot);

                ApplyThumbnailAspect(tile);

                tile.Button = Button()
                   .NoMargin()
                   .NoMinSize()
                   .NoPadding()
                   .NoBorder()
                   .Class("tsspdf-thumb")
                   .SetTitle($"Page {captured}".t())
                   .ReplaceContent(VStack().WS().AlignItems(ItemAlign.Center).Gap(4.px()).Children(
                        Raw(tile.Frame),
                        TextBlock(captured.ToString()).Class("tsspdf-thumb-num")))
                   .OnClick(() => _viewer.GoToPage(captured));

                tile.Element = tile.Button.Render();

                grid.Add(tile.Button);

                _thumbnails.Add(tile);
            }

            _thumbnailHost.Add(grid);

            var options = new IntersectionObserverInit
            {
                root       = PanelScroller(),
                rootMargin = "240px 0px",
            };

            _thumbnailObserver = new IntersectionObserver(new IntersectionObserverCallback((entries, observer) =>
            {
                foreach (var entry in entries)
                {
                    if (!entry.isIntersecting) continue;

                    var tile = FindTile(entry.target.As<HTMLElement>());

                    if (tile is null || tile.Canvas is object) continue;

                    // One page per tile, off the document the viewer already opened - borrowed, not
                    // opened, so the panel never holds a second worker-side copy of the file. The
                    // viewer owns it and releases it.
                    tile.Canvas = PdfJs.PageCanvas()
                       .Document(document_)
                       .Page(tile.Page)
                       .FitWidth()
                       .OnRendered(_2 =>
                       {
                           // The frame's guessed proportions have served their purpose; the canvas in
                           // it carries the real ones. Dropped rather than corrected, so a page that
                           // is not the shape page 1 was is shown whole instead of cropped to it.
                           tile.Frame.style.removeProperty("aspect-ratio");

                           // Every tile that renders above the selected one moves it, so the panel is
                           // re-aimed as the grid settles rather than once while it was all stubs.
                           ScrollSelectedThumbnailIntoView();
                       });

                    var canvasElement = tile.Canvas.Render();

                    // The component sizes itself to its container, and this container has no height
                    // of its own to give - the tile's height is whatever the page turns out to be.
                    // Left at 100% it resolves against an indefinite parent, which is the difference
                    // between a thumbnail and an empty frame.
                    canvasElement.style.height = "auto";

                    tile.Frame.appendChild(canvasElement);

                    // Rendered once and kept: a tile scrolled out and back is the common case, and
                    // re-rendering it would make a flick through the panel a queue of renders.
                    observer.unobserve(entry.target);
                }
            }), options);

            foreach (var tile in _thumbnails)
            {
                _thumbnailObserver.observe(tile.Element);
            }

            HighlightCurrentPage();
            UpdateMatchPages();
        }

        private ThumbnailTile FindTile(HTMLElement element)
        {
            if (element is null) return null;

            foreach (var tile in _thumbnails)
            {
                if (tile.Element == element) return tile;
            }

            return null;
        }

        /* --------------------------------------------------------- current page */

        /// <summary>
        /// Moves the "you are here" marks: the selected thumbnail, and the current entry and section
        /// in the outline.
        /// </summary>
        private void HighlightCurrentPage()
        {
            foreach (var tile in _thumbnails)
            {
                Toggle(tile.Element, PdfChromeStyles.ON, tile.Page == _page);
            }

            ScrollSelectedThumbnailIntoView();

            if (_outlineRows.Count == 0) return;

            var current = FindCurrentOutlineRow();
            var section = current is object ? (current.Parent ?? current) : null;

            foreach (var row in _outlineRows)
            {
                if (row.Node is null) continue;

                // A class rather than the tree's own Selected(), deliberately. Selecting an item
                // programmatically makes the tree scroll it into view with the native
                // scrollIntoView - which scrolls every scrollable ancestor up to the window, so
                // every page change would drag the host application's scrollbar. The same bug
                // PdfViewer.ScrollMatchIntoView exists to avoid, arriving through a component
                // instead of through pdf.js. Selected() stays for what the reader clicks.
                Toggle(row.Element, PdfChromeStyles.CURRENT, row == current);
                Toggle(row.Element, PdfChromeStyles.SECTION, row == section);
            }
        }

        private void ScrollSelectedThumbnailIntoView()
        {
            if (_page <= 0) return;

            foreach (var tile in _thumbnails)
            {
                if (tile.Page == _page) ScrollPanelTo(tile.Element);
            }
        }

        /// <summary>
        /// Scrolls the panel just far enough to bring a row into view, and not at all when it already
        /// is.
        ///
        /// Deliberately not <c>scrollIntoView</c>: that centres, and it scrolls every scrollable
        /// ancestor up to the window - which is the bug <see cref="PdfViewer.ScrollMatchIntoView"/>
        /// exists to avoid, and it would be no better here.
        /// </summary>
        private void ScrollPanelTo(HTMLElement element)
        {
            var scroller = PanelScroller();

            if (scroller is null || element is null) return;

            var target = element.getBoundingClientRect().As<DOMRect>();
            var host   = scroller.getBoundingClientRect().As<DOMRect>();

            if (target.top < host.top)
            {
                scroller.scrollTop += target.top - host.top - MARGIN;
            }
            else if (target.bottom > host.bottom)
            {
                scroller.scrollTop += target.bottom - host.bottom + MARGIN;
            }
        }

        private const int MARGIN = 12;

        /// <summary>
        /// The element the panel's content scrolls in.
        ///
        /// The element the scroller was inserted as, not a fresh <c>Render()</c>: asking a Tesserae
        /// component to render twice can hand back a second element. Everything that reads this
        /// treats null as "cannot scroll yet", which is what it means before the panel is up.
        /// </summary>
        private HTMLElement PanelScroller() => _panelBodyElement;

        /* ------------------------------------------------------- matched pages */

        private readonly List<int> _matchPages = new List<int>();

        private void ClearMatchPages()
        {
            _matchPages.Clear();

            foreach (var tile in _thumbnails)
            {
                tile.MatchDot.style.display = "none";
            }

            UpdatePanelFooter();
        }

        /// <summary>
        /// Reads which pages the search has hit off the find controller, and marks them.
        ///
        /// <c>pageMatches</c> is the only thing that answers this - the events carry totals - and it is
        /// sparse while a search runs: a page pdf.js has not read yet has no entry, which is not the
        /// same as a page with no matches. So this is a running answer too, replaced each time a
        /// report arrives rather than accumulated.
        /// </summary>
        private void UpdateMatchPages()
        {
            _matchPages.Clear();

            if (_query.Length > 0)
            {
                var controller = _viewer.FindController;
                var perPage    = controller is object ? controller.pageMatches : null;

                if (perPage is object)
                {
                    for (double index = 0; index < perPage.length; index++)
                    {
                        var onPage = perPage[index];

                        if (onPage is object && onPage.length > 0) _matchPages.Add((int)index + 1);
                    }
                }
            }

            foreach (var tile in _thumbnails)
            {
                tile.MatchDot.style.display = _matchPages.Contains(tile.Page) ? "" : "none";
            }

            UpdatePanelFooter();
        }
    }
}
