/**
 * Tests for StatsGrid component
 * Issue #3005: Frontend coverage improvements
 */

import React from 'react';

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import type { DashboardStats, ViewMode } from '../types';

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <div {...props}>{children}</div>
    ),
  },
}));

// Import after mocking
import { StatsGrid } from '../StatsGrid';

describe('StatsGrid', () => {
  const mockStats: DashboardStats = {
    ragVariants: 6,
    avgTokensPerQuery: 3500,
    tokenReduction: 60,
    targetAccuracy: 95,
    monthlyCost: 150,
    cacheHitRate: 80,
  };

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the stats grid container', () => {
      const { container } = render(
        <StatsGrid stats={mockStats} viewMode="technical" />
      );

      expect(container.querySelector('.grid')).toBeInTheDocument();
    });

    it('should render 6 stat cards', () => {
      render(<StatsGrid stats={mockStats} viewMode="technical" />);

      const statCards = document.querySelectorAll('.rag-stat-card');
      expect(statCards.length).toBe(6);
    });

    it('should apply custom className', () => {
      const { container } = render(
        <StatsGrid stats={mockStats} viewMode="technical" className="custom-class" />
      );

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Technical View Mode Tests
  // =========================================================================

  describe('Technical View Mode', () => {
    it('should display RAG Variants stat', () => {
      render(<StatsGrid stats={mockStats} viewMode="technical" />);

      expect(screen.getByText('RAG Variants')).toBeInTheDocument();
      expect(screen.getByText('6')).toBeInTheDocument();
    });

    it('should display Avg Tokens/Query stat', () => {
      render(<StatsGrid stats={mockStats} viewMode="technical" />);

      expect(screen.getByText('Avg Tokens/Query')).toBeInTheDocument();
      // toLocaleString formatting varies by locale (3,500 or 3.500 or 3500)
      expect(screen.getByText((content) => /^3[\s,.\u00A0]?500$/.test(content))).toBeInTheDocument();
    });

    it('should display Token Reduction stat with percentage', () => {
      render(<StatsGrid stats={mockStats} viewMode="technical" />);

      expect(screen.getByText('Token Reduction')).toBeInTheDocument();
      expect(screen.getByText('60%')).toBeInTheDocument();
    });

    it('should display Target Accuracy stat', () => {
      render(<StatsGrid stats={mockStats} viewMode="technical" />);

      expect(screen.getByText('Target Accuracy')).toBeInTheDocument();
      expect(screen.getByText('95%')).toBeInTheDocument();
    });

    it('should display Monthly Cost stat with dollar sign', () => {
      render(<StatsGrid stats={mockStats} viewMode="technical" />);

      expect(screen.getByText('Monthly Cost')).toBeInTheDocument();
      expect(screen.getByText('$150')).toBeInTheDocument();
    });

    it('should display Cache Hit Rate stat', () => {
      render(<StatsGrid stats={mockStats} viewMode="technical" />);

      expect(screen.getByText('Cache Hit Rate')).toBeInTheDocument();
      expect(screen.getByText('80%')).toBeInTheDocument();
    });

    it('should display technical sublabels', () => {
      render(<StatsGrid stats={mockStats} viewMode="technical" />);

      expect(screen.getByText('Configurable strategies')).toBeInTheDocument();
      expect(screen.getByText('Optimized consumption')).toBeInTheDocument();
      expect(screen.getByText('vs naive RAG')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Business View Mode Tests
  // =========================================================================

  describe('Business View Mode', () => {
    it('should display Cost Reduction instead of Token Reduction', () => {
      render(<StatsGrid stats={mockStats} viewMode="business" />);

      expect(screen.getByText('Cost Reduction')).toBeInTheDocument();
    });

    it('should display Answer Accuracy instead of Target Accuracy', () => {
      render(<StatsGrid stats={mockStats} viewMode="business" />);

      expect(screen.getByText('Answer Accuracy')).toBeInTheDocument();
    });

    it('should display Cost per 1K Queries', () => {
      render(<StatsGrid stats={mockStats} viewMode="business" />);

      expect(screen.getByText('Cost per 1K Queries')).toBeInTheDocument();
    });

    it('should display Strategy Options instead of RAG Variants', () => {
      render(<StatsGrid stats={mockStats} viewMode="business" />);

      expect(screen.getByText('Strategy Options')).toBeInTheDocument();
    });

    it('should display Cache Efficiency instead of Cache Hit Rate', () => {
      render(<StatsGrid stats={mockStats} viewMode="business" />);

      expect(screen.getByText('Cache Efficiency')).toBeInTheDocument();
    });

    it('should display business-focused sublabels', () => {
      render(<StatsGrid stats={mockStats} viewMode="business" />);

      expect(screen.getByText('Token optimization savings')).toBeInTheDocument();
      expect(screen.getByText('User satisfaction driver')).toBeInTheDocument();
      expect(screen.getByText('Unit economics')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Stat Card Variants Tests
  // =========================================================================

  describe('Stat Card Variants', () => {
    it('should have success variant for positive metrics', () => {
      const { container } = render(
        <StatsGrid stats={mockStats} viewMode="technical" />
      );

      const successCards = container.querySelectorAll('[data-variant="success"]');
      expect(successCards.length).toBeGreaterThan(0);
    });

    it('should have warning variant for cost-related metrics', () => {
      const { container } = render(
        <StatsGrid stats={mockStats} viewMode="technical" />
      );

      const warningCards = container.querySelectorAll('[data-variant="warning"]');
      expect(warningCards.length).toBeGreaterThan(0);
    });

    it('should have default variant for neutral metrics', () => {
      const { container } = render(
        <StatsGrid stats={mockStats} viewMode="technical" />
      );

      const defaultCards = container.querySelectorAll('[data-variant="default"]');
      expect(defaultCards.length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // View Mode Switching Tests
  // =========================================================================

  describe('View Mode Switching', () => {
    it('should switch stats when view mode changes', () => {
      const { rerender } = render(
        <StatsGrid stats={mockStats} viewMode="technical" />
      );

      expect(screen.getByText('RAG Variants')).toBeInTheDocument();

      rerender(<StatsGrid stats={mockStats} viewMode="business" />);

      expect(screen.getByText('Strategy Options')).toBeInTheDocument();
    });

    it('should maintain same number of cards across view modes', () => {
      const { rerender, container } = render(
        <StatsGrid stats={mockStats} viewMode="technical" />
      );

      const technicalCards = container.querySelectorAll('.rag-stat-card').length;

      rerender(<StatsGrid stats={mockStats} viewMode="business" />);

      const businessCards = container.querySelectorAll('.rag-stat-card').length;

      expect(technicalCards).toBe(businessCards);
    });
  });

  // =========================================================================
  // Edge Cases Tests
  // =========================================================================

  describe('Edge Cases', () => {
    it('should handle zero values', () => {
      const zeroStats: DashboardStats = {
        ragVariants: 0,
        avgTokensPerQuery: 0,
        tokenReduction: 0,
        targetAccuracy: 0,
        monthlyCost: 0,
        cacheHitRate: 0,
      };

      render(<StatsGrid stats={zeroStats} viewMode="technical" />);

      // Use getAllByText for multiple matching elements
      const zeroElements = screen.getAllByText('0');
      expect(zeroElements.length).toBeGreaterThan(0);

      const percentElements = screen.getAllByText('0%');
      expect(percentElements.length).toBeGreaterThan(0);

      expect(screen.getByText('$0')).toBeInTheDocument();
    });

    it('should handle large numbers', () => {
      const largeStats: DashboardStats = {
        ragVariants: 100,
        avgTokensPerQuery: 1000000,
        tokenReduction: 99,
        targetAccuracy: 100,
        monthlyCost: 10000,
        cacheHitRate: 100,
      };

      render(<StatsGrid stats={largeStats} viewMode="technical" />);

      // Use getAllByText for multiple matching elements (100 and 100%)
      const hundredElements = screen.getAllByText((content) => content === '100' || content === '100%');
      expect(hundredElements.length).toBeGreaterThan(0);

      // toLocaleString formatting varies by locale (1,000,000 or 1.000.000 or 1 000 000 or 1000000)
      expect(screen.getByText((content) => /^1[\s,.\u00A0]?000[\s,.\u00A0]?000$/.test(content))).toBeInTheDocument();
      // monthlyCost may be formatted as $10,000 or $10.000 or $10000
      expect(screen.getByText((content) => /^\$10[\s,.\u00A0]?000$/.test(content))).toBeInTheDocument();
    });

    it('should format large avgTokensPerQuery with locale string', () => {
      const stats: DashboardStats = {
        ...mockStats,
        avgTokensPerQuery: 12345,
      };

      render(<StatsGrid stats={stats} viewMode="technical" />);

      // toLocaleString formatting varies by locale (12,345 or 12.345 or 12 345 or 12345)
      // Use flexible pattern to handle any thousands separator
      expect(screen.getByText((content) => /^12[\s,.\u00A0]?345$/.test(content))).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Grid Layout Tests
  // =========================================================================

  describe('Grid Layout', () => {
    it('should have responsive grid classes', () => {
      const { container } = render(
        <StatsGrid stats={mockStats} viewMode="technical" />
      );

      const grid = container.querySelector('.grid');
      expect(grid).toHaveClass('grid-cols-2');
      expect(grid).toHaveClass('md:grid-cols-3');
      expect(grid).toHaveClass('lg:grid-cols-6');
    });

    it('should have gap between cards', () => {
      const { container } = render(
        <StatsGrid stats={mockStats} viewMode="technical" />
      );

      const grid = container.querySelector('.grid');
      expect(grid).toHaveClass('gap-4');
    });
  });
});
