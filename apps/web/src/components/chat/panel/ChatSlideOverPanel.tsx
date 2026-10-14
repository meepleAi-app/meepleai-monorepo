'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import { createThread } from '@/components/chat/entry/ThreadCreator';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import { useLibrarySearch } from '@/hooks/queries/useLibrarySearch';
import { useAgentChatStream } from '@/hooks/useAgentChatStream';
import { useChatPanel } from '@/hooks/useChatPanel';
import { useEmbeddingStatus } from '@/hooks/useEmbeddingStatus';
import { useToast } from '@/hooks/useToast';
import { api } from '@/lib/api';
import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

import { ChatContextSwitcher, type AvailableGame } from './ChatContextSwitcher';
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

/** A library game whose knowledge base is ready (`hasKb`: at least one PDF fully indexed). */
function libraryEntryToKbGame(entry: UserLibraryEntry): ChatKbGame {
  return {
    id: entry.gameId,
    name: entry.gameTitle,
    status: 'ready',
    imageUrl: entry.gameImageUrl ?? entry.gameIconUrl ?? undefined,
  };
}

// ─── Component ──────────────────────────────────────────────────────────────

export function ChatSlideOverPanel() {
  const { isOpen, gameContext, close, setGameContext } = useChatPanel();
  const { toast } = useToast();

  // Local chat state — lives on the component so the store stays pure UI
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [threadId, setThreadId] = useState<string | null>(null);

  // Real data hooks. #4187: il pannello è montato su ogni pagina autenticata (DesktopShell), e queste
  // righe girano prima di `if (!isOpen) return null`: senza `isOpen` partivano due richieste a ogni
  // pagina, a pannello chiuso.
  const { data: recentSessions } = useRecentChatSessions(50, isOpen);
  // #4187: the context switcher offers the user's library, searched on the server; it used to
  // offer the first 50 catalog games, and a game outside the library ends at the RAG gate
  // (#4155). The sidebar lists the library games with a ready KB, unaffected by the search.
  // Both use the largest page the server serves (100). #4227: the server filters `hasKb`, so the
  // sidebar says it is partial only when the games with a KB, not the whole library, pass 100.
  const [gameSearch, setGameSearch] = useState('');
  const pickerLibrary = useLibrarySearch(gameSearch, { enabled: isOpen, pageSize: 100 });
  const sidebarLibrary = useLibrarySearch('', { enabled: isOpen, pageSize: 100, hasKb: true });

  // A search belongs to one opening of the panel.
  useEffect(() => {
    if (!isOpen) setGameSearch('');
  }, [isOpen]);

  // Issue #4139: the agent lookup that stood here existed only to feed
  // /api/v1/agents/{agentId}/chat, a route the backend never mounted — the comment
  // even said sending "would 404" without it, which was true either way. The stream
  // now posts to /agents/qa/stream, scoped by game, so the game context is all this
  // panel needs; #4138 loses one more reader of useGameAgents.

  // Live KB status for the selected game
  const { isReady: kbIsReady, data: kbStatusData } = useEmbeddingStatus(gameContext?.id ?? null, {
    enabled: !!gameContext?.id,
  });
  const liveKbStatus: 'ready' | 'indexing' =
    kbStatusData?.status === 'Completed' || kbIsReady ? 'ready' : 'indexing';
  const enrichedGameContext = gameContext ? { ...gameContext, kbStatus: liveKbStatus } : null;

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
  const { stopStreaming } = stream;
  useEffect(() => {
    if (!isOpen) {
      stopStreaming();
    }
    return () => {
      stopStreaming();
    };
  }, [isOpen, stopStreaming]);

  // Adapt recent sessions to sidebar shape
  const recentChats: ChatRecentItem[] = useMemo(() => {
    const sessions = recentSessions?.sessions ?? [];
    return sessions.filter(s => !s.isArchived).map(s => sessionToRecentItem(s, threadId));
  }, [recentSessions, threadId]);

  // Adapt games to KB game list
  const kbGames: ChatKbGame[] = useMemo(
    () => sidebarLibrary.items.map(libraryEntryToKbGame),
    [sidebarLibrary.items]
  );

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
    async (message: string) => {
      // Need a game + no in-flight stream to send safely.
      if (!message.trim() || !gameContext || stream.state.isStreaming) return;

      const userMessage: ChatMessage = {
        id: `user-${Date.now()}`,
        role: 'user',
        content: message,
        authorName: 'Tu',
        timestamp: formatMessageTime(new Date().toISOString()),
      };
      setMessages(prev => [...prev, userMessage]);

      // Lazy thread creation: if opened from a ManaPip click with gameContext
      // but no existing thread, create one on the first message send.
      let currentThreadId = threadId;
      if (!currentThreadId && gameContext) {
        try {
          const result = await createThread({
            gameId: gameContext.id,
            gameName: gameContext.name,
            initialMessage: message,
          });
          currentThreadId = result.threadId;
          setThreadId(currentThreadId);
        } catch {
          toast({
            title: 'Errore creazione chat',
            description: 'Non è stato possibile avviare la conversazione.',
            variant: 'destructive',
          });
          return;
        }
      }

      stream.sendMessage(gameContext.id, message, currentThreadId ?? undefined, {
        gameName: gameContext.name,
        agentTypology: 'default',
      });
    },
    [gameContext, stream, threadId, toast]
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
        toast({
          title: 'Errore caricamento chat',
          description: 'Non è stato possibile caricare la conversazione.',
          variant: 'destructive',
        });
      }
    },
    [stream, toast]
  );

  const handleSelectGame = useCallback(
    (gameId: string) => {
      const entry =
        pickerLibrary.items.find(e => e.gameId === gameId) ??
        sidebarLibrary.items.find(e => e.gameId === gameId);
      if (!entry) return;
      setGameContext({
        id: entry.gameId,
        name: entry.gameTitle,
        year: entry.gameYearPublished ?? undefined,
        pdfCount: 0,
        kbStatus: 'ready',
        imageUrl: entry.gameImageUrl ?? entry.gameIconUrl ?? undefined,
      });
      setMessages([]);
      setThreadId(null);
      stream.reset();
    },
    [pickerLibrary.items, sidebarLibrary.items, setGameContext, stream]
  );

  // Game picker: called by ChatContextSwitcher dropdown with the selected gameId.
  const handlePickGame = useCallback(
    (gameId: string) => {
      if (gameId) handleSelectGame(gameId);
    },
    [handleSelectGame]
  );

  // Games list passed to ChatContextSwitcher dropdown.
  const availableGames: AvailableGame[] = useMemo(
    () => pickerLibrary.items.map(e => ({ id: e.gameId, name: e.gameTitle })),
    [pickerLibrary.items]
  );

  if (!isOpen) return null;

  return (
    <>
      {/* Backdrop. #4341: velo e pannello prendono il livello dalla stessa scala, quella canonica
          (design-tokens-canonical.css: overlay 50 < modal 60). Il velo prendeva --z-modal-backdrop
          (1040, solo nella scala vecchia) e il pannello --z-modal, che la canonica ridefinisce a
          60: il velo copriva il pannello, e un clic nel campo chiudeva la chat. */}
      <div
        data-testid="chat-panel-backdrop"
        className="fixed inset-0 z-[var(--z-overlay,50)]"
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
        className="fixed right-0 top-0 bottom-0 z-[var(--z-modal,60)] flex w-[760px] max-w-[60vw] flex-col overflow-hidden border-l border-[var(--border)] shadow-[var(--shadow-warm-2xl)]"
        style={{
          background: 'linear-gradient(180deg, var(--bg-card), var(--bg))',
          animation: 'chatPanelSlideIn 350ms cubic-bezier(0.4, 0, 0.2, 1)',
        }}
      >
        <ChatPanelHeader
          subtitle={
            enrichedGameContext?.kbStatus === 'ready'
              ? 'KB pronta · Powered by MeepleAI'
              : 'Powered by MeepleAI'
          }
          onClose={close}
        />
        <ChatContextSwitcher
          gameContext={enrichedGameContext}
          onPickGame={handlePickGame}
          availableGames={availableGames}
          search={gameSearch}
          onSearchChange={setGameSearch}
          onCatalogNavigate={close}
        />
        <div className="flex min-h-0 flex-1">
          <ChatSidebar
            chats={recentChats}
            kbGames={kbGames}
            kbGamesTruncated={sidebarLibrary.totalCount > sidebarLibrary.items.length}
            onNewChat={handleNewChat}
            onSelectChat={handleSelectChat}
            onSelectGame={handleSelectGame}
          />
          <ChatMainArea
            messages={displayedMessages}
            gameName={gameContext?.name}
            suggestedQuestions={suggestedQuestions}
            onSend={handleSend}
            error={stream.state.error}
            tierLimit={stream.state.tierLimit}
          />
        </div>
      </aside>
    </>
  );
}
