/**
 * TokenQuotaDisplay Component Tests
 * Issue #3240: [FRONT-004] Token quota progress bar
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import React from 'react';

import { TokenQuotaDisplay } from '../TokenQuotaDisplay';

describe('TokenQuotaDisplay', () => {
  describe('Rendering', () => {
    it('renders Token Quota label', () => {
      render(<TokenQuotaDisplay />);
      expect(screen.getByText('Token Quota')).toBeInTheDocument();
    });

    it('renders reset time', () => {
      render(<TokenQuotaDisplay />);
      // Uses regex for locale-independent matching
      expect(screen.getByText(/Resets in/)).toBeInTheDocument();
    });

    it('renders progress bar', () => {
      render(<TokenQuotaDisplay />);
      const progressBar = document.querySelector('.h-3.rounded-full');
      expect(progressBar).toBeInTheDocument();
    });

    it('renders usage text with token counts', () => {
      render(<TokenQuotaDisplay />);
      // Matches locale-independent format (450 / 500 tokens with possible thousand separators)
      expect(screen.getByText(/450.*\/.*500.*tokens/)).toBeInTheDocument();
    });
  });

  describe('Warning State', () => {
    it('does not show warning at exactly 90%', () => {
      // Current mock data has 450/500 = 90%, warning shows when > 90%
      render(<TokenQuotaDisplay />);
      expect(screen.queryByText('Quota almost full')).not.toBeInTheDocument();
    });
  });

  describe('Progress Bar Styling', () => {
    it('applies correct width based on percentage', () => {
      render(<TokenQuotaDisplay />);
      // 450/500 = 90%
      const progressIndicator = document.querySelector('.h-full.transition-all');
      expect(progressIndicator).toHaveStyle({ width: '90%' });
    });

    it('applies yellow color for 90% usage', () => {
      // 90% is > 70% and <= 90%, should be yellow
      render(<TokenQuotaDisplay />);
      const progressIndicator = document.querySelector('.bg-yellow-500');
      expect(progressIndicator).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has proper heading structure', () => {
      render(<TokenQuotaDisplay />);
      const label = screen.getByText('Token Quota');
      expect(label.tagName.toLowerCase()).toBe('label');
    });
  });
});
