'use client';

/**
 * Game Diff View Component (Issue #2515)
 *
 * Shows before/after comparison of changed fields.
 * Highlights modifications in yellow for admin review.
 *
 * Displays changes for:
 * - Basic info (name, description, BGG ID)
 * - Players & duration
 * - Complexity
 * - Cover image
 * - Tags/categories
 */

import { AlertCircle } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Badge } from '@/components/ui/data-display/badge';

import { type SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';

interface GameDiffViewProps {
  gameId: string;
  currentData: SharedGameDetail;
  previousData?: SharedGameDetail | null; // If editing, compare with previous version
}

interface FieldChange {
  field: string;
  label: string;
  before: string;
  after: string;
}

export function GameDiffView({ gameId, currentData, previousData }: GameDiffViewProps) {
  // If no previous data, this is a new game submission (no diff to show)
  const isNewGame = !previousData;

  // Compute changes
  const changes: FieldChange[] = [];

  if (!isNewGame && previousData) {
    if (currentData.title !== previousData.title) {
      changes.push({
        field: 'title',
        label: 'Name',
        before: previousData.title,
        after: currentData.title,
      });
    }

    if (currentData.description !== previousData.description) {
      changes.push({
        field: 'description',
        label: 'Description',
        before: previousData.description || 'N/A',
        after: currentData.description || 'N/A',
      });
    }

    if (currentData.minPlayers !== previousData.minPlayers) {
      changes.push({
        field: 'minPlayers',
        label: 'Min Players',
        before: previousData.minPlayers.toString(),
        after: currentData.minPlayers.toString(),
      });
    }

    if (currentData.maxPlayers !== previousData.maxPlayers) {
      changes.push({
        field: 'maxPlayers',
        label: 'Max Players',
        before: previousData.maxPlayers.toString(),
        after: currentData.maxPlayers.toString(),
      });
    }

    if (currentData.playingTimeMinutes !== previousData.playingTimeMinutes) {
      changes.push({
        field: 'playingTime',
        label: 'Playing Time',
        before: `${previousData.playingTimeMinutes} min`,
        after: `${currentData.playingTimeMinutes} min`,
      });
    }

    if (currentData.complexityRating !== previousData.complexityRating) {
      changes.push({
        field: 'complexity',
        label: 'Complexity',
        before: previousData.complexityRating?.toString() || 'N/A',
        after: currentData.complexityRating?.toString() || 'N/A',
      });
    }

    if (currentData.imageUrl !== previousData.imageUrl) {
      changes.push({
        field: 'imageUrl',
        label: 'Cover Image URL',
        before: previousData.imageUrl || 'N/A',
        after: currentData.imageUrl || 'N/A',
      });
    }
  }

  return (
    <div className="space-y-4">
      {isNewGame ? (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            This is a new game submission. No previous version to compare.
          </AlertDescription>
        </Alert>
      ) : changes.length === 0 ? (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>No changes detected from previous version.</AlertDescription>
        </Alert>
      ) : (
        <>
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              {changes.length} {changes.length === 1 ? 'field' : 'fields'} modified
            </AlertDescription>
          </Alert>

          <div className="space-y-3">
            {changes.map((change) => (
              <Card key={change.field} className="bg-yellow-50 border-yellow-200">
                <CardHeader className="py-3">
                  <CardTitle className="text-sm font-medium">{change.label}</CardTitle>
                </CardHeader>
                <CardContent className="py-3 space-y-2">
                  <div>
                    <Badge variant="outline" className="mb-1">
                      Before
                    </Badge>
                    <p className="text-sm text-muted-foreground line-through">{change.before}</p>
                  </div>
                  <div>
                    <Badge variant="default" className="mb-1">
                      After
                    </Badge>
                    <p className="text-sm font-medium">{change.after}</p>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
