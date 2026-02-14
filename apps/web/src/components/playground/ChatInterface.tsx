'use client';

import { useEffect, useRef, useState } from 'react';

import { Send, ThumbsUp, ThumbsDown } from 'lucide-react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

import { Button, ScrollArea, Textarea } from '@/components/ui';
import { cn } from '@/lib/utils';
import { usePlaygroundStore } from '@/stores/playground-store';

interface ChatInterfaceProps {
  agentId: string;
  onSendMessage: (message: string) => Promise<void>;
}

export function ChatInterface({ agentId: _agentId, onSendMessage }: ChatInterfaceProps) {
  const [input, setInput] = useState('');
  const {
    messages, isStreaming, addMessage, setMessageFeedback,
    followUpQuestions, stateUpdates,
  } = usePlaygroundStore();
  const scrollRef = useRef<HTMLDivElement>(null);

  // Auto-scroll to bottom on new messages
  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [messages, stateUpdates]);

  const handleSend = async () => {
    if (!input.trim() || isStreaming) return;

    const userMessage = input.trim();
    setInput('');

    addMessage({ role: 'user', content: userMessage });
    await onSendMessage(userMessage);
  };

  const handleFollowUp = async (question: string) => {
    if (isStreaming) return;

    setInput('');
    addMessage({ role: 'user', content: question });
    await onSendMessage(question);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      void handleSend();
    }
  };

  return (
    <div className="flex flex-col h-[600px] border rounded-lg">
      {/* Messages */}
      <ScrollArea className="flex-1 p-4" ref={scrollRef}>
        <div className="space-y-4">
          {messages.length === 0 && (
            <div className="text-center text-muted-foreground py-12">
              No messages yet. Start a conversation!
            </div>
          )}

          {messages.map((message) => (
            <div
              key={message.id}
              className={cn(
                'flex',
                message.role === 'user' ? 'justify-end' : 'justify-start'
              )}
            >
              <div
                className={cn(
                  'max-w-[80%] rounded-lg px-4 py-2',
                  message.role === 'user'
                    ? 'bg-primary text-primary-foreground'
                    : 'bg-muted'
                )}
              >
                <div className="text-xs opacity-70 mb-1">
                  {message.role === 'user' ? 'You' : 'Agent'}
                </div>

                {message.role === 'assistant' ? (
                  <div className="prose prose-sm dark:prose-invert max-w-none [&>*:first-child]:mt-0 [&>*:last-child]:mb-0">
                    <ReactMarkdown remarkPlugins={[remarkGfm]}>
                      {message.content || '\u200B'}
                    </ReactMarkdown>
                  </div>
                ) : (
                  <div className="whitespace-pre-wrap">{message.content}</div>
                )}

                {/* Metadata + Feedback row */}
                {message.role === 'assistant' && message.content && (
                  <div className="flex items-center justify-between mt-2 pt-1 border-t border-foreground/10">
                    <div className="text-xs opacity-70">
                      {message.metadata?.tokens && `${message.metadata.tokens} tokens`}
                      {message.metadata?.latency && ` \u00B7 ${message.metadata.latency}ms`}
                    </div>
                    <div className="flex gap-1">
                      <button
                        type="button"
                        onClick={() => setMessageFeedback(message.id, message.feedback === 'up' ? null : 'up')}
                        className={cn(
                          'p-1 rounded hover:bg-foreground/10 transition-colors',
                          message.feedback === 'up' && 'text-green-600 dark:text-green-400'
                        )}
                        aria-label="Thumbs up"
                      >
                        <ThumbsUp className={cn('h-3.5 w-3.5', message.feedback === 'up' && 'fill-current')} />
                      </button>
                      <button
                        type="button"
                        onClick={() => setMessageFeedback(message.id, message.feedback === 'down' ? null : 'down')}
                        className={cn(
                          'p-1 rounded hover:bg-foreground/10 transition-colors',
                          message.feedback === 'down' && 'text-red-600 dark:text-red-400'
                        )}
                        aria-label="Thumbs down"
                      >
                        <ThumbsDown className={cn('h-3.5 w-3.5', message.feedback === 'down' && 'fill-current')} />
                      </button>
                    </div>
                  </div>
                )}

                {/* User message metadata */}
                {message.role === 'user' && message.metadata && (
                  <div className="text-xs opacity-70 mt-2">
                    {message.metadata.tokens && `${message.metadata.tokens} tokens`}
                  </div>
                )}
              </div>
            </div>
          ))}

          {/* Pipeline step indicator during streaming */}
          {isStreaming && stateUpdates.length > 0 && (
            <div className="flex justify-start">
              <div className="text-xs text-muted-foreground px-4 py-1 italic">
                {stateUpdates[stateUpdates.length - 1]}
              </div>
            </div>
          )}

          {/* Typing indicator */}
          {isStreaming && (
            <div className="flex justify-start">
              <div className="bg-muted rounded-lg px-4 py-2">
                <div className="flex gap-1">
                  <div className="w-2 h-2 bg-current rounded-full animate-bounce" style={{ animationDelay: '0ms' }} />
                  <div className="w-2 h-2 bg-current rounded-full animate-bounce" style={{ animationDelay: '150ms' }} />
                  <div className="w-2 h-2 bg-current rounded-full animate-bounce" style={{ animationDelay: '300ms' }} />
                </div>
              </div>
            </div>
          )}
        </div>
      </ScrollArea>

      {/* Follow-up questions */}
      {followUpQuestions.length > 0 && !isStreaming && (
        <div className="px-4 pb-2 flex flex-wrap gap-2">
          {followUpQuestions.map((q, i) => (
            <button
              key={i}
              type="button"
              onClick={() => handleFollowUp(q)}
              className="text-sm px-3 py-1.5 rounded-full border bg-background hover:bg-muted transition-colors text-left"
            >
              {q}
            </button>
          ))}
        </div>
      )}

      {/* Input */}
      <div className="p-4 border-t">
        <div className="flex gap-2 items-end">
          <Textarea
            placeholder="Type your message... (Shift+Enter for new line)"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            disabled={isStreaming}
            rows={2}
            className="min-h-[60px] max-h-[160px] resize-none"
          />
          <Button
            onClick={handleSend}
            disabled={isStreaming || !input.trim()}
            size="icon"
            className="shrink-0 h-10 w-10"
          >
            <Send className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
