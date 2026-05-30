/**
 * ConnectionBar — Task 2 (Issue #1488 / Epic #1475 Phase D).
 *
 * Horizontal row of entity-tinted chips showing the key connections of
 * a play record: game, player count, chat presence, event date.
 *
 * Design: chips styled with entity CSS vars (game/player/chat/event),
 * dashed border for "empty" state (no chat), solid border when present.
 *
 * AC-2.3: chip entity-tinted (game, player count, chat presence, event date)
 * AC-2.8 EC-2: freeform (gameId=null) → no anchor, plain chip text
 *
 * @see mockup `admin-mockups/design_files/sp4-play-records-detail.jsx` ConnectionBar
 */
'use client';

import type { ReactElement } from 'react';

import Link from 'next/link';

import { entityHsl, entityHslText } from '@/components/ui/data-display/meeple-card';

export interface ConnectionBarProps {
  readonly gameId: string | null;
  readonly gameName: string;
  readonly playerCount: number;
  readonly dateLabel: string;
  readonly chatCount: number;
  readonly className?: string;
}

// ── helpers ───────────────────────────────────────────────────────────────────

function chipStyle(
  entity: 'game' | 'player' | 'chat' | 'event',
  empty = false
): React.CSSProperties {
  return {
    background: empty ? 'transparent' : entityHsl(entity, 0.1),
    border: `1px ${empty ? 'dashed' : 'solid'} ${empty ? entityHsl(entity, 0.4) : entityHsl(entity, 0.22)}`,
    color: entityHslText(entity),
    opacity: empty ? 0.65 : 1,
  };
}

const CHIP_BASE =
  'inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 font-mono text-[11px] font-bold whitespace-nowrap shrink-0 transition-opacity';

// ── component ─────────────────────────────────────────────────────────────────

export function ConnectionBar({
  gameId,
  gameName,
  playerCount,
  dateLabel,
  chatCount,
  className,
}: ConnectionBarProps): ReactElement {
  const hasChat = chatCount > 0;

  const GameChip = (): ReactElement => {
    const content = (
      <>
        <span aria-hidden="true">🎲</span>
        {gameName}
      </>
    );

    if (gameId !== null) {
      return (
        <Link
          href={`/games/${gameId}`}
          className={CHIP_BASE}
          style={chipStyle('game')}
          aria-label={`Gioco: ${gameName}`}
        >
          {content}
        </Link>
      );
    }
    // EC-2: freeform — plain span, no anchor
    return (
      <span className={CHIP_BASE} style={chipStyle('game')} aria-label={`Gioco: ${gameName}`}>
        {content}
      </span>
    );
  };

  return (
    <div
      data-slot="connection-bar"
      className={`flex items-center gap-1.5 overflow-x-auto bg-card border-b border-border px-4 py-2 sm:px-8 ${className ?? ''}`}
      role="region"
      aria-label="Collegamenti"
    >
      <span className="font-mono text-[9px] font-extrabold text-muted-foreground uppercase tracking-widest shrink-0 mr-1">
        Collegamenti
      </span>

      <GameChip />

      {/* Player count */}
      <span
        className={CHIP_BASE}
        style={chipStyle('player')}
        aria-label={`${playerCount} giocatori`}
      >
        <span aria-hidden="true">👥</span>
        {playerCount} giocatori
      </span>

      {/* Chat presence */}
      <span
        className={CHIP_BASE}
        style={chipStyle('chat', !hasChat)}
        aria-label={hasChat ? `${chatCount} messaggi chat` : 'Nessuna chat'}
      >
        <span aria-hidden="true">💬</span>
        {hasChat ? `${chatCount} messaggi` : 'Nessuna chat'}
      </span>

      {/* Date */}
      <span className={CHIP_BASE} style={chipStyle('event')} aria-label={`Data: ${dateLabel}`}>
        <span aria-hidden="true">📅</span>
        {dateLabel}
      </span>
    </div>
  );
}
