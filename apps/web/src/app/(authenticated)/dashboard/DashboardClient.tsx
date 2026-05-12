'use client';

import { useEffect, useMemo, useState } from 'react';

import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { DashboardHero } from '@/components/dashboard/DashboardHero';
import { DashboardStatsRow } from '@/components/dashboard/DashboardStatsRow';
import { EmptyCTA } from '@/components/dashboard/EmptyCTA';
import { EntityZone } from '@/components/dashboard/EntityZone';
import { OwnershipConfirmDialog } from '@/components/dialogs/OwnershipConfirmDialog';
import { HubLayout, type FilterChip } from '@/components/layout/HubLayout';
import { DiscoverCarousel } from '@/components/ui/data-display/discover-carousel';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardProps, MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import type { ManaPip } from '@/components/ui/data-display/meeple-card/parts/ManaPips';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useAgents } from '@/hooks/queries/useAgents';
import { useBatchGameStatus } from '@/hooks/queries/useBatchGameStatus';
import { useGames } from '@/hooks/queries/useGames';
import { useUpcomingGameNights } from '@/hooks/queries/useGameNights';
import {
  useAddGameToLibrary,
  useLibrary,
  useLibraryStats,
} from '@/hooks/queries/useLibrary';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import { useRecentsStore } from '@/stores/use-recents';

// ---------------------------------------------------------------------------
// Filter chip definitions
// ---------------------------------------------------------------------------

const GAMES_FILTERS: FilterChip[] = [
  { id: 'all', label: 'Tutti' },
  { id: 'recent', label: 'Recenti' },
];

// Catalog view (new-user) has no server-side sort by date — only "Tutti" makes sense
const CATALOG_FILTERS: FilterChip[] = [{ id: 'all', label: 'Tutti' }];

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
// Toolkit tools (static — no API required)
// ---------------------------------------------------------------------------

const TOOLKIT_TOOLS = [
  { id: 'dice', icon: '🎲', name: 'Dado', desc: 'Lancia d4–d20' },
  { id: 'timer', icon: '⏳', name: 'Clessidra', desc: 'Timer per turno' },
  { id: 'score', icon: '📊', name: 'Scoreboard', desc: 'Punteggi multi-player' },
  { id: 'token', icon: '🪙', name: 'Token', desc: 'Contatori risorse' },
] as const;

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

function LoadingSkeleton({ count }: { count: number }) {
  return (
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="h-40 rounded-xl bg-muted animate-pulse" />
      ))}
    </div>
  );
}

function MeepleCardGrid({
  items,
  isLoading,
  emptyNode,
}: {
  items: MeepleCardProps[];
  isLoading: boolean;
  emptyNode?: React.ReactNode;
}) {
  if (isLoading) return <LoadingSkeleton count={6} />;
  if (items.length === 0) {
    if (emptyNode) return <>{emptyNode}</>;
    return (
      <div className="flex flex-col items-center gap-2 py-8 text-muted-foreground/60">
        <p className="text-sm font-medium">Nessun elemento.</p>
      </div>
    );
  }
  return (
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
      {items.map(item => (
        <MeepleCard key={item.id ?? item.title} {...item} />
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Catalog games for new user — with "Aggiungi" button overlay
// ---------------------------------------------------------------------------

function CatalogGameCard({
  game,
  inLibrary,
  onAdd,
  adding,
  hasKb,
}: {
  game: {
    id: string;
    title: string;
    publisher?: string | null;
    imageUrl?: string | null;
    averageRating?: number | null;
  };
  inLibrary: boolean;
  onAdd: (gameId: string) => void;
  adding: boolean;
  hasKb?: boolean;
}) {
  return (
    <div className="e-game bg-card border border-border rounded-xl shadow-sm overflow-hidden flex flex-col">
      {/* Thumbnail */}
      <div className="relative h-[68px] flex items-center justify-center bg-[hsl(var(--c-game)/0.08)] dark:bg-[hsl(var(--c-game)/0.15)] overflow-hidden flex-shrink-0">
        {game.imageUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={game.imageUrl} alt={game.title} className="w-full h-full object-cover" />
        ) : (
          <span className="text-3xl select-none">🎲</span>
        )}
        {hasKb !== undefined && (
          <span
            className={`absolute top-1 right-1 px-1.5 py-0.5 rounded text-[8px] font-extrabold font-[Quicksand] leading-none ${hasKb ? 'bg-[hsl(var(--c-success))] text-white' : 'bg-muted-foreground/60 text-white'}`}
          >
            {hasKb ? 'KB ✓' : 'KB –'}
          </span>
        )}
      </div>

      {/* Info */}
      <div className="px-2 pt-1.5 pb-1 flex-1 min-h-0">
        <p
          className="font-[Quicksand] font-bold text-[11px] leading-tight
                     overflow-hidden line-clamp-2 text-foreground"
        >
          {game.title}
        </p>
        {game.publisher && (
          <p className="text-[10px] text-muted-foreground mt-0.5 truncate">{game.publisher}</p>
        )}
      </div>

      {/* Add button */}
      <button
        type="button"
        onClick={() => !inLibrary && !adding && onAdd(game.id)}
        disabled={inLibrary || adding}
        aria-label={
          inLibrary ? `${game.title} già in libreria` : `Aggiungi ${game.title} alla libreria`
        }
        className={
          inLibrary
            ? 'mx-1.5 mb-1.5 h-[22px] rounded-lg text-[10px] font-bold font-[Quicksand] flex items-center justify-center gap-1 bg-muted text-muted-foreground/60 cursor-default'
            : adding
              ? 'mx-1.5 mb-1.5 h-[22px] rounded-lg text-[10px] font-bold font-[Quicksand] flex items-center justify-center gap-1 bg-muted text-muted-foreground cursor-wait'
              : 'mx-1.5 mb-1.5 h-[22px] rounded-lg text-[10px] font-bold font-[Quicksand] flex items-center justify-center gap-1 bg-[hsl(var(--c-game))] text-white hover:opacity-90 active:scale-95 transition-transform'
        }
      >
        <span className="text-xs leading-none">＋</span>
        {inLibrary ? 'In libreria' : adding ? '…' : 'Aggiungi'}
      </button>
    </div>
  );
}

function NewUserGamesBlock({
  search,
  onSearchChange,
  filter,
  onFilterChange,
}: {
  search: string;
  onSearchChange: (v: string) => void;
  filter: string;
  onFilterChange: (v: string) => void;
}) {
  const { data: catalogData, isLoading } = useGames(undefined, undefined, 1, 20);
  // Sort client-side by rating descending, take top 12
  const games = useMemo(
    () =>
      [...(catalogData?.games ?? [])]
        .sort((a, b) => {
          // KB completeness first (spec annotation 1: "ordinati per KB completeness")
          const aKb = a.hasKnowledgeBase ? 1 : 0;
          const bKb = b.hasKnowledgeBase ? 1 : 0;
          if (bKb !== aKb) return bKb - aKb;
          return (b.averageRating ?? 0) - (a.averageRating ?? 0);
        })
        .slice(0, 12),
    [catalogData]
  );

  const gameIds = useMemo(() => games.map(g => g.id), [games]);
  const { data: batchStatus } = useBatchGameStatus(gameIds, gameIds.length > 0);

  const addMutation = useAddGameToLibrary();
  const [addingIds, setAddingIds] = useState<Set<string>>(new Set());
  const [confirmGame, setConfirmGame] = useState<{ id: string; title: string } | null>(null);
  const router = useRouter();

  const handleAdd = (gameId: string) => {
    const game = games.find(g => g.id === gameId);
    if (!game) return;
    setConfirmGame({ id: game.id, title: game.title });
  };

  const handleConfirmOwnership = () => {
    if (!confirmGame) return;
    setAddingIds(prev => new Set(prev).add(confirmGame.id));
    addMutation.mutate(
      { gameId: confirmGame.id },
      {
        onSuccess: () => {
          setAddingIds(prev => {
            const next = new Set(prev);
            next.delete(confirmGame.id);
            return next;
          });
          setConfirmGame(null);
          router.push(`/library/${confirmGame.id}`);
        },
        onError: () => {
          setAddingIds(prev => {
            const next = new Set(prev);
            next.delete(confirmGame.id);
            return next;
          });
        },
      }
    );
  };

  const filtered = useMemo(() => {
    if (!search.trim()) return games;
    const q = search.toLowerCase();
    return games.filter(
      g => g.title.toLowerCase().includes(q) || g.publisher?.toLowerCase().includes(q)
    );
  }, [games, search]);

  return (
    <HubLayout
      searchPlaceholder="Cerca giochi..."
      searchValue={search}
      onSearchChange={onSearchChange}
      filterChips={CATALOG_FILTERS}
      activeFilterId={filter}
      onFilterChange={onFilterChange}
    >
      {isLoading ? (
        <LoadingSkeleton count={6} />
      ) : (
        <>
          <p className="e-game text-xs text-muted-foreground/80 bg-[hsl(var(--c-game)/0.06)] border border-[hsl(var(--c-game)/0.25)] rounded-lg px-3 py-2 mb-3 font-medium">
            💡 Libreria vuota — ecco i top giochi dal catalogo. Aggiungili per iniziare!
          </p>
          <div className="grid grid-cols-2 gap-2">
            {filtered.map(game => {
              const status = batchStatus?.results?.[game.id];
              const inLibrary = status?.inLibrary ?? false;
              return (
                <CatalogGameCard
                  key={game.id}
                  game={game}
                  inLibrary={inLibrary}
                  onAdd={handleAdd}
                  adding={addingIds.has(game.id)}
                  hasKb={game.hasKnowledgeBase}
                />
              );
            })}
          </div>
        </>
      )}
      <OwnershipConfirmDialog
        open={!!confirmGame}
        onOpenChange={open => {
          if (!open) setConfirmGame(null);
        }}
        gameTitle={confirmGame?.title ?? ''}
        onConfirm={handleConfirmOwnership}
        confirming={addMutation.isPending}
      />
    </HubLayout>
  );
}

function ToolkitGrid() {
  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
      {TOOLKIT_TOOLS.map(tool => (
        <Link
          key={tool.id}
          href={`/toolkit?tool=${tool.id}`}
          className="e-toolkit flex flex-col items-center gap-1.5 rounded-xl border border-[hsl(var(--c-toolkit)/0.25)] bg-[hsl(var(--c-toolkit)/0.06)] p-4 text-center transition-all hover:-translate-y-0.5 hover:border-[hsl(var(--c-toolkit)/0.6)] hover:shadow-md"
        >
          <span className="text-[28px]">{tool.icon}</span>
          <span className="font-quicksand text-[13px] font-bold text-[hsl(var(--c-toolkit))]">
            {tool.name}
          </span>
          <span className="text-[11px] text-muted-foreground">{tool.desc}</span>
        </Link>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Main DashboardClient
// ---------------------------------------------------------------------------

export function DashboardClient() {
  const { user } = useAuth();
  const router = useRouter();
  const displayName = user?.displayName ?? 'giocatore';

  const [gamesSearch, setGamesSearch] = useState('');
  const [gamesFilter, setGamesFilter] = useState('all');

  const [sessionsSearch, setSessionsSearch] = useState('');
  const [sessionsFilter, setSessionsFilter] = useState('all');

  const [agentsSearch, setAgentsSearch] = useState('');
  const [agentsFilter, setAgentsFilter] = useState('all');

  useMiniNavConfig(
    useMemo(
      () => ({
        breadcrumb: 'Home',
        tabs: [{ id: 'overview', label: 'Overview', href: '/dashboard' }],
        activeTabId: 'overview',
      }),
      []
    )
  );

  // Register this page in recents for cross-page context memory
  useEffect(() => {
    useRecentsStore.getState().push({
      id: 'section-dashboard',
      entity: 'game',
      title: 'Home',
      href: '/dashboard',
    });
  }, []);

  // Data fetching
  const { data: libraryData, isLoading: libraryLoading } = useLibrary({ page: 1, pageSize: 12 });
  const {
    data: libraryStats,
    isLoading: statsLoading,
    isError: statsError,
    isFetching: statsFetching,
    refetch: refetchStats,
  } = useLibraryStats();
  const {
    data: sessionsData,
    isLoading: sessionsLoading,
    isError: sessionsError,
    isFetching: sessionsFetching,
    refetch: refetchSessions,
  } = useActiveSessions();
  const {
    data: agentsData,
    isLoading: agentsLoading,
    isError: agentsError,
    isFetching: agentsFetching,
    refetch: refetchAgents,
  } = useAgents({ activeOnly: false });
  const {
    data: upcomingNights,
    isLoading: upcomingLoading,
    isError: upcomingError,
    isFetching: upcomingFetching,
    refetch: refetchUpcoming,
  } = useUpcomingGameNights({ retry: 1 });

  // Detect new user: library loaded with no items
  const isNewUser = !libraryLoading && (libraryData?.items ?? []).length === 0;

  // ---------------------------------------------------------------------------
  // Library → MeepleCardProps
  // ---------------------------------------------------------------------------

  const gameItems: MeepleCardProps[] = useMemo(() => {
    const entries = libraryData?.items ?? [];
    return entries.map(entry => {
      const gameId = entry.gameId;
      const manaPips: ManaPip[] = [
        {
          entityType: 'session',
          count: 0,
          onCreate: () =>
            router.push(
              `/sessions/new?gameId=${gameId}&gameName=${encodeURIComponent(entry.gameTitle)}`
            ),
          createLabel: 'Nuova sessione',
        },
        {
          entityType: 'kb',
          count: 0,
          onCreate: () => router.push(`/library/${gameId}`),
          createLabel: 'Carica documento',
        },
        {
          entityType: 'agent',
          count: 0,
          onCreate: () => router.push(`/chat?gameId=${gameId}`),
          createLabel: 'Crea agente',
        },
      ];
      return {
        // MeepleCard.id must be the canonical gameId (used by ENTITY_NAVIGATION_GRAPH
        // to build /library/${id}?tab=agent hrefs). Using entry.id here breaks
        // navigation because /library expects the gameId, not the library entry pk.
        id: gameId,
        entity: 'game' as MeepleEntityType,
        title: entry.gameTitle,
        subtitle: entry.gamePublisher ?? undefined,
        imageUrl: entry.gameImageUrl ?? undefined,
        rating: entry.averageRating ?? undefined,
        variant: 'grid',
        manaPips,
      };
    });
  }, [libraryData, router]);

  const filteredGameItems = useMemo(() => {
    let result = gameItems;
    if (gamesSearch.trim()) {
      const q = gamesSearch.toLowerCase();
      result = result.filter(
        g => g.title.toLowerCase().includes(q) || g.subtitle?.toLowerCase().includes(q)
      );
    }
    return result;
  }, [gameItems, gamesSearch]);

  // ---------------------------------------------------------------------------
  // Sessions → MeepleCardProps
  // ---------------------------------------------------------------------------

  const sessionItems: MeepleCardProps[] = useMemo(() => {
    const sessions = sessionsData?.sessions ?? [];
    return sessions.map(session => {
      const manaPips: ManaPip[] = [
        {
          entityType: 'game',
          count: 1,
          items: [
            {
              id: session.gameId,
              label: session.gameId.slice(0, 8),
              href: `/library/${session.gameId}`,
            },
          ],
        },
        {
          entityType: 'player',
          count: session.playerCount ?? 0,
          items: (session.players ?? []).map(p => ({
            id: p.playerName,
            label: p.playerName,
            href: `/sessions/${session.id}`,
          })),
        },
      ];
      return {
        id: session.id,
        entity: 'session' as MeepleEntityType,
        title: `Sessione ${session.id.slice(0, 8)}`,
        subtitle: session.status,
        variant: 'grid',
        manaPips,
        status:
          session.status.toLowerCase() === 'inprogress'
            ? ('inprogress' as const)
            : session.status.toLowerCase() === 'paused'
              ? ('paused' as const)
              : session.status.toLowerCase() === 'setup'
                ? ('setup' as const)
                : undefined,
      };
    });
  }, [sessionsData]);

  const filteredSessionItems = useMemo(() => {
    let result = sessionItems;
    if (sessionsSearch.trim()) {
      const q = sessionsSearch.toLowerCase();
      result = result.filter(s => s.title.toLowerCase().includes(q));
    }
    if (sessionsFilter === 'active') {
      result = result.filter(s => s.status === 'inprogress' || s.status === 'setup');
    }
    return result;
  }, [sessionItems, sessionsSearch, sessionsFilter]);

  // ---------------------------------------------------------------------------
  // Agents → MeepleCardProps
  // ---------------------------------------------------------------------------

  const agentItems: MeepleCardProps[] = useMemo(() => {
    const agents: Array<{
      id: string;
      name: string;
      type: string;
      isActive: boolean;
      gameId?: string | null;
      gameName?: string | null;
    }> = Array.isArray(agentsData) ? agentsData : [];
    return agents.map(agent => {
      const manaPips: ManaPip[] = [];
      if (agent.gameId) {
        manaPips.push({
          entityType: 'game',
          count: 1,
          items: [
            {
              id: agent.gameId,
              label: agent.gameName ?? agent.gameId.slice(0, 8),
              href: `/library/${agent.gameId}`,
            },
          ],
        });
      }
      manaPips.push({
        entityType: 'chat',
        count: 0,
        onCreate: () => router.push(`/chat?agentId=${agent.id}`),
        createLabel: 'Avvia chat',
      });
      return {
        id: agent.id,
        entity: 'agent' as MeepleEntityType,
        title: agent.name,
        subtitle: agent.type,
        variant: 'grid',
        manaPips,
        status: agent.isActive ? ('active' as const) : ('idle' as const),
      };
    });
  }, [agentsData, router]);

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

  const statsRowProps = useMemo(
    () => ({
      stats: {
        games: {
          value: libraryStats?.totalGames ?? 0,
          isLoading: statsLoading,
          isError: statsError,
          isFetching: statsFetching,
        },
        sessions: {
          value: sessionItems.length,
          isLoading: sessionsLoading,
          isError: sessionsError,
          isFetching: sessionsFetching,
        },
        agents: {
          value: agentItems.length,
          isLoading: agentsLoading,
          isError: agentsError,
          isFetching: agentsFetching,
        },
        events: {
          value: upcomingNights?.length ?? 0,
          isLoading: upcomingLoading,
          isError: upcomingError,
          isFetching: upcomingFetching,
        },
      },
      onRetry: {
        games: () => {
          refetchStats();
        },
        sessions: () => {
          refetchSessions();
        },
        agents: () => {
          refetchAgents();
        },
        events: () => {
          refetchUpcoming();
        },
      },
    }),
    [
      libraryStats,
      statsLoading,
      statsError,
      statsFetching,
      sessionItems.length,
      sessionsLoading,
      sessionsError,
      sessionsFetching,
      agentItems.length,
      agentsLoading,
      agentsError,
      agentsFetching,
      upcomingNights,
      upcomingLoading,
      upcomingError,
      upcomingFetching,
      refetchStats,
      refetchSessions,
      refetchAgents,
      refetchUpcoming,
    ]
  );

  return (
    <div className="mx-auto flex max-w-[1200px] flex-col gap-7 px-4 pb-24 pt-4">
      <DashboardHero displayName={displayName} />
      <DashboardStatsRow {...statsRowProps} />

      {/* Games zone (orange) */}
      <EntityZone entity="game" title="Giochi" count={gameItems.length} viewAllHref="/games">
        {isNewUser ? (
          <NewUserGamesBlock
            search={gamesSearch}
            onSearchChange={setGamesSearch}
            filter={gamesFilter}
            onFilterChange={setGamesFilter}
          />
        ) : (
          <HubLayout
            searchPlaceholder="Cerca giochi..."
            searchValue={gamesSearch}
            onSearchChange={setGamesSearch}
            filterChips={GAMES_FILTERS}
            activeFilterId={gamesFilter}
            onFilterChange={setGamesFilter}
          >
            <MeepleCardGrid items={filteredGameItems} isLoading={libraryLoading} />
          </HubLayout>
        )}
      </EntityZone>

      {/* Sessions zone (indigo) — horizontal scroll */}
      <EntityZone
        entity="session"
        title="Sessioni"
        count={sessionItems.length}
        viewAllHref="/sessions"
      >
        <HubLayout
          searchPlaceholder="Filtra per stato..."
          searchValue={sessionsSearch}
          onSearchChange={setSessionsSearch}
          filterChips={SESSIONS_FILTERS}
          activeFilterId={sessionsFilter}
          onFilterChange={setSessionsFilter}
        >
          {sessionsLoading ? (
            <LoadingSkeleton count={4} />
          ) : filteredSessionItems.length === 0 ? (
            <EmptyCTA
              entity="session"
              icon="🎯"
              title="Nessuna sessione"
              sub="Inizia una nuova partita e traccia i tuoi progressi in tempo reale."
              actions={[{ label: '＋ Crea sessione', href: '/sessions/new', primary: true }]}
            />
          ) : (
            <DiscoverCarousel ariaLabel="Carosello sessioni attive" itemWidth={260} gap={14}>
              {filteredSessionItems.map(item => (
                <div key={item.id ?? item.title} className="w-[260px] shrink-0">
                  <MeepleCard {...item} />
                </div>
              ))}
            </DiscoverCarousel>
          )}
        </HubLayout>
      </EntityZone>

      {/* Agents zone (amber) */}
      <EntityZone entity="agent" title="Agenti AI" count={agentItems.length} viewAllHref="/agents">
        <HubLayout
          searchPlaceholder="Cerca agenti..."
          searchValue={agentsSearch}
          onSearchChange={setAgentsSearch}
          filterChips={AGENTS_FILTERS}
          activeFilterId={agentsFilter}
          onFilterChange={setAgentsFilter}
        >
          <MeepleCardGrid
            items={filteredAgentItems}
            isLoading={agentsLoading}
            emptyNode={
              <EmptyCTA
                entity="agent"
                icon="🤖"
                title="Nessun agente attivo"
                sub="Avvia una chat con un agente AI per ricevere aiuto durante la partita."
                actions={[
                  { label: '💬 Avvia chat', href: '/chat', primary: true },
                  { label: '＋ Crea agente', href: '/agents/new' },
                ]}
              />
            }
          />
        </HubLayout>
      </EntityZone>

      {/* Toolkit zone (green) */}
      <EntityZone entity="toolkit" title="Strumenti" count={TOOLKIT_TOOLS.length}>
        <ToolkitGrid />
      </EntityZone>
    </div>
  );
}
