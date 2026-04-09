'use client';

import { useEffect } from 'react';

import { useChatPanel } from '@/hooks/useChatPanel';

import { ChatContextSwitcher } from './ChatContextSwitcher';
import { ChatMainArea, type ChatMessage } from './ChatMainArea';
import { ChatPanelHeader } from './ChatPanelHeader';
import { ChatSidebar, type ChatRecentItem, type ChatKbGame } from './ChatSidebar';

// Mock data — Phase 5 will wire real chat/KB services
const MOCK_RECENT_CHATS: ChatRecentItem[] = [
  { id: 'c1', emoji: '🎨', title: 'Azul · Turno finale', timestamp: 'Oggi, 14:32', active: true },
  { id: 'c2', emoji: '🦅', title: 'Wingspan · Bonus fine', timestamp: 'Ieri, 21:15' },
  { id: 'c3', emoji: '🌲', title: 'Everdell · Eventi autunno', timestamp: '2 giorni fa' },
];

const MOCK_KB_GAMES: ChatKbGame[] = [
  { id: 'wings', name: 'Wingspan', status: 'ready' },
  { id: 'everd', name: 'Everdell', status: 'indexing' },
  { id: 'catan', name: 'Catan', status: 'ready' },
  { id: 'brass', name: 'Brass: Birmingham', status: 'ready' },
];

const MOCK_SUGGESTED_QUESTIONS = [
  'Come si vince?',
  'Qual è la regola del turno finale?',
  'Spiega i bonus di fine partita',
  'Quante risorse si pescano?',
];

export function ChatSlideOverPanel() {
  const { isOpen, gameContext, close } = useChatPanel();

  // Esc key closes the panel
  useEffect(() => {
    if (!isOpen) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        close();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [isOpen, close]);

  if (!isOpen) return null;

  // TODO(Phase 5): wire real messages, recent chats, KB games via chat service
  const messages: ChatMessage[] = [];

  const handleSend = (_message: string) => {
    // TODO(Phase 5): dispatch to chat service
  };

  const handleNewChat = () => {
    // TODO(Phase 5): create new chat thread
  };

  const handleSelectChat = (_chatId: string) => {
    // TODO(Phase 5): load messages for selected chat
  };

  const handleSelectGame = (_gameId: string) => {
    // TODO(Phase 5): switch game context
  };

  const handlePickGame = () => {
    // TODO(Phase 5): open game picker dropdown
  };

  return (
    <>
      {/* Backdrop */}
      <div
        data-testid="chat-panel-backdrop"
        className="fixed inset-0 z-[var(--z-modal-backdrop,1040)]"
        style={{
          background: 'rgba(40, 28, 14, 0.28)',
          backdropFilter: 'blur(2px)',
        }}
        onClick={close}
        aria-hidden
      />

      {/* Panel */}
      <aside
        data-testid="chat-slide-over-panel"
        className="fixed right-0 top-0 bottom-0 z-[var(--z-modal,1050)] flex w-[760px] max-w-[60vw] flex-col overflow-hidden border-l border-[var(--nh-border-default)] shadow-[var(--shadow-warm-2xl)]"
        style={{
          background: 'linear-gradient(180deg, var(--nh-bg-surface), var(--nh-bg-base))',
          animation: 'chatPanelSlideIn 350ms cubic-bezier(0.4, 0, 0.2, 1)',
        }}
      >
        <style>{`
          @keyframes chatPanelSlideIn {
            from {
              transform: translateX(30px);
              opacity: 0;
            }
            to {
              transform: translateX(0);
              opacity: 1;
            }
          }
        `}</style>
        <ChatPanelHeader
          subtitle={
            gameContext?.kbStatus === 'ready'
              ? 'KB pronta · Powered by MeepleAI'
              : 'Powered by MeepleAI'
          }
          onClose={close}
        />
        <ChatContextSwitcher gameContext={gameContext} onPickGame={handlePickGame} />
        <div className="flex min-h-0 flex-1">
          <ChatSidebar
            chats={MOCK_RECENT_CHATS}
            kbGames={MOCK_KB_GAMES}
            onNewChat={handleNewChat}
            onSelectChat={handleSelectChat}
            onSelectGame={handleSelectGame}
          />
          <ChatMainArea
            messages={messages}
            gameName={gameContext?.name}
            suggestedQuestions={MOCK_SUGGESTED_QUESTIONS}
            onSend={handleSend}
          />
        </div>
      </aside>
    </>
  );
}
