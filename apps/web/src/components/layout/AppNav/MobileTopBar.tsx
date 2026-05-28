'use client';

import { Bell, Menu } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { Button } from '@/components/ui/primitives/button';
import { useNavigationItems } from '@/hooks/useNavigationItems';
import { cn } from '@/lib/utils';
import { selectUnreadCount, useNotificationStore } from '@/stores/notification/store';

interface MobileTopBarProps {
  /** Open the navigation drawer (☰). */
  onHamburgerClick: () => void;
  /** Admin shell: forces the title to "Admin". */
  adminMode?: boolean;
  className?: string;
}

/**
 * Mobile top bar (sp4-dashboard graphic): ☰ menu + contextual page title + 🔔.
 * Hidden at `md` and above — desktop uses {@link AppTopBar}.
 */
export function MobileTopBar({ onHamburgerClick, adminMode, className }: MobileTopBarProps) {
  const pathname = usePathname();
  const { topBarItems, bottomTabItems, mobileDrawerItems, isItemActive } = useNavigationItems();
  const unreadCount = useNotificationStore(selectUnreadCount);

  // Contextual title = label of the active nav item across all surfaces.
  const allItems = [...topBarItems, ...bottomTabItems, ...mobileDrawerItems];
  const activeItem = allItems.find(item => isItemActive(item, pathname));
  const title = adminMode ? 'Admin' : (activeItem?.label ?? 'MeepleAI');

  return (
    <header
      data-testid="mobile-top-bar"
      className={cn(
        'sticky top-0 z-40 flex h-14 items-center gap-2 border-b px-3 md:hidden',
        className
      )}
      style={{
        backdropFilter: 'blur(12px)',
        WebkitBackdropFilter: 'blur(12px)',
        background: 'var(--glass-bg)',
        borderColor: 'var(--border)',
      }}
    >
      <Button
        variant="ghost"
        size="icon"
        aria-label="Menu navigazione"
        onClick={onHamburgerClick}
        className="shrink-0"
      >
        <Menu className="h-5 w-5" />
      </Button>

      <div className="flex-1 truncate text-center font-[family-name:var(--font-quicksand)] text-sm font-bold">
        {title}
      </div>

      <Link
        href="/notifications"
        aria-label="Notifiche"
        data-testid="mobile-top-bar-notifications"
        className="relative inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-md text-foreground transition-colors hover:bg-muted"
      >
        <Bell className="h-5 w-5" />
        {unreadCount > 0 && (
          <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-[1rem] items-center justify-center rounded-full bg-destructive px-1 text-[9px] font-bold text-destructive-foreground">
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </Link>
    </header>
  );
}
