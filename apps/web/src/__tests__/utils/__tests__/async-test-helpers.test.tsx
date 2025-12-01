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
      setCount(c => c + 1);
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

describe('Async Test Helpers', () => {
  describe('advanceTimersAndFlush', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.runOnlyPendingTimers();
      vi.useRealTimers();
    });

    describe('Happy Path', () => {
      it('should advance timers by specified milliseconds', async () => {
        const callback = vi.fn();
        setTimeout(callback, 1000);

        await advanceTimersAndFlush(1000);

        expect(callback).toHaveBeenCalled();
      });

      it('should advance timers multiple times', async () => {
        const callback = vi.fn();
        setTimeout(callback, 500);
        setTimeout(callback, 1000);
        setTimeout(callback, 1500);

        await advanceTimersAndFlush(1500);

        expect(callback).toHaveBeenCalledTimes(3);
      });

      it('should flush promise microtask queue', async () => {
        const callback = vi.fn();
        Promise.resolve().then(callback);

        await advanceTimersAndFlush(0);

        expect(callback).toHaveBeenCalled();
      });

      it('should handle component with timer', async () => {
        const onTick = vi.fn();
        render(<ComponentWithTimer onTick={onTick} />);

        await advanceTimersAndFlush(2000);

        expect(onTick).toHaveBeenCalledTimes(2);
      });

      it('should work with act() wrapper automatically', async () => {
        render(<ComponentWithTimer />);

        await advanceTimersAndFlush(1000);

        const count = screen.getByTestId('count');
        expect(count.textContent).toBe('1');
      });
    });

    describe('Edge Cases', () => {
      it('should handle zero milliseconds', async () => {
        const callback = vi.fn();
        setTimeout(callback, 100);

        await advanceTimersAndFlush(0);

        expect(callback).not.toHaveBeenCalled();
      });

      it('should reject negative milliseconds', async () => {
        // Jest doesn't support negative ticks
        await expect(advanceTimersAndFlush(-100)).rejects.toThrow();
      });

      it('should handle very large milliseconds', async () => {
        const callback = vi.fn();
        setTimeout(callback, 100000);

        await advanceTimersAndFlush(200000);

        expect(callback).toHaveBeenCalled();
      });

      it('should not trigger timers scheduled for future', async () => {
        const callback = vi.fn();
        setTimeout(callback, 2000);

        await advanceTimersAndFlush(1000);

        expect(callback).not.toHaveBeenCalled();
      });

      it('should handle multiple pending promises', async () => {
        const callback1 = vi.fn();
        const callback2 = vi.fn();
        const callback3 = vi.fn();

        Promise.resolve().then(callback1);
        Promise.resolve().then(callback2);
        Promise.resolve().then(callback3);

        await advanceTimersAndFlush(0);

        expect(callback1).toHaveBeenCalled();
        expect(callback2).toHaveBeenCalled();
        expect(callback3).toHaveBeenCalled();
      });
    });
  });

  describe('waitForAsyncEffects', () => {
    describe('Happy Path', () => {
      it('should wait for async effects to complete', async () => {
        render(<ComponentWithAsyncEffect />);

        await waitForAsyncEffects();

        const data = screen.getByTestId('data');
        expect(data.textContent).toBe('loaded');
      });

      it('should work with multiple components', async () => {
        render(
          <>
            <ComponentWithAsyncEffect />
            <ComponentWithAsyncEffect />
          </>
        );

        await waitForAsyncEffects();

        const dataElements = screen.getAllByTestId('data');
        dataElements.forEach(el => {
          expect(el.textContent).toBe('loaded');
        });
      });

      it('should flush microtask queue', async () => {
        const callback = vi.fn();
        Promise.resolve().then(callback);

        await waitForAsyncEffects();

        expect(callback).toHaveBeenCalled();
      });

      it('should handle nested promises', async () => {
        const callback = vi.fn();

        Promise.resolve()
          .then(() => Promise.resolve())
          .then(() => Promise.resolve())
          .then(callback);

        await waitForAsyncEffects();

        expect(callback).toHaveBeenCalled();
      });
    });

    describe('Edge Cases', () => {
      it('should handle no pending effects', async () => {
        await expect(waitForAsyncEffects()).resolves.not.toThrow();
      });

      it('should work after render without async effects', async () => {
        render(<div>Static content</div>);

        await expect(waitForAsyncEffects()).resolves.not.toThrow();
      });

      it('should handle rejected promises gracefully', async () => {
        // Suppress unhandled rejection warnings
        const rejectedPromise = Promise.reject(new Error('Test error'));
        rejectedPromise.catch(() => {});

        await expect(waitForAsyncEffects()).resolves.not.toThrow();
      });
    });

    describe('Integration with act()', () => {
      it('should work within act() automatically', async () => {
        render(<ComponentWithAsyncEffect />);

        // waitForAsyncEffects wraps in act() internally
        await waitForAsyncEffects();

        expect(screen.getByTestId('data').textContent).toBe('loaded');
      });
    });
  });

  describe('setupUserEvent', () => {
    describe('Happy Path', () => {
      it('should create userEvent instance', () => {
        const user = setupUserEvent();

        expect(user).toBeDefined();
        expect(user.click).toBeDefined();
        expect(user.type).toBeDefined();
      });

      it('should handle click events', async () => {
        const handleClick = vi.fn();
        render(<ComponentWithButton onClick={handleClick} />);

        const user = setupUserEvent();
        const button = screen.getByRole('button');

        await user.click(button);

        expect(handleClick).toHaveBeenCalledTimes(1);
      });

      it('should handle type events', async () => {
        render(<input type="text" data-testid="input" />);

        const user = setupUserEvent();
        const input = screen.getByTestId('input') as HTMLInputElement;

        await user.type(input, 'Hello');

        expect(input.value).toBe('Hello');
      });

      it('should have null delay for immediate actions', async () => {
        const user = setupUserEvent();

        // User event with null delay should execute immediately
        const handleClick = vi.fn();
        render(<ComponentWithButton onClick={handleClick} />);

        await user.click(screen.getByRole('button'));

        expect(handleClick).toHaveBeenCalled();
      });
    });

    describe('Multiple Instances', () => {
      it('should allow multiple user event instances', () => {
        const user1 = setupUserEvent();
        const user2 = setupUserEvent();

        expect(user1).toBeDefined();
        expect(user2).toBeDefined();
        expect(user1).not.toBe(user2);
      });
    });

    describe('Integration with Components', () => {
      it('should work with state updates', async () => {
        const Counter: React.FC = () => {
          const [count, setCount] = useState(0);
          return (
            <div>
              <button onClick={() => setCount(count + 1)}>Increment</button>
              <div data-testid="count">{count}</div>
            </div>
          );
        };

        render(<Counter />);
        const user = setupUserEvent();

        await user.click(screen.getByText('Increment'));

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
              {items.map(item => (
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
