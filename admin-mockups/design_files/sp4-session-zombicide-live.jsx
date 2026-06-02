/* MeepleAI SP4 · Zombicide: Green Horde — SESSION LIVE  (App)
   /sessions/[id]/live — estensione game-specific dello skeleton universale
   applicata a Zombicide: Green Horde (co-op miniatures dungeon-crawler,
   1-6 giocatori, ~60-90 min/scenario).

   PREMIUM mockup. Lo skeleton (mockup #1) fornisce i renderer polimorfici +
   la chrome universale; QUESTO layer aggiunge la board di Zombicide GH:
   PhaseTimeline (3 fasi del round), DangerLevel (pilota lo spawn), SurvivorCard
   (skill tree + equip + ferite + AP), BoardStatePanel (conteggi zombie),
   CombatDicePanel (D6 rollable), SpawnDeckPanel, MapTilesGrid e una colonna
   destra a 5 tab (Scoring · Dice · Board · Equip · Chat).

   ACCORDION (CRITICAL): la colonna LEFT replica il pattern accordion dello
   skeleton (un solo pannello expanded), esteso via sec(id) a Survivor · Board ·
   Spawn · ChatAgent · ActionLog (questi ultimi due riusati verbatim).

   Riusa verbatim: TopBar · ChatAgentPanel · ActionLog · ScoringPanelRenderer
   (BinaryWin) · TurnIndicatorRenderer (Sequential 3-fasi) · ToolkitRenderer.
   Dark = modalità primaria (ambiance Zombicide).

   Carica: sp4-parts-common · zombicide-data(shim SkeletonData) · skeleton-renderers
           · skeleton-parts · zombicide-flavor · zombicide-parts · QUESTO file.
   DEMO-NAV-HINTS: sp4-session-zombicide-summary.html
*/

const M = window.MAI;
const R = window.SkeletonRenderers;
const S = window.SkeletonParts;
const Z = window.ZOM;
const F = window.ZOMFlavor;
const P = window.ZOMParts;
const eHsl = M.entityHsl;
const { useState, useEffect } = React;
const { mono, disp } = F;

function ThemeToggle() {
  const [dark, setDark] = useState(true);
  useEffect(() => { document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light'); }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle" aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

const Intro = () => (
  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 12, margin: '4px 0 8px' }}>
    {[
      { e: 'session', h: 'Estende lo skeleton', b: 'Riusa TopBar · ChatAgent · ActionLog e i 3 renderer polimorfici. Aggiunge sopra la board di Zombicide GH: fasi, survivor, plancia, dadi.' },
      { e: 'event',   h: 'Plancia & Danger', b: 'Conteggi zombie per 6 tipi (Walker → Necromancer). Il Danger Level segue il survivor con XP più alto e detta l\u2019aggressività dello spawn.' },
      { e: 'game',    h: 'Skill tree XP', b: 'Ogni survivor cresce su 4 livelli color-coded: Blu (0-6) → Giallo (7-18) → Arancione (19-42) → Rosso (43+). Skill ottenuti highlighted, prossimi greyed.' },
      { e: 'toolkit', h: '5 stati canonici', b: 'default · empty · loading · error · sse-disconnect per ogni pannello (survivor, plancia, dadi, spawn, mappa, scoring).' },
    ].map(c => (
      <div key={c.h} style={{ padding: 12, borderRadius: 'var(--r-lg)', background: eHsl(c.e, 0.06), border: `1px solid ${eHsl(c.e, 0.25)}` }}>
        <div style={{ ...disp(13, 800, eHsl(c.e)), marginBottom: 4 }}>{c.h}</div>
        <div style={{ fontFamily: 'var(--f-body)', fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.5 }}>{c.b}</div>
      </div>
    ))}
  </div>
);

const StatesRow = ({ title, sub, entity, render }) => (
  <div style={{ marginBottom: 26 }}>
    <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 10, flexWrap: 'wrap' }}>
      <span style={{ ...disp(15, 800, eHsl(entity)) }}>{title}</span>
      {sub && <span style={{ ...mono(10, 700, 'var(--text-muted)') }}>{sub}</span>}
    </div>
    <div className="mai-cb-scroll" style={{ display: 'flex', gap: 16, overflowX: 'auto', paddingBottom: 10 }}>
      {P.STATES.map(s => (
        <P.PanelFrame key={s.id} label={s.lb} entity={entity} dark={s.id === 'sse'}>
          {render(s.id)}
        </P.PanelFrame>
      ))}
    </div>
  </div>
);

function App() {
  return (
    <div className="stage">
      <ThemeToggle />
      <div className="stage-wrap">
        <div style={{ ...mono(11, 600, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Zombicide: Green Horde · Live 🧟 · estensione skeleton</div>
        <h1>Session live — Zombicide: Green Horde</h1>
        <p className="lead">
          Estensione game-specific dello <strong>skeleton universale</strong> applicata a <strong>Zombicide: Green Horde</strong>
          (co-op miniatures dungeon-crawler, 1-6 giocatori, ~60-90 min). Lo skeleton fornisce i renderer polimorfici e la
          chrome; questo layer aggiunge il <strong>banner a 3 fasi</strong>, il <strong>Danger Level</strong>, la
          <strong> SurvivorCard</strong> (skill tree + equip + ferite + AP), lo <strong>stato plancia</strong>, i
          <strong> dadi da combattimento</strong> (D6 rollable), il <strong>mazzo spawn</strong> e la
          <strong> mappa a tessere</strong>, più una colonna destra a 5 tab. Dark = modalità primaria.
        </p>
        <Intro />

        {/* INTERACTIVE — desktop */}
        <div className="section-label">Interattivo · Desktop 1280 — fasi + danger · rail survivor · accordion · colonna tab</div>
        <S.DesktopFrame ds={Z.ds} dark label="Desktop · live · Zombicide GH" url="meepleai.app/sessions/zom-4/live?tab=scoring" height={840}
          desc="Top: PhaseTimeline (Fase Giocatori attiva, Round 6) + strip Danger Level (Rosso · pilotato da Bruno). SINISTRA rail del gruppo survivor (clic per cambiare l'attivo) + sintesi plancia. CENTRO accordion (un solo pannello aperto): SurvivorCard · Stato plancia · Mazzo spawn · ChatAgent · ActionLog. DESTRA tab Scoring (BinaryWin) / Dice / Board / Equip / Chat con selettore stato.">
          <S.TopBar ds={Z.ds} connection="connected" />
          <P.DesktopBody initialTab="scoring" initialState="default" />
        </S.DesktopFrame>

        {/* INTERACTIVE — mobile */}
        <div className="section-label">Mobile 375 — banner fase sticky · accordion scroll · survivor drawer · tab → bottom-sheet</div>
        <div className="phones-grid">
          <P.PhoneShell label="01 · base · sheet chiuso" desc="PhaseTimeline sticky, strip Danger + chip survivor (tap → drawer scheda), accordion scrollabile. In basso strip tab." />
          <P.PhoneShell label="02 · sheet Dice" initialTab="dice" desc="Tap su una tab apre il bottom-sheet: dadi D6 rollable (live), conteggio colpi e riferimento armi." />
          <P.PhoneShell label="03 · sheet Board · dark" dark initialTab="board" desc="Tab Board: snapshot tessere con survivor + cluster zombie e mazzo spawn rimanente." />
        </div>

        {/* STATES — reused polymorphic renderers */}
        <div className="section-label">Gallery stati · renderer polimorfici riusati — alimentati dal toolkit Zombicide GH</div>
        <StatesRow title="ScoringPanelRenderer" sub="scoreType · BinaryWin — esito co-op condiviso · 3 categorie dal JSON (Custom · Count · Sum)" entity="session"
          render={(st) => <R.ScoringPanelRenderer data={Z.ds} state={st} />} />
        <StatesRow title="TurnIndicatorRenderer" sub="turnOrderType · Sequential — phase stepper sulle 3 fasi del round" entity="player"
          render={(st) => <R.TurnIndicatorRenderer data={Z.ds} state={st} />} />
        <StatesRow title="ToolkitRenderer" sub="dispatch widgets[] — Counter/DiceTools del JSON (zombie · survivor · dadi · turni)" entity="toolkit"
          render={(st) => <R.ToolkitRenderer widgets={Z.counterWidgets} players={Z.ds.players} state={st} />} />

        {/* STATES — ZOM-specific panels */}
        <div className="section-label">Gallery stati · pannelli specifici Zombicide GH × 5 stati canonici</div>
        <StatesRow title="SurvivorCard" sub="skill tree + equip + ferite + AP" entity="session"
          render={(st) => <div style={{ flex: 1, overflowY: 'auto', padding: 10 }}><P.SurvivorCard state={st} survivor={Z.survivors[0]} layout="rows" /></div>} />
        <StatesRow title="BoardStatePanel" sub="conteggi zombie + Danger Level + obiettivi" entity="event"
          render={(st) => <div style={{ flex: 1, overflowY: 'auto' }}><P.BoardStatePanel state={st} compact /></div>} />
        <StatesRow title="CombatDicePanel" sub="D6 rollable · colpo 4+ · riferimento armi" entity="tool"
          render={(st) => <div style={{ flex: 1, overflowY: 'auto' }}><P.CombatDicePanel state={st} compact /></div>} />
        <StatesRow title="SpawnDeckPanel" sub="carte spawn rimaste · pesca per Danger Level" entity="event"
          render={(st) => <div style={{ flex: 1, overflowY: 'auto' }}><P.SpawnDeckPanel state={st} /></div>} />

        {/* ZOM component showcase */}
        <div className="section-label">Componenti Zombicide GH · banner fase · danger · skill tree · gettoni zombie · dadi · carta morto</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <P.SectionCard title="PhaseTimeline + Danger Level" entity="session" accent flush>
            <F.PhaseTimeline />
            <div style={{ padding: '10px 14px' }}><F.DangerLevel current={Z.dangerLevel} reason={`pilotato da ${Z.topSurvivor.name} (XP ${Z.topSurvivor.xp})`} /></div>
          </P.SectionCard>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 16, alignItems: 'start' }}>
            <div>
              <div style={{ ...mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 8 }}>SkillTree · 4 livelli (Bruno · Rosso)</div>
              <div style={{ padding: 12, borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: '1px solid var(--border)' }}>
                <F.SkillTree survivor={Z.survivors[0]} layout="rows" />
              </div>
            </div>
            <div data-theme="dark">
              <div style={{ ...mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 8 }}>SkillTree · in progress (Tomas · Giallo) · dark</div>
              <div style={{ padding: 12, borderRadius: 'var(--r-lg)', background: 'var(--bg)' }}>
                <F.SkillTree survivor={Z.byId('s-tomas')} layout="rows" />
              </div>
            </div>
          </div>

          <div>
            <div style={{ ...mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 8 }}>ZombieCounter · 6 tipi distintivi</div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(170px, 1fr))', gap: 8 }}>
              {Z.ZORDER.map(t => <F.ZombieCounter key={t} type={t} count={Z.board[t]} big />)}
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 16, alignItems: 'start' }}>
            <div>
              <div style={{ ...mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 8 }}>WoundPips · XPBar · APCounter · EquipCard</div>
              <div style={{ padding: 14, borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: '1px solid var(--border)', display: 'flex', flexDirection: 'column', gap: 12 }}>
                <div style={{ display: 'flex', gap: 14, alignItems: 'center', flexWrap: 'wrap' }}>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, alignItems: 'center' }}><F.WoundPips wounds={0} /><span style={{ ...mono(8, 700, 'var(--text-muted)') }}>0 ferite</span></div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, alignItems: 'center' }}><F.WoundPips wounds={1} /><span style={{ ...mono(8, 700, 'var(--text-muted)') }}>1 ferita</span></div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 4, alignItems: 'center' }}><F.WoundPips wounds={2} /><span style={{ ...mono(8, 700, 'var(--text-muted)') }}>morto</span></div>
                  <F.APCounter ap={4} base={3} bonus={1} />
                </div>
                <F.XPBar xp={31} level="orange" />
                <F.EquipSlots survivor={Z.survivors[0]} cols={2} />
              </div>
            </div>
            <div>
              <div style={{ ...mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 8 }}>Survivor a terra · carta flippata (2 ferite)</div>
              <div style={{ padding: 12, borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: `1px solid ${eHsl('danger', 0.35)}` }}>
                <P.SurvivorCardInner s={{ ...Z.byId('s-greta'), wounds: 2, dead: true }} layout="rows" />
              </div>
            </div>
          </div>

          <P.SectionCard title="MapTilesGrid · snapshot tessere" entity="session" accent flush>
            <div style={{ padding: 12 }}><F.MapTilesGrid focusId="s-bruno" /></div>
          </P.SectionCard>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
        @keyframes mai-pulse-dot-anim { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        @keyframes zom-die-roll-anim { 0% { transform: rotate(-14deg) scale(.86); } 60% { transform: rotate(8deg) scale(1.06); } 100% { transform: rotate(0) scale(1); } }
        .mai-shimmer { background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important; background-size: 200% 100% !important; animation: mai-shimmer-anim 1.4s linear infinite; }
        .mai-pulse-dot { animation: mai-pulse-dot-anim 1.5s ease-in-out infinite; transform-origin: center; display: inline-block; }
        .zom-die-roll { animation: zom-die-roll-anim .4s var(--ease-spring); }
        .mai-cb-scroll::-webkit-scrollbar { height: 7px; width: 7px; }
        .mai-cb-scroll::-webkit-scrollbar-thumb { background: var(--border-strong); border-radius: 999px; }
        .phones-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(380px, 1fr)); gap: var(--s-7); align-items: start; }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage code { background: var(--bg-muted); padding: 1px 6px; border-radius: var(--r-sm); font-size: .85em; font-family: var(--f-mono); }
        @media (prefers-reduced-motion: reduce) { .mai-pulse-dot, .mai-shimmer, .zom-die-roll { animation: none !important; } }
        .phone > div::-webkit-scrollbar { display: none; }
        button:focus-visible, a:focus-visible, input:focus-visible, textarea:focus-visible { outline: 2px solid hsl(var(--c-session)); outline-offset: 2px; }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
