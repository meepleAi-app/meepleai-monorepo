/**
 * SelectFilter - Single/Multi-select dropdown filter
 *
 * @module components/ui/data-display/entity-list-view/components/filters/select-filter
 */

'use client';

import React from 'react';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Label } from '@/components/ui/label';
import type { SelectFilter as SelectFilterConfig } from '../../entity-list-view.types';

export interface SelectFilterProps<T> {
  filter: SelectFilterConfig<T>;
  value: string | string[] | undefined;
  onChange: (value: string | string[]) => void;
}

export function SelectFilter<T>({ filter, value, onChange }: SelectFilterProps<T>) {
  // For now, support single-select only (multi-select is complex with shadcn Select)
  // Multi-select can be added later with custom component or different primitive

  const currentValue = Array.isArray(value) ? value[0] || '' : value || '';

  return (
    <div className="space-y-2">
      <Label htmlFor={filter.id} className="text-sm font-medium">
        {filter.label}
      </Label>
      {filter.description && (
        <p className="text-xs text-muted-foreground">{filter.description}</p>
      )}

      <Select value={currentValue} onValueChange={(val) => onChange(val)}>
        <SelectTrigger id={filter.id} className="w-full">
          <SelectValue placeholder={`Select ${filter.label.toLowerCase()}...`} />
        </SelectTrigger>
        <SelectContent>
          {filter.options.map((option) => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
