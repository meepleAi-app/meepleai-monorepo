/**
 * StatePreviewProvider dynamic loader — CANONICAL CONSUMER ENTRY POINT.
 *
 * Asse B WP5 T5 (Issue #1897). DEC-4 + CRIT-5: Next.js dead-code-elimination
 * combined with a dev-only `next/dynamic` import guarantees the provider
 * implementation is tree-shaken from production chunks (>99% strip).
 *
 * Verified via static analysis acceptance test in
 * `apps/web/__tests__/state-preview-tree-shake.test.ts`.
 *
 * ────────────────────────────────────────────────────────────────────────
 * Issue #2770 — DO NOT pass `ssr: false` here.
 *
 * This provider wraps `<AppContent>{children}</AppContent>` for the ENTIRE
 * app (providers.tsx). A `next/dynamic({ ssr: false })` component throws
 * `BailoutToCSR` during server rendering; React then client-renders the whole
 * subtree, so NO route emitted any body HTML server-side. The staging login
 * form only appeared after heavy client hydration — the flaky E2E "Auth login
 * form visibile" (the input exceeded the 30s timeout on the 3 GB headless VPS
 * runner). Confirmed by curling the SSR HTML: it contained a single
 * `BAILOUT_TO_CLIENT_SIDE_RENDERING` template and zero app content.
 *
 * Fix: StatePreview is a dev-only tool. In production we render children
 * directly (a zero-cost pass-through) so SSR works for every route AND the
 * provider impl tree-shakes out entirely (the dead `import()` below is
 * eliminated once `process.env.NODE_ENV` is inlined). In development we load
 * the real provider lazily WITHOUT `ssr: false`, so dev SSRs like production.
 * ────────────────────────────────────────────────────────────────────────
 *
 * Consumers MUST import from the barrel `@/components/ui/state-preview`,
 * which re-exports this loader. Direct import of `state-preview-provider`
 * is blocked by ESLint rule `no-restricted-imports`.
 */

'use client';

import type { ComponentType, ReactNode } from 'react';

import dynamic from 'next/dynamic';

import type { StatePreviewProviderProps } from './state-preview-types';

/**
 * Production pass-through: renders children with no context wrapper. Safe
 * because every consumer short-circuits in production — `useStatePreview`
 * returns a fixed no-op and `StatePreviewToggle` renders null. Keeping this a
 * plain synchronous component is what restores server-side rendering.
 */
function StatePreviewPassThrough({ children }: StatePreviewProviderProps): ReactNode {
  return <>{children}</>;
}

/**
 * Dev-only lazy provider. The `import()` lives in the dead branch of the
 * build-time-constant ternary below, so production builds eliminate it and the
 * heavy impl never reaches a client chunk. No `ssr: false` → the provider
 * server-renders in dev (mirrors production), instead of bailing the app to
 * client-side rendering.
 */
export const StatePreviewProvider: ComponentType<StatePreviewProviderProps> =
  process.env.NODE_ENV === 'production'
    ? StatePreviewPassThrough
    : (dynamic(
        () => import('./state-preview-provider').then(m => ({ default: m.StatePreviewProvider })),
        { loading: () => null }
      ) as ComponentType<StatePreviewProviderProps>);
