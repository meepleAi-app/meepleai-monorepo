/**
 * Tests for TypingIndicator component
 * Animated typing indicator for chat interfaces
 */

import { render, screen } from '@testing-library/react';
import { TypingIndicator } from '../TypingIndicator';

// Mock useReducedMotion hook
const mockUseReducedMotion = jest.fn();
jest.mock('@/lib/animations', () => ({
  ...jest.requireActual('@/lib/animations'),
  useReducedMotion: () => mockUseReducedMotion(),
}));

// Mock framer-motion to avoid animation issues in tests
jest.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: any) => <div {...props}>{children}</div>,
    span: ({ children, ...props }: any) => <span {...props}>{children}</span>,
  },
  AnimatePresence: ({ children }: any) => <>{children}</>,
}));

describe('TypingIndicator', () => {
  beforeEach(() => {
    mockUseReducedMotion.mockReturnValue(false);
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('Visibility', () => {
    it('should render when visible prop is true', () => {
      render(<TypingIndicator visible={true} agentName="AI Assistant" />);
      expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('should not render when visible prop is false', () => {
      render(<TypingIndicator visible={false} agentName="AI Assistant" />);
      expect(screen.queryByRole('status')).not.toBeInTheDocument();
    });
  });

  describe('Agent name display', () => {
    it('should display agentName in accessible text', () => {
      render(<TypingIndicator visible={true} agentName="AI Assistant" />);
      const status = screen.getByRole('status');
      expect(status).toHaveAttribute('aria-label', 'AI Assistant is typing');
    });

    it('should handle different agent names', () => {
      render(<TypingIndicator visible={true} agentName="Chess Expert" />);
      const status = screen.getByRole('status');
      expect(status).toHaveAttribute('aria-label', 'Chess Expert is typing');
    });

    it('should display screen reader text with agent name', () => {
      render(<TypingIndicator visible={true} agentName="Support Bot" />);
      expect(screen.getByText('Support Bot is typing...')).toBeInTheDocument();
    });
  });

  describe('Dots rendering', () => {
    it('should render 3 dots', () => {
      const { container } = render(
        <TypingIndicator visible={true} agentName="AI" />
      );
      const dots = container.querySelectorAll('.w-2.h-2');
      expect(dots).toHaveLength(3);
    });

    it('should render dots with rounded-full class', () => {
      const { container } = render(
        <TypingIndicator visible={true} agentName="AI" />
      );
      const dots = container.querySelectorAll('.rounded-full');
      expect(dots.length).toBeGreaterThanOrEqual(3);
    });
  });

  describe('Reduced motion', () => {
    it('should respect prefers-reduced-motion', () => {
      mockUseReducedMotion.mockReturnValue(true);
      const { container } = render(
        <TypingIndicator visible={true} agentName="AI" />
      );
      const status = container.querySelector('[role="status"]');
      expect(status).toBeInTheDocument();
      // Component should still render but with no animation variants
      // Animation behavior is mocked in tests, so we just verify it renders
    });

    it('should render with animation when reduced motion is false', () => {
      mockUseReducedMotion.mockReturnValue(false);
      render(<TypingIndicator visible={true} agentName="AI" />);
      expect(screen.getByRole('status')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have correct ARIA attributes', () => {
      render(<TypingIndicator visible={true} agentName="AI Assistant" />);
      const status = screen.getByRole('status');
      expect(status).toHaveAttribute('role', 'status');
      expect(status).toHaveAttribute('aria-live', 'polite');
      expect(status).toHaveAttribute('aria-label', 'AI Assistant is typing');
    });

    it('should include screen reader text', () => {
      render(<TypingIndicator visible={true} agentName="Helper Bot" />);
      const srText = screen.getByText('Helper Bot is typing...');
      expect(srText).toHaveClass('sr-only');
    });
  });

  describe('Custom className', () => {
    it('should apply custom className', () => {
      const { container } = render(
        <TypingIndicator
          visible={true}
          agentName="AI"
          className="custom-typing-indicator"
        />
      );
      const wrapper = container.querySelector('[role="status"]');
      expect(wrapper).toHaveClass('custom-typing-indicator');
    });

    it('should preserve base classes with custom className', () => {
      const { container } = render(
        <TypingIndicator visible={true} agentName="AI" className="mt-4" />
      );
      const wrapper = container.querySelector('[role="status"]');
      expect(wrapper).toHaveClass('mt-4');
      expect(wrapper).toHaveClass('flex'); // Base class
    });
  });

  describe('Snapshot test', () => {
    it('should match snapshot when visible', () => {
      const { container } = render(
        <TypingIndicator visible={true} agentName="AI Assistant" />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot when not visible', () => {
      const { container } = render(
        <TypingIndicator visible={false} agentName="AI Assistant" />
      );
      expect(container.firstChild).toMatchSnapshot();
    });
  });
});
