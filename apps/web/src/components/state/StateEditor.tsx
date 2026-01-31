/* eslint-disable security/detect-object-injection */
/**
 * State Editor Container - Issue #2420
 *
 * Main container that orchestrates the three editor components:
 * - PlayerStateEditor: Manages player count, names, and scores
 * - ResourceEditor: Manages tokens, cards, and resources
 * - BoardStateEditor: Manages grid and piece placement
 *
 * Features:
 * - Validation across all sub-editors
 * - Consolidated state management
 * - Error handling and feedback
 */

'use client';

import { useState } from 'react';

import { z } from 'zod';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Separator } from '@/components/ui/navigation/separator';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';

import { BoardStateEditor } from './BoardStateEditor';
import { PlayerStateEditor } from './PlayerStateEditor';
import { ResourceEditor } from './ResourceEditor';

// ========== Mock Types (per backend schema) ==========

export interface PlayerState {
  id: string;
  name: string;
  score: number;
  color?: string;
}

export interface ResourceState {
  id: string;
  type: 'token' | 'card' | 'resource';
  name: string;
  quantity: number;
  ownerId?: string; // Player ID se risorsa assegnata a giocatore
}

export interface BoardPiece {
  id: string;
  type: string;
  position: { x: number; y: number };
  ownerId?: string; // Player ID se pezzo di un giocatore
}

export interface BoardState {
  gridWidth: number;
  gridHeight: number;
  pieces: BoardPiece[];
}

export interface GameState {
  players: PlayerState[];
  resources: ResourceState[];
  board: BoardState;
}

// ========== Validation Schemas ==========

const PlayerStateSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1, 'Nome giocatore obbligatorio').max(50),
  score: z.number().int().min(0),
  color: z.string().optional(),
});

const ResourceStateSchema = z.object({
  id: z.string().uuid(),
  type: z.enum(['token', 'card', 'resource']),
  name: z.string().min(1, 'Nome risorsa obbligatorio'),
  quantity: z.number().int().min(0, 'Quantità non può essere negativa'),
  ownerId: z.string().uuid().optional(),
});

const BoardPieceSchema = z.object({
  id: z.string().uuid(),
  type: z.string().min(1, 'Tipo pezzo obbligatorio'),
  position: z.object({
    x: z.number().int().min(0),
    y: z.number().int().min(0),
  }),
  ownerId: z.string().uuid().optional(),
});

const BoardStateSchema = z.object({
  gridWidth: z.number().int().min(1, 'Larghezza griglia minima: 1').max(100),
  gridHeight: z.number().int().min(1, 'Altezza griglia minima: 1').max(100),
  pieces: z.array(BoardPieceSchema),
});

const GameStateSchema = z.object({
  players: z.array(PlayerStateSchema).min(1, 'Almeno 1 giocatore richiesto'),
  resources: z.array(ResourceStateSchema),
  board: BoardStateSchema,
});

export type GameStateValidation = z.infer<typeof GameStateSchema>;

// ========== Component Props ==========

export interface StateEditorProps {
  /** Initial game state (for edit mode) */
  initialState?: GameState;
  /** Callback on state change */
  onChange?: (state: GameState) => void;
  /** Callback on validation error */
  onValidationError?: (errors: z.ZodError) => void;
  /** Read-only mode */
  readonly?: boolean;
}

// ========== Component ==========

export function StateEditor({
  initialState,
  onChange,
  onValidationError,
  readonly = false,
}: StateEditorProps) {
  const [gameState, setGameState] = useState<GameState>(
    initialState || {
      players: [],
      resources: [],
      board: { gridWidth: 10, gridHeight: 10, pieces: [] },
    }
  );

  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

  /**
   * Validate entire game state
   */
  const validateState = (state: GameState): boolean => {
    try {
      GameStateSchema.parse(state);
      setValidationErrors({});
      return true;
    } catch (error) {
      if (error instanceof z.ZodError) {
        const errors: Record<string, string> = {};
        error.issues.forEach((err: z.ZodIssue) => {
          const path = err.path.join('.');
          errors[path] = err.message;
        });
        setValidationErrors(errors);
        onValidationError?.(error);
      }
      return false;
    }
  };

  /**
   * Handle player state changes
   */
  const handlePlayersChange = (players: PlayerState[]) => {
    const newState = { ...gameState, players };
    setGameState(newState);
    validateState(newState);
    onChange?.(newState);
  };

  /**
   * Handle resource state changes
   */
  const handleResourcesChange = (resources: ResourceState[]) => {
    const newState = { ...gameState, resources };
    setGameState(newState);
    validateState(newState);
    onChange?.(newState);
  };

  /**
   * Handle board state changes
   */
  const handleBoardChange = (board: BoardState) => {
    const newState = { ...gameState, board };
    setGameState(newState);
    validateState(newState);
    onChange?.(newState);
  };

  return (
    <Card className="w-full">
      <CardHeader>
        <CardTitle>Editor Stato Gioco</CardTitle>
        <CardDescription>
          Modifica giocatori, risorse e disposizione plancia
          {readonly && ' (Solo lettura)'}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Tabs defaultValue="players" className="w-full">
          <TabsList className="grid w-full grid-cols-3">
            <TabsTrigger value="players">Giocatori</TabsTrigger>
            <TabsTrigger value="resources">Risorse</TabsTrigger>
            <TabsTrigger value="board">Plancia</TabsTrigger>
          </TabsList>

          <Separator className="my-4" />

          <TabsContent value="players" className="space-y-4">
            <PlayerStateEditor
              players={gameState.players}
              onChange={handlePlayersChange}
              readonly={readonly}
              validationErrors={validationErrors}
            />
          </TabsContent>

          <TabsContent value="resources" className="space-y-4">
            <ResourceEditor
              resources={gameState.resources}
              players={gameState.players}
              onChange={handleResourcesChange}
              readonly={readonly}
              validationErrors={validationErrors}
            />
          </TabsContent>

          <TabsContent value="board" className="space-y-4">
            <BoardStateEditor
              board={gameState.board}
              players={gameState.players}
              onChange={handleBoardChange}
              readonly={readonly}
              validationErrors={validationErrors}
            />
          </TabsContent>
        </Tabs>
      </CardContent>
    </Card>
  );
}
