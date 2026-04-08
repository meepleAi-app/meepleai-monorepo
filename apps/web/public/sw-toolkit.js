const CACHE_NAME = 'meepleai-toolkit-v1';

self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', event => {
  event.waitUntil(
    caches
      .keys()
      .then(keys => Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k))))
  );
  self.clients.claim();
});

self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);

  // NEVER cache API calls (security: authenticated responses)
  if (url.pathname.startsWith('/api/')) return;

  // Cache static assets only (JS, CSS, images, fonts)
  if (['script', 'style', 'image', 'font'].includes(event.request.destination)) {
    event.respondWith(
      caches.match(event.request).then(
        cached =>
          cached ||
          fetch(event.request).then(response => {
            const clone = response.clone();
            caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
            return response;
          })
      )
    );
  }
});
