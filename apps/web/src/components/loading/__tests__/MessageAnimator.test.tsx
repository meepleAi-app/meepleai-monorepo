/**
 * Tests for MessageAnimator component
 * Wrapper for animating chat messages with direction-specific slide-in effects
 */

import { render, screen } from '@testing-library/react';
import { MessageAnimator } from '../MessageAnimator';

// Mock useReducedMotion hook
const mockUseReducedMotion = jest.fn();
jest.mock('@/lib/animations', () => ({
  ...jest.requireActual('@/lib/animations'),
  useReducedMotion: () => mockUseReducedMotion(),
}));

// Mock framer-motion with props tracking
const mockMotionDiv = jest.fn(({ children, ...props }: any) => (
  <div {...props}>{children}</div>
));

jest.mock('framer-motion', () => ({
  motion: {
    div: (props: any) => mockMotionDiv(props),
  },
}));

describe('MessageAnimator', () => {
  beforeEach(() => {
    mockUseReducedMotion.mockReturnValue(false);
    mockMotionDiv.mockClear();
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('Children wrapping', () => {
    it('should wrap children correctly', () => {
      render(
        <MessageAnimator direction="left" id="msg-1">
          <div>Test message content</div>
        </MessageAnimator>
      );
      expect(screen.getByText('Test message content')).toBeInTheDocument();
    });

    it('should wrap multiple children', () => {
      render(
        <MessageAnimator direction="right" id="msg-2">
          <div>First child</div>
          <div>Second child</div>
        </MessageAnimator>
      );
      expect(screen.getByText('First child')).toBeInTheDocument();
      expect(screen.getByText('Second child')).toBeInTheDocument();
    });

    it('should wrap text nodes', () => {
      render(
        <MessageAnimator direction="left" id="msg-3">
          Plain text message
        </MessageAnimator>
      );
      expect(screen.getByText('Plain text message')).toBeInTheDocument();
    });
  });

  describe('Direction-specific animation', () => {
    it('should apply left direction animation (x: -20)', () => {
      render(
        <MessageAnimator direction="left" id="msg-4">
          <div>AI Message</div>
        </MessageAnimator>
      );
      expect(mockMotionDiv).toHaveBeenCalled();
      const lastCall = mockMotionDiv.mock.calls[mockMotionDiv.mock.calls.length - 1][0];
      expect(lastCall.variants.initial.x).toBe(-20);
      expect(lastCall.variants.animate.x).toBe(0);
    });

    it('should apply right direction animation (x: 20)', () => {
      render(
        <MessageAnimator direction="right" id="msg-5">
          <div>User Message</div>
        </MessageAnimator>
      );
      expect(mockMotionDiv).toHaveBeenCalled();
      const lastCall = mockMotionDiv.mock.calls[mockMotionDiv.mock.calls.length - 1][0];
      expect(lastCall.variants.initial.x).toBe(20);
      expect(lastCall.variants.animate.x).toBe(0);
    });

    it('should include opacity animation for both directions', () => {
      const { rerender } = render(
        <MessageAnimator direction="left" id="msg-6">
          <div>Message</div>
        </MessageAnimator>
      );
      let lastCall = mockMotionDiv.mock.calls[mockMotionDiv.mock.calls.length - 1][0];
      expect(lastCall.variants.initial.opacity).toBe(0);
      expect(lastCall.variants.animate.opacity).toBe(1);

      mockMotionDiv.mockClear();
      rerender(
        <MessageAnimator direction="right" id="msg-7">
          <div>Message</div>
        </MessageAnimator>
      );
      lastCall = mockMotionDiv.mock.calls[mockMotionDiv.mock.calls.length - 1][0];
      expect(lastCall.variants.initial.opacity).toBe(0);
      expect(lastCall.variants.animate.opacity).toBe(1);
    });
  });

  describe('Delay prop', () => {
    it('should respect delay prop', () => {
      render(
        <MessageAnimator direction="left" id="msg-8" delay={0.5}>
          <div>Delayed Message</div>
        </MessageAnimator>
      );
      const lastCall = mockMotionDiv.mock.calls[mockMotionDiv.mock.calls.length - 1][0];
      expect(lastCall.transition.delay).toBe(0.5);
    });

    it('should default to 0 delay when not provided', () => {
      render(
        <MessageAnimator direction="left" id="msg-9">
          <div>Message</div>
        </MessageAnimator>
      );
      const lastCall = mockMotionDiv.mock.calls[mockMotionDiv.mock.calls.length - 1][0];
      expect(lastCall.transition.delay).toBe(0);
    });
  });

  describe('Reduced motion', () => {
    it('should respect prefers-reduced-motion', () => {
      mockUseReducedMotion.mockReturnValue(true);
      render(
        <MessageAnimator direction="left" id="msg-10">
          <div>Message</div>
        </MessageAnimator>
      );
      const lastCall = mockMotionDiv.mock.calls[mockMotionDiv.mock.calls.length - 1][0];
      // Simplified variants when reduced motion is true
      expect(lastCall.variants.initial.x).toBe(0);
      expect(lastCall.variants.animate.x).toBe(0);
      expect(lastCall.variants.initial.opacity).toBe(1);
      expect(lastCall.variants.animate.opacity).toBe(1);
    });
  });

  describe('Data attributes', () => {
    it('should set data-message-id attribute', () => {
      const { container } = render(
        <MessageAnimator direction="left" id="msg-11">
          <div>Message</div>
        </MessageAnimator>
      );
      const wrapper = container.firstChild;
      expect(wrapper).toHaveAttribute('data-message-id', 'msg-11');
    });

    it('should set data-animation-complete attribute', () => {
      const { container } = render(
        <MessageAnimator direction="right" id="msg-12">
          <div>Message</div>
        </MessageAnimator>
      );
      const wrapper = container.firstChild;
      expect(wrapper).toHaveAttribute('data-animation-complete', 'false');
    });

    it('should handle different message IDs', () => {
      const { container: container1 } = render(
        <MessageAnimator direction="left" id="unique-id-123">
          <div>Message 1</div>
        </MessageAnimator>
      );
      expect(container1.firstChild).toHaveAttribute('data-message-id', 'unique-id-123');

      const { container: container2 } = render(
        <MessageAnimator direction="right" id="another-id-456">
          <div>Message 2</div>
        </MessageAnimator>
      );
      expect(container2.firstChild).toHaveAttribute('data-message-id', 'another-id-456');
    });
  });

  describe('Snapshot tests', () => {
    it('should match snapshot for left direction', () => {
      const { container } = render(
        <MessageAnimator direction="left" id="msg-13">
          <div>AI Message</div>
        </MessageAnimator>
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for right direction', () => {
      const { container } = render(
        <MessageAnimator direction="right" id="msg-14">
          <div>User Message</div>
        </MessageAnimator>
      );
      expect(container.firstChild).toMatchSnapshot();
    });
  });
});
