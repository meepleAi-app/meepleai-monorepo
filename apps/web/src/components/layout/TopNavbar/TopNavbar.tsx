/**
 * TopNavbar — 3-Tier Navigation System, Level 1
 * Issue #5036 — Navbar Component
 *
 * Role-aware horizontal navbar with 3 sections:
 *   [🎲 MeepleAI]  [Tool ▾] [Discover ▾] [Admin ▾]  [🔔] [Avatar]
 *
 * - Desktop: full sections + dropdowns
 * - Mobile: logo + icons → hamburger drawer
 * - Admin section: visible only to admin/editor roles
 */

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

import {
  Bell,
  ChevronDown,
  LayoutDashboard,
  LogOut,
  Menu,
  Settings,
  User,
  X,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { NotificationBell } from '@/components/notifications';
import { ThemeToggle } from '@/components/ui/navigation/ThemeToggle';
import { useAdminRole } from '@/hooks/useAdminRole';
import { useAuthUser } from '@/hooks/useAuthUser';
import { cn } from '@/lib/utils';

import { Logo } from '../Navbar/Logo';
import {
  ADMIN_SECTION,
  DISCOVER_SECTION,
  EDITOR_SECTION,
  TOOL_SECTION,
  type NavSection,
  type NavSectionItem,
} from './top-navbar-config';

// ─── Section Dropdown ─────────────────────────────────────────────────────────

interface SectionDropdownProps {
  section: NavSection;
  isOpen: boolean;
  onToggle: () => void;
  onClose: () => void;
  isActive: boolean;
}

function SectionDropdown({
  section,
  isOpen,
  onToggle,
  onClose,
  isActive,
}: SectionDropdownProps) {
  const dropdownRef = useRef<HTMLDivElement>(null);
  const pathname = usePathname();

  // Close on outside click
  useEffect(() => {
    if (!isOpen) return;
    function handleClick(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        onClose();
      }
    }
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [isOpen, onClose]);

  // Close on Escape
  useEffect(() => {
    if (!isOpen) return;
    function handleKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose();
    }
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [isOpen, onClose]);

  return (
    <div ref={dropdownRef} className="relative">
      <button
        onClick={onToggle}
        aria-haspopup="menu"
        aria-expanded={isOpen}
        className={cn(
          'flex items-center gap-1 px-3 py-1.5 rounded-lg text-sm font-semibold',
          'transition-colors duration-200',
          'font-nunito',
          isActive || isOpen
            ? 'bg-primary/10 text-primary'
            : 'text-foreground/70 hover:text-foreground hover:bg-muted'
        )}
      >
        {section.label}
        <ChevronDown
          className={cn(
            'h-3.5 w-3.5 transition-transform duration-200',
            isOpen && 'rotate-180'
          )}
        />
      </button>

      {isOpen && (
        <div
          role="menu"
          aria-label={`${section.label} menu`}
          className={cn(
            'absolute top-full left-0 mt-2 z-50',
            'w-56 rounded-xl border border-border bg-card',
            'shadow-lg shadow-black/10',
            'p-1.5',
            'animate-in fade-in-0 zoom-in-95 duration-150'
          )}
        >
          {section.items.map((item) => {
            const Icon = item.icon;
            const isItemActive = pathname === item.href || pathname.startsWith(item.href + '/');
            return (
              <Link
                key={item.href}
                href={item.href}
                role="menuitem"
                onClick={onClose}
                className={cn(
                  'flex items-start gap-3 px-3 py-2.5 rounded-lg',
                  'transition-colors duration-150',
                  isItemActive
                    ? 'bg-primary/10 text-primary'
                    : 'text-foreground hover:bg-muted'
                )}
              >
                <Icon className="h-4 w-4 mt-0.5 shrink-0" />
                <div className="min-w-0">
                  <p className="text-sm font-semibold font-quicksand leading-none">
                    {item.label}
                  </p>
                  {item.description && (
                    <p className="text-xs text-muted-foreground mt-0.5 leading-snug">
                      {item.description}
                    </p>
                  )}
                </div>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}

// ─── User Menu ────────────────────────────────────────────────────────────────

interface UserMenuProps {
  userName?: string;
  userRole?: string;
}

function UserMenu({ userName, userRole }: UserMenuProps) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [open]);

  const initials = userName
    ? userName.split(' ').map((w) => w[0]).join('').toUpperCase().slice(0, 2)
    : 'U';

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => setOpen(!open)}
        aria-label="User menu"
        aria-haspopup="menu"
        aria-expanded={open}
        className={cn(
          'flex items-center gap-2 rounded-lg px-2 py-1.5',
          'transition-colors duration-200',
          'hover:bg-muted'
        )}
      >
        <div className="h-7 w-7 rounded-full bg-primary flex items-center justify-center">
          <span className="text-xs font-bold text-primary-foreground font-quicksand">
            {initials}
          </span>
        </div>
        <ChevronDown className="h-3 w-3 text-muted-foreground hidden sm:block" />
      </button>

      {open && (
        <div
          role="menu"
          className={cn(
            'absolute top-full right-0 mt-2 z-50',
            'w-52 rounded-xl border border-border bg-card',
            'shadow-lg shadow-black/10 p-1.5',
            'animate-in fade-in-0 zoom-in-95 duration-150'
          )}
        >
          {/* User info */}
          <div className="px-3 py-2 mb-1 border-b border-border">
            <p className="text-sm font-semibold font-quicksand text-foreground truncate">
              {userName ?? 'Utente'}
            </p>
            <p className="text-xs text-muted-foreground capitalize">{userRole ?? 'user'}</p>
          </div>

          <Link
            href="/profile"
            role="menuitem"
            onClick={() => setOpen(false)}
            className="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-foreground hover:bg-muted transition-colors"
          >
            <User className="h-4 w-4" />
            Il mio profilo
          </Link>
          <Link
            href="/profile?tab=settings"
            role="menuitem"
            onClick={() => setOpen(false)}
            className="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-foreground hover:bg-muted transition-colors"
          >
            <Settings className="h-4 w-4" />
            Impostazioni
          </Link>

          <div className="my-1 border-t border-border" />

          <div className="px-3 py-1">
            <ThemeToggle showLabel size="sm" className="w-full justify-start" />
          </div>

          <div className="my-1 border-t border-border" />

          <Link
            href="/api/auth/logout"
            role="menuitem"
            className="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-destructive hover:bg-destructive/10 transition-colors"
          >
            <LogOut className="h-4 w-4" />
            Esci
          </Link>
        </div>
      )}
    </div>
  );
}

// ─── Mobile Drawer ────────────────────────────────────────────────────────────

interface MobileDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  sections: NavSection[];
}

function MobileDrawer({ isOpen, onClose, sections }: MobileDrawerProps) {
  const pathname = usePathname();

  useEffect(() => {
    if (!isOpen) return;
    function handleKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose();
    }
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 z-40 bg-black/40 backdrop-blur-sm md:hidden"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Drawer */}
      <div
        className={cn(
          'fixed top-0 left-0 bottom-0 z-50 w-72 md:hidden',
          'bg-card border-r border-border',
          'flex flex-col',
          'animate-in slide-in-from-left duration-200'
        )}
        role="dialog"
        aria-modal="true"
        aria-label="Menu di navigazione"
      >
        {/* Header */}
        <div className="flex items-center justify-between px-4 py-3 border-b border-border">
          <Logo variant="auto" size="sm" />
          <button
            onClick={onClose}
            aria-label="Chiudi menu"
            className="p-1.5 rounded-lg hover:bg-muted transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Nav sections */}
        <nav className="flex-1 overflow-y-auto p-3 space-y-4">
          {sections.map((section) => (
            <div key={section.id}>
              <p className="px-2 mb-1 text-xs font-semibold uppercase tracking-widest text-muted-foreground font-nunito">
                {section.label}
              </p>
              <ul className="space-y-0.5">
                {section.items.map((item) => {
                  const Icon = item.icon;
                  const isActive =
                    pathname === item.href || pathname.startsWith(item.href + '/');
                  return (
                    <li key={item.href}>
                      <Link
                        href={item.href}
                        onClick={onClose}
                        className={cn(
                          'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-semibold',
                          'transition-colors duration-150 font-nunito',
                          isActive
                            ? 'bg-primary/10 text-primary'
                            : 'text-foreground/80 hover:bg-muted hover:text-foreground'
                        )}
                      >
                        <Icon className="h-4 w-4 shrink-0" />
                        {item.label}
                      </Link>
                    </li>
                  );
                })}
              </ul>
            </div>
          ))}
        </nav>

        {/* Footer */}
        <div className="p-3 border-t border-border">
          <ThemeToggle showLabel size="sm" className="w-full justify-start" />
        </div>
      </div>
    </>
  );
}

// ─── TopNavbar ────────────────────────────────────────────────────────────────

export interface TopNavbarProps {
  className?: string;
}

export function TopNavbar({ className }: TopNavbarProps) {
  const { user } = useAuthUser();
  const { isAdminOrAbove, isEditorOrAbove } = useAdminRole();
  const pathname = usePathname();

  const [openSection, setOpenSection] = useState<string | null>(null);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);

  // Close dropdown on route change
  useEffect(() => {
    setOpenSection(null);
    setMobileOpen(false);
  }, [pathname]);

  // Shadow on scroll
  useEffect(() => {
    function onScroll() {
      setScrolled(window.scrollY > 4);
    }
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  const toggleSection = useCallback((id: string) => {
    setOpenSection((prev) => (prev === id ? null : id));
  }, []);

  const closeSection = useCallback(() => setOpenSection(null), []);

  // Determine admin section
  const adminSection = isAdminOrAbove ? ADMIN_SECTION : isEditorOrAbove ? EDITOR_SECTION : null;

  // All sections for mobile drawer
  const mobileSections = [TOOL_SECTION, DISCOVER_SECTION, ...(adminSection ? [adminSection] : [])];

  // Helper: is any item in a section active
  const isSectionActive = (section: NavSection) =>
    section.items.some(
      (item) => pathname === item.href || pathname.startsWith(item.href + '/')
    );

  return (
    <>
      {/* Skip to main content — keyboard/screen-reader navigation (WCAG 2.4.1) */}
      <a
        href="#main-content"
        className={cn(
          'sr-only focus:not-sr-only',
          'focus:fixed focus:top-2 focus:left-2 focus:z-[100]',
          'focus:px-4 focus:py-2 focus:rounded-lg',
          'focus:bg-background focus:text-foreground',
          'focus:border focus:border-border',
          'focus:text-sm focus:font-semibold focus:font-nunito',
          'focus:shadow-md'
        )}
      >
        Vai al contenuto principale
      </a>

      <header
        data-testid="top-navbar"
        className={cn(
          'sticky top-0 z-40 w-full',
          'h-14',
          'bg-background/95 backdrop-blur-md backdrop-saturate-150',
          'border-b border-border/60',
          'transition-shadow duration-200',
          scrolled && 'shadow-sm',
          className
        )}
      >
        <div className="flex h-full items-center justify-between px-4 md:px-6 gap-2">
          {/* ── LEFT: Hamburger (mobile) + Logo ── */}
          <div className="flex items-center gap-2 shrink-0">
            {/* Mobile hamburger */}
            <button
              className="md:hidden p-1.5 rounded-lg hover:bg-muted transition-colors"
              onClick={() => setMobileOpen(true)}
              aria-label="Apri menu"
            >
              <Menu className="h-5 w-5" />
            </button>

            <Logo variant="auto" size="sm" />
          </div>

          {/* ── CENTER: Section dropdowns (desktop) ── */}
          <nav
            className="hidden md:flex items-center gap-1"
            aria-label="Navigazione principale"
          >
            <SectionDropdown
              section={TOOL_SECTION}
              isOpen={openSection === 'tool'}
              onToggle={() => toggleSection('tool')}
              onClose={closeSection}
              isActive={isSectionActive(TOOL_SECTION)}
            />
            <SectionDropdown
              section={DISCOVER_SECTION}
              isOpen={openSection === 'discover'}
              onToggle={() => toggleSection('discover')}
              onClose={closeSection}
              isActive={isSectionActive(DISCOVER_SECTION)}
            />
            {adminSection && (
              <SectionDropdown
                section={adminSection}
                isOpen={openSection === 'admin'}
                onToggle={() => toggleSection('admin')}
                onClose={closeSection}
                isActive={isSectionActive(adminSection)}
              />
            )}
          </nav>

          {/* ── RIGHT: Notifications + Avatar ── */}
          <div className="flex items-center gap-1 shrink-0">
            <NotificationBell />
            <UserMenu
              userName={user?.displayName ?? user?.email}
              userRole={user?.role}
            />
          </div>
        </div>
      </header>

      {/* Mobile drawer */}
      <MobileDrawer
        isOpen={mobileOpen}
        onClose={() => setMobileOpen(false)}
        sections={mobileSections}
      />
    </>
  );
}
