/**
 * GameNightStatusBadge - v2 SP7 #951 commit 2
 *
 * Status pill for game-night lifecycle. Replaces the inline
 * `GameNightStatusBadge` helper in legacy `app/(authenticated)/game-nights/[id]/page.tsx`.
 *
 * Mapped from `admin-mockups/design_files/sp7-game-night-detail-rsvp.jsx` (StatusBadge).
 *
 * Status → entity color mapping (per CLAUDE.md DS-15 entity utilities):
 *   - draft/pending   → event   (rose, pulsing dot — planned, not yet committed)
 *   - published       → toolkit (green, pulsing dot — ready/upcoming)
 *   - inProgress      → session (indigo, pulsing dot — live)
 *   - completed       → muted (no dot, ✓ icon)
 *   - cancelled       → danger (no dot, ✕ icon, strikethrough host-side)
 *
 * Pure presentational; labels resolved by caller via i18n.
 */

import clsx from 'clsx';

import type { GameNightStatus } from '@/lib/api/schemas/game-nights.schemas';

export interface GameNightStatusBadgeProps {
  readonly status: GameNightStatus;
  /** Localized label (caller resolves t('gameNightDetail.status.<key>')). */
  readonly label: string;
  /** Optional class hook for layout overrides. */
  readonly className?: string;
}

interface StatusVisualConfig {
  readonly entityClass: string;
  readonly showPulsingDot: boolean;
  readonly icon: string | null;
}

const STATUS_VISUALS: Record<GameNightStatus, StatusVisualConfig> = {
  Draft: {
    entityClass: 'bg-entity-event/14 text-entity-event border-entity-event/28',
    showPulsingDot: true,
    icon: null,
  },
  Published: {
    entityClass: 'bg-entity-toolkit/14 text-entity-toolkit border-entity-toolkit/28',
    showPulsingDot: true,
    icon: null,
  },
  Completed: {
    entityClass: 'bg-muted text-muted-foreground border-border',
    showPulsingDot: false,
    icon: '✓',
  },
  Cancelled: {
    entityClass: 'bg-destructive/10 text-destructive border-destructive/30',
    showPulsingDot: false,
    icon: '✕',
  },
};

export function GameNightStatusBadge({
  status,
  label,
  className,
}: GameNightStatusBadgeProps): React.JSX.Element {
  const visual = STATUS_VISUALS[status];

  return (
    <span
      data-testid="game-night-status-badge"
      data-status={status}
      className={clsx(
        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1',
        'font-mono text-[10px] font-extrabold uppercase tracking-[0.08em]',
        visual.entityClass,
        className
      )}
    >
      {visual.showPulsingDot ? (
        <span
          aria-hidden="true"
          className={clsx(
            'inline-block h-1.5 w-1.5 shrink-0 rounded-full animate-pulse',
            // Inherits text color for the dot fill — avoids a separate token map.
            'bg-current'
          )}
        />
      ) : visual.icon !== null ? (
        <span aria-hidden="true">{visual.icon}</span>
      ) : null}
      <span>{label}</span>
    </span>
  );
}
