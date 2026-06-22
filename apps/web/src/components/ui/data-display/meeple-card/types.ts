import type { ReactNode } from 'react';

import type { ManaPip } from './parts/ManaPips';

// 10 entity types (#1929 WP2: added 'gameNightEvent' for cascade drawer flow)
export type MeepleEntityType =
  | 'game'
  | 'player'
  | 'session'
  | 'agent'
  | 'kb'
  | 'chat'
  | 'event'
  | 'toolkit'
  | 'tool'
  | 'gameNightEvent';

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
  /** Click handler for chip activation. Lower precedence than items (popover) and onCreate; higher precedence than href. When both `onClick` and `href` are provided, the chip renders as a Link and onClick fires on left-click while href preserves middle-click semantics. */
  onClick?: () => void;
  href?: string;
  colorOverride?: string;
  disabled?: boolean;
  loading?: boolean;
  /**
   * Optional icon node to render instead of the default Lucide icon for `entityType`.
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
  /**
   * UTF-8 emoji shown in the squat-band cover mode (when `imageUrl` is absent).
   * Falls back to `entityIcon[entity]` when omitted.
   * Example: 🎲 for game, 🎯 for session, 🤖 for agent.
   * Naming endorses existing FE convention (Toolkit.coverEmoji, play-records StatsHero.tsx:137).
   */
  coverEmoji?: string;
  /**
   * Semantic heading level for the card's title element (2, 3, or 4).
   * Default: 3 for most variants (GridCard/ListCard/FeaturedCard/HeroCard);
   * 2 for FocusCard (full-focus detail card).
   *
   * Pass `headingLevel={2}` when this card is rendered in a grid below an
   * `<h1>` hero — without this, axe-core flags `heading-order` (h1→h3 jump
   * skipping h2). See #1842 spec for the audit of 20 grid-below-hero
   * consumer surfaces that need `headingLevel={2}`.
   */
  headingLevel?: 2 | 3 | 4;
  rating?: number;
  ratingMax?: number;
  metadata?: MeepleCardMetadata[];
  /**
   * Render a small entity badge ABOVE the title (currently HeroCard only).
   * Mockup parity: `sp3-shared-game-detail.jsx` GameHero block — pill like
   * "🎲 GIOCO" / "🃏 SESSIONE" gives the reader a fast type signal before
   * the title. Opt-in so existing consumers keep their current rendering.
   */
  showEntityLabel?: boolean;
  /**
   * Display text for the entity badge (e.g. "Gioco", "Sessione"). When
   * omitted but `showEntityLabel` is true, the badge is hidden — callers
   * must pass the user-facing localized label explicitly to keep the design
   * system locale-agnostic.
   */
  entityLabel?: string;
  tags?: string[];
  status?: CardStatus;
  badge?: string;
  coverLabels?: CoverLabel[];
  actions?: MeepleCardAction[];
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
  /**
   * Issue #1823 Wave 3 M14 — license + attribution metadata for the cover
   * image. Renders a small footer chip under the title when present
   * (typically Wikidata-sourced covers per ADR DEC-3c whitelist).
   *
   * Surface depends on variant: GridCard/ListCard/CompactCard render an
   * inline `<small>` line; HeroCard/FeaturedCard get a more prominent
   * footer-row treatment. Variants that explicitly opt-out are responsible
   * for omitting the prop on their consumers.
   */
  attribution?: CoverAttribution;
  /**
   * Issue #2055 Phase G AC-G6 — Wikidata cover license + attribution rendered
   * as a plain-text footer beneath the card. Activated only for
   * `entity === 'game'`. All three fields are optional so the footer
   * gracefully degrades; when `wikidataCoverLicense` is null/undefined the
   * footer renders nothing. BE strips HTML upstream per DEC-G6-1 LOCKED
   * 2026-06-20 — render as plain text only, do NOT use
   * `dangerouslySetInnerHTML`.
   */
  wikidataCoverLicense?: string | null;
  wikidataCoverAttribution?: string | null;
  wikidataCoverSourceUrl?: string | null;
}

/**
 * Issue #1823 Wave 3 M14 — minimal license/attribution payload for cover
 * imagery sourced from external providers (Wikidata Commons, BGG, etc.).
 * All three fields are optional so the chip degrades gracefully when only
 * a subset is known.
 */
export interface CoverAttribution {
  /** Plain-text author / artist credit (e.g. "John Doe"). */
  author?: string | null;
  /** Whitelisted license identifier (e.g. "CC BY-SA 4.0"). */
  license?: string | null;
  /** Canonical source URL — rendered as a `target="_blank"` link on the license tag. */
  sourceUrl?: string | null;
}

export interface Carousel3DProps {
  cards: MeepleCardProps[];
  activeIndex?: number;
  onNavigate?: (index: number) => void;
  className?: string;
}
