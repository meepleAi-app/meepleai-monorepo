import type { ReactNode } from 'react';

import type { ManaPip } from './parts/ManaPips';

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

// 6 variants
export type MeepleCardVariant = 'grid' | 'list' | 'compact' | 'featured' | 'hero' | 'focus';

export interface MeepleCardMetadata {
  icon?: ReactNode;
  label: string;
  value?: string;
}

export interface MeepleCardAction {
  icon: ReactNode;
  label: string;
  onClick: () => void;
  variant?: 'default' | 'primary' | 'danger';
  disabled?: boolean;
}

export interface NavFooterItem {
  icon: ReactNode;
  label: string;
  entity: MeepleEntityType;
  count?: number;
  showPlus?: boolean;
  disabled?: boolean;
  onClick?: () => void;
  onPlusClick?: () => void;
  href?: string;
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

export interface ConnectionItem {
  id: string;
  label: string;
  href: string;
}

export interface ConnectionChipProps {
  entityType: MeepleEntityType;
  count?: number;
  items?: ConnectionItem[];
  size?: 'sm' | 'md';
  showLabel?: boolean;
  label?: string;
  onCreate?: () => void;
  createLabel?: string;
  href?: string;
  colorOverride?: string;
  disabled?: boolean;
  loading?: boolean;
  /**
   * Optional icon node to render instead of the default Lucide icon for `entityType`.
   * Used by the `navItems → connections` adapter to preserve custom icons.
   */
  iconOverride?: import('react').ReactNode;
}

export type OwnershipBadge = 'owned' | 'wishlist' | 'archived';

export type LifecycleState = 'active' | 'idle' | 'completed' | 'setup' | 'processing' | 'failed';

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
  manaPips?: ManaPip[];
  connections?: ConnectionChipProps[];
  connectionsVariant?: 'footer' | 'inline' | 'auto';
  ownership?: OwnershipBadge;
  lifecycle?: LifecycleState;
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
  /** Optional test id forwarded to the root wrapper element. */
  'data-testid'?: string;
  /** @internal Opt-in: use ConnectionChipStrip for navItems-source rendering (A/B in tests). Remove in Step 2. */
  __useConnectionsForNavItems?: boolean;
}

export interface Carousel3DProps {
  cards: MeepleCardProps[];
  activeIndex?: number;
  onNavigate?: (index: number) => void;
  className?: string;
}
