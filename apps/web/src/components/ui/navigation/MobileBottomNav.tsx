'use client';

import React from 'react';

import { Home, Library, MessageCircle, User, Plus } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

interface NavItem {
  href: string;
  label: string;
  icon: React.ElementType;
  matchPaths: string[];
}

const navItems: NavItem[] = [
  { href: '/dashboard', label: 'Home', icon: Home, matchPaths: ['/dashboard'] },
  { href: '/library', label: 'Libreria', icon: Library, matchPaths: ['/library'] },
  { href: '/chat', label: 'Chat', icon: MessageCircle, matchPaths: ['/chat'] },
  { href: '/profile', label: 'Profilo', icon: User, matchPaths: ['/profile', '/settings'] },
];

export interface MobileBottomNavProps {
  hidden?: boolean;
  onQuickAction?: () => void;
}

export function MobileBottomNav({ hidden = false, onQuickAction }: MobileBottomNavProps) {
  const pathname = usePathname();

  if (hidden) return null;

  const isActive = (item: NavItem) => item.matchPaths.some(p => pathname.startsWith(p));

  return (
    <nav
      aria-label="Bottom navigation"
      className={cn(
        'fixed bottom-0 inset-x-0 z-40 lg:hidden',
        'flex items-center justify-around',
        'h-[var(--size-bottom-nav)]',
        'bg-[var(--gaming-bg-elevated)] border-t border-[var(--gaming-border-glass)]',
        'safe-area-pb'
      )}
    >
      {navItems.slice(0, 2).map(item => (
        <NavLink key={item.href} item={item} active={isActive(item)} />
      ))}

      <button
        type="button"
        aria-label="Azione rapida"
        onClick={onQuickAction}
        className={cn(
          'flex items-center justify-center',
          'h-12 w-12 -mt-4 rounded-full',
          'gradient-primary shadow-lg shadow-orange-500/20'
        )}
      >
        <Plus className="h-6 w-6 text-white" />
      </button>

      {navItems.slice(2).map(item => (
        <NavLink key={item.href} item={item} active={isActive(item)} />
      ))}
    </nav>
  );
}

function NavLink({ item, active }: { item: NavItem; active: boolean }) {
  const Icon = item.icon;
  return (
    <Link
      href={item.href}
      aria-current={active ? 'page' : undefined}
      className={cn(
        'flex flex-col items-center justify-center gap-0.5 px-3 py-1',
        'text-[10px] font-medium transition-colors',
        active ? 'text-[var(--gaming-text-accent)]' : 'text-[var(--gaming-text-secondary)]'
      )}
    >
      <Icon className="h-5 w-5" />
      <span>{item.label}</span>
    </Link>
  );
}
