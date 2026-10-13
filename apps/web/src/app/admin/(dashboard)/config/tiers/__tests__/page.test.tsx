/**
 * Admin Tiers Page — unit tests
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { EMPTY, LOADING, ERROR, ADMIN } from '../../../../../../__tests__/fixtures/test-strings';

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

    expect(screen.getByText(new RegExp(LOADING.tiers, 'i'))).toBeInTheDocument();
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
      expect(screen.getByText(EMPTY.tiers)).toBeInTheDocument();
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

    expect(screen.getByText(`${ADMIN.editTierPrefix}free`)).toBeInTheDocument();
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
      expect(screen.getByText(ERROR.tiersLoad)).toBeInTheDocument();
    });
  });

  /**
   * Issue #4212. The dialog built the limits from the keys it shows. A limit it did not show was
   * left out of the PUT, and the backend rebuilt it with its default: every save of a plan zeroed
   * the gamebook translation quota and switched the RAPTOR rebuild off.
   */
  describe('saving a plan keeps the limits it carries', () => {
    const premium = {
      ...mockTiers[1],
      limits: {
        ...mockTiers[1].limits,
        maxGamebookTranslationsPerMonth: 500,
        maxLibraryGames: 50,
        raptorRebuildEnabled: true,
      },
    };

    beforeEach(() => {
      mockGetTiers.mockResolvedValue([premium]);
      mockUpdateTier.mockResolvedValue(premium);
    });

    async function openAndSave(edit?: () => Promise<void>) {
      renderWithQuery(<AdminTiersPage />);
      await waitFor(() => expect(screen.getByTestId('btn-edit-premium')).toBeInTheDocument());
      await user.click(screen.getByTestId('btn-edit-premium'));
      if (edit) await edit();
      await user.click(screen.getByTestId('btn-save'));
      await waitFor(() => expect(mockUpdateTier).toHaveBeenCalled());
      return mockUpdateTier.mock.calls[0][1].limits;
    }

    it('sends back the gamebook quota, the library limit and the RAPTOR flag untouched', async () => {
      const sent = await openAndSave();

      expect(sent).toMatchObject({
        maxGamebookTranslationsPerMonth: 500,
        maxLibraryGames: 50,
        raptorRebuildEnabled: true,
      });
    });

    it('lets the admin change the library limit', async () => {
      const sent = await openAndSave(async () => {
        const field = screen.getByTestId('field-limit-maxLibraryGames');
        await user.clear(field);
        await user.type(field, '60');
      });

      expect(sent.maxLibraryGames).toBe(60);
    });

    it('lets the admin switch the RAPTOR rebuild off, and only then sends false', async () => {
      const sent = await openAndSave(async () => {
        await user.click(screen.getByTestId('field-raptorRebuildEnabled'));
      });

      expect(sent.raptorRebuildEnabled).toBe(false);
    });
  });

  /**
   * Issue #4212. The backend refuses a library limit, or a player count, below 1. The dialog
   * offered 0 for both: a new plan started at 0 and the first save was a 400.
   */
  describe('limits that cannot be zero', () => {
    const field = (key: string) => screen.getByTestId(`field-limit-${key}`) as HTMLInputElement;

    it('start a new plan at their minimum', async () => {
      renderWithQuery(<AdminTiersPage />);
      await waitFor(() => expect(screen.getByTestId('tier-row-free')).toBeInTheDocument());
      await user.click(screen.getByTestId('btn-create-tier'));

      expect(field('maxLibraryGames').value).toBe('1');
      expect(field('maxLibraryGames').min).toBe('1');
      expect(field('maxSessionPlayers').value).toBe('1');
      expect(field('maxSessionPlayers').min).toBe('1');
      // A limit that may be zero keeps starting, and bottoming out, at zero.
      expect(field('maxPrivateGames').value).toBe('0');
      expect(field('maxPrivateGames').min).toBe('0');
    });

    it('start from the minimum when the payload has no library limit', async () => {
      // mockTiers carry no maxLibraryGames: a backend that does not send it yet.
      renderWithQuery(<AdminTiersPage />);
      await waitFor(() => expect(screen.getByTestId('btn-edit-free')).toBeInTheDocument());
      await user.click(screen.getByTestId('btn-edit-free'));

      expect(field('maxLibraryGames').value).toBe('1');
    });
  });

  /**
   * Issue #4255. The admin shell scopes a dark theme to its container but does not set the text
   * colour, so text with no colour class of its own inherits the dark text of the light theme:
   * the title and every cell of this table were dark on dark. Until the shell is fixed the page
   * declares its own colours. jsdom does not compute Tailwind, so this asserts the declaration;
   * the rendered contrast is measured in the browser (see the PR).
   */
  it('declares its text and border colours instead of inheriting them', async () => {
    renderWithQuery(<AdminTiersPage />);
    await waitFor(() => expect(screen.getByTestId('tiers-table')).toBeInTheDocument());

    expect(screen.getByText('Tier Management').closest('.text-foreground')).not.toBeNull();
    expect(screen.getByTestId('tiers-table').closest('.text-foreground')).not.toBeNull();
    expect(screen.getByTestId('tiers-table').parentElement).toHaveClass('border-border');
  });
});
