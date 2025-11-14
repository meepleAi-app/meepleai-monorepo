import React from 'react';
import { GameSortOptions, GameSortField, SortDirection } from '@/lib/api';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { ArrowUpDown, ArrowUp, ArrowDown } from 'lucide-react';
import { Label } from '@/components/ui/label';

interface GameSortControlProps {
  sortOptions: GameSortOptions | null;
  onChange: (sortOptions: GameSortOptions | null) => void;
}

const SORT_FIELDS: Array<{ value: GameSortField; label: string }> = [
  { value: 'title', label: 'Title' },
  { value: 'yearPublished', label: 'Year' },
  { value: 'minPlayers', label: 'Min Players' },
  { value: 'maxPlayers', label: 'Max Players' },
];

export function GameSortControl({ sortOptions, onChange }: GameSortControlProps) {
  const handleFieldChange = (field: string) => {
    if (!field) {
      onChange(null);
    } else {
      onChange({
        field: field as GameSortField,
        direction: sortOptions?.direction || 'asc'
      });
    }
  };

  const handleDirectionToggle = () => {
    if (!sortOptions) return;

    onChange({
      ...sortOptions,
      direction: sortOptions.direction === 'asc' ? 'desc' : 'asc'
    });
  };

  const getSortIcon = () => {
    if (!sortOptions) return <ArrowUpDown className="h-4 w-4" />;
    return sortOptions.direction === 'asc'
      ? <ArrowUp className="h-4 w-4" />
      : <ArrowDown className="h-4 w-4" />;
  };

  return (
    <div className="flex items-end gap-2">
      <div className="flex-1 space-y-1">
        <Label htmlFor="sortField" className="text-sm font-medium">
          Sort by
        </Label>
        <Select
          value={sortOptions?.field || ''}
          onValueChange={handleFieldChange}
        >
          <SelectTrigger id="sortField">
            <SelectValue placeholder="None" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="">None</SelectItem>
            {SORT_FIELDS.map(({ value, label }) => (
              <SelectItem key={value} value={value}>
                {label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <Button
        variant="outline"
        size="icon"
        onClick={handleDirectionToggle}
        disabled={!sortOptions}
        aria-label={`Sort direction: ${sortOptions?.direction === 'asc' ? 'Ascending' : 'Descending'}`}
        title={sortOptions ? `Currently: ${sortOptions.direction === 'asc' ? 'Ascending' : 'Descending'}` : 'Select a sort field first'}
      >
        {getSortIcon()}
      </Button>
    </div>
  );
}
