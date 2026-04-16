'use client';

import { useState, useMemo } from 'react';

import Link from 'next/link';

import { HubLayout, type FilterChip } from '@/components/layout/HubLayout';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';

// ========== Filter chips ==========

const FILTERS: FilterChip[] = [
  { id: 'all', label: 'Tutte' },
  { id: 'active', label: 'Attive' },
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
      <span className="text-5xl">🎮</span>
      <p className="font-medium">Nessuna sessione trovata.</p>
      <p className="text-sm">Inizia una nuova sessione per giocare con i tuoi amici.</p>
    </div>
  );
}

// ========== Hub page ==========

export default function SessionsHubPage() {
  const [search, setSearch] = useState('');
  const [activeFilter, setActiveFilter] = useState('all');
  const [viewMode, setViewMode] = useState<'grid' | 'list' | 'carousel'>('list');

  // Fetch active sessions (limit 20)
  const { data, isLoading } = useActiveSessions(20);

  // Tabs/breadcrumb are now owned by `sessions/layout.tsx` via PageHeader.

  const items = useMemo(() => {
    const sessions = data?.sessions ?? [];
    const q = search.toLowerCase();
    return sessions
      .filter(s => {
        if (activeFilter === 'active') {
          return s.status === 'InProgress' || s.status === 'Setup' || s.status === 'Paused';
        }
        return true;
      })
      .filter(s => {
        if (!q) return true;
        // GameSessionDto has gameId but no gameName — use id as fallback title
        return String(s.id).toLowerCase().includes(q) || s.status.toLowerCase().includes(q);
      })
      .map(s => ({
        entity: 'session' as const,
        id: s.id,
        // GameSessionDto doesn't carry a display name; use status + playerCount as title
        title: `Sessione (${s.playerCount} ${s.playerCount === 1 ? 'giocatore' : 'giocatori'})`,
        subtitle: s.status,
        variant: 'list' as const,
      }));
  }, [data, search, activeFilter]);

  const topActions = (
    <Link
      href="/sessions/new"
      className="inline-flex items-center gap-1 h-9 px-3 text-sm font-semibold rounded-2xl bg-[var(--nh-text-primary,#1a1a1a)] text-white shrink-0"
    >
      ＋ Nuova
    </Link>
  );

  return (
    <HubLayout
      searchPlaceholder="Filtra per stato..."
      filterChips={FILTERS}
      activeFilterId={activeFilter}
      onFilterChange={setActiveFilter}
      searchValue={search}
      onSearchChange={setSearch}
      viewMode={viewMode}
      onViewModeChange={setViewMode}
      showViewToggle
      topActions={topActions}
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
