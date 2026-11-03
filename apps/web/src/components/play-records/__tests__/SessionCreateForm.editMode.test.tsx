/**
 * SessionCreateForm — «Annulla» of an edit and the draft of a new record (#4802).
 *
 * The edit form is the create form in another mode, so it shares the localStorage draft of a NEW record with
 * it. «Annulla» in edit used to delete that draft: the hook is switched off in edit (`enabled: false`), but
 * `clear()` removed the stored draft without looking at it.
 *
 * Real hook and real storage: the draft test of this form replaces the store with a double and never presses
 * «Annulla» from the edit mode.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { PLAY_RECORD_DRAFT_SCHEMA_VERSION } from '@/lib/play-records/draft-types';

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
