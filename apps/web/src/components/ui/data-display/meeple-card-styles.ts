import { cva } from 'class-variance-authority';

// ============================================================================
// Entity Types (shared across meeple-card-*.tsx)
// ============================================================================

/**
 * Supported entity types with semantic colors
 * Issue #4030: Extended from 5 to 9 types (game, player, session, agent, kb, chatSession, event, toolkit, custom)
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
  tool: { hsl: '195 80% 50%', name: 'Tool' }, // Sky Blue
  custom: { hsl: '220 70% 50%', name: 'Custom' }, // Blue (default)
};

// Map MeepleEntityType → DrawerEntityType for ExtraMeepleCardDrawer (Issue #5025)
// Entities absent from this map do not render an info button.
export const DRAWER_ENTITY_TYPE_MAP: Partial<
  Record<MeepleEntityType, 'game' | 'agent' | 'chat' | 'kb' | 'links'>
> = {
  game: 'game',
  agent: 'agent',
  chatSession: 'chat',
  kb: 'kb',
};

// ============================================================================
// CVA Variants
// ============================================================================

export const meepleCardVariants = cva(
  // Base styles for all variants — v2 warm styling (Issue #4604)
  [
    'group relative overflow-hidden cursor-pointer',
    'transition-all duration-[350ms] ease-[cubic-bezier(0.4,0,0.2,1)]',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
  ],
  {
    variants: {
      variant: {
        grid: [
          'flex flex-col rounded-2xl overflow-hidden',
          'bg-card border border-border/50',
          '[box-shadow:var(--shadow-warm-sm)] hover:[box-shadow:var(--shadow-warm-xl)]',
        ],
        list: [
          'flex flex-row items-center gap-4 p-3 rounded-xl',
          'bg-card border border-border/50',
          '[box-shadow:var(--shadow-warm-sm)] hover:[box-shadow:var(--shadow-warm-md)]',
          'hover:translate-x-1',
        ],
        compact: [
          'flex flex-row items-center gap-2 p-2 rounded-lg',
          'bg-card/80 border border-border/30',
          'hover:bg-card',
        ],
        featured: [
          'flex flex-col rounded-2xl overflow-hidden',
          'bg-card border border-border/50',
          '[box-shadow:var(--shadow-warm-md)] hover:[box-shadow:var(--shadow-warm-xl)]',
        ],
        hero: [
          'relative flex flex-col rounded-3xl overflow-hidden',
          'min-h-[320px]',
          '[box-shadow:var(--shadow-warm-xl)] hover:[box-shadow:var(--shadow-warm-2xl)]',
          'hover:scale-[1.01]',
        ],
        expanded: [
          'flex flex-col rounded-2xl overflow-hidden',
          'bg-card border border-border/50',
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
