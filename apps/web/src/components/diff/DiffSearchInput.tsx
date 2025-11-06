import React, { useState, useCallback, useRef, useEffect } from 'react';

export interface DiffSearchInputProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  matchCount?: number;
}

/**
 * Search input for filtering diff content
 * Debounced to avoid excessive re-renders
 */
export function DiffSearchInput({
  value,
  onChange,
  placeholder = 'Search in diff...',
  matchCount
}: DiffSearchInputProps) {
  const [localValue, setLocalValue] = useState(value);
  const debounceTimerRef = useRef<NodeJS.Timeout | null>(null);

  const handleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
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
  }, [onChange]);

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
    <div className="diff-search-input">
      <input
        type="text"
        value={localValue}
        onChange={handleChange}
        placeholder={placeholder}
        className="diff-search-field"
        aria-label="Search in diff"
      />
      {localValue && (
        <button
          onClick={handleClear}
          className="diff-search-clear"
          aria-label="Clear search"
          title="Clear search"
        >
          ✕
        </button>
      )}
      {matchCount !== undefined && matchCount > 0 && (
        <span className="diff-search-count" aria-live="polite">
          {matchCount} {matchCount === 1 ? 'match' : 'matches'}
        </span>
      )}
    </div>
  );
}
