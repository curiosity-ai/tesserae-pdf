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
controls, a zoom stepper with a menu, the two fit modes, rotate and spread, an always-visible search
box with a `Fuzzy | Precise` switch, and a tabbed outline / thumbnails panel. For an application that
wants a document reader and does not want to have an opinion about what one looks like. It is a
composition of `PdfJs.Viewer()`'s public surface and nothing else, and `chrome.Viewer` hands that
component back - so starting here and replacing the toolbar later costs the toolbar and nothing more.
It is built from Tesserae's own components - `Button`, `TextBox`, `SearchBox`, `Pivot`, `Tree`,
`Grid`, `ContextMenu`, `HStack`/`VStack` - so it looks and behaves like the rest of your application,
and every colour resolves to a `--tss-*` theme variable, so `UI.Theme.Dark()` and your own
`Theme.Build()` come through with no work. It sheds controls into an overflow menu as it narrows
rather than clipping them, and grows its touch targets on a coarse pointer.

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

## Localizing pdf.js's own strings

pdf.js puts `data-l10n-id` attributes on the elements it builds - page landmarks a screen reader
announces, alt text on annotation icons, tooltips on the editor's buttons - and expects something to
turn them into text. This package answers them through **TNT**, the same translation table Tesserae
itself uses, so a German application gets a German viewer from the dictionary that already
translates its own buttons. There is nothing to configure.

There is one gap a package cannot close by itself: your `tnt extract` scans your source, and these
strings live in a NuGet package it never sees. **So add the keys below to your translation source**
(or to whatever merges into `TNT.T.SetTranslation`). They are the English text of every message
pdf.js can ask for, and `{0}` is TNT's own placeholder convention.

`PdfJs.Language` tells pdf.js which language it is looking at - which decides text direction, and
how dates inside annotations are formatted. `L10n(customObject)` replaces the bridge entirely, and
`WithoutOwnLocalization()` falls back to pdf.js's built-in English.

`PdfJs.ViewerChrome()` labels its own controls through the same table, and they are in the same
position - add these too if you use it.

<details>
<summary>The 40 strings the chrome uses</summary>

| Key |
| --- |
| `({0} of {1})` |
| `Actual size` |
| `Automatic` |
| `Clear` |
| `Continued from the start of the document` |
| `Document outline` |
| `Find in document` |
| `Fit content` |
| `Fit page` |
| `Fit the page width` |
| `Fit the whole page` |
| `Fuzzy` |
| `Ignore case, accents and word boundaries` |
| `Match case, whole words, diacritics respected` |
| `Next match` |
| `Next page` |
| `No document.` |
| `No matches` |
| `No matches - try Fuzzy` |
| `of {0}` |
| `Outline` |
| `Page` |
| `Page {0}` |
| `Page {0} of {1}` |
| `pages {0}` |
| `pages {0} +{1} more` |
| `Precise` |
| `Previous match` |
| `Previous page` |
| `Rotate right` |
| `Searching...` |
| `Show in outline` |
| `This document has no outline.` |
| `Thumbnails` |
| `Two-page spread` |
| `Zoom` |
| `Zoom in` |
| `Zoom out` |
| `{0} matches` |
| `{0} pages` |

</details>

<details>
<summary>The 45 translatable strings pdf.js can ask for</summary>

| Key |
| --- |
| `[{0} Annotation]` |
| `Add comment` |
| `Alt text` |
| `Alt text added` |
| `Blue` |
| `Bottom left corner — resize` |
| `Bottom middle — resize` |
| `Bottom right corner — resize` |
| `Change color` |
| `Change drawing color` |
| `Change text color` |
| `Color choices` |
| `Comment` |
| `Created automatically: {0}` |
| `Drawing added` |
| `Drawing editor` |
| `Edit alt text` |
| `Green` |
| `Highlight` |
| `Highlight added` |
| `Highlight editor` |
| `Image added` |
| `Image editor` |
| `Marked as decorative` |
| `Middle left — resize` |
| `Middle right — resize` |
| `Missing alt text` |
| `Page {0}` |
| `Pink` |
| `Red` |
| `Remove drawing` |
| `Remove highlight` |
| `Remove image` |
| `Remove signature` |
| `Remove text` |
| `Review alt text` |
| `Show comment` |
| `Signature added` |
| `Signature editor: {0}` |
| `Text added` |
| `Text Editor` |
| `Top left corner — resize` |
| `Top middle — resize` |
| `Top right corner — resize` |
| `Yellow` |

</details>

## Requirements

- .NET SDK 10 and the [Transpose](https://github.com/curiosity-ai/transpose) compiler.
- Node, for a build from source: pdf.js is not vendored, it is bundled from the pinned
  `pdfjs-dist` npm package on every build.

## Licensing

This package is MIT. pdf.js is Apache-2.0, and the bundled distribution carries its license plus the
separate licenses of the WebAssembly decoders, the substitute fonts and the ICC profile it ships -
all of them in `assets/js/pdf/LICENSE.txt` and beside the files they cover.
