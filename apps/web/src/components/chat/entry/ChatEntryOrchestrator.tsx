/**
 * ChatEntryOrchestrator — Composes GameSelector, AgentSelector, QuickStartSuggestions,
 * and ThreadCreator into the full "new chat" flow.
 *
 * Two modes:
 * 1. Full mode (no ?game param): Game grid → Agent grid → Start
 * 2. Direct game mode (?game=id from MeepleCard chat button):
 *    - 0 custom agents → show system agent selection
 *    - 1 custom agent  → auto-create thread and redirect
 *    - 2+ custom agents → show agent picker only
 */

'use client';

import React, { useEffect, useState, useCallback, useMemo, useRef } from 'react';

import { MessageSquarePlus } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { trackRulesChatStarted } from '@/lib/analytics/flywheel-events';
import { cn } from '@/lib/utils';
import type { Game } from '@/types';

import { AgentSelector } from './AgentSelector';
import { DEFAULT_AGENTS } from './constants';
import { GameSelector } from './GameSelector';
import { QuickStartSuggestions } from './QuickStartSuggestions';
import { createThreadWithContext } from './ThreadCreator';

import type { CustomAgent, PromptType } from './types';

export interface ChatEntryOrchestratorProps {
  className?: string;
}

export function ChatEntryOrchestrator({ className }: ChatEntryOrchestratorProps) {
  const router = useRouter();
  const searchParams = useSearchParams();

  // Direct game mode from ?game= or ?gameId=
  const directGameId = searchParams?.get('gameId') ?? searchParams?.get('game') ?? null;
  const isDirectGameMode = !!directGameId;

  // Pre-selected KB IDs from ?kbIds=
  const kbIdsParam = searchParams?.get('kbIds');
  const selectedKbIds = useMemo(
    () => (kbIdsParam ? kbIdsParam.split(',').filter(Boolean) : undefined),
    [kbIdsParam]
  );

  // Selection state
  const [selectedGameId, setSelectedGameId] = useState<string | null>(directGameId);
  const [selectedAgentType, setSelectedAgentType] = useState<string | null>(
    isDirectGameMode ? null : 'auto'
  );
  const [selectedCustomAgentId, setSelectedCustomAgentId] = useState<string | null>(null);

  // Loaded game list for title resolution
  const [allGames, setAllGames] = useState<Game[]>([]);

  // Custom agents resolved by AgentSelector
  const [customAgents, setCustomAgents] = useState<CustomAgent[]>([]);
  const [isLoadingCustomAgents, setIsLoadingCustomAgents] = useState(isDirectGameMode);

  // Thread creation state
  const [isCreating, setIsCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Prevent double auto-start in direct game mode
  const autoStartedRef = useRef(false);

  // Pre-select agent from ?agent= param
  useEffect(() => {
    const agentParam = searchParams?.get('agent');
    if (agentParam) setSelectedAgentType(agentParam);
  }, [searchParams]);

  // Track games loaded from GameSelector
  const handleGamesLoaded = useCallback((games: Game[]) => {
    setAllGames(prev => {
      // Merge without duplicates
      const ids = new Set(prev.map(g => g.id));
      const newGames = games.filter(g => !ids.has(g.id));
      return newGames.length > 0 ? [...prev, ...newGames] : prev;
    });
  }, []);

  // AgentSelector callback — track custom agents and loading state
  const handleCustomAgentsResolved = useCallback((agents: CustomAgent[], isLoading: boolean) => {
    setCustomAgents(agents);
    setIsLoadingCustomAgents(isLoading);
  }, []);

  // Direct game mode: auto-start or show agent picker
  useEffect(() => {
    if (!isDirectGameMode || autoStartedRef.current) return;
    if (isLoadingCustomAgents) return;
    if (!selectedGameId) return;

    if (customAgents.length === 0) {
      // No custom agents — show system agent selection
      autoStartedRef.current = true;
    } else if (customAgents.length === 1) {
      // Exactly 1 agent → auto-create thread
      autoStartedRef.current = true;
      setIsCreating(true);

      const agent = customAgents[0];
      const gameName = allGames.find(g => g.id === selectedGameId)?.title;

      createThreadWithContext({
        gameId: selectedGameId,
        gameName,
        selectedCustomAgentId: agent.id,
        customAgents,
        selectedKbIds,
      })
        .then(({ threadId }) => {
          router.push(`/chat/${threadId}`);
        })
        .catch(() => {
          setError('Errore nella creazione della conversazione');
          setIsCreating(false);
        });
    }
    // 2+ agents: fall through to show agent selection UI
  }, [
    isDirectGameMode,
    isLoadingCustomAgents,
    selectedGameId,
    selectedKbIds,
    customAgents,
    allGames,
    router,
  ]);

  // Handle game selection — reset agent selection
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

  // Selected game name for display and thread title
  const selectedGame = useMemo(
    () => allGames.find(g => g.id === selectedGameId),
    [allGames, selectedGameId]
  );

  // Create thread and redirect
  const handleStartChat = useCallback(
    async (initialMessage?: string, promptType: PromptType = 'general') => {
      setIsCreating(true);
      setError(null);

      try {
        const { threadId } = await createThreadWithContext({
          gameId: selectedGameId,
          gameName: selectedGame?.title,
          selectedCustomAgentId,
          customAgents,
          initialMessage,
          promptType,
          selectedKbIds,
        });

        trackRulesChatStarted({
          gameId: selectedGameId && selectedGameId !== '' ? selectedGameId : undefined,
          promptType,
        });

        router.push(`/chat/${threadId}`);
      } catch {
        setError('Errore nella creazione della conversazione');
      } finally {
        setIsCreating(false);
      }
    },
    [
      selectedGameId,
      selectedKbIds,
      selectedGame?.title,
      selectedCustomAgentId,
      customAgents,
      router,
    ]
  );

  const handleQuickStart = useCallback(
    (message: string, promptType?: PromptType) => {
      void handleStartChat(message, promptType);
    },
    [handleStartChat]
  );

  const hasAgentAvailable = DEFAULT_AGENTS.length > 0 || customAgents.length > 0;
  const canStart =
    hasAgentAvailable && (selectedAgentType !== null || selectedCustomAgentId !== null);

  // Direct game mode: loading spinner while resolving agents
  if (
    isDirectGameMode &&
    (isLoadingCustomAgents || isCreating || (customAgents.length === 1 && !error))
  ) {
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
    <div className={cn('min-h-dvh bg-background', className)}>
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
            {isDirectGameMode && selectedGame ? (
              <>
                Scegli un agente per{' '}
                <span className="font-semibold text-foreground">{selectedGame.title}</span>
              </>
            ) : (
              'Seleziona un gioco e un agente AI per iniziare. Puoi anche chattare senza un gioco specifico.'
            )}
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
            <GameSelector
              onSelect={handleGameSelect}
              selectedGameId={selectedGameId}
              onGamesLoaded={handleGamesLoaded}
            />
          )}

          {/* Agent Selection */}
          <AgentSelector
            gameId={selectedGameId}
            onSelectSystemAgent={handleSystemAgentSelect}
            onSelectCustomAgent={handleCustomAgentSelect}
            selectedAgentType={selectedAgentType}
            selectedCustomAgentId={selectedCustomAgentId}
            onCustomAgentsResolved={handleCustomAgentsResolved}
          />

          {/* Quick Start */}
          <QuickStartSuggestions
            gameName={selectedGame?.title}
            onSelect={handleQuickStart}
            disabled={isCreating}
          />

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
