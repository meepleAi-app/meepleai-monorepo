/**
 * SidebarContextNav - Issue #4936
 * Context-sensitive sidebar navigation panel.
 *
 * Renders different content based on current route, with fade/slide animation.
 * Integrates with AnimatePresence (framer-motion) for smooth transitions.
 *
 * Context map:
 *   /dashboard  → DashboardPanel (quick links + stats hints)
 *   /library/*  → LibraryPanel  (collection filters)
 *   /admin/*    → AdminPanel    (admin menu — delegated to SidebarNav)
 *   /games/*    → GamesPanel    (catalog filters)
 *   default     → SidebarNav    (standard navigation)
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
  SlidersHorizontal,
  Layers,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

import { SidebarNav } from './SidebarNav';

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
          ? 'bg-[hsl(25_95%_45%/0.12)] text-[hsl(25,95%,42%)] font-semibold'
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

// ─── Context panels ──────────────────────────────────────────────────────────

function DashboardPanel({ isCollapsed }: { isCollapsed: boolean }) {
  const pathname = usePathname();
  return (
    <nav className="flex flex-col gap-0.5 px-2 py-3" aria-label="Dashboard navigation">
      <SidebarLink
        href="/dashboard"
        icon={LayoutDashboard}
        label="Overview"
        isActive={pathname === '/dashboard'}
        isCollapsed={isCollapsed}
      />
      <SidebarLink
        href="/play-records"
        icon={Clock}
        label="Sessioni recenti"
        isActive={pathname?.startsWith('/play-records')}
        isCollapsed={isCollapsed}
      />
      <SidebarLink
        href="/library"
        icon={Library}
        label="La mia collezione"
        isActive={pathname?.startsWith('/library')}
        isCollapsed={isCollapsed}
      />

      <SectionLabel label="Accesso rapido" isCollapsed={isCollapsed} />
      <SidebarLink
        href="/games"
        icon={Gamepad2}
        label="Catalogo giochi"
        isActive={false}
        isCollapsed={isCollapsed}
      />
      <SidebarLink
        href="/players"
        icon={Users}
        label="Giocatori"
        isActive={pathname?.startsWith('/players')}
        isCollapsed={isCollapsed}
      />
    </nav>
  );
}

function LibraryPanel({ isCollapsed }: { isCollapsed: boolean }) {
  const pathname = usePathname();
  return (
    <nav className="flex flex-col gap-0.5 px-2 py-3" aria-label="Library navigation">
      {/* Back to Dashboard */}
      <SidebarLink
        href="/dashboard"
        icon={LayoutDashboard}
        label="Dashboard"
        isActive={false}
        isCollapsed={isCollapsed}
      />
      <hr className="my-1 border-sidebar-border" />

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

function GamesPanel({ isCollapsed }: { isCollapsed: boolean }) {
  const pathname = usePathname();
  return (
    <nav className="flex flex-col gap-0.5 px-2 py-3" aria-label="Games catalog navigation">
      {/* Back to Dashboard */}
      <SidebarLink
        href="/dashboard"
        icon={LayoutDashboard}
        label="Dashboard"
        isActive={false}
        isCollapsed={isCollapsed}
      />
      <hr className="my-1 border-sidebar-border" />

      <SidebarLink
        href="/games"
        icon={Gamepad2}
        label="Tutti i giochi"
        isActive={pathname === '/games'}
        isCollapsed={isCollapsed}
      />

      <SectionLabel label="Filtri rapidi" isCollapsed={isCollapsed} />
      <SidebarLink
        href="/games?bggOnly=true"
        icon={Star}
        label="Top BGG"
        isActive={false}
        isCollapsed={isCollapsed}
      />
      <SidebarLink
        href="/games?minPlayers=2&maxPlayers=2"
        icon={Users}
        label="2 Giocatori"
        isActive={false}
        isCollapsed={isCollapsed}
      />
      <SidebarLink
        href="/games?minPlayers=3&maxPlayers=6"
        icon={Users}
        label="3-6 Giocatori"
        isActive={false}
        isCollapsed={isCollapsed}
      />

      <SectionLabel label="Avanzato" isCollapsed={isCollapsed} />
      <SidebarLink
        href="/games?sortBy=recent"
        icon={Clock}
        label="Aggiunti di recente"
        isActive={false}
        isCollapsed={isCollapsed}
      />
      <SidebarLink
        href="/games"
        icon={SlidersHorizontal}
        label="Filtri avanzati"
        isActive={false}
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
 * Determines the context key for the current pathname.
 * Used as AnimatePresence key to trigger re-animation on context change.
 */
function getContextKey(pathname: string | null): string {
  if (!pathname) return 'default';
  if (pathname === '/dashboard') return 'dashboard';
  if (pathname.startsWith('/library')) return 'library';
  if (pathname.startsWith('/games')) return 'games';
  // Admin keeps using SidebarNav (standard)
  return 'default';
}

export function SidebarContextNav({ isCollapsed }: SidebarContextNavProps) {
  const pathname = usePathname();
  const contextKey = getContextKey(pathname);

  return (
    <div className="flex-1 overflow-y-auto overflow-x-hidden">
      <AnimatePresence mode="wait" initial={false}>
        <motion.div
          key={contextKey}
          variants={PANEL_VARIANTS}
          initial="initial"
          animate="animate"
          exit="exit"
          className="h-full"
        >
          {contextKey === 'dashboard' && <DashboardPanel isCollapsed={isCollapsed} />}
          {contextKey === 'library' && <LibraryPanel isCollapsed={isCollapsed} />}
          {contextKey === 'games' && <GamesPanel isCollapsed={isCollapsed} />}
          {contextKey === 'default' && <SidebarNav isCollapsed={isCollapsed} />}
        </motion.div>
      </AnimatePresence>
    </div>
  );
}
