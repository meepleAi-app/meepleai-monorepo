/**
 * AchievementsCarousel — Wave D.3 v2 component (Issue #756).
 *
 * Horizontal scroll-snap carousel of achievement cards (icon + title +
 * unlock-status). Each card carries `data-unlocked={true|undefined}` for
 * test/CSS targeting. Empty state renders a dashed-border placeholder.
 *
 * Mockup mapping:
 *   - admin-mockups/design_files/sp4-session-summary.jsx (Achievements)
 *
 * Contract reference: docs/frontend/contracts/sessions-id-summary-hooks.md §5.6.
 *
 * Schema reality v1 carryover (Gate B): no backend `/achievements` endpoint.
 * Cards are rendered from a frontend-only stub fixture
 * (`AchievementDto` per contract §4.2). Orchestrator sources data from
 * `useSessionAchievements` hook returning deterministic fixture.
 *
 * MeepleCard divergence (Gate C): emoji-first card with locked/unlocked
 * variant inversion (locked → muted, dashed border, opacity 0.45). MeepleCard's
 * subject avatar/title/subtitle pattern doesn't fit the locked-overlay
 * inverted state. DIVERGE.
 *
 * A11y:
 *   - `<ul role="list">` (CSS overrides default list-style; explicit role
 *     restores semantic meaning for screen readers).
 *   - Locked items: `aria-label="Bloccato: {name}"` (orchestrator builds via
 *     `lockedAriaPrefix + titleKey resolved`).
 *   - Carousel uses CSS `scroll-snap-type: x mandatory` for snap-to-card
 *     behavior.
 *
 * Pure component: orchestrator resolves all i18n strings.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { AchievementDto } from '@/lib/sessions-summary/schemas';

export interface AchievementsCarouselLabels {
  readonly title: string;
  /** Pre-resolved unlock count (e.g., "3 / 6 sbloccati") — orchestrator handles plural. */
  readonly unlockedCount: string;
  /** Empty title shown when achievements array is empty. */
  readonly emptyTitle: string;
  readonly emptyDescription: string;
  /** Prefix for `aria-label` on locked cards. */
  readonly lockedAriaPrefix: string;
  /** Pre-resolved unlock-time text (orchestrator formats date per locale). */
  readonly unlockedAtText: (achievementId: string) => string | null;
  /** Resolved title per achievement id (i18n key resolution by orchestrator). */
  readonly titleFor: (achievementId: string) => string;
  /** Resolved description per achievement id. */
  readonly descriptionFor: (achievementId: string) => string;
}

export interface AchievementsCarouselProps {
  readonly achievements: readonly AchievementDto[];
  readonly labels: AchievementsCarouselLabels;
  readonly className?: string;
}

export function AchievementsCarousel({
  achievements,
  labels,
  className,
}: AchievementsCarouselProps): ReactElement {
  const isEmpty = achievements.length === 0;

  if (isEmpty) {
    return (
      <section
        data-slot="achievements-carousel"
        data-empty="true"
        className={clsx('rounded-lg border border-dashed border-border bg-card p-6', className)}
      >
        <div className="flex flex-col items-center gap-2 text-center">
          <span aria-hidden="true" className="text-3xl">
            🏅
          </span>
          <h3 className="font-display text-sm font-extrabold text-foreground">
            {labels.emptyTitle}
          </h3>
          <p className="text-xs text-muted-foreground">{labels.emptyDescription}</p>
        </div>
      </section>
    );
  }

  return (
    <section
      data-slot="achievements-carousel"
      data-empty={undefined}
      className={clsx('flex flex-col gap-2', className)}
    >
      <div className="flex items-baseline justify-between">
        <h3 className="font-display text-base font-extrabold text-foreground">
          <span aria-hidden="true" className="mr-1.5">
            🏅
          </span>
          {labels.title}
        </h3>
        <span className="font-mono text-[11px] font-bold text-muted-foreground">
          {labels.unlockedCount}
        </span>
      </div>
      <ul
        role="list"
        className={clsx(
          'flex gap-2 overflow-x-auto pb-1',
          '[scrollbar-width:none] [&::-webkit-scrollbar]:hidden',
          '[scroll-snap-type:x_mandatory]'
        )}
      >
        {achievements.map(a => {
          const unlocked = a.unlockedAt !== null;
          const titleResolved = labels.titleFor(a.id);
          const ariaLabel = unlocked ? titleResolved : `${labels.lockedAriaPrefix}${titleResolved}`;
          return (
            <li
              key={a.id}
              role="listitem"
              data-slot="achievement-card"
              data-unlocked={unlocked || undefined}
              aria-label={ariaLabel}
              className={clsx(
                'flex min-w-[220px] shrink-0 items-center gap-2.5 rounded-md p-3',
                '[scroll-snap-align:start]',
                unlocked
                  ? 'border border-[hsla(142,70%,31%,0.3)] bg-[hsla(142,70%,31%,0.06)]'
                  : 'border border-dashed border-border bg-muted/40 opacity-60'
              )}
            >
              <div
                aria-hidden="true"
                className={clsx(
                  'flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-base',
                  unlocked ? 'bg-[hsla(142,70%,31%,0.18)]' : 'bg-card'
                )}
              >
                {unlocked ? a.iconEmoji : '🔒'}
              </div>
              <div className="min-w-0 flex-1">
                <div
                  className={clsx(
                    'truncate font-display text-xs font-extrabold',
                    unlocked ? 'text-[hsl(142,70%,31%)]' : 'text-muted-foreground'
                  )}
                >
                  {titleResolved}
                </div>
                <div className="truncate font-mono text-[10px] font-bold text-muted-foreground">
                  {unlocked
                    ? (labels.unlockedAtText(a.id) ?? labels.descriptionFor(a.id))
                    : labels.descriptionFor(a.id)}
                </div>
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
