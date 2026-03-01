/**
 * GameCarousel Story
 */

import { GameCarousel, type CarouselGame } from '@/components/ui/data-display/game-carousel';

import type { ShowcaseStory } from '../types';

const SAMPLE_CAROUSEL_GAMES: CarouselGame[] = [
  { id: '1', title: 'Twilight Imperium 4th Ed.', subtitle: 'Fantasy Flight Games', rating: 8.4, ratingMax: 10, badge: 'Epic' },
  { id: '2', title: 'Gloomhaven', subtitle: 'Cephalofair Games', rating: 8.7, ratingMax: 10, badge: 'Best Seller' },
  { id: '3', title: 'Spirit Island', subtitle: 'Greater Than Games', rating: 8.3, ratingMax: 10 },
  { id: '4', title: 'Wingspan', subtitle: 'Stonemaier Games', rating: 7.9, ratingMax: 10 },
];

type GameCarouselShowcaseProps = {
  autoPlay: boolean;
  showDots: boolean;
  sortable: boolean;
  flippable: boolean;
  title: string;
};

export const gameCarouselStory: ShowcaseStory<GameCarouselShowcaseProps> = {
  id: 'game-carousel',
  title: 'GameCarousel',
  category: 'Data Display',
  description: 'Card-based carousel for game discovery with optional sorting and flip.',

  component: function GameCarouselStory({ autoPlay, showDots, sortable, flippable, title }: GameCarouselShowcaseProps) {
    return (
      <div className="w-[700px]">
        <GameCarousel
          games={SAMPLE_CAROUSEL_GAMES}
          autoPlay={autoPlay}
          showDots={showDots}
          sortable={sortable}
          flippable={flippable}
          title={title || undefined}
        />
      </div>
    );
  },

  defaultProps: {
    autoPlay: false,
    showDots: true,
    sortable: false,
    flippable: false,
    title: 'Featured Games',
  },

  controls: {
    title: { type: 'text', label: 'title', default: 'Featured Games' },
    autoPlay: { type: 'boolean', label: 'autoPlay', default: false },
    showDots: { type: 'boolean', label: 'showDots', default: true },
    sortable: { type: 'boolean', label: 'sortable', default: false },
    flippable: { type: 'boolean', label: 'flippable', default: false },
  },

  presets: {
    default: { label: 'Default', props: { autoPlay: false, showDots: true, title: 'Featured Games' } },
    autoplay: { label: 'Autoplay', props: { autoPlay: true, showDots: true } },
    withSort: { label: 'With Sort', props: { sortable: true } },
    flippable: { label: 'Flippable', props: { flippable: true } },
  },
};
