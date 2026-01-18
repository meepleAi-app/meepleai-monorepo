/**
 * GameChatTab Component - Integrated AI Chat Tab
 *
 * Displays:
 * - Quick questions chips (AI-generated from backend, with fallback)
 * - ChatContent component (AI chat interface)
 *
 * Uses Zustand chat store with selectedGameId set on mount.
 * Integrates with existing ChatContent from /chat page.
 * Issue #2401: Fetches AI-generated quick questions from backend.
 *
 * Issue #1841 (PAGE-005)
 */

'use client';

import React, { useEffect } from 'react';

import { useQuery } from '@tanstack/react-query';

import { ChatContent } from '@/components/chat/ChatContent';
import { Spinner } from '@/components/loading';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { api, type QuickQuestion } from '@/lib/api';
import { ChatStoreProvider } from '@/store/chat/ChatStoreProvider';
import { useChatStore } from '@/store/chat/store';

// ============================================================================
// Types
// ============================================================================

export interface GameChatTabProps {
  /** Game ID for chat context */
  gameId: string;
  /** Game title for context display */
  gameTitle: string;
}

// ============================================================================
// Fallback Quick Questions (Issue #2401)
// ============================================================================
// Used when backend has no AI-generated questions or fetch fails

const FALLBACK_QUICK_QUESTIONS: QuickQuestion[] = [
  {
    id: 'fallback-q1',
    sharedGameId: '',
    text: 'Come si gioca?',
    emoji: '🎮',
    category: 0,
    displayOrder: 1,
    isGenerated: false,
    createdAt: new Date().toISOString(),
    isActive: true,
  },
  {
    id: 'fallback-q2',
    sharedGameId: '',
    text: 'Quali sono le regole principali?',
    emoji: '📖',
    category: 1,
    displayOrder: 2,
    isGenerated: false,
    createdAt: new Date().toISOString(),
    isActive: true,
  },
  {
    id: 'fallback-q3',
    sharedGameId: '',
    text: 'Come si vince?',
    emoji: '🏆',
    category: 2,
    displayOrder: 3,
    isGenerated: false,
    createdAt: new Date().toISOString(),
    isActive: true,
  },
  {
    id: 'fallback-q4',
    sharedGameId: '',
    text: 'Quanto dura una partita?',
    emoji: '⏱️',
    category: 3,
    displayOrder: 4,
    isGenerated: false,
    createdAt: new Date().toISOString(),
    isActive: true,
  },
  {
    id: 'fallback-q5',
    sharedGameId: '',
    text: 'È adatto ai principianti?',
    emoji: '🎯',
    category: 4,
    displayOrder: 5,
    isGenerated: false,
    createdAt: new Date().toISOString(),
    isActive: true,
  },
];

// ============================================================================
// Internal Component (needs ChatStore context)
// ============================================================================

function GameChatTabInternal({ gameId, gameTitle: _gameTitle }: GameChatTabProps) {
  // Issue #1676: Migrated from useChatContext to direct Zustand store
  const { selectGame, sendMessage } = useChatStore(state => ({
    selectGame: state.selectGame,
    sendMessage: state.sendMessage,
  }));

  // Set selected game on mount
  useEffect(() => {
    selectGame(gameId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gameId]); // Only run on gameId change

  // Issue #2401: Fetch AI-generated quick questions from backend
  const {
    data: quickQuestions,
    isLoading,
    isError,
  } = useQuery({
    queryKey: ['game', gameId, 'quick-questions'],
    queryFn: () => api.games.getQuickQuestions(gameId),
    staleTime: 5 * 60 * 1000, // 5 minutes
    gcTime: 10 * 60 * 1000, // 10 minutes (was cacheTime)
    retry: 1, // Retry once on failure
  });

  // Use fetched questions if available, otherwise use fallback
  const questionsToDisplay =
    quickQuestions && quickQuestions.length > 0 ? quickQuestions : FALLBACK_QUICK_QUESTIONS;
  const _showingFallback = !quickQuestions || quickQuestions.length === 0 || isError;

  const handleQuickQuestionClick = (question: string) => {
    sendMessage(question);
  };

  return (
    <div className="space-y-4" data-testid="chat-tab">
      {/* Quick Questions Section */}
      <div>
        <h3 className="text-sm font-medium text-muted-foreground mb-3">Domande Rapide</h3>

        {isLoading && (
          <div className="flex justify-center py-4">
            <Spinner size="sm" />
          </div>
        )}

        {!isLoading && (
          <>
            {isError && (
              <div className="text-xs text-amber-600 mb-2">
                ⚠️ Impossibile caricare domande AI. Usando domande generiche.
              </div>
            )}
            <div className="flex flex-wrap gap-2">
              {questionsToDisplay.map(q => (
                <Badge
                  key={q.id}
                  variant="secondary"
                  className="cursor-pointer hover:bg-primary hover:text-primary-foreground transition-colors px-3 py-1.5"
                  onClick={() => handleQuickQuestionClick(q.text)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={e => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      handleQuickQuestionClick(q.text);
                    }
                  }}
                  aria-label={`Quick question: ${q.text}`}
                >
                  <span className="mr-1.5" aria-hidden="true">
                    {q.emoji}
                  </span>
                  {q.text}
                </Badge>
              ))}
            </div>
          </>
        )}
      </div>

      <Separator />

      {/* Chat Interface */}
      <div className="min-h-[500px]" data-testid="chat-interface">
        <ChatContent />
      </div>
    </div>
  );
}

// ============================================================================
// Exported Component (with ChatStoreProvider wrapper)
// ============================================================================

export function GameChatTab(props: GameChatTabProps) {
  return (
    <ChatStoreProvider>
      <GameChatTabInternal {...props} />
    </ChatStoreProvider>
  );
}
