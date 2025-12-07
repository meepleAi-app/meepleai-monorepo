'use client';

import { FormEvent, useEffect, useState } from 'react';
import Link from 'next/link';
import { Chessboard, type PieceDropHandlerArgs } from 'react-chessboard';
import { Chess } from 'chess.js';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

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
  role: 'user' | 'assistant';
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
  const [inputValue, setInputValue] = useState<string>('');
  const [errorMessage, setErrorMessage] = useState<string>('');
  const [isSendingMessage, setIsSendingMessage] = useState(false);

  // UI state
  const [boardOrientation, setBoardOrientation] = useState<'white' | 'black'>('white');

  // Load current user on mount
  useEffect(() => {
    void loadCurrentUser();
  }, []);

  const loadCurrentUser = async () => {
    try {
      const user = await api.auth.getMe();
      setAuthUser(user);
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
    setInputValue('');
    setErrorMessage('');
  };

  const loadFenPosition = (fen: string) => {
    try {
      const newGame = new Chess(fen);
      setGame(newGame);
      setCurrentPosition(newGame.fen());
      setHighlightedSquares({});
    } catch (error) {
      logger.error(
        'Invalid FEN position',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('ChessPage', 'loadFenPosition', { fen, operation: 'load_fen' })
      );
      setErrorMessage('Posizione FEN non valida.');
    }
  };

  const highlightMoves = (moves: string[]) => {
    const highlights: Record<string, object> = {};

    moves.forEach(move => {
      // Parse move notation (e.g., "e2-e4", "Nf3", "O-O")
      try {
        // Try to validate the move
        const tempGame = new Chess(game.fen());
        const result = tempGame.move(move);

        if (result) {
          // Highlight both from and to squares
          highlights[result.from] = {
            background: 'rgba(255, 255, 0, 0.4)',
            borderRadius: '50%',
          };
          highlights[result.to] = {
            background: 'rgba(0, 255, 0, 0.4)',
            borderRadius: '50%',
          };
        }
      } catch (error) {
        logger.error(
          'Invalid move in highlight',
          error instanceof Error ? error : new Error(String(error)),
          createErrorContext('ChessPage', 'highlightMoves', { move, operation: 'highlight_move' })
        );
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
      role: 'user',
      content: userMessageContent,
      timestamp: new Date(),
    };

    setMessages(prev => [...prev, userMessage]);
    setInputValue('');
    setErrorMessage('');
    setIsSendingMessage(true);

    try {
      // Send question with current FEN position
      const res = await api.agents.invokeChess({
        question: userMessageContent,
        fenPosition: currentPosition,
      });

      const tempAssistantId = `temp-assistant-${Date.now()}`;

      const assistantMessage: Message = {
        id: tempAssistantId,
        role: 'assistant',
        content: res.answer,
        fen: res.fen,
        suggestedMoves: res.suggestedMoves,
        timestamp: new Date(),
      };

      setMessages(prev => [...prev, assistantMessage]);

      // Update board if FEN position is provided
      if (res.fen) {
        loadFenPosition(res.fen);
      }

      // Highlight suggested moves
      if (res.suggestedMoves && res.suggestedMoves.length > 0) {
        highlightMoves(res.suggestedMoves);
      }
    } catch (err) {
      logger.error(
        'Failed to send chess message',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('ChessPage', 'sendMessage', {
          question: userMessageContent,
          position: currentPosition,
          operation: 'send_chess_message',
        })
      );
      setErrorMessage("Errore nella comunicazione con l'agente scacchi. Riprova.");

      // Remove the user message if the request failed
      setMessages(prev => prev.filter(m => m.id !== tempUserId));
    } finally {
      setIsSendingMessage(false);
    }
  };

  const onDrop = ({ piece, sourceSquare, targetSquare }: PieceDropHandlerArgs) => {
    // targetSquare can be null when piece is dropped off board
    if (!targetSquare) return false;

    try {
      const move = game.move({
        from: sourceSquare,
        to: targetSquare,
        promotion: 'q', // Always promote to queen for simplicity
      });

      // Invalid move
      if (move === null) return false;

      setCurrentPosition(game.fen());
      setHighlightedSquares({});

      // Add a system message about the move
      const moveMessage: Message = {
        id: `move-${Date.now()}`,
        role: 'assistant',
        content: `Mossa eseguita: ${move.san}`,
        fen: game.fen(),
        timestamp: new Date(),
      };

      setMessages(prev => [...prev, moveMessage]);

      return true;
    } catch (error) {
      logger.error(
        'Failed to make chess move',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('ChessPage', 'onDrop', {
          from: sourceSquare,
          to: targetSquare,
          operation: 'make_move',
        })
      );
      return false;
    }
  };

  // Render login required state
  if (!authUser) {
    return (
      <main className="p-6 max-w-[900px] mx-auto font-sans bg-white min-h-dvh">
        <Link href="/" className="text-blue-600 underline">
          ← Torna alla Home
        </Link>
        <div className="mt-6 p-8 text-center border border-gray-300 rounded-lg">
          <h2 className="text-xl font-semibold">Accesso richiesto</h2>
          <p className="mt-2">Devi effettuare l&apos;accesso per utilizzare la chat scacchi.</p>
          <Link
            href="/"
            className="inline-block mt-4 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white no-underline rounded transition-colors"
          >
            Vai al Login
          </Link>
        </div>
      </main>
    );
  }

  // Main chess interface
  return (
    <main className="flex h-dvh font-sans overflow-hidden bg-gray-50">
      {/* Chess Board Panel */}
      <div className="flex-shrink-0 w-[600px] flex flex-col p-6 bg-white border-r border-gray-300">
        {/* Board Header */}
        <div className="mb-4">
          <h1 className="m-0 text-2xl mb-2">Chess Assistant</h1>
          <p className="m-0 text-slate-600 text-sm">
            Gioca o chiedi consigli sull&apos;agente scacchi AI
          </p>
        </div>

        {/* Board Controls */}
        <div className="flex gap-2 mb-4">
          <button
            onClick={resetBoard}
            className="px-4 py-2 bg-blue-600 text-white border-0 rounded text-sm font-medium cursor-pointer hover:bg-blue-700"
          >
            Nuova Partita
          </button>
          <button
            onClick={() => setBoardOrientation(prev => (prev === 'white' ? 'black' : 'white'))}
            className="px-4 py-2 bg-gray-100 text-gray-800 border border-gray-300 rounded text-sm font-medium cursor-pointer hover:bg-gray-200"
          >
            Ruota Scacchiera
          </button>
          <Link
            href="/"
            className="px-4 py-2 bg-gray-100 text-gray-800 border border-gray-300 rounded text-sm font-medium no-underline flex items-center hover:bg-gray-200"
          >
            Home
          </Link>
        </div>

        {/* Chess Board */}
        <div className="w-full max-w-[550px] aspect-square mx-auto">
          <Chessboard
            options={{
              position: currentPosition,
              onPieceDrop: onDrop,
              boardOrientation: boardOrientation,
              squareStyles: highlightedSquares,
            }}
          />
        </div>

        {/* Game Status */}
        <div className="mt-4 p-3 bg-gray-100 rounded text-sm">
          <div className="mb-1">
            <strong>Turno:</strong> {game.turn() === 'w' ? 'Bianco' : 'Nero'}
          </div>
          <div className="mb-1">
            <strong>Stato:</strong>{' '}
            {game.isCheckmate()
              ? 'Scacco Matto!'
              : game.isCheck()
                ? 'Scacco!'
                : game.isDraw()
                  ? 'Patta'
                  : game.isStalemate()
                    ? 'Stallo'
                    : 'In corso'}
          </div>
          <div className="text-xs text-slate-600 mt-2">
            <strong>FEN:</strong> {currentPosition}
          </div>
        </div>
      </div>

      {/* Chat Panel */}
      <div className="flex-1 flex flex-col overflow-hidden">
        {/* Chat Header */}
        <div className="p-4 border-b border-gray-300 bg-white">
          <h2 className="m-0 text-lg">Chat con l&apos;Agente</h2>
          <p className="mt-1 mb-0 text-slate-600 text-sm">
            Chiedi consigli, analisi o spiegazioni sulle mosse
          </p>
        </div>

        {/* Error Message */}
        {errorMessage && (
          <div className="m-4 p-3 bg-red-50 text-red-600 rounded text-sm">{errorMessage}</div>
        )}

        {/* Messages Area */}
        <div className="flex-1 overflow-y-auto p-6 bg-white">
          {messages.length === 0 ? (
            <div className="text-center py-12 text-slate-600">
              <p className="text-base mb-2">Benvenuto nella Chess Chat!</p>
              <p className="text-sm">
                Inizia facendo una domanda o muovi un pezzo sulla scacchiera.
              </p>
              <p className="text-sm mt-4">Esempi:</p>
              <ul className="text-sm text-left max-w-[400px] my-2 mx-auto">
                <li>Qual è la migliore apertura per il bianco?</li>
                <li>Come si fa l&apos;arrocco?</li>
                <li>Analizza questa posizione</li>
                <li>Suggerisci la prossima mossa</li>
              </ul>
            </div>
          ) : (
            messages.map(msg => (
              <div
                key={msg.id}
                className={cn(
                  'mb-6 flex flex-col',
                  msg.role === 'user' ? 'items-end' : 'items-start'
                )}
              >
                {/* Message Bubble */}
                <div
                  className="max-w-[80%] p-3 rounded-lg text-sm leading-normal"
                  style={{
                    background: msg.role === 'user' ? '#e3f2fd' : '#f1f3f4',
                  }}
                >
                  <div className="font-medium mb-1 text-xs text-slate-600">
                    {msg.role === 'user' ? 'Tu' : 'Chess AI'}
                  </div>
                  <div className="whitespace-pre-wrap">{msg.content}</div>

                  {/* Suggested Moves */}
                  {msg.suggestedMoves && msg.suggestedMoves.length > 0 && (
                    <div className="mt-3 pt-3 border-t border-gray-300">
                      <div className="text-xs font-medium mb-2 text-slate-600">
                        Mosse suggerite:
                      </div>
                      <div className="flex flex-wrap gap-2">
                        {msg.suggestedMoves.map((move, idx) => (
                          <span
                            key={idx}
                            className="px-2 py-1 bg-green-600 text-white rounded text-xs font-medium"
                          >
                            {move}
                          </span>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* FEN Position */}
                  {msg.fen && (
                    <div className="mt-2 text-xs text-slate-600">
                      Posizione: {msg.fen.substring(0, 40)}...
                    </div>
                  )}
                </div>

                {/* Timestamp */}
                <div className="text-xs text-slate-600 mt-1">
                  {msg.timestamp.toLocaleTimeString()}
                </div>
              </div>
            ))
          )}

          {isSendingMessage && (
            <div className="flex items-start mb-6">
              <div className="max-w-[80%] p-3 rounded-lg bg-gray-100 text-sm">
                <div className="font-medium mb-1 text-xs text-slate-600">Chess AI</div>
                <div className="text-slate-600">Sto analizzando...</div>
              </div>
            </div>
          )}
        </div>

        {/* Input Form */}
        <form onSubmit={sendMessage} className="p-4 border-t border-gray-300 bg-white flex gap-2">
          <input
            type="text"
            value={inputValue}
            onChange={e => setInputValue(e.target.value)}
            placeholder="Chiedi consigli o analisi della posizione..."
            disabled={isSendingMessage}
            className="flex-1 p-3 text-sm border border-gray-300 rounded"
          />
          <button
            type="submit"
            disabled={isSendingMessage || !inputValue.trim()}
            className={cn(
              'px-6 py-3 text-white border-0 rounded text-sm font-medium',
              isSendingMessage || !inputValue.trim()
                ? 'bg-gray-300 cursor-not-allowed'
                : 'bg-green-600 cursor-pointer hover:bg-green-700'
            )}
          >
            {isSendingMessage ? 'Invio...' : 'Invia'}
          </button>
        </form>
      </div>
    </main>
  );
}
