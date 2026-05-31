/* MeepleAI SP4 wave 2 — Session live NEW TABS — window.LiveTabs
   4 consolidated routes → tabs inside the existing RightColumnTabs:
     scores  → /sessions/[id]/scores   (live scoring, realtime)
     photos  → /sessions/[id]/photos   (in-session gallery, lazy)
     agent   → /sessions/[id]/agent    (in-session agent panel)
     players → /sessions/[id]/players  (live roster mgmt: mute/adj/kick)
   Each tab renders 5 states: default · empty · loading · error · sse.
   Consumes window.MAI (data + helpers) and window.LiveAgentChatEmbed (set by main). */

(function () {
  const { useState } = React;
  const M = window.MAI;
  const eHsl = M.entityHsl;
  const { StateBlock, Shimmer, SseBanner } = M;

  // shared scroll body
  const Body = ({ children, style }) => (
    <div style={{ flex: 1, minHeight: 0, overflowY: 'auto', display: 'flex', flexDirection: 'column', ...style }}>
      {children}
    </div>
  );
  const Pad = ({ children, gap = 10 }) => (
    <div style={{ padding: 12, display: 'flex', flexDirection: 'column', gap }}>{children}</div>
  );
  const Centered = ({ children }) => (
    <div style={{ flex: 1, display: 'flex', padding: 16 }}>{children}</div>
  );

  // ══════════════════════════════════════════════════
  // 1 · SCORES — live scoring, realtime
  // ══════════════════════════════════════════════════
  const ScoresTab = ({ state = 'default' }) => {
    if (state === 'loading') return (
      <Body><Pad>{[0, 1, 2, 3].map(i => <Shimmer key={i} h={64} r="var(--r-lg)" />)}</Pad></Body>
    );
    if (state === 'error') return (
      <Body><Centered><StateBlock tone="error" icon="⚠" title="Calcolo punteggi fallito"
        body="Non riusciamo a ricalcolare il live score." action={
          <button type="button" style={ctaDanger}>↻ Riprova</button>} /></Centered></Body>
    );
    if (state === 'empty') return (
      <Body><Centered><StateBlock icon="🎯" title="Nessun punteggio ancora"
        body="I punteggi compaiono qui appena registri il primo turno."
        action={<button type="button" style={ctaTint('session')}>Vai allo scoring</button>} /></Centered></Body>
    );
    const sse = state === 'sse';
    const leader = M.ROSTER[0];
    const gap = leader.score - M.ROSTER[1].score;
    return (
      <Body>
        {sse && <SseBanner where="scores" />}
        <Pad gap={8}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', flex: 1 }}>Live scores</div>
            <span style={{
              display: 'inline-flex', alignItems: 'center', gap: 5, padding: '2px 8px', borderRadius: 'var(--r-pill)',
              background: sse ? 'var(--bg-muted)' : eHsl('session', 0.12), color: sse ? 'var(--text-muted)' : eHsl('session'),
              fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800, textTransform: 'uppercase', letterSpacing: '.05em',
            }}>
              <span aria-hidden="true" className={sse ? undefined : 'mai-pulse-dot'} style={{ width: 6, height: 6, borderRadius: '50%', background: 'currentColor' }} />
              {sse ? 'In pausa' : 'Realtime'}
            </span>
          </div>
          {/* leader gap callout */}
          <div style={{
            display: 'flex', alignItems: 'center', gap: 10, padding: '10px 12px', borderRadius: 'var(--r-md)',
            background: eHsl('toolkit', 0.08), border: `1px solid ${eHsl('toolkit', 0.25)}`,
            opacity: sse ? .6 : 1,
          }}>
            <span aria-hidden="true" style={{ fontSize: 18 }}>🏆</span>
            <div style={{ flex: 1 }}>
              <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, color: 'var(--text)' }}>{leader.name} in testa</div>
              <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 700, color: 'var(--text-muted)' }}>+{gap} su {M.ROSTER[1].name}</div>
            </div>
            <div style={{ fontFamily: 'var(--f-display)', fontSize: 26, fontWeight: 800, color: eHsl('toolkit'), fontVariantNumeric: 'tabular-nums' }}>{leader.score}</div>
          </div>
          {/* ranked rows with progress bars */}
          {M.ROSTER.map((p, i) => {
            const pct = Math.round((p.score / leader.score) * 100);
            return (
              <div key={p.id} style={{
                display: 'flex', alignItems: 'center', gap: 10, padding: '8px 10px', borderRadius: 'var(--r-md)',
                background: 'var(--bg-card)', border: '1px solid var(--border)', opacity: sse ? .6 : 1,
              }}>
                <span style={{ fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 800, color: 'var(--text-muted)', width: 14 }}>{i + 1}</span>
                <div style={{
                  width: 30, height: 30, borderRadius: '50%', flexShrink: 0,
                  background: `linear-gradient(135deg, hsl(${p.hue},70%,64%), hsl(${p.hue},58%,44%))`,
                  color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800,
                }}>{p.name[0]}</div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, color: 'var(--text)' }}>{p.name}</div>
                  <div style={{ height: 5, borderRadius: 999, background: 'var(--bg-muted)', marginTop: 4, overflow: 'hidden' }}>
                    <div style={{ width: `${pct}%`, height: '100%', borderRadius: 999, background: i === 0 ? eHsl('toolkit') : eHsl('session', 0.7) }} />
                  </div>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <div style={{ fontFamily: 'var(--f-display)', fontSize: 19, fontWeight: 800, color: 'var(--text)', fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{p.score}</div>
                  {p.turnDelta !== 0 && <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800, color: eHsl('toolkit') }}>+{p.turnDelta}</div>}
                </div>
              </div>
            );
          })}
        </Pad>
      </Body>
    );
  };

  // ══════════════════════════════════════════════════
  // 2 · PHOTOS — in-session gallery, lazy
  // ══════════════════════════════════════════════════
  const PHOTOS = [
    { lb: 'Tableau T8', turn: 8, g: 'linear-gradient(135deg, hsl(85,60%,55%), hsl(180,50%,40%))' },
    { lb: 'Wetland', turn: 10, g: 'linear-gradient(135deg, hsl(200,60%,55%), hsl(230,50%,40%))' },
    { lb: 'Mano Anna', turn: 11, g: 'linear-gradient(135deg, hsl(330,55%,58%), hsl(20,60%,48%))' },
    { lb: 'Plancia', turn: 12, g: 'linear-gradient(135deg, hsl(40,70%,60%), hsl(10,60%,45%))' },
  ];
  const PhotosTab = ({ state = 'default' }) => {
    if (state === 'loading') return (
      <Body><Pad>
        <Shimmer h={14} r="var(--r-sm)" style={{ width: 90 }} />
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
          {[0, 1, 2, 3].map(i => <Shimmer key={i} h={110} r="var(--r-md)" />)}
        </div>
      </Pad></Body>
    );
    if (state === 'error') return (
      <Body><Centered><StateBlock tone="error" icon="⚠" title="Foto non caricate"
        body="La galleria non risponde." action={<button type="button" style={ctaDanger}>↻ Riprova</button>} /></Centered></Body>
    );
    if (state === 'empty') return (
      <Body><Centered><StateBlock icon="📷" title="Nessuna foto"
        body="Cattura il tavolo durante la partita: tableau, mani, plancia finale."
        action={<button type="button" style={ctaTint('kb')}>+ Aggiungi foto</button>} /></Centered></Body>
    );
    const sse = state === 'sse';
    return (
      <Body>
        {sse && <SseBanner where="foto" />}
        <Pad>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', flex: 1 }}>Foto · {PHOTOS.length}</div>
            <button type="button" style={{
              padding: '3px 9px', borderRadius: 'var(--r-pill)', background: eHsl('kb', 0.1), color: eHsl('kb'),
              border: `1px solid ${eHsl('kb', 0.3)}`, fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800, cursor: 'pointer',
              textTransform: 'uppercase', letterSpacing: '.04em',
            }}>+ Foto</button>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
            {PHOTOS.map((p, i) => (
              <button key={i} type="button" style={{
                aspectRatio: '4/3', borderRadius: 'var(--r-md)', background: p.g, border: '1px solid var(--border)',
                position: 'relative', overflow: 'hidden', cursor: 'pointer', padding: 0,
              }}>
                <span style={{
                  position: 'absolute', top: 6, left: 6, fontFamily: 'var(--f-mono)', fontSize: 8.5, fontWeight: 800,
                  color: '#fff', background: 'rgba(0,0,0,.45)', padding: '1px 6px', borderRadius: 'var(--r-pill)',
                }}>T{p.turn}</span>
                <span style={{
                  position: 'absolute', bottom: 6, left: 6, fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
                  color: '#fff', background: 'rgba(0,0,0,.45)', padding: '2px 6px', borderRadius: 'var(--r-pill)',
                }}>{p.lb}</span>
              </button>
            ))}
            <button type="button" aria-label="Aggiungi foto" style={{
              aspectRatio: '4/3', borderRadius: 'var(--r-md)', background: 'transparent',
              border: '1px dashed var(--border-strong)', color: 'var(--text-muted)', fontSize: 24, cursor: 'pointer',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
            }}>+</button>
          </div>
        </Pad>
      </Body>
    );
  };

  // ══════════════════════════════════════════════════
  // 3 · AGENT — in-session agent panel (chat embedded)
  // ══════════════════════════════════════════════════
  const AgentHeader = ({ offline }) => (
    <div style={{
      padding: '10px 12px', background: eHsl('agent', 0.06), borderBottom: `1px solid ${eHsl('agent', 0.2)}`,
      display: 'flex', alignItems: 'center', gap: 10, flexShrink: 0,
    }}>
      <div style={{
        width: 36, height: 36, borderRadius: '50%', position: 'relative',
        background: `linear-gradient(135deg, ${eHsl('agent')}, ${eHsl('agent', 0.6)})`,
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 16,
      }} aria-hidden="true">🦜
        <span className={offline ? undefined : 'mai-pulse-dot'} style={{
          position: 'absolute', bottom: -1, right: -1, width: 11, height: 11, borderRadius: '50%',
          background: offline ? 'var(--text-muted)' : 'hsl(140,60%,45%)', border: '2px solid var(--bg-card)',
        }} />
      </div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, color: 'var(--text)' }}>Wingspan Coach</div>
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 700, color: offline ? 'var(--text-muted)' : 'hsl(140,50%,35%)' }}>
          {offline ? '○ Riconnessione…' : '● Online · 2 KB · claude-3.5'}
        </div>
      </div>
      <span style={{
        display: 'inline-flex', gap: 4, padding: '2px 7px', borderRadius: 'var(--r-pill)',
        background: eHsl('kb', 0.1), color: eHsl('kb'), border: `1px solid ${eHsl('kb', 0.25)}`,
        fontFamily: 'var(--f-mono)', fontSize: 8.5, fontWeight: 800,
      }}>📄 2</span>
    </div>
  );

  const AgentTab = ({ state = 'default' }) => {
    if (state === 'loading') return (
      <Body><AgentHeader /><Pad>
        {[0, 1, 2].map(i => <Shimmer key={i} h={i === 1 ? 56 : 38} r="var(--r-lg)" style={{ width: i % 2 ? '70%' : '85%', alignSelf: i % 2 ? 'flex-end' : 'flex-start' }} />)}
      </Pad></Body>
    );
    if (state === 'error') return (
      <Body><AgentHeader offline /><Centered><StateBlock tone="error" icon="🤖" title="Agente non disponibile"
        body="Il modello non risponde in questo momento." action={<button type="button" style={ctaDanger}>↻ Riprova</button>} /></Centered></Body>
    );
    if (state === 'sse') return (
      <Body><AgentHeader offline />
        <SseBanner where="agente" />
        {window.LiveAgentChatEmbed ? <window.LiveAgentChatEmbed dimmed /> : null}
      </Body>
    );
    if (state === 'empty') return (
      <Body><AgentHeader /><Centered><StateBlock icon="💬" title="Chiedi al coach"
        body="Domande su regole, edge case e strategia — con citazioni dal manuale."
        action={
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5, justifyContent: 'center' }}>
            {M.CHAT_SUGGESTIONS.map(s => (
              <span key={s} style={{
                padding: '5px 10px', borderRadius: 'var(--r-pill)', background: eHsl('chat', 0.08),
                border: `1px solid ${eHsl('chat', 0.25)}`, color: eHsl('chat'),
                fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 700,
              }}>{s}</span>
            ))}
          </div>
        } /></Centered></Body>
    );
    // default: full agent panel = header + embedded chat
    return (
      <Body>
        <AgentHeader />
        {window.LiveAgentChatEmbed ? <window.LiveAgentChatEmbed /> : null}
      </Body>
    );
  };

  // ══════════════════════════════════════════════════
  // 4 · PLAYERS — live roster mgmt (mute / score adj / kick)
  // ══════════════════════════════════════════════════
  const CONN = { 'p-marco': 'host', 'p-anna': 'online', 'p-luca': 'online', 'p-sara': 'away' };
  const PlayerRow = ({ p, conn, sse }) => {
    const [muted, setMuted] = useState(false);
    const isHost = conn === 'host';
    const dot = sse ? 'var(--text-muted)' : (conn === 'away' ? eHsl('agent') : eHsl('toolkit'));
    return (
      <div style={{
        padding: '10px 11px', borderRadius: 'var(--r-md)', background: 'var(--bg-card)',
        border: '1px solid var(--border)', display: 'flex', flexDirection: 'column', gap: 8,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div style={{ position: 'relative', flexShrink: 0 }}>
            <div style={{
              width: 34, height: 34, borderRadius: '50%',
              background: `linear-gradient(135deg, hsl(${p.hue},70%,64%), hsl(${p.hue},58%,44%))`,
              color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800,
            }}>{p.name[0]}</div>
            <span style={{ position: 'absolute', bottom: -1, right: -1, width: 10, height: 10, borderRadius: '50%', background: dot, border: '2px solid var(--bg-card)' }} />
          </div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, color: 'var(--text)', display: 'flex', alignItems: 'center', gap: 5 }}>
              {p.name}
              {isHost && <span style={{
                fontFamily: 'var(--f-mono)', fontSize: 8, fontWeight: 800, color: eHsl('player'),
                background: eHsl('player', 0.14), padding: '1px 5px', borderRadius: 'var(--r-pill)',
                textTransform: 'uppercase', letterSpacing: '.05em',
              }}>Host</span>}
            </div>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.04em' }}>
              {sse ? 'stato sconosciuto' : conn}
            </div>
          </div>
          <div style={{ fontFamily: 'var(--f-display)', fontSize: 18, fontWeight: 800, color: 'var(--text)', fontVariantNumeric: 'tabular-nums' }}>{p.score}</div>
        </div>
        <div style={{ display: 'flex', gap: 6 }}>
          {/* score adjust */}
          <div style={{ display: 'inline-flex', alignItems: 'center', borderRadius: 'var(--r-md)', border: '1px solid var(--border)', overflow: 'hidden' }}>
            <button type="button" aria-label="−1" style={miniBtn}>−</button>
            <span style={{ padding: '0 8px', fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800, color: 'var(--text-muted)' }}>adj</span>
            <button type="button" aria-label="+1" style={miniBtn}>+</button>
          </div>
          <button type="button" onClick={() => setMuted(m => !m)} aria-pressed={muted} style={{
            ...pillBtn, background: muted ? eHsl('agent', 0.12) : 'var(--bg-muted)',
            color: muted ? eHsl('agent') : 'var(--text-sec)', borderColor: muted ? eHsl('agent', 0.3) : 'var(--border)',
          }}>{muted ? '🔇 Mutato' : '🔊 Muta'}</button>
          {!isHost && (
            <button type="button" style={{
              ...pillBtn, marginLeft: 'auto', background: eHsl('event', 0.08),
              color: eHsl('event'), borderColor: eHsl('event', 0.3),
            }}>Espelli</button>
          )}
        </div>
      </div>
    );
  };

  const PlayersTab = ({ state = 'default' }) => {
    if (state === 'loading') return (
      <Body><Pad>{[0, 1, 2, 3].map(i => <Shimmer key={i} h={84} r="var(--r-md)" />)}</Pad></Body>
    );
    if (state === 'error') return (
      <Body><Centered><StateBlock tone="error" icon="⚠" title="Roster non disponibile"
        body="Impossibile sincronizzare i giocatori." action={<button type="button" style={ctaDanger}>↻ Riprova</button>} /></Centered></Body>
    );
    if (state === 'empty') return (
      <Body><Centered><StateBlock icon="👥" title="Solo tu in tavola"
        body="Invita altri giocatori per gestirli da qui."
        action={<button type="button" style={ctaTint('player')}>+ Invita giocatori</button>} /></Centered></Body>
    );
    const sse = state === 'sse';
    return (
      <Body>
        {sse && <SseBanner where="presenze" />}
        <Pad>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', flex: 1 }}>Giocatori · 4</div>
            <button type="button" style={{
              padding: '3px 9px', borderRadius: 'var(--r-pill)', background: eHsl('player', 0.1), color: eHsl('player'),
              border: `1px solid ${eHsl('player', 0.3)}`, fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800, cursor: 'pointer',
              textTransform: 'uppercase', letterSpacing: '.04em',
            }}>+ Invita</button>
          </div>
          {M.ROSTER.map(p => <PlayerRow key={p.id} p={p} conn={CONN[p.id]} sse={sse} />)}
        </Pad>
      </Body>
    );
  };

  // shared button styles
  const ctaDanger = {
    padding: '8px 14px', borderRadius: 'var(--r-md)', background: eHsl('event'), color: '#fff', border: 'none',
    fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, cursor: 'pointer',
  };
  function ctaTint(e) {
    return {
      padding: '8px 14px', borderRadius: 'var(--r-md)', background: eHsl(e, 0.12), color: eHsl(e),
      border: `1px solid ${eHsl(e, 0.35)}`, fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, cursor: 'pointer',
    };
  }
  const miniBtn = {
    width: 26, height: 26, background: 'var(--bg-muted)', border: 'none', color: 'var(--text)',
    fontSize: 14, fontWeight: 800, cursor: 'pointer',
  };
  const pillBtn = {
    padding: '5px 10px', borderRadius: 'var(--r-md)', border: '1px solid var(--border)', background: 'var(--bg-muted)',
    color: 'var(--text-sec)', fontFamily: 'var(--f-display)', fontSize: 10.5, fontWeight: 700, cursor: 'pointer', whiteSpace: 'nowrap',
  };

  window.LiveTabs = { ScoresTab, PhotosTab, AgentTab, PlayersTab };
})();
