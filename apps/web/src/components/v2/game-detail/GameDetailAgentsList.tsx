/**
 * GameDetailAgentsList - v2 Wave C.1 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-game-detail.jsx` (chat tab agent header).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #573)
 *
 * Lists AI agents linked to this game (via `gameId`). Each row exposes name,
 * model, KB count, invocations and an "Open" link to `/agents/[id]` (Wave C.2).
 *
 * AC: T A V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';
import Link from 'next/link';

export interface GameDetailAgentEntry {
  readonly id: string;
  readonly name: string;
  readonly model: string | null;
  readonly kbCount: number;
  readonly invocations: number;
  readonly isActive: boolean;
}

export interface GameDetailAgentsListLabels {
  readonly title: string;
  readonly subtitle: string;
  readonly empty: string;
  readonly emptySubtitle: string;
  readonly createCta: string;
  readonly openAriaLabel: string;
  readonly indexedLabel: string;
  readonly invocationsLabel: string;
}

export interface GameDetailAgentsListProps {
  readonly agents: ReadonlyArray<GameDetailAgentEntry>;
  readonly labels: GameDetailAgentsListLabels;
  readonly onCreateAgent?: () => void;
  readonly className?: string;
}

export function GameDetailAgentsList(props: GameDetailAgentsListProps): ReactElement {
  const { agents, labels, onCreateAgent, className } = props;
  const isEmpty = agents.length === 0;

  return (
    <section
      data-slot="game-detail-agents-list"
      data-empty={isEmpty}
      className={clsx('flex flex-col gap-3', className)}
    >
      <header className="flex items-baseline justify-between gap-2">
        <div>
          <h3 className="font-display text-[15px] font-extrabold text-foreground">
            {labels.title}
          </h3>
          <p className="mt-0.5 text-[12px] text-muted-foreground">{labels.subtitle}</p>
        </div>
        {onCreateAgent ? (
          <button
            type="button"
            onClick={onCreateAgent}
            data-slot="game-detail-agents-create"
            className="rounded-md border-none bg-violet-700 px-3 py-1 font-display text-[11px] font-extrabold text-white shadow-sm transition-shadow hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {labels.createCta}
          </button>
        ) : null}
      </header>

      {isEmpty ? (
        <div
          data-slot="game-detail-agents-empty"
          className="flex flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-border bg-muted/30 px-6 py-10 text-center"
        >
          <span aria-hidden="true" className="text-3xl">
            🤖
          </span>
          <h4 className="font-display text-[14px] font-extrabold text-foreground">
            {labels.empty}
          </h4>
          <p className="max-w-sm text-[12px] text-muted-foreground">{labels.emptySubtitle}</p>
        </div>
      ) : (
        <ul role="list" className="flex flex-col gap-2">
          {agents.map(agent => (
            <li
              key={agent.id}
              data-slot="game-detail-agent-row"
              data-agent-id={agent.id}
              className="flex items-center gap-3 rounded-2xl border border-border bg-card p-3 shadow-sm transition-shadow hover:shadow-md"
            >
              <div
                aria-hidden="true"
                className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-md bg-violet-100 text-[20px] text-violet-700 dark:bg-violet-950/40 dark:text-violet-300"
              >
                🤖
              </div>
              <div className="flex min-w-0 flex-1 flex-col">
                <span className="truncate font-display text-[14px] font-extrabold text-foreground">
                  {agent.name}
                </span>
                <span className="truncate font-mono text-[11px] font-semibold text-muted-foreground">
                  {agent.model ? `${agent.model} · ` : ''}
                  {labels.indexedLabel.replace('{count}', String(agent.kbCount))}
                  {' · '}
                  {labels.invocationsLabel.replace('{count}', String(agent.invocations))}
                </span>
              </div>
              <span
                data-slot="game-detail-agent-status"
                data-active={agent.isActive}
                className={clsx(
                  'rounded-full px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase tracking-[0.06em]',
                  agent.isActive
                    ? 'bg-violet-100 text-violet-700 dark:bg-violet-950/40 dark:text-violet-300'
                    : 'bg-muted text-muted-foreground'
                )}
              >
                {agent.isActive ? '● Attivo' : '○ Inattivo'}
              </span>
              <Link
                href={`/agents/${agent.id}`}
                aria-label={labels.openAriaLabel.replace('{name}', agent.name)}
                data-slot="game-detail-agent-open"
                className="rounded-md border border-border px-2 py-1 font-display text-[11px] font-extrabold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                ↗
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
