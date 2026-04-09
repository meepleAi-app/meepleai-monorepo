/**
 * GameDetailMobile — S5 (library-to-game epic)
 *
 * New mobile layout: MeepleCard hero foreground + top-sheet drawer with the 5
 * game detail tabs (shared contract from S4). Replaces the previous 303-line
 * vertical-scroll layout.
 *
 * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.5
 */

'use client';

import { useState } from 'react';

import { ArrowLeft } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { FocusedGameCard } from '@/components/game-detail/mobile/FocusedGameCard';
import { GameDetailsDrawer } from '@/components/game-detail/mobile/GameDetailsDrawer';
import { type GameTabId } from '@/components/game-detail/tabs';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';

export interface GameDetailMobileProps {
  gameId: string;
}

export default function GameDetailMobile({ gameId }: GameDetailMobileProps) {
  const router = useRouter();
  const { data: game, isLoading, error } = useLibraryGameDetail(gameId);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [initialTab, setInitialTab] = useState<GameTabId>('info');

  const handleOpenDrawer = () => {
    setInitialTab('info');
    setDrawerOpen(true);
  };

  if (isLoading) {
    return (
      <div
        className="flex min-h-[60vh] items-center justify-center p-6 text-sm text-muted-foreground"
        data-testid="game-detail-mobile-loading"
      >
        Caricamento in corso…
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-4" data-testid="game-detail-mobile-error">
        <Alert variant="destructive">
          <AlertTitle>Errore</AlertTitle>
          <AlertDescription>
            {error instanceof Error
              ? error.message
              : 'Si è verificato un errore nel caricamento del gioco.'}
          </AlertDescription>
        </Alert>
        <Button variant="outline" className="mt-4 w-full" onClick={() => router.push('/library')}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Torna alla Libreria
        </Button>
      </div>
    );
  }

  if (!game) {
    return (
      <div className="p-4" data-testid="game-detail-mobile-not-found">
        <Alert>
          <AlertTitle>Gioco non trovato</AlertTitle>
          <AlertDescription>Questo gioco non è presente nella tua libreria.</AlertDescription>
        </Alert>
        <Button variant="outline" className="mt-4 w-full" onClick={() => router.push('/library')}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Torna alla Libreria
        </Button>
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col" data-testid="game-detail-mobile">
      <FocusedGameCard game={game} onOpenDrawer={handleOpenDrawer} />
      <GameDetailsDrawer
        open={drawerOpen}
        onOpenChange={setDrawerOpen}
        gameId={gameId}
        initialTab={initialTab}
        isNotInLibrary={false}
      />
    </div>
  );
}
