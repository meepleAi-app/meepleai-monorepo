/**
 * KB Admin Hub — Gap fixes tests
 * Verifies: Embedding Service card, Usage & Costs quick link, vector search panel,
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

const mockVectorStats = {
  totalVectors: 1500,
  dimensions: 768,
  gamesIndexed: 2,
  avgHealthPercent: 95,
  sizeEstimateBytes: 15728640,
  gameBreakdown: [
    {
      gameId: 'abc',
      gameName: 'Catan',
      vectorCount: 1000,
      completedCount: 950,
      failedCount: 50,
      healthPercent: 95.0,
    },
    {
      gameId: 'xyz',
      gameName: 'Ticket to Ride',
      vectorCount: 500,
      completedCount: 480,
      failedCount: 20,
      healthPercent: 96.0,
    },
  ],
};

const mockAdminClient = {
  getVectorStats: vi.fn().mockResolvedValue(mockVectorStats),
  searchVectors: vi.fn().mockResolvedValue({
    results: [
      { documentId: 'doc1', text: 'This is the first result', chunkIndex: 0, pageNumber: 1 },
      { documentId: 'doc2', text: 'Second result content', chunkIndex: 1, pageNumber: 2 },
    ],
    errorMessage: null,
  }),
  getPipelineHealth: vi.fn().mockResolvedValue(mockPipelineData),
};

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: vi.fn(() => mockAdminClient),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn().mockImplementation(() => ({})),
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

// ── Vector Search Panel tests ─────────────────────────────────────────────────

describe('VectorStorePage — Semantic Search Panel', () => {
  beforeEach(async () => {
    vi.clearAllMocks();

    // Reset admin client mocks to defaults
    mockAdminClient.getVectorStats.mockResolvedValue(mockVectorStats);
    mockAdminClient.searchVectors.mockResolvedValue({
      results: [
        { documentId: 'doc1', text: 'First result', chunkIndex: 0, pageNumber: 1 },
        { documentId: 'doc2', text: 'Second result', chunkIndex: 1, pageNumber: 2 },
      ],
      errorMessage: null,
    });

    // Make useQuery return vector stats
    const rq = await import('@tanstack/react-query');
    vi.mocked(rq.useQuery).mockReturnValue({
      data: mockVectorStats,
      isLoading: false,
      error: null,
      refetch: vi.fn(),
      isRefetching: false,
    } as ReturnType<typeof rq.useQuery>);
  });

  it('renders the Semantic Search section heading', async () => {
    const { default: VectorStorePage } = await import('../vectors/page');
    render(<VectorStorePage />);

    expect(screen.getByText('Semantic Search')).toBeDefined();
  });

  it('renders search input and Search button', async () => {
    const { default: VectorStorePage } = await import('../vectors/page');
    render(<VectorStorePage />);

    const searchInput = screen.getByPlaceholderText('Enter a query to search vectors...');
    expect(searchInput).toBeDefined();

    const searchBtn = screen.getByRole('button', { name: /^search$/i });
    expect(searchBtn).toBeDefined();
  });

  it('disables Search button when query is empty', async () => {
    const { default: VectorStorePage } = await import('../vectors/page');
    render(<VectorStorePage />);

    const searchBtn = screen.getByRole('button', { name: /^search$/i });
    expect(searchBtn).toHaveProperty('disabled', true);
  });

  it('enables Search button when query is provided', async () => {
    const { default: VectorStorePage } = await import('../vectors/page');
    render(<VectorStorePage />);

    // Type in query — that is enough to enable the button (no collection required)
    const searchInput = screen.getByPlaceholderText('Enter a query to search vectors...');
    fireEvent.change(searchInput, { target: { value: 'board game rules' } });

    const searchBtn = screen.getByRole('button', { name: /^search$/i });
    expect(searchBtn).toHaveProperty('disabled', false);
  });

  it('shows "No results found" message when results are empty', async () => {
    mockAdminClient.searchVectors.mockResolvedValueOnce({
      results: [],
      errorMessage: null,
    });

    const { default: VectorStorePage } = await import('../vectors/page');
    render(<VectorStorePage />);

    const searchInput = screen.getByPlaceholderText('Enter a query to search vectors...');
    fireEvent.change(searchInput, { target: { value: 'no match' } });

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
