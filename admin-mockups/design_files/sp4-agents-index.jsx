/* MeepleAI SP4 wave 1 — Schermata #3 / 5
   Route: /agents
   File: admin-mockups/design_files/sp4-agents-index.{html,jsx}
   Pattern desktop: Hero leggero + grid agenti (3-col 1280 max)

   Hero 120-140px (100 mobile): titolo + subtitle + action bar destra
   ([+ Crea agente] primary entity-agent + filter chips status).
   Sotto-hero sticky: filter chips + search + sort.
   Body grid agenti MeepleCard variante grid entity=agent.

   Stati: default · empty · loading · error · filter-no-results.
   Mobile 375 → 1 col. Tablet 768 → 2 col. Desktop 1440 → 3 col / 1280 max.

   v2 nuovi:
   - AgentsHero          → apps/web/src/components/ui/v2/agents-hero/
   - AgentFilters        → apps/web/src/components/ui/v2/agent-filters/
   - AgentCardGrid       → variante MeepleCard.grid per entity=agent
   - EmptyAgents         → apps/web/src/components/ui/v2/empty-agents/

   Riusi pixel-perfect:
   - ConnectionChipStrip (PR #549/#552) come footer card
   - EntityChip game cliccabile (per link gioco assegnato)
*/

const { useState, useEffect, useMemo } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.agent;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// Estendo i 5 agenti DS a 9 per popolare grid 3-col
const AGENTS_INDEX = [
  ...DS.agents,
  { id:'a-cascadia', type:'agent', title:'Naturalista di Cascadia', avatar:'🍃',
    gameId:'g-cascadia', tags:['rules-master', 'tile-laying'], desc:'Spiega tessere e habitat scoring.',
    chatCount: 38, kbCount: 1, sessionCount: 7, lastActive:'1h fa', status:'attivo',
    model:'Claude Sonnet 4', invocations: 87 },
  { id:'a-marvel', type:'agent', title:'Coach Marvel Champions', avatar:'🦸',
    gameId:'g-marvel', tags:['lcg', 'helper'], desc:'Combo deck e strategia per ogni eroe.',
    chatCount: 152, kbCount: 4, sessionCount: 14, lastActive:'30m fa', status:'attivo',
    model:'Claude Opus 4', invocations: 412 },
  { id:'a-dune', type:'agent', title:'Mentat di Arrakis', avatar:'🪐',
    gameId:'g-dune', tags:['expert', 'strategist'], desc:'Workers, intrighi e svolte di battaglia.',
    chatCount: 67, kbCount: 2, sessionCount: 9, lastActive:'5m fa', status:'attivo',
    model:'Claude Sonnet 4', invocations: 198 },
  { id:'a-terraforming', type:'agent', title:'Architetto di Marte', avatar:'🚀',
    gameId:'g-terraforming', tags:['heavy', 'expert'], desc:'Progetti e milestone, ottimizza ossigeno e calore.',
    chatCount: 0, kbCount: 3, sessionCount: 0, lastActive:'mai', status:'in-setup',
    model:'Claude Sonnet 4', invocations: 0 },
  { id:'a-pandemic', type:'agent', title:'Medico CDC', avatar:'🧬',
    gameId:'g-pandemic', tags:['legacy', 'helper'], desc:'Coordinazione team, code rules legacy.',
    chatCount: 12, kbCount: 1, sessionCount: 4, lastActive:'2g fa', status:'archiviato',
    model:'Claude Haiku 4', invocations: 24 },
];
// Aggiungo campi mancanti a quelli base DS
const baseAgents = DS.agents.map(a => ({
  ...a,
  status: a.id === 'a-azul' ? 'attivo' : (a.invocations > 0 ? 'attivo' : 'in-setup'),
  chatCount: DS.chats.filter(c => c.gameId === a.gameId).reduce((s, c) => s + (c.messageCount || 0), 0) || a.invocations || 0,
  kbCount: DS.kbs.filter(k => k.gameId === a.gameId).length,
  sessionCount: DS.sessions.filter(s => s.gameId === a.gameId).length,
}));
const ALL_AGENTS = [...baseAgents, ...AGENTS_INDEX.slice(DS.agents.length)];

// ─── Helpers ────────────────────────────────────────
const STATUS_META = {
  'attivo':     { label:'● Attivo',     color:'success', dotColor:'hsl(140, 60%, 45%)' },
  'in-setup':   { label:'⚙ In setup',   color:'warning', dotColor:'hsl(38, 92%, 50%)' },
  'archiviato': { label:'⊘ Archiviato', color:'muted',   dotColor:'hsl(220, 10%, 60%)' },
};

// ═══════════════════════════════════════════════════════
// ─── CONNECTIONCHIPSTRIP (footer, prod) ─────────────
// /* v2: ConnectionChipStrip — ✅ in produzione (PR #549/#552) */
// ═══════════════════════════════════════════════════════
const ConnectionChipStrip = ({ connections, compact }) => {
  const visible = connections.filter(c => !c.isEmpty).slice(0, 3);
  if (visible.length === 0) {
    return (
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
        fontWeight: 600, letterSpacing:'.04em',
      }}>Nessuna connessione</div>
    );
  }
  return (
    <div style={{ display:'flex', gap: 4, flexWrap:'wrap' }}>
      {visible.map(c => {
        const ec = DS.EC[c.entityType];
        return (
          <span key={c.entityType} style={{
            display:'inline-flex', alignItems:'center', gap: 3,
            padding: compact ? '2px 6px' : '3px 7px',
            borderRadius: 999,
            background: entityHsl(c.entityType, 0.1),
            color: entityHsl(c.entityType),
            fontFamily:'var(--f-display)', fontSize: compact ? 10 : 11, fontWeight: 700,
            lineHeight: 1.2,
          }}>
            <span aria-hidden="true" style={{ fontSize: compact ? 10 : 11 }}>{ec.em}</span>
            <span style={{ fontFamily:'var(--f-mono)', fontVariantNumeric:'tabular-nums' }}>{c.count}</span>
          </span>
        );
      })}
    </div>
  );
};

// ─── EntityChip game cliccabile ─────────────────────
const EntityChipGame = ({ gameId, compact }) => {
  const g = DS.byId[gameId];
  if (!g) return null;
  return (
    <a href="#" onClick={e => e.preventDefault()} style={{
      display:'inline-flex', alignItems:'center', gap: 5,
      padding: compact ? '2px 7px' : '3px 9px',
      borderRadius:'var(--r-pill)',
      background: entityHsl('game', 0.1),
      color: entityHsl('game'),
      fontFamily:'var(--f-display)',
      fontSize: compact ? 10 : 11, fontWeight: 700,
      textDecoration:'none',
      border:`1px solid ${entityHsl('game', 0.2)}`,
      whiteSpace:'nowrap',
      maxWidth:'100%', overflow:'hidden', textOverflow:'ellipsis',
    }}>
      <span aria-hidden="true">🎲</span>
      <span style={{ overflow:'hidden', textOverflow:'ellipsis' }}>{g.title}</span>
    </a>
  );
};

// ═══════════════════════════════════════════════════════
// ─── AGENT CARD GRID (variante MeepleCard) ──────────
// /* v2: AgentCardGrid — variante MeepleCard.grid entity=agent */
// ═══════════════════════════════════════════════════════
const AgentCardGrid = ({ agent, compact }) => {
  const game = DS.byId[agent.gameId];
  const status = STATUS_META[agent.status] || STATUS_META['attivo'];
  const connections = [
    { entityType:'game', count: 1, isEmpty: false },
    { entityType:'kb', count: agent.kbCount, isEmpty: agent.kbCount === 0 },
    { entityType:'chat', count: agent.chatCount, isEmpty: agent.chatCount === 0 },
  ];
  return (
    <article tabIndex={0} className="mai-card-agent" style={{
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
      overflow:'hidden',
      cursor:'pointer',
      display:'flex', flexDirection:'column',
      position:'relative',
      transition:'transform var(--dur-sm) var(--ease-out), border-color var(--dur-sm), box-shadow var(--dur-sm)',
    }}>
      {/* Top accent bar entity-agent */}
      <div style={{
        height: 4, background:`linear-gradient(90deg, ${entityHsl('agent')} 0%, ${entityHsl('agent', 0.5)} 100%)`,
      }}/>

      {/* Header: avatar + status badge */}
      <div style={{
        display:'flex', alignItems:'flex-start', gap: 12,
        padding: compact ? '14px 14px 10px' : '16px 18px 12px',
      }}>
        <div style={{
          width: compact ? 52 : 58, height: compact ? 52 : 58,
          borderRadius:'var(--r-md)',
          background: entityHsl('agent', 0.12),
          color: entityHsl('agent'),
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: compact ? 28 : 32,
          flexShrink: 0,
          border:`1px solid ${entityHsl('agent', 0.2)}`,
        }} aria-hidden="true">{agent.avatar}</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display:'flex', alignItems:'center', gap: 6, marginBottom: 4 }}>
            <span style={{
              padding:'2px 7px', borderRadius:'var(--r-pill)',
              background: entityHsl('agent', 0.14), color: entityHsl('agent'),
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform:'uppercase', letterSpacing:'.06em',
              display:'inline-flex', alignItems:'center', gap: 3,
            }}><span aria-hidden="true">🤖</span>Agent</span>
            <span style={{
              padding:'2px 7px', borderRadius:'var(--r-pill)',
              background: status.color === 'success' ? 'hsl(140, 60%, 45% / .14)'
                       : status.color === 'warning' ? entityHsl('agent', 0.14)
                       : 'var(--bg-muted)',
              color: status.color === 'success' ? 'hsl(140, 50%, 35%)'
                   : status.color === 'warning' ? entityHsl('agent')
                   : 'var(--text-muted)',
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform:'uppercase', letterSpacing:'.06em',
            }}>{status.label}</span>
          </div>
          <h3 style={{
            fontFamily:'var(--f-display)', fontWeight: 800,
            fontSize: compact ? 15 : 16, lineHeight: 1.15,
            color:'var(--text)', margin:'0 0 5px',
            display:'-webkit-box', WebkitLineClamp: 1, WebkitBoxOrient:'vertical', overflow:'hidden',
          }}>{agent.title}</h3>
          {game && <EntityChipGame gameId={agent.gameId} compact={compact}/>}
        </div>
      </div>

      {/* Body: description + KPI inline */}
      <div style={{
        padding: compact ? '0 14px 12px' : '0 18px 14px',
        flex: 1,
        display:'flex', flexDirection:'column', gap: 10,
      }}>
        <p style={{
          fontSize: compact ? 12.5 : 13, color:'var(--text-sec)',
          lineHeight: 1.5, margin: 0,
          display:'-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient:'vertical', overflow:'hidden',
        }}>{agent.desc || 'Agente esperto del gioco, pronto a rispondere alle tue domande sulle regole.'}</p>

        <div style={{
          display:'flex', flexWrap:'wrap', gap: 10,
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          fontWeight: 700,
        }}>
          <span style={{ display:'inline-flex', alignItems:'center', gap: 4 }}>
            <span aria-hidden="true">💬</span>
            <span style={{ fontVariantNumeric:'tabular-nums', color:'var(--text-sec)' }}>{agent.chatCount}</span>
          </span>
          <span style={{ display:'inline-flex', alignItems:'center', gap: 4 }}>
            <span aria-hidden="true">📚</span>
            <span style={{ fontVariantNumeric:'tabular-nums', color:'var(--text-sec)' }}>{agent.kbCount} KB</span>
          </span>
          <span style={{ display:'inline-flex', alignItems:'center', gap: 4 }}>
            <span aria-hidden="true">⏱</span>
            <span style={{ color:'var(--text-sec)' }}>{agent.lastActive}</span>
          </span>
        </div>
      </div>

      {/* Footer: ConnectionChipStrip (max 3) */}
      <div style={{
        padding: compact ? '10px 14px' : '12px 18px',
        borderTop:'1px solid var(--border-light)',
        background:'var(--bg-muted)',
      }}>
        <ConnectionChipStrip connections={connections} compact={compact}/>
      </div>
    </article>
  );
};

// ═══════════════════════════════════════════════════════
// ─── AGENTS HERO ────────────────────────────────────
// /* v2: AgentsHero → apps/web/src/components/ui/v2/agents-hero/ */
// ═══════════════════════════════════════════════════════
const AgentsHero = ({ compact, count }) => (
  <header style={{
    padding: compact ? '14px 16px 12px' : '24px 32px 16px',
    background:`linear-gradient(180deg, ${entityHsl('agent', 0.08)} 0%, transparent 100%)`,
    borderBottom:'1px solid var(--border-light)',
    display:'flex', alignItems: compact ? 'flex-start' : 'center',
    gap: 16, flexDirection: compact ? 'column' : 'row',
  }}>
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 6, marginBottom: 8,
      }}>
        <span style={{
          display:'inline-flex', alignItems:'center', gap: 5,
          padding:'3px 9px', borderRadius:'var(--r-pill)',
          background: entityHsl('agent', 0.14),
          color: entityHsl('agent'),
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.08em',
        }}><span aria-hidden="true">🤖</span>Agents · /agents</span>
      </div>
      <h1 style={{
        fontFamily:'var(--f-display)', fontWeight: 800,
        fontSize: compact ? 22 : 32, letterSpacing:'-.02em', lineHeight: 1.05,
        color:'var(--text)', margin:'0 0 4px',
      }}>I tuoi agenti AI</h1>
      <p style={{
        color:'var(--text-sec)', fontSize: compact ? 12.5 : 14, lineHeight: 1.55,
        margin: 0,
      }}>Esperti dei tuoi giochi, sempre disponibili — {count} agenti totali.</p>
    </div>
    <button type="button" style={{
      padding: compact ? '9px 14px' : '10px 18px',
      borderRadius:'var(--r-md)',
      background: entityHsl('agent'), color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
      cursor:'pointer',
      boxShadow:`0 4px 14px ${entityHsl('agent', 0.4)}`,
      flexShrink: 0,
      display:'inline-flex', alignItems:'center', gap: 6,
      alignSelf: compact ? 'stretch' : 'auto',
      justifyContent:'center',
    }}>
      <span aria-hidden="true">+</span>Crea agente
    </button>
  </header>
);

// ═══════════════════════════════════════════════════════
// ─── AGENT FILTERS (sticky) ─────────────────────────
// /* v2: AgentFilters → apps/web/src/components/ui/v2/agent-filters/ */
// ═══════════════════════════════════════════════════════
const STATUS_FILTERS = [
  { id:'all', label:'Tutti' },
  { id:'attivo', label:'Attivi' },
  { id:'in-setup', label:'In setup' },
  { id:'archiviato', label:'Archiviati' },
];
const SORT_OPTS = [
  { id:'recent', label:'Più recenti' },
  { id:'alpha', label:'Alfabetico' },
  { id:'used', label:'Più usati' },
];

const AgentFilters = ({
  q, setQ, status, setStatus, sort, setSort, compact, count,
}) => (
  <div className="mai-cb-scroll" style={{
    padding: compact ? '10px 16px' : '12px 32px',
    display:'flex', alignItems:'center',
    gap: compact ? 8 : 12,
    borderBottom:'1px solid var(--border-light)',
    background:'var(--glass-bg)', backdropFilter:'blur(12px)',
    position:'sticky', top: 0, zIndex: 10,
    overflowX: compact ? 'auto' : 'visible',
    scrollbarWidth:'none',
  }}>
    {/* Status chips */}
    <div role="tablist" style={{
      display:'inline-flex', gap: 4, flexShrink: 0,
    }}>
      {STATUS_FILTERS.map(o => {
        const active = status === o.id;
        return (
          <button key={o.id} role="tab" aria-selected={active}
            onClick={()=>setStatus(o.id)}
            style={{
              padding:'6px 12px',
              border: active ? `1px solid ${entityHsl('agent', 0.3)}` : '1px solid var(--border)',
              background: active ? entityHsl('agent', 0.12) : 'var(--bg-card)',
              color: active ? entityHsl('agent') : 'var(--text-sec)',
              fontFamily:'var(--f-display)',
              fontSize: 12, fontWeight: 700,
              borderRadius:'var(--r-pill)',
              cursor:'pointer', whiteSpace:'nowrap',
              transition:'all var(--dur-sm) var(--ease-out)',
            }}>{o.label}</button>
        );
      })}
    </div>

    <div style={{ flex: 1 }}/>

    {/* Search */}
    {!compact && (
      <label style={{
        display:'flex', alignItems:'center', gap: 8,
        flex:'0 1 280px',
        padding:'7px 12px',
        background:'var(--bg-card)',
        border:'1px solid var(--border)',
        borderRadius:'var(--r-md)',
      }}>
        <span aria-hidden="true" style={{ color:'var(--text-muted)' }}>🔍</span>
        <input
          type="text" value={q} onChange={e=>setQ(e.target.value)}
          placeholder="Cerca agente o gioco…"
          style={{
            flex: 1, border:'none', outline:'none', background:'transparent',
            color:'var(--text)', fontFamily:'var(--f-body)', fontSize: 13,
            minWidth: 0,
          }}
        />
        {q && (
          <button type="button" onClick={()=>setQ('')} aria-label="Pulisci"
            style={{ border:'none', background:'transparent', color:'var(--text-muted)',
              cursor:'pointer', fontSize: 13, padding: 0 }}>✕</button>
        )}
      </label>
    )}

    {/* Sort */}
    <select value={sort} onChange={e=>setSort(e.target.value)}
      aria-label="Ordina"
      style={{
        padding:'7px 10px', borderRadius:'var(--r-md)',
        border:'1px solid var(--border)', background:'var(--bg-card)',
        color:'var(--text)', fontFamily:'var(--f-display)',
        fontSize: 12, fontWeight: 700, cursor:'pointer', flexShrink: 0,
      }}>
      {SORT_OPTS.map(o => <option key={o.id} value={o.id}>{o.label}</option>)}
    </select>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── EMPTY / ERROR / LOADING ────────────────────────
// ═══════════════════════════════════════════════════════
// /* v2: EmptyAgents → apps/web/src/components/ui/v2/empty-agents/ */
const EmptyAgents = ({ kind, onAction, compact }) => {
  const cfg = {
    'no-agents': {
      icon:'🤖',
      t:'Non hai ancora creato agenti',
      sub:'Gli agenti AI rispondono alle domande sui tuoi giochi usando i regolamenti che carichi. Crea il primo per iniziare.',
      cta:'Crea il tuo primo agente',
    },
    'no-results': {
      icon:'🔎',
      t:'Nessun agente corrisponde',
      sub:'Prova a cambiare filtro status o azzera la ricerca per vedere tutti gli agenti.',
      cta:'Azzera filtri',
    },
  }[kind];
  return (
    <div style={{
      gridColumn:'1 / -1',
      display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
      padding: compact ? '40px 20px' : '64px 32px',
      background:'var(--bg-card)',
      border:'1px dashed var(--border-strong)',
      borderRadius:'var(--r-xl)',
    }}>
      <div style={{
        width: compact ? 64 : 80, height: compact ? 64 : 80,
        borderRadius:'50%',
        background: entityHsl('agent', 0.12), color: entityHsl('agent'),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: compact ? 32 : 40, marginBottom: 16,
      }} aria-hidden="true">{cfg.icon}</div>
      <h3 style={{
        fontFamily:'var(--f-display)', fontSize: compact ? 16 : 19, fontWeight: 800,
        color:'var(--text)', margin:'0 0 6px',
      }}>{cfg.t}</h3>
      <p style={{
        fontSize: 13, color:'var(--text-muted)', maxWidth: 380,
        margin:'0 0 18px', lineHeight: 1.55,
      }}>{cfg.sub}</p>
      <button type="button" onClick={onAction}
        style={{
          padding:'10px 18px', borderRadius:'var(--r-md)',
          background: entityHsl('agent'), color:'#fff', border:'none',
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, cursor:'pointer',
          boxShadow:`0 4px 14px ${entityHsl('agent', 0.35)}`,
        }}>+ {cfg.cta}</button>
    </div>
  );
};

const ErrorState = ({ onRetry, compact }) => (
  <div style={{
    gridColumn:'1 / -1',
    padding: compact ? '32px 20px' : '48px 32px',
    background:'hsl(var(--c-danger) / .08)',
    border:'1px solid hsl(var(--c-danger) / .3)',
    borderRadius:'var(--r-xl)',
    display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
  }}>
    <div style={{
      width: 56, height: 56, borderRadius:'50%',
      background:'hsl(var(--c-danger) / .15)', color:'hsl(var(--c-danger))',
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 26, marginBottom: 12,
    }} aria-hidden="true">⚠</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
      color:'var(--text)', margin:'0 0 4px',
    }}>Impossibile caricare gli agenti</h3>
    <p style={{
      fontSize: 12.5, color:'var(--text-muted)', maxWidth: 360,
      margin:'0 0 16px',
    }}>Si è verificato un errore di rete. Verifica la connessione e riprova.</p>
    <button type="button" onClick={onRetry}
      style={{
        padding:'8px 16px', borderRadius:'var(--r-md)',
        background:'hsl(var(--c-danger))', color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
      }}>↻ Riprova</button>
  </div>
);

const SkeletonCard = ({ compact }) => (
  <div style={{
    background:'var(--bg-card)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)',
    overflow:'hidden',
  }}>
    <div className="mai-shimmer" style={{ height: 4, background:'var(--bg-muted)' }}/>
    <div style={{ padding: compact ? 14 : 18, display:'flex', gap: 12 }}>
      <div className="mai-shimmer" style={{
        width: compact ? 52 : 58, height: compact ? 52 : 58,
        borderRadius:'var(--r-md)', background:'var(--bg-muted)', flexShrink: 0,
      }}/>
      <div style={{ flex: 1, display:'flex', flexDirection:'column', gap: 6 }}>
        <div className="mai-shimmer" style={{ height: 10, width:'40%', borderRadius: 4, background:'var(--bg-muted)' }}/>
        <div className="mai-shimmer" style={{ height: 16, width:'70%', borderRadius: 4, background:'var(--bg-muted)' }}/>
        <div className="mai-shimmer" style={{ height: 10, width:'50%', borderRadius: 4, background:'var(--bg-muted)' }}/>
      </div>
    </div>
    <div style={{ padding:'0 18px 14px' }}>
      <div className="mai-shimmer" style={{ height: 10, width:'90%', borderRadius: 4, background:'var(--bg-muted)', marginBottom: 6 }}/>
      <div className="mai-shimmer" style={{ height: 10, width:'72%', borderRadius: 4, background:'var(--bg-muted)' }}/>
    </div>
    <div style={{
      padding:'10px 18px', borderTop:'1px solid var(--border-light)',
      background:'var(--bg-muted)', display:'flex', gap: 4,
    }}>
      {[0,1,2].map(i => (
        <div key={i} className="mai-shimmer" style={{
          height: 16, width: 28 + i*4, borderRadius: 999, background:'var(--bg-card)',
        }}/>
      ))}
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── BODY ───────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const filterAgents = (list, q, status) => {
  let out = list;
  if (q) {
    const needle = q.toLowerCase();
    out = out.filter(a => {
      const game = DS.byId[a.gameId];
      return a.title.toLowerCase().includes(needle) ||
        (game && game.title.toLowerCase().includes(needle)) ||
        (a.tags || []).some(t => t.toLowerCase().includes(needle));
    });
  }
  if (status !== 'all') out = out.filter(a => a.status === status);
  return out;
};

const sortAgents = (list, sort) => {
  const out = [...list];
  if (sort === 'alpha') return out.sort((a, b) => a.title.localeCompare(b.title));
  if (sort === 'used') return out.sort((a, b) => (b.chatCount || 0) - (a.chatCount || 0));
  return out; // recent default order
};

const AgentsIndexBody = ({ stateOverride, compact }) => {
  const [q, setQ] = useState('');
  const [status, setStatus] = useState('all');
  const [sort, setSort] = useState('recent');

  useEffect(() => {
    if (stateOverride === 'no-results') {
      setStatus('archiviato');
      setQ('zzznotfound');
    } else {
      setStatus('all');
      setQ('');
    }
  }, [stateOverride]);

  const isLoading = stateOverride === 'loading';
  const isEmpty = stateOverride === 'empty';
  const isError = stateOverride === 'error';

  const sourceList = isEmpty ? [] : ALL_AGENTS;
  const filtered = useMemo(() => sortAgents(filterAgents(sourceList, q, status), sort),
    [sourceList, q, status, sort]);

  return (
    <>
      <AgentsHero compact={compact} count={ALL_AGENTS.length}/>
      {!isError && !isEmpty && (
        <AgentFilters
          q={q} setQ={setQ}
          status={status} setStatus={setStatus}
          sort={sort} setSort={setSort}
          compact={compact}
          count={filtered.length}
        />
      )}
      <div style={{
        padding: compact ? '14px 16px 24px' : '24px 32px 64px',
        maxWidth: 1280, margin:'0 auto',
      }}>
        {isError && <ErrorState compact={compact}/>}
        {isEmpty && <EmptyAgents kind="no-agents" compact={compact}/>}
        {!isError && !isEmpty && isLoading && (
          <div style={{
            display:'grid',
            gridTemplateColumns: compact ? '1fr' : 'repeat(auto-fit, minmax(320px, 1fr))',
            gap: compact ? 12 : 16,
          }}>
            {Array.from({ length: compact ? 4 : 6 }).map((_, i) => (
              <SkeletonCard key={i} compact={compact}/>
            ))}
          </div>
        )}
        {!isError && !isEmpty && !isLoading && (
          filtered.length === 0 ? (
            <EmptyAgents kind="no-results" compact={compact}
              onAction={() => { setStatus('all'); setQ(''); }}/>
          ) : (
            <div style={{
              display:'grid',
              gridTemplateColumns: compact ? '1fr' : 'repeat(auto-fit, minmax(320px, 1fr))',
              gap: compact ? 12 : 16,
            }}>
              {filtered.map(a => <AgentCardGrid key={a.id} agent={a} compact={compact}/>)}
            </div>
          )
        )}
      </div>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── DESKTOP / MOBILE SHELLS ─────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopNav = ({ active = 'agents' }) => {
  const items = [
    { id:'home', label:'Home' },
    { id:'library', label:'Libreria' },
    { id:'games', label:'Giochi' },
    { id:'agents', label:'Agenti' },
    { id:'sessions', label:'Sessioni' },
    { id:'toolkit', label:'Toolkit' },
  ];
  return (
    <div style={{
      display:'flex', alignItems:'center', gap: 14,
      padding:'10px 32px',
      background:'var(--glass-bg)', backdropFilter:'blur(12px)',
      borderBottom:'1px solid var(--border)',
    }}>
      <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
        <div style={{
          width: 26, height: 26, borderRadius: 7,
          background:'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)))',
          color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
          fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)',
        }}>M</div>
        <span style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 14 }}>MeepleAI</span>
      </div>
      <div style={{ display:'flex', alignItems:'center', gap: 2, marginLeft: 18 }}>
        {items.map(it => (
          <a key={it.id} href="#" onClick={e=>e.preventDefault()}
            aria-current={it.id === active ? 'page' : undefined}
            style={{
              padding:'6px 12px', borderRadius:'var(--r-md)',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
              color: it.id === active ? entityHsl('agent') : 'var(--text-sec)',
              background: it.id === active ? entityHsl('agent', 0.12) : 'transparent',
              textDecoration:'none',
            }}>{it.label}</a>
        ))}
      </div>
    </div>
  );
};

const DesktopScreen = ({ stateOverride }) => (
  <div style={{ minHeight: 720, background:'var(--bg)' }}>
    <DesktopNav/>
    <AgentsIndexBody stateOverride={stateOverride}/>
  </div>
);

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);
const PhoneTopNav = () => (
  <div style={{
    display:'flex', alignItems:'center', gap: 9,
    padding:'10px 14px', borderBottom:'1px solid var(--border)',
    background:'var(--bg)',
  }}>
    <button aria-label="Indietro" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'transparent', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>←</button>
    <div style={{ flex: 1, fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700, textAlign:'center' }}>
      Agenti
    </div>
    <button aria-label="Cerca" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'transparent', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>🔍</button>
  </div>
);
const MobileBottomBar = () => {
  const tabs = [
    { id:'home', icon:'⌂', label:'Home' },
    { id:'search', icon:'🔍', label:'Cerca' },
    { id:'library', icon:'📚', label:'Libreria' },
    { id:'agents', icon:'🤖', label:'Agenti', active: true },
    { id:'profile', icon:'👤', label:'Profilo' },
  ];
  return (
    <div style={{
      position:'absolute', bottom: 0, left: 0, right: 0,
      display:'flex',
      padding:'8px 10px 12px',
      background:'var(--glass-bg)', backdropFilter:'blur(14px)',
      borderTop:'1px solid var(--border)',
      zIndex: 5,
    }}>
      {tabs.map(t => (
        <button key={t.id} style={{
          flex: 1,
          display:'flex', flexDirection:'column', alignItems:'center', gap: 2,
          padding:'4px 0',
          background:'transparent', border:'none',
          color: t.active ? entityHsl('agent') : 'var(--text-muted)',
          fontFamily:'var(--f-display)', fontSize: 9, fontWeight: 700,
          cursor:'pointer',
        }}>
          <span aria-hidden="true" style={{ fontSize: 16 }}>{t.icon}</span>
          <span>{t.label}</span>
        </button>
      ))}
    </div>
  );
};
const MobileScreen = ({ stateOverride }) => (
  <>
    <PhoneSbar/>
    <div style={{ flex: 1, overflowY:'auto', position:'relative', background:'var(--bg)' }}>
      <PhoneTopNav/>
      <div style={{ paddingBottom: 70 }}>
        <AgentsIndexBody stateOverride={stateOverride} compact/>
      </div>
      <MobileBottomBar/>
    </div>
  </>
);

const PhoneShell = ({ label, desc, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 10 }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div className="phone">{children}</div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 340,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

const DesktopFrame = ({ label, desc, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width:'100%', maxWidth: 1280,
      borderRadius:'var(--r-xl)', border:'1px solid var(--border)',
      background:'var(--bg)', overflow:'hidden',
      boxShadow:'var(--shadow-lg)',
    }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        padding:'9px 14px',
        background:'var(--bg-muted)',
        borderBottom:'1px solid var(--border)',
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      }}>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-event))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-warning))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-toolkit))' }}/>
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/agents</span>
      </div>
      {children}
    </div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 720,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ROOT ────────────────────────────────────────────
const MOBILE_STATES = [
  { id:'default', label:'01 · Default', desc:'9 agenti totali, grid 1-col mobile, footer ConnectionChipStrip max 3 chip (game/kb/chat).' },
  { id:'empty', label:'02 · Empty', desc:'Stato first-run: 🤖 + CTA "Crea il tuo primo agente" entity-agent.' },
  { id:'loading', label:'03 · Loading', desc:'4 skeleton cards con accent bar shimmer + avatar placeholder + 2 line + 3 chip footer.' },
  { id:'no-results', label:'04 · No results', desc:'Filter "Archiviati" + query "zzznotfound" → empty filtrato + CTA "Azzera filtri".' },
  { id:'error', label:'05 · Error', desc:'Banner danger + retry CTA full-width. Hero/filtri nascosti durante errore.' },
];

function ThemeToggle() {
  const [dark, setDark] = useState(false);
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle"
      aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span>
      <span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

function App() {
  return (
    <div className="stage">
      <ThemeToggle/>
      <div className="stage-wrap">
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 8,
        }}>SP4 wave 1 · #3/5 · Hero leggero + grid agenti</div>
        <h1>Agents index — /agents</h1>
        <p className="lead">
          Hero compatto entity-agent + filtri sticky (chips status + search + sort) +
          grid auto-fit 320min (3-col @1280, 2-col tablet, 1-col mobile).
          Card MeepleCard variante <strong>grid</strong> entity=agent con avatar emoji,
          status badge, KPI inline, footer <strong>ConnectionChipStrip</strong> max 3 (game/kb/chat).
        </p>

        <div className="section-label">Mobile · 375 — 5 stati</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <MobileScreen stateOverride={s.id === 'default' ? null : s.id}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1440 — 4 stati</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="06 · Desktop · Default"
            desc="Hero entity-agent + filtri sticky completi (chips + search 280 + sort) + grid 3-col auto-fit 320min, max-width 1280 centrato. 9 agent cards.">
            <DesktopScreen stateOverride={null}/>
          </DesktopFrame>
          <DesktopFrame label="07 · Desktop · Empty"
            desc="First-run: hero visibile (motiva alla creazione), filtri nascosti, empty state full-width entity-agent dashed con CTA primaria.">
            <DesktopScreen stateOverride="empty"/>
          </DesktopFrame>
          <DesktopFrame label="08 · Desktop · Loading"
            desc="6 skeleton cards (3-col × 2 row) con accent bar + avatar shimmer + KPI placeholder + footer chip placeholder. Hero e filtri visibili e interattivi.">
            <DesktopScreen stateOverride="loading"/>
          </DesktopFrame>
          <DesktopFrame label="09 · Desktop · No results"
            desc="Filter 'Archiviati' + query 'zzznotfound' → empty filtrato. Filtri restano sticky con stato corrente, CTA 'Azzera filtri' resetta entrambi.">
            <DesktopScreen stateOverride="no-results"/>
          </DesktopFrame>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim {
          0%   { background-position: -200% 0; }
          100% { background-position:  200% 0; }
        }
        .mai-shimmer {
          background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important;
          background-size: 200% 100% !important;
          animation: mai-shimmer-anim 1.4s linear infinite;
        }
        .phones-grid {
          display: grid;
          grid-template-columns: repeat(auto-fill, minmax(380px, 1fr));
          gap: var(--s-7);
          align-items: start;
        }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage .lead { line-height: var(--lh-relax); }
        .phone > div::-webkit-scrollbar { display: none; }
        .mai-cb-scroll::-webkit-scrollbar { display: none; }
        .mai-card-agent {
          outline: none;
        }
        .mai-card-agent:hover {
          transform: translateY(-2px);
          border-color: hsl(var(--c-agent) / .4) !important;
          box-shadow: 0 8px 24px hsl(var(--c-agent) / .18), var(--shadow-md);
        }
        .mai-card-agent:focus-visible {
          outline: 2px solid hsl(var(--c-agent));
          outline-offset: 2px;
        }
        select:focus-visible, input:focus-visible, button:focus-visible, a:focus-visible {
          outline: 2px solid hsl(var(--c-agent));
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
