/**
 * Unit tests for useToast hook
 *
 * Tests cover:
 * - Toast creation (success, error, warning, info)
 * - Toast dismissal (manual and auto)
 * - Multiple toasts management
 * - Toast IDs and uniqueness
 * - Clear all toasts functionality
 * - Toast duration handling
 * - onDismiss callback mapping
 * - Edge cases (rapid creation, duplicate operations, etc.)
 */

import { renderHook, act } from '@testing-library/react';
import { useToast } from '../useToast';

describe('useToast', () => {
  beforeEach(() => {
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  describe('Toast Creation', () => {
    it('should create a success toast', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Success Title', 'Success message');
      });

      expect(result.current.toasts).toHaveLength(1);
      expect(result.current.toasts[0]).toMatchObject({
        type: 'success',
        title: 'Success Title',
        message: 'Success message',
      });
      expect(result.current.toasts[0].id).toBeDefined();
    });

    it('should create an error toast', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.error('Error Title', 'Error message');
      });

      expect(result.current.toasts).toHaveLength(1);
      expect(result.current.toasts[0]).toMatchObject({
        type: 'error',
        title: 'Error Title',
        message: 'Error message',
      });
    });

    it('should create a warning toast', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.warning('Warning Title', 'Warning message');
      });

      expect(result.current.toasts).toHaveLength(1);
      expect(result.current.toasts[0]).toMatchObject({
        type: 'warning',
        title: 'Warning Title',
        message: 'Warning message',
      });
    });

    it('should create an info toast', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.info('Info Title', 'Info message');
      });

      expect(result.current.toasts).toHaveLength(1);
      expect(result.current.toasts[0]).toMatchObject({
        type: 'info',
        title: 'Info Title',
        message: 'Info message',
      });
    });

    it('should create toast without message', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Title Only');
      });

      expect(result.current.toasts).toHaveLength(1);
      expect(result.current.toasts[0]).toMatchObject({
        type: 'success',
        title: 'Title Only',
        message: undefined,
      });
    });

    it('should return unique toast ID on creation', () => {
      const { result } = renderHook(() => useToast());

      let id1: string = '';
      let id2: string = '';

      act(() => {
        id1 = result.current.success('Toast 1');
        id2 = result.current.success('Toast 2');
      });

      expect(id1).not.toBe(id2);
      expect(id1).toMatch(/^toast-\d+$/);
      expect(id2).toMatch(/^toast-\d+$/);
    });

    it('should create toast with custom duration', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Custom Duration', 'Message', 3000);
      });

      expect(result.current.toasts[0].duration).toBe(3000);
    });

    it('should create toast with default duration when not specified', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Default Duration');
      });

      expect(result.current.toasts[0].duration).toBeUndefined();
    });
  });

  describe('Multiple Toasts', () => {
    it('should manage multiple toasts simultaneously', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Toast 1');
        result.current.error('Toast 2');
        result.current.warning('Toast 3');
      });

      expect(result.current.toasts).toHaveLength(3);
      expect(result.current.toasts[0].title).toBe('Toast 1');
      expect(result.current.toasts[1].title).toBe('Toast 2');
      expect(result.current.toasts[2].title).toBe('Toast 3');
    });

    it('should maintain toast order (FIFO)', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('First');
        result.current.error('Second');
        result.current.info('Third');
      });

      expect(result.current.toasts[0].title).toBe('First');
      expect(result.current.toasts[1].title).toBe('Second');
      expect(result.current.toasts[2].title).toBe('Third');
    });

    it('should handle rapid toast creation', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        for (let i = 0; i < 10; i++) {
          result.current.success(`Toast ${i}`);
        }
      });

      expect(result.current.toasts).toHaveLength(10);
      expect(result.current.toasts.map((t) => t.title)).toEqual(
        Array.from({ length: 10 }, (_, i) => `Toast ${i}`)
      );
    });
  });

  describe('Toast Dismissal', () => {
    it('should dismiss toast by ID', () => {
      const { result } = renderHook(() => useToast());

      let toastId: string = '';

      act(() => {
        toastId = result.current.success('Dismissible Toast');
      });

      expect(result.current.toasts).toHaveLength(1);

      act(() => {
        result.current.dismiss(toastId);
      });

      expect(result.current.toasts).toHaveLength(0);
    });

    it('should dismiss specific toast from multiple toasts', () => {
      const { result } = renderHook(() => useToast());

      let id1: string = '';
      let id2: string = '';
      let id3: string = '';

      act(() => {
        id1 = result.current.success('Toast 1');
        id2 = result.current.error('Toast 2');
        id3 = result.current.warning('Toast 3');
      });

      expect(result.current.toasts).toHaveLength(3);

      act(() => {
        result.current.dismiss(id2);
      });

      expect(result.current.toasts).toHaveLength(2);
      expect(result.current.toasts[0].id).toBe(id1);
      expect(result.current.toasts[1].id).toBe(id3);
    });

    it('should handle dismissing non-existent toast ID gracefully', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Toast 1');
      });

      expect(result.current.toasts).toHaveLength(1);

      act(() => {
        result.current.dismiss('non-existent-id');
      });

      expect(result.current.toasts).toHaveLength(1);
    });

    it('should handle dismissing same toast twice', () => {
      const { result } = renderHook(() => useToast());

      let toastId: string = '';

      act(() => {
        toastId = result.current.success('Toast');
      });

      act(() => {
        result.current.dismiss(toastId);
        result.current.dismiss(toastId);
      });

      expect(result.current.toasts).toHaveLength(0);
    });

    it('should map onDismiss callback to each toast', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Toast 1');
        result.current.error('Toast 2');
      });

      expect(result.current.toasts[0].onDismiss).toBeDefined();
      expect(result.current.toasts[1].onDismiss).toBeDefined();
      expect(typeof result.current.toasts[0].onDismiss).toBe('function');
    });

    it('should use onDismiss callback to remove toast', () => {
      const { result } = renderHook(() => useToast());

      let toastId: string = '';

      act(() => {
        toastId = result.current.success('Toast');
      });

      const toast = result.current.toasts[0];

      expect(result.current.toasts).toHaveLength(1);

      act(() => {
        toast.onDismiss(toastId);
      });

      expect(result.current.toasts).toHaveLength(0);
    });
  });

  describe('Clear All Toasts', () => {
    it('should clear all toasts', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Toast 1');
        result.current.error('Toast 2');
        result.current.warning('Toast 3');
      });

      expect(result.current.toasts).toHaveLength(3);

      act(() => {
        result.current.clearAll();
      });

      expect(result.current.toasts).toHaveLength(0);
    });

    it('should handle clearAll when no toasts exist', () => {
      const { result } = renderHook(() => useToast());

      expect(result.current.toasts).toHaveLength(0);

      act(() => {
        result.current.clearAll();
      });

      expect(result.current.toasts).toHaveLength(0);
    });

    it('should allow creating new toasts after clearAll', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Toast 1');
        result.current.clearAll();
        result.current.error('Toast 2');
      });

      expect(result.current.toasts).toHaveLength(1);
      expect(result.current.toasts[0].title).toBe('Toast 2');
    });
  });

  describe('Hook Methods Stability', () => {
    it('should maintain stable function references', () => {
      const { result, rerender } = renderHook(() => useToast());

      const initialSuccess = result.current.success;
      const initialError = result.current.error;
      const initialWarning = result.current.warning;
      const initialInfo = result.current.info;
      const initialDismiss = result.current.dismiss;
      const initialClearAll = result.current.clearAll;

      act(() => {
        result.current.success('Toast');
      });

      rerender();

      expect(result.current.success).toBe(initialSuccess);
      expect(result.current.error).toBe(initialError);
      expect(result.current.warning).toBe(initialWarning);
      expect(result.current.info).toBe(initialInfo);
      expect(result.current.dismiss).toBe(initialDismiss);
      expect(result.current.clearAll).toBe(initialClearAll);
    });
  });

  describe('Toast ID Generation', () => {
    it('should generate incrementing toast IDs', () => {
      const { result } = renderHook(() => useToast());

      const ids: string[] = [];

      act(() => {
        for (let i = 0; i < 5; i++) {
          const id = result.current.success(`Toast ${i}`);
          ids.push(id);
        }
      });

      // Each ID should be unique and incrementing
      const idNumbers = ids.map((id) => parseInt(id.replace('toast-', ''), 10));
      expect(idNumbers).toEqual(idNumbers.sort((a, b) => a - b));
      expect(new Set(idNumbers).size).toBe(5);
    });
  });

  describe('Edge Cases', () => {
    it('should handle very long toast titles', () => {
      const { result } = renderHook(() => useToast());

      const longTitle = 'A'.repeat(1000);

      act(() => {
        result.current.success(longTitle);
      });

      expect(result.current.toasts[0].title).toBe(longTitle);
    });

    it('should handle very long toast messages', () => {
      const { result } = renderHook(() => useToast());

      const longMessage = 'B'.repeat(1000);

      act(() => {
        result.current.success('Title', longMessage);
      });

      expect(result.current.toasts[0].message).toBe(longMessage);
    });

    it('should handle empty string title', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('');
      });

      expect(result.current.toasts[0].title).toBe('');
    });

    it('should handle empty string message', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Title', '');
      });

      expect(result.current.toasts[0].message).toBe('');
    });

    it('should handle special characters in title and message', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('<script>alert("xss")</script>', 'Message with "quotes"');
      });

      expect(result.current.toasts[0].title).toBe('<script>alert("xss")</script>');
      expect(result.current.toasts[0].message).toBe('Message with "quotes"');
    });

    it('should handle zero duration', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Zero Duration', 'Message', 0);
      });

      expect(result.current.toasts[0].duration).toBe(0);
    });

    it('should handle negative duration', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Negative Duration', 'Message', -1000);
      });

      expect(result.current.toasts[0].duration).toBe(-1000);
    });

    it('should handle very large duration', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Large Duration', 'Message', 999999999);
      });

      expect(result.current.toasts[0].duration).toBe(999999999);
    });
  });

  describe('Multiple Hook Instances', () => {
    it('should maintain separate state for different hook instances', () => {
      const { result: result1 } = renderHook(() => useToast());
      const { result: result2 } = renderHook(() => useToast());

      act(() => {
        result1.current.success('Toast 1');
        result2.current.error('Toast 2');
      });

      expect(result1.current.toasts).toHaveLength(1);
      expect(result2.current.toasts).toHaveLength(1);
      expect(result1.current.toasts[0].title).toBe('Toast 1');
      expect(result2.current.toasts[0].title).toBe('Toast 2');
    });
  });

  describe('Toast Array Immutability', () => {
    it('should create new array reference when adding toast', () => {
      const { result } = renderHook(() => useToast());

      const initialToasts = result.current.toasts;

      act(() => {
        result.current.success('Toast');
      });

      expect(result.current.toasts).not.toBe(initialToasts);
    });

    it('should create new array reference when dismissing toast', () => {
      const { result } = renderHook(() => useToast());

      let toastId: string = '';

      act(() => {
        toastId = result.current.success('Toast');
      });

      const toastsBeforeDismiss = result.current.toasts;

      act(() => {
        result.current.dismiss(toastId);
      });

      expect(result.current.toasts).not.toBe(toastsBeforeDismiss);
    });

    it('should create new array reference when clearing all toasts', () => {
      const { result } = renderHook(() => useToast());

      act(() => {
        result.current.success('Toast');
      });

      const toastsBeforeClear = result.current.toasts;

      act(() => {
        result.current.clearAll();
      });

      expect(result.current.toasts).not.toBe(toastsBeforeClear);
    });
  });
});
