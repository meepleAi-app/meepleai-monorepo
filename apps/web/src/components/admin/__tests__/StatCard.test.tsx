/**
 * StatCard Component Tests - Issue #874
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatCard } from '../StatCard';

describe('StatCard', () => {
  it('renders label and value correctly', () => {
    render(<StatCard label="Total Users" value="1,247" />);

    expect(screen.getByText('Total Users')).toBeInTheDocument();
    expect(screen.getByText('1,247')).toBeInTheDocument();
  });

  it('applies default variant styling', () => {
    const { container } = render(<StatCard label="Test" value="100" variant="default" />);
    const card = container.querySelector('[class*="border-gray-200"]');
    expect(card).toBeInTheDocument();
  });

  it('applies success variant styling', () => {
    const { container } = render(<StatCard label="Test" value="100" variant="success" />);
    const card = container.querySelector('[class*="border-green-200"]');
    expect(card).toBeInTheDocument();
  });

  it('applies warning variant styling', () => {
    const { container } = render(<StatCard label="Test" value="100" variant="warning" />);
    const card = container.querySelector('[class*="border-yellow-200"]');
    expect(card).toBeInTheDocument();
  });

  it('applies danger variant styling', () => {
    const { container } = render(<StatCard label="Test" value="100" variant="danger" />);
    const card = container.querySelector('[class*="border-red-200"]');
    expect(card).toBeInTheDocument();
  });

  it('renders trend indicator when provided', () => {
    render(<StatCard label="Test" value="100" trend="up" trendValue="+15%" />);

    expect(screen.getByText('+15%')).toBeInTheDocument();
  });

  it('shows up arrow for upward trend', () => {
    const { container } = render(
      <StatCard label="Test" value="100" trend="up" trendValue="+15%" />
    );
    const svg = container.querySelector('svg');
    expect(svg).toBeInTheDocument();
  });

  it('shows down arrow for downward trend', () => {
    const { container } = render(
      <StatCard label="Test" value="100" trend="down" trendValue="-8%" />
    );
    const svg = container.querySelector('svg');
    expect(svg).toBeInTheDocument();
  });

  it('shows minus icon for neutral trend', () => {
    const { container } = render(
      <StatCard label="Test" value="100" trend="neutral" trendValue="No change" />
    );
    const svg = container.querySelector('svg');
    expect(svg).toBeInTheDocument();
  });

  it('does not render trend when not provided', () => {
    render(<StatCard label="Test" value="100" />);

    expect(screen.queryByRole('img', { hidden: true })).not.toBeInTheDocument();
  });

  it('accepts numeric value', () => {
    render(<StatCard label="Count" value={12345} />);

    expect(screen.getByText('12345')).toBeInTheDocument();
  });

  it('accepts string value', () => {
    render(<StatCard label="Rate" value="94.5%" />);

    expect(screen.getByText('94.5%')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(<StatCard label="Test" value="100" className="custom-class" />);
    const card = container.querySelector('.custom-class');
    expect(card).toBeInTheDocument();
  });

  it('has accessible structure', () => {
    render(<StatCard label="Total Users" value="1,247" />);

    // Label should be uppercase and smaller
    const label = screen.getByText('Total Users');
    expect(label).toHaveClass('uppercase');
    expect(label).toHaveClass('text-xs');

    // Value should be large and bold
    const value = screen.getByText('1,247');
    expect(value).toHaveClass('text-3xl');
    expect(value).toHaveClass('font-bold');
  });
});
