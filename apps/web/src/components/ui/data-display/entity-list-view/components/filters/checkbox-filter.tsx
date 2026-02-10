/**
 * CheckboxFilter - Boolean toggle filter
 *
 * @module components/ui/data-display/entity-list-view/components/filters/checkbox-filter
 */

'use client';

import React from 'react';

import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';

import type { CheckboxFilter as CheckboxFilterConfig } from '../../entity-list-view.types';

export interface CheckboxFilterProps<T> {
  filter: CheckboxFilterConfig<T>;
  value: boolean | undefined;
  onChange: (value: boolean) => void;
}

export function CheckboxFilter<T>({
  filter,
  value,
  onChange,
}: CheckboxFilterProps<T>) {
  return (
    <div className="flex items-start space-x-3">
      <Checkbox
        id={filter.id}
        checked={value || false}
        onCheckedChange={(checked) => onChange(checked as boolean)}
      />
      <div className="space-y-1 leading-none">
        <Label htmlFor={filter.id} className="text-sm font-medium cursor-pointer">
          {filter.label}
        </Label>
        {filter.description && (
          <p className="text-xs text-muted-foreground">{filter.description}</p>
        )}
      </div>
    </div>
  );
}
