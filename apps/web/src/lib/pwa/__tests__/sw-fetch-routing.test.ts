/**
 * #4987: il service worker riconosceva un asset statico solo dall'estensione del percorso, quindi
 * intercettava anche le immagini di altre origini (le copertine su MinIO/R2) e le richiedeva con
 * fetch(). Dentro il service worker vale `connect-src`, che non elenca l'host delle immagini: la
 * fetch falliva e il catch rispondeva 503 «Offline», con le copertine rotte.
 *
 * Il test esegue il vero `public/sw.js` in una sandbox e guarda se il listener di `fetch` prende
 * in carico la richiesta (`respondWith`) oppure la lascia al browser.
 */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import vm from 'node:vm';

import { describe, expect, it, vi } from 'vitest';

const SW_ORIGIN = 'http://localhost:3000';

type FetchListener = (event: { request: FakeRequest; respondWith: (r: unknown) => void }) => void;

interface FakeRequest {
  method: string;
  url: string;
  mode: string;
}

function loadFetchListener(): FetchListener {
  const source = readFileSync(join(process.cwd(), 'public', 'sw.js'), 'utf8');
  const listeners: Record<string, FetchListener> = {};
  const self = {
    location: new URL(`${SW_ORIGIN}/sw.js`),
    addEventListener: (type: string, listener: FetchListener) => {
      listeners[type] = listener;
    },
    skipWaiting: vi.fn(),
    clients: { claim: vi.fn(), matchAll: vi.fn(async () => []), openWindow: vi.fn() },
    registration: { showNotification: vi.fn() },
  };
  const caches = {
    match: vi.fn(async () => undefined),
    open: vi.fn(async () => ({ match: vi.fn(), put: vi.fn() })),
    keys: vi.fn(async () => []),
    delete: vi.fn(async () => true),
  };
  vm.runInNewContext(source, {
    self,
    caches,
    clients: self.clients,
    fetch: vi.fn(async () => new Response('ok')),
    Response,
    URL,
    console,
  });
  const listener = listeners.fetch;
  if (!listener) throw new Error('sw.js did not register a fetch listener');
  return listener;
}

function intercepts(url: string, mode = 'no-cors'): boolean {
  const respondWith = vi.fn();
  loadFetchListener()({ request: { method: 'GET', url, mode }, respondWith });
  return respondWith.mock.calls.length > 0;
}

describe('sw.js — quali richieste di asset statici intercetta (#4987)', () => {
  it.each([
    'http://localhost:9000/meepleai-uploads/bgg-covers/256382/cover.jpg?X-Amz-Signature=abc',
    'https://covers.example.r2.dev/bgg-covers/1/cover.png',
    'https://fonts.gstatic.com/s/quicksand/v31/font.woff2',
  ])('lascia al browser un asset di un’altra origine: %s', url => {
    expect(intercepts(url)).toBe(false);
  });

  it.each([
    `${SW_ORIGIN}/_next/static/chunks/app.js`,
    `${SW_ORIGIN}/icons/icon-192.png`,
    `${SW_ORIGIN}/fonts/inter.woff2`,
  ])('tiene CacheFirst per un asset della stessa origine: %s', url => {
    expect(intercepts(url, 'cors')).toBe(true);
  });
});
