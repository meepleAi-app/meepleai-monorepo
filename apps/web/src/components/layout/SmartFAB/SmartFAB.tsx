/**
 * SmartFAB — Context-Aware Floating Action Button
 * Issues #6, #11, #12 from mobile-first-ux-epic.md
 *
 * A single floating button on mobile that changes icon and action
 * based on the current route context. Hides during fast scroll.
 *
 * Features:
 *   - 56px diameter, bg-primary, rounded-full
 *   - Position: right-4, above MobileTabBar
 *   - Mobile only (md:hidden)
 *   - Hides when FloatingActionBar has actions (avoid duplication)
 *   - Hides during scroll down (reuses useScrollDirection)
 *   - Long-press QuickMenu: radial menu with 2-3 secondary actions (#11)
 *   - Morph animation: icon rotates + scales on context change (#12)
 */

'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { resolveSmartFab } from '@/config/smart-fab';
import type { SmartFabAction } from '@/config/smart-fab';
import { useNavigation } from '@/context/NavigationContext';
import { usePrefersReducedMotion } from '@/hooks/useResponsive';
import { useScrollDirection } from '@/hooks/useScrollDirection';
import { cn } from '@/lib/utils';

// ─── QuickMenu Item ──────────────────────────────────────────────────────────

function QuickMenuItem({
  action,
  index,
  total,
  onClose,
}: {
  action: SmartFabAction;
  index: number;
  total: number;
  onClose: () => void;
}) {
  const Icon = action.icon;
  const isLink = !action.href.startsWith('#');

  // Fan items upward in an arc from bottom-right
  const angle = Math.PI / 2 + ((index + 1) / (total + 1)) * Math.PI * 0.6 - Math.PI * 0.3;
  const radius = 72;
  const x = -Math.cos(angle) * radius;
  const y = -Math.sin(angle) * radius;

  const className = cn(
    'absolute flex items-center gap-2',
    'animate-in fade-in-0 zoom-in-75 duration-150',
    'origin-bottom-right'
  );

  const buttonClassName = cn(
    'flex items-center justify-center',
    'h-11 w-11 rounded-full',
    'bg-card/95 backdrop-blur-sm',
    'border border-border/60',
    'shadow-md shadow-black/10',
    'text-foreground',
    'active:scale-90',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
  );

  const handleClick = () => {
    if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
      navigator.vibrate(5);
    }
    onClose();
  };

  const content = (
    <>
      <span
        className={cn(
          'px-2 py-1 rounded-lg',
          'bg-foreground/90 text-background',
          'text-xs font-medium font-nunito whitespace-nowrap',
          'pointer-events-none'
        )}
      >
        {action.label}
      </span>
      <div className={buttonClassName}>
        <Icon className="h-4 w-4" />
      </div>
    </>
  );

  if (isLink) {
    return (
      <Link
        href={action.href}
        className={className}
        style={{
          transform: `translate(${x}px, ${y}px)`,
          animationDelay: `${index * 50}ms`,
        }}
        aria-label={action.label}
        onClick={handleClick}
        data-testid={`quick-menu-item-${index}`}
      >
        {content}
      </Link>
    );
  }

  return (
    <button
      type="button"
      className={className}
      style={{
        transform: `translate(${x}px, ${y}px)`,
        animationDelay: `${index * 50}ms`,
      }}
      aria-label={action.label}
      onClick={handleClick}
      data-testid={`quick-menu-item-${index}`}
    >
      {content}
    </button>
  );
}

// ─── SmartFAB ────────────────────────────────────────────────────────────────

export function SmartFAB() {
  const pathname = usePathname();
  const scrollDirection = useScrollDirection({ threshold: 50 });
  const prefersReducedMotion = usePrefersReducedMotion();
  const { actionBarActions } = useNavigation();

  const [isQuickMenuOpen, setIsQuickMenuOpen] = useState(false);
  const [isMorphing, setIsMorphing] = useState(false);
  const longPressRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const prevPathnameRef = useRef(pathname);

  const resolved = useMemo(() => resolveSmartFab(pathname), [pathname]);
  const { primary: action, secondary: secondaryActions } = resolved;

  // Morph animation on context change (Issue #12)
  useEffect(() => {
    if (prevPathnameRef.current !== pathname && !prefersReducedMotion) {
      setIsMorphing(true);
      const timer = setTimeout(() => setIsMorphing(false), 300);
      prevPathnameRef.current = pathname;
      return () => clearTimeout(timer);
    }
    prevPathnameRef.current = pathname;
  }, [pathname, prefersReducedMotion]);

  // Close QuickMenu on scroll
  useEffect(() => {
    if (scrollDirection === 'down') setIsQuickMenuOpen(false);
  }, [scrollDirection]);

  // Close QuickMenu on outside click
  useEffect(() => {
    if (!isQuickMenuOpen) return;
    const handleClick = () => setIsQuickMenuOpen(false);
    const timer = setTimeout(() => document.addEventListener('click', handleClick), 0);
    return () => {
      clearTimeout(timer);
      document.removeEventListener('click', handleClick);
    };
  }, [isQuickMenuOpen]);

  const handleLongPressStart = useCallback(() => {
    if (secondaryActions.length === 0) return;
    longPressRef.current = setTimeout(() => {
      setIsQuickMenuOpen(true);
      if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
        navigator.vibrate(15);
      }
    }, 400);
  }, [secondaryActions.length]);

  const handleLongPressEnd = useCallback(() => {
    if (longPressRef.current) {
      clearTimeout(longPressRef.current);
      longPressRef.current = null;
    }
  }, []);

  // Hide when FloatingActionBar has visible actions (avoid duplication)
  const hasFloatingActions = actionBarActions.filter(a => !a.hidden).length > 0;
  if (hasFloatingActions) return null;

  const isHiddenByScroll = scrollDirection === 'down';
  const Icon = action.icon;
  const isLink = !action.href.startsWith('#') && !isQuickMenuOpen;
  const hasQuickMenu = secondaryActions.length > 0;

  const buttonContent = (
    <>
      <Icon
        className={cn(
          'h-6 w-6',
          // Morph animation (Issue #12)
          isMorphing && 'animate-in spin-in-90 zoom-in-75 duration-300'
        )}
      />
      <span className="sr-only">{action.label}</span>
    </>
  );

  const sharedClassName = cn(
    // Mobile only
    'md:hidden',
    // Size: 56px circle
    'h-14 w-14',
    'flex items-center justify-center',
    'rounded-full',
    // Design: primary color + elevation
    'bg-primary text-primary-foreground',
    'shadow-lg shadow-primary/25',
    // Interaction
    'active:scale-95',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
    // QuickMenu open state
    isQuickMenuOpen && 'rotate-45 scale-110 shadow-xl shadow-primary/40'
  );

  const wrapperClassName = cn(
    // Mobile only
    'md:hidden',
    // Position: right side, above MobileTabBar
    'fixed right-4 z-45',
    'bottom-[calc(72px+1rem+env(safe-area-inset-bottom))]',
    // Entrance animation
    'animate-in fade-in-0 zoom-in-75 duration-200',
    // Auto-hide on scroll
    isHiddenByScroll &&
      !isQuickMenuOpen &&
      (prefersReducedMotion ? 'invisible' : 'translate-y-[calc(100%+24px)] opacity-0'),
    !prefersReducedMotion && 'transition-[transform,opacity] duration-200 ease-in-out'
  );

  const fabElement = isLink ? (
    <Link
      href={action.href}
      className={sharedClassName}
      aria-label={action.label}
      onTouchStart={handleLongPressStart}
      onTouchEnd={handleLongPressEnd}
      onMouseDown={handleLongPressStart}
      onMouseUp={handleLongPressEnd}
      onMouseLeave={handleLongPressEnd}
      data-testid="smart-fab"
    >
      {buttonContent}
    </Link>
  ) : (
    <button
      type="button"
      className={sharedClassName}
      aria-label={action.label}
      onTouchStart={handleLongPressStart}
      onTouchEnd={handleLongPressEnd}
      onMouseDown={handleLongPressStart}
      onMouseUp={handleLongPressEnd}
      onMouseLeave={handleLongPressEnd}
      onClick={() => {
        if (isQuickMenuOpen) {
          setIsQuickMenuOpen(false);
          return;
        }
        if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
          navigator.vibrate(10);
        }
      }}
      data-testid="smart-fab"
    >
      {buttonContent}
    </button>
  );

  return (
    <div className={wrapperClassName} data-testid="smart-fab-wrapper">
      {/* QuickMenu backdrop */}
      {isQuickMenuOpen && (
        <div
          className="fixed inset-0 z-[-1] bg-black/20 backdrop-blur-[2px] animate-in fade-in-0 duration-150"
          data-testid="quick-menu-backdrop"
          aria-hidden="true"
        />
      )}

      {/* QuickMenu items */}
      {isQuickMenuOpen && hasQuickMenu && (
        <div
          className="absolute bottom-0 right-0"
          role="menu"
          aria-label="Azioni rapide"
          data-testid="quick-menu"
        >
          {secondaryActions.map((sa, i) => (
            <QuickMenuItem
              key={sa.href}
              action={sa}
              index={i}
              total={secondaryActions.length}
              onClose={() => setIsQuickMenuOpen(false)}
            />
          ))}
        </div>
      )}

      {/* Main FAB button */}
      {fabElement}

      {/* Long-press hint (shows briefly on first visit) */}
      {hasQuickMenu && !isQuickMenuOpen && (
        <div
          className={cn(
            'absolute -top-1 -left-1',
            'h-3 w-3 rounded-full',
            'bg-accent border-2 border-background',
            'pointer-events-none'
          )}
          aria-hidden="true"
          data-testid="quick-menu-indicator"
        />
      )}
    </div>
  );
}
