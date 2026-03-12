import { useCallback, useEffect, useRef, useState } from 'react';

const EXPAND_DELAY = 200;
const COLLAPSE_DELAY = 300;

export interface UseCardRackStateReturn {
  isExpanded: boolean;
  onMouseEnter: () => void;
  onMouseLeave: () => void;
  rackRef: React.RefObject<HTMLElement | null>;
}

export function useCardRackState(): UseCardRackStateReturn {
  const [isExpanded, setIsExpanded] = useState(false);
  const expandTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const collapseTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const rackRef = useRef<HTMLElement | null>(null);

  const clearTimers = useCallback(() => {
    if (expandTimer.current) {
      clearTimeout(expandTimer.current);
      expandTimer.current = null;
    }
    if (collapseTimer.current) {
      clearTimeout(collapseTimer.current);
      collapseTimer.current = null;
    }
  }, []);

  const onMouseEnter = useCallback(() => {
    clearTimers();
    expandTimer.current = setTimeout(() => {
      setIsExpanded(true);
    }, EXPAND_DELAY);
  }, [clearTimers]);

  const onMouseLeave = useCallback(() => {
    clearTimers();
    collapseTimer.current = setTimeout(() => {
      setIsExpanded(false);
    }, COLLAPSE_DELAY);
  }, [clearTimers]);

  // Clean up pending timers on unmount to prevent state updates after unmount
  useEffect(() => {
    return () => clearTimers();
  }, [clearTimers]);

  return { isExpanded, onMouseEnter, onMouseLeave, rackRef };
}
