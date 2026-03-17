'use client';

import { useEffect, useRef, useState } from 'react';

import { ChevronDown } from 'lucide-react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';

import { getActiveSection, isSidebarItemActive } from '@/config/admin-dashboard-navigation';
import { cn } from '@/lib/utils';

export function AdminBreadcrumb() {
  const pathname = usePathname();
  const router = useRouter();
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const activeSection = getActiveSection(pathname);

  // Find active sub-item
  const activeItem = activeSection?.sidebarItems.find(item => isSidebarItemActive(item, pathname));

  // Fallback: capitalize last pathname segment
  const fallbackLabel = (() => {
    if (activeSection) return null;
    const segments = pathname
      .replace(/^\/admin\/?/, '')
      .split('/')
      .filter(Boolean);
    if (segments.length === 0) return 'Overview';
    return segments[0].charAt(0).toUpperCase() + segments[0].slice(1);
  })();

  const hasSubItems = (activeSection?.sidebarItems.length ?? 0) > 1;

  // Close dropdown on outside click
  useEffect(() => {
    if (!dropdownOpen) return;
    function handleClick(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setDropdownOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [dropdownOpen]);

  // Close dropdown on navigation
  useEffect(() => {
    setDropdownOpen(false);
  }, [pathname]);

  return (
    <div className="md:hidden" ref={dropdownRef}>
      <div
        className={cn(
          'h-9 bg-background/80 backdrop-blur-sm',
          'border-b',
          dropdownOpen ? 'border-primary' : 'border-border/40',
          'flex items-center px-4 gap-1.5'
        )}
        data-testid="admin-breadcrumb"
      >
        {activeSection ? (
          <>
            <Link
              href={activeSection.baseRoute}
              className="text-xs font-semibold text-primary font-quicksand hover:underline"
            >
              {activeSection.label}
            </Link>
            {activeItem && (
              <>
                <span className="text-xs text-muted-foreground">›</span>
                <span className="text-xs font-medium text-foreground font-nunito truncate">
                  {activeItem.label}
                </span>
              </>
            )}
          </>
        ) : (
          <span className="text-xs font-semibold text-primary font-quicksand">{fallbackLabel}</span>
        )}

        {hasSubItems && (
          <button
            type="button"
            className="ml-auto flex items-center justify-center w-6 h-6 rounded text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
            onClick={() => setDropdownOpen(prev => !prev)}
            aria-label="Show sub-sections"
            aria-expanded={dropdownOpen}
          >
            <ChevronDown
              className={cn('w-3.5 h-3.5 transition-transform', dropdownOpen && 'rotate-180')}
            />
          </button>
        )}
      </div>

      {/* Dropdown */}
      {dropdownOpen && activeSection && (
        <div
          className="absolute left-0 right-0 z-50 bg-popover border-b-2 border-primary shadow-lg"
          data-testid="admin-breadcrumb-dropdown"
        >
          <div className="py-1">
            {activeSection.sidebarItems.map(item => {
              const isActive = isSidebarItemActive(item, pathname);
              const ItemIcon = item.icon;

              return (
                <button
                  key={item.href}
                  type="button"
                  className={cn(
                    'flex items-center gap-2.5 w-full px-4 py-2.5 text-left',
                    'transition-colors border-l-[3px]',
                    isActive
                      ? 'bg-primary/5 border-primary text-foreground font-medium'
                      : 'border-transparent text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                  )}
                  onClick={() => {
                    router.push(item.href);
                    setDropdownOpen(false);
                  }}
                >
                  <ItemIcon className="w-4 h-4 shrink-0" />
                  <div className="min-w-0">
                    <div className="text-sm font-nunito">{item.label}</div>
                    {isActive && <div className="text-[10px] text-primary">current page</div>}
                  </div>
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
