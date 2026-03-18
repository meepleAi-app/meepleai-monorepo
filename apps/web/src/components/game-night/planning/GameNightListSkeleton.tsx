export function GameNightListSkeleton() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      {Array.from({ length: 3 }).map((_, i) => (
        <div
          key={i}
          data-testid="game-night-skeleton"
          className="rounded-xl border border-border bg-card p-4 animate-pulse"
        >
          <div className="h-5 w-2/3 bg-muted rounded mb-3" />
          <div className="space-y-2">
            <div className="h-4 w-1/2 bg-muted rounded" />
            <div className="h-4 w-1/3 bg-muted rounded" />
          </div>
        </div>
      ))}
    </div>
  );
}
