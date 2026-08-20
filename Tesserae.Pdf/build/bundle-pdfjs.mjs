/**
 * Bundles the **ESM** build of pdfjs-dist into the handful of browser assets that Tesserae.Pdf
 * ships. Everything that can be resolved ahead of time is - the module graph, minification,
 * pdf.js's stylesheet and the images it references - so the browser fetches one plain IIFE script
 * and pdf.js's own globals are simply there.
 *
 * Why a bundle step exists at all, given pdf.js's ESM build has no CSS imports and (since v5) no
 * top-level await: two files have to be evaluated in a fixed order, and one of them is only
 * reachable as a module.
 *
 *   - `web/pdf_viewer.mjs` has zero imports. It destructures `globalThis.pdfjsLib` at its top
 *     level, so `build/pdf.mjs` MUST have evaluated first. Two <script type="module"> tags do not
 *     guarantee that ordering across a cold cache; one bundle does, because ESM evaluation order
 *     inside it is the import order of the entry below.
 *   - Loading them as modules also means the page cannot ask "has pdf.js loaded yet" without
 *     import-map plumbing. An IIFE sets `globalThis.pdfjsLib` / `globalThis.pdfjsViewer`
 *     synchronously, which is exactly what PdfJs.IsLoaded reads.
 *
 * pdf.js source is never modified. The bundle is esbuild's output with a prelude and an epilogue
 * concatenated around it, both of which only add things (a <style>, a default workerSrc).
 *
 * Emits into assets/js/pdf/:
 *
 *   pdf.js                 the display API + the viewer components as one IIFE, with
 *                          web/pdf_viewer.css (and every image it references) folded in
 *   pdf.worker.min.mjs     copied verbatim - see below
 *   pdf.sandbox.min.mjs    copied verbatim - see below
 *   cmaps/                 169 .bcmap files, for CJK documents        (cMapUrl)
 *   standard_fonts/        the 14 standard PDF fonts                 (standardFontDataUrl)
 *   wasm/                  JPX/JBIG2/ICC decoders + the QuickJS glue (wasmUrl)
 *   iccs/                  the CMYK ICC profile                      (iccUrl)
 *   images/                annotation and editor icons the viewer loads by URL at runtime
 *   LICENSE.txt, version.txt
 *
 * Two files are copied rather than bundled, and neither is negotiable:
 *
 *   - **The worker.** pdf.js constructs it with `new Worker(src, { type: "module" })`, a hard-coded
 *     literal, so it has to remain a separate module file. Bundling it in would produce a script
 *     nothing ever loads and a fake worker running the parser on the main thread.
 *   - **The sandbox.** `GenericScripting` loads it with a native `import(sandboxBundleSrc)` and
 *     reads `QuickJSSandbox` off the module namespace, and the sandbox in turn dynamic-imports
 *     `${wasmUrl}quickjs-eval.js`, which finds its .wasm next to itself via `import.meta.url`.
 *     Folded into an IIFE, all three of those break.
 *
 * Nothing here is committed: the output is generated from the pinned npm package on every build
 * (see the BundlePdfJs target in Tesserae.Pdf.csproj) and is gitignored.
 *
 * Run with:  npm run bundle   (from the Tesserae.Pdf/ folder)
 */
import { build } from 'esbuild';
import { mkdir, rm, readFile, writeFile, cp, readdir, stat } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, resolve, join } from 'node:path';

const here     = dirname(fileURLToPath(import.meta.url));
const pkgRoot  = resolve(here, '..');
const distRoot = resolve(pkgRoot, 'node_modules/pdfjs-dist');
const outDir   = resolve(pkgRoot, 'assets/js/pdf');

await rm(outDir, { recursive: true, force: true });
await mkdir(outDir, { recursive: true });

/**
 * pdf.js's stylesheet, built on its own so its ~20 relative `url(images/*.svg|gif)` references
 * resolve and inline. It is required in practice even for a bare canvas render plus a text layer:
 * all of the `.textLayer` / `.annotationLayer` / `.pdfViewer .page` positioning lives here, and
 * without it selectable text lands nowhere near the glyphs it covers.
 *
 * Inlining rather than shipping a second file removes a request, removes any chance of the viewer
 * painting unpositioned, and means the host cannot forget it. What is left after this are the two
 * `url(#fragment)` references, which point at SVG masks in the document and must stay as they are.
 */
const cssBuild = await build({
  entryPoints: [join(distRoot, 'web/pdf_viewer.css')],
  outfile:     join(outDir, 'pdf_viewer.css'),
  bundle:      true,
  minify:      true,
  write:       false,
  logLevel:    'warning',
  loader: { '.svg': 'dataurl', '.gif': 'dataurl', '.png': 'dataurl' },
});

const viewerCss = cssBuild.outputFiles.find((f) => f.path.endsWith('.css')).text;

/**
 * The display API and the viewer components, in that order.
 *
 * A synthetic entry rather than two esbuild calls: pdf_viewer.mjs reads `globalThis.pdfjsLib`
 * while it evaluates, so the ordering is a correctness requirement and an import list is the
 * cheapest way to state it.
 *
 * No `globalName`: unlike Monaco, pdf.js publishes itself. pdf.mjs assigns `globalThis.pdfjsLib`
 * (and `globalThis.pdfjsWorker`) and pdf_viewer.mjs assigns `globalThis.pdfjsViewer`, all
 * synchronously during evaluation, which is the shape the C# `[External]` declarations name.
 *
 * The dynamic `import(...)` calls esbuild leaves alone are wanted: they are the wasm-less decoder
 * fallbacks, the sandbox, and the fake-worker path, each of which must resolve at runtime against
 * a URL the host supplies.
 */
const jsBuild = await build({
  stdin: {
    contents: "import 'pdfjs-dist/build/pdf.mjs';\nimport 'pdfjs-dist/web/pdf_viewer.mjs';\n",
    resolveDir: pkgRoot,
    sourcefile: 'tesserae-pdf-entry.mjs',
    loader: 'js',
  },
  outfile:       join(outDir, 'pdf.js'),
  bundle:        true,
  format:        'iife',
  target:        'es2022',
  minify:        true,
  legalComments: 'none',
  write:         false,
  logLevel:      'warning',
});

const pdfJs = jsBuild.outputFiles.find((f) => f.path.endsWith('.js')).text;

/**
 * Injects pdf.js's stylesheet as a <style>, once. `document.currentScript` is valid here because
 * the prelude runs during this script's own evaluation.
 */
const prelude = `(function () {
  if (document.querySelector('style[data-tss-pdf]')) { return; }

  var style = document.createElement('style');
  style.setAttribute('data-tss-pdf', '');
  style.textContent = ${JSON.stringify(viewerCss)};
  document.head.appendChild(style);
})();
`;

/**
 * Defaults `GlobalWorkerOptions.workerSrc` to the worker sitting next to this script.
 *
 * pdf.js has no browser default - `PDFWorker.workerSrc` throws 'No "GlobalWorkerOptions.workerSrc"
 * specified' - and getting it wrong does not fail loudly: pdf.js falls back to importing the
 * worker on the main thread ("Setting up fake worker"), which parses correctly and freezes the UI
 * while it does. Deriving it from `document.currentScript.src` means the worker follows wherever
 * this bundle is served from, with no second setting to keep in sync, including a CDN (pdf.js
 * wraps a cross-origin workerSrc in a same-origin blob itself).
 *
 * Guarded on both options, so a host that installed its own workerSrc or handed pdf.js a
 * workerPort before loading this file keeps it.
 */
const epilogue = `
;(function () {
  var lib = globalThis.pdfjsLib;

  if (!lib || !lib.GlobalWorkerOptions) { return; }
  if (lib.GlobalWorkerOptions.workerSrc || lib.GlobalWorkerOptions.workerPort) { return; }

  var src  = (document.currentScript && document.currentScript.src) || '';
  var base = src ? src.replace(/\\/[^\\/]*$/, '') : '.';

  lib.GlobalWorkerOptions.workerSrc = base + '/pdf.worker.min.mjs';
})();
`;

await writeFile(join(outDir, 'pdf.js'), prelude + pdfJs + epilogue);

// Verbatim copies - see the header for why each one cannot be bundled.
await cp(join(distRoot, 'build/pdf.worker.min.mjs'),  join(outDir, 'pdf.worker.min.mjs'));
await cp(join(distRoot, 'build/pdf.sandbox.min.mjs'), join(outDir, 'pdf.sandbox.min.mjs'));

/**
 * The asset directories pdf.js fetches at runtime, each addressed by one DocumentInitParameters
 * URL (or, for images/, by PDFViewer's imageResourcesPath). PdfJs.Runtime defaults all five to
 * these paths.
 *
 * Copied whole rather than filtered: each directory carries the sidecar LICENSE files covering
 * its contents (the wasm decoders, the Foxit and Liberation fonts, the ICC profile), and
 * redistribution requires they travel with it. wasm/ also holds the `*_nowasm_fallback.js` files
 * pdf.js dynamic-imports when a .wasm fails to instantiate, plus `quickjs-eval.js` - the
 * Emscripten glue the scripting sandbox loads, which is mandatory rather than a fallback.
 */
for (const dir of ['cmaps', 'standard_fonts', 'wasm', 'iccs']) {
  await cp(join(distRoot, dir), join(outDir, dir), { recursive: true });
}

// The annotation and editor icons the viewer builds <img src> for at runtime, from
// imageResourcesPath. Separate from the ones the stylesheet inlines above.
await cp(join(distRoot, 'web/images'), join(outDir, 'images'), { recursive: true });

// pdf.js is Apache-2.0; ship its license text alongside the code it covers, and point at the
// sidecar licenses that travel inside the asset directories.
const version = JSON.parse(await readFile(join(distRoot, 'package.json'), 'utf8')).version;
const license = await readFile(join(distRoot, 'LICENSE'), 'utf8');

await writeFile(
  join(outDir, 'LICENSE.txt'),
  `pdfjs-dist ${version}\n` +
  `https://github.com/mozilla/pdf.js\n\n` +
  `The bundled code in pdf.js, pdf.worker.min.mjs and pdf.sandbox.min.mjs is covered by the\n` +
  `Apache License 2.0 reproduced below. Additional components carry their own licenses, which\n` +
  `travel in the directory holding them:\n\n` +
  `  wasm/LICENSE_OPENJPEG, wasm/LICENSE_PDFJS_OPENJPEG   the JPX (JPEG 2000) decoder\n` +
  `  wasm/LICENSE_JBIG2, wasm/LICENSE_PDFJS_JBIG2         the JBIG2 / CCITTFax decoder\n` +
  `  wasm/LICENSE_QCMS, wasm/LICENSE_PDFJS_QCMS           the ICC colour-management decoder\n` +
  `  standard_fonts/LICENSE_FOXIT                         the Foxit standard-font substitutes\n` +
  `  standard_fonts/LICENSE_LIBERATION                    Liberation Sans (SIL OFL)\n` +
  `  cmaps/LICENSE                                        the CJK character maps\n` +
  `  iccs/LICENSE                                         the CGATS001 CMYK profile\n\n` +
  `${license}`
);

/** Every file under `dir`, recursively, as absolute paths. */
async function walk(dir) {
  const entries = await readdir(dir, { withFileTypes: true });
  const files   = [];

  for (const entry of entries) {
    const full = join(dir, entry.name);

    if (entry.isDirectory()) {
      files.push(...(await walk(full)));
    } else {
      files.push(full);
    }
  }

  return files;
}

// version.txt is written LAST, on purpose: it is the Outputs marker of the BundlePdfJs MSBuild
// target, so a run that dies half way must not leave behind something that looks finished.
await writeFile(join(outDir, 'version.txt'), version + '\n');

const files = await walk(outDir);
let total   = 0;

for (const file of files) {
  total += (await stat(file)).size;
}

console.log(
  `pdfjs-dist ${version} bundled (ESM -> IIFE): ${files.length} files, ` +
  `${(total / 1024 / 1024).toFixed(1)} MB -> assets/js/pdf/`
);
