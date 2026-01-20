import { type FormEvent, useState } from 'react';

import { CheckCircle2 } from 'lucide-react';

import { LoadingButton } from '@/components/loading';
import { Card } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Separator } from '@/components/ui/navigation/separator';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

interface GameSummary {
  id: string;
  title: string;
  createdAt: string;
}

interface GamePickerProps {
  games: GameSummary[];
  selectedGameId: string | null;
  onGameSelect: (gameId: string) => void;
  onGameCreate: (name: string) => Promise<void>;
  loading?: boolean;
}

/**
 * GamePicker - Game selection and creation component
 *
 * Features:
 * - Select from existing games
 * - Create new games with validation
 * - Loading states
 * - Design system compliance
 * - Accessible forms
 */
export function GamePicker({
  games,
  selectedGameId,
  onGameSelect,
  onGameCreate,
  loading = false,
}: GamePickerProps) {
  const [newGameName, setNewGameName] = useState('');
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleCreate = async (e: FormEvent) => {
    e.preventDefault();

    if (!newGameName.trim()) {
      setError('Game name cannot be empty');
      return;
    }

    if (newGameName.trim().length < 2) {
      setError('Game name must be at least 2 characters');
      return;
    }

    setCreating(true);
    setError(null);

    try {
      await onGameCreate(newGameName.trim());
      setNewGameName('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create game');
    } finally {
      setCreating(false);
    }
  };

  const selectedGame = games.find(g => g.id === selectedGameId);

  return (
    <Card className="p-4 space-y-4">
      <div>
        <Label htmlFor="game-select">Select Game</Label>
        <Select
          value={selectedGameId ?? ''}
          onValueChange={onGameSelect}
          disabled={loading || creating}
        >
          <SelectTrigger id="game-select">
            <SelectValue placeholder="Choose a game..." />
          </SelectTrigger>
          <SelectContent>
            {games.map(game => (
              <SelectItem key={game.id} value={game.id}>
                {game.title}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <Separator />

      <form onSubmit={handleCreate} className="space-y-3">
        <Label htmlFor="new-game">Create New Game</Label>
        <div className="flex gap-2">
          <Input
            id="new-game"
            value={newGameName}
            onChange={e => {
              setNewGameName(e.target.value);
              setError(null);
            }}
            placeholder="e.g., Gloomhaven"
            disabled={creating}
            className="flex-1"
          />
          <LoadingButton
            type="submit"
            isLoading={creating}
            disabled={!newGameName.trim() || creating}
          >
            Create
          </LoadingButton>
        </div>
        {error && <p className="text-sm text-destructive">{error}</p>}
      </form>

      {selectedGame && (
        <Alert>
          <CheckCircle2 className="h-4 w-4" />
          <AlertDescription>
            Selected: <span className="font-semibold">{selectedGame.title}</span>
          </AlertDescription>
        </Alert>
      )}
    </Card>
  );
}
