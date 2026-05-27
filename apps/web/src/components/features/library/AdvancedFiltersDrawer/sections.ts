/**
 * AdvancedFiltersDrawer — pure section configuration (Phase 3a #1606).
 *
 * Declarative section descriptors keyed by `HybridHubEntity` scope. The
 * renderer iterates over the returned array — no scope-switching at render
 * time. Each section is one of three kinds (`checkbox-group`, `toggle`,
 * `slider`, `range`); adding a new kind only requires a new case in the
 * renderer + a new branch here.
 *
 * Each call returns a NEW array (caller can safely mutate state derived from
 * it without leaking back into the config).
 */

import type { HybridHubEntity } from '@/lib/library/hybrid-hub.types';

export interface CheckboxOption<V extends string = string> {
  readonly value: V;
  readonly i18nKey: string;
}

export interface CheckboxGroupSection {
  readonly kind: 'checkbox-group';
  readonly key: string;
  readonly i18nLabel: string;
  readonly options: ReadonlyArray<CheckboxOption>;
}

export interface ToggleSection {
  readonly kind: 'toggle';
  readonly key: string;
  readonly i18nLabel: string;
}

export interface SliderSection {
  readonly kind: 'slider';
  readonly key: string;
  readonly i18nLabel: string;
  readonly min: number;
  readonly max: number;
  readonly step: number;
}

export interface RangeSection {
  readonly kind: 'range';
  readonly key: string;
  readonly i18nLabel: string;
  readonly min: number;
  readonly max: number;
  readonly step: number;
}

export type SectionConfig = CheckboxGroupSection | ToggleSection | SliderSection | RangeSection;

const GAME_STATE_OPTIONS: ReadonlyArray<CheckboxOption> = [
  { value: 'Owned', i18nKey: 'pages.library.filters.state.owned' },
  { value: 'Wishlist', i18nKey: 'pages.library.filters.state.wishlist' },
  { value: 'InPrestito', i18nKey: 'pages.library.filters.state.loaned' },
];

const KB_STATE_OPTIONS: ReadonlyArray<CheckboxOption> = [
  { value: 'Ready', i18nKey: 'pages.library.filters.kbState.ready' },
  { value: 'Pending', i18nKey: 'pages.library.filters.kbState.pending' },
  { value: 'Failed', i18nKey: 'pages.library.filters.kbState.failed' },
];

const CURRENT_YEAR = new Date().getFullYear();

export function getSectionsForScope(scope: HybridHubEntity): ReadonlyArray<SectionConfig> {
  switch (scope) {
    case 'game':
      return [
        {
          kind: 'checkbox-group',
          key: 'states',
          i18nLabel: 'pages.library.filters.section.state',
          options: GAME_STATE_OPTIONS,
        },
        { kind: 'toggle', key: 'withKb', i18nLabel: 'pages.library.filters.section.withKb' },
        {
          kind: 'slider',
          key: 'rating',
          i18nLabel: 'pages.library.filters.section.rating',
          min: 0,
          max: 10,
          step: 0.5,
        },
        {
          kind: 'range',
          key: 'players',
          i18nLabel: 'pages.library.filters.section.players',
          min: 1,
          max: 10,
          step: 1,
        },
        {
          kind: 'range',
          key: 'year',
          i18nLabel: 'pages.library.filters.section.year',
          min: 1900,
          max: CURRENT_YEAR,
          step: 1,
        },
      ];
    case 'agent':
      return [
        {
          kind: 'checkbox-group',
          key: 'types',
          i18nLabel: 'pages.library.filters.section.agentType',
          options: [],
        },
        {
          kind: 'toggle',
          key: 'activeOnly',
          i18nLabel: 'pages.library.filters.section.activeOnly',
        },
      ];
    case 'session':
      return [
        {
          kind: 'checkbox-group',
          key: 'statuses',
          i18nLabel: 'pages.library.filters.section.sessionStatus',
          options: [],
        },
        {
          kind: 'checkbox-group',
          key: 'sessionTypes',
          i18nLabel: 'pages.library.filters.section.sessionType',
          options: [],
        },
        {
          kind: 'slider',
          key: 'playerCount',
          i18nLabel: 'pages.library.filters.section.playerCount',
          min: 1,
          max: 12,
          step: 1,
        },
      ];
    case 'kb':
      return [
        {
          kind: 'checkbox-group',
          key: 'processingStates',
          i18nLabel: 'pages.library.filters.section.processingState',
          options: KB_STATE_OPTIONS,
        },
      ];
    case 'chat':
      return [
        {
          kind: 'slider',
          key: 'messageCountMin',
          i18nLabel: 'pages.library.filters.section.messageCountMin',
          min: 0,
          max: 100,
          step: 5,
        },
      ];
  }
}
