'use client';

import { useRouter } from 'next/navigation';

interface WelcomeHeroProps {
  firstName: string;
}

export function WelcomeHero({ firstName }: WelcomeHeroProps) {
  const router = useRouter();

  return (
    <div
      className="col-span-6 lg:col-span-12 row-span-3 rounded-xl border overflow-hidden p-5 flex items-center gap-5 relative"
      style={{
        background:
          'linear-gradient(135deg, hsl(var(--e-game) / 0.12) 0%, hsl(var(--e-session) / 0.06) 100%)',
        borderColor: 'hsl(var(--e-game) / 0.3)',
      }}
    >
      {/* Cerchio decorativo */}
      <div
        className="absolute -top-10 -right-10 w-40 h-40 rounded-full pointer-events-none"
        style={{ background: 'hsl(var(--e-game) / 0.05)' }}
      />

      {/* Avatar iniziale */}
      <div
        className="w-12 h-12 rounded-full flex items-center justify-center font-quicksand font-extrabold text-xl text-white shrink-0"
        style={{
          background: 'linear-gradient(135deg, hsl(var(--e-player)), hsl(var(--e-game)))',
        }}
      >
        {firstName[0]?.toUpperCase() ?? 'M'}
      </div>

      {/* Testo e azioni */}
      <div className="flex-1 min-w-0 relative z-10">
        <p className="font-quicksand font-extrabold text-lg text-foreground">
          Benvenuto in MeepleAI, {firstName}! 👋
        </p>
        <p className="font-nunito text-sm text-muted-foreground mt-0.5 mb-3">
          Il tuo assistente AI per giochi da tavolo. Inizia aggiungendo i giochi che possiedi.
        </p>
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => router.push('/library?action=add')}
            className="font-nunito font-bold text-xs text-white px-4 py-2 rounded-full hover:opacity-90 transition-opacity"
            style={{ background: 'hsl(var(--e-game))' }}
          >
            ➕ Aggiungi un gioco
          </button>
          <button
            type="button"
            onClick={() => router.push('/library')}
            className="font-nunito font-bold text-xs text-muted-foreground px-4 py-2 rounded-full border border-border hover:bg-muted/30 transition-colors"
          >
            🔍 Sfoglia catalogo
          </button>
          <button
            type="button"
            onClick={() => router.push('/chat/new')}
            className="font-nunito font-bold text-xs text-muted-foreground px-4 py-2 rounded-full border border-border hover:bg-muted/30 transition-colors"
          >
            🤖 Prova l&apos;AI Chat
          </button>
        </div>
      </div>
    </div>
  );
}
