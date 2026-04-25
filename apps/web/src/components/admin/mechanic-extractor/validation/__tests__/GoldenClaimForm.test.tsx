/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { MechanicGoldenClaimDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

const mockCreateMutate = vi.hoisted(() => vi.fn());
const mockUpdateMutate = vi.hoisted(() => vi.fn());

vi.mock('@/hooks/admin/useCreateGoldenClaim', () => ({
  useCreateGoldenClaim: () => ({
    mutate: mockCreateMutate,
    isPending: false,
    isError: false,
    isSuccess: false,
  }),
}));

vi.mock('@/hooks/admin/useUpdateGoldenClaim', () => ({
  useUpdateGoldenClaim: () => ({
    mutate: mockUpdateMutate,
    isPending: false,
    isError: false,
    isSuccess: false,
  }),
}));

import { GoldenClaimForm } from '../GoldenClaimForm';

const SHARED_GAME_ID = '11111111-1111-1111-1111-111111111111';
const CLAIM_ID = '22222222-2222-2222-2222-222222222222';

function renderWithClient(ui: ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('GoldenClaimForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders all fields in create mode', () => {
    renderWithClient(<GoldenClaimForm sharedGameId={SHARED_GAME_ID} mode="create" />);

    expect(screen.getByLabelText(/Section/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Statement/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Expected page/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Source quote/)).toBeInTheDocument();
    expect(screen.queryByLabelText(/Display order/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Create claim/i })).toBeInTheDocument();
  });

  it('submits valid data via useCreateGoldenClaim', async () => {
    const user = userEvent.setup();
    renderWithClient(<GoldenClaimForm sharedGameId={SHARED_GAME_ID} mode="create" />);

    await user.type(screen.getByLabelText(/Statement/), 'Players draw two cards each turn.');
    const expectedPageInput = screen.getByLabelText(/Expected page/);
    await user.clear(expectedPageInput);
    await user.type(expectedPageInput, '5');
    await user.type(
      screen.getByLabelText(/Source quote/),
      'On your turn, draw two cards from the deck.'
    );

    await user.click(screen.getByRole('button', { name: /Create claim/i }));

    expect(mockCreateMutate).toHaveBeenCalledTimes(1);
    const [variables] = mockCreateMutate.mock.calls[0];
    expect(variables).toMatchObject({
      sharedGameId: SHARED_GAME_ID,
      request: {
        sharedGameId: SHARED_GAME_ID,
        section: 'Mechanics',
        statement: 'Players draw two cards each turn.',
        expectedPage: 5,
        sourceQuote: 'On your turn, draw two cards from the deck.',
      },
    });
  });

  it('shows validation error when statement is too short', async () => {
    const user = userEvent.setup();
    renderWithClient(<GoldenClaimForm sharedGameId={SHARED_GAME_ID} mode="create" />);

    await user.type(screen.getByLabelText(/Statement/), 'short');
    await user.type(
      screen.getByLabelText(/Source quote/),
      'A long enough source quote that passes validation.'
    );

    await user.click(screen.getByRole('button', { name: /Create claim/i }));

    expect(await screen.findByText(/at least 10 characters/i)).toBeInTheDocument();
    expect(mockCreateMutate).not.toHaveBeenCalled();
  });

  it('shows validation error when expectedPage is below 1', async () => {
    const user = userEvent.setup();
    renderWithClient(<GoldenClaimForm sharedGameId={SHARED_GAME_ID} mode="create" />);

    await user.type(screen.getByLabelText(/Statement/), 'Players take two actions on each turn.');
    await user.type(
      screen.getByLabelText(/Source quote/),
      'A long enough source quote that passes validation.'
    );
    const expectedPageInput = screen.getByLabelText(/Expected page/);
    await user.clear(expectedPageInput);
    await user.type(expectedPageInput, '0');

    await user.click(screen.getByRole('button', { name: /Create claim/i }));

    expect(await screen.findByText(/at least 1/i)).toBeInTheDocument();
    expect(mockCreateMutate).not.toHaveBeenCalled();
  });

  it('prefills fields from initialClaim in edit mode and submits via useUpdateGoldenClaim', async () => {
    const user = userEvent.setup();
    const initial: MechanicGoldenClaimDto = {
      id: CLAIM_ID,
      section: 'Victory',
      statement: 'The first player to 10 points wins the game.',
      expectedPage: 7,
      sourceQuote: 'When a player reaches 10 victory points, the game ends.',
      keywords: [],
      createdAt: '2026-04-23T10:00:00Z',
    };

    renderWithClient(
      <GoldenClaimForm
        sharedGameId={SHARED_GAME_ID}
        mode="edit"
        initialClaim={initial}
        claimId={CLAIM_ID}
      />
    );

    expect(screen.getByDisplayValue(initial.statement)).toBeInTheDocument();
    expect(screen.getByDisplayValue(String(initial.expectedPage))).toBeInTheDocument();
    expect(screen.getByDisplayValue(initial.sourceQuote)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Save changes/i }));

    expect(mockUpdateMutate).toHaveBeenCalledTimes(1);
    const [variables] = mockUpdateMutate.mock.calls[0];
    expect(variables).toMatchObject({
      sharedGameId: SHARED_GAME_ID,
      claimId: CLAIM_ID,
      request: {
        statement: initial.statement,
        expectedPage: initial.expectedPage,
        sourceQuote: initial.sourceQuote,
      },
    });
  });
});
