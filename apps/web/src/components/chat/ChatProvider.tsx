/**
 * ChatProvider - Centralized state management for chat feature
 *
 * Manages all chat-related state including:
 * - Authentication
 * - Game and agent selection
 * - Chat sessions and messages
 * - UI state (loading, errors, sidebar)
 *
 * Uses Context API for state management to avoid prop drilling
 * and enable easy testing with mocked context.
 */

import React, { createContext, useContext, useState, useCallback, useMemo, useRef, useEffect } from 'react';
import { AuthUser, Game, Agent, Chat, Message } from '@/types';
import { api } from '@/lib/api';

// API response type for authentication
interface AuthResponse {
  user: AuthUser;
}

// ============================================================================
// Types
// ============================================================================

/**
 * Game-specific chat state
 * Each game maintains its own independent chat session state
 */
interface GameChatState {
  chats: Chat[];
  activeChatId: string | null;
  messages: Message[];
}

/**
 * Loading states for async operations
 */
interface LoadingState {
  games: boolean;
  agents: boolean;
  chats: boolean;
  messages: boolean;
  sending: boolean;
  creating: boolean;
  updating: boolean;
  deleting: boolean;
}

/**
 * Context value exposed to consumers
 */
export interface ChatContextValue {
  // Authentication
  authUser: AuthUser | null;

  // Game & Agent Selection
  games: Game[];
  selectedGameId: string | null;
  agents: Agent[];
  selectedAgentId: string | null;
  selectGame: (gameId: string) => Promise<void>;
  selectAgent: (agentId: string | null) => void;

  // Chat Management
  chats: Chat[];
  activeChatId: string | null;
  messages: Message[];
  createChat: () => Promise<void>;
  deleteChat: (chatId: string) => Promise<void>;
  selectChat: (chatId: string) => Promise<void>;

  // Messaging
  sendMessage: (content: string) => Promise<void>;
  setMessageFeedback: (messageId: string, feedback: 'helpful' | 'not-helpful') => Promise<void>;
  editMessage: (messageId: string, content: string) => Promise<void>;
  deleteMessage: (messageId: string) => Promise<void>;

  // UI State
  loading: LoadingState;
  errorMessage: string;
  sidebarCollapsed: boolean;
  toggleSidebar: () => void;

  // Message Edit State
  editingMessageId: string | null;
  editContent: string;
  setEditContent: (content: string) => void;
  startEditMessage: (messageId: string, content: string) => void;
  cancelEdit: () => void;
  saveEdit: () => Promise<void>;

  // Input State
  inputValue: string;
  setInputValue: (value: string) => void;
}

// ============================================================================
// Context
// ============================================================================

const ChatContext = createContext<ChatContextValue | null>(null);

/**
 * Hook to access chat context
 * Throws error if used outside ChatProvider
 */
export function useChatContext(): ChatContextValue {
  const context = useContext(ChatContext);
  if (!context) {
    throw new Error('useChatContext must be used within ChatProvider');
  }
  return context;
}

// ============================================================================
// Provider Component
// ============================================================================

interface ChatProviderProps {
  children: React.ReactNode;
}

export function ChatProvider({ children }: ChatProviderProps) {
  // ============================================================================
  // State
  // ============================================================================

  // Authentication
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);

  // Game & Agent Selection
  const [games, setGames] = useState<Game[]>([]);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [agents, setAgents] = useState<Agent[]>([]);
  const [selectedAgentId, setSelectedAgentId] = useState<string | null>(null);

  // Multi-game chat state management
  const [chatStatesByGame, setChatStatesByGame] = useState<Map<string, GameChatState>>(new Map());
  const previousSelectedGameRef = useRef<string | null>(null);

  // Derived state for current game
  const currentGameState = selectedGameId ? chatStatesByGame.get(selectedGameId) : undefined;
  const chats = currentGameState?.chats ?? [];
  const activeChatId = currentGameState?.activeChatId ?? null;
  const messages = currentGameState?.messages ?? [];

  // UI State
  const [inputValue, setInputValue] = useState<string>('');
  const [errorMessage, setErrorMessage] = useState<string>('');
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

  // Loading States
  const [loading, setLoading] = useState<LoadingState>({
    games: false,
    agents: false,
    chats: false,
    messages: false,
    sending: false,
    creating: false,
    updating: false,
    deleting: false,
  });

  // Message Edit State
  const [editingMessageId, setEditingMessageId] = useState<string | null>(null);
  const [editContent, setEditContent] = useState<string>('');

  // ============================================================================
  // Helper Functions for Game-Specific State
  // ============================================================================

  const setChats = useCallback((updater: React.SetStateAction<Chat[]>) => {
    if (!selectedGameId) return;
    setChatStatesByGame(prev => {
      const newMap = new Map(prev);
      const currentState = newMap.get(selectedGameId) || { chats: [], activeChatId: null, messages: [] };
      const newChats = typeof updater === 'function' ? updater(currentState.chats) : updater;
      newMap.set(selectedGameId, { ...currentState, chats: newChats });
      return newMap;
    });
  }, [selectedGameId]);

  const setActiveChatId = useCallback((chatId: string | null) => {
    if (!selectedGameId) return;
    setChatStatesByGame(prev => {
      const newMap = new Map(prev);
      const currentState = newMap.get(selectedGameId) || { chats: [], activeChatId: null, messages: [] };
      newMap.set(selectedGameId, { ...currentState, activeChatId: chatId });
      return newMap;
    });
  }, [selectedGameId]);

  const setMessages = useCallback((updater: React.SetStateAction<Message[]>) => {
    const targetGameId = selectedGameId ?? previousSelectedGameRef.current;
    if (!targetGameId) return;

    setChatStatesByGame(prev => {
      const newMap = new Map(prev);
      const currentState = newMap.get(targetGameId) || { chats: [], activeChatId: null, messages: [] };
      const newMessages = typeof updater === 'function' ? updater(currentState.messages) : updater;
      newMap.set(targetGameId, { ...currentState, messages: newMessages });
      return newMap;
    });
  }, [selectedGameId]);

  // ============================================================================
  // Data Loading Functions
  // ============================================================================

  const loadCurrentUser = useCallback(async () => {
    try {
      const res = await api.get<AuthResponse>('/api/v1/auth/me');
      if (res) {
        setAuthUser(res.user);
      } else {
        setAuthUser(null);
      }
    } catch {
      setAuthUser(null);
    }
  }, []);

  const loadGames = useCallback(async () => {
    setLoading(prev => ({ ...prev, games: true }));
    setErrorMessage('');
    try {
      const gamesList = await api.get<Game[]>('/api/v1/games');
      setGames(gamesList ?? []);

      // Auto-select first game if available
      if (gamesList && gamesList.length > 0 && !selectedGameId) {
        setSelectedGameId(gamesList[0].id);
      }
    } catch (err) {
      console.error('Error loading games:', err);
      setErrorMessage('Failed to load games');
      setGames([]);
    } finally {
      setLoading(prev => ({ ...prev, games: false }));
    }
  }, [selectedGameId]);

  const loadAgents = useCallback(async (gameId: string) => {
    setLoading(prev => ({ ...prev, agents: true }));
    try {
      const agentsResponse = await api.get<Agent[]>(`/api/v1/games/${gameId}/agents`);
      const agentsList = Array.isArray(agentsResponse) ? agentsResponse : [];
      setErrorMessage('');
      setAgents(agentsList);

      // Auto-select first agent if available
      if (agentsList.length > 0) {
        setSelectedAgentId(agentsList[0].id);
      }
    } catch (err) {
      console.error('Error loading agents:', err);
      setErrorMessage('Failed to load agents');
      setAgents([]);
    } finally {
      setLoading(prev => ({ ...prev, agents: false }));
    }
  }, []);

  const loadChats = useCallback(async (gameId: string) => {
    setLoading(prev => ({ ...prev, chats: true }));
    try {
      const chatsList = await api.get<Chat[]>(`/api/v1/chats?gameId=${gameId}`);

      setChats(chatsList ?? []);
      setErrorMessage('');
    } catch (err) {
      console.error('Error loading chats:', err);
      setErrorMessage('Failed to load chats');
      setChats([]);
    } finally {
      setLoading(prev => ({ ...prev, chats: false }));
    }
  }, [setChats]);

  const loadChatHistory = useCallback(async (chatId: string) => {
    setLoading(prev => ({ ...prev, messages: true }));
    try {
      const messagesResponse = await api.get<Message[]>(`/api/v1/chats/${chatId}/messages`);
      const messagesList = Array.isArray(messagesResponse) ? messagesResponse : [];
      setMessages(messagesList);
      setErrorMessage('');
    } catch (err) {
      console.error('Error loading chat history:', err);
      setErrorMessage('Failed to load messages');
      setMessages([]);
    } finally {
      setLoading(prev => ({ ...prev, messages: false }));
    }
  }, [setMessages]);

  // ============================================================================
  // API Functions
  // ============================================================================

  const selectGame = useCallback(async (gameId: string) => {
    setSelectedGameId(gameId);
    // Load agents and chats for the selected game
    await Promise.all([
      loadAgents(gameId),
      loadChats(gameId)
    ]);
  }, []);

  const selectAgent = useCallback((agentId: string | null) => {
    setSelectedAgentId(agentId);
  }, []);

  const createChat = useCallback(async () => {
    if (!selectedGameId || !selectedAgentId) return;

    setLoading(prev => ({ ...prev, creating: true }));
    setErrorMessage('');

    try {
      const newChat = await api.post<Chat>('/api/v1/chats', {
        gameId: selectedGameId,
        agentId: selectedAgentId
      });

      if (newChat) {
        // Add to chat list
        setChats((prev) => [newChat, ...prev]);

        // Set as active chat
        setActiveChatId(newChat.id);
        setMessages([]);
      }
    } catch (err) {
      console.error('Error creating chat:', err);
      setErrorMessage('Errore nella creazione della chat.');
    } finally {
      setLoading(prev => ({ ...prev, creating: false }));
    }
  }, [selectedGameId, selectedAgentId, setChats, setActiveChatId, setMessages]);

  const deleteChat = useCallback(async (chatId: string) => {
    if (!confirm('Sei sicuro di voler eliminare questa chat?')) {
      return;
    }

    setLoading(prev => ({ ...prev, deleting: true }));
    setErrorMessage('');

    try {
      await api.delete(`/api/v1/chats/${chatId}`);

      // Remove from list
      setChats((prev) => prev.filter((c) => c.id !== chatId));

      // Clear if it was the active chat
      if (activeChatId === chatId) {
        setActiveChatId(null);
        setMessages([]);
      }
    } catch (err) {
      console.error('Error deleting chat:', err);
      setErrorMessage("Errore nell'eliminazione della chat.");
    } finally {
      setLoading(prev => ({ ...prev, deleting: false }));
    }
  }, [activeChatId, setChats, setActiveChatId, setMessages]);

  const selectChat = useCallback(async (chatId: string) => {
    setActiveChatId(chatId);
    await loadChatHistory(chatId);
  }, [setActiveChatId, loadChatHistory]);

  const sendMessage = useCallback(async (content: string) => {
    if (!selectedGameId || !selectedAgentId || !content.trim()) return;

    const tempUserId = `temp-user-${Date.now()}`;
    const userMessage: Message = {
      id: tempUserId,
      role: 'user',
      content: content.trim(),
      timestamp: new Date()
    };

    // Add user message optimistically
    setMessages((prev) => [...prev, userMessage]);
    setInputValue('');
    setErrorMessage('');
    setLoading(prev => ({ ...prev, sending: true }));

    try {
      // Create chat if none exists
      let chatId = activeChatId;
      if (!chatId) {
        const newChat = await api.post<Chat>('/api/v1/chats', {
          gameId: selectedGameId,
          agentId: selectedAgentId
        });

        if (newChat) {
          chatId = newChat.id;
          setActiveChatId(chatId);
          setChats((prev) => [newChat, ...prev]);
        } else {
          throw new Error('Failed to create chat');
        }
      }

      // Note: Streaming integration will be added in future enhancement
      // For now, this creates the chat and user message
      // The actual AI response streaming should be handled separately
    } catch (err) {
      console.error('Error sending message:', err);
      setErrorMessage('Errore nella comunicazione con l\'agente. Riprova.');

      // Remove the user message if the request failed
      setMessages((prev) => prev.filter((m) => m.id !== tempUserId));
    } finally {
      setLoading(prev => ({ ...prev, sending: false }));
    }
  }, [selectedGameId, selectedAgentId, activeChatId, setMessages, setInputValue, setActiveChatId, setChats]);

  const setMessageFeedback = useCallback(async (messageId: string, feedback: 'helpful' | 'not-helpful') => {
    const targetMessage = messages.find((msg) => msg.id === messageId);
    if (!targetMessage) return;

    const previousFeedback = targetMessage.feedback ?? null;
    const nextFeedback = previousFeedback === feedback ? null : feedback;
    const endpoint = targetMessage.endpoint ?? 'qa';
    const gameId = targetMessage.gameId ?? selectedGameId ?? '';

    // Use backend message ID if available
    const feedbackMessageId = targetMessage.backendMessageId ?? messageId;

    // Optimistic update
    setMessages((prev) =>
      prev.map((msg) => (msg.id === messageId ? { ...msg, feedback: nextFeedback } : msg))
    );

    try {
      await api.post('/api/v1/agents/feedback', {
        messageId: feedbackMessageId,
        endpoint,
        gameId,
        feedback: nextFeedback
      });
    } catch (err) {
      console.error('Error setting feedback:', err);
      // Revert on error
      setMessages((prev) =>
        prev.map((msg) => (msg.id === messageId ? { ...msg, feedback: previousFeedback } : msg))
      );
      setErrorMessage('Errore nell\'invio del feedback.');
    }
  }, [messages, selectedGameId, setMessages]);

  const editMessage = useCallback(async (messageId: string, content: string) => {
    if (!activeChatId || !content.trim()) return;

    setLoading(prev => ({ ...prev, updating: true }));
    setErrorMessage('');

    try {
      await api.chat.updateMessage(activeChatId, messageId, content.trim());

      // Reload chat history to get updated messages and invalidation states
      await loadChatHistory(activeChatId);
    } catch (err) {
      console.error('Error updating message:', err);
      setErrorMessage('Errore nell\'aggiornamento del messaggio.');
    } finally {
      setLoading(prev => ({ ...prev, updating: false }));
    }
  }, [activeChatId, loadChatHistory]);

  const deleteMessage = useCallback(async (messageId: string) => {
    if (!activeChatId) return;

    if (!confirm('Sei sicuro di voler eliminare questo messaggio?')) {
      return;
    }

    setLoading(prev => ({ ...prev, deleting: true }));
    setErrorMessage('');

    try {
      await api.chat.deleteMessage(activeChatId, messageId);

      // Reload chat history to get updated messages and invalidation states
      await loadChatHistory(activeChatId);
    } catch (err) {
      console.error('Error deleting message:', err);
      setErrorMessage('Errore nell\'eliminazione del messaggio.');
    } finally {
      setLoading(prev => ({ ...prev, deleting: false }));
    }
  }, [activeChatId, loadChatHistory]);

  // ============================================================================
  // UI Actions
  // ============================================================================

  const toggleSidebar = useCallback(() => {
    setSidebarCollapsed(prev => !prev);
  }, []);

  const startEditMessage = useCallback((messageId: string, content: string) => {
    setEditingMessageId(messageId);
    setEditContent(content);
  }, []);

  const cancelEdit = useCallback(() => {
    setEditingMessageId(null);
    setEditContent('');
  }, []);

  const saveEdit = useCallback(async () => {
    if (!editingMessageId) return;
    await editMessage(editingMessageId, editContent);
    cancelEdit();
  }, [editingMessageId, editContent, editMessage, cancelEdit]);

  // ============================================================================
  // Effects
  // ============================================================================

  // Track previous selected game for message updates
  useEffect(() => {
    previousSelectedGameRef.current = selectedGameId;
  }, [selectedGameId]);

  // ============================================================================
  // Context Value
  // ============================================================================

  const contextValue = useMemo<ChatContextValue>(() => ({
    // Authentication
    authUser,

    // Game & Agent Selection
    games,
    selectedGameId,
    agents,
    selectedAgentId,
    selectGame,
    selectAgent,

    // Chat Management
    chats,
    activeChatId,
    messages,
    createChat,
    deleteChat,
    selectChat,

    // Messaging
    sendMessage,
    setMessageFeedback,
    editMessage,
    deleteMessage,

    // UI State
    loading,
    errorMessage,
    sidebarCollapsed,
    toggleSidebar,

    // Message Edit State
    editingMessageId,
    editContent,
    setEditContent,
    startEditMessage,
    cancelEdit,
    saveEdit,

    // Input State
    inputValue,
    setInputValue,
  }), [
    authUser,
    games,
    selectedGameId,
    agents,
    selectedAgentId,
    selectGame,
    selectAgent,
    chats,
    activeChatId,
    messages,
    createChat,
    deleteChat,
    selectChat,
    sendMessage,
    setMessageFeedback,
    editMessage,
    deleteMessage,
    loading,
    errorMessage,
    sidebarCollapsed,
    toggleSidebar,
    editingMessageId,
    editContent,
    setEditContent,
    startEditMessage,
    cancelEdit,
    saveEdit,
    inputValue,
  ]);

  return (
    <ChatContext.Provider value={contextValue}>
      {children}
    </ChatContext.Provider>
  );
}
