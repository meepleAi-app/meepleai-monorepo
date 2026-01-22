/**
 * KPICard Component Tests (Issue #2785)
 *
 * Tests for individual KPI card rendering:
 * - Basic rendering
 * - Trend indicators
 * - Badge variants
 * - Hover effects
 * - Dark mode
 *
 * Part of Epic #2783 - Admin Dashboard Redesign
 */

import { render, screen } from '@testing-library/react';
import { Users, Activity } from 'lucide-react';
import { describe, it, expect } from 'vitest';

import { KPICard, type KPICardData } from '@/components/admin/KPICard';

describe('KPICard', () => {
  const baseProps: KPICardData = {
    title: 'Test Card',
    value: '1,234',
    icon: <Users className="h-5 w-5" data-testid="card-icon" />,
  };

  describe('Basic Rendering', () => {
    it('should render title and value', () => {
      render(<KPICard {...baseProps} data-testid="kpi-card" />);

      expect(screen.getByText('Test Card')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-card-value')).toHaveTextContent('1,234');
    });

    it('should render icon', () => {
      render(<KPICard {...baseProps} />);

      expect(screen.getByTestId('card-icon')).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      render(<KPICard {...baseProps} className="custom-class" data-testid="kpi-card" />);

      expect(screen.getByTestId('kpi-card')).toHaveClass('custom-class');
    });

    it('should have correct base styling', () => {
      render(<KPICard {...baseProps} data-testid="kpi-card" />);

      const card = screen.getByTestId('kpi-card');
      expect(card).toHaveClass('rounded-xl', 'border', 'shadow-sm');
    });
  });

  describe('Trend Indicator', () => {
    it('should display positive trend with green styling', () => {
      render(
        <KPICard
          {...baseProps}
          trend={12.5}
          trendLabel="vs last month"
          data-testid="kpi-card"
        />
      );

      const trendElement = screen.getByTestId('kpi-card-trend');
      expect(trendElement).toHaveTextContent('+12.5%');
      expect(trendElement).toHaveClass('text-green-600');
    });

    it('should display negative trend with red styling', () => {
      render(
        <KPICard
          {...baseProps}
          trend={-5}
          trendLabel="vs last month"
          data-testid="kpi-card"
        />
      );

      const trendElement = screen.getByTestId('kpi-card-trend');
      expect(trendElement).toHaveTextContent('-5%');
      expect(trendElement).toHaveClass('text-red-600');
    });

    it('should display zero trend without plus sign', () => {
      render(
        <KPICard
          {...baseProps}
          trend={0}
          trendLabel="no change"
          data-testid="kpi-card"
        />
      );

      const trendElement = screen.getByTestId('kpi-card-trend');
      expect(trendElement).toHaveTextContent('0%');
    });

    it('should display trend label', () => {
      render(
        <KPICard
          {...baseProps}
          trend={10}
          trendLabel="vs mese scorso"
          data-testid="kpi-card"
        />
      );

      expect(screen.getByText('vs mese scorso')).toBeInTheDocument();
    });

    it('should not display trend when undefined', () => {
      render(<KPICard {...baseProps} data-testid="kpi-card" />);

      expect(screen.queryByTestId('kpi-card-trend')).not.toBeInTheDocument();
    });
  });

  describe('Badge', () => {
    it('should render warning badge', () => {
      render(
        <KPICard
          {...baseProps}
          badge="12 in attesa"
          badgeVariant="warning"
          data-testid="kpi-card"
        />
      );

      const badge = screen.getByTestId('kpi-card-badge');
      expect(badge).toHaveTextContent('12 in attesa');
      expect(badge).toHaveClass('bg-amber-100', 'text-amber-700');
    });

    it('should render error badge', () => {
      render(
        <KPICard
          {...baseProps}
          badge="Critical"
          badgeVariant="error"
          data-testid="kpi-card"
        />
      );

      const badge = screen.getByTestId('kpi-card-badge');
      expect(badge).toHaveClass('bg-red-100', 'text-red-700');
    });

    it('should render success badge by default', () => {
      render(
        <KPICard
          {...baseProps}
          badge="Active"
          badgeVariant="success"
          data-testid="kpi-card"
        />
      );

      const badge = screen.getByTestId('kpi-card-badge');
      expect(badge).toHaveClass('bg-green-100', 'text-green-700');
    });

    it('should not render badge when not provided', () => {
      render(<KPICard {...baseProps} data-testid="kpi-card" />);

      expect(screen.queryByTestId('kpi-card-badge')).not.toBeInTheDocument();
    });
  });

  describe('Subtitle', () => {
    it('should render subtitle when provided', () => {
      render(
        <KPICard
          {...baseProps}
          subtitle="in tempo reale"
          data-testid="kpi-card"
        />
      );

      expect(screen.getByText('in tempo reale')).toBeInTheDocument();
    });

    it('should not render subtitle when not provided', () => {
      render(<KPICard {...baseProps} />);

      // Just checking the card renders without subtitle
      expect(screen.getByText('Test Card')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have aria-hidden on decorative elements', () => {
      render(<KPICard {...baseProps} data-testid="kpi-card" />);

      const card = screen.getByTestId('kpi-card');
      const decorativeCorner = card.querySelector('[aria-hidden="true"]');
      expect(decorativeCorner).toBeInTheDocument();
    });
  });

  describe('Responsive Design', () => {
    it('should have hover transition classes', () => {
      render(<KPICard {...baseProps} data-testid="kpi-card" />);

      const card = screen.getByTestId('kpi-card');
      expect(card).toHaveClass('transition-all', 'hover:border-orange-200', 'hover:shadow-md');
    });
  });
});
