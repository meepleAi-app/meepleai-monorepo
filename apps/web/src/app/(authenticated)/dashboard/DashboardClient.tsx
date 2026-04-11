'use client';

import { useMemo, useState } from 'react';

import { useAuth } from '@/components/auth/AuthProvider';
import { HubLayout, type FilterChip } from '@/components/layout/HubLayout';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardProps, MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useAgents } from '@/hooks/queries/useAgents';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';

// ---------------------------------------------------------------------------
// Filter chip definitions
// ---------------------------------------------------------------------------

const GAMES_FILTERS: FilterChip[] = [
  { id: 'all', label: 'Tutti' },
  { id: 'recent', label: 'Recenti' },
];

const SESSIONS_FILTERS: FilterChip[] = [
  { id: 'all', label: 'Tutte' },
  { id: 'active', label: 'Attive' },
  { id: 'recent', label: 'Recenti' },
];

const AGENTS_FILTERS: FilterChip[] = [
  { id: 'all', label: 'Tutti' },
  { id: 'active', label: 'Attivi' },
];

// ---------------------------------------------------------------------------
// Internal sub-components (not exported, live in same file per spec)
// ---------------------------------------------------------------------------

function GreetingHeader({ displayName }: { displayName: string }) {
  return (
    <div className="pt-2 pb-1">
      <h2 className="font-[Quicksand] font-bold text-2xl text-[var(--nh-text-primary,#1a1a1a)]">
        Ciao, {displayName} 👋
      </h2>
      <p className="text-sm text-[var(--nh-text-secondary,#5a4a35)] mt-0.5">
        La tua tavola da gioco
      </p>
    </div>
  );
}

function HubBlock({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section>
      <h3
        className="font-[Quicksand] font-bold text-sm uppercase tracking-wide
                   text-[var(--nh-text-secondary,#5a4a35)] mb-2"
      >
        {title}
      </h3>
      {children}
    </section>
  );
}

function LoadingSkeleton({ count }: { count: number }) {
  return (
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="h-40 rounded-xl bg-black/5 animate-pulse" />
      ))}
    </div>
  );
}

function EmptyState({ entity }: { entity: MeepleEntityType }) {
  const messages: Partial<Record<MeepleEntityType, { icon: string; text: string }>> = {
    game: { icon: '🎲', text: 'Nessun gioco ancora. Aggiungi qualcosa alla libreria!' },
    session: { icon: '🎯', text: 'Nessuna sessione. Inizia una nuova partita!' },
    agent: { icon: '🤖', text: 'Nessun agente disponibile.' },
    toolkit: { icon: '🛠️', text: 'Nessun toolkit ancora.' },
    player: { icon: '👤', text: 'Nessun giocatore.' },
    kb: { icon: '📚', text: 'Nessuna knowledge base.' },
    chat: { icon: '💬', text: 'Nessuna chat.' },
    event: { icon: '📅', text: 'Nessun evento.' },
    tool: { icon: '🔧', text: 'Nessuno strumento.' },
  };
  const msg = messages[entity] ?? { icon: '📋', text: 'Nessun elemento.' };
  return (
    <div className="flex flex-col items-center gap-2 py-8 text-[var(--nh-text-muted,#94a3b8)]">
      <span className="text-3xl">{msg.icon}</span>
      <p className="text-sm font-medium">{msg.text}</p>
    </div>
  );
}

function MeepleCardGrid({
  entity,
  items,
  isLoading,
}: {
  entity: MeepleEntityType;
  items: MeepleCardProps[];
  isLoading: boolean;
}) {
  if (isLoading) return <LoadingSkeleton count={6} />;
  if (items.length === 0) return <EmptyState entity={entity} />;

  return (
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
      {items.map(item => (
        <MeepleCard key={item.id ?? item.title} {...item} />
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Main DashboardClient
// ---------------------------------------------------------------------------

export function DashboardClient() {
  const { user } = useAuth();
  const displayName = user?.displayName ?? 'giocatore';

  // Local state for search + filters + view modes per block
  const [gamesSearch, setGamesSearch] = useState('');
  const [gamesFilter, setGamesFilter] = useState('all');
  const [gamesViewMode, setGamesViewMode] = useState<'grid' | 'list' | 'carousel'>('grid');

  const [sessionsSearch, setSessionsSearch] = useState('');
  const [sessionsFilter, setSessionsFilter] = useState('all');
  const [sessionsViewMode, setSessionsViewMode] = useState<'grid' | 'list' | 'carousel'>('grid');

  const [agentsSearch, setAgentsSearch] = useState('');
  const [agentsFilter, setAgentsFilter] = useState('all');
  const [agentsViewMode, setAgentsViewMode] = useState<'grid' | 'list' | 'carousel'>('grid');

  // Mini-nav config (breadcrumb only, single tab)
  const miniNavConfig = useMemo(
    () => ({
      breadcrumb: 'Home',
      tabs: [{ id: 'overview', label: 'Overview', href: '/dashboard' }],
      activeTabId: 'overview',
    }),
    []
  );
  useMiniNavConfig(miniNavConfig);

  // ---------------------------------------------------------------------------
  // Data fetching
  // ---------------------------------------------------------------------------

  const { data: libraryData, isLoading: libraryLoading } = useLibrary({ page: 1, pageSize: 12 });
  const { data: sessionsData, isLoading: sessionsLoading } = useActiveSessions();
  const { data: agentsData, isLoading: agentsLoading } = useAgents({ activeOnly: false });

  // ---------------------------------------------------------------------------
  // Data mapping — library entries to MeepleCardProps
  // ---------------------------------------------------------------------------

  const gameItems: MeepleCardProps[] = useMemo(() => {
    const entries = libraryData?.items ?? [];
    return entries.map(entry => ({
      id: entry.id,
      entity: 'game' as MeepleEntityType,
      title: entry.gameTitle,
      subtitle: entry.gamePublisher ?? undefined,
      imageUrl: entry.gameImageUrl ?? undefined,
      rating: entry.averageRating ?? undefined,
      variant: gamesViewMode === 'list' ? 'list' : 'grid',
    }));
  }, [libraryData, gamesViewMode]);

  const filteredGameItems = useMemo(() => {
    let result = gameItems;
    if (gamesSearch.trim()) {
      const q = gamesSearch.toLowerCase();
      result = result.filter(
        g => g.title.toLowerCase().includes(q) || g.subtitle?.toLowerCase().includes(q)
      );
    }
    if (gamesFilter === 'wishlist') {
      // Wishlist filter: re-fetch with state param would be ideal; for now show all
      // TODO: pass currentState filter to useLibrary when gamesFilter === 'wishlist'
    }
    return result;
  }, [gameItems, gamesSearch, gamesFilter]);

  // ---------------------------------------------------------------------------
  // Data mapping — sessions to MeepleCardProps
  // ---------------------------------------------------------------------------

  const sessionItems: MeepleCardProps[] = useMemo(() => {
    const sessions = sessionsData?.sessions ?? [];
    return sessions.map(session => ({
      id: session.id,
      entity: 'session' as MeepleEntityType,
      title: `Sessione ${session.id.slice(0, 8)}`,
      subtitle: session.status,
      variant: sessionsViewMode === 'list' ? 'list' : 'grid',
      status:
        session.status.toLowerCase() === 'inprogress'
          ? ('inprogress' as const)
          : session.status.toLowerCase() === 'paused'
            ? ('paused' as const)
            : session.status.toLowerCase() === 'setup'
              ? ('setup' as const)
              : undefined,
    }));
  }, [sessionsData, sessionsViewMode]);

  const filteredSessionItems = useMemo(() => {
    let result = sessionItems;
    if (sessionsSearch.trim()) {
      const q = sessionsSearch.toLowerCase();
      result = result.filter(s => s.title.toLowerCase().includes(q));
    }
    if (sessionsFilter === 'active') {
      result = result.filter(
        s => s.status === 'inprogress' || s.status === 'active' || s.status === 'setup'
      );
    }
    return result;
  }, [sessionItems, sessionsSearch, sessionsFilter]);

  // ---------------------------------------------------------------------------
  // Data mapping — agents to MeepleCardProps
  // ---------------------------------------------------------------------------

  const agentItems: MeepleCardProps[] = useMemo(() => {
    // useAgents returns AgentDto[] directly
    const agents: Array<{ id: string; name: string; type: string; isActive: boolean }> =
      Array.isArray(agentsData) ? agentsData : [];
    return agents.map(agent => ({
      id: agent.id,
      entity: 'agent' as MeepleEntityType,
      title: agent.name,
      subtitle: agent.type,
      variant: agentsViewMode === 'list' ? 'list' : 'grid',
      status: agent.isActive ? ('active' as const) : ('idle' as const),
    }));
  }, [agentsData, agentsViewMode]);

  const filteredAgentItems = useMemo(() => {
    let result = agentItems;
    if (agentsSearch.trim()) {
      const q = agentsSearch.toLowerCase();
      result = result.filter(
        a => a.title.toLowerCase().includes(q) || a.subtitle?.toLowerCase().includes(q)
      );
    }
    if (agentsFilter === 'active') {
      result = result.filter(a => a.status === 'active');
    }
    return result;
  }, [agentItems, agentsSearch, agentsFilter]);

  // ---------------------------------------------------------------------------
  // Render
  // ---------------------------------------------------------------------------

  return (
    <div className="flex flex-col gap-6 px-4 pb-24 pt-4 max-w-[1440px] mx-auto">
      {/* Greeting */}
      <GreetingHeader displayName={displayName} />

      {/* Block 1: Games */}
      <HubBlock title="🎲 Giochi">
        <HubLayout
          searchPlaceholder="Cerca giochi..."
          searchValue={gamesSearch}
          onSearchChange={setGamesSearch}
          filterChips={GAMES_FILTERS}
          activeFilterId={gamesFilter}
          onFilterChange={setGamesFilter}
          viewMode={gamesViewMode}
          onViewModeChange={setGamesViewMode}
          showViewToggle
        >
          <MeepleCardGrid entity="game" items={filteredGameItems} isLoading={libraryLoading} />
        </HubLayout>
      </HubBlock>

      {/* Block 2: Sessions */}
      <HubBlock title="🎯 Sessioni">
        <HubLayout
          searchPlaceholder="Cerca sessioni..."
          searchValue={sessionsSearch}
          onSearchChange={setSessionsSearch}
          filterChips={SESSIONS_FILTERS}
          activeFilterId={sessionsFilter}
          onFilterChange={setSessionsFilter}
          viewMode={sessionsViewMode}
          onViewModeChange={setSessionsViewMode}
          showViewToggle
        >
          <MeepleCardGrid
            entity="session"
            items={filteredSessionItems}
            isLoading={sessionsLoading}
          />
        </HubLayout>
      </HubBlock>

      {/* Block 3: Agents */}
      <HubBlock title="🤖 Agenti">
        <HubLayout
          searchPlaceholder="Cerca agenti..."
          searchValue={agentsSearch}
          onSearchChange={setAgentsSearch}
          filterChips={AGENTS_FILTERS}
          activeFilterId={agentsFilter}
          onFilterChange={setAgentsFilter}
          viewMode={agentsViewMode}
          onViewModeChange={setAgentsViewMode}
          showViewToggle
        >
          <MeepleCardGrid entity="agent" items={filteredAgentItems} isLoading={agentsLoading} />
        </HubLayout>
      </HubBlock>

      {/* Block 4: Toolkit (placeholder) */}
      <HubBlock title="🛠️ Toolkit">
        <HubLayout searchPlaceholder="Cerca toolkit...">
          <MeepleCardGrid entity="toolkit" items={[]} isLoading={false} />
        </HubLayout>
      </HubBlock>
    </div>
  );
}
