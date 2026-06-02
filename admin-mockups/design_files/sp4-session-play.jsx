/* MeepleAI SP4 — B17 · #1492 · 1/1 — sp4-session-play  (chiude epic #1475, gap N7 residuo)
   Route: /sessions/[id]/play — Live game session view (active play mode).
          Distinta da /sessions/[id]/live (spectator con tabs ?tab=). Qui l'host gioca:
          scoreboard live, quick actions, activity feed SSE, chat agent rules.
   File: sp4-session-play.{html,jsx,-parts.jsx,-ui.jsx}
   Pattern: Hero header (span 3-col) + 3-col desktop (Scoreboard+Actions / Activity feed / Chat)
            + stack mobile (chat = bottom sheet) + Rules sheet slide-over + Score/Dispute modals.
   Riusa: chat bubble (#1489 S5), activity log streaming (#1490 S4), modal/sheet (#1490, #1489).

   Source restyle (NO ridisegnare logica):
     apps/web/src/app/(authenticated)/sessions/[id]/play/page.tsx → LiveSessionView
     components/game-night/{SessionHeader,LiveScoreboard,QuickActions,ActivityFeed,
       SessionChatWidget,ScoreInput,ScoreAssistant,SaveCompleteDialog}.tsx

   Entity: --c-session (primaria/header/active player) · --c-player (scoreboard/chat user)
           --c-agent (chat header) · --c-chat (agent bubbles) · --c-toolkit (CTA primary)
           event types: --c-toolkit score · --c-game dice · --c-danger dispute · --c-kb photo · --c-agent agent.

   10 stati (state picker, persistito localStorage `sp-state`):
     active-default · paused · active-with-dispute · score-input-modal-open · dispute-modal-open ·
     rules-sheet-open · agent-streaming · sse-disconnected · finalize-prompt · mobile-stack
*/

const { useState, useEffect, useMemo, useRef, useCallback, useLayoutEffect } = React;
const DS = window.DS;

const eHsl = (type, a) => {
  const c = DS.EC[type] || DS.EC.session;
  return a !== undefined ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${a})` : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};
const sem = (name, a) => a !== undefined ? `hsl(var(--c-${name}) / ${a})` : `hsl(var(--c-${name}))`;
const PAD = n => String(n).padStart(2, '0');
const fmtDur = secs => { const h = Math.floor(secs / 3600); const m = Math.floor((secs % 3600) / 60); return h > 0 ? `${h}h ${PAD(m)}m` : `${m}m ${PAD(secs % 60)}s`; };
const reducedMotion = () => window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
const entE = ent => `hsl(var(--c-${ent}))`;

// ═══════════════════════════════════════════════════════
// ─── GAME + SESSION FIXTURE (Catan — da data.js) ────────
// ═══════════════════════════════════════════════════════
const GAME = { id: 'g-catan', title: 'I Coloni di Catan', emoji: '🌾' };
const SESSION = { id: 'sess-abc-123', code: 'CTN-9K3', date: 'Mar 2 · 16:00' };

// players (brief: Marco R./Sara T./Aaron R./Lara F., VP Catan realistici 8–15)
const PLAYERS = [
  { id: 'p-marco', name: 'Marco R.', initials: 'MR', color: 262, score: 9, delta: 2, active: true,
    breakdown: [['Insediamenti', 3], ['Città', 2], ['Strada più lunga', 2], ['Carte sviluppo (PV)', 2]] },
  { id: 'p-sara', name: 'Sara T.', initials: 'ST', color: 320, score: 7, delta: -1, active: false,
    breakdown: [['Insediamenti', 4], ['Città', 1], ['Esercito più grande', 2], ['Carte sviluppo (PV)', 0]] },
  { id: 'p-aaron', name: 'Aaron R.', initials: 'AR', color: 38, score: 6, delta: 0, active: false,
    breakdown: [['Insediamenti', 4], ['Città', 1], ['Strada più lunga', 0], ['Carte sviluppo (PV)', 1]] },
  { id: 'p-lara', name: 'Lara F.', initials: 'LF', color: 180, score: 5, delta: 1, active: false,
    breakdown: [['Insediamenti', 3], ['Città', 1], ['Strada più lunga', 0], ['Carte sviluppo (PV)', 1]] },
];

const SCORE_CATEGORIES = [
  { id: 'settlement', label: 'Insediamento', pts: 1 },
  { id: 'city', label: 'Città', pts: 2 },
  { id: 'longest-road', label: 'Strada più lunga', pts: 2 },
  { id: 'largest-army', label: 'Esercito più grande', pts: 2 },
  { id: 'dev-card', label: 'Carta sviluppo', pts: 1 },
  { id: 'vp-card', label: 'Carta Punto Vittoria', pts: 1 },
];

// quick actions (6, 2-col grid)
const QUICK_ACTIONS = [
  { id: 'dice', icon: '🎲', label: 'Tira dadi', ent: 'game' },
  { id: 'photo', icon: '📸', label: 'Foto board', ent: 'kb' },
  { id: 'score', icon: '📋', label: 'Score input', ent: 'toolkit' },
  { id: 'dispute', icon: '⚖️', label: 'Dispute', ent: 'danger' },
  { id: 'rules', icon: '📜', label: 'Regole', ent: 'kb' },
  { id: 'save', icon: '💾', label: 'Salva', ent: 'session' },
];

// activity event types → entity + glyph (cross-reference brief)
const EVENT_TYPE = {
  score:    { ent: 'toolkit', icon: '🎯' },
  dice:     { ent: 'game',    icon: '🎲' },
  dispute:  { ent: 'danger',  icon: '⚖️' },
  photo:    { ent: 'kb',      icon: '📸' },
  agent:    { ent: 'agent',   icon: '🤖' },
  milestone:{ ent: 'session', icon: '🏁' },
};

// activity feed filter chips (multi-select)
const FEED_FILTERS = [
  { id: 'all',     label: 'Tutti',    icon: '▦' },
  { id: 'score',   label: 'Punteggi', icon: '🎯' },
  { id: 'dispute', label: 'Dispute',  icon: '⚖️' },
  { id: 'photo',   label: 'Foto',     icon: '📸' },
  { id: 'agent',   label: 'Agent',    icon: '🤖' },
];

const findPlayer = id => PLAYERS.find(p => p.id === id);

// base activity feed (cronologico, newest first) — 8+ event
const BASE_FEED = [
  { id: 'ev-1', type: 'score',   time: '16:42', actor: 'p-marco', text: 'ha aggiornato il punteggio', detail: '+2 strada più lunga → 9 PV' },
  { id: 'ev-2', type: 'dice',    time: '16:38', actor: 'p-sara',  text: 'tira 2D6 → 8', detail: 'commercio porto · +2 grano' },
  { id: 'ev-3', type: 'dispute', time: '16:32', actor: 'p-aaron', text: 'ha aperto una dispute', detail: '«La strada conta come 2 segmenti?»', resolvable: true },
  { id: 'ev-4', type: 'photo',   time: '16:25', actor: 'p-lara',  text: 'ha caricato una foto della board', detail: 'Turno 5 · setup centrale' },
  { id: 'ev-5', type: 'agent',   time: '16:18', actor: null,      text: 'Agent: Marco può costruire 2 strade questo turno', detail: 'risposta a domanda regole' },
  { id: 'ev-6', type: 'score',   time: '16:12', actor: 'p-sara',  text: 'ha aggiornato il punteggio', detail: '+2 esercito più grande → 7 PV' },
  { id: 'ev-7', type: 'dice',    time: '16:06', actor: 'p-aaron', text: 'tira 2D6 → 6', detail: 'nessuna produzione · ladro su 7 evitato' },
  { id: 'ev-8', type: 'milestone', time: '16:00', actor: null,    text: 'Partita iniziata', detail: '4 giocatori · Catan base' },
];

// chat agent thread (Catan Coach) — 3 round (#1489 S5 bubble pattern)
const BASE_CHAT = [
  { id: 'm-1', role: 'user', actor: 'p-marco', text: 'Posso usare 2 carte sviluppo nello stesso turno?' },
  { id: 'm-2', role: 'agent', text: 'No: puoi giocare solo 1 carta sviluppo per turno (eccetto le carte Punto Vittoria, che si rivelano a fine partita). Vedi regolamento p.8 §3.4.', cite: 'p.8 §3.4' },
  { id: 'm-3', role: 'user', actor: 'p-sara', text: 'E per la strada più lunga? Quanti segmenti servono?' },
  { id: 'm-4', role: 'agent', text: 'Servono almeno 5 segmenti di strada continui. Chi la ottiene prende 2 PV, finché un altro giocatore non costruisce una strada più lunga. Vedi p.6 §2.7.', cite: 'p.6 §2.7' },
];

const QUICK_PROMPTS = ['Spiega le regole base', 'Come si calcola il punteggio finale?', 'Risolvi la dispute aperta'];

// rules sheet — TOC + Catan content (p.8 highlighted)
const RULES_TOC = [
  { id: 's1', n: '§1', label: 'Setup e materiali', page: 'p.2' },
  { id: 's2', n: '§2', label: 'Il turno di gioco', page: 'p.5' },
  { id: 's3', n: '§3', label: 'Carte sviluppo', page: 'p.8', highlight: true },
  { id: 's4', n: '§4', label: 'Commercio', page: 'p.10' },
  { id: 's5', n: '§5', label: 'Punti vittoria', page: 'p.12' },
];

window.__SP = {
  eHsl, sem, PAD, fmtDur, reducedMotion, entE,
  GAME, SESSION, PLAYERS, SCORE_CATEGORIES, QUICK_ACTIONS, EVENT_TYPE, FEED_FILTERS,
  findPlayer, BASE_FEED, BASE_CHAT, QUICK_PROMPTS, RULES_TOC,
};

// ═══════════════════════════════════════════════════════
// ─── COMPONENT CSS (inject) — solo token da tokens.css ──
// ═══════════════════════════════════════════════════════
window.__SP_CSS = `
.sp-app { display:flex; flex-direction:column; height:100%; min-height:0; background:var(--bg); color:var(--text); position:relative; overflow:hidden; }
.sp-app.dimmed .sp-layout { filter:saturate(.5) brightness(.97); opacity:.82; transition:filter var(--dur-md), opacity var(--dur-md); }

/* ─ header (sticky, span 3-col) ─ */
.sp-head { flex-shrink:0; position:sticky; top:0; z-index:14; background:var(--glass-bg); backdrop-filter:blur(14px); border-bottom:1px solid var(--border); padding:13px 22px 14px; }
.sp-bread { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); letter-spacing:.03em; display:flex; align-items:center; gap:7px; margin-bottom:9px; flex-wrap:wrap; }
.sp-bread .sep { opacity:.5; }
.sp-bread .cur { color:var(--text-sec); font-weight:700; }
.sp-bread .gchip { display:inline-flex; align-items:center; gap:5px; padding:1px 8px 1px 5px; border-radius:var(--r-pill); background:hsl(var(--c-game) / .12); color:hsl(var(--c-game)); font-weight:700; }

.sp-htop { display:flex; align-items:flex-start; gap:16px; }
.sp-htxt { min-width:0; flex:1; }
.sp-titlerow { display:flex; align-items:center; gap:11px; flex-wrap:wrap; }
.sp-h1 { font-family:var(--f-display); font-weight:800; font-size:25px; letter-spacing:-.02em; line-height:1.1; color:var(--text); }
.sp-statusbadge { display:inline-flex; align-items:center; gap:7px; padding:4px 11px 4px 9px; border-radius:var(--r-pill); font-family:var(--f-display); font-weight:800; font-size:12px; white-space:nowrap; }
.sp-statusbadge.live { background:hsl(var(--c-success) / .14); color:hsl(var(--c-success)); }
.sp-statusbadge.paused { background:hsl(var(--c-warning) / .15); color:hsl(var(--c-warning)); }
.sp-statusbadge.done { background:hsl(var(--c-success)); color:#fff; }
.sp-statusdot { width:8px; height:8px; border-radius:50%; flex-shrink:0; background:currentColor; }
.sp-statusdot.live { animation:sp-pulse-dot 1.6s var(--ease-in-out) infinite; }

/* turn indicator */
.sp-turn { display:inline-flex; align-items:center; gap:8px; margin-top:9px; font-size:13px; color:var(--text-sec); flex-wrap:wrap; }
.sp-turn .lbl { font-family:var(--f-display); font-weight:700; }
.sp-chip { display:inline-flex; align-items:center; gap:6px; padding:2px 10px 2px 3px; border-radius:var(--r-pill); background:hsl(var(--c-player) / .12); border:1px solid hsl(var(--c-player) / .26); white-space:nowrap; }
.sp-chip .av { width:20px; height:20px; flex-shrink:0; border-radius:50%; display:inline-flex; align-items:center; justify-content:center; font-family:var(--f-display); font-size:8px; font-weight:800; color:#fff; }
.sp-chip .nm { font-family:var(--f-display); font-weight:700; font-size:12px; color:hsl(var(--c-player)); }

/* meta sub-row */
.sp-meta { display:flex; align-items:center; gap:16px; margin-top:11px; flex-wrap:wrap; }
.sp-meta .mi { display:inline-flex; align-items:center; gap:6px; font-family:var(--f-mono); font-size:11.5px; color:var(--text-sec); }
.sp-meta .mi .g { font-size:13px; }
.sp-meta .mi b { color:var(--text); font-weight:700; }

/* header CTA right */
.sp-hcta { display:flex; align-items:center; gap:9px; flex-shrink:0; }
.sp-btn { display:inline-flex; align-items:center; justify-content:center; gap:7px; padding:9px 15px; border-radius:var(--r-md); cursor:pointer; white-space:nowrap;
  font-family:var(--f-display); font-weight:800; font-size:13px; border:1.5px solid var(--border); background:var(--bg-card); color:var(--text-sec); transition:all var(--dur-sm); }
.sp-btn:hover { background:var(--bg-hover); }
.sp-btn.warn { border-color:hsl(var(--c-warning) / .5); color:hsl(var(--c-warning)); }
.sp-btn.warn:hover { background:hsl(var(--c-warning) / .1); }
.sp-btn.primary { background:hsl(var(--c-toolkit)); border-color:transparent; color:#fff; box-shadow:0 3px 12px hsl(var(--c-toolkit) / .32); }
.sp-btn.primary:hover { filter:brightness(1.05); transform:translateY(-1px); }
.sp-btn.sm { padding:7px 11px; font-size:12px; }
.sp-iconbtn { width:38px; height:38px; flex-shrink:0; border-radius:var(--r-md); border:1.5px solid var(--border); background:var(--bg-card); color:var(--text-sec); cursor:pointer; font-size:16px; display:inline-flex; align-items:center; justify-content:center; transition:all var(--dur-sm); }
.sp-iconbtn:hover { background:var(--bg-hover); }

/* ─ session banner (paused / dispute / finalize) ─ */
.sp-banner { display:flex; align-items:center; gap:12px; padding:12px 22px; flex-shrink:0; animation:sp-event-in var(--dur-md) var(--ease-out); }
.sp-banner .bi { width:30px; height:30px; flex-shrink:0; border-radius:var(--r-md); display:inline-flex; align-items:center; justify-content:center; font-size:16px; }
.sp-banner .btxt { flex:1; min-width:0; }
.sp-banner .bt { font-family:var(--f-display); font-weight:800; font-size:14px; line-height:1.25; }
.sp-banner .bd { font-size:12px; margin-top:2px; opacity:.85; }
.sp-banner .bcta { display:flex; gap:8px; flex-shrink:0; }
.sp-banner.paused { background:hsl(var(--c-warning) / .12); border-bottom:1px solid hsl(var(--c-warning) / .3); }
.sp-banner.paused .bi { background:hsl(var(--c-warning) / .18); color:hsl(var(--c-warning)); }
.sp-banner.paused .bt { color:hsl(var(--c-warning)); }
.sp-banner.dispute { background:hsl(var(--c-danger) / .1); border-bottom:1px solid hsl(var(--c-danger) / .3); }
.sp-banner.dispute .bi { background:hsl(var(--c-danger) / .16); color:hsl(var(--c-danger)); }
.sp-banner.dispute .bt { color:hsl(var(--c-danger)); }
.sp-banner.finalize { background:hsl(var(--c-success) / .12); border-bottom:1px solid hsl(var(--c-success) / .3); }
.sp-banner.finalize .bi { background:hsl(var(--c-success) / .18); color:hsl(var(--c-success)); }
.sp-banner.finalize .bt { color:hsl(var(--c-success)); }
.sp-bbtn { display:inline-flex; align-items:center; gap:6px; padding:8px 14px; border-radius:var(--r-md); cursor:pointer; white-space:nowrap; font-family:var(--f-display); font-weight:800; font-size:12.5px; border:none; color:#fff; }
.sp-bbtn.warn { background:hsl(var(--c-warning)); }
.sp-bbtn.success { background:hsl(var(--c-success)); }
.sp-bbtn.ghost { background:transparent; border:1.5px solid var(--border-strong); color:var(--text-sec); }

/* ─ 3-col layout (30/40/30) ─ */
.sp-layout { flex:1; min-height:0; display:grid; grid-template-columns:320px 1fr 340px; gap:0; overflow:hidden; }
.sp-col { min-height:0; display:flex; flex-direction:column; overflow:hidden; }
.sp-col.left { border-right:1px solid var(--border); background:var(--bg-sunken); }
.sp-col.center { background:var(--bg); }
.sp-col.right { border-left:1px solid var(--border); background:var(--bg-sunken); }
.sp-colscroll { flex:1; min-height:0; overflow-y:auto; display:flex; flex-direction:column; }

/* panel header (shared) */
.sp-phead { display:flex; align-items:center; gap:9px; padding:14px 16px 11px; flex-shrink:0; border-bottom:1px solid var(--border-light); }
.sp-phead .pi { width:28px; height:28px; flex-shrink:0; border-radius:var(--r-sm); display:inline-flex; align-items:center; justify-content:center; font-size:14px; background:hsl(var(--e) / .15); color:hsl(var(--e)); }
.sp-phead .pt { font-family:var(--f-display); font-weight:800; font-size:14px; color:var(--text); letter-spacing:-.01em; }
.sp-phead .pc { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); margin-top:1px; }
.sp-phead .grow { flex:1; }

/* ─── SCOREBOARD ─────────────────────────────────────── */
.sp-board { display:flex; flex-direction:column; }
.sp-prow { position:relative; display:flex; align-items:center; gap:11px; padding:12px 14px; border-bottom:1px solid var(--border-light); cursor:pointer; transition:background var(--dur-sm); }
.sp-prow:hover { background:var(--bg-hover); }
.sp-prow.active { background:hsl(var(--c-session) / .08); box-shadow:inset 3px 0 0 hsl(var(--c-session)); }
.sp-prow .rank { font-family:var(--f-mono); font-weight:700; font-size:12px; color:var(--text-muted); width:22px; flex-shrink:0; text-align:center; }
.sp-prow.active .rank { color:hsl(var(--c-session)); }
.sp-pav { width:34px; height:34px; flex-shrink:0; border-radius:50%; display:inline-flex; align-items:center; justify-content:center; font-family:var(--f-display); font-size:12px; font-weight:800; color:#fff; }
.sp-pmain { flex:1; min-width:0; }
.sp-pname { font-family:var(--f-display); font-weight:700; font-size:13.5px; color:var(--text); display:flex; align-items:center; gap:6px; }
.sp-pturn { font-family:var(--f-mono); font-size:9px; padding:1px 6px; border-radius:var(--r-pill); background:hsl(var(--c-session) / .16); color:hsl(var(--c-session)); white-space:nowrap; }
.sp-pdelta { font-family:var(--f-mono); font-size:10.5px; margin-top:2px; display:flex; align-items:center; gap:4px; }
.sp-pdelta.up { color:hsl(var(--c-success)); }
.sp-pdelta.down { color:hsl(var(--c-danger)); }
.sp-pdelta.flat { color:var(--text-muted); }
.sp-pscore { display:flex; flex-direction:column; align-items:flex-end; flex-shrink:0; }
.sp-score { font-family:var(--f-display); font-weight:800; font-size:24px; line-height:1; color:var(--text); font-variant-numeric:tabular-nums; }
.sp-prow.active .sp-score { color:hsl(var(--c-session)); }
.sp-score.pop { animation:sp-score-pop .42s var(--ease-spring); }
.sp-score .vp { font-size:10px; font-weight:700; color:var(--text-muted); margin-left:2px; }
.sp-pinc { display:flex; gap:5px; margin-top:6px; }
.sp-incbtn { width:30px; height:30px; flex-shrink:0; border-radius:var(--r-sm); border:1.5px solid var(--border); background:var(--bg-card); color:var(--text-sec); cursor:pointer; font-size:15px; font-weight:700; line-height:1; display:inline-flex; align-items:center; justify-content:center; transition:all var(--dur-xs); }
.sp-incbtn:hover { border-color:hsl(var(--c-session) / .5); color:hsl(var(--c-session)); background:hsl(var(--c-session) / .08); }
.sp-incbtn:active { transform:scale(.92); }
/* expanded breakdown */
.sp-breakdown { padding:4px 14px 13px 47px; background:hsl(var(--c-session) / .03); border-bottom:1px solid var(--border-light); display:flex; flex-direction:column; gap:5px; animation:sp-event-in var(--dur-sm) var(--ease-out); }
.sp-brow { display:flex; align-items:center; justify-content:space-between; gap:10px; font-size:11.5px; }
.sp-brow .bl { color:var(--text-sec); }
.sp-brow .bv { font-family:var(--f-mono); font-weight:700; color:var(--text); }

/* ─── QUICK ACTIONS ──────────────────────────────────── */
.sp-qa { padding:13px 14px 16px; border-top:1px solid var(--border); }
.sp-qa-head { display:flex; align-items:center; gap:8px; margin-bottom:11px; }
.sp-qa-head .qi { width:26px; height:26px; flex-shrink:0; border-radius:var(--r-sm); background:hsl(var(--c-toolkit) / .15); color:hsl(var(--c-toolkit)); display:inline-flex; align-items:center; justify-content:center; font-size:13px; }
.sp-qa-head .qt { font-family:var(--f-display); font-weight:800; font-size:13px; color:var(--text); }
.sp-qa-grid { display:grid; grid-template-columns:1fr 1fr; gap:8px; }
.sp-qabtn { display:flex; align-items:center; gap:9px; padding:11px 12px; border-radius:var(--r-md); cursor:pointer; text-align:left;
  background:hsl(var(--e) / .08); border:1.5px solid hsl(var(--e) / .2); color:var(--text); transition:all var(--dur-sm); min-height:44px; }
.sp-qabtn:hover { background:hsl(var(--e) / .15); border-color:hsl(var(--e) / .4); transform:translateY(-1px); }
.sp-qabtn .qg { font-size:17px; flex-shrink:0; }
.sp-qabtn .ql { font-family:var(--f-display); font-weight:700; font-size:12.5px; line-height:1.15; }

/* ─── ACTIVITY FEED ──────────────────────────────────── */
.sp-feed { display:flex; flex-direction:column; min-height:0; }
.sp-feed.disc .sp-feedhead { animation:sp-disc-flash 1.1s var(--ease-in-out) 3; }
.sp-ssedot { width:8px; height:8px; border-radius:50%; flex-shrink:0; background:hsl(var(--c-success)); }
.sp-ssedot.live { animation:sp-pulse-dot 1.6s var(--ease-in-out) infinite; }
.sp-ssedot.off { background:hsl(var(--c-danger)); }
.sp-ssetag { display:inline-flex; align-items:center; gap:6px; padding:2px 9px; border-radius:var(--r-pill); font-family:var(--f-mono); font-size:10px; font-weight:700; background:hsl(var(--c-success) / .12); color:hsl(var(--c-success)); white-space:nowrap; }
.sp-ssetag.off { background:hsl(var(--c-danger) / .12); color:hsl(var(--c-danger)); }
/* filters */
.sp-feedfilters { display:flex; gap:6px; padding:10px 16px; flex-wrap:wrap; border-bottom:1px solid var(--border-light); flex-shrink:0; }
.sp-fchip { display:inline-flex; align-items:center; gap:5px; padding:5px 11px; border-radius:var(--r-pill); white-space:nowrap; cursor:pointer;
  background:var(--bg-card); border:1.5px solid var(--border); color:var(--text-sec); font-family:var(--f-display); font-weight:700; font-size:11px; transition:all var(--dur-sm); }
.sp-fchip:hover { background:var(--bg-hover); }
.sp-fchip.on { background:hsl(var(--c-session) / .14); border-color:hsl(var(--c-session) / .4); color:hsl(var(--c-session)); }
/* feed body */
.sp-feedbody { flex:1; min-height:0; overflow-y:auto; display:flex; flex-direction:column; }
.sp-feedmore { margin:10px auto; padding:7px 16px; border-radius:var(--r-pill); border:1px solid var(--border); background:var(--bg-card); color:var(--text-sec); font-family:var(--f-display); font-weight:700; font-size:11.5px; cursor:pointer; flex-shrink:0; }
.sp-feedmore:hover { background:var(--bg-hover); }
.sp-event { display:flex; gap:12px; padding:13px 16px; border-bottom:1px solid var(--border-light); position:relative; }
.sp-event:hover { background:var(--bg-hover); }
.sp-event.fresh { animation:sp-event-in .35s var(--ease-out); }
.sp-event.flag { background:hsl(var(--c-danger) / .06); box-shadow:inset 3px 0 0 hsl(var(--c-danger)); }
.sp-event .eic { width:32px; height:32px; flex-shrink:0; border-radius:var(--r-md); display:inline-flex; align-items:center; justify-content:center; font-size:15px; background:hsl(var(--e) / .14); }
.sp-event .ebody { flex:1; min-width:0; }
.sp-event .etop { display:flex; align-items:baseline; gap:8px; flex-wrap:wrap; }
.sp-event .etime { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); flex-shrink:0; }
.sp-event .etext { font-family:var(--f-body); font-weight:700; font-size:13px; color:var(--text); line-height:1.35; }
.sp-event .edetail { font-size:12px; color:var(--text-sec); margin-top:3px; line-height:1.4; }
.sp-emeta { display:flex; align-items:center; gap:8px; margin-top:7px; flex-wrap:wrap; }
.sp-echip { display:inline-flex; align-items:center; gap:5px; padding:1px 8px 1px 2px; border-radius:var(--r-pill); background:hsl(var(--c-player) / .12); }
.sp-echip .av { width:16px; height:16px; border-radius:50%; display:inline-flex; align-items:center; justify-content:center; font-family:var(--f-display); font-size:7px; font-weight:800; color:#fff; }
.sp-echip .nm { font-family:var(--f-display); font-weight:700; font-size:10px; color:hsl(var(--c-player)); }
.sp-eactions { display:flex; gap:7px; }
.sp-elink { background:none; border:none; cursor:pointer; font-family:var(--f-display); font-weight:700; font-size:11px; color:var(--text-muted); padding:2px 4px; border-radius:var(--r-xs); }
.sp-elink:hover { color:hsl(var(--c-session)); }
.sp-elink.danger:hover { color:hsl(var(--c-danger)); }

/* ─── CHAT WIDGET ────────────────────────────────────── */
.sp-chat { display:flex; flex-direction:column; min-height:0; height:100%; }
.sp-chathead { display:flex; align-items:center; gap:10px; padding:13px 14px; border-bottom:1px solid var(--border-light); flex-shrink:0; }
.sp-chathead .ca { width:32px; height:32px; flex-shrink:0; border-radius:var(--r-md); background:hsl(var(--c-agent) / .15); color:hsl(var(--c-agent)); display:inline-flex; align-items:center; justify-content:center; font-size:16px; }
.sp-chathead .cmeta { flex:1; min-width:0; }
.sp-chathead .cname { font-family:var(--f-display); font-weight:800; font-size:13.5px; color:var(--text); }
.sp-chathead .crole { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); margin-top:1px; }
.sp-chatbody { flex:1; min-height:0; overflow-y:auto; padding:14px; display:flex; flex-direction:column; gap:12px; }
.sp-msg { display:flex; flex-direction:column; gap:4px; max-width:88%; }
.sp-msg.fresh { animation:sp-msg-in .3s var(--ease-out); }
.sp-msg.user { align-self:flex-end; align-items:flex-end; }
.sp-msg.agent { align-self:flex-start; align-items:flex-start; }
.sp-msgwho { display:inline-flex; align-items:center; gap:6px; font-family:var(--f-mono); font-size:9px; color:var(--text-muted); }
.sp-msgwho .av { width:15px; height:15px; border-radius:50%; display:inline-flex; align-items:center; justify-content:center; font-family:var(--f-display); font-size:6px; font-weight:800; color:#fff; }
.sp-bubble { padding:9px 12px; border-radius:var(--r-lg); font-size:13px; line-height:1.45; }
.sp-msg.user .sp-bubble { background:hsl(var(--c-player) / .14); color:var(--text); border-bottom-right-radius:var(--r-xs); }
.sp-msg.agent .sp-bubble { background:hsl(var(--c-chat) / .12); color:var(--text); border-bottom-left-radius:var(--r-xs); }
.sp-cite { display:inline-flex; align-items:center; gap:4px; margin-top:6px; padding:2px 8px; border-radius:var(--r-pill); background:hsl(var(--c-kb) / .14); color:hsl(var(--c-kb)); font-family:var(--f-mono); font-size:10px; font-weight:700; cursor:pointer; border:none; }
.sp-cite:hover { background:hsl(var(--c-kb) / .22); }
.sp-cursor { display:inline-block; width:7px; height:14px; background:hsl(var(--c-chat)); margin-left:2px; vertical-align:text-bottom; animation:sp-cursor-blink .9s steps(1) infinite; border-radius:1px; }
.sp-typing { display:inline-flex; gap:4px; align-items:center; padding:11px 13px; }
.sp-typing span { width:7px; height:7px; border-radius:50%; background:hsl(var(--c-chat)); animation:sp-typing-bounce 1.2s var(--ease-in-out) infinite; }
.sp-typing span:nth-child(2) { animation-delay:.18s; }
.sp-typing span:nth-child(3) { animation-delay:.36s; }
/* chat input */
.sp-chatfoot { flex-shrink:0; border-top:1px solid var(--border-light); padding:10px 12px 12px; display:flex; flex-direction:column; gap:9px; }
.sp-prompts { display:flex; gap:6px; flex-wrap:wrap; }
.sp-prompt { padding:5px 10px; border-radius:var(--r-pill); background:var(--bg-card); border:1px solid var(--border); color:var(--text-sec); font-family:var(--f-display); font-weight:700; font-size:10.5px; cursor:pointer; white-space:nowrap; }
.sp-prompt:hover { border-color:hsl(var(--c-agent) / .4); color:hsl(var(--c-agent)); }
.sp-inputrow { display:flex; align-items:flex-end; gap:8px; }
.sp-chatinput { flex:1; min-width:0; resize:none; font-family:var(--f-body); font-size:13px; padding:10px 12px; border-radius:var(--r-md); border:1.5px solid var(--border); background:var(--bg-card); color:var(--text); outline:none; line-height:1.4; max-height:96px; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.sp-chatinput:focus { border-color:hsl(var(--c-agent) / .55); box-shadow:0 0 0 3px hsl(var(--c-agent) / .12); }
.sp-sendbtn { width:42px; height:42px; flex-shrink:0; border-radius:var(--r-md); border:none; cursor:pointer; background:hsl(var(--c-agent)); color:#fff; font-size:17px; display:inline-flex; align-items:center; justify-content:center; transition:all var(--dur-sm); }
.sp-sendbtn:hover { filter:brightness(1.06); transform:translateY(-1px); }
.sp-sendbtn:disabled { background:var(--bg-muted); color:var(--text-muted); cursor:not-allowed; transform:none; }
.sp-kbdhint { font-family:var(--f-mono); font-size:9.5px; color:var(--text-muted); text-align:right; }
.sp-kbdhint kbd { font-family:var(--f-mono); padding:1px 5px; border-radius:var(--r-xs); background:var(--bg-muted); border:1px solid var(--border); font-size:9px; }

/* ─── MODAL (Score / Dispute) ────────────────────────── */
.sp-overlay { position:absolute; inset:0; z-index:50; background:rgba(20,12,4,.46); backdrop-filter:blur(3px); display:flex; align-items:center; justify-content:center; padding:26px; animation:sp-overlay-in var(--dur-md) var(--ease-out); }
[data-theme="dark"] .sp-overlay { background:rgba(0,0,0,.62); }
.sp-modal { width:min(440px, 100%); max-height:100%; overflow-y:auto; background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-xl); box-shadow:var(--shadow-lg); animation:sp-modal-in var(--dur-md) var(--ease-spring); }
.sp-mhead { display:flex; align-items:center; gap:12px; padding:18px 18px 0; }
.sp-mhead .mi { width:38px; height:38px; flex-shrink:0; border-radius:var(--r-md); display:inline-flex; align-items:center; justify-content:center; font-size:18px; background:hsl(var(--e) / .15); color:hsl(var(--e)); }
.sp-mhead .mt { flex:1; min-width:0; font-family:var(--f-display); font-weight:800; font-size:17px; color:var(--text); line-height:1.2; }
.sp-mhead .mx { width:32px; height:32px; flex-shrink:0; border-radius:var(--r-md); border:none; background:var(--bg-muted); color:var(--text-sec); cursor:pointer; font-size:14px; display:inline-flex; align-items:center; justify-content:center; }
.sp-mhead .mx:hover { background:var(--bg-hover); color:var(--text); }
.sp-mbody { padding:14px 18px 4px; display:flex; flex-direction:column; gap:13px; }
.sp-field { display:flex; flex-direction:column; gap:7px; }
.sp-field > .fl { font-family:var(--f-display); font-weight:700; font-size:12px; color:var(--text-sec); }
.sp-pselect { display:flex; gap:7px; flex-wrap:wrap; }
.sp-popt { display:inline-flex; align-items:center; gap:7px; padding:6px 11px 6px 5px; border-radius:var(--r-pill); cursor:pointer; background:var(--bg-card); border:1.5px solid var(--border); transition:all var(--dur-sm); }
.sp-popt:hover { border-color:hsl(var(--c-player) / .4); }
.sp-popt.on { background:hsl(var(--c-player) / .12); border-color:hsl(var(--c-player) / .5); }
.sp-popt .av { width:22px; height:22px; flex-shrink:0; border-radius:50%; display:inline-flex; align-items:center; justify-content:center; font-family:var(--f-display); font-size:9px; font-weight:800; color:#fff; }
.sp-popt .nm { font-family:var(--f-display); font-weight:700; font-size:12px; color:var(--text); }
.sp-catgrid { display:grid; grid-template-columns:1fr 1fr; gap:7px; }
.sp-catopt { display:flex; align-items:center; justify-content:space-between; gap:8px; padding:9px 11px; border-radius:var(--r-md); cursor:pointer; background:var(--bg-card); border:1.5px solid var(--border); transition:all var(--dur-sm); }
.sp-catopt:hover { border-color:hsl(var(--c-toolkit) / .4); }
.sp-catopt.on { background:hsl(var(--c-toolkit) / .1); border-color:hsl(var(--c-toolkit) / .5); }
.sp-catopt .cl { font-family:var(--f-display); font-weight:700; font-size:12px; color:var(--text); }
.sp-catopt .cp { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.sp-catopt.on .cp { color:hsl(var(--c-toolkit)); }
.sp-stepperrow { display:flex; align-items:center; gap:12px; }
.sp-stepbtn { width:40px; height:40px; flex-shrink:0; border-radius:var(--r-md); border:1.5px solid var(--border); background:var(--bg-card); color:var(--text); cursor:pointer; font-size:19px; font-weight:700; display:inline-flex; align-items:center; justify-content:center; }
.sp-stepbtn:hover { border-color:hsl(var(--c-toolkit) / .5); color:hsl(var(--c-toolkit)); }
.sp-stepval { font-family:var(--f-display); font-weight:800; font-size:30px; color:var(--text); font-variant-numeric:tabular-nums; min-width:48px; text-align:center; }
.sp-stepval .pl { font-size:14px; color:var(--text-muted); }
.sp-ta { width:100%; resize:none; font-family:var(--f-body); font-size:13px; padding:9px 11px; border-radius:var(--r-md); border:1.5px solid var(--border); background:var(--bg-card); color:var(--text); outline:none; line-height:1.45; }
.sp-ta:focus { border-color:hsl(var(--c-toolkit) / .5); box-shadow:0 0 0 3px hsl(var(--c-toolkit) / .12); }
/* dispute agent suggestion */
.sp-suggest { display:flex; gap:10px; padding:12px; border-radius:var(--r-md); background:hsl(var(--c-agent) / .08); border:1px solid hsl(var(--c-agent) / .22); }
.sp-suggest .si { width:28px; height:28px; flex-shrink:0; border-radius:var(--r-md); background:hsl(var(--c-agent) / .16); color:hsl(var(--c-agent)); display:inline-flex; align-items:center; justify-content:center; font-size:14px; }
.sp-suggest .sb { flex:1; min-width:0; }
.sp-suggest .sl { font-family:var(--f-display); font-weight:800; font-size:11px; color:hsl(var(--c-agent)); margin-bottom:3px; }
.sp-suggest .st { font-size:12.5px; color:var(--text-sec); line-height:1.5; }
.sp-mfoot { display:flex; gap:9px; padding:14px 18px 18px; flex-wrap:wrap; }
.sp-mbtn { flex:1; min-width:120px; display:inline-flex; align-items:center; justify-content:center; gap:6px; padding:11px; border-radius:var(--r-md); font-family:var(--f-display); font-weight:800; font-size:13px; cursor:pointer; border:1.5px solid var(--border-strong); background:var(--bg-card); color:var(--text); transition:all var(--dur-sm); }
.sp-mbtn:hover { background:var(--bg-muted); }
.sp-mbtn.primary { background:hsl(var(--c-toolkit)); border-color:transparent; color:#fff; box-shadow:0 4px 14px hsl(var(--c-toolkit) / .3); }
.sp-mbtn.primary:hover { filter:brightness(1.05); background:hsl(var(--c-toolkit)); }
.sp-mbtn.agent { background:hsl(var(--c-agent)); border-color:transparent; color:#fff; }
.sp-mbtn.agent:hover { filter:brightness(1.05); background:hsl(var(--c-agent)); }

/* ─── RULES SHEET (slide-over right) ─────────────────── */
.sp-sheet-overlay { position:absolute; inset:0; z-index:48; background:rgba(20,12,4,.3); backdrop-filter:blur(1px); animation:sp-overlay-in var(--dur-md) var(--ease-out); }
[data-theme="dark"] .sp-sheet-overlay { background:rgba(0,0,0,.5); }
.sp-sheet { position:absolute; top:0; right:0; bottom:0; z-index:49; width:min(560px, 92%); background:var(--bg-card); border-left:1px solid var(--border); box-shadow:var(--shadow-lg); display:flex; flex-direction:column; animation:sp-sheet-in var(--dur-md) var(--ease-out); }
.sp-sheethead { display:flex; align-items:center; gap:11px; padding:16px 18px; border-bottom:1px solid var(--border); flex-shrink:0; }
.sp-sheethead .shi { width:34px; height:34px; flex-shrink:0; border-radius:var(--r-md); background:hsl(var(--c-kb) / .15); color:hsl(var(--c-kb)); display:inline-flex; align-items:center; justify-content:center; font-size:16px; }
.sp-sheethead .sht { flex:1; min-width:0; }
.sp-sheethead .shtitle { font-family:var(--f-display); font-weight:800; font-size:15px; color:var(--text); }
.sp-sheethead .shsub { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); margin-top:1px; }
.sp-sheetbody { flex:1; min-height:0; display:grid; grid-template-columns:170px 1fr; overflow:hidden; }
.sp-toc { border-right:1px solid var(--border); background:var(--bg-sunken); overflow-y:auto; padding:10px 8px; display:flex; flex-direction:column; gap:2px; }
.sp-tocitem { display:flex; align-items:baseline; gap:8px; padding:9px 10px; border-radius:var(--r-md); cursor:pointer; text-align:left; background:none; border:none; transition:background var(--dur-sm); }
.sp-tocitem:hover { background:var(--bg-hover); }
.sp-tocitem.on { background:hsl(var(--c-kb) / .12); }
.sp-tocitem .tn { font-family:var(--f-mono); font-size:10px; font-weight:700; color:var(--text-muted); flex-shrink:0; }
.sp-tocitem.on .tn { color:hsl(var(--c-kb)); }
.sp-tocitem .tl { font-family:var(--f-display); font-weight:700; font-size:12px; color:var(--text-sec); line-height:1.3; }
.sp-tocitem.on .tl { color:var(--text); }
.sp-tocitem .tp { font-family:var(--f-mono); font-size:9px; color:var(--text-muted); margin-left:auto; flex-shrink:0; }
.sp-rulescontent { overflow-y:auto; padding:20px 22px; }
.sp-pdfpage { background:var(--bg); border:1px solid var(--border); border-radius:var(--r-md); padding:22px 24px; box-shadow:var(--shadow-xs); }
.sp-pdfpage .pn { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); text-align:right; margin-bottom:14px; }
.sp-pdfpage h4 { font-family:var(--f-display); font-weight:800; font-size:18px; color:var(--text); margin-bottom:12px; }
.sp-pdfpage h5 { font-family:var(--f-display); font-weight:800; font-size:13px; color:var(--text); margin:16px 0 7px; }
.sp-pdfpage p { font-size:13px; color:var(--text-sec); line-height:1.65; margin:0 0 11px; }
.sp-pdfpage .hl { background:hsl(var(--c-kb) / .18); border-radius:var(--r-xs); padding:1px 3px; color:var(--text); box-shadow:0 0 0 1px hsl(var(--c-kb) / .3); }

/* ─── FULLSCREEN CHAT (desktop expand) ───────────────── */
.sp-chatfull { width:min(680px, 100%); height:min(86%, 760px); background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-xl); box-shadow:var(--shadow-lg); overflow:hidden; display:flex; flex-direction:column; animation:sp-modal-in var(--dur-md) var(--ease-spring); }
.sp-chatfull .sp-chat { height:100%; }
.sp-chatfull .sp-bubble { font-size:14px; }

/* ─── TOAST ──────────────────────────────────────────── */
.sp-toast { position:absolute; bottom:20px; left:50%; transform:translateX(-50%); z-index:70; display:inline-flex; align-items:center; gap:9px;
  padding:11px 16px; border-radius:var(--r-pill); background:var(--text); color:var(--bg); box-shadow:var(--shadow-lg); font-family:var(--f-display); font-weight:700; font-size:13px; animation:sp-toast-in var(--dur-md) var(--ease-spring); white-space:nowrap; }
.sp-toast .td { width:8px; height:8px; border-radius:50%; background:hsl(var(--c-success)); flex-shrink:0; }

/* ═══ MOBILE ═══ */
.sp-app.is-mobile .sp-head { padding:11px 14px 12px; }
.sp-app.is-mobile .sp-h1 { font-size:19px; }
.sp-app.is-mobile .sp-htop { flex-direction:column; gap:11px; }
.sp-app.is-mobile .sp-hcta { align-self:stretch; flex-wrap:wrap; }
.sp-app.is-mobile .sp-hcta .sp-btn.primary { flex:1; }
.sp-app.is-mobile .sp-banner { padding:11px 14px; flex-wrap:wrap; }
.sp-app.is-mobile .sp-banner .bcta { width:100%; }
.sp-app.is-mobile .sp-banner .sp-bbtn { flex:1; justify-content:center; }
.sp-app.is-mobile .sp-layout { display:block; overflow-y:auto; -webkit-overflow-scrolling:touch; }
.sp-app.is-mobile .sp-col { overflow:visible; height:auto; }
.sp-app.is-mobile .sp-col.left { border-right:none; border-bottom:1px solid var(--border); }
.sp-app.is-mobile .sp-col.center { border-bottom:1px solid var(--border); }
.sp-app.is-mobile .sp-col.right { display:none; } /* chat → bottom sheet */
.sp-app.is-mobile .sp-colscroll { overflow:visible; }
.sp-app.is-mobile .sp-feedbody { overflow:visible; }
/* collapsible activity feed on mobile */
.sp-app.is-mobile .sp-feed.collapsed .sp-feedfilters,
.sp-app.is-mobile .sp-feed.collapsed .sp-feedbody { display:none; }
.sp-collapse-btn { display:none; }
.sp-app.is-mobile .sp-collapse-btn { display:inline-flex; align-items:center; justify-content:center; width:26px; height:26px; border-radius:var(--r-sm); border:1px solid var(--border); background:var(--bg-card); color:var(--text-sec); cursor:pointer; font-size:11px; flex-shrink:0; transition:transform var(--dur-sm); }
.sp-app.is-mobile .sp-feed:not(.collapsed) .sp-collapse-btn { transform:rotate(180deg); }
/* mobile chat sheet trigger */
.sp-chatfab { display:none; }
.sp-app.is-mobile .sp-chatfab { display:flex; position:sticky; bottom:0; z-index:30; align-items:center; gap:10px; width:100%; padding:13px 16px; border:none; border-top:1px solid var(--border);
  background:hsl(var(--c-agent)); color:#fff; cursor:pointer; font-family:var(--f-display); font-weight:800; font-size:14px; box-shadow:0 -6px 20px hsl(var(--c-agent) / .25); }
.sp-chatfab .cg { font-size:18px; }
.sp-chatfab .cbadge { margin-left:auto; font-family:var(--f-mono); font-size:11px; background:rgba(255,255,255,.25); padding:2px 9px; border-radius:var(--r-pill); }
/* mobile chat as bottom sheet */
.sp-chatsheet { position:absolute; left:0; right:0; bottom:0; top:38px; z-index:55; background:var(--bg-card); border-top-left-radius:var(--r-2xl); border-top-right-radius:var(--r-2xl); box-shadow:var(--shadow-drawer); display:flex; flex-direction:column; overflow:hidden; animation:sp-sheet-up var(--dur-md) var(--ease-out); }
.sp-chatsheet-grab { display:flex; align-items:center; justify-content:center; padding:9px 0 4px; flex-shrink:0; }
.sp-chatsheet-grab::before { content:''; width:38px; height:4px; border-radius:var(--r-pill); background:var(--border-strong); }
.sp-app.is-mobile .sp-qa-grid { grid-template-columns:1fr 1fr; }
.sp-app.is-mobile .sp-catgrid { grid-template-columns:1fr; }
`;
