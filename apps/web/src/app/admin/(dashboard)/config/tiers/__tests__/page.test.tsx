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
    limits: {
      maxPrivateGames: 3,
      maxPdfUploadsPerMonth: 3,
      maxPdfSizeBytes: 10485760,
      maxAgents: 1,
      maxAgentQueriesPerDay: 20,
      maxSessionQueries: 10,
      maxSessionPlayers: 6,
      maxPhotosPerSession: 5,
      sessionSaveEnabled: true,
      maxCatalogProposalsPerWeek: 2,
    },
    llmModelTier: 'standard',
    isDefault: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
  },
  {
    id: '00000000-0000-0000-0000-000000000002',
    name: 'premium',
    displayName: 'Premium',
    limits: {
      maxPrivateGames: 15,
      maxPdfUploadsPerMonth: 15,
      maxPdfSizeBytes: 52428800,
      maxAgents: 10,
      maxAgentQueriesPerDay: 200,
      maxSessionQueries: 50,
      maxSessionPlayers: 12,
      maxPhotosPerSession: 20,
      sessionSaveEnabled: true,
      maxCatalogProposalsPerWeek: 10,
    },
    llmModelTier: 'premium',
    isDefault: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
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
    expect(screen.queryByTestId('field-name')).not.toBeInTheDocument();
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
          isDefault: true,
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
