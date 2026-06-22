/* ════════════════════════════════════════════════════════════════
   sp4-library-mobile.jsx
   Schermata : Library Mobile (variante mobile-first di sp4-library-desktop)
   Route     : /library  (breakpoint mobile <768px; tablet 768px adaptation)
   Descrizione: Hero compatto + tab semplificate (Games/Sessions/Chat +
                overflow Agents/KB) + grid 1-col MeepleCard list + sezione
                Recente + bulk-select via long-press + filters bottom-sheet.
   Brief      : SP8-mobile-parity. Content model riusato da SP4 desktop.
   ════════════════════════════════════════════════════════════════ */

const { useState, useEffect, useRef, useCallback } = React;

/* ─── entityHsl helper (inline, da brief) ─── */
const ENTITY_HSL = {
  game:    '25 95% 45%',  player:  '262 83% 58%',  session: '240 60% 55%',
  agent:   '38 92% 50%',  kb:      '174 60% 40%',   chat:    '220 80% 55%',
  event:   '350 89% 60%', toolkit: '142 70% 45%',   tool:    '195 80% 50%',
};
const entityHsl = (entity, alpha) =>
  alpha != null ? `hsl(${ENTITY_HSL[entity]} / ${alpha})` : `hsl(${ENTITY_HSL[entity]})`;

/* In dark mode i token entity cambiano: usiamo le CSS var live per coerenza. */
const eVar = (entity, alpha) =>
  alpha != null ? `hsl(var(--c-${entity}) / ${alpha})` : `hsl(var(--c-${entity}))`;

/* ─── Tab config (IA semplificata 3 + overflow) ─── */
const PRIMARY_TABS = [
  { id: 'game',    label: 'Games',    em: '🎲', count: 47 },
  { id: 'session', label: 'Sessions', em: '🎯', count: DS.sessions.length },
  { id: 'chat',    label: 'Chat',     em: '💬', count: DS.chats.length },
];
const OVERFLOW_TABS = [
  { id: 'agent', label: 'Agents', em: '🤖', count: DS.agents.length, hasNew: true },
  { id: 'kb',    label: 'KB',     em: '📄', count: DS.kbs.length,     hasNew: false },
];
const ALL_TABS = [...PRIMARY_TABS, ...OVERFLOW_TABS];

/* lista per tab */
const listFor = (tab) => ({
  game: DS.games, session: DS.sessions, chat: DS.chats, agent: DS.agents, kb: DS.kbs,
}[tab] || []);

/* ─── shape di una MeepleCard list ─── */
function cardData(item) {
  switch (item.type) {
    case 'game': return {
      cover: item.cover, em: item.coverEmoji, ti: item.title,
      sub: `${item.publisher} · ${item.year}`,
      metas: [
        { cls: 'star', ic: '★', txt: item.rating },
        { ic: '👥', txt: `${item.players} giocatori` },
      ],
      badge: item.badge,
    };
    case 'session': return {
      cover: item.cover, em: item.coverEmoji, ti: item.title, sub: item.subtitle,
      metas: [ { ic: '👥', txt: `${item.playerIds.length} giocatori` }, { ic: '📊', txt: `Turno ${item.turn}` } ],
      badge: item.badge,
    };
    case 'chat': return {
      cover: DS.color('chat'), em: '💬', ti: item.title, sub: item.subtitle,
      metas: [ { ic: '💬', txt: `${item.msgCount} msg` }, { ic: '🕒', txt: item.lastAt } ],
      badge: item.badge,
    };
    case 'agent': return {
      cover: item.cover, em: item.coverEmoji, ti: item.title, sub: item.subtitle,
      metas: [ { ic: '💬', txt: `${item.invocations} inv` }, { ic: '⚡', txt: item.avgLatency } ],
      badge: item.badge,
    };
    case 'kb': return {
      cover: item.cover, em: item.coverEmoji, ti: item.title, sub: item.subtitle,
      metas: [ { ic: '📄', txt: `${item.pages} pag` }, { ic: '💾', txt: item.size } ],
      badge: item.badge,
    };
    default: return { cover: item.cover, em: '📦', ti: item.title, sub: item.subtitle || '', metas: [], badge: item.badge };
  }
}

/* ─── connessioni (versione compatta da mobile-app.jsx) ─── */
function connectionsOf(e) {
  const out = [];
  const push = (type, items) => { if (items.length) out.push({ type, count: items.length, first: items[0] }); };
  if (e.type === 'game') {
    push('agent', DS.agents.filter(a => a.gameId === e.id));
    push('kb', DS.kbs.filter(k => k.gameId === e.id));
    push('chat', DS.chats.filter(c => c.gameId === e.id));
    push('session', DS.sessions.filter(s => s.gameId === e.id));
    push('toolkit', DS.toolkits.filter(t => t.gameId === e.id));
  } else if (e.type === 'session') {
    if (e.gameId) push('game', [DS.byId[e.gameId]].filter(Boolean));
    push('player', (e.playerIds || []).map(id => DS.byId[id]).filter(Boolean));
  } else if (e.type === 'chat') {
    if (e.agentId) push('agent', [DS.byId[e.agentId]].filter(Boolean));
    if (e.gameId) push('game', [DS.byId[e.gameId]].filter(Boolean));
  } else if (e.type === 'agent') {
    if (e.gameId) push('game', [DS.byId[e.gameId]].filter(Boolean));
    push('kb', DS.kbs.filter(k => k.gameId === e.gameId));
    push('chat', DS.chats.filter(c => c.agentId === e.id));
  } else if (e.type === 'kb') {
    if (e.gameId) push('game', [DS.byId[e.gameId]].filter(Boolean));
    push('agent', DS.agents.filter(a => a.gameId === e.gameId));
  }
  return out;
}

/* ─── Recente (cross-entity, stessa data del RecentActivityRail) ─── */
const RECENTS = [
  { type: 'game',    ref: 'g-catan',     pre: 'Hai aperto', ent: 'I Coloni di Catan', when: '2 ore fa' },
  { type: 'session', ref: 's-azul-live', pre: 'Sessione',   ent: 'Serata Azul in corso', when: 'ora · Turno 3/5' },
  { type: 'chat',    ref: 'c-catan-strat', pre: 'Chat con', ent: 'Catan Coach', when: 'Ieri' },
  { type: 'kb',      ref: 'kb-wingspan', pre: 'Indicizzato', ent: 'wingspan-rules.pdf', when: '2 giorni fa' },
];

/* ════════════════════════════════════════════════════════════════
   useLongPress — primitive greenfield (500ms)
   ════════════════════════════════════════════════════════════════ */
function useLongPress(onLongPress, ms = 500) {
  const timer = useRef(null);
  const fired = useRef(false);
  const [arming, setArming] = useState(null);

  const start = (id) => (e) => {
    if (e.button === 2) return;          // ignora tasto destro
    fired.current = false;
    setArming(id);
    timer.current = setTimeout(() => {
      fired.current = true;
      setArming(null);
      onLongPress(id);
    }, ms);
  };
  const cancel = () => { clearTimeout(timer.current); setArming(null); };
  const consume = () => { const f = fired.current; fired.current = false; return f; };
  useEffect(() => () => clearTimeout(timer.current), []);
  return { arming, start, cancel, consume };
}

/* ════════════════════════════════════════════════════════════════
   MeepleCard — variant="list"
   ════════════════════════════════════════════════════════════════ */
function MeepleCard({ item, bulkMode, selected, arming, bind, onTap }) {
  const d = cardData(item);
  return (
    <div
      className={`mcard${selected ? ' selected' : ''}${arming ? ' lp-arming' : ''}`}
      style={{ '--e': `var(--c-${item.type})` }}
      role="button" tabIndex={0}
      aria-pressed={bulkMode ? selected : undefined}
      aria-label={`${d.ti}${bulkMode ? (selected ? ' · selezionato' : ' · non selezionato') : ''}`}
      onMouseDown={bind.start(item.id)} onMouseUp={bind.cancel} onMouseLeave={bind.cancel}
      onTouchStart={bind.start(item.id)} onTouchEnd={bind.cancel} onTouchMove={bind.cancel}
      onContextMenu={(e) => e.preventDefault()}
      onClick={() => onTap(item)}
      onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onTap(item); } }}
    >
      <div className="cov" style={{ background: d.cover }}>{d.em}</div>
      <div className="body">
        <div className="title-row">
          <span className="ti">{d.ti}</span>
          {d.badge && <span className="e-chip2">{d.badge}</span>}
        </div>
        <div className="sub">{d.sub}</div>
        <div className="meta">
          {d.metas.map((m, i) => (
            <span key={i} className={`mi${m.cls ? ' ' + m.cls : ''}`}><span>{m.ic}</span>{m.txt}</span>
          ))}
        </div>
      </div>
      {bulkMode
        ? <div className="check">{selected ? '✓' : ''}</div>
        : <span className="chevron" aria-hidden="true">›</span>}
    </div>
  );
}

/* ════════════════════════════════════════════════════════════════
   Skeleton card (loading)
   ════════════════════════════════════════════════════════════════ */
function SkeletonCard() {
  return (
    <div className="sk">
      <div className="skcov shimmer"></div>
      <div className="skbody">
        <div className="skln shimmer" style={{ width: '62%', height: 13 }}></div>
        <div className="skln shimmer" style={{ width: '40%' }}></div>
        <div className="skln shimmer" style={{ width: '78%' }}></div>
      </div>
    </div>
  );
}

/* ════════════════════════════════════════════════════════════════
   Recente section
   ════════════════════════════════════════════════════════════════ */
function RecentSection({ onOpen }) {
  return (
    <div className="recent">
      <div className="rdiv"><span className="ln"></span><span className="tx">Recente</span><span className="ln"></span></div>
      {RECENTS.map((r, i) => {
        const ref = DS.byId[r.ref];
        return (
          <div key={i} className="rrow" style={{ '--e': `var(--c-${r.type})` }}
            role="button" tabIndex={0}
            onClick={() => ref && onOpen(ref)}
            onKeyDown={(e) => { if (e.key === 'Enter') ref && onOpen(ref); }}>
            <div className="ric">{DS.EC[r.type].em}</div>
            <div className="rtx">
              {r.pre} <span className="ent">{r.ent}</span>
              <span className="when">{r.when}</span>
            </div>
            <span className="rar" aria-hidden="true">→</span>
          </div>
        );
      })}
    </div>
  );
}

/* ════════════════════════════════════════════════════════════════
   Detail bottom-sheet (drawer V1 canonico — compatto)
   ════════════════════════════════════════════════════════════════ */
function DetailSheet({ entity, onClose, onOpen }) {
  const open = !!entity;
  const e = entity;
  const conns = e ? connectionsOf(e) : [];
  return (
    <React.Fragment>
      <div className={`overlay${open ? ' open' : ''}`} onClick={onClose}></div>
      <div className={`sheet${open ? ' open' : ''}`} role="dialog" aria-modal="true"
        aria-label={e ? e.title : 'Dettaglio'} style={e ? { '--e': `var(--c-${e.type})` } : null}>
        <div className="grab"></div>
        {e && (
          <React.Fragment>
            <div className="sh-head">
              <div className="cov" style={{ width: 36, height: 36, borderRadius: 9, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 18, color: '#fff', background: e.cover || eVar(e.type) }}>{e.coverEmoji || DS.EC[e.type].em}</div>
              <h3>{e.title}</h3>
              <button className="lh-icon" aria-label="Chiudi" onClick={onClose} style={{ width: 40, height: 40 }}>✕</button>
            </div>
            <div className="sh-body">
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginBottom: 16 }}>
                {conns.map(c => (
                  <button key={c.type} className="fopt" onClick={() => c.first && onOpen(c.first)}
                    style={{ borderColor: eVar(c.type, .4), background: eVar(c.type, .1), color: eVar(c.type) }}>
                    <span>{DS.EC[c.type].em}</span>{c.count} {DS.EC[c.type].lb}
                  </button>
                ))}
              </div>
              <div className="fgroup">
                <div className="fg-lbl">Apri in /library</div>
                <p style={{ fontSize: 13, color: 'var(--text-sec)', lineHeight: 1.55, margin: 0 }}>
                  Su mobile il tap apre il drawer entità a tutta scheda (bottom-sheet tabbed canonico). Le pip sopra sono <strong style={{ color: 'var(--text)' }}>EntityPip</strong> tappabili che sostituiscono l'entità in vista.
                </p>
              </div>
            </div>
            <div className="sh-foot">
              <button className="btn-pri" style={{ '--e': `var(--c-${e.type})` }} onClick={onClose}>Apri scheda</button>
              <button className="btn-sec" onClick={onClose}>Chiudi</button>
            </div>
          </React.Fragment>
        )}
      </div>
    </React.Fragment>
  );
}

/* ════════════════════════════════════════════════════════════════
   Filters bottom-sheet 80vh (AdvancedFiltersDrawer, anchor=bottom)
   ════════════════════════════════════════════════════════════════ */
const FILTER_GROUPS = [
  { key: 'sort', label: 'Ordina per', opts: ['Recenti', 'A → Z', 'Rating', 'Più giocati'] },
  { key: 'status', label: 'Stato', opts: ['Posseduti', 'Wishlist', 'Preferiti'] },
  { key: 'players', label: 'Giocatori', opts: ['2', '3–4', '5+'] },
  { key: 'weight', label: 'Complessità', opts: ['Leggero', 'Medio', 'Pesante'] },
];
function FiltersSheet({ open, draft, onToggle, onReset, onApply, onClose }) {
  return (
    <React.Fragment>
      <div className={`overlay${open ? ' open' : ''}`} onClick={onClose}></div>
      <div className={`sheet tall${open ? ' open' : ''}`} role="dialog" aria-modal="true" aria-label="Filtri avanzati">
        <div className="grab"></div>
        <div className="sh-head">
          <h3>Filtri</h3>
          <button className="reset" onClick={onReset}>Resetta</button>
        </div>
        <div className="sh-body">
          {FILTER_GROUPS.map(g => (
            <div className="fgroup" key={g.key}>
              <div className="fg-lbl">{g.label}</div>
              <div className="fopts">
                {g.opts.map(o => {
                  const on = (draft[g.key] || []).includes(o);
                  return (
                    <button key={o} className="fopt" aria-pressed={on}
                      onClick={() => onToggle(g.key, o)}>
                      {on && <span aria-hidden="true">✓</span>}{o}
                    </button>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
        <div className="sh-foot">
          <button className="btn-sec" onClick={onClose}>Annulla</button>
          <button className="btn-pri" onClick={onApply}>Applica filtri</button>
        </div>
      </div>
    </React.Fragment>
  );
}

/* ════════════════════════════════════════════════════════════════
   Bulk actions bottom-sheet (FAB → Archivia / Tag / Esporta)
   ════════════════════════════════════════════════════════════════ */
function BulkActionsSheet({ open, count, onClose, onAction }) {
  const acts = [
    { id: 'archive', em: '📦', e: 'toolkit', lb: 'Archivia', sub: `Sposta ${count} in archivio` },
    { id: 'tag', em: '🏷️', e: 'player', lb: 'Aggiungi tag', sub: 'Etichetta la selezione' },
    { id: 'export', em: '📤', e: 'chat', lb: 'Esporta', sub: 'Condividi o esporta lista' },
    { id: 'remove', em: '🗑️', e: 'event', lb: 'Rimuovi dalla libreria', sub: null, danger: true },
  ];
  return (
    <React.Fragment>
      <div className={`overlay${open ? ' open' : ''}`} onClick={onClose}></div>
      <div className={`sheet${open ? ' open' : ''}`} role="dialog" aria-modal="true" aria-label="Azioni selezione">
        <div className="grab"></div>
        <div className="sh-head"><h3>{count} selezionati</h3>
          <button className="lh-icon" aria-label="Chiudi" onClick={onClose} style={{ width: 40, height: 40 }}>✕</button></div>
        <div className="sh-body">
          <div className="bulkacts">
            {acts.map(a => (
              <button key={a.id} className={`bulkact${a.danger ? ' danger' : ''}`} style={{ '--e': `var(--c-${a.e})` }}
                onClick={() => onAction(a.lb)}>
                <span className="bi">{a.em}</span>
                <span>{a.lb}{a.sub && <span className="bsub">{a.sub}</span>}</span>
              </button>
            ))}
          </div>
        </div>
      </div>
    </React.Fragment>
  );
}

/* ════════════════════════════════════════════════════════════════
   LibraryMobile — schermata principale
   ════════════════════════════════════════════════════════════════ */
function LibraryMobile({ condition, isTablet }) {
  const [tab, setTab] = useState('game');
  const [query, setQuery] = useState('');
  const [overflowOpen, setOverflowOpen] = useState(false);
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [filterDraft, setFilterDraft] = useState({});
  const [activeFilters, setActiveFilters] = useState(
    condition === 'filtered' ? [{ key: 'weight', label: 'Pesante' }, { key: 'players', label: '5+' }] : []
  );
  const [visible, setVisible] = useState(6);
  const [bulkMode, setBulkMode] = useState(false);
  const [selected, setSelected] = useState(() => new Set());
  const [bulkSheet, setBulkSheet] = useState(false);
  const [detail, setDetail] = useState(null);
  const [toast, setToast] = useState('');
  const toastTimer = useRef(null);

  const offline = condition === 'offline';
  const readOnly = offline;

  const showToast = (msg) => {
    setToast(msg);
    clearTimeout(toastTimer.current);
    toastTimer.current = setTimeout(() => setToast(''), 2200);
  };

  const enterBulk = (id) => {
    if (readOnly) { showToast('Non disponibile offline'); return; }
    setBulkMode(true);
    setSelected(new Set([id]));
  };
  const lp = useLongPress(enterBulk, 500);

  const exitBulk = () => { setBulkMode(false); setSelected(new Set()); setBulkSheet(false); };

  const onTapCard = (item) => {
    if (lp.consume()) return;            // long-press ha già agito
    if (bulkMode) {
      setSelected(prev => {
        const n = new Set(prev);
        n.has(item.id) ? n.delete(item.id) : n.add(item.id);
        if (n.size === 0) setBulkMode(false);
        return n;
      });
      return;
    }
    setDetail(item);
  };

  const switchTab = (id) => { setTab(id); setVisible(6); setQuery(''); setOverflowOpen(false); };

  // filtra lista per query
  const base = listFor(tab);
  const filtered = query
    ? base.filter(e => e.title.toLowerCase().includes(query.toLowerCase()))
    : base;
  const shown = filtered.slice(0, visible);
  const hasMore = visible < filtered.length;

  const toggleDraft = (key, opt) => setFilterDraft(prev => {
    const cur = new Set(prev[key] || []);
    cur.has(opt) ? cur.delete(opt) : cur.add(opt);
    return { ...prev, [key]: [...cur] };
  });
  const applyFilters = () => {
    const next = [];
    Object.entries(filterDraft).forEach(([k, arr]) => arr.forEach(label => next.push({ key: k, label })));
    setActiveFilters(next);
    setFiltersOpen(false);
    showToast(next.length ? `${next.length} filtri applicati` : 'Filtri rimossi');
  };
  const removeFilter = (idx) => setActiveFilters(prev => prev.filter((_, i) => i !== idx));
  const resetFiltersToZero = () => { setActiveFilters([]); setFilterDraft({}); };

  const activeTabMeta = ALL_TABS.find(t => t.id === tab);

  /* ── derived: la lista è "vuota da filtro"? ── */
  const filteredEmpty = condition === 'filtered';

  /* tabs da mostrare: tablet mostra tutte e 5, mobile 3 + overflow */
  const tabsToShow = isTablet ? ALL_TABS : PRIMARY_TABS;

  return (
    <div className="app">
      {offline && (
        <div className="offbar" role="status">
          <span className="od"></span>Sei offline — alcuni dati potrebbero non essere aggiornati
        </div>
      )}

      {/* ─── HEADER (bulk bar sostituisce header quando attivo) ─── */}
      {bulkMode ? (
        <div className="bulkbar">
          <button className="bb-x" onClick={exitBulk}>← Annulla</button>
          <span className="bb-count">{selected.size} selezionat{selected.size === 1 ? 'o' : 'i'}</span>
          <button className="bb-kebab" aria-label="Azioni sulla selezione" onClick={() => setBulkSheet(true)}>⋮</button>
        </div>
      ) : (
        <header className="lh-header">
          <div className="lh-bar">
            <div className="lh-mark">M</div>
            <div className="lh-titles">
              <div className="t">{condition === 'permission' ? 'Libreria di Sara' : 'La mia libreria'}</div>
              <div className="s">
                {condition === 'permission' ? 'Condivisa · privata' : `${activeTabMeta.count} ${activeTabMeta.label.toLowerCase()}`}
              </div>
            </div>
            <button className="lh-icon" aria-label="Menu libreria">☰</button>
          </div>

          {condition !== 'permission' && (
            <React.Fragment>
              <div className="lh-tabs" role="tablist" aria-label="Categorie libreria">
                {tabsToShow.map(t => (
                  <button key={t.id} className="lh-tab" role="tab"
                    aria-selected={tab === t.id}
                    style={{ '--e': `var(--c-${t.id})` }}
                    onClick={() => switchTab(t.id)}>
                    {t.label}<span className="cnt">{t.count}</span>
                  </button>
                ))}
                {!isTablet && (
                  <button className={`lh-more${overflowOpen ? ' active-overflow' : ''}`}
                    aria-label="Più categorie: Agents e KB" aria-haspopup="menu" aria-expanded={overflowOpen}
                    onClick={() => setOverflowOpen(o => !o)}>
                    ⋯{OVERFLOW_TABS.some(t => t.hasNew) && <span className="ndot"></span>}
                  </button>
                )}
              </div>

              <div className="lh-search">
                <div className="field">
                  <span className="ic" aria-hidden="true">🔍</span>
                  <input value={query} onChange={e => { setQuery(e.target.value); setVisible(6); }}
                    placeholder={`Cerca in ${activeTabMeta.label}…`} aria-label={`Cerca in ${activeTabMeta.label}`} />
                </div>
                <button className={`lh-filter${activeFilters.length ? ' has-active' : ''}`}
                  aria-label="Filtri avanzati" onClick={() => !readOnly && setFiltersOpen(true)}>
                  <span aria-hidden="true">⛃</span>Filtri
                  {activeFilters.length > 0 && <span className="badge">{activeFilters.length}</span>}
                </button>
              </div>
            </React.Fragment>
          )}
        </header>
      )}

      {/* ─── BODY ─── */}
      <div className="app-scroll">
        {/* LOADING */}
        {condition === 'loading' && (
          <div className="lh-body" aria-live="polite" aria-busy="true">
            <span className="sr-only">Caricamento libreria…</span>
            <div className="mlist">
              <SkeletonCard /><SkeletonCard /><SkeletonCard />
            </div>
          </div>
        )}

        {/* ERROR */}
        {condition === 'error' && (
          <div className="lh-body">
            <div className="ebanner" role="alert">
              <span className="eic">⚠️</span>
              <div className="ebody">
                <h4>Impossibile caricare la libreria</h4>
                <p>Si è verificato un errore di rete. Riprova o consulta i dati in cache.</p>
                <div className="eacts">
                  <button className="retry" onClick={() => showToast('Nuovo tentativo…')}>↻ Riprova</button>
                  <button className="cache" onClick={() => showToast('Mostro dati in cache')}>Mostra cache</button>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* PERMISSION DENIED */}
        {condition === 'permission' && (
          <div className="estate" style={{ '--e': 'var(--c-player)' }}>
            <div className="ill">🔒</div>
            <h3>Non hai accesso</h3>
            <p>La libreria di Sara T. è privata. Chiedi un invito per consultare i suoi giochi e le sessioni.</p>
            <div className="acts">
              <button className="btn-pri" style={{ '--e': 'var(--c-player)' }} onClick={() => showToast('Richiesta inviata a Sara T.')}>✉️ Chiedi un invito</button>
              <button className="btn-sec" onClick={() => showToast('Torna alla tua libreria')}>Torna alla mia libreria</button>
            </div>
          </div>
        )}

        {/* EMPTY (collezione vuota) */}
        {condition === 'empty' && (
          <div className="estate" style={{ '--e': 'var(--c-game)' }}>
            <div className="ill">🎲</div>
            <h3>Nessun gioco ancora</h3>
            <p>Aggiungi il primo! La tua libreria raccoglie giochi, sessioni e chat in un unico posto.</p>
            <div className="acts">
              <button className="btn-pri" onClick={() => showToast('Vai a /library/add')}>＋ Aggiungi gioco</button>
              <button className="btn-sec" onClick={() => showToast('Vai a /shared-games')}>🔎 Scopri shared</button>
            </div>
          </div>
        )}

        {/* FILTERED EMPTY */}
        {filteredEmpty && (
          <div className="lh-body">
            <div className="filterchips">
              <span className="lbl">Filtri attivi</span>
              {activeFilters.map((f, i) => (
                <button key={i} className="fchip" onClick={() => removeFilter(i)}>
                  {f.label}<span className="x" aria-hidden="true">✕</span>
                </button>
              ))}
            </div>
            <div className="estate" style={{ '--e': 'var(--c-game)', paddingTop: 24 }}>
              <div className="ill">🔍</div>
              <h3>Nessun risultato</h3>
              <p>Nessun gioco corrisponde a questi filtri. Prova a rimuoverne qualcuno.</p>
              <div className="acts">
                <button className="btn-pri" onClick={resetFiltersToZero}>Resetta filtri</button>
              </div>
            </div>
          </div>
        )}

        {/* DEFAULT + OFFLINE (lista normale) */}
        {(condition === 'default' || condition === 'offline') && (
          <div className="lh-body">
            {activeFilters.length > 0 && (
              <div className="filterchips">
                <span className="lbl">Filtri</span>
                {activeFilters.map((f, i) => (
                  <button key={i} className="fchip" onClick={() => removeFilter(i)}>
                    {f.label}<span className="x" aria-hidden="true">✕</span>
                  </button>
                ))}
              </div>
            )}

            {shown.length === 0 ? (
              <div className="estate" style={{ '--e': `var(--c-${tab})`, paddingTop: 24 }}>
                <div className="ill">🔍</div>
                <h3>Nessun risultato</h3>
                <p>Nessun elemento per «{query}». Prova un altro termine.</p>
              </div>
            ) : (
              <div className="mlist">
                {shown.map(item => (
                  <MeepleCard key={item.id} item={item}
                    bulkMode={bulkMode} selected={selected.has(item.id)}
                    arming={lp.arming === item.id} bind={lp} onTap={onTapCard} />
                ))}
                {hasMore ? (
                  <div className="loadmore">
                    <button onClick={() => setVisible(v => v + 6)}>
                      ↓ Carica altri · {shown.length} di {activeTabMeta.count}
                    </button>
                  </div>
                ) : (
                  <div className="loadmore">
                    <div className="done">{filtered.length} di {activeTabMeta.count} caricati</div>
                  </div>
                )}
              </div>
            )}

            {/* Sezione Recente: solo tab Games, no bulk, no ricerca attiva */}
            {tab === 'game' && !bulkMode && !query && <RecentSection onOpen={setDetail} />}
          </div>
        )}
      </div>

      {/* ─── FAB (bulk mode) ─── */}
      {bulkMode && (
        <button className="fab" aria-label="Azioni sulla selezione" onClick={() => setBulkSheet(true)}>⋮</button>
      )}

      {/* ─── Overflow menu (Agents / KB) ─── */}
      {overflowOpen && !isTablet && (
        <React.Fragment>
          <div className="overlay open" style={{ background: 'transparent' }} onClick={() => setOverflowOpen(false)}></div>
          <div className="ovmenu" role="menu" aria-label="Più categorie">
            <div className="ov-lbl">Più categorie</div>
            {OVERFLOW_TABS.map(t => (
              <button key={t.id} className="ovitem" role="menuitem" style={{ '--e': `var(--c-${t.id})` }}
                onClick={() => switchTab(t.id)}>
                <span className="ovic">{t.em}</span>{t.label}
                {t.hasNew && <span className="ovdot" title="Nuove entries"></span>}
                <span className="ovcnt">{t.count}</span>
              </button>
            ))}
          </div>
        </React.Fragment>
      )}

      {/* ─── Sheets & toast ─── */}
      <FiltersSheet open={filtersOpen} draft={filterDraft}
        onToggle={toggleDraft} onReset={() => setFilterDraft({})}
        onApply={applyFilters} onClose={() => setFiltersOpen(false)} />
      <BulkActionsSheet open={bulkSheet} count={selected.size}
        onClose={() => setBulkSheet(false)}
        onAction={(lb) => { setBulkSheet(false); exitBulk(); showToast(`${lb} · ${selected.size} elementi`); }} />
      <DetailSheet entity={detail} onClose={() => setDetail(null)} onOpen={setDetail} />

      <div className={`toast${toast ? ' show' : ''}`} role="status">{toast}</div>
    </div>
  );
}

/* ════════════════════════════════════════════════════════════════
   Device frame
   ════════════════════════════════════════════════════════════════ */
function Device({ tablet, condition, label }) {
  return (
    <div className="frame-col">
      <div className="frame-tag">{label} · <b>{tablet ? '768px tablet' : '375px mobile'}</b></div>
      <div className={`device${tablet ? ' tablet' : ''}`}>
        <div className="screen">
          <div className="sbar">
            <span>14:32</span>
            <div className="ind"><span>📶</span><span>🔋</span></div>
          </div>
          <LibraryMobile condition={condition} isTablet={tablet} />
        </div>
      </div>
    </div>
  );
}

/* ════════════════════════════════════════════════════════════════
   Root — control bar (tema + stato) e frames mobile + tablet
   ════════════════════════════════════════════════════════════════ */
const STATES = [
  { id: 'default', lb: 'Default' },
  { id: 'empty', lb: 'Vuoto' },
  { id: 'loading', lb: 'Loading' },
  { id: 'error', lb: 'Errore' },
  { id: 'permission', lb: 'Permesso' },
  { id: 'offline', lb: 'Offline' },
  { id: 'filtered', lb: 'Filtrato' },
];

function Root() {
  const [theme, setTheme] = useState(() => localStorage.getItem('mai-theme') || 'light');
  const [condition, setCondition] = useState('default');

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('mai-theme', theme);
  }, [theme]);

  return (
    <div className="lab">
      <div className="lab-head">
        <div className="kick">SP8 · Mobile Parity</div>
        <h1>Library <span style={{ color: 'hsl(var(--c-game))' }}>mobile</span></h1>
        <p>Variante mobile-first di <code>/library</code>. IA semplificata: 3 tab + overflow. Lo stato <strong>Default</strong> è interattivo — tab, ricerca, long-press 500ms per selezione multipla, menu ⋯, filtri 80vh.</p>
      </div>

      <div className="controls" role="toolbar" aria-label="Controlli anteprima">
        <div className="ctl-grp">
          <span className="ctl-lbl">Stato</span>
          {STATES.map(s => (
            <button key={s.id} className={`seg${condition === s.id ? ' on' : ''}`}
              aria-pressed={condition === s.id} onClick={() => setCondition(s.id)}>{s.lb}</button>
          ))}
        </div>
        <div className="ctl-grp">
          <span className="ctl-lbl">Tema</span>
          {['light', 'dark'].map(t => (
            <button key={t} className={`seg theme${theme === t ? ' on' : ''}`}
              aria-pressed={theme === t} onClick={() => setTheme(t)}>{t === 'light' ? '☀︎ Light' : '☾ Dark'}</button>
          ))}
        </div>
      </div>

      <div className="stage-frames">
        <Device key={'m' + condition} tablet={false} condition={condition} label="Mobile" />
        <Device key={'t' + condition} tablet={true} condition={condition} label="Tablet" />
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<Root />);
