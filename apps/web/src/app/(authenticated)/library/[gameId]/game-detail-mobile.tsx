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

import { useEffect, useState } from 'react';

import { ArrowLeft } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { CustomCoverDialog } from '@/components/features/library/custom-cover/CustomCoverDialog';
import { EditCoverOverlay } from '@/components/features/library/custom-cover/EditCoverOverlay';
import { FocusedGameCard } from '@/components/game-detail/mobile/FocusedGameCard';
import { GameDetailsDrawer } from '@/components/game-detail/mobile/GameDetailsDrawer';
import { type GameTabId } from '@/components/game-detail/tabs';
import { StartSessionSheet } from '@/components/session/StartSessionSheet';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';

export interface GameDetailMobileProps {
  gameId: string;
  /**
   * #4992: the tab of a `?tab=` deep link (the /agent redirect and chat notifications point at
   * `?tab=aiChat`). Opens the drawer on it once, at mount, and only below lg.
   */
  deepLinkTab?: GameTabId;
}

// Both layouts are mounted and CSS hides one, but the drawer renders in a portal: opened at
// desktop width it would cover the desktop page.
const DESKTOP_QUERY = '(min-width: 1024px)';

function isDesktopViewport(): boolean {
  return typeof window.matchMedia === 'function' && window.matchMedia(DESKTOP_QUERY).matches;
}

export default function GameDetailMobile({ gameId, deepLinkTab }: GameDetailMobileProps) {
  const router = useRouter();
  const { data: game, isLoading, error } = useLibraryGameDetail(gameId);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [initialTab, setInitialTab] = useState<GameTabId>('info');
  const [sessionSheetOpen, setSessionSheetOpen] = useState(false);
  const [coverDialogOpen, setCoverDialogOpen] = useState(false);

  const openDrawerOn = (tab: GameTabId) => {
    setInitialTab(tab);
    setDrawerOpen(true);
  };

  // Mount only: on desktop the page rewrites `?tab=` at every tab click, and that must not open
  // the mobile drawer.
  useEffect(() => {
    if (deepLinkTab && !isDesktopViewport()) {
      openDrawerOn(deepLinkTab);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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

  const hasCustomCover = Boolean(game.customCoverR2Key);

  return (
    <div className="flex h-full flex-col" data-testid="game-detail-mobile">
      <div className="group relative flex-1">
        <FocusedGameCard
          game={game}
          onOpenDrawer={() => openDrawerOn('info')}
          onOpenChat={() => openDrawerOn('aiChat')}
          onStartSession={() => setSessionSheetOpen(true)}
        />
        <EditCoverOverlay
          onEditClick={() => setCoverDialogOpen(true)}
          hasCustomCover={hasCustomCover}
        />
      </div>
      <GameDetailsDrawer
        open={drawerOpen}
        onOpenChange={setDrawerOpen}
        gameId={gameId}
        initialTab={initialTab}
        isNotInLibrary={false}
      />
      <StartSessionSheet
        open={sessionSheetOpen}
        onOpenChange={setSessionSheetOpen}
        gameId={gameId}
        gameName={game.gameTitle}
      />
      <CustomCoverDialog
        gameId={game.gameId}
        open={coverDialogOpen}
        onClose={() => setCoverDialogOpen(false)}
        hasCustomCover={hasCustomCover}
      />
    </div>
  );
}
