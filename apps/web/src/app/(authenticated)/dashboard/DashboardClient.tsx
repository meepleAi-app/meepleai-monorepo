'use client';

import { useEffect, useMemo, useState } from 'react';

import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { OwnershipConfirmDialog } from '@/components/dialogs/OwnershipConfirmDialog';
import { HubLayout, type FilterChip } from '@/components/layout/HubLayout';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardProps, MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useAgents } from '@/hooks/queries/useAgents';
import { useBatchGameStatus } from '@/hooks/queries/useBatchGameStatus';
import { useGames } from '@/hooks/queries/useGames';
import { useAddGameToLibrary, useLibrary } from '@/hooks/queries/useLibrary';
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
  { id: 'dice', icon: '🎲', name: 'Dado', desc: 'Lancia d4–d20', iconBg: 'bg-amber-100' },
  { id: 'timer', icon: '⏳', name: 'Clessidra', desc: 'Timer per turno', iconBg: 'bg-sky-100' },
  {
    id: 'score',
    icon: '📊',
    name: 'Scoreboard',
    desc: 'Punteggi multi-player',
    iconBg: 'bg-purple-100',
  },
  { id: 'token', icon: '🪙', name: 'Token', desc: 'Contatori risorse', iconBg: 'bg-green-100' },
] as const;

// ---------------------------------------------------------------------------
// Sub-components
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

// CTA for empty sections (sessions, agents)
interface CtaAction {
  label: string;
  href: string;
  primary?: boolean;
}

function EmptyCTA({
  icon,
  title,
  sub,
  actions,
}: {
  icon: string;
  title: string;
  sub: string;
  actions: CtaAction[];
}) {
  return (
    <div
      className="flex flex-col items-center gap-3 py-6 px-4 text-center
                 rounded-xl border border-dashed border-[rgba(180,130,80,0.25)]
                 bg-[var(--nh-bg-card,white)]"
    >
      <span className="text-3xl">{icon}</span>
      <div>
        <p className="font-[Quicksand] font-bold text-sm text-[var(--nh-text-primary,#1a1a1a)]">
          {title}
        </p>
        <p className="text-xs text-[var(--nh-text-muted,#94a3b8)] mt-1 max-w-[240px] mx-auto leading-relaxed">
          {sub}
        </p>
      </div>
      <div className="flex gap-2 flex-wrap justify-center">
        {actions.map(a => (
          <Link
            key={a.href}
            href={a.href}
            className={
              a.primary
                ? 'inline-flex items-center gap-1 px-4 py-1.5 rounded-xl text-xs font-bold font-[Quicksand] bg-amber-600 text-white shadow-[0_2px_8px_rgba(180,100,20,.25)]'
                : 'inline-flex items-center gap-1 px-4 py-1.5 rounded-xl text-xs font-bold font-[Quicksand] border border-amber-600 text-amber-600'
            }
          >
            {a.label}
          </Link>
        ))}
      </div>
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
      <div className="flex flex-col items-center gap-2 py-8 text-[var(--nh-text-muted,#94a3b8)]">
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
    <div
      className="bg-[var(--nh-bg-card,white)] border border-[var(--nh-border,rgba(0,0,0,0.07))]
                 rounded-xl shadow-sm overflow-hidden flex flex-col"
    >
      {/* Thumbnail */}
      <div className="relative h-[68px] flex items-center justify-center bg-gradient-to-br from-[#fdf0e0] to-[#fce8cc] overflow-hidden flex-shrink-0">
        {game.imageUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={game.imageUrl} alt={game.title} className="w-full h-full object-cover" />
        ) : (
          <span className="text-3xl select-none">🎲</span>
        )}
        {hasKb !== undefined && (
          <span
            className={`absolute top-1 right-1 px-1.5 py-0.5 rounded text-[8px] font-extrabold font-[Quicksand] text-white leading-none ${hasKb ? 'bg-green-500' : 'bg-[var(--nh-text-muted,#94a3b8)]'}`}
          >
            {hasKb ? 'KB ✓' : 'KB –'}
          </span>
        )}
      </div>

      {/* Info */}
      <div className="px-2 pt-1.5 pb-1 flex-1 min-h-0">
        <p
          className="font-[Quicksand] font-bold text-[11px] leading-tight
                     overflow-hidden line-clamp-2 text-[var(--nh-text-primary,#1a1a2e)]"
        >
          {game.title}
        </p>
        {game.publisher && (
          <p className="text-[10px] text-[var(--nh-text-secondary,#64748b)] mt-0.5 truncate">
            {game.publisher}
          </p>
        )}
      </div>

      {/* Add button */}
      <button
        onClick={() => !inLibrary && !adding && onAdd(game.id)}
        disabled={inLibrary || adding}
        aria-label={
          inLibrary ? `${game.title} già in libreria` : `Aggiungi ${game.title} alla libreria`
        }
        className={
          inLibrary
            ? 'mx-1.5 mb-1.5 h-[22px] rounded-lg text-[10px] font-bold font-[Quicksand] flex items-center justify-center gap-1 bg-black/5 text-[var(--nh-text-muted,#94a3b8)] cursor-default'
            : adding
              ? 'mx-1.5 mb-1.5 h-[22px] rounded-lg text-[10px] font-bold font-[Quicksand] flex items-center justify-center gap-1 bg-black/20 text-white cursor-wait'
              : 'mx-1.5 mb-1.5 h-[22px] rounded-lg text-[10px] font-bold font-[Quicksand] flex items-center justify-center gap-1 bg-amber-600 text-white hover:opacity-90 active:scale-95 transition-transform'
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
          router.push(`/games/${confirmGame.id}`);
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
          <p className="text-xs text-[var(--nh-text-muted,#94a3b8)] bg-amber-50 border border-amber-100 rounded-lg px-3 py-2 mb-3 font-medium">
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

// ---------------------------------------------------------------------------
// Toolkit carousel (always shown, static data)
// ---------------------------------------------------------------------------

function ToolkitCarousel() {
  return (
    <>
      <p className="text-[10px] text-[var(--nh-text-muted,#94a3b8)] font-semibold mb-2">
        Strumenti disponibili subito, senza libreria
      </p>
      <div className="flex gap-2 overflow-x-auto scrollbar-none pb-1 -mx-1 px-1">
        {TOOLKIT_TOOLS.map(tool => (
          <Link
            key={tool.id}
            href={`/toolkit?tool=${tool.id}`}
            className="flex-shrink-0 w-[100px] bg-[var(--nh-bg-card,white)]
                       border border-[var(--nh-border,rgba(0,0,0,0.07))]
                       rounded-xl p-2.5 flex flex-col items-center gap-1.5
                       hover:shadow-md transition-shadow"
          >
            <span
              className={`w-[38px] h-[38px] rounded-[10px] flex items-center justify-center text-xl ${tool.iconBg}`}
            >
              {tool.icon}
            </span>
            <span className="font-[Quicksand] font-bold text-[11px] text-center leading-tight text-[var(--nh-text-primary,#1a1a1a)]">
              {tool.name}
            </span>
            <span className="text-[9px] text-[var(--nh-text-muted,#94a3b8)] text-center leading-tight">
              {tool.desc}
            </span>
          </Link>
        ))}
      </div>
    </>
  );
}

// ---------------------------------------------------------------------------
// Main DashboardClient
// ---------------------------------------------------------------------------

export function DashboardClient() {
  const { user } = useAuth();
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
  const { data: sessionsData, isLoading: sessionsLoading } = useActiveSessions();
  const { data: agentsData, isLoading: agentsLoading } = useAgents({ activeOnly: false });

  // Detect new user: library loaded with no items
  const isNewUser = !libraryLoading && (libraryData?.items ?? []).length === 0;

  // ---------------------------------------------------------------------------
  // Library → MeepleCardProps
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
      variant: 'grid',
    }));
  }, [libraryData]);

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
    return sessions.map(session => ({
      id: session.id,
      entity: 'session' as MeepleEntityType,
      title: `Sessione ${session.id.slice(0, 8)}`,
      subtitle: session.status,
      variant: 'grid',
      status:
        session.status.toLowerCase() === 'inprogress'
          ? ('inprogress' as const)
          : session.status.toLowerCase() === 'paused'
            ? ('paused' as const)
            : session.status.toLowerCase() === 'setup'
              ? ('setup' as const)
              : undefined,
    }));
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
    const agents: Array<{ id: string; name: string; type: string; isActive: boolean }> =
      Array.isArray(agentsData) ? agentsData : [];
    return agents.map(agent => ({
      id: agent.id,
      entity: 'agent' as MeepleEntityType,
      title: agent.name,
      subtitle: agent.type,
      variant: 'grid',
      status: agent.isActive ? ('active' as const) : ('idle' as const),
    }));
  }, [agentsData]);

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
      <GreetingHeader displayName={displayName} />

      {/* Block 1: Games */}
      <HubBlock title="🎲 Giochi">
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
      </HubBlock>

      {/* Block 2: Sessions */}
      <HubBlock title="🎯 Sessioni">
        <HubLayout
          searchPlaceholder="Filtra per stato..."
          searchValue={sessionsSearch}
          onSearchChange={setSessionsSearch}
          filterChips={SESSIONS_FILTERS}
          activeFilterId={sessionsFilter}
          onFilterChange={setSessionsFilter}
        >
          <MeepleCardGrid
            items={filteredSessionItems}
            isLoading={sessionsLoading}
            emptyNode={
              <EmptyCTA
                icon="🎯"
                title="Nessuna sessione"
                sub="Inizia una nuova partita e traccia i tuoi progressi in tempo reale."
                actions={[{ label: '＋ Crea sessione', href: '/sessions/new', primary: true }]}
              />
            }
          />
        </HubLayout>
      </HubBlock>

      {/* Block 3: Agents */}
      <HubBlock title="🤖 Agenti AI">
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
      </HubBlock>

      {/* Block 4: Toolkit */}
      <HubBlock title="🛠️ Toolkit">
        <ToolkitCarousel />
      </HubBlock>
    </div>
  );
}
