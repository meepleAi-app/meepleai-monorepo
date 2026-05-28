'use client';

import { ChevronDown, Menu } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { UserMenuDropdown } from '@/components/layout/UserMenuDropdown';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { useNavigationItems } from '@/hooks/useNavigationItems';
import { cn } from '@/lib/utils';

interface AppTopBarProps {
  /** Admin shell: hide the user-shell primary links (admin nav lives in the sidebar) + show Admin badge. */
  adminMode?: boolean;
  /** Admin shell: opens the admin drawer on the md–lg range (where the sidebar is hidden). */
  onMenuClick?: () => void;
  className?: string;
}

/**
 * Desktop/tablet top bar (sp4-dashboard graphic).
 * Logo + contextual primary links (role-filtered) + "Altro" overflow + user pill.
 * Hidden below `md` — mobile uses {@link MobileTopBar} + {@link MobileBottomBar}.
 */
export function AppTopBar({ adminMode, onMenuClick, className }: AppTopBarProps) {
  const pathname = usePathname();
  const { topBarItems, desktopOverflowItems, isItemActive } = useNavigationItems();

  return (
    <header
      data-testid="app-top-bar"
      className={cn(
        'sticky top-0 z-40 hidden h-14 items-center gap-4 border-b px-5 md:flex',
        className
      )}
      style={{
        backdropFilter: 'blur(12px)',
        WebkitBackdropFilter: 'blur(12px)',
        background: 'var(--glass-bg)',
        borderColor: 'var(--border)',
      }}
    >
      {/* Admin: hamburger for the md–lg range where the sidebar is hidden */}
      {adminMode && onMenuClick && (
        <Button
          variant="ghost"
          size="icon"
          aria-label="Menu navigazione"
          onClick={onMenuClick}
          className="shrink-0 lg:hidden"
        >
          <Menu className="h-5 w-5" />
        </Button>
      )}

      {/* Logo */}
      <Link href="/dashboard" aria-label="MeepleAI" className="flex shrink-0 items-center gap-2">
        <span
          aria-hidden="true"
          className="flex h-7 w-7 items-center justify-center rounded-[10px] bg-[linear-gradient(135deg,hsl(var(--c-game)),hsl(var(--c-event)))] text-sm font-bold text-white"
          style={{ fontFamily: 'var(--f-display)' }}
        >
          M
        </span>
        <span className="font-[family-name:var(--font-quicksand)] text-sm font-bold">MeepleAI</span>
        {adminMode && (
          <span className="ml-1 rounded bg-destructive/10 px-1.5 py-0.5 text-[10px] font-semibold leading-none text-destructive">
            Admin
          </span>
        )}
      </Link>

      {/* Primary links + overflow (user shell only) */}
      {!adminMode && (
        <nav aria-label="Navigazione principale" className="flex items-center gap-0.5">
          {topBarItems.map(item => {
            const active = isItemActive(item, pathname);
            return (
              <Link
                key={item.id}
                href={item.href}
                data-testid={item.testId}
                aria-current={active ? 'page' : undefined}
                className={cn(
                  'rounded-[10px] px-3 py-1.5 text-[13px] font-bold transition-colors',
                  active
                    ? 'bg-entity-game/10 text-entity-game-text'
                    : 'text-foreground/70 hover:bg-muted hover:text-foreground'
                )}
              >
                {item.label}
              </Link>
            );
          })}

          {desktopOverflowItems.length > 0 && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button
                  type="button"
                  data-testid="app-top-bar-overflow"
                  className="flex items-center gap-1 rounded-[10px] px-3 py-1.5 text-[13px] font-bold text-foreground/70 transition-colors hover:bg-muted hover:text-foreground"
                >
                  Altro
                  <ChevronDown className="h-3.5 w-3.5" />
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="start" className="w-56">
                {desktopOverflowItems.map(item => {
                  const Icon = item.icon;
                  return (
                    <DropdownMenuItem key={item.id} asChild>
                      <Link
                        href={item.href}
                        data-testid={item.testId}
                        className="flex cursor-pointer items-center gap-2"
                      >
                        <Icon className="h-4 w-4" />
                        <span>{item.label}</span>
                      </Link>
                    </DropdownMenuItem>
                  );
                })}
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </nav>
      )}

      <div className="flex-1" />

      <UserMenuDropdown variant="pill" />
    </header>
  );
}
