/**
 * Admin Tiers Page — unit tests
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

const mockGetTiers = vi.hoisted(() => vi.fn());
const mockCreateTier = vi.hoisted(() => vi.fn());
const mockUpdateTier = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    tiers: {
      getTiers: mockGetTiers,
      createTier: mockCreateTier,
      updateTier: mockUpdateTier,
    },
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import AdminTiersPage from '../page';

const mockTiers = [
  {
    id: '00000000-0000-0000-0000-000000000001',
    name: 'free',
    displayName: 'Free',
    description: 'Piano gratuito',
    monthlyPriceEur: 0,
    isActive: true,
    limits: [
      { key: 'MaxPrivateGames', value: 3 },
      { key: 'MaxPdfPerMonth', value: 3 },
      { key: 'MaxAgents', value: 1 },
      { key: 'MaxAgentQueriesPerDay', value: 20 },
      { key: 'MaxSessionQueries', value: 10 },
      { key: 'MaxPlayersPerSession', value: 6 },
      { key: 'MaxPhotosPerSession', value: 5 },
      { key: 'MaxCatalogProposalsPerWeek', value: 2 },
    ],
  },
  {
    id: '00000000-0000-0000-0000-000000000002',
    name: 'premium',
    displayName: 'Premium',
    description: null,
    monthlyPriceEur: 4.99,
    isActive: true,
    limits: [
      { key: 'MaxPrivateGames', value: 15 },
      { key: 'MaxPdfPerMonth', value: 15 },
      { key: 'MaxAgents', value: 10 },
      { key: 'MaxAgentQueriesPerDay', value: 200 },
      { key: 'MaxSessionQueries', value: 50 },
      { key: 'MaxPlayersPerSession', value: 12 },
      { key: 'MaxPhotosPerSession', value: 20 },
      { key: 'MaxCatalogProposalsPerWeek', value: 10 },
    ],
  },
];

describe('AdminTiersPage', () => {
  const user = userEvent.setup();

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetTiers.mockResolvedValue(mockTiers);
  });

  it('renders heading and create button', () => {
    renderWithQuery(<AdminTiersPage />);

    expect(screen.getByText('Tier Management')).toBeInTheDocument();
    expect(screen.getByTestId('btn-create-tier')).toBeInTheDocument();
  });

  it('renders loading state initially', () => {
    mockGetTiers.mockReturnValue(new Promise(() => {})); // never resolves
    renderWithQuery(<AdminTiersPage />);

    expect(screen.getByText(/caricamento tier/i)).toBeInTheDocument();
  });

  it('renders tier rows after loading', async () => {
    renderWithQuery(<AdminTiersPage />);

    await waitFor(() => {
      expect(screen.getByTestId('tier-row-free')).toBeInTheDocument();
    });

    expect(screen.getByTestId('tier-row-premium')).toBeInTheDocument();
    expect(screen.getByText('Free')).toBeInTheDocument();
    expect(screen.getByText('Premium')).toBeInTheDocument();
  });

  it('shows empty state when no tiers', async () => {
    mockGetTiers.mockResolvedValue([]);
    renderWithQuery(<AdminTiersPage />);

    await waitFor(() => {
      expect(screen.getByText('Nessun tier trovato.')).toBeInTheDocument();
    });
  });

  it('opens create dialog when Nuovo Tier is clicked', async () => {
    renderWithQuery(<AdminTiersPage />);

    await waitFor(() => {
      expect(screen.getByTestId('tier-row-free')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('btn-create-tier'));

    expect(screen.getByText('Nuovo tier')).toBeInTheDocument();
    expect(screen.getByTestId('field-name')).toBeInTheDocument();
  });

  it('opens edit dialog with tier name prefilled', async () => {
    renderWithQuery(<AdminTiersPage />);

    await waitFor(() => {
      expect(screen.getByTestId('btn-edit-free')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('btn-edit-free'));

    expect(screen.getByText('Modifica tier: free')).toBeInTheDocument();
    // Name field should NOT be shown for edits
    expect(screen.queryByTestId('field-name')).not.toBeInTheDocument();
    // Display name should be prefilled
    const displayField = screen.getByTestId('field-displayName') as HTMLInputElement;
    expect(displayField.value).toBe('Free');
  });

  it('calls updateTier and closes dialog on save', async () => {
    mockUpdateTier.mockResolvedValue({ ...mockTiers[0], displayName: 'Free Updated' });

    renderWithQuery(<AdminTiersPage />);

    await waitFor(() => {
      expect(screen.getByTestId('btn-edit-free')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('btn-edit-free'));
    await user.click(screen.getByTestId('btn-save'));

    await waitFor(() => {
      expect(mockUpdateTier).toHaveBeenCalledWith(
        'free',
        expect.objectContaining({
          displayName: 'Free',
          isActive: true,
        })
      );
    });
  });

  it('shows error state when getTiers fails', async () => {
    mockGetTiers.mockRejectedValue(new Error('Network error'));

    renderWithQuery(<AdminTiersPage />);

    await waitFor(() => {
      expect(screen.getByText('Errore nel caricamento dei tier.')).toBeInTheDocument();
    });
  });
});
