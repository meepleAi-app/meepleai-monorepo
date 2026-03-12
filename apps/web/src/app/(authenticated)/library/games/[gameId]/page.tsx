/**
 * Library Game Detail Page
 *
 * Integrated into LayoutShell's 3-tier navigation system:
 * - MiniNav tabs: Overview · Agent · KB · Sessions
 * - ActionBar actions: Chat Agent · Upload PDF · Favorite · Notes · Remove
 * - Compact hero card + tabbed content
 */

'use client';

import { useState, useEffect } from 'react';

import { ArrowLeft } from 'lucide-react';
import { useParams, useRouter, useSearchParams } from 'next/navigation';

import { EditNotesModal } from '@/components/library/EditNotesModal';
import {
  GameDetailHeroCard,
  GameDetailOverviewTab,
  GameDetailAgentTab,
  GameDetailKbTab,
  GameDetailSessionsTab,
} from '@/components/library/game-detail';
import { RemoveGameDialog } from '@/components/library/RemoveGameDialog';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';

import { GameDetailNavConfig } from './GameDetailNavConfig';
import LibraryGameDetailLoading from './loading';

export default function LibraryGameDetailPage() {
  const params = useParams();
  const router = useRouter();
  const searchParams = useSearchParams();
  const gameId = params?.gameId as string;
  const tab = searchParams.get('tab');

  const { data: gameDetail, isLoading, error } = useLibraryGameDetail(gameId);

  // Event-driven modal state (from ActionBar CustomEvents)
  const [isNotesModalOpen, setIsNotesModalOpen] = useState(false);
  const [isRemoveDialogOpen, setIsRemoveDialogOpen] = useState(false);

  useEffect(() => {
    const handleEditNotes = () => setIsNotesModalOpen(true);
    const handleRemoveGame = () => setIsRemoveDialogOpen(true);

    document.addEventListener('game-detail:edit-notes', handleEditNotes);
    document.addEventListener('game-detail:remove-game', handleRemoveGame);
    return () => {
      document.removeEventListener('game-detail:edit-notes', handleEditNotes);
      document.removeEventListener('game-detail:remove-game', handleRemoveGame);
    };
  }, []);

  // Error state
  if (error) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-8">
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

  // Game not found
  if (!isLoading && !gameDetail) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-8">
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

  // Loading state
  if (isLoading || !gameDetail) {
    return <LibraryGameDetailLoading />;
  }

  return (
    <>
      <GameDetailNavConfig gameId={gameId} isFavorite={gameDetail.isFavorite} />

      <div style={{ viewTransitionName: `meeple-card-${gameId}` }}>
        <GameDetailHeroCard gameDetail={gameDetail} />
      </div>

      <div className="mx-auto max-w-6xl px-4 py-4">
        {tab === 'agent' ? (
          <GameDetailAgentTab gameId={gameId} gameTitle={gameDetail.gameTitle} />
        ) : tab === 'kb' ? (
          <GameDetailKbTab
            gameId={gameId}
            gameTitle={gameDetail.gameTitle}
            bggId={gameDetail.bggId}
          />
        ) : tab === 'sessions' ? (
          <GameDetailSessionsTab gameId={gameId} />
        ) : (
          <GameDetailOverviewTab gameDetail={gameDetail} />
        )}
      </div>

      {/* Event-driven modals from ActionBar */}
      <EditNotesModal
        isOpen={isNotesModalOpen}
        onClose={() => setIsNotesModalOpen(false)}
        gameId={gameDetail.gameId}
        gameTitle={gameDetail.gameTitle}
        currentNotes={gameDetail.notes}
      />

      <RemoveGameDialog
        isOpen={isRemoveDialogOpen}
        onClose={() => setIsRemoveDialogOpen(false)}
        gameId={gameDetail.gameId}
        gameTitle={gameDetail.gameTitle}
        onRemoved={() => router.push('/library')}
      />
    </>
  );
}
