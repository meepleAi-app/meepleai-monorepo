/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { MechanicGoldenClaimDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

const mockDeactivateMutate = vi.hoisted(() => vi.fn());

vi.mock('@/hooks/admin/useDeactivateGoldenClaim', () => ({
  useDeactivateGoldenClaim: () => ({
    mutate: mockDeactivateMutate,
    isPending: false,
  }),
}));

// The form has its own mocks in its own test; here we stub it so the edit
// dialog renders cleanly when opened.
vi.mock('@/components/admin/mechanic-extractor/validation/GoldenClaimForm', () => ({
  GoldenClaimForm: () => <div data-testid="golden-claim-form-stub" />,
}));

import { GoldenClaimsList } from '../GoldenClaimsList';

const SHARED_GAME_ID = '11111111-1111-1111-1111-111111111111';

const claims: MechanicGoldenClaimDto[] = [
  {
    id: '00000000-0000-0000-0000-000000000001',
    section: 'Mechanics',
    statement: 'Players take two actions on each turn.',
    expectedPage: 3,
    sourceQuote: 'Each turn, a player takes two actions.',
    keywords: [],
    createdAt: '2026-04-23T10:00:00Z',
  },
  {
    id: '00000000-0000-0000-0000-000000000002',
    section: 'Mechanics',
    statement: 'Actions can include moving, trading, or building.',
    expectedPage: 4,
    sourceQuote: 'Available actions: move, trade, build.',
    keywords: [],
    createdAt: '2026-04-23T10:00:00Z',
  },
  {
    id: '00000000-0000-0000-0000-000000000003',
    section: 'Victory',
    statement: 'The first player to 10 points wins the game.',
    expectedPage: 7,
    sourceQuote: 'When a player reaches 10 victory points, the game ends.',
    keywords: [],
    createdAt: '2026-04-23T10:00:00Z',
  },
];

function renderWithClient(ui: ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('GoldenClaimsList', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the empty state when there are no claims', () => {
    renderWithClient(<GoldenClaimsList sharedGameId={SHARED_GAME_ID} claims={[]} />);
    expect(screen.getByTestId('golden-claims-list')).toBeInTheDocument();
    expect(screen.getByText(/No golden claims yet/i)).toBeInTheDocument();
  });

  it('renders claims grouped by section', () => {
    renderWithClient(<GoldenClaimsList sharedGameId={SHARED_GAME_ID} claims={claims} />);

    // Section headings appear as <h3>
    const mechanicsHeading = screen.getByRole('heading', { name: /Mechanics/ });
    const victoryHeading = screen.getByRole('heading', { name: /Victory/ });

    expect(mechanicsHeading).toBeInTheDocument();
    expect(victoryHeading).toBeInTheDocument();

    // 2 Mechanics rows + 1 Victory row → 3 rows total in tbody
    const list = screen.getByTestId('golden-claims-list');
    const rows = within(list).getAllByRole('row');
    // header rows + body rows: 2 sections × 1 header each = 2 headers + 3 body = 5 rows
    expect(rows.length).toBe(5);
  });

  it('opens the AlertDialog when the deactivate button is clicked', async () => {
    const user = userEvent.setup();
    renderWithClient(<GoldenClaimsList sharedGameId={SHARED_GAME_ID} claims={claims} />);

    const deactivateButtons = screen.getAllByRole('button', { name: /Deactivate claim/i });
    await user.click(deactivateButtons[0]);

    expect(
      await screen.findByRole('alertdialog', { name: /Deactivate this golden claim/i })
    ).toBeInTheDocument();
  });

  it('calls useDeactivateGoldenClaim.mutate on confirm', async () => {
    const user = userEvent.setup();
    renderWithClient(<GoldenClaimsList sharedGameId={SHARED_GAME_ID} claims={claims} />);

    const deactivateButtons = screen.getAllByRole('button', { name: /Deactivate claim/i });
    await user.click(deactivateButtons[0]);

    const dialog = await screen.findByRole('alertdialog');
    const confirmButton = within(dialog).getByRole('button', { name: /^Deactivate$/i });
    await user.click(confirmButton);

    expect(mockDeactivateMutate).toHaveBeenCalledTimes(1);
    const [variables] = mockDeactivateMutate.mock.calls[0];
    expect(variables).toEqual({
      sharedGameId: SHARED_GAME_ID,
      claimId: claims[0].id,
    });
  });

  it('does not call mutate when Cancel is clicked', async () => {
    const user = userEvent.setup();
    renderWithClient(<GoldenClaimsList sharedGameId={SHARED_GAME_ID} claims={claims} />);

    const deactivateButtons = screen.getAllByRole('button', { name: /Deactivate claim/i });
    await user.click(deactivateButtons[0]);

    const dialog = await screen.findByRole('alertdialog');
    const cancelButton = within(dialog).getByRole('button', { name: /Cancel/i });
    await user.click(cancelButton);

    expect(mockDeactivateMutate).not.toHaveBeenCalled();
  });
});
