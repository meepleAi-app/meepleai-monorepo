// apps/web/src/components/play-records/__tests__/SessionCreateForm.draft.test.tsx
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { SessionCreateForm } from '../SessionCreateForm';
import { PLAY_RECORD_DRAFT_SCHEMA_VERSION } from '@/lib/play-records/draft-types';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));
const mockUseMediaQuery = vi.fn(() => true); // mobile
vi.mock('@/lib/hooks/useMediaQuery', () => ({
  useMediaQuery: () => mockUseMediaQuery(),
}));
const mockSetSessionField = vi.fn();
let mockCurrentStep = 0;
vi.mock('@/lib/stores/play-records-store', () => ({
  usePlayRecordsStore: () => ({
    sessionCreation: { currentStep: mockCurrentStep },
    nextStep: vi.fn(),
    prevStep: vi.fn(),
    resetSessionCreation: vi.fn(),
    setSessionField: mockSetSessionField,
  }),
}));
vi.mock('@/components/play-records/GameCombobox', () => ({
  GameCombobox: () => <div data-testid="game-combobox" />,
}));
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: { id: 'user-1' } }),
}));

const KEY = 'meepleai:play-record-create-draft:user-1';

function wrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

const props = { onSubmit: vi.fn(), onCancel: vi.fn(), isSubmitting: false };

beforeEach(() => {
  localStorage.clear();
  mockCurrentStep = 0;
  vi.clearAllMocks();
});
afterEach(() => vi.useRealTimers());

describe('SessionCreateForm — draft persistence wiring', () => {
  it('AC-A3: restores a persisted draft into the form on mount (no prefill)', () => {
    mockCurrentStep = 1; // Step 2 "Quando" — location field visible
    localStorage.setItem(
      KEY,
      JSON.stringify({
        savedAt: Date.now(),
        draft: {
          schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
          currentStep: 1,
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
      })
    );
    render(<SessionCreateForm {...props} />, { wrapper: wrapper() });
    expect(screen.getByDisplayValue('Verona')).toBeInTheDocument();
    expect(mockSetSessionField).toHaveBeenCalledWith('currentStep', 1);
  });

  it('clamps an out-of-range restored draft step to the last valid step (2)', () => {
    // A corrupted/stale draft must never open the wizard on an invalid step
    // index (would render a broken StepIndicator + undefined STEP_FIELDS).
    mockCurrentStep = 0;
    localStorage.setItem(
      KEY,
      JSON.stringify({
        savedAt: Date.now(),
        draft: {
          schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
          currentStep: 99, // out of range
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
      })
    );
    render(<SessionCreateForm {...props} />, { wrapper: wrapper() });
    expect(mockSetSessionField).toHaveBeenCalledWith('currentStep', 2);
    expect(mockSetSessionField).not.toHaveBeenCalledWith('currentStep', 99);
  });

  it('AC-A3: does NOT restore when initialValues (gameNight prefill) is present', () => {
    mockCurrentStep = 1;
    localStorage.setItem(
      KEY,
      JSON.stringify({
        savedAt: Date.now(),
        draft: {
          schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
          currentStep: 1,
          gameType: 'catalog',
          gameName: 'Catan',
          sessionDate: '2026-06-19T10:00:00.000Z',
          visibility: 'Private',
          enableScoring: false,
          scoringDimensions: [],
          dimensionUnits: {},
          location: 'Verona',
          players: [],
        },
      })
    );
    render(
      <SessionCreateForm {...props} initialValues={{ gameName: 'Brass', location: 'Bologna' }} />,
      { wrapper: wrapper() }
    );
    expect(screen.getByDisplayValue('Bologna')).toBeInTheDocument();
    expect(screen.queryByDisplayValue('Verona')).toBeNull();
  });

  it('AC-A1: autosaves to localStorage after editing a field (debounced)', () => {
    vi.useFakeTimers();
    mockCurrentStep = 1;
    render(<SessionCreateForm {...props} />, { wrapper: wrapper() });
    // AC-A1: pristine form must NOT persist before any edit (first-run skip).
    expect(localStorage.getItem(KEY)).toBeNull();
    const location = screen.getByLabelText('playRecords.new.step2.locationLabel');
    fireEvent.change(location, { target: { value: 'Milano' } });
    act(() => vi.advanceTimersByTime(800));
    const raw = localStorage.getItem(KEY);
    expect(raw).not.toBeNull();
    expect(JSON.parse(raw as string).draft.location).toBe('Milano');
  });

  it('AC-A6: clears the draft on successful submit', () => {
    vi.useFakeTimers();
    mockCurrentStep = 2; // Step 3 — submit button present
    localStorage.setItem(KEY, JSON.stringify({ savedAt: Date.now(), draft: {} }));
    render(<SessionCreateForm {...props} />, { wrapper: wrapper() });
    const saveBtn = screen.getByRole('button', { name: /actions\.save/i });
    fireEvent.click(saveBtn);
    act(() => vi.advanceTimersByTime(0));
    expect(localStorage.getItem(KEY)).toBeNull();
  });
});
