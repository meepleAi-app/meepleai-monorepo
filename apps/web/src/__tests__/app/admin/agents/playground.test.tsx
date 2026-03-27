import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents/playground',
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    testRagPipeline: vi.fn().mockResolvedValue({ answer: 'test', tokens: 100 }),
    getRagExecutions: vi.fn().mockResolvedValue({ items: [], totalCount: 0 }),
    compareRagExecutions: vi.fn().mockResolvedValue({ runA: null, runB: null }),
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn().mockImplementation(() => ({})),
}));

// Mock the debug chat components to avoid deep dependency chains
vi.mock('@/components/admin/debug-chat', () => ({
  DebugTimeline: () => <div data-testid="debug-timeline">DebugTimeline</div>,
  StrategySelectorBar: () => <div data-testid="strategy-selector">StrategySelectorBar</div>,
}));

vi.mock('@/hooks/useDebugChatStream', () => ({
  useDebugChatStream: () => ({
    messages: [],
    isStreaming: false,
    sendMessage: vi.fn(),
    events: [],
  }),
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('RAG Playground', () => {
  it('renders three tabs', async () => {
    const { default: PlaygroundPage } =
      await import('@/app/admin/(dashboard)/agents/playground/page');
    render(<PlaygroundPage />, { wrapper: Wrapper });
    expect(screen.getByRole('tab', { name: /query tester/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /chat debug/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /compare/i })).toBeInTheDocument();
  });

  it('shows query input and execute button on Query Tester tab', async () => {
    const { default: PlaygroundPage } =
      await import('@/app/admin/(dashboard)/agents/playground/page');
    render(<PlaygroundPage />, { wrapper: Wrapper });
    expect(screen.getByPlaceholderText(/scrivi una domanda/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /esegui/i })).toBeInTheDocument();
  });
});
