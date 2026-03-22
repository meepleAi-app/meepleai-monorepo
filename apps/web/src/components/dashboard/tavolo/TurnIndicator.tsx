'use client';

interface TurnIndicatorProps {
  playerName: string;
  playerColor: string;
  turnElapsed?: number;
}

export function TurnIndicator({ playerName, playerColor, turnElapsed }: TurnIndicatorProps) {
  return (
    <div
      data-testid="turn-indicator"
      className="flex items-center gap-3 rounded-xl border border-border/30 bg-card/60 px-3 py-2 backdrop-blur-sm"
    >
      {/* Color accent dot */}
      <div
        className="h-3 w-3 shrink-0 rounded-full"
        style={{ backgroundColor: playerColor }}
        aria-hidden="true"
      />

      {/* Turn label */}
      <div className="flex-1">
        <p className="font-nunito text-xs text-muted-foreground">Turno di</p>
        <p className="font-quicksand text-sm font-bold" style={{ color: playerColor }}>
          {playerName}
        </p>
      </div>

      {/* Optional timer */}
      {turnElapsed !== undefined && (
        <p
          data-testid="turn-elapsed"
          className="font-quicksand text-sm font-bold text-muted-foreground"
        >
          {turnElapsed}s
        </p>
      )}
    </div>
  );
}
