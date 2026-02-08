/**
 * DateRangeFilter - Date range picker filter
 *
 * @module components/ui/data-display/entity-list-view/components/filters/date-range-filter
 */

'use client';

import React, { useState } from 'react';
import { Calendar } from '@/components/ui/calendar';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Calendar as CalendarIcon, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { format } from 'date-fns';
import type { DateRangeFilter as DateRangeFilterConfig } from '../../entity-list-view.types';
import type { DateRange } from 'react-day-picker';

export interface DateRangeFilterProps<T> {
  filter: DateRangeFilterConfig<T>;
  value: { start: Date; end: Date } | undefined;
  onChange: (value: { start: Date; end: Date } | undefined) => void;
}

export function DateRangeFilter<T>({
  filter,
  value,
  onChange,
}: DateRangeFilterProps<T>) {
  const [isOpen, setIsOpen] = useState(false);

  // Convert to react-day-picker DateRange format
  const dateRange: DateRange | undefined = value
    ? { from: value.start, to: value.end }
    : undefined;

  const handleSelect = (range: DateRange | undefined) => {
    if (range?.from && range?.to) {
      onChange({ start: range.from, end: range.to });
    } else if (range?.from) {
      // Single date selected - use as both start and end
      onChange({ start: range.from, end: range.from });
    } else {
      onChange(undefined);
    }
  };

  const displayText = value
    ? `${format(value.start, 'MMM d, yyyy')} - ${format(value.end, 'MMM d, yyyy')}`
    : `Select ${filter.label.toLowerCase()}...`;

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <Label className="text-sm font-medium">{filter.label}</Label>
        {value && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onChange(undefined)}
            className="h-6 px-2"
          >
            <X className="w-3 h-3" />
            <span className="sr-only">Clear date range</span>
          </Button>
        )}
      </div>

      {filter.description && (
        <p className="text-xs text-muted-foreground">{filter.description}</p>
      )}

      <Popover open={isOpen} onOpenChange={setIsOpen}>
        <PopoverTrigger asChild>
          <Button
            variant="outline"
            className={cn(
              'w-full justify-start text-left font-normal',
              !value && 'text-muted-foreground'
            )}
          >
            <CalendarIcon className="mr-2 h-4 w-4" />
            {displayText}
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-auto p-0" align="start">
          <Calendar
            mode="range"
            selected={dateRange}
            onSelect={handleSelect}
            numberOfMonths={2}
            initialFocus
          />
        </PopoverContent>
      </Popover>
    </div>
  );
}
