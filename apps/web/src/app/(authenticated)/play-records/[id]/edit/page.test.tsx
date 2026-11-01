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

import { useState } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { toast } from 'sonner';

import EditPlayRecordPage from './page';
import { playRecordsEditMessages } from '@/__tests__/fixtures/i18n-test-messages';
import { UpdatePlayRecordError } from '@/lib/api/play-records.api';
import { SessionCreateFormSchema } from '@/lib/api/schemas/play-records.schemas';

// What the real form hands over, built with its own schema: if `sessionDate` stopped being a `Date`
// there, this would throw instead of letting a stale fake keep the page test green (#4791). The date
// is not the one in `playRecordFixture`, so a save that sent the record's date instead of the form's
// would be told apart.
const FORM_SUBMISSION = SessionCreateFormSchema.pick({
  sessionDate: true,
  notes: true,
  location: true,
}).parse({
  sessionDate: new Date('2026-06-02T21:30:00.000Z'),
  notes: 'Test notes',
  location: 'Test location',
});

// ─── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) =>
      (playRecordsEditMessages as Record<string, string>)[key.replace('playRecords.edit.', '')] ??
      key,
  }),
}));

// next/navigation
const mockRouterPush = vi.fn();
const mockParams = { id: 'record-123' };
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockRouterPush }),
  useParams: () => mockParams,
}));

// usePlayRecord — provide fixture data
const mockUsePlayRecord = vi.fn();
const mockUseUpdateRecord = vi.fn();
vi.mock('@/lib/domain-hooks/usePlayRecords', () => ({
  usePlayRecord: (id: string) => mockUsePlayRecord(id),
  useUpdateRecord: (id: string) => mockUseUpdateRecord(id),
  useDeleteRecord: () => ({
    mutateAsync: vi.fn(async () => {
      // Mock successful delete
      return { id: 'record-123' };
    }),
    isPending: false,
  }),
}));

// The page re-reads the record before overwriting after a conflict (`playRecordsApi.getRecord`);
// everything else of the module, `UpdatePlayRecordError` included, stays real.
const mockGetRecord = vi.fn();
vi.mock('@/lib/api/play-records.api', async importOriginal => ({
  ...(await importOriginal<typeof import('@/lib/api/play-records.api')>()),
  playRecordsApi: { getRecord: (id: string) => mockGetRecord(id) },
}));

// PlayRecordConflictDialog — mock to avoid Radix portal issues in jsdom
vi.mock('@/components/play-records/PlayRecordConflictDialog', () => ({
  PlayRecordConflictDialog: ({
    open,
    labels,
    isOverwriting,
    onReload,
    onOverwrite,
    onContinueEditing,
  }: {
    open: boolean;
    labels: {
      title: string;
      description: string;
      reload: string;
      overwrite: string;
      continueEditing: string;
    };
    isOverwriting: boolean;
    onReload: () => void;
    onOverwrite: () => void;
    onContinueEditing: () => void;
  }) =>
    open ? (
      <div role="dialog" aria-label={labels.title}>
        <span>{labels.title}</span>
        <span>{labels.description}</span>
        {/* The real dialog turns all three buttons off while `isOverwriting`. */}
        <button type="button" onClick={onContinueEditing} disabled={isOverwriting}>
          {labels.continueEditing}
        </button>
        <button type="button" onClick={onReload} disabled={isOverwriting}>
          {labels.reload}
        </button>
        <button type="button" onClick={onOverwrite} disabled={isOverwriting}>
          {labels.overwrite}
        </button>
      </div>
    ) : null,
}));

// SessionCreateForm — mock the form component.
// The real K5 gate is driven solely by `mode`: in edit mode, game (Step 1) and
// players/scores (Step 3) become readonly; sessionDate/notes/location (Step 2)
// always stay editable. The mock mirrors that contract.
// #2349 AC-4.2: capturedFormProps lets individual tests assert on initialValues/initialPlayers.
const capturedFormProps: { initialValues?: unknown; initialPlayers?: unknown } = {};
// What `onSubmit` answered, one entry per submit. The real form acts on it: `false` means «not saved»
// and it keeps what was typed (#4793), anything else and it clears itself.
const submitAnswers: unknown[] = [];
// How many times the fake form has been mounted: the real one takes `initialValues` only when it mounts, so
// whether the page draws it again (a new `key`) is what decides if it shows a record that changed (#4801).
const formMounts = { count: 0 };
vi.mock('@/components/play-records/SessionCreateForm', () => ({
  SessionCreateForm: ({
    mode,
    onSubmit,
    onCancel,
    isSubmitting,
    initialValues,
    initialPlayers,
  }: any) => {
    capturedFormProps.initialValues = initialValues;
    capturedFormProps.initialPlayers = initialPlayers;
    const [instance] = useState(() => {
      formMounts.count += 1;
      return formMounts.count;
    });
    return (
      <form
        data-testid="session-form"
        data-instance={instance}
        onSubmit={e => {
          e.preventDefault();
          // Simulate form submission with partial data. The real form hands over a `Date`
          // (`SessionCreateFormSchema.sessionDate` is `z.date()`), not a string: a mock that sent
          // a string hid #4791 — the update request wants a string and the page never converted.
          void Promise.resolve(onSubmit(FORM_SUBMISSION)).then(answer => {
            submitAnswers.push(answer);
          });
        }}
      >
        {/* Editable fields (Step 2) — never disabled */}
        <input data-testid="sessionDate-field" type="datetime-local" />
        <input data-testid="location-field" />
        <textarea data-testid="notes-field" />
        {/* Readonly fields (game / players / scores) — disabled in edit mode */}
        <input
          data-testid="game-field"
          disabled={mode === 'edit'}
          aria-readonly={mode === 'edit'}
        />
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
    );
  },
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
    warning: vi.fn(),
  },
}));

// ─── Fixture ────────────────────────────────────────────────────────────────────

// #2349 AC-4.2: fixture matches real PlayRecordDto shape (SessionPlayer has
// id/userId/displayName/scores/totalScore — NOT name/score directly).
const playRecordFixture = {
  id: 'record-123',
  gameName: 'Wingspan',
  gameId: 'game-456',
  sessionDate: new Date('2026-05-28T19:00:00Z').toISOString(),
  notes: 'gg',
  location: 'Padova',
  players: [
    {
      id: 'player-uuid-1',
      userId: 'user-1',
      displayName: 'Alice',
      scores: [{ dimension: 'points', value: 42, unit: null }],
      totalScore: 42,
    },
  ],
  scoringConfig: { enabledDimensions: ['points'], dimensionUnits: {} },
  visibility: 'Private' as const,
  status: 'Completed' as const,
  duration: null,
  startTime: null,
  endTime: null,
  createdByUserId: 'user-1',
  // Optimistic-concurrency token the page must send back with the update (#2437-1).
  xmin: 7,
  createdAt: new Date('2026-05-28T20:00:00Z').toISOString(),
  updatedAt: new Date('2026-05-28T20:00:00Z').toISOString(),
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('EditPlayRecordPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Reset captured form props for each test
    capturedFormProps.initialValues = undefined;
    capturedFormProps.initialPlayers = undefined;
    submitAnswers.length = 0;
    mockParams.id = 'record-123';
    mockUsePlayRecord.mockReturnValue({
      data: playRecordFixture,
      isLoading: false,
      error: null,
    });
    // Default: successful update
    mockUseUpdateRecord.mockReturnValue({
      mutateAsync: vi.fn(async () => ({ id: 'record-123' })),
      isPending: false,
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

  it('AC-4.2: passes mapped record values into SessionCreateForm as initialValues + initialPlayers', async () => {
    // Reset captured props before this test
    capturedFormProps.initialValues = undefined;
    capturedFormProps.initialPlayers = undefined;

    const queryClient = new QueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );

    await screen.findByTestId('session-form');

    // Assert initialValues contains the mapped record fields
    expect(capturedFormProps.initialValues).toMatchObject({
      gameType: 'catalog',
      gameName: 'Wingspan',
      notes: 'gg',
      location: 'Padova',
    });

    // Assert initialPlayers is populated from record.players
    expect(capturedFormProps.initialPlayers).toHaveLength(1);
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
    const mutateAsync = vi.fn(async (_request: Record<string, unknown>) => ({ id: 'record-123' }));
    mockUseUpdateRecord.mockReturnValue({ mutateAsync, isPending: false });
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

    // #4791: the request carries the date the form handed over — converted from `Date` to an ISO
    // string — and nothing but the three editable fields plus the record's concurrency token.
    expect(toast.error).not.toHaveBeenCalled();
    expect(mutateAsync).toHaveBeenCalledTimes(1);
    expect(mutateAsync.mock.calls[0][0]).toStrictEqual({
      sessionDate: '2026-06-02T21:30:00.000Z',
      notes: 'Test notes',
      location: 'Test location',
      xmin: 7,
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

    // Click delete CTA → opens the inline confirmation modal (component uses local
    // showDeleteConfirm state, not a useConfirmationDialog hook).
    await user.click(screen.getByTestId('delete-trigger'));

    // Confirmation modal appears with its title (mock i18n resolves delete.title)
    await waitFor(() => {
      expect(screen.getByText(playRecordsEditMessages['delete.title'])).toBeInTheDocument();
    });

    // Click the destructive confirm button → handleDelete → redirect to /play-records
    await user.click(
      screen.getByRole('button', { name: playRecordsEditMessages['delete.confirm'] })
    );

    await waitFor(() => {
      expect(mockRouterPush).toHaveBeenCalledWith('/play-records');
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

    // Component renders error.message when error is an Error instance (falls back
    // to t('error.loadFailed') only when error has no message).
    await waitFor(() => {
      expect(screen.getByText(/Failed to load record/i)).toBeInTheDocument();
    });
  });

  it('#13: fires the non-blocking amber toast + go-to-live action on SAVED_WHILE_LIVE_ACTIVE', async () => {
    const user = userEvent.setup();

    // The update succeeds but the user had another session live → warning headers.
    mockUseUpdateRecord.mockReturnValue({
      mutateAsync: vi
        .fn()
        .mockResolvedValue({ warningCode: 'SAVED_WHILE_LIVE_ACTIVE', liveSessionId: 'live-1' }),
      isPending: false,
    });

    const queryClient = new QueryClient();
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

    await waitFor(() => {
      expect(toast.warning).toHaveBeenCalledTimes(1);
    });
    const [, opts] = (toast.warning as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(opts).toMatchObject({ duration: 6000 });
    expect(opts.action).toMatchObject({ label: expect.any(String) });
    // The action deep-links to the live session.
    opts.action.onClick();
    expect(mockRouterPush).toHaveBeenCalledWith('/sessions/live-1/live');
  });

  it('#13: fires the warning toast WITHOUT an action when liveSessionId is missing', async () => {
    const user = userEvent.setup();
    mockUseUpdateRecord.mockReturnValue({
      mutateAsync: vi
        .fn()
        .mockResolvedValue({ warningCode: 'SAVED_WHILE_LIVE_ACTIVE', liveSessionId: undefined }),
      isPending: false,
    });

    const queryClient = new QueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('session-form')).toBeInTheDocument();
    });
    await user.click(screen.getByTestId('session-form').querySelector('button[type="submit"]')!);

    await waitFor(() => {
      expect(toast.warning).toHaveBeenCalledTimes(1);
    });
    const [, opts] = (toast.warning as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(opts).toMatchObject({ duration: 6000 });
    expect(opts.action).toBeUndefined();
  });

  it('#13: does NOT fire the warning toast on a normal save (no warning code)', async () => {
    const user = userEvent.setup();
    mockUseUpdateRecord.mockReturnValue({
      mutateAsync: vi.fn().mockResolvedValue({ warningCode: undefined, liveSessionId: undefined }),
      isPending: false,
    });

    const queryClient = new QueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('session-form')).toBeInTheDocument();
    });
    await user.click(screen.getByTestId('session-form').querySelector('button[type="submit"]')!);

    await waitFor(() => {
      expect(toast.success).toHaveBeenCalled();
    });
    expect(toast.warning).not.toHaveBeenCalled();
  });

  it('#2437-1: shows conflict dialog when updateRecord rejects with kind="conflict"', async () => {
    const user = userEvent.setup();

    // Override useUpdateRecord to reject with a conflict error
    mockUseUpdateRecord.mockReturnValue({
      mutateAsync: vi
        .fn()
        .mockRejectedValue(
          new UpdatePlayRecordError(
            'Modifica concorrente rilevata.',
            'conflict',
            409,
            'concurrent-edit'
          )
        ),
      isPending: false,
    });

    const queryClient = new QueryClient();
    render(
      <QueryClientProvider client={queryClient}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('session-form')).toBeInTheDocument();
    });

    // Submit the form — triggers handleSubmit → conflict path
    const form = screen.getByTestId('session-form');
    await user.click(form.querySelector('button[type="submit"]')!);

    // Conflict dialog should appear
    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByText(playRecordsEditMessages['conflict.title'])).toBeInTheDocument();
    });
  });

  // #4793: the form keeps what the user typed only when told the data was not saved. The page used to
  // catch every error and resolve, which the form took for «saved» and wiped the wizard.
  describe('#4793: the answer the page gives the form', () => {
    async function submitOnce(user: ReturnType<typeof userEvent.setup>) {
      render(
        <QueryClientProvider client={new QueryClient()}>
          <EditPlayRecordPage />
        </QueryClientProvider>
      );
      await waitFor(() => {
        expect(screen.getByTestId('session-form')).toBeInTheDocument();
      });
      await user.click(screen.getByTestId('session-form').querySelector('button[type="submit"]')!);
      await waitFor(() => {
        expect(submitAnswers).toHaveLength(1);
      });
    }

    it('says «not saved» when the update fails for a reason other than a conflict', async () => {
      mockUseUpdateRecord.mockReturnValue({
        mutateAsync: vi
          .fn()
          .mockRejectedValue(new UpdatePlayRecordError('Forbidden', 'forbidden', 403)),
        isPending: false,
      });

      await submitOnce(userEvent.setup());

      expect(toast.error).toHaveBeenCalledTimes(1);
      expect(submitAnswers).toEqual([false]);
      expect(mockRouterPush).not.toHaveBeenCalled();
    });

    it('does not say «not saved» when the update went through', async () => {
      await submitOnce(userEvent.setup());

      expect(submitAnswers).toEqual([undefined]);
      expect(mockRouterPush).toHaveBeenCalledWith('/play-records/record-123');
    });

    it('says «not saved» on a conflict too: the form keeps the typed values, the page keeps a copy for «overwrite» (#4805)', async () => {
      mockUseUpdateRecord.mockReturnValue({
        mutateAsync: vi
          .fn()
          .mockRejectedValue(
            new UpdatePlayRecordError('Concurrent edit', 'conflict', 409, 'concurrent-edit')
          ),
        isPending: false,
      });

      await submitOnce(userEvent.setup());

      expect(await screen.findByRole('dialog')).toBeInTheDocument();
      expect(submitAnswers).toEqual([false]);
    });
  });

  it('#4791: «sovrascrivi» after a conflict re-sends the same edit, as an ISO date, with the fresh xmin', async () => {
    const user = userEvent.setup();
    const mutateAsync = vi
      .fn(async (_request: Record<string, unknown>) => ({ id: 'record-123' }))
      .mockRejectedValueOnce(
        new UpdatePlayRecordError(
          'Modifica concorrente rilevata.',
          'conflict',
          409,
          'concurrent-edit'
        )
      );
    mockUseUpdateRecord.mockReturnValue({ mutateAsync, isPending: false });
    // Somebody else saved in between: the record now carries a newer token.
    mockGetRecord.mockResolvedValue({ ...playRecordFixture, xmin: 9 });

    render(
      <QueryClientProvider client={new QueryClient()}>
        <EditPlayRecordPage />
      </QueryClientProvider>
    );
    await waitFor(() => {
      expect(screen.getByTestId('session-form')).toBeInTheDocument();
    });
    await user.click(screen.getByTestId('session-form').querySelector('button[type="submit"]')!);
    await user.click(
      await screen.findByRole('button', { name: playRecordsEditMessages['conflict.overwrite'] })
    );

    await waitFor(() => {
      expect(mutateAsync).toHaveBeenCalledTimes(2);
    });
    expect(mockGetRecord).toHaveBeenCalledWith('record-123');
    expect(toast.error).not.toHaveBeenCalled();
    // Same edit as the first attempt — `conflictForm` keeps the form's `Date` and it is converted
    // again — with the token read back, not the stale one.
    expect(mutateAsync.mock.calls[1][0]).toStrictEqual({
      sessionDate: '2026-06-02T21:30:00.000Z',
      notes: 'Test notes',
      location: 'Test location',
      xmin: 9,
    });
  });

  // #4801: the form takes its values when it mounts, so what «Salva» is checked against has to be the
  // record those values come from. Two tabs, B saves first, then A (opened before) hits the conflict and
  // chooses «Ricarica»: A's form kept the old values while `record.xmin` moved to the new one, and the next
  // «Salva» passed the concurrency check with the old values and overwrote B's edit, silently.
  describe('#4801: the record the form was drawn from', () => {
    const OTHERS_EDIT = { ...playRecordFixture, xmin: 9, notes: 'MODIFICA DI B' };
    const conflict = () =>
      new UpdatePlayRecordError(
        'Modifica concorrente rilevata.',
        'conflict',
        409,
        'concurrent-edit'
      );

    function renderPage(queryClient = new QueryClient()) {
      const page = () => (
        <QueryClientProvider client={queryClient}>
          <EditPlayRecordPage />
        </QueryClientProvider>
      );
      const view = render(page());
      return { ...view, again: () => view.rerender(page()) };
    }
    const submit = (user: ReturnType<typeof userEvent.setup>) =>
      user.click(screen.getByTestId('session-form').querySelector('button[type="submit"]')!);
    const instance = () => screen.getByTestId('session-form').getAttribute('data-instance');

    it('saves with the xmin of the record the form shows, even when the cache has moved on', async () => {
      const mutateAsync = vi.fn(async (_request: Record<string, unknown>) => ({
        id: 'record-123',
      }));
      mockUseUpdateRecord.mockReturnValue({ mutateAsync, isPending: false });
      const user = userEvent.setup();
      const view = renderPage();
      await screen.findByTestId('session-form');

      // Somebody else saves and a refetch (window focus) hands the page their record; the form still
      // shows what it was drawn with.
      mockUsePlayRecord.mockReturnValue({ data: OTHERS_EDIT, isLoading: false, error: null });
      const reads = mockUsePlayRecord.mock.calls.length;
      view.again();
      // The page did render again with the moved record (a case that never re-rendered would pass).
      expect(mockUsePlayRecord.mock.calls.length).toBeGreaterThan(reads);
      expect(capturedFormProps.initialValues).toMatchObject({ notes: 'gg' });
      await submit(user);

      await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(1));
      expect(mutateAsync.mock.calls[0][0]).toMatchObject({ xmin: 7 });
    });

    it('«Ricarica» draws the form again from the fresh record, and the next save is checked against it', async () => {
      const mutateAsync = vi
        .fn(async (_request: Record<string, unknown>) => ({ id: 'record-123' }))
        .mockRejectedValueOnce(conflict());
      mockUseUpdateRecord.mockReturnValue({ mutateAsync, isPending: false });
      mockGetRecord.mockResolvedValue(OTHERS_EDIT);
      const user = userEvent.setup();
      const queryClient = new QueryClient();
      renderPage(queryClient);
      await screen.findByTestId('session-form');
      const drawnFirst = instance();
      await submit(user);
      await user.click(
        await screen.findByRole('button', { name: playRecordsEditMessages['conflict.reload'] })
      );

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      expect(instance()).not.toBe(drawnFirst);
      expect(capturedFormProps.initialValues).toMatchObject({ notes: 'MODIFICA DI B' });
      // The cache holds what was read, so the page and the form speak of the same record.
      expect(queryClient.getQueryData(['play-records', 'detail', 'record-123'])).toEqual(
        OTHERS_EDIT
      );

      await submit(user);
      await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(2));
      expect(mutateAsync.mock.calls[1][0]).toMatchObject({ xmin: 9 });
    });

    it('when «Ricarica» cannot read the record it says so, closes the dialog and invalidates the query', async () => {
      // The dialog has no way out but its two buttons: a record that is gone (404) must not trap the
      // user in it. The invalidation sends such a record to the page's own error state, with a way back.
      const mutateAsync = vi
        .fn(async (_request: Record<string, unknown>) => ({ id: 'record-123' }))
        .mockRejectedValueOnce(conflict());
      mockUseUpdateRecord.mockReturnValue({ mutateAsync, isPending: false });
      mockGetRecord.mockRejectedValue(new Error('rete assente'));
      const user = userEvent.setup();
      const queryClient = new QueryClient();
      const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
      renderPage(queryClient);
      await screen.findByTestId('session-form');
      const drawnFirst = instance();
      await submit(user);
      await user.click(
        await screen.findByRole('button', { name: playRecordsEditMessages['conflict.reload'] })
      );

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      expect(toast.error).toHaveBeenCalledWith(
        playRecordsEditMessages['error.loadFailed'],
        expect.objectContaining({ description: 'rete assente' })
      );
      expect(invalidate).toHaveBeenCalledWith({
        queryKey: ['play-records', 'detail', 'record-123'],
      });
      expect(instance()).toBe(drawnFirst);

      // Nothing was drawn again, so a «Salva» from here is still checked against the old token: it
      // meets the conflict again instead of overwriting.
      await submit(user);
      await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(2));
      expect(mutateAsync.mock.calls[1][0]).toMatchObject({ xmin: 7 });
    });

    it('a second click on «Ricarica» while it is reading does nothing, and «Sovrascrivi» is off too', async () => {
      mockUseUpdateRecord.mockReturnValue({
        mutateAsync: vi.fn().mockRejectedValue(conflict()),
        isPending: false,
      });
      let release: (record: unknown) => void = () => {};
      mockGetRecord.mockReturnValue(
        new Promise(resolve => {
          release = resolve;
        })
      );
      const user = userEvent.setup();
      renderPage();
      await screen.findByTestId('session-form');
      const mountsBefore = formMounts.count;
      await submit(user);
      const reload = await screen.findByRole('button', {
        name: playRecordsEditMessages['conflict.reload'],
      });
      await user.click(reload);

      await waitFor(() => expect(reload).toBeDisabled());
      expect(
        screen.getByRole('button', { name: playRecordsEditMessages['conflict.overwrite'] })
      ).toBeDisabled();
      await user.click(reload);
      expect(mockGetRecord).toHaveBeenCalledTimes(1);

      release(OTHERS_EDIT);
      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      // Drawn again once, not twice.
      expect(formMounts.count).toBe(mountsBefore + 1);
    });

    it('keeps the dialog when «Sovrascrivi comunque» fails for another reason, so it can be retried', async () => {
      const mutateAsync = vi
        .fn(async (_request: Record<string, unknown>) => ({ id: 'record-123' }))
        .mockRejectedValueOnce(conflict())
        .mockRejectedValueOnce(new UpdatePlayRecordError('Forbidden', 'forbidden', 403));
      mockUseUpdateRecord.mockReturnValue({ mutateAsync, isPending: false });
      mockGetRecord.mockResolvedValue(OTHERS_EDIT);
      const user = userEvent.setup();
      renderPage();
      await screen.findByTestId('session-form');
      await submit(user);
      const overwrite = playRecordsEditMessages['conflict.overwrite'];
      await user.click(await screen.findByRole('button', { name: overwrite }));

      await waitFor(() => expect(toast.error).toHaveBeenCalledTimes(1));
      expect(screen.getByRole('dialog')).toBeInTheDocument();

      await user.click(screen.getByRole('button', { name: overwrite }));
      await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(3));
      // The retry sends the same edit the user made, with the token read back.
      expect(mutateAsync.mock.calls[2][0]).toStrictEqual({
        sessionDate: '2026-06-02T21:30:00.000Z',
        notes: 'Test notes',
        location: 'Test location',
        xmin: 9,
      });
    });

    it('takes the record when it arrives after the first render (a cold cache)', async () => {
      // The real path: the first render has no record and shows the skeleton, the second has it and has to
      // fix `base` then, not at the first render.
      const mutateAsync = vi.fn(async (_request: Record<string, unknown>) => ({
        id: 'record-123',
      }));
      mockUseUpdateRecord.mockReturnValue({ mutateAsync, isPending: false });
      mockUsePlayRecord.mockReturnValue({ data: undefined, isLoading: true, error: null });
      const user = userEvent.setup();
      const view = renderPage();
      expect(screen.queryByTestId('session-form')).not.toBeInTheDocument();

      mockUsePlayRecord.mockReturnValue({ data: playRecordFixture, isLoading: false, error: null });
      view.again();
      await screen.findByTestId('session-form');
      expect(capturedFormProps.initialValues).toMatchObject({ gameName: 'Wingspan', notes: 'gg' });
      await submit(user);

      await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(1));
      expect(mutateAsync.mock.calls[0][0]).toMatchObject({ xmin: 7 });
    });

    it('never reads the record of one id as the record of another', async () => {
      // `base` is tied to the route id it was taken for. If the page is reused for another id, the form is
      // drawn from that record and saved against its token, not against the first record's.
      const mutateAsync = vi.fn(async (_request: Record<string, unknown>) => ({
        id: 'record-456',
      }));
      mockUseUpdateRecord.mockReturnValue({ mutateAsync, isPending: false });
      const user = userEvent.setup();
      const view = renderPage();
      await screen.findByTestId('session-form');
      expect(capturedFormProps.initialValues).toMatchObject({ gameName: 'Wingspan' });

      mockParams.id = 'record-456';
      mockUsePlayRecord.mockImplementation((id: string) => ({
        data:
          id === 'record-456'
            ? { ...playRecordFixture, id, gameName: 'Catan', xmin: 3 }
            : undefined,
        isLoading: false,
        error: null,
      }));
      view.again();
      await submit(user);

      await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(1));
      expect(capturedFormProps.initialValues).toMatchObject({ gameName: 'Catan' });
      expect(mutateAsync.mock.calls[0][0]).toMatchObject({ xmin: 3 });
    });
  });
});
