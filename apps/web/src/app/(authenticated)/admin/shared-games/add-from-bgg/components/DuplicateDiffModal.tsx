'use client';

/**
 * Duplicate Diff Modal Component
 * Issue: Admin Add Shared Game from BGG flow
 *
 * Features:
 * - Shows existing game vs BGG data comparison
 * - Field-by-field diff with visual indicators
 * - Checkbox selection for fields to update
 * - Update/Cancel actions
 */

import { useState, useCallback, useMemo } from 'react';

import { AlertTriangle, ArrowRight, Check, Loader2, RefreshCw, X } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Separator } from '@/components/ui/navigation/separator';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Label } from '@/components/ui/primitives/label';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import type { BggDuplicateCheckResult, BggUpdatableField } from '@/lib/api/schemas/shared-games.schemas';
import { cn } from '@/lib/utils';

export interface DuplicateDiffModalProps {
  duplicateResult: BggDuplicateCheckResult;
  onUpdate: (fieldsToUpdate: string[]) => void;
  onCancel: () => void;
  isSubmitting: boolean;
}

interface FieldDiff {
  field: BggUpdatableField;
  label: string;
  existingValue: string | null;
  bggValue: string | null;
  isDifferent: boolean;
}

// Helper to strip HTML tags
function stripHtml(html: string | undefined | null): string {
  if (!html) return '';
  return html
    .replace(/<[^>]*>/g, '')
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/\s+/g, ' ')
    .trim();
}

// Format array values for display
function formatArray(arr: string[] | undefined | null): string {
  if (!arr || arr.length === 0) return '-';
  return arr.join(', ');
}

// Format number values
function formatNumber(num: number | undefined | null): string {
  if (num === undefined || num === null) return '-';
  return num.toString();
}

export function DuplicateDiffModal({
  duplicateResult,
  onUpdate,
  onCancel,
  isSubmitting,
}: DuplicateDiffModalProps) {
  const { existingGame, bggData } = duplicateResult;

  // Selected fields to update
  const [selectedFields, setSelectedFields] = useState<Set<string>>(new Set());

  // Calculate field diffs
  const fieldDiffs: FieldDiff[] = useMemo(() => {
    if (!existingGame || !bggData) return [];

    const diffs: FieldDiff[] = [
      {
        field: 'title',
        label: 'Titolo',
        existingValue: existingGame.title,
        bggValue: bggData.name,
        isDifferent: existingGame.title !== bggData.name,
      },
      {
        field: 'description',
        label: 'Descrizione',
        existingValue: stripHtml(existingGame.description)?.substring(0, 100) + '...',
        bggValue: stripHtml(bggData.description)?.substring(0, 100) + '...',
        isDifferent: stripHtml(existingGame.description) !== stripHtml(bggData.description),
      },
      {
        field: 'minPlayers',
        label: 'Giocatori Min',
        existingValue: formatNumber(existingGame.minPlayers),
        bggValue: formatNumber(bggData.minPlayers),
        isDifferent: existingGame.minPlayers !== bggData.minPlayers,
      },
      {
        field: 'maxPlayers',
        label: 'Giocatori Max',
        existingValue: formatNumber(existingGame.maxPlayers),
        bggValue: formatNumber(bggData.maxPlayers),
        isDifferent: existingGame.maxPlayers !== bggData.maxPlayers,
      },
      {
        field: 'playingTime',
        label: 'Durata',
        existingValue: existingGame.playingTimeMinutes ? `${existingGame.playingTimeMinutes} min` : '-',
        bggValue: bggData.playingTime ? `${bggData.playingTime} min` : '-',
        isDifferent: existingGame.playingTimeMinutes !== bggData.playingTime,
      },
      {
        field: 'complexityRating',
        label: 'Complessità',
        existingValue: existingGame.complexityRating?.toFixed(2) || '-',
        bggValue: bggData.averageWeight?.toFixed(2) || '-',
        isDifferent: Math.abs((existingGame.complexityRating || 0) - (bggData.averageWeight || 0)) > 0.01,
      },
      {
        field: 'averageRating',
        label: 'Voto BGG',
        existingValue: existingGame.averageRating?.toFixed(1) || '-',
        bggValue: bggData.averageRating?.toFixed(1) || '-',
        isDifferent: Math.abs((existingGame.averageRating || 0) - (bggData.averageRating || 0)) > 0.1,
      },
      {
        field: 'thumbnailUrl',
        label: 'Immagine',
        existingValue: existingGame.thumbnailUrl ? 'Presente' : '-',
        bggValue: bggData.thumbnailUrl ? 'Presente' : '-',
        isDifferent: existingGame.thumbnailUrl !== bggData.thumbnailUrl,
      },
      {
        field: 'imageUrl',
        label: 'Immagine HD',
        existingValue: existingGame.imageUrl ? 'Presente' : '-',
        bggValue: bggData.imageUrl ? 'Presente' : '-',
        isDifferent: existingGame.imageUrl !== bggData.imageUrl,
      },
      {
        field: 'designers',
        label: 'Autori',
        existingValue: formatArray(existingGame.designers?.map((d) => d.name)),
        bggValue: formatArray(bggData.designers),
        isDifferent:
          JSON.stringify(existingGame.designers?.map((d) => d.name).sort()) !==
          JSON.stringify(bggData.designers?.sort()),
      },
      {
        field: 'publishers',
        label: 'Editori',
        existingValue: formatArray(existingGame.publishers?.map((p) => p.name).slice(0, 5)),
        bggValue: formatArray(bggData.publishers?.slice(0, 5)),
        isDifferent:
          JSON.stringify(existingGame.publishers?.map((p) => p.name).slice(0, 5).sort()) !==
          JSON.stringify(bggData.publishers?.slice(0, 5).sort()),
      },
      {
        field: 'categories',
        label: 'Categorie',
        existingValue: formatArray(existingGame.categories?.map((c) => c.name)),
        bggValue: formatArray(bggData.categories),
        isDifferent:
          JSON.stringify(existingGame.categories?.map((c) => c.name).sort()) !==
          JSON.stringify(bggData.categories?.sort()),
      },
      {
        field: 'mechanics',
        label: 'Meccaniche',
        existingValue: formatArray(existingGame.mechanics?.map((m) => m.name)),
        bggValue: formatArray(bggData.mechanics),
        isDifferent:
          JSON.stringify(existingGame.mechanics?.map((m) => m.name).sort()) !==
          JSON.stringify(bggData.mechanics?.sort()),
      },
    ];

    return diffs;
  }, [existingGame, bggData]);

  // Count of different fields
  const differentFieldsCount = useMemo(
    () => fieldDiffs.filter((d) => d.isDifferent).length,
    [fieldDiffs]
  );

  // Toggle field selection
  const toggleField = useCallback((field: string) => {
    setSelectedFields((prev) => {
      const next = new Set(prev);
      if (next.has(field)) {
        next.delete(field);
      } else {
        next.add(field);
      }
      return next;
    });
  }, []);

  // Select all different fields
  const selectAllDifferent = useCallback(() => {
    const differentFields = fieldDiffs.filter((d) => d.isDifferent).map((d) => d.field);
    setSelectedFields(new Set(differentFields));
  }, [fieldDiffs]);

  // Clear selection
  const clearSelection = useCallback(() => {
    setSelectedFields(new Set());
  }, []);

  // Handle update
  const handleUpdate = useCallback(() => {
    onUpdate(Array.from(selectedFields));
  }, [selectedFields, onUpdate]);

  if (!existingGame || !bggData) {
    return null;
  }

  return (
    <Card className="max-w-4xl">
      <CardHeader>
        <Alert variant="destructive" className="mb-4">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Gioco già presente nel catalogo</AlertTitle>
          <AlertDescription>
            È stato trovato un gioco esistente con lo stesso BGG ID. Puoi aggiornare i campi
            selezionati con i dati più recenti da BoardGameGeek.
          </AlertDescription>
        </Alert>

        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="text-xl">Confronto Dati</CardTitle>
            <CardDescription>
              {differentFieldsCount} campi diversi • {selectedFields.size} selezionati per
              l'aggiornamento
            </CardDescription>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={selectAllDifferent}>
              <Check className="h-4 w-4 mr-1" />
              Seleziona Tutti
            </Button>
            <Button variant="outline" size="sm" onClick={clearSelection}>
              <X className="h-4 w-4 mr-1" />
              Deseleziona
            </Button>
          </div>
        </div>
      </CardHeader>

      <CardContent>
        <ScrollArea className="h-[400px] pr-4">
          <div className="space-y-2">
            {fieldDiffs.map((diff) => (
              <div
                key={diff.field}
                className={cn(
                  'flex items-start gap-4 p-3 rounded-lg border transition-colors',
                  diff.isDifferent
                    ? selectedFields.has(diff.field)
                      ? 'bg-primary/5 border-primary'
                      : 'bg-amber-50/50 border-amber-200 dark:bg-amber-950/20 dark:border-amber-800'
                    : 'bg-muted/30 border-transparent'
                )}
              >
                {/* Checkbox */}
                <div className="pt-0.5">
                  <Checkbox
                    id={diff.field}
                    checked={selectedFields.has(diff.field)}
                    onCheckedChange={() => toggleField(diff.field)}
                    disabled={!diff.isDifferent}
                  />
                </div>

                {/* Field Info */}
                <div className="flex-1 min-w-0">
                  <Label
                    htmlFor={diff.field}
                    className={cn(
                      'font-medium cursor-pointer',
                      !diff.isDifferent && 'text-muted-foreground'
                    )}
                  >
                    {diff.label}
                    {diff.isDifferent && (
                      <Badge variant="outline" className="ml-2 text-xs">
                        Diverso
                      </Badge>
                    )}
                  </Label>

                  {/* Values comparison */}
                  <div className="mt-1 flex items-center gap-2 text-sm">
                    <div
                      className={cn(
                        'flex-1 p-2 rounded bg-background border truncate',
                        diff.isDifferent && 'border-red-200 dark:border-red-800'
                      )}
                      title={diff.existingValue || '-'}
                    >
                      <span className="text-xs text-muted-foreground block mb-0.5">
                        Attuale:
                      </span>
                      {diff.existingValue || '-'}
                    </div>

                    <ArrowRight
                      className={cn(
                        'h-4 w-4 flex-shrink-0',
                        diff.isDifferent ? 'text-amber-500' : 'text-muted-foreground'
                      )}
                    />

                    <div
                      className={cn(
                        'flex-1 p-2 rounded bg-background border truncate',
                        diff.isDifferent && 'border-green-200 dark:border-green-800'
                      )}
                      title={diff.bggValue || '-'}
                    >
                      <span className="text-xs text-muted-foreground block mb-0.5">BGG:</span>
                      {diff.bggValue || '-'}
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </ScrollArea>
      </CardContent>

      <Separator />

      <CardFooter className="flex justify-between bg-muted/50 p-4">
        <Button variant="outline" onClick={onCancel} disabled={isSubmitting}>
          Annulla
        </Button>

        <div className="flex gap-2">
          <Button
            variant="secondary"
            onClick={() => onUpdate([])}
            disabled={isSubmitting}
            title="Vai al gioco esistente senza modifiche"
          >
            Vai al Gioco Esistente
          </Button>
          <Button
            onClick={handleUpdate}
            disabled={isSubmitting || selectedFields.size === 0}
          >
            {isSubmitting ? (
              <>
                <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                Aggiornamento...
              </>
            ) : (
              <>
                <RefreshCw className="h-4 w-4 mr-2" />
                Aggiorna {selectedFields.size} Campi
              </>
            )}
          </Button>
        </div>
      </CardFooter>
    </Card>
  );
}
