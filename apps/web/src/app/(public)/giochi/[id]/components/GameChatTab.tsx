/**
 * GameChatTab Component - Integrated AI Chat Tab
 *
 * Displays:
 * - Quick questions chips (clickable shortcuts)
 * - ChatContent component (AI chat interface)
 *
 * Uses Zustand chat store with selectedGameId set on mount.
 * Integrates with existing ChatContent from /chat page.
 *
 * Issue #1841 (PAGE-005)
 */

'use client';

import React, { useEffect } from 'react';

import { ChatContent } from '@/components/chat/ChatContent';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
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

interface QuickQuestion {
  id: string;
  text: string;
  emoji: string;
}

// ============================================================================
// Quick Questions Data
// ============================================================================

const QUICK_QUESTIONS: QuickQuestion[] = [
  { id: 'q1', text: 'Come si gioca?', emoji: '🎮' },
  { id: 'q2', text: 'Quali sono le regole principali?', emoji: '📖' },
  { id: 'q3', text: 'Come si vince?', emoji: '🏆' },
  { id: 'q4', text: 'Quanto dura una partita?', emoji: '⏱️' },
  { id: 'q5', text: 'È adatto ai principianti?', emoji: '🎯' },
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

  const handleQuickQuestionClick = (question: string) => {
    sendMessage(question);
  };

  return (
    <div className="space-y-4" data-testid="chat-tab">
      {/* Quick Questions Section */}
      <div>
        <h3 className="text-sm font-medium text-muted-foreground mb-3">Domande Rapide</h3>
        <div className="flex flex-wrap gap-2">
          {QUICK_QUESTIONS.map(q => (
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
