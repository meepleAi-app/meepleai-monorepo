/**
 * GamesFilterPanel - Interactive sidebar filter panel for games catalog.
 *
 * Features:
 * - Quick filter links (Top BGG, player counts, recent)
 * - Expandable advanced filters (categories, mechanics, players, playtime, complexity, sort)
 * - URL search params as single source of truth (shareable, SSR-compatible)
 * - Lazy-loads categories/mechanics only when advanced panel is open
 * - Active filter count badge
 *
 * @see apps/web/src/app/(public)/games/page.tsx for URL param consumption
 */

'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';

import { AnimatePresence, motion } from 'framer-motion';
import {
  LayoutDashboard,
  Gamepad2,
  Star,
  Users,
  Clock,
  SlidersHorizontal,
  ChevronDown,
  Search,
  RotateCcw,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Label } from '@/components/ui/primitives/label';
import { cn } from '@/lib/utils';

// ─── Types ───────────────────────────────────────────────────────────────────

interface GamesFilterPanelProps {
  isCollapsed: boolean;
}

interface CategoryItem {
  id: string;
  name: string;
  slug: string;
}

interface MechanicItem {
  id: string;
  name: string;
  slug: string;
}

// ─── Shared link style (mirrors SidebarContextNav pattern) ───────────────────

function FilterLink({
  href,
  icon: Icon,
  label,
  isActive,
  isCollapsed,
}: {
  href: string;
  icon: React.ElementType;
  label: string;
  isActive?: boolean;
  isCollapsed: boolean;
}) {
  return (
    <Link
      href={href}
      className={cn(
        'flex items-center gap-3 rounded-lg text-sm font-medium',
        'min-h-[44px] px-3 py-2',
        'transition-colors duration-150',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring focus-visible:ring-offset-1',
        isActive
          ? 'bg-[hsl(25_95%_45%/0.12)] text-[hsl(25_95%_42%)] font-semibold'
          : 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
        isCollapsed && 'justify-center px-2'
      )}
    >
      <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
      {!isCollapsed && <span className="truncate">{label}</span>}
    </Link>
  );
}

function SectionLabel({ label, isCollapsed }: { label: string; isCollapsed: boolean }) {
  if (isCollapsed) return <hr className="border-sidebar-border my-2" />;
  return (
    <p className="px-3 pt-3 pb-1 text-[10px] font-semibold uppercase tracking-wider text-sidebar-foreground/40">
      {label}
    </p>
  );
}

// ─── Active filter counter ───────────────────────────────────────────────────

function countActiveFilters(sp: URLSearchParams): number {
  let count = 0;
  if (sp.get('categoryIds')) count++;
  if (sp.get('mechanicIds')) count++;
  if (sp.get('minPlayers') || sp.get('maxPlayers')) count++;
  if (sp.get('maxPlayingTime')) count++;
  if (sp.get('minComplexity') || sp.get('maxComplexity')) count++;
  if (sp.get('sortBy')) count++;
  return count;
}

// ─── Quick filter matching ───────────────────────────────────────────────────

function isQuickFilterActive(
  currentParams: URLSearchParams,
  expected: Record<string, string>
): boolean {
  const keys = Object.keys(expected);
  // All expected keys must match AND no other filter keys should exist
  const filterKeys = [
    'sortBy',
    'sortDesc',
    'minPlayers',
    'maxPlayers',
    'maxPlayingTime',
    'minComplexity',
    'maxComplexity',
    'categoryIds',
    'mechanicIds',
  ];

  for (const key of keys) {
    if (currentParams.get(key) !== expected[key]) return false;
  }

  // No extra filter keys beyond the expected ones
  for (const key of filterKeys) {
    if (!keys.includes(key) && currentParams.has(key)) return false;
  }

  return true;
}

// ─── Checkbox list with optional search ──────────────────────────────────────

function CheckboxFilterList({
  items,
  selectedIds,
  onToggle,
  label,
}: {
  items: CategoryItem[] | MechanicItem[];
  selectedIds: Set<string>;
  onToggle: (id: string) => void;
  label: string;
}) {
  const [filterText, setFilterText] = useState('');
  const showSearch = items.length > 8;

  const filtered = useMemo(() => {
    if (!filterText) return items;
    const lower = filterText.toLowerCase();
    return items.filter(item => item.name.toLowerCase().includes(lower));
  }, [items, filterText]);

  return (
    <div className="space-y-1.5">
      <Label className="text-[11px] font-semibold uppercase tracking-wider text-sidebar-foreground/50">
        {label}
      </Label>
      {showSearch && (
        <div className="relative">
          <Search className="absolute left-2 top-1.5 h-3 w-3 text-sidebar-foreground/40" />
          <input
            type="text"
            value={filterText}
            onChange={e => setFilterText(e.target.value)}
            placeholder="Cerca..."
            className="w-full rounded-md border border-sidebar-border bg-sidebar/50 py-1 pl-7 pr-2 text-xs text-sidebar-foreground placeholder:text-sidebar-foreground/30 focus:outline-none focus:ring-1 focus:ring-sidebar-ring"
          />
        </div>
      )}
      <div className="max-h-40 overflow-y-auto space-y-1 pr-1">
        {filtered.length === 0 && (
          <p className="text-[11px] text-sidebar-foreground/40 py-1">Nessun risultato</p>
        )}
        {filtered.map(item => (
          <div key={item.id} className="flex items-center gap-2">
            <Checkbox
              id={`filter-${label}-${item.id}`}
              checked={selectedIds.has(item.id)}
              onCheckedChange={() => onToggle(item.id)}
              className="h-3.5 w-3.5"
            />
            <Label
              htmlFor={`filter-${label}-${item.id}`}
              className="text-xs font-normal cursor-pointer text-sidebar-foreground/80 leading-tight"
            >
              {item.name}
            </Label>
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── Small native select ─────────────────────────────────────────────────────

function FilterSelect({
  label,
  value,
  onChange,
  options,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
}) {
  return (
    <div className="space-y-1">
      <Label className="text-[11px] font-semibold uppercase tracking-wider text-sidebar-foreground/50">
        {label}
      </Label>
      <select
        value={value}
        onChange={e => onChange(e.target.value)}
        className="w-full rounded-md border border-sidebar-border bg-sidebar/50 px-2 py-1.5 text-xs text-sidebar-foreground focus:outline-none focus:ring-1 focus:ring-sidebar-ring"
      >
        {options.map(opt => (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        ))}
      </select>
    </div>
  );
}

// ─── Main component ──────────────────────────────────────────────────────────

export function GamesFilterPanel({ isCollapsed }: GamesFilterPanelProps) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const router = useRouter();

  // Advanced panel expand state
  const [isAdvancedOpen, setIsAdvancedOpen] = useState(false);

  // Lazy-loaded reference data
  const [categories, setCategories] = useState<CategoryItem[]>([]);
  const [mechanics, setMechanics] = useState<MechanicItem[]>([]);
  const [hasFetchedRefData, setHasFetchedRefData] = useState(false);

  // Local advanced filter state (initialized from URL)
  const [selectedCategories, setSelectedCategories] = useState<Set<string>>(
    () => new Set(searchParams.get('categoryIds')?.split(',').filter(Boolean) ?? [])
  );
  const [selectedMechanics, setSelectedMechanics] = useState<Set<string>>(
    () => new Set(searchParams.get('mechanicIds')?.split(',').filter(Boolean) ?? [])
  );
  const [minPlayers, setMinPlayers] = useState(searchParams.get('minPlayers') ?? '');
  const [maxPlayers, setMaxPlayers] = useState(searchParams.get('maxPlayers') ?? '');
  const [maxPlayingTime, setMaxPlayingTime] = useState(searchParams.get('maxPlayingTime') ?? '');
  const [minComplexity, setMinComplexity] = useState(searchParams.get('minComplexity') ?? '');
  const [maxComplexity, setMaxComplexity] = useState(searchParams.get('maxComplexity') ?? '');
  const [sortBy, setSortBy] = useState(searchParams.get('sortBy') ?? '');
  const [sortDesc, setSortDesc] = useState(searchParams.get('sortDesc') === 'true');

  // Re-sync local state when URL changes (e.g., quick filter navigation)
  useEffect(() => {
    setSelectedCategories(
      new Set(searchParams.get('categoryIds')?.split(',').filter(Boolean) ?? [])
    );
    setSelectedMechanics(
      new Set(searchParams.get('mechanicIds')?.split(',').filter(Boolean) ?? [])
    );
    setMinPlayers(searchParams.get('minPlayers') ?? '');
    setMaxPlayers(searchParams.get('maxPlayers') ?? '');
    setMaxPlayingTime(searchParams.get('maxPlayingTime') ?? '');
    setMinComplexity(searchParams.get('minComplexity') ?? '');
    setMaxComplexity(searchParams.get('maxComplexity') ?? '');
    setSortBy(searchParams.get('sortBy') ?? '');
    setSortDesc(searchParams.get('sortDesc') === 'true');
  }, [searchParams]);

  // Fetch categories & mechanics when advanced panel first opens
  useEffect(() => {
    if (!isAdvancedOpen || hasFetchedRefData) return;

    let cancelled = false;

    async function fetchRefData() {
      try {
        const [catRes, mechRes] = await Promise.all([
          fetch('/api/v1/shared-games/categories'),
          fetch('/api/v1/shared-games/mechanics'),
        ]);

        if (cancelled) return;

        if (catRes.ok) {
          const catData = await catRes.json();
          // API may return { items: [...] } or array directly
          setCategories(Array.isArray(catData) ? catData : (catData.items ?? []));
        }
        if (mechRes.ok) {
          const mechData = await mechRes.json();
          setMechanics(Array.isArray(mechData) ? mechData : (mechData.items ?? []));
        }

        setHasFetchedRefData(true);
      } catch {
        // Mark as fetched so loading message doesn't persist forever
        setHasFetchedRefData(true);
      }
    }

    fetchRefData();
    return () => {
      cancelled = true;
    };
  }, [isAdvancedOpen, hasFetchedRefData]);

  // Active filter count from current URL
  const activeFilterCount = useMemo(() => countActiveFilters(searchParams), [searchParams]);

  // Check which quick filter is active
  const isAllGames =
    pathname === '/games' && activeFilterCount === 0 && !searchParams.get('search');
  const isTopBgg = isQuickFilterActive(searchParams, {
    sortBy: 'AverageRating',
    sortDesc: 'true',
  });
  const is2Players = isQuickFilterActive(searchParams, {
    minPlayers: '2',
    maxPlayers: '2',
  });
  const is3to6Players = isQuickFilterActive(searchParams, {
    minPlayers: '3',
    maxPlayers: '6',
  });
  const isRecent = isQuickFilterActive(searchParams, {
    sortBy: 'CreatedAt',
    sortDesc: 'true',
  });

  // Toggle helpers for checkbox lists
  const toggleCategory = useCallback((id: string) => {
    setSelectedCategories(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const toggleMechanic = useCallback((id: string) => {
    setSelectedMechanics(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  // Apply advanced filters — builds fresh URL
  const applyFilters = useCallback(() => {
    const params = new URLSearchParams();

    // Preserve search and view from current URL
    const currentSearch = searchParams.get('search');
    const currentView = searchParams.get('view');
    if (currentSearch) params.set('search', currentSearch);
    if (currentView) params.set('view', currentView);

    // Always reset page to 1
    // (page param omitted = defaults to 1 server-side)

    // Categories
    if (selectedCategories.size > 0) {
      params.set('categoryIds', Array.from(selectedCategories).join(','));
    }

    // Mechanics
    if (selectedMechanics.size > 0) {
      params.set('mechanicIds', Array.from(selectedMechanics).join(','));
    }

    // Players
    if (minPlayers) params.set('minPlayers', minPlayers);
    if (maxPlayers) params.set('maxPlayers', maxPlayers);

    // Playtime
    if (maxPlayingTime) params.set('maxPlayingTime', maxPlayingTime);

    // Complexity
    if (minComplexity) params.set('minComplexity', minComplexity);
    if (maxComplexity) params.set('maxComplexity', maxComplexity);

    // Sort
    if (sortBy) {
      params.set('sortBy', sortBy);
      if (sortDesc) params.set('sortDesc', 'true');
    }

    const qs = params.toString();
    router.push(qs ? `/games?${qs}` : '/games');
  }, [
    searchParams,
    selectedCategories,
    selectedMechanics,
    minPlayers,
    maxPlayers,
    maxPlayingTime,
    minComplexity,
    maxComplexity,
    sortBy,
    sortDesc,
    router,
  ]);

  // Reset all filters
  const resetFilters = useCallback(() => {
    const params = new URLSearchParams();
    const currentView = searchParams.get('view');
    if (currentView) params.set('view', currentView);
    const qs = params.toString();
    router.push(qs ? `/games?${qs}` : '/games');
  }, [searchParams, router]);

  // Complexity preset handler
  const handleComplexityChange = useCallback((value: string) => {
    if (value === '') {
      setMinComplexity('');
      setMaxComplexity('');
    } else {
      const [min, max] = value.split('-');
      setMinComplexity(min);
      setMaxComplexity(max);
    }
  }, []);

  // Current complexity select value
  const complexityValue = minComplexity && maxComplexity ? `${minComplexity}-${maxComplexity}` : '';

  // Current sort select value
  const sortValue = sortBy ? (sortDesc ? `${sortBy}:desc` : sortBy) : '';

  const handleSortChange = useCallback((value: string) => {
    if (value === '') {
      setSortBy('');
      setSortDesc(false);
    } else if (value.endsWith(':desc')) {
      setSortBy(value.replace(':desc', ''));
      setSortDesc(true);
    } else {
      setSortBy(value);
      setSortDesc(false);
    }
  }, []);

  return (
    <nav className="flex flex-col gap-0.5 px-2 py-3" aria-label="Games catalog navigation">
      {/* Back to Dashboard */}
      <FilterLink
        href="/library"
        icon={LayoutDashboard}
        label="Dashboard"
        isActive={false}
        isCollapsed={isCollapsed}
      />
      <hr className="my-1 border-sidebar-border" />

      {/* All games */}
      <FilterLink
        href="/library"
        icon={Gamepad2}
        label="Tutti i giochi"
        isActive={isAllGames}
        isCollapsed={isCollapsed}
      />

      {/* Quick Filters */}
      <SectionLabel label="Filtri rapidi" isCollapsed={isCollapsed} />
      <FilterLink
        href="/library?sortBy=AverageRating&sortDesc=true"
        icon={Star}
        label="Top Rated"
        isActive={isTopBgg}
        isCollapsed={isCollapsed}
      />
      <FilterLink
        href="/library?minPlayers=2&maxPlayers=2"
        icon={Users}
        label="2 Giocatori"
        isActive={is2Players}
        isCollapsed={isCollapsed}
      />
      <FilterLink
        href="/library?minPlayers=3&maxPlayers=6"
        icon={Users}
        label="3-6 Giocatori"
        isActive={is3to6Players}
        isCollapsed={isCollapsed}
      />

      {/* Advanced Section */}
      <SectionLabel label="Avanzato" isCollapsed={isCollapsed} />
      <FilterLink
        href="/library?sortBy=CreatedAt&sortDesc=true"
        icon={Clock}
        label="Aggiunti di recente"
        isActive={isRecent}
        isCollapsed={isCollapsed}
      />

      {/* Advanced filter toggle */}
      {!isCollapsed && (
        <button
          onClick={() => setIsAdvancedOpen(prev => !prev)}
          className={cn(
            'flex items-center gap-3 rounded-lg text-sm font-medium',
            'min-h-[44px] px-3 py-2 w-full text-left',
            'transition-colors duration-150',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring focus-visible:ring-offset-1',
            isAdvancedOpen
              ? 'bg-sidebar-accent text-sidebar-accent-foreground'
              : 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
          )}
          aria-expanded={isAdvancedOpen}
          aria-controls="advanced-filters-panel"
        >
          <SlidersHorizontal className="h-4 w-4 shrink-0" aria-hidden="true" />
          <span className="truncate flex-1">Filtri avanzati</span>

          {/* Active filter badge */}
          {activeFilterCount > 0 && !isAdvancedOpen && (
            <span className="bg-orange-500 text-white text-[10px] rounded-full px-1.5 min-w-[18px] text-center font-semibold leading-[18px]">
              {activeFilterCount}
            </span>
          )}

          <ChevronDown
            className={cn(
              'h-3.5 w-3.5 shrink-0 transition-transform duration-200',
              isAdvancedOpen && 'rotate-180'
            )}
            aria-hidden="true"
          />
        </button>
      )}

      {/* Advanced filter panel (expandable) */}
      <AnimatePresence initial={false}>
        {isAdvancedOpen && !isCollapsed && (
          <motion.div
            id="advanced-filters-panel"
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1, transition: { duration: 0.2, ease: 'easeOut' } }}
            exit={{ height: 0, opacity: 0, transition: { duration: 0.15, ease: 'easeIn' } }}
            className="overflow-hidden"
          >
            <div className="bg-sidebar-accent/30 rounded-lg p-3 mx-0 mt-1 space-y-3">
              {/* Categories */}
              {categories.length > 0 && (
                <CheckboxFilterList
                  items={categories}
                  selectedIds={selectedCategories}
                  onToggle={toggleCategory}
                  label="Categorie"
                />
              )}

              {/* Mechanics */}
              {mechanics.length > 0 && (
                <CheckboxFilterList
                  items={mechanics}
                  selectedIds={selectedMechanics}
                  onToggle={toggleMechanic}
                  label="Meccaniche"
                />
              )}

              {/* Loading state for ref data */}
              {!hasFetchedRefData && (
                <p className="text-[11px] text-sidebar-foreground/40 text-center py-2">
                  Caricamento filtri...
                </p>
              )}

              {/* Players — two selects side by side */}
              <div className="space-y-1">
                <Label className="text-[11px] font-semibold uppercase tracking-wider text-sidebar-foreground/50">
                  Giocatori
                </Label>
                <div className="grid grid-cols-2 gap-2">
                  <select
                    value={minPlayers}
                    onChange={e => setMinPlayers(e.target.value)}
                    aria-label="Numero minimo giocatori"
                    className="w-full rounded-md border border-sidebar-border bg-sidebar/50 px-2 py-1.5 text-xs text-sidebar-foreground focus:outline-none focus:ring-1 focus:ring-sidebar-ring"
                  >
                    <option value="">Min</option>
                    <option value="1">1</option>
                    <option value="2">2</option>
                    <option value="3">3</option>
                    <option value="4">4</option>
                    <option value="5">5</option>
                    <option value="6">6+</option>
                  </select>
                  <select
                    value={maxPlayers}
                    onChange={e => setMaxPlayers(e.target.value)}
                    aria-label="Numero massimo giocatori"
                    className="w-full rounded-md border border-sidebar-border bg-sidebar/50 px-2 py-1.5 text-xs text-sidebar-foreground focus:outline-none focus:ring-1 focus:ring-sidebar-ring"
                  >
                    <option value="">Max</option>
                    <option value="2">2</option>
                    <option value="3">3</option>
                    <option value="4">4</option>
                    <option value="5">5</option>
                    <option value="6">6</option>
                    <option value="8">8</option>
                    <option value="10">10+</option>
                  </select>
                </div>
              </div>

              {/* Playing time */}
              <FilterSelect
                label="Tempo di gioco"
                value={maxPlayingTime}
                onChange={setMaxPlayingTime}
                options={[
                  { value: '', label: 'Qualsiasi' },
                  { value: '30', label: '< 30 min' },
                  { value: '60', label: '30-60 min' },
                  { value: '120', label: '1-2 ore' },
                ]}
              />

              {/* Complexity */}
              <FilterSelect
                label="Difficolta"
                value={complexityValue}
                onChange={handleComplexityChange}
                options={[
                  { value: '', label: 'Qualsiasi' },
                  { value: '1-2', label: 'Leggero (1-2)' },
                  { value: '2-3', label: 'Medio (2-3)' },
                  { value: '3-4', label: 'Pesante (3-4)' },
                  { value: '4-5', label: 'Molto pesante (4-5)' },
                ]}
              />

              {/* Sort */}
              <FilterSelect
                label="Ordina per"
                value={sortValue}
                onChange={handleSortChange}
                options={[
                  { value: '', label: 'Predefinito' },
                  { value: 'Title', label: 'Nome' },
                  { value: 'AverageRating:desc', label: 'Rating' },
                  { value: 'YearPublished:desc', label: 'Anno' },
                  { value: 'CreatedAt:desc', label: 'Recenti' },
                  { value: 'ComplexityRating', label: 'Complessita' },
                ]}
              />

              {/* Action buttons */}
              <div className="flex gap-2 pt-1">
                <button
                  onClick={applyFilters}
                  className="flex-1 rounded-md bg-[hsl(25_95%_45%)] px-3 py-1.5 text-xs font-semibold text-white transition-colors hover:bg-[hsl(25_95%_40%)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring"
                >
                  Applica
                </button>
                <button
                  onClick={resetFilters}
                  className="flex items-center justify-center gap-1 rounded-md px-3 py-1.5 text-xs font-medium text-sidebar-foreground/70 transition-colors hover:bg-sidebar-accent hover:text-sidebar-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring"
                >
                  <RotateCcw className="h-3 w-3" aria-hidden="true" />
                  Reset
                </button>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </nav>
  );
}
