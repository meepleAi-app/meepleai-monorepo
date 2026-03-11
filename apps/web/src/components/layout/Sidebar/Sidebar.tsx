/**
 * Sidebar Component - Issue #4936 (updated)
 * Desktop-only collapsible sidebar with context-sensitive navigation.
 * Hidden on mobile (< md breakpoint).
 *
 * Changes from original (Issue #3479):
 * - Starts at top-14 (56px offset for UniversalNavbar)
 * - Height: h-[calc(100vh-56px)] instead of h-screen
 * - Logo removed (now in UniversalNavbar)
 * - SidebarNav replaced by SidebarContextNav (context-sensitive, animated)
 * - SidebarUser kept for notifications + theme (profile moved to navbar)
 */

'use client';

import { cn } from '@/lib/utils';

import { SidebarContextNav } from './SidebarContextNav';
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
        // Offset 56px for UniversalNavbar (h-14)
        'fixed top-14 left-0 z-40',
        'h-[calc(100vh-56px)]',
        'bg-sidebar text-sidebar-foreground',
        'border-r border-sidebar-border',
        'transition-[width] duration-200 ease-in-out motion-reduce:transition-none',
        isCollapsed ? 'w-[var(--sidebar-width-collapsed)]' : 'w-[var(--sidebar-width-expanded)]'
      )}
      data-testid="sidebar"
      aria-label="Main sidebar navigation"
    >
      {/* Toggle button */}
      <SidebarToggle isCollapsed={isCollapsed} onToggle={onToggle} />

      {/* Context-sensitive navigation (scrollable middle) */}
      <SidebarContextNav isCollapsed={isCollapsed} />

      {/* User utilities (bottom): notifications + theme toggle */}
      <SidebarUser isCollapsed={isCollapsed} />
    </aside>
  );
}
