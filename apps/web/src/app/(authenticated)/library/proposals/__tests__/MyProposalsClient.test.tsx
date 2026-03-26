/**
 * MyProposalsClient Component Tests (Issue #4054)
 *
 * Tests for proposals dashboard with real API integration.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { axe } from 'jest-axe';

import MyProposalsClient from '../MyProposalsClient';

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({
    push: vi.fn(),
    back: vi.fn(),
    replace: vi.fn(),
    prefetch: vi.fn(),
    refresh: vi.fn(),
  })),
  usePathname: vi.fn(() => '/test'),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));

import type { PaginatedShareRequestsResponse, RateLimitStatusDto } from '@/lib/api';

// Mock the hooks
vi.mock('@/hooks/queries/useShareRequests', () => ({
  useShareRequests: vi.fn(),
  useRateLimitStatus: vi.fn(),
}));

// Mock Next.js Link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

import { useShareRequests, useRateLimitStatus } from '@/hooks/queries/useShareRequests';

const mockUseShareRequests = vi.mocked(useShareRequests);
const mockUseRateLimitStatus = vi.mocked(useRateLimitStatus);

const MOCK_PROPOSALS: PaginatedShareRequestsResponse = {
  items: [
    {
      id: 'req-1',
      sourceGameId: 'game-1',
      gameTitle: 'Test Game 1',
      gameThumbnailUrl: null,
      status: 'Pending',
      contributionType: 'PrivateGame',
      userNotes: 'Test notes',
      adminFeedback: null,
      attachedDocumentCount: 0,
      createdAt: '2026-02-10T10:00:00Z',
      resolvedAt: null,
      resultingSharedGameId: null,
    },
    {
      id: 'req-2',
      sourceGameId: 'game-2',
      gameTitle: 'Test Game 2',
      gameThumbnailUrl: null,
      status: 'Approved',
      contributionType: 'PrivateGame',
      userNotes: null,
      adminFeedback: 'Great addition!',
      attachedDocumentCount: 0,
      createdAt: '2026-02-09T10:00:00Z',
      resolvedAt: '2026-02-11T12:00:00Z',
      resultingSharedGameId: 'shared-123',
    },
  ],
  total: 2,
  page: 1,
  pageSize: 20,
};

const MOCK_RATE_LIMIT: RateLimitStatusDto = {
  currentPendingCount: 1,
  maxPendingAllowed: 5,
  currentMonthlyCount: 3,
  maxMonthlyAllowed: 10,
  isInCooldown: false,
  cooldownEndsAt: null,
  monthResetAt: '2026-03-01T00:00:00Z',
};

describe('MyProposalsClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseShareRequests.mockReturnValue({
      data: MOCK_PROPOSALS,
      isLoading: false,
      error: null,
    } as any);
    mockUseRateLimitStatus.mockReturnValue({
      data: MOCK_RATE_LIMIT,
      isLoading: false,
      error: null,
    } as any);
  });

  describe('Rendering', () => {
    it('renders page description', () => {
      render(<MyProposalsClient />);
      expect(screen.getByText(/Traccia le tue proposte/i)).toBeInTheDocument();
    });

    it('renders proposal cards when data available', () => {
      render(<MyProposalsClient />);
      expect(screen.getByText('Test Game 1')).toBeInTheDocument();
      expect(screen.getByText('Test Game 2')).toBeInTheDocument();
    });
  });

  describe('Loading State', () => {
    it('shows loading spinner', () => {
      mockUseShareRequests.mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as any);

      render(<MyProposalsClient />);
      expect(screen.getByText('Caricamento proposte...')).toBeInTheDocument();
    });
  });

  describe('Error State', () => {
    it('shows error card on API failure', () => {
      mockUseShareRequests.mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Network error'),
      } as any);

      render(<MyProposalsClient />);
      expect(screen.getByText('Errore di Caricamento')).toBeInTheDocument();
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });

    it('has retry button on error', () => {
      mockUseShareRequests.mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Failed'),
      } as any);

      render(<MyProposalsClient />);
      expect(screen.getByText('Riprova')).toBeInTheDocument();
    });
  });

  describe('Empty State', () => {
    it('shows empty state when no proposals', () => {
      mockUseShareRequests.mockReturnValue({
        data: { items: [], total: 0, page: 1, pageSize: 20 },
        isLoading: false,
        error: null,
      } as any);

      render(<MyProposalsClient />);
      expect(screen.getByText('Nessuna Proposta')).toBeInTheDocument();
    });

    it('shows link to private games in empty state', () => {
      mockUseShareRequests.mockReturnValue({
        data: { items: [], total: 0, page: 1, pageSize: 20 },
        isLoading: false,
        error: null,
      } as any);

      render(<MyProposalsClient />);
      expect(screen.getByText('Vai ai Giochi Privati')).toBeInTheDocument();
    });
  });

  describe('Status Badges', () => {
    it('shows Italian status labels', () => {
      render(<MyProposalsClient />);
      expect(screen.getByText('In Attesa')).toBeInTheDocument(); // Pending
      expect(screen.getByText('Approvata')).toBeInTheDocument(); // Approved
    });

    it('uses correct colors for Pending status', () => {
      render(<MyProposalsClient />);
      const badge = screen.getByText('In Attesa');
      expect(badge).toHaveClass('bg-yellow-100', 'text-yellow-800');
    });

    it('uses correct colors for Approved status', () => {
      render(<MyProposalsClient />);
      const badge = screen.getByText('Approvata');
      expect(badge).toHaveClass('bg-green-100', 'text-green-800');
    });
  });

  describe('Proposal Details', () => {
    it('shows game title', () => {
      render(<MyProposalsClient />);
      expect(screen.getByText('Test Game 1')).toBeInTheDocument();
      expect(screen.getByText('Test Game 2')).toBeInTheDocument();
    });

    it('shows submission date', () => {
      render(<MyProposalsClient />);
      const dates = screen.getAllByText(/Inviata il/i);
      expect(dates.length).toBeGreaterThan(0); // At least one proposal shows date
    });

    it('shows resolution date for resolved proposals', () => {
      render(<MyProposalsClient />);
      expect(screen.getByText(/Risolta il/i)).toBeInTheDocument();
    });

    it('shows user notes when present', () => {
      render(<MyProposalsClient />);
      expect(screen.getByText('Test notes')).toBeInTheDocument();
    });

    it('shows admin feedback when present', () => {
      render(<MyProposalsClient />);
      expect(screen.getByText('Feedback Amministratore:')).toBeInTheDocument();
      expect(screen.getByText('Great addition!')).toBeInTheDocument();
    });
  });

  describe('Actions', () => {
    it('shows catalog link for approved proposals', () => {
      render(<MyProposalsClient />);
      const catalogLink = screen.getByText('Vedi nel Catalogo');
      expect(catalogLink.closest('a')).toHaveAttribute('href', '/library/games/shared-123');
    });

    it('does not show catalog link for pending proposals', () => {
      render(<MyProposalsClient />);
      const detailsLinks = screen.getAllByText('Dettagli');
      expect(detailsLinks).toHaveLength(2); // Both proposals have details link
      expect(screen.getByText('Vedi nel Catalogo')).toBeInTheDocument(); // Only 1 catalog link
    });

    it('shows details link for all proposals', () => {
      render(<MyProposalsClient />);
      const detailsLinks = screen.getAllByText('Dettagli');
      expect(detailsLinks).toHaveLength(2);
    });
  });

  describe('Rate Limit Banner', () => {
    it('shows rate limit status', () => {
      render(<MyProposalsClient />);
      expect(screen.getByText('Limiti di Proposta')).toBeInTheDocument();
    });

    it('shows pending quota usage', () => {
      render(<MyProposalsClient />);
      expect(screen.getByText('Proposte Pendenti')).toBeInTheDocument();
      expect(screen.getByText('1 / 5')).toBeInTheDocument();
    });

    it('shows monthly quota usage', () => {
      render(<MyProposalsClient />);
      expect(screen.getByText('Proposte Mensili')).toBeInTheDocument();
      expect(screen.getByText('3 / 10')).toBeInTheDocument();
    });

    it('shows progress bars with aria-labels', () => {
      render(<MyProposalsClient />);
      const progressBars = screen.getAllByRole('progressbar');
      expect(progressBars).toHaveLength(2);
      expect(progressBars[0]).toHaveAttribute('aria-label');
    });

    it('shows warning when pending usage >= 80%', () => {
      mockUseRateLimitStatus.mockReturnValue({
        data: {
          ...MOCK_RATE_LIMIT,
          currentPendingCount: 4,
          maxPendingAllowed: 5,
        },
        isLoading: false,
        error: null,
      } as any);

      const { container } = render(<MyProposalsClient />);
      const banner = container.querySelector('.border-orange-500');
      expect(banner).toBeInTheDocument();
    });

    it('shows destructive state when in cooldown', () => {
      mockUseRateLimitStatus.mockReturnValue({
        data: {
          ...MOCK_RATE_LIMIT,
          isInCooldown: true,
          cooldownEndsAt: '2026-02-15T10:00:00Z',
        },
        isLoading: false,
        error: null,
      } as any);

      render(<MyProposalsClient />);
      expect(screen.getByText('Cooldown Attivo')).toBeInTheDocument();
      expect(screen.getByText(/Cooldown termina il/i)).toBeInTheDocument();
    });

    it('shows error when pending limit reached', () => {
      mockUseRateLimitStatus.mockReturnValue({
        data: {
          ...MOCK_RATE_LIMIT,
          currentPendingCount: 5,
          maxPendingAllowed: 5,
        },
        isLoading: false,
        error: null,
      } as any);

      render(<MyProposalsClient />);
      expect(screen.getByText(/Limite raggiunto/i)).toBeInTheDocument();
    });

    it('shows month reset date', () => {
      render(<MyProposalsClient />);
      expect(screen.getByText(/Reset:/i)).toBeInTheDocument();
    });
  });

  describe('Pagination', () => {
    it('shows pagination info when items < total', () => {
      mockUseShareRequests.mockReturnValue({
        data: {
          ...MOCK_PROPOSALS,
          total: 15,
        },
        isLoading: false,
        error: null,
      } as any);

      render(<MyProposalsClient />);
      expect(screen.getByText('Mostrate 2 di 15 proposte')).toBeInTheDocument();
    });

    it('does not show pagination info when all items shown', () => {
      render(<MyProposalsClient />);
      expect(screen.queryByText(/Mostrate.*proposte/)).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has no axe violations with data', async () => {
      const { container } = render(<MyProposalsClient />);
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('has no axe violations in empty state', async () => {
      mockUseShareRequests.mockReturnValue({
        data: { items: [], total: 0, page: 1, pageSize: 20 },
        isLoading: false,
        error: null,
      } as any);

      const { container } = render(<MyProposalsClient />);
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('has no axe violations with rate limit warning', async () => {
      mockUseRateLimitStatus.mockReturnValue({
        data: {
          ...MOCK_RATE_LIMIT,
          currentPendingCount: 4,
        },
        isLoading: false,
        error: null,
      } as any);

      const { container } = render(<MyProposalsClient />);
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });
});
