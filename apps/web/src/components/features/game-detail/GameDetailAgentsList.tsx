/**
 * GameDetailAgentsList - v2 Wave C.1 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-game-detail.jsx` (chat tab agent header).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #573)
 *
 * Phase 0.5 contract sez. 4.3: uses a discriminated union `AgentsState` to prevent
 * `data + loading` co-occurrence (FSM cells 6/7/8/9).
 *
 * Orchestrator passes `state` derived from `agentsQuery` — component is PURE,
 * no hooks called inside.
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
  readonly loadingLabel: string;
  readonly errorLabel: string;
  readonly errorSubtitle: string;
  readonly retryLabel: string;
  readonly empty: string;
  readonly emptySubtitle: string;
  readonly createCta: string;
  readonly openAriaLabel: string;
  readonly indexedLabel: string;
  readonly invocationsLabel: string;
}

/**
 * Discriminated union per Phase 0.5 contract sez. 4.3.
 * Prevents `data + loading` co-occurrence.
 */
export type AgentsState =
  | { kind: 'loading' }
  | { kind: 'error'; retry: () => void }
  | { kind: 'empty'; ctaCreate: () => void }
  | { kind: 'success'; agents: ReadonlyArray<GameDetailAgentEntry> };

export interface GameDetailAgentsListProps {
  readonly state: AgentsState;
  readonly labels: GameDetailAgentsListLabels;
  readonly className?: string;
}

export function GameDetailAgentsList(props: GameDetailAgentsListProps): ReactElement {
  const { state, labels, className } = props;

  return (
    <section
      data-slot="game-detail-agents-list"
      data-agents-kind={state.kind}
      className={clsx('flex flex-col gap-3', className)}
    >
      <header className="flex items-baseline justify-between gap-2">
        <div>
          <h3 className="font-display text-[15px] font-extrabold text-foreground">
            {labels.title}
          </h3>
          <p className="mt-0.5 text-[12px] text-muted-foreground">{labels.subtitle}</p>
        </div>
        {state.kind === 'empty' ? (
          <button
            type="button"
            onClick={state.ctaCreate}
            data-slot="game-detail-agents-create"
            className="rounded-md border-none bg-violet-700 px-3 py-1 font-display text-[11px] font-extrabold text-white shadow-sm transition-shadow hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {labels.createCta}
          </button>
        ) : null}
      </header>

      {state.kind === 'loading' ? (
        <div
          data-slot="game-detail-agents-loading"
          className="flex flex-col gap-2"
          aria-busy="true"
          aria-label={labels.loadingLabel}
        >
          {[1, 2, 3].map(i => (
            <div key={i} className="h-16 animate-pulse rounded-2xl bg-muted" aria-hidden="true" />
          ))}
        </div>
      ) : state.kind === 'error' ? (
        <div
          data-slot="game-detail-agents-error"
          className="flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-rose-300 bg-rose-50/50 px-6 py-8 text-center dark:border-rose-700/40 dark:bg-rose-950/10"
          role="alert"
        >
          <span aria-hidden="true" className="text-2xl">
            ⚠️
          </span>
          <p className="font-display text-[14px] font-bold text-rose-700 dark:text-rose-400">
            {labels.errorLabel}
          </p>
          <p className="text-[12px] text-muted-foreground">{labels.errorSubtitle}</p>
          <button
            type="button"
            onClick={state.retry}
            data-slot="game-detail-agents-retry"
            className="rounded-md border border-rose-300 bg-transparent px-4 py-2 font-display text-[12px] font-bold text-rose-700 transition-colors hover:bg-rose-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-rose-400 dark:border-rose-700/50 dark:text-rose-400 dark:hover:bg-rose-950/20"
          >
            {labels.retryLabel}
          </button>
        </div>
      ) : state.kind === 'empty' ? (
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
        // state.kind === 'success'
        <ul role="list" className="flex flex-col gap-2">
          {state.agents.map(agent => (
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
