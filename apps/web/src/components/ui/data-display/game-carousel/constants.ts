import { ArrowDownAZ, Calendar, Star, TrendingUp } from 'lucide-react';

import type { CarouselSortOption } from './types';

/**
 * Available sort options
 */
export const CAROUSEL_SORT_OPTIONS: CarouselSortOption[] = [
  { value: 'rating', label: 'Rating', icon: Star },
  { value: 'popularity', label: 'Popularity', icon: TrendingUp },
  { value: 'name', label: 'Name', icon: ArrowDownAZ },
  { value: 'date', label: 'Date Added', icon: Calendar },
];

export const ANIMATION_DURATION = 500; // ms
export const SWIPE_THRESHOLD = 50; // px
