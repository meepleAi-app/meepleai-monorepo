/**
 * PrivateGameDetailClient — Client component for the /private-games/[id] page.
 *
 * Issue #3664: Private game PDF support — detail page with game info, PDF
 * upload/status, and RAG chat button.
 *
 * Renders:
 * 1. Game header (title, description, image)
 * 2. PrivateGamePdfSection (upload form → processing status → chat button)
 */

'use client';

import Image from 'next/image';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { usePrivateGame } from '@/hooks/queries/useLibrary';

import { PrivateGamePdfSection } from './PrivateGamePdfSection';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface PrivateGameDetailClientProps {
  privateGameId: string;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function PrivateGameDetailClient({ privateGameId }: PrivateGameDetailClientProps) {
  const { data: game, isLoading, isError } = usePrivateGame(privateGameId);

  if (isLoading) {
    return (
      <div className="max-w-2xl mx-auto px-4 py-6 space-y-6" data-testid="private-game-loading">
        <div className="flex gap-4">
          <Skeleton className="h-20 w-20 rounded-lg shrink-0" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-6 w-3/4" />
            <Skeleton className="h-4 w-1/3" />
            <Skeleton className="h-4 w-1/2" />
          </div>
        </div>
        <Skeleton className="h-48 w-full rounded-xl" />
      </div>
    );
  }

  if (isError || !game) {
    return (
      <div
        className="max-w-2xl mx-auto px-4 py-6"
        data-testid="private-game-not-found"
        role="alert"
      >
        <p className="text-muted-foreground">Gioco non trovato.</p>
      </div>
    );
  }

  // Determine if this game already has a PDF indexed (agent created signals completed indexing)
  const hasPdf = !!game.agentDefinitionId;

  return (
    <div className="max-w-2xl mx-auto px-4 py-6 space-y-6" data-testid="private-game-detail">
      {/* Game Header */}
      <div className="flex gap-4 items-start">
        {game.thumbnailUrl && (
          <Image
            src={game.thumbnailUrl}
            alt={game.title}
            width={80}
            height={80}
            className="rounded-lg object-cover shrink-0"
          />
        )}
        <div className="min-w-0">
          <h1 className="text-2xl font-bold truncate">{game.title}</h1>
          {game.yearPublished && (
            <p className="text-sm text-muted-foreground">{game.yearPublished}</p>
          )}
          {game.minPlayers != null && game.maxPlayers != null && (
            <p className="text-sm text-muted-foreground">
              {game.minPlayers}&ndash;{game.maxPlayers} giocatori
            </p>
          )}
          {game.description && (
            <p className="mt-2 text-sm text-muted-foreground line-clamp-3">{game.description}</p>
          )}
        </div>
      </div>

      {/* PDF / KB / Chat section */}
      <PrivateGamePdfSection privateGameId={privateGameId} hasPdf={hasPdf} />
    </div>
  );
}
