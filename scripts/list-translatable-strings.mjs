/**
 * List every English string this package hands to TNT, so a host can extract them.
 *
 * The package translates its own text - the chrome's labels and tooltips, and the pdf.js message
 * table in `src/L10n/` - by calling `.t()` on a literal (or `t($"...")` where a value goes in),
 * which is what TNT's CLI scans source for. A host's `tnt extract` never sees this source (it
 * arrives as a NuGet package), so those keys are absent from the host's translation file and every
 * one of them renders in English however many languages the host ships.
 *
 * The fix on the host's side is a file of its own carrying the same literals, somewhere its
 * extraction already looks. This script writes that list, so both it and the tables in
 * `Tesserae.Pdf/README.md` are generated from the call sites rather than transcribed from them:
 *
 *   node scripts/list-translatable-strings.mjs                    # the keys, one per line
 *   node scripts/list-translatable-strings.mjs --json             # keys with their call sites
 *   node scripts/list-translatable-strings.mjs --csharp [area]    # the C# lines a host pastes
 *   node scripts/list-translatable-strings.mjs --markdown [area]  # the README's tables
 *   node scripts/list-translatable-strings.mjs --update <file>    # rewrite every marked region in
 *                                                                 # a file - the README's tables,
 *                                                                 # or a host's extraction file
 *
 * A region to rewrite is a `<tesserae-pdf-strings>` ... `</tesserae-pdf-strings>` pair of comments,
 * in whatever comment syntax the file uses. It may name an `area` (`Chrome` or `L10n`, the folders
 * the strings come from) and a `format` (`csharp` or `markdown`, otherwise taken from the file's
 * extension). `--update` is what keeps a list honest: the README's chrome table had drifted by one
 * key before this script existed, which is invisible until a translator asks about it.
 *
 * Keys are TNT keys, not source text: an interpolated `$"Page {page} of {count}"` is the key
 * `Page {0} of {1}`, which is the shape `T.t(FormattableString)` looks up and the shape a
 * translator sees for every other string in a Tesserae application.
 *
 * Cross-platform, no dependencies.
 */
import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { relative, join, sep } from 'node:path';

const SOURCE = 'Tesserae.Pdf/src';

/** What each folder of strings is, for the README's tables and the C# group comments. */
const AREAS = {
  Chrome: 'The ready-made chrome - PdfJs.ViewerChrome()',
  L10n:   "pdf.js's own messages, asked for by the annotation and editor layers"
};

function sources(dir, found = []) {
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry);

    if (statSync(path).isDirectory()) sources(path, found);
    else if (path.endsWith('.cs')) found.push(path);
  }
  return found;
}

/**
 * Walk a C# file's tokens and yield every string literal TNT translates: `"...".t()`, and
 * `t($"...")` for a formattable one - which is *not* the same as `$"...".t()`, because C# resolves
 * an interpolated string's `.t()` to the `string` extension and the compiler then hands TNT text
 * that already has the values in it. See the CLAUDE.md section this script is described in.
 *
 * Comments and char literals are walked rather than skipped, so a `//` inside a string and a `"`
 * inside a comment are both read as what they are - a doc comment mentioning `"x".t()` would
 * otherwise be extracted as a string the package translates.
 */
function translatedLiterals(text) {
  const found = [];

  for (let i = 0; i < text.length; i++) {
    const c = text[i];

    if (c === '/' && text[i + 1] === '/') {
      i = text.indexOf('\n', i);
      if (i < 0) break;
      continue;
    }

    if (c === '/' && text[i + 1] === '*') {
      const end = text.indexOf('*/', i + 2);
      if (end < 0) break;
      i = end + 1;
      continue;
    }

    if (c === "'") {
      for (i++; i < text.length && text[i] !== "'"; i++) {
        if (text[i] === '\\') i++;
      }
      continue;
    }

    const prefix = /^(?:\$@|@\$|\$|@)?"/.exec(text.slice(i, i + 3));

    if (!prefix) continue;

    const interpolated = prefix[0].includes('$');
    const verbatim     = prefix[0].includes('@');
    const start        = i + prefix[0].length;

    let raw   = '';
    let depth = 0;
    let end   = start;

    for (; end < text.length; end++) {
      const s = text[end];

      if (!verbatim && s === '\\') {
        raw += s + text[end + 1];
        end++;
        continue;
      }

      if (verbatim && s === '"' && text[end + 1] === '"') {
        raw += '""';
        end++;
        continue;
      }

      if (interpolated && s === '{') { depth++; raw += s; continue; }
      if (interpolated && s === '}') { depth--; raw += s; continue; }

      if (s === '"' && depth === 0) break;

      raw += s;
    }

    const applied = text.startsWith('.t()', end + 1)
      || (text[end + 1] === ')' && /(^|[^\w.])(?:(?:TNT\.)?T\.)?t\($/.test(text.slice(Math.max(0, i - 8), i)));

    if (applied) {
      found.push({
        key:  key(raw, interpolated, verbatim),
        line: text.slice(0, i).split('\n').length
      });
    }

    i = end;
  }

  return found;
}

/** The TNT key a literal is looked up by: `$"Page {n}"` is `Page {0}`, an escape is its character. */
function key(raw, interpolated, verbatim) {
  const escapes = { n: '\n', r: '\r', t: '\t', 0: '\0', a: '\x07', b: '\b', f: '\f', v: '\v' };

  let text = verbatim
    ? raw.replace(/""/g, '"')
    : raw.replace(/\\(u[0-9a-fA-F]{4}|.)/g, (_, escaped) => escapes[escaped]
      ?? (escaped[0] === 'u' ? String.fromCharCode(parseInt(escaped.slice(1), 16)) : escaped));

  if (interpolated) {
    let next = 0;
    text = text.replace(/\{\{|\}\}|\{[^{}]*\}/g, match =>
      (match === '{{' || match === '}}') ? match : `{${next++}}`);
  }

  return text;
}

/** A C# string literal for a key. Never verbatim, so what it holds reads like the call site's. */
function literal(text) {
  const escaped = text
    .replace(/\\/g, '\\\\')
    .replace(/"/g, '\\"')
    .replace(/\n/g, '\\n')
    .replace(/\r/g, '\\r')
    .replace(/\t/g, '\\t');

  return `"${escaped}"`;
}

const keys = new Map();

for (const file of sources(SOURCE)) {
  const text = readFileSync(file, 'utf8');
  const area = relative(SOURCE, file).split(sep)[0].replace(/\.cs$/, '');

  for (const { key, line } of translatedLiterals(text)) {
    if (!keys.has(key)) keys.set(key, { areas: new Set(), at: [] });
    keys.get(key).areas.add(area);
    keys.get(key).at.push(`${file.split(sep).join('/')}:${line}`);
  }
}

/**
 * The keys of one area, or of all of them, in the order a translator reads them.
 *
 * A key used in both places (`Page {0}` is a chrome label *and* one of pdf.js's messages) belongs to
 * both areas, so each area's list carries it - a host copying one table must not be left without it.
 * Asked for every area it is still listed once, under the first area it appears in.
 */
function keysOf(area) {
  if (area !== undefined && !(area in AREAS)) {
    throw new Error(`Unknown area '${area}' - one of ${Object.keys(AREAS).join(', ')}`);
  }

  const order = one => Object.keys(AREAS).indexOf(one);
  const first = entry => [...entry.areas].sort((a, b) => order(a) - order(b))[0];

  return [...keys]
    .filter(([, entry]) => area === undefined || entry.areas.has(area))
    .sort(([a, x], [b, y]) => order(first(x)) - order(first(y)) || a.localeCompare(b, 'en'))
    .map(([key, entry]) => ({ key, area: area ?? first(entry), at: entry.at }));
}

/** The C# lines a host's extraction file carries, grouped by the folder the strings come from. */
function csharp(area, indent) {
  const lines = [];
  let written  = null;

  for (const entry of keysOf(area)) {
    if (entry.area !== written) {
      if (written !== null) lines.push('');
      lines.push(`${indent}// ${AREAS[entry.area] ?? entry.area}`);
      written = entry.area;
    }
    lines.push(`${indent}${literal(entry.key)}.t(),`);
  }

  return lines;
}

/** The README's tables: one per area when asked for all of them, keys in a single column. */
function markdown(area, indent) {
  const areas = area === undefined ? Object.keys(AREAS) : [area];

  return areas.flatMap((one, index) => [
    ...(index === 0 ? [] : ['']),
    ...(areas.length > 1 ? [`${indent}**${AREAS[one]}**`, ''] : []),
    `${indent}| Key |`,
    `${indent}| --- |`,
    ...keysOf(one).map(({ key }) => `${indent}| \`${key.replace(/\|/g, '\\|')}\` |`)
  ]);
}

const RENDERERS = { csharp, markdown };

const EXTENSIONS = { '.cs': 'csharp', '.md': 'markdown' };

const REGION =
  /^([^\S\n]*)([^\n]*<tesserae-pdf-strings([^>]*)>[^\n]*)\r?\n[\s\S]*?^([^\n]*<\/tesserae-pdf-strings>[^\n]*)$/gm;

/** Rewrite every marked region of a file with what its `area` and `format` ask for. */
function update(path) {
  const text      = readFileSync(path, 'utf8');
  const eol       = text.includes('\r\n') ? '\r\n' : '\n';
  const extension = path.slice(path.lastIndexOf('.'));

  let regions = 0;

  const updated = text.replace(REGION, (_, indent, open, attributes, close) => {
    const area   = /area\s*=\s*"([^"]*)"/.exec(attributes)?.[1];
    const format = /format\s*=\s*"([^"]*)"/.exec(attributes)?.[1] ?? EXTENSIONS[extension];

    if (area !== undefined && !(area in AREAS)) {
      throw new Error(`${path}: unknown area '${area}' - one of ${Object.keys(AREAS).join(', ')}`);
    }

    if (!(format in RENDERERS)) {
      throw new Error(`${path}: no format for '${extension}' - name one with format="csharp|markdown"`);
    }

    regions++;

    return [indent + open, ...RENDERERS[format](area, indent), close].join(eol);
  });

  if (regions === 0) {
    throw new Error(
      `${path} carries no '<tesserae-pdf-strings>' ... '</tesserae-pdf-strings>' pair to rewrite between`);
  }

  writeFileSync(path, updated);
  console.error(`${path}: wrote ${regions === 1 ? 'one region' : `${regions} regions`}, ${keys.size} strings`);
}

const [mode, out] = process.argv.slice(2);

try {
  switch (mode) {
    case undefined:
      for (const { key } of keysOf()) console.log(key);
      break;

    case '--json':
      console.log(JSON.stringify(keysOf().map(({ key, area, at }) => ({ key, area, at })), null, 2));
      break;

    case '--csharp':
      console.log(csharp(out, '            ').join('\n'));
      break;

    case '--markdown':
      console.log(markdown(out, '').join('\n'));
      break;

    case '--update':
      if (!out) throw new Error('--update needs the path of the file to rewrite');
      update(out);
      break;

    default:
      throw new Error(`Unknown option '${mode}' - one of --json, --csharp, --markdown, --update <file>`);
  }
} catch (error) {
  console.error(error.message);
  process.exit(1);
}
