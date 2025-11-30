'use client';

/**
 * Board Game AI Ask - Client Component
 *
 * Interactive Q&A interface for board game rules.
 * Uses the useChatQuery hook to communicate with /api/v1/knowledge-base/ask.
 *
 * Issue #1006: Backend API Integration (Non-streaming)
 */

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { motion } from 'framer-motion';
import { api } from '@/lib/api';
import { useChatQuery } from '@/lib/hooks/useChatQuery';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import type { Citation } from '@/types';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

type Game = {
  id: string;
  title: string;
  description?: string;
  imageUrl?: string;
};

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

  // Query hook (Issue #1006: Non-streaming backend API)
  const [queryState, queryControls] = useChatQuery({
    onComplete: (answer, citations, metadata) => {
      // Add assistant response to conversation history
      setConversationHistory((prev) => [
        ...prev,
        {
          role: 'assistant',
          content: answer,
          citations: citations || [],
        },
      ]);
      setQuestion(''); // Clear input after successful response
    },
    onError: (error) => {
      logger.error(
        'Query error occurred',
        new Error(error),
        createErrorContext('BoardGameAskClient', 'queryError', { operation: 'knowledge_base_ask' })
      );
    },
  });

  // Fetch games on mount
  useEffect(() => {
    const abortController = new AbortController();

    const fetchGames = async () => {
      try {
        setLoadingGames(true);
        const response = await api.get<Game[]>('/api/v1/games');

        // Check if component is still mounted
        if (abortController.signal.aborted) return;

        if (response && Array.isArray(response)) {
          setGames(response);
          // Auto-select first game if available
          if (response.length > 0 && !selectedGameId) {
            setSelectedGameId(response[0].id);
          }
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
    if (!selectedGameId) {
      return;
    }
    if (!question.trim()) {
      return;
    }

    // Add user question to history
    setConversationHistory((prev) => [
      ...prev,
      { role: 'user', content: question },
    ]);

    // Ask question via backend API
    await queryControls.askQuestion(selectedGameId, question);
  };

  // Handle Enter key (Ctrl+Enter to submit)
  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      handleAskQuestion();
    }
  };

  const selectedGame = games.find((g) => g.id === selectedGameId);

  return (
    <div className="min-h-dvh bg-slate-950 text-white">
      {/* Header */}
      <header className="sticky top-0 bg-slate-900/95 backdrop-blur-sm border-b border-slate-800 z-50">
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
                Select a game and ask any rule question. Our AI will provide an answer with citations.
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
                disabled={loadingGames || queryState.isLoading}
              >
                <SelectTrigger id="game-select" className="w-full">
                  <SelectValue
                    placeholder={loadingGames ? 'Loading games...' : 'Select a game'}
                  />
                </SelectTrigger>
                <SelectContent>
                  {games.map((game) => (
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
                onChange={(e) => setQuestion(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="e.g., Can I play a development card on the same turn I bought it in Catan?"
                rows={4}
                disabled={queryState.isLoading || !selectedGameId}
                className="resize-none"
              />
              <p className="text-xs text-slate-400">
                Press <kbd className="px-1.5 py-0.5 bg-slate-800 rounded">Ctrl+Enter</kbd> to submit
              </p>
            </div>

            <Button
              onClick={handleAskQuestion}
              disabled={!selectedGameId || !question.trim() || queryState.isLoading}
              className="w-full"
              size="lg"
            >
              {queryState.isLoading ? (
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
        {queryState.state && (
          <Card className="p-4 mb-6 bg-blue-500/10 border-blue-500/30">
            <div className="flex items-center gap-2">
              <span className="inline-block animate-pulse">⚙️</span>
              <span className="text-blue-400 font-medium">{queryState.state}</span>
            </div>
          </Card>
        )}

        {/* Error Display */}
        {queryState.error && (
          <Alert variant="destructive" className="mb-6">
            <AlertDescription>{queryState.error}</AlertDescription>
          </Alert>
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
                                <Card key={citIndex} className="p-3 bg-slate-800/50">
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
        {conversationHistory.length === 0 && !queryState.isLoading && (
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
