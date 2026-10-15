// A static file server with the MIME types pdf.js needs - a module worker refuses anything but a
// JavaScript type, and the sandbox's .wasm must be application/wasm to compile streaming.
import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { extname, join, normalize, resolve } from 'node:path';

const TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js':   'text/javascript; charset=utf-8',
  '.mjs':  'text/javascript; charset=utf-8',
  '.json': 'application/json',
  '.css':  'text/css',
  '.wasm': 'application/wasm',
  '.pdf':  'application/pdf',
  '.svg':  'image/svg+xml',
  '.png':  'image/png',
};

/** Serves `root` under `/prefix/` on a free port; resolves with the base URL and a close function. */
export function serve(root, prefix = '/') {
  const base = resolve(root);

  const server = createServer(async (request, response) => {
    const path = decodeURIComponent(new URL(request.url, 'http://x').pathname);

    if (!path.startsWith(prefix)) { response.writeHead(404).end(); return; }

    let file = normalize(join(base, path.slice(prefix.length)));

    if (!file.startsWith(base)) { response.writeHead(403).end(); return; }

    try {
      if ((await stat(file)).isDirectory()) file = join(file, 'index.html');

      const body = await readFile(file);

      response.writeHead(200, { 'Content-Type': TYPES[extname(file)] ?? 'application/octet-stream' }).end(body);
    } catch {
      response.writeHead(404).end();
    }
  });

  return new Promise(done => server.listen(0, '127.0.0.1', () =>
    done({ url: `http://127.0.0.1:${server.address().port}${prefix}`, close: () => server.close() })));
}
