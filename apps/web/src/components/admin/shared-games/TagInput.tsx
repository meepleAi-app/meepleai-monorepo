/**
 * TagInput Component - Issue #2391 Sprint 1
 *
 * Input component for managing free-form tags on Homerule documents.
 * Features:
 * - Tag normalization (lowercase, spaces to hyphens)
 * - Chip display with remove buttons
 * - Max 10 tags limit
 * - Keyboard support (Enter to add, Backspace to remove)
 */

'use client';

import { useState, KeyboardEvent } from 'react';

import { X } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

export interface TagInputProps {
  tags: string[];
  onChange: (tags: string[]) => void;
  maxTags?: number;
  disabled?: boolean;
  placeholder?: string;
}

/**
 * Normalizes a tag: lowercase, spaces to hyphens, remove special chars
 */
function normalizeTag(tag: string): string {
  return tag
    .trim()
    .toLowerCase()
    .replace(/\s+/g, '-')
    .replace(/[^a-z0-9-]/g, '');
}

export function TagInput({
  tags,
  onChange,
  maxTags = 10,
  disabled = false,
  placeholder = 'Aggiungi tag (es. speed-mode, 2-players)',
}: TagInputProps) {
  const [inputValue, setInputValue] = useState('');
  const [error, setError] = useState<string | null>(null);

  const addTag = () => {
    setError(null);

    if (!inputValue.trim()) {
      return;
    }

    const normalized = normalizeTag(inputValue);

    if (!normalized) {
      setError('Il tag contiene solo caratteri non validi');
      return;
    }

    if (normalized.length > 50) {
      setError('Il tag non può superare 50 caratteri');
      return;
    }

    if (tags.length >= maxTags) {
      setError(`Non puoi aggiungere più di ${maxTags} tag`);
      return;
    }

    if (tags.includes(normalized)) {
      setError('Questo tag è già stato aggiunto');
      return;
    }

    onChange([...tags, normalized]);
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
      // Remove last tag if input is empty
      removeTag(tags[tags.length - 1]);
    }
  };

  return (
    <div className="space-y-2">
      <div className="flex gap-2">
        <div className="flex-1">
          <Label htmlFor="tag-input">Tag (opzionali)</Label>
          <div className="flex gap-2 mt-1.5">
            <Input
              id="tag-input"
              type="text"
              value={inputValue}
              onChange={e => {
                setInputValue(e.target.value);
                setError(null);
              }}
              onKeyDown={handleKeyDown}
              placeholder={placeholder}
              disabled={disabled || tags.length >= maxTags}
              className="flex-1"
            />
            <Button
              type="button"
              onClick={addTag}
              disabled={disabled || tags.length >= maxTags || !inputValue.trim()}
              variant="outline"
            >
              Aggiungi
            </Button>
          </div>
          {error && <p className="text-sm text-destructive mt-1">{error}</p>}
          <p className="text-sm text-muted-foreground mt-1">
            {tags.length}/{maxTags} tag utilizzati
          </p>
        </div>
      </div>

      {/* Tag chips */}
      {tags.length > 0 && (
        <div className="flex flex-wrap gap-2 pt-2">
          {tags.map(tag => (
            <Badge key={tag} variant="secondary" className="gap-1.5 pr-1.5">
              <span className="font-mono text-xs">{tag}</span>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="h-4 w-4 p-0 hover:bg-transparent"
                onClick={() => removeTag(tag)}
                disabled={disabled}
                aria-label={`Rimuovi tag ${tag}`}
              >
                <X className="h-3 w-3" />
              </Button>
            </Badge>
          ))}
        </div>
      )}
    </div>
  );
}
