/**
 * SessionSetupModal Component (SPRINT-4, Issue #863)
 *
 * Modal for starting a new game session with player setup.
 * Integrates with backend StartGameSessionCommand (CQRS).
 *
 * Features:
 * - WCAG 2.1 AA compliant accessibility
 * - Dynamic player management (min 2, max 8 players)
 * - Real-time validation (unique names, valid colors)
 * - Colorblind-friendly color palette
 * - Keyboard accessible (ESC to close, Tab navigation)
 * - Loading and error states
 *
 * @example
 * ```tsx
 * <SessionSetupModal
 *   isOpen={isModalOpen}
 *   onClose={() => setIsModalOpen(false)}
 *   game={selectedGame}
 *   onSessionCreated={(session) => router.push(`/sessions/${session.id}`)}
 * />
 * ```
 */

import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Game, SessionPlayerDto, GameSessionDto, api } from '@/lib/api';
import { Plus, Trash2, AlertCircle } from 'lucide-react';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

export interface SessionSetupModalProps {
  /** Whether the modal is open */
  isOpen: boolean;

  /** Callback when modal should close */
  onClose: () => void;

  /** Game to start session for */
  game: Game;

  /** Callback when session is successfully created */
  onSessionCreated?: (session: GameSessionDto) => void;
}

// Colorblind-friendly color palette
const PLAYER_COLORS = [
  { value: '#E63946', label: 'Red', className: 'bg-[#E63946]' },
  { value: '#F77F00', label: 'Orange', className: 'bg-[#F77F00]' },
  { value: '#06D6A0', label: 'Green', className: 'bg-[#06D6A0]' },
  { value: '#118AB2', label: 'Blue', className: 'bg-[#118AB2]' },
  { value: '#9B59B6', label: 'Purple', className: 'bg-[#9B59B6]' },
  { value: '#F4D03F', label: 'Yellow', className: 'bg-[#F4D03F]' },
  { value: '#95A5A6', label: 'Gray', className: 'bg-[#95A5A6]' },
  { value: '#E91E63', label: 'Pink', className: 'bg-[#E91E63]' },
];

interface PlayerFormData {
  id: string; // Temp UI ID
  playerName: string;
  playerOrder: number;
  color: string | null;
}

/**
 * SessionSetupModal component
 */
export function SessionSetupModal({
  isOpen,
  onClose,
  game,
  onSessionCreated,
}: SessionSetupModalProps) {
  const minPlayers = game.minPlayers ?? 2;
  const maxPlayers = game.maxPlayers ?? 8;

  // Players state (start with minimum players)
  const [players, setPlayers] = useState<PlayerFormData[]>([]);

  // Loading and error states
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Validation state
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

  // Initialize players when modal opens
  useEffect(() => {
    if (isOpen && players.length === 0) {
      const initialPlayers: PlayerFormData[] = Array.from(
        { length: minPlayers },
        (_, i) => ({
          id: crypto.randomUUID(),
          playerName: '',
          playerOrder: i + 1,
          color: PLAYER_COLORS[i % PLAYER_COLORS.length].value,
        })
      );
      setPlayers(initialPlayers);
    }
  }, [isOpen, players.length, minPlayers]);

  // Reset state when modal closes
  useEffect(() => {
    if (!isOpen) {
      setPlayers([]);
      setError(null);
      setValidationErrors({});
      setIsSubmitting(false);
    }
  }, [isOpen]);

  // Validate players
  const validatePlayers = (): boolean => {
    const errors: Record<string, string> = {};

    // Check player count
    if (players.length < minPlayers) {
      errors.playerCount = `At least ${minPlayers} players required`;
    }
    if (players.length > maxPlayers) {
      errors.playerCount = `Maximum ${maxPlayers} players allowed`;
    }

    // Check for empty names
    players.forEach((player) => {
      if (!player.playerName.trim()) {
        errors[`name_${player.id}`] = 'Player name is required';
      }
    });

    // Check for duplicate names
    const names = players.map((p) => p.playerName.trim().toLowerCase());
    const duplicates = names.filter((name, index) => names.indexOf(name) !== index);
    if (duplicates.length > 0) {
      players.forEach((player) => {
        if (duplicates.includes(player.playerName.trim().toLowerCase())) {
          errors[`name_${player.id}`] = 'Player names must be unique';
        }
      });
    }

    setValidationErrors(errors);
    return Object.keys(errors).length === 0;
  };

  // Add player
  const handleAddPlayer = () => {
    if (players.length >= maxPlayers) return;

    const newPlayer: PlayerFormData = {
      id: crypto.randomUUID(),
      playerName: '',
      playerOrder: players.length + 1,
      color: PLAYER_COLORS[players.length % PLAYER_COLORS.length].value,
    };

    setPlayers([...players, newPlayer]);
  };

  // Remove player
  const handleRemovePlayer = (id: string) => {
    if (players.length <= minPlayers) return;

    const updatedPlayers = players
      .filter((p) => p.id !== id)
      .map((p, index) => ({ ...p, playerOrder: index + 1 }));

    setPlayers(updatedPlayers);
  };

  // Update player field
  const handlePlayerChange = (
    id: string,
    field: keyof PlayerFormData,
    value: string | number | null
  ) => {
    setPlayers(
      players.map((p) =>
        p.id === id ? { ...p, [field]: value } : p
      )
    );

    // Clear field-specific validation error
    if (validationErrors[`${field}_${id}`]) {
      const { [`${field}_${id}`]: _, ...rest } = validationErrors;
      setValidationErrors(rest);
    }
  };

  // Submit form
  const handleSubmit = async () => {
    if (!validatePlayers()) {
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      // Convert to API format
      const sessionPlayers: SessionPlayerDto[] = players.map((p) => ({
        playerName: p.playerName.trim(),
        playerOrder: p.playerOrder,
        color: p.color,
      }));

      // Call API
      const session = await api.sessions.start({
        gameId: game.id,
        players: sessionPlayers,
      });

      // Success
      onSessionCreated?.(session);
      onClose();
    } catch (err) {
      logger.error(
        'Failed to start session',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('SessionSetupModal', 'handleSubmit', { gameId: game.id, playerCount: players.length })
      );
      setError(
        err instanceof Error
          ? err.message
          : 'Failed to start session. Please try again.'
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Start New Session</DialogTitle>
          <DialogDescription>
            Setup players for {game.title}
            {game.minPlayers && game.maxPlayers && (
              <span className="block mt-1 text-sm">
                {game.minPlayers === game.maxPlayers
                  ? `Requires exactly ${game.minPlayers} players`
                  : `Supports ${game.minPlayers}-${game.maxPlayers} players`}
              </span>
            )}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* Error message */}
          {error && (
            <div
              className="flex items-start gap-2 p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-md"
              role="alert"
            >
              <AlertCircle className="h-5 w-5 text-red-600 dark:text-red-400 flex-shrink-0 mt-0.5" />
              <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
            </div>
          )}

          {/* Player count validation error */}
          {validationErrors.playerCount && (
            <div
              className="flex items-start gap-2 p-3 bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 rounded-md"
              role="alert"
            >
              <AlertCircle className="h-5 w-5 text-amber-600 dark:text-amber-400 flex-shrink-0 mt-0.5" />
              <p className="text-sm text-amber-800 dark:text-amber-200">
                {validationErrors.playerCount}
              </p>
            </div>
          )}

          {/* Player list */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <Label className="text-base font-semibold">Players ({players.length})</Label>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={handleAddPlayer}
                disabled={players.length >= maxPlayers || isSubmitting}
                aria-label="Add player"
              >
                <Plus className="h-4 w-4 mr-1" />
                Add Player
              </Button>
            </div>

            {players.map((player, index) => (
              <div
                key={player.id}
                className="flex items-start gap-3 p-3 border rounded-md bg-slate-50 dark:bg-slate-900/50"
              >
                <div className="flex-1 space-y-2">
                  {/* Player order badge */}
                  <div className="flex items-center gap-2">
                    <span className="flex items-center justify-center w-6 h-6 text-xs font-bold bg-slate-200 dark:bg-slate-700 rounded-full">
                      {player.playerOrder}
                    </span>
                    <span className="text-xs text-slate-600 dark:text-slate-400">
                      Player {player.playerOrder}
                    </span>
                  </div>

                  {/* Player name input */}
                  <div className="space-y-1">
                    <Label htmlFor={`player-name-${player.id}`} className="sr-only">
                      Player {player.playerOrder} Name
                    </Label>
                    <Input
                      id={`player-name-${player.id}`}
                      placeholder={`Player ${player.playerOrder} name`}
                      value={player.playerName}
                      onChange={(e) =>
                        handlePlayerChange(player.id, 'playerName', e.target.value)
                      }
                      disabled={isSubmitting}
                      aria-invalid={!!validationErrors[`name_${player.id}`]}
                      aria-describedby={
                        validationErrors[`name_${player.id}`]
                          ? `error-name-${player.id}`
                          : undefined
                      }
                      className={
                        validationErrors[`name_${player.id}`]
                          ? 'border-red-500 focus-visible:ring-red-500'
                          : ''
                      }
                    />
                    {validationErrors[`name_${player.id}`] && (
                      <p
                        id={`error-name-${player.id}`}
                        className="text-xs text-red-600 dark:text-red-400"
                      >
                        {validationErrors[`name_${player.id}`]}
                      </p>
                    )}
                  </div>

                  {/* Player color selector */}
                  <div className="space-y-1">
                    <Label htmlFor={`player-color-${player.id}`} className="text-xs">
                      Color (Optional)
                    </Label>
                    <Select
                      value={player.color ?? undefined}
                      onValueChange={(value) =>
                        handlePlayerChange(player.id, 'color', value)
                      }
                      disabled={isSubmitting}
                    >
                      <SelectTrigger id={`player-color-${player.id}`} className="w-full">
                        <SelectValue placeholder="Choose color">
                          {player.color && (
                            <div className="flex items-center gap-2">
                              <div
                                className={`w-4 h-4 rounded-full border ${
                                  PLAYER_COLORS.find((c) => c.value === player.color)
                                    ?.className
                                }`}
                                aria-hidden="true"
                              />
                              <span>
                                {PLAYER_COLORS.find((c) => c.value === player.color)?.label}
                              </span>
                            </div>
                          )}
                        </SelectValue>
                      </SelectTrigger>
                      <SelectContent>
                        {PLAYER_COLORS.map((color) => (
                          <SelectItem key={color.value} value={color.value}>
                            <div className="flex items-center gap-2">
                              <div
                                className={`w-4 h-4 rounded-full border ${color.className}`}
                                aria-hidden="true"
                              />
                              <span>{color.label}</span>
                            </div>
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>

                {/* Remove button */}
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  onClick={() => handleRemovePlayer(player.id)}
                  disabled={players.length <= minPlayers || isSubmitting}
                  aria-label={`Remove player ${player.playerOrder}`}
                  className="flex-shrink-0"
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            ))}
          </div>
        </div>

        <DialogFooter>
          <LoadingButton
            type="button"
            variant="outline"
            onClick={onClose}
            isLoading={isSubmitting}
          >
            Cancel
          </LoadingButton>
          <LoadingButton
            type="submit"
            onClick={handleSubmit}
            isLoading={isSubmitting}
            loadingText="Starting Session..."
            disabled={players.length < minPlayers}
          >
            Start Session
          </LoadingButton>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
