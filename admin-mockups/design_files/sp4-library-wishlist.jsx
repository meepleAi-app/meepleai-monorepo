/* sp4-library-wishlist — Issue #1491 / Design v1 B16
   Decisione: Option B (standalone) vs Option A (variant in sp4-library-desktop).
   Razionale: separation of concerns, coerenza cluster #1490 (1 file = 1 screen),
   naming auto-descrittivo per discovery.

   Route: /library/wishlist — Lista personale di giochi salvati che l'utente vuole
          comprare o provare. Ogni item ha priorità (Alta / Media / Bassa),
          target price opzionale, note opzionali. Distinto da /library (posseduti).
   File: admin-mockups/design_files/sp4-library-wishlist.{html,jsx,-ui.jsx}
   Pattern: Hero + body con grid responsive (riusa card grid sp4-library-desktop +
            filter chips leggeri da sp4-toolkit-templates-ui). NO sidebar, NO split-view.

   Source restyle (NO ridisegnare logica):
     apps/web/src/app/(authenticated)/library/wishlist/page.tsx
     Component MeepleWishlistCard · AddToWishlistDialog
     Hooks useWishlist() · useAddToWishlist() · useRemoveFromWishlist()
   API: wishlistClient.getWishlist() → Array<WishlistItemDto>
        { id, gameId, gameName, priority, targetPrice, notes, addedAt }

   Entity: --c-game (arancio) primaria — wishlist è collection di giochi.
           Priorità → colori semantici (Alta --c-danger / Media --c-warning / Bassa muted).

   8 stati (state picker continuity, persistito localStorage `lw-state`):
     default · filter-priority-alta · filter-search-active · empty-no-items ·
     loading · error · add-dialog-open · mobile-stack
*/

const { useState, useEffect, useMemo, useRef, useCallback } = React;
const DS = window.DS;

const eHsl = (type, a) => {
  const c = DS.EC[type] || DS.EC.game;
  return a !== undefined ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${a})` : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

const MONTHS_SHORT = ['gen','feb','mar','apr','mag','giu','lug','ago','set','ott','nov','dic'];
const PAD = n => String(n).padStart(2, '0');
const NOW = new Date(2026, 4, 28, 16, 5); // 28 mag 2026 (allineato al cluster)
const fmtDateTime = d =>
  `${d.getDate()} ${MONTHS_SHORT[d.getMonth()]} ${d.getFullYear()} · ${PAD(d.getHours())}:${PAD(d.getMinutes())}`;
function relDate(d, now) {
  const days = Math.round((now - d) / 86400000);
  if (days <= 0) return 'oggi';
  if (days === 1) return 'ieri';
  if (days < 7) return `${days} giorni fa`;
  if (days < 14) return '1 sett. fa';
  if (days < 30) return `${Math.floor(days / 7)} sett. fa`;
  if (days < 60) return '1 mese fa';
  if (days < 365) return `${Math.floor(days / 30)} mesi fa`;
  return `${Math.floor(days / 365)} anni fa`;
}
const euro = n => `€${Number.isInteger(n) ? n : n.toFixed(2)}`;

// ═══════════════════════════════════════════════════════
// ─── PRIORITY META ──────────────────────────────────
// ═══════════════════════════════════════════════════════
// `pvar` è il triplet HSL usato in hsl(var(--p) / a). Per "Bassa" un grigio
// caldo coerente col tema (non c'è triplet semantico per muted).
const PRIO = {
  high:   { key: 'high',   it: 'Alta',  icon: '🔥', pvar: 'var(--c-danger)',  rank: 0 },
  medium: { key: 'medium', it: 'Media', icon: '⭐', pvar: 'var(--c-warning)', rank: 1 },
  low:    { key: 'low',    it: 'Bassa', icon: '⬇️', pvar: '32 14% 52%',       rank: 2 },
};
const PRIO_ORDER = ['high', 'medium', 'low'];

// ═══════════════════════════════════════════════════════
// ─── CATEGORY META (tutte renderizzate --c-game) ─────
// ═══════════════════════════════════════════════════════
const CATMETA = {
  Strategy:    { it: 'Strategia',   icon: '🎯' },
  Family:      { it: 'Famiglia',    icon: '👪' },
  CardGames:   { it: 'Carte',       icon: '🃏' },
  Cooperative: { it: 'Cooperativo', icon: '🤝' },
  Party:       { it: 'Party',       icon: '🎉' },
  Abstract:    { it: 'Astratto',    icon: '♟️' },
};

// ═══════════════════════════════════════════════════════
// ─── FIXTURE — 12 wishlist item deterministici ──────
// ═══════════════════════════════════════════════════════
// [name, emoji, category, players, duration, bgg, priority, targetPrice|null, notes, daysAgo, minimal]
const RAW = [
  // ── Alta (5) ──
  ['Brass: Birmingham', '🏭', 'Strategy', '2–4', '60–120m', 8.6, 'high', 65, 'Acquisto entro Q3 — edizione Roxley deluxe.', 14, false],
  ['Spirit Island', '🌋', 'Cooperative', '1–4', '90–120m', 8.3, 'high', 55, 'Aspetto la ristampa con le miniature spirito.', 9, false],
  ['Gloomhaven: Le Fauci del Leone', '⚔️', 'Cooperative', '1–4', '60–120m', 8.4, 'high', 40, 'Versione entry per provare la campagna prima del box grande.', 21, false],
  ['Ark Nova', '🦒', 'Strategy', '1–4', '90–150m', 8.5, 'high', 70, '', 5, false],            // price only
  ['Dune: Imperium', '🏜️', 'Strategy', '1–4', '60–120m', 8.4, 'high', null, 'Luca ce l\'ha — valutare se prenderlo o giocarlo da lui.', 3, false], // notes only
  // ── Media (4) ──
  ['Wingspan', '🦜', 'Family', '1–5', '40–70m', 8.1, 'medium', 45, 'Regalo per Sara, con espansione europea inclusa.', 30, false],
  ['Cascadia', '🏞️', 'Family', '1–4', '30–45m', 7.9, 'medium', 30, '', 18, false],           // price only
  ['Everdell', '🌳', 'Strategy', '1–4', '40–80m', 8.0, 'medium', null, 'Componentistica stupenda — soprattutto l\'albero 3D.', 45, false], // notes only
  ['The Crew: Missione Nove', '🚀', 'CardGames', '2–5', '20m', 7.9, 'medium', 15, 'Cooperativo a prese, perfetto da viaggio.', 12, false],
  // ── Bassa (3) ──
  ['Power Grid', '⚡', 'Strategy', '2–6', '120m', 7.9, 'low', 38, '', 60, false],             // price only
  ['Patchwork', '🧵', 'Abstract', '2', '15–30m', 7.7, 'low', null, 'Filler veloce per due — buono per la coda di serata.', 50, false], // notes only
  ['Sky Team', '✈️', 'CardGames', '2', '15m', 7.8, 'low', null, '', 1, true],               // minimal quick-add
];

function buildWishlist() {
  return RAW.map((r, i) => {
    const [name, emoji, category, players, duration, bgg, priority, targetPrice, notes, daysAgo, minimal] = r;
    const slug = name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
    return {
      id: `wl-${PAD(i)}-${slug}`,
      gameId: `g-${slug}`,
      name, emoji, category, players, duration, bgg, priority,
      targetPrice: targetPrice, notes: notes || '',
      addedAt: new Date(NOW.getTime() - daysAgo * 86400000),
      daysAgo, minimal: !!minimal,
    };
  });
}
const WISHLIST = buildWishlist();
const TOTAL = WISHLIST.length;
const PRIO_COUNTS = Object.fromEntries(PRIO_ORDER.map(p => [p, WISHLIST.filter(w => w.priority === p).length]));
const TOTAL_SPEND = WISHLIST.reduce((a, w) => a + (w.targetPrice || 0), 0);

// catalog per il game selector del dialog (giochi non già in wishlist + alcuni owned)
const LIB_GAMES = DS.games.filter(g => g.status !== 'wishlist');

window.__LW_CSS = '';
window.__LW = {
  eHsl, relDate, fmtDateTime, euro, NOW,
  PRIO, PRIO_ORDER, CATMETA,
  WISHLIST, TOTAL, PRIO_COUNTS, TOTAL_SPEND, LIB_GAMES,
};

// ═══════════════════════════════════════════════════════
// ─── COMPONENT CSS (inject) — solo token da tokens.css ──
// ═══════════════════════════════════════════════════════
const LW_CSS = `
.lw-app { display:flex; flex-direction:column; height:100%; min-height:0; background:var(--bg); color:var(--text); position:relative; overflow:hidden; --e:var(--c-game); }

/* ─ error banner ─ */
.lw-errbar { flex-shrink:0; display:flex; align-items:center; gap:11px; padding:11px 18px; font-family:var(--f-display); font-weight:700; font-size:13px;
  background:hsl(var(--c-danger) / .12); color:hsl(var(--c-danger)); border-bottom:1px solid hsl(var(--c-danger) / .3); }
.lw-errbar .grow { flex:1; }
.lw-errbar .retry { display:inline-flex; align-items:center; gap:6px; padding:6px 13px; border-radius:var(--r-md); border:1px solid hsl(var(--c-danger) / .4);
  background:var(--bg-card); color:hsl(var(--c-danger)); font-family:var(--f-display); font-weight:700; font-size:13px; cursor:pointer; }

/* ─ header (sticky) ─ */
.lw-head { flex-shrink:0; position:sticky; top:0; z-index:12; background:var(--glass-bg); backdrop-filter:blur(14px); border-bottom:1px solid var(--border); padding:14px 22px 0; }
.lw-htop { display:flex; align-items:flex-start; gap:16px; }
.lw-htxt { min-width:0; flex:1; }
.lw-bread { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); letter-spacing:.04em; display:flex; align-items:center; gap:6px; margin-bottom:7px; }
.lw-bread .sep { opacity:.5; }
.lw-bread .cur { color:var(--text-sec); font-weight:700; }
.lw-titlerow { display:flex; align-items:center; gap:11px; }
.lw-ico { width:34px; height:34px; flex-shrink:0; border-radius:var(--r-md); background:hsl(var(--c-game) / .16); color:hsl(var(--c-game));
  display:inline-flex; align-items:center; justify-content:center; font-size:18px; }
.lw-h1 { font-family:var(--f-display); font-weight:800; font-size:29px; letter-spacing:-.02em; line-height:1.1; color:var(--text); white-space:nowrap; }
.lw-h1 .heart { color:hsl(var(--c-game)); }
.lw-sub { font-size:14px; color:var(--text-sec); margin-top:5px; max-width:560px; }
.lw-hright { display:flex; flex-direction:column; align-items:flex-end; gap:10px; flex-shrink:0; }
.lw-qstat { display:inline-flex; align-items:center; gap:7px; font-family:var(--f-mono); font-size:11px; color:var(--text-muted); white-space:nowrap; }
.lw-qstat b { color:var(--text-sec); font-weight:700; }
.lw-qstat .dot { opacity:.5; }
.lw-cta { display:inline-flex; align-items:center; gap:7px; padding:9px 16px; border-radius:var(--r-md); background:hsl(var(--c-game));
  border:none; color:#fff; font-family:var(--f-display); font-weight:800; font-size:13px; cursor:pointer; white-space:nowrap;
  box-shadow:0 4px 14px hsl(var(--c-game) / .35); transition:all var(--dur-sm) var(--ease-out); }
.lw-cta:hover { filter:brightness(1.05); transform:translateY(-1px); }

/* ─ tabs nav (tie-in /library) ─ */
.lw-tabs { display:flex; gap:4px; margin-top:14px; overflow-x:auto; scrollbar-width:none; }
.lw-tabs::-webkit-scrollbar { display:none; }
.lw-tab { display:inline-flex; align-items:center; gap:6px; padding:9px 14px 11px; border:none; background:transparent; cursor:pointer; white-space:nowrap;
  border-bottom:2px solid transparent; color:var(--text-muted); font-family:var(--f-display); font-weight:700; font-size:13px; transition:color var(--dur-sm); text-decoration:none; }
.lw-tab:hover { color:var(--text-sec); }
.lw-tab.on { color:hsl(var(--c-game)); border-bottom-color:hsl(var(--c-game)); }

/* ─ toolbar ─ */
.lw-toolbar { flex-shrink:0; display:flex; align-items:center; gap:12px; padding:11px 22px; background:var(--bg); border-bottom:1px solid var(--border); flex-wrap:wrap; row-gap:10px; }
.lw-search { flex:0 1 300px; max-width:340px; min-width:160px; position:relative; }
.lw-search .ic { position:absolute; left:11px; top:50%; transform:translateY(-50%); font-size:13px; opacity:.6; pointer-events:none; }
.lw-search input { width:100%; padding:9px 70px 9px 32px; border-radius:var(--r-md); border:1.5px solid var(--border);
  background:var(--bg-card); font-family:var(--f-body); font-size:13px; color:var(--text); outline:none; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.lw-search input::placeholder { color:var(--text-muted); }
.lw-search input:focus, .lw-search.active input { border-color:hsl(var(--c-game) / .6); box-shadow:0 0 0 3px hsl(var(--c-game) / .14); }
.lw-search .clear { position:absolute; right:9px; top:50%; transform:translateY(-50%); width:20px; height:20px; border-radius:var(--r-pill);
  border:none; background:var(--bg-muted); color:var(--text-sec); cursor:pointer; font-size:11px; display:inline-flex; align-items:center; justify-content:center; }
.lw-search .clear:hover { background:var(--border-strong); color:var(--text); }
.lw-search .busy { position:absolute; right:34px; top:50%; transform:translateY(-50%); display:inline-flex; align-items:center; gap:5px;
  font-family:var(--f-mono); font-size:10px; color:hsl(var(--c-info)); white-space:nowrap; }
.lw-search .busy i { width:6px; height:6px; border-radius:50%; background:currentColor; animation:lw-typedot 1s var(--ease-in-out) infinite; }

/* priority filter chips (multi-select checkbox group) */
.lw-chips { flex:1 1 auto; display:flex; align-items:center; gap:8px; flex-wrap:wrap; row-gap:8px; min-width:0; padding:2px; }
.lw-chips.scroll { flex-wrap:nowrap; overflow-x:auto; scrollbar-width:none; }
.lw-chips.scroll::-webkit-scrollbar { display:none; }
.lw-chip { display:inline-flex; align-items:center; gap:7px; padding:7px 13px; border-radius:var(--r-pill); white-space:nowrap; flex-shrink:0;
  background:var(--bg-card); border:1.5px solid var(--border); color:var(--text-sec); cursor:pointer; transition:all var(--dur-sm) var(--ease-out);
  font-family:var(--f-display); font-weight:700; font-size:12.5px; }
.lw-chip:hover { background:var(--bg-hover); }
.lw-chip .cdot { width:8px; height:8px; border-radius:50%; flex-shrink:0; background:var(--text-muted); }
.lw-chip .ccount { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); opacity:.9; }
/* "Tutti" = neutral game */
.lw-chip.all.on { color:var(--text); background:var(--bg-muted); border-color:var(--border-strong); }
.lw-chip.all.on .cdot { background:hsl(var(--c-game)); }
.lw-chip.all.on .ccount { color:var(--text-sec); }
/* priority-tinted active (via --p) */
.lw-chip.prio .cdot { background:hsl(var(--p)); }
.lw-chip.prio.on { background:hsl(var(--p) / .14); border-color:hsl(var(--p) / .42); color:hsl(var(--p)); }
.lw-chip.prio.on .ccount { color:currentColor; }

/* right cluster: sort */
.lw-right { flex-shrink:0; display:flex; align-items:center; gap:10px; }
.lw-fgroup { position:relative; display:flex; align-items:center; }
.lw-sortbtn { display:inline-flex; align-items:center; gap:7px; padding:8px 13px; border-radius:var(--r-md); background:var(--bg-card); border:1.5px solid var(--border);
  color:var(--text-sec); cursor:pointer; font-family:var(--f-display); font-weight:700; font-size:12.5px; white-space:nowrap; }
.lw-sortbtn:hover { background:var(--bg-hover); }
.lw-pop { position:absolute; top:calc(100% + 6px); right:0; z-index:30; background:var(--bg-card); border:1px solid var(--border);
  border-radius:var(--r-md); box-shadow:var(--shadow-lg); padding:6px; min-width:210px; display:flex; flex-direction:column; gap:2px; }
.lw-pophead { font-family:var(--f-mono); font-size:10px; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); padding:4px 8px 6px; }
.lw-popitem { display:flex; align-items:center; gap:9px; width:100%; padding:8px 10px; border-radius:var(--r-sm); border:none; background:transparent; cursor:pointer;
  color:var(--text); font-family:var(--f-display); font-weight:600; font-size:13px; text-align:left; }
.lw-popitem:hover { background:var(--bg-muted); }
.lw-popitem .check { width:16px; height:16px; border-radius:50%; border:1.5px solid var(--border-strong); flex-shrink:0; display:inline-flex; align-items:center; justify-content:center; font-size:10px; color:hsl(var(--c-game)); }
.lw-popitem.sel .check { border-color:hsl(var(--c-game)); }
.lw-popitem .lbl { flex:1; }

/* active filter summary */
.lw-fsum { flex-shrink:0; display:flex; align-items:center; gap:10px; padding:8px 22px; background:var(--bg-sunken); border-bottom:1px solid var(--border-light);
  font-family:var(--f-mono); font-size:11px; color:var(--text-sec); }
.lw-fsum .badge { display:inline-flex; align-items:center; gap:6px; padding:3px 9px; border-radius:var(--r-pill); background:hsl(var(--c-game) / .14); color:hsl(var(--c-game)); font-weight:700; }
.lw-fsum .clear { background:transparent; border:none; cursor:pointer; color:hsl(var(--c-warning)); font-family:var(--f-display); font-weight:800; font-size:12px; }
.lw-fsum .grow { flex:1; }

/* ─ body / grid ─ */
.lw-body { flex:1; overflow:auto; min-height:0; position:relative; padding:18px 22px 26px; }
.lw-grid { display:grid; gap:16px; grid-template-columns:repeat(auto-fill, minmax(260px, 1fr)); }

/* ─ wishlist card ─ */
.lw-card { position:relative; display:flex; flex-direction:column; gap:12px; min-height:280px; padding:16px;
  background:var(--bg-card); border:1px solid var(--border-light); border-radius:var(--r-lg); box-shadow:var(--shadow-sm);
  transition:box-shadow var(--dur-sm) var(--ease-out), border-color var(--dur-sm) var(--ease-out), transform var(--dur-sm) var(--ease-out); }
.lw-card:hover { box-shadow:var(--shadow-md); border-color:hsl(var(--c-game) / .4); transform:translateY(-2px); }
.lw-card.high { border-left:3px solid hsl(var(--c-danger)); }

/* header row */
.lw-chead { display:flex; align-items:center; gap:8px; }
.lw-heart { width:26px; height:26px; flex-shrink:0; border-radius:var(--r-sm); display:inline-flex; align-items:center; justify-content:center; font-size:14px;
  background:hsl(var(--c-game) / .14); }
.lw-chgrow { flex:1; }
.lw-pbadge { display:inline-flex; align-items:center; gap:5px; padding:3px 9px 3px 8px; border-radius:var(--r-pill); cursor:pointer; flex-shrink:0;
  border:1px solid hsl(var(--p) / .4); background:hsl(var(--p) / .14); color:hsl(var(--p));
  font-family:var(--f-display); font-weight:800; font-size:10.5px; transition:filter var(--dur-xs); }
.lw-pbadge:hover { filter:brightness(.95); }
.lw-pbadge.low { color:var(--text-sec); border-color:var(--border-strong); background:var(--bg-muted); }

/* game name */
.lw-cname { font-family:var(--f-display); font-weight:800; font-size:16.5px; line-height:1.2; color:var(--text); overflow:hidden; text-overflow:ellipsis; white-space:nowrap;
  border:none; background:transparent; padding:0; cursor:pointer; text-align:left; width:100%; display:flex; align-items:center; gap:7px; }
.lw-cname .gem { font-size:15px; flex-shrink:0; }
.lw-cname .gtxt { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.lw-cname:hover .gtxt { color:hsl(var(--c-game)); }

/* meta box (mini-card) */
.lw-metabox { display:flex; flex-direction:column; gap:6px; padding:9px 11px; border-radius:var(--r-md); background:var(--bg-muted); border:1px solid var(--border-light); }
.lw-metarow { display:flex; align-items:center; gap:8px; flex-wrap:wrap; }
.lw-ecat { display:inline-flex; align-items:center; gap:5px; padding:3px 9px; border-radius:var(--r-pill); flex-shrink:0;
  background:hsl(var(--c-game) / .14); color:hsl(var(--c-game)); border:1px solid hsl(var(--c-game) / .22);
  font-family:var(--f-display); font-weight:700; font-size:10.5px; }
.lw-mchip { display:inline-flex; align-items:center; gap:4px; font-family:var(--f-mono); font-size:11px; color:var(--text-sec); white-space:nowrap; }
.lw-mchip .mi { opacity:.7; }
.lw-mchip.bgg { color:hsl(var(--c-warning)); font-weight:700; }

/* target price + notes */
.lw-pn { display:flex; flex-direction:column; gap:4px; }
.lw-price { font-family:var(--f-mono); font-size:13px; font-weight:700; color:var(--text); display:inline-flex; align-items:baseline; gap:6px; }
.lw-price .plbl { font-family:var(--f-display); font-size:11px; font-weight:700; color:var(--text-muted); text-transform:uppercase; letter-spacing:.05em; }
.lw-notes { font-size:12px; color:var(--text-sec); line-height:var(--lh-snug); margin:0;
  display:-webkit-box; -webkit-line-clamp:2; -webkit-box-orient:vertical; overflow:hidden; }
.lw-noprice { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); }

.lw-spacer { flex:1; }

/* added-at */
.lw-added { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); display:inline-flex; align-items:center; gap:6px; }
.lw-added .ai { opacity:.7; }

.lw-cdiv { height:1px; background:var(--border-light); }

/* footer actions */
.lw-acts { display:flex; align-items:center; gap:6px; }
.lw-act { display:inline-flex; align-items:center; gap:6px; padding:7px 11px; border-radius:var(--r-md); border:1px solid var(--border-light); background:transparent;
  color:var(--text-sec); cursor:pointer; font-family:var(--f-display); font-weight:700; font-size:12px; transition:all var(--dur-sm) var(--ease-out); }
.lw-act.edit:hover { color:hsl(var(--c-game)); border-color:hsl(var(--c-game) / .4); background:hsl(var(--c-game) / .08); }
.lw-act.del { margin-left:auto; }
.lw-act.del:hover { color:hsl(var(--c-danger)); border-color:hsl(var(--c-danger) / .4); background:hsl(var(--c-danger) / .08); }

/* ─ empty / loading ─ */
.lw-pad { min-height:340px; display:flex; align-items:center; justify-content:center; padding:30px 24px; }
.lw-empty { text-align:center; max-width:440px; border:1.5px dashed var(--border-strong); border-radius:var(--r-xl); padding:46px 34px;
  display:flex; flex-direction:column; align-items:center; }
.lw-empty .em { width:74px; height:74px; border-radius:50%; display:inline-flex; align-items:center; justify-content:center; font-size:32px; margin-bottom:16px;
  background:hsl(var(--c-game) / .12); }
.lw-empty h3 { font-family:var(--f-display); font-size:21px; font-weight:800; margin:0 0 8px; }
.lw-empty p { font-size:14px; color:var(--text-sec); line-height:1.55; margin:0 0 22px; max-width:340px; }
.lw-empty .cta { display:inline-flex; align-items:center; gap:8px; padding:11px 20px; border-radius:var(--r-md); border:none;
  background:hsl(var(--c-game)); color:#fff; font-family:var(--f-display); font-weight:800; font-size:14px; cursor:pointer; box-shadow:0 4px 14px hsl(var(--c-game) / .32); }
.lw-empty.filter { border-style:dashed; }
.lw-empty.filter .em { background:hsl(var(--c-warning) / .12); }
.lw-empty.filter .cta { background:transparent; color:hsl(var(--c-warning)); border:1px solid hsl(var(--c-warning) / .5); box-shadow:none; }

.lw-sk { border-radius:var(--r-sm); }
.lw-skcard { display:flex; flex-direction:column; gap:12px; min-height:280px; padding:16px; background:var(--bg-card); border:1px solid var(--border-light); border-radius:var(--r-lg); }

/* ─ add/edit dialog ─ */
.lw-overlay { position:absolute; inset:0; z-index:50; background:rgba(20,12,4,.46); backdrop-filter:blur(3px); display:flex; align-items:center; justify-content:center; padding:28px; animation:lw-overlay-in var(--dur-md) var(--ease-out); }
[data-theme="dark"] .lw-overlay { background:rgba(0,0,0,.62); }
.lw-modal { width:min(520px, 100%); max-height:100%; background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-xl); box-shadow:var(--shadow-lg);
  display:flex; flex-direction:column; overflow:hidden; animation:lw-modal-in var(--dur-md) var(--ease-spring); }
.lw-mhead { flex-shrink:0; display:flex; align-items:flex-start; gap:13px; padding:18px 20px; border-bottom:1px solid var(--border); }
.lw-mcov { width:40px; height:40px; flex-shrink:0; border-radius:var(--r-md); display:inline-flex; align-items:center; justify-content:center; font-size:19px; background:hsl(var(--c-game) / .16); color:hsl(var(--c-game)); }
.lw-mhtxt { flex:1; min-width:0; }
.lw-mtitle { font-family:var(--f-display); font-weight:800; font-size:19px; letter-spacing:-.01em; color:var(--text); }
.lw-msub { font-family:var(--f-mono); font-size:11px; color:var(--text-sec); margin-top:4px; }
.lw-mclose { width:32px; height:32px; flex-shrink:0; border-radius:var(--r-sm); border:none; background:var(--bg-muted); color:var(--text-muted); cursor:pointer; font-size:16px; }
.lw-mclose:hover { background:var(--border-strong); color:var(--text); }
.lw-mbody { flex:1; overflow-y:auto; padding:18px 20px; display:flex; flex-direction:column; gap:17px; }

/* error banner in dialog */
.lw-merr { display:flex; align-items:center; gap:9px; padding:10px 12px; border-radius:var(--r-md); background:hsl(var(--c-danger) / .08); border:1px solid hsl(var(--c-danger) / .3);
  color:hsl(var(--c-danger)); font-family:var(--f-display); font-weight:700; font-size:12.5px; }
.lw-merr .grow { flex:1; }
.lw-merr .rt { border:none; background:transparent; color:hsl(var(--c-danger)); font-family:var(--f-display); font-weight:800; font-size:12px; cursor:pointer; text-decoration:underline; }

.lw-field { display:flex; flex-direction:column; gap:7px; }
.lw-flabel { display:flex; align-items:center; gap:6px; font-family:var(--f-mono); font-size:10px; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); }
.lw-flabel .req { color:hsl(var(--c-danger)); }
.lw-flabel .grow { flex:1; }
.lw-counter { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.lw-counter.near { color:hsl(var(--c-warning)); }

/* game combo */
.lw-combo { position:relative; }
.lw-combo input { width:100%; padding:10px 12px; border-radius:var(--r-md); border:1.5px solid var(--border); background:var(--bg-card); color:var(--text);
  font-family:var(--f-body); font-size:13px; outline:none; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.lw-combo input:focus { border-color:hsl(var(--c-game) / .6); box-shadow:0 0 0 3px hsl(var(--c-game) / .14); }
.lw-selected { display:flex; align-items:center; gap:8px; padding:8px 8px 8px 11px; border-radius:var(--r-md); background:hsl(var(--c-game) / .1); border:1.5px solid hsl(var(--c-game) / .3); }
.lw-selected .se { font-size:16px; }
.lw-selected .sn { flex:1; font-family:var(--f-display); font-weight:700; font-size:13.5px; color:hsl(var(--c-game)); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.lw-selected .sx { width:24px; height:24px; flex-shrink:0; border-radius:var(--r-sm); border:none; background:hsl(var(--c-game) / .14); color:hsl(var(--c-game)); cursor:pointer; font-size:12px; }
.lw-selected .sx:hover { background:hsl(var(--c-game) / .24); }
.lw-dropdown { position:absolute; top:calc(100% + 5px); left:0; right:0; z-index:20; background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-md);
  box-shadow:var(--shadow-lg); padding:5px; display:flex; flex-direction:column; gap:1px; max-height:184px; overflow-y:auto; }
.lw-dopt { display:flex; align-items:center; gap:9px; padding:8px 10px; border-radius:var(--r-sm); border:none; background:transparent; cursor:pointer; text-align:left;
  font-family:var(--f-display); font-weight:600; font-size:13px; color:var(--text); }
.lw-dopt:hover, .lw-dopt.hl { background:var(--bg-muted); }
.lw-dopt .de { font-size:15px; }
.lw-dopt .dmeta { margin-left:auto; font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }

/* priority radio chips */
.lw-prioset { display:flex; gap:8px; }
.lw-priochip { flex:1; display:inline-flex; align-items:center; justify-content:center; gap:6px; padding:10px 8px; border-radius:var(--r-md); cursor:pointer;
  background:var(--bg-card); border:1.5px solid var(--border); color:var(--text-sec); font-family:var(--f-display); font-weight:700; font-size:12.5px; transition:all var(--dur-sm) var(--ease-out); }
.lw-priochip:hover { background:var(--bg-hover); }
.lw-priochip.on { background:hsl(var(--p) / .14); border-color:hsl(var(--p) / .5); color:hsl(var(--p)); }
.lw-priochip.low.on { color:var(--text); border-color:var(--border-strong); background:var(--bg-muted); }

/* number + textarea */
.lw-numwrap { position:relative; display:flex; align-items:center; }
.lw-numwrap .pfx { position:absolute; left:12px; font-family:var(--f-mono); font-size:13px; color:var(--text-muted); pointer-events:none; }
.lw-numwrap input { width:100%; padding:10px 12px 10px 28px; border-radius:var(--r-md); border:1.5px solid var(--border); background:var(--bg-card); color:var(--text);
  font-family:var(--f-mono); font-size:13px; outline:none; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.lw-numwrap input:focus { border-color:hsl(var(--c-game) / .6); box-shadow:0 0 0 3px hsl(var(--c-game) / .14); }
.lw-ta { width:100%; padding:10px 12px; border-radius:var(--r-md); border:1.5px solid var(--border); background:var(--bg-card); color:var(--text);
  font-family:var(--f-body); font-size:13px; line-height:1.5; outline:none; resize:vertical; min-height:64px; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.lw-ta:focus { border-color:hsl(var(--c-game) / .6); box-shadow:0 0 0 3px hsl(var(--c-game) / .14); }
.lw-ta::placeholder { color:var(--text-muted); }
.lw-hint { font-size:11px; color:var(--text-muted); display:inline-flex; align-items:center; gap:5px; }

.lw-mfoot { flex-shrink:0; display:flex; align-items:center; gap:10px; padding:14px 20px; border-top:1px solid var(--border); }
.lw-mfoot .grow { flex:1; }
.lw-mbtn { display:inline-flex; align-items:center; justify-content:center; gap:6px; padding:10px 16px; border-radius:var(--r-md); border:1px solid var(--border-strong); background:var(--bg-card);
  color:var(--text-sec); font-family:var(--f-display); font-weight:700; font-size:13px; cursor:pointer; transition:all var(--dur-sm) var(--ease-out); }
.lw-mbtn:hover { background:var(--bg-muted); color:var(--text); }
.lw-mbtn.primary { background:hsl(var(--c-game)); border-color:transparent; color:#fff; box-shadow:0 4px 14px hsl(var(--c-game) / .3); }
.lw-mbtn.primary:hover { filter:brightness(1.05); background:hsl(var(--c-game)); color:#fff; }
.lw-mbtn.primary:disabled { background:var(--bg-muted); color:var(--text-muted); box-shadow:none; cursor:not-allowed; border-color:var(--border); }
.lw-spin { width:14px; height:14px; border-radius:50%; border:2px solid rgba(255,255,255,.4); border-top-color:#fff; animation:lw-spin .7s linear infinite; }

/* toast */
.lw-toast { position:absolute; right:20px; bottom:20px; z-index:70; display:flex; align-items:center; gap:10px; padding:12px 16px; border-radius:var(--r-md);
  background:var(--bg-card); border:1px solid hsl(var(--c-game) / .35); box-shadow:var(--shadow-lg); animation:lw-toast-in var(--dur-md) var(--ease-spring); }
.lw-toast .tk { width:26px; height:26px; border-radius:50%; flex-shrink:0; display:inline-flex; align-items:center; justify-content:center; background:hsl(var(--c-game) / .16); color:hsl(var(--c-game)); font-size:14px; }
.lw-toast .tt { font-family:var(--f-display); font-weight:700; font-size:13px; color:var(--text); }

/* ─ mobile adaptations ─ */
.lw-app.is-mobile .lw-head { padding:12px 14px 0; }
.lw-app.is-mobile .lw-h1 { font-size:20px; white-space:normal; }
.lw-app.is-mobile .lw-htop { flex-direction:column; gap:10px; }
.lw-app.is-mobile .lw-hright { flex-direction:row; align-items:center; align-self:stretch; flex-wrap:wrap; justify-content:space-between; }
.lw-app.is-mobile .lw-toolbar { padding:11px 14px; gap:10px; }
.lw-app.is-mobile .lw-search { flex:1 1 100%; max-width:100%; }
.lw-app.is-mobile .lw-chips { flex:1 1 100%; flex-wrap:wrap; overflow:visible; row-gap:8px; }
.lw-app.is-mobile .lw-chip { flex:1 1 auto; justify-content:center; }
.lw-app.is-mobile .lw-right { flex:1 1 100%; }
.lw-app.is-mobile .lw-sortbtn { flex:1; justify-content:center; }
.lw-app.is-mobile .lw-body { padding:14px; }
.lw-app.is-mobile .lw-grid { grid-template-columns:repeat(auto-fill, minmax(150px, 1fr)); gap:12px; }
.lw-app.is-mobile .lw-card { min-height:0; }
.lw-app.is-mobile .lw-fsum { padding:8px 14px; }
.lw-app.is-mobile .lw-overlay { padding:0; align-items:flex-end; }
.lw-app.is-mobile .lw-modal { width:100%; max-height:94%; border-radius:var(--r-2xl) var(--r-2xl) 0 0; animation:lw-sheet-in var(--dur-lg) var(--ease-spring); }
.lw-app.is-mobile .lw-toast { left:14px; right:14px; bottom:14px; }
`;

window.__LW_CSS = LW_CSS;
