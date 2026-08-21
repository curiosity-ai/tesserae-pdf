# TODO

What is deliberately not in Tesserae.Pdf, and what might be worth adding.

## Out of scope by decision

**A toolbar on `PdfJs.Viewer()`.** It draws none, and this is still the load-bearing decision: a
toolbar is the part that has to look like the rest of the host application, and the same viewer is
asked for by a full-page reader, a preview pane and a modal, which want three different sets of
buttons. The component exposes the methods a toolbar calls; the buttons stay outside.

What that left out was the application that wants a reader and no opinion about it, which
`PdfJs.ViewerChrome()` now answers — as a *composition* of the viewer's public surface, in the
package rather than instead of it. Both are the same component; the chrome is the one with the
buttons already attached, and `chrome.Viewer` is the one underneath.

**A print service.** pdf.js's own printing lives in `web/app.js`, which is not shipped on npm, and it
works by building a hidden iframe of rasterised pages sized to the browser's print dialogue —
assumptions that belong to a browser page rather than an application. A host that needs printing has
`page.RenderAsync` and `document.SaveAsync`, and the scripting manager's
`dispatchWillPrint`/`dispatchDidPrint` hooks for a document that wants to know.

**Thumbnails via `PDFThumbnailViewer`.** Also `web/app.js`-only. `PdfJs.PageCanvas()` covers the same
ground with less machinery, and lets a host lay the rail out however it likes.

**XFA forms** beyond `PdfSource.WithXfa()`, which turns pdf.js's own XFA rendering on. The format is
effectively dead and pdf.js's support for it is partial.

**An annotation-editor toolbar.** The editor *modes* are wrapped, and switching between them is one
call — but the palette, the colour picker and the thickness slider are a different kind of UI from
the chrome's: a reader's controls are the same everywhere, an editor's are a design decision per
application. `PdfViewerChrome` deliberately has no editing controls for that reason.

## Worth considering

**`PDFHistory`.** pdf.js can keep the page and zoom in the URL hash and make the browser's back
button walk a document. It is declared but not wrapped, largely because a host that already routes
will want to own its own URL — but a `History()` opt-in that hands pdf.js the hash would be small,
and is what a full-page reader wants.

**A `PdfSource` from a `File` or a stream.** `FromBytes` covers the case, but reading a whole upload
into memory to show its first page is exactly what pdf.js's range support exists to avoid. pdf.js has
`PDFDataRangeTransport` for supplying bytes on demand; wrapping it would let a host stream from
anywhere.

**Optional content groups (layers).** `getOptionalContentConfig` is on the document proxy and a
viewer can be told which layers to draw. Documents that use them are rare but the ones that do —
CAD exports, multilingual overlays — are unusable without it.

**Structure trees.** `page.Instance.getStructTree()` reaches pdf.js's tagged-PDF tree, but nothing
wraps it. It is the honest route to reading-order text extraction, which is the one real limitation
of `GetTextAsync`.

**A leaner default payload.** The bundle is ~7 MB installed, of which 1.7 MB is character maps and
1.6 MB WebAssembly. A host that never shows a CJK document or a JPEG 2000 image pays for both. The
asset directories are already separately addressable, so an opt-out would be a build-time filter
rather than a code change.

## Watch when the pdfjs-dist pin moves

**Re-check whether the modern build is usable again.** The package bundles `legacy/` only because
pdf.js 6.2's modern build calls `Map.prototype.getOrInsertComputed`, a proposal no shipping browser
has. When that stops being true, switching back drops ~110 KB — see CLAUDE.md.

**Re-check the l10n message ids.** `PdfL10nStrings` carries all 50 that pdf.js 6.2 can ask for. New
ones fall back to whatever pdf.js put in the DOM, which is safe but untranslated; removed ones become
dead cases. `grep -ohE '"pdfjs-[a-z0-9-]+"' legacy/build/pdf.mjs legacy/web/pdf_viewer.mjs` is the
check.

**Re-check the provider member names.** pdf.js renames things between minors without deprecating them
(`renderForms`'s exact-equality test, the `ResponseException` consolidation in v5,
`PDFDocumentProxy.destroy()`'s removal in v6). The interop declarations are silent when a member
moves — a call on an absent member is `undefined`, not an error.
