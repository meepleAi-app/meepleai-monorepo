/**
 * Session Notes Page Tests (Issue #4891)
 */

import { screen, waitFor, fireEvent } from '@testing-library/react';
import { useParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import SessionNotesPage from '../page';

const mockGetById = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({
  useParams: vi.fn(),
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/lib/api', () => ({
  api: {
    sessions: {
      getById: mockGetById,
    },
  },
}));

const mockSession = {
  id: 'session-abc',
  gameId: 'game-xyz',
  status: 'Completed',
  startedAt: '2026-01-15T10:00:00Z',
  completedAt: '2026-01-15T12:00:00Z',
  playerCount: 2,
  winnerName: null,
  notes: 'Great game, everyone had fun!',
  durationMinutes: 120,
  players: [{ playerName: 'Alice', playerOrder: 1, color: null }],
};

describe('SessionNotesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (useParams as ReturnType<typeof vi.fn>).mockReturnValue({ id: 'session-abc' });
    localStorage.clear();
  });

  it('renders session official notes', async () => {
    mockGetById.mockResolvedValue(mockSession);

    renderWithQuery(<SessionNotesPage />);

    await waitFor(() => {
      expect(screen.getByText('Great game, everyone had fun!')).toBeInTheDocument();
    });
  });

  it('renders page headings', async () => {
    mockGetById.mockResolvedValue(mockSession);

    renderWithQuery(<SessionNotesPage />);

    await waitFor(() => {
      expect(screen.getByText('Session Notes')).toBeInTheDocument();
    });

    expect(screen.getByText('My Notes')).toBeInTheDocument();
    expect(screen.getByText('Session Summary')).toBeInTheDocument();
  });

  it('shows empty state when no official notes', async () => {
    const noNotesSession = { ...mockSession, notes: null };
    mockGetById.mockResolvedValue(noNotesSession);

    renderWithQuery(<SessionNotesPage />);

    await waitFor(() => {
      expect(screen.getByText(/No official notes recorded/i)).toBeInTheDocument();
    });
  });

  it('saves personal notes to localStorage', async () => {
    mockGetById.mockResolvedValue(mockSession);

    renderWithQuery(<SessionNotesPage />);

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/Add your personal notes/i)).toBeInTheDocument();
    });

    const textarea = screen.getByPlaceholderText(/Add your personal notes/i);
    fireEvent.change(textarea, { target: { value: 'My personal note' } });

    const saveBtn = screen.getByRole('button', { name: /save notes/i });
    fireEvent.click(saveBtn);

    expect(localStorage.getItem('meepleai_session_notes_session-abc')).toBe('My personal note');
  });

  it('loads existing personal notes from localStorage', async () => {
    localStorage.setItem('meepleai_session_notes_session-abc', 'Saved note');
    mockGetById.mockResolvedValue(mockSession);

    renderWithQuery(<SessionNotesPage />);

    await waitFor(() => {
      const textarea = screen.getByPlaceholderText(/Add your personal notes/i);
      expect((textarea as HTMLTextAreaElement).value).toBe('Saved note');
    });
  });

  it('shows error on API failure', async () => {
    mockGetById.mockRejectedValue(new Error('Load failed'));

    renderWithQuery(<SessionNotesPage />);

    await waitFor(() => {
      expect(screen.getByText('Load failed')).toBeInTheDocument();
    });
  });
});
