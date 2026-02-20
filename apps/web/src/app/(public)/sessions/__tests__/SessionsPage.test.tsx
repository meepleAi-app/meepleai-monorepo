/**
 * Sessions Page Tests
 * Issue #4863: EntityListView migration with grid/list/table views
 *
 * Tests EntityListView integration, renderItem mapping,
 * quickActions for session management, and status filtering.
 */

import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Hoisted mocks that need to be referenced in vi.mock factories
const { mockEntityListView, mockPush, mockGetActive, mockGetAllGames } = vi.hoisted(() => ({
  mockEntityListView: vi.fn(() => null),
  mockPush: vi.fn(),
  mockGetActive: vi.fn(),
  mockGetAllGames: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

vi.mock('sonner', () => ({
  toast: { warning: vi.fn() },
}));

vi.mock('@/components/errors', () => ({
  ErrorDisplay: () => <div data-testid="error-display">Error</div>,
}));

vi.mock('@/components/sessions/SessionQuotaBar', () => ({
  SessionQuotaBar: () => <div data-testid="session-quota-bar">Quota</div>,
}));

vi.mock('@/components/ui/data-display/badge', () => ({
  Badge: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
}));

vi.mock('@/components/ui/data-display/card', () => ({
  Card: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  CardContent: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  CardDescription: ({ children }: { children: React.ReactNode }) => <p>{children}</p>,
  CardHeader: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  CardTitle: ({ children }: { children: React.ReactNode }) => <h2>{children}</h2>,
}));

vi.mock('@/components/ui/feedback/skeleton', () => ({
  Skeleton: () => <div data-testid="skeleton" />,
}));

vi.mock('@/components/ui/overlays/tooltip', () => ({
  Tooltip: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  TooltipContent: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  TooltipProvider: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  TooltipTrigger: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

vi.mock('@/components/ui/primitives/button', () => ({
  Button: ({ children, onClick, ...props }: { children: React.ReactNode; onClick?: () => void; [key: string]: unknown }) => (
    <button onClick={onClick} {...props}>{children}</button>
  ),
}));

vi.mock('@/components/ui/data-display/entity-list-view', () => ({
  EntityListView: (props: Record<string, unknown>) => {
    mockEntityListView(props);
    return <div data-testid="entity-list-view">EntityListView</div>;
  },
}));

vi.mock('@/hooks/queries/useSessionQuota', () => ({
  useSessionQuotaWithStatus: () => ({
    data: null,
    isLoading: false,
  }),
}));

vi.mock('@/lib/api', () => ({
  api: {
    sessions: {
      getActive: mockGetActive,
      pause: vi.fn().mockResolvedValue(undefined),
      resume: vi.fn().mockResolvedValue(undefined),
      end: vi.fn().mockResolvedValue(undefined),
    },
    games: {
      getAll: mockGetAllGames,
    },
  },
  GameSessionDto: {},
  Game: {},
  PaginatedSessionsResponse: {},
}));

vi.mock('@/lib/errors', () => ({
  createErrorContext: vi.fn(),
}));

vi.mock('@/lib/errorUtils', () => ({
  categorizeError: vi.fn((e: Error) => e),
}));

vi.mock('@/lib/logger', () => ({
  logger: { error: vi.fn() },
}));

const mockSessions = [
  {
    id: 'session-1',
    gameId: 'game-1',
    status: 'InProgress',
    startedAt: '2025-01-15T10:00:00Z',
    completedAt: null,
    playerCount: 3,
    players: [{ playerName: 'Marco', playerOrder: 1, color: null }],
    winnerName: null,
    notes: null,
    durationMinutes: 45,
  },
  {
    id: 'session-2',
    gameId: 'game-2',
    status: 'Paused',
    startedAt: '2025-01-15T14:00:00Z',
    completedAt: null,
    playerCount: 2,
    players: [{ playerName: 'Luca', playerOrder: 1, color: null }],
    winnerName: null,
    notes: null,
    durationMinutes: 120,
  },
];

const mockGames = [
  { id: 'game-1', title: 'Catan', imageUrl: 'https://example.com/catan.jpg' },
  { id: 'game-2', title: 'Ticket to Ride', imageUrl: null },
];

import ActiveSessionsPage from '../page';

describe('ActiveSessionsPage - EntityListView Migration', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetActive.mockResolvedValue({ sessions: mockSessions, total: 2 });
    mockGetAllGames.mockResolvedValue({ games: mockGames });
  });

  it('renders page title', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      expect(screen.getByText('Active Game Sessions')).toBeInTheDocument();
    });
  });

  it('renders EntityListView after loading', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      expect(screen.getByTestId('entity-list-view')).toBeInTheDocument();
    });
  });

  it('passes entity="session" to EntityListView', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      expect(mockEntityListView).toHaveBeenCalledWith(
        expect.objectContaining({ entity: 'session' })
      );
    });
  });

  it('passes persistenceKey to EntityListView', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      expect(mockEntityListView).toHaveBeenCalledWith(
        expect.objectContaining({ persistenceKey: 'active-sessions' })
      );
    });
  });

  it('supports grid, list, table view modes', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      expect(mockEntityListView).toHaveBeenCalledWith(
        expect.objectContaining({
          availableModes: ['grid', 'list', 'table'],
          defaultViewMode: 'table',
        })
      );
    });
  });

  it('passes enriched sessions with game titles', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      const lastCall = mockEntityListView.mock.calls[mockEntityListView.mock.calls.length - 1]?.[0] as Record<string, unknown> | undefined;
      const items = lastCall?.items as Array<{ gameTitle: string }>;
      expect(items).toHaveLength(2);
      expect(items[0].gameTitle).toBe('Catan');
      expect(items[1].gameTitle).toBe('Ticket to Ride');
    });
  });

  it('renderItem maps session to correct MeepleCard props', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      const lastCall = mockEntityListView.mock.calls[mockEntityListView.mock.calls.length - 1]?.[0] as Record<string, unknown> | undefined;
      const renderItem = lastCall?.renderItem as (s: (typeof mockSessions)[0] & { gameTitle: string; gameImageUrl?: string }) => Record<string, unknown>;
      const result = renderItem({
        ...mockSessions[0],
        gameTitle: 'Catan',
        gameImageUrl: 'https://example.com/catan.jpg',
      });

      expect(result.id).toBe('session-1');
      expect(result.title).toBe('Catan');
      expect(result.badge).toBe('InProgress');
      expect(result.imageUrl).toBe('https://example.com/catan.jpg');
    });
  });

  it('renderItem includes player count and duration in subtitle', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      const lastCall = mockEntityListView.mock.calls[mockEntityListView.mock.calls.length - 1]?.[0] as Record<string, unknown> | undefined;
      const renderItem = lastCall?.renderItem as (s: (typeof mockSessions)[0] & { gameTitle: string }) => Record<string, unknown>;
      const result = renderItem({ ...mockSessions[0], gameTitle: 'Catan' });

      expect(result.subtitle).toContain('3 players');
      expect(result.subtitle).toContain('45m');
    });
  });

  it('renderItem provides metadata with icons', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      const lastCall = mockEntityListView.mock.calls[mockEntityListView.mock.calls.length - 1]?.[0] as Record<string, unknown> | undefined;
      const renderItem = lastCall?.renderItem as (s: (typeof mockSessions)[0] & { gameTitle: string }) => Record<string, unknown>;
      const result = renderItem({ ...mockSessions[0], gameTitle: 'Catan' });
      const metadata = result.metadata as Array<{ label: string }>;

      expect(metadata).toHaveLength(3);
      expect(metadata[0].label).toBe('3 players');
      expect(metadata[2].label).toBe('45m');
    });
  });

  it('renderItem provides Pause quickAction for InProgress sessions', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      const lastCall = mockEntityListView.mock.calls[mockEntityListView.mock.calls.length - 1]?.[0] as Record<string, unknown> | undefined;
      const renderItem = lastCall?.renderItem as (s: (typeof mockSessions)[0] & { gameTitle: string }) => Record<string, unknown>;
      const result = renderItem({ ...mockSessions[0], gameTitle: 'Catan' });
      const actions = result.quickActions as Array<{ label: string; destructive?: boolean }>;

      const labels = actions.map(a => a.label);
      expect(labels).toContain('Pause');
      expect(labels).toContain('End Session');
      expect(labels).not.toContain('Resume');
    });
  });

  it('renderItem provides Resume quickAction for Paused sessions', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      const lastCall = mockEntityListView.mock.calls[mockEntityListView.mock.calls.length - 1]?.[0] as Record<string, unknown> | undefined;
      const renderItem = lastCall?.renderItem as (s: (typeof mockSessions)[0] & { gameTitle: string }) => Record<string, unknown>;
      const result = renderItem({ ...mockSessions[1], gameTitle: 'Ticket to Ride' });
      const actions = result.quickActions as Array<{ label: string }>;

      const labels = actions.map(a => a.label);
      expect(labels).toContain('Resume');
      expect(labels).toContain('End Session');
      expect(labels).not.toContain('Pause');
    });
  });

  it('passes searchable config', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      expect(mockEntityListView).toHaveBeenCalledWith(
        expect.objectContaining({
          searchable: true,
          searchFields: ['gameTitle'],
        })
      );
    });
  });

  it('passes sort options', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      const lastCall = mockEntityListView.mock.calls[mockEntityListView.mock.calls.length - 1]?.[0] as Record<string, unknown> | undefined;
      const sortOptions = lastCall?.sortOptions as Array<{ value: string }>;
      const values = sortOptions.map(s => s.value);

      expect(values).toContain('newest');
      expect(values).toContain('oldest');
      expect(values).toContain('duration');
      expect(values).toContain('players');
    });
  });

  it('passes status filter', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      const lastCall = mockEntityListView.mock.calls[mockEntityListView.mock.calls.length - 1]?.[0] as Record<string, unknown> | undefined;
      const filters = lastCall?.filters as Array<{ id: string; type: string }>;

      expect(filters).toHaveLength(1);
      expect(filters[0].id).toBe('status');
      expect(filters[0].type).toBe('select');
    });
  });

  it('passes tableColumns for table view', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      const lastCall = mockEntityListView.mock.calls[mockEntityListView.mock.calls.length - 1]?.[0] as Record<string, unknown> | undefined;
      const columns = lastCall?.tableColumns as Array<{ header: string }>;

      expect(columns.map(c => c.header)).toEqual([
        'Game', 'Status', 'Players', 'Started', 'Duration',
      ]);
    });
  });

  it('passes onItemClick for navigation', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      const lastCall = mockEntityListView.mock.calls[mockEntityListView.mock.calls.length - 1]?.[0] as Record<string, unknown> | undefined;
      const onItemClick = lastCall?.onItemClick as (session: { id: string }) => void;

      onItemClick({ id: 'session-1' });
      expect(mockPush).toHaveBeenCalledWith('/sessions/session-1');
    });
  });

  it('shows loading skeletons initially', () => {
    mockGetActive.mockImplementation(() => new Promise(() => {}));
    render(<ActiveSessionsPage />);

    expect(screen.getAllByTestId('skeleton')).toHaveLength(3);
  });

  it('shows empty state when no sessions', async () => {
    mockGetActive.mockResolvedValue({ sessions: [], total: 0 });
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      expect(screen.getByText('No active sessions found')).toBeInTheDocument();
    });
  });

  it('renders data-testid on EntityListView', async () => {
    render(<ActiveSessionsPage />);

    await waitFor(() => {
      expect(mockEntityListView).toHaveBeenCalledWith(
        expect.objectContaining({ 'data-testid': 'sessions-list-view' })
      );
    });
  });
});
