/**
 * NewChatView - Game + Agent selection for new conversations (Issue #4363)
 *
 * Two modes:
 * 1. Full mode (no ?game param): Game grid → Agent grid → Start
 * 2. Direct game mode (?game=id from MeepleCard chat button):
 *    - 0 custom agents → redirect to agent creation
 *    - 1 custom agent  → auto-create thread and redirect to chat
 *    - 2+ custom agents → show agent picker only (no game grid)
 *
 * Features:
 * - Query params pre-selection (?game=id, ?agent=type)
 * - Quick start suggestions contextual to selected game
 * - Creates thread via API and redirects to /chat/{threadId}
 * - Glassmorphism design, responsive layout
 */

'use client';

import React, { useEffect, useState, useCallback, useMemo, useRef } from 'react';

import { Bot, Gamepad2, MessageSquarePlus, Plus, Search, Sparkles, Zap } from 'lucide-react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { api } from '@/lib/api';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import type { PrivateGameDto } from '@/lib/api/schemas/private-games.schemas';
import { cn } from '@/lib/utils';
import type { Game } from '@/types';

// ============================================================================
// Type Adapters
// ============================================================================

function privateGameToGame(pg: PrivateGameDto): Game {
  return { id: pg.id, title: pg.title, createdAt: pg.createdAt };
}

function libraryEntryToGame(entry: UserLibraryEntry): Game {
  return { id: entry.gameId, title: entry.gameTitle, createdAt: entry.addedAt };
}

// ============================================================================
// Types
// ============================================================================

/** Pre-defined agent types for the selection grid */
interface AgentOption {
  id: string;
  name: string;
  type: string;
  description: string;
  icon: string;
}

/** Custom agent from backend — user-owned */
interface CustomAgent {
  id: string;
  name: string;
  type: string;
}

/** Quick start suggestion */
interface QuickStartSuggestion {
  label: string;
  message: string;
}

// ============================================================================
// Constants
// ============================================================================

const DEFAULT_AGENTS: AgentOption[] = [
  {
    id: 'auto',
    name: 'Auto',
    type: 'auto',
    description: 'Scelta automatica in base alla domanda',
    icon: '🤖',
  },
  {
    id: 'tutor',
    name: 'Tutor',
    type: 'qa',
    description: 'Spiega regole e meccaniche',
    icon: '📚',
  },
  {
    id: 'arbitro',
    name: 'Arbitro',
    type: 'rules',
    description: 'Risolve dubbi e dispute',
    icon: '⚖️',
  },
  {
    id: 'decisore',
    name: 'Decisore',
    type: 'strategy',
    description: 'Consigli strategici e tattici',
    icon: '🎯',
  },
];

function getQuickStartSuggestions(gameName?: string): QuickStartSuggestion[] {
  if (!gameName) {
    return [
      { label: 'Consiglia un gioco', message: 'Consigliami un gioco da tavolo per la serata' },
      { label: 'Regole base', message: 'Spiegami le regole di base' },
      { label: 'Migliori per 2 giocatori', message: 'Quali sono i migliori giochi per 2 giocatori?' },
    ];
  }
  return [
    { label: `Come si gioca a ${gameName}`, message: `Come si gioca a ${gameName}?` },
    { label: `Regole di ${gameName}`, message: `Spiegami le regole di ${gameName}` },
    { label: `Strategia per ${gameName}`, message: `Qual è la migliore strategia per ${gameName}?` },
  ];
}

// ============================================================================
// Sub-components
// ============================================================================

function SectionTitle({ icon: Icon, title }: { icon: React.ElementType; title: string }) {
  return (
    <div className="flex items-center gap-2 mb-4">
      <Icon className="h-5 w-5 text-amber-600 dark:text-amber-400" />
      <h2 className="text-lg font-semibold font-quicksand text-foreground">{title}</h2>
    </div>
  );
}

function GameGrid({
  games,
  selectedGameId,
  onSelect,
  isLoading,
}: {
  games: Game[];
  selectedGameId: string | null;
  onSelect: (gameId: string) => void;
  isLoading: boolean;
}) {
  const [search, setSearch] = useState('');

  const filtered = useMemo(() => {
    if (!search.trim()) return games;
    const lower = search.toLowerCase();
    return games.filter(g => g.title.toLowerCase().includes(lower));
  }, [games, search]);

  if (isLoading) {
    return (
      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
        {Array.from({ length: 8 }).map((_, i) => (
          <div
            key={i}
            className="h-24 rounded-xl bg-muted/50 animate-pulse"
          />
        ))}
      </div>
    );
  }

  return (
    <div>
      {/* Search */}
      <div className="relative mb-3">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <input
          type="text"
          placeholder="Cerca gioco..."
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="w-full pl-9 pr-3 py-2 rounded-lg border border-border/50 bg-white/70 dark:bg-card/70 backdrop-blur-md text-sm font-nunito placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-amber-500/40"
          data-testid="game-search-input"
        />
      </div>

      {/* Grid */}
      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3 max-h-64 overflow-y-auto pr-1">
        {filtered.length === 0 ? (
          <p className="col-span-full text-sm text-muted-foreground text-center py-4">
            Nessun gioco trovato
          </p>
        ) : (
          filtered.map(game => (
            <button
              key={game.id}
              onClick={() => onSelect(game.id)}
              className={cn(
                'text-left rounded-xl transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-amber-500/40',
                selectedGameId === game.id
                  ? 'ring-2 ring-amber-500 scale-[1.02]'
                  : 'hover:scale-[1.01]'
              )}
              aria-pressed={selectedGameId === game.id}
              data-testid={`game-card-${game.id}`}
            >
              <MeepleCard
                entity="game"
                variant="compact"
                title={game.title}
                className={cn(
                  selectedGameId === game.id && 'border-amber-500'
                )}
              />
            </button>
          ))
        )}
      </div>

      {/* Skip game selection */}
      <button
        onClick={() => onSelect('')}
        className={cn(
          'mt-3 w-full py-2 px-3 rounded-lg text-xs text-muted-foreground border border-dashed border-border/50 transition-colors',
          'hover:bg-muted/50 hover:text-foreground',
          selectedGameId === '' && 'bg-muted/50 text-foreground border-solid'
        )}
        data-testid="skip-game-btn"
      >
        Continua senza gioco (chat generica)
      </button>
    </div>
  );
}

function AgentGrid({
  agents,
  selectedAgentType,
  onSelect,
}: {
  agents: AgentOption[];
  selectedAgentType: string | null;
  onSelect: (agentType: string) => void;
}) {
  return (
    <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
      {agents.map(agent => (
        <button
          key={agent.id}
          onClick={() => onSelect(agent.type)}
          className={cn(
            'text-left rounded-xl transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-amber-500/40',
            selectedAgentType === agent.type
              ? 'ring-2 ring-amber-500 scale-[1.02]'
              : 'hover:scale-[1.01]'
          )}
          aria-pressed={selectedAgentType === agent.type}
          data-testid={`agent-card-${agent.type}`}
        >
          <MeepleCard
            entity="agent"
            variant="compact"
            title={agent.name}
            subtitle={agent.description}
            badge={agent.icon}
            className={cn(
              selectedAgentType === agent.type && 'border-amber-500'
            )}
          />
        </button>
      ))}
    </div>
  );
}

function CustomAgentGrid({
  agents,
  selectedCustomAgentId,
  onSelect,
  gameId,
  isLoading,
}: {
  agents: CustomAgent[];
  selectedCustomAgentId: string | null;
  onSelect: (agentId: string) => void;
  gameId: string;
  isLoading: boolean;
}) {
  if (isLoading) {
    return (
      <div className="flex gap-3 mb-4">
        {Array.from({ length: 2 }).map((_, i) => (
          <div key={i} className="h-20 w-32 rounded-xl bg-muted/50 animate-pulse" />
        ))}
      </div>
    );
  }

  if (agents.length === 0) return null;

  return (
    <div className="mb-4">
      <div className="flex items-center justify-between mb-2">
        <p className="text-xs font-semibold uppercase tracking-wide text-amber-700 dark:text-amber-400 font-nunito">
          I tuoi agent
        </p>
        <Link
          href={`/chat/agents/create?gameId=${gameId}`}
          className="flex items-center gap-1 text-xs text-amber-600 hover:text-amber-700 dark:text-amber-400 dark:hover:text-amber-300 font-nunito transition-colors"
          data-testid="create-agent-link"
        >
          <Plus className="h-3 w-3" />
          Crea nuovo agent
        </Link>
      </div>
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {agents.map(agent => (
          <button
            key={agent.id}
            onClick={() => onSelect(agent.id)}
            className={cn(
              'text-left rounded-xl transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-amber-500/40',
              selectedCustomAgentId === agent.id
                ? 'ring-2 ring-amber-500 scale-[1.02]'
                : 'hover:scale-[1.01]'
            )}
            aria-pressed={selectedCustomAgentId === agent.id}
            data-testid={`custom-agent-card-${agent.id}`}
          >
            <MeepleCard
              entity="agent"
              variant="compact"
              title={agent.name}
              subtitle={agent.type}
              badge="🤖"
              className={cn(
                'border-amber-300/50',
                selectedCustomAgentId === agent.id && 'border-amber-500'
              )}
            />
          </button>
        ))}
      </div>
      <div className="mt-2 border-t border-border/30 pt-2">
        <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground font-nunito">
          Agent di sistema
        </p>
      </div>
    </div>
  );
}

function QuickStartSection({
  suggestions,
  onSuggestionClick,
  disabled,
}: {
  suggestions: QuickStartSuggestion[];
  onSuggestionClick: (message: string) => void;
  disabled: boolean;
}) {
  return (
    <div>
      <SectionTitle icon={Sparkles} title="Quick Start" />
      <div className="flex flex-wrap gap-2">
        {suggestions.map(s => (
          <button
            key={s.label}
            onClick={() => onSuggestionClick(s.message)}
            disabled={disabled}
            className={cn(
              'px-4 py-2 rounded-full text-sm font-nunito transition-all duration-200',
              'border border-border/50',
              disabled
                ? 'bg-muted text-muted-foreground/50 cursor-not-allowed'
                : 'bg-white/70 dark:bg-card/70 backdrop-blur-md text-foreground hover:bg-amber-50 dark:hover:bg-amber-500/10 hover:border-amber-300 cursor-pointer'
            )}
            data-testid={`quick-start-${s.label.replace(/\s/g, '-').toLowerCase()}`}
          >
            <Zap className="inline h-3.5 w-3.5 mr-1.5 text-amber-500" />
            {s.label}
          </button>
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function NewChatView() {
  const router = useRouter();
  const searchParams = useSearchParams();

  // Direct game mode: arrived from MeepleCard "Chat" button with ?gameId= or ?game=
  const directGameId = searchParams?.get('gameId') ?? searchParams?.get('game') ?? null;
  const isDirectGameMode = !!directGameId;

  // State — tabbed game sources
  const [activeTab, setActiveTab] = useState<'private' | 'shared'>('private');
  const [privateGames, setPrivateGames] = useState<Game[]>([]);
  const [sharedGames, setSharedGames] = useState<Game[]>([]);
  const [isLoadingPrivateGames, setIsLoadingPrivateGames] = useState(true);
  const [isLoadingSharedGames, setIsLoadingSharedGames] = useState(false);
  const [sharedGamesLoaded, setSharedGamesLoaded] = useState(false);

  const [agents, setAgents] = useState<AgentDto[]>([]);
  const [customAgents, setCustomAgents] = useState<CustomAgent[]>([]);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(directGameId);
  const [selectedAgentType, setSelectedAgentType] = useState<string | null>(isDirectGameMode ? null : 'auto');
  const [selectedCustomAgentId, setSelectedCustomAgentId] = useState<string | null>(null);
  const [isLoadingCustomAgents, setIsLoadingCustomAgents] = useState(isDirectGameMode);
  const [isCreating, setIsCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Derived state for active tab
  const activeGames = activeTab === 'private' ? privateGames : sharedGames;
  const isLoadingGames = activeTab === 'private' ? isLoadingPrivateGames : isLoadingSharedGames;

  // Prevent double auto-start in direct game mode
  const autoStartedRef = useRef(false);

  // Pre-select from query params (non-direct mode uses agent param too)
  useEffect(() => {
    const agentParam = searchParams?.get('agent');
    if (agentParam) setSelectedAgentType(agentParam);
  }, [searchParams]);

  // Load private games + system agents on mount
  useEffect(() => {
    async function loadData() {
      setIsLoadingPrivateGames(true);
      try {
        const [privateResponse, agentsResponse] = await Promise.all([
          api.library.getPrivateGames({ pageSize: 100 }),
          api.agents.getAvailable(),
        ]);
        setPrivateGames((privateResponse.items ?? []).map(privateGameToGame));
        setAgents(agentsResponse ?? []);
      } catch {
        setError('Errore nel caricamento dei dati');
      } finally {
        setIsLoadingPrivateGames(false);
      }
    }
    void loadData();
  }, []);

  // Lazy-load shared library games on first tab switch
  useEffect(() => {
    if (activeTab !== 'shared' || sharedGamesLoaded) return;

    async function loadSharedGames() {
      setIsLoadingSharedGames(true);
      try {
        const libraryResponse = await api.library.getLibrary({ pageSize: 100 });
        const kbReady = (libraryResponse.items ?? []).filter(e => e.hasKb);
        setSharedGames(kbReady.map(libraryEntryToGame));
      } catch {
        setError('Errore nel caricamento della libreria');
      } finally {
        setIsLoadingSharedGames(false);
        setSharedGamesLoaded(true);
      }
    }
    void loadSharedGames();
  }, [activeTab, sharedGamesLoaded]);

  // Issue #4914: load custom agents when game selected
  useEffect(() => {
    if (!selectedGameId) {
      setCustomAgents([]);
      return;
    }

    let cancelled = false;
    setIsLoadingCustomAgents(true);

    api.agents.getUserAgentsForGame(selectedGameId)
      .then(result => {
        if (!cancelled) {
          setCustomAgents(result.map(a => ({ id: a.id, name: a.name, type: a.type })));
        }
      })
      .catch(() => {
        // In direct game mode, agent fetch failure is critical (drives redirect logic)
        if (!cancelled && isDirectGameMode) {
          setError('Errore nel caricamento degli agenti');
        }
      })
      .finally(() => { if (!cancelled) setIsLoadingCustomAgents(false); });

    return () => { cancelled = true; };
  }, [selectedGameId, isDirectGameMode]);

  // Direct game mode: auto-start or redirect based on agent count
  useEffect(() => {
    if (!isDirectGameMode || autoStartedRef.current) return;
    if (isLoadingCustomAgents) return;
    if (!selectedGameId) return;

    if (customAgents.length === 0) {
      // No agents → redirect to agent creation
      autoStartedRef.current = true;
      router.replace(`/chat/agents/create?gameId=${selectedGameId}`);
    } else if (customAgents.length === 1) {
      // Exactly 1 agent → auto-create thread
      autoStartedRef.current = true;
      setIsCreating(true);

      const agent = customAgents[0];
      const gameName = [...privateGames, ...sharedGames].find(g => g.id === selectedGameId)?.title;

      api.chat.createThread({
        gameId: selectedGameId,
        agentId: agent.id,
        title: gameName ? `Chat: ${gameName}` : 'Nuova conversazione',
        initialMessage: null,
      })
        .then(thread => {
          if (thread?.id) {
            router.push(`/chat/${thread.id}`);
          }
        })
        .catch(() => {
          setError('Errore nella creazione della conversazione');
          setIsCreating(false);
        });
    }
    // 2+ agents: fall through to show agent selection UI
  }, [isDirectGameMode, isLoadingCustomAgents, selectedGameId, customAgents, privateGames, sharedGames, router]);

  // Handle game selection — reset custom agent selection
  const handleGameSelect = useCallback((gameId: string) => {
    setSelectedGameId(gameId);
    setSelectedCustomAgentId(null);
  }, []);

  // Handle custom agent selection — clear system agent
  const handleCustomAgentSelect = useCallback((agentId: string) => {
    setSelectedCustomAgentId(agentId);
    setSelectedAgentType(null);
  }, []);

  // Handle system agent selection — clear custom agent
  const handleSystemAgentSelect = useCallback((agentType: string) => {
    setSelectedAgentType(agentType);
    setSelectedCustomAgentId(null);
  }, []);

  // Resolve actual agent ID — system agent types (auto/qa/rules/strategy) are UI-only
  // labels that don't map to backend agent types, so pick the best real agent
  const resolveAgentId = useCallback(
    (): string | undefined => {
      // If user explicitly selected a system agent type, prefer system agents
      if (selectedAgentType !== null) {
        return agents[0]?.id ?? customAgents[0]?.id;
      }
      // Otherwise prefer custom agents (more game-specific), fall back to system
      return customAgents[0]?.id ?? agents[0]?.id;
    },
    [selectedAgentType, customAgents, agents]
  );

  // Get selected game name for quick-start (search both lists)
  const selectedGame = useMemo(
    () => [...privateGames, ...sharedGames].find(g => g.id === selectedGameId),
    [privateGames, sharedGames, selectedGameId]
  );

  const suggestions = useMemo(
    () => getQuickStartSuggestions(selectedGame?.title),
    [selectedGame?.title]
  );

  // Create thread and redirect
  const handleStartChat = useCallback(
    async (initialMessage?: string) => {
      setIsCreating(true);
      setError(null);

      try {
        const gameId = selectedGameId && selectedGameId !== '' ? selectedGameId : undefined;
        // Issue #4914: custom agent → use UUID directly; system agent → resolve best available
        const agentId = selectedCustomAgentId ?? resolveAgentId();

        const thread = await api.chat.createThread({
          gameId: gameId ?? null,
          agentId: agentId ?? null,
          title: selectedGame?.title
            ? `Chat: ${selectedGame.title}`
            : 'Nuova conversazione',
          initialMessage: initialMessage ?? null,
        });

        if (thread?.id) {
          router.push(`/chat/${thread.id}`);
        }
      } catch {
        setError('Errore nella creazione della conversazione');
      } finally {
        setIsCreating(false);
      }
    },
    [selectedGameId, selectedGame?.title, selectedCustomAgentId, resolveAgentId, router]
  );

  const handleQuickStart = useCallback(
    (message: string) => {
      void handleStartChat(message);
    },
    [handleStartChat]
  );

  const hasAgentAvailable = agents.length > 0 || customAgents.length > 0;
  const canStart = hasAgentAvailable && (selectedAgentType !== null || selectedCustomAgentId !== null);

  // Direct game mode: show loading spinner while resolving agents (0 or 1 → auto-redirect)
  if (isDirectGameMode && (isLoadingCustomAgents || isCreating || (customAgents.length <= 1 && !error))) {
    return (
      <div className="min-h-dvh bg-background flex items-center justify-center">
        <div className="text-center">
          <div className="h-8 w-8 border-3 border-amber-500/30 border-t-amber-500 rounded-full animate-spin mx-auto mb-4" />
          <p className="text-muted-foreground font-nunito">
            {isCreating ? 'Avvio chat in corso...' : 'Preparazione...'}
          </p>
          {error && (
            <div
              role="alert"
              className="mt-4 p-3 bg-red-50 dark:bg-red-500/10 text-red-700 dark:text-red-400 rounded-lg text-sm border border-red-200 dark:border-red-500/20"
            >
              {error}
            </div>
          )}
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-dvh bg-background">
      <div className="max-w-4xl mx-auto px-4 py-8 sm:py-12">
        {/* Header */}
        <div className="text-center mb-8 sm:mb-12">
          <div className="inline-flex items-center gap-3 mb-4">
            <MessageSquarePlus className="h-8 w-8 text-amber-500" />
            <h1 className="text-2xl sm:text-3xl font-bold font-quicksand text-foreground">
              {isDirectGameMode ? 'Seleziona un agente' : 'Inizia una nuova conversazione'}
            </h1>
          </div>
          <p className="text-muted-foreground font-nunito max-w-lg mx-auto">
            {isDirectGameMode && selectedGame
              ? <>Scegli un agente per <span className="font-semibold text-foreground">{selectedGame.title}</span></>
              : 'Seleziona un gioco e un agente AI per iniziare. Puoi anche chattare senza un gioco specifico.'
            }
          </p>
        </div>

        {/* Error */}
        {error && (
          <div
            role="alert"
            className="mb-6 p-3 bg-red-50 dark:bg-red-500/10 text-red-700 dark:text-red-400 rounded-lg text-sm border border-red-200 dark:border-red-500/20"
            data-testid="new-chat-error"
          >
            {error}
          </div>
        )}

        {/* Content */}
        <div className="space-y-8">
          {/* Game Selection — hidden in direct game mode */}
          {!isDirectGameMode && (
            <section
              className="p-6 rounded-2xl bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50"
              data-testid="game-selection-section"
            >
              <SectionTitle icon={Gamepad2} title="Seleziona un gioco" />

              {/* Game source tabs */}
              <div
                className="flex gap-1 mb-4 p-1 rounded-lg bg-muted/50"
                data-testid="game-source-tabs"
                role="tablist"
              >
                <button
                  role="tab"
                  aria-selected={activeTab === 'private'}
                  onClick={() => setActiveTab('private')}
                  className={cn(
                    'flex-1 py-1.5 px-3 rounded-md text-sm font-nunito font-medium transition-all duration-200',
                    activeTab === 'private'
                      ? 'bg-white dark:bg-card shadow-sm text-foreground'
                      : 'text-muted-foreground hover:text-foreground'
                  )}
                  data-testid="tab-private-games"
                >
                  I miei giochi
                </button>
                <button
                  role="tab"
                  aria-selected={activeTab === 'shared'}
                  onClick={() => setActiveTab('shared')}
                  className={cn(
                    'flex-1 py-1.5 px-3 rounded-md text-sm font-nunito font-medium transition-all duration-200',
                    activeTab === 'shared'
                      ? 'bg-white dark:bg-card shadow-sm text-foreground'
                      : 'text-muted-foreground hover:text-foreground'
                  )}
                  data-testid="tab-shared-games"
                >
                  Libreria condivisa
                </button>
              </div>

              <GameGrid
                games={activeGames}
                selectedGameId={selectedGameId}
                onSelect={handleGameSelect}
                isLoading={isLoadingGames}
              />
            </section>
          )}

          {/* Agent Selection */}
          <section
            className="p-6 rounded-2xl bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50"
            data-testid="agent-selection-section"
          >
            <SectionTitle icon={Bot} title="Seleziona un agente" />
            {/* Custom agents (only when game selected) */}
            {selectedGameId && selectedGameId !== '' && (
              <CustomAgentGrid
                agents={customAgents}
                selectedCustomAgentId={selectedCustomAgentId}
                onSelect={handleCustomAgentSelect}
                gameId={selectedGameId}
                isLoading={isLoadingCustomAgents}
              />
            )}
            <AgentGrid
              agents={DEFAULT_AGENTS}
              selectedAgentType={selectedAgentType}
              onSelect={handleSystemAgentSelect}
            />
          </section>

          {/* Quick Start */}
          <section
            className="p-6 rounded-2xl bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50"
            data-testid="quick-start-section"
          >
            <QuickStartSection
              suggestions={suggestions}
              onSuggestionClick={handleQuickStart}
              disabled={isCreating}
            />
          </section>

          {/* Start Button */}
          <div className="text-center">
            <button
              onClick={() => void handleStartChat()}
              disabled={!canStart || isCreating}
              className={cn(
                'inline-flex items-center gap-2 px-8 py-3 rounded-xl text-base font-semibold font-quicksand transition-all duration-200',
                canStart && !isCreating
                  ? 'bg-amber-500 hover:bg-amber-600 text-white shadow-lg shadow-amber-500/25 hover:shadow-amber-500/40 cursor-pointer'
                  : 'bg-muted text-muted-foreground cursor-not-allowed'
              )}
              data-testid="start-chat-btn"
            >
              {isCreating ? (
                <>
                  <div className="h-4 w-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                  Creazione in corso...
                </>
              ) : (
                <>
                  <MessageSquarePlus className="h-5 w-5" />
                  Inizia Chat
                </>
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
