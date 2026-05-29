'use client';

import Link from 'next/link';

import type { KbDocConsumingAgent } from '@/lib/api/schemas/kb-consuming-agents.schemas';

interface UsedByAgentRowProps {
  readonly agent: KbDocConsumingAgent;
}

function statusChipClass(status: KbDocConsumingAgent['status']): string {
  switch (status) {
    case 'Published':
      return 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/30';
    case 'Testing':
      return 'bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/30';
    default: // Draft
      return 'bg-muted text-muted-foreground border-border';
  }
}

function formatRelative(iso: string | null): string {
  if (iso === null) return 'mai';
  const date = new Date(iso);
  return date.toLocaleDateString('it-IT', { year: 'numeric', month: '2-digit', day: '2-digit' });
}

/**
 * Single row in the "Used by" tab. Read-only — links out to the agent detail
 * in the AI Lab. Issue #1651.
 */
export function UsedByAgentRow({ agent }: UsedByAgentRowProps) {
  const gameLabel = agent.gameName ?? 'KB globale';

  return (
    <li className="py-3" data-testid="used-by-agent-row">
      <Link
        href={`/admin/agents/definitions/${agent.id}`}
        className="block hover:bg-muted/40 rounded-md px-3 py-2 -mx-3 transition-colors"
      >
        <div className="flex items-center gap-2 flex-wrap">
          <span className="font-quicksand font-bold text-sm truncate">{agent.name}</span>
          {agent.isSystemDefined && (
            <span
              data-testid="used-by-system-badge"
              className="inline-flex items-center px-1.5 py-0.5 text-[9.5px] font-semibold rounded-full border bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/30 uppercase tracking-wider"
              title={agent.typologySlug ?? undefined}
            >
              sistema
            </span>
          )}
          <span
            className={`inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border ${statusChipClass(agent.status)}`}
          >
            {agent.status}
          </span>
          <span className="ml-auto text-[10.5px] font-mono text-muted-foreground">
            {agent.type}
          </span>
        </div>
        <div className="mt-1 flex items-center gap-3 text-[11.5px] text-muted-foreground font-mono">
          <span>{gameLabel}</span>
          <span aria-hidden="true">·</span>
          <span>
            {agent.invocationCount.toLocaleString('it-IT')} invocaz · ultimo{' '}
            {formatRelative(agent.lastInvokedAt)}
          </span>
        </div>
      </Link>
    </li>
  );
}
