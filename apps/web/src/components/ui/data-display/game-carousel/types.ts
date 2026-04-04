import type { MeepleCardMetadata } from '../meeple-card';
import type { Star } from 'lucide-react';

export interface CarouselGame {
  id: string;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
  ratingMax?: number;
  metadata?: MeepleCardMetadata[];
  badge?: string;
  description?: string;
  /** Whether the game has an indexed knowledge base for RAG-based chat */
  hasKb?: boolean;
}

/**
 * Sort option values for carousel
 */
export type CarouselSortValue = 'rating' | 'popularity' | 'name' | 'date';

/**
 * Sort option configuration
 */
export interface CarouselSortOption {
  value: CarouselSortValue;
  label: string;
  icon: typeof Star;
}

export interface GameCarouselProps {
  /** Array of games to display */
  games: CarouselGame[];
  /** Callback when a game card is clicked */
  onGameSelect?: (game: CarouselGame) => void;
  /** Title above the carousel */
  title?: string;
  /** Subtitle/description */
  subtitle?: string;
  /** Enable auto-play rotation */
  autoPlay?: boolean;
  /** Auto-play interval in ms (default: 5000) */
  autoPlayInterval?: number;
  /** Show navigation dots */
  showDots?: boolean;
  /** Enable sorting controls */
  sortable?: boolean;
  /** Default sort value */
  defaultSort?: CarouselSortValue;
  /** Current sort value (controlled) */
  sort?: CarouselSortValue;
  /** Callback when sort changes */
  onSortChange?: (sort: CarouselSortValue) => void;
  /** Enable flip on carousel cards */
  flippable?: boolean;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
  /** Current user ID (for auth-gated actions) */
  userId?: string;
}
