'use client';

/**
 * BggGameCard - BGG search result card with "Add to Library" action
 *
 * Wraps MeepleCard for displaying a BoardGameGeek search result
 * with an import action button.
 */

import { Plus, Loader2, Check } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Button } from '@/components/ui/primitives/button';
import type { BggGameSummary } from '@/lib/api/clients/gameNightBggClient';

// ============================================================================
// Types
// ============================================================================

export interface BggGameCardProps {
  /** BGG game summary from search results */
  game: BggGameSummary;
  /** Whether the import is currently in progress */
  isImporting?: boolean;
  /** Whether this game was already imported */
  isImported?: boolean;
  /** Handler for the "Add to Library" action */
  onImport?: (bggId: number) => void;
}

// ============================================================================
// Component
// ============================================================================

export function BggGameCard({ game, isImporting, isImported, onImport }: BggGameCardProps) {
  const subtitle = game.yearPublished ? `(${game.yearPublished})` : undefined;

  return (
    <div className="relative" data-testid={`bgg-game-card-${game.bggId}`}>
      <MeepleCard
        entity="game"
        variant="grid"
        title={game.title}
        subtitle={subtitle}
        imageUrl={game.thumbnailUrl ?? undefined}
      />
      <div className="mt-2">
        <Button
          size="sm"
          variant={isImported ? 'outline' : 'default'}
          className="w-full font-nunito"
          disabled={isImporting || isImported}
          onClick={() => onImport?.(game.bggId)}
          data-testid={`import-btn-${game.bggId}`}
        >
          {isImporting ? (
            <>
              <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
              Importing...
            </>
          ) : isImported ? (
            <>
              <Check className="mr-1.5 h-4 w-4" />
              Added
            </>
          ) : (
            <>
              <Plus className="mr-1.5 h-4 w-4" />
              Add to Library
            </>
          )}
        </Button>
      </div>
    </div>
  );
}
