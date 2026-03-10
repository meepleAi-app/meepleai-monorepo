/**
 * My AI Usage Page Tests — Issue #94 (C3: Editor Self-Service AI Usage Page)
 *
 * Tests:
 * 1. Renders multi-period summary cards (today/7d/30d)
 * 2. Shows loading skeleton
 * 3. Shows error state
 * 4. Renders distribution charts (model, provider)
 * 5. Renders recent requests table with pagination
 * 6. Shows info banner
 * 7. Empty state when no usage
 * 8. Requires Editor+ role
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type {
  AiUsageSummaryDto,
  AiUsageDistributionsDto,
  AiUsageRecentDto,
  UserAiUsageDto,
} from '@/lib/api/schemas/ai-usage.schemas';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockUser = { id: 'user-1', name: 'Test Editor', role: 'Editor' };

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ user: mockUser, isLoading: false }),
}));

vi.mock('@/components/auth/RequireRole', () => ({
  RequireRole: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

const mockGetMyAiUsageSummary = vi.fn();
const mockGetMyAiUsageDistributions = vi.fn();
const mockGetMyAiUsageRecent = vi.fn();
const mockGetMyAiUsage = vi.fn();

vi.mock('@/lib/api/dashboard-client', () => ({
  dashboardClient: {
    getMyAiUsageSummary: (...args: unknown[]) => mockGetMyAiUsageSummary(...args),
    getMyAiUsageDistributions: (...args: unknown[]) => mockGetMyAiUsageDistributions(...args),
    getMyAiUsageRecent: (...args: unknown[]) => mockGetMyAiUsageRecent(...args),
    getMyAiUsage: (...args: unknown[]) => mockGetMyAiUsage(...args),
  },
}));

const mockSummary: AiUsageSummaryDto = {
  today: {
    requestCount: 12,
    promptTokens: 8400,
    completionTokens: 3200,
    totalTokens: 11600,
    costUsd: 0.0234,
    averageLatencyMs: 1250,
  },
  last7Days: {
    requestCount: 87,
    promptTokens: 62000,
    completionTokens: 24000,
    totalTokens: 86000,
    costUsd: 0.172,
    averageLatencyMs: 1180,
  },
  last30Days: {
    requestCount: 342,
    promptTokens: 245000,
    completionTokens: 98000,
    totalTokens: 343000,
    costUsd: 0.684,
    averageLatencyMs: 1210,
  },
};

const mockDistributions: AiUsageDistributionsDto = {
  models: [
    { name: 'llama3:8b', count: 180, percentage: 52.6 },
    { name: 'gpt-4o-mini', count: 95, percentage: 27.8 },
    { name: 'claude-3-haiku', count: 67, percentage: 19.6 },
  ],
  providers: [
    { name: 'Ollama', count: 210, percentage: 61.4 },
    { name: 'OpenRouter', count: 132, percentage: 38.6 },
  ],
  operations: [
    { name: 'chat', count: 200, percentage: 58.5 },
    { name: 'rag_query', count: 142, percentage: 41.5 },
  ],
};

const mockRecent: AiUsageRecentDto = {
  items: [
    {
      requestedAt: '2026-03-10T14:22:00Z',
      model: 'llama3:8b',
      provider: 'Ollama',
      operation: 'chat',
      promptTokens: 650,
      completionTokens: 280,
      costUsd: 0.0,
      latencyMs: 890,
      success: true,
    },
    {
      requestedAt: '2026-03-10T14:10:00Z',
      model: 'gpt-4o-mini',
      provider: 'OpenRouter',
      operation: 'rag_query',
      promptTokens: 1100,
      completionTokens: 320,
      costUsd: 0.0012,
      latencyMs: 1420,
      success: true,
    },
  ],
  total: 87,
  page: 1,
  pageSize: 20,
  note: 'Individual requests are available for the last 7 days only (GDPR compliance).',
};

const mockDailyUsage: UserAiUsageDto = {
  userId: 'user-1',
  period: { from: '2026-02-08', to: '2026-03-10' },
  totalTokens: 343000,
  totalCostUsd: 0.684,
  requestCount: 342,
  byModel: [],
  byOperation: [],
  dailyUsage: [
    { date: '2026-03-08', tokens: 5000 },
    { date: '2026-03-09', tokens: 7200 },
    { date: '2026-03-10', tokens: 3100 },
  ],
};

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('MyAiUsagePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetMyAiUsageSummary.mockResolvedValue(mockSummary);
    mockGetMyAiUsageDistributions.mockResolvedValue(mockDistributions);
    mockGetMyAiUsageRecent.mockResolvedValue(mockRecent);
    mockGetMyAiUsage.mockResolvedValue(mockDailyUsage);
  });

  it('renders multi-period summary cards', async () => {
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByTestId('usage-summary')).toBeInTheDocument();
    });

    // Today card
    expect(screen.getByText('Oggi')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();

    // 7 day card
    expect(screen.getByText('7 Giorni')).toBeInTheDocument();
    expect(screen.getByText('87')).toBeInTheDocument();

    // 30 day card
    expect(screen.getByText('30 Giorni')).toBeInTheDocument();
    expect(screen.getByText('342')).toBeInTheDocument();
  });

  it('shows loading skeleton initially', async () => {
    mockGetMyAiUsageSummary.mockReturnValue(new Promise(() => {}));
    mockGetMyAiUsageDistributions.mockReturnValue(new Promise(() => {}));
    mockGetMyAiUsageRecent.mockReturnValue(new Promise(() => {}));
    mockGetMyAiUsage.mockReturnValue(new Promise(() => {}));

    const { default: Page } = await import('../page');
    render(<Page />);

    expect(screen.getByTestId('usage-skeleton')).toBeInTheDocument();
  });

  it('shows error when API fails', async () => {
    mockGetMyAiUsageSummary.mockRejectedValue(new Error('Network error'));
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });
  });

  it('renders model distribution chart', async () => {
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByText('Distribuzione modelli')).toBeInTheDocument();
    });

    expect(screen.getAllByText('llama3:8b').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('gpt-4o-mini').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('52.6%')).toBeInTheDocument();
  });

  it('renders provider distribution chart', async () => {
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByText('Distribuzione provider')).toBeInTheDocument();
    });

    expect(screen.getAllByText('Ollama').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('OpenRouter').length).toBeGreaterThanOrEqual(1);
  });

  it('renders recent requests table', async () => {
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByTestId('recent-requests-table')).toBeInTheDocument();
    });

    expect(screen.getByText('Richieste recenti')).toBeInTheDocument();
    expect(screen.getByText(/87 richieste totali/)).toBeInTheDocument();
  });

  it('paginates recent requests', async () => {
    const user = userEvent.setup();
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByTestId('recent-requests-table')).toBeInTheDocument();
    });

    // Page 1 of 5 (87 total / 20 per page)
    expect(screen.getByText(/Pagina 1 di 5/)).toBeInTheDocument();

    // Click next page
    mockGetMyAiUsageRecent.mockResolvedValue({ ...mockRecent, page: 2 });
    await user.click(screen.getByLabelText('Pagina successiva'));

    await waitFor(() => {
      expect(mockGetMyAiUsageRecent).toHaveBeenCalledWith(2, 20);
    });
  });

  it('shows info banner about 7-day vs 30-day data', async () => {
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByTestId('usage-info-banner')).toBeInTheDocument();
    });

    expect(screen.getByText(/dettagli individuali.*7 giorni/i)).toBeInTheDocument();
  });

  it('renders daily usage chart', async () => {
    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByTestId('daily-usage-chart')).toBeInTheDocument();
    });
  });

  it('shows empty state when no requests', async () => {
    mockGetMyAiUsageSummary.mockResolvedValue({
      today: { requestCount: 0, promptTokens: 0, completionTokens: 0, totalTokens: 0, costUsd: 0, averageLatencyMs: 0 },
      last7Days: { requestCount: 0, promptTokens: 0, completionTokens: 0, totalTokens: 0, costUsd: 0, averageLatencyMs: 0 },
      last30Days: { requestCount: 0, promptTokens: 0, completionTokens: 0, totalTokens: 0, costUsd: 0, averageLatencyMs: 0 },
    });
    mockGetMyAiUsageDistributions.mockResolvedValue({ models: [], providers: [], operations: [] });
    mockGetMyAiUsageRecent.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20, note: '' });
    mockGetMyAiUsage.mockResolvedValue({
      ...mockDailyUsage,
      requestCount: 0,
      totalTokens: 0,
      dailyUsage: [],
    });

    const { default: Page } = await import('../page');
    render(<Page />);

    await waitFor(() => {
      expect(screen.getByText(/Non hai ancora utilizzato/)).toBeInTheDocument();
    });
  });
});
