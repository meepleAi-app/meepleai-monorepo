import Link from 'next/link';

import type { AdminNavGroup, AdminNavItem } from './admin-nav-config';

/**
 * Icon color per nav group, matching SP5 mockup categorical palette
 * (admin-mockups/.../admin-base.css uses --c-* HSL tokens).
 * Maps to Tailwind `text-entity-*` utilities (see globals.css @theme inline).
 */
const GROUP_ICON_COLOR: Record<AdminNavGroup['id'], string> = {
  A: 'text-entity-game', // Admin Console (orange)
  B: 'text-entity-chat', // Power-User Tools (blue)
  C: 'text-entity-toolkit', // Platform & Operations (green)
  D: 'text-entity-agent', // AI Tooling & Data Quality (amber)
};

function isPathActive(pathname: string, href: string): boolean {
  const hrefPath = href.split('?')[0];
  return pathname === hrefPath || pathname.startsWith(hrefPath + '/');
}

interface NavLinkProps {
  item: AdminNavItem;
  pathname: string;
  onClick?: () => void;
  /** Icon color for inactive state (group-themed) */
  iconColorClass?: string;
}

function NavLink({ item, pathname, onClick, iconColorClass }: NavLinkProps) {
  const Icon = item.icon;
  const active = isPathActive(pathname, item.href);

  return (
    <Link
      href={item.href}
      onClick={onClick}
      aria-current={active ? 'page' : undefined}
      className={[
        'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
        active
          ? 'bg-[hsla(25,95%,45%,0.12)] text-[hsl(var(--c-game-text))]'
          : 'text-foreground/70 hover:bg-muted hover:text-foreground',
      ].join(' ')}
    >
      <Icon className={`h-4 w-4 shrink-0 ${active ? '' : (iconColorClass ?? '')}`} />
      <span>{item.label}</span>
    </Link>
  );
}

export interface AdminNavListProps {
  groups: AdminNavGroup[];
  pathname: string;
  onNavigate?: () => void;
  ariaLabel?: string;
}

/**
 * Shared rendering of the admin navigation groups. Used by both the mobile
 * drawer (AdminSideDrawer) and the desktop sidebar (AdminSidebar, F0b Task 2).
 * Receives already-filtered groups; does not read the user/role itself.
 */
export function AdminNavList({ groups, pathname, onNavigate, ariaLabel }: AdminNavListProps) {
  return (
    <nav aria-label={ariaLabel} className="flex flex-col gap-0.5">
      {groups.map(group => {
        const iconColorClass = GROUP_ICON_COLOR[group.id];
        return (
          <div key={group.id} className="flex flex-col gap-0.5">
            <div className="flex items-center gap-2 px-3 py-1.5 mt-2">
              <group.icon className={`h-3.5 w-3.5 ${iconColorClass}`} />
              <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                {group.label}
              </span>
            </div>
            {group.items.map(item => (
              <NavLink
                key={item.href}
                item={item}
                pathname={pathname}
                onClick={onNavigate}
                iconColorClass={iconColorClass}
              />
            ))}
          </div>
        );
      })}
    </nav>
  );
}
