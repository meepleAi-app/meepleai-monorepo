/* MeepleAI SP4 wave 4 — S · #1490 · 2/4 — sp4-toolkit-history
   Route: /toolkit/history — Lista paginata di sessioni finalizzate cross-game con filtri
          (game / date range / winner) + detail modal + pagination.
   File: admin-mockups/design_files/sp4-toolkit-history.{html,jsx}
   Pattern: Hero + body con tabella filtrabile (riusa list view sp4-library-desktop +
            filter chips / status badge da sp4-editor-proposals-index #1489) + detail modal.
            NO sidebar, NO split-view. Desktop = table, mobile = cards stack + bottom-sheet modal.

   Source restyle (NO ridisegnare logica): apps/web/src/app/(authenticated)/toolkit/history/client.tsx
   API: api.sessions.getHistory({ gameId?, startDate?, endDate? }) →
        { sessions[{id,gameId,gameName,date,durationMinutes,players[{name,score,isWinner}],winner,notes}],
          totalCount, pageSize:20, currentPage }

   Entity: --c-toolkit (verde, continuity con S1) primaria · --c-game per i giochi (EntityChip 🎲) ·
           --c-player per i giocatori (avatar) · --c-success per il winner (🏆).

   9 stati (state picker continuity con #1489 + #1490 S1, persistito localStorage `th-state`):
   default · filter-game · filter-multi · filter-empty · pagination-3 · modal-detail · loading · error · mobile-cards

   Deviazioni flaggate: punteggi Catan nel modal riportati su scala realistica del gioco (~10-13 VP,
   coerente con data.js avgScore:9) invece del valore 87 del brief — mantiene "dati realistici" (_common.md).
*/

const { useState, useEffect, useMemo, useRef, useCallback } = React;
const DS = window.DS;

const eHsl = (type, a) => {
  const c = DS.EC[type] || DS.EC.toolkit;
  return a !== undefined ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${a})` : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};
const sem = (name, a) => a !== undefined ? `hsl(var(--c-${name}) / ${a})` : `hsl(var(--c-${name}))`;

const MONTHS_SHORT = ['gen','feb','mar','apr','mag','giu','lug','ago','set','ott','nov','dic'];
const MONTHS_FULL  = ['gennaio','febbraio','marzo','aprile','maggio','giugno','luglio','agosto','settembre','ottobre','novembre','dicembre'];
const PAD = n => String(n).padStart(2, '0');
const fmtDate = d => `${d.getDate()} ${MONTHS_SHORT[d.getMonth()]} ${d.getFullYear()}`;
const fmtTime = d => `${PAD(d.getHours())}:${PAD(d.getMinutes())}`;
const fmtDur = m => { const h = Math.floor(m / 60), mm = m % 60; return h ? (mm ? `${h}h ${mm}m` : `${h}h`) : `${mm}m`; };
function relDate(d, now) {
  const days = Math.round((now - d) / 86400000);
  if (days <= 0) return 'oggi';
  if (days === 1) return 'ieri';
  if (days < 7) return `${days} giorni fa`;
  if (days < 30) return `${Math.floor(days / 7)} settimane fa`;
  if (days < 365) return `${Math.floor(days / 30)} mesi fa`;
  return `${Math.floor(days / 365)} anni fa`;
}

// ═══════════════════════════════════════════════════════
// ─── FIXTURE — 156 sessioni deterministiche (cross-ref data.js) ──
// ═══════════════════════════════════════════════════════
const NOW = new Date(2026, 4, 28, 16, 5); // 28 mag 2026
const PLAYER_POOL = ['p-marco', 'p-sara', 'p-luca', 'p-giulia', 'p-andrea'];
const ME = 'Marco R.';

// score range realistico per gioco (coerente con data.js avgScore)
const GAME_CFG = {
  'g-catan':    { w: 22, range: [7, 14],    dur: [62, 128], coop: false },
  'g-wingspan': { w: 18, range: [62, 102],  dur: [44, 78],  coop: false },
  'g-azul':     { w: 16, range: [48, 95],   dur: [28, 52],  coop: false },
  'g-7wonders': { w: 14, range: [44, 82],   dur: [26, 38],  coop: false },
  'g-brass':    { w: 11, range: [104, 162], dur: [88, 142], coop: false },
  'g-arknova':  { w: 8,  range: [88, 142],  dur: [96, 152], coop: false },
  'g-spirit':   { w: 6,  range: [0, 0],     dur: [92, 124], coop: true },
};
const GAME_IDS = Object.keys(GAME_CFG);
// weighted bag for deterministic distribution
const GAME_BAG = GAME_IDS.flatMap(id => Array(GAME_CFG[id].w).fill(id));

const NOTE_SNIPS = [
  'Partita combattutissima fino all’ultimo turno.',
  'Apertura aggressiva ripagata nel finale.',
  'Da rivedere la gestione delle risorse a metà partita.',
  'Vittoria di misura, scarto minimo in classifica.',
  'Bella rimonta nel secondo tempo.',
  'Setup lento ma finale spettacolare.',
  'Buon equilibrio tra i giocatori, decisa ai tie-break.',
  'Partita di rodaggio con un nuovo giocatore al tavolo.',
];

// PRNG deterministico (mulberry32)
function mk(seed) { return () => { seed |= 0; seed = (seed + 0x6D2B79F5) | 0; let t = Math.imul(seed ^ (seed >>> 15), 1 | seed); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }

function buildSessions() {
  const rng = mk(20260528);
  const out = [];
  let cursor = new Date(NOW.getTime() - 4 * 3600000); // most recent ~ 4h ago
  for (let i = 0; i < 156; i++) {
    const gameId = i === 0 ? 'g-catan' : GAME_BAG[Math.floor(rng() * GAME_BAG.length)];
    const cfg = GAME_CFG[gameId];
    const g = DS.byId[gameId];
    // step back 0.4–4 days between sessions
    if (i > 0) cursor = new Date(cursor.getTime() - (0.4 + rng() * 3.6) * 86400000);
    const date = new Date(cursor);

    let nP = i === 0 ? 4 : 2 + Math.floor(rng() * 3); // 2–4
    if (gameId === 'g-7wonders') nP = 2; // duel
    if (gameId === 'g-arknova' && nP > 4) nP = 4;
    // pick distinct players, Marco present ~65%
    const pool = [...PLAYER_POOL];
    const picks = [];
    const marcoIn = i === 0 ? true : rng() < 0.62;
    if (marcoIn) { picks.push('p-marco'); pool.splice(pool.indexOf('p-marco'), 1); }
    while (picks.length < nP && pool.length) picks.push(pool.splice(Math.floor(rng() * pool.length), 1)[0]);

    const [lo, hi] = cfg.range;
    const players = picks.map(pid => ({
      pid, name: DS.byId[pid].title, initials: DS.byId[pid].initials, color: DS.byId[pid].color,
      score: cfg.coop ? 0 : lo + Math.floor(rng() * (hi - lo + 1)),
    }));
    if (i === 0) {
      // sessione Catan in evidenza: Marco R. vince con 12 PV (coerente con timeline + note del modal)
      const fixed = [['p-marco', 12], ['p-sara', 10], ['p-luca', 9], ['p-giulia', 8]];
      players.length = 0;
      fixed.forEach(([pid, sc]) => players.push({ pid, name: DS.byId[pid].title, initials: DS.byId[pid].initials, color: DS.byId[pid].color, score: sc }));
    }
    let winner = null, winScore = null;
    if (!cfg.coop) {
      let best = players[0];
      players.forEach(p => { if (p.score > best.score) best = p; });
      best.isWinner = true; winner = best.name; winScore = best.score;
    }
    const dur = cfg.dur[0] + Math.floor(rng() * (cfg.dur[1] - cfg.dur[0] + 1));
    const hasNote = i === 0 || rng() < 0.34;
    const note = i === 0
      ? 'Ottima partita! Marco si è difeso bene dal ladro. Da ripetere in 4 giocatori.'
      : hasNote ? NOTE_SNIPS[Math.floor(rng() * NOTE_SNIPS.length)] : null;

    out.push({
      id: `s-${PAD(i)}-${gameId.slice(2)}`,
      gameId, gameName: g.title, cover: g.cover, coverEmoji: g.coverEmoji,
      date, durationMinutes: dur, players, winner, winScore,
      coop: cfg.coop, notes: note, mine: winner === ME,
      turns: 8 + Math.floor(rng() * 14),
    });
  }
  return out;
}
const SESSIONS = buildSessions();
const TOTAL = SESSIONS.length;
const PAGE_SIZE = 20;

// derived counts for filters
const GAME_COUNTS = GAME_IDS.map(id => ({ id, count: SESSIONS.filter(s => s.gameId === id).length }))
  .sort((a, b) => b.count - a.count);
const WINNER_COUNTS = (() => {
  const m = {};
  SESSIONS.forEach(s => { if (s.winner) m[s.winner] = (m[s.winner] || 0) + 1; });
  const noWin = SESSIONS.filter(s => !s.winner).length;
  const arr = Object.entries(m).map(([name, count]) => ({ name, count })).sort((a, b) => b.count - a.count);
  return { players: arr.slice(0, 4), noWin };
})();
const N_GAMES = GAME_IDS.length;

// timeline IT per il modal (bespoke per sessione 0 = Catan, generico per le altre)
const CATAN_TL = [
  { t: '15:42', icon: '🎲', e: 'Setup completato · 4 giocatori al tavolo' },
  { t: '15:48', icon: '🏘️', e: 'Marco R. costruisce la prima città' },
  { t: '16:23', icon: '🃏', e: 'Sara T. attiva una carta sviluppo' },
  { t: '17:06', icon: '🦹', e: 'Luca B. ruba 2 carte risorsa (ladro)' },
  { t: '17:08', icon: '🏁', e: 'Fine partita · Marco R. raggiunge 12 PV' },
];
function genTimeline(s) {
  if (s.id === SESSIONS[0].id) return CATAN_TL;
  const start = new Date(s.date.getTime());
  const end = new Date(start.getTime() + s.durationMinutes * 60000);
  const mid = new Date(start.getTime() + s.durationMinutes * 0.5 * 60000);
  const lead = s.players[0], second = s.players[1] || s.players[0];
  return [
    { t: fmtTime(start), icon: '🎲', e: `Setup completato · ${s.players.length} giocatori` },
    { t: fmtTime(new Date(start.getTime() + 8 * 60000)), icon: '▶️', e: `${lead.name} apre la partita` },
    { t: fmtTime(mid), icon: '⚡', e: `${second.name} prende il vantaggio a metà partita` },
    { t: fmtTime(end), icon: '🏁', e: s.coop ? 'Fine partita · risultato cooperativo' : `Fine partita · vince ${s.winner}` },
  ];
}

// ═══════════════════════════════════════════════════════
// ─── COMPONENT CSS (inject) — solo token da tokens.css ──
// ═══════════════════════════════════════════════════════
const TH_CSS = `
.th-app { display:flex; flex-direction:column; height:100%; min-height:0; background:var(--bg); color:var(--text); position:relative; overflow:hidden; }

/* ─ error banner ─ */
.th-errbar { flex-shrink:0; display:flex; align-items:center; gap:11px; padding:11px 18px; font-family:var(--f-display); font-weight:700; font-size:13px;
  background:hsl(var(--c-danger) / .12); color:hsl(var(--c-danger)); border-bottom:1px solid hsl(var(--c-danger) / .3); }
.th-errbar .grow { flex:1; }
.th-errbar .retry { display:inline-flex; align-items:center; gap:6px; padding:6px 13px; border-radius:var(--r-md); border:1px solid hsl(var(--c-danger) / .4);
  background:var(--bg-card); color:hsl(var(--c-danger)); font-family:var(--f-display); font-weight:700; font-size:13px; cursor:pointer; }

/* ─ header (sticky) ─ */
.th-head { flex-shrink:0; position:sticky; top:0; z-index:12; background:var(--glass-bg); backdrop-filter:blur(14px); border-bottom:1px solid var(--border); padding:14px 22px 0; }
.th-htop { display:flex; align-items:flex-start; gap:16px; }
.th-htxt { min-width:0; flex:1; }
.th-bread { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); letter-spacing:.04em; display:flex; align-items:center; gap:6px; margin-bottom:7px; }
.th-bread .sep { opacity:.5; }
.th-bread .cur { color:var(--text-sec); font-weight:700; }
.th-titlerow { display:flex; align-items:center; gap:10px; }
.th-ico { width:34px; height:34px; flex-shrink:0; border-radius:var(--r-md); background:hsl(var(--c-toolkit) / .16); color:hsl(var(--c-toolkit));
  display:inline-flex; align-items:center; justify-content:center; font-size:18px; }
.th-h1 { font-family:var(--f-display); font-weight:800; font-size:30px; letter-spacing:-.02em; line-height:1.1; color:var(--text); white-space:nowrap; }
.th-sub { font-size:14px; color:var(--text-sec); margin-top:5px; max-width:560px; }
.th-hright { display:flex; flex-direction:column; align-items:flex-end; gap:10px; flex-shrink:0; }
.th-qstat { display:inline-flex; align-items:center; gap:7px; font-family:var(--f-mono); font-size:11px; color:var(--text-muted); white-space:nowrap; }
.th-qstat b { color:var(--text-sec); font-weight:700; }
.th-export { display:inline-flex; align-items:center; gap:7px; padding:8px 14px; border-radius:var(--r-md); background:var(--bg-card);
  border:1px solid var(--border-strong); color:var(--text); font-family:var(--f-display); font-weight:800; font-size:13px; cursor:pointer; white-space:nowrap;
  transition:all var(--dur-sm) var(--ease-out); }
.th-export:hover { border-color:hsl(var(--c-toolkit) / .5); color:hsl(var(--c-toolkit)); }

/* ─ tabs nav ─ */
.th-tabs { display:flex; gap:4px; margin-top:14px; overflow-x:auto; scrollbar-width:none; }
.th-tabs::-webkit-scrollbar { display:none; }
.th-tab { display:inline-flex; align-items:center; gap:6px; padding:9px 14px 11px; border:none; background:transparent; cursor:pointer; white-space:nowrap;
  border-bottom:2px solid transparent; color:var(--text-muted); font-family:var(--f-display); font-weight:700; font-size:13px; transition:color var(--dur-sm); }
.th-tab:hover { color:var(--text-sec); }
.th-tab.on { color:hsl(var(--c-toolkit)); border-bottom-color:hsl(var(--c-toolkit)); }

/* ─ toolbar ─ */
.th-toolbar { flex-shrink:0; display:flex; align-items:center; gap:12px; padding:11px 22px; background:var(--bg); border-bottom:1px solid var(--border); flex-wrap:wrap; row-gap:10px; }
.th-search { flex:1 1 210px; max-width:290px; min-width:150px; position:relative; }
.th-search .ic { position:absolute; left:11px; top:50%; transform:translateY(-50%); font-size:13px; opacity:.6; pointer-events:none; }
.th-search input { width:100%; padding:8px 64px 8px 32px; border-radius:var(--r-md); border:1.5px solid var(--border);
  background:var(--bg-card); font-family:var(--f-body); font-size:13px; color:var(--text); outline:none; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.th-search input::placeholder { color:var(--text-muted); }
.th-search input:focus, .th-search.active input { border-color:hsl(var(--c-toolkit) / .6); box-shadow:0 0 0 3px hsl(var(--c-toolkit) / .14); }
.th-search .clear { position:absolute; right:9px; top:50%; transform:translateY(-50%); width:20px; height:20px; border-radius:var(--r-pill);
  border:none; background:var(--bg-muted); color:var(--text-sec); cursor:pointer; font-size:11px; display:inline-flex; align-items:center; justify-content:center; }
.th-search .clear:hover { background:var(--border-strong); color:var(--text); }
.th-search .busy { position:absolute; right:34px; top:50%; transform:translateY(-50%); display:inline-flex; align-items:center; gap:5px;
  font-family:var(--f-mono); font-size:10px; color:hsl(var(--c-info)); white-space:nowrap; }
.th-search .busy i { width:6px; height:6px; border-radius:50%; background:currentColor; animation:th-typedot 1s var(--ease-in-out) infinite; }

.th-filters { flex:1 1 auto; display:flex; align-items:center; gap:8px; flex-wrap:wrap; row-gap:8px; padding:2px; min-width:0; }
.th-filters::-webkit-scrollbar { display:none; }
.th-fgroup { position:relative; display:flex; align-items:center; gap:6px; flex-shrink:0; }
.th-fdiv { width:1px; height:22px; background:var(--border); flex-shrink:0; }

/* generic filter chip */
.th-chip { display:inline-flex; align-items:center; gap:7px; padding:6px 12px; border-radius:var(--r-pill); white-space:nowrap; flex-shrink:0;
  background:var(--bg-card); border:1.5px solid var(--border); color:var(--text-sec); cursor:pointer; transition:all var(--dur-sm) var(--ease-out);
  font-family:var(--f-display); font-weight:700; font-size:12px; }
.th-chip:hover { background:var(--bg-hover); }
.th-chip .cdot { width:8px; height:8px; border-radius:50%; flex-shrink:0; background:var(--text-muted); }
.th-chip .ccount { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); }
.th-chip .cav { width:16px; height:16px; border-radius:50%; display:inline-flex; align-items:center; justify-content:center; font-size:8px; font-weight:800; color:#fff; }
.th-chip.on { color:var(--text); background:var(--bg-muted); border-color:var(--border-strong); }
.th-chip.on .ccount { color:var(--text-sec); }
/* game-tinted */
.th-chip.game.on { background:hsl(var(--c-game) / .14); border-color:hsl(var(--c-game) / .4); color:hsl(var(--c-game)); }
.th-chip.game .cdot { background:hsl(var(--c-game)); }
/* toolkit-tinted (date) */
.th-chip.tk.on { background:hsl(var(--c-toolkit) / .14); border-color:hsl(var(--c-toolkit) / .4); color:hsl(var(--c-toolkit)); }
/* success-tinted (winner) */
.th-chip.win.on { background:hsl(var(--c-success) / .14); border-color:hsl(var(--c-success) / .4); color:hsl(var(--c-success)); }
.th-chip.on .ccount { color:currentColor; opacity:.85; }

/* dropdown chip (game / winner multi-select + sort) */
.th-pop { position:absolute; top:calc(100% + 6px); left:0; z-index:30; background:var(--bg-card); border:1px solid var(--border);
  border-radius:var(--r-md); box-shadow:var(--shadow-lg); padding:6px; min-width:230px; display:flex; flex-direction:column; gap:2px; max-height:400px; overflow-y:auto; }
.th-pop.right { left:auto; right:0; min-width:190px; }
.th-pophead { font-family:var(--f-mono); font-size:10px; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); padding:4px 8px 6px; }
.th-popitem { display:flex; align-items:center; gap:9px; width:100%; padding:8px 10px; border-radius:var(--r-sm); border:none; background:transparent; cursor:pointer;
  color:var(--text); font-family:var(--f-display); font-weight:600; font-size:13px; text-align:left; }
.th-popitem:hover { background:var(--bg-muted); }
.th-popitem .check { width:16px; height:16px; border-radius:var(--r-xs); border:1.5px solid var(--border-strong); flex-shrink:0; display:inline-flex; align-items:center; justify-content:center; font-size:10px; color:#fff; }
.th-popitem.sel .check { background:hsl(var(--c-toolkit)); border-color:hsl(var(--c-toolkit)); }
.th-popitem.radio .check { border-radius:50%; }
.th-popitem .lbl { flex:1; }
.th-popitem .cnt { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); }
.th-popmore { font-family:var(--f-display); font-weight:700; font-size:12px; color:hsl(var(--c-toolkit)); background:transparent; border:none; cursor:pointer; padding:7px 10px; text-align:left; }
.th-daterange { display:flex; flex-direction:column; gap:8px; padding:10px 8px 6px; border-top:1px solid var(--border-light); margin-top:4px; }
.th-daterange .r2 { display:flex; gap:8px; }
.th-daterange label { flex:1; font-family:var(--f-mono); font-size:10px; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); display:flex; flex-direction:column; gap:4px; }
.th-daterange input { font-family:var(--f-body); font-size:12px; padding:7px 9px; border-radius:var(--r-sm); border:1px solid var(--border); background:var(--bg-card); color:var(--text); color-scheme:light dark; }
.th-daterange input:focus { outline:none; border-color:hsl(var(--c-toolkit) / .55); box-shadow:0 0 0 3px hsl(var(--c-toolkit) / .12); }
.th-daterange .apply { padding:8px 12px; border-radius:var(--r-sm); border:none; background:hsl(var(--c-toolkit)); color:#fff; font-family:var(--f-display); font-weight:700; font-size:12px; cursor:pointer; }
.th-daterange .apply:disabled { opacity:.45; cursor:default; }

.th-right { flex-shrink:0; display:flex; align-items:center; gap:10px; }
.th-vtoggle { display:inline-flex; padding:3px; gap:2px; background:var(--bg-muted); border-radius:var(--r-md); border:1px solid var(--border); }
.th-vtoggle button { display:inline-flex; align-items:center; gap:5px; padding:5px 10px; border-radius:var(--r-sm); border:none; background:transparent; color:var(--text-muted);
  font-family:var(--f-display); font-weight:700; font-size:11px; cursor:pointer; }
.th-vtoggle button[aria-pressed="true"] { background:var(--bg-card); color:hsl(var(--c-toolkit)); box-shadow:var(--shadow-xs); }

/* active filter summary bar */
.th-fsum { flex-shrink:0; display:flex; align-items:center; gap:10px; padding:8px 22px; background:var(--bg-sunken); border-bottom:1px solid var(--border-light);
  font-family:var(--f-mono); font-size:11px; color:var(--text-sec); }
.th-fsum .badge { display:inline-flex; align-items:center; gap:6px; padding:3px 9px; border-radius:var(--r-pill); background:hsl(var(--c-toolkit) / .14); color:hsl(var(--c-toolkit)); font-weight:700; }
.th-fsum .clear { background:transparent; border:none; cursor:pointer; color:hsl(var(--c-warning)); font-family:var(--f-display); font-weight:800; font-size:12px; }
.th-fsum .grow { flex:1; }

/* ─ table ─ */
.th-body { flex:1; overflow:auto; min-height:0; position:relative; }
.th-table { --cols: 168px minmax(150px,1.3fr) 92px minmax(118px,1fr) minmax(140px,1.2fr) 72px 40px 88px; min-width:1080px; }
.th-thead { position:sticky; top:0; z-index:5; display:grid; grid-template-columns:var(--cols); gap:14px; align-items:center;
  padding:10px 22px; background:var(--bg-sunken); border-bottom:1px solid var(--border); }
.th-th { font-family:var(--f-mono); font-size:11px; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); display:flex; align-items:center; gap:5px; }
.th-th.sortable { cursor:pointer; }
.th-th.sortable:hover { color:var(--text-sec); }
.th-th .arr { font-size:9px; opacity:0; transition:opacity var(--dur-sm); }
.th-th[aria-sort="ascending"] .arr, .th-th[aria-sort="descending"] .arr { opacity:1; color:hsl(var(--c-toolkit)); }
.th-th.right { justify-content:flex-end; }
.th-th.center { justify-content:center; }

.th-row { border-bottom:1px solid var(--border-light); cursor:pointer; transition:background var(--dur-sm) var(--ease-out); }
.th-row:hover { background:var(--bg-hover); }
.th-row.mine { background:hsl(var(--c-success) / .045); }
.th-row.mine:hover { background:hsl(var(--c-success) / .08); }
.th-row.sel { background:hsl(var(--c-toolkit) / .07); box-shadow:inset 3px 0 0 hsl(var(--c-toolkit)); }
.th-rowmain { display:grid; grid-template-columns:var(--cols); gap:14px; align-items:center; padding:12px 22px; }
.th-cell { min-width:0; }
.th-dt { font-family:var(--f-mono); font-size:12px; color:var(--text); font-weight:600; }
.th-dt .rel { display:block; font-size:10px; color:var(--text-muted); margin-top:2px; }
.th-durpill { display:inline-flex; align-items:center; gap:5px; font-family:var(--f-mono); font-size:12px; color:var(--text-sec); font-variant-numeric:tabular-nums; }
.th-score { font-family:var(--f-mono); font-size:14px; font-weight:800; color:var(--text); font-variant-numeric:tabular-nums; text-align:left; }
.th-score.coop { color:var(--text-muted); font-size:12px; font-weight:600; }
.th-noteic { width:26px; height:26px; border-radius:var(--r-sm); border:none; background:transparent; color:var(--text-muted); cursor:pointer; font-size:13px; display:inline-flex; align-items:center; justify-content:center; position:relative; }
.th-noteic:hover { background:var(--bg-muted); color:var(--text-sec); }
.th-noteic.empty { opacity:.28; cursor:default; }

/* EntityChip game */
.th-egame { display:inline-flex; align-items:center; gap:6px; max-width:100%; overflow:hidden; padding:3px 10px 3px 3px; border-radius:var(--r-pill);
  background:hsl(var(--c-game) / .12); color:hsl(var(--c-game)); border:1px solid hsl(var(--c-game) / .22);
  font-family:var(--f-display); font-weight:800; font-size:12px; cursor:pointer; white-space:nowrap; }
.th-egame .cov { width:20px; height:20px; border-radius:var(--r-xs); flex-shrink:0; display:inline-flex; align-items:center; justify-content:center; font-size:11px; color:rgba(255,255,255,.95); }
.th-egame .gt { overflow:hidden; text-overflow:ellipsis; min-width:0; }

/* avatar stack */
.th-avs { display:inline-flex; align-items:center; }
.th-av { width:26px; height:26px; border-radius:50%; border:2px solid var(--bg-card); margin-left:-7px; display:inline-flex; align-items:center; justify-content:center;
  font-family:var(--f-display); font-size:9px; font-weight:800; color:#fff; flex-shrink:0; }
.th-av:first-child { margin-left:0; }
.th-av.more { background:var(--bg-muted); color:var(--text-sec); border-color:var(--bg-card); }
.th-row.mine .th-av { border-color:#fffaf2; }
[data-theme="dark"] .th-row.mine .th-av { border-color:#1c2018; }

/* winner chip */
.th-win { display:inline-flex; align-items:center; gap:6px; max-width:100%; overflow:hidden; padding:3px 10px 3px 4px; border-radius:var(--r-pill);
  background:hsl(var(--c-success) / .14); color:hsl(var(--c-success)); font-family:var(--f-display); font-weight:800; font-size:12px; white-space:nowrap; }
.th-win .wt { overflow:hidden; text-overflow:ellipsis; min-width:0; }
.th-coop { display:inline-flex; align-items:center; gap:6px; padding:3px 10px; border-radius:var(--r-pill); background:var(--bg-muted); color:var(--text-muted);
  font-family:var(--f-mono); font-size:11px; font-weight:700; white-space:nowrap; }
.th-nowin { color:var(--text-muted); font-family:var(--f-mono); font-size:13px; }

/* row actions */
.th-acts { display:flex; align-items:center; gap:4px; justify-content:flex-end; }
.th-act { width:30px; height:30px; border-radius:var(--r-sm); border:1px solid var(--border); background:var(--bg-card); color:var(--text-sec);
  display:inline-flex; align-items:center; justify-content:center; cursor:pointer; font-size:13px; transition:all var(--dur-sm) var(--ease-out); }
.th-act:hover { background:var(--bg-muted); color:var(--text); border-color:var(--border-strong); }
.th-act.see:hover { color:hsl(var(--c-toolkit)); border-color:hsl(var(--c-toolkit) / .4); background:hsl(var(--c-toolkit) / .1); }

/* ─ pagination ─ */
.th-pag { flex-shrink:0; display:flex; align-items:center; gap:10px; padding:12px 22px; border-top:1px solid var(--border); background:var(--bg-card); flex-wrap:nowrap; }
.th-pagbtn { display:inline-flex; align-items:center; gap:6px; padding:7px 13px; border-radius:var(--r-md); border:1px solid var(--border-strong); background:var(--bg-card);
  color:var(--text); font-family:var(--f-display); font-weight:700; font-size:13px; cursor:pointer; transition:all var(--dur-sm) var(--ease-out); }
.th-pagbtn:hover:not(:disabled) { border-color:hsl(var(--c-toolkit) / .5); color:hsl(var(--c-toolkit)); }
.th-pagbtn:disabled { opacity:.4; cursor:default; }
.th-pagbtn.ar { padding:8px 15px; font-size:15px; flex-shrink:0; }
.th-pagnums { display:flex; align-items:center; gap:4px; }
.th-pagnum { min-width:32px; height:32px; padding:0 6px; border-radius:var(--r-pill); border:none; background:transparent; color:var(--text-sec);
  font-family:var(--f-mono); font-size:13px; font-weight:700; cursor:pointer; }
.th-pagnum:hover { background:var(--bg-muted); color:var(--text); }
.th-pagnum.on { background:hsl(var(--c-toolkit)); color:#fff; box-shadow:0 3px 10px hsl(var(--c-toolkit) / .35); }
.th-pagell { color:var(--text-muted); padding:0 2px; font-family:var(--f-mono); }
.th-pag .grow { flex:1; }
.th-pagmeta { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); white-space:nowrap; min-width:0; overflow:hidden; text-overflow:ellipsis; }
.th-pagsize { display:inline-flex; align-items:center; gap:7px; font-family:var(--f-mono); font-size:11px; color:var(--text-muted); }
.th-pagsize select { font-family:var(--f-mono); font-size:11px; padding:5px 8px; border-radius:var(--r-sm); border:1px solid var(--border); background:var(--bg-card); color:var(--text); cursor:pointer; }

/* ─ empty / loading ─ */
.th-pad { flex:1; display:flex; align-items:center; justify-content:center; padding:48px 24px; }
.th-empty { text-align:center; max-width:400px; border:1.5px dashed var(--border-strong); border-radius:var(--r-xl); padding:50px 34px;
  display:flex; flex-direction:column; align-items:center; }
.th-empty .em { width:72px; height:72px; border-radius:50%; display:inline-flex; align-items:center; justify-content:center; font-size:32px; margin-bottom:18px; }
.th-empty.tk .em { background:hsl(var(--c-toolkit) / .12); color:hsl(var(--c-toolkit)); }
.th-empty.warn .em { background:hsl(var(--c-warning) / .12); }
.th-empty h3 { font-family:var(--f-display); font-size:20px; font-weight:800; margin:0 0 8px; }
.th-empty p { font-size:14px; color:var(--text-sec); line-height:1.55; margin:0 0 22px; }
.th-empty .cta { display:inline-flex; align-items:center; gap:8px; padding:11px 20px; border-radius:var(--r-md); border:none;
  background:hsl(var(--c-toolkit)); color:#fff; font-family:var(--f-display); font-weight:800; font-size:14px; cursor:pointer; box-shadow:0 4px 14px hsl(var(--c-toolkit) / .4); }
.th-empty .cta.warn { background:transparent; border:1px solid hsl(var(--c-warning) / .5); color:hsl(var(--c-warning)); box-shadow:none; }

.th-sk { border-radius:var(--r-sm); }

/* ─ modal ─ */
.th-overlay { position:absolute; inset:0; z-index:50; background:rgba(20,12,4,.46); backdrop-filter:blur(3px); display:flex; align-items:center; justify-content:center; padding:28px; animation:th-overlay-in var(--dur-md) var(--ease-out); }
[data-theme="dark"] .th-overlay { background:rgba(0,0,0,.62); }
.th-modal { width:min(580px, 100%); max-height:100%; background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-xl); box-shadow:var(--shadow-lg);
  display:flex; flex-direction:column; overflow:hidden; animation:th-modal-in var(--dur-md) var(--ease-spring); }
.th-mhead { flex-shrink:0; display:flex; align-items:flex-start; gap:14px; padding:18px 20px; border-bottom:1px solid var(--border); background:var(--bg-card); }
.th-mhead .mcov { width:42px; height:42px; border-radius:var(--r-md); flex-shrink:0; display:inline-flex; align-items:center; justify-content:center; font-size:21px; color:rgba(255,255,255,.95); }
.th-mhtxt { flex:1; min-width:0; }
.th-mtitle { font-family:var(--f-display); font-weight:800; font-size:20px; letter-spacing:-.01em; color:var(--text); }
.th-msub { font-family:var(--f-mono); font-size:12px; color:var(--text-sec); margin-top:4px; }
.th-mclose { width:32px; height:32px; flex-shrink:0; border-radius:var(--r-sm); border:none; background:var(--bg-muted); color:var(--text-muted); cursor:pointer; font-size:16px; }
.th-mclose:hover { background:var(--border-strong); color:var(--text); }
.th-mbody { flex:1; overflow-y:auto; padding:18px 20px; display:flex; flex-direction:column; gap:20px; }
.th-msec h4 { font-family:var(--f-mono); font-size:11px; text-transform:uppercase; letter-spacing:.07em; color:var(--text-muted); margin:0 0 10px; display:flex; align-items:center; gap:8px; }
.th-msec h4 .gr { flex:1; height:1px; background:var(--border-light); }

/* leaderboard */
.th-lb { display:flex; flex-direction:column; gap:4px; }
.th-lbrow { display:grid; grid-template-columns:34px 1fr auto; gap:11px; align-items:center; padding:9px 11px; border-radius:var(--r-md); }
.th-lbrow.first { background:hsl(var(--c-success) / .1); }
.th-lbrank { font-family:var(--f-mono); font-size:13px; font-weight:800; color:var(--text-muted); text-align:center; }
.th-lbrow.first .th-lbrank { color:hsl(var(--c-success)); }
.th-lbplayer { display:flex; align-items:center; gap:9px; min-width:0; }
.th-lbplayer .nm { font-family:var(--f-display); font-weight:700; font-size:14px; color:var(--text); overflow:hidden; text-overflow:ellipsis; }
.th-lbscore { font-family:var(--f-mono); font-size:16px; font-weight:800; color:var(--text); font-variant-numeric:tabular-nums; }
.th-lbtag { font-family:var(--f-mono); font-size:10px; color:hsl(var(--c-player)); }

/* timeline */
.th-tl-toggle { display:flex; align-items:center; gap:6px; width:100%; padding:0; border:none; background:transparent; cursor:pointer; font-family:var(--f-mono);
  font-size:11px; text-transform:uppercase; letter-spacing:.07em; color:var(--text-muted); }
.th-tl-toggle .car { font-size:9px; transition:transform var(--dur-sm); }
.th-tl-toggle .gr { flex:1; height:1px; background:var(--border-light); }
.th-tl { list-style:none; margin:12px 0 0; padding:0 0 0 4px; display:flex; flex-direction:column; }
.th-tlrow { display:grid; grid-template-columns:54px 28px 1fr; gap:10px; align-items:flex-start; padding:0 0 14px; position:relative; }
.th-tlrow:not(:last-child)::before { content:''; position:absolute; left:67px; top:24px; bottom:-2px; width:1.5px; background:var(--border); }
.th-tltime { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); padding-top:5px; font-variant-numeric:tabular-nums; }
.th-tldot { width:28px; height:28px; border-radius:50%; background:var(--bg-muted); display:inline-flex; align-items:center; justify-content:center; font-size:13px; z-index:1; }
.th-tle { font-size:13px; color:var(--text-sec); line-height:1.45; padding-top:5px; }

/* notes */
.th-noteta { width:100%; min-height:64px; resize:none; border-radius:var(--r-md); border:1px solid var(--border); background:var(--bg-sunken);
  padding:11px 13px; font-family:var(--f-body); font-size:13px; color:var(--text); line-height:1.5; }
.th-noteta[readonly] { cursor:default; }
.th-noteta:focus { outline:none; border-color:hsl(var(--c-toolkit) / .5); background:var(--bg-card); }
.th-noteempty { font-size:13px; color:var(--text-muted); font-style:italic; }

/* game stats grid */
.th-gstats { display:grid; grid-template-columns:repeat(2,1fr); gap:10px; }
.th-gstat { padding:12px 14px; border-radius:var(--r-md); background:var(--bg-sunken); border:1px solid var(--border-light); }
.th-gstat .gl { font-family:var(--f-mono); font-size:10px; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); }
.th-gstat .gv { font-family:var(--f-display); font-size:22px; font-weight:800; color:var(--text); margin-top:4px; font-variant-numeric:tabular-nums; }
.th-gstat .gv small { font-size:12px; color:var(--text-muted); font-weight:600; }
.th-hl { display:flex; flex-wrap:wrap; gap:6px; margin-top:4px; }
.th-hlchip { display:inline-flex; align-items:center; gap:5px; padding:3px 9px; border-radius:var(--r-pill); background:hsl(var(--c-toolkit) / .12); color:hsl(var(--c-toolkit));
  font-family:var(--f-display); font-weight:700; font-size:11px; }

/* modal footer */
.th-mfoot { flex-shrink:0; display:flex; align-items:center; gap:8px; padding:14px 20px; border-top:1px solid var(--border); background:var(--bg-card); flex-wrap:wrap; }
.th-mbtn { display:inline-flex; align-items:center; gap:6px; padding:9px 14px; border-radius:var(--r-md); border:1px solid var(--border-strong); background:var(--bg-card);
  color:var(--text); font-family:var(--f-display); font-weight:700; font-size:13px; cursor:pointer; transition:all var(--dur-sm) var(--ease-out); }
.th-mbtn:hover { background:var(--bg-muted); }
.th-mbtn.tk { background:hsl(var(--c-toolkit)); border-color:transparent; color:#fff; }
.th-mbtn.tk:hover { filter:brightness(1.04); background:hsl(var(--c-toolkit)); }
.th-mbtn.game { background:hsl(var(--c-game)); border-color:transparent; color:#fff; }
.th-mbtn.game:hover { filter:brightness(1.04); background:hsl(var(--c-game)); }
.th-mbtn.danger { border:none; background:transparent; color:hsl(var(--c-danger)); }
.th-mbtn.danger:hover { background:hsl(var(--c-danger) / .1); }
.th-mfoot .grow { flex:1; }

/* ─ mobile cards ─ */
.th-cards { display:flex; flex-direction:column; gap:11px; padding:14px; }
.th-card { background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-lg); padding:13px 14px; cursor:pointer;
  transition:border-color var(--dur-sm) var(--ease-out); display:flex; flex-direction:column; gap:11px; }
.th-card:hover, .th-card:focus-visible { border-color:var(--border-strong); }
.th-card.mine { border-left:3px solid hsl(var(--c-success) / .6); }
.th-card .chead { display:flex; align-items:center; gap:10px; }
.th-card .chead .grow { flex:1; }
.th-card .cdate { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); }
.th-card .cmid { display:flex; align-items:center; gap:10px; flex-wrap:wrap; }
.th-card .cfoot { display:flex; align-items:center; gap:10px; padding-top:11px; border-top:1px solid var(--border-light); }
.th-card .cfoot .grow { flex:1; }
.th-cscore { display:inline-flex; align-items:center; gap:6px; padding:4px 11px; border-radius:var(--r-md); background:hsl(var(--c-toolkit) / .12); color:hsl(var(--c-toolkit));
  font-family:var(--f-mono); font-size:13px; font-weight:800; }

/* mobile adaptations */
.th-app.is-mobile .th-head { padding:12px 14px 0; }
.th-app.is-mobile .th-h1 { font-size:21px; }
.th-app.is-mobile .th-htop { flex-direction:column; gap:10px; }
.th-app.is-mobile .th-hright { flex-direction:row; align-items:center; align-self:stretch; flex-wrap:wrap; }
.th-app.is-mobile .th-toolbar { flex-wrap:wrap; padding:11px 14px; gap:10px; }
.th-app.is-mobile .th-search { flex:1 1 100%; max-width:100%; }
.th-app.is-mobile .th-right { display:none; }
.th-app.is-mobile .th-fsum { padding:8px 14px; }
.th-app.is-mobile .th-pag { padding:10px 14px; gap:8px; flex-wrap:nowrap; justify-content:space-between; }
.th-app.is-mobile .th-pagmeta { flex:1; order:0; text-align:center; }
.th-app.is-mobile .th-overlay { padding:0; align-items:flex-end; }
.th-app.is-mobile .th-modal { width:100%; max-height:92%; border-radius:var(--r-2xl) var(--r-2xl) 0 0; animation:th-sheet-in var(--dur-lg) var(--ease-spring); }
.th-app.is-mobile .th-gstats { grid-template-columns:1fr; }
`;

window.__TH_CSS = TH_CSS;
window.__TH = { eHsl, sem, SESSIONS, TOTAL, PAGE_SIZE, GAME_COUNTS, WINNER_COUNTS, N_GAMES,
  fmtDate, fmtTime, fmtDur, relDate, NOW, MONTHS_FULL, genTimeline, GAME_IDS };
