/**
 * ModelPerformanceTab Tests (Issue #3716)
 *
 * Tests:
 * 1. Loading state
 * 2. Renders global stat cards
 * 3. Model comparison table
 * 4. Daily trends table
 * 5. Time range selector
 * 6. Error state
 * 7. Empty data
 * 8. Cost formatting
 * 9. Latency formatting
 * 10. Model name shortening
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { ModelPerformanceTab } from '../ModelPerformanceTab';

// Mock the API
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getModelPerformance: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';
const mockGetModelPerformance = vi.mocked(api.admin.getModelPerformance);

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

const mockData = {
  totalRequests: 5432,
  totalCost: 12.58,
  totalTokens: 1_234_567,
  avgLatencyMs: 850,
  successRate: 97.3,
  models: [
    {
      modelId: 'openai/gpt-4o-mini',
      provider: 'OpenRouter',
      requestCount: 3200,
      usagePercent: 58.9,
      totalCost: 8.50,
      avgLatencyMs: 920,
      totalTokens: 800_000,
      successRate: 98.1,
      avgTokensPerRequest: 250,
    },
    {
      modelId: 'meta-llama/llama-3.3-70b-instruct:free',
      provider: 'OpenRouter',
      requestCount: 2232,
      usagePercent: 41.1,
      totalCost: 4.08,
      avgLatencyMs: 750,
      totalTokens: 434_567,
      successRate: 96.2,
      avgTokensPerRequest: 195,
    },
  ],
  dailyStats: [
    { date: '2026-02-14', requestCount: 180, totalCost: 0.42, avgLatencyMs: 880 },
    { date: '2026-02-15', requestCount: 210, totalCost: 0.51, avgLatencyMs: 820 },
  ],
};

describe('ModelPerformanceTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders loading state initially', () => {
    mockGetModelPerformance.mockReturnValue(new Promise(() => {}));
    render(<ModelPerformanceTab />, { wrapper: createWrapper() });

    const loadingElements = screen.getAllByText('...');
    expect(loadingElements.length).toBeGreaterThan(0);
  });

  it('renders global stat cards with data', async () => {
    mockGetModelPerformance.mockResolvedValue(mockData);
    render(<ModelPerformanceTab />, { wrapper: createWrapper() });

    expect(await screen.findByText('$12.58')).toBeInTheDocument();
    expect(screen.getByText('97.3%')).toBeInTheDocument();
    expect(screen.getByText('850ms')).toBeInTheDocument();
    expect(screen.getByText('Total Requests')).toBeInTheDocument();
    expect(screen.getByText('Total Tokens')).toBeInTheDocument();
  });

  it('renders model comparison table', async () => {
    mockGetModelPerformance.mockResolvedValue(mockData);
    render(<ModelPerformanceTab />, { wrapper: createWrapper() });

    // Wait for model name to render (data fully loaded)
    expect(await screen.findByText('gpt-4o-mini')).toBeInTheDocument();
    expect(screen.getByText('Model Comparison')).toBeInTheDocument();
    expect(screen.getByText('llama-3.3-70b-instruct:free')).toBeInTheDocument();
    // Provider
    const providerCells = screen.getAllByText('OpenRouter');
    expect(providerCells.length).toBe(2);
    // Usage percentages - inside span alongside bar, use function matcher
    expect(screen.getByText((content) => content.includes('58.9%'))).toBeInTheDocument();
    expect(screen.getByText((content) => content.includes('41.1%'))).toBeInTheDocument();
  });

  it('renders daily trends table', async () => {
    mockGetModelPerformance.mockResolvedValue(mockData);
    render(<ModelPerformanceTab />, { wrapper: createWrapper() });

    expect(await screen.findByText('2026-02-14')).toBeInTheDocument();
    expect(screen.getByText('2026-02-15')).toBeInTheDocument();
    expect(screen.getByText('180')).toBeInTheDocument();
    expect(screen.getByText('210')).toBeInTheDocument();
  });

  it('shows time range selector with active state', () => {
    mockGetModelPerformance.mockResolvedValue(mockData);
    render(<ModelPerformanceTab />, { wrapper: createWrapper() });

    const btn30 = screen.getByText('30 days');
    expect(btn30.className).toContain('bg-amber-100');

    const btn7 = screen.getByText('7 days');
    expect(btn7.className).not.toContain('bg-amber-100');
  });

  it('changes time range on button click', async () => {
    mockGetModelPerformance.mockResolvedValue(mockData);
    render(<ModelPerformanceTab />, { wrapper: createWrapper() });

    fireEvent.click(screen.getByText('90 days'));
    expect(mockGetModelPerformance).toHaveBeenCalledWith(90);
  });

  it('renders error state', async () => {
    mockGetModelPerformance.mockRejectedValue(new Error('Server error'));
    render(<ModelPerformanceTab />, { wrapper: createWrapper() });

    expect(await screen.findByText('Failed to load model performance data', {}, { timeout: 5000 })).toBeInTheDocument();
    expect(screen.getByText('Server error')).toBeInTheDocument();
  });

  it('shows empty data message when no models', async () => {
    mockGetModelPerformance.mockResolvedValue({
      ...mockData,
      totalRequests: 0,
      totalCost: 0,
      totalTokens: 0,
      avgLatencyMs: 0,
      successRate: 0,
      models: [],
      dailyStats: [],
    });
    render(<ModelPerformanceTab />, { wrapper: createWrapper() });

    expect(await screen.findByText('No model data for this period')).toBeInTheDocument();
    expect(screen.getByText('No daily data for this period')).toBeInTheDocument();
  });

  it('formats cost values correctly', async () => {
    mockGetModelPerformance.mockResolvedValue(mockData);
    render(<ModelPerformanceTab />, { wrapper: createWrapper() });

    // Total cost: $12.58
    expect(await screen.findByText('$12.58')).toBeInTheDocument();
    // Model costs: $8.50 and $4.08
    expect(screen.getByText('$8.50')).toBeInTheDocument();
    expect(screen.getByText('$4.08')).toBeInTheDocument();
  });

  it('formats latency as seconds when >= 1000ms', async () => {
    mockGetModelPerformance.mockResolvedValue({
      ...mockData,
      avgLatencyMs: 1500,
    });
    render(<ModelPerformanceTab />, { wrapper: createWrapper() });

    // 1500ms → 1.5s
    expect(await screen.findByText('1.5s')).toBeInTheDocument();
  });
});
