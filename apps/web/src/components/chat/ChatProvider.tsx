/**
 * ChatProvider - Chat state management (DDD INTEGRATED - SPRINT-3 #858)
 *
 * Manages:
 * - ChatThread sessions per game (DDD KnowledgeBase)
 * - Messages per thread
 * - Message operations (send, edit, delete, feedback)
 *
 * Key improvements:
 * - Integrated with DDD KnowledgeBase.Application (Issue #858)
 * - Uses ChatThread endpoints (Backend #1126)
 * - Normalized state structure (no Map, serializable)
 * - localStorage persistence
 * - No useRef anti-patterns
 * - No disabled ESLint rules
 *
 * Nested under GameProvider in provider hierarchy
 */

import React, { createContext, useContext, useState, useCallback, useMemo, useEffect, PropsWithChildren } from 'react';
import { ChatThread, Message } from '@/types';
import { api } from '@/lib/api';
import { useGame } from '@/components/game/GameProvider';

// ============================================================================
// Types & Constants
// ============================================================================

/**
 * Maximum threads per game (Issue #858 - configurable limit)
 * When exceeded, oldest inactive thread is archived
 */
const MAX_THREADS_PER_GAME = 5;

/**
 * Normalized chat state structure (serializable for localStorage)
 * SPRINT-3 #858: Using ChatThread from DDD KnowledgeBase
 */
interface ChatState {
  chatsByGame: Record<string, ChatThread[]>;  // ChatThreads indexed by gameId
  activeChatIds: Record<string, string | null>; // Active thread per game (nullable)
  messagesByChat: Record<string, Message[]>; // Messages indexed by threadId (UI format)
}

interface LoadingState {
  chats: boolean;
  messages: boolean;
  sending: boolean;
  creating: boolean;
  updating: boolean;
  deleting: boolean;
}

export interface ChatContextValue {
  // Current game's threads (derived from normalized state)
  // Using ChatThread from DDD KnowledgeBase (#858)
  chats: ChatThread[];
  activeChat: ChatThread | null;
  activeChatId: string | null;
  messages: Message[];

  // Thread operations (DDD ChatThread API)
  createChat: () => Promise<void>;
  deleteChat: (chatId: string) => Promise<void>;
  selectChat: (chatId: string) => Promise<void>;

  // Message operations
  sendMessage: (content: string) => Promise<void>;
  editMessage: (messageId: string, content: string) => Promise<void>;
  deleteMessage: (messageId: string) => Promise<void>;
  setMessageFeedback: (messageId: string, feedback: 'helpful' | 'not-helpful') => Promise<void>;

  // State
  loading: LoadingState;
  error: string | null;
}

// ============================================================================
// localStorage utilities
// ============================================================================

const STORAGE_KEY = 'meepleai_chat_state';
const STORAGE_VERSION = '1.0';

interface StoredState {
  version: string;
  timestamp: number;
  state: ChatState;
}

function loadStateFromStorage(): ChatState | null {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (!stored) return null;

    const parsed: StoredState = JSON.parse(stored);
    if (parsed.version !== STORAGE_VERSION) return null;

    // Validate age (expire after 24 hours)
    const ageHours = (Date.now() - parsed.timestamp) / (1000 * 60 * 60);
    if (ageHours > 24) {
      localStorage.removeItem(STORAGE_KEY);
      return null;
    }

    return parsed.state;
  } catch (err) {
    console.error('Failed to load state from localStorage:', err);
    return null;
  }
}

function saveStateToStorage(state: ChatState): void {
  try {
    const toStore: StoredState = {
      version: STORAGE_VERSION,
      timestamp: Date.now(),
      state,
    };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(toStore));
  } catch (err) {
    console.error('Failed to save state to localStorage:', err);
  }
}

// ============================================================================
// Context
// ============================================================================

const ChatContext = createContext<ChatContextValue | null>(null);

// ============================================================================
// Provider Component
// ============================================================================

export function ChatProvider({ children }: PropsWithChildren) {
  const { selectedGameId, selectedAgentId } = useGame();

  // Load initial state from localStorage
  const [state, setState] = useState<ChatState>(() => {
    const stored = loadStateFromStorage();
    return stored ?? {
      chatsByGame: {},
      activeChatIds: {},
      messagesByChat: {},
    };
  });

  const [loading, setLoading] = useState<LoadingState>({
    chats: false,
    messages: false,
    sending: false,
    creating: false,
    updating: false,
    deleting: false,
  });
  const [error, setError] = useState<string | null>(null);

  // Persist state to localStorage whenever it changes
  useEffect(() => {
    saveStateToStorage(state);
  }, [state]);

  // Derived values for current game
  const chats = useMemo(
    () => {
      if (!selectedGameId) return [];
      const gameChats = state.chatsByGame[selectedGameId];
      // Defensive: ensure gameChats is an array (protect against corrupted localStorage)
      return Array.isArray(gameChats) ? gameChats : [];
    },
    [state.chatsByGame, selectedGameId]
  );

  const activeChatId = useMemo(
    () => (selectedGameId ? state.activeChatIds[selectedGameId] ?? null : null),
    [state.activeChatIds, selectedGameId]
  );

  const activeChat = useMemo(
    () => chats.find((c) => c.id === activeChatId) ?? null,
    [chats, activeChatId]
  );

  const messages = useMemo(
    () => (activeChatId ? state.messagesByChat[activeChatId] ?? [] : []),
    [state.messagesByChat, activeChatId]
  );

  // ============================================================================
  // Data Loading Functions (DDD ChatThread Integration - #858)
  // ============================================================================

  const loadChats = useCallback(async (gameId: string) => {
    setLoading((prev) => ({ ...prev, chats: true }));
    setError(null);
    try {
      // SPRINT-3 #858: Use DDD KnowledgeBase ChatThread API
      const chatThreads = await api.chatThreads.getByGame(gameId);
      setState((prev) => ({
        ...prev,
        chatsByGame: {
          ...prev.chatsByGame,
          [gameId]: chatThreads ?? [],
        },
      }));
    } catch (err) {
      console.error('Failed to load chat threads:', err);
      setError('Failed to load chats');
      setState((prev) => ({
        ...prev,
        chatsByGame: {
          ...prev.chatsByGame,
          [gameId]: [],
        },
      }));
    } finally {
      setLoading((prev) => ({ ...prev, chats: false }));
    }
  }, []);

  const loadMessages = useCallback(async (threadId: string) => {
    setLoading((prev) => ({ ...prev, messages: true }));
    setError(null);
    try {
      // SPRINT-3 #858: Messages come with ChatThread, convert to UI format
      const thread = await api.chatThreads.getById(threadId);
      if (thread) {
        const uiMessages: Message[] = thread.messages.map((msg, index) => ({
          id: `${threadId}-${index}`, // Generate temp ID (backend messages don't have IDs yet)
          role: msg.role as 'user' | 'assistant',
          content: msg.content,
          timestamp: new Date(msg.timestamp),
          // SPRINT-3 #858: Preserve optional fields from ChatThreadMessage
          backendMessageId: msg.backendMessageId,
          endpoint: msg.endpoint,
          gameId: msg.gameId,
          feedback: msg.feedback ?? null,
        }));
        setState((prev) => ({
          ...prev,
          messagesByChat: {
            ...prev.messagesByChat,
            [threadId]: uiMessages,
          },
        }));
      } else {
        setState((prev) => ({
          ...prev,
          messagesByChat: {
            ...prev.messagesByChat,
            [threadId]: [],
          },
        }));
      }
    } catch (err) {
      console.error('Failed to load messages:', err);
      setError('Failed to load messages');
      setState((prev) => ({
        ...prev,
        messagesByChat: {
          ...prev.messagesByChat,
          [threadId]: [],
        },
      }));
    } finally {
      setLoading((prev) => ({ ...prev, messages: false }));
    }
  }, []);

  // Load chats when game changes
  useEffect(() => {
    if (selectedGameId) {
      void loadChats(selectedGameId);
    }
  }, [selectedGameId, loadChats]);

  // Load messages when active chat changes
  useEffect(() => {
    if (activeChatId) {
      void loadMessages(activeChatId);
    }
  }, [activeChatId, loadMessages]);

  // ============================================================================
  // Helper Functions
  // ============================================================================

  /**
   * Enforce thread limit per game (Issue #858)
   * Archives oldest inactive thread when limit is exceeded
   */
  const enforceThreadLimit = useCallback(async (gameId: string, threads: ChatThread[]) => {
    const activeThreads = threads.filter(t => t.status === 'Active');

    if (activeThreads.length > MAX_THREADS_PER_GAME) {
      // Sort by lastMessageAt (oldest first)
      const sortedThreads = [...activeThreads].sort((a, b) =>
        new Date(a.lastMessageAt ?? a.createdAt).getTime() -
        new Date(b.lastMessageAt ?? b.createdAt).getTime()
      );

      // Find oldest thread that's not currently active
      const currentActiveId = state.activeChatIds[gameId];
      const threadToArchive = sortedThreads.find(t => t.id !== currentActiveId);

      if (threadToArchive) {
        try {
          console.log(`Auto-archiving oldest thread: ${threadToArchive.id}`);
          await api.chatThreads.close(threadToArchive.id);

          // Reload threads to reflect archived state
          await loadChats(gameId);
        } catch (err) {
          console.error('Failed to auto-archive thread:', err);
          // Non-critical error, continue anyway
        }
      }
    }
  }, [state.activeChatIds, loadChats]);

  /**
   * Generate thread title from first message (Issue #858)
   * Extracts first 50 characters, removes line breaks
   */
  const generateTitleFromMessage = (content: string): string => {
    const cleaned = content.trim().replace(/\n/g, ' ');
    return cleaned.length > 50 ? `${cleaned.substring(0, 50)}...` : cleaned;
  };

  // ============================================================================
  // Chat Operations
  // ============================================================================

  const createChat = useCallback(async () => {
    if (!selectedGameId || !selectedAgentId) return;

    setLoading((prev) => ({ ...prev, creating: true }));
    setError(null);

    try {
      // SPRINT-3 #858: Use DDD CreateChatThreadCommand
      const newThread = await api.chatThreads.create({
        gameId: selectedGameId,
        title: null, // Let backend generate default title
        initialMessage: null, // No initial message for now
      });

      if (newThread) {
        // Use functional update to access latest state (Issue #858)
        setState((prev) => {
          const currentGameThreads = prev.chatsByGame[selectedGameId] ?? [];
          const updatedThreads = [newThread, ...currentGameThreads];

          return {
            ...prev,
            chatsByGame: {
              ...prev.chatsByGame,
              [selectedGameId]: updatedThreads,
            },
            activeChatIds: {
              ...prev.activeChatIds,
              [selectedGameId]: newThread.id,
            },
            messagesByChat: {
              ...prev.messagesByChat,
              [newThread.id]: [],
            },
          };
        });

        // Enforce thread limit after state update (Issue #858)
        // Use setState callback to get latest threads
        setState((prev) => {
          const currentGameThreads = prev.chatsByGame[selectedGameId] ?? [];
          void enforceThreadLimit(selectedGameId, currentGameThreads);
          return prev; // Return unchanged state, enforceThreadLimit updates separately
        });
      }
    } catch (err) {
      console.error('Failed to create chat thread:', err);
      setError('Errore nella creazione della chat.');
    } finally {
      setLoading((prev) => ({ ...prev, creating: false }));
    }
  }, [selectedGameId, selectedAgentId, enforceThreadLimit]);

  const deleteChat = useCallback(
    async (chatId: string) => {
      if (!selectedGameId) return;
      if (!confirm('Sei sicuro di voler eliminare questa chat?')) return;

      setLoading((prev) => ({ ...prev, deleting: true }));
      setError(null);

      try {
        await api.delete(`/api/v1/chats/${chatId}`);

        setState((prev) => {
          const updatedChats = (prev.chatsByGame[selectedGameId] ?? []).filter((c) => c.id !== chatId);
          const updatedActiveChatIds = { ...prev.activeChatIds };
          if (updatedActiveChatIds[selectedGameId] === chatId) {
            updatedActiveChatIds[selectedGameId] = null;
          }

          const updatedMessages = { ...prev.messagesByChat };
          delete updatedMessages[chatId];

          return {
            ...prev,
            chatsByGame: {
              ...prev.chatsByGame,
              [selectedGameId]: updatedChats,
            },
            activeChatIds: updatedActiveChatIds,
            messagesByChat: updatedMessages,
          };
        });
      } catch (err) {
        console.error('Failed to delete chat:', err);
        setError("Errore nell'eliminazione della chat.");
      } finally {
        setLoading((prev) => ({ ...prev, deleting: false }));
      }
    },
    [selectedGameId]
  );

  const selectChat = useCallback(
    async (chatId: string) => {
      if (!selectedGameId) return;

      setState((prev) => ({
        ...prev,
        activeChatIds: {
          ...prev.activeChatIds,
          [selectedGameId]: chatId,
        },
      }));

      await loadMessages(chatId);
    },
    [selectedGameId, loadMessages]
  );

  // ============================================================================
  // Message Operations
  // ============================================================================

  const sendMessage = useCallback(
    async (content: string) => {
      if (!selectedGameId || !selectedAgentId || !content.trim()) return;

      const tempUserId = `temp-user-${Date.now()}`;
      const userMessage: Message = {
        id: tempUserId,
        role: 'user',
        content: content.trim(),
        timestamp: new Date(),
      };

      setError(null);
      setLoading((prev) => ({ ...prev, sending: true }));

      try {
        // Create thread if none exists (Hybrid approach - Issue #858)
        let threadId: string = activeChatId ?? '';
        let isNewThread = false;

        if (!threadId) {
          // Auto-generate title from first message (Issue #858)
          const autoTitle = generateTitleFromMessage(content.trim());

          // SPRINT-3 #858: Create thread using DDD
          const newThread = await api.chatThreads.create({
            gameId: selectedGameId,
            title: autoTitle,
            initialMessage: null,
          });

          if (!newThread) throw new Error('Failed to create chat thread');

          threadId = newThread.id;
          isNewThread = true;

          // Use functional update to access latest state (Issue #858)
          setState((prev) => {
            const currentGameThreads = prev.chatsByGame[selectedGameId] ?? [];
            const updatedThreads = [newThread, ...currentGameThreads];

            return {
              ...prev,
              chatsByGame: {
                ...prev.chatsByGame,
                [selectedGameId]: updatedThreads,
              },
              activeChatIds: {
                ...prev.activeChatIds,
                [selectedGameId]: threadId,
              },
              messagesByChat: {
                ...prev.messagesByChat,
                [threadId]: [],
              },
            };
          });

          // Enforce thread limit after state update (Issue #858)
          setState((prev) => {
            const currentGameThreads = prev.chatsByGame[selectedGameId] ?? [];
            void enforceThreadLimit(selectedGameId, currentGameThreads);
            return prev; // Return unchanged state, enforceThreadLimit updates separately
          });
        }

        // Optimistic update
        setState((prev) => ({
          ...prev,
          messagesByChat: {
            ...prev.messagesByChat,
            [threadId]: [...(prev.messagesByChat[threadId] ?? []), userMessage],
          },
        }));

        // SPRINT-3 #858: Add message using DDD AddMessageCommand
        await api.chatThreads.addMessage(threadId, {
          content: content.trim(),
          role: 'user',
        });

        // If this was the first message in an existing thread, update title (Issue #858)
        if (!isNewThread && messages.length === 0) {
          // This is first message in existing thread, could update title
          // For now, backend handles title management
        }

        // Note: AI response streaming will be added in future enhancement
        // For now, this sends the user message to the backend
      } catch (err) {
        console.error('Failed to send message:', err);
        setError("Errore nella comunicazione con l'agente. Riprova.");

        // Rollback optimistic update
        if (activeChatId) {
          setState((prev) => ({
            ...prev,
            messagesByChat: {
              ...prev.messagesByChat,
              [activeChatId]: (prev.messagesByChat[activeChatId] ?? []).filter((m) => m.id !== tempUserId),
            },
          }));
        }
      } finally {
        setLoading((prev) => ({ ...prev, sending: false }));
      }
    },
    [selectedGameId, selectedAgentId, activeChatId, messages.length, enforceThreadLimit]
  );

  const editMessage = useCallback(
    async (messageId: string, content: string) => {
      if (!activeChatId || !content.trim()) return;

      setLoading((prev) => ({ ...prev, updating: true }));
      setError(null);

      try {
        await api.chat.updateMessage(activeChatId, messageId, content.trim());

        // Reload messages to get updated state
        await loadMessages(activeChatId);
      } catch (err) {
        console.error('Failed to edit message:', err);
        setError("Errore nell'aggiornamento del messaggio.");
      } finally {
        setLoading((prev) => ({ ...prev, updating: false }));
      }
    },
    [activeChatId, loadMessages]
  );

  const deleteMessage = useCallback(
    async (messageId: string) => {
      if (!activeChatId) return;
      if (!confirm('Sei sicuro di voler eliminare questo messaggio?')) return;

      setLoading((prev) => ({ ...prev, deleting: true }));
      setError(null);

      try {
        await api.chat.deleteMessage(activeChatId, messageId);

        // Reload messages to get updated state
        await loadMessages(activeChatId);
      } catch (err) {
        console.error('Failed to delete message:', err);
        setError("Errore nell'eliminazione del messaggio.");
      } finally {
        setLoading((prev) => ({ ...prev, deleting: false }));
      }
    },
    [activeChatId, loadMessages]
  );

  const setMessageFeedback = useCallback(
    async (messageId: string, feedback: 'helpful' | 'not-helpful') => {
      if (!activeChatId) return;

      const targetMessage = messages.find((msg) => msg.id === messageId);
      if (!targetMessage) return;

      const previousFeedback = targetMessage.feedback ?? null;
      const nextFeedback = previousFeedback === feedback ? null : feedback;
      const endpoint = targetMessage.endpoint ?? 'qa';
      const gameId = targetMessage.gameId ?? selectedGameId ?? '';
      const feedbackMessageId = targetMessage.backendMessageId ?? messageId;

      // Optimistic update
      setState((prev) => ({
        ...prev,
        messagesByChat: {
          ...prev.messagesByChat,
          [activeChatId]: (prev.messagesByChat[activeChatId] ?? []).map((msg) =>
            msg.id === messageId ? { ...msg, feedback: nextFeedback } : msg
          ),
        },
      }));

      try {
        await api.post('/api/v1/agents/feedback', {
          messageId: feedbackMessageId,
          endpoint,
          gameId,
          feedback: nextFeedback,
        });
      } catch (err) {
        console.error('Failed to set feedback:', err);
        setError("Errore nell'invio del feedback.");

        // Revert on error
        setState((prev) => ({
          ...prev,
          messagesByChat: {
            ...prev.messagesByChat,
            [activeChatId]: (prev.messagesByChat[activeChatId] ?? []).map((msg) =>
              msg.id === messageId ? { ...msg, feedback: previousFeedback } : msg
            ),
          },
        }));
      }
    },
    [activeChatId, messages, selectedGameId]
  );

  // ============================================================================
  // Context Value
  // ============================================================================

  const value = useMemo<ChatContextValue>(
    () => ({
      chats,
      activeChat,
      activeChatId,
      messages,
      createChat,
      deleteChat,
      selectChat,
      sendMessage,
      editMessage,
      deleteMessage,
      setMessageFeedback,
      loading,
      error,
    }),
    [
      chats,
      activeChat,
      activeChatId,
      messages,
      createChat,
      deleteChat,
      selectChat,
      sendMessage,
      editMessage,
      deleteMessage,
      setMessageFeedback,
      loading,
      error,
    ]
  );

  return <ChatContext.Provider value={value}>{children}</ChatContext.Provider>;
}

// ============================================================================
// Hook
// ============================================================================

/**
 * Hook to access chat context
 * Throws error if used outside ChatProvider
 */
export function useChat(): ChatContextValue {
  const context = useContext(ChatContext);
  if (!context) {
    throw new Error('useChat must be used within ChatProvider');
  }
  return context;
}

/**
 * Backward compatibility export
 * @deprecated Use useChatContext from '@/hooks/useChatContext' instead
 */
export { useChatContext } from '@/hooks/useChatContext';
