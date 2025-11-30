/**
 * Unit tests for timer test helpers
 * Tests fake timer utilities and lifecycle management
 */

import { waitFor } from '@testing-library/react';
import {
  advanceTimersAndWaitFor,
  setupFakeTimers,
  advanceTimers,
  runAllPendingTimers,
} from '../timer-test-helpers';

describe('Timer Test Helpers', () => {
  describe('advanceTimersAndWaitFor', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.runOnlyPendingTimers();
      vi.useRealTimers();
    });

    it('should advance timers and wait for assertion', async () => {
      let executed = false;

      setTimeout(() => {
        executed = true;
      }, 1000);

      await advanceTimersAndWaitFor(1000, () => {
        expect(executed).toBe(true);
      });
    });

    it('should work with callbacks executed at exact time', async () => {
      let counter = 0;

      setTimeout(() => {
        counter++;
      }, 500);

      setTimeout(() => {
        counter++;
      }, 1000);

      await advanceTimersAndWaitFor(500, () => {
        expect(counter).toBe(1);
      });

      await advanceTimersAndWaitFor(500, () => {
        expect(counter).toBe(2);
      });
    });

    it('should handle multiple timer advances', async () => {
      const events: string[] = [];

      setTimeout(() => events.push('1s'), 1000);
      setTimeout(() => events.push('2s'), 2000);
      setTimeout(() => events.push('3s'), 3000);

      await advanceTimersAndWaitFor(1000, () => {
        expect(events).toEqual(['1s']);
      });

      await advanceTimersAndWaitFor(1000, () => {
        expect(events).toEqual(['1s', '2s']);
      });

      await advanceTimersAndWaitFor(1000, () => {
        expect(events).toEqual(['1s', '2s', '3s']);
      });
    });

    it('should work with async assertions', async () => {
      let resolved = false;

      setTimeout(() => {
        resolved = true;
      }, 500);

      await advanceTimersAndWaitFor(500, async () => {
        await Promise.resolve();
        expect(resolved).toBe(true);
      });
    });

    it('should not execute timers inside waitFor callback', async () => {
      let executionCount = 0;

      setTimeout(() => {
        executionCount++;
      }, 100);

      // This pattern is correct: advance BEFORE waitFor
      await advanceTimersAndWaitFor(100, () => {
        expect(executionCount).toBe(1);
      });

      // Verify it only executed once
      expect(executionCount).toBe(1);
    });

    it('should handle zero millisecond timers', async () => {
      let executed = false;

      setTimeout(() => {
        executed = true;
      }, 0);

      await advanceTimersAndWaitFor(0, () => {
        expect(executed).toBe(true);
      });
    });

    it('should work with setInterval', async () => {
      let count = 0;

      const intervalId = setInterval(() => {
        count++;
      }, 100);

      await advanceTimersAndWaitFor(300, () => {
        expect(count).toBe(3);
      });

      clearInterval(intervalId);
    });
  });

  describe('setupFakeTimers', () => {
    // Note: We can't test setupFakeTimers directly as it sets up hooks
    // Instead, we test its behavior pattern manually

    it('should enable fake timers in beforeEach', () => {
      vi.useFakeTimers();

      const callback = vi.fn();
      setTimeout(callback, 1000);

      expect(callback).not.toHaveBeenCalled();

      vi.advanceTimersByTime(1000);
      expect(callback).toHaveBeenCalledTimes(1);

      vi.useRealTimers();
    });

    it('should run pending timers in afterEach', () => {
      vi.useFakeTimers();

      const callback = vi.fn();
      setTimeout(callback, 1000);

      // Simulate afterEach behavior
      vi.runOnlyPendingTimers();

      expect(callback).toHaveBeenCalledTimes(1);

      vi.useRealTimers();
    });

    it('should restore real timers in afterEach', () => {
      vi.useFakeTimers();

      // Simulate afterEach behavior
      vi.runOnlyPendingTimers();
      vi.useRealTimers();

      // Verify real timers work
      const start = Date.now();
      const callback = vi.fn();

      setTimeout(callback, 10);

      // With real timers, Date.now() should actually advance
      return new Promise<void>((resolve) => {
        setTimeout(() => {
          expect(callback).toHaveBeenCalledTimes(1);
          expect(Date.now()).toBeGreaterThan(start);
          resolve();
        }, 20);
      });
    });
  });

  describe('advanceTimers', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.runOnlyPendingTimers();
      vi.useRealTimers();
    });

    it('should advance timers by specified milliseconds', () => {
      const callback = vi.fn();

      setTimeout(callback, 1000);

      advanceTimers(1000);

      expect(callback).toHaveBeenCalledTimes(1);
    });

    it('should work with multiple callbacks', () => {
      const callback1 = vi.fn();
      const callback2 = vi.fn();

      setTimeout(callback1, 500);
      setTimeout(callback2, 1000);

      advanceTimers(500);
      expect(callback1).toHaveBeenCalledTimes(1);
      expect(callback2).not.toHaveBeenCalled();

      advanceTimers(500);
      expect(callback2).toHaveBeenCalledTimes(1);
    });

    it('should handle advancing past multiple timers at once', () => {
      const callback1 = vi.fn();
      const callback2 = vi.fn();
      const callback3 = vi.fn();

      setTimeout(callback1, 100);
      setTimeout(callback2, 200);
      setTimeout(callback3, 300);

      advanceTimers(500);

      expect(callback1).toHaveBeenCalledTimes(1);
      expect(callback2).toHaveBeenCalledTimes(1);
      expect(callback3).toHaveBeenCalledTimes(1);
    });

    it('should be a simple wrapper around jest.advanceTimersByTime', () => {
      const callback = vi.fn();

      setTimeout(callback, 1000);

      // Both should work identically
      advanceTimers(500);
      vi.advanceTimersByTime(500);

      expect(callback).toHaveBeenCalledTimes(1);
    });
  });

  describe('runAllPendingTimers', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.runOnlyPendingTimers();
      vi.useRealTimers();
    });

    it('should execute all pending timers', () => {
      const callback1 = vi.fn();
      const callback2 = vi.fn();
      const callback3 = vi.fn();

      setTimeout(callback1, 100);
      setTimeout(callback2, 1000);
      setTimeout(callback3, 5000);

      runAllPendingTimers();

      expect(callback1).toHaveBeenCalledTimes(1);
      expect(callback2).toHaveBeenCalledTimes(1);
      expect(callback3).toHaveBeenCalledTimes(1);
    });

    it('should work with setInterval', () => {
      const callback = vi.fn();

      const intervalId = setInterval(callback, 100);

      // Note: vi.runAllTimers() with intervals can run indefinitely
      // so we clear it after a reasonable count
      setTimeout(() => clearInterval(intervalId), 350);

      runAllPendingTimers();

      // Should execute interval 3 times before clearInterval
      expect(callback).toHaveBeenCalledTimes(3);
    });

    it('should handle nested timers', () => {
      const execution: number[] = [];

      setTimeout(() => {
        execution.push(1);
        setTimeout(() => {
          execution.push(2);
        }, 100);
      }, 100);

      runAllPendingTimers();

      expect(execution).toEqual([1, 2]);
    });

    it('should advance fake Date when running timers', () => {
      const before = Date.now();

      setTimeout(() => {
        // Timer callback
      }, 1000);

      runAllPendingTimers();

      const after = Date.now();

      // Date.now() should advance by at least the timer duration (1000ms)
      // since vi.runAllTimers() advances the fake timer clock
      expect(after - before).toBeGreaterThanOrEqual(1000);
    });
  });

  describe('Anti-Pattern Prevention', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.runOnlyPendingTimers();
      vi.useRealTimers();
    });

    it('should demonstrate correct pattern: advance THEN wait', async () => {
      let executed = false;

      setTimeout(() => {
        executed = true;
      }, 1000);

      // ✅ CORRECT: Advance timers first
      vi.advanceTimersByTime(1000);

      // THEN wait for assertion
      await waitFor(() => {
        expect(executed).toBe(true);
      });
    });

    it('should show why advancing inside waitFor is wrong', async () => {
      let executed = false;

      setTimeout(() => {
        executed = true;
      }, 1000);

      // ❌ ANTI-PATTERN (commented out to prevent actual failure):
      // await waitFor(() => {
      //   vi.advanceTimersByTime(1000); // This won't work!
      //   expect(executed).toBe(true);
      // });

      // This test just documents the anti-pattern
      expect(executed).toBe(false); // Timer hasn't fired yet
    });

    it('should demonstrate advanceTimersAndWaitFor prevents anti-pattern', async () => {
      let executed = false;

      setTimeout(() => {
        executed = true;
      }, 500);

      // ✅ CORRECT: Helper ensures proper ordering
      await advanceTimersAndWaitFor(500, () => {
        expect(executed).toBe(true);
      });
    });
  });

  describe('Edge Cases', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.runOnlyPendingTimers();
      vi.useRealTimers();
    });

    it('should handle timers with same delay', () => {
      const callback1 = vi.fn();
      const callback2 = vi.fn();
      const callback3 = vi.fn();

      setTimeout(callback1, 100);
      setTimeout(callback2, 100);
      setTimeout(callback3, 100);

      advanceTimers(100);

      expect(callback1).toHaveBeenCalledTimes(1);
      expect(callback2).toHaveBeenCalledTimes(1);
      expect(callback3).toHaveBeenCalledTimes(1);
    });

    it('should handle clearTimeout', () => {
      const callback = vi.fn();

      const timeoutId = setTimeout(callback, 1000);
      clearTimeout(timeoutId);

      advanceTimers(1000);

      expect(callback).not.toHaveBeenCalled();
    });

    it('should handle clearInterval', () => {
      const callback = vi.fn();

      const intervalId = setInterval(callback, 100);

      advanceTimers(100);
      expect(callback).toHaveBeenCalledTimes(1);

      clearInterval(intervalId);

      advanceTimers(100);
      expect(callback).toHaveBeenCalledTimes(1); // Should not increase
    });

    it('should work with Promise.resolve timers', async () => {
      let resolved = false;

      Promise.resolve().then(() => {
        setTimeout(() => {
          resolved = true;
        }, 100);
      });

      // Need to let Promise resolve first
      await Promise.resolve();

      advanceTimers(100);

      expect(resolved).toBe(true);
    });
  });
});
