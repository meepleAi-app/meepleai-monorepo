/* MeepleAI SP4 · Paleo — SUMMARY  (post-game, game-specific)
   /sessions/[id]/summary?game=paleo

   Riepilogo post-partita per Paleo (co-op · BinaryWin). Hero VITTORIA
   (pittura rupestre completa) / SCONFITTA (5 teschi · oppure tribù estinta),
   con causa specifica nel sottotitolo. Quattro tab: Tabellone · Cammino della
   tribù · Carte giocate · Statistiche. Switcher d'esito in alto per mostrare
   i tre finali canonici.

   Loads: sp4-parts-common.jsx · sp4-session-paleo-data.jsx ·
          sp4-session-skeleton-renderers.jsx · sp4-session-skeleton-parts.jsx ·
          sp4-session-paleo-flavor.jsx · sp4-session-paleo-parts.jsx
   DEMO-NAV-HINTS: sp4-session-paleo-live.html */

const S = window.SkeletonParts;
const R = window.SkeletonRenderers;
const PP = window.PaleoParts;
const PD = window.PaleoData;
const PF = window.PaleoFlavor;
const M = window.MAI;
const eHsl = M.entityHsl;
const { useState } = React;
const { mono, disp } = PF;
const SUM = PD.summary;
const STATES = PD.STATES;

// ─── hero ────────────────────────────────────────────────────────────────
const HeroBanner = ({ outcome, compact }) => {
  const win = outcome.kind === 'victory';
  const e = win ? 'toolkit' : 'event';
  return (
    <div role="status" style={{
      display: 'flex', alignItems: 'center', gap: compact ? 12 : 18, padding: compact ? '16px 16px' : '22px 26px',
      borderRadius: 'var(--r-xl)', background: `linear-gradient(135deg, ${eHsl(e, 0.16)}, ${eHsl(e, 0.05)})`,
      border: `1px solid ${eHsl(e, 0.4)}`, boxShadow: `0 8px 28px ${eHsl(e, 0.18)}`, flexShrink: 0,
    }}>
      <div aria-hidden="true" style={{ width: compact ? 52 : 72, height: compact ? 52 : 72, borderRadius: 'var(--r-lg)', background: eHsl(e, 0.16), border: `1px solid ${eHsl(e, 0.4)}`, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: compact ? 28 : 40, flexShrink: 0 }}>{outcome.emoji}</div>
      <div style={{ minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          <span style={{ ...disp(compact ? 24 : 34, 800, eHsl(e)), lineHeight: 1 }}>{outcome.title}</span>
          <span style={{ ...mono(9.5, 800, eHsl(e)), textTransform: 'uppercase', letterSpacing: '.06em', padding: '3px 9px', borderRadius: 'var(--r-pill)', background: eHsl(e, 0.12), border: `1px solid ${eHsl(e, 0.35)}` }}>BinaryWin · {win ? 'vittoria' : 'sconfitta'}</span>
        </div>
        <div style={{ ...disp(compact ? 13 : 15, 700, 'var(--text)'), marginTop: 6, lineHeight: 1.35 }}>{outcome.sub}</div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginTop: 8, padding: '6px 10px', borderRadius: 'var(--r-md)', background: 'var(--bg-card)', border: `1px solid ${eHsl(e, 0.25)}` }}>
          <span style={{ ...mono(8.5, 800, eHsl(e)), textTransform: 'uppercase', letterSpacing: '.05em', flexShrink: 0 }}>Causa</span>
          <span style={{ ...mono(10, 700, 'var(--text-sec)'), lineHeight: 1.4 }}>{outcome.cause}</span>
        </div>
      </div>
    </div>
  );
};

// ─── shared bits ───────────────────────────────────────────────────────────
const StatCard = ({ icon, label, value, sub, e = 'session', big }) => (
  <div style={{ flex: '1 1 130px', minWidth: 0, display: 'flex', flexDirection: 'column', gap: 5, padding: '12px 13px', borderRadius: 'var(--r-md)', background: eHsl(e, 0.07), border: `1px solid ${eHsl(e, 0.25)}` }}>
    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
      <span aria-hidden="true" style={{ fontSize: 15 }}>{icon}</span>
      <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>{label}</span>
    </div>
    <span style={{ ...disp(big ? 30 : 24, 800, eHsl(e)), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{value}</span>
    {sub && <span style={{ ...mono(9, 700, 'var(--text-sec)') }}>{sub}</span>}
  </div>
);
const SecLabel = ({ children }) => (
  <div style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.08em', margin: '2px 0' }}>{children}</div>
);
const Bar = ({ pct, e }) => (
  <div style={{ flex: 1, height: 9, borderRadius: 'var(--r-pill)', background: 'var(--bg-sunken)', overflow: 'hidden' }}>
    <div style={{ width: `${pct}%`, height: '100%', borderRadius: 'var(--r-pill)', background: eHsl(e) }} />
  </div>
);

// ─── tab · Tabellone (BinaryWin scoreboard) ─────────────────────────────────
const ScoreboardTab = ({ outcome }) => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 14, padding: 14 }}>
    <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr', gap: 10 }}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
          <span style={{ ...mono(9, 800, PF.OCHRE_DK), textTransform: 'uppercase', letterSpacing: '.06em' }}>Pittura rupestre</span>
          <span style={{ ...mono(9, 800, PF.OCHRE) }}>{outcome.painting}/5</span>
          <div style={{ flex: 1 }} />
          {outcome.painting >= 5 && <span style={{ ...mono(8, 800, eHsl('toolkit')) }}>completata ✓</span>}
        </div>
        <PF.CavePainting value={outcome.painting} max={5} size={36} />
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
          <span style={{ ...mono(9, 800, eHsl('event')), textTransform: 'uppercase', letterSpacing: '.06em' }}>Teschi</span>
          <span style={{ ...mono(9, 800, eHsl('event')) }}>{outcome.skulls}/5</span>
        </div>
        <PF.SkullCluster value={outcome.skulls} max={5} size={26} />
      </div>
    </div>
    <SecLabel>Bilancio finale</SecLabel>
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
      <StatCard icon="✋" label="Impronte" value={`${outcome.painting}/5`} e="toolkit" sub={outcome.painting >= 5 ? 'pittura completa' : 'incompleta'} />
      <StatCard icon="💀" label="Teschi" value={`${outcome.skulls}/5`} e="event" sub={outcome.skulls >= 5 ? 'estinzione' : 'sotto soglia'} />
      <StatCard icon="🦣" label="Membri vivi" value={outcome.membersAlive} e="player" sub={`su ${PD.MEMBERS.length} iniziali`} />
    </div>
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
      <StatCard icon="📜" label="Carte sapere" value={SUM.base.knowledge} e="kb" sub="totali raccolte" />
      <StatCard icon="📦" label="Risorse" value={SUM.base.resourcesGained} e="game" sub="totali guadagnate" />
    </div>
  </div>
);

// ─── tab · Cammino della tribù ──────────────────────────────────────────────
const JourneyTab = () => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 14, padding: 14 }}>
    <SecLabel>Per membro · turni sopravvissuti · caccia · ferite</SecLabel>
    <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
      {SUM.base.journey.map(({ member, turns, kills, wounds, end }) => {
        const owner = PD.byPlayer(member.owner);
        const dead = end === 'dead';
        return (
          <div key={member.id} style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '8px 10px', borderRadius: 'var(--r-md)', background: dead ? eHsl('event', 0.05) : 'var(--bg-muted)', border: `1px solid ${dead ? eHsl('event', 0.25) : 'var(--border-light)'}` }}>
            <PF.Meeple member={member} size={30} />
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <span style={{ ...disp(12.5, 800, 'var(--text)') }}>{member.name}</span>
                <PF.MiniAvatar p={owner} size={15} />
                <PF.SkillGlyph skill={member.skill} size={16} />
              </div>
              <div style={{ ...mono(9, 700, dead ? eHsl('event') : 'var(--text-muted)') }}>{dead ? `caduto al Giorno ${member.diedDay} · ${member.cause}` : 'sopravvissuto alla partita'}</div>
            </div>
            <div style={{ display: 'flex', gap: 10, flexShrink: 0 }}>
              <span title="turni" style={{ ...mono(10, 700, 'var(--text-sec)'), display: 'inline-flex', flexDirection: 'column', alignItems: 'center' }}><span style={{ ...disp(15, 800, eHsl('session')) }}>{turns}</span>turni</span>
              <span title="caccia" style={{ ...mono(10, 700, 'var(--text-sec)'), display: 'inline-flex', flexDirection: 'column', alignItems: 'center' }}><span style={{ ...disp(15, 800, eHsl('event')) }}>{kills}</span>🏹</span>
              <span title="ferite" style={{ ...mono(10, 700, 'var(--text-sec)'), display: 'inline-flex', flexDirection: 'column', alignItems: 'center' }}><span style={{ ...disp(15, 800, eHsl('agent')) }}>{wounds}</span>✚</span>
            </div>
          </div>
        );
      })}
    </div>
    <SecLabel>Cronologia eventi</SecLabel>
    <div style={{ display: 'flex', flexDirection: 'column' }}>
      {SUM.base.timeline.map((ev, i) => (
        <div key={i} style={{ display: 'flex', gap: 10, padding: '6px 0' }}>
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', flexShrink: 0 }}>
            <span style={{ width: 24, height: 24, borderRadius: '50%', background: eHsl(ev.e, 0.14), border: `1px solid ${eHsl(ev.e, 0.35)}`, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12 }} aria-hidden="true">{ev.icon}</span>
            {i < SUM.base.timeline.length - 1 && <span style={{ width: 2, flex: 1, minHeight: 12, background: 'var(--border)', marginTop: 2 }} />}
          </div>
          <div style={{ flex: 1, minWidth: 0, paddingBottom: 4 }}>
            <span style={{ ...mono(8.5, 800, eHsl(ev.e)), textTransform: 'uppercase', letterSpacing: '.05em' }}>Giorno {ev.day}</span>
            <div style={{ fontFamily: 'var(--f-body)', fontSize: 12.5, color: 'var(--text-sec)', lineHeight: 1.4 }}>{ev.text}</div>
          </div>
        </div>
      ))}
    </div>
  </div>
);

// ─── tab · Carte giocate ────────────────────────────────────────────────────
const CardsPlayedTab = () => {
  const maxN = Math.max(...SUM.base.cardFreq.map(c => c.n));
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14, padding: 14 }}>
      <SecLabel>Frequenza azioni · carte giocate</SecLabel>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {SUM.base.cardFreq.map(c => (
          <div key={c.lb} style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
            <span style={{ width: 92, flexShrink: 0, display: 'inline-flex', alignItems: 'center', gap: 5, ...disp(11.5, 800, eHsl(c.e)) }}><span aria-hidden="true">{c.icon}</span>{c.lb}</span>
            <Bar pct={(c.n / maxN) * 100} e={c.e} />
            <span style={{ ...mono(11, 800, 'var(--text)'), width: 22, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>{c.n}</span>
          </div>
        ))}
      </div>
      <SecLabel>Tassi</SecLabel>
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        <StatCard icon="🎯" label="Missioni" value={`${SUM.base.missionsDone}/${SUM.base.missionsTotal}`} e="toolkit" sub={`${Math.round((SUM.base.missionsDone / SUM.base.missionsTotal) * 100)}% completate`} />
        <StatCard icon="🕳" label="Incontri" value={`${Math.round(SUM.base.encounterSurvival * 100)}%`} e="agent" sub="sopravvivenza pericoli" />
      </div>
    </div>
  );
};

// ─── tab · Statistiche ──────────────────────────────────────────────────────
const StatsTab = () => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 14, padding: 14 }}>
    <SecLabel>Partita</SecLabel>
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
      <StatCard icon="📅" label="Giorni" value={`${SUM.base.days}/${SUM.base.totalDays}`} e="session" sub="durata partita" big />
      <StatCard icon="📦" label="Risorse/giorno" value={SUM.base.resourcesPerDay} e="game" sub="efficienza media" big />
    </div>
    <SecLabel>Coordinamento</SecLabel>
    <div style={{ padding: '12px 13px', borderRadius: 'var(--r-md)', background: eHsl('player', 0.07), border: `1px solid ${eHsl('player', 0.25)}` }}>
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, marginBottom: 8 }}>
        <span style={{ ...disp(12.5, 800, 'var(--text)') }}>Tasso di sincronia</span>
        <div style={{ flex: 1 }} />
        <span style={{ ...disp(20, 800, eHsl('player')), fontVariantNumeric: 'tabular-nums' }}>{Math.round(SUM.base.syncRate * 100)}%</span>
      </div>
      <Bar pct={SUM.base.syncRate * 100} e="player" />
      <div style={{ ...mono(9.5, 700, 'var(--text-muted)'), lineHeight: 1.5, marginTop: 8 }}>Quanto spesso le azioni dei giocatori sono state coordinate (stessa missione / nessuno spreco) anziché scollegate, durante la fase Giorno.</div>
    </div>
  </div>
);

// ─── summary view (tabs) ────────────────────────────────────────────────────
const SUM_TABS = [
  { id: 'board',   icon: '🎯', label: 'Tabellone', e: 'session', render: (o) => <ScoreboardTab outcome={o} /> },
  { id: 'journey', icon: '🦣', label: 'Cammino',   e: 'player',  render: () => <JourneyTab /> },
  { id: 'cards',   icon: '🃏', label: 'Carte',     e: 'game',    render: () => <CardsPlayedTab /> },
  { id: 'stats',   icon: '📊', label: 'Statistiche', e: 'kb',    render: () => <StatsTab /> },
];
const SummaryView = ({ outcomeKey, compact }) => {
  const [tab, setTab] = useState('board');
  const outcome = SUM.outcomes[outcomeKey];
  const active = SUM_TABS.find(t => t.id === tab) || SUM_TABS[0];
  return (
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, background: 'var(--bg)' }}>
      <div style={{ padding: compact ? 12 : 16, flexShrink: 0 }}><HeroBanner outcome={outcome} compact={compact} /></div>
      <div role="tablist" aria-label="Sezioni riepilogo" style={{ display: 'flex', borderBottom: '1px solid var(--border)', flexShrink: 0, padding: '0 8px' }}>
        {SUM_TABS.map(t => {
          const on = tab === t.id;
          return (
            <button key={t.id} type="button" role="tab" aria-selected={on} onClick={() => setTab(t.id)} style={{ flex: '1 1 0', minWidth: 0, padding: '11px 4px', background: 'transparent', border: 'none', borderBottom: on ? `2px solid ${eHsl(t.e)}` : '2px solid transparent', color: on ? eHsl(t.e) : 'var(--text-sec)', ...disp(11, 800), cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 5, whiteSpace: 'nowrap' }}>
              <span aria-hidden="true" style={{ fontSize: 13 }}>{t.icon}</span><span style={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis' }}>{t.label}</span>
            </button>
          );
        })}
      </div>
      <div role="tabpanel" className="mai-cb-scroll" style={{ flex: 1, minHeight: 0, overflowY: 'auto' }}>{active.render(outcome)}</div>
    </div>
  );
};

// ─── outcome switcher ───────────────────────────────────────────────────────
const OUTCOME_OPTS = [
  { id: 'victory', lb: 'Vittoria' },
  { id: 'defeat-skulls', lb: 'Sconfitta · teschi' },
  { id: 'defeat-extinct', lb: 'Sconfitta · estinzione' },
];
const OutcomeSwitch = ({ value, onChange }) => (
  <div role="group" aria-label="Esito da mostrare" style={{ display: 'flex', gap: 5, flexWrap: 'wrap' }}>
    <span style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', alignSelf: 'center', marginRight: 2 }}>Esito</span>
    {OUTCOME_OPTS.map(o => {
      const on = value === o.id;
      const win = o.id === 'victory';
      return (
        <button key={o.id} type="button" onClick={() => onChange(o.id)} aria-pressed={on} style={{ padding: '5px 12px', borderRadius: 'var(--r-pill)', background: on ? eHsl(win ? 'toolkit' : 'event', 0.14) : 'var(--bg-card)', border: on ? `1px solid ${eHsl(win ? 'toolkit' : 'event', 0.45)}` : '1px solid var(--border)', color: on ? eHsl(win ? 'toolkit' : 'event') : 'var(--text-sec)', ...disp(11, 800), cursor: 'pointer' }}>{o.lb}</button>
      );
    })}
  </div>
);

// ─── phone frame (summary body) ─────────────────────────────────────────────
const PhoneFrame = ({ label, dark, outcomeKey, desc }) => (
  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
    <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark</span>}</div>
    <div className="phone" data-theme={dark ? 'dark' : undefined}>
      <div className="phone-sbar" style={{ color: 'var(--text)' }}><span style={{ fontFamily: 'var(--f-mono)' }}>18:04</span><div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div></div>
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', overflow: 'hidden', minHeight: 0 }}>
        <S.TopBar ds={PD.ds} compact />
        <SummaryView outcomeKey={outcomeKey} compact />
      </div>
    </div>
    {desc && <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 340, textAlign: 'center', lineHeight: 1.55 }}>{desc}</div>}
  </div>
);

function App() {
  const [outcome, setOutcome] = useState('victory');
  return (
    <div className="stage">
      <S.ThemeToggle />
      <div className="stage-wrap">
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8, display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <span>SP4 · Paleo · Riepilogo 🦣</span>
          <a href="sp4-session-paleo-live.html" style={{ color: eHsl('session'), borderBottom: `1px solid ${eHsl('session', 0.4)}` }}>← Sessione live</a>
        </div>
        <h1>Paleo — riepilogo post-partita</h1>
        <p className="lead">
          Esito collettivo <strong>BinaryWin</strong> per <code>/sessions/[id]/summary</code>. L'hero mostra
          <strong> Vittoria</strong> (pittura rupestre completa) o <strong>Sconfitta</strong> (5 teschi oppure tribù
          estinta), con la causa specifica. Quattro tab: <strong>Tabellone</strong>, <strong>Cammino della tribù</strong>,
          <strong> Carte giocate</strong>, <strong>Statistiche</strong>. Usa lo switch per i tre finali canonici.
        </p>

        <div className="section-label">Desktop 1280 — scegli l'esito · le tab e l'hero si aggiornano</div>
        <div style={{ marginBottom: 14 }}><OutcomeSwitch value={outcome} onChange={setOutcome} /></div>
        <S.DesktopFrame ds={PD.ds} label="Paleo — riepilogo" url="meepleai.app/sessions/paleo-1/summary" height={640}
          desc="Hero d'esito con causa · Tabellone (BinaryWin: pittura vs teschi, membri vivi, sapere, risorse) · Cammino (per membro + cronologia) · Carte (frequenza azioni + tassi) · Statistiche (giorni, efficienza, sincronia).">
          <S.TopBar ds={PD.ds} connection="offline" />
          <SummaryView outcomeKey={outcome} />
        </S.DesktopFrame>

        <div className="section-label">Mobile 375 — i tre esiti canonici</div>
        <div className="phones-grid">
          <PhoneFrame label="Vittoria" outcomeKey="victory" desc="Pittura rupestre completa (5/5). Hero toolkit-green, causa nel sottotitolo." />
          <PhoneFrame label="Sconfitta · teschi" outcomeKey="defeat-skulls" desc="5° teschio: la tribù si estingue. Hero danger-red, cluster teschi al massimo." />
          <PhoneFrame label="Sconfitta · estinzione" dark outcomeKey="defeat-extinct" desc="Nessun membro sopravvissuto. Stesso hero, causa diversa nel sottotitolo." />
        </div>

        <div className="section-label">Gallery stati · ScoringPanelRenderer (BinaryWin) — il tabellone riusa lo stesso renderer dello skeleton × 5 stati</div>
        <div className="mai-cb-scroll" style={{ display: 'flex', gap: 16, overflowX: 'auto', paddingBottom: 10 }}>
          {STATES.map(s => (
            <PP.PanelFrame key={s.id} label={s.lb} entity="session" h={470}>
              <R.ScoringPanelRenderer data={PD.ds} state={s.id} />
            </PP.PanelFrame>
          ))}
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
        @keyframes mai-pulse-dot-anim { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        .mai-shimmer { background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important; background-size: 200% 100% !important; animation: mai-shimmer-anim 1.4s linear infinite; }
        .mai-pulse-dot { animation: mai-pulse-dot-anim 1.5s ease-in-out infinite; transform-origin: center; display: inline-block; }
        .mai-cb-scroll::-webkit-scrollbar { height: 7px; }
        .mai-cb-scroll::-webkit-scrollbar-thumb { background: var(--border-strong); border-radius: 999px; }
        .phones-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(380px, 1fr)); gap: var(--s-7); align-items: start; }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage code { background: var(--bg-muted); padding: 1px 6px; border-radius: var(--r-sm); font-size: .85em; font-family: var(--f-mono); }
        @media (prefers-reduced-motion: reduce) { .mai-pulse-dot, .mai-shimmer { animation: none !important; } }
        .phone > div::-webkit-scrollbar { display: none; }
        button:focus-visible, a:focus-visible, input:focus-visible, textarea:focus-visible { outline: 2px solid hsl(var(--c-session)); outline-offset: 2px; }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
