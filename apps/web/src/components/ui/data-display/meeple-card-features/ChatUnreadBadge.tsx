/**
 * ChatUnreadBadge - Unread Message Count Badge
 * Issue #4400 - ChatSession-Specific Metadata & Status Display
 *
 * Displays a blue circle badge with unread message count.
 * Positioned absolute top-right on the card. Renders only when count > 0.
 */

'use client';

import React from 'react';

import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface ChatUnreadBadgeProps {
  /** Unread message count */
  count: number;
  /** Custom className */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export const ChatUnreadBadge = React.memo(function ChatUnreadBadge({
  count,
  className,
}: ChatUnreadBadgeProps) {
  if (count <= 0) return null;

  const displayCount = count >= 100 ? '99+' : count.toString();

  return (
    <span
      className={cn(
        'absolute top-3 right-4 z-[16]',
        'inline-flex items-center justify-center',
        'min-w-[20px] h-5 px-1.5 rounded-full',
        'bg-blue-500 text-white',
        'text-[10px] font-bold leading-none',
        'shadow-sm',
        className
      )}
      data-testid="chat-unread-badge"
      aria-label={`${count} unread messages`}
    >
      {displayCount}
    </span>
  );
});

ChatUnreadBadge.displayName = 'ChatUnreadBadge';
