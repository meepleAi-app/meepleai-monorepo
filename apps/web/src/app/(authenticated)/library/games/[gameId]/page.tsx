/**
 * Library Game Detail Page — Responsive Layout
 *
 * Mobile: GameDetailMobile (vertical scroll, bottom sheets)
 * Desktop: Game Table Layout (zone-based with MeepleCard hero)
 *
 * Issue #3513 — Game Table Detail
 */

'use client';

import { ArrowLeft } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';

import {
  GameTableLayout,
  GameTableDrawer,
  GameTableZoneTools,
  GameTableZoneKnowledge,
  GameTableZoneSessions,
  GameTableSkeleton,
} from '@/components/library/game-table';
import { GameHeader } from '@/components/library/GameHeader';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { useGameTableDrawer } from '@/lib/stores/game-table-drawer-store';

import GameDetailMobile from './game-detail-mobile';

// ============================================================================
// Helpers
// ============================================================================

function buildPlayerLabel(min: number | null, max: number | null): string | null {
  if (min == null && max == null) return null;
  if (min != null && max != null) return min === max ? `${min}` : `${min}-${max}`;
  return `${min ?? max}`;
}

function buildSubtitle(publisher: string | null, year: number | null): string | undefined {
  const parts: string[] = [];
  if (publisher) parts.push(publisher);
  if (year) parts.push(`(${year})`);
  return parts.length > 0 ? parts.join(' ') : undefined;
}

function mapStateToLabel(
  currentState: string
): { text: string; variant: 'success' | 'warning' | 'error' | 'info' } | undefined {
  switch (currentState) {
    case 'Owned':
      return { text: 'Posseduto', variant: 'success' };
    case 'Nuovo':
      return { text: 'Nuovo', variant: 'info' };
    case 'InPrestito':
      return { text: 'In Prestito', variant: 'warning' };
    case 'Wishlist':
      return { text: 'Wishlist', variant: 'info' };
    default:
      return undefined;
  }
}

// ============================================================================
// Page component
// ============================================================================

export default function LibraryGameDetailPage() {
  const params = useParams();
  const router = useRouter();
  const gameId = params?.gameId as string;
  const { data: gameDetail, isLoading, error } = useLibraryGameDetail(gameId);
  const drawer = useGameTableDrawer();

  // --- Loading (shared by both layouts) ---
  if (isLoading) return <GameTableSkeleton />;

  // --- Error ---
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

  // --- Not found ---
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

  // --- Build metadata chips ---
  const metadata: MeepleCardMetadata[] = [];

  const playerLabel = buildPlayerLabel(gameDetail.minPlayers, gameDetail.maxPlayers);
  if (playerLabel) {
    metadata.push({ label: `${playerLabel} giocatori` });
  }

  if (gameDetail.playingTimeMinutes) {
    metadata.push({ label: `${gameDetail.playingTimeMinutes} min` });
  }

  if (gameDetail.complexityRating != null) {
    metadata.push({ label: `${gameDetail.complexityRating.toFixed(1)} / 5` });
  }

  // --- State badge from library state ---
  const _stateLabel = mapStateToLabel(gameDetail.currentState);

  return (
    <div className="max-w-[1100px] mx-auto px-4 sm:px-6 py-6 space-y-6">
      {/* Game header: cover + title + metadata */}
      <GameHeader
        title={gameDetail.gameTitle}
        publisher={gameDetail.gamePublisher}
        year={gameDetail.gameYearPublished}
        rating={gameDetail.averageRating}
        coverUrl={gameDetail.gameImageUrl}
        minPlayers={gameDetail.minPlayers}
        maxPlayers={gameDetail.maxPlayers}
        playingTimeMinutes={gameDetail.playingTimeMinutes}
      />

      {/* Mobile layout */}
      <div className="lg:hidden">
        <GameDetailMobile gameId={gameId} />
      </div>

      {/* Desktop layout */}
      <div className="hidden lg:block">
        <GameTableLayout
          card={
            <MeepleCard
              entity="game"
              variant="hero"
              title={gameDetail.gameTitle}
              subtitle={buildSubtitle(gameDetail.gamePublisher, gameDetail.gameYearPublished)}
              imageUrl={gameDetail.gameImageUrl || undefined}
              rating={gameDetail.averageRating ?? undefined}
              ratingMax={10}
              metadata={metadata}
              badge={_stateLabel?.text}
              data-testid="game-hero-card"
            />
          }
          toolsZone={<GameTableZoneTools gameDetail={gameDetail} gameId={gameId} />}
          knowledgeZone={<GameTableZoneKnowledge gameId={gameId} />}
          sessionsZone={<GameTableZoneSessions gameDetail={gameDetail} gameId={gameId} />}
          drawer={
            drawer.content ? (
              <GameTableDrawer content={drawer.content} onClose={drawer.close} />
            ) : undefined
          }
          drawerOpen={drawer.isOpen}
          onDrawerClose={drawer.close}
        />
      </div>
    </div>
  );
}
