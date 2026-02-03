/**
 * useLongPress Hook
 * Issue #3291 - Phase 5: Smart FAB
 *
 * Provides long-press detection for touch and mouse interactions.
 * Used for triggering quick menu on FAB.
 */

'use client';

import { useCallback, useRef, useEffect } from 'react';

import { FAB_TIMING } from '@/config/fab';

/**
 * Long press event handlers
 */
export interface LongPressHandlers {
  onTouchStart: (e: React.TouchEvent) => void;
  onTouchEnd: (e: React.TouchEvent) => void;
  onTouchMove: (e: React.TouchEvent) => void;
  onMouseDown: (e: React.MouseEvent) => void;
  onMouseUp: (e: React.MouseEvent) => void;
  onMouseLeave: (e: React.MouseEvent) => void;
}

/**
 * Long press options
 */
export interface LongPressOptions {
  /** Delay before long press is triggered (ms) */
  delay?: number;
  /** Callback for regular click/tap */
  onClick?: () => void;
  /** Movement threshold before canceling long press */
  moveThreshold?: number;
}

/**
 * useLongPress Hook
 *
 * Detects long press interactions on touch and mouse devices.
 * Returns handlers to attach to interactive elements.
 *
 * @param onLongPress - Callback when long press is detected
 * @param options - Configuration options
 * @returns Event handlers to spread onto the element
 *
 * @example
 * ```tsx
 * const handlers = useLongPress(handleLongPress, { onClick: handleClick });
 * return <button {...handlers}>Press Me</button>;
 * ```
 */
export function useLongPress(
  onLongPress: () => void,
  options: LongPressOptions = {}
): LongPressHandlers {
  const {
    delay = FAB_TIMING.longPressDelay,
    onClick,
    moveThreshold = 10,
  } = options;

  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isLongPressRef = useRef(false);
  const startPosRef = useRef<{ x: number; y: number } | null>(null);

  // Cleanup timer on unmount
  useEffect(() => {
    return () => {
      if (timerRef.current) {
        clearTimeout(timerRef.current);
      }
    };
  }, []);

  const cancelLongPress = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  }, []);

  const startLongPress = useCallback(
    (x: number, y: number) => {
      isLongPressRef.current = false;
      startPosRef.current = { x, y };

      timerRef.current = setTimeout(() => {
        isLongPressRef.current = true;
        onLongPress();
        // Haptic feedback on mobile
        if ('vibrate' in navigator) {
          navigator.vibrate(50);
        }
      }, delay);
    },
    [delay, onLongPress]
  );

  const endPress = useCallback(() => {
    cancelLongPress();

    // If not a long press, trigger regular click
    if (!isLongPressRef.current && onClick) {
      onClick();
    }

    startPosRef.current = null;
    isLongPressRef.current = false;
  }, [cancelLongPress, onClick]);

  const checkMovement = useCallback(
    (x: number, y: number) => {
      if (!startPosRef.current) return;

      const dx = Math.abs(x - startPosRef.current.x);
      const dy = Math.abs(y - startPosRef.current.y);

      if (dx > moveThreshold || dy > moveThreshold) {
        cancelLongPress();
      }
    },
    [moveThreshold, cancelLongPress]
  );

  // Touch handlers
  const onTouchStart = useCallback(
    (e: React.TouchEvent) => {
      const touch = e.touches[0];
      startLongPress(touch.clientX, touch.clientY);
    },
    [startLongPress]
  );

  const onTouchEnd = useCallback(
    (e: React.TouchEvent) => {
      e.preventDefault(); // Prevent mouse events from firing
      endPress();
    },
    [endPress]
  );

  const onTouchMove = useCallback(
    (e: React.TouchEvent) => {
      const touch = e.touches[0];
      checkMovement(touch.clientX, touch.clientY);
    },
    [checkMovement]
  );

  // Mouse handlers (for testing and desktop)
  const onMouseDown = useCallback(
    (e: React.MouseEvent) => {
      if (e.button !== 0) return; // Only primary button
      startLongPress(e.clientX, e.clientY);
    },
    [startLongPress]
  );

  const onMouseUp = useCallback(() => {
    endPress();
  }, [endPress]);

  const onMouseLeave = useCallback(() => {
    cancelLongPress();
    startPosRef.current = null;
  }, [cancelLongPress]);

  return {
    onTouchStart,
    onTouchEnd,
    onTouchMove,
    onMouseDown,
    onMouseUp,
    onMouseLeave,
  };
}
