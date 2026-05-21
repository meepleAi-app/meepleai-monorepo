/**
 * AgentDangerZone - v2 Wave C.2 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-agent-detail.jsx` (SettingsTab danger zone).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #581)
 *
 * Pure presentational component — no hooks, no i18n calls, no data fetching.
 *
 * CRITICAL: only renders when `variant === 'active'`. All other variants return `null`.
 * This contract must be enforced here so the orchestrator can pass variant freely
 * without conditional rendering at the call site.
 *
 * A11y contract (Phase 0.5 sez. 4.4):
 *   - Container: `role="region"` + `aria-labelledby`
 *   - Archive button is destructive — labeled clearly
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';
import { useId } from 'react';

import clsx from 'clsx';

export type AgentDangerZoneVariant = 'active' | 'draft' | 'archived';

export interface AgentDangerZoneLabels {
  readonly dangerZoneTitle: string;
  readonly archiveCta: string;
  readonly archiveConfirmTitle: string;
  readonly archiveConfirmSubtitle: string;
  readonly archiveConfirm: string;
  readonly archiveCancel: string;
}

export interface AgentDangerZoneProps {
  readonly variant: AgentDangerZoneVariant;
  readonly onArchive: () => void;
  readonly labels: AgentDangerZoneLabels;
  readonly className?: string;
}

export function AgentDangerZone(props: AgentDangerZoneProps): ReactElement | null {
  const { variant, onArchive, labels, className } = props;
  const headingId = useId();

  // CRITICAL: only render for active variant.
  if (variant !== 'active') {
    return null;
  }

  return (
    <section
      role="region"
      aria-labelledby={headingId}
      data-slot="agent-detail-danger-zone"
      data-variant={variant}
      className={clsx(
        'rounded-xl border border-rose-200 bg-rose-50/50 px-5 py-5 dark:border-rose-900/40 dark:bg-rose-950/10',
        className
      )}
    >
      <h3
        id={headingId}
        className="mb-4 font-display text-[13px] font-extrabold uppercase tracking-[0.06em] text-rose-700 dark:text-rose-400"
      >
        {labels.dangerZoneTitle}
      </h3>

      <div className="flex flex-wrap items-center justify-between gap-4 rounded-lg border border-rose-200 bg-card px-4 py-4 dark:border-rose-900/30 dark:bg-rose-950/20">
        <div>
          <p className="font-display text-[13px] font-bold text-foreground">
            {labels.archiveConfirmTitle}
          </p>
          <p className="mt-0.5 font-display text-[12px] text-muted-foreground">
            {labels.archiveConfirmSubtitle}
          </p>
        </div>
        <button
          type="button"
          onClick={onArchive}
          className="inline-flex shrink-0 items-center gap-1.5 rounded-lg border border-rose-300 bg-white px-4 py-2 font-display text-[13px] font-bold text-rose-700 hover:bg-rose-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-700 focus-visible:ring-offset-2 dark:border-rose-800 dark:bg-rose-950/30 dark:text-rose-400 dark:hover:bg-rose-950/50"
        >
          ⊘ {labels.archiveCta}
        </button>
      </div>
    </section>
  );
}
