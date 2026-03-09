'use client';

/**
 * useVirtualKeyboard — Detect virtual keyboard visibility
 * Mobile UX Epic — Issue 13
 *
 * Uses the Visual Viewport API to detect when a virtual keyboard
 * is open on mobile devices. When the visual viewport height shrinks
 * significantly compared to the layout viewport, a keyboard is likely open.
 *
 * Usage:
 * - Hide FloatingActionBar and MobileTabBar when keyboard is open
 * - Prevent bottom-fixed elements from overlapping the keyboard
 *
 * Browser support:
 * - Visual Viewport API: Chrome 61+, Safari 13+, Firefox 91+
 * - Falls back to false (keyboard not detected) on unsupported browsers
 */

import { useCallback, useEffect, useState } from 'react';

/** Minimum height difference (px) to consider keyboard open */
const KEYBOARD_THRESHOLD = 150;

export interface UseVirtualKeyboardResult {
  /** Whether the virtual keyboard is currently visible */
  isKeyboardOpen: boolean;
  /** Estimated keyboard height in pixels (0 when closed) */
  keyboardHeight: number;
}

export function useVirtualKeyboard(): UseVirtualKeyboardResult {
  const [state, setState] = useState<UseVirtualKeyboardResult>({
    isKeyboardOpen: false,
    keyboardHeight: 0,
  });

  const handleResize = useCallback(() => {
    const vv = window.visualViewport;
    if (!vv) return;

    const layoutHeight = window.innerHeight;
    const viewportHeight = vv.height;
    const diff = layoutHeight - viewportHeight;

    setState({
      isKeyboardOpen: diff > KEYBOARD_THRESHOLD,
      keyboardHeight: diff > KEYBOARD_THRESHOLD ? diff : 0,
    });
  }, []);

  useEffect(() => {
    const vv = window.visualViewport;
    if (!vv) return;

    // Check initial state
    handleResize();

    vv.addEventListener('resize', handleResize);
    vv.addEventListener('scroll', handleResize);

    return () => {
      vv.removeEventListener('resize', handleResize);
      vv.removeEventListener('scroll', handleResize);
    };
  }, [handleResize]);

  return state;
}
