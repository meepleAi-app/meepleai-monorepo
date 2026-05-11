/**
 * SystemPromptViewer - v2 Wave C.2 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-agent-detail.jsx` (SystemPromptViewer).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #581)
 *
 * Pure presentational component — no hooks, no i18n calls, no data fetching.
 * Displays the agent system prompt text (Identity tab) in a readable pre-formatted area.
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface SystemPromptViewerLabels {
  readonly title: string;
  readonly empty: string;
  readonly hidden: string;
}

export interface SystemPromptViewerProps {
  readonly prompt: string | null;
  readonly labels: SystemPromptViewerLabels;
  readonly className?: string;
}

export function SystemPromptViewer(props: SystemPromptViewerProps): ReactElement {
  const { prompt, labels, className } = props;

  return (
    <section
      data-slot="agent-detail-system-prompt-viewer"
      className={clsx('rounded-xl border border-border bg-card shadow-sm', className)}
    >
      <div className="flex items-center justify-between border-b border-border px-5 py-3">
        <h3 className="font-display text-[13px] font-extrabold uppercase tracking-[0.06em] text-muted-foreground">
          {labels.title}
        </h3>
        {prompt ? (
          <span className="rounded-full bg-emerald-100 px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase tracking-[0.06em] text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300">
            ✓
          </span>
        ) : null}
      </div>

      {prompt ? (
        <pre className="overflow-x-auto px-5 py-4 font-mono text-[12px] leading-relaxed text-foreground [white-space:pre-wrap] [word-break:break-word]">
          {prompt}
        </pre>
      ) : (
        <p className="px-5 py-4 font-display text-[13px] italic text-muted-foreground">
          {labels.empty}
        </p>
      )}
    </section>
  );
}
