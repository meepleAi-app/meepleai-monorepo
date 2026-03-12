/**
 * SidebarContextNav - Issue #4936 + Issue #15 (Sidebar Strategy Rework)
 *
 * Split into two zones:
 *   1. Fixed Nav Zone — always-visible primary navigation links
 *   2. Contextual Zone — route-specific panels with animated transitions
 *
 * Context map (contextual zone):
 *   /dashboard  -> DashboardPanel (quick links + stats hints)
 *   /library/*  -> LibraryPanel  (collection filters)
 *   /games/*    -> GamesPanel    (catalog filters)
 *   default     -> null (no contextual panel)
 */

'use client';

import { AnimatePresence, motion } from 'framer-motion';
import {
  LayoutDashboard,
  BookOpen,
  Star,
  Archive,
  Clock,
  Heart,
  Gamepad2,
  Users,
  Calendar,
  Library,
  Layers,
  MessageSquare,
  Play,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { GamesFilterPanel } from '@/components/catalog/GamesFilterPanel';
import { useNavigation } from '@/context/NavigationContext';
import { cn } from '@/lib/utils';

import type { Variants } from 'framer-motion';

// ─── Animation config ────────────────────────────────────────────────────────

const PANEL_VARIANTS: Variants = {
  initial: { opacity: 0, x: -8 },
  animate: { opacity: 1, x: 0, transition: { duration: 0.18, ease: 'easeOut' } },
  exit: { opacity: 0, x: -6, transition: { duration: 0.12, ease: 'easeIn' } },
};

// ─── Shared link style ───────────────────────────────────────────────────────

function SidebarLink({
  href,
  icon: Icon,
  label,
  isActive,
  isCollapsed,
}: {
  href: string;
  icon: React.ElementType;
  label: string;
  isActive?: boolean;
  isCollapsed: boolean;
}) {
  return (
    <Link
      href={href}
      className={cn(
        'flex items-center gap-3 rounded-lg text-sm font-medium',
        'min-h-[44px] px-3 py-2',
        'transition-colors duration-150',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring focus-visible:ring-offset-1',
        isActive
          ? 'bg-[hsl(25_95%_45%/0.12)] text-[hsl(25_95%_42%)] font-semibold'
          : 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
        isCollapsed && 'justify-center px-2'
      )}
    >
      <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
      {!isCollapsed && <span className="truncate">{label}</span>}
    </Link>
  );
}

function SectionLabel({ label, isCollapsed }: { label: string; isCollapsed: boolean }) {
  if (isCollapsed) return <hr className="border-sidebar-border my-2" />;
  return (
    <p className="px-3 pt-3 pb-1 text-[10px] font-semibold uppercase tracking-wider text-sidebar-foreground/40">
      {label}
    </p>
  );
}

// ─── Fixed Nav Zone ──────────────────────────────────────────────────────────

function FixedNavZone({ isCollapsed }: { isCollapsed: boolean }) {
  const pathname = usePathname();

  const primaryLinks = [
    { href: '/dashboard', icon: LayoutDashboard, label: 'Dashboard' },
    { href: '/library', icon: Library, label: 'Libreria' },
    { href: '/games', icon: Gamepad2, label: 'Scopri' },
    { href: '/chat', icon: MessageSquare, label: 'Chat AI' },
    { href: '/sessions', icon: Play, label: 'Sessioni' },
    { href: '/players', icon: Users, label: 'Giocatori' },
  ];

  return (
    <nav
      className="flex flex-col gap-0.5 px-2 py-3"
      aria-label="Primary navigation"
      data-testid="sidebar-fixed-nav"
    >
      {primaryLinks.map(link => (
        <SidebarLink
          key={link.href}
          href={link.href}
          icon={link.icon}
          label={link.label}
          isActive={
            link.href === '/dashboard' ? pathname === '/dashboard' : pathname?.startsWith(link.href)
          }
          isCollapsed={isCollapsed}
        />
      ))}
    </nav>
  );
}

// ─── Contextual panels ──────────────────────────────────────────────────────

function DashboardContextPanel({ isCollapsed }: { isCollapsed: boolean }) {
  const pathname = usePathname();
  return (
    <nav className="flex flex-col gap-0.5 px-2 py-1" aria-label="Dashboard shortcuts">
      <SectionLabel label="Accesso rapido" isCollapsed={isCollapsed} />
      <SidebarLink
        href="/play-records"
        icon={Clock}
        label="Sessioni recenti"
        isActive={pathname?.startsWith('/play-records')}
        isCollapsed={isCollapsed}
      />
      <SidebarLink
        href="/library/wishlist"
        icon={Star}
        label="Wishlist"
        isActive={pathname?.startsWith('/library/wishlist')}
        isCollapsed={isCollapsed}
      />
    </nav>
  );
}

function LibraryContextPanel({ isCollapsed }: { isCollapsed: boolean }) {
  const pathname = usePathname();
  return (
    <nav className="flex flex-col gap-0.5 px-2 py-1" aria-label="Library filters">
      <SectionLabel label="Libreria" isCollapsed={isCollapsed} />
      <SidebarLink
        href="/library"
        icon={BookOpen}
        label="Tutti i giochi"
        isActive={pathname === '/library'}
        isCollapsed={isCollapsed}
      />
      <SidebarLink
        href="/library/favorites"
        icon={Heart}
        label="Preferiti"
        isActive={pathname?.startsWith('/library/favorites')}
        isCollapsed={isCollapsed}
      />
      <SidebarLink
        href="/library/wishlist"
        icon={Star}
        label="Wishlist"
        isActive={pathname?.startsWith('/library/wishlist')}
        isCollapsed={isCollapsed}
      />
      <SidebarLink
        href="/library/archived"
        icon={Archive}
        label="Archiviati"
        isActive={pathname?.startsWith('/library/archived')}
        isCollapsed={isCollapsed}
      />

      <SectionLabel label="Collezioni" isCollapsed={isCollapsed} />
      <SidebarLink
        href="/library/private"
        icon={Layers}
        label="Giochi privati"
        isActive={pathname?.startsWith('/library/private')}
        isCollapsed={isCollapsed}
      />
      <SidebarLink
        href="/library/proposals"
        icon={Calendar}
        label="Proposte"
        isActive={pathname?.startsWith('/library/proposals')}
        isCollapsed={isCollapsed}
      />
    </nav>
  );
}

// ─── Main component ──────────────────────────────────────────────────────────

export interface SidebarContextNavProps {
  isCollapsed: boolean;
}

/**
 * Determines the context key for the contextual zone.
 */
function getContextKey(pathname: string | null): string | null {
  if (!pathname) return null;
  if (pathname === '/dashboard') return 'dashboard';
  if (pathname.startsWith('/library')) return 'library';
  if (pathname.startsWith('/games')) return 'games';
  return null;
}

export function SidebarContextNav({ isCollapsed }: SidebarContextNavProps) {
  const pathname = usePathname();
  const contextKey = getContextKey(pathname);
  const { miniNavTabs } = useNavigation();

  const sectionLabel = contextKey
    ? contextKey.charAt(0).toUpperCase() + contextKey.slice(1).replace(/-/g, ' ')
    : '';

  return (
    <div className="flex-1 overflow-y-auto overflow-x-hidden flex flex-col">
      {/* Zone 1: Fixed primary navigation — always visible */}
      <FixedNavZone isCollapsed={isCollapsed} />

      {/* Divider between zones (only if contextual zone is active) */}
      {(contextKey || miniNavTabs.length > 0) && <hr className="mx-3 border-sidebar-border" />}

      {/* Zone 2: Contextual — changes based on route */}
      <div className="flex-1 overflow-y-auto">
        <AnimatePresence mode="wait" initial={false}>
          {contextKey && (
            <motion.div
              key={contextKey}
              variants={PANEL_VARIANTS}
              initial="initial"
              animate="animate"
              exit="exit"
            >
              {contextKey === 'dashboard' && <DashboardContextPanel isCollapsed={isCollapsed} />}
              {contextKey === 'library' && <LibraryContextPanel isCollapsed={isCollapsed} />}
              {contextKey === 'games' && <GamesFilterPanel isCollapsed={isCollapsed} />}
            </motion.div>
          )}
        </AnimatePresence>

        {/* Zone 3: MiniNav tabs — declared by pages via NavigationContext */}
        <AnimatePresence mode="wait" initial={false}>
          {miniNavTabs.length > 0 && (
            <motion.div
              key="context-tabs"
              variants={PANEL_VARIANTS}
              initial="initial"
              animate="animate"
              exit="exit"
              className="mt-2 space-y-0.5 px-2"
            >
              {sectionLabel && <SectionLabel label={sectionLabel} isCollapsed={isCollapsed} />}
              {miniNavTabs.map(tab => {
                // In collapsed mode, hide tabs without icons (they'd show nothing)
                if (isCollapsed && !tab.icon) return null;

                const TabIcon = tab.icon;
                return (
                  <Link
                    key={tab.id}
                    href={tab.href}
                    className={cn(
                      'flex items-center gap-3 rounded-lg text-sm font-medium',
                      'min-h-[44px] px-3 py-2',
                      'transition-colors duration-150',
                      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring focus-visible:ring-offset-1',
                      isCollapsed && 'justify-center px-2',
                      pathname === tab.href
                        ? 'bg-[hsl(25_95%_45%/0.12)] text-[hsl(25_95%_42%)] font-semibold'
                        : 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
                    )}
                  >
                    {TabIcon && <TabIcon className="h-4 w-4 shrink-0" aria-hidden="true" />}
                    {!isCollapsed && <span className="truncate">{tab.label}</span>}
                  </Link>
                );
              })}
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </div>
  );
}
