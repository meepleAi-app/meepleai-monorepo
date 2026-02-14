/**
 * LibraryDropdown Component (Issue #4064)
 *
 * Dropdown navigation for Library section with sub-items:
 * - Collezione → /library (games collection)
 * - Giochi Privati → /library/private (private games)
 *
 * Features:
 * - Desktop navigation dropdown
 * - Active state highlighting for sub-routes
 * - Keyboard navigation (Tab, Enter, Escape)
 * - ARIA attributes for accessibility
 * - Purple active state, orange hover
 */

'use client';

import { useState } from 'react';

import { BookOpen, ChevronDown } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export interface LibraryDropdownProps {
  /** Additional className */
  className?: string;
}

const LIBRARY_ITEMS = [
  {
    href: '/library',
    label: 'Collezione',
    ariaLabel: 'Navigate to your game collection',
  },
  {
    href: '/library/private',
    label: 'Giochi Privati',
    ariaLabel: 'Navigate to your private games',
  },
];

export function LibraryDropdown({ className }: LibraryDropdownProps) {
  const pathname = usePathname();
  const [isOpen, setIsOpen] = useState(false);

  // Check if any library route is active
  const isLibraryActive = pathname === '/library' || pathname?.startsWith('/library/');

  // Check if specific sub-route is active
  const isActive = (href: string) => {
    if (href === '/library') {
      return pathname === '/library';
    }
    return pathname?.startsWith(href) ?? false;
  };

  return (
    <DropdownMenu open={isOpen} onOpenChange={setIsOpen}>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          className={cn(
            'flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium',
            'transition-colors duration-200',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
            isLibraryActive
              ? 'bg-[hsl(262_83%_62%/0.1)] dark:bg-[hsl(262_83%_62%/0.2)] text-[hsl(262_83%_62%)] font-semibold'
              : 'text-muted-foreground hover:text-primary hover:bg-muted dark:hover:bg-muted/70',
            className
          )}
          aria-label="Library navigation menu"
          aria-expanded={isOpen}
          aria-haspopup="menu"
          data-testid="library-dropdown-trigger"
        >
          <BookOpen className="h-4 w-4" aria-hidden="true" />
          <span>Libreria</span>
          <ChevronDown
            className={cn(
              'h-3.5 w-3.5 transition-transform duration-200',
              isOpen && 'rotate-180'
            )}
            aria-hidden="true"
          />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-48">
        {LIBRARY_ITEMS.map(({ href, label, ariaLabel }) => {
          const active = isActive(href);
          return (
            <DropdownMenuItem
              key={href}
              asChild
              data-testid={`library-item-${href.split('/').pop()}`}
            >
              <Link
                href={href}
                aria-label={ariaLabel}
                aria-current={active ? 'page' : undefined}
                className={cn(
                  'flex items-center gap-2 cursor-pointer w-full',
                  active && 'bg-[hsl(262_83%_62%/0.1)] text-[hsl(262_83%_62%)] font-semibold'
                )}
                onClick={() => setIsOpen(false)}
              >
                <span>{label}</span>
              </Link>
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export default LibraryDropdown;
