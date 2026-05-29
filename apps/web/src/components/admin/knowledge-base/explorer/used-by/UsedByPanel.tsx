/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer: rose error palette + zinc dark overrides (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import { useKbDocConsumingAgents } from '@/hooks/queries/useKbDocConsumingAgents';

import { UsedByAgentRow } from './UsedByAgentRow';
import { UsedByEmptyState } from './UsedByEmptyState';

interface UsedByPanelProps {
  readonly docId: string;
}

/**
 * Container of the "Used by" tab inside KbDocDetailPanel. Wires the React Query
 * hook to UsedByAgentRow + UsedByEmptyState. Read-only. Issue #1651.
 */
export function UsedByPanel({ docId }: UsedByPanelProps) {
  const query = useKbDocConsumingAgents({ docId });

  if (query.isLoading) {
    return (
      <div
        data-testid="used-by-panel-loading"
        className="border border-border/60 rounded-lg bg-card/80 p-6 animate-pulse min-h-[200px]"
      >
        <div className="h-4 w-1/2 bg-muted rounded mb-3" />
        <div className="h-4 w-1/3 bg-muted rounded mb-3" />
        <div className="h-4 w-2/3 bg-muted rounded" />
      </div>
    );
  }

  if (query.isError) {
    return (
      <div
        data-testid="used-by-panel-error"
        className="border border-rose-500/30 rounded-lg bg-rose-500/5 p-6 text-sm text-rose-700 dark:text-rose-300"
      >
        Errore caricamento agent consumatori: {query.error.message}
      </div>
    );
  }

  const agents = query.data ?? [];
  if (agents.length === 0) {
    return <UsedByEmptyState />;
  }

  return (
    <section
      data-testid="used-by-panel"
      className="border border-border/60 dark:border-zinc-700/60 rounded-lg bg-card/80 dark:bg-zinc-900/80 overflow-hidden"
    >
      <header className="p-4 border-b border-border/60 dark:border-zinc-700/60">
        <h3 className="font-quicksand font-bold text-sm">
          Agent che consumano questo documento ({agents.length})
        </h3>
      </header>
      <ul className="divide-y divide-border/60 dark:divide-zinc-700/60 px-4">
        {agents.map(a => (
          <UsedByAgentRow key={a.id} agent={a} />
        ))}
      </ul>
    </section>
  );
}
