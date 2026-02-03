/**
 * useRateLimitNotifications Hook Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: Rate limit status monitoring and toast notifications
 * - Cooldown end notification
 * - Monthly usage 80% threshold notification
 * - Pending requests 80% threshold notification
 */

import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useRateLimitNotifications } from '../useRateLimitNotifications';

// Mock sonner toast
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    warning: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  },
}));

// Mock the useRateLimitStatus hook
vi.mock('../queries/useShareRequests', () => ({
  useRateLimitStatus: vi.fn(),
}));

import { toast } from 'sonner';
import { useRateLimitStatus } from '../queries/useShareRequests';

const mockToastSuccess = vi.mocked(toast.success);
const mockToastWarning = vi.mocked(toast.warning);
const mockUseRateLimitStatus = vi.mocked(useRateLimitStatus);

interface RateLimitStatus {
  isInCooldown: boolean;
  currentMonthlyCount: number;
  maxMonthlyAllowed: number;
  currentPendingCount: number;
  maxPendingAllowed: number;
}

describe('useRateLimitNotifications - Issue #3026', () => {
  const createStatus = (overrides: Partial<RateLimitStatus> = {}): RateLimitStatus => ({
    isInCooldown: false,
    currentMonthlyCount: 0,
    maxMonthlyAllowed: 100,
    currentPendingCount: 0,
    maxPendingAllowed: 10,
    ...overrides,
  });

  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('Initial State', () => {
    it('should not show notifications on initial render', () => {
      mockUseRateLimitStatus.mockReturnValue({ data: createStatus() } as any);

      renderHook(() => useRateLimitNotifications());

      expect(mockToastSuccess).not.toHaveBeenCalled();
      expect(mockToastWarning).not.toHaveBeenCalled();
    });

    it('should handle undefined status gracefully', () => {
      mockUseRateLimitStatus.mockReturnValue({ data: undefined } as any);

      expect(() => renderHook(() => useRateLimitNotifications())).not.toThrow();
      expect(mockToastSuccess).not.toHaveBeenCalled();
      expect(mockToastWarning).not.toHaveBeenCalled();
    });
  });

  describe('Cooldown End Notification', () => {
    it('should show success toast when cooldown ends', () => {
      const initialStatus = createStatus({ isInCooldown: true });
      const updatedStatus = createStatus({ isInCooldown: false });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      // Simulate status change - cooldown ended
      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastSuccess).toHaveBeenCalledWith(
        'You can now submit share requests again!'
      );
    });

    it('should not show notification if cooldown remains active', () => {
      const initialStatus = createStatus({ isInCooldown: true });
      const updatedStatus = createStatus({ isInCooldown: true });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastSuccess).not.toHaveBeenCalled();
    });

    it('should not show notification if not previously in cooldown', () => {
      const initialStatus = createStatus({ isInCooldown: false });
      const updatedStatus = createStatus({ isInCooldown: false });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastSuccess).not.toHaveBeenCalled();
    });
  });

  describe('Monthly Usage 80% Threshold Notification', () => {
    it('should show warning toast when monthly usage reaches 80%', () => {
      const initialStatus = createStatus({
        currentMonthlyCount: 75,
        maxMonthlyAllowed: 100,
      });
      const updatedStatus = createStatus({
        currentMonthlyCount: 80,
        maxMonthlyAllowed: 100,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastWarning).toHaveBeenCalledWith(
        "You've used 80% of your monthly share requests. 20 remaining."
      );
    });

    it('should show warning toast when exceeding 80%', () => {
      const initialStatus = createStatus({
        currentMonthlyCount: 70,
        maxMonthlyAllowed: 100,
      });
      const updatedStatus = createStatus({
        currentMonthlyCount: 85,
        maxMonthlyAllowed: 100,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastWarning).toHaveBeenCalledWith(
        "You've used 80% of your monthly share requests. 15 remaining."
      );
    });

    it('should not show notification if already above 80%', () => {
      const initialStatus = createStatus({
        currentMonthlyCount: 85,
        maxMonthlyAllowed: 100,
      });
      const updatedStatus = createStatus({
        currentMonthlyCount: 90,
        maxMonthlyAllowed: 100,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastWarning).not.toHaveBeenCalled();
    });

    it('should not show notification if below 80%', () => {
      const initialStatus = createStatus({
        currentMonthlyCount: 50,
        maxMonthlyAllowed: 100,
      });
      const updatedStatus = createStatus({
        currentMonthlyCount: 60,
        maxMonthlyAllowed: 100,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastWarning).not.toHaveBeenCalled();
    });

    it('should handle zero max monthly allowed', () => {
      const initialStatus = createStatus({
        currentMonthlyCount: 0,
        maxMonthlyAllowed: 0,
      });
      const updatedStatus = createStatus({
        currentMonthlyCount: 0,
        maxMonthlyAllowed: 0,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastWarning).not.toHaveBeenCalled();
    });
  });

  describe('Pending Requests 80% Threshold Notification', () => {
    it('should show warning toast when pending requests reach 80%', () => {
      const initialStatus = createStatus({
        currentPendingCount: 7,
        maxPendingAllowed: 10,
      });
      const updatedStatus = createStatus({
        currentPendingCount: 8,
        maxPendingAllowed: 10,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastWarning).toHaveBeenCalledWith(
        "You're approaching your pending request limit. 2 remaining."
      );
    });

    it('should show warning toast when exceeding 80% pending', () => {
      const initialStatus = createStatus({
        currentPendingCount: 5,
        maxPendingAllowed: 10,
      });
      const updatedStatus = createStatus({
        currentPendingCount: 9,
        maxPendingAllowed: 10,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastWarning).toHaveBeenCalledWith(
        "You're approaching your pending request limit. 1 remaining."
      );
    });

    it('should not show notification if already above 80% pending', () => {
      const initialStatus = createStatus({
        currentPendingCount: 9,
        maxPendingAllowed: 10,
      });
      const updatedStatus = createStatus({
        currentPendingCount: 10,
        maxPendingAllowed: 10,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastWarning).not.toHaveBeenCalled();
    });

    it('should handle zero max pending allowed', () => {
      const initialStatus = createStatus({
        currentPendingCount: 0,
        maxPendingAllowed: 0,
      });
      const updatedStatus = createStatus({
        currentPendingCount: 0,
        maxPendingAllowed: 0,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastWarning).not.toHaveBeenCalled();
    });
  });

  describe('Multiple Thresholds', () => {
    it('should show both monthly and pending warnings when both hit 80%', () => {
      const initialStatus = createStatus({
        currentMonthlyCount: 75,
        maxMonthlyAllowed: 100,
        currentPendingCount: 7,
        maxPendingAllowed: 10,
      });
      const updatedStatus = createStatus({
        currentMonthlyCount: 85,
        maxMonthlyAllowed: 100,
        currentPendingCount: 9,
        maxPendingAllowed: 10,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastWarning).toHaveBeenCalledTimes(2);
      expect(mockToastWarning).toHaveBeenCalledWith(
        "You've used 80% of your monthly share requests. 15 remaining."
      );
      expect(mockToastWarning).toHaveBeenCalledWith(
        "You're approaching your pending request limit. 1 remaining."
      );
    });

    it('should show cooldown end and threshold warnings together', () => {
      const initialStatus = createStatus({
        isInCooldown: true,
        currentMonthlyCount: 75,
        maxMonthlyAllowed: 100,
      });
      const updatedStatus = createStatus({
        isInCooldown: false,
        currentMonthlyCount: 85,
        maxMonthlyAllowed: 100,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastSuccess).toHaveBeenCalledWith(
        'You can now submit share requests again!'
      );
      expect(mockToastWarning).toHaveBeenCalledWith(
        "You've used 80% of your monthly share requests. 15 remaining."
      );
    });
  });

  describe('Status Updates', () => {
    it('should update previous status reference after each change', () => {
      const status1 = createStatus({ currentMonthlyCount: 50 });
      const status2 = createStatus({ currentMonthlyCount: 70 });
      const status3 = createStatus({ currentMonthlyCount: 85 });

      mockUseRateLimitStatus.mockReturnValue({ data: status1 } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      // First update - below 80%
      mockUseRateLimitStatus.mockReturnValue({ data: status2 } as any);
      rerender();
      expect(mockToastWarning).not.toHaveBeenCalled();

      // Second update - crosses 80% threshold
      mockUseRateLimitStatus.mockReturnValue({ data: status3 } as any);
      rerender();
      expect(mockToastWarning).toHaveBeenCalledTimes(1);
    });
  });

  describe('Edge Cases', () => {
    it('should handle exactly 80% monthly usage', () => {
      const initialStatus = createStatus({
        currentMonthlyCount: 79,
        maxMonthlyAllowed: 100,
      });
      const updatedStatus = createStatus({
        currentMonthlyCount: 80,
        maxMonthlyAllowed: 100,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastWarning).toHaveBeenCalledWith(
        "You've used 80% of your monthly share requests. 20 remaining."
      );
    });

    it('should handle exactly 80% pending usage', () => {
      const initialStatus = createStatus({
        currentPendingCount: 7,
        maxPendingAllowed: 10,
      });
      const updatedStatus = createStatus({
        currentPendingCount: 8,
        maxPendingAllowed: 10,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastWarning).toHaveBeenCalledWith(
        "You're approaching your pending request limit. 2 remaining."
      );
    });

    it('should handle small max values (e.g., max = 5)', () => {
      const initialStatus = createStatus({
        currentPendingCount: 3,
        maxPendingAllowed: 5,
      });
      const updatedStatus = createStatus({
        currentPendingCount: 4,
        maxPendingAllowed: 5,
      });

      mockUseRateLimitStatus.mockReturnValue({ data: initialStatus } as any);

      const { rerender } = renderHook(() => useRateLimitNotifications());

      mockUseRateLimitStatus.mockReturnValue({ data: updatedStatus } as any);
      rerender();

      expect(mockToastWarning).toHaveBeenCalledWith(
        "You're approaching your pending request limit. 1 remaining."
      );
    });
  });
});
