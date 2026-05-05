/**
 * EmptyState — empty-tab placeholder for /shared-games/[id] V2.
 *
 * Wave A.4 (Issue #603). Mirrors mockup `sp3-shared-game-detail.jsx` TabEmpty
 * pattern: 64px circle bg-muted icon, h3 title, p description.
 *
 * Used inside Toolkits / Agents / Knowledge tabs when count = 0.
 * The Community tab does not have an empty state (always shows aggregate counts).
 */

import type { JSX } from 'react';

import clsx from 'clsx';

export type EmptyStateKind = 'no-toolkits' | 'no-agents' | 'no-kbs';

const KIND_ICON: Record<EmptyStateKind, string> = {
  'no-toolkits': '🧰',
  'no-agents': '🤖',
  'no-kbs': '📚',
};

export interface EmptyStateLabels {
  readonly title: string;
  readonly description: string;
}

export interface EmptyStateProps {
  readonly kind: EmptyStateKind;
  readonly labels: EmptyStateLabels;
  readonly className?: string;
}

export function EmptyState({ kind, labels, className }: EmptyStateProps): JSX.Element {
  return (
    <div
      data-slot="shared-game-detail-empty-state"
      data-kind={kind}
      className={clsx(
        'flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-border bg-muted/30 px-6 py-10 text-center',
        className
      )}
    >
      <span
        aria-hidden="true"
        className="flex h-16 w-16 items-center justify-center rounded-full bg-muted text-[28px]"
      >
        {KIND_ICON[kind]}
      </span>
      <h3 className="m-0 font-display text-base font-semibold text-foreground">{labels.title}</h3>
      <p className="m-0 max-w-md text-sm text-[hsl(var(--text-muted))]">{labels.description}</p>
    </div>
  );
}
