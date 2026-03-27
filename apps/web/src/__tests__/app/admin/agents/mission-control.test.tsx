import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents',
}));

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAgentMetrics: vi.fn().mockResolvedValue({
        totalInvocations: 347,
        totalTokensUsed: 52400,
        totalCost: 1.84,
        avgLatencyMs: 1200,
        avgConfidenceScore: 0.87,
        userSatisfactionRate: 0.92,
        topQueries: [],
        costBreakdown: [],
        usageOverTime: [],
      }),
      getRagExecutions: vi.fn().mockResolvedValue({
        items: [
          {
            id: '1',
            query: 'Come si gioca a Catan?',
            agentDefinitionId: null,
            agentName: null,
            strategy: 'HybridRAG',
            model: null,
            provider: null,
            gameId: null,
            isPlayground: false,
            totalLatencyMs: 890,
            promptTokens: 800,
            completionTokens: 447,
            totalTokens: 1247,
            totalCost: 0.003,
            confidence: 0.92,
            cacheHit: false,
            status: 'ok',
            errorMessage: null,
            createdAt: '2026-03-27T14:32:05Z',
          },
        ],
        totalCount: 1,
      }),
      getEmbeddingInfo: vi
        .fn()
        .mockResolvedValue({
          status: 'healthy',
          model: 'e5-base',
          device: null,
          supportedLanguages: [],
          dimension: 768,
          maxInputChars: 512,
          maxBatchSize: 32,
        }),
      getOpenRouterStatus: vi
        .fn()
        .mockResolvedValue({
          balanceUsd: 10,
          dailySpendUsd: 0.5,
          todayRequestCount: 78,
          currentRpm: 78,
          limitRpm: 100,
          utilizationPercent: 78,
          isThrottled: false,
          isFreeTier: false,
          rateLimitInterval: '1m',
          lastUpdated: null,
        }),
    },
  },
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('Mission Control', () => {
  it('renders KPI cards', async () => {
    const { default: MissionControlPage } = await import('@/app/admin/(dashboard)/agents/page');
    render(<MissionControlPage />, { wrapper: Wrapper });
    expect(await screen.findByText('Esecuzioni Oggi')).toBeInTheDocument();
    expect(screen.getByText('Latenza Media')).toBeInTheDocument();
    expect(screen.getByText('Error Rate')).toBeInTheDocument();
    expect(screen.getByText('Token Consumati')).toBeInTheDocument();
    expect(screen.getByText('Costo Oggi')).toBeInTheDocument();
  });

  it('renders service health section', async () => {
    const { default: MissionControlPage } = await import('@/app/admin/(dashboard)/agents/page');
    render(<MissionControlPage />, { wrapper: Wrapper });
    expect(await screen.findByText('Service Health')).toBeInTheDocument();
  });

  it('renders quick action buttons', async () => {
    const { default: MissionControlPage } = await import('@/app/admin/(dashboard)/agents/page');
    render(<MissionControlPage />, { wrapper: Wrapper });
    expect(await screen.findByText('Test RAG Query')).toBeInTheDocument();
    expect(screen.getByText('Ispeziona Esecuzioni')).toBeInTheDocument();
  });

  it('renders recent executions table', async () => {
    const { default: MissionControlPage } = await import('@/app/admin/(dashboard)/agents/page');
    render(<MissionControlPage />, { wrapper: Wrapper });
    expect(await screen.findByText('Ultime Esecuzioni RAG')).toBeInTheDocument();
    expect(await screen.findByText('Come si gioca a Catan?')).toBeInTheDocument();
  });
});
