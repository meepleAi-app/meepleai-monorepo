'use client';

/**
 * Board Game AI Ask - Client Component
 *
 * Interactive Q&A interface for board game rules.
 * Uses the useStreamingChat hook for real-time SSE responses.
 *
 * Issue #3373: Streaming SSE Integration
 */

import { useEffect, useState } from 'react';

import { motion } from 'framer-motion';
import Link from 'next/link';

import { Card } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';
import { GamesArrayResponseSchema } from '@/lib/api/schemas/games.schemas';
import type { Game } from '@/lib/api/schemas/games.schemas';
import type { Citation } from '@/lib/api/schemas/streaming.schemas';
import { createErrorContext } from '@/lib/errors';
import { useStreamingChat } from '@/lib/hooks/useStreamingChat';
import { logger } from '@/lib/logger';

export default function BoardGameAskClient() {
  // State
  const [games, setGames] = useState<Game[]>([]);
  const [selectedGameId, setSelectedGameId] = useState<string>('');
  const [question, setQuestion] = useState('');
  const [loadingGames, setLoadingGames] = useState(true);
  const [gamesError, setGamesError] = useState<string | null>(null);
  const [conversationHistory, setConversationHistory] = useState<
    Array<{ role: 'user' | 'assistant'; content: string; citations?: Citation[] }>
  >([]);

  // Streaming chat hook (Issue #3373: SSE streaming integration)
  const [streamState, streamControls] = useStreamingChat({
    onComplete: (answer, citations, confidence) => {
      // Add assistant response to conversation history
      setConversationHistory(prev => [
        ...prev,
        {
          role: 'assistant',
          content: answer,
          citations: citations || [],
        },
      ]);
      setQuestion(''); // Clear input after successful response
      logger.info('Streaming completed', createErrorContext('BoardGameAskClient', 'streamComplete', {
        answerLength: answer.length,
        citationCount: citations.length,
        confidence,
      }));
    },
    onError: error => {
      logger.error(
        'Streaming error occurred',
        error,
        createErrorContext('BoardGameAskClient', 'streamError', { operation: 'streaming_chat' })
      );
    },
    onStateUpdate: state => {
      logger.debug('Streaming state update', createErrorContext('BoardGameAskClient', 'stateUpdate', {
        state,
      }));
    },
  });

  // Fetch games on mount
  useEffect(() => {
    const abortController = new AbortController();

    const fetchGames = async () => {
      try {
        setLoadingGames(true);
        const response = await api.games.getAll();

        // Validate response with Zod schema (replaces unsafe `as any` casting)
        const validatedData = GamesArrayResponseSchema.safeParse(response);

        if (!validatedData.success) {
          logger.error(
            'Invalid games response schema',
            new Error(`Validation failed: ${validatedData.error.message}`),
            createErrorContext('BoardGameAskClient', 'fetchGames', {
              operation: 'load_games',
              validationError: validatedData.error.issues,
            })
          );
          setGamesError('Failed to load games: Invalid response format.');
          setGames([]);
          return;
        }

        const gamesData = validatedData.data;

        // Check if component is still mounted
        if (abortController.signal.aborted) return;

        setGames(gamesData);

        // Auto-select first game if available
        if (gamesData.length > 0 && !selectedGameId) {
          setSelectedGameId(gamesData[0].id);
        }
      } catch (err) {
        // Don't set error if request was aborted
        if (!abortController.signal.aborted) {
          logger.error(
            'Failed to fetch games',
            err instanceof Error ? err : new Error(String(err)),
            createErrorContext('BoardGameAskClient', 'fetchGames', { operation: 'load_games' })
          );
          setGamesError('Failed to load games. Please try again.');
          setGames([]); // ensure controlled Select has data
        }
      } finally {
        if (!abortController.signal.aborted) {
          setLoadingGames(false);
        }
      }
    };

    void fetchGames();

    // Cleanup on unmount
    return () => {
      abortController.abort();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- selectedGameId intentionally omitted to prevent infinite loop from auto-selection
  }, []);

  // Handle ask question
  const handleAskQuestion = async () => {
    if (!selectedGameId?.trim()) {
      setGamesError('Please select a game.');
      return;
    }

    const trimmedQuestion = question.trim();
    if (!trimmedQuestion) {
      return;
    }

    // Validate input length (prevent abuse)
    const MAX_QUESTION_LENGTH = 2000;
    if (trimmedQuestion.length > MAX_QUESTION_LENGTH) {
      logger.warn(
        'Question exceeds maximum length',
        createErrorContext('BoardGameAskClient', 'handleAskQuestion', {
          length: trimmedQuestion.length,
          max: MAX_QUESTION_LENGTH,
        })
      );
      setGamesError(`Question must be less than ${MAX_QUESTION_LENGTH} characters.`);
      return;
    }

    // Add user question to history
    setConversationHistory(prev => [...prev, { role: 'user', content: trimmedQuestion }]);

    // Start streaming chat response (Issue #3373)
    await streamControls.startStreaming(selectedGameId, trimmedQuestion);
  };

  // Handle Enter key (Ctrl+Enter to submit)
  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      handleAskQuestion();
    }
  };

  const selectedGame = games.find(g => g.id === selectedGameId);

  return (
    <div className="min-h-dvh bg-background text-white">
      {/* Header */}
      <header className="sticky top-0 bg-card/95 backdrop-blur-[16px] dark:backdrop-blur-none backdrop-blur-sm border-b border-border/30 z-50">
        <div className="max-w-5xl mx-auto px-6 py-4 flex justify-between items-center">
          <Link
            href="/board-game-ai"
            className="flex items-center gap-3 hover:opacity-80 transition-opacity"
          >
            <span className="text-3xl">🎲</span>
            <span className="text-xl font-bold gradient-text">Board Game AI</span>
          </Link>
          <div className="flex items-center gap-4">
            <Link href="/chat" className="text-slate-300 hover:text-white transition-colors">
              Chat
            </Link>
            <Link href="/upload" className="text-slate-300 hover:text-white transition-colors">
              Upload
            </Link>
            <Button asChild variant="outline" size="sm">
              <Link href="/">Home</Link>
            </Button>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-5xl mx-auto px-6 py-8">
        {/* Game Selection */}
        <Card className="p-6 mb-6">
          <div className="space-y-4">
            <div>
              <h1 className="text-3xl font-bold mb-2">Ask a Question</h1>
              <p className="text-slate-400">
                Select a game and ask any rule question. Our AI will provide an answer with
                citations.
              </p>
            </div>

            {gamesError && (
              <Alert variant="destructive">
                <AlertDescription>{gamesError}</AlertDescription>
              </Alert>
            )}

            <div className="space-y-2">
              <Label htmlFor="game-select">Select Game</Label>
              <Select
                value={selectedGameId}
                onValueChange={setSelectedGameId}
                disabled={loadingGames || streamState.isStreaming}
              >
                <SelectTrigger id="game-select" className="w-full">
                  <SelectValue placeholder={loadingGames ? 'Loading games...' : 'Select a game'} />
                </SelectTrigger>
                <SelectContent>
                  {games.map(game => (
                    <SelectItem key={game.id} value={game.id}>
                      {game.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {selectedGame?.description && (
                <p className="text-sm text-slate-400">{selectedGame.description}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="question-input">Your Question</Label>
              <Textarea
                id="question-input"
                value={question}
                onChange={e => setQuestion(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="e.g., Can I play a development card on the same turn I bought it in Catan?"
                rows={4}
                disabled={streamState.isStreaming || !selectedGameId}
                className="resize-none"
              />
              <p className="text-xs text-slate-400">
                Press <kbd className="px-1.5 py-0.5 bg-card rounded">Ctrl+Enter</kbd> to submit
              </p>
            </div>

            <Button
              onClick={handleAskQuestion}
              disabled={!selectedGameId || !question.trim() || streamState.isStreaming}
              className="w-full"
              size="lg"
            >
              {streamState.isStreaming ? (
                <>
                  <span className="inline-block animate-spin mr-2">⏳</span>
                  Thinking...
                </>
              ) : (
                '🚀 Ask Question'
              )}
            </Button>
          </div>
        </Card>

        {/* Loading State Indicator */}
        {streamState.stateMessage && (
          <Card className="p-4 mb-6 bg-blue-500/10 border-blue-500/30">
            <div className="flex items-center gap-2">
              <span className="inline-block animate-pulse">⚙️</span>
              <span className="text-blue-400 font-medium">{streamState.stateMessage}</span>
            </div>
          </Card>
        )}

        {/* Error Display */}
        {streamState.error?.message && (
          <Alert variant="destructive" className="mb-6">
            <AlertDescription>{streamState.error?.message}</AlertDescription>
          </Alert>
        )}

        {/* Streaming Response (Issue #3373: Show progressive answer) */}
        {streamState.isStreaming && streamState.currentAnswer && (
          <div className="space-y-6 mb-6">
            <h2 className="text-2xl font-bold">Streaming Response</h2>
            <motion.div
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.3 }}
            >
              <div className="flex justify-start">
                <Card className="max-w-[90%] p-4 bg-blue-500/5 border-blue-500/30">
                  <div className="space-y-3">
                    <div className="flex items-center gap-2 mb-2">
                      <span className="text-xl">🤖</span>
                      <span className="font-semibold">Board Game AI</span>
                      <span className="ml-auto flex items-center gap-1 text-xs text-blue-400">
                        <span className="inline-block animate-pulse">●</span>
                        typing...
                      </span>
                    </div>
                    <p className="whitespace-pre-wrap leading-relaxed">{streamState.currentAnswer}</p>
                  </div>
                </Card>
              </div>
            </motion.div>
          </div>
        )}

        {/* Conversation History */}
        {conversationHistory.length > 0 && (
          <div className="space-y-6">
            <h2 className="text-2xl font-bold">Conversation</h2>
            {conversationHistory.map((message, index) => (
              <motion.div
                key={index}
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.3 }}
              >
                {message.role === 'user' ? (
                  <div className="flex justify-end">
                    <Card className="max-w-[80%] p-4 bg-primary text-primary-foreground">
                      <p className="whitespace-pre-wrap">{message.content}</p>
                    </Card>
                  </div>
                ) : (
                  <div className="flex justify-start">
                    <Card className="max-w-[90%] p-4">
                      <div className="space-y-3">
                        <div className="flex items-center gap-2 mb-2">
                          <span className="text-xl">🤖</span>
                          <span className="font-semibold">Board Game AI</span>
                        </div>
                        <p className="whitespace-pre-wrap leading-relaxed">{message.content}</p>

                        {/* Citations */}
                        {message.citations && message.citations.length > 0 && (
                          <div className="mt-4 pt-4 border-t border-slate-700">
                            <h4 className="text-sm font-semibold mb-2">📖 Sources:</h4>
                            <div className="space-y-2">
                              {message.citations.map((citation, citIndex) => (
                                <Card key={citIndex} className="p-3 bg-card/50">
                                  <p className="text-sm text-slate-300">
                                    Document: {citation.documentId}
                                    <span className="ml-2 text-slate-400">
                                      (Page {citation.pageNumber})
                                    </span>
                                  </p>
                                  {citation.snippet && (
                                    <p className="text-xs text-slate-400 mt-1 italic">
                                      "{citation.snippet}"
                                    </p>
                                  )}
                                  {citation.relevanceScore && (
                                    <p className="text-xs text-slate-500 mt-1">
                                      Relevance: {(citation.relevanceScore * 100).toFixed(1)}%
                                    </p>
                                  )}
                                </Card>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                    </Card>
                  </div>
                )}
              </motion.div>
            ))}
          </div>
        )}

        {/* Empty State */}
        {conversationHistory.length === 0 && !streamState.isStreaming && (
          <Card className="p-12 text-center">
            <div className="space-y-4">
              <div className="text-6xl">🎲</div>
              <h3 className="text-2xl font-bold">Ready to Answer Your Questions</h3>
              <p className="text-slate-400 max-w-md mx-auto">
                Select a game above and ask any rule question. Our AI will provide accurate answers
                with citations from official rulebooks.
              </p>
            </div>
          </Card>
        )}
      </main>
    </div>
  );
}
