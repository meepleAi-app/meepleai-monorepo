import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

vi.mock('@/components/admin/charts/ChartsSection', () => ({
  ChartsSection: () => <div data-testid="charts-section" />,
}));

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getOverviewStats: vi.fn().mockResolvedValue({
        totalUsers: 0,
        activeUsers: 0,
        publishedGames: 0,
        recentSubmissions: 0,
      }),
    },
  },
}));

import { OverviewTab } from '../OverviewTab';

describe('OverviewTab', () => {
  it('renders heading', () => {
    renderWithQuery(<OverviewTab />);
    expect(screen.getByText('Panoramica Analitiche')).toBeInTheDocument();
  });

  it('renders ChartsSection component', () => {
    renderWithQuery(<OverviewTab />);
    expect(screen.getByTestId('charts-section')).toBeInTheDocument();
  });
});
