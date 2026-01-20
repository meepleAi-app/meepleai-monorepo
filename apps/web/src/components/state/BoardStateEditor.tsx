/**
 * Board State Editor - Issue #2420
 *
 * Editor for managing board grid and piece placement.
 *
 * Features:
 * - Configure grid dimensions (width x height)
 * - Add/remove pieces
 * - Edit piece type and position
 * - Assign pieces to players (optional)
 * - Visual grid preview
 * - Validation for positions within grid bounds
 * - Supports readonly mode
 */

'use client';

import { Grid3x3, Plus, Trash2 } from 'lucide-react';

import { Card, CardContent } from '@/components/ui/data-display/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

import type { BoardState, PlayerState } from './StateEditor';

// ========== Component Props ==========

export interface BoardStateEditorProps {
  /** Current board state */
  board: BoardState;
  /** Players for piece assignment */
  players: PlayerState[];
  /** Callback on board change */
  onChange: (board: BoardState) => void;
  /** Read-only mode */
  readonly?: boolean;
  /** Validation errors map (path -> error message) */
  validationErrors?: Record<string, string>;
}

// ========== Component ==========

export function BoardStateEditor({
  board,
  players,
  onChange,
  readonly = false,
  validationErrors = {},
}: BoardStateEditorProps) {
  /**
   * Update grid dimensions
   */
  const handleUpdateGrid = (field: 'gridWidth' | 'gridHeight', value: number) => {
    onChange({
      ...board,
      [field]: value,
    });
  };

  /**
   * Add new piece
   */
  const handleAddPiece = () => {
    const newPiece = {
      id: crypto.randomUUID(),
      type: 'pawn',
      position: { x: 0, y: 0 },
    };
    onChange({
      ...board,
      pieces: [...board.pieces, newPiece],
    });
  };

  /**
   * Remove piece
   */
  const handleRemovePiece = (id: string) => {
    onChange({
      ...board,
      pieces: board.pieces.filter(p => p.id !== id),
    });
  };

  /**
   * Update piece field
   */
  const handleUpdatePiece = (id: string, updates: Partial<(typeof board.pieces)[0]>) => {
    onChange({
      ...board,
      pieces: board.pieces.map(p => (p.id === id ? { ...p, ...updates } : p)),
    });
  };

  /**
   * Get validation error for specific field
   */
  const getFieldError = (field: string): string | undefined => {
    return validationErrors[`board.${field}`];
  };

  /**
   * Get validation error for specific piece field
   */
  const getPieceFieldError = (index: number, field: string): string | undefined => {
    return validationErrors[`board.pieces.${index}.${field}`];
  };

  /**
   * Render visual grid preview
   */
  const renderGridPreview = () => {
    const maxPreviewSize = 20;
    const cellSize = 20;
    const previewWidth = Math.min(board.gridWidth, maxPreviewSize);
    const previewHeight = Math.min(board.gridHeight, maxPreviewSize);

    return (
      <div className="rounded-md border border-gray-200 bg-gray-50 p-4">
        <div className="mb-2 flex items-center justify-between text-sm text-muted-foreground">
          <span>Anteprima Griglia</span>
          <span>
            {board.gridWidth} × {board.gridHeight}
          </span>
        </div>
        <div
          className="grid gap-0.5 border border-gray-300"
          style={{
            gridTemplateColumns: `repeat(${previewWidth}, ${cellSize}px)`,
            width: `${previewWidth * cellSize + (previewWidth - 1) * 2}px`,
          }}
        >
          {Array.from({ length: previewHeight }).map((_, y) =>
            Array.from({ length: previewWidth }).map((_, x) => {
              const piece = board.pieces.find(p => p.position.x === x && p.position.y === y);
              const player = piece?.ownerId ? players.find(p => p.id === piece.ownerId) : undefined;

              return (
                <div
                  key={`${x}-${y}`}
                  className="flex items-center justify-center border border-gray-200 bg-white text-xs"
                  style={{
                    width: `${cellSize}px`,
                    height: `${cellSize}px`,
                    backgroundColor: piece ? player?.color || '#e5e7eb' : 'white',
                  }}
                  title={piece ? `${piece.type} (${x},${y})` : `(${x},${y})`}
                >
                  {piece && <span className="text-[10px]">●</span>}
                </div>
              );
            })
          )}
        </div>
        {(board.gridWidth > maxPreviewSize || board.gridHeight > maxPreviewSize) && (
          <p className="mt-2 text-xs text-muted-foreground">
            Anteprima limitata a {maxPreviewSize}×{maxPreviewSize}
          </p>
        )}
      </div>
    );
  };

  return (
    <div className="space-y-4">
      <div>
        <h3 className="text-lg font-semibold">Plancia</h3>
        <p className="text-sm text-muted-foreground">Configura griglia e posiziona pezzi</p>
      </div>

      {/* Grid Configuration */}
      <Card className="p-4">
        <h4 className="mb-3 font-medium">Dimensioni Griglia</h4>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <Label htmlFor="grid-width">Larghezza</Label>
            <Input
              id="grid-width"
              type="number"
              value={board.gridWidth}
              onChange={e => handleUpdateGrid('gridWidth', parseInt(e.target.value) || 1)}
              disabled={readonly}
              min={1}
              max={100}
              className={getFieldError('gridWidth') ? 'border-red-500' : ''}
            />
            {getFieldError('gridWidth') && (
              <p className="mt-1 text-sm text-red-500">{getFieldError('gridWidth')}</p>
            )}
          </div>

          <div>
            <Label htmlFor="grid-height">Altezza</Label>
            <Input
              id="grid-height"
              type="number"
              value={board.gridHeight}
              onChange={e => handleUpdateGrid('gridHeight', parseInt(e.target.value) || 1)}
              disabled={readonly}
              min={1}
              max={100}
              className={getFieldError('gridHeight') ? 'border-red-500' : ''}
            />
            {getFieldError('gridHeight') && (
              <p className="mt-1 text-sm text-red-500">{getFieldError('gridHeight')}</p>
            )}
          </div>
        </div>
      </Card>

      {/* Grid Preview */}
      {renderGridPreview()}

      {/* Pieces Management */}
      <div className="flex items-center justify-between">
        <h4 className="font-medium">Pezzi</h4>
        {!readonly && (
          <Button onClick={handleAddPiece} size="sm" variant="outline">
            <Plus className="mr-2 h-4 w-4" />
            Aggiungi Pezzo
          </Button>
        )}
      </div>

      {board.pieces.length === 0 ? (
        <Card className="border-dashed">
          <CardContent className="flex flex-col items-center justify-center py-8">
            <Grid3x3 className="mb-2 h-12 w-12 text-muted-foreground" />
            <p className="text-sm text-muted-foreground">Nessun pezzo posizionato</p>
            {!readonly && (
              <Button onClick={handleAddPiece} size="sm" variant="ghost" className="mt-2">
                Aggiungi il primo pezzo
              </Button>
            )}
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {board.pieces.map((piece, index) => (
            <Card key={piece.id} className="p-4">
              <div className="flex items-start gap-4">
                <div className="flex-1 space-y-3">
                  <div>
                    <Label htmlFor={`piece-type-${piece.id}`}>Tipo</Label>
                    <Input
                      id={`piece-type-${piece.id}`}
                      value={piece.type}
                      onChange={e => handleUpdatePiece(piece.id, { type: e.target.value })}
                      disabled={readonly}
                      placeholder="es: pedone, torre, regina"
                      className={getPieceFieldError(index, 'type') ? 'border-red-500' : ''}
                    />
                    {getPieceFieldError(index, 'type') && (
                      <p className="mt-1 text-sm text-red-500">
                        {getPieceFieldError(index, 'type')}
                      </p>
                    )}
                  </div>

                  <div className="grid grid-cols-3 gap-3">
                    <div>
                      <Label htmlFor={`piece-x-${piece.id}`}>Posizione X</Label>
                      <Input
                        id={`piece-x-${piece.id}`}
                        type="number"
                        value={piece.position.x}
                        onChange={e =>
                          handleUpdatePiece(piece.id, {
                            position: { ...piece.position, x: parseInt(e.target.value) || 0 },
                          })
                        }
                        disabled={readonly}
                        min={0}
                        max={board.gridWidth - 1}
                        className={getPieceFieldError(index, 'position.x') ? 'border-red-500' : ''}
                      />
                    </div>

                    <div>
                      <Label htmlFor={`piece-y-${piece.id}`}>Posizione Y</Label>
                      <Input
                        id={`piece-y-${piece.id}`}
                        type="number"
                        value={piece.position.y}
                        onChange={e =>
                          handleUpdatePiece(piece.id, {
                            position: { ...piece.position, y: parseInt(e.target.value) || 0 },
                          })
                        }
                        disabled={readonly}
                        min={0}
                        max={board.gridHeight - 1}
                        className={getPieceFieldError(index, 'position.y') ? 'border-red-500' : ''}
                      />
                    </div>

                    <div>
                      <Label htmlFor={`piece-owner-${piece.id}`}>Proprietario</Label>
                      <Select
                        value={piece.ownerId || 'none'}
                        onValueChange={value =>
                          handleUpdatePiece(piece.id, {
                            ownerId: value === 'none' ? undefined : value,
                          })
                        }
                        disabled={readonly}
                      >
                        <SelectTrigger id={`piece-owner-${piece.id}`}>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="none">Nessuno</SelectItem>
                          {players.map(player => (
                            <SelectItem key={player.id} value={player.id}>
                              {player.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                </div>

                {!readonly && (
                  <Button
                    onClick={() => handleRemovePiece(piece.id)}
                    variant="ghost"
                    size="icon"
                    className="text-red-500 hover:bg-red-50 hover:text-red-600"
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                )}
              </div>
            </Card>
          ))}
        </div>
      )}

      <div className="rounded-md border border-blue-200 bg-blue-50 p-3 text-sm text-blue-800">
        <strong>Pezzi posizionati:</strong> {board.pieces.length} su {board.gridWidth} ×{' '}
        {board.gridHeight} celle
      </div>
    </div>
  );
}
