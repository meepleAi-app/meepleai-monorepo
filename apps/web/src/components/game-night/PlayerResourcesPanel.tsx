import { Button } from '@/components/ui/primitives/button';
import type { PlayerResource } from '@/stores/game-night/types';

interface Props {
  resources: PlayerResource[];
  onUpdate: (participantId: string, key: string, newValue: number) => void;
}

export function PlayerResourcesPanel({ resources, onUpdate }: Props) {
  if (resources.length === 0) return null;

  return (
    <div className="space-y-4">
      {resources.map(player => (
        <div key={player.participantId} className="space-y-1">
          <h4 className="text-sm font-semibold">{player.playerName}</h4>
          <div className="flex flex-wrap gap-2">
            {Object.entries(player.resources).map(([key, value]) => (
              <div key={key} className="flex items-center gap-1 bg-muted rounded-md px-2 py-1">
                <span className="text-xs text-muted-foreground">{key}</span>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-5 w-5 text-xs"
                  onClick={() => onUpdate(player.participantId, key, Math.max(0, value - 1))}
                  aria-label={`Decrementa ${key}`}
                >
                  -
                </Button>
                <span className="text-sm font-mono w-6 text-center">{value}</span>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-5 w-5 text-xs"
                  onClick={() => onUpdate(player.participantId, key, value + 1)}
                  aria-label={`Incrementa ${key}`}
                >
                  +
                </Button>
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
