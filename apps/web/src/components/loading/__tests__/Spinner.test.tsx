/**
 * Tests for Spinner component
 * Issue #1951: Add coverage for loading components
 */

import { render } from '@testing-library/react';
import { Spinner } from '../Spinner';

describe('Spinner', () => {
  describe('Rendering', () => {
    it('renders spinner with default size (md)', () => {
      const { container } = render(<Spinner />);
      const spinner = container.querySelector('.animate-spin');
      expect(spinner).toBeInTheDocument();
    });

    it('renders SVG element', () => {
      const { container } = render(<Spinner />);
      const svg = container.querySelector('svg');
      expect(svg).toBeInTheDocument();
      expect(svg).toHaveAttribute('viewBox', '0 0 24 24');
    });

    it('has aria-hidden attribute', () => {
      const { container } = render(<Spinner />);
      const svg = container.querySelector('svg');
      expect(svg).toHaveAttribute('aria-hidden', 'true');
    });
  });

  describe('Size Variants', () => {
    it('renders small size (16px)', () => {
      const { container } = render(<Spinner size="sm" />);
      const svg = container.querySelector('svg');
      expect(svg).toHaveAttribute('width', '16');
      expect(svg).toHaveAttribute('height', '16');
    });

    it('renders medium size (24px) as default', () => {
      const { container } = render(<Spinner size="md" />);
      const svg = container.querySelector('svg');
      expect(svg).toHaveAttribute('width', '24');
      expect(svg).toHaveAttribute('height', '24');
    });

    it('renders large size (32px)', () => {
      const { container } = render(<Spinner size="lg" />);
      const svg = container.querySelector('svg');
      expect(svg).toHaveAttribute('width', '32');
      expect(svg).toHaveAttribute('height', '32');
    });

    it('defaults to medium size when no size prop provided', () => {
      const { container } = render(<Spinner />);
      const svg = container.querySelector('svg');
      expect(svg).toHaveAttribute('width', '24');
    });
  });

  describe('Styling', () => {
    it('applies custom className', () => {
      const { container } = render(<Spinner className="custom-class" />);
      const spinner = container.querySelector('.animate-spin');
      expect(spinner).toHaveClass('custom-class');
    });

    it('has inline-block display class', () => {
      const { container } = render(<Spinner />);
      const spinner = container.querySelector('.animate-spin');
      expect(spinner).toHaveClass('inline-block');
    });

    it('has animate-spin class for rotation', () => {
      const { container } = render(<Spinner />);
      const spinner = container.querySelector('div');
      expect(spinner).toHaveClass('animate-spin');
    });

    it('merges className with defaults', () => {
      const { container } = render(<Spinner className="text-blue-500" />);
      const spinner = container.querySelector('.animate-spin');
      expect(spinner).toHaveClass('inline-block', 'animate-spin', 'text-blue-500');
    });
  });

  describe('SVG Structure', () => {
    it('renders circle element', () => {
      const { container } = render(<Spinner />);
      const circle = container.querySelector('circle');
      expect(circle).toBeInTheDocument();
      expect(circle).toHaveClass('opacity-25');
    });

    it('renders path element', () => {
      const { container } = render(<Spinner />);
      const path = container.querySelector('path');
      expect(path).toBeInTheDocument();
      expect(path).toHaveClass('opacity-75');
    });

    it('uses currentColor for stroke and fill', () => {
      const { container } = render(<Spinner />);
      const circle = container.querySelector('circle');
      const path = container.querySelector('path');
      expect(circle).toHaveAttribute('stroke', 'currentColor');
      expect(path).toHaveAttribute('fill', 'currentColor');
    });
  });
});
