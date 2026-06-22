/**
 * StatePreviewProvider dynamic loader — CANONICAL CONSUMER ENTRY POINT.
 *
 * Asse B WP5 T5 (Issue #1897). DEC-4 + CRIT-5: Next.js dead-code-elimination
 * combined with `dynamic({ssr:false, loading: () => null})` guarantees the
 * provider implementation is tree-shaken from production chunks (>99% strip).
 *
 * Verified via static analysis acceptance test in
 * `apps/web/__tests__/state-preview-tree-shake.test.ts`.
 *
 * Consumers MUST import from the barrel `@/components/ui/state-preview`,
 * which re-exports this loader. Direct import of `state-preview-provider`
 * is blocked by ESLint rule `no-restricted-imports`.
 */

'use client';

import type { ComponentType } from 'react';

import dynamic from 'next/dynamic';

import type { StatePreviewProviderProps } from './state-preview-types';

export const StatePreviewProvider: ComponentType<StatePreviewProviderProps> = dynamic(
  () => import('./state-preview-provider').then(m => m.StatePreviewProvider),
  { ssr: false, loading: () => null }
);
