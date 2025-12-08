/**
 * MetricsGrid Component Tests - Issue #874
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

  it('handles empty metrics array', () => {
    const { container } = render(<MetricsGrid metrics={[]} />);
    const grid = container.querySelector('[class*="grid"]');
    expect(grid).toBeInTheDocument();
    expect(grid?.children.length).toBe(0);
  });

  it('handles single metric', () => {
    render(<MetricsGrid metrics={[mockMetrics[0]]} />);

    expect(screen.getByText('Total Users')).toBeInTheDocument();
    expect(screen.getByText('1,247')).toBeInTheDocument();
  });

  it('has responsive grid classes', () => {
    const { container } = render(<MetricsGrid metrics={mockMetrics} />);
    const grid = container.querySelector('[class*="grid"]');

    expect(grid).toHaveClass('grid-cols-1'); // Mobile
    expect(grid).toHaveClass('md:grid-cols-2'); // Tablet
    expect(grid).toHaveClass('lg:grid-cols-3'); // Desktop
    expect(grid).toHaveClass('xl:grid-cols-4'); // Large desktop
  });

  it('applies custom className', () => {
    const { container } = render(<MetricsGrid metrics={mockMetrics} className="custom-grid" />);
    const grid = container.querySelector('.custom-grid');
    expect(grid).toBeInTheDocument();
  });

  it('renders metrics with unique keys', () => {
    const { container } = render(<MetricsGrid metrics={mockMetrics} />);
    const cards = container.querySelectorAll('[class*="rounded-2xl"]');
    expect(cards.length).toBe(mockMetrics.length);
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
