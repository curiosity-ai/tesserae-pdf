using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TNT;
using Transpose;
using static TNT.T;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.Sample.SamplesHelper;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// The four steps between pdf.js deciding an element needs a label and that label appearing in the
    /// DOM - shown with the live values of each, for whichever localization implementation the viewer
    /// was built with.
    ///
    /// The Localization page shows that a German application gets a German viewer. This one shows how,
    /// because everything the mechanism does lands on attributes nobody looks at until they matter:
    /// the trace and the inspector below read them back out of the document.
    /// </summary>
    [SampleDetails(Group = "Runtime and hosting", Order = 90, Icon = UIcons.Translate)]
    public class TranslationPipelineSample : IComponent, ISample
    {
        // The three implementations a viewer can be built with: the package's TNT-backed bridge, one
        // written on this page, and pdf.js's own inlined English.
        private const string BRIDGE      = "bridge";
        private const string HANDWRITTEN = "handwritten";
        private const string PDFJS       = "pdfjs";

        // The translation table TNT is holding: none, or one of the two below.
        private const string NONE   = "none";
        private const string GERMAN = "de";
        private const string ARABIC = "ar";

        /// <summary>
        /// Two of the package's l10n keys and three more, in the form <c>PdfL10nStrings</c> asks for
        /// them - the English text, not pdf.js's message ids. A real application merges these into
        /// whatever it already feeds <c>TNT.T.SetTranslation</c>.
        /// </summary>
        private static readonly Dictionary<string, string> German = new Dictionary<string, string>
        {
            { "Page {0}",         "Seite {0}" },
            { "[{0} Annotation]", "[Anmerkung: {0}]" },
            { "Highlight",        "Hervorheben" },
            { "Comment",          "Kommentar" },
            { "Alt text",         "Alternativtext" },
        };

        /// <summary>
        /// The same five in Arabic, which is here for its direction rather than its text: a
        /// right-to-left tag is what makes the bridge's <c>getDirection</c> answer <c>"rtl"</c>.
        /// </summary>
        private static readonly Dictionary<string, string> Arabic = new Dictionary<string, string>
        {
            { "Page {0}",         "صفحة {0}" },
            { "[{0} Annotation]", "[تعليق: {0}]" },
            { "Highlight",        "تظليل" },
            { "Comment",          "تعليق" },
            { "Alt text",         "نص بديل" },
        };

        // The attributes pdf.js's 50 messages write to, in the order the inspector looks for them.
        private static readonly string[] MessageAttributes = { "aria-label", "title", "alt", "aria-description" };

        private readonly IComponent _content;

        private readonly Stack _viewHost  = VStack().WS().H(420);
        private readonly Stack _trace     = VStack().WS().Gap(2.px());
        private readonly Stack _inspector = VStack().WS().Gap(2.px());
        private readonly Stack _callLog   = VStack().WS().Gap(2.px());

        private readonly TextBlock _languageLine;

        // Nothing to look at: an element whose removal is this page being navigated away from. A raw
        // element rather than one of the stacks above, because Stack.Render() is free to hand back a
        // different element the second time it is asked - and the hook has to be on the one that is
        // actually in the document.
        private readonly HTMLElement _lifetime = DIV();

        // The last 40 members pdf.js called on the hand-written implementation, newest last.
        private readonly List<string> _calls = new List<string>();

        private readonly string _pageLanguage;

        // The current viewer's own element. Everything this page reads back - the trace, the
        // inspector - is scoped to it rather than to the document, so the sidebar and the gallery's
        // own markup cannot show up as results.
        private HTMLElement _container;

        private string _implementation = BRIDGE;
        private string _table          = NONE;

        public TranslationPipelineSample()
        {
            _pageLanguage = PdfJs.Language;
            _languageLine = TextBlock("").Tiny().Secondary();

            var implementations = new[]
            {
                new { Key = BRIDGE,      Label = "The package's bridge (PdfL10n, through TNT)" },
                new { Key = HANDWRITTEN, Label = "An implementation written on this page (L10n(...))" },
                new { Key = PDFJS,       Label = "pdf.js's own English (WithoutOwnLocalization())" },
            };

            var implementationPicker = Dropdown().Width(360.px());

            foreach (var item in implementations)
            {
                var captured = item;

                implementationPicker.AddItems(DropdownItem(captured.Label).SelectedIf(captured.Key == BRIDGE)
                   .OnSelected(_ =>
                    {
                        _implementation = captured.Key;
                        Rebuild();
                    }));
            }

            var tables = new[]
            {
                new { Key = NONE,   Label = "No table - every key falls back to its own English" },
                new { Key = GERMAN, Label = "Deutsch" },
                new { Key = ARABIC, Label = "العربية (right to left)" },
            };

            var tablePicker = Dropdown().Width(360.px());

            foreach (var item in tables)
            {
                var captured = item;

                tablePicker.AddItems(DropdownItem(captured.Label).SelectedIf(captured.Key == NONE)
                   .OnSelected(_ =>
                    {
                        _table = captured.Key;
                        Rebuild();
                    }));
            }

            Rebuild();

            _content = SectionStack().Secondary()
               .SampleTitle(typeof(TranslationPipelineSample), UIcons.Translate, "From a data-l10n-id attribute to the text a screen reader reads")
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("pdf.js never writes the text of a label it builds. It writes a message id - data-l10n-id=\"pdfjs-page-landmark\" - and, for the five messages that take an argument, data-l10n-args='{\"page\":\"3\"}' beside it, and leaves the element for something else to finish. Four things happen after that, and the trace below shows all four with their live values."),
                        TextBlock("1. The bridge notices. A MutationObserver on the viewer's container watches for added subtrees and for changes to those two attributes. It is not an optimisation: given an l10n implementation of its own, pdf.js stops calling translate() - its page view does it only under if (!options.l10n) - because it assumes the object is watching the document, the way Fluent's DOM localization does.").MT(8),
                        TextBlock("2. The id becomes an English literal. PdfL10nStrings maps all 50 ids pdf.js can ask for onto \"Page {0}\".t() and its 49 siblings - written out as literals rather than looked up, because TNT extracts translatable strings by scanning source for .t() applied to one.").MT(8),
                        TextBlock("3. TNT answers, from the table the application set. The package never calls SetTranslation itself: the table is process-global and singular, so a package that set it would clobber its host's.").MT(8),
                        TextBlock("4. The bridge writes the result where the message says it goes - textContent for the 14 messages that carry a value, the attribute named by the message for the 36 that are attribute-only. Which is why almost nothing here is visible: page landmarks, annotation alt text and editor tooltips are read by assistive technology, not by eyes.").MT(8))).SetTitle("Overview")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        TextBlock("An id the package does not carry leaves the element exactly as pdf.js built it. Better an untranslated label than an empty one - and it is what makes a pdf.js upgrade that adds message ids safe."),
                        TextBlock("Placeholders are TNT's, not Fluent's: \"Page {0}\", not \"Page { $page }\". Each of the five parameterised messages takes exactly one argument, which is why the bridge reads whichever of the four known argument names is present rather than mapping names to positions.").MT(8),
                        TextBlock("pause() and resume() bracket every layer pdf.js inserts - text, annotation, struct tree, XFA - so a layer is translated once when it is complete rather than per node as it is built. resume() has to walk the subtree itself, because the inserts that happened while paused produced no records to replay. Switch to the hand-written implementation and the call log below fills up with those pairs - and with nothing else, because pdf.js never calls translate() on an implementation it did not build. Every label it shows was written by that implementation's own observer.").MT(8),
                        TextBlock("get() is the one member asked for a string rather than for an element to decorate; the annotation editor's alt-text UI uses it. And one message has no text at all - the date on an annotation popup is a Fluent DATETIME call, so the bridge formats data-l10n-args.dateObj with the browser's own locale conventions instead of looking it up.").MT(8),
                        TextBlock("PdfJs.Language is what decides direction, and it translates nothing by itself: it tells pdf.js which language it is looking at. It defaults to the document's lang attribute, and this page sets it alongside the table so the two answers stay consistent.").MT(8))).SetTitle("Best Practices")))
               .FlatSection(VStack().Children(
                    Card(VStack().WS().Children(
                        SampleSubTitle("Try it"),
                        TextBlock("Who does the writing").Small().SemiBold(),
                        implementationPicker,
                        TextBlock("What TNT has to say about it").Small().SemiBold().MT(8),
                        tablePicker,
                        _languageLine.MT(4),

                        SampleSubTitle("The four steps, for the first page landmark in the DOM"),
                        _trace,

                        _viewHost.MT(8),

                        SampleSubTitle("What is in the viewer's DOM"),
                        _inspector,

                        SampleSubTitle("What pdf.js called on the hand-written implementation"),
                        _callLog,

                        Raw(_lifetime),

                        SampleHint("Pick Deutsch: step 3 changes, and step 4 changes with it - the aria-label on every page div becomes \"Seite n\". Then pick the hand-written implementation to see which members pdf.js actually calls, and pdf.js's own English to watch the table stop mattering.")
                    )).SetTitle("Usage")))
               .SeeAlso(typeof(LocalizationSample), typeof(FormsAndAnnotationsSample), typeof(TextSelectionSample));

            // The table and the language are process-global, so they are put back when the page goes -
            // the gallery tears a page down on every navigation, which is exactly the hook for it.
            DomObserver.WhenRemoved(_lifetime, () =>
            {
                T.SetTranslation(null);
                PdfJs.Language = _pageLanguage;
            });
        }

        /// <summary>
        /// Applies the table and builds a viewer with the chosen implementation.
        ///
        /// The viewer is rebuilt rather than refreshed for the same reason the Localization page
        /// rebuilds: TNT reads its table at each lookup, but pdf.js has already written its ids into
        /// the DOM and the bridge has already answered them.
        /// </summary>
        private void Rebuild()
        {
            switch (_table)
            {
                case GERMAN:
                    T.SetTranslation(German);
                    PdfJs.Language = "de";

                    break;

                case ARABIC:
                    T.SetTranslation(Arabic);
                    PdfJs.Language = "ar";

                    break;

                default:
                    // null is how TNT is told to stop translating: every key falls back to its own text.
                    T.SetTranslation(null);
                    PdfJs.Language = _pageLanguage;

                    break;
            }

            _calls.Clear();

            _viewHost.Clear();
            _trace.Clear();
            _inspector.Clear();

            var viewer = PdfJs.Viewer();

            // The component's container exists from its constructor and is the element pdf.js builds
            // inside, so it can be observed and read back before anything is mounted.
            _container = viewer.StylingContainer;

            if (_implementation == HANDWRITTEN) viewer.L10n(new HandWrittenL10n(Log).Attach(_container));
            if (_implementation == PDFJS) viewer.WithoutOwnLocalization();

            viewer
               .Url(OUTLINE_PDF)
               .FitWidth()
               .OnPageRendered(_ => Report());

            _viewHost.Add(viewer.S());

            Report();
        }

        private void Report()
        {
            ShowLanguage();
            ShowTrace();
            ShowInspector();
            ShowCallLog();
        }

        private void ShowLanguage()
        {
            var language = PdfJs.Language;

            _languageLine.Text = $"PdfJs.Language = \"{language}\", so the implementation reports getDirection() = \"{(IsRightToLeft(language) ? "rtl" : "ltr")}\"";
        }

        /// <summary>
        /// The same four steps, with the values they actually hold right now: the first and the last
        /// read out of the DOM, the middle two describing whichever implementation is in play.
        /// </summary>
        private void ShowTrace()
        {
            _trace.Clear();

            var landmark = _container?.querySelector("[data-l10n-id='pdfjs-page-landmark']").As<HTMLElement>();

            if (landmark is null)
            {
                _trace.Add(TextBlock("No page is in the DOM yet - pdf.js writes its ids as it builds each page view.").Tiny().Secondary());

                return;
            }

            Step("1. pdf.js marks the element", $"data-l10n-id=\"{landmark.getAttribute("data-l10n-id")}\"  data-l10n-args={landmark.getAttribute("data-l10n-args") ?? "(none)"}");

            switch (_implementation)
            {
                case BRIDGE:
                    Step("2. the id becomes a literal", "PdfL10nStrings: Message(Attribute(\"aria-label\", \"Page {0}\".t()))");
                    Step("3. TNT answers",              $"\"Page {{0}}\".t() = \"{"Page {0}".t()}\"");

                    break;

                case HANDWRITTEN:
                    Step("2. the id becomes a literal", "HandWrittenL10n on this page: its own switch, which carries three of the fifty ids");
                    Step("3. TNT answers",              "nothing - this implementation does not ask TNT, so the table below has no effect on it");

                    break;

                default:
                    Step("2. the id becomes a literal", "pdf.js's own l10n: the en-US Fluent bundle inlined into its viewer");
                    Step("3. TNT answers",              "nothing - pdf.js never sees TNT, so this viewer is English whatever the table says");

                    break;
            }

            var landed = Landed(landmark);

            Step("4. it is written back", landed ?? "(nothing yet - the element is still as pdf.js built it)");

            void Step(string label, string value)
            {
                _trace.Add(HStack().WS().Gap(8.px()).Children(
                    TextBlock(label).Small().SemiBold().W(190),
                    TextBlock(value).Small().Style(s => s.wordBreak = "break-word").Grow()));
            }
        }

        /// <summary>
        /// Every element inside the viewer carrying a message id, and what became of it. This is the
        /// only place the mechanism is visible: one row per page landmark on a plain document, plus a
        /// row per annotation and per editor control on the documents that have them.
        /// </summary>
        private void ShowInspector()
        {
            _inspector.Clear();

            var localized = _container?.querySelectorAll("[data-l10n-id]");

            if (localized is null || localized.length == 0)
            {
                _inspector.Add(TextBlock("Nothing carries a data-l10n-id yet.").Tiny().Secondary());

                return;
            }

            _inspector.Add(TextBlock($"{localized.length} element(s), of which the first {Math.Min(12, (int)localized.length)}:").Tiny().Secondary().PB(4));

            for (var i = 0; i < localized.length && i < 12; i++)
            {
                var element = localized[i].As<HTMLElement>();

                _inspector.Add(HStack().WS().Gap(8.px()).Children(
                    TextBlock(element.getAttribute("data-l10n-id")).Tiny().SemiBold().W(190),
                    TextBlock(element.getAttribute("data-l10n-args") ?? "-").Tiny().Secondary().W(120),
                    TextBlock(Landed(element) ?? "(untranslated)").Tiny().Style(s => s.wordBreak = "break-word").Grow()));
            }
        }

        /// <summary>
        /// Where a message landed on an element: the first of pdf.js's four attributes that is set,
        /// or the element's own text. Null when nothing answered the id at all.
        /// </summary>
        private static string Landed(HTMLElement element)
        {
            foreach (var attribute in MessageAttributes)
            {
                var value = element.getAttribute(attribute);

                if (!string.IsNullOrEmpty(value)) return $"{attribute}=\"{value}\"";
            }

            var text = element.textContent;

            return string.IsNullOrEmpty(text) ? null : $"textContent=\"{text}\"";
        }

        private void ShowCallLog()
        {
            _callLog.Clear();

            if (_implementation != HANDWRITTEN)
            {
                _callLog.Add(TextBlock("Only the hand-written implementation is instrumented - the package's bridge and pdf.js's own are not this page's code to log.").Tiny().Secondary());

                return;
            }

            if (_calls.Count == 0)
            {
                _callLog.Add(TextBlock("Nothing called yet.").Tiny().Secondary());

                return;
            }

            // The last dozen, oldest first, so a pause/resume pair reads in the order it happened.
            for (var i = Math.Max(0, _calls.Count - 12); i < _calls.Count; i++)
            {
                _callLog.Add(TextBlock(_calls[i]).Tiny().Secondary());
            }
        }

        private void Log(string call)
        {
            _calls.Add(call);

            if (_calls.Count > 40) _calls.RemoveAt(0);

            ShowCallLog();
        }

        /// <summary>The five short codes pdf.js writes right to left - the bridge's own list.</summary>
        private static bool IsRightToLeft(string language)
        {
            if (string.IsNullOrEmpty(language)) return false;

            var separator = language.IndexOf('-');
            var shortCode = separator > 0 ? language.Substring(0, separator) : language;

            switch (shortCode.ToLower())
            {
                case "ar":
                case "he":
                case "fa":
                case "ps":
                case "ur": return true;
                default:   return false;
            }
        }

        public HTMLElement Render() => _content.Render();

        /// <summary>
        /// A localization implementation written from scratch, to show what the interface actually
        /// demands - and, through the log it keeps, which of its members pdf.js calls and when.
        ///
        /// It is deliberately three ids wide. The point is not to be a second bridge but to make the
        /// two non-obvious parts of the contract concrete: <b>the object has to watch the document
        /// itself</b>, because pdf.js only calls <c>translate()</c> when it built the implementation,
        /// and <c>resume()</c> has to walk the subtree, because whatever was inserted while it was
        /// paused produced no mutation records.
        /// </summary>
        private sealed class HandWrittenL10n
        {
            private readonly Action<string> _log;

            private MutationObserver _observer;
            private HTMLElement      _root;

            internal HandWrittenL10n(Action<string> log) => _log = log;

            /// <summary>
            /// Starts watching <paramref name="root"/> and hands back the object pdf.js is given.
            /// Watching starts here rather than on the first <c>translate()</c> - unlike the package's
            /// bridge, which the component calls once so it learns which element to observe, this one
            /// is handed the element it belongs to.
            /// </summary>
            internal PdfL10nObject Attach(HTMLElement root)
            {
                _root = root;

                _observer = new MutationObserver((records, _) =>
                {
                    foreach (var record in records)
                    {
                        if (record.type == "attributes")
                        {
                            Apply(record.target.As<HTMLElement>());

                            continue;
                        }

                        foreach (var added in record.addedNodes)
                        {
                            var element = added.As<HTMLElement>();

                            // nodeType 1 is an element; a text node has neither attributes nor
                            // querySelectorAll, and what pdf.js adds is a whole subtree.
                            if (element is object && element.nodeType == 1) TranslateSubtree(element);
                        }
                    }
                });

                Connect();

                return new PdfL10nObject
                {
                    getLanguage  = () => PdfJs.Language,
                    getDirection = () => IsRightToLeft(PdfJs.Language) ? "rtl" : "ltr",

                    translate = element =>
                    {
                        Log("translate(element)");
                        TranslateSubtree(element);

                        return Resolved();
                    },

                    translateOnce = element =>
                    {
                        Log("translateOnce(element)");
                        TranslateSubtree(element);

                        return Resolved();
                    },

                    get = (ids, args, fallback) =>
                    {
                        Log($"get(\"{ids}\") -> fallback \"{fallback}\"");

                        return Resolved<object>(Text(ids as string, null) ?? fallback);
                    },

                    pause = () =>
                    {
                        Log("pause()");
                        _observer.disconnect();
                    },

                    resume = () =>
                    {
                        Log("resume()");
                        Connect();

                        // Without this, a layer inserted between pause and resume is never
                        // translated at all: it produced no records to catch up on.
                        if (_root is object) TranslateSubtree(_root);
                    },

                    destroy = () =>
                    {
                        Log("destroy()");

                        _observer?.disconnect();
                        _observer = null;
                        _root     = null;

                        return Resolved();
                    },
                };
            }

            private void Connect()
            {
                if (_observer is null || _root is null) return;

                _observer.observe(_root, new MutationObserverInit
                {
                    childList       = true,
                    subtree         = true,
                    attributes      = true,
                    attributeFilter = new[] { "data-l10n-id", "data-l10n-args" },
                });
            }

            private void TranslateSubtree(HTMLElement root)
            {
                if (root is null) return;

                Apply(root);

                var localized = root.querySelectorAll("[data-l10n-id]");

                for (var i = 0; i < localized.length; i++)
                {
                    Apply(localized[i].As<HTMLElement>());
                }
            }

            private void Apply(HTMLElement element)
            {
                if (element is null || element.nodeType != 1) return;

                var id = element.getAttribute("data-l10n-id");

                if (string.IsNullOrEmpty(id)) return;

                var text = Text(id, Argument(element.getAttribute("data-l10n-args")));

                // An id this implementation does not carry: leave the element as pdf.js built it,
                // which is the same courtesy the package's bridge extends.
                if (text is null) return;

                var attribute = AttributeFor(id);

                if (attribute is null) element.textContent = text;
                else                   element.setAttribute(attribute, text);
            }

            /// <summary>
            /// The demo's whole vocabulary: three of pdf.js's fifty ids, in brackets so they are
            /// unmistakable in the trace above. Nothing here goes through TNT - that is the bridge's
            /// job, and this exists to show what replacing the bridge means.
            /// </summary>
            private static string Text(string id, string argument)
            {
                switch (id)
                {
                    case "pdfjs-page-landmark":               return $"[page {argument ?? "?"}]";
                    case "pdfjs-text-annotation-type":        return $"[{argument ?? "?"} annotation]";
                    case "pdfjs-highlight-floating-button-label": return "[highlight]";
                    default:                                  return null;
                }
            }

            /// <summary>The attribute a message writes to, or null when it carries the element's text.</summary>
            private static string AttributeFor(string id)
            {
                switch (id)
                {
                    case "pdfjs-page-landmark":        return "aria-label";
                    case "pdfjs-text-annotation-type": return "alt";
                    default:                           return null;
                }
            }

            /// <summary>
            /// The one argument a parameterised message carries. Read by shape rather than by name:
            /// each of the five that take one takes exactly one, so whichever of the four names is
            /// present is the one wanted.
            /// </summary>
            private static string Argument(string json)
            {
                if (string.IsNullOrEmpty(json)) return null;

                object args;

                try
                {
                    args = Transpose.Core.es5.JSON.parse(json);
                }
                catch (Exception)
                {
                    // Malformed args are pdf.js's to fix; a label without its number still beats a
                    // throw in the middle of building a page.
                    return null;
                }

                foreach (var name in new[] { "page", "type", "description", "generatedAltText" })
                {
                    var value = Script.Get<object>(args, name);

                    if (value is object) return value.ToString();
                }

                return null;
            }

            private void Log(string call) => _log(call);

            // pdf.js awaits every async member, so each hands back a promise that is already
            // resolved - the work is a table lookup. PromiseExtensions.ToPromise is the runtime's own
            // Task-to-Promise adapter, the same one the compiler emits for await.
            private static IPromise Resolved() => PromiseExtensions.ToPromise(Task.CompletedTask);

            private static IPromise Resolved<T>(T value) => PromiseExtensions.ToPromise(Task.FromResult(value));
        }
    }
}
