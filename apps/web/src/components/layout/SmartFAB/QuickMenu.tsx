/**
 * QuickMenu Component
 * Issue #3291 - Phase 5: Smart FAB
 *
 * Long-press popup with alternative actions.
 */

'use client';

import { forwardRef, useEffect, useRef, type ComponentPropsWithoutRef } from 'react';

import {
  Plus,
  Search,
  Camera,
  Play,
  MessageSquare,
  Share,
  Timer,
  Trophy,
  PlusCircle,
  History,
  type LucideIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import type { QuickMenuItem } from '@/config/fab';
import { cn } from '@/lib/utils';

/**
 * Icon mapping for quick menu icons
 */
const ICON_MAP: Record<string, LucideIcon> = {
  plus: Plus,
  search: Search,
  camera: Camera,
  play: Play,
  'message-square': MessageSquare,
  share: Share,
  timer: Timer,
  trophy: Trophy,
  'plus-circle': PlusCircle,
  history: History,
};

export interface QuickMenuProps
  extends Omit<ComponentPropsWithoutRef<'div'>, 'children' | 'onSelect'> {
  /** Menu items to display */
  items: QuickMenuItem[];
  /** Whether menu is open */
  isOpen: boolean;
  /** Close menu handler */
  onClose: () => void;
  /** Item selection handler */
  onSelect: (itemId: string) => void;
}

/**
 * QuickMenu Component
 *
 * Renders a popup menu with alternative FAB actions.
 * Appears above the FAB on long-press.
 */
export const QuickMenu = forwardRef<HTMLDivElement, QuickMenuProps>(
  function QuickMenu(
    { items, isOpen, onClose, onSelect, className, ...props },
    ref
  ) {
    const menuRef = useRef<HTMLDivElement>(null);

    // Close on click outside
    useEffect(() => {
      if (!isOpen) return;

      const handleClickOutside = (e: MouseEvent | TouchEvent) => {
        const target = e.target as Node;
        if (menuRef.current && !menuRef.current.contains(target)) {
          onClose();
        }
      };

      // Delay adding listener to prevent immediate close
      const timer = setTimeout(() => {
        document.addEventListener('mousedown', handleClickOutside);
        document.addEventListener('touchstart', handleClickOutside);
      }, 50);

      return () => {
        clearTimeout(timer);
        document.removeEventListener('mousedown', handleClickOutside);
        document.removeEventListener('touchstart', handleClickOutside);
      };
    }, [isOpen, onClose]);

    // Close on escape
    useEffect(() => {
      if (!isOpen) return;

      const handleEscape = (e: KeyboardEvent) => {
        if (e.key === 'Escape') {
          onClose();
        }
      };

      document.addEventListener('keydown', handleEscape);
      return () => document.removeEventListener('keydown', handleEscape);
    }, [isOpen, onClose]);

    if (!isOpen || items.length === 0) {
      return null;
    }

    return (
      <div
        ref={ref}
        className={cn(
          // Positioning
          'absolute bottom-full right-0 mb-3',

          // Appearance
          'bg-background/95 dark:bg-card/95',
          'backdrop-blur-lg',
          'border border-border/50',
          'rounded-xl shadow-lg',

          // Animation
          'animate-in fade-in-0 zoom-in-95 slide-in-from-bottom-4',
          'duration-150',

          // Padding
          'p-2',

          className
        )}
        role="menu"
        aria-label="Azioni rapide"
        {...props}
      >
        <div ref={menuRef} className="flex flex-col gap-1">
          {items.map((item, index) => {
            const IconComponent = ICON_MAP[item.icon];

            return (
              <Button
                key={item.id}
                variant="ghost"
                onClick={() => onSelect(item.id)}
                className={cn(
                  'flex items-center gap-3 justify-start',
                  'min-h-[44px] px-4',
                  'animate-in fade-in-0 slide-in-from-right-2',
                  'hover:bg-accent'
                )}
                style={{
                  animationDelay: `${index * 30}ms`,
                  animationFillMode: 'backwards',
                }}
                role="menuitem"
              >
                {IconComponent && (
                  <IconComponent
                    className="h-5 w-5 shrink-0"
                    aria-hidden="true"
                  />
                )}
                <span className="text-sm font-medium">{item.label}</span>
              </Button>
            );
          })}
        </div>
      </div>
    );
  }
);

QuickMenu.displayName = 'QuickMenu';
