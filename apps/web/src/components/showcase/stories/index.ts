/**
 * Showcase Story Registry
 *
 * Central registry of all component stories.
 * Add new stories here to make them available in the showcase.
 */

import { actionGridStory } from './action-grid.story';
import { confidenceBadgeStory } from './confidence-badge.story';
import { entityListViewStory } from './entity-list-view.story';
import { gameCarouselStory } from './game-carousel.story';
import { meepleCardStory } from './meeple-card.story';
import { pageTransitionStory } from './page-transition.story';
import { ruleSourceCardStory } from './rule-source-card.story';
import { tagStripStory } from './tag-strip.story';
import { tierBadgeStory } from './tier-badge.story';

import type { ShowcaseStory } from '../types';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export const ALL_STORIES: ShowcaseStory<any>[] = [
  meepleCardStory,
  entityListViewStory,
  gameCarouselStory,
  actionGridStory,
  tagStripStory,
  pageTransitionStory,
  confidenceBadgeStory,
  tierBadgeStory,
  ruleSourceCardStory,
];

/** Look up a story by its slug id */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export const STORY_MAP: Record<string, ShowcaseStory<any>> = Object.fromEntries(
  ALL_STORIES.map(s => [s.id, s])
);
