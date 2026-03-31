/**
 * TypologySelector — Agent definition selection component
 * Issue #3: Agent Config Selectors
 *
 * Dropdown for selecting an agent definition during wizard setup.
 * Fetches active definitions from the admin endpoint.
 */

'use client';

import { Check, Loader2, Sparkles } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { useApprovedTypologies } from '@/hooks/queries/useAgentTypologies';
import type { AgentDefinitionDto } from '@/lib/api/schemas/agent-definitions.schemas';

export interface TypologySelectorProps {
  value?: string;
  onChange: (agentDefinitionId: string) => void;
  disabled?: boolean;
  placeholder?: string;
  className?: string;
}

function getDefinitionIcon(def: AgentDefinitionDto): string {
  const name = def.name.toLowerCase();
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
  const { data: definitions, isLoading, error } = useApprovedTypologies();

  const selected = definitions?.find(d => d.id === value);

  if (error) {
    return (
      <div className="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
        <p className="text-sm text-red-700 dark:text-red-300">
          Failed to load agent types. Please try again.
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

      <Select value={value} onValueChange={onChange} disabled={disabled || isLoading}>
        <SelectTrigger className={className} aria-label="Select agent typology">
          {isLoading ? (
            <div className="flex items-center gap-2">
              <Loader2 className="h-4 w-4 animate-spin" />
              <span className="text-muted-foreground">Loading types...</span>
            </div>
          ) : selected ? (
            <div className="flex items-center gap-2">
              <span>{getDefinitionIcon(selected)}</span>
              <span>{selected.name}</span>
            </div>
          ) : (
            <SelectValue placeholder={placeholder} />
          )}
        </SelectTrigger>

        <SelectContent>
          {definitions && definitions.length > 0 ? (
            definitions.map(def => (
              <SelectItem key={def.id} value={def.id} className="cursor-pointer">
                <div className="flex items-center justify-between gap-3 w-full">
                  <div className="flex items-center gap-2 flex-1 min-w-0">
                    <span className="text-base shrink-0">{getDefinitionIcon(def)}</span>
                    <div className="flex flex-col min-w-0">
                      <span className="font-medium truncate">{def.name}</span>
                      {def.description && (
                        <span className="text-xs text-muted-foreground truncate">
                          {def.description}
                        </span>
                      )}
                    </div>
                  </div>
                  <div className="flex items-center gap-1.5 shrink-0">
                    {def.strategyName && (
                      <span className="px-1.5 py-0.5 rounded text-[10px] bg-purple-100 dark:bg-purple-500/20 text-purple-700 dark:text-purple-300">
                        <Sparkles className="h-2.5 w-2.5 inline mr-0.5" />
                        {def.strategyName}
                      </span>
                    )}
                    {value === def.id && (
                      <Check className="h-4 w-4 text-green-600 dark:text-green-400" />
                    )}
                  </div>
                </div>
              </SelectItem>
            ))
          ) : (
            <div className="p-3 text-sm text-muted-foreground text-center">
              No agent types available
            </div>
          )}
        </SelectContent>
      </Select>

      {selected?.description && (
        <p className="text-xs text-muted-foreground">{selected.description}</p>
      )}
    </div>
  );
}
