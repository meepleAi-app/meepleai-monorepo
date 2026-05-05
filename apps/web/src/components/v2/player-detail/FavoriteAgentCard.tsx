/**
 * FavoriteAgentCard — Wave 3 /players/[id] v2 component (Task 2).
 *
 * Mapped from `admin-mockups/design_files/sp4-player-detail.jsx`
 * (FavoriteAgentCard in OverviewTab — agente preferito).
 *
 * Pure component: all i18n strings injected via `labels`. No hooks.
 *
 * Renders a card with:
 *   - Robot emoji icon + title
 *   - Agent name + game name when agentName is non-null
 *   - "none" fallback text when agentName is null
 *   - Optional open button (↗) with aria-label "Open {agentName}"
 *
 * WCAG:
 *   - Open button aria-label interpolated from labels.ariaLabel template.
 *   - Robot emoji aria-hidden (decorative).
 *   - 700-shade violet for open button (WCAG AA pre-emption).
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface FavoriteAgentCardLabels {
  readonly title: string;
  readonly none: string;
  /** Template: "Open {agentName}" — substitute {agentName} */
  readonly ariaLabel: string;
}

export interface FavoriteAgentCardProps {
  readonly agentName: string | null;
  readonly gameName: string | null;
  readonly onClick?: () => void;
  readonly labels: FavoriteAgentCardLabels;
  readonly className?: string;
}

/** Substitutes {agentName} in the aria-label template. */
function interpolateAriaLabel(template: string, agentName: string): string {
  return template.replace('{agentName}', agentName);
}

export function FavoriteAgentCard({
  agentName,
  gameName,
  onClick,
  labels,
  className,
}: FavoriteAgentCardProps): ReactElement {
  return (
    <div
      data-slot="player-detail-favorite-agent"
      className={clsx(
        'flex items-center gap-3 rounded-2xl border border-border bg-card p-4 shadow-sm',
        className
      )}
    >
      {/* Agent icon */}
      <div
        aria-hidden="true"
        className="flex h-11 w-11 flex-shrink-0 items-center justify-center rounded-xl bg-violet-500/14 text-[22px] text-violet-700 dark:text-violet-300"
      >
        🤖
      </div>

      {/* Content */}
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <div className="font-mono text-[9px] font-extrabold uppercase tracking-widest text-muted-foreground">
          {labels.title}
        </div>
        {agentName !== null ? (
          <>
            <div className="truncate font-display text-[14px] font-extrabold text-foreground">
              {agentName}
            </div>
            {gameName !== null ? (
              <div className="truncate font-mono text-[10px] font-semibold text-muted-foreground">
                {gameName}
              </div>
            ) : null}
          </>
        ) : (
          <div className="text-sm text-muted-foreground">{labels.none}</div>
        )}
      </div>

      {/* Open button — only when agent exists and onClick provided */}
      {agentName !== null && onClick ? (
        <button
          type="button"
          aria-label={interpolateAriaLabel(labels.ariaLabel, agentName)}
          onClick={onClick}
          data-slot="player-detail-favorite-agent-cta"
          className={clsx(
            'flex-shrink-0 rounded-xl border border-border px-3 py-2',
            'font-display text-[11px] font-extrabold text-violet-700 dark:text-violet-300',
            'transition-colors hover:bg-violet-500/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
          )}
        >
          ↗
        </button>
      ) : null}
    </div>
  );
}
