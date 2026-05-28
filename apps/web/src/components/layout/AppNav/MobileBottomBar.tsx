'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { BOTTOM_TAB_LABEL_OVERRIDES } from '@/config/navigation';
import { useNavigationItems } from '@/hooks/useNavigationItems';
import { cn } from '@/lib/utils';

import { isImmersiveRoute } from './immersive-routes';

// Re-exported for the existing MobileBottomBar test + the AppNav barrel.
export { isImmersiveRoute };

interface MobileBottomBarProps {
  className?: string;
}

/**
 * Mobile bottom tab bar (sp4-dashboard graphic): the fixed primary tabs.
 * Hidden at `md`+ and on immersive in-session routes.
 */
export function MobileBottomBar({ className }: MobileBottomBarProps) {
  const pathname = usePathname();
  const { bottomTabItems, isItemActive } = useNavigationItems();

  if (isImmersiveRoute(pathname) || bottomTabItems.length === 0) {
    return null;
  }

  return (
    <nav
      data-testid="mobile-bottom-bar"
      aria-label="Navigazione principale"
      className={cn(
        'fixed inset-x-0 bottom-0 z-40 flex border-t px-2 pb-3 pt-2 md:hidden',
        className
      )}
      style={{
        backdropFilter: 'blur(14px)',
        WebkitBackdropFilter: 'blur(14px)',
        background: 'var(--glass-bg)',
        borderColor: 'var(--border)',
      }}
    >
      {bottomTabItems.map(item => {
        const Icon = item.icon;
        const active = isItemActive(item, pathname);
        const label = BOTTOM_TAB_LABEL_OVERRIDES[item.id] ?? item.label;
        return (
          <Link
            key={item.id}
            href={item.href}
            data-testid={`bottom-tab-${item.id}`}
            aria-current={active ? 'page' : undefined}
            className={cn(
              'flex min-h-[44px] flex-1 flex-col items-center justify-center gap-0.5 rounded-md py-1 text-[10px] font-bold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              active ? 'text-entity-game-text' : 'text-muted-foreground hover:text-foreground'
            )}
          >
            <Icon className={cn('h-[22px] w-[22px] transition-transform', active && 'scale-105')} />
            <span>{label}</span>
          </Link>
        );
      })}
    </nav>
  );
}
