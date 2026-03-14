'use client';

import React from 'react';

import { LayoutGrid, Dice5, MessageSquare, BookOpen, User } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

const TABS = [
  { label: 'Home', icon: LayoutGrid, href: '/hub', match: ['/hub'] },
  { label: 'Games', icon: Dice5, href: '/discover', match: ['/discover', '/games'] },
  { label: 'AI', icon: MessageSquare, href: '/agents', match: ['/agents', '/ask'] },
  { label: 'Library', icon: BookOpen, href: '/library', match: ['/library'] },
  { label: 'Settings', icon: User, href: '/profile', match: ['/profile', '/settings'] },
] as const;

export function MobileBottomBar() {
  const pathname = usePathname();

  return (
    <nav
      data-testid="mobile-bottom-bar"
      className="fixed bottom-0 left-0 right-0 z-40 md:hidden h-14 bg-card/90 backdrop-blur-md backdrop-saturate-150 border-t border-border/30 pb-[env(safe-area-inset-bottom)]"
    >
      <div className="flex items-center justify-around h-full">
        {TABS.map(({ label, icon: Icon, href, match }) => {
          const isActive = match.some(m => pathname.startsWith(m));
          return (
            <Link
              key={label}
              href={href}
              className={cn(
                'flex flex-col items-center justify-center gap-0.5 min-h-[44px] min-w-[44px] px-2 rounded-lg transition-colors',
                isActive ? 'text-foreground' : 'text-muted-foreground'
              )}
            >
              <Icon size={20} strokeWidth={isActive ? 2.5 : 1.5} />
              <span className={cn('text-[10px] leading-none', isActive && 'font-bold')}>
                {label}
              </span>
            </Link>
          );
        })}
      </div>
    </nav>
  );
}
