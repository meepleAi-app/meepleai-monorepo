'use client';

import React, { useState, useCallback } from 'react';

import { Dices, History, RotateCcw } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

import { WidgetCard } from './WidgetCard';

interface RollResult {
  id: string;
  formula: string;
  result: number;
  breakdown: number[];
  timestamp: Date;
}

interface RandomGeneratorWidgetProps {
  isEnabled: boolean;
  onToggle?: (enabled: boolean) => void;
  onStateChange?: (stateJson: string) => void;
  'data-testid'?: string;
}

const DICE_OPTIONS = [
  { label: 'D4', sides: 4 },
  { label: 'D6', sides: 6 },
  { label: 'D8', sides: 8 },
  { label: 'D10', sides: 10 },
  { label: 'D12', sides: 12 },
  { label: 'D20', sides: 20 },
  { label: 'D100', sides: 100 },
] as const;

function rollDie(sides: number): number {
  return Math.floor(Math.random() * sides) + 1;
}

/**
 * RandomGeneratorWidget — dice roller, multi-dice, random pick.
 * Issue #5149 — Epic B6.
 */
export function RandomGeneratorWidget({
  isEnabled,
  onToggle,
  onStateChange,
  'data-testid': testId,
}: RandomGeneratorWidgetProps) {
  const [selectedDie, setSelectedDie] = useState<string>('6');
  const [diceCount, setDiceCount] = useState(1);
  const [history, setHistory] = useState<RollResult[]>([]);
  const [lastResult, setLastResult] = useState<RollResult | null>(null);
  const [showHistory, setShowHistory] = useState(false);

  const roll = useCallback(() => {
    const sides = parseInt(selectedDie, 10);
    const breakdown = Array.from({ length: diceCount }, () => rollDie(sides));
    const total = breakdown.reduce((a, b) => a + b, 0);
    const formula = `${diceCount}d${sides}`;

    const result: RollResult = {
      id: crypto.randomUUID(),
      formula,
      result: total,
      breakdown,
      timestamp: new Date(),
    };

    setLastResult(result);
    setHistory(prev => [result, ...prev].slice(0, 20));

    const stateJson = JSON.stringify({
      lastRoll: result,
      history: [result, ...history].slice(0, 5),
    });
    onStateChange?.(stateJson);
  }, [selectedDie, diceCount, history, onStateChange]);

  const clearHistory = useCallback(() => {
    setHistory([]);
    setLastResult(null);
  }, []);

  return (
    <WidgetCard
      title="Random Generator"
      icon={<Dices className="h-4 w-4 text-indigo-500" />}
      isEnabled={isEnabled}
      onToggle={onToggle}
      data-testid={testId ?? 'random-generator-widget'}
    >
      <div className="space-y-4">
        {/* Controls */}
        <div className="flex items-center gap-2">
          <Select value={selectedDie} onValueChange={setSelectedDie}>
            <SelectTrigger className="w-24" aria-label="Select die type">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {DICE_OPTIONS.map(d => (
                <SelectItem key={d.sides} value={String(d.sides)}>
                  {d.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <div className="flex items-center gap-1">
            <Button
              variant="outline"
              size="icon"
              className="h-8 w-8"
              onClick={() => setDiceCount(c => Math.max(1, c - 1))}
              aria-label="Decrease dice count"
            >
              −
            </Button>
            <span className="w-8 text-center text-sm font-medium" aria-label="Dice count">
              {diceCount}
            </span>
            <Button
              variant="outline"
              size="icon"
              className="h-8 w-8"
              onClick={() => setDiceCount(c => Math.min(10, c + 1))}
              aria-label="Increase dice count"
            >
              +
            </Button>
          </div>

          <Button onClick={roll} className="flex-1">
            <Dices className="mr-1 h-4 w-4" />
            Roll {diceCount}d{selectedDie}
          </Button>
        </div>

        {/* Last result */}
        {lastResult && (
          <div className="rounded-lg bg-muted p-3 text-center" data-testid="roll-result">
            <div className="text-3xl font-bold tabular-nums">{lastResult.result}</div>
            <div className="mt-1 flex flex-wrap justify-center gap-1">
              {lastResult.breakdown.map((v, i) => (
                // eslint-disable-next-line react/no-array-index-key
                <Badge key={i} variant="secondary" className="text-xs">
                  {v}
                </Badge>
              ))}
            </div>
            <div className="mt-1 text-xs text-muted-foreground">{lastResult.formula}</div>
          </div>
        )}

        {/* History toggle */}
        {history.length > 0 && (
          <div>
            <div className="flex items-center justify-between">
              <Button
                variant="ghost"
                size="sm"
                className="h-7 text-xs"
                onClick={() => setShowHistory(v => !v)}
                aria-label={showHistory ? 'Hide roll history' : 'Show roll history'}
              >
                <History className="mr-1 h-3 w-3" />
                History ({history.length})
              </Button>
              <Button
                variant="ghost"
                size="icon"
                className="h-7 w-7"
                onClick={clearHistory}
                aria-label="Clear roll history"
              >
                <RotateCcw className="h-3 w-3" />
              </Button>
            </div>

            {showHistory && (
              <ScrollArea className="mt-1 h-32">
                <div className="space-y-1 pr-3">
                  {history.map(r => (
                    <div
                      key={r.id}
                      className="flex items-center justify-between rounded px-2 py-1 text-xs hover:bg-muted"
                    >
                      <span className="text-muted-foreground">{r.formula}</span>
                      <span className="font-medium">{r.result}</span>
                    </div>
                  ))}
                </div>
              </ScrollArea>
            )}
          </div>
        )}
      </div>
    </WidgetCard>
  );
}
