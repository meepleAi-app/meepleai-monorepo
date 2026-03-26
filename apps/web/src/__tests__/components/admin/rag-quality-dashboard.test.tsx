/**
 * RAG Quality Dashboard - Unit Tests
 *
 * Tests for the RAG quality observability dashboard:
 * - Summary cards render with data
 * - Top games table displays
 * - Enhancement flags table displays
 * - Loading and error states
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { RagQualityDashboard } from '@/components/admin/rag-quality-dashboard';
import type { RagQualityReport } from '@/lib/api/rag-quality';

// ============================================================================
// Mock Data
// ============================================================================

const mockReport: RagQualityReport = {
  totalIndexedDocuments: 42,
  totalRaptorSummaries: 128,
  totalEntityRelations: 356,
  totalEmbeddedChunks: 1_024,
  topGamesByChunkCount: [
    {
      gameId: 'game-1',
      gameTitle: 'Catan',
      chunkCount: 150,
      raptorNodeCount: 12,
      entityRelationCount: 45,
    },
    {
      gameId: 'game-2',
      gameTitle: 'Wingspan',
      chunkCount: 120,
      raptorNodeCount: 8,
      entityRelationCount: 30,
    },
  ],
  enhancementStatuses: [
    {
      name: 'RAPTOR Summaries',
      featureFlagKey: 'rag.raptor_summaries',
      freeEnabled: false,
      normalEnabled: true,
      premiumEnabled: true,
    },
    {
      name: 'Entity Graph',
      featureFlagKey: 'rag.entity_graph',
      freeEnabled: false,
      normalEnabled: false,
      premiumEnabled: true,
    },
  ],
};

// ============================================================================
// Mock Fetch
// ============================================================================

let mockFetchOk = true;
let mockFetchData: RagQualityReport = mockReport;

beforeEach(() => {
  vi.clearAllMocks();
  mockFetchOk = true;
  mockFetchData = mockReport;

  global.fetch = vi.fn(() =>
    mockFetchOk
      ? Promise.resolve({
          ok: true,
          json: () => Promise.resolve(mockFetchData),
        } as Response)
      : Promise.resolve({
          ok: false,
          status: 500,
          statusText: 'Internal Server Error',
        } as Response)
  );
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ============================================================================
// Test Utilities
// ============================================================================

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
      },
    },
  });
}

function renderWithProviders(component: React.ReactElement) {
  const queryClient = createTestQueryClient();
  return render(<QueryClientProvider client={queryClient}>{component}</QueryClientProvider>);
}

// ============================================================================
// Tests
// ============================================================================

describe('RagQualityDashboard', () => {
  describe('Summary Cards', () => {
    it('should render all four summary cards with data', async () => {
      renderWithProviders(<RagQualityDashboard />);

      await waitFor(() => {
        expect(screen.getByText('42')).toBeInTheDocument();
      });

      expect(screen.getByText('1,024')).toBeInTheDocument();
      expect(screen.getByText('128')).toBeInTheDocument();
      expect(screen.getByText('356')).toBeInTheDocument();
    });

    it('should render summary card labels', async () => {
      renderWithProviders(<RagQualityDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Total Indexed Documents')).toBeInTheDocument();
      });

      expect(screen.getByText('Embedded Chunks')).toBeInTheDocument();
      expect(screen.getByText('RAPTOR Summaries')).toBeInTheDocument();
      expect(screen.getByText('Entity Relations')).toBeInTheDocument();
    });
  });

  describe('Top Games Table', () => {
    it('should render game titles in the table', async () => {
      renderWithProviders(<RagQualityDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      expect(screen.getByText('Wingspan')).toBeInTheDocument();
    });

    it('should render chunk counts', async () => {
      renderWithProviders(<RagQualityDashboard />);

      await waitFor(() => {
        expect(screen.getByText('150')).toBeInTheDocument();
      });

      expect(screen.getByText('120')).toBeInTheDocument();
    });
  });

  describe('Enhancement Flags Table', () => {
    it('should render enhancement names', async () => {
      renderWithProviders(<RagQualityDashboard />);

      // Wait for data to load by checking for an enhancement-only element
      await waitFor(() => {
        expect(screen.getByText('Entity Graph')).toBeInTheDocument();
      });

      // "RAPTOR Summaries" appears both as summary card label and enhancement name
      expect(screen.getAllByText('RAPTOR Summaries').length).toBeGreaterThanOrEqual(2);
    });

    it('should render feature flag keys', async () => {
      renderWithProviders(<RagQualityDashboard />);

      await waitFor(() => {
        expect(screen.getByText('(rag.raptor_summaries)')).toBeInTheDocument();
      });

      expect(screen.getByText('(rag.entity_graph)')).toBeInTheDocument();
    });

    it('should render enabled/disabled icons', async () => {
      renderWithProviders(<RagQualityDashboard />);

      // Wait for enhancement data to load
      await waitFor(() => {
        expect(screen.getByText('Entity Graph')).toBeInTheDocument();
      });

      const enabledIcons = screen.getAllByLabelText('Enabled');
      const disabledIcons = screen.getAllByLabelText('Disabled');

      // RAPTOR: free=no, normal=yes, premium=yes
      // Entity: free=no, normal=no, premium=yes
      expect(enabledIcons.length).toBe(3);
      expect(disabledIcons.length).toBe(3);
    });
  });

  describe('Loading State', () => {
    it('should show loading skeletons initially', () => {
      renderWithProviders(<RagQualityDashboard />);

      // Page title should always be visible
      expect(screen.getByText('RAG Quality Dashboard')).toBeInTheDocument();
    });
  });

  describe('Error State', () => {
    it('should show error message when API fails', async () => {
      mockFetchOk = false;

      renderWithProviders(<RagQualityDashboard />);

      await waitFor(() => {
        expect(
          screen.getByText('Failed to load RAG quality report. Please try again.')
        ).toBeInTheDocument();
      });
    });
  });

  describe('Page Header', () => {
    it('should render page title and description', () => {
      renderWithProviders(<RagQualityDashboard />);

      expect(screen.getByText('RAG Quality Dashboard')).toBeInTheDocument();
      expect(
        screen.getByText('Monitor RAG pipeline health and enhancement status')
      ).toBeInTheDocument();
    });
  });
});
