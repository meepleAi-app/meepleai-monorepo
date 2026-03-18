/**
 * HouseRulesDisplay
 *
 * AgentMemory — Task 25
 *
 * Shows house rules for the current game during a live session.
 * Supports adding new rules when onAddRule callback is provided.
 */

'use client';

import { useState } from 'react';

import { BookOpen, Gavel, Plus, User } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface HouseRule {
  description: string;
  addedAt: string;
  source: 'UserAdded' | 'DisputeOverride';
}

export interface HouseRulesDisplayProps {
  gameId: string;
  rules: HouseRule[];
  onAddRule?: (description: string) => void;
}

// ─── Source Badge ─────────────────────────────────────────────────────────────

function SourceBadge({ source }: { source: HouseRule['source'] }) {
  if (source === 'DisputeOverride') {
    return (
      <Badge
        variant="outline"
        className="bg-red-50 border-red-200 text-red-700 gap-1 text-xs font-nunito"
      >
        <Gavel className="h-3 w-3" />
        Dispute
      </Badge>
    );
  }

  return (
    <Badge
      variant="outline"
      className="bg-amber-50 border-amber-200 text-amber-700 gap-1 text-xs font-nunito"
    >
      <User className="h-3 w-3" />
      Added
    </Badge>
  );
}

// ─── Component ───────────────────────────────────────────────────────────────

export function HouseRulesDisplay({ gameId, rules, onAddRule }: HouseRulesDisplayProps) {
  const [newRule, setNewRule] = useState('');

  const handleAdd = () => {
    const trimmed = newRule.trim();
    if (!trimmed || !onAddRule) return;
    onAddRule(trimmed);
    setNewRule('');
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleAdd();
    }
  };

  return (
    <div
      className="rounded-xl border border-white/40 bg-white/70 backdrop-blur-md shadow-sm p-4 space-y-3"
      data-testid="house-rules-display"
      data-game-id={gameId}
    >
      {/* Header */}
      <div className="flex items-center gap-2">
        <BookOpen className="h-4 w-4 text-amber-600" />
        <h3 className="font-quicksand font-semibold text-sm text-gray-900">House Rules</h3>
        <span className="text-xs text-gray-500 font-nunito">({rules.length})</span>
      </div>

      {/* Rules list */}
      {rules.length === 0 ? (
        <p className="text-sm text-gray-500 font-nunito italic">No house rules yet</p>
      ) : (
        <ul className="space-y-2" role="list">
          {rules.map((rule, index) => (
            <li
              key={`${rule.description}-${index}`}
              className="flex items-start gap-2 rounded-lg bg-white/50 px-3 py-2"
            >
              <span className="flex-1 text-sm text-gray-800 font-nunito">{rule.description}</span>
              <SourceBadge source={rule.source} />
            </li>
          ))}
        </ul>
      )}

      {/* Add rule input */}
      {onAddRule && (
        <div className="flex gap-2 pt-1">
          <Input
            value={newRule}
            onChange={e => setNewRule(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Add a house rule..."
            className="flex-1 text-sm font-nunito"
            aria-label="New house rule"
          />
          <Button
            size="sm"
            variant="outline"
            onClick={handleAdd}
            disabled={!newRule.trim()}
            className="gap-1"
          >
            <Plus className="h-3 w-3" />
            Add
          </Button>
        </div>
      )}
    </div>
  );
}
