import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mockGetMechanicDraft = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/knowledge-base/mechanic-extractor/review',
  useSearchParams: () => new URLSearchParams({ sharedGameId: 'sg-1', pdfDocumentId: 'pdf-1' }),
}));

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getMechanicDraft: mockGetMechanicDraft,
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn().mockImplementation(() => ({})),
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('MechanicExtractorReviewPage', () => {
  it('shows "Draft non trovato" when no data returned', async () => {
    mockGetMechanicDraft.mockResolvedValue(null);

    const { default: MechanicExtractorReviewPage } =
      await import('@/app/admin/(dashboard)/knowledge-base/mechanic-extractor/review/page');

    render(<MechanicExtractorReviewPage />, { wrapper: Wrapper });

    expect(await screen.findByText(/Draft non trovato/i)).toBeInTheDocument();
  });

  it('renders game title and stats when draft exists', async () => {
    mockGetMechanicDraft.mockResolvedValue({
      gameTitle: 'Puerto Rico',
      summaryDraft: 'A classic euro game',
      mechanicsDraft: JSON.stringify(['Worker Placement', 'Role Selection']),
      victoryDraft: JSON.stringify({ primary: 'Most victory points' }),
      resourcesDraft: JSON.stringify([]),
      phasesDraft: JSON.stringify([]),
      questionsDraft: JSON.stringify([]),
      totalTokensUsed: 1234,
      estimatedCostUsd: 0.0056,
    });

    const { default: MechanicExtractorReviewPage } =
      await import('@/app/admin/(dashboard)/knowledge-base/mechanic-extractor/review/page');

    render(<MechanicExtractorReviewPage />, { wrapper: Wrapper });

    expect(await screen.findByText(/Puerto Rico/)).toBeInTheDocument();
    expect(screen.getByText('1234')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument(); // 2 mechanics
  });

  it('renders copyright footer with Variant C text', async () => {
    mockGetMechanicDraft.mockResolvedValue({
      gameTitle: 'Catan',
      summaryDraft: 'Trade and build',
      mechanicsDraft: JSON.stringify([]),
      victoryDraft: JSON.stringify({ primary: '' }),
      resourcesDraft: JSON.stringify([]),
      phasesDraft: JSON.stringify([]),
      questionsDraft: JSON.stringify([]),
      totalTokensUsed: 500,
      estimatedCostUsd: 0.002,
    });

    const { default: MechanicExtractorReviewPage } =
      await import('@/app/admin/(dashboard)/knowledge-base/mechanic-extractor/review/page');

    render(<MechanicExtractorReviewPage />, { wrapper: Wrapper });

    expect(await screen.findByText(/Variant C/)).toBeInTheDocument();
    expect(
      screen.getByText(/L'AI non ha mai letto il testo del PDF originale/i)
    ).toBeInTheDocument();
  });
});
