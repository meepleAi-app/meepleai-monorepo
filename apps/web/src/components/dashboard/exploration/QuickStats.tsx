'use client';

interface QuickStatsProps {
  totalGames: number;
  totalSessions: number;
  avgRating: number;
}

export function QuickStats({ totalGames, totalSessions, avgRating }: QuickStatsProps) {
  const stats = [
    { icon: '🎲', value: totalGames, label: 'Giochi in libreria' },
    { icon: '🎯', value: totalSessions, label: 'Sessioni totali' },
    { icon: '⭐', value: avgRating.toFixed(1), label: 'Voto medio' },
  ] as const;

  return (
    <div className="grid grid-cols-3 gap-3">
      {stats.map(({ icon, value, label }) => (
        <div
          key={label}
          className="flex flex-col items-center justify-center gap-1 rounded-xl px-3 py-4 bg-[rgba(255,255,255,0.75)] dark:bg-[rgba(30,27,24,0.75)] backdrop-blur-md border border-[rgba(200,180,160,0.20)] dark:border-[rgba(100,90,75,0.25)] shadow-[0_2px_12px_rgba(180,120,60,0.06)]"
        >
          <span className="text-2xl leading-none" role="img" aria-label={label}>
            {icon}
          </span>
          <p className="font-quicksand text-xl font-bold text-foreground leading-none">{value}</p>
          <p className="font-nunito text-[10px] text-muted-foreground text-center leading-tight">
            {label}
          </p>
        </div>
      ))}
    </div>
  );
}
