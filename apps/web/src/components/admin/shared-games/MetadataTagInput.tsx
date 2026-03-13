'use client';

import { useState, type KeyboardEvent } from 'react';
import type { JSX } from 'react';

import { X } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

export interface MetadataTagInputProps {
  label: string;
  tags: string[];
  onChange: (tags: string[]) => void;
  maxTags?: number;
  disabled?: boolean;
  placeholder?: string;
  /** Badge color variant */
  colorClass?: string;
  /** Autocomplete suggestions from existing DB values */
  suggestions?: string[];
}

export function MetadataTagInput({
  label,
  tags,
  onChange,
  maxTags = 50,
  disabled = false,
  placeholder = 'Add...',
  colorClass = '',
  suggestions = [],
}: MetadataTagInputProps): JSX.Element {
  const [inputValue, setInputValue] = useState('');
  const [showSuggestions, setShowSuggestions] = useState(false);

  // Filter suggestions: match input, exclude already-added tags
  const filteredSuggestions =
    inputValue.length >= 2
      ? suggestions
          .filter(
            s =>
              s.toLowerCase().includes(inputValue.toLowerCase()) &&
              !tags.some(t => t.toLowerCase() === s.toLowerCase())
          )
          .slice(0, 8)
      : [];

  const addTag = () => {
    const trimmed = inputValue.trim();
    if (!trimmed) return;
    if (tags.length >= maxTags) return;
    // Case-insensitive duplicate check
    if (tags.some(t => t.toLowerCase() === trimmed.toLowerCase())) return;
    onChange([...tags, trimmed]);
    setInputValue('');
  };

  const removeTag = (tagToRemove: string) => {
    onChange(tags.filter(t => t !== tagToRemove));
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      addTag();
    } else if (e.key === 'Backspace' && !inputValue && tags.length > 0) {
      removeTag(tags[tags.length - 1]);
    }
  };

  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      <div className="flex flex-wrap gap-1.5 rounded-lg border border-input bg-background p-2 min-h-[42px] items-center">
        {tags.map(tag => (
          <Badge key={tag} variant="secondary" className={`gap-1 pr-1 ${colorClass}`}>
            <span className="text-xs">{tag}</span>
            <button
              type="button"
              onClick={() => removeTag(tag)}
              disabled={disabled}
              aria-label={`Remove ${tag}`}
              className="ml-0.5 rounded-full p-0.5 hover:bg-destructive/20"
            >
              <X className="h-3 w-3" />
            </button>
          </Badge>
        ))}
        <div className="relative flex-1 min-w-[80px]">
          <Input
            type="text"
            value={inputValue}
            onChange={e => {
              setInputValue(e.target.value);
              setShowSuggestions(true);
            }}
            onKeyDown={handleKeyDown}
            onFocus={() => setShowSuggestions(true)}
            onBlur={() => setTimeout(() => setShowSuggestions(false), 150)}
            placeholder={placeholder}
            disabled={disabled || tags.length >= maxTags}
            className="border-0 p-0 h-7 text-sm shadow-none focus-visible:ring-0"
          />
          {showSuggestions && filteredSuggestions.length > 0 && (
            <ul className="absolute z-10 mt-1 w-56 rounded-md border bg-popover p-1 shadow-md">
              {filteredSuggestions.map(suggestion => (
                <li key={suggestion}>
                  <button
                    type="button"
                    className="w-full rounded-sm px-2 py-1.5 text-sm text-left hover:bg-accent"
                    onMouseDown={() => {
                      onChange([...tags, suggestion]);
                      setInputValue('');
                      setShowSuggestions(false);
                    }}
                  >
                    {suggestion}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}
