import type { ReactNode } from 'react';

// 9 entity types only
export type MeepleEntityType =
  | 'game'
  | 'player'
  | 'session'
  | 'agent'
  | 'kb'
  | 'chat'
  | 'event'
  | 'toolkit'
  | 'tool';

// 5 variants only
export type MeepleCardVariant = 'grid' | 'list' | 'compact' | 'featured' | 'hero';

export interface MeepleCardMetadata {
  icon?: string;
  label: string;
  value?: string;
}

export interface MeepleCardAction {
  icon: string;
  label: string;
  onClick: () => void;
  variant?: 'default' | 'primary' | 'danger';
  disabled?: boolean;
}

export interface NavFooterItem {
  icon: string;
  label: string;
  entity: MeepleEntityType;
  count?: number;
  showPlus?: boolean;
  disabled?: boolean;
  onClick?: () => void;
}

export type CardStatus =
  | 'owned'
  | 'wishlist'
  | 'active'
  | 'idle'
  | 'archived'
  | 'processing'
  | 'indexed'
  | 'failed'
  | 'inprogress'
  | 'setup'
  | 'completed'
  | 'paused';

export interface CoverLabel {
  text: string;
  color?: string;
}

export interface MeepleCardProps {
  entity: MeepleEntityType;
  title: string;
  variant?: MeepleCardVariant;
  id?: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
  ratingMax?: number;
  metadata?: MeepleCardMetadata[];
  tags?: string[];
  status?: CardStatus;
  badge?: string;
  coverLabels?: CoverLabel[];
  actions?: MeepleCardAction[];
  navItems?: NavFooterItem[];
  onClick?: () => void;
  flippable?: boolean;
  flipBackContent?: ReactNode;
  flipTrigger?: 'card' | 'button';
  draggable?: boolean;
  showQuickActions?: boolean;
  onWishlistToggle?: () => void;
  onDragStart?: () => void;
  onDragEnd?: () => void;
  className?: string;
  customColor?: string;
}

export interface MobileCardLayoutProps {
  cards: MeepleCardProps[];
  activeId?: string;
  onCardSelect?: (id: string) => void;
  className?: string;
}

export interface Carousel3DProps {
  cards: MeepleCardProps[];
  activeIndex?: number;
  onNavigate?: (index: number) => void;
  className?: string;
}
