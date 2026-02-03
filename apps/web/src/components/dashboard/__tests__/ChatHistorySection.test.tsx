/**
 * ChatHistorySection Unit Tests (Issue #3312)
 *
 * Coverage areas:
 * - Rendering with threads
 * - Empty state
 * - Loading state
 * - Thread details (title, timestamp)
 * - Navigation links
 * - "View All" visibility logic
 *
 * Target: 90%+ coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ChatHistorySection, type ChatThread } from '../ChatHistorySection';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
      <div {...props}>{children}</div>
    ),
  },
}));

// ============================================================================
// Test Data
// ============================================================================

const mockThreads: ChatThread[] = [
  {
    id: 'thread-1',
    title: 'Regole Wingspan - Setup iniziale',
    lastMessageAt: new Date('2026-01-20T14:30:00Z').toISOString(),
    messageCount: 12,
  },
  {
    id: 'thread-2',
    title: 'Strategie Catan - Espansione Marinai',
    lastMessageAt: new Date('2026-01-19T20:00:00Z').toISOString(),
    messageCount: 8,
  },
];

const fourThreads: ChatThread[] = [
  ...mockThreads,
  {
    id: 'thread-3',
    title: 'FAQ Ticket to Ride',
    lastMessageAt: new Date('2026-01-18T19:30:00Z').toISOString(),
    messageCount: 5,
  },
  {
    id: 'thread-4',
    title: 'Setup Azul',
    lastMessageAt: new Date('2026-01-17T21:00:00Z').toISOString(),
    messageCount: 3,
  },
];

const fiveThreads: ChatThread[] = [
  ...fourThreads,
  {
    id: 'thread-5',
    title: 'Another Chat',
    lastMessageAt: new Date('2026-01-16T10:00:00Z').toISOString(),
    messageCount: 2,
  },
];

describe('ChatHistorySection', () => {
  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders widget container', () => {
      render(<ChatHistorySection threads={mockThreads} />);

      expect(screen.getByTestId('chat-history-widget')).toBeInTheDocument();
    });

    it('renders widget title', () => {
      render(<ChatHistorySection threads={mockThreads} />);

      expect(screen.getByTestId('chat-history-title')).toHaveTextContent('Conversazioni AI Recenti');
    });

    it('renders max 4 thread items', () => {
      render(<ChatHistorySection threads={fiveThreads} />);

      expect(screen.getByTestId('chat-thread-thread-1')).toBeInTheDocument();
      expect(screen.getByTestId('chat-thread-thread-2')).toBeInTheDocument();
      expect(screen.getByTestId('chat-thread-thread-3')).toBeInTheDocument();
      expect(screen.getByTestId('chat-thread-thread-4')).toBeInTheDocument();
      expect(screen.queryByTestId('chat-thread-thread-5')).not.toBeInTheDocument();
    });

    it('renders new chat button', () => {
      render(<ChatHistorySection threads={mockThreads} />);

      const newChatBtn = screen.getByTestId('new-chat-button');
      expect(newChatBtn).toBeInTheDocument();
      expect(newChatBtn.closest('a')).toHaveAttribute('href', '/chat');
    });

    it('applies custom className', () => {
      const { container } = render(
        <ChatHistorySection threads={mockThreads} className="custom-widget" />
      );

      expect(container.querySelector('.custom-widget')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Thread Item Tests
  // ============================================================================

  describe('Thread Item', () => {
    it('displays thread title', () => {
      render(<ChatHistorySection threads={mockThreads} />);

      expect(screen.getByTestId('chat-title-thread-1')).toHaveTextContent('Regole Wingspan - Setup iniziale');
      expect(screen.getByTestId('chat-title-thread-2')).toHaveTextContent('Strategie Catan - Espansione Marinai');
    });

    it('displays timestamp', () => {
      render(<ChatHistorySection threads={mockThreads} />);

      // Timestamps should be rendered
      expect(screen.getByTestId('chat-time-thread-1')).toBeInTheDocument();
      expect(screen.getByTestId('chat-time-thread-2')).toBeInTheDocument();
    });

    it('thread links to chat page', () => {
      render(<ChatHistorySection threads={mockThreads} />);

      const threadLink = screen.getByTestId('chat-thread-thread-1');
      expect(threadLink).toHaveAttribute('href', '/chat/thread-1');
    });
  });

  // ============================================================================
  // Navigation Tests
  // ============================================================================

  describe('Navigation', () => {
    it('new chat button links to /chat', () => {
      render(<ChatHistorySection threads={mockThreads} />);

      const newChatBtn = screen.getByTestId('new-chat-button');
      expect(newChatBtn.closest('a')).toHaveAttribute('href', '/chat');
    });

    it('view all link appears when more than 4 threads', () => {
      render(<ChatHistorySection threads={fiveThreads} totalCount={5} />);

      const viewAll = screen.getByTestId('view-all-chats');
      expect(viewAll).toBeInTheDocument();
      expect(viewAll).toHaveAttribute('href', '/chat/history');
    });

    it('view all link hidden when 4 or fewer threads', () => {
      render(<ChatHistorySection threads={fourThreads} totalCount={4} />);

      expect(screen.queryByTestId('view-all-chats')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Empty State Tests
  // ============================================================================

  describe('Empty State', () => {
    it('renders empty state when no threads', () => {
      render(<ChatHistorySection threads={[]} />);

      expect(screen.getByTestId('chat-history-empty')).toBeInTheDocument();
    });

    it('shows empty message', () => {
      render(<ChatHistorySection threads={[]} />);

      expect(screen.getByText('Nessuna conversazione')).toBeInTheDocument();
    });

    it('shows start chat CTA', () => {
      render(<ChatHistorySection threads={[]} />);

      const cta = screen.getByTestId('start-chat-cta');
      expect(cta).toBeInTheDocument();
      expect(cta.closest('a')).toHaveAttribute('href', '/chat');
    });

    it('does not show thread items in empty state', () => {
      render(<ChatHistorySection threads={[]} />);

      expect(screen.queryByTestId('threads-list')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders skeleton when isLoading', () => {
      render(<ChatHistorySection isLoading />);

      expect(screen.getByTestId('chat-history-skeleton')).toBeInTheDocument();
    });

    it('does not render content when loading', () => {
      render(<ChatHistorySection threads={mockThreads} isLoading />);

      expect(screen.queryByTestId('chat-history-widget')).not.toBeInTheDocument();
      expect(screen.queryByTestId('chat-thread-thread-1')).not.toBeInTheDocument();
    });

    it('skeleton has glassmorphic styling', () => {
      render(<ChatHistorySection isLoading />);

      const skeleton = screen.getByTestId('chat-history-skeleton');
      expect(skeleton).toHaveClass('backdrop-blur-xl');
    });
  });

  // ============================================================================
  // Default Props Tests
  // ============================================================================

  describe('Default Props', () => {
    it('uses mock data when threads not provided', () => {
      render(<ChatHistorySection />);

      // Should render with default mock data
      expect(screen.getByTestId('chat-history-widget')).toBeInTheDocument();
      expect(screen.getByTestId('chat-thread-thread-1')).toBeInTheDocument();
    });

    it('isLoading defaults to false', () => {
      render(<ChatHistorySection threads={mockThreads} />);

      expect(screen.queryByTestId('chat-history-skeleton')).not.toBeInTheDocument();
    });

    it('totalCount defaults to threads length', () => {
      render(<ChatHistorySection threads={fourThreads} />);

      // 4 threads, no "View All" because totalCount defaults to 4
      expect(screen.queryByTestId('view-all-chats')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Styling Tests
  // ============================================================================

  describe('Styling', () => {
    it('widget has glassmorphic styling', () => {
      render(<ChatHistorySection threads={mockThreads} />);

      const widget = screen.getByTestId('chat-history-widget');
      expect(widget).toHaveClass('backdrop-blur-xl');
      expect(widget).toHaveClass('rounded-2xl');
    });

    it('header icon has blue styling', () => {
      const { container } = render(
        <ChatHistorySection threads={mockThreads} />
      );

      const iconContainer = container.querySelector('.bg-blue-500\\/20');
      expect(iconContainer).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Edge Cases
  // ============================================================================

  describe('Edge Cases', () => {
    it('handles single thread', () => {
      const singleThread = [mockThreads[0]];
      render(<ChatHistorySection threads={singleThread} />);

      expect(screen.getByTestId('chat-thread-thread-1')).toBeInTheDocument();
      expect(screen.queryByTestId('chat-thread-thread-2')).not.toBeInTheDocument();
    });

    it('truncates long titles in CSS', () => {
      const longTitleThread: ChatThread[] = [
        {
          id: 'thread-long',
          title: 'This is a very long title that should be truncated by CSS',
          lastMessageAt: new Date().toISOString(),
          messageCount: 1,
        },
      ];
      render(<ChatHistorySection threads={longTitleThread} />);

      const titleElement = screen.getByTestId('chat-title-thread-long');
      expect(titleElement).toHaveClass('truncate');
    });
  });
});
