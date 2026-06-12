/**
 * AdvancedFiltersDrawer — section configuration.
 *
 * SP4 mockup conformance (Issue #1585-followup, plan
 * docs/superpowers/plans/2026-06-03-library-sp4-mockup-conformance.md Task 3.3).
 * Mapped from `admin-mockups/design_files/sp4-library-desktop.jsx`
 * (DRAWER_SECTIONS, lines 319–388).
 *
 * REFACTORED from scope-conditional `getSectionsForScope` to **static 7-section
 * declarative config**. The drawer now exposes the same sections regardless
 * of the active hub tab — matches mockup hub-level filter surface.
 *
 * Four section kinds match mockup:
 *  - `chips-multi`: multi-select toggle chips with optional icon + entity color
 *  - `select-multi`: searchable multi-select (placeholder + scrolling options)
 *  - `period-quick`: single-select radio list (7d/30d/1y/all/range) with
 *    custom-range option that just stores the value 'range' (date pickers
 *    deferred)
 *  - `range`: numeric range (lo, hi) backed by two `<input type="range">`
 *    rendered visually like the mockup dual-thumb slider.
 *
 * Adding a new kind requires (1) new branch here, (2) new renderer case in
 * AdvancedFiltersDrawer.tsx, (3) new test in sections.test.ts.
 */

import type {
  LibraryFilterPeriod,
  LibraryFilterStatus,
  LibraryFilterTag,
  LibraryFilterWeight,
} from './types';

/** All chips-multi option literals are entity-color slugs from tokens.css. */
export type EntityColorSlug =
  | 'game'
  | 'agent'
  | 'kb'
  | 'session'
  | 'chat'
  | 'event'
  | 'toolkit'
  | 'player';

export interface ChipOption<V extends string = string> {
  readonly value: V;
  readonly i18nKey: string;
  readonly icon?: string;
  readonly color?: EntityColorSlug;
}

export interface ChipsMultiSection<V extends string = string> {
  readonly kind: 'chips-multi';
  readonly key: string;
  readonly i18nLabel: string;
  readonly icon: string;
  readonly options: ReadonlyArray<ChipOption<V>>;
}

export interface SelectMultiSection {
  readonly kind: 'select-multi';
  readonly key: string;
  readonly i18nLabel: string;
  readonly icon: string;
  readonly i18nPlaceholder: string;
}

export interface PeriodQuickOption<V extends string = string> {
  readonly value: V;
  readonly i18nKey: string;
}

export interface PeriodQuickSection<V extends string = string> {
  readonly kind: 'period-quick';
  readonly key: string;
  readonly i18nLabel: string;
  readonly icon: string;
  readonly options: ReadonlyArray<PeriodQuickOption<V>>;
}

export interface RangeSection {
  readonly kind: 'range';
  readonly key: string;
  /** Field name used to read/write the lower bound on `LibraryFilters`. */
  readonly minField: string;
  /** Field name used to read/write the upper bound on `LibraryFilters`. */
  readonly maxField: string;
  readonly i18nLabel: string;
  readonly icon: string;
  readonly min: number;
  readonly max: number;
  readonly step: number;
  readonly defaultLo: number;
  readonly defaultHi: number;
}

export type SectionConfig =
  | ChipsMultiSection<LibraryFilterStatus>
  | ChipsMultiSection<LibraryFilterTag>
  | ChipsMultiSection<LibraryFilterWeight>
  | SelectMultiSection
  | PeriodQuickSection<LibraryFilterPeriod>
  | RangeSection;

const STATUS_OPTIONS: ReadonlyArray<ChipOption<LibraryFilterStatus>> = [
  {
    value: 'owned',
    i18nKey: 'pages.library.filters.section.status.options.owned',
    icon: '✓',
    color: 'game',
  },
  {
    value: 'wishlist',
    i18nKey: 'pages.library.filters.section.status.options.wishlist',
    icon: '★',
    color: 'event',
  },
  {
    value: 'setup',
    i18nKey: 'pages.library.filters.section.status.options.setup',
    icon: '⚙',
    color: 'agent',
  },
  {
    value: 'archived',
    i18nKey: 'pages.library.filters.section.status.options.archived',
    icon: '⊘',
    color: 'kb',
  },
];

// Issue #2186: ENTITY_OPTIONS removed — entity scope is the responsibility
// of LibraryTabs (Tutti / Giochi / Agenti / KB / Sessioni / Chat) and
// duplicating it inside the AdvancedFiltersDrawer created two parallel
// filter paths with overlapping semantics.

const PERIOD_OPTIONS: ReadonlyArray<PeriodQuickOption<LibraryFilterPeriod>> = [
  { value: '7d', i18nKey: 'pages.library.filters.section.period.options.7d' },
  { value: '30d', i18nKey: 'pages.library.filters.section.period.options.30d' },
  { value: '1y', i18nKey: 'pages.library.filters.section.period.options.1y' },
  { value: 'all', i18nKey: 'pages.library.filters.section.period.options.all' },
  { value: 'range', i18nKey: 'pages.library.filters.section.period.options.range' },
];

const TAG_OPTIONS: ReadonlyArray<ChipOption<LibraryFilterTag>> = [
  {
    value: 'family',
    i18nKey: 'pages.library.filters.section.tags.options.family',
    color: 'event',
  },
  {
    value: 'strategy',
    i18nKey: 'pages.library.filters.section.tags.options.strategy',
    color: 'session',
  },
  { value: 'coop', i18nKey: 'pages.library.filters.section.tags.options.coop', color: 'kb' },
  {
    value: 'engine',
    i18nKey: 'pages.library.filters.section.tags.options.engine',
    color: 'agent',
  },
  {
    value: 'auction',
    i18nKey: 'pages.library.filters.section.tags.options.auction',
    color: 'toolkit',
  },
  {
    value: 'roll-and-write',
    i18nKey: 'pages.library.filters.section.tags.options.rollAndWrite',
    color: 'player',
  },
  {
    value: 'card-driven',
    i18nKey: 'pages.library.filters.section.tags.options.cardDriven',
    color: 'chat',
  },
  {
    value: 'tableau',
    i18nKey: 'pages.library.filters.section.tags.options.tableau',
    color: 'game',
  },
];

const WEIGHT_OPTIONS: ReadonlyArray<ChipOption<LibraryFilterWeight>> = [
  { value: 'light', i18nKey: 'pages.library.filters.section.weight.options.light', color: 'kb' },
  {
    value: 'medium',
    i18nKey: 'pages.library.filters.section.weight.options.medium',
    color: 'agent',
  },
  {
    value: 'heavy',
    i18nKey: 'pages.library.filters.section.weight.options.heavy',
    color: 'game',
  },
  {
    value: 'extra',
    i18nKey: 'pages.library.filters.section.weight.options.extra',
    color: 'event',
  },
];

/**
 * Static 6-section descriptor (#2186: 'entities' removed, was index 1 / 7-of-7
 * in the original mockup). Order is significant: first 3 sections are open
 * by default (see drawer `defaultOpen` logic), remaining 3 collapse on
 * initial render.
 */
export const DRAWER_SECTIONS: ReadonlyArray<SectionConfig> = [
  {
    kind: 'chips-multi',
    key: 'statuses',
    i18nLabel: 'pages.library.filters.section.status.title',
    icon: '●',
    options: STATUS_OPTIONS,
  },
  // Issue #2186: 'entities' section removed — handled by LibraryTabs instead.
  {
    kind: 'select-multi',
    key: 'games',
    i18nLabel: 'pages.library.filters.section.game.title',
    icon: '🎲',
    i18nPlaceholder: 'pages.library.filters.section.game.placeholder',
  },
  {
    kind: 'period-quick',
    key: 'period',
    i18nLabel: 'pages.library.filters.section.period.title',
    icon: '📅',
    options: PERIOD_OPTIONS,
  },
  {
    kind: 'chips-multi',
    key: 'tags',
    i18nLabel: 'pages.library.filters.section.tags.title',
    icon: '🏷',
    options: TAG_OPTIONS,
  },
  {
    kind: 'range',
    key: 'rating',
    minField: 'ratingMin',
    maxField: 'ratingMax',
    i18nLabel: 'pages.library.filters.section.rating.title',
    icon: '★',
    min: 1,
    max: 10,
    step: 0.5,
    defaultLo: 6,
    defaultHi: 10,
  },
  {
    kind: 'chips-multi',
    key: 'weights',
    i18nLabel: 'pages.library.filters.section.weight.title',
    icon: '⚖',
    options: WEIGHT_OPTIONS,
  },
];

/** Whether a section index should default to expanded (mockup: first 3). */
export function isDefaultOpen(sectionIndex: number): boolean {
  return sectionIndex < 3;
}
