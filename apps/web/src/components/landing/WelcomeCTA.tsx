import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';

export function WelcomeCTA() {
  return (
    <section aria-label="Call to action" className="px-4 py-20 text-center">
      <h2 className="mb-4 text-3xl font-bold text-foreground">
        Pronto per la prossima serata giochi?
      </h2>
      <p className="mb-8 text-lg text-muted-foreground">
        Registrati gratis e prepara il tuo primo agente AI in 5 minuti
      </p>
      <div className="flex flex-col items-center gap-3 sm:flex-row sm:justify-center">
        <Button asChild size="lg">
          <Link href="/register">Inizia gratis</Link>
        </Button>
        <Button asChild variant="outline" size="lg">
          <Link href="/register">Esplora il catalogo</Link>
        </Button>
      </div>
    </section>
  );
}
