import { useState, useCallback, useEffect } from "react";
import { api } from "../api";

/**
 * CHAT-03: Multi-game chat context switching hook
 *
 * Manages separate chat sessions for multiple games, preserving conversation
 * history independently for each game. Enables seamless switching between
 * games without losing context.
 *
 * Features:
 * - Separate chat state per game (Map-based storage)
 * - Automatic chat loading when switching games
 * - Preserves active chat ID per game
 * - Handles loading states per game
 * - Supports multiple concurrent chats per game
 */

// Type definitions
export type Message = {
  id: string;
  role: "user" | "assistant";
  content: string;
  snippets?: Snippet[];
  feedback?: "helpful" | "not-helpful" | null;
  endpoint?: string;
  gameId?: string;
  timestamp: Date;
  backendMessageId?: string;
};

export type Snippet = {
  text: string;
  source: string;
  page?: number | null;
  line?: number | null;
};

export type Chat = {
  id: string;
  gameId: string;
  gameName: string;
  agentId: string;
  agentName: string;
  startedAt: string;
  lastMessageAt: string | null;
};

export type ChatMessage = {
  id: string;
  level: string;
  message: string;
  metadataJson: string | null;
  createdAt: string;
};

export type ChatWithHistory = Chat & {
  messages: ChatMessage[];
};

/**
 * Per-game chat session state
 */
type GameChatState = {
  activeChatId: string | null;
  chats: Chat[];
  messages: Message[];
  isLoadingChats: boolean;
  isLoadingMessages: boolean;
};

/**
 * Hook return type
 */
type UseMultiGameChatReturn = {
  // Current game state
  activeChatId: string | null;
  chats: Chat[];
  messages: Message[];
  isLoadingChats: boolean;
  isLoadingMessages: boolean;

  // Actions
  switchGame: (gameId: string) => Promise<void>;
  loadChatHistory: (chatId: string) => Promise<void>;
  createNewChat: (gameId: string, agentId: string) => Promise<Chat | null>;
  deleteChat: (chatId: string) => Promise<void>;
  setMessages: React.Dispatch<React.SetStateAction<Message[]>>;
  setChats: React.Dispatch<React.SetStateAction<Chat[]>>;
  setActiveChatId: (chatId: string | null) => void;

  // State inspection
  hasGameState: (gameId: string) => boolean;
  getGameChatCount: (gameId: string) => number;
};

/**
 * Custom hook for managing multi-game chat sessions
 *
 * @param activeGameId - Currently selected game ID
 * @returns Multi-game chat state and actions
 */
export function useMultiGameChat(activeGameId: string | null): UseMultiGameChatReturn {
  // Map of game ID to chat state
  const [chatStatesByGame, setChatStatesByGame] = useState<Map<string, GameChatState>>(new Map());

  /**
   * Get or initialize chat state for a game
   */
  const getGameState = useCallback((gameId: string): GameChatState => {
    const existingState = chatStatesByGame.get(gameId);
    if (existingState) {
      return existingState;
    }

    // Initialize new game state
    const newState: GameChatState = {
      activeChatId: null,
      chats: [],
      messages: [],
      isLoadingChats: false,
      isLoadingMessages: false,
    };

    setChatStatesByGame((prev) => new Map(prev).set(gameId, newState));
    return newState;
  }, [chatStatesByGame]);

  /**
   * Update state for a specific game
   */
  const updateGameState = useCallback((gameId: string, updates: Partial<GameChatState>) => {
    setChatStatesByGame((prev) => {
      const newMap = new Map(prev);
      const currentState = newMap.get(gameId) || {
        activeChatId: null,
        chats: [],
        messages: [],
        isLoadingChats: false,
        isLoadingMessages: false,
      };
      newMap.set(gameId, { ...currentState, ...updates });
      return newMap;
    });
  }, []);

  /**
   * Load chats for a specific game
   */
  const loadChatsForGame = useCallback(async (gameId: string) => {
    updateGameState(gameId, { isLoadingChats: true });

    try {
      const chatsList = await api.get<Chat[]>(`/api/v1/chats?gameId=${gameId}`);
      updateGameState(gameId, {
        chats: chatsList ?? [],
        isLoadingChats: false,
      });
    } catch (err) {
      console.error(`Error loading chats for game ${gameId}:`, err);
      updateGameState(gameId, {
        chats: [],
        isLoadingChats: false,
      });
    }
  }, [updateGameState]);

  /**
   * Switch to a different game
   */
  const switchGame = useCallback(async (gameId: string) => {
    // Get or initialize state for this game
    const gameState = getGameState(gameId);

    // Load chats if not already loaded
    if (gameState.chats.length === 0 && !gameState.isLoadingChats) {
      await loadChatsForGame(gameId);
    }
  }, [getGameState, loadChatsForGame]);

  /**
   * Load chat history for a specific chat
   */
  const loadChatHistory = useCallback(async (chatId: string) => {
    if (!activeGameId) return;

    updateGameState(activeGameId, { isLoadingMessages: true });

    try {
      const chatWithHistory = await api.get<ChatWithHistory>(`/api/v1/chats/${chatId}`);

      if (chatWithHistory) {
        // Convert backend messages to frontend Message format
        const loadedMessages: Message[] = chatWithHistory.messages.map((msg) => {
          const role: "user" | "assistant" = msg.level === "user" ? "user" : "assistant";

          let snippets: Snippet[] | undefined;
          if (msg.metadataJson) {
            try {
              const metadata = JSON.parse(msg.metadataJson);
              snippets = metadata.snippets;
            } catch {
              // Ignore JSON parse errors
            }
          }

          return {
            id: `backend-${msg.id}`,
            role,
            content: msg.message,
            snippets,
            feedback: null,
            endpoint: "qa",
            gameId: chatWithHistory.gameId,
            timestamp: new Date(msg.createdAt),
            backendMessageId: msg.id,
          };
        });

        updateGameState(activeGameId, {
          messages: loadedMessages,
          activeChatId: chatId,
          isLoadingMessages: false,
        });
      }
    } catch (err) {
      console.error("Error loading chat history:", err);
      updateGameState(activeGameId, { isLoadingMessages: false });
    }
  }, [activeGameId, updateGameState]);

  /**
   * Create a new chat for current game
   */
  const createNewChat = useCallback(async (gameId: string, agentId: string): Promise<Chat | null> => {
    try {
      const newChat = await api.post<Chat>("/api/v1/chats", {
        gameId,
        agentId,
      });

      if (newChat) {
        // Add to chat list for this game
        const gameState = getGameState(gameId);
        updateGameState(gameId, {
          chats: [newChat, ...(gameState.chats || [])],
          activeChatId: newChat.id,
          messages: [],
        });

        return newChat;
      }
      return null;
    } catch (err) {
      console.error("Error creating chat:", err);
      return null;
    }
  }, [getGameState, updateGameState]);

  /**
   * Delete a chat
   */
  const deleteChat = useCallback(async (chatId: string) => {
    if (!activeGameId) return;

    try {
      await api.delete(`/api/v1/chats/${chatId}`);

      const gameState = getGameState(activeGameId);

      updateGameState(activeGameId, {
        chats: gameState.chats.filter((c) => c.id !== chatId),
        activeChatId: gameState.activeChatId === chatId ? null : gameState.activeChatId,
        messages: gameState.activeChatId === chatId ? [] : gameState.messages,
      });
    } catch (err) {
      console.error("Error deleting chat:", err);
    }
  }, [activeGameId, getGameState, updateGameState]);

  /**
   * Set messages for current game
   */
  const setMessages = useCallback((updater: React.SetStateAction<Message[]>) => {
    if (!activeGameId) return;

    const gameState = getGameState(activeGameId);
    const newMessages = typeof updater === 'function' ? updater(gameState.messages) : updater;

    updateGameState(activeGameId, { messages: newMessages });
  }, [activeGameId, getGameState, updateGameState]);

  /**
   * Set chats for current game
   */
  const setChats = useCallback((updater: React.SetStateAction<Chat[]>) => {
    if (!activeGameId) return;

    const gameState = getGameState(activeGameId);
    const newChats = typeof updater === 'function' ? updater(gameState.chats) : updater;

    updateGameState(activeGameId, { chats: newChats });
  }, [activeGameId, getGameState, updateGameState]);

  /**
   * Set active chat ID for current game
   */
  const setActiveChatId = useCallback((chatId: string | null) => {
    if (!activeGameId) return;
    updateGameState(activeGameId, { activeChatId: chatId });
  }, [activeGameId, updateGameState]);

  /**
   * Check if a game has initialized state
   */
  const hasGameState = useCallback((gameId: string): boolean => {
    return chatStatesByGame.has(gameId);
  }, [chatStatesByGame]);

  /**
   * Get number of chats for a game
   */
  const getGameChatCount = useCallback((gameId: string): number => {
    const gameState = chatStatesByGame.get(gameId);
    return gameState?.chats.length ?? 0;
  }, [chatStatesByGame]);

  // Load chats when active game changes
  useEffect(() => {
    if (activeGameId) {
      void switchGame(activeGameId);
    }
  }, [activeGameId, switchGame]);

  // Get current game state (or empty state if no game selected)
  const currentState = activeGameId ? getGameState(activeGameId) : {
    activeChatId: null,
    chats: [],
    messages: [],
    isLoadingChats: false,
    isLoadingMessages: false,
  };

  return {
    ...currentState,
    switchGame,
    loadChatHistory,
    createNewChat,
    deleteChat,
    setMessages,
    setChats,
    setActiveChatId,
    hasGameState,
    getGameChatCount,
  };
}
