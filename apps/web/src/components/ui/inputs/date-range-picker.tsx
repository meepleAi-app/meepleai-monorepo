/**
 * DateRangePicker Component - Issue #903 Enhancement
 *
 * Advanced date range picker for filtering.
 * Features:
 * - Quick presets (Today, Last 7 days, Last 30 days, Custom)
 * - Manual date input
 * - Clear functionality
 * - Keyboard accessible
 */

import React, { useState } from 'react';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Calendar, X } from 'lucide-react';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

export interface DateRange {
  from?: Date;
  to?: Date;
}

export interface DateRangePickerProps {
  /** Current date range */
  value?: DateRange;

  /** Callback when date range changes */
  onChange: (range: DateRange) => void;

  /** Label for the picker */
  label?: string;

  /** Optional CSS classes */
  className?: string;
}

type Preset = 'today' | 'last7days' | 'last30days' | 'last90days' | 'custom';

/**
 * Format date for input[type="date"]
 */
function formatDateForInput(date?: Date): string {
  if (!date) return '';
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * Parse date from input[type="date"]
 */
function parseDateFromInput(value: string): Date | undefined {
  if (!value) return undefined;
  return new Date(value);
}

/**
 * Get date range for preset
 */
function getPresetRange(preset: Preset): DateRange {
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());

  switch (preset) {
    case 'today':
      return { from: today, to: today };
    case 'last7days': {
      const from = new Date(today);
      from.setDate(from.getDate() - 7);
      return { from, to: today };
    }
    case 'last30days': {
      const from = new Date(today);
      from.setDate(from.getDate() - 30);
      return { from, to: today };
    }
    case 'last90days': {
      const from = new Date(today);
      from.setDate(from.getDate() - 90);
      return { from, to: today };
    }
    default:
      return {};
  }
}

/**
 * DateRangePicker component
 */
export function DateRangePicker({ value, onChange, label, className = '' }: DateRangePickerProps) {
  const [selectedPreset, setSelectedPreset] = useState<Preset>('custom');

  // Generate unique IDs based on label to avoid conflicts with multiple DateRangePickers
  const labelSlug = label ? label.toLowerCase().replace(/\s+/g, '-') : 'date';
  const fromId = `${labelSlug}-from`;
  const toId = `${labelSlug}-to`;
  // Remove "Date" suffix from label for cleaner aria-labels (e.g., "Created Date" → "Created from date")
  const labelPrefix = label ? label.replace(/\s*Date$/i, '') : '';
  const fromAriaLabel = labelPrefix ? `${labelPrefix} from date` : 'From date';
  const toAriaLabel = labelPrefix ? `${labelPrefix} to date` : 'To date';

  const handlePresetChange = (preset: string) => {
    const p = preset as Preset;
    setSelectedPreset(p);

    if (p !== 'custom') {
      onChange(getPresetRange(p));
    }
  };

  const handleFromChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    onChange({
      ...value,
      from: parseDateFromInput(e.target.value),
    });
    setSelectedPreset('custom');
  };

  const handleToChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    onChange({
      ...value,
      to: parseDateFromInput(e.target.value),
    });
    setSelectedPreset('custom');
  };

  const handleClear = () => {
    onChange({});
    setSelectedPreset('custom');
  };

  const hasValue = value?.from || value?.to;

  return (
    <div className={`space-y-2 ${className}`}>
      {label && (
        <Label className="text-xs flex items-center gap-1">
          <Calendar className="w-3 h-3" />
          {label}
        </Label>
      )}

      {/* Preset selector */}
      <Select value={selectedPreset} onValueChange={handlePresetChange}>
        <SelectTrigger className="h-8 text-xs">
          <SelectValue placeholder="Select range" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="today">Today</SelectItem>
          <SelectItem value="last7days">Last 7 days</SelectItem>
          <SelectItem value="last30days">Last 30 days</SelectItem>
          <SelectItem value="last90days">Last 90 days</SelectItem>
          <SelectItem value="custom">Custom range</SelectItem>
        </SelectContent>
      </Select>

      {/* Custom date inputs */}
      <div className="grid grid-cols-2 gap-2">
        <div>
          <Label htmlFor={fromId} className="text-[10px] text-muted-foreground">
            From
          </Label>
          <input
            id={fromId}
            type="date"
            value={formatDateForInput(value?.from)}
            onChange={handleFromChange}
            className="h-8 w-full px-2 text-xs rounded-md border border-input bg-background"
            aria-label={fromAriaLabel}
          />
        </div>
        <div>
          <Label htmlFor={toId} className="text-[10px] text-muted-foreground">
            To
          </Label>
          <input
            id={toId}
            type="date"
            value={formatDateForInput(value?.to)}
            onChange={handleToChange}
            className="h-8 w-full px-2 text-xs rounded-md border border-input bg-background"
            aria-label={toAriaLabel}
          />
        </div>
      </div>

      {/* Clear button */}
      {hasValue && (
        <Button
          variant="ghost"
          size="sm"
          onClick={handleClear}
          className="h-6 text-xs w-full"
          aria-label="Clear date range"
        >
          <X className="w-3 h-3 mr-1" />
          Clear
        </Button>
      )}
    </div>
  );
}

export default DateRangePicker;
