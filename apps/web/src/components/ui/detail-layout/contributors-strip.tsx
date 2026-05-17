/**
 * ContributorsStrip — top contributors avatars row for /shared-games/[id] V2.
 *
 * Wave A.4 (Issue #603). Mirrors mockup `sp3-shared-game-detail.jsx`
 * ContributorsStrip (lines 635-693).
 *
 * Visual contract:
 *  - Header row: "Top contributors" label + count "${n} player(s)"
 *  - Avatars: 34px circles with -8px marginLeft overlap stack, hover translateY(-2px)
 *    raises z-index. Maximum N visible avatars; surplus rendered as "+N" overflow
 *    circle (bg-card text-muted).
 *  - Avatar fallback: initials extracted from name (first letter of first 2 tokens).
 *
 * Backend exposes ContributorsCount as an aggregate; the actual contributor list
 * comes from the existing `GetTopContributorsQuery` (Wave A.3a global endpoint).
 * Per-game scoping is deferred to a follow-up.
 */

import type { JSX } from 'react';

import clsx from 'clsx';

export interface ContributorAvatar {
  readonly id: string;
  readonly name: string;
  readonly avatarUrl?: string | null;
}

export interface ContributorsStripLabels {
  readonly title: string;
  readonly playersLabel: (count: number) => string;
  readonly overflowAriaLabel: (count: number) => string;
}

export interface ContributorsStripProps {
  /** Visible avatars. Surplus renders as "+N" overflow circle. */
  readonly contributors: ReadonlyArray<ContributorAvatar>;
  /** Total contributor count (>= contributors.length). */
  readonly totalCount: number;
  /** Maximum avatars to render before overflow indicator (default 8). */
  readonly maxVisible?: number;
  readonly labels: ContributorsStripLabels;
  readonly className?: string;
}

function initials(name: string): string {
  const tokens = name.trim().split(/\s+/).filter(Boolean);
  if (tokens.length === 0) return '?';
  if (tokens.length === 1) return tokens[0].slice(0, 1).toUpperCase();
  return (tokens[0][0] + tokens[1][0]).toUpperCase();
}

export function ContributorsStrip({
  contributors,
  totalCount,
  maxVisible = 8,
  labels,
  className,
}: ContributorsStripProps): JSX.Element {
  const visible = contributors.slice(0, maxVisible);
  const overflow = Math.max(0, totalCount - visible.length);

  return (
    <section
      data-slot="shared-game-detail-contributors"
      className={clsx('flex flex-col gap-2', className)}
      aria-labelledby="contributors-strip-title"
    >
      <div className="flex items-center justify-between gap-2">
        <h3
          id="contributors-strip-title"
          className="m-0 font-display text-sm font-semibold text-foreground"
        >
          {labels.title}
        </h3>
        <span
          className="font-mono text-[11px] uppercase tracking-[0.06em] text-[hsl(var(--text-muted))]"
          data-dynamic="number"
        >
          {labels.playersLabel(totalCount)}
        </span>
      </div>

      {visible.length > 0 ? (
        <ul role="list" className="flex items-center" style={{ paddingLeft: '8px' }}>
          {visible.map((c, i) => (
            <li
              key={c.id}
              className={clsx(
                'relative inline-flex h-[34px] w-[34px] items-center justify-center rounded-full border-2 border-card bg-muted text-[11px] font-bold uppercase text-foreground',
                'transition-transform duration-150 hover:-translate-y-0.5'
              )}
              style={{
                marginLeft: i === 0 ? 0 : '-8px',
                zIndex: visible.length - i,
              }}
              title={c.name}
            >
              {c.avatarUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={c.avatarUrl}
                  alt={c.name}
                  className="h-full w-full rounded-full object-cover"
                />
              ) : (
                <span aria-label={c.name}>{initials(c.name)}</span>
              )}
            </li>
          ))}
          {overflow > 0 ? (
            <li
              aria-label={labels.overflowAriaLabel(overflow)}
              className="relative inline-flex h-[34px] w-[34px] items-center justify-center rounded-full border-2 border-card bg-card text-[10px] font-mono font-semibold text-[hsl(var(--text-muted))]"
              style={{ marginLeft: '-8px', zIndex: 0 }}
              data-dynamic="number"
            >
              +{overflow}
            </li>
          ) : null}
        </ul>
      ) : null}
    </section>
  );
}
