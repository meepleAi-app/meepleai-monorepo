'use client';

/**
 * NavbarDropdown — Dropdown menu for Navbar sections (Tool, Discover, Admin)
 * Issue #5036 — Navbar Component
 *
 * Renders a trigger button with a popover dropdown list of nav links.
 */

import { useState, useRef, useEffect, type KeyboardEvent } from 'react';
import Link from 'next/link';
import { ChevronDown, type LucideIcon } from 'lucide-react';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

export interface NavbarDropdownItem {
  id: string;
  label: string;
  href: string;
  icon?: LucideIcon;
  description?: string;
}

export interface NavbarDropdownProps {
  /** Section label shown as the trigger button */
  label: string;
  /** Items in the dropdown */
  items: NavbarDropdownItem[];
  /** Additional className for the trigger */
  className?: string;
}

/**
 * NavbarDropdown
 *
 * Accessible dropdown menu for Navbar sections.
 * - Keyboard: Enter/Space to open, Arrow keys to navigate, Escape to close
 * - ARIA: aria-haspopup="menu", aria-expanded, role="menu", role="menuitem"
 * - Auto-closes on outside click or item selection
 */
export function NavbarDropdown({ label, items, className }: NavbarDropdownProps) {
  const [open, setOpen] = useState(false);
  const pathname = usePathname();
  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  // Check if any item in this section is active
  const isActive = items.some((item) => pathname?.startsWith(item.href));

  // Close on outside click
  useEffect(() => {
    if (!open) return;
    function handleOutside(e: MouseEvent) {
      if (
        menuRef.current &&
        !menuRef.current.contains(e.target as Node) &&
        triggerRef.current &&
        !triggerRef.current.contains(e.target as Node)
      ) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handleOutside);
    return () => document.removeEventListener('mousedown', handleOutside);
  }, [open]);

  // Keyboard navigation
  function handleTriggerKeyDown(e: KeyboardEvent<HTMLButtonElement>) {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      setOpen((prev) => !prev);
    } else if (e.key === 'ArrowDown' && !open) {
      e.preventDefault();
      setOpen(true);
    } else if (e.key === 'Escape') {
      setOpen(false);
    }
  }

  function handleMenuKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    if (e.key === 'Escape') {
      setOpen(false);
      triggerRef.current?.focus();
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      const items = menuRef.current?.querySelectorAll<HTMLAnchorElement>('[role="menuitem"]');
      if (items) {
        const idx = Array.from(items).indexOf(document.activeElement as HTMLAnchorElement);
        items[Math.min(idx + 1, items.length - 1)]?.focus();
      }
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      const items = menuRef.current?.querySelectorAll<HTMLAnchorElement>('[role="menuitem"]');
      if (items) {
        const idx = Array.from(items).indexOf(document.activeElement as HTMLAnchorElement);
        items[Math.max(idx - 1, 0)]?.focus();
      }
    }
  }

  return (
    <div className="relative">
      {/* Trigger */}
      <button
        ref={triggerRef}
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((prev) => !prev)}
        onKeyDown={handleTriggerKeyDown}
        className={cn(
          'flex items-center gap-1 px-3 py-2 rounded-md text-sm font-medium',
          'text-foreground/80 hover:text-foreground hover:bg-accent',
          'transition-colors duration-150',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          isActive && 'text-foreground font-semibold',
          className
        )}
      >
        {label}
        <ChevronDown
          className={cn(
            'h-3.5 w-3.5 text-foreground/50 transition-transform duration-200',
            open && 'rotate-180'
          )}
          aria-hidden="true"
        />
      </button>

      {/* Dropdown menu */}
      {open && (
        <div
          ref={menuRef}
          role="menu"
          aria-label={`${label} menu`}
          onKeyDown={handleMenuKeyDown}
          className={cn(
            'absolute top-full left-0 mt-1 z-50 min-w-[200px]',
            'rounded-lg border border-border bg-popover shadow-md',
            'py-1 animate-in fade-in-0 zoom-in-95 duration-100'
          )}
        >
          {items.map((item) => {
            const Icon = item.icon;
            const isItemActive = pathname?.startsWith(item.href);
            return (
              <Link
                key={item.id}
                href={item.href}
                role="menuitem"
                onClick={() => setOpen(false)}
                className={cn(
                  'flex items-center gap-3 px-4 py-2.5 text-sm',
                  'text-popover-foreground hover:bg-accent hover:text-accent-foreground',
                  'transition-colors duration-100 outline-none',
                  'focus-visible:bg-accent',
                  isItemActive && 'bg-accent/60 font-medium text-accent-foreground'
                )}
              >
                {Icon && (
                  <Icon className="h-4 w-4 text-foreground/60 shrink-0" aria-hidden="true" />
                )}
                <span>{item.label}</span>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}
