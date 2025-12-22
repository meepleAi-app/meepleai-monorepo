/**
 * ChatHistoryItem - Individual chat item in the history list
 *
 * Displays a chat session with agent name, timestamp, and delete button.
 * Highlights active chat and supports keyboard navigation.
 */

import React from 'react';

import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { ChatThread } from '@/types';

interface ChatHistoryItemProps {
  chat: ChatThread;
  isActive: boolean;
  onSelect: () => void;
  onDelete: () => void;
}

function formatChatPreview(chat: ChatThread): string {
  const date = new Date(chat.lastMessageAt ?? chat.createdAt);
  return `${chat.title ?? 'Chat'} - ${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
}

export function ChatHistoryItem({ chat, isActive, onSelect, onDelete }: ChatHistoryItemProps) {
  return (
    <li
      className={cn(
        'p-3 mb-2 rounded cursor-pointer relative text-sm border',
        isActive ? 'bg-primary/10 border-primary' : 'bg-background border-border'
      )}
      onClick={onSelect}
      role="button"
      tabIndex={0}
      onKeyDown={e => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onSelect();
        }
      }}
      aria-current={isActive ? 'true' : undefined}
    >
      <div className="font-medium mb-1">{chat.title ?? 'Chat'}</div>
      <div className="text-xs text-slate-500">{formatChatPreview(chat)}</div>
      <Button
        variant="destructive"
        size="sm"
        className="absolute top-2 right-2 h-7 px-2 text-xs"
        onClick={e => {
          e.stopPropagation();
          onDelete();
        }}
        aria-label={`Delete chat ${chat.title ?? ''}`}
        title="Elimina chat"
      >
        🗑️
      </Button>
    </li>
  );
}
