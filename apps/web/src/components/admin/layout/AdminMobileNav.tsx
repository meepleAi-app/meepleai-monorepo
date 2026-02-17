'use client';

import { useState } from 'react';

import { MenuIcon } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Sheet,
  SheetContent,
  SheetTitle,
  SheetDescription,
  SheetTrigger,
} from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import {
  DASHBOARD_SECTIONS,
  isSectionActive,
  isSidebarItemActive,
  type DashboardSection,
} from '@/config/admin-dashboard-navigation';
import { cn } from '@/lib/utils';

export interface AdminMobileNavProps {
  /** Current active section */
  section: DashboardSection | undefined;
  /** Badge counts keyed by badgeKey */
  badges?: Record<string, number>;
}

export function AdminMobileNav({ section, badges = {} }: AdminMobileNavProps) {
  const pathname = usePathname() ?? '';
  const [open, setOpen] = useState(false);

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="md:hidden h-8 w-8"
          aria-label="Open navigation menu"
          data-testid="admin-mobile-nav-trigger"
        >
          <MenuIcon className="h-5 w-5" />
        </Button>
      </SheetTrigger>
      <SheetContent
        side="left"
        className={cn(
          'w-80 p-0 flex flex-col',
          'bg-white/95 dark:bg-zinc-900/95 backdrop-blur-xl'
        )}
        data-testid="admin-mobile-nav"
      >
        <SheetTitle className="px-4 py-3 border-b border-slate-200/60 dark:border-zinc-700/40 shrink-0">
          <div className="flex items-center gap-2">
            <div className="h-7 w-7 rounded-lg bg-gradient-to-br from-amber-500 to-orange-600 flex items-center justify-center">
              <span className="text-white font-quicksand font-bold text-xs">M</span>
            </div>
            <span className="font-quicksand font-bold text-base">Admin Panel</span>
          </div>
        </SheetTitle>
        <SheetDescription className="sr-only">
          Navigation menu for admin dashboard sections
        </SheetDescription>

        <div className="flex-1 overflow-y-auto">
          {/* Section tabs */}
          <div className="p-3 border-b border-slate-200/60 dark:border-zinc-700/40">
            <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wider px-2 mb-2">
              Sections
            </p>
            <div className="space-y-1">
              {DASHBOARD_SECTIONS.map((s) => {
                const isActive = isSectionActive(s, pathname);
                const Icon = s.icon;
                const badgeCount = s.sidebarItems.reduce((total, item) => {
                  if (item.badgeKey && badges[item.badgeKey]) {
                    return total + badges[item.badgeKey];
                  }
                  return total;
                }, 0);

                return (
                  <Link
                    key={s.id}
                    href={s.baseRoute}
                    onClick={() => setOpen(false)}
                    className={cn(
                      'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all',
                      isActive
                        ? 'bg-amber-100/80 dark:bg-amber-900/30 text-amber-900 dark:text-amber-200'
                        : 'text-muted-foreground hover:bg-slate-100/80 dark:hover:bg-zinc-800/60'
                    )}
                  >
                    <Icon className="h-4 w-4 shrink-0" />
                    <span className="flex-1">{s.label}</span>
                    {badgeCount > 0 && (
                      <Badge variant="destructive" className="text-xs h-5 px-1.5">
                        {badgeCount > 99 ? '99+' : badgeCount}
                      </Badge>
                    )}
                  </Link>
                );
              })}
            </div>
          </div>

          {/* Current section sidebar items */}
          {section && (
            <div className="p-3">
              <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wider px-2 mb-2">
                {section.label}
              </p>
              <div className="space-y-1">
                {section.sidebarItems.map((item) => {
                  const isActive = isSidebarItemActive(item, pathname);
                  const Icon = item.icon;
                  const badgeCount = item.badgeKey ? badges[item.badgeKey] : undefined;

                  return (
                    <Link
                      key={item.href}
                      href={item.href}
                      onClick={() => setOpen(false)}
                      className={cn(
                        'flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all',
                        isActive
                          ? 'bg-amber-100/80 dark:bg-amber-900/30 text-amber-900 dark:text-amber-200'
                          : 'text-muted-foreground hover:bg-slate-100/80 dark:hover:bg-zinc-800/60'
                      )}
                    >
                      <Icon className="h-4 w-4 shrink-0" />
                      <span className="flex-1">{item.label}</span>
                      {badgeCount != null && badgeCount > 0 && (
                        <Badge variant="destructive" className="text-xs h-5 px-1.5">
                          {badgeCount > 99 ? '99+' : badgeCount}
                        </Badge>
                      )}
                    </Link>
                  );
                })}
              </div>
            </div>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
