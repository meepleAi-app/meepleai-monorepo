/**
 * ChatProvider - Chat state management (REFACTORED)
 *
 * Manages:
 * - Chat sessions per game
 * - Messages per chat
 * - Message operations (send, edit, delete, feedback)
 *
 * Key improvements over original:
 * - Normalized state structure (no Map, serializable)
 * - localStorage persistence
 * - No useRef anti-patterns
 * - No disabled ESLint rules
 * - Focused responsibility (chat operations only)
 *
 * Nested under GameProvider in provider hierarchy
 */

import React, { createContext, useContext, useState, useCallback, useMemo, useEffect, PropsWithChildren } from 'react';
import { Chat, Message } from '@/types';
import { api } from '@/lib/api';
import { useGame } from '@/components/game/GameProvider';

// ============================================================================
// Types
// ============================================================================

/**
 * Normalized chat state structure (serializable for localStorage)
 */
interface ChatState {
  chatsByGame: Record<string, Chat[]>;  // Chats indexed by gameId
  activeChatIds: Record<string, string | null>; // Active chat per game (nullable)
  messagesByChat: Record<string, Message[]>; // Messages indexed by chatId
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
  // Current game's chats (derived from normalized state)
  chats: Chat[];
  activeChat: Chat | null;
  activeChatId: string | null;
  messages: Message[];

  // Chat operations
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
  // Data Loading Functions
  // ============================================================================

  const loadChats = useCallback(async (gameId: string) => {
    setLoading((prev) => ({ ...prev, chats: true }));
    setError(null);
    try {
      const chatsList = await api.get<Chat[]>(`/api/v1/chats?gameId=${gameId}`);
      setState((prev) => ({
        ...prev,
        chatsByGame: {
          ...prev.chatsByGame,
          [gameId]: chatsList ?? [],
        },
      }));
    } catch (err) {
      console.error('Failed to load chats:', err);
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

  const loadMessages = useCallback(async (chatId: string) => {
    setLoading((prev) => ({ ...prev, messages: true }));
    setError(null);
    try {
      const messagesResponse = await api.get<Message[]>(`/api/v1/chats/${chatId}/messages`);
      const messagesList = Array.isArray(messagesResponse) ? messagesResponse : [];
      setState((prev) => ({
        ...prev,
        messagesByChat: {
          ...prev.messagesByChat,
          [chatId]: messagesList,
        },
      }));
    } catch (err) {
      console.error('Failed to load messages:', err);
      setError('Failed to load messages');
      setState((prev) => ({
        ...prev,
        messagesByChat: {
          ...prev.messagesByChat,
          [chatId]: [],
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
  // Chat Operations
  // ============================================================================

  const createChat = useCallback(async () => {
    if (!selectedGameId || !selectedAgentId) return;

    setLoading((prev) => ({ ...prev, creating: true }));
    setError(null);

    try {
      const newChat = await api.post<Chat>('/api/v1/chats', {
        gameId: selectedGameId,
        agentId: selectedAgentId,
      });

      if (newChat) {
        setState((prev) => ({
          ...prev,
          chatsByGame: {
            ...prev.chatsByGame,
            [selectedGameId]: [newChat, ...(prev.chatsByGame[selectedGameId] ?? [])],
          },
          activeChatIds: {
            ...prev.activeChatIds,
            [selectedGameId]: newChat.id,
          },
          messagesByChat: {
            ...prev.messagesByChat,
            [newChat.id]: [],
          },
        }));
      }
    } catch (err) {
      console.error('Failed to create chat:', err);
      setError('Errore nella creazione della chat.');
    } finally {
      setLoading((prev) => ({ ...prev, creating: false }));
    }
  }, [selectedGameId, selectedAgentId]);

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
        // Create chat if none exists
        let chatId = activeChatId;
        if (!chatId) {
          const newChat = await api.post<Chat>('/api/v1/chats', {
            gameId: selectedGameId,
            agentId: selectedAgentId,
          });

          if (!newChat) throw new Error('Failed to create chat');

          const newChatId = newChat.id;
          chatId = newChatId;
          setState((prev) => ({
            ...prev,
            chatsByGame: {
              ...prev.chatsByGame,
              [selectedGameId]: [newChat, ...(prev.chatsByGame[selectedGameId] ?? [])],
            },
            activeChatIds: {
              ...prev.activeChatIds,
              [selectedGameId]: newChatId,
            },
            messagesByChat: {
              ...prev.messagesByChat,
              [newChatId]: [],
            },
          }));
        }

        // Optimistic update
        setState((prev) => ({
          ...prev,
          messagesByChat: {
            ...prev.messagesByChat,
            [chatId]: [...(prev.messagesByChat[chatId] ?? []), userMessage],
          },
        }));

        // Note: Streaming integration will be added in future enhancement
        // For now, this creates the chat and user message
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
    [selectedGameId, selectedAgentId, activeChatId]
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
