/**
 * GameDetailCommunityGate - Wave C follow-up (Issue #1465)
 *
 * Mapped from `admin-mockups/design_files/sp4-game-detail.jsx` (CommunityGate, lines 596-626).
 * Tracking: epic #1475; design-handoff PILOT_GAP_REPORT § 2.4.
 *
 * Pure presentational empty-state shown in the locked tabs of a community-variant game
 * (not in the user's library). The caller (#1466 GameDetailView refactor) renders it inside
 * locked tab content and wires `onAdd` to the add-to-library mutation. i18n is caller-side.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface GameDetailCommunityGateProps {
  /** Emoji icon shown in the circle. Defaults to 📘. */
  readonly icon?: string;
  /** Localized title, resolved upstream (caller-side i18n). */
  readonly title: string;
  /** Localized description, resolved upstream. */
  readonly description: string;
  /** Localized CTA label, resolved upstream (no hardcoded fallback). */
  readonly ctaLabel: string;
  /** Invoked when the CTA is clicked (e.g. add-to-library mutation). */
  readonly onAdd: () => void;
  readonly className?: string;
}

export function GameDetailCommunityGate(props: GameDetailCommunityGateProps): ReactElement {
  const { icon = '📘', title, description, ctaLabel, onAdd, className } = props;

  return (
    <section
      data-slot="game-detail-community-gate"
      className={clsx(
        'flex flex-col items-center rounded-2xl border border-dashed border-border-strong bg-card px-6 py-10 text-center',
        className
      )}
    >
      <span
        aria-hidden="true"
        className="mb-3.5 inline-flex h-16 w-16 items-center justify-center rounded-full bg-entity-game/12 text-[28px] text-entity-game"
      >
        {icon}
      </span>
      <h3 className="mb-1.5 font-display text-base font-extrabold text-foreground">{title}</h3>
      <p className="mb-4 max-w-[360px] text-[12.5px] leading-relaxed text-muted-foreground">
        {description}
      </p>
      <button
        type="button"
        onClick={onAdd}
        data-slot="game-detail-community-gate-cta"
        className="rounded-md border-none bg-entity-game px-[18px] py-2.5 font-display text-[13px] font-extrabold text-white shadow-md transition-shadow hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {ctaLabel}
      </button>
    </section>
  );
}
