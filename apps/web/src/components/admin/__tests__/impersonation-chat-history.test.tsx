/**
 * ImpersonationChatHistory Tests (Issue #3700)
 *
 * Tests for the chat history viewer component:
 * - Loading state
 * - Empty state
 * - Error state
 * - Session list rendering
 * - Session count display
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { ImpersonationChatHistory } from '../impersonation-chat-history';

// Mock the hooks
vi.mock('@/hooks/queries', () => ({
  useRecentChatSessions: vi.fn(),
}));

import { useRecentChatSessions } from '@/hooks/queries';

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
};

const mockSessions = [
  {
    id: 'session-1',
    title: 'Catan rules question',
    createdAt: '2026-02-10T10:00:00Z',
    lastMessageAt: '2026-02-10T12:30:00Z',
    messageCount: 5,
    gameId: 'game-1',
    gameTitle: 'Catan',
    userId: 'user-123',
  },
  {
    id: 'session-2',
    title: 'Wingspan strategy',
    createdAt: '2026-02-09T08:00:00Z',
    lastMessageAt: '2026-02-09T09:15:00Z',
    messageCount: 12,
    gameId: 'game-2',
    gameTitle: 'Wingspan',
    userId: 'user-123',
  },
  {
    id: 'session-3',
    title: 'General help',
    createdAt: '2026-02-08T14:00:00Z',
    lastMessageAt: '2026-02-08T14:45:00Z',
    messageCount: 3,
    gameId: undefined,
    gameTitle: undefined,
    userId: 'user-123',
  },
];

describe('ImpersonationChatHistory', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('loading state', () => {
    it('should show skeleton when loading', () => {
      vi.mocked(useRecentChatSessions).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useRecentChatSessions>);

      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByTestId('impersonation-chat-skeleton')).toBeInTheDocument();
    });
  });

  describe('empty state', () => {
    it('should show empty state when no sessions', () => {
      vi.mocked(useRecentChatSessions).mockReturnValue({
        data: { sessions: [], totalCount: 0 },
        isLoading: false,
        error: null,
      } as ReturnType<typeof useRecentChatSessions>);

      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByTestId('impersonation-chat-empty')).toBeInTheDocument();
      expect(screen.getByText('This user has no AI chat sessions.')).toBeInTheDocument();
    });

    it('should show "No conversations found" text when empty', () => {
      vi.mocked(useRecentChatSessions).mockReturnValue({
        data: { sessions: [], totalCount: 0 },
        isLoading: false,
        error: null,
      } as ReturnType<typeof useRecentChatSessions>);

      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('No conversations found')).toBeInTheDocument();
    });
  });

  describe('error state', () => {
    it('should show error message on fetch failure', () => {
      vi.mocked(useRecentChatSessions).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Network error'),
      } as ReturnType<typeof useRecentChatSessions>);

      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('Failed to load chat history.')).toBeInTheDocument();
    });
  });

  describe('session list', () => {
    beforeEach(() => {
      vi.mocked(useRecentChatSessions).mockReturnValue({
        data: { sessions: mockSessions, totalCount: 3 },
        isLoading: false,
        error: null,
      } as ReturnType<typeof useRecentChatSessions>);
    });

    it('should render session items', () => {
      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      const sessions = screen.getAllByTestId('impersonation-chat-session');
      expect(sessions).toHaveLength(3);
    });

    it('should display session titles', () => {
      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('Catan rules question')).toBeInTheDocument();
      expect(screen.getByText('Wingspan strategy')).toBeInTheDocument();
      expect(screen.getByText('General help')).toBeInTheDocument();
    });

    it('should display game titles when available', () => {
      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Wingspan')).toBeInTheDocument();
    });

    it('should display message counts', () => {
      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('5')).toBeInTheDocument();
      expect(screen.getByText('12')).toBeInTheDocument();
      expect(screen.getByText('3')).toBeInTheDocument();
    });

    it('should display conversation count in summary', () => {
      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('3 conversations found')).toBeInTheDocument();
    });

    it('should handle singular conversation count', () => {
      vi.mocked(useRecentChatSessions).mockReturnValue({
        data: { sessions: [mockSessions[0]], totalCount: 1 },
        isLoading: false,
        error: null,
      } as ReturnType<typeof useRecentChatSessions>);

      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('1 conversation found')).toBeInTheDocument();
    });
  });

  describe('accessibility', () => {
    beforeEach(() => {
      vi.mocked(useRecentChatSessions).mockReturnValue({
        data: { sessions: mockSessions, totalCount: 3 },
        isLoading: false,
        error: null,
      } as ReturnType<typeof useRecentChatSessions>);
    });

    it('should have role="list" on session container', () => {
      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByRole('list', { name: 'Chat sessions' })).toBeInTheDocument();
    });

    it('should have role="listitem" on each session', () => {
      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      const items = screen.getAllByRole('listitem');
      expect(items).toHaveLength(3);
    });

    it('should have aria-hidden on decorative icons', () => {
      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      const list = screen.getByRole('list');
      const hiddenIcons = list.querySelectorAll('[aria-hidden="true"]');
      expect(hiddenIcons.length).toBeGreaterThanOrEqual(3);
    });

    it('should have role="alert" on error state', () => {
      vi.mocked(useRecentChatSessions).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Network error'),
      } as ReturnType<typeof useRecentChatSessions>);

      render(<ImpersonationChatHistory userId="user-123" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });

  describe('props', () => {
    it('should pass userId to useRecentChatSessions', () => {
      vi.mocked(useRecentChatSessions).mockReturnValue({
        data: { sessions: [], totalCount: 0 },
        isLoading: false,
        error: null,
      } as ReturnType<typeof useRecentChatSessions>);

      render(<ImpersonationChatHistory userId="user-456" />, {
        wrapper: createWrapper(),
      });

      expect(useRecentChatSessions).toHaveBeenCalledWith('user-456', {
        limit: 20,
        enabled: true,
      });
    });

    it('should pass custom limit', () => {
      vi.mocked(useRecentChatSessions).mockReturnValue({
        data: { sessions: [], totalCount: 0 },
        isLoading: false,
        error: null,
      } as ReturnType<typeof useRecentChatSessions>);

      render(<ImpersonationChatHistory userId="user-123" limit={10} />, {
        wrapper: createWrapper(),
      });

      expect(useRecentChatSessions).toHaveBeenCalledWith('user-123', {
        limit: 10,
        enabled: true,
      });
    });

    it('should apply additional className', () => {
      vi.mocked(useRecentChatSessions).mockReturnValue({
        data: { sessions: [], totalCount: 0 },
        isLoading: false,
        error: null,
      } as ReturnType<typeof useRecentChatSessions>);

      render(
        <ImpersonationChatHistory userId="user-123" className="custom-class" />,
        { wrapper: createWrapper() }
      );

      expect(screen.getByTestId('impersonation-chat-history')).toHaveClass('custom-class');
    });
  });
});
