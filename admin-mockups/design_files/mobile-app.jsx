/* Mobile app — interactive screens with drawer, navigation between entities,
   session mode, and multiple screen types. */

const { useState, useEffect, useRef, useMemo } = React;

// ─── UTILITIES ───────────────────────────────────────
const entStyle = (type) => {
  const ec = DS.EC[type];
  return ec ? `${ec.h} ${ec.s}% ${ec.l}%` : '25 95% 45%';
};
const cov = (item) => ({ background: item.cover });

// ─── PHONE SHELL ─────────────────────────────────────
function Phone({ label, desc, children, banner }) {
  return (
    <div className="phone-col">
      <div className="phone-label">{label}</div>
      <div className="phone">
        <div className="phone-sbar">
          <span>14:32</span>
          <div className="ind"><span>📶</span><span>🔋</span></div>
        </div>
        {children}
      </div>
      {desc && <div className="phone-desc">{desc}</div>}
    </div>
  );
}

// ─── COMMON HEADER (topbar + mininav) ────────────────
function TopBar({ onHome }) {
  return (
    <div className="topbar">
      <div className="logo" onClick={onHome} style={{cursor:'pointer'}}>
        <div className="logo-mark">M</div>
        <span>MeepleAI</span>
      </div>
      <div className="sp"></div>
      <button className="tb-btn">🔍</button>
      <button className="tb-btn">🔔</button>
      <div className="avatar">MR</div>
    </div>
  );
}

function MiniNav({ entity, tabs, active, onTab, recents, onOpenRecent }) {
  return (
    <div className="mininav" style={{'--e': entStyle(entity.type)}}>
      <span className="bc">› {DS.EC[entity.type].lb} / {entity.title}</span>
      {tabs.map((t, i) => (
        <span key={t.id} className={`tab ${i === active ? 'active' : ''}`} onClick={() => onTab(i)}>
          {t.lb}{t.bg ? <span className="cnt">{t.bg}</span> : null}
        </span>
      ))}
      <span className="sp"></span>
      {recents?.map(r => (
        <div key={r.id} className="rec" onClick={() => onOpenRecent(r)}
          style={{background:`hsl(${DS.EC[r.type].h} ${DS.EC[r.type].s}% ${DS.EC[r.type].l}% / .12)`,
                  color:DS.color(r.type)}}>
          {DS.EC[r.type].em}
        </div>
      ))}
    </div>
  );
}

function BottomBar({ screen, onNav, sessionMode, onClose }) {
  if (sessionMode) {
    return (
      <div className="bbar session">
        <button className="bb-item" onClick={onClose}><span className="ic">◀</span><span className="lb">Back</span></button>
        <button className="bb-item active session-active"><span className="ic">📊</span><span className="lb">Classifica</span></button>
        <button className="bb-item"><span className="ic">🧰</span><span className="lb">Toolkit</span></button>
        <button className="bb-item"><span className="ic">💬</span><span className="lb">AI</span></button>
        <button className="bb-item"><span className="ic">⋯</span><span className="lb">Altro</span></button>
      </div>
    );
  }
  const items = [
    { k: 'home', ic: '🏠', lb: 'Home' },
    { k: 'search', ic: '🔍', lb: 'Cerca' },
    { k: 'library', ic: '📚', lb: 'Libreria' },
    { k: 'chats', ic: '💬', lb: 'Chat' },
    { k: 'profile', ic: '👤', lb: 'Profilo' },
  ];
  return (
    <div className="bbar">
      {items.map(it => (
        <button key={it.k} className={`bb-item ${screen === it.k ? 'active' : ''}`} onClick={() => onNav(it.k)}>
          <span className="ic">{it.ic}</span><span className="lb">{it.lb}</span>
        </button>
      ))}
    </div>
  );
}

// ─── ENTITY DETAIL (hero + chips + connections + actions) ───
function EntityDetail({ entity, onOpenEntity }) {
  const ec = DS.EC[entity.type];
  const conns = getConnections(entity);

  return (
    <>
      <div className="hero">
        <div className="cov-bg" style={cov(entity)}></div>
        <div className="cov-em">{entity.coverEmoji || ec.em}</div>
        <div className="cov-grad"></div>
        <div className="hr-bdg">{entity.badge || ec.lb}</div>
        {entity.rating && <div className="hr-rat">⭐ {entity.rating}</div>}
      </div>
      <div className="einfo">
        <div className="ti">{entity.title}</div>
        <div className="sub">{entity.subtitle || `${entity.publisher || ''}${entity.year ? ' · '+entity.year : ''}`}</div>
        <div className="chips">
          {getMetaChips(entity).map((m, i) => (
            <span key={i} className="meta-chip"><span>{m.i}</span>{m.v}</span>
          ))}
        </div>
      </div>
      <div className="conn-bar">
        {conns.map(c => (
          <button key={c.type} className="conn-pip"
            onClick={() => c.items[0] && onOpenEntity(c.items[0])}
            style={{
              background: `hsl(${entStyle(c.type)} / .12)`,
              color: DS.color(c.type),
            }}>
            <span>{DS.EC[c.type].em}</span>
            <span className="cp-cnt" style={{background: DS.color(c.type)}}><span>{c.count}</span></span>
            {DS.EC[c.type].lb}
          </button>
        ))}
      </div>
      <div className="qact">
        {getActions(entity).map((a, i) => (
          <button key={i} className={`qa-btn ${i === 0 ? 'primary' : ''}`}
            style={i === 0 ? { background: DS.color(entity.type) } : { color: DS.color(entity.type), borderColor: DS.color(entity.type, .3) }}>
            {a}
          </button>
        ))}
      </div>
      <div style={{padding:'12px 14px 20px', color:'var(--text-sec)', fontSize:12, lineHeight:1.6}}>
        <h4 style={{fontFamily:'var(--f-display)', fontSize:13, color:'var(--text)', marginBottom:6, textTransform:'uppercase', letterSpacing:'.04em'}}>Descrizione</h4>
        {getDescription(entity)}
      </div>
    </>
  );
}

// ─── HELPERS (entity-specific) ───────────────────────
function getConnections(e) {
  const out = [];
  const push = (type, items) => { if (items.length) out.push({ type, count: items.length, items }); };
  if (e.type === 'game') {
    push('agent', DS.agents.filter(a => a.gameId === e.id));
    push('kb', DS.kbs.filter(k => k.gameId === e.id));
    push('chat', DS.chats.filter(c => c.gameId === e.id));
    push('session', DS.sessions.filter(s => s.gameId === e.id));
    push('toolkit', DS.toolkits.filter(t => t.gameId === e.id));
  } else if (e.type === 'player') {
    push('session', DS.sessions.filter(s => (s.playerIds||[]).includes(e.id)));
    push('event', DS.events.filter(ev => (ev.participantIds||[]).includes(e.id)));
  } else if (e.type === 'session') {
    if (e.gameId) push('game', [DS.byId[e.gameId]].filter(Boolean));
    push('player', (e.playerIds||[]).map(id => DS.byId[id]).filter(Boolean));
    push('tool', DS.tools.slice(0, e.toolCount||0));
  } else if (e.type === 'agent') {
    if (e.gameId) push('game', [DS.byId[e.gameId]].filter(Boolean));
    push('kb', DS.kbs.filter(k => k.gameId === e.gameId));
    push('chat', DS.chats.filter(c => c.agentId === e.id));
  } else if (e.type === 'kb') {
    if (e.gameId) push('game', [DS.byId[e.gameId]].filter(Boolean));
    push('agent', DS.agents.filter(a => a.gameId === e.gameId));
  } else if (e.type === 'chat') {
    if (e.agentId) push('agent', [DS.byId[e.agentId]].filter(Boolean));
    if (e.gameId) push('game', [DS.byId[e.gameId]].filter(Boolean));
  } else if (e.type === 'event') {
    push('player', (e.participantIds||[]).map(id => DS.byId[id]).filter(Boolean));
    push('game', (e.gameIds||[]).map(id => DS.byId[id]).filter(Boolean));
  } else if (e.type === 'toolkit') {
    if (e.gameId) push('game', [DS.byId[e.gameId]].filter(Boolean));
    push('tool', DS.tools.filter(t => t.toolkitId === e.id));
  } else if (e.type === 'tool') {
    if (e.toolkitId) push('toolkit', [DS.byId[e.toolkitId]].filter(Boolean));
  }
  return out;
}

function getMetaChips(e) {
  switch (e.type) {
    case 'game': return [
      { i:'👥', v:e.players }, { i:'⏱️', v:e.duration }, { i:'🧩', v:e.weight }
    ];
    case 'player': return [
      { i:'🏆', v:`${e.totalWins} vittorie` }, { i:'🎲', v:`${e.totalSessions} partite` }, { i:'📊', v:`${Math.round(e.winRate*100)}%` }
    ];
    case 'session': return [
      { i:'👥', v:`${e.playerIds?.length||0} giocatori` }, { i:'⏱️', v:e.duration }, { i:'📊', v:e.turn }
    ];
    case 'agent': return [
      { i:'💬', v:`${e.invocations} inv` }, { i:'📚', v:`${e.docs} docs` }, { i:'⚡', v:e.avgLatency }
    ];
    case 'kb': return [
      { i:'📊', v:`${e.chunks||0} chunks` }, { i:'📄', v:`${e.pages} pag` }, { i:'💾', v:e.size }
    ];
    case 'chat': return [
      { i:'💬', v:`${e.msgCount} msg` }, { i:'⏱️', v:e.lastAt }
    ];
    case 'event': return [
      { i:'👥', v:`${e.participantIds?.length||0} pers` }, { i:'🎲', v:`${e.gameIds?.length||0} giochi` }, { i:'📍', v:'Location' }
    ];
    case 'toolkit': return [
      { i:'🔧', v:`${e.toolCount} tools` }, { i:'📊', v:`${e.useCount} usi` }, { i:'v', v:e.version }
    ];
    case 'tool': return [
      { i:'📊', v:`${e.uses} usi` }, { i:'🔧', v:e.kind }
    ];
    default: return [];
  }
}

function getActions(e) {
  switch (e.type) {
    case 'game': return ['▶️ Gioca', '🤖 Chiedi AI'];
    case 'player': return ['📊 Profilo', '💬 Contatta'];
    case 'session': return e.state === 'live' ? ['▶️ Riprendi', '📊 Classifica'] : ['📊 Risultati', '🔄 Rigioca'];
    case 'agent': return ['💬 Chat', '⚙️ Config'];
    case 'kb': return ['👁️ Anteprima', '⬇️ Download'];
    case 'chat': return ['💬 Continua', '📦 Archivia'];
    case 'event': return ['✅ Conferma', '📤 Invita'];
    case 'toolkit': return ['▶️ Usa', '✏️ Modifica'];
    case 'tool': return ['▶️ Start', '⚙️ Config'];
    default: return ['Apri'];
  }
}

function getDescription(e) {
  const descs = {
    game: `${e.title} è un gioco da tavolo di ${e.author}, pubblicato da ${e.publisher} nel ${e.year}. Ideale per ${e.players} giocatori, con una durata di ${e.duration} e peso ${e.weight}.`,
    player: `${e.title} è membro da ${e.subtitle.split('·')[0].replace('Membro ','').trim()}. ${e.totalWins} vittorie su ${e.totalSessions} partite (${Math.round(e.winRate*100)}% win rate). Gioco preferito: ${e.fav}.`,
    session: `Sessione ${e.state === 'live' ? 'in corso' : e.state === 'done' ? 'conclusa' : 'in pausa'} del gioco ${DS.byId[e.gameId]?.title || 'N/A'}. Codice partita: ${e.code}.`,
    agent: `Agente AI con strategia ${e.strategy}, basato su modello ${e.model}. Ha elaborato ${e.invocations} invocazioni con latenza media di ${e.avgLatency}.`,
    kb: `Knowledge base file ${e.title}. ${e.pages} pagine, ${e.size}, stato: ${e.status}. Embedding: ${e.embedding || 'pending'}.`,
    chat: `Conversazione con ${DS.byId[e.agentId]?.title}. ${e.msgCount} messaggi scambiati, ultima attività ${e.lastAt}.`,
    event: `Evento organizzato per ${e.date} alle ${e.time}. ${e.participantIds?.length} partecipanti (${e.confirmed} confermati, ${e.pending} in attesa).`,
    toolkit: `Toolkit contenente ${e.toolCount} strumenti. Versione ${e.version}, utilizzato in ${e.useCount} sessioni.`,
    tool: `Strumento tipo ${e.kind}. Configurazione: ${JSON.stringify(e.config)}. Utilizzato ${e.uses} volte.`,
  };
  return descs[e.type] || 'Nessuna descrizione disponibile.';
}

// ─── DRAWER ──────────────────────────────────────────
function Drawer({ entity, open, onClose, onOpenEntity }) {
  const [tabIdx, setTabIdx] = useState(0);
  const [dragY, setDragY] = useState(0);
  const startY = useRef(null);

  useEffect(() => { setTabIdx(0); }, [entity?.id]);

  if (!entity) return null;
  const ec = DS.EC[entity.type];
  const tabs = getDrawerTabs(entity);
  const footer = getDrawerFooter(entity);

  const onTouchStart = (e) => { startY.current = e.touches[0].clientY; };
  const onTouchMove = (e) => {
    if (startY.current === null) return;
    const dy = e.touches[0].clientY - startY.current;
    if (dy > 0) setDragY(dy);
  };
  const onTouchEnd = () => {
    if (dragY > 100) onClose();
    setDragY(0); startY.current = null;
  };

  const panelStyle = dragY > 0
    ? { transform: `translateY(${dragY}px)`, transition: 'none' }
    : {};

  return (
    <div className={`dr-ovl ${open ? 'open' : ''}`} onClick={onClose} style={{'--e': entStyle(entity.type)}}>
      <div className="dr-panel" onClick={e => e.stopPropagation()} style={panelStyle}>
        <div className="dr-handle"
          onTouchStart={onTouchStart} onTouchMove={onTouchMove} onTouchEnd={onTouchEnd}
          onMouseDown={(e) => { startY.current = e.clientY;
            const mv = (ev) => { if (startY.current !== null) { const dy = ev.clientY - startY.current; if (dy > 0) setDragY(dy); } };
            const up = () => { if (dragY > 100) onClose(); setDragY(0); startY.current = null; window.removeEventListener('mousemove', mv); window.removeEventListener('mouseup', up); };
            window.addEventListener('mousemove', mv); window.addEventListener('mouseup', up);
          }}
        ></div>
        <div className="dr-header">
          <div className="dr-icon">{entity.coverEmoji || ec.em}</div>
          <div className="dr-ti">{entity.title}</div>
          <button className="dr-close" onClick={onClose}>✕</button>
        </div>
        <div className="dr-tabs">
          {tabs.map((t, i) => (
            <button key={t.id} className={`dr-tab ${i === tabIdx ? 'active' : ''}`} onClick={() => setTabIdx(i)}>
              <span>{t.ic}</span>{t.lb}{t.bg ? <span className="tb">{t.bg}</span> : null}
            </button>
          ))}
        </div>
        <div className="dr-content">
          {renderTabContent(entity, tabs[tabIdx]?.id, onOpenEntity)}
        </div>
        <div className="dr-footer">
          {footer.map((f, i) => (
            <button key={i} className={`af ${f.v}`}
              style={f.v === 'primary' ? { background: DS.color(entity.type) } : {}}>
              {f.lb}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

function getDrawerTabs(e) {
  switch (e.type) {
    case 'game': return [
      { id:'info', lb:'Info', ic:'ℹ️' },
      { id:'stats', lb:'Stats', ic:'📊', bg: e.totalPlays },
      { id:'history', lb:'Storico', ic:'📜' },
    ];
    case 'player': return [
      { id:'profile', lb:'Profilo', ic:'👤' },
      { id:'stats', lb:'Stats', ic:'🏆', bg: e.totalWins },
      { id:'history', lb:'Storico', ic:'📜' },
    ];
    case 'session': return [
      { id:'live', lb: e.state==='live' ? 'Live' : 'Dettagli', ic:'📋' },
      { id:'toolkit', lb:'Toolkit', ic:'🔧', bg: e.toolCount },
      { id:'timeline', lb:'Timeline', ic:'🕒' },
    ];
    case 'agent': return [
      { id:'overview', lb:'Overview', ic:'🤖' },
      { id:'history', lb:'Storico', ic:'📜', bg: e.invocations },
      { id:'config', lb:'Config', ic:'⚙️' },
    ];
    case 'kb': return [
      { id:'overview', lb:'Overview', ic:'📄' },
      { id:'preview', lb:'Anteprima', ic:'👁️' },
      { id:'citations', lb:'Citazioni', ic:'💬' },
    ];
    case 'chat': return [
      { id:'messages', lb:'Messaggi', ic:'💬', bg: e.msgCount },
      { id:'sources', lb:'Fonti', ic:'📄' },
    ];
    case 'event': return [
      { id:'overview', lb:'Overview', ic:'📅' },
      { id:'program', lb:'Programma', ic:'🎲', bg: e.gameIds?.length },
    ];
    case 'toolkit': return [
      { id:'overview', lb:'Overview', ic:'🧰' },
      { id:'tools', lb:'Tools', ic:'🔧', bg: e.toolCount },
      { id:'history', lb:'Storico', ic:'📜' },
    ];
    case 'tool': return [
      { id:'detail', lb:'Dettaglio', ic:'🔧' },
      { id:'preview', lb:'Preview', ic:'▶️' },
    ];
    default: return [{ id:'info', lb:'Info', ic:'ℹ️' }];
  }
}

function getDrawerFooter(e) {
  switch (e.type) {
    case 'game': return [
      { lb:'▶️ Gioca', v:'primary' },
      { lb:'🤖 AI', v:'secondary' },
      { lb:'↗️ Apri', v:'secondary' },
    ];
    case 'player': return [
      { lb:'📊 Confronta', v:'secondary' },
      { lb:'↗️ Apri', v:'secondary' },
    ];
    case 'session': return e.state === 'live'
      ? [{lb:'▶️ Riprendi', v:'primary'}, {lb:'📊 Stats', v:'secondary'}, {lb:'↗️ Apri', v:'secondary'}]
      : [{lb:'📊 Risultati', v:'primary'}, {lb:'🔄 Rigioca', v:'secondary'}, {lb:'↗️ Apri', v:'secondary'}];
    case 'agent': return [
      { lb:'💬 Chat', v:'primary' },
      { lb:'🔄 Riavvia', v:'secondary' },
      { lb:'↗️ Apri', v:'secondary' },
    ];
    case 'kb': return [
      { lb:'🔄 Reindex', v:'secondary' },
      { lb:'⬇️ Download', v:'secondary' },
      { lb:'↗️ Apri', v:'secondary' },
    ];
    case 'chat': return [
      { lb:'💬 Continua', v:'primary' },
      { lb:'📦 Archivia', v:'secondary' },
    ];
    case 'event': return [
      { lb:'✅ Conferma', v:'primary' },
      { lb:'📤 Invita', v:'secondary' },
    ];
    case 'toolkit': return [
      { lb:'▶️ Usa', v:'primary' },
      { lb:'✏️ Modifica', v:'secondary' },
    ];
    case 'tool': return [
      { lb:'▶️ Usa', v:'primary' },
      { lb:'⚙️ Config', v:'secondary' },
    ];
    default: return [{ lb:'↗️ Apri', v:'secondary' }];
  }
}

function renderTabContent(e, tabId, onOpen) {
  // Minimal per-entity rendering of tab bodies
  const rows = (items) => (
    <>{items.map(([l, v], i) => (
      <div key={i} className="ir"><span className="l">{l}</span><span className="v">{v}</span></div>
    ))}</>
  );

  const linkRow = (item) => (
    <div key={item.id} className="ir" style={{cursor:'pointer'}} onClick={() => onOpen(item)}>
      <span className="l">{DS.EC[item.type].em} {item.title}</span>
      <span className="v" style={{color: DS.color(item.type)}}>→</span>
    </div>
  );

  if (e.type === 'game') {
    if (tabId === 'info') return (<>
      <h4>Dettagli</h4>
      {rows([['Editore', e.publisher], ['Anno', e.year], ['Giocatori', e.players], ['Durata', e.duration], ['Complessità', e.weight], ['Status', e.badge]])}
      <h4 style={{marginTop:14}}>Designer</h4>
      {rows([['Autore', e.author]])}
    </>);
    if (tabId === 'stats') return (<>
      <h4>Statistiche partite</h4>
      <div style={{display:'grid',gridTemplateColumns:'1fr 1fr 1fr',gap:6,marginBottom:10}}>
        <div style={{padding:8,borderRadius:8,background:'var(--bg-muted)',textAlign:'center'}}>
          <div style={{fontSize:18,fontWeight:800,fontFamily:'var(--f-display)',color:'hsl(var(--c-game))'}}>{e.totalPlays}</div>
          <div style={{fontSize:9,textTransform:'uppercase',color:'var(--text-muted)',fontWeight:700}}>Partite</div></div>
        <div style={{padding:8,borderRadius:8,background:'var(--bg-muted)',textAlign:'center'}}>
          <div style={{fontSize:18,fontWeight:800,fontFamily:'var(--f-display)',color:'hsl(var(--c-toolkit))'}}>{Math.round(e.winRate*100)}%</div>
          <div style={{fontSize:9,textTransform:'uppercase',color:'var(--text-muted)',fontWeight:700}}>Win Rate</div></div>
        <div style={{padding:8,borderRadius:8,background:'var(--bg-muted)',textAlign:'center'}}>
          <div style={{fontSize:18,fontWeight:800,fontFamily:'var(--f-display)',color:'hsl(var(--c-chat))'}}>{e.avgScore}</div>
          <div style={{fontSize:9,textTransform:'uppercase',color:'var(--text-muted)',fontWeight:700}}>Avg Score</div></div>
      </div>
    </>);
    if (tabId === 'history') return (<>
      <h4>Ultime sessioni</h4>
      {DS.sessions.filter(s => s.gameId === e.id).map(s => linkRow(s))}
    </>);
  }
  if (e.type === 'session') {
    if (tabId === 'live') return (<>
      <h4>Stato</h4>
      {rows([['Stato', e.badge], ['Turno', e.turn], ['Codice', e.code], ['Durata', e.duration]])}
      <h4 style={{marginTop:14}}>Giocatori</h4>
      {(e.playerIds||[]).map(id => {
        const p = DS.byId[id]; if (!p) return null;
        return <div key={id} className="ir" style={{cursor:'pointer'}} onClick={() => onOpen(p)}>
          <span className="l">{p.title}</span>
          <span className="v" style={{color:DS.color('player')}}>→</span>
        </div>;
      })}
    </>);
    if (tabId === 'toolkit') return (<>
      <h4>Tools attivi</h4>
      {DS.tools.slice(0, e.toolCount||0).map(t => linkRow(t))}
    </>);
    if (tabId === 'timeline') return (<>
      <h4>Timeline</h4>
      {['T3 · Marco gioca tessera blu','T2 · Sara completa riga','T1 · Luca setup','T0 · Partita iniziata'].map((ev, i) => (
        <div key={i} className="ir"><span className="l">{ev.split('·')[1]}</span><span className="v">{ev.split('·')[0]}</span></div>
      ))}
    </>);
  }
  if (e.type === 'agent') {
    if (tabId === 'overview') return rows([
      ['Nome', e.title], ['Strategia', e.strategy], ['Modello', e.model], ['Status', e.badge],
      ['Docs KB', e.docs], ['Latenza', e.avgLatency]
    ]);
    if (tabId === 'history') return (<>
      <h4>Chat recenti</h4>
      {DS.chats.filter(c => c.agentId === e.id).map(c => linkRow(c))}
    </>);
    if (tabId === 'config') return (<>
      <h4>Parametri</h4>
      {rows([['temperature', '0.7'], ['maxTokens', '2048'], ['topP', '0.95'], ['model', e.model]])}
    </>);
  }
  if (e.type === 'player') {
    if (tabId === 'profile') return (<>
      <div style={{display:'flex',alignItems:'center',gap:10,marginBottom:10}}>
        <div style={{width:48,height:48,borderRadius:'50%',background:DS.color('player'),color:'#fff',display:'flex',alignItems:'center',justifyContent:'center',fontWeight:800}}>{e.initials}</div>
        <div><div style={{fontWeight:700,fontSize:14,color:'var(--text)'}}>{e.title}</div><div style={{fontSize:11,color:'var(--text-muted)'}}>{e.subtitle}</div></div>
      </div>
      {rows([['Preferito', e.fav], ['Status', e.badge]])}
    </>);
    if (tabId === 'stats') return rows([
      ['Vittorie', e.totalWins], ['Partite', e.totalSessions], ['Win Rate', `${Math.round(e.winRate*100)}%`]
    ]);
    if (tabId === 'history') return (<>
      <h4>Sessioni partecipate</h4>
      {DS.sessions.filter(s => (s.playerIds||[]).includes(e.id)).map(s => linkRow(s))}
    </>);
  }
  if (e.type === 'kb') {
    if (tabId === 'overview') return rows([
      ['File', e.title], ['Size', e.size], ['Pagine', e.pages], ['Chunks', e.chunks||'—'],
      ['Status', e.badge], ['Embedding', e.embedding || 'pending']
    ]);
    if (tabId === 'preview') return (<div style={{padding:10,background:'var(--bg-muted)',borderRadius:8,fontSize:11,lineHeight:1.6}}>
      <p><strong>{e.title.toUpperCase().replace(/\.\w+$/,'')}</strong></p>
      <p>Regolamento del gioco. Le regole sono divise in fase setup, fase di gioco, e fase di scoring. Ogni turno un giocatore...</p>
      <p style={{fontSize:9,color:'var(--text-muted)',marginTop:6}}>500 / 24,567 char</p>
    </div>);
    if (tabId === 'citations') return (<>
      <h4>Chat che citano questo doc</h4>
      {DS.chats.filter(c => c.gameId === e.gameId).slice(0,3).map(c => linkRow(c))}
    </>);
  }
  if (e.type === 'chat') {
    if (tabId === 'messages') return (<>
      <div style={{display:'flex',flexDirection:'column',gap:4}}>
        <div style={{padding:'6px 10px',borderRadius:10,fontSize:11,background:'var(--bg-muted)',marginLeft:'auto',maxWidth:'80%',borderBottomRightRadius:3}}>{e.title}</div>
        <div style={{padding:'6px 10px',borderRadius:10,fontSize:11,border:'1px solid var(--border)',maxWidth:'80%',borderBottomLeftRadius:3}}>
          Risposta dell'agente con citazione <span style={{padding:'1px 5px',borderRadius:4,fontSize:9,background:`hsl(${DS.EC.kb.h} ${DS.EC.kb.s}% ${DS.EC.kb.l}% / .12)`,color:DS.color('kb'),fontWeight:700}}>📄 p.3 §2</span>
        </div>
      </div>
    </>);
    if (tabId === 'sources') return (<>
      <h4>KB citati</h4>
      {DS.kbs.filter(k => k.gameId === e.gameId).slice(0, 2).map(k => linkRow(k))}
    </>);
  }
  if (e.type === 'event') {
    if (tabId === 'overview') return (<>
      {rows([['Data', e.date], ['Ora', e.time], ['Luogo', e.location]])}
      <h4 style={{marginTop:14}}>Partecipanti ({e.participantIds.length})</h4>
      {(e.participantIds||[]).map(id => { const p = DS.byId[id]; return p && linkRow(p); })}
    </>);
    if (tabId === 'program') return (<>
      <h4>Giochi previsti</h4>
      {(e.gameIds||[]).map(id => { const g = DS.byId[id]; return g && linkRow(g); })}
    </>);
  }
  if (e.type === 'toolkit') {
    if (tabId === 'overview') return rows([
      ['Nome', e.title], ['Versione', e.version], ['Status', e.badge], ['Utilizzi', e.useCount]
    ]);
    if (tabId === 'tools') return (<>
      <h4>Strumenti inclusi</h4>
      {DS.tools.filter(t => t.toolkitId === e.id).map(t => linkRow(t))}
    </>);
    if (tabId === 'history') return (<>
      <h4>Sessioni che hanno usato questo toolkit</h4>
      {DS.sessions.slice(0, 3).map(s => linkRow(s))}
    </>);
  }
  if (e.type === 'tool') {
    if (tabId === 'detail') return rows([
      ['Tipo', e.kind], ['Uses', e.uses],
      ...Object.entries(e.config).map(([k,v]) => [k, String(v)])
    ]);
    if (tabId === 'preview') return (<div style={{padding:14,background:'var(--bg-muted)',borderRadius:10,textAlign:'center'}}>
      <div style={{fontSize:10,color:'var(--text-muted)',textTransform:'uppercase',fontWeight:700}}>{e.kind}</div>
      <div style={{fontSize:32,fontWeight:800,fontFamily:'var(--f-display)',color:DS.color('tool'),margin:'10px 0'}}>{e.kind === 'timer' ? '4:12' : '127'}</div>
      <div style={{height:6,borderRadius:3,background:'var(--border)',overflow:'hidden',margin:'8px 0'}}>
        <div style={{height:'100%',width:'72%',background:DS.color('tool')}}></div>
      </div>
    </div>);
  }
  return <p style={{color:'var(--text-muted)',textAlign:'center',padding:20}}>Contenuto tab in sviluppo</p>;
}

// ─── SCREENS ─────────────────────────────────────────

function HomeFeed({ onOpen }) {
  return (
    <>
      <div style={{padding:'16px 14px'}}>
        <div style={{fontFamily:'var(--f-display)',fontSize:22,fontWeight:700,letterSpacing:'-.01em'}}>Ciao Marco 👋</div>
        <div style={{fontSize:12,color:'var(--text-sec)'}}>23 partite questo mese · 65% win rate</div>
      </div>
      <div style={{padding:'0 14px 10px'}}>
        <div style={{fontFamily:'var(--f-mono)',fontSize:10,color:'var(--text-muted)',textTransform:'uppercase',letterSpacing:'.08em',marginBottom:6}}>Continua</div>
        {DS.sessions.filter(s => s.state === 'live' || s.state === 'paused').slice(0, 2).map(s => (
          <div key={s.id} style={{'--e':entStyle(s.type)}} className="row" onClick={() => onOpen(s)}>
            <div className="rcov" style={cov(s)}>{s.coverEmoji}</div>
            <div className="rbody">
              <div className="rti">{s.title}</div>
              <div className="rsub">{s.subtitle}</div>
              <div className="rmeta"><span className="rtag">{s.badge}</span></div>
            </div>
            <div style={{width:32,height:32,borderRadius:'50%',background:DS.color(s.type),color:'#fff',display:'flex',alignItems:'center',justifyContent:'center'}}>▶</div>
          </div>
        ))}
      </div>
      <div style={{padding:'10px 14px'}}>
        <div style={{fontFamily:'var(--f-mono)',fontSize:10,color:'var(--text-muted)',textTransform:'uppercase',letterSpacing:'.08em',marginBottom:6}}>Attività recente</div>
        {DS.activity.slice(0, 6).map(a => {
          const ref = DS.byId[a.ref]; if (!ref) return null;
          return (
            <div key={a.id} style={{display:'flex',alignItems:'center',gap:8,padding:'8px 0',borderBottom:'1px solid var(--border-light)',cursor:'pointer'}} onClick={() => onOpen(ref)}>
              <div style={{width:24,height:24,borderRadius:6,background:`hsl(${entStyle(a.kind)} / .15)`,color:DS.color(a.kind),display:'flex',alignItems:'center',justifyContent:'center',fontSize:12,flexShrink:0}}>{DS.EC[a.kind].em}</div>
              <div style={{flex:1,fontSize:12,lineHeight:1.4,color:'var(--text-sec)'}}>
                <strong style={{color:'var(--text)'}}>{a.who}</strong> {a.what} <span style={{color:DS.color(a.kind),fontWeight:700}}>{ref.title}</span>
                <div style={{fontSize:9,color:'var(--text-muted)',marginTop:1}}>{a.at}</div>
              </div>
            </div>
          );
        })}
      </div>
    </>
  );
}

function SearchScreen({ onOpen }) {
  const [q, setQ] = useState('');
  const filtered = q ? DS.all.filter(e => e.title.toLowerCase().includes(q.toLowerCase())) : DS.all.slice(0, 8);
  return (
    <>
      <div style={{padding:14}}>
        <div style={{position:'relative'}}>
          <input value={q} onChange={e => setQ(e.target.value)} placeholder="Cerca giochi, giocatori, sessioni..."
            style={{width:'100%',padding:'10px 12px 10px 34px',borderRadius:10,border:'1.5px solid var(--border)',background:'var(--bg-card)',fontSize:13,color:'var(--text)',outline:'none'}} />
          <span style={{position:'absolute',left:10,top:10,fontSize:14,opacity:.5}}>🔍</span>
        </div>
        <div style={{display:'flex',gap:4,marginTop:10,flexWrap:'wrap'}}>
          {['Tutto', 'Giochi', 'Giocatori', 'Agenti', 'Sessioni'].map(f => (
            <button key={f} style={{padding:'3px 10px',borderRadius:12,border:'1px solid var(--border)',background:f==='Tutto'?'hsl(var(--c-game) / .15)':'transparent',color:f==='Tutto'?'hsl(var(--c-game))':'var(--text-sec)',fontSize:11,fontWeight:700,fontFamily:'var(--f-display)',cursor:'pointer'}}>{f}</button>
          ))}
        </div>
      </div>
      <div>
        {filtered.map(e => (
          <div key={e.id} style={{'--e':entStyle(e.type)}} className="row" onClick={() => onOpen(e)}>
            <div className="rcov" style={cov(e)}>{e.coverEmoji || DS.EC[e.type].em}</div>
            <div className="rbody">
              <div className="rti">{e.title}</div>
              <div className="rsub">{e.subtitle || DS.EC[e.type].lb}</div>
              <div className="rmeta"><span className="rtag">{DS.EC[e.type].lb}</span></div>
            </div>
          </div>
        ))}
      </div>
    </>
  );
}

function LibraryScreen({ onOpen }) {
  const [filter, setFilter] = useState('Tutto');
  const map = { 'Tutto': null, 'Giochi':'game','Player':'player','Sessioni':'session','Agenti':'agent','KB':'kb','Eventi':'event','Toolkit':'toolkit' };
  const t = map[filter];
  const list = t ? DS.all.filter(e => e.type === t) : DS.all.filter(e => ['game','session','agent','event','toolkit'].includes(e.type));
  return (
    <>
      <div style={{padding:'14px 14px 8px'}}>
        <div style={{fontFamily:'var(--f-display)',fontSize:20,fontWeight:700}}>La tua libreria</div>
        <div style={{fontSize:11,color:'var(--text-muted)',marginTop:2}}>{list.length} elementi</div>
      </div>
      <div style={{display:'flex',gap:4,padding:'4px 14px 10px',overflowX:'auto',scrollbarWidth:'none'}}>
        {Object.keys(map).map(f => (
          <button key={f} onClick={() => setFilter(f)}
            style={{padding:'4px 10px',borderRadius:12,border:'1px solid var(--border)',background:f===filter?'hsl(var(--c-game) / .15)':'transparent',color:f===filter?'hsl(var(--c-game))':'var(--text-sec)',fontSize:11,fontWeight:700,cursor:'pointer',whiteSpace:'nowrap',fontFamily:'var(--f-display)'}}>
            {f}
          </button>
        ))}
      </div>
      <div>
        {list.map(e => (
          <div key={e.id} style={{'--e':entStyle(e.type)}} className="row" onClick={() => onOpen(e)}>
            <div className="rcov" style={cov(e)}>{e.coverEmoji || DS.EC[e.type].em}</div>
            <div className="rbody">
              <div className="rti">{e.title}</div>
              <div className="rsub">{e.subtitle || `${DS.EC[e.type].lb} · ${e.badge||''}`}</div>
              <div className="rmeta">
                <span className="rtag">{e.badge || DS.EC[e.type].lb}</span>
                {e.rating && <span className="rtag">⭐ {e.rating}</span>}
              </div>
            </div>
          </div>
        ))}
      </div>
    </>
  );
}

function ChatsScreen({ onOpen }) {
  return (
    <>
      <div style={{padding:14}}>
        <div style={{fontFamily:'var(--f-display)',fontSize:20,fontWeight:700}}>Chat</div>
        <div style={{fontSize:11,color:'var(--text-muted)'}}>{DS.chats.length} conversazioni</div>
      </div>
      {DS.chats.map(c => (
        <div key={c.id} style={{'--e':entStyle(c.type)}} className="row" onClick={() => onOpen(c)}>
          <div className="rcov" style={{background:DS.color('chat'),display:'flex',alignItems:'center',justifyContent:'center',color:'#fff',fontSize:20}}>💬</div>
          <div className="rbody">
            <div className="rti">{c.title}</div>
            <div className="rsub">{c.subtitle}</div>
            <div className="rmeta">
              <span className="rtag">{c.msgCount} msg</span>
              <span className="rtag" style={{background:`hsl(${entStyle('agent')} / .12)`,color:DS.color('agent')}}>{DS.byId[c.agentId]?.title}</span>
            </div>
          </div>
          <div style={{fontSize:9,color:'var(--text-muted)',flexShrink:0,textAlign:'right'}}>{c.lastAt}</div>
        </div>
      ))}
    </>
  );
}

function ProfileScreen({ onOpen }) {
  const me = DS.players[0];
  return (
    <>
      <div style={{padding:24,textAlign:'center'}}>
        <div style={{width:80,height:80,borderRadius:'50%',margin:'0 auto 10px',background:DS.color('player'),color:'#fff',display:'flex',alignItems:'center',justifyContent:'center',fontSize:28,fontWeight:800}}>{me.initials}</div>
        <div style={{fontFamily:'var(--f-display)',fontSize:22,fontWeight:700}}>{me.title}</div>
        <div style={{fontSize:12,color:'var(--text-sec)'}}>{me.subtitle}</div>
      </div>
      <div style={{display:'grid',gridTemplateColumns:'1fr 1fr 1fr',gap:8,padding:'0 14px 14px'}}>
        <div style={{padding:10,borderRadius:10,background:'var(--bg-muted)',textAlign:'center'}}>
          <div style={{fontSize:18,fontWeight:800,fontFamily:'var(--f-display)',color:DS.color('player')}}>{me.totalWins}</div>
          <div style={{fontSize:9,textTransform:'uppercase',color:'var(--text-muted)',fontWeight:700}}>Vittorie</div>
        </div>
        <div style={{padding:10,borderRadius:10,background:'var(--bg-muted)',textAlign:'center'}}>
          <div style={{fontSize:18,fontWeight:800,fontFamily:'var(--f-display)',color:DS.color('toolkit')}}>{Math.round(me.winRate*100)}%</div>
          <div style={{fontSize:9,textTransform:'uppercase',color:'var(--text-muted)',fontWeight:700}}>Win Rate</div>
        </div>
        <div style={{padding:10,borderRadius:10,background:'var(--bg-muted)',textAlign:'center'}}>
          <div style={{fontSize:18,fontWeight:800,fontFamily:'var(--f-display)',color:DS.color('chat')}}>{me.totalSessions}</div>
          <div style={{fontSize:9,textTransform:'uppercase',color:'var(--text-muted)',fontWeight:700}}>Partite</div>
        </div>
      </div>
      <div style={{padding:'0 14px 10px'}}>
        <div style={{fontFamily:'var(--f-mono)',fontSize:10,color:'var(--text-muted)',textTransform:'uppercase',letterSpacing:'.08em',marginBottom:6}}>Sessioni partecipate</div>
        {DS.sessions.filter(s => (s.playerIds||[]).includes(me.id)).map(s => (
          <div key={s.id} style={{'--e':entStyle(s.type)}} className="row" onClick={() => onOpen(s)}>
            <div className="rcov" style={cov(s)}>{s.coverEmoji}</div>
            <div className="rbody">
              <div className="rti">{s.title}</div>
              <div className="rsub">{s.subtitle}</div>
            </div>
          </div>
        ))}
      </div>
    </>
  );
}

// ─── MAIN APP ────────────────────────────────────────
function MobileApp({ initialScreen = 'home', sessionMode = false }) {
  const [screen, setScreen] = useState(initialScreen);
  const [drawer, setDrawer] = useState(null);
  const [entityView, setEntityView] = useState(null);
  const [sessMode, setSessMode] = useState(sessionMode);

  useEffect(() => { if (sessionMode) setEntityView(DS.sessions.find(s => s.state === 'live')); }, [sessionMode]);

  const openEntity = (e) => {
    // In drawer: if already in drawer, replace; else if we're on a screen, show drawer
    if (drawer) { setDrawer(e); return; }
    setDrawer(e);
  };

  const navHome = () => { setScreen('home'); setEntityView(null); setSessMode(false); setDrawer(null); };

  return (
    <div style={{position:'relative', flex:1, minHeight:0, width:'100%', display:'flex', flexDirection:'column'}}>
      <TopBar onHome={navHome} />
      {sessMode && (
        <div className="sbanner">
          <span className="pulse"></span>
          <span className="stxt">Sessione: {entityView?.title} · {entityView?.turn}</span>
          <button className="sb-btn">📊</button>
          <button className="sb-btn">🧰</button>
          <button className="sb-btn" onClick={() => setSessMode(false)}>✕</button>
        </div>
      )}
      <div className="content">
        {entityView && !sessMode && <EntityDetail entity={entityView} onOpenEntity={openEntity} />}
        {!entityView && screen === 'home' && <HomeFeed onOpen={(e) => { if (e.type === 'session' && e.state === 'live') { setEntityView(e); setSessMode(true); } else openEntity(e); }} />}
        {!entityView && screen === 'search' && <SearchScreen onOpen={openEntity} />}
        {!entityView && screen === 'library' && <LibraryScreen onOpen={openEntity} />}
        {!entityView && screen === 'chats' && <ChatsScreen onOpen={openEntity} />}
        {!entityView && screen === 'profile' && <ProfileScreen onOpen={openEntity} />}
        {sessMode && entityView && (
          <div style={{padding:14}}>
            <div style={{aspectRatio:'1',borderRadius:14,background:entityView.cover,display:'flex',alignItems:'center',justifyContent:'center',fontSize:80,color:'rgba(255,255,255,.85)',marginBottom:12,position:'relative'}}>
              {entityView.coverEmoji}
              <div style={{position:'absolute',top:10,left:10,padding:'4px 10px',borderRadius:999,background:'rgba(255,255,255,.9)',color:DS.color('session'),fontSize:10,fontWeight:800,fontFamily:'var(--f-display)'}}>IN SESSIONE</div>
            </div>
            <div style={{fontFamily:'var(--f-display)',fontSize:18,fontWeight:700}}>Classifica live</div>
            {(entityView.playerIds||[]).map((id,i) => {
              const p = DS.byId[id]; if (!p) return null;
              const pts = [72,61,54,48][i] || 0;
              return (
                <div key={id} style={{display:'flex',alignItems:'center',gap:8,padding:'10px 0',borderBottom:'1px solid var(--border-light)'}} onClick={() => openEntity(p)}>
                  <div style={{fontSize:18,width:22}}>{['🥇','🥈','🥉','4️⃣'][i]}</div>
                  <div style={{width:30,height:30,borderRadius:'50%',background:DS.color('player'),color:'#fff',display:'flex',alignItems:'center',justifyContent:'center',fontWeight:800,fontSize:11}}>{p.initials}</div>
                  <div style={{flex:1,fontWeight:700,fontSize:13}}>{p.title}</div>
                  <div style={{fontWeight:800,fontFamily:'var(--f-display)',color:i===0?DS.color('toolkit'):'var(--text)',fontSize:15}}>{pts}pt</div>
                </div>
              );
            })}
          </div>
        )}
      </div>
      <BottomBar screen={screen} onNav={setScreen} sessionMode={sessMode} onClose={() => { setSessMode(false); setEntityView(null); setScreen('home'); }} />
      <Drawer entity={drawer} open={!!drawer} onClose={() => setDrawer(null)} onOpenEntity={openEntity} />
    </div>
  );
}

// ─── ROOT: 3 phones side-by-side ────────────────────
function Root() {
  const [variant, setVariant] = useState('gallery'); // gallery | focus

  if (variant === 'focus') {
    return (
      <>
        <SwitchBar value={variant} onChange={setVariant} />
        <div style={{display:'flex', justifyContent:'center'}}>
          <Phone label="Focus view" desc="Un solo phone, più spazio per interagire">
            <MobileApp initialScreen="home" />
          </Phone>
        </div>
      </>
    );
  }

  return (
    <>
      <SwitchBar value={variant} onChange={setVariant} />
      <div className="phones-grid">
        <Phone label="Home feed + Drawer" desc="Tap una card → apre drawer. Tap un connection pip dentro al drawer → sostituisce entità.">
          <MobileApp initialScreen="home" />
        </Phone>
        <Phone label="Library + Search" desc="Filtri rapidi per tipo entity. Tap una row → drawer. Ogni tipo ha colore e tabs specifici.">
          <MobileApp initialScreen="library" />
        </Phone>
        <Phone label="Session Mode attivo" desc="Banner blu + bottom bar dedicato. I giocatori nella classifica sono cliccabili → drawer Player.">
          <MobileApp sessionMode={true} />
        </Phone>
      </div>
    </>
  );
}

function SwitchBar({ value, onChange }) {
  return (
    <div style={{display:'flex',gap:8,justifyContent:'center',marginBottom:24}}>
      <button onClick={() => onChange('gallery')}
        style={{padding:'7px 14px',borderRadius:10,border:value==='gallery'?'1px solid transparent':'1px solid var(--border)',background:value==='gallery'?'hsl(var(--c-game))':'var(--bg-card)',color:value==='gallery'?'#fff':'var(--text-sec)',fontFamily:'var(--f-display)',fontSize:12,fontWeight:700,cursor:'pointer'}}>
        Gallery (3 phones)
      </button>
      <button onClick={() => onChange('focus')}
        style={{padding:'7px 14px',borderRadius:10,border:value==='focus'?'1px solid transparent':'1px solid var(--border)',background:value==='focus'?'hsl(var(--c-game))':'var(--bg-card)',color:value==='focus'?'#fff':'var(--text-sec)',fontFamily:'var(--f-display)',fontSize:12,fontWeight:700,cursor:'pointer'}}>
        Focus (1 phone)
      </button>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<Root />);
