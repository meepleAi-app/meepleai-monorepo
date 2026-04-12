/**
 * MSW Browser ServiceWorker setup
 *
 * Initialized by MockProvider when NEXT_PUBLIC_MOCK_MODE=true.
 * Intercepts all fetch() calls in the browser and returns mock responses.
 *
 * Prerequisites:
 * - Run `npx msw init public/ --save` to generate /public/mockServiceWorker.js
 * - Set NEXT_PUBLIC_MOCK_MODE=true (or use `pnpm dev:mock`)
 *
 * Group toggles (build-time env vars):
 * - NEXT_PUBLIC_MSW_ENABLE=auth,games  — activate only listed groups
 * - NEXT_PUBLIC_MSW_DISABLE=admin      — activate all except listed groups
 *   (DISABLE takes precedence over ENABLE for any given group name)
 */
import { setupWorker } from 'msw/browser';

import { HANDLER_GROUPS, type HandlerGroupBase } from './handlers/registry';

import type { HttpHandler } from 'msw';

// Inline copies of the toggle helpers (also defined in @/dev-tools/mockControlCore
// and @/dev-tools/mswHandlerRegistry). Duplicated here so that mocks/ stays
// self-contained and the dev-tools/ folder can be removed without breaking
// the typecheck — see .github/workflows/dev-tools-isolation.yml.
function parseGroupList(raw: string | null | undefined): string[] {
  if (!raw) return [];
  return raw
    .split(',')
    .map(s => s.trim())
    .filter(s => s.length > 0);
}

function computeGroupToggles(
  allGroups: string[],
  enableList: string[],
  disableList: string[]
): Record<string, boolean> {
  const result: Record<string, boolean> = {};
  const hasEnableList = enableList.length > 0;
  for (const g of allGroups) {
    if (disableList.includes(g)) {
      result[g] = false;
    } else if (hasEnableList) {
      result[g] = enableList.includes(g);
    } else {
      result[g] = true;
    }
  }
  return result;
}

function buildActiveHandlers(
  groups: HandlerGroupBase[],
  groupToggles: Record<string, boolean>
): HttpHandler[] {
  const active: HttpHandler[] = [];
  for (const group of groups) {
    if (groupToggles[group.name] !== false) {
      active.push(...group.handlers);
    }
  }
  return active;
}

function getInitialHandlers(): HttpHandler[] {
  const enable = parseGroupList(process.env.NEXT_PUBLIC_MSW_ENABLE);
  const disable = parseGroupList(process.env.NEXT_PUBLIC_MSW_DISABLE);
  const allNames = HANDLER_GROUPS.map(g => g.name);
  const groupToggles = computeGroupToggles(allNames, enable, disable);
  return buildActiveHandlers(HANDLER_GROUPS, groupToggles);
}

export const worker = setupWorker(...getInitialHandlers());

// HMR support for Turbopack/Webpack Fast Refresh.
// When this module (or its handler dependencies) is hot-replaced, stop the
// current worker and notify MockProvider to restart the new worker instance.
// Without this, the new worker created by re-evaluation would never be started,
// causing all subsequent fetch calls to reach the proxy route instead of MSW.
if (typeof window !== 'undefined') {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const hot = (typeof module !== 'undefined' ? (module as any).hot : undefined) as
    | { accept(): void; dispose(fn: () => void): void }
    | undefined;

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
