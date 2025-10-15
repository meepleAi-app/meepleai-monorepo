import { FormEvent, useEffect, useState } from "react";
import Link from "next/link";
import { api } from "../lib/api";

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

  // Chat management
  const [chats, setChats] = useState<Chat[]>([]);
  const [activeChatId, setActiveChatId] = useState<string | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);

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

  // Load agents and chats when game is selected
  useEffect(() => {
    if (selectedGameId) {
      void loadAgents(selectedGameId);
      void loadChats(selectedGameId);
      // Reset active chat when game changes
      setActiveChatId(null);
      setMessages([]);
      setSelectedAgentId(null);
    } else {
      setAgents([]);
      setChats([]);
      setActiveChatId(null);
      setMessages([]);
      setSelectedAgentId(null);
    }
  }, [selectedGameId]);

  const loadCurrentUser = async () => {
    try {
      const res = await api.get<AuthResponse>("/auth/me");
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
      const gamesList = await api.get<Game[]>("/games");
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
      const agentsList = await api.get<Agent[]>(`/games/${gameId}/agents`);
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
      const chatsList = await api.get<Chat[]>(`/chats?gameId=${gameId}`);
      setChats(chatsList ?? []);
    } catch (err) {
      console.error("Error loading chats:", err);
      // Don't show error to user - empty chat list is acceptable
      setChats([]);
    } finally {
      setIsLoadingChats(false);
    }
  };

  const loadChatHistory = async (chatId: string) => {
    setIsLoadingMessages(true);
    setErrorMessage("");
    try {
      const chatWithHistory = await api.get<ChatWithHistory>(`/chats/${chatId}`);

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

        setMessages(loadedMessages);
        setActiveChatId(chatId);

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
      const newChat = await api.post<Chat>("/chats", {
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
      await api.delete(`/chats/${chatId}`);

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
        const newChat = await api.post<Chat>("/chats", {
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

      // Send message with chatId
      const res = await api.post<QaResponse>("/agents/qa", {
        gameId: selectedGameId,
        query: userMessageContent,
        chatId: chatId
      });

      const tempAssistantId = `temp-assistant-${Date.now()}`;

      const assistantMessage: Message = {
        id: tempAssistantId,
        role: "assistant",
        content: res.answer,
        snippets: formatSnippets(res.snippets),
        feedback: null,
        endpoint: "qa",
        gameId: selectedGameId,
        timestamp: new Date(),
        backendMessageId: res.messageId
      };

      setMessages((prev) => [...prev, assistantMessage]);

      // Update chat's lastMessageAt in the list
      if (chatId) {
        setChats((prev) =>
          prev.map((c) =>
            c.id === chatId ? { ...c, lastMessageAt: new Date().toISOString() } : c
          )
        );
      }
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
      await api.post("/agents/feedback", {
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
      <main style={{ padding: 24, maxWidth: 900, margin: "0 auto", fontFamily: "sans-serif" }}>
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
      style={{
        display: "flex",
        height: "100vh",
        fontFamily: "sans-serif",
        overflow: "hidden"
      }}
    >
      {/* Sidebar */}
      <aside
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
          <h2 style={{ margin: 0, fontSize: 18, marginBottom: 16 }}>MeepleAI Chat</h2>

          {/* Game Selector */}
          <div style={{ marginBottom: 12 }}>
            <label
              htmlFor="gameSelect"
              style={{ display: "block", marginBottom: 6, fontWeight: 500, fontSize: 13 }}
            >
              Gioco:
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
        <div style={{ flex: 1, overflowY: "auto", padding: 8 }}>
          {isLoadingChats ? (
            <div style={{ padding: 16, textAlign: "center", color: "#5f6368", fontSize: 13 }}>
              Caricamento chat...
            </div>
          ) : chats.length === 0 ? (
            <div style={{ padding: 16, textAlign: "center", color: "#5f6368", fontSize: 13 }}>
              Nessuna chat. Creane una nuova!
            </div>
          ) : (
            chats.map((chat) => (
              <div
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
              >
                <div style={{ fontWeight: 500, marginBottom: 4 }}>{chat.agentName}</div>
                <div style={{ fontSize: 11, color: "#5f6368" }}>{formatChatPreview(chat)}</div>
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    void deleteChat(chat.id);
                  }}
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
              </div>
            ))
          )}
        </div>
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
              <p style={{ margin: "4px 0 0 0", color: "#5f6368", fontSize: 13 }}>
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
          style={{
            flex: 1,
            overflowY: "auto",
            padding: 24,
            background: "#ffffff"
          }}
        >
          {isLoadingMessages ? (
            <div style={{ textAlign: "center", padding: 48, color: "#5f6368" }}>
              <p style={{ fontSize: 16 }}>Caricamento messaggi...</p>
            </div>
          ) : messages.length === 0 ? (
            <div style={{ textAlign: "center", padding: 48, color: "#5f6368" }}>
              <p style={{ fontSize: 16, marginBottom: 8 }}>Nessun messaggio ancora.</p>
              <p style={{ fontSize: 14 }}>
                {activeChatId
                  ? "Inizia facendo una domanda!"
                  : "Seleziona una chat esistente o creane una nuova per iniziare."}
              </p>
            </div>
          ) : (
            messages.map((msg) => (
              <div
                key={msg.id}
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
                  <div style={{ fontWeight: 500, marginBottom: 4, fontSize: 12, color: "#5f6368" }}>
                    {msg.role === "user" ? "Tu" : "MeepleAI"}
                  </div>
                  <div style={{ whiteSpace: "pre-wrap" }}>{msg.content}</div>

                  {/* Sources */}
                  {msg.snippets && msg.snippets.length > 0 && (
                    <div style={{ marginTop: 12, paddingTop: 12, borderTop: "1px solid #dadce0" }}>
                      <div style={{ fontSize: 12, fontWeight: 500, marginBottom: 8, color: "#5f6368" }}>
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
                          <div style={{ color: "#5f6368", fontSize: 11 }}>{snippet.text}</div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                {/* Feedback buttons (only for assistant messages) */}
                {msg.role === "assistant" && (
                  <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
                    <button
                      onClick={() => void setFeedback(msg.id, "helpful")}
                      style={{
                        padding: "4px 8px",
                        background: msg.feedback === "helpful" ? "#34a853" : "#f1f3f4",
                        color: msg.feedback === "helpful" ? "white" : "#5f6368",
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
                      style={{
                        padding: "4px 8px",
                        background: msg.feedback === "not-helpful" ? "#ea4335" : "#f1f3f4",
                        color: msg.feedback === "not-helpful" ? "white" : "#5f6368",
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
                <div style={{ fontSize: 11, color: "#9aa0a6", marginTop: 4 }}>
                  {msg.timestamp.toLocaleTimeString()}
                </div>
              </div>
            ))
          )}

          {isSendingMessage && (
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
                <div style={{ fontWeight: 500, marginBottom: 4, fontSize: 12, color: "#5f6368" }}>
                  MeepleAI
                </div>
                <div style={{ color: "#5f6368" }}>Sto pensando...</div>
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
          <input
            type="text"
            value={inputValue}
            onChange={(e) => setInputValue(e.target.value)}
            placeholder="Fai una domanda sul gioco..."
            disabled={isSendingMessage || !selectedGameId || !selectedAgentId}
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
            disabled={isSendingMessage || !inputValue.trim() || !selectedGameId || !selectedAgentId}
            style={{
              padding: "12px 24px",
              background:
                isSendingMessage || !inputValue.trim() || !selectedGameId || !selectedAgentId
                  ? "#dadce0"
                  : "#0070f3",
              color: "white",
              border: "none",
              borderRadius: 4,
              fontSize: 14,
              fontWeight: 500,
              cursor:
                isSendingMessage || !inputValue.trim() || !selectedGameId || !selectedAgentId
                  ? "not-allowed"
                  : "pointer"
            }}
          >
            {isSendingMessage ? "Invio..." : "Invia"}
          </button>
        </form>
      </div>
    </main>
  );
}
