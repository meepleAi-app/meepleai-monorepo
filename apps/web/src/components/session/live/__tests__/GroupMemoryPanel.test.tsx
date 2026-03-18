/**
 * GroupMemoryPanel Tests
 *
 * AgentMemory — Task 25
 */

import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { GroupMemoryPanel } from '../GroupMemoryPanel';

// ─── Mock API Client ─────────────────────────────────────────────────────────

const mockGetGroup = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api/context', () => ({
  useApiClient: () => ({
    agentMemory: {
      getGroup: mockGetGroup,
    },
  }),
}));

// ─── Test Data ────────────────────────────────────────────────────────────────

const mockGroupData = {
  id: 'group-1',
  name: 'Friday Gamers',
  members: [
    { userId: 'user-abc12345', guestName: null, joinedAt: '2026-01-01T00:00:00Z' },
    { userId: null, guestName: 'Alex', joinedAt: '2026-01-02T00:00:00Z' },
  ],
  preferences: {
    maxDuration: '02:00:00',
    preferredComplexity: 'Medium',
    customNotes: 'We like cooperative games',
  },
  stats: {
    totalSessions: 12,
    gamePlayCounts: { 'game-1': 5, 'game-2': 7 },
    lastPlayedAt: '2026-03-10T18:00:00Z',
  },
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('GroupMemoryPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows loading state initially', () => {
    mockGetGroup.mockReturnValue(new Promise(() => {})); // never resolves
    render(<GroupMemoryPanel groupId="group-1" />);
    expect(screen.getByText('Loading group...')).toBeInTheDocument();
  });

  it('renders group name after loading', async () => {
    mockGetGroup.mockResolvedValue(mockGroupData);
    render(<GroupMemoryPanel groupId="group-1" />);

    await waitFor(() => {
      expect(screen.getByText('Friday Gamers')).toBeInTheDocument();
    });
  });

  it('renders members with guest badge', async () => {
    mockGetGroup.mockResolvedValue(mockGroupData);
    render(<GroupMemoryPanel groupId="group-1" />);

    await waitFor(() => {
      expect(screen.getByText('Alex')).toBeInTheDocument();
      expect(screen.getByText('(guest)')).toBeInTheDocument();
    });
  });

  it('renders preferences', async () => {
    mockGetGroup.mockResolvedValue(mockGroupData);
    render(<GroupMemoryPanel groupId="group-1" />);

    await waitFor(() => {
      expect(screen.getByText(/Complexity: Medium/)).toBeInTheDocument();
      expect(screen.getByText('We like cooperative games')).toBeInTheDocument();
    });
  });

  it('renders session stats', async () => {
    mockGetGroup.mockResolvedValue(mockGroupData);
    render(<GroupMemoryPanel groupId="group-1" />);

    await waitFor(() => {
      expect(screen.getByText('12')).toBeInTheDocument();
      expect(screen.getByText('sessions')).toBeInTheDocument();
    });
  });

  it('shows error state on fetch failure', async () => {
    mockGetGroup.mockRejectedValue(new Error('Network error'));
    render(<GroupMemoryPanel groupId="group-1" />);

    await waitFor(() => {
      expect(screen.getByText('Failed to load group data')).toBeInTheDocument();
    });
  });

  it('shows not found when group is null', async () => {
    mockGetGroup.mockResolvedValue(null);
    render(<GroupMemoryPanel groupId="group-999" />);

    await waitFor(() => {
      expect(screen.getByText('Group not found')).toBeInTheDocument();
    });
  });
});
