/**
 * UsageWidget — unit tests
 */

import { screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

const mockGetMyUsage = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    tiers: {
      getMyUsage: mockGetMyUsage,
    },
  },
}));

import { UsageWidget } from '../UsageWidget';

const mockUsage = {
  privateGames: 2,
  privateGamesMax: 3,
  pdfThisMonth: 1,
  pdfThisMonthMax: 3,
  agentQueriesToday: 15,
  agentQueriesTodayMax: 20,
  sessionQueries: 4,
  sessionQueriesMax: 10,
  agents: 1,
  agentsMax: 1,
  photosThisSession: 2,
  photosThisSessionMax: 5,
  sessionSaveEnabled: false,
  catalogProposalsThisWeek: 0,
  catalogProposalsThisWeekMax: 2,
};

describe('UsageWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetMyUsage.mockResolvedValue(mockUsage);
  });

  it('renders the widget with usage data', async () => {
    renderWithQuery(<UsageWidget tier="free" />);

    await waitFor(() => {
      expect(screen.getByTestId('usage-widget')).toBeInTheDocument();
    });

    expect(screen.getByText('Il tuo piano')).toBeInTheDocument();
  });

  it('shows upgrade CTA for free tier', async () => {
    renderWithQuery(<UsageWidget tier="free" />);

    await waitFor(() => {
      expect(screen.getByTestId('upgrade-cta')).toBeInTheDocument();
    });

    expect(screen.getByTestId('upgrade-cta')).toHaveAttribute('href', '/pricing');
  });

  it('does not show upgrade CTA for premium tier', async () => {
    renderWithQuery(<UsageWidget tier="premium" />);

    await waitFor(() => {
      expect(screen.getByTestId('usage-widget')).toBeInTheDocument();
    });

    expect(screen.queryByTestId('upgrade-cta')).not.toBeInTheDocument();
  });

  it('shows quota rows for main limits', async () => {
    renderWithQuery(<UsageWidget tier="free" />);

    await waitFor(() => {
      expect(screen.getByTestId('quota-Giochi privati')).toBeInTheDocument();
    });

    expect(screen.getByTestId('quota-PDF questo mese')).toBeInTheDocument();
    expect(screen.getByTestId('quota-Query oggi')).toBeInTheDocument();
    expect(screen.getByTestId('quota-Agent')).toBeInTheDocument();
    expect(screen.getByTestId('quota-Query sessione')).toBeInTheDocument();
  });

  it('shows session save status', async () => {
    renderWithQuery(<UsageWidget tier="free" />);

    await waitFor(() => {
      expect(screen.getByText('Salvataggio sessione')).toBeInTheDocument();
    });

    // sessionSaveEnabled is false in mockUsage
    expect(screen.getByText('✗ Non disponibile')).toBeInTheDocument();
  });

  it('shows enabled session save when true', async () => {
    mockGetMyUsage.mockResolvedValue({ ...mockUsage, sessionSaveEnabled: true });
    renderWithQuery(<UsageWidget tier="premium" />);

    await waitFor(() => {
      expect(screen.getByText('✓ Abilitato')).toBeInTheDocument();
    });
  });

  it('shows skeleton while loading', () => {
    mockGetMyUsage.mockReturnValue(new Promise(() => {})); // never resolves
    renderWithQuery(<UsageWidget tier="free" />);

    // No widget yet — just skeleton markup (no data-testid on skeleton)
    expect(screen.queryByTestId('usage-widget')).not.toBeInTheDocument();
  });

  it('shows error fallback on fetch failure', async () => {
    mockGetMyUsage.mockRejectedValue(new Error('Network error'));
    renderWithQuery(<UsageWidget tier="free" />);

    await waitFor(() => {
      expect(screen.getByText('Impossibile caricare i dati di utilizzo.')).toBeInTheDocument();
    });
  });

  it('shows ∞ for unlimited quotas', async () => {
    mockGetMyUsage.mockResolvedValue({
      ...mockUsage,
      privateGamesMax: 2_147_483_647,
    });
    renderWithQuery(<UsageWidget tier="premium" />);

    await waitFor(() => {
      expect(screen.getByTestId('usage-widget')).toBeInTheDocument();
    });

    // The quota row for unlimited shows ∞ inline
    const row = screen.getByTestId('quota-Giochi privati');
    expect(row).toHaveTextContent('∞');
  });
});
