/**
 * Gamebook Index Page — `/gamebook` (SP6 Phase B Tier M, Issue #788).
 *
 * Renders the user's gamebook library: 6-cell FSM
 * (loading | error | empty | default | quota-soft | quota-hard).
 *
 * Pattern: thin Suspense shell. All hook composition + URL state SSOT live in
 * the client orchestrator (Wave D.3 blueprint).
 *
 * @see docs/superpowers/specs/2026-05-06-sp6-libro-game.md
 */

import { Suspense, type JSX } from 'react';

import { GamebookIndexView } from './_components/GamebookIndexView';

export const metadata = {
  title: 'I tuoi manuali | MeepleAI',
  description: 'Manuali fotografati pronti per il tuo tavolo',
};

function PageFallback(): JSX.Element {
  return (
    <div
      data-slot="gamebook-index-page-fallback"
      role="status"
      aria-live="polite"
      className="mx-auto max-w-[1280px] px-4 py-12"
    >
      <div className="h-48 animate-pulse rounded-lg bg-muted/40" />
    </div>
  );
}

export default function GamebookIndexPage(): JSX.Element {
  return (
    <Suspense fallback={<PageFallback />}>
      <GamebookIndexView />
    </Suspense>
  );
}
