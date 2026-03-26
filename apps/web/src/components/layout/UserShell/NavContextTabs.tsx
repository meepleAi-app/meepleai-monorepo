'use client';

import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';

import { getTabsForPathname } from '@/config/contextual-tabs';
import { cn } from '@/lib/utils';

export function NavContextTabs() {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const tabs = getTabsForPathname(pathname);

  if (!tabs) return null;

  const currentUrl = searchParams.toString() ? `${pathname}?${searchParams.toString()}` : pathname;

  return (
    <div
      className={cn(
        'h-9 border-b border-border/40',
        'bg-background/90 backdrop-blur-xl',
        'flex items-end px-4 gap-1 overflow-x-auto'
      )}
      role="tablist"
      aria-label="Section tabs"
      data-testid="nav-context-tabs"
    >
      {tabs.map(tab => {
        const isActive = currentUrl === tab.href || pathname === tab.href;
        return (
          <Link
            key={tab.href}
            href={tab.href}
            role="tab"
            aria-selected={isActive}
            className={cn(
              'px-3 py-1.5 text-xs whitespace-nowrap transition-colors duration-200',
              isActive
                ? 'font-semibold text-foreground border-b-2 border-primary -mb-px'
                : 'text-muted-foreground hover:text-foreground'
            )}
          >
            {tab.label}
          </Link>
        );
      })}
    </div>
  );
}

/** Returns true if the current pathname has contextual tabs */
export function useHasContextTabs(): boolean {
  const pathname = usePathname();
  return !!getTabsForPathname(pathname);
}
