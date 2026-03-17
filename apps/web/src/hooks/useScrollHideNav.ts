'use client';

import { useState, useEffect, useRef, useCallback, type RefObject } from 'react';

export type ScrollDirection = 'up' | 'down' | null;

interface UseScrollHideNavOptions {
  scrollContainerRef: RefObject<HTMLElement | null>;
  threshold?: number;
  disabled?: boolean;
}

interface UseScrollHideNavReturn {
  isNavVisible: boolean;
  scrollDirection: ScrollDirection;
}

export function useScrollHideNav({
  scrollContainerRef,
  threshold = 10,
  disabled = false,
}: UseScrollHideNavOptions): UseScrollHideNavReturn {
  const [isNavVisible, setIsNavVisible] = useState(true);
  const [scrollDirection, setScrollDirection] = useState<ScrollDirection>(null);
  const lastScrollTop = useRef(0);

  const handleScroll = useCallback(() => {
    const el = scrollContainerRef.current;
    if (!el) return;
    const currentScrollTop = el.scrollTop;
    const diff = currentScrollTop - lastScrollTop.current;
    if (Math.abs(diff) < threshold) return;
    const newDirection: ScrollDirection = diff > 0 ? 'down' : 'up';
    setScrollDirection(newDirection);
    setIsNavVisible(newDirection === 'up');
    lastScrollTop.current = currentScrollTop;
  }, [scrollContainerRef, threshold]);

  useEffect(() => {
    if (disabled) return;
    const el = scrollContainerRef.current;
    if (!el) return;
    lastScrollTop.current = el.scrollTop;
    el.addEventListener('scroll', handleScroll, { passive: true });
    return () => el.removeEventListener('scroll', handleScroll);
  }, [scrollContainerRef, handleScroll, disabled]);

  if (disabled) {
    return { isNavVisible: true, scrollDirection: null };
  }

  return { isNavVisible, scrollDirection };
}
