/**
 * MSW Browser ServiceWorker setup
 *
 * Initialized by MockProvider when NEXT_PUBLIC_MOCK_MODE=true.
 * Intercepts all fetch() calls in the browser and returns mock responses.
 *
 * Prerequisites:
 * - Run `npx msw init public/ --save` to generate /public/mockServiceWorker.js
 * - Set NEXT_PUBLIC_MOCK_MODE=true (or use `pnpm dev:mock`)
 */
import { setupWorker } from 'msw/browser';

import { handlers } from './handlers';

export const worker = setupWorker(...handlers);

// HMR support for Turbopack/Webpack Fast Refresh.
// When this module (or its handler dependencies) is hot-replaced, stop the
// current worker and notify MockProvider to restart the new worker instance.
// Without this, the new worker created by re-evaluation would never be started,
// causing all subsequent fetch calls to reach the proxy route instead of MSW.
if (typeof window !== 'undefined') {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const hot = (module as any).hot as { accept(): void; dispose(fn: () => void): void } | undefined;

  if (hot) {
    hot.dispose(() => {
      // Stop the current worker so the SW is deactivated cleanly.
      worker.stop();
      // Defer the event dispatch so the new module finishes evaluating before
      // MockProvider's handler imports and starts the replacement worker.
      queueMicrotask(() => {
        window.dispatchEvent(new CustomEvent('msw:worker-invalidated'));
      });
    });
    // Mark this module as self-accepting so HMR applies updates here directly
    // (rather than bubbling up to cause a full page reload).
    hot.accept();
  }
}
