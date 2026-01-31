/**
 * AgentChatSidebar - Session-based Agent Chat Component (Issue #3187)
 *
 * SSE streaming chat sidebar for real-time agent interaction.
 * Features:
 * - SSE streaming with progressive text display
 * - Auto-scroll to latest message
 * - Message types: User, Agent, System
 * - Minimize/maximize toggle
 * - Copy/export conversation
 * - Responsive: Desktop sidebar, Mobile bottom sheet
 */

import React, { useRef, useEffect, useState } from 'react';

import { MessageSquare, Minimize2, Maximize2, X, Download, Copy } from 'lucide-react';

import { useMediaQuery } from '@/lib/hooks/useMediaQuery';
import { cn } from '@/lib/utils';
import { useAgentChatStore } from '@/store/agent/store';

import { AgentMessage as AgentMessageComponent } from './AgentMessage';
import { AgentTypingIndicator } from './AgentTypingIndicator';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '../ui/navigation/sheet';

interface AgentChatSidebarProps {
  sessionId: string;
  typologyName: string;
  isOpen?: boolean;
  onClose?: () => void;
}

export function AgentChatSidebar({
  sessionId,
  typologyName,
  isOpen = true,
  onClose,
}: AgentChatSidebarProps) {
  const isMobile = useMediaQuery('(max-width: 768px)');
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const messagesContainerRef = useRef<HTMLDivElement>(null);
  const [shouldAutoScroll, setShouldAutoScroll] = useState(true);

  // Zustand store
  const { messages, isMinimized, isStreaming, setMinimized } = useAgentChatStore(state => ({
    messages: state.messagesBySession[sessionId] ?? [],
    isMinimized: state.isMinimized,
    isStreaming: state.isStreaming,
    setMinimized: state.setMinimized,
  }));

  // Auto-scroll to latest message during streaming
  useEffect(() => {
    if (shouldAutoScroll && isStreaming) {
      messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [messages, isStreaming, shouldAutoScroll]);

  // Detect manual scroll to disable auto-scroll
  const handleScroll = () => {
    if (!messagesContainerRef.current) return;

    const { scrollTop, scrollHeight, clientHeight } = messagesContainerRef.current;
    const isAtBottom = scrollHeight - scrollTop - clientHeight < 50;

    setShouldAutoScroll(isAtBottom);
  };

  // Copy conversation to clipboard
  const handleCopyConversation = async () => {
    const text = messages
      .map(m => {
        const role = m.type === 'user' ? 'User' : 'Agent';
        return `${role}: ${m.content}`;
      })
      .join('\n\n');

    try {
      await navigator.clipboard.writeText(text);
      // TODO: Add toast notification
    } catch (error) {
      console.error('Failed to copy conversation:', error);
    }
  };

  // Export conversation as JSON
  const handleExportConversation = () => {
    const data = {
      sessionId,
      typologyName,
      messages,
      exportedAt: new Date().toISOString(),
    };

    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `agent-conversation-${sessionId}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  // Sidebar header
  const renderHeader = () => (
    <div className="flex items-center justify-between p-4 border-b border-[#dadce0]">
      <div className="flex items-center gap-2">
        <MessageSquare className="h-5 w-5 text-[#1a73e8]" />
        <span className="font-medium text-sm">{typologyName}</span>
      </div>
      <div className="flex items-center gap-2">
        <button
          onClick={() => setMinimized(!isMinimized)}
          className="p-1.5 rounded hover:bg-[#f1f3f4] transition-colors"
          aria-label={isMinimized ? 'Maximize sidebar' : 'Minimize sidebar'}
        >
          {isMinimized ? <Maximize2 className="h-4 w-4" /> : <Minimize2 className="h-4 w-4" />}
        </button>
        {onClose && (
          <button
            onClick={onClose}
            className="p-1.5 rounded hover:bg-[#f1f3f4] transition-colors"
            aria-label="Close sidebar"
          >
            <X className="h-4 w-4" />
          </button>
        )}
      </div>
    </div>
  );

  // Messages area
  const renderMessages = () => (
    <div
      ref={messagesContainerRef}
      className="flex-1 overflow-y-auto p-4 space-y-4"
      onScroll={handleScroll}
    >
      {messages.map((message, index) => (
        <AgentMessageComponent key={index} message={message} />
      ))}
      {isStreaming && <AgentTypingIndicator />}
      <div ref={messagesEndRef} />
    </div>
  );

  // Footer with actions
  const renderFooter = () => (
    <div className="border-t border-[#dadce0] p-4 flex items-center justify-between">
      <div className="flex gap-2">
        <button
          onClick={handleCopyConversation}
          disabled={messages.length === 0}
          className="p-2 rounded hover:bg-[#f1f3f4] transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          aria-label="Copy conversation"
          title="Copy conversation"
        >
          <Copy className="h-4 w-4" />
        </button>
        <button
          onClick={handleExportConversation}
          disabled={messages.length === 0}
          className="p-2 rounded hover:bg-[#f1f3f4] transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          aria-label="Export conversation"
          title="Export conversation"
        >
          <Download className="h-4 w-4" />
        </button>
      </div>
      <div className="text-xs text-[#5f6368]">
        {messages.length} message{messages.length !== 1 ? 's' : ''}
      </div>
    </div>
  );

  // Desktop: Fixed sidebar
  if (!isMobile) {
    return (
      <div
        className={cn(
          'fixed right-0 top-0 h-full bg-white border-l border-[#dadce0] shadow-lg transition-all duration-300 z-40',
          isMinimized ? 'w-16' : 'w-96',
          !isOpen && 'translate-x-full'
        )}
      >
        {renderHeader()}
        {!isMinimized && (
          <>
            {renderMessages()}
            {renderFooter()}
          </>
        )}
      </div>
    );
  }

  // Mobile: Bottom sheet
  return (
    <Sheet open={isOpen} onOpenChange={open => !open && onClose?.()}>
      <SheetContent side="bottom" className="h-[80vh]">
        <SheetHeader>
          <SheetTitle className="flex items-center gap-2">
            <MessageSquare className="h-5 w-5 text-[#1a73e8]" />
            {typologyName}
          </SheetTitle>
        </SheetHeader>
        <div className="flex flex-col h-full mt-4">
          {renderMessages()}
          {renderFooter()}
        </div>
      </SheetContent>
    </Sheet>
  );
}
