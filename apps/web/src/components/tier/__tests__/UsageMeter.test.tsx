/**
 * UsageMeter Test Suite (Game Night Improvvisata - E2-4)
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { UsageMeter } from '../UsageMeter';

describe('UsageMeter', () => {
  describe('Rendering', () => {
    it('renders label and current/max text', () => {
      render(<UsageMeter label="Giochi privati" current={2} max={3} />);
      expect(screen.getByTestId('usage-meter')).toBeInTheDocument();
      expect(screen.getByText('Giochi privati')).toBeInTheDocument();
      expect(screen.getByText('2/3')).toBeInTheDocument();
    });

    it('renders progress bar with correct aria-label', () => {
      render(<UsageMeter label="PDF" current={5} max={20} />);
      expect(screen.getByRole('progressbar')).toHaveAttribute('aria-label', 'PDF: 5 di 20');
    });
  });

  describe('Color coding', () => {
    it('shows emerald (green) color for usage below 80%', () => {
      const { container } = render(<UsageMeter label="Test" current={5} max={10} />);
      const progressBar = container.querySelector('[role="progressbar"]');
      expect(progressBar).toHaveClass('[&>div]:bg-emerald-500');
    });

    it('shows amber color at 80% usage', () => {
      const { container } = render(<UsageMeter label="Test" current={8} max={10} />);
      const progressBar = container.querySelector('[role="progressbar"]');
      expect(progressBar).toHaveClass('[&>div]:bg-amber-500');
    });

    it('shows amber color at 90% usage', () => {
      const { container } = render(<UsageMeter label="Test" current={9} max={10} />);
      const progressBar = container.querySelector('[role="progressbar"]');
      expect(progressBar).toHaveClass('[&>div]:bg-amber-500');
    });

    it('shows red color at 100% usage', () => {
      const { container } = render(<UsageMeter label="Test" current={10} max={10} />);
      const progressBar = container.querySelector('[role="progressbar"]');
      expect(progressBar).toHaveClass('[&>div]:bg-red-500');
    });

    it('shows red color when over limit', () => {
      const { container } = render(<UsageMeter label="Test" current={12} max={10} />);
      const progressBar = container.querySelector('[role="progressbar"]');
      expect(progressBar).toHaveClass('[&>div]:bg-red-500');
    });
  });

  describe('Unlimited max', () => {
    it('shows "Illimitato" for very large max values', () => {
      render(<UsageMeter label="Query" current={5} max={9_999_999} />);
      expect(screen.getByText('5/Illimitato')).toBeInTheDocument();
    });

    it('shows zero progress for unlimited max', () => {
      render(<UsageMeter label="Query" current={5} max={9_999_999} />);
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-label', 'Query: 5 di illimitato');
    });
  });

  describe('Edge cases', () => {
    it('handles zero values', () => {
      render(<UsageMeter label="Test" current={0} max={10} />);
      expect(screen.getByText('0/10')).toBeInTheDocument();
    });

    it('handles zero max gracefully', () => {
      render(<UsageMeter label="Test" current={0} max={0} />);
      expect(screen.getByText('0/0')).toBeInTheDocument();
    });
  });
});
