'use client';

import { useState, useEffect } from 'react';

import { Bot } from 'lucide-react';
import Link from 'next/link';

import { AdminHubEmptyState } from '@/components/admin/layout/AdminHubEmptyState';
import { api } from '@/lib/api';

interface AgentSummary {
  id: string;
  name: string;
  status: string;
  totalChats: number;
}

export function AgentsTab() {
  const [agents, setAgents] = useState<AgentSummary[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.admin
      .getStats()
      .then(stats => {
        setAgents(
          (stats as Record<string, unknown>)?.agents
            ? ((stats as Record<string, unknown>).agents as AgentSummary[])
            : []
        );
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Agent Catalog
          </h2>
          <p className="text-sm text-muted-foreground mt-0.5">Browse and manage AI agents.</p>
        </div>
        <Link
          href="/admin/agents"
          className="inline-flex items-center justify-center gap-1.5 rounded-lg border border-border/60 px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground hover:border-primary/50 transition-colors self-start sm:self-auto"
        >
          Full Catalog
        </Link>
      </div>

      {loading ? (
        <div className="grid gap-2 sm:grid-cols-2">
          {[1, 2, 3, 4].map(i => (
            <div
              key={i}
              className="h-16 rounded-xl bg-white/40 dark:bg-zinc-800/40 animate-pulse"
            />
          ))}
        </div>
      ) : agents.length > 0 ? (
        <div className="grid gap-2 sm:grid-cols-2">
          {agents.map(agent => (
            <Link
              key={agent.id}
              href={`/admin/agents/definitions/${agent.id}`}
              className="flex items-center gap-3 rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-3 sm:p-4 hover:border-primary/30 hover:shadow-sm transition-all duration-200"
            >
              <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/10">
                <Bot className="h-4 w-4 text-primary" />
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-foreground truncate">{agent.name}</p>
                <p className="text-xs text-muted-foreground">
                  {agent.totalChats} chats · {agent.status}
                </p>
              </div>
            </Link>
          ))}
        </div>
      ) : (
        <AdminHubEmptyState
          icon={<Bot />}
          title="No agents found"
          description="Visit the full catalog to create your first AI agent."
          action={
            <Link
              href="/admin/agents"
              className="inline-flex items-center gap-1.5 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90 transition-colors"
            >
              Open Catalog
            </Link>
          }
        />
      )}
    </div>
  );
}
