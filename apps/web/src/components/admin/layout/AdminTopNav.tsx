'use client';

import { useCallback, useEffect, useState } from 'react';

import { Moon, Sun, UserIcon, LogOut, ChevronDown, Search } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useTheme } from 'next-themes';

import { CommandPalette } from '@/components/layout/CommandPalette';
import { Badge } from '@/components/ui/data-display/badge';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import {
  DASHBOARD_SECTIONS,
  isSectionActive,
  type DashboardSection,
} from '@/config/admin-dashboard-navigation';
import { useCommandPalette } from '@/hooks/useCommandPalette';
import { cn } from '@/lib/utils';

export interface AdminTopNavProps {
  /** Badge counts keyed by badgeKey */
  badges?: Record<string, number>;
  /** Current user display name */
  userName?: string;
  /** Current user email */
  userEmail?: string;
}

export function AdminTopNav({ badges = {}, userName, userEmail }: AdminTopNavProps) {
  const pathname = usePathname() ?? '/admin/overview';
  const { theme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);
  const commandPalette = useCommandPalette();

  useEffect(() => {
    setMounted(true);
  }, []);

  const toggleTheme = useCallback(() => {
    setTheme(theme === 'dark' ? 'light' : 'dark');
  }, [theme, setTheme]);

  const sections = DASHBOARD_SECTIONS;

  /** Count total badges for a section */
  const getSectionBadgeCount = useCallback(
    (section: DashboardSection): number => {
      return section.sidebarItems.reduce((total, item) => {
        if (item.badgeKey && badges[item.badgeKey]) {
          return total + badges[item.badgeKey];
        }
        return total;
      }, 0);
    },
    [badges]
  );

  return (
    <header
      className={cn(
        'sticky top-0 z-40 w-full',
        'bg-white/80 dark:bg-zinc-900/80',
        'backdrop-blur-xl',
        'border-b border-slate-200/60 dark:border-zinc-700/60',
        'shadow-[0_1px_3px_rgba(0,0,0,0.04)]'
      )}
      data-testid="admin-top-nav"
    >
      <div className="flex items-center h-14 px-4 lg:px-6">
        {/* Logo / Brand */}
        <Link href="/admin/overview" className="flex items-center gap-2 mr-6 shrink-0">
          <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-amber-500 to-orange-600 flex items-center justify-center">
            <span className="text-white font-quicksand font-bold text-sm">M</span>
          </div>
          <span className="hidden sm:inline font-quicksand font-bold text-lg text-foreground">
            Admin
          </span>
        </Link>

        {/* Section Tabs */}
        <nav className="hidden md:flex items-center gap-1 flex-1" aria-label="Admin sections">
          {sections.map(section => {
            const isActive = isSectionActive(section, pathname);
            const badgeCount = getSectionBadgeCount(section);
            const Icon = section.icon;
            if (!Icon) return null;

            return (
              <Link
                key={section.id}
                href={section.baseRoute}
                className={cn(
                  'flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium transition-all duration-200',
                  isActive
                    ? 'bg-amber-100/80 dark:bg-amber-900/30 text-amber-900 dark:text-amber-200'
                    : 'text-muted-foreground hover:bg-slate-100/80 dark:hover:bg-zinc-800/80 hover:text-foreground'
                )}
                data-testid={`admin-topnav-${section.id}`}
              >
                <Icon className="h-4 w-4" />
                <span className="hidden lg:inline">{section.label}</span>
                {badgeCount > 0 && (
                  <Badge variant="destructive" className="h-5 min-w-[20px] text-xs px-1.5">
                    {badgeCount > 99 ? '99+' : badgeCount}
                  </Badge>
                )}
              </Link>
            );
          })}
        </nav>

        {/* Right side actions */}
        <div className="flex items-center gap-2 ml-auto">
          {/* Search trigger → opens CommandPalette */}
          <button
            onClick={commandPalette.toggle}
            className="hidden md:flex items-center gap-2 rounded-md border border-border/40 bg-muted/50 px-3 py-1.5 text-sm text-muted-foreground hover:bg-accent transition-colors"
          >
            <Search className="h-4 w-4" />
            <span>Search...</span>
            <kbd className="ml-4 text-xs border border-border rounded px-1.5 py-0.5">⌘K</kbd>
          </button>
          <CommandPalette
            isOpen={commandPalette.isOpen}
            onClose={commandPalette.close}
            dataSources={{}}
          />

          {/* Theme toggle */}
          {mounted && (
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={toggleTheme}
                  className="h-8 w-8"
                  aria-label={`Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`}
                  data-testid="admin-theme-toggle"
                >
                  {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
                </Button>
              </TooltipTrigger>
              <TooltipContent>{theme === 'dark' ? 'Light mode' : 'Dark mode'}</TooltipContent>
            </Tooltip>
          )}

          {/* User menu */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" className="h-8 gap-2 px-2" data-testid="admin-user-menu">
                <div className="h-6 w-6 rounded-full bg-gradient-to-br from-amber-400 to-orange-500 flex items-center justify-center">
                  <UserIcon className="h-3 w-3 text-white" />
                </div>
                <span className="hidden sm:inline text-sm font-medium truncate max-w-[120px]">
                  {userName ?? 'Admin'}
                </span>
                <ChevronDown className="h-3 w-3 text-muted-foreground" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-56">
              {userEmail && (
                <>
                  <div className="px-2 py-1.5">
                    <p className="text-sm font-medium">{userName ?? 'Admin'}</p>
                    <p className="text-xs text-muted-foreground">{userEmail}</p>
                  </div>
                  <DropdownMenuSeparator />
                </>
              )}
              <DropdownMenuItem asChild>
                <Link href="/dashboard">
                  <UserIcon className="h-4 w-4 mr-2" />
                  Back to App
                </Link>
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem asChild>
                <Link href="/login" className="text-destructive">
                  <LogOut className="h-4 w-4 mr-2" />
                  Sign Out
                </Link>
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>
    </header>
  );
}
