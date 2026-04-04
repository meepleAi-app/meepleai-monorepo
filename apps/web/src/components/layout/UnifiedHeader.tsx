/**
 * UnifiedHeader - Compact header for public/unauthenticated pages
 * Issue #3104 - Unify header navigation
 *
 * Used by PublicLayout for guest pages (home, about, pricing, etc.).
 * Authenticated users use UserShell/AdminShell instead.
 *
 * Features:
 * - Compact 48px height
 * - Left: Logo (icon only)
 * - Right: Notifications + User avatar dropdown
 * - Glass morphism design
 * - WCAG 2.1 AA compliance
 */

'use client';

import { useState, useEffect } from 'react';

import Link from 'next/link';

import { NotificationBell } from '@/components/notifications';
import { MeepleLogo } from '@/components/ui/meeple/meeple-logo';
import { useNavigationItems } from '@/hooks/useNavigationItems';
import { cn } from '@/lib/utils';

import { UserMenuDropdown } from './UserMenuDropdown';

export interface UnifiedHeaderProps {
  /** Additional className */
  className?: string;
}

export function UnifiedHeader({ className }: UnifiedHeaderProps) {
  const { isAuthLoading, isAuthenticated } = useNavigationItems();
  const [isScrolled, setIsScrolled] = useState(false);

  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 10);
    };
    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return (
    <header
      className={cn(
        'sticky top-0 z-50 w-full border-b',
        // Glass morphism
        'bg-background/95 backdrop-blur-[16px] backdrop-saturate-[180%]',
        'dark:bg-card dark:backdrop-blur-none',
        'border-border/50 dark:border-border/30',
        'transition-shadow duration-200',
        isScrolled && 'shadow-sm dark:shadow-md',
        className
      )}
      data-testid="unified-header"
    >
      <div className="container mx-auto flex h-12 items-center justify-between px-4">
        {/* Left: Logo (icon only) */}
        <div className="flex items-center gap-2">
          <Link href="/" className="flex items-center" aria-label="MeepleAI Home">
            <MeepleLogo variant="icon" size="sm" />
          </Link>
        </div>

        {/* Right: Notifications + User Menu */}
        <div className="flex items-center gap-1">
          {isAuthenticated && !isAuthLoading && <NotificationBell />}
          <UserMenuDropdown />
        </div>
      </div>
    </header>
  );
}
