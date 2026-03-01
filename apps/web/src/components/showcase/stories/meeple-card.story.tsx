/**
 * MeepleCard Story
 * Demonstrates all entity types, variants, and feature toggles.
 */

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

import type { ShowcaseStory } from '../types';

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
};

export const meepleCardStory: ShowcaseStory<MeepleCardShowcaseProps> = {
  id: 'meeple-card',
  title: 'MeepleCard',
  category: 'Data Display',
  description: 'Universal card component for games, players, agents, sessions, and more.',

  component: function MeepleCardStory({
    entity,
    variant,
    title,
    subtitle,
    rating,
    showWishlist,
    loading,
    badge,
    selectable,
  }: MeepleCardShowcaseProps) {
    return (
      <div className="w-72">
        <MeepleCard
          entity={entity as 'game' | 'player' | 'agent' | 'session' | 'document' | 'event'}
          variant={variant as 'grid' | 'list' | 'compact' | 'featured' | 'hero'}
          title={title}
          subtitle={subtitle}
          rating={rating > 0 ? rating : undefined}
          ratingMax={10}
          badge={badge || undefined}
          showWishlist={showWishlist}
          loading={loading}
          selectable={selectable}
          imageUrl={entity === 'game' ? 'https://cf.geekdo-images.com/WPKk3MeT3EKhKnhFLB8OoA__itemrep/img/yJB95GXRb10MKzqxKOXGKjgMrPQ=/fit-in/246x300/filters:strip_icc()/pic3490053.jpg' : undefined}
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
  },

  controls: {
    entity: {
      type: 'select',
      label: 'entity',
      options: ['game', 'player', 'agent', 'session', 'document', 'event'],
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
  },

  presets: {
    default: { label: 'Default', props: { entity: 'game', variant: 'grid', title: 'Catan', subtitle: 'Mayfair Games', rating: 7.2 } },
    withRating: { label: 'With Rating', props: { entity: 'game', title: 'Twilight Imperium', subtitle: 'FFG', rating: 8.4, badge: 'Hot' } },
    withWishlist: { label: 'With Wishlist', props: { showWishlist: true, title: 'Pandemic', subtitle: 'Z-Man Games', rating: 7.6 } },
    agent: { label: 'Agent', props: { entity: 'agent', title: 'Catan Expert', subtitle: 'RAG Agent', variant: 'grid', rating: 0 } },
    compact: { label: 'Compact', props: { variant: 'compact', title: 'Agricola', subtitle: 'Lookout Games' } },
    loading: { label: 'Loading', props: { loading: true } },
  },
};
