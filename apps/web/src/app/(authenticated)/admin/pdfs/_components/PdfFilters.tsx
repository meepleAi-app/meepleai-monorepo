'use client';

import { X } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import type { PdfState } from '@/types/pdf';

const ALL_STATES: PdfState[] = ['pending', 'uploading', 'extracting', 'chunking', 'embedding', 'indexing', 'ready', 'failed'];

const SIZE_PRESETS = [
  { label: 'All sizes', min: undefined, max: undefined },
  { label: '< 1 MB', min: undefined, max: 1_048_576 },
  { label: '1-10 MB', min: 1_048_576, max: 10_485_760 },
  { label: '10-100 MB', min: 10_485_760, max: 104_857_600 },
  { label: '> 100 MB', min: 104_857_600, max: undefined },
] as const;

const DATE_PRESETS = [
  { label: 'All time', value: undefined },
  { label: 'Last 24h', value: 1 },
  { label: 'Last 7d', value: 7 },
  { label: 'Last 30d', value: 30 },
] as const;

export interface PdfFilterState {
  states: PdfState[];
  sizePreset: number;
  datePreset: number;
}

interface PdfFiltersProps {
  filters: PdfFilterState;
  onFiltersChange: (filters: PdfFilterState) => void;
}

export const defaultFilters: PdfFilterState = {
  states: [],
  sizePreset: 0,
  datePreset: 0,
};

export function PdfFilters({ filters, onFiltersChange }: PdfFiltersProps) {
  const hasFilters = filters.states.length > 0 || filters.sizePreset > 0 || filters.datePreset > 0;

  const toggleState = (state: PdfState) => {
    const current = filters.states;
    const next = current.includes(state)
      ? current.filter(s => s !== state)
      : [...current, state];
    onFiltersChange({ ...filters, states: next });
  };

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        {/* State filters */}
        <span className="text-xs font-medium text-muted-foreground mr-1">State:</span>
        {ALL_STATES.map(state => (
          <button
            key={state}
            onClick={() => toggleState(state)}
            className={`px-2 py-1 rounded text-xs font-medium transition-colors ${
              filters.states.includes(state)
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:bg-muted/80'
            }`}
          >
            {state.charAt(0).toUpperCase() + state.slice(1)}
          </button>
        ))}

        <span className="text-border mx-1">|</span>

        {/* Size filter */}
        <span className="text-xs font-medium text-muted-foreground mr-1">Size:</span>
        <select
          value={filters.sizePreset}
          onChange={e => onFiltersChange({ ...filters, sizePreset: Number(e.target.value) })}
          className="h-7 rounded border bg-background px-2 text-xs"
        >
          {SIZE_PRESETS.map((preset, i) => (
            <option key={i} value={i}>{preset.label}</option>
          ))}
        </select>

        {/* Date filter */}
        <span className="text-xs font-medium text-muted-foreground mr-1">Date:</span>
        <select
          value={filters.datePreset}
          onChange={e => onFiltersChange({ ...filters, datePreset: Number(e.target.value) })}
          className="h-7 rounded border bg-background px-2 text-xs"
        >
          {DATE_PRESETS.map((preset, i) => (
            <option key={i} value={i}>{preset.label}</option>
          ))}
        </select>

        {hasFilters && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onFiltersChange(defaultFilters)}
            className="h-7 px-2 text-xs"
          >
            <X className="h-3 w-3 mr-1" />
            Clear
          </Button>
        )}
      </div>
    </div>
  );
}

/**
 * Converts filter state to API query params
 */
export function filtersToQueryParams(filters: PdfFilterState) {
  const params: Record<string, string | number | undefined> = {};

  if (filters.states.length === 1) {
    // Capitalize first letter for backend enum format
    params.state = filters.states[0].charAt(0).toUpperCase() + filters.states[0].slice(1);
  }

  const sizePreset = SIZE_PRESETS[filters.sizePreset];
  if (sizePreset) {
    params.minSizeBytes = sizePreset.min;
    params.maxSizeBytes = sizePreset.max;
  }

  const datePreset = DATE_PRESETS[filters.datePreset];
  if (datePreset?.value) {
    const d = new Date();
    d.setDate(d.getDate() - datePreset.value);
    params.uploadedAfter = d.toISOString();
  }

  return params;
}
