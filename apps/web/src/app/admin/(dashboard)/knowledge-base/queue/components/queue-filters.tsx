'use client';

import { useState, useEffect } from 'react';
import { SearchIcon } from 'lucide-react';

import { Input } from '@/components/ui/primitives/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';

import { useDebounce } from '@/hooks/useDebounce';
import type { QueueFilters } from '../lib/queue-api';

interface QueueFiltersBarProps {
  filters: QueueFilters;
  onFiltersChange: (filters: QueueFilters) => void;
}

const STATUS_OPTIONS = [
  { value: 'all', label: 'All Statuses' },
  { value: 'Queued', label: 'Queued' },
  { value: 'Processing', label: 'Processing' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Failed', label: 'Failed' },
  { value: 'Cancelled', label: 'Cancelled' },
];

const DATE_OPTIONS = [
  { value: 'all', label: 'All Time' },
  { value: 'today', label: 'Today' },
  { value: '7d', label: 'Last 7 Days' },
  { value: '30d', label: 'Last 30 Days' },
];

function getDateRange(value: string): { fromDate?: string; toDate?: string } {
  if (value === 'all') return {};
  const now = new Date();
  const toDate = now.toISOString();

  switch (value) {
    case 'today': {
      const start = new Date(now);
      start.setHours(0, 0, 0, 0);
      return { fromDate: start.toISOString(), toDate };
    }
    case '7d': {
      const start = new Date(now);
      start.setDate(start.getDate() - 7);
      return { fromDate: start.toISOString(), toDate };
    }
    case '30d': {
      const start = new Date(now);
      start.setDate(start.getDate() - 30);
      return { fromDate: start.toISOString(), toDate };
    }
    default:
      return {};
  }
}

export function QueueFiltersBar({ filters, onFiltersChange }: QueueFiltersBarProps) {
  const [localSearch, setLocalSearch] = useState(filters.search ?? '');
  const [datePreset, setDatePreset] = useState('all');
  const debouncedSearch = useDebounce(localSearch, 300);

  useEffect(() => {
    const searchValue = debouncedSearch || undefined;
    if (searchValue !== filters.search) {
      onFiltersChange({ ...filters, search: searchValue, page: 1 });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedSearch]);

  return (
    <div className="flex flex-col sm:flex-row gap-3">
      {/* Search */}
      <div className="relative flex-1 min-w-[200px]">
        <SearchIcon className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Search by filename..."
          value={localSearch}
          onChange={(e) => setLocalSearch(e.target.value)}
          className="pl-9 bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md"
        />
      </div>

      {/* Status Filter */}
      <Select
        value={filters.status ?? 'all'}
        onValueChange={(value) =>
          onFiltersChange({
            ...filters,
            status: value === 'all' ? undefined : value,
            page: 1,
          })
        }
      >
        <SelectTrigger className="w-[160px] bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md">
          <SelectValue placeholder="Status" />
        </SelectTrigger>
        <SelectContent>
          {STATUS_OPTIONS.map((opt) => (
            <SelectItem key={opt.value} value={opt.value}>
              {opt.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {/* Date Filter */}
      <Select
        value={datePreset}
        onValueChange={(value) => {
          setDatePreset(value);
          const range = getDateRange(value);
          onFiltersChange({ ...filters, ...range, page: 1 });
        }}
      >
        <SelectTrigger className="w-[160px] bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md">
          <SelectValue placeholder="Date Range" />
        </SelectTrigger>
        <SelectContent>
          {DATE_OPTIONS.map((opt) => (
            <SelectItem key={opt.value} value={opt.value}>
              {opt.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
