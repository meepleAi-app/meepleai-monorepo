/**
 * EnterpriseAdminSidebar Component
 * Issue #3689 - Layout Base & Navigation System
 *
 * Vertical sidebar for the enterprise admin dashboard.
 * Features:
 * - 7 section navigation (Overview, Resources, Ops, AI, Users, Business, Sims)
 * - Active state highlighting with amber accent
 * - Collapsible on mobile (via Sheet drawer)
 * - Footer with admin user info + audit log button
 * - Glassmorphic design matching mockup
 */

'use client';

import React, { useState, useCallback, useEffect } from 'react';

import {
  MenuIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';


import {
  Sheet,
  SheetContent,
  SheetTrigger,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/navigation/sheet';
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
  TooltipProvider,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import {
  ENTERPRISE_SECTIONS,
  type EnterpriseSection,
} from '@/config/enterprise-navigation';
import { cn } from '@/lib/utils';

import { AuditLogWidget } from './AuditLogWidget';

const SIDEBAR_COLLAPSED_KEY = 'enterprise-sidebar-collapsed';

export interface EnterpriseAdminSidebarProps {
  className?: string;
}

function SidebarNavItem({
  section,
  isActive,
  collapsed,
  onClick,
}: {
  section: EnterpriseSection;
  isActive: boolean;
  collapsed: boolean;
  onClick?: () => void;
}) {
  const Icon = section.icon;
  // (enterprise) route group is invisible in URLs
  const href = `/admin/${section.route}`;

  const linkContent = (
    <Link
      href={href}
      onClick={onClick}
      className={cn(
        'flex items-center gap-3 rounded-lg transition-all duration-200 font-medium text-sm',
        collapsed ? 'justify-center px-2 py-2.5' : 'px-3 py-2.5',
        isActive
          ? 'bg-amber-100 text-amber-900 dark:bg-amber-500/20 dark:text-amber-200 font-semibold'
          : 'text-zinc-600 dark:text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800 hover:text-zinc-900 dark:hover:text-zinc-100'
      )}
      aria-current={isActive ? 'page' : undefined}
      data-testid={`enterprise-nav-${section.id}`}
    >
      <Icon className="h-5 w-5 shrink-0" aria-hidden="true" />
      {!collapsed && <span className="truncate">{section.label}</span>}
    </Link>
  );

  if (collapsed) {
    return (
      <Tooltip delayDuration={0}>
        <TooltipTrigger asChild>{linkContent}</TooltipTrigger>
        <TooltipContent side="right">{section.label}</TooltipContent>
      </Tooltip>
    );
  }

  return linkContent;
}

export function EnterpriseAdminSidebar({ className }: EnterpriseAdminSidebarProps) {
  const pathname = usePathname() ?? '/admin';
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
    const stored = localStorage.getItem(SIDEBAR_COLLAPSED_KEY);
    if (stored === 'true') {
      setCollapsed(true);
    }
  }, []);

  const toggleCollapsed = useCallback(() => {
    setCollapsed((prev) => {
      const next = !prev;
      localStorage.setItem(SIDEBAR_COLLAPSED_KEY, String(next));
      return next;
    });
  }, []);

  const closeMobile = useCallback(() => {
    setMobileOpen(false);
  }, []);

  const isActive = (section: EnterpriseSection) => {
    // Match /admin/{route} or /admin/{route}?tab=...
    const sectionPath = `/admin/${section.route}`;
    return pathname === sectionPath || pathname.startsWith(sectionPath + '/');
  };

  const renderNav = (isMobile: boolean) => (
    <nav aria-label="Enterprise admin navigation" className="flex flex-col h-full">
      {/* Logo / Header */}
      {!collapsed && !isMobile && (
        <div className="px-4 py-5 border-b border-zinc-200/50 dark:border-zinc-700/50 shrink-0">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 bg-gradient-to-br from-amber-500 to-orange-600 rounded-xl shadow-lg shadow-amber-500/25 flex items-center justify-center text-white">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path d="M6 3h12l4 6-10 13L2 9Z" />
              </svg>
            </div>
            <div>
              <div className="font-quicksand font-bold text-base text-zinc-900 dark:text-zinc-100">
                MeepleAI
              </div>
              <div className="text-xs text-zinc-500 dark:text-zinc-400">Admin Console</div>
            </div>
          </div>
        </div>
      )}

      {/* Navigation Items */}
      <div className="flex-1 p-3 space-y-1 overflow-y-auto">
        {ENTERPRISE_SECTIONS.map((section) => (
          <SidebarNavItem
            key={section.id}
            section={section}
            isActive={isActive(section)}
            collapsed={isMobile ? false : collapsed}
            onClick={isMobile ? closeMobile : undefined}
          />
        ))}
      </div>

      {/* Footer - Admin Info + Audit Log Widget */}
      <div className={cn(
        'border-t border-zinc-200/50 dark:border-zinc-700/50 shrink-0',
        collapsed ? 'p-2' : 'p-4'
      )}>
        {!collapsed && (
          <div className="flex items-center gap-2 mb-3">
            <div className="w-8 h-8 rounded-full bg-gradient-to-br from-purple-500 to-violet-600 flex items-center justify-center text-white text-sm font-bold">
              A
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100 truncate">Admin</p>
              <p className="text-xs text-zinc-500 dark:text-zinc-400">SuperAdmin</p>
            </div>
          </div>
        )}
        <AuditLogWidget collapsed={isMobile ? false : collapsed} />
      </div>
    </nav>
  );

  // Skeleton while not mounted (prevent hydration mismatch)
  if (!mounted) {
    return (
      <>
        <aside
          className={cn(
            'hidden lg:flex lg:flex-col border-r border-zinc-200/50 dark:border-zinc-700/50',
            'bg-white/80 backdrop-blur-xl dark:bg-zinc-900/95 dark:backdrop-blur-none',
            'lg:w-56',
            className
          )}
        >
          <div className="flex-1 p-3">
            <div className="animate-pulse space-y-2">
              {Array.from({ length: 7 }).map((_, i) => (
                <div key={i} className="h-10 bg-muted/30 rounded-lg" />
              ))}
            </div>
          </div>
        </aside>
        <Button variant="ghost" size="icon" className="lg:hidden" aria-label="Open menu">
          <MenuIcon className="h-5 w-5" />
        </Button>
      </>
    );
  }

  // Desktop sidebar
  const desktopSidebar = (
    <aside
      className={cn(
        'hidden lg:flex lg:flex-col transition-all duration-300 h-full',
        'bg-white/80 backdrop-blur-xl dark:bg-zinc-900/95 dark:backdrop-blur-none',
        'border-r border-zinc-200/50 dark:border-zinc-700/50',
        collapsed ? 'lg:w-16' : 'lg:w-56',
        className
      )}
      data-testid="enterprise-sidebar-desktop"
    >
      {/* Collapse toggle */}
      <div className={cn('p-2 shrink-0', collapsed ? 'flex justify-center' : 'flex justify-end')}>
        <Button
          variant="ghost"
          size="icon"
          onClick={toggleCollapsed}
          aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          className="h-7 w-7"
          data-testid="enterprise-sidebar-toggle"
        >
          {collapsed ? (
            <ChevronRightIcon className="h-4 w-4" />
          ) : (
            <ChevronLeftIcon className="h-4 w-4" />
          )}
        </Button>
      </div>

      <TooltipProvider>{renderNav(false)}</TooltipProvider>
    </aside>
  );

  // Mobile sidebar (Sheet drawer)
  const mobileSidebar = (
    <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
      <SheetTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="lg:hidden fixed top-3 left-3 z-40"
          aria-label="Open enterprise admin menu"
          data-testid="enterprise-sidebar-mobile-trigger"
        >
          <MenuIcon className="h-5 w-5" />
        </Button>
      </SheetTrigger>
      <SheetContent
        side="left"
        className="w-64 p-0 flex flex-col"
        data-testid="enterprise-sidebar-mobile"
      >
        <SheetTitle className="px-4 py-3 border-b border-zinc-200/50 dark:border-zinc-700/50 shrink-0">
          Enterprise Admin
        </SheetTitle>
        <SheetDescription className="sr-only">
          Navigation menu for enterprise admin dashboard
        </SheetDescription>
        <div className="flex-1 overflow-y-auto">
          {renderNav(true)}
        </div>
      </SheetContent>
    </Sheet>
  );

  return (
    <>
      {desktopSidebar}
      {mobileSidebar}
    </>
  );
}

export default EnterpriseAdminSidebar;
