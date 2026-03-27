/**
 * HouseRulesSection — House rules display and input for the Game Table
 *
 * Renders a list of house rules with source badges, an inline add input,
 * and loading/empty states. Matches the Game Table dark theme styling.
 *
 * US-53: House Rules — Phase 1
 */

'use client';

import React, { useState, useCallback } from 'react';

import { ScrollText, Plus } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { useGameMemory, useAddHouseRule } from '@/hooks/queries/useGameMemory';

// ============================================================================
// Types
// ============================================================================

export interface HouseRulesSectionProps {
  gameId: string;
}

// ============================================================================
// Styling constants (matching GameTableZoneKnowledge)
// ============================================================================

const CARD_ROW = 'bg-[#21262d] rounded-lg p-3 border border-[#30363d]';

const SOURCE_BADGE_STYLES: Record<string, string> = {
  User: 'bg-blue-900/40 text-blue-300 border-blue-700/50',
  Admin: 'bg-amber-900/40 text-amber-300 border-amber-700/50',
  System: 'bg-gray-800/60 text-gray-400 border-gray-600/50',
};

// ============================================================================
// Sub-components
// ============================================================================

function SourceBadge({ source }: { source: string }) {
  const style = SOURCE_BADGE_STYLES[source] ?? SOURCE_BADGE_STYLES.System;
  return (
    <span
      className={`inline-flex items-center rounded-full border px-1.5 py-0.5 text-[10px] font-nunito font-medium leading-none ${style}`}
      data-testid="source-badge"
    >
      {source}
    </span>
  );
}

// ============================================================================
// Component
// ============================================================================

export function HouseRulesSection({ gameId }: HouseRulesSectionProps): React.ReactNode {
  const { data: memory, isLoading } = useGameMemory(gameId);
  const addRule = useAddHouseRule(gameId);
  const [newRule, setNewRule] = useState('');

  const houseRules = memory?.houseRules ?? [];

  const handleAdd = useCallback(() => {
    const trimmed = newRule.trim();
    if (!trimmed) return;
    addRule.mutate(trimmed, {
      onSuccess: () => setNewRule(''),
    });
  }, [newRule, addRule]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLInputElement>) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        handleAdd();
      }
    },
    [handleAdd]
  );

  return (
    <div className={CARD_ROW} data-testid="house-rules-section">
      {/* Header */}
      <div className="flex items-center gap-2 mb-2">
        <ScrollText className="h-4 w-4 text-amber-400" />
        <span className="text-sm font-quicksand font-semibold text-[#e6edf3]">Regole di Casa</span>
        <span
          className="ml-auto text-xs text-[#8b949e] font-nunito"
          data-testid="house-rules-count"
        >
          {isLoading ? '...' : houseRules.length}
        </span>
      </div>

      {/* Content */}
      {isLoading ? (
        <div className="space-y-2" data-testid="house-rules-skeleton">
          {[1, 2].map(i => (
            <div key={i} className="h-6 bg-[#30363d] rounded animate-pulse" />
          ))}
        </div>
      ) : houseRules.length === 0 ? (
        <p className="text-xs text-[#8b949e] font-nunito" data-testid="house-rules-empty">
          Nessuna regola di casa. Aggiungine una!
        </p>
      ) : (
        <ul className="space-y-1.5" data-testid="house-rules-list">
          {houseRules.map((rule, idx) => (
            <li
              key={`${rule.description}-${idx}`}
              className="flex items-start justify-between gap-2 text-sm"
              data-testid="house-rule-item"
            >
              <span className="text-[#e6edf3] font-nunito text-xs leading-relaxed">
                {rule.description}
              </span>
              <SourceBadge source={rule.source} />
            </li>
          ))}
        </ul>
      )}

      {/* Add input */}
      <div className="flex items-center gap-1.5 mt-2">
        <input
          type="text"
          value={newRule}
          onChange={e => setNewRule(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Nuova regola di casa..."
          disabled={addRule.isPending}
          className="flex-1 bg-[#0d1117] border border-[#30363d] rounded-md px-2 py-1 text-xs text-[#e6edf3] font-nunito placeholder:text-[#484f58] focus:outline-none focus:border-amber-500/50 disabled:opacity-50"
          data-testid="house-rule-input"
        />
        <Button
          size="sm"
          variant="ghost"
          onClick={handleAdd}
          disabled={addRule.isPending || !newRule.trim()}
          className="h-7 w-7 p-0 text-amber-400 hover:text-amber-300 hover:bg-[#30363d] shrink-0"
          data-testid="add-house-rule-btn"
        >
          <Plus className="h-3.5 w-3.5" />
        </Button>
      </div>
    </div>
  );
}
