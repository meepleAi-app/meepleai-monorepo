/**
 * ChatHistorySection Unit Tests (Issue #3312, #3484)
 *
 * Coverage areas:
 * - Rendering with threads (from props or backend)
 * - Empty state
 * - Loading state
 * - Error state
 * - Thread details (title, timestamp)
 * - Navigation links
 * - "View All" visibility logic
 * - Delete functionality
 *
 * Target: 90%+ coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ChatHistorySection, type ChatThread } from '../ChatHistorySection';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    prefetch: vi.fn(),
  }),
}));

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
      <div {...props}>{children}</div>
    ),
  },
}));

// Mock the hooks
const mockUseRecentChatSessions = vi.fn();
const mockUseDeleteChatSession = vi.fn();

vi.mock('@/hooks/queries', () => ({
  useRecentChatSessions: (userId: string | undefined, options?: { limit?: number; enabled?: boolean }) =>
    mockUseRecentChatSessions(userId, options),
  useDeleteChatSession: () => mockUseDeleteChatSession(),
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

// ============================================================================
// Test Helpers
// ============================================================================

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });
}

function renderWithQueryClient(ui: React.ReactElement) {
  const queryClient = createQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      {ui}
    </QueryClientProvider>
  );
}

describe('ChatHistorySection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Default mock implementations
    mockUseRecentChatSessions.mockReturnValue({
      data: null,
      isLoading: false,
      error: null,
    });
    mockUseDeleteChatSession.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
      variables: null,
    });
  });

  // ============================================================================
  // Rendering Tests (with props)
  // ============================================================================

  describe('Rendering with props', () => {
    it('renders widget container', () => {
      renderWithQueryClient(<ChatHistorySection threads={mockThreads} />);

      expect(screen.getByTestId('chat-history-widget')).toBeInTheDocument();
    });

    it('renders widget title', () => {
      renderWithQueryClient(<ChatHistorySection threads={mockThreads} />);

      expect(screen.getByTestId('chat-history-title')).toHaveTextContent('Conversazioni AI Recenti');
    });

    it('renders max 4 thread items', () => {
      renderWithQueryClient(<ChatHistorySection threads={fiveThreads} />);

      expect(screen.getByTestId('chat-thread-thread-1')).toBeInTheDocument();
      expect(screen.getByTestId('chat-thread-thread-2')).toBeInTheDocument();
      expect(screen.getByTestId('chat-thread-thread-3')).toBeInTheDocument();
      expect(screen.getByTestId('chat-thread-thread-4')).toBeInTheDocument();
      expect(screen.queryByTestId('chat-thread-thread-5')).not.toBeInTheDocument();
    });

    it('renders new chat button', () => {
      renderWithQueryClient(<ChatHistorySection threads={mockThreads} />);

      const newChatBtn = screen.getByTestId('new-chat-button');
      expect(newChatBtn).toBeInTheDocument();
      expect(newChatBtn.closest('a')).toHaveAttribute('href', '/chat');
    });

    it('applies custom className', () => {
      const { container } = renderWithQueryClient(
        <ChatHistorySection threads={mockThreads} className="custom-widget" />
      );

      expect(container.querySelector('.custom-widget')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Backend Integration Tests
  // ============================================================================

  describe('Backend Integration', () => {
    it('fetches sessions when userId is provided', () => {
      const mockData = {
        sessions: [
          {
            id: 'session-1',
            userId: 'user-123',
            gameId: 'game-456',
            title: 'Backend Session',
            messageCount: 5,
            createdAt: '2026-01-20T10:00:00Z',
            lastMessageAt: '2026-01-20T12:00:00Z',
            isArchived: false,
          },
        ],
        totalCount: 1,
      };

      mockUseRecentChatSessions.mockReturnValue({
        data: mockData,
        isLoading: false,
        error: null,
      });

      renderWithQueryClient(<ChatHistorySection userId="user-123" />);

      expect(mockUseRecentChatSessions).toHaveBeenCalledWith('user-123', {
        limit: 10,
        enabled: true,
      });
      expect(screen.getByTestId('chat-thread-session-1')).toBeInTheDocument();
      expect(screen.getByTestId('chat-title-session-1')).toHaveTextContent('Backend Session');
    });

    it('shows loading state from query', () => {
      mockUseRecentChatSessions.mockReturnValue({
        data: null,
        isLoading: true,
        error: null,
      });

      renderWithQueryClient(<ChatHistorySection userId="user-123" />);

      expect(screen.getByTestId('chat-history-skeleton')).toBeInTheDocument();
    });

    it('shows error state from query', () => {
      mockUseRecentChatSessions.mockReturnValue({
        data: null,
        isLoading: false,
        error: new Error('Failed to fetch'),
      });

      renderWithQueryClient(<ChatHistorySection userId="user-123" />);

      expect(screen.getByTestId('chat-history-error')).toBeInTheDocument();
      expect(screen.getByText('Impossibile caricare le conversazioni')).toBeInTheDocument();
    });

    it('prioritizes props over backend data', () => {
      mockUseRecentChatSessions.mockReturnValue({
        data: { sessions: [], totalCount: 0 },
        isLoading: false,
        error: null,
      });

      renderWithQueryClient(<ChatHistorySection threads={mockThreads} userId="user-123" />);

      // Should use props, not backend data
      expect(screen.getByTestId('chat-thread-thread-1')).toBeInTheDocument();
    });

    it('generates default title when session title is null', () => {
      const mockData = {
        sessions: [
          {
            id: 'session-no-title',
            userId: 'user-123',
            gameId: 'game-456',
            title: null,
            messageCount: 3,
            createdAt: '2026-01-20T10:00:00Z',
            lastMessageAt: '2026-01-20T12:00:00Z',
            isArchived: false,
          },
        ],
        totalCount: 1,
      };

      mockUseRecentChatSessions.mockReturnValue({
        data: mockData,
        isLoading: false,
        error: null,
      });

      renderWithQueryClient(<ChatHistorySection userId="user-123" />);

      // Should generate a title based on creation date
      expect(screen.getByTestId('chat-title-session-no-title')).toHaveTextContent(/Chat del/);
    });
  });

  // ============================================================================
  // Thread Item Tests
  // ============================================================================

  describe('Thread Item', () => {
    it('displays thread title', () => {
      renderWithQueryClient(<ChatHistorySection threads={mockThreads} />);

      expect(screen.getByTestId('chat-title-thread-1')).toHaveTextContent('Regole Wingspan - Setup iniziale');
      expect(screen.getByTestId('chat-title-thread-2')).toHaveTextContent('Strategie Catan - Espansione Marinai');
    });

    it('displays timestamp', () => {
      renderWithQueryClient(<ChatHistorySection threads={mockThreads} />);

      // Timestamps should be rendered
      expect(screen.getByTestId('chat-time-thread-1')).toBeInTheDocument();
      expect(screen.getByTestId('chat-time-thread-2')).toBeInTheDocument();
    });

    it('thread links to chat page', () => {
      renderWithQueryClient(<ChatHistorySection threads={mockThreads} />);

      const threadLink = screen.getByTestId('chat-thread-thread-1');
      expect(threadLink).toHaveAttribute('href', '/chat/thread-1');
    });

    it('displays game title when available', () => {
      const threadsWithGame: ChatThread[] = [
        {
          id: 'thread-game',
          title: 'Discussion',
          lastMessageAt: new Date().toISOString(),
          messageCount: 5,
          gameTitle: 'Wingspan',
        },
      ];

      renderWithQueryClient(<ChatHistorySection threads={threadsWithGame} />);

      expect(screen.getByText('Wingspan')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Navigation Tests
  // ============================================================================

  describe('Navigation', () => {
    it('new chat button links to /chat', () => {
      renderWithQueryClient(<ChatHistorySection threads={mockThreads} />);

      const newChatBtn = screen.getByTestId('new-chat-button');
      expect(newChatBtn.closest('a')).toHaveAttribute('href', '/chat');
    });

    it('view all link appears when more than 4 threads', () => {
      renderWithQueryClient(<ChatHistorySection threads={fiveThreads} totalCount={5} />);

      const viewAll = screen.getByTestId('view-all-chats');
      expect(viewAll).toBeInTheDocument();
      expect(viewAll).toHaveAttribute('href', '/chat/history');
    });

    it('view all link hidden when 4 or fewer threads', () => {
      renderWithQueryClient(<ChatHistorySection threads={fourThreads} totalCount={4} />);

      expect(screen.queryByTestId('view-all-chats')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Empty State Tests
  // ============================================================================

  describe('Empty State', () => {
    it('renders empty state when no threads', () => {
      renderWithQueryClient(<ChatHistorySection threads={[]} />);

      expect(screen.getByTestId('chat-history-empty')).toBeInTheDocument();
    });

    it('shows empty message', () => {
      renderWithQueryClient(<ChatHistorySection threads={[]} />);

      expect(screen.getByText('Nessuna conversazione')).toBeInTheDocument();
    });

    it('shows start chat CTA', () => {
      renderWithQueryClient(<ChatHistorySection threads={[]} />);

      const cta = screen.getByTestId('start-chat-cta');
      expect(cta).toBeInTheDocument();
      expect(cta.closest('a')).toHaveAttribute('href', '/chat');
    });

    it('does not show thread items in empty state', () => {
      renderWithQueryClient(<ChatHistorySection threads={[]} />);

      expect(screen.queryByTestId('threads-list')).not.toBeInTheDocument();
    });

    it('renders empty state from backend when no sessions', () => {
      mockUseRecentChatSessions.mockReturnValue({
        data: { sessions: [], totalCount: 0 },
        isLoading: false,
        error: null,
      });

      renderWithQueryClient(<ChatHistorySection userId="user-123" />);

      expect(screen.getByTestId('chat-history-empty')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders skeleton when isLoading prop is true', () => {
      renderWithQueryClient(<ChatHistorySection isLoading />);

      expect(screen.getByTestId('chat-history-skeleton')).toBeInTheDocument();
    });

    it('does not render content when loading', () => {
      renderWithQueryClient(<ChatHistorySection threads={mockThreads} isLoading />);

      expect(screen.queryByTestId('chat-history-widget')).not.toBeInTheDocument();
      expect(screen.queryByTestId('chat-thread-thread-1')).not.toBeInTheDocument();
    });

    it('skeleton has glassmorphic styling', () => {
      renderWithQueryClient(<ChatHistorySection isLoading />);

      const skeleton = screen.getByTestId('chat-history-skeleton');
      expect(skeleton).toHaveClass('backdrop-blur-xl');
    });
  });

  // ============================================================================
  // Delete Functionality Tests
  // ============================================================================

  describe('Delete Functionality', () => {
    it('shows delete button on hover when userId provided', () => {
      renderWithQueryClient(
        <ChatHistorySection threads={mockThreads} userId="user-123" />
      );

      // Delete button should exist (visible on hover via CSS)
      expect(screen.getByTestId('delete-thread-thread-1')).toBeInTheDocument();
    });

    it('does not show delete button without userId', () => {
      renderWithQueryClient(<ChatHistorySection threads={mockThreads} />);

      expect(screen.queryByTestId('delete-thread-thread-1')).not.toBeInTheDocument();
    });

    it('calls delete mutation when delete clicked', async () => {
      const mockMutate = vi.fn();
      mockUseDeleteChatSession.mockReturnValue({
        mutate: mockMutate,
        isPending: false,
        variables: null,
      });

      renderWithQueryClient(
        <ChatHistorySection
          threads={[{ ...mockThreads[0], gameId: 'game-123' }]}
          userId="user-123"
        />
      );

      const deleteBtn = screen.getByTestId('delete-thread-thread-1');
      fireEvent.click(deleteBtn);

      expect(mockMutate).toHaveBeenCalledWith({
        sessionId: 'thread-1',
        userId: 'user-123',
        gameId: 'game-123',
      });
    });

    it('shows loading state during deletion', () => {
      mockUseDeleteChatSession.mockReturnValue({
        mutate: vi.fn(),
        isPending: true,
        variables: { sessionId: 'thread-1' },
      });

      const { container } = renderWithQueryClient(
        <ChatHistorySection threads={mockThreads} userId="user-123" />
      );

      // First thread should have opacity class
      const firstThread = container.querySelector('[data-testid="chat-thread-thread-1"]')?.parentElement;
      expect(firstThread).toHaveClass('opacity-50');
    });
  });

  // ============================================================================
  // Styling Tests
  // ============================================================================

  describe('Styling', () => {
    it('widget has glassmorphic styling', () => {
      renderWithQueryClient(<ChatHistorySection threads={mockThreads} />);

      const widget = screen.getByTestId('chat-history-widget');
      expect(widget).toHaveClass('backdrop-blur-xl');
      expect(widget).toHaveClass('rounded-2xl');
    });

    it('header icon has blue styling', () => {
      const { container } = renderWithQueryClient(
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
      renderWithQueryClient(<ChatHistorySection threads={singleThread} />);

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
      renderWithQueryClient(<ChatHistorySection threads={longTitleThread} />);

      const titleElement = screen.getByTestId('chat-title-thread-long');
      expect(titleElement).toHaveClass('truncate');
    });

    it('uses lastMessageAt for timestamp when available', () => {
      const mockData = {
        sessions: [
          {
            id: 'session-1',
            userId: 'user-123',
            gameId: 'game-456',
            title: 'Test',
            messageCount: 5,
            createdAt: '2026-01-01T10:00:00Z',
            lastMessageAt: '2026-01-20T12:00:00Z',
            isArchived: false,
          },
        ],
        totalCount: 1,
      };

      mockUseRecentChatSessions.mockReturnValue({
        data: mockData,
        isLoading: false,
        error: null,
      });

      renderWithQueryClient(<ChatHistorySection userId="user-123" />);

      // Should show lastMessageAt time, not createdAt
      expect(screen.getByTestId('chat-time-session-1')).toBeInTheDocument();
    });

    it('falls back to createdAt when lastMessageAt is null', () => {
      const mockData = {
        sessions: [
          {
            id: 'session-1',
            userId: 'user-123',
            gameId: 'game-456',
            title: 'Test',
            messageCount: 0,
            createdAt: '2026-01-20T10:00:00Z',
            lastMessageAt: null,
            isArchived: false,
          },
        ],
        totalCount: 1,
      };

      mockUseRecentChatSessions.mockReturnValue({
        data: mockData,
        isLoading: false,
        error: null,
      });

      renderWithQueryClient(<ChatHistorySection userId="user-123" />);

      expect(screen.getByTestId('chat-time-session-1')).toBeInTheDocument();
    });
  });
});
