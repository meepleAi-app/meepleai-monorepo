/* sp4-library-wishlist-ui.jsx — components + interactive app + harness
   (loads after sp4-library-wishlist.jsx). Route: /library/wishlist — vedi header foundation. */

const { useState, useEffect, useMemo, useRef, useCallback } = React;
const DS = window.DS;
const L = window.__LW;
const {
  eHsl, relDate, fmtDateTime, euro, NOW,
  PRIO, PRIO_ORDER, CATMETA,
  WISHLIST, TOTAL, PRIO_COUNTS, TOTAL_SPEND, LIB_GAMES,
} = L;

const pStyle = key => ({ '--p': PRIO[key].pvar });

// ═══════════════════════════════════════════════════════
// ─── PRIMITIVES ─────────────────────────────────────
// ═══════════════════════════════════════════════════════
const PriorityBadge = ({ prio, onToggle, active }) => {
  const p = PRIO[prio];
  return (
    <button type="button" className={'lw-pbadge' + (prio === 'low' ? ' low' : '')} style={pStyle(prio)}
      role="button" aria-pressed={!!active} aria-label={`Priorità ${p.it} — filtra per questa priorità`}
      onClick={e => { e.stopPropagation(); onToggle && onToggle(prio); }}>
      <span aria-hidden="true">{p.icon}</span>{p.it}
    </button>
  );
};

const CategoryChip = ({ category }) => {
  const c = CATMETA[category] || { it: category, icon: '🎲' };
  return (
    <span className="lw-ecat" title={`Categoria: ${c.it}`}>
      <span aria-hidden="true">{c.icon}</span>{c.it}
    </span>
  );
};

const MetaBox = ({ w }) => (
  <div className="lw-metabox" aria-label={`${CATMETA[w.category].it}, ${w.players} giocatori, ${w.duration}, valutazione BGG ${w.bgg}`}>
    <div className="lw-metarow">
      <CategoryChip category={w.category} />
      <span className="lw-mchip"><span className="mi" aria-hidden="true">👥</span>{w.players}</span>
    </div>
    <div className="lw-metarow">
      <span className="lw-mchip"><span className="mi" aria-hidden="true">⏱</span>{w.duration}</span>
      <span className="lw-mchip bgg"><span aria-hidden="true">⭐</span>BGG {w.bgg.toFixed(1)}</span>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── WISHLIST CARD ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const WishlistCard = ({ w, activePrios, onTogglePrio, onEdit, onRemove }) => {
  const hasPrice = w.targetPrice != null;
  const hasNotes = !!w.notes;
  return (
    <article className={'lw-card' + (w.priority === 'high' ? ' high' : '')} style={pStyle(w.priority)}
      role="listitem" aria-labelledby={'wlname-' + w.id}>
      {/* header row */}
      <div className="lw-chead">
        <span className="lw-heart" aria-hidden="true">❤️</span>
        <span className="lw-chgrow" />
        <PriorityBadge prio={w.priority} onToggle={onTogglePrio} active={activePrios.includes(w.priority)} />
      </div>

      {/* game name */}
      <button type="button" className="lw-cname" id={'wlname-' + w.id} title={w.name}
        aria-label={`Apri dettaglio gioco ${w.name}`}>
        <span className="gem" aria-hidden="true">{w.emoji}</span>
        <span className="gtxt">{w.name}</span>
      </button>

      {/* meta box (skip su minimal) */}
      {!w.minimal && <MetaBox w={w} />}

      {/* target price + notes */}
      {(hasPrice || hasNotes) && (
        <div className="lw-pn">
          {hasPrice && (
            <span className="lw-price"><span className="plbl">Target</span>{euro(w.targetPrice)}</span>
          )}
          {hasNotes
            ? <p className="lw-notes">{w.notes}</p>
            : (hasPrice ? null : <span className="lw-noprice">Nessuna nota</span>)}
        </div>
      )}

      <span className="lw-spacer" />

      {/* added-at */}
      <span className="lw-added" title={`Aggiunto il ${fmtDateTime(w.addedAt)}`}>
        <span className="ai" aria-hidden="true">➕</span>Aggiunto {relDate(w.addedAt, NOW)}
      </span>

      <div className="lw-cdiv" />

      {/* footer actions */}
      <div className="lw-acts">
        <button type="button" className="lw-act edit" onClick={() => onEdit(w)} aria-label={`Modifica wishlist item ${w.name}`}>
          <span aria-hidden="true">✏️</span>Modifica
        </button>
        <button type="button" className="lw-act del" onClick={() => onRemove(w)} aria-label={`Rimuovi ${w.name} dalla wishlist`}>
          <span aria-hidden="true">✕</span>Rimuovi
        </button>
      </div>
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
  { id: 'priority', label: 'Per priorità' },
  { id: 'recent', label: 'Più recenti' },
  { id: 'oldest', label: 'Più vecchi' },
  { id: 'alpha', label: 'Alfabetico' },
  { id: 'price', label: 'Prezzo target' },
];

const Toolbar = ({ st, set, togglePrio, typing, searchRef, mobile }) => {
  const [openSort, setOpenSort] = useState(false);
  const sortRef = useOutside(() => setOpenSort(false));
  const allOn = st.prio.length === 0;

  return (
    <div className="lw-toolbar">
      {/* search */}
      <div className={'lw-search' + (st.search ? ' active' : '')}>
        <span className="ic" aria-hidden="true">🔍</span>
        <input ref={searchRef} value={st.search} onChange={e => set({ search: e.target.value })} role="searchbox"
          aria-label="Cerca per nome gioco o note" placeholder="Cerca per nome gioco o note…"
          onKeyDown={e => { if (e.key === 'Escape') set({ search: '' }); }} />
        {typing && <span className="busy" aria-hidden="true"><i />Cercando…</span>}
        {st.search && <button className="clear" aria-label="Cancella ricerca" onClick={() => { set({ search: '' }); searchRef.current && searchRef.current.focus(); }}>✕</button>}
      </div>

      {/* priority chips — multi-select checkbox group */}
      <div className={'lw-chips' + (mobile ? ' scroll' : '')} role="group" aria-label="Filtra per priorità (selezione multipla)">
        <button type="button" role="checkbox" aria-checked={allOn} className={'lw-chip all' + (allOn ? ' on' : '')}
          onClick={() => set({ prio: [] })}>
          <span className="cdot" aria-hidden="true" />Tutti<span className="ccount">{TOTAL}</span>
        </button>
        {PRIO_ORDER.map(key => {
          const p = PRIO[key], on = st.prio.includes(key);
          return (
            <button key={key} type="button" role="checkbox" aria-checked={on}
              className={'lw-chip prio' + (on ? ' on' : '')} style={pStyle(key)}
              onClick={() => togglePrio(key)}>
              <span aria-hidden="true">{p.icon}</span>{p.it}<span className="ccount">{PRIO_COUNTS[key]}</span>
            </button>
          );
        })}
      </div>

      {/* sort */}
      <div className="lw-right">
        <div className="lw-fgroup" ref={sortRef}>
          <button type="button" className="lw-sortbtn" aria-haspopup="listbox" aria-expanded={openSort} onClick={() => setOpenSort(o => !o)}>
            <span aria-hidden="true">↕</span>{SORT_OPTS.find(s => s.id === st.sort).label}
            <span aria-hidden="true" style={{ fontSize: 8, opacity: .7 }}>▼</span>
          </button>
          {openSort && (
            <div className="lw-pop" role="listbox" aria-label="Ordina wishlist">
              <div className="lw-pophead">Ordina per</div>
              {SORT_OPTS.map(o => (
                <button key={o.id} type="button" role="option" aria-selected={st.sort === o.id}
                  className={'lw-popitem' + (st.sort === o.id ? ' sel' : '')} onClick={() => { set({ sort: o.id }); setOpenSort(false); }}>
                  <span className="check" aria-hidden="true">{st.sort === o.id ? '●' : ''}</span>
                  <span className="lbl">{o.label}</span>
                </button>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── EMPTY / LOADING ────────────────────────────────
// ═══════════════════════════════════════════════════════
const FilterEmpty = ({ onClear }) => (
  <div className="lw-pad" aria-live="polite">
    <div className="lw-empty filter">
      <div className="em" aria-hidden="true">🔍</div>
      <h3>Nessun gioco corrisponde ai filtri</h3>
      <p>Prova a cambiare priorità o a modificare la ricerca per nome o note.</p>
      <button className="cta" onClick={onClear}>Cancella filtri</button>
    </div>
  </div>
);

const EmptyNoItems = ({ onAdd }) => (
  <div className="lw-pad" aria-live="polite">
    <div className="lw-empty">
      <div className="em" aria-hidden="true">❤️</div>
      <h3>Nessun gioco nella wishlist</h3>
      <p>Aggiungi giochi che ti interessano per non perderli di vista — con priorità, prezzo target e note.</p>
      <button className="cta" onClick={onAdd}><span aria-hidden="true">❤️</span>Aggiungi il primo</button>
    </div>
  </div>
);

const Sk = ({ w, h, r = 'var(--r-sm)', style }) => <div className="lw-sk lw-shimmer" style={{ width: w, height: h, borderRadius: r, ...style }} />;
const SkeletonGrid = () => (
  <div className="lw-grid" aria-busy="true">
    {Array.from({ length: 8 }).map((_, i) => (
      <div className="lw-skcard" key={i}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <Sk w={26} h={26} r="var(--r-sm)" /><span style={{ flex: 1 }} /><Sk w={62} h={20} r="var(--r-pill)" />
        </div>
        <Sk w="72%" h={17} />
        <Sk w="100%" h={62} r="var(--r-md)" />
        <Sk w="55%" h={13} />
        <div style={{ flex: 1 }} />
        <Sk w="44%" h={11} />
        <Sk w="100%" h={1} />
        <div style={{ display: 'flex', gap: 6 }}><Sk w={92} h={32} r="var(--r-md)" /><Sk w={84} h={32} r="var(--r-md)" style={{ marginLeft: 'auto' }} /></div>
      </div>
    ))}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ADD / EDIT WISHLIST DIALOG ─────────────────────
// ═══════════════════════════════════════════════════════
const NOTE_MAX = 200;

const AddWishlistDialog = ({ mode, prefill, item, mobile, onClose }) => {
  const editing = mode === 'edit';
  const seedGame = editing
    ? { id: item.gameId, title: item.name, coverEmoji: item.emoji }
    : (prefill ? { id: 'g-brass', title: 'Brass: Birmingham', coverEmoji: '🏭' } : null);

  const [game, setGame] = useState(seedGame);
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const [hl, setHl] = useState(0);
  const [prio, setPrio] = useState(editing ? item.priority : (prefill ? 'high' : 'medium'));
  const [price, setPrice] = useState(editing && item.targetPrice != null ? String(item.targetPrice) : (prefill ? '65' : ''));
  const [notes, setNotes] = useState(editing ? item.notes : (prefill ? 'Acquisto entro Q3, ' : ''));
  const [phase, setPhase] = useState('idle'); // idle | submitting | success | error

  const closeRef = useRef(null);
  const modalRef = useRef(null);
  const comboRef = useOutside(() => setOpen(false));

  useEffect(() => { closeRef.current && closeRef.current.focus(); }, []);
  useEffect(() => {
    const onKey = e => {
      if (e.key === 'Escape') { if (open) { setOpen(false); return; } onClose(); return; }
      if (e.key === 'Tab' && modalRef.current) {
        const f = modalRef.current.querySelectorAll('button, input, select, textarea, [tabindex]:not([tabindex="-1"])');
        if (!f.length) return;
        const first = f[0], last = f[f.length - 1];
        if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
        else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose, open]);

  const matches = useMemo(() => {
    const q = query.trim().toLowerCase();
    const pool = LIB_GAMES.filter(g => g.title.toLowerCase().includes(q));
    return pool.slice(0, 6);
  }, [query]);

  const pick = g => { setGame({ id: g.id, title: g.title, coverEmoji: g.coverEmoji }); setOpen(false); setQuery(''); };
  const canSubmit = !!game && !!prio && phase !== 'submitting';

  const submit = () => {
    if (!canSubmit) return;
    setPhase('submitting');
    setTimeout(() => {
      // demo: prefill scenario mostra il percorso error→retry una volta
      setPhase('success');
      setTimeout(onClose, 1100);
    }, 950);
  };

  const counterNear = notes.length > NOTE_MAX - 30;

  return (
    <div className="lw-overlay" onMouseDown={e => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="lw-modal" ref={modalRef} role="dialog" aria-modal="true" aria-labelledby="lw-mtitle">
        <div className="lw-mhead">
          <span className="lw-mcov" aria-hidden="true">❤️</span>
          <div className="lw-mhtxt">
            <div className="lw-mtitle" id="lw-mtitle">{editing ? 'Modifica wishlist' : 'Aggiungi alla wishlist'}</div>
            <div className="lw-msub">{editing ? `${item.name} · priorità ${PRIO[item.priority].it}` : 'Salva un gioco che vuoi comprare o provare'}</div>
          </div>
          <button type="button" className="lw-mclose" ref={closeRef} aria-label="Chiudi" onClick={onClose}>✕</button>
        </div>

        <div className="lw-mbody">
          {phase === 'error' && (
            <div className="lw-merr" role="alert">
              <span aria-hidden="true">⚠</span>Errore aggiungendo alla wishlist.
              <span className="grow" />
              <button className="rt" onClick={submit}>Riprova</button>
            </div>
          )}

          {/* game selector combo */}
          <div className="lw-field">
            <span className="lw-flabel">Gioco <span className="req" aria-hidden="true">*</span></span>
            {game ? (
              <div className="lw-selected">
                <span className="se" aria-hidden="true">{game.coverEmoji || '🎲'}</span>
                <span className="sn">{game.title}</span>
                {!editing && <button type="button" className="sx" aria-label="Rimuovi gioco selezionato" onClick={() => setGame(null)}>✕</button>}
              </div>
            ) : (
              <div className="lw-combo" ref={comboRef}>
                <input type="text" value={query} role="combobox" aria-expanded={open} aria-autocomplete="list" aria-controls="lw-gamelist"
                  placeholder="Cerca per nome o incolla un game ID…" aria-label="Cerca gioco da aggiungere"
                  onFocus={() => setOpen(true)} onChange={e => { setQuery(e.target.value); setOpen(true); setHl(0); }}
                  onKeyDown={e => {
                    if (e.key === 'ArrowDown') { e.preventDefault(); setHl(h => Math.min(h + 1, matches.length - 1)); }
                    else if (e.key === 'ArrowUp') { e.preventDefault(); setHl(h => Math.max(h - 1, 0)); }
                    else if (e.key === 'Enter' && matches[hl]) { e.preventDefault(); pick(matches[hl]); }
                  }} />
                {open && matches.length > 0 && (
                  <div className="lw-dropdown" id="lw-gamelist" role="listbox">
                    {matches.map((g, i) => (
                      <button key={g.id} type="button" role="option" aria-selected={i === hl}
                        className={'lw-dopt' + (i === hl ? ' hl' : '')} onMouseEnter={() => setHl(i)} onClick={() => pick(g)}>
                        <span className="de" aria-hidden="true">{g.coverEmoji}</span>{g.title}
                        <span className="dmeta">{g.players} · {g.duration}</span>
                      </button>
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>

          {/* priority radiogroup */}
          <div className="lw-field">
            <span className="lw-flabel">Priorità <span className="req" aria-hidden="true">*</span></span>
            <div className="lw-prioset" role="radiogroup" aria-label="Priorità">
              {PRIO_ORDER.map(key => {
                const p = PRIO[key], on = prio === key;
                return (
                  <button key={key} type="button" role="radio" aria-checked={on}
                    className={'lw-priochip' + (key === 'low' ? ' low' : '') + (on ? ' on' : '')} style={pStyle(key)}
                    onClick={() => setPrio(key)}>
                    <span aria-hidden="true">{p.icon}</span>{p.it}
                  </button>
                );
              })}
            </div>
          </div>

          {/* target price */}
          <div className="lw-field">
            <span className="lw-flabel">Target price (€)<span className="grow" /></span>
            <div className="lw-numwrap">
              <span className="pfx" aria-hidden="true">€</span>
              <input type="number" min="0" max="999" step="0.5" value={price} placeholder="65"
                aria-label="Prezzo massimo che sei disposto a spendere" onChange={e => setPrice(e.target.value)} />
            </div>
            <span className="lw-hint"><span aria-hidden="true">ℹ</span>Prezzo massimo che sei disposto a spendere — opzionale.</span>
          </div>

          {/* notes */}
          <div className="lw-field">
            <span className="lw-flabel">Note<span className="grow" /><span className={'lw-counter' + (counterNear ? ' near' : '')}>{notes.length} / {NOTE_MAX}</span></span>
            <textarea className="lw-ta" maxLength={NOTE_MAX} value={notes} aria-label="Note"
              placeholder="es. acquisto entro Q3, voglio aspettare la nuova edizione, ecc."
              onChange={e => setNotes(e.target.value)} />
          </div>
        </div>

        <div className="lw-mfoot">
          <button type="button" className="lw-mbtn" onClick={onClose}>Annulla</button>
          <span className="grow" />
          <button type="button" className="lw-mbtn primary" disabled={!canSubmit} onClick={submit}>
            {phase === 'submitting'
              ? <React.Fragment><span className="lw-spin" aria-hidden="true" />Aggiungo…</React.Fragment>
              : phase === 'success'
                ? <React.Fragment><span aria-hidden="true">✓</span>Aggiunto</React.Fragment>
                : <React.Fragment><span aria-hidden="true">❤️</span>{editing ? 'Salva' : 'Aggiungi'}</React.Fragment>}
          </button>
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── HEADER (sticky + tabs) ─────────────────────────
// ═══════════════════════════════════════════════════════
const Header = ({ mobile, onAdd }) => (
  <header className="lw-head">
    <div className="lw-htop">
      <div className="lw-htxt">
        <div className="lw-bread"><span>Libreria</span><span className="sep" aria-hidden="true">›</span><span className="cur">Wishlist</span></div>
        <div className="lw-titlerow">
          <span className="lw-ico" aria-hidden="true">❤️</span>
          <h1 className="lw-h1">Wishlist <span style={{ color: 'var(--text-muted)', fontWeight: 600 }}>·</span> Giochi desiderati</h1>
        </div>
        {!mobile && <p className="lw-sub">Tieni traccia dei giochi che vuoi comprare o provare.</p>}
      </div>
      <div className="lw-hright">
        <span className="lw-qstat"><b>{TOTAL}</b> giochi<span className="dot" aria-hidden="true">·</span><b>{PRIO_COUNTS.high}</b> alta priorità<span className="dot" aria-hidden="true">·</span>spesa stimata <b>{euro(TOTAL_SPEND)}</b></span>
        <button type="button" className="lw-cta" onClick={onAdd}><span aria-hidden="true">❤️</span>{mobile ? 'Aggiungi' : 'Aggiungi alla wishlist'}</button>
      </div>
    </div>
    <nav className="lw-tabs" role="tablist" aria-label="Sezioni libreria">
      <button type="button" role="tab" aria-selected={false} className="lw-tab"><span aria-hidden="true">📚</span>Libreria</button>
      <button type="button" role="tab" aria-selected={true} className="lw-tab on"><span aria-hidden="true">❤️</span>Wishlist</button>
    </nav>
  </header>
);

// ═══════════════════════════════════════════════════════
// ─── WISHLIST APP (interactive, one per state×viewport) ─
// ═══════════════════════════════════════════════════════
const DEFAULT_ST = { search: '', prio: [], sort: 'priority' };

const WishlistApp = ({ scenario, mobile }) => {
  const sc = scenario.sc || {};
  const [st, setSt] = useState({ ...DEFAULT_ST, ...sc.st });
  const [dialog, setDialog] = useState(sc.dialog ? { mode: 'add', prefill: true } : null);
  const [typing, setTyping] = useState(false);
  const searchRef = useRef(null);
  const typingTimer = useRef(null);
  const loading = sc.loading, error = sc.error, emptyAll = sc.empty;

  const set = patch => setSt(prev => ({ ...prev, ...patch }));
  const togglePrio = key => setSt(prev => ({ ...prev, prio: prev.prio.includes(key) ? prev.prio.filter(p => p !== key) : [...prev.prio, key] }));

  // debounce visual
  useEffect(() => {
    if (!st.search) { setTyping(false); return; }
    setTyping(true);
    clearTimeout(typingTimer.current);
    typingTimer.current = setTimeout(() => setTyping(false), 650);
    return () => clearTimeout(typingTimer.current);
  }, [st.search]);

  // keyboard: "/" focus search · n new · 1/2/3 priority (desktop)
  useEffect(() => {
    if (mobile) return;
    const h = e => {
      const tag = document.activeElement.tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;
      if (e.key === '/') { e.preventDefault(); searchRef.current && searchRef.current.focus(); }
      else if (e.key === 'n') { e.preventDefault(); setDialog({ mode: 'add' }); }
      else if (e.key >= '1' && e.key <= '3') { togglePrio(PRIO_ORDER[Number(e.key) - 1]); }
    };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [mobile]);

  // filter + sort
  const rows = useMemo(() => {
    const q = st.search.trim().toLowerCase();
    let out = WISHLIST.filter(w => {
      if (st.prio.length && !st.prio.includes(w.priority)) return false;
      if (q && !(w.name.toLowerCase().includes(q) || w.notes.toLowerCase().includes(q))) return false;
      return true;
    });
    out = [...out].sort((a, b) => {
      if (st.sort === 'recent') return b.addedAt - a.addedAt;
      if (st.sort === 'oldest') return a.addedAt - b.addedAt;
      if (st.sort === 'alpha') return a.name.localeCompare(b.name);
      if (st.sort === 'price') return (b.targetPrice || -1) - (a.targetPrice || -1);
      // priority: high→low, poi più recenti
      return PRIO[a.priority].rank - PRIO[b.priority].rank || b.addedAt - a.addedAt;
    });
    return out;
  }, [st.search, st.prio, st.sort]);

  const nActive = (st.prio.length ? 1 : 0) + (st.search.trim() ? 1 : 0);
  const clearAll = () => set({ search: '', prio: [] });

  let body;
  if (loading) body = <SkeletonGrid />;
  else if (emptyAll) body = <EmptyNoItems onAdd={() => setDialog({ mode: 'add' })} />;
  else if (rows.length === 0) body = <FilterEmpty onClear={clearAll} />;
  else body = (
    <div className="lw-grid" role="list" aria-label="Wishlist giochi">
      {rows.map(w => (
        <WishlistCard key={w.id} w={w} activePrios={st.prio} onTogglePrio={togglePrio}
          onEdit={it => setDialog({ mode: 'edit', item: it })} onRemove={() => {}} />
      ))}
    </div>
  );

  return (
    <div className={'lw-app' + (mobile ? ' is-mobile' : '')} aria-busy={loading ? 'true' : undefined}>
      {error && (
        <div className="lw-errbar" role="alert">
          <span aria-hidden="true">⚠</span>Impossibile caricare la wishlist — riprova.
          <span className="grow" />
          <button className="retry"><span aria-hidden="true">↻</span> Riprova</button>
        </div>
      )}
      <Header mobile={mobile} onAdd={() => setDialog({ mode: 'add' })} />
      {!error && !emptyAll && <Toolbar st={st} set={set} togglePrio={togglePrio} typing={typing} searchRef={searchRef} mobile={mobile} />}
      {!error && !loading && !emptyAll && nActive > 0 && rows.length > 0 && (
        <div className="lw-fsum">
          <span className="badge"><span aria-hidden="true">⚑</span>{nActive} {nActive === 1 ? 'filtro attivo' : 'filtri attivi'}</span>
          <span>·</span>
          <span>{rows.length} {rows.length === 1 ? 'gioco' : 'giochi'} su {TOTAL}</span>
          <span className="grow" />
          <button className="clear" onClick={clearAll}>Cancella filtri</button>
        </div>
      )}
      <div className="lw-body">{!error && body}</div>
      {dialog && <AddWishlistDialog {...dialog} mobile={mobile} onClose={() => setDialog(null)} />}
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
      <span style={{ flex: 1, textAlign: 'center', letterSpacing: '.04em' }}>meepleai.app/library/wishlist</span>
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
  { id: 'default', label: 'Default', view: 'desktop', sc: {}, desc: '12 giochi (5 alta / 4 media / 3 bassa), grid responsive, ordinamento "per priorità". Mix di card: complete, solo-prezzo, solo-note, high-priority (border --c-danger), minimal.' },
  { id: 'filter-priority-alta', label: 'Filter · Alta', view: 'desktop', sc: { st: { prio: ['high'] } }, desc: 'Chip "🔥 Alta" attivo (multi-select) → 5 card, badge "1 filtro attivo". Click un priority badge sulla card per filtrare.' },
  { id: 'filter-search-active', label: 'Filter · Search', view: 'desktop', sc: { st: { search: 'brass' } }, desc: 'Ricerca "brass" su nome + note → 1 card filtrata (Brass: Birmingham), spinner debounce, badge filtro.' },
  { id: 'empty-no-items', label: 'Empty', view: 'desktop', sc: { empty: true }, desc: 'Utente nuovo: wishlist vuota → empty state centrato con ❤️ muted + CTA "Aggiungi il primo".' },
  { id: 'loading', label: 'Loading', view: 'desktop', sc: { loading: true }, desc: 'Skeleton shimmer: header + toolbar + 8 card placeholder con meta box e azioni.' },
  { id: 'error', label: 'Error', view: 'desktop', sc: { error: true }, desc: 'Banner danger in alto + Riprova; toolbar e grid nascosti.' },
  { id: 'add-dialog-open', label: 'Add dialog', view: 'desktop', sc: { dialog: true }, desc: 'AddToWishlistDialog in stato "filling": gioco "Brass: Birmingham" selezionato (EntityChip), priorità "Alta", target €65, note iniziate. Combo · radiogroup · number · textarea con counter.' },
  { id: 'mobile-stack', label: 'Mobile · stack', view: 'mobile', sc: {}, desc: 'Viewport 375px: grid 2-col compatta, chip priorità con scroll orizzontale, dialog come bottom-sheet.' },
];
const SKEY = 'lw-state';

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
      <style dangerouslySetInnerHTML={{ __html: window.__LW_CSS }} />

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
            background: `linear-gradient(135deg, ${eHsl('game')}, ${eHsl('event')})`,
            color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontWeight: 800, fontFamily: 'var(--f-display)', fontSize: 14,
          }}>S</div>
          <div>
            <div style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 14, lineHeight: 1.1 }}>Library Wishlist</div>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)' }}>#1491 · 1/1 · /library/wishlist</div>
          </div>
        </div>

        <div role="tablist" aria-label="Stati schermata" style={{ display: 'flex', gap: 6, flexWrap: 'wrap', flex: 1, minWidth: 0 }}>
          {STATES.map(s => {
            const on = s.id === active;
            return (
              <button key={s.id} type="button" role="tab" aria-selected={on} onClick={() => setActive(s.id)} style={{
                padding: '7px 12px', borderRadius: 'var(--r-pill)', cursor: 'pointer',
                background: on ? eHsl('game') : 'var(--bg-muted)',
                border: on ? 'none' : '1px solid var(--border)',
                color: on ? '#fff' : 'var(--text-sec)',
                fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, whiteSpace: 'nowrap',
                boxShadow: on ? `0 3px 10px ${eHsl('game', 0.35)}` : 'none',
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
        <strong style={{ color: eHsl('game') }}>{cur.label}</strong> — {cur.desc}
      </div>

      {/* render area */}
      <div style={{ maxWidth: 1320, margin: '0 auto', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 36 }}>
        {cur.view === 'desktop' && (
          <div style={{ width: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <VpLabel>Desktop · 1440 — hero + grid responsive</VpLabel>
            <DesktopFrame>
              <WishlistApp key={'d-' + cur.id} scenario={cur} mobile={false} />
            </DesktopFrame>
          </div>
        )}
        {cur.view === 'mobile' && (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <VpLabel>Mobile · 375 — grid 2-col + bottom-sheet dialog</VpLabel>
            <PhoneFrame>
              <WishlistApp key={'m-' + cur.id} scenario={cur} mobile={true} />
            </PhoneFrame>
          </div>
        )}
      </div>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
