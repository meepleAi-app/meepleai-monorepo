/**
 * OutcomeBadge (play-records) — Task 0.4 (Issue #1488 / Epic #1475 Phase D).
 *
 * Pill-shaped badge displaying a PlayRecord's outcome from the current user's
 * point of view. Pairs with `derivePerspective` — `variant` typically equals
 * the returned `perspective.kind` (with `spectator` mapped by callers to the
 * appropriate competitive variant, since spectator is "view-only" UX).
 *
 * **Distinct from `components/features/sessions/OutcomeBadge`** — sessions
 * uses a `status × outcome × paused` matrix. Play-records collapses status
 * and outcome into a single 6-variant union for clearer call-site ergonomics.
 *
 * Entity tint mapping (mockup `sp4-play-records-{index,detail}.jsx`):
 *   - won         → entity-toolkit (success green)
 *   - lost        → entity-event   (loss red)
 *   - tie         → entity-toolkit (neutral handshake — matches mockup)
 *   - cooperative → entity-toolkit (same as tie — non-competitive accent)
 *   - inprogress  → entity-session (live, pulsing dot)
 *   - planned     → entity-event   (calendar — future-tense accent)
 *
 * Pure component: all strings come from `labels`. No `useTranslations`.
 *
 * @see plan `docs/superpowers/plans/2026-05-29-play-records-reskin.md` Task 0 Step 4
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export type OutcomeBadgeVariant = 'won' | 'lost' | 'tie' | 'cooperative' | 'inprogress' | 'planned';

export interface OutcomeBadgeLabels {
  readonly won: string;
  readonly lost: string;
  readonly tie: string;
  readonly cooperative: string;
  readonly inprogress: string;
  readonly planned: string;
}

export interface OutcomeBadgeProps {
  readonly variant: OutcomeBadgeVariant;
  readonly labels: OutcomeBadgeLabels;
  readonly className?: string;
  /** Override visual label with an explicit accessible name. */
  readonly 'aria-label'?: string;
}

const PILL_BASE =
  'inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-mono text-[10px] font-extrabold uppercase tracking-wider ring-1';

// Per-variant Tailwind className — uses semantic + entity utilities only.
// `won` and `lost` use Tailwind semantic palettes (emerald-700/rose-700) which
// are already allowlisted by the ESLint rule (text-emerald-700 / bg-emerald-50
// are explicitly used elsewhere in `sessions/OutcomeBadge.tsx`).
const VARIANT_STYLE: Record<OutcomeBadgeVariant, string> = {
  won: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  lost: 'bg-rose-50 text-rose-700 ring-rose-200',
  tie: 'bg-entity-toolkit/14 text-entity-toolkit ring-entity-toolkit/30',
  cooperative: 'bg-entity-toolkit/14 text-entity-toolkit ring-entity-toolkit/30',
  inprogress: 'mai-pulse bg-entity-session/14 text-entity-session ring-entity-session/30',
  planned: 'bg-entity-event/14 text-entity-event ring-entity-event/30',
};

export function OutcomeBadge({
  variant,
  labels,
  className,
  'aria-label': ariaLabel,
}: OutcomeBadgeProps): ReactElement {
  const label = labels[variant];
  const isLive = variant === 'inprogress';

  return (
    <span
      data-slot="outcome-badge"
      data-variant={variant}
      role={isLive ? 'status' : undefined}
      aria-label={ariaLabel}
      className={clsx(PILL_BASE, VARIANT_STYLE[variant], className)}
    >
      {isLive ? (
        <span
          aria-hidden="true"
          className="inline-block h-1.5 w-1.5 rounded-full bg-entity-session"
        />
      ) : null}
      {label}
    </span>
  );
}
