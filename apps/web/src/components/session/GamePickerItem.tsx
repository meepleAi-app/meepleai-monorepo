'use client';

/**
 * GamePickerItem — Single game row in the GamePickerDialog.
 *
 * Shows game info via MeepleCard (list variant) and lazy-loads KB readiness
 * via React Query. Disabled games show a tooltip with current KB state.
 *
 * Plan 2 Task 6 — Session Flow v2.1
 */

import { AlertTriangle, BookOpen, CheckCircle2, Loader2 } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { useKbReadinessQuery } from '@/hooks/queries/useSessionFlow';
import { cn } from '@/lib/utils';

// ─── Types ──────────────────────────────────────────────────────────────────

export interface GamePickerGame {
  id: string;
  gameId: string;
  gameTitle: string;
  gameImageUrl?: string | null;
  gamePublisher?: string | null;
  averageRating?: number | null;
  minPlayers?: number | null;
  maxPlayers?: number | null;
}

interface GamePickerItemProps {
  game: GamePickerGame;
  onSelect: (gameId: string) => void;
  isSelecting: boolean;
}

// ─── KB Status Label ────────────────────────────────────────────────────────

const KB_STATE_LABELS: Record<string, string> = {
  NoPdfs: 'Nessun PDF caricato',
  Uploading: 'Upload in corso...',
  Extracting: 'Estrazione in corso...',
  Indexing: 'Indicizzazione in corso...',
  Ready: 'Pronto',
  Failed: 'Errore elaborazione',
  PartiallyReady: 'Parzialmente pronto',
};

function kbStateLabel(state: string): string {
  return KB_STATE_LABELS[state] ?? `Stato: ${state}`;
}

// ─── Component ──────────────────────────────────────────────────────────────

export function GamePickerItem({ game, onSelect, isSelecting }: GamePickerItemProps) {
  const { data: kbReadiness, isLoading: isKbLoading } = useKbReadinessQuery(game.gameId);

  const isReady = kbReadiness?.isReady ?? false;
  const hasWarnings = (kbReadiness?.warnings?.length ?? 0) > 0;
  const isDisabled = !isReady || isSelecting;

  // Build subtitle with player count if available
  const parts: string[] = [];
  if (game.gamePublisher) parts.push(game.gamePublisher);
  if (game.minPlayers && game.maxPlayers) {
    parts.push(`${game.minPlayers}–${game.maxPlayers} giocatori`);
  }
  const subtitle = parts.join(' · ') || undefined;

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <button
          type="button"
          onClick={() => !isDisabled && onSelect(game.gameId)}
          disabled={isDisabled && !isKbLoading}
          className={cn(
            'w-full text-left rounded-xl transition-all duration-200',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50',
            isDisabled && !isKbLoading && 'opacity-50 cursor-not-allowed',
            !isDisabled && 'hover:bg-accent/50 cursor-pointer'
          )}
          data-testid={`game-picker-item-${game.gameId}`}
        >
          <div className="flex items-center gap-3 p-2">
            <div className="flex-1 min-w-0">
              <MeepleCard
                entity="game"
                variant="compact"
                title={game.gameTitle}
                subtitle={subtitle}
                imageUrl={game.gameImageUrl ?? undefined}
                rating={game.averageRating ?? undefined}
                ratingMax={10}
              />
            </div>

            {/* KB Status Indicator */}
            <div className="shrink-0 flex items-center gap-1.5 pr-1">
              {isKbLoading ? (
                <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
              ) : isReady && hasWarnings ? (
                <AlertTriangle className="h-4 w-4 text-amber-500" />
              ) : isReady ? (
                <CheckCircle2 className="h-4 w-4 text-emerald-500" />
              ) : (
                <BookOpen className="h-4 w-4 text-muted-foreground" />
              )}
            </div>
          </div>
        </button>
      </TooltipTrigger>

      {/* Tooltip: show KB state for disabled games or warnings */}
      {!isKbLoading && (!isReady || hasWarnings) && (
        <TooltipContent side="left">
          <div className="space-y-1 max-w-[220px]">
            <p className="font-medium">{isReady ? 'Avvisi KB' : 'KB non pronto'}</p>
            <p className="text-xs opacity-90">
              {kbReadiness ? kbStateLabel(kbReadiness.state) : 'Stato sconosciuto'}
            </p>
            {hasWarnings &&
              kbReadiness?.warnings.map((w, i) => (
                <p key={i} className="text-xs opacity-80">
                  {w}
                </p>
              ))}
          </div>
        </TooltipContent>
      )}
    </Tooltip>
  );
}
