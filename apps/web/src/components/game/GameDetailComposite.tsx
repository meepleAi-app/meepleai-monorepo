/**
 * GameDetailComposite - Wrapper component combining MeepleCard + MeepleInfoCard
 *
 * Reduces boilerplate when displaying game detail pages by providing
 * a standardized layout with hero card and tabbed info panel.
 *
 * Issue #2: Brainstorm session - DRY wrapper without merging components
 */

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import { MeepleInfoCard } from '@/components/ui/data-display/meeple-info-card';

export interface GameDetailCompositeProps {
  // Game data
  gameId: string;
  gameTitle: string;
  publisher?: string;
  yearPublished?: number;
  imageUrl?: string;
  averageRating?: number;

  // Metadata for MeepleCard
  metadata?: MeepleCardMetadata[];

  // Flip data for MeepleCard back
  flipData?: {
    description?: string;
    categories?: Array<{ id: string; name: string }>;
    mechanics?: Array<{ id: string; name: string }>;
    designers?: Array<{ id: string; name: string }>;
    publishers?: Array<{ id: string; name: string }>;
    complexity?: number;
    minAge?: number;
  };

  // Authentication context
  isAuthenticated: boolean;

  // Stats for InfoMeepleCard (authenticated only)
  showStats?: boolean;
  statsData?: {
    timesPlayed: number;
    lastPlayed: string | null;
    winRate: string | null;
    avgDuration: string | null;
  };
  recentSessions?: Array<{
    id: string;
    playedAt: string;
    durationFormatted: string;
    didWin: boolean | null;
  }>;

  // InfoMeepleCard controls
  showKnowledgeBase?: boolean;
  showSocialLinks?: boolean;
}

export function GameDetailComposite({
  gameId,
  gameTitle,
  publisher,
  yearPublished,
  imageUrl,
  averageRating,
  metadata,
  flipData,
  isAuthenticated,
  showStats = false,
  statsData,
  recentSessions,
  showKnowledgeBase = true,
  showSocialLinks = true,
}: GameDetailCompositeProps) {
  const subtitle = [publisher, yearPublished].filter(Boolean).join(' • ');

  return (
    <section className="flex flex-col lg:flex-row lg:items-start gap-6">
      {/* Left: Hero card with flip */}
      <MeepleCard
        entity="game"
        variant="hero"
        title={gameTitle}
        subtitle={subtitle}
        imageUrl={imageUrl}
        rating={averageRating}
        ratingMax={10}
        metadata={metadata}
        flippable={!!flipData}
        flipData={flipData}
      />

      {/* Right: Tabbed info panel */}
      <MeepleInfoCard
        gameId={gameId}
        gameTitle={gameTitle}
        readOnly={!isAuthenticated}
        showKnowledgeBase={showKnowledgeBase}
        showSocialLinks={showSocialLinks}
        showStats={showStats && isAuthenticated}
        statsData={statsData}
        recentSessions={recentSessions}
      />
    </section>
  );
}
