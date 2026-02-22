/**
 * AgentsDashboardSection — Issue #5097, Epic #5094
 *
 * Dashboard section: 2 recent agents as compact cards + "Crea agente" CTA card.
 */

'use client';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { ArrowRight, Bot, Plus } from 'lucide-react';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useRecentAgents } from '@/hooks/queries/useRecentAgents';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

// ─── Section header ───────────────────────────────────────────────────────────

function SectionHeader() {
  return (
    <div className="flex items-center justify-between mb-3">
      <h3 className="font-quicksand text-sm font-bold text-foreground">
        🤖 I tuoi agenti
      </h3>
      <Link
        href="/agents"
        className="flex items-center gap-1 text-xs font-semibold font-nunito text-muted-foreground hover:text-foreground transition-colors"
      >
        Vedi tutti
        <ArrowRight className="h-3 w-3" />
      </Link>
    </div>
  );
}

// ─── Agent card ───────────────────────────────────────────────────────────────

function AgentCard({ agent }: { agent: AgentDto }) {
  const lastUsedLabel = agent.lastInvokedAt
    ? formatDistanceToNow(new Date(agent.lastInvokedAt), {
        addSuffix: true,
        locale: it,
      })
    : null;

  return (
    <Link
      href={`/agents/${agent.id}`}
      className="flex flex-col gap-2 p-4 rounded-xl border border-border bg-surface hover:bg-accent/40 transition-colors group"
    >
      {/* Icon + name */}
      <div className="flex items-start gap-2">
        <span
          className="flex items-center justify-center w-10 h-10 rounded-xl text-white text-lg shrink-0"
          style={{ background: 'linear-gradient(135deg, hsl(38,92%,50%), hsl(38,92%,32%))' }}
          aria-hidden
        >
          <Bot className="h-5 w-5" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="font-quicksand font-bold text-sm text-foreground truncate group-hover:text-[hsl(38,92%,40%)] transition-colors">
            {agent.name}
          </p>
          <p className="text-[10px] text-muted-foreground font-nunito truncate">
            {agent.type}
          </p>
        </div>
      </div>

      {/* Meta */}
      <div className="flex items-center justify-between">
        <span
          className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${
            agent.isActive
              ? 'bg-[hsl(38,92%,92%)] text-[hsl(38,92%,35%)] border border-[hsl(38,92%,50%)]'
              : 'bg-muted text-muted-foreground border border-border'
          }`}
        >
          {agent.isActive ? 'Attivo' : 'Inattivo'}
        </span>
        {lastUsedLabel && (
          <span className="text-[10px] text-muted-foreground font-nunito">
            {lastUsedLabel}
          </span>
        )}
      </div>
    </Link>
  );
}

// ─── CTA card ────────────────────────────────────────────────────────────────

function CreateAgentCard() {
  return (
    <Link
      href="/agents/new"
      className="flex flex-col items-center justify-center gap-3 p-4 rounded-xl border-2 border-dashed border-border hover:border-[hsl(38,92%,50%)] hover:bg-[hsl(38,92%,92%)] transition-colors group min-h-[100px]"
    >
      <div className="flex items-center justify-center w-10 h-10 rounded-xl bg-muted group-hover:bg-[hsl(38,92%,50%)] transition-colors">
        <Plus className="h-5 w-5 text-muted-foreground group-hover:text-white transition-colors" />
      </div>
      <p className="text-xs font-bold font-quicksand text-muted-foreground group-hover:text-[hsl(38,92%,35%)] transition-colors text-center">
        Crea nuovo agente
      </p>
    </Link>
  );
}

// ─── Skeleton ────────────────────────────────────────────────────────────────

function AgentCardSkeleton() {
  return (
    <div className="flex flex-col gap-2 p-4 rounded-xl border border-border">
      <div className="flex items-start gap-2">
        <Skeleton className="w-10 h-10 rounded-xl shrink-0" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-4 w-3/4" />
          <Skeleton className="h-3 w-1/2" />
        </div>
      </div>
      <Skeleton className="h-5 w-16" />
    </div>
  );
}

// ─── Public component ─────────────────────────────────────────────────────────

export function AgentsDashboardSection() {
  const { data: agents = [], isLoading } = useRecentAgents(2);

  return (
    <section>
      <SectionHeader />
      <div className="grid grid-cols-[repeat(auto-fill,minmax(200px,1fr))] gap-3">
        {isLoading ? (
          <>
            <AgentCardSkeleton />
            <AgentCardSkeleton />
          </>
        ) : (
          <>
            {agents.map((agent) => <AgentCard key={agent.id} agent={agent} />)}
            <CreateAgentCard />
          </>
        )}
      </div>
    </section>
  );
}
