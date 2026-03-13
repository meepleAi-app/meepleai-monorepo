'use client';

import { useState } from 'react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectSeparator,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';

/**
 * Language option with flag emoji and display name.
 */
const LANGUAGE_OPTIONS = [
  { code: 'en', flag: '\uD83C\uDDEC\uD83C\uDDE7', name: 'English' },
  { code: 'it', flag: '\uD83C\uDDEE\uD83C\uDDF9', name: 'Italian' },
  { code: 'de', flag: '\uD83C\uDDE9\uD83C\uDDEA', name: 'German' },
  { code: 'fr', flag: '\uD83C\uDDEB\uD83C\uDDF7', name: 'French' },
  { code: 'es', flag: '\uD83C\uDDEA\uD83C\uDDF8', name: 'Spanish' },
  { code: 'pt', flag: '\uD83C\uDDF5\uD83C\uDDF9', name: 'Portuguese' },
  { code: 'ja', flag: '\uD83C\uDDEF\uD83C\uDDF5', name: 'Japanese' },
  { code: 'zh', flag: '\uD83C\uDDE8\uD83C\uDDF3', name: 'Chinese' },
  { code: 'ko', flag: '\uD83C\uDDF0\uD83C\uDDF7', name: 'Korean' },
  { code: 'ru', flag: '\uD83C\uDDF7\uD83C\uDDFA', name: 'Russian' },
  { code: 'pl', flag: '\uD83C\uDDF5\uD83C\uDDF1', name: 'Polish' },
  { code: 'nl', flag: '\uD83C\uDDF3\uD83C\uDDF1', name: 'Dutch' },
] as const;

const RESET_VALUE = '__reset__';

export interface LanguageOverrideSelectorProps {
  pdfId: string;
  detectedLanguage?: string | null;
  languageConfidence?: number | null;
  currentOverride?: string | null;
  onOverrideChange?: (languageCode: string | null) => void;
}

function formatConfidence(confidence: number | null | undefined): string {
  if (confidence === null || confidence === undefined) return '';
  return ` (${Math.round(confidence * 100)}%)`;
}

/**
 * E5-2: Language override selector for PDF documents.
 * Shows detected language with confidence and allows manual override.
 */
export function LanguageOverrideSelector({
  pdfId,
  detectedLanguage,
  languageConfidence,
  currentOverride,
  onOverrideChange,
}: LanguageOverrideSelectorProps) {
  const [isUpdating, setIsUpdating] = useState(false);

  const effectiveValue = currentOverride ?? '';

  const detectedLabel = detectedLanguage
    ? `Detected: ${detectedLanguage.toUpperCase()}${formatConfidence(languageConfidence)}`
    : 'No language detected';

  const handleValueChange = async (value: string) => {
    const newCode = value === RESET_VALUE ? null : value;
    setIsUpdating(true);

    try {
      const response = await fetch(`/api/v1/pdfs/${pdfId}/language`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ languageCode: newCode }),
      });

      if (response.ok) {
        onOverrideChange?.(newCode);
      }
    } finally {
      setIsUpdating(false);
    }
  };

  return (
    <div className="flex flex-col gap-1.5" data-testid="language-override-selector">
      <span className="text-xs text-muted-foreground" data-testid="detected-language-label">
        {detectedLabel}
      </span>
      <Select value={effectiveValue} onValueChange={handleValueChange} disabled={isUpdating}>
        <SelectTrigger className="w-[200px]" data-testid="language-select-trigger">
          <SelectValue placeholder="Override language..." />
        </SelectTrigger>
        <SelectContent>
          {currentOverride && (
            <>
              <SelectItem value={RESET_VALUE} data-testid="reset-option">
                Reset to detected
              </SelectItem>
              <SelectSeparator />
            </>
          )}
          {LANGUAGE_OPTIONS.map(({ code, flag, name }) => (
            <SelectItem key={code} value={code} data-testid={`language-option-${code}`}>
              {flag} {name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
