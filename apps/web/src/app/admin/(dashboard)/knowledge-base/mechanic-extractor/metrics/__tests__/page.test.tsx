/**
 * #532 ME-M2.3 metrics dashboard page tests.
 */
import { screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

const mockGetSummary = vi.hoisted(() => vi.fn());
const mockGetCostByDay = vi.hoisted(() => vi.fn());
const mockGetRecent = vi.hoisted(() => vi.fn());
const mockGetFilterOptions = vi.hoisted(() => vi.fn());
const mockExportCsv = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getMechanicMetricsSummary: mockGetSummary,
    getMechanicCostByDay: mockGetCostByDay,
    getMechanicRecentAnalyses: mockGetRecent,
    getMechanicMetricsFilterOptions: mockGetFilterOptions,
    exportMechanicAnalysesCsv: mockExportCsv,
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: class {},
  getApiBase: () => '',
}));

import MechanicMetricsPage from '../page';

describe('MechanicMetricsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetSummary.mockResolvedValue({
      totalCostUsd: 4.7,
      totalAnalyses: 4,
      publishedCount: 2,
      rejectedCount: 1,
      inReviewCount: 1,
      averageCostUsd: 1.18,
      averageReviewTimeHours: 2.3,
      approvalRatePct: 66.7,
      rejectionBreakdown: [{ reason: 'factual', count: 1 }],
    });
    mockGetCostByDay.mockResolvedValue([{ date: '2026-07-10', costUsd: 1, analysisCount: 1 }]);
    mockGetRecent.mockResolvedValue({
      items: [
        {
          id: 'a1',
          sharedGameId: 'g1',
          gameName: 'Catan',
          status: 2,
          reviewedBy: 'u1',
          reviewerName: 'Alice',
          createdAt: '2026-07-10T00:00:00Z',
          reviewedAt: '2026-07-10T02:00:00Z',
          estimatedCostUsd: 1.25,
        },
      ],
      totalCount: 1,
    });
    mockGetFilterOptions.mockResolvedValue({
      games: [{ id: 'g1', name: 'Catan' }],
      reviewers: [{ id: 'u1', name: 'Alice' }],
    });
    mockExportCsv.mockResolvedValue(new Blob(['x'], { type: 'text/csv' }));
  });

  it('renders KPI tiles, chart, rejection breakdown, and the recent table', async () => {
    renderWithQuery(<MechanicMetricsPage />);

    expect(await screen.findByText('67%')).toBeInTheDocument(); // approval rate 66.7 → 67%
    expect(screen.getByText('$1.18')).toBeInTheDocument(); // avg cost
    expect(screen.getByText('2.3 h')).toBeInTheDocument(); // avg review time
    expect(screen.getByTestId('mechanic-cost-chart')).toBeInTheDocument();

    expect(await screen.findByText('Catan')).toBeInTheDocument(); // recent table row
    expect(screen.getByTestId('rejection-breakdown')).toHaveTextContent('factual');
  });

  it('renders the three filter dropdowns', async () => {
    renderWithQuery(<MechanicMetricsPage />);
    await screen.findByText('67%');
    expect(screen.getByLabelText('Filtra per gioco')).toBeInTheDocument();
    expect(screen.getByLabelText('Filtra per reviewer')).toBeInTheDocument();
    expect(screen.getByLabelText('Filtra per status')).toBeInTheDocument();
  });

  it('triggers CSV export when the export button is clicked', async () => {
    const createUrl = vi.fn(() => 'blob:x');
    const revokeUrl = vi.fn();
    Object.defineProperty(URL, 'createObjectURL', { value: createUrl, configurable: true });
    Object.defineProperty(URL, 'revokeObjectURL', { value: revokeUrl, configurable: true });

    renderWithQuery(<MechanicMetricsPage />);
    await screen.findByText('67%');

    fireEvent.click(screen.getByTestId('export-csv'));

    await waitFor(() => expect(mockExportCsv).toHaveBeenCalledTimes(1));
  });
});
