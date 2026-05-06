/**
 * Session Live page — /sessions/[id]/live
 *
 * Wave D.2 Foundation sub-PR (Issue #746): thin client shell.
 *
 * All rendering, data-fetching, and FSM logic live in SessionLiveView.
 *
 * Suspense boundary required: SessionLiveView calls `useSearchParams()` for
 * URL-based state SSOT (?tab, ?mtab, ?fixture, ?state, ?dialog), which triggers
 * CSR bailout during static prerender (Next.js 16). Wrapping in Suspense allows
 * the page to prerender without search params resolved on client hydration.
 *
 * Mirror: Wave D.1 sessions/page.tsx pattern (PR #736).
 *
 * Subroutes preserved (UNTOUCHED):
 *   /sessions/[id]/page.tsx     — D.3 summary (not yet migrated)
 *   /sessions/[id]/diary/*      — existing diary routes
 *   /sessions/[id]/layout.tsx   — existing layout
 *
 * @see apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx
 */

'use client';

import { Suspense } from 'react';

import { SessionLiveView } from './_components/SessionLiveView';

export default function SessionLivePage() {
  return (
    <Suspense>
      <SessionLiveView />
    </Suspense>
  );
}
