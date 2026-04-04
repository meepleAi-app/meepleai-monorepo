/**
 * RatingStars Story
 * Demonstrates the star rating display with BGG-scale conversion.
 */

'use client';

import { RatingStars } from '@/components/ui/data-display/rating-stars';

import type { ShowcaseStory } from '../types';

type RatingStarsShowcaseProps = {
  rating: number;
  maxRating: string;
  size: string;
  showValue: boolean;
  showHalfStars: boolean;
};

export const ratingStarsStory: ShowcaseStory<RatingStarsShowcaseProps> = {
  id: 'rating-stars',
  title: 'RatingStars',
  category: 'Data Display',
  description:
    'Star rating display with BGG 10-point scale conversion. Supports half-stars and value labels.',

  component: function RatingStarsStory({
    rating,
    maxRating,
    size,
    showValue,
    showHalfStars,
  }: RatingStarsShowcaseProps) {
    return (
      <div className="flex items-center gap-4 p-4">
        <RatingStars
          rating={rating}
          maxRating={Number(maxRating)}
          size={size as 'sm' | 'md' | 'lg'}
          showValue={showValue}
          showHalfStars={showHalfStars}
        />
      </div>
    );
  },

  defaultProps: {
    rating: 7.2,
    maxRating: '10',
    size: 'md',
    showValue: true,
    showHalfStars: true,
  },

  controls: {
    rating: { type: 'range', label: 'rating', min: 0, max: 10, step: 0.1, default: 7.2 },
    maxRating: {
      type: 'select',
      label: 'maxRating',
      options: ['5', '10'],
      default: '10',
    },
    size: {
      type: 'select',
      label: 'size',
      options: ['sm', 'md', 'lg'],
      default: 'md',
    },
    showValue: { type: 'boolean', label: 'showValue', default: true },
    showHalfStars: { type: 'boolean', label: 'showHalfStars', default: true },
  },

  presets: {
    high: { label: 'High Rating', props: { rating: 9.2, maxRating: '10', showValue: true } },
    medium: { label: 'Medium', props: { rating: 6.5, maxRating: '10', showValue: true } },
    low: { label: 'Low', props: { rating: 3.0, maxRating: '10', showValue: true } },
    compact: { label: 'Compact', props: { size: 'sm', showValue: false, rating: 7.8 } },
  },
};
