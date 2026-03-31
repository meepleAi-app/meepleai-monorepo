import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';

const EXAMPLE_QUESTIONS = [
  '🎲 "Posso usare una carta azione già nella mano del turno precedente?"',
  '🃏 "Cosa succede se il mazzo si esaurisce durante la pescata?"',
  '🏰 "Un attacco con 2 guerrieri contro 1 — chi vince a parità di forza?"',
  '⚔️ "Si possono giocare più carte evento nello stesso turno?"',
];

export function RulesQuickDemo() {
  return (
    <section aria-labelledby="rules-demo-heading" className="px-4 py-16 bg-background">
      <div className="mx-auto max-w-3xl text-center">
        <h2 id="rules-demo-heading" className="mb-3 text-2xl font-bold text-foreground">
          Chiedi una regola
        </h2>
        <p className="mb-8 text-muted-foreground">
          Come se avessi un esperto del gioco sempre al tavolo con te.
        </p>

        <div className="mb-8 grid gap-3 text-left">
          {EXAMPLE_QUESTIONS.map((q, i) => (
            <button
              key={i}
              type="button"
              disabled
              className="rounded-xl border border-border/60 bg-muted/30 px-4 py-3 text-sm text-foreground/80 text-left cursor-default"
              aria-label={`Esempio di domanda: ${q}`}
            >
              {q}
            </button>
          ))}
        </div>

        <Button asChild size="lg">
          <Link href="/register">Prova gratis — risposta in 10 secondi</Link>
        </Button>
      </div>
    </section>
  );
}
