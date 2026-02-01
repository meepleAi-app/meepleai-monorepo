/**
 * AgentTypingIndicator Component Tests (Issue #3026)
 *
 * Test Coverage:
 * - Rendering and structure
 * - Accessibility attributes
 * - Animation elements
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { AgentTypingIndicator } from '../AgentTypingIndicator';

describe('AgentTypingIndicator', () => {
  describe('Rendering', () => {
    it('renders the typing indicator container', () => {
      render(<AgentTypingIndicator />);

      expect(screen.getByLabelText('Agent is typing')).toBeInTheDocument();
    });

    it('renders three animated dots', () => {
      const { container } = render(<AgentTypingIndicator />);

      const dots = container.querySelectorAll('.animate-bounce');
      expect(dots).toHaveLength(3);
    });

    it('applies correct styling to dots', () => {
      const { container } = render(<AgentTypingIndicator />);

      const dots = container.querySelectorAll('.rounded-full');
      expect(dots).toHaveLength(3);

      dots.forEach((dot) => {
        expect(dot).toHaveClass('w-2', 'h-2');
      });
    });
  });

  describe('Animation', () => {
    it('has staggered animation delays', () => {
      const { container } = render(<AgentTypingIndicator />);

      const dots = container.querySelectorAll('.animate-bounce');

      // First dot has -0.3s delay
      expect(dots[0]).toHaveClass('[animation-delay:-0.3s]');

      // Second dot has -0.15s delay
      expect(dots[1]).toHaveClass('[animation-delay:-0.15s]');

      // Third dot has no delay class (default 0s)
      expect(dots[2]).not.toHaveClass('[animation-delay:-0.3s]');
      expect(dots[2]).not.toHaveClass('[animation-delay:-0.15s]');
    });
  });

  describe('Accessibility', () => {
    it('has accessible label for screen readers', () => {
      render(<AgentTypingIndicator />);

      const indicator = screen.getByLabelText('Agent is typing');
      expect(indicator).toBeInTheDocument();
    });
  });

  describe('Structure', () => {
    it('has correct container structure', () => {
      const { container } = render(<AgentTypingIndicator />);

      // Outer flex container
      const outerContainer = container.querySelector('.flex.items-start');
      expect(outerContainer).toBeInTheDocument();

      // Inner bubble container
      const bubble = container.querySelector('.bg-\\[\\#f1f3f4\\]');
      expect(bubble).toBeInTheDocument();
      expect(bubble).toHaveClass('p-3', 'rounded-lg');

      // Dots container
      const dotsContainer = bubble?.querySelector('.flex.items-center.gap-1');
      expect(dotsContainer).toBeInTheDocument();
    });
  });
});
