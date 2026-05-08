/**
 * Gamebook Index Page — `/gamebook` (SP6 Phase B Tier M, Issue #788).
 *
 * Renders the user's gamebook library: 6-cell FSM
 * (loading | error | empty | default | quota-soft | quota-hard).
 *
 * Pattern: thin Suspense shell. All hook composition + URL state SSOT live in
 * the client orchestrator (Wave D.3 blueprint).
 *
 * Scope distinction (issue #836 Task 3):
 * - `/gamebook` (questa pagina): index globale dei manuali fotografati
 *   (collezione di gamebook caricati dall'utente, indipendenti da game/campagna).
 * - `/library/games/[gameId]/play`: resume picker delle campagne attive
 *   per un gioco specifico (4 stati mockup G — issue #835).
 * - `/library/games/[gameId]/play/[campaignId]`: shell di gioco in-campaign.
 *
 * Le tre route servono use case differenti e NON sono sovrapponibili.
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
