'use client';

/**
 * YourAgents Component — Dashboard v2 "Il Tavolo"
 *
 * Grid of agent MeepleCards with a "Crea agente" CTA slot.
 */

import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { cn } from '@/lib/utils';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface AgentItem {
  id: string;
  name: string;
  imageUrl?: string;
  status: 'active' | 'idle' | 'training' | 'error';
  stats?: { invocationCount: number; lastExecutedAt?: string };
  gameTitle?: string;
}

export interface YourAgentsProps {
  agents: AgentItem[];
  loading?: boolean;
  className?: string;
  onCreateAgent?: () => void;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function YourAgents({ agents, loading, className, onCreateAgent }: YourAgentsProps) {
  return (
    <section data-testid="your-agents" className={cn('flex flex-col gap-3', className)}>
      {/* Section header */}
      <div className="flex items-center justify-between">
        <h2 className="font-quicksand font-bold text-base text-foreground">🤖 I Tuoi Agenti</h2>
        <Link
          href="/agents"
          className="text-xs font-semibold text-primary hover:underline"
          data-testid="agents-manage-link"
        >
          Gestisci →
        </Link>
      </div>

      {/* Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3.5">
        {loading ? (
          // Loading skeletons
          Array.from({ length: 3 }).map((_, i) => (
            <MeepleCard key={i} entity="agent" variant="grid" title="" loading={true} />
          ))
        ) : (
          <>
            {agents.map(agent => (
              <MeepleCard
                key={agent.id}
                entity="agent"
                variant="grid"
                id={agent.id}
                entityId={agent.id}
                title={agent.name}
                subtitle={agent.gameTitle}
                imageUrl={agent.imageUrl}
                agentStatus={agent.status}
                agentStats={agent.stats}
                showInfoButton
                loading={false}
              />
            ))}

            {/* CTA card */}
            <div
              className="flex flex-col items-center justify-center rounded-2xl border-[1.5px] border-dashed border-border bg-[rgba(255,255,255,0.4)] p-6 cursor-pointer hover:bg-[rgba(255,255,255,0.6)] transition-colors min-h-[200px]"
              onClick={onCreateAgent}
              data-testid="create-agent-cta"
            >
              <span className="text-2xl opacity-40 mb-2">➕</span>
              <span className="font-quicksand font-bold text-sm text-muted-foreground">
                Crea agente
              </span>
              <span className="text-xs text-muted-foreground">Configura nuovo</span>
            </div>
          </>
        )}
      </div>
    </section>
  );
}
