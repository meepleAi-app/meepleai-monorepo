/**
 * Library Game Detail Page — Responsive Layout
 *
 * Mobile: GameDetailMobile (vertical scroll, bottom sheets) — replaced by S5
 * Desktop: GameDetailDesktop (SplitViewLayout + MeepleCard hero + GameTabsPanel)
 *
 * S4 (library-to-game epic): migrated desktop path from GameTableLayout to
 * GameDetailDesktop with 5 tabs (Info / AI Chat / Toolbox / House Rules / Partite).
 */

'use client';

import { useCallback } from 'react';

import { ArrowLeft } from 'lucide-react';
import { useParams, useRouter, useSearchParams } from 'next/navigation';

import { LibroGameDetailView } from '@/components/features/gamebook/LibroGameDetailView';
import { NanolithCampaignCTA } from '@/components/features/gamebook/NanolithCampaignCTA';
import { GameDetailDesktop } from '@/components/game-detail/GameDetailDesktop';
import { isGameTabId, type GameTabId } from '@/components/game-detail/tabs';
import { GameTableSkeleton } from '@/components/library/game-table';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { tryLoadVisualTestFixture } from '@/lib/games/game-detail-visual-test-fixture';
import { isLibroGame } from '@/lib/games/is-libro-game';
import { useStateOverride } from '@/lib/visual-test/state-override';

import GameDetailMobile from './game-detail-mobile';

/**
 * WS-D Exemplar (Issue #1070, umbrella #1066): canonical states declared in
 * `admin-mockups/design_files/nanolith-runthrough-game-detail.html` Stati line.
 * Drives `?state=` URL override via the WS-D Foundation helper
 * (`useStateOverride`) so visual regression CI can exercise each branch.
 */
const LIBRARY_GAME_DETAIL_STATES = ['default', 'loading', 'error', 'not-found'] as const;

export default function LibraryGameDetailPage() {
  const params = useParams();
  const router = useRouter();
  const searchParams = useSearchParams();
  const gameId = params?.gameId as string;

  // WS-D Exemplar: state-override takes precedence over real fetch when the
  // visual-test build flag is set. In production the hook short-circuits to
  // null and the real fetch flow runs unchanged.
  const stateOverride = useStateOverride(LIBRARY_GAME_DETAIL_STATES);
  const fixtureDetail = stateOverride === 'default' ? tryLoadVisualTestFixture('default') : null;

  const {
    data: gameDetail,
    isLoading,
    error,
  } = useLibraryGameDetail(gameId, stateOverride === null);

  // S4 — parse `?tab=` query param to open a specific tab (deep-link support)
  const tabParam = searchParams?.get('tab');
  const initialTab: GameTabId = isGameTabId(tabParam) ? tabParam : 'info';

  const handleTabChange = useCallback(
    (tab: GameTabId) => {
      const next = new URLSearchParams(searchParams?.toString() ?? '');
      if (tab === 'info') {
        next.delete('tab');
      } else {
        next.set('tab', tab);
      }
      const qs = next.toString();
      router.replace(`/library/${gameId}${qs ? `?${qs}` : ''}`, { scroll: false });
    },
    [gameId, router, searchParams]
  );

  // State-override branches (visual-test build only). AC-D.4: `error` and
  // `not-found` rendered as visually distinct surfaces (red alert + retry CTA
  // vs neutral alert + "torna alla libreria" CTA).
  if (stateOverride === 'loading' || (stateOverride === null && isLoading)) {
    return <GameTableSkeleton />;
  }

  if (stateOverride === 'error' || (stateOverride === null && error)) {
    const errorMessage =
      stateOverride === 'error'
        ? 'Si è verificato un errore di rete nel caricamento del gioco. Riprova.'
        : error instanceof Error
          ? error.message
          : 'Si è verificato un errore nel caricamento del gioco.';
    return (
      <div className="mx-auto max-w-6xl px-4 py-8" data-testid="error-state">
        <Alert variant="destructive">
          <AlertTitle>Errore di caricamento</AlertTitle>
          <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
        <Button variant="outline" className="mt-4" onClick={() => router.refresh()}>
          Riprova
        </Button>
        <Button variant="ghost" className="ml-2 mt-4" onClick={() => router.push('/library')}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Torna alla Libreria
        </Button>
      </div>
    );
  }

  const effectiveDetail = fixtureDetail ?? gameDetail;

  if (stateOverride === 'not-found' || (stateOverride === null && !effectiveDetail)) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-8" data-testid="not-found-state">
        <Alert>
          <AlertTitle>Gioco non trovato</AlertTitle>
          <AlertDescription>Questo gioco non è presente nella tua libreria.</AlertDescription>
        </Alert>
        <Button variant="outline" className="mt-4" onClick={() => router.push('/library')}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Torna alla Libreria
        </Button>
      </div>
    );
  }

  // Type-narrowing: not-found branch above already returned when effectiveDetail
  // is falsy. The `if (!effectiveDetail)` guard re-narrows for TypeScript without
  // a non-null assertion (lint rule no-non-null-assertion compliance).
  if (!effectiveDetail) {
    return null;
  }

  // Iter B1 · Libro-game games render the storyboard-compliant detail view
  // (light warm palette, session+game gradient hero, pip bar, inline tabs).
  // Non-libro-game titles keep the legacy GameDetailDesktop until B2/B3.
  const renderLibroView = isLibroGame({ gameTitle: effectiveDetail.gameTitle });
  if (renderLibroView) {
    return <LibroGameDetailView gameDetail={effectiveDetail} />;
  }

  return (
    <div className="h-full">
      {/* CTA legacy hook — no-op for non-libro-game games (isLibroGame === false) */}
      <div className="mx-auto w-full max-w-2xl px-4 pt-4 pb-1">
        <NanolithCampaignCTA gameId={gameId} gameTitle={effectiveDetail.gameTitle} />
      </div>

      {/* Mobile layout — replaced by S5 */}
      <div className="lg:hidden">
        <GameDetailMobile gameId={gameId} />
      </div>

      {/* Desktop layout — S4: GameDetailDesktop with 5 tabs */}
      <div className="hidden h-full lg:block">
        <GameDetailDesktop gameId={gameId} initialTab={initialTab} onTabChange={handleTabChange} />
      </div>
    </div>
  );
}
