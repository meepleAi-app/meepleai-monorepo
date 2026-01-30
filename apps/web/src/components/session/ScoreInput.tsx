'use client';

import React, { useState, useEffect } from 'react';
import { Plus, Minus, Undo2, Check, Loader2, AlertCircle } from 'lucide-react';
import { Participant, SyncStatus } from './types';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';

interface ScoreInputProps {
  participants: Participant[];
  rounds: number[];
  categories?: string[];
  currentRound?: number;
  onSubmit: (data: {
    participantId: string;
    roundNumber: number | null;
    category: string | null;
    scoreValue: number;
  }) => Promise<void>;
  onUndo?: () => void;
  syncStatus?: SyncStatus;
}

export function ScoreInput({
  participants,
  rounds,
  categories = [],
  currentRound,
  onSubmit,
  onUndo,
  syncStatus = 'idle'
}: ScoreInputProps) {
  const [participantId, setParticipantId] = useState<string>('');
  const [roundNumber, setRoundNumber] = useState<number | null>(currentRound ?? null);
  const [category, setCategory] = useState<string | null>(null);
  const [scoreValue, setScoreValue] = useState<string>('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Auto-select first participant
  useEffect(() => {
    if (participants.length > 0 && !participantId) {
      setParticipantId(participants[0].id);
    }
  }, [participants, participantId]);

  // Update round when currentRound changes
  useEffect(() => {
    if (currentRound !== undefined) {
      setRoundNumber(currentRound);
    }
  }, [currentRound]);

  const adjustScore = (amount: number) => {
    const current = parseFloat(scoreValue) || 0;
    setScoreValue((current + amount).toString());
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!participantId) {
      setError('Please select a participant');
      return;
    }

    const score = parseFloat(scoreValue);
    if (isNaN(score)) {
      setError('Please enter a valid score');
      return;
    }

    setIsSubmitting(true);
    try {
      await onSubmit({
        participantId,
        roundNumber,
        category,
        scoreValue: score
      });
      // Clear score after submit
      setScoreValue('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save score');
    } finally {
      setIsSubmitting(false);
    }
  };

  const syncStatusConfig = {
    idle: { icon: null, text: '', color: '' },
    saving: {
      icon: <Loader2 className="h-3 w-3 animate-spin" />,
      text: 'Saving...',
      color: 'text-amber-600 dark:text-amber-400'
    },
    synced: {
      icon: <Check className="h-3 w-3" />,
      text: 'Synced',
      color: 'text-emerald-600 dark:text-emerald-400'
    },
    error: {
      icon: <AlertCircle className="h-3 w-3" />,
      text: 'Error',
      color: 'text-red-600 dark:text-red-400'
    }
  };

  const currentSyncStatus = syncStatusConfig[syncStatus];

  return (
    <div className="sticky bottom-0 border-t border-amber-900/20 bg-gradient-to-br from-amber-50 via-orange-50 to-amber-100 dark:from-slate-900 dark:via-slate-800 dark:to-slate-900 shadow-2xl backdrop-blur-xl">
      {/* Wood grain texture */}
      <div className="absolute inset-0 opacity-[0.03] dark:opacity-[0.05] bg-[url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMjAwIiBoZWlnaHQ9IjIwMCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48ZmlsdGVyIGlkPSJub2lzZSI+PGZlVHVyYnVsZW5jZSB0eXBlPSJmcmFjdGFsTm9pc2UiIGJhc2VGcmVxdWVuY3k9IjAuOCIgbnVtT2N0YXZlcz0iNCIgc3RpdGNoVGlsZXM9InN0aXRjaCIvPjwvZmlsdGVyPjxyZWN0IHdpZHRoPSIxMDAlIiBoZWlnaHQ9IjEwMCUiIGZpbHRlcj0idXJsKCNub2lzZSkiIG9wYWNpdHk9IjAuNSIvPjwvc3ZnPg==')] pointer-events-none" />

      <form onSubmit={handleSubmit} className="relative p-4 sm:p-6 space-y-4">
        {/* Sync Status Indicator */}
        {syncStatus !== 'idle' && (
          <div
            className={`flex items-center justify-center gap-2 text-xs font-medium ${currentSyncStatus.color} animate-in fade-in slide-in-from-top-2 duration-300`}
          >
            {currentSyncStatus.icon}
            <span>{currentSyncStatus.text}</span>
          </div>
        )}

        {/* Participant Tabs - Mobile Optimized */}
        <div className="space-y-2">
          <Label className="text-xs font-bold uppercase tracking-wider text-slate-600 dark:text-slate-400">
            Player
          </Label>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            {participants.map((p) => (
              <button
                key={p.id}
                type="button"
                onClick={() => setParticipantId(p.id)}
                className={`relative flex items-center justify-center gap-2 rounded-lg border-2 px-3 py-3 text-sm font-semibold transition-all active:scale-95 ${
                  participantId === p.id
                    ? 'border-amber-600 bg-gradient-to-br from-amber-500 to-orange-600 text-white shadow-lg shadow-amber-600/30'
                    : 'border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-300 hover:border-amber-600/50'
                }`}
              >
                <span className="truncate">{p.displayName.replace(/\s*\(io\)\s*/i, '')}</span>
                {p.isCurrentUser && participantId !== p.id && (
                  <span className="absolute top-1 right-1 h-2 w-2 rounded-full bg-amber-500" />
                )}
              </button>
            ))}
          </div>
        </div>

        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {/* Round Selector */}
          {rounds.length > 0 && (
            <div className="space-y-2">
              <Label htmlFor="round" className="text-xs font-bold uppercase tracking-wider text-slate-600 dark:text-slate-400">
                Round
              </Label>
              <Select
                value={roundNumber?.toString() ?? 'final'}
                onValueChange={(v: string) => setRoundNumber(v === 'final' ? null : parseInt(v))}
              >
                <SelectTrigger
                  id="round"
                  className="h-12 border-2 border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 font-semibold"
                >
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {rounds.map((r) => (
                    <SelectItem key={r} value={r.toString()}>
                      Round {r}
                    </SelectItem>
                  ))}
                  <SelectItem value="final">Final Score</SelectItem>
                </SelectContent>
              </Select>
            </div>
          )}

          {/* Category Selector */}
          {categories.length > 0 && (
            <div className="space-y-2">
              <Label htmlFor="category" className="text-xs font-bold uppercase tracking-wider text-slate-600 dark:text-slate-400">
                Category
              </Label>
              <Select value={category ?? 'none'} onValueChange={(v: string) => setCategory(v === 'none' ? null : v)}>
                <SelectTrigger
                  id="category"
                  className="h-12 border-2 border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 font-semibold"
                >
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No Category</SelectItem>
                  {categories.map((cat) => (
                    <SelectItem key={cat} value={cat}>
                      {cat}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}
        </div>

        {/* Score Input with Quick Actions */}
        <div className="space-y-3">
          <Label htmlFor="score" className="text-xs font-bold uppercase tracking-wider text-slate-600 dark:text-slate-400">
            Score
          </Label>

          <div className="flex items-center gap-2">
            {/* Quick Decrement Buttons */}
            <div className="flex gap-1">
              <Button
                type="button"
                variant="outline"
                size="icon"
                onClick={() => adjustScore(-5)}
                className="h-12 w-12 shrink-0 border-2 border-red-200 dark:border-red-900/50 bg-red-50 dark:bg-red-950/30 text-red-700 dark:text-red-400 hover:bg-red-100 dark:hover:bg-red-950/50 active:scale-90 transition-all"
                aria-label="Decrease by 5"
              >
                <span className="font-bold">-5</span>
              </Button>
              <Button
                type="button"
                variant="outline"
                size="icon"
                onClick={() => adjustScore(-1)}
                className="h-12 w-12 shrink-0 border-2 border-red-200 dark:border-red-900/50 bg-red-50 dark:bg-red-950/30 text-red-700 dark:text-red-400 hover:bg-red-100 dark:hover:bg-red-950/50 active:scale-90 transition-all"
                aria-label="Decrease by 1"
              >
                <Minus className="h-5 w-5" />
              </Button>
            </div>

            {/* Score Input */}
            <Input
              id="score"
              type="number"
              step="0.01"
              value={scoreValue}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setScoreValue(e.target.value)}
              placeholder="0"
              className="h-14 flex-1 border-2 border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-800 text-center font-mono text-2xl font-bold tabular-nums focus:border-amber-600 focus:ring-amber-600"
            />

            {/* Quick Increment Buttons */}
            <div className="flex gap-1">
              <Button
                type="button"
                variant="outline"
                size="icon"
                onClick={() => adjustScore(1)}
                className="h-12 w-12 shrink-0 border-2 border-emerald-200 dark:border-emerald-900/50 bg-emerald-50 dark:bg-emerald-950/30 text-emerald-700 dark:text-emerald-400 hover:bg-emerald-100 dark:hover:bg-emerald-950/50 active:scale-90 transition-all"
                aria-label="Increase by 1"
              >
                <Plus className="h-5 w-5" />
              </Button>
              <Button
                type="button"
                variant="outline"
                size="icon"
                onClick={() => adjustScore(5)}
                className="h-12 w-12 shrink-0 border-2 border-emerald-200 dark:border-emerald-900/50 bg-emerald-50 dark:bg-emerald-950/30 text-emerald-700 dark:text-emerald-400 hover:bg-emerald-100 dark:hover:bg-emerald-950/50 active:scale-90 transition-all"
                aria-label="Increase by 5"
              >
                <span className="font-bold">+5</span>
              </Button>
            </div>
          </div>
        </div>

        {/* Error Message */}
        {error && (
          <div className="flex items-center gap-2 rounded-lg bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900/50 px-3 py-2 text-sm text-red-700 dark:text-red-400 animate-in fade-in slide-in-from-top-2">
            <AlertCircle className="h-4 w-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {/* Action Buttons */}
        <div className="flex gap-2">
          {onUndo && (
            <Button
              type="button"
              variant="outline"
              onClick={onUndo}
              disabled={isSubmitting}
              className="flex-shrink-0 border-2 border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 hover:bg-slate-50 dark:hover:bg-slate-700 active:scale-95 transition-all"
            >
              <Undo2 className="h-4 w-4 mr-2" />
              Undo
            </Button>
          )}
          <Button
            type="submit"
            disabled={isSubmitting || !scoreValue}
            className="flex-1 h-12 bg-gradient-to-r from-amber-600 to-orange-600 hover:from-amber-700 hover:to-orange-700 text-white font-bold text-base shadow-lg shadow-amber-600/30 active:scale-95 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isSubmitting ? (
              <>
                <Loader2 className="h-5 w-5 mr-2 animate-spin" />
                Saving...
              </>
            ) : (
              <>
                <Check className="h-5 w-5 mr-2" />
                Add Score
              </>
            )}
          </Button>
        </div>
      </form>
    </div>
  );
}
