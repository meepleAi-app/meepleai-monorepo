'use client';

import { useState } from 'react';

import { AlertTriangle, Eye, EyeOff } from 'lucide-react';

import { Input } from '@/components/ui/primitives/input';
import { cn } from '@/lib/utils';

interface SecretEntryInputProps {
  entryKey: string;
  maskedValue: string;
  hasValue: boolean;
  isPlaceholder: boolean;
  onChange: (key: string, value: string) => void;
  isDirty: boolean;
}

export function SecretEntryInput({
  entryKey,
  maskedValue,
  hasValue,
  isPlaceholder,
  onChange,
  isDirty,
}: SecretEntryInputProps) {
  const [revealed, setRevealed] = useState(false);
  const [editValue, setEditValue] = useState('');
  const [isEditing, setIsEditing] = useState(false);

  const displayValue = isEditing ? editValue : maskedValue;

  const handleFocus = () => {
    if (!isEditing) {
      setEditValue('');
      setIsEditing(true);
    }
  };

  const handleChange = (val: string) => {
    setEditValue(val);
    onChange(entryKey, val);
  };

  return (
    <div className="flex items-center gap-2">
      <label
        className="min-w-[200px] shrink-0 text-xs font-mono text-muted-foreground truncate"
        title={entryKey}
      >
        {entryKey}
      </label>
      <div className="relative flex-1">
        <Input
          type={revealed || isEditing ? 'text' : 'password'}
          value={displayValue}
          placeholder={hasValue ? '(set)' : '(empty)'}
          onFocus={handleFocus}
          onChange={e => handleChange(e.target.value)}
          className={cn(
            'font-mono text-xs h-8 pr-8',
            isDirty && 'border-amber-400 bg-amber-50/50',
            isPlaceholder && !isDirty && 'border-red-300 bg-red-50/30'
          )}
        />
        <button
          type="button"
          onClick={() => setRevealed(!revealed)}
          className="absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
          tabIndex={-1}
        >
          {revealed ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
        </button>
      </div>
      {isPlaceholder && !isDirty && (
        <span title="Placeholder value">
          <AlertTriangle className="h-4 w-4 shrink-0 text-amber-500" />
        </span>
      )}
    </div>
  );
}
