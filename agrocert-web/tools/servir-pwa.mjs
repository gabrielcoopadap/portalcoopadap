import http from 'node:http';
import https from 'node:https';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const raiz = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'dist', 'portal-demo', 'browser');
const porta = Number(process.argv[2] ?? 8080);
const backend = new URL(process.argv[3] ?? 'https://127.0.0.1:7180');
const cliente = backend.protocol === 'https:' ? https : http;

const tipos = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.json': 'application/json', '.webmanifest': 'application/manifest+json',
  '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.webp': 'image/webp', '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon', '.woff': 'font/woff', '.woff2': 'font/woff2', '.ttf': 'font/ttf', '.otf': 'font/otf', '.txt': 'text/plain; charset=utf-8',
};

if (!fs.existsSync(path.join(raiz, 'index.html'))) {
  console.error(`Build não encontrado em ${raiz}. Rode antes: npx ng build portal-demo`);
  process.exit(1);
}

function repassar(req, res) {
  const pedido = cliente.request({
    hostname: backend.hostname, port: backend.port, path: req.url, method: req.method,
    headers: { ...req.headers, host: backend.host },
    rejectUnauthorized: false, // certificado de desenvolvimento do Visual Studio
  }, resp => { res.writeHead(resp.statusCode ?? 502, resp.headers); resp.pipe(res); });
  pedido.on('error', e => {
    console.error(`  backend fora do ar (${backend.origin}): ${e.message}`);
    res.writeHead(502, { 'content-type': 'application/json' });
    res.end(JSON.stringify({ mensagem: `Backend não respondeu em ${backend.origin}. Ele está rodando no Visual Studio?` }));
  });
  req.pipe(pedido);
}

function servirArquivo(req, res) {
  const url = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  let arquivo = path.normalize(path.join(raiz, url));
  if (!arquivo.startsWith(raiz)) { res.writeHead(403); return res.end(); }
  const existe = fs.existsSync(arquivo) && fs.statSync(arquivo).isFile();
  if (!existe) {
    if (path.extname(url)) { res.writeHead(404); return res.end('Não encontrado'); }
    arquivo = path.join(raiz, 'index.html');
  }
  const ext = path.extname(arquivo).toLowerCase();
  // index.html e o service worker nunca vão para o cache do navegador, senão a versão nova não chega.
  const semCache = ext === '.html' || /ngsw|worker/.test(path.basename(arquivo));
  res.writeHead(200, { 'content-type': tipos[ext] ?? 'application/octet-stream', 'cache-control': semCache ? 'no-cache' : 'public, max-age=3600' });
  fs.createReadStream(arquivo).pipe(res);
}

http.createServer((req, res) => {
  if (/^\/(api|uploads)(\/|$|\?)/.test(req.url)) return repassar(req, res);
  if (req.method !== 'GET' && req.method !== 'HEAD') { res.writeHead(405); return res.end(); }
  servirArquivo(req, res);
}).listen(porta, () => {
  console.log(`Portal (PWA) em http://localhost:${porta}  →  API repassada para ${backend.origin}`);
  console.log('Ctrl+C para parar.');
});
