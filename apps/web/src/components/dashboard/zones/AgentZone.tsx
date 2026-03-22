'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { AgentStatus } from '@/components/ui/data-display/meeple-card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useAgents } from '@/hooks/queries/useAgents';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

function mapAgentStatus(agent: AgentDto): AgentStatus {
  if (!agent.isActive) return 'idle';
  if (agent.isIdle) return 'idle';
  return 'active';
}

export function AgentZone() {
  const router = useRouter();
  const { data: agents = [], isLoading } = useAgents();

  if (isLoading) {
    return (
      <div data-testid="agent-zone-skeleton" className="flex gap-3 overflow-x-auto pb-2">
        {Array.from({ length: 3 }, (_, i) => (
          <div
            key={i}
            className="flex items-center gap-3 p-3 rounded-xl border border-border min-w-[220px] shrink-0"
          >
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

  return (
    <section data-testid="agent-zone">
      <h3 className="text-sm font-medium text-foreground/80 mb-2">Agenti AI</h3>
      {agents.length === 0 ? (
        <Link href="/chat/new">
          <MeepleCard
            entity="agent"
            variant="compact"
            title="Il tuo assistente AI e' pronto"
            subtitle="Chiedi aiuto con regole, strategie e altro"
            data-testid="agent-cta"
          />
        </Link>
      ) : (
        <div className="flex gap-3 overflow-x-auto pb-2">
          {agents.map(agent => (
            <div key={agent.id} className="shrink-0 min-w-[220px]">
              <MeepleCard
                entity="agent"
                variant="compact"
                title={agent.name}
                subtitle={agent.type}
                agentStatus={mapAgentStatus(agent)}
                agentModel={{ modelName: agent.strategyName }}
                onClick={() => router.push(`/agents/${agent.id}`)}
                data-testid={`agent-${agent.id}`}
              />
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
