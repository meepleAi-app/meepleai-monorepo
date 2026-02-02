/**
 * KpiCard Unit Tests (Issue #3308: HeroStats Component)
 *
 * Coverage areas:
 * - Rendering with different props
 * - Color variants
 * - Trend indicators (positive/negative)
 * - Streak indicators
 * - Loading/skeleton state
 * - Navigation links
 * - Accessibility
 *
 * Target: 90%+ coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { KpiCard, KpiCardSkeleton } from '../KpiCard';
import { Library, Dices, MessageSquare, Star } from 'lucide-react';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

describe('KpiCard', () => {
  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders with required props', () => {
      render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
        />
      );

      expect(screen.getByTestId('kpi-card')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-value')).toHaveTextContent('127');
      expect(screen.getByTestId('kpi-label')).toHaveTextContent('Collezione');
    });

    it('renders icon correctly', () => {
      const { container } = render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
        />
      );

      const icon = container.querySelector('svg');
      expect(icon).toBeInTheDocument();
    });

    it('formats large numbers with locale', () => {
      render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={12345}
          label="Collection"
          href="/library"
        />
      );

      // Italian locale formats with dots
      expect(screen.getByTestId('kpi-value')).toHaveTextContent('12.345');
    });

    it('applies custom testId', () => {
      render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
          testId="custom-kpi"
        />
      );

      expect(screen.getByTestId('custom-kpi')).toBeInTheDocument();
    });

    it('applies custom className', () => {
      const { container } = render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
          className="custom-class"
        />
      );

      const card = container.querySelector('.custom-class');
      expect(card).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Color Variant Tests
  // ============================================================================

  describe('Color Variants', () => {
    it.each([
      ['amber', 'bg-amber-500/20'],
      ['emerald', 'bg-emerald-500/20'],
      ['blue', 'bg-blue-500/20'],
      ['purple', 'bg-purple-500/20'],
    ] as const)('renders %s variant with correct icon background', (color, expectedClass) => {
      const { container } = render(
        <KpiCard
          icon={Library}
          iconColor={color}
          value={127}
          label="Test"
          href="/test"
        />
      );

      const iconContainer = container.querySelector(`.${expectedClass.replace('/', '\\/')}`);
      expect(iconContainer).toBeInTheDocument();
    });

    it.each([
      ['amber', 'from-amber-500/10'],
      ['emerald', 'from-emerald-500/10'],
      ['blue', 'from-blue-500/10'],
      ['purple', 'from-purple-500/10'],
    ] as const)('renders %s variant with correct gradient', (color, expectedClass) => {
      const { container } = render(
        <KpiCard
          icon={Library}
          iconColor={color}
          value={127}
          label="Test"
          href="/test"
        />
      );

      const card = container.querySelector(`.${expectedClass.replace('/', '\\/')}`);
      expect(card).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Trend Indicator Tests
  // ============================================================================

  describe('Trend Indicators', () => {
    it('renders positive trend with up arrow', () => {
      render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
          trend={{ value: 3, period: 'mese' }}
        />
      );

      const trend = screen.getByTestId('kpi-trend');
      expect(trend).toHaveTextContent('+3 mese');
      expect(trend).toHaveClass('text-emerald-600');
    });

    it('renders negative trend with down arrow', () => {
      render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
          trend={{ value: -2, period: 'mese' }}
        />
      );

      const trend = screen.getByTestId('kpi-trend');
      expect(trend).toHaveTextContent('-2 mese');
      expect(trend).toHaveClass('text-red-600');
    });

    it('renders zero trend as positive', () => {
      render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
          trend={{ value: 0, period: 'mese' }}
        />
      );

      const trend = screen.getByTestId('kpi-trend');
      expect(trend).toHaveTextContent('+0 mese');
      expect(trend).toHaveClass('text-emerald-600');
    });

    it('does not render trend when not provided', () => {
      render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
        />
      );

      expect(screen.queryByTestId('kpi-trend')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Streak Indicator Tests
  // ============================================================================

  describe('Streak Indicators', () => {
    it('renders active streak with flame icon', () => {
      render(
        <KpiCard
          icon={Dices}
          iconColor="emerald"
          value={23}
          label="Giocati"
          href="/sessions"
          streak={{ count: 7, isActive: true }}
        />
      );

      const streak = screen.getByTestId('kpi-streak');
      expect(streak).toHaveTextContent('7d');
      expect(streak).toHaveClass('text-orange-600');
    });

    it('does not render streak when not active', () => {
      render(
        <KpiCard
          icon={Dices}
          iconColor="emerald"
          value={23}
          label="Giocati"
          href="/sessions"
          streak={{ count: 7, isActive: false }}
        />
      );

      expect(screen.queryByTestId('kpi-streak')).not.toBeInTheDocument();
    });

    it('does not render streak when not provided', () => {
      render(
        <KpiCard
          icon={Dices}
          iconColor="emerald"
          value={23}
          label="Giocati"
          href="/sessions"
        />
      );

      expect(screen.queryByTestId('kpi-streak')).not.toBeInTheDocument();
    });

    it('renders both trend and streak when provided', () => {
      render(
        <KpiCard
          icon={Dices}
          iconColor="emerald"
          value={23}
          label="Giocati"
          href="/sessions"
          trend={{ value: 5, period: '30gg' }}
          streak={{ count: 7, isActive: true }}
        />
      );

      expect(screen.getByTestId('kpi-trend')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-streak')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders skeleton when isLoading is true', () => {
      render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
          isLoading
        />
      );

      expect(screen.getByTestId('kpi-card-skeleton')).toBeInTheDocument();
      expect(screen.queryByTestId('kpi-card')).not.toBeInTheDocument();
    });

    it('renders content when isLoading is false', () => {
      render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
          isLoading={false}
        />
      );

      expect(screen.queryByTestId('kpi-card-skeleton')).not.toBeInTheDocument();
      expect(screen.getByTestId('kpi-card')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Navigation Tests
  // ============================================================================

  describe('Navigation', () => {
    it('wraps card in link with correct href', () => {
      render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
        />
      );

      const link = screen.getByRole('link');
      expect(link).toHaveAttribute('href', '/library');
    });

    it('card is clickable', async () => {
      const user = userEvent.setup();

      render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
        />
      );

      const link = screen.getByRole('link');
      await user.click(link);
      // Navigation would happen in real app
      expect(link).toHaveAttribute('href', '/library');
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has link role for navigation', () => {
      render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
        />
      );

      expect(screen.getByRole('link')).toBeInTheDocument();
    });

    it('card content is accessible', () => {
      render(
        <KpiCard
          icon={Library}
          iconColor="amber"
          value={127}
          label="Collezione"
          href="/library"
        />
      );

      expect(screen.getByText('127')).toBeInTheDocument();
      expect(screen.getByText('Collezione')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Different Icons Tests
  // ============================================================================

  describe('Different Icons', () => {
    it.each([
      [Library, 'Library'],
      [Dices, 'Dices'],
      [MessageSquare, 'MessageSquare'],
      [Star, 'Star'],
    ])('renders %s icon correctly', (IconComponent) => {
      const { container } = render(
        <KpiCard
          icon={IconComponent}
          iconColor="amber"
          value={100}
          label="Test"
          href="/test"
        />
      );

      const icon = container.querySelector('svg');
      expect(icon).toBeInTheDocument();
    });
  });
});

// ============================================================================
// KpiCardSkeleton Tests
// ============================================================================

describe('KpiCardSkeleton', () => {
  it('renders skeleton component', () => {
    render(<KpiCardSkeleton />);

    expect(screen.getByTestId('kpi-card-skeleton')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(<KpiCardSkeleton className="custom-skeleton" />);

    const skeleton = container.querySelector('.custom-skeleton');
    expect(skeleton).toBeInTheDocument();
  });

  it('has glassmorphic styling', () => {
    render(<KpiCardSkeleton />);

    const skeleton = screen.getByTestId('kpi-card-skeleton');
    expect(skeleton).toHaveClass('backdrop-blur-xl');
    expect(skeleton).toHaveClass('rounded-2xl');
  });
});
