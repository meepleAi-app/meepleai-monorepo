'use client';

/**
 * Navbar — Main navigation header (3-section, role-aware)
 * Issue #5036 — Navbar Component
 *
 * Replaces the deprecated Navbar + UnifiedHeader with a unified 3-tier L1 component.
 *
 * Desktop layout:
 *   [MeepleAI logo]  [Tool ▾]  [Discover ▾]  [Admin ▾]  [🔔]  [Avatar ▾]
 *
 * Mobile layout:
 *   [☰]  [MeepleAI logo]  [🔔]  [Avatar ▾]
 *   ↳ hamburger opens NavbarMobileDrawer with 3 collapsible sections
 *
 * Role visibility:
 *   - Admin section: visible for role admin | superadmin | editor
 *   - Tool + Discover: always visible (auth-gated by route)
 *
 * Does NOT depend on LayoutProvider — self-contained with own mobile state.
 * Designed to be placed in the `navbar` slot of <LayoutShell>.
 */

import { useState } from 'react';

import {
  BookOpen,
  MessageSquare,
  Gamepad2,
  BarChart3,
  Lightbulb,
  Globe,
  Shield,
  Bot,
  FolderOpen,
  Activity,
  Settings,
  Menu,
} from 'lucide-react';

import { NotificationBell } from '@/components/notifications';
import { Button } from '@/components/ui/primitives/button';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { cn } from '@/lib/utils';

import { Logo } from '@/components/layout/TopNavbar/Logo';
import { NavbarDropdown, type NavbarDropdownItem } from './NavbarDropdown';
import { NavbarMobileDrawer } from './NavbarMobileDrawer';
import { NavbarUserMenu } from './NavbarUserMenu';

// ─── Section Definitions ────────────────────────────────────────────────────

const TOOL_ITEMS: NavbarDropdownItem[] = [
  { id: 'library', label: 'Libreria', href: '/library', icon: BookOpen },
  { id: 'agents', label: 'Agenti', href: '/agents', icon: Bot },
  { id: 'chat', label: 'Chat', href: '/chat', icon: MessageSquare },
  { id: 'sessions', label: 'Sessioni', href: '/sessions', icon: Gamepad2 },
  { id: 'play-records', label: 'Partite', href: '/play-records', icon: BarChart3 },
];

const DISCOVER_ITEMS: NavbarDropdownItem[] = [
  { id: 'catalog', label: 'Catalogo', href: '/games', icon: Gamepad2 },
  { id: 'proposals', label: 'Proposte', href: '/proposals', icon: Lightbulb },
  { id: 'community', label: 'Community', href: '/community', icon: Globe },
];

const ADMIN_ITEMS: NavbarDropdownItem[] = [
  { id: 'users', label: 'Utenti', href: '/admin/users', icon: Shield },
  { id: 'ai', label: 'Intelligenza Artificiale', href: '/admin/agents', icon: Bot },
  { id: 'content', label: 'Contenuti', href: '/admin/shared-games', icon: FolderOpen },
  { id: 'analytics', label: 'Analytics', href: '/admin/overview', icon: BarChart3 },
  { id: 'config', label: 'Configurazione', href: '/admin/config', icon: Settings },
  { id: 'monitor', label: 'Monitor', href: '/admin/monitor', icon: Activity },
];

// ─── Component ──────────────────────────────────────────────────────────────

export interface NavbarProps {
  /** Additional className for the nav element */
  className?: string;
}

/**
 * Navbar
 *
 * 3-section role-aware navigation bar for the MeepleAI layout system.
 * Slot target: `navbar` prop of `<LayoutShell>`.
 */
export function Navbar({ className }: NavbarProps) {
  const { data: user } = useCurrentUser();
  const [mobileOpen, setMobileOpen] = useState(false);

  const isAdmin =
    user?.role?.toLowerCase() === 'admin' ||
    user?.role?.toLowerCase() === 'superadmin' ||
    user?.role?.toLowerCase() === 'editor';

  return (
    <>
      <nav
        className={cn(
          'flex h-full w-full items-center justify-between px-4 sm:px-6',
          'bg-background/95 backdrop-blur-sm border-b border-border/50',
          className
        )}
        role="navigation"
        aria-label="Main navigation"
        data-testid="navbar"
      >
        {/* ── Left: Hamburger (mobile) + Logo ────────────────────────────── */}
        <div className="flex items-center gap-2">
          {/* Hamburger — mobile only */}
          <Button
            variant="ghost"
            size="icon"
            className="md:hidden"
            aria-label="Apri menu di navigazione"
            aria-expanded={mobileOpen}
            aria-controls="mobile-nav-drawer"
            onClick={() => setMobileOpen(true)}
            data-testid="hamburger-button"
          >
            <Menu className="h-5 w-5" aria-hidden="true" />
          </Button>

          {/* Logo */}
          <Logo variant="auto" size="sm" />
        </div>

        {/* ── Center: Section dropdowns (desktop only) ───────────────────── */}
        <div
          className="hidden md:flex items-center gap-1"
          role="menubar"
          aria-label="Navigation sections"
        >
          <NavbarDropdown label="Tool" items={TOOL_ITEMS} data-testid="tool-dropdown" />
          <NavbarDropdown label="Scopri" items={DISCOVER_ITEMS} data-testid="discover-dropdown" />
          {isAdmin && (
            <NavbarDropdown label="Admin" items={ADMIN_ITEMS} data-testid="admin-dropdown" />
          )}
        </div>

        {/* ── Right: Notifications + User ────────────────────────────────── */}
        <div className="flex items-center gap-1">
          <NotificationBell />
          <NavbarUserMenu />
        </div>
      </nav>

      {/* Mobile drawer (rendered outside nav to avoid nesting issues) */}
      <NavbarMobileDrawer open={mobileOpen} onClose={() => setMobileOpen(false)} />
    </>
  );
}
