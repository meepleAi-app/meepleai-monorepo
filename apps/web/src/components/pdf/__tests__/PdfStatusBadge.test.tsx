/**
 * PdfStatusBadge Component Tests (Issue #4217)
 * Tests all states, variants, and accessibility features
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { PdfStatusBadge } from '../PdfStatusBadge';
import type { PdfState } from '@/types/pdf';

describe('PdfStatusBadge', () => {
  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders with default props', () => {
      render(<PdfStatusBadge state="uploading" />);

      expect(screen.getByTestId('pdf-status-badge')).toBeInTheDocument();
      expect(screen.getByText('Uploading')).toBeInTheDocument();
    });

    it('renders all 7 states correctly', () => {
      const states: PdfState[] = [
        'pending',
        'uploading',
        'extracting',
        'chunking',
        'embedding',
        'indexing',
        'ready',
        'failed',
      ];

      const expectedLabels = [
        'Pending',
        'Uploading',
        'Extracting',
        'Chunking',
        'Embedding',
        'Indexing',
        'Ready',
        'Failed',
      ];

      states.forEach((state, index) => {
        const { unmount } = render(<PdfStatusBadge state={state} />);
        expect(screen.getByText(expectedLabels[index])).toBeInTheDocument();
        unmount();
      });
    });

    it('renders icon by default', () => {
      render(<PdfStatusBadge state="uploading" />);

      const badge = screen.getByTestId('pdf-status-badge');
      const icon = badge.querySelector('svg');
      expect(icon).toBeInTheDocument();
    });

    it('hides icon when showIcon is false', () => {
      render(<PdfStatusBadge state="uploading" showIcon={false} />);

      const badge = screen.getByTestId('pdf-status-badge');
      const icon = badge.querySelector('svg');
      expect(icon).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Variant Tests
  // ============================================================================

  describe('Variants', () => {
    it('applies default variant styles', () => {
      render(<PdfStatusBadge state="uploading" variant="default" />);

      const badge = screen.getByTestId('pdf-status-badge');
      expect(badge).toHaveClass('px-3', 'py-1', 'text-sm');
    });

    it('applies compact variant styles', () => {
      render(<PdfStatusBadge state="uploading" variant="compact" />);

      const badge = screen.getByTestId('pdf-status-badge');
      expect(badge).toHaveClass('px-2', 'py-0.5', 'text-xs');
    });
  });

  // ============================================================================
  // State-Specific Tests
  // ============================================================================

  describe('State-Specific Rendering', () => {
    it('renders pending state with gray colors', () => {
      render(<PdfStatusBadge state="pending" />);

      const badge = screen.getByTestId('pdf-status-badge');
      expect(badge).toHaveAttribute('data-state', 'pending');
      expect(badge).toHaveClass('bg-gray-100');
    });

    it('renders ready state with green colors', () => {
      render(<PdfStatusBadge state="ready" />);

      const badge = screen.getByTestId('pdf-status-badge');
      expect(badge).toHaveAttribute('data-state', 'ready');
      expect(badge).toHaveClass('bg-green-100');
    });

    it('renders failed state with red colors', () => {
      render(<PdfStatusBadge state="failed" />);

      const badge = screen.getByTestId('pdf-status-badge');
      expect(badge).toHaveAttribute('data-state', 'failed');
      expect(badge).toHaveClass('bg-red-100');
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has proper aria-label', () => {
      render(<PdfStatusBadge state="uploading" />);

      const badge = screen.getByTestId('pdf-status-badge');
      expect(badge).toHaveAttribute('aria-label', 'PDF Status: Uploading');
    });

    it('marks icon as aria-hidden', () => {
      render(<PdfStatusBadge state="uploading" />);

      const badge = screen.getByTestId('pdf-status-badge');
      const icon = badge.querySelector('svg');
      expect(icon).toHaveAttribute('aria-hidden', 'true');
    });

    it('provides semantic label for each state', () => {
      const states: PdfState[] = ['pending', 'uploading', 'ready', 'failed'];
      const expectedLabels = [
        'PDF Status: Pending',
        'PDF Status: Uploading',
        'PDF Status: Ready',
        'PDF Status: Failed',
      ];

      states.forEach((state, index) => {
        const { unmount } = render(<PdfStatusBadge state={state} />);
        const badge = screen.getByTestId('pdf-status-badge');
        expect(badge).toHaveAttribute('aria-label', expectedLabels[index]);
        unmount();
      });
    });
  });

  // ============================================================================
  // Custom Classes Tests
  // ============================================================================

  describe('Custom Classes', () => {
    it('applies custom className', () => {
      render(<PdfStatusBadge state="uploading" className="custom-class" />);

      const badge = screen.getByTestId('pdf-status-badge');
      expect(badge).toHaveClass('custom-class');
    });

    it('preserves base classes with custom className', () => {
      render(<PdfStatusBadge state="uploading" className="custom-class" />);

      const badge = screen.getByTestId('pdf-status-badge');
      expect(badge).toHaveClass('backdrop-blur-md', 'custom-class');
    });
  });

  // ============================================================================
  // Glassmorphic Style Tests
  // ============================================================================

  describe('Glassmorphic Design', () => {
    it('applies backdrop-blur effect', () => {
      render(<PdfStatusBadge state="uploading" />);

      const badge = screen.getByTestId('pdf-status-badge');
      expect(badge).toHaveClass('backdrop-blur-md');
    });

    it('has no border (border-0)', () => {
      render(<PdfStatusBadge state="uploading" />);

      const badge = screen.getByTestId('pdf-status-badge');
      expect(badge).toHaveClass('border-0');
    });

    it('has transition animation', () => {
      render(<PdfStatusBadge state="uploading" />);

      const badge = screen.getByTestId('pdf-status-badge');
      expect(badge).toHaveClass('transition-all', 'duration-200');
    });
  });
});
