/* MeepleAI SP4 — Generic Session Skeleton · SUMMARY
   /sessions/[id]  — post-game review, game-AGNOSTIC.

   Hero post-game (vincitore / esito collettivo / obiettivi) + 5 tab:
     Scoreboard (ScoringPanelRenderer) · Diario · Foto · ChatHighlights · Stat.
   Rendered SIDE-BY-SIDE per due dataset opposti:
     A · Wingspan — Points, c'è un vincitore
     B · Paleo    — BinaryWin, esito collettivo (tribù salva)

   Loads: sp4-parts-common.jsx · sp4-session-skeleton-data.jsx ·
          sp4-session-skeleton-renderers.jsx · sp4-session-skeleton-parts.jsx
   DEMO-NAV-HINTS: sp4-session-skeleton-live.html
*/

const { useState: useStateS } = React;
const Ds = window.SkeletonData;
const Sp = window.SkeletonParts;
const Rs = window.SkeletonRenderers;
const Ms = window.MAI;
const eHslS = Ms.entityHsl;
const monoS = Rs.mono;
const WINGs = Ds.DATASETS.wingspan;
const PALEOs = Ds.DATASETS.paleo;
const STATESs = Ds.STATES;
const AvatarS = Rs.Avatar;
const LabelS = Rs.Label;

// ─── HERO ──────────────────────────────────────────────────────────────────
const HeroSummary = ({ ds }) => {
  const sm = ds.summary;
  const res = sm.result;
  const winner = res.kind === 'winner' ? ds.players.find(p => p.id === res.playerId) : null;
  return (
    <header aria-label="Esito partita" style={{ position: 'relative', padding: '22px 22px 20px', background: ds.game.cover, overflow: 'hidden', flexShrink: 0 }}>
      <div style={{ position: 'absolute', inset: 0, background: 'linear-gradient(180deg, rgba(0,0,0,.12), rgba(0,0,0,.55))' }} aria-hidden="true" />
      <div style={{ position: 'relative', display: 'flex', flexDirection: 'column', gap: 14 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <span style={{ fontSize: 26 }} aria-hidden="true">{ds.game.emoji}</span>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontFamily: 'var(--f-display)', fontSize: 20, fontWeight: 800, color: '#fff', textShadow: '0 2px 8px rgba(0,0,0,.4)' }}>{ds.game.title}</div>
            <div style={{ ...monoS(10, 700, 'rgba(255,255,255,.85)') }}>{sm.date} · {sm.duration} · {sm.rounds}</div>
          </div>
          <Ms.OutcomeBadge status={sm.outcome} />
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 14, padding: 14, borderRadius: 'var(--r-lg)', background: 'rgba(0,0,0,.32)', backdropFilter: 'blur(6px)', border: '1px solid rgba(255,255,255,.16)' }}>
          {winner
            ? <AvatarS p={winner} size={52} ring />
            : <div style={{ width: 52, height: 52, borderRadius: 'var(--r-md)', background: eHslS(res.won ? 'toolkit' : 'event', 0.9), display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 26 }} aria-hidden="true">{res.won ? '🏆' : '💀'}</div>}
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ ...monoS(9.5, 800, 'rgba(255,255,255,.8)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>{res.kind === 'winner' ? 'Vincitore' : res.kind === 'collective' ? 'Esito collettivo' : 'Obiettivi'}</div>
            <div style={{ fontFamily: 'var(--f-display)', fontSize: 19, fontWeight: 800, color: '#fff' }}>{res.headline}</div>
            <div style={{ ...monoS(10.5, 700, 'rgba(255,255,255,.85)') }}>{res.sub}</div>
          </div>
        </div>
      </div>
    </header>
  );
};

// ─── TAB BODIES ────────────────────────────────────────────────────────────
const DiaryTab = ({ ds, state = 'default' }) => {
  const KIND_E = { 'game-start': 'session', 'turn-change': 'player', 'score-update': 'toolkit', custom: 'event' };
  return (
    <Rs.StateScaffold state={state} sseWhere="diario"
      empty={{ icon: '📓', title: 'Diario vuoto', body: 'Nessun evento rilevante registrato in questa partita.' }}
      error={{ title: 'Diario non disponibile', body: 'Impossibile caricare la timeline della partita.' }}>
      <Rs.Panel>
        <LabelS>Diario · {ds.events.length} eventi</LabelS>
        <div style={{ display: 'flex', flexDirection: 'column' }}>
          {ds.events.map((ev, i) => {
            const e = KIND_E[ev.kind] || 'session';
            const who = ds.players.find(p => p.id === ev.who);
            return (
              <div key={i} style={{ display: 'flex', gap: 11, padding: '8px 0' }}>
                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', flexShrink: 0 }}>
                  <span style={{ width: 11, height: 11, borderRadius: '50%', background: eHslS(e), boxShadow: `0 0 0 3px ${eHslS(e, 0.2)}` }} aria-hidden="true" />
                  {i < ds.events.length - 1 && <span style={{ width: 2, flex: 1, minHeight: 16, background: 'var(--border)', marginTop: 3 }} />}
                </div>
                <div style={{ flex: 1, minWidth: 0, paddingBottom: 4 }}>
                  <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
                    {who && <span style={{ fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, color: `hsl(${who.hue},60%,52%)` }}>{who.name}</span>}
                    <span style={{ ...monoS(8.5, 800, eHslS(e)), textTransform: 'uppercase', letterSpacing: '.05em' }}>{ev.kind}</span>
                    <div style={{ flex: 1 }} />
                    <span style={{ ...monoS(9, 700, 'var(--text-muted)') }}>{ev.t}</span>
                  </div>
                  <div style={{ fontFamily: 'var(--f-body)', fontSize: 13, color: 'var(--text-sec)', lineHeight: 1.45 }} dangerouslySetInnerHTML={{ __html: ev.text }} />
                </div>
              </div>
            );
          })}
        </div>
      </Rs.Panel>
    </Rs.StateScaffold>
  );
};

const PhotosTab = ({ ds, state = 'default' }) => (
  <Rs.StateScaffold state={state} sseWhere="foto"
    empty={{ icon: '📷', title: 'Nessuna foto', body: 'Aggiungi foto del tavolo durante o dopo la partita.', action: <button type="button" style={{ marginTop: 4, padding: '6px 12px', borderRadius: 'var(--r-pill)', background: eHslS('kb', 0.12), color: eHslS('kb'), border: `1px solid ${eHslS('kb', 0.35)}`, cursor: 'pointer', ...monoS(10, 800) }}>+ Carica foto</button> }}
    error={{ title: 'Foto non disponibili', body: 'Impossibile caricare la galleria.' }}>
    <Rs.Panel>
      <LabelS>Galleria · {ds.photos.length}</LabelS>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 8 }}>
        {ds.photos.map((p, i) => (
          <div key={i} style={{ aspectRatio: '4/3', borderRadius: 'var(--r-md)', background: p.g, display: 'flex', alignItems: 'flex-end', padding: 8, boxShadow: 'var(--shadow-xs)' }}>
            <span style={{ ...monoS(9.5, 800, '#fff'), background: 'rgba(0,0,0,.42)', padding: '2px 7px', borderRadius: 'var(--r-pill)' }}>{p.lb}</span>
          </div>
        ))}
        <button type="button" aria-label="Aggiungi foto" style={{ aspectRatio: '4/3', borderRadius: 'var(--r-md)', background: 'transparent', border: '1px dashed var(--border-strong)', color: 'var(--text-muted)', fontSize: 22, cursor: 'pointer' }}>+</button>
      </div>
    </Rs.Panel>
  </Rs.StateScaffold>
);

const ChatHighlightsTab = ({ ds, state = 'default' }) => {
  const highlights = ds.chat.filter(m => m.from === 'agent');
  return (
    <Rs.StateScaffold state={state} sseWhere="chat"
      empty={{ icon: '💬', title: 'Nessun highlight', body: 'Le risposte salienti dell\u2019agente compariranno qui.' }}
      error={{ title: 'Chat non disponibile', body: 'Impossibile caricare gli estratti chat.' }}>
      <Rs.Panel>
        <LabelS>Estratti chat AI · {highlights.length}</LabelS>
        {highlights.map((m, i) => {
          const q = ds.chat[ds.chat.indexOf(m) - 1];
          return (
            <div key={m.id} style={{ borderRadius: 'var(--r-md)', background: 'var(--bg-card)', border: '1px solid var(--border)', overflow: 'hidden' }}>
              {q && q.from === 'user' && <div style={{ padding: '7px 11px', background: eHslS('chat', 0.06), borderBottom: `1px solid ${eHslS('chat', 0.18)}`, ...monoS(10.5, 700, eHslS('chat')) }}>“{q.text}”</div>}
              <div style={{ padding: '9px 11px', display: 'flex', gap: 8 }}>
                <span style={{ fontSize: 15 }} aria-hidden="true">{ds.agent.emoji}</span>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontFamily: 'var(--f-body)', fontSize: 12.5, color: 'var(--text)', lineHeight: 1.45, fontWeight: 500 }}>{m.text}</div>
                  {m.citations && <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap', marginTop: 6 }}>
                    {m.citations.map((c, j) => <span key={j} style={{ display: 'inline-flex', alignItems: 'center', gap: 3, padding: '2px 7px', borderRadius: 'var(--r-pill)', background: eHslS('kb', 0.1), border: `1px solid ${eHslS('kb', 0.25)}`, color: eHslS('kb'), ...monoS(9, 700) }}><span aria-hidden="true">📄</span>{c}</span>)}
                  </div>}
                </div>
              </div>
            </div>
          );
        })}
      </Rs.Panel>
    </Rs.StateScaffold>
  );
};

const PlayersStatsTab = ({ ds, state = 'default' }) => {
  const hasScore = ds.scoring.scoreType === 'Points';
  const rows = [...ds.players].sort((a, b) => (b.score || 0) - (a.score || 0));
  const topCat = (pid) => {
    if (!ds.breakdown?.[pid]) return null;
    const entries = Object.entries(ds.breakdown[pid]);
    const top = entries.sort((a, b) => b[1] - a[1])[0];
    const cat = ds.scoring.categories.find(c => c.id === top[0]);
    return cat ? `${cat.label} (${top[1]})` : null;
  };
  return (
    <Rs.StateScaffold state={state} sseWhere="statistiche"
      empty={{ icon: '👥', title: 'Nessuna statistica', body: 'Le statistiche post-partita non sono disponibili.' }}
      error={{ title: 'Statistiche non disponibili', body: 'Impossibile calcolare le statistiche dei giocatori.' }}>
      <Rs.Panel>
        <LabelS>{hasScore ? 'Statistiche giocatori' : 'Contributo · co-op'}</LabelS>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
          {rows.map((p, i) => (
            <div key={p.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '9px 11px', borderRadius: 'var(--r-md)', background: i === 0 && hasScore ? eHslS('toolkit', 0.07) : 'var(--bg-card)', border: `1px solid ${i === 0 && hasScore ? eHslS('toolkit', 0.28) : 'var(--border)'}` }}>
              <AvatarS p={p} size={32} ring={i === 0 && hasScore} />
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, color: 'var(--text)' }}>{p.name}{p.presence === 'host' && <span style={{ ...monoS(8.5, 800, eHslS('session')), marginLeft: 6, padding: '1px 5px', borderRadius: 'var(--r-pill)', background: eHslS('session', 0.12) }}>HOST</span>}</div>
                <div style={{ ...monoS(9.5, 700, 'var(--text-muted)') }}>{hasScore ? (topCat(p.id) ? `Top: ${topCat(p.id)}` : 'partita completata') : 'membro tribù · attivo'}</div>
              </div>
              {hasScore
                ? <div style={{ textAlign: 'right' }}><div style={{ fontFamily: 'var(--f-display)', fontSize: 18, fontWeight: 800, color: i === 0 ? eHslS('toolkit') : 'var(--text)', fontVariantNumeric: 'tabular-nums' }}>{p.score}</div><div style={{ ...monoS(8.5, 700, 'var(--text-muted)') }}>{i + 1}° posto</div></div>
                : <span style={{ ...monoS(10, 800, eHslS('toolkit')) }}>✓</span>}
            </div>
          ))}
        </div>
      </Rs.Panel>
    </Rs.StateScaffold>
  );
};

// ─── TABS ──────────────────────────────────────────────────────────────────
const SUMMARY_TABS = [
  { id: 'score',   icon: '🏆', label: 'Risultato', entity: 'session', render: (ds, st) => <Rs.ScoringPanelRenderer data={ds} state={st} /> },
  { id: 'diary',   icon: '📓', label: 'Diario',    entity: 'event',   render: (ds, st) => <DiaryTab ds={ds} state={st} /> },
  { id: 'photos',  icon: '📷', label: 'Foto',      entity: 'kb',      render: (ds, st) => <PhotosTab ds={ds} state={st} /> },
  { id: 'chat',    icon: '💬', label: 'Chat',      entity: 'chat',    render: (ds, st) => <ChatHighlightsTab ds={ds} state={st} /> },
  { id: 'stats',   icon: '👥', label: 'Stat',      entity: 'player',  render: (ds, st) => <PlayersStatsTab ds={ds} state={st} /> },
];

const SummaryTabs = ({ ds, initial = 'score', initialState = 'default', mobile }) => {
  const [tab, setTab] = useStateS(initial);
  const [st, setSt] = useStateS(initialState);
  const active = SUMMARY_TABS.find(t => t.id === tab) || SUMMARY_TABS[0];
  return (
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, background: 'var(--bg-card)' }}>
      <div role="tablist" aria-label="Sezioni recap" className="mai-cb-scroll" style={{ display: 'flex', borderBottom: '1px solid var(--border-light)', flexShrink: 0, overflowX: 'auto' }}>
        {SUMMARY_TABS.map(t => {
          const on = tab === t.id;
          return (
            <button key={t.id} type="button" role="tab" aria-selected={on} aria-label={t.label} onClick={() => setTab(t.id)} style={{ flex: mobile ? '0 0 auto' : 1, padding: '10px 12px', background: on ? eHslS(t.entity, 0.06) : 'transparent', border: 'none', borderBottom: on ? `2px solid ${eHslS(t.entity)}` : '2px solid transparent', color: on ? eHslS(t.entity) : 'var(--text-sec)', fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 5, whiteSpace: 'nowrap' }}>
              <span aria-hidden="true" style={{ fontSize: 14 }}>{t.icon}</span><span>{t.label}</span>
            </button>
          );
        })}
      </div>
      <Sp.StateSwitch value={st} onChange={setSt} />
      <div role="tabpanel" style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(ds, st)}</div>
    </div>
  );
};

const SummaryDesktopBody = ({ ds, initialTab, initialState }) => (
  <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
    <HeroSummary ds={ds} />
    <SummaryTabs ds={ds} initial={initialTab} initialState={initialState} />
  </div>
);

const SummaryPhone = ({ ds, label, desc, dark, initialTab }) => (
  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
    <div style={{ ...monoS(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>{label}{dark && <span style={{ color: eHslS('session'), marginLeft: 6 }}>· dark</span>}</div>
    <div className="phone" data-theme={dark ? 'dark' : undefined}>
      <div className="phone-sbar" style={{ color: 'var(--text)' }}>
        <span style={{ fontFamily: 'var(--f-mono)' }}>22:40</span>
        <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
      </div>
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', overflow: 'hidden', minHeight: 0 }}>
        <HeroSummary ds={ds} />
        <SummaryTabs ds={ds} initial={initialTab || 'score'} mobile />
      </div>
    </div>
    {desc && <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 340, textAlign: 'center', lineHeight: 1.55 }}>{desc}</div>}
  </div>
);

// ─── APP ───────────────────────────────────────────────────────────────────
function AppSummary() {
  return (
    <div className="stage">
      <Sp.ThemeToggle />
      <div className="stage-wrap">
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Generic Session Skeleton · Summary 🏁</div>
        <h1>Session summary — recap post-game generico</h1>
        <p className="lead">
          Template universale per <code>/sessions/[id]</code>. Hero post-game (vincitore · esito collettivo · obiettivi) +
          5 tab: <strong>Risultato</strong> (ScoringPanelRenderer) · Diario · Foto · ChatHighlights · Statistiche.
          Stesso skeleton applicato a due esiti opposti, affiancati: <strong>Wingspan</strong> (c'è un vincitore) e
          <strong> Paleo</strong> (esito collettivo co-op). Dark = primario.
        </p>

        {/* SIDE-BY-SIDE */}
        <div className="section-label">Side-by-side · Desktop — stesso recap, 2 esiti · cambia tab e stato dai selettori</div>
        <div className="sk-sxs">
          <Sp.DesktopFrame ds={WINGs} label="Dataset A · Wingspan — vincitore + Points" url="meepleai.app/sessions/wing-3" height={680}
            desc="Hero con vincitore (Marco, 87). Tab Risultato → leaderboard + breakdown 6 categorie; Diario → eventi chiave; Stat → punteggi e categoria migliore per giocatore.">
            <SummaryDesktopBody ds={WINGs} initialTab="score" initialState="default" />
          </Sp.DesktopFrame>
          <Sp.DesktopFrame ds={PALEOs} label="Dataset B · Paleo — esito collettivo + BinaryWin" url="meepleai.app/sessions/paleo-1" height={680}
            desc="Hero con esito collettivo (tribù salva, no vincitore singolo). Tab Risultato → indicatore BinaryWin; Stat → contributo co-op invece di punteggi.">
            <SummaryDesktopBody ds={PALEOs} initialTab="score" initialState="default" />
          </Sp.DesktopFrame>
        </div>

        {/* MOBILE */}
        <div className="section-label">Mobile 375 — hero + tab scrollabili</div>
        <div className="phones-grid">
          <SummaryPhone ds={WINGs} label="A · Wingspan · Risultato" desc="Hero vincitore + leaderboard. Tab strip scrollabile, selettore stato sotto." />
          <SummaryPhone ds={WINGs} label="A · Wingspan · Diario" initialTab="diary" desc="Timeline degli eventi salienti della partita." />
          <SummaryPhone ds={PALEOs} label="B · Paleo · Risultato · dark" dark initialTab="score" desc="Esito collettivo BinaryWin: stesso hero, nessun punteggio individuale." />
        </div>

        {/* STATES GALLERY for the 5 summary tabs */}
        <div className="section-label">Gallery stati · ogni tab del recap × 5 stati canonici (su Wingspan)</div>
        {SUMMARY_TABS.map(t => (
          <div key={t.id} style={{ marginBottom: 26 }}>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 10 }}>
              <span style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: eHslS(t.entity), display: 'inline-flex', alignItems: 'center', gap: 7 }}><span aria-hidden="true">{t.icon}</span>{t.label}</span>
              <span style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)', fontWeight: 700 }}>/sessions/[id] · tab {t.id}</span>
            </div>
            <div className="mai-cb-scroll" style={{ display: 'flex', gap: 16, overflowX: 'auto', paddingBottom: 10 }}>
              {STATESs.map(s => (
                <Sp.PanelFrame key={s.id} label={s.lb} entity={t.entity} h={460}>
                  {t.render(WINGs, s.id)}
                </Sp.PanelFrame>
              ))}
            </div>
          </div>
        ))}
      </div>

      <style>{`
        @keyframes mai-shimmer-anim { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
        @keyframes mai-pulse-dot-anim { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        .mai-shimmer { background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important; background-size: 200% 100% !important; animation: mai-shimmer-anim 1.4s linear infinite; }
        .mai-pulse-dot { animation: mai-pulse-dot-anim 1.5s ease-in-out infinite; transform-origin: center; display: inline-block; }
        .mai-cb-scroll::-webkit-scrollbar { height: 7px; }
        .mai-cb-scroll::-webkit-scrollbar-thumb { background: var(--border-strong); border-radius: 999px; }
        .sk-sxs { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 22px; align-items: start; }
        @media (max-width: 1100px) { .sk-sxs { grid-template-columns: 1fr; } }
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

ReactDOM.createRoot(document.getElementById('root')).render(<AppSummary />);
