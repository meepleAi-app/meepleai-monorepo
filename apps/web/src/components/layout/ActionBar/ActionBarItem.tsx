/**
 * ActionBarItem Component
 * Issue #3290 - Phase 4: ActionBar System
 *
 * Single action button with icon and optional label.
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
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
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

export interface ActionBarItemProps
  extends Omit<ComponentPropsWithoutRef<'button'>, 'onClick'> {
  /** The action configuration */
  action: Action;
  /** Display variant */
  variant?: 'icon-only' | 'icon-label';
  /** Whether the action is currently active */
  isActive?: boolean;
  /** Whether the action is disabled */
  isDisabled?: boolean;
  /** Click handler */
  onClick?: (action: Action) => void;
  /** Animation delay for stagger effect (in ms) */
  animationDelay?: number;
}

/**
 * ActionBarItem Component
 *
 * Renders a single action button with icon and optional label.
 * Supports different variants and animation states.
 */
export const ActionBarItem = forwardRef<HTMLButtonElement, ActionBarItemProps>(
  function ActionBarItem(
    {
      action,
      variant = 'icon-only',
      isActive = false,
      isDisabled = false,
      onClick,
      animationDelay = 0,
      className,
      ...props
    },
    ref
  ) {
    // Get the icon component
    const IconComponent = typeof action.icon === 'string'
      ? ICON_MAP[action.icon]
      : null;

    // Determine button variant based on action variant
    const buttonVariant = action.variant === 'primary'
      ? 'default'
      : action.variant === 'destructive'
        ? 'destructive'
        : 'ghost';

    const handleClick = () => {
      if (!isDisabled && onClick) {
        onClick(action);
      }
    };

    const button = (
      <Button
        ref={ref}
        variant={buttonVariant}
        size={variant === 'icon-only' ? 'icon' : 'sm'}
        disabled={isDisabled || action.isDisabled}
        onClick={handleClick}
        className={cn(
          // Base styles
          'relative flex items-center justify-center gap-2',
          'min-h-[44px] min-w-[44px]', // Touch target
          'transition-all duration-200',

          // Active state
          isActive && 'bg-accent text-accent-foreground',

          // Animation
          'animate-in fade-in-0 slide-in-from-bottom-2',

          // Icon-only styles
          variant === 'icon-only' && 'rounded-full',

          // Icon-label styles
          variant === 'icon-label' && 'flex-col py-1.5 px-3 h-auto',

          className
        )}
        style={{
          animationDelay: `${animationDelay}ms`,
          animationFillMode: 'backwards',
        }}
        aria-label={action.label}
        aria-pressed={isActive}
        {...props}
      >
        {IconComponent && (
          <IconComponent
            className={cn(
              'h-5 w-5',
              variant === 'icon-label' && 'h-4 w-4'
            )}
            aria-hidden="true"
          />
        )}
        {variant === 'icon-label' && (
          <span className="text-xs font-medium truncate max-w-[56px]">
            {action.label}
          </span>
        )}
      </Button>
    );

    // Wrap in tooltip for icon-only variant
    if (variant === 'icon-only') {
      return (
        <TooltipProvider delayDuration={300}>
          <Tooltip>
            <TooltipTrigger asChild>
              {button}
            </TooltipTrigger>
            <TooltipContent side="top" sideOffset={8}>
              <p>{action.label}</p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      );
    }

    return button;
  }
);

ActionBarItem.displayName = 'ActionBarItem';
