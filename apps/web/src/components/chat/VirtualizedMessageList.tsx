/**
 * VirtualizedMessageList - Virtualized chat message list with ChatMessage
 *
 * Uses react-window for efficient rendering of large message lists.
 * Integrates with ChatMessage component from Issue #1831.
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md (Page 4 - Chat AI)
 * @issue #1840 (PAGE-004)
 * @dependency react-window ^2.2.3
 */

import React, { useRef, useEffect } from 'react';

// @ts-ignore - react-window types not available in v2.x
import AutoSizer from 'react-virtualized-auto-sizer';
import { List as FixedSizeList } from 'react-window';

// @ts-ignore - auto-sizer types
import { ChatMessage, type Citation } from '@/components/ui/chat-message';
import { Message as MessageType } from '@/types';

// Type for list children (from react-window)
interface _ListChildComponentProps {
  index: number;
  style: React.CSSProperties;
}

// ============================================================================
// Types
// ============================================================================

export interface VirtualizedMessageListProps {
  /** Array of messages to display */
  messages: MessageType[];
  /** Streaming message (displayed separately at bottom) */
  streamingMessage?: {
    content: string;
    stateMessage?: string;
  };
  /** Is streaming active */
  isStreaming?: boolean;
  /** Citation click handler */
  onCitationClick?: (documentId: string, pageNumber: number) => void;
  /** User avatar config */
  userAvatar?: {
    src?: string;
    fallback: string;
  };
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

/** Fixed height for messages (px) - FixedSizeList requirement */
const FIXED_MESSAGE_HEIGHT = 150;

/** Minimum threshold for virtualization (disable for small lists) */
const VIRTUALIZATION_THRESHOLD = 50;

// ============================================================================
// Component
// ============================================================================

/**
 * VirtualizedMessageList - Efficient message rendering with react-window
 *
 * Features:
 * - Virtual scrolling for >50 messages
 * - Auto-scroll to bottom on new messages
 * - ChatMessage component integration
 * - Streaming message support
 * - Variable height handling
 *
 * @example
 * ```tsx
 * <VirtualizedMessageList
 *   messages={messages}
 *   streamingMessage={{ content: 'Thinking...' }}
 *   isStreaming={true}
 *   onCitationClick={(id) => console.log('Citation:', id)}
 * />
 * ```
 */
// Type for react-window v2 List ref
interface ListRef {
  readonly element: HTMLDivElement | null;
  scrollToRow(config: {
    align?: 'auto' | 'smart' | 'center' | 'end' | 'start';
    behavior?: 'auto' | 'smooth' | 'instant';
    index: number;
  }): void;
}

export function VirtualizedMessageList({
  messages,
  streamingMessage,
  isStreaming,
  onCitationClick,
  userAvatar = { fallback: 'U' },
  className,
}: VirtualizedMessageListProps) {
  const listRef = useRef<ListRef | null>(null);

  // Disable virtualization for small lists
  const shouldVirtualize = messages.length >= VIRTUALIZATION_THRESHOLD;

  // Scroll to bottom when new messages arrive
  useEffect(() => {
    if (listRef.current && shouldVirtualize) {
      listRef.current.scrollToRow({ index: messages.length - 1, align: 'end' });
    }
  }, [messages.length, shouldVirtualize]);

  // Message row renderer for react-window v2 List
  // v2 API: rowComponent receives { index, style, ariaAttributes }
  const RowComponent = React.useMemo(
    () =>
      ({
        index,
        style,
      }: {
        index: number;
        style: React.CSSProperties;
        ariaAttributes?: {
          'aria-posinset': number;
          'aria-setsize': number;
          role: string;
        };
      }) => {
        // eslint-disable-next-line security/detect-object-injection
        const message = messages[index];
        if (!message) return <div style={style} />;

        return (
          <div style={style}>
            <MessageRow
              message={message}
              onCitationClick={onCitationClick}
              userAvatar={userAvatar}
            />
          </div>
        );
      },
    [messages, onCitationClick, userAvatar]
  );

  // Non-virtualized fallback for small lists
  if (!shouldVirtualize) {
    return (
      <div className={className} role="log" aria-live="polite" aria-atomic="false">
        {messages.map(message => (
          <MessageRow
            key={message.id}
            message={message}
            onCitationClick={onCitationClick}
            userAvatar={userAvatar}
          />
        ))}

        {/* Streaming message */}
        {isStreaming && streamingMessage && (
          <StreamingMessageRow
            content={streamingMessage.content}
            stateMessage={streamingMessage.stateMessage}
          />
        )}
      </div>
    );
  }

  // Virtualized list
  return (
    <div className={className} style={{ height: '100%', width: '100%' }}>
      <AutoSizer>
        {({ height, width: _width }: { height: number; width: number }) => (
          <FixedSizeList
            listRef={listRef}
            defaultHeight={height}
            rowComponent={RowComponent}
            rowCount={messages.length}
            rowHeight={FIXED_MESSAGE_HEIGHT}
            // @ts-expect-error - react-window v2 rowProps type incompatibility
            rowProps={{}}
            overscanCount={5}
          />
        )}
      </AutoSizer>

      {/* Streaming message (outside virtualization for smooth append) */}
      {isStreaming && streamingMessage && (
        <StreamingMessageRow
          content={streamingMessage.content}
          stateMessage={streamingMessage.stateMessage}
        />
      )}
    </div>
  );
}

// ============================================================================
// Sub-Components
// ============================================================================

interface MessageRowProps {
  message: MessageType;
  onCitationClick?: (documentId: string, pageNumber: number) => void;
  userAvatar?: {
    src?: string;
    fallback: string;
  };
}

/**
 * MessageRow - Individual message wrapper (memoized for performance)
 */
const MessageRow = React.memo(function MessageRow({
  message,
  onCitationClick,
  userAvatar,
}: MessageRowProps) {
  // Convert MessageType citations to ChatMessage Citation format
  const citations: Citation[] | undefined = message.citations?.map(c => ({
    id: c.documentId,
    label: c.documentId,
    page: c.pageNumber,
    source: c.snippet,
  }));

  return (
    <div className="px-2 py-2">
      <ChatMessage
        role={message.role}
        content={message.content}
        citations={citations}
        timestamp={message.timestamp}
        avatar={message.role === 'user' ? userAvatar : undefined}
        onCitationClick={onCitationClick}
      />
    </div>
  );
});

interface StreamingMessageRowProps {
  content: string;
  stateMessage?: string;
}

/**
 * StreamingMessageRow - Streaming/typing indicator message
 */
function StreamingMessageRow({ content, stateMessage }: StreamingMessageRowProps) {
  // Show state message if no content yet
  if (!content && stateMessage) {
    return (
      <div className="mb-4 flex justify-start">
        <div className="max-w-[70%] px-4 py-2 rounded-lg bg-gray-100 text-gray-600 text-sm italic">
          {stateMessage}
        </div>
      </div>
    );
  }

  // Show streaming message with typing indicator
  return (
    <div className="mb-4">
      <ChatMessage
        role="assistant"
        content={content}
        isTyping={!content} // Show typing if no content
      />
    </div>
  );
}
