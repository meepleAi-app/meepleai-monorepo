// Version History Types (Issue #3355)

export interface VersionNode {
  id: string;
  version: string;
  title: string;
  description: string;
  author: string;
  createdAt: string;
  parentVersionId?: string;
  parentVersion?: string;
  mergedFromVersionIds?: string[];
  mergedFromVersions?: string[];
  changeCount: number;
  isCurrentVersion: boolean;
}

export interface VersionHistoryResponse {
  gameId: string;
  versions: VersionNode[];
  authors: string[];
  totalVersions: number;
}

export interface RuleAtom {
  id: string;
  text: string;
  section?: string | null;
  page?: string | null;
  line?: string | null;
}

export interface FieldChange {
  fieldName: string;
  oldValue?: string | null;
  newValue?: string | null;
}

export type ChangeType = 'Added' | 'Modified' | 'Deleted' | 'Unchanged';

export interface RuleAtomChange {
  type: ChangeType;
  oldAtom?: string | null;
  newAtom?: string | null;
  oldValue?: RuleAtom | null;
  newValue?: RuleAtom | null;
  fieldChanges?: FieldChange[] | null;
}

export interface DiffSummary {
  totalChanges: number;
  added: number;
  modified: number;
  deleted: number;
  unchanged: number;
}

export interface VersionDiff {
  gameId: string;
  fromVersion: string;
  toVersion: string;
  fromCreatedAt: string;
  toCreatedAt: string;
  summary: DiffSummary;
  changes: RuleAtomChange[];
}

export interface TimelineFilters {
  startDate?: string;
  endDate?: string;
  author?: string;
  searchQuery?: string;
}
