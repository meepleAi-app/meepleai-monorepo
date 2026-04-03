'use client';

/**
 * MockProvider — avvia MSW ServiceWorker in browser dev mode
 *
 * Attivato solo quando NEXT_PUBLIC_MOCK_MODE=true.
 * Blocca il render fino a worker.start() completato per evitare
 * race condition con le prime fetch dell'app.
 *
 * Prerequisito: public/mockServiceWorker.js deve esistere.
 * Generarlo con: cd apps/web && npx msw init public/ --save
 */

import { useEffect, useState } from 'react';

interface MockProviderProps {
  children: React.ReactNode;
}

/**
 * Unregister any service workers that are not MSW's mockServiceWorker.js.
 * This prevents the PWA sw.js from intercepting requests instead of MSW.
 */
async function unregisterConflictingSWs() {
  if (typeof window === 'undefined' || !('serviceWorker' in navigator)) return;
  const registrations = await navigator.serviceWorker.getRegistrations();
  for (const reg of registrations) {
    const swUrl =
      reg.active?.scriptURL || reg.installing?.scriptURL || reg.waiting?.scriptURL || '';
    if (!swUrl.includes('mockServiceWorker')) {
      await reg.unregister();
    }
  }
}

async function initMocks(skipControllerCheck = false) {
  await unregisterConflictingSWs();
  const { worker } = await import('@/mocks/browser');
  await worker.start({
    onUnhandledRequest: 'bypass', // Non bloccare richieste non gestite (es. font, next internals)
  });

  // MSW requires the Service Worker to control the current page.
  // If controller is null, the SW was just registered for the first time and
  // doesn't yet control this page (browser spec: newly-registered SWs only
  // control pages opened after registration, unless clients.claim() is called).
  // MSW's mockServiceWorker.js calls clients.claim() in the activate event,
  // but there can be a race on first load. Reloading ensures full SW control.
  // Skip this check on HMR restarts: the SW was already active before the restart
  // and will reclaim the page without a full reload.
  if (
    !skipControllerCheck &&
    typeof window !== 'undefined' &&
    !navigator.serviceWorker.controller
  ) {
    window.location.reload();
    // Never resolve: page is reloading, no need to unblock rendering
    return new Promise<void>(() => {});
  }
}

export function MockProvider({ children }: MockProviderProps) {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    initMocks().then(() => setReady(true));

    // Re-initialize MSW after Turbopack/Webpack Fast Refresh replaces browser.ts
    // or its handler dependencies. The old worker is stopped and this event is
    // dispatched by the module.hot.dispose hook in browser.ts. By the time this
    // handler runs, the new worker module has been evaluated and the dynamic
    // import below will return the replacement worker instance.
    function handleWorkerInvalidated() {
      setReady(false);
      initMocks(/* skipControllerCheck */ true).then(() => setReady(true));
    }

    window.addEventListener('msw:worker-invalidated', handleWorkerInvalidated);
    return () => {
      window.removeEventListener('msw:worker-invalidated', handleWorkerInvalidated);
    };
  }, []);

  if (!ready) {
    // Blocca il render fino all'avvio del worker per evitare race condition
    return null;
  }

  return <>{children}</>;
}
