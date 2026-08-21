# tesserae-pdf

**Tesserae.Pdf** — a [Tesserae](https://github.com/curiosity-ai/tesserae) wrapper around
[Mozilla's pdf.js](https://github.com/mozilla/pdf.js), for Transpose C#-to-JavaScript applications.

A scrollable, searchable document viewer — bare, or with a full reader chrome already on it; a
single-page canvas renderer for thumbnails and previews; and a typed API over pdf.js's display
surface for everything that needs no viewer at all — text extraction, metadata, permissions,
outlines.

**[Browse the sample gallery →](https://curiosity-ai.github.io/tesserae-pdf/)**

```csharp
PdfJs.Viewer()
   .Url("/api/files/42.pdf")
   .FitWidth()
   .OnPageChanged(page => label.Text = $"Page {page}")
```

pdf.js ships with the package: nothing to add to your page, no CDN to configure, no worker path to
keep in sync.

See [`Tesserae.Pdf/README.md`](Tesserae.Pdf/README.md) for the package documentation — what is in it,
how to get started, and the handful of pdf.js behaviours worth knowing about. See
[`CLAUDE.md`](CLAUDE.md) for how the repository is put together and what was learned building it,
and [`TODO.md`](TODO.md) for what is deliberately not in it.

## Repository layout

| | |
| --- | --- |
| `Tesserae.Pdf/` | the package |
| `Tesserae.Pdf.Sample/` | the sample gallery — 22 pages, one per feature |
| `scripts/make-sample-pdfs.mjs` | generates the gallery's PDF fixtures |
| `scripts/stage-samples.mjs` | stages a Release build for GitHub Pages |

## Building

```bash
dotnet tool update --global Transpose.Compiler
export PATH="$PATH:$HOME/.dotnet/tools"

dotnet build Tesserae.Pdf.slnx
```

Node is a prerequisite: pdf.js is not vendored, it is bundled from the pinned `pdfjs-dist` npm
package on every build.

## Licensing

This repository is MIT. pdf.js is Apache-2.0, and the bundled distribution carries its license plus
the separate licenses of the WebAssembly decoders, substitute fonts and ICC profile it ships.
