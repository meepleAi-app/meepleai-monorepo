/**
 * GameBackContent - Game Card Back Side
 *
 * Renders game-specific back content for FlipCard:
 * - Entity-colored header with title, last-played, win rate
 * - InfoChip row (players, time, complexity dots, rating)
 * - ContextualAction list (session, AI, game night, expansions)
 * - Compact footer (favorite, noteCount, detail link)
 */

'use client';

import React, { useId } from 'react';

import { Bot, Calendar, Clock, Heart, Link2, Play, StickyNote, Star, Users } from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface GameBackData {
  /** Complexity rating (1-5 scale, e.g. BGG weight) */
  complexityRating?: number | null;
  /** Playing time in minutes */
  playingTimeMinutes?: number | null;
  /** Min players */
  minPlayers?: number | null;
  /** Max players */
  maxPlayers?: number | null;
  /** Average rating (0-10) */
  averageRating?: number | null;
  /** Times played by user */
  timesPlayed?: number;
  /** KB document previews */
  kbDocuments?: Array<{ id: string; fileName: string; status: string }>;
  /** Whether game has KB */
  hasKb?: boolean;
  /** Number of KB cards */
  kbCardCount?: number;
  /** Pre-formatted "last played" label (consumer handles locale) */
  lastPlayedLabel?: string;
  /** Win rate percentage (0-100) */
  winRate?: number;
  /** ISO date string of next game night */
  nextGameNight?: string;
  /** Number of entity links (expansions, etc.) */
  entityLinkCount?: number;
  /** Number of user notes */
  noteCount?: number;
}

export interface GameBackActions {
  onChatAgent?: () => void;
  onToggleFavorite?: () => void;
  isFavorite?: boolean;
  onNewSession?: () => void;
  onAddToGameNight?: () => void;
  onViewLinks?: () => void;
}

export interface GameBackContentProps {
  data: GameBackData;
  actions?: GameBackActions;
  /** Entity color HSL string (default game-orange) */
  entityColor?: string;
  /** Game title */
  title?: string;
  /** Link to game detail page */
  detailHref?: string;
  className?: string;
}

// ============================================================================
// Sub-components
// ============================================================================

function InfoChip({ children }: { children: React.ReactNode }) {
  return (
    <span className="inline-flex items-center gap-1 bg-muted/40 px-2 py-0.5 rounded-md text-xs text-card-foreground">
      {children}
    </span>
  );
}

function ContextualAction({
  icon: Icon,
  label,
  context,
  colorHsl,
  onClick,
}: {
  icon: typeof Play;
  label: string;
  context?: string | null;
  colorHsl: string;
  onClick?: () => void;
}) {
  const id = useId();
  const contextId = context ? `${id}-ctx` : undefined;

  return (
    <button
      type="button"
      className={cn(
        'flex items-center gap-2 w-full rounded-lg px-3 py-2 text-xs font-medium transition-colors',
        !onClick && 'opacity-50 cursor-not-allowed'
      )}
      style={{
        backgroundColor: `hsla(${colorHsl}, 0.08)`,
        borderColor: `hsla(${colorHsl}, 0.15)`,
        color: `hsl(${colorHsl})`,
      }}
      disabled={!onClick}
      aria-describedby={contextId}
      onClick={e => {
        e.stopPropagation();
        onClick?.();
      }}
    >
      <Icon className="w-4 h-4 shrink-0" />
      <span>{label}</span>
      {context && (
        <span id={contextId} className="ml-auto text-[10px] text-muted-foreground">
          {context}
        </span>
      )}
    </button>
  );
}

/** Validate that href is a safe relative path */
const isSafeHref = (href: string) => href.startsWith('/') && !href.startsWith('//');

// ============================================================================
// Main Component
// ============================================================================

export const GameBackContent = React.memo(function GameBackContent({
  data,
  actions,
  entityColor = '25 95% 45%',
  title,
  detailHref,
  className,
}: GameBackContentProps) {
  const {
    complexityRating,
    playingTimeMinutes,
    minPlayers,
    maxPlayers,
    averageRating,
    timesPlayed = 0,
    hasKb,
    kbCardCount = 0,
    lastPlayedLabel = 'Mai giocato',
    winRate,
    entityLinkCount = 0,
    noteCount = 0,
  } = data;

  // Computed values
  const playerRange =
    minPlayers && maxPlayers
      ? minPlayers === maxPlayers
        ? `${minPlayers}`
        : `${minPlayers}-${maxPlayers}`
      : '—';

  const complexityDots = complexityRating != null ? Math.round(complexityRating) : null;

  const kbContextLabel = hasKb
    ? `KB pronta · ${kbCardCount} doc`
    : data.kbDocuments?.some(d => d.status !== 'Ready')
      ? 'KB in elaborazione'
      : 'Nessuna KB';

  const timesPlayedContext = timesPlayed > 0 ? `${timesPlayed} partite giocate` : 'Nessuna partita';

  return (
    <div
      className={cn('flex h-full flex-col overflow-hidden', className)}
      data-testid="game-back-content"
    >
      {/* Entity-colored header */}
      <div
        className="relative overflow-hidden px-5 pb-3 pt-5"
        style={{ backgroundColor: `hsl(${entityColor})` }}
      >
        <div
          className="pointer-events-none absolute inset-0 opacity-[0.12]"
          style={{
            backgroundImage:
              'repeating-linear-gradient(45deg, transparent, transparent 10px, currentColor 10px, currentColor 11px)',
          }}
          aria-hidden="true"
        />
        <h2 className="relative z-[1] font-quicksand text-lg font-bold text-white line-clamp-1">
          {title || 'Statistiche'}
        </h2>
        <p className="relative z-[1] text-xs text-white/80 mt-0.5">
          {lastPlayedLabel}
          {winRate != null && ` · Win rate ${winRate}%`}
        </p>
      </div>

      {/* Content area */}
      <div className="flex flex-1 flex-col overflow-y-auto px-4 py-3 gap-3">
        {/* InfoChip row */}
        <div className="flex gap-1.5 flex-wrap" data-testid="info-chips">
          <InfoChip>
            <Users className="w-3 h-3" />
            {playerRange}
          </InfoChip>
          {playingTimeMinutes != null && (
            <InfoChip>
              <Clock className="w-3 h-3" />
              {playingTimeMinutes}m
            </InfoChip>
          )}
          {complexityDots != null && (
            <InfoChip>
              <span
                className="inline-flex gap-0.5"
                data-testid="complexity-dots"
                aria-label={`Complessità: ${complexityDots} su 5`}
              >
                {Array.from({ length: 5 }, (_, i) => (
                  <span
                    key={i}
                    aria-hidden="true"
                    className={cn(
                      'rounded-full w-2 h-2',
                      i < complexityDots ? 'bg-current' : 'bg-current/20'
                    )}
                  />
                ))}
              </span>
            </InfoChip>
          )}
          {averageRating != null && (
            <InfoChip>
              <Star className="w-3 h-3" />
              {averageRating.toFixed(1)}
            </InfoChip>
          )}
        </div>

        {/* Contextual actions */}
        {actions && (
          <div className="flex flex-col gap-0.5" data-testid="game-back-actions">
            {actions.onNewSession && (
              <ContextualAction
                icon={Play}
                label="Nuova Sessione"
                context={timesPlayedContext}
                colorHsl="25 95% 45%"
                onClick={actions.onNewSession}
              />
            )}
            {actions.onChatAgent && (
              <ContextualAction
                icon={Bot}
                label="Chiedi all'AI"
                context={kbContextLabel}
                colorHsl="262 83% 58%"
                onClick={actions.onChatAgent}
              />
            )}
            {actions.onAddToGameNight && (
              <ContextualAction
                icon={Calendar}
                label="Aggiungi a serata"
                context={data.nextGameNight ?? undefined}
                colorHsl="217 91% 60%"
                onClick={actions.onAddToGameNight}
              />
            )}
            {entityLinkCount > 0 && actions.onViewLinks && (
              <ContextualAction
                icon={Link2}
                label="Espansioni"
                context={`${entityLinkCount} collegate`}
                colorHsl="142 70% 45%"
                onClick={actions.onViewLinks}
              />
            )}
          </div>
        )}

        {/* Compact footer */}
        <div className="mt-auto border-t border-border/10 pt-2 flex items-center justify-between text-xs">
          <div className="flex items-center gap-2">
            {actions?.onToggleFavorite && (
              <button
                type="button"
                className="inline-flex items-center gap-1 text-muted-foreground hover:text-card-foreground transition-colors"
                onClick={e => {
                  e.stopPropagation();
                  actions.onToggleFavorite?.();
                }}
                data-testid="favorite-toggle"
              >
                <Heart
                  className={cn('w-3 h-3', actions.isFavorite && 'fill-current')}
                  style={actions.isFavorite ? { color: `hsl(${entityColor})` } : undefined}
                  data-filled={actions.isFavorite ? 'true' : 'false'}
                />
                <span>Pref</span>
              </button>
            )}
            {noteCount > 0 && (
              <span
                className="inline-flex items-center gap-0.5 text-muted-foreground"
                data-testid="note-count-indicator"
              >
                <StickyNote className="w-3 h-3" />
                <span>{noteCount}</span>
              </span>
            )}
          </div>
          {detailHref && isSafeHref(detailHref) && (
            <Link
              href={detailHref}
              className="text-xs font-medium transition-colors font-nunito"
              style={{ color: `hsl(${entityColor})` }}
              onClick={e => e.stopPropagation()}
              data-testid="game-detail-link"
            >
              Dettaglio →
            </Link>
          )}
        </div>
      </div>
    </div>
  );
});
