/* MeepleAI SP4 · Zombicide: Green Horde — COMPOSED PARTS  (window.ZOMParts)
   Pannelli specifici Zombicide GH montati sopra lo skeleton universale:
     SectionCard · PanelFrame · STATES
     SurvivorCard       — skill tree + equipment + ferite + AP + zona
     SurvivorsRail      — lista survivor (picker attivo) + sintesi plancia
     BoardStatePanel    — conteggi zombie + Danger Level + obiettivi
     CombatDicePanel    — D6 rollable (interattivo · aria-live) + ref armi
     SpawnDeckPanel     — carte spawn rimaste · pesca per Danger Level
     MapPanel           — snapshot tessere + spawn deck
     EquipOverviewPanel — equipaggiamento di tutti i survivor
     RightColumnTabs    — Scoring · Dice · Board · Equip · Chat
     DesktopBody · MobileBody · PhoneShell

   ACCORDION (CRITICAL · match skeleton): la colonna LEFT replica il pattern
   accordion (un solo pannello expanded; header collapsed = solo titolo;
   aria-expanded). Esteso via helper sec(id) a ChatAgent · ActionLog ·
   Survivor · Board · Spawn (riuso verbatim S.ChatAgentPanel · S.ActionLogTimeline).

   Riusa: S.TopBar · S.ChatAgentPanel · S.ActionLogTimeline · R.ScoringPanelRenderer
          (BinaryWin) · R.StateScaffold · M.StateBlock/Shimmer/SseBanner.
   Legge window.ZOM (dati) · window.ZOMFlavor (atomi) · window.MAI (eHsl). */

(function () {
  const M = window.MAI;
  const R = window.SkeletonRenderers;
  const S = window.SkeletonParts;
  const Z = window.ZOM;
  const F = window.ZOMFlavor;
  const eHsl = M.entityHsl;
  const { useState } = React;
  const { mono, disp } = F;

  const STATES = window.SkeletonData.STATES;

  // ─── SectionCard — contenitore titolato collassabile (accordion) ──────────
  const SectionCard = ({ title, entity = 'session', accent, icon, right, children, pad = 12, flush, collapsed, onHeaderClick, sub }) => {
    const collapsible = !!onHeaderClick;
    const headStyle = { width: '100%', textAlign: 'left', display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px', background: accent ? eHsl(entity, collapsed ? 0.04 : 0.06) : 'var(--bg-muted)', border: 'none', borderBottom: collapsed ? 'none' : `1px solid ${accent ? eHsl(entity, 0.18) : 'var(--border-light)'}`, cursor: collapsible ? 'pointer' : 'default' };
    const headInner = (
      <React.Fragment>
        {icon && <span aria-hidden="true" style={{ fontSize: 13, flexShrink: 0 }}>{icon}</span>}
        <span style={{ ...mono(10, 800, accent ? eHsl(entity) : 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>{title}</span>
        {sub && <span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>{sub}</span>}
        <div style={{ flex: 1 }} />{right}
        {collapsible && <span aria-hidden="true" style={{ ...mono(11, 800, eHsl(entity)), flexShrink: 0, transform: collapsed ? 'rotate(-90deg)' : 'none', transition: 'transform var(--dur-sm) var(--ease-out)' }}>▾</span>}
      </React.Fragment>
    );
    return (
      <section style={{ background: 'var(--bg-card)', border: `1px solid ${accent || collapsible ? eHsl(entity, collapsed ? 0.2 : 0.3) : 'var(--border)'}`, borderRadius: 'var(--r-lg)', overflow: 'hidden', boxShadow: 'var(--shadow-sm)', flexShrink: 0 }}>
        {title && (collapsible
          ? <button type="button" onClick={onHeaderClick} aria-expanded={!collapsed} style={headStyle}>{headInner}</button>
          : <div style={headStyle}>{headInner}</div>)}
        {!collapsed && <div style={{ padding: flush ? 0 : pad }}>{children}</div>}
      </section>
    );
  };

  // ─── PanelFrame — frame rail per la gallery degli stati ───────────────────
  const PanelFrame = ({ label, entity = 'session', dark, children, w = 320, h = 470 }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }} data-theme={dark ? 'dark' : undefined}>
      <div style={{ ...mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>{label}</div>
      <div style={{ width: w, height: h, borderRadius: 'var(--r-lg)', border: `1px solid ${eHsl(entity, 0.3)}`, background: 'var(--bg-card)', overflow: 'hidden', display: 'flex', flexDirection: 'column', boxShadow: 'var(--shadow-sm)' }}>{children}</div>
    </div>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // SurvivorCard — carta survivor ricca (skill tree + equip + ferite + AP)
  // ══════════════════════════════════════════════════════════════════════════
  const SurvivorCardInner = ({ s, layout = 'grid' }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 11, opacity: s.dead ? 0.82 : 1 }}>
      {/* identità + stato */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 11, flexWrap: 'wrap', rowGap: 8 }}>
        <F.SurvivorAvatar s={s} size={46} ring />
        <div style={{ minWidth: 0, flex: 1 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 7, flexWrap: 'wrap' }}>
            <span style={{ ...disp(17, 800, 'var(--text)') }}>{s.name}</span>
            <F.LevelChip level={s.level} />
          </div>
          <div style={{ ...mono(9.5, 700, 'var(--text-muted)') }}>{s.klass} · controllato da {s.player}</div>
        </div>
        <F.WoundPips wounds={s.wounds} dead={s.dead} />
        <F.APCounter ap={s.ap} base={s.apBase} bonus={s.apBonus} />
      </div>
      {/* zona */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 7, padding: '6px 10px', borderRadius: 'var(--r-md)', background: eHsl('session', 0.06), border: `1px solid ${eHsl('session', 0.22)}` }}>
        <span aria-hidden="true" style={{ ...mono(11, 800, eHsl('session')) }}>📍</span>
        <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>Zona</span>
        <span style={{ ...disp(12, 800, 'var(--text)') }}>{s.zone}</span>
      </div>
      {/* XP */}
      <F.XPBar xp={s.xp} level={s.level} />
      {/* skill tree */}
      <div>
        <div style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 6 }}>Albero abilità · 4 livelli</div>
        <F.SkillTree survivor={s} layout={layout} />
      </div>
      {/* equipaggiamento */}
      <div>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, marginBottom: 6 }}>
          <span style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>Equipaggiamento</span>
          <span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>2 mani · corpo · zaino</span>
        </div>
        <F.EquipSlots survivor={s} cols={layout === 'rows' ? 2 : 4} />
      </div>
    </div>
  );
  const SurvivorCard = ({ state = 'default', survivor, layout = 'grid', collapsed, onHeaderClick }) => {
    const s = survivor || Z.survivors[0];
    return (
      <SectionCard title={`Survivor · ${s.name}`} entity="session" accent icon="🧍" pad={12} collapsed={collapsed} onHeaderClick={onHeaderClick}
        right={!collapsed && <F.LevelChip level={s.level} compact />}>
        <R.StateScaffold state={state} sseWhere="survivor"
          empty={{ icon: '🧍', title: 'Nessun survivor', body: 'I survivor compariranno alla creazione del gruppo.' }}
          error={{ title: 'Survivor non disponibile', body: 'Impossibile caricare la scheda del survivor.' }}
          loading={<div style={{ padding: 2, display: 'flex', flexDirection: 'column', gap: 10 }}>{[52, 36, 40, 90, 70].map((h, i) => <M.Shimmer key={i} h={h} />)}</div>}>
          <SurvivorCardInner s={s} layout={layout} />
        </R.StateScaffold>
      </SectionCard>
    );
  };

  // ─── SurvivorsRail — lista survivor (picker) + sintesi plancia ────────────
  const SurvivorsRail = ({ activeId, onPick }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      <SectionCard title="Gruppo survivor" entity="session" accent icon="👥" pad={9}
        right={<span style={{ ...mono(8.5, 800, eHsl('session')), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl('session', 0.12), border: `1px solid ${eHsl('session', 0.3)}` }}>{Z.survivors.filter(s => !s.dead).length}/{Z.survivors.length} vivi</span>}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {Z.survivors.map(s => {
            const on = s.id === activeId;
            return (
              <button key={s.id} type="button" onClick={() => onPick && onPick(s.id)} aria-pressed={on} style={{
                display: 'flex', alignItems: 'center', gap: 8, padding: '7px 8px', borderRadius: 'var(--r-md)', textAlign: 'left', cursor: onPick ? 'pointer' : 'default',
                background: on ? eHsl('session', 0.1) : 'var(--bg-muted)', border: `1px solid ${on ? eHsl('session', 0.4) : 'var(--border-light)'}`,
              }}>
                <F.SurvivorAvatar s={s} size={28} ring={on} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                    <span style={{ ...disp(12.5, 800, 'var(--text)'), overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{s.name}</span>
                    <F.LevelChip level={s.level} compact />
                  </div>
                  <div style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>{s.klass} · ⚡{s.ap} · {s.killTotal} kill</div>
                </div>
                <F.WoundPips wounds={s.wounds} dead={s.dead} size={13} />
              </button>
            );
          })}
        </div>
      </SectionCard>
      <SectionCard title="Sintesi plancia" entity="event" icon="🧟" pad={10}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '7px 10px', borderRadius: 'var(--r-md)', background: eHsl('event', 0.08), border: `1px solid ${eHsl('event', 0.25)}` }}>
            <span aria-hidden="true" style={{ ...disp(22, 800, eHsl('event')), fontVariantNumeric: 'tabular-nums' }}>{Z.zombieTotal}</span>
            <span style={{ ...mono(9, 700, 'var(--text-sec)'), lineHeight: 1.3 }}>zombie<br/>sulla plancia</span>
          </div>
          <F.DangerLevel current={Z.dangerLevel} reason={`XP top · ${Z.topSurvivor.name}`} />
        </div>
      </SectionCard>
    </div>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // BoardStatePanel — conteggi zombie + Danger Level + obiettivi
  // ══════════════════════════════════════════════════════════════════════════
  const BoardStateInner = ({ compact }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, padding: 12 }}>
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginBottom: 8 }}>
          <span style={{ ...mono(9, 800, eHsl('event')), textTransform: 'uppercase', letterSpacing: '.07em' }}>Zombie per tipo</span>
          <span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>{Z.zombieTotal} totali</span>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: `repeat(auto-fill, minmax(${compact ? 130 : 150}px, 1fr))`, gap: 7 }} role="list" aria-label="Conteggi zombie per tipo">
          {Z.ZORDER.map(t => <F.ZombieCounter key={t} type={t} count={Z.board[t]} big={!compact} />)}
        </div>
      </div>
      <div style={{ padding: '9px 11px', borderRadius: 'var(--r-md)', background: eHsl('danger', 0.06), border: `1px solid ${eHsl('danger', 0.28)}` }}>
        <F.DangerLevel current={Z.dangerLevel} reason={`pilotato da ${Z.topSurvivor.name} (XP ${Z.topSurvivor.xp})`} />
        <div style={{ ...mono(8.5, 700, 'var(--text-muted)'), marginTop: 7, lineHeight: 1.5 }}>{Z.DANGER.find(d => d.id === Z.dangerLevel).spawn} · determina l\u2019aggressività dello spawn nella Fase Finale.</div>
      </div>
      <div>
        <div style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 6 }}>Obiettivi scenario</div>
        <div role="list" style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          {Z.objectives.map(o => <F.ObjectiveRow key={o.id} o={o} />)}
        </div>
      </div>
    </div>
  );
  const BoardStatePanel = ({ state = 'default', compact, collapsed, onHeaderClick }) => (
    <SectionCard title="Stato plancia" entity="event" accent icon="🧟" flush collapsed={collapsed} onHeaderClick={onHeaderClick}
      right={!collapsed && <span style={{ ...mono(8.5, 800, eHsl('event')), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl('event', 0.12), border: `1px solid ${eHsl('event', 0.3)}` }}>{Z.zombieTotal} zombie</span>}>
      <R.StateScaffold state={state} sseWhere="plancia"
        empty={{ icon: '🧟', title: 'Plancia pulita', body: 'Nessuno zombie in gioco · in attesa del primo spawn.' }}
        error={{ title: 'Plancia non disponibile', body: 'Impossibile caricare lo stato della plancia.' }}
        loading={<div style={{ padding: 14, display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8 }}>{Array.from({ length: 6 }).map((_, i) => <M.Shimmer key={i} h={52} />)}</div>}>
        <BoardStateInner compact={compact} />
      </R.StateScaffold>
    </SectionCard>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // CombatDicePanel — D6 rollable (interattivo) + riferimento armi
  // ══════════════════════════════════════════════════════════════════════════
  const rollDice = (n) => Array.from({ length: n }, () => 1 + Math.floor(Math.random() * 6));
  const CombatDiceInner = ({ compact }) => {
    const [roll, setRoll] = useState(Z.combat.lastRoll);
    const [rolling, setRolling] = useState(false);
    const [wid, setWid] = useState(Z.combat.weaponId);
    const w = Z.weaponsRef.find(x => x.id === wid) || Z.weaponsRef[0];
    const threshold = Z.combat.threshold;
    const diceCount = compact ? 5 : Z.combat.dice;
    const hits = roll.filter(v => v >= threshold).length;
    const doRoll = () => {
      setRolling(true);
      setRoll(rollDice(diceCount));
      setTimeout(() => setRolling(false), 420);
    };
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12, padding: 12 }}>
        {/* arma + soglia */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', rowGap: 8 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>Arma</span>
            <div role="group" aria-label="Seleziona arma" style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
              {Z.weaponsRef.map(x => {
                const on = x.id === wid;
                return <button key={x.id} type="button" onClick={() => setWid(x.id)} aria-pressed={on} style={{ padding: '3px 9px', borderRadius: 'var(--r-pill)', background: on ? eHsl('tool', 0.14) : 'var(--bg-muted)', border: `1px solid ${on ? eHsl('tool', 0.4) : 'var(--border-light)'}`, color: on ? eHsl('tool') : 'var(--text-sec)', ...disp(10.5, 800), cursor: 'pointer', whiteSpace: 'nowrap' }}>{x.lb}</button>;
              })}
            </div>
          </div>
          <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 7, padding: '4px 10px', borderRadius: 'var(--r-md)', background: eHsl('toolkit', 0.08), border: `1px solid ${eHsl('toolkit', 0.3)}` }}>
            <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em' }}>Colpo</span>
            <span style={{ ...disp(15, 800, eHsl('toolkit')) }}>{threshold}+</span>
          </div>
        </div>
        {/* tray */}
        <div role="status" aria-live="polite" aria-label={`${hits} colpi su ${roll.length} dadi`} style={{ padding: 12, borderRadius: 'var(--r-lg)', background: 'var(--bg-sunken)', border: '1px solid var(--border-light)', display: 'flex', flexDirection: 'column', gap: 10 }}>
          <F.DiceTray roll={roll} threshold={threshold} size={compact ? 34 : 42} />
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
              <span style={{ ...disp(compact ? 24 : 30, 800, eHsl('toolkit')), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{hits}</span>
              <span style={{ ...mono(9.5, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.05em' }}>colpi</span>
            </div>
            <span style={{ ...mono(9, 700, 'var(--text-muted)') }}>{roll.length} dadi · {roll.length - hits} mancati</span>
            <button type="button" onClick={doRoll} disabled={rolling} style={{ marginLeft: 'auto', padding: '8px 16px', borderRadius: 'var(--r-md)', background: eHsl('tool'), color: '#06222b', border: 'none', ...disp(12.5, 800, '#06222b'), cursor: rolling ? 'default' : 'pointer', boxShadow: `0 3px 10px ${eHsl('tool', 0.35)}`, opacity: rolling ? 0.7 : 1 }}>🎲 {rolling ? 'Lancio…' : 'Tira i dadi'}</button>
          </div>
        </div>
        {/* riferimento armi */}
        <div>
          <div style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 6 }}>Riferimento armi · gittata</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
            {Z.weaponsRef.map(x => <F.WeaponRefRow key={x.id} w={x} />)}
          </div>
        </div>
      </div>
    );
  };
  const CombatDicePanel = ({ state = 'default', compact, collapsed, onHeaderClick }) => (
    <SectionCard title="Dadi combattimento" entity="tool" accent icon="🎲" flush collapsed={collapsed} onHeaderClick={onHeaderClick}
      right={!collapsed && <span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>D6 · colpo {Z.combat.threshold}+</span>}>
      <R.StateScaffold state={state} sseWhere="dadi"
        empty={{ icon: '🎲', title: 'Nessun tiro', body: 'Tira i dadi durante un attacco per vedere i colpi.' }}
        error={{ title: 'Dadi non disponibili', body: 'Impossibile caricare lo strumento dadi.' }}
        loading={<div style={{ padding: 14, display: 'flex', gap: 8 }}>{Array.from({ length: 6 }).map((_, i) => <M.Shimmer key={i} h={42} style={{ width: 42 }} />)}</div>}>
        <CombatDiceInner compact={compact} />
      </R.StateScaffold>
    </SectionCard>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // SpawnDeckPanel — carte spawn rimaste · pesca per Danger Level
  // ══════════════════════════════════════════════════════════════════════════
  const SpawnDeckInner = () => {
    const sd = Z.spawnDeck;
    const pct = Math.round((sd.remaining / sd.total) * 100);
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 11, padding: 12 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 5 }}>
            <span style={{ ...disp(32, 800, eHsl('event')), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{sd.remaining}</span>
            <span style={{ ...mono(10, 700, 'var(--text-muted)') }}>/ {sd.total} carte</span>
          </div>
          <div style={{ flex: 1, minWidth: 120 }}>
            <div style={{ height: 10, borderRadius: 'var(--r-pill)', background: 'var(--bg-sunken)', overflow: 'hidden' }}>
              <div style={{ width: `${pct}%`, height: '100%', borderRadius: 'var(--r-pill)', background: eHsl('event') }} />
            </div>
            <div style={{ ...mono(8.5, 700, 'var(--text-muted)'), marginTop: 4 }}>{pct}% del mazzo spawn rimasto · {sd.drawnThisRound} pescate questo round</div>
          </div>
        </div>
        <div>
          <div style={{ ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 6 }}>Pescate per Danger Level</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
            {sd.byDanger.map(d => {
              const m = Z.levelMeta(d.level);
              const on = d.level === Z.dangerLevel;
              return (
                <div key={d.level} aria-current={on ? 'true' : undefined} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 9px', borderRadius: 'var(--r-md)', background: on ? eHsl(m.e, 0.1) : 'var(--bg-muted)', border: `1px solid ${on ? eHsl(m.e, 0.4) : 'var(--border-light)'}` }}>
                  <span aria-hidden="true" style={{ width: 9, height: 9, borderRadius: '50%', background: eHsl(m.e), flexShrink: 0 }} />
                  <span style={{ flex: 1, ...disp(11.5, on ? 800 : 600, on ? eHsl(m.e) : 'var(--text-sec)') }}>Danger {m.lb}</span>
                  {on && <span style={{ ...mono(7.5, 800, eHsl(m.e)), textTransform: 'uppercase' }}>attuale</span>}
                  <span style={{ ...mono(10, 800, 'var(--text)') }}>{d.draws}× / spawn</span>
                </div>
              );
            })}
          </div>
        </div>
        <div style={{ ...mono(9, 700, 'var(--text-muted)'), lineHeight: 1.5, padding: '7px 10px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)' }}>{Z.spawnDeck.note}</div>
      </div>
    );
  };
  const SpawnDeckPanel = ({ state = 'default', collapsed, onHeaderClick }) => (
    <SectionCard title="Mazzo spawn" entity="event" accent icon="🃏" flush collapsed={collapsed} onHeaderClick={onHeaderClick}
      right={!collapsed && <span style={{ ...mono(8.5, 800, eHsl('event')), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl('event', 0.12), border: `1px solid ${eHsl('event', 0.3)}` }}>{Z.spawnDeck.remaining} rim.</span>}>
      <R.StateScaffold state={state} sseWhere="spawn"
        empty={{ icon: '🃏', title: 'Mazzo esaurito', body: 'Nessuna carta spawn rimasta · rimescola gli scarti.' }}
        error={{ title: 'Mazzo non disponibile', body: 'Impossibile caricare il mazzo spawn.' }}
        loading={<div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 8 }}>{Array.from({ length: 4 }).map((_, i) => <M.Shimmer key={i} h={32} />)}</div>}>
        <SpawnDeckInner />
      </R.StateScaffold>
    </SectionCard>
  );

  // ─── MapPanel — snapshot tessere + mazzo spawn (tab Board) ────────────────
  const MapPanel = ({ state = 'default', focusId, h = 340 }) => (
    <SectionCard title="Mappa · tessere" entity="session" accent icon="🗺" flush
      right={<span style={{ ...mono(8.5, 700, 'var(--text-muted)') }}>{Z.mapTiles.scenario}</span>}>
      <R.StateScaffold state={state} sseWhere="mappa"
        empty={{ icon: '🗺', title: 'Mappa vuota', body: 'Le tessere compariranno all\u2019avvio dello scenario.' }}
        error={{ title: 'Mappa non disponibile', body: 'Impossibile caricare lo snapshot della mappa.' }}
        loading={<div style={{ padding: 14 }}><M.Shimmer h={h - 20} /></div>}>
        <div style={{ padding: 10 }}>
          <F.MapTilesGrid focusId={focusId} />
        </div>
      </R.StateScaffold>
    </SectionCard>
  );

  // ─── EquipOverviewPanel — equip di tutti i survivor (tab Equip) ────────────
  const EquipOverviewPanel = ({ state = 'default' }) => (
    <R.StateScaffold state={state} sseWhere="equipaggiamento"
      empty={{ icon: '🎒', title: 'Nessun equipaggiamento', body: 'Cerca nelle zone per trovare armi e oggetti.' }}
      error={{ title: 'Equipaggiamento non disponibile', body: 'Impossibile caricare l\u2019inventario dei survivor.' }}
      loading={<div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 10 }}>{Array.from({ length: 4 }).map((_, i) => <M.Shimmer key={i} h={80} />)}</div>}>
      <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 12, display: 'flex', flexDirection: 'column', gap: 12 }} className="mai-cb-scroll">
        {Z.survivors.map(s => (
          <div key={s.id}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginBottom: 6 }}>
              <F.SurvivorAvatar s={s} size={22} />
              <span style={{ ...disp(12.5, 800, 'var(--text)') }}>{s.name}</span>
              <F.LevelChip level={s.level} compact />
              {s.dead && <span style={{ ...mono(8, 800, eHsl('danger')), textTransform: 'uppercase' }}>a terra</span>}
            </div>
            <F.EquipSlots survivor={s} cols={2} />
          </div>
        ))}
      </div>
    </R.StateScaffold>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // RightColumnTabs — Scoring · Dice · Board · Equip · Chat
  // ══════════════════════════════════════════════════════════════════════════
  const RC_TABS = [
    { id: 'scoring', icon: '🎯', label: 'Scoring', entity: 'session', stateful: true,  render: (st) => <R.ScoringPanelRenderer data={Z.ds} state={st} /> },
    { id: 'dice',    icon: '🎲', label: 'Dice',    entity: 'tool',    stateful: false, render: (st) => <div style={{ flex: 1, minHeight: 0, overflowY: 'auto' }}><CombatDicePanel state={st} compact /></div> },
    { id: 'board',   icon: '🗺', label: 'Board',   entity: 'event',   stateful: true,  render: (st) => <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 12, display: 'flex', flexDirection: 'column', gap: 12 }} className="mai-cb-scroll"><MapPanel state={st} focusId="s-bruno" /><SpawnDeckPanel state={st} /></div> },
    { id: 'equip',   icon: '🎒', label: 'Equip',   entity: 'kb',      stateful: true,  render: (st) => <EquipOverviewPanel state={st} /> },
    { id: 'chat',    icon: '💬', label: 'Chat',    entity: 'agent',   stateful: false, render: () => <div style={{ flex: 1, minHeight: 0, padding: 12, display: 'flex' }}><S.ChatAgentPanel ds={Z.ds} /></div> },
  ];
  const RightColumnTabs = ({ initial = 'scoring', initialState = 'default', embedded }) => {
    const [tab, setTab] = useState(initial);
    const [st, setSt] = useState(initialState);
    const active = RC_TABS.find(t => t.id === tab) || RC_TABS[0];
    return (
      <aside aria-label="Pannello sessione" style={{ width: embedded ? '100%' : 348, minWidth: embedded ? 0 : 300, flexShrink: 0, height: '100%', background: 'var(--bg-card)', borderLeft: embedded ? 'none' : '1px solid var(--border)', display: 'flex', flexDirection: 'column', minHeight: 0 }}>
        <div role="tablist" aria-label="Sezioni" style={{ display: 'flex', borderBottom: '1px solid var(--border-light)', flexShrink: 0, overflow: 'hidden' }}>
          {RC_TABS.map(t => {
            const on = tab === t.id;
            return (
              <button key={t.id} type="button" role="tab" aria-selected={on} aria-label={t.label} onClick={() => setTab(t.id)} style={{ flex: '1 1 0', minWidth: 0, padding: '10px 3px', background: on ? eHsl(t.entity, 0.06) : 'transparent', border: 'none', borderBottom: on ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent', color: on ? eHsl(t.entity) : 'var(--text-sec)', ...disp(10.5, 800), cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 4, whiteSpace: 'nowrap' }}>
                <span aria-hidden="true" style={{ fontSize: 12, flexShrink: 0 }}>{t.icon}</span><span style={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis' }}>{t.label}</span>
              </button>
            );
          })}
        </div>
        {active.stateful && <S.StateSwitch value={st} onChange={setSt} />}
        <div role="tabpanel" style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(st)}</div>
      </aside>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // DESKTOP body — banner fase + danger · rail survivor · accordion · tabs
  // ══════════════════════════════════════════════════════════════════════════
  const DesktopBody = ({ initialTab, initialState }) => {
    const [focusId, setFocusId] = useState('s-bruno');
    const [expanded, setExpanded] = useState('survivor');
    const sec = (id) => ({ collapsed: expanded !== id, onHeaderClick: () => setExpanded(id) });
    const survivor = Z.byId(focusId);
    return (
      <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
        <F.PhaseTimeline />
        {/* danger strip */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '8px 16px', background: 'var(--bg-card)', borderBottom: '1px solid var(--border)', flexWrap: 'wrap', rowGap: 6, flexShrink: 0 }}>
          <F.DangerLevel current={Z.dangerLevel} reason={`pilotato da ${Z.topSurvivor.name} (XP ${Z.topSurvivor.xp})`} />
          <span aria-hidden="true" style={{ width: 1, alignSelf: 'stretch', background: 'var(--border)', flexShrink: 0 }} />
          <span style={{ ...mono(9, 700, 'var(--text-sec)') }}>🧟 {Z.zombieTotal} zombie · 🃏 {Z.spawnDeck.remaining} spawn · ★ {Z.objectives.filter(o => o.done).length}/{Z.objectives.length} obiettivi</span>
        </div>
        <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
          {/* LEFT — survivors rail */}
          <div className="mai-cb-scroll" style={{ width: 258, flexShrink: 0, borderRight: '1px solid var(--border)', overflowY: 'auto', padding: 12, background: 'var(--bg)' }}>
            <SurvivorsRail activeId={focusId} onPick={setFocusId} />
          </div>
          {/* CENTER — accordion (un solo pannello expanded · include ChatAgent + ActionLog) */}
          <main className="mai-cb-scroll" style={{ flex: 1, minWidth: 0, overflowY: 'auto', padding: 12, display: 'flex', flexDirection: 'column', gap: 10, background: 'var(--bg)' }}>
            <SurvivorCard survivor={survivor} {...sec('survivor')} />
            <BoardStatePanel {...sec('board')} />
            <SpawnDeckPanel {...sec('spawn')} />
            <S.ChatAgentPanel ds={Z.ds} {...sec('chat')} />
            <S.ActionLogTimeline ds={Z.ds} {...sec('log')} />
          </main>
          {/* RIGHT — tabs */}
          <RightColumnTabs initial={initialTab} initialState={initialState} />
        </div>
      </div>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // MOBILE — banner fase sticky · accordion scroll · tab strip → bottom sheet
  //          + survivor card in bottom-sheet drawer al tap
  // ══════════════════════════════════════════════════════════════════════════
  const BottomSheet = ({ open, label, icon, entity, onClose, children, stateSwitch }) => (
    <div aria-hidden={!open} style={{ position: 'absolute', inset: 0, zIndex: 50, pointerEvents: open ? 'auto' : 'none' }}>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(8,12,8,.55)', backdropFilter: 'blur(3px)', opacity: open ? 1 : 0, transition: 'opacity var(--dur-md) var(--ease-out)' }} />
      <div role="dialog" aria-modal="true" aria-label={label} style={{ position: 'absolute', left: 0, right: 0, bottom: 0, height: '86%', background: 'var(--bg-card)', borderTopLeftRadius: 'var(--r-2xl)', borderTopRightRadius: 'var(--r-2xl)', borderTop: `3px solid ${eHsl(entity)}`, boxShadow: 'var(--shadow-drawer)', display: 'flex', flexDirection: 'column', minHeight: 0, transform: open ? 'translateY(0)' : 'translateY(101%)', transition: 'transform var(--dur-lg) var(--ease-spring)' }}>
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', paddingTop: 8, flexShrink: 0 }}>
          <div style={{ width: 38, height: 4, borderRadius: 999, background: 'var(--border-strong)' }} />
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 14px 10px', flexShrink: 0 }}>
          <span aria-hidden="true" style={{ fontSize: 16 }}>{icon}</span>
          <span style={{ ...disp(15, 800, eHsl(entity)) }}>{label}</span>
          <div style={{ flex: 1 }} />
          <button type="button" onClick={onClose} aria-label="Chiudi" style={{ width: 28, height: 28, borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontSize: 13 }}>✕</button>
        </div>
        {stateSwitch}
        <div className="mai-cb-scroll" style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column', overflowY: 'auto' }}>{children}</div>
      </div>
    </div>
  );

  const MobileTabSheet = ({ open, tab, st, setSt, onClose }) => {
    const active = RC_TABS.find(t => t.id === tab) || RC_TABS[0];
    return (
      <BottomSheet open={open} label={active.label} icon={active.icon} entity={active.entity} onClose={onClose}
        stateSwitch={active.stateful ? <S.StateSwitch value={st} onChange={setSt} /> : null}>
        <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(st)}</div>
      </BottomSheet>
    );
  };

  const MobileBody = ({ initialTab }) => {
    const [sheet, setSheet] = useState(initialTab || null);
    const [st, setSt] = useState('default');
    const [survDrawer, setSurvDrawer] = useState(null);
    const [expanded, setExpanded] = useState('survivor');
    const sec = (id) => ({ collapsed: expanded !== id, onHeaderClick: () => setExpanded(id) });
    const drawerSurv = survDrawer ? Z.byId(survDrawer) : null;
    return (
      <>
        <F.PhaseTimeline compact sticky />
        <div className="mai-cb-scroll" style={{ flex: 1, overflowY: 'auto', minHeight: 0, background: 'var(--bg)' }}>
          {/* danger + survivor chips (tap → drawer) */}
          <div style={{ padding: '9px 12px', display: 'flex', flexDirection: 'column', gap: 9, borderBottom: '1px solid var(--border)', background: 'var(--bg-card)' }}>
            <F.DangerLevel current={Z.dangerLevel} compact reason={Z.topSurvivor.name} />
            <div className="mai-cb-scroll" style={{ display: 'flex', gap: 6, overflowX: 'auto' }}>
              {Z.survivors.map(s => (
                <button key={s.id} type="button" onClick={() => setSurvDrawer(s.id)} style={{ flex: '0 0 auto', display: 'inline-flex', alignItems: 'center', gap: 6, padding: '4px 10px 4px 4px', borderRadius: 'var(--r-pill)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)', cursor: 'pointer' }}>
                  <F.SurvivorAvatar s={s} size={22} />
                  <span style={{ ...disp(11, 800, 'var(--text)') }}>{s.name}</span>
                  <F.WoundPips wounds={s.wounds} dead={s.dead} size={11} />
                </button>
              ))}
            </div>
          </div>
          <div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 10 }}>
            <SurvivorCard survivor={Z.survivors[0]} layout="rows" {...sec('survivor')} />
            <BoardStatePanel compact {...sec('board')} />
            <SpawnDeckPanel {...sec('spawn')} />
            <S.ChatAgentPanel ds={Z.ds} compact {...sec('chat')} />
            <S.ActionLogTimeline ds={Z.ds} compact {...sec('log')} />
          </div>
        </div>
        <nav aria-label="Sezioni sessione" style={{ display: 'flex', gap: 5, flexShrink: 0, padding: '8px 8px', background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderTop: '1px solid var(--border)' }}>
          {RC_TABS.map(t => (
            <button key={t.id} type="button" onClick={() => { setSt('default'); setSheet(t.id); }} style={{ flex: '1 1 0', minWidth: 0, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 4, padding: '7px 4px', borderRadius: 'var(--r-pill)', background: eHsl(t.entity, 0.1), border: `1px solid ${eHsl(t.entity, 0.28)}`, color: eHsl(t.entity), ...disp(10.5, 800), cursor: 'pointer', whiteSpace: 'nowrap' }}>
              <span aria-hidden="true" style={{ flexShrink: 0 }}>{t.icon}</span><span style={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis' }}>{t.label}</span>
            </button>
          ))}
        </nav>
        <MobileTabSheet open={!!sheet} tab={sheet} st={st} setSt={setSt} onClose={() => setSheet(null)} />
        {/* survivor bottom-sheet drawer al tap */}
        <BottomSheet open={!!drawerSurv} label={drawerSurv ? `Survivor · ${drawerSurv.name}` : ''} icon="🧍" entity="session" onClose={() => setSurvDrawer(null)}>
          {drawerSurv && <div style={{ padding: 12 }}><SurvivorCardInner s={drawerSurv} layout="rows" /></div>}
        </BottomSheet>
      </>
    );
  };

  // ─── PhoneShell ───────────────────────────────────────────────────────────
  const PhoneSbar = () => (
    <div className="phone-sbar" style={{ color: 'var(--text)' }}>
      <span style={{ fontFamily: 'var(--f-mono)' }}>21:14</span>
      <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
    </div>
  );
  const PhoneShell = ({ label, desc, dark, initialTab }) => (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
      <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark</span>}</div>
      <div className="phone" data-theme={dark ? 'dark' : undefined}>
        <PhoneSbar />
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', position: 'relative', overflow: 'hidden', minHeight: 0 }}>
          <S.TopBar ds={Z.ds} compact />
          <MobileBody initialTab={initialTab} />
        </div>
      </div>
      {desc && <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 340, textAlign: 'center', lineHeight: 1.55 }}>{desc}</div>}
    </div>
  );

  window.ZOMParts = {
    STATES, SectionCard, PanelFrame,
    SurvivorCard, SurvivorCardInner, SurvivorsRail, BoardStatePanel, CombatDicePanel,
    SpawnDeckPanel, MapPanel, EquipOverviewPanel, RightColumnTabs,
    DesktopBody, MobileBody, PhoneShell,
  };
})();
