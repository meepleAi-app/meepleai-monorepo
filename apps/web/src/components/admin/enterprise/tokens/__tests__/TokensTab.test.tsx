/**
 * TokensTab Tests
 * Issue #3692 - Token Management System
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getTokenBalance: vi.fn().mockRejectedValue(new Error('Not available')),
      getTokenConsumption: vi.fn().mockRejectedValue(new Error('Not available')),
      getTokenTierUsage: vi.fn().mockRejectedValue(new Error('Not available')),
      getTopConsumers: vi.fn().mockRejectedValue(new Error('Not available')),
      addTokenCredits: vi.fn().mockResolvedValue(undefined),
    },
  },
}));

import { TokensTab } from '../TokensTab';

describe('TokensTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders tokens tab container', async () => {
    render(<TokensTab />);
    await waitFor(() => {
      expect(screen.getByTestId('tokens-tab')).toBeInTheDocument();
    });
  });

  it('renders action buttons', async () => {
    render(<TokensTab />);
    await waitFor(() => {
      expect(screen.getByTestId('refresh-tokens')).toBeInTheDocument();
      expect(screen.getByTestId('add-credits-btn')).toBeInTheDocument();
      expect(screen.getByTestId('export-report')).toBeInTheDocument();
    });
  });

  it('renders balance card with mock data', async () => {
    render(<TokensTab />);
    await waitFor(() => {
      expect(screen.getByTestId('token-balance-card')).toBeInTheDocument();
    });
  });

  it('renders consumption chart', async () => {
    render(<TokensTab />);
    await waitFor(() => {
      expect(screen.getByTestId('consumption-chart')).toBeInTheDocument();
    });
  });

  it('renders tier usage table', async () => {
    render(<TokensTab />);
    await waitFor(() => {
      expect(screen.getByTestId('tier-usage-table')).toBeInTheDocument();
    });
  });

  it('renders top consumers table', async () => {
    render(<TokensTab />);
    await waitFor(() => {
      expect(screen.getByTestId('top-consumers-table')).toBeInTheDocument();
    });
  });

  it('opens add credits modal', async () => {
    render(<TokensTab />);
    await waitFor(() => {
      expect(screen.getByTestId('add-credits-btn')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId('add-credits-btn'));
    expect(screen.getByTestId('add-credits-modal')).toBeInTheDocument();
  });

  it('shows error banner when all APIs fail', async () => {
    render(<TokensTab />);
    await waitFor(() => {
      expect(screen.getByTestId('tokens-error')).toBeInTheDocument();
    });
  });
});
