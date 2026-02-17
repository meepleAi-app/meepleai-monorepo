'use client';

import { useCallback, useEffect, useState } from 'react';

import { ChevronLeftIcon, ChevronRightIcon } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import {
  ADMIN_DASHBOARD_SIDEBAR_COLLAPSED_KEY,
  isSidebarItemActive,
  type DashboardSection,
  type DashboardSidebarItem,
} from '@/config/admin-dashboard-navigation';
import { cn } from '@/lib/utils';

export interface AdminContextualSidebarProps {
  /** Current active section */
  section: DashboardSection | undefined;
  /** Badge counts keyed by badgeKey */
  badges?: Record<string, number>;
  /** Additional className */
  className?: string;
}

function SidebarLink({
  item,
  isActive,
  collapsed,
  badgeCount,
}: {
  item: DashboardSidebarItem;
  isActive: boolean;
  collapsed: boolean;
  badgeCount?: number;
}) {
  const Icon = item.icon;

  const content = (
    <Link
      href={item.href}
      className={cn(
        'flex items-center gap-3 rounded-xl transition-all duration-200 font-medium text-sm',
        collapsed ? 'justify-center p-2.5' : 'px-3 py-2.5',
        isActive
          ? 'bg-amber-100/80 dark:bg-amber-900/30 text-amber-900 dark:text-amber-200 shadow-sm'
          : 'text-muted-foreground hover:bg-slate-100/80 dark:hover:bg-zinc-800/60 hover:text-foreground'
      )}
      aria-current={isActive ? 'page' : undefined}
      data-testid={`admin-sidebar-link-${item.href.split('/').pop()}`}
    >
      <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
      {!collapsed && (
        <>
          <span className="flex-1 truncate">{item.label}</span>
          {badgeCount != null && badgeCount > 0 && (
            <Badge variant="destructive" className="ml-auto text-xs h-5 min-w-[20px] px-1.5">
              {badgeCount > 99 ? '99+' : badgeCount}
            </Badge>
          )}
        </>
      )}
    </Link>
  );

  if (collapsed) {
    return (
      <Tooltip delayDuration={0}>
        <TooltipTrigger asChild>{content}</TooltipTrigger>
        <TooltipContent side="right" className="flex items-center gap-2">
          {item.label}
          {badgeCount != null && badgeCount > 0 && (
            <Badge variant="destructive" className="text-xs">
              {badgeCount}
            </Badge>
          )}
        </TooltipContent>
      </Tooltip>
    );
  }

  return content;
}

export function AdminContextualSidebar({
  section,
  badges = {},
  className,
}: AdminContextualSidebarProps) {
  const pathname = usePathname() ?? '';
  const [collapsed, setCollapsed] = useState(false);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
    const stored = localStorage.getItem(ADMIN_DASHBOARD_SIDEBAR_COLLAPSED_KEY);
    if (stored === 'true') {
      setCollapsed(true);
    }
  }, []);

  const toggleCollapsed = useCallback(() => {
    setCollapsed((prev) => {
      const next = !prev;
      localStorage.setItem(ADMIN_DASHBOARD_SIDEBAR_COLLAPSED_KEY, String(next));
      return next;
    });
  }, []);

  if (!section) return null;

  const SectionIcon = section.icon;

  // SSR placeholder
  if (!mounted) {
    return (
      <aside
        className={cn(
          'hidden lg:flex lg:flex-col w-60 shrink-0',
          'bg-white/60 dark:bg-zinc-900/60 backdrop-blur-md',
          'border-r border-slate-200/60 dark:border-zinc-700/40',
          className
        )}
      >
        <div className="flex-1 p-3">
          <div className="animate-pulse space-y-2">
            {Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="h-10 bg-muted/20 rounded-xl" />
            ))}
          </div>
        </div>
      </aside>
    );
  }

  return (
    <aside
      className={cn(
        'hidden lg:flex lg:flex-col shrink-0 transition-all duration-300',
        collapsed ? 'w-16' : 'w-60',
        'bg-white/60 dark:bg-zinc-900/60',
        'backdrop-blur-md dark:backdrop-blur-md',
        'border-r border-slate-200/60 dark:border-zinc-700/40',
        className
      )}
      data-testid="admin-contextual-sidebar"
    >
      {/* Section header */}
      <div
        className={cn(
          'flex items-center border-b border-slate-200/60 dark:border-zinc-700/40 shrink-0',
          collapsed ? 'justify-center p-3' : 'px-4 py-3'
        )}
      >
        {collapsed ? (
          <Tooltip delayDuration={0}>
            <TooltipTrigger asChild>
              <div className="flex items-center justify-center">
                <SectionIcon className="h-5 w-5 text-amber-700 dark:text-amber-400" />
              </div>
            </TooltipTrigger>
            <TooltipContent side="right">
              <span className="font-semibold">{section.label}</span>
              <p className="text-xs text-muted-foreground">{section.description}</p>
            </TooltipContent>
          </Tooltip>
        ) : (
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <SectionIcon className="h-4 w-4 text-amber-700 dark:text-amber-400 shrink-0" />
              <h2 className="font-quicksand font-bold text-sm text-foreground truncate">
                {section.label}
              </h2>
            </div>
            <p className="text-xs text-muted-foreground mt-0.5 truncate">
              {section.description}
            </p>
          </div>
        )}
      </div>

      {/* Navigation items (TooltipProvider from AdminShell wraps this) */}
      <nav className="flex-1 p-2 space-y-1 overflow-y-auto" aria-label={`${section.label} navigation`}>
        {section.sidebarItems.map((item) => (
          <SidebarLink
            key={item.href}
            item={item}
            isActive={isSidebarItemActive(item, pathname)}
            collapsed={collapsed}
            badgeCount={item.badgeKey ? badges[item.badgeKey] : undefined}
          />
        ))}
      </nav>

      {/* Collapse toggle */}
      <div
        className={cn(
          'border-t border-slate-200/60 dark:border-zinc-700/40 p-2 shrink-0',
          collapsed ? 'flex justify-center' : 'flex justify-end'
        )}
      >
        <Button
          variant="ghost"
          size="icon"
          onClick={toggleCollapsed}
          aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          className="h-7 w-7"
          data-testid="admin-sidebar-collapse-toggle"
        >
          {collapsed ? (
            <ChevronRightIcon className="h-4 w-4" />
          ) : (
            <ChevronLeftIcon className="h-4 w-4" />
          )}
        </Button>
      </div>
    </aside>
  );
}
