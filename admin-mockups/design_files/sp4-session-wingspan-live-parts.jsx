/* MeepleAI SP4 wave 2 — Session live PARTS — window.LiveSessionParts1
   Reconstructed center/left-column primitives consumed by sp4-session-live.jsx:
     entityHsl · CHAT · CHAT_SUGGESTIONS · ROSTER
     LiveTopBar · PlayerRosterLive · LiveScoringPanel · ActionLogTimeline
   Faithful to tokens.css; data shared via window.MAI. */

(function () {
  const { useState, useMemo } = React;
  const M = window.MAI;
  const eHsl = M.entityHsl;

  // ─── LiveTopBar ──────────────────────────────────────
  const LiveTopBar = ({ compact, paused }) => (
    <header style={{
      flexShrink: 0, position: 'sticky', top: 0, zIndex: 10,
      background: 'var(--glass-bg)', backdropFilter: 'blur(14px)',
      borderBottom: '1px solid var(--border)',
    }}>
      <div style={{
        display: 'flex', alignItems: 'center', gap: compact ? 8 : 12,
        padding: compact ? '8px 12px' : '10px 18px',
      }}>
        <div style={{
          display: 'inline-flex', alignItems: 'center', gap: 5,
          padding: '3px 9px', borderRadius: 'var(--r-pill)',
          background: eHsl('game', 0.1), color: eHsl('game'),
          border: `1px solid ${eHsl('game', 0.3)}`,
          fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
          textTransform: 'uppercase', letterSpacing: '.05em', flexShrink: 0,
        }}>🦜 Wingspan</div>
        {!compact && (
          <span style={{ fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800, color: 'var(--text)' }}>Serata #3</span>
        )}
        {/* Turn indicator */}
        <div style={{
          display: 'inline-flex', alignItems: 'center', gap: 6,
          padding: '3px 10px', borderRadius: 'var(--r-pill)',
          background: paused ? eHsl('session', 0.08) : eHsl('session', 0.12),
          border: `1px solid ${eHsl('session', 0.3)}`,
          color: eHsl('session'), flexShrink: 0,
        }}>
          <span aria-hidden="true" className={paused ? undefined : 'mai-pulse-dot'} style={{
            width: 7, height: 7, borderRadius: '50%', background: eHsl('session'),
          }} />
          <span style={{ fontFamily: 'var(--f-mono)', fontSize: 10.5, fontWeight: 800, letterSpacing: '.04em' }}>
            Turno 12<span style={{ opacity: .6 }}>/18</span>
          </span>
        </div>
        <div style={{ flex: 1 }} />
        {/* Timer */}
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: compact ? 13 : 15, fontWeight: 800,
          color: paused ? 'var(--text-muted)' : 'var(--text)',
          fontVariantNumeric: 'tabular-nums', letterSpacing: '.02em', flexShrink: 0,
        }}>{paused ? '⏸ 1:24:06' : '1:24:06'}</div>
        {!compact && (
          <button type="button" aria-label="Termina" style={{
            padding: '6px 12px', borderRadius: 'var(--r-md)', flexShrink: 0,
            background: eHsl('session'), color: '#fff', border: 'none',
            fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer',
            boxShadow: `0 3px 10px ${eHsl('session', 0.4)}`,
          }}>Termina</button>
        )}
      </div>
      {!paused && (
        <div style={{
          display: 'flex', alignItems: 'center', gap: 6,
          padding: '4px 18px', background: eHsl('toolkit', 0.06),
          borderTop: `1px solid ${eHsl('toolkit', 0.15)}`,
          fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 700, color: eHsl('toolkit'),
        }}>
          <span aria-hidden="true">✓</span> Auto-save attivo · ultimo salvataggio 14:58
        </div>
      )}
    </header>
  );

  // ─── PlayerRosterLive ────────────────────────────────
  const PlayerCard = ({ p, rank, compact, open, onToggle }) => {
    const cur = p.current;
    return (
      <div style={{
        borderRadius: 'var(--r-lg)',
        background: cur ? eHsl('session', 0.08) : 'var(--bg-card)',
        border: cur ? `1px solid ${eHsl('session', 0.3)}` : '1px solid var(--border)',
        borderLeft: cur ? `4px solid ${eHsl('session')}` : '1px solid var(--border)',
        overflow: 'hidden',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 12px' }}>
          <div style={{ position: 'relative', flexShrink: 0 }}>
            <div style={{
              width: 38, height: 38, borderRadius: '50%',
              background: `linear-gradient(135deg, hsl(${p.hue},70%,64%), hsl(${p.hue},58%,44%))`,
              color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800,
            }}>{p.name[0]}</div>
            {cur && <span className="mai-pulse-dot" style={{
              position: 'absolute', bottom: -1, right: -1, width: 11, height: 11,
              borderRadius: '50%', background: eHsl('session'), border: '2px solid var(--bg-card)',
            }} />}
          </div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800, color: 'var(--text)', display: 'flex', alignItems: 'center', gap: 6 }}>
              {p.name}
              {rank === 1 && <span aria-hidden="true">🏆</span>}
              {cur && <span style={{
                fontFamily: 'var(--f-mono)', fontSize: 8.5, fontWeight: 800, color: eHsl('session'),
                background: eHsl('session', 0.14), padding: '1px 6px', borderRadius: 'var(--r-pill)',
                textTransform: 'uppercase', letterSpacing: '.05em',
              }}>Al turno</span>}
            </div>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9.5, color: 'var(--text-muted)', fontWeight: 700 }}>{rank}° posto</div>
          </div>
          <div style={{ textAlign: 'right' }}>
            <div style={{ fontFamily: 'var(--f-display)', fontSize: 24, fontWeight: 800, color: cur ? eHsl('session') : 'var(--text)', fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{p.score}</div>
            {p.turnDelta !== 0 && (
              <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: eHsl('toolkit') }}>+{p.turnDelta}</div>
            )}
          </div>
          <button type="button" onClick={onToggle} aria-label="Dettaglio punteggio" style={{
            width: 24, height: 24, borderRadius: 'var(--r-sm)', flexShrink: 0,
            background: 'transparent', border: '1px solid var(--border)', color: 'var(--text-muted)',
            cursor: 'pointer', fontSize: 11,
          }}>{open ? '▴' : '▾'}</button>
        </div>
        {open && (
          <div style={{ padding: '0 12px 10px', display: 'flex', flexWrap: 'wrap', gap: 5 }}>
            {M.CATS.map(c => (
              <span key={c.id} style={{
                display: 'inline-flex', alignItems: 'center', gap: 4,
                padding: '2px 8px', borderRadius: 'var(--r-pill)',
                background: 'var(--bg-muted)', color: 'var(--text-sec)',
                fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 700,
              }}><span aria-hidden="true">{c.em}</span>{M.BREAKDOWN[p.id][c.id]}</span>
            ))}
          </div>
        )}
      </div>
    );
  };

  const PlayerRosterLive = ({ compact }) => {
    const [open, setOpen] = useState({ 'p-marco': true });
    return (
      <aside style={{
        width: compact ? 'auto' : 300, flexShrink: 0,
        padding: 12, display: 'flex', flexDirection: 'column', gap: 8,
        background: compact ? 'transparent' : 'var(--bg)',
        borderRight: compact ? 'none' : '1px solid var(--border)',
        overflowY: 'auto',
      }}>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-muted)',
          textTransform: 'uppercase', letterSpacing: '.08em',
        }}>Roster · 4 player</div>
        {M.ROSTER.map((p, i) => (
          <PlayerCard key={p.id} p={p} rank={i + 1} compact={compact}
            open={!!open[p.id]} onToggle={() => setOpen(s => ({ ...s, [p.id]: !s[p.id] }))} />
        ))}
      </aside>
    );
  };

  // ─── LiveScoringPanel ────────────────────────────────
  const LiveScoringPanel = ({ compact, emptyTurn }) => {
    const [cat, setCat] = useState('eggs');
    const [val, setVal] = useState(emptyTurn ? 0 : 4);
    const presets = [1, 2, 3, 5];
    return (
      <div style={{
        background: 'var(--bg-card)', border: '1px solid var(--border)',
        borderRadius: 'var(--r-lg)', padding: 14, boxShadow: 'var(--shadow-sm)',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 }}>
          <div style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: 'var(--text)' }}>Punteggio · Marco</div>
          <span style={{
            padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHsl('session', 0.12),
            color: eHsl('session'), fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
            textTransform: 'uppercase', letterSpacing: '.05em',
          }}>Turno 12</span>
        </div>
        {/* category tabs */}
        <div className="mai-cb-scroll" style={{ display: 'flex', gap: 5, overflowX: 'auto', marginBottom: 14, paddingBottom: 2 }}>
          {M.CATS.map(c => {
            const active = cat === c.id;
            return (
              <button key={c.id} type="button" onClick={() => setCat(c.id)} aria-pressed={active} style={{
                display: 'inline-flex', alignItems: 'center', gap: 4, flexShrink: 0,
                padding: '5px 11px', borderRadius: 'var(--r-pill)',
                background: active ? eHsl('session', 0.14) : 'var(--bg-muted)',
                border: active ? `1px solid ${eHsl('session', 0.4)}` : '1px solid transparent',
                color: active ? eHsl('session') : 'var(--text-sec)',
                fontFamily: 'var(--f-display)', fontSize: 11.5, fontWeight: 700, cursor: 'pointer',
              }}><span aria-hidden="true">{c.em}</span>{c.lb}</button>
            );
          })}
        </div>
        {/* big +/- stepper */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 18, marginBottom: 14 }}>
          <button type="button" onClick={() => setVal(v => Math.max(0, v - 1))} aria-label="Meno" style={{
            width: 52, height: 52, borderRadius: '50%', flexShrink: 0,
            background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text)',
            fontSize: 26, fontWeight: 800, cursor: 'pointer', lineHeight: 1,
          }}>−</button>
          <div style={{ textAlign: 'center', minWidth: 96 }}>
            <div style={{ fontFamily: 'var(--f-display)', fontSize: 52, fontWeight: 800, color: 'var(--text)', fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{val}</div>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9.5, color: 'var(--text-muted)', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '.06em' }}>punti {M.CATS.find(c => c.id === cat).lb}</div>
          </div>
          <button type="button" onClick={() => setVal(v => v + 1)} aria-label="Più" style={{
            width: 52, height: 52, borderRadius: '50%', flexShrink: 0,
            background: eHsl('session'), border: 'none', color: '#fff',
            fontSize: 26, fontWeight: 800, cursor: 'pointer', lineHeight: 1,
            boxShadow: `0 4px 14px ${eHsl('session', 0.4)}`,
          }}>+</button>
        </div>
        {/* presets */}
        <div style={{ display: 'flex', gap: 6, justifyContent: 'center' }}>
          {presets.map(n => (
            <button key={n} type="button" onClick={() => setVal(v => v + n)} style={{
              padding: '5px 14px', borderRadius: 'var(--r-md)',
              background: eHsl('session', 0.08), border: `1px solid ${eHsl('session', 0.25)}`,
              color: eHsl('session'), fontFamily: 'var(--f-mono)', fontSize: 12, fontWeight: 800, cursor: 'pointer',
            }}>+{n}</button>
          ))}
        </div>
      </div>
    );
  };

  // ─── ActionLogTimeline ───────────────────────────────
  const LOG = [
    { t: '14:58', kind: 'chat', em: '💬', text: 'Marco ha chiesto all\'agente delle uova multiple' },
    { t: '14:55', kind: 'score', em: '🥚', text: 'Marco +4 uova (turno 12)' },
    { t: '14:51', kind: 'agent', em: '🤖', text: 'Agente: regola wetland chiarita' },
    { t: '14:48', kind: 'score', em: '🐦', text: 'Anna ha giocato Bald Eagle (+12 Forest)' },
    { t: '14:43', kind: 'score', em: '🏆', text: 'Anna +8 Habitat bonus' },
    { t: '14:38', kind: 'event', em: '▶', text: 'Ripresa dopo pausa (6m)' },
    { t: '14:32', kind: 'event', em: '⏸', text: 'Pausa partita' },
    { t: '14:20', kind: 'tool', em: '🎲', text: 'Luca ha pescato 2 bird card' },
    { t: '14:08', kind: 'kb', em: '📷', text: 'Foto tableau aggiunta · Turno 8' },
    { t: '13:08', kind: 'event', em: '▶', text: 'Sessione iniziata · setup completato' },
  ];
  const ec = (k) => ({ score: 'session', tool: 'tool', agent: 'agent', chat: 'chat', kb: 'kb', event: 'event' }[k] || 'session');

  const ActionLogTimeline = ({ compact }) => {
    const [filter, setFilter] = useState('all');
    const filters = [
      { id: 'all', lb: 'Tutti' }, { id: 'score', lb: 'Score' },
      { id: 'agent', lb: 'Agente' }, { id: 'event', lb: 'Eventi' },
    ];
    const rows = filter === 'all' ? LOG : LOG.filter(e => e.kind === filter);
    return (
      <div style={{
        background: 'var(--bg-card)', border: '1px solid var(--border)',
        borderRadius: 'var(--r-lg)', overflow: 'hidden', boxShadow: 'var(--shadow-sm)',
        flex: compact ? 'none' : 1, minHeight: 0, display: 'flex', flexDirection: 'column',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '10px 14px', borderBottom: '1px solid var(--border-light)' }}>
          <div style={{ fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800, color: 'var(--text)' }}>Action log</div>
          <div className="mai-cb-scroll" style={{ display: 'flex', gap: 4, overflowX: 'auto', flex: 1 }}>
            {filters.map(f => (
              <button key={f.id} type="button" onClick={() => setFilter(f.id)} aria-pressed={filter === f.id} style={{
                padding: '3px 9px', borderRadius: 'var(--r-pill)', flexShrink: 0,
                background: filter === f.id ? eHsl('session', 0.14) : 'transparent',
                border: filter === f.id ? `1px solid ${eHsl('session', 0.4)}` : '1px solid var(--border)',
                color: filter === f.id ? eHsl('session') : 'var(--text-sec)',
                fontFamily: 'var(--f-display)', fontSize: 10.5, fontWeight: 700, cursor: 'pointer',
              }}>{f.lb}</button>
            ))}
          </div>
        </div>
        <div style={{ padding: '8px 14px', overflowY: 'auto', position: 'relative' }}>
          <div aria-hidden="true" style={{ position: 'absolute', left: 23, top: 12, bottom: 12, width: 2, background: eHsl('session', 0.15) }} />
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            {rows.map((e, i) => {
              const c = ec(e.kind);
              return (
                <div key={i} style={{ display: 'flex', gap: 10, alignItems: 'flex-start', padding: '4px 0', position: 'relative' }}>
                  <div style={{
                    width: 22, height: 22, borderRadius: '50%', flexShrink: 0, zIndex: 1,
                    background: eHsl(c, 0.14), border: `2px solid ${eHsl(c, 0.4)}`,
                    display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 10,
                  }} aria-hidden="true">{e.em}</div>
                  <div style={{ flex: 1, paddingTop: 1 }}>
                    <span style={{ fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800, color: eHsl(c), marginRight: 6 }}>{e.t}</span>
                    <span style={{ fontFamily: 'var(--f-body)', fontSize: 12, color: 'var(--text-sec)', fontWeight: 600 }}>{e.text}</span>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    );
  };

  window.LiveSessionParts1 = {
    entityHsl: eHsl,
    CHAT: M.CHAT, CHAT_SUGGESTIONS: M.CHAT_SUGGESTIONS, ROSTER: M.ROSTER,
    LiveTopBar, PlayerRosterLive, LiveScoringPanel, ActionLogTimeline,
  };
  window.DS = window.DS || {};
})();
