/**
 * StatePreview loader — SSR regression tests (Issue #2770).
 *
 * The loader wraps `<AppContent>{children}</AppContent>` for the ENTIRE app
 * (via providers.tsx). It previously mounted the provider with
 * `next/dynamic({ ssr: false })`, which throws `BailoutToCSR` during server
 * rendering and forced React to client-render the whole subtree — disabling
 * SSR for every route. The staging login form only appeared after (heavy)
 * client hydration, causing the flaky E2E "Auth login form visibile".
 *
 * These tests lock in the fix: in production the loader MUST render its
 * children server-side (no ssr:false bailout). We assert this at the unit
 * level with `renderToString` so the guarantee survives future refactors
 * without needing a full `next build` + curl.
 */

import type { ComponentType, ReactNode } from 'react';

import { renderToString } from 'react-dom/server';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type { StatePreviewProviderProps } from '../state-preview-types';

const CHILD_MARKER = 'ssr-child-marker-2770';

/**
 * Re-import the loader with a fresh module registry so the module-level
 * `process.env.NODE_ENV` branch is re-evaluated under the stubbed env.
 */
async function loadProvider(): Promise<ComponentType<StatePreviewProviderProps>> {
  vi.resetModules();
  const mod = await import('../state-preview-loader');
  return mod.StatePreviewProvider;
}

function Child(): ReactNode {
  return <div data-testid="ssr-child">{CHILD_MARKER}</div>;
}

describe('StatePreviewProvider loader — SSR (Issue #2770)', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  it('server-renders children in production (no ssr:false CSR bailout)', async () => {
    vi.stubEnv('NODE_ENV', 'production');
    const StatePreviewProvider = await loadProvider();

    const html = renderToString(
      <StatePreviewProvider>
        <Child />
      </StatePreviewProvider>
    );

    // The child MUST be present in the server-rendered HTML. With the old
    // `dynamic({ ssr: false })` loader this string was absent (bailout → null).
    expect(html).toContain(CHILD_MARKER);
  });

  it('renders children (pass-through) so it never gates the app tree', async () => {
    vi.stubEnv('NODE_ENV', 'production');
    const StatePreviewProvider = await loadProvider();

    const html = renderToString(
      <StatePreviewProvider>
        <Child />
      </StatePreviewProvider>
    );

    expect(html).toContain('data-testid="ssr-child"');
  });
});
