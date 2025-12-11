/**
 * MetricsGrid Component Tests - Issue #874, #883
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MetricsGrid } from '../MetricsGrid';
import type { StatCardProps } from '../StatCard';

const mockMetrics: StatCardProps[] = [
  { label: 'Total Users', value: '1,247', variant: 'default' },
  { label: 'Active Sessions', value: '42', variant: 'success' },
  { label: 'Total Games', value: '125', variant: 'default' },
  { label: 'API Requests', value: '3,456', variant: 'default' },
];

describe('MetricsGrid', () => {
  describe('rendering', () => {
    it('renders all metrics', () => {
      render(<MetricsGrid metrics={mockMetrics} />);

      expect(screen.getByText('Total Users')).toBeInTheDocument();
      expect(screen.getByText('Active Sessions')).toBeInTheDocument();
      expect(screen.getByText('Total Games')).toBeInTheDocument();
      expect(screen.getByText('API Requests')).toBeInTheDocument();
    });

    it('renders metric values', () => {
      render(<MetricsGrid metrics={mockMetrics} />);

      expect(screen.getByText('1,247')).toBeInTheDocument();
      expect(screen.getByText('42')).toBeInTheDocument();
      expect(screen.getByText('125')).toBeInTheDocument();
      expect(screen.getByText('3,456')).toBeInTheDocument();
    });

    it('handles single metric', () => {
      render(<MetricsGrid metrics={[mockMetrics[0]]} />);

      expect(screen.getByText('Total Users')).toBeInTheDocument();
      expect(screen.getByText('1,247')).toBeInTheDocument();
    });

    it('maintains metric order', () => {
      render(<MetricsGrid metrics={mockMetrics} />);

      const labels = screen.getAllByText(/Total|Active|API/).map(el => el.textContent);
      expect(labels[0]).toContain('Total Users');
      expect(labels[1]).toContain('Active Sessions');
      expect(labels[2]).toContain('Total Games');
    });

    it('handles large number of metrics (16+)', () => {
      const manyMetrics: StatCardProps[] = Array.from({ length: 20 }, (_, i) => ({
        label: `Metric ${i + 1}`,
        value: `${i * 100}`,
        variant: 'default' as const,
      }));

      render(<MetricsGrid metrics={manyMetrics} />);

      expect(screen.getByText('Metric 1')).toBeInTheDocument();
      expect(screen.getByText('Metric 20')).toBeInTheDocument();
    });
  });

  describe('loading state', () => {
    it('shows loading skeleton when loading=true', () => {
      render(<MetricsGrid metrics={[]} loading={true} />);

      expect(screen.getByTestId('metrics-grid-skeleton')).toBeInTheDocument();
    });

    it('shows 12 skeleton cards when loading', () => {
      const { container } = render(<MetricsGrid metrics={[]} loading={true} />);

      const skeletons = container.querySelectorAll('[data-testid="metrics-grid-skeleton"] > div');
      expect(skeletons.length).toBe(12);
    });

    it('does not show metrics when loading', () => {
      render(<MetricsGrid metrics={mockMetrics} loading={true} />);

      expect(screen.queryByText('Total Users')).not.toBeInTheDocument();
    });
  });

  describe('empty state', () => {
    it('shows empty state when metrics array is empty', () => {
      render(<MetricsGrid metrics={[]} />);

      expect(screen.getByTestId('metrics-grid-empty')).toBeInTheDocument();
    });

    it('shows default empty message', () => {
      render(<MetricsGrid metrics={[]} />);

      expect(screen.getByText('No metrics available')).toBeInTheDocument();
    });

    it('shows custom empty message', () => {
      render(<MetricsGrid metrics={[]} emptyStateMessage="No data to display" />);

      expect(screen.getByText('No data to display')).toBeInTheDocument();
    });

    it('shows empty state icon', () => {
      const { container } = render(<MetricsGrid metrics={[]} />);

      const icon = container.querySelector('svg');
      expect(icon).toBeInTheDocument();
    });
  });

  describe('responsive layout', () => {
    it('has responsive grid classes', () => {
      render(<MetricsGrid metrics={mockMetrics} />);
      const grid = screen.getByTestId('metrics-grid');

      expect(grid).toHaveClass('grid-cols-1'); // Mobile
      expect(grid).toHaveClass('md:grid-cols-2'); // Tablet
      expect(grid).toHaveClass('lg:grid-cols-3'); // Desktop
      expect(grid).toHaveClass('xl:grid-cols-4'); // Large desktop
    });

    it('applies custom className', () => {
      render(<MetricsGrid metrics={mockMetrics} className="custom-grid" />);
      const grid = screen.getByTestId('metrics-grid');

      expect(grid).toHaveClass('custom-grid');
    });
  });

  describe('transitions', () => {
    it('applies transition classes to metric cards', () => {
      render(<MetricsGrid metrics={mockMetrics} />);
      const grid = screen.getByTestId('metrics-grid');

      // Direct children of grid are the animated wrappers
      const cardWrappers = grid.querySelectorAll(':scope > div');
      expect(cardWrappers.length).toBe(mockMetrics.length);
    });

    it('applies staggered animation delay', () => {
      render(<MetricsGrid metrics={mockMetrics} />);
      const grid = screen.getByTestId('metrics-grid');

      // Direct children of grid are the animated wrappers
      const cardWrappers = grid.querySelectorAll(':scope > div');
      const firstCard = cardWrappers[0] as HTMLElement;
      const secondCard = cardWrappers[1] as HTMLElement;

      expect(firstCard.style.animationDelay).toBe('0ms');
      expect(secondCard.style.animationDelay).toBe('50ms');
    });
  });
});
