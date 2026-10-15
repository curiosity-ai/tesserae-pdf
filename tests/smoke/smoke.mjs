/**
 * Drives the sample gallery in Chromium and fails on what `dotnet build` cannot see: a page that
 * logs an error, a request that 404s, a viewer that never shows a page.
 *
 * Nearly every pdf.js failure in this package is a runtime one, and most are silent - a wrong asset
 * path, a detached byte array, a remount that never rebuilds. So this visits every page of the
 * gallery, then runs a few targeted checks for regressions that have happened before.
 *
 * Usage: node tests/smoke/smoke.mjs [site-dir]
 *   site-dir defaults to the Debug output, Tesserae.Pdf.Sample/bin/Debug/netstandard2.0/tps.
 *
 * Served under /tesserae-pdf/ rather than at the root, the way GitHub Pages serves it, so a
 * leading-slash regression fails here too.
 */
import { chromium } from 'playwright';
import { serve } from './serve.mjs';

const SITE = process.argv[2] ?? 'Tesserae.Pdf.Sample/bin/Debug/netstandard2.0/tps';

// Console errors that are the test working, not failing - see "Expected console noise" in CLAUDE.md.
const EXPECTED = [
  /Cannot load system font/,
  /getHexString/,
  // The Progress and Errors page asks for a file that is not there, on purpose.
  /does-not-exist\.pdf/,
  /Failed to load resource: the server responded with a status of 404/,
];

// Pages whose viewer is empty until a button on the page gives it a document.
const STARTS_EMPTY = new Set(['Bytes and Passwords', 'Progress and Errors']);

const failures = [];
const fail = (where, what) => { failures.push(`${where}: ${what}`); console.log(`  FAIL ${what}`); };

const site    = await serve(SITE, '/tesserae-pdf/');
const browser = await chromium.launch();
const page    = await browser.newPage({ viewport: { width: 1400, height: 900 } });

let current = 'startup';

page.on('pageerror', error => fail(current, `uncaught: ${error.message}`));
page.on('console', message => {
  if (message.type() !== 'error') return;
  if (EXPECTED.some(pattern => pattern.test(message.text()))) return;
  fail(current, `console.error: ${message.text().slice(0, 300)}`);
});
page.on('response', response => {
  if (response.status() < 400 || /does-not-exist\.pdf/.test(response.url())) return;
  fail(current, `HTTP ${response.status()} ${response.url()}`);
});

const pageCount = () => page.evaluate(() => document.querySelectorAll('.pdfViewer .page').length);

/** Waits until a viewer has pages, or a canvas has painted - whichever the page uses - or times out. */
async function waitForDocument(timeout = 15000) {
  await page.waitForFunction(
    () => document.querySelectorAll('.pdfViewer .page').length > 0 || document.querySelectorAll('canvas').length > 0,
    null, { timeout }).catch(() => {});
}

async function open(name) {
  current = name;
  console.log(`- ${name}`);
  await page.goto(`${site.url}#/view/${encodeURIComponent(name)}`);
  await waitForDocument();
  // Let the worker finish what it started - labels, outlines, thumbnails - so its errors land here.
  await page.waitForTimeout(1500);
}

const texts = () => page.evaluate(() => [...document.querySelectorAll('.tss-textblock')].map(e => e.textContent));

try {
  await page.goto(site.url);
  await page.waitForSelector('.tss-sidebar-btn', { timeout: 20000 });

  const names = await page.evaluate(() =>
    [...document.querySelectorAll('.tss-sidebar-middle .tss-sidebar-btn')].map(button => button.textContent.trim()).filter(Boolean));

  if (names.length < 20) fail('startup', `only ${names.length} sample pages in the sidebar`);

  /* ------------------------------------------------------------ every page */

  for (const name of names) {
    await open(name);

    // A page that builds a viewer must get pages into it. Not every page has one.
    const viewers = await page.evaluate(() => document.querySelectorAll('.pdfViewer').length);

    if (viewers > 0 && !STARTS_EMPTY.has(name) && (await pageCount()) === 0) fail(name, 'a viewer came up with no pages');
  }

  /* ------------------------------------------- attachments carry their bytes */

  // pdf.js 6.3 stopped sending content with the attachment list; the fixture has one 38-byte file.
  await open('Metadata');
  await page.waitForFunction(() => document.body.textContent.includes('notes.txt'), null, { timeout: 10000 }).catch(() => {});
  if (!(await texts()).some(text => text.includes('notes.txt (38 bytes)'))) fail('Metadata', 'the attachment did not report 38 bytes');

  /* ------------------------------------- a byte-backed viewer survives a remount */

  // pdf.js detaches the array it is given; the viewer replays its source on every remount.
  await open('Bytes and Passwords');
  await page.getByRole('button', { name: 'Open from bytes' }).last().click();
  await page.waitForFunction(() => document.querySelectorAll('.pdfViewer .page').length > 0, null, { timeout: 10000 });

  await page.evaluate(async () => {
    const container = document.querySelector('.pdfViewer').parentElement.parentElement;
    const parent    = container.parentElement;
    const next      = container.nextSibling;

    parent.removeChild(container);
    // A frame apart, so the mutation observer sees the removal and the insertion separately.
    await new Promise(done => requestAnimationFrame(() => setTimeout(done, 50)));
    parent.insertBefore(container, next);
  });

  await page.waitForFunction(() => document.querySelectorAll('.pdfViewer .page').length > 0, null, { timeout: 10000 })
    .catch(() => fail('Bytes and Passwords', 'the byte-backed viewer showed no pages after a remount'));

  /* ------------------------------ the chrome's page box commits on Enter only */

  await open('Viewer Chrome');

  const box      = page.locator('.tsspdf-pagebox input').first();
  const scroller = () => page.evaluate(() => document.querySelector('.tsspdf-view .pdfViewer').parentElement.scrollTop);
  const before   = await scroller();

  await box.click();
  await box.pressSequentially('5');
  await page.waitForTimeout(500);

  if ((await scroller()) !== before) fail('Viewer Chrome', 'the page box navigated before Enter');

  await box.press('Enter');
  await page.waitForTimeout(800);

  if ((await scroller()) === before) fail('Viewer Chrome', 'Enter in the page box did not navigate');
} finally {
  await browser.close();
  site.close();
}

if (failures.length > 0) {
  console.log(`\n${failures.length} failure(s):\n` + failures.map(f => `  ${f}`).join('\n'));
  process.exit(1);
}

console.log('\nAll pages loaded cleanly.');
