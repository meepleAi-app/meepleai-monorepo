/**
 * Tests for Spinner component
 * Simple loading spinner with size variants
 */

import { render, screen } from '@testing-library/react';
import { Spinner } from '../Spinner';

describe('Spinner', () => {
  describe('Size variants', () => {
    it('should render with small size (16px)', () => {
      const { container } = render(<Spinner size="sm" />);
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
      expect(spinner).toHaveAttribute('width', '16');
      expect(spinner).toHaveAttribute('height', '16');
    });

    it('should render with medium size (24px)', () => {
      const { container } = render(<Spinner size="md" />);
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
      expect(spinner).toHaveAttribute('width', '24');
      expect(spinner).toHaveAttribute('height', '24');
    });

    it('should render with large size (32px)', () => {
      const { container } = render(<Spinner size="lg" />);
      const spinner = container.querySelector('svg');
      expect(spinner).toBeInTheDocument();
      expect(spinner).toHaveAttribute('width', '32');
      expect(spinner).toHaveAttribute('height', '32');
    });

    it('should default to medium size when size prop is not provided', () => {
      const { container } = render(<Spinner />);
      const spinner = container.querySelector('svg');
      expect(spinner).toHaveAttribute('width', '24');
      expect(spinner).toHaveAttribute('height', '24');
    });
  });

  describe('Custom styling', () => {
    it('should apply custom className', () => {
      const { container } = render(<Spinner className="custom-spinner" />);
      const wrapper = container.firstChild;
      expect(wrapper).toHaveClass('custom-spinner');
    });

    it('should preserve base classes when custom className is provided', () => {
      const { container } = render(<Spinner className="text-red-500" />);
      const wrapper = container.firstChild;
      expect(wrapper).toHaveClass('text-red-500');
      expect(wrapper).toHaveClass('animate-spin'); // Base class should still be present
    });
  });

  describe('Accessibility', () => {
    it('should have aria-hidden="true" for decorative spinner', () => {
      const { container } = render(<Spinner />);
      const spinner = container.querySelector('svg');
      expect(spinner).toHaveAttribute('aria-hidden', 'true');
    });

    it('should not be focusable', () => {
      const { container } = render(<Spinner />);
      const spinner = container.querySelector('svg');
      expect(spinner).not.toHaveAttribute('tabindex');
    });
  });

  describe('Animation', () => {
    it('should have animate-spin class for CSS animation', () => {
      const { container } = render(<Spinner />);
      const wrapper = container.firstChild;
      expect(wrapper).toHaveClass('animate-spin');
    });
  });
});
