/**
 * Inline Chat Panel - Admin RAG Dashboard
 *
 * Private chat interface for admin to test RAG agent.
 * Uses SSE streaming via useAgentChatStream hook.
 */

'use client';

import { useState, useRef, useEffect } from 'react';

import { Bot, MessageSquare, Send, Loader2, User } from 'lucide-react';

import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useAgentChatStream } from '@/hooks/useAgentChatStream';
import { cn } from '@/lib/utils';

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

interface InlineChatPanelProps {
  agentId: string | null;
  chatThreadId: string | null;
}

export function InlineChatPanel({ agentId, chatThreadId }: InlineChatPanelProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [threadId, setThreadId] = useState(chatThreadId);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const { state, sendMessage, stopStreaming } = useAgentChatStream({
    onComplete: (answer, metadata) => {
      setMessages((prev) => [
        ...prev,
        {
          id: `assistant-${Date.now()}`,
          role: 'assistant',
          content: answer,
          timestamp: new Date(),
        },
      ]);
      if (metadata.chatThreadId) {
        setThreadId(metadata.chatThreadId);
      }
    },
  });

  // Auto-scroll to bottom on new messages
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, state.currentAnswer]);

  const handleSend = () => {
    if (!input.trim() || !agentId || state.isStreaming) return;

    const userMessage: ChatMessage = {
      id: `user-${Date.now()}`,
      role: 'user',
      content: input.trim(),
      timestamp: new Date(),
    };

    setMessages((prev) => [...prev, userMessage]);
    sendMessage(agentId, input.trim(), threadId ?? undefined);
    setInput('');
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  // Empty state when no agent
  if (!agentId) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <MessageSquare className="h-5 w-5" />
            Chat di Test
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col items-center justify-center py-12 text-center text-muted-foreground">
            <Bot className="mb-3 h-10 w-10 opacity-30" />
            <p className="font-medium">Crea un agente per avviare la chat</p>
            <p className="mt-1 text-sm">
              Prima carica documenti e crea un agente RAG
            </p>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="flex flex-col">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <MessageSquare className="h-5 w-5" />
          Chat di Test (Privata)
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-1 flex-col space-y-3">
        {/* Messages area */}
        <div className="flex h-[400px] flex-col gap-3 overflow-y-auto rounded-lg border bg-muted/20 p-3">
          {messages.length === 0 && !state.isStreaming && (
            <div className="flex flex-1 items-center justify-center text-sm text-muted-foreground">
              Scrivi un messaggio per testare l&apos;agente RAG
            </div>
          )}

          {messages.map((msg) => (
            <div
              key={msg.id}
              className={cn(
                'flex gap-2',
                msg.role === 'user' ? 'justify-end' : 'justify-start'
              )}
            >
              {msg.role === 'assistant' && (
                <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary/10">
                  <Bot className="h-4 w-4 text-primary" />
                </div>
              )}
              <div
                className={cn(
                  'max-w-[80%] rounded-lg px-3 py-2 text-sm',
                  msg.role === 'user'
                    ? 'bg-primary text-primary-foreground'
                    : 'bg-muted'
                )}
              >
                <p className="whitespace-pre-wrap">{msg.content}</p>
              </div>
              {msg.role === 'user' && (
                <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-muted">
                  <User className="h-4 w-4" />
                </div>
              )}
            </div>
          ))}

          {/* Streaming response */}
          {state.isStreaming && state.currentAnswer && (
            <div className="flex gap-2">
              <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary/10">
                <Bot className="h-4 w-4 text-primary" />
              </div>
              <div className="max-w-[80%] rounded-lg bg-muted px-3 py-2 text-sm">
                <p className="whitespace-pre-wrap">{state.currentAnswer}</p>
                <Loader2 className="mt-1 inline h-3 w-3 animate-spin text-muted-foreground" />
              </div>
            </div>
          )}

          {/* Status message */}
          {state.isStreaming && !state.currentAnswer && state.statusMessage && (
            <div className="flex items-center justify-center gap-2 text-xs text-muted-foreground">
              <Loader2 className="h-3 w-3 animate-spin" />
              {state.statusMessage}
            </div>
          )}

          {/* Error message */}
          {state.error && (
            <div className="rounded-lg border border-destructive/20 bg-destructive/5 p-2 text-xs text-destructive">
              {state.error}
            </div>
          )}

          <div ref={messagesEndRef} />
        </div>

        {/* Input area */}
        <div className="flex gap-2">
          <Textarea
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Scrivi un messaggio per testare il RAG..."
            className="min-h-[44px] resize-none"
            rows={1}
            disabled={state.isStreaming}
          />
          {state.isStreaming ? (
            <Button
              variant="outline"
              size="icon"
              onClick={stopStreaming}
              className="shrink-0"
            >
              <span className="sr-only">Stop</span>
              <div className="h-3 w-3 rounded-sm bg-foreground" />
            </Button>
          ) : (
            <Button
              size="icon"
              onClick={handleSend}
              disabled={!input.trim()}
              className="shrink-0"
            >
              <Send className="h-4 w-4" />
              <span className="sr-only">Invia</span>
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
