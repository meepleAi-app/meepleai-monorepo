/**
 * Showcase Story Metadata — Server-safe
 *
 * Static metadata only (no component imports, no hooks).
 * Used by the showcase homepage (Server Component) to render the grid.
 * The full story registry (with actual components) lives in `index.ts`
 * and is only imported by the `[component]/page.tsx` Client Component.
 */

import type { ShowcaseCategory } from '../types';

export interface StoryMeta {
  id: string;
  title: string;
  category: ShowcaseCategory;
  description?: string;
  controlCount: number;
  presetCount: number;
}

export const STORY_METADATA: StoryMeta[] = [
  {
    id: 'meeple-card',
    title: 'MeepleCard',
    category: 'Data Display',
    description: 'Universal card component for games, players, agents, sessions, and more.',
    controlCount: 9,
    presetCount: 6,
  },
  {
    id: 'entity-list-view',
    title: 'EntityListView',
    category: 'Data Display',
    description: 'Generic multi-view list with grid, list, carousel, and table modes.',
    controlCount: 4,
    presetCount: 4,
  },
  {
    id: 'game-carousel',
    title: 'GameCarousel',
    category: 'Data Display',
    description: 'Card-based carousel for game discovery with optional sorting and flip.',
    controlCount: 5,
    presetCount: 4,
  },
  {
    id: 'action-grid',
    title: 'ActionGrid',
    category: 'Navigation',
    description: 'Responsive grid of navigation actions with gradient icons.',
    controlCount: 4,
    presetCount: 4,
  },
  {
    id: 'tag-strip',
    title: 'TagStrip',
    category: 'Tags',
    description: 'Horizontal strip of colored tag badges with overflow indicator.',
    controlCount: 3,
    presetCount: 4,
  },
  {
    id: 'page-transition',
    title: 'PageTransition',
    category: 'Animations',
    description: 'Smooth page transition animations using Framer Motion.',
    controlCount: 1,
    presetCount: 3,
  },
  {
    id: 'confidence-badge',
    title: 'ConfidenceBadge',
    category: 'Feedback',
    description: 'Displays AI confidence score with color-coded visual feedback.',
    controlCount: 2,
    presetCount: 4,
  },
  {
    id: 'tier-badge',
    title: 'TierBadge',
    category: 'Feedback',
    description: 'Displays user subscription tier with color-coded styling.',
    controlCount: 2,
    presetCount: 5,
  },
  {
    id: 'rule-source-card',
    title: 'RuleSourceCard',
    category: 'Feedback',
    description: 'Citation card per risposte RAG con fonti dal regolamento.',
    controlCount: 4,
    presetCount: 4,
  },
];
