/**
 * Breadcrumb Component
 * Issue #3292 - Phase 6: Breadcrumb & Polish
 *
 * Context indicator showing current location in the app.
 * Displays icon and label based on current layout context.
 */

'use client';

import { forwardRef, type ComponentPropsWithoutRef } from 'react';
import {
  Library,
  Gamepad2,
  Play,
  MessageSquare,
  Search,
  Settings,
  Home,
  type LucideIcon,
} from 'lucide-react';

import { useLayout } from '@/components/layout/LayoutProvider';
import { cn } from '@/lib/utils';
import type { LayoutContext } from '@/types/layout';

/**
 * Context configuration with icon and label
 */
interface ContextConfig {
  icon: LucideIcon;
  label: string;
  emoji?: string;
}

/**
 * Context-to-display mapping
 */
const CONTEXT_CONFIG: Record<LayoutContext, ContextConfig> = {
  default: {
    icon: Home,
    label: 'Home',
    emoji: '🏠',
  },
  library: {
    icon: Library,
    label: 'La mia libreria',
    emoji: '📚',
  },
  game_detail: {
    icon: Gamepad2,
    label: 'Dettagli gioco',
    emoji: '🎲',
  },
  session_active: {
    icon: Play,
    label: 'Sessione attiva',
    emoji: '▶️',
  },
  chat: {
    icon: MessageSquare,
    label: 'Chat AI',
    emoji: '🤖',
  },
  search: {
    icon: Search,
    label: 'Ricerca',
    emoji: '🔍',
  },
  settings: {
    icon: Settings,
    label: 'Impostazioni',
    emoji: '⚙️',
  },
};

export interface BreadcrumbProps
  extends Omit<ComponentPropsWithoutRef<'nav'>, 'children'> {
  /** Whether to show emoji instead of icon */
  useEmoji?: boolean;
  /** Custom context label override */
  customLabel?: string;
}

/**
 * Breadcrumb Component
 *
 * Shows the current context with an icon and label.
 * Positioned above the ActionBar on mobile.
 */
export const Breadcrumb = forwardRef<HTMLElement, BreadcrumbProps>(
  function Breadcrumb(
    { useEmoji = false, customLabel, className, ...props },
    ref
  ) {
    const { context, responsive } = useLayout();
    const { isMobile } = responsive;

    const config = CONTEXT_CONFIG[context];
    const IconComponent = config.icon;
    const displayLabel = customLabel ?? config.label;

    // Only show on mobile
    if (!isMobile) {
      return null;
    }

    return (
      <nav
        ref={ref}
        role="navigation"
        aria-label="Contesto corrente"
        className={cn(
          // Positioning
          'fixed z-30',
          'left-4 right-20', // Leave space for FAB
          'bottom-[72px]', // Above ActionBar

          // Appearance
          'h-8',
          'bg-card/90 dark:bg-card/80',
          'backdrop-blur-md',
          'border border-border/30',
          'rounded-full',

          // Shadow
          'shadow-sm',

          // Animation
          'animate-in fade-in-0 slide-in-from-bottom-2',
          'duration-150',

          className
        )}
        {...props}
      >
        <div className="h-full flex items-center gap-2 px-3">
          {/* Icon or Emoji */}
          {useEmoji ? (
            <span className="text-sm" aria-hidden="true">
              {config.emoji}
            </span>
          ) : (
            <IconComponent
              className="h-4 w-4 text-muted-foreground"
              aria-hidden="true"
            />
          )}

          {/* Label */}
          <span className="text-sm font-medium text-foreground truncate">
            {displayLabel}
          </span>
        </div>

        {/* Screen reader announcement */}
        <span className="sr-only">
          Sei in: {displayLabel}
        </span>
      </nav>
    );
  }
);

Breadcrumb.displayName = 'Breadcrumb';

/**
 * Get context configuration for external use
 */
export function getContextConfig(context: LayoutContext): ContextConfig {
  return CONTEXT_CONFIG[context];
}
