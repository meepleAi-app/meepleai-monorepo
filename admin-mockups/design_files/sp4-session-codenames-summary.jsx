/* MeepleAI SP4 · Codenames — SUMMARY (post-game)
   /sessions/[id]/summary  — Codenames recap.

   Hero winner banner (Red / Blue / Assassin defeat) + 4 tabs:
     Scoreboard (Ranking) · Final Board (full reveal) · Clue Analysis · Stats.
   Reuses skeleton/flavor atoms (OutcomeBadge · ClueChip · ClueOutcome ·
   RoleAvatar · WordCard). Dark = primary.

   Loads: sp4-parts-common.jsx · sp4-session-codenames-data.jsx ·
          sp4-session-skeleton-renderers.jsx · sp4-session-skeleton-parts.jsx ·
          sp4-session-codenames-flavor.jsx · sp4-session-codenames-parts.jsx ·
          sp4-session-codenames-bodies.jsx */

const S = window.SkeletonParts;
const R = window.SkeletonRenderers;
const M = window.MAI;
const CN = window.CN;
const P = window.CNParts;
const F = window.CNFlavor;
const eHsl = M.entityHsl;
const { useState } = React;
const { mono, disp, teamEntity, teamName, RoleAvatar, ClueChip, ClueOutcome } = F;
const SUM = CN.summary;

// ─── Hero — winner banner ───────────────────────────────────────────────────
const RESULTS = {
  red:      { winner: 'red',  title: 'Squadra Rossa vince', icon: '🏆', e: 'event', cause: 'Ha rivelato tutti i 9 agenti', kind: 'agents' },
  blue:     { winner: 'blue', title: 'Squadra Blu vince',   icon: '🏆', e: 'chat',  cause: 'Ha rivelato tutti gli 8 agenti', kind: 'agents' },
  assassin: { winner: 'blue', title: 'Sconfitta · Assassino', icon: '💀', e: null,  cause: 'Il Rosso ha rivelato l\u2019assassino — sconfitta immediata', kind: 'assassin' },
};

const SummaryHero = ({ result = RESULTS.red, duration = SUM.duration, rounds = SUM.rounds, compact }) => {
  const dark = result.kind === 'assassin';
  const accent = result.e ? eHsl(result.e) : '#fff';
  const bg = dark
    ? 'repeating-linear-gradient(45deg, #0f0c08, #0f0c08 10px, #1a140d 10px, #1a140d 20px)'
    : `linear-gradient(135deg, ${eHsl(result.e, 0.9)}, ${eHsl(result.e, 0.55)})`;
  return (
    <div role="status" style={{
      position: 'relative', overflow: 'hidden', borderRadius: 'var(--r-xl)', padding: compact ? '20px 18px' : '30px 28px',
      background: bg, border: dark ? `1.5px solid ${eHsl('event', 0.6)}` : 'none',
      boxShadow: result.e ? `0 12px 36px ${eHsl(result.e, 0.32)}` : 'var(--shadow-lg)', color: '#fff',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: compact ? 14 : 20, flexWrap: 'wrap' }}>
        <div aria-hidden="true" style={{ fontSize: compact ? 40 : 54, lineHeight: 1, filter: 'drop-shadow(0 3px 8px rgba(0,0,0,.3))' }}>{result.icon}</div>
        <div style={{ minWidth: 0, flex: 1 }}>
          <div style={{ ...mono(compact ? 9 : 10, 800, 'rgba(255,255,255,.75)'), textTransform: 'uppercase', letterSpacing: '.12em', marginBottom: 4 }}>Esito partita · Codenames</div>
          <div style={{ ...disp(compact ? 24 : 34, 800, '#fff'), letterSpacing: '-0.01em' }}>{result.title}</div>
          <div style={{ ...disp(compact ? 13 : 15, 600, 'rgba(255,255,255,.92)'), marginTop: 4 }}>{result.cause}</div>
        </div>
      </div>
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: compact ? 14 : 18 }}>
        {[['⏱', 'Durata', duration], ['🔁', 'Round', rounds], ['🗝', 'Assassino', SUM.assassinAvoided && result.kind !== 'assassin' ? 'evitato' : 'rivelato']].map(([ic, lb, v]) => (
          <span key={lb} style={{ display: 'inline-flex', alignItems: 'center', gap: 7, padding: '6px 12px', borderRadius: 'var(--r-pill)', background: 'rgba(0,0,0,.22)', border: '1px solid rgba(255,255,255,.22)' }}>
            <span aria-hidden="true">{ic}</span>
            <span style={{ ...mono(9, 800, 'rgba(255,255,255,.7)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>{lb}</span>
            <span style={{ ...disp(13, 800, '#fff'), fontVariantNumeric: 'tabular-nums' }}>{v}</span>
          </span>
        ))}
      </div>
    </div>
  );
};

// ─── Scoreboard tab — team results (Ranking) ────────────────────────────────
const TeamResultRow = ({ teamId, rank, winner }) => {
  const t = SUM.teams[teamId];
  const e = teamEntity(teamId);
  const isWin = teamId === winner;
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '12px 14px', borderRadius: 'var(--r-lg)', background: isWin ? eHsl(e, 0.1) : 'var(--bg-card)', border: `${isWin ? 2 : 1}px solid ${eHsl(e, isWin ? 0.5 : 0.28)}` }}>
      <span style={{ width: 30, height: 30, borderRadius: 'var(--r-pill)', flexShrink: 0, background: isWin ? eHsl(e) : eHsl(e, 0.14), color: isWin ? '#fff' : eHsl(e), display: 'flex', alignItems: 'center', justifyContent: 'center', ...disp(14, 800) }}>{rank}</span>
      <span aria-hidden="true" style={{ width: 12, height: 12, borderRadius: 3, background: eHsl(e), flexShrink: 0 }} />
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ ...disp(15, 800, 'var(--text)') }}>Squadra {t.name}</div>
        <div style={{ ...mono(10, 700, 'var(--text-muted)') }}>{t.found} / {t.total} agenti rivelati</div>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 4 }}>
        <span style={{ ...disp(22, 800, eHsl(e)), fontVariantNumeric: 'tabular-nums' }}>{t.found}<span style={{ ...mono(11, 700, 'var(--text-muted)') }}>/{t.total}</span></span>
        {isWin && <span style={{ fontSize: 17 }} aria-hidden="true">🏆</span>}
      </div>
    </div>
  );
};
const ScoreboardTab = ({ winner }) => {
  const ranked = winner === 'red' ? ['red', 'blue'] : ['blue', 'red'];
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <span style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>Classifica finale</span>
        <div style={{ flex: 1 }} />
        <span style={{ ...mono(9, 800, eHsl('session')), padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHsl('session', 0.12), border: `1px solid ${eHsl('session', 0.3)}` }}>scoreType · Ranking</span>
      </div>
      {ranked.map((id, i) => <TeamResultRow key={id} teamId={id} rank={i + 1} winner={winner} />)}
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px', borderRadius: 'var(--r-md)', background: eHsl('toolkit', 0.08), border: `1px solid ${eHsl('toolkit', 0.3)}` }}>
        <span aria-hidden="true" style={{ fontSize: 16 }}>🗝</span>
        <span style={{ ...disp(12.5, 800, 'var(--text)') }}>Assassino evitato</span>
        <div style={{ flex: 1 }} />
        <span style={{ ...mono(10, 700, eHsl('toolkit')) }}>nessuna squadra ha toccato la tessera nera</span>
      </div>
    </div>
  );
};

// ─── Final Board tab — full reveal ──────────────────────────────────────────
const FinalBoardTab = ({ compact }) => {
  const counts = { red: 0, blue: 0, neutral: 0, assassin: 0 };
  CN.BOARD.forEach((c) => counts[c.key]++);
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
        <span style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>Griglia finale · tutte rivelate</span>
        <div style={{ flex: 1 }} />
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          {[['red', 'event', 'Rossi'], ['blue', 'chat', 'Blu'], ['neutral', null, 'Civili'], ['assassin', null, 'Assassino']].map(([k, e, lb]) => (
            <span key={k} style={{ display: 'inline-flex', alignItems: 'center', gap: 5, ...mono(9, 700, 'var(--text-sec)') }}>
              <span style={{ width: 11, height: 11, borderRadius: 2, background: e ? eHsl(e) : k === 'assassin' ? '#0f0c08' : 'var(--bg-sunken)', border: '1px solid var(--border-strong)' }} />{lb} {counts[k]}
            </span>
          ))}
        </div>
      </div>
      <div role="grid" aria-label="Griglia finale 5×5" style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: compact ? 5 : 7 }}>
        {SUM.finalBoard.map((cell, i) => <P.WordCard key={i} cell={cell} compact={compact} />)}
      </div>
    </div>
  );
};

// ─── Clue Analysis tab ──────────────────────────────────────────────────────
const ClueAnalysisTab = () => {
  const clues = SUM.clues;
  const best = clues.find((c) => c.best);
  const worst = clues.find((c) => c.worst);
  const byTeam = (team) => clues.filter((c) => c.team === team);
  const Highlight = ({ label, clue, tone }) => clue && (
    <div style={{ flex: 1, minWidth: 180, padding: 12, borderRadius: 'var(--r-lg)', background: eHsl(tone, 0.08), border: `1px solid ${eHsl(tone, 0.3)}` }}>
      <div style={{ ...mono(9, 800, eHsl(tone)), textTransform: 'uppercase', letterSpacing: '.06em', marginBottom: 7 }}>{label}</div>
      <ClueChip word={clue.word} number={clue.number} team={clue.team} />
      <div style={{ ...mono(10, 700, 'var(--text-sec)'), marginTop: 7 }}>{clue.correct}/{clue.attempts} corretti · Spymaster {CN.byId(clue.spymaster).name}</div>
    </div>
  );
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
        <Highlight label="★ Miglior indizio" clue={best} tone="toolkit" />
        <Highlight label="Indizio peggiore" clue={worst} tone="event" />
      </div>
      {['red', 'blue'].map((team) => {
        const list = byTeam(team);
        const e = teamEntity(team);
        const corr = list.reduce((s, c) => s + c.correct, 0);
        const att = list.reduce((s, c) => s + c.attempts, 0);
        return (
          <div key={team} style={{ borderRadius: 'var(--r-lg)', overflow: 'hidden', border: `1px solid ${eHsl(e, 0.28)}`, background: 'var(--bg-card)' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px', background: eHsl(e, 0.08), borderBottom: `1px solid ${eHsl(e, 0.2)}` }}>
              <span aria-hidden="true" style={{ width: 11, height: 11, borderRadius: 3, background: eHsl(e) }} />
              <span style={{ ...disp(13, 800, 'var(--text)') }}>Spymaster {CN.byId(list[0].spymaster).name} · {teamName(team)}</span>
              <div style={{ flex: 1 }} />
              <span style={{ ...mono(10, 800, eHsl(e)) }}>{corr}/{att} corretti</span>
            </div>
            <div style={{ padding: '4px 12px' }}>
              {list.map((c) => (
                <div key={c.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 0', borderBottom: '1px solid var(--border-light)', flexWrap: 'wrap', rowGap: 6 }}>
                  <ClueChip word={c.word} number={c.number} team={c.team} dim />
                  {c.best && <span style={{ ...mono(8.5, 800, eHsl('toolkit')) }}>★</span>}
                  <div style={{ flex: 1 }} />
                  <span style={{ ...mono(11, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{c.correct}/{c.number}</span>
                  <ClueOutcome outcome={c.outcome} />
                </div>
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );
};

// ─── Stats tab ──────────────────────────────────────────────────────────────
const StatsTab = () => {
  const s = SUM.stats;
  const cards = [
    { e: 'session', lb: 'Durata totale', v: SUM.duration, sub: `${SUM.rounds} round` },
    { e: 'player',  lb: 'Durata media / turno', v: s.avgTurn, sub: 'mm:ss' },
    { e: 'agent',   lb: 'Miss rate', v: `${Math.round(s.missRate * 100)}%`, sub: 'tentativi sbagliati' },
    { e: 'event',   lb: 'Assassino toccato', v: s.assassinTouched, sub: 'volte' },
    { e: 'toolkit', lb: 'Clue density', v: s.clueDensity, sub: 'parole / indizio' },
    { e: 'kb',      lb: 'Tessere rivelate', v: `${s.tilesRevealed}/25`, sub: `${s.neutralsHit} civili` },
  ];
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 10 }}>
      {cards.map((c) => (
        <div key={c.lb} style={{ padding: 14, borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: `1px solid ${eHsl(c.e, 0.28)}`, borderLeft: `3px solid ${eHsl(c.e)}` }}>
          <div style={{ ...mono(8.5, 800, eHsl(c.e)), textTransform: 'uppercase', letterSpacing: '.06em' }}>{c.lb}</div>
          <div style={{ ...disp(26, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums', margin: '4px 0 2px' }}>{c.v}</div>
          <div style={{ ...mono(9.5, 700, 'var(--text-muted)') }}>{c.sub}</div>
        </div>
      ))}
    </div>
  );
};

// ─── SummaryView — hero + tabs ──────────────────────────────────────────────
const SUM_TABS = [
  { id: 'scoreboard', icon: '🎯', label: 'Scoreboard', entity: 'session' },
  { id: 'board',      icon: '🗂', label: 'Griglia',    entity: 'event' },
  { id: 'clues',      icon: '💬', label: 'Indizi',     entity: 'agent' },
  { id: 'stats',      icon: '📊', label: 'Stats',      entity: 'toolkit' },
];
const SummaryView = ({ result = RESULTS.red, compact, initialTab = 'scoreboard' }) => {
  const [tab, setTab] = useState(initialTab);
  return (
    <div className="mai-cb-scroll" style={{ flex: 1, minHeight: 0, overflowY: 'auto', background: 'var(--bg)', padding: compact ? 12 : 18, display: 'flex', flexDirection: 'column', gap: 14 }}>
      <SummaryHero result={result} compact={compact} />
      <div role="tablist" aria-label="Sezioni recap" style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
        {SUM_TABS.map((t) => {
          const on = tab === t.id;
          return (
            <button key={t.id} type="button" role="tab" aria-selected={on} onClick={() => setTab(t.id)} style={{ display: 'inline-flex', alignItems: 'center', gap: 6, padding: '7px 13px', borderRadius: 'var(--r-pill)', background: on ? eHsl(t.entity, 0.14) : 'var(--bg-card)', border: `1px solid ${on ? eHsl(t.entity, 0.45) : 'var(--border)'}`, color: on ? eHsl(t.entity) : 'var(--text-sec)', ...disp(11.5, 800), cursor: 'pointer' }}>
              <span aria-hidden="true">{t.icon}</span>{t.label}
            </button>
          );
        })}
      </div>
      <div role="tabpanel">
        {tab === 'scoreboard' && <ScoreboardTab winner={result.winner} />}
        {tab === 'board' && <FinalBoardTab compact={compact} />}
        {tab === 'clues' && <ClueAnalysisTab />}
        {tab === 'stats' && <StatsTab />}
      </div>
    </div>
  );
};

// ─── PhoneSummary ───────────────────────────────────────────────────────────
const PhoneSummary = ({ label, dark, result, initialTab }) => (
  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
    <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark</span>}</div>
    <div className="phone" data-theme={dark ? 'dark' : undefined}>
      <div className="phone-sbar" style={{ color: 'var(--text)' }}><span style={{ fontFamily: 'var(--f-mono)' }}>20:31</span><div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div></div>
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', position: 'relative', overflow: 'hidden', minHeight: 0 }}>
        <S.TopBar ds={CN.ds} compact connection="connected" />
        <SummaryView result={result} compact initialTab={initialTab} />
      </div>
    </div>
  </div>
);

function App() {
  return (
    <div className="stage">
      <S.ThemeToggle />
      <div className="stage-wrap">
        <div style={{ ...mono(11, 600, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Codenames · Session summary 🏆</div>
        <h1>Codenames — recap partita</h1>
        <p className="lead">
          Post-partita per <code>/sessions/[id]/summary</code>: hero del vincitore (Rosso / Blu / sconfitta Assassino) e quattro tab —
          <strong> Scoreboard</strong> (Ranking), <strong>Griglia finale</strong> (reveal totale), <strong>Analisi indizi</strong> (success rate, migliore/peggiore) e <strong>Stats</strong>.
        </p>

        <div className="section-label">Hero · 3 esiti — Rosso vince · Blu vince · sconfitta Assassino</div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 14, marginBottom: 8 }}>
          <SummaryHero result={RESULTS.red} compact />
          <SummaryHero result={RESULTS.blue} compact />
          <SummaryHero result={RESULTS.assassin} compact />
        </div>

        <div className="section-label">Desktop 1280 — recap completo · cambia tab</div>
        <S.DesktopFrame ds={CN.ds} label="Recap · Rosso vince" url="meepleai.app/sessions/cdn-7/summary" height={720}
          desc="Hero + tab Scoreboard / Griglia finale / Analisi indizi / Stats.">
          <S.TopBar ds={CN.ds} connection="connected" />
          <SummaryView result={RESULTS.red} />
        </S.DesktopFrame>

        <div className="section-label">Mobile 375 — recap · scoreboard e griglia finale</div>
        <div className="phones-grid">
          <PhoneSummary label="Recap · Scoreboard" result={RESULTS.red} initialTab="scoreboard" />
          <PhoneSummary label="Recap · Griglia · dark" dark result={RESULTS.red} initialTab="board" />
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
        @keyframes mai-pulse-dot-anim { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        .mai-shimmer { background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important; background-size: 200% 100% !important; animation: mai-shimmer-anim 1.4s linear infinite; }
        .mai-pulse-dot { animation: mai-pulse-dot-anim 1.5s ease-in-out infinite; transform-origin: center; display: inline-block; }
        .mai-cb-scroll::-webkit-scrollbar { width: 7px; height: 7px; }
        .mai-cb-scroll::-webkit-scrollbar-thumb { background: var(--border-strong); border-radius: 999px; }
        .phones-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(380px, 1fr)); gap: var(--s-7); align-items: start; }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage code { background: var(--bg-muted); padding: 1px 6px; border-radius: var(--r-sm); font-size: .85em; font-family: var(--f-mono); }
        @media (prefers-reduced-motion: reduce) { .mai-pulse-dot, .mai-shimmer { animation: none !important; } }
        .phone > div::-webkit-scrollbar { display: none; }
        button:focus-visible, a:focus-visible { outline: 2px solid hsl(var(--c-session)); outline-offset: 2px; }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
