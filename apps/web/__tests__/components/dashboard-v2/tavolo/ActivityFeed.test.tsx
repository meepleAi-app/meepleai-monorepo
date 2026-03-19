/**
 * ActivityFeed (tavolo) — Unit Tests
 * Tests the API-driven activity timeline sidebar component.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ActivityFeed } from '@/components/dashboard-v2/tavolo/ActivityFeed';
import type { ActivityTimelineResponse } from '@/hooks/useActivityTimeline';

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock('@/hooks/useActivityTimeline', () => ({
  useActivityTimeline: vi.fn(),
}));

// date-fns/locale import is fine in jsdom — no mock needed

const { useActivityTimeline } = await import('@/hooks/useActivityTimeline');

// ─── Helpers ─────────────────────────────────────────────────────────────────

function mockHook(overrides: { data?: ActivityTimelineResponse; isLoading?: boolean }) {
  (useActivityTimeline as ReturnType<typeof vi.fn>).mockReturnValue({
    data: undefined,
    isLoading: false,
    ...overrides,
  });
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('ActivityFeed (tavolo)', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it('renders activity items and highlights gameName in amber', () => {
    mockHook({
      data: {
        items: [
          {
            id: 'ev-1',
            type: 'game_added',
            gameName: 'Wingspan',
            timestamp: new Date(Date.now() - 1000 * 60 * 60).toISOString(),
          },
          {
            id: 'ev-2',
            type: 'chat_saved',
            topic: 'Regole Catan',
            gameName: undefined,
            timestamp: new Date(Date.now() - 1000 * 60 * 60 * 5).toISOString(),
          },
        ],
        totalCount: 2,
        hasMore: false,
      },
      isLoading: false,
    });

    render(<ActivityFeed />);

    // gameName should be rendered with amber highlight
    const wingsSpan = screen.getByText('Wingspan');
    expect(wingsSpan).toBeInTheDocument();
    expect(wingsSpan).toHaveClass('text-[#f0a030]');

    // topic should render as plain text
    expect(screen.getByText(/Regole Catan/)).toBeInTheDocument();

    // both items should have testids
    expect(screen.getByTestId('activity-item-ev-1')).toBeInTheDocument();
    expect(screen.getByTestId('activity-item-ev-2')).toBeInTheDocument();
  });

  it('renders empty state when items array is empty', () => {
    mockHook({
      data: { items: [], totalCount: 0, hasMore: false },
      isLoading: false,
    });

    render(<ActivityFeed />);

    expect(screen.getByText('Nessuna attività recente')).toBeInTheDocument();
    expect(screen.queryByTestId(/^activity-item/)).not.toBeInTheDocument();
  });

  it('renders empty state when data is undefined', () => {
    mockHook({ data: undefined, isLoading: false });

    render(<ActivityFeed />);

    expect(screen.getByText('Nessuna attività recente')).toBeInTheDocument();
  });

  it('renders loading skeleton while fetching', () => {
    mockHook({ data: undefined, isLoading: true });

    render(<ActivityFeed />);

    const skeleton = screen.getByTestId('feed-skeleton');
    expect(skeleton).toBeInTheDocument();

    // skeleton contains animated pulse elements
    const pulseElements = skeleton.querySelectorAll('.animate-pulse');
    expect(pulseElements.length).toBe(5);

    // no items should be rendered during loading
    expect(screen.queryByTestId(/^activity-item/)).not.toBeInTheDocument();
  });

  it('passes correct params to useActivityTimeline hook', () => {
    mockHook({
      data: { items: [], totalCount: 0, hasMore: false },
      isLoading: false,
    });

    render(<ActivityFeed />);

    expect(useActivityTimeline).toHaveBeenCalledWith({
      types: [],
      search: '',
      skip: 0,
      take: 20,
      order: 'desc',
    });
  });

  it('renders both topic and gameName when both are present', () => {
    mockHook({
      data: {
        items: [
          {
            id: 'ev-3',
            type: 'chat_saved',
            topic: 'Strategie di gioco',
            gameName: 'Catan',
            timestamp: new Date().toISOString(),
          },
        ],
        totalCount: 1,
        hasMore: false,
      },
      isLoading: false,
    });

    render(<ActivityFeed />);

    expect(screen.getByText(/Strategie di gioco/)).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });
});
