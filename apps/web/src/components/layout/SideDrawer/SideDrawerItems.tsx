'use client';

import { useState } from 'react';

import {
  Bell,
  BookOpen,
  ChevronRight,
  LayoutDashboard,
  MessageCircle,
  Moon,
  PenTool,
  Settings,
  Shield,
  Target,
  User,
  Users2,
  Wrench,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { isAdminRole } from '@/lib/utils/roles';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface NavItem {
  label: string;
  href: string;
  icon: React.ElementType;
}

interface SideDrawerItemsProps {
  userRole?: string;
  onNavigate: () => void;
}

// ---------------------------------------------------------------------------
// Nav definitions
// ---------------------------------------------------------------------------

const PRIMARY_ITEMS: NavItem[] = [
  { label: 'Dashboard', href: '/dashboard', icon: LayoutDashboard },
  { label: 'Libreria', href: '/library', icon: BookOpen },
  { label: 'Sessioni', href: '/sessions', icon: Target },
  { label: 'Partite & Statistiche', href: '/play-records', icon: LayoutDashboard },
  { label: 'Chat AI', href: '/chat', icon: MessageCircle },
];

// Use Dice5 via a workaround — lucide-react may not export Dice5 in all versions.
// We import LayoutDashboard as fallback above; let's fix with the correct icon name.

const ALTRO_ITEMS: NavItem[] = [
  { label: 'Giocatori', href: '/players', icon: Users2 },
  { label: 'Serate', href: '/game-nights', icon: Moon },
  { label: 'Toolkit', href: '/toolkit', icon: Wrench },
  { label: 'Editor Agenti', href: '/editor', icon: PenTool },
];

const ACCOUNT_ITEMS: NavItem[] = [
  { label: 'Notifiche', href: '/notifications', icon: Bell },
  { label: 'Profilo', href: '/profile', icon: User },
  { label: 'Impostazioni', href: '/profile?tab=settings', icon: Settings },
];

const ADMIN_ITEM: NavItem = {
  label: 'Admin Hub',
  href: '/admin/overview',
  icon: Shield,
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const ALTRO_HREFS = ALTRO_ITEMS.map(i => i.href);

function useIsAltroActive(pathname: string): boolean {
  return ALTRO_HREFS.some(href => pathname.startsWith(href));
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

interface NavLinkProps {
  item: NavItem;
  pathname: string;
  onClick: () => void;
}

function NavLink({ item, pathname, onClick }: NavLinkProps) {
  const Icon = item.icon;
  // Strip query string for active detection
  const isActive = pathname === item.href || pathname.startsWith(item.href + '/');

  return (
    <Link
      href={item.href}
      onClick={onClick}
      aria-current={isActive ? 'page' : undefined}
      className={[
        'flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
        isActive
          ? 'bg-[hsla(25,95%,45%,0.12)] text-[hsl(25,95%,45%)]'
          : 'text-foreground/70 hover:bg-muted hover:text-foreground',
      ].join(' ')}
    >
      <Icon className="h-4 w-4 shrink-0" />
      <span>{item.label}</span>
    </Link>
  );
}

// ---------------------------------------------------------------------------
// SideDrawerItems
// ---------------------------------------------------------------------------

export function SideDrawerItems({ userRole, onNavigate }: SideDrawerItemsProps) {
  const pathname = usePathname();
  const altroActive = useIsAltroActive(pathname);
  const [altroOpen, setAltroOpen] = useState(altroActive);

  const showAdmin = isAdminRole(userRole);

  return (
    <nav className="flex flex-col gap-1 flex-1 overflow-y-auto">
      {/* Primary items */}
      <div className="flex flex-col gap-0.5">
        {PRIMARY_ITEMS.map(item => (
          <NavLink key={item.href} item={item} pathname={pathname} onClick={onNavigate} />
        ))}
      </div>

      {/* Altro collapsible */}
      <div className="mt-1">
        <button
          type="button"
          onClick={() => setAltroOpen(prev => !prev)}
          className="flex w-full items-center justify-between rounded-lg px-3 py-2.5 text-sm font-medium text-foreground/70 hover:bg-muted hover:text-foreground transition-colors"
          aria-expanded={altroOpen}
        >
          <span>Altro</span>
          <ChevronRight
            className="h-4 w-4 shrink-0 transition-transform duration-200"
            style={{ transform: altroOpen ? 'rotate(90deg)' : 'rotate(0deg)' }}
          />
        </button>

        {altroOpen && (
          <div className="mt-0.5 flex flex-col gap-0.5 pl-2">
            {ALTRO_ITEMS.map(item => (
              <NavLink key={item.href} item={item} pathname={pathname} onClick={onNavigate} />
            ))}
          </div>
        )}
      </div>

      {/* Account section */}
      <div className="mt-4 border-t pt-4 flex flex-col gap-0.5">
        {ACCOUNT_ITEMS.map(item => (
          <NavLink key={item.href} item={item} pathname={pathname} onClick={onNavigate} />
        ))}
        {showAdmin && <NavLink item={ADMIN_ITEM} pathname={pathname} onClick={onNavigate} />}
      </div>
    </nav>
  );
}
