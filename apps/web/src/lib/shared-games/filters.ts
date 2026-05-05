/**
 * SharedGames filter / sort taxonomy — Wave A.3b (Issue #596).
 *
 * Mockup `admin-mockups/design_files/sp3-shared-games.jsx` defines:
 *  - 4 chip filters: with-toolkit, with-agent, top-rated, new
 *  - 1 genre dropdown (resolved server-side to categoryIds Guid[])
 *  - 4 sort keys: rating, contrib, new, title
 *
 * Genre values are stable UI keys (slug-style) decoupled from the backend
 * category names, which the page resolves at SSR via `getCategories()` and
 * a name→id map.
 */

export type ChipKey = 'with-toolkit' | 'with-agent' | 'top-rated' | 'new';

export const FILTER_CHIPS: readonly { readonly key: ChipKey; readonly i18nKey: string }[] = [
  { key: 'with-toolkit', i18nKey: 'pages.sharedGames.chips.withToolkit' },
  { key: 'with-agent', i18nKey: 'pages.sharedGames.chips.withAgent' },
  { key: 'top-rated', i18nKey: 'pages.sharedGames.chips.topRated' },
  { key: 'new', i18nKey: 'pages.sharedGames.chips.new' },
];

export type SortKey = 'rating' | 'contrib' | 'new' | 'title';

export const SORT_OPTIONS: readonly { readonly key: SortKey; readonly i18nKey: string }[] = [
  { key: 'rating', i18nKey: 'pages.sharedGames.sort.rating' },
  { key: 'contrib', i18nKey: 'pages.sharedGames.sort.contrib' },
  { key: 'new', i18nKey: 'pages.sharedGames.sort.new' },
  { key: 'title', i18nKey: 'pages.sharedGames.sort.title' },
];

/**
 * Stable UI genre keys; resolution to backend `categoryIds` (Guid[]) happens
 * at SSR via the catalog `/shared-games/categories` lookup. Keys mirror the
 * mockup's GENRES array but decoupled from display labels (i18n).
 */
export const GENRES: readonly {
  readonly key: string;
  readonly i18nKey: string;
  readonly matchNames: readonly string[];
}[] = [
  { key: 'all', i18nKey: 'pages.sharedGames.genres.all', matchNames: [] },
  {
    key: 'strategy',
    i18nKey: 'pages.sharedGames.genres.strategy',
    matchNames: ['Strategy', 'Strategy Game'],
  },
  {
    key: 'family',
    i18nKey: 'pages.sharedGames.genres.family',
    matchNames: ['Family', 'Family Game'],
  },
  { key: 'party', i18nKey: 'pages.sharedGames.genres.party', matchNames: ['Party', 'Party Game'] },
  {
    key: 'cooperative',
    i18nKey: 'pages.sharedGames.genres.cooperative',
    matchNames: ['Cooperative', 'Co-op'],
  },
  { key: 'thematic', i18nKey: 'pages.sharedGames.genres.thematic', matchNames: ['Thematic'] },
  { key: 'wargame', i18nKey: 'pages.sharedGames.genres.wargame', matchNames: ['Wargame'] },
];

/**
 * Translate a UI sort key to backend (sortBy, sortDescending) tuple.
 *
 * Backend `SearchSharedGamesQueryHandler` switch values:
 *   "Title" | "YearPublished" | "AverageRating" | "CreatedAt"
 *   | "ComplexityRating" | "Contrib" | "New"
 */
export function sortKeyToBackendParams(key: SortKey): {
  readonly sortBy: string;
  readonly sortDescending: boolean;
} {
  switch (key) {
    case 'rating':
      return { sortBy: 'AverageRating', sortDescending: true };
    case 'contrib':
      return { sortBy: 'Contrib', sortDescending: true };
    case 'new':
      return { sortBy: 'New', sortDescending: true };
    case 'title':
    default:
      return { sortBy: 'Title', sortDescending: false };
  }
}

/**
 * Resolve a genre UI key to backend category Guids using a name→id map
 * built from `getCategories()` at SSR. Returns `[]` for `'all'` / unknown.
 */
export function genreKeyToCategoryIds(
  key: string,
  nameToId: ReadonlyMap<string, string>
): readonly string[] {
  if (!key || key === 'all') return [];
  const entry = GENRES.find(g => g.key === key);
  if (!entry) return [];
  const ids: string[] = [];
  for (const name of entry.matchNames) {
    const id = nameToId.get(name);
    if (id) ids.push(id);
  }
  return ids;
}

/**
 * Build a `nameToId` map from `getCategories()` response. Lower-case
 * lookup variant kept by callers if case-insensitive matching is needed.
 */
export function buildCategoryNameMap(
  categories: readonly { readonly id: string; readonly name: string }[]
): ReadonlyMap<string, string> {
  const map = new Map<string, string>();
  for (const c of categories) map.set(c.name, c.id);
  return map;
}
