import { FormEvent, useEffect, useState } from "react";
import Link from "next/link";
import { Chessboard } from "react-chessboard";
import { Chess } from "chess.js";
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

type Message = {
  id: string;
  role: "user" | "assistant";
  content: string;
  fen?: string; // FEN position from AI response
  suggestedMoves?: string[]; // Moves in algebraic notation (e.g., ["e2-e4", "Nf3"])
  timestamp: Date;
};

type ChessResponse = {
  answer: string;
  fen?: string;
  suggestedMoves?: string[];
};

export default function ChessPage() {
  // Authentication
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);

  // Chess game state
  const [game, setGame] = useState(new Chess());
  const [currentPosition, setCurrentPosition] = useState(game.fen());
  const [highlightedSquares, setHighlightedSquares] = useState<Record<string, object>>({});

  // Chat state
  const [messages, setMessages] = useState<Message[]>([]);
  const [inputValue, setInputValue] = useState<string>("");
  const [errorMessage, setErrorMessage] = useState<string>("");
  const [isSendingMessage, setIsSendingMessage] = useState(false);

  // UI state
  const [boardOrientation, setBoardOrientation] = useState<"white" | "black">("white");

  // Load current user on mount
  useEffect(() => {
    void loadCurrentUser();
  }, []);

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

  const resetBoard = () => {
    const newGame = new Chess();
    setGame(newGame);
    setCurrentPosition(newGame.fen());
    setHighlightedSquares({});
    setMessages([]);
    setInputValue("");
    setErrorMessage("");
  };

  const loadFenPosition = (fen: string) => {
    try {
      const newGame = new Chess(fen);
      setGame(newGame);
      setCurrentPosition(newGame.fen());
      setHighlightedSquares({});
    } catch (error) {
      console.error("Invalid FEN:", error);
      setErrorMessage("Posizione FEN non valida.");
    }
  };

  const highlightMoves = (moves: string[]) => {
    const highlights: Record<string, object> = {};

    moves.forEach((move) => {
      // Parse move notation (e.g., "e2-e4", "Nf3", "O-O")
      try {
        // Try to validate the move
        const tempGame = new Chess(game.fen());
        const result = tempGame.move(move);

        if (result) {
          // Highlight both from and to squares
          highlights[result.from] = {
            background: "rgba(255, 255, 0, 0.4)",
            borderRadius: "50%"
          };
          highlights[result.to] = {
            background: "rgba(0, 255, 0, 0.4)",
            borderRadius: "50%"
          };
        }
      } catch (error) {
        console.error("Invalid move:", move, error);
      }
    });

    setHighlightedSquares(highlights);
  };

  const sendMessage = async (e: FormEvent) => {
    e.preventDefault();

    if (!authUser) {
      setErrorMessage("Devi effettuare l'accesso per utilizzare la chat.");
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
      // Send question with current FEN position
      const res = await api.post<ChessResponse>("/api/v1/agents/chess", {
        question: userMessageContent,
        fenPosition: currentPosition
      });

      const tempAssistantId = `temp-assistant-${Date.now()}`;

      const assistantMessage: Message = {
        id: tempAssistantId,
        role: "assistant",
        content: res.answer,
        fen: res.fen,
        suggestedMoves: res.suggestedMoves,
        timestamp: new Date()
      };

      setMessages((prev) => [...prev, assistantMessage]);

      // Update board if FEN position is provided
      if (res.fen) {
        loadFenPosition(res.fen);
      }

      // Highlight suggested moves
      if (res.suggestedMoves && res.suggestedMoves.length > 0) {
        highlightMoves(res.suggestedMoves);
      }
    } catch (err) {
      console.error("Error sending message:", err);
      setErrorMessage("Errore nella comunicazione con l'agente scacchi. Riprova.");

      // Remove the user message if the request failed
      setMessages((prev) => prev.filter((m) => m.id !== tempUserId));
    } finally {
      setIsSendingMessage(false);
    }
  };

  const onDrop = (sourceSquare: string, targetSquare: string) => {
    try {
      const move = game.move({
        from: sourceSquare,
        to: targetSquare,
        promotion: "q" // Always promote to queen for simplicity
      });

      // Invalid move
      if (move === null) return false;

      setCurrentPosition(game.fen());
      setHighlightedSquares({});

      // Add a system message about the move
      const moveMessage: Message = {
        id: `move-${Date.now()}`,
        role: "assistant",
        content: `Mossa eseguita: ${move.san}`,
        fen: game.fen(),
        timestamp: new Date()
      };

      setMessages((prev) => [...prev, moveMessage]);

      return true;
    } catch (error) {
      console.error("Error making move:", error);
      return false;
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
          <p>Devi effettuare l&apos;accesso per utilizzare la chat scacchi.</p>
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

  // Main chess interface
  return (
    <main
      style={{
        display: "flex",
        height: "100vh",
        fontFamily: "sans-serif",
        overflow: "hidden",
        background: "#f8f9fa"
      }}
    >
      {/* Chess Board Panel */}
      <div
        style={{
          flex: "0 0 600px",
          display: "flex",
          flexDirection: "column",
          padding: 24,
          background: "white",
          borderRight: "1px solid #dadce0"
        }}
      >
        {/* Board Header */}
        <div style={{ marginBottom: 16 }}>
          <h1 style={{ margin: 0, fontSize: 24, marginBottom: 8 }}>Chess Assistant</h1>
          <p style={{ margin: 0, color: "#5f6368", fontSize: 14 }}>
            Gioca o chiedi consigli sull&apos;agente scacchi AI
          </p>
        </div>

        {/* Board Controls */}
        <div style={{ display: "flex", gap: 8, marginBottom: 16 }}>
          <button
            onClick={resetBoard}
            style={{
              padding: "8px 16px",
              background: "#1a73e8",
              color: "white",
              border: "none",
              borderRadius: 4,
              fontSize: 14,
              fontWeight: 500,
              cursor: "pointer"
            }}
          >
            Nuova Partita
          </button>
          <button
            onClick={() =>
              setBoardOrientation((prev) => (prev === "white" ? "black" : "white"))
            }
            style={{
              padding: "8px 16px",
              background: "#f1f3f4",
              color: "#202124",
              border: "1px solid #dadce0",
              borderRadius: 4,
              fontSize: 14,
              fontWeight: 500,
              cursor: "pointer"
            }}
          >
            Ruota Scacchiera
          </button>
          <Link
            href="/"
            style={{
              padding: "8px 16px",
              background: "#f1f3f4",
              color: "#202124",
              border: "1px solid #dadce0",
              borderRadius: 4,
              fontSize: 14,
              fontWeight: 500,
              textDecoration: "none",
              display: "flex",
              alignItems: "center"
            }}
          >
            Home
          </Link>
        </div>

        {/* Chess Board */}
        <div
          style={{
            width: "100%",
            maxWidth: 550,
            aspectRatio: "1",
            margin: "0 auto"
          }}
        >
          <Chessboard
            position={currentPosition}
            onPieceDrop={onDrop}
            boardOrientation={boardOrientation}
            customSquareStyles={highlightedSquares}
            boardWidth={550}
          />
        </div>

        {/* Game Status */}
        <div
          style={{
            marginTop: 16,
            padding: 12,
            background: "#f1f3f4",
            borderRadius: 4,
            fontSize: 13
          }}
        >
          <div style={{ marginBottom: 4 }}>
            <strong>Turno:</strong> {game.turn() === "w" ? "Bianco" : "Nero"}
          </div>
          <div style={{ marginBottom: 4 }}>
            <strong>Stato:</strong>{" "}
            {game.isCheckmate()
              ? "Scacco Matto!"
              : game.isCheck()
                ? "Scacco!"
                : game.isDraw()
                  ? "Patta"
                  : game.isStalemate()
                    ? "Stallo"
                    : "In corso"}
          </div>
          <div style={{ fontSize: 11, color: "#5f6368", marginTop: 8 }}>
            <strong>FEN:</strong> {currentPosition}
          </div>
        </div>
      </div>

      {/* Chat Panel */}
      <div style={{ flex: 1, display: "flex", flexDirection: "column", overflow: "hidden" }}>
        {/* Chat Header */}
        <div
          style={{
            padding: 16,
            borderBottom: "1px solid #dadce0",
            background: "white"
          }}
        >
          <h2 style={{ margin: 0, fontSize: 18 }}>Chat con l&apos;Agente</h2>
          <p style={{ margin: "4px 0 0 0", color: "#5f6368", fontSize: 13 }}>
            Chiedi consigli, analisi o spiegazioni sulle mosse
          </p>
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
          {messages.length === 0 ? (
            <div style={{ textAlign: "center", padding: 48, color: "#5f6368" }}>
              <p style={{ fontSize: 16, marginBottom: 8 }}>Benvenuto nella Chess Chat!</p>
              <p style={{ fontSize: 14 }}>
                Inizia facendo una domanda o muovi un pezzo sulla scacchiera.
              </p>
              <p style={{ fontSize: 13, marginTop: 16 }}>Esempi:</p>
              <ul style={{ fontSize: 13, textAlign: "left", maxWidth: 400, margin: "8px auto" }}>
                <li>Qual è la migliore apertura per il bianco?</li>
                <li>Come si fa l&apos;arrocco?</li>
                <li>Analizza questa posizione</li>
                <li>Suggerisci la prossima mossa</li>
              </ul>
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
                    maxWidth: "80%",
                    padding: 12,
                    borderRadius: 8,
                    background: msg.role === "user" ? "#e3f2fd" : "#f1f3f4",
                    fontSize: 14,
                    lineHeight: 1.5
                  }}
                >
                  <div style={{ fontWeight: 500, marginBottom: 4, fontSize: 12, color: "#5f6368" }}>
                    {msg.role === "user" ? "Tu" : "Chess AI"}
                  </div>
                  <div style={{ whiteSpace: "pre-wrap" }}>{msg.content}</div>

                  {/* Suggested Moves */}
                  {msg.suggestedMoves && msg.suggestedMoves.length > 0 && (
                    <div style={{ marginTop: 12, paddingTop: 12, borderTop: "1px solid #dadce0" }}>
                      <div style={{ fontSize: 12, fontWeight: 500, marginBottom: 8, color: "#5f6368" }}>
                        Mosse suggerite:
                      </div>
                      <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
                        {msg.suggestedMoves.map((move, idx) => (
                          <span
                            key={idx}
                            style={{
                              padding: "4px 8px",
                              background: "#34a853",
                              color: "white",
                              borderRadius: 4,
                              fontSize: 12,
                              fontWeight: 500
                            }}
                          >
                            {move}
                          </span>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* FEN Position */}
                  {msg.fen && (
                    <div style={{ marginTop: 8, fontSize: 11, color: "#9aa0a6" }}>
                      Posizione: {msg.fen.substring(0, 40)}...
                    </div>
                  )}
                </div>

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
                  maxWidth: "80%",
                  padding: 12,
                  borderRadius: 8,
                  background: "#f1f3f4",
                  fontSize: 14
                }}
              >
                <div style={{ fontWeight: 500, marginBottom: 4, fontSize: 12, color: "#5f6368" }}>
                  Chess AI
                </div>
                <div style={{ color: "#5f6368" }}>Sto analizzando...</div>
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
            placeholder="Chiedi consigli o analisi della posizione..."
            disabled={isSendingMessage}
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
            disabled={isSendingMessage || !inputValue.trim()}
            style={{
              padding: "12px 24px",
              background: isSendingMessage || !inputValue.trim() ? "#dadce0" : "#34a853",
              color: "white",
              border: "none",
              borderRadius: 4,
              fontSize: 14,
              fontWeight: 500,
              cursor: isSendingMessage || !inputValue.trim() ? "not-allowed" : "pointer"
            }}
          >
            {isSendingMessage ? "Invio..." : "Invia"}
          </button>
        </form>
      </div>
    </main>
  );
}
