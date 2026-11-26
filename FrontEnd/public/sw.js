// EMG Construction ERP — Safe PWA Service Worker
// Security rule: NEVER cache /api/* responses, auth cookies, or confidential financial data.

const CACHE_NAME = 'emg-shell-v1';
const SHELL_ASSETS = [
  '/manifest.webmanifest',
  '/emg-logo.png',
  '/emg-logo-3d.png'
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(CACHE_NAME)
      .then((cache) => cache.addAll(SHELL_ASSETS))
      .catch(() => undefined)
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((key) => key.startsWith('emg-shell-') && key !== CACHE_NAME).map((key) => caches.delete(key))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('message', (event) => {
  if (event.data && event.data.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
});

self.addEventListener('fetch', (event) => {
  const { request } = event;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

  // Never intercept or cache API or authentication endpoints
  if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/auth/')) {
    return;
  }

  // Only serve static brand/manifest assets cache-first; keep HTML/JS network-first for safe updates
  if (SHELL_ASSETS.includes(url.pathname)) {
    event.respondWith(
      caches.match(request).then((cached) => cached || fetch(request))
    );
    return;
  }

  // Navigation requests: network-first with friendly Arabic offline fallback
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request).catch(
        () =>
          new Response(
            '<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/><title>EMG – انقطاع الاتصال</title><style>body{font-family:system-ui,sans-serif;background:#0b2042;color:#fff;display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;padding:24px;text-align:center}.card{max-width:400px;background:rgba(255,255,255,.08);border:1px solid rgba(255,255,255,.2);border-radius:16px;padding:28px}button{margin-top:16px;padding:10px 24px;border-radius:10px;border:none;background:#1e88e5;color:#fff;font-size:15px;font-weight:700;cursor:pointer}</style></head><body><div class="card"><h2>تعذّر الاتصال بالخادم</h2><p>يرجى التحقق من اتصال الإنترنت ثم إعادة المحاولة. لا يتم حفظ أي معاملات مالية بدون اتصال مباشر.</p><button onclick="location.reload()">إعادة المحاولة</button></div></body></html>',
            { status: 503, headers: { 'Content-Type': 'text/html; charset=utf-8' } }
          )
      )
    );
  }
});
