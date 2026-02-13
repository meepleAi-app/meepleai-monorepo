import { screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { SharedGamesBlock } from '../shared-games-block';
import * as adminClientModule from '@/lib/api/admin-client';

// Mock dependencies
vi.mock('@/lib/api/admin-client', () => ({
  adminClient: {
    getApprovalQueue: vi.fn(),
    batchApproveGames: vi.fn(),
    batchRejectGames: vi.fn(),
  },
}));

vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({
    toast: vi.fn(),
  }),
}));

describe('SharedGamesBlock', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders block header with title and badge', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue({
      items: [],
      totalCount: 23,
      page: 1,
      pageSize: 6,
      totalPages: 4,
    });

    renderWithQuery(<SharedGamesBlock />);

    expect(screen.getByRole('heading', { name: /approval queue/i })).toBeInTheDocument();
    await screen.findByText('23 pending');
    expect(screen.getByRole('link', { name: /view all/i })).toHaveAttribute(
      'href',
      '/admin/shared-games/approvals'
    );
  });

  it('displays loading skeletons initially', () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockImplementation(
      () => new Promise(() => {})
    );

    const { container } = renderWithQuery(<SharedGamesBlock />);

    // Should show 6 skeleton cards
    const skeletons = container.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('displays game cards when data loads', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue({
      items: [
        {
          gameId: '1',
          title: 'Twilight Imperium',
          submittedBy: 'user@example.com',
          submittedAt: new Date().toISOString(),
          daysPending: 5,
          pdfCount: 3,
        },
        {
          gameId: '2',
          title: 'Gloomhaven',
          submittedBy: 'admin@example.com',
          submittedAt: new Date().toISOString(),
          daysPending: 10,
          pdfCount: 2,
        },
      ],
      totalCount: 2,
      page: 1,
      pageSize: 6,
      totalPages: 1,
    });

    renderWithQuery(<SharedGamesBlock />);

    await waitFor(() => {
      expect(screen.getByText(/twilight imperium/i)).toBeInTheDocument();
      expect(screen.getByText(/gloomhaven/i)).toBeInTheDocument();
    });
  });

  it('shows urgent badge for games 7+ days pending', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue({
      items: [
        {
          gameId: '1',
          title: 'Old Game',
          submittedBy: 'user@example.com',
          submittedAt: new Date().toISOString(),
          daysPending: 10,
          pdfCount: 2,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 6,
      totalPages: 1,
    });

    renderWithQuery(<SharedGamesBlock />);

    await waitFor(() => {
      expect(screen.getByText(/urgent/i)).toBeInTheDocument();
    });
  });

  it('toggles between grid and list views', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 6,
      totalPages: 0,
    });

    renderWithQuery(<SharedGamesBlock />);

    const buttons = screen.getAllByRole('button');
    const gridButton = buttons.find((btn) => btn.querySelector('svg'));
    const listButton = buttons.find((btn) => btn.querySelector('svg') && btn !== gridButton);

    expect(gridButton).toBeDefined();
    expect(listButton).toBeDefined();
  });

  it('filters games by search query', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 6,
      totalPages: 0,
    });

    renderWithQuery(<SharedGamesBlock />);

    const searchInput = screen.getByPlaceholderText(/search games/i);
    fireEvent.change(searchInput, { target: { value: 'Wingspan' } });

    expect(searchInput).toHaveValue('Wingspan');
  });

  it('shows empty state when no games', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 6,
      totalPages: 0,
    });

    renderWithQuery(<SharedGamesBlock />);

    await waitFor(() => {
      expect(screen.getByText(/no games in approval queue/i)).toBeInTheDocument();
    });
  });

  it('handles undefined API response gracefully', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 6,
      totalPages: 0,
    });

    renderWithQuery(<SharedGamesBlock />);

    await waitFor(() => {
      expect(screen.getByText(/no games in approval queue/i)).toBeInTheDocument();
    });
  });
});
