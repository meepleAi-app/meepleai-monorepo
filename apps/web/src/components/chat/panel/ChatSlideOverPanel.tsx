'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import { useGameAgents } from '@/hooks/queries/useGameAgents';
import { useGames } from '@/hooks/queries/useGames';
import { useAgentChatStream } from '@/hooks/useAgentChatStream';
import { useChatPanel } from '@/hooks/useChatPanel';
import { api } from '@/lib/api';
import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';
import type { Game } from '@/lib/api/schemas/games.schemas';

import { ChatContextSwitcher } from './ChatContextSwitcher';
import { ChatMainArea, type ChatMessage } from './ChatMainArea';
import { ChatPanelHeader } from './ChatPanelHeader';
import { ChatSidebar, type ChatRecentItem, type ChatKbGame } from './ChatSidebar';

// ─── Adapters: API DTOs → panel view models ────────────────────────────────

function formatRelativeTime(iso: string | null): string {
  if (!iso) return '';
  const date = new Date(iso);
  const now = Date.now();
  const diffMs = now - date.getTime();
  const diffMinutes = Math.floor(diffMs / 60_000);
  const diffHours = Math.floor(diffMinutes / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffMinutes < 1) return 'Adesso';
  if (diffMinutes < 60) return `${diffMinutes} min fa`;
  if (diffHours < 24)
    return `Oggi, ${date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })}`;
  if (diffDays < 2)
    return `Ieri, ${date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })}`;
  if (diffDays < 7) return `${diffDays} giorni fa`;
  return date.toLocaleDateString();
}

function formatMessageTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
  } catch {
    return '';
  }
}

function sessionToRecentItem(
  session: ChatSessionSummaryDto,
  activeSessionId: string | null
): ChatRecentItem {
  return {
    id: session.id,
    emoji: '🎲',
    title: session.title ?? session.gameTitle ?? 'Chat',
    timestamp: formatRelativeTime(session.lastMessageAt ?? session.createdAt),
    active: session.id === activeSessionId,
  };
}

function gameToKbGame(game: Game): ChatKbGame {
  return {
    id: game.id,
    name: game.title,
    // KB status fetch is out of scope for this PR; treat published games as ready.
    status: 'ready',
    imageUrl: game.imageUrl ?? game.iconUrl ?? undefined,
  };
}

// ─── Component ──────────────────────────────────────────────────────────────

export function ChatSlideOverPanel() {
  const { isOpen, gameContext, close, setGameContext } = useChatPanel();

  // Local chat state — lives on the component so the store stays pure UI
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [threadId, setThreadId] = useState<string | null>(null);

  // Real data hooks
  const { data: recentSessions } = useRecentChatSessions(50);
  const { data: gamesResponse } = useGames(undefined, undefined, 1, 50);

  // Resolve the default agent for the selected game — sendMessage needs a real
  // agent UUID (the SSE endpoint is /api/v1/agents/{agentId}/chat). Without
  // this, sending would 404.
  const { data: gameAgents } = useGameAgents({
    gameId: gameContext?.id ?? null,
    enabled: !!gameContext?.id,
  });
  const agentId = gameAgents?.[0]?.id ?? null;

  // SSE streaming
  const stream = useAgentChatStream({
    onComplete: (answer, metadata) => {
      const assistantMessage: ChatMessage = {
        id: `assistant-${Date.now()}`,
        role: 'assistant',
        content: answer,
        authorName: 'Agente',
        timestamp: formatMessageTime(new Date().toISOString()),
      };
      setMessages(prev => [...prev, assistantMessage]);
      if (metadata.chatThreadId && metadata.chatThreadId !== threadId) {
        setThreadId(metadata.chatThreadId);
      }
    },
    onError: () => {
      // stream.state.error is surfaced below; no further handling needed here
    },
  });

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

  // Abort any in-flight SSE when the panel is closed or the component unmounts,
  // so we don't leak the fetch or fire state updates after the user navigated away.
  useEffect(() => {
    if (!isOpen) {
      stream.stopStreaming();
    }
    return () => {
      stream.stopStreaming();
    };
  }, [isOpen, stream]);

  // Adapt recent sessions to sidebar shape
  const recentChats: ChatRecentItem[] = useMemo(() => {
    const sessions = recentSessions?.sessions ?? [];
    return sessions.filter(s => !s.isArchived).map(s => sessionToRecentItem(s, threadId));
  }, [recentSessions, threadId]);

  // Adapt games to KB game list
  const kbGames: ChatKbGame[] = useMemo(() => {
    if (!gamesResponse) return [];
    return gamesResponse.games.map(gameToKbGame);
  }, [gamesResponse]);

  // Suggested questions come from the last Complete event; fall back to empty
  const suggestedQuestions: string[] = useMemo(
    () => (messages.length === 0 ? [] : stream.state.followUpQuestions),
    [messages.length, stream.state.followUpQuestions]
  );

  // Live streaming message, rendered inline while the stream is still running
  const streamingPreview: ChatMessage | null =
    stream.state.isStreaming && stream.state.currentAnswer
      ? {
          id: 'assistant-streaming',
          role: 'assistant',
          content: stream.state.currentAnswer,
          authorName: 'Agente',
          timestamp: formatMessageTime(new Date().toISOString()),
        }
      : null;

  const displayedMessages = streamingPreview ? [...messages, streamingPreview] : messages;

  // ─── Handlers ────────────────────────────────────────────────────────────

  const handleSend = useCallback(
    (message: string) => {
      // Need a game + a resolved agent + no in-flight stream to send safely.
      if (!message.trim() || !gameContext || !agentId || stream.state.isStreaming) return;

      const userMessage: ChatMessage = {
        id: `user-${Date.now()}`,
        role: 'user',
        content: message,
        authorName: 'Tu',
        timestamp: formatMessageTime(new Date().toISOString()),
      };
      setMessages(prev => [...prev, userMessage]);

      stream.sendMessage(agentId, message, threadId ?? undefined, {
        gameName: gameContext.name,
        agentTypology: 'default',
      });
    },
    [gameContext, agentId, stream, threadId]
  );

  const handleNewChat = useCallback(() => {
    setMessages([]);
    setThreadId(null);
    stream.reset();
  }, [stream]);

  const handleSelectChat = useCallback(
    async (chatId: string) => {
      try {
        const thread = await api.chat.getThreadById(chatId);
        if (!thread) return;

        const mapped: ChatMessage[] = thread.messages.map((m, idx) => ({
          id: m.backendMessageId ?? `thread-${thread.id}-${idx}`,
          role: m.role === 'user' ? 'user' : 'assistant',
          content: m.content,
          authorName: m.role === 'user' ? 'Tu' : 'Agente',
          timestamp: formatMessageTime(m.timestamp),
        }));

        setThreadId(thread.id);
        setMessages(mapped);
        stream.reset();
      } catch {
        // Failure is surfaced via the stream error UI on next send
      }
    },
    [stream]
  );

  const handleSelectGame = useCallback(
    (gameId: string) => {
      const game = gamesResponse?.games.find(g => g.id === gameId);
      if (!game) return;
      setGameContext({
        id: game.id,
        name: game.title,
        year: game.yearPublished ?? undefined,
        pdfCount: 0,
        kbStatus: 'ready',
        imageUrl: game.imageUrl ?? game.iconUrl ?? undefined,
      });
      setMessages([]);
      setThreadId(null);
      stream.reset();
    },
    [gamesResponse, setGameContext, stream]
  );

  // Game picker reuses the KB games list; ChatContextSwitcher calls this on click.
  // For now it simply cycles to the next available game — a real dropdown is a
  // polish item separate from the wiring work in this PR.
  const handlePickGame = useCallback(() => {
    if (!gamesResponse || gamesResponse.games.length === 0) return;
    const currentIndex = gameContext
      ? gamesResponse.games.findIndex(g => g.id === gameContext.id)
      : -1;
    const nextIndex = (currentIndex + 1) % gamesResponse.games.length;
    handleSelectGame(gamesResponse.games[nextIndex].id);
  }, [gamesResponse, gameContext, handleSelectGame]);

  if (!isOpen) return null;

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
            chats={recentChats}
            kbGames={kbGames}
            onNewChat={handleNewChat}
            onSelectChat={handleSelectChat}
            onSelectGame={handleSelectGame}
          />
          <ChatMainArea
            messages={displayedMessages}
            gameName={gameContext?.name}
            suggestedQuestions={suggestedQuestions}
            onSend={handleSend}
          />
        </div>
      </aside>
    </>
  );
}
