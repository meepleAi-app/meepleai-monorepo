/**
 * Tests for PerformanceMetricsTable component
 * Issue #3005: Frontend coverage improvements
 */

import React from 'react';

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <div {...props}>{children}</div>
    ),
    tr: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <tr {...props}>{children}</tr>
    ),
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => <>{children}</>,
}));

// Import after mocking
import { PerformanceMetricsTable } from '../metrics/PerformanceMetricsTable';

describe('PerformanceMetricsTable', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render summary cards', () => {
      render(<PerformanceMetricsTable />);

      // These labels may appear multiple times (summary card and table rows)
      expect(screen.getAllByText('Avg Latency').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Accuracy').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Throughput').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Cache Hit').length).toBeGreaterThan(0);
    });

    it('should render metric values', () => {
      render(<PerformanceMetricsTable />);

      // Metric values may appear multiple times in different sections
      expect(screen.getAllByText('450ms').length).toBeGreaterThan(0);
      expect(screen.getAllByText('89%').length).toBeGreaterThan(0);
      expect(screen.getAllByText('45 qps').length).toBeGreaterThan(0);
      expect(screen.getAllByText('80%').length).toBeGreaterThan(0);
    });

    it('should render trend comparisons', () => {
      render(<PerformanceMetricsTable />);

      expect(screen.getByText('-18% vs last week')).toBeInTheDocument();
      expect(screen.getByText('+3% vs last week')).toBeInTheDocument();
    });

    it('should render three tabs', () => {
      render(<PerformanceMetricsTable />);

      expect(screen.getByRole('button', { name: /strategy comparison/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /query types/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /system health/i })).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Tab Navigation Tests
  // =========================================================================

  describe('Tab Navigation', () => {
    it('should show Strategy Comparison tab by default', () => {
      render(<PerformanceMetricsTable />);

      expect(screen.getByText('All Strategies')).toBeInTheDocument();
    });

    it('should switch to Query Types tab', async () => {
      const user = userEvent.setup();
      render(<PerformanceMetricsTable />);

      const queryTab = screen.getByRole('button', { name: /query types/i });
      await user.click(queryTab);

      expect(screen.getByText('Rule Lookup')).toBeInTheDocument();
      expect(screen.getByText('Setup Guide')).toBeInTheDocument();
    });

    it('should switch to System Health tab', async () => {
      const user = userEvent.setup();
      render(<PerformanceMetricsTable />);

      const healthTab = screen.getByRole('button', { name: /system health/i });
      await user.click(healthTab);

      expect(screen.getByText('Vector DB Load')).toBeInTheDocument();
      expect(screen.getByText('Cache Memory')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Strategy Comparison Tab Tests
  // =========================================================================

  describe('Strategy Comparison Tab', () => {
    it('should render strategy filter buttons', () => {
      render(<PerformanceMetricsTable />);

      expect(screen.getByRole('button', { name: 'All Strategies' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'FAST' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'BALANCED' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'PRECISE' })).toBeInTheDocument();
    });

    it('should filter by specific strategy', async () => {
      const user = userEvent.setup();
      render(<PerformanceMetricsTable />);

      const fastButton = screen.getByRole('button', { name: 'FAST' });
      await user.click(fastButton);

      // Should show FAST column only
      expect(fastButton).toHaveClass('bg-primary');
    });

    it('should display metric table headers', () => {
      render(<PerformanceMetricsTable />);

      expect(screen.getByText('Metric')).toBeInTheDocument();
      expect(screen.getByText('Baseline')).toBeInTheDocument();
      expect(screen.getByText('Trend')).toBeInTheDocument();
    });

    it('should display performance metrics', () => {
      render(<PerformanceMetricsTable />);

      expect(screen.getByText('P50 Latency')).toBeInTheDocument();
      expect(screen.getByText('P95 Latency')).toBeInTheDocument();
      expect(screen.getByText('Citation Rate')).toBeInTheDocument();
      expect(screen.getByText('Token Efficiency')).toBeInTheDocument();
    });

    it('should display metric descriptions', () => {
      render(<PerformanceMetricsTable />);

      expect(screen.getByText('Median response time')).toBeInTheDocument();
      expect(screen.getByText('95th percentile response time')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Query Types Tab Tests
  // =========================================================================

  describe('Query Types Tab', () => {
    it('should display query type cards', async () => {
      const user = userEvent.setup();
      render(<PerformanceMetricsTable />);

      const queryTab = screen.getByRole('button', { name: /query types/i });
      await user.click(queryTab);

      expect(screen.getByText('Rule Lookup')).toBeInTheDocument();
      expect(screen.getByText('Setup Guide')).toBeInTheDocument();
      expect(screen.getByText('Strategy Advice')).toBeInTheDocument();
      expect(screen.getByText('Resource Planning')).toBeInTheDocument();
      expect(screen.getByText('Educational')).toBeInTheDocument();
    });

    it('should display query volume percentages', async () => {
      const user = userEvent.setup();
      render(<PerformanceMetricsTable />);

      const queryTab = screen.getByRole('button', { name: /query types/i });
      await user.click(queryTab);

      expect(screen.getByText('45% of queries')).toBeInTheDocument();
      expect(screen.getByText('20% of queries')).toBeInTheDocument();
    });

    it('should expand query type details', async () => {
      const user = userEvent.setup();
      render(<PerformanceMetricsTable />);

      const queryTab = screen.getByRole('button', { name: /query types/i });
      await user.click(queryTab);

      // Find and click Rule Lookup card
      const ruleLookup = screen.getByText('Rule Lookup').closest('button');
      if (ruleLookup) {
        await user.click(ruleLookup);

        // Should show expanded details (labels may appear multiple times)
        await waitFor(() => {
          expect(screen.getAllByText('Avg Latency').length).toBeGreaterThan(0);
          expect(screen.getAllByText('Avg Tokens').length).toBeGreaterThan(0);
          expect(screen.getAllByText('Cache Hit Rate').length).toBeGreaterThan(0);
        });
      }
    });
  });

  // =========================================================================
  // System Health Tab Tests
  // =========================================================================

  describe('System Health Tab', () => {
    it('should display health metrics', async () => {
      const user = userEvent.setup();
      render(<PerformanceMetricsTable />);

      const healthTab = screen.getByRole('button', { name: /system health/i });
      await user.click(healthTab);

      expect(screen.getByText('Vector DB Load')).toBeInTheDocument();
      expect(screen.getByText('Cache Memory')).toBeInTheDocument();
      expect(screen.getByText('API Rate Limit')).toBeInTheDocument();
      expect(screen.getByText('Embedding Queue')).toBeInTheDocument();
    });

    it('should display health values with units', async () => {
      const user = userEvent.setup();
      render(<PerformanceMetricsTable />);

      const healthTab = screen.getByRole('button', { name: /system health/i });
      await user.click(healthTab);

      // Multiple health metrics have max=100 and unit='%', so use getAllByText
      expect(screen.getAllByText(/\/ 100 %/).length).toBeGreaterThan(0);
      expect(screen.getByText(/\/ 4 GB/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Legend Tests
  // =========================================================================

  describe('Legend', () => {
    it('should display legend items', () => {
      render(<PerformanceMetricsTable />);

      expect(screen.getByText('Improving (good)')).toBeInTheDocument();
      expect(screen.getByText('Decreasing (good for errors/latency)')).toBeInTheDocument();
      expect(screen.getByText('Stable')).toBeInTheDocument();
      expect(screen.getByText('Healthy')).toBeInTheDocument();
      expect(screen.getByText('Warning')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Strategy Selection Tests
  // =========================================================================

  describe('Strategy Selection', () => {
    it('should highlight selected strategy', async () => {
      const user = userEvent.setup();
      render(<PerformanceMetricsTable />);

      const balancedButton = screen.getByRole('button', { name: 'BALANCED' });
      await user.click(balancedButton);

      expect(balancedButton).toHaveClass('bg-primary');
    });

    it('should show all strategies button', () => {
      render(<PerformanceMetricsTable />);

      expect(screen.getByRole('button', { name: 'All Strategies' })).toBeInTheDocument();
    });

    it('should include EXPERT and CONSENSUS strategies', () => {
      render(<PerformanceMetricsTable />);

      expect(screen.getByRole('button', { name: 'EXPERT' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'CONSENSUS' })).toBeInTheDocument();
    });
  });
});
