/**
 * AgentChat - Base Chat UI Component (Issue #4085)
 *
 * Unified chat interface with 3 layout variants: modal, sidebar, full-page.
 * Integrates existing chat components with SSE streaming.
 *
 * Features:
 * - Layout variants: modal (600px), sidebar (400px), full-page (800px)
 * - Context header: Game + Agent name, Strategy badge
 * - Message list with auto-scroll
 * - Input with auto-resize, character limit, voice
 * - SSE streaming with typing indicator
 * - Markdown support in messages
 * - WCAG 2.1 AA compliant
 */

'use client';

import React, { useState, useCallback } from 'react';

import { X, Bot, Sparkles } from 'lucide-react';
import { toast } from 'sonner';

import { ChatMessageList, ChatInput } from '@/components/agent/chat';
import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { useAgentChat } from '@/hooks/queries/useAgentChat';
import { cn } from '@/lib/utils';
import type { AgentMessage } from '@/types/agent';

// ============================================================================
// Types
// ============================================================================

export type ChatLayout = 'modal' | 'sidebar' | 'full-page';

export interface AgentChatProps {
  /** Agent ID */
  agentId: string;
  /** Game ID (optional, for context) */
  gameId?: string;
  /** Layout variant */
  layout?: ChatLayout;
  /** Game name for header */
  gameName?: string;
  /** Agent name for header */
  agentName?: string;
  /** Strategy type for badge */
  strategy?: 'RetrievalOnly' | 'SingleModel' | 'MultiModelConsensus';
  /** Close callback (for modal/sidebar) */
  onClose?: () => void;
  /** Additional class name */
  className?: string;
}

// ============================================================================
// Layout Configuration
// ============================================================================

const LAYOUT_CONFIG: Record<ChatLayout, { width: string; height: string; containerClass: string }> = {
  modal: {
    width: 'max-w-[600px]',
    height: 'max-h-[80vh]',
    containerClass: 'fixed inset-0 z-50 flex items-center justify-center bg-black/50',
  },
  sidebar: {
    width: 'w-[400px]',
    height: 'h-screen',
    containerClass: 'fixed right-0 top-0 z-40 shadow-2xl',
  },
  'full-page': {
    width: 'max-w-[800px]',
    height: 'min-h-screen',
    containerClass: 'mx-auto',
  },
};

const STRATEGY_LABELS: Record<string, string> = {
  RetrievalOnly: 'Solo Retrieval',
  SingleModel: 'Modello Singolo',
  MultiModelConsensus: 'Consenso Multi-Modello',
};

const STRATEGY_COLORS: Record<string, string> = {
  RetrievalOnly: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400',
  SingleModel: 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-400',
  MultiModelConsensus: 'bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-400',
};

// ============================================================================
// Main Component
// ============================================================================

export function AgentChat({
  agentId,
  gameId: _gameId,
  layout = 'full-page',
  gameName,
  agentName = 'AI Assistant',
  strategy = 'RetrievalOnly',
  onClose,
  className,
}: AgentChatProps) {
  const [messages, setMessages] = useState<AgentMessage[]>([]);
  const [streamingContent, setStreamingContent] = useState('');
  const [error, setError] = useState<string | null>(null);

  // Use real agent chat hook (Issue #4126)
  const { isStreaming, sendMessage: sendChatMessage } = useAgentChat(agentId, {
    onToken: (token) => {
      setStreamingContent(prev => prev + token);
    },
    onComplete: () => {
      const agentMessage: AgentMessage = {
        type: 'agent',
        content: streamingContent,
        timestamp: new Date(),
        confidence: 0.85,
      };
      setMessages(prev => [...prev, agentMessage]);
      setStreamingContent('');
    },
    onError: (err) => {
      setError(err.message);
      toast.error('Failed to send message');
    },
  });

  const config = LAYOUT_CONFIG[layout];

  // Handle send message
  const handleSendMessage = useCallback(
    (content: string) => {
      // Add user message
      const userMessage: AgentMessage = {
        type: 'user',
        content,
        timestamp: new Date(),
      };
      setMessages(prev => [...prev, userMessage]);

      // Send via real API
      sendChatMessage(content);
    },
    [sendChatMessage]
  );

  // Handle retry on error
  const handleRetry = useCallback(() => {
    setError(null);
    toast.info('Retrying connection...');
    // Re-send last user message or reconnect SSE
  }, []);

  // Render header
  const renderHeader = () => (
    <div
      className="flex items-center justify-between p-4 border-b border-border/50 bg-background/95 backdrop-blur-sm"
      data-testid="agent-chat-header"
    >
      <div className="flex items-center gap-3 flex-1 min-w-0">
        <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10 shrink-0">
          <Bot className="h-5 w-5 text-primary" />
        </div>
        <div className="flex-1 min-w-0">
          <h2 className="font-semibold text-sm truncate">{agentName}</h2>
          {gameName && (
            <p className="text-xs text-muted-foreground truncate">{gameName}</p>
          )}
        </div>
        {strategy && STRATEGY_LABELS[strategy] && (
          <Badge className={STRATEGY_COLORS[strategy]} data-testid="strategy-badge">
            <Sparkles className="h-3 w-3 mr-1" />
            {STRATEGY_LABELS[strategy]}
          </Badge>
        )}
      </div>
      {onClose && (layout === 'modal' || layout === 'sidebar') && (
        <Button
          variant="ghost"
          size="icon"
          onClick={onClose}
          aria-label="Close chat"
          data-testid="close-chat-button"
        >
          <X className="h-4 w-4" />
        </Button>
      )}
    </div>
  );

  // Layout wrapper
  const LayoutWrapper = ({ children }: { children: React.ReactNode }) => {
    if (layout === 'full-page') {
      return (
        <div className={cn(config.containerClass, className)} data-testid="agent-chat-container">
          {children}
        </div>
      );
    }

    // Modal or Sidebar with overlay
    return (
      <div className={config.containerClass} data-testid="agent-chat-overlay">
        {children}
      </div>
    );
  };

  return (
    <LayoutWrapper>
      <div
        className={cn(
          'flex flex-col bg-background border border-border/50 rounded-lg shadow-lg',
          config.width,
          config.height,
          layout === 'full-page' && 'my-8',
          className
        )}
        role="region"
        aria-label={`Chat with ${agentName}`}
        data-testid="agent-chat"
        data-layout={layout}
      >
        {/* Header */}
        {renderHeader()}

        {/* Messages */}
        <div className="flex-1 overflow-hidden">
          <ChatMessageList
            messages={messages}
            isStreaming={isStreaming}
            currentChunk={streamingContent}
            className="h-full"
          />
        </div>

        {/* Input */}
        <div className="border-t border-border/50 p-4">
          <ChatInput
            isStreaming={isStreaming}
            onSendMessage={handleSendMessage}
            error={error}
            onRetry={handleRetry}
            placeholder={`Chiedi a ${agentName}...`}
          />
        </div>
      </div>
    </LayoutWrapper>
  );
}

export default AgentChat;
