/**
 * Typology Selector - Agent typology selection component
 * Issue #3: Agent Config Selectors
 *
 * Dropdown for selecting agent typology during wizard setup.
 * Fetches approved typologies from backend API.
 */

'use client';

import { Check, ChevronDown, Loader2, Sparkles } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { useApprovedTypologies } from '@/hooks/queries/useAgentTypologies';
import type { Typology } from '@/lib/api/schemas/agent-typologies.schemas';

export interface TypologySelectorProps {
  /** Currently selected typology ID */
  value?: string;
  /** Callback when typology changes */
  onChange: (typologyId: string) => void;
  /** Whether the selector is disabled */
  disabled?: boolean;
  /** Optional placeholder text */
  placeholder?: string;
  /** Optional className for styling */
  className?: string;
}

/**
 * Get icon for typology based on its name/strategy
 */
function getTypologyIcon(typology: Typology): string {
  const name = typology.name.toLowerCase();
  if (name.includes('rules') || name.includes('clarif')) return '📖';
  if (name.includes('strateg') || name.includes('coach')) return '🎯';
  if (name.includes('setup') || name.includes('assist')) return '⚙️';
  if (name.includes('faq') || name.includes('q&a')) return '❓';
  if (name.includes('educat') || name.includes('learn')) return '🎓';
  return '🤖';
}

export function TypologySelector({
  value,
  onChange,
  disabled = false,
  placeholder = 'Select agent type...',
  className,
}: TypologySelectorProps) {
  const { data: typologies, isLoading, error } = useApprovedTypologies();

  // Find selected typology
  const selectedTypology = typologies?.find(t => t.id === value);

  if (error) {
    return (
      <div className="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
        <p className="text-sm text-red-700 dark:text-red-300">
          Failed to load typologies. Please try again.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <label className="block text-sm font-medium">
        Agent Type
        <span className="ml-1 text-red-500">*</span>
      </label>

      <Select
        value={value}
        onValueChange={onChange}
        disabled={disabled || isLoading}
      >
        <SelectTrigger className={className} aria-label="Select agent typology">
          {isLoading ? (
            <div className="flex items-center gap-2">
              <Loader2 className="h-4 w-4 animate-spin" />
              <span className="text-muted-foreground">Loading typologies...</span>
            </div>
          ) : selectedTypology ? (
            <div className="flex items-center gap-2">
              <span>{getTypologyIcon(selectedTypology)}</span>
              <span>{selectedTypology.name}</span>
            </div>
          ) : (
            <SelectValue placeholder={placeholder} />
          )}
        </SelectTrigger>

        <SelectContent>
          {typologies && typologies.length > 0 ? (
            typologies.map((typology) => {
              const isSelected = value === typology.id;
              return (
                <SelectItem
                  key={typology.id}
                  value={typology.id}
                  className="cursor-pointer"
                >
                  <div className="flex items-center justify-between gap-3 w-full">
                    <div className="flex items-center gap-2 flex-1 min-w-0">
                      <span className="text-base shrink-0">{getTypologyIcon(typology)}</span>
                      <div className="flex flex-col min-w-0">
                        <span className="font-medium truncate">
                          {typology.name}
                        </span>
                        {typology.description && (
                          <span className="text-xs text-muted-foreground truncate">
                            {typology.description}
                          </span>
                        )}
                      </div>
                    </div>
                    <div className="flex items-center gap-1.5 shrink-0">
                      {typology.defaultStrategyName && (
                        <span className="px-1.5 py-0.5 rounded text-[10px] bg-purple-100 dark:bg-purple-500/20 text-purple-700 dark:text-purple-300">
                          <Sparkles className="h-2.5 w-2.5 inline mr-0.5" />
                          {typology.defaultStrategyName}
                        </span>
                      )}
                      {isSelected && (
                        <Check className="h-4 w-4 text-green-600 dark:text-green-400" />
                      )}
                    </div>
                  </div>
                </SelectItem>
              );
            })
          ) : (
            <div className="p-3 text-sm text-muted-foreground text-center">
              No typologies available
            </div>
          )}
        </SelectContent>
      </Select>

      {selectedTypology?.description && (
        <p className="text-xs text-muted-foreground">
          {selectedTypology.description}
        </p>
      )}
    </div>
  );
}
