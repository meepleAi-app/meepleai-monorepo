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
  User,
  Heart,
  Bell,
  FileText,
  PlayCircle,
  CheckCircle,
  ListPlus,
  PlusCircle,
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
 * Issue #3479 - Extended contexts for Layout System v2
 */
const CONTEXT_CONFIG: Record<LayoutContext, ContextConfig> = {
  default: {
    icon: Home,
    label: 'Home',
    emoji: '🏠',
  },
  dashboard: {
    icon: Home,
    label: 'Dashboard',
    emoji: '🏠',
  },
  library: {
    icon: Library,
    label: 'La mia libreria',
    emoji: '📚',
  },
  library_empty: {
    icon: PlusCircle,
    label: 'Aggiungi giochi',
    emoji: '➕',
  },
  library_selection: {
    icon: ListPlus,
    label: 'Selezione',
    emoji: '☑️',
  },
  game_detail: {
    icon: Gamepad2,
    label: 'Dettagli gioco',
    emoji: '🎲',
  },
  game_detail_not_owned: {
    icon: Gamepad2,
    label: 'Dettagli gioco',
    emoji: '🎲',
  },
  session_setup: {
    icon: PlayCircle,
    label: 'Preparazione partita',
    emoji: '🎯',
  },
  session_active: {
    icon: Play,
    label: 'Sessione attiva',
    emoji: '▶️',
  },
  session_end: {
    icon: CheckCircle,
    label: 'Fine partita',
    emoji: '🏆',
  },
  document_viewer: {
    icon: FileText,
    label: 'Documento',
    emoji: '📄',
  },
  chat: {
    icon: MessageSquare,
    label: 'Chat AI',
    emoji: '🤖',
  },
  catalog: {
    icon: Gamepad2,
    label: 'Catalogo',
    emoji: '🎮',
  },
  search: {
    icon: Search,
    label: 'Ricerca',
    emoji: '🔍',
  },
  wishlist: {
    icon: Heart,
    label: 'Wishlist',
    emoji: '❤️',
  },
  notifications: {
    icon: Bell,
    label: 'Notifiche',
    emoji: '🔔',
  },
  profile: {
    icon: User,
    label: 'Profilo',
    emoji: '👤',
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

    // eslint-disable-next-line security/detect-object-injection -- context is from typed LayoutContext union
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
  // eslint-disable-next-line security/detect-object-injection -- context is from typed LayoutContext union
  return CONTEXT_CONFIG[context];
}
