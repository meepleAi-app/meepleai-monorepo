/**
 * NewChatView - Game + Agent selection for new conversations (Issue #4363)
 *
 * Welcome page showing selectable MeepleCard grids for:
 * 1. Games from user library + shared catalog
 * 2. AI agents (Auto, Tutor, Arbitro, Decisore)
 *
 * Features:
 * - Query params pre-selection (?game=id, ?agent=type)
 * - Quick start suggestions contextual to selected game
 * - Creates thread via API and redirects to /chat/{threadId}
 * - Glassmorphism design, responsive layout
 */

'use client';

import React, { useEffect, useState, useCallback, useMemo } from 'react';

import { Bot, Gamepad2, MessageSquarePlus, Search, Sparkles, Zap } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';
import type { Game } from '@/types';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

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

  // State
  const [games, setGames] = useState<Game[]>([]);
  const [agents, setAgents] = useState<AgentDto[]>([]);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [selectedAgentType, setSelectedAgentType] = useState<string | null>('auto');
  const [isLoadingGames, setIsLoadingGames] = useState(true);
  const [isCreating, setIsCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Pre-select from query params
  useEffect(() => {
    const gameParam = searchParams?.get('game');
    const agentParam = searchParams?.get('agent');

    if (gameParam) setSelectedGameId(gameParam);
    if (agentParam) setSelectedAgentType(agentParam);
  }, [searchParams]);

  // Load games + agents on mount
  useEffect(() => {
    async function loadData() {
      setIsLoadingGames(true);
      try {
        const [gamesResponse, agentsResponse] = await Promise.all([
          api.games.getAll(),
          api.agents.getAvailable(),
        ]);
        setGames(gamesResponse.games ?? []);
        setAgents(agentsResponse ?? []);
      } catch {
        setError('Errore nel caricamento dei dati');
      } finally {
        setIsLoadingGames(false);
      }
    }
    void loadData();
  }, []);

  // Resolve actual agent ID from backend agents list
  const resolveAgentId = useCallback(
    (agentType: string | null): string | undefined => {
      if (!agentType || agentType === 'auto') return undefined;
      const match = agents.find(a => a.type === agentType);
      return match?.id;
    },
    [agents]
  );

  // Get selected game name for quick-start
  const selectedGame = useMemo(
    () => games.find(g => g.id === selectedGameId),
    [games, selectedGameId]
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
        const thread = await api.chat.createThread({
          gameId: gameId ?? null,
          title: selectedGame?.title
            ? `Chat: ${selectedGame.title}`
            : 'Nuova conversazione',
          initialMessage: initialMessage ?? null,
        });

        if (thread?.id) {
          router.push(`/chat?threadId=${thread.id}`);
        }
      } catch {
        setError('Errore nella creazione della conversazione');
      } finally {
        setIsCreating(false);
      }
    },
    [selectedGameId, selectedGame?.title, router]
  );

  const handleQuickStart = useCallback(
    (message: string) => {
      void handleStartChat(message);
    },
    [handleStartChat]
  );

  const canStart = selectedAgentType !== null;

  return (
    <div className="min-h-dvh bg-background">
      <div className="max-w-4xl mx-auto px-4 py-8 sm:py-12">
        {/* Header */}
        <div className="text-center mb-8 sm:mb-12">
          <div className="inline-flex items-center gap-3 mb-4">
            <MessageSquarePlus className="h-8 w-8 text-amber-500" />
            <h1 className="text-2xl sm:text-3xl font-bold font-quicksand text-foreground">
              Inizia una nuova conversazione
            </h1>
          </div>
          <p className="text-muted-foreground font-nunito max-w-lg mx-auto">
            Seleziona un gioco e un agente AI per iniziare. Puoi anche chattare senza un gioco specifico.
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
          {/* Game Selection */}
          <section
            className="p-6 rounded-2xl bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50"
            data-testid="game-selection-section"
          >
            <SectionTitle icon={Gamepad2} title="Seleziona un gioco" />
            <GameGrid
              games={games}
              selectedGameId={selectedGameId}
              onSelect={setSelectedGameId}
              isLoading={isLoadingGames}
            />
          </section>

          {/* Agent Selection */}
          <section
            className="p-6 rounded-2xl bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50"
            data-testid="agent-selection-section"
          >
            <SectionTitle icon={Bot} title="Seleziona un agente" />
            <AgentGrid
              agents={DEFAULT_AGENTS}
              selectedAgentType={selectedAgentType}
              onSelect={setSelectedAgentType}
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
