/**
 * Writes the PDFs the sample gallery opens, into Tesserae.Pdf.Sample/assets/pdfs/.
 *
 * They are generated rather than downloaded so the repository owns its own fixtures: each one
 * exists to exercise something specific in the wrapper, the licensing is trivial, and a page that
 * starts failing can be answered by reading what the document actually contains rather than by
 * guessing at a PDF somebody found on the web.
 *
 *   sample-outline.pdf     12 pages, a three-level outline with bold/italic/coloured entries, named
 *                          destinations, roman-then-decimal page labels, full metadata, and a phrase
 *                          repeated on known pages for the search page to count
 *   sample-images.pdf      three pages of embedded images, for the render and thumbnail pages
 *   sample-cjk.pdf         a CID font with no embedded font file, so pdf.js has to fetch a .bcmap -
 *                          which is what makes this the regression test for cMapUrl
 *   sample-forms.pdf       an AcroForm: text fields, a checkbox, a dropdown, a button
 *   sample-scripting.pdf   an AcroForm whose total is computed by embedded JavaScript
 *   sample-protected.pdf   encrypted with the user password "tesserae", and denying print and copy
 *
 * No dependencies: PDF is a text-ish container and everything here is written by hand. Streams are
 * stored uncompressed on purpose - a fixture you can read in a hex dump is worth more than a small
 * one, and the largest of these is a few tens of KB.
 *
 * Usage: node scripts/make-sample-pdfs.mjs [out-dir]
 */
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const OUT = process.argv[2] ?? 'Tesserae.Pdf.Sample/assets/pdfs';

mkdirSync(OUT, { recursive: true });

/* ------------------------------------------------------------------ writer */

/**
 * A PDF is a list of numbered objects, a table of their byte offsets, and a trailer pointing at the
 * catalog. This collects objects, then serialises the three together.
 *
 * Object numbers are handed out up front with `reserve()`, because PDF is full of forward references
 * - a page names its parent, an outline entry names the page after it - and back-patching offsets is
 * how every PDF writer works.
 */
class Pdf {
  constructor() {
    this.objects = [];   // index 0 unused: PDF object numbers start at 1
    this.encrypt = null; // set by `encryptWith` to { key, keyLength }
  }

  /** Claims an object number without deciding what goes in it yet. */
  reserve() {
    this.objects.push(null);
    return this.objects.length;
  }

  /** Fills in a reserved object. Body is a string, or a { dict, stream } pair. */
  put(number, body) {
    this.objects[number - 1] = body;
    return number;
  }

  /** Reserves and fills in one step, for an object nothing forward-references. */
  add(body) {
    return this.put(this.reserve(), body);
  }

  /**
   * RC4-encrypts every string and stream with a per-object key, which is what the PDF standard
   * security handler means by "encrypted". Call it after all objects are in place.
   */
  encryptWith(key) {
    this.encrypt = { key };
  }

  build(catalogNumber, id) {
    const chunks = [];
    let length = 0;
    const push = (buffer) => { chunks.push(buffer); length += buffer.length; };

    push(Buffer.from('%PDF-1.7\n'));
    // A comment of high bytes, so anything transferring this file byte-counts it as binary.
    push(Buffer.from([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]));

    const offsets = [];

    for (let i = 0; i < this.objects.length; i++) {
      const number = i + 1;
      offsets[number] = length;

      const body = this.objects[i];

      if (body === null) throw new Error(`object ${number} was reserved but never filled in`);

      push(Buffer.from(`${number} 0 obj\n`));

      if (typeof body === 'string') {
        push(Buffer.from(this.#maybeEncryptStrings(body, number), 'latin1'));
      } else {
        let data = Buffer.isBuffer(body.stream) ? body.stream : Buffer.from(body.stream, 'latin1');

        if (this.encrypt) data = rc4(objectKey(this.encrypt.key, number), data);

        push(Buffer.from(this.#maybeEncryptStrings(`<< ${body.dict} /Length ${data.length} >>\nstream\n`, number), 'latin1'));
        push(data);
        push(Buffer.from('\nendstream'));
      }

      push(Buffer.from('\nendobj\n'));
    }

    const xrefOffset = length;

    let xref = `xref\n0 ${this.objects.length + 1}\n0000000000 65535 f \n`;

    for (let number = 1; number <= this.objects.length; number++) {
      xref += `${String(offsets[number]).padStart(10, '0')} 00000 n \n`;
    }

    push(Buffer.from(xref));

    let trailer = `trailer\n<< /Size ${this.objects.length + 1} /Root ${catalogNumber} 0 R /ID [<${id}> <${id}>]`;

    if (this.encryptNumber) trailer += ` /Encrypt ${this.encryptNumber} 0 R`;

    trailer += ` >>\nstartxref\n${xrefOffset}\n%%EOF\n`;

    push(Buffer.from(trailer));

    return Buffer.concat(chunks, length);
  }

  /**
   * Encrypts the literal strings inside an object body.
   *
   * The /Encrypt dictionary itself is exempt - its own O and U values are what the reader needs to
   * derive the key - so it is marked with a sentinel rather than being parsed for.
   */
  #maybeEncryptStrings(body, number) {
    if (!this.encrypt || number === this.encryptNumber) return body;

    // Only the parenthesised literals this generator writes: no nested parens, no escapes to
    // rebalance. Enough for these fixtures, and a real writer would tokenise properly.
    return body.replace(/\(([^()]*)\)/g, (_, text) =>
      '<' + rc4(objectKey(this.encrypt.key, number), Buffer.from(text, 'latin1')).toString('hex') + '>');
  }
}

/** RC4, which is what a 40-bit standard security handler uses. */
function rc4(key, data) {
  const s = new Uint8Array(256);

  for (let i = 0; i < 256; i++) s[i] = i;

  for (let i = 0, j = 0; i < 256; i++) {
    j = (j + s[i] + key[i % key.length]) & 0xff;
    [s[i], s[j]] = [s[j], s[i]];
  }

  const out = Buffer.alloc(data.length);

  for (let k = 0, i = 0, j = 0; k < data.length; k++) {
    i = (i + 1) & 0xff;
    j = (j + s[i]) & 0xff;
    [s[i], s[j]] = [s[j], s[i]];
    out[k] = data[k] ^ s[(s[i] + s[j]) & 0xff];
  }

  return out;
}

const md5 = (...buffers) => {
  const hash = createHash('md5');
  for (const buffer of buffers) hash.update(buffer);
  return hash.digest();
};

/** The per-object key: the file key plus the object and generation numbers, hashed. */
function objectKey(fileKey, number) {
  const extra = Buffer.from([number & 0xff, (number >> 8) & 0xff, (number >> 16) & 0xff, 0, 0]);

  return md5(fileKey, extra).subarray(0, Math.min(fileKey.length + 5, 16));
}

/** The 32-byte padding string every password is padded or truncated to. */
const PAD = Buffer.from([
  0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
  0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
]);

const padPassword = (password) => Buffer.concat([Buffer.from(password, 'latin1'), PAD]).subarray(0, 32);

const escape = (text) => text.replace(/([\\()])/g, '\\$1');

/* --------------------------------------------------------- sample-outline */

/**
 * The document most pages open. Twelve pages of plain text, plus every piece of document-level
 * structure a viewer has a feature for.
 */
function outlinePdf() {
  const pdf = new Pdf();

  const pagesNumber   = pdf.reserve();
  const catalogNumber = pdf.reserve();
  const outlineNumber = pdf.reserve();
  const infoNumber    = pdf.reserve();

  const helvetica = pdf.add('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>');
  const bold      = pdf.add('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>');

  // What the Search page counts. On pages 3, 7 and 11 only, so a count of 3 is a real assertion
  // rather than "some matches were found".
  const NEEDLE = 'tesserae';

  const titles = [
    'Cover', 'Contents', 'Introduction', 'Getting started', 'Loading a document',
    'Rendering pages', 'Text extraction', 'Outlines and destinations', 'Forms',
    'Annotations', 'Scripting', 'Colophon',
  ];

  const pageNumbers = [];

  for (let i = 0; i < 12; i++) {
    const page = i + 1;
    const lines = [
      { font: bold,      size: 24, y: 740, text: titles[i] },
      { font: helvetica, size: 11, y: 700, text: `Page ${page} of 12.` },
    ];

    if (page === 3 || page === 7 || page === 11) {
      lines.push({ font: helvetica, size: 11, y: 676, text: `This page mentions ${NEEDLE} exactly once.` });
    } else {
      lines.push({ font: helvetica, size: 11, y: 676, text: 'This page does not mention the search term.' });
    }

    // Enough text that a text layer has something to select and a search has context around a hit.
    for (let line = 0; line < 14; line++) {
      lines.push({
        font: helvetica,
        size: 10,
        y:    640 - line * 16,
        text: `Paragraph ${line + 1} on page ${page}. PDF stores text as positioned runs rather than as prose.`,
      });
    }

    const content = lines
      .map((l) => `BT /F${l.font === bold ? 'B' : 'R'} ${l.size} Tf 72 ${l.y} Td (${escape(l.text)}) Tj ET`)
      .join('\n');

    const contentNumber = pdf.add({ dict: '', stream: content });

    pageNumbers.push(pdf.add(
      `<< /Type /Page /Parent ${pagesNumber} 0 R /MediaBox [0 0 612 792] ` +
      `/Resources << /Font << /FR ${helvetica} 0 R /FB ${bold} 0 R >> >> ` +
      `/Contents ${contentNumber} 0 R >>`));
  }

  pdf.put(pagesNumber,
    `<< /Type /Pages /Count 12 /Kids [${pageNumbers.map((n) => `${n} 0 R`).join(' ')}] >>`);

  // A three-level outline. /F is the style bitfield - 1 italic, 2 bold - and /C the RGB colour, so
  // between them these entries cover every combination the Outline page renders.
  const entries = [
    { title: 'Front matter',             page: 1,  flags: 2, color: null,          children: [
      { title: 'Cover',                  page: 1,  flags: 0, color: null },
      { title: 'Contents',               page: 2,  flags: 1, color: null },
    ] },
    { title: 'Part one: the basics',     page: 3,  flags: 2, color: [0.8, 0.1, 0.1], children: [
      { title: 'Introduction',           page: 3,  flags: 0, color: null },
      { title: 'Getting started',        page: 4,  flags: 0, color: null, children: [
        { title: 'Loading a document',   page: 5,  flags: 1, color: [0.1, 0.4, 0.8] },
        { title: 'Rendering pages',      page: 6,  flags: 0, color: null },
      ] },
      { title: 'Text extraction',        page: 7,  flags: 0, color: null },
    ] },
    { title: 'Part two: interaction',    page: 8,  flags: 2, color: [0.1, 0.5, 0.2], children: [
      { title: 'Outlines and destinations', page: 8,  flags: 0, color: null },
      { title: 'Forms',                  page: 9,  flags: 0, color: null },
      { title: 'Annotations',            page: 10, flags: 0, color: null },
      { title: 'Scripting',              page: 11, flags: 3, color: null },
    ] },
    { title: 'Colophon',                 page: 12, flags: 0, color: null },
  ];

  /** Writes one level of the outline, linked as the doubly-linked list PDF wants. */
  function writeOutlineLevel(items, parentNumber) {
    const numbers = items.map(() => pdf.reserve());

    items.forEach((item, i) => {
      const children = item.children ? writeOutlineLevel(item.children, numbers[i]) : null;

      let dict = `<< /Title (${escape(item.title)}) /Parent ${parentNumber} 0 R ` +
                 `/Dest [${pageNumbers[item.page - 1]} 0 R /XYZ 0 792 null]`;

      if (i > 0)                dict += ` /Prev ${numbers[i - 1]} 0 R`;
      if (i < items.length - 1) dict += ` /Next ${numbers[i + 1]} 0 R`;
      if (item.flags)           dict += ` /F ${item.flags}`;
      if (item.color)           dict += ` /C [${item.color.join(' ')}]`;

      if (children) {
        // A negative /Count is how a PDF asks for a branch to open collapsed. Positive on the
        // second part, so the Outline page shows both states.
        const count = item.title.startsWith('Part two') ? children.count : -children.count;

        dict += ` /First ${children.first} 0 R /Last ${children.last} 0 R /Count ${count}`;
      }

      pdf.put(numbers[i], dict + ' >>');
    });

    return { first: numbers[0], last: numbers[numbers.length - 1], count: items.length };
  }

  const top = writeOutlineLevel(entries, outlineNumber);

  pdf.put(outlineNumber,
    `<< /Type /Outlines /First ${top.first} 0 R /Last ${top.last} 0 R /Count ${top.count} >>`);

  // Named destinations, for the "go to a place by name" half of navigation. Deliberately not the
  // same targets as the outline, so a page can prove it used these rather than those.
  const dests = pdf.add(
    `<< /introduction [${pageNumbers[2]} 0 R /XYZ 0 792 null] ` +
    `/forms [${pageNumbers[8]} 0 R /XYZ 0 792 null] ` +
    `/colophon [${pageNumbers[11]} 0 R /Fit] >>`);

  // Roman numerals for the first two pages, then decimal restarting at 1 - the arrangement that
  // makes a viewer's page label differ from its page number, which is the whole point of labels.
  const pageLabels = pdf.add('<< /Nums [0 << /S /r >> 2 << /S /D /St 1 >>] >>');

  pdf.put(catalogNumber,
    `<< /Type /Catalog /Pages ${pagesNumber} 0 R /Outlines ${outlineNumber} 0 R ` +
    `/Dests ${dests} 0 R /PageLabels ${pageLabels} 0 R /PageMode /UseOutlines /PageLayout /OneColumn >>`);

  pdf.put(infoNumber,
    '<< /Title (Tesserae.Pdf sample document) /Author (Curiosity GmbH) ' +
    '/Subject (A generated fixture for the Tesserae.Pdf sample gallery) ' +
    '/Keywords (tesserae, pdf.js, sample, outline) ' +
    '/Creator (scripts/make-sample-pdfs.mjs) /Producer (Tesserae.Pdf) ' +
    '/CreationDate (D:20260501120000Z) /ModDate (D:20260501120000Z) >>');

  // The Info dictionary is reachable from the trailer, not the catalog, so it is appended by hand.
  const bytes = pdf.build(catalogNumber, '54657373657261654f75746c696e6531');

  return Buffer.from(bytes.toString('latin1').replace(
    `/Root ${catalogNumber} 0 R`, `/Root ${catalogNumber} 0 R /Info ${infoNumber} 0 R`), 'latin1');
}

/* ---------------------------------------------------------- sample-images */

/**
 * Three pages of embedded bitmaps. Drawn as raw DeviceRGB samples rather than as a JPEG, so the
 * file stays readable and no encoder is involved - the point is that pdf.js paints image XObjects,
 * not that it decodes any particular format.
 */
function imagesPdf() {
  const pdf = new Pdf();

  const pagesNumber   = pdf.reserve();
  const catalogNumber = pdf.reserve();

  const font = pdf.add('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>');

  /** A width x height RGB bitmap from a per-pixel function. */
  const bitmap = (width, height, pixel) => {
    const data = Buffer.alloc(width * height * 3);

    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        const [r, g, b] = pixel(x / (width - 1), y / (height - 1));
        const at = (y * width + x) * 3;

        data[at] = r; data[at + 1] = g; data[at + 2] = b;
      }
    }

    return data;
  };

  const images = [
    { name: 'Gradient', pixel: (u, v) => [Math.round(255 * u), Math.round(255 * v), 160] },
    { name: 'Rings',    pixel: (u, v) => {
      const d = Math.hypot(u - 0.5, v - 0.5) * 8;
      const t = (Math.sin(d * Math.PI) + 1) / 2;

      return [Math.round(40 + 200 * t), Math.round(80 * t), Math.round(220 - 180 * t)];
    } },
    { name: 'Checks',   pixel: (u, v) => {
      const on = (Math.floor(u * 8) + Math.floor(v * 8)) % 2 === 0;

      return on ? [235, 235, 240] : [40, 44, 60];
    } },
  ];

  const pageNumbers = images.map((image, i) => {
    const size = 128;
    const data = bitmap(size, size, image.pixel);

    const xobject = pdf.add({
      dict: `/Type /XObject /Subtype /Image /Width ${size} /Height ${size} ` +
            '/ColorSpace /DeviceRGB /BitsPerComponent 8',
      stream: data,
    });

    const content = pdf.add({ dict: '', stream:
      `BT /F1 20 Tf 72 740 Td (${escape(image.name)} - page ${i + 1} of 3) Tj ET\n` +
      'q 400 0 0 400 106 280 cm /Im1 Do Q\n' +
      `BT /F1 10 Tf 72 240 Td (A ${size} by ${size} DeviceRGB image, scaled to 400 points square.) Tj ET`,
    });

    return pdf.add(
      `<< /Type /Page /Parent ${pagesNumber} 0 R /MediaBox [0 0 612 792] ` +
      `/Resources << /Font << /F1 ${font} 0 R >> /XObject << /Im1 ${xobject} 0 R >> >> ` +
      `/Contents ${content} 0 R >>`);
  });

  pdf.put(pagesNumber, `<< /Type /Pages /Count ${pageNumbers.length} /Kids [${pageNumbers.map((n) => `${n} 0 R`).join(' ')}] >>`);
  pdf.put(catalogNumber, `<< /Type /Catalog /Pages ${pagesNumber} 0 R >>`);

  return pdf.build(catalogNumber, '5465737365726165496d6167657331');
}

/* ------------------------------------------------------------- sample-cjk */

/**
 * A page of Chinese text set in a CID font the document does not embed.
 *
 * That combination is what forces pdf.js to fetch a character map: it has to map the CIDs to
 * Unicode through the Adobe-GB1 encoding, which lives in cmaps/UniGB-UCS2-H.bcmap, and it has to
 * find a substitute font. Get cMapUrl wrong and this page renders blanks while the console shows a
 * 404 - which is exactly the failure this fixture exists to catch.
 */
function cjkPdf() {
  const pdf = new Pdf();

  const pagesNumber   = pdf.reserve();
  const catalogNumber = pdf.reserve();

  const latin = pdf.add('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>');

  const descendant = pdf.add(
    '<< /Type /Font /Subtype /CIDFontType0 /BaseFont /STSong-Light ' +
    '/CIDSystemInfo << /Registry (Adobe) /Ordering (GB1) /Supplement 2 >> ' +
    '/FontDescriptor << /Type /FontDescriptor /FontName /STSong-Light /Flags 4 ' +
    '/FontBBox [-25 -254 1000 880] /ItalicAngle 0 /Ascent 880 /Descent -254 ' +
    '/CapHeight 626 /StemV 58 >> /DW 1000 >>');

  const cid = pdf.add(
    `<< /Type /Font /Subtype /Type0 /BaseFont /STSong-Light-UniGB-UCS2-H ` +
    `/Encoding /UniGB-UCS2-H /DescendantFonts [${descendant} 0 R] >>`);

  // UniGB-UCS2-H takes UTF-16BE code units, so the string is the text's own code points as bytes.
  const utf16 = (text) => {
    let out = '';

    for (const ch of text) {
      const code = ch.codePointAt(0);

      out += String.fromCharCode((code >> 8) & 0xff, code & 0xff);
    }

    return out;
  };

  const hex = (text) => Buffer.from(utf16(text), 'latin1').toString('hex');

  const content = pdf.add({ dict: '', stream:
    'BT /FR 18 Tf 72 740 Td (CJK text in a non-embedded CID font) Tj ET\n' +
    `BT /FC 28 Tf 72 690 Td <${hex('你好，世界！')}> Tj ET\n` +
    `BT /FC 20 Tf 72 640 Td <${hex('这是一个用于测试字符映射的示例文档。')}> Tj ET\n` +
    'BT /FR 10 Tf 72 600 Td (Rendering the two lines above requires cmaps/UniGB-UCS2-H.bcmap and a) Tj ET\n' +
    'BT /FR 10 Tf 72 586 Td (substitute font, both fetched by the worker from the asset directories.) Tj ET',
  });

  const page = pdf.add(
    `<< /Type /Page /Parent ${pagesNumber} 0 R /MediaBox [0 0 612 792] ` +
    `/Resources << /Font << /FR ${latin} 0 R /FC ${cid} 0 R >> >> /Contents ${content} 0 R >>`);

  pdf.put(pagesNumber, `<< /Type /Pages /Count 1 /Kids [${page} 0 R] >>`);
  pdf.put(catalogNumber, `<< /Type /Catalog /Pages ${pagesNumber} 0 R /Lang (zh-CN) >>`);

  return pdf.build(catalogNumber, '5465737365726165434a4b31');
}

/* ----------------------------------------------------------- sample-forms */

/**
 * An AcroForm with one of each widget the annotation layer knows how to make interactive. No
 * JavaScript - the fields just hold what is typed into them, which is what
 * AnnotationMode.EnableStorage and SaveAsync are about.
 */
function formsPdf() {
  const pdf = new Pdf();

  const pagesNumber   = pdf.reserve();
  const catalogNumber = pdf.reserve();
  const acroNumber    = pdf.reserve();
  const pageNumber    = pdf.reserve();

  const font   = pdf.add('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>');
  const zapf   = pdf.add('<< /Type /Font /Subtype /Type1 /BaseFont /ZapfDingbats >>');

  const label = (y, text) => `BT /F1 10 Tf 72 ${y} Td (${escape(text)}) Tj ET`;

  const content = pdf.add({ dict: '', stream:
    'BT /F1 20 Tf 72 740 Td (A fillable form) Tj ET\n' +
    label(700, 'Every field below is a real input once the annotation layer is interactive.') + '\n' +
    label(650, 'Name') + '\n' +
    label(600, 'Email') + '\n' +
    label(550, 'Notes') + '\n' +
    label(470, 'Subscribe') + '\n' +
    label(420, 'Plan'),
  });

  const widget = (rect, extra) =>
    `<< /Type /Annot /Subtype /Widget /Rect [${rect.join(' ')}] /P ${pageNumber} 0 R ` +
    `/F 4 /DA (/Helv 10 Tf 0 g) /MK << /BC [0.4 0.4 0.4] /BG [1 1 1] >> ${extra} >>`;

  const fields = [
    pdf.add(widget([180, 644, 480, 666], '/FT /Tx /T (name) /TU (Your full name) /V (Ada Lovelace)')),
    pdf.add(widget([180, 594, 480, 616], '/FT /Tx /T (email) /TU (Where we should reply) /V ()')),
    pdf.add(widget([180, 520, 480, 590], '/FT /Tx /Ff 4096 /T (notes) /TU (Anything else) /V ()')),
    pdf.add(widget([180, 464, 196, 480], '/FT /Btn /T (subscribe) /TU (Send occasional updates) /V /Off /AS /Off /DA (/ZaDb 0 Tf 0 g)')),
    pdf.add(widget([180, 414, 340, 436], '/FT /Ch /Ff 131072 /T (plan) /TU (Which plan) /Opt [(Free) (Team) (Enterprise)] /V (Team)')),
  ];

  pdf.put(pageNumber,
    `<< /Type /Page /Parent ${pagesNumber} 0 R /MediaBox [0 0 612 792] ` +
    `/Resources << /Font << /F1 ${font} 0 R >> >> /Contents ${content} 0 R ` +
    `/Annots [${fields.map((n) => `${n} 0 R`).join(' ')}] >>`);

  pdf.put(pagesNumber, `<< /Type /Pages /Count 1 /Kids [${pageNumber} 0 R] >>`);

  pdf.put(acroNumber,
    `<< /Fields [${fields.map((n) => `${n} 0 R`).join(' ')}] /NeedAppearances true ` +
    `/DA (/Helv 10 Tf 0 g) /DR << /Font << /Helv ${font} 0 R /ZaDb ${zapf} 0 R >> >> >>`);

  pdf.put(catalogNumber, `<< /Type /Catalog /Pages ${pagesNumber} 0 R /AcroForm ${acroNumber} 0 R >>`);

  return pdf.build(catalogNumber, '54657373657261654636726d7331');
}

/* ------------------------------------------------------- sample-scripting */

/**
 * The form the Scripting page opens: two numbers and a total the document computes itself.
 *
 * Three things have to line up for that to happen, and all three are the reason this fixture is
 * hand-written. The total field carries a calculate action (/AA /C) calling AFSimple_Calculate,
 * which is a function the pdf.js sandbox provides; /AcroForm /CO lists the calculation order, which
 * is what makes the viewer re-run it after an edit; and a document-level /Names /JavaScript entry
 * runs on open, which is what proves the sandbox came up at all.
 */
function scriptingPdf() {
  const pdf = new Pdf();

  const pagesNumber   = pdf.reserve();
  const catalogNumber = pdf.reserve();
  const acroNumber    = pdf.reserve();
  const pageNumber    = pdf.reserve();

  const font = pdf.add('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>');

  const label = (y, text) => `BT /F1 11 Tf 72 ${y} Td (${escape(text)}) Tj ET`;

  const content = pdf.add({ dict: '', stream:
    'BT /F1 20 Tf 72 740 Td (An invoice that adds itself up) Tj ET\n' +
    label(700, 'Type into either amount. The total is computed by JavaScript inside the document.') + '\n' +
    label(650, 'Subtotal') + '\n' +
    label(600, 'Shipping') + '\n' +
    label(550, 'Total'),
  });

  const widget = (rect, extra) =>
    `<< /Type /Annot /Subtype /Widget /Rect [${rect.join(' ')}] /P ${pageNumber} 0 R ` +
    `/F 4 /FT /Tx /DA (/Helv 11 Tf 0 g) /MK << /BC [0.4 0.4 0.4] /BG [1 1 1] >> ${extra} >>`;

  const subtotal = pdf.add(widget([200, 644, 380, 666], '/T (subtotal) /TU (Subtotal) /V (100) /Q 2'));
  const shipping = pdf.add(widget([200, 594, 380, 616], '/T (shipping) /TU (Shipping) /V (12.5) /Q 2'));

  // AFSimple_Calculate is built into the sandbox, so the document does not have to ship the
  // arithmetic - which is also what makes this a test of the sandbox rather than of eval.
  // The script goes through escape() rather than being escaped by hand: every parenthesis in it has
  // to be backslashed, because PDF's own string delimiters are parentheses, and one miscounted
  // closer ends the string early - which pdf.js reports as "Illegal character: 41" from its lexer,
  // with no hint that a script is involved.
  const calculate = 'AFSimple_Calculate("SUM", new Array("subtotal", "shipping"));';

  const total = pdf.add(widget([200, 544, 380, 566],
    '/T (total) /TU (Total) /V () /Q 2 /Ff 1 ' +
    `/AA << /C << /S /JavaScript /JS (${escape(calculate)}) >> >>`));

  pdf.put(pageNumber,
    `<< /Type /Page /Parent ${pagesNumber} 0 R /MediaBox [0 0 612 792] ` +
    `/Resources << /Font << /F1 ${font} 0 R >> >> /Contents ${content} 0 R ` +
    `/Annots [${subtotal} 0 R ${shipping} 0 R ${total} 0 R] >>`);

  pdf.put(pagesNumber, `<< /Type /Pages /Count 1 /Kids [${pageNumber} 0 R] >>`);

  pdf.put(acroNumber,
    `<< /Fields [${subtotal} 0 R ${shipping} 0 R ${total} 0 R] /NeedAppearances true ` +
    `/CO [${total} 0 R] /DA (/Helv 11 Tf 0 g) /DR << /Font << /Helv ${font} 0 R >> >> >>`);

  // Runs when the document opens. console.println goes to the browser console through the sandbox,
  // and computing the total here means the field is right before anything has been typed.
  const onOpen =
    'console.println("Tesserae.Pdf scripting sample: document JavaScript ran."); ' +
    'this.getField("total").value = this.getField("subtotal").value * 1 + this.getField("shipping").value * 1;';

  const openScript = pdf.add(`<< /S /JavaScript /JS (${escape(onOpen)}) >>`);

  const names = pdf.add(`<< /JavaScript << /Names [(open) ${openScript} 0 R] >> >>`);

  pdf.put(catalogNumber,
    `<< /Type /Catalog /Pages ${pagesNumber} 0 R /AcroForm ${acroNumber} 0 R /Names ${names} 0 R >>`);

  return pdf.build(catalogNumber, '546573736572616553637269707431');
}

/* ------------------------------------------------------- sample-protected */

/**
 * The same shape as the others, encrypted with the standard security handler: user password
 * "tesserae", 40-bit RC4 (revision 2), and a permission mask that denies printing and copying.
 *
 * The permissions matter as much as the password: a document that allows everything reports no
 * permissions at all, so this is the only fixture on which GetPermissionsAsync returns a list.
 */
function protectedPdf() {
  const USER_PASSWORD  = 'tesserae';
  const OWNER_PASSWORD = 'tesserae-owner';

  // Print (bit 3), copy (bit 5) and high-quality print (bit 12) cleared, every reserved high bit
  // set as revision 2 requires. -1 is "allow everything". High-quality print goes with plain print:
  // granting it while denying print is legal but incoherent, and would read as a bug in the
  // permissions page rather than in the fixture.
  const PERMISSIONS = -1 & ~0x04 & ~0x10 & ~0x800;

  const id = '546573736572616550726f74656374';

  const pdf = new Pdf();

  const pagesNumber   = pdf.reserve();
  const catalogNumber = pdf.reserve();
  const encryptNumber = pdf.reserve();

  const font = pdf.add('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>');

  const pageNumbers = [];

  for (let i = 0; i < 2; i++) {
    const content = pdf.add({ dict: '', stream:
      `BT /F1 20 Tf 72 740 Td (Encrypted document - page ${i + 1} of 2) Tj ET\n` +
      'BT /F1 11 Tf 72 700 Td (The user password is "tesserae".) Tj ET\n' +
      'BT /F1 11 Tf 72 676 Td (This document denies printing and copying, so it is the one) Tj ET\n' +
      'BT /F1 11 Tf 72 660 Td (fixture whose permissions come back as a list rather than null.) Tj ET',
    });

    pageNumbers.push(pdf.add(
      `<< /Type /Page /Parent ${pagesNumber} 0 R /MediaBox [0 0 612 792] ` +
      `/Resources << /Font << /F1 ${font} 0 R >> >> /Contents ${content} 0 R >>`));
  }

  pdf.put(pagesNumber, `<< /Type /Pages /Count 2 /Kids [${pageNumbers.map((n) => `${n} 0 R`).join(' ')}] >>`);
  pdf.put(catalogNumber, `<< /Type /Catalog /Pages ${pagesNumber} 0 R >>`);

  // Algorithm 3: the owner value is the padded user password, RC4'd with a key from the owner one.
  const ownerValue = rc4(md5(padPassword(OWNER_PASSWORD)).subarray(0, 5), padPassword(USER_PASSWORD));

  // Algorithm 2: the file key, from the user password, the owner value, the permissions as a
  // little-endian signed int, and the file id.
  const permissionBytes = Buffer.alloc(4);
  permissionBytes.writeInt32LE(PERMISSIONS, 0);

  const fileKey = md5(padPassword(USER_PASSWORD), ownerValue, permissionBytes, Buffer.from(id, 'hex')).subarray(0, 5);

  // Algorithm 4: the user value is the padding string RC4'd with the file key. That is what a
  // reader recomputes to check a password.
  const userValue = rc4(fileKey, PAD);

  pdf.put(encryptNumber,
    `<< /Filter /Standard /V 1 /R 2 /Length 40 ` +
    `/O <${ownerValue.toString('hex')}> /U <${userValue.toString('hex')}> /P ${PERMISSIONS} >>`);

  // Set last, so the strings and streams above are encrypted but the /Encrypt dictionary is not.
  pdf.encryptNumber = encryptNumber;
  pdf.encryptWith(fileKey);

  return pdf.build(catalogNumber, id);
}

/* ------------------------------------------------------------------- main */

const documents = {
  'sample-outline.pdf':   outlinePdf,
  'sample-images.pdf':    imagesPdf,
  'sample-cjk.pdf':       cjkPdf,
  'sample-forms.pdf':     formsPdf,
  'sample-scripting.pdf': scriptingPdf,
  'sample-protected.pdf': protectedPdf,
};

for (const [name, make] of Object.entries(documents)) {
  const bytes = make();

  writeFileSync(join(OUT, name), bytes);
  console.log(`${name.padEnd(22)} ${String(bytes.length).padStart(7)} bytes`);
}

console.log(`\n${Object.keys(documents).length} documents -> ${OUT}/`);
