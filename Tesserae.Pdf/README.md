# Tesserae.Pdf

A [Tesserae](https://github.com/curiosity-ai/tesserae) wrapper around
[Mozilla's pdf.js](https://github.com/mozilla/pdf.js), for Transpose C#-to-JavaScript apps.

```csharp
PdfJs.Viewer()
   .Url("/api/files/42.pdf")
   .FitWidth()
   .OnPageChanged(page => label.Text = $"Page {page} of {viewer.PageCount}")
```

pdf.js ships with the package - there is nothing to add to your page, no CDN to configure, and no
worker path to keep in sync.

## What is in it

**`PdfJs.Viewer()`** - a scrollable, searchable, linkable document viewer. Pages, links, text
selection, form fields, keyboard scrolling and full-text search all work with no further wiring.
It draws **no toolbar**: that is the part that has to look like the rest of your application, so the
component exposes the methods a toolbar calls (`NextPage`, `FitWidth`, `Rotate`, `Search`, ...) and
leaves the buttons to you.

**`PdfJs.ViewerChrome()`** - the same viewer with the toolbar already on it. Panel toggles, page
controls, a zoom stepper whose menu holds the fit modes, rotate and spread, an always-visible search
box with a `Fuzzy | Precise` switch (whose two meanings a host can redefine with `SearchOptions`, down
to "any of these words" through `FindOptions.AnyWord`), and a side panel showing the outline or the page
thumbnails. For
an application that wants a document reader and does not want to have an opinion about what one looks
like. It is a composition of `PdfJs.Viewer()`'s public surface and nothing else, and `chrome.Viewer`
hands that component back - so starting here and replacing the toolbar later costs the toolbar and
nothing more. It is built from Tesserae's own components - `Button`, `TextBox`, `SearchBox`, `Tree`,
`Grid`, `ContextMenu`, `HStack`/`VStack` - so it looks and behaves like the rest of your application,
and every colour resolves to a `--tss-*` theme variable, so `UI.Theme.Dark()` and your own
`Theme.Build()` come through with no work. It sheds controls into an overflow menu as it narrows
rather than clipping them, wraps its search box onto a second row on a phone, and grows its touch
targets on a coarse pointer. `Border()` frames it in the theme's border colour for the case where
nothing around it draws the edge - off by default, since a chrome filling a window or sitting in a
`Card` has one already.

**`PdfJs.PageCanvas()`** - one page painted into a canvas. A thumbnail, a preview tile, a page in a
contact sheet. Give it a URL and it opens its own document; give it a `PdfDocument` and it borrows
one, which is how a rail of thumbnails shares a single document rather than opening twelve.

**`PdfJs.OpenAsync(source)`** - a document with nothing on screen, for the things that need no
viewer: extracting text, reading metadata and permissions, listing an outline, rendering a page into
a canvas of your own.

Alongside those: encrypted documents (`OnPassword`), embedded JavaScript (`EnableScripting`),
localization through Tesserae's TNT table, typed failures (`PdfError.Kind`), and `SaveAsync` for
getting a filled form's bytes back out.

The [sample gallery](https://curiosity-ai.github.io/tesserae-pdf/) has a page per feature.

## Getting started

Add the package. Its build copies pdf.js into your app's output under `assets/js/pdf`, and the
components load it from there on first use - nothing is fetched until a viewer mounts.

The package's own JavaScript is on demand too, all the way down: `index.html` does not script it, so
an application fetches the entry itself, just before the first `PdfJs` call - one line, and a
natural place for it is the route or view that shows documents:

```csharp
await Transpose.Require.RequireAsync(Transpose.RequireKind.Module, "./Tesserae.Pdf.js");
```

Until that has run, nothing in the `Tesserae.Pdf` namespace exists. An application that shows a PDF on
every page can make the call first thing in `Main`, which is what the sample gallery does.

```csharp
var viewer = PdfJs.Viewer();

viewer
   .Url("report.pdf")
   .FitWidth()
   .OnDocumentLoaded(document => Console.WriteLine($"{document.PageCount} pages"))
   .OnError(error => ShowMessage(error.Message));

// Give it a height, or a parent that has one: the viewer fills its container and scrolls inside it.
MountToBody(viewer.H(600).WS());
```

Serving pdf.js from somewhere else - a CDN, a shared static host - is one setting, and it moves the
worker and every asset directory with it:

```csharp
PdfJs.AssetsPath = "https://static.example.com/pdfjs";
```

## Things worth knowing

**The chrome is the shortcut, not the replacement.** `PdfJs.ViewerChrome()` and `PdfJs.Viewer()` are
the same component with and without a toolbar. Reach for the chrome when a reader is what you want;
reach for the viewer when the controls have to be yours, or when there is barely a control at all - a
preview pane, a print dialog, a thumbnail with a click-to-zoom. The chrome can also be pared back
(`ShowZoom(false)`, `Tabs(thumbnails: false)`, `ShowSearch(false)`) rather than swapped out.

**The chrome owns the viewer's event slots.** `OnPageChanged` and friends on `PdfViewer` are single
slots - a second call replaces the first - so register on the chrome (`OnPanelChanged`,
`OnSearchModeChanged`) or on the shared event bus. The chrome deliberately uses the bus itself so
`chrome.Viewer.OnPageChanged(...)` stays free for you.

**Give the viewer a height.** It fills its container and scrolls inside it, so in a container of no
height it renders nothing - which looks like a document that failed to load.

**Prefer the fit modes to an explicit zoom.** `FitWidth()` and its siblings are re-applied when the
container resizes; `Zoom(1.4)` is a number and stays one. (pdf.js resolves a fit mode once, into a
number, and does not re-resolve it - the component re-applies it for you.)

**A component owns what it opened.** A viewer releases its document when it is torn down, including
the teardown that happens when it leaves the DOM; being re-added rebuilds it and restores the page,
zoom, rotation and layout. A document you opened yourself with `PdfJs.OpenAsync` is yours to release
with `DestroyAsync`.

**`AnnotationMode.EnableStorage` is not "EnableForms and more".** In a viewer it makes the form
*non*-interactive, silently - pdf.js tests for exactly `EnableForms` when deciding whether to build
real inputs. `EnableForms` is the default and the right choice for a viewer; `EnableStorage` belongs
on a page render, where it means "include the values already entered".

**Whether a viewer has an annotation editor is decided before it is built.** Call
`AnnotationEditor(AnnotationEditorMode.None)` while configuring the component to build the editor
layer; afterwards tools switch freely, but a viewer built without it cannot grow one.

**A search scrolls the viewer, not your page.** pdf.js 6 brings a match into view with the native
`element.scrollIntoView`, which scrolls every scrollable ancestor up to the window - so in a viewer
embedded in a scrolling page it moves your scrollbar as well as the document's. The component
replaces that one call with the bounded equivalent, so you do not have to do anything about it.

**Watch for "Setting up fake worker" in the console.** It means the worker could not be loaded and
pdf.js is parsing on the main thread - documents still render, and the UI freezes while they do.

## Localization

Everything this package puts on screen is translated, in twenty languages, and a consuming
application needs one line to switch it on.

The chrome's own labels, and the text pdf.js expects a localization implementation to supply - page
landmarks a screen reader announces, alt text on annotation icons, tooltips on the editor's buttons,
written as `data-l10n-id` attributes on the elements it builds - all go through **TNT**, the same
translation table Tesserae itself uses. The package extracts its own strings with
[TNTC](https://www.nuget.org/packages/TNTC) and ships the result beside pdf.js, so a German
application gets a German viewer without translating anything of ours.

**Merge our table into yours; the package cannot install it.** `TNT.T.SetTranslation` takes one
dictionary for the whole application and replaces what was there, and TNT has no way to read it
back - so a package that called it would throw away its host's translations. Load ours first and put
yours over it, and anything you word differently stays yours:

```csharp
var table = await PdfJs.LoadTranslationsAsync("de");   // or your current language

foreach (var entry in myOwnTranslations) table[entry.Key] = entry.Value;

TNT.T.SetTranslation(table);
```

The tables are JSON files next to the pdf.js bundle - `assets/js/pdf/l10n/de.json`, put there by the
package's build targets - so they move with `PdfJs.AssetsPath` and need no content-type mapping of
their own. Each is a flat array of `[english, translated]` pairs, which is also all
`LoadTranslationsAsync` does with it, so an application whose own translations are loaded before any
`Tesserae.Pdf` type exists (a host that keeps the package out of its boot payload) can fetch and
merge that file itself instead.

**Or merge them into your own tables at build time**, which costs no code at run time at all: the
package's targets declare every table as a `@(TranslationTable)` MSBuild item, carrying its
`PackageId`, so a target of your own can fold them into the file your application already ships -
your entries last, so your wording wins. That is what mosaik does.

A language we have no table for answers an empty dictionary rather than throwing, and every key then
falls back to its English text - which is what TNT does with a key it cannot find. The twenty are
`cs, de, el, es, fr, he, hi, it, ja, ko, ms, ne, nl, pl, pt, ru, sr, sv, uk, zh`.

`PdfJs.Language` is a separate thing: it tells pdf.js which language it is looking at, which decides
text direction and how dates inside annotations are formatted. `L10n(customObject)` replaces the
bridge entirely, and `WithoutOwnLocalization()` falls back to pdf.js's built-in English.

## Requirements

- .NET SDK 10 and the [Transpose](https://github.com/curiosity-ai/transpose) compiler.
- Node, for a build from source: pdf.js is not vendored, it is bundled from the pinned
  `pdfjs-dist` npm package on every build.

## Licensing

This package is MIT. pdf.js is Apache-2.0, and the bundled distribution carries its license plus the
separate licenses of the WebAssembly decoders, the substitute fonts and the ICC profile it ships -
all of them in `assets/js/pdf/LICENSE.txt` and beside the files they cover.
