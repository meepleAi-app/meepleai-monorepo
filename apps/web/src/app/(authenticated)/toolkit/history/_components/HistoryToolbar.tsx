'use client';

import { useEffect, useRef, useState, type JSX, type ReactNode } from 'react';

import {
  Calendar as CalendarIcon,
  Download,
  Filter as FilterIcon,
  LayoutGrid,
  List,
  Loader2,
  Search as SearchIcon,
  X,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/overlays/popover';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';
import { useTranslation, type TranslationFunction } from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';

import {
  countActiveFilters,
  NO_WINNER_FILTER_VALUE,
  type DateRangePreset,
  type HistoryFilterState,
  type HistorySort,
} from '../_lib/history-filters';

/** Debounce delay (ms) applied to search input before it is committed via `onChange`. */
const SEARCH_DEBOUNCE_MS = 400;

/** Games popover: number of options shown before "Show more". */
const GAMES_VISIBLE_THRESHOLD = 4;

/** Winners popover: number of options shown before "Show more". */
const WINNERS_VISIBLE_THRESHOLD = 5;

const DATE_PRESETS: DateRangePreset[] = ['all', 'last30', 'last90', 'lastYear', 'custom'];

const SORT_OPTIONS: HistorySort[] = ['recent', 'oldest', 'longest', 'score'];

export interface HistoryToolbarGameOption {
  id: string;
  label: string;
  count: number;
}

export interface HistoryToolbarWinnerOption {
  value: string;
  label: string;
  count: number;
}

export interface HistoryToolbarProps {
  state: HistoryFilterState;
  onChange: (next: HistoryFilterState) => void;
  gameOptions: HistoryToolbarGameOption[];
  winnerOptions: HistoryToolbarWinnerOption[];
  view: 'table' | 'cards';
  onViewChange: (view: 'table' | 'cards') => void;
  totalCount: number;
  resultCount: number;
  onClearAll: () => void;
  onExport: () => void;
}

interface MultiSelectOption {
  value: string;
  label: string;
  count: number;
}

interface MultiSelectPopoverProps {
  icon: ReactNode;
  triggerLabel: string;
  ariaLabel: string;
  options: MultiSelectOption[];
  selected: string[];
  onToggle: (value: string) => void;
  onClear: () => void;
  moreThreshold: number;
  allLabel: string;
  moreLabel: (n: number) => string;
  lessLabel: string;
}

/**
 * Reusable Popover + Checkbox multi-select used for both the Giochi and
 * Vincitori filter groups. Mirrors `MultiDropdown` in
 * `admin-mockups/design_files/sp4-toolkit-history-ui.jsx`: a "Tutti" clear
 * option, then a `moreThreshold`-capped option list with a "Mostra
 * altri/meno" toggle.
 */
function MultiSelectPopover({
  icon,
  triggerLabel,
  ariaLabel,
  options,
  selected,
  onToggle,
  onClear,
  moreThreshold,
  allLabel,
  moreLabel,
  lessLabel,
}: MultiSelectPopoverProps): JSX.Element {
  const [showAll, setShowAll] = useState(false);
  const visible = showAll ? options : options.slice(0, moreThreshold);
  const hasMore = options.length > moreThreshold;
  const count = selected.length;

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          size="sm"
          aria-label={triggerLabel}
          className={cn('gap-1.5', count > 0 && 'border-primary text-primary')}
        >
          <span aria-hidden="true">{icon}</span>
          {triggerLabel}
          {count > 0 && <span className="text-xs font-semibold">({count})</span>}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-64 p-2" role="listbox" aria-label={ariaLabel}>
        <div className="px-2 py-1 text-xs font-semibold text-muted-foreground">{ariaLabel}</div>
        <button
          type="button"
          role="option"
          aria-selected={count === 0}
          onClick={onClear}
          className={cn(
            'flex w-full items-center rounded px-2 py-1.5 text-left text-sm hover:bg-muted',
            count === 0 && 'font-semibold text-foreground'
          )}
        >
          {allLabel}
        </button>
        {visible.map(option => {
          const isSelected = selected.includes(option.value);
          return (
            <div
              key={option.value}
              className="flex items-center gap-2 rounded px-2 py-1.5 text-sm hover:bg-muted"
            >
              <Checkbox
                checked={isSelected}
                onCheckedChange={() => onToggle(option.value)}
                aria-label={option.label}
              />
              <span className="flex-1 truncate">{option.label}</span>
              <span className="text-xs text-muted-foreground">{option.count}</span>
            </div>
          );
        })}
        {hasMore && (
          <button
            type="button"
            onClick={() => setShowAll(a => !a)}
            className="mt-1 w-full rounded px-2 py-1.5 text-left text-xs font-medium text-primary hover:bg-muted"
          >
            {showAll ? lessLabel : moreLabel(options.length - moreThreshold)}
          </button>
        )}
      </PopoverContent>
    </Popover>
  );
}

interface DateRangeFilterPopoverProps {
  state: HistoryFilterState;
  onChange: (next: HistoryFilterState) => void;
  t: TranslationFunction;
}

/**
 * Date-range filter: preset radio group (all/last30/last90/lastYear) applied
 * immediately, plus a "custom" option that reveals two date inputs and an
 * explicit "Applica intervallo" button — matching the mockup's `customMode`
 * pattern (selecting "custom" does not commit a range until Applica is
 * clicked with both bounds filled in).
 *
 * The popover is a CONTROLLED Radix `<Popover>` (`open`/`onOpenChange`) so it
 * can be closed programmatically: selecting a non-custom preset or clicking
 * "Applica intervallo" closes it (mirrors `setOpenDate(false)` in
 * `admin-mockups/design_files/sp4-toolkit-history-ui.jsx:176,190`), while
 * switching to the "custom" preset keeps it open so the user can fill in the
 * date inputs.
 */
function DateRangeFilterPopover({ state, onChange, t }: DateRangeFilterPopoverProps): JSX.Element {
  const [open, setOpen] = useState(false);
  const [customMode, setCustomMode] = useState(state.datePreset === 'custom');
  const [customFrom, setCustomFrom] = useState(state.dateFrom ?? '');
  const [customTo, setCustomTo] = useState(state.dateTo ?? '');

  useEffect(() => {
    setCustomMode(state.datePreset === 'custom');
    setCustomFrom(state.dateFrom ?? '');
    setCustomTo(state.dateTo ?? '');
  }, [state.datePreset, state.dateFrom, state.dateTo]);

  const radioValue = customMode ? 'custom' : state.datePreset;
  const isActive = state.datePreset !== 'all';

  const handlePresetChange = (preset: string) => {
    if (preset === 'custom') {
      setCustomMode(true);
      return;
    }
    setCustomMode(false);
    onChange({
      ...state,
      datePreset: preset as DateRangePreset,
      dateFrom: undefined,
      dateTo: undefined,
    });
    setOpen(false);
  };

  const handleApply = () => {
    if (!customFrom || !customTo) return;
    onChange({ ...state, datePreset: 'custom', dateFrom: customFrom, dateTo: customTo });
    setOpen(false);
  };

  const triggerLabel =
    state.datePreset === 'custom' && state.dateFrom && state.dateTo
      ? `${state.dateFrom} – ${state.dateTo}`
      : t(`pages.toolkitHistory.filters.dateOptions.${state.datePreset}`);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          size="sm"
          aria-label={t('pages.toolkitHistory.filters.date')}
          className={cn('gap-1.5', isActive && 'border-primary text-primary')}
        >
          <CalendarIcon className="h-3.5 w-3.5" aria-hidden="true" />
          {triggerLabel}
        </Button>
      </PopoverTrigger>
      <PopoverContent
        align="start"
        className="w-72 p-3"
        role="listbox"
        aria-label={t('pages.toolkitHistory.filters.dateRange')}
      >
        <div className="mb-2 text-xs font-semibold text-muted-foreground">
          {t('pages.toolkitHistory.filters.dateRange')}
        </div>
        <RadioGroup value={radioValue} onValueChange={handlePresetChange} className="gap-1">
          {DATE_PRESETS.map(preset => (
            <label
              key={preset}
              className="flex cursor-pointer items-center gap-2 rounded px-1 py-1 text-sm hover:bg-muted"
            >
              <RadioGroupItem value={preset} />
              {t(`pages.toolkitHistory.filters.dateOptions.${preset}`)}
            </label>
          ))}
        </RadioGroup>
        {customMode && (
          <div className="mt-3 space-y-2 border-t border-border pt-3">
            <div className="grid grid-cols-2 gap-2">
              <label className="flex flex-col gap-1 text-xs text-muted-foreground">
                {t('pages.toolkitHistory.filters.dateFrom')}
                <Input
                  type="date"
                  value={customFrom}
                  max={customTo || undefined}
                  onChange={e => setCustomFrom(e.target.value)}
                  aria-label={t('pages.toolkitHistory.filters.dateFromLabel')}
                />
              </label>
              <label className="flex flex-col gap-1 text-xs text-muted-foreground">
                {t('pages.toolkitHistory.filters.dateTo')}
                <Input
                  type="date"
                  value={customTo}
                  min={customFrom || undefined}
                  onChange={e => setCustomTo(e.target.value)}
                  aria-label={t('pages.toolkitHistory.filters.dateToLabel')}
                />
              </label>
            </div>
            <Button
              type="button"
              size="sm"
              className="w-full"
              disabled={!customFrom || !customTo}
              onClick={handleApply}
            >
              {t('pages.toolkitHistory.filters.applyRange')}
            </Button>
          </div>
        )}
      </PopoverContent>
    </Popover>
  );
}

/**
 * Filter/sort toolbar for /toolkit/history (Issue #3006, Task A7).
 *
 * Matches `admin-mockups/design_files/sp4-toolkit-history-ui.jsx`'s
 * `Toolbar`: `[ search ] [ Giochi | Data | Vincitori ] [ Ordina | Vista ]`,
 * an "Esporta CSV" action, and a conditional summary bar (`nActive > 0`)
 * showing the active-filter count, the filtered/total result count, and a
 * "Cancella tutto" reset.
 *
 * Search is debounced 400ms client-side (`localSearch` state) so `onChange`
 * — and therefore any parent-side refetch/re-filter — only fires once
 * typing pauses; a "Cercando…" indicator is shown while the debounce is
 * pending.
 */
export function HistoryToolbar({
  state,
  onChange,
  gameOptions,
  winnerOptions,
  view,
  onViewChange,
  totalCount,
  resultCount,
  onClearAll,
  onExport,
}: HistoryToolbarProps): JSX.Element {
  const { t } = useTranslation();

  const [localSearch, setLocalSearch] = useState(state.search);
  const [isSearching, setIsSearching] = useState(false);
  const searchTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const stateRef = useRef(state);
  const onChangeRef = useRef(onChange);

  useEffect(() => {
    stateRef.current = state;
  }, [state]);

  useEffect(() => {
    onChangeRef.current = onChange;
  }, [onChange]);

  useEffect(() => {
    setLocalSearch(state.search);
  }, [state.search]);

  useEffect(
    () => () => {
      if (searchTimerRef.current) clearTimeout(searchTimerRef.current);
    },
    []
  );

  const commitSearch = (value: string) => {
    if (searchTimerRef.current) {
      clearTimeout(searchTimerRef.current);
      searchTimerRef.current = null;
    }
    setIsSearching(false);
    onChangeRef.current({ ...stateRef.current, search: value });
  };

  const handleSearchInput = (value: string) => {
    setLocalSearch(value);
    if (searchTimerRef.current) clearTimeout(searchTimerRef.current);
    setIsSearching(true);
    searchTimerRef.current = setTimeout(() => {
      searchTimerRef.current = null;
      commitSearch(value);
    }, SEARCH_DEBOUNCE_MS);
  };

  const handleClearSearch = () => {
    setLocalSearch('');
    commitSearch('');
  };

  const gameFilterOptions: MultiSelectOption[] = gameOptions.map(g => ({
    value: g.id,
    label: g.label,
    count: g.count,
  }));

  const winnerFilterOptions: MultiSelectOption[] = winnerOptions.map(w => ({
    value: w.value,
    label:
      w.value === NO_WINNER_FILTER_VALUE
        ? t('pages.toolkitHistory.filters.noWinner')
        : t('pages.toolkitHistory.filters.winnerWon', { name: w.label }),
    count: w.count,
  }));

  const activeCount = countActiveFilters(state);

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-3 lg:flex-row lg:flex-wrap lg:items-center">
        {/* search */}
        <div className="relative min-w-[240px] flex-1">
          <SearchIcon
            className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
            aria-hidden="true"
          />
          <Input
            type="search"
            value={localSearch}
            onChange={e => handleSearchInput(e.target.value)}
            onKeyDown={e => {
              if (e.key === 'Escape') handleClearSearch();
            }}
            placeholder={t('pages.toolkitHistory.filters.searchPlaceholder')}
            aria-label={t('pages.toolkitHistory.filters.searchAriaLabel')}
            className="pl-9 pr-10"
          />
          <div className="absolute right-1 top-1/2 flex -translate-y-1/2 items-center gap-1">
            {isSearching && (
              <span className="flex items-center gap-1 pr-1 text-xs text-muted-foreground">
                <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" />
                {t('pages.toolkitHistory.filters.searching')}
              </span>
            )}
            {localSearch && (
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="h-7 w-7"
                onClick={handleClearSearch}
                aria-label={t('pages.toolkitHistory.filters.clearSearch')}
              >
                <X className="h-3.5 w-3.5" />
              </Button>
            )}
          </div>
        </div>

        {/* filters */}
        <div
          className="flex flex-wrap items-center gap-2"
          role="group"
          aria-label={t('pages.toolkitHistory.filters.filterByGame')}
        >
          <MultiSelectPopover
            icon="🎲"
            triggerLabel={t('pages.toolkitHistory.filters.games')}
            ariaLabel={t('pages.toolkitHistory.filters.filterByGame')}
            options={gameFilterOptions}
            selected={state.gameIds}
            moreThreshold={GAMES_VISIBLE_THRESHOLD}
            allLabel={t('pages.toolkitHistory.filters.all')}
            moreLabel={n => t('pages.toolkitHistory.filters.showMore', { n })}
            lessLabel={t('pages.toolkitHistory.filters.showLess')}
            onToggle={value =>
              onChange({
                ...state,
                gameIds: state.gameIds.includes(value)
                  ? state.gameIds.filter(v => v !== value)
                  : [...state.gameIds, value],
              })
            }
            onClear={() => onChange({ ...state, gameIds: [] })}
          />

          <DateRangeFilterPopover state={state} onChange={onChange} t={t} />

          <MultiSelectPopover
            icon="🏆"
            triggerLabel={t('pages.toolkitHistory.filters.winners')}
            ariaLabel={t('pages.toolkitHistory.filters.filterByWinner')}
            options={winnerFilterOptions}
            selected={state.winners}
            moreThreshold={WINNERS_VISIBLE_THRESHOLD}
            allLabel={t('pages.toolkitHistory.filters.all')}
            moreLabel={n => t('pages.toolkitHistory.filters.showMore', { n })}
            lessLabel={t('pages.toolkitHistory.filters.showLess')}
            onToggle={value =>
              onChange({
                ...state,
                winners: state.winners.includes(value)
                  ? state.winners.filter(v => v !== value)
                  : [...state.winners, value],
              })
            }
            onClear={() => onChange({ ...state, winners: [] })}
          />
        </div>

        {/* sort + view + export */}
        <div className="flex items-center gap-2 lg:ml-auto">
          <Select
            value={state.sort}
            onValueChange={value => onChange({ ...state, sort: value as HistorySort })}
          >
            <SelectTrigger
              className="h-9 w-auto min-w-[9rem]"
              aria-label={t('pages.toolkitHistory.filters.sortBy')}
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {SORT_OPTIONS.map(opt => (
                <SelectItem key={opt} value={opt}>
                  {t(`pages.toolkitHistory.filters.sortOptions.${opt}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <div
            className="flex items-center gap-1 rounded-lg border border-border p-0.5"
            role="group"
            aria-label={t('pages.toolkitHistory.filters.view')}
          >
            <Button
              type="button"
              variant={view === 'table' ? 'default' : 'ghost'}
              size="icon"
              className="h-8 w-8"
              aria-pressed={view === 'table'}
              aria-label={t('pages.toolkitHistory.filters.viewTable')}
              onClick={() => onViewChange('table')}
            >
              <List className="h-4 w-4" aria-hidden="true" />
            </Button>
            <Button
              type="button"
              variant={view === 'cards' ? 'default' : 'ghost'}
              size="icon"
              className="h-8 w-8"
              aria-pressed={view === 'cards'}
              aria-label={t('pages.toolkitHistory.filters.viewCards')}
              onClick={() => onViewChange('cards')}
            >
              <LayoutGrid className="h-4 w-4" aria-hidden="true" />
            </Button>
          </div>

          <Button type="button" variant="outline" size="sm" onClick={onExport} className="gap-1.5">
            <Download className="h-3.5 w-3.5" aria-hidden="true" />
            {t('pages.toolkitHistory.hero.exportCsv')}
          </Button>
        </div>
      </div>

      {/* summary bar */}
      {activeCount > 0 && (
        <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-muted/40 px-3 py-2 text-sm">
          <Badge variant="secondary" className="gap-1">
            <FilterIcon className="h-3 w-3" aria-hidden="true" />
            {t('pages.toolkitHistory.summary.activeFilters', { n: activeCount })}
          </Badge>
          <span className="text-muted-foreground" aria-hidden="true">
            ·
          </span>
          <span className="text-muted-foreground">
            {t('pages.toolkitHistory.summary.results', { n: resultCount, total: totalCount })}
          </span>
          <span className="flex-1" />
          <Button type="button" variant="ghost" size="sm" onClick={onClearAll}>
            {t('pages.toolkitHistory.summary.clearAll')}
          </Button>
        </div>
      )}
    </div>
  );
}
