/**
 * RecordFilters — Sticky Filter Bar
 *
 * AC-1.2: RecordFilters sticky top (position: sticky; top: 0) con:
 * - search input
 * - 4 status chips (Tutte, In corso, Completate, Pianificate)
 * - 4 dropdown (gioco/data/esito/sort) — stub for now, fully functional later
 * - view toggle list/grid with radiogroup role
 *
 * Issue #1488: Play Records Index Reskin (Task 1)
 */

'use client';

import { Search, ChevronDown } from 'lucide-react';
import { useTranslations } from 'next-intl';

import type { PlayRecordStatus } from '@/lib/api/schemas/play-records.schemas';
import { cn } from '@/lib/utils';

export interface RecordFiltersProps {
  statusFilter: PlayRecordStatus | 'all';
  view: 'list' | 'grid';
  search: string;
  onStatusChange: (status: PlayRecordStatus | 'all') => void;
  onViewChange: (view: 'list' | 'grid') => void;
  onSearchChange: (search: string) => void;
}

const STATUS_OPTIONS: Array<{ value: PlayRecordStatus | 'all'; icon?: string; key: string }> = [
  { value: 'all', key: 'statusAll' },
  { value: 'InProgress', icon: '●', key: 'statusInProgress' },
  { value: 'Completed', icon: '✓', key: 'statusCompleted' },
  { value: 'Planned', icon: '📅', key: 'statusPlanned' },
];

const DROPDOWN_OPTIONS = [
  { key: 'dropdownGame', label: 'GIOCO', value: 'Tutti' },
  { key: 'dropdownDate', label: 'DATA', value: 'Sempre' },
  { key: 'dropdownOutcome', label: 'ESITO', value: 'Tutti' },
  { key: 'dropdownSort', label: 'SORT', value: 'Data ↓' },
];

export function RecordFilters({
  statusFilter,
  view,
  search,
  onStatusChange,
  onViewChange,
  onSearchChange,
}: RecordFiltersProps) {
  const t = useTranslations('playRecords.index');

  return (
    <div className="sticky top-0 z-8 space-y-3 border-b border-border-light bg-glass/80 px-4 py-3 backdrop-blur-md sm:px-8 sm:py-4 lg:px-12">
      {/* Search */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <input
          type="search"
          placeholder={t('filters.searchPlaceholder')}
          value={search}
          onChange={e => onSearchChange(e.target.value)}
          role="searchbox"
          className="w-full rounded-md border border-border bg-card px-3 py-2 pl-9 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring/30"
          data-testid="record-filters-search"
        />
      </div>

      {/* Status chips row */}
      <div className="flex items-center gap-2 overflow-x-auto pb-0.5 scrollbar-none">
        {STATUS_OPTIONS.map(option => (
          <button
            key={option.value}
            type="button"
            onClick={() => onStatusChange(option.value)}
            role="button"
            aria-pressed={statusFilter === option.value}
            className={cn(
              'flex-shrink-0 inline-flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-xs font-bold transition-colors',
              statusFilter === option.value
                ? 'border-entity-session/40 bg-entity-session/14 text-entity-session'
                : 'border-border bg-card/50 text-muted-foreground hover:border-border-strong'
            )}
            data-testid={`filter-status-${option.value}`}
          >
            {option.icon && <span aria-hidden="true">{option.icon}</span>}
            {t(option.key as any)}
          </button>
        ))}
      </div>

      {/* Dropdowns + view toggle row */}
      <div className="flex items-center justify-between gap-2 overflow-x-auto pb-0.5 scrollbar-none">
        <div className="flex items-center gap-2 flex-shrink-0">
          {DROPDOWN_OPTIONS.map(option => (
            <button
              key={option.label}
              type="button"
              className="flex-shrink-0 inline-flex items-center gap-1 rounded-md border border-border bg-card px-2.5 py-1.5 text-xs font-bold text-muted-foreground hover:border-border-strong transition-colors"
              data-testid={`dropdown-${option.label}`}
            >
              <span className="font-mono text-[9px] uppercase tracking-wider text-muted-foreground/60">
                {option.label}
              </span>
              <span className="text-xs">{option.value}</span>
              <ChevronDown className="h-3 w-3 opacity-60" aria-hidden="true" />
            </button>
          ))}
        </div>

        <div className="flex-shrink-0" />

        {/* View toggle (radiogroup) */}
        <div
          role="radiogroup"
          aria-label="Vista"
          className="flex-shrink-0 inline-flex items-center rounded-md border border-border bg-card overflow-hidden"
        >
          {[
            { id: 'list', icon: '☰', label: t('filters.viewList') },
            { id: 'grid', icon: '▦', label: t('filters.viewGrid') },
          ].map(option => (
            <button
              key={option.id}
              type="button"
              role="radio"
              aria-checked={view === option.id}
              aria-label={option.label}
              onClick={() => onViewChange(option.id as 'list' | 'grid')}
              className={cn(
                'px-2.5 py-1.5 border-none font-bold text-sm transition-colors',
                view === option.id
                  ? 'bg-entity-session/14 text-entity-session'
                  : 'bg-transparent text-muted-foreground hover:text-foreground'
              )}
              data-testid={`view-toggle-${option.id}`}
            >
              {option.icon}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
