using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Transpose;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Pdf
{
    /// <summary>
    /// A scrollable, searchable, linkable PDF viewer - pdf.js's viewer components, as a Tesserae
    /// component.
    ///
    /// <code>
    /// PdfJs.Viewer()
    ///    .Url("/api/files/42.pdf")
    ///    .FitWidth()
    ///    .OnPageChanged(page => label.Text = $"Page {page}")
    /// </code>
    ///
    /// <b>What it builds.</b> One event bus, one link service, one find controller and one viewer,
    /// wired to each other, inside a scroll host of the shape pdf.js insists on. Everything else -
    /// the toolbar, the page indicator, the search box, the outline sidebar - is the host's, because
    /// those are the parts that have to look like the rest of your application. The methods here are
    /// what such a toolbar calls.
    ///
    /// <b>Configuration splits three ways</b>, and which one a setting falls into is decided by
    /// pdf.js rather than by taste:
    /// <list type="bullet">
    /// <item><see cref="Options"/> is for what pdf.js only reads when the viewer is constructed.
    /// There is no <c>updateOptions</c>, so changing one of those means rebuilding, and the mutators
    /// are recorded and re-run on every remount.</item>
    /// <item>The page, zoom, rotation and layout setters work at any time: before the viewer exists
    /// they are remembered, and applied when it does.</item>
    /// <item><see cref="Configure"/> takes a callback against the live viewer, recorded and replayed
    /// after every rebuild, for anything this surface does not cover.</item>
    /// </list>
    ///
    /// <b>Lifetime.</b> The viewer owns the document it opened and releases it on teardown, including
    /// the teardown that happens when the component leaves the DOM. Being re-added rebuilds
    /// everything and restores the page, zoom, rotation and layout it had - see
    /// <see cref="PdfComponent"/>.
    /// </summary>
    public sealed class PdfViewer : PdfComponent
    {
        private readonly bool _singlePage;

        private readonly List<Action<IPdfViewerInstance>> _configured    = new List<Action<IPdfViewerInstance>>();
        private readonly List<Action<PdfViewerOptions>>   _optionSetters = new List<Action<PdfViewerOptions>>();

        private IPdfViewerInstance _viewer;
        private EventBus           _events;
        private PdfLinkServiceJs   _linkService;
        private PdfFindControllerJs _findController;
        private PdfScriptingManagerJs _scriptingManager;

        private IPdfDocumentLoadingTask _loadingTask;
        private PdfDocument             _document;
        private PdfSource               _source;

        // Bumped every time a document is asked for, so a load that resolves after the viewer has
        // moved on can tell that it has and drop its result. Without it, opening B while A is still
        // loading shows A.
        private int _generation;

        // Restored at pagesinit, which is the only point the viewer can accept them.
        private string     _scaleValue = "auto";
        private int        _page       = 1;
        private int        _rotation;
        private ScrollMode _scrollMode = ScrollMode.Vertical;
        private SpreadMode _spreadMode = SpreadMode.None;

        private string _lastScalePreset = "auto";
        private bool   _keepFitOnResize = true;
        private bool   _scriptingEnabled;
        private bool   _useOwnL10n = true;
        private object _customL10n;

        // Whether the viewer was constructed with the editor layer. pdf.js only builds its editor
        // machinery then, so this decides whether a runtime mode change is possible at all.
        private bool _annotationEditorEnabled;

        private TextLayerMode        _textLayerMode        = TextLayerMode.Enable;
        private AnnotationMode       _annotationMode       = AnnotationMode.EnableForms;
        private AnnotationEditorMode _annotationEditorMode = AnnotationEditorMode.Disable;

        private Action<PdfDocument>  _onDocumentLoaded;
        private Action<int>          _onPageChanged;
        private Action<double>       _onScaleChanged;
        private Action<int>          _onRotationChanged;
        private Action<int>          _onPageRendered;
        private Action<double, double> _onProgress;
        private Action<PdfError>     _onError;
        private Action<PdfSearchResult> _onSearchResults;
        private Action               _onSandboxCreated;
        private Action<AnnotationEditorMode> _onAnnotationEditorModeChanged;
        private Func<PasswordReason, Task<string>> _onPassword;

        private HTMLElement _scrollHost;
        private HTMLElement _pagesHost;

        internal PdfViewer(bool singlePage)
        {
            _singlePage = singlePage;
        }

        /// <summary>The live pdf.js viewer, or null before the component has mounted.</summary>
        public IPdfViewerInstance ViewerInstance => _viewer;

        /// <summary>
        /// The event bus the whole stack shares, for events this surface does not wrap. Null before
        /// the component has mounted.
        /// </summary>
        public EventBus Events => _events;

        /// <summary>
        /// The link service, for resolving destinations directly. Null before the component has
        /// mounted.
        /// </summary>
        public PdfLinkServiceJs LinkService => _linkService;

        /// <summary>
        /// The find controller, for reading which match is selected. Null before the component has
        /// mounted. Note a search is <b>started</b> by dispatching on the event bus, not through
        /// this - see <see cref="Search(string, FindOptions)"/>.
        /// </summary>
        public PdfFindControllerJs FindController => _findController;

        /// <summary>
        /// The scripting manager, for the save and print hooks a document's JavaScript can react to.
        /// Null unless <see cref="EnableScripting"/> was called.
        /// </summary>
        public PdfScriptingManagerJs ScriptingManager => _scriptingManager;

        /// <summary>The document being shown, or null before one has loaded.</summary>
        public PdfDocument Document => _document;

        /// <summary>How many pages the document has, or 0 before one has loaded.</summary>
        public int PageCount => _document is object ? _document.PageCount : 0;

        /// <summary>The page in view, 1-based. Settable before the viewer exists.</summary>
        public int Page
        {
            get => _viewer is object ? _viewer.currentPageNumber : _page;
            set
            {
                _page = value;

                if (_viewer is object) _viewer.currentPageNumber = value;
            }
        }

        /// <summary>The zoom as a number, where 1 is 100%. 0 before the viewer exists.</summary>
        public double Scale => _viewer is object ? _viewer.currentScale : 0;

        /// <summary>
        /// The zoom as pdf.js's string form - a number, or <c>"auto"</c> / <c>"page-width"</c> /
        /// <c>"page-fit"</c> / <c>"page-height"</c> / <c>"page-actual"</c>.
        /// </summary>
        public string ScaleValue => _viewer is object ? _viewer.currentScaleValue : _scaleValue;

        /* ------------------------------------------------------------------ source */

        /// <summary>Shows the document at a URL. Replaces whatever is showing.</summary>
        public PdfViewer Url(string url) => Source(PdfSource.FromUrl(url));

        /// <summary>Shows a document already in memory.</summary>
        public PdfViewer Data(byte[] bytes) => Source(PdfSource.FromBytes(bytes));

        /// <summary>
        /// Shows a document. Everything about how it is fetched - password, headers, range behaviour
        /// - is on the <see cref="PdfSource"/>.
        ///
        /// Safe to call before the component has mounted, and safe to call again while a previous
        /// document is still loading: the earlier load is abandoned rather than raced.
        /// </summary>
        public PdfViewer Source(PdfSource source)
        {
            _source = source;

            if (IsCreated) OpenDocumentAsync(source).FireAndForget();

            return this;
        }

        /// <summary>Lets go of the document without tearing the viewer down.</summary>
        public PdfViewer Clear()
        {
            _source = null;

            if (IsCreated) OpenDocumentAsync(null).FireAndForget();

            return this;
        }

        /* ------------------------------------------------------------- navigation */

        /// <summary>Goes to a page, 1-based.</summary>
        public PdfViewer GoToPage(int pageNumber)
        {
            Page = pageNumber;

            return this;
        }

        /// <summary>Goes to the next page.</summary>
        public PdfViewer NextPage()
        {
            _viewer?.nextPage();

            return this;
        }

        /// <summary>Goes to the previous page.</summary>
        public PdfViewer PreviousPage()
        {
            _viewer?.previousPage();

            return this;
        }

        /// <summary>
        /// Goes to the page carrying a label, for a document that numbers its pages its own way
        /// ("iv", "A-3"). Does nothing when no page carries it.
        /// </summary>
        public PdfViewer GoToPageLabel(string label)
        {
            if (_viewer is object && !string.IsNullOrEmpty(label)) _viewer.currentPageLabel = label;

            return this;
        }

        /// <summary>
        /// Goes to a destination - the value on a <see cref="PdfOutlineItem"/>, or a named
        /// destination's name. Both forms are pdf.js's own and are passed through untouched.
        /// </summary>
        public PdfViewer GoToDestination(object destination)
        {
            if (_linkService is object && destination is object) _linkService.goToDestination(destination);

            return this;
        }

        /// <summary>Goes to a named destination, e.g. one from <c>GetNamedDestinationsAsync</c>.</summary>
        public PdfViewer GoToNamedDestination(string name) => GoToDestination(name);

        /* ------------------------------------------------------------------- zoom */

        /// <summary>Fits the page's width to the container, and keeps doing so as it resizes.</summary>
        public PdfViewer FitWidth() => SetScaleValue("page-width");

        /// <summary>Fits the whole page in the container.</summary>
        public PdfViewer FitPage() => SetScaleValue("page-fit");

        /// <summary>Fits the page's height to the container.</summary>
        public PdfViewer FitHeight() => SetScaleValue("page-height");

        /// <summary>Shows the page at its paper size, ignoring the container.</summary>
        public PdfViewer ActualSize() => SetScaleValue("page-actual");

        /// <summary>
        /// pdf.js's own default: fits the width on a narrow container and shows actual size on a
        /// wide one.
        /// </summary>
        public PdfViewer AutoZoom() => SetScaleValue("auto");

        /// <summary>Sets an explicit zoom, where 1 is 100%.</summary>
        public PdfViewer Zoom(double scale) => SetScaleValue(scale.ToString());

        /// <summary>Zooms in one step.</summary>
        public PdfViewer ZoomIn(double factor = 1.1)
        {
            _viewer?.increaseScale(new ScaleChangeParameters { scaleFactor = factor });

            return this;
        }

        /// <summary>Zooms out one step.</summary>
        public PdfViewer ZoomOut(double factor = 1.1)
        {
            _viewer?.decreaseScale(new ScaleChangeParameters { scaleFactor = factor });

            return this;
        }

        /// <summary>
        /// Whether a fit mode is re-applied when the container resizes. On by default.
        ///
        /// pdf.js resolves <c>page-width</c> and its siblings once, into a number, so without this a
        /// viewer that fitted its width when it was 600px wide keeps that zoom in a 1200px pane. Only
        /// the named presets are re-applied - an explicit zoom the user chose is left alone.
        /// </summary>
        public PdfViewer KeepFitOnResize(bool keep = true)
        {
            _keepFitOnResize = keep;

            return this;
        }

        private PdfViewer SetScaleValue(string scaleValue)
        {
            _scaleValue = scaleValue;

            // Remembered separately from _scaleValue, which tracks whatever pdf.js last reported -
            // including the number a preset resolved to. Only a preset is worth re-applying.
            if (IsPreset(scaleValue)) _lastScalePreset = scaleValue;
            else                      _lastScalePreset = null;

            if (_viewer is object) _viewer.currentScaleValue = scaleValue;

            return this;
        }

        private static bool IsPreset(string scaleValue)
        {
            return scaleValue == "auto" || scaleValue == "page-width" || scaleValue == "page-fit"
                || scaleValue == "page-height" || scaleValue == "page-actual";
        }

        /* --------------------------------------------------------------- rotation */

        /// <summary>Rotates every page a quarter turn clockwise.</summary>
        public PdfViewer Rotate() => Rotation(_rotation + 90);

        /// <summary>Rotates every page a quarter turn anticlockwise.</summary>
        public PdfViewer RotateBack() => Rotation(_rotation - 90);

        /// <summary>Sets the rotation applied to every page, in degrees. Normalised to 0, 90, 180 or 270.</summary>
        public PdfViewer Rotation(int degrees)
        {
            // pdf.js accepts any multiple of 90 but reports it normalised, so normalising here keeps
            // the remembered value and the reported one the same.
            _rotation = ((degrees % 360) + 360) % 360;

            if (_viewer is object) _viewer.pagesRotation = _rotation;

            return this;
        }

        /* ----------------------------------------------------------------- layout */

        /// <summary>How pages are laid out.</summary>
        public PdfViewer Scroll(ScrollMode mode)
        {
            _scrollMode = mode;

            if (_viewer is object) _viewer.scrollMode = (int)mode;

            return this;
        }

        /// <summary>Whether pages are shown in pairs.</summary>
        public PdfViewer Spread(SpreadMode mode)
        {
            _spreadMode = mode;

            if (_viewer is object) _viewer.spreadMode = (int)mode;

            return this;
        }

        /// <summary>
        /// Whether text is selectable, and whether the document's permissions get a say. Read when
        /// the viewer is built.
        /// </summary>
        public PdfViewer TextSelection(TextLayerMode mode)
        {
            _textLayerMode = mode;

            return this;
        }

        /// <summary>
        /// How much of the annotation layer to build. Read when the viewer is built.
        ///
        /// Leave this alone unless you want links without form fields
        /// (<see cref="AnnotationMode.Enable"/>) or nothing at all
        /// (<see cref="AnnotationMode.Disable"/>). In particular <b>do not reach for
        /// <see cref="AnnotationMode.EnableStorage"/> here</b>: despite its name it makes a viewer's
        /// form non-interactive, silently - see the remarks on <see cref="AnnotationMode"/>. The
        /// default, <see cref="AnnotationMode.EnableForms"/>, is the one that both makes fields
        /// editable and keeps what is typed into them.
        /// </summary>
        public PdfViewer Annotations(AnnotationMode mode)
        {
            _annotationMode = mode;

            return this;
        }

        /// <summary>
        /// Which annotation-editing tool is active - what a "highlight" or "add note" button in a
        /// toolbar sets.
        ///
        /// <b>Whether there is an editor at all is decided before the viewer is built.</b> Call this
        /// with anything other than <see cref="AnnotationEditorMode.Disable"/> while configuring the
        /// component to build the editor layer; afterwards, tools can be switched freely but
        /// <see cref="AnnotationEditorMode.Disable"/> cannot be set and neither can any tool on a
        /// viewer that was built without the editor. <see cref="AnnotationEditorMode.None"/> is the
        /// runtime way to mean "no tool active".
        ///
        /// Both limits are pdf.js's: it creates its editor machinery only when constructed with the
        /// editor enabled, and its own setter rejects Disable outright. Attempting either here throws
        /// a message that says so, rather than pdf.js's "The AnnotationEditor is not enabled."
        /// </summary>
        public PdfViewer AnnotationEditor(AnnotationEditorMode mode)
        {
            if (!IsCreated)
            {
                // Before the viewer exists this is the construction option, and Disable is a valid
                // value for it - it is what leaves the editor layer out.
                _annotationEditorMode = mode;

                return this;
            }

            if (!_annotationEditorEnabled)
            {
                throw new InvalidOperationException(
                    "This viewer was built without the annotation editor, so its mode cannot be changed. " +
                    "Call AnnotationEditor(AnnotationEditorMode.None) before the component is mounted to build the editor layer.");
            }

            if (mode == AnnotationEditorMode.Disable)
            {
                throw new InvalidOperationException(
                    "AnnotationEditorMode.Disable removes the editor layer and can only be set before the viewer is built. " +
                    "Use AnnotationEditorMode.None to deactivate the current tool.");
            }

            _annotationEditorMode = mode;

            // Asymmetric on the pdf.js side: the getter answers with an object carrying a mode, the
            // setter takes one.
            _viewer.annotationEditorMode = new AnnotationEditorModeChange { mode = (int)mode };

            return this;
        }

        /// <summary>Whether this viewer was built with pdf.js's annotation editor layer.</summary>
        public bool IsAnnotationEditorEnabled => _annotationEditorEnabled;

        /// <summary>The annotation editor's active tool.</summary>
        public AnnotationEditorMode CurrentAnnotationEditorMode
        {
            get
            {
                if (_viewer is null) return _annotationEditorMode;

                var current = (IAnnotationEditorModeChangedEvent)_viewer.annotationEditorMode;

                return current is null ? _annotationEditorMode : (AnnotationEditorMode)current.mode;
            }
        }

        /* ------------------------------------------------------------------ search */

        /// <summary>
        /// Searches the document and highlights every match, selecting the first.
        ///
        /// Results arrive through <see cref="OnSearchResults"/> rather than being returned: pdf.js
        /// reads the pages as it goes and reports a running count, so a long document has an answer
        /// before it has a total.
        /// </summary>
        public PdfViewer Search(string query, FindOptions options = null) => Find(query, options, type: "");

        /// <summary>
        /// Searches for several terms at once, each matched independently. What a space-separated
        /// search box wants.
        /// </summary>
        public PdfViewer Search(string[] terms, FindOptions options = null)
        {
            // Script.ToArray strips the $type a C# array carries. The find controller hands the query
            // to the page-text machinery, and a typed array survives that - but the same query is
            // also compared and re-dispatched, and an array that is not a plain one has already
            // caused enough trouble elsewhere in this package to be worth normalising here.
            return Find(terms is null ? null : Script.ToArray(terms), options, type: "");
        }

        /// <summary>Moves to the next match of the current search.</summary>
        public PdfViewer FindNext() => Find(_lastQuery, _lastOptions, type: "again", findPrevious: false);

        /// <summary>Moves to the previous match of the current search.</summary>
        public PdfViewer FindPrevious() => Find(_lastQuery, _lastOptions, type: "again", findPrevious: true);

        /// <summary>Drops the highlights and forgets the search.</summary>
        public PdfViewer ClearSearch()
        {
            _lastQuery     = null;
            _lastFindState = FindState.Pending;

            _events?.dispatch(PdfViewerEvents.FindBarClose, new FindEventPayload());

            return this;
        }

        private object      _lastQuery;
        private FindOptions _lastOptions;
        private FindState   _lastFindState = FindState.Pending;

        private PdfViewer Find(object query, FindOptions options, string type, bool findPrevious = false)
        {
            if (_events is null || query is null) return this;

            _lastQuery   = query;
            _lastOptions = options;

            // A fresh search has no outcome yet; without this a second search reports the first
            // one's result while it is still running.
            if (type != "again") _lastFindState = FindState.Pending;

            var effective = options ?? new FindOptions();

            var payload = new FindEventPayload
            {
                type            = type,
                query           = query,
                caseSensitive   = effective.CaseSensitive,
                entireWord      = effective.EntireWord,
                highlightAll    = effective.HighlightAll,
                findPrevious    = findPrevious,
                matchDiacritics = effective.MatchDiacritics,
            };

            _events.dispatch(PdfViewerEvents.Find, payload);

            return this;
        }

        /* ------------------------------------------------------------------ events */

        /// <summary>Called once a document has loaded and the viewer has laid out its first page.</summary>
        public PdfViewer OnDocumentLoaded(Action<PdfDocument> handler)
        {
            _onDocumentLoaded = handler;

            return this;
        }

        /// <summary>Called with the 1-based page whenever the page in view changes.</summary>
        public PdfViewer OnPageChanged(Action<int> handler)
        {
            _onPageChanged = handler;

            return this;
        }

        /// <summary>Called with the new scale whenever the zoom changes, however it changed.</summary>
        public PdfViewer OnScaleChanged(Action<double> handler)
        {
            _onScaleChanged = handler;

            return this;
        }

        /// <summary>Called with the new rotation in degrees.</summary>
        public PdfViewer OnRotationChanged(Action<int> handler)
        {
            _onRotationChanged = handler;

            return this;
        }

        /// <summary>Called with the 1-based page each time one finishes painting.</summary>
        public PdfViewer OnPageRendered(Action<int> handler)
        {
            _onPageRendered = handler;

            return this;
        }

        /// <summary>
        /// Called as the document downloads, with bytes loaded and bytes total. Total is 0 when the
        /// server sends no content length - which is also when pdf.js's own percentage is NaN, so
        /// this hands over the two numbers rather than a percentage.
        /// </summary>
        public PdfViewer OnProgress(Action<double, double> handler)
        {
            _onProgress = handler;

            return this;
        }

        /// <summary>
        /// Called when a document fails to load. Without a handler, failures go to
        /// <c>console.error</c> - which is the right default for a component, but not one a user ever
        /// sees.
        /// </summary>
        public PdfViewer OnError(Action<PdfError> handler)
        {
            _onError = handler;

            return this;
        }

        /// <summary>
        /// Called with the outcome of a search, and again as its match count grows. See
        /// <see cref="PdfSearchResult"/>.
        /// </summary>
        public PdfViewer OnSearchResults(Action<PdfSearchResult> handler)
        {
            _onSearchResults = handler;

            return this;
        }

        /// <summary>Called when the annotation editor's active tool changes, including by keyboard.</summary>
        public PdfViewer OnAnnotationEditorModeChanged(Action<AnnotationEditorMode> handler)
        {
            _onAnnotationEditorModeChanged = handler;

            return this;
        }

        /// <summary>
        /// Asked for the password when a document turns out to be encrypted, and asked again with
        /// <see cref="PasswordReason.IncorrectPassword"/> if the one it gives is wrong.
        ///
        /// Returning null gives up, and the load fails with <see cref="PdfErrorKind.Password"/>.
        /// Without a handler at all, an encrypted document fails the same way - pdf.js waits for an
        /// answer that never comes, so the load is abandoned rather than left pending.
        /// </summary>
        public PdfViewer OnPassword(Func<PasswordReason, Task<string>> handler)
        {
            _onPassword = handler;

            return this;
        }

        /* ------------------------------------------------------------------ options */

        /// <summary>
        /// Amends the options the viewer is constructed with, for anything this surface does not
        /// name.
        ///
        /// pdf.js has no <c>updateOptions</c>, so these are read once per viewer - the mutator is
        /// recorded and re-run against a fresh options object on every rebuild, which is what makes a
        /// remounted viewer come back configured the same way.
        /// </summary>
        public PdfViewer Options(Action<PdfViewerOptions> configure)
        {
            if (configure is object) _optionSetters.Add(configure);

            return this;
        }

        /// <summary>
        /// Runs a callback against the live pdf.js viewer, now if it exists and again after every
        /// rebuild.
        ///
        /// The escape hatch for anything <see cref="IPdfViewerInstance"/> does not cover. Because it
        /// is replayed, it must be idempotent - it will run again on the next remount.
        /// </summary>
        public PdfViewer Configure(Action<IPdfViewerInstance> configure)
        {
            if (configure is null) return this;

            _configured.Add(configure);

            if (_viewer is object) configure(_viewer);

            return this;
        }

        /// <summary>
        /// Runs a callback against the live viewer once, and only if it exists. Not replayed on a
        /// remount - for a one-off action rather than configuration.
        /// </summary>
        public PdfViewer Live(Action<IPdfViewerInstance> action)
        {
            if (_viewer is object) action?.Invoke(_viewer);

            return this;
        }

        /// <summary>Drops the shadow and margin pdf.js draws around each page. Read when the viewer is built.</summary>
        public PdfViewer RemovePageBorders(bool remove = true)
        {
            return Options(options => options.removePageBorders = remove);
        }

        /// <summary>Opens links inside the document in a new tab. Read when the viewer is built.</summary>
        public PdfViewer ExternalLinksOpenInNewTab(bool newTab = true)
        {
            _externalLinkTarget = newTab ? LinkTarget.Blank : LinkTarget.None;

            return this;
        }

        private LinkTarget _externalLinkTarget = LinkTarget.Blank;

        /// <summary>
        /// The largest canvas pdf.js will paint a page into, in pixels. Above it, pages are painted
        /// smaller and scaled up - which is what keeps a very large page renderable at high zoom.
        /// </summary>
        public PdfViewer MaxCanvasPixels(double pixels)
        {
            return Options(options => options.maxCanvasPixels = pixels);
        }

        /// <summary>
        /// Remaps the page's black and white, for showing a document in a dark UI without inverting
        /// its photographs. Both colours are needed, and pdf.js only remaps content the document
        /// draws in pure black or white.
        /// </summary>
        public PdfViewer PageColors(string background, string foreground)
        {
            return Options(options => options.pageColors = new PageColors { background = background, foreground = foreground });
        }

        /// <summary>
        /// Runs the document's own embedded JavaScript - the calculate, format and validate actions
        /// an AcroForm carries.
        ///
        /// Read when the viewer is built. Safe to leave on for a viewer that shows arbitrary
        /// documents: pdf.js starts no sandbox at all for a document with neither form fields nor
        /// document-level actions, which is most of them.
        ///
        /// See <see cref="OnSandboxCreated"/> for the readiness signal, and note that interactive
        /// form fields also need <see cref="AnnotationMode.EnableForms"/>, which is the default.
        /// </summary>
        public PdfViewer EnableScripting(bool enable = true)
        {
            _scriptingEnabled = enable;

            return this;
        }

        /// <summary>
        /// Called when the scripting sandbox has come up for a document. The one reliable signal that
        /// it did - a sandbox that fails to start reports itself to the console and leaves the form
        /// inert rather than throwing.
        ///
        /// Note pdf.js starts a sandbox for any document with <b>form fields or document-level
        /// actions</b>, not only for one carrying scripts - so this fires for an ordinary AcroForm
        /// too. It says the sandbox is running, not that anything in the document will use it;
        /// <c>PdfDocument.HasEmbeddedJavaScriptAsync</c> is the question to ask for that.
        /// </summary>
        public PdfViewer OnSandboxCreated(Action handler)
        {
            _onSandboxCreated = handler;

            return this;
        }

        /// <summary>
        /// Replaces the localization the package hands pdf.js. Pass null to go back to the package's
        /// own; see <see cref="WithoutOwnLocalization"/> to fall back to pdf.js's built-in English.
        /// </summary>
        public PdfViewer L10n(object l10n)
        {
            _customL10n = l10n;
            _useOwnL10n = l10n is null;

            return this;
        }

        /// <summary>
        /// Lets pdf.js use its own built-in English localization instead of the package's
        /// TNT-backed bridge.
        /// </summary>
        public PdfViewer WithoutOwnLocalization()
        {
            _useOwnL10n = false;
            _customL10n = null;

            return this;
        }

        /* -------------------------------------------------------------- lifecycle */

        protected override void CreateCore(HTMLElement container)
        {
            // The DOM contract pdf.js enforces, and the reason this component builds its own
            // elements rather than taking one: the container it is given must be positioned and must
            // be the thing that scrolls, and the pages must go in a child carrying the pdfViewer
            // class - which is what its stylesheet positions them through.
            _scrollHost = DIV();

            _scrollHost.style.position = "absolute";
            _scrollHost.style.top      = "0";
            _scrollHost.style.left     = "0";
            _scrollHost.style.right    = "0";
            _scrollHost.style.bottom   = "0";
            _scrollHost.style.overflow = "auto";

            _pagesHost           = DIV();
            _pagesHost.className = "pdfViewer";

            _scrollHost.appendChild(_pagesHost);
            container.appendChild(_scrollHost);

            _events      = new EventBus();
            _linkService = new PdfLinkServiceJs(new PdfLinkServiceOptions
            {
                eventBus           = _events,
                externalLinkTarget = _externalLinkTarget,
            });

            _findController = new PdfFindControllerJs(new PdfFindControllerOptions
            {
                linkService = _linkService,
                eventBus    = _events,

                // Report the count as pages are scanned rather than only at the end, so a long
                // document's "3 of ..." moves instead of appearing all at once.
                updateMatchesCountOnProgress = true,
            });

            var options = new PdfViewerOptions
            {
                container            = _scrollHost,
                viewer               = _pagesHost,
                eventBus             = _events,
                linkService          = _linkService,
                findController       = _findController,
                textLayerMode        = _textLayerMode,
                annotationMode       = _annotationMode,
                annotationEditorMode = _annotationEditorMode,

                // Without this the annotation layer's note and comment icons are broken images: it
                // builds <img> elements against this path rather than using the inlined CSS ones.
                imageResourcesPath = PdfJs.ImageResourcesPath,
            };

            if (_scriptingEnabled)
            {
                // Both URLs absolute, because the two resolve against different bases - see
                // PdfScriptingManagerOptions.
                _scriptingManager = new PdfScriptingManagerJs(new PdfScriptingManagerOptions
                {
                    eventBus         = _events,
                    sandboxBundleSrc = PdfJs.SandboxUrl,
                    wasmUrl          = PdfJs.WasmUrl,
                });

                options.scriptingManager = _scriptingManager;
            }

            var l10n = _customL10n ?? (_useOwnL10n ? BuildOwnL10n() : null);

            if (l10n is object) options.l10n = l10n;

            // Last, so a host can override anything decided above.
            foreach (var set in _optionSetters)
            {
                set(options);
            }

            // Read back off the options rather than off the field, so an Options(...) mutator that
            // changed it is accounted for.
            _annotationEditorEnabled = options.annotationEditorMode != AnnotationEditorMode.Disable;

            _viewer = _singlePage
                ? (IPdfViewerInstance)(object)new PdfSinglePageViewerJs(options)
                : (IPdfViewerInstance)(object)new PdfViewerJs(options);

            _linkService.setViewer(_viewer);

            // pdf.js calls translate() itself only when it built the l10n implementation. Given one,
            // it assumes the object is watching the document - so the first call, which is also what
            // tells the bridge which element to observe, has to come from here.
            if (_l10nObject is object) _l10nObject.translate(_scrollHost);

            // Before any setDocument: pdf.js wires the manager to the viewer inside setDocument, and
            // one that has not been given a viewer by then never runs anything.
            _scriptingManager?.setViewer(_viewer);

            Subscribe();
        }

        /// <summary>
        /// The package's own localization bridge, which answers pdf.js's message ids through
        /// Tesserae's TNT translation table. Held so its observer can be released on teardown.
        /// </summary>
        private object BuildOwnL10n()
        {
            _l10n       = new PdfL10n();
            _l10nObject = _l10n.Build();

            return _l10nObject;
        }

        private PdfL10n       _l10n;
        private PdfL10nObject _l10nObject;

        private void Subscribe()
        {
            On(PdfViewerEvents.PagesInit, _ => ApplyViewState());

            On(PdfViewerEvents.PageChanging, data =>
            {
                var changed = (IPageChangingEvent)data;

                _page = changed.pageNumber;

                _onPageChanged?.Invoke(changed.pageNumber);
            });

            On(PdfViewerEvents.ScaleChanging, data =>
            {
                var changed = (IScaleChangingEvent)data;

                // presetValue is the difference between "the user asked for page-width" and "the
                // user asked for 112%", and only the former should survive a resize.
                if (!string.IsNullOrEmpty(changed.presetValue) && IsPreset(changed.presetValue))
                {
                    _lastScalePreset = changed.presetValue;
                    _scaleValue      = changed.presetValue;
                }
                else
                {
                    _lastScalePreset = null;
                    _scaleValue      = changed.scale.ToString();
                }

                _onScaleChanged?.Invoke(changed.scale);
            });

            On(PdfViewerEvents.RotationChanging, data =>
            {
                var changed = (IRotationChangingEvent)data;

                _rotation = changed.pagesRotation;

                _onRotationChanged?.Invoke(changed.pagesRotation);
            });

            On(PdfViewerEvents.PageRendered, data =>
            {
                var rendered = (IPageRenderedEvent)data;

                _onPageRendered?.Invoke(rendered.pageNumber);
            });

            // Both find events feed the same handler: the count one arrives repeatedly as pages are
            // scanned and carries no state, the control-state one arrives with the outcome. A host
            // that only listened to the second would show no progress on a long document; one that
            // only listened to the first would never learn that nothing was found.
            // A running count carries no state of its own, so it reports the last state seen rather
            // than assuming Pending. pdf.js raises a control-state event for Pending when a search
            // starts and another for the outcome when it ends, with count events in between and
            // sometimes after - so forcing Pending here would overwrite "found" with "searching" a
            // moment after the search succeeded.
            On(PdfViewerEvents.UpdateFindMatchesCount, data =>
            {
                if (_onSearchResults is null) return;

                var counted = (IUpdateFindMatchesCountEvent)data;

                _onSearchResults(new PdfSearchResult(_lastFindState, counted.matchesCount, _lastQuery));
            });

            On(PdfViewerEvents.UpdateFindControlState, data =>
            {
                var state = (IUpdateFindControlStateEvent)data;

                _lastFindState = (FindState)state.state;

                if (_onSearchResults is null) return;

                _onSearchResults(new PdfSearchResult(_lastFindState, state.matchesCount, state.rawQuery ?? _lastQuery));
            });

            On(PdfViewerEvents.AnnotationEditorModeChanged, data =>
            {
                var changed = (IAnnotationEditorModeChangedEvent)data;

                _annotationEditorMode = (AnnotationEditorMode)changed.mode;

                _onAnnotationEditorModeChanged?.Invoke(_annotationEditorMode);
            });

            On(PdfViewerEvents.SandboxCreated, _ => _onSandboxCreated?.Invoke());
        }

        /// <summary>
        /// Subscribes to an event and registers the matching <c>off</c>. pdf.js's bus has no
        /// disposable handle - a listener is removed by passing the same function back - so the
        /// delegate has to be held onto, which is what this closure does.
        /// </summary>
        private void On(string eventName, Action<object> handler)
        {
            _events.on(eventName, handler);

            Disposables.Add(() => _events.off(eventName, handler));
        }

        /// <summary>
        /// Applies the page, zoom, rotation and layout the component was configured with, or was
        /// showing before a remount.
        ///
        /// Called from <c>pagesinit</c> and nowhere else. Before that event the viewer has no pages to
        /// apply them to and silently keeps its defaults - which is the single easiest thing to get
        /// wrong here, because setting them earlier looks like it worked.
        /// </summary>
        private void ApplyViewState()
        {
            if (_viewer is null) return;

            if (_scrollMode != ScrollMode.Vertical) _viewer.scrollMode = (int)_scrollMode;
            if (_spreadMode != SpreadMode.None)     _viewer.spreadMode = (int)_spreadMode;

            _viewer.currentScaleValue = _scaleValue;

            if (_rotation != 0) _viewer.pagesRotation = _rotation;
            if (_page > 1)      _viewer.currentPageNumber = _page;
        }

        protected override void AfterCreate()
        {
            foreach (var configure in _configured)
            {
                configure(_viewer);
            }

            if (_source is object) OpenDocumentAsync(_source).FireAndForget();
        }

        protected override void OnResized()
        {
            // pdf.js resolves a fit mode into a number once, so without re-applying it a viewer that
            // fitted its width in a narrow pane keeps that zoom in a wide one.
            if (!_keepFitOnResize || _viewer is null || _lastScalePreset is null) return;

            _viewer.currentScaleValue = _lastScalePreset;
        }

        protected override void BeforeDispose()
        {
            // What a remount restores. The pixel scroll offset within a page is deliberately not
            // kept: it means nothing at a different container size, and restoring it fights the fit
            // mode that is about to be re-applied.
            if (_viewer is object)
            {
                _page       = _viewer.currentPageNumber;
                _scaleValue = _viewer.currentScaleValue;
                _rotation   = _viewer.pagesRotation;
                _scrollMode = (ScrollMode)_viewer.scrollMode;
                _spreadMode = (SpreadMode)_viewer.spreadMode;
            }
        }

        protected override void DisposeCore()
        {
            ReleaseDocument();

            // Releases the bridge's MutationObserver, which is watching the scroll host about to be
            // removed below.
            //
            // Written out rather than as `_l10nObject?.destroy()`: the compiler emits a
            // null-conditional call on a *delegate field* as an invocation of the object itself -
            // `($nc) => $nc == null ? null : $nc()` - dropping the member name, and the page dies on
            // "$nc25 is not a function" at teardown. A plain null check emits correctly.
            if (_l10nObject is object) _l10nObject.destroy();
            _l10nObject = null;
            _l10n       = null;

            _viewer           = null;
            _events           = null;
            _linkService      = null;
            _findController   = null;
            _scriptingManager = null;

            // The pages the viewer built live inside these, so dropping them is what releases the
            // canvases and text layers.
            if (_scrollHost is object && _scrollHost.parentElement is object)
            {
                _scrollHost.parentElement.removeChild(_scrollHost);
            }

            _scrollHost = null;
            _pagesHost  = null;
        }

        /* --------------------------------------------------------------- document */

        private async Task OpenDocumentAsync(PdfSource source)
        {
            var generation = ++_generation;

            ReleaseDocument();

            if (source is null) return;

            IPdfDocumentLoadingTask loadingTask;

            try
            {
                loadingTask = PdfJsLib.getDocument(source.ToInitParameters());
            }
            catch (Exception exception)
            {
                Report(PdfError.FromJs(exception));

                return;
            }

            _loadingTask = loadingTask;

            if (_onProgress is object)
            {
                loadingTask.onProgress = progress => _onProgress(progress.loaded, progress.total);
            }

            // pdf.js does not wait for this callback: it hands over a function to call with the
            // password and leaves the load pending until it is called. That is what makes a dialog
            // possible - and what makes "no handler" a hang, which is why the task is destroyed when
            // nothing produces a password.
            loadingTask.onPassword = (updatePassword, reason) =>
                AnswerPasswordAsync(loadingTask, updatePassword, (PasswordReason)reason).FireAndForget();

            IPdfDocumentProxy document;

            try
            {
                document = await PromiseHelper.ToTask<IPdfDocumentProxy>(loadingTask.promise);
            }
            catch (Exception exception)
            {
                // A load abandoned because another document was asked for is not a failure worth
                // reporting - the host already knows, because it asked.
                if (generation == _generation) Report(PdfError.FromJs(exception));

                if (!loadingTask.destroyed) loadingTask.destroy();

                if (generation == _generation) _loadingTask = null;

                return;
            }

            // Superseded while loading, or torn down: hand the document straight back rather than
            // showing something nobody asked for.
            if (generation != _generation || _viewer is null)
            {
                if (!loadingTask.destroyed) loadingTask.destroy();

                return;
            }

            _document = new PdfDocument(loadingTask, document);

            _viewer.setDocument(document);
            _linkService.setDocument(document, null);

            _onDocumentLoaded?.Invoke(_document);

            // pdf.js does not fetch these itself: currentPageLabel and pageLabelToPageNumber both
            // answer against labels the host passes in, and answer nothing until it has. Fetched
            // after the callback above so a slow round trip does not delay the document being
            // reported as loaded, and generation-checked because it is another await.
            await ApplyPageLabelsAsync(generation);
        }

        /// <summary>
        /// Fetches the document's page labels and hands them to the viewer, so a document that
        /// numbers its front matter i, ii reports those instead of 1, 2.
        /// </summary>
        private async Task ApplyPageLabelsAsync(int generation)
        {
            if (_document is null) return;

            string[] labels;

            try
            {
                labels = await _document.GetPageLabelsAsync();
            }
            catch (Exception exception)
            {
                // Labels are cosmetic: a document whose label array is malformed should still be
                // readable, so this is the one failure here that is logged rather than reported.
                console.error("Tesserae.Pdf: could not read the document's page labels", exception);

                return;
            }

            // Superseded, or torn down, while the labels were being fetched.
            if (generation != _generation || _viewer is null) return;

            // null is the normal answer - most documents just use their page numbers - and passing it
            // is how pdf.js is told to do the same.
            _viewer.setPageLabels(labels);

            // pdf.js raises nothing here, and until this point currentPageLabel answers null even for
            // a document that has labels - so a toolbar showing one has no way to know it should look
            // again. Raised for a document with no labels too: "there are none" is the answer that
            // stops a toolbar waiting. See PdfViewerEvents.PageLabelsApplied.
            _events?.dispatch(PdfViewerEvents.PageLabelsApplied, new PageLabelsEventPayload { pageLabels = labels });
        }

        private async Task AnswerPasswordAsync(IPdfDocumentLoadingTask loadingTask, Action<string> updatePassword, PasswordReason reason)
        {
            string password = null;

            if (_onPassword is object)
            {
                try
                {
                    password = await _onPassword(reason);
                }
                catch (Exception exception)
                {
                    console.error("Tesserae.Pdf: the password callback threw", exception);
                }
            }

            if (string.IsNullOrEmpty(password))
            {
                // Nothing is going to answer, so end the load rather than leaving it pending for
                // ever - the promise then rejects with a PasswordException and reaches OnError.
                if (!loadingTask.destroyed) loadingTask.destroy();

                return;
            }

            updatePassword(password);
        }

        private void ReleaseDocument()
        {
            // The viewer has to be told to let go first: it holds page objects of the document, and
            // destroying the loading task under it leaves it holding pages of a document that is
            // gone - which pdf.js reports as "TextModel got disposed" style noise on the next paint.
            _viewer?.setDocument(null);
            _findController?.setDocument(null);
            _linkService?.setDocument(null, null);

            if (_loadingTask is object && !_loadingTask.destroyed)
            {
                _loadingTask.destroy();
            }

            _loadingTask = null;
            _document    = null;
        }

        private void Report(PdfError error)
        {
            if (_onError is object)
            {
                _onError(error);

                return;
            }

            console.error("Tesserae.Pdf: could not load the document", error.Kind.ToString(), error.Message);
        }
    }

    /// <summary>
    /// What the annotation editor's mode setter takes. pdf.js's getter and setter for that property
    /// disagree in shape, so the two sides are typed separately.
    /// </summary>
    [ObjectLiteral]
    public class AnnotationEditorModeChange
    {
        public int mode;

        /// <summary>The id of an existing annotation to open for editing, rather than starting a new one.</summary>
        public string editId;
    }

    /// <summary>How a search should be run.</summary>
    public sealed class FindOptions
    {
        /// <summary>Match case. Off by default.</summary>
        public bool CaseSensitive { get; set; }

        /// <summary>Match whole words only. Off by default.</summary>
        public bool EntireWord { get; set; }

        /// <summary>Highlight every match, not only the selected one. On by default.</summary>
        public bool HighlightAll { get; set; } = true;

        /// <summary>
        /// Treat accented and unaccented letters as different. Off by default, so "cafe" finds
        /// "café" - which is usually what a person searching means.
        /// </summary>
        public bool MatchDiacritics { get; set; }
    }

    /// <summary>The outcome of a search, as pdf.js reports it.</summary>
    public sealed class PdfSearchResult
    {
        internal PdfSearchResult(FindState state, IMatchesCount matches, object query)
        {
            State   = state;
            Current = matches is object ? matches.current : 0;
            Total   = matches is object ? matches.total   : 0;
            Query   = query;
        }

        /// <summary>
        /// Whether anything was found. <see cref="FindState.Pending"/> means the document is still
        /// being read, and the counts below are a running total rather than a final one.
        /// </summary>
        public FindState State { get; }

        /// <summary>The 1-based index of the selected match, or 0 when there is none.</summary>
        public int Current { get; }

        /// <summary>How many matches have been found so far.</summary>
        public int Total { get; }

        /// <summary>What was searched for - a string, or an array of terms.</summary>
        public object Query { get; }

        /// <summary>Whether the search has finished, i.e. the counts are final.</summary>
        public bool IsComplete => State != FindState.Pending;

        /// <summary>Whether anything matched.</summary>
        public bool HasMatches => Total > 0;
    }
}
