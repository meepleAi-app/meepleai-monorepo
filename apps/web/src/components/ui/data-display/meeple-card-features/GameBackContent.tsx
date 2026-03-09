/**
 * GameBackContent - Game Card Back Side
 *
 * Renders game-specific back content for FlipCard:
 * - Entity-colored header with game title
 * - Stats grid (Peso, Durata, Giocatori, Voto, Partite)
 * - KB document preview (up to 3 docs with status)
 * - Quick action buttons (Chat, KB, Note, Preferito)
 * - Detail page link
 */

'use client';

import React from 'react';

import {
  BookOpen,
  Clock,
  ExternalLink,
  FileText,
  Heart,
  MessageCircle,
  StickyNote,
  Users,
  Weight,
  Star,
  Gamepad2,
} from 'lucide-react';
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
}

export interface GameBackActions {
  onChatAgent?: () => void;
  onViewKb?: () => void;
  onEditNotes?: () => void;
  onToggleFavorite?: () => void;
  isFavorite?: boolean;
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

function StatChip({
  icon: Icon,
  label,
  value,
  entityColor,
}: {
  icon: typeof Clock;
  label: string;
  value: string | number;
  entityColor: string;
}) {
  return (
    <div
      className="flex flex-col items-center gap-0.5 rounded-lg px-2 py-1.5 bg-muted/40"
      data-testid={`game-stat-${label.toLowerCase().replace(/\s/g, '-')}`}
    >
      <Icon className="w-3.5 h-3.5" style={{ color: `hsl(${entityColor})` }} />
      <span className="text-xs font-bold tabular-nums text-card-foreground">{value}</span>
      <span className="text-[9px] text-muted-foreground leading-tight">{label}</span>
    </div>
  );
}

function ActionButton({
  icon: Icon,
  label,
  onClick,
  active,
  entityColor,
}: {
  icon: typeof MessageCircle;
  label: string;
  onClick?: () => void;
  active?: boolean;
  entityColor: string;
}) {
  return (
    <button
      type="button"
      className={cn(
        'flex flex-col items-center gap-1 rounded-lg px-2 py-2 text-xs font-medium',
        'transition-all duration-200',
        'hover:bg-muted/60',
        active ? 'bg-muted/50 text-card-foreground' : 'text-muted-foreground'
      )}
      onClick={e => {
        e.stopPropagation();
        onClick?.();
      }}
      title={label}
    >
      <Icon className="w-4 h-4" style={active ? { color: `hsl(${entityColor})` } : undefined} />
      <span className="leading-tight">{label}</span>
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
    kbDocuments,
    hasKb,
    kbCardCount = 0,
  } = data;

  const playerRange =
    minPlayers && maxPlayers
      ? minPlayers === maxPlayers
        ? `${minPlayers}`
        : `${minPlayers}-${maxPlayers}`
      : '—';

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
      </div>

      {/* Content area */}
      <div className="flex flex-1 flex-col overflow-y-auto px-4 py-3 gap-3">
        {/* Stats grid */}
        <div className="grid grid-cols-3 gap-1.5" data-testid="game-stats-grid">
          {complexityRating != null && (
            <StatChip
              icon={Weight}
              label="Peso"
              value={complexityRating.toFixed(1)}
              entityColor={entityColor}
            />
          )}
          {playingTimeMinutes != null && (
            <StatChip
              icon={Clock}
              label="Durata"
              value={`${playingTimeMinutes}m`}
              entityColor={entityColor}
            />
          )}
          <StatChip icon={Users} label="Giocatori" value={playerRange} entityColor={entityColor} />
          {averageRating != null && (
            <StatChip
              icon={Star}
              label="Voto"
              value={averageRating.toFixed(1)}
              entityColor={entityColor}
            />
          )}
          <StatChip icon={Gamepad2} label="Partite" value={timesPlayed} entityColor={entityColor} />
        </div>

        {/* KB preview */}
        {(hasKb || (kbDocuments && kbDocuments.length > 0)) && (
          <div data-testid="kb-preview-section">
            <h3 className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              <BookOpen className="w-3 h-3" />
              Knowledge Base
              {kbCardCount > 0 && (
                <span className="ml-auto text-[10px] font-normal">{kbCardCount} doc</span>
              )}
            </h3>
            <div className="space-y-1">
              {kbDocuments &&
                kbDocuments.slice(0, 3).map(doc => {
                  const isReady = doc.status === 'Ready' || doc.status === 'completed';
                  return (
                    <div key={doc.id} className="flex items-center gap-2 text-xs">
                      <span
                        className={cn(
                          'w-1.5 h-1.5 rounded-full flex-shrink-0',
                          isReady ? 'bg-green-500' : 'bg-amber-500 animate-pulse'
                        )}
                      />
                      <FileText className="w-3 h-3 text-muted-foreground flex-shrink-0" />
                      <span className="text-card-foreground truncate flex-1">{doc.fileName}</span>
                    </div>
                  );
                })}
              {kbDocuments && kbDocuments.length > 3 && (
                <p className="text-[10px] text-muted-foreground ml-5">
                  +{kbDocuments.length - 3} altri documenti
                </p>
              )}
            </div>
          </div>
        )}

        {/* Quick actions */}
        {actions && (
          <div className="grid grid-cols-4 gap-1" data-testid="game-back-actions">
            {actions.onChatAgent && (
              <ActionButton
                icon={MessageCircle}
                label="Chat"
                onClick={actions.onChatAgent}
                active={hasKb}
                entityColor={entityColor}
              />
            )}
            {actions.onViewKb && (
              <ActionButton
                icon={BookOpen}
                label="KB"
                onClick={actions.onViewKb}
                active={hasKb}
                entityColor={entityColor}
              />
            )}
            {actions.onEditNotes && (
              <ActionButton
                icon={StickyNote}
                label="Note"
                onClick={actions.onEditNotes}
                entityColor={entityColor}
              />
            )}
            {actions.onToggleFavorite && (
              <ActionButton
                icon={Heart}
                label={actions.isFavorite ? 'Preferito' : 'Preferisci'}
                onClick={actions.onToggleFavorite}
                active={actions.isFavorite}
                entityColor={entityColor}
              />
            )}
          </div>
        )}

        {/* Detail link */}
        {detailHref && isSafeHref(detailHref) && (
          <div className="mt-auto pt-2">
            <Link
              href={detailHref}
              className="inline-flex items-center gap-1.5 text-sm font-medium transition-colors font-nunito"
              style={{ color: `hsl(${entityColor})` }}
              onClick={e => e.stopPropagation()}
              data-testid="game-detail-link"
            >
              <ExternalLink className="h-3.5 w-3.5" />
              Vai alla pagina gioco
            </Link>
          </div>
        )}
      </div>
    </div>
  );
});
