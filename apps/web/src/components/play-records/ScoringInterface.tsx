/**
 * ScoringInterface - Multi-dimensional Score Recording
 *
 * Matrix input for recording scores across multiple dimensions per player.
 * Supports: Points, Rankings, Wins, Custom dimensions with units.
 *
 * Issue #3892: Play Records Frontend UI
 */

'use client';

import { useState } from 'react';
import { Trophy, Save, Check, AlertCircle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Badge } from '@/components/ui/data-display/badge';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';

import type { SessionPlayer, SessionScoringConfig } from '@/lib/api/schemas/play-records.schemas';

export interface ScoringInterfaceProps {
  players: SessionPlayer[];
  scoringConfig: SessionScoringConfig;
  onRecordScore: (playerId: string, dimension: string, value: number) => Promise<void>;
  isRecording?: boolean;
}

interface ScoreState {
  [playerId: string]: {
    [dimension: string]: number | '';
  };
}

export function ScoringInterface({
  players,
  scoringConfig,
  onRecordScore,
  isRecording = false,
}: ScoringInterfaceProps) {
  const [scores, setScores] = useState<ScoreState>(() => {
    // Initialize with existing scores
    const initial: ScoreState = {};
    players.forEach((player) => {
      initial[player.id] = {};
      player.scores.forEach((score) => {
        initial[player.id][score.dimension] = score.value;
      });
    });
    return initial;
  });

  const [savingState, setSavingState] = useState<Record<string, boolean>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});

  const dimensions = scoringConfig.enabledDimensions;
  const units = scoringConfig.dimensionUnits;

  const handleScoreChange = (playerId: string, dimension: string, value: string) => {
    setScores((prev) => ({
      ...prev,
      [playerId]: {
        ...prev[playerId],
        [dimension]: value === '' ? '' : parseInt(value, 10) || 0,
      },
    }));

    // Clear error for this cell
    const errorKey = `${playerId}-${dimension}`;
    if (errors[errorKey]) {
      setErrors((prev) => {
        const newErrors = { ...prev };
        delete newErrors[errorKey];
        return newErrors;
      });
    }
  };

  const handleSaveScore = async (playerId: string, dimension: string) => {
    const value = scores[playerId]?.[dimension];

    if (value === '' || value === undefined) {
      return; // Don't save empty scores
    }

    const stateKey = `${playerId}-${dimension}`;
    setSavingState((prev) => ({ ...prev, [stateKey]: true }));
    setErrors((prev) => {
      const newErrors = { ...prev };
      delete newErrors[stateKey];
      return newErrors;
    });

    try {
      await onRecordScore(playerId, dimension, Number(value));
    } catch (error) {
      setErrors((prev) => ({
        ...prev,
        [stateKey]: error instanceof Error ? error.message : 'Failed to save',
      }));
    } finally {
      setSavingState((prev) => ({ ...prev, [stateKey]: false }));
    }
  };

  const handleSaveAll = async () => {
    const promises: Promise<void>[] = [];

    players.forEach((player) => {
      dimensions.forEach((dimension) => {
        const value = scores[player.id]?.[dimension];
        if (value !== '' && value !== undefined) {
          promises.push(handleSaveScore(player.id, dimension));
        }
      });
    });

    await Promise.all(promises);
  };

  if (dimensions.length === 0) {
    return (
      <Alert>
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>
          No scoring dimensions configured. You can still track play sessions without scores.
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold flex items-center gap-2">
          <Trophy className="w-5 h-5 text-amber-500" />
          Record Scores
        </h3>
        <Button
          onClick={handleSaveAll}
          disabled={isRecording}
          size="sm"
        >
          <Save className="w-4 h-4 mr-2" />
          Save All
        </Button>
      </div>

      {/* Scoring Matrix Table */}
      <div className="border rounded-lg overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-[200px]">Player</TableHead>
              {dimensions.map((dimension) => (
                <TableHead key={dimension} className="text-center">
                  <div>
                    <div className="font-semibold">{dimension}</div>
                    {units[dimension] && (
                      <div className="text-xs text-muted-foreground font-normal">
                        ({units[dimension]})
                      </div>
                    )}
                  </div>
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {players.map((player, index) => (
              <TableRow key={player.id}>
                <TableCell className="font-medium">
                  <div className="flex items-center gap-2">
                    <span className="flex items-center justify-center w-6 h-6 rounded-full bg-primary/10 text-primary text-xs font-bold">
                      {index + 1}
                    </span>
                    <span>{player.displayName}</span>
                    {!player.userId && (
                      <Badge variant="outline" className="text-xs">
                        Guest
                      </Badge>
                    )}
                  </div>
                </TableCell>
                {dimensions.map((dimension) => {
                  const stateKey = `${player.id}-${dimension}`;
                  const isSaving = savingState[stateKey];
                  const hasError = !!errors[stateKey];
                  const currentValue = scores[player.id]?.[dimension];
                  const existingScore = player.scores.find((s) => s.dimension === dimension);

                  return (
                    <TableCell key={dimension} className="text-center">
                      <div className="flex items-center justify-center gap-2">
                        <Input
                          type="number"
                          min="0"
                          step="1"
                          value={currentValue === '' ? '' : currentValue}
                          onChange={(e) =>
                            handleScoreChange(player.id, dimension, e.target.value)
                          }
                          onBlur={() => handleSaveScore(player.id, dimension)}
                          disabled={isSaving}
                          className={`w-24 text-center ${
                            hasError ? 'border-destructive' : ''
                          }`}
                          aria-label={`${player.displayName} ${dimension} score`}
                        />
                        {isSaving && (
                          <span className="text-muted-foreground text-xs">Saving...</span>
                        )}
                        {!isSaving && existingScore && (
                          <Check className="w-4 h-4 text-green-500" />
                        )}
                      </div>
                      {hasError && (
                        <p className="text-xs text-destructive mt-1">{errors[stateKey]}</p>
                      )}
                    </TableCell>
                  );
                })}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {players.length === 0 && (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            Add players first to enable score recording
          </AlertDescription>
        </Alert>
      )}
    </div>
  );
}
