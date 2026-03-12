'use client';

import {
  Award,
  BookOpen,
  Calendar,
  Gamepad2,
  LayoutDashboard,
  Library,
  MessageSquare,
  Play,
} from 'lucide-react';
import { usePathname } from 'next/navigation';

import { useCardRackState } from '@/hooks/useCardRackState';
import { cn } from '@/lib/utils';

import { CardRackItem } from './CardRackItem';

const NAV_ITEMS = [
  { href: '/dashboard', icon: LayoutDashboard, label: 'Dashboard' },
  { href: '/library', icon: Library, label: 'Libreria' },
  { href: '/games', icon: Gamepad2, label: 'Scopri' },
  { href: '/chat', icon: MessageSquare, label: 'Chat AI' },
  { href: '/sessions', icon: Play, label: 'Sessioni' },
  { href: '/game-nights', icon: Calendar, label: 'Serate' },
] as const;

const BOTTOM_ITEMS = [
  { href: '/agents', icon: BookOpen, label: 'Agenti' },
  { href: '/badges', icon: Award, label: 'Badge' },
] as const;

export interface CardRackProps {
  className?: string;
}

export function CardRack({ className }: CardRackProps) {
  const pathname = usePathname();
  const { isExpanded, onMouseEnter, onMouseLeave, rackRef } = useCardRackState();

  const isActive = (href: string) =>
    href === '/dashboard' ? pathname === '/dashboard' : pathname?.startsWith(href);

  return (
    <nav
      ref={rackRef as React.Ref<HTMLElement>}
      onMouseEnter={onMouseEnter}
      onMouseLeave={onMouseLeave}
      aria-label="Main sidebar navigation"
      data-testid="card-rack"
      className={cn(
        'hidden md:flex flex-col',
        'fixed left-0 z-40',
        'top-[var(--top-bar-height,48px)]',
        'h-[calc(100vh-var(--top-bar-height,48px))]',
        'bg-sidebar text-sidebar-foreground',
        'border-r border-sidebar-border',
        'transition-[width] duration-200 ease-in-out motion-reduce:transition-none',
        isExpanded ? 'w-[var(--card-rack-hover-width,240px)]' : 'w-[var(--card-rack-width,64px)]',
        className
      )}
    >
      <div className="flex-1 flex flex-col gap-0.5 px-2 py-3 overflow-y-auto">
        {NAV_ITEMS.map((item) => (
          <CardRackItem
            key={item.href}
            href={item.href}
            icon={item.icon}
            label={item.label}
            isActive={!!isActive(item.href)}
            isExpanded={isExpanded}
          />
        ))}
      </div>

      <hr className="mx-3 border-sidebar-border" />

      <div className="flex flex-col gap-0.5 px-2 py-3">
        {BOTTOM_ITEMS.map((item) => (
          <CardRackItem
            key={item.href}
            href={item.href}
            icon={item.icon}
            label={item.label}
            isActive={!!isActive(item.href)}
            isExpanded={isExpanded}
          />
        ))}
      </div>
    </nav>
  );
}
