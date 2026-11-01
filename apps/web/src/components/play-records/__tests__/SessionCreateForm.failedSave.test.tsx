/**
 * SessionCreateForm — what the form does with the answer of `onSubmit` (#4793).
 *
 * After `await onSubmit(...)` the form clears itself: wizard back to the first step, values back to the
 * initial ones, roster emptied. That is right once the data was saved and wrong when it was not. The
 * comment in the form says a failed save "leaves the entered data intact for a retry", but it relied on
 * `onSubmit` REJECTING, and the edit page does not reject: it catches every error, shows the toast and
 * resolves — so a failed save wiped what the user had typed. `onSubmit` can now answer `false` for
 * «not saved»; any other resolution (`undefined` included, as before) means saved.
 *
 * This is the real form (the page test uses a fake one, which is exactly what let #4791 through): the
 * wizard store is the only thing replaced, so the reset is seen through `resetSessionCreation` and
 * through the roster the last step draws.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { playRecordsNewMessages } from '@/__tests__/fixtures/i18n-test-messages';

import { SessionCreateForm } from '../SessionCreateForm';

// ─── Mocks (same set as SessionCreateForm.test.tsx) ───────────────────────────

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) =>
      (playRecordsNewMessages as Record<string, string>)[
        key.replace('playRecords.new.', '').replace('playRecords.edit.', '')
      ] ?? key,
  }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/lib/hooks/useMediaQuery', () => ({
  useMediaQuery: () => true,
}));

// The last step (Punteggi, index 2) is the one from which the form really submits. The step is read at
// render time, so a test can move the wizard (and `rerender`) to read a field on another step.
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

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: undefined }),
}));

vi.mock('@/components/features/game-picker', () => ({
  GamePicker: () => <div data-testid="play-record-game-stub" />,
}));

vi.mock('@/components/library/AddPrivateGameForm', () => ({
  AddPrivateGameForm: () => <div />,
}));

// ─── Helpers ──────────────────────────────────────────────────────────────────

const INITIAL_NOTES = 'Great match';
const TYPED_NOTES = 'Notes typed before saving';

function renderEditForm(onSubmit: () => unknown) {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const initialValues = {
    gameType: 'catalog' as const,
    gameName: 'Wingspan',
    sessionDate: new Date('2026-05-17T20:00:00.000Z'),
    notes: INITIAL_NOTES,
    location: 'Padova',
  };
  const initialPlayers = [{ id: 'p1', name: 'Anna', score: '42' }];
  // A new element each time: React skips `rerender` of the very same element object, and the step the
  // store mock answers is only read when the form renders.
  const tree = () => (
    <QueryClientProvider client={qc}>
      <SessionCreateForm
        mode="edit"
        onSubmit={onSubmit as never}
        onCancel={vi.fn()}
        initialValues={initialValues}
        initialPlayers={initialPlayers}
      />
    </QueryClientProvider>
  );
  const view = render(tree());
  return {
    ...view,
    /** Moves the wizard to another step and draws the form again, its state kept. */
    goToStep: (step: number) => {
      mockStep = step;
      view.rerender(tree());
    },
  };
}

/** Submits from the last step and lets the continuation of `handleFormSubmit` run. */
async function submit(container: HTMLElement, onSubmit: ReturnType<typeof vi.fn>) {
  fireEvent.submit(container.querySelector('form') as HTMLFormElement);
  await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
  // What follows `await onSubmit(...)` is a few microtasks away; a timer tick is after all of them.
  await act(async () => {
    await new Promise(resolve => setTimeout(resolve, 0));
  });
}

beforeEach(() => {
  mockResetSessionCreation.mockClear();
  mockStep = 2;
});

describe('SessionCreateForm — the answer of onSubmit (#4793)', () => {
  it('keeps the form as it is when onSubmit answers that nothing was saved', async () => {
    const onSubmit = vi.fn().mockResolvedValue(false);
    const { container } = renderEditForm(onSubmit);
    expect(screen.getByTestId('player-score-Anna')).toBeInTheDocument();

    await submit(container, onSubmit);

    expect(mockResetSessionCreation).not.toHaveBeenCalled();
    expect(screen.getByTestId('player-score-Anna')).toBeInTheDocument();
  });

  it('clears the form once onSubmit resolves without saying «not saved»', async () => {
    // The other direction: a check that also kept the form after a save that did go through would
    // leave the finished wizard on screen. `undefined` is what every caller answered until now.
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    const { container } = renderEditForm(onSubmit);

    await submit(container, onSubmit);

    expect(mockResetSessionCreation).toHaveBeenCalledTimes(1);
    expect(screen.queryByTestId('player-score-Anna')).not.toBeInTheDocument();
  });

  it('lets a retry through after a failed save', async () => {
    // Keeping the form is only worth something if it can be submitted again.
    const onSubmit = vi.fn().mockResolvedValueOnce(false).mockResolvedValueOnce(undefined);
    const { container } = renderEditForm(onSubmit);

    await submit(container, onSubmit);
    expect(mockResetSessionCreation).not.toHaveBeenCalled();

    fireEvent.submit(container.querySelector('form') as HTMLFormElement);
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(mockResetSessionCreation).toHaveBeenCalledTimes(1));
  });

  describe('the values typed in the editable fields', () => {
    // `resetSessionCreation` and the roster show that the wizard state survived; the fields have their own
    // state, reset by `form.reset()`. Step 2 holds the notes, so the wizard is moved there to type and to read.
    async function typeNotesThenSubmit(onSubmit: ReturnType<typeof vi.fn>) {
      mockStep = 1;
      const view = renderEditForm(onSubmit);
      fireEvent.change(
        screen.getByPlaceholderText(playRecordsNewMessages['step2.notesPlaceholder']),
        {
          target: { value: TYPED_NOTES },
        }
      );
      view.goToStep(2);
      await submit(view.container, onSubmit);
      view.goToStep(1);
      return screen.getByPlaceholderText(playRecordsNewMessages['step2.notesPlaceholder']);
    }

    it('are still in the field after a save that did not happen', async () => {
      const notes = await typeNotesThenSubmit(vi.fn().mockResolvedValue(false));

      expect(notes).toHaveValue(TYPED_NOTES);
    });

    it('go back to the values the form started with after a save that did happen', async () => {
      // The other direction, on the same path: without it the test above would pass for a form that
      // never resets anything.
      const notes = await typeNotesThenSubmit(vi.fn().mockResolvedValue(undefined));

      expect(notes).toHaveValue(INITIAL_NOTES);
    });
  });
});
