/* sp4-session-play-parts.jsx — panels (Scoreboard, QuickActions, ActivityFeed, Chat),
   modals (Score, Dispute), Rules sheet, Toast. Loads after sp4-session-play.jsx.
   Route: /sessions/[id]/play. Exports components to window for -ui.jsx. */

const { useState, useEffect, useMemo, useRef, useCallback } = React;
const SP = window.__SP;
const {
  eHsl, sem, PAD, fmtDur, entE, GAME, SESSION, PLAYERS, SCORE_CATEGORIES, QUICK_ACTIONS,
  EVENT_TYPE, FEED_FILTERS, findPlayer, QUICK_PROMPTS, RULES_TOC,
} = SP;

const Avatar = ({ p, size = 16, font = 7, cls = 'av' }) => (
  <span className={cls} style={{ width: size, height: size, fontSize: font, background: `hsl(${p.color},58%,52%)` }} aria-hidden="true">{p.initials}</span>
);
const PlayerChip = ({ p }) => (
  <span className="sp-chip"><Avatar p={p} size={20} font={8} /><span className="nm">{p.name}</span></span>
);

// ═══════════════════════════════════════════════════════
// ─── HEADER (sticky, span 3-col) ────────────────────────
// ═══════════════════════════════════════════════════════
const SessionHeader = ({ status, durationSecs, turnPlayer, round, onPause, onResume, onFinish, onRules, mobile }) => {
  const statusMap = {
    live:   { cls: 'live',   dot: 'live', label: 'Attiva' },
    paused: { cls: 'paused', dot: '',     label: 'In pausa' },
    done:   { cls: 'done',   dot: '',     label: 'Conclusa' },
  };
  const s = statusMap[status] || statusMap.live;
  return (
    <header className="sp-head">
      <div className="sp-bread">
        <span>Sessioni</span><span className="sep" aria-hidden="true">›</span>
        <span className="gchip"><span aria-hidden="true">{GAME.emoji}</span>{GAME.title}</span>
        <span className="sep" aria-hidden="true">›</span><span className="cur">Live play</span>
      </div>
      <div className="sp-htop">
        <div className="sp-htxt">
          <div className="sp-titlerow">
            <h1 className="sp-h1">{GAME.title}</h1>
            <span className={'sp-statusbadge ' + s.cls}>
              <span className={'sp-statusdot ' + s.dot} aria-hidden="true"></span>{s.label}
            </span>
          </div>
          {status !== 'done' && turnPlayer && (
            <div className="sp-turn">
              <span className="lbl">Turno di</span><PlayerChip p={turnPlayer} />
            </div>
          )}
          <div className="sp-meta">
            <span className="mi"><span className="g" aria-hidden="true">⏱</span><b>{fmtDur(durationSecs)}</b> live</span>
            <span className="mi"><span className="g" aria-hidden="true">👤</span><b>{PLAYERS.length}</b> giocatori</span>
            <span className="mi"><span className="g" aria-hidden="true">🎯</span>Turno <b>{round}</b> di ~15</span>
            <span className="mi"><span className="g" aria-hidden="true">#</span>{SESSION.code}</span>
          </div>
        </div>
        <div className="sp-hcta">
          {status === 'paused'
            ? <button type="button" className="sp-btn warn" onClick={onResume}><span aria-hidden="true">▶</span>Riprendi</button>
            : <button type="button" className="sp-btn warn" onClick={onPause}><span aria-hidden="true">⏸</span>{!mobile && 'Pausa'}</button>}
          <button type="button" className="sp-btn primary" onClick={onFinish}><span aria-hidden="true">🏁</span>{mobile ? 'Concludi' : 'Conclude partita'}</button>
          {!mobile && <button type="button" className="sp-iconbtn" onClick={onRules} aria-label="Apri menu (regole, impostazioni, salva ed esci)" title="Regole · impostazioni · salva ed esci">⋯</button>}
        </div>
      </div>
    </header>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SESSION BANNER (paused / dispute / finalize) ───────
// ═══════════════════════════════════════════════════════
const SessionBanner = ({ kind, onPrimary, onSecondary }) => {
  const map = {
    paused:   { icon: '⏸', role: 'status', title: 'Partita in pausa da Marco R. (15:42)', desc: 'I timer sono fermi. Riprendi quando il tavolo è pronto.', primary: ['▶ Riprendi', 'warn'], secondary: null },
    dispute:  { icon: '⚖️', role: 'alert', title: 'Dispute aperta — Aaron R.', desc: '«La strada conta come 2 segmenti?» · in attesa di risoluzione', primary: ['Risolvi ora', 'warn'], secondary: ['Vedi nel feed', 'ghost'] },
    finalize: { icon: '🏁', role: 'status', title: 'Tutti i giocatori hanno completato il turno', desc: 'Vuoi concludere la partita e calcolare il punteggio finale?', primary: ['Conclude partita', 'success'], secondary: ['Continua', 'ghost'] },
  };
  const b = map[kind];
  if (!b) return null;
  return (
    <div className={'sp-banner ' + kind} role={b.role} aria-live={b.role === 'alert' ? 'assertive' : 'polite'}>
      <span className="bi" aria-hidden="true">{b.icon}</span>
      <div className="btxt">
        <div className="bt">{b.title}</div>
        <div className="bd">{b.desc}</div>
      </div>
      <div className="bcta">
        {b.secondary && <button type="button" className={'sp-bbtn ' + b.secondary[1]} onClick={onSecondary}>{b.secondary[0]}</button>}
        <button type="button" className={'sp-bbtn ' + b.primary[1]} onClick={onPrimary}>{b.primary[0]}</button>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SCOREBOARD ─────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const ScoreRow = ({ p, rank, expanded, onToggle, onInc, popping }) => {
  const dCls = p.delta > 0 ? 'up' : p.delta < 0 ? 'down' : 'flat';
  const dArrow = p.delta > 0 ? '▲' : p.delta < 0 ? '▼' : '─';
  const dTxt = p.delta === 0 ? '0' : `${p.delta > 0 ? '+' : ''}${p.delta}`;
  return (
    <React.Fragment>
      <li className={'sp-prow' + (p.active ? ' active' : '')} aria-current={p.active ? 'true' : undefined}
        onClick={onToggle} tabIndex={0}
        onKeyDown={e => { if (e.key === 'Enter') onToggle(); }}>
        <span className="rank">#{rank}</span>
        <Avatar p={p} size={34} font={12} cls="sp-pav" />
        <div className="sp-pmain">
          <div className="sp-pname">{p.name}{p.active && <span className="sp-pturn">🎯 Sta giocando</span>}</div>
          <div className={'sp-pdelta ' + dCls}><span aria-hidden="true">{dArrow}</span>{dTxt} dall'ultimo aggiornamento</div>
        </div>
        <div className="sp-pscore">
          <div className={'sp-score' + (popping ? ' pop' : '')}>{p.score}<span className="vp">PV</span></div>
          <div className="sp-pinc" onClick={e => e.stopPropagation()}>
            <button type="button" className="sp-incbtn" onClick={() => onInc(-1)} aria-label={'Decrementa punteggio ' + p.name}>−</button>
            <button type="button" className="sp-incbtn" onClick={() => onInc(1)} aria-label={'Incrementa punteggio ' + p.name}>+</button>
          </div>
        </div>
      </li>
      {expanded && (
        <div className="sp-breakdown">
          {p.breakdown.map(([label, val]) => (
            <div className="sp-brow" key={label}><span className="bl">{label}</span><span className="bv">{val} PV</span></div>
          ))}
        </div>
      )}
    </React.Fragment>
  );
};

const Scoreboard = ({ players, onInc, popId }) => {
  const [expanded, setExpanded] = useState(null);
  const ranked = [...players].sort((a, b) => b.score - a.score);
  return (
    <section className="sp-board" role="region" aria-label="Classifica live">
      <div className="sp-phead" style={{ '--e': entE('session') }}>
        <span className="pi" aria-hidden="true">🏆</span>
        <div>
          <div className="pt">Classifica live</div>
          <div className="pc">{players.length} giocatori · Punti Vittoria</div>
        </div>
      </div>
      <ul className="sp-board" role="list" style={{ listStyle: 'none', margin: 0, padding: 0 }}>
        {ranked.map((p, i) => (
          <ScoreRow key={p.id} p={p} rank={i + 1} expanded={expanded === p.id}
            onToggle={() => setExpanded(e => e === p.id ? null : p.id)}
            onInc={d => onInc(p.id, d)} popping={popId === p.id} />
        ))}
      </ul>
    </section>
  );
};

// ═══════════════════════════════════════════════════════
// ─── QUICK ACTIONS ──────────────────────────────────────
// ═══════════════════════════════════════════════════════
const QuickActions = ({ onAction }) => (
  <section className="sp-qa" role="region" aria-label="Azioni rapide">
    <div className="sp-qa-head">
      <span className="qi" aria-hidden="true">⚡</span>
      <span className="qt">Azioni rapide</span>
    </div>
    <div className="sp-qa-grid">
      {QUICK_ACTIONS.map(a => (
        <button key={a.id} type="button" className="sp-qabtn" style={{ '--e': entE(a.ent) }} onClick={() => onAction(a.id)}>
          <span className="qg" aria-hidden="true">{a.icon}</span>
          <span className="ql">{a.label}</span>
        </button>
      ))}
    </div>
  </section>
);

// ═══════════════════════════════════════════════════════
// ─── ACTIVITY FEED ──────────────────────────────────────
// ═══════════════════════════════════════════════════════
const ActivityEvent = ({ ev, onResolve }) => {
  const t = EVENT_TYPE[ev.type] || EVENT_TYPE.milestone;
  const actor = ev.actor ? findPlayer(ev.actor) : null;
  return (
    <div className={'sp-event' + (ev.fresh ? ' fresh' : '') + (ev.type === 'dispute' ? ' flag' : '')} style={{ '--e': entE(t.ent) }}>
      <span className="eic" aria-hidden="true">{t.icon}</span>
      <div className="ebody">
        <div className="etop">
          <span className="etime">{ev.time}</span>
          <span className="etext">{ev.text}</span>
        </div>
        {ev.detail && <div className="edetail">{ev.detail}</div>}
        <div className="sp-emeta">
          {actor && <span className="sp-echip"><Avatar p={actor} /><span className="nm">{actor.name}</span></span>}
          {ev.resolvable
            ? <span className="sp-eactions"><button type="button" className="sp-elink">Vedi</button><button type="button" className="sp-elink danger" onClick={onResolve}>Risolvi</button></span>
            : <span className="sp-eactions"><button type="button" className="sp-elink">Rispondi</button><button type="button" className="sp-elink">Nota</button></span>}
        </div>
      </div>
    </div>
  );
};

const ActivityFeed = ({ feed, sseOff, mobile, collapsed, onToggleCollapse, onResolveDispute }) => {
  const [filters, setFilters] = useState(['all']);
  const toggle = id => {
    setFilters(prev => {
      if (id === 'all') return ['all'];
      const next = prev.includes('all') ? [id] : prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id];
      return next.length === 0 ? ['all'] : next;
    });
  };
  const shown = filters.includes('all') ? feed : feed.filter(e => filters.includes(e.type));
  return (
    <section className={'sp-feed' + (sseOff ? ' disc' : '') + (collapsed ? ' collapsed' : '')} role="region" aria-label="Activity feed">
      <div className="sp-phead sp-feedhead" style={{ '--e': entE('session') }}>
        <span className="pi" aria-hidden="true">📡</span>
        <div className="grow">
          <div className="pt">Attività live</div>
          <div className="pc">{shown.length} eventi · feed cronologico</div>
        </div>
        <span className={'sp-ssetag' + (sseOff ? ' off' : '')} aria-label={sseOff ? 'Connessione persa, riconnessione in corso' : 'Connesso live'}>
          <span className={'sp-ssedot' + (sseOff ? ' off' : ' live')} aria-hidden="true"></span>{sseOff ? 'Riconnessione…' : 'Connesso live'}
        </span>
        {mobile && <button type="button" className="sp-collapse-btn" onClick={onToggleCollapse} aria-label={collapsed ? 'Espandi feed' : 'Comprimi feed'} aria-expanded={!collapsed}>▾</button>}
      </div>
      <div className="sp-feedfilters" role="group" aria-label="Filtra eventi">
        {FEED_FILTERS.map(f => (
          <button key={f.id} type="button" className={'sp-fchip' + (filters.includes(f.id) ? ' on' : '')}
            aria-pressed={filters.includes(f.id)} onClick={() => toggle(f.id)}>
            <span aria-hidden="true">{f.icon}</span>{f.label}
          </button>
        ))}
      </div>
      <div className="sp-feedbody" role="log" aria-live="polite" aria-relevant="additions" aria-busy={sseOff}>
        {shown.map(ev => <ActivityEvent key={ev.id} ev={ev} onResolve={onResolveDispute} />)}
        <button type="button" className="sp-feedmore">↑ Carica eventi precedenti</button>
      </div>
    </section>
  );
};

// ═══════════════════════════════════════════════════════
// ─── CHAT WIDGET ────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const ChatMessage = ({ m, onCite }) => {
  if (m.role === 'user') {
    const p = findPlayer(m.actor);
    return (
      <div className={'sp-msg user' + (m.fresh ? ' fresh' : '')}>
        <span className="sp-msgwho">{p && <Avatar p={p} size={15} font={6} />}{p ? p.name : 'Tu'}</span>
        <div className="sp-bubble">{m.text}</div>
      </div>
    );
  }
  return (
    <div className={'sp-msg agent' + (m.fresh ? ' fresh' : '')}>
      <span className="sp-msgwho"><span aria-hidden="true">🤖</span>Catan Coach</span>
      <div className="sp-bubble">
        {m.text}{m.streaming && <span className="sp-cursor" aria-hidden="true"></span>}
        {m.cite && !m.streaming && <div><button type="button" className="sp-cite" onClick={() => onCite(m.cite)}><span aria-hidden="true">📜</span>Vedi {m.cite}</button></div>}
      </div>
    </div>
  );
};

const ChatWidget = ({ chat, streaming, onCite, onExpand, expanded, mobile, onCloseSheet }) => {
  const bodyRef = useRef(null);
  useEffect(() => { if (bodyRef.current) bodyRef.current.scrollTop = bodyRef.current.scrollHeight; }, [chat, streaming]);
  const [draft, setDraft] = useState('');
  return (
    <section className="sp-chat" role="region" aria-label="Chat con agent">
      <div className="sp-chathead">
        <span className="ca" aria-hidden="true">🤖</span>
        <div className="cmeta">
          <div className="cname">Catan Coach</div>
          <div className="crole">Agent · esperto regole</div>
        </div>
        {mobile
          ? <button type="button" className="sp-iconbtn" onClick={onCloseSheet} aria-label="Chiudi chat">✕</button>
          : expanded
            ? <button type="button" className="sp-iconbtn" onClick={onExpand} aria-label="Riduci chat" title="Riduci chat">⤡</button>
            : <button type="button" className="sp-iconbtn" onClick={onExpand} aria-label="Espandi chat a schermo intero" title="Espandi chat">⛶</button>}
      </div>
      <div className="sp-chatbody" ref={bodyRef} role="log" aria-live="polite" aria-busy={streaming}>
        {chat.map(m => <ChatMessage key={m.id} m={m} onCite={onCite} />)}
        {streaming && (
          <div className="sp-msg agent fresh">
            <span className="sp-msgwho"><span aria-hidden="true">🤖</span>Catan Coach</span>
            <div className="sp-bubble" style={{ padding: 0 }}><div className="sp-typing" aria-label="L'agent sta scrivendo"><span></span><span></span><span></span></div></div>
          </div>
        )}
      </div>
      <div className="sp-chatfoot">
        <div className="sp-prompts">
          {QUICK_PROMPTS.map(q => <button key={q} type="button" className="sp-prompt" onClick={() => setDraft(q)}>{q}</button>)}
        </div>
        <div className="sp-inputrow">
          <textarea className="sp-chatinput" rows={1} placeholder="Scrivi un messaggio per l'agent…"
            aria-label="Messaggio per agent" value={draft} onChange={e => setDraft(e.target.value)} />
          <button type="button" className="sp-sendbtn" disabled={!draft.trim()} aria-label="Invia messaggio">➤</button>
        </div>
        {!mobile && <div className="sp-kbdhint"><kbd>Ctrl</kbd>+<kbd>Enter</kbd> per inviare</div>}
      </div>
    </section>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SCORE INPUT MODAL ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const useModalDismiss = (onClose, mobile) => {
  const ref = useRef(null);
  useEffect(() => { ref.current && ref.current.focus(); }, []);
  useEffect(() => {
    if (mobile) return; // mobile: no Esc key — usa ✕ / tap backdrop
    const onKey = e => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose, mobile]);
  return ref;
};

const ScoreModal = ({ onClose, mobile }) => {
  const [player, setPlayer] = useState('p-sara');
  const [cat, setCat] = useState('longest-road');
  const [pts, setPts] = useState(2);
  const closeRef = useModalDismiss(onClose, mobile);
  return (
    <div className="sp-overlay" onMouseDown={e => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="sp-modal" role="dialog" aria-modal="true" aria-labelledby="sp-score-t" style={{ '--e': entE('toolkit') }}>
        <div className="sp-mhead">
          <span className="mi" aria-hidden="true">📋</span>
          <div className="mt" id="sp-score-t">Aggiungi punteggio</div>
          <button type="button" className="mx" ref={closeRef} onClick={onClose} aria-label="Chiudi">✕</button>
        </div>
        <div className="sp-mbody">
          <div className="sp-field">
            <span className="fl">Giocatore</span>
            <div className="sp-pselect">
              {PLAYERS.map(p => (
                <button key={p.id} type="button" className={'sp-popt' + (player === p.id ? ' on' : '')} onClick={() => setPlayer(p.id)}>
                  <Avatar p={p} size={22} font={9} /><span className="nm">{p.name}</span>
                </button>
              ))}
            </div>
          </div>
          <div className="sp-field">
            <span className="fl">Categoria di punteggio</span>
            <div className="sp-catgrid">
              {SCORE_CATEGORIES.map(c => (
                <button key={c.id} type="button" className={'sp-catopt' + (cat === c.id ? ' on' : '')} onClick={() => setCat(c.id)}>
                  <span className="cl">{c.label}</span><span className="cp">+{c.pts} PV</span>
                </button>
              ))}
            </div>
          </div>
          <div className="sp-field">
            <span className="fl">Punti</span>
            <div className="sp-stepperrow">
              <button type="button" className="sp-stepbtn" onClick={() => setPts(v => Math.max(0, v - 1))} aria-label="Diminuisci punti">−</button>
              <span className="sp-stepval">{pts}<span className="pl"> PV</span></span>
              <button type="button" className="sp-stepbtn" onClick={() => setPts(v => v + 1)} aria-label="Aumenta punti">+</button>
            </div>
          </div>
          <div className="sp-field">
            <span className="fl">Nota (opzionale)</span>
            <textarea className="sp-ta" rows={2} placeholder="Es. strada completata con il porto…" aria-label="Nota opzionale"></textarea>
          </div>
        </div>
        <div className="sp-mfoot">
          <button type="button" className="sp-mbtn" onClick={onClose}>Annulla</button>
          <button type="button" className="sp-mbtn primary" onClick={onClose}><span aria-hidden="true">＋</span>Aggiungi punto</button>
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── DISPUTE MODAL ──────────────────────────────────────
// ═══════════════════════════════════════════════════════
const DisputeModal = ({ onClose, mobile }) => {
  const closeRef = useModalDismiss(onClose, mobile);
  return (
    <div className="sp-overlay" onMouseDown={e => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="sp-modal" role="dialog" aria-modal="true" aria-labelledby="sp-disp-t" style={{ '--e': entE('danger') }}>
        <div className="sp-mhead">
          <span className="mi" aria-hidden="true">⚖️</span>
          <div className="mt" id="sp-disp-t">Dispute — Aaron R.</div>
          <button type="button" className="mx" ref={closeRef} onClick={onClose} aria-label="Chiudi">✕</button>
        </div>
        <div className="sp-mbody">
          <div className="sp-field">
            <span className="fl">Descrizione</span>
            <p style={{ margin: 0, fontSize: 13.5, color: 'var(--text-sec)', lineHeight: 1.55 }}>
              «La strada che ho costruito attraversa un incrocio con un insediamento avversario — conta comunque come 2 segmenti per la strada più lunga?»
            </p>
          </div>
          <div className="sp-suggest">
            <span className="si" aria-hidden="true">🤖</span>
            <div className="sb">
              <div className="sl">Suggerimento agent · Catan Coach</div>
              <div className="st">Per regola §3.4, una strada interrotta da un insediamento o città avversari <b>spezza</b> il percorso: i segmenti dopo l'interruzione non contano per la strada più lunga. Vedi p.8 §3.4.</div>
            </div>
          </div>
        </div>
        <div className="sp-mfoot">
          <button type="button" className="sp-mbtn" onClick={onClose}>Annulla dispute</button>
          <button type="button" className="sp-mbtn" onClick={onClose}>Risolvi manualmente</button>
          <button type="button" className="sp-mbtn agent" onClick={onClose}><span aria-hidden="true">🤖</span>Conferma con agent</button>
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── RULES SHEET (slide-over right) ─────────────────────
// ═══════════════════════════════════════════════════════
const RulesSheet = ({ onClose, mobile }) => {
  const [section, setSection] = useState('s3');
  useEffect(() => {
    if (mobile) return;
    const onKey = e => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose, mobile]);
  return (
    <React.Fragment>
      <div className="sp-sheet-overlay" onClick={onClose}></div>
      <aside className="sp-sheet" role="dialog" aria-modal="false" aria-labelledby="sp-rules-t">
        <div className="sp-sheethead">
          <span className="shi" aria-hidden="true">📜</span>
          <div className="sht">
            <div className="shtitle" id="sp-rules-t">Regolamento Catan</div>
            <div className="shsub">catan-regole.pdf · 18 pagine</div>
          </div>
          <button type="button" className="sp-iconbtn" onClick={onClose} aria-label="Chiudi regole">✕</button>
        </div>
        <div className="sp-sheetbody">
          <nav className="sp-toc" aria-label="Indice regolamento">
            {RULES_TOC.map(t => (
              <button key={t.id} type="button" className={'sp-tocitem' + (section === t.id ? ' on' : '')} onClick={() => setSection(t.id)} aria-current={section === t.id ? 'true' : undefined}>
                <span className="tn">{t.n}</span><span className="tl">{t.label}</span><span className="tp">{t.page}</span>
              </button>
            ))}
          </nav>
          <div className="sp-rulescontent">
            <div className="sp-pdfpage">
              <div className="pn">catan-regole.pdf · p.8</div>
              <h4>§3 — Carte sviluppo</h4>
              <p>Le carte sviluppo si acquistano spendendo 1 grano, 1 lana e 1 minerale. Vengono tenute coperte fino al momento del gioco.</p>
              <h5>§3.4 — Limiti di gioco per turno</h5>
              <p>Puoi giocare <span className="hl">una sola carta sviluppo per turno</span>, in qualsiasi momento durante il tuo turno, eccetto le carte Punto Vittoria che restano coperte fino alla fine della partita. Una carta acquistata nello stesso turno non può essere giocata.</p>
              <p>Per la <span className="hl">strada più lunga</span>: un percorso continuo di almeno 5 segmenti. Una strada interrotta da un insediamento o città avversari spezza il percorso e i segmenti successivi non contano.</p>
              <h5>§3.5 — Cavaliere</h5>
              <p>La carta Cavaliere sposta il ladro e ruba una risorsa a un avversario adiacente. 3 carte Cavaliere giocate assegnano la carta Esercito più grande (2 PV).</p>
            </div>
          </div>
        </div>
      </aside>
    </React.Fragment>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TOAST ──────────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const Toast = ({ text }) => (
  <div className="sp-toast" role="status"><span className="td" aria-hidden="true"></span>{text}</div>
);

Object.assign(window, {
  SP_Avatar: Avatar, SP_PlayerChip: PlayerChip,
  SessionHeader, SessionBanner, Scoreboard, QuickActions, ActivityFeed, ChatWidget,
  ScoreModal, DisputeModal, RulesSheet, SP_Toast: Toast,
});
