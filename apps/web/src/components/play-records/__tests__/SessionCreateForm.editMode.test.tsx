/**
 * SessionCreateForm — what correcting a record leaves behind for the next new one (#4802).
 *
 * The edit form is the create form in another mode, so it shares two things with it: the wizard store (the
 * step) and the localStorage draft of a NEW record. Both used to cross over: «Annulla» in edit deleted the
 * draft of a record being created, and the step an edit was left on was still there for the next form.
 *
 * Real store and real hook: the other tests of this form replace the store with a double that keeps no
 * state, and a double like that cannot show a step that outlives the form it was set by.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { PLAY_RECORD_DRAFT_SCHEMA_VERSION } from '@/lib/play-records/draft-types';
import { usePlayRecordsStore } from '@/lib/stores/play-records-store';

import { SessionCreateForm } from '../SessionCreateForm';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@/lib/hooks/useMediaQuery', () => ({ useMediaQuery: () => true }));
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: { id: 'user-1' } }),
}));
vi.mock('@/components/features/game-picker', () => ({
  GamePicker: () => <div data-testid="play-record-game-stub" />,
}));
vi.mock('@/components/library/AddPrivateGameForm', () => ({
  AddPrivateGameForm: () => <div />,
}));

const DRAFT_KEY = 'meepleai:play-record-create-draft:user-1';
// The translation double answers the whole key, prefixed by the mode («playRecords.edit.actions.cancel»).
const CANCEL_LABEL = /actions\.cancel$/;

const stepInTheStore = (): number => usePlayRecordsStore.getState().sessionCreation.currentStep;

function seedDraftOfANewRecord(): string {
  const stored = JSON.stringify({
    savedAt: Date.now(),
    draft: {
      schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
      currentStep: 0,
      gameType: 'catalog',
      gameName: 'Catan',
      sessionDate: '2026-06-19T10:00:00.000Z',
      visibility: 'Private',
      enableScoring: false,
      scoringDimensions: [],
      dimensionUnits: {},
      notes: '',
      location: 'Verona',
      players: [],
    },
  });
  localStorage.setItem(DRAFT_KEY, stored);
  return stored;
}

function renderForm(mode: 'create' | 'edit') {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <SessionCreateForm
        mode={mode}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
        isSubmitting={false}
        initialValues={
          mode === 'edit'
            ? {
                gameType: 'catalog' as const,
                gameName: 'Wingspan',
                sessionDate: new Date('2026-05-17T20:00:00.000Z'),
                notes: '',
                location: 'Padova',
              }
            : undefined
        }
      />
    </QueryClientProvider>
  );
}

beforeEach(() => {
  localStorage.clear();
  usePlayRecordsStore.getState().resetSessionCreation();
});

describe('SessionCreateForm — «Annulla» and the draft of a new record (#4802)', () => {
  it('in edit leaves the draft of a new record where it was', () => {
    const stored = seedDraftOfANewRecord();
    renderForm('edit');

    fireEvent.click(screen.getByRole('button', { name: CANCEL_LABEL }));

    expect(localStorage.getItem(DRAFT_KEY)).toBe(stored);
  });

  it('in create still discards the draft, which is what «Annulla» is for', () => {
    // The other direction: the guard on `enabled` must not turn the button off where it belongs.
    seedDraftOfANewRecord();
    renderForm('create');

    fireEvent.click(screen.getByRole('button', { name: CANCEL_LABEL }));

    expect(localStorage.getItem(DRAFT_KEY)).toBeNull();
  });
});

describe('SessionCreateForm — the wizard step between one form and the next (#4802)', () => {
  it('an edit left on a later step does not leave the next new record there', () => {
    const { unmount } = renderForm('edit');
    act(() => usePlayRecordsStore.getState().nextStep());
    expect(stepInTheStore()).toBe(1);

    unmount();

    expect(stepInTheStore()).toBe(0);
  });

  it('an edit opens on the first step even if the store was left on another', () => {
    usePlayRecordsStore.getState().setSessionField('currentStep', 2);

    renderForm('edit');

    expect(stepInTheStore()).toBe(0);
  });

  it('a new record closed on a later step keeps it, so the draft can bring the user back', () => {
    // The step is reset where an edit comes and goes, not for every form: a reset when the NEW form is
    // unmounted would also wipe the step the draft restores, because StrictMode unmounts it once on mount.
    usePlayRecordsStore.getState().setSessionField('currentStep', 1);
    const { unmount } = renderForm('create');

    unmount();

    expect(stepInTheStore()).toBe(1);
  });
});
