import React, { useState, useCallback } from 'react';

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

  const handleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const newValue = e.target.value;
    setLocalValue(newValue);

    // Debounce onChange callback (300ms)
    const timeoutId = setTimeout(() => {
      onChange(newValue);
    }, 300);

    return () => clearTimeout(timeoutId);
  }, [onChange]);

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
