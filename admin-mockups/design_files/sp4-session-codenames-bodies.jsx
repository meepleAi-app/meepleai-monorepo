/* MeepleAI SP4 · Codenames — BODIES & PANELS  (extends window.CNParts)
   TeamPanel · ClueHistoryTimeline · RightColumnTabs · DesktopBody ·
   MobileBody · MobileSheet · PhoneShell.

   Accordion contract (mandatory): the CENTER/main column stacks the
   always-visible WordGrid above a single-open accordion of
   ChatAgentPanel · ActionLogTimeline · ClueHistoryTimeline — exactly one
   expanded, switched via sec(id), identical to the skeleton DesktopSessionBody
   and the Power Grid extension. */

(function () {
  const M = window.MAI;
  const R = window.SkeletonRenderers;
  const S = window.SkeletonParts;
  const CN = window.CN;
  const F = window.CNFlavor;
  const P = window.CNParts;
  const eHsl = M.entityHsl;
  const { useState } = React;
  const { mono, disp, teamEntity, teamName, RoleAvatar, RoleTag, ClueChip, ClueOutcome } = F;
  const { SectionCard, PhaseBanner, CurrentCluePanel, WordGrid, SpymasterKeyCardOverlay, GameOverBanner, ScenarioSwitcher } = P;
  const STATES = window.SkeletonData.STATES;

  // ══════════════════════════════════════════════════════════════════════════
  // TeamPanel — red + blue trackers · current highlighted · rosters
  // ══════════════════════════════════════════════════════════════════════════
  const TeamCard = ({ team, compact }) => {
    const t = team;
    const teamId = t.id;
    const e = teamEntity(teamId);
    const spy = CN.byId(t.spymasterId);
    const ops = t.operativeIds.map(CN.byId);
    const on = t.current;
    return (
      <div aria-label={`Squadra ${t.name}`} style={{
        borderRadius: 'var(--r-lg)', overflow: 'hidden', background: 'var(--bg-card)', flexShrink: 0,
        border: `${on ? 2 : 1}px solid ${eHsl(e, on ? 0.55 : 0.28)}`,
        boxShadow: on ? `0 5px 18px ${eHsl(e, 0.2)}` : 'var(--shadow-sm)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 11px', background: eHsl(e, on ? 0.12 : 0.06), borderBottom: `1px solid ${eHsl(e, 0.2)}` }}>
          <span aria-hidden="true" style={{ width: 12, height: 12, borderRadius: 3, background: eHsl(e), flexShrink: 0 }} />
          <span style={{ ...disp(13.5, 800, 'var(--text)') }}>Squadra {t.name}</span>
          {on && <span className="mai-pulse-dot-host" style={{ ...mono(8, 800, eHsl(e)), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl(e, 0.14), border: `1px solid ${eHsl(e, 0.35)}`, textTransform: 'uppercase', letterSpacing: '.05em', display: 'inline-flex', alignItems: 'center', gap: 4 }}><span className="mai-pulse-dot" aria-hidden="true">●</span>al turno</span>}
          <div style={{ flex: 1 }} />
          <div style={{ textAlign: 'right', lineHeight: 1 }}>
            <span style={{ ...disp(compact ? 20 : 24, 800, eHsl(e)), fontVariantNumeric: 'tabular-nums' }}>{t.remaining}</span>
            <span style={{ ...mono(10, 700, 'var(--text-muted)') }}> / {t.total}</span>
          </div>
        </div>
        <div style={{ padding: '8px 11px', display: 'flex', flexDirection: 'column', gap: 8 }}>
          {/* progress of agents found */}
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 4 }}>
              <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em' }}>Agenti trovati</span>
              <span style={{ ...mono(10, 800, eHsl(e)) }}>{t.found}/{t.total}</span>
            </div>
            <div style={{ height: 7, borderRadius: 'var(--r-pill)', background: 'var(--bg-sunken)', overflow: 'hidden', display: 'flex', gap: 2, padding: 1 }}>
              {Array.from({ length: t.total }).map((_, i) => (
                <span key={i} style={{ flex: 1, borderRadius: 1, background: i < t.found ? eHsl(e) : eHsl(e, 0.16) }} />
              ))}
            </div>
          </div>
          {/* roster */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <RoleAvatar p={spy} size={26} ring={on} />
            <div style={{ minWidth: 0, flex: 1 }}>
              <div style={{ ...disp(12, 800, 'var(--text)') }}>{spy.name}</div>
              <RoleTag role="spymaster" team={teamId} />
            </div>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', rowGap: 6 }}>
            <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em', flexShrink: 0 }}>Operativi</span>
            {ops.map((p) => (
              <span key={p.id} style={{ display: 'inline-flex', alignItems: 'center', gap: 5, padding: '2px 9px 2px 2px', borderRadius: 'var(--r-pill)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)' }}>
                <RoleAvatar p={p} size={19} />
                <span style={{ ...disp(11, 700, 'var(--text)') }}>{p.name}</span>
                {on && <span className="mai-pulse-dot" aria-label="al turno" style={{ ...mono(7, 800, eHsl(e)) }}>●</span>}
              </span>
            ))}
          </div>
        </div>
      </div>
    );
  };

  const TeamPanel = ({ state = 'default', compact, teams = CN.teams, order = ['red', 'blue'] }) => (
    <R.StateScaffold state={state} sseWhere="squadre"
      empty={{ icon: '👥', title: 'Squadre non formate', body: 'Assegna giocatori a Rosso e Blu per iniziare.' }}
      error={{ title: 'Squadre non disponibili', body: 'Impossibile caricare i roster delle squadre.' }}
      loading={<div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 10 }}>{[0, 1].map((i) => <M.Shimmer key={i} h={150} style={{ borderRadius: 'var(--r-lg)' }} />)}</div>}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10, padding: compact ? 0 : 0 }}>
        {order.map((id) => <TeamCard key={id} team={teams[id]} compact={compact} />)}
      </div>
    </R.StateScaffold>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // ClueHistoryTimeline — clues given · success ratio · best/worst
  // ══════════════════════════════════════════════════════════════════════════
  const ClueRow = ({ clue }) => {
    const e = teamEntity(clue.team);
    const ratio = `${clue.correct}/${clue.number}`;
    return (
      <div style={{ display: 'flex', gap: 10, padding: '8px 0' }}>
        <span aria-hidden="true" style={{ width: 10, height: 10, marginTop: 5, borderRadius: '50%', flexShrink: 0, background: eHsl(e), boxShadow: `0 0 0 3px ${eHsl(e, 0.18)}` }} />
        <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 5 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', rowGap: 5 }}>
            <ClueChip word={clue.word} number={clue.number} team={clue.team} dim={clue.outcome !== 'active'} />
            {clue.best && <span title="Miglior indizio" style={{ ...mono(8.5, 800, eHsl('toolkit')), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl('toolkit', 0.12), border: `1px solid ${eHsl('toolkit', 0.3)}` }}>★ migliore</span>}
            {clue.worst && <span title="Indizio peggiore" style={{ ...mono(8.5, 800, eHsl('event')), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl('event', 0.12), border: `1px solid ${eHsl('event', 0.3)}` }}>peggiore</span>}
            <div style={{ flex: 1 }} />
            <span style={{ ...mono(11, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{ratio}</span>
            <ClueOutcome outcome={clue.outcome} />
          </div>
          <span style={{ ...mono(9, 700, 'var(--text-muted)') }}>{teamName(clue.team)} · Spymaster {CN.byId(clue.spymaster).name}{clue.note ? ` · ${clue.note}` : ''}</span>
        </div>
      </div>
    );
  };

  const ClueHistoryInner = ({ clues }) => {
    const list = [...clues].reverse(); // newest first
    const done = clues.filter((c) => c.outcome !== 'active');
    const clean = done.filter((c) => c.outcome === 'clean').length;
    return (
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '4px 0 8px', flexWrap: 'wrap' }}>
          <span style={{ ...mono(9.5, 700, 'var(--text-sec)') }}>Indizi pieni</span>
          <span style={{ ...mono(11, 800, eHsl('toolkit')) }}>{clean}/{done.length}</span>
          <div style={{ flex: 1 }} />
          <span style={{ ...mono(9, 700, 'var(--text-muted)') }}>{clues.length} indizi dati</span>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column' }}>
          {list.map((c) => <ClueRow key={c.id} clue={c} />)}
        </div>
      </div>
    );
  };

  const ClueHistoryTimeline = ({ state = 'default', clues = CN.CLUES, collapsed, onHeaderClick }) => (
    <SectionCard title="Storico indizi" entity="agent" accent={!!onHeaderClick} collapsed={collapsed} onHeaderClick={onHeaderClick}
      right={<span style={{ ...mono(9, 800, eHsl('agent')), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl('agent', 0.1), border: `1px solid ${eHsl('agent', 0.28)}` }}>{clues.length}</span>}>
      <R.StateScaffold state={state} sseWhere="indizi"
        empty={{ icon: '💬', title: 'Nessun indizio', body: 'Gli indizi dati dagli Spymaster compaiono qui.' }}
        error={{ title: 'Storico non disponibile', body: 'Impossibile caricare lo storico degli indizi.' }}>
        <ClueHistoryInner clues={clues} />
      </R.StateScaffold>
    </SectionCard>
  );

  // ══════════════════════════════════════════════════════════════════════════
  // RightColumnTabs — Scoring · Board · Clue history · Teams · Chat
  // ══════════════════════════════════════════════════════════════════════════
  const BoardTab = ({ state, board = CN.BOARD }) => (
    <div className="mai-cb-scroll" style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 12, display: 'flex', flexDirection: 'column', gap: 10 }}>
      <div style={{ ...mono(9.5, 700, 'var(--text-muted)'), lineHeight: 1.5 }}>Snapshot della griglia con la <strong style={{ color: eHsl('session') }}>key card</strong> dello Spymaster — vista segreta.</div>
      <WordGrid state={state} compact perspective="spymaster" keyVisible board={board} />
    </div>
  );

  const RC_TABS = [
    { id: 'scoring', icon: '🎯', label: 'Scoring', entity: 'session', render: (st, scn) => <R.ScoringPanelRenderer data={scn.ds} state={st} /> },
    { id: 'board',   icon: '🗂', label: 'Board',   entity: 'session', render: (st, scn) => <BoardTab state={st} board={scn.board} /> },
    { id: 'clues',   icon: '💬', label: 'Indizi',  entity: 'agent',   render: (st, scn) => <div className="mai-cb-scroll" style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 12 }}><ClueHistoryTimeline state={st} clues={scn.clues} /></div> },
    { id: 'teams',   icon: '👥', label: 'Squadre', entity: 'player',  render: (st, scn) => <div className="mai-cb-scroll" style={{ flex: 1, minHeight: 0, overflowY: 'auto', padding: 12 }}><TeamPanel state={st} teams={scn.teams} /></div> },
    { id: 'chat',    icon: '💬', label: 'Chat',    entity: 'agent',   render: () => <div style={{ flex: 1, minHeight: 0, padding: 12, display: 'flex' }}><S.ChatAgentPanel ds={CN.ds} /></div> },
  ];
  const RightColumnTabs = ({ initial = 'scoring', initialState = 'default', embedded, scn = CN.SCENARIOS.mid }) => {
    const [tab, setTab] = useState(initial);
    const [st, setSt] = useState(initialState);
    const active = RC_TABS.find((t) => t.id === tab) || RC_TABS[0];
    return (
      <aside aria-label="Pannello sessione" style={{ width: embedded ? '100%' : 352, minWidth: embedded ? 0 : 300, flexShrink: 0, height: '100%', background: 'var(--bg-card)', borderLeft: embedded ? 'none' : '1px solid var(--border)', display: 'flex', flexDirection: 'column', minHeight: 0 }}>
        <div role="tablist" aria-label="Sezioni" style={{ display: 'flex', borderBottom: '1px solid var(--border-light)', flexShrink: 0, overflow: 'hidden' }}>
          {RC_TABS.map((t) => {
            const on = tab === t.id;
            return (
              <button key={t.id} type="button" role="tab" aria-selected={on} aria-label={t.label} onClick={() => setTab(t.id)} style={{ flex: '1 1 0', minWidth: 0, padding: '10px 3px', background: on ? eHsl(t.entity, 0.06) : 'transparent', border: 'none', borderBottom: on ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent', color: on ? eHsl(t.entity) : 'var(--text-sec)', ...disp(10.5, 800), cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 4, whiteSpace: 'nowrap' }}>
                <span aria-hidden="true" style={{ fontSize: 12, flexShrink: 0 }}>{t.icon}</span><span style={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis' }}>{t.label}</span>
              </button>
            );
          })}
        </div>
        {active.id !== 'chat' && <S.StateSwitch value={st} onChange={setSt} />}
        <div role="tabpanel" style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(st, scn)}</div>
      </aside>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // DESKTOP body — phase band · clue band · 3 columns (teams · board+accordion · tabs)
  // ══════════════════════════════════════════════════════════════════════════
  const DesktopBody = ({ initialTab, initialState, initialPerspective = 'operative', initialScenario = 'mid' }) => {
    const [scnId, setScnId] = useState(initialScenario);
    const [perspective, setPerspective] = useState(initialPerspective);
    const [keyVisible, setKeyVisible] = useState(true);
    const [expanded, setExpanded] = useState('chat'); // 'chat' | 'log' | 'clues'
    const sec = (id) => ({ collapsed: expanded !== id, onHeaderClick: () => setExpanded(id) });
    const scn = CN.SCENARIOS[scnId];
    return (
      <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
        <ScenarioSwitcher value={scnId} onChange={setScnId} />
        <PhaseBanner phaseState={scn.phaseState} />
        <div style={{ padding: '12px 14px 0' }}>{scn.gameOver ? <GameOverBanner over={scn.gameOver} /> : <CurrentCluePanel clue={scn.clue} />}</div>
        <div style={{ display: 'flex', flex: 1, minHeight: 0, marginTop: 12 }}>
          {/* LEFT — team trackers */}
          <div className="mai-cb-scroll" style={{ width: 264, flexShrink: 0, borderRight: '1px solid var(--border)', overflowY: 'auto', padding: 12, background: 'var(--bg)' }}>
            <TeamPanel teams={scn.teams} />
          </div>
          {/* CENTER — word grid (always) + single-open accordion */}
          <main className="mai-cb-scroll" style={{ flex: 1, minWidth: 0, overflowY: 'auto', padding: 12, display: 'flex', flexDirection: 'column', gap: 10, background: 'var(--bg)' }}>
            <WordGrid perspective={perspective} keyVisible={keyVisible} board={scn.board}
              onTogglePerspective={setPerspective} onToggleKey={() => setKeyVisible((v) => !v)} />
            <S.ChatAgentPanel ds={CN.ds} {...sec('chat')} />
            <S.ActionLogTimeline ds={scn.ds} {...sec('log')} />
            <ClueHistoryTimeline clues={scn.clues} {...sec('clues')} />
          </main>
          {/* RIGHT — polymorphic tabs */}
          <RightColumnTabs initial={initialTab} initialState={initialState} scn={scn} />
        </div>
      </div>
    );
  };

  // ══════════════════════════════════════════════════════════════════════════
  // MOBILE — phase sticky · scroll body · tab strip → bottom sheet
  // ══════════════════════════════════════════════════════════════════════════
  const MobileSheet = ({ open, tab, st, setSt, onClose, scn }) => {
    const active = RC_TABS.find((t) => t.id === tab) || RC_TABS[0];
    return (
      <div aria-hidden={!open} style={{ position: 'absolute', inset: 0, zIndex: 50, pointerEvents: open ? 'auto' : 'none' }}>
        <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(15,12,8,.5)', backdropFilter: 'blur(3px)', opacity: open ? 1 : 0, transition: 'opacity var(--dur-md) var(--ease-out)' }} />
        <div role="dialog" aria-modal="true" aria-label={active.label} style={{ position: 'absolute', left: 0, right: 0, bottom: 0, height: '86%', background: 'var(--bg-card)', borderTopLeftRadius: 'var(--r-2xl)', borderTopRightRadius: 'var(--r-2xl)', borderTop: `3px solid ${eHsl(active.entity)}`, boxShadow: 'var(--shadow-drawer)', display: 'flex', flexDirection: 'column', minHeight: 0, transform: open ? 'translateY(0)' : 'translateY(101%)', transition: 'transform var(--dur-lg) var(--ease-spring)' }}>
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', paddingTop: 8, flexShrink: 0 }}>
            <div style={{ width: 38, height: 4, borderRadius: 999, background: 'var(--border-strong)' }} />
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 14px 10px', flexShrink: 0 }}>
            <span aria-hidden="true" style={{ fontSize: 16 }}>{active.icon}</span>
            <span style={{ ...disp(15, 800, eHsl(active.entity)) }}>{active.label}</span>
            <div style={{ flex: 1 }} />
            <button type="button" onClick={onClose} aria-label="Chiudi" style={{ width: 28, height: 28, borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontSize: 13 }}>✕</button>
          </div>
          {active.id !== 'chat' && <S.StateSwitch value={st} onChange={setSt} />}
          <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(st, scn)}</div>
        </div>
      </div>
    );
  };

  const MobileBody = ({ initialTab, initialPerspective = 'operative', initialScenario = 'mid' }) => {
    const [sheet, setSheet] = useState(initialTab || null);
    const [st, setSt] = useState('default');
    const [scnId, setScnId] = useState(initialScenario);
    const [perspective, setPerspective] = useState(initialPerspective);
    const [keyVisible, setKeyVisible] = useState(true);
    const [expanded, setExpanded] = useState('chat');
    const sec = (id) => ({ collapsed: expanded !== id, onHeaderClick: () => setExpanded(id) });
    const scn = CN.SCENARIOS[scnId];
    return (
      <>
        <ScenarioSwitcher value={scnId} onChange={setScnId} compact />
        <PhaseBanner compact sticky phaseState={scn.phaseState} />
        <div className="mai-cb-scroll" style={{ flex: 1, overflowY: 'auto', minHeight: 0, background: 'var(--bg)' }}>
          <div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 10 }}>
            {scn.gameOver ? <GameOverBanner over={scn.gameOver} compact /> : <CurrentCluePanel compact clue={scn.clue} />}
            <WordGrid compact perspective={perspective} keyVisible={keyVisible} board={scn.board}
              onTogglePerspective={setPerspective} onToggleKey={() => setKeyVisible((v) => !v)} />
            <TeamPanel compact teams={scn.teams} />
            <S.ChatAgentPanel ds={CN.ds} compact {...sec('chat')} />
            <S.ActionLogTimeline ds={scn.ds} compact {...sec('log')} />
            <ClueHistoryTimeline clues={scn.clues} {...sec('clues')} />
          </div>
        </div>
        <nav aria-label="Sezioni sessione" style={{ display: 'flex', gap: 5, flexShrink: 0, padding: '8px 8px', background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderTop: '1px solid var(--border)' }}>
          {RC_TABS.map((t) => (
            <button key={t.id} type="button" onClick={() => { setSt('default'); setSheet(t.id); }} style={{ flex: '1 1 0', minWidth: 0, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 4, padding: '7px 4px', borderRadius: 'var(--r-pill)', background: eHsl(t.entity, 0.1), border: `1px solid ${eHsl(t.entity, 0.28)}`, color: eHsl(t.entity), ...disp(10, 800), cursor: 'pointer', whiteSpace: 'nowrap' }}>
              <span aria-hidden="true" style={{ flexShrink: 0 }}>{t.icon}</span><span style={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis' }}>{t.label}</span>
            </button>
          ))}
        </nav>
        <MobileSheet open={!!sheet} tab={sheet} st={st} setSt={setSt} scn={scn} onClose={() => setSheet(null)} />
      </>
    );
  };

  // ─── PhoneShell ───────────────────────────────────────────────────────────
  const PhoneSbar = () => (
    <div className="phone-sbar" style={{ color: 'var(--text)' }}>
      <span style={{ fontFamily: 'var(--f-mono)' }}>20:15</span>
      <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
    </div>
  );
  const PhoneShell = ({ label, desc, dark, initialTab, initialPerspective, initialScenario }) => (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
      <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark</span>}</div>
      <div className="phone" data-theme={dark ? 'dark' : undefined}>
        <PhoneSbar />
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', position: 'relative', overflow: 'hidden', minHeight: 0 }}>
          <S.TopBar ds={CN.ds} compact />
          <MobileBody initialTab={initialTab} initialPerspective={initialPerspective} initialScenario={initialScenario} />
        </div>
      </div>
      {desc && <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 360, textAlign: 'center', lineHeight: 1.55 }}>{desc}</div>}
    </div>
  );

  Object.assign(window.CNParts, {
    TeamCard, TeamPanel, ClueHistoryTimeline, ClueHistoryInner, RightColumnTabs, RC_TABS,
    DesktopBody, MobileBody, MobileSheet, PhoneShell, STATES,
  });
})();
