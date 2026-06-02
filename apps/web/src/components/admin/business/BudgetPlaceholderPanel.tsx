'use client';

/**
 * #1838 SP5 F4-C5 — BudgetPlaceholderPanel
 *
 * Componente generico per i 4 pannelli SP5 mockup non ancora wired al BE:
 *   - CostStackedArea (mockup §2)
 *   - FeatureCostTable (mockup §3)
 *   - CostSimulator (mockup §4)
 *   - BudgetGauge (mockup §5)
 *
 * Mostra un placeholder con titolo, descrizione "Coming soon", e una nota
 * tecnica sull'endpoint BE da implementare.
 */

export interface BudgetPlaceholderPanelProps {
  readonly id: string;
  readonly title: string;
  readonly description: string;
  readonly endpoint: string;
}

export function BudgetPlaceholderPanel({
  id,
  title,
  description,
  endpoint,
}: BudgetPlaceholderPanelProps) {
  return (
    <section
      className="rounded-lg border border-border/60 dark:border-zinc-700/60 bg-card/80 dark:bg-zinc-900/80 p-6"
      aria-labelledby={`${id}-heading`}
      data-testid={`budget-placeholder-${id}`}
    >
      <header className="mb-3 flex items-center gap-2">
        <h3 id={`${id}-heading`} className="font-quicksand text-base font-bold text-foreground">
          {title}
        </h3>
        <span className="inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border bg-zinc-500/10 text-zinc-700 dark:text-zinc-300 border-zinc-500/30">
          BE pending
        </span>
      </header>

      <p className="text-sm text-muted-foreground">{description}</p>

      <p className="mt-3 font-mono text-[10.5px] text-muted-foreground">
        Endpoint richiesto: <code className="bg-muted/40 px-1.5 py-0.5 rounded">{endpoint}</code>
      </p>
    </section>
  );
}
