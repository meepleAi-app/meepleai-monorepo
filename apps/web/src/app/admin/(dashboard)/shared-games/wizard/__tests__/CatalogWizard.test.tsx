import { vi, describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  redirect: vi.fn(),
  usePathname: () => '/admin/shared-games/wizard',
}));

const mockSearch = vi.fn();
const mockGetSharedGameDocuments = vi.fn();
const mockBulkUploadPdfs = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      search: (...args: unknown[]) => mockSearch(...args),
    },
    admin: {
      getSharedGameDocuments: (...args: unknown[]) => mockGetSharedGameDocuments(...args),
      bulkUploadPdfs: (...args: unknown[]) => mockBulkUploadPdfs(...args),
    },
  },
}));

import { CatalogWizard } from '../CatalogWizard';

describe('CatalogWizard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders step 1 with search input', () => {
    render(<CatalogWizard />);
    expect(screen.getByPlaceholderText('Search games by title...')).toBeInTheDocument();
    expect(screen.getByText('Select a Game')).toBeInTheDocument();
  });

  it('displays step indicators', () => {
    render(<CatalogWizard />);
    expect(screen.getByText('Select Game')).toBeInTheDocument();
    expect(screen.getByText('Upload PDFs')).toBeInTheDocument();
    expect(screen.getByText('Review')).toBeInTheDocument();
  });

  it('searches for games when search is triggered', async () => {
    mockSearch.mockResolvedValue({
      items: [
        { id: 'game-1', title: 'Catan' },
        { id: 'game-2', title: 'Carcassonne' },
      ],
    });

    render(<CatalogWizard />);

    const input = screen.getByPlaceholderText('Search games by title...');
    fireEvent.change(input, { target: { value: 'Catan' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    await waitFor(() => {
      expect(mockSearch).toHaveBeenCalledWith({ searchTerm: 'Catan', pageSize: 10 });
    });

    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Carcassonne')).toBeInTheDocument();
    });
  });

  it('moves to step 2 when a game is selected', async () => {
    mockSearch.mockResolvedValue({
      items: [{ id: 'game-1', title: 'Catan' }],
    });
    mockGetSharedGameDocuments.mockResolvedValue({
      sharedGameId: 'game-1',
      documents: [],
      totalCount: 0,
    });

    render(<CatalogWizard />);

    const input = screen.getByPlaceholderText('Search games by title...');
    fireEvent.change(input, { target: { value: 'Catan' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Catan'));

    await waitFor(() => {
      expect(screen.getByText(/Upload PDFs for Catan/)).toBeInTheDocument();
    });
  });

  it('renders period selector buttons in step 1', () => {
    render(<CatalogWizard />);
    expect(screen.getByText('Select a Game')).toBeInTheDocument();
  });
});
