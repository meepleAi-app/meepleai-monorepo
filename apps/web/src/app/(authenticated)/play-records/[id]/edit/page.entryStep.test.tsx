/**
 * EditPlayRecordPage — the wizard opens on its first step, whatever step a new record was left on (#4842).
 *
 * The wizard's step lives in a module-level store that the create page and the edit page share. The edit page
 * resets it when it unmounts (#4808), but the create page does not: leaving a new record half done and then
 * opening an edit by a client-side navigation used to start the edit on the step the creation was left on.
 *
 * Here the store, the page and the form are the real ones (`page.conflictExit.test.tsx` replaces the store with
 * a double, which cannot tell a step the page set from a step it was handed). Replaced: the data hooks, the
 * router and the picker widgets the form draws.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  playRecordsEditMessages,
  playRecordsNewMessages,
} from '@/__tests__/fixtures/i18n-test-messages';
import { usePlayRecordsStore } from '@/lib/stores/play-records-store';

import EditPlayRecordPage from './page';

// ─── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => {
      const bare = key.replace(/^playRecords\.(new|edit)\./, '');
      const edit = key.startsWith('playRecords.edit.')
        ? (playRecordsEditMessages as Record<string, string>)[bare]
        : undefined;
      return edit ?? (playRecordsNewMessages as Record<string, string>)[bare] ?? key;
    },
  }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  useParams: () => ({ id: 'record-123' }),
}));

vi.mock('@/lib/domain-hooks/usePlayRecords', () => ({
  usePlayRecord: () => ({ data: RECORD, isLoading: false, error: null }),
  useUpdateRecord: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useDeleteRecord: () => ({ mutateAsync: vi.fn(), isPending: false }),
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
  players: [],
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

// ─── Helpers ──────────────────────────────────────────────────────────────────

/** 0-based index of the step the indicator marks as the current one. */
function shownStep(): number {
  const circles = Array.from(
    screen.getByTestId('step-indicator').querySelectorAll('span.border-2')
  );
  return circles.findIndex(circle => circle.getAttribute('aria-current') === 'step');
}

function renderPage() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <EditPlayRecordPage />
    </QueryClientProvider>
  );
}

beforeEach(() => {
  usePlayRecordsStore.getState().resetSessionCreation();
});

describe('EditPlayRecordPage — the step it opens on (#4842)', () => {
  it('opens on the first step after a new record was left on a later one', () => {
    // What leaving the create wizard half done (no «Annulla», no unmount reset) leaves in the store.
    usePlayRecordsStore.getState().setSessionField('currentStep', 2);

    renderPage();

    expect(shownStep()).toBe(0);
    expect(usePlayRecordsStore.getState().sessionCreation.currentStep).toBe(0);
  });

  it('keeps what the creation had typed: only the step is brought back', () => {
    const store = usePlayRecordsStore.getState();
    store.setSessionField('currentStep', 2);
    store.setSessionField('gameName', 'Catan, not yet in the draft');

    renderPage();

    // `resetSessionCreation()` here would erase the half-done creation before its autosave (800 ms debounce) saw it.
    expect(usePlayRecordsStore.getState().sessionCreation.gameName).toBe(
      'Catan, not yet in the draft'
    );
  });

  it('still lets the user move on from the first step', async () => {
    usePlayRecordsStore.getState().setSessionField('currentStep', 2);
    renderPage();

    await userEvent.setup().click(screen.getByRole('button', { name: /Avanti/ }));

    expect(shownStep()).toBe(1);
  });

  it('opens on the first step when the store was already at the start', () => {
    renderPage();

    expect(shownStep()).toBe(0);
  });
});
