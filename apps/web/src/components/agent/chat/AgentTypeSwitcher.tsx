/**
 * Agent Type Switcher - Dropdown to switch typology during active chat
 * Issue #3249: [FRONT-013] Agent Type Switcher & Dynamic Typology
 *
 * Features:
 * - Dropdown shows all available (Approved) typologies
 * - Current typology highlighted with checkmark
 * - Click switches typology (PATCH request)
 * - Loading state during switch (spinner + disabled)
 * - Does NOT lose chat history on switch
 */

'use client';

import { Check, ChevronDown, Loader2, Sparkles } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
} from '@/components/ui/overlays/select';
import type { Typology } from '@/lib/api/schemas/agent-typologies.schemas';

// ========== Types ==========

export interface AgentTypeSwitcherProps {
  /** Current active typology */
  currentTypology: Typology;
  /** List of available typologies to switch to */
  availableTypologies: Typology[];
  /** Callback when user selects a new typology */
  onSwitch: (typologyId: string) => void;
  /** Whether switch operation is in progress */
  isLoading?: boolean;
  /** Whether the component is disabled */
  disabled?: boolean;
}

// ========== Helper Functions ==========

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

// ========== Component ==========

export function AgentTypeSwitcher({
  currentTypology,
  availableTypologies,
  onSwitch,
  isLoading = false,
  disabled = false,
}: AgentTypeSwitcherProps) {
  const handleValueChange = (typologyId: string) => {
    // Don't switch if selecting the same typology
    if (typologyId === currentTypology.id) return;
    onSwitch(typologyId);
  };

  return (
    <Select
      value={currentTypology.id}
      onValueChange={handleValueChange}
      disabled={disabled || isLoading}
    >
      <SelectTrigger
        className="w-auto min-w-[140px] max-w-[200px] h-8 px-2 gap-1
                   bg-slate-900/50 border-slate-700 hover:border-slate-600
                   text-sm focus:ring-cyan-400/30"
        aria-label="Switch agent type"
      >
        {isLoading ? (
          <div className="flex items-center gap-1.5">
            <Loader2 className="h-3.5 w-3.5 animate-spin text-cyan-400" />
            <span className="text-slate-400 text-xs">Switching...</span>
          </div>
        ) : (
          <div className="flex items-center gap-1.5 truncate">
            <span className="text-sm">{getTypologyIcon(currentTypology)}</span>
            <span className="truncate text-white">{currentTypology.name}</span>
          </div>
        )}
        <ChevronDown className="h-3.5 w-3.5 shrink-0 opacity-50" />
      </SelectTrigger>

      <SelectContent
        className="bg-slate-900 border-slate-700 min-w-[220px]"
        align="start"
      >
        {availableTypologies.map((typology) => {
          const isSelected = currentTypology.id === typology.id;
          return (
            <SelectItem
              key={typology.id}
              value={typology.id}
              className={`
                flex items-center justify-between py-2.5 px-3 cursor-pointer
                ${isSelected ? 'bg-cyan-500/10' : 'hover:bg-slate-800'}
              `}
            >
              <div className="flex items-center gap-2 flex-1 min-w-0">
                <span className="text-base shrink-0">{getTypologyIcon(typology)}</span>
                <div className="flex flex-col min-w-0">
                  <span className="font-medium text-white truncate">
                    {typology.name}
                  </span>
                  {typology.description && (
                    <span className="text-xs text-slate-400 truncate">
                      {typology.description}
                    </span>
                  )}
                </div>
              </div>
              <div className="flex items-center gap-1.5 ml-2 shrink-0">
                {typology.defaultStrategyName && (
                  <span className="px-1.5 py-0.5 rounded text-[10px] bg-purple-500/20 text-purple-300">
                    <Sparkles className="h-2.5 w-2.5 inline mr-0.5" />
                    {typology.defaultStrategyName}
                  </span>
                )}
                {isSelected && (
                  <Check className="h-4 w-4 text-cyan-400" />
                )}
              </div>
            </SelectItem>
          );
        })}
      </SelectContent>
    </Select>
  );
}
