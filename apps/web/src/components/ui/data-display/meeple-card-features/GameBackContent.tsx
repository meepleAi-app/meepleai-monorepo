/**
 * GameBackContent - Game Card Back Side (Issue #336 Redesign)
 *
 * 6-section layout:
 * ① Enriched header (title + subtitle on entity gradient)
 * ② Stats row (partite, vittorie, tempo, last played)
 * ③ KB summary (doc count + aggregate status)
 * ④ Tag pills (categories + mechanics, max 6 + overflow)
 * ⑤ Navigation links (sessions, notes, links with counters)
 * ⑥ Compact footer (favorite toggle, meta pills, detail link)
 *
 * InfoChip row (players, time, complexity, rating) retained between ② and ③.
 * All new sections auto-hide when data is absent.
 */

'use client';

import React from 'react';

import { ChevronRight, Clock, Heart, Link2, Notebook, Play, Star, Users } from 'lucide-react';
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
  timesPlayed?: number | null;
  /** KB document previews */
  kbDocuments?: Array<{ id: string; fileName: string; status: string }>;
  /** Whether game has KB */
  hasKb?: boolean;
  /** Number of KB cards */
  kbCardCount?: number;
  /** Pre-formatted "last played" label (consumer handles locale) */
  lastPlayedLabel?: string;
  /** Win rate percentage (0-100) */
  winRate?: number | null;
  /** Number of entity links (expansions, etc.) */
  entityLinkCount?: number;
  /** Number of user notes */
  noteCount?: number;

  // --- New fields (Issue #336) ---
  /** Total play time in minutes (aggregate across sessions) */
  totalPlayTimeMinutes?: number | null;
  /** Category tags (e.g. "Strategia", "Famiglia") */
  categories?: string[];
  /** Mechanic tags (e.g. "Worker Placement", "Deck Building") */
  mechanics?: string[];
  /** BGG weight (1-5 scale) */
  bggWeight?: number | null;
  /** Best player count */
  bestPlayerCount?: number | null;
  /** Number of sessions played */
  sessionCount?: number;
}

export interface GameBackActions {
  onChatAgent?: () => void;
  onToggleFavorite?: () => void;
  isFavorite?: boolean;
  // --- Navigation (Issue #336) ---
  onViewSessions?: () => void;
  onViewNotes?: () => void;
  onViewLinks?: () => void;
}

export interface GameBackContentProps {
  data: GameBackData;
  actions?: GameBackActions;
  /** Entity color HSL string (default game-orange) */
  entityColor?: string;
  /** Game title */
  title?: string;
  /** Publisher or subtitle, shown under title in header */
  subtitle?: string;
  /** Link to game detail page */
  detailHref?: string;
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const MAX_VISIBLE_TAGS = 6;

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

function StatItem({ value, label }: { value: string; label: string }) {
  return (
    <span className="inline-flex items-baseline gap-1">
      <strong className="text-sm font-bold text-card-foreground">{value}</strong>
      <span className="text-xs text-muted-foreground">{label}</span>
    </span>
  );
}

function StatsRow({
  timesPlayed,
  winRate,
  totalPlayTimeMinutes,
  lastPlayedLabel,
}: Pick<GameBackData, 'timesPlayed' | 'winRate' | 'totalPlayTimeMinutes' | 'lastPlayedLabel'>) {
  const items: Array<{ value: string; label: string }> = [];

  if (timesPlayed != null) {
    items.push({ value: String(timesPlayed), label: 'Partite' });
  }
  if (winRate != null) {
    items.push({ value: `${winRate}%`, label: 'Vittorie' });
  }
  if (totalPlayTimeMinutes != null) {
    const formatted =
      totalPlayTimeMinutes < 60
        ? `${totalPlayTimeMinutes}m`
        : `${Math.round(totalPlayTimeMinutes / 60)}h`;
    items.push({ value: formatted, label: 'Tempo' });
  }

  if (items.length === 0 && !lastPlayedLabel) return null;

  return (
    <div className="flex items-center justify-center gap-3 py-1.5" data-testid="stats-row">
      {items.map((item, i) => (
        <React.Fragment key={item.label}>
          {i > 0 && (
            <span className="text-muted-foreground/50 text-xs" data-separator aria-hidden="true">
              |
            </span>
          )}
          <StatItem value={item.value} label={item.label} />
        </React.Fragment>
      ))}
      {lastPlayedLabel && (
        <>
          {items.length > 0 && (
            <span className="text-muted-foreground/50 text-xs" data-separator aria-hidden="true">
              |
            </span>
          )}
          <span className="text-xs text-muted-foreground italic">{lastPlayedLabel}</span>
        </>
      )}
    </div>
  );
}

function KbSummary({ hasKb, kbDocuments }: Pick<GameBackData, 'hasKb' | 'kbDocuments'>) {
  if (!hasKb || !kbDocuments?.length) return null;

  const allReady = kbDocuments.every(d => d.status === 'Ready');
  const docCount = kbDocuments.length;

  return (
    <div
      className="flex items-center justify-between bg-muted/30 rounded-lg px-3 py-2"
      data-testid="kb-summary"
    >
      <span className="text-xs text-card-foreground">
        {docCount} {docCount === 1 ? 'documento' : 'documenti'} KB
      </span>
      <span
        className={cn(
          'text-[10px] font-semibold px-1.5 py-0.5 rounded',
          allReady ? 'bg-emerald-500/10 text-emerald-600' : 'bg-amber-500/10 text-amber-600'
        )}
        data-testid="kb-status-badge"
      >
        {allReady ? 'Pronta' : 'In elaborazione'}
      </span>
    </div>
  );
}

function TagPills({ categories, mechanics }: Pick<GameBackData, 'categories' | 'mechanics'>) {
  const allTags = [...(categories ?? []), ...(mechanics ?? [])];
  if (allTags.length === 0) return null;

  const visible = allTags.slice(0, MAX_VISIBLE_TAGS);
  const overflowCount = allTags.length - MAX_VISIBLE_TAGS;

  return (
    <div className="flex flex-wrap gap-1.5" data-testid="tag-pills">
      {visible.map(tag => (
        <span
          key={tag}
          className="bg-muted text-muted-foreground text-[10px] px-2 py-0.5 rounded-full"
        >
          {tag}
        </span>
      ))}
      {overflowCount > 0 && (
        <span
          className="bg-muted text-muted-foreground text-[10px] px-2 py-0.5 rounded-full font-semibold"
          aria-label={`${overflowCount} ${overflowCount === 1 ? 'altro tag' : 'altri tag'}`}
        >
          +{overflowCount}
        </span>
      )}
    </div>
  );
}

function NavLinks({
  actions,
  sessionCount,
  noteCount,
  entityLinkCount,
}: {
  actions?: GameBackActions;
  sessionCount?: number;
  noteCount?: number;
  entityLinkCount?: number;
}) {
  const links = [
    { icon: Play, label: 'Sessioni', count: sessionCount, onClick: actions?.onViewSessions },
    { icon: Notebook, label: 'Note', count: noteCount, onClick: actions?.onViewNotes },
    { icon: Link2, label: 'Collegamenti', count: entityLinkCount, onClick: actions?.onViewLinks },
  ].filter(l => l.onClick && (l.count ?? 0) > 0);

  if (links.length === 0) return null;

  return (
    <div className="flex flex-col gap-0.5" data-testid="nav-links">
      {links.map(({ icon: Icon, label, count, onClick }) => (
        <button
          key={label}
          type="button"
          className="flex items-center gap-2 w-full rounded-lg px-2 py-1.5 text-xs font-medium transition-colors hover:bg-muted/50"
          onClick={e => {
            e.stopPropagation();
            onClick?.();
          }}
        >
          <Icon className="w-3.5 h-3.5 text-muted-foreground" />
          <span className="text-card-foreground">{label}</span>
          <span className="ml-auto text-muted-foreground">{count}</span>
          <ChevronRight className="w-3 h-3 text-muted-foreground/50" />
        </button>
      ))}
    </div>
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
  subtitle,
  detailHref,
  className,
}: GameBackContentProps) {
  const {
    complexityRating,
    playingTimeMinutes,
    minPlayers,
    maxPlayers,
    averageRating,
    timesPlayed,
    hasKb,
    lastPlayedLabel,
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

  return (
    <div
      className={cn('flex h-full flex-col overflow-hidden', className)}
      data-testid="game-back-content"
    >
      {/* ① Enriched header (Issue #336) */}
      <div
        className="relative overflow-hidden px-5 pb-3 pt-5"
        style={{
          background: `linear-gradient(135deg, hsl(${entityColor}), hsl(${entityColor} / 0.85))`,
        }}
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
        {subtitle && (
          <p className="relative z-[1] text-sm text-white/70 mt-0.5 line-clamp-1">{subtitle}</p>
        )}
      </div>

      {/* Content area */}
      <div className="flex flex-1 flex-col overflow-y-auto px-4 py-3 gap-3">
        {/* ② Stats Row (Issue #336) */}
        <StatsRow
          timesPlayed={timesPlayed}
          winRate={winRate}
          totalPlayTimeMinutes={data.totalPlayTimeMinutes}
          lastPlayedLabel={lastPlayedLabel}
        />

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

        {/* ③ KB Summary (Issue #336) */}
        <KbSummary hasKb={hasKb} kbDocuments={data.kbDocuments} />

        {/* ④ Tag Pills (Issue #336) */}
        <TagPills categories={data.categories} mechanics={data.mechanics} />

        {/* ⑤ Navigation Links (Issue #336) */}
        <NavLinks
          actions={actions}
          sessionCount={data.sessionCount}
          noteCount={noteCount}
          entityLinkCount={entityLinkCount}
        />

        {/* ⑥ Compact Footer (Issue #336) */}
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
                aria-pressed={!!actions.isFavorite}
                data-testid="favorite-toggle"
              >
                <Heart
                  className={cn('w-3 h-3', actions.isFavorite && 'fill-current')}
                  style={actions.isFavorite ? { color: `hsl(${entityColor})` } : undefined}
                  data-filled={actions.isFavorite ? 'true' : 'false'}
                />
              </button>
            )}
            {data.bggWeight != null && (
              <span className="text-muted-foreground text-[10px]">
                BGG {data.bggWeight.toFixed(1)}
              </span>
            )}
            {data.bestPlayerCount != null && (
              <span className="text-muted-foreground text-[10px]">
                Best {data.bestPlayerCount}p
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
