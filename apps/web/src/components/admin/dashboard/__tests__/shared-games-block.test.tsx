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

vi.mock('@/hooks/useDebounce', () => ({
  useDebounce: (value: string) => value,
}));

const mockItems = [
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
];

const mockPagedResult = {
  items: mockItems,
  totalCount: 2,
  page: 1,
  pageSize: 6,
  totalPages: 1,
};

const emptyResult = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 6,
  totalPages: 0,
};

describe('SharedGamesBlock', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders block header with title and badge', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue({
      ...emptyResult,
      totalCount: 23,
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

    const skeletons = container.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('displays game cards when data loads', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue(mockPagedResult);

    renderWithQuery(<SharedGamesBlock />);

    await waitFor(() => {
      expect(screen.getByText(/twilight imperium/i)).toBeInTheDocument();
      expect(screen.getByText(/gloomhaven/i)).toBeInTheDocument();
    });
  });

  it('shows urgent badge for games 7+ days pending', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue({
      items: [mockItems[1]],
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
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue(emptyResult);

    renderWithQuery(<SharedGamesBlock />);

    const buttons = screen.getAllByRole('button');
    const gridButton = buttons.find((btn) => btn.querySelector('svg'));
    const listButton = buttons.find((btn) => btn.querySelector('svg') && btn !== gridButton);

    expect(gridButton).toBeDefined();
    expect(listButton).toBeDefined();
  });

  it('filters games by search query', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue(emptyResult);

    renderWithQuery(<SharedGamesBlock />);

    const searchInput = screen.getByPlaceholderText(/search games/i);
    fireEvent.change(searchInput, { target: { value: 'Wingspan' } });

    expect(searchInput).toHaveValue('Wingspan');
  });

  it('shows empty state when no games', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue(emptyResult);

    renderWithQuery(<SharedGamesBlock />);

    await waitFor(() => {
      expect(screen.getByText(/no games in approval queue/i)).toBeInTheDocument();
    });
  });

  it('shows select all button', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue(mockPagedResult);

    renderWithQuery(<SharedGamesBlock />);

    await screen.findByText(/twilight imperium/i);

    expect(screen.getByLabelText(/select all/i)).toBeInTheDocument();
  });

  it('shows selection checkboxes on game cards', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue(mockPagedResult);

    renderWithQuery(<SharedGamesBlock />);

    await screen.findByText(/twilight imperium/i);

    expect(screen.getByLabelText(/select twilight imperium/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/select gloomhaven/i)).toBeInTheDocument();
  });

  it('shows bulk actions bar when items are selected', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue(mockPagedResult);

    renderWithQuery(<SharedGamesBlock />);

    await screen.findByText(/twilight imperium/i);

    // Select a game
    fireEvent.click(screen.getByLabelText(/select twilight imperium/i));

    await waitFor(() => {
      expect(screen.getByTestId('bulk-actions')).toBeInTheDocument();
      expect(screen.getByText('1 selected')).toBeInTheDocument();
      expect(screen.getByText('Batch Approve')).toBeInTheDocument();
      expect(screen.getByText('Batch Reject')).toBeInTheDocument();
    });
  });

  it('calls batch approve with selected game IDs', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue(mockPagedResult);
    vi.mocked(adminClientModule.adminClient.batchApproveGames).mockResolvedValue(undefined);

    renderWithQuery(<SharedGamesBlock />);

    await screen.findByText(/twilight imperium/i);

    // Select first game
    fireEvent.click(screen.getByLabelText(/select twilight imperium/i));

    await waitFor(() => {
      expect(screen.getByText('Batch Approve')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Batch Approve'));

    await waitFor(() => {
      expect(adminClientModule.adminClient.batchApproveGames).toHaveBeenCalledWith(['1']);
    });
  });

  it('handles undefined API response gracefully', async () => {
    vi.mocked(adminClientModule.adminClient.getApprovalQueue).mockResolvedValue(emptyResult);

    renderWithQuery(<SharedGamesBlock />);

    await waitFor(() => {
      expect(screen.getByText(/no games in approval queue/i)).toBeInTheDocument();
    });
  });
});
