import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { SessionDrawerContent } from '../SessionDrawerContent';
import type { SessionDetailData } from '../../types';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock useSessionDetail hook
vi.mock('../../hooks', () => ({
  useSessionDetail: vi.fn(),
}));

// Mock cascade-navigation-store
vi.mock('@/lib/stores/cascade-navigation-store', () => ({
  useCascadeNavigationStore: vi.fn(() => vi.fn()),
}));

import { useSessionDetail } from '../../hooks';

const mockData: SessionDetailData = {
  id: 's1',
  sessionCode: 'ABC123',
  status: 'InProgress',
  title: 'Catan Pro',
  players: [
    { id: 'p1', displayName: 'Alice', color: 'red', totalScore: 10 },
    { id: 'p2', displayName: 'Bob', color: 'blue', totalScore: 7 },
  ],
  timeline: [
    {
      id: 'e1',
      type: 'turn',
      description: 'Turno 1',
      timestamp: '2026-04-10T10:00:00Z',
      label: 'Turno 1',
    },
  ],
};

describe('SessionDrawerContent', () => {
  beforeEach(() => {
    vi.mocked(useSessionDetail).mockReturnValue({
      data: mockData,
      loading: false,
      error: null,
      retry: vi.fn(),
    });
  });

  it('renders session title', () => {
    render(<SessionDrawerContent entityId="s1" />);
    expect(screen.getByText('Catan Pro')).toBeInTheDocument();
  });

  it('renders live and timeline tabs', () => {
    render(<SessionDrawerContent entityId="s1" />);
    expect(screen.getByRole('tab', { name: /live/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /timeline/i })).toBeInTheDocument();
  });

  it('toolkit tab hidden when no toolkit', () => {
    vi.mocked(useSessionDetail).mockReturnValue({
      data: { ...mockData, toolkit: undefined },
      loading: false,
      error: null,
      retry: vi.fn(),
    });
    render(<SessionDrawerContent entityId="s1" />);
    expect(screen.queryByRole('tab', { name: /toolkit/i })).not.toBeInTheDocument();
  });

  it('toolkit tab shown when toolkit present', () => {
    vi.mocked(useSessionDetail).mockReturnValue({
      data: {
        ...mockData,
        toolkit: {
          id: 't1',
          name: 'My Toolkit',
          version: 1,
          isPublished: true,
          diceTools: [],
          cardTools: [],
          timerTools: [],
          counterTools: [],
        },
      },
      loading: false,
      error: null,
      retry: vi.fn(),
    });
    render(<SessionDrawerContent entityId="s1" />);
    expect(screen.getByRole('tab', { name: /toolkit/i })).toBeInTheDocument();
  });

  it('shows loading skeleton when loading', () => {
    vi.mocked(useSessionDetail).mockReturnValue({
      data: null,
      loading: true,
      error: null,
      retry: vi.fn(),
    });
    render(<SessionDrawerContent entityId="s1" />);
    expect(screen.getByTestId('drawer-loading-skeleton')).toBeInTheDocument();
  });

  it('shows error state when error', () => {
    vi.mocked(useSessionDetail).mockReturnValue({
      data: null,
      loading: false,
      error: 'Network error',
      retry: vi.fn(),
    });
    render(<SessionDrawerContent entityId="s1" />);
    expect(screen.getByTestId('drawer-error-state')).toBeInTheDocument();
  });

  it('live tab shows player names', () => {
    render(<SessionDrawerContent entityId="s1" />);
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('opens toolkit tab when initialTabId is toolkit', () => {
    vi.mocked(useSessionDetail).mockReturnValue({
      data: {
        ...mockData,
        toolkit: {
          id: 't1',
          name: 'My Toolkit',
          version: 1,
          isPublished: true,
          diceTools: [],
          cardTools: [],
          timerTools: [],
          counterTools: [],
        },
      },
      loading: false,
      error: null,
      retry: vi.fn(),
    });
    render(<SessionDrawerContent entityId="s1" initialTabId="toolkit" />);
    const toolkitTab = screen.getByRole('tab', { name: /toolkit/i });
    expect(toolkitTab).toHaveAttribute('aria-selected', 'true');
  });
});
