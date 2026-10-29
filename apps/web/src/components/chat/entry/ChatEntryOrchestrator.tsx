/**
 * ChatEntryOrchestrator — Composes GameSelector, QuickStartSuggestions and ThreadCreator into the
 * full "new chat" flow.
 *
 * Issue #4138: the per-game "custom agents" leg is gone, and with it the loading state, the
 * auto-start-on-exactly-one-agent branch and the OFFSCREEN AgentSelector mount (#923).
 * Issue #4730: the persona picker (Auto / Tutor / Arbitro / Stratega / Narratore) is gone too. What it
 * picked never reached the thread — the create request carries no agent type — so it changed nothing
 * but whether the start button was enabled; inside the thread there is a switcher of its own.
 *
 * Two modes:
 * 1. Full mode (no ?game param): Game grid → Start
 * 2. Direct game mode (?game=id from MeepleCard chat button): the game is already chosen → Start
 */

'use client';

import React, { useState, useCallback, useMemo } from 'react';

import { MessageSquarePlus } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { trackRulesChatStarted } from '@/lib/analytics/flywheel-events';
import { cn } from '@/lib/utils';
import type { Game } from '@/types';

import { GameSelector } from './GameSelector';
import { QuickStartSuggestions } from './QuickStartSuggestions';
import { createThreadWithContext } from './ThreadCreator';

import type { PromptType } from './types';

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

  // Loaded game list for title resolution
  const [allGames, setAllGames] = useState<Game[]>([]);

  // Thread creation state
  const [isCreating, setIsCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Track games loaded from GameSelector
  const handleGamesLoaded = useCallback((games: Game[]) => {
    setAllGames(prev => {
      // Merge without duplicates
      const ids = new Set(prev.map(g => g.id));
      const newGames = games.filter(g => !ids.has(g.id));
      return newGames.length > 0 ? [...prev, ...newGames] : prev;
    });
  }, []);

  const handleGameSelect = useCallback((gameId: string) => {
    setSelectedGameId(gameId);
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
    [selectedGameId, selectedKbIds, selectedGame?.title, router]
  );

  const handleQuickStart = useCallback(
    (message: string, promptType?: PromptType) => {
      void handleStartChat(message, promptType);
    },
    [handleStartChat]
  );

  // #4715: the conversation needs a game. "Continue without a game" used to start a thread that never
  // answers, so the start button now waits for one.
  const hasGame = !!selectedGameId;
  const canStart = hasGame;

  return (
    <div className={cn('min-h-dvh bg-background', className)}>
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
            {/* In direct game mode GameSelector is hidden, so the game's title is never loaded here. */}
            {isDirectGameMode
              ? 'Le risposte si basano sul regolamento del gioco scelto.'
              : 'Seleziona un gioco per iniziare: le risposte si basano sul suo regolamento.'}
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

          {/* Quick Start — only with a game: every suggestion is a question about one */}
          {hasGame && (
            <QuickStartSuggestions
              gameName={selectedGame?.title}
              onSelect={handleQuickStart}
              disabled={isCreating}
            />
          )}

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
                  <div className="h-4 w-4 border-2 border-border border-t-white rounded-full animate-spin" />
                  Creazione in corso...
                </>
              ) : (
                <>
                  <MessageSquarePlus className="h-5 w-5" />
                  Inizia Chat
                </>
              )}
            </button>
            {!hasGame && (
              <p
                className="mt-3 text-sm text-muted-foreground font-nunito"
                data-testid="start-needs-game"
              >
                Scegli un gioco per iniziare la conversazione.
              </p>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
