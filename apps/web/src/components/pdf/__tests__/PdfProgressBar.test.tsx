/**
 * PdfProgressBar Component Tests (Issue #4217)
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { PdfProgressBar } from '../PdfProgressBar';
import type { PdfState } from '@/types/pdf';

describe('PdfProgressBar', () => {
  describe('Rendering', () => {
    it('renders with default props', () => {
      render(<PdfProgressBar state="uploading" />);

      expect(screen.getByTestId('pdf-progress-bar')).toBeInTheDocument();
    });

    it('renders label by default', () => {
      render(<PdfProgressBar state="uploading" />);

      expect(screen.getByTestId('progress-label')).toBeInTheDocument();
      expect(screen.getByText('Uploading')).toBeInTheDocument();
    });

    it('hides label when showLabel is false', () => {
      render(<PdfProgressBar state="uploading" showLabel={false} />);

      expect(screen.queryByTestId('progress-label')).not.toBeInTheDocument();
    });

    it('displays progress percentage', () => {
      render(<PdfProgressBar state="uploading" progress={45} />);

      expect(screen.getByTestId('progress-percentage')).toHaveTextContent('45%');
    });
  });

  describe('Progress Values', () => {
    it('uses custom progress value when provided', () => {
      render(<PdfProgressBar state="uploading" progress={75} />);

      const progressBar = screen.getByTestId('pdf-progress-bar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '75');
    });

    it('uses default progress based on state', () => {
      const { rerender } = render(<PdfProgressBar state="uploading" />);
      expect(screen.getByTestId('pdf-progress-bar')).toHaveAttribute('aria-valuenow', '15');

      rerender(<PdfProgressBar state="extracting" />);
      expect(screen.getByTestId('pdf-progress-bar')).toHaveAttribute('aria-valuenow', '30');

      rerender(<PdfProgressBar state="ready" />);
      expect(screen.getByTestId('pdf-progress-bar')).toHaveAttribute('aria-valuenow', '100');
    });

    it('clamps progress value between 0 and 100', () => {
      const { rerender } = render(<PdfProgressBar state="uploading" progress={-10} />);
      expect(screen.getByTestId('pdf-progress-bar')).toHaveAttribute('aria-valuenow', '0');

      rerender(<PdfProgressBar state="uploading" progress={150} />);
      expect(screen.getByTestId('pdf-progress-bar')).toHaveAttribute('aria-valuenow', '100');
    });
  });

  describe('State Labels', () => {
    it('displays correct label for each state', () => {
      const states: Array<{ state: PdfState; label: string }> = [
        { state: 'pending', label: 'Pending' },
        { state: 'uploading', label: 'Uploading' },
        { state: 'extracting', label: 'Extracting' },
        { state: 'chunking', label: 'Chunking' },
        { state: 'embedding', label: 'Embedding' },
        { state: 'indexing', label: 'Indexing' },
        { state: 'ready', label: 'Ready' },
        { state: 'failed', label: 'Failed' },
      ];

      states.forEach(({ state, label }) => {
        const { unmount } = render(<PdfProgressBar state={state} />);
        expect(screen.getByText(label)).toBeInTheDocument();
        unmount();
      });
    });
  });

  describe('Accessibility', () => {
    it('has proper ARIA role', () => {
      render(<PdfProgressBar state="uploading" />);

      const progressBar = screen.getByTestId('pdf-progress-bar');
      expect(progressBar).toHaveAttribute('role', 'progressbar');
    });

    it('has proper ARIA attributes', () => {
      render(<PdfProgressBar state="uploading" progress={50} />);

      const progressBar = screen.getByTestId('pdf-progress-bar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '50');
      expect(progressBar).toHaveAttribute('aria-valuemin', '0');
      expect(progressBar).toHaveAttribute('aria-valuemax', '100');
      expect(progressBar).toHaveAttribute('aria-label', 'PDF Processing: Uploading');
    });

    it('marks percentage as live region', () => {
      render(<PdfProgressBar state="uploading" progress={50} />);

      const percentage = screen.getByTestId('progress-percentage');
      expect(percentage).toHaveAttribute('aria-live', 'polite');
    });
  });

  describe('Custom Classes', () => {
    it('applies custom className', () => {
      render(<PdfProgressBar state="uploading" className="custom-class" />);

      const progressBar = screen.getByTestId('pdf-progress-bar');
      expect(progressBar).toHaveClass('custom-class');
    });
  });
});
