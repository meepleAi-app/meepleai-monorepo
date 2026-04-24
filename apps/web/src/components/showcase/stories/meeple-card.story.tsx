/**
 * MeepleCard Story
 * Demonstrates all entity types, variants, feature toggles and NavFooter states.
 *
 * Mirrors the visual scenarios documented in
 * admin-mockups/meeple-card-nav-buttons-mockup.html
 */

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import {
  buildAgentNavItems,
  buildChatConnections,
  buildGameNavItems,
  buildKbConnections,
  buildSessionNavItems,
} from '@/components/ui/data-display/meeple-card/nav-items';
import type {
  ConnectionChipProps,
  NavFooterItem,
} from '@/components/ui/data-display/meeple-card/types';

import type { ShowcaseStory } from '../types';

type NavPreset = 'none' | 'game' | 'session' | 'kb' | 'agent' | 'chat';

type MeepleCardShowcaseProps = {
  entity: string;
  variant: string;
  title: string;
  subtitle: string;
  rating: number;
  showWishlist: boolean;
  loading: boolean;
  badge: string;
  selectable: boolean;
  navPreset: string;
  navDisabled: boolean;
  navShowCounts: boolean;
};

const noop = () => undefined;

function buildNavItems(
  preset: NavPreset,
  { showCounts, disabled }: { showCounts: boolean; disabled: boolean }
): NavFooterItem[] | undefined {
  if (preset === 'none') return undefined;

  const counts = showCounts
    ? { kbCount: 4, agentCount: 2, chatCount: 7, sessionCount: 3, playerCount: 5, chunkCount: 42 }
    : { kbCount: 0, agentCount: 0, chatCount: 0, sessionCount: 0, playerCount: 0, chunkCount: 0 };

  const handlers = disabled
    ? {}
    : { onKbClick: noop, onAgentClick: noop, onChatClick: noop, onSessionClick: noop };

  let items: NavFooterItem[];
  switch (preset) {
    case 'game':
      items = buildGameNavItems(
        {
          kbCount: counts.kbCount,
          agentCount: counts.agentCount,
          chatCount: counts.chatCount,
          sessionCount: counts.sessionCount,
        },
        handlers
      );
      break;
    case 'session':
      items = buildSessionNavItems(
        {
          playerCount: counts.playerCount,
          hasNotes: showCounts,
          toolCount: showCounts ? 2 : 0,
          photoCount: showCounts ? 6 : 0,
        },
        disabled
          ? {}
          : {
              onPlayersClick: noop,
              onNotesClick: noop,
              onToolsClick: noop,
              onPhotosClick: noop,
            }
      );
      break;
    case 'kb': {
      // TODO(task-8): remove cast when showcase switches navItems= → connections= in the cleanup commit
      const kbConnections: ConnectionChipProps[] = buildKbConnections(
        { chunkCount: counts.chunkCount },
        disabled
          ? {}
          : {
              onChunksClick: noop,
              onReindexClick: noop,
              onPreviewClick: noop,
              onDownloadClick: noop,
            }
      );
      items = kbConnections as unknown as NavFooterItem[];
      break;
    }
    case 'chat': {
      // TODO(task-8): remove cast when showcase switches navItems= → connections= in the cleanup commit
      const chatConnections: ConnectionChipProps[] = buildChatConnections(
        { messageCount: showCounts ? 24 : 0 },
        disabled
          ? {}
          : {
              onMessagesClick: noop,
              onAgentLinkClick: noop,
              onSourcesClick: noop,
              onArchiveClick: noop,
            }
      );
      items = chatConnections as unknown as NavFooterItem[];
      break;
    }
    case 'agent':
      items = buildAgentNavItems(
        { chatCount: counts.chatCount, kbCount: counts.kbCount },
        disabled
          ? {}
          : {
              onChatClick: noop,
              onKbClick: noop,
              onMemoryClick: noop,
              onConfigClick: noop,
            }
      );
      break;
    default:
      return undefined;
  }

  // Force-disable first item to demonstrate the "locked feature" state (§5 of mockup)
  if (disabled && items.length > 0) {
    items[0] = { ...items[0], disabled: true };
  }

  return items;
}

export const meepleCardStory: ShowcaseStory<MeepleCardShowcaseProps> = {
  id: 'meeple-card',
  title: 'MeepleCard',
  category: 'Data Display',
  description:
    'Universal card component for games, players, agents, sessions, and more. Includes NavFooter states (grid/list/featured/disabled) matching the admin mockup.',

  component: function MeepleCardStory({
    entity,
    variant,
    title,
    subtitle,
    rating,
    showWishlist: _showWishlist,
    loading: _loading,
    badge,
    selectable: _selectable,
    navPreset,
    navDisabled,
    navShowCounts,
  }: MeepleCardShowcaseProps) {
    const navItems = buildNavItems(navPreset as NavPreset, {
      showCounts: navShowCounts,
      disabled: navDisabled,
    });

    return (
      <div className="w-72">
        <MeepleCard
          entity={entity as 'game' | 'player' | 'agent' | 'session' | 'kb' | 'event'}
          variant={variant as 'grid' | 'list' | 'compact' | 'featured' | 'hero'}
          title={title}
          subtitle={subtitle}
          rating={rating > 0 ? rating : undefined}
          ratingMax={10}
          badge={badge || undefined}
          navItems={navItems}
          imageUrl={
            entity === 'game'
              ? 'https://cf.geekdo-images.com/WPKk3MeT3EKhKnhFLB8OoA__itemrep/img/yJB95GXRb10MKzqxKOXGKjgMrPQ=/fit-in/246x300/filters:strip_icc()/pic3490053.jpg'
              : undefined
          }
        />
      </div>
    );
  },

  defaultProps: {
    entity: 'game',
    variant: 'grid',
    title: 'Catan',
    subtitle: 'Mayfair Games',
    rating: 7.2,
    showWishlist: false,
    loading: false,
    badge: '',
    selectable: false,
    navPreset: 'none',
    navDisabled: false,
    navShowCounts: true,
  },

  controls: {
    entity: {
      type: 'select',
      label: 'entity',
      options: ['game', 'player', 'agent', 'session', 'kb', 'event'],
      default: 'game',
    },
    variant: {
      type: 'select',
      label: 'variant',
      options: ['grid', 'list', 'compact', 'featured', 'hero'],
      default: 'grid',
    },
    title: { type: 'text', label: 'title', default: 'Catan' },
    subtitle: { type: 'text', label: 'subtitle', default: 'Mayfair Games' },
    rating: { type: 'range', label: 'rating', min: 0, max: 10, step: 0.1, default: 7.2 },
    badge: { type: 'text', label: 'badge', default: '' },
    showWishlist: { type: 'boolean', label: 'showWishlist', default: false },
    selectable: { type: 'boolean', label: 'selectable', default: false },
    loading: { type: 'boolean', label: 'loading', default: false },
    navPreset: {
      type: 'select',
      label: 'navPreset',
      options: ['none', 'game', 'session', 'kb', 'agent', 'chat'],
      default: 'none',
    },
    navDisabled: { type: 'boolean', label: 'navDisabled', default: false },
    navShowCounts: { type: 'boolean', label: 'navShowCounts', default: true },
  },

  presets: {
    default: {
      label: 'Default',
      props: {
        entity: 'game',
        variant: 'grid',
        title: 'Catan',
        subtitle: 'Mayfair Games',
        rating: 7.2,
        navPreset: 'none',
      },
    },
    withRating: {
      label: 'With Rating',
      props: {
        entity: 'game',
        title: 'Twilight Imperium',
        subtitle: 'FFG',
        rating: 8.4,
        badge: 'Hot',
      },
    },
    withWishlist: {
      label: 'With Wishlist',
      props: { showWishlist: true, title: 'Pandemic', subtitle: 'Z-Man Games', rating: 7.6 },
    },
    // ─── NavFooter presets — mirror the 8 sections of the admin mockup ────────
    gameWithNav: {
      label: 'Game + NavFooter',
      props: {
        entity: 'game',
        variant: 'grid',
        title: 'Brass: Birmingham',
        subtitle: 'Roxley Games',
        rating: 8.6,
        navPreset: 'game',
        navShowCounts: true,
      },
    },
    gameFeaturedNav: {
      label: 'Featured + NavFooter',
      props: {
        entity: 'game',
        variant: 'featured',
        title: 'Gloomhaven',
        subtitle: 'Cephalofair Games',
        rating: 8.8,
        navPreset: 'game',
        navShowCounts: true,
      },
    },
    sessionWithNav: {
      label: 'Session + NavFooter',
      props: {
        entity: 'session',
        variant: 'grid',
        title: 'Partita #12 — Sabato sera',
        subtitle: '2h 45m · 4 giocatori',
        rating: 0,
        navPreset: 'session',
        navShowCounts: true,
      },
    },
    kbWithNav: {
      label: 'KB Document + NavFooter',
      props: {
        entity: 'kb',
        variant: 'grid',
        title: 'Catan Rulebook.pdf',
        subtitle: '42 chunks · Indexed',
        rating: 0,
        navPreset: 'kb',
        navShowCounts: true,
      },
    },
    agentWithNav: {
      label: 'Agent + NavFooter',
      props: {
        entity: 'agent',
        variant: 'grid',
        title: 'Catan Expert',
        subtitle: 'RAG Agent',
        rating: 0,
        navPreset: 'agent',
        navShowCounts: true,
      },
    },
    navDisabled: {
      label: 'NavFooter Disabled',
      props: {
        entity: 'game',
        variant: 'grid',
        title: 'Wingspan',
        subtitle: 'Stonemaier Games',
        rating: 8.1,
        navPreset: 'game',
        navDisabled: true,
        navShowCounts: false,
      },
    },
    compact: {
      label: 'Compact',
      props: { variant: 'compact', title: 'Agricola', subtitle: 'Lookout Games' },
    },
    loading: { label: 'Loading', props: { loading: true } },
  },
};
