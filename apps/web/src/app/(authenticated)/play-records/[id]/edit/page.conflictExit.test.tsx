/**
 * EditPlayRecordPage — leaving the conflict dialog without losing what was typed (#4805).
 *
 * Before: on a conflict the page answered «saved», the form wiped itself and the typed values lived only in
 * `conflictForm`; the dialog had no way out but «Ricarica» (which drops them) and «Sovrascrivi». Now the page
 * answers «not saved», the form keeps the values, and the dialog has a third way out.
 *
 * Here the form and the dialog are the real ones (`page.test.tsx` fakes both, and a fake form that does not
 * wipe itself is exactly what cannot tell these two behaviours apart). Replaced: the wizard store, the data
 * hooks, the router and the picker widgets the form draws.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { toast } from 'sonner';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  playRecordsEditMessages,
  playRecordsNewMessages,
} from '@/__tests__/fixtures/i18n-test-messages';
import { UpdatePlayRecordError } from '@/lib/api/play-records.api';

import EditPlayRecordPage from './page';

// ─── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    // The page asks for `playRecords.edit.*`; the form (shared with the create page) asks for either prefix.
    t: (key: string) => {
      const bare = key.replace(/^playRecords\.(new|edit)\./, '');
      const edit = key.startsWith('playRecords.edit.')
        ? (playRecordsEditMessages as Record<string, string>)[bare]
        : undefined;
      return edit ?? (playRecordsNewMessages as Record<string, string>)[bare] ?? key;
    },
  }),
}));

const mockRouterPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockRouterPush }),
  useParams: () => ({ id: 'record-123' }),
}));

const mockUseUpdateRecord = vi.fn();
const mockUsePlayRecord = vi.fn();
vi.mock('@/lib/domain-hooks/usePlayRecords', () => ({
  usePlayRecord: () => mockUsePlayRecord(),
  useUpdateRecord: () => mockUseUpdateRecord(),
  useDeleteRecord: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

const mockGetRecord = vi.fn();
vi.mock('@/lib/api/play-records.api', async importOriginal => ({
  ...(await importOriginal<typeof import('@/lib/api/play-records.api')>()),
  playRecordsApi: { getRecord: (id: string) => mockGetRecord(id) },
}));

// The wizard store: the step is read at render time, so a test can move the wizard and draw the page again.
const mockResetSessionCreation = vi.fn();
let mockStep = 2;
vi.mock('@/lib/stores/play-records-store', () => ({
  usePlayRecordsStore: () => ({
    sessionCreation: { currentStep: mockStep },
    nextStep: vi.fn(),
    prevStep: vi.fn(),
    resetSessionCreation: mockResetSessionCreation,
    setSessionField: vi.fn(),
  }),
}));

vi.mock('@/lib/hooks/useMediaQuery', () => ({ useMediaQuery: () => true }));
vi.mock('@/hooks/queries/useCurrentUser', () => ({ useCurrentUser: () => ({ data: undefined }) }));
vi.mock('@/components/features/game-picker', () => ({
  GamePicker: () => <div data-testid="play-record-game-stub" />,
}));
vi.mock('@/components/library/AddPrivateGameForm', () => ({ AddPrivateGameForm: () => <div /> }));
vi.mock('@/components/play-records/EditGateBanner', () => ({
  EditGateBanner: () => <div data-testid="edit-gate-banner" />,
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }));

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const RECORD = {
  id: 'record-123',
  gameName: 'Wingspan',
  gameId: 'game-456',
  sessionDate: '2026-05-28T19:00:00.000Z',
  notes: 'gg',
  location: 'Padova',
  players: [
    {
      id: 'player-1',
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
  xmin: 7,
  createdAt: '2026-05-28T20:00:00.000Z',
  updatedAt: '2026-05-28T20:00:00.000Z',
};
const OTHERS_EDIT = { ...RECORD, xmin: 9, notes: 'MODIFICA DI B' };
const TYPED_NOTES = 'Note scritte prima del conflitto';

const conflict = () =>
  new UpdatePlayRecordError('Modifica concorrente rilevata.', 'conflict', 409, 'concurrent-edit');

// ─── Helpers ──────────────────────────────────────────────────────────────────

const notesPlaceholder = () => playRecordsNewMessages['step2.notesPlaceholder'];
const dialog = () => screen.queryByRole('dialog');
const button = (key: 'reload' | 'overwrite' | 'continueEditing') =>
  screen.getByRole('button', { name: playRecordsEditMessages[`conflict.${key}`] });

/** Draws the page, types in the notes (step 2 of the wizard), moves to the last step and returns the helpers. */
function renderPage() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  // A new element each time: React skips a `rerender` of the very same element object.
  const page = () => (
    <QueryClientProvider client={qc}>
      <EditPlayRecordPage />
    </QueryClientProvider>
  );
  mockStep = 1;
  const view = render(page());
  return {
    ...view,
    goToStep: (step: number) => {
      mockStep = step;
      view.rerender(page());
    },
  };
}

/** Submits from the last step, the one from which the real form does submit. */
const submitForm = () => fireEvent.submit(document.querySelector('form') as HTMLFormElement);

async function typeNotesAndSubmit(user: ReturnType<typeof userEvent.setup>) {
  const view = renderPage();
  await user.clear(screen.getByPlaceholderText(notesPlaceholder()));
  await user.type(screen.getByPlaceholderText(notesPlaceholder()), TYPED_NOTES);
  view.goToStep(2);
  submitForm();
  return view;
}

beforeEach(() => {
  vi.clearAllMocks();
  mockStep = 2;
  mockGetRecord.mockResolvedValue(OTHERS_EDIT);
  mockUsePlayRecord.mockReturnValue({ data: RECORD, isLoading: false, error: null });
});

describe('EditPlayRecordPage — the way out of the conflict dialog (#4805)', () => {
  it('keeps the wizard and what was typed when the save meets a conflict', async () => {
    const mutateAsync = vi.fn().mockRejectedValue(conflict());
    mockUseUpdateRecord.mockReturnValue({ mutateAsync, isPending: false });

    const view = await typeNotesAndSubmit(userEvent.setup());

    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    // The form did not take the conflict for «saved»: the wizard is not reset and the roster is still drawn.
    expect(mockResetSessionCreation).not.toHaveBeenCalled();
    expect(screen.getByTestId('player-score-Alice')).toBeInTheDocument();
    view.goToStep(1);
    expect(screen.getByPlaceholderText(notesPlaceholder())).toHaveValue(TYPED_NOTES);
  });

  it('still wipes the form after a save that went through, so the test above can tell them apart', async () => {
    mockUseUpdateRecord.mockReturnValue({
      mutateAsync: vi.fn(async () => ({ id: 'record-123' })),
      isPending: false,
    });

    await typeNotesAndSubmit(userEvent.setup());

    await waitFor(() => expect(mockResetSessionCreation).toHaveBeenCalledTimes(1));
    expect(mockRouterPush).toHaveBeenCalledWith('/play-records/record-123');
  });

  it('«Continua a modificare» closes the dialog and the next «Salva» meets the conflict again', async () => {
    const mutateAsync = vi.fn().mockRejectedValue(conflict());
    mockUseUpdateRecord.mockReturnValue({ mutateAsync, isPending: false });
    const user = userEvent.setup();
    const view = await typeNotesAndSubmit(user);
    await screen.findByRole('dialog');

    await user.click(button('continueEditing'));

    await waitFor(() => expect(dialog()).not.toBeInTheDocument());
    // The cache moves on while the user keeps editing (a refetch on window focus): the other tab's record
    // is now `xmin` 9, but the form still shows the values drawn from the record with `xmin` 7.
    mockUsePlayRecord.mockReturnValue({ data: OTHERS_EDIT, isLoading: false, error: null });
    view.goToStep(1);
    expect(screen.getByPlaceholderText(notesPlaceholder())).toHaveValue(TYPED_NOTES);
    view.goToStep(2);
    submitForm();

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(2));
    // Checked against the record the form was drawn from, not the cache and not a fresh read: nothing was
    // overwritten.
    expect(mutateAsync.mock.calls[1][0]).toMatchObject({ notes: TYPED_NOTES, xmin: 7 });
    expect(mockGetRecord).not.toHaveBeenCalled();
    expect(await screen.findByRole('dialog')).toBeInTheDocument();
  });

  it('ESC is the same way out as the button', async () => {
    mockUseUpdateRecord.mockReturnValue({
      mutateAsync: vi.fn().mockRejectedValue(conflict()),
      isPending: false,
    });
    const user = userEvent.setup();
    const view = await typeNotesAndSubmit(user);
    await screen.findByRole('dialog');

    await user.keyboard('{Escape}');

    await waitFor(() => expect(dialog()).not.toBeInTheDocument());
    view.goToStep(1);
    expect(screen.getByPlaceholderText(notesPlaceholder())).toHaveValue(TYPED_NOTES);
  });

  it('after a permanent failure in «Sovrascrivi» the typed values are still one click away', async () => {
    const mutateAsync = vi
      .fn()
      .mockRejectedValueOnce(conflict())
      .mockRejectedValueOnce(new UpdatePlayRecordError('Forbidden', 'forbidden', 403));
    mockUseUpdateRecord.mockReturnValue({ mutateAsync, isPending: false });
    const user = userEvent.setup();
    const view = await typeNotesAndSubmit(user);
    await screen.findByRole('dialog');

    await user.click(button('overwrite'));
    await waitFor(() => expect(toast.error).toHaveBeenCalledTimes(1));
    expect(dialog()).toBeInTheDocument();

    await user.click(button('continueEditing'));

    await waitFor(() => expect(dialog()).not.toBeInTheDocument());
    view.goToStep(1);
    expect(screen.getByPlaceholderText(notesPlaceholder())).toHaveValue(TYPED_NOTES);
  });

  it('does not let ESC leave while the read that precedes «Sovrascrivi» is in flight', async () => {
    mockUseUpdateRecord.mockReturnValue({
      mutateAsync: vi.fn().mockRejectedValue(conflict()),
      isPending: false,
    });
    let release!: (record: typeof OTHERS_EDIT) => void;
    mockGetRecord.mockReturnValue(new Promise(resolve => (release = resolve)));
    const user = userEvent.setup();
    await typeNotesAndSubmit(user);
    await screen.findByRole('dialog');

    await user.click(button('overwrite'));
    await user.keyboard('{Escape}');

    expect(dialog()).toBeInTheDocument();
    expect(button('continueEditing')).toBeDisabled();
    release(OTHERS_EDIT);
    await waitFor(() => expect(button('overwrite')).toBeEnabled());
  });

  it('sends one edit when «Sovrascrivi» is clicked twice', async () => {
    const mutateAsync = vi
      .fn()
      .mockRejectedValueOnce(conflict())
      .mockResolvedValue({ id: 'record-123' });
    mockUseUpdateRecord.mockReturnValue({ mutateAsync, isPending: false });
    let release!: (record: typeof OTHERS_EDIT) => void;
    mockGetRecord.mockReturnValue(new Promise(resolve => (release = resolve)));
    const user = userEvent.setup();
    await typeNotesAndSubmit(user);
    await screen.findByRole('dialog');

    await user.dblClick(button('overwrite'));
    release(OTHERS_EDIT);

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(2));
    expect(mockGetRecord).toHaveBeenCalledTimes(1);
    expect(mutateAsync.mock.calls[1][0]).toMatchObject({ notes: TYPED_NOTES, xmin: 9 });
  });

  // The wizard's step lives in a store the create page reads too. A conflict used to be answered «saved»
  // and the form reset it; now it is answered «not saved», so the page has to leave it where a new wizard
  // starts, or the next create opens on the last step.
  describe('the wizard step it leaves behind', () => {
    it('is reset when the page goes away after a successful «Sovrascrivi»', async () => {
      const mutateAsync = vi
        .fn()
        .mockRejectedValueOnce(conflict())
        .mockResolvedValue({ id: 'record-123' });
      mockUseUpdateRecord.mockReturnValue({ mutateAsync, isPending: false });
      const user = userEvent.setup();
      const view = await typeNotesAndSubmit(user);
      await screen.findByRole('dialog');
      await user.click(button('overwrite'));
      await waitFor(() => expect(mockRouterPush).toHaveBeenCalledWith('/play-records/record-123'));
      expect(mockResetSessionCreation).not.toHaveBeenCalled();

      view.unmount();

      expect(mockResetSessionCreation).toHaveBeenCalledTimes(1);
    });

    it('is reset when the page goes away after «Continua a modificare»', async () => {
      mockUseUpdateRecord.mockReturnValue({
        mutateAsync: vi.fn().mockRejectedValue(conflict()),
        isPending: false,
      });
      const user = userEvent.setup();
      const view = await typeNotesAndSubmit(user);
      await screen.findByRole('dialog');
      await user.click(button('continueEditing'));
      await waitFor(() => expect(dialog()).not.toBeInTheDocument());
      // Staying on the page keeps the step: the user is still in the middle of the edit.
      expect(mockResetSessionCreation).not.toHaveBeenCalled();

      view.unmount();

      expect(mockResetSessionCreation).toHaveBeenCalledTimes(1);
    });

    it('is reset by «Ricarica», which draws the form again from the top', async () => {
      mockUseUpdateRecord.mockReturnValue({
        mutateAsync: vi.fn().mockRejectedValue(conflict()),
        isPending: false,
      });
      const user = userEvent.setup();
      await typeNotesAndSubmit(user);
      await screen.findByRole('dialog');

      await user.click(button('reload'));

      await waitFor(() => expect(dialog()).not.toBeInTheDocument());
      expect(mockResetSessionCreation).toHaveBeenCalledTimes(1);
    });
  });
});
