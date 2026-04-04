/**
 * Agent Chat Child Card — /sessions/live/[sessionId]/agent
 *
 * Game Night Improvvisata — Task 22
 *
 * Provides an in-session chat interface for querying the game Arbitro
 * agent. Includes ArbitroModal (for formal disputes) and DisputeHistory.
 *
 * Note: The existing ChatThreadView is deeply coupled to the toolkit
 * session system. This page implements a simplified inline chat that
 * works directly with the live session context.
 */

'use client';

import { use, useState, useRef } from 'react';

import { Bot, Loader2, Send } from 'lucide-react';

import { ArbitroModal } from '@/components/session/live/ArbitroModal';
import { DisputeHistory } from '@/components/session/live/DisputeHistory';
import { Button } from '@/components/ui/primitives/button';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

// ─── Types ────────────────────────────────────────────────────────────────────

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: number;
}

// ─── Sub-components ───────────────────────────────────────────────────────────

interface MessageBubbleProps {
  message: ChatMessage;
}

function MessageBubble({ message }: MessageBubbleProps) {
  const isUser = message.role === 'user';

  return (
    <div className={`flex ${isUser ? 'justify-end' : 'justify-start'} gap-2`}>
      {!isUser && (
        <div className="h-7 w-7 rounded-full bg-amber-100 border border-amber-200 flex items-center justify-center shrink-0 mt-1">
          <Bot className="h-4 w-4 text-amber-600" />
        </div>
      )}
      <div
        className={[
          'max-w-[80%] rounded-2xl px-4 py-2.5 text-sm font-nunito',
          isUser
            ? 'bg-amber-500 text-white rounded-tr-sm'
            : 'bg-white/80 backdrop-blur-sm border border-white/60 text-gray-800 rounded-tl-sm shadow-sm',
        ].join(' ')}
      >
        <p className="whitespace-pre-wrap leading-relaxed">{message.content}</p>
      </div>
    </div>
  );
}

// ─── Main Component ────────────────────────────────────────────────────────────

interface AgentPageProps {
  params: Promise<{ sessionId: string }>;
}

export default function AgentPage({ params }: AgentPageProps) {
  const { sessionId } = use(params);

  const players = useLiveSessionStore(s => s.players);
  const gameName = useLiveSessionStore(s => s.gameName);

  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: 'welcome',
      role: 'assistant',
      content: `Ciao! Sono l'Arbitro per ${gameName || 'la vostra partita'}. Chiedimi qualsiasi cosa sulle regole del gioco, oppure usa "⚖️ Arbitro" per una contestazione formale.`,
      timestamp: Date.now(),
    },
  ]);
  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  function scrollToBottom() {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }

  async function handleSend(e: React.FormEvent) {
    e.preventDefault();
    const trimmed = input.trim();
    if (!trimmed || isLoading) return;

    const userMessage: ChatMessage = {
      id: `user-${Date.now()}`,
      role: 'user',
      content: trimmed,
      timestamp: Date.now(),
    };

    setMessages(prev => [...prev, userMessage]);
    setInput('');
    setIsLoading(true);
    scrollToBottom();

    try {
      // POST to the agent chat endpoint with session context for RAG injection
      const res = await fetch('/api/v1/agent/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          message: trimmed,
          sessionContext: { sessionId, gameName },
        }),
      });

      if (!res.ok) throw new Error('Agent unavailable');

      const assistantId = `assistant-${Date.now()}`;
      let assistantContent = '';

      // Add placeholder message that we'll fill from the stream
      setMessages(prev => [
        ...prev,
        { id: assistantId, role: 'assistant', content: '...', timestamp: Date.now() },
      ]);

      const reader = res.body?.getReader();
      const decoder = new TextDecoder();

      if (reader) {
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          const chunk = decoder.decode(value, { stream: true });
          for (const line of chunk.split('\n')) {
            if (!line.startsWith('data: ')) continue;
            const data = line.slice(6);
            if (data === '[DONE]') break;
            try {
              const parsed = JSON.parse(data) as { content?: string };
              if (parsed.content) {
                assistantContent += parsed.content;
                setMessages(prev =>
                  prev.map(m => (m.id === assistantId ? { ...m, content: assistantContent } : m))
                );
                scrollToBottom();
              }
            } catch {
              // Non-JSON SSE chunk — ignore
            }
          }
        }
      }

      // If stream produced nothing, fallback to full JSON response
      if (!assistantContent) {
        const fallback = (await res.json().catch(() => null)) as { content?: string } | null;
        setMessages(prev =>
          prev.map(m =>
            m.id === assistantId
              ? { ...m, content: fallback?.content ?? 'Risposta non disponibile.' }
              : m
          )
        );
      }
    } catch {
      setMessages(prev => [
        ...prev,
        {
          id: `error-${Date.now()}`,
          role: 'assistant',
          content:
            "Non riesco a rispondere al momento. Usa '⚖️ Arbitro' per una contestazione formale.",
          timestamp: Date.now(),
        },
      ]);
    } finally {
      setIsLoading(false);
      scrollToBottom();
    }
  }

  function handleKeyDown(e: React.KeyboardEvent) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend(e as unknown as React.FormEvent);
    }
  }

  return (
    <div className="flex flex-col" style={{ minHeight: 'calc(100vh - 12rem)' }}>
      {/* Header */}
      <div className="flex items-center justify-between p-4 pb-2 shrink-0">
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-full bg-amber-100 border border-amber-200 flex items-center justify-center">
            <Bot className="h-4 w-4 text-amber-600" />
          </div>
          <div>
            <h2 className="text-base font-quicksand font-bold text-gray-900 leading-none">
              Arbitro AI
            </h2>
            <p className="text-xs text-gray-500 font-nunito">{gameName || 'Sessione live'}</p>
          </div>
        </div>

        {/* ArbitroModal renders its own trigger button */}
        <ArbitroModal sessionId={sessionId} players={players.map(p => ({ name: p.name }))} />
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto px-4 py-2 space-y-3">
        {messages.map(msg => (
          <MessageBubble key={msg.id} message={msg} />
        ))}

        {isLoading && (
          <div className="flex justify-start gap-2">
            <div className="h-7 w-7 rounded-full bg-amber-100 border border-amber-200 flex items-center justify-center shrink-0">
              <Loader2 className="h-4 w-4 text-amber-600 animate-spin" />
            </div>
            <div className="rounded-2xl rounded-tl-sm bg-white/80 border border-white/60 px-4 py-3 shadow-sm">
              <div className="flex gap-1 items-center">
                <span className="h-1.5 w-1.5 rounded-full bg-gray-400 animate-bounce [animation-delay:0ms]" />
                <span className="h-1.5 w-1.5 rounded-full bg-gray-400 animate-bounce [animation-delay:150ms]" />
                <span className="h-1.5 w-1.5 rounded-full bg-gray-400 animate-bounce [animation-delay:300ms]" />
              </div>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Input */}
      <form onSubmit={handleSend} className="shrink-0 px-4 pb-4 pt-2 flex gap-2 items-end">
        <Textarea
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Chiedi una regola... (Invio per inviare)"
          className="flex-1 resize-none min-h-[42px] max-h-[120px] font-nunito text-sm"
          rows={1}
          disabled={isLoading}
          aria-label="Messaggio per l'arbitro"
        />
        <Button
          type="submit"
          size="icon"
          disabled={isLoading || !input.trim()}
          className="h-[42px] w-[42px] bg-amber-500 hover:bg-amber-600 shrink-0"
          aria-label="Invia messaggio"
        >
          <Send className="h-4 w-4 text-white" />
        </Button>
      </form>

      {/* Dispute History collapsible */}
      <div className="shrink-0 px-4 pb-4">
        <DisputeHistory sessionId={sessionId} />
      </div>
    </div>
  );
}
