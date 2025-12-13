/**
 * PresenceIndicator Tests (Issue #2055)
 *
 * Tests for editor lock presence indicator component.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import { PresenceIndicator, PresenceIndicatorCompact } from '../PresenceIndicator';
import type { EditorLock } from '@/lib/api/schemas';

const createMockLock = (overrides?: Partial<EditorLock>): EditorLock => ({
  gameId: '123e4567-e89b-12d3-a456-426614174000',
  lockedByUserId: '123e4567-e89b-12d3-a456-426614174001',
  lockedByUserEmail: 't***@e***.com',
  lockedAt: new Date().toISOString(),
  expiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString(), // 5 min from now
  isLocked: true,
  isCurrentUserLock: true,
  ...overrides,
});

describe('PresenceIndicator', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('idle state', () => {
    it('should show read-only status when idle', () => {
      render(<PresenceIndicator lockStatus={null} acquisitionStatus="idle" />);

      expect(screen.getByText('Sola lettura')).toBeInTheDocument();
    });

    it('should have gray styling when idle', () => {
      const { container } = render(
        <PresenceIndicator lockStatus={null} acquisitionStatus="idle" />
      );

      expect(container.firstChild).toHaveClass('text-gray-600');
    });
  });

  describe('acquiring state', () => {
    it('should show acquiring status', () => {
      render(<PresenceIndicator lockStatus={null} acquisitionStatus="acquiring" />);

      expect(screen.getByText('Acquisizione lock...')).toBeInTheDocument();
    });

    it('should have yellow styling when acquiring', () => {
      const { container } = render(
        <PresenceIndicator lockStatus={null} acquisitionStatus="acquiring" />
      );

      expect(container.firstChild).toHaveClass('text-yellow-600');
    });

    it('should show spinner animation', () => {
      const { container } = render(
        <PresenceIndicator lockStatus={null} acquisitionStatus="acquiring" />
      );

      const spinner = container.querySelector('.animate-spin');
      expect(spinner).toBeInTheDocument();
    });
  });

  describe('acquired state', () => {
    it('should show editing status when acquired', () => {
      render(<PresenceIndicator lockStatus={createMockLock()} acquisitionStatus="acquired" />);

      expect(screen.getByText('Stai modificando')).toBeInTheDocument();
    });

    it('should have green styling when acquired', () => {
      const { container } = render(
        <PresenceIndicator lockStatus={createMockLock()} acquisitionStatus="acquired" />
      );

      expect(container.firstChild).toHaveClass('text-green-600');
    });

    it('should show time remaining', () => {
      render(<PresenceIndicator lockStatus={createMockLock()} acquisitionStatus="acquired" />);

      // Should show time remaining (approximately 5:00)
      expect(screen.getByText(/\(\d+:\d+\)/)).toBeInTheDocument();
    });

    it('should update time remaining countdown', async () => {
      // Set a fixed system time
      const baseTime = new Date('2024-01-01T12:00:00Z').getTime();
      vi.setSystemTime(baseTime);

      render(
        <PresenceIndicator
          lockStatus={createMockLock({
            expiresAt: new Date(baseTime + 2 * 60 * 1000).toISOString(), // 2 min from base
          })}
          acquisitionStatus="acquired"
        />
      );

      // Initial time should be 2:00 (120 seconds remaining)
      expect(screen.getByText(/\(2:00\)/)).toBeInTheDocument();

      // Advance by 30 seconds
      await act(async () => {
        vi.advanceTimersByTime(30000);
      });

      // Time should decrease to 1:30
      expect(screen.getByText(/\(1:30\)/)).toBeInTheDocument();
    });

    it('should show "Scaduto" when lock expires', async () => {
      // Set a fixed system time
      const baseTime = new Date('2024-01-01T12:00:00Z').getTime();
      vi.setSystemTime(baseTime);

      render(
        <PresenceIndicator
          lockStatus={createMockLock({
            expiresAt: new Date(baseTime + 1000).toISOString(), // 1 second from base
          })}
          acquisitionStatus="acquired"
        />
      );

      // Advance past expiration
      await act(async () => {
        vi.advanceTimersByTime(2000);
      });

      expect(screen.getByText(/Scaduto/)).toBeInTheDocument();
    });

    it('should have pulsing dot animation', () => {
      const { container } = render(
        <PresenceIndicator lockStatus={createMockLock()} acquisitionStatus="acquired" />
      );

      const dot = container.querySelector('.animate-pulse');
      expect(dot).toBeInTheDocument();
    });
  });

  describe('conflict state', () => {
    it('should show other user email when conflict', () => {
      render(
        <PresenceIndicator
          lockStatus={createMockLock({
            lockedByUserEmail: 'o***@e***.com',
            isCurrentUserLock: false,
          })}
          acquisitionStatus="conflict"
        />
      );

      expect(screen.getByText('In modifica da o***@e***.com')).toBeInTheDocument();
    });

    it('should have orange styling when conflict', () => {
      const { container } = render(
        <PresenceIndicator
          lockStatus={createMockLock({
            isCurrentUserLock: false,
          })}
          acquisitionStatus="conflict"
        />
      );

      expect(container.firstChild).toHaveClass('text-orange-600');
    });

    it('should show generic conflict message when no email', () => {
      render(
        <PresenceIndicator
          lockStatus={createMockLock({
            lockedByUserEmail: null,
            isCurrentUserLock: false,
          })}
          acquisitionStatus="conflict"
        />
      );

      expect(screen.getByText('Conflitto rilevato')).toBeInTheDocument();
    });
  });

  describe('failed state', () => {
    it('should show error message when failed', () => {
      render(
        <PresenceIndicator lockStatus={null} acquisitionStatus="failed" lockError="Network error" />
      );

      expect(screen.getByText('Network error')).toBeInTheDocument();
    });

    it('should show default error when no lockError provided', () => {
      render(<PresenceIndicator lockStatus={null} acquisitionStatus="failed" />);

      expect(screen.getByText('Impossibile acquisire lock')).toBeInTheDocument();
    });

    it('should have red styling when failed', () => {
      const { container } = render(
        <PresenceIndicator lockStatus={null} acquisitionStatus="failed" />
      );

      expect(container.firstChild).toHaveClass('text-red-600');
    });
  });

  describe('accessibility', () => {
    it('should apply custom className', () => {
      const { container } = render(
        <PresenceIndicator lockStatus={null} acquisitionStatus="idle" className="custom-class" />
      );

      expect(container.firstChild).toHaveClass('custom-class');
    });
  });
});

describe('PresenceIndicatorCompact', () => {
  it('should render compact version when locked', () => {
    const { container } = render(
      <PresenceIndicatorCompact lockStatus={createMockLock()} acquisitionStatus="acquired" />
    );

    // Should have green pulsing dot
    const dot = container.querySelector('.bg-green-500');
    expect(dot).toBeInTheDocument();
    expect(dot).toHaveClass('animate-pulse');
  });

  it('should show other user email in compact mode', () => {
    render(
      <PresenceIndicatorCompact
        lockStatus={createMockLock({
          lockedByUserEmail: 'o***@e***.com',
          isCurrentUserLock: false,
        })}
        acquisitionStatus="conflict"
      />
    );

    expect(screen.getByText('o***@e***.com')).toBeInTheDocument();
  });

  it('should not show email when no conflict', () => {
    render(<PresenceIndicatorCompact lockStatus={createMockLock()} acquisitionStatus="acquired" />);

    // Email should not be visible
    expect(screen.queryByText(/\*\*\*@/)).not.toBeInTheDocument();
  });

  it('should have gray dot when idle', () => {
    const { container } = render(
      <PresenceIndicatorCompact lockStatus={null} acquisitionStatus="idle" />
    );

    const dot = container.querySelector('.bg-gray-400');
    expect(dot).toBeInTheDocument();
  });

  it('should have orange dot when conflict', () => {
    const { container } = render(
      <PresenceIndicatorCompact
        lockStatus={createMockLock({ isCurrentUserLock: false })}
        acquisitionStatus="conflict"
      />
    );

    const dot = container.querySelector('.bg-orange-500');
    expect(dot).toBeInTheDocument();
  });

  it('should have tooltip with status information', () => {
    const { container } = render(
      <PresenceIndicatorCompact lockStatus={createMockLock()} acquisitionStatus="acquired" />
    );

    expect(container.firstChild).toHaveAttribute('title', 'Hai il controllo esclusivo');
  });
});
