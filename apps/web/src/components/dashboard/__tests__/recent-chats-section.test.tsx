/**
 * RecentChatsDashboardSection — Unit Tests
 * Issue #5097, Epic #5094
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { RecentChatsDashboardSection } from '../recent-chats-section';
import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock('@/hooks/queries/useChatSessions', () => ({
  useRecentChatSessions: vi.fn(),
}));

const { useRecentChatSessions } = await import('@/hooks/queries/useChatSessions');

const makeChat = (overrides: Partial<ChatSessionSummaryDto> = {}): ChatSessionSummaryDto => ({
  id: 'chat-1',
  userId: '00000000-0000-0000-0000-000000000001',
  gameId: '00000000-0000-0000-0000-000000000002',
  title: 'Regole di Catan',
  gameTitle: 'Catan',
  messageCount: 5,
  isArchived: false,
  createdAt: new Date().toISOString(),
  lastMessageAt: new Date().toISOString(),
  ...overrides,
});

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('RecentChatsDashboardSection', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it('renders loading skeletons when isLoading', () => {
    (useRecentChatSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: undefined,
      isLoading: true,
    });

    const { container } = render(<RecentChatsDashboardSection />);
    expect(container.querySelectorAll('.animate-pulse').length).toBeGreaterThan(0);
    expect(screen.queryByText('Regole di Catan')).not.toBeInTheDocument();
  });

  it('renders empty state when no chats', () => {
    (useRecentChatSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: { sessions: [] },
      isLoading: false,
    });

    render(<RecentChatsDashboardSection />);
    expect(screen.getByText('Nessuna chat recente')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Inizia una conversazione/ })).toBeInTheDocument();
  });

  it('renders up to 2 chats', () => {
    (useRecentChatSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: {
        sessions: [
          makeChat({ id: 'c1', title: 'Chat uno' }),
          makeChat({ id: 'c2', title: 'Chat due' }),
          makeChat({ id: 'c3', title: 'Chat tre' }), // should not appear
        ],
      },
      isLoading: false,
    });

    render(<RecentChatsDashboardSection />);
    expect(screen.getByText('Chat uno')).toBeInTheDocument();
    expect(screen.getByText('Chat due')).toBeInTheDocument();
    expect(screen.queryByText('Chat tre')).not.toBeInTheDocument();
  });

  it('falls back to gameTitle when title is null', () => {
    (useRecentChatSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: {
        sessions: [makeChat({ title: null as never, gameTitle: 'Wingspan' })],
      },
      isLoading: false,
    });

    render(<RecentChatsDashboardSection />);
    expect(screen.getByText('Chat · Wingspan')).toBeInTheDocument();
  });

  it('falls back to "Chat senza titolo" when title and gameTitle are null', () => {
    (useRecentChatSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: {
        sessions: [makeChat({ title: null as never, gameTitle: null as never })],
      },
      isLoading: false,
    });

    render(<RecentChatsDashboardSection />);
    expect(screen.getByText('Chat senza titolo')).toBeInTheDocument();
  });

  it('links each card to /chat/{id}', () => {
    (useRecentChatSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: {
        sessions: [makeChat({ id: 'chat-99', title: 'La mia chat' })],
      },
      isLoading: false,
    });

    render(<RecentChatsDashboardSection />);
    expect(screen.getByRole('link', { name: /La mia chat/ })).toHaveAttribute(
      'href',
      '/chat/chat-99'
    );
  });

  it('links "Vedi tutte" to /chat', () => {
    (useRecentChatSessions as ReturnType<typeof vi.fn>).mockReturnValue({
      data: { sessions: [makeChat()] },
      isLoading: false,
    });

    render(<RecentChatsDashboardSection />);
    expect(screen.getByRole('link', { name: /Vedi tutte/ })).toHaveAttribute('href', '/chat');
  });
});
