/**
 * LibraryQuotaSection Component Tests (Issue #2861)
 *
 * Test Coverage:
 * - Loading state (skeleton)
 * - Error state (alert)
 * - Empty/null data state
 * - Quota display with various states
 * - Near limit warning (>=80%)
 * - At limit warning (100%)
 * - Singular/plural text handling
 * - Navigation link
 * - Accessibility
 *
 * Target: >=85% coverage
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { LibraryQuotaSection } from '../LibraryQuotaSection';
import * as useLibraryModule from '@/hooks/queries/useLibrary';
import type { LibraryQuotaResponse } from '@/lib/api';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/hooks/queries/useLibrary', async importOriginal => {
  const actual = await importOriginal<typeof useLibraryModule>();
  return {
    ...actual,
    useLibraryQuota: vi.fn(),
  };
});

vi.mock('next/link', () => ({
  default: ({ children, href, className }: { children: React.ReactNode; href: string; className?: string }) => (
    <a href={href} className={className}>{children}</a>
  ),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockQuotaNormal: LibraryQuotaResponse = {
  currentCount: 25,
  maxAllowed: 100,
  userTier: 'normal',
  remainingSlots: 75,
  percentageUsed: 25,
};

const mockQuotaNearLimit: LibraryQuotaResponse = {
  currentCount: 85,
  maxAllowed: 100,
  userTier: 'premium',
  remainingSlots: 15,
  percentageUsed: 85,
};

const mockQuotaAtLimit: LibraryQuotaResponse = {
  currentCount: 100,
  maxAllowed: 100,
  userTier: 'premium',
  remainingSlots: 0,
  percentageUsed: 100,
};

const mockQuotaSingleGame: LibraryQuotaResponse = {
  currentCount: 1,
  maxAllowed: 100,
  userTier: 'free',
  remainingSlots: 99,
  percentageUsed: 1,
};

const mockQuotaSingleSlot: LibraryQuotaResponse = {
  currentCount: 99,
  maxAllowed: 100,
  userTier: 'premium',
  remainingSlots: 1,
  percentageUsed: 99,
};

// ============================================================================
// Test Suite
// ============================================================================

describe('LibraryQuotaSection', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false,
        },
      },
    });
    vi.clearAllMocks();
  });

  const renderComponent = () => {
    return render(
      <QueryClientProvider client={queryClient}>
        <LibraryQuotaSection />
      </QueryClientProvider>
    );
  };

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders loading skeleton while loading', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      const { container } = renderComponent();

      expect(screen.getByText('La Mia Libreria')).toBeInTheDocument();
      const skeletons = container.querySelectorAll('.animate-pulse');
      expect(skeletons.length).toBeGreaterThanOrEqual(2);
    });

    it('renders BookOpen icon in loading state', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      const { container } = renderComponent();

      const icon = container.querySelector('svg.lucide-book-open');
      expect(icon).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Error State Tests
  // ============================================================================

  describe('Error State', () => {
    it('renders error alert when query fails', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Network error'),
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByText('La Mia Libreria')).toBeInTheDocument();
      expect(screen.getByText(/Impossibile caricare il quota/)).toBeInTheDocument();
    });

    it('renders error when data is null', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: null,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByText(/Impossibile caricare il quota/)).toBeInTheDocument();
    });

    it('renders AlertCircle icon in error state', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Error'),
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      const { container } = renderComponent();

      // AlertCircle is rendered inside the Alert component
      const alertIcon = container.querySelector('svg');
      expect(alertIcon).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Normal State Tests
  // ============================================================================

  describe('Normal State', () => {
    it('renders game count correctly', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByText('25')).toBeInTheDocument();
      expect(screen.getByText('giochi')).toBeInTheDocument();
    });

    it('renders remaining slots', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByText('75 slot disponibili')).toBeInTheDocument();
    });

    it('links to library page', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const link = screen.getByRole('link');
      expect(link).toHaveAttribute('href', '/library');
    });

    it('renders ChevronRight icon', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      const { container } = renderComponent();

      const icon = container.querySelector('svg.lucide-chevron-right');
      expect(icon).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Singular/Plural Tests
  // ============================================================================

  describe('Singular/Plural Text', () => {
    it('renders "gioco" (singular) for 1 game', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaSingleGame,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByText('gioco')).toBeInTheDocument();
    });

    it('renders "giochi" (plural) for multiple games', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByText('giochi')).toBeInTheDocument();
    });

    it('renders "1 slot disponibile" (singular) for 1 slot', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaSingleSlot,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByText('1 slot disponibile')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Near Limit State Tests
  // ============================================================================

  describe('Near Limit State (>=80%)', () => {
    it('renders warning text when near limit', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNearLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByText('15 slot disponibili')).toBeInTheDocument();
    });

    it('applies yellow styling when near limit', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNearLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const statusText = screen.getByText('15 slot disponibili');
      expect(statusText).toHaveClass('text-yellow-600');
    });

    it('applies yellow border to card when near limit', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNearLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      const { container } = renderComponent();

      const card = container.querySelector('.border-yellow-500\\/50');
      expect(card).toBeInTheDocument();
    });
  });

  // ============================================================================
  // At Limit State Tests
  // ============================================================================

  describe('At Limit State (100%)', () => {
    it('renders "Limite raggiunto" when at limit', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaAtLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByText('Limite raggiunto')).toBeInTheDocument();
    });

    it('applies destructive styling when at limit', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaAtLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const statusText = screen.getByText('Limite raggiunto');
      expect(statusText).toHaveClass('text-destructive');
    });

    it('applies destructive border to card when at limit', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaAtLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      const { container } = renderComponent();

      const card = container.querySelector('.border-destructive\\/50');
      expect(card).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('card is a link and accessible', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const link = screen.getByRole('link');
      expect(link).toBeInTheDocument();
    });

    it('has CardTitle component', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByText('La Mia Libreria')).toBeInTheDocument();
    });

    it('applies hover transform class', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const link = screen.getByRole('link');
      expect(link).toHaveClass('hover:scale-[1.02]');
    });
  });

  // ============================================================================
  // Hook Integration Tests
  // ============================================================================

  describe('Hook Integration', () => {
    it('calls useLibraryQuota hook', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(useLibraryModule.useLibraryQuota).toHaveBeenCalled();
    });
  });
});
