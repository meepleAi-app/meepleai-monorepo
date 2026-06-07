import Link from 'next/link';

import type { MainNavItem } from './main-nav-config';

/**
 * Returns true when the current pathname is exactly the item path or a
 * descendant of it. The query string (e.g. `/games?tab=discover`) is
 * stripped before comparison so that `/games?tab=collection` is still
 * considered "active" for the Games entry.
 */
export function isPathActive(pathname: string, href: string): boolean {
  const hrefPath = href.split('?')[0];
  return pathname === hrefPath || pathname.startsWith(hrefPath + '/');
}

interface NavLinkProps {
  item: MainNavItem;
  pathname: string;
  onClick?: () => void;
  notificationCount?: number;
}

function NavLink({ item, pathname, onClick, notificationCount }: NavLinkProps) {
  const Icon = item.icon;
  const active = isPathActive(pathname, item.defaultHref);
  const showBadge = item.showCounter === true && (notificationCount ?? 0) > 0;

  return (
    <Link
      href={item.defaultHref}
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
      <span className="flex-1">{item.label}</span>
      {showBadge ? (
        <span
          aria-label={`${notificationCount} unread`}
          className="ml-auto inline-flex h-5 min-w-[1.25rem] items-center justify-center rounded-full bg-[hsl(var(--c-warning-ink))] px-1.5 text-xs font-semibold text-background"
        >
          {notificationCount}
        </span>
      ) : null}
    </Link>
  );
}

export interface MainNavListProps {
  items: ReadonlyArray<MainNavItem>;
  pathname: string;
  onNavigate?: () => void;
  /** Count for items where `showCounter` is true (T6 will wire SSE). */
  notificationCount?: number;
  ariaLabel?: string;
}

/**
 * Stateless renderer of the main user navigation. Mirrors the convention of
 * `AdminNavList` (admin-nav/AdminNavList.tsx): does not read auth/role, simply
 * renders the filtered items it receives. The container (`MainSidebar`) wires
 * `useCurrentUser` + `filterNavByPermission` and passes the result here.
 */
export function MainNavList({
  items,
  pathname,
  onNavigate,
  notificationCount,
  ariaLabel,
}: MainNavListProps) {
  return (
    <nav aria-label={ariaLabel} className="flex flex-col gap-0.5">
      {items.map(item => (
        <NavLink
          key={item.id}
          item={item}
          pathname={pathname}
          onClick={onNavigate}
          notificationCount={notificationCount}
        />
      ))}
    </nav>
  );
}
