import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';

export function WelcomeHero() {
  return (
    <section
      aria-label="Hero"
      className="flex min-h-[80vh] flex-col items-center justify-center px-4 py-20 text-center"
    >
      <p className="mb-4 text-sm uppercase tracking-widest text-muted-foreground">
        Il tuo compagno di gioco AI
      </p>
      <h1 className="mb-4 max-w-2xl text-4xl font-extrabold leading-tight text-foreground sm:text-5xl lg:text-6xl">
        Ogni serata giochi merita un arbitro
      </h1>
      <p className="mb-10 max-w-lg text-lg text-muted-foreground">
        Setup, regole, punteggi, dispute — un agente AI che conosce il tuo gioco e vi aiuta al
        tavolo.
      </p>
      <div className="flex flex-col gap-3 sm:flex-row">
        <Button asChild size="lg">
          <Link href="/register">Inizia gratis</Link>
        </Button>
        <Button asChild variant="outline" size="lg">
          <Link href="#come-funziona">Scopri come funziona ↓</Link>
        </Button>
      </div>
    </section>
  );
}
