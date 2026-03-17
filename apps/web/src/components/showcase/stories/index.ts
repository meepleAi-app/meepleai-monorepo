/**
 * Showcase Story Registry
 *
 * Central registry of all component stories.
 * Add new stories here to make them available in the showcase.
 */

// ─── Existing Stories ─────────────────────────────────────────────────────────
import { actionGridStory } from './action-grid.story';
import { agentStatsDisplayStory } from './agent-stats-display.story';
import { agentStatusBadgeStory } from './agent-status-badge.story';
import { alertStory } from './alert.story';
import { badgeStory } from './badge.story';
import { buttonStory } from './button.story';
import { chatMessageStory } from './chat-message.story';
import { checkboxStory } from './checkbox.story';
import { collectionProgressStory } from './collection-progress.story';
import { confidenceBadgeStory } from './confidence-badge.story';
import { dialogStory } from './dialog.story';
import { dropdownMenuStory } from './dropdown-menu.story';
import { entityListViewStory } from './entity-list-view.story';
import { formShowcaseStory } from './form-showcase.story';
import { gameCarouselStory } from './game-carousel.story';
import { inputStory } from './input.story';
import { kpiCardsStory } from './kpi-cards.story';
import { meepleAvatarStory } from './meeple-avatar.story';
import { meepleCardStory } from './meeple-card.story';
import { meepleLogoStory } from './meeple-logo.story';
import { pageTransitionStory } from './page-transition.story';
import { popoverStory } from './popover.story';
import { progressStory } from './progress.story';
import { radioGroupStory } from './radio-group.story';
import { ratingStarsStory } from './rating-stars.story';
import { ruleSourceCardStory } from './rule-source-card.story';
import { selectStory } from './select.story';
import { separatorStory } from './separator.story';
import { sheetStory } from './sheet.story';
import { skeletonStory } from './skeleton.story';
import { statCardStory } from './stat-card.story';
import { strategyBadgeStory } from './strategy-badge.story';
import { switchStory } from './switch.story';
import { tabsStory } from './tabs.story';
import { tagStripStory } from './tag-strip.story';
import { textareaStory } from './textarea.story';
import { tierBadgeStory } from './tier-badge.story';

// ─── Forms ────────────────────────────────────────────────────────────────────

// ─── Overlays ─────────────────────────────────────────────────────────────────
import { tooltipStory } from './tooltip.story';

// ─── Feedback ────────────────────────────────────────────────────────────────

// ─── Navigation ──────────────────────────────────────────────────────────────

// ─── Layout ──────────────────────────────────────────────────────────────────

// ─── Meeple ──────────────────────────────────────────────────────────────────

// ─── Agent ───────────────────────────────────────────────────────────────────

// ─── Charts ──────────────────────────────────────────────────────────────────
import { upgradePromptStory } from './upgrade-prompt.story';
import { waterfallChartStory } from './waterfall-chart.story';

// ─── Data Display ────────────────────────────────────────────────────────────

// ─── Gates ───────────────────────────────────────────────────────────────────

import type { ShowcaseStory } from '../types';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export const ALL_STORIES: ShowcaseStory<any>[] = [
  // Data Display
  meepleCardStory,
  entityListViewStory,
  gameCarouselStory,
  badgeStory,
  ratingStarsStory,
  statCardStory,

  // Navigation
  actionGridStory,
  tabsStory,
  dropdownMenuStory,

  // Feedback
  confidenceBadgeStory,
  tierBadgeStory,
  ruleSourceCardStory,
  alertStory,
  progressStory,
  skeletonStory,

  // Tags
  tagStripStory,

  // Animations
  pageTransitionStory,

  // Forms
  buttonStory,
  inputStory,
  switchStory,
  checkboxStory,
  textareaStory,
  selectStory,
  radioGroupStory,
  formShowcaseStory,

  // Overlays
  dialogStory,
  sheetStory,
  tooltipStory,
  popoverStory,

  // Layout
  separatorStory,

  // Meeple
  meepleAvatarStory,
  meepleLogoStory,
  chatMessageStory,

  // Agent
  agentStatusBadgeStory,
  agentStatsDisplayStory,
  strategyBadgeStory,

  // Charts
  waterfallChartStory,
  kpiCardsStory,

  // Gates
  upgradePromptStory,
  collectionProgressStory,
];

/** Look up a story by its slug id */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export const STORY_MAP: Record<string, ShowcaseStory<any>> = Object.fromEntries(
  ALL_STORIES.map(s => [s.id, s])
);
