/**
 * GameNightRsvpRow - v2 SP7 #951 commit 2
 *
 * Single roster entry showing avatar + name + RSVP status. Replaces the inline
 * `RsvpBadge` + list-item layout in legacy game-nights/[id]/page.tsx.
 *
 * Mapped from `admin-mockups/design_files/sp7-game-night-detail-rsvp.jsx` (RSVPRow).
 *
 * Visual states:
 *   - Pending (dashed border)  → entity-player muted, dotted
 *   - Accepted                 → ✓ icon, success color
 *   - Maybe                    → ? icon, warning color
 *   - Declined                 → ✕ icon, muted text, line-through name
 *   - isMe                     → entity-player ring, highlighted background
 *
 * Pure presentational; consumer composes from rsvps + viewer context.
 */

import clsx from 'clsx';

import type { RsvpStatus } from '@/lib/api/schemas/game-nights.schemas';

import { GameNightAvatar, computeInitials, deriveHueFromId } from './GameNightAvatar';

export interface GameNightRsvpRowProps {
  readonly userId: string;
  readonly userName: string;
  readonly status: RsvpStatus;
  /** Localized status label (caller resolves t('gameNightDetail.participants.rsvpStatus.<key>')). */
  readonly statusLabel: string;
  /** Marks this row as the viewer's own RSVP. */
  readonly isMe?: boolean;
  /** Marks the host row with a small "host" pill. */
  readonly isHost?: boolean;
  /** Localized "host" pill label (caller resolves). Defaults to 'host' when isHost && undefined. */
  readonly hostLabel?: string;
  /**
   * Render mode (issue #1169). Defaults to `'authenticated'`. In `'public'`
   * mode the "me" pill is suppressed (no signed-in user can be matched to a
   * roster entry on the public surface), keeping the row purely read-only
   * for anonymous viewers.
   */
  readonly mode?: 'authenticated' | 'public';
  readonly className?: string;
}

interface StatusVisual {
  readonly icon: string;
  readonly iconClass: string;
  readonly labelClass: string;
  readonly lineThrough: boolean;
  readonly dashedBorder: boolean;
  readonly mutedRow: boolean;
}

const STATUS_VISUALS: Record<RsvpStatus, StatusVisual> = {
  Accepted: {
    icon: '✓',
    iconClass: 'text-success',
    labelClass: 'text-success',
    lineThrough: false,
    dashedBorder: false,
    mutedRow: false,
  },
  Maybe: {
    icon: '?',
    iconClass: 'text-warning',
    labelClass: 'text-warning',
    lineThrough: false,
    dashedBorder: false,
    mutedRow: false,
  },
  Declined: {
    icon: '×',
    iconClass: 'text-muted-foreground',
    labelClass: 'text-muted-foreground',
    lineThrough: true,
    dashedBorder: false,
    mutedRow: true,
  },
  Pending: {
    icon: '⏳',
    iconClass: 'text-muted-foreground',
    labelClass: 'text-muted-foreground',
    lineThrough: false,
    dashedBorder: true,
    mutedRow: false,
  },
};

export function GameNightRsvpRow({
  userId,
  userName,
  status,
  statusLabel,
  isMe = false,
  isHost = false,
  hostLabel,
  mode = 'authenticated',
  className,
}: GameNightRsvpRowProps): React.JSX.Element {
  const visual = STATUS_VISUALS[status];
  // In public mode the "me" pill never renders — there is no authenticated
  // viewer to match against (issue #1169). The `isMe` prop is silently
  // ignored to keep callers from having to gate the boolean upstream.
  const resolvedIsMe = mode === 'public' ? false : isMe;

  return (
    <div
      data-testid="game-night-rsvp-row"
      data-user-id={userId}
      data-status={status}
      data-is-me={resolvedIsMe ? 'true' : 'false'}
      data-mode={mode}
      className={clsx(
        'flex items-center gap-2.5 rounded-md px-3.5 py-2.5',
        visual.dashedBorder
          ? 'border border-dashed border-entity-player/40'
          : resolvedIsMe
            ? 'border border-entity-player/30 bg-entity-player/[0.06]'
            : 'border border-border bg-card',
        visual.mutedRow && 'opacity-70',
        className
      )}
    >
      <GameNightAvatar
        initials={computeInitials(userName)}
        label={userName}
        hue={deriveHueFromId(userId)}
        size={32}
        highlightSelf={resolvedIsMe}
      />

      <div className="min-w-0 flex-1">
        <div
          className={clsx(
            'flex items-center gap-1.5 font-display text-[13px] font-extrabold text-foreground',
            visual.lineThrough && 'line-through'
          )}
        >
          <span className="truncate">{userName}</span>
          {resolvedIsMe && (
            <span
              className={clsx(
                'shrink-0 rounded-sm bg-entity-player/[0.18] px-1.5 py-0.5',
                'font-mono text-[8px] font-extrabold uppercase tracking-[0.06em] text-entity-player'
              )}
            >
              me
            </span>
          )}
          {isHost && hostLabel !== undefined && (
            <span
              className={clsx(
                'shrink-0 rounded-sm bg-entity-event/[0.18] px-1.5 py-0.5',
                'font-mono text-[8px] font-extrabold uppercase tracking-[0.06em] text-entity-event'
              )}
            >
              {hostLabel}
            </span>
          )}
        </div>
      </div>

      <div
        className={clsx(
          'flex shrink-0 items-center gap-1.5',
          'font-mono text-[11px] font-extrabold uppercase tracking-[0.04em]',
          visual.labelClass
        )}
      >
        <span aria-hidden="true" className={clsx('text-base leading-none', visual.iconClass)}>
          {visual.icon}
        </span>
        <span>{statusLabel}</span>
      </div>
    </div>
  );
}
