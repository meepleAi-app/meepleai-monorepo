import { cva } from 'class-variance-authority';

// ============================================================================
// Entity Types (shared across meeple-card-*.tsx)
// ============================================================================

/**
 * Supported entity types with semantic colors
 * Issue #4030: Extended from 5 to 9 types (game, player, session, agent, kb, chatSession, event, toolkit, custom)
 * Mana system: Extended to 16 types (added collection, group, location, expansion, achievement, note)
 */
export type MeepleEntityType =
  | 'game'
  | 'player'
  | 'session'
  | 'agent'
  | 'kb'
  | 'chatSession'
  | 'event'
  | 'toolkit'
  | 'tool'
  | 'collection'
  | 'group'
  | 'location'
  | 'expansion'
  | 'achievement'
  | 'note'
  | 'custom';

/**
 * Layout variant options
 */
export type MeepleCardVariant = 'grid' | 'list' | 'compact' | 'featured' | 'hero' | 'expanded';

// ============================================================================
// Entity Color Configuration
// ============================================================================

export const entityColors: Record<MeepleEntityType, { hsl: string; name: string }> = {
  game: { hsl: '25 95% 45%', name: 'Game' }, // Orange
  player: { hsl: '262 83% 58%', name: 'Player' }, // Purple
  session: { hsl: '240 60% 55%', name: 'Session' }, // Indigo
  agent: { hsl: '38 92% 50%', name: 'Agent' }, // Amber
  kb: { hsl: '174 60% 40%', name: 'KB' }, // Teal
  chatSession: { hsl: '220 80% 55%', name: 'Chat' }, // Blue
  event: { hsl: '350 89% 60%', name: 'Event' }, // Rose
  toolkit: { hsl: '142 70% 45%', name: 'Toolkit' }, // Green
  tool: { hsl: '195 80% 50%', name: 'Tool' }, // Sky Blue (Epic #412)
  collection: { hsl: '20 70% 42%', name: 'Copper' },
  group: { hsl: '280 50% 48%', name: 'Warm Violet' },
  location: { hsl: '200 55% 45%', name: 'Slate Cyan' },
  expansion: { hsl: '290 65% 50%', name: 'Magenta' },
  achievement: { hsl: '45 90% 48%', name: 'Gold' },
  note: { hsl: '40 30% 42%', name: 'Warm Gray' },
  custom: { hsl: '220 15% 45%', name: 'Custom' }, // Silver (Mana spec)
};

// Map MeepleEntityType → DrawerEntityType for ExtraMeepleCardDrawer (Issue #5025)
// Entities absent from this map do not render an info button.
// Extended to all 16 entity types (Mana system); new types map to themselves
// and will render DrawerComingSoon until dedicated content is implemented.
// Note: DrawerEntityType = MeepleEntityType | 'chat' | 'links'
// 'chat' is the legacy alias for 'chatSession' kept for backward compat.
export const DRAWER_ENTITY_TYPE_MAP: Partial<
  Record<MeepleEntityType, MeepleEntityType | 'chat' | 'links'>
> = {
  game: 'game',
  agent: 'agent',
  chatSession: 'chat', // keep 'chat' as value for backward compat
  kb: 'kb',
  // New types — map to themselves (drawer will show Coming Soon)
  collection: 'collection',
  group: 'group',
  location: 'location',
  expansion: 'expansion',
  achievement: 'achievement',
  note: 'note',
  session: 'session',
  player: 'player',
  event: 'event',
  toolkit: 'toolkit',
  tool: 'tool',
  custom: 'custom',
};

// ============================================================================
// CVA Variants
// ============================================================================

export const meepleCardVariants = cva(
  // Base styles for all variants — v2 warm styling (Issue #4604)
  // Env-aware: reads --env-card-lift from parent .env-tavolo/.env-hub
  [
    'group relative overflow-hidden cursor-pointer',
    'transition-all duration-[350ms] ease-[cubic-bezier(0.4,0,0.2,1)]',
    'motion-reduce:transition-none',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
  ],
  {
    variants: {
      variant: {
        grid: [
          'flex flex-col rounded-2xl overflow-hidden',
          'bg-[var(--nh-bg-surface)] border border-[var(--nh-border-default)]',
          'border-l-[3px] [border-left-color:var(--mc-entity-color,transparent)]',
          '[box-shadow:var(--shadow-warm-sm)] hover:[box-shadow:var(--shadow-warm-xl)]',
          // Neon Holo: 3D tilt + performance containment
          'hover:-translate-y-2',
          '[contain:layout_paint]',
        ],
        list: [
          'flex flex-row items-center gap-4 p-3 rounded-xl',
          'bg-[var(--nh-bg-surface)] border border-[var(--nh-border-default)]',
          'border-l-[3px] [border-left-color:var(--mc-entity-color,transparent)]',
          '[box-shadow:var(--shadow-warm-sm)] hover:[box-shadow:var(--shadow-warm-md)]',
          'hover:translate-x-1',
        ],
        compact: [
          'flex flex-row items-center gap-2 p-2 rounded-lg',
          'bg-card/80 border border-border/30',
          'border-l-[3px] [border-left-color:var(--mc-entity-color,transparent)]',
          'hover:bg-card',
        ],
        featured: [
          'flex flex-col rounded-2xl overflow-hidden',
          'bg-[var(--nh-bg-surface)] border border-[var(--nh-border-default)]',
          'border-l-[3px] [border-left-color:var(--mc-entity-color,transparent)]',
          '[box-shadow:var(--shadow-warm-md)] hover:[box-shadow:var(--shadow-warm-xl)]',
          // Neon Holo: hover lift + performance containment
          'hover:-translate-y-2',
          '[contain:layout_paint]',
        ],
        hero: [
          'relative flex flex-col rounded-3xl overflow-hidden',
          'min-h-[320px]',
          '[box-shadow:var(--shadow-warm-xl)] hover:[box-shadow:var(--shadow-warm-2xl)]',
          'hover:scale-[1.01]',
          '[background-image:var(--texture-parchment)]',
        ],
        expanded: [
          'flex flex-col rounded-2xl overflow-hidden',
          'bg-[var(--nh-bg-surface)] border border-[var(--nh-border-default)]',
          'border-l-[3px] [border-left-color:var(--mc-entity-color,transparent)]',
          '[box-shadow:var(--shadow-warm-md)]',
        ],
      },
    },
    defaultVariants: {
      variant: 'grid',
    },
  }
);

export const coverVariants = cva('relative overflow-hidden', {
  variants: {
    variant: {
      grid: 'rounded-t-2xl',
      list: 'w-16 h-16 rounded-lg flex-shrink-0',
      compact: 'w-10 h-10 rounded-md flex-shrink-0',
      featured: '',
      hero: 'absolute inset-0',
      expanded: 'w-full rounded-t-2xl',
    },
  },
  defaultVariants: { variant: 'grid' },
});

export const contentVariants = cva('', {
  variants: {
    variant: {
      grid: 'flex-1 flex flex-col px-2.5 py-2 sm:px-3.5 sm:py-3',
      list: 'flex-1 min-w-0 py-1',
      compact: 'flex-1 min-w-0',
      featured: 'flex-1 flex flex-col px-5 py-4',
      hero: 'relative z-10 mt-auto flex flex-col justify-end p-6',
      expanded: 'flex-1 flex flex-col px-4 py-3 gap-2',
    },
  },
  defaultVariants: { variant: 'grid' },
});

// ============================================================================
// Cover Overlay Styles (MtG-inspired)
// ============================================================================

/**
 * Styles for the MtG-inspired cover overlay slots.
 * - container: absolute bar at bottom of cover image
 * - subtypeIcon: bottom-left classification icon slot
 * - stateLabel: bottom-right state badge slot with semantic color variants
 */
export const coverOverlayStyles = {
  container:
    'absolute bottom-0 left-0 right-0 flex items-end justify-between p-1.5 pointer-events-none',
  subtypeIcon: 'pointer-events-auto rounded-sm bg-black/60 p-0.5 backdrop-blur-sm',
  stateLabel: {
    base: 'pointer-events-auto rounded-sm px-1.5 py-0.5 text-[10px] font-semibold backdrop-blur-sm',
    success: 'bg-emerald-500/80 text-white',
    warning: 'bg-amber-500/80 text-black',
    error: 'bg-red-500/80 text-white',
    info: 'bg-blue-500/80 text-white',
  },
} as const;
