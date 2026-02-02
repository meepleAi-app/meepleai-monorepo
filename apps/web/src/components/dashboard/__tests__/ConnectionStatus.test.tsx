/**
 * ConnectionStatus Component Tests (Issue #3324)
 *
 * Tests for SSE connection status indicator component
 *
 * Coverage areas:
 * - Rendering with different connection states
 * - Label and icon display
 * - Tooltip content
 * - Accessibility (ARIA, roles)
 * - Variants (size)
 *
 * Target: 95%+ coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ConnectionStatus } from '../ConnectionStatus';
import type { ConnectionState } from '@/lib/hooks/useDashboardStream';

describe('ConnectionStatus', () => {
  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders with required state prop', () => {
      render(<ConnectionStatus state="connected" />);

      expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('renders status dot', () => {
      const { container } = render(<ConnectionStatus state="connected" />);

      const dot = container.querySelector('.rounded-full');
      expect(dot).toBeInTheDocument();
    });

    it('renders label by default', () => {
      render(<ConnectionStatus state="connected" />);

      expect(screen.getByText('Live')).toBeInTheDocument();
    });

    it('hides label when showLabel is false', () => {
      render(<ConnectionStatus state="connected" showLabel={false} />);

      expect(screen.queryByText('Live')).not.toBeInTheDocument();
    });

    it('shows custom label when provided', () => {
      render(<ConnectionStatus state="connected" label="Online" />);

      expect(screen.getByText('Online')).toBeInTheDocument();
      expect(screen.queryByText('Live')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Connection State Tests
  // ============================================================================

  describe('Connection States', () => {
    const states: { state: ConnectionState; expectedLabel: string }[] = [
      { state: 'connected', expectedLabel: 'Live' },
      { state: 'connecting', expectedLabel: 'Connecting...' },
      { state: 'reconnecting', expectedLabel: 'Reconnecting...' },
      { state: 'disconnected', expectedLabel: 'Offline' },
      { state: 'error', expectedLabel: 'Connection Error' },
    ];

    states.forEach(({ state, expectedLabel }) => {
      it(`displays correct label for ${state} state`, () => {
        render(<ConnectionStatus state={state} />);

        expect(screen.getByText(expectedLabel)).toBeInTheDocument();
      });
    });

    it('applies correct color class for connected state', () => {
      const { container } = render(<ConnectionStatus state="connected" />);

      const statusElement = container.querySelector('[role="status"]');
      expect(statusElement).toHaveClass('text-green-600');
    });

    it('applies correct color class for connecting state', () => {
      const { container } = render(<ConnectionStatus state="connecting" />);

      const statusElement = container.querySelector('[role="status"]');
      expect(statusElement).toHaveClass('text-yellow-600');
    });

    it('applies correct color class for error state', () => {
      const { container } = render(<ConnectionStatus state="error" />);

      const statusElement = container.querySelector('[role="status"]');
      expect(statusElement).toHaveClass('text-destructive');
    });

    it('applies pulse animation for connecting state', () => {
      const { container } = render(<ConnectionStatus state="connecting" />);

      const dot = container.querySelector('.rounded-full');
      expect(dot).toHaveClass('animate-pulse');
    });

    it('applies pulse animation for reconnecting state', () => {
      const { container } = render(<ConnectionStatus state="reconnecting" />);

      const dot = container.querySelector('.rounded-full');
      expect(dot).toHaveClass('animate-pulse');
    });

    it('does not apply pulse animation for connected state', () => {
      const { container } = render(<ConnectionStatus state="connected" />);

      const dot = container.querySelector('.rounded-full');
      expect(dot).not.toHaveClass('animate-pulse');
    });
  });

  // ============================================================================
  // Size Variant Tests
  // ============================================================================

  describe('Size Variants', () => {
    it('applies small size classes by default', () => {
      const { container } = render(<ConnectionStatus state="connected" />);

      const statusElement = container.querySelector('[role="status"]');
      expect(statusElement).toHaveClass('text-xs');

      const dot = container.querySelector('.rounded-full');
      expect(dot).toHaveClass('h-1.5');
      expect(dot).toHaveClass('w-1.5');
    });

    it('applies medium size classes', () => {
      const { container } = render(<ConnectionStatus state="connected" size="md" />);

      const statusElement = container.querySelector('[role="status"]');
      expect(statusElement).toHaveClass('text-sm');

      const dot = container.querySelector('.rounded-full');
      expect(dot).toHaveClass('h-2');
      expect(dot).toHaveClass('w-2');
    });

    it('applies large size classes', () => {
      const { container } = render(<ConnectionStatus state="connected" size="lg" />);

      const statusElement = container.querySelector('[role="status"]');
      expect(statusElement).toHaveClass('text-base');

      const dot = container.querySelector('.rounded-full');
      expect(dot).toHaveClass('h-2.5');
      expect(dot).toHaveClass('w-2.5');
    });
  });

  // ============================================================================
  // Icon Tests
  // ============================================================================

  describe('Icon Display', () => {
    it('does not show icon by default', () => {
      const { container } = render(<ConnectionStatus state="connected" />);

      const svg = container.querySelector('svg');
      // Only the dot span should exist, no icon SVG
      const statusElement = container.querySelector('[role="status"]');
      const svgsInStatus = statusElement?.querySelectorAll('svg');
      expect(svgsInStatus?.length ?? 0).toBe(0);
    });

    it('shows icon when showIcon is true', () => {
      const { container } = render(<ConnectionStatus state="connected" showIcon />);

      const statusElement = container.querySelector('[role="status"]');
      const svg = statusElement?.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('shows spinning icon for connecting state', () => {
      const { container } = render(<ConnectionStatus state="connecting" showIcon />);

      const statusElement = container.querySelector('[role="status"]');
      const svg = statusElement?.querySelector('svg');
      expect(svg).toHaveClass('animate-spin');
    });

    it('shows spinning icon for reconnecting state', () => {
      const { container } = render(<ConnectionStatus state="reconnecting" showIcon />);

      const statusElement = container.querySelector('[role="status"]');
      const svg = statusElement?.querySelector('svg');
      expect(svg).toHaveClass('animate-spin');
    });

    it('does not show spinning icon for connected state', () => {
      const { container } = render(<ConnectionStatus state="connected" showIcon />);

      const statusElement = container.querySelector('[role="status"]');
      const svg = statusElement?.querySelector('svg');
      expect(svg).not.toHaveClass('animate-spin');
    });
  });

  // ============================================================================
  // Tooltip Tests
  // ============================================================================

  describe('Tooltip', () => {
    // Note: Radix Tooltip duplicates content for accessibility (visible + aria-hidden)
    // We use findAllByText and check length >= 1

    it('shows tooltip on hover', async () => {
      const user = userEvent.setup();
      render(<ConnectionStatus state="connected" />);

      const statusElement = screen.getByRole('status');
      await user.hover(statusElement);

      // Tooltip content should be visible (Radix duplicates for a11y)
      const tooltips = await screen.findAllByText(/Real-time updates active/);
      expect(tooltips.length).toBeGreaterThanOrEqual(1);
    });

    it('shows correct tooltip for connected state', async () => {
      const user = userEvent.setup();
      render(<ConnectionStatus state="connected" />);

      const statusElement = screen.getByRole('status');
      await user.hover(statusElement);

      const tooltips = await screen.findAllByText(/Real-time updates active/);
      expect(tooltips.length).toBeGreaterThanOrEqual(1);
    });

    it('shows correct tooltip for connecting state', async () => {
      const user = userEvent.setup();
      render(<ConnectionStatus state="connecting" />);

      const statusElement = screen.getByRole('status');
      await user.hover(statusElement);

      const tooltips = await screen.findAllByText(/Establishing real-time connection/);
      expect(tooltips.length).toBeGreaterThanOrEqual(1);
    });

    it('shows correct tooltip for disconnected state', async () => {
      const user = userEvent.setup();
      render(<ConnectionStatus state="disconnected" />);

      const statusElement = screen.getByRole('status');
      await user.hover(statusElement);

      const tooltips = await screen.findAllByText(/Real-time updates disabled/);
      expect(tooltips.length).toBeGreaterThanOrEqual(1);
    });

    it('shows correct tooltip for error state', async () => {
      const user = userEvent.setup();
      render(<ConnectionStatus state="error" />);

      const statusElement = screen.getByRole('status');
      await user.hover(statusElement);

      const tooltips = await screen.findAllByText(/Connection failed/);
      expect(tooltips.length).toBeGreaterThanOrEqual(1);
    });

    it('shows reconnect attempts in tooltip', async () => {
      const user = userEvent.setup();
      render(<ConnectionStatus state="reconnecting" reconnectAttempts={3} />);

      const statusElement = screen.getByRole('status');
      await user.hover(statusElement);

      const tooltips = await screen.findAllByText(/Reconnection attempt 3/);
      expect(tooltips.length).toBeGreaterThanOrEqual(1);
    });

    it('shows last heartbeat time in tooltip', async () => {
      const user = userEvent.setup();
      const lastHeartbeat = new Date();
      render(<ConnectionStatus state="connected" lastHeartbeat={lastHeartbeat} />);

      const statusElement = screen.getByRole('status');
      await user.hover(statusElement);

      const tooltips = await screen.findAllByText(/Last heartbeat:/);
      expect(tooltips.length).toBeGreaterThanOrEqual(1);
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has status role', () => {
      render(<ConnectionStatus state="connected" />);

      expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('has correct aria-label', () => {
      render(<ConnectionStatus state="connected" />);

      const statusElement = screen.getByRole('status');
      expect(statusElement).toHaveAttribute('aria-label', 'Connection status: Live');
    });

    it('updates aria-label based on state', () => {
      render(<ConnectionStatus state="disconnected" />);

      const statusElement = screen.getByRole('status');
      expect(statusElement).toHaveAttribute('aria-label', 'Connection status: Offline');
    });

    it('uses custom label in aria-label', () => {
      render(<ConnectionStatus state="connected" label="Online" />);

      const statusElement = screen.getByRole('status');
      expect(statusElement).toHaveAttribute('aria-label', 'Connection status: Online');
    });

    it('status dot has aria-hidden', () => {
      const { container } = render(<ConnectionStatus state="connected" />);

      const dot = container.querySelector('.rounded-full');
      expect(dot).toHaveAttribute('aria-hidden', 'true');
    });

    it('icon has aria-hidden when shown', () => {
      const { container } = render(<ConnectionStatus state="connected" showIcon />);

      const statusElement = container.querySelector('[role="status"]');
      const svg = statusElement?.querySelector('svg');
      expect(svg).toHaveAttribute('aria-hidden', 'true');
    });
  });

  // ============================================================================
  // Custom Class Tests
  // ============================================================================

  describe('Custom Classes', () => {
    it('applies custom className', () => {
      const { container } = render(<ConnectionStatus state="connected" className="custom-class" />);

      const statusElement = container.querySelector('.custom-class');
      expect(statusElement).toBeInTheDocument();
    });

    it('merges custom className with default classes', () => {
      const { container } = render(<ConnectionStatus state="connected" className="my-custom-class" />);

      const statusElement = container.querySelector('[role="status"]');
      expect(statusElement).toHaveClass('my-custom-class');
      expect(statusElement).toHaveClass('flex');
      expect(statusElement).toHaveClass('items-center');
    });
  });

  // ============================================================================
  // Edge Cases
  // ============================================================================

  describe('Edge Cases', () => {
    // Note: Radix Tooltip duplicates content for accessibility (visible + aria-hidden)
    // We use findAllByText and check length >= 1

    it('handles undefined lastHeartbeat', async () => {
      const user = userEvent.setup();
      render(<ConnectionStatus state="connected" lastHeartbeat={undefined} />);

      const statusElement = screen.getByRole('status');
      await user.hover(statusElement);

      // Should not show "Last heartbeat:" when undefined
      const tooltips = await screen.findAllByText(/Real-time updates active/);
      expect(tooltips.length).toBeGreaterThanOrEqual(1);
    });

    it('handles null lastHeartbeat', async () => {
      const user = userEvent.setup();
      render(<ConnectionStatus state="connected" lastHeartbeat={null} />);

      const statusElement = screen.getByRole('status');
      await user.hover(statusElement);

      // Should show basic message without heartbeat time
      const tooltips = await screen.findAllByText('Real-time updates active');
      expect(tooltips.length).toBeGreaterThanOrEqual(1);
    });

    it('handles zero reconnect attempts', async () => {
      const user = userEvent.setup();
      render(<ConnectionStatus state="reconnecting" reconnectAttempts={0} />);

      const statusElement = screen.getByRole('status');
      await user.hover(statusElement);

      const tooltips = await screen.findAllByText(/Reconnection attempt 0/);
      expect(tooltips.length).toBeGreaterThanOrEqual(1);
    });

    it('forwards ref correctly', () => {
      const ref = vi.fn();
      render(<ConnectionStatus state="connected" ref={ref} />);

      expect(ref).toHaveBeenCalled();
    });
  });
});
