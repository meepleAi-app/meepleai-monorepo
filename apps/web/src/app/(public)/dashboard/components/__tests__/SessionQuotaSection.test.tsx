/**
 * SessionQuotaSection Component Tests (Issue #3075)
 *
 * Test Coverage:
 * - Loading state (skeleton)
 * - Error state (alert)
 * - Empty/null data state
 * - Quota display with various states
 * - Near limit warning (>=80%)
 * - At limit warning (100%)
 * - Unlimited sessions display
 * - Singular/plural text handling
 * - Navigation link
 * - Accessibility
 *
 * Target: >=85% coverage
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { SessionQuotaSection } from '../SessionQuotaSection';
import * as useSessionQuotaModule from '@/hooks/queries/useSessionQuota';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/hooks/queries/useSessionQuota', () => ({
  useSessionQuotaWithStatus: vi.fn(),
}));

vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    className,
    'data-testid': testId,
  }: {
    children: React.ReactNode;
    href: string;
    className?: string;
    'data-testid'?: string;
  }) => (
    <a href={href} className={className} data-testid={testId}>
      {children}
    </a>
  ),
}));

// ============================================================================
// Test Data
// ============================================================================

interface MockSessionQuota {
  currentSessions: number;
  maxSessions: number;
  remainingSlots: number;
  canCreateNew: boolean;
  isUnlimited: boolean;
  userTier: string;
  percentageUsed: number;
  warningLevel: 'none' | 'warning' | 'critical' | 'full';
}

const mockQuotaNormal: MockSessionQuota = {
  currentSessions: 2,
  maxSessions: 5,
  remainingSlots: 3,
  canCreateNew: true,
  isUnlimited: false,
  userTier: 'normal',
  percentageUsed: 40,
  warningLevel: 'none',
};

const mockQuotaNearLimit: MockSessionQuota = {
  currentSessions: 4,
  maxSessions: 5,
  remainingSlots: 1,
  canCreateNew: true,
  isUnlimited: false,
  userTier: 'premium',
  percentageUsed: 80,
  warningLevel: 'warning',
};

const mockQuotaCritical: MockSessionQuota = {
  currentSessions: 4,
  maxSessions: 5,
  remainingSlots: 1,
  canCreateNew: true,
  isUnlimited: false,
  userTier: 'premium',
  percentageUsed: 90,
  warningLevel: 'critical',
};

const mockQuotaAtLimit: MockSessionQuota = {
  currentSessions: 5,
  maxSessions: 5,
  remainingSlots: 0,
  canCreateNew: false,
  isUnlimited: false,
  userTier: 'premium',
  percentageUsed: 100,
  warningLevel: 'full',
};

const mockQuotaSingleSession: MockSessionQuota = {
  currentSessions: 1,
  maxSessions: 5,
  remainingSlots: 4,
  canCreateNew: true,
  isUnlimited: false,
  userTier: 'free',
  percentageUsed: 20,
  warningLevel: 'none',
};

const mockQuotaUnlimited: MockSessionQuota = {
  currentSessions: 10,
  maxSessions: 999,
  remainingSlots: 989,
  canCreateNew: true,
  isUnlimited: true,
  userTier: 'admin',
  percentageUsed: 0,
  warningLevel: 'none',
};

// ============================================================================
// Test Suite
// ============================================================================

describe('SessionQuotaSection', () => {
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
        <SessionQuotaSection />
      </QueryClientProvider>
    );
  };

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders loading skeleton while loading', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      expect(screen.getByTestId('session-quota-title')).toBeInTheDocument();
      expect(screen.getByTestId('session-quota-loading')).toBeInTheDocument();
    });

    it('renders skeleton elements in loading state', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      expect(screen.getByTestId('session-quota-skeleton-count')).toBeInTheDocument();
      expect(screen.getByTestId('session-quota-skeleton-status')).toBeInTheDocument();
    });

    it('renders Gamepad2 icon in loading state', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: undefined,
        isLoading: true,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      const { container } = renderComponent();

      const icon = container.querySelector('svg.lucide-gamepad-2');
      expect(icon).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Error State Tests
  // ============================================================================

  describe('Error State', () => {
    it('renders error alert when query fails', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Network error'),
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      expect(screen.getByTestId('session-quota-title')).toBeInTheDocument();
      expect(screen.getByTestId('session-quota-error')).toBeInTheDocument();
    });

    it('renders error when data is null', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: null,
        isLoading: false,
        error: null,
      } as unknown as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      expect(screen.getByTestId('session-quota-error')).toBeInTheDocument();
    });

    it('renders AlertCircle icon in error state', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: undefined,
        isLoading: false,
        error: new Error('Error'),
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      const { container } = renderComponent();

      const alertIcon = container.querySelector('svg');
      expect(alertIcon).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Normal State Tests
  // ============================================================================

  describe('Normal State', () => {
    it('renders session count correctly', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      expect(screen.getByTestId('session-quota-count')).toBeInTheDocument();
      expect(screen.getByTestId('session-quota-count-number')).toHaveTextContent('2');
      expect(screen.getByTestId('session-quota-count-label')).toBeInTheDocument();
    });

    it('renders remaining slots status', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      const statusElement = screen.getByTestId('session-quota-status');
      expect(statusElement).toBeInTheDocument();
      expect(statusElement.textContent).toContain('3');
    });

    it('links to games page', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      const link = screen.getByTestId('session-quota-link');
      expect(link).toHaveAttribute('href', '/games');
    });

    it('renders ChevronRight icon', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      const { container } = renderComponent();

      const icon = container.querySelector('svg.lucide-chevron-right');
      expect(icon).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Singular/Plural Tests
  // ============================================================================

  describe('Singular/Plural Text', () => {
    it('renders singular label for 1 session', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaSingleSession,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      const countLabel = screen.getByTestId('session-quota-count-label');
      expect(countLabel).toBeInTheDocument();
      expect(screen.getByTestId('session-quota-count-number')).toHaveTextContent('1');
    });

    it('renders plural label for multiple sessions', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      const countLabel = screen.getByTestId('session-quota-count-label');
      expect(countLabel).toBeInTheDocument();
      expect(screen.getByTestId('session-quota-count-number')).toHaveTextContent('2');
    });
  });

  // ============================================================================
  // Near Limit State Tests
  // ============================================================================

  describe('Near Limit State (warning)', () => {
    it('renders warning status when near limit', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaNearLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      const statusElement = screen.getByTestId('session-quota-status');
      expect(statusElement).toBeInTheDocument();
      expect(statusElement.textContent).toContain('1');
    });

    it('applies yellow styling when near limit', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaNearLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      const statusText = screen.getByTestId('session-quota-status');
      expect(statusText).toHaveClass('text-yellow-600');
    });

    it('applies yellow border to card when near limit', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaNearLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      const card = screen.getByTestId('session-quota-card');
      expect(card).toHaveClass('border-yellow-500/50');
    });
  });

  // ============================================================================
  // Critical State Tests
  // ============================================================================

  describe('Critical State', () => {
    it('applies yellow styling in critical state', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaCritical,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      const card = screen.getByTestId('session-quota-card');
      expect(card).toHaveClass('border-yellow-500/50');
    });
  });

  // ============================================================================
  // At Limit State Tests
  // ============================================================================

  describe('At Limit State (full)', () => {
    it('renders limit reached status when at limit', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaAtLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      const statusElement = screen.getByTestId('session-quota-status');
      expect(statusElement).toBeInTheDocument();
    });

    it('applies destructive styling when at limit', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaAtLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      const statusText = screen.getByTestId('session-quota-status');
      expect(statusText).toHaveClass('text-destructive');
    });

    it('applies destructive border to card when at limit', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaAtLimit,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      const card = screen.getByTestId('session-quota-card');
      expect(card).toHaveClass('border-destructive/50');
    });
  });

  // ============================================================================
  // Unlimited Sessions Tests
  // ============================================================================

  describe('Unlimited Sessions', () => {
    it('renders unlimited indicator for admin users', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaUnlimited,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      expect(screen.getByTestId('session-quota-unlimited')).toBeInTheDocument();
    });

    it('renders Infinity icon for unlimited users', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaUnlimited,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      const { container } = renderComponent();

      const icon = container.querySelector('svg.lucide-infinity');
      expect(icon).toBeInTheDocument();
    });

    it('does not show status element for unlimited users', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaUnlimited,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      expect(screen.queryByTestId('session-quota-status')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('card is a link and accessible', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      const link = screen.getByTestId('session-quota-link');
      expect(link).toBeInTheDocument();
      expect(link.tagName.toLowerCase()).toBe('a');
    });

    it('has CardTitle component', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      expect(screen.getByTestId('session-quota-title')).toBeInTheDocument();
    });

    it('applies hover transform class', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      const link = screen.getByTestId('session-quota-link');
      expect(link).toHaveClass('hover:scale-[1.02]');
    });
  });

  // ============================================================================
  // Hook Integration Tests
  // ============================================================================

  describe('Hook Integration', () => {
    it('calls useSessionQuotaWithStatus hook', () => {
      vi.mocked(useSessionQuotaModule.useSessionQuotaWithStatus).mockReturnValue({
        data: mockQuotaNormal,
        isLoading: false,
        error: null,
      } as ReturnType<typeof useSessionQuotaModule.useSessionQuotaWithStatus>);

      renderComponent();

      expect(useSessionQuotaModule.useSessionQuotaWithStatus).toHaveBeenCalled();
    });
  });
});
