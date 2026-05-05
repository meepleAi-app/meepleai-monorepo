/**
 * @vitest-environment jsdom
 *
 * Unit tests for {@link MechanicAnalysesListCard} (spec-panel gap #2).
 *
 * Verifies discovery list rendering, suppressed-row badge, row-click
 * delegation to `onSelect`, pagination guard rails and empty/error states.
 * The admin client is fully mocked — we never touch the network from a
 * jsdom environment.
 */

import { fireEvent, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

const mockListMechanicAnalyses = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    listMechanicAnalyses: mockListMechanicAnalyses,
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn(() => ({})),
}));

import { MechanicAnalysesListCard } from '../MechanicAnalysesListCard';

const ROW_CARCASSONNE = {
  id: '11111111-1111-1111-1111-111111111111',
  sharedGameId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  gameTitle: 'Carcassonne',
  pdfDocumentId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
  promptVersion: 'v1.0.0',
  status: 2, // Published
  claimsCount: 18,
  totalTokensUsed: 4200,
  estimatedCostUsd: 0.42,
  certificationStatus: 1,
  isSuppressed: false,
  createdAt: '2026-04-25T10:00:00Z',
};

const ROW_SUPPRESSED = {
  id: '22222222-2222-2222-2222-222222222222',
  sharedGameId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
  gameTitle: '7 Wonders',
  pdfDocumentId: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
  promptVersion: 'v1.0.0',
  status: 1, // InReview
  claimsCount: 9,
  totalTokensUsed: 3100,
  estimatedCostUsd: 0.31,
  certificationStatus: 0,
  isSuppressed: true,
  createdAt: '2026-04-24T09:00:00Z',
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe('MechanicAnalysesListCard', () => {
  it('renders the loading state initially', () => {
    mockListMechanicAnalyses.mockReturnValue(new Promise(() => {})); // never resolves

    renderWithQuery(<MechanicAnalysesListCard onSelect={vi.fn()} />);

    expect(screen.getByText(/Loading analyses/i)).toBeInTheDocument();
  });

  it('renders rows with game title, status badge and claims count', async () => {
    mockListMechanicAnalyses.mockResolvedValue({
      items: [ROW_CARCASSONNE, ROW_SUPPRESSED],
      page: 1,
      pageSize: 20,
      totalCount: 2,
    });

    renderWithQuery(<MechanicAnalysesListCard onSelect={vi.fn()} />);

    await waitFor(() => {
      expect(screen.getByText('Carcassonne')).toBeInTheDocument();
    });

    expect(screen.getByText('7 Wonders')).toBeInTheDocument();
    // Status labels (Published / In Review) — the labels object exposes them
    expect(screen.getByText(/Published/i)).toBeInTheDocument();
    expect(screen.getByText(/In Review/i)).toBeInTheDocument();
    // Claims count appears in its own cell
    expect(screen.getByText('18')).toBeInTheDocument();
    expect(screen.getByText('9')).toBeInTheDocument();
  });

  it('renders the Suppressed badge for suppressed rows only', async () => {
    mockListMechanicAnalyses.mockResolvedValue({
      items: [ROW_CARCASSONNE, ROW_SUPPRESSED],
      page: 1,
      pageSize: 20,
      totalCount: 2,
    });

    renderWithQuery(<MechanicAnalysesListCard onSelect={vi.fn()} />);

    await waitFor(() => {
      expect(
        screen.getByTestId(`mechanic-analyses-list-suppressed-${ROW_SUPPRESSED.id}`)
      ).toBeInTheDocument();
    });

    // Carcassonne row must NOT carry the suppressed badge
    expect(
      screen.queryByTestId(`mechanic-analyses-list-suppressed-${ROW_CARCASSONNE.id}`)
    ).not.toBeInTheDocument();
  });

  it('calls onSelect with the row id when a row is clicked', async () => {
    mockListMechanicAnalyses.mockResolvedValue({
      items: [ROW_CARCASSONNE],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });

    const onSelect = vi.fn();
    renderWithQuery(<MechanicAnalysesListCard onSelect={onSelect} />);

    const row = await screen.findByTestId(`mechanic-analyses-list-row-${ROW_CARCASSONNE.id}`);
    fireEvent.click(row);

    expect(onSelect).toHaveBeenCalledWith(ROW_CARCASSONNE.id);
  });

  it('renders the empty state when there are no analyses', async () => {
    mockListMechanicAnalyses.mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
    });

    renderWithQuery(<MechanicAnalysesListCard onSelect={vi.fn()} />);

    await waitFor(() => {
      expect(screen.getByText(/No mechanic analyses yet/i)).toBeInTheDocument();
    });
  });

  it('hides pagination when totalCount fits in one page', async () => {
    mockListMechanicAnalyses.mockResolvedValue({
      items: [ROW_CARCASSONNE],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });

    renderWithQuery(<MechanicAnalysesListCard onSelect={vi.fn()} />);

    await screen.findByText('Carcassonne');
    expect(screen.queryByTestId('mechanic-analyses-list-prev')).not.toBeInTheDocument();
    expect(screen.queryByTestId('mechanic-analyses-list-next')).not.toBeInTheDocument();
  });

  it('shows pagination controls when totalCount exceeds one page', async () => {
    mockListMechanicAnalyses.mockResolvedValue({
      items: [ROW_CARCASSONNE],
      page: 1,
      pageSize: 20,
      totalCount: 25,
    });

    renderWithQuery(<MechanicAnalysesListCard onSelect={vi.fn()} />);

    await screen.findByText('Carcassonne');
    expect(screen.getByTestId('mechanic-analyses-list-prev')).toBeInTheDocument();
    expect(screen.getByTestId('mechanic-analyses-list-next')).toBeInTheDocument();
    // Page 1: prev disabled, next enabled
    expect(screen.getByTestId('mechanic-analyses-list-prev')).toBeDisabled();
    expect(screen.getByTestId('mechanic-analyses-list-next')).not.toBeDisabled();
  });

  it('highlights the selected row when selectedId matches', async () => {
    mockListMechanicAnalyses.mockResolvedValue({
      items: [ROW_CARCASSONNE, ROW_SUPPRESSED],
      page: 1,
      pageSize: 20,
      totalCount: 2,
    });

    renderWithQuery(
      <MechanicAnalysesListCard onSelect={vi.fn()} selectedId={ROW_CARCASSONNE.id} />
    );

    const selected = await screen.findByTestId(`mechanic-analyses-list-row-${ROW_CARCASSONNE.id}`);
    const other = screen.getByTestId(`mechanic-analyses-list-row-${ROW_SUPPRESSED.id}`);
    expect(selected.className).toMatch(/amber-100/);
    expect(other.className).not.toMatch(/amber-100/);
  });

  it('renders the error state when the query fails', async () => {
    mockListMechanicAnalyses.mockRejectedValue(new Error('boom'));

    renderWithQuery(<MechanicAnalysesListCard onSelect={vi.fn()} />);

    await waitFor(() => {
      expect(screen.getByText(/Failed to load analyses/i)).toBeInTheDocument();
    });
    expect(screen.getByText(/boom/)).toBeInTheDocument();
  });
});
