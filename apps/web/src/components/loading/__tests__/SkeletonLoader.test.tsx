/**
 * Tests for SkeletonLoader component
 * Loading placeholders with variant-specific styles
 */

import { render, screen } from '@testing-library/react';
import { SkeletonLoader } from '../SkeletonLoader';

// Mock useReducedMotion hook
const mockUseReducedMotion = jest.fn();
jest.mock('@/lib/animations', () => ({
  ...jest.requireActual('@/lib/animations'),
  useReducedMotion: () => mockUseReducedMotion(),
}));

describe('SkeletonLoader', () => {
  beforeEach(() => {
    mockUseReducedMotion.mockReturnValue(false);
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('Count prop', () => {
    it('should render correct number of skeletons based on count prop', () => {
      const { container } = render(<SkeletonLoader variant="games" count={3} />);
      const skeletons = container.querySelectorAll('[role="status"]');
      expect(skeletons).toHaveLength(3);
    });

    it('should render single skeleton when count is 1', () => {
      const { container } = render(<SkeletonLoader variant="games" count={1} />);
      const skeletons = container.querySelectorAll('[role="status"]');
      expect(skeletons).toHaveLength(1);
    });

    it('should default to count of 1 when not provided', () => {
      const { container } = render(<SkeletonLoader variant="games" />);
      const skeletons = container.querySelectorAll('[role="status"]');
      expect(skeletons).toHaveLength(1);
    });
  });

  describe('Variant-specific styles', () => {
    it('should apply games variant styles (card layout)', () => {
      const { container } = render(<SkeletonLoader variant="games" />);
      const skeleton = container.querySelector('[role="status"]');
      expect(skeleton).toHaveClass('h-64'); // Card height
      expect(skeleton).toHaveClass('rounded-lg');
    });

    it('should apply agents variant styles (list item layout)', () => {
      const { container } = render(<SkeletonLoader variant="agents" />);
      const skeleton = container.querySelector('[role="status"]');
      expect(skeleton).toHaveClass('h-20'); // List item height
      expect(skeleton).toHaveClass('rounded-md');
    });

    it('should apply message variant styles (chat message layout)', () => {
      const { container } = render(<SkeletonLoader variant="message" />);
      const skeleton = container.querySelector('[role="status"]');
      expect(skeleton).toHaveClass('h-16'); // Message height
      expect(skeleton).toHaveClass('rounded-xl');
    });

    it('should apply chatHistory variant styles (sidebar list)', () => {
      const { container } = render(<SkeletonLoader variant="chatHistory" />);
      const skeleton = container.querySelector('[role="status"]');
      expect(skeleton).toHaveClass('h-12'); // Compact list item
      expect(skeleton).toHaveClass('rounded-md');
    });
  });

  describe('Animation', () => {
    it('should have animate-pulse class when animate prop is true', () => {
      const { container } = render(<SkeletonLoader variant="games" animate={true} />);
      const skeleton = container.querySelector('[role="status"]');
      expect(skeleton).toHaveClass('animate-pulse');
    });

    it('should not have animate-pulse class when animate prop is false', () => {
      const { container } = render(<SkeletonLoader variant="games" animate={false} />);
      const skeleton = container.querySelector('[role="status"]');
      expect(skeleton).not.toHaveClass('animate-pulse');
    });

    it('should respect prefers-reduced-motion and disable animation', () => {
      mockUseReducedMotion.mockReturnValue(true);
      const { container } = render(<SkeletonLoader variant="games" animate={true} />);
      const skeleton = container.querySelector('[role="status"]');
      expect(skeleton).not.toHaveClass('animate-pulse');
    });

    it('should default to animated state', () => {
      const { container } = render(<SkeletonLoader variant="games" />);
      const skeleton = container.querySelector('[role="status"]');
      expect(skeleton).toHaveClass('animate-pulse');
    });
  });

  describe('Custom className', () => {
    it('should apply custom className', () => {
      const { container } = render(
        <SkeletonLoader variant="games" className="custom-skeleton" />
      );
      const skeleton = container.querySelector('[role="status"]');
      expect(skeleton).toHaveClass('custom-skeleton');
    });

    it('should preserve base classes with custom className', () => {
      const { container } = render(
        <SkeletonLoader variant="games" className="text-red-500" />
      );
      const skeleton = container.querySelector('[role="status"]');
      expect(skeleton).toHaveClass('text-red-500');
      expect(skeleton).toHaveClass('bg-slate-200'); // Base class
    });
  });

  describe('Accessibility', () => {
    it('should have correct ARIA attributes', () => {
      render(<SkeletonLoader variant="games" ariaLabel="Loading games" />);
      const skeleton = screen.getByRole('status');
      expect(skeleton).toBeInTheDocument();
      expect(skeleton).toHaveAttribute('aria-live', 'polite');
      expect(skeleton).toHaveAttribute('aria-label', 'Loading games');
    });

    it('should have default aria-label when not provided', () => {
      render(<SkeletonLoader variant="games" />);
      const skeleton = screen.getByRole('status');
      expect(skeleton).toHaveAttribute('aria-label', 'Loading...');
    });

    it('should include screen reader text', () => {
      render(<SkeletonLoader variant="games" ariaLabel="Loading games" />);
      const srText = screen.getByText('Loading games');
      expect(srText).toBeInTheDocument();
      expect(srText).toHaveClass('sr-only');
    });
  });

  describe('Multiple skeletons', () => {
    it('should render multiple skeletons with spacing', () => {
      const { container } = render(<SkeletonLoader variant="games" count={3} />);
      const wrapper = container.firstChild;
      expect(wrapper).toHaveClass('space-y-4'); // Vertical spacing
    });

    it('should render each skeleton with unique key', () => {
      const { container } = render(<SkeletonLoader variant="agents" count={5} />);
      const skeletons = container.querySelectorAll('[role="status"]');
      expect(skeletons).toHaveLength(5);
    });
  });

  describe('Snapshot tests', () => {
    it('should match snapshot for games variant', () => {
      const { container } = render(<SkeletonLoader variant="games" count={2} />);
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for agents variant', () => {
      const { container } = render(<SkeletonLoader variant="agents" count={3} />);
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for message variant', () => {
      const { container } = render(<SkeletonLoader variant="message" count={2} />);
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for chatHistory variant', () => {
      const { container } = render(<SkeletonLoader variant="chatHistory" count={4} />);
      expect(container.firstChild).toMatchSnapshot();
    });
  });
});
