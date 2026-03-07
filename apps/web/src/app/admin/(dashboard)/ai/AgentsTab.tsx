'use client';

import { useState, useEffect } from 'react';

import { Bot, ExternalLink } from 'lucide-react';
import Link from 'next/link';

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
        // Extract agent summary data from admin stats
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
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Agent Catalog
          </h2>
          <p className="text-sm text-muted-foreground mt-1">Browse and manage AI agents.</p>
        </div>
        <Link
          href="/admin/agents"
          className="inline-flex items-center gap-1.5 rounded-md border border-border/60 px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground hover:border-primary/50 transition-colors"
        >
          Full Catalog <ExternalLink className="h-3.5 w-3.5" />
        </Link>
      </div>

      {loading ? (
        <div className="grid gap-3">
          {[1, 2, 3].map(i => (
            <div
              key={i}
              className="h-16 rounded-xl bg-white/40 dark:bg-zinc-800/40 animate-pulse"
            />
          ))}
        </div>
      ) : agents.length > 0 ? (
        <div className="grid gap-3">
          {agents.map(agent => (
            <Link
              key={agent.id}
              href={`/admin/agents/definitions/${agent.id}`}
              className="flex items-center gap-4 rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-4 hover:border-primary/40 transition-colors"
            >
              <Bot className="h-5 w-5 text-primary" />
              <div className="flex-1 min-w-0">
                <p className="font-medium text-foreground truncate">{agent.name}</p>
                <p className="text-xs text-muted-foreground">
                  {agent.totalChats} chats &middot; {agent.status}
                </p>
              </div>
            </Link>
          ))}
        </div>
      ) : (
        <div className="rounded-xl border border-dashed border-border/60 p-12 text-center">
          <Bot className="mx-auto h-8 w-8 text-muted-foreground/50" />
          <p className="mt-3 text-sm text-muted-foreground">
            No agents found. Visit the{' '}
            <Link href="/admin/agents" className="text-primary hover:underline">
              full catalog
            </Link>{' '}
            to create one.
          </p>
        </div>
      )}
    </div>
  );
}
