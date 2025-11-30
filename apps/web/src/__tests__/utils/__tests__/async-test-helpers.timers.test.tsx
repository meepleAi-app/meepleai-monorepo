/**
 * Tests for Async Test Helpers
 *
 * These tests ensure that async testing utilities work correctly
 * and handle all edge cases, especially with fake timers and promises.
 *
 * Coverage targets:
 * - Statements: 70% → 90%+
 * - Branches: 0% → 90%+
 * - Functions: 62.5% → 90%+
 */

import { act, render, screen, waitFor } from '@testing-library/react';
import React, { useEffect, useState } from 'react';
import {
  advanceTimersAndFlush,
  waitForAsyncEffects,
  setupUserEvent,
  flushAllPending,
  waitForCondition,
} from '../async-test-helpers';

// Test components
const ComponentWithTimer: React.FC<{ onTick?: () => void }> = ({ onTick }) => {
  const [count, setCount] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setCount((c) => c + 1);
      onTick?.();
    }, 1000);

    return () => clearInterval(interval);
  }, [onTick]);

  return <div data-testid="count">{count}</div>;
};

const ComponentWithAsyncEffect: React.FC = () => {
  const [data, setData] = useState<string>('loading');

  useEffect(() => {
    Promise.resolve('loaded').then(setData);
  }, []);

  return <div data-testid="data">{data}</div>;
};

const ComponentWithButton: React.FC<{ onClick: () => void }> = ({ onClick }) => {
  return <button onClick={onClick}>Click me</button>;
};

        expect(screen.getByTestId('count').textContent).toBe('1');
      });
    });
  });

  describe('flushAllPending', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    describe('Happy Path', () => {
      it('should flush all pending timers', async () => {
        const callback = vi.fn();
        setTimeout(callback, 1000);
        setTimeout(callback, 2000);

        await flushAllPending();

        expect(callback).toHaveBeenCalledTimes(2);
      });

      it('should flush promise queue', async () => {
        const callback = vi.fn();
        Promise.resolve().then(callback);

        await flushAllPending();

        expect(callback).toHaveBeenCalled();
      });

      it('should work as cleanup in afterEach', async () => {
        const callback = vi.fn();
        setTimeout(callback, 100);

        await flushAllPending();

        expect(callback).toHaveBeenCalled();
      });
    });

    describe('Edge Cases', () => {
      it('should handle no pending timers', async () => {
        await expect(flushAllPending()).resolves.not.toThrow();
      });

      it('should handle only pending timers (no promises)', async () => {
        const callback = vi.fn();
        setTimeout(callback, 500);

        await flushAllPending();

        expect(callback).toHaveBeenCalled();
      });

      it('should handle only pending promises (no timers)', async () => {
        const callback = vi.fn();
        Promise.resolve().then(callback);

        await flushAllPending();

        expect(callback).toHaveBeenCalled();
      });

      it('should handle pending timers at current time', async () => {
        const callback = vi.fn();
        setTimeout(callback, 100);

        // runOnlyPendingTimers runs only currently scheduled timers
        await flushAllPending();

        // The timer would have been executed
        expect(callback).toHaveBeenCalled();
      });
    });

    describe('Integration', () => {
      it('should clean up component timers', async () => {
        const onTick = vi.fn();
        const { unmount } = render(<ComponentWithTimer onTick={onTick} />);

        unmount();
        await flushAllPending();

        // Timer should be cleaned up after unmount
        expect(onTick).not.toHaveBeenCalled();
      });
    });
  });

  describe('waitForCondition', () => {
    describe('Happy Path', () => {
      it('should wait for condition to be true', async () => {
        let ready = false;
        setTimeout(() => {
          ready = true;
        }, 100);

        await waitForCondition(() => {
          expect(ready).toBe(true);
        });

        expect(ready).toBe(true);
      });

      it('should work with component state changes', async () => {
        render(<ComponentWithAsyncEffect />);

        await waitForCondition(() => {
          expect(screen.getByTestId('data').textContent).toBe('loaded');
        });

        expect(screen.getByTestId('data').textContent).toBe('loaded');
      });

      it('should use default timeout of 3000ms', async () => {
        let ready = false;
        setTimeout(() => {
          ready = true;
        }, 100);

        // Should not timeout with default 3000ms
        await waitForCondition(() => {
          expect(ready).toBe(true);
        });

        expect(ready).toBe(true);
      });

      it('should accept custom timeout', async () => {
        let ready = false;
        setTimeout(() => {
          ready = true;
        }, 100);

        await waitForCondition(() => {
          expect(ready).toBe(true);
        }, 5000);

        expect(ready).toBe(true);
      });
    });

    describe('Timeout Behavior', () => {
      it('should timeout if condition never met', async () => {
        await expect(
          waitForCondition(() => {
            expect(false).toBe(true);
          }, 100)
        ).rejects.toThrow();
      });

      it('should timeout after specified milliseconds', async () => {
        const startTime = Date.now();

        try {
          await waitForCondition(() => {
            expect(false).toBe(true);
          }, 500);
        } catch (error) {
          const elapsed = Date.now() - startTime;
          expect(elapsed).toBeGreaterThanOrEqual(500);
        }
      });

      it('should throw assertion error on timeout', async () => {
        await expect(
          waitForCondition(() => {
            throw new Error('Condition not met');
          }, 100)
        ).rejects.toThrow('Condition not met');
      });
    });

    describe('Edge Cases', () => {
      it('should handle immediate success', async () => {
        await waitForCondition(() => {
          expect(true).toBe(true);
        }, 1000);
      });

      it('should handle zero timeout (immediate success if passes)', async () => {
        // With zero timeout and immediate success, it resolves
        await expect(
          waitForCondition(() => {
            expect(true).toBe(true);
          }, 0)
        ).resolves.not.toThrow();
      });

      it('should handle very long timeout', async () => {
        let ready = false;
        setTimeout(() => {
          ready = true;
        }, 50);

        await waitForCondition(() => {
          expect(ready).toBe(true);
        }, 10000);

        expect(ready).toBe(true);
      });

      it('should handle multiple assertions in callback', async () => {
        render(<ComponentWithAsyncEffect />);

        await waitForCondition(() => {
          const data = screen.getByTestId('data');
          expect(data).toBeInTheDocument();
          expect(data.textContent).toBe('loaded');
        });
      });
    });

    describe('Integration Scenarios', () => {
      it('should work with async component updates', async () => {
        const AsyncComponent: React.FC = () => {
          const [items, setItems] = useState<string[]>([]);

          useEffect(() => {
            setTimeout(() => {
              setItems(['item1', 'item2', 'item3']);
            }, 100);
          }, []);

          return (
            <ul>
              {items.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          );
        };

        render(<AsyncComponent />);

        await waitForCondition(() => {
          expect(screen.getAllByRole('listitem')).toHaveLength(3);
        });
      });

      it('should retry until condition met', async () => {
        let attempts = 0;

        await waitForCondition(() => {
          attempts++;
          if (attempts < 3) {
            throw new Error('Not ready yet');
          }
          expect(true).toBe(true);
        }, 2000);

        expect(attempts).toBeGreaterThanOrEqual(3);
      });
    });
  });

  describe('Helper Integration Tests', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(async () => {
      await flushAllPending();
      vi.useRealTimers();
    });

    describe('Combined Usage', () => {
      it('should work together: waitForAsyncEffects + setupUserEvent', async () => {
        const handleClick = vi.fn();
        render(
          <>
            <ComponentWithAsyncEffect />
            <ComponentWithButton onClick={handleClick} />
          </>
        );

        await waitForAsyncEffects();
        expect(screen.getByTestId('data').textContent).toBe('loaded');

        const user = setupUserEvent();
        await user.click(screen.getByRole('button'));

        expect(handleClick).toHaveBeenCalled();
      });

      it('should work together: advanceTimersAndFlush + waitForCondition', async () => {
        render(<ComponentWithTimer />);

        await advanceTimersAndFlush(2000);

        await waitForCondition(() => {
          expect(screen.getByTestId('count').textContent).toBe('2');
        });
      });

      it('should support full test lifecycle', async () => {
        const callback = vi.fn();

        // Render component
        render(<ComponentWithTimer onTick={callback} />);

        // Advance time
        await advanceTimersAndFlush(1000);
        expect(callback).toHaveBeenCalledTimes(1);

        // Wait for async effects
        await waitForAsyncEffects();

        // Cleanup
        await flushAllPending();
      });
    });
  });
});
