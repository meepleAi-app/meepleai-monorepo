/* MeepleAI SP4 — Generic Session Skeleton · SHARED PARTS
   Game-agnostic chrome that wraps the polymorphic renderers:
     TopBar · ConnectionChip · ChatAgentPanel · ActionLogTimeline ·
     NotesPanel · RightColumnTabs (Scoring/Turn/Widget/Notes) · StateSwitch ·
     ThemeToggle · DesktopFrame · PhoneShell · PanelFrame

   Reads window.MAI (primitives) + window.SkeletonRenderers (polymorphic tabs).
   Exposes window.SkeletonParts. */

(function () {
  const M = window.MAI;
  const R = window.SkeletonRenderers;
  const eHsl = M.entityHsl;
  const { useState } = React;
  const mono = R.mono;

  // ─── connection chip (SSE status) ────────────────────────────────────────
  const CONN = {
    connected:    { e: 'toolkit', dot: '●', lb: 'Connesso',       pulse: true },
    reconnecting: { e: 'agent',   dot: '◐', lb: 'Riconnessione…', pulse: true },
    offline:      { e: 'event',   dot: '○', lb: 'Offline' },
  };
  const ConnectionChip = ({ status = 'connected' }) => {
    const c = CONN[status] || CONN.connected;
    return (
      <span role="status" aria-label={`Stato connessione: ${c.lb}`} style={{
        display: 'inline-flex', alignItems: 'center', gap: 5, padding: '4px 9px', borderRadius: 'var(--r-pill)',
        background: eHsl(c.e, 0.1), color: eHsl(c.e), border: `1px solid ${eHsl(c.e, 0.3)}`,
        ...mono(9.5, 800), textTransform: 'uppercase', letterSpacing: '.05em', whiteSpace: 'nowrap',
      }}>
        <span aria-hidden="true" className={c.pulse ? 'mai-pulse-dot' : undefined}>{c.dot}</span>{c.lb}
      </span>
    );
  };

  // ─── TopBar (identical chrome for every game) ────────────────────────────
  const PlayerStrip = ({ players, max = 4 }) => {
    const shown = players.slice(0, max);
    const extra = players.length - shown.length;
    return (
      <div style={{ display: 'flex', alignItems: 'center' }} aria-label={`${players.length} giocatori`}>
        {shown.map((p, i) => (
          <div key={p.id} title={p.name} style={{
            width: 26, height: 26, borderRadius: '50%', marginLeft: i === 0 ? 0 : -8,
            background: `linear-gradient(135deg, hsl(${p.hue},70%,64%), hsl(${p.hue},58%,44%))`,
            color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
            ...mono(11, 800), border: '2px solid var(--bg-card)', zIndex: max - i,
          }} aria-hidden="true">{p.name[0]}</div>
        ))}
        {extra > 0 && <span style={{ ...mono(10, 800, 'var(--text-muted)'), marginLeft: 6 }}>+{extra}</span>}
      </div>
    );
  };

  const TopBar = ({ ds, connection = 'connected', compact }) => (
    <header aria-label="Barra sessione" style={{
      display: 'flex', alignItems: 'center', gap: compact ? 8 : 12, rowGap: 8, flexWrap: compact ? 'nowrap' : 'wrap',
      padding: compact ? '8px 12px' : '10px 16px', flexShrink: 0,
      background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderBottom: '1px solid var(--border)',
    }}>
      <div style={{ width: compact ? 30 : 38, height: compact ? 38 : 48, borderRadius: 'var(--r-md)', background: ds.game.cover, flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: compact ? 16 : 20, boxShadow: 'var(--shadow-xs)' }} aria-hidden="true">{ds.game.emoji}</div>
      <div style={{ minWidth: compact ? 0 : 120, flex: compact ? 1 : '1 1 auto' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
          <span style={{ fontFamily: 'var(--f-display)', fontSize: compact ? 14 : 16, fontWeight: 800, color: 'var(--text)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{ds.game.title}</span>
          <span style={{ ...mono(9, 800, eHsl('session')), padding: '1px 6px', borderRadius: 'var(--r-pill)', background: eHsl('session', 0.12), border: `1px solid ${eHsl('session', 0.3)}`, textTransform: 'uppercase', letterSpacing: '.05em' }} className="mai-pulse-dot-host">live</span>
        </div>
        {!compact && <div style={{ ...mono(9.5, 700, 'var(--text-muted)') }}>{ds.game.meta}</div>}
      </div>
      {compact && <ConnectionChip status={connection} />}
      {compact && (
        <span style={{ ...mono(11, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums', display: 'inline-flex', alignItems: 'center', gap: 4 }} aria-label={`Tempo trascorso ${ds.session.elapsed}`}>
          <span aria-hidden="true" style={{ color: 'var(--text-muted)' }}>⏱</span>{ds.session.elapsed}
        </span>
      )}
      {compact && <button type="button" aria-label="Pausa" style={{ width: 30, height: 30, borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontSize: 12, flexShrink: 0 }}>⏸</button>}
      {!compact && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginLeft: 'auto', flexShrink: 0 }}>
          <PlayerStrip players={ds.players} />
          <ConnectionChip status={connection} />
          <span style={{ ...mono(12.5, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums', display: 'inline-flex', alignItems: 'center', gap: 4 }} aria-label={`Tempo trascorso ${ds.session.elapsed}`}>
            <span aria-hidden="true" style={{ color: 'var(--text-muted)' }}>⏱</span>{ds.session.elapsed}
          </span>
          <button type="button" aria-label="Pausa" style={{ width: 34, height: 34, borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontSize: 13, flexShrink: 0 }}>⏸</button>
          <button type="button" style={{ padding: '7px 14px', borderRadius: 'var(--r-md)', background: eHsl('session'), color: '#fff', border: 'none', fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, cursor: 'pointer', flexShrink: 0, boxShadow: `0 3px 10px ${eHsl('session', 0.35)}` }}>Salva</button>
        </div>
      )}
    </header>
  );

  // ─── ChatAgent panel (the magnet — always visible, no toggle) ────────────
  const ChatAgentPanel = ({ ds, compact, collapsed, onHeaderClick }) => (
    <section aria-label="Chat con l'agente AI" style={{ display: 'flex', flexDirection: 'column', flex: collapsed ? '0 0 auto' : 1, minHeight: 0, background: 'var(--bg-card)', border: `1px solid ${collapsed ? 'var(--border)' : eHsl('agent', 0.3)}`, borderRadius: 'var(--r-lg)', overflow: 'hidden' }}>
      <button type="button" onClick={onHeaderClick} aria-expanded={!collapsed} style={{ width: '100%', textAlign: 'left', border: 'none', padding: '9px 12px', background: eHsl('agent', collapsed ? 0.04 : 0.06), borderBottom: collapsed ? 'none' : `1px solid ${eHsl('agent', 0.2)}`, display: 'flex', alignItems: 'center', gap: 10, flexShrink: 0, cursor: onHeaderClick ? 'pointer' : 'default' }}>
        <div style={{ width: 34, height: 34, borderRadius: '50%', position: 'relative', background: `linear-gradient(135deg, ${eHsl('agent')}, ${eHsl('agent', 0.6)})`, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 16, flexShrink: 0 }} aria-hidden="true">
          {ds.agent.emoji}
          <span className="mai-pulse-dot" style={{ position: 'absolute', bottom: -1, right: -1, width: 11, height: 11, borderRadius: '50%', background: 'hsl(140,60%,45%)', border: '2px solid var(--bg-card)' }} />
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, color: 'var(--text)' }}>{ds.agent.name}</div>
          <div style={{ ...mono(9.5, 700, 'hsl(140,50%,40%)') }}>● Online · {ds.agent.latency}</div>
        </div>
        <span style={{ ...mono(8.5, 800, eHsl('agent')), textTransform: 'uppercase', letterSpacing: '.05em', padding: '2px 7px', borderRadius: 'var(--r-pill)', background: eHsl('agent', 0.1), border: `1px solid ${eHsl('agent', 0.28)}` }}>ChatAgent</span>
        {onHeaderClick && <span aria-hidden="true" style={{ ...mono(11, 800, eHsl('agent')), flexShrink: 0, transform: collapsed ? 'rotate(-90deg)' : 'none', transition: 'transform var(--dur-sm) var(--ease-out)' }}>▾</span>}
      </button>
      {!collapsed && (<>
      <div style={{ flex: 1, overflowY: 'auto', padding: 12, display: 'flex', flexDirection: 'column', gap: 10, minHeight: compact ? 180 : 0 }}>
        {ds.chat.map(m => {
          const isAgent = m.from === 'agent';
          return (
            <div key={m.id} style={{ alignSelf: isAgent ? 'flex-start' : 'flex-end', maxWidth: '86%', display: 'flex', flexDirection: 'column', gap: 3 }}>
              <div style={{ padding: '8px 11px', borderRadius: 'var(--r-lg)', background: isAgent ? eHsl('agent', 0.1) : eHsl('chat', 0.12), border: `1px solid ${isAgent ? eHsl('agent', 0.25) : eHsl('chat', 0.25)}`, borderTopLeftRadius: isAgent ? 4 : 'var(--r-lg)', borderTopRightRadius: isAgent ? 'var(--r-lg)' : 4, fontFamily: 'var(--f-body)', fontSize: 12.5, fontWeight: 500, color: 'var(--text)', lineHeight: 1.45 }}>{m.text}</div>
              <div style={{ ...mono(9, 700, 'var(--text-muted)'), alignSelf: isAgent ? 'flex-start' : 'flex-end', padding: '0 4px' }}>{m.t}</div>
              {m.citations && (
                <div style={{ display: 'flex', gap: 3, flexWrap: 'wrap', marginTop: 2 }}>
                  {m.citations.map((c, i) => (
                    <span key={i} style={{ display: 'inline-flex', alignItems: 'center', gap: 3, padding: '2px 6px', borderRadius: 'var(--r-pill)', background: eHsl('kb', 0.1), border: `1px solid ${eHsl('kb', 0.25)}`, color: eHsl('kb'), ...mono(9, 700) }}><span aria-hidden="true">📄</span>{c}</span>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>
      <div style={{ padding: '8px 12px', borderTop: '1px solid var(--border-light)', display: 'flex', gap: 5, flexWrap: 'wrap', flexShrink: 0 }}>
        {ds.suggestions.map(s => (
          <button key={s} type="button" style={{ padding: '5px 10px', borderRadius: 'var(--r-pill)', background: eHsl('chat', 0.08), border: `1px solid ${eHsl('chat', 0.25)}`, color: eHsl('chat'), fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 700, cursor: 'pointer' }}>{s}</button>
        ))}
      </div>
      <div style={{ padding: '10px 12px', borderTop: '1px solid var(--border)', display: 'flex', gap: 6, alignItems: 'flex-end', flexShrink: 0 }}>
        <textarea placeholder="Chiedi all'agente…" rows={1} aria-label="Messaggio all'agente" style={{ flex: 1, padding: '8px 11px', borderRadius: 'var(--r-md)', border: '1px solid var(--border)', background: 'var(--bg)', color: 'var(--text)', fontFamily: 'var(--f-body)', fontSize: 12.5, resize: 'none', minHeight: 34 }} />
        <button type="button" aria-label="Invia" style={{ width: 36, height: 36, borderRadius: 'var(--r-md)', background: eHsl('chat'), color: '#fff', border: 'none', fontSize: 14, cursor: 'pointer', flexShrink: 0, boxShadow: `0 3px 10px ${eHsl('chat', 0.4)}` }}>↑</button>
      </div>
      </>)}
    </section>
  );

  // ─── Action log timeline ─────────────────────────────────────────────────
  const KIND_E = { 'game-start': 'session', 'turn-change': 'player', 'score-update': 'toolkit', custom: 'event' };
  const ActionLogTimeline = ({ ds, compact, state = 'default', collapsed, onHeaderClick }) => {
    const inner = (
      <div style={{ display: 'flex', flexDirection: 'column' }}>
        {ds.events.map((ev, i) => {
          const e = KIND_E[ev.kind] || 'session';
          const who = ds.players.find(p => p.id === ev.who);
          return (
            <div key={i} style={{ display: 'flex', gap: 10, padding: '7px 0' }}>
              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', flexShrink: 0 }}>
                <span style={{ width: 10, height: 10, borderRadius: '50%', background: eHsl(e), border: `2px solid var(--bg-card)`, boxShadow: `0 0 0 2px ${eHsl(e, 0.25)}` }} aria-hidden="true" />
                {i < ds.events.length - 1 && <span style={{ width: 2, flex: 1, minHeight: 14, background: 'var(--border)', marginTop: 2 }} />}
              </div>
              <div style={{ flex: 1, minWidth: 0, paddingBottom: 2 }}>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
                  {who && <span style={{ fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, color: `hsl(${who.hue},60%,52%)` }}>{who.name}</span>}
                  <span style={{ ...mono(8.5, 800, eHsl(e)), textTransform: 'uppercase', letterSpacing: '.05em' }}>{ev.kind}</span>
                  <div style={{ flex: 1 }} />
                  <span style={{ ...mono(9, 700, 'var(--text-muted)') }}>{ev.t}</span>
                </div>
                <div style={{ fontFamily: 'var(--f-body)', fontSize: 12.5, color: 'var(--text-sec)', lineHeight: 1.45 }} dangerouslySetInnerHTML={{ __html: ev.text }} />
              </div>
            </div>
          );
        })}
      </div>
    );
    return (
      <section aria-label="Registro azioni" style={{ background: 'var(--bg-card)', border: `1px solid ${onHeaderClick && !collapsed ? eHsl('event', 0.3) : 'var(--border)'}`, borderRadius: 'var(--r-lg)', overflow: 'hidden', display: 'flex', flexDirection: 'column', ...(onHeaderClick ? { flex: collapsed ? '0 0 auto' : 1, minHeight: 0 } : { flexShrink: 0, maxHeight: compact ? 'none' : 260 }) }}>
        <button type="button" onClick={onHeaderClick} aria-expanded={onHeaderClick ? !collapsed : undefined} style={{ width: '100%', textAlign: 'left', border: 'none', background: onHeaderClick && !collapsed ? eHsl('event', 0.06) : 'transparent', padding: '9px 12px', borderBottom: collapsed ? 'none' : '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 8, flexShrink: 0, cursor: onHeaderClick ? 'pointer' : 'default' }}>
          <span style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>Registro azioni</span>
          <span style={{ ...mono(9, 800, eHsl('event')), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl('event', 0.1), border: `1px solid ${eHsl('event', 0.28)}` }}>{ds.events.length}</span>
          {onHeaderClick && <><div style={{ flex: 1 }} /><span aria-hidden="true" style={{ ...mono(11, 800, eHsl('event')), transform: collapsed ? 'rotate(-90deg)' : 'none', transition: 'transform var(--dur-sm) var(--ease-out)' }}>▾</span></>}
        </button>
        {!collapsed && <div style={{ padding: '4px 12px 10px', overflowY: 'auto', flex: 1, minHeight: 0 }}>
          <R.StateScaffold state={state} sseWhere="eventi"
            empty={{ icon: '🎉', title: 'Nessun evento', body: 'Gli eventi della partita compariranno qui in tempo reale.' }}
            error={{ title: 'Registro non disponibile', body: 'Impossibile caricare gli eventi della sessione.' }}>
            {inner}
          </R.StateScaffold>
        </div>}
      </section>
    );
  };

  // ─── Notes panel (private notes + photos) ────────────────────────────────
  const NotesPanel = ({ ds, state = 'default' }) => (
    <R.StateScaffold state={state} sseWhere="note"
      empty={{ icon: '📋', title: 'Nessuna nota', body: 'Aggiungi note private condivise con il tavolo.' }}
      error={{ title: 'Note non disponibili', body: 'Impossibile sincronizzare le note della sessione.' }}>
      <R.Panel gap={12}>
        <R.Label>Note private · condivise</R.Label>
        <textarea defaultValue={ds.notes} rows={5} aria-label="Note sessione" style={{ width: '100%', padding: '10px 12px', borderRadius: 'var(--r-md)', border: '1px solid var(--border)', background: 'var(--bg-card)', color: 'var(--text)', fontFamily: 'var(--f-mono)', fontSize: 12, lineHeight: 1.55, resize: 'vertical', minHeight: 100 }} />
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <R.Label>Foto · {ds.photos.length}</R.Label>
          <button type="button" style={{ padding: '3px 8px', borderRadius: 'var(--r-pill)', background: eHsl('kb', 0.1), color: eHsl('kb'), border: `1px solid ${eHsl('kb', 0.3)}`, ...mono(9.5, 800), cursor: 'pointer', textTransform: 'uppercase', letterSpacing: '.04em' }}>+ Foto</button>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 6 }}>
          {ds.photos.map((p, i) => (
            <div key={i} style={{ aspectRatio: '1', borderRadius: 'var(--r-sm)', background: p.g, display: 'flex', alignItems: 'flex-end', padding: 4 }}>
              <span style={{ ...mono(9, 800, '#fff'), background: 'rgba(0,0,0,.4)', padding: '1px 5px', borderRadius: 'var(--r-pill)' }}>{p.lb}</span>
            </div>
          ))}
          <button type="button" aria-label="Aggiungi foto" style={{ aspectRatio: '1', borderRadius: 'var(--r-sm)', background: 'transparent', border: '1px dashed var(--border-strong)', color: 'var(--text-muted)', fontSize: 20, cursor: 'pointer' }}>+</button>
        </div>
      </R.Panel>
    </R.StateScaffold>
  );

  // ─── state selector (segmented) ──────────────────────────────────────────
  const STATES = window.SkeletonData.STATES;
  const StateSwitch = ({ value, onChange }) => (
    <div className="mai-cb-scroll" role="group" aria-label="Stato del componente" style={{ display: 'flex', gap: 4, overflowX: 'auto', padding: '7px 10px', borderBottom: '1px solid var(--border-light)', background: 'var(--bg)', flexShrink: 0 }}>
      <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.08em', alignSelf: 'center', flexShrink: 0, marginRight: 2 }}>Stato</span>
      {STATES.map(s => {
        const active = value === s.id;
        return (
          <button key={s.id} type="button" onClick={() => onChange(s.id)} aria-pressed={active} style={{ padding: '3px 9px', borderRadius: 'var(--r-pill)', flexShrink: 0, background: active ? eHsl('session', 0.14) : 'var(--bg-card)', border: active ? `1px solid ${eHsl('session', 0.4)}` : '1px solid var(--border)', color: active ? eHsl('session') : 'var(--text-sec)', ...mono(9.5, 800), cursor: 'pointer' }}>{s.lb}</button>
        );
      })}
    </div>
  );

  // ─── RightColumnTabs (polymorphic: Scoring · Turn · Widget · Notes) ──────
  const TABS = [
    { id: 'scoring', icon: '🎯', label: 'Score',  entity: 'session', render: (ds, st) => <R.ScoringPanelRenderer data={ds} state={st} /> },
    { id: 'turn',    icon: '🔄', label: 'Turni',  entity: 'player',  render: (ds, st) => <R.TurnIndicatorRenderer data={ds} state={st} /> },
    { id: 'widget',  icon: '🧰', label: 'Widget', entity: 'toolkit', render: (ds, st) => <R.ToolkitRenderer data={ds} players={ds.players} state={st} /> },
    { id: 'notes',   icon: '📋', label: 'Note',   entity: 'kb',      render: (ds, st) => <NotesPanel ds={ds} state={st} /> },
  ];

  const RightColumnTabs = ({ ds, initial = 'scoring', initialState = 'default', embedded }) => {
    const [tab, setTab] = useState(initial);
    const [st, setSt] = useState(initialState);
    const active = TABS.find(t => t.id === tab) || TABS[0];
    return (
      <aside aria-label="Pannello polimorfico" style={{ width: embedded ? '100%' : '40%', minWidth: embedded ? 0 : 300, flexShrink: 0, height: '100%', background: 'var(--bg-card)', borderLeft: embedded ? 'none' : '1px solid var(--border)', display: 'flex', flexDirection: 'column', minHeight: 0 }}>
        <div role="tablist" aria-label="Sezioni" style={{ display: 'flex', borderBottom: '1px solid var(--border-light)', flexShrink: 0 }}>
          {TABS.map(t => {
            const on = tab === t.id;
            return (
              <button key={t.id} type="button" role="tab" aria-selected={on} aria-label={t.label} onClick={() => setTab(t.id)} style={{ flex: 1, padding: '10px 6px', background: on ? eHsl(t.entity, 0.06) : 'transparent', border: 'none', borderBottom: on ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent', color: on ? eHsl(t.entity) : 'var(--text-sec)', fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 5, whiteSpace: 'nowrap' }}>
                <span aria-hidden="true" style={{ fontSize: 14 }}>{t.icon}</span><span>{t.label}</span>
              </button>
            );
          })}
        </div>
        <StateSwitch value={st} onChange={setSt} />
        <div role="tabpanel" style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(ds, st)}</div>
      </aside>
    );
  };

  // ─── DESKTOP session body (LEFT 60% chat+log · RIGHT 40% tabs) ───────────
  const DesktopSessionBody = ({ ds, initialTab, initialState }) => {
    const [expanded, setExpanded] = useState('chat'); // 'chat' | 'log'
    return (
      <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
        <main style={{ flex: 3, minWidth: 0, padding: 14, display: 'flex', flexDirection: 'column', gap: 12, overflow: 'hidden' }}>
          <ChatAgentPanel ds={ds} collapsed={expanded !== 'chat'} onHeaderClick={() => setExpanded('chat')} />
          <ActionLogTimeline ds={ds} collapsed={expanded !== 'log'} onHeaderClick={() => setExpanded('log')} />
        </main>
        <RightColumnTabs ds={ds} initial={initialTab} initialState={initialState} />
      </div>
    );
  };

  // ─── MOBILE session body (full-width main · bottom-sheet drawer) ─────────
  const MobileTabSheet = ({ ds, open, tab, st, setSt, onClose }) => {
    const active = TABS.find(t => t.id === tab) || TABS[0];
    return (
      <div aria-hidden={!open} style={{ position: 'absolute', inset: 0, zIndex: 50, pointerEvents: open ? 'auto' : 'none' }}>
        <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(15,12,30,.5)', backdropFilter: 'blur(3px)', opacity: open ? 1 : 0, transition: 'opacity var(--dur-md) var(--ease-out)' }} />
        <div role="dialog" aria-modal="true" aria-label={active.label} style={{ position: 'absolute', left: 0, right: 0, bottom: 0, height: '84%', background: 'var(--bg-card)', borderTopLeftRadius: 'var(--r-2xl)', borderTopRightRadius: 'var(--r-2xl)', borderTop: `3px solid ${eHsl(active.entity)}`, boxShadow: 'var(--shadow-drawer)', display: 'flex', flexDirection: 'column', minHeight: 0, transform: open ? 'translateY(0)' : 'translateY(101%)', transition: 'transform var(--dur-lg) var(--ease-spring)' }}>
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', paddingTop: 8, flexShrink: 0 }}>
            <div style={{ width: 38, height: 4, borderRadius: 999, background: 'var(--border-strong)' }} />
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 14px 10px', flexShrink: 0 }}>
            <span aria-hidden="true" style={{ fontSize: 16 }}>{active.icon}</span>
            <span style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: eHsl(active.entity) }}>{active.label}</span>
            <div style={{ flex: 1 }} />
            <button type="button" onClick={onClose} aria-label="Chiudi" style={{ width: 28, height: 28, borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontSize: 13 }}>✕</button>
          </div>
          <StateSwitch value={st} onChange={setSt} />
          <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(ds, st)}</div>
        </div>
      </div>
    );
  };

  const MobileSessionBody = ({ ds, initialTab }) => {
    const [sheet, setSheet] = useState(initialTab || null);
    const [st, setSt] = useState('default');
    const [expanded, setExpanded] = useState('chat'); // 'chat' | 'log'
    return (
      <>
        <div style={{ flex: 1, overflowY: 'auto', minHeight: 0, background: 'var(--bg)' }}>
          <div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 12, height: '100%', minHeight: 0 }}>
            <ChatAgentPanel ds={ds} compact collapsed={expanded !== 'chat'} onHeaderClick={() => setExpanded('chat')} />
            <ActionLogTimeline ds={ds} compact collapsed={expanded !== 'log'} onHeaderClick={() => setExpanded('log')} />
          </div>
        </div>
        <nav className="mai-cb-scroll" aria-label="Sezioni sessione" style={{ display: 'flex', gap: 6, overflowX: 'auto', flexShrink: 0, padding: '8px 10px', background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderTop: '1px solid var(--border)' }}>
          {TABS.map(t => (
            <button key={t.id} type="button" onClick={() => { setSt('default'); setSheet(t.id); }} style={{ flex: '0 0 auto', display: 'inline-flex', alignItems: 'center', gap: 5, padding: '7px 13px', borderRadius: 'var(--r-pill)', background: eHsl(t.entity, 0.1), border: `1px solid ${eHsl(t.entity, 0.28)}`, color: eHsl(t.entity), fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer', whiteSpace: 'nowrap' }}>
              <span aria-hidden="true">{t.icon}</span>{t.label}
            </button>
          ))}
        </nav>
        <MobileTabSheet ds={ds} open={!!sheet} tab={sheet} st={st} setSt={setSt} onClose={() => setSheet(null)} />
      </>
    );
  };

  // ─── Frames ──────────────────────────────────────────────────────────────
  const PhoneSbar = () => (
    <div className="phone-sbar" style={{ color: 'var(--text)' }}>
      <span style={{ fontFamily: 'var(--f-mono)' }}>14:32</span>
      <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
    </div>
  );
  const PhoneShell = ({ ds, label, desc, dark, initialTab }) => (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
      <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark</span>}</div>
      <div className="phone" data-theme={dark ? 'dark' : undefined}>
        <PhoneSbar />
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', position: 'relative', overflow: 'hidden', minHeight: 0 }}>
          <TopBar ds={ds} compact />
          <MobileSessionBody ds={ds} initialTab={initialTab} />
        </div>
      </div>
      {desc && <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 340, textAlign: 'center', lineHeight: 1.55 }}>{desc}</div>}
    </div>
  );

  const DesktopFrame = ({ ds, label, desc, dark, url, children, height = 720 }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10, width: '100%' }} data-theme={dark ? 'dark' : undefined}>
      <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>{label}</div>
      <div style={{ width: '100%', borderRadius: 'var(--r-xl)', border: '1px solid var(--border)', background: 'var(--bg)', overflow: 'hidden', boxShadow: 'var(--shadow-lg)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 14px', background: 'var(--bg-muted)', borderBottom: '1px solid var(--border)', ...mono(11, 600, 'var(--text-muted)') }}>
          <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-event))' }} />
          <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-warning))' }} />
          <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-toolkit))' }} />
          <span style={{ flex: 1, textAlign: 'center', letterSpacing: '.03em' }}>{url}</span>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', height, background: 'var(--bg)', overflow: 'hidden' }}>{children}</div>
      </div>
      {desc && <div style={{ fontSize: 11.5, color: 'var(--text-muted)', maxWidth: 720, lineHeight: 1.55 }}>{desc}</div>}
    </div>
  );

  // panel frame for the states gallery (rail-sized)
  const PanelFrame = ({ label, entity, dark, children, w = 300, h = 460 }) => (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }} data-theme={dark ? 'dark' : undefined}>
      <div style={{ ...mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>{label}</div>
      <div style={{ width: w, height: h, borderRadius: 'var(--r-lg)', border: `1px solid ${eHsl(entity, 0.3)}`, background: 'var(--bg-card)', overflow: 'hidden', display: 'flex', flexDirection: 'column', boxShadow: 'var(--shadow-sm)' }}>{children}</div>
    </div>
  );

  function ThemeToggle() {
    const [dark, setDark] = useState(true);
    React.useEffect(() => { document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light'); }, [dark]);
    return (
      <button onClick={() => setDark(d => !d)} className="theme-toggle" aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
        <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
      </button>
    );
  }

  window.SkeletonParts = {
    TopBar, ConnectionChip, ChatAgentPanel, ActionLogTimeline, NotesPanel,
    RightColumnTabs, StateSwitch, DesktopSessionBody, MobileSessionBody,
    PhoneShell, DesktopFrame, PanelFrame, ThemeToggle, TABS, PlayerStrip,
  };
})();
