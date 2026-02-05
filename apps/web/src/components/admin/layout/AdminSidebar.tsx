/**
 * AdminSidebar Component - Issue #3595
 *
 * Enhanced collapsible sidebar navigation for admin pages.
 * Features:
 * - Collapsible sections for organized navigation (50+ pages)
 * - Collapsible sidebar state with localStorage persistence
 * - Badge counts on navigation items
 * - Tooltips when sidebar is collapsed
 * - Mobile drawer via Sheet component
 * - Active route highlighting with nested route support
 */

'use client';

import React, { useCallback, useEffect, useState, useMemo } from 'react';

import {
  ChevronLeftIcon,
  ChevronRightIcon,
  ChevronDownIcon,
  MenuIcon,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/data-display/collapsible';
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
  ADMIN_NAVIGATION,
  ADMIN_SIDEBAR_COLLAPSED_KEY,
  ADMIN_SIDEBAR_SECTIONS_KEY,
  getExpandedSections,
  isAdminNavItemActive,
  type AdminNavItem,
  type AdminNavSection,
} from '@/config/admin-navigation';
import { cn } from '@/lib/utils';

export interface NavItemBadge {
  count: number;
  variant?: 'default' | 'secondary' | 'destructive' | 'outline';
}

export interface AdminSidebarProps {
  /** Badge counts for nav items (keyed by badge key or href) */
  badges?: Record<string, NavItemBadge>;
  /** Controlled collapsed state */
  collapsed?: boolean;
  /** Callback when collapsed state changes */
  onCollapsedChange?: (collapsed: boolean) => void;
  /** Additional className */
  className?: string;
}

/**
 * Individual navigation link component
 */
function NavLink({
  item,
  isActive,
  collapsed,
  badge,
  onClick,
}: {
  item: AdminNavItem;
  isActive: boolean;
  collapsed: boolean;
  badge?: NavItemBadge;
  onClick?: () => void;
}) {
  const Icon = item.icon;

  const linkContent = (
    <Link
      href={item.href}
      onClick={onClick}
      className={cn(
        'flex items-center gap-3 rounded-[0.625rem] transition-all duration-200 font-medium text-sm',
        collapsed ? 'justify-center px-2 py-2' : 'px-4 py-2',
        isActive
          ? 'bg-accent text-accent-foreground dark:bg-accent/90'
          : 'text-muted-foreground hover:bg-accent/10 dark:hover:bg-accent/20 hover:text-accent-foreground'
      )}
      aria-current={isActive ? 'page' : undefined}
      data-testid={`admin-nav-link-${item.href.replace(/^\/admin\/?/, '') || 'dashboard'}`}
    >
      <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
      {!collapsed && (
        <>
          <span className="flex-1 truncate">{item.label}</span>
          {badge && badge.count > 0 && (
            <Badge variant={badge.variant ?? 'secondary'} className="ml-auto text-xs">
              {badge.count > 99 ? '99+' : badge.count}
            </Badge>
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

/**
 * Collapsible section component for navigation groups
 */
function NavSection({
  section,
  pathname,
  collapsed,
  isExpanded,
  onToggle,
  badges,
  onNavClick,
}: {
  section: AdminNavSection;
  pathname: string;
  collapsed: boolean;
  isExpanded: boolean;
  onToggle: () => void;
  badges: Record<string, NavItemBadge>;
  onNavClick?: () => void;
}) {
  const SectionIcon = section.icon;
  const hasActiveItem = section.items.some((item) => isAdminNavItemActive(item, pathname));

  // Calculate total badges in section
  const sectionBadgeCount = section.items.reduce((total, item) => {
    const badge = resolveBadge(item, badges);
    return total + (badge?.count ?? 0);
  }, 0);

  // When sidebar is collapsed, show only section icons with tooltips
  if (collapsed) {
    const firstActiveItem = section.items.find((item) => isAdminNavItemActive(item, pathname));
    const targetHref = firstActiveItem?.href ?? section.items[0]?.href ?? '/admin';

    return (
      <Tooltip delayDuration={0}>
        <TooltipTrigger asChild>
          <Link
            href={targetHref}
            className={cn(
              'flex items-center justify-center p-2 rounded-[0.625rem] transition-all duration-200',
              hasActiveItem
                ? 'bg-accent text-accent-foreground dark:bg-accent/90'
                : 'text-muted-foreground hover:bg-accent/10 dark:hover:bg-accent/20 hover:text-accent-foreground'
            )}
            data-testid={`admin-nav-section-${section.id}`}
          >
            <SectionIcon className="h-5 w-5" aria-hidden="true" />
            {sectionBadgeCount > 0 && (
              <span className="sr-only">{sectionBadgeCount} items need attention</span>
            )}
          </Link>
        </TooltipTrigger>
        <TooltipContent side="right" className="flex flex-col gap-1 p-3 max-w-[200px]">
          <span className="font-semibold flex items-center gap-2">
            {section.label}
            {sectionBadgeCount > 0 && (
              <Badge variant="secondary" className="text-xs">
                {sectionBadgeCount}
              </Badge>
            )}
          </span>
          <span className="text-xs text-muted-foreground">
            {section.items.length} items
          </span>
        </TooltipContent>
      </Tooltip>
    );
  }

  return (
    <Collapsible open={isExpanded} onOpenChange={onToggle}>
      <CollapsibleTrigger asChild>
        <button
          className={cn(
            'flex items-center gap-3 w-full px-4 py-2 rounded-[0.625rem] transition-all duration-200 font-semibold text-sm',
            hasActiveItem
              ? 'text-accent-foreground'
              : 'text-muted-foreground hover:text-foreground'
          )}
          data-testid={`admin-nav-section-trigger-${section.id}`}
        >
          <SectionIcon className="h-4 w-4 shrink-0" aria-hidden="true" />
          <span className="flex-1 text-left">{section.label}</span>
          {sectionBadgeCount > 0 && (
            <Badge variant="secondary" className="text-xs mr-1">
              {sectionBadgeCount > 99 ? '99+' : sectionBadgeCount}
            </Badge>
          )}
          <ChevronDownIcon
            className={cn(
              'h-4 w-4 shrink-0 transition-transform duration-200',
              isExpanded ? '' : '-rotate-90'
            )}
            aria-hidden="true"
          />
        </button>
      </CollapsibleTrigger>
      <CollapsibleContent className="overflow-hidden">
        <ul className="pl-4 pt-1 space-y-0.5">
          {section.items.map((item) => {
            const isActive = isAdminNavItemActive(item, pathname);
            const badge = resolveBadge(item, badges);
            return (
              <li key={item.href}>
                <NavLink
                  item={item}
                  isActive={isActive}
                  collapsed={false}
                  badge={badge}
                  onClick={onNavClick}
                />
              </li>
            );
          })}
        </ul>
      </CollapsibleContent>
    </Collapsible>
  );
}

/**
 * Resolve badge for a navigation item using flexible key matching
 */
function resolveBadge(
  item: AdminNavItem,
  badges: Record<string, NavItemBadge>
): NavItemBadge | undefined {
  const keysToTry = [
    item.badge?.key,
    item.href,
    item.href.replace(/^\/admin\//, ''),
    item.href.replace(/^\//, ''),
    item.label.toLowerCase().replace(/\s+/g, '-'),
    item.label.toLowerCase().replace(/\s+/g, ''),
    item.label,
  ].filter(Boolean) as string[];

  const match = keysToTry.find((key) => badges[key]);
  if (match && badges[match]) {
    return {
      ...badges[match],
      variant: badges[match].variant ?? item.badge?.variant,
    };
  }
  return undefined;
}

export function AdminSidebar({
  badges = {},
  collapsed: controlledCollapsed,
  onCollapsedChange,
  className,
}: AdminSidebarProps) {
  const pathname = usePathname() ?? '/admin';
  const [internalCollapsed, setInternalCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [mounted, setMounted] = useState(false);
  const [expandedSections, setExpandedSections] = useState<string[]>([]);

  const isControlled = controlledCollapsed !== undefined;
  const collapsed = isControlled ? controlledCollapsed : internalCollapsed;

  // Load persisted state on mount
  useEffect(() => {
    setMounted(true);
    if (!isControlled) {
      const stored = localStorage.getItem(ADMIN_SIDEBAR_COLLAPSED_KEY);
      if (stored === 'true') {
        setInternalCollapsed(true);
      }
    }

    // Load expanded sections from storage or derive from pathname
    const storedSections = localStorage.getItem(ADMIN_SIDEBAR_SECTIONS_KEY);
    if (storedSections) {
      try {
        setExpandedSections(JSON.parse(storedSections) as string[]);
      } catch {
        setExpandedSections(getExpandedSections(pathname));
      }
    } else {
      setExpandedSections(getExpandedSections(pathname));
    }
  }, [isControlled, pathname]);

  // Update expanded sections when pathname changes (expand section containing current route)
  useEffect(() => {
    if (mounted) {
      const activeSection = ADMIN_NAVIGATION.find((section) =>
        section.items.some((item) => isAdminNavItemActive(item, pathname))
      );
      if (activeSection && !expandedSections.includes(activeSection.id)) {
        setExpandedSections((prev) => [...prev, activeSection.id]);
      }
    }
  }, [pathname, mounted, expandedSections]);

  const toggleCollapsed = useCallback(() => {
    const newValue = !collapsed;
    if (isControlled) {
      onCollapsedChange?.(newValue);
    } else {
      setInternalCollapsed(newValue);
      localStorage.setItem(ADMIN_SIDEBAR_COLLAPSED_KEY, String(newValue));
    }
  }, [collapsed, isControlled, onCollapsedChange]);

  const toggleSection = useCallback((sectionId: string) => {
    setExpandedSections((prev) => {
      const newExpanded = prev.includes(sectionId)
        ? prev.filter((id) => id !== sectionId)
        : [...prev, sectionId];
      localStorage.setItem(ADMIN_SIDEBAR_SECTIONS_KEY, JSON.stringify(newExpanded));
      return newExpanded;
    });
  }, []);

  const closeMobile = useCallback(() => {
    setMobileOpen(false);
  }, []);

  // Memoize navigation rendering
  const renderNavigation = useMemo(
    () => (isMobile: boolean) => (
      <nav aria-label="Admin navigation" className="flex flex-col h-full">
        <div className="flex-1 space-y-1 overflow-y-auto">
          {ADMIN_NAVIGATION.map((section) => (
            <NavSection
              key={section.id}
              section={section}
              pathname={pathname}
              collapsed={isMobile ? false : collapsed}
              isExpanded={expandedSections.includes(section.id)}
              onToggle={() => toggleSection(section.id)}
              badges={badges}
              onNavClick={isMobile ? closeMobile : undefined}
            />
          ))}
        </div>
      </nav>
    ),
    [pathname, collapsed, expandedSections, badges, toggleSection, closeMobile]
  );

  // Desktop sidebar
  const desktopSidebar = (
    <aside
      className={cn(
        'hidden lg:flex lg:flex-col transition-all duration-300 h-full',
        // Light mode: Glass morphism sidebar
        'bg-sidebar/95 backdrop-blur-[12px]',
        // Dark mode: Solid sidebar
        'dark:bg-sidebar dark:backdrop-blur-none',
        'border-r border-border/50 dark:border-border/30',
        collapsed ? 'lg:w-16' : 'lg:w-64',
        className
      )}
      data-testid="admin-sidebar-desktop"
    >
      {/* Collapse toggle */}
      <div className={cn('p-2 shrink-0', collapsed ? 'flex justify-center' : 'flex justify-end')}>
        <Button
          variant="ghost"
          size="icon"
          onClick={toggleCollapsed}
          aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          className="h-8 w-8"
          data-testid="admin-sidebar-toggle"
        >
          {collapsed ? (
            <ChevronRightIcon className="h-4 w-4" />
          ) : (
            <ChevronLeftIcon className="h-4 w-4" />
          )}
        </Button>
      </div>

      {/* Navigation */}
      <div className="flex-1 px-2 pb-4 overflow-hidden">
        <TooltipProvider>{renderNavigation(false)}</TooltipProvider>
      </div>
    </aside>
  );

  // Mobile sidebar (Sheet)
  const mobileSidebar = (
    <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
      <SheetTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="lg:hidden"
          aria-label="Open admin menu"
          data-testid="admin-sidebar-mobile-trigger"
        >
          <MenuIcon className="h-5 w-5" />
        </Button>
      </SheetTrigger>
      <SheetContent
        side="left"
        className="w-72 p-0 flex flex-col"
        data-testid="admin-sidebar-mobile"
      >
        <SheetTitle className="px-4 py-3 border-b border-border/50 dark:border-border/30 shrink-0">
          Admin Menu
        </SheetTitle>
        <SheetDescription className="sr-only">
          Navigation menu for admin pages
        </SheetDescription>
        <div className="flex-1 p-2 overflow-y-auto">
          <TooltipProvider>{renderNavigation(true)}</TooltipProvider>
        </div>
      </SheetContent>
    </Sheet>
  );

  // Prevent hydration mismatch - show skeleton on first render
  if (!mounted) {
    return (
      <>
        <aside
          className={cn(
            'hidden lg:flex lg:flex-col border-r border-border/50 dark:border-border/30 bg-sidebar/95 backdrop-blur-[12px] dark:bg-sidebar dark:backdrop-blur-none lg:w-64',
            className
          )}
        >
          <div className="flex-1 px-2 py-4">
            <div className="animate-pulse space-y-2">
              {Array.from({ length: 8 }).map((_, i) => (
                <div
                  key={i}
                  className="h-10 bg-muted/30 rounded-[0.625rem]"
                />
              ))}
            </div>
          </div>
        </aside>
        <Button
          variant="ghost"
          size="icon"
          className="lg:hidden"
          aria-label="Open navigation menu"
        >
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

/**
 * Mobile trigger button for use in layouts
 */
export function AdminSidebarMobileTrigger() {
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) {
    return (
      <Button variant="ghost" size="icon" className="lg:hidden">
        <MenuIcon className="h-5 w-5" />
      </Button>
    );
  }

  // The actual trigger is rendered by AdminSidebar component
  return null;
}

export default AdminSidebar;
