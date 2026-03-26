/**
 * RangeFilter - Numeric range slider filter
 *
 * @module components/ui/data-display/entity-list-view/components/filters/range-filter
 */

'use client';

import React from 'react';

import { Label } from '@/components/ui/primitives/label';
import { Slider } from '@/components/ui/slider';

import type { RangeFilter as RangeFilterConfig } from '../../entity-list-view.types';

export interface RangeFilterProps<T> {
  filter: RangeFilterConfig<T>;
  value: { min: number; max: number } | undefined;
  onChange: (value: { min: number; max: number }) => void;
}

export function RangeFilter<T>({ filter, value, onChange }: RangeFilterProps<T>) {
  const currentValue = value || { min: filter.min, max: filter.max };
  const step = filter.step || 1;

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <Label htmlFor={filter.id} className="text-sm font-medium">
          {filter.label}
        </Label>
        <span className="text-sm text-muted-foreground">
          {currentValue.min}
          {filter.unit} - {currentValue.max}
          {filter.unit}
        </span>
      </div>

      {filter.description && <p className="text-xs text-muted-foreground">{filter.description}</p>}

      {/* Dual-handle slider */}
      <Slider
        id={filter.id}
        min={filter.min}
        max={filter.max}
        step={step}
        value={[currentValue.min, currentValue.max]}
        onValueChange={([min, max]) => onChange({ min, max })}
        className="w-full"
      />

      <div className="flex justify-between text-xs text-muted-foreground">
        <span>
          Min: {filter.min}
          {filter.unit}
        </span>
        <span>
          Max: {filter.max}
          {filter.unit}
        </span>
      </div>
    </div>
  );
}
