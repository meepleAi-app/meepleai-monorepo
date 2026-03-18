/**
 * AgentsSidebar — Zone-based dashboard component
 *
 * Displays user's AI agents as compact cards in a sidebar (desktop)
 * or stacked section (mobile). Uses the agents API with 5min staleTime.
 */

'use client';

import { Bot } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { AgentStatus } from '@/components/ui/data-display/meeple-card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useAgents } from '@/hooks/queries/useAgents';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function mapAgentStatus(agent: AgentDto): AgentStatus {
  if (!agent.isActive) return 'idle';
  if (agent.isIdle) return 'idle';
  return 'active';
}

// ---------------------------------------------------------------------------
// Skeleton
// ---------------------------------------------------------------------------

function AgentsSidebarSkeleton() {
  return (
    <div data-testid="agents-sidebar-skeleton" className="space-y-3">
      {Array.from({ length: 3 }).map((_, i) => (
        <div key={i} className="flex items-center gap-3 p-3 rounded-xl border border-border">
          <Skeleton className="w-9 h-9 rounded-lg shrink-0" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-3 w-1/2" />
          </div>
        </div>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Empty state
// ---------------------------------------------------------------------------

function EmptyState() {
  return (
    <div
      data-testid="agents-sidebar-empty"
      className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-border bg-muted/30 p-6 text-center"
    >
      <span
        className="flex items-center justify-center w-10 h-10 rounded-lg bg-amber-100 dark:bg-amber-900/30"
        aria-hidden
      >
        <Bot className="h-5 w-5 text-amber-600 dark:text-amber-400" />
      </span>
      <div>
        <p className="text-sm font-medium text-foreground">Nessun agente configurato</p>
        <p className="text-xs text-muted-foreground mt-1">
          Crea il tuo primo agente AI per iniziare.
        </p>
      </div>
      <Link
        href="/agents"
        className="text-xs font-semibold text-amber-600 dark:text-amber-400 hover:underline"
      >
        Crea agente
      </Link>
    </div>
  );
}

// ---------------------------------------------------------------------------
// AgentsSidebar
// ---------------------------------------------------------------------------

export function AgentsSidebar() {
  const router = useRouter();
  const { data: agents = [], isLoading } = useAgents();

  return (
    <aside data-testid="agents-sidebar" className="w-full lg:w-[280px] shrink-0">
      {/* Section title */}
      <h3 className="font-quicksand text-sm font-bold text-foreground mb-3">I tuoi Agenti</h3>

      {/* Loading */}
      {isLoading && <AgentsSidebarSkeleton />}

      {/* Empty */}
      {!isLoading && agents.length === 0 && <EmptyState />}

      {/* Agent cards */}
      {!isLoading && agents.length > 0 && (
        <div className="flex flex-row gap-3 overflow-x-auto pb-2 lg:flex-col lg:overflow-x-visible lg:pb-0">
          {agents.map(agent => (
            <div key={agent.id} className="min-w-[220px] lg:min-w-0">
              <MeepleCard
                entity="agent"
                variant="compact"
                title={agent.name}
                subtitle={agent.type}
                agentStatus={mapAgentStatus(agent)}
                agentModel={{ modelName: agent.strategyName }}
                onClick={() => router.push(`/agents/${agent.id}`)}
              />
            </div>
          ))}
        </div>
      )}
    </aside>
  );
}
