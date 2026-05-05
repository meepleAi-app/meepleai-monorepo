/**
 * PersonaCard - v2 Wave C.2 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-agent-detail.jsx` (PersonaCard).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #581)
 *
 * Pure presentational component — no hooks, no i18n calls, no data fetching.
 * Displays the agent persona description (Identity tab).
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface PersonaCardLabels {
  readonly title: string;
  readonly empty: string;
}

export interface PersonaCardProps {
  readonly persona: string | null;
  readonly labels: PersonaCardLabels;
  readonly className?: string;
}

export function PersonaCard(props: PersonaCardProps): ReactElement {
  const { persona, labels, className } = props;

  return (
    <section
      data-slot="agent-detail-persona-card"
      className={clsx('rounded-xl border border-border bg-card px-5 py-4 shadow-sm', className)}
    >
      <h3 className="mb-3 font-display text-[13px] font-extrabold uppercase tracking-[0.06em] text-muted-foreground">
        {labels.title}
      </h3>

      {persona ? (
        <p className="font-display text-[14px] font-medium leading-relaxed text-foreground">
          {persona}
        </p>
      ) : (
        <p className="font-display text-[13px] italic text-muted-foreground">{labels.empty}</p>
      )}
    </section>
  );
}
