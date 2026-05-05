/**
 * SessionMetaGrid — vertical 5-row metadata grid for invite session details.
 *
 * Wave A.5b (Issue #611). Mirrors mockup `sp3-accept-invite.jsx` lines 164-208
 * (SessionMetaGrid). Five fixed rows: Data / Ora / Durata / Luogo / Giocatori.
 * Mono font on values flagged `mono: true` (Ora, Durata) for visual rhythm
 * with mockup's typographic hierarchy.
 *
 * Layout: `<ul>` with `<li>` rows separated by `border-light`. Each row:
 *  - 22×22 icon box (bg-card, rounded 6px)
 *  - 9px mono uppercase eyebrow label, fixed 60px width
 *  - flex-1 value (right-aligned), conditionally mono-font
 *
 * Empty/null values fall back to em-dash "—" (visible difference from
 * "Nessun" copy used elsewhere; semantically distinct).
 */

import type { JSX, ReactNode } from 'react';

import clsx from 'clsx';

export interface SessionMetaItem {
  /** Unique key (label is also used). */
  readonly key: string;
  /** Single emoji or icon node, decorative only. */
  readonly icon: ReactNode;
  /** Localized label (e.g. "Data", "Ora", "Luogo"). */
  readonly label: string;
  /** Display value (already formatted by caller). */
  readonly value: ReactNode;
  /** True for tabular/timestamp-like values (Ora, Durata). */
  readonly mono?: boolean;
}

export interface SessionMetaGridProps {
  readonly items: readonly SessionMetaItem[];
  readonly compact?: boolean;
  readonly className?: string;
}

export function SessionMetaGrid({
  items,
  compact = false,
  className,
}: SessionMetaGridProps): JSX.Element {
  return (
    <ul
      data-slot="invite-session-meta-grid"
      className={clsx(
        'm-0 flex list-none flex-col overflow-hidden rounded-lg border bg-[hsl(var(--bg-muted))] p-0',
        className
      )}
    >
      {items.map((item, i) => (
        <li
          key={item.key}
          className={clsx(
            'flex items-center gap-2.5',
            compact ? 'px-3 py-2' : 'px-3.5 py-2.5',
            i === 0 ? 'border-t-0' : 'border-t border-[hsl(var(--border-light))]'
          )}
        >
          <span
            aria-hidden="true"
            className="inline-flex h-[22px] w-[22px] flex-shrink-0 items-center justify-center rounded-md bg-[hsl(var(--bg-card))] text-[12px]"
          >
            {item.icon}
          </span>
          <span className="w-[60px] flex-shrink-0 font-mono text-[9px] font-bold uppercase tracking-[0.08em] text-[hsl(var(--text-muted))]">
            {item.label}
          </span>
          <span
            className={clsx(
              'flex-1 text-right text-[12px] font-semibold text-foreground',
              item.mono && 'font-mono'
            )}
          >
            {item.value}
          </span>
        </li>
      ))}
    </ul>
  );
}
