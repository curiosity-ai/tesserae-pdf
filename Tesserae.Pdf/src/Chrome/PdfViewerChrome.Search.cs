using System;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.Pdf.PdfChromeElements;
using static TNT.T;

namespace Tesserae.Pdf
{
    /// <summary>
    /// The search box, and the state machine behind it.
    ///
    /// <b>It is a Tesserae <see cref="SearchBox"/></b>, which turns out to be most of the design
    /// already: the magnifier, the keyboard-shortcut chip the mockup draws as <c>⌘F</c>,
    /// search-as-you-type with its own debounce, the shortcut handler, and an invalid state for the
    /// red "no matches" appearance. What the chrome adds beside it is the running count, the two match
    /// steppers, the clear, and the <c>Fuzzy | Precise</c> pill - the mockup puts those inside the
    /// field's border; here they sit next to it, because reaching inside a component to add trailing
    /// controls is a coupling that buys 4px.
    ///
    /// <b>Always visible, rather than a bar that opens.</b> A find bar that appears on ⌘F is right for
    /// a browser, where finding is occasional. In a document reader it is most of what people do, and
    /// a control that has to be summoned is a control most readers never learn exists.
    ///
    /// <b>Fuzzy | Precise instead of three checkboxes.</b> pdf.js's matching has three independent
    /// switches - case, whole words, diacritics - and a reader who wants any of them wants all three:
    /// they are looking for this exact word, not for something like it. <see cref="FindOptions"/> is
    /// still there on the viewer for a host that needs them separately.
    ///
    /// <b>Results arrive over time, and that shapes everything here.</b> pdf.js reads a page's text
    /// when it reaches it, so a search on a long document reports a growing count before it reports an
    /// outcome. The count is shown as it grows, and the "no matches" appearance is only ever taken
    /// from a control-state event that says so - never from "the count is still zero", which is what
    /// every long document looks like for its first few hundred milliseconds.
    /// </summary>
    public sealed partial class PdfViewerChrome
    {
        private SearchBox _searchBox;
        private Stack     _searchRow;
        private TextBlock _searchCount;
        private TextBlock _searchNote;
        private Button    _searchPrevious;
        private Button    _searchNext;
        private Button    _searchClear;
        private Button    _fuzzySegment;
        private Button    _preciseSegment;

        private string    _query = "";

        /// <summary>
        /// Set while the chrome is writing into the search box.
        ///
        /// <b>A Tesserae input raises the same event for a programmatic write as for a keystroke.</b>
        /// So <c>SetText</c> inside a handler for that event is a loop - and this one is a tight one:
        /// clearing the box reports an empty query, which clears the box. The guard is on the write
        /// rather than on the handler, so the handler stays the single place that reacts to a change.
        /// </summary>
        private bool _writingSearchBox;
        private string    _runningQuery;
        private int       _matchCurrent;
        private int       _matchTotal;
        private FindState _findState = FindState.Pending;
        private bool      _searchSettled;

        /* ------------------------------------------------------------------ public */

        /// <summary>How strictly the search box matches. Re-runs the current query.</summary>
        public PdfViewerChrome SearchMode(PdfSearchMode mode)
        {
            if (_searchMode == mode) return this;

            _searchMode = mode;

            UpdateSearchState();

            if (_query.Length > 0) RunSearch();

            _onSearchModeChanged?.Invoke(_searchMode);

            return this;
        }

        /// <summary>Called when the reader switches between Fuzzy and Precise.</summary>
        public PdfViewerChrome OnSearchModeChanged(Action<PdfSearchMode> handler)
        {
            _onSearchModeChanged = handler;

            return this;
        }

        /// <summary>
        /// Puts a query in the box and runs it, as though it had been typed. Pass null or an empty
        /// string to clear.
        /// </summary>
        public PdfViewerChrome Search(string query)
        {
            _query = query ?? "";

            WriteSearchBox(_query);

            if (_query.Length == 0)
            {
                ClearSearch();
            }
            else
            {
                RunSearch();
            }

            return this;
        }

        /// <summary>
        /// Puts a query in the box <b>without</b> running it, ready for the reader to press Enter or
        /// the next-match button.
        ///
        /// For the case a host has and this chrome cannot infer: a reader who arrived from a search
        /// elsewhere in the application, at a particular page. The term is the context they came with,
        /// but running it would scroll them to its first match and off the page they asked for - so the
        /// box is filled and the document left alone. <see cref="Search"/> is the other half of that
        /// choice, for when there is no page to protect.
        /// </summary>
        public PdfViewerChrome SearchQuery(string query)
        {
            _query = query ?? "";

            WriteSearchBox(_query);

            // A query that has not run is neither found nor not-found, and the previous one's counts
            // do not describe it.
            ResetSearchResults();

            UpdateSearchState();

            return this;
        }

        /// <summary>Moves keyboard focus into the search box.</summary>
        public PdfViewerChrome FocusSearch()
        {
            _searchBox?.Focus();

            return this;
        }

        /// <summary>Empties the box and drops the highlighting.</summary>
        public PdfViewerChrome ClearSearch()
        {
            _query        = "";
            _runningQuery = null;

            WriteSearchBox("");

            ResetSearchResults();

            _viewer.ClearSearch();

            return this;
        }

        /* --------------------------------------------------------------- assembly */

        /// <summary>
        /// The search box and the controls beside it, as one row that gives up width before anything
        /// else in the toolbar does.
        /// </summary>
        private Stack BuildSearchRow()
        {
            _searchBox = SearchBox("Find in document".t())
               .SetIcon(UIcons.Search)
               .SearchAsYouType()
               .Class("tsspdf-search");

            // The component draws the shortcut as a chip at the trailing edge of the field, which is
            // exactly where the design puts it - and takes the keystroke itself, so the chrome's own
            // root handler is only there for the case where focus is inside the document.
            _searchBox.SetKeyboardShortcut(FindShortcutKeys());
            _searchBox.OnShortcut(() => FocusSearch());

            _searchBox.OnSearch((_, text) => HandleSearchInput(text));

            _searchCount = TextBlock("").Class("tsspdf-count");
            _searchNote  = TextBlock("").Class("tsspdf-note");

            _searchPrevious = StepButton(UIcons.AngleUp,   "Previous match".t(), () => _viewer.FindPrevious());
            _searchNext     = StepButton(UIcons.AngleDown, "Next match".t(),     () => _viewer.FindNext());

            _searchClear = StepButton(UIcons.CrossSmall, "Clear".t(), () =>
            {
                ClearSearch();
                FocusSearch();
            });

            _fuzzySegment = Segment("Fuzzy".t(), null,
                "Ignore case, accents and word boundaries".t(), () => SearchMode(PdfSearchMode.Fuzzy));

            _preciseSegment = Segment("Precise".t(), null,
                "Match case, whole words, diacritics respected".t(), () => SearchMode(PdfSearchMode.Precise));

            var modes = HStack().Class("tsspdf-seg tsspdf-seg-sm").AlignItems(ItemAlign.Center)
               .Gap(2.px()).Children(_fuzzySegment, _preciseSegment);

            _searchRow = HStack().Class("tsspdf-searchrow").AlignItems(ItemAlign.Center).Gap(2.px())
               .Children(_searchBox.Grow(), _searchNote, _searchCount,
                         _searchPrevious, _searchNext, _searchClear, modes);

            return _searchRow;
        }

        /// <summary>
        /// The shortcut as the reader's own keyboard writes it, for the component's chip.
        ///
        /// Read off <c>navigator.platform</c>, which is deprecated and still the only thing that
        /// answers this question in every browser that ships. Getting it wrong costs a wrong hint in a
        /// 10px chip, so it is not worth a user-agent-data round trip.
        /// </summary>
        private static string[] FindShortcutKeys()
        {
            var platform = navigator.platform ?? "";

            return platform.ToLower().Contains("mac")
                ? new[] { "⌘", "F" }
                : new[] { "Ctrl", "F" };
        }

        /* ---------------------------------------------------------------- typing */

        /// <summary>
        /// Takes what the reader typed. <see cref="SearchBox.SearchAsYouType"/> debounces this, so it
        /// arrives once per pause rather than once per keystroke - which matters, because each search
        /// reads the whole document.
        /// </summary>
        private void WriteSearchBox(string text)
        {
            if (_searchBox is null) return;

            _writingSearchBox = true;

            try
            {
                _searchBox.SetText(text);
            }
            finally
            {
                _writingSearchBox = false;
            }
        }

        private void HandleSearchInput(string text)
        {
            if (_writingSearchBox) return;

            _query = text ?? "";

            if (_query.Length == 0)
            {
                ClearSearch();

                return;
            }

            // A search that has not run yet is neither found nor not-found, so drop the outcome now
            // rather than leaving the red "no matches" border under a query nobody has looked for.
            _searchSettled = false;
            _findState     = FindState.Pending;
            _matchCurrent  = 0;
            _matchTotal    = 0;

            UpdateSearchState();
            RunSearch();
        }

        private void RunSearch()
        {
            if (_query.Length == 0) return;

            _runningQuery  = _query;
            _searchSettled = false;

            var precise = _searchMode == PdfSearchMode.Precise;

            _viewer.Search(_query, new FindOptions
            {
                CaseSensitive   = precise,
                EntireWord      = precise,
                MatchDiacritics = precise,
                HighlightAll    = true,
            });
        }

        /* ---------------------------------------------------------------- results */

        /// <summary>
        /// Takes a report from the find controller.
        ///
        /// <paramref name="carriesState"/> is false for a running-count event, which carries no state
        /// of its own - so the last outcome is kept rather than assumed to be Pending. That is the same
        /// trap <see cref="PdfViewer"/> documents: count events arrive between control-state events
        /// and sometimes after them, and treating one as "still searching" overwrites "found" a moment
        /// after the search succeeded.
        /// </summary>
        private void ApplyMatches(IMatchesCount matches, bool carriesState, FindState state)
        {
            if (matches is object)
            {
                _matchCurrent = matches.current;
                _matchTotal   = matches.total;
            }

            if (carriesState)
            {
                _findState     = state;
                _searchSettled = state != FindState.Pending;
            }

            UpdateSearchState();
            UpdateMatchPages();
        }

        private void ResetSearchResults()
        {
            _matchCurrent  = 0;
            _matchTotal    = 0;
            _findState     = FindState.Pending;
            _searchSettled = false;

            ClearMatchPages();

            UpdateSearchState();
        }

        /// <summary>
        /// Paints the whole row from the state above: which of the count and the note is showing,
        /// whether the steppers are there, whether the field reads as invalid, and which of the two
        /// mode segments is selected.
        ///
        /// One function rather than a change per event, because the four states this control has are
        /// distinguished by combinations - a non-empty query with a settled zero count is the only one
        /// that is red - and a set of individual updates gets a combination wrong sooner or later.
        /// </summary>
        private void UpdateSearchState()
        {
            if (_searchRow is null) return;

            Toggle(_fuzzySegment,   PdfChromeStyles.ON, _searchMode == PdfSearchMode.Fuzzy);
            Toggle(_preciseSegment, PdfChromeStyles.ON, _searchMode == PdfSearchMode.Precise);

            SetPressed(_fuzzySegment,   _searchMode == PdfSearchMode.Fuzzy);
            SetPressed(_preciseSegment, _searchMode == PdfSearchMode.Precise);

            var searching = _query.Length > 0;
            var noMatches = searching && _searchSettled && _matchTotal == 0;
            var hasCount  = searching && _matchTotal > 0;

            // The component's own invalid state, plus a class for the parts of the red appearance it
            // does not cover. Assigned only on a change: the setter re-renders the field.
            if (_searchBox.IsInvalid != noMatches) _searchBox.IsInvalid = noMatches;

            Toggle(_searchBox, PdfChromeStyles.NO_MATCHES, noMatches);

            Show(_searchCount, hasCount);
            Show(_searchNote,  noMatches);

            // The steppers appear with the count, not with the query: "previous match" means nothing
            // while there are none, and a disabled pair of chevrons is two controls' worth of space
            // spent saying so. The clear button is the exception - it is the way out of a query that
            // found nothing, which is exactly when it is needed.
            Show(_searchPrevious, hasCount);
            Show(_searchNext,     hasCount);
            Show(_searchClear,    searching);

            if (hasCount)
            {
                _searchCount.Text = _matchCurrent + " / " + _matchTotal;

                // Wrapping is why the view jumped backwards, and a reader who is not told assumes
                // something broke. Too small a thing for a banner, big enough for the tooltip.
                _searchCount.Render().title = _findState == FindState.Wrapped
                    ? "Continued from the start of the document".t()
                    : "";
            }

            if (noMatches)
            {
                // "Try Fuzzy" is only advice if Fuzzy is not what just failed.
                _searchNote.Text = _searchMode == PdfSearchMode.Precise
                    ? "No matches - try Fuzzy".t()
                    : "No matches".t();
            }
        }
    }
}
