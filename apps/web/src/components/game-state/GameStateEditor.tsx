/**
 * GameStateEditor Component
 * Issue #2406: Game State Editor UI
 *
 * Editable game state form with dynamic field generation from JSON Schema.
 */

'use client';

import { useCallback } from 'react';

import Form from '@rjsf/core';
import validator from '@rjsf/validator-ajv8';
import { Save } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useGameStateStore } from '@/lib/stores/game-state-store';
import type { GameState } from '@/types/game-state';

import { PlayerStateCard } from './PlayerStateCard';

import type { IChangeEvent } from '@rjsf/core';
import type { JSONSchema7 } from 'json-schema';

interface GameStateEditorProps {
  sessionId: string;
  onSave?: (state: GameState) => void;
  onCancel?: () => void;
}

/**
 * Custom widgets for better UX
 */
const customWidgets = {
  // Add custom widgets here if needed (e.g., color picker, range slider)
};

/**
 * UI Schema for improved form layout
 */
const uiSchema = {
  players: {
    'ui:options': {
      orderable: true, // Allow reordering players
    },
    items: {
      score: {
        'ui:widget': 'updown', // Use number input with up/down buttons
      },
      resources: {
        'ui:options': {
          inline: true,
        },
      },
    },
  },
  phase: {
    'ui:widget': 'select', // Dropdown for phase
  },
};

export function GameStateEditor({ sessionId, onSave, onCancel }: GameStateEditorProps) {
  const { currentState, template, updateField, canUndo, canRedo, undo, redo } = useGameStateStore();

  // Parse schema from template
  const schema: JSONSchema7 | undefined = template?.schemaJson
    ? (JSON.parse(template.schemaJson) as JSONSchema7)
    : undefined;

  const handleChange = useCallback((event: IChangeEvent<GameState>) => {
    // Extract changed field path and value
    // RJSF provides full form data, we need to diff with current state
    const newState = event.formData;
    if (newState) {
      useGameStateStore.getState().setState(newState, 'State updated via form');
    }
  }, []);

  const handleSave = useCallback(async () => {
    if (!currentState) return;

    try {
      // Save to backend
      await useGameStateStore.getState().saveState(sessionId);
      onSave?.(currentState);
    } catch (error) {
      console.error('Failed to save state:', error);
    }
  }, [currentState, sessionId, onSave]);

  if (!currentState) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>No State Available</CardTitle>
          <CardDescription>Load a game state to start editing</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  if (!schema) {
    // Fallback: Show manual editor without schema-driven form
    return (
      <div className="space-y-6" data-testid="game-state-editor">
        {/* Manual Editor (when no schema available) */}
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>Game State Editor</CardTitle>
                <CardDescription>Manually edit game state</CardDescription>
              </div>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={undo}
                  disabled={!canUndo()}
                  aria-label="Undo"
                >
                  Undo
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={redo}
                  disabled={!canRedo()}
                  aria-label="Redo"
                >
                  Redo
                </Button>
                <Button size="sm" onClick={handleSave}>
                  <Save className="mr-2 h-4 w-4" />
                  Save
                </Button>
                {onCancel && (
                  <Button variant="outline" size="sm" onClick={onCancel}>
                    Cancel
                  </Button>
                )}
              </div>
            </div>
          </CardHeader>
          <CardContent className="space-y-6">
            {/* Players Editor */}
            <div>
              <h3 className="text-lg font-semibold mb-4">
                Players ({currentState.players.length})
              </h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {currentState.players.map((player, index) => (
                  <PlayerStateCard
                    key={`${player.playerName}-${index}`}
                    player={player}
                    isCurrentPlayer={currentState.currentPlayerIndex === index}
                    editable={true}
                    onScoreChange={newScore => {
                      updateField(['players', String(index), 'score'], newScore, 'Update score');
                    }}
                    onResourceChange={(resourceKey, newValue) => {
                      updateField(
                        ['players', String(index), 'resources', resourceKey],
                        newValue,
                        `Update ${resourceKey}`
                      );
                    }}
                  />
                ))}
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  // Schema-driven form with RJSF
  return (
    <div className="space-y-6" data-testid="game-state-editor-schema">
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>Game State Editor</CardTitle>
              <CardDescription>
                Editing with schema: {template?.name} (v{template?.version})
              </CardDescription>
            </div>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={undo}
                disabled={!canUndo()}
                aria-label="Undo"
              >
                Undo
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={redo}
                disabled={!canRedo()}
                aria-label="Redo"
              >
                Redo
              </Button>
              <Button size="sm" onClick={handleSave}>
                <Save className="mr-2 h-4 w-4" />
                Save
              </Button>
              {onCancel && (
                <Button variant="outline" size="sm" onClick={onCancel}>
                  Cancel
                </Button>
              )}
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <Form
            schema={schema}
            uiSchema={uiSchema}
            formData={currentState}
            validator={validator}
            onChange={handleChange}
            widgets={customWidgets}
            liveValidate
            showErrorList="bottom"
            noHtml5Validate
          >
            {/* Hide default submit button (we have custom buttons) */}
            <button type="submit" style={{ display: 'none' }} aria-hidden="true" />
          </Form>
        </CardContent>
      </Card>
    </div>
  );
}
