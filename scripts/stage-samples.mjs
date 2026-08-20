/**
 * Stage the compiled sample into `_site/` for publishing to GitHub Pages.
 *
 * The Transpose output folder is already a complete, self-contained site - `tps.json` generates
 * an index.html referencing every script relatively, and Tesserae.Pdf.targets copies pdf.js
 * into `assets/js/pdf/` beside it - so staging is a copy plus two small fixes:
 *
 *   - `.tps-manifest.<app>.json` is dropped. It is the compiler's own record of what it wrote
 *     (used to clean the output folder on the next build), not something the page fetches.
 *   - `.nojekyll` is written, so GitHub serves every path verbatim rather than running the
 *     content through Jekyll (which drops files and folders beginning with `_`).
 *
 * Deliberately not rewritten: nothing needs a <base> or an absolute-path fixup for the
 * `/tesserae-pdf/` sub-path Pages serves from. Every reference in index.html is relative,
 * and the pdf.js bundle resolves its worker from `document.currentScript.src`.
 *
 * Usage: node scripts/stage-samples.mjs [source-dir] [dest-dir]
 * Cross-platform, no dependencies. Run after `dotnet build ... -c Release`.
 */
import { rmSync, cpSync, existsSync, writeFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const SOURCE = process.argv[2] ?? 'Tesserae.Pdf.Sample/bin/Release/netstandard2.0/tps';
const DEST = process.argv[3] ?? '_site';

if (!existsSync(join(SOURCE, 'index.html'))) {
  console.error(
    `No index.html in ${SOURCE} - build the sample first:\n` +
    '  dotnet build Tesserae.Pdf.Sample/Tesserae.Pdf.Sample.csproj -c Release'
  );
  process.exit(1);
}

// The pdf.js copy is what makes the site work at all, and it lands in a separate MSBuild target
// after the compiler has written the rest - so an output folder without it is a half-built site,
// not a configuration choice. Fail loudly rather than publish a viewer that cannot load.
if (!existsSync(join(SOURCE, 'assets/js/pdf/pdf.js'))) {
  console.error(`No assets/js/pdf/pdf.js in ${SOURCE} - the pdf.js asset copy did not run.`);
  process.exit(1);
}

// The gallery opens these by URL; a site without them renders pages that can never load a
// document. They are copied by the sample's own _CopySamplePdfs target, so the same "half-built
// site" argument applies.
if (!existsSync(join(SOURCE, 'assets/pdfs'))) {
  console.error(`No assets/pdfs in ${SOURCE} - the sample PDF copy did not run.`);
  process.exit(1);
}

rmSync(DEST, { recursive: true, force: true });
cpSync(SOURCE, DEST, { recursive: true });

for (const entry of readdirSync(DEST)) {
  if (entry.startsWith('.tps-manifest.') && entry.endsWith('.json')) {
    rmSync(join(DEST, entry));
  }
}

writeFileSync(join(DEST, '.nojekyll'), '');

console.log(`Staged ${SOURCE} -> ${DEST}/`);
