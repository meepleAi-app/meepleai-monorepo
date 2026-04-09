'use client';

export interface LiveSessionPreview {
  id: string;
  gameName: string;
  locationName: string;
  playerCount: number;
  roundCurrent: number;
  roundTotal: number;
  startedMinutesAgo: number;
}

interface HeroLiveSessionProps {
  session: LiveSessionPreview | null;
  onContinue: () => void;
}

export function HeroLiveSession({ session, onContinue }: HeroLiveSessionProps) {
  if (!session) {
    return (
      <div className="mb-6 flex min-h-[180px] items-center justify-between gap-6 overflow-hidden rounded-3xl border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] p-8 shadow-[var(--shadow-warm-sm)]">
        <div>
          <h2 className="font-quicksand text-xl font-extrabold text-[var(--nh-text-primary)]">
            Nessuna partita in corso
          </h2>
          <p className="mt-1 text-sm text-[var(--nh-text-muted)]">
            Pronto per una nuova serata di gioco?
          </p>
        </div>
        <button
          type="button"
          onClick={onContinue}
          className="rounded-xl px-5 py-3 font-nunito text-sm font-bold text-white transition-all hover:-translate-y-0.5"
          style={{
            background: 'linear-gradient(135deg, hsl(25 95% 48%), hsl(25 95% 40%))',
            boxShadow: '0 2px 8px hsla(25, 95%, 45%, 0.35)',
          }}
        >
          ▶ Inizia nuova partita
        </button>
      </div>
    );
  }

  return (
    <div
      className="group relative mb-6 flex min-h-[220px] cursor-pointer gap-0 overflow-hidden rounded-3xl border border-[var(--nh-border-default)] shadow-[var(--shadow-warm-md)] transition-all duration-300 hover:-translate-y-1 hover:shadow-[var(--shadow-warm-xl)]"
      style={{
        background:
          'linear-gradient(135deg, hsla(240,60%,55%,0.12), hsla(262,83%,58%,0.08)), var(--nh-bg-elevated)',
      }}
      onClick={onContinue}
      role="button"
      tabIndex={0}
      onKeyDown={e => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onContinue();
        }
      }}
    >
      <div
        aria-hidden
        className="absolute left-0 top-0 bottom-0 w-[5px] z-10"
        style={{ background: 'hsl(240 60% 55%)' }}
      />
      <div
        className="relative hidden md:flex w-[300px] shrink-0 items-center justify-center text-[80px] overflow-hidden"
        style={{
          background: 'linear-gradient(135deg, hsl(240 55% 65%), hsl(262 80% 55%))',
        }}
        aria-hidden
      >
        🎯
      </div>
      <div className="relative z-[2] flex flex-1 flex-col justify-between p-7 md:p-8">
        <div>
          <span
            className="mb-2.5 inline-flex w-fit items-center gap-1.5 rounded-md px-2.5 py-1 font-quicksand text-[10px] font-extrabold uppercase tracking-wider text-white"
            style={{ background: 'hsl(240 60% 55%)' }}
          >
            🎯 Session
          </span>
          <h2 className="mb-1.5 font-quicksand text-[1.75rem] font-extrabold leading-tight text-[var(--nh-text-primary)]">
            Serata {session.gameName} · {session.locationName}
          </h2>
          <p className="mb-3.5 text-[0.92rem] text-[var(--nh-text-secondary)]">
            {session.playerCount} giocatori · round {session.roundCurrent} di {session.roundTotal} ·
            iniziata {session.startedMinutesAgo} min fa
          </p>
        </div>
        <div className="flex gap-2.5">
          <button
            type="button"
            onClick={e => {
              e.stopPropagation();
              onContinue();
            }}
            className="rounded-xl px-5 py-2.5 font-nunito text-[0.82rem] font-bold text-white transition-all hover:-translate-y-0.5"
            style={{
              background: 'linear-gradient(135deg, hsl(240 60% 55%), hsl(240 60% 42%))',
              boxShadow: '0 2px 8px hsla(240, 60%, 55%, 0.35)',
            }}
          >
            ▶ Continua partita
          </button>
        </div>
      </div>
      <div
        className="absolute right-7 top-6 flex items-center gap-1.5 rounded-full border px-2.5 py-1 font-nunito text-[0.7rem] font-extrabold uppercase tracking-wider"
        style={{
          background: 'hsla(140, 60%, 45%, 0.12)',
          borderColor: 'hsla(140, 60%, 45%, 0.25)',
          color: 'hsl(140 60% 30%)',
        }}
      >
        <span
          className="inline-block h-1.5 w-1.5 animate-pulse rounded-full"
          style={{ background: 'hsl(140 60% 45%)' }}
        />
        In corso
      </div>
    </div>
  );
}
