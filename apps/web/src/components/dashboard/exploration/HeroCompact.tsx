'use client';

interface HeroCompactProps {
  userName: string;
  gamesThisWeek: number;
  hoursPlayed: number;
  avgRating: number;
}

export function HeroCompact({ userName, gamesThisWeek, hoursPlayed, avgRating }: HeroCompactProps) {
  const hour = new Date().getHours();
  const greeting = hour < 12 ? 'Buongiorno' : hour < 18 ? 'Buon pomeriggio' : 'Buonasera';

  return (
    <div className="flex items-center justify-between gap-4 py-2">
      <h1 className="font-quicksand text-2xl font-bold text-foreground leading-tight">
        {greeting}, {userName}!
      </h1>

      <div className="flex items-center gap-4 shrink-0">
        <div className="text-center">
          <p className="font-quicksand text-lg font-bold text-foreground leading-none">
            {gamesThisWeek}
          </p>
          <p className="font-nunito text-[10px] text-muted-foreground mt-0.5">questa settimana</p>
        </div>

        <div className="w-px h-8 bg-border/40" aria-hidden />

        <div className="text-center">
          <p className="font-quicksand text-lg font-bold text-foreground leading-none">
            {hoursPlayed}h
          </p>
          <p className="font-nunito text-[10px] text-muted-foreground mt-0.5">ore giocate</p>
        </div>

        <div className="w-px h-8 bg-border/40" aria-hidden />

        <div className="text-center">
          <p className="font-quicksand text-lg font-bold text-foreground leading-none">
            ⭐ {avgRating.toFixed(1)}
          </p>
          <p className="font-nunito text-[10px] text-muted-foreground mt-0.5">media voti</p>
        </div>
      </div>
    </div>
  );
}
