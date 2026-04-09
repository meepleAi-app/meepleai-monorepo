'use client';

export interface LibraryHeaderStats {
  owned: number;
  catalog: number;
  wishlist: number;
}

interface LibraryHeaderProps {
  stats: LibraryHeaderStats;
}

interface StatCardData {
  key: keyof LibraryHeaderStats;
  label: string;
  color: string;
}

const STAT_CARDS: StatCardData[] = [
  { key: 'owned', label: 'Tuoi giochi', color: 'hsl(25 95% 38%)' },
  { key: 'catalog', label: 'Catalogo', color: 'hsl(240 60% 45%)' },
  { key: 'wishlist', label: 'Wishlist', color: 'hsl(38 92% 42%)' },
];

export function LibraryHeader({ stats }: LibraryHeaderProps) {
  return (
    <div className="mb-6 flex items-end justify-between gap-6 border-b border-[var(--nh-border-default)] pb-5">
      <div>
        <h1 className="flex items-center gap-3.5 font-quicksand text-[2rem] font-extrabold leading-tight tracking-tight text-[var(--nh-text-primary)]">
          <span
            aria-hidden
            className="inline-block h-8 w-1.5 rounded-sm"
            style={{
              background: 'linear-gradient(180deg, hsl(25 95% 52%), hsl(25 95% 38%))',
            }}
          />
          La tua libreria
        </h1>
        <p className="ml-5 mt-1.5 text-sm text-[var(--nh-text-muted)]">
          Personal collection · Community catalog · Lista desideri · Continua a giocare
        </p>
      </div>
      <div className="flex gap-4">
        {STAT_CARDS.map(card => (
          <div
            key={card.key}
            className="min-w-[110px] cursor-pointer rounded-2xl border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] px-5 py-3 text-center shadow-[var(--shadow-warm-sm)] transition-all duration-200 hover:-translate-y-0.5 hover:shadow-[var(--shadow-warm-md)]"
          >
            <div
              className="font-quicksand text-[1.55rem] font-extrabold leading-none"
              style={{ color: card.color }}
            >
              {stats[card.key]}
            </div>
            <div className="mt-1 text-[0.68rem] font-bold uppercase tracking-wider text-[var(--nh-text-muted)]">
              {card.label}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
