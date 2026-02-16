/**
 * PdfAnalyticsTab Tests (Issue #3715)
 *
 * Tests:
 * 1. Loading state
 * 2. Renders stat cards with real data
 * 3. Time range selector works
 * 4. Error state
 * 5. Empty data
 * 6. TimeSpan formatting
 * 7. Bytes formatting
 * 8. Daily uploads table
 * 9. Storage breakdown
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { PdfAnalyticsTab } from '../PdfAnalyticsTab';

// Mock the API
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getPdfAnalytics: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';
const mockGetPdfAnalytics = vi.mocked(api.admin.getPdfAnalytics);

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
  };
}

const mockAnalytics = {
  totalUploaded: 1234,
  successCount: 1100,
  failedCount: 134,
  successRate: 89.1,
  avgProcessingTime: '00:02:45.0000000',
  p95ProcessingTime: '00:08:30.0000000',
  totalStorageBytes: 15_674_000_000,
  storageByTier: {
    Ready: 12_000_000_000,
    Failed: 2_000_000_000,
    Pending: 1_674_000_000,
  },
  uploadsByDay: [
    { date: '2026-02-14', totalCount: 45, successCount: 40, failedCount: 5 },
    { date: '2026-02-15', totalCount: 52, successCount: 48, failedCount: 4 },
  ],
};

describe('PdfAnalyticsTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders loading state initially', () => {
    mockGetPdfAnalytics.mockReturnValue(new Promise(() => {})); // never resolves
    render(<PdfAnalyticsTab />, { wrapper: createWrapper() });

    // Should show loading indicators
    const loadingElements = screen.getAllByText('...');
    expect(loadingElements.length).toBeGreaterThan(0);
  });

  it('renders stat cards with data', async () => {
    mockGetPdfAnalytics.mockResolvedValue(mockAnalytics);
    render(<PdfAnalyticsTab />, { wrapper: createWrapper() });

    // Wait for data to load - toLocaleString output depends on test locale
    expect(await screen.findByText('89.1%')).toBeInTheDocument();
    expect(screen.getByText('2m 45s')).toBeInTheDocument();
    expect(screen.getByText('1100 success / 134 failed')).toBeInTheDocument();
    // Total PDFs: 1234 (locale-independent check)
    expect(screen.getByText('Total PDFs')).toBeInTheDocument();
  });

  it('shows P95 processing time in subtext', async () => {
    mockGetPdfAnalytics.mockResolvedValue(mockAnalytics);
    render(<PdfAnalyticsTab />, { wrapper: createWrapper() });

    expect(await screen.findByText('P95: 8m 30s')).toBeInTheDocument();
  });

  it('renders daily uploads table', async () => {
    mockGetPdfAnalytics.mockResolvedValue(mockAnalytics);
    render(<PdfAnalyticsTab />, { wrapper: createWrapper() });

    expect(await screen.findByText('2026-02-14')).toBeInTheDocument();
    expect(screen.getByText('2026-02-15')).toBeInTheDocument();
    expect(screen.getByText('45')).toBeInTheDocument();
    expect(screen.getByText('52')).toBeInTheDocument();
  });

  it('renders storage breakdown', async () => {
    mockGetPdfAnalytics.mockResolvedValue(mockAnalytics);
    render(<PdfAnalyticsTab />, { wrapper: createWrapper() });

    expect(await screen.findByText('Storage by State')).toBeInTheDocument();
    expect(screen.getByText('Ready')).toBeInTheDocument();
    expect(screen.getByText('Pending')).toBeInTheDocument();
    // "Failed" appears in both table header and storage breakdown
    const failedElements = screen.getAllByText('Failed');
    expect(failedElements.length).toBeGreaterThanOrEqual(2);
  });

  it('shows time range selector with active state', () => {
    mockGetPdfAnalytics.mockResolvedValue(mockAnalytics);
    render(<PdfAnalyticsTab />, { wrapper: createWrapper() });

    const btn30 = screen.getByText('30 days');
    expect(btn30.className).toContain('bg-amber-100');

    const btn7 = screen.getByText('7 days');
    expect(btn7.className).not.toContain('bg-amber-100');
  });

  it('changes time range on button click', async () => {
    mockGetPdfAnalytics.mockResolvedValue(mockAnalytics);
    render(<PdfAnalyticsTab />, { wrapper: createWrapper() });

    fireEvent.click(screen.getByText('7 days'));

    // Should call API with 7 days
    expect(mockGetPdfAnalytics).toHaveBeenCalledWith(7);
  });

  it('renders error state', async () => {
    mockGetPdfAnalytics.mockRejectedValue(new Error('Network error'));
    render(<PdfAnalyticsTab />, { wrapper: createWrapper() });

    expect(await screen.findByText('Failed to load PDF analytics', {}, { timeout: 5000 })).toBeInTheDocument();
    expect(screen.getByText('Network error')).toBeInTheDocument();
  });

  it('shows empty data message when no uploads', async () => {
    mockGetPdfAnalytics.mockResolvedValue({
      ...mockAnalytics,
      totalUploaded: 0,
      successCount: 0,
      failedCount: 0,
      successRate: 0,
      uploadsByDay: [],
      storageByTier: {},
      totalStorageBytes: 0,
    });
    render(<PdfAnalyticsTab />, { wrapper: createWrapper() });

    expect(await screen.findByText('No upload data for this period')).toBeInTheDocument();
  });

  it('formats storage bytes correctly', async () => {
    mockGetPdfAnalytics.mockResolvedValue(mockAnalytics);
    render(<PdfAnalyticsTab />, { wrapper: createWrapper() });

    // 15_674_000_000 bytes ≈ 14.6 GB
    expect(await screen.findByText('14.6 GB')).toBeInTheDocument();
  });

  it('handles null processing times', async () => {
    mockGetPdfAnalytics.mockResolvedValue({
      ...mockAnalytics,
      avgProcessingTime: null,
      p95ProcessingTime: null,
    });
    render(<PdfAnalyticsTab />, { wrapper: createWrapper() });

    // Wait for data to load, then check for dash character for null timespan
    expect(await screen.findByText('89.1%')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();
  });
});
