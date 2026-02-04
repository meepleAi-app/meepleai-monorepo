import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { PerformanceMetrics } from '../PerformanceMetrics';
import { MOCK_RAG_METRICS } from '../mock-data';
import type { RagMetrics } from '../types';

describe('PerformanceMetrics', () => {
  it('renders the component title', () => {
    render(<PerformanceMetrics />);
    expect(screen.getByText('Performance Metrics')).toBeInTheDocument();
  });

  it('displays subtitle', () => {
    render(<PerformanceMetrics />);
    expect(
      screen.getByText('Real-time analytics for RAG system performance')
    ).toBeInTheDocument();
  });

  it('shows last updated timestamp', () => {
    render(<PerformanceMetrics />);
    expect(screen.getByText(/Last updated:/)).toBeInTheDocument();
  });

  it('renders all 5 metric widgets', () => {
    render(<PerformanceMetrics />);
    // Check for metric widget titles
    expect(screen.getByText('Latency')).toBeInTheDocument();
    expect(screen.getByText('Token Usage')).toBeInTheDocument();
    expect(screen.getByText('Cache Performance')).toBeInTheDocument();
    expect(screen.getByText('Accuracy')).toBeInTheDocument();
    expect(screen.getByText('Cost Analysis')).toBeInTheDocument();
  });

  it('renders refresh button', () => {
    render(<PerformanceMetrics />);
    expect(screen.getByRole('button', { name: /refresh/i })).toBeInTheDocument();
  });

  it('handles refresh click', async () => {
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    render(<PerformanceMetrics onRefresh={onRefresh} />);

    const refreshButton = screen.getByRole('button', { name: /refresh/i });
    fireEvent.click(refreshButton);

    await waitFor(() => {
      expect(onRefresh).toHaveBeenCalledTimes(1);
    });
  });

  it('uses external metrics when provided', () => {
    const customMetrics: RagMetrics = {
      ...MOCK_RAG_METRICS,
      latency: {
        p50: 100,
        p95: 200,
        p99: 300,
        avg: 150,
        trend: -5,
      },
    };
    render(<PerformanceMetrics metrics={customMetrics} />);
    expect(screen.getByText('100ms')).toBeInTheDocument();
  });

  it('shows loading state', () => {
    render(<PerformanceMetrics isLoading={true} />);
    // Should show skeleton placeholders
    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows error state', () => {
    render(<PerformanceMetrics error="Failed to fetch metrics" />);
    expect(screen.getByText('Failed to load metrics')).toBeInTheDocument();
    expect(screen.getByText('Failed to fetch metrics')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });

  it('handles retry on error state', async () => {
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    render(<PerformanceMetrics error="Error" onRefresh={onRefresh} />);

    const retryButton = screen.getByRole('button', { name: /try again/i });
    fireEvent.click(retryButton);

    await waitFor(() => {
      expect(onRefresh).toHaveBeenCalledTimes(1);
    });
  });

  it('applies custom className', () => {
    const { container } = render(
      <PerformanceMetrics className="custom-class" />
    );
    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('disables buttons while loading', () => {
    render(<PerformanceMetrics isLoading={true} />);
    const buttons = screen.getAllByRole('button');
    buttons.forEach(button => {
      expect(button).toBeDisabled();
    });
  });
});
