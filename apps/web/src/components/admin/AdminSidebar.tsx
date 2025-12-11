/**
 * AdminSidebar Component - Issue #881
 *
 * Collapsible sidebar navigation for admin pages.
 * Features:
 * - Collapsible state with localStorage persistence
 * - Badge counts on navigation items
 * - Tooltips when collapsed
 * - Mobile drawer via Sheet component
 */

'use client';

import React, { useCallback, useEffect, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Sheet,
  SheetContent,
  SheetTrigger,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/sheet';
import { Tooltip, TooltipContent, TooltipTrigger, TooltipProvider } from '@/components/ui/tooltip';
import {
  LayoutDashboardIcon,
  UsersIcon,
  SettingsIcon,
  BarChartIcon,
  DatabaseIcon,
  FileTextIcon,
  ServerIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  MenuIcon,
  PackageIcon,
  BellIcon,
} from 'lucide-react';

const SIDEBAR_COLLAPSED_KEY = 'admin-sidebar-collapsed';

export interface NavItemBadge {
  count: number;
  variant?: 'default' | 'secondary' | 'destructive' | 'outline';
}

export interface NavItem {
  href: string;
  label: string;
  icon: typeof LayoutDashboardIcon;
  badge?: NavItemBadge;
}

export interface AdminSidebarProps {
  /** Badge counts for nav items (keyed by href) */
  badges?: Record<string, NavItemBadge>;
  /** Custom navigation items (uses defaults if not provided) */
  navigation?: NavItem[];
  /** Controlled collapsed state */
  collapsed?: boolean;
  /** Callback when collapsed state changes */
  onCollapsedChange?: (collapsed: boolean) => void;
  /** Additional className */
  className?: string;
}

const defaultNavigation: NavItem[] = [
  { href: '/admin', label: 'Dashboard', icon: LayoutDashboardIcon },
  { href: '/admin/infrastructure', label: 'Infrastructure', icon: ServerIcon },
  { href: '/admin/users', label: 'Users', icon: UsersIcon },
  { href: '/admin/alerts', label: 'Alerts', icon: BellIcon },
  { href: '/admin/analytics', label: 'Analytics', icon: BarChartIcon },
  { href: '/admin/configuration', label: 'Configuration', icon: SettingsIcon },
  { href: '/admin/cache', label: 'Cache', icon: DatabaseIcon },
  { href: '/admin/prompts', label: 'Prompts', icon: FileTextIcon },
  { href: '/admin/n8n-templates', label: 'N8N Templates', icon: PackageIcon },
  { href: '/admin/bulk-export', label: 'Bulk Export', icon: PackageIcon },
];

function NavLink({
  item,
  isActive,
  collapsed,
  badge,
}: {
  item: NavItem;
  isActive: boolean;
  collapsed: boolean;
  badge?: NavItemBadge;
}) {
  const Icon = item.icon;

  const linkContent = (
    <Link
      href={item.href}
      className={cn(
        'flex items-center gap-3 rounded-md transition-colors',
        collapsed ? 'justify-center px-2 py-2' : 'px-3 py-2',
        isActive
          ? 'bg-indigo-100 text-indigo-700 dark:bg-indigo-900/50 dark:text-indigo-300'
          : 'text-gray-700 hover:bg-gray-100 hover:text-gray-900 dark:text-gray-300 dark:hover:bg-gray-800 dark:hover:text-white'
      )}
      aria-current={isActive ? 'page' : undefined}
    >
      <Icon className="h-5 w-5 shrink-0" aria-hidden="true" />
      {!collapsed && (
        <>
          <span className="flex-1 text-sm font-medium">{item.label}</span>
          {badge && badge.count > 0 && (
            <Badge variant={badge.variant ?? 'secondary'} className="ml-auto text-xs">
              {badge.count > 99 ? '99+' : badge.count}
            </Badge>
          )}
          {badge && badge.count > 0 && (
            <span className="sr-only">{`Badge count ${badge.count}`}</span>
          )}
        </>
      )}
    </Link>
  );

  if (collapsed) {
    return (
      <Tooltip delayDuration={0}>
        <TooltipTrigger asChild>{linkContent}</TooltipTrigger>
        <TooltipContent side="right" className="flex items-center gap-2">
          {item.label}
          {badge && badge.count > 0 && (
            <Badge variant={badge.variant ?? 'secondary'} className="text-xs">
              {badge.count > 99 ? '99+' : badge.count}
            </Badge>
          )}
        </TooltipContent>
      </Tooltip>
    );
  }

  return linkContent;
}

export function AdminSidebar({
  badges = {},
  navigation = defaultNavigation,
  collapsed: controlledCollapsed,
  onCollapsedChange,
  className,
}: AdminSidebarProps) {
  const pathname = usePathname();
  const [internalCollapsed, setInternalCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [mounted, setMounted] = useState(false);

  const isControlled = controlledCollapsed !== undefined;
  const collapsed = isControlled ? controlledCollapsed : internalCollapsed;

  // Load collapsed state from localStorage on mount
  useEffect(() => {
    setMounted(true);
    if (!isControlled) {
      const stored = localStorage.getItem(SIDEBAR_COLLAPSED_KEY);
      if (stored === 'true') {
        setInternalCollapsed(true);
      }
    }
  }, [isControlled]);

  const toggleCollapsed = useCallback(() => {
    const newValue = !collapsed;
    if (isControlled) {
      onCollapsedChange?.(newValue);
    } else {
      setInternalCollapsed(newValue);
      localStorage.setItem(SIDEBAR_COLLAPSED_KEY, String(newValue));
    }
  }, [collapsed, isControlled, onCollapsedChange]);

  const resolveBadge = (item: NavItem) => {
    const keysToTry = [
      item.href,
      item.href.replace(/^\/admin\//, ''),
      item.href.replace(/^\//, ''),
      item.label.toLowerCase().replace(/\s+/g, '-'),
      item.label.toLowerCase().replace(/\s+/g, ''),
      item.label,
    ];

    const match = keysToTry.find(key => badges[key]);
    return match ? badges[match] : item.badge;
  };

  // Merge navigation items with badges using flexible key matching
  const navWithBadges = navigation.map(item => ({
    ...item,
    badge: resolveBadge(item),
  }));

  const renderNav = (className?: string, style?: React.CSSProperties) => (
    <nav
      aria-label="Admin navigation"
      className={cn('flex flex-col h-full', className)}
      style={style}
    >
      <ul className="space-y-1 flex-1">
        {navWithBadges.map(item => {
          const isActive = pathname === item.href;
          return (
            <li key={item.href}>
              <NavLink item={item} isActive={isActive} collapsed={collapsed} badge={item.badge} />
            </li>
          );
        })}
      </ul>
      <div className="text-xs text-gray-500 dark:text-gray-400">
        {navWithBadges
          .filter(item => item.badge && item.badge.count > 0)
          .map(item => (
            <span key={`badge-${item.href}`} className="mr-1">
              {`${item.label}: ${item.badge?.count && item.badge.count > 99 ? '99+' : item.badge?.count}`}
            </span>
          ))}
      </div>
    </nav>
  );

  // Desktop sidebar
  const desktopSidebar = (
    <aside
      className={cn(
        'hidden lg:flex lg:flex-col border-r border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 transition-all duration-300',
        collapsed ? 'lg:w-16' : 'lg:w-60',
        className
      )}
    >
      {/* Collapse toggle */}
      <div className={cn('p-2', collapsed ? 'flex justify-center' : 'flex justify-end')}>
        <Button
          variant="ghost"
          size="icon"
          onClick={toggleCollapsed}
          aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          className="h-8 w-8"
        >
          {collapsed ? (
            <ChevronRightIcon className="h-4 w-4" />
          ) : (
            <ChevronLeftIcon className="h-4 w-4" />
          )}
        </Button>
      </div>

      {/* Navigation */}
      <div className="flex-1 px-2 pb-4">
        <TooltipProvider>{renderNav('hidden lg:flex', { display: 'flex' })}</TooltipProvider>
      </div>
    </aside>
  );

  // Mobile sidebar (Sheet)
  const mobileSidebar = (
    <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
      <SheetTrigger asChild>
        <Button variant="ghost" size="icon" className="lg:hidden" aria-label="Open sidebar">
          <MenuIcon className="h-5 w-5" />
        </Button>
      </SheetTrigger>
      <SheetContent side="left" className="w-60 p-0">
        <SheetTitle className="px-4 py-3 border-b border-gray-200 dark:border-gray-700">
          Admin Menu
        </SheetTitle>
        <SheetDescription className="sr-only">Navigation menu</SheetDescription>
        <div className="p-2">
          <TooltipProvider>{renderNav()}</TooltipProvider>
        </div>
      </SheetContent>
    </Sheet>
  );

  // Prevent hydration mismatch
  if (!mounted) {
    return (
      <>
        <aside
          className={cn(
            'hidden lg:flex lg:flex-col border-r border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 lg:w-60',
            className
          )}
        >
          <div className="flex-1 px-2 py-4">
            <nav aria-label="Admin navigation" className="hidden lg:flex">
              <ul className="space-y-1">
                {navWithBadges.map(item => {
                  const Icon = item.icon;
                  const isActive = pathname === item.href;
                  return (
                    <li key={item.href}>
                      <Link
                        href={item.href}
                        className={cn(
                          'flex items-center gap-3 px-3 py-2 rounded-md transition-colors text-sm font-medium',
                          isActive
                            ? 'bg-indigo-100 text-indigo-700'
                            : 'text-gray-700 hover:bg-gray-100 hover:text-gray-900'
                        )}
                        aria-current={isActive ? 'page' : undefined}
                      >
                        <Icon className="h-5 w-5 shrink-0" aria-hidden="true" />
                        <span>{item.label}</span>
                        {item.badge && item.badge.count > 0 && (
                          <Badge
                            variant={item.badge.variant ?? 'secondary'}
                            className="ml-auto text-xs"
                          >
                            {item.badge.count > 99 ? '99+' : item.badge.count}
                          </Badge>
                        )}
                      </Link>
                    </li>
                  );
                })}
              </ul>
            </nav>
          </div>
        </aside>
        <Button variant="ghost" size="icon" className="lg:hidden" aria-label="Open navigation menu">
          <MenuIcon className="h-5 w-5" />
        </Button>
      </>
    );
  }

  return (
    <>
      {desktopSidebar}
      {mobileSidebar}
    </>
  );
}

export { defaultNavigation };
