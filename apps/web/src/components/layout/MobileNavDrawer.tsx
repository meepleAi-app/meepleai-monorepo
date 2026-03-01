/**
 * MobileNavDrawer Component (Issue #4064)
 *
 * Slide-out navigation drawer for mobile viewports (<768px).
 * Uses useNavigationItems hook for unified nav config.
 *
 * Features:
 * - Hamburger button triggers drawer from left
 * - Role-filtered navigation items (via unified hook)
 * - Libreria expandable section with children
 * - Active link purple highlighting
 * - Close on link click or outside tap
 * - 300ms smooth animation
 * - Focus trap and keyboard navigation
 * - WCAG 2.1 AA compliant
 */

'use client';

import { useState } from 'react';

import {
  Menu,
  X,
  ChevronDown,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';

import { getActiveLibraryTab } from '@/config/library-navigation';
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { useNavigationItems } from '@/hooks/useNavigationItems';
import { NAV_TEST_IDS } from '@/lib/test-ids';
import { cn } from '@/lib/utils';

export interface MobileNavDrawerProps {
  /** Additional className */
  className?: string;
}

export function MobileNavDrawer({
  className,
}: MobileNavDrawerProps) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const { items: navItems, isItemActive } = useNavigationItems();
  const [isOpen, setIsOpen] = useState(false);

  // Check if any library route is active
  const isLibraryActive = pathname?.startsWith('/library') ?? false;
  const search = searchParams?.toString() ? `?${searchParams.toString()}` : '';
  const activeLibraryTabId = getActiveLibraryTab(pathname ?? '', search);
  const [isLibraryExpanded, setIsLibraryExpanded] = useState(isLibraryActive);

  // Find the library item to get its children
  const libraryItem = navItems.find(item => item.id === 'library');
  const libraryChildren = libraryItem?.children ?? [];

  // Split items: main (no group, no library) vs strumenti group
  const mainItems = navItems.filter(item => item.id !== 'library' && !item.group);
  const strumentiItems = navItems.filter(item => item.group === 'strumenti');

  // Close drawer on navigation
  const handleLinkClick = () => {
    setIsOpen(false);
  };

  return (
    <Sheet open={isOpen} onOpenChange={setIsOpen}>
      <SheetTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className={cn('md:hidden', className)}
          aria-label="Open navigation menu"
          data-testid={NAV_TEST_IDS.mobileNavTrigger}
        >
          <Menu className="h-5 w-5" />
        </Button>
      </SheetTrigger>
      <SheetContent
        side="left"
        className="w-[280px] sm:w-[320px]"
        data-testid={NAV_TEST_IDS.mobileNavDrawer}
      >
        <SheetHeader>
          <SheetTitle>Navigazione</SheetTitle>
        </SheetHeader>

        <nav className="flex flex-col gap-1 mt-6" aria-label="Mobile navigation">
          {/* Main navigation items */}
          {mainItems.map(item => {
            const Icon = item.icon;
            const active = isItemActive(item, pathname ?? '');
            return (
              <Link
                key={item.id}
                href={item.href}
                aria-label={item.ariaLabel}
                aria-current={active ? 'page' : undefined}
                className={cn(
                  'flex items-center gap-3 px-4 py-3 rounded-lg text-sm font-medium',
                  'transition-colors duration-200',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
                  active
                    ? 'bg-[hsl(262_83%_62%/0.15)] text-[hsl(262_83%_62%)] font-semibold'
                    : 'text-muted-foreground hover:text-primary hover:bg-muted'
                )}
                onClick={handleLinkClick}
                data-testid={NAV_TEST_IDS.mobileNavItem(item.href.split('/').filter(Boolean).join('-') || 'home')}
              >
                <Icon className="h-5 w-5" aria-hidden="true" />
                <span>{item.label}</span>
              </Link>
            );
          })}

          {/* Library Expandable Section */}
          {libraryItem && libraryChildren.length > 0 && (
            <div className="flex flex-col">
              <button
                onClick={() => setIsLibraryExpanded(!isLibraryExpanded)}
                aria-expanded={isLibraryExpanded}
                aria-controls="library-submenu"
                className={cn(
                  'flex items-center justify-between gap-3 px-4 py-3 rounded-lg text-sm font-medium',
                  'transition-colors duration-200',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
                  isLibraryActive
                    ? 'bg-[hsl(262_83%_62%/0.15)] text-[hsl(262_83%_62%)] font-semibold'
                    : 'text-muted-foreground hover:text-primary hover:bg-muted'
                )}
                data-testid={NAV_TEST_IDS.mobileLibraryToggle}
              >
                <div className="flex items-center gap-3">
                  {(() => {
                    const LibIcon = libraryItem.icon;
                    return <LibIcon className="h-5 w-5" aria-hidden="true" />;
                  })()}
                  <span>{libraryItem.label}</span>
                </div>
                <ChevronDown
                  className={cn(
                    'h-4 w-4 transition-transform duration-200',
                    isLibraryExpanded && 'rotate-180'
                  )}
                  aria-hidden="true"
                />
              </button>

              {isLibraryExpanded && (
                <div
                  id="library-submenu"
                  className="flex flex-col gap-0.5 mt-1 pl-4"
                  role="group"
                  aria-label="Library submenu"
                >
                  {libraryChildren.map(child => {
                    const active = child.id === activeLibraryTabId;
                    return (
                      <Link
                        key={child.id}
                        href={child.href}
                        aria-label={child.ariaLabel}
                        aria-current={active ? 'page' : undefined}
                        className={cn(
                          'flex items-center gap-3 px-4 py-2.5 rounded-lg text-sm',
                          'transition-colors duration-200',
                          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
                          active
                            ? 'bg-[hsl(262_83%_62%/0.1)] text-[hsl(262_83%_62%)] font-medium'
                            : 'text-muted-foreground hover:text-primary hover:bg-muted/50'
                        )}
                        onClick={handleLinkClick}
                        data-testid={NAV_TEST_IDS.mobileLibraryItem(child.id)}
                      >
                        <span>{child.label}</span>
                      </Link>
                    );
                  })}
                </div>
              )}
            </div>
          )}

          {/* Strumenti group */}
          {strumentiItems.length > 0 && (
            <>
              <hr className="my-3 border-border/50" />
              <p className="px-4 mb-2 text-xs font-semibold uppercase tracking-wider text-muted-foreground/60">
                Strumenti
              </p>
              {strumentiItems.map(item => {
                const Icon = item.icon;
                const active = isItemActive(item, pathname ?? '');
                return (
                  <Link
                    key={item.id}
                    href={item.href}
                    aria-label={item.ariaLabel}
                    aria-current={active ? 'page' : undefined}
                    className={cn(
                      'flex items-center gap-3 px-4 py-3 rounded-lg text-sm font-medium',
                      'transition-colors duration-200',
                      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
                      active
                        ? 'bg-[hsl(262_83%_62%/0.15)] text-[hsl(262_83%_62%)] font-semibold'
                        : 'text-muted-foreground hover:text-primary hover:bg-muted'
                    )}
                    onClick={handleLinkClick}
                    data-testid={NAV_TEST_IDS.mobileNavItem(item.href.split('/').filter(Boolean).join('-') || item.id)}
                  >
                    <Icon className="h-5 w-5" aria-hidden="true" />
                    <span>{item.label}</span>
                  </Link>
                );
              })}
            </>
          )}
        </nav>

        {/* Close Button (redundant with X in SheetContent, but accessible) */}
        <SheetClose asChild>
          <Button
            variant="outline"
            className="absolute bottom-6 left-6 right-6"
            data-testid={NAV_TEST_IDS.mobileNavClose}
          >
            <X className="mr-2 h-4 w-4" />
            Chiudi
          </Button>
        </SheetClose>
      </SheetContent>
    </Sheet>
  );
}

export default MobileNavDrawer;
