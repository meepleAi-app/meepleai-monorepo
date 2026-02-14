/**
 * LibraryDropdown Component
 *
 * Dropdown navigation for Library section in the desktop header.
 * Reads children from the unified nav config (library item children).
 *
 * Features:
 * - Desktop navigation dropdown
 * - Active state highlighting for sub-routes
 * - Keyboard navigation (Tab, Enter, Escape)
 * - ARIA attributes for accessibility
 * - Purple active state
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
import { UNIFIED_NAV_ITEMS } from '@/config/navigation';
import { cn } from '@/lib/utils';

export interface LibraryDropdownProps {
  /** Additional className */
  className?: string;
}

export function LibraryDropdown({ className }: LibraryDropdownProps) {
  const pathname = usePathname();
  const [isOpen, setIsOpen] = useState(false);

  // Get library children from unified config
  const libraryItem = UNIFIED_NAV_ITEMS.find(item => item.id === 'library');
  const libraryChildren = libraryItem?.children ?? [];

  // Check if any library route is active
  const isLibraryActive = pathname?.startsWith('/library') ?? false;

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
        {libraryChildren.map(child => {
          const active = isActive(child.href);
          return (
            <DropdownMenuItem
              key={child.id}
              asChild
              data-testid={`library-item-${child.id}`}
            >
              <Link
                href={child.href}
                aria-label={child.ariaLabel}
                aria-current={active ? 'page' : undefined}
                className={cn(
                  'flex items-center gap-2 cursor-pointer w-full',
                  active && 'bg-[hsl(262_83%_62%/0.1)] text-[hsl(262_83%_62%)] font-semibold'
                )}
                onClick={() => setIsOpen(false)}
              >
                <span>{child.label}</span>
              </Link>
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export default LibraryDropdown;
