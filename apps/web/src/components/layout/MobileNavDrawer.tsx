/**
 * MobileNavDrawer Component (Issue #4064)
 *
 * Slide-out navigation drawer for mobile viewports (<768px).
 *
 * Features:
 * - Hamburger button triggers drawer from left
 * - Role-filtered navigation items
 * - Libreria expandable section
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
  BookOpen,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export interface MobileNavDrawerProps {
  /** Navigation items to display */
  navItems: Array<{
    href: string;
    icon: React.ComponentType<{ className?: string }>;
    label: string;
    ariaLabel: string;
  }>;
  /** Library sub-items (Collezione, Giochi Privati) */
  libraryItems: Array<{
    href: string;
    label: string;
    ariaLabel: string;
  }>;
  /** Is any library route active */
  isLibraryActive: boolean;
  /** Additional className */
  className?: string;
}

export function MobileNavDrawer({
  navItems,
  libraryItems,
  isLibraryActive,
  className,
}: MobileNavDrawerProps) {
  const pathname = usePathname();
  const [isOpen, setIsOpen] = useState(false);
  const [isLibraryExpanded, setIsLibraryExpanded] = useState(isLibraryActive);

  // Check if route is active
  const isActive = (href: string) => {
    if (href === '/') {
      return pathname === '/';
    }
    if (href === '/dashboard') {
      return pathname === '/dashboard';
    }
    if (href === '/library') {
      return pathname === '/library';
    }
    // Chat routes: /chat/new, /chat/{threadId}, etc.
    if (href === '/chat/new') {
      return pathname?.startsWith('/chat') ?? false;
    }
    return pathname?.startsWith(href) ?? false;
  };

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
          data-testid="mobile-nav-trigger"
        >
          <Menu className="h-5 w-5" />
        </Button>
      </SheetTrigger>
      <SheetContent
        side="left"
        className="w-[280px] sm:w-[320px]"
        data-testid="mobile-nav-drawer"
      >
        <SheetHeader>
          <SheetTitle>Navigazione</SheetTitle>
        </SheetHeader>

        <nav className="flex flex-col gap-1 mt-6" aria-label="Mobile navigation">
          {navItems.map(({ href, icon: Icon, label, ariaLabel }) => {
            const active = isActive(href);
            return (
              <Link
                key={href}
                href={href}
                aria-label={ariaLabel}
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
                data-testid={`mobile-nav-item-${href.split('/').filter(Boolean).join('-') || 'home'}`}
              >
                <Icon className="h-5 w-5" aria-hidden="true" />
                <span>{label}</span>
              </Link>
            );
          })}

          {/* Library Expandable Section */}
          {libraryItems.length > 0 && (
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
                data-testid="mobile-library-toggle"
              >
                <div className="flex items-center gap-3">
                  <BookOpen className="h-5 w-5" aria-hidden="true" />
                  <span>Libreria</span>
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
                  {libraryItems.map(({ href, label, ariaLabel }) => {
                    const active = isActive(href);
                    return (
                      <Link
                        key={href}
                        href={href}
                        aria-label={ariaLabel}
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
                        data-testid={`mobile-library-item-${href.split('/').pop()}`}
                      >
                        <span>{label}</span>
                      </Link>
                    );
                  })}
                </div>
              )}
            </div>
          )}
        </nav>

        {/* Close Button (redundant with X in SheetContent, but accessible) */}
        <SheetClose asChild>
          <Button
            variant="outline"
            className="absolute bottom-6 left-6 right-6"
            data-testid="mobile-nav-close"
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
