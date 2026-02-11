/**
 * Service Worker for MeepleAI PWA (Issue #3346)
 *
 * Caching Strategies:
 * - Static assets: CacheFirst (CSS, JS, images)
 * - API calls: NetworkFirst with fallback
 * - Session data: StaleWhileRevalidate
 */

const CACHE_VERSION = 'meepleai-v1';
const STATIC_CACHE = `${CACHE_VERSION}-static`;
const DYNAMIC_CACHE = `${CACHE_VERSION}-dynamic`;
const API_CACHE = `${CACHE_VERSION}-api`;

// Assets to cache on install
const STATIC_ASSETS = [
  '/',
  '/offline',
  '/manifest.json',
  '/icons/icon-192x192.png',
  '/icons/icon-512x512.png',
];

// API routes that should be cached
const API_CACHE_PATTERNS = [
  /\/api\/v1\/games\/\d+$/,           // Individual game details
  /\/api\/v1\/games\/search/,          // Game search results
  /\/api\/v1\/library/,                // User library
  /\/api\/v1\/sessions\/[a-f0-9-]+$/,  // Individual session details
];

// Routes that should never be cached
const NO_CACHE_PATTERNS = [
  /\/api\/v1\/auth/,
  /\/api\/v1\/users\/me/,
  /\/_next\/webpack-hmr/,
  /\/__nextjs/,
  /pdf\.worker/,
];

// ============================================================================
// Install Event
// ============================================================================

self.addEventListener('install', (event) => {
  console.log('[SW] Installing service worker...');

  event.waitUntil(
    caches.open(STATIC_CACHE)
      .then((cache) => {
        console.log('[SW] Caching static assets');
        return cache.addAll(STATIC_ASSETS);
      })
      .then(() => self.skipWaiting())
  );
});

// ============================================================================
// Activate Event
// ============================================================================

self.addEventListener('activate', (event) => {
  console.log('[SW] Activating service worker...');

  event.waitUntil(
    caches.keys()
      .then((cacheNames) => {
        return Promise.all(
          cacheNames
            .filter((cacheName) => cacheName.startsWith('meepleai-') && cacheName !== STATIC_CACHE && cacheName !== DYNAMIC_CACHE && cacheName !== API_CACHE)
            .map((cacheName) => {
              console.log('[SW] Deleting old cache:', cacheName);
              return caches.delete(cacheName);
            })
        );
      })
      .then(() => self.clients.claim())
  );
});

// ============================================================================
// Fetch Event - Caching Strategies
// ============================================================================

self.addEventListener('fetch', (event) => {
  const { request } = event;
  const url = new URL(request.url);

  // Skip non-GET requests
  if (request.method !== 'GET') {
    return;
  }

  // Skip blob: URLs (e.g., PDF preview from File objects)
  if (request.url.startsWith('blob:')) {
    return;
  }

  // Skip requests that should never be cached
  if (shouldNotCache(url)) {
    return;
  }

  // API requests - NetworkFirst
  if (url.pathname.startsWith('/api/')) {
    event.respondWith(networkFirstStrategy(request));
    return;
  }

  // Static assets - CacheFirst
  if (isStaticAsset(url)) {
    event.respondWith(cacheFirstStrategy(request));
    return;
  }

  // Navigation requests - NetworkFirst with offline fallback
  if (request.mode === 'navigate') {
    event.respondWith(navigationStrategy(request));
    return;
  }

  // Default - StaleWhileRevalidate
  event.respondWith(staleWhileRevalidateStrategy(request));
});

// ============================================================================
// Caching Strategies
// ============================================================================

/**
 * CacheFirst - Best for static assets
 */
async function cacheFirstStrategy(request) {
  const cached = await caches.match(request);
  if (cached) {
    return cached;
  }

  try {
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(STATIC_CACHE);
      cache.put(request, response.clone());
    }
    return response;
  } catch {
    return new Response('Offline', { status: 503 });
  }
}

/**
 * NetworkFirst - Best for API calls
 */
async function networkFirstStrategy(request) {
  const url = new URL(request.url);

  try {
    const response = await fetch(request);

    // Cache successful API responses that match patterns
    if (response.ok && shouldCacheApi(url)) {
      const cache = await caches.open(API_CACHE);
      cache.put(request, response.clone());
    }

    return response;
  } catch {
    // Try to return cached response
    const cached = await caches.match(request);
    if (cached) {
      // Add header to indicate offline response
      const headers = new Headers(cached.headers);
      headers.set('X-From-Cache', 'true');
      return new Response(cached.body, {
        status: cached.status,
        statusText: cached.statusText,
        headers,
      });
    }

    // Return offline response for API
    return new Response(
      JSON.stringify({ error: 'Offline', message: 'You are currently offline' }),
      {
        status: 503,
        headers: { 'Content-Type': 'application/json' }
      }
    );
  }
}

/**
 * StaleWhileRevalidate - Best for frequently updated content
 */
async function staleWhileRevalidateStrategy(request) {
  const cache = await caches.open(DYNAMIC_CACHE);
  const cached = await cache.match(request);

  const networkPromise = fetch(request)
    .then((response) => {
      if (response.ok) {
        cache.put(request, response.clone());
      }
      return response;
    })
    .catch(() => cached);

  return cached || networkPromise;
}

/**
 * Navigation strategy with offline fallback
 */
async function navigationStrategy(request) {
  try {
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(DYNAMIC_CACHE);
      cache.put(request, response.clone());
    }
    return response;
  } catch {
    // Try cached version
    const cached = await caches.match(request);
    if (cached) {
      return cached;
    }

    // Return offline page
    const offlinePage = await caches.match('/offline');
    if (offlinePage) {
      return offlinePage;
    }

    return new Response('Offline', { status: 503 });
  }
}

// ============================================================================
// Helper Functions
// ============================================================================

function shouldNotCache(url) {
  return NO_CACHE_PATTERNS.some((pattern) => pattern.test(url.pathname));
}

function shouldCacheApi(url) {
  return API_CACHE_PATTERNS.some((pattern) => pattern.test(url.pathname));
}

function isStaticAsset(url) {
  const staticExtensions = ['.css', '.js', '.png', '.jpg', '.jpeg', '.gif', '.svg', '.woff', '.woff2', '.ttf', '.ico'];
  return staticExtensions.some((ext) => url.pathname.endsWith(ext)) || url.pathname.startsWith('/_next/static/');
}

// ============================================================================
// Background Sync
// ============================================================================

self.addEventListener('sync', (event) => {
  console.log('[SW] Sync event:', event.tag);

  if (event.tag === 'session-sync') {
    event.waitUntil(syncOfflineActions());
  }
});

async function syncOfflineActions() {
  console.log('[SW] Syncing offline actions...');

  // Broadcast to client to trigger sync
  const clients = await self.clients.matchAll();
  clients.forEach((client) => {
    client.postMessage({
      type: 'SYNC_OFFLINE_ACTIONS',
    });
  });
}

// ============================================================================
// Push Notifications (Future)
// ============================================================================

self.addEventListener('push', (event) => {
  if (!event.data) return;

  const data = event.data.json();
  const options = {
    body: data.body,
    icon: '/icons/icon-192x192.png',
    badge: '/icons/icon-96x96.png',
    vibrate: [100, 50, 100],
    data: {
      url: data.url || '/',
    },
  };

  event.waitUntil(
    self.registration.showNotification(data.title, options)
  );
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();

  const url = event.notification.data?.url || '/';

  event.waitUntil(
    self.clients.matchAll({ type: 'window' })
      .then((clients) => {
        // Focus existing window if available
        for (const client of clients) {
          if (client.url === url && 'focus' in client) {
            return client.focus();
          }
        }
        // Open new window
        return self.clients.openWindow(url);
      })
  );
});

// ============================================================================
// Message Handling
// ============================================================================

self.addEventListener('message', (event) => {
  console.log('[SW] Message received:', event.data);

  if (event.data?.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }

  if (event.data?.type === 'CLEAR_CACHE') {
    event.waitUntil(
      caches.keys().then((names) =>
        Promise.all(names.map((name) => caches.delete(name)))
      )
    );
  }
});
