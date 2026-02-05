// Barrel exports for versioning module (Issue #3355)

export { default as VersionTimeline } from './VersionTimeline';
export { default as VersionTimelineFilters } from './VersionTimelineFilters';
export { default as VersionHistory } from './VersionHistory';
export { default as VersionComparison } from './VersionComparison';
export { ChangeItem } from './ChangeItem';

export type {
  VersionNode,
  VersionHistoryResponse,
  RuleAtom,
  FieldChange,
  ChangeType,
  RuleAtomChange,
  DiffSummary,
  VersionDiff,
  TimelineFilters,
} from './types';
