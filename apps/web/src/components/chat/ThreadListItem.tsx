/**
 * ThreadListItem - Individual thread item in sidebar (Issue #858)
 *
 * Displays:
 * - Thread title (auto-generated from first message or custom)
 * - Last message timestamp
 * - Active/selected state
 * - Message count indicator
 * - Status badge (Active/Archived)
 * - Delete action
 */

import React from 'react';

import { cn } from '@/lib/utils';
import { ChatThread } from '@/types';

interface ThreadListItemProps {
  thread: ChatThread;
  isActive: boolean;
  onSelect: () => void;
  onDelete: () => void;
}

export function ThreadListItem({ thread, isActive, onSelect, onDelete }: ThreadListItemProps) {
  // Format last message timestamp
  const formatTimestamp = (timestamp: string | null): string => {
    if (!timestamp) return 'No messages';

    const date = new Date(timestamp);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;

    return date.toLocaleDateString();
  };

  // Thread title: use custom title or generate from content
  const displayTitle = thread.title || 'New Chat';
  const isArchived = thread.status === 'Closed';
  const lastMessageTime = formatTimestamp(thread.lastMessageAt);

  const handleDelete = (e: React.MouseEvent) => {
    e.stopPropagation(); // Prevent triggering onSelect
    onDelete();
  };

  return (
    <li className="list-none">
      <div
        className={cn(
          'w-full px-3 py-2.5 mb-1 rounded transition-colors flex flex-col gap-1 relative group',
          isActive
            ? 'bg-[#e8f0fe] text-[#1a73e8]'
            : 'bg-transparent text-[#202124] hover:bg-[#f1f3f4]'
        )}
        aria-current={isActive ? 'true' : undefined}
      >
        {/* Main clickable area (thread selection) */}
        <button
          onClick={onSelect}
          className="absolute inset-0 w-full h-full border-none bg-transparent cursor-pointer"
          style={{ zIndex: 1 }}
          aria-label={`Select thread: ${displayTitle}, ${thread.messageCount} messages, ${lastMessageTime}`}
        >
          <span className="sr-only">
            {displayTitle}, {thread.messageCount} messages, {lastMessageTime}
          </span>
        </button>

        {/* Thread Title Row */}
        <div className="flex items-center justify-between gap-2 relative" style={{ zIndex: 2 }}>
          <span
            className={cn(
              'flex-1 text-sm font-medium truncate pointer-events-none',
              isArchived && 'opacity-60'
            )}
            title={displayTitle}
          >
            {displayTitle}
          </span>

          {/* Status Badge (only show if archived) */}
          {isArchived && (
            <span
              className="text-[10px] px-1.5 py-0.5 bg-[#dadce0] text-[#5f6368] rounded-sm uppercase font-semibold pointer-events-none"
              title="Archived thread"
              aria-label="Archived"
              data-testid="archived-badge"
            >
              Archived
            </span>
          )}

          {/* Delete Button */}
          <button
            onClick={handleDelete}
            className="p-1 text-[#5f6368] hover:text-[#d93025] hover:bg-[#fce8e6] rounded transition-colors relative"
            style={{ zIndex: 3 }}
            aria-label={`Delete ${displayTitle}`}
            title="Delete thread"
          >
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M3 6h18M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />
            </svg>
          </button>
        </div>

        {/* Thread Metadata Row */}
        <div className="flex items-center gap-2 text-[11px] text-[#5f6368] pointer-events-none">
          {/* Message Count */}
          <span className="flex items-center gap-1" aria-label={`${thread.messageCount} messages`}>
            <svg
              width="12"
              height="12"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z" />
            </svg>
            {thread.messageCount}
          </span>

          {/* Separator */}
          <span>•</span>

          {/* Last Message Time */}
          <span>{lastMessageTime}</span>
        </div>
      </div>
    </li>
  );
}
