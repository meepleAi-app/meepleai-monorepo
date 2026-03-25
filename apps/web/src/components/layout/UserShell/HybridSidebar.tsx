'use client';

/**
 * HybridSidebar — Collapsible sidebar navigation for the user layout.
 *
 * Features:
 * - 52px collapsed (icon-only), expands to 220px on hover/focus-within
 * - Three sections: Navigazione, AI Assistant, Collezioni
 * - Settings pinned to bottom
 * - Hidden on mobile, visible on desktop (lg:flex)
 * - motion-reduce:transition-none for accessibility
 */

import {
  BookOpen,
  Bot,
  Dice5,
  FileText,
  Heart,
  House,
  MessageCircle,
  Settings,
  Star,
  Target,
  Users,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';

import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

interface NavItem {
  label: string;
  icon: LucideIcon;
  href: string;
  /** Match pathname prefix for active state (defaults to href) */
  activeMatch?: string;
  /** Match search param key=value for active state refinement */
  activeSearchParam?: { key: string; value: string };
}

interface NavSection {
  title: string;
  items: NavItem[];
}

const NAV_SECTIONS: NavSection[] = [
  {
    title: 'Navigazione',
    items: [
      { label: 'Dashboard', icon: House, href: '/dashboard' },
      {
        label: 'Libreria',
        icon: BookOpen,
        href: '/library?tab=collection',
        activeMatch: '/library',
        activeSearchParam: { key: 'tab', value: 'collection' },
      },
      {
        label: 'Wishlist',
        icon: Heart,
        href: '/library?tab=wishlist',
        activeMatch: '/library',
        activeSearchParam: { key: 'tab', value: 'wishlist' },
      },
      { label: 'Sessioni', icon: Dice5, href: '/sessions', activeMatch: '/sessions' },
    ],
  },
  {
    title: 'AI Assistant',
    items: [
      { label: 'Chat RAG', icon: MessageCircle, href: '/chat', activeMatch: '/chat' },
      {
        label: 'Documenti',
        icon: FileText,
        href: '/library?tab=private',
        activeMatch: '/library',
        activeSearchParam: { key: 'tab', value: 'private' },
      },
      { label: 'Agenti', icon: Bot, href: '/agents', activeMatch: '/agents' },
    ],
  },
  {
    title: 'Collezioni',
    items: [
      { label: 'Preferiti', icon: Star, href: '#' },
      { label: 'Con amici', icon: Users, href: '#' },
      { label: 'Strategici', icon: Target, href: '#' },
    ],
  },
];

const BOTTOM_ITEMS: NavItem[] = [{ label: 'Impostazioni', icon: Settings, href: '/settings' }];

function useIsActive(item: NavItem): boolean {
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const matchPath = item.activeMatch ?? item.href.split('?')[0];

  // For items with search param matching, require both pathname and param match
  if (item.activeSearchParam) {
    const paramMatch =
      searchParams.get(item.activeSearchParam.key) === item.activeSearchParam.value;
    return pathname.startsWith(matchPath) && paramMatch;
  }

  // For dashboard, exact match only to avoid matching everything
  if (matchPath === '/dashboard') {
    return pathname === '/dashboard';
  }

  return pathname.startsWith(matchPath);
}

function SidebarLink({ item }: { item: NavItem }) {
  const isActive = useIsActive(item);
  const Icon = item.icon;

  return (
    <Link
      href={item.href}
      className={cn(
        'relative flex items-center gap-3 px-[9px] py-2 mx-1 rounded-lg',
        'transition-colors duration-200',
        isActive
          ? 'bg-primary text-white shadow-[0_2px_8px_rgba(180,80,0,0.3)]'
          : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'
      )}
      aria-label={item.label}
      aria-current={isActive ? 'page' : undefined}
    >
      <span className="flex items-center justify-center w-[34px] h-[34px] shrink-0">
        <Icon className="w-5 h-5" />
      </span>
      <span
        className={cn(
          'text-sm font-medium whitespace-nowrap',
          'opacity-0 group-hover:opacity-100 group-focus-within:opacity-100',
          'transition-opacity duration-300'
        )}
      >
        {item.label}
      </span>
    </Link>
  );
}

function SectionLabel({ title }: { title: string }) {
  return (
    <span
      className={cn(
        'text-[10px] font-semibold uppercase tracking-wider text-muted-foreground/60 px-4',
        'opacity-0 group-hover:opacity-100 group-focus-within:opacity-100',
        'transition-opacity duration-300'
      )}
    >
      {title}
    </span>
  );
}

export function HybridSidebar() {
  return (
    <nav
      className={cn(
        'hidden lg:flex flex-col',
        'fixed top-12 left-0 z-30',
        'h-[calc(100dvh-48px)]',
        'w-[52px] hover:w-[220px] focus-within:w-[220px]',
        'transition-[width] duration-300 ease-[cubic-bezier(0.4,0,0.2,1)]',
        'motion-reduce:transition-none',
        'border-r border-border/40 bg-background',
        'group overflow-hidden'
      )}
      role="navigation"
      aria-label="Navigazione principale"
      data-testid="hybrid-sidebar"
    >
      {/* Nav sections */}
      <div className="flex-1 flex flex-col gap-1 py-3">
        {NAV_SECTIONS.map((section, sectionIdx) => (
          <div key={section.title}>
            {sectionIdx > 0 && <div className="h-px bg-border/40 mx-3 my-2" />}
            <SectionLabel title={section.title} />
            <div className="flex flex-col gap-0.5 mt-1">
              {section.items.map(item => (
                <SidebarLink key={item.label} item={item} />
              ))}
            </div>
          </div>
        ))}
      </div>

      {/* Bottom items */}
      <div className="border-t border-border/40 py-2">
        {BOTTOM_ITEMS.map(item => (
          <SidebarLink key={item.label} item={item} />
        ))}
      </div>
    </nav>
  );
}
