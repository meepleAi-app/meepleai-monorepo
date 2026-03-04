/**
 * SidebarNav Component
 * Navigation items renderer for the sidebar.
 * Splits items into main items and grouped items (e.g., 'strumenti').
 * Library item expands to show children.
 */

'use client';

import { useState } from 'react';

import { ChevronDown } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { useNavigationItems } from '@/hooks/useNavigationItems';
import { cn } from '@/lib/utils';

export interface SidebarNavProps {
  isCollapsed: boolean;
}

export function SidebarNav({ isCollapsed }: SidebarNavProps) {
  const pathname = usePathname();
  const { items: navItems, isItemActive } = useNavigationItems();

  const isLibraryActive = pathname?.startsWith('/library') ?? false;
  const [isLibraryOpen, setIsLibraryOpen] = useState(isLibraryActive);

  // Split items: main (no group) vs grouped
  const mainItems = navItems.filter(item => !item.group && item.id !== 'library');
  const libraryItem = navItems.find(item => item.id === 'library');
  const strumentiItems = navItems.filter(item => item.group === 'strumenti');

  return (
    <nav className="flex-1 overflow-y-auto px-3 py-4" aria-label="Sidebar navigation">
      {/* Main navigation items */}
      <ul className="flex flex-col gap-1" role="list">
        {mainItems.map(item => {
          const Icon = item.icon;
          const active = isItemActive(item, pathname ?? '');
          const link = (
            <Link
              href={item.href}
              aria-label={item.ariaLabel}
              aria-current={active ? 'page' : undefined}
              className={cn(
                'flex items-center gap-3 rounded-lg text-sm font-medium',
                'min-h-[44px] px-3 py-2',
                'transition-colors duration-200',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring focus-visible:ring-offset-2',
                active
                  ? 'bg-[hsl(25_95%_45%/0.12)] text-[hsl(25_95%_42%)] font-semibold'
                  : 'text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
                isCollapsed && 'justify-center px-2'
              )}
              data-testid={item.testId}
            >
              <Icon className="h-5 w-5 shrink-0" aria-hidden="true" />
              {!isCollapsed && <span>{item.label}</span>}
            </Link>
          );

          return (
            <li key={item.id}>
              {isCollapsed ? (
                <TooltipProvider delayDuration={0}>
                  <Tooltip>
                    <TooltipTrigger asChild>{link}</TooltipTrigger>
                    <TooltipContent side="right">{item.label}</TooltipContent>
                  </Tooltip>
                </TooltipProvider>
              ) : (
                link
              )}
            </li>
          );
        })}

        {/* Library expandable item */}
        {libraryItem && (
          <li>
            {isCollapsed ? (
              <TooltipProvider delayDuration={0}>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Link
                      href={libraryItem.href}
                      aria-label={libraryItem.ariaLabel}
                      aria-current={isLibraryActive ? 'page' : undefined}
                      className={cn(
                        'flex items-center justify-center rounded-lg text-sm font-medium',
                        'min-h-[44px] px-2 py-2',
                        'transition-colors duration-200',
                        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring focus-visible:ring-offset-2',
                        isLibraryActive
                          ? 'bg-[hsl(25_95%_45%/0.12)] text-[hsl(25_95%_42%)] font-semibold'
                          : 'text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
                      )}
                      data-testid={libraryItem.testId}
                    >
                      {(() => {
                        const LibIcon = libraryItem.icon;
                        return <LibIcon className="h-5 w-5 shrink-0" aria-hidden="true" />;
                      })()}
                    </Link>
                  </TooltipTrigger>
                  <TooltipContent side="right">{libraryItem.label}</TooltipContent>
                </Tooltip>
              </TooltipProvider>
            ) : (
              <Collapsible open={isLibraryOpen} onOpenChange={setIsLibraryOpen}>
                <CollapsibleTrigger
                  className={cn(
                    'flex w-full items-center justify-between gap-3 rounded-lg text-sm font-medium',
                    'min-h-[44px] px-3 py-2',
                    'transition-colors duration-200',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring focus-visible:ring-offset-2',
                    isLibraryActive
                      ? 'bg-[hsl(25_95%_45%/0.12)] text-[hsl(25_95%_42%)] font-semibold'
                      : 'text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
                  )}
                  data-testid="sidebar-library-toggle"
                >
                  <span className="flex items-center gap-3">
                    {(() => {
                      const LibIcon = libraryItem.icon;
                      return <LibIcon className="h-5 w-5 shrink-0" aria-hidden="true" />;
                    })()}
                    <span>{libraryItem.label}</span>
                  </span>
                  <ChevronDown
                    className={cn(
                      'h-4 w-4 transition-transform duration-200',
                      isLibraryOpen && 'rotate-180'
                    )}
                    aria-hidden="true"
                  />
                </CollapsibleTrigger>
                <CollapsibleContent>
                  <ul className="mt-1 ml-4 flex flex-col gap-0.5" role="list">
                    {libraryItem.children?.map(child => {
                      const childActive =
                        child.href === '/library'
                          ? pathname === '/library'
                          : (pathname?.startsWith(child.href) ?? false);
                      return (
                        <li key={child.id}>
                          <Link
                            href={child.href}
                            aria-label={child.ariaLabel}
                            aria-current={childActive ? 'page' : undefined}
                            className={cn(
                              'flex items-center gap-3 rounded-lg text-sm',
                              'min-h-[40px] px-3 py-1.5',
                              'transition-colors duration-200',
                              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring focus-visible:ring-offset-2',
                              childActive
                                ? 'bg-[hsl(25_95%_45%/0.12)] text-[hsl(25_95%_42%)] font-medium'
                                : 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
                            )}
                            data-testid={`sidebar-library-${child.id}`}
                          >
                            {child.icon &&
                              (() => {
                                const ChildIcon = child.icon;
                                return (
                                  <ChildIcon className="h-4 w-4 shrink-0" aria-hidden="true" />
                                );
                              })()}
                            <span>{child.label}</span>
                          </Link>
                        </li>
                      );
                    })}
                  </ul>
                </CollapsibleContent>
              </Collapsible>
            )}
          </li>
        )}
      </ul>

      {/* Strumenti group */}
      {strumentiItems.length > 0 && (
        <>
          <hr className="my-3 border-sidebar-border" />
          {!isCollapsed && (
            <p className="px-3 mb-2 text-xs font-semibold uppercase tracking-wider text-sidebar-foreground/50">
              Strumenti
            </p>
          )}
          <ul className="flex flex-col gap-1" role="list">
            {strumentiItems.map(item => {
              const Icon = item.icon;
              const active = isItemActive(item, pathname ?? '');
              const link = (
                <Link
                  href={item.href}
                  aria-label={item.ariaLabel}
                  aria-current={active ? 'page' : undefined}
                  className={cn(
                    'flex items-center gap-3 rounded-lg text-sm font-medium',
                    'min-h-[44px] px-3 py-2',
                    'transition-colors duration-200',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring focus-visible:ring-offset-2',
                    active
                      ? 'bg-[hsl(25_95%_45%/0.12)] text-[hsl(25_95%_42%)] font-semibold'
                      : 'text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
                    isCollapsed && 'justify-center px-2'
                  )}
                  data-testid={item.testId}
                >
                  <Icon className="h-5 w-5 shrink-0" aria-hidden="true" />
                  {!isCollapsed && <span>{item.label}</span>}
                </Link>
              );

              return (
                <li key={item.id}>
                  {isCollapsed ? (
                    <TooltipProvider delayDuration={0}>
                      <Tooltip>
                        <TooltipTrigger asChild>{link}</TooltipTrigger>
                        <TooltipContent side="right">{item.label}</TooltipContent>
                      </Tooltip>
                    </TooltipProvider>
                  ) : (
                    link
                  )}
                </li>
              );
            })}
          </ul>
        </>
      )}
    </nav>
  );
}
