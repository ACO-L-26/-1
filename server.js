const http = require('http');
const https = require('https');
const fs = require('fs');
const path = require('path');
const url = require('url');

const PORT = 8080;
const ROOT = __dirname;

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css',
  '.js': 'application/javascript',
  '.json': 'application/json',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
};

// Proxy external APIs to bypass network restrictions
function proxyRequest(req, res, targetBase) {
  const parsed = url.parse(req.url);
  const targetUrl = targetBase + parsed.path.replace('/api/deezer', '') + (parsed.search || '');

  console.log('[Proxy] ->', targetUrl);

  const opts = {
    hostname: url.parse(targetBase).hostname,
    path: targetUrl.replace(targetBase, ''),
    method: req.method,
    headers: {
      'User-Agent': 'MusicDiscovery/1.0',
      'Accept': 'application/json',
    },
    timeout: 15000,
  };

  const proxy = https.request(opts, (proxyRes) => {
    let body = '';
    proxyRes.on('data', chunk => body += chunk);
    proxyRes.on('end', () => {
      res.writeHead(proxyRes.statusCode || 200, {
        'Content-Type': 'application/json; charset=utf-8',
        'Access-Control-Allow-Origin': '*',
      });
      res.end(body);
    });
  });

  proxy.on('error', (e) => {
    console.error('[Proxy] Error:', e.message);
    res.writeHead(502);
    res.end(JSON.stringify({ error: 'Proxy error: ' + e.message }));
  });

  proxy.on('timeout', () => {
    console.error('[Proxy] Timeout');
    proxy.destroy();
    res.writeHead(504);
    res.end(JSON.stringify({ error: 'Proxy timeout' }));
  });

  proxy.end();
}

http.createServer((req, res) => {
  const parsed = url.parse(req.url);
  let urlPath = parsed.pathname;

  // Proxy Deezer API
  if (urlPath.startsWith('/api/deezer/')) {
    proxyRequest(req, res, 'https://api.deezer.com');
    return;
  }

  // Static files
  let filePath = path.join(ROOT, urlPath);

  try {
    if (fs.existsSync(filePath) && fs.statSync(filePath).isDirectory()) {
      filePath = path.join(filePath, 'index.html');
    }
    if (!path.extname(filePath)) {
      filePath = path.join(filePath, 'index.html');
    }
    if (!fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
      res.writeHead(404, { 'Content-Type': 'text/plain' });
      res.end('Not found');
      return;
    }
    const ext = path.extname(filePath);
    res.writeHead(200, { 'Content-Type': MIME[ext] || 'application/octet-stream' });
    const stream = fs.createReadStream(filePath);
    stream.on('error', () => { res.writeHead(500); res.end('Server error'); });
    stream.pipe(res);
  } catch (e) {
    res.writeHead(500);
    res.end('Server error');
  }
}).listen(PORT, () => {
  console.log('Server running at http://127.0.0.1:' + PORT);
  console.log('Deezer API proxy: /api/deezer/...');
});
