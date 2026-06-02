/* MeepleAI SP4 wave 4 — M · #1490 · 4/4 — sp4-toolkit-play  (ULTIMO del cluster)
   Route: /toolkit/play — Toolkit standalone live per assistere partite physical.
          Helper cross-game (NON legato a sessioni): dadi, contatori, timer, randomizer
          + log streaming delle azioni. Giocatori fisici al tavolo, l'app fa solo da helper.
   File: admin-mockups/design_files/sp4-toolkit-play.{html,jsx,-tools.jsx,-ui.jsx}
   Pattern: Hero + body 2-col (desktop 65/35 tool/log) / stack mobile (log accordion bottom).
            NO sidebar fissa, NO split-view. Riusa header+tabs+state-picker S1+S2+S3.

   Source restyle (NO ridisegnare logica):
     apps/web/src/app/(authenticated)/toolkit/play/page.tsx
     components/toolkit/{DiceRoller,CounterTool,Timer,Randomizer}.tsx

   Entity: --c-toolkit (verde, primaria/header/tabs/log) · tool entities:
           dice→--c-game · counter→--c-toolkit · timer→--c-warning · random→--c-event.
           Actor → EntityChip --c-player (header + log entries).

   10 stati (state picker continuity, persistito localStorage `tp-state`):
     default · idle-empty-log · dice-rolling · timer-running · timer-expired ·
     counter-incrementing · randomizer-picking · filter-log-dice · clear-log-confirm · mobile-stack
*/

const { useState, useEffect, useMemo, useRef, useCallback, useLayoutEffect } = React;
const DS = window.DS;

const eHsl = (type, a) => {
  const c = DS.EC[type] || DS.EC.toolkit;
  return a !== undefined ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${a})` : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};
const sem = (name, a) => a !== undefined ? `hsl(var(--c-${name}) / ${a})` : `hsl(var(--c-${name}))`;
const PAD = n => String(n).padStart(2, '0');
const fmtClock = secs => `${PAD(Math.floor(Math.max(0, secs) / 60))}:${PAD(Math.max(0, secs) % 60)}`;
const reducedMotion = () => window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

// tool-type → entity + glyph (cross-reference brief)
const TOOL_ENT = {
  dice:    { ent: 'game',    icon: '🎲' },
  counter: { ent: 'toolkit', icon: '🔢' },
  timer:   { ent: 'warning', icon: '⏱' },
  random:  { ent: 'event',   icon: '🎰' },
};
const entVar = ent => ent === 'warning' || ent === 'success' || ent === 'danger' ? `var(--c-${ent})` : `var(--c-${ent})`;
const entE = ent => `hsl(${entVar(ent)})`;

// ═══════════════════════════════════════════════════════
// ─── DEFAULT TOOLKIT CONFIG (da page.tsx) ────────────
// ═══════════════════════════════════════════════════════
const DICE = [
  { name: 'D6',  sides: 6,  count: 1 },
  { name: '2D6', sides: 6,  count: 2 },
  { name: 'D20', sides: 20, count: 1 },
  { name: 'D4',  sides: 4,  count: 1 },
  { name: 'D8',  sides: 8,  count: 1 },
  { name: 'D10', sides: 10, count: 1 },
  { name: 'D12', sides: 12, count: 1 },
];
const TIMERS = [
  { id: 'tm-count', name: 'Timer countdown', type: 'countdown', defaultSeconds: 60,  icon: '⏱' },
  { id: 'tm-turn',  name: 'Timer turno',     type: 'turn',      defaultSeconds: 120, icon: '👤' },
];
const TIMER_PRESETS = [30, 60, 90, 120, 180, 300];
const COUNTER_INIT = { id: 'default-counter', name: 'Punti', value: 5 };
const RANDOM_ITEMS = ['Pizza', 'Sushi', 'Burger', 'Tacos'];

// actors (cross-ref data.js players, brief richiede Marco/Sara/Aaron)
const ACTORS = {
  'Marco R.': { name: 'Marco R.', initials: 'MR', color: 262 },
  'Sara T.':  { name: 'Sara T.',  initials: 'ST', color: 320 },
  'Aaron R.': { name: 'Aaron R.', initials: 'AR', color: 38  },
};
// resolve any actor label (known or free-typed) to chip data
const mkActor = name => {
  if (!name) return null;
  if (ACTORS[name]) return ACTORS[name];
  const parts = name.trim().split(/\s+/);
  const initials = (parts[0][0] + (parts[1] ? parts[1][0] : '')).toUpperCase();
  let h = 0; for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) % 360;
  return { name: name.trim(), initials, color: h };
};

// log filter chips (single-select)
const LOG_FILTERS = [
  { id: 'all',     label: 'Tutti',      icon: '▦' },
  { id: 'dice',    label: 'Dadi',       icon: '🎲' },
  { id: 'counter', label: 'Contatori',  icon: '🔢' },
  { id: 'timer',   label: 'Timer',      icon: '⏱' },
  { id: 'random',  label: 'Random',     icon: '🎰' },
];

// deterministic mock log (default state — orario fittizio serata)
const MOCK_LOG = [
  { id: 'lg-1', time: '16:42', toolType: 'dice',    action: 'roll',      result: 'D20 → 17',                actorLabel: 'Marco R.' },
  { id: 'lg-2', time: '16:41', toolType: 'timer',   action: 'start',     result: 'Timer turno avviato (120s)', actorLabel: 'Sara T.' },
  { id: 'lg-3', time: '16:40', toolType: 'counter', action: 'increment', result: 'Punti +1 = 5',            actorLabel: 'Marco R.' },
  { id: 'lg-4', time: '16:38', toolType: 'random',  action: 'pick',      result: 'Random pick: Sushi',      actorLabel: 'Aaron R.' },
  { id: 'lg-5', time: '16:35', toolType: 'dice',    action: 'roll',      result: '2D6 → 8',                 actorLabel: null },
];

// ═══════════════════════════════════════════════════════
// ─── DICE BUILDER — engine (parser / roller / format) ───
// ═══════════════════════════════════════════════════════
const DICE_TYPES = [4, 6, 8, 10, 12, 20, 100];
const DICE_PRESETS = ['2D6', '1D20', '1D100', '3D6+1', '4D6kh3', '6D6cs6'];
const DICE_SYNTAX = [
  ['NdM', 'N dadi di tipo dM', '2D6 · 1D20'],
  ['+N / −N', 'modificatore al totale', '2D6+3'],
  ['khN / klN', 'tieni gli N più alti / bassi', '4D6kh3'],
  ['csN', 'conta successi ≥ N', '6D6cs6'],
  ['rN', 're-roll sui tiri ≤ N', '2D6r1'],
  ['!', 'dadi esplosivi (max face)', '3D10!'],
];
const diceRnd = (min, max) => Math.floor(Math.random() * (max - min + 1)) + min;

// parse a formula string → { ok, spec } | { ok:false, error }
function parseFormula(raw) {
  if (!raw || !raw.trim()) return { ok: false, error: 'Inserisci una formula (es. 2D6)' };
  let s = raw.trim().toLowerCase().replace(/\s+/g, '').replace(/[−–]/g, '-');
  const m = s.match(/^(\d*)d(\d+)/);
  if (!m) return { ok: false, error: 'Manca il dado — usa NdM (es. 2d6)' };
  const count = m[1] === '' ? 1 : parseInt(m[1], 10);
  const sides = parseInt(m[2], 10);
  if (count < 1 || count > 20) return { ok: false, error: 'Quantità fuori range (1–20)' };
  if (!DICE_TYPES.includes(sides)) return { ok: false, error: `d${sides} non supportato (d4–d100)` };
  const spec = { count, sides, mod: 0, kh: null, kl: null, cs: null, reroll: null, exploding: false };
  let rest = s.slice(m[0].length);
  while (rest.length) {
    let mm;
    if ((mm = rest.match(/^kh(\d+)/))) spec.kh = +mm[1];
    else if ((mm = rest.match(/^kl(\d+)/))) spec.kl = +mm[1];
    else if ((mm = rest.match(/^cs(\d+)/))) spec.cs = +mm[1];
    else if ((mm = rest.match(/^r(\d+)/))) spec.reroll = +mm[1];
    else if ((mm = rest.match(/^!/))) spec.exploding = true;
    else if ((mm = rest.match(/^([+-]\d+)/))) spec.mod = +mm[1];
    else return { ok: false, error: `Sintassi non riconosciuta dopo «${s.slice(0, s.length - rest.length)}»` };
    rest = rest.slice(mm[0].length);
  }
  if (spec.kh != null && spec.kl != null) return { ok: false, error: 'kh e kl non sono combinabili' };
  if (spec.kh != null && spec.kh > count) return { ok: false, error: `kh${spec.kh} richiede ≥ ${spec.kh} dadi` };
  if (spec.kl != null && spec.kl > count) return { ok: false, error: `kl${spec.kl} richiede ≥ ${spec.kl} dadi` };
  if (spec.cs != null && (spec.cs < 1 || spec.cs > sides)) return { ok: false, error: `cs${spec.cs} fuori dal range del dado` };
  return { ok: true, spec };
}

// canonical formula string from a spec
function specToFormula(sp) {
  let f = `${sp.count}D${sp.sides}`;
  if (sp.kh != null) f += `kh${sp.kh}`;
  if (sp.kl != null) f += `kl${sp.kl}`;
  if (sp.cs != null) f += `cs${sp.cs}`;
  if (sp.reroll != null) f += `r${sp.reroll}`;
  if (sp.exploding) f += '!';
  if (sp.mod) f += `${sp.mod > 0 ? '+' : ''}${sp.mod}`;
  return f;
}

// human-readable interpretation
function describeSpec(sp) {
  const p = [`${sp.count} ${sp.count === 1 ? 'dado' : 'dadi'} d${sp.sides}`];
  if (sp.kh != null) p.push(`tieni i ${sp.kh} più alti`);
  if (sp.kl != null) p.push(`tieni i ${sp.kl} più bassi`);
  if (sp.cs != null) p.push(`conta successi ≥ ${sp.cs}`);
  if (sp.reroll != null) p.push(`re-roll ≤ ${sp.reroll}`);
  if (sp.exploding) p.push('esplosivi');
  if (sp.mod) p.push(`${sp.mod > 0 ? '+' : ''}${sp.mod} modificatore`);
  return p.join(', ');
}

// execute a spec → { rolls[], mode, total/successes, spec }
function rollSpec(sp) {
  const sides = sp.sides;
  const rolls = [];
  for (let i = 0; i < sp.count; i++) {
    let base = diceRnd(1, sides);
    let rerolledFrom = null;
    if (sp.reroll != null && base <= sp.reroll) { rerolledFrom = base; base = diceRnd(1, sides); }
    let value = base, exploded = false, guard = 0;
    if (sp.exploding) {
      let cur = base;
      while (cur === sides && guard < 20) { exploded = true; cur = diceRnd(1, sides); value += cur; guard++; }
    }
    rolls.push({ value, base, kept: true, rerolledFrom, exploded, crit: base === sides, fail: base === 1 });
  }
  if (sp.kh != null || sp.kl != null) {
    const n = sp.kh != null ? sp.kh : sp.kl;
    const sorted = rolls.map((r, idx) => ({ idx, v: r.value })).sort((a, b) => sp.kh != null ? b.v - a.v : a.v - b.v);
    const keep = new Set(sorted.slice(0, n).map(o => o.idx));
    rolls.forEach((r, idx) => { r.kept = keep.has(idx); });
  }
  if (sp.cs != null) {
    const successes = rolls.filter(r => r.kept && r.value >= sp.cs).length;
    return { rolls, mode: 'cs', successes, total: successes, spec: sp };
  }
  const sum = rolls.filter(r => r.kept).reduce((a, r) => a + r.value, 0);
  return { rolls, mode: 'sum', sum, total: sum + sp.mod, spec: sp };
}

// roll result → compact history/log shape
function resultToHistory(res, id) {
  const sp = res.spec;
  const formula = specToFormula(sp);
  const kept = res.rolls.filter(r => r.kept).map(r => r.value);
  let detail = null;
  if (sp.kh != null || sp.kl != null) detail = `tieni ${kept.join(',')}`;
  else if (res.rolls.length > 1) detail = res.rolls.map(r => r.value).join(',');
  if (sp.mod) detail = (detail ? detail + ' ' : '') + `${sp.mod > 0 ? '+' : ''}${sp.mod}`;
  const resultText = res.mode === 'cs'
    ? `${res.successes} ${res.successes === 1 ? 'successo' : 'successi'}`
    : `${res.total}`;
  return { id, formula, resultText, detail };
}

// fixture recent rolls (varietà di formule)
const DICE_HISTORY = [
  { id: 'dh1', formula: '4D6kh3+2', resultText: '17', detail: 'tieni 6,5,4 +2', time: '16:42', actorLabel: 'Marco R.' },
  { id: 'dh2', formula: '1D20',     resultText: '17', detail: null,            time: '16:40', actorLabel: 'Sara T.' },
  { id: 'dh3', formula: '3D6+1',    resultText: '13', detail: '4,2,6 +1',      time: '16:38', actorLabel: 'Aaron R.' },
  { id: 'dh4', formula: '6D6cs6',   resultText: '2 successi', detail: null,     time: '16:35', actorLabel: 'Marco R.' },
  { id: 'dh5', formula: '2D6r1',    resultText: '9',  detail: 're-roll 1→4',   time: '16:32', actorLabel: 'Sara T.' },
];

window.__TP = {
  eHsl, sem, PAD, fmtClock, reducedMotion,
  TOOL_ENT, entE, DICE, TIMERS, TIMER_PRESETS, COUNTER_INIT, RANDOM_ITEMS,
  ACTORS, mkActor, LOG_FILTERS, MOCK_LOG,
  DICE_TYPES, DICE_PRESETS, DICE_SYNTAX, DICE_HISTORY,
  parseFormula, specToFormula, describeSpec, rollSpec, resultToHistory,
};

// ═══════════════════════════════════════════════════════
// ─── COMPONENT CSS (inject) — solo token da tokens.css ──
// ═══════════════════════════════════════════════════════
const TP_CSS = `
.tp-app { display:flex; flex-direction:column; height:100%; min-height:0; background:var(--bg); color:var(--text); position:relative; overflow:hidden; }

/* ─ header (sticky) ─ */
.tp-head { flex-shrink:0; position:sticky; top:0; z-index:12; background:var(--glass-bg); backdrop-filter:blur(14px); border-bottom:1px solid var(--border); padding:14px 22px 0; }
.tp-htop { display:flex; align-items:flex-start; gap:16px; }
.tp-htxt { min-width:0; flex:1; }
.tp-bread { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); letter-spacing:.04em; display:flex; align-items:center; gap:6px; margin-bottom:7px; }
.tp-bread .sep { opacity:.5; }
.tp-bread .cur { color:var(--text-sec); font-weight:700; }
.tp-titlerow { display:flex; align-items:center; gap:10px; }
.tp-ico { width:34px; height:34px; flex-shrink:0; border-radius:var(--r-md); background:hsl(var(--c-toolkit) / .16); color:hsl(var(--c-toolkit));
  display:inline-flex; align-items:center; justify-content:center; font-size:18px; }
.tp-h1 { font-family:var(--f-display); font-weight:800; font-size:28px; letter-spacing:-.02em; line-height:1.1; color:var(--text); white-space:nowrap; }
.tp-sub { font-size:13.5px; color:var(--text-sec); margin-top:5px; max-width:540px; }

/* header right: actor input + config CTA */
.tp-hright { display:flex; flex-direction:column; align-items:flex-end; gap:10px; flex-shrink:0; }
.tp-actor { display:flex; align-items:center; gap:8px; }
.tp-actor-field { position:relative; display:flex; align-items:center; }
.tp-actor-field .ic { position:absolute; left:11px; font-size:13px; opacity:.6; pointer-events:none; }
.tp-actor input { width:188px; padding:8px 12px 8px 32px; border-radius:var(--r-md); border:1.5px solid var(--border);
  background:var(--bg-card); font-family:var(--f-body); font-size:13px; color:var(--text); outline:none; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.tp-actor input::placeholder { color:var(--text-muted); }
.tp-actor input:focus { border-color:hsl(var(--c-toolkit) / .6); box-shadow:0 0 0 3px hsl(var(--c-toolkit) / .14); }
.tp-actorchip { display:inline-flex; align-items:center; gap:7px; padding:5px 11px 5px 5px; border-radius:var(--r-pill);
  background:hsl(var(--c-player) / .12); border:1px solid hsl(var(--c-player) / .26); white-space:nowrap; }
.tp-actorchip .av { width:22px; height:22px; flex-shrink:0; border-radius:50%; display:inline-flex; align-items:center; justify-content:center;
  font-family:var(--f-display); font-size:9px; font-weight:800; color:#fff; }
.tp-actorchip .nm { font-family:var(--f-display); font-weight:700; font-size:12.5px; color:hsl(var(--c-player)); }
.tp-actorchip .x { width:18px; height:18px; border-radius:50%; border:none; background:hsl(var(--c-player) / .16); color:hsl(var(--c-player));
  cursor:pointer; font-size:10px; display:inline-flex; align-items:center; justify-content:center; flex-shrink:0; }
.tp-cfg { display:inline-flex; align-items:center; gap:7px; padding:8px 14px; border-radius:var(--r-md); background:var(--bg-card);
  border:1.5px solid var(--border); color:var(--text-sec); font-family:var(--f-display); font-weight:700; font-size:12.5px; cursor:pointer; white-space:nowrap; transition:background var(--dur-sm); }
.tp-cfg:hover { background:var(--bg-hover); }

/* ─ tabs nav (continuity S1+S2+S3) ─ */
.tp-tabs { display:flex; gap:4px; margin-top:14px; overflow-x:auto; scrollbar-width:none; }
.tp-tabs::-webkit-scrollbar { display:none; }
.tp-tab { display:inline-flex; align-items:center; gap:6px; padding:9px 14px 11px; border:none; background:transparent; cursor:pointer; white-space:nowrap;
  border-bottom:2px solid transparent; color:var(--text-muted); font-family:var(--f-display); font-weight:700; font-size:13px; transition:color var(--dur-sm); text-decoration:none; }
.tp-tab:hover { color:var(--text-sec); }
.tp-tab.on { color:hsl(var(--c-toolkit)); border-bottom-color:hsl(var(--c-toolkit)); }

/* ─ body: 2-col layout (tools 65 / log 35) ─ */
.tp-layout { flex:1; min-height:0; display:grid; grid-template-columns:1fr 360px; gap:0; overflow:hidden; }
.tp-toolcol { overflow:auto; min-height:0; padding:18px 22px 30px; display:flex; flex-direction:column; gap:24px; }
.tp-logcol { border-left:1px solid var(--border); background:var(--bg-sunken); display:flex; flex-direction:column; min-height:0; }

/* ─ tool section ─ */
.tp-section { display:flex; flex-direction:column; gap:13px; }
.tp-shead { display:flex; align-items:flex-end; gap:11px; }
.tp-shead-main { flex:1; min-width:0; display:flex; align-items:flex-end; gap:11px; }
.tp-stext { flex:1; min-width:0; }
.tp-sicon { width:30px; height:30px; flex-shrink:0; border-radius:var(--r-sm); display:inline-flex; align-items:center; justify-content:center; font-size:16px;
  background:hsl(var(--e) / .15); }
.tp-stitle { font-family:var(--f-display); font-weight:800; font-size:17px; letter-spacing:-.01em; color:var(--text); line-height:1.1; }
.tp-ssub { font-size:12px; color:var(--text-muted); margin-top:2px; }
.tp-shead .grow { flex:1; }
.tp-slink { background:none; border:1px solid transparent; cursor:pointer; font-family:var(--f-display); font-weight:700; font-size:12px; color:var(--text-muted); white-space:nowrap; padding:5px 9px; border-radius:var(--r-pill); transition:all var(--dur-sm); }
.tp-slink:hover { color:hsl(var(--e)); background:hsl(var(--e) / .08); }
.tp-slink.on { color:hsl(var(--e)); background:hsl(var(--e) / .12); border-color:hsl(var(--e) / .3); }
.tp-saction { display:inline-flex; align-items:center; gap:6px; padding:7px 12px; border-radius:var(--r-md); background:var(--bg-card); border:1.5px solid var(--border);
  color:var(--text-sec); font-family:var(--f-display); font-weight:700; font-size:12px; cursor:pointer; white-space:nowrap; }
.tp-saction:hover { background:var(--bg-hover); }

/* ─── DICE BUILDER ─────────────────────────────────── */
.tp-db { --e:var(--c-game); display:flex; flex-direction:column; gap:14px; padding:16px 18px 18px; background:var(--bg-card); border:1.5px solid var(--border-light); border-radius:var(--r-lg); box-shadow:var(--shadow-xs); }

/* presets */
.tp-db-presets { display:flex; align-items:center; gap:8px; flex-wrap:wrap; }
.tp-db-presets .lbl { font-family:var(--f-mono); font-size:10px; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); flex-shrink:0; }
.tp-preset { font-family:var(--f-mono); font-weight:700; font-size:12px; padding:5px 11px; border-radius:var(--r-pill); cursor:pointer; white-space:nowrap;
  background:hsl(var(--e) / .10); border:1.5px solid hsl(var(--e) / .30); color:hsl(var(--e)); transition:all var(--dur-sm); }
.tp-preset:hover { background:hsl(var(--e) / .18); border-color:hsl(var(--e) / .5); }
.tp-preset.pulse { animation:tp-preset-pulse .22s var(--ease-spring); }
.tp-preset.custom { background:var(--bg-card); border-style:dashed; border-color:var(--border-strong); color:var(--text-muted); }
.tp-preset.custom.on { color:hsl(var(--e)); border-color:hsl(var(--e) / .5); border-style:solid; }

/* builder grid */
.tp-db-grid { display:grid; grid-template-columns:repeat(3, 1fr); gap:16px; padding:14px; border-radius:var(--r-md); background:var(--bg-sunken); border:1px solid var(--border-light); }
.tp-db-col { display:flex; flex-direction:column; gap:9px; }
.tp-db-col > .lbl { font-family:var(--f-mono); font-size:10px; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); }
.tp-db-typechips { display:flex; flex-wrap:wrap; gap:6px; }
.tp-typechip { font-family:var(--f-mono); font-weight:700; font-size:12px; padding:6px 11px; border-radius:var(--r-sm); cursor:pointer;
  background:var(--bg-card); border:1.5px solid var(--border); color:var(--text); transition:all var(--dur-sm); }
.tp-typechip:hover { border-color:hsl(var(--e) / .4); }
.tp-typechip.on { background:hsl(var(--e)); border-color:hsl(var(--e)); color:#fff; }

/* stepper */
.tp-stepper { display:flex; align-items:center; justify-content:space-between; gap:8px; }
.tp-step-btn { width:36px; height:36px; flex-shrink:0; border-radius:50%; border:none; cursor:pointer; font-size:18px; font-weight:700; line-height:1;
  display:inline-flex; align-items:center; justify-content:center; transition:all var(--dur-sm); }
.tp-step-btn.g { background:hsl(var(--c-game) / .14); color:hsl(var(--c-game)); }
.tp-step-btn.t { background:hsl(var(--c-toolkit) / .14); color:hsl(var(--c-toolkit)); }
.tp-step-btn:hover:not(:disabled) { filter:brightness(1.05); transform:scale(1.06); }
.tp-step-btn:disabled { opacity:.32; cursor:not-allowed; }
.tp-step-val { font-family:var(--f-mono); font-weight:800; font-size:26px; color:var(--text); font-variant-numeric:tabular-nums; }
.tp-step-val.muted { color:var(--text-muted); }
.tp-db-col .hint { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); text-align:center; }

/* advanced toggles */
.tp-db-adv { display:flex; flex-direction:column; gap:9px; }
.tp-adv-head { display:inline-flex; align-items:center; gap:7px; cursor:pointer; background:none; border:none; padding:0; align-self:flex-start;
  font-family:var(--f-display); font-weight:700; font-size:12.5px; color:var(--text-sec); }
.tp-adv-head:hover { color:hsl(var(--c-game)); }
.tp-adv-head .chev { transition:transform var(--dur-sm); font-size:9px; }
.tp-adv-head.open .chev { transform:rotate(180deg); }
.tp-adv-grid { display:grid; grid-template-columns:repeat(2, 1fr); gap:8px; }
.tp-adv-toggle { display:flex; align-items:center; gap:9px; padding:9px 11px; border-radius:var(--r-md); border:1.5px solid var(--border-light); background:var(--bg-sunken); }
.tp-adv-toggle.on { border-color:hsl(var(--e) / .4); background:hsl(var(--e) / .07); }
.tp-adv-sw { width:34px; height:20px; border-radius:var(--r-pill); flex-shrink:0; background:var(--border-strong); position:relative; cursor:pointer; border:none; padding:0; transition:background var(--dur-sm); }
.tp-adv-sw::after { content:''; position:absolute; top:2px; left:2px; width:16px; height:16px; border-radius:50%; background:#fff; transition:transform var(--dur-sm); box-shadow:var(--shadow-xs); }
.tp-adv-toggle.on .tp-adv-sw { background:hsl(var(--e)); }
.tp-adv-toggle.on .tp-adv-sw::after { transform:translateX(14px); }
.tp-adv-toggle .tl { flex:1; min-width:0; font-family:var(--f-display); font-weight:700; font-size:11.5px; color:var(--text); display:inline-flex; align-items:center; gap:5px; }
.tp-adv-toggle input.num { width:40px; flex-shrink:0; font-family:var(--f-mono); font-size:12px; padding:4px 5px; border-radius:var(--r-xs); border:1px solid var(--border); background:var(--bg-card); color:var(--text); outline:none; text-align:center; }
.tp-adv-toggle input.num:disabled { opacity:.4; }

/* main CTA */
.tp-db-cta { display:inline-flex; align-items:center; justify-content:center; gap:8px; padding:12px 18px; border-radius:var(--r-md); border:none; cursor:pointer;
  background:hsl(var(--e)); color:#fff; font-family:var(--f-display); font-weight:800; font-size:15px; box-shadow:0 4px 14px hsl(var(--e) / .35); transition:all var(--dur-sm); }
.tp-db-cta:hover:not(:disabled) { filter:brightness(1.05); transform:translateY(-1px); }
.tp-db-cta:disabled { background:var(--bg-muted); color:var(--text-muted); cursor:not-allowed; box-shadow:none; }

/* formula mode */
.tp-fm-head { display:inline-flex; align-items:center; gap:7px; cursor:pointer; background:none; border:none; padding:0; align-self:flex-start;
  font-family:var(--f-display); font-weight:700; font-size:12.5px; color:var(--text-sec); }
.tp-fm-head:hover { color:hsl(var(--c-game)); }
.tp-fm-head .chev { transition:transform var(--dur-sm); font-size:9px; }
.tp-fm-head.open .chev { transform:rotate(180deg); }
.tp-db-formula { display:flex; flex-direction:column; gap:10px; padding:13px; border-radius:var(--r-md); background:var(--bg-sunken); border:1px solid var(--border-light); }
.tp-formula-row { display:flex; align-items:center; gap:9px; }
.tp-formula-row .fl { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); flex-shrink:0; }
.tp-formula-input { flex:1; min-width:0; font-family:var(--f-mono); font-size:14px; padding:10px 12px; border-radius:var(--r-md); border:1.5px solid var(--border); background:var(--bg-card); color:var(--text); outline:none; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.tp-formula-input:focus { border-color:hsl(var(--e) / .6); box-shadow:0 0 0 3px hsl(var(--e) / .14); }
.tp-formula-input.bad { border-color:hsl(var(--c-danger) / .55); }
.tp-formula-input.bad:focus { box-shadow:0 0 0 3px hsl(var(--c-danger) / .14); }
.tp-parse { display:flex; align-items:flex-start; gap:7px; padding:8px 11px; border-radius:var(--r-sm); font-size:12px; line-height:1.45; }
.tp-parse.ok { background:hsl(var(--c-success) / .09); color:hsl(var(--c-success)); }
.tp-parse.bad { background:hsl(var(--c-danger) / .09); color:hsl(var(--c-danger)); }
.tp-parse .ic { flex-shrink:0; }

/* syntax help */
.tp-syntax { padding:12px 14px; border-radius:var(--r-md); background:var(--bg-sunken); border:1px solid var(--border-light); display:flex; flex-direction:column; gap:7px; }
.tp-syntax .sh { font-family:var(--f-display); font-weight:800; font-size:12px; color:var(--text); }
.tp-syntax-row { display:grid; grid-template-columns:96px 1fr auto; gap:10px; align-items:baseline; font-size:11.5px; }
.tp-syntax-row code { font-family:var(--f-mono); font-weight:700; color:hsl(var(--e)); }
.tp-syntax-row .d { color:var(--text-sec); }
.tp-syntax-row .ex { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); white-space:nowrap; }

/* result */
.tp-db-result { display:flex; flex-direction:column; gap:11px; padding:15px 16px; border-radius:var(--r-md); border:1.5px solid var(--border-light); background:var(--bg-sunken); }
.tp-db-result.empty { align-items:center; justify-content:center; min-height:84px; color:var(--text-muted); font-family:var(--f-mono); font-size:12px; }
.tp-result-top { display:flex; align-items:baseline; gap:9px; flex-wrap:wrap; }
.tp-result-top .f { font-family:var(--f-mono); font-weight:700; font-size:13px; color:hsl(var(--e)); }
.tp-result-top .t { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.tp-dice-row { display:flex; flex-wrap:wrap; gap:8px; }
.tp-die-result { width:40px; height:44px; border-radius:var(--r-sm); display:flex; align-items:center; justify-content:center; position:relative;
  font-family:var(--f-mono); font-weight:800; font-size:18px; font-variant-numeric:tabular-nums; animation:tp-die-in .3s var(--ease-out) backwards; }
.tp-die-result.kept { background:hsl(var(--e) / .18); border:1.5px solid hsl(var(--e) / .42); color:hsl(var(--e)); }
.tp-die-result.dropped { background:var(--bg-muted); border:1.5px solid var(--border); color:var(--text-muted); text-decoration:line-through; text-decoration-thickness:1.5px; opacity:.65; }
.tp-die-result.crit.kept { box-shadow:0 0 0 2px hsl(var(--c-warning) / .45); }
.tp-die-result.fail.kept { background:hsl(var(--c-danger) / .12); border-color:hsl(var(--c-danger) / .42); color:hsl(var(--c-danger)); }
.tp-die-result .mk { position:absolute; top:-8px; right:-6px; font-size:11px; }
.tp-die-result.rolling { animation:tp-dice-roll .6s var(--ease-in-out) infinite; background:hsl(var(--e) / .14); border:1.5px solid hsl(var(--e) / .3); color:hsl(var(--e)); }
.tp-calc { font-family:var(--f-mono); font-size:12px; color:var(--text-sec); line-height:1.5; }
.tp-calc .mod { color:hsl(var(--c-toolkit)); font-weight:700; }
.tp-calc .eq { color:var(--text-muted); }
.tp-total-row { display:flex; align-items:baseline; gap:10px; }
.tp-total-row .tl { font-family:var(--f-mono); font-size:11px; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); }
.tp-total { font-family:var(--f-display); font-weight:800; font-size:40px; line-height:1; color:hsl(var(--e)); font-variant-numeric:tabular-nums; letter-spacing:-.02em; }
.tp-total.pop { animation:tp-dice-result-pop .4s var(--ease-spring); }
.tp-total .suf { font-family:var(--f-display); font-size:16px; font-weight:700; color:var(--text-sec); margin-left:5px; }

/* history */
.tp-db-history { display:flex; flex-direction:column; gap:0; border-top:1px solid var(--border-light); padding-top:4px; }
.tp-hist-head { display:flex; align-items:center; gap:8px; padding:8px 2px; }
.tp-hist-head .ht { font-family:var(--f-display); font-weight:800; font-size:13px; color:var(--text); }
.tp-hist-head .hc { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.tp-hist-head .grow { flex:1; }
.tp-hist-clear { background:none; border:none; cursor:pointer; color:var(--text-muted); font-size:13px; padding:5px; border-radius:var(--r-xs); }
.tp-hist-clear:hover { color:hsl(var(--c-danger)); background:hsl(var(--c-danger) / .1); }
.tp-hist-row { display:flex; align-items:center; gap:9px; padding:8px 10px; border-radius:var(--r-sm); cursor:pointer; }
.tp-hist-row:hover { background:var(--bg-muted); }
.tp-hist-f { font-family:var(--f-mono); font-weight:700; font-size:12px; color:hsl(var(--e)); flex-shrink:0; }
.tp-hist-res { font-family:var(--f-mono); font-size:12px; color:var(--text); }
.tp-hist-res b { font-weight:700; }
.tp-hist-detail { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.tp-hist-meta { margin-left:auto; display:flex; align-items:center; gap:8px; flex-shrink:0; }
.tp-hist-time { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.tp-hist-chip { display:inline-flex; align-items:center; gap:4px; padding:1px 7px 1px 2px; border-radius:var(--r-pill); background:hsl(var(--c-player) / .12); }
.tp-hist-chip .av { width:14px; height:14px; border-radius:50%; display:inline-flex; align-items:center; justify-content:center; font-family:var(--f-display); font-size:6px; font-weight:800; color:#fff; }
.tp-hist-chip .nm { font-family:var(--f-display); font-weight:700; font-size:9px; color:hsl(var(--c-player)); }
.tp-hist-reroll { width:26px; height:26px; border-radius:var(--r-sm); border:1px solid var(--border); background:var(--bg-card); color:hsl(var(--e)); cursor:pointer; font-size:12px; flex-shrink:0; display:inline-flex; align-items:center; justify-content:center; }
.tp-hist-reroll:hover { background:hsl(var(--e) / .12); border-color:hsl(var(--e) / .4); }
.tp-hist-empty { padding:16px; text-align:center; font-family:var(--f-mono); font-size:11px; color:var(--text-muted); }
.tp-hist-more { width:100%; padding:9px; border:none; background:var(--bg-muted); color:var(--text-sec); font-family:var(--f-display); font-weight:700; font-size:11.5px; cursor:pointer; border-radius:var(--r-sm); margin-top:4px; }
.tp-hist-more:hover { background:var(--bg-hover); }

/* ─── secondary controls accordion (single-open) ─── */
.tp-db-acc { display:flex; flex-direction:column; border-top:1px solid var(--border-light); }
.tp-acc { border-bottom:1px solid var(--border-light); }
.tp-acc:last-child { border-bottom:none; }
.tp-acc-head { display:flex; align-items:center; gap:9px; width:100%; padding:12px 4px; background:none; border:none; cursor:pointer;
  font-family:var(--f-display); font-weight:700; font-size:13px; color:var(--text-sec); text-align:left; transition:color var(--dur-sm); }
.tp-acc-head:hover { color:hsl(var(--c-game)); }
.tp-acc.open .tp-acc-head { color:var(--text); }
.tp-acc-head .ai { font-size:14px; flex-shrink:0; }
.tp-acc-head .ac { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); padding:1px 7px; border-radius:var(--r-pill); background:var(--bg-muted); flex-shrink:0; }
.tp-acc-head .grow { flex:1; }
.tp-acc-head .chev { font-size:9px; color:var(--text-muted); flex-shrink:0; transition:transform var(--dur-sm); }
.tp-acc.open .tp-acc-head .chev { transform:rotate(180deg); }
.tp-acc-body { padding:2px 2px 14px; }

/* ─── COUNTER GRID ────────────────────────────────── */
.tp-counter-grid { display:grid; grid-template-columns:repeat(2, 1fr); gap:12px; }
.tp-counter { --e:var(--c-toolkit); position:relative; display:flex; flex-direction:column; gap:12px; padding:16px 18px 18px; overflow:hidden;
  background:var(--bg-card); border:1.5px solid var(--border-light); border-radius:var(--r-lg); box-shadow:var(--shadow-xs); transition:border-color var(--dur-sm); }
.tp-counter:hover { border-color:hsl(var(--e) / .3); }
.tp-ctop { display:flex; align-items:center; gap:8px; }
.tp-cname { font-family:var(--f-display); font-weight:800; font-size:15px; color:var(--text); background:none; border:none; padding:2px 4px; margin:-2px -4px; border-radius:var(--r-xs);
  cursor:text; text-align:left; }
.tp-cname:hover { background:var(--bg-muted); }
.tp-cname-input { font-family:var(--f-display); font-weight:800; font-size:15px; color:var(--text); background:var(--bg-card); border:1.5px solid hsl(var(--e) / .5);
  border-radius:var(--r-sm); padding:2px 6px; outline:none; width:140px; }
.tp-creset { margin-left:auto; background:none; border:none; cursor:pointer; font-family:var(--f-mono); font-size:10px; color:var(--text-muted); display:inline-flex; align-items:center; gap:4px; }
.tp-creset:hover { color:hsl(var(--c-warning)); }
.tp-cbody { display:flex; align-items:center; justify-content:space-between; gap:12px; }
.tp-cbtn { width:48px; height:48px; flex-shrink:0; border-radius:50%; border:none; cursor:pointer; font-size:22px; font-weight:700; line-height:1;
  background:hsl(var(--e) / .14); color:hsl(var(--e)); display:inline-flex; align-items:center; justify-content:center; transition:all var(--dur-sm) var(--ease-out); }
.tp-cbtn:hover { background:hsl(var(--e) / .24); transform:scale(1.06); }
.tp-cbtn:active { transform:scale(.94); }
.tp-cbtn.hot { background:hsl(var(--e)); color:#fff; box-shadow:0 4px 12px hsl(var(--e) / .4); }
.tp-cval { font-family:var(--f-display); font-weight:800; font-size:46px; line-height:1; color:var(--text); font-variant-numeric:tabular-nums; letter-spacing:-.02em; }
.tp-cval.pop { animation:tp-counter-pop .42s var(--ease-spring); }
.tp-cval.up { color:hsl(var(--e)); }
.tp-chint { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); text-align:center; }
/* reset confirm overlay */
.tp-cconfirm { position:absolute; inset:0; z-index:3; background:hsl(var(--c-warning) / .14); backdrop-filter:blur(2px); display:flex; flex-direction:column;
  align-items:center; justify-content:center; gap:11px; padding:14px; }
.tp-cconfirm .q { font-family:var(--f-display); font-weight:800; font-size:14px; color:var(--text); }
.tp-cconfirm .row { display:flex; gap:9px; }
.tp-cconfirm button { padding:7px 15px; border-radius:var(--r-md); font-family:var(--f-display); font-weight:800; font-size:12.5px; cursor:pointer; border:1px solid var(--border-strong); background:var(--bg-card); color:var(--text); }
.tp-cconfirm button.warn { background:hsl(var(--c-warning)); border-color:transparent; color:#fff; }

/* ─── TIMER GRID ──────────────────────────────────── */
.tp-timer-grid { display:grid; grid-template-columns:repeat(2, 1fr); gap:12px; }
.tp-timer { --e:var(--c-warning); position:relative; display:flex; flex-direction:column; gap:14px; padding:16px 18px 18px; overflow:hidden;
  background:var(--bg-card); border:1.5px solid var(--border-light); border-radius:var(--r-lg); box-shadow:var(--shadow-xs); transition:border-color var(--dur-sm); }
.tp-timer .tlabel { display:flex; align-items:center; gap:8px; font-family:var(--f-display); font-weight:800; font-size:14px; color:var(--text); flex-wrap:wrap; }
.tp-timer .tlabel .tg { font-size:15px; flex-shrink:0; }
.tp-timer .tlabel .tnm { flex-shrink:1; min-width:0; }
.tp-tactor { padding:3px 9px 3px 3px !important; flex-shrink:0; }
.tp-tactor .nm { font-size:11px !important; }
.tp-timer .tstate { margin-left:auto; font-family:var(--f-mono); font-size:10px; padding:2px 8px; border-radius:var(--r-pill); background:var(--bg-muted); color:var(--text-muted); text-transform:uppercase; letter-spacing:.04em; font-weight:700; flex-shrink:0; }
.tp-timer.running .tstate { background:hsl(var(--c-success) / .15); color:hsl(var(--c-success)); }
.tp-timer.paused .tstate { background:hsl(var(--c-info) / .15); color:hsl(var(--c-info)); }
.tp-timer.expired .tstate, .tp-timer.finishing .tstate { background:hsl(var(--c-danger) / .15); color:hsl(var(--c-danger)); }
.tp-timer-display { display:flex; flex-direction:column; align-items:center; gap:10px; padding:8px 0; border-radius:var(--r-md); border:1.5px solid var(--border-light); }
.tp-timer.finishing .tp-timer-display { animation:tp-timer-alert 1s var(--ease-in-out) infinite; }
.tp-timer.expired .tp-timer-display { border-color:hsl(var(--c-danger) / .5); animation:tp-timer-flash .6s var(--ease-in-out) 3; }
.tp-clock { font-family:var(--f-mono); font-weight:800; font-size:42px; line-height:1; color:var(--text); font-variant-numeric:tabular-nums; letter-spacing:.01em; }
.tp-timer.finishing .tp-clock { color:hsl(var(--c-danger)); }
.tp-timer.expired .tp-clock { color:hsl(var(--c-danger)); }
.tp-progress { width:82%; height:7px; border-radius:var(--r-pill); background:var(--bg-muted); overflow:hidden; }
.tp-progress .fill { height:100%; border-radius:var(--r-pill); background:hsl(var(--e)); transition:width 1s linear; }
.tp-timer.finishing .tp-progress .fill, .tp-timer.expired .tp-progress .fill { background:hsl(var(--c-danger)); }
.tp-progress-lbl { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.tp-timer-foot { display:flex; align-items:center; gap:9px; }
.tp-tselect { display:inline-flex; align-items:center; gap:5px; }
.tp-tselect select { font-family:var(--f-mono); font-size:11px; padding:6px 8px; border-radius:var(--r-sm); border:1.5px solid var(--border); background:var(--bg-card); color:var(--text); cursor:pointer; outline:none; }
.tp-tbtn { display:inline-flex; align-items:center; justify-content:center; gap:6px; padding:9px 14px; border-radius:var(--r-md); border:none; cursor:pointer;
  font-family:var(--f-display); font-weight:800; font-size:13px; transition:all var(--dur-sm) var(--ease-out); }
.tp-tbtn.primary { flex:1; background:hsl(var(--e)); color:#fff; box-shadow:0 3px 10px hsl(var(--e) / .35); }
.tp-tbtn.primary:hover { filter:brightness(1.05); transform:translateY(-1px); }
.tp-tbtn.primary.danger { background:hsl(var(--c-danger)); box-shadow:0 3px 10px hsl(var(--c-danger) / .35); }
.tp-tbtn.ghost { background:var(--bg-muted); color:var(--text-sec); border:1px solid var(--border); }
.tp-tbtn.ghost:hover { background:var(--bg-hover); }
.tp-timer-actor { position:absolute; top:14px; right:16px; }

/* ─── RANDOMIZER ──────────────────────────────────── */
.tp-rand { --e:var(--c-event); display:grid; grid-template-columns:1fr auto; gap:18px; align-items:center; padding:18px 20px;
  background:var(--bg-card); border:1.5px solid var(--border-light); border-radius:var(--r-lg); box-shadow:var(--shadow-xs); }
.tp-rand-list { display:flex; flex-direction:column; gap:6px; min-width:0; }
.tp-rand-list .lh { font-family:var(--f-mono); font-size:10px; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); margin-bottom:2px; }
.tp-rand-item { display:flex; align-items:center; gap:9px; padding:8px 11px; border-radius:var(--r-md); background:var(--bg-muted); border:1px solid var(--border-light); }
.tp-rand-item.cycling { animation:tp-randomizer-cycle .16s steps(2) infinite; border-color:hsl(var(--e) / .4); }
.tp-rand-item.winner { background:hsl(var(--e) / .16); border-color:hsl(var(--e) / .5); }
.tp-rand-item .dot { width:7px; height:7px; border-radius:50%; background:hsl(var(--e)); flex-shrink:0; }
.tp-rand-item .txt { flex:1; font-family:var(--f-display); font-weight:700; font-size:13px; color:var(--text); min-width:0; }
.tp-rand-item .rm { width:20px; height:20px; border-radius:50%; border:none; background:transparent; color:var(--text-muted); cursor:pointer; font-size:12px; flex-shrink:0; }
.tp-rand-item .rm:hover { background:hsl(var(--c-danger) / .12); color:hsl(var(--c-danger)); }
.tp-rand-add { display:inline-flex; align-items:center; gap:7px; padding:8px 11px; border-radius:var(--r-md); border:1.5px dashed var(--border-strong); background:transparent;
  color:var(--text-muted); font-family:var(--f-display); font-weight:700; font-size:12.5px; cursor:pointer; }
.tp-rand-add:hover { border-color:hsl(var(--e) / .5); color:hsl(var(--e)); }
.tp-rand-add input { flex:1; border:none; background:transparent; outline:none; font-family:var(--f-body); font-size:13px; color:var(--text); min-width:0; }
.tp-rand-right { display:flex; flex-direction:column; align-items:center; gap:12px; width:208px; flex-shrink:0; }
.tp-rand-result { width:100%; text-align:center; padding:14px 12px; border-radius:var(--r-md); background:var(--bg-sunken); border:1px solid var(--border-light); min-height:62px;
  display:flex; flex-direction:column; align-items:center; justify-content:center; gap:3px; }
.tp-rand-result.has { background:hsl(var(--e) / .12); border-color:hsl(var(--e) / .3); }
.tp-rand-result .big { font-family:var(--f-display); font-weight:800; font-size:22px; color:hsl(var(--e)); line-height:1.1; }
.tp-rand-result .big.pop { animation:tp-counter-pop .5s var(--ease-spring); }
.tp-rand-result .meta { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.tp-rand-result .ph { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); }
.tp-rand-btn { width:100%; display:inline-flex; align-items:center; justify-content:center; gap:8px; padding:13px; border-radius:var(--r-md); border:none; cursor:pointer;
  background:hsl(var(--e)); color:#fff; font-family:var(--f-display); font-weight:800; font-size:15px; box-shadow:0 4px 14px hsl(var(--e) / .35); transition:all var(--dur-sm) var(--ease-out); }
.tp-rand-btn:hover { filter:brightness(1.05); transform:translateY(-1px); }
.tp-rand-btn.disabled { background:var(--bg-muted); color:var(--text-muted); cursor:not-allowed; box-shadow:none; }
.tp-rand-btn.disabled:hover { transform:none; filter:none; }

/* ─── LOG PANEL ───────────────────────────────────── */
.tp-loghead { flex-shrink:0; padding:15px 16px 11px; border-bottom:1px solid var(--border); display:flex; flex-direction:column; gap:11px; }
.tp-logtop { display:flex; align-items:center; gap:9px; }
.tp-logtop .li { width:28px; height:28px; flex-shrink:0; border-radius:var(--r-sm); background:hsl(var(--c-toolkit) / .15); color:hsl(var(--c-toolkit)); display:inline-flex; align-items:center; justify-content:center; font-size:14px; }
.tp-logtop .lt { font-family:var(--f-display); font-weight:800; font-size:15px; color:var(--text); }
.tp-logtop .lc { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); margin-top:1px; }
.tp-logtop .grow { flex:1; }
.tp-logclear { display:inline-flex; align-items:center; gap:5px; padding:6px 10px; border-radius:var(--r-md); border:1px solid var(--border); background:var(--bg-card);
  color:var(--text-sec); font-family:var(--f-display); font-weight:700; font-size:11px; cursor:pointer; }
.tp-logclear:hover { border-color:hsl(var(--c-danger) / .4); color:hsl(var(--c-danger)); }
.tp-logclear:disabled { opacity:.4; cursor:not-allowed; }
.tp-logfilters { display:flex; gap:5px; flex-wrap:wrap; row-gap:6px; }
.tp-lfchip { display:inline-flex; align-items:center; gap:5px; padding:5px 10px; border-radius:var(--r-pill); white-space:nowrap; flex-shrink:0;
  background:var(--bg-card); border:1.5px solid var(--border); color:var(--text-sec); cursor:pointer; font-family:var(--f-display); font-weight:700; font-size:11px; transition:all var(--dur-sm); }
.tp-lfchip:hover { background:var(--bg-hover); }
.tp-lfchip.on { background:hsl(var(--c-toolkit) / .14); border-color:hsl(var(--c-toolkit) / .4); color:hsl(var(--c-toolkit)); }
.tp-logsort { display:flex; align-items:center; justify-content:space-between; }
.tp-logsort .cnt { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.tp-sortbtn { background:none; border:none; cursor:pointer; font-family:var(--f-display); font-weight:700; font-size:11px; color:var(--text-sec); display:inline-flex; align-items:center; gap:5px; }
.tp-sortbtn:hover { color:hsl(var(--c-toolkit)); }

.tp-logbody { flex:1; overflow-y:auto; min-height:0; }
.tp-logentry { display:flex; gap:11px; padding:11px 16px; border-bottom:1px solid var(--border-light); }
.tp-logentry.fresh { animation:tp-log-slide-in .3s var(--ease-out); }
.tp-logentry .lic { width:30px; height:30px; flex-shrink:0; border-radius:var(--r-sm); display:inline-flex; align-items:center; justify-content:center; font-size:15px;
  background:hsl(var(--e) / .15); }
.tp-logentry .lbody { flex:1; min-width:0; }
.tp-logentry .lres { font-family:var(--f-body); font-weight:700; font-size:13px; color:var(--text); line-height:1.3; }
.tp-logentry .lmeta { display:flex; align-items:center; gap:7px; margin-top:4px; }
.tp-logentry .ltime { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.tp-logentry .lchip { display:inline-flex; align-items:center; gap:5px; padding:1px 7px 1px 2px; border-radius:var(--r-pill); background:hsl(var(--c-player) / .12); }
.tp-logentry .lchip .av { width:15px; height:15px; border-radius:50%; display:inline-flex; align-items:center; justify-content:center; font-family:var(--f-display); font-size:7px; font-weight:800; color:#fff; }
.tp-logentry .lchip .nm { font-family:var(--f-display); font-weight:700; font-size:10px; color:hsl(var(--c-player)); }
.tp-logmore { width:100%; padding:11px; border:none; background:var(--bg-muted); color:var(--text-sec); font-family:var(--f-display); font-weight:700; font-size:12px; cursor:pointer; }
.tp-logmore:hover { background:var(--bg-hover); }
.tp-logempty { flex:1; display:flex; flex-direction:column; align-items:center; justify-content:center; gap:13px; padding:36px 26px; text-align:center; }
.tp-logempty .em { width:58px; height:58px; border-radius:50%; background:var(--bg-muted); display:inline-flex; align-items:center; justify-content:center; font-size:26px; opacity:.7; }
.tp-logempty p { font-size:13px; color:var(--text-muted); line-height:1.5; max-width:240px; margin:0; }

/* ─── CLEAR-LOG MODAL ─────────────────────────────── */
.tp-overlay { position:absolute; inset:0; z-index:50; background:rgba(20,12,4,.46); backdrop-filter:blur(3px); display:flex; align-items:center; justify-content:center; padding:28px; animation:tp-overlay-in var(--dur-md) var(--ease-out); }
[data-theme="dark"] .tp-overlay { background:rgba(0,0,0,.62); }
.tp-modal { width:min(420px, 100%); background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-xl); box-shadow:var(--shadow-lg);
  overflow:hidden; animation:tp-modal-in var(--dur-md) var(--ease-spring); }
.tp-mhead { display:flex; align-items:center; gap:12px; padding:20px 20px 0; }
.tp-mhead .mi { width:40px; height:40px; flex-shrink:0; border-radius:var(--r-md); background:hsl(var(--c-warning) / .16); color:hsl(var(--c-warning)); display:inline-flex; align-items:center; justify-content:center; font-size:19px; }
.tp-mhead .mt { font-family:var(--f-display); font-weight:800; font-size:18px; color:var(--text); }
.tp-mbody { padding:12px 20px 0; }
.tp-mbody p { font-size:13.5px; color:var(--text-sec); line-height:1.55; margin:0; }
.tp-mbody .cnt { font-family:var(--f-mono); font-weight:700; color:var(--text); }
.tp-mfoot { display:flex; gap:10px; padding:18px 20px 20px; }
.tp-mbtn { flex:1; display:inline-flex; align-items:center; justify-content:center; gap:6px; padding:11px; border-radius:var(--r-md); font-family:var(--f-display); font-weight:800; font-size:13px; cursor:pointer; border:1px solid var(--border-strong); background:var(--bg-card); color:var(--text); transition:all var(--dur-sm); }
.tp-mbtn:hover { background:var(--bg-muted); }
.tp-mbtn.warn { background:hsl(var(--c-warning)); border-color:transparent; color:#fff; box-shadow:0 4px 14px hsl(var(--c-warning) / .3); }
.tp-mbtn.warn:hover { filter:brightness(1.05); background:hsl(var(--c-warning)); }

/* ═══ MOBILE ═══ */
.tp-app.is-mobile .tp-head { padding:12px 14px 0; }
.tp-app.is-mobile .tp-h1 { font-size:19px; }
.tp-app.is-mobile .tp-htop { flex-direction:column; gap:11px; }
.tp-app.is-mobile .tp-hright { flex-direction:row; align-items:center; align-self:stretch; flex-wrap:wrap; gap:8px; }
.tp-app.is-mobile .tp-actor input { width:140px; }
.tp-app.is-mobile .tp-layout { display:block; overflow-y:auto; -webkit-overflow-scrolling:touch; }
.tp-app.is-mobile .tp-toolcol { padding:14px; gap:20px; overflow:visible; height:auto; }
.tp-app.is-mobile .tp-shead { flex-wrap:wrap; row-gap:10px; align-items:center; }
.tp-app.is-mobile .tp-shead-main { flex:1 1 100%; }
.tp-app.is-mobile .tp-db-grid { grid-template-columns:1fr; gap:14px; }
.tp-app.is-mobile .tp-adv-grid { grid-template-columns:1fr; }
.tp-app.is-mobile .tp-db-presets { flex-wrap:wrap; row-gap:7px; }
.tp-app.is-mobile .tp-syntax-row { grid-template-columns:80px 1fr; }
.tp-app.is-mobile .tp-syntax-row .ex { display:none; }
.tp-app.is-mobile .tp-counter-grid, .tp-app.is-mobile .tp-timer-grid { grid-template-columns:1fr; }
.tp-app.is-mobile .tp-rand { grid-template-columns:1fr; }
.tp-app.is-mobile .tp-rand-right { width:100%; }
/* log = accordion bottom */
.tp-app.is-mobile .tp-logcol { border-left:none; border-top:1px solid var(--border); }
.tp-logacc-btn { display:none; }
.tp-app.is-mobile .tp-logacc-btn { display:flex; align-items:center; gap:9px; width:100%; padding:13px 16px; border:none; background:var(--bg-sunken); cursor:pointer; white-space:nowrap;
  font-family:var(--f-display); font-weight:800; font-size:14px; color:var(--text); }
.tp-app.is-mobile .tp-logacc-btn .li { width:26px; height:26px; border-radius:var(--r-sm); background:hsl(var(--c-toolkit) / .15); color:hsl(var(--c-toolkit)); display:inline-flex; align-items:center; justify-content:center; font-size:13px; }
.tp-app.is-mobile .tp-logacc-btn .badge { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); }
.tp-app.is-mobile .tp-logacc-btn .grow { flex:1; }
.tp-app.is-mobile .tp-logacc-btn .chev { transition:transform var(--dur-sm); }
.tp-app.is-mobile .tp-logacc-btn.open .chev { transform:rotate(180deg); }
.tp-app.is-mobile .tp-logcol .tp-loghead, .tp-app.is-mobile .tp-logcol .tp-logbody, .tp-app.is-mobile .tp-logcol .tp-logempty { max-height:0; overflow:hidden; padding-top:0; padding-bottom:0; transition:none; }
.tp-app.is-mobile .tp-logbody { flex:none; }
.tp-app.is-mobile .tp-logcol.open .tp-loghead { max-height:none; padding:15px 16px 11px; }
.tp-app.is-mobile .tp-logcol.open .tp-logbody { max-height:none; overflow:visible; }
.tp-app.is-mobile .tp-logcol.open .tp-logempty { padding:30px 24px; }
.tp-app.is-mobile .tp-logtop > .li, .tp-app.is-mobile .tp-logtop > div:not(.grow) { display:none; }
.tp-app.is-mobile .tp-overlay { padding:0; align-items:flex-end; }
.tp-app.is-mobile .tp-modal { width:100%; border-radius:var(--r-2xl) var(--r-2xl) 0 0; animation:tp-sheet-in var(--dur-lg) var(--ease-spring); }
`;

window.__TP_CSS = TP_CSS;
