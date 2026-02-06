/**
 * MobileBottomNav Component
 * Issue #3689 - Layout Base & Navigation System
 *
 * Bottom navigation bar visible only on mobile (< lg breakpoint).
 * Shows the 7 enterprise sections as icon-only buttons.
 */

'use client';

import React from 'react';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { ENTERPRISE_SECTIONS } from '@/config/enterprise-navigation';
import { cn } from '@/lib/utils';

export function MobileBottomNav() {
  const pathname = usePathname() ?? '/admin';

  return (
    <nav
      className={cn(
        'fixed bottom-0 inset-x-0 z-50 lg:hidden',
        'bg-white/95 dark:bg-zinc-900/95 backdrop-blur-lg',
        'border-t border-zinc-200/50 dark:border-zinc-700/50',
        'safe-bottom'
      )}
      aria-label="Enterprise mobile navigation"
      data-testid="enterprise-mobile-bottom-nav"
    >
      <div className="flex items-center justify-around px-1 py-1.5">
        {ENTERPRISE_SECTIONS.map((section) => {
          const Icon = section.icon;
          const href = `/admin/${section.route}`;
          const isActive = pathname === `/admin/${section.route}` || pathname.startsWith(`/admin/${section.route}/`);

          return (
            <Link
              key={section.id}
              href={href}
              className={cn(
                'flex flex-col items-center gap-0.5 px-2 py-1.5 rounded-lg transition-colors min-w-0',
                isActive
                  ? 'text-amber-600 dark:text-amber-400'
                  : 'text-zinc-500 dark:text-zinc-400'
              )}
              aria-current={isActive ? 'page' : undefined}
              data-testid={`enterprise-mobile-nav-${section.id}`}
            >
              <Icon className="h-5 w-5 shrink-0" aria-hidden="true" />
              <span className="text-[10px] font-medium truncate max-w-[56px]">
                {section.label}
              </span>
            </Link>
          );
        })}
      </div>
    </nav>
  );
}

export default MobileBottomNav;
