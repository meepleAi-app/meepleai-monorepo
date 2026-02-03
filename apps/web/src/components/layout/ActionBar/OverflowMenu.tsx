/**
 * OverflowMenu Component
 * Issue #3290 - Phase 4: ActionBar System
 *
 * Dropdown menu for actions that don't fit in visible slots.
 */

'use client';

import { forwardRef, type ComponentPropsWithoutRef } from 'react';

import {
  Plus,
  Filter,
  ArrowUpDown,
  LayoutGrid,
  Download,
  Play,
  MessageSquare,
  Heart,
  Share,
  Flag,
  Timer,
  Trophy,
  Pause,
  Square,
  Send,
  Paperclip,
  Mic,
  PlusCircle,
  History,
  Bookmark,
  Save,
  RotateCcw,
  MoreHorizontal,
  type LucideIcon,
} from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';
import type { Action } from '@/types/layout';

/**
 * Icon mapping for action icons
 */
const ICON_MAP: Record<string, LucideIcon> = {
  plus: Plus,
  filter: Filter,
  'arrow-up-down': ArrowUpDown,
  'layout-grid': LayoutGrid,
  download: Download,
  play: Play,
  'message-square': MessageSquare,
  heart: Heart,
  share: Share,
  flag: Flag,
  timer: Timer,
  trophy: Trophy,
  pause: Pause,
  square: Square,
  send: Send,
  paperclip: Paperclip,
  mic: Mic,
  'plus-circle': PlusCircle,
  history: History,
  bookmark: Bookmark,
  save: Save,
  'rotate-ccw': RotateCcw,
  'more-horizontal': MoreHorizontal,
};

export interface OverflowMenuProps
  extends Omit<ComponentPropsWithoutRef<'div'>, 'children'> {
  /** Actions to show in the overflow menu */
  actions: Action[];
  /** Handler for action selection */
  onActionSelect?: (action: Action) => void;
  /** Animation delay for stagger effect (in ms) */
  animationDelay?: number;
}

/**
 * OverflowMenu Component
 *
 * Renders a dropdown menu containing overflow actions.
 * Uses shadcn/ui DropdownMenu for accessibility.
 */
export const OverflowMenu = forwardRef<HTMLDivElement, OverflowMenuProps>(
  function OverflowMenu(
    { actions, onActionSelect, animationDelay = 0, className, ...props },
    ref
  ) {
    if (actions.length === 0) {
      return null;
    }

    return (
      <div
        ref={ref}
        className={cn(
          'animate-in fade-in-0 slide-in-from-bottom-2',
          className
        )}
        style={{
          animationDelay: `${animationDelay}ms`,
          animationFillMode: 'backwards',
        }}
        {...props}
      >
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              className={cn(
                'min-h-[44px] min-w-[44px]',
                'rounded-full',
                'transition-all duration-200',
                'hover:bg-accent'
              )}
              aria-label="Altre azioni"
            >
              <MoreHorizontal className="h-5 w-5" aria-hidden="true" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent
            align="end"
            side="top"
            sideOffset={8}
            className={cn(
              'min-w-[180px]',
              'bg-background/95 backdrop-blur-lg',
              'border border-border/50',
              'shadow-lg'
            )}
          >
            {actions.map((action, index) => {
              const IconComponent = typeof action.icon === 'string'
                ? ICON_MAP[action.icon]
                : null;

              return (
                <DropdownMenuItem
                  key={action.id}
                  disabled={action.isDisabled}
                  onClick={() => onActionSelect?.(action)}
                  className={cn(
                    'flex items-center gap-3 px-3 py-2',
                    'cursor-pointer',
                    'min-h-[44px]', // Touch target
                    'animate-in fade-in-0 slide-in-from-left-2',
                    action.variant === 'destructive' && 'text-destructive focus:text-destructive'
                  )}
                  style={{
                    animationDelay: `${index * 30}ms`,
                    animationFillMode: 'backwards',
                  }}
                >
                  {IconComponent && (
                    <IconComponent
                      className="h-4 w-4 shrink-0"
                      aria-hidden="true"
                    />
                  )}
                  <span className="flex-1 truncate">{action.label}</span>
                </DropdownMenuItem>
              );
            })}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    );
  }
);

OverflowMenu.displayName = 'OverflowMenu';
