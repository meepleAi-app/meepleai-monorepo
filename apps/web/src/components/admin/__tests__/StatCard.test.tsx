/**
 * StatCard Component Tests - Issue #874, #882
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatCard } from '../StatCard';
import { Users, Activity, AlertTriangle } from 'lucide-react';

describe('StatCard', () => {
  it('renders label and value correctly', () => {
    render(<StatCard label="Total Users" value="1,247" />);

    expect(screen.getByTestId('stat-card-label')).toHaveTextContent('Total Users');
    expect(screen.getByTestId('stat-card-value')).toHaveTextContent('1,247');
  });

  it('applies default variant styling', () => {
    const { container } = render(<StatCard label="Test" value="100" variant="default" />);
    // Issue #2850: Component uses design system border tokens
    const card = container.querySelector('[class*="border-border"]');
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

    // Label should use proper styling
    const label = screen.getByTestId('stat-card-label');
    expect(label).toBeInTheDocument();

    // Value should be large and bold
    const value = screen.getByTestId('stat-card-value');
    expect(value).toHaveClass('text-5xl');
    expect(value).toHaveClass('font-bold');
  });

  // ==================== Issue #882 New Tests ====================

  describe('Icon support', () => {
    it('renders icon when provided', () => {
      const { container } = render(<StatCard label="Total Users" value="1,247" icon={Users} />);
      const svg = container.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('does not render icon wrapper when icon not provided', () => {
      const { container } = render(<StatCard label="Total Users" value="1,247" />);
      // Should not have icon container with rounded-lg class in the flex layout
      const iconContainer = container.querySelector('.rounded-lg.p-2');
      expect(iconContainer).not.toBeInTheDocument();
    });

    it('applies correct icon styling for default variant', () => {
      const { container } = render(
        <StatCard label="Test" value="100" icon={Users} variant="default" />
      );
      // Issue #2850: MeepleAI Design System uses #d2691e for default icon
      const iconContainer = container.querySelector('[class*="text-[#d2691e]"]');
      expect(iconContainer).toBeInTheDocument();
    });

    it('applies correct icon styling for success variant', () => {
      const { container } = render(
        <StatCard label="Test" value="100" icon={Activity} variant="success" />
      );
      const iconContainer = container.querySelector('[class*="text-green-600"]');
      expect(iconContainer).toBeInTheDocument();
    });

    it('applies correct icon styling for warning variant', () => {
      const { container } = render(
        <StatCard label="Test" value="100" icon={AlertTriangle} variant="warning" />
      );
      const iconContainer = container.querySelector('[class*="text-yellow-600"]');
      expect(iconContainer).toBeInTheDocument();
    });

    it('applies correct icon styling for danger variant', () => {
      const { container } = render(
        <StatCard label="Test" value="100" icon={AlertTriangle} variant="danger" />
      );
      const iconContainer = container.querySelector('[class*="text-red-600"]');
      expect(iconContainer).toBeInTheDocument();
    });
  });

  describe('Loading state', () => {
    it('renders loading skeleton when loading is true', () => {
      render(<StatCard label="Test" value="100" loading />);
      expect(screen.getByTestId('statcard-loading')).toBeInTheDocument();
    });

    it('does not render label or value when loading', () => {
      render(<StatCard label="Total Users" value="1,247" loading />);
      expect(screen.queryByTestId('stat-card-label')).not.toBeInTheDocument();
      expect(screen.queryByTestId('stat-card-value')).not.toBeInTheDocument();
    });

    it('renders skeleton placeholders when loading', () => {
      const { container } = render(<StatCard label="Test" value="100" loading />);
      const skeletons = container.querySelectorAll('.animate-pulse');
      expect(skeletons.length).toBeGreaterThan(0);
    });

    it('renders icon skeleton when icon is provided and loading', () => {
      const { container } = render(<StatCard label="Test" value="100" icon={Users} loading />);
      // Issue #2850: Icon skeleton is 48x48 (h-12 w-12)
      const iconSkeleton = container.querySelector('.h-12.w-12');
      expect(iconSkeleton).toBeInTheDocument();
    });

    it('does not render icon skeleton when no icon and loading', () => {
      const { container } = render(<StatCard label="Test" value="100" loading />);
      const iconSkeleton = container.querySelector('.h-12.w-12');
      expect(iconSkeleton).not.toBeInTheDocument();
    });
  });

  describe('Hover effect', () => {
    it('has hover transition classes', () => {
      const { container } = render(<StatCard label="Test" value="100" />);
      // Issue #2850: MeepleAI Design System uses custom hover shadow
      const card = container.querySelector('[class*="hover:-translate-y-"]');
      expect(card).toBeInTheDocument();
    });

    it('has transition-all class for smooth animation', () => {
      const { container } = render(<StatCard label="Test" value="100" />);
      const card = container.querySelector('[class*="transition-all"]');
      expect(card).toBeInTheDocument();
    });
  });
});
