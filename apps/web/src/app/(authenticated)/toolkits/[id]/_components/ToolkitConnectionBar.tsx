/**
 * ToolkitConnectionBar — 6-pip connection bar for `/toolkits/[id]`.
 *
 * Pips (left → right): agent · kb · tools · game · author · sessions.
 * Per spec `2026-05-14-stage3-toolkit-detail.md` AC2: 2 pip wired (game,
 * tools), 4 pip "dashed-empty" placeholders for Phase-5 entities (#822).
 *
 * Pure presentational — no click-through handlers in v1 (footer actions
 * cover the primary CTAs). Wraps `data-slot="toolkit-detail-connection-bar"`
 * for tests + spec composability.
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

export type ToolkitPipId = 'agent' | 'kb' | 'tools' | 'game' | 'author' | 'sessions';

export interface ToolkitConnectionPip {
  readonly id: ToolkitPipId;
  readonly label: string;
  /** Display value (count or label). When undefined, pip renders dashed-empty. */
  readonly count?: number | string;
  /** Optional emoji glyph for visual entity-tagging. */
  readonly icon?: string;
}

export interface ToolkitConnectionBarLabels {
  readonly ariaLabel: string;
  readonly emptyHint: string;
}

export interface ToolkitConnectionBarProps {
  readonly pips: ReadonlyArray<ToolkitConnectionPip>;
  readonly labels: ToolkitConnectionBarLabels;
  readonly className?: string;
}

export function ToolkitConnectionBar({
  pips,
  labels,
  className,
}: ToolkitConnectionBarProps): JSX.Element {
  return (
    <aside
      data-slot="toolkit-detail-connection-bar"
      aria-label={labels.ariaLabel}
      className={clsx('flex flex-wrap items-center gap-2 py-2', className)}
    >
      {pips.map(pip => {
        const isEmpty = pip.count === undefined;
        return (
          <span
            key={pip.id}
            data-pip-id={pip.id}
            data-pip-empty={isEmpty || undefined}
            title={isEmpty ? labels.emptyHint : undefined}
            className={clsx(
              'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-bold font-[Quicksand] transition-colors',
              isEmpty
                ? 'border-dashed border-border bg-muted/40 text-muted-foreground/70'
                : 'border-border bg-card text-foreground'
            )}
          >
            {pip.icon && (
              <span aria-hidden="true" className="text-sm">
                {pip.icon}
              </span>
            )}
            <span>{pip.label}</span>
            {!isEmpty && (
              <span
                className="tabular-nums font-mono text-[11px] text-muted-foreground"
                data-pip-count={pip.count}
              >
                {pip.count}
              </span>
            )}
            {isEmpty && (
              <span aria-hidden="true" className="font-mono text-[10px] text-muted-foreground/60">
                —
              </span>
            )}
          </span>
        );
      })}
    </aside>
  );
}
