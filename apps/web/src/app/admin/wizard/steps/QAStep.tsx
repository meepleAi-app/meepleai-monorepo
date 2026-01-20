'use client';

/**
 * Step 4: Q&A
 *
 * Ask questions about the game rules and receive answers from the RAG agent.
 * Uses SSE streaming for real-time response.
 */

import { useState, useCallback, useRef } from 'react';

import Link from 'next/link';

import { toast } from '@/components/layout';
import { Spinner } from '@/components/loading';
import { Card } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';

interface QAStepProps {
  gameId: string;
  gameName: string;
  chatThreadId: string;
  onReset: () => void;
}

interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
  citations?: Citation[];
}

interface Citation {
  documentId: string;
  pageNumber?: number;
  section?: string;
  relevanceScore: number;
  text: string;
}

export function QAStep({ gameId, gameName, chatThreadId, onReset }: QAStepProps) {
  const [question, setQuestion] = useState('');
  const [messages, setMessages] = useState<Message[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [currentAnswer, setCurrentAnswer] = useState('');
  const [_currentCitations, setCurrentCitations] = useState<Citation[]>([]);
  const abortControllerRef = useRef<AbortController | null>(null);

  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  const handleAsk = useCallback(async () => {
    if (!question.trim() || isStreaming) return;

    const userMessage: Message = {
      id: `user-${Date.now()}`,
      role: 'user',
      content: question.trim(),
      timestamp: new Date(),
    };

    setMessages(prev => [...prev, userMessage]);
    setQuestion('');
    setIsStreaming(true);
    setCurrentAnswer('');
    setCurrentCitations([]);

    abortControllerRef.current = new AbortController();

    try {
      const response = await fetch(`${API_BASE}/api/v1/chat-threads/${chatThreadId}/messages`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'text/event-stream',
        },
        credentials: 'include',
        body: JSON.stringify({
          question: userMessage.content,
          gameId,
          threadId: chatThreadId,
        }),
        signal: abortControllerRef.current.signal,
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const reader = response.body?.getReader();
      if (!reader) {
        throw new Error('No response body');
      }

      const decoder = new TextDecoder();
      let fullAnswer = '';
      let citations: Citation[] = [];

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        const chunk = decoder.decode(value, { stream: true });
        const lines = chunk.split('\n');

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.slice(6);
            if (data === '[DONE]') continue;

            try {
              const parsed = JSON.parse(data);

              if (parsed.token) {
                fullAnswer += parsed.token;
                setCurrentAnswer(fullAnswer);
              }

              if (parsed.citations) {
                citations = parsed.citations;
                setCurrentCitations(citations);
              }

              if (parsed.error) {
                throw new Error(parsed.error);
              }
            } catch (_parseErr) {
              // Ignore parse errors for incomplete JSON
              if (data !== '[DONE]' && data.trim()) {
                // It might be plain text token
                fullAnswer += data;
                setCurrentAnswer(fullAnswer);
              }
            }
          }
        }
      }

      // Add assistant message when complete
      const assistantMessage: Message = {
        id: `assistant-${Date.now()}`,
        role: 'assistant',
        content: fullAnswer,
        timestamp: new Date(),
        citations,
      };

      setMessages(prev => [...prev, assistantMessage]);
      setCurrentAnswer('');
      setCurrentCitations([]);
    } catch (err) {
      if (err instanceof Error && err.name === 'AbortError') {
        // User cancelled
        return;
      }
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error(`Errore: ${message}`);
    } finally {
      setIsStreaming(false);
      abortControllerRef.current = null;
    }
  }, [question, isStreaming, gameId, chatThreadId, API_BASE]);

  const handleStop = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }
  }, []);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        void handleAsk();
      }
    },
    [handleAsk]
  );

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
          Q&A: {gameName}
        </h2>
        <p className="text-slate-600 dark:text-slate-400">
          Fai una domanda sul regolamento e l'agente RAG rispondera' basandosi sul PDF.
        </p>
      </div>

      {/* Messages */}
      <Card className="p-4 min-h-[300px] max-h-[400px] overflow-y-auto space-y-4">
        {messages.length === 0 && !isStreaming && (
          <div className="text-center text-slate-500 dark:text-slate-400 py-12">
            <span className="text-4xl block mb-2">💬</span>
            <p>Fai la prima domanda sul regolamento!</p>
            <p className="text-sm mt-2">
              Esempio: "Come si vince una partita?" o "Quante carte si pescano all'inizio?"
            </p>
          </div>
        )}

        {messages.map(msg => (
          <div
            key={msg.id}
            className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}
          >
            <div
              className={`max-w-[80%] p-3 rounded-lg ${
                msg.role === 'user'
                  ? 'bg-blue-600 text-white'
                  : 'bg-slate-100 dark:bg-slate-700 text-slate-900 dark:text-white'
              }`}
            >
              <p className="whitespace-pre-wrap">{msg.content}</p>

              {/* Citations */}
              {msg.citations && msg.citations.length > 0 && (
                <div className="mt-3 pt-2 border-t border-slate-300 dark:border-slate-600">
                  <p className="text-xs font-medium mb-1 opacity-75">Fonti:</p>
                  <div className="space-y-1">
                    {msg.citations.slice(0, 3).map((cit, idx) => (
                      <div
                        key={idx}
                        className="text-xs opacity-75 bg-slate-200 dark:bg-slate-600 px-2 py-1 rounded"
                      >
                        {cit.section && <span>§ {cit.section}</span>}
                        {cit.pageNumber && <span> (p. {cit.pageNumber})</span>}
                        <span className="ml-1">- {cit.relevanceScore.toFixed(0)}%</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        ))}

        {/* Streaming answer */}
        {isStreaming && currentAnswer && (
          <div className="flex justify-start">
            <div className="max-w-[80%] p-3 rounded-lg bg-slate-100 dark:bg-slate-700 text-slate-900 dark:text-white">
              <p className="whitespace-pre-wrap">{currentAnswer}</p>
              <span className="inline-block w-2 h-4 bg-blue-600 animate-pulse ml-1" />
            </div>
          </div>
        )}

        {/* Loading indicator */}
        {isStreaming && !currentAnswer && (
          <div className="flex justify-start">
            <div className="p-3 rounded-lg bg-slate-100 dark:bg-slate-700">
              <div className="flex items-center gap-2">
                <Spinner size="sm" />
                <span className="text-slate-500">Sto pensando...</span>
              </div>
            </div>
          </div>
        )}
      </Card>

      {/* Input */}
      <div className="flex gap-2">
        <Input
          value={question}
          onChange={e => setQuestion(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Scrivi la tua domanda..."
          disabled={isStreaming}
          className="flex-1"
        />
        {isStreaming ? (
          <Button onClick={handleStop} variant="destructive">
            Stop
          </Button>
        ) : (
          <Button onClick={handleAsk} disabled={!question.trim()}>
            Chiedi
          </Button>
        )}
      </div>

      {/* Actions */}
      <div className="flex justify-between gap-3 pt-4 border-t">
        <Button variant="outline" onClick={onReset}>
          Nuovo Gioco
        </Button>
        <div className="flex gap-2">
          <Button variant="outline" asChild>
            <Link href={`/chat?gameId=${gameId}`}>Apri Chat Completa</Link>
          </Button>
          <Button asChild>
            <Link href="/admin">Torna ad Admin</Link>
          </Button>
        </div>
      </div>
    </div>
  );
}
