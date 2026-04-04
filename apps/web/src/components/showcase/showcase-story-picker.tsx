'use client';

/**
 * ShowcaseStoryPicker — Preset dropdown selector
 *
 * Shows named presets (e.g. "Default", "WithRating", "FullFeatured")
 * and applies their props when selected.
 */

import { BookOpen } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';

interface ShowcaseStoryPickerProps {
  presets: Record<string, { label: string }>;
  activePreset: string;
  onChange: (presetKey: string) => void;
}

export function ShowcaseStoryPicker({ presets, activePreset, onChange }: ShowcaseStoryPickerProps) {
  const keys = Object.keys(presets);

  if (keys.length === 0) return null;

  return (
    <div className="flex items-center gap-1.5">
      <BookOpen className="h-3.5 w-3.5 text-muted-foreground" />
      <Select value={activePreset} onValueChange={onChange}>
        <SelectTrigger className="h-7 w-[150px] text-xs border-border/60">
          <SelectValue placeholder="Preset…" />
        </SelectTrigger>
        <SelectContent align="end">
          {keys.map(key => (
            <SelectItem key={key} value={key} className="text-xs">
              {presets[key].label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
