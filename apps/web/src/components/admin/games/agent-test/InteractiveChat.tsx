'use client';

/**
 * Interactive Chat
 * Free-form Q&A with the game's RAG agent. Shows responses with confidence and chunk info.
 */

import { useState, useRef, useEffect, useCallback } from 'react';

import {
  SendHorizontalIcon,
  LoaderCircleIcon,
  BotIcon,
  UserIcon,
  DatabaseIcon,
  ClockIcon,
  TargetIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent } from '@/components/ui/data-display/card';

import {
  useAskAgentQuestion,
  type AgentChatMessage,
} from '@/hooks/queries/useAgentTesting';

// ─── Types ───────────────────────────────────────────────────────────────────

interface InteractiveChatProps {
  gameId: string;
  gameTitle?: string;
}

// ─── Chat Message Component ──────────────────────────────────────────────────

function ChatMessageBubble({ message }: { message: AgentChatMessage }) {
  const isUser = message.role === 'user';

  return (
    <div className={`flex gap-3 ${isUser ? 'justify-end' : 'justify-start'}`}>
      {!isUser && (
        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-amber-100 dark:bg-amber-900/30">
          <BotIcon className="h-4 w-4 text-amber-600 dark:text-amber-400" />
        </div>
      )}
      <div
        className={`max-w-[80%] rounded-2xl px-4 py-3 ${
          isUser
            ? 'bg-amber-500 text-white'
            : 'bg-slate-100 dark:bg-zinc-800 text-foreground'
        }`}
      >
        <p className="text-sm whitespace-pre-wrap">{message.content}</p>

        {/* Metadata badges for assistant messages */}
        {!isUser && (message.confidence !== undefined || message.latencyMs !== undefined) && (
          <div className="flex flex-wrap gap-2 mt-2 pt-2 border-t border-slate-200/50 dark:border-zinc-700/50">
            {message.confidence !== undefined && (
              <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
                <TargetIcon className="h-3 w-3" />
                {(message.confidence * 100).toFixed(0)}% confidence
              </span>
            )}
            {message.latencyMs !== undefined && (
              <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
                <ClockIcon className="h-3 w-3" />
                {message.latencyMs}ms
              </span>
            )}
            {message.chunksRetrieved !== undefined && (
              <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
                <DatabaseIcon className="h-3 w-3" />
                {message.chunksRetrieved} chunks
              </span>
            )}
          </div>
        )}
      </div>
      {isUser && (
        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-slate-200 dark:bg-zinc-700">
          <UserIcon className="h-4 w-4 text-muted-foreground" />
        </div>
      )}
    </div>
  );
}

// ─── Main Component ──────────────────────────────────────────────────────────

export function InteractiveChat({ gameId, gameTitle }: InteractiveChatProps) {
  const [messages, setMessages] = useState<AgentChatMessage[]>([]);
  const [input, setInput] = useState('');
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const { mutateAsync: askQuestion, isPending } = useAskAgentQuestion();

  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [messages, scrollToBottom]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const question = input.trim();
    if (!question || isPending) return;

    // Add user message
    const userMessage: AgentChatMessage = {
      role: 'user',
      content: question,
      timestamp: new Date(),
    };
    setMessages((prev) => [...prev, userMessage]);
    setInput('');

    try {
      const response = await askQuestion({ gameId, question });

      const avgConfidence =
        response.retrievedChunks.length > 0
          ? response.retrievedChunks.reduce((sum, c) => sum + c.relevanceScore, 0) /
            response.retrievedChunks.length
          : 0;

      const assistantMessage: AgentChatMessage = {
        role: 'assistant',
        content: response.answer ?? 'No answer could be generated for this question.',
        confidence: avgConfidence,
        latencyMs: response.latencyMs,
        chunksRetrieved: response.retrievedChunks.length,
        timestamp: new Date(),
      };
      setMessages((prev) => [...prev, assistantMessage]);
    } catch {
      const errorMessage: AgentChatMessage = {
        role: 'assistant',
        content: 'Sorry, I encountered an error while processing your question. Please try again.',
        timestamp: new Date(),
      };
      setMessages((prev) => [...prev, errorMessage]);
    }
  };

  return (
    <div className="flex flex-col h-[600px]">
      {/* Messages area */}
      <Card className="flex-1 overflow-hidden bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
        <CardContent className="p-4 h-full overflow-y-auto">
          {messages.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full text-center">
              <BotIcon className="h-12 w-12 text-muted-foreground/30 mb-4" />
              <p className="text-sm text-muted-foreground">
                Ask any question about {gameTitle ?? 'this game'}&apos;s rules.
              </p>
              <p className="text-xs text-muted-foreground mt-1">
                The agent uses RAG to answer from the processed rulebook.
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {messages.map((msg) => (
                <ChatMessageBubble key={`${msg.role}-${msg.timestamp.getTime()}`} message={msg} />
              ))}
              {isPending && (
                <div className="flex gap-3 justify-start">
                  <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-amber-100 dark:bg-amber-900/30">
                    <BotIcon className="h-4 w-4 text-amber-600 dark:text-amber-400" />
                  </div>
                  <div className="rounded-2xl bg-slate-100 dark:bg-zinc-800 px-4 py-3">
                    <LoaderCircleIcon className="h-4 w-4 animate-spin text-muted-foreground" />
                  </div>
                </div>
              )}
              <div ref={messagesEndRef} />
            </div>
          )}
        </CardContent>
      </Card>

      {/* Input area */}
      <form onSubmit={handleSubmit} className="mt-3 flex gap-2">
        <input
          type="text"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder="Ask a question about the game rules..."
          disabled={isPending}
          className="flex-1 rounded-lg border border-slate-200 dark:border-zinc-700 bg-white dark:bg-zinc-900 px-4 py-2.5 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-amber-500/50 disabled:opacity-50"
        />
        <Button
          type="submit"
          disabled={isPending || !input.trim()}
          className="bg-amber-500 hover:bg-amber-600 text-white"
        >
          <SendHorizontalIcon className="h-4 w-4" />
        </Button>
      </form>
    </div>
  );
}
