'use client';

import { useEffect, useState } from 'react';

import {
  Activity,
  BarChart2,
  Bot,
  ChevronLeft,
  ChevronRight,
  Database,
  LayoutDashboard,
  Mail,
  MonitorCheck,
  Settings,
  Shield,
  Users,
  Gamepad2,
  BellRing,
  FileSearch,
  BookOpen,
  UserCheck,
  Heart,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface AdminSideDrawerProps {
  open: boolean;
  onClose: () => void;
}

interface NavItem {
  label: string;
  href: string;
  icon: React.ElementType;
}

interface NavSection {
  label: string;
  icon: React.ElementType;
  items: NavItem[];
  altroItems?: NavItem[];
}

// ---------------------------------------------------------------------------
// Nav definitions
// ---------------------------------------------------------------------------

const SECTIONS: NavSection[] = [
  {
    label: 'Overview',
    icon: LayoutDashboard,
    items: [
      { label: 'Dashboard', href: '/admin/overview', icon: LayoutDashboard },
      { label: 'Activity Feed', href: '/admin/overview/activity', icon: Activity },
      { label: 'System Health', href: '/admin/overview/system', icon: MonitorCheck },
    ],
  },
  {
    label: 'Content',
    icon: Gamepad2,
    items: [
      { label: 'Giochi', href: '/admin/shared-games/all', icon: Gamepad2 },
      { label: 'Knowledge Base', href: '/admin/knowledge-base', icon: BookOpen },
      { label: 'Email Templates', href: '/admin/content/email-templates', icon: Mail },
    ],
  },
  {
    label: 'AI',
    icon: Bot,
    items: [
      { label: 'Mission Control', href: '/admin/agents', icon: Bot },
      { label: 'RAG Inspector', href: '/admin/agents/inspector', icon: FileSearch },
      { label: 'Agent Definitions', href: '/admin/agents/definitions', icon: Database },
    ],
    altroItems: [
      { label: 'Config', href: '/admin/agents/config', icon: Settings },
      { label: 'Usage', href: '/admin/agents/usage', icon: BarChart2 },
      { label: 'Analytics', href: '/admin/agents/analytics', icon: BarChart2 },
    ],
  },
  {
    label: 'Users',
    icon: Users,
    items: [
      { label: 'Tutti gli utenti', href: '/admin/users', icon: Users },
      { label: 'Invitations', href: '/admin/users/invitations', icon: Mail },
      { label: 'Ruoli & Permessi', href: '/admin/users/roles', icon: Shield },
    ],
    altroItems: [
      { label: 'Access Requests', href: '/admin/users/access-requests', icon: UserCheck },
      { label: 'Activity', href: '/admin/users/activity', icon: Activity },
    ],
  },
];

const SYSTEM_ITEMS: NavItem[] = [
  { label: 'Monitor', href: '/admin/monitor', icon: MonitorCheck },
  { label: 'Config', href: '/admin/config', icon: Settings },
  { label: 'Notifications', href: '/admin/notifications/compose', icon: BellRing },
];

const ANALYTICS_ITEMS: NavItem[] = [
  { label: 'Overview', href: '/admin/analytics', icon: BarChart2 },
  { label: 'Audit', href: '/admin/analytics?tab=audit', icon: Heart },
];

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function isPathActive(pathname: string, href: string): boolean {
  // Strip query string for startsWith check
  const hrefPath = href.split('?')[0];
  return pathname === hrefPath || pathname.startsWith(hrefPath + '/');
}

function isSectionAltroActive(pathname: string, altroItems?: NavItem[]): boolean {
  if (!altroItems) return false;
  return altroItems.some(item => isPathActive(pathname, item.href));
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

interface NavLinkProps {
  item: NavItem;
  pathname: string;
  onClick: () => void;
  indent?: boolean;
}

function NavLink({ item, pathname, onClick, indent = false }: NavLinkProps) {
  const Icon = item.icon;
  const active = isPathActive(pathname, item.href);

  return (
    <Link
      href={item.href}
      onClick={onClick}
      aria-current={active ? 'page' : undefined}
      className={[
        'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
        indent ? 'pl-5' : '',
        active
          ? 'bg-[hsla(25,95%,45%,0.12)] text-[hsl(25,95%,45%)]'
          : 'text-foreground/70 hover:bg-muted hover:text-foreground',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      <Icon className="h-4 w-4 shrink-0" />
      <span>{item.label}</span>
    </Link>
  );
}

interface CollapsibleSectionProps {
  label: string;
  icon: React.ElementType;
  items: NavItem[];
  altroItems?: NavItem[];
  pathname: string;
  onNavigate: () => void;
}

function CollapsibleSection({
  label,
  icon: SectionIcon,
  items,
  altroItems,
  pathname,
  onNavigate,
}: CollapsibleSectionProps) {
  const altroActive = isSectionAltroActive(pathname, altroItems);
  const [altroOpen, setAltroOpen] = useState(altroActive);

  return (
    <div className="flex flex-col gap-0.5">
      {/* Section label */}
      <div className="flex items-center gap-2 px-3 py-1.5 mt-2">
        <SectionIcon className="h-3.5 w-3.5 text-muted-foreground" />
        <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
          {label}
        </span>
      </div>

      {/* Items */}
      {items.map(item => (
        <NavLink key={item.href} item={item} pathname={pathname} onClick={onNavigate} />
      ))}

      {/* Altro collapsible */}
      {altroItems && altroItems.length > 0 && (
        <>
          <button
            type="button"
            onClick={() => setAltroOpen(prev => !prev)}
            className="flex w-full items-center justify-between rounded-lg px-3 py-2 text-sm font-medium text-foreground/60 hover:bg-muted hover:text-foreground transition-colors"
            aria-expanded={altroOpen}
          >
            <span className="text-xs">Altro</span>
            <ChevronRight
              className="h-3.5 w-3.5 shrink-0 transition-transform duration-200"
              style={{ transform: altroOpen ? 'rotate(90deg)' : 'rotate(0deg)' }}
            />
          </button>

          {altroOpen && (
            <div className="flex flex-col gap-0.5 pl-2">
              {altroItems.map(item => (
                <NavLink
                  key={item.href}
                  item={item}
                  pathname={pathname}
                  onClick={onNavigate}
                  indent
                />
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// AdminSideDrawer
// ---------------------------------------------------------------------------

export function AdminSideDrawer({ open, onClose }: AdminSideDrawerProps) {
  const { data: user } = useCurrentUser();
  const pathname = usePathname();

  const [systemOpen, setSystemOpen] = useState(false);
  const [analyticsOpen, setAnalyticsOpen] = useState(false);

  // Prevent body scroll when drawer is open
  useEffect(() => {
    if (open) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }
    return () => {
      document.body.style.overflow = '';
    };
  }, [open]);

  // Close on Escape key
  useEffect(() => {
    if (!open) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [open, onClose]);

  if (!open) return null;

  const userInitial =
    user?.displayName?.charAt(0).toUpperCase() || user?.email?.charAt(0).toUpperCase() || 'A';

  return (
    <>
      {/* Overlay */}
      <div
        data-testid="drawer-overlay"
        className="fixed inset-0 bg-black/40 backdrop-blur-sm z-40"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Drawer panel */}
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Admin navigation menu"
        className="fixed top-0 left-0 bottom-0 w-[280px] bg-background border-r shadow-xl z-50 flex flex-col animate-in slide-in-from-left duration-200"
      >
        {/* User info header */}
        <div className="flex items-center gap-3 px-4 py-5 border-b shrink-0">
          <div className="w-10 h-10 rounded-full bg-[hsl(25,95%,45%)] flex items-center justify-center shrink-0">
            <span className="text-white text-sm font-bold">{userInitial}</span>
          </div>
          <div className="flex flex-col min-w-0">
            <span className="text-sm font-semibold truncate">{user?.displayName || 'Admin'}</span>
            <span className="text-xs text-muted-foreground truncate">{user?.email}</span>
            <span className="text-xs font-medium text-[hsl(25,95%,45%)] capitalize mt-0.5">
              {user?.role || 'admin'}
            </span>
          </div>
        </div>

        {/* Navigation — scrollable */}
        <div className="flex-1 overflow-y-auto px-3 py-3">
          <nav className="flex flex-col gap-0.5">
            {/* Back to app */}
            <Link
              href="/dashboard"
              onClick={onClose}
              className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors mb-2"
              style={{ color: '#4ecdc4' }}
            >
              <ChevronLeft className="h-4 w-4 shrink-0" />
              <span>Torna all&apos;app</span>
            </Link>

            <div className="border-t mb-1" />

            {/* Main admin sections */}
            {SECTIONS.map(section => (
              <CollapsibleSection
                key={section.label}
                label={section.label}
                icon={section.icon}
                items={section.items}
                altroItems={section.altroItems}
                pathname={pathname}
                onNavigate={onClose}
              />
            ))}

            <div className="border-t my-2" />

            {/* System collapsible */}
            <div className="flex flex-col gap-0.5">
              <button
                type="button"
                onClick={() => setSystemOpen(prev => !prev)}
                className="flex w-full items-center justify-between rounded-lg px-3 py-2 text-sm font-medium text-foreground/70 hover:bg-muted hover:text-foreground transition-colors"
                aria-expanded={systemOpen}
              >
                <div className="flex items-center gap-2">
                  <Settings className="h-4 w-4 shrink-0" />
                  <span>System</span>
                </div>
                <ChevronRight
                  className="h-4 w-4 shrink-0 transition-transform duration-200"
                  style={{ transform: systemOpen ? 'rotate(90deg)' : 'rotate(0deg)' }}
                />
              </button>
              {systemOpen && (
                <div className="flex flex-col gap-0.5 pl-2">
                  {SYSTEM_ITEMS.map(item => (
                    <NavLink
                      key={item.href}
                      item={item}
                      pathname={pathname}
                      onClick={onClose}
                      indent
                    />
                  ))}
                </div>
              )}
            </div>

            {/* Analytics collapsible */}
            <div className="flex flex-col gap-0.5">
              <button
                type="button"
                onClick={() => setAnalyticsOpen(prev => !prev)}
                className="flex w-full items-center justify-between rounded-lg px-3 py-2 text-sm font-medium text-foreground/70 hover:bg-muted hover:text-foreground transition-colors"
                aria-expanded={analyticsOpen}
              >
                <div className="flex items-center gap-2">
                  <BarChart2 className="h-4 w-4 shrink-0" />
                  <span>Analytics</span>
                </div>
                <ChevronRight
                  className="h-4 w-4 shrink-0 transition-transform duration-200"
                  style={{ transform: analyticsOpen ? 'rotate(90deg)' : 'rotate(0deg)' }}
                />
              </button>
              {analyticsOpen && (
                <div className="flex flex-col gap-0.5 pl-2">
                  {ANALYTICS_ITEMS.map(item => (
                    <NavLink
                      key={item.href}
                      item={item}
                      pathname={pathname}
                      onClick={onClose}
                      indent
                    />
                  ))}
                </div>
              )}
            </div>
          </nav>
        </div>
      </div>
    </>
  );
}
