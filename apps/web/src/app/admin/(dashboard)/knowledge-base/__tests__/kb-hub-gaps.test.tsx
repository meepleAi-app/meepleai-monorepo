/**
 * KB Admin Hub — Gap fixes tests
 * Verifies: Embedding Service card, Usage & Costs quick link, Qdrant search panel,
 * RAGPipelineFlow stage drill-down.
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';

// ── Mock Next.js Link ─────────────────────────────────────────────────────────

vi.mock('next/link', () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  ),
}));

// ── Mock lucide-react icons used in hub ──────────────────────────────────────

vi.mock('lucide-react', async importOriginal => {
  const actual = await importOriginal<typeof import('lucide-react')>();
  return {
    ...actual,
    // Keep all, just need them to render
  };
});

// ── Mock Card components ──────────────────────────────────────────────────────

vi.mock('@/components/ui/data-display/card', () => ({
  Card: ({ children, className }: { children: React.ReactNode; className?: string }) => (
    <div className={className} data-testid="card">
      {children}
    </div>
  ),
  CardContent: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  CardHeader: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  CardTitle: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

// ── Pipeline data (must be defined before mockAdminClient) ────────────────────

const mockPipelineData = {
  stages: [
    {
      name: 'Ingest',
      status: 'healthy' as const,
      metrics: { activeJobs: 2, queuedJobs: 5 },
    },
    {
      name: 'Extract',
      status: 'warning' as const,
      metrics: { avgDurationMs: '1200', totalProcessed: 42 },
    },
    {
      name: 'Embed',
      status: 'error' as const,
      metrics: { requestsTotal: 0, failureRate: 100 },
    },
  ],
  summary: { healthyCount: 1, warningCount: 1, errorCount: 1 },
  recentActivity: [],
  distribution: null,
};

// ── Admin client mock (single, shared across all suites) ──────────────────────

const mockAdminClient = {
  getVectorCollections: vi.fn().mockResolvedValue({
    collections: [
      { name: 'game_rules', vectorCount: 1000, dimensions: 384, storage: '10 MB', health: 95 },
      { name: 'faq_docs', vectorCount: 500, dimensions: 384, storage: '5 MB', health: 80 },
    ],
  }),
  searchQdrantCollection: vi.fn().mockResolvedValue({
    query: 'test query',
    results: [
      { id: 1, score: 0.92, payload: { text: 'This is the first result', gameId: 'abc' } },
      { id: 2, score: 0.65, payload: { text: 'Second result content', gameId: 'xyz' } },
    ],
    total: 2,
  }),
  deleteQdrantCollection: vi.fn(),
  rebuildQdrantIndex: vi.fn(),
  getPipelineHealth: vi.fn().mockResolvedValue(mockPipelineData),
};

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: vi.fn(() => mockAdminClient),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn().mockImplementation(() => ({})),
}));

vi.mock('@/components/admin/knowledge-base/vector-collection-card', () => ({
  VectorCollectionCard: ({ name }: { name: string }) => (
    <div data-testid={`collection-card-${name}`}>{name}</div>
  ),
}));

vi.mock('@/components/ui/primitives/button', () => ({
  Button: ({
    children,
    onClick,
    disabled,
  }: {
    children: React.ReactNode;
    onClick?: () => void;
    disabled?: boolean;
  }) => (
    <button onClick={onClick} disabled={disabled}>
      {children}
    </button>
  ),
}));

vi.mock('@/components/ui/primitives/input', () => ({
  Input: ({
    value,
    onChange,
    onKeyDown,
    placeholder,
  }: {
    value: string;
    onChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
    onKeyDown?: (e: React.KeyboardEvent<HTMLInputElement>) => void;
    placeholder?: string;
  }) => <input value={value} onChange={onChange} onKeyDown={onKeyDown} placeholder={placeholder} />,
}));

vi.mock('@/components/ui/overlays/select', () => ({
  Select: ({
    value,
    onValueChange,
    children,
  }: {
    value: string;
    onValueChange: (v: string) => void;
    children: React.ReactNode;
  }) => (
    <select value={value} onChange={e => onValueChange(e.target.value)} data-testid="select">
      {children}
    </select>
  ),
  SelectTrigger: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  SelectValue: ({ placeholder }: { placeholder?: string }) => (
    <option value="">{placeholder}</option>
  ),
  SelectContent: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  SelectItem: ({ value, children }: { value: string; children: React.ReactNode }) => (
    <option value={value}>{children}</option>
  ),
}));

vi.mock('@tanstack/react-query', async importOriginal => {
  const actual = await importOriginal<typeof import('@tanstack/react-query')>();
  return {
    ...actual,
    useQuery: vi.fn(() => ({
      data: undefined,
      isLoading: false,
      error: null,
      refetch: vi.fn(),
      isRefetching: false,
    })),
    useMutation: vi.fn(() => ({
      mutate: vi.fn(),
      isPending: false,
    })),
    useQueryClient: vi.fn(() => ({
      invalidateQueries: vi.fn(),
    })),
  };
});

// ── Hub page tests ────────────────────────────────────────────────────────────

function renderWithQueryClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('KnowledgeBasePage — Hub Gap Fixes', () => {
  it('renders Embedding Service card with correct link', async () => {
    const { default: KnowledgeBasePage } = await import('../page');
    renderWithQueryClient(<KnowledgeBasePage />);

    const embeddingLink = screen.getByRole('link', { name: /embedding service/i });
    expect(embeddingLink).toBeDefined();
    expect(embeddingLink.getAttribute('href')).toBe('/admin/knowledge-base/embedding');
  });

  it('renders Usage & Costs quick link', async () => {
    const { default: KnowledgeBasePage } = await import('../page');
    renderWithQueryClient(<KnowledgeBasePage />);

    const usageLink = screen.getByRole('link', { name: /usage.*costs/i });
    expect(usageLink).toBeDefined();
    expect(usageLink.getAttribute('href')).toBe('/admin/agents/usage');
  });

  it('renders all 7 section cards (original 6 + Embedding)', async () => {
    const { default: KnowledgeBasePage } = await import('../page');
    renderWithQueryClient(<KnowledgeBasePage />);

    const expectedSections = [
      'Documents',
      'Vector Collections',
      'Processing Queue',
      'Upload & Process',
      'RAG Pipeline',
      'Settings',
      'Embedding Service',
    ];

    for (const section of expectedSections) {
      expect(screen.getByText(section)).toBeDefined();
    }
  });

  it('renders 5 quick links including the new Usage & Costs', async () => {
    const { default: KnowledgeBasePage } = await import('../page');
    renderWithQueryClient(<KnowledgeBasePage />);

    const expectedLinks = [
      { text: /rag executions log/i, href: '/admin/agents/analytics' },
      { text: /ai models/i, href: '/admin/agents/models' },
      { text: /strategy config/i, href: '/admin/agents/strategy' },
      { text: /kb settings/i, href: '/admin/knowledge-base/settings' },
      { text: /usage.*costs/i, href: '/admin/agents/usage' },
    ];

    for (const { text, href } of expectedLinks) {
      const link = screen.getByRole('link', { name: text });
      expect(link.getAttribute('href')).toBe(href);
    }
  });
});

// ── Qdrant Search Panel tests ─────────────────────────────────────────────────

describe('VectorCollectionsPage — Semantic Search Panel', () => {
  beforeEach(async () => {
    vi.clearAllMocks();

    // Reset admin client mocks to defaults
    mockAdminClient.getVectorCollections.mockResolvedValue({
      collections: [
        { name: 'game_rules', vectorCount: 1000, dimensions: 384, storage: '10 MB', health: 95 },
        { name: 'faq_docs', vectorCount: 500, dimensions: 384, storage: '5 MB', health: 80 },
      ],
    });
    mockAdminClient.searchQdrantCollection.mockResolvedValue({
      query: 'test query',
      results: [
        { id: 1, score: 0.92, payload: { text: 'First result', gameId: 'abc' } },
        { id: 2, score: 0.65, payload: { text: 'Second result', gameId: 'xyz' } },
      ],
      total: 2,
    });

    // Make useQuery return collections so the collection <select> has real <option> elements
    const rq = await import('@tanstack/react-query');
    vi.mocked(rq.useQuery).mockReturnValue({
      data: {
        collections: [
          { name: 'game_rules', vectorCount: 1000, dimensions: 384, storage: '10 MB', health: 95 },
          { name: 'faq_docs', vectorCount: 500, dimensions: 384, storage: '5 MB', health: 80 },
        ],
      },
      isLoading: false,
      error: null,
      refetch: vi.fn(),
      isRefetching: false,
    } as ReturnType<typeof rq.useQuery>);
  });

  it('renders the Semantic Search section heading', async () => {
    const { default: VectorCollectionsPage } = await import('../vectors/page');
    render(<VectorCollectionsPage />);

    expect(screen.getByText('Semantic Search')).toBeDefined();
  });

  it('renders search input and Search button', async () => {
    const { default: VectorCollectionsPage } = await import('../vectors/page');
    render(<VectorCollectionsPage />);

    const searchInput = screen.getByPlaceholderText('Enter a query to search vectors...');
    expect(searchInput).toBeDefined();

    const searchBtn = screen.getByRole('button', { name: /^search$/i });
    expect(searchBtn).toBeDefined();
  });

  it('disables Search button when query is empty', async () => {
    const { default: VectorCollectionsPage } = await import('../vectors/page');
    render(<VectorCollectionsPage />);

    const searchBtn = screen.getByRole('button', { name: /^search$/i });
    expect(searchBtn).toHaveProperty('disabled', true);
  });

  it('enables Search button when query and collection are provided', async () => {
    const { default: VectorCollectionsPage } = await import('../vectors/page');
    render(<VectorCollectionsPage />);

    // Type in query
    const searchInput = screen.getByPlaceholderText('Enter a query to search vectors...');
    fireEvent.change(searchInput, { target: { value: 'board game rules' } });

    // Select collection — selects[0] is collection, selects[1] is limit
    const selects = screen.getAllByTestId('select');
    fireEvent.change(selects[0], { target: { value: 'game_rules' } });

    const searchBtn = screen.getByRole('button', { name: /^search$/i });
    expect(searchBtn).toHaveProperty('disabled', false);
  });

  it('shows "No results found" message when results are empty', async () => {
    mockAdminClient.searchQdrantCollection.mockResolvedValueOnce({
      query: 'no match',
      results: [],
      total: 0,
    });

    const { default: VectorCollectionsPage } = await import('../vectors/page');
    render(<VectorCollectionsPage />);

    const searchInput = screen.getByPlaceholderText('Enter a query to search vectors...');
    fireEvent.change(searchInput, { target: { value: 'no match' } });

    const selects = screen.getAllByTestId('select');
    fireEvent.change(selects[0], { target: { value: 'game_rules' } });

    const searchBtn = screen.getByRole('button', { name: /^search$/i });
    fireEvent.click(searchBtn);

    await waitFor(() => {
      expect(screen.getByText('No results found for this query.')).toBeDefined();
    });
  });
});

// ── RAGPipelineFlow drill-down tests ─────────────────────────────────────────

describe('RAGPipelineFlow — Stage Drill-Down', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders stage buttons as clickable', async () => {
    const { RAGPipelineFlow } = await import('@/components/admin/knowledge-base/rag-pipeline-flow');
    const { useQuery } = await import('@tanstack/react-query');
    vi.mocked(useQuery).mockReturnValue({
      data: mockPipelineData,
      isLoading: false,
      refetch: vi.fn(),
      isRefetching: false,
      error: null,
    } as ReturnType<typeof useQuery>);

    render(<RAGPipelineFlow />);

    // Stage names should be in buttons
    const ingestBtn = screen.getByRole('button', { name: /ingest/i });
    expect(ingestBtn).toBeDefined();
  });

  it('shows drill-down panel on stage click with metric details', async () => {
    const { RAGPipelineFlow } = await import('@/components/admin/knowledge-base/rag-pipeline-flow');
    const { useQuery } = await import('@tanstack/react-query');
    vi.mocked(useQuery).mockReturnValue({
      data: mockPipelineData,
      isLoading: false,
      refetch: vi.fn(),
      isRefetching: false,
      error: null,
    } as ReturnType<typeof useQuery>);

    render(<RAGPipelineFlow />);

    // Click on Ingest stage
    const ingestBtn = screen.getByRole('button', { name: /ingest/i });
    fireEvent.click(ingestBtn);

    // Drill-down panel should appear with stage detail heading
    await waitFor(() => {
      expect(screen.getByText(/Ingest — Stage Details/i)).toBeDefined();
    });

    // Should show metric values
    expect(screen.getByText('2')).toBeDefined(); // activeJobs value
    expect(screen.getByText('5')).toBeDefined(); // queuedJobs value
  });

  it('collapses drill-down panel when same stage clicked again', async () => {
    const { RAGPipelineFlow } = await import('@/components/admin/knowledge-base/rag-pipeline-flow');
    const { useQuery } = await import('@tanstack/react-query');
    vi.mocked(useQuery).mockReturnValue({
      data: mockPipelineData,
      isLoading: false,
      refetch: vi.fn(),
      isRefetching: false,
      error: null,
    } as ReturnType<typeof useQuery>);

    render(<RAGPipelineFlow />);

    const ingestBtn = screen.getByRole('button', { name: /ingest/i });

    // Open
    fireEvent.click(ingestBtn);
    await waitFor(() => {
      expect(screen.getByText(/Ingest — Stage Details/i)).toBeDefined();
    });

    // Close (click same)
    fireEvent.click(ingestBtn);
    await waitFor(() => {
      expect(screen.queryByText(/Ingest — Stage Details/i)).toBeNull();
    });
  });

  it('switches drill-down panel when different stage clicked', async () => {
    const { RAGPipelineFlow } = await import('@/components/admin/knowledge-base/rag-pipeline-flow');
    const { useQuery } = await import('@tanstack/react-query');
    vi.mocked(useQuery).mockReturnValue({
      data: mockPipelineData,
      isLoading: false,
      refetch: vi.fn(),
      isRefetching: false,
      error: null,
    } as ReturnType<typeof useQuery>);

    render(<RAGPipelineFlow />);

    // Click Ingest
    fireEvent.click(screen.getByRole('button', { name: /ingest/i }));
    await waitFor(() => {
      expect(screen.getByText(/Ingest — Stage Details/i)).toBeDefined();
    });

    // Click Extract
    fireEvent.click(screen.getByRole('button', { name: /extract/i }));
    await waitFor(() => {
      expect(screen.queryByText(/Ingest — Stage Details/i)).toBeNull();
      expect(screen.getByText(/Extract — Stage Details/i)).toBeDefined();
    });
  });
});
