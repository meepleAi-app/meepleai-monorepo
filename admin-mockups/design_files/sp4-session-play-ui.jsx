/* sp4-session-play-ui.jsx — SessionApp orchestrator, frames, 10-state picker, App root.
   Loads after sp4-session-play.jsx + sp4-session-play-parts.jsx. Route: /sessions/[id]/play. */

const { useState, useEffect, useMemo, useRef, useCallback } = React;
const SP = window.__SP;
const { eHsl, PAD, fmtDur, BASE_FEED, BASE_CHAT, EVENT_TYPE, findPlayer, PLAYERS } = SP;

// ═══════════════════════════════════════════════════════
// ─── SESSION APP (interactive, one per state × viewport) ─
// ═══════════════════════════════════════════════════════
const SessionApp = ({ scenario, mobile }) => {
  const sc = scenario.sc || {};

  const [players, setPlayers] = useState(() => PLAYERS.map(p => ({ ...p })));
  const [popId, setPopId] = useState(null);
  const [feed, setFeed] = useState(() => BASE_FEED.map(e => ({ ...e })));
  const [status, setStatus] = useState(sc.status || 'live');
  const [banner, setBanner] = useState(sc.banner || null);
  const [modal, setModal] = useState(sc.modal || null);
  const [rules, setRules] = useState(!!sc.rules);
  const [sseOff, setSseOff] = useState(!!sc.sseOff);
  const [toast, setToast] = useState(sc.toast || null);
  const [chatSheet, setChatSheet] = useState(false);
  const [chatFull, setChatFull] = useState(false);
  const [feedCollapsed, setFeedCollapsed] = useState(false);
  const idRef = useRef(2000);

  const nowClock = () => { const d = new Date(); return `${PAD(d.getHours())}:${PAD(d.getMinutes())}`; };

  // chat thread (+ optional streaming tail for agent-streaming state)
  const chat = useMemo(() => {
    let base = BASE_CHAT.map(m => ({ ...m }));
    if (sc.streamingChat) {
      base.push({ id: 'm-stream-q', role: 'user', actor: 'p-marco', text: 'Se gioco un Cavaliere posso anche costruire una strada nello stesso turno?' });
      base.push({ id: 'm-stream-a', role: 'agent', text: 'Sì: giocare una carta Cavaliere non consuma la tua azione di costruzione. Puoi spostare il ladro, rubare una risorsa e poi', streaming: true });
    }
    return base;
  }, [scenario.id]); // eslint-disable-line

  const addEvent = useCallback((type, text, detail, actor) => {
    idRef.current += 1;
    setFeed(prev => [{ id: 'ev-' + idRef.current, type, time: nowClock(), actor: actor || null, text, detail, fresh: true }, ...prev]);
  }, []);

  const handleInc = useCallback((id, d) => {
    setPlayers(prev => prev.map(p => p.id === id ? { ...p, score: Math.max(0, p.score + d), delta: d } : p));
    setPopId(id); setTimeout(() => setPopId(null), 440);
    const p = findPlayer(id);
    addEvent('score', `${p.name} ha aggiornato il punteggio`, `${d > 0 ? '+' : ''}${d} PV manuale`, id);
  }, [addEvent]);

  const handleAction = useCallback(actionId => {
    if (actionId === 'score') setModal('score');
    else if (actionId === 'dispute') setModal('dispute');
    else if (actionId === 'rules') setRules(true);
    else if (actionId === 'dice') addEvent('dice', 'Tiro dadi 2D6 → 7', 'il ladro si attiva', null);
    else if (actionId === 'photo') addEvent('photo', 'Foto della board caricata', 'turno corrente', null);
    else if (actionId === 'save') setToast('Sessione salvata');
  }, [addEvent]);

  useEffect(() => { if (toast) { const t = setTimeout(() => setToast(null), 3200); return () => clearTimeout(t); } }, [toast]);

  // desktop keyboard shortcuts (gated to !mobile — mobile non ha Ctrl/Esc)
  useEffect(() => {
    if (mobile) return;
    const h = e => {
      const tag = document.activeElement.tagName;
      const typing = tag === 'INPUT' || tag === 'TEXTAREA';
      if (e.key === 'Escape') { setModal(null); setRules(false); setChatFull(false); }
      if (typing) return;
      if (e.key === '/') { e.preventDefault(); const i = document.querySelector('.sp-chatinput'); i && i.focus(); }
      if ((e.ctrlKey || e.metaKey) && (e.key === 'p' || e.key === 'P')) { e.preventDefault(); setStatus(s => s === 'paused' ? 'live' : 'paused'); setBanner(b => b === 'paused' ? null : 'paused'); }
      if ((e.ctrlKey || e.metaKey) && (e.key === 's' || e.key === 'S')) { e.preventDefault(); setToast('Sessione salvata'); }
    };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [mobile]);

  const turnPlayer = players.find(p => p.active);
  const dimmed = status === 'paused';

  return (
    <div className={'sp-app' + (mobile ? ' is-mobile' : '') + (dimmed ? ' dimmed' : '')}>
      <SessionHeader
        status={status} durationSecs={sc.durationSecs || 5040} turnPlayer={turnPlayer} round={sc.round || 8}
        onPause={() => { setStatus('paused'); setBanner('paused'); }}
        onResume={() => { setStatus('live'); setBanner(null); }}
        onFinish={() => setBanner('finalize')}
        onRules={() => setRules(true)} mobile={mobile} />

      {banner && (
        <SessionBanner kind={banner}
          onPrimary={() => {
            if (banner === 'paused') { setStatus('live'); setBanner(null); }
            else if (banner === 'dispute') { setModal('dispute'); }
            else if (banner === 'finalize') { setStatus('done'); setBanner(null); setToast('Partita conclusa · punteggio salvato'); }
          }}
          onSecondary={() => setBanner(null)} />
      )}

      <div className="sp-layout">
        <div className="sp-col left">
          <div className="sp-colscroll">
            <Scoreboard players={players} onInc={handleInc} popId={popId} />
            <QuickActions onAction={handleAction} />
          </div>
        </div>

        <div className="sp-col center">
          <ActivityFeed feed={feed} sseOff={sseOff} mobile={mobile}
            collapsed={mobile && feedCollapsed} onToggleCollapse={() => setFeedCollapsed(c => !c)}
            onResolveDispute={() => setModal('dispute')} />
        </div>

        <div className="sp-col right">
          <ChatWidget chat={chat} streaming={false} onCite={() => setRules(true)} onExpand={() => setChatFull(true)} mobile={false} />
        </div>
      </div>

      {/* desktop: fullscreen chat overlay (Espandi chat) */}
      {!mobile && chatFull && (
        <div className="sp-overlay" onMouseDown={e => { if (e.target === e.currentTarget) setChatFull(false); }}>
          <div className="sp-chatfull" role="dialog" aria-modal="true" aria-label="Chat agent a schermo intero">
            <ChatWidget chat={chat} streaming={false} onCite={() => { setChatFull(false); setRules(true); }} onExpand={() => setChatFull(false)} expanded={true} mobile={false} />
          </div>
        </div>
      )}

      {/* mobile: sticky chat FAB + bottom-sheet chat */}
      {mobile && !chatSheet && (
        <button type="button" className="sp-chatfab" onClick={() => setChatSheet(true)}>
          <span className="cg" aria-hidden="true">🤖</span>Apri chat agent
          <span className="cbadge">{chat.length} msg</span>
        </button>
      )}
      {mobile && chatSheet && (
        <div className="sp-chatsheet">
          <div className="sp-chatsheet-grab" aria-hidden="true"></div>
          <ChatWidget chat={chat} streaming={false} onCite={() => { setChatSheet(false); setRules(true); }} mobile={true} onCloseSheet={() => setChatSheet(false)} />
        </div>
      )}

      {modal === 'score' && <ScoreModal onClose={() => setModal(null)} mobile={mobile} />}
      {modal === 'dispute' && <DisputeModal onClose={() => setModal(null)} mobile={mobile} />}
      {rules && <RulesSheet onClose={() => setRules(false)} mobile={mobile} />}
      {toast && <SP_Toast text={toast} />}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── FRAMES ─────────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopFrame = ({ height = 720, children }) => (
  <div style={{ width: '100%', borderRadius: 'var(--r-xl)', border: '1px solid var(--border)', background: 'var(--bg-card)', overflow: 'hidden', boxShadow: 'var(--shadow-lg)' }}>
    <div className="sp-desk" style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '9px 14px', background: 'var(--bg-muted)', borderBottom: '1px solid var(--border)', fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)' }}>
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: '#ff5f57' }}></span>
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: '#febc2e' }}></span>
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: '#28c840' }}></span>
      <span style={{ flex: 1, textAlign: 'center', letterSpacing: '.04em' }}>meepleai.app/sessions/sess-abc-123/play</span>
    </div>
    <div style={{ height }}>{children}</div>
  </div>
);

const PhoneFrame = ({ children }) => (
  <div className="phone" style={{ width: 375, height: 780 }}>
    <div className="phone-sbar" style={{ color: 'var(--text)' }}>
      <span style={{ fontFamily: 'var(--f-mono)' }}>16:43</span>
      <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
    </div>
    <div style={{ flex: 1, minHeight: 0, display: 'flex' }}>{children}</div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── STATE PICKER + ROOT ────────────────────────────────
// ═══════════════════════════════════════════════════════
const STATES = [
  { id: 'active-default',        label: 'Active default',     view: 'desktop', sc: { status: 'live' }, desc: 'Sessione live: scoreboard 4 player con Marco R. attivo, activity feed 8 eventi (SSE connesso), chat agent 2 round. Tutti i tool interattivi (score +/−, quick actions, filtri feed).' },
  { id: 'paused',                label: 'Paused',             view: 'desktop', sc: { status: 'paused', banner: 'paused' }, desc: 'Banner --c-warning “Partita in pausa da Marco R. (15:42)”, body dimmed (saturazione ridotta), CTA “▶ Riprendi” in header e banner.' },
  { id: 'active-with-dispute',   label: 'Dispute attiva',     view: 'desktop', sc: { status: 'live', banner: 'dispute' }, desc: 'Banner --c-danger role=alert “Dispute aperta — Aaron R.”, evento dispute evidenziato nel feed (border-left rosso) con azione “Risolvi”.' },
  { id: 'score-input-modal-open',label: 'Score modal',        view: 'desktop', sc: { status: 'live', modal: 'score' }, desc: 'Modal Score Input aperto mid-form: Sara T. selezionata, categoria “Strada più lunga”, 2 PV. Stepper punti + nota. role=dialog aria-modal, ✕ + Esc + tap-backdrop per chiudere.' },
  { id: 'dispute-modal-open',    label: 'Dispute modal',      view: 'desktop', sc: { status: 'live', modal: 'dispute' }, desc: 'Modal Dispute aperto: descrizione + suggerimento agent (regola §3.4) + 3 CTA (Annulla / Risolvi manualmente / Conferma con agent).' },
  { id: 'rules-sheet-open',      label: 'Rules sheet',        view: 'desktop', sc: { status: 'live', rules: true }, desc: 'Rules Sheet slide-over destra (aria-modal=false, non bloccante): TOC sinistra + contenuto PDF Catan p.8 §3.4 con highlight, scrollabile.' },
  { id: 'agent-streaming',       label: 'Agent streaming',    view: 'desktop', sc: { status: 'live', streamingChat: true }, desc: 'Chat widget con risposta agent in streaming: testo parziale + cursore lampeggiante, aria-busy. Auto-scroll al messaggio nuovo.' },
  { id: 'sse-disconnected',      label: 'SSE disconnesso',    view: 'desktop', sc: { status: 'live', sseOff: true, toast: 'Riconnesso · feed aggiornato' }, desc: 'Indicator activity feed in rosso “Riconnessione…” con flash --c-danger, aria-busy. Toast “Riconnesso” quando la connessione torna.' },
  { id: 'finalize-prompt',       label: 'Finalize prompt',    view: 'desktop', sc: { status: 'live', banner: 'finalize' }, desc: 'Banner --c-success “Tutti i giocatori hanno completato il turno”: CTA “Conclude partita” / “Continua”. Conclude → status “Conclusa” + toast.' },
  { id: 'mobile-stack',          label: 'Mobile · stack',     view: 'mobile',  sc: { status: 'live' }, desc: 'Viewport 375px: header → scoreboard fullwidth → quick actions 2-col → activity feed collassabile → chat come bottom-sheet via CTA sticky “Apri chat agent”. Nessuna shortcut Ctrl/Esc: tutto touch (✕ / tap-backdrop / Invia).' },
];
const SKEY = 'sp-state';

const VpLabel = ({ children }) => (
  <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)', textTransform: 'uppercase', letterSpacing: '.08em', fontWeight: 700 }}>{children}</div>
);

const App = () => {
  const [theme, setTheme] = useState(() => localStorage.getItem('mai-theme') || document.documentElement.getAttribute('data-theme') || 'light');
  const [active, setActive] = useState(() => {
    const s = localStorage.getItem(SKEY);
    return STATES.some(x => x.id === s) ? s : 'active-default';
  });
  useEffect(() => { document.documentElement.setAttribute('data-theme', theme); localStorage.setItem('mai-theme', theme); }, [theme]);
  useEffect(() => { localStorage.setItem(SKEY, active); }, [active]);

  const cur = STATES.find(s => s.id === active) || STATES[0];

  return (
    <div style={{ minHeight: '100vh', background: 'var(--bg)', color: 'var(--text)', padding: '20px 20px 80px' }}>
      <style dangerouslySetInnerHTML={{ __html: window.__SP_CSS }} />

      {/* state picker bar (continuity #1489 + #1490 + #1491) */}
      <header style={{
        position: 'sticky', top: 12, zIndex: 50, maxWidth: 1340, margin: '0 auto 24px',
        background: 'var(--glass-bg)', backdropFilter: 'blur(16px)', border: '1px solid var(--border)',
        borderRadius: 'var(--r-xl)', boxShadow: 'var(--shadow-md)', padding: '12px 16px',
        display: 'flex', alignItems: 'center', gap: 14, flexWrap: 'wrap',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <div style={{ width: 30, height: 30, borderRadius: 8, flexShrink: 0, background: `linear-gradient(135deg, ${eHsl('session')}, ${eHsl('player')})`, color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 800, fontFamily: 'var(--f-display)', fontSize: 14 }}>S</div>
          <div>
            <div style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 14, lineHeight: 1.1 }}>Session play</div>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)' }}>#1492 · 1/1 · /sessions/[id]/play</div>
          </div>
        </div>

        <div role="tablist" aria-label="Stati schermata" style={{ display: 'flex', gap: 6, flexWrap: 'wrap', flex: 1, minWidth: 0 }}>
          {STATES.map(s => {
            const on = s.id === active;
            return (
              <button key={s.id} type="button" role="tab" aria-selected={on} onClick={() => setActive(s.id)} style={{
                padding: '7px 12px', borderRadius: 'var(--r-pill)', cursor: 'pointer',
                background: on ? eHsl('session') : 'var(--bg-muted)', border: on ? 'none' : '1px solid var(--border)',
                color: on ? '#fff' : 'var(--text-sec)', fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, whiteSpace: 'nowrap',
                boxShadow: on ? `0 3px 10px ${eHsl('session', 0.35)}` : 'none',
              }}>{s.label}</button>
            );
          })}
        </div>

        <button type="button" onClick={() => setTheme(t => t === 'light' ? 'dark' : 'light')} style={{
          padding: '8px 14px', borderRadius: 'var(--r-md)', flexShrink: 0, background: 'var(--bg-card)', border: '1px solid var(--border)',
          color: 'var(--text)', fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer',
        }}>🌗 {theme === 'light' ? 'Light' : 'Dark'}</button>
      </header>

      {/* active state description */}
      <div style={{ maxWidth: 1340, margin: '0 auto 18px', padding: '0 4px', fontFamily: 'var(--f-mono)', fontSize: 12, color: 'var(--text-muted)', lineHeight: 1.5 }}>
        <strong style={{ color: eHsl('session') }}>{cur.label}</strong> — {cur.desc}
      </div>

      {/* render area */}
      <div style={{ maxWidth: 1340, margin: '0 auto', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 36 }}>
        {cur.view === 'desktop' && (
          <div style={{ width: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <VpLabel>Desktop · 1440 — header + 3-col (Scoreboard 30 / Activity 40 / Chat 30)</VpLabel>
            <DesktopFrame><SessionApp key={'d-' + cur.id} scenario={cur} mobile={false} /></DesktopFrame>
          </div>
        )}
        {cur.view === 'mobile' && (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <VpLabel>Mobile · 375 — stack 1-col + chat bottom-sheet</VpLabel>
            <PhoneFrame><SessionApp key={'m-' + cur.id} scenario={cur} mobile={true} /></PhoneFrame>
          </div>
        )}
      </div>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
