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
