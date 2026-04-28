/**
 * ContributorsSidebar — top-N community contributors leaderboard.
 *
 * Wave A.3b — V2 Migration `/shared-games` (Issue #596).
 *
 * Renders as an `<aside>` so screen readers can skip it on the catalog page,
 * and is hidden on small viewports (`md:block`) per the spec design.
 *
 * Resilience: when the leaderboard fetch fails or returns empty the aside
 * collapses to a discreet help line rather than blocking the catalog grid.
 */
import type { JSX } from 'react';

import clsx from 'clsx';

import type { TopContributor } from '@/lib/api';

export interface ContributorsSidebarProps {
  readonly contributors: ReadonlyArray<TopContributor> | undefined;
  readonly isLoading: boolean;
  readonly isError: boolean;
  /** Localised heading; defaults to 'Top contributors'. */
  readonly title?: string;
  /** Localised empty-state copy; defaults to a sensible fallback. */
  readonly emptyLabel?: string;
  readonly className?: string;
}

function getInitials(name: string): string {
  const trimmed = name.trim();
  if (!trimmed) return '?';
  const parts = trimmed.split(/\s+/).slice(0, 2);
  return parts.map(p => p.charAt(0).toUpperCase()).join('') || '?';
}

export function ContributorsSidebar({
  contributors,
  isLoading,
  isError,
  title = 'Top contributors',
  emptyLabel = 'Nessun contributor da mostrare al momento.',
  className,
}: ContributorsSidebarProps): JSX.Element {
  const list = contributors ?? [];

  return (
    <aside
      aria-label={title}
      data-testid="shared-games-contributors"
      className={clsx(
        'hidden md:block w-full max-w-[280px] rounded-2xl border border-border bg-card p-4',
        className
      )}
    >
      <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
        {title}
      </h2>

      {isLoading && (
        <ul className="space-y-2" aria-busy="true">
          {Array.from({ length: 5 }).map((_, idx) => (
            <li key={idx} className="h-10 animate-pulse rounded-lg bg-[var(--mc-bg-muted)]" />
          ))}
        </ul>
      )}

      {!isLoading && (isError || list.length === 0) && (
        <p className="text-xs text-muted-foreground">{emptyLabel}</p>
      )}

      {!isLoading && !isError && list.length > 0 && (
        <ol className="space-y-2" data-testid="shared-games-contributors-list">
          {list.map((c, idx) => (
            <li
              key={c.userId}
              className="flex items-center gap-3 rounded-lg px-2 py-1.5 hover:bg-muted/40"
            >
              <span
                aria-hidden="true"
                className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-amber-500/20 text-xs font-semibold text-amber-400"
              >
                {idx + 1}
              </span>
              {c.avatarUrl ? (
                // Use a plain <img> (no Next/Image) to keep this widget free of
                // remote-loader allow-listing constraints. Avatars are tiny.
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={c.avatarUrl}
                  alt=""
                  className="h-8 w-8 shrink-0 rounded-full object-cover"
                  loading="lazy"
                />
              ) : (
                <span
                  aria-hidden="true"
                  className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-medium text-foreground"
                >
                  {getInitials(c.displayName)}
                </span>
              )}
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-foreground">{c.displayName}</p>
                <p className="text-xs text-muted-foreground">
                  {c.totalSessions} sessioni · {c.totalWins} vittorie
                </p>
              </div>
            </li>
          ))}
        </ol>
      )}
    </aside>
  );
}
