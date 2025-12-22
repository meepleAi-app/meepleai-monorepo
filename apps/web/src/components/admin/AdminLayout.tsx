/**
 * AdminLayout Component - Issue #881
 *
 * Enhanced admin layout with modular sidebar, header, and breadcrumbs.
 * Features:
 * - Collapsible sidebar with localStorage persistence
 * - Badge counts on navigation items
 * - User menu with profile/settings/logout
 * - Breadcrumb navigation
 * - Mobile responsive with Sheet drawer
 * - Dark mode support
 */

'use client';

import { ReactNode, useCallback, useEffect, useState } from 'react';

import { MenuIcon } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Sheet,
  SheetContent,
  SheetTrigger,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/sheet';
import { TooltipProvider } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';

import { AdminBreadcrumbs, type BreadcrumbItem } from './AdminBreadcrumbs';
import { AdminHeader, type AdminUser } from './AdminHeader';
import { AdminSidebar, type NavItemBadge } from './AdminSidebar';

const SIDEBAR_COLLAPSED_KEY = 'admin-sidebar-collapsed';

export interface AdminLayoutProps {
  /** Page content */
  children: ReactNode;
  /** Current user info for header */
  user?: AdminUser;
  /** Badge counts for navigation items */
  badges?: Record<string, NavItemBadge>;
  /** Custom breadcrumb items */
  breadcrumbs?: BreadcrumbItem[];
  /** Show breadcrumbs */
  showBreadcrumbs?: boolean;
  /** Additional header actions */
  headerActions?: ReactNode;
  /** Additional className for main content */
  className?: string;
}

export function AdminLayout({
  children,
  user,
  badges,
  breadcrumbs,
  showBreadcrumbs = true,
  headerActions,
  className,
}: AdminLayoutProps) {
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [mounted, setMounted] = useState(false);

  // Load collapsed state from localStorage on mount
  useEffect(() => {
    setMounted(true);
    const stored = localStorage.getItem(SIDEBAR_COLLAPSED_KEY);
    if (stored === 'true') {
      setCollapsed(true);
    }
  }, []);

  const handleCollapsedChange = useCallback((value: boolean) => {
    setCollapsed(value);
    localStorage.setItem(SIDEBAR_COLLAPSED_KEY, String(value));
  }, []);

  // Mobile menu trigger for header
  const mobileMenuTrigger = (
    <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
      <SheetTrigger asChild>
        <Button variant="ghost" size="icon" aria-label="Open navigation menu">
          <MenuIcon className="h-5 w-5" />
        </Button>
      </SheetTrigger>
      <SheetContent side="left" className="w-60 p-0">
        <SheetTitle className="px-4 py-3 border-b border-gray-200 dark:border-gray-700">
          Admin Menu
        </SheetTitle>
        <SheetDescription className="sr-only">Navigation menu</SheetDescription>
        <div className="p-2">
          <TooltipProvider>
            <AdminSidebar badges={badges} collapsed={false} className="border-0 lg:flex" />
          </TooltipProvider>
        </div>
      </SheetContent>
    </Sheet>
  );

  // Prevent hydration mismatch by showing default state initially
  const _sidebarWidth = !mounted ? 'lg:w-60' : collapsed ? 'lg:w-16' : 'lg:w-60';

  const badgeSummary =
    badges && Object.keys(badges).length > 0 ? (
      <span
        className="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400"
        data-testid="badge-summary"
      >
        {Object.entries(badges).map(([key, badge]) => (
          <span key={`badge-${key}`} aria-label={`${key} badge`}>
            {`${key}: ${badge.count > 99 ? '99+' : badge.count}`}
          </span>
        ))}
      </span>
    ) : null;

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-950">
      {/* Header */}
      <AdminHeader
        user={user}
        mobileMenuTrigger={mobileMenuTrigger}
        actions={
          <>
            {headerActions}
            {badgeSummary}
          </>
        }
      />

      <div className="flex">
        {/* Sidebar - Desktop only (mobile uses Sheet) */}
        <TooltipProvider>
          <AdminSidebar
            badges={badges}
            collapsed={collapsed}
            onCollapsedChange={handleCollapsedChange}
            className="hidden lg:flex"
          />
        </TooltipProvider>

        {/* Main Content */}
        <main
          className={cn(
            'flex-1 min-h-[calc(100vh-3.5rem)]',
            'transition-all duration-300',
            className
          )}
        >
          <div className="p-4 sm:p-6 lg:p-8">
            {/* Breadcrumbs */}
            {showBreadcrumbs && <AdminBreadcrumbs items={breadcrumbs} className="mb-4" />}

            {/* Page Content */}
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}
