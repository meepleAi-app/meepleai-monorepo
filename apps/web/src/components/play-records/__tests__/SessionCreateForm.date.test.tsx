/**
 * SessionCreateForm — step 2, a date field that is emptied (#4788).
 *
 * Emptying the `datetime-local` input makes `e.target.value` an empty string, and `new Date('')` is an
 * invalid date that is still `instanceof Date`. The field then wrote it back with `toISOString()`, which
 * throws «Invalid time value» during render. An empty field means «no date»: the field stays empty, the
 * step cannot be left until a date is chosen, and the form says so.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { playRecordsNewMessages } from '@/__tests__/fixtures/i18n-test-messages';

import { SessionCreateForm } from '../SessionCreateForm';

// ─── Mocks (same set as SessionCreateForm.timezone.test.tsx) ──────────────────

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

// Step 2 is the second step of the wizard; `nextStep` is observable.
const mockNextStep = vi.fn();
vi.mock('@/lib/stores/play-records-store', () => ({
  usePlayRecordsStore: () => ({
    sessionCreation: { currentStep: 1 },
    nextStep: mockNextStep,
    prevStep: vi.fn(),
    resetSessionCreation: vi.fn(),
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

function renderForm(props: Partial<React.ComponentProps<typeof SessionCreateForm>> = {}) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  render(
    <QueryClientProvider client={queryClient}>
      <SessionCreateForm onSubmit={vi.fn()} onCancel={vi.fn()} isSubmitting={false} {...props} />
    </QueryClientProvider>
  );
  return document.querySelector('input[type="datetime-local"]') as HTMLInputElement;
}

const clickNext = (): void => {
  fireEvent.click(screen.getByRole('button', { name: /avanti/i }));
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('SessionCreateForm — the date field emptied (#4788)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('stays empty instead of throwing «Invalid time value»', () => {
    const input = renderForm();
    expect(input.value).not.toBe('');

    fireEvent.change(input, { target: { value: '' } });

    expect(input).toHaveValue('');
  });

  it('cannot be left without a date, and says so', async () => {
    const input = renderForm();
    fireEvent.change(input, { target: { value: '' } });

    clickNext();

    expect(await screen.findByText('Scegli la data della partita')).toBeInTheDocument();
    expect(mockNextStep).not.toHaveBeenCalled();
  });

  it('takes the message away as soon as a date is chosen, without waiting for «Avanti»', async () => {
    // The form validates on «Avanti», not on change: left alone, the message would stay next to a filled
    // field until the next click.
    const input = renderForm();
    fireEvent.change(input, { target: { value: '' } });
    clickNext();
    await screen.findByText('Scegli la data della partita');

    fireEvent.change(input, { target: { value: '2026-10-30T20:00' } });

    await waitFor(() =>
      expect(screen.queryByText('Scegli la data della partita')).not.toBeInTheDocument()
    );
    expect(mockNextStep).not.toHaveBeenCalled();
  });

  it('a value that is not empty but is not a date either (a year of five digits) does not throw', () => {
    // A datetime-local accepts a year of more than four digits, and `new Date('20271-10-30T20:00')` is an
    // invalid date that is still `instanceof Date`: the field would have thrown again, as for `''`.
    const input = renderForm();

    expect(() => fireEvent.change(input, { target: { value: '20271-10-30T20:00' } })).not.toThrow();
    expect(document.querySelector('input[type="datetime-local"]')).not.toBeNull();
  });

  it('is the same when an existing record is edited', async () => {
    const input = renderForm({
      mode: 'edit',
      initialValues: {
        gameType: 'catalog',
        gameName: 'Wingspan',
        sessionDate: new Date('2026-10-30T20:00:00.000Z'),
        notes: '',
        location: '',
      },
    });
    expect(input.value).not.toBe('');

    fireEvent.change(input, { target: { value: '' } });
    clickNext();

    expect(input).toHaveValue('');
    expect(await screen.findByText('Scegli la data della partita')).toBeInTheDocument();
    expect(mockNextStep).not.toHaveBeenCalled();
  });

  it('lets the user move on again once a date is chosen', async () => {
    const input = renderForm();
    fireEvent.change(input, { target: { value: '' } });
    clickNext();
    await screen.findByText('Scegli la data della partita');

    fireEvent.change(input, { target: { value: '2026-10-30T20:00' } });
    clickNext();

    await waitFor(() => expect(mockNextStep).toHaveBeenCalledTimes(1));
    expect(screen.queryByText('Scegli la data della partita')).not.toBeInTheDocument();
  });
});
