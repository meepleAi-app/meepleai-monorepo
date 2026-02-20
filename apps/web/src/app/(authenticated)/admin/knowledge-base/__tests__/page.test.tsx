/**
 * Admin Knowledge Base Overview Page Tests (Issue #4892)
 */

import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { KnowledgeBaseOverviewClient } from '../client';

// ─── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('@/components/admin/AdminAuthGuard', () => ({
  AdminAuthGuard: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuthUser: vi.fn(() => ({ user: { id: '1', email: 'admin@test.com', role: 'Admin' }, loading: false })),
}));

const mockGetPdfStorageHealth = vi.fn();
const mockGetVectorCollections = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getPdfStorageHealth: (...args: unknown[]) => mockGetPdfStorageHealth(...args),
      getVectorCollections: (...args: unknown[]) => mockGetVectorCollections(...args),
    },
  },
}));

// ─── Test Data ────────────────────────────────────────────────────────────────

const mockStorageHealth = {
  postgres: { totalDocuments: 42, totalChunks: 1234, estimatedChunksSizeMB: 56.7 },
  qdrant: { vectorCount: 5678, memoryBytes: 1024 * 1024 * 200, memoryFormatted: '200 MB', isAvailable: true },
  fileStorage: { totalFiles: 42, totalSizeBytes: 1024 * 1024 * 500, totalSizeFormatted: '500 MB', sizeByState: {} },
  overallHealth: 'Healthy',
  measuredAt: '2026-01-01T00:00:00Z',
};

const mockCollections = {
  collections: [
    { name: 'game_rules', vectorCount: 3456, dimensions: 768, storage: 'memory', health: 0.98 },
    { name: 'faqs', vectorCount: 2222, dimensions: 768, storage: 'disk', health: 0.75 },
  ],
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('KnowledgeBaseOverviewClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPdfStorageHealth.mockResolvedValue(mockStorageHealth);
    mockGetVectorCollections.mockResolvedValue(mockCollections);
  });

  it('renders the page heading', () => {
    renderWithQuery(<KnowledgeBaseOverviewClient />);
    expect(screen.getByText('Knowledge Base')).toBeInTheDocument();
  });

  it('shows loading skeletons initially', () => {
    mockGetPdfStorageHealth.mockReturnValue(new Promise(() => {}));
    renderWithQuery(<KnowledgeBaseOverviewClient />);
    expect(screen.getByText('Storage Health')).toBeInTheDocument();
  });

  it('shows quick links to Documents and Processing Queue', async () => {
    renderWithQuery(<KnowledgeBaseOverviewClient />);
    await waitFor(() => {
      expect(screen.getByText('Documents')).toBeInTheDocument();
      expect(screen.getByText('Processing Queue')).toBeInTheDocument();
    });
  });

  it('shows storage health data after loading', async () => {
    renderWithQuery(<KnowledgeBaseOverviewClient />);
    await waitFor(() => {
      // totalDocuments=42 and totalFiles=42 both appear, use getAllByText
      expect(screen.getAllByText('42').length).toBeGreaterThan(0);
      expect(screen.getByText('Healthy')).toBeInTheDocument();
    });
  });

  it('shows Qdrant online status', async () => {
    renderWithQuery(<KnowledgeBaseOverviewClient />);
    await waitFor(() => {
      expect(screen.getByText('● Online')).toBeInTheDocument();
    });
  });

  it('shows vector collections table', async () => {
    renderWithQuery(<KnowledgeBaseOverviewClient />);
    await waitFor(() => {
      expect(screen.getByText('game_rules')).toBeInTheDocument();
      expect(screen.getByText('faqs')).toBeInTheDocument();
    });
  });

  it('shows error message when API fails', async () => {
    mockGetPdfStorageHealth.mockRejectedValue(new Error('Service unavailable'));
    renderWithQuery(<KnowledgeBaseOverviewClient />);
    await waitFor(() => {
      // Error appears in both alert and toast - use getAllByText
      expect(screen.getAllByText('Service unavailable').length).toBeGreaterThan(0);
    });
  });

  it('calls API methods on mount', async () => {
    renderWithQuery(<KnowledgeBaseOverviewClient />);
    await waitFor(() => {
      expect(mockGetPdfStorageHealth).toHaveBeenCalledTimes(1);
      expect(mockGetVectorCollections).toHaveBeenCalledTimes(1);
    });
  });
});
