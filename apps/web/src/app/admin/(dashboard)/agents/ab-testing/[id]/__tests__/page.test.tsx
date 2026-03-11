import React from 'react';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AbTestEvaluationPageInner } from '../page';

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents/ab-testing/test-id',
}));

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

vi.mock('../../../NavConfig', () => ({
  AgentsNavConfig: () => null,
}));

const mockGetAbTest = vi.fn();
const mockEvaluateAbTest = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAbTest: (...args: unknown[]) => mockGetAbTest(...args),
      evaluateAbTest: (...args: unknown[]) => mockEvaluateAbTest(...args),
    },
  },
}));

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function createWrapper() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

// ---------------------------------------------------------------------------
// Mock data
// ---------------------------------------------------------------------------

const mockSession = {
  id: 'test-id',
  createdBy: '00000000-0000-0000-0000-000000000001',
  query: 'What are the rules for Catan?',
  status: 'Generated',
  createdAt: '2026-03-10T12:00:00Z',
  completedAt: null,
  totalCost: 0.0042,
  variants: [
    {
      id: '00000000-0000-0000-0000-000000000010',
      label: 'A',
      response: 'Catan is a game about building settlements...',
      tokensUsed: 150,
      latencyMs: 2000,
      costUsd: 0.002,
      failed: false,
    },
    {
      id: '00000000-0000-0000-0000-000000000020',
      label: 'B',
      response: 'In Catan, players collect resources...',
      tokensUsed: 120,
      latencyMs: 1500,
      costUsd: 0.0022,
      failed: false,
    },
  ],
};

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('AbTestEvaluationPageInner', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows loading state initially', () => {
    // Never resolve so the query stays in loading state
    mockGetAbTest.mockReturnValue(new Promise(() => {}));

    render(<AbTestEvaluationPageInner id="test-id" />, {
      wrapper: createWrapper(),
    });

    // The Loader2 spinner has the animate-spin class inside a centering container
    const spinner = document.querySelector('.animate-spin');
    expect(spinner).toBeTruthy();
  });

  it('renders session query and variant responses after loading', async () => {
    mockGetAbTest.mockResolvedValue(mockSession);

    render(<AbTestEvaluationPageInner id="test-id" />, {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(screen.getByText('What are the rules for Catan?')).toBeInTheDocument();
    });

    // Both variant responses should be visible
    expect(screen.getByText('Catan is a game about building settlements...')).toBeInTheDocument();
    expect(screen.getByText('In Catan, players collect resources...')).toBeInTheDocument();
  });

  it('shows "not found" message when session is null', async () => {
    mockGetAbTest.mockResolvedValue(null);

    render(<AbTestEvaluationPageInner id="missing-id" />, {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(screen.getByText('A/B test session not found.')).toBeInTheDocument();
    });
  });

  it('renders star rating buttons for each dimension per variant', async () => {
    mockGetAbTest.mockResolvedValue(mockSession);

    render(<AbTestEvaluationPageInner id="test-id" />, {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(screen.getByText('What are the rules for Catan?')).toBeInTheDocument();
    });

    // 4 dimensions (Accuracy, Completeness, Clarity, Tone) x 2 variants = 8 labels
    const accuracyLabels = screen.getAllByText('Accuracy');
    expect(accuracyLabels).toHaveLength(2);

    const completenessLabels = screen.getAllByText('Completeness');
    expect(completenessLabels).toHaveLength(2);

    const clarityLabels = screen.getAllByText('Clarity');
    expect(clarityLabels).toHaveLength(2);

    const toneLabels = screen.getAllByText('Tone');
    expect(toneLabels).toHaveLength(2);

    // Each dimension has 5 stars per variant: 5 stars x 4 dims x 2 variants = 40 star buttons
    const starButtons = screen.getAllByRole('button', { name: /star/i });
    expect(starButtons).toHaveLength(40);
  });

  it('submit button is disabled when not all variants are scored', async () => {
    mockGetAbTest.mockResolvedValue(mockSession);

    render(<AbTestEvaluationPageInner id="test-id" />, {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(screen.getByText('What are the rules for Catan?')).toBeInTheDocument();
    });

    const submitButton = screen.getByRole('button', {
      name: /submit evaluation/i,
    });
    expect(submitButton).toBeDisabled();

    // Guidance text should indicate scoring is incomplete
    expect(screen.getByText('Score all variants to submit evaluation.')).toBeInTheDocument();
  });

  it('shows variant labels (A, B) and metadata (tokens, latency)', async () => {
    mockGetAbTest.mockResolvedValue(mockSession);

    render(<AbTestEvaluationPageInner id="test-id" />, {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(screen.getByText('What are the rules for Catan?')).toBeInTheDocument();
    });

    // Variant labels rendered as "Variant A" and "Variant B"
    expect(screen.getByText('Variant A')).toBeInTheDocument();
    expect(screen.getByText('Variant B')).toBeInTheDocument();

    // Metadata: latencyMs + tokensUsed shown as "2000ms · 150 tokens" etc.
    expect(screen.getByText(/2000ms/)).toBeInTheDocument();
    expect(screen.getByText(/150 tokens/)).toBeInTheDocument();
    expect(screen.getByText(/1500ms/)).toBeInTheDocument();
    expect(screen.getByText(/120 tokens/)).toBeInTheDocument();
  });
});
