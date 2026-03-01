/**
 * KB Processing Pipeline Page Tests (Issue #4892)
 */

import { screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { ProcessingPipelineClient } from '../components/processing-pipeline-client';

const mockGetPipelineHealth = vi.hoisted(() => vi.fn());
const mockGetProcessingMetrics = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getPipelineHealth: mockGetPipelineHealth,
    getProcessingMetrics: mockGetProcessingMetrics,
  }),
  HttpClient: vi.fn(),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn(() => ({})),
}));

const mockHealth = {
  stages: [
    { name: 'embedding', status: 'healthy', metrics: {} },
    { name: 'chunking', status: 'warning', metrics: {} },
    { name: 'indexing', status: 'error', metrics: {} },
  ],
  summary: { healthyCount: 1, warningCount: 1, errorCount: 1 },
  recentActivity: [
    {
      jobId: 'job-1',
      fileName: 'rules.pdf',
      status: 'Completed',
      completedAt: '2026-01-10T10:00:00Z',
      durationMs: 1500,
    },
  ],
  distribution: {
    totalDocuments: 42,
    totalChunks: 840,
    vectorCount: 840,
    totalFiles: 12,
    storageSizeFormatted: '128 MB',
  },
  checkedAt: '2026-02-20T10:00:00Z',
};

const mockMetrics = {
  averages: {
    chunking: { step: 'chunking', avgDuration: 250, sampleSize: 100 },
    embedding: { step: 'embedding', avgDuration: 800, sampleSize: 100 },
  },
  percentiles: {
    chunking: { p50: 200, p95: 450, p99: 700 },
    embedding: { p50: 750, p95: 1200, p99: 2000 },
  },
  lastUpdated: '2026-02-20T10:00:00Z',
};

describe('ProcessingPipelineClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders page heading', async () => {
    mockGetPipelineHealth.mockResolvedValue(mockHealth);
    mockGetProcessingMetrics.mockResolvedValue(mockMetrics);

    renderWithQuery(<ProcessingPipelineClient />);

    await waitFor(() => {
      expect(screen.getByText('Pipeline Status')).toBeInTheDocument();
    });
  });

  it('shows summary counts', async () => {
    mockGetPipelineHealth.mockResolvedValue(mockHealth);
    mockGetProcessingMetrics.mockResolvedValue(mockMetrics);

    renderWithQuery(<ProcessingPipelineClient />);

    await waitFor(() => {
      expect(screen.getByText('Healthy Stages')).toBeInTheDocument();
    });

    expect(screen.getByText('Warning Stages')).toBeInTheDocument();
    expect(screen.getByText('Error Stages')).toBeInTheDocument();
  });

  it('renders pipeline stages', async () => {
    mockGetPipelineHealth.mockResolvedValue(mockHealth);
    mockGetProcessingMetrics.mockResolvedValue(mockMetrics);

    renderWithQuery(<ProcessingPipelineClient />);

    await waitFor(() => {
      // embedding appears in both stage list and metrics table
      expect(screen.getAllByText('embedding').length).toBeGreaterThan(0);
    });

    expect(screen.getAllByText('chunking').length).toBeGreaterThan(0);
    expect(screen.getByText('indexing')).toBeInTheDocument();
  });

  it('shows document distribution', async () => {
    mockGetPipelineHealth.mockResolvedValue(mockHealth);
    mockGetProcessingMetrics.mockResolvedValue(mockMetrics);

    renderWithQuery(<ProcessingPipelineClient />);

    await waitFor(() => {
      expect(screen.getByText('128 MB')).toBeInTheDocument();
    });

    expect(screen.getByText('42')).toBeInTheDocument(); // totalDocuments
  });

  it('shows recent activity', async () => {
    mockGetPipelineHealth.mockResolvedValue(mockHealth);
    mockGetProcessingMetrics.mockResolvedValue(mockMetrics);

    renderWithQuery(<ProcessingPipelineClient />);

    await waitFor(() => {
      expect(screen.getByText('rules.pdf')).toBeInTheDocument();
    });
  });

  it('shows loading skeletons', () => {
    mockGetPipelineHealth.mockReturnValue(new Promise(() => {}));
    mockGetProcessingMetrics.mockReturnValue(new Promise(() => {}));

    renderWithQuery(<ProcessingPipelineClient />);

    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows error state', async () => {
    mockGetPipelineHealth.mockRejectedValue(new Error('API unreachable'));
    mockGetProcessingMetrics.mockRejectedValue(new Error('API unreachable'));

    renderWithQuery(<ProcessingPipelineClient />);

    await waitFor(() => {
      expect(screen.getByText(/API unreachable/i)).toBeInTheDocument();
    });
  });

  it('shows no data state when both return null', async () => {
    mockGetPipelineHealth.mockResolvedValue(null);
    mockGetProcessingMetrics.mockResolvedValue(null);

    renderWithQuery(<ProcessingPipelineClient />);

    await waitFor(() => {
      expect(screen.getByText(/No pipeline data available/i)).toBeInTheDocument();
    });
  });
});
