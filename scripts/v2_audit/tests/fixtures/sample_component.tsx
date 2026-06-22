import Link from "next/link";

export function GameDetailHero({ gameId, title }: { gameId: string; title: string }) {
  return (
    <section className="bg-card border-border">
      <header>
        <h1 className="text-foreground">{title}</h1>
      </header>
      <Link href="/games" className="text-muted-foreground">Back to games</Link>
      <button className="bg-entity-game">
        <Link href="/games/play">Avvia libro game</Link>
      </button>
    </section>
  );
}
