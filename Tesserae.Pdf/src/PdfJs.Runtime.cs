using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Transpose;
using static Transpose.Core.dom;

namespace Tesserae.Pdf
{
    public static partial class PdfJs
    {
        private const string DEFAULT_ASSETS_PATH = "assets/js/pdf";

        private static string _assetsPath = DEFAULT_ASSETS_PATH;
        private static string _language;
        private static Task   _loading;

        private static readonly List<Action> _pendingActions = new List<Action>();

        /// <summary>
        /// Where this package's pdf.js bundle lives, relative to the page - the folder holding
        /// <c>pdf.js</c>, <c>pdf.worker.min.mjs</c> and the <c>cmaps/</c>, <c>standard_fonts/</c>,
        /// <c>wasm/</c>, <c>iccs/</c> and <c>images/</c> directories. Defaults to
        /// <c>assets/js/pdf</c>, which is where the build copies them, so you only need to set this
        /// if you serve pdf.js from somewhere else (a CDN, a shared static host). Must be set before
        /// the first viewer is built.
        ///
        /// The worker is located by the bundle itself, relative to its own script URL, so pointing
        /// this at another origin moves it too - and every asset URL below is derived from the same
        /// place, so there is no second setting to keep in sync.
        /// </summary>
        public static string AssetsPath
        {
            get => _assetsPath;
            set => _assetsPath = string.IsNullOrWhiteSpace(value) ? DEFAULT_ASSETS_PATH : value.TrimEnd('/');
        }

        /// <summary>
        /// True once pdf.js has finished loading and both <c>pdfjsLib.*</c> and <c>pdfjsViewer.*</c>
        /// are safe to call.
        /// </summary>
        public static bool IsLoaded => JsWindow.pdfjsLib != null && JsWindow.pdfjsLib.getDocument != null
                                    && JsWindow.pdfjsViewer != null && JsWindow.pdfjsViewer.EventBus != null;

        /// <summary>
        /// Loads pdf.js, at most once per page. Every component awaits this before creating anything,
        /// so callers rarely need it - reach for it when you want to call the pdf.js API directly
        /// before a component has mounted, or to warm pdf.js up before it is first shown.
        /// </summary>
        public static Task LoadAsync()
        {
            if (_loading is null)
            {
                _loading = LoadCoreAsync();
            }

            return _loading;
        }

        /// <summary>
        /// The absolute URL of the folder holding the pdf.js bundle, without a trailing slash.
        ///
        /// Resolved by the browser against <c>document.baseURI</c> rather than assembled by hand:
        /// that gets the directory right whether the app is served as <c>/index.html</c> or from
        /// <c>/some/path/</c>, honours a <c>&lt;base href&gt;</c>, and passes an already-absolute
        /// <see cref="AssetsPath"/> (a CDN) straight through.
        /// </summary>
        public static string BaseUrl => new URL(_assetsPath, document.baseURI).href.TrimEnd('/');

        /// <summary>
        /// The CJK character maps, as <c>cMapUrl</c> wants them: an absolute URL ending in a slash.
        /// A document using a CJK font renders blank glyphs without these, and pdf.js only warns.
        /// </summary>
        public static string CMapUrl => BaseUrl + "/cmaps/";

        /// <summary>The 14 standard PDF fonts, as <c>standardFontDataUrl</c> wants them.</summary>
        public static string StandardFontDataUrl => BaseUrl + "/standard_fonts/";

        /// <summary>
        /// The wasm decoders (JPX, JBIG2, ICC) plus the QuickJS glue the scripting sandbox needs, as
        /// <c>wasmUrl</c> wants them. pdf.js also loads the <c>*_nowasm_fallback.js</c> files from
        /// here when a <c>.wasm</c> fails to instantiate.
        /// </summary>
        public static string WasmUrl => BaseUrl + "/wasm/";

        /// <summary>The CMYK ICC profile, as <c>iccUrl</c> wants it.</summary>
        public static string IccUrl => BaseUrl + "/iccs/";

        /// <summary>
        /// The annotation and editor icons the viewer builds <c>&lt;img src&gt;</c> for at runtime,
        /// as <c>imageResourcesPath</c> wants them. (The ones pdf.js's stylesheet references are
        /// inlined into the bundle instead.)
        /// </summary>
        public static string ImageResourcesPath => BaseUrl + "/images/";

        /// <summary>
        /// The scripting sandbox module, for <c>PDFScriptingManager</c>'s <c>sandboxBundleSrc</c>.
        ///
        /// Absolute on purpose. pdf.js loads it with a native <c>import(sandboxBundleSrc)</c>, so a
        /// relative value resolves against the module that does the importing - which, once the
        /// viewer has been bundled, is a URL that has nothing to do with this package's assets.
        /// </summary>
        public static string SandboxUrl => BaseUrl + "/pdf.sandbox.min.mjs";

        /// <summary>
        /// The BCP-47 language tag reported to pdf.js by the localization bridge - lowercased, since
        /// that is the form pdf.js's own l10n implementations answer with. Defaults to the
        /// document's <c>lang</c> attribute, or <c>"en-us"</c> when the document does not say.
        ///
        /// Setting this does not translate anything on its own: the package's strings go through
        /// Tesserae's TNT translation table, and this only tells pdf.js which language it is looking
        /// at (which decides text direction, and how dates and numbers inside annotations are
        /// formatted).
        /// </summary>
        public static string Language
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_language)) return _language;

                var documentLanguage = document.documentElement?.getAttribute("lang");

                return string.IsNullOrWhiteSpace(documentLanguage) ? "en-us" : documentLanguage.ToLower();
            }
            set => _language = value;
        }

        /// <summary>The version of pdf.js this package bundles, or null before it has loaded.</summary>
        public static string Version => IsLoaded ? PdfJsLib.version : null;

        /// <summary>The pdf.js build hash, or null before it has loaded.</summary>
        public static string Build => IsLoaded ? PdfJsLib.build : null;

        /// <summary>
        /// Where pdf.js loads its worker from. The bundle points this at the worker sitting beside it,
        /// so setting it is only needed to serve the worker from somewhere else entirely.
        ///
        /// Getting this wrong does not fail loudly: pdf.js falls back to importing the worker on the
        /// main thread, which parses correctly and freezes the UI while it does. Watch the console for
        /// "Setting up fake worker".
        /// </summary>
        public static string WorkerSrc
        {
            get => IsLoaded ? PdfJsLib.GlobalWorkerOptions.workerSrc : null;
            set => WhenLoaded(() => PdfJsLib.GlobalWorkerOptions.workerSrc = value);
        }

        private static async Task LoadCoreAsync()
        {
            var baseUrl = BaseUrl;

            // One script, and everything else is already inside it: the bundle injects pdf.js's
            // stylesheet, publishes both globals, and points GlobalWorkerOptions.workerSrc at the
            // worker beside it, resolved from its own script URL. See build/bundle-pdfjs.mjs.
            //
            // Transpose.Require is the runtime's own loader - fully qualified, because the enclosing
            // Tesserae namespace has a Require of its own. It resolves the URL against the document's
            // base, shares one fetch between concurrent callers, waits on a bundle index.html already
            // carries rather than fetching it twice, and forgets a failed load so a later mount
            // retries instead of inheriting the failure.
            await Transpose.Require.RequireAsync(baseUrl + "/pdf.js");

            if (!IsLoaded)
            {
                throw new Exception("Loaded " + baseUrl + "/pdf.js but window.pdfjsLib / window.pdfjsViewer are not defined.");
            }

            // Anything queued while pdf.js was still loading - a host's own pdf.js call, a worker
            // override - runs now, in the order it was requested. One that throws must not strand
            // the rest, or a single bad call takes every viewer on the page down with it.
            var queued = _pendingActions.ToArray();

            _pendingActions.Clear();

            foreach (var action in queued)
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    console.error("Tesserae.Pdf: a queued pdf.js call failed", exception);
                }
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> once pdf.js is safe to touch - immediately if it already is,
        /// otherwise queued until the load finishes.
        ///
        /// This is the safe way to make any global pdf.js call from application code, because most
        /// configuration happens while components are being built in <c>Main</c>, long before the
        /// first mount triggers the load. Note it does not start the load itself: queued actions run
        /// when the first component mounts, or on an explicit <see cref="LoadAsync"/>.
        /// </summary>
        public static void WhenLoaded(Action action)
        {
            if (action is null) return;

            if (IsLoaded)
            {
                action();
                return;
            }

            _pendingActions.Add(action);
        }
    }
}
