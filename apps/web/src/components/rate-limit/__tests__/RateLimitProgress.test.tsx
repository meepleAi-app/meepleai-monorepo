/**
 * RateLimitProgress Component Tests
 * Issue #2749: Frontend - Rate Limit Feedback UI
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { RateLimitProgress } from '../RateLimitProgress';

describe('RateLimitProgress', () => {
  describe('initialization', () => {
    it('should render with label and count', () => {
      render(
        <RateLimitProgress current={3} max={5} label="Pending Requests" />
      );

      expect(screen.getByText('Pending Requests')).toBeInTheDocument();
      expect(screen.getByText('3 / 5')).toBeInTheDocument();
    });

    it('should calculate percentage correctly', () => {
      const { container } = render(
        <RateLimitProgress current={3} max={5} label="Test" />
      );

      // 3/5 = 60%
      const progressBar = container.querySelector('[role="progressbar"]');
      expect(progressBar).toHaveAttribute('aria-valuenow', '60');
    });

    it('should handle zero max without error', () => {
      render(<RateLimitProgress current={0} max={0} label="Test" />);

      expect(screen.getByText('0 / 0')).toBeInTheDocument();
    });
  });

  describe('color coding', () => {
    it('should use normal color for < 80%', () => {
      const { container } = render(
        <RateLimitProgress current={3} max={5} label="Test" />
      );

      const progress = container.querySelector('.h-2');
      expect(progress).not.toHaveClass('[&>div]:bg-destructive');
      expect(progress).not.toHaveClass('[&>div]:bg-amber-500');
    });

    it('should use warning color for 80-99%', () => {
      const { container } = render(
        <RateLimitProgress current={4} max={5} label="Test" />
      );

      const progress = container.querySelector('.h-2');
      expect(progress).toHaveClass('[&>div]:bg-amber-500');
    });

    it('should use destructive color for >= 100%', () => {
      const { container } = render(
        <RateLimitProgress current={5} max={5} label="Test" />
      );

      const progress = container.querySelector('.h-2');
      expect(progress).toHaveClass('[&>div]:bg-destructive');
    });

    it('should use destructive color when over 100%', () => {
      const { container } = render(
        <RateLimitProgress current={6} max={5} label="Test" />
      );

      const progress = container.querySelector('.h-2');
      expect(progress).toHaveClass('[&>div]:bg-destructive');
    });
  });

  describe('reset time display', () => {
    it('should not show reset time when < 80%', () => {
      render(
        <RateLimitProgress
          current={3}
          max={5}
          label="Test"
          resetAt="2024-12-31T23:59:59Z"
        />
      );

      expect(screen.queryByText(/Resets/)).not.toBeInTheDocument();
    });

    it('should show reset time when >= 80%', () => {
      render(
        <RateLimitProgress
          current={4}
          max={5}
          label="Test"
          resetAt="2024-12-31T23:59:59Z"
        />
      );

      expect(screen.getByText(/Resets/)).toBeInTheDocument();
    });

    it('should not show reset time when resetAt is undefined', () => {
      render(<RateLimitProgress current={5} max={5} label="Test" />);

      expect(screen.queryByText(/Resets/)).not.toBeInTheDocument();
    });
  });

  describe('edge cases', () => {
    it('should cap percentage at 100%', () => {
      const { container } = render(
        <RateLimitProgress current={10} max={5} label="Test" />
      );

      const progressBar = container.querySelector('[role="progressbar"]');
      expect(progressBar).toHaveAttribute('aria-valuenow', '100');
    });

    it('should handle custom className', () => {
      const { container } = render(
        <RateLimitProgress
          current={3}
          max={5}
          label="Test"
          className="custom-class"
        />
      );

      const wrapper = container.firstChild;
      expect(wrapper).toHaveClass('custom-class');
    });
  });
});
