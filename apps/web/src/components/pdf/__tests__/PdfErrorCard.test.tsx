/**
 * PdfErrorCard Component Tests (Issue #4217)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { PdfErrorCard } from '../PdfErrorCard';

describe('PdfErrorCard', () => {
  it('renders error message', () => {
    render(<PdfErrorCard error="Test error" category="network" canRetry={false} />);
    expect(screen.getByText('Test error')).toBeInTheDocument();
  });

  it('renders all categories correctly', () => {
    const categories = ['network', 'parsing', 'quota', 'service', 'unknown'] as const;
    const labels = ['Network Error', 'Parsing Error', 'Quota Exceeded', 'Service Error', 'Unknown Error'];

    categories.forEach((category, index) => {
      const { unmount } = render(<PdfErrorCard error="Test" category={category} canRetry={false} />);
      expect(screen.getByText(labels[index])).toBeInTheDocument();
      unmount();
    });
  });

  it('shows retry button when canRetry is true', () => {
    render(<PdfErrorCard error="Test" category="network" canRetry onRetry={() => {}} />);
    expect(screen.getByTestId('retry-button')).toBeInTheDocument();
  });

  it('hides retry button when canRetry is false', () => {
    render(<PdfErrorCard error="Test" category="network" canRetry={false} />);
    expect(screen.queryByTestId('retry-button')).not.toBeInTheDocument();
  });

  it('calls onRetry when retry button is clicked', () => {
    const onRetry = vi.fn();
    render(<PdfErrorCard error="Test" category="network" canRetry onRetry={onRetry} />);

    fireEvent.click(screen.getByTestId('retry-button'));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('has proper accessibility attributes', () => {
    render(<PdfErrorCard error="Test" category="network" canRetry={false} />);
    const card = screen.getByTestId('pdf-error-card');
    expect(card).toHaveAttribute('role', 'alert');
  });
});
