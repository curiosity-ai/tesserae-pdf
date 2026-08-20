using System;
using System.Threading.Tasks;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Pdf
{
    /// <summary>
    /// Localizes the text pdf.js writes into the DOM, through Tesserae's TNT translation table - so a
    /// viewer's aria labels, annotation tooltips and editor buttons speak the same language as the
    /// application around them.
    ///
    /// <b>What pdf.js does with this.</b> Its viewer, page views and annotation-editor layer put
    /// <c>data-l10n-id</c> attributes on the elements they build and expect something to turn those
    /// into text. Left to itself pdf.js builds an English-only implementation with an inlined Fluent
    /// bundle; this replaces it, answering the same 50 message ids from
    /// <see cref="PdfL10nStrings"/>.
    ///
    /// <b>Duck-typed on purpose.</b> pdf.js's own <c>L10n</c> base class is not exported from the
    /// viewer bundle and there is no <c>NullL10n</c> to subclass any more, but nothing in pdf.js ever
    /// type-tests the object it is given - so what it wants is an object with the right members, which
    /// is what this builds.
    ///
    /// <b>The observer is not optional.</b> When a custom implementation is supplied, pdf.js stops
    /// calling <c>translate</c> itself - it assumes the object is watching the document, the way
    /// Fluent's DOM localization does. So this connects a <see cref="MutationObserver"/> for
    /// <c>data-l10n-id</c>, and that is what makes a page rendered later, or an annotation added
    /// later, come out translated. <c>pause()</c> and <c>resume()</c> - which pdf.js calls around
    /// inserting a text or annotation layer - suspend it, so a layer is translated once when it is
    /// complete rather than per node as it is built.
    /// </summary>
    internal sealed class PdfL10n
    {
        private MutationObserver _observer;
        private HTMLElement      _root;
        private bool             _paused;

        /// <summary>
        /// The object handed to pdf.js. Its member names are pdf.js's, and its members are C#
        /// delegates - an <c>[ObjectLiteral]</c> field keeps the name it is declared with, and a
        /// lambda assigned to one emits a plain function pdf.js can call.
        /// </summary>
        internal PdfL10nObject Build()
        {
            return new PdfL10nObject
            {
                getLanguage  = () => PdfJs.Language,
                getDirection = () => IsRightToLeft(PdfJs.Language) ? "rtl" : "ltr",

                // Returns a promise because pdf.js awaits it. The work is synchronous - a table
                // lookup - so the promise is already resolved by the time it is handed back.
                translate     = element => PromiseHelper.AsPromise(TranslateAsync(element)),
                translateOnce = element => PromiseHelper.AsPromise(TranslateOnceAsync(element)),

                // The only member pdf.js calls for a string rather than to decorate an element: the
                // annotation editor's alt-text UI asks for its labels this way.
                get = (ids, args, fallback) => PromiseHelper.AsPromise(GetAsync(ids, args, fallback)),

                pause  = Pause,
                resume = Resume,

                destroy = () => PromiseHelper.AsPromise(DestroyAsync()),
            };
        }

        /// <summary>
        /// The languages written right to left, by their short code. pdf.js's own list, kept because
        /// its <c>L10n</c> base class is not reachable to ask.
        /// </summary>
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

        private Task TranslateAsync(HTMLElement element)
        {
            if (element is object)
            {
                TranslateSubtree(element);

                // The first element pdf.js hands over is the one to watch: it is the viewer's own
                // container, and every page, text layer and annotation appears inside it.
                if (_root is null) Observe(element);
            }

            return Task.CompletedTask;
        }

        private Task TranslateOnceAsync(HTMLElement element)
        {
            if (element is object) TranslateSubtree(element);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Answers a message id, or an array of them, as text. <paramref name="fallback"/> is used for
        /// an id this package does not carry, which is what pdf.js expects.
        /// </summary>
        private Task<object> GetAsync(object ids, object args, string fallback)
        {
            if (ids is string[] many)
            {
                var texts = new string[many.Length];

                for (var i = 0; i < many.Length; i++)
                {
                    texts[i] = Resolve(many[i], null) ?? fallback;
                }

                // Script.ToArray: pdf.js maps over the result, and a C# array carries a $type
                // property that has no business crossing into its code.
                return Task.FromResult<object>(Script.ToArray(texts));
            }

            return Task.FromResult<object>(Resolve(ids as string, args) ?? fallback);
        }

        /// <summary>
        /// A message id as plain text, for the <c>get</c> member - which is asked for a string rather
        /// than for an element to decorate. The value if the message has one, otherwise its first
        /// attribute, since an attribute-only message's text is what a caller asking by id means.
        /// </summary>
        private static string Resolve(string id, object args)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (id == PdfL10nStrings.DATE_TIME_ID) return FormatDateTime(args);

            var message = PdfL10nStrings.Get(id);

            if (message is null) return null;

            var argument = FirstArgument(args);

            if (message.Value is object) return Format(message.Value, argument);

            foreach (var attribute in message.Attributes)
            {
                return Format(attribute.Value, argument);
            }

            return null;
        }

        /// <summary>
        /// Applies every message in a subtree, including the root itself - pdf.js puts a
        /// <c>data-l10n-id</c> on the element it hands over as often as on one inside it.
        /// </summary>
        private void TranslateSubtree(HTMLElement root)
        {
            Apply(root);

            var localized = root.querySelectorAll("[data-l10n-id]");

            for (var i = 0; i < localized.length; i++)
            {
                Apply(localized[i].As<HTMLElement>());
            }
        }

        private void Apply(HTMLElement element)
        {
            // nodeType 1 is an element; a text or comment node has no attributes to read.
            if (element is null || element.nodeType != 1) return;

            var id = element.getAttribute("data-l10n-id");

            if (string.IsNullOrEmpty(id)) return;

            var args = ParseArguments(element.getAttribute("data-l10n-args"));

            if (id == PdfL10nStrings.DATE_TIME_ID)
            {
                var formatted = FormatDateTime(args);

                if (formatted is object) element.textContent = formatted;

                return;
            }

            var message = PdfL10nStrings.Get(id);

            // An id this package does not carry: leave the element exactly as pdf.js built it. Better
            // an untranslated label than an empty one.
            if (message is null) return;

            var argument = FirstArgument(args);

            if (message.Value is object) element.textContent = Format(message.Value, argument);

            foreach (var attribute in message.Attributes)
            {
                element.setAttribute(attribute.Key, Format(attribute.Value, argument));
            }
        }

        /// <summary>
        /// Substitutes the message's single argument, TNT-style. Every one of pdf.js's parameterised
        /// messages takes exactly one, so there is no name-to-position mapping to get wrong.
        /// </summary>
        private static string Format(string text, string argument)
        {
            if (text is null || argument is null || text.IndexOf("{0}") < 0) return text;

            return text.Replace("{0}", argument);
        }

        private static object ParseArguments(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                return es5.JSON.parse(json);
            }
            catch (Exception)
            {
                // Malformed args are pdf.js's to fix, and a label without its number is still better
                // than a thrown exception in the middle of building a page.
                return null;
            }
        }

        /// <summary>
        /// The one argument a parameterised message carries, as text. Read by shape rather than by
        /// name: the five ids that take an argument each take exactly one, so whichever property is
        /// there is the one wanted.
        /// </summary>
        private static string FirstArgument(object args)
        {
            if (args is null) return null;

            foreach (var name in ARGUMENT_NAMES)
            {
                var value = Script.Get<object>(args, name);

                if (value is object) return value.ToString();
            }

            return null;
        }

        // The argument names pdf.js's five parameterised messages use.
        private static readonly string[] ARGUMENT_NAMES = { "page", "type", "description", "generatedAltText" };

        /// <summary>
        /// Formats the one message whose Fluent value is a function call rather than text: the date on
        /// an annotation's popup. Left to the browser, which knows the locale's conventions - a
        /// hand-rolled format would be wrong in most of them.
        /// </summary>
        private static string FormatDateTime(object args)
        {
            if (args is null) return null;

            var value = Script.Get<object>(args, "dateObj");

            if (value is null) return null;

            // pdf.js passes a timestamp in milliseconds; a Date built from it formats in the user's
            // own locale, which is the only way to get the conventions right.
            if (!(value is double milliseconds)) return value.ToString();

            return new es5.Date(milliseconds).toLocaleString();
        }

        private void Observe(HTMLElement root)
        {
            _root = root;

            _observer = new MutationObserver((records, _) =>
            {
                foreach (var record in records)
                {
                    // An attribute change: the element's own id was replaced, which is how pdf.js
                    // re-labels a button that changed state.
                    if (record.type == "attributes")
                    {
                        Apply(record.target.As<HTMLElement>());

                        continue;
                    }

                    foreach (var added in record.addedNodes)
                    {
                        var element = added.As<HTMLElement>();

                        // Text nodes have no querySelectorAll, and a subtree is what pdf.js adds -
                        // a whole page, a whole annotation layer - so each addition is walked.
                        if (element is object && element.nodeType == 1) TranslateSubtree(element);
                    }
                }
            });

            Connect();
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

        /// <summary>
        /// Stops watching. pdf.js calls this before inserting a text, annotation or XFA layer, so the
        /// layer is translated once when it is complete rather than node by node as it is built.
        /// </summary>
        private void Pause()
        {
            if (_paused || _observer is null) return;

            _paused = true;
            _observer.disconnect();
        }

        /// <summary>Starts watching again, and catches up on whatever was inserted while paused.</summary>
        private void Resume()
        {
            if (!_paused) return;

            _paused = false;

            Connect();

            // The inserts that happened while paused produced no records, so the subtree is walked
            // once here. Without this, a text layer inserted between pause and resume is never
            // translated at all.
            if (_root is object) TranslateSubtree(_root);
        }

        private Task DestroyAsync()
        {
            _observer?.disconnect();
            _observer = null;
            _root     = null;
            _paused   = false;

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// The shape pdf.js expects of a localization implementation.
    ///
    /// Field names are pdf.js's, and are emitted verbatim - an <c>[ObjectLiteral]</c> field keeps the
    /// name it is declared with. The async members hand back promises because pdf.js awaits them.
    /// </summary>
    [ObjectLiteral]
    public class PdfL10nObject
    {
        /// <summary>The BCP-47 tag, lowercased.</summary>
        public Func<string> getLanguage;

        /// <summary><c>"ltr"</c> or <c>"rtl"</c>.</summary>
        public Func<string> getDirection;

        /// <summary>Localizes an element and everything under it.</summary>
        public Func<HTMLElement, IPromise> translate;

        /// <summary>Localizes one element, without taking on responsibility for watching it.</summary>
        public Func<HTMLElement, IPromise> translateOnce;

        /// <summary>
        /// One message, or an array of them, as text. <c>ids</c> is a string or an array of strings.
        /// </summary>
        public Func<object, object, string, IPromise> get;

        /// <summary>Stop watching for new elements to localize.</summary>
        public Action pause;

        /// <summary>Start watching again.</summary>
        public Action resume;

        /// <summary>Release everything held.</summary>
        public Func<IPromise> destroy;
    }
}
