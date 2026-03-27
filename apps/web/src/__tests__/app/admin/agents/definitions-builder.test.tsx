import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents/definitions',
  useSearchParams: () => new URLSearchParams(),
}));

// Mock the agent definitions API (used directly, not via @/lib/api)
vi.mock('@/lib/api/agent-definitions.api', () => ({
  agentDefinitionsApi: {
    getAll: vi.fn().mockResolvedValue([]),
    delete: vi.fn().mockResolvedValue(undefined),
    startTesting: vi.fn().mockResolvedValue(undefined),
    publish: vi.fn().mockResolvedValue(undefined),
    unpublish: vi.fn().mockResolvedValue(undefined),
  },
}));

// Mock the httpClient used internally by the API (avoids initialization errors)
vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn().mockImplementation(() => ({
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  })),
}));

// Mock the builder component to avoid deep deps
vi.mock('@/app/admin/(dashboard)/agents/builder/BuilderClient', () => ({
  BuilderClient: () => <div data-testid="builder-client">Builder</div>,
}));

// Mock table/filter components to keep the test focused
vi.mock('@/components/admin/agent-definitions/BuilderTable', () => ({
  BuilderTable: () => <div data-testid="builder-table" />,
}));

vi.mock('@/components/admin/agent-definitions/BuilderFilters', () => ({
  BuilderFilters: () => <div data-testid="builder-filters" />,
}));

// Mock sonner toast
vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('Definitions — Builder Sheet', () => {
  it('has a Strategy Builder button', async () => {
    const { default: DefinitionsPage } =
      await import('@/app/admin/(dashboard)/agents/definitions/page');
    render(<DefinitionsPage />, { wrapper: Wrapper });
    expect(await screen.findByRole('button', { name: /strategy builder/i })).toBeInTheDocument();
  });
});
