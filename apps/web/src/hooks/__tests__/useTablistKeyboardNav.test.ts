/**
 * useTablistKeyboardNav — pure hook unit tests (Wave A.6 Tier 1, Issue #622).
 *
 * Validates the WAI-ARIA APG tablist keyboard contract extracted from
 * Wave A.4 (`shared-game-detail/tabs.tsx`) and Wave A.4-followup PR #620
 * (`category-tabs.tsx`). Both call sites had near-identical inline logic;
 * this hook is the consolidated, generically-typed implementation.
 *
 * Contract (mirrors WAI-ARIA APG horizontal tablist):
 *   - ArrowRight → next key (wraps last → first)
 *   - ArrowLeft  → previous key (wraps first → last)
 *   - Home       → first key
 *   - End        → last key
 *   - Other keys (ArrowUp, ArrowDown, char keys) → no-op
 *   - Activation is automatic: focus = onChange same tick
 *   - preventDefault is called for Arrow/Home/End to suppress page scroll
 *
 * Test strategy: pure handler invocation with synthetic KeyboardEvents +
 * manually-populated `tabRefs` Map. No React tree needed — that's exactly
 * the value of extracting this hook.
 */

import { renderHook } from '@testing-library/react';
import type { KeyboardEvent } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { useTablistKeyboardNav } from '../useTablistKeyboardNav';

type Key = 'a' | 'b' | 'c' | 'd';

interface SyntheticEventOpts {
  readonly key: string;
}

function makeEvent({ key }: SyntheticEventOpts): KeyboardEvent<HTMLButtonElement> & {
  readonly preventDefault: ReturnType<typeof vi.fn>;
} {
  const preventDefault = vi.fn();
  return {
    key,
    preventDefault,
  } as unknown as KeyboardEvent<HTMLButtonElement> & {
    readonly preventDefault: ReturnType<typeof vi.fn>;
  };
}

function makeButtonStub(): HTMLButtonElement & { readonly focus: ReturnType<typeof vi.fn> } {
  const focus = vi.fn();
  return { focus } as unknown as HTMLButtonElement & { readonly focus: ReturnType<typeof vi.fn> };
}

interface SetupOpts<T extends string> {
  readonly orderedKeys: ReadonlyArray<T>;
}

function setup<T extends string>({ orderedKeys }: SetupOpts<T>) {
  const onChange = vi.fn();
  const { result } = renderHook(() => useTablistKeyboardNav<T>({ orderedKeys, onChange }));
  // Populate refs with stubbed buttons so focus() can be asserted.
  const buttons = new Map<T, HTMLButtonElement & { readonly focus: ReturnType<typeof vi.fn> }>();
  for (const key of orderedKeys) {
    const btn = makeButtonStub();
    buttons.set(key, btn);
    result.current.tabRefs.current.set(key, btn);
  }
  return { result, onChange, buttons };
}

describe('useTablistKeyboardNav (Wave A.6 Tier 1)', () => {
  describe('return shape', () => {
    it('returns tabRefs as a Map ref and handleKeyDown as a function', () => {
      const { result } = renderHook(() =>
        useTablistKeyboardNav<Key>({ orderedKeys: ['a', 'b'], onChange: vi.fn() })
      );
      expect(result.current.tabRefs.current).toBeInstanceOf(Map);
      expect(typeof result.current.handleKeyDown).toBe('function');
    });

    it('tabRefs persists across re-renders (stable ref identity)', () => {
      const { result, rerender } = renderHook(
        ({ keys }: { keys: ReadonlyArray<Key> }) =>
          useTablistKeyboardNav<Key>({ orderedKeys: keys, onChange: vi.fn() }),
        { initialProps: { keys: ['a', 'b'] as ReadonlyArray<Key> } }
      );
      const firstRef = result.current.tabRefs;
      rerender({ keys: ['a', 'b', 'c'] as ReadonlyArray<Key> });
      expect(result.current.tabRefs).toBe(firstRef);
    });
  });

  describe('ArrowRight', () => {
    it('advances to the next key', () => {
      const { result, onChange, buttons } = setup<Key>({ orderedKeys: ['a', 'b', 'c'] });
      result.current.handleKeyDown(makeEvent({ key: 'ArrowRight' }), 'a');
      expect(onChange).toHaveBeenCalledWith('b');
      expect(buttons.get('b')!.focus).toHaveBeenCalledOnce();
    });

    it('wraps from last key to first', () => {
      const { result, onChange, buttons } = setup<Key>({ orderedKeys: ['a', 'b', 'c'] });
      result.current.handleKeyDown(makeEvent({ key: 'ArrowRight' }), 'c');
      expect(onChange).toHaveBeenCalledWith('a');
      expect(buttons.get('a')!.focus).toHaveBeenCalledOnce();
    });

    it('calls preventDefault to suppress page scroll', () => {
      const { result } = setup<Key>({ orderedKeys: ['a', 'b'] });
      const e = makeEvent({ key: 'ArrowRight' });
      result.current.handleKeyDown(e, 'a');
      expect(e.preventDefault).toHaveBeenCalledOnce();
    });
  });

  describe('ArrowLeft', () => {
    it('retreats to the previous key', () => {
      const { result, onChange, buttons } = setup<Key>({ orderedKeys: ['a', 'b', 'c'] });
      result.current.handleKeyDown(makeEvent({ key: 'ArrowLeft' }), 'b');
      expect(onChange).toHaveBeenCalledWith('a');
      expect(buttons.get('a')!.focus).toHaveBeenCalledOnce();
    });

    it('wraps from first key to last', () => {
      const { result, onChange, buttons } = setup<Key>({ orderedKeys: ['a', 'b', 'c'] });
      result.current.handleKeyDown(makeEvent({ key: 'ArrowLeft' }), 'a');
      expect(onChange).toHaveBeenCalledWith('c');
      expect(buttons.get('c')!.focus).toHaveBeenCalledOnce();
    });

    it('calls preventDefault', () => {
      const { result } = setup<Key>({ orderedKeys: ['a', 'b'] });
      const e = makeEvent({ key: 'ArrowLeft' });
      result.current.handleKeyDown(e, 'b');
      expect(e.preventDefault).toHaveBeenCalledOnce();
    });
  });

  describe('Home', () => {
    it('jumps to the first key', () => {
      const { result, onChange, buttons } = setup<Key>({ orderedKeys: ['a', 'b', 'c', 'd'] });
      result.current.handleKeyDown(makeEvent({ key: 'Home' }), 'c');
      expect(onChange).toHaveBeenCalledWith('a');
      expect(buttons.get('a')!.focus).toHaveBeenCalledOnce();
    });

    it('is a no-op when already on first key (still calls onChange + focuses for idempotency)', () => {
      // Note: WAI-ARIA APG doesn't require short-circuit. Calling onChange with
      // the current key is acceptable and simpler. Same-key case is tolerated.
      const { result, onChange, buttons } = setup<Key>({ orderedKeys: ['a', 'b', 'c'] });
      result.current.handleKeyDown(makeEvent({ key: 'Home' }), 'a');
      expect(onChange).toHaveBeenCalledWith('a');
      expect(buttons.get('a')!.focus).toHaveBeenCalledOnce();
    });

    it('calls preventDefault', () => {
      const { result } = setup<Key>({ orderedKeys: ['a', 'b'] });
      const e = makeEvent({ key: 'Home' });
      result.current.handleKeyDown(e, 'b');
      expect(e.preventDefault).toHaveBeenCalledOnce();
    });
  });

  describe('End', () => {
    it('jumps to the last key', () => {
      const { result, onChange, buttons } = setup<Key>({ orderedKeys: ['a', 'b', 'c', 'd'] });
      result.current.handleKeyDown(makeEvent({ key: 'End' }), 'b');
      expect(onChange).toHaveBeenCalledWith('d');
      expect(buttons.get('d')!.focus).toHaveBeenCalledOnce();
    });

    it('calls preventDefault', () => {
      const { result } = setup<Key>({ orderedKeys: ['a', 'b'] });
      const e = makeEvent({ key: 'End' });
      result.current.handleKeyDown(e, 'a');
      expect(e.preventDefault).toHaveBeenCalledOnce();
    });
  });

  describe('ignored keys (no-op)', () => {
    it.each(['ArrowUp', 'ArrowDown', 'a', 'Enter', ' ', 'Escape', 'Tab'])(
      'does not call onChange or preventDefault for %s',
      keyName => {
        const { result, onChange } = setup<Key>({ orderedKeys: ['a', 'b', 'c'] });
        const e = makeEvent({ key: keyName });
        result.current.handleKeyDown(e, 'a');
        expect(onChange).not.toHaveBeenCalled();
        expect(e.preventDefault).not.toHaveBeenCalled();
      }
    );
  });

  describe('defensive: unknown currentKey', () => {
    it('is a no-op when currentKey is not in orderedKeys', () => {
      const { result, onChange } = setup<Key>({ orderedKeys: ['a', 'b'] });
      const e = makeEvent({ key: 'ArrowRight' });
      // 'd' is not in orderedKeys
      result.current.handleKeyDown(e, 'd');
      expect(onChange).not.toHaveBeenCalled();
      expect(e.preventDefault).not.toHaveBeenCalled();
    });
  });

  describe('defensive: missing ref for next key', () => {
    it('still calls onChange even if focus target is unmounted', () => {
      // Edge case: tab key exists in orderedKeys but its button ref was never
      // registered (e.g., conditionally rendered). onChange still fires; focus
      // is silently skipped (`?.focus()` optional chain).
      const { result, onChange } = setup<Key>({ orderedKeys: ['a', 'b', 'c'] });
      result.current.tabRefs.current.delete('b');
      const e = makeEvent({ key: 'ArrowRight' });
      expect(() => result.current.handleKeyDown(e, 'a')).not.toThrow();
      expect(onChange).toHaveBeenCalledWith('b');
    });
  });

  describe('single-key array (degenerate case)', () => {
    it('ArrowRight wraps onto self', () => {
      const { result, onChange, buttons } = setup<Key>({ orderedKeys: ['a'] });
      result.current.handleKeyDown(makeEvent({ key: 'ArrowRight' }), 'a');
      expect(onChange).toHaveBeenCalledWith('a');
      expect(buttons.get('a')!.focus).toHaveBeenCalledOnce();
    });

    it('ArrowLeft wraps onto self', () => {
      const { result, onChange } = setup<Key>({ orderedKeys: ['a'] });
      result.current.handleKeyDown(makeEvent({ key: 'ArrowLeft' }), 'a');
      expect(onChange).toHaveBeenCalledWith('a');
    });

    it('Home/End → self', () => {
      const { result, onChange } = setup<Key>({ orderedKeys: ['a'] });
      result.current.handleKeyDown(makeEvent({ key: 'Home' }), 'a');
      result.current.handleKeyDown(makeEvent({ key: 'End' }), 'a');
      expect(onChange).toHaveBeenNthCalledWith(1, 'a');
      expect(onChange).toHaveBeenNthCalledWith(2, 'a');
    });
  });

  describe('orderedKeys reactivity', () => {
    it('uses the latest orderedKeys after re-render', () => {
      const onChange = vi.fn();
      const { result, rerender } = renderHook(
        ({ keys }: { keys: ReadonlyArray<Key> }) =>
          useTablistKeyboardNav<Key>({ orderedKeys: keys, onChange }),
        { initialProps: { keys: ['a', 'b'] as ReadonlyArray<Key> } }
      );

      // Initially 2 keys: ArrowRight from 'b' wraps to 'a'.
      result.current.handleKeyDown(makeEvent({ key: 'ArrowRight' }), 'b');
      expect(onChange).toHaveBeenLastCalledWith('a');

      // Add a third key. ArrowRight from 'b' should now go to 'c', not wrap.
      rerender({ keys: ['a', 'b', 'c'] as ReadonlyArray<Key> });
      result.current.handleKeyDown(makeEvent({ key: 'ArrowRight' }), 'b');
      expect(onChange).toHaveBeenLastCalledWith('c');
    });
  });

  describe('generic key type', () => {
    it('works with arbitrary string union', () => {
      type CustomKey = 'overview' | 'details' | 'history';
      const onChange = vi.fn();
      const { result } = renderHook(() =>
        useTablistKeyboardNav<CustomKey>({
          orderedKeys: ['overview', 'details', 'history'],
          onChange,
        })
      );
      result.current.handleKeyDown(makeEvent({ key: 'End' }), 'overview');
      expect(onChange).toHaveBeenCalledWith('history');
    });
  });
});
