'use client';

/**
 * RecentAgentsSidebar — Dashboard v2 "Il Tavolo"
 *
 * Compact sidebar section showing up to 3 recently used AI agents.
 */

import { cn } from '@/lib/utils';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface RecentAgentItem {
  id: string;
  name: string;
  lastUsedAt: string;
  isReady: boolean;
}

export interface RecentAgentsSidebarProps {
  agents: RecentAgentItem[];
  loading?: boolean;
  className?: string;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function relativeTime(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'ora';
  if (mins < 60) return `${mins} min fa`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h fa`;
  const days = Math.floor(hours / 24);
  if (days === 1) return 'ieri';
  return `${days} giorni fa`;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function RecentAgentsSidebar({ agents, loading, className }: RecentAgentsSidebarProps) {
  if (!loading && agents.length === 0) return null;

  return (
    <div data-testid="sidebar-agents" className={cn('', className)}>
      <p className="text-[11px] font-bold uppercase tracking-wide text-[hsl(38,92%,50%)] mb-2">
        ⚡ Agenti Recenti
      </p>

      {loading ? (
        <>
          <div className="h-[42px] rounded-[10px] bg-[rgba(200,180,160,0.20)] animate-pulse mb-1.5" />
          <div className="h-[42px] rounded-[10px] bg-[rgba(200,180,160,0.20)] animate-pulse mb-1.5" />
        </>
      ) : (
        agents.slice(0, 3).map(agent => (
          <div
            key={agent.id}
            className="flex gap-2 items-center p-2 rounded-[10px] bg-[rgba(255,255,255,0.5)] backdrop-blur-sm mb-1.5 cursor-pointer hover:bg-[rgba(255,255,255,0.75)] hover:shadow-[var(--shadow-warm-sm)] transition-all"
          >
            <div className="w-[30px] h-[30px] rounded-full bg-gradient-to-br from-[hsl(38,92%,50%,0.3)] to-[hsl(38,92%,50%,0.1)] flex items-center justify-center text-sm flex-shrink-0">
              ⚡
            </div>
            <div className="flex-1 min-w-0">
              <div className="text-xs font-semibold truncate">{agent.name}</div>
              <div className="text-[10px] text-muted-foreground">
                {relativeTime(agent.lastUsedAt)}
              </div>
            </div>
            <div
              className={cn(
                'w-2 h-2 rounded-full flex-shrink-0',
                agent.isReady ? 'bg-[hsl(142,76%,36%)]' : 'bg-muted'
              )}
            />
          </div>
        ))
      )}
    </div>
  );
}
