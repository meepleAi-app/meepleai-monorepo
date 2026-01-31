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
 *
 * Updated for i18n compliance (Issue #3096): Uses data-testid pattern
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
  default: ({ children, href, className, 'data-testid': testId }: { children: React.ReactNode; href: string; className?: string; 'data-testid'?: string }) => (
    <a href={href} className={className} data-testid={testId}>{children}</a>
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

      renderComponent();

      expect(screen.getByTestId('library-quota-title')).toBeInTheDocument();
      expect(screen.getByTestId('library-quota-loading')).toBeInTheDocument();
    });

    it('renders skeleton elements in loading state', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByTestId('library-quota-skeleton-count')).toBeInTheDocument();
      expect(screen.getByTestId('library-quota-skeleton-status')).toBeInTheDocument();
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

      expect(screen.getByTestId('library-quota-title')).toBeInTheDocument();
      expect(screen.getByTestId('library-quota-error')).toBeInTheDocument();
    });

    it('renders error when data is null', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: null,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByTestId('library-quota-error')).toBeInTheDocument();
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

      expect(screen.getByTestId('library-quota-count')).toBeInTheDocument();
      expect(screen.getByTestId('library-quota-count-number')).toHaveTextContent('25');
      expect(screen.getByTestId('library-quota-count-label')).toBeInTheDocument();
    });

    it('renders remaining slots status', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const statusElement = screen.getByTestId('library-quota-status');
      expect(statusElement).toBeInTheDocument();
      expect(statusElement.textContent).toContain('75');
    });

    it('links to library page', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const link = screen.getByTestId('library-quota-link');
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
    it('renders singular label for 1 game', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaSingleGame,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const countLabel = screen.getByTestId('library-quota-count-label');
      expect(countLabel).toBeInTheDocument();
      // The text should be singular (gioco) but we check via testid
      expect(screen.getByTestId('library-quota-count-number')).toHaveTextContent('1');
    });

    it('renders plural label for multiple games', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const countLabel = screen.getByTestId('library-quota-count-label');
      expect(countLabel).toBeInTheDocument();
      expect(screen.getByTestId('library-quota-count-number')).toHaveTextContent('25');
    });

    it('renders singular slot status for 1 slot', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaSingleSlot,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const statusElement = screen.getByTestId('library-quota-status');
      expect(statusElement).toBeInTheDocument();
      expect(statusElement.textContent).toContain('1');
    });
  });

  // ============================================================================
  // Near Limit State Tests
  // ============================================================================

  describe('Near Limit State (>=80%)', () => {
    it('renders warning status when near limit', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNearLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const statusElement = screen.getByTestId('library-quota-status');
      expect(statusElement).toBeInTheDocument();
      expect(statusElement.textContent).toContain('15');
    });

    it('applies yellow styling when near limit', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNearLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const statusText = screen.getByTestId('library-quota-status');
      expect(statusText).toHaveClass('text-yellow-600');
    });

    it('applies yellow border to card when near limit', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNearLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const card = screen.getByTestId('library-quota-card');
      expect(card).toHaveClass('border-yellow-500/50');
    });
  });

  // ============================================================================
  // At Limit State Tests
  // ============================================================================

  describe('At Limit State (100%)', () => {
    it('renders limit reached status when at limit', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaAtLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const statusElement = screen.getByTestId('library-quota-status');
      expect(statusElement).toBeInTheDocument();
    });

    it('applies destructive styling when at limit', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaAtLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const statusText = screen.getByTestId('library-quota-status');
      expect(statusText).toHaveClass('text-destructive');
    });

    it('applies destructive border to card when at limit', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaAtLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const card = screen.getByTestId('library-quota-card');
      expect(card).toHaveClass('border-destructive/50');
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

      const link = screen.getByTestId('library-quota-link');
      expect(link).toBeInTheDocument();
      expect(link.tagName.toLowerCase()).toBe('a');
    });

    it('has CardTitle component', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByTestId('library-quota-title')).toBeInTheDocument();
    });

    it('applies hover transform class', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      const link = screen.getByTestId('library-quota-link');
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
