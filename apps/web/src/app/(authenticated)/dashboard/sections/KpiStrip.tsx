'use client';

export interface KpiValues {
  games: number;
  sessions: number;
  friends: number;
  chats: number;
}

interface KpiStripProps {
  kpis: KpiValues;
}

interface KpiCardData {
  key: keyof KpiValues;
  label: string;
  icon: string;
  entity: 'game' | 'session' | 'player' | 'chat';
}

const CARDS: KpiCardData[] = [
  { key: 'games', label: 'Giochi in libreria', icon: '🎲', entity: 'game' },
  { key: 'sessions', label: 'Sessioni totali', icon: '🎯', entity: 'session' },
  { key: 'friends', label: 'Amici preferiti', icon: '👥', entity: 'player' },
  { key: 'chats', label: 'Chat con agente', icon: '💬', entity: 'chat' },
];

const ENTITY_BG: Record<KpiCardData['entity'], string> = {
  game: 'hsla(25, 95%, 45%, 0.12)',
  session: 'hsla(240, 60%, 55%, 0.12)',
  player: 'hsla(262, 83%, 58%, 0.12)',
  chat: 'hsla(220, 80%, 55%, 0.12)',
};

const ENTITY_FG: Record<KpiCardData['entity'], string> = {
  game: 'hsl(25 95% 38%)',
  session: 'hsl(240 60% 45%)',
  player: 'hsl(262 83% 50%)',
  chat: 'hsl(220 80% 45%)',
};

export function KpiStrip({ kpis }: KpiStripProps) {
  return (
    <div className="mb-7 grid grid-cols-4 gap-3.5">
      {CARDS.map(card => (
        <div
          key={card.key}
          className="relative overflow-hidden rounded-2xl border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] p-5 shadow-[var(--shadow-warm-sm)] transition-all duration-300 hover:-translate-y-0.5 hover:shadow-[var(--shadow-warm-md)]"
        >
          <div
            className="mb-3 flex h-10 w-10 items-center justify-center rounded-xl text-lg"
            style={{ background: ENTITY_BG[card.entity], color: ENTITY_FG[card.entity] }}
            aria-hidden
          >
            {card.icon}
          </div>
          <div className="text-[0.7rem] font-bold uppercase tracking-wider text-[var(--nh-text-muted)]">
            {card.label}
          </div>
          <div className="mt-1 font-quicksand text-2xl font-extrabold leading-none text-[var(--nh-text-primary)]">
            {kpis[card.key]}
          </div>
        </div>
      ))}
    </div>
  );
}
