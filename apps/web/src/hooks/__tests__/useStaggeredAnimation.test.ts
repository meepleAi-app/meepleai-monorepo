/**
 * useStaggeredAnimation Hook Tests
 * Issue #3292 - Phase 6: Breadcrumb & Polish
 */

import { describe, it, expect } from 'vitest';
import { renderHook } from '@testing-library/react';

import {
  useStaggeredAnimation,
  calculateStaggerDelays,
  STAGGER_CONFIGS,
} from '../useStaggeredAnimation';

describe('useStaggeredAnimation', () => {
  it('should return correct animation delay for first item', () => {
    const { result } = renderHook(() => useStaggeredAnimation(0));

    expect(result.current.animationDelay).toBe('0ms');
    expect(result.current.animationFillMode).toBe('backwards');
  });

  it('should return correct animation delay for subsequent items', () => {
    const { result: result1 } = renderHook(() => useStaggeredAnimation(1));
    const { result: result2 } = renderHook(() => useStaggeredAnimation(2));

    // Default stagger is 50ms
    expect(result1.current.animationDelay).toBe('50ms');
    expect(result2.current.animationDelay).toBe('100ms');
  });

  it('should use custom delay', () => {
    const { result } = renderHook(() =>
      useStaggeredAnimation(1, { delay: 100 })
    );

    expect(result.current.animationDelay).toBe('100ms');
  });

  it('should respect maxDelay', () => {
    const { result } = renderHook(() =>
      useStaggeredAnimation(100, { delay: 50, maxDelay: 200 })
    );

    expect(result.current.animationDelay).toBe('200ms');
  });

  it('should support reverse direction', () => {
    const { result } = renderHook(() =>
      useStaggeredAnimation(2, { direction: 'reverse', startIndex: 5 })
    );

    // With reverse from startIndex 5, index 2 should be (5-2) * 50 = 150ms
    expect(result.current.animationDelay).toBe('150ms');
  });

  it('should use startIndex offset', () => {
    const { result } = renderHook(() =>
      useStaggeredAnimation(3, { startIndex: 1 })
    );

    // (3-1) * 50 = 100ms
    expect(result.current.animationDelay).toBe('100ms');
  });
});

describe('calculateStaggerDelays', () => {
  it('should calculate delays for all items', () => {
    const delays = calculateStaggerDelays(3);

    expect(delays).toEqual([0, 50, 100]);
  });

  it('should respect maxDelay', () => {
    const delays = calculateStaggerDelays(10, { delay: 50, maxDelay: 150 });

    expect(delays[0]).toBe(0);
    expect(delays[3]).toBe(150); // Clamped at maxDelay
    expect(delays[9]).toBe(150); // Still clamped
  });

  it('should handle empty count', () => {
    const delays = calculateStaggerDelays(0);

    expect(delays).toEqual([]);
  });

  it('should support custom delay', () => {
    const delays = calculateStaggerDelays(3, { delay: 100 });

    expect(delays).toEqual([0, 100, 200]);
  });
});

describe('STAGGER_CONFIGS', () => {
  it('should have actionBar config', () => {
    expect(STAGGER_CONFIGS.actionBar.delay).toBe(50);
    expect(STAGGER_CONFIGS.actionBar.direction).toBe('forward');
  });

  it('should have menu config', () => {
    expect(STAGGER_CONFIGS.menu.delay).toBe(30);
  });

  it('should have list config', () => {
    expect(STAGGER_CONFIGS.list.delay).toBe(40);
    expect(STAGGER_CONFIGS.list.maxDelay).toBe(400);
  });
});
