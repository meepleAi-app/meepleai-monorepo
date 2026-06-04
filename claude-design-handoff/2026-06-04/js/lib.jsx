/* MeepleAI Nav Prototype — shared primitives, GAP audit store, state patterns.
   QA tooling layer. Exports to window for cross-file babel scope. */
const { useState, useEffect, useRef, useCallback, useSyncExternalStore } = React;
const DS = window.DS;
const h = React.createElement;

/* ─────────── Entity helpers ─────────── */
const ENT_CLASS = {
  game: 'e-game', player: 'e-player', session: 'e-session', agent: 'e-agent',
  kb: 'e-kb', chat: 'e-chat', event: 'e-event', toolkit: 'e-toolkit', tool: 'e-tool',
};
const entEmoji = (t) => (DS.EC[t] || {}).em || '•';
const entLabel = (t) => (DS.EC[t] || {}).lb || t;
const relTimeRank = 0;

/* ─────────── GAP audit store ───────────
   Five categories per brief. Components register gaps on mount so the topbar
   counter and the slide-over panel always reflect what's actually rendered. */
const GAP_CATS = {
  'GAP-ROUTE':   'Route mancante o semantica ambigua',
  'GAP-STATE':   'Stato non coperto dal mockup',
  'GAP-CTA':     'Link/button verso destinazione inesistente',
  'GAP-ENTITY':  'Cross-reference o regola entity non definita',
  'GAP-TOKEN':   'Valore inventato non in tokens.css',
  'GAP-DATA':    'Dataset fixture insufficiente / campo assente',
  'GAP-FEATURE': 'Feature referenziata ma non costruita',
};
const gapStore = (() => {
  let items = [];
  let listeners = new Set();
  const emit = () => { items = items.slice(); listeners.forEach(l => l()); };
  return {
    register(entry) {
      // entry: {id, cat, loc, note}
      if (items.find(i => i.id === entry.id)) return () => {};
      items.push(entry); emit();
      return () => { items = items.filter(i => i.id !== entry.id); emit(); };
    },
    subscribe(l) { listeners.add(l); return () => listeners.delete(l); },
    get() { return items; },
  };
})();

/* Inline gap badge — registers itself to the store. */
function Gap({ cat, loc, note, block, mini, children }) {
  const id = useRef('gap-' + Math.random().toString(36).slice(2)).current;
  useEffect(() => gapStore.register({ id, cat, loc: loc || cat, note: note || '' }), []);
  if (mini) {
    return h('span', { className: 'gap-badge mini', title: '[' + cat + '] ' + (GAP_CATS[cat] || '') + (note ? ' — ' + note : '') },
      '\u26A0', h('span', { className: 'cat' }, cat.replace('GAP-', '')));
  }
  return h('span', { className: 'gap-badge' + (block ? ' block' : ''), title: (GAP_CATS[cat] || '') + (note ? ' — ' + note : '') },
    h('span', { className: 'cat' }, '[' + cat + ']'),
    children ? h('span', null, children) : (note ? h('span', null, note) : null)
  );
}

function useGaps() {
  return useSyncExternalStore(gapStore.subscribe, gapStore.get, gapStore.get);
}

/* ─────────── Slide animation (Web Animations API) ───────────
   CSS transform transitions freeze at their start value in the hidden preview
   iframe. So the resting OPEN/CLOSED state is governed by the `.open` class
   (no CSS transition → instant correct end-state, always visible), and the
   250ms motion is layered on with element.animate(), which reaches its end
   state even when declarative transitions don't fire. If WAAPI is unavailable
   the class snap keeps the panel functional. */
const EASE_OUT = 'cubic-bezier(.16, 1, .3, 1)';
function animateSlide(el, fromX, toX, ms) {
  if (!el || typeof el.animate !== 'function') return;
  try {
    el.getAnimations().forEach(a => a.cancel());
    const a = el.animate([{ transform: 'translateX(' + fromX + ')' }, { transform: 'translateX(' + toX + ')' }],
      { duration: ms, easing: EASE_OUT });
    // No fill: the .open class holds the resting state. Safety cancel un-pins
    // the animation if the iframe's clock is frozen (start keyframe would
    // otherwise override the class indefinitely).
    setTimeout(() => { try { a.cancel(); } catch (e) {} }, ms + 100);
  } catch (e) {}
}
function animateFade(el, from, to, ms) {
  if (!el || typeof el.animate !== 'function') return;
  try {
    el.getAnimations().forEach(a => a.cancel());
    const a = el.animate([{ opacity: from }, { opacity: to }], { duration: ms });
    setTimeout(() => { try { a.cancel(); } catch (e) {} }, ms + 100);
  } catch (e) {}
}

/* Generic slide-over controller: keeps content mounted through the exit and
   drives in/out motion via WAAPI. Attach panelRef to the sliding element and
   scrimRef to the backdrop. `active` is the desired-open boolean. */
function useSlideAnim(active, ms = 250) {
  const [mounted, setMounted] = useState(active);
  const panelRef = useRef(null);
  const scrimRef = useRef(null);
  const prev = useRef(active);
  useEffect(() => { if (active) setMounted(true); }, [active]);
  useEffect(() => {
    if (active && mounted) { animateSlide(panelRef.current, '100%', '0', ms); animateFade(scrimRef.current, 0, 1, ms); }
  }, [active, mounted]);
  useEffect(() => {
    if (!active && prev.current) {
      animateSlide(panelRef.current, '0', '100%', ms);
      animateFade(scrimRef.current, 1, 0, ms);
      const t = setTimeout(() => setMounted(false), ms + 40);
      prev.current = active;
      return () => clearTimeout(t);
    }
    prev.current = active;
  }, [active]);
  return { mounted, panelRef, scrimRef };
}

/* ─────────── Entity primitives ─────────── */
function Pip({ id, size, onOpen }) {
  const e = DS.byId[id];
  if (!e) return null;
  const cls = ENT_CLASS[e.type] || 'e-game';
  return h('button', {
    className: 'pip ' + (size || '') + ' ' + cls,
    title: entLabel(e.type) + ' · ' + e.title,
    onClick: (ev) => { ev.stopPropagation(); onOpen && onOpen(id); },
  }, e.coverEmoji || entEmoji(e.type));
}

function PipStack({ ids, max = 4, onOpen }) {
  const shown = ids.slice(0, max);
  const extra = ids.length - shown.length;
  return h('div', { className: 'pip-stack' },
    shown.map(id => h(Pip, { key: id, id, size: 'sm', onOpen })),
    extra > 0 ? h('span', { className: 'pip-more' }, '+' + extra) : null
  );
}

function Chip({ type, label, onClick }) {
  const cls = ENT_CLASS[type] || 'e-game';
  return h('button', { className: 'chip-btn ' + cls, onClick: (e) => { e.stopPropagation(); onClick && onClick(); } },
    h('span', null, entEmoji(type)),
    h('span', null, label || entLabel(type))
  );
}

/* ─────────── Connection builder ───────────
   Resolve cross-references in data.js into a pip list for the drawer connection bar.
   Returns [{type, ids[], count, label, isEmpty}]. */
function buildConnections(e) {
  const g = DS;
  const byGame = (arr, gid) => arr.filter(x => x.gameId === gid).map(x => x.id);
  let rows = [];
  if (e.type === 'game') {
    rows = [
      { type: 'agent',   ids: byGame(g.agents, e.id),  label: 'Agenti' },
      { type: 'kb',      ids: byGame(g.kbs, e.id),      label: 'Documenti' },
      { type: 'toolkit', ids: byGame(g.toolkits, e.id), label: 'Toolkit' },
      { type: 'session', ids: byGame(g.sessions, e.id), label: 'Partite' },
      { type: 'chat',    ids: byGame(g.chats, e.id),    label: 'Chat' },
    ];
  } else if (e.type === 'agent') {
    rows = [
      { type: 'game',    ids: e.gameId ? [e.gameId] : [], label: 'Gioco' },
      { type: 'kb',      ids: e.gameId ? byGame(g.kbs, e.gameId) : [], label: 'Knowledge' },
      { type: 'chat',    ids: g.chats.filter(c => c.agentId === e.id).map(c => c.id), label: 'Chat' },
    ];
  } else if (e.type === 'session') {
    rows = [
      { type: 'game',    ids: e.gameId ? [e.gameId] : [], label: 'Gioco' },
      { type: 'player',  ids: e.playerIds || [], label: 'Giocatori' },
      { type: 'chat',    ids: g.chats.filter(c => c.gameId === e.gameId).map(c => c.id), label: 'Chat' },
    ];
  } else if (e.type === 'player') {
    const psess = g.sessions.filter(s => (s.playerIds || []).includes(e.id));
    const pgames = [...new Set(psess.map(s => s.gameId).filter(Boolean))];
    rows = [
      { type: 'session', ids: psess.map(s => s.id), label: 'Partite' },
      { type: 'game',    ids: pgames, label: 'Giochi' },
      { type: 'event',   ids: g.events.filter(ev => (ev.participantIds || []).includes(e.id)).map(ev => ev.id), label: 'Serate' },
    ];
  } else if (e.type === 'kb') {
    rows = [
      { type: 'game',    ids: e.gameId ? [e.gameId] : [], label: 'Gioco' },
      { type: 'agent',   ids: e.gameId ? byGame(g.agents, e.gameId) : [], label: 'Usato da' },
    ];
  } else if (e.type === 'chat') {
    rows = [
      { type: 'agent',   ids: e.agentId ? [e.agentId] : [], label: 'Agente' },
      { type: 'game',    ids: e.gameId ? [e.gameId] : [], label: 'Gioco' },
    ];
  } else if (e.type === 'event') {
    rows = [
      { type: 'game',    ids: e.gameIds || [], label: 'Lineup' },
      { type: 'player',  ids: e.participantIds || [], label: 'Invitati' },
    ];
  } else if (e.type === 'toolkit') {
    rows = [
      { type: 'game',    ids: e.gameId ? [e.gameId] : [], label: 'Gioco' },
      { type: 'tool',    ids: g.tools.filter(t => t.toolkitId === e.id).map(t => t.id), label: 'Strumenti' },
      { type: 'player',  ids: e.owner ? [e.owner] : [], label: 'Autore' },
    ];
  } else if (e.type === 'tool') {
    rows = [
      { type: 'toolkit', ids: e.toolkitId ? [e.toolkitId] : [], label: 'Toolkit' },
    ];
  }
  return rows.map(r => ({ ...r, count: r.ids.length, isEmpty: r.ids.length === 0 }));
}

/* ─────────── State patterns (desktop) ───────────
   Sourced from state-matrix.html (empty / error / loading / offline).
   `entity` drives the accent color; `gapState` flags states absent from the
   original mockup (which, for screens without a JSX twin, is all of them). */
function EmptyState({ entity, icon, title, desc, cta }) {
  return h('div', { className: 'state-block ' + (ENT_CLASS[entity] || '') },
    h('div', { className: 'sb-icon' }, icon),
    h('h2', null, title),
    desc ? h('p', null, desc) : null,
    cta ? h('button', { className: 'sb-cta' }, cta) : null
  );
}
function ErrorState({ title, msg, primary, secondary }) {
  return h('div', { style: { padding: 'var(--s-9) 0' } },
    h('div', { className: 'err-card' },
      h('div', { className: 'ec-head' }, h('span', null, '\u26A0'), h('span', null, title || 'Errore di caricamento')),
      h('div', { className: 'ec-msg' }, msg || 'Non riesco a caricare questa vista. Verifica la connessione.'),
      h('div', { className: 'ec-actions' },
        h('button', { className: 'btn-danger' }, primary || 'Riprova'),
        h('button', { className: 'btn-outline' }, secondary || 'Segnala')
      )
    )
  );
}
function SkeletonLine({ w, hgt }) {
  return h('div', { className: 'skel', style: { width: (w || 100) + '%', height: (hgt || 14) + 'px', marginBottom: 'var(--s-2)' } });
}
function SkeletonCard({ hgt }) {
  return h('div', { className: 'skel', style: { height: (hgt || 96) + 'px', borderRadius: 'var(--r-xl)' } });
}
function OfflineBar({ note }) {
  return h('div', { className: 'offline-bar' },
    h('span', null, '\uD83D\uDCE1 Offline — ' + (note || 'dati dalla cache locale')),
    h('button', { className: 'retry' }, 'Riprova')
  );
}

/* expose */
Object.assign(window, {
  DS, h, ENT_CLASS, entEmoji, entLabel,
  GAP_CATS, gapStore, Gap, useGaps, useSlideAnim, animateSlide, animateFade,
  Pip, PipStack, Chip, buildConnections,
  EmptyState, ErrorState, SkeletonLine, SkeletonCard, OfflineBar,
});
