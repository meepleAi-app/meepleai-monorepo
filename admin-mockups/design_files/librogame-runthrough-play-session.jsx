/* ════════════════════════════════════════════════════════════════
   librogame-runthrough-play-session.jsx
   Schermata : Librogame Play Session Shell — interattivo (SP8 companion)
   Route     : /library/librogame/play/{campaignId}
   Descrizione: versione runnable Babel-standalone delle estensioni SP8.
                Monta nel #diary-lab-root in fondo a
                librogame-runthrough-play-session.html (la gallery statica
                sopra resta la fonte di verità pixel-perfect; questo lab è la
                controparte cliccabile).
   Brief      : SP8-libro-game-companion. Persona Aaron, companion model
                single-user. NON ridisegna i 4 stati congelati.
   Stato      : ⏳ state-05 Diary (questa risposta).
                state-06 Paragrafi drawer + state-07 End campaign verranno
                APPESI in coda a questo file nelle risposte 2 e 3.
   Dati       : inline (data.js è read-only). Tainted Grail · "La grotta dei
                Goblin" · Cap 4 · §214 · glossario Niamh/Korak/Pietra Spettrale.
   ════════════════════════════════════════════════════════════════ */

const { useState, useEffect, useRef, useCallback } = React;

/* ─── entityHsl helper inline (palette 9 entity, da brief) ─── */
const ENTITY_HSL = {
  game:    '25 95% 45%',  player:  '262 83% 58%',  session: '240 60% 55%',
  agent:   '38 92% 50%',  kb:      '174 60% 40%',   chat:    '220 80% 55%',
  event:   '350 89% 60%', toolkit: '142 70% 45%',   tool:    '195 80% 50%',
};
const entityHsl = (entity, alpha) =>
  alpha != null ? `hsl(${ENTITY_HSL[entity]} / ${alpha})` : `hsl(${ENTITY_HSL[entity]})`;
/* in dark mode i token entity cambiano: per coerenza tema usiamo le CSS var live */
const eVar = (entity, alpha) =>
  alpha != null ? `hsl(var(--c-${entity}) / ${alpha})` : `hsl(var(--c-${entity}))`;

/* ─── paragrafo corrente della sessione (auto-pin) ─── */
const CURRENT_PARA = '§214';

/* ─── note seed per ciascuno stato (inline, NON da data.js) ─── */
const SEED_DEFAULT = [
  { id: 'diary-12', para: '§214', time: '5 min fa',     text: 'Goblin liberati senza combattere — bonus reputazione **+2**. Niamh sembra fidarsi di più del gruppo adesso.' },
  { id: 'diary-11', para: '§198', time: 'ieri 21:34',   text: 'NPC **Korak** mi deve un favore, citato a §134. Da rigiocare se serve un passaggio sicuro.' },
  { id: 'diary-09', para: '§134', time: '12 mag 21:34', text: 'Tesoro nella grotta non preso, tornare dopo §340. La **Spada di Avalon** è ancora là.' },
];
const SEED_OFFLINE = [
  { id: 'diary-12', para: '§214', time: '5 min fa', text: 'Goblin liberati senza combattere — bonus reputazione **+2**. Niamh si fida di più.' },
];
const SEED_ERROR = [
  { id: 'diary-draft', para: '§214', time: 'ora · non sincr.', text: 'Combattuto i 2 Goblin a §214. **Korak** è caduto al secondo turno — loot 14 oro.', unsynced: true },
];

const seedFor = (st) =>
  st === 'empty' ? [] :
  st === 'offline' ? SEED_OFFLINE.map(n => ({ ...n })) :
  st === 'error' ? SEED_ERROR.map(n => ({ ...n })) :
  SEED_DEFAULT.map(n => ({ ...n }));

/* ─── render markdown statico: **bold** + §NNN ref chip ─── */
function renderMarkdown(text) {
  const out = [];
  // split su **bold**
  const parts = text.split(/(\*\*[^*]+\*\*)/g);
  parts.forEach((part, i) => {
    if (/^\*\*[^*]+\*\*$/.test(part)) {
      out.push(<strong key={'b' + i}>{part.slice(2, -2)}</strong>);
      return;
    }
    // dentro al testo normale, evidenzia §NNN
    const segs = part.split(/(§\d+)/g);
    segs.forEach((seg, j) => {
      if (/^§\d+$/.test(seg)) out.push(<span key={'r' + i + '_' + j} className="dy-ref">{seg}</span>);
      else if (seg) out.push(<React.Fragment key={'t' + i + '_' + j}>{seg}</React.Fragment>);
    });
  });
  return out;
}

/* ════════════════════════════════════════════════════════════════
   useDiary — stato condiviso fra frame mobile e desktop
   ════════════════════════════════════════════════════════════════ */
function useDiary(lisaState) {
  const [notes, setNotes] = useState(() => seedFor(lisaState));
  const [editorOpen, setEditorOpen] = useState(false);
  const [draft, setDraft] = useState('');
  const [editingId, setEditingId] = useState(null);
  const [expanded, setExpanded] = useState(() => new Set());

  // reset quando cambia lo stato del lab
  useEffect(() => {
    setNotes(seedFor(lisaState));
    setEditorOpen(false); setDraft(''); setEditingId(null); setExpanded(new Set());
  }, [lisaState]);

  const openNew = () => { setEditingId(null); setDraft(''); setEditorOpen(true); };
  const openEdit = (n) => { setEditingId(n.id); setDraft(n.text); setEditorOpen(true); };
  const cancel = () => { setEditorOpen(false); setDraft(''); setEditingId(null); };
  const canSave = draft.trim().length > 0;
  const save = () => {
    if (!canSave) return;
    if (editingId) {
      setNotes(ns => ns.map(n => n.id === editingId ? { ...n, text: draft.trim(), time: 'ora · modificata' } : n));
    } else {
      const id = 'diary-' + Math.random().toString(36).slice(2, 7);
      setNotes(ns => [{ id, para: CURRENT_PARA, time: 'ora', text: draft.trim() }, ...ns]);
    }
    cancel();
  };
  const del = (id) => setNotes(ns => ns.filter(n => n.id !== id));
  const toggleExpand = (id) => setExpanded(s => { const n = new Set(s); n.has(id) ? n.delete(id) : n.add(id); return n; });

  return { notes, editorOpen, draft, setDraft, editingId, canSave, expanded,
           openNew, openEdit, cancel, save, del, toggleExpand };
}

/* ─── singola entry ─── */
function DiaryEntry({ n, expanded, onExpand, onEdit, onDelete, desktop }) {
  const long = n.text.length > 90;
  const isOpen = desktop || expanded;
  return (
    <div className="dy-entry">
      <div className="dy-meta">
        <span className="dy-pin">📌 {n.para}</span>
        <span className="dy-time">{n.time}</span>
        {n.unsynced && <span className="dy-localsave"><span className="dot"></span>locale</span>}
      </div>
      <p className={`dy-body${isOpen ? ' dy-full' : ''}`}>{renderMarkdown(n.text)}</p>
      {long && !desktop && (
        <button className="dy-expand" onClick={() => onExpand(n.id)}>{expanded ? 'comprimi' : 'espandi'}</button>
      )}
      <div className="dy-acts">
        <button className="dy-act" onClick={() => onEdit(n)}>✏️ Modifica</button>
        <button className="dy-act dy-del" onClick={() => onDelete(n.id)}>🗑️ Elimina</button>
      </div>
    </div>
  );
}

/* ─── editor markdown (condiviso) ─── */
function DiaryEditor({ d, desktop }) {
  const taRef = useRef(null);
  useEffect(() => { if (taRef.current) taRef.current.focus(); }, []);
  const insert = (wrap) => {
    const ta = taRef.current; if (!ta) return;
    const s = ta.selectionStart, e = ta.selectionEnd;
    const sel = d.draft.slice(s, e) || '';
    const next = d.draft.slice(0, s) + wrap + sel + wrap + d.draft.slice(e);
    d.setDraft(next);
    requestAnimationFrame(() => { ta.focus(); ta.selectionStart = ta.selectionEnd = s + wrap.length + sel.length; });
  };
  const insertPara = () => {
    const ta = taRef.current; if (!ta) return;
    const s = ta.selectionStart;
    const next = d.draft.slice(0, s) + CURRENT_PARA + d.draft.slice(s);
    d.setDraft(next);
    requestAnimationFrame(() => { ta.focus(); ta.selectionStart = ta.selectionEnd = s + CURRENT_PARA.length; });
  };
  const editorStyle = desktop
    ? { position: 'relative', inset: 'auto', border: '1px solid var(--border)', borderRadius: 'var(--r-lg)', overflow: 'hidden', height: '100%' }
    : null;
  return (
    <div className="dy-editor" style={editorStyle} role="dialog" aria-label={`${d.editingId ? 'Modifica' : 'Nuova'} nota ${CURRENT_PARA}`}>
      <div className="dy-ed-top">
        <button className="dy-cancel" onClick={d.cancel}>← Annulla</button>
        <span className="dy-ed-ti">Nota {CURRENT_PARA}</span>
        <button className="dy-save" disabled={!d.canSave} onClick={d.save}>Salva</button>
      </div>
      <div className="dy-toolbar">
        <button className="dy-tool" aria-label="Grassetto" onClick={() => insert('**')}>B</button>
        <button className="dy-tool" style={{ fontStyle: 'italic' }} aria-label="Corsivo" onClick={() => insert('*')}>I</button>
        <button className="dy-tool" style={{ textDecoration: 'underline' }} aria-label="Sottolineato" onClick={() => insert('__')}>U</button>
        <button className="dy-tool dy-pintool" aria-label="Collega paragrafo" onClick={insertPara}>📌</button>
        <button className="dy-tool" aria-label="Link" onClick={() => insert('[]')}>🔗</button>
        <span className="dy-draft"><span className="dot"></span>Bozza salvata</span>
      </div>
      <textarea ref={taRef} className="dy-textarea" aria-label="Testo nota" aria-multiline="true"
        placeholder="Scrivi quello che vuoi ricordare… (markdown)"
        value={d.draft} onChange={(e) => d.setDraft(e.target.value)} />
      {d.draft.trim().length > 0 && (
        <div className="dy-preview">
          <div className="dy-pv-lbl">Anteprima live</div>
          <p className="dy-body">{renderMarkdown(d.draft)}</p>
        </div>
      )}
    </div>
  );
}

/* ─── skeleton loading ─── */
function DiarySkeleton() {
  return (
    <React.Fragment>
      <div className="dy-skel"><div className="ln dy-shimmer" style={{ width: '35%' }}></div><div className="ln dy-shimmer" style={{ width: '90%' }}></div><div className="ln dy-shimmer" style={{ width: '70%', marginBottom: 0 }}></div></div>
      <div className="dy-skel"><div className="ln dy-shimmer" style={{ width: '35%' }}></div><div className="ln dy-shimmer" style={{ width: '80%' }}></div><div className="ln dy-shimmer" style={{ width: '55%', marginBottom: 0 }}></div></div>
    </React.Fragment>
  );
}

/* ─── corpo diary (lista o editor) condiviso mobile/desktop ─── */
function DiaryBody({ d, lisaState, desktop }) {
  if (lisaState === 'loading') {
    return (
      <div aria-busy="true">
        <button className="dy-newcta" style={{ opacity: 0.5, maxWidth: desktop ? 200 : undefined }} disabled>＋ Nuova nota</button>
        <DiarySkeleton />
      </div>
    );
  }
  if (d.editorOpen) return <DiaryEditor d={d} desktop={desktop} />;
  if (lisaState === 'empty' || d.notes.length === 0) {
    return (
      <div className="dy-empty">
        <div className="dy-ill">📓</div>
        <h3>Nessuna nota ancora</h3>
        <p>Scrivi quello che vuoi ricordare — scelte, NPC, tesori lasciati indietro.</p>
        <button className="dy-newcta" style={{ marginBottom: 0, maxWidth: 220 }} onClick={d.openNew}>✍️ Scrivi la prima</button>
      </div>
    );
  }
  return (
    <React.Fragment>
      {lisaState === 'error' && (
        <div className="dy-banner err" role="alert">
          <span className="dy-bic">⚠️</span>
          <div className="dy-btx">
            <strong style={{ color: eVar('event') }}>Errore salvataggio — riprova</strong>
            <span className="dy-sub">La nota non è arrivata al server.</span>
            <span className="dy-localsave"><span className="dot"></span>Salvata in locale · nessuna perdita</span>
          </div>
          <button className="dy-retry">↻ Riprova</button>
        </div>
      )}
      {lisaState === 'offline' && (
        <div className="dy-banner off" role="status">
          <span className="dy-bic">📴</span>
          <div className="dy-btx">
            <strong style={{ color: eVar('tool') }}>Offline</strong>
            <span className="dy-sub">La nota sarà salvata appena torni online.</span>
            <span className="dy-localsave" style={{ color: eVar('tool') }}><span className="dot" style={{ background: eVar('tool') }}></span>1 in coda</span>
          </div>
        </div>
      )}
      <button className="dy-newcta" style={{ maxWidth: desktop ? 200 : undefined }} onClick={d.openNew}>＋ Nuova nota</button>
      <div className="dy-timeline">
        {d.notes.map(n => (
          <DiaryEntry key={n.id} n={n} desktop={desktop}
            expanded={d.expanded.has(n.id)} onExpand={d.toggleExpand}
            onEdit={d.openEdit} onDelete={d.del} />
        ))}
      </div>
    </React.Fragment>
  );
}

/* ─── shell mobile (375px) ─── */
function PhoneFrame({ d, lisaState }) {
  const offline = lisaState === 'offline';
  const noteCount = lisaState === 'empty' ? 0 : 12;
  return (
    <div className="phone" role="region" aria-label="Mobile · diary interattivo">
      <span className="frame-label">Mobile · 375px</span>
      <div className="phone-sbar"><span>22:36</span><span>{offline ? '✈️' : '📶'} 🔋 58%</span></div>
      <div className="session-h">
        <span className="crumbs"><strong>Avalon</strong> · La grotta dei Goblin · <span className="para">{CURRENT_PARA}</span></span>
        <button className="menu" aria-label="Menu sessione">⋯</button>
      </div>
      <div className="header-pips">
        <span className="manapip kb"><span className="badge">47</span></span>
        <span className="manapip chat"><span className="badge">15</span></span>
        <span className="manapip session"><span className="badge">1</span></span>
        <span className="manapip toolkit"><span className="badge">12</span></span>
        <span className="lbl-mini">· Cap 4 · note {noteCount}</span>
      </div>
      <div className="tabs dy-scroll" role="tablist">
        <button className="tab" role="tab" aria-selected="false"><span className="em">📖</span>Story</button>
        <button className="tab" role="tab" aria-selected="false"><span className="em">⚔️</span>Encounter</button>
        <button className="tab" role="tab" aria-selected="false"><span className="em">📚</span>Glossario</button>
        <button className="tab dy-diary" role="tab" aria-selected="true"><span className="em">📓</span>Diary</button>
      </div>
      <div className="content" style={d.editorOpen ? { padding: 0, position: 'relative' } : { position: 'relative' }}>
        <DiaryBody d={d} lisaState={lisaState} desktop={false} />
      </div>
    </div>
  );
}

/* ─── shell desktop (split-view) ─── */
function DesktopFrame({ d, lisaState }) {
  return (
    <div className="desktop" role="region" aria-label="Desktop · diary interattivo">
      <span className="frame-label">Desktop · 1440px (scaled)</span>
      <div className="titlebar">
        <span className="dot r"></span><span className="dot y"></span><span className="dot g"></span>
        <span className="url">meepleai.app/library/librogame/play/camp-avalon-1?tab=diary</span>
      </div>
      <div className="d-body">
        <aside className="d-side" style={{ width: 260, display: 'flex', flexDirection: 'column' }}>
          <div className="side-card" style={{ marginBottom: 'var(--s-2)' }}>
            <h4>Campagna</h4>
            <div className="row"><span className="k">Nome</span><span className="v">Grotta Goblin</span></div>
            <div className="row"><span className="k">Capitolo</span><span className="v">4 · {CURRENT_PARA}</span></div>
          </div>
          <nav aria-label="Sezioni sessione" style={{ display: 'flex', flexDirection: 'column', gap: 2, marginBottom: 'var(--s-3)' }}>
            <button className="dy-navitem"><span className="em">📖</span>Story<span className="cnt">{CURRENT_PARA}</span></button>
            <button className="dy-navitem"><span className="em">⚔️</span>Encounter<span className="cnt">—</span></button>
            <button className="dy-navitem"><span className="em">📚</span>Glossario<span className="cnt">12</span></button>
            <button className="dy-navitem" aria-selected="true"><span className="em">📓</span>Diary<span className="cnt">{lisaState === 'empty' ? 0 : 12}</span></button>
          </nav>
          <div className="dy-d-foot"><button className="dy-endbtn">🏁 Chiudi campagna</button></div>
        </aside>
        <div className="d-main">
          <DiaryBody d={d} lisaState={lisaState} desktop={true} />
        </div>
      </div>
    </div>
  );
}

/* ════════════════════════════════════════════════════════════════
   DiaryLab — selettore stato + frame mobile/desktop in sync
   ════════════════════════════════════════════════════════════════ */
const LISA_STATES = [
  { id: 'default', lb: 'Default' },
  { id: 'empty',   lb: 'Vuoto' },
  { id: 'loading', lb: 'Loading' },
  { id: 'error',   lb: 'Errore save' },
  { id: 'offline', lb: 'Offline' },
];

function DiaryLab() {
  const [lisaState, setLisaState] = useState('default');
  const d = useDiary(lisaState);
  return (
    <div>
      <div className="dy-lab-bar" role="toolbar" aria-label="Stato diary">
        <div className="grp">
          <span className="lbl">Stato</span>
          {LISA_STATES.map(s => (
            <button key={s.id} className={`dy-seg${lisaState === s.id ? ' on' : ''}`}
              aria-pressed={lisaState === s.id} onClick={() => setLisaState(s.id)}>{s.lb}</button>
          ))}
        </div>
      </div>
      <div className="frames">
        <PhoneFrame d={d} lisaState={lisaState} />
        <DesktopFrame d={d} lisaState={lisaState} />
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('diary-lab-root')).render(<DiaryLab />);

/* ════════════════════════════════════════════════════════════════
   state-06 — Paragrafi visitati drawer (APPEND)
   Function #5. Drawer canonico bottom-sheet (mobile) / side-panel (desktop).
   Jump-back con conferma alertdialog. Search v1 = §numero/capitolo client-side.
   ════════════════════════════════════════════════════════════════ */

const HISTORY_SEED = [
  { para: '§198', n: 198, when: '22 min fa', chap: 'Cap 4', snip: "Entri nel corridoio buio, una corrente d'aria spegne la torcia. In fondo senti un raschiare metallico." },
  { para: '§189', n: 189, when: '1h fa',     chap: 'Cap 4', snip: 'Il ponte di corda oscilla sul precipizio. Niamh ti fa cenno di aspettare prima di attraversare.' },
  { para: '§134', n: 134, when: '2h fa',     chap: 'Cap 3', snip: 'Korak ti porge la mano: "Ti devo un favore. Cercami quando avrai bisogno di passare il valico."' },
];

const HIST_STATES = [
  { id: 'default', lb: 'Default' },
  { id: 'empty',   lb: 'Vuoto' },
  { id: 'loading', lb: 'Loading' },
  { id: 'error',   lb: 'Errore' },
  { id: 'offline', lb: 'Offline' },
];

/* hook condiviso mobile/desktop */
function useHistory(histState) {
  const [open, setOpen] = useState(true);
  const [query, setQuery] = useState('');
  const [confirmTarget, setConfirmTarget] = useState(null); // entry o null
  const [currentPara, setCurrentPara] = useState('§214');
  const [toast, setToast] = useState(null);

  useEffect(() => {
    setOpen(true); setQuery(''); setConfirmTarget(null); setCurrentPara('§214'); setToast(null);
  }, [histState]);

  const rows = histState === 'empty' ? [] : HISTORY_SEED;
  const filtered = rows.filter(r => {
    const q = query.trim().toLowerCase().replace(/^§/, '');
    if (!q) return true;
    return String(r.n).includes(q) || r.chap.toLowerCase().includes(q) || ('cap ' + r.chap).includes(q);
  });

  const askJump = (entry) => setConfirmTarget(entry);
  const cancelJump = () => setConfirmTarget(null);
  const doJump = () => {
    const prev = currentPara;
    setCurrentPara(confirmTarget.para);
    setToast(`Tornato a ${confirmTarget.para} · ${prev} resta in history`);
    setConfirmTarget(null);
    setOpen(false);
    setTimeout(() => setToast(null), 2600);
  };

  return { open, setOpen, query, setQuery, confirmTarget, askJump, cancelJump, doJump,
           currentPara, rows, filtered, toast, histState };
}

/* riga history */
function HistoryRow({ r, disabled, onJump }) {
  return (
    <div className="hy-row">
      <div className="hy-r-top">
        <span className="hy-para">{r.para}</span>
        <span className="hy-when">{r.when}</span>
        <span className="hy-chap">{r.chap}</span>
      </div>
      <p className="hy-snip">{r.snip}</p>
      {disabled ? (
        <div className="hy-goto-wrap"><span className="hy-tip">Disponibile online</span><button className="hy-goto" disabled>→ vai qui</button></div>
      ) : (
        <button className="hy-goto" onClick={() => onJump(r)}>→ vai qui</button>
      )}
    </div>
  );
}

/* corpo drawer condiviso */
function HistoryBody({ h, desktop }) {
  const offline = h.histState === 'offline';
  return (
    <React.Fragment>
      <div className="hy-head">
        <span className="hy-title">🕰 Paragrafi visitati</span>
        <button className="hy-x" aria-label="Chiudi" onClick={() => h.setOpen(false)}>✕</button>
      </div>

      {h.histState !== 'empty' && h.histState !== 'error' && (
        <div className="hy-search">
          <div className="hy-field">
            <span className="ic">🔍</span>
            <input placeholder="Cerca per numero o capitolo…" aria-label="Cerca paragrafo"
              value={h.query} onChange={(e) => h.setQuery(e.target.value)} />
          </div>
          <div className="hy-scope">Cerca per §numero o capitolo</div>
        </div>
      )}

      {h.histState === 'error' && (
        <div className="hy-banner err" role="alert">
          <span className="hy-bic">⚠️</span>
          <div className="hy-btx"><strong style={{ color: eVar('event') }}>Impossibile caricare la history</strong><span className="hy-sub">Controlla la connessione e riprova.</span></div>
          <button className="hy-retry">↻ Riprova</button>
        </div>
      )}
      {offline && (
        <div className="hy-banner off" role="status">
          <span className="hy-bic">📴</span>
          <div className="hy-btx"><strong style={{ color: eVar('tool') }}>Offline — history in sola lettura</strong><span className="hy-sub">Il salto sarà disponibile quando torni online.</span></div>
        </div>
      )}

      <div className="hy-current" style={h.histState === 'error' ? { opacity: 0.55 } : null}>
        <span className="hy-now-pin">📍 {h.currentPara}</span>
        <span className="hy-now-meta">Cap 4</span>
        <span className="hy-now-tag">ora</span>
      </div>

      {h.histState === 'loading' ? (
        <div className="hy-list">
          {[95, 80, 70].map((w, i) => (
            <div className="hy-skel" key={i}><div className="ln dy-shimmer" style={{ width: '45%' }}></div><div className="ln dy-shimmer" style={{ width: w + '%' }}></div><div className="ln dy-shimmer" style={{ width: '30%', marginBottom: 0 }}></div></div>
          ))}
        </div>
      ) : h.histState === 'empty' ? (
        <div className="hy-empty">
          <div className="hy-ill">🧭</div>
          <h3>Sei appena partito</h3>
          <p>Continua a leggere per popolare la history dei paragrafi visitati.</p>
        </div>
      ) : h.histState === 'error' ? null : (
        <div className="hy-list">
          {h.filtered.length === 0 ? (
            <div className="hy-empty" style={{ padding: 'var(--s-6)' }}>
              <div className="hy-ill" style={{ width: 56, height: 56, fontSize: 28 }}>🔍</div>
              <p>Nessun paragrafo per "{h.query}"</p>
            </div>
          ) : h.filtered.map(r => (
            <HistoryRow key={r.para} r={r} disabled={offline} onJump={h.askJump} />
          ))}
        </div>
      )}
    </React.Fragment>
  );
}

/* conferma jump-back (alertdialog) */
function JumpConfirm({ h }) {
  const goRef = useRef(null);
  useEffect(() => { if (goRef.current) goRef.current.focus(); }, []);
  useEffect(() => {
    const onKey = (e) => { if (e.key === 'Escape') h.cancelJump(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);
  return (
    <div className="hy-confirm-scrim" role="alertdialog" aria-modal="true" aria-labelledby="hyl-ti" aria-describedby="hyl-bd" onClick={(e) => { if (e.target === e.currentTarget) h.cancelJump(); }}>
      <div className="hy-confirm">
        <div className="hy-c-icon">↩️</div>
        <h3 id="hyl-ti">Tornare al {h.confirmTarget.para}?</h3>
        <p id="hyl-bd">Perderai la posizione corrente <span className="hy-ref">{h.currentPara}</span>. Il {h.currentPara} resterà comunque visitato nella history.</p>
        <div className="hy-c-acts">
          <button className="hy-c-cancel" onClick={h.cancelJump}>Annulla</button>
          <button className="hy-c-go" ref={goRef} onClick={h.doJump}>Sì, torna</button>
        </div>
      </div>
    </div>
  );
}

/* toast jump-confirmato (story → target) */
function HistoryToast({ text }) {
  return (
    <div style={{ position: 'absolute', left: '50%', bottom: 'var(--s-5)', transform: 'translateX(-50%)', zIndex: 60, background: eVar('session'), color: '#fff', padding: 'var(--s-2) var(--s-4)', borderRadius: 'var(--r-pill)', fontFamily: 'var(--f-display)', fontWeight: 'var(--fw-bold)', fontSize: 'var(--fs-sm)', boxShadow: 'var(--shadow-lg)', maxWidth: '86%', textAlign: 'center' }} role="status">
      ✓ {text}
    </div>
  );
}

/* shell mobile history */
function HistoryPhone({ h }) {
  const offline = h.histState === 'offline';
  return (
    <div className="phone" role="region" aria-label="Mobile · history interattiva">
      <span className="frame-label">Mobile · 375px</span>
      <div className="phone-sbar"><span>22:46</span><span>{offline ? '✈️' : '📶'} 🔋 55%</span></div>
      <div className="session-h">
        <span className="crumbs"><strong>Avalon</strong> · La grotta dei Goblin · <span className="para">{h.currentPara}</span></span>
        <button className="hy-trigger" aria-label="Paragrafi visitati" aria-expanded={h.open} onClick={() => h.setOpen(true)}>🕰</button>
      </div>
      <div className="header-pips"><span className="manapip kb"><span className="badge">47</span></span><span className="manapip session"><span className="badge">1</span></span><span className="manapip toolkit"><span className="badge">12</span></span><span className="lbl-mini">· Cap 4</span></div>
      <div className="tabs"><button className="tab" aria-selected="true"><span className="em">📖</span>Story</button><button className="tab" aria-selected="false"><span className="em">⚔️</span>Encounter</button><button className="tab" aria-selected="false"><span className="em">📚</span>Glossario</button></div>
      <div className="content">
        <div className="para-card"><div className="num-row"><span className="num">{h.currentPara}</span><span className="chap">Cap 4</span></div><p className="text">La grotta si apre davanti a te, l'aria sa di muschio e metallo. Da qualche parte gocciola dell'acqua.</p></div>
        {!h.open && (
          <p style={{ textAlign: 'center', color: 'var(--text-muted)', fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)', marginTop: 'var(--s-5)' }}>Tocca 🕰 in alto per riaprire la history</p>
        )}
      </div>
      {h.open && (
        <div className="hy-scrim" role="dialog" aria-modal="true" aria-label="Paragrafi visitati" onClick={(e) => { if (e.target === e.currentTarget) h.setOpen(false); }}>
          <div className="hy-sheet">
            <div className="hy-drag"></div>
            <HistoryBody h={h} desktop={false} />
          </div>
        </div>
      )}
      {h.confirmTarget && <JumpConfirm h={h} />}
      {h.toast && <HistoryToast text={h.toast} />}
    </div>
  );
}

/* shell desktop history */
function HistoryDesktop({ h }) {
  return (
    <div className="desktop" role="region" aria-label="Desktop · history interattiva">
      <span className="frame-label">Desktop · 1440px (scaled)</span>
      <div className="titlebar"><span className="dot r"></span><span className="dot y"></span><span className="dot g"></span><span className="url">meepleai.app/library/librogame/play/camp-avalon-1?history={h.open ? 'open' : 'closed'}</span></div>
      <div className="d-body" style={{ position: 'relative' }}>
        <aside className="d-side" style={{ width: 260, display: 'flex', flexDirection: 'column' }}>
          <div className="side-card" style={{ marginBottom: 'var(--s-2)' }}><h4>Campagna</h4><div className="row"><span className="k">Capitolo</span><span className="v">4 · {h.currentPara}</span></div></div>
          <nav aria-label="Sezioni sessione" style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <button className="dy-navitem"><span className="em">📖</span>Story<span className="cnt">{h.currentPara}</span></button>
            <button className="dy-navitem"><span className="em">⚔️</span>Encounter<span className="cnt">—</span></button>
            <button className="dy-navitem"><span className="em">📚</span>Glossario<span className="cnt">12</span></button>
            <button className="dy-navitem"><span className="em">📓</span>Diary<span className="cnt">12</span></button>
            <button className="dy-navitem hy-secondary" aria-selected={h.open} onClick={() => h.setOpen(true)}><span className="em">🕰</span>History<span className="cnt">47</span></button>
          </nav>
        </aside>
        <div className="d-main"><div className="para-card"><div className="num-row"><span className="num">{h.currentPara}</span><span className="chap">Cap 4</span></div><p className="text">La grotta si apre davanti a te, l'aria sa di muschio e metallo. Da qualche parte gocciola dell'acqua.</p></div></div>
        {h.open && (
          <div className="hy-side-host">
            <div className="hy-side"><HistoryBody h={h} desktop={true} /></div>
            <div className="hy-side-scrim" onClick={() => h.setOpen(false)}></div>
          </div>
        )}
        {h.confirmTarget && <JumpConfirm h={h} />}
        {h.toast && <HistoryToast text={h.toast} />}
      </div>
    </div>
  );
}

function HistoryLab() {
  const [histState, setHistState] = useState('default');
  const h = useHistory(histState);
  return (
    <div>
      <div className="dy-lab-bar" role="toolbar" aria-label="Stato history">
        <div className="grp">
          <span className="lbl">Stato</span>
          {HIST_STATES.map(s => (
            <button key={s.id} className={`dy-seg${histState === s.id ? ' on' : ''}`}
              aria-pressed={histState === s.id} onClick={() => setHistState(s.id)}>{s.lb}</button>
          ))}
        </div>
      </div>
      <div className="frames">
        <HistoryPhone h={h} />
        <HistoryDesktop h={h} />
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('history-lab-root')).render(<HistoryLab />);

/* ════════════════════════════════════════════════════════════════
   state-07 — End campaign (APPEND)
   Function #8. Kebab → dialog 3-vie soft → loading → summary.
   PDF CTA solo Completata. Reopen Abbandona = single-confirm (non-distruttivo).
   ════════════════════════════════════════════════════════════════ */

const CLOSE_OPTIONS = [
  { id: 'done',     e: 'toolkit', ic: '✅', ti: 'Completata', ds: 'Ho finito la storia. Genero il PDF riassunto.', cta: 'Completa campagna' },
  { id: 'archive',  e: 'session', ic: '📥', ti: 'Archivia',   ds: 'Chiudo per ora, posso riaprire quando voglio.',  cta: 'Archivia campagna' },
  { id: 'abandon',  e: 'event',   ic: '❌', ti: 'Abbandona',  ds: 'Chiudo definitivamente.', note: 'Riapri con un solo tap', cta: 'Abbandona campagna' },
];

const CAMP_STATS = [
  { v: '3h 24m', k: 'Durata' },
  { v: '47', k: 'Paragrafi visitati' },
  { v: '12', k: 'Note diary' },
  { v: '8', k: 'Foto translate' },
];

const SERVER_OUTCOMES = [
  { id: 'ok',      lb: 'Successo' },
  { id: 'error',   lb: 'Errore' },
  { id: 'offline', lb: 'Offline' },
];

function Confetti() {
  const cols = ['toolkit', 'session', 'kb', 'game', 'chat'];
  return (
    <div className="ec-confetti" aria-hidden="true">
      {Array.from({ length: 14 }).map((_, i) => (
        <i key={i} style={{ left: (5 + i * 6.5) + '%', background: eVar(cols[i % cols.length]), animationDelay: (i % 5) * 0.12 + 's' }}></i>
      ))}
    </div>
  );
}

function CloseSummary({ outcomeId, server, onReopen, onLib, onRetry, onForce }) {
  const opt = CLOSE_OPTIONS.find(o => o.id === outcomeId);
  const isDone = outcomeId === 'done';
  const isArchive = outcomeId === 'archive';
  const kind = isDone ? 'ec-done' : 'ec-arch';

  if (server === 'error') {
    return (
      <div className="ec-error" role="alert" style={{ margin: 'var(--s-4)' }}>
        <div className="ec-e-top"><span className="ec-e-ic">⚠️</span><div><h4>Impossibile chiudere — riprova</h4><p>Il server non ha risposto. La tua partita è al sicuro, niente è andato perso.</p></div></div>
        <div className="ec-e-acts"><button className="ec-e-retry" onClick={onRetry}>↻ Riprova</button><button className="ec-e-force" onClick={onForce}>Forza chiusura locale</button></div>
      </div>
    );
  }

  return (
    <React.Fragment>
      {isDone && server === 'ok' && <Confetti />}
      <div className={`ec-summary ${kind}`} style={{ position: 'relative', zIndex: 2 }}>
        <div className="ec-s-badge">{isDone ? '🏆' : '📥'}</div>
        <span className="ec-s-kick">{isDone ? 'Completata' : (server === 'offline' ? 'Archiviata · in coda' : (isArchive ? 'Archiviata' : 'Abbandonata'))}</span>
        <h2>{isDone ? 'Hai finito la storia!' : (isArchive ? 'Campagna messa via' : 'Campagna chiusa')}</h2>
        <p className="ec-s-camp">"La grotta dei Goblin"{isDone ? ' · Tainted Grail' : ' · la riprendi quando vuoi'}</p>
        {server === 'offline' && <span className="ec-pending"><span className="dot"></span>Pending sync · si completa online</span>}
        {server !== 'offline' && (
          <div className="ec-stats">
            {CAMP_STATS.map(s => (
              <div className="ec-stat" key={s.k}><div className="ec-st-v">{s.v}</div><div className="ec-st-k">{s.k}</div></div>
            ))}
            <div className="ec-stat ec-wide"><span className="ec-st-k" style={{ margin: 0 }}>Chat regola</span><span className="ec-st-v">15</span></div>
          </div>
        )}
        <div className="ec-cta-col" style={{ marginTop: server === 'offline' ? 'var(--s-5)' : 0 }}>
          {isDone && server === 'ok' && (
            <React.Fragment>
              <button className="ec-btn-pdf">📄 Scarica PDF riassunto</button>
              <span className="ec-pdf-note">Il PDF viene generato sul server · ~10s</span>
            </React.Fragment>
          )}
          {!isDone && server === 'ok' && (
            <button className="ec-btn-reopen" onClick={onReopen}>▶️ Riapri campagna</button>
          )}
          <button className="ec-btn-lib" onClick={onLib}>🏠 Torna alla libreria</button>
        </div>
      </div>
    </React.Fragment>
  );
}

/* dialog 3-vie */
function CloseDialog({ centered, selected, onSelect, onCancel, onConfirm }) {
  const dRef = useRef(null);
  useEffect(() => {
    const onKey = (e) => { if (e.key === 'Escape') onCancel(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);
  const opt = CLOSE_OPTIONS.find(o => o.id === selected);
  return (
    <div className={`ec-scrim${centered ? ' ec-center' : ''}`} role="alertdialog" aria-modal="true" aria-labelledby="ecl-ti" onClick={(e) => { if (e.target === e.currentTarget) onCancel(); }}>
      <div className="ec-dialog" ref={dRef}>
        {!centered && <div className="ec-grab"></div>}
        <h3 id="ecl-ti">Come vuoi chiudere?</h3>
        <p className="ec-d-sub">"La grotta dei Goblin" · Cap 4 · §214</p>
        <div className="ec-choices" role="radiogroup" aria-label="Modalità di chiusura">
          {CLOSE_OPTIONS.map(o => (
            <button key={o.id} className="ec-choice" style={{ '--e': `var(--c-${o.e})` }}
              role="radio" aria-checked={selected === o.id} aria-pressed={selected === o.id}
              onClick={() => onSelect(o.id)}>
              <span className="ec-c-ic">{o.ic}</span>
              <span className="ec-c-tx">
                <span className="ec-c-ti">{o.ti}</span>
                <span className="ec-c-ds">{o.ds}</span>
                {o.note && <span className="ec-c-note">{o.note}</span>}
              </span>
              <span className="ec-c-radio">{selected === o.id ? '✓' : ''}</span>
            </button>
          ))}
        </div>
        <div className="ec-d-acts">
          <button className="ec-d-cancel" onClick={onCancel}>Annulla</button>
          <button className="ec-d-confirm" style={{ '--e': `var(--c-${opt ? opt.e : 'session'})` }} disabled={!selected} onClick={onConfirm}>
            {opt ? opt.cta : 'Conferma'}
          </button>
        </div>
      </div>
    </div>
  );
}

function useEndCampaign(server) {
  const [phase, setPhase] = useState('idle'); // idle | kebab | dialog | loading | summary
  const [selected, setSelected] = useState('done');
  const [outcome, setOutcome] = useState(null);

  useEffect(() => { setPhase('idle'); setSelected('done'); setOutcome(null); }, [server]);

  const confirm = () => {
    setPhase('loading');
    setTimeout(() => {
      setOutcome(selected);
      setPhase('summary');
    }, server === 'ok' ? 1100 : 900);
  };
  const retry = () => { setPhase('loading'); setTimeout(() => setPhase('summary'), 1000); };
  const reset = () => { setPhase('idle'); setOutcome(null); };

  return { phase, setPhase, selected, setSelected, outcome, confirm, retry, reset };
}

function KebabMenu({ onClose }) {
  return (
    <div className="ec-kebab" role="menu" aria-label="Impostazioni sessione">
      <div className="ec-k-lbl">Impostazioni sessione</div>
      <button className="ec-kitem" role="menuitem">📦<span style={{ marginLeft: 6 }}>Esporta dati</span></button>
      <div className="ec-k-div"></div>
      <button className="ec-kitem ec-target" role="menuitem" onClick={onClose}>🏁<span style={{ marginLeft: 6 }}>Chiudi campagna</span></button>
      <button className="ec-kitem" role="menuitem">⚙️<span style={{ marginLeft: 6 }}>Preferenze</span></button>
    </div>
  );
}

function EndPhone({ ec, server }) {
  // error/offline: dopo loading mostriamo summary che internamente rende error/pending
  const showSummary = ec.phase === 'summary';
  return (
    <div className="phone" role="region" aria-label="Mobile · end campaign interattivo">
      <span className="frame-label">Mobile · 375px</span>
      <div className="phone-sbar"><span>23:02</span><span>{server === 'offline' ? '✈️' : '📶'} 🔋 50%</span></div>
      <div className="session-h">
        <span className="crumbs"><strong>Avalon</strong> · La grotta dei Goblin · <span className="para">§214</span></span>
        {!showSummary && <button className="menu" aria-label="Menu sessione" aria-expanded={ec.phase === 'kebab'} onClick={() => ec.setPhase(ec.phase === 'kebab' ? 'idle' : 'kebab')}>⋮</button>}
      </div>
      {!showSummary && (
        <React.Fragment>
          <div className="header-pips"><span className="manapip kb"><span className="badge">47</span></span><span className="manapip session"><span className="badge">1</span></span><span className="manapip toolkit"><span className="badge">12</span></span></div>
          <div className="tabs"><button className="tab" aria-selected="true"><span className="em">📖</span>Story</button><button className="tab" aria-selected="false"><span className="em">⚔️</span>Encounter</button><button className="tab" aria-selected="false"><span className="em">📚</span>Glossario</button></div>
        </React.Fragment>
      )}
      <div className="content" style={showSummary ? { padding: 0, position: 'relative' } : { position: 'relative' }}>
        {!showSummary && ec.phase !== 'dialog' && (
          <div className="para-card"><div className="num-row"><span className="num">§214</span><span className="chap">Cap 4</span></div><p className="text">La grotta si apre davanti a te, l'aria sa di muschio e metallo. Da qualche parte gocciola dell'acqua.</p></div>
        )}
        {showSummary && (
          <CloseSummary outcomeId={ec.outcome} server={server}
            onReopen={ec.reset} onLib={ec.reset} onRetry={ec.retry} onForce={() => { /* forza locale */ ec.reset(); }} />
        )}
      </div>
      {ec.phase === 'kebab' && <KebabMenu onClose={() => ec.setPhase('dialog')} />}
      {ec.phase === 'dialog' && (
        <CloseDialog selected={ec.selected} onSelect={ec.setSelected} onCancel={() => ec.setPhase('idle')} onConfirm={ec.confirm} />
      )}
      {ec.phase === 'loading' && (
        <div className="ec-loading" role="status" aria-live="polite">
          <div className="ec-spinner"></div>
          <span className="ec-l-tx">Chiudendo la campagna…</span>
          <span className="ec-l-sub">{ec.selected === 'done' ? 'Genero il riassunto e blocco i paragrafi' : 'Salvo lo stato della sessione'}</span>
        </div>
      )}
    </div>
  );
}

function EndDesktop({ ec, server }) {
  const showSummary = ec.phase === 'summary';
  return (
    <div className="desktop" role="region" aria-label="Desktop · end campaign interattivo">
      <span className="frame-label">Desktop · 1440px (scaled)</span>
      <div className="titlebar"><span className="dot r"></span><span className="dot y"></span><span className="dot g"></span><span className="url">meepleai.app/library/librogame/play/camp-avalon-1{showSummary ? '/summary' : '?close=' + (ec.phase === 'dialog' ? '1' : '0')}</span></div>
      <div className="d-body" style={{ position: 'relative', justifyContent: showSummary ? 'center' : undefined, alignItems: showSummary ? 'center' : undefined }}>
        {!showSummary && (
          <React.Fragment>
            <aside className="d-side" style={{ width: 260, display: 'flex', flexDirection: 'column' }}>
              <div className="side-card" style={{ marginBottom: 'var(--s-2)' }}><h4>Campagna</h4><div className="row"><span className="k">Capitolo</span><span className="v">4 · §214</span></div></div>
              <nav style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                <button className="dy-navitem"><span className="em">📖</span>Story<span className="cnt">§214</span></button>
                <button className="dy-navitem"><span className="em">📓</span>Diary<span className="cnt">12</span></button>
                <button className="dy-navitem"><span className="em">🕰</span>History<span className="cnt">47</span></button>
              </nav>
              <div className="dy-d-foot"><button className="dy-endbtn" style={{ background: eVar('session', 0.12), color: eVar('session'), borderColor: eVar('session', 0.3) }} onClick={() => ec.setPhase('dialog')}>🏁 Chiudi campagna</button></div>
            </aside>
            <div className="d-main"><div className="para-card"><div className="num-row"><span className="num">§214</span><span className="chap">Cap 4</span></div><p className="text">La grotta si apre davanti a te, l'aria sa di muschio e metallo. Da qualche parte gocciola dell'acqua.</p></div></div>
          </React.Fragment>
        )}
        {showSummary && (
          <div style={{ width: '100%', maxWidth: 480, position: 'relative' }}>
            <CloseSummary outcomeId={ec.outcome} server={server}
              onReopen={ec.reset} onLib={ec.reset} onRetry={ec.retry} onForce={ec.reset} />
          </div>
        )}
        {ec.phase === 'dialog' && (
          <CloseDialog centered selected={ec.selected} onSelect={ec.setSelected} onCancel={() => ec.setPhase('idle')} onConfirm={ec.confirm} />
        )}
        {ec.phase === 'loading' && (
          <div className="ec-loading" role="status" aria-live="polite"><div className="ec-spinner"></div><span className="ec-l-tx">Chiudendo la campagna…</span><span className="ec-l-sub">{ec.selected === 'done' ? 'Genero il riassunto e blocco i paragrafi' : 'Salvo lo stato della sessione'}</span></div>
        )}
      </div>
    </div>
  );
}

function EndCampaignLab() {
  const [server, setServer] = useState('ok');
  const ec = useEndCampaign(server);
  return (
    <div>
      <div className="dy-lab-bar" role="toolbar" aria-label="Esito server alla conferma">
        <div className="grp">
          <span className="lbl">Esito server</span>
          {SERVER_OUTCOMES.map(s => (
            <button key={s.id} className={`dy-seg${server === s.id ? ' on' : ''}`}
              aria-pressed={server === s.id} onClick={() => setServer(s.id)}>{s.lb}</button>
          ))}
        </div>
        <button className="dy-seg" style={{ border: '1px solid var(--border)' }} onClick={ec.reset}>↺ Reset flow</button>
      </div>
      <div className="frames">
        <EndPhone ec={ec} server={server} />
        <EndDesktop ec={ec} server={server} />
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('endcampaign-lab-root')).render(<EndCampaignLab />);
