/**
 * HubAgentCard — authenticated catalog card for `/hub/agents` (#1166).
 * Cover gradient + entity chip + name + model + invocations + hover install button.
 */
'use client';

import Link from 'next/link';

import type { PopularAgent } from '@/lib/api/schemas/discover.schemas';

export interface HubAgentCardLabels {
  readonly installLabel: string;
  readonly invocationsTemplate: string;
}

export interface HubAgentCardProps {
  readonly agent: PopularAgent;
  readonly labels: HubAgentCardLabels;
  readonly onClick?: (id: string) => void;
  readonly onInstall?: (id: string) => void;
}

export function HubAgentCard({ agent, labels, onClick, onInstall }: HubAgentCardProps) {
  return (
    <Link
      href={`/agents/${agent.id}`}
      data-slot="hub-agent-card"
      onClick={() => onClick?.(agent.id)}
      className="group relative flex flex-col overflow-hidden rounded-xl border border-border bg-card transition-shadow hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-foreground/25"
    >
      <div
        aria-hidden="true"
        className="flex aspect-[5/3] items-center justify-center bg-gradient-to-br from-[hsl(var(--c-agent)/0.20)] to-[hsl(var(--c-agent)/0.05)] text-4xl"
      >
        🤖
        <span className="absolute right-2 top-2 inline-flex items-center gap-1 rounded-full bg-[hsl(var(--c-agent)/0.95)] px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase tracking-wider text-background">
          🤖 Agent
        </span>
      </div>
      <div className="flex flex-col gap-1 p-3">
        <h3 className="line-clamp-1 font-bold font-[Quicksand] text-sm text-foreground">
          {agent.name}
        </h3>
        <div className="font-mono text-[10px] text-muted-foreground">
          {agent.gameName && (
            <span className="line-clamp-1">
              🎲 {agent.gameName} ·{' '}
              {labels.invocationsTemplate.replace('{count}', String(agent.invocationCount))}
            </span>
          )}
          {!agent.gameName && (
            <span>
              {labels.invocationsTemplate.replace('{count}', String(agent.invocationCount))}
            </span>
          )}
        </div>
      </div>
      {/* Hover install button overlay */}
      <button
        type="button"
        data-slot="hub-card-install-button"
        onClick={event => {
          event.preventDefault();
          event.stopPropagation();
          onInstall?.(agent.id);
        }}
        className="absolute bottom-3 left-3 right-3 translate-y-2 rounded-lg bg-foreground py-1.5 font-bold font-[Quicksand] text-xs text-background opacity-0 transition-all group-hover:translate-y-0 group-hover:opacity-100 focus-visible:translate-y-0 focus-visible:opacity-100"
      >
        + {labels.installLabel}
      </button>
    </Link>
  );
}
