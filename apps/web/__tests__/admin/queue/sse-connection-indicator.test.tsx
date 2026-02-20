/**
 * SSEConnectionIndicator Component Tests (Issue #4737)
 *
 * Tests for the SSE connection status indicator:
 * - All 5 connection states render correctly
 * - Button enabled/disabled per state
 * - Correct labels and accessibility attributes
 * - Reconnect callback behavior
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { SSEConnectionIndicator } from '@/app/admin/(dashboard)/knowledge-base/queue/components/sse-connection-indicator';

describe('SSEConnectionIndicator', () => {
  describe('Connected State', () => {
    it('should display "Live" label', () => {
      render(<SSEConnectionIndicator state="connected" />);
      expect(screen.getByText('Live')).toBeInTheDocument();
    });

    it('should have correct aria-label', () => {
      render(<SSEConnectionIndicator state="connected" />);
      expect(screen.getByRole('button')).toHaveAttribute(
        'aria-label',
        'SSE connection: Live',
      );
    });

    it('should be disabled (not reconnectable)', () => {
      render(<SSEConnectionIndicator state="connected" />);
      expect(screen.getByRole('button')).toBeDisabled();
    });

    it('should show emerald styling', () => {
      render(<SSEConnectionIndicator state="connected" />);
      expect(screen.getByRole('button')).toHaveClass('bg-emerald-50');
    });
  });

  describe('Connecting State', () => {
    it('should display "Connecting..." label', () => {
      render(<SSEConnectionIndicator state="connecting" />);
      expect(screen.getByText('Connecting...')).toBeInTheDocument();
    });

    it('should be disabled', () => {
      render(<SSEConnectionIndicator state="connecting" />);
      expect(screen.getByRole('button')).toBeDisabled();
    });

    it('should show amber styling', () => {
      render(<SSEConnectionIndicator state="connecting" />);
      expect(screen.getByRole('button')).toHaveClass('bg-amber-50');
    });
  });

  describe('Reconnecting State', () => {
    it('should display "Reconnecting..." label', () => {
      render(<SSEConnectionIndicator state="reconnecting" />);
      expect(screen.getByText('Reconnecting...')).toBeInTheDocument();
    });

    it('should be disabled', () => {
      render(<SSEConnectionIndicator state="reconnecting" />);
      expect(screen.getByRole('button')).toBeDisabled();
    });
  });

  describe('Error State', () => {
    it('should display "Disconnected" label', () => {
      render(<SSEConnectionIndicator state="error" />);
      expect(screen.getByText('Disconnected')).toBeInTheDocument();
    });

    it('should be enabled for reconnection', () => {
      render(<SSEConnectionIndicator state="error" onReconnect={() => {}} />);
      expect(screen.getByRole('button')).not.toBeDisabled();
    });

    it('should show red styling', () => {
      render(<SSEConnectionIndicator state="error" />);
      expect(screen.getByRole('button')).toHaveClass('bg-red-50');
    });

    it('should include reconnect hint in aria-label', () => {
      render(<SSEConnectionIndicator state="error" />);
      expect(screen.getByRole('button')).toHaveAttribute(
        'aria-label',
        'SSE connection: Disconnected. Click to reconnect.',
      );
    });

    it('should show reconnect tooltip', () => {
      render(<SSEConnectionIndicator state="error" />);
      expect(screen.getByRole('button')).toHaveAttribute('title', 'Click to reconnect');
    });

    it('should call onReconnect when clicked', () => {
      const onReconnect = vi.fn();
      render(<SSEConnectionIndicator state="error" onReconnect={onReconnect} />);
      fireEvent.click(screen.getByRole('button'));
      expect(onReconnect).toHaveBeenCalledOnce();
    });
  });

  describe('Closed State', () => {
    it('should display "Offline" label', () => {
      render(<SSEConnectionIndicator state="closed" />);
      expect(screen.getByText('Offline')).toBeInTheDocument();
    });

    it('should be enabled for reconnection', () => {
      render(<SSEConnectionIndicator state="closed" onReconnect={() => {}} />);
      expect(screen.getByRole('button')).not.toBeDisabled();
    });

    it('should call onReconnect when clicked', () => {
      const onReconnect = vi.fn();
      render(<SSEConnectionIndicator state="closed" onReconnect={onReconnect} />);
      fireEvent.click(screen.getByRole('button'));
      expect(onReconnect).toHaveBeenCalledOnce();
    });
  });

  describe('Ping Animation', () => {
    it('should show ping animation for connected state', () => {
      const { container } = render(<SSEConnectionIndicator state="connected" />);
      expect(container.querySelector('.animate-ping')).toBeInTheDocument();
    });

    it('should show ping animation for connecting state', () => {
      const { container } = render(<SSEConnectionIndicator state="connecting" />);
      expect(container.querySelector('.animate-ping')).toBeInTheDocument();
    });

    it('should show ping animation for reconnecting state', () => {
      const { container } = render(<SSEConnectionIndicator state="reconnecting" />);
      expect(container.querySelector('.animate-ping')).toBeInTheDocument();
    });

    it('should not show ping animation for closed state', () => {
      const { container } = render(<SSEConnectionIndicator state="closed" />);
      expect(container.querySelector('.animate-ping')).not.toBeInTheDocument();
    });

    it('should not show ping animation for error state', () => {
      const { container } = render(<SSEConnectionIndicator state="error" />);
      expect(container.querySelector('.animate-ping')).not.toBeInTheDocument();
    });
  });

  describe('No onReconnect', () => {
    it('should not throw when clicked without onReconnect', () => {
      render(<SSEConnectionIndicator state="error" />);
      expect(() => fireEvent.click(screen.getByRole('button'))).not.toThrow();
    });
  });
});
