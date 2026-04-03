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

async function initMocks() {
  const { worker } = await import('@/mocks/browser');
  return worker.start({
    onUnhandledRequest: 'bypass', // Non bloccare richieste non gestite (es. font, next internals)
  });
}

export function MockProvider({ children }: MockProviderProps) {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    initMocks().then(() => setReady(true));
  }, []);

  if (!ready) {
    // Blocca il render fino all'avvio del worker per evitare race condition
    return null;
  }

  return <>{children}</>;
}
