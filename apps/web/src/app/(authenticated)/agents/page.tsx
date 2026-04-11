'use client';

import { useState, useMemo } from 'react';

import { HubLayout, type FilterChip } from '@/components/layout/HubLayout';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useAgents } from '@/hooks/queries/useAgents';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

// ========== Filter chips ==========

const FILTERS: FilterChip[] = [
  { id: 'all', label: 'Tutti' },
  { id: 'active', label: 'Attivi' },
];

// ========== Loading skeleton ==========

function LoadingSkeleton() {
  return (
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3 px-4">
      {Array.from({ length: 8 }).map((_, i) => (
        <Skeleton key={i} className="h-40 rounded-xl" />
      ))}
    </div>
  );
}

// ========== Empty state ==========

function EmptyState() {
  return (
    <div className="flex flex-col items-center gap-3 py-16 px-4 text-center text-[var(--nh-text-muted,#94a3b8)]">
      <span className="text-5xl">🤖</span>
      <p className="font-medium">Nessun agente trovato.</p>
      <p className="text-sm">Crea il tuo primo agente AI per assistere le partite.</p>
    </div>
  );
}

// ========== Hub page ==========

export default function AgentsHubPage() {
  const [search, setSearch] = useState('');
  const [activeFilter, setActiveFilter] = useState('all');
  const [viewMode, setViewMode] = useState<'grid' | 'list' | 'carousel'>('grid');

  const { data, isLoading } = useAgents({ activeOnly: false });

  useMiniNavConfig({
    breadcrumb: 'Agenti',
    tabs: [{ id: 'all', label: 'Agenti', href: '/agents' }],
    activeTabId: 'all',
  });

  const items = useMemo(() => {
    const agents = (data ?? []) as AgentDto[];
    const q = search.toLowerCase();
    return agents
      .filter(a => {
        if (activeFilter === 'active') return a.isActive === true;
        return true;
      })
      .filter(a => !q || a.name.toLowerCase().includes(q) || a.type.toLowerCase().includes(q))
      .map(a => ({
        entity: 'agent' as const,
        id: a.id,
        title: a.name,
        subtitle: a.type,
        variant: 'grid' as const,
      }));
  }, [data, search, activeFilter]);

  return (
    <HubLayout
      searchPlaceholder="Cerca agenti..."
      filterChips={FILTERS}
      activeFilterId={activeFilter}
      onFilterChange={setActiveFilter}
      searchValue={search}
      onSearchChange={setSearch}
      viewMode={viewMode}
      onViewModeChange={setViewMode}
      showViewToggle
    >
      {isLoading ? (
        <LoadingSkeleton />
      ) : items.length === 0 ? (
        <EmptyState />
      ) : (
        <div
          className={
            viewMode === 'list'
              ? 'flex flex-col gap-2 px-4 pb-24'
              : 'grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3 px-4 pb-24'
          }
        >
          {items.map(item => (
            <MeepleCard key={item.id} {...item} />
          ))}
        </div>
      )}
    </HubLayout>
  );
}
