using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Pdf
{
    public static partial class PdfJs
    {
        private const string TRANSLATIONS_FOLDER = "l10n";

        private static readonly Dictionary<string, Task<Dictionary<string, string>>> _translations =
            new Dictionary<string, Task<Dictionary<string, string>>>();

        /// <summary>
        /// The translations this package ships for a language - every label the chrome draws and every
        /// message pdf.js asks the localization bridge for, keyed by its English text.
        ///
        /// <b>Merge the result into your own TNT table; this does not install it.</b>
        /// <c>TNT.T.SetTranslation</c> takes one dictionary for the whole application and replaces
        /// whatever was there, so a package that called it would throw away its host's translations -
        /// which is also why the package cannot merge for you: TNT has no way to read the table back.
        /// Put these entries in first and your own over them, so an application that words something
        /// differently keeps its wording:
        ///
        /// <code>
        /// var table = await PdfJs.LoadTranslationsAsync("de");
        ///
        /// foreach (var entry in myOwnTranslations) table[entry.Key] = entry.Value;
        ///
        /// TNT.T.SetTranslation(table);
        /// </code>
        ///
        /// The tables are JSON files beside the pdf.js bundle (<c>assets/js/pdf/l10n/de.json</c>),
        /// copied there by the package's build targets, so they move with
        /// <see cref="AssetsPath"/> and need no <c>Content-Type</c> mapping of their own. A language
        /// this package has no table for answers an empty dictionary rather than throwing - every key
        /// then falls back to its English text, which is what TNT does with a key it cannot find.
        ///
        /// Fetched at most once per language; a language that failed to load is asked for again on the
        /// next call, so a viewer opened after the network came back is translated.
        /// </summary>
        /// <param name="language">
        /// A language code or BCP-47 tag (<c>de</c>, <c>de-DE</c>); the region is ignored. Defaults to
        /// <see cref="Language"/>.
        /// </param>
        public static Task<Dictionary<string, string>> LoadTranslationsAsync(string language = null)
        {
            var code = LanguageCodeOf(language ?? Language);

            if (!_translations.TryGetValue(code, out var loading))
            {
                loading = LoadTranslationsCoreAsync(code, BaseUrl + "/" + TRANSLATIONS_FOLDER + "/" + code + ".json");

                _translations[code] = loading;
            }

            return loading;
        }

        private static async Task<Dictionary<string, string>> LoadTranslationsCoreAsync(string code, string url)
        {
            var table = new Dictionary<string, string>();

            var response = await GetTextAsync(url);

            if (response.Status == 0)
            {
                // The fetch itself failed rather than the file being absent, so let the next caller retry.
                _translations.Remove(code);

                console.warn("Tesserae.Pdf: could not fetch " + url + ", the viewer's labels stay in English");

                return table;
            }

            if (response.Status != 200) return table;

            var rows = (es5.Array<es5.Array<string>>)es5.JSON.parse(response.Text);

            for (double index = 0; index < rows.length; index++)
            {
                var pair = rows[index];

                if (pair is object && pair.length > 1) table[pair[0]] = pair[1];
            }

            return table;
        }

        /// <summary>The language part of a tag, lowercased: <c>de-DE</c> and <c>DE</c> are both <c>de</c>.</summary>
        private static string LanguageCodeOf(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return "en";

            var separator = language.IndexOf('-');

            return (separator > 0 ? language.Substring(0, separator) : language).Trim().ToLower();
        }

        /// <summary>
        /// A plain GET, through <c>XMLHttpRequest</c> rather than <c>fetch</c>: a rejected fetch promise
        /// arrives here wrapped (see <c>PromiseHelper</c>) and its network failure is indistinguishable
        /// from a parse error, while an XHR says which happened - <c>status</c> 0 for a failed request,
        /// 404 for a language this package does not ship.
        /// </summary>
        private static Task<(int Status, string Text)> GetTextAsync(string url)
        {
            var completion = new TaskCompletionSource<(int Status, string Text)>();
            var request    = new XMLHttpRequest();

            request.open("GET", url, true);

            request.onload  = _ => completion.SetResult((request.status, request.responseText));
            request.onerror = _ => completion.SetResult((0, null));

            request.send();

            return completion.Task;
        }
    }
}
