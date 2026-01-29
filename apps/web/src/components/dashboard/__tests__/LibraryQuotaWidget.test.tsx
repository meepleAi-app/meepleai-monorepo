/**
 * LibraryQuotaWidget Component Tests (Issue #2857)
 *
 * Test Coverage:
 * - Loading state (skeleton with progress bar)
 * - Error state (alert)
 * - Success state with various percentages
 * - Color-coded progress bar (green <70%, yellow 70-90%, red >90%)
 * - X/Y games text display
 * - CTA button states (Manage Library vs Upgrade Plan)
 * - Animated progress bar
 * - Accessibility
 *
 * Target: >=85% coverage
 */

import { render, screen, waitFor, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { LibraryQuotaWidget } from '../LibraryQuotaWidget';
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

const mockQuotaLowUsage: LibraryQuotaResponse = {
  currentCount: 25,
  maxAllowed: 100,
  userTier: 'normal',
  remainingSlots: 75,
  percentageUsed: 25,
};

const mockQuotaMediumUsage: LibraryQuotaResponse = {
  currentCount: 75,
  maxAllowed: 100,
  userTier: 'normal',
  remainingSlots: 25,
  percentageUsed: 75,
};

const mockQuotaHighUsage: LibraryQuotaResponse = {
  currentCount: 92,
  maxAllowed: 100,
  userTier: 'premium',
  remainingSlots: 8,
  percentageUsed: 92,
};

const mockQuotaAtLimit: LibraryQuotaResponse = {
  currentCount: 100,
  maxAllowed: 100,
  userTier: 'premium',
  remainingSlots: 0,
  percentageUsed: 100,
};

const mockQuotaAt70Percent: LibraryQuotaResponse = {
  currentCount: 70,
  maxAllowed: 100,
  userTier: 'normal',
  remainingSlots: 30,
  percentageUsed: 70,
};

const mockQuotaAt90Percent: LibraryQuotaResponse = {
  currentCount: 90,
  maxAllowed: 100,
  userTier: 'normal',
  remainingSlots: 10,
  percentageUsed: 90,
};

const mockQuotaEmpty: LibraryQuotaResponse = {
  currentCount: 0,
  maxAllowed: 50,
  userTier: 'free',
  remainingSlots: 50,
  percentageUsed: 0,
};

// ============================================================================
// Test Suite
// ============================================================================

describe('LibraryQuotaWidget', () => {
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
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  const renderComponent = (className?: string) => {
    return render(
      <QueryClientProvider client={queryClient}>
        <LibraryQuotaWidget className={className} />
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

      expect(screen.getByTestId('library-quota-widget')).toBeInTheDocument();
      expect(screen.getByTestId('library-quota-widget-title')).toBeInTheDocument();
      expect(screen.getByTestId('library-quota-widget-loading')).toBeInTheDocument();
    });

    it('renders skeleton progress bar in loading state', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByTestId('library-quota-widget-skeleton-progress')).toBeInTheDocument();
    });

    it('renders skeleton count and percent in loading state', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByTestId('library-quota-widget-skeleton-count')).toBeInTheDocument();
      expect(screen.getByTestId('library-quota-widget-skeleton-percent')).toBeInTheDocument();
    });

    it('renders skeleton CTA in loading state', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByTestId('library-quota-widget-skeleton-cta')).toBeInTheDocument();
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

      expect(screen.getByTestId('library-quota-widget')).toBeInTheDocument();
      expect(screen.getByTestId('library-quota-widget-title')).toBeInTheDocument();
      expect(screen.getByTestId('library-quota-widget-error')).toBeInTheDocument();
    });

    it('renders error message in error state', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Error'),
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByTestId('library-quota-widget-error-message')).toBeInTheDocument();
    });

    it('renders error when data is null', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: null,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(screen.getByTestId('library-quota-widget-error')).toBeInTheDocument();
    });

    it('renders AlertCircle icon in error state', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Error'),
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      const { container } = renderComponent();

      const icon = container.querySelector('svg.lucide-circle-alert');
      expect(icon).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Success State - Games Count Display Tests
  // ============================================================================

  describe('Games Count Display', () => {
    it('renders current count correctly', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();
      vi.advanceTimersByTime(100);

      expect(screen.getByTestId('library-quota-widget-current')).toHaveTextContent('25');
    });

    it('renders max count correctly', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();
      vi.advanceTimersByTime(100);

      expect(screen.getByTestId('library-quota-widget-max')).toHaveTextContent('100');
    });

    it('renders separator between counts', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();
      vi.advanceTimersByTime(100);

      expect(screen.getByTestId('library-quota-widget-separator')).toHaveTextContent('/');
    });

    it('renders "giochi" label', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();
      vi.advanceTimersByTime(100);

      expect(screen.getByTestId('library-quota-widget-label')).toBeInTheDocument();
    });

    it('renders percentage correctly', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();
      vi.advanceTimersByTime(100);

      expect(screen.getByTestId('library-quota-widget-percentage')).toHaveTextContent('25%');
    });

    it('renders 0% for empty library', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaEmpty,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();
      vi.advanceTimersByTime(100);

      expect(screen.getByTestId('library-quota-widget-percentage')).toHaveTextContent('0%');
      expect(screen.getByTestId('library-quota-widget-current')).toHaveTextContent('0');
    });
  });

  // ============================================================================
  // Progress Bar Tests
  // ============================================================================

  describe('Progress Bar', () => {
    it('renders progress bar element', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();
      vi.advanceTimersByTime(100);

      expect(screen.getByTestId('library-quota-widget-progress')).toBeInTheDocument();
    });

    it('applies animation duration class', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();
      vi.advanceTimersByTime(100);

      const progress = screen.getByTestId('library-quota-widget-progress');
      expect(progress).toHaveClass('duration-[1200ms]');
    });

    it('applies ease-out timing function', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();
      vi.advanceTimersByTime(100);

      const progress = screen.getByTestId('library-quota-widget-progress');
      expect(progress).toHaveClass('ease-out');
    });
  });

  // ============================================================================
  // Color Coding Tests
  // ============================================================================

  describe('Color Coding', () => {
    describe('Green (< 70%)', () => {
      it('uses primary color for low usage (25%)', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaLowUsage,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        const progress = screen.getByTestId('library-quota-widget-progress');
        // Should NOT have amber or destructive classes
        expect(progress).not.toHaveClass('[&>div]:bg-amber-500');
        expect(progress).not.toHaveClass('[&>div]:bg-destructive');
      });

      it('applies muted-foreground text for percentage < 70%', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaLowUsage,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        const percentage = screen.getByTestId('library-quota-widget-percentage');
        expect(percentage).toHaveClass('text-muted-foreground');
      });
    });

    describe('Yellow (70-90%)', () => {
      it('uses amber color at exactly 70%', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaAt70Percent,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        const progress = screen.getByTestId('library-quota-widget-progress');
        expect(progress).toHaveClass('[&>div]:bg-amber-500');
      });

      it('uses amber color for medium usage (75%)', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaMediumUsage,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        const progress = screen.getByTestId('library-quota-widget-progress');
        expect(progress).toHaveClass('[&>div]:bg-amber-500');
      });

      it('uses amber color at exactly 90%', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaAt90Percent,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        const progress = screen.getByTestId('library-quota-widget-progress');
        expect(progress).toHaveClass('[&>div]:bg-amber-500');
      });

      it('applies amber text for percentage 70-90%', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaMediumUsage,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        const percentage = screen.getByTestId('library-quota-widget-percentage');
        expect(percentage).toHaveClass('text-amber-600');
      });
    });

    describe('Red (> 90%)', () => {
      it('uses destructive color for high usage (92%)', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaHighUsage,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        const progress = screen.getByTestId('library-quota-widget-progress');
        expect(progress).toHaveClass('[&>div]:bg-destructive');
      });

      it('uses destructive color at limit (100%)', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaAtLimit,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        const progress = screen.getByTestId('library-quota-widget-progress');
        expect(progress).toHaveClass('[&>div]:bg-destructive');
      });

      it('applies destructive text for percentage > 90%', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaHighUsage,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        const percentage = screen.getByTestId('library-quota-widget-percentage');
        expect(percentage).toHaveClass('text-destructive');
      });
    });
  });

  // ============================================================================
  // CTA Button Tests
  // ============================================================================

  describe('CTA Button', () => {
    describe('Manage Library CTA (<=90%)', () => {
      it('renders Manage Library button for low usage', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaLowUsage,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        expect(screen.getByTestId('library-quota-widget-manage-cta')).toBeInTheDocument();
        expect(screen.queryByTestId('library-quota-widget-upgrade-cta')).not.toBeInTheDocument();
      });

      it('Manage Library links to /library', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaLowUsage,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        expect(screen.getByTestId('library-quota-widget-manage-cta')).toHaveAttribute('href', '/library');
      });

      it('renders Manage Library for exactly 90% usage', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaAt90Percent,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        expect(screen.getByTestId('library-quota-widget-manage-cta')).toBeInTheDocument();
        expect(screen.queryByTestId('library-quota-widget-upgrade-cta')).not.toBeInTheDocument();
      });

      it('renders ChevronRight icon in Manage Library button', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaLowUsage,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        const { container } = renderComponent();
        vi.advanceTimersByTime(100);

        const icon = container.querySelector('svg.lucide-chevron-right');
        expect(icon).toBeInTheDocument();
      });
    });

    describe('Upgrade Plan CTA (>90%)', () => {
      it('renders Upgrade Plan button for high usage (>90%)', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaHighUsage,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        expect(screen.getByTestId('library-quota-widget-upgrade-cta')).toBeInTheDocument();
        expect(screen.queryByTestId('library-quota-widget-manage-cta')).not.toBeInTheDocument();
      });

      it('Upgrade Plan links to /settings/subscription', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaHighUsage,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        expect(screen.getByTestId('library-quota-widget-upgrade-cta')).toHaveAttribute('href', '/settings/subscription');
      });

      it('renders Upgrade Plan for 100% usage', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaAtLimit,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        renderComponent();
        vi.advanceTimersByTime(100);

        expect(screen.getByTestId('library-quota-widget-upgrade-cta')).toBeInTheDocument();
      });

      it('renders Sparkles icon in Upgrade Plan button', () => {
        vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
          data: mockQuotaHighUsage,
          isLoading: false,
          error: null,
        } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

        const { container } = renderComponent();
        vi.advanceTimersByTime(100);

        const icon = container.querySelector('svg.lucide-sparkles');
        expect(icon).toBeInTheDocument();
      });
    });
  });

  // ============================================================================
  // Animation Tests
  // ============================================================================

  describe('Animation', () => {
    it('starts animation from 0', async () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      // Initially should have value 0
      const progress = screen.getByTestId('library-quota-widget-progress');
      expect(progress).toHaveAttribute('aria-valuenow', '0');
    });

    it('animates to target percentage after delay', async () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      // Advance timers past the animation delay (50ms setTimeout + some buffer)
      // and wrap in act to flush React state updates
      await act(async () => {
        vi.advanceTimersByTime(100);
      });

      const progress = screen.getByTestId('library-quota-widget-progress');
      expect(progress).toHaveAttribute('aria-valuenow', '25');
    });
  });

  // ============================================================================
  // Custom ClassName Tests
  // ============================================================================

  describe('Custom className', () => {
    it('applies custom className to Card', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent('custom-class');
      vi.advanceTimersByTime(100);

      const widget = screen.getByTestId('library-quota-widget');
      expect(widget).toHaveClass('custom-class');
    });

    it('applies custom className in loading state', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent('loading-class');

      const widget = screen.getByTestId('library-quota-widget');
      expect(widget).toHaveClass('loading-class');
    });

    it('applies custom className in error state', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Error'),
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent('error-class');

      const widget = screen.getByTestId('library-quota-widget');
      expect(widget).toHaveClass('error-class');
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has accessible title', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();
      vi.advanceTimersByTime(100);

      expect(screen.getByTestId('library-quota-widget-title')).toBeInTheDocument();
    });

    it('progress bar has aria-valuenow attribute', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();
      vi.advanceTimersByTime(100);

      const progress = screen.getByTestId('library-quota-widget-progress');
      expect(progress).toHaveAttribute('aria-valuenow');
    });

    it('CTA buttons are accessible links', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();
      vi.advanceTimersByTime(100);

      const manageLink = screen.getByTestId('library-quota-widget-manage-cta');
      expect(manageLink.tagName.toLowerCase()).toBe('a');
      expect(manageLink).toHaveAttribute('href');
    });
  });

  // ============================================================================
  // Hook Integration Tests
  // ============================================================================

  describe('Hook Integration', () => {
    it('calls useLibraryQuota hook', () => {
      vi.mocked(useLibraryModule.useLibraryQuota).mockReturnValue({
        data: mockQuotaLowUsage,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useLibraryModule.useLibraryQuota>);

      renderComponent();

      expect(useLibraryModule.useLibraryQuota).toHaveBeenCalled();
    });
  });
});
