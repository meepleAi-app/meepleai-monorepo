/* MeepleAI SP4 wave 4 — S · #1490 · 3/4 — sp4-toolkit-templates
   Route: /toolkit/templates — Gallery dei template toolkit approvati clonabili.
          Un template è un "preset toolkit" pre-configurato con set di tool
          (dice / cards / timer / counter) bilanciati per un tipo di gioco.
   File: admin-mockups/design_files/sp4-toolkit-templates.{html,jsx,-ui.jsx}
   Pattern: Hero + body con grid gallery (riusa grid sp4-library-desktop +
            filter chips / search debounce da S2 #1490). NO sidebar, NO split-view.
            Desktop = grid 4-col, tablet 3-col, mobile 2-col / 1-col.

   Source restyle (NO ridisegnare logica):
     apps/web/src/app/(authenticated)/toolkit/templates/page.tsx
   API: api.gameToolkit.getApprovedTemplates(category?) →
        Array<{ id, name, diceTools[], cardTools[], timerTools[], counterTools[],
                stateTemplate{category,description}, creator{name,userId,isOfficial},
                usageCount, rating, createdAt }>

   Entity: --c-toolkit (verde, continuity con S1+S2) primaria · category entities:
           Strategy→--c-game · Party→--c-event · CardGames→--c-kb · Cooperative→--c-success.
           Creator → EntityChip --c-player. Tool types (dice/cards/timer/counter) = internal.

   8 stati (state picker continuity, persistito localStorage `tt-state`):
     default · filter-strategy · filter-empty · default-3col-tablet ·
     clone-modal-open · loading · error · mobile-stack

   Deviazione flaggata: i template "ufficiali" sono attribuiti a creator reali
   (Marco R. / Sara T. / Aaron R.) con flag isOfficial=true (curati/verificati dal team),
   per rispettare "dati realistici" del _common.md invece di un autore di sistema anonimo.
*/

const { useState, useEffect, useMemo, useRef, useCallback } = React;
const DS = window.DS;

const eHsl = (type, a) => {
  const c = DS.EC[type] || DS.EC.toolkit;
  return a !== undefined ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${a})` : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};
const sem = (name, a) => a !== undefined ? `hsl(var(--c-${name}) / ${a})` : `hsl(var(--c-${name}))`;

const MONTHS_SHORT = ['gen','feb','mar','apr','mag','giu','lug','ago','set','ott','nov','dic'];
const PAD = n => String(n).padStart(2, '0');
const NOW = new Date(2026, 4, 28, 16, 5); // 28 mag 2026 (allineato a S2)
const fmtDate = d => `${d.getDate()} ${MONTHS_SHORT[d.getMonth()]} ${d.getFullYear()}`;
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
// ─── CATEGORY META (entity-mapped) ──────────────────
// ═══════════════════════════════════════════════════════
const CATS = {
  Strategy:    { key: 'Strategy',    label: 'Strategy',    it: 'Strategia',   icon: '🎯', ent: 'game' },
  Party:       { key: 'Party',       label: 'Party',       it: 'Party',       icon: '🎉', ent: 'event' },
  CardGames:   { key: 'CardGames',   label: 'Card Games',  it: 'Carte',       icon: '📄', ent: 'kb' },
  Cooperative: { key: 'Cooperative', label: 'Cooperative', it: 'Cooperativo', icon: '🤝', ent: 'success' },
};
const CAT_ORDER = ['Strategy', 'Party', 'CardGames', 'Cooperative'];

// tool type meta (internal, NON entity)
const TOOL_META = {
  dice:    { icon: '🎲', label: 'dadi',      one: 'dado',      tip: c => `${c} ${c === 1 ? 'dado' : 'dadi'} · 6 facce` },
  cards:   { icon: '🃏', label: 'mazzi',     one: 'mazzo',     tip: c => `${c} ${c === 1 ? 'mazzo' : 'mazzi'} di carte` },
  timer:   { icon: '⏱️', label: 'timer',     one: 'timer',     tip: c => `${c} timer turno` },
  counter: { icon: '🔢', label: 'contatori', one: 'contatore', tip: c => `${c} ${c === 1 ? 'contatore' : 'contatori'}` },
};
const TOOL_ORDER = ['dice', 'cards', 'timer', 'counter'];

// creators (cross-ref data.js players + 1 extra)
const CREATORS = {
  'Marco R.': { name: 'Marco R.', initials: 'MR', color: 262 },
  'Sara T.':  { name: 'Sara T.',  initials: 'ST', color: 320 },
  'Aaron R.': { name: 'Aaron R.', initials: 'AR', color: 38  },
};

// ═══════════════════════════════════════════════════════
// ─── FIXTURE — 24 template deterministici ───────────
// ═══════════════════════════════════════════════════════
// [name, cat, desc, dice, cards, timer, counter, creator, official, usage, rating, daysAgo, pending]
const RAW = [
  // ── Strategy (8) ──
  ['Strategy Standard', 'Strategy', 'Setup classico per giochi euro e familiari: dadi, timer e contatori bilanciati per partite fluide.', 2, 0, 1, 4, 'Marco R.', false, 124, 4.5, 210, false],
  ['Euro Classico', 'Strategy', 'Per gestionali a piazzamento lavoratori: nessun dado, molti contatori risorsa da tracciare.', 0, 0, 1, 6, 'Sara T.', false, 98, 4.0, 150, false],
  ['Worker Placement Pro', 'Strategy', 'Preset curato per piazzamento lavoratori competitivo, con timer per turno e tracker azioni.', 0, 0, 1, 5, 'Sara T.', true, 156, 5.0, 300, false],
  ['Engine Builder', 'Strategy', 'Ottimizzato per motori di produzione: un mazzo sviluppo e sei contatori sinergia, niente timer.', 0, 1, 0, 6, 'Marco R.', false, 67, 4.0, 70, false],
  ['Heavy Eurogame', 'Strategy', 'Configurazione pesante per partite lunghe: due timer di fase e otto contatori per economie complesse.', 1, 0, 2, 8, 'Aaron R.', false, 41, 4.5, 95, false],
  ['Tableau Builder', 'Strategy', 'Costruzione tableau con due mazzi carte e quattro contatori di combo e ricorrenze.', 0, 2, 0, 4, 'Aaron R.', false, 52, 4.0, 25, false],
  ['4X Galattico', 'Strategy', 'Esplora, espandi, sfrutta, stermina: dadi combattimento, mazzo tecnologie e tracker territori.', 3, 1, 1, 5, 'Sara T.', false, 33, 3.5, 40, false],
  ['Economic Sim', 'Strategy', 'Simulazione economica avanzata con mercato dinamico — preset in fase di approvazione dal team.', 2, 0, 2, 7, 'Marco R.', false, 0, 0, 3, true],

  // ── Party (6) ──
  ['Party Quick', 'Party', 'Veloce e leggero per serate in compagnia: due dadi e due contatori punteggio, zero attese.', 2, 0, 0, 2, 'Sara T.', true, 142, 4.5, 180, false],
  ['Social Deduction', 'Party', 'Per giochi di ruolo nascosto: timer di discussione e contatori voti e sospetti.', 0, 2, 1, 2, 'Aaron R.', false, 89, 4.5, 110, false],
  ['Festa Veloce', 'Party', 'Round rapidi a squadre: un dado, un mazzo prove e tre contatori punteggio.', 1, 1, 0, 3, 'Marco R.', false, 76, 4.0, 60, false],
  ['Family Fun', 'Party', 'Pensato per famiglie e bambini: dado singolo e due contatori, nessuno stress da setup.', 1, 0, 0, 2, 'Marco R.', false, 63, 4.0, 20, false],
  ['Quiz Night', 'Party', 'Serata quiz a squadre: timer di risposta e quattro contatori, uno per ogni team.', 0, 0, 1, 4, 'Sara T.', false, 54, 3.5, 30, false],
  ['Goliardia', 'Party', 'Nuovo preset per serate goliardiche: dadi penalità e timer sfida a rotazione.', 2, 1, 1, 0, 'Aaron R.', false, 12, 3.5, 4, false],

  // ── CardGames (5) ──
  ['Deck Builder', 'CardGames', 'Preset curato per deck-building: mazzo principale, scarti e quattro contatori risorse.', 0, 3, 0, 4, 'Marco R.', true, 134, 4.5, 250, false],
  ['Card Battle', 'CardGames', 'Scontri a carte uno contro uno: due mazzi e tre contatori per vita ed energia.', 0, 2, 0, 3, 'Marco R.', false, 71, 4.0, 85, false],
  ['Briscola Digitale', 'CardGames', 'Classici italiani di carte: mazzo da 40 e due contatori per il punteggio delle mani.', 0, 1, 0, 2, 'Aaron R.', false, 58, 4.5, 33, false],
  ['Trick Taking', 'CardGames', 'Per giochi di prese: mazzo da 52, timer di mano e due contatori prese vinte.', 0, 1, 1, 2, 'Sara T.', false, 47, 4.0, 45, false],
  ['Solitario Pro', 'CardGames', 'Solitari avanzati con sfida a tempo: due mazzi, un timer e un contatore mosse.', 0, 2, 1, 1, 'Marco R.', false, 22, 3.5, 12, false],

  // ── Cooperative (5) ──
  ['Pandemic Style', 'Cooperative', 'Preset curato per cooperativi a diffusione: dado eventi, mazzo crisi e contatori focolaio.', 1, 1, 0, 6, 'Aaron R.', true, 119, 5.0, 220, false],
  ['Co-op Survival', 'Cooperative', 'Sopravvivenza cooperativa: dadi minaccia e cinque contatori per risorse condivise dal gruppo.', 2, 0, 0, 5, 'Sara T.', false, 81, 4.5, 130, false],
  ['Dungeon Crawler', 'Cooperative', 'Esplorazione dungeon in squadra: dadi combattimento, mazzo eventi e contatori HP per eroe.', 3, 2, 0, 4, 'Aaron R.', false, 64, 4.0, 50, false],
  ['Spirit Sync', 'Cooperative', 'Sinergia tra spiriti: nessun dado, mazzo poteri e cinque contatori energia e presenza.', 0, 2, 0, 5, 'Marco R.', false, 38, 4.5, 28, false],
  ['Legacy Campaign', 'Cooperative', 'Campagna legacy persistente: timer di sessione, mazzo storia e sei contatori di progressione.', 1, 1, 1, 6, 'Sara T.', false, 9, 4.0, 5, false],
];

function buildTemplates() {
  return RAW.map((r, i) => {
    const [name, category, description, dice, cards, timer, counter, creatorName, official, usageCount, rating, daysAgo, pending] = r;
    const createdAt = new Date(NOW.getTime() - daysAgo * 86400000);
    const tools = { dice, cards, timer, counter };
    const slug = name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
    return {
      id: `tpl-${PAD(i)}-${slug}`,
      name, category, description, tools,
      creator: { ...CREATORS[creatorName], isOfficial: official },
      usageCount, rating, createdAt, daysAgo,
      pending: !!pending,
      popular: usageCount > 100 && !pending,
      recent: daysAgo < 7 && !pending,
      official,
      // deterministic "growth this week" for popular cards
      weekGrowth: usageCount > 100 ? 6 + (i % 9) : 0,
    };
  });
}
const TEMPLATES = buildTemplates();
const TOTAL = TEMPLATES.length;
const CLONE_TOTAL = TEMPLATES.reduce((a, t) => a + t.usageCount, 0);
const CAT_COUNTS = Object.fromEntries(CAT_ORDER.map(c => [c, TEMPLATES.filter(t => t.category === c).length]));

// games library for the clone modal target dropdown
const LIB_GAMES = DS.games.filter(g => g.status === 'owned');

window.__TT_CSS = ''; // filled below
window.__TT = {
  eHsl, sem, fmtDate, relDate, NOW,
  CATS, CAT_ORDER, CAT_COUNTS, TOOL_META, TOOL_ORDER, CREATORS,
  TEMPLATES, TOTAL, CLONE_TOTAL, LIB_GAMES,
};

// ═══════════════════════════════════════════════════════
// ─── COMPONENT CSS (inject) — solo token da tokens.css ──
// ═══════════════════════════════════════════════════════
const TT_CSS = `
.tt-app { display:flex; flex-direction:column; height:100%; min-height:0; background:var(--bg); color:var(--text); position:relative; overflow:hidden; }

/* ─ error banner ─ */
.tt-errbar { flex-shrink:0; display:flex; align-items:center; gap:11px; padding:11px 18px; font-family:var(--f-display); font-weight:700; font-size:13px;
  background:hsl(var(--c-danger) / .12); color:hsl(var(--c-danger)); border-bottom:1px solid hsl(var(--c-danger) / .3); }
.tt-errbar .grow { flex:1; }
.tt-errbar .retry { display:inline-flex; align-items:center; gap:6px; padding:6px 13px; border-radius:var(--r-md); border:1px solid hsl(var(--c-danger) / .4);
  background:var(--bg-card); color:hsl(var(--c-danger)); font-family:var(--f-display); font-weight:700; font-size:13px; cursor:pointer; }

/* ─ header (sticky) ─ */
.tt-head { flex-shrink:0; position:sticky; top:0; z-index:12; background:var(--glass-bg); backdrop-filter:blur(14px); border-bottom:1px solid var(--border); padding:14px 22px 0; }
.tt-htop { display:flex; align-items:flex-start; gap:16px; }
.tt-htxt { min-width:0; flex:1; }
.tt-bread { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); letter-spacing:.04em; display:flex; align-items:center; gap:6px; margin-bottom:7px; }
.tt-bread .sep { opacity:.5; }
.tt-bread .cur { color:var(--text-sec); font-weight:700; }
.tt-titlerow { display:flex; align-items:center; gap:10px; }
.tt-ico { width:34px; height:34px; flex-shrink:0; border-radius:var(--r-md); background:hsl(var(--c-toolkit) / .16); color:hsl(var(--c-toolkit));
  display:inline-flex; align-items:center; justify-content:center; font-size:18px; }
.tt-h1 { font-family:var(--f-display); font-weight:800; font-size:30px; letter-spacing:-.02em; line-height:1.1; color:var(--text); white-space:nowrap; }
.tt-sub { font-size:14px; color:var(--text-sec); margin-top:5px; max-width:560px; }
.tt-hright { display:flex; flex-direction:column; align-items:flex-end; gap:10px; flex-shrink:0; }
.tt-qstat { display:inline-flex; align-items:center; gap:7px; font-family:var(--f-mono); font-size:11px; color:var(--text-muted); white-space:nowrap; }
.tt-qstat b { color:var(--text-sec); font-weight:700; }
.tt-cta { display:inline-flex; align-items:center; gap:7px; padding:9px 16px; border-radius:var(--r-md); background:hsl(var(--c-toolkit));
  border:none; color:#fff; font-family:var(--f-display); font-weight:800; font-size:13px; cursor:pointer; white-space:nowrap;
  box-shadow:0 4px 14px hsl(var(--c-toolkit) / .35); transition:all var(--dur-sm) var(--ease-out); }
.tt-cta:hover { filter:brightness(1.05); transform:translateY(-1px); }

/* ─ tabs nav ─ */
.tt-tabs { display:flex; gap:4px; margin-top:14px; overflow-x:auto; scrollbar-width:none; }
.tt-tabs::-webkit-scrollbar { display:none; }
.tt-tab { display:inline-flex; align-items:center; gap:6px; padding:9px 14px 11px; border:none; background:transparent; cursor:pointer; white-space:nowrap;
  border-bottom:2px solid transparent; color:var(--text-muted); font-family:var(--f-display); font-weight:700; font-size:13px; transition:color var(--dur-sm); text-decoration:none; }
.tt-tab:hover { color:var(--text-sec); }
.tt-tab.on { color:hsl(var(--c-toolkit)); border-bottom-color:hsl(var(--c-toolkit)); }

/* ─ toolbar ─ */
.tt-toolbar { flex-shrink:0; display:flex; align-items:center; gap:12px; padding:11px 22px; background:var(--bg); border-bottom:1px solid var(--border); flex-wrap:wrap; row-gap:10px; }
.tt-search { flex:0 1 300px; max-width:340px; min-width:160px; position:relative; }
.tt-search .ic { position:absolute; left:11px; top:50%; transform:translateY(-50%); font-size:13px; opacity:.6; pointer-events:none; }
.tt-search input { width:100%; padding:9px 70px 9px 32px; border-radius:var(--r-md); border:1.5px solid var(--border);
  background:var(--bg-card); font-family:var(--f-body); font-size:13px; color:var(--text); outline:none; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.tt-search input::placeholder { color:var(--text-muted); }
.tt-search input:focus, .tt-search.active input { border-color:hsl(var(--c-toolkit) / .6); box-shadow:0 0 0 3px hsl(var(--c-toolkit) / .14); }
.tt-search .clear { position:absolute; right:9px; top:50%; transform:translateY(-50%); width:20px; height:20px; border-radius:var(--r-pill);
  border:none; background:var(--bg-muted); color:var(--text-sec); cursor:pointer; font-size:11px; display:inline-flex; align-items:center; justify-content:center; }
.tt-search .clear:hover { background:var(--border-strong); color:var(--text); }
.tt-search .busy { position:absolute; right:34px; top:50%; transform:translateY(-50%); display:inline-flex; align-items:center; gap:5px;
  font-family:var(--f-mono); font-size:10px; color:hsl(var(--c-info)); white-space:nowrap; }
.tt-search .busy i { width:6px; height:6px; border-radius:50%; background:currentColor; animation:tt-typedot 1s var(--ease-in-out) infinite; }

/* category filter chips (radiogroup, single-select) */
.tt-chips { flex:1 1 auto; display:flex; align-items:center; gap:8px; flex-wrap:wrap; row-gap:8px; min-width:0; padding:2px; }
.tt-chips.scroll { flex-wrap:nowrap; overflow-x:auto; scrollbar-width:none; }
.tt-chips.scroll::-webkit-scrollbar { display:none; }
.tt-chip { display:inline-flex; align-items:center; gap:7px; padding:7px 13px; border-radius:var(--r-pill); white-space:nowrap; flex-shrink:0;
  background:var(--bg-card); border:1.5px solid var(--border); color:var(--text-sec); cursor:pointer; transition:all var(--dur-sm) var(--ease-out);
  font-family:var(--f-display); font-weight:700; font-size:12.5px; }
.tt-chip:hover { background:var(--bg-hover); }
.tt-chip .cdot { width:8px; height:8px; border-radius:50%; flex-shrink:0; background:var(--text-muted); }
.tt-chip .ccount { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); opacity:.9; }
/* "Tutte" = neutral toolkit */
.tt-chip.all.on { color:var(--text); background:var(--bg-muted); border-color:var(--border-strong); }
.tt-chip.all.on .cdot { background:hsl(var(--c-toolkit)); }
.tt-chip.all.on .ccount { color:var(--text-sec); }
/* category-tinted active (via --e) */
.tt-chip.cat .cdot { background:hsl(var(--e)); }
.tt-chip.cat.on { background:hsl(var(--e) / .14); border-color:hsl(var(--e) / .4); color:hsl(var(--e)); }
.tt-chip.cat.on .ccount { color:currentColor; }

/* right cluster: sort + view */
.tt-right { flex-shrink:0; display:flex; align-items:center; gap:10px; }
.tt-fgroup { position:relative; display:flex; align-items:center; }
.tt-sortbtn { display:inline-flex; align-items:center; gap:7px; padding:8px 13px; border-radius:var(--r-md); background:var(--bg-card); border:1.5px solid var(--border);
  color:var(--text-sec); cursor:pointer; font-family:var(--f-display); font-weight:700; font-size:12.5px; white-space:nowrap; }
.tt-sortbtn:hover { background:var(--bg-hover); }
.tt-pop { position:absolute; top:calc(100% + 6px); right:0; z-index:30; background:var(--bg-card); border:1px solid var(--border);
  border-radius:var(--r-md); box-shadow:var(--shadow-lg); padding:6px; min-width:210px; display:flex; flex-direction:column; gap:2px; }
.tt-pophead { font-family:var(--f-mono); font-size:10px; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); padding:4px 8px 6px; }
.tt-popitem { display:flex; align-items:center; gap:9px; width:100%; padding:8px 10px; border-radius:var(--r-sm); border:none; background:transparent; cursor:pointer;
  color:var(--text); font-family:var(--f-display); font-weight:600; font-size:13px; text-align:left; }
.tt-popitem:hover { background:var(--bg-muted); }
.tt-popitem .check { width:16px; height:16px; border-radius:50%; border:1.5px solid var(--border-strong); flex-shrink:0; display:inline-flex; align-items:center; justify-content:center; font-size:10px; color:hsl(var(--c-toolkit)); }
.tt-popitem.sel .check { border-color:hsl(var(--c-toolkit)); }
.tt-popitem .lbl { flex:1; }

.tt-vtoggle { display:inline-flex; padding:3px; gap:2px; background:var(--bg-muted); border-radius:var(--r-md); border:1px solid var(--border); }
.tt-vtoggle button { display:inline-flex; align-items:center; gap:5px; padding:6px 10px; border-radius:var(--r-sm); border:none; background:transparent; color:var(--text-muted);
  font-family:var(--f-display); font-weight:700; font-size:11px; cursor:pointer; }
.tt-vtoggle button[aria-pressed="true"] { background:var(--bg-card); color:hsl(var(--c-toolkit)); box-shadow:var(--shadow-xs); }

/* active filter summary */
.tt-fsum { flex-shrink:0; display:flex; align-items:center; gap:10px; padding:8px 22px; background:var(--bg-sunken); border-bottom:1px solid var(--border-light);
  font-family:var(--f-mono); font-size:11px; color:var(--text-sec); }
.tt-fsum .badge { display:inline-flex; align-items:center; gap:6px; padding:3px 9px; border-radius:var(--r-pill); background:hsl(var(--e, var(--c-toolkit)) / .14); color:hsl(var(--e)); font-weight:700; }
.tt-fsum .clear { background:transparent; border:none; cursor:pointer; color:hsl(var(--c-warning)); font-family:var(--f-display); font-weight:800; font-size:12px; }
.tt-fsum .grow { flex:1; }

/* ─ body / grid ─ */
.tt-body { flex:1; overflow:auto; min-height:0; position:relative; padding:18px 22px 26px; }
.tt-grid { display:grid; gap:16px; grid-template-columns:repeat(var(--cols, 4), minmax(0, 1fr)); }
.tt-grid.list { grid-template-columns:1fr; gap:11px; }

/* ─ template card ─ */
.tt-card { position:relative; display:flex; flex-direction:column; gap:13px; min-height:322px; padding:17px 16px 16px;
  background:var(--bg-card); border:1px solid var(--border-light); border-radius:var(--r-lg); box-shadow:var(--shadow-sm);
  transition:box-shadow var(--dur-sm) var(--ease-out), border-color var(--dur-sm) var(--ease-out), transform var(--dur-sm) var(--ease-out); }
.tt-card:hover { box-shadow:var(--shadow-md); border-color:hsl(var(--c-toolkit) / .4); transform:translateY(-2px); }
.tt-card.official { border-color:hsl(var(--c-toolkit) / .35); box-shadow:0 0 0 1px hsl(var(--c-toolkit) / .12), var(--shadow-sm); }
.tt-card.pending { opacity:.66; }
.tt-card.pending:hover { transform:none; box-shadow:var(--shadow-sm); border-color:var(--border-light); }

/* corner badges */
.tt-corner { position:absolute; top:-9px; right:13px; display:inline-flex; align-items:center; gap:5px; padding:3px 9px; border-radius:var(--r-pill);
  font-family:var(--f-display); font-weight:800; font-size:10px; letter-spacing:.04em; text-transform:uppercase; box-shadow:var(--shadow-xs); z-index:2; }
.tt-corner.new { background:hsl(var(--c-info)); color:#fff; }
.tt-crown { position:absolute; top:-10px; left:13px; width:24px; height:24px; border-radius:var(--r-pill); display:inline-flex; align-items:center; justify-content:center;
  background:hsl(var(--c-warning)); color:#fff; font-size:12px; box-shadow:var(--shadow-sm); z-index:2; }

/* card header */
.tt-chead { display:flex; align-items:flex-start; gap:10px; }
.tt-cicon { width:30px; height:30px; flex-shrink:0; border-radius:var(--r-sm); display:inline-flex; align-items:center; justify-content:center; font-size:16px;
  background:hsl(var(--e) / .14); }
.tt-chtxt { flex:1; min-width:0; }
.tt-cname { font-family:var(--f-display); font-weight:800; font-size:16px; line-height:1.2; color:var(--text); overflow:hidden; text-overflow:ellipsis; white-space:nowrap;
  border:none; background:transparent; padding:0; cursor:pointer; text-align:left; width:100%; }
.tt-cname:hover { color:hsl(var(--c-toolkit)); }
.tt-ccat { font-family:var(--f-mono); font-size:10px; color:hsl(var(--e)); text-transform:uppercase; letter-spacing:.05em; margin-top:3px; font-weight:600; }
.tt-rating { display:inline-flex; align-items:center; gap:1px; flex-shrink:0; }
.tt-star { font-size:12px; line-height:1; position:relative; color:var(--border-strong); }
.tt-star.full { color:hsl(var(--c-warning)); }
.tt-star.half { color:var(--border-strong); }
.tt-star.half::before { content:'★'; position:absolute; left:0; top:0; width:50%; overflow:hidden; color:hsl(var(--c-warning)); }
.tt-ratenew { font-family:var(--f-mono); font-size:10px; color:hsl(var(--c-info)); font-weight:700; padding:2px 7px; border-radius:var(--r-pill); background:hsl(var(--c-info) / .12); }

/* description */
.tt-desc { font-size:12.5px; color:var(--text-sec); line-height:var(--lh-snug); margin:0;
  display:-webkit-box; -webkit-line-clamp:2; -webkit-box-orient:vertical; overflow:hidden; min-height:34px; }

/* tool composition */
.tt-tools { display:grid; grid-template-columns:repeat(4, 1fr); gap:7px; }
.tt-tool { display:flex; flex-direction:column; align-items:center; gap:3px; padding:9px 4px; border-radius:var(--r-sm);
  background:var(--bg-muted); border:1px solid var(--border-light); position:relative; cursor:default; }
.tt-tool.empty { opacity:.4; }
.tt-tool .ti { font-size:15px; line-height:1; }
.tt-tool .tc { font-family:var(--f-mono); font-size:13px; font-weight:700; color:var(--text); font-variant-numeric:tabular-nums; }
.tt-tool.empty .tc { color:var(--text-muted); font-weight:600; }
.tt-tool .tip { position:absolute; bottom:calc(100% + 6px); left:50%; transform:translateX(-50%); white-space:nowrap; z-index:5;
  background:var(--text); color:var(--bg-card); font-family:var(--f-mono); font-size:10px; padding:4px 8px; border-radius:var(--r-xs);
  opacity:0; pointer-events:none; transition:opacity var(--dur-xs); box-shadow:var(--shadow-md); }
.tt-tool:hover .tip { opacity:1; }

.tt-spacer { flex:1; }

/* divider */
.tt-cdiv { height:1px; background:var(--border-light); }

/* creator + meta row */
.tt-meta { display:flex; align-items:center; gap:8px; }
.tt-creator { display:inline-flex; align-items:center; gap:7px; min-width:0; padding:3px 9px 3px 3px; border-radius:var(--r-pill);
  background:hsl(var(--c-player) / .1); border:1px solid hsl(var(--c-player) / .2); }
.tt-cav { width:18px; height:18px; flex-shrink:0; border-radius:50%; display:inline-flex; align-items:center; justify-content:center;
  font-family:var(--f-display); font-size:8px; font-weight:800; color:#fff; }
.tt-cnm { font-family:var(--f-display); font-weight:700; font-size:11.5px; color:hsl(var(--c-player)); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; min-width:0; }
.tt-official { display:inline-flex; align-items:center; gap:4px; padding:3px 8px; border-radius:var(--r-pill); flex-shrink:0;
  background:hsl(var(--c-warning) / .14); color:hsl(var(--c-warning)); font-family:var(--f-display); font-weight:800; font-size:10px; }
.tt-clones { margin-left:auto; display:inline-flex; align-items:center; gap:5px; font-family:var(--f-mono); font-size:11px; color:var(--text-muted); white-space:nowrap; flex-shrink:0; }
.tt-clones .fire { color:hsl(var(--c-game)); }
.tt-grow { font-family:var(--f-mono); font-size:10px; color:hsl(var(--c-success)); font-weight:700; }

/* footer CTA */
.tt-clonebtn { display:inline-flex; align-items:center; justify-content:center; gap:7px; width:100%; padding:10px; border-radius:var(--r-md); border:none;
  background:hsl(var(--c-toolkit)); color:#fff; font-family:var(--f-display); font-weight:800; font-size:13px; cursor:pointer;
  transition:all var(--dur-sm) var(--ease-out); }
.tt-clonebtn:hover { filter:brightness(1.05); transform:translateY(-1px); box-shadow:0 4px 14px hsl(var(--c-toolkit) / .35); }
.tt-clonebtn.disabled { background:var(--bg-muted); color:var(--text-muted); cursor:not-allowed; box-shadow:none; }
.tt-clonebtn.disabled:hover { transform:none; filter:none; }

/* ─ list view row ─ */
.tt-grid.list .tt-card { flex-direction:row; align-items:center; min-height:0; gap:16px; padding:13px 16px; }
.tt-grid.list .tt-card .tt-chead { flex:0 0 220px; }
.tt-grid.list .tt-desc { flex:1; -webkit-line-clamp:2; min-height:0; }
.tt-grid.list .tt-tools { flex:0 0 200px; grid-template-columns:repeat(4,1fr); }
.tt-grid.list .tt-cdiv, .tt-grid.list .tt-spacer { display:none; }
.tt-grid.list .tt-meta { flex:0 0 auto; }
.tt-grid.list .tt-clonebtn { width:auto; flex:0 0 auto; padding:9px 16px; }
.tt-grid.list .tt-corner, .tt-grid.list .tt-crown { display:none; }

/* ─ empty / loading ─ */
.tt-pad { min-height:340px; display:flex; align-items:center; justify-content:center; padding:30px 24px; }
.tt-empty { text-align:center; max-width:420px; border:1.5px dashed var(--border-strong); border-radius:var(--r-xl); padding:46px 34px;
  display:flex; flex-direction:column; align-items:center; }
.tt-empty .em { width:70px; height:70px; border-radius:50%; display:inline-flex; align-items:center; justify-content:center; font-size:30px; margin-bottom:16px;
  background:hsl(var(--c-warning) / .12); }
.tt-empty h3 { font-family:var(--f-display); font-size:20px; font-weight:800; margin:0 0 8px; }
.tt-empty p { font-size:14px; color:var(--text-sec); line-height:1.55; margin:0 0 22px; }
.tt-empty .cta { display:inline-flex; align-items:center; gap:8px; padding:10px 19px; border-radius:var(--r-md); border:1px solid hsl(var(--c-warning) / .5);
  background:transparent; color:hsl(var(--c-warning)); font-family:var(--f-display); font-weight:800; font-size:14px; cursor:pointer; }

.tt-sk { border-radius:var(--r-sm); }
.tt-skcard { display:flex; flex-direction:column; gap:13px; min-height:322px; padding:17px 16px; background:var(--bg-card); border:1px solid var(--border-light); border-radius:var(--r-lg); }

/* ─ clone modal ─ */
.tt-overlay { position:absolute; inset:0; z-index:50; background:rgba(20,12,4,.46); backdrop-filter:blur(3px); display:flex; align-items:center; justify-content:center; padding:28px; animation:tt-overlay-in var(--dur-md) var(--ease-out); }
[data-theme="dark"] .tt-overlay { background:rgba(0,0,0,.62); }
.tt-modal { width:min(540px, 100%); max-height:100%; background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-xl); box-shadow:var(--shadow-lg);
  display:flex; flex-direction:column; overflow:hidden; animation:tt-modal-in var(--dur-md) var(--ease-spring); }
.tt-mhead { flex-shrink:0; display:flex; align-items:flex-start; gap:13px; padding:18px 20px; border-bottom:1px solid var(--border); }
.tt-mcov { width:40px; height:40px; flex-shrink:0; border-radius:var(--r-md); display:inline-flex; align-items:center; justify-content:center; font-size:19px; background:hsl(var(--e) / .16); }
.tt-mhtxt { flex:1; min-width:0; }
.tt-mtitle { font-family:var(--f-display); font-weight:800; font-size:19px; letter-spacing:-.01em; color:var(--text); }
.tt-msub { font-family:var(--f-mono); font-size:11px; color:var(--text-sec); margin-top:4px; }
.tt-mclose { width:32px; height:32px; flex-shrink:0; border-radius:var(--r-sm); border:none; background:var(--bg-muted); color:var(--text-muted); cursor:pointer; font-size:16px; }
.tt-mclose:hover { background:var(--border-strong); color:var(--text); }
.tt-mbody { flex:1; overflow-y:auto; padding:18px 20px; display:flex; flex-direction:column; gap:18px; }
.tt-field { display:flex; flex-direction:column; gap:7px; }
.tt-field label { font-family:var(--f-mono); font-size:10px; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); }
.tt-field input[type=text], .tt-field select { font-family:var(--f-body); font-size:13px; padding:10px 12px; border-radius:var(--r-md); border:1.5px solid var(--border);
  background:var(--bg-card); color:var(--text); outline:none; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.tt-field input:focus, .tt-field select:focus { border-color:hsl(var(--c-toolkit) / .6); box-shadow:0 0 0 3px hsl(var(--c-toolkit) / .14); }
.tt-toggle { display:flex; align-items:center; gap:11px; padding:11px 13px; border-radius:var(--r-md); border:1px solid var(--border-light); background:var(--bg-sunken); cursor:pointer; }
.tt-toggle .tlbl { flex:1; min-width:0; }
.tt-toggle .tlbl .tt { font-family:var(--f-display); font-weight:700; font-size:13px; color:var(--text); }
.tt-toggle .tlbl .td { font-size:11.5px; color:var(--text-muted); margin-top:2px; }
.tt-sw { width:40px; height:23px; border-radius:var(--r-pill); flex-shrink:0; background:var(--border-strong); position:relative; transition:background var(--dur-sm); }
.tt-sw::after { content:''; position:absolute; top:2px; left:2px; width:19px; height:19px; border-radius:50%; background:#fff; box-shadow:var(--shadow-sm); transition:transform var(--dur-sm) var(--ease-out); }
.tt-toggle.on .tt-sw { background:hsl(var(--c-toolkit)); }
.tt-toggle.on .tt-sw::after { transform:translateX(17px); }
.tt-summary { display:flex; flex-direction:column; gap:8px; padding:13px; border-radius:var(--r-md); background:hsl(var(--c-toolkit) / .07); border:1px solid hsl(var(--c-toolkit) / .2); }
.tt-summary .sh { font-family:var(--f-mono); font-size:10px; text-transform:uppercase; letter-spacing:.06em; color:hsl(var(--c-toolkit)); font-weight:700; }
.tt-summary .stools { display:flex; flex-wrap:wrap; gap:7px; }
.tt-summary .stool { display:inline-flex; align-items:center; gap:5px; padding:4px 9px; border-radius:var(--r-pill); background:var(--bg-card); border:1px solid var(--border-light);
  font-family:var(--f-mono); font-size:11px; color:var(--text-sec); font-weight:600; }
.tt-mfoot { flex-shrink:0; display:flex; align-items:center; gap:10px; padding:14px 20px; border-top:1px solid var(--border); }
.tt-mbtn { display:inline-flex; align-items:center; justify-content:center; gap:6px; padding:10px 16px; border-radius:var(--r-md); border:1px solid var(--border-strong); background:var(--bg-card);
  color:var(--text); font-family:var(--f-display); font-weight:700; font-size:13px; cursor:pointer; transition:all var(--dur-sm) var(--ease-out); }
.tt-mbtn:hover { background:var(--bg-muted); }
.tt-mbtn.tk { flex:1; background:hsl(var(--c-toolkit)); border-color:transparent; color:#fff; box-shadow:0 4px 14px hsl(var(--c-toolkit) / .3); }
.tt-mbtn.tk:hover { filter:brightness(1.05); background:hsl(var(--c-toolkit)); }

/* ─ mobile adaptations ─ */
.tt-app.is-mobile .tt-head { padding:12px 14px 0; }
.tt-app.is-mobile .tt-h1 { font-size:20px; }
.tt-app.is-mobile .tt-htop { flex-direction:column; gap:10px; }
.tt-app.is-mobile .tt-hright { flex-direction:row; align-items:center; align-self:stretch; flex-wrap:wrap; }
.tt-app.is-mobile .tt-toolbar { padding:11px 14px; gap:10px; }
.tt-app.is-mobile .tt-search { flex:1 1 100%; max-width:100%; }
.tt-app.is-mobile .tt-chips { flex-wrap:nowrap; overflow-x:auto; scrollbar-width:none; }
.tt-app.is-mobile .tt-chips::-webkit-scrollbar { display:none; }
.tt-app.is-mobile .tt-right { flex:1 1 100%; justify-content:space-between; }
.tt-app.is-mobile .tt-body { padding:14px; }
.tt-app.is-mobile .tt-fsum { padding:8px 14px; }
.tt-app.is-mobile .tt-overlay { padding:0; align-items:flex-end; }
.tt-app.is-mobile .tt-modal { width:100%; max-height:92%; border-radius:var(--r-2xl) var(--r-2xl) 0 0; animation:tt-sheet-in var(--dur-lg) var(--ease-spring); }
`;

window.__TT_CSS = TT_CSS;
