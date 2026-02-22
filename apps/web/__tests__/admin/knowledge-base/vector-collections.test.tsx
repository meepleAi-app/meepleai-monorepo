/**
 * Vector Collections Page Tests (Issue #4788)
 *
 * Tests for the Vector Collections page (/admin/knowledge-base/vectors):
 * - Renders with real API data via useQuery
 * - Loading skeletons while fetching
 * - Error state with retry
 * - Empty state when no collections
 * - Stats calculation (total vectors, avg health)
 * - Refresh button
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ADMIN_TEST_IDS } from '@/lib/test-ids';

const { mockGetVectorCollections } = vi.hoisted(() => ({
  mockGetVectorCollections: vi.fn(),
}));

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getVectorCollections: mockGetVectorCollections,
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn(),
}));

// Mock VectorCollectionCard since it's a separate component
// Props match VectorCollectionCardProps (flat props, not a { collection } object)
vi.mock('@/components/admin/knowledge-base/vector-collection-card', () => ({
  VectorCollectionCard: ({ name, vectorCount }: { name: string; vectorCount: number }) => (
    <div data-testid={ADMIN_TEST_IDS.collectionCard(name)}>
      <span>{name}</span>
      <span>{vectorCount.toLocaleString()}</span>
    </div>
  ),
}));

import VectorCollectionsPage from '@/app/admin/(dashboard)/knowledge-base/vectors/page';

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: 0 },
      mutations: { retry: false },
    },
  });
}

function renderWithQuery(ui: React.ReactElement) {
  const client = createTestQueryClient();
  return render(
    <QueryClientProvider client={client}>{ui}</QueryClientProvider>
  );
}

const MOCK_COLLECTIONS = {
  collections: [
    { name: 'Game Rules', vectorCount: 42500, dimensions: 384, storage: '3.2 GB', health: 98 },
    { name: 'Strategy Guides', vectorCount: 28300, dimensions: 384, storage: '2.1 GB', health: 95 },
    { name: 'FAQ Database', vectorCount: 9200, dimensions: 384, storage: '1.5 GB', health: 92 },
  ],
};

describe('VectorCollectionsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetVectorCollections.mockResolvedValue(MOCK_COLLECTIONS);
  });

  it('should render page header', async () => {
    renderWithQuery(<VectorCollectionsPage />);

    expect(screen.getByText('Vector Collections')).toBeInTheDocument();
    expect(screen.getByText('Manage your knowledge base vector stores')).toBeInTheDocument();
  });

  it('should render stats cards with calculated values', async () => {
    renderWithQuery(<VectorCollectionsPage />);

    await waitFor(() => {
      // Total collections = 3
      expect(screen.getByText('3')).toBeInTheDocument();
    });

    // Total vectors: 42500 + 28300 + 9200 = 80000 (locale-formatted)
    expect(screen.getByText((80000).toLocaleString())).toBeInTheDocument();
    // Avg health: (98 + 95 + 92) / 3 = 95
    expect(screen.getByText('95%')).toBeInTheDocument();
    // Dimensions from first collection
    expect(screen.getByText('384')).toBeInTheDocument();
  });

  it('should render collection cards', async () => {
    renderWithQuery(<VectorCollectionsPage />);

    await waitFor(() => {
      expect(screen.getByTestId(ADMIN_TEST_IDS.collectionCard('Game Rules'))).toBeInTheDocument();
    });

    expect(screen.getByTestId(ADMIN_TEST_IDS.collectionCard('Strategy Guides'))).toBeInTheDocument();
    expect(screen.getByTestId(ADMIN_TEST_IDS.collectionCard('FAQ Database'))).toBeInTheDocument();
  });

  it('should show error state when fetch fails', async () => {
    mockGetVectorCollections.mockRejectedValue(new Error('Connection refused'));
    renderWithQuery(<VectorCollectionsPage />);

    await waitFor(() => {
      expect(screen.getByText('Failed to load vector collections')).toBeInTheDocument();
    });

    expect(screen.getByText('Connection refused')).toBeInTheDocument();
    expect(screen.getByText('Retry')).toBeInTheDocument();
  });

  it('should show empty state when no collections', async () => {
    mockGetVectorCollections.mockResolvedValue({ collections: [] });
    renderWithQuery(<VectorCollectionsPage />);

    await waitFor(() => {
      expect(screen.getByText('No vector collections found')).toBeInTheDocument();
    });
  });

  it('should render refresh button', () => {
    renderWithQuery(<VectorCollectionsPage />);

    expect(screen.getByText('Refresh')).toBeInTheDocument();
  });

  it('should show stat labels', async () => {
    renderWithQuery(<VectorCollectionsPage />);

    await waitFor(() => {
      expect(screen.getByText('Total Collections')).toBeInTheDocument();
    });

    expect(screen.getByText('Total Vectors')).toBeInTheDocument();
    expect(screen.getByText('Dimensions')).toBeInTheDocument();
    expect(screen.getByText('Avg Health')).toBeInTheDocument();
  });
});
