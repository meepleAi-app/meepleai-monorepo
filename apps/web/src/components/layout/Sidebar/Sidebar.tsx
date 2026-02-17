/**
 * Sidebar Component
 * Desktop-only collapsible sidebar with navigation, user section, and logo.
 * Hidden on mobile (< md breakpoint).
 */

'use client';

import Link from 'next/link';

import { MeepleLogo } from '@/components/ui/meeple/meeple-logo';
import { cn } from '@/lib/utils';

import { SidebarNav } from './SidebarNav';
import { SidebarToggle } from './SidebarToggle';
import { SidebarUser } from './SidebarUser';

export interface SidebarProps {
  isCollapsed: boolean;
  onToggle: () => void;
}

export function Sidebar({ isCollapsed, onToggle }: SidebarProps) {
  return (
    <aside
      className={cn(
        'hidden md:flex flex-col',
        'fixed top-0 left-0 h-screen z-40',
        'bg-sidebar text-sidebar-foreground',
        'border-r border-sidebar-border',
        'transition-[width] duration-200 ease-in-out motion-reduce:transition-none',
        isCollapsed ? 'w-[60px]' : 'w-[220px]'
      )}
      data-testid="sidebar"
      aria-label="Main sidebar navigation"
    >
      {/* Toggle button */}
      <SidebarToggle isCollapsed={isCollapsed} onToggle={onToggle} />

      {/* Logo */}
      <div className={cn('flex items-center border-b border-sidebar-border', isCollapsed ? 'justify-center px-2 py-4' : 'px-4 py-4')}>
        <Link href="/dashboard" className="flex items-center gap-2" aria-label="MeepleAI Home">
          <MeepleLogo variant="icon" size="sm" />
          {!isCollapsed && (
            <span className="font-quicksand font-bold text-lg text-sidebar-foreground">
              MeepleAI
            </span>
          )}
        </Link>
      </div>

      {/* Navigation (scrollable middle) */}
      <SidebarNav isCollapsed={isCollapsed} />

      {/* User section (bottom) */}
      <SidebarUser isCollapsed={isCollapsed} />
    </aside>
  );
}
