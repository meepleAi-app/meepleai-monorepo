/**
 * AdvancedFiltersDrawer — type definitions (Phase 3a #1606).
 *
 * `LibraryFilters` is a discriminated union keyed by `scope` so the public API
 * doesn't leak `Record<string, unknown>`. Each variant only carries the fields
 * relevant to its entity type — Phase 1's `HybridHubEntity` literal drives the
 * scope discriminant.
 *
 * Section config types (in `./sections.ts`) describe each section declaratively
 * so the renderer doesn't switch on scope at every level — `getSectionsForScope`
 * returns the array, the renderer iterates.
 */

import type { GameStateType } from '@/lib/api/schemas/library.schemas';
import type { HybridHubEntity } from '@/lib/library/hybrid-hub.types';

export interface GameLibraryFilters {
  readonly scope: 'game';
  readonly states?: ReadonlyArray<GameStateType>;
  readonly withKb?: boolean;
  readonly ratingMin?: number;
  readonly playersMin?: number;
  readonly playersMax?: number;
  readonly yearMin?: number;
  readonly yearMax?: number;
}

export interface AgentLibraryFilters {
  readonly scope: 'agent';
  readonly types?: ReadonlyArray<string>;
  readonly activeOnly?: boolean;
}

export interface SessionLibraryFilters {
  readonly scope: 'session';
  readonly statuses?: ReadonlyArray<string>;
  readonly sessionTypes?: ReadonlyArray<string>;
  readonly playerCountMin?: number;
}

export interface KbLibraryFilters {
  readonly scope: 'kb';
  readonly processingStates?: ReadonlyArray<'Ready' | 'Pending' | 'Failed'>;
}

export interface ChatLibraryFilters {
  readonly scope: 'chat';
  readonly messageCountMin?: number;
}

export type LibraryFilters =
  | GameLibraryFilters
  | AgentLibraryFilters
  | SessionLibraryFilters
  | KbLibraryFilters
  | ChatLibraryFilters;

/**
 * Narrow `LibraryFilters` to the variant matching a given `HybridHubEntity` scope.
 */
export type FiltersForScope<S extends HybridHubEntity> = Extract<LibraryFilters, { scope: S }>;

export interface AdvancedFiltersDrawerProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  /** Drives which sections render. Single entity — NOT 'all' from HybridHubTab. */
  readonly entityScope: HybridHubEntity;
  /** Active filters applied to the surface; shown in the drawer when it opens. */
  readonly activeFilters: LibraryFilters;
  /** Called with the new draft when the user clicks Apply. Drawer closes after. */
  readonly onApply: (filters: LibraryFilters) => void;
  /** Called when the user clicks Clear. Drawer resets draft to default empty. */
  readonly onClear: () => void;
}
