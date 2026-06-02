/* sp4-editor-proposals-index.jsx
   Route: /editor/agent-proposals — Typology proposals list (filtri + status badges)
   B14 (issue #1489) · screen 2 of 5 · Tier S
   Pattern: Hero + body con tabella filtrabile (list view come sp4-library-desktop) +
            status badges (pattern sp4-toolkit-detail). Desktop = table, mobile = cards stack.
   Continuity con sp4-editor-index (S1): stesso state-picker UI, theme toggle 🌗 (mai-theme),
   desktop frame chrome (.ed-desk) e phone-row pattern. Entity primaria = --c-agent (typology).
   Loadable standalone via Babel. Injects own component CSS; relies on tokens.css + components.css.

   v2 components surfaced here (annotate at implementation time):
   /* v2: ProposalsList, ProposalsTable, ProposalsTableRow, ProposalsCards, ProposalsFilters,
          ProposalsSearchBox, StatusFilterChips, StatusBadge, CapabilityStrip, GameScopePip,
          AuthorChip, RejectionReasonAlert, ProposalActions, ProposalsEmpty, ProposalsSkeleton */

const { useState, useEffect, useMemo, useRef } = React;

/* ──────────────────────────────────────────────────────────
   Component CSS — solo token da tokens.css / components.css.
   .ed-* = harness chrome riusato da S1 (continuity). .pr-* = proposals.
   ────────────────────────────────────────────────────────── */
const PROP_CSS = `
/* ─── harness (riuso esatto da sp4-editor-index S1) ─── */
.ed-stage { min-height:100vh; padding:72px 24px 96px; background:var(--bg); color:var(--text); }
.ed-wrap { max-width:1380px; margin:0 auto; }
.ed-kicker { font-family:var(--f-mono); font-size:var(--fs-xs); letter-spacing:.1em; text-transform:uppercase; color:var(--text-muted); }
.ed-stage h1 { font-size:var(--fs-3xl); margin:8px 0 6px; }
.ed-stage h1 .acc { color:hsl(var(--c-agent)); }
.ed-lead { color:var(--text-sec); font-size:var(--fs-md); max-width:820px; line-height:var(--lh-body); }
.ed-notes { display:grid; grid-template-columns:repeat(3,1fr); gap:12px; margin:22px 0 4px; }
.ed-note { background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-lg); padding:14px 16px; }
.ed-note h4 { font-family:var(--f-display); font-size:var(--fs-sm); text-transform:uppercase; letter-spacing:.04em; color:hsl(var(--c-agent)); margin-bottom:6px; }
.ed-note p { font-size:var(--fs-sm); color:var(--text-sec); line-height:var(--lh-snug); }
.ed-note p b { color:var(--text); font-weight:var(--fw-bold); }
.ed-note code { background:var(--bg-muted); padding:1px 5px; border-radius:var(--r-xs); font-size:11px; }
.ed-rail { position:sticky; top:0; z-index:var(--z-sticky); margin:26px 0 18px; padding:12px 0;
  background:var(--bg); display:flex; align-items:flex-start; gap:14px; flex-wrap:wrap; border-bottom:1px solid var(--border); }
.ed-rail .lab { font-family:var(--f-mono); font-size:var(--fs-xs); text-transform:uppercase; letter-spacing:.08em; color:var(--text-muted); padding-top:8px; }
.ed-states { display:flex; gap:6px; flex-wrap:wrap; flex:1; }
.ed-sbtn { display:inline-flex; align-items:center; gap:7px; padding:7px 12px; border-radius:var(--r-pill);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm);
  background:var(--bg-card); border:1.5px solid var(--border); color:var(--text-sec); cursor:pointer; transition:all var(--dur-sm) var(--ease-out); }
.ed-sbtn:hover { transform:translateY(-1px); border-color:var(--border-strong); }
.ed-sbtn .pip { width:7px; height:7px; border-radius:50%; background:currentColor; opacity:.6; }
.ed-sbtn.on { background:hsl(var(--c-agent)); border-color:transparent; color:#fff; }
.ed-sbtn.on .pip { opacity:1; background:#fff; }
.ed-vp-label { font-family:var(--f-mono); font-size:var(--fs-xs); text-transform:uppercase; letter-spacing:.08em;
  color:var(--text-muted); margin:30px 0 12px; display:flex; align-items:center; gap:10px; }
.ed-vp-label::after { content:''; flex:1; height:1px; background:var(--border); }
.ed-desk { width:100%; max-width:1340px; height:792px; border-radius:var(--r-lg); overflow:hidden;
  background:var(--bg-card); border:1px solid var(--border); box-shadow:var(--shadow-lg); display:flex; flex-direction:column; }
.ed-chrome { height:38px; flex-shrink:0; display:flex; align-items:center; gap:8px; padding:0 14px;
  background:var(--bg-muted); border-bottom:1px solid var(--border); font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); }
.ed-chrome .dots { display:flex; gap:6px; }
.ed-chrome .dots i { width:11px; height:11px; border-radius:50%; display:block; }
.ed-chrome .dots i:nth-child(1){ background:#ff5f57; } .ed-chrome .dots i:nth-child(2){ background:#febc2e; } .ed-chrome .dots i:nth-child(3){ background:#28c840; }
.ed-chrome .url { flex:1; text-align:center; background:var(--bg-card); border-radius:var(--r-sm); padding:4px 10px; margin:0 14%; }
.ed-phone-row { display:flex; gap:28px; align-items:flex-start; flex-wrap:wrap; }
.ed-phone-cap { font-size:var(--fs-sm); color:var(--text-sec); max-width:300px; line-height:var(--lh-snug); }
.ed-phone-cap h4 { font-family:var(--f-display); font-size:var(--fs-base); margin-bottom:6px; }

/* ─── proposals app shell ─── */
.pr-app { display:flex; flex-direction:column; height:100%; min-height:0; background:var(--bg); color:var(--text); position:relative; overflow:hidden; }
.pr-app :focus-visible { outline:2px solid hsl(var(--c-agent)); outline-offset:2px; border-radius:var(--r-xs); }

/* error banner (top) */
.pr-errbar { flex-shrink:0; display:flex; align-items:center; gap:11px; padding:11px 18px; font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm);
  background:hsl(var(--c-danger) / .14); color:hsl(var(--c-danger)); border-bottom:1px solid hsl(var(--c-danger) / .3); }
.pr-errbar .grow { flex:1; }
.pr-errbar .retry { display:inline-flex; align-items:center; gap:6px; padding:5px 12px; border-radius:var(--r-md); border:1px solid hsl(var(--c-danger) / .4);
  background:var(--bg-card); color:hsl(var(--c-danger)); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); cursor:pointer; }

/* header (sticky) */
.pr-head { flex-shrink:0; position:sticky; top:0; z-index:var(--z-sticky); background:var(--bg-card); border-bottom:1px solid var(--border);
  display:flex; align-items:flex-end; gap:16px; padding:14px 22px 16px; }
.pr-head .htxt { min-width:0; }
.pr-bread { font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); letter-spacing:.04em; display:flex; align-items:center; gap:6px; margin-bottom:6px; }
.pr-bread .sep { opacity:.5; }
.pr-bread .cur { color:hsl(var(--c-agent)); }
.pr-h1 { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-2xl); letter-spacing:-.01em; line-height:var(--lh-tight); }
.pr-sub { font-size:var(--fs-base); color:var(--text-sec); margin-top:4px; }
.pr-head .grow { flex:1; }
.pr-newbtn { flex-shrink:0; display:inline-flex; align-items:center; gap:7px; padding:9px 16px; border-radius:var(--r-md); border:none;
  background:hsl(var(--c-agent)); color:#3a2400; font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm);
  box-shadow:var(--shadow-xs); cursor:pointer; transition:all var(--dur-sm) var(--ease-out); white-space:nowrap; }
.pr-newbtn:hover { transform:translateY(-1px); box-shadow:var(--shadow-sm); filter:brightness(1.03); }
.pr-newbtn .ic { font-size:14px; line-height:1; }

/* toolbar */
.pr-toolbar { flex-shrink:0; display:flex; align-items:center; gap:12px; padding:11px 22px; background:var(--bg); border-bottom:1px solid var(--border); }
.pr-search { flex:0 0 38%; max-width:38%; position:relative; }
.pr-search .ic { position:absolute; left:11px; top:50%; transform:translateY(-50%); font-size:13px; opacity:.6; pointer-events:none; }
.pr-search input { width:100%; padding:8px 64px 8px 32px; border-radius:var(--r-md); border:1.5px solid var(--border);
  background:var(--bg-card); font-family:var(--f-body); font-size:var(--fs-sm); color:var(--text); outline:none; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.pr-search input:focus, .pr-search.active input { border-color:hsl(var(--c-agent) / .55); box-shadow:0 0 0 3px hsl(var(--c-agent) / .14); }
.pr-search .clear { position:absolute; right:9px; top:50%; transform:translateY(-50%); width:20px; height:20px; border-radius:var(--r-pill);
  border:none; background:var(--bg-muted); color:var(--text-sec); cursor:pointer; font-size:12px; display:inline-flex; align-items:center; justify-content:center; }
.pr-search .clear:hover { background:var(--border-strong); color:var(--text); }
.pr-search .busy { position:absolute; right:34px; top:50%; transform:translateY(-50%); display:inline-flex; align-items:center; gap:5px;
  font-family:var(--f-mono); font-size:10px; color:hsl(var(--c-info)); white-space:nowrap; }
.pr-search .busy i { width:6px; height:6px; border-radius:50%; background:currentColor; animation:prpulse 1s var(--ease-in-out) infinite; }
@keyframes prpulse { 0%,100%{ opacity:1; transform:scale(1);} 50%{ opacity:.3; transform:scale(.6);} }

.pr-chips { flex:1; display:flex; align-items:center; gap:7px; overflow-x:auto; scrollbar-width:none; padding:2px; }
.pr-chips::-webkit-scrollbar { display:none; }
.pr-chip { display:inline-flex; align-items:center; gap:7px; padding:6px 12px; border-radius:var(--r-pill); white-space:nowrap;
  background:var(--bg-card); border:1.5px solid var(--border); color:var(--text-sec); cursor:pointer; transition:all var(--dur-sm) var(--ease-out);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); }
.pr-chip:hover { background:var(--bg-hover); }
.pr-chip .cdot { width:8px; height:8px; border-radius:50%; flex-shrink:0; }
.pr-chip .ccount { font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); }
.pr-chip.all .cdot { background:var(--text-muted); }
.pr-chip[data-st="Draft"] .cdot { background:var(--text-muted); }
.pr-chip[data-st="PendingReview"] .cdot { background:hsl(var(--c-warning)); }
.pr-chip[data-st="Approved"] .cdot { background:hsl(var(--c-toolkit)); }
.pr-chip[data-st="Rejected"] .cdot { background:hsl(var(--c-danger)); }
.pr-chip.on { color:var(--text); background:var(--bg-muted); border-color:var(--border-strong); }
.pr-chip.on .ccount { color:var(--text-sec); }
.pr-chip[data-st="PendingReview"].on { background:hsl(var(--c-warning) / .14); border-color:hsl(var(--c-warning) / .4); color:hsl(var(--c-warning)); }
.pr-chip[data-st="Approved"].on { background:hsl(var(--c-toolkit) / .14); border-color:hsl(var(--c-toolkit) / .4); color:hsl(var(--c-toolkit)); }
.pr-chip[data-st="Rejected"].on { background:hsl(var(--c-danger) / .14); border-color:hsl(var(--c-danger) / .4); color:hsl(var(--c-danger)); }
.pr-chip[data-st="PendingReview"].on .ccount, .pr-chip[data-st="Approved"].on .ccount, .pr-chip[data-st="Rejected"].on .ccount { color:currentColor; opacity:.8; }

.pr-vtoggle { flex-shrink:0; display:inline-flex; padding:3px; gap:2px; background:var(--bg-muted); border-radius:var(--r-md); border:1px solid var(--border); }
.pr-vtoggle button { display:inline-flex; align-items:center; gap:5px; padding:5px 10px; border-radius:var(--r-sm); border:none; background:transparent; color:var(--text-muted);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); cursor:pointer; }
.pr-vtoggle button[aria-pressed="true"] { background:var(--bg-card); color:hsl(var(--c-agent)); box-shadow:var(--shadow-xs); }

/* table */
.pr-body { flex:1; overflow:auto; min-height:0; }
.pr-table { --cols: minmax(150px,1.35fr) minmax(190px,2fr) 152px 158px 102px 116px 146px; min-width:1060px; }
.pr-thead { position:sticky; top:0; z-index:2; display:grid; grid-template-columns:var(--cols); gap:14px; align-items:center;
  padding:10px 22px; background:var(--bg-sunken); border-bottom:1px solid var(--border); }
.pr-th { font-family:var(--f-mono); font-size:var(--fs-xs); text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); display:flex; align-items:center; gap:5px; }
.pr-th.sortable { cursor:pointer; }
.pr-th.sortable:hover { color:var(--text-sec); }
.pr-th .arr { font-size:9px; opacity:0; transition:opacity var(--dur-sm); }
.pr-th[aria-sort="ascending"] .arr, .pr-th[aria-sort="descending"] .arr { opacity:1; color:hsl(var(--c-agent)); }
.pr-th.right { justify-content:flex-end; }

.pr-row { border-bottom:1px solid var(--border-light); cursor:pointer; transition:background var(--dur-sm) var(--ease-out); }
.pr-row:hover { background:var(--bg-hover); }
.pr-row.rejected:hover { background:hsl(var(--c-danger) / .04); }
.pr-rowmain { display:grid; grid-template-columns:var(--cols); gap:14px; align-items:center; padding:14px 22px; }
.pr-cell { min-width:0; }
.pr-name { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); color:var(--text); line-height:var(--lh-snug); }
.pr-name .av-row { display:flex; align-items:center; gap:7px; margin-top:6px; }
.pr-desc { font-size:var(--fs-sm); color:var(--text-sec); line-height:var(--lh-snug); display:-webkit-box; -webkit-line-clamp:2; -webkit-box-orient:vertical; overflow:hidden; }
.pr-upd { font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); }

/* author EntityChip (player) */
.pr-author { display:inline-flex; align-items:center; gap:5px; padding:2px 9px 2px 2px; border-radius:var(--r-pill);
  background:hsl(var(--c-player) / .14); color:hsl(var(--c-player)); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); cursor:pointer; }
.pr-author .av { width:17px; height:17px; border-radius:50%; background:hsl(var(--c-player)); color:#fff; display:flex; align-items:center; justify-content:center; font-size:9px; font-weight:var(--fw-ext); }

/* status badge (pattern toolkit-detail) */
.pr-status { display:flex; flex-direction:column; gap:5px; align-items:flex-start; }
.pr-badge { display:inline-flex; align-items:center; gap:5px; padding:3px 9px; border-radius:var(--r-pill);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); border:1px solid transparent; white-space:nowrap; }
.pr-badge .bi { font-size:12px; line-height:1; }
.pr-badge.draft    { background:var(--bg-muted); border-color:var(--border); color:var(--text-muted); }
.pr-badge.review   { background:hsl(var(--c-warning) / .14); border-color:hsl(var(--c-warning) / .35); color:hsl(var(--c-warning)); }
.pr-badge.approved { background:hsl(var(--c-toolkit) / .14); border-color:hsl(var(--c-toolkit) / .35); color:hsl(var(--c-toolkit)); }
.pr-badge.rejected { background:hsl(var(--c-danger) / .14); border-color:hsl(var(--c-danger) / .35); color:hsl(var(--c-danger)); }
.pr-since { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.pr-live { display:inline-flex; align-items:center; gap:5px; font-family:var(--f-mono); font-size:10px; font-weight:var(--fw-bold); color:hsl(var(--c-success)); }
.pr-live i { width:7px; height:7px; border-radius:50%; background:hsl(var(--c-success)); animation:prlive 2s var(--ease-in-out) infinite; }
@keyframes prlive { 0%,100%{ opacity:1; box-shadow:0 0 0 0 hsl(var(--c-success) / .5);} 50%{ opacity:.6; box-shadow:0 0 0 4px hsl(var(--c-success) / 0);} }

/* capability strip (internal taxonomy, agent-tinted) */
.pr-caps { display:flex; flex-wrap:wrap; gap:5px; }
.pr-cap { padding:2px 7px; border-radius:var(--r-sm); background:hsl(var(--c-agent) / .12); color:hsl(var(--c-agent));
  font-family:var(--f-mono); font-size:10px; font-weight:var(--fw-bold); white-space:nowrap; }
.pr-cap.more { background:var(--bg-muted); color:var(--text-muted); }

/* game scope EntityPip (--c-game) + tooltip */
.pr-scope { position:relative; display:inline-flex; align-items:center; gap:6px; padding:3px 10px 3px 3px; border-radius:var(--r-pill);
  background:hsl(var(--c-game) / .12); color:hsl(var(--c-game)); border:1px solid hsl(var(--c-game) / .22);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); cursor:default; }
.pr-scope .pin { width:18px; height:18px; border-radius:50%; background:hsl(var(--c-game)); color:#fff; display:flex; align-items:center; justify-content:center; font-size:10px; }
.pr-scope .tip { position:absolute; bottom:calc(100% + 8px); left:50%; transform:translateX(-50%) translateY(4px); opacity:0; pointer-events:none;
  background:var(--text); color:var(--bg-card); padding:7px 11px; border-radius:var(--r-sm); box-shadow:var(--shadow-lg); white-space:nowrap;
  font-family:var(--f-body); font-weight:var(--fw-semi); font-size:var(--fs-xs); transition:opacity var(--dur-sm), transform var(--dur-sm); z-index:var(--z-tooltip); }
.pr-scope .tip::after { content:''; position:absolute; top:100%; left:50%; transform:translateX(-50%); border:5px solid transparent; border-top-color:var(--text); }
.pr-scope:hover .tip, .pr-scope:focus-within .tip { opacity:1; transform:translateX(-50%) translateY(0); }

/* actions */
.pr-acts { display:flex; align-items:center; gap:4px; justify-content:flex-end; }
.pr-act { width:30px; height:30px; border-radius:var(--r-sm); border:1px solid var(--border); background:var(--bg-card); color:var(--text-sec);
  display:inline-flex; align-items:center; justify-content:center; cursor:pointer; font-size:13px; transition:all var(--dur-sm) var(--ease-out); }
.pr-act:hover { background:var(--bg-muted); color:var(--text); border-color:var(--border-strong); }
.pr-act.submit:hover { color:hsl(var(--c-agent)); border-color:hsl(var(--c-agent) / .4); background:hsl(var(--c-agent) / .1); }

/* rejection reason inline alert */
.pr-reject { display:flex; align-items:flex-start; gap:8px; margin:0 22px 14px; padding:8px 14px; border-radius:var(--r-sm);
  background:hsl(var(--c-danger) / .08); border-left:3px solid hsl(var(--c-danger)); }
.pr-reject .ri { font-size:13px; line-height:1.4; flex-shrink:0; }
.pr-reject .rt { font-size:var(--fs-sm); color:var(--text-sec); line-height:var(--lh-snug); }
.pr-reject .rt b { color:hsl(var(--c-danger)); font-weight:var(--fw-bold); }
.pr-reject .rt a { color:hsl(var(--c-danger)); text-decoration:underline; font-weight:var(--fw-bold); cursor:pointer; white-space:nowrap; }

/* empty / filter-empty / skeleton */
.pr-pad { flex:1; display:flex; align-items:center; justify-content:center; padding:40px 24px; }
.pr-empty { text-align:center; max-width:380px; border:1.5px dashed var(--border-strong); border-radius:var(--r-xl); padding:48px 32px; }
.pr-empty .em { font-size:40px; opacity:.7; }
.pr-empty h3 { font-family:var(--f-display); font-size:var(--fs-lg); margin:14px 0 6px; }
.pr-empty p { font-size:var(--fs-sm); color:var(--text-sec); line-height:var(--lh-body); margin-bottom:20px; }
.pr-empty .cta { display:inline-flex; align-items:center; gap:7px; padding:9px 16px; border-radius:var(--r-md); border:none;
  background:hsl(var(--c-agent)); color:#3a2400; font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); cursor:pointer; }
.pr-empty .cta.ghost { background:transparent; border:1px solid var(--border-strong); color:var(--text-sec); }

@keyframes prshimmer { 0%{ background-position:-400px 0; } 100%{ background-position:400px 0; } }
.pr-sk { border-radius:var(--r-sm); background:linear-gradient(90deg,var(--bg-muted) 25%,var(--bg-hover) 37%,var(--bg-muted) 63%); background-size:800px 100%; animation:prshimmer 1.4s linear infinite; }

/* ─── mobile cards ─── */
.pr-cards { display:flex; flex-direction:column; gap:11px; padding:14px; }
.pr-card { position:relative; background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-lg); padding:13px 14px; cursor:pointer;
  transition:border-color var(--dur-sm) var(--ease-out); }
.pr-card:hover, .pr-card:focus-visible { border-color:var(--border-strong); }
.pr-card.rejected { border-left:3px solid hsl(var(--c-danger) / .5); }
.pr-card .chead { display:flex; align-items:flex-start; gap:10px; }
.pr-card .cname { flex:1; font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-base); line-height:var(--lh-snug); }
.pr-card .cmenu { width:28px; height:28px; flex-shrink:0; border-radius:var(--r-sm); border:none; background:transparent; color:var(--text-muted); cursor:pointer; font-size:16px; }
.pr-card .cmenu:hover { background:var(--bg-muted); color:var(--text); }
.pr-card .cdesc { font-size:var(--fs-sm); color:var(--text-sec); line-height:var(--lh-snug); margin:9px 0 11px;
  display:-webkit-box; -webkit-line-clamp:2; -webkit-box-orient:vertical; overflow:hidden; }
.pr-card .cfoot { display:flex; align-items:center; flex-wrap:wrap; gap:8px; }
.pr-card .cfoot .grow { flex:1; }
.pr-card .creject { margin-top:11px; }
.pr-menu { position:absolute; top:40px; right:10px; z-index:var(--z-overlay); background:var(--bg-card); border:1px solid var(--border);
  border-radius:var(--r-md); box-shadow:var(--shadow-lg); padding:5px; min-width:170px; display:flex; flex-direction:column; gap:2px; }
.pr-menu button { display:flex; align-items:center; gap:9px; padding:8px 10px; border-radius:var(--r-sm); border:none; background:transparent;
  color:var(--text); font-family:var(--f-body); font-weight:var(--fw-semi); font-size:var(--fs-sm); cursor:pointer; text-align:left; }
.pr-menu button:hover { background:var(--bg-muted); }

/* mobile adaptations */
.pr-app.is-mobile .pr-head { padding:12px 14px 13px; flex-wrap:wrap; gap:10px; }
.pr-app.is-mobile .pr-h1 { font-size:var(--fs-xl); }
.pr-app.is-mobile .pr-newbtn { width:100%; justify-content:center; order:3; }
.pr-app.is-mobile .pr-toolbar { flex-wrap:wrap; padding:11px 14px; gap:10px; }
.pr-app.is-mobile .pr-search { flex:1 1 100%; max-width:100%; }
.pr-app.is-mobile .pr-chips { flex:1 1 100%; flex-wrap:wrap; overflow-x:visible; }
.pr-app.is-mobile .pr-body { overflow-x:hidden; }
.pr-app.is-mobile .pr-vtoggle { display:none; }
.pr-app.is-mobile .pr-empty { padding:34px 22px; }

@media (prefers-reduced-motion: reduce) {
  .pr-live i, .pr-search .busy i, .pr-sk { animation:none; }
}
`;

/* ──────────────────────────────────────────────────────────
   Dataset — 12 typology proposals (dati realistici IT).
   Game scope: Catan, Power Grid, Codenames, Wingspan, Ark Nova, Brass.
   Author: Marco R. / Sara T. — caps = internal taxonomy.
   ────────────────────────────────────────────────────────── */
const AUTHORS = {
  marco: { name: 'Marco R.', initials: 'MR' },
  sara:  { name: 'Sara T.',  initials: 'ST' },
};

const PROPOSALS = [
  { id: 'tp-catan-rules', name: 'Catan Rules Expert', status: 'Approved', live: true, author: 'marco',
    desc: 'Esperto delle regole ufficiali di Catan: risponde su preparazione, commercio, ladrone e calcolo dei punti citando il manuale.',
    caps: ['Q&A', 'Streaming', 'Web'], scope: ['Catan'], updated: '2 ore fa', updatedAbs: '2 giu 2026, 16:04' },

  { id: 'tp-strategy-advisor', name: 'Strategy Advisor', status: 'Draft', author: 'sara',
    desc: 'Consiglia mosse di apertura e gestione delle risorse nei gestionali economici, analizzando il tempo di gioco residuo.',
    caps: ['Q&A', 'Tool'], scope: ['Power Grid', 'Brass'], updated: '5 ore fa', updatedAbs: '2 giu 2026, 13:20' },

  { id: 'tp-setup-tutor', name: 'Setup Tutor', status: 'Draft', author: 'marco',
    desc: 'Guida passo-passo alla preparazione del tavolo: piazzamento iniziale, mazzi e plance per ciascun giocatore.',
    caps: ['Q&A', 'Image'], scope: ['Wingspan', 'Catan', 'Ark Nova'], updated: 'Ieri', updatedAbs: '1 giu 2026, 21:42' },

  { id: 'tp-wing-strategy', name: 'Wingspan Strategy Advisor', status: 'PendingReview', author: 'sara', reviewSince: 'Da 2 giorni in review',
    desc: 'Suggerisce combinazioni di uccelli e ottimizzazione degli habitat per massimizzare i punti a fine partita.',
    caps: ['Q&A', 'Streaming'], scope: ['Wingspan'], updated: '2 giorni fa', updatedAbs: '31 mag 2026, 10:15' },

  { id: 'tp-codenames-spy', name: 'Codenames Spymaster', status: 'PendingReview', author: 'marco', reviewSince: 'Da 3 giorni in review',
    desc: 'Genera indizi a tema e valuta il rischio delle parole avversarie per la squadra rossa o blu.',
    caps: ['Q&A'], scope: ['Codenames'], updated: '3 giorni fa', updatedAbs: '30 mag 2026, 18:00' },

  { id: 'tp-powergrid-auction', name: 'Power Grid Auction Helper', status: 'Approved', live: true, author: 'sara',
    desc: 'Calcola l\u2019offerta ottimale nelle aste delle centrali in base a risorse disponibili e turno di gioco.',
    caps: ['Q&A', 'Tool', 'Streaming'], scope: ['Power Grid'], updated: '4 giorni fa', updatedAbs: '29 mag 2026, 09:30' },

  { id: 'tp-rules-arbiter', name: 'Rules Arbiter', status: 'Rejected', author: 'marco',
    desc: 'Arbitro imparziale per dispute sulle regole durante la partita, con riferimento al regolamento ufficiale.',
    caps: ['Q&A', 'Web'], scope: ['Catan', 'Codenames'], updated: '5 giorni fa', updatedAbs: '28 mag 2026, 14:11',
    rejection: 'Sovrapposizione troppo ampia con \u201CCatan Rules Expert\u201D. Restringi lo scope a un singolo gioco prima di risottomettere.' },

  { id: 'tp-onboarding-coach', name: 'Onboarding Coach', status: 'Draft', author: 'sara',
    desc: 'Insegna le basi a chi gioca per la prima volta, con esempi visivi e brevi quiz di verifica.',
    caps: ['Q&A', 'Image'], scope: ['Wingspan'], updated: '6 giorni fa', updatedAbs: '27 mag 2026, 11:48' },

  { id: 'tp-catan-negotiate', name: 'Catan Negotiation Bot', status: 'Draft', author: 'marco',
    desc: 'Simula trattative commerciali e suggerisce scambi equi tra giocatori durante la fase di commercio.',
    caps: ['Q&A', 'Streaming'], scope: ['Catan'], updated: '1 settimana fa', updatedAbs: '26 mag 2026, 20:05' },

  { id: 'tp-tournament-judge', name: 'Tournament Judge', status: 'PendingReview', author: 'sara', reviewSince: 'Da 1 giorno in review',
    desc: 'Applica i regolamenti da torneo e tiene traccia delle penalità ufficiali per ogni giocatore.',
    caps: ['Q&A', 'Tool', 'Web'], scope: ['Power Grid', 'Wingspan', 'Catan'], updated: '1 settimana fa', updatedAbs: '25 mag 2026, 15:33' },

  { id: 'tp-codenames-guess', name: 'Codenames Guesser Aid', status: 'Approved', live: true, author: 'marco',
    desc: 'Aiuta a interpretare gli indizi del capospia minimizzando i tocchi sulle parole avversarie.',
    caps: ['Q&A', 'Streaming'], scope: ['Codenames'], updated: '2 settimane fa', updatedAbs: '19 mag 2026, 12:00' },

  { id: 'tp-endgame-scorer', name: 'Endgame Scorer', status: 'Approved', author: 'sara',
    desc: 'Calcola il punteggio finale gestendo bonus, obiettivi di fine partita e criteri di spareggio.',
    caps: ['Q&A', 'Tool'], scope: ['Wingspan', 'Ark Nova'], updated: '3 settimane fa', updatedAbs: '12 mag 2026, 17:22' },
];

const STATUS_META = {
  Draft:         { cls: 'draft',    icon: '\uD83D\uDCDD', label: 'Draft' },
  PendingReview: { cls: 'review',   icon: '\u23F3', label: 'In review' },
  Approved:      { cls: 'approved', icon: '\u2713', label: 'Approvata' },
  Rejected:      { cls: 'rejected', icon: '\u2717', label: 'Rifiutata' },
};

const STATUS_ORDER = ['Draft', 'PendingReview', 'Approved', 'Rejected'];

function actionsFor(status) {
  switch (status) {
    case 'Draft':         return [['edit', '\u270F\uFE0F', 'Modifica'], ['test', '\uD83E\uDDEA', 'Testa nel playground'], ['submit', '\u2B06\uFE0F', 'Sottometti per review', 'submit']];
    case 'PendingReview': return [['view', '\uD83D\uDC41\uFE0F', 'Vedi dettaglio']];
    case 'Approved':      return [['view', '\uD83D\uDC41\uFE0F', 'Vedi dettaglio'], ['test', '\uD83E\uDDEA', 'Testa nel playground']];
    case 'Rejected':      return [['edit', '\u270F\uFE0F', 'Riprendi e modifica'], ['feedback', '\uD83D\uDC41\uFE0F', 'Vedi feedback admin']];
    default:              return [];
  }
}

/* ──────────────────────────────────────────────────────────
   Scenari (9 stati)
   ────────────────────────────────────────────────────────── */
const SCENARIOS = {
  'default':            { loading: false, error: false, empty: false, search: '', status: [], view: 'table', expandRejected: false },
  'empty-all':          { loading: false, error: false, empty: true,  search: '', status: [], view: 'table', expandRejected: false },
  'loading':            { loading: true,  error: false, empty: false, search: '', status: [], view: 'table', expandRejected: false },
  'error':              { loading: false, error: true,  empty: false, search: '', status: [], view: 'table', expandRejected: false },
  'filter-search':      { loading: false, error: false, empty: false, search: 'Catan', status: [], view: 'table', expandRejected: false },
  'filter-status':      { loading: false, error: false, empty: false, search: '', status: ['Rejected'], view: 'table', expandRejected: true },
  'filter-empty':       { loading: false, error: false, empty: false, search: 'Monopoly', status: [], view: 'table', expandRejected: false },
  'rejected-expanded':  { loading: false, error: false, empty: false, search: '', status: [], view: 'table', expandRejected: true },
  'mobile-cards':       { loading: false, error: false, empty: false, search: '', status: [], view: 'cards', expandRejected: false },
};
const STATE_LIST = [
  ['default', 'Default'], ['empty-all', 'Empty'], ['loading', 'Loading'], ['error', 'Error'],
  ['filter-search', 'Filter · search'], ['filter-status', 'Filter · status'], ['filter-empty', 'Filter · 0 match'],
  ['rejected-expanded', 'Rejected row'], ['mobile-cards', 'Cards view'],
];

/* ──────────────────────────────────────────────────────────
   Sub-components
   ────────────────────────────────────────────────────────── */
function StatusBadge({ status, live, reviewSince }) {
  const m = STATUS_META[status];
  return (
    <div className="pr-status">
      <span className={'pr-badge ' + m.cls} aria-label={'Stato: ' + m.label}>
        <span className="bi" aria-hidden="true">{m.icon}</span>{m.label}
      </span>
      {live && <span className="pr-live"><i />Live</span>}
      {reviewSince && <span className="pr-since">{reviewSince}</span>}
    </div>
  );
}

function CapStrip({ caps }) {
  const visible = caps.slice(0, 3);
  const extra = caps.length - visible.length;
  return (
    <div className="pr-caps" aria-label={'Capacità: ' + caps.join(', ')}>
      {visible.map(c => <span className="pr-cap" key={c}>{c}</span>)}
      {extra > 0 && <span className="pr-cap more">+{extra}</span>}
    </div>
  );
}

function ScopePip({ scope }) {
  return (
    <span className="pr-scope" tabIndex={0} aria-label={'Scope: ' + scope.length + ' giochi — ' + scope.join(', ')}>
      <span className="pin" aria-hidden="true">🎲</span>
      {scope.length} {scope.length === 1 ? 'gioco' : 'giochi'}
      <span className="tip" role="tooltip">{scope.join(' · ')}</span>
    </span>
  );
}

function AuthorChip({ author }) {
  const a = AUTHORS[author];
  return (
    <span className="pr-author" title={'Autore: ' + a.name}>
      <span className="av" aria-hidden="true">{a.initials}</span>{a.name}
    </span>
  );
}

function ProposalActions({ status }) {
  return (
    <div className="pr-acts" role="group" aria-label="Azioni proposal">
      {actionsFor(status).map(([key, icon, label, mod]) => (
        <button key={key} className={'pr-act' + (mod ? ' ' + mod : '')} aria-label={label} title={label}
                onClick={e => e.stopPropagation()}>{icon}</button>
      ))}
    </div>
  );
}

function RejectionAlert({ reason, margin = true }) {
  return (
    <div className={'pr-reject' + (margin ? '' : ' nomargin')} style={margin ? null : { margin: '11px 0 0' }} role="note">
      <span className="ri" aria-hidden="true">⚠️</span>
      <div className="rt"><b>Feedback admin:</b> {reason} <a tabIndex={0} role="button">Vedi feedback completo</a></div>
    </div>
  );
}

/* ─── desktop table ─── */
function TableHeader({ sortKey, sortDir, onSort }) {
  const sortAttr = k => sortKey === k ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none';
  return (
    <div className="pr-thead" role="row">
      <div className="pr-th sortable" role="columnheader" aria-sort={sortAttr('name')} tabIndex={0}
           onClick={() => onSort('name')} onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onSort('name'); } }}>
        Name <span className="arr">{sortKey === 'name' && sortDir === 'desc' ? '▼' : '▲'}</span>
      </div>
      <div className="pr-th" role="columnheader">Descrizione</div>
      <div className="pr-th" role="columnheader">Status</div>
      <div className="pr-th" role="columnheader">Capabilities</div>
      <div className="pr-th" role="columnheader">Scope</div>
      <div className="pr-th sortable" role="columnheader" aria-sort={sortAttr('updated')} tabIndex={0}
           onClick={() => onSort('updated')} onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onSort('updated'); } }}>
        Updated <span className="arr">{sortKey === 'updated' && sortDir === 'desc' ? '▼' : '▲'}</span>
      </div>
      <div className="pr-th right" role="columnheader">Azioni</div>
    </div>
  );
}

function TableRow({ p, expandRejected, rowRef }) {
  const showReject = p.status === 'Rejected' && p.rejection && expandRejected;
  return (
    <div className={'pr-row' + (p.status === 'Rejected' ? ' rejected' : '')} role="row" tabIndex={0} ref={rowRef}
         onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); /* naviga a /edit */ } }}>
      <div className="pr-rowmain">
        <div className="pr-cell">
          <div className="pr-name">{p.name}</div>
          <div className="av-row"><AuthorChip author={p.author} /></div>
        </div>
        <div className="pr-cell"><div className="pr-desc">{p.desc}</div></div>
        <div className="pr-cell" role="cell"><StatusBadge status={p.status} live={p.live} reviewSince={p.reviewSince} /></div>
        <div className="pr-cell" role="cell"><CapStrip caps={p.caps} /></div>
        <div className="pr-cell" role="cell"><ScopePip scope={p.scope} /></div>
        <div className="pr-cell" role="cell"><span className="pr-upd" title={p.updatedAbs}>{p.updated}</span></div>
        <div className="pr-cell" role="cell"><ProposalActions status={p.status} /></div>
      </div>
      {showReject && <RejectionAlert reason={p.rejection} />}
    </div>
  );
}

function ProposalsTable({ rows, expandRejected, sortKey, sortDir, onSort, firstRowRef }) {
  return (
    <div className="pr-table" role="table" aria-label="Typology proposals">
      <TableHeader sortKey={sortKey} sortDir={sortDir} onSort={onSort} />
      <div role="rowgroup">
        {rows.map((p, i) => <TableRow key={p.id} p={p} expandRejected={expandRejected} rowRef={i === 0 ? firstRowRef : null} />)}
      </div>
    </div>
  );
}

/* ─── mobile cards ─── */
function ProposalCard({ p, expandRejected }) {
  const [menu, setMenu] = useState(false);
  const showReject = p.status === 'Rejected' && p.rejection && expandRejected;
  return (
    <div className={'pr-card' + (p.status === 'Rejected' ? ' rejected' : '')} role="button" tabIndex={0}>
      <div className="chead">
        <div className="cname">{p.name}</div>
        <StatusBadge status={p.status} live={p.live} reviewSince={p.reviewSince} />
        <button className="cmenu" aria-label="Azioni proposal" aria-haspopup="menu" aria-expanded={menu}
                onClick={e => { e.stopPropagation(); setMenu(m => !m); }}>⋮</button>
      </div>
      <div className="cdesc">{p.desc}</div>
      <div className="cfoot">
        <CapStrip caps={p.caps} />
        <span className="grow" />
        <ScopePip scope={p.scope} />
        <span className="pr-upd" title={p.updatedAbs}>{p.updated}</span>
      </div>
      <div className="cfoot" style={{ marginTop: 9 }}><AuthorChip author={p.author} /></div>
      {showReject && <div className="creject"><RejectionAlert reason={p.rejection} margin={false} /></div>}
      {menu && (
        <div className="pr-menu" role="menu" onClick={e => e.stopPropagation()}>
          {actionsFor(p.status).map(([key, icon, label]) => (
            <button key={key} role="menuitem" onClick={() => setMenu(false)}><span aria-hidden="true">{icon}</span>{label}</button>
          ))}
        </div>
      )}
    </div>
  );
}

function ProposalsCards({ rows, expandRejected }) {
  return <div className="pr-cards">{rows.map(p => <ProposalCard key={p.id} p={p} expandRejected={expandRejected} />)}</div>;
}

/* ─── empty / filter-empty / skeleton ─── */
function EmptyAll() {
  return (
    <div className="pr-pad">
      <div className="pr-empty">
        <div className="em" aria-hidden="true">🤖</div>
        <h3>Nessuna proposal ancora</h3>
        <p>Inizia creando la tua prima typology AI: definisci nome, capacità e i giochi su cui sarà esperta.</p>
        <button className="cta"><span aria-hidden="true">+</span> Crea la prima proposal</button>
      </div>
    </div>
  );
}

function FilterEmpty({ onClear }) {
  return (
    <div className="pr-pad" aria-live="polite">
      <div className="pr-empty">
        <div className="em" aria-hidden="true">🔍</div>
        <h3>Nessuna proposal corrisponde ai filtri</h3>
        <p>Prova a modificare la ricerca o a rimuovere i filtri di stato attivi.</p>
        <button className="cta ghost" onClick={onClear}>Rimuovi filtri</button>
      </div>
    </div>
  );
}

function SkeletonTable({ mobile }) {
  if (mobile) {
    return (
      <div className="pr-cards" aria-busy="true">
        {[0, 1, 2, 3].map(i => (
          <div className="pr-card" key={i}>
            <div className="chead"><div className="pr-sk" style={{ flex: 1, height: 16 }} /><div className="pr-sk" style={{ width: 78, height: 22, borderRadius: 'var(--r-pill)' }} /></div>
            <div className="pr-sk" style={{ width: '100%', height: 13, margin: '11px 0 6px' }} />
            <div className="pr-sk" style={{ width: '72%', height: 13, marginBottom: 11 }} />
            <div className="pr-sk" style={{ width: 130, height: 20, borderRadius: 'var(--r-pill)' }} />
          </div>
        ))}
      </div>
    );
  }
  return (
    <div className="pr-table" aria-busy="true">
      <div className="pr-thead">
        {['name', 'desc', 'st', 'cap', 'sc', 'up', 'ac'].map(k => <div key={k} className="pr-sk" style={{ height: 11, width: k === 'desc' ? '60%' : '50%' }} />)}
      </div>
      {[0, 1, 2, 3].map(i => (
        <div className="pr-rowmain" key={i} style={{ borderBottom: '1px solid var(--border-light)' }}>
          <div><div className="pr-sk" style={{ width: '80%', height: 14, marginBottom: 8 }} /><div className="pr-sk" style={{ width: 70, height: 17, borderRadius: 'var(--r-pill)' }} /></div>
          <div><div className="pr-sk" style={{ width: '100%', height: 12, marginBottom: 6 }} /><div className="pr-sk" style={{ width: '70%', height: 12 }} /></div>
          <div className="pr-sk" style={{ width: 84, height: 22, borderRadius: 'var(--r-pill)' }} />
          <div className="pr-sk" style={{ width: '90%', height: 18, borderRadius: 'var(--r-sm)' }} />
          <div className="pr-sk" style={{ width: 78, height: 24, borderRadius: 'var(--r-pill)' }} />
          <div className="pr-sk" style={{ width: 60, height: 12 }} />
          <div className="pr-sk" style={{ width: 96, height: 30, borderRadius: 'var(--r-sm)', marginLeft: 'auto' }} />
        </div>
      ))}
    </div>
  );
}

/* ─── Toolbar ─── */
function Toolbar({ search, setSearch, typing, status, toggleStatus, counts, view, setView, searchRef, onClearSearch }) {
  return (
    <div className="pr-toolbar">
      <div className={'pr-search' + (search ? ' active' : '')}>
        <span className="ic" aria-hidden="true">🔍</span>
        <input ref={searchRef} value={search} onChange={e => setSearch(e.target.value)} role="searchbox"
               aria-label="Cerca proposals" placeholder="Cerca per nome o descrizione…"
               onKeyDown={e => { if (e.key === 'Escape') onClearSearch(); }} />
        {typing && <span className="busy" aria-hidden="true"><i />Cercando…</span>}
        {search && <button className="clear" aria-label="Cancella ricerca" onClick={onClearSearch}>✕</button>}
      </div>
      <div className="pr-chips" role="group" aria-label="Filtra per stato">
        <button className={'pr-chip all' + (status.length === 0 ? ' on' : '')} aria-pressed={status.length === 0} onClick={() => toggleStatus(null)}>
          <span className="cdot" />Tutti <span className="ccount">({counts.all})</span>
        </button>
        {STATUS_ORDER.map(st => (
          <button key={st} className={'pr-chip' + (status.includes(st) ? ' on' : '')} data-st={st} aria-pressed={status.includes(st)} onClick={() => toggleStatus(st)}>
            <span className="cdot" />{st} <span className="ccount">({counts[st]})</span>
          </button>
        ))}
      </div>
      <div className="pr-vtoggle" role="group" aria-label="Vista">
        <button aria-pressed={view === 'table'} onClick={() => setView('table')} title="Vista tabella">☰ Table</button>
        <button aria-pressed={view === 'cards'} onClick={() => setView('cards')} title="Vista cards">▦ Cards</button>
      </div>
    </div>
  );
}

/* ──────────────────────────────────────────────────────────
   ProposalsApp — un'istanza per (state × viewport). Remount via key.
   ────────────────────────────────────────────────────────── */
function ProposalsApp({ stateId, mobile }) {
  const sc = SCENARIOS[stateId];
  const [search, setSearch] = useState(sc.search);
  const [status, setStatus] = useState(sc.status);
  const [view, setView] = useState(sc.view);
  const [sortKey, setSortKey] = useState('name');
  const [sortDir, setSortDir] = useState('asc');
  const [typing, setTyping] = useState(false);
  const searchRef = useRef(null);
  const firstRowRef = useRef(null);
  const typingTimer = useRef(null);

  // debounce visual
  useEffect(() => {
    if (!search) { setTyping(false); return; }
    setTyping(true);
    clearTimeout(typingTimer.current);
    typingTimer.current = setTimeout(() => setTyping(false), 650);
    return () => clearTimeout(typingTimer.current);
  }, [search]);

  // Ctrl+F → focus search
  useEffect(() => {
    const h = e => { if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'f') { e.preventDefault(); searchRef.current && searchRef.current.focus(); } };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, []);

  const counts = useMemo(() => {
    const c = { all: PROPOSALS.length };
    STATUS_ORDER.forEach(s => { c[s] = PROPOSALS.filter(p => p.status === s).length; });
    return c;
  }, []);

  const toggleStatus = st => {
    if (st === null) { setStatus([]); return; }
    setStatus(prev => prev.includes(st) ? prev.filter(s => s !== st) : [...prev, st]);
  };
  const clearFilters = () => { setSearch(''); setStatus([]); };
  const onClearSearch = () => { setSearch(''); searchRef.current && searchRef.current.focus(); };
  const onSort = key => {
    if (sortKey === key) setSortDir(d => d === 'asc' ? 'desc' : 'asc');
    else { setSortKey(key); setSortDir('asc'); }
  };

  const q = search.trim().toLowerCase();
  let rows = PROPOSALS.filter(p =>
    (status.length === 0 || status.includes(p.status)) &&
    (!q || p.name.toLowerCase().includes(q) || p.desc.toLowerCase().includes(q) || p.scope.some(g => g.toLowerCase().includes(q)))
  );
  const UPD_RANK = id => PROPOSALS.findIndex(p => p.id === id); // array già in ordine cronologico desc
  rows = [...rows].sort((a, b) => {
    let r;
    if (sortKey === 'name') r = a.name.localeCompare(b.name);
    else r = UPD_RANK(a.id) - UPD_RANK(b.id);
    return sortDir === 'asc' ? r : -r;
  });

  const expandRejected = sc.expandRejected;
  const filtersActive = status.length > 0 || q.length > 0;

  let bodyEl;
  if (sc.empty) bodyEl = <EmptyAll />;
  else if (sc.loading) bodyEl = <SkeletonTable mobile={mobile} />;
  else if (rows.length === 0) bodyEl = <FilterEmpty onClear={clearFilters} />;
  else if (mobile || view === 'cards') bodyEl = <ProposalsCards rows={rows} expandRejected={expandRejected} />;
  else bodyEl = <ProposalsTable rows={rows} expandRejected={expandRejected} sortKey={sortKey} sortDir={sortDir} onSort={onSort} firstRowRef={firstRowRef} />;

  return (
    <div className={'pr-app' + (mobile ? ' is-mobile' : '')}>
      {sc.error && (
        <div className="pr-errbar" role="alert">
          <span aria-hidden="true">⚠️</span> Impossibile caricare le proposals — riprova.
          <span className="grow" />
          <button className="retry"><span aria-hidden="true">↻</span> Riprova</button>
        </div>
      )}

      <header className="pr-head">
        <div className="htxt">
          <div className="pr-bread"><span>Editor</span><span className="sep">›</span><span className="cur">Agent proposals</span></div>
          <h1 className="pr-h1">Le mie typology proposals</h1>
          <div className="pr-sub">Crea, testa e sottometti tipologie AI agent per l’approvazione</div>
        </div>
        <span className="grow" />
        <button className="pr-newbtn"><span className="ic" aria-hidden="true">+</span> Crea proposal</button>
      </header>

      {!sc.error && (
        <Toolbar search={search} setSearch={setSearch} typing={typing} status={status} toggleStatus={toggleStatus}
                 counts={counts} view={view} setView={setView} searchRef={searchRef} onClearSearch={onClearSearch} />
      )}

      <div className="pr-body">{bodyEl}</div>
    </div>
  );
}

/* ──────────────────────────────────────────────────────────
   Harness — continuity con S1
   ────────────────────────────────────────────────────────── */
function Harness() {
  const [stateId, setStateId] = useState(() => localStorage.getItem('pr-state') || 'default');
  const [theme, setTheme] = useState(() => localStorage.getItem('mai-theme') || 'light');

  useEffect(() => { document.documentElement.setAttribute('data-theme', theme); localStorage.setItem('mai-theme', theme); }, [theme]);
  useEffect(() => { localStorage.setItem('pr-state', stateId); }, [stateId]);

  return (
    <div className="ed-stage">
      <style dangerouslySetInnerHTML={{ __html: PROP_CSS }} />
      <button className="theme-toggle" onClick={() => setTheme(theme === 'light' ? 'dark' : 'light')}>🌗 <span>{theme === 'dark' ? 'Dark' : 'Light'}</span></button>

      <div className="ed-wrap">
        <div className="ed-kicker">SP4 · B14 · #1489 — schermata 2 / 5 · typology proposals list</div>
        <h1>Typology <span className="acc">proposals</span> — /editor/agent-proposals</h1>
        <p className="ed-lead">
          Lista delle <b>typology proposals</b> dell’editor: ogni proposal è un nuovo tipo di agente AI con un lifecycle
          (<b>Draft → PendingReview → Approved → Rejected</b>). Pattern hero + tabella filtrabile con search e status chip;
          table su desktop, cards stack su mobile. Entity primaria <code>--c-agent</code>, scope giochi via EntityPip <code>--c-game</code>.
        </p>

        <div className="ed-notes">
          <div className="ed-note">
            <h4>Pattern</h4>
            <p><b>Hero + body</b>: header sticky con breadcrumb/title/CTA, toolbar (search 38% · status chip · view toggle), body <b>table desktop</b> / <b>cards mobile</b>. Badge status entity-colored riusati da toolkit-detail.</p>
          </div>
          <div className="ed-note">
            <h4>9 stati</h4>
            <p>Selettore qui sotto: default · empty · loading · error · filter-search · filter-status · filter-0-match · rejected-row · cards-view. Action button per-status (Edit / Test / Submit / View).</p>
          </div>
          <div className="ed-note">
            <h4>Mobile & a11y</h4>
            <p>Mobile = cards stack, chip a capo, menu <code>⋮</code> per azioni. <code>role=table/row/cell</code>, <code>aria-sort</code> sulle colonne, <code>aria-pressed</code> sui chip, <code>searchbox</code>, <code>Ctrl+F</code> / <code>Esc</code>.</p>
          </div>
        </div>

        <div className="ed-rail">
          <span className="lab">Stato</span>
          <div className="ed-states" role="group" aria-label="Selettore stato schermata">
            {STATE_LIST.map(([id, label]) => (
              <button key={id} className={'ed-sbtn' + (stateId === id ? ' on' : '')} aria-pressed={stateId === id} onClick={() => setStateId(id)}>
                <span className="pip" />{label}
              </button>
            ))}
          </div>
        </div>

        <div className="ed-vp-label">Desktop · 1440 — hero + table</div>
        <div className="ed-desk">
          <div className="ed-chrome">
            <div className="dots"><i /><i /><i /></div>
            <div className="url">meepleai.app/editor/agent-proposals</div>
          </div>
          <div style={{ flex: 1, minHeight: 0 }}>
            <ProposalsApp key={'d-' + stateId} stateId={stateId} mobile={false} />
          </div>
        </div>

        <div className="ed-vp-label">Mobile · 375 — cards stack</div>
        <div className="ed-phone-row">
          <div className="phone">
            <div className="phone-sbar"><span>9:41</span><span className="ind">●●● 5G ▮</span></div>
            <div style={{ flex: 1, minHeight: 0, display: 'flex' }}>
              <ProposalsApp key={'m-' + stateId} stateId={stateId} mobile={true} />
            </div>
          </div>
          <div className="ed-phone-cap">
            <h4>Layout mobile</h4>
            <p>Niente table: stack di <b>cards</b>. Status badge in alto a destra, menu <code>⋮</code> per le azioni (drawer-style), chip di stato che vanno a capo senza scroll orizzontale. Le righe Rejected mostrano l’alert feedback inline (stato <code>rejected-row</code>).</p>
          </div>
        </div>
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<Harness />);
