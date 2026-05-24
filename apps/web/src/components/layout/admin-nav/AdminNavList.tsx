import Link from 'next/link';

import type { AdminNavGroup, AdminNavItem } from './admin-nav-config';

export function isPathActive(pathname: string, href: string): boolean {
  const hrefPath = href.split('?')[0];
  return pathname === hrefPath || pathname.startsWith(hrefPath + '/');
}

interface NavLinkProps {
  item: AdminNavItem;
  pathname: string;
  onClick?: () => void;
}

function NavLink({ item, pathname, onClick }: NavLinkProps) {
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
      <Icon className="h-4 w-4 shrink-0" />
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
      {groups.map(group => (
        <div key={group.id} className="flex flex-col gap-0.5">
          <div className="flex items-center gap-2 px-3 py-1.5 mt-2">
            <group.icon className="h-3.5 w-3.5 text-muted-foreground" />
            <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              {group.label}
            </span>
          </div>
          {group.items.map(item => (
            <NavLink key={item.href} item={item} pathname={pathname} onClick={onNavigate} />
          ))}
        </div>
      ))}
    </nav>
  );
}
