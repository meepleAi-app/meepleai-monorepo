/* sp4-toolkit-templates-ui.jsx — components + interactive app + harness
   (loads after sp4-toolkit-templates.jsx). Route: /toolkit/templates — vedi header foundation. */

const { useState, useEffect, useMemo, useRef, useCallback } = React;
const DS = window.DS;
const T = window.__TT;
const {
  eHsl, sem, fmtDate, relDate, NOW,
  CATS, CAT_ORDER, CAT_COUNTS, TOOL_META, TOOL_ORDER, CREATORS,
  TEMPLATES, TOTAL, CLONE_TOTAL, LIB_GAMES,
} = T;

const pColor = (h, l = 52, s = 58) => `hsl(${h}, ${s}%, ${l}%)`;
const entVar = ent => ent === 'success' ? 'var(--c-success)' : `var(--c-${ent})`;
// resolve a category's --e custom prop value
const catE = cat => `hsl(${entVar(CATS[cat].ent)})`;

// ═══════════════════════════════════════════════════════
// ─── PRIMITIVES ─────────────────────────────────────
// ═══════════════════════════════════════════════════════
const StarRating = ({ value }) => {
  const full = Math.floor(value);
  const half = value - full >= 0.5;
  return (
    <span className="tt-rating" role="img" aria-label={`Valutazione ${value} stelle su 5`}>
      {[0, 1, 2, 3, 4].map(i => {
        const cls = i < full ? 'full' : (i === full && half ? 'half' : '');
        return <span key={i} className={'tt-star ' + cls} aria-hidden="true">★</span>;
      })}
    </span>
  );
};

const ToolComposition = ({ tools }) => {
  const label = TOOL_ORDER.map(k => `${tools[k]} ${TOOL_META[k].label}`).join(', ');
  return (
    <div className="tt-tools" role="img" aria-label={`Composizione: ${label}`}>
      {TOOL_ORDER.map(k => {
        const c = tools[k], empty = c === 0;
        return (
          <div key={k} className={'tt-tool' + (empty ? ' empty' : '')} aria-hidden="true">
            <span className="ti">{TOOL_META[k].icon}</span>
            <span className="tc">{empty ? '—' : c}</span>
            {!empty && <span className="tip">{TOOL_META[k].tip(c)}</span>}
          </div>
        );
      })}
    </div>
  );
};

const CreatorChip = ({ creator }) => (
  <span className="tt-creator" title={`Creato da ${creator.name}`}>
    <span className="tt-cav" style={{ background: pColor(creator.color) }} aria-hidden="true">{creator.initials}</span>
    <span className="tt-cnm">{creator.name}</span>
  </span>
);

// ═══════════════════════════════════════════════════════
// ─── TEMPLATE CARD ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const TemplateCard = ({ t, onClone }) => {
  const cat = CATS[t.category];
  const eStyle = { '--e': catE(t.category) };
  const cls = 'tt-card' + (t.official ? ' official' : '') + (t.pending ? ' pending' : '');
  return (
    <article className={cls} style={eStyle} role="listitem" aria-labelledby={'tplname-' + t.id}>
      {t.official && <span className="tt-crown" title="Template ufficiale" aria-hidden="true">👑</span>}
      {t.recent && <span className="tt-corner new" aria-hidden="true">Nuovo</span>}

      {/* header */}
      <div className="tt-chead">
        <span className="tt-cicon" aria-hidden="true">{cat.icon}</span>
        <div className="tt-chtxt">
          <button type="button" className="tt-cname" id={'tplname-' + t.id} title={t.name}
            aria-label={`Apri dettaglio template ${t.name}`}>{t.name}</button>
          <div className="tt-ccat">{cat.label}</div>
        </div>
        {t.rating > 0
          ? <StarRating value={t.rating} />
          : <span className="tt-ratenew" aria-label="Nessuna valutazione">nuovo</span>}
      </div>

      {/* description */}
      <p className="tt-desc">{t.description}</p>

      {/* tool composition */}
      <ToolComposition tools={t.tools} />

      <span className="tt-spacer" />
      <div className="tt-cdiv" />

      {/* creator + meta */}
      <div className="tt-meta">
        <CreatorChip creator={t.creator} />
        {t.official && <span className="tt-official"><span aria-hidden="true">⭐</span>Ufficiale</span>}
        <span className="tt-clones" title={`${t.usageCount} cloni`}>
          {t.popular && <span className="fire" aria-hidden="true">🔥</span>}
          <span aria-hidden="true">📋</span>{t.usageCount}
        </span>
      </div>
      {t.popular && <div className="tt-grow" aria-label={`+${t.weekGrowth} cloni questa settimana`}>↗ +{t.weekGrowth} questa settimana</div>}

      {/* footer CTA */}
      {t.pending
        ? <button type="button" className="tt-clonebtn disabled" disabled aria-label="Template in approvazione, non clonabile">
            <span aria-hidden="true">🔒</span>In approvazione</button>
        : <button type="button" className="tt-clonebtn" onClick={() => onClone(t)} aria-label={`Clona template ${t.name}`}>
            <span aria-hidden="true">📋</span>Clona template</button>}
    </article>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TOOLBAR ────────────────────────────────────────
// ═══════════════════════════════════════════════════════
function useOutside(onClose) {
  const ref = useRef(null);
  useEffect(() => {
    const onDoc = e => { if (ref.current && !ref.current.contains(e.target)) onClose(); };
    const onEsc = e => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('mousedown', onDoc);
    document.addEventListener('keydown', onEsc);
    return () => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onEsc); };
  }, [onClose]);
  return ref;
}

const SORT_OPTS = [
  { id: 'popular', label: 'Più popolari' },
  { id: 'recent', label: 'Più recenti' },
  { id: 'rating', label: 'Più amati' },
  { id: 'alpha', label: 'Alfabetico' },
];

const Toolbar = ({ st, set, typing, searchRef, mobile }) => {
  const [openSort, setOpenSort] = useState(false);
  const sortRef = useOutside(() => setOpenSort(false));
  const chips = [{ key: 'all', label: 'Tutte', count: TOTAL }, ...CAT_ORDER.map(c => ({ key: c, label: CATS[c].label, count: CAT_COUNTS[c], cat: c }))];

  return (
    <div className="tt-toolbar">
      {/* search */}
      <div className={'tt-search' + (st.search ? ' active' : '')}>
        <span className="ic" aria-hidden="true">🔍</span>
        <input ref={searchRef} value={st.search} onChange={e => set({ search: e.target.value })} role="searchbox"
          aria-label="Cerca template per nome o creatore" placeholder="Cerca per nome o creatore…"
          onKeyDown={e => { if (e.key === 'Escape') set({ search: '' }); }} />
        {typing && <span className="busy" aria-hidden="true"><i />Cercando…</span>}
        {st.search && <button className="clear" aria-label="Cancella ricerca" onClick={() => { set({ search: '' }); searchRef.current && searchRef.current.focus(); }}>✕</button>}
      </div>

      {/* category chips — single-select radiogroup */}
      <div className={'tt-chips' + (mobile ? ' scroll' : '')} role="radiogroup" aria-label="Filtra per categoria">
        {chips.map(c => {
          const on = st.cat === c.key;
          const isAll = c.key === 'all';
          const eStyle = isAll ? undefined : { '--e': catE(c.cat) };
          return (
            <button key={c.key} type="button" role="radio" aria-checked={on}
              className={'tt-chip ' + (isAll ? 'all' : 'cat') + (on ? ' on' : '')} style={eStyle}
              onClick={() => set({ cat: c.key })}>
              {isAll ? <span className="cdot" aria-hidden="true" /> : <span aria-hidden="true">{CATS[c.cat].icon}</span>}
              {c.label}<span className="ccount">{c.count}</span>
            </button>
          );
        })}
      </div>

      {/* sort + view */}
      <div className="tt-right">
        <div className="tt-fgroup" ref={sortRef}>
          <button type="button" className="tt-sortbtn" aria-haspopup="listbox" aria-expanded={openSort} onClick={() => setOpenSort(o => !o)}>
            <span aria-hidden="true">↕</span>{SORT_OPTS.find(s => s.id === st.sort).label}
            <span aria-hidden="true" style={{ fontSize: 8, opacity: .7 }}>▼</span>
          </button>
          {openSort && (
            <div className="tt-pop" role="listbox" aria-label="Ordina template">
              <div className="tt-pophead">Ordina per</div>
              {SORT_OPTS.map(o => (
                <button key={o.id} type="button" role="option" aria-selected={st.sort === o.id}
                  className={'tt-popitem' + (st.sort === o.id ? ' sel' : '')} onClick={() => { set({ sort: o.id }); setOpenSort(false); }}>
                  <span className="check" aria-hidden="true">{st.sort === o.id ? '●' : ''}</span>
                  <span className="lbl">{o.label}</span>
                </button>
              ))}
            </div>
          )}
        </div>
        {!mobile && (
          <div className="tt-vtoggle" role="group" aria-label="Densità griglia">
            <button aria-pressed={st.cols === 4} onClick={() => set({ cols: 4 })} title="Griglia 4 colonne" aria-label="Griglia 4 colonne">▦</button>
            <button aria-pressed={st.cols === 3} onClick={() => set({ cols: 3 })} title="Griglia 3 colonne" aria-label="Griglia 3 colonne">▥</button>
            <button aria-pressed={st.cols === 'list'} onClick={() => set({ cols: 'list' })} title="Vista lista" aria-label="Vista lista">☰</button>
          </div>
        )}
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── EMPTY / LOADING / ERROR ────────────────────────
// ═══════════════════════════════════════════════════════
const FilterEmpty = ({ onClear }) => (
  <div className="tt-pad" aria-live="polite">
    <div className="tt-empty">
      <div className="em" aria-hidden="true">🔍</div>
      <h3>Nessun template corrisponde ai filtri</h3>
      <p>Prova a cambiare categoria o a modificare la ricerca per nome o creatore.</p>
      <button className="cta" onClick={onClear}>Cancella filtri</button>
    </div>
  </div>
);

const Sk = ({ w, h, r = 'var(--r-sm)', style }) => <div className="tt-sk th-shimmer tt-shimmer" style={{ width: w, height: h, borderRadius: r, ...style }} />;
const SkeletonGrid = ({ cols }) => (
  <div className="tt-grid" style={{ '--cols': cols }} aria-busy="true">
    {Array.from({ length: cols === 3 ? 6 : 8 }).map((_, i) => (
      <div className="tt-skcard" key={i}>
        <div style={{ display: 'flex', gap: 10 }}>
          <Sk w={30} h={30} r="var(--r-sm)" />
          <div style={{ flex: 1 }}><Sk w="70%" h={15} style={{ marginBottom: 6 }} /><Sk w="40%" h={10} /></div>
          <Sk w={66} h={12} />
        </div>
        <Sk w="100%" h={11} /><Sk w="82%" h={11} />
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4,1fr)', gap: 7 }}>
          {[0, 1, 2, 3].map(j => <Sk key={j} w="100%" h={44} r="var(--r-sm)" />)}
        </div>
        <div style={{ flex: 1 }} />
        <Sk w="100%" h={1} />
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}><Sk w={96} h={24} r="var(--r-pill)" /><Sk w={42} h={12} style={{ marginLeft: 'auto' }} /></div>
        <Sk w="100%" h={38} r="var(--r-md)" />
      </div>
    ))}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── CLONE MODAL ────────────────────────────────────
// ═══════════════════════════════════════════════════════
const CloneModal = ({ t, mobile, onClose }) => {
  const cat = CATS[t.category];
  const closeRef = useRef(null);
  const modalRef = useRef(null);
  const [target, setTarget] = useState(LIB_GAMES[0] ? LIB_GAMES[0].id : '');
  const [name, setName] = useState(`${t.name} (copia)`);
  const [keepRatings, setKeepRatings] = useState(false);
  const [editAfter, setEditAfter] = useState(true);

  useEffect(() => { closeRef.current && closeRef.current.focus(); }, []);
  useEffect(() => {
    const onKey = e => {
      if (e.key === 'Escape') { onClose(); return; }
      if (e.key === 'Tab' && modalRef.current) {
        const f = modalRef.current.querySelectorAll('button, input, select, [tabindex]:not([tabindex="-1"])');
        if (!f.length) return;
        const first = f[0], last = f[f.length - 1];
        if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
        else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose]);

  const toolSummary = TOOL_ORDER.filter(k => t.tools[k] > 0)
    .map(k => `${TOOL_META[k].icon} ${t.tools[k]} ${t.tools[k] === 1 ? TOOL_META[k].one : TOOL_META[k].label}`);

  return (
    <div className="tt-overlay" onMouseDown={e => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="tt-modal" ref={modalRef} role="dialog" aria-modal="true" aria-labelledby="tt-mtitle" style={{ '--e': catE(t.category) }}>
        <div className="tt-mhead">
          <span className="tt-mcov" aria-hidden="true">{cat.icon}</span>
          <div className="tt-mhtxt">
            <div className="tt-mtitle" id="tt-mtitle">Clona “{t.name}”</div>
            <div className="tt-msub">{cat.label} · creato da {t.creator.name}{t.official ? ' · ⭐ ufficiale' : ''}</div>
          </div>
          <button type="button" className="tt-mclose" ref={closeRef} aria-label="Chiudi" onClick={onClose}>✕</button>
        </div>

        <div className="tt-mbody">
          <div className="tt-field">
            <label htmlFor="tt-target">Gioco di destinazione</label>
            <select id="tt-target" value={target} onChange={e => setTarget(e.target.value)}>
              {LIB_GAMES.map(g => <option key={g.id} value={g.id}>{g.coverEmoji} {g.title}</option>)}
            </select>
          </div>

          <div className="tt-field">
            <label htmlFor="tt-name">Nome del nuovo toolkit</label>
            <input id="tt-name" type="text" value={name} onChange={e => setName(e.target.value)} placeholder="Es. Catan — setup serata" />
          </div>

          <div className="tt-summary">
            <div className="sh">Strumenti inclusi</div>
            <div className="stools">
              {toolSummary.map((s, i) => <span key={i} className="stool">{s}</span>)}
            </div>
          </div>

          <button type="button" className={'tt-toggle' + (editAfter ? ' on' : '')} role="switch" aria-checked={editAfter} onClick={() => setEditAfter(v => !v)}>
            <span className="tlbl"><div className="tt">Apri l’editor dopo la clonazione</div><div className="td">Personalizza tool e parametri subito dopo</div></span>
            <span className="tt-sw" aria-hidden="true" />
          </button>

          <button type="button" className={'tt-toggle' + (keepRatings ? ' on' : '')} role="switch" aria-checked={keepRatings} onClick={() => setKeepRatings(v => !v)}>
            <span className="tlbl"><div className="tt">Mantieni i valori predefiniti</div><div className="td">Conserva durate timer e soglie contatori originali</div></span>
            <span className="tt-sw" aria-hidden="true" />
          </button>
        </div>

        <div className="tt-mfoot">
          <button type="button" className="tt-mbtn" onClick={onClose}>Annulla</button>
          <button type="button" className="tt-mbtn tk"><span aria-hidden="true">📋</span>Conferma clonazione</button>
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── HEADER (sticky + tabs) ─────────────────────────
// ═══════════════════════════════════════════════════════
const TABS = [
  { id: 'stats', icon: '📊', label: 'Stats', href: 'sp4-toolkit-stats.html' },
  { id: 'history', icon: '📜', label: 'History', href: 'sp4-toolkit-history.html' },
  { id: 'templates', icon: '🎨', label: 'Templates', href: null },
  { id: 'play', icon: '🎮', label: 'Play', href: null },
];
const Header = ({ mobile, onCreate }) => (
  <header className="tt-head">
    <div className="tt-htop">
      <div className="tt-htxt">
        <div className="tt-bread"><span>Toolkit</span><span className="sep" aria-hidden="true">›</span><span className="cur">Templates</span></div>
        <div className="tt-titlerow">
          <span className="tt-ico" aria-hidden="true">🧰</span>
          <h1 className="tt-h1">Template toolkit</h1>
        </div>
        {!mobile && <p className="tt-sub">Esplora i template approvati e clonali per il tuo gioco.</p>}
      </div>
      <div className="tt-hright">
        <span className="tt-qstat"><b>{TOTAL}</b> template<span aria-hidden="true">·</span><b>{CAT_ORDER.length}</b> categorie<span aria-hidden="true">·</span><b>{CLONE_TOTAL}</b> cloni</span>
        <button type="button" className="tt-cta" onClick={onCreate}><span aria-hidden="true">+</span>{mobile ? 'Crea' : 'Crea template'}</button>
      </div>
    </div>
    <nav className="tt-tabs" role="tablist" aria-label="Sezioni toolkit">
      {TABS.map(t => {
        const on = t.id === 'templates';
        const cls = 'tt-tab' + (on ? ' on' : '');
        const inner = <React.Fragment><span aria-hidden="true">{t.icon}</span>{t.label}</React.Fragment>;
        return t.href
          ? <a key={t.id} href={t.href} className={cls} role="tab" aria-selected={false}>{inner}</a>
          : <button key={t.id} type="button" role="tab" aria-selected={on} className={cls}>{inner}</button>;
      })}
    </nav>
  </header>
);

// ═══════════════════════════════════════════════════════
// ─── TEMPLATES APP (interactive, one per state×viewport) ─
// ═══════════════════════════════════════════════════════
const DEFAULT_ST = { search: '', cat: 'all', sort: 'popular', cols: 4 };

const TemplatesApp = ({ scenario, mobile, tablet }) => {
  const sc = scenario.sc || {};
  const baseCols = mobile ? 1 : tablet ? 3 : 4;
  const [st, setSt] = useState({ ...DEFAULT_ST, ...sc, cols: sc.cols || baseCols });
  const [cloneT, setCloneT] = useState(sc.cloneId ? TEMPLATES.find(t => t.id === sc.cloneId) : null);
  const [typing, setTyping] = useState(false);
  const searchRef = useRef(null);
  const typingTimer = useRef(null);
  const loading = sc.loading, error = sc.error;

  const set = patch => setSt(prev => ({ ...prev, ...patch }));

  // debounce visual
  useEffect(() => {
    if (!st.search) { setTyping(false); return; }
    setTyping(true);
    clearTimeout(typingTimer.current);
    typingTimer.current = setTimeout(() => setTyping(false), 650);
    return () => clearTimeout(typingTimer.current);
  }, [st.search]);

  // keyboard: "/" focus search · 1-5 category (desktop)
  useEffect(() => {
    if (mobile) return;
    const h = e => {
      const tag = document.activeElement.tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;
      if (e.key === '/') { e.preventDefault(); searchRef.current && searchRef.current.focus(); }
      else if (e.key >= '1' && e.key <= '5') {
        const map = ['all', ...CAT_ORDER];
        const idx = Number(e.key) - 1;
        if (map[idx]) set({ cat: map[idx] });
      }
    };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [mobile]);

  // filter + sort
  const rows = useMemo(() => {
    const q = st.search.trim().toLowerCase();
    let out = TEMPLATES.filter(t => {
      if (st.cat !== 'all' && t.category !== st.cat) return false;
      if (q && !(t.name.toLowerCase().includes(q) || t.creator.name.toLowerCase().includes(q) || CATS[t.category].label.toLowerCase().includes(q))) return false;
      return true;
    });
    out = [...out].sort((a, b) => {
      if (st.sort === 'recent') return a.createdAt < b.createdAt ? 1 : -1;
      if (st.sort === 'rating') return b.rating - a.rating || b.usageCount - a.usageCount;
      if (st.sort === 'alpha') return a.name.localeCompare(b.name);
      return b.usageCount - a.usageCount; // popular
    });
    return out;
  }, [st.search, st.cat, st.sort]);

  const nActive = (st.cat !== 'all' ? 1 : 0) + (st.search.trim() ? 1 : 0);
  const clearAll = () => set({ search: '', cat: 'all' });
  const gridCols = mobile ? 1 : st.cols;
  const isList = !mobile && st.cols === 'list';
  const sumE = st.cat !== 'all' ? catE(st.cat) : 'hsl(var(--c-toolkit))';

  let body;
  if (loading) body = <SkeletonGrid cols={typeof gridCols === 'number' ? gridCols : 4} />;
  else if (rows.length === 0) body = <FilterEmpty onClear={clearAll} />;
  else body = (
    <div className={'tt-grid' + (isList ? ' list' : '')} style={{ '--cols': typeof gridCols === 'number' ? gridCols : 4 }} role="list" aria-label="Galleria template">
      {rows.map(t => <TemplateCard key={t.id} t={t} onClone={setCloneT} />)}
    </div>
  );

  return (
    <div className={'tt-app' + (mobile ? ' is-mobile' : '')}>
      {error && (
        <div className="tt-errbar" role="alert">
          <span aria-hidden="true">⚠</span>Impossibile caricare i template — riprova.
          <span className="grow" />
          <button className="retry"><span aria-hidden="true">↻</span> Riprova</button>
        </div>
      )}
      <Header mobile={mobile} onCreate={() => {}} />
      {!error && <Toolbar st={st} set={set} typing={typing} searchRef={searchRef} mobile={mobile} />}
      {!error && !loading && nActive > 0 && rows.length > 0 && (
        <div className="tt-fsum" style={{ '--e': sumE }}>
          <span className="badge"><span aria-hidden="true">⚑</span>{nActive} {nActive === 1 ? 'filtro attivo' : 'filtri attivi'}</span>
          <span>·</span>
          <span>{rows.length} {rows.length === 1 ? 'template' : 'template'} su {TOTAL}</span>
          <span className="grow" />
          <button className="clear" onClick={clearAll}>Cancella filtri</button>
        </div>
      )}
      <div className="tt-body" aria-busy={loading ? 'true' : undefined}>{!error && body}</div>
      {cloneT && <CloneModal t={cloneT} mobile={mobile} onClose={() => setCloneT(null)} />}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── FRAMES ─────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopFrame = ({ width = '100%', height = 686, children }) => (
  <div style={{
    width, borderRadius: 'var(--r-xl)', border: '1px solid var(--border)',
    background: 'var(--bg-card)', overflow: 'hidden', boxShadow: 'var(--shadow-lg)',
  }}>
    <div style={{
      display: 'flex', alignItems: 'center', gap: 8, padding: '9px 14px',
      background: 'var(--bg-muted)', borderBottom: '1px solid var(--border)',
      fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
    }}>
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: '#ff5f57' }} />
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: '#febc2e' }} />
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: '#28c840' }} />
      <span style={{ flex: 1, textAlign: 'center', letterSpacing: '.04em' }}>meepleai.app/toolkit/templates</span>
    </div>
    <div style={{ height }}>{children}</div>
  </div>
);

const PhoneFrame = ({ children }) => (
  <div className="phone" style={{ width: 375, height: 760 }}>
    <div className="phone-sbar" style={{ color: 'var(--text)' }}>
      <span style={{ fontFamily: 'var(--f-mono)' }}>14:32</span>
      <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
    </div>
    <div style={{ flex: 1, minHeight: 0, display: 'flex' }}>{children}</div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── STATE PICKER + ROOT ────────────────────────────
// ═══════════════════════════════════════════════════════
const STATES = [
  { id: 'default',          label: 'Default',           view: 'desktop', sc: {}, desc: '24 template, grid 4-col, nessun filtro, ordinamento "più popolari". Mix di card default / ufficiali / popolari / nuove.' },
  { id: 'filter-strategy',  label: 'Filter · Strategy', view: 'desktop', sc: { cat: 'Strategy' }, desc: 'Chip "Strategy" attivo (single-select radiogroup) → 8 card, badge "1 filtro attivo", accent --c-game.' },
  { id: 'filter-empty',     label: 'Filter · 0 match',  view: 'desktop', sc: { cat: 'Cooperative', search: 'briscola' }, desc: 'Categoria "Cooperative" + ricerca "briscola" → 0 corrispondenze → empty state con CTA "Cancella filtri".' },
  { id: 'default-3col-tablet', label: 'Tablet · 3-col', view: 'tablet', sc: { cols: 3 }, desc: 'Viewport 768px: grid 3-col, toolbar wrappa, card invariate. Stesso contenuto del default.' },
  { id: 'clone-modal-open', label: 'Clone modal',       view: 'desktop', sc: { cloneId: TEMPLATES[0].id }, desc: 'Modal "Clona template" su Strategy Standard: gioco target, nuovo nome, riepilogo strumenti, due opzioni, CTA conferma.' },
  { id: 'loading',          label: 'Loading',           view: 'desktop', sc: { loading: true }, desc: 'Skeleton shimmer: header + toolbar + 8 card placeholder con composizione tool.' },
  { id: 'error',            label: 'Error',             view: 'desktop', sc: { error: true }, desc: 'Banner danger in alto + Riprova; toolbar e grid nascosti.' },
  { id: 'mobile-stack',     label: 'Mobile · stack',    view: 'mobile',  sc: {}, desc: 'Viewport 375px: card full-width in colonna singola, chip categoria con scroll orizzontale, modal come bottom-sheet.' },
];
const SKEY = 'tt-state';

const VpLabel = ({ children }) => (
  <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)', textTransform: 'uppercase', letterSpacing: '.08em', fontWeight: 700 }}>{children}</div>
);

const App = () => {
  const [theme, setTheme] = useState(() => localStorage.getItem('mai-theme') || document.documentElement.getAttribute('data-theme') || 'light');
  const [active, setActive] = useState(() => {
    const s = localStorage.getItem(SKEY);
    return STATES.some(x => x.id === s) ? s : 'default';
  });
  useEffect(() => { document.documentElement.setAttribute('data-theme', theme); localStorage.setItem('mai-theme', theme); }, [theme]);
  useEffect(() => { localStorage.setItem(SKEY, active); }, [active]);

  const cur = STATES.find(s => s.id === active) || STATES[0];

  return (
    <div style={{ minHeight: '100vh', background: 'var(--bg)', color: 'var(--text)', padding: '20px 20px 80px' }}>
      <style dangerouslySetInnerHTML={{ __html: window.__TT_CSS }} />

      {/* state picker bar */}
      <header style={{
        position: 'sticky', top: 12, zIndex: 50, maxWidth: 1320, margin: '0 auto 24px',
        background: 'var(--glass-bg)', backdropFilter: 'blur(16px)',
        border: '1px solid var(--border)', borderRadius: 'var(--r-xl)',
        boxShadow: 'var(--shadow-md)', padding: '12px 16px',
        display: 'flex', alignItems: 'center', gap: 14, flexWrap: 'wrap',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <div style={{
            width: 30, height: 30, borderRadius: 8, flexShrink: 0,
            background: `linear-gradient(135deg, ${eHsl('toolkit')}, ${eHsl('game')})`,
            color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontWeight: 800, fontFamily: 'var(--f-display)', fontSize: 14,
          }}>S</div>
          <div>
            <div style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 14, lineHeight: 1.1 }}>Toolkit Templates</div>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)' }}>#1490 · 3/4 · /toolkit/templates</div>
          </div>
        </div>

        <div role="tablist" aria-label="Stati schermata" style={{ display: 'flex', gap: 6, flexWrap: 'wrap', flex: 1, minWidth: 0 }}>
          {STATES.map(s => {
            const on = s.id === active;
            return (
              <button key={s.id} type="button" role="tab" aria-selected={on} onClick={() => setActive(s.id)} style={{
                padding: '7px 12px', borderRadius: 'var(--r-pill)', cursor: 'pointer',
                background: on ? eHsl('toolkit') : 'var(--bg-muted)',
                border: on ? 'none' : '1px solid var(--border)',
                color: on ? '#fff' : 'var(--text-sec)',
                fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, whiteSpace: 'nowrap',
                boxShadow: on ? `0 3px 10px ${eHsl('toolkit', 0.35)}` : 'none',
              }}>{s.label}</button>
            );
          })}
        </div>

        <button type="button" onClick={() => setTheme(t => t === 'light' ? 'dark' : 'light')} style={{
          padding: '8px 14px', borderRadius: 'var(--r-md)', flexShrink: 0,
          background: 'var(--bg-card)', border: '1px solid var(--border)',
          color: 'var(--text)', fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer',
        }}>🌗 {theme === 'light' ? 'Light' : 'Dark'}</button>
      </header>

      {/* active state description */}
      <div style={{ maxWidth: 1320, margin: '0 auto 18px', padding: '0 4px', fontFamily: 'var(--f-mono)', fontSize: 12, color: 'var(--text-muted)', lineHeight: 1.5 }}>
        <strong style={{ color: eHsl('toolkit') }}>{cur.label}</strong> — {cur.desc}
      </div>

      {/* render area */}
      <div style={{ maxWidth: 1320, margin: '0 auto', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 36 }}>
        {cur.view === 'desktop' && (
          <div style={{ width: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <VpLabel>Desktop · 1440 — hero + grid gallery 4-col</VpLabel>
            <DesktopFrame>
              <TemplatesApp key={'d-' + cur.id} scenario={cur} mobile={false} />
            </DesktopFrame>
          </div>
        )}
        {cur.view === 'tablet' && (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <VpLabel>Tablet · 768 — grid gallery 3-col</VpLabel>
            <DesktopFrame width={768} height={720}>
              <TemplatesApp key={'t-' + cur.id} scenario={cur} mobile={false} tablet={true} />
            </DesktopFrame>
          </div>
        )}
        {cur.view === 'mobile' && (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <VpLabel>Mobile · 375 — stack 1-col + bottom-sheet modal</VpLabel>
            <PhoneFrame>
              <TemplatesApp key={'m-' + cur.id} scenario={cur} mobile={true} />
            </PhoneFrame>
          </div>
        )}
      </div>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
