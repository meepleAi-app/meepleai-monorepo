'use client';

import { useEffect, useState } from 'react';

import { Menu } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
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

/**
 * Hamburger menu shown below the md breakpoint.
 * Opens a Sheet drawer with the same primary links as TopBarNavLinks.
 * Closes automatically on navigation.
 */
export function TopBarMobileMenu() {
  const [open, setOpen] = useState(false);
  const pathname = usePathname() ?? '';

  useEffect(() => {
    if (open) setOpen(false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname]);

  return (
    <>
      <button
        type="button"
        aria-label="Apri menu"
        aria-expanded={open}
        aria-controls="topbar-mobile-drawer"
        onClick={() => setOpen(true)}
        className="md:hidden inline-flex h-10 w-10 items-center justify-center rounded-lg text-[var(--text-secondary)] hover:bg-[var(--bg-elevated)] hover:text-[var(--text-primary)] transition-colors shrink-0"
      >
        <Menu className="h-5 w-5" aria-hidden />
      </button>

      {open && (
        <Sheet open={open} onOpenChange={setOpen}>
          <SheetContent side="left" className="w-[260px] p-0" id="topbar-mobile-drawer">
            <SheetHeader className="sr-only">
              <SheetTitle>Navigazione</SheetTitle>
            </SheetHeader>
            <nav aria-label="Primary mobile" className="flex flex-col gap-1 p-4 pt-8">
              {LINKS.map(link => {
                const active = link.isActive(pathname);
                return (
                  <Link
                    key={link.href}
                    href={link.href}
                    aria-current={active ? 'page' : undefined}
                    className={cn(
                      'px-4 py-3 rounded-lg font-nunito font-bold text-[0.95rem] transition-colors',
                      active
                        ? 'text-[hsl(25_95%_38%)] bg-[hsla(25,95%,45%,0.1)] shadow-[inset_0_0_0_1px_hsla(25,95%,45%,0.25)]'
                        : 'text-[var(--text-secondary)] hover:bg-[var(--bg-elevated)] hover:text-[var(--text-primary)]'
                    )}
                  >
                    {link.label}
                  </Link>
                );
              })}
            </nav>
          </SheetContent>
        </Sheet>
      )}
    </>
  );
}
