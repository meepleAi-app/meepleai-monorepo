/**
 * ClaimGuestGames Tests
 *
 * AgentMemory — Task 25
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ClaimGuestGames } from '../ClaimGuestGames';

// ─── Mock API Client ─────────────────────────────────────────────────────────

const mockGetClaimableGuests = vi.hoisted(() => vi.fn());
const mockClaimGuest = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api/context', () => ({
  useApiClient: () => ({
    agentMemory: {
      getClaimableGuests: mockGetClaimableGuests,
      claimGuest: mockClaimGuest,
    },
  }),
}));

// ─── Test Data ────────────────────────────────────────────────────────────────

const mockGuests = [
  {
    playerMemoryId: 'pm-1',
    guestName: 'Alex',
    groupId: 'group-1',
    groupName: 'Friday Gamers',
  },
  {
    playerMemoryId: 'pm-2',
    guestName: 'Alex G',
    groupId: null,
    groupName: null,
  },
];

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('ClaimGuestGames', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the search input and header', () => {
    render(<ClaimGuestGames />);
    expect(screen.getByText('Claim Guest Games')).toBeInTheDocument();
    expect(screen.getByLabelText('Guest name search')).toBeInTheDocument();
  });

  it('searches for guests when Search button is clicked', async () => {
    mockGetClaimableGuests.mockResolvedValue(mockGuests);
    render(<ClaimGuestGames />);

    const input = screen.getByLabelText('Guest name search');
    fireEvent.change(input, { target: { value: 'Alex' } });
    fireEvent.click(screen.getByText('Search'));

    await waitFor(() => {
      expect(mockGetClaimableGuests).toHaveBeenCalledWith('Alex');
      expect(screen.getByText('Alex')).toBeInTheDocument();
      expect(screen.getByText('Alex G')).toBeInTheDocument();
    });
  });

  it('searches on Enter key press', async () => {
    mockGetClaimableGuests.mockResolvedValue(mockGuests);
    render(<ClaimGuestGames />);

    const input = screen.getByLabelText('Guest name search');
    fireEvent.change(input, { target: { value: 'Alex' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    await waitFor(() => {
      expect(mockGetClaimableGuests).toHaveBeenCalledWith('Alex');
    });
  });

  it('shows group name badge when available', async () => {
    mockGetClaimableGuests.mockResolvedValue(mockGuests);
    render(<ClaimGuestGames />);

    fireEvent.change(screen.getByLabelText('Guest name search'), { target: { value: 'Alex' } });
    fireEvent.click(screen.getByText('Search'));

    await waitFor(() => {
      expect(screen.getByText('Friday Gamers')).toBeInTheDocument();
    });
  });

  it('claims a guest when Claim button is clicked', async () => {
    mockGetClaimableGuests.mockResolvedValue(mockGuests);
    mockClaimGuest.mockResolvedValue(undefined);
    render(<ClaimGuestGames />);

    // Search first
    fireEvent.change(screen.getByLabelText('Guest name search'), { target: { value: 'Alex' } });
    fireEvent.click(screen.getByText('Search'));

    await waitFor(() => {
      expect(screen.getByText('Alex')).toBeInTheDocument();
    });

    // Claim the first guest
    const claimButtons = screen.getAllByText('Claim');
    fireEvent.click(claimButtons[0]);

    await waitFor(() => {
      expect(mockClaimGuest).toHaveBeenCalledWith('pm-1');
      expect(screen.getByText('Claimed! Your history has been linked.')).toBeInTheDocument();
    });
  });

  it('shows error on search failure', async () => {
    mockGetClaimableGuests.mockRejectedValue(new Error('Network error'));
    render(<ClaimGuestGames />);

    fireEvent.change(screen.getByLabelText('Guest name search'), { target: { value: 'Alex' } });
    fireEvent.click(screen.getByText('Search'));

    await waitFor(() => {
      expect(screen.getByText('Failed to search for guests')).toBeInTheDocument();
    });
  });

  it('shows error on claim failure', async () => {
    mockGetClaimableGuests.mockResolvedValue(mockGuests);
    mockClaimGuest.mockRejectedValue(new Error('Claim failed'));
    render(<ClaimGuestGames />);

    fireEvent.change(screen.getByLabelText('Guest name search'), { target: { value: 'Alex' } });
    fireEvent.click(screen.getByText('Search'));

    await waitFor(() => {
      expect(screen.getByText('Alex')).toBeInTheDocument();
    });

    const claimButtons = screen.getAllByText('Claim');
    fireEvent.click(claimButtons[0]);

    await waitFor(() => {
      expect(screen.getByText('Failed to claim guest identity')).toBeInTheDocument();
    });
  });

  it('disables Search button when input is empty', () => {
    render(<ClaimGuestGames />);
    const searchButton = screen.getByText('Search').closest('button');
    expect(searchButton).toBeDisabled();
  });
});
