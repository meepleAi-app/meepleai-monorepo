/* MeepleAI SP4 wave 2 — Schermata #2/3 — Session live — EXTENDED
   /sessions/[id]/live  (+ consolidated sub-routes as tabs)

   RightColumnTabs now hosts 7 tabs:
     EXISTING: tools · chat · notes
     NEW:      scores · photos · agent · players   (was 4 separate routes)
   Query-param deep link: ?tab=<id>. SSE stream preserved across tab switches
   (the live socket lives above the tab layer, so switching never reconnects).

   Imports:
     window.MAI               (sp4-parts-common.jsx)
     window.LiveSessionParts1 (sp4-session-live-parts.jsx)  → P
     window.LiveTabs          (sp4-session-live-tabs.jsx)
   DEMO-NAV-HINTS: sp4-session-summary.html sp4-sessions-index.html
*/

const { useState: useState2, useEffect: useEffect2, useRef: useRef2 } = React;
const P = window.LiveSessionParts1;
const M = window.MAI;
const LT = window.LiveTabs;
const eHsl = P.entityHsl;

// ═══════════════════════════════════════════════════════
// ─── EXISTING TAB · TOOLS RAIL ───────────────────────
// ═══════════════════════════════════════════════════════
const ToolCard = ({ icon, name, desc, color = 'tool', custom }) => (
  <button type="button" style={{
    padding: '12px 10px', borderRadius: 'var(--r-md)',
    background: custom ? eHsl('toolkit', 0.06) : 'var(--bg-card)',
    border: custom ? `1px solid ${eHsl('toolkit', 0.3)}` : '1px solid var(--border)',
    cursor: 'pointer', textAlign: 'left', display: 'flex', flexDirection: 'column', gap: 4, minHeight: 78,
  }}>
    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
      <div style={{
        width: 28, height: 28, borderRadius: 'var(--r-sm)', background: eHsl(color, 0.14),
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 14,
      }} aria-hidden="true">{icon}</div>
      {custom && <span style={{
        padding: '1px 5px', borderRadius: 'var(--r-pill)', background: eHsl('toolkit', 0.14), color: eHsl('toolkit'),
        fontFamily: 'var(--f-mono)', fontSize: 8, fontWeight: 800, textTransform: 'uppercase', letterSpacing: '.05em',
      }}>Custom</span>}
    </div>
    <div style={{ fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, color: 'var(--text)' }}>{name}</div>
    <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9.5, color: 'var(--text-muted)', fontWeight: 600 }}>{desc}</div>
  </button>
);

const SessionToolsRail = () => (
  <div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 10, overflowY: 'auto' }}>
    <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em' }}>Quick tools</div>
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 7 }}>
      <ToolCard icon="🎲" name="Dadi" desc="d4 d6 d8 d10 d20" />
      <ToolCard icon="⏱" name="Timer" desc="Countdown · audio" />
      <ToolCard icon="🔢" name="Contatore" desc="Multi · history" />
      <ToolCard icon="🪙" name="Moneta" desc="Flip · history" />
    </div>
    <div style={{ padding: '9px 11px', borderRadius: 'var(--r-md)', background: eHsl('tool', 0.06), border: `1px dashed ${eHsl('tool', 0.3)}` }}>
      <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800, color: eHsl('tool'), textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 4 }}>Ultimo tiro · 14:41</div>
      <div style={{ fontFamily: 'var(--f-mono)', fontSize: 16, fontWeight: 800, color: 'var(--text)', fontVariantNumeric: 'tabular-nums' }}>2d6 → 5 + 3 = <strong style={{ color: eHsl('tool') }}>8</strong></div>
    </div>
    <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginTop: 4 }}>Custom · Wingspan</div>
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 7 }}>
      <ToolCard icon="🦜" name="Pesca uccello" desc="Random bird card" color="toolkit" custom />
      <ToolCard icon="🌳" name="Habitat picker" desc="Forest · Grassland" color="toolkit" custom />
    </div>
    <button type="button" style={{
      padding: '10px', borderRadius: 'var(--r-md)', background: 'transparent', color: eHsl('tool'),
      border: `1px dashed ${eHsl('tool', 0.4)}`, fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700, cursor: 'pointer',
    }}>+ Aggiungi tool</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── EXISTING TAB · AGENT CHAT (embeddable) ──────────
// ═══════════════════════════════════════════════════════
// Embed body (messages + suggestions + input) — no header, so the
// new "agent" tab can wrap it with its own richer AgentHeader.
const LiveAgentChatEmbed = ({ dimmed }) => (
  <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, opacity: dimmed ? 0.55 : 1 }}>
    <div style={{ flex: 1, overflowY: 'auto', padding: 12, display: 'flex', flexDirection: 'column', gap: 10 }}>
      {M.CHAT.map(m => {
        const isAgent = m.from === 'agent';
        return (
          <div key={m.id} style={{ alignSelf: isAgent ? 'flex-start' : 'flex-end', maxWidth: '88%', display: 'flex', flexDirection: 'column', gap: 3 }}>
            <div style={{
              padding: '7px 11px', borderRadius: 'var(--r-lg)',
              background: isAgent ? eHsl('agent', 0.1) : eHsl('chat', 0.12),
              border: `1px solid ${isAgent ? eHsl('agent', 0.25) : eHsl('chat', 0.25)}`,
              borderTopLeftRadius: isAgent ? 4 : 'var(--r-lg)', borderTopRightRadius: isAgent ? 'var(--r-lg)' : 4,
              fontFamily: 'var(--f-body)', fontSize: 12.5, fontWeight: 500, color: 'var(--text)', lineHeight: 1.45,
            }}>{m.text}</div>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9, color: 'var(--text-muted)', fontWeight: 700, alignSelf: isAgent ? 'flex-start' : 'flex-end', padding: '0 4px' }}>{m.t}</div>
            {m.citations && (
              <div style={{ display: 'flex', gap: 3, flexWrap: 'wrap', marginTop: 2 }}>
                {m.citations.map((c, i) => (
                  <span key={i} style={{
                    display: 'inline-flex', alignItems: 'center', gap: 3, padding: '2px 6px', borderRadius: 'var(--r-pill)',
                    background: eHsl('kb', 0.1), border: `1px solid ${eHsl('kb', 0.25)}`, color: eHsl('kb'),
                    fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 700,
                  }}><span aria-hidden="true">📄</span>{c}</span>
                ))}
              </div>
            )}
          </div>
        );
      })}
    </div>
    <div style={{ padding: '8px 12px', borderTop: '1px solid var(--border-light)', display: 'flex', gap: 5, flexWrap: 'wrap' }}>
      {M.CHAT_SUGGESTIONS.map(s => (
        <button key={s} type="button" style={{
          padding: '5px 10px', borderRadius: 'var(--r-pill)', background: eHsl('chat', 0.08),
          border: `1px solid ${eHsl('chat', 0.25)}`, color: eHsl('chat'),
          fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 700, cursor: 'pointer',
        }}>{s}</button>
      ))}
    </div>
    <div style={{ padding: '10px 12px', borderTop: '1px solid var(--border)', display: 'flex', gap: 6, alignItems: 'flex-end' }}>
      <textarea placeholder="Chiedi all'agente..." rows={1} style={{
        flex: 1, padding: '8px 11px', borderRadius: 'var(--r-md)', border: '1px solid var(--border)',
        background: 'var(--bg)', color: 'var(--text)', fontFamily: 'var(--f-body)', fontSize: 12.5, resize: 'none', minHeight: 34,
      }} />
      <button type="button" aria-label="Voice" style={{ width: 36, height: 36, borderRadius: 'var(--r-md)', background: 'var(--bg-card)', border: '1px solid var(--border)', color: 'var(--text-sec)', fontSize: 14, cursor: 'pointer', flexShrink: 0 }}>🎙</button>
      <button type="button" aria-label="Invia" style={{ width: 36, height: 36, borderRadius: 'var(--r-md)', background: eHsl('chat'), color: '#fff', border: 'none', fontSize: 14, cursor: 'pointer', flexShrink: 0, boxShadow: `0 3px 10px ${eHsl('chat', 0.4)}` }}>↑</button>
    </div>
  </div>
);
window.LiveAgentChatEmbed = LiveAgentChatEmbed;

// Full chat tab = original agent header + embed body
const LiveAgentChat = () => (
  <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
    <div style={{ padding: '10px 12px', background: eHsl('agent', 0.06), borderBottom: `1px solid ${eHsl('agent', 0.2)}`, display: 'flex', alignItems: 'center', gap: 10 }}>
      <div style={{
        width: 36, height: 36, borderRadius: '50%', position: 'relative',
        background: `linear-gradient(135deg, ${eHsl('agent')}, ${eHsl('agent', 0.6)})`,
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 16,
      }} aria-hidden="true">🦜
        <span className="mai-pulse-dot" style={{ position: 'absolute', bottom: -1, right: -1, width: 11, height: 11, borderRadius: '50%', background: 'hsl(140,60%,45%)', border: '2px solid var(--bg-card)' }} />
      </div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, color: 'var(--text)' }}>Wingspan Coach</div>
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9.5, color: 'hsl(140,50%,35%)', fontWeight: 700 }}>● Online · Risponde in 1-2s</div>
      </div>
    </div>
    <LiveAgentChatEmbed />
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── EXISTING TAB · NOTES ────────────────────────────
// ═══════════════════════════════════════════════════════
const LiveSessionNotes = () => (
  <div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 12, flex: 1, overflowY: 'auto' }}>
    <div>
      <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 6 }}>Quick notes</div>
      <textarea defaultValue={"Marco apertura aggressiva sul forest.\nWetland con 3 uccelli a turno 8.\n→ Considerare bonus card 'Eggs in nest' a fine partita."} rows={5} style={{
        width: '100%', padding: '10px 12px', borderRadius: 'var(--r-md)', border: '1px solid var(--border)',
        background: 'var(--bg-card)', color: 'var(--text)', fontFamily: 'var(--f-mono)', fontSize: 12, lineHeight: 1.55, resize: 'vertical', minHeight: 100,
      }} />
    </div>
    <div>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em' }}>Foto · 2</div>
        <button type="button" style={{ padding: '3px 8px', borderRadius: 'var(--r-pill)', background: eHsl('kb', 0.1), color: eHsl('kb'), border: `1px solid ${eHsl('kb', 0.3)}`, fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800, cursor: 'pointer', textTransform: 'uppercase', letterSpacing: '.04em' }}>+ Foto</button>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 6 }}>
        {[{ lb: 'Tableau T8', g: 'linear-gradient(135deg, hsl(85,60%,55%), hsl(180,50%,40%))' }, { lb: 'Wetland', g: 'linear-gradient(135deg, hsl(200,60%,55%), hsl(230,50%,40%))' }].map((p, i) => (
          <div key={i} style={{ aspectRatio: '1', borderRadius: 'var(--r-sm)', background: p.g, display: 'flex', alignItems: 'flex-end', padding: 4 }}>
            <span style={{ fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800, color: '#fff', background: 'rgba(0,0,0,.4)', padding: '1px 5px', borderRadius: 'var(--r-pill)' }}>{p.lb}</span>
          </div>
        ))}
        <button type="button" aria-label="Aggiungi foto" style={{ aspectRatio: '1', borderRadius: 'var(--r-sm)', background: 'transparent', border: '1px dashed var(--border-strong)', color: 'var(--text-muted)', fontSize: 20, cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>+</button>
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── TAB REGISTRY (single source for desktop + mobile) ─
// ═══════════════════════════════════════════════════════
// `stateful` tabs accept a state prop (default/empty/loading/error/sse).
const TABS = [
  { id: 'scores',  icon: '🎯', label: 'Score',   entity: 'session', stateful: true,  render: (st) => <LT.ScoresTab state={st} /> },
  { id: 'players', icon: '👥', label: 'Player',  entity: 'player',  stateful: true,  render: (st) => <LT.PlayersTab state={st} /> },
  { id: 'agent',   icon: '🤖', label: 'Agente',  entity: 'agent',   stateful: true,  render: (st) => <LT.AgentTab state={st} /> },
  { id: 'chat',    icon: '💬', label: 'Chat',    entity: 'chat',    stateful: false, render: () => <LiveAgentChat /> },
  { id: 'photos',  icon: '📷', label: 'Foto',    entity: 'kb',      stateful: true,  render: (st) => <LT.PhotosTab state={st} /> },
  { id: 'tools',   icon: '🔧', label: 'Tools',   entity: 'tool',    stateful: false, render: () => <SessionToolsRail /> },
  { id: 'notes',   icon: '📋', label: 'Note',    entity: 'kb',      stateful: false, render: () => <LiveSessionNotes /> },
];
const STATES = [
  { id: 'default', lb: 'Default' }, { id: 'empty', lb: 'Empty' }, { id: 'loading', lb: 'Loading' },
  { id: 'error', lb: 'Error' }, { id: 'sse', lb: 'SSE-off' },
];

// state selector (segmented) shown only when active tab is stateful
const StateSwitch = ({ value, onChange }) => (
  <div className="mai-cb-scroll" style={{ display: 'flex', gap: 4, overflowX: 'auto', padding: '7px 10px', borderBottom: '1px solid var(--border-light)', background: 'var(--bg)' }}>
    <span style={{ fontFamily: 'var(--f-mono)', fontSize: 8.5, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', alignSelf: 'center', flexShrink: 0, marginRight: 2 }}>Stato</span>
    {STATES.map(s => {
      const active = value === s.id;
      return (
        <button key={s.id} type="button" onClick={() => onChange(s.id)} aria-pressed={active} style={{
          padding: '3px 9px', borderRadius: 'var(--r-pill)', flexShrink: 0,
          background: active ? eHsl('session', 0.14) : 'var(--bg-card)',
          border: active ? `1px solid ${eHsl('session', 0.4)}` : '1px solid var(--border)',
          color: active ? eHsl('session') : 'var(--text-sec)',
          fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800, cursor: 'pointer',
        }}>{s.lb}</button>
      );
    })}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── RIGHT COLUMN (extended: 7 tabs, scrollable strip) ─
// ═══════════════════════════════════════════════════════
const RightColumnTabs = ({ initial = 'scores', initialState = 'default', width = 380, embedded }) => {
  const [tab, setTab] = useState2(initial);
  const [st, setSt] = useState2(initialState);
  const active = TABS.find(t => t.id === tab);
  return (
    <aside style={{
      width: embedded ? '100%' : width, flexShrink: 0, height: '100%',
      background: 'var(--bg-card)', borderLeft: embedded ? 'none' : '1px solid var(--border)',
      display: 'flex', flexDirection: 'column', minHeight: 0,
    }}>
      {/* 7 tabs fit without scroll: inactive = icon-only, active expands to show its label */}
      <div role="tablist" style={{ display: 'flex', borderBottom: '1px solid var(--border-light)', flexShrink: 0, overflow: 'hidden' }}>
        {TABS.map(t => {
          const on = tab === t.id;
          return (
            <button key={t.id} type="button" role="tab" aria-selected={on} aria-label={t.label} title={t.label} onClick={() => setTab(t.id)} style={{
              flex: on ? '1 1 auto' : '0 0 46px', minWidth: 0, padding: on ? '10px 12px' : '10px 4px',
              background: on ? eHsl(t.entity, 0.06) : 'transparent', border: 'none',
              borderBottom: on ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent',
              color: on ? eHsl(t.entity) : 'var(--text-sec)',
              fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer',
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 5, whiteSpace: 'nowrap',
              transition: 'flex var(--dur-sm) var(--ease-out)',
            }}>
              <span aria-hidden="true" style={{ fontSize: 15 }}>{t.icon}</span>{on && <span>{t.label}</span>}
            </button>
          );
        })}
      </div>
      {active.stateful && <StateSwitch value={st} onChange={setSt} />}
      <div role="tabpanel" style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
        {active.render(st)}
      </div>
    </aside>
  );
};

// ═══════════════════════════════════════════════════════
// ─── DESKTOP LAYOUT ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopBody = ({ emptyTurn, initialTab, initialState }) => (
  <div style={{ display: 'flex', flex: 1, minHeight: 0 }}>
    <P.PlayerRosterLive />
    <main style={{ flex: 1, padding: 16, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 12, minWidth: 0 }}>
      <P.LiveScoringPanel emptyTurn={emptyTurn} />
      <P.ActionLogTimeline />
    </main>
    <RightColumnTabs initial={initialTab} initialState={initialState} />
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── MOBILE LAYOUT (bottom-sheet tabs) ───────────────
// ═══════════════════════════════════════════════════════
const MobileTabSheet = ({ open, tab, st, setSt, onClose }) => {
  const active = TABS.find(t => t.id === tab) || TABS[0];
  return (
    <div aria-hidden={!open} style={{
      position: 'absolute', inset: 0, zIndex: 50, pointerEvents: open ? 'auto' : 'none',
    }}>
      <div onClick={onClose} style={{
        position: 'absolute', inset: 0, background: 'rgba(15,12,30,.5)', backdropFilter: 'blur(3px)',
        opacity: open ? 1 : 0, transition: 'opacity var(--dur-md) var(--ease-out)',
      }} />
      <div role="dialog" aria-label={active.label} style={{
        position: 'absolute', left: 0, right: 0, bottom: 0, height: '82%',
        background: 'var(--bg-card)', borderTopLeftRadius: 'var(--r-2xl)', borderTopRightRadius: 'var(--r-2xl)',
        borderTop: `3px solid ${eHsl(active.entity)}`, boxShadow: 'var(--shadow-drawer)',
        display: 'flex', flexDirection: 'column', minHeight: 0,
        transform: open ? 'translateY(0)' : 'translateY(101%)',
        transition: 'transform var(--dur-lg) var(--ease-spring)',
      }}>
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', paddingTop: 8, flexShrink: 0 }}>
          <div style={{ width: 38, height: 4, borderRadius: 999, background: 'var(--border-strong)' }} />
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 14px 10px', flexShrink: 0 }}>
          <span aria-hidden="true" style={{ fontSize: 16 }}>{active.icon}</span>
          <span style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: eHsl(active.entity) }}>{active.label}</span>
          <div style={{ flex: 1 }} />
          <button type="button" onClick={onClose} aria-label="Chiudi" style={{ width: 28, height: 28, borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontSize: 13 }}>✕</button>
        </div>
        {active.stateful && <StateSwitch value={st} onChange={setSt} />}
        <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(st)}</div>
      </div>
    </div>
  );
};

const MobileBody = ({ initialTab }) => {
  const [sheet, setSheet] = useState2(initialTab || null);
  const [st, setSt] = useState2('default');
  // base content = live scoring + log (the always-on session surface)
  return (
    <>
      <div style={{ flex: 1, overflowY: 'auto', minHeight: 0, background: 'var(--bg)' }}>
        <div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap: 12 }}>
          <P.LiveScoringPanel compact />
          <P.ActionLogTimeline compact />
        </div>
      </div>
      {/* scrollable tab strip → opens sheet */}
      <nav className="mai-cb-scroll" aria-label="Sezioni sessione" style={{
        display: 'flex', gap: 6, overflowX: 'auto', flexShrink: 0, padding: '8px 10px',
        background: 'var(--glass-bg)', backdropFilter: 'blur(14px)', borderTop: '1px solid var(--border)',
      }}>
        {TABS.map(t => (
          <button key={t.id} type="button" onClick={() => { setSt('default'); setSheet(t.id); }} style={{
            flex: '0 0 auto', display: 'inline-flex', alignItems: 'center', gap: 5, padding: '7px 12px',
            borderRadius: 'var(--r-pill)', background: eHsl(t.entity, 0.1), border: `1px solid ${eHsl(t.entity, 0.28)}`,
            color: eHsl(t.entity), fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer', whiteSpace: 'nowrap',
          }}>
            <span aria-hidden="true">{t.icon}</span>{t.label}
          </button>
        ))}
      </nav>
      <MobileTabSheet open={!!sheet} tab={sheet} st={st} setSt={setSt} onClose={() => setSheet(null)} />
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── FRAMES ───────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color: 'var(--text)' }}>
    <span style={{ fontFamily: 'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);

const PhoneScreen = ({ initialTab }) => (
  <>
    <PhoneSbar />
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', position: 'relative', overflow: 'hidden', minHeight: 0 }}>
      <P.LiveTopBar compact />
      <MobileBody initialTab={initialTab} />
    </div>
  </>
);

const PhoneShell = ({ label, desc, dark, initialTab }) => (
  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
    <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)', textTransform: 'uppercase', letterSpacing: '.08em', fontWeight: 700 }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark</span>}</div>
    <div className="phone" data-theme={dark ? 'dark' : undefined}><PhoneScreen initialTab={initialTab} /></div>
    {desc && <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 340, textAlign: 'center', lineHeight: 1.55 }}>{desc}</div>}
  </div>
);

const DesktopFrame = ({ label, desc, dark, children }) => (
  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, width: '100%' }} data-theme={dark ? 'dark' : undefined}>
    <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)', textTransform: 'uppercase', letterSpacing: '.08em', fontWeight: 700 }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark (primario)</span>}</div>
    <div style={{ width: '100%', maxWidth: 1400, borderRadius: 'var(--r-xl)', border: '1px solid var(--border)', background: 'var(--bg)', overflow: 'hidden', boxShadow: 'var(--shadow-lg)' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '9px 14px', background: 'var(--bg-muted)', borderBottom: '1px solid var(--border)', fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)' }}>
        <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-event))' }} />
        <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-warning))' }} />
        <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-toolkit))' }} />
        <span style={{ flex: 1, textAlign: 'center', letterSpacing: '.04em' }}>meepleai.app/sessions/wing-3/live?tab=scores</span>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', height: 720, background: 'var(--bg)', overflow: 'hidden' }}>{children}</div>
    </div>
    {desc && <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 820, textAlign: 'center', lineHeight: 1.55 }}>{desc}</div>}
  </div>
);

// states-gallery panel (fixed rail-sized frame showing one tab+state)
const PanelFrame = ({ label, entity, children, dark }) => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }} data-theme={dark ? 'dark' : undefined}>
    <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-sec)', textTransform: 'uppercase', letterSpacing: '.07em', fontWeight: 800 }}>{label}</div>
    <div style={{ width: 320, height: 460, borderRadius: 'var(--r-lg)', border: `1px solid ${eHsl(entity, 0.3)}`, background: 'var(--bg-card)', overflow: 'hidden', display: 'flex', flexDirection: 'column', boxShadow: 'var(--shadow-sm)' }}>
      {children}
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
function ThemeToggle() {
  const [dark, setDark] = useState2(true);
  useEffect2(() => { document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light'); }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle" aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

const NEW_TABS = TABS.filter(t => t.stateful);

function App() {
  return (
    <div className="stage">
      <ThemeToggle />
      <div className="stage-wrap">
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Sessions consolidation · Live ⚡</div>
        <h1>Session live — RightColumnTabs esteso a 7 tab</h1>
        <p className="lead">
          Le 4 sub-route <strong>scores · photos · agent · players</strong> diventano tab dentro il
          <strong> RightColumnTabs</strong> esistente (con tools · chat · note). Deep-link <code>?tab=&lt;id&gt;</code>,
          stream SSE preservato tra i cambi tab. Desktop: rail 380 con strip scrollabile + selettore stato.
          Mobile 375: strip orizzontale → <strong>bottom-sheet</strong> drawer. Dark = modalità primaria.
        </p>

        {/* INTERACTIVE — desktop */}
        <div className="section-label">Interattivo · Desktop 1280 — clicca le tab, cambia stato dal selettore</div>
        <DesktopFrame label="Desktop · live · consolidato" dark
          desc="3-col: roster 300 · scoring+log · RightColumnTabs 380 (7 tab scrollabili). Le tab nuove mostrano il selettore Stato (default/empty/loading/error/SSE-off). Tab Chat/Tools/Note invariate.">
          <P.LiveTopBar />
          <DesktopBody initialTab="scores" initialState="default" />
        </DesktopFrame>

        {/* INTERACTIVE — mobile */}
        <div className="section-label">Interattivo · Mobile 375 — strip → bottom-sheet</div>
        <div className="phones-grid">
          <PhoneShell label="01 · Mobile · sheet chiuso (base)" desc="Surface live sempre visibile (scoring + action log). Strip tab in basso, scrollabile." />
          <PhoneShell label="02 · Mobile · sheet Score aperto" initialTab="scores" desc="Tap su una tab apre il bottom-sheet con handle, header entity-color, selettore stato e contenuto." />
          <PhoneShell label="03 · Mobile · sheet Player · dark" dark initialTab="players" desc="Gestione live: mute / adj score / espelli. Stato connessione per giocatore (host/online/away)." />
        </div>

        {/* STATES GALLERY */}
        <div className="section-label">Gallery stati — ogni tab nuova × 5 stati (default · empty · loading · error · SSE-off)</div>
        {NEW_TABS.map(t => (
          <div key={t.id} style={{ marginBottom: 28 }}>
            <div style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: eHsl(t.entity), marginBottom: 10, display: 'flex', alignItems: 'center', gap: 7 }}>
              <span aria-hidden="true">{t.icon}</span>{t.label} <span style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)', fontWeight: 700 }}>· /sessions/[id]/{t.id}</span>
            </div>
            <div style={{ display: 'flex', gap: 16, overflowX: 'auto', paddingBottom: 8 }}>
              {STATES.map(s => (
                <PanelFrame key={s.id} label={s.lb} entity={t.entity} dark={s.id === 'sse'}>
                  {t.render(s.id)}
                </PanelFrame>
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
        .mai-cb-scroll::-webkit-scrollbar { height: 0; display: none; }
        .mai-cb-scroll { scrollbar-width: none; }
        @media (prefers-reduced-motion: reduce) { .mai-pulse-dot, .mai-shimmer { animation: none !important; } }
        .phones-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(380px, 1fr)); gap: var(--s-7); align-items: start; }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage code { background: var(--bg-muted); padding: 1px 6px; border-radius: var(--r-sm); font-size: .85em; }
        .phone > div::-webkit-scrollbar { display: none; }
        button:focus-visible, a:focus-visible, input:focus-visible, textarea:focus-visible { outline: 2px solid hsl(var(--c-session)); outline-offset: 2px; }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
