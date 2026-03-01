import Link from 'next/link';
import { ArrowLeft, Search } from 'lucide-react';

export default function DiscoverGameNotFound() {
  return (
    <div className="min-h-screen bg-background">
      <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6">
        <Link
          href="/discover"
          className="inline-flex items-center gap-2 text-sm font-medium text-muted-foreground hover:text-foreground transition-colors"
        >
          <ArrowLeft className="h-4 w-4" />
          Torna a Scopri
        </Link>

        <div className="mt-20 flex flex-col items-center gap-4 text-center">
          <div className="flex h-16 w-16 items-center justify-center rounded-full bg-muted">
            <Search className="h-8 w-8 text-muted-foreground" />
          </div>
          <h1 className="text-2xl font-bold text-foreground">Gioco non trovato</h1>
          <p className="max-w-md text-sm text-muted-foreground">
            Questo gioco non è disponibile nel catalogo condiviso. Potrebbe essere stato rimosso o
            l&apos;ID non è corretto.
          </p>
          <div className="mt-4 flex flex-wrap items-center justify-center gap-3">
            <Link
              href="/discover"
              className="inline-flex items-center gap-2 rounded-lg bg-orange-500 px-4 py-2 text-sm font-medium text-white hover:bg-orange-600 transition-colors"
            >
              <Search className="h-4 w-4" />
              Cerca nel catalogo
            </Link>
            <Link
              href="/library"
              className="inline-flex items-center gap-2 rounded-lg border border-input bg-background px-4 py-2 text-sm font-medium text-foreground hover:bg-accent transition-colors"
            >
              Vai alla mia libreria
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
