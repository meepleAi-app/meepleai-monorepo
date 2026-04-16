'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

interface NavLink {
  href: string;
  label: string;
  isActive: (pathname: string) => boolean;
}

const LINKS: NavLink[] = [
  {
    href: '/',
    label: 'Home',
    isActive: p => p === '/' || p === '/dashboard' || p?.startsWith('/dashboard') || false,
  },
  {
    href: '/library',
    label: 'Libreria',
    isActive: p => p?.startsWith('/library') || false,
  },
  {
    href: '/sessions',
    label: 'Sessioni',
    isActive: p => p?.startsWith('/sessions') || false,
  },
];

export function TopBarNavLinks() {
  const pathname = usePathname() ?? '';

  return (
    <nav className="hidden md:flex items-center gap-1 ml-3 shrink-0" aria-label="Primary">
      {LINKS.map(link => {
        const active = link.isActive(pathname);
        return (
          <Link
            key={link.href}
            href={link.href}
            aria-current={active ? 'page' : undefined}
            className={cn(
              'px-3.5 py-2 rounded-[10px] font-nunito font-bold text-[0.82rem] transition-colors',
              active
                ? 'text-[hsl(25_95%_38%)] shadow-[inset_0_0_0_1px_hsla(25,95%,45%,0.25)]'
                : 'text-[var(--text-secondary)] hover:bg-[var(--bg-elevated)] hover:text-[var(--text-primary)]'
            )}
            style={active ? { background: 'hsla(25, 95%, 45%, 0.1)' } : undefined}
          >
            {link.label}
          </Link>
        );
      })}
    </nav>
  );
}
