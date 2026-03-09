'use client';

/**
 * Admin Debug Chat - Real-time RAG Pipeline Debug
 *
 * Split-view page: live chat on left, real-time pipeline debug on right.
 * Strategy switcher allows re-executing queries with different RAG strategies.
 *
 * Protected by admin layout (RequireRole(['Admin'])).
 */

import { useState, useCallback, useRef } from 'react';

import { SendIcon, LinkIcon } from 'lucide-react';
import { useSearchParams } from 'next/navigation';

import { DebugTimeline } from '@/components/admin/debug-chat/DebugTimeline';
import { StrategySelectorBar } from '@/components/admin/debug-chat/StrategySelectorBar';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { useDebugChatStream } from '@/hooks/useDebugChatStream';
import { useLocalStorage } from '@/hooks/useLocalStorage';
import { cn } from '@/lib/utils';

// ─── Types ────────────────────────────────────────────────────────────────────

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

// ─── Component ────────────────────────────────────────────────────────────────

export default function AdminDebugChatPage() {
  // Deep link: executionId from TechnicalDetailsPanel (Issue #5486)
  const searchParams = useSearchParams();
  const linkedExecutionId = searchParams.get('executionId');

  // Chat state
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [inputValue, setInputValue] = useState('');
  const [selectedGameId, setSelectedGameId] = useState('');
  const [selectedStrategy, setSelectedStrategy] = useState('');
  const lastQueryRef = useRef<{ gameId: string; query: string } | null>(null);
  const chatEndRef = useRef<HTMLDivElement>(null);
  const messageIdRef = useRef(0);

  // Debug panel toggle — persisted in localStorage
  const [showDebug, setShowDebug] = useLocalStorage('admin-debug-panel-visible', true);
  const [mobileDebugOpen, setMobileDebugOpen] = useState(false);

  const handleToggleDebug = useCallback(() => {
    setShowDebug(prev => !prev);
  }, [setShowDebug]);

  const { state, sendMessage, stopStreaming, reset } = useDebugChatStream({
    onComplete: answer => {
      // Add completed assistant message
      setMessages(prev => {
        const updated = [...prev];
        // Replace or add the streaming message
        const lastIdx = updated.findLastIndex(m => m.role === 'assistant');
        if (lastIdx >= 0) {
          updated[lastIdx] = { ...updated[lastIdx], content: answer };
        }
        return updated;
      });
    },
    onError: error => {
      setMessages(prev => [
        ...prev,
        {
          id: `error-${Date.now()}`,
          role: 'assistant',
          content: `Error: ${error}`,
          timestamp: new Date(),
        },
      ]);
    },
  });

  // ─── Handlers ───────────────────────────────────────────────────────────────

  const handleSend = useCallback(() => {
    const query = inputValue.trim();
    if (!query || !selectedGameId || state.isStreaming) return;

    // Add user message
    const userMsgId = ++messageIdRef.current;
    const assistantMsgId = ++messageIdRef.current;
    setMessages(prev => [
      ...prev,
      {
        id: `user-${userMsgId}`,
        role: 'user',
        content: query,
        timestamp: new Date(),
      },
      {
        id: `assistant-${assistantMsgId}`,
        role: 'assistant',
        content: '',
        timestamp: new Date(),
      },
    ]);

    lastQueryRef.current = { gameId: selectedGameId, query };
    setInputValue('');

    const strategy = selectedStrategy === '__default__' ? undefined : selectedStrategy || undefined;
    sendMessage(selectedGameId, query, strategy, state.chatThreadId ?? undefined);

    // Scroll chat to bottom
    setTimeout(() => chatEndRef.current?.scrollIntoView({ behavior: 'smooth' }), 100);
  }, [
    inputValue,
    selectedGameId,
    selectedStrategy,
    state.isStreaming,
    state.chatThreadId,
    sendMessage,
  ]);

  const handleReExecute = useCallback(() => {
    if (!lastQueryRef.current || state.isStreaming) return;

    const { gameId, query } = lastQueryRef.current;

    // Reset debug events but keep chat
    reset();

    const reUserMsgId = ++messageIdRef.current;
    const reAssistantMsgId = ++messageIdRef.current;
    setMessages(prev => [
      ...prev,
      {
        id: `user-${reUserMsgId}`,
        role: 'user',
        content: `[Re-execute] ${query}`,
        timestamp: new Date(),
      },
      {
        id: `assistant-${reAssistantMsgId}`,
        role: 'assistant',
        content: '',
        timestamp: new Date(),
      },
    ]);

    const strategy = selectedStrategy === '__default__' ? undefined : selectedStrategy || undefined;
    sendMessage(gameId, query, strategy);
  }, [state.isStreaming, selectedStrategy, sendMessage, reset]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        handleSend();
      }
    },
    [handleSend]
  );

  // ─── Render ─────────────────────────────────────────────────────────────────

  // Current streaming answer overlaid on last assistant message
  const displayMessages = messages.map((msg, i) => {
    if (
      msg.role === 'assistant' &&
      i === messages.length - 1 &&
      state.isStreaming &&
      state.currentAnswer
    ) {
      return { ...msg, content: state.currentAnswer };
    }
    return msg;
  });

  return (
    <div className="flex h-[calc(100vh-4rem)] flex-col">
      {/* Top bar: game + strategy selectors */}
      <StrategySelectorBar
        selectedGameId={selectedGameId}
        onGameChange={setSelectedGameId}
        selectedStrategy={selectedStrategy}
        onStrategyChange={setSelectedStrategy}
        onReExecute={handleReExecute}
        isStreaming={state.isStreaming}
        hasLastQuery={!!lastQueryRef.current}
        showDebug={showDebug}
        onToggleDebug={handleToggleDebug}
      />

      {/* Deep link context banner (Issue #5486) */}
      {linkedExecutionId && (
        <div
          className="flex items-center gap-2 border-b bg-blue-500/10 px-4 py-2 text-xs text-blue-700 dark:text-blue-300"
          data-testid="execution-context-banner"
        >
          <LinkIcon className="h-3.5 w-3.5 flex-shrink-0" />
          <span>
            Sessione collegata · Execution ID:{' '}
            <code className="rounded bg-blue-500/10 px-1 py-0.5 font-mono text-[10px]">
              {linkedExecutionId}
            </code>
          </span>
        </div>
      )}

      {/* Main split view */}
      <div
        className={cn(
          'flex-1 grid min-h-0',
          showDebug ? 'grid-cols-1 lg:grid-cols-[55%_45%]' : 'grid-cols-1'
        )}
      >
        {/* ── Left Panel: Chat ────────────────────────────────────────── */}
        <div className="flex flex-col border-r min-h-0">
          {/* Messages */}
          <div className="flex-1 overflow-y-auto px-4 py-4 space-y-4">
            {displayMessages.length === 0 ? (
              <div className="flex h-full items-center justify-center">
                <p className="text-sm text-muted-foreground text-center max-w-xs">
                  Select a game and ask a question to start a debug chat session. Debug events will
                  appear in the right panel.
                </p>
              </div>
            ) : (
              displayMessages.map(msg => (
                <div
                  key={msg.id}
                  className={cn('flex', msg.role === 'user' ? 'justify-end' : 'justify-start')}
                >
                  <div
                    className={cn(
                      'max-w-[80%] rounded-lg px-3.5 py-2.5 text-sm',
                      msg.role === 'user'
                        ? 'bg-primary text-primary-foreground'
                        : 'bg-muted text-foreground'
                    )}
                  >
                    {msg.content ||
                      (state.isStreaming ? (
                        <span className="inline-flex items-center gap-1 text-muted-foreground">
                          <span className="h-1.5 w-1.5 rounded-full bg-current animate-pulse" />
                          <span className="h-1.5 w-1.5 rounded-full bg-current animate-pulse [animation-delay:0.2s]" />
                          <span className="h-1.5 w-1.5 rounded-full bg-current animate-pulse [animation-delay:0.4s]" />
                        </span>
                      ) : null)}
                  </div>
                </div>
              ))
            )}
            <div ref={chatEndRef} />
          </div>

          {/* Status bar */}
          {state.statusMessage && (
            <div className="border-t px-4 py-1.5 text-xs text-muted-foreground bg-muted/30">
              {state.statusMessage}
            </div>
          )}

          {/* Input */}
          <div className="border-t px-4 py-3">
            <div className="flex items-end gap-2">
              <textarea
                value={inputValue}
                onChange={e => setInputValue(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder={selectedGameId ? 'Ask a question...' : 'Select a game first'}
                disabled={!selectedGameId || state.isStreaming}
                className={cn(
                  'flex-1 resize-none rounded-md border bg-background px-3 py-2 text-sm',
                  'placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring',
                  'disabled:cursor-not-allowed disabled:opacity-50',
                  'min-h-[40px] max-h-[120px]'
                )}
                rows={1}
              />
              {state.isStreaming ? (
                <button
                  onClick={stopStreaming}
                  className="shrink-0 rounded-md bg-destructive px-3 py-2 text-sm font-medium text-destructive-foreground hover:bg-destructive/90"
                  type="button"
                >
                  Stop
                </button>
              ) : (
                <button
                  onClick={handleSend}
                  disabled={!inputValue.trim() || !selectedGameId}
                  className={cn(
                    'shrink-0 rounded-md bg-primary p-2 text-primary-foreground hover:bg-primary/90',
                    'disabled:opacity-50 disabled:cursor-not-allowed'
                  )}
                  type="button"
                >
                  <SendIcon className="h-4 w-4" />
                </button>
              )}
            </div>
          </div>

          {/* Error display */}
          {state.error && (
            <div className="border-t border-destructive/20 bg-destructive/5 px-4 py-2 text-xs text-destructive">
              {state.error}
            </div>
          )}
        </div>

        {/* ── Right Panel: Debug Timeline ─────────────────────────────── */}
        {showDebug && (
          <>
            {/* Mobile: button to open Sheet */}
            <div className="lg:hidden border-t px-4 py-2 text-xs text-muted-foreground bg-muted/30 flex items-center justify-between">
              <span>
                Pipeline debug
                {state.debugEvents.length > 0 && ` · ${state.debugEvents.length} eventi`}
              </span>
              <button
                onClick={() => setMobileDebugOpen(true)}
                className="text-xs font-medium text-primary underline underline-offset-2"
                type="button"
              >
                Apri
              </button>
            </div>

            {/* Desktop: inline panel */}
            <div className="min-h-0 hidden lg:flex lg:flex-col">
              <DebugTimeline events={state.debugEvents} isStreaming={state.isStreaming} />
            </div>
          </>
        )}
      </div>

      {/* Mobile debug Sheet */}
      <Sheet open={mobileDebugOpen} onOpenChange={setMobileDebugOpen}>
        <SheetContent side="right" className="w-full sm:max-w-lg p-0 flex flex-col">
          <SheetHeader className="px-4 py-3 border-b">
            <SheetTitle className="text-sm">Pipeline Debug</SheetTitle>
          </SheetHeader>
          <div className="flex-1 min-h-0 overflow-hidden">
            <DebugTimeline events={state.debugEvents} isStreaming={state.isStreaming} />
          </div>
        </SheetContent>
      </Sheet>
    </div>
  );
}
