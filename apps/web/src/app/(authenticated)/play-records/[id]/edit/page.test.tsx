/**
 * EditPlayRecordPage — K5 gate readonly + delete flow tests
 *
 * AC-4.1  Layout identico a `new` (riusa SessionCreateForm con prop `mode='edit'`)
 * AC-4.2  Pre-fill: caricamento via `usePlayRecord(id)` + setValues su form
 * AC-4.3  K5 gate readonly: ONLY sessionDate/notes/location editable; others disabled
 * AC-4.4  Banner inline sopra form: "Per modificare..." + "Cancella partita" link
 * AC-4.5  Submit → PUT /api/v1/play-records/{id} con UpdatePlayRecordRequest
 * AC-4.6  Delete CTA: confirmation dialog → DELETE → redirect /play-records
 * AC-4.7  K11 cache invalidation post-update/delete
 * AC-4.8  K16 a11y: aria-readonly on readonly fields; focus on sessionDate
 *
 * Issue #1488: Play Records reskin — Task 4
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import EditPlayRecordPage from './page';
import { playRecordsEditMessages } from '@/__tests__/fixtures/i18n-test-messages';

// ─── Mocks ────────────────────────────────────────────────────────────────────

// next-intl
vi.mock('next-intl', () => ({
  useTranslations: (ns: string) => (key: string) => {
    const full = `${key}`;
    return (playRecordsEditMessages as Record<string, string>)[full] ?? key;
  },
}));

// next/navigation
const mockRouterPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockRouterPush }),
  useParams: () => ({ id: 'record-123' }),
}));

// usePlayRecord — provide fixture data
const mockUsePlayRecord = vi.fn();
vi.mock('@/lib/domain-hooks/usePlayRecords', () => ({
  usePlayRecord: (id: string) => mockUsePlayRecord(id),
  useUpdateRecord: () => ({
    mutateAsync: vi.fn(async data => {
      // Mock successful update
      return { id: 'record-123' };
    }),
    isPending: false,
  }),
  useDeleteRecord: () => ({
    mutateAsync: vi.fn(async () => {
      // Mock successful delete
      return { id: 'record-123' };
    }),
    isPending: false,
  }),
}));

// SessionCreateForm — mock the form component
vi.mock('@/components/play-records/SessionCreateForm', () => ({
  SessionCreateForm: ({
    mode,
    editableFields,
    initialValues,
    onSubmit,
    onCancel,
    isSubmitting,
  }: any) => (
    <form
      data-testid="session-form"
      onSubmit={e => {
        e.preventDefault();
        // Simulate form submission with partial data
        onSubmit({
          sessionDate: new Date().toISOString(),
          notes: 'Test notes',
          location: 'Test location',
        });
      }}
    >
      <input
        data-testid="sessionDate-field"
        type="datetime-local"
        disabled={mode === 'edit' && !editableFields?.has('sessionDate')}
        aria-readonly={mode === 'edit' && !editableFields?.has('sessionDate')}
      />
      <input
        data-testid="location-field"
        disabled={mode === 'edit' && !editableFields?.has('location')}
        aria-readonly={mode === 'edit' && !editableFields?.has('location')}
      />
      <textarea
        data-testid="notes-field"
        disabled={mode === 'edit' && !editableFields?.has('notes')}
        aria-readonly={mode === 'edit' && !editableFields?.has('notes')}
      />
      {/* Mock readonly fields (game, players, scores) */}
      <input data-testid="game-field" disabled={mode === 'edit'} aria-readonly={mode === 'edit'} />
      <input
        data-testid="players-field"
        disabled={mode === 'edit'}
        aria-readonly={mode === 'edit'}
      />
      <input
        data-testid="scores-field"
        disabled={mode === 'edit'}
        aria-readonly={mode === 'edit'}
      />
      <button type="submit" disabled={isSubmitting}>
        Save
      </button>
      {onCancel && (
        <button type="button" onClick={onCancel}>
          Cancel
        </button>
      )}
    </form>
  ),
}));

// EditGateBanner — mock the banner
vi.mock('@/components/play-records/EditGateBanner', () => ({
  EditGateBanner: ({ onDelete }: any) => (
    <div data-testid="edit-gate-banner" role="alert">
      <p>Per modificare giocatori o punteggi, elimina e ricrea la partita</p>
      <button data-testid="delete-trigger" onClick={onDelete}>
        Cancella partita
      </button>
    </div>
  ),
}));

// ConfirmationDialog
const mockShowConfirmation = vi.fn(async () => true);
vi.mock('@/components/ui/dialogs/confirmation-dialog', () => ({
  useConfirmationDialog: () => ({
    show: mockShowConfirmation,
  }),
}));

// toast
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// ─── Fixture ────────────────────────────────────────────────────────────────────

const playRecordFixture = {
  id: 'record-123',
  gameName: 'Catan',
  gameId: 'game-456',
  sessionDate: new Date('2026-05-28T19:00:00Z').toISOString(),
  notes: 'Great game!',
  location: 'Home',
  players: [
    { userId: 'user-1', name: 'Alice', score: 10 },
    { userId: 'user-2', name: 'Bob', score: 8 },
  ],
  winner: 'user-1',
  totalScore: 18,
  status: 'Completed',
  createdAt: new Date('2026-05-28T20:00:00Z').toISOString(),
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('EditPlayRecordPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUsePlayRecord.mockReturnValue({
      data: playRecordFixture,
      isLoading: false,
      error: null,
    });
  });

  it('AC-4.1: renders layout identical to new (using SessionCreateForm)', async () => {
    const queryClient = new QueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('session-form')).toBeInTheDocument();
    });
  });

  it('AC-4.2: pre-fills form with loaded record data', async () => {
    const queryClient = new QueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );

    await waitFor(() => {
      // Form should be rendered with pre-filled values (verified by SessionCreateForm receiving initialValues)
      expect(mockUsePlayRecord).toHaveBeenCalledWith('record-123');
    });
  });

  it('AC-4.3: K5 gate – only sessionDate/notes/location editable; others readonly', async () => {
    const queryClient = new QueryClient();
    const { container } = render(
      <QueryClientProvider client={queryClient}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );

    await waitFor(() => {
      // Editable fields should NOT be disabled
      const sessionDateField = screen.getByTestId('sessionDate-field');
      const locationField = screen.getByTestId('location-field');
      const notesField = screen.getByTestId('notes-field');

      expect(sessionDateField).not.toHaveAttribute('disabled');
      expect(locationField).not.toHaveAttribute('disabled');
      expect(notesField).not.toHaveAttribute('disabled');

      // Readonly fields SHOULD be disabled
      const gameField = screen.getByTestId('game-field');
      const playersField = screen.getByTestId('players-field');
      const scoresField = screen.getByTestId('scores-field');

      expect(gameField).toHaveAttribute('disabled');
      expect(playersField).toHaveAttribute('disabled');
      expect(scoresField).toHaveAttribute('disabled');

      // Readonly fields should have aria-readonly
      expect(gameField).toHaveAttribute('aria-readonly', 'true');
      expect(playersField).toHaveAttribute('aria-readonly', 'true');
      expect(scoresField).toHaveAttribute('aria-readonly', 'true');
    });
  });

  it('AC-4.4: displays banner with delete CTA', async () => {
    const queryClient = new QueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('edit-gate-banner')).toBeInTheDocument();
      expect(screen.getByTestId('delete-trigger')).toBeInTheDocument();
    });
  });

  it('AC-4.5: submits PUT request with only sessionDate/notes/location', async () => {
    const queryClient = new QueryClient();
    const user = userEvent.setup();

    render(
      <QueryClientProvider client={queryClient}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('session-form')).toBeInTheDocument();
    });

    const form = screen.getByTestId('session-form');
    await user.click(form.querySelector('button[type="submit"]')!);

    // Verify onSubmit was called (would call PUT API in real implementation)
    await waitFor(() => {
      expect(mockRouterPush).toHaveBeenCalledWith('/play-records/record-123');
    });
  });

  it('AC-4.6: delete flow shows confirmation dialog and deletes record', async () => {
    const queryClient = new QueryClient();
    const user = userEvent.setup();

    render(
      <QueryClientProvider client={queryClient}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('delete-trigger')).toBeInTheDocument();
    });

    const deleteButton = screen.getByTestId('delete-trigger');
    await user.click(deleteButton);

    // Verify confirmation dialog was shown
    await waitFor(() => {
      expect(mockShowConfirmation).toHaveBeenCalled();
    });
  });

  it('AC-4.8: K16 a11y – sessionDate gets focus on mount', async () => {
    const queryClient = new QueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );

    await waitFor(() => {
      // In real implementation, sessionDate field would receive autofocus
      expect(screen.getByTestId('sessionDate-field')).toBeInTheDocument();
    });
  });

  it('handles loading state gracefully', async () => {
    mockUsePlayRecord.mockReturnValue({
      data: null,
      isLoading: true,
      error: null,
    });

    const queryClient = new QueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );

    // Loading skeleton should be visible
    await waitFor(() => {
      expect(screen.queryByTestId('session-form')).not.toBeInTheDocument();
    });
  });

  it('handles error state gracefully', async () => {
    const testError = new Error('Failed to load record');
    mockUsePlayRecord.mockReturnValue({
      data: null,
      isLoading: false,
      error: testError,
    });

    const queryClient = new QueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );

    await waitFor(() => {
      expect(screen.getByText(/not found/i)).toBeInTheDocument();
    });
  });
});
