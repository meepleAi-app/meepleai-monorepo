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

import { GameDetailDesktop } from '@/components/game-detail/GameDetailDesktop';
import { isGameTabId, type GameTabId } from '@/components/game-detail/tabs';
import { GameTableSkeleton } from '@/components/library/game-table';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';

import GameDetailMobile from './game-detail-mobile';

export default function LibraryGameDetailPage() {
  const params = useParams();
  const router = useRouter();
  const searchParams = useSearchParams();
  const gameId = params?.gameId as string;
  const { data: gameDetail, isLoading, error } = useLibraryGameDetail(gameId);

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
      router.replace(`/library/games/${gameId}${qs ? `?${qs}` : ''}`, { scroll: false });
    },
    [gameId, router, searchParams]
  );

  if (isLoading) return <GameTableSkeleton />;

  if (error) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-8" data-testid="error-state">
        <Alert variant="destructive">
          <AlertTitle>Errore</AlertTitle>
          <AlertDescription>
            {error instanceof Error
              ? error.message
              : 'Si è verificato un errore nel caricamento del gioco.'}
          </AlertDescription>
        </Alert>
        <Button variant="outline" className="mt-4" onClick={() => router.push('/library')}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Torna alla Libreria
        </Button>
      </div>
    );
  }

  if (!gameDetail) {
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

  return (
    <div className="h-full">
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
