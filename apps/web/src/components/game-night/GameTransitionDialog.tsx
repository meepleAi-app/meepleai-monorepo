import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import type { GameNightGame } from '@/stores/game-night/types';

interface Props {
  open: boolean;
  completedGameTitle: string;
  winnerName?: string;
  availableGames: GameNightGame[];
  onSelectGame: (gameId: string, gameTitle: string) => void;
  onFinishNight: () => void;
  onClose: () => void;
}

export function GameTransitionDialog({
  open,
  completedGameTitle,
  winnerName,
  availableGames,
  onSelectGame,
  onFinishNight,
  onClose,
}: Props) {
  return (
    <Dialog open={open} onOpenChange={v => !v && onClose()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{completedGameTitle} completato!</DialogTitle>
        </DialogHeader>

        {winnerName && <p className="text-center text-lg font-semibold">Vincitore: {winnerName}</p>}

        {availableGames.length > 0 && (
          <div className="space-y-2 mt-4">
            <p className="text-sm text-muted-foreground">Prossimo gioco:</p>
            {availableGames.map(game => (
              <Button
                key={game.id}
                variant="outline"
                className="w-full justify-start"
                onClick={() => onSelectGame(game.id, game.title)}
              >
                {game.title}
              </Button>
            ))}
          </div>
        )}

        <DialogFooter className="mt-4">
          <Button variant="secondary" onClick={onFinishNight}>
            Termina serata
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
