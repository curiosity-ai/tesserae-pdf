using System;
using System.Collections.Generic;
using Transpose;
using Transpose.Core;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.Pdf.PdfChromeElements;

namespace Tesserae.Pdf
{
    /// <summary>
    /// A <see cref="PdfViewer"/> with a toolbar around it: page and zoom controls, fit modes, rotate
    /// and spread, an always-visible search box, and a tabbed outline / thumbnails panel.
    ///
    /// <code>
    /// PdfJs.ViewerChrome()
    ///    .Url("/api/files/42.pdf")
    ///    .Panel(PdfChromePanel.Outline)
    /// </code>
    ///
    /// <b>Why this exists alongside <see cref="PdfViewer"/>, which deliberately draws no toolbar.</b>
    /// That reasoning has not changed - a toolbar is the part of a viewer that has to look like the
    /// rest of the application, and the same viewer gets asked for by a full-page reader, a preview
    /// pane and a modal that want three different sets of buttons. What it left out was the fourth
    /// case: an application that wants a document reader and does not want to have an opinion about
    /// it. This is that reader. It is a composition of the public surface below it and nothing more -
    /// every control here calls a method a host could call itself - so "start with the chrome and
    /// replace it later" costs nothing but the toolbar.
    ///
    /// <b>Everything is still reachable.</b> <see cref="Viewer"/> is the component underneath, with
    /// its whole surface: <c>Options</c>, <c>Configure</c>, the annotation editor, scripting,
    /// password handling, the typed failures. The chrome subscribes to pdf.js's event bus rather than
    /// to the viewer's <c>On*</c> callbacks precisely so that all of those stay free for the host -
    /// <c>chrome.Viewer.OnPageChanged(...)</c> does not fight the page box for the slot.
    ///
    /// <b>Two layouts</b>, from one set of controls - see <see cref="PdfChromeLayout"/>.
    ///
    /// <b>It follows Tesserae's theme.</b> Every colour resolves to a <c>--tss-*</c> variable, so
    /// <c>UI.Theme.Dark()</c> and a host's own <c>Theme.Build()</c> both come through with no work
    /// here. See <see cref="PdfChromeStyles"/>.
    ///
    /// <b>Lifetime</b> is the viewer's: leaving the DOM tears the view down and re-adding it rebuilds
    /// it, and the chrome rebuilds its panel with it. <see cref="Dispose"/> is the one-way door.
    /// </summary>
    public sealed partial class PdfViewerChrome : IComponent, ISpecialCaseStyling, IDisposable
    {
        private readonly PdfViewer   _viewer;
        private readonly HTMLElement _root;
        private readonly HTMLElement _body;
        private readonly HTMLElement _view;
        private readonly HTMLElement _viewerElement;

        private IComponent _toolbar;
        private IComponent _rail;

        // The elements those two rendered to.
        //
        // <b>Render() is not idempotent on a Tesserae component</b> - a Stack asked twice can hand back
        // a different element - so the thing that was inserted has to be remembered rather than asked
        // for again. Asking again leaves the first copy in the DOM, which is two toolbars.
        private HTMLElement _toolbarElement;
        private HTMLElement _railElement;

        private ResizeObserver _sizeObserver;

        /// <summary>
        /// The width band the chrome is currently in, as the class that is on its root.
        ///
        /// <b>Why bands rather than a media query.</b> A media query asks about the window, and this
        /// is a component: the same page can hold one of these full-width and another in a 360px
        /// pane, and they need different toolbars. So the breakpoints are measured on the chrome's own
        /// box and published as a class, which is what the sheet keys off.
        ///
        /// Nothing is ever hidden without somewhere else to reach it: what the bands take out of the
        /// toolbar goes into the overflow menu, and the fit modes are in the zoom menu regardless.
        /// </summary>
        private string _widthClass = "";

        /// <summary>Below this, the fit modes lose their labels and keep their icons.</summary>
        private const int NARROW_WIDTH = 1180;

        /// <summary>Below this, the fit modes and the document's name leave the toolbar.</summary>
        private const int TIGHT_WIDTH = 940;

        /// <summary>
        /// Below this the toolbar keeps only what a reader cannot do without - the panel toggle, the
        /// page controls and search - and everything else moves into the overflow menu. The side panel
        /// stops taking width from the document and covers it instead.
        /// </summary>
        private const int MINI_WIDTH = 700;

        /* ------------------------------------------------------------ configuration */

        private PdfChromeLayout _layout      = PdfChromeLayout.SingleToolbar;
        private PdfChromePanel  _panel       = PdfChromePanel.None;
        private PdfSearchMode   _searchMode  = PdfSearchMode.Fuzzy;

        private bool _showPanelToggles = true;
        private bool _showPageControls = true;
        private bool _showZoom         = true;
        private bool _showFitModes     = true;
        private bool _showRotate       = true;
        private bool _showSpread       = true;
        private bool _showSearch       = true;
        private bool _showDocumentName = true;
        private bool _showOutlineTab   = true;
        private bool _showThumbnailTab = true;

        private string   _documentName;
        private double[] _zoomLevels = { 0.5, 1, 2, 4 };
        private bool     _border;

        private Action<PdfChromePanel> _onPanelChanged;
        private Action<PdfSearchMode>  _onSearchModeChanged;

        /* -------------------------------------------------------------- live state */

        private int    _page      = 0;
        private int    _pageCount = 0;
        private double _scale     = 0;
        private string _scalePreset;
        private SpreadMode _spreadMode = SpreadMode.None;

        internal PdfViewerChrome(bool singlePage)
        {
            PdfChromeStyles.Ensure();

            _viewer = new PdfViewer(singlePage);

            _root           = Box(PdfChromeStyles.ROOT);
            _body           = Box("tsspdf-body");
            _view           = Box("tsspdf-view");
            _viewerElement  = _viewer.Render();

            _view.appendChild(_viewerElement);
            _body.appendChild(_view);
            _root.appendChild(_body);

            // The chrome measures itself rather than the window: it is a component, and a 1200px
            // window can hold it in a 380px pane. A ResizeObserver on its own box is the only thing
            // that knows which.
            _sizeObserver = new ResizeObserver((_1, _2) => ApplyWidthClass());

            _sizeObserver.observe(_root);

            // ⌘F / Ctrl+F while the focus is anywhere inside the chrome. Bound on the root rather
            // than on the document because a page can hold more than one of these, and the one the
            // reader is using is the one their focus is in - pdf.js's scroll host takes focus as soon
            // as the document is clicked or scrolled, so that is nearly always true.
            _root.addEventListener("keydown", new Action<KeyboardEvent>(HandleRootKeyDown));

            BuildChrome();
            Subscribe();
        }

        /* ---------------------------------------------------------------- rendering */

        public HTMLElement Render() => _root;

        /// <summary>The root element - what the Tesserae sizing helpers write to.</summary>
        public HTMLElement StylingContainer => _root;

        /// <summary>
        /// Kept here rather than hoisted onto a wrapper, for the reason <see cref="PdfComponent"/>
        /// gives: the height has to land on the element the viewer measures through, and hoisting it
        /// leaves the chrome - and so the viewer inside it - with none.
        /// </summary>
        public bool PropagateStylesToWrapper => false;

        /// <summary>
        /// The viewer underneath, with its whole surface. Everything the chrome does goes through
        /// this, so anything it does not offer can be done here instead.
        /// </summary>
        public PdfViewer Viewer => _viewer;

        /// <summary>The loaded document, or null.</summary>
        public PdfDocument Document => _viewer.Document;

        /// <summary>The page in view, 1-based.</summary>
        public int Page => _page > 0 ? _page : _viewer.Page;

        /// <summary>How many pages the document has.</summary>
        public int PageCount => _viewer.PageCount;

        /// <summary>Which side panel is showing.</summary>
        public PdfChromePanel CurrentPanel => _panel;

        /// <summary>How strictly the search box is matching.</summary>
        public PdfSearchMode CurrentSearchMode => _searchMode;

        /* ---------------------------------------------------------------- document */

        /// <summary>
        /// Shows the document at a URL. Also names the chrome's title from the URL's last segment,
        /// unless <see cref="DocumentName"/> has already been given one.
        /// </summary>
        public PdfViewerChrome Url(string url)
        {
            if (_documentName is null) SetDocumentName(NameFromUrl(url), fromHost: false);

            _viewer.Url(url);

            return this;
        }

        /// <summary>Shows a document already in memory. See <see cref="PdfSource.FromBytes"/>.</summary>
        public PdfViewerChrome Data(byte[] bytes)
        {
            _viewer.Data(bytes);

            return this;
        }

        /// <summary>Shows a document from any of the sources <see cref="PdfSource"/> describes.</summary>
        public PdfViewerChrome Source(PdfSource source)
        {
            _viewer.Source(source);

            return this;
        }

        /// <summary>Lets go of the document and empties the chrome with it.</summary>
        public PdfViewerChrome Clear()
        {
            _viewer.Clear();

            ResetDocumentState();

            return this;
        }

        /// <summary>
        /// Reaches the viewer for anything this surface does not cover - <c>Options</c>,
        /// <c>OnPassword</c>, <c>EnableScripting</c>, the annotation editor. Returns the chrome, so it
        /// chains.
        /// </summary>
        public PdfViewerChrome Configure(Action<PdfViewer> configure)
        {
            configure?.Invoke(_viewer);

            return this;
        }

        /* ------------------------------------------------------------------- looks */

        /// <summary>Where the controls sit. See <see cref="PdfChromeLayout"/>.</summary>
        public PdfViewerChrome Layout(PdfChromeLayout layout)
        {
            if (_layout == layout) return this;

            _layout = layout;

            BuildChrome();

            return this;
        }

        /// <summary>
        /// The name shown beside the file glyph in <see cref="PdfChromeLayout.IconRail"/>. Taken from
        /// the URL when <see cref="Url"/> is used and not set here.
        /// </summary>
        public PdfViewerChrome DocumentName(string name)
        {
            SetDocumentName(name, fromHost: true);

            return this;
        }

        /// <summary>How wide the side panel is, in pixels. 264 by default.</summary>
        public PdfViewerChrome PanelWidth(int pixels)
        {
            _root.style.setProperty("--tsspdf-panel-width", pixels + "px");

            return this;
        }

        /// <summary>
        /// How wide the search box is, in pixels. 430 by default, and it shrinks below that before
        /// anything else in the toolbar does.
        /// </summary>
        public PdfViewerChrome SearchWidth(int pixels)
        {
            _root.style.setProperty("--tsspdf-search-width", pixels + "px");

            return this;
        }

        /// <summary>
        /// Draws a 1px frame with rounded corners around the whole chrome, in the theme's border
        /// colour. Off by default.
        ///
        /// <b>Why it is not the default.</b> The chrome's own lines are all internal - under the
        /// toolbar, beside the panel - because the usual host already draws the outer one: filling a
        /// window there is nothing to frame, and inside a modal or a Tesserae <c>Card</c> a second
        /// line just inside the container's own reads as a seam. This is for the other case: a chrome
        /// sitting on a page's background with nothing else to say where the document ends.
        ///
        /// The corner radius follows <see cref="CornerRadius"/>, and the root clips to it, so the
        /// toolbar and the panel are rounded off by the frame rather than needing to know about it.
        /// </summary>
        public PdfViewerChrome Border(bool show = true)
        {
            _border = show;

            Toggle(_root, PdfChromeStyles.BORDERED, show);

            return this;
        }

        /// <summary>
        /// How round the corners of <see cref="Border"/> are, in pixels. 6 by default - the radius
        /// the chrome's own controls use. 0 squares the frame off.
        /// </summary>
        public PdfViewerChrome CornerRadius(int pixels)
        {
            _root.style.setProperty("--tsspdf-radius", pixels + "px");

            return this;
        }

        /// <summary>Whether the frame is drawn. See <see cref="Border"/>.</summary>
        public bool HasBorder => _border;

        /* -------------------------------------------------------- what is on show */

        /// <summary>Whether the outline and thumbnails toggles are drawn.</summary>
        public PdfViewerChrome ShowPanelToggles(bool show = true) => Rebuilt(() => _showPanelToggles = show);

        /// <summary>Whether the previous/next page buttons and the page box are drawn.</summary>
        public PdfViewerChrome ShowPageControls(bool show = true) => Rebuilt(() => _showPageControls = show);

        /// <summary>Whether the zoom stepper and its menu are drawn.</summary>
        public PdfViewerChrome ShowZoom(bool show = true) => Rebuilt(() => _showZoom = show);

        /// <summary>Whether the <c>Fit page | Fit content</c> control is drawn.</summary>
        public PdfViewerChrome ShowFitModes(bool show = true) => Rebuilt(() => _showFitModes = show);

        /// <summary>Whether the rotate button is drawn.</summary>
        public PdfViewerChrome ShowRotate(bool show = true) => Rebuilt(() => _showRotate = show);

        /// <summary>Whether the two-page-spread toggle is drawn.</summary>
        public PdfViewerChrome ShowSpread(bool show = true) => Rebuilt(() => _showSpread = show);

        /// <summary>Whether the search box is drawn. Also decides whether ⌘F / Ctrl+F does anything.</summary>
        public PdfViewerChrome ShowSearch(bool show = true) => Rebuilt(() => _showSearch = show);

        /// <summary>
        /// Whether the document's name is drawn. Only <see cref="PdfChromeLayout.IconRail"/> has
        /// somewhere to put it; the single toolbar spends that space on controls.
        /// </summary>
        public PdfViewerChrome ShowDocumentName(bool show = true) => Rebuilt(() => _showDocumentName = show);

        /// <summary>
        /// Which panel tabs exist. Turning one off also stops its toolbar toggle being drawn, and
        /// closes the panel if that was the tab showing.
        /// </summary>
        public PdfViewerChrome Tabs(bool outline = true, bool thumbnails = true)
        {
            _showOutlineTab   = outline;
            _showThumbnailTab = thumbnails;

            if (_panel == PdfChromePanel.Outline    && !outline)    _panel = PdfChromePanel.None;
            if (_panel == PdfChromePanel.Thumbnails && !thumbnails) _panel = PdfChromePanel.None;

            BuildChrome();

            return this;
        }

        /// <summary>
        /// The fixed zoom steps the zoom menu offers, as factors where 1 is 100%. Defaults to
        /// 50%, 100%, 200% and 400%. The three fit modes above them are always there.
        /// </summary>
        public PdfViewerChrome ZoomLevels(params double[] levels)
        {
            if (levels is object && levels.Length > 0) _zoomLevels = levels;

            return this;
        }

        private PdfViewerChrome Rebuilt(Action change)
        {
            change();

            BuildChrome();

            return this;
        }

        /* ------------------------------------------------------------------- panel */

        /// <summary>
        /// Opens a panel, or closes it with <see cref="PdfChromePanel.None"/>.
        ///
        /// Closing takes the panel's elements out of the DOM, which tears down the thumbnail
        /// canvases with them - deliberately: a 200-page rail is 200 renders to keep warm for a panel
        /// nobody is looking at. Reopening rebuilds what is in view.
        /// </summary>
        public PdfViewerChrome Panel(PdfChromePanel panel)
        {
            if (panel == PdfChromePanel.Outline    && !_showOutlineTab)   panel = PdfChromePanel.None;
            if (panel == PdfChromePanel.Thumbnails && !_showThumbnailTab) panel = PdfChromePanel.None;

            if (_panel == panel) return this;

            var wasOpen = _panel != PdfChromePanel.None;

            _panel = panel;

            // Switching pane swaps what is in the panel; opening and closing is what changes whether
            // there is a panel at all. Kept apart so a switch does not take the panel out of the DOM
            // and put an identical one back.
            if (wasOpen && panel != PdfChromePanel.None)
            {
                ShowPanelPane();
            }
            else
            {
                BuildPanel();
            }

            UpdateToolbarState();

            _onPanelChanged?.Invoke(_panel);

            return this;
        }

        /// <summary>Opens a panel if it is closed, closes it if that panel is already showing.</summary>
        public PdfViewerChrome TogglePanel(PdfChromePanel panel)
            => Panel(_panel == panel ? PdfChromePanel.None : panel);

        /// <summary>Called whenever the panel opens, closes or switches tab.</summary>
        public PdfViewerChrome OnPanelChanged(Action<PdfChromePanel> handler)
        {
            _onPanelChanged = handler;

            return this;
        }

        /* ---------------------------------------------------------------- keyboard */

        private void HandleRootKeyDown(KeyboardEvent e)
        {
            if (!_showSearch || e is null) return;

            // Not a shortcut worth intercepting if the reader is holding Alt or Shift as well: those
            // are other people's chords.
            if (!(e.ctrlKey || e.metaKey) || e.altKey || e.shiftKey) return;

            if (e.key != "f" && e.key != "F") return;

            e.preventDefault();

            FocusSearch();
        }

        /* ---------------------------------------------------------------- assembly */

        /// <summary>
        /// (Re)builds everything except the viewer: the toolbar, the rail and the panel.
        ///
        /// <b>The view element is never moved.</b> It is the last child of the body and stays there,
        /// because taking it out of the DOM - even to put it straight back - is a teardown as far as
        /// the viewer's mount observer is concerned, and would drop the document on a change of
        /// layout. Everything else is a sibling inserted before it.
        /// </summary>
        private void BuildChrome()
        {
            if (_toolbarElement is object && _toolbarElement.parentElement is object)
            {
                _toolbarElement.parentElement.removeChild(_toolbarElement);
            }

            if (_railElement is object && _railElement.parentElement is object)
            {
                _railElement.parentElement.removeChild(_railElement);
            }

            _toolbar        = _layout == PdfChromeLayout.IconRail ? BuildSplitToolbar() : BuildSingleToolbar();
            _toolbarElement = _toolbar.Render();
            _rail           = null;
            _railElement    = null;

            _root.insertBefore(_toolbarElement, _body);

            if (_layout == PdfChromeLayout.IconRail)
            {
                _rail        = BuildRail();
                _railElement = _rail.Render();

                _body.insertBefore(_railElement, _body.firstChild);
            }

            BuildPanel();
            UpdateToolbarState();
            UpdatePageState();
            UpdateSearchState();
            UpdateOverflowState();
        }

        /* -------------------------------------------------------------- event wiring */

        /// <summary>
        /// Listens on pdf.js's event bus rather than through the viewer's own <c>On*</c> callbacks.
        ///
        /// Those are single slots - a second <c>OnPageChanged</c> replaces the first - so a chrome
        /// that used them would silently take them away from the host. The bus is multicast, and it is
        /// rebuilt with the viewer, so the subscriptions below go away with it and are replayed by
        /// <c>Configure</c> on the next mount. Which is also why this is the only safe place to read
        /// the state the chrome shows: it is told, rather than polling.
        /// </summary>
        private void Subscribe()
        {
            _viewer.Configure(_ =>
            {
                var events = _viewer.Events;

                if (events is null) return;

                // A new bus means a new document, or the same one after a remount. Either way what
                // the panel is showing belongs to the old one.
                ResetDocumentState();

                events.on(PdfViewerEvents.PagesInit, new Action<object>(_2 => HandlePagesInit()));

                events.on(PdfViewerEvents.PageChanging, new Action<object>(data =>
                {
                    var changed = (IPageChangingEvent)data;

                    _page = changed.pageNumber;

                    UpdatePageState();
                    HighlightCurrentPage();
                }));

                events.on(PdfViewerEvents.ScaleChanging, new Action<object>(data =>
                {
                    var changed = (IScaleChangingEvent)data;

                    _scale       = changed.scale;
                    _scalePreset = changed.presetValue;

                    UpdateZoomState();
                }));

                // The document's own page numbering arrives after the document does, so the page box
                // has to be told to look again - see PdfViewerEvents.PageLabelsApplied.
                events.on(PdfViewerEvents.PageLabelsApplied, new Action<object>(data =>
                    ApplyPageLabels(((IPageLabelsEvent)data).pageLabels)));

                events.on(PdfViewerEvents.SpreadModeChanged, new Action<object>(data =>
                {
                    var changed = (ILayoutModeChangedEvent)data;

                    _spreadMode = (SpreadMode)changed.mode;

                    UpdateToolbarState();
                }));

                events.on(PdfViewerEvents.UpdateFindMatchesCount, new Action<object>(data =>
                {
                    var counted = (IUpdateFindMatchesCountEvent)data;

                    ApplyMatches(counted.matchesCount, carriesState: false, state: FindState.Pending);
                }));

                events.on(PdfViewerEvents.UpdateFindControlState, new Action<object>(data =>
                {
                    var outcome = (IUpdateFindControlStateEvent)data;

                    ApplyMatches(outcome.matchesCount, carriesState: true, state: (FindState)outcome.state);
                }));
            });
        }

        private void HandlePagesInit()
        {
            _pageCount = _viewer.PageCount;
            _page      = _viewer.Page;

            var instance = _viewer.ViewerInstance;

            if (instance is object) _spreadMode = (SpreadMode)instance.spreadMode;

            UpdatePageState();
            UpdateToolbarState();

            LoadOutlineAsync().FireAndForget();
            MeasureThumbnailAspectAsync().FireAndForget();

            BuildPanel();
        }

        private void ResetDocumentState()
        {
            _pageCount         = 0;
            _page              = 0;
            _documentHasLabels = false;

            ResetOutline();
            ResetThumbnails();
            ResetSearchResults();

            UpdatePageState();
        }

        /* ---------------------------------------------------------------- teardown */

        /// <summary>
        /// Releases the chrome and the viewer inside it for good. Leaving the DOM does <b>not</b> do
        /// this - that tears the view down and re-arms it, as <see cref="PdfComponent"/> describes -
        /// so call it when the chrome is genuinely finished with.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            ResetThumbnails();

            if (_sizeObserver is object)
            {
                _sizeObserver.disconnect();

                _sizeObserver = null;
            }

            _viewer.Dispose();
        }

        private bool _disposed;

        /// <summary>Whether <see cref="Dispose"/> has been called.</summary>
        public bool IsDisposed => _disposed;

        /* -------------------------------------------------------------- responsive */

        /// <summary>
        /// Puts the current width band on the root, and moves whatever that band takes out of the
        /// toolbar into the overflow menu.
        /// </summary>
        private void ApplyWidthClass()
        {
            var width = _root.getBoundingClientRect().As<DOMRect>().width;

            // 0 while the chrome is detached or inside a collapsed parent, which is not a band.
            if (width <= 0) return;

            var band = width < MINI_WIDTH   ? "tsspdf-mini"
                     : width < TIGHT_WIDTH  ? "tsspdf-tight"
                     : width < NARROW_WIDTH ? "tsspdf-narrow"
                     : "";

            if (band == _widthClass) return;

            if (_widthClass.Length > 0) _root.classList.remove(_widthClass);

            _widthClass = band;

            if (band.Length > 0) _root.classList.add(band);

            UpdateOverflowState();
        }

        /// <summary>
        /// Whether the fit modes need a home in the overflow menu.
        ///
        /// They are not on the toolbar any more - they are the first two entries of the zoom menu - so
        /// the question is whether that menu is reachable. It is not when the band has taken the zoom
        /// stepper away, and it never is when the host turned zoom off, and in both cases the fit
        /// modes would otherwise be unreachable.
        /// </summary>
        private bool FitModesInOverflow => !_showZoom || ZoomInOverflow;

        /// <summary>Whether the band in force has taken rotate and spread out of the toolbar.</summary>
        private bool ViewControlsInOverflow => _widthClass == "tsspdf-mini";

        /// <summary>Whether the band in force has taken the Fuzzy | Precise pill out of the search row.</summary>
        private bool SearchModeInOverflow => _widthClass == "tsspdf-tight" || _widthClass == "tsspdf-mini";

        /// <summary>Whether the band in force has taken the zoom stepper out of the toolbar.</summary>
        private bool ZoomInOverflow => _widthClass == "tsspdf-mini";

        /// <summary>
        /// The overflow button appears exactly when something has been taken out of the toolbar, and
        /// its menu is built on open from whatever that is.
        ///
        /// On the rail layout it never appears: the rail has room for everything, which is the reason
        /// that layout exists.
        /// </summary>
        private void UpdateOverflowState()
        {
            if (_overflowButton is null) return;

            var needed = _layout == PdfChromeLayout.SingleToolbar
                      && ((_showZoom && ZoomInOverflow)
                       || (_showFitModes && FitModesInOverflow)
                       || ((_showRotate || _showSpread) && ViewControlsInOverflow)
                       || (_showSearch && SearchModeInOverflow));

            PdfChromeElements.Show(_overflowButton, needed);
        }

        /* ----------------------------------------------------------------- helpers */

        private void SetDocumentName(string name, bool fromHost)
        {
            if (fromHost) _documentName = name;

            _effectiveDocumentName = name;

            if (_documentNameText is null) return;

            _documentNameText.Text          = name ?? "";
            _documentNameText.Render().title = name ?? "";
        }

        private string _effectiveDocumentName;

        /// <summary>
        /// The last path segment of a URL, percent-decoded, or null when there is nothing name-shaped
        /// in it. Query and fragment are dropped first, so <c>/files/42.pdf?token=...</c> is
        /// <c>42.pdf</c> rather than the whole thing.
        /// </summary>
        private static string NameFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            var text = url;
            var cut  = text.IndexOf('#');

            if (cut >= 0) text = text.Substring(0, cut);

            cut = text.IndexOf('?');

            if (cut >= 0) text = text.Substring(0, cut);

            cut = text.LastIndexOf('/');

            if (cut >= 0) text = text.Substring(cut + 1);

            if (string.IsNullOrEmpty(text)) return null;

            try
            {
                return es5.decodeURIComponent(text);
            }
            catch (Exception)
            {
                // A stray % in a filename is not worth failing a document over.
                return text;
            }
        }
    }
}
