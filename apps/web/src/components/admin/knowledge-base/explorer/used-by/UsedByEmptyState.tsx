'use client';

export function UsedByEmptyState() {
  return (
    <div
      data-testid="used-by-empty"
      className="border border-border/60 dark:border-zinc-700/60 rounded-lg bg-card/80 dark:bg-zinc-900/80 p-8 text-center text-sm text-muted-foreground min-h-[200px] flex flex-col items-center justify-center gap-2"
    >
      <span aria-hidden="true" className="text-3xl">
        🧑‍🤝‍🧑
      </span>
      <p>Nessun agent consuma questo documento.</p>
      <p className="text-xs">Aggiungi il documento alla KB di un agent per vederlo apparire qui.</p>
    </div>
  );
}
