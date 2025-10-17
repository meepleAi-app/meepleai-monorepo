import { FormEvent, useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { api } from "../lib/api";
import { useChatStreaming } from "../lib/hooks/useChatStreaming";

// Type definitions
type AuthUser = {
  id: string;
  email: string;
  displayName?: string | null;
  role: string;
};

type AuthResponse = {
  user: AuthUser;
  expiresAt: string;
};

type Game = {
  id: string;
  name: string;
};

type Agent = {
  id: string;
  gameId: string;
  name: string;
  kind: string;
  createdAt: string;
};

type Chat = {
  id: string;
  gameId: string;
  gameName: string;
  agentId: string;
  agentName: string;
  startedAt: string;
  lastMessageAt: string | null;
};

type ChatMessage = {
  id: string;
  level: string;
  message: string;
  metadataJson: string | null;
  createdAt: string;
};

type ChatWithHistory = Chat & {
  messages: ChatMessage[];
};

type Snippet = {
  text: string;
  source: string;
  page?: number | null;
  line?: number | null;
};

type Message = {
  id: string;
  role: "user" | "assistant";
  content: string;
  snippets?: Snippet[];
  feedback?: "helpful" | "not-helpful" | null;
  endpoint?: string;
  gameId?: string;
  timestamp: Date;
  backendMessageId?: string; // Link to backend chat_log.id
};

type QaResponse = {
  answer: string;
  snippets?: Snippet[];
  messageId?: string; // Backend message ID for feedback
};

// Utility functions
const formatSnippets = (snippets?: Snippet[]): Snippet[] =>
  (snippets ?? []).map(({ text, source, page = null, line = null }) => ({
    text,
    source,
    page,
    line
  }));

const getSnippetLabel = (snippet: Snippet): string => {
  const baseLabel = snippet.source;
  if (snippet.page !== null && snippet.page !== undefined) {
    return `${baseLabel} (Pagina ${snippet.page})`;
  }
  return baseLabel;
};

const formatChatPreview = (chat: Chat): string => {
  const date = new Date(chat.lastMessageAt ?? chat.startedAt);
  return `${chat.agentName} - ${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
};

export default function ChatPage() {
  // Authentication
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);

  // Game and Agent selection
  const [games, setGames] = useState<Game[]>([]);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [agents, setAgents] = useState<Agent[]>([]);
  const [selectedAgentId, setSelectedAgentId] = useState<string | null>(null);

  // CHAT-03: Multi-game chat management with per-game state preservation
  type GameChatState = {
    chats: Chat[];
    activeChatId: string | null;
    messages: Message[];
  };
  const [chatStatesByGame, setChatStatesByGame] = useState<Map<string, GameChatState>>(new Map());

  // Derived state for current game
  const currentGameState = selectedGameId ? chatStatesByGame.get(selectedGameId) : undefined;
  const chats = currentGameState?.chats ?? [];
  const activeChatId = currentGameState?.activeChatId ?? null;
  const messages = currentGameState?.messages ?? [];

  // Helper functions for game-specific state updates
  const setChats = (updater: React.SetStateAction<Chat[]>) => {
    if (!selectedGameId) return;
    setChatStatesByGame(prev => {
      const newMap = new Map(prev);
      const currentState = newMap.get(selectedGameId) || { chats: [], activeChatId: null, messages: [] };
      const newChats = typeof updater === 'function' ? updater(currentState.chats) : updater;
      newMap.set(selectedGameId, { ...currentState, chats: newChats });
      return newMap;
    });
  };

  const setActiveChatId = (chatId: string | null) => {
    if (!selectedGameId) return;
    setChatStatesByGame(prev => {
      const newMap = new Map(prev);
      const currentState = newMap.get(selectedGameId) || { chats: [], activeChatId: null, messages: [] };
      newMap.set(selectedGameId, { ...currentState, activeChatId: chatId });
      return newMap;
    });
  };

  const setMessages = (updater: React.SetStateAction<Message[]>) => {
    if (!selectedGameId) return;
    setChatStatesByGame(prev => {
      const newMap = new Map(prev);
      const currentState = newMap.get(selectedGameId) || { chats: [], activeChatId: null, messages: [] };
      const newMessages = typeof updater === 'function' ? updater(currentState.messages) : updater;
      newMap.set(selectedGameId, { ...currentState, messages: newMessages });
      return newMap;
    });
  };

  // UI state
  const [inputValue, setInputValue] = useState<string>("");
  const [errorMessage, setErrorMessage] = useState<string>("");
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

  // Loading states
  const [isLoadingGames, setIsLoadingGames] = useState(false);
  const [isLoadingAgents, setIsLoadingAgents] = useState(false);
  const [isLoadingChats, setIsLoadingChats] = useState(false);
  const [isLoadingMessages, setIsLoadingMessages] = useState(false);
  const [isSendingMessage, setIsSendingMessage] = useState(false);
  const [isCreatingChat, setIsCreatingChat] = useState(false);

  // Streaming hook
  const [streamingState, streamingControls] = useChatStreaming({
    onComplete: useCallback(
      (answer: string, snippets: Snippet[], metadata: { totalTokens: number; confidence: number | null }) => {
        // Add completed assistant message to chat
        const assistantMessage: Message = {
          id: `stream-${Date.now()}`,
          role: "assistant",
          content: answer,
          snippets,
          feedback: null,
          endpoint: "qa",
          gameId: selectedGameId ?? undefined,
          timestamp: new Date(),
        };

        setMessages((prev) => [...prev, assistantMessage]);

        // Update chat's lastMessageAt
        if (activeChatId) {
          setChats((prev) =>
            prev.map((c) =>
              c.id === activeChatId ? { ...c, lastMessageAt: new Date().toISOString() } : c
            )
          );
        }
      },
      [selectedGameId, activeChatId]
    ),
    onError: useCallback((error: string) => {
      setErrorMessage(error);
      // Remove the temporary user message on error
      setMessages((prev) => prev.filter((m) => !m.id.startsWith("temp-user-")));
    }, []),
  });

  // Load current user on mount
  useEffect(() => {
    void loadCurrentUser();
  }, []);

  // Load games after user is authenticated
  useEffect(() => {
    if (authUser) {
      void loadGames();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authUser]);

  // CHAT-03: Load agents and chats when game is selected (preserve per-game state)
  useEffect(() => {
    if (selectedGameId) {
      void loadAgents(selectedGameId);

      // Only load chats if this game hasn't been loaded yet
      const gameState = chatStatesByGame.get(selectedGameId);
      if (!gameState) {
        void loadChats(selectedGameId);
      }
      // Note: We do NOT reset activeChatId or messages - they're preserved per-game

      // Reset agent selection when switching games
      setSelectedAgentId(null);
    } else {
      setAgents([]);
      setSelectedAgentId(null);
    }
  }, [selectedGameId, chatStatesByGame]);

  const loadCurrentUser = async () => {
    try {
      const res = await api.get<AuthResponse>("/api/v1/auth/me");
      if (res) {
        setAuthUser(res.user);
      } else {
        setAuthUser(null);
      }
    } catch {
      setAuthUser(null);
    }
  };

  const loadGames = async () => {
    setIsLoadingGames(true);
    setErrorMessage("");
    try {
      const gamesList = await api.get<Game[]>("/api/v1/games");
      setGames(gamesList ?? []);

      // Auto-select first game if available
      if (gamesList && gamesList.length > 0 && !selectedGameId) {
        setSelectedGameId(gamesList[0].id);
      }
    } catch (err) {
      console.error("Error loading games:", err);
      setErrorMessage("Errore nel caricamento dei giochi.");
      setGames([]);
    } finally {
      setIsLoadingGames(false);
    }
  };

  const loadAgents = async (gameId: string) => {
    setIsLoadingAgents(true);
    setErrorMessage("");
    try {
      const agentsList = await api.get<Agent[]>(`/api/v1/games/${gameId}/agents`);
      setAgents(agentsList ?? []);

      // Auto-select first agent if available
      if (agentsList && agentsList.length > 0) {
        setSelectedAgentId(agentsList[0].id);
      }
    } catch (err) {
      console.error("Error loading agents:", err);
      setErrorMessage("Errore nel caricamento degli agenti.");
      setAgents([]);
    } finally {
      setIsLoadingAgents(false);
    }
  };

  const loadChats = async (gameId: string) => {
    setIsLoadingChats(true);
    try {
      const chatsList = await api.get<Chat[]>(`/api/v1/chats?gameId=${gameId}`);

      // CHAT-03: Update chats for specific game
      setChatStatesByGame(prev => {
        const newMap = new Map(prev);
        const currentState = newMap.get(gameId) || { chats: [], activeChatId: null, messages: [] };
        newMap.set(gameId, { ...currentState, chats: chatsList ?? [] });
        return newMap;
      });
    } catch (err) {
      console.error("Error loading chats:", err);
      // Don't show error to user - empty chat list is acceptable
      setChatStatesByGame(prev => {
        const newMap = new Map(prev);
        const currentState = newMap.get(gameId) || { chats: [], activeChatId: null, messages: [] };
        newMap.set(gameId, { ...currentState, chats: [] });
        return newMap;
      });
    } finally {
      setIsLoadingChats(false);
    }
  };

  const loadChatHistory = async (chatId: string) => {
    setIsLoadingMessages(true);
    setErrorMessage("");
    try {
      const chatWithHistory = await api.get<ChatWithHistory>(`/api/v1/chats/${chatId}`);

      if (chatWithHistory) {
        // Convert backend messages to frontend Message format
        const loadedMessages: Message[] = chatWithHistory.messages.map((msg) => {
          // Determine role based on message level
          const role: "user" | "assistant" = msg.level === "user" ? "user" : "assistant";

          // Parse metadata for snippets (if present)
          let snippets: Snippet[] | undefined;
          if (msg.metadataJson) {
            try {
              const metadata = JSON.parse(msg.metadataJson);
              snippets = formatSnippets(metadata.snippets);
            } catch {
              // Ignore JSON parse errors
            }
          }

          return {
            id: `backend-${msg.id}`,
            role,
            content: msg.message,
            snippets,
            feedback: null, // Feedback is not stored in backend yet
            endpoint: "qa",
            gameId: chatWithHistory.gameId,
            timestamp: new Date(msg.createdAt),
            backendMessageId: msg.id
          };
        });

        // CHAT-03: Update messages for the chat's game
        const chatGameId = chatWithHistory.gameId;
        setChatStatesByGame(prev => {
          const newMap = new Map(prev);
          const currentState = newMap.get(chatGameId) || { chats: [], activeChatId: null, messages: [] };
          newMap.set(chatGameId, { ...currentState, messages: loadedMessages, activeChatId: chatId });
          return newMap;
        });

        // Update selected game and agent to match the chat
        if (selectedGameId !== chatWithHistory.gameId) {
          setSelectedGameId(chatWithHistory.gameId);
        }
        if (selectedAgentId !== chatWithHistory.agentId) {
          setSelectedAgentId(chatWithHistory.agentId);
        }
      }
    } catch (err) {
      console.error("Error loading chat history:", err);
      setErrorMessage("Errore nel caricamento della cronologia chat.");
    } finally {
      setIsLoadingMessages(false);
    }
  };

  const createNewChat = async () => {
    if (!selectedGameId || !selectedAgentId) {
      setErrorMessage("Seleziona un gioco e un agente prima di creare una chat.");
      return;
    }

    setIsCreatingChat(true);
    setErrorMessage("");
    try {
      const newChat = await api.post<Chat>("/api/v1/chats", {
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
      console.error("Error creating chat:", err);
      setErrorMessage("Errore nella creazione della chat.");
    } finally {
      setIsCreatingChat(false);
    }
  };

  const deleteChat = async (chatId: string) => {
    if (!confirm("Sei sicuro di voler eliminare questa chat?")) {
      return;
    }

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
      console.error("Error deleting chat:", err);
      setErrorMessage("Errore nell'eliminazione della chat.");
    }
  };

  const sendMessage = async (e: FormEvent) => {
    e.preventDefault();

    if (!authUser) {
      setErrorMessage("Devi effettuare l'accesso per chattare.");
      return;
    }

    if (!selectedGameId || !selectedAgentId) {
      setErrorMessage("Seleziona un gioco e un agente prima di inviare un messaggio.");
      return;
    }

    if (!inputValue.trim()) {
      return;
    }

    const userMessageContent = inputValue;
    const tempUserId = `temp-user-${Date.now()}`;

    const userMessage: Message = {
      id: tempUserId,
      role: "user",
      content: userMessageContent,
      timestamp: new Date()
    };

    setMessages((prev) => [...prev, userMessage]);
    setInputValue("");
    setErrorMessage("");
    setIsSendingMessage(true);

    try {
      // Create chat if none exists
      let chatId = activeChatId;
      if (!chatId) {
        const newChat = await api.post<Chat>("/api/v1/chats", {
          gameId: selectedGameId,
          agentId: selectedAgentId
        });

        if (newChat) {
          chatId = newChat.id;
          setActiveChatId(chatId);
          setChats((prev) => [newChat, ...prev]);
        } else {
          throw new Error("Failed to create chat");
        }
      }

      // Start streaming response
      streamingControls.startStreaming(selectedGameId, userMessageContent, chatId);
    } catch (err) {
      console.error("Error sending message:", err);
      setErrorMessage("Errore nella comunicazione con l'agente. Riprova.");

      // Remove the user message if the request failed
      setMessages((prev) => prev.filter((m) => m.id !== tempUserId));
    } finally {
      setIsSendingMessage(false);
    }
  };

  const setFeedback = async (messageId: string, feedback: "helpful" | "not-helpful") => {
    if (!authUser) {
      return;
    }

    const targetMessage = messages.find((msg) => msg.id === messageId);
    if (!targetMessage) {
      return;
    }

    const previousFeedback = targetMessage.feedback ?? null;
    const nextFeedback = previousFeedback === feedback ? null : feedback;
    const endpoint = targetMessage.endpoint ?? "qa";
    const gameId = targetMessage.gameId ?? selectedGameId ?? "";

    // Use backend message ID if available, otherwise use frontend ID
    const feedbackMessageId = targetMessage.backendMessageId ?? messageId;

    // Optimistic update
    setMessages((prev) =>
      prev.map((msg) => (msg.id === messageId ? { ...msg, feedback: nextFeedback } : msg))
    );

    try {
      await api.post("/api/v1/agents/feedback", {
        messageId: feedbackMessageId,
        endpoint,
        outcome: nextFeedback,
        userId: authUser.id,
        gameId
      });
    } catch (error) {
      console.error("Error submitting feedback:", error);
      // Revert on error
      setMessages((prev) =>
        prev.map((msg) => (msg.id === messageId ? { ...msg, feedback: previousFeedback } : msg))
      );
    }
  };

  // Render login required state
  if (!authUser) {
    return (
      <main id="main-content" style={{ padding: 24, maxWidth: 900, margin: "0 auto", fontFamily: "sans-serif" }}>
        <Link href="/" style={{ color: "#0070f3", textDecoration: "none" }}>
          ← Torna alla Home
        </Link>
        <div
          style={{
            marginTop: 24,
            padding: 32,
            textAlign: "center",
            border: "1px solid #dadce0",
            borderRadius: 8
          }}
        >
          <h2>Accesso richiesto</h2>
          <p>Devi effettuare l&apos;accesso per utilizzare la chat.</p>
          <Link
            href="/"
            style={{
              display: "inline-block",
              marginTop: 16,
              padding: "8px 16px",
              background: "#0070f3",
              color: "white",
              textDecoration: "none",
              borderRadius: 4
            }}
          >
            Vai al Login
          </Link>
        </div>
      </main>
    );
  }

  // Main chat interface
  return (
    <main
      id="main-content"
      style={{
        display: "flex",
        height: "100vh",
        fontFamily: "sans-serif",
        overflow: "hidden"
      }}
    >
      {/* Sidebar */}
      <aside
        aria-label="Chat sidebar with game selection and chat history"
        style={{
          width: sidebarCollapsed ? 0 : 320,
          minWidth: sidebarCollapsed ? 0 : 320,
          background: "#f8f9fa",
          borderRight: "1px solid #dadce0",
          display: "flex",
          flexDirection: "column",
          overflow: "hidden",
          transition: "width 0.3s ease, min-width 0.3s ease"
        }}
      >
        {/* Sidebar Header */}
        <div style={{ padding: 16, borderBottom: "1px solid #dadce0" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
            <h2 style={{ margin: 0, fontSize: 18 }}>MeepleAI Chat</h2>
            {/* CHAT-03: Game context badge */}
            {selectedGameId && (
              <div
                style={{
                  padding: "4px 12px",
                  background: "#e8f0fe",
                  color: "#1a73e8",
                  borderRadius: 12,
                  fontSize: 11,
                  fontWeight: 600,
                  border: "1px solid #1a73e8"
                }}
                title={`Currently chatting about: ${games.find(g => g.id === selectedGameId)?.name ?? "Unknown game"}`}
                aria-label={`Active game context: ${games.find(g => g.id === selectedGameId)?.name ?? "Unknown game"}`}
              >
                {games.find(g => g.id === selectedGameId)?.name ?? "..."}
              </div>
            )}
          </div>

          {/* Game Selector */}
          <div style={{ marginBottom: 12 }}>
            <label
              htmlFor="gameSelect"
              style={{ display: "block", marginBottom: 6, fontWeight: 500, fontSize: 13 }}
            >
              Cambia Gioco:
            </label>
            <select
              id="gameSelect"
              value={selectedGameId ?? ""}
              onChange={(e) => setSelectedGameId(e.target.value || null)}
              disabled={isLoadingGames}
              style={{
                width: "100%",
                padding: 8,
                fontSize: 13,
                border: "1px solid #dadce0",
                borderRadius: 4,
                background: "white"
              }}
            >
              <option value="">Seleziona un gioco...</option>
              {games.map((game) => (
                <option key={game.id} value={game.id}>
                  {game.name}
                </option>
              ))}
            </select>
          </div>

          {/* Agent Selector */}
          <div style={{ marginBottom: 12 }}>
            <label
              htmlFor="agentSelect"
              style={{ display: "block", marginBottom: 6, fontWeight: 500, fontSize: 13 }}
            >
              Agente:
            </label>
            <select
              id="agentSelect"
              value={selectedAgentId ?? ""}
              onChange={(e) => setSelectedAgentId(e.target.value || null)}
              disabled={isLoadingAgents || !selectedGameId}
              style={{
                width: "100%",
                padding: 8,
                fontSize: 13,
                border: "1px solid #dadce0",
                borderRadius: 4,
                background: "white"
              }}
            >
              <option value="">Seleziona un agente...</option>
              {agents.map((agent) => (
                <option key={agent.id} value={agent.id}>
                  {agent.name}
                </option>
              ))}
            </select>
          </div>

          {/* New Chat Button */}
          <button
            onClick={() => void createNewChat()}
            disabled={!selectedGameId || !selectedAgentId || isCreatingChat}
            aria-label="Create new chat"
            style={{
              width: "100%",
              padding: 10,
              background: !selectedGameId || !selectedAgentId || isCreatingChat ? "#dadce0" : "#1a73e8",
              color: "white",
              border: "none",
              borderRadius: 4,
              fontSize: 14,
              fontWeight: 500,
              cursor: !selectedGameId || !selectedAgentId || isCreatingChat ? "not-allowed" : "pointer"
            }}
          >
            {isCreatingChat ? "Creazione..." : "+ Nuova Chat"}
          </button>
        </div>

        {/* Chat List */}
        <nav aria-label="Chat history" style={{ flex: 1, overflowY: "auto", padding: 8 }}>
          {isLoadingChats ? (
            <div role="status" aria-live="polite" style={{ padding: 16, textAlign: "center", color: "#64748b", fontSize: 13 }}>
              Caricamento chat...
            </div>
          ) : chats.length === 0 ? (
            <div style={{ padding: 16, textAlign: "center", color: "#64748b", fontSize: 13 }}>
              Nessuna chat. Creane una nuova!
            </div>
          ) : (
            <ul role="list" style={{ listStyle: "none", margin: 0, padding: 0 }}>
              {chats.map((chat) => (
                <li
                  key={chat.id}
                  style={{
                    padding: 12,
                    marginBottom: 8,
                    background: activeChatId === chat.id ? "#e8f0fe" : "white",
                    border: `1px solid ${activeChatId === chat.id ? "#1a73e8" : "#dadce0"}`,
                    borderRadius: 4,
                    cursor: "pointer",
                    position: "relative",
                    fontSize: 13
                  }}
                  onClick={() => void loadChatHistory(chat.id)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      void loadChatHistory(chat.id);
                    }
                  }}
                  aria-current={activeChatId === chat.id ? "true" : undefined}
                >
                  <div style={{ fontWeight: 500, marginBottom: 4 }}>{chat.agentName}</div>
                  <div style={{ fontSize: 11, color: "#64748b" }}>{formatChatPreview(chat)}</div>
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      void deleteChat(chat.id);
                    }}
                    aria-label={`Delete chat with ${chat.agentName}`}
                    style={{
                      position: "absolute",
                      top: 8,
                      right: 8,
                      padding: "4px 8px",
                      background: "#ea4335",
                      color: "white",
                      border: "none",
                      borderRadius: 3,
                      fontSize: 11,
                      cursor: "pointer"
                    }}
                    title="Elimina chat"
                  >
                    🗑️
                  </button>
                </li>
              ))}
            </ul>
          )}
        </nav>
      </aside>

      {/* Main Content */}
      <div style={{ flex: 1, display: "flex", flexDirection: "column", overflow: "hidden" }}>
        {/* Header */}
        <div
          style={{
            padding: 16,
            borderBottom: "1px solid #dadce0",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            background: "white"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
            <button
              onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
              aria-label={sidebarCollapsed ? "Show sidebar" : "Hide sidebar"}
              aria-expanded={!sidebarCollapsed}
              style={{
                padding: "8px 12px",
                background: "#f1f3f4",
                border: "none",
                borderRadius: 4,
                cursor: "pointer",
                fontSize: 18
              }}
              title={sidebarCollapsed ? "Mostra sidebar" : "Nascondi sidebar"}
            >
              {sidebarCollapsed ? "☰" : "✕"}
            </button>
            <div>
              <h1 style={{ margin: 0, fontSize: 20 }}>
                {activeChatId
                  ? chats.find((c) => c.id === activeChatId)?.agentName ?? "Chat"
                  : "Seleziona o crea una chat"}
              </h1>
              <p style={{ margin: "4px 0 0 0", color: "#64748b", fontSize: 13 }}>
                {selectedGameId
                  ? games.find((g) => g.id === selectedGameId)?.name ?? ""
                  : "Nessun gioco selezionato"}
              </p>
            </div>
          </div>
          <Link
            href="/"
            style={{
              padding: "8px 16px",
              background: "#1a73e8",
              color: "white",
              textDecoration: "none",
              borderRadius: 4,
              fontSize: 14
            }}
          >
            Home
          </Link>
        </div>

        {/* Error Message */}
        {errorMessage && (
          <div
            role="alert"
            aria-live="polite"
            style={{
              margin: 16,
              padding: 12,
              background: "#fce8e6",
              color: "#d93025",
              borderRadius: 4,
              fontSize: 14
            }}
          >
            {errorMessage}
          </div>
        )}

        {/* Messages Area */}
        <div
          role="region"
          aria-label="Chat messages"
          style={{
            flex: 1,
            overflowY: "auto",
            padding: 24,
            background: "#ffffff"
          }}
        >
          {isLoadingMessages ? (
            <div role="status" aria-live="polite" style={{ textAlign: "center", padding: 48, color: "#64748b" }}>
              <p style={{ fontSize: 16 }}>Caricamento messaggi...</p>
            </div>
          ) : messages.length === 0 ? (
            <div style={{ textAlign: "center", padding: 48, color: "#64748b" }}>
              <p style={{ fontSize: 16, marginBottom: 8 }}>Nessun messaggio ancora.</p>
              <p style={{ fontSize: 14 }}>
                {activeChatId
                  ? "Inizia facendo una domanda!"
                  : "Seleziona una chat esistente o creane una nuova per iniziare."}
              </p>
            </div>
          ) : (
            <ul role="log" aria-live="polite" aria-atomic="false" style={{ listStyle: "none", margin: 0, padding: 0 }}>
              {messages.map((msg) => (
                <li
                  key={msg.id}
                  aria-label={`${msg.role === "user" ? "Your message" : "AI response"}`}
                  style={{
                    marginBottom: 24,
                    display: "flex",
                    flexDirection: "column",
                    alignItems: msg.role === "user" ? "flex-end" : "flex-start"
                  }}
                >
                {/* Message Bubble */}
                <div
                  style={{
                    maxWidth: "75%",
                    padding: 12,
                    borderRadius: 8,
                    background: msg.role === "user" ? "#e3f2fd" : "#f1f3f4",
                    fontSize: 14,
                    lineHeight: 1.5
                  }}
                >
                  <div style={{ fontWeight: 500, marginBottom: 4, fontSize: 12, color: "#64748b" }}>
                    {msg.role === "user" ? "Tu" : "MeepleAI"}
                  </div>
                  <div style={{ whiteSpace: "pre-wrap" }}>{msg.content}</div>

                  {/* Sources */}
                  {msg.snippets && msg.snippets.length > 0 && (
                    <div style={{ marginTop: 12, paddingTop: 12, borderTop: "1px solid #dadce0" }}>
                      <div style={{ fontSize: 12, fontWeight: 500, marginBottom: 8, color: "#64748b" }}>
                        Fonti:
                      </div>
                      {msg.snippets.map((snippet, idx) => (
                        <div
                          key={idx}
                          style={{
                            marginBottom: 8,
                            padding: 8,
                            background: "#ffffff",
                            border: "1px solid #dadce0",
                            borderRadius: 4,
                            fontSize: 12
                          }}
                        >
                          <div style={{ fontWeight: 500, marginBottom: 4 }}>
                            {getSnippetLabel(snippet)}
                          </div>
                          <div style={{ color: "#64748b", fontSize: 11 }}>{snippet.text}</div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                {/* Feedback buttons (only for assistant messages) */}
                {msg.role === "assistant" && (
                  <div role="group" aria-label="Message feedback" style={{ display: "flex", gap: 8, marginTop: 8 }}>
                    <button
                      onClick={() => void setFeedback(msg.id, "helpful")}
                      aria-label="Mark as helpful"
                      aria-pressed={msg.feedback === "helpful"}
                      style={{
                        padding: "4px 8px",
                        background: msg.feedback === "helpful" ? "#34a853" : "#f1f3f4",
                        color: msg.feedback === "helpful" ? "white" : "#64748b",
                        border: "none",
                        borderRadius: 4,
                        fontSize: 12,
                        cursor: "pointer",
                        display: "flex",
                        alignItems: "center",
                        gap: 4
                      }}
                      title="Questa risposta è stata utile"
                    >
                      👍 Utile
                    </button>
                    <button
                      onClick={() => void setFeedback(msg.id, "not-helpful")}
                      aria-label="Mark as not helpful"
                      aria-pressed={msg.feedback === "not-helpful"}
                      style={{
                        padding: "4px 8px",
                        background: msg.feedback === "not-helpful" ? "#ea4335" : "#f1f3f4",
                        color: msg.feedback === "not-helpful" ? "white" : "#64748b",
                        border: "none",
                        borderRadius: 4,
                        fontSize: 12,
                        cursor: "pointer",
                        display: "flex",
                        alignItems: "center",
                        gap: 4
                      }}
                      title="Questa risposta non è stata utile"
                    >
                      👎 Non utile
                    </button>
                  </div>
                )}

                {/* Timestamp */}
                <div style={{ fontSize: 11, color: "#64748b", marginTop: 4 }}>
                  {msg.timestamp.toLocaleTimeString()}
                </div>
              </li>
            ))}
            </ul>
          )}

          {/* Streaming Response */}
          {streamingState.isStreaming && (
            <div style={{ display: "flex", alignItems: "flex-start", marginBottom: 24 }}>
              <div
                style={{
                  maxWidth: "75%",
                  padding: 12,
                  borderRadius: 8,
                  background: "#f1f3f4",
                  fontSize: 14
                }}
              >
                <div style={{ fontWeight: 500, marginBottom: 4, fontSize: 12, color: "#64748b", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <span>MeepleAI</span>
                  <button
                    onClick={() => streamingControls.stopStreaming()}
                    aria-label="Stop streaming"
                    style={{
                      padding: "2px 8px",
                      background: "#ea4335",
                      color: "white",
                      border: "none",
                      borderRadius: 3,
                      fontSize: 11,
                      cursor: "pointer"
                    }}
                    title="Interrompi risposta"
                  >
                    ⏹ Stop
                  </button>
                </div>

                {/* State indicator */}
                {streamingState.state && (
                  <div style={{ color: "#64748b", fontSize: 12, fontStyle: "italic", marginBottom: 8 }}>
                    {streamingState.state}
                  </div>
                )}

                {/* Streaming content */}
                {streamingState.currentAnswer ? (
                  <div style={{ whiteSpace: "pre-wrap" }}>
                    {streamingState.currentAnswer}
                    <span style={{ opacity: 0.7 }}>▋</span>
                  </div>
                ) : (
                  <div style={{ color: "#64748b" }}>Sto pensando...</div>
                )}

                {/* Citations (if available during streaming) */}
                {streamingState.snippets && streamingState.snippets.length > 0 && (
                  <div style={{ marginTop: 12, paddingTop: 12, borderTop: "1px solid #dadce0" }}>
                    <div style={{ fontSize: 12, fontWeight: 500, marginBottom: 8, color: "#64748b" }}>
                      Fonti:
                    </div>
                    {streamingState.snippets.map((snippet, idx) => (
                      <div
                        key={idx}
                        style={{
                          marginBottom: 8,
                          padding: 8,
                          background: "#ffffff",
                          border: "1px solid #dadce0",
                          borderRadius: 4,
                          fontSize: 12
                        }}
                      >
                        <div style={{ fontWeight: 500, marginBottom: 4 }}>
                          {getSnippetLabel(snippet)}
                        </div>
                        <div style={{ color: "#64748b", fontSize: 11 }}>{snippet.text}</div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Legacy loading state (fallback) */}
          {isSendingMessage && !streamingState.isStreaming && (
            <div style={{ display: "flex", alignItems: "flex-start", marginBottom: 24 }}>
              <div
                style={{
                  maxWidth: "75%",
                  padding: 12,
                  borderRadius: 8,
                  background: "#f1f3f4",
                  fontSize: 14
                }}
              >
                <div style={{ fontWeight: 500, marginBottom: 4, fontSize: 12, color: "#64748b" }}>
                  MeepleAI
                </div>
                <div style={{ color: "#64748b" }}>Sto pensando...</div>
              </div>
            </div>
          )}
        </div>

        {/* Input Form */}
        <form
          onSubmit={sendMessage}
          style={{
            padding: 16,
            borderTop: "1px solid #dadce0",
            background: "white",
            display: "flex",
            gap: 8
          }}
        >
          <label htmlFor="message-input" className="sr-only">
            Ask a question about the game
          </label>
          <input
            id="message-input"
            type="text"
            value={inputValue}
            onChange={(e) => setInputValue(e.target.value)}
            placeholder="Fai una domanda sul gioco..."
            disabled={isSendingMessage || streamingState.isStreaming || !selectedGameId || !selectedAgentId}
            aria-label="Message input"
            style={{
              flex: 1,
              padding: 12,
              fontSize: 14,
              border: "1px solid #dadce0",
              borderRadius: 4
            }}
          />
          <button
            type="submit"
            disabled={isSendingMessage || streamingState.isStreaming || !inputValue.trim() || !selectedGameId || !selectedAgentId}
            aria-label="Send message"
            style={{
              padding: "12px 24px",
              background:
                isSendingMessage || streamingState.isStreaming || !inputValue.trim() || !selectedGameId || !selectedAgentId
                  ? "#dadce0"
                  : "#0070f3",
              color: "white",
              border: "none",
              borderRadius: 4,
              fontSize: 14,
              fontWeight: 500,
              cursor:
                isSendingMessage || streamingState.isStreaming || !inputValue.trim() || !selectedGameId || !selectedAgentId
                  ? "not-allowed"
                  : "pointer"
            }}
          >
            {isSendingMessage || streamingState.isStreaming ? "Invio..." : "Invia"}
          </button>
        </form>
      </div>
    </main>
  );
}
