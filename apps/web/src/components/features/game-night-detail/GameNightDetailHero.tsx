/**
 * GameNightDetailHero - v2 SP7 #951 commit 2b
 *
 * Hero section for /game-nights/[id] showing status pill + title + schedule +
 * location + host card. Replaces the legacy inline `<h1>` + Card header in
 * `app/(authenticated)/game-nights/[id]/page.tsx`.
 *
 * Mapped from `admin-mockups/design_files/sp7-game-night-detail-rsvp.jsx` (Hero).
 *
 * Visual contract:
 *   - Background gradient tinted by event entity (rose) for upcoming/draft,
 *     session (indigo) for inProgress, muted for completed, destructive for
 *     cancelled. Achieved via `data-status` + Tailwind data-attr variants below.
 *   - Title strikes through when status=Cancelled (mockup line 201-203).
 *   - Decorative corner blob (mockup line 177-182) preserved via a single
 *     absolutely-positioned span; aria-hidden.
 *
 * Pure presentational: labels + formatted date come from caller (page-client
 * resolves i18n + locale-aware date format via Intl.DateTimeFormat upstream).
 *
 * AC: T A V (mobile + desktop)
 */

import clsx from 'clsx';

import type { GameNightStatus } from '@/lib/api/schemas/game-nights.schemas';

import { GameNightAvatar, computeInitials, deriveHueFromId } from './GameNightAvatar';
import { GameNightStatusBadge } from './GameNightStatusBadge';

export interface GameNightDetailHeroLabels {
  /** Localized status label matching GameNightStatusBadge expectation. */
  readonly statusLabel: string;
  /** Pre-formatted, localized schedule line, e.g. "Sabato 17 maggio · 21:00". */
  readonly scheduledLine: string;
  /** Optional pre-formatted location line, e.g. "Casa Marco · Padova". */
  readonly locationLine?: string;
  /** "Organizzata da Marco R." — already localized + interpolated by caller. */
  readonly organizedByLine: string;
  /** "8 invitati · 3h stimate" — optional meta line under organizer. */
  readonly metaLine?: string;
}

export interface GameNightDetailHeroProps {
  readonly title: string;
  readonly status: GameNightStatus;
  readonly labels: GameNightDetailHeroLabels;
  readonly organizerId: string;
  readonly organizerName: string;
  /** Click handler for the location button — opens map / details in caller. */
  readonly onOpenLocation?: () => void;
  readonly className?: string;
}

const GRADIENT_BY_STATUS: Record<GameNightStatus, string> = {
  Draft: 'bg-gradient-to-br from-entity-event/[0.16] to-entity-event/[0.02]',
  Published: 'bg-gradient-to-br from-entity-toolkit/[0.16] to-entity-toolkit/[0.02]',
  Completed: 'bg-muted',
  Cancelled: 'bg-gradient-to-br from-destructive/[0.14] to-destructive/[0.04]',
};

const ACCENT_TEXT_BY_STATUS: Record<GameNightStatus, string> = {
  Draft: 'text-entity-event',
  Published: 'text-entity-toolkit',
  Completed: 'text-muted-foreground',
  Cancelled: 'text-destructive',
};

export function GameNightDetailHero({
  title,
  status,
  labels,
  organizerId,
  organizerName,
  onOpenLocation,
  className,
}: GameNightDetailHeroProps): React.JSX.Element {
  const isCancelled = status === 'Cancelled';
  const accentTextClass = ACCENT_TEXT_BY_STATUS[status];

  return (
    <header
      data-testid="game-night-detail-hero"
      data-status={status}
      className={clsx(
        'relative overflow-hidden border-b border-border px-4 py-5 md:px-6 md:py-6',
        GRADIENT_BY_STATUS[status],
        className
      )}
    >
      <span
        aria-hidden="true"
        className={clsx(
          'pointer-events-none absolute -right-6 -top-6 h-28 w-28 rounded-full opacity-25 blur-xl',
          // The blob inherits the accent text color via current-color trick.
          'bg-current',
          accentTextClass
        )}
      />

      <div className="relative flex flex-col gap-2.5">
        <div className="flex items-center gap-2">
          <GameNightStatusBadge status={status} label={labels.statusLabel} />
        </div>

        <h1
          className={clsx(
            'font-display text-xl font-extrabold leading-tight tracking-tight text-foreground md:text-2xl',
            isCancelled && 'line-through decoration-2 decoration-destructive/60'
          )}
        >
          {title}
        </h1>

        <div className="flex flex-col gap-1.5 font-mono text-xs font-bold text-muted-foreground">
          <div className="flex items-center gap-2">
            <span aria-hidden="true" className={clsx('text-base leading-none', accentTextClass)}>
              📅
            </span>
            <span>{labels.scheduledLine}</span>
          </div>

          {labels.locationLine !== undefined &&
            (onOpenLocation ? (
              <button
                type="button"
                onClick={onOpenLocation}
                className="group flex items-center gap-2 self-start text-left text-muted-foreground hover:text-foreground"
              >
                <span
                  aria-hidden="true"
                  className={clsx('text-base leading-none', accentTextClass)}
                >
                  📍
                </span>
                <span className="border-b border-dashed border-current">{labels.locationLine}</span>
                <span
                  aria-hidden="true"
                  className={clsx('text-xs font-extrabold', accentTextClass)}
                >
                  ↗
                </span>
              </button>
            ) : (
              <div className="flex items-center gap-2">
                <span
                  aria-hidden="true"
                  className={clsx('text-base leading-none', accentTextClass)}
                >
                  📍
                </span>
                <span>{labels.locationLine}</span>
              </div>
            ))}
        </div>

        <div className="mt-2 flex items-center gap-2.5 border-t border-border/60 pt-2.5">
          <GameNightAvatar
            initials={computeInitials(organizerName)}
            label={organizerName}
            hue={deriveHueFromId(organizerId)}
            size={28}
          />
          <div className="min-w-0 flex-1">
            <div className="truncate font-display text-xs font-extrabold text-foreground">
              {labels.organizedByLine}
            </div>
            {labels.metaLine !== undefined && (
              <div className="font-mono text-[10px] font-bold text-muted-foreground">
                {labels.metaLine}
              </div>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
