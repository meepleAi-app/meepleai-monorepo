'use client';

import React, { useState, useCallback } from 'react';

import { Dices, Plus, Minus, History, X, Loader2 } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  HoverCard,
  HoverCardContent,
  HoverCardTrigger,
} from '@/components/ui/overlays/hover-card';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';

import { DiceRoll, DiceType, DICE_TYPES } from './types';

interface DiceRollerProps {
  sessionId: string;
  participantId: string;
  participantName: string;
  onRoll: (formula: string, label?: string) => Promise<DiceRoll>;
  rollHistory: DiceRoll[];
  disabled?: boolean;
}

// Dice icon components with distinct shapes
const DiceIcon = ({ sides, size = 24 }: { sides: number; size?: number }) => {
  const color = getDiceColor(sides);

  // Simple polygon-based dice shapes
  switch (sides) {
    case 4:
      return (
        <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
          <polygon points="12,2 22,20 2,20" fill={color} stroke="currentColor" strokeWidth="1.5" />
          <text x="12" y="15" textAnchor="middle" fontSize="8" fill="white" fontWeight="bold">4</text>
        </svg>
      );
    case 6:
      return (
        <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
          <rect x="3" y="3" width="18" height="18" rx="2" fill={color} stroke="currentColor" strokeWidth="1.5" />
          <text x="12" y="16" textAnchor="middle" fontSize="10" fill="white" fontWeight="bold">6</text>
        </svg>
      );
    case 8:
      return (
        <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
          <polygon points="12,1 22,7 22,17 12,23 2,17 2,7" fill={color} stroke="currentColor" strokeWidth="1.5" />
          <text x="12" y="16" textAnchor="middle" fontSize="9" fill="white" fontWeight="bold">8</text>
        </svg>
      );
    case 10:
      return (
        <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
          <polygon points="12,1 21,8 18,22 6,22 3,8" fill={color} stroke="currentColor" strokeWidth="1.5" />
          <text x="12" y="15" textAnchor="middle" fontSize="8" fill="white" fontWeight="bold">10</text>
        </svg>
      );
    case 12:
      return (
        <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
          <polygon points="12,1 20,5 22,13 17,21 7,21 2,13 4,5" fill={color} stroke="currentColor" strokeWidth="1.5" />
          <text x="12" y="15" textAnchor="middle" fontSize="8" fill="white" fontWeight="bold">12</text>
        </svg>
      );
    case 20:
      return (
        <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
          <polygon points="12,1 22,8 19,21 5,21 2,8" fill={color} stroke="currentColor" strokeWidth="1.5" />
          <text x="12" y="15" textAnchor="middle" fontSize="8" fill="white" fontWeight="bold">20</text>
        </svg>
      );
    case 100:
      return (
        <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
          <circle cx="12" cy="12" r="10" fill={color} stroke="currentColor" strokeWidth="1.5" />
          <text x="12" y="15" textAnchor="middle" fontSize="7" fill="white" fontWeight="bold">%</text>
        </svg>
      );
    default:
      return <Dices size={size} className="text-amber-600" />;
  }
};

function getDiceColor(sides: number): string {
  const colors: Record<number, string> = {
    4: '#ef4444',   // red
    6: '#f97316',   // orange
    8: '#eab308',   // yellow
    10: '#22c55e',  // green
    12: '#0ea5e9',  // sky
    20: '#8b5cf6',  // violet
    100: '#ec4899', // pink
  };
  return colors[sides] || '#f59e0b';
}

export function DiceRoller({
  sessionId,
  participantId,
  participantName,
  onRoll,
  rollHistory,
  disabled = false,
}: DiceRollerProps) {
  const [selectedDice, setSelectedDice] = useState<DiceType>('d20');
  const [diceCount, setDiceCount] = useState(1);
  const [modifier, setModifier] = useState(0);
  const [customFormula, setCustomFormula] = useState('');
  const [label, setLabel] = useState('');
  const [isRolling, setIsRolling] = useState(false);
  const [lastRoll, setLastRoll] = useState<DiceRoll | null>(null);
  const [showHistory, setShowHistory] = useState(false);

  const getFormula = useCallback(() => {
    if (customFormula.trim()) {
      return customFormula.trim().toUpperCase();
    }
    const sides = DICE_TYPES.find(d => d.type === selectedDice)?.sides || 20;
    let formula = `${diceCount}d${sides}`;
    if (modifier > 0) formula += `+${modifier}`;
    if (modifier < 0) formula += `${modifier}`;
    return formula;
  }, [customFormula, selectedDice, diceCount, modifier]);

  const handleRoll = useCallback(async () => {
    if (disabled || isRolling) return;

    setIsRolling(true);
    try {
      const formula = getFormula();
      const result = await onRoll(formula, label.trim() || undefined);
      setLastRoll(result);
      setLabel(''); // Clear label after roll
    } catch {
      // Error handling - could show toast
      console.error('Roll failed');
    } finally {
      setIsRolling(false);
    }
  }, [disabled, isRolling, getFormula, label, onRoll]);

  const handleQuickRoll = useCallback(async (type: DiceType) => {
    if (disabled || isRolling) return;

    setIsRolling(true);
    try {
      const sides = DICE_TYPES.find(d => d.type === type)?.sides || 20;
      const result = await onRoll(`1d${sides}`);
      setLastRoll(result);
    } catch {
      console.error('Roll failed');
    } finally {
      setIsRolling(false);
    }
  }, [disabled, isRolling, onRoll]);

  const incrementCount = () => setDiceCount(prev => Math.min(prev + 1, 100));
  const decrementCount = () => setDiceCount(prev => Math.max(prev - 1, 1));
  const incrementModifier = () => setModifier(prev => Math.min(prev + 1, 100));
  const decrementModifier = () => setModifier(prev => Math.max(prev - 1, -100));

  return (
    <Card className="border-amber-900/20 bg-gradient-to-br from-amber-50/50 via-orange-50/50 to-amber-100/50 dark:from-slate-900/50 dark:via-slate-800/50 dark:to-slate-900/50">
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2 text-lg">
            <Dices className="h-5 w-5 text-amber-600" />
            Dice Roller
          </CardTitle>
          <HoverCard open={showHistory} onOpenChange={setShowHistory}>
            <HoverCardTrigger asChild>
              <Button variant="ghost" size="icon" className="h-8 w-8" onClick={() => setShowHistory(!showHistory)}>
                <History className="h-4 w-4" />
              </Button>
            </HoverCardTrigger>
            <HoverCardContent className="w-80 p-0" align="end">
              <div className="p-3 border-b border-amber-900/10">
                <h4 className="font-semibold text-sm">Roll History</h4>
              </div>
              <ScrollArea className="h-64">
                {rollHistory.length === 0 ? (
                  <div className="p-4 text-center text-sm text-slate-500">
                    No rolls yet
                  </div>
                ) : (
                  <div className="p-2 space-y-2">
                    {rollHistory.map((roll) => (
                      <div
                        key={roll.id}
                        className="p-2 rounded-lg bg-white/50 dark:bg-slate-800/50 border border-amber-900/10"
                      >
                        <div className="flex items-center justify-between">
                          <span className="font-medium text-sm">{roll.participantName}</span>
                          <Badge variant="outline" className="text-xs">
                            {roll.formula}
                          </Badge>
                        </div>
                        <div className="flex items-center gap-2 mt-1">
                          <span className="text-xs text-slate-500">
                            [{roll.rolls.join(', ')}]
                            {roll.modifier !== 0 && (
                              <span className={roll.modifier > 0 ? 'text-emerald-600' : 'text-red-600'}>
                                {roll.modifier > 0 ? ` +${roll.modifier}` : ` ${roll.modifier}`}
                              </span>
                            )}
                          </span>
                          <span className="font-bold text-amber-700 dark:text-amber-400">
                            = {roll.total}
                          </span>
                        </div>
                        {roll.label && (
                          <div className="text-xs text-slate-500 mt-1 italic">
                            {roll.label}
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </ScrollArea>
            </HoverCardContent>
          </HoverCard>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        {/* Quick Roll Buttons */}
        <div className="flex flex-wrap gap-2 justify-center">
          {DICE_TYPES.map(({ type, sides, label: diceLabel }) => (
            <Button
              key={type}
              variant={selectedDice === type ? 'default' : 'outline'}
              size="sm"
              className={`h-12 w-12 p-0 transition-all ${
                selectedDice === type
                  ? 'ring-2 ring-amber-500 ring-offset-2'
                  : 'hover:bg-amber-50 dark:hover:bg-slate-800'
              }`}
              onClick={() => {
                setSelectedDice(type);
                setCustomFormula('');
              }}
              onDoubleClick={() => handleQuickRoll(type)}
              disabled={disabled || isRolling}
              title={`${diceLabel} (double-click to quick roll)`}
            >
              <DiceIcon sides={sides} size={28} />
            </Button>
          ))}
        </div>

        {/* Count and Modifier Controls */}
        <div className="grid grid-cols-2 gap-3">
          {/* Dice Count */}
          <div className="space-y-1">
            <label className="text-xs font-medium text-slate-600 dark:text-slate-400">
              Count
            </label>
            <div className="flex items-center gap-1">
              <Button
                variant="outline"
                size="icon"
                className="h-8 w-8"
                onClick={decrementCount}
                disabled={diceCount <= 1 || disabled}
              >
                <Minus className="h-3 w-3" />
              </Button>
              <Input
                type="number"
                value={diceCount}
                onChange={(e) => setDiceCount(Math.max(1, Math.min(100, parseInt(e.target.value) || 1)))}
                className="h-8 w-14 text-center font-mono"
                min={1}
                max={100}
                disabled={disabled}
              />
              <Button
                variant="outline"
                size="icon"
                className="h-8 w-8"
                onClick={incrementCount}
                disabled={diceCount >= 100 || disabled}
              >
                <Plus className="h-3 w-3" />
              </Button>
            </div>
          </div>

          {/* Modifier */}
          <div className="space-y-1">
            <label className="text-xs font-medium text-slate-600 dark:text-slate-400">
              Modifier
            </label>
            <div className="flex items-center gap-1">
              <Button
                variant="outline"
                size="icon"
                className="h-8 w-8"
                onClick={decrementModifier}
                disabled={modifier <= -100 || disabled}
              >
                <Minus className="h-3 w-3" />
              </Button>
              <Input
                type="number"
                value={modifier}
                onChange={(e) => setModifier(Math.max(-100, Math.min(100, parseInt(e.target.value) || 0)))}
                className="h-8 w-14 text-center font-mono"
                min={-100}
                max={100}
                disabled={disabled}
              />
              <Button
                variant="outline"
                size="icon"
                className="h-8 w-8"
                onClick={incrementModifier}
                disabled={modifier >= 100 || disabled}
              >
                <Plus className="h-3 w-3" />
              </Button>
            </div>
          </div>
        </div>

        {/* Custom Formula Input */}
        <div className="space-y-1">
          <label className="text-xs font-medium text-slate-600 dark:text-slate-400">
            Custom Formula
          </label>
          <div className="flex items-center gap-2">
            <Input
              placeholder="e.g., 2d6+3"
              value={customFormula}
              onChange={(e) => setCustomFormula(e.target.value.toUpperCase())}
              className="font-mono"
              disabled={disabled}
            />
            {customFormula && (
              <Button
                variant="ghost"
                size="icon"
                className="h-8 w-8 shrink-0"
                onClick={() => setCustomFormula('')}
              >
                <X className="h-3 w-3" />
              </Button>
            )}
          </div>
        </div>

        {/* Label Input */}
        <div className="space-y-1">
          <label className="text-xs font-medium text-slate-600 dark:text-slate-400">
            Label (optional)
          </label>
          <Input
            placeholder="e.g., Attack roll"
            value={label}
            onChange={(e) => setLabel(e.target.value)}
            maxLength={100}
            disabled={disabled}
          />
        </div>

        {/* Roll Button */}
        <Button
          onClick={handleRoll}
          disabled={disabled || isRolling}
          className="w-full h-12 text-lg font-bold bg-gradient-to-r from-amber-600 to-orange-600 hover:from-amber-700 hover:to-orange-700 text-white shadow-lg"
        >
          {isRolling ? (
            <Loader2 className="h-5 w-5 animate-spin mr-2" />
          ) : (
            <Dices className="h-5 w-5 mr-2" />
          )}
          Roll {getFormula()}
        </Button>

        {/* Last Roll Result */}
        <AnimatePresence mode="wait">
          {lastRoll && (
            <motion.div
              initial={{ opacity: 0, y: 20, scale: 0.95 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, y: -20, scale: 0.95 }}
              transition={{ type: 'spring', stiffness: 500, damping: 30 }}
              className="p-4 rounded-xl bg-gradient-to-br from-amber-100 to-orange-100 dark:from-slate-800 dark:to-slate-700 border border-amber-900/20 shadow-inner"
            >
              <div className="text-center">
                <div className="text-sm text-slate-600 dark:text-slate-400 mb-1">
                  {lastRoll.participantName} rolled {lastRoll.formula}
                  {lastRoll.label && <span className="italic"> - {lastRoll.label}</span>}
                </div>
                <div className="flex items-center justify-center gap-2 mb-2">
                  {lastRoll.rolls.map((roll, idx) => (
                    <motion.span
                      key={idx}
                      initial={{ rotateY: 180, scale: 0 }}
                      animate={{ rotateY: 0, scale: 1 }}
                      transition={{ delay: idx * 0.1, type: 'spring' }}
                      className="inline-flex items-center justify-center h-10 w-10 rounded-lg bg-white dark:bg-slate-900 border-2 border-amber-600/30 font-mono font-bold text-lg shadow-sm"
                    >
                      {roll}
                    </motion.span>
                  ))}
                  {lastRoll.modifier !== 0 && (
                    <span className={`font-bold ${lastRoll.modifier > 0 ? 'text-emerald-600' : 'text-red-600'}`}>
                      {lastRoll.modifier > 0 ? `+${lastRoll.modifier}` : lastRoll.modifier}
                    </span>
                  )}
                </div>
                <motion.div
                  initial={{ scale: 0.5 }}
                  animate={{ scale: 1 }}
                  transition={{ delay: lastRoll.rolls.length * 0.1, type: 'spring', stiffness: 300 }}
                  className="text-4xl font-black text-amber-700 dark:text-amber-400"
                >
                  {lastRoll.total}
                </motion.div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </CardContent>
    </Card>
  );
}
