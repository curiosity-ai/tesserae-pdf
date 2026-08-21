using System;
using System.Threading.Tasks;
using Transpose;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.Pdf.PdfChromeElements;
using static TNT.T;

namespace Tesserae.Pdf
{
    /// <summary>
    /// The search box, and the state machine behind it.
    ///
    /// <b>Always visible, rather than a bar that opens.</b> A find bar that appears on ⌘F is right for
    /// a browser, where finding is occasional. In a document reader it is most of what people do, and
    /// a control that has to be summoned is a control most readers never learn exists.
    ///
    /// <b>Fuzzy | Precise instead of three checkboxes.</b> pdf.js's matching has three independent
    /// switches - case, whole words, diacritics - and a reader who wants any of them wants all three:
    /// they are looking for this exact word, not for something like it. So the two named modes are
    /// what the chrome offers, and <see cref="FindOptions"/> stays there for a host that needs the
    /// switches separately.
    ///
    /// <b>Results arrive over time, and that shapes everything here.</b> pdf.js reads a page's text
    /// when it reaches it, so a search on a long document reports a growing count before it reports an
    /// outcome. The count is shown as it grows, and the "no matches" appearance is only ever taken
    /// from a control-state event that says so - never from "the count is still zero", which is what
    /// every long document looks like for its first few hundred milliseconds.
    /// </summary>
    public sealed partial class PdfViewerChrome
    {
        private HTMLElement      _searchBox;
        private HTMLInputElement _searchInput;
        private HTMLElement      _searchCount;
        private HTMLElement      _searchHint;
        private HTMLElement      _searchNote;

        private HTMLButtonElement _searchPrevious;
        private HTMLButtonElement _searchNext;
        private HTMLButtonElement _searchClear;
        private HTMLButtonElement _fuzzySegment;
        private HTMLButtonElement _preciseSegment;

        private string    _query        = "";
        private string    _runningQuery;
        private int       _matchCurrent;
        private int       _matchTotal;
        private FindState _findState = FindState.Pending;
        private bool      _searchSettled;

        private int _searchGeneration;

        /// <summary>
        /// How long the box waits after a keystroke before searching.
        ///
        /// Long enough that typing a word is one search rather than five - each of which reads the
        /// whole document - and short enough that it still feels like as-you-type. Enter skips it.
        /// </summary>
        private const int SEARCH_DEBOUNCE_MS = 220;

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

            if (_searchInput is object) _searchInput.value = _query;

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

        /// <summary>Moves keyboard focus into the search box and selects what is in it.</summary>
        public PdfViewerChrome FocusSearch()
        {
            if (_searchInput is object)
            {
                _searchInput.focus();
                _searchInput.select();
            }

            return this;
        }

        /// <summary>Empties the box and drops the highlighting.</summary>
        public PdfViewerChrome ClearSearch()
        {
            _query        = "";
            _runningQuery = null;

            if (_searchInput is object) _searchInput.value = "";

            ResetSearchResults();

            _viewer.ClearSearch();

            return this;
        }

        /* --------------------------------------------------------------- assembly */

        private HTMLElement BuildSearchBox()
        {
            _searchBox = Box("tsspdf-omni");

            _searchBox.appendChild(Glyph("tsspdf-omni-icon", PdfChromeIcons.SEARCH_14));

            _searchInput = document.createElement("input").As<HTMLInputElement>();

            _searchInput.className   = "tsspdf-omni-input";
            _searchInput.type        = "text";
            _searchInput.value       = _query;
            _searchInput.placeholder = "Find in document".t();

            _searchInput.setAttribute("aria-label", "Find in document".t());

            _searchInput.addEventListener("input",   new Action<Event>(_ => HandleSearchInput()));
            _searchInput.addEventListener("keydown", new Action<KeyboardEvent>(HandleSearchKeyDown));

            _searchInput.addEventListener("focus", new Action<Event>(_ => UpdateSearchState()));
            _searchInput.addEventListener("blur",  new Action<Event>(_ => UpdateSearchState()));

            _searchBox.appendChild(_searchInput);

            _searchCount = Text("tsspdf-omni-count", "");
            _searchNote  = Text("tsspdf-omni-note", "");
            _searchHint  = Text("tsspdf-omni-hint", FindShortcutHint());

            _searchBox.appendChild(_searchNote);
            _searchBox.appendChild(_searchCount);
            _searchBox.appendChild(_searchHint);

            _searchPrevious = Button("tsspdf-omni-step", "Previous match".t(), () => _viewer.FindPrevious());
            _searchNext     = Button("tsspdf-omni-step", "Next match".t(),     () => _viewer.FindNext());

            _searchPrevious.innerHTML = PdfChromeIcons.CHEVRON_UP_13;
            _searchNext.innerHTML     = PdfChromeIcons.CHEVRON_DOWN_13;

            _searchBox.appendChild(_searchPrevious);
            _searchBox.appendChild(_searchNext);

            _searchClear = Button("tsspdf-omni-clear", "Clear".t(), () =>
            {
                ClearSearch();
                FocusSearch();
            });

            _searchClear.innerHTML = PdfChromeIcons.CLOSE_12;

            _searchBox.appendChild(_searchClear);

            var modes = Box("tsspdf-seg tsspdf-seg-sm");

            _fuzzySegment = Segment(null, "Fuzzy".t(),
                "Ignore case, accents and word boundaries".t(), () => SearchMode(PdfSearchMode.Fuzzy));

            _preciseSegment = Segment(null, "Precise".t(),
                "Match case, whole words, diacritics respected".t(), () => SearchMode(PdfSearchMode.Precise));

            modes.appendChild(_fuzzySegment);
            modes.appendChild(_preciseSegment);

            _searchBox.appendChild(modes);

            return _searchBox;
        }

        /// <summary>
        /// The shortcut as the reader's own keyboard writes it. ⌘F on a Mac, Ctrl+F everywhere else.
        ///
        /// Read off <c>navigator.platform</c>, which is deprecated and still the only thing that
        /// answers this question in every browser that ships. Getting it wrong costs a wrong hint in a
        /// 10px grey, so it is not worth a user-agent-data round trip.
        /// </summary>
        private static string FindShortcutHint()
        {
            var platform = navigator.platform ?? "";

            return platform.ToLower().Contains("mac") ? "\u2318F" : "Ctrl+F";
        }

        /* ---------------------------------------------------------------- typing */

        private void HandleSearchInput()
        {
            _query = _searchInput.value ?? "";

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

            var generation = ++_searchGeneration;

            DebouncedSearchAsync(generation).FireAndForget();
        }

        private async Task DebouncedSearchAsync(int generation)
        {
            await Task.Delay(SEARCH_DEBOUNCE_MS);

            // Superseded by a later keystroke, or the chrome has gone.
            if (generation != _searchGeneration || _disposed) return;

            RunSearch();
        }

        private void HandleSearchKeyDown(KeyboardEvent e)
        {
            if (e.key == "Enter")
            {
                e.preventDefault();

                // Enter on a query already running means "the next one"; on a query the debounce has
                // not got to yet it means "now, please".
                if (_query.Length > 0 && _query == _runningQuery)
                {
                    if (e.shiftKey)
                    {
                        _viewer.FindPrevious();
                    }
                    else
                    {
                        _viewer.FindNext();
                    }
                }
                else if (_query.Length > 0)
                {
                    _searchGeneration++;

                    RunSearch();
                }
            }
            else if (e.key == "Escape")
            {
                e.preventDefault();

                ClearSearch();
            }

            // The chrome's own ⌘F handler is on the root, and the viewer scrolls on the arrow keys.
            // Neither should hear a reader typing into this box.
            e.stopPropagation();
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
        /// of its own - so the last outcome is kept rather than assumed to be Pending. That is the same trap
        /// <see cref="PdfViewer"/> documents: count events arrive between control-state events and
        /// sometimes after them, and treating one as "still searching" overwrites "found" a moment
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
        /// Paints the whole box from the state above: which of the count, the hint and the note is
        /// showing, whether the steppers are usable, and which of the two mode segments is selected.
        ///
        /// One function rather than a change per event, because the four states this control has are
        /// distinguished by combinations - a non-empty query with a settled zero count is the only one
        /// that is red - and a set of individual updates gets a combination wrong sooner or later.
        /// </summary>
        private void UpdateSearchState()
        {
            if (_searchBox is null) return;

            Toggle(_fuzzySegment,   PdfChromeStyles.ON, _searchMode == PdfSearchMode.Fuzzy);
            Toggle(_preciseSegment, PdfChromeStyles.ON, _searchMode == PdfSearchMode.Precise);

            SetPressed(_fuzzySegment,   _searchMode == PdfSearchMode.Fuzzy);
            SetPressed(_preciseSegment, _searchMode == PdfSearchMode.Precise);

            var searching = _query.Length > 0;
            var noMatches = searching && _searchSettled && _matchTotal == 0;
            var hasCount  = searching && _matchTotal > 0;

            Toggle(_searchBox, PdfChromeStyles.FOCUS, document.activeElement == _searchInput && !noMatches);
            Toggle(_searchBox, PdfChromeStyles.NO_MATCHES, noMatches);

            Show(_searchHint,  !searching);
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
                _searchCount.textContent = _matchCurrent + " / " + _matchTotal;

                // Wrapping is why the view jumped backwards, and a reader who is not told assumes
                // something broke. Too small a thing for a banner, big enough for the tooltip.
                _searchCount.title = _findState == FindState.Wrapped
                    ? "Continued from the start of the document".t()
                    : "";
            }

            if (noMatches)
            {
                // "Try Fuzzy" is only advice if Fuzzy is not what just failed.
                _searchNote.textContent = _searchMode == PdfSearchMode.Precise
                    ? "No matches - try Fuzzy".t()
                    : "No matches".t();
            }

        }
    }
}
