/**
 * AgentsDashboardSection — Issue #5097, Epic #5094
 *
 * Dashboard section: 2 recent agents as compact cards + "Crea agente" CTA card.
 */

'use client';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { ArrowRight, Bot } from 'lucide-react';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useRecentAgents } from '@/hooks/queries/useRecentAgents';
import { IS_ALPHA_MODE } from '@/lib/alpha-mode';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

// ─── Section header ───────────────────────────────────────────────────────────

function SectionHeader() {
  return (
    <div className="flex items-center justify-between mb-3">
      <h3 className="font-quicksand text-sm font-bold text-foreground">🤖 I tuoi agenti</h3>
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
      className="flex items-center gap-3 p-3 rounded-xl border border-border bg-surface hover:bg-accent/40 transition-colors group"
    >
      {/* Icon */}
      <span
        className="flex items-center justify-center w-9 h-9 rounded-lg text-white shrink-0"
        style={{ background: 'linear-gradient(135deg, hsl(38,92%,50%), hsl(38,92%,32%))' }}
        aria-hidden
      >
        <Bot className="h-4 w-4" />
      </span>

      {/* Name + meta */}
      <div className="min-w-0 flex-1">
        <p className="font-quicksand font-bold text-sm text-foreground truncate group-hover:text-[hsl(38,92%,40%)] transition-colors">
          {agent.name}
        </p>
        <div className="flex items-center gap-2 mt-0.5">
          <span
            className={`px-1.5 py-0.5 rounded-full text-[10px] font-bold leading-none ${
              agent.isActive
                ? 'bg-[hsl(38,92%,92%)] text-[hsl(38,92%,35%)] border border-[hsl(38,92%,50%)]'
                : 'bg-muted text-muted-foreground border border-border'
            }`}
          >
            {agent.isActive ? 'Attivo' : 'Inattivo'}
          </span>
          {lastUsedLabel && (
            <span className="text-[10px] text-muted-foreground font-nunito truncate">
              {lastUsedLabel}
            </span>
          )}
        </div>
      </div>
    </Link>
  );
}

// ─── Skeleton ────────────────────────────────────────────────────────────────

function AgentCardSkeleton() {
  return (
    <div className="flex items-center gap-3 p-3 rounded-xl border border-border">
      <Skeleton className="w-9 h-9 rounded-lg shrink-0" />
      <div className="flex-1 space-y-2">
        <Skeleton className="h-4 w-3/4" />
        <Skeleton className="h-3 w-1/2" />
      </div>
    </div>
  );
}

// ─── Public component ─────────────────────────────────────────────────────────

export function AgentsDashboardSection() {
  const { data: agents = [], isLoading } = useRecentAgents(2, !IS_ALPHA_MODE);

  if (IS_ALPHA_MODE) return null;

  return (
    <section>
      <SectionHeader />
      <div className="space-y-2">
        {isLoading ? (
          <>
            <AgentCardSkeleton />
            <AgentCardSkeleton />
          </>
        ) : (
          <>
            {agents.map(agent => (
              <AgentCard key={agent.id} agent={agent} />
            ))}
          </>
        )}
      </div>
    </section>
  );
}
