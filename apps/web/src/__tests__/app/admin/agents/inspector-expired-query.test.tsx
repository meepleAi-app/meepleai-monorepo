import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

// #4634: the server blanks the question of an execution older than the retention window and keeps the row.
// The inspector has to say so, not show an empty cell.
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents/inspector',
  useSearchParams: () => new URLSearchParams(),
}));

const execution = (id: string, query: string) => ({
  id,
  query,
  agentDefinitionId: null,
  agentName: 'Rules Agent',
  strategy: 'HybridRag',
  model: 'test-model',
  provider: 'test-provider',
  gameId: null,
  isPlayground: true,
  totalLatencyMs: 812,
  promptTokens: 400,
  completionTokens: 112,
  totalTokens: 512,
  totalCost: 0.01,
  confidence: 0.42,
  cacheHit: false,
  status: 'Success',
  errorMessage: null,
  createdAt: '2026-01-01T10:00:00Z',
});

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getRagExecutions: vi.fn().mockResolvedValue({
      items: [
        execution('expired-1', ''),
        execution('fresh-1', 'How many cards does each player draw?'),
      ],
      totalCount: 2,
    }),
    getRagExecutionById: vi.fn().mockResolvedValue(null),
    getRagExecutionStats: vi.fn().mockResolvedValue({
      totalExecutions: 2,
      avgLatencyMs: 812,
      errorRate: 0,
      cacheHitRate: 0,
      totalCost: 0.02,
    }),
  }),
}));

class MockHttpClient {}
vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: MockHttpClient,
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('RAG Inspector, expired question (#4634)', () => {
  it('labels the row whose question expired, and leaves the others alone', async () => {
    const { default: InspectorPage } =
      await import('@/app/admin/(dashboard)/agents/inspector/page');
    render(<InspectorPage />, { wrapper: Wrapper });

    expect((await screen.findAllByText('(testo scaduto)')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('How many cards does each player draw?').length).toBeGreaterThan(0);
  });
});
