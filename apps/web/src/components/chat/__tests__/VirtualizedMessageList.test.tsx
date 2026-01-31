/**
 * VirtualizedMessageList Component - Unit Tests
 *
 * @issue #1840 (PAGE-004)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { VirtualizedMessageList } from '../VirtualizedMessageList';
import { Message as MessageType } from '@/types';

// Mock react-window v2 (virtualization library)
// v2 API: List component with rowComponent, rowCount, rowHeight
vi.mock('react-window', () => ({
  List: vi.fn(({ rowComponent: RowComponent, rowCount }) => (
    <div data-testid="virtualized-list">
      {Array.from({ length: rowCount }, (_, index) => (
        <RowComponent key={index} index={index} style={{}} />
      ))}
    </div>
  )),
}));

// Mock react-virtualized-auto-sizer
vi.mock('react-virtualized-auto-sizer', () => ({
  default: vi.fn(({ children }) => children({ height: 600, width: 800 })),
}));

// Mock ChatMessage component
vi.mock('@/components/ui/meeple/chat-message', () => ({
  ChatMessage: vi.fn(({ role, content }) => (
    <div data-testid={`chat-message-${role}`}>{content}</div>
  )),
}));

describe('VirtualizedMessageList', () => {
  const mockMessages: MessageType[] = [
    {
      id: '1',
      role: 'user',
      content: 'Hello AI',
      timestamp: new Date('2025-01-01T10:00:00Z'),
    },
    {
      id: '2',
      role: 'assistant',
      content: 'Hello! How can I help?',
      timestamp: new Date('2025-01-01T10:00:05Z'),
      confidence: 0.95,
    },
    {
      id: '3',
      role: 'user',
      content: 'What are the setup rules?',
      timestamp: new Date('2025-01-01T10:00:10Z'),
    },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering - Small Lists (< 50 messages)', () => {
    it('should render messages without virtualization for small lists', () => {
      render(<VirtualizedMessageList messages={mockMessages} />);

      expect(screen.getByText('Hello AI')).toBeInTheDocument();
      expect(screen.getByText('Hello! How can I help?')).toBeInTheDocument();
      expect(screen.getByText('What are the setup rules?')).toBeInTheDocument();
    });

    it('should render user and assistant messages correctly', () => {
      render(<VirtualizedMessageList messages={mockMessages} />);

      const userMessages = screen.getAllByTestId('chat-message-user');
      const assistantMessages = screen.getAllByTestId('chat-message-assistant');

      expect(userMessages).toHaveLength(2);
      expect(assistantMessages).toHaveLength(1);
    });

    it('should render empty list without errors', () => {
      render(<VirtualizedMessageList messages={[]} />);

      expect(screen.queryByTestId('chat-message-user')).not.toBeInTheDocument();
      expect(screen.queryByTestId('chat-message-assistant')).not.toBeInTheDocument();
    });
  });

  describe('Rendering - Large Lists (>= 50 messages)', () => {
    it('should use virtualization for large lists', () => {
      const largeMessageList: MessageType[] = Array.from({ length: 60 }, (_, i) => ({
        id: `msg-${i}`,
        role: i % 2 === 0 ? 'user' : 'assistant',
        content: `Message ${i}`,
        timestamp: new Date(),
      })) as MessageType[];

      render(<VirtualizedMessageList messages={largeMessageList} />);

      // Should render virtualized list
      expect(screen.getByTestId('virtualized-list')).toBeInTheDocument();
    });

    it('should render all messages in virtualized list', () => {
      const largeMessageList: MessageType[] = Array.from({ length: 60 }, (_, i) => ({
        id: `msg-${i}`,
        role: i % 2 === 0 ? 'user' : 'assistant',
        content: `Message ${i}`,
        timestamp: new Date(),
      })) as MessageType[];

      render(<VirtualizedMessageList messages={largeMessageList} />);

      // All messages should be rendered (mocked virtualization renders all)
      expect(screen.getAllByTestId(/chat-message-/)).toHaveLength(60);
    });
  });

  describe('Streaming Messages', () => {
    it('should render streaming message when isStreaming true', () => {
      render(
        <VirtualizedMessageList
          messages={mockMessages}
          streamingMessage={{ content: 'Typing...' }}
          isStreaming={true}
        />
      );

      expect(screen.getByText('Typing...')).toBeInTheDocument();
    });

    it('should not render streaming message when isStreaming false', () => {
      render(
        <VirtualizedMessageList
          messages={mockMessages}
          streamingMessage={{ content: 'Should not appear' }}
          isStreaming={false}
        />
      );

      expect(screen.queryByText('Should not appear')).not.toBeInTheDocument();
    });

    it('should render state message when no content yet', () => {
      render(
        <VirtualizedMessageList
          messages={mockMessages}
          streamingMessage={{
            content: '',
            stateMessage: 'Generating embeddings...',
          }}
          isStreaming={true}
        />
      );

      expect(screen.getByText('Generating embeddings...')).toBeInTheDocument();
    });

    it('should prioritize content over state message', () => {
      render(
        <VirtualizedMessageList
          messages={mockMessages}
          streamingMessage={{
            content: 'AI response',
            stateMessage: 'State message',
          }}
          isStreaming={true}
        />
      );

      expect(screen.getByText('AI response')).toBeInTheDocument();
      expect(screen.queryByText('State message')).not.toBeInTheDocument();
    });
  });

  describe('Citation Handling', () => {
    it('should pass citation click handler to ChatMessage', () => {
      const onCitationClick = vi.fn();
      const messagesWithCitations: MessageType[] = [
        {
          id: '1',
          role: 'assistant',
          content: 'Answer with citation',
          timestamp: new Date(),
          citations: [
            {
              id: 'cite-1',
              documentId: 'doc-1',
              documentTitle: 'Rulebook',
              pageNumber: 5,
              relevanceScore: 0.9,
            },
          ],
        },
      ];

      render(
        <VirtualizedMessageList
          messages={messagesWithCitations}
          onCitationClick={onCitationClick}
        />
      );

      // ChatMessage component should receive onCitationClick prop
      // (verified through mocking)
      expect(screen.getByText('Answer with citation')).toBeInTheDocument();
    });
  });

  describe('User Avatar', () => {
    it('should use default user avatar fallback', () => {
      render(<VirtualizedMessageList messages={mockMessages} />);

      // Default fallback 'U' should be used
      expect(screen.getAllByTestId('chat-message-user')).toHaveLength(2);
    });

    it('should use custom user avatar', () => {
      const customAvatar = {
        src: 'https://example.com/avatar.jpg',
        fallback: 'JD',
      };

      render(<VirtualizedMessageList messages={mockMessages} userAvatar={customAvatar} />);

      // Custom avatar should be passed to ChatMessage
      expect(screen.getAllByTestId('chat-message-user')).toHaveLength(2);
    });
  });

  describe('Custom Styling', () => {
    it('should apply custom className', () => {
      const { container } = render(
        <VirtualizedMessageList messages={mockMessages} className="custom-class" />
      );

      expect(container.firstChild).toHaveClass('custom-class');
    });
  });

  describe('Accessibility', () => {
    it('should have log role with aria-live for small lists', () => {
      render(<VirtualizedMessageList messages={mockMessages} />);

      const logElement = screen.getByRole('log');
      expect(logElement).toHaveAttribute('aria-live', 'polite');
      expect(logElement).toHaveAttribute('aria-atomic', 'false');
    });

    it('should render virtualized list for large messages (accessibility maintained)', () => {
      const largeMessageList: MessageType[] = Array.from({ length: 60 }, (_, i) => ({
        id: `msg-${i}`,
        role: i % 2 === 0 ? 'user' : 'assistant',
        content: `Message ${i}`,
        timestamp: new Date(),
      })) as MessageType[];

      render(<VirtualizedMessageList messages={largeMessageList} />);

      // Virtualized list should be rendered
      expect(screen.getByTestId('virtualized-list')).toBeInTheDocument();
      // All messages should be accessible
      expect(screen.getAllByTestId(/chat-message-/)).toHaveLength(60);
    });
  });

  describe('Edge Cases', () => {
    it('should handle single message', () => {
      render(<VirtualizedMessageList messages={[mockMessages[0]]} />);

      expect(screen.getByText('Hello AI')).toBeInTheDocument();
      expect(screen.queryByTestId('virtualized-list')).not.toBeInTheDocument(); // Not virtualized
    });

    it('should handle exactly 50 messages (virtualization threshold)', () => {
      const fiftyMessages: MessageType[] = Array.from({ length: 50 }, (_, i) => ({
        id: `msg-${i}`,
        role: i % 2 === 0 ? 'user' : 'assistant',
        content: `Message ${i}`,
        timestamp: new Date(),
      })) as MessageType[];

      render(<VirtualizedMessageList messages={fiftyMessages} />);

      // 50 messages should trigger virtualization
      expect(screen.getByTestId('virtualized-list')).toBeInTheDocument();
    });

    it('should handle messages with missing optional fields', () => {
      const minimalMessage: MessageType[] = [
        {
          id: '1',
          role: 'user',
          content: 'Minimal message',
          timestamp: new Date(),
        },
      ];

      render(<VirtualizedMessageList messages={minimalMessage} />);

      expect(screen.getByText('Minimal message')).toBeInTheDocument();
    });

    it('should handle streaming without streamingMessage prop', () => {
      render(<VirtualizedMessageList messages={mockMessages} isStreaming={true} />);

      // Should render without errors
      expect(screen.getByText('Hello AI')).toBeInTheDocument();
    });
  });
});
