import React, { useState, useCallback, useRef, useEffect } from 'react';

import { Search, X } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';

export interface DiffSearchInputProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  matchCount?: number;
}

/**
 * Search input for filtering diff content
 * Debounced to avoid excessive re-renders
 * Migrated to shadcn UI components
 */
export function DiffSearchInput({
  value,
  onChange,
  placeholder = 'Search in diff...',
  matchCount,
}: DiffSearchInputProps) {
  const [localValue, setLocalValue] = useState(value);
  const debounceTimerRef = useRef<NodeJS.Timeout | null>(null);

  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const newValue = e.target.value;
      setLocalValue(newValue);

      // Clear previous timeout if it exists
      if (debounceTimerRef.current) {
        clearTimeout(debounceTimerRef.current);
      }

      // Debounce onChange callback (300ms)
      debounceTimerRef.current = setTimeout(() => {
        onChange(newValue);
      }, 300);
    },
    [onChange]
  );

  // Cleanup timeout on unmount
  useEffect(() => {
    return () => {
      if (debounceTimerRef.current) {
        clearTimeout(debounceTimerRef.current);
      }
    };
  }, []);

  const handleClear = () => {
    setLocalValue('');
    onChange('');
  };

  return (
    <div className="diff-search-input flex items-center gap-2">
      <div className="relative flex-1">
        <Search className="absolute left-2 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          type="text"
          value={localValue}
          onChange={handleChange}
          placeholder={placeholder}
          className="pl-8 pr-8"
          aria-label="Search in diff"
        />
        {localValue && (
          <Button
            variant="ghost"
            size="icon"
            onClick={handleClear}
            className="absolute right-0 top-1/2 h-8 w-8 -translate-y-1/2"
            aria-label="Clear search"
            title="Clear search"
          >
            <X className="h-4 w-4" />
          </Button>
        )}
      </div>
      {matchCount !== undefined && matchCount > 0 && (
        <span
          className="diff-search-count text-sm text-muted-foreground whitespace-nowrap"
          aria-live="polite"
        >
          {matchCount} {matchCount === 1 ? 'match' : 'matches'}
        </span>
      )}
    </div>
  );
}
