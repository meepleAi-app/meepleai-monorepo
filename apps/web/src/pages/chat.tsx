import { FormEvent, useEffect, useState } from "react";
import Link from "next/link";
import { api } from "../lib/api";

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

type Message = {
  id: string;
  role: "user" | "assistant";
  content: string;
  sources?: Source[];
  feedback?: "helpful" | "not-helpful" | null;
  endpoint?: string;
  gameId?: string;
  timestamp: Date;
};

type Source = {
  title: string;
  snippet: string;
  page?: number;
};

type QAResponse = {
  answer: string;
  sources?: Source[];
};

export default function ChatPage() {
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);
  const [selectedGame, setSelectedGame] = useState<string>("demo-chess");
  const [messages, setMessages] = useState<Message[]>([]);
  const [inputValue, setInputValue] = useState<string>("");
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string>("");

  useEffect(() => {
    void loadCurrentUser();
  }, []);

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

  const sendMessage = async (e: FormEvent) => {
    e.preventDefault();

    if (!authUser) {
      setErrorMessage("Devi effettuare l&apos;accesso per chattare.");
      return;
    }

    if (!inputValue.trim()) {
      return;
    }

    const userMessage: Message = {
      id: `msg-${Date.now()}-user`,
      role: "user",
      content: inputValue,
      timestamp: new Date()
    };

    setMessages((prev) => [...prev, userMessage]);
    setInputValue("");
    setErrorMessage("");
    setIsLoading(true);

    try {
      const res = await api.post<QAResponse>("/agents/qa", {
        gameId: selectedGame,
        query: inputValue
      });

      const assistantMessage: Message = {
        id: `msg-${Date.now()}-assistant`,
        role: "assistant",
        content: res.answer,
        sources: res.sources,
        feedback: null,
        endpoint: "qa",
        gameId: selectedGame,
        timestamp: new Date()
      };

      setMessages((prev) => [...prev, assistantMessage]);
    } catch (err: any) {
      console.error(err);
      setErrorMessage("Errore nella comunicazione con l'agente. Riprova.");

      // Remove the user message if the request failed
      setMessages((prev) => prev.filter((m) => m.id !== userMessage.id));
    } finally {
      setIsLoading(false);
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
    const gameId = targetMessage.gameId ?? selectedGame;

    setMessages((prev) =>
      prev.map((msg) => (msg.id === messageId ? { ...msg, feedback: nextFeedback } : msg))
    );

    try {
      await api.post("/agents/feedback", {
        messageId,
        endpoint,
        outcome: nextFeedback,
        userId: authUser.id,
        gameId
      });
    } catch (error) {
      console.error(error);
      setMessages((prev) =>
        prev.map((msg) => (msg.id === messageId ? { ...msg, feedback: previousFeedback } : msg))
      );
    }
  };

  const clearHistory = () => {
    setMessages([]);
    setErrorMessage("");
  };

  if (!authUser) {
    return (
      <main style={{ padding: 24, maxWidth: 900, margin: "0 auto", fontFamily: "sans-serif" }}>
        <Link href="/" style={{ color: "#0070f3", textDecoration: "none" }}>
          ← Torna alla Home
        </Link>
        <div style={{ marginTop: 24, padding: 32, textAlign: "center", border: "1px solid #dadce0", borderRadius: 8 }}>
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

  return (
    <main style={{ padding: 24, maxWidth: 900, margin: "0 auto", fontFamily: "sans-serif", height: "100vh", display: "flex", flexDirection: "column" }}>
      {/* Header */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
        <div>
          <h1 style={{ margin: 0, fontSize: 24 }}>MeepleAI Chat</h1>
          <p style={{ margin: "4px 0 0 0", color: "#5f6368", fontSize: 14 }}>
            Chatta con l&apos;agente del tuo gioco
          </p>
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

      {/* Game Selector */}
      <div style={{ marginBottom: 16, padding: 16, border: "1px solid #dadce0", borderRadius: 8, background: "#f8f9fa" }}>
        <label htmlFor="gameSelect" style={{ display: "block", marginBottom: 8, fontWeight: 500, fontSize: 14 }}>
          Seleziona il gioco:
        </label>
        <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
          <select
            id="gameSelect"
            value={selectedGame}
            onChange={(e) => setSelectedGame(e.target.value)}
            style={{
              flex: 1,
              padding: 8,
              fontSize: 14,
              border: "1px solid #dadce0",
              borderRadius: 4
            }}
          >
            <option value="demo-chess">Demo - Chess</option>
            <option value="demo-catan">Demo - Catan</option>
            <option value="demo-risk">Demo - Risk</option>
          </select>
          <button
            onClick={clearHistory}
            style={{
              padding: "8px 16px",
              background: "#ea4335",
              color: "white",
              border: "none",
              borderRadius: 4,
              fontSize: 14,
              cursor: "pointer"
            }}
          >
            Cancella Storia
          </button>
        </div>
      </div>

      {errorMessage && (
        <div style={{ marginBottom: 16, padding: 12, background: "#fce8e6", color: "#d93025", borderRadius: 4, fontSize: 14 }}>
          {errorMessage}
        </div>
      )}

      {/* Chat Messages */}
      <div
        style={{
          flex: 1,
          overflowY: "auto",
          border: "1px solid #dadce0",
          borderRadius: 8,
          padding: 16,
          marginBottom: 16,
          background: "#ffffff"
        }}
      >
        {messages.length === 0 ? (
          <div style={{ textAlign: "center", padding: 48, color: "#5f6368" }}>
            <p style={{ fontSize: 16, marginBottom: 8 }}>Nessun messaggio ancora.</p>
            <p style={{ fontSize: 14 }}>Inizia facendo una domanda sul gioco selezionato!</p>
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
                {msg.sources && msg.sources.length > 0 && (
                  <div style={{ marginTop: 12, paddingTop: 12, borderTop: "1px solid #dadce0" }}>
                    <div style={{ fontSize: 12, fontWeight: 500, marginBottom: 8, color: "#5f6368" }}>
                      Fonti:
                    </div>
                    {msg.sources.map((source, idx) => (
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
                          {source.title} {source.page !== undefined && `(Pagina ${source.page})`}
                        </div>
                        <div style={{ color: "#5f6368", fontSize: 11 }}>{source.snippet}</div>
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

        {isLoading && (
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
      <form onSubmit={sendMessage} style={{ display: "flex", gap: 8 }}>
        <input
          type="text"
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          placeholder="Fai una domanda sul gioco..."
          disabled={isLoading}
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
          disabled={isLoading || !inputValue.trim()}
          style={{
            padding: "12px 24px",
            background: isLoading || !inputValue.trim() ? "#dadce0" : "#0070f3",
            color: "white",
            border: "none",
            borderRadius: 4,
            fontSize: 14,
            fontWeight: 500,
            cursor: isLoading || !inputValue.trim() ? "not-allowed" : "pointer"
          }}
        >
          {isLoading ? "Invio..." : "Invia"}
        </button>
      </form>
    </main>
  );
}
