/**
 * ReviewLockTimer Component Tests
 *
 * Test Coverage:
 * - Timer countdown accuracy
 * - Expiring soon warning (< 5 min)
 * - onExpired callback
 * - Visual state transitions
 *
 * Issue #2748: Frontend - Admin Review Lock UI
 * Target: ≥85% coverage
 */

import { render, screen, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ReviewLockTimer } from '../ReviewLockTimer';

describe('ReviewLockTimer', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('renders countdown with correct time remaining', () => {
    const expiresAt = new Date(Date.now() + 10 * 60 * 1000).toISOString(); // 10 minutes from now
    render(<ReviewLockTimer expiresAt={expiresAt} />);

    expect(screen.getByText(/10:00 remaining/)).toBeInTheDocument();
  });

  it('updates countdown every second', async () => {
    const expiresAt = new Date(Date.now() + 2 * 60 * 1000).toISOString(); // 2 minutes from now
    render(<ReviewLockTimer expiresAt={expiresAt} />);

    expect(screen.getByText(/2:00 remaining/)).toBeInTheDocument();

    // Advance time by 1 second with act() to trigger state update
    await act(async () => {
      vi.advanceTimersByTime(1000);
    });

    expect(screen.getByText(/1:59 remaining/)).toBeInTheDocument();
  });

  it('shows expiring soon warning when < 5 minutes remaining', () => {
    const expiresAt = new Date(Date.now() + 4 * 60 * 1000).toISOString(); // 4 minutes from now
    render(<ReviewLockTimer expiresAt={expiresAt} />);

    expect(screen.getByText('Expiring soon')).toBeInTheDocument();
  });

  it('does not show expiring soon warning when > 5 minutes remaining', () => {
    const expiresAt = new Date(Date.now() + 6 * 60 * 1000).toISOString(); // 6 minutes from now
    render(<ReviewLockTimer expiresAt={expiresAt} />);

    expect(screen.queryByText('Expiring soon')).not.toBeInTheDocument();
  });

  it('calls onExpired callback when timer reaches 0', async () => {
    const onExpired = vi.fn();
    const expiresAt = new Date(Date.now() + 2000).toISOString(); // 2 seconds from now

    render(<ReviewLockTimer expiresAt={expiresAt} onExpired={onExpired} />);

    // Fast-forward past expiration with act() to trigger state updates
    await act(async () => {
      vi.advanceTimersByTime(3000);
    });

    // onExpired may be called multiple times due to interval - verify at least once
    expect(onExpired).toHaveBeenCalled();
  });

  it('displays 0:00 when expired', () => {
    const expiresAt = new Date(Date.now() - 1000).toISOString(); // 1 second ago
    render(<ReviewLockTimer expiresAt={expiresAt} />);

    expect(screen.getByText(/0:00 remaining/)).toBeInTheDocument();
  });

  it('pads seconds with leading zero', () => {
    const expiresAt = new Date(Date.now() + 5 * 1000).toISOString(); // 5 seconds from now
    render(<ReviewLockTimer expiresAt={expiresAt} />);

    expect(screen.getByText(/0:05 remaining/)).toBeInTheDocument();
  });

  it('cleans up interval on unmount', () => {
    const clearIntervalSpy = vi.spyOn(global, 'clearInterval');
    const expiresAt = new Date(Date.now() + 10 * 60 * 1000).toISOString();

    const { unmount } = render(<ReviewLockTimer expiresAt={expiresAt} />);
    unmount();

    expect(clearIntervalSpy).toHaveBeenCalled();
  });
});
