/* sp4-editor-proposals-test.jsx
   Route: /editor/agent-proposals/[id]/test — Playground streaming per testare una typology PRE-submit.
   B14 (issue #1489) · screen 5 of 5 (ULTIMA del cluster) · Tier M
   Pattern: Split-view asimmetrico — config sidebar sx (320px) + chat body dx (fluid) + optional trace drawer (380px).
   Mobile: config collassa in header-drawer top + chat full-width + trace in bottom-sheet.
   Continuity: S1 split-view · S2 pr-badge (status) · S4 ce-author chip + breadcrumb header. Entity primaria --c-agent,
   user message --c-player, accenti sessione --c-chat, KB sources --c-kb, game scope --c-game.
   FSM streaming 4-stati: idle · streaming (cursor BLINK + typewriter) · completed · error.
   Loadable standalone via Babel. Injects own component CSS; relies on tokens.css + components.css.
   v2 components surfaced here:
   /* v2: TestPlaygroundSplit, ConfigSidebar, TypologySummaryCard, SampleInputsPanel, SessionConfigPanel,
          TraceSummaryMini, ChatBody, ChatStreamHeader, MessageList, UserBubble, AgentBubble (fsm),
          StreamingCursor, MessageActions, SystemNotice, ChatComposer, ComposerToolbar, StopButton,
          TraceDrawer, TraceBlock, CompareColumns, MobileConfigDrawer */

const { useState, useEffect, useMemo, useRef } = React;

/* ══════════════════════════════════════════════════════════
   Component CSS — solo token da tokens.css / components.css.
   ══════════════════════════════════════════════════════════ */
const PT_CSS = `
/* ─── harness chrome (continuity S1/S4) ─── */
.ed-stage { min-height:100vh; padding:72px 24px 96px; background:var(--bg); color:var(--text); }
.ed-wrap { max-width:1380px; margin:0 auto; }
.ed-kicker { font-family:var(--f-mono); font-size:var(--fs-xs); letter-spacing:.1em; text-transform:uppercase; color:var(--text-muted); }
.ed-stage h1 { font-size:var(--fs-3xl); margin:8px 0 6px; }
.ed-stage h1 .acc { color:hsl(var(--c-agent)); }
.ed-lead { color:var(--text-sec); font-size:var(--fs-md); max-width:820px; line-height:var(--lh-body); }
.ed-lead code { background:var(--bg-muted); padding:1px 5px; border-radius:var(--r-xs); font-size:12px; font-family:var(--f-mono); }
.ed-notes { display:grid; grid-template-columns:repeat(3,1fr); gap:12px; margin:22px 0 4px; }
.ed-note { background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-lg); padding:14px 16px; }
.ed-note h4 { font-family:var(--f-display); font-size:var(--fs-sm); text-transform:uppercase; letter-spacing:.04em; color:hsl(var(--c-agent)); margin-bottom:6px; }
.ed-note p { font-size:var(--fs-sm); color:var(--text-sec); line-height:var(--lh-snug); }
.ed-note p b { color:var(--text); font-weight:var(--fw-bold); }
.ed-note code { background:var(--bg-muted); padding:1px 5px; border-radius:var(--r-xs); font-size:11px; font-family:var(--f-mono); }

.ed-rail { position:sticky; top:0; z-index:var(--z-sticky); margin:26px 0 18px; padding:12px 0;
  background:var(--bg); display:flex; align-items:flex-start; gap:14px; flex-wrap:wrap; border-bottom:1px solid var(--border); }
.ed-rail .lab { font-family:var(--f-mono); font-size:var(--fs-xs); text-transform:uppercase; letter-spacing:.08em; color:var(--text-muted); padding-top:8px; }
.ed-states { display:flex; gap:6px; flex-wrap:wrap; flex:1; }
.ed-sbtn { display:inline-flex; align-items:center; gap:7px; padding:7px 12px; border-radius:var(--r-pill);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm);
  background:var(--bg-card); border:1.5px solid var(--border); color:var(--text-sec); cursor:pointer; transition:all var(--dur-sm) var(--ease-out); }
.ed-sbtn:hover { transform:translateY(-1px); border-color:var(--border-strong); }
.ed-sbtn .pip { width:7px; height:7px; border-radius:50%; background:currentColor; opacity:.6; }
.ed-sbtn.on { background:hsl(var(--c-agent)); border-color:transparent; color:#3a2400; }
.ed-sbtn.on .pip { opacity:1; background:#3a2400; }

.ed-vp-label { font-family:var(--f-mono); font-size:var(--fs-xs); text-transform:uppercase; letter-spacing:.08em;
  color:var(--text-muted); margin:30px 0 12px; display:flex; align-items:center; gap:10px; }
.ed-vp-label::after { content:''; flex:1; height:1px; background:var(--border); }

.ed-desk { width:100%; max-width:1340px; height:880px; border-radius:var(--r-lg); overflow:hidden;
  background:var(--bg-card); border:1px solid var(--border); box-shadow:var(--shadow-lg); display:flex; flex-direction:column; }
.ed-chrome { height:38px; flex-shrink:0; display:flex; align-items:center; gap:8px; padding:0 14px;
  background:var(--bg-muted); border-bottom:1px solid var(--border); font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); }
.ed-chrome .dots { display:flex; gap:6px; }
.ed-chrome .dots i { width:11px; height:11px; border-radius:50%; display:block; }
.ed-chrome .dots i:nth-child(1){ background:#ff5f57; } .ed-chrome .dots i:nth-child(2){ background:#febc2e; } .ed-chrome .dots i:nth-child(3){ background:#28c840; }
.ed-chrome .url { flex:1; text-align:center; background:var(--bg-card); border-radius:var(--r-sm); padding:4px 10px; margin:0 11%; }
.ed-phone-row { display:flex; gap:28px; align-items:flex-start; flex-wrap:wrap; }
.ed-phone-cap { font-size:var(--fs-sm); color:var(--text-sec); max-width:300px; line-height:var(--lh-snug); }
.ed-phone-cap h4 { font-family:var(--f-display); font-size:var(--fs-base); margin-bottom:6px; }
.ed-phone-cap code { background:var(--bg-muted); padding:1px 5px; border-radius:var(--r-xs); font-size:11px; font-family:var(--f-mono); }

/* ─── app shell ─── */
.pt-app { display:flex; flex-direction:column; height:100%; min-height:0; background:var(--bg); color:var(--text); position:relative; overflow:hidden; }
.pt-app :focus-visible { outline:2px solid hsl(var(--c-agent)); outline-offset:2px; border-radius:var(--r-xs); }

/* header (sticky, esteso da S4) */
.pt-head { flex-shrink:0; background:var(--bg-card); border-bottom:1px solid var(--border); }
.pt-head .hrow { display:flex; align-items:flex-start; gap:16px; padding:13px 20px 14px; }
.pt-head .htxt { min-width:0; }
.pt-bread { font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); letter-spacing:.03em; display:flex; align-items:center; gap:6px; margin-bottom:6px; flex-wrap:wrap; }
.pt-bread .sep { opacity:.5; }
.pt-bread .cur { color:hsl(var(--c-agent)); font-weight:var(--fw-bold); }
.pt-titlerow { display:flex; align-items:center; gap:10px; flex-wrap:wrap; }
.pt-h1 { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-2xl); letter-spacing:-.01em; line-height:var(--lh-tight); }
.pt-sub { font-size:var(--fs-sm); color:var(--text-sec); margin-top:6px; }
.pt-sub b { color:var(--text); }
.pt-livechip { display:inline-flex; align-items:center; gap:5px; padding:3px 9px; border-radius:var(--r-pill);
  background:hsl(var(--c-toolkit) / .14); color:hsl(var(--c-toolkit)); font-family:var(--f-mono); font-size:10px; font-weight:var(--fw-bold); border:1px solid hsl(var(--c-toolkit) / .3); }
.pt-livechip i { width:7px; height:7px; border-radius:50%; background:hsl(var(--c-toolkit)); animation:ptpulse 2s var(--ease-in-out) infinite; }
.pt-head .grow { flex:1; }
.pt-headcta { display:flex; align-items:center; gap:8px; flex-shrink:0; flex-wrap:wrap; justify-content:flex-end; }

/* status badge inline (riuso pr-badge S2) */
.ce-badge { display:inline-flex; align-items:center; gap:5px; padding:4px 11px; border-radius:var(--r-pill);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); border:1px solid transparent; white-space:nowrap; }
.ce-badge .bi { font-size:13px; line-height:1; }
.ce-badge.draft    { background:var(--bg-muted); border-color:var(--border); color:var(--text-muted); }
.ce-badge.rejected { background:hsl(var(--c-danger) / .14); border-color:hsl(var(--c-danger) / .35); color:hsl(var(--c-danger)); }

/* author chip (player entity, riuso S4) */
.ce-author { display:inline-flex; align-items:center; gap:6px; padding:2px 10px 2px 2px; border-radius:var(--r-pill);
  background:hsl(var(--c-player) / .14); color:hsl(var(--c-player)); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); }
.ce-author .av { width:18px; height:18px; border-radius:50%; background:hsl(var(--c-player)); color:#fff; display:flex; align-items:center; justify-content:center; font-size:9px; font-weight:var(--fw-ext); }

/* generic buttons */
.pt-btn { display:inline-flex; align-items:center; gap:6px; padding:8px 13px; border-radius:var(--r-md); border:1px solid transparent;
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); cursor:pointer; transition:all var(--dur-sm) var(--ease-out); white-space:nowrap; }
.pt-btn .ic { font-size:13px; line-height:1; }
.pt-btn.link { background:transparent; color:var(--text-sec); padding:8px 6px; }
.pt-btn.link:hover { color:var(--text); }
.pt-btn.sec { background:var(--bg-card); border-color:var(--border); color:var(--text-sec); }
.pt-btn.sec:hover { border-color:var(--border-strong); color:var(--text); transform:translateY(-1px); }
.pt-btn.info-out { background:transparent; border-color:hsl(var(--c-info) / .5); color:hsl(var(--c-info)); }
.pt-btn.info-out:hover { background:hsl(var(--c-info) / .1); }
.pt-btn.agent { background:hsl(var(--c-agent)); color:#3a2400; box-shadow:var(--shadow-xs); }
.pt-btn.agent:hover:not(:disabled) { transform:translateY(-1px); box-shadow:var(--shadow-sm); filter:brightness(1.03); }
.pt-btn.agent:disabled { opacity:.45; cursor:not-allowed; }
.pt-btn.danger-out { background:transparent; border-color:hsl(var(--c-danger) / .5); color:hsl(var(--c-danger)); }
.pt-btn.danger-out:hover { background:hsl(var(--c-danger) / .1); }
.pt-btn.danger { background:hsl(var(--c-danger)); color:#fff; }
.pt-btn.danger:hover { filter:brightness(1.04); transform:translateY(-1px); }

/* ─── split ─── */
.pt-split { flex:1; display:grid; grid-template-columns:320px minmax(0,1fr); overflow:hidden; min-height:0; }
.pt-split.has-trace { grid-template-columns:300px minmax(0,1fr) 360px; }
.pt-split.compare { grid-template-columns:300px minmax(0,1fr); }

/* ─── config sidebar ─── */
.pt-config { background:var(--bg-muted); border-right:1px solid var(--border); overflow-y:auto; padding:14px; display:flex; flex-direction:column; gap:12px; min-height:0; }
.pt-cfg-card { background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-lg); padding:13px 14px; }
.pt-cfg-card > h4 { font-family:var(--f-mono); font-size:10px; text-transform:uppercase; letter-spacing:.07em; color:var(--text-muted); margin-bottom:10px; display:flex; align-items:center; gap:6px; }
.pt-cfg-card > h4 .ct { color:var(--text-sec); font-weight:var(--fw-bold); }

/* typology summary */
.pt-tysum .row1 { display:flex; align-items:center; gap:10px; margin-bottom:11px; }
.pt-tyav { width:40px; height:40px; border-radius:50%; flex-shrink:0; background:hsl(var(--c-agent) / .16); color:hsl(var(--c-agent)); display:flex; align-items:center; justify-content:center; font-size:20px; }
.pt-tysum .nm { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-md); }
.pt-tysum .vr { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); margin-top:2px; }
.pt-chipstrip { display:flex; flex-wrap:wrap; gap:5px; margin-bottom:10px; }
.pt-cap { display:inline-flex; align-items:center; gap:4px; padding:3px 8px; border-radius:var(--r-pill); font-family:var(--f-mono); font-size:10px; font-weight:var(--fw-bold);
  background:hsl(var(--c-agent) / .12); color:hsl(var(--c-agent)); }
.pt-cap.off { background:var(--bg-muted); color:var(--text-muted); opacity:.7; }
.pt-gchip { display:inline-flex; align-items:center; gap:5px; padding:3px 9px; border-radius:var(--r-pill); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs);
  background:hsl(var(--c-game) / .14); color:hsl(var(--c-game)); border:1px solid hsl(var(--c-game) / .25); cursor:pointer; }
.pt-cfg-lbl { font-family:var(--f-mono); font-size:9px; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); margin:4px 0 6px; }
.pt-tysum .foot { display:flex; align-items:center; justify-content:space-between; gap:8px; margin-top:11px; padding-top:10px; border-top:1px dashed var(--border); }
.pt-cfg-link { background:none; border:none; cursor:pointer; font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); color:hsl(var(--c-agent)); padding:0; }
.pt-cfg-link:hover { text-decoration:underline; }
.pt-cfg-link.muted { color:var(--text-muted); }

/* sample inputs */
.pt-sample { display:flex; flex-direction:column; gap:7px; padding:10px 11px; border-radius:var(--r-md); background:var(--bg); border:1px solid var(--border-light); cursor:pointer; transition:all var(--dur-sm) var(--ease-out); margin-bottom:6px; }
.pt-sample:hover { background:var(--bg-hover); border-color:hsl(var(--c-agent) / .4); }
.pt-sample:focus-visible { border-color:hsl(var(--c-agent)); }
.pt-sample .q { font-size:var(--fs-sm); color:var(--text); line-height:var(--lh-snug); display:-webkit-box; -webkit-line-clamp:2; -webkit-box-orient:vertical; overflow:hidden; }
.pt-sample .meta { display:flex; align-items:center; gap:6px; }
.pt-sample .tmpl { display:inline-flex; align-items:center; gap:3px; font-family:var(--f-mono); font-size:9px; color:hsl(var(--c-kb)); background:hsl(var(--c-kb) / .1); padding:1px 6px; border-radius:var(--r-pill); }
.pt-sample .fill { margin-left:auto; font-family:var(--f-mono); font-size:9px; color:var(--text-muted); }

/* session config controls */
.pt-field { margin-bottom:11px; }
.pt-field:last-child { margin-bottom:0; }
.pt-selectw { position:relative; }
.pt-select { width:100%; padding:7px 28px 7px 10px; border-radius:var(--r-md); border:1px solid var(--border); background:var(--bg);
  font-family:var(--f-mono); font-weight:var(--fw-bold); font-size:var(--fs-xs); color:var(--text); cursor:pointer; appearance:none; outline:none; }
.pt-selectw::after { content:'▾'; position:absolute; right:11px; top:50%; transform:translateY(-50%); font-size:9px; color:var(--text-muted); pointer-events:none; }
.pt-rangerow { display:flex; align-items:center; gap:10px; }
.pt-range { flex:1; -webkit-appearance:none; appearance:none; height:4px; border-radius:var(--r-pill); background:var(--bg-sunken); outline:none; }
.pt-range::-webkit-slider-thumb { -webkit-appearance:none; appearance:none; width:15px; height:15px; border-radius:50%; background:hsl(var(--c-agent)); cursor:pointer; box-shadow:var(--shadow-xs); }
.pt-range::-moz-range-thumb { width:15px; height:15px; border:none; border-radius:50%; background:hsl(var(--c-agent)); cursor:pointer; }
.pt-rangeval { font-family:var(--f-mono); font-size:var(--fs-xs); font-weight:var(--fw-bold); color:var(--text); min-width:26px; text-align:right; }
.pt-num { width:100%; padding:7px 10px; border-radius:var(--r-md); border:1px solid var(--border); background:var(--bg); font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text); outline:none; }
.pt-num:focus, .pt-select:focus { border-color:hsl(var(--c-agent) / .5); }
.pt-togrow { display:flex; align-items:center; gap:9px; padding:6px 0; }
.pt-togrow .tl { font-size:var(--fs-sm); color:var(--text); flex:1; }
.pt-togrow .ts { font-size:10px; color:var(--text-muted); }
.pt-toggle { width:38px; height:22px; border-radius:var(--r-pill); border:none; background:var(--bg-sunken); position:relative; cursor:pointer; flex-shrink:0; transition:background var(--dur-sm) var(--ease-out); }
.pt-toggle i { position:absolute; top:3px; left:3px; width:16px; height:16px; border-radius:50%; background:#fff; box-shadow:var(--shadow-xs); transition:transform var(--dur-sm) var(--ease-out); }
.pt-toggle.on { background:hsl(var(--c-agent)); }
.pt-toggle.on i { transform:translateX(16px); }
.pt-resetlink { background:none; border:none; cursor:pointer; font-family:var(--f-display); font-weight:var(--fw-bold); font-size:10px; color:hsl(var(--c-warning)); padding:6px 0 0; }
.pt-resetlink:hover { text-decoration:underline; }

/* trace summary mini */
.pt-tracemini { display:flex; align-items:center; gap:9px; padding:11px 13px; }
.pt-tracemini .tx { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); line-height:1.5; flex:1; }
.pt-tracemini .tx b { color:var(--text-sec); }
.pt-tracemini .exp { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:10px; color:hsl(var(--c-agent)); background:hsl(var(--c-agent) / .1); border:none; padding:5px 9px; border-radius:var(--r-sm); cursor:pointer; white-space:nowrap; }

/* ─── chat body ─── */
.pt-chat { display:flex; flex-direction:column; min-height:0; overflow:hidden; background:var(--bg); }
.pt-chathead { flex-shrink:0; display:flex; align-items:center; gap:10px; padding:11px 16px; border-bottom:1px solid var(--border); background:var(--bg-card); }
.pt-session-ind { display:inline-flex; align-items:center; gap:6px; font-family:var(--f-mono); font-size:var(--fs-xs); font-weight:var(--fw-bold); color:hsl(var(--c-success)); }
.pt-session-ind i { width:8px; height:8px; border-radius:50%; background:hsl(var(--c-success)); animation:ptpulse 1.6s var(--ease-in-out) infinite; }
.pt-session-ind.idle { color:var(--text-muted); } .pt-session-ind.idle i { background:var(--text-muted); animation:none; }
.pt-chathead .grow { flex:1; }
.pt-icbtn { width:30px; height:30px; border-radius:var(--r-sm); border:1px solid var(--border); background:var(--bg); color:var(--text-sec); cursor:pointer; font-size:14px; display:inline-flex; align-items:center; justify-content:center; }
.pt-icbtn:hover { background:var(--bg-muted); color:var(--text); }
.pt-icbtn.on { background:hsl(var(--c-agent) / .14); color:hsl(var(--c-agent)); border-color:hsl(var(--c-agent) / .35); }

.pt-msgs { flex:1; overflow-y:auto; padding:18px 20px 8px; min-height:0; display:flex; flex-direction:column; gap:16px; }

/* message row */
.pt-row { display:flex; gap:10px; align-items:flex-start; max-width:760px; width:100%; }
.pt-row.user { flex-direction:row-reverse; margin-left:auto; }
.pt-mav { width:30px; height:30px; border-radius:50%; flex-shrink:0; display:flex; align-items:center; justify-content:center; font-size:14px; font-weight:var(--fw-ext); }
.pt-row.user .pt-mav { background:hsl(var(--c-player)); color:#fff; font-size:10px; }
.pt-row.agent .pt-mav { background:hsl(var(--c-agent) / .16); color:hsl(var(--c-agent)); }
.pt-mcol { min-width:0; display:flex; flex-direction:column; gap:4px; }
.pt-row.user .pt-mcol { align-items:flex-end; }

.pt-bubble { padding:11px 15px; border-radius:var(--r-lg); font-size:var(--fs-md); line-height:var(--lh-body); position:relative; }
.pt-row.user .pt-bubble { background:hsl(var(--c-player) / .14); border:1px solid hsl(var(--c-player) / .3); color:var(--text); border-bottom-right-radius:var(--r-sm); }
.pt-row.agent .pt-bubble { background:hsl(var(--c-agent) / .10); border:1px solid hsl(var(--c-agent) / .25); color:var(--text); border-bottom-left-radius:var(--r-sm); }
.pt-bubble.err { background:hsl(var(--c-danger) / .05); border-color:hsl(var(--c-danger) / .5); }
.pt-bubble b { font-weight:var(--fw-bold); }
.pt-bubble p { margin:0 0 8px; } .pt-bubble p:last-child { margin-bottom:0; }
.pt-streamhint { display:flex; align-items:center; gap:7px; font-family:var(--f-mono); font-size:10px; color:var(--text-muted); margin-bottom:7px; }
.pt-streamhint .tk { color:hsl(var(--c-agent)); font-weight:var(--fw-bold); }

.pt-pre { background:var(--bg-sunken); border:1px solid var(--border); border-radius:var(--r-sm); padding:9px 11px; font-family:var(--f-mono); font-size:var(--fs-sm); line-height:1.5; overflow-x:auto; margin:8px 0; color:var(--text-sec); }
.pt-pre .kw { color:hsl(var(--c-agent)); } .pt-pre .st { color:hsl(var(--c-toolkit)); } .pt-pre .nm { color:hsl(var(--c-chat)); }
.pt-quote { border-left:3px solid hsl(var(--c-kb)); padding:4px 0 4px 12px; margin:8px 0; font-size:var(--fs-sm); font-style:italic; color:var(--text-sec); }
.pt-quote .src { display:block; font-style:normal; font-family:var(--f-mono); font-size:10px; color:hsl(var(--c-kb)); margin-top:4px; }

.pt-cursor { display:inline-block; width:7px; height:16px; background:hsl(var(--c-agent)); margin-left:2px; vertical-align:text-bottom; animation:ptblink .9s step-end infinite; }
@keyframes ptblink { 50% { opacity:0; } }
@keyframes ptpulse { 0%,100%{ opacity:1; transform:scale(1);} 50%{ opacity:.4; transform:scale(.7);} }

.pt-mhead { display:flex; align-items:center; gap:7px; font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.pt-mhead .nm { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); color:var(--text-sec); }
.pt-mhead .writing { color:hsl(var(--c-agent)); font-weight:var(--fw-bold); display:inline-flex; align-items:center; gap:4px; }
.pt-mhead .writing i { width:6px; height:6px; border-radius:50%; background:hsl(var(--c-agent)); animation:ptpulse 1.2s var(--ease-in-out) infinite; }
.pt-mhead .dur { color:var(--text-muted); }
.pt-mhead .errt { color:hsl(var(--c-danger)); font-weight:var(--fw-bold); }
.pt-ts { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }

/* message action footer */
.pt-mact { display:flex; align-items:center; gap:5px; flex-wrap:wrap; margin-top:8px; padding-top:8px; border-top:1px dashed hsl(var(--c-agent) / .2); }
.pt-actbtn { display:inline-flex; align-items:center; gap:4px; padding:3px 8px; border-radius:var(--r-sm); border:1px solid transparent; background:var(--bg-card); color:var(--text-sec);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:10px; cursor:pointer; }
.pt-actbtn:hover { background:var(--bg-muted); color:var(--text); }
.pt-actbtn.on.up { background:hsl(var(--c-success) / .14); color:hsl(var(--c-success)); }
.pt-actbtn.on.down { background:hsl(var(--c-danger) / .14); color:hsl(var(--c-danger)); }
.pt-mact .grow { flex:1; }

/* error footer CTAs */
.pt-errfoot { display:flex; align-items:center; gap:7px; flex-wrap:wrap; margin-top:9px; }
.pt-interrupt-suffix { display:inline-block; margin-top:6px; font-family:var(--f-mono); font-size:10px; color:var(--text-muted); font-style:italic; }

/* system notice */
.pt-notice { align-self:center; display:inline-flex; align-items:center; gap:7px; padding:6px 14px; border-radius:var(--r-pill);
  background:var(--bg-muted); color:var(--text-muted); font-family:var(--f-mono); font-size:var(--fs-xs); }
.pt-notice.warn { background:hsl(var(--c-warning) / .12); color:hsl(var(--c-warning)); }

/* empty / idle state */
.pt-idle { flex:1; display:flex; flex-direction:column; align-items:center; justify-content:center; text-align:center; gap:10px; padding:30px; }
.pt-idle .em { font-size:48px; color:hsl(var(--c-agent)); }
.pt-idle h3 { font-family:var(--f-display); font-size:var(--fs-xl); }
.pt-idle p { color:var(--text-sec); font-size:var(--fs-sm); max-width:340px; line-height:var(--lh-body); }
.pt-idle .quick { display:flex; flex-wrap:wrap; gap:8px; justify-content:center; margin-top:6px; }
.pt-qcta { padding:7px 13px; border-radius:var(--r-pill); border:1px solid hsl(var(--c-agent) / .35); background:hsl(var(--c-agent) / .08); color:hsl(var(--c-agent));
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); cursor:pointer; }
.pt-qcta:hover { background:hsl(var(--c-agent) / .16); }

/* ─── composer ─── */
.pt-composer { flex-shrink:0; border-top:1px solid var(--border); background:var(--bg-card); padding:10px 16px 12px; }
.pt-comp-tools { display:flex; align-items:center; gap:6px; margin-bottom:8px; }
.pt-tool { display:inline-flex; align-items:center; gap:5px; padding:5px 10px; border-radius:var(--r-pill); border:1px solid var(--border); background:var(--bg); color:var(--text-sec);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); cursor:pointer; }
.pt-tool:hover { background:var(--bg-muted); color:var(--text); }
.pt-tool.on { background:hsl(var(--c-chat) / .12); color:hsl(var(--c-chat)); border-color:hsl(var(--c-chat) / .35); }
.pt-comp-tools .grow { flex:1; }
.pt-charcount { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.pt-comp-row { display:flex; align-items:flex-end; gap:9px; }
.pt-ta { flex:1; resize:none; padding:10px 12px; border-radius:var(--r-md); border:1px solid var(--border); background:var(--bg);
  font-family:var(--f-body); font-size:var(--fs-md); line-height:var(--lh-body); color:var(--text); outline:none; min-height:46px; max-height:160px; }
.pt-ta:focus { border-color:hsl(var(--c-agent) / .5); box-shadow:0 0 0 3px hsl(var(--c-agent) / .12); }
.pt-ta:disabled { background:var(--bg-muted); color:var(--text-muted); cursor:not-allowed; }
.pt-comp-fail { display:inline-flex; align-items:center; gap:5px; margin-bottom:8px; padding:3px 9px; border-radius:var(--r-pill); background:hsl(var(--c-danger) / .12); color:hsl(var(--c-danger)); font-family:var(--f-mono); font-size:10px; font-weight:var(--fw-bold); }
.pt-kbd { font-family:var(--f-mono); font-size:9px; background:var(--bg-muted); border:1px solid var(--border); border-bottom-width:2px; border-radius:var(--r-xs); padding:1px 4px; color:var(--text-sec); }

/* ─── trace drawer ─── */
.pt-trace { background:var(--bg-card); border-left:1px solid var(--border); display:flex; flex-direction:column; min-height:0; overflow:hidden; animation:pttracein var(--dur-md) var(--ease-out); }
@keyframes pttracein { from { opacity:0; transform:translateX(12px); } to { opacity:1; transform:none; } }
.pt-trace-head { flex-shrink:0; display:flex; align-items:center; gap:8px; padding:12px 14px; border-bottom:1px solid var(--border); }
.pt-trace-head h3 { font-family:var(--f-display); font-size:var(--fs-md); }
.pt-trace-head .grow { flex:1; }
.pt-trace-body { flex:1; overflow-y:auto; padding:14px 14px 28px; min-height:0; display:flex; flex-direction:column; gap:12px; }
.pt-tblock { border:1px solid var(--border); border-radius:var(--r-md); overflow:hidden; flex-shrink:0; }
.pt-tblock-h { width:100%; display:flex; align-items:center; gap:8px; padding:9px 11px; background:var(--bg-muted); border:none; cursor:pointer; text-align:left;
  font-family:var(--f-mono); font-size:9px; text-transform:uppercase; letter-spacing:.06em; color:var(--text-muted); }
.pt-tblock-h:hover { background:var(--bg-sunken); }
.pt-tblock.open .pt-tblock-h { border-bottom:1px solid var(--border); }
.pt-tblock-h .t { flex:1; font-weight:var(--fw-bold); }
.pt-tblock-h .cv { font-size:8px; color:hsl(var(--c-agent)); transition:transform var(--dur-sm) var(--ease-out); }
.pt-tblock.collapsed .pt-tblock-h .cv { transform:rotate(-90deg); }
.pt-tgrid { display:grid; grid-template-columns:auto 1fr; gap:6px 12px; padding:10px 11px; font-family:var(--f-mono); font-size:11px; }
.pt-tgrid .k { color:var(--text-muted); } .pt-tgrid .v { color:var(--text); font-weight:var(--fw-bold); text-align:right; }
.pt-trow { display:flex; gap:8px; padding:8px 11px; border-top:1px solid var(--border-light); font-size:var(--fs-xs); align-items:flex-start; }
.pt-trow:first-child { border-top:none; }
.pt-trow .ti { flex-shrink:0; }
.pt-trow .tc { min-width:0; }
.pt-trow .tc .l1 { font-family:var(--f-mono); font-size:11px; color:var(--text); word-break:break-word; }
.pt-trow .tc .l2 { font-family:var(--f-mono); font-size:10px; color:var(--text-muted); margin-top:2px; }
.pt-kbsrc { cursor:pointer; } .pt-kbsrc:hover { background:hsl(var(--c-kb) / .07); }
.pt-kbsrc .l1 { color:hsl(var(--c-kb)); }
.pt-score { margin-left:auto; font-family:var(--f-mono); font-size:10px; color:var(--text-muted); flex-shrink:0; }
.pt-twarn { padding:10px 11px; background:hsl(var(--c-warning) / .08); border-top:1px solid hsl(var(--c-warning) / .3); display:flex; gap:8px; font-size:var(--fs-xs); color:hsl(var(--c-warning)); }

/* ─── compare mode ─── */
.pt-compare { flex:1; display:grid; grid-template-columns:1fr 1fr; min-height:0; overflow:hidden; }
.pt-ccol { display:flex; flex-direction:column; min-height:0; overflow:hidden; }
.pt-ccol:first-child { border-right:1px solid var(--border); }
.pt-chead { flex-shrink:0; display:flex; align-items:center; gap:8px; padding:9px 14px; border:none; width:100%; text-align:left; cursor:pointer; color:var(--text); font-family:inherit; background:var(--bg-muted); border-bottom:1px solid var(--border); position:sticky; top:0; z-index:1; }
.pt-chead:hover { background:var(--bg-sunken); }
.pt-cv { margin-left:8px; font-size:9px; color:var(--text-muted); transition:transform var(--dur-sm) var(--ease-out); flex-shrink:0; }
.pt-ccol.collapsed .pt-cv { transform:rotate(-90deg); }
.pt-ccol.collapsed { flex:0 0 auto; }
.pt-chead .ttl { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); }
.pt-cmsgs { flex:1; overflow-y:auto; padding:14px; min-height:0; display:flex; flex-direction:column; gap:13px; }
.pt-cmsgs .pt-row { max-width:none; }

/* ─── mobile ─── */
.pt-app.is-mobile .pt-head .hrow { flex-direction:column; align-items:stretch; gap:9px; padding:11px 13px; }
.pt-app.is-mobile .pt-head .htxt { width:100%; min-width:0; }
.pt-app.is-mobile .pt-head .grow { display:none; }
.pt-app.is-mobile .pt-bread { font-size:10px; }
.pt-app.is-mobile .pt-h1 { font-size:var(--fs-lg); }
.pt-app.is-mobile .pt-sub { font-size:var(--fs-xs); }
.pt-app.is-mobile .pt-headcta { display:none; }
.pt-app.is-mobile .pt-kbdhint { display:none; }
.pt-app.is-mobile .pt-split, .pt-app.is-mobile .pt-split.has-trace, .pt-app.is-mobile .pt-split.compare { display:flex; flex-direction:column; }
.pt-app.is-mobile .pt-chat { flex:1; min-height:0; }
.pt-app.is-mobile .pt-config { display:none; }
.pt-mcfg { display:none; }
.pt-app.is-mobile .pt-mcfg { display:block; flex-shrink:0; border-bottom:1px solid var(--border); background:var(--bg-muted); }
.pt-mcfg-head { display:flex; align-items:center; gap:8px; padding:10px 13px; cursor:pointer; background:none; border:none; width:100%; text-align:left; color:var(--text); }
.pt-mcfg-head:hover { background:var(--bg-sunken); }
.pt-mcfg-head .ttl { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); flex:1; }
.pt-mcfg-head .cv { font-family:var(--f-mono); font-size:10px; color:hsl(var(--c-agent)); font-weight:var(--fw-bold); }
.pt-app.is-mobile .pt-fsbtn { display:none; }
.pt-app.is-mobile .pt-compare { display:flex; flex-direction:column; overflow-y:auto; min-height:0; }
.pt-app.is-mobile .pt-ccol { flex:0 0 auto; overflow:visible; }
.pt-app.is-mobile .pt-cmsgs { overflow:visible; flex:0 0 auto; }
.pt-app.is-mobile .pt-ccol:first-child { border-right:none; border-bottom:1px solid var(--border); }
.pt-app.is-mobile .pt-row { max-width:none; }
.pt-app.is-mobile .pt-composer { padding:9px 12px 11px; }

/* mobile trace bottom-sheet */
.pt-sheet-veil { position:absolute; inset:0; z-index:var(--z-modal); background:rgba(20,12,4,.5); display:flex; align-items:stretch; animation:ptfade var(--dur-md) var(--ease-out); }
@keyframes ptfade { from { opacity:0; } to { opacity:1; } }
.pt-sheet { width:100%; height:100%; max-height:100%; background:var(--bg-card); border-radius:0; box-shadow:var(--shadow-drawer); display:flex; flex-direction:column; overflow:hidden; animation:ptsheet var(--dur-md) var(--ease-out); }
@keyframes ptsheet { from { transform:translateY(24px); opacity:.4; } to { transform:none; opacity:1; } }
.pt-sheet .grab { width:38px; height:4px; border-radius:var(--r-pill); background:var(--border-strong); margin:9px auto 4px; }
.pt-sheet-head { display:flex; align-items:center; gap:8px; padding:4px 14px 10px; border-bottom:1px solid var(--border); flex-shrink:0; }
.pt-sheet-head h3 { font-family:var(--f-display); font-size:var(--fs-md); }
.pt-sheet-head .grow { flex:1; }
.pt-sheet-body { overflow-y:auto; padding:12px 13px 16px; display:flex; flex-direction:column; gap:11px; min-height:0; }
.pt-app.is-mobile .pt-sheet .pt-trace { display:flex; flex:1; min-height:0; border-left:none; animation:none; }

@media (prefers-reduced-motion: reduce) {
  .pt-cursor, .pt-session-ind i, .pt-livechip i, .pt-mhead .writing i, .pt-trace { animation:none; }
}
`;

/* ══════════════════════════════════════════════════════════
   Dati — typology "Catan Rules Expert v3 (Draft)" + sessione test (IT)
   ══════════════════════════════════════════════════════════ */
const TYPOLOGY = {
  id: 'tp-catan-rules-3', name: 'Catan Rules Expert', version: 'v3 (Draft)', icon: '🤖',
  caps: [['Q&A', true], ['Streaming', true], ['Tool', true], ['Image', false], ['Web', true]],
  game: 'Catan', author: { name: 'Marco R.', initials: 'MR' }, model: 'gpt-4o-mini',
};

const SAMPLES = [
  { q: 'Quante carte risorsa pesca ogni giocatore a inizio partita?', tmpl: 'Conteggio + fonte' },
  { q: 'Cosa succede quando esce un 7 con i dadi?', tmpl: 'Regola ladro' },
  { q: 'Come si imposta una partita per 3 giocatori?', tmpl: 'Setup passo-passo' },
];

/* singola risposta agent renderizzata con quote RAG + (opz) code block */
const AGENT_A1 = {
  blocks: [
    { t: 'p', html: 'A inizio partita ogni giocatore riceve le risorse dei terreni adiacenti al <b>secondo</b> insediamento piazzato: <b>una carta risorsa per ogni esagono confinante</b>. Il deserto non produce nulla.' },
    { t: 'quote', html: '«Dopo aver piazzato il secondo insediamento, ciascun giocatore prende una carta risorsa per ogni esagono adiacente a quell’insediamento.»', src: '📄 catan-rules.pdf · p.4 §1.3 · score 0.91' },
    { t: 'p', html: 'Quindi tipicamente <b>3 carte</b>, ma possono essere 2 se l’insediamento tocca un esagono deserto o un porto di mare.' },
  ],
};
const AGENT_A2 = {
  blocks: [
    { t: 'p', html: 'Quando esce <b>7</b> nessun terreno produce risorse. La sequenza è:' },
    { t: 'pre', code: [
      { txt: '1. ', cls: '' }, { txt: 'scarto', cls: 'kw' }, { txt: ' → chi ha >7 carte ne scarta metà (arrot. difetto)\n', cls: '' },
      { txt: '2. ', cls: '' }, { txt: 'ladro', cls: 'kw' }, { txt: '  → il giocatore di turno lo sposta su un esagono\n', cls: '' },
      { txt: '3. ', cls: '' }, { txt: 'furto', cls: 'kw' }, { txt: '  → ruba ', cls: '' }, { txt: '1', cls: 'nm' }, { txt: ' carta a un avversario adiacente', cls: '' },
    ] },
    { t: 'p', html: 'L’esagono col ladro resta bloccato finché un nuovo 7 (o un cavaliere) non lo sposta.' },
  ],
};
const AGENT_A3 = {
  blocks: [{ t: 'p', html: 'Con <b>3 giocatori</b> si usa il tabellone base completo. Ogni giocatore riceve 5 insediamenti, 4 città e 15 strade. Si piazzano 2 insediamenti + 2 strade a testa in ordine, poi a ritroso.' }],
};

const ROUNDS = [
  { id: 'r1', q: SAMPLES[0].q, a: AGENT_A1, ts: '15:38', dur: '1.8s', tokens: 312 },
  { id: 'r2', q: SAMPLES[1].q, a: AGENT_A2, ts: '15:40', dur: '2.3s', tokens: 287 },
  { id: 'r3', q: 'Posso costruire una strada non collegata alle mie?', a: AGENT_A3, ts: '15:41', dur: '1.4s', tokens: 198 },
  { id: 'r4', q: SAMPLES[2].q, a: AGENT_A3, ts: '15:43', dur: '1.9s', tokens: 241 },
];

/* testo parziale per lo stato streaming (typewriter) */
const STREAM_PARTIAL = 'I malus di ferita si cumulano solo con i malus situazionali. Quando esce 7 il ladro va spostato e il giocatore di turno ruba una carta a un avversario adiacente all’esagono';

const TRACE = {
  model: 'gpt-4o-mini', ttft: '240ms', total: '1.8s',
  tokIn: 145, tokOut: 312, tokTot: 457, cost: '$0.0023',
  tools: [{ name: "search_rules(query='Catan distribuzione iniziale')", meta: '180ms · 3 risultati' }],
  rag: [
    { doc: 'catan-rules.pdf · p.4 §1.3', meta: 'Distribuzione iniziale risorse', score: '0.91' },
    { doc: 'catan-faq.md · §setup', meta: 'FAQ ufficiale Kosmos', score: '0.78' },
  ],
  web: [{ q: "'catan setup ufficiale regole'", meta: '→ 5 risultati' }],
};

/* ══════════════════════════════════════════════════════════
   Scenari (12 stati)
   ══════════════════════════════════════════════════════════ */
const SC = {
  'idle-empty':          { chat: 'idle', rounds: 0, fsm: null, input: 'empty', trace: false, compare: false, stopped: false, notice: null, err: null, status: 'Draft' },
  'typing-input':        { chat: 'idle', rounds: 0, fsm: null, input: 'typing', trace: false, compare: false, stopped: false, notice: null, err: null, status: 'Draft' },
  'streaming-active':    { chat: 'active', rounds: 1, fsm: 'streaming', input: 'disabled', trace: false, compare: false, stopped: false, notice: null, err: null, status: 'Draft' },
  'streaming-stop-clicked': { chat: 'active', rounds: 1, fsm: 'stopped', input: 'empty', trace: false, compare: false, stopped: true, notice: null, err: null, status: 'Draft' },
  'completed-single':    { chat: 'active', rounds: 1, fsm: 'completed', input: 'empty', trace: false, compare: false, stopped: false, notice: null, err: null, status: 'Draft' },
  'completed-multi':     { chat: 'active', rounds: 4, fsm: 'completed', input: 'empty', trace: false, compare: false, stopped: false, notice: null, err: null, status: 'Draft' },
  'error-rate-limit':    { chat: 'active', rounds: 1, fsm: 'error', input: 'empty', trace: false, compare: false, stopped: false, notice: null, err: 'rate-limit', status: 'Draft' },
  'error-timeout':       { chat: 'active', rounds: 1, fsm: 'error', input: 'empty', trace: false, compare: false, stopped: false, notice: null, err: 'timeout', status: 'Draft' },
  'error-network':       { chat: 'active', rounds: 2, fsm: 'error', input: 'failed', trace: false, compare: false, stopped: false, notice: 'network', err: 'network', status: 'Draft' },
  'trace-drawer-open':   { chat: 'active', rounds: 1, fsm: 'completed', input: 'empty', trace: true, compare: false, stopped: false, notice: null, err: null, status: 'Draft' },
  'compare-mode':        { chat: 'active', rounds: 1, fsm: 'completed', input: 'empty', trace: false, compare: true, stopped: false, notice: null, err: null, status: 'Draft' },
  'mobile-stack':        { chat: 'active', rounds: 1, fsm: 'completed', input: 'empty', trace: false, compare: false, stopped: false, notice: null, err: null, status: 'Draft' },
};
const STATE_LIST = [
  ['idle-empty', 'Idle empty'], ['typing-input', 'Typing input'], ['streaming-active', 'Streaming'],
  ['streaming-stop-clicked', 'Stop clicked'], ['completed-single', 'Completed'], ['completed-multi', 'Multi-turn'],
  ['error-rate-limit', 'Err · rate limit'], ['error-timeout', 'Err · timeout'], ['error-network', 'Err · network'],
  ['trace-drawer-open', 'Trace drawer'], ['compare-mode', 'Compare'], ['mobile-stack', 'Mobile stack'],
];
const ERR_META = {
  'rate-limit': { title: '⚠ Errore streaming · Rate limit', body: 'Limite di richieste raggiunto sul modello gpt-4o-mini. Riprova tra', countdown: '30s', reason: 'Rate limit reached' },
  'timeout':    { title: '⚠ Errore streaming · Timeout', body: 'Il modello non ha risposto entro 15 secondi (hard timeout). La richiesta è stata interrotta.', countdown: null, reason: 'Model timeout (15s)' },
  'network':    { title: '⚠ Errore streaming · Connessione persa', body: 'Connessione interrotta durante lo streaming. L’ultima richiesta non è stata completata.', countdown: null, reason: 'Network lost' },
};

/* ══════════════════════════════════════════════════════════
   Sub-components
   ══════════════════════════════════════════════════════════ */
function AuthorChip({ who }) {
  return <span className="ce-author" title={'Autore: ' + who.name}><span className="av" aria-hidden="true">{who.initials}</span>{who.name}</span>;
}

function TestHeader({ sc, mobile }) {
  const m = sc.status === 'Rejected' ? { cls: 'rejected', icon: '✕', label: 'Rejected' } : { cls: 'draft', icon: '✎', label: 'Draft' };
  return (
    <header className="pt-head">
      <div className="hrow">
        <div className="htxt">
          <nav className="pt-bread" aria-label="Breadcrumb">
            <span>Editor</span><span className="sep">›</span><span>Agent proposals</span><span className="sep">›</span>
            <span>Catan Rules Expert</span><span className="sep">›</span><span className="cur">Test playground</span>
          </nav>
          <div className="pt-titlerow">
            <h1 className="pt-h1">{TYPOLOGY.icon} {TYPOLOGY.name} · {TYPOLOGY.version}</h1>
            <span className={'ce-badge ' + m.cls} aria-label={'Stato: ' + m.label}><span className="bi" aria-hidden="true">{m.icon}</span>{m.label}</span>
            <span className="pt-livechip" title="Esiste una versione in produzione"><i aria-hidden="true" />v2 live</span>
          </div>
          <div className="pt-sub">Modalità <b>test playground</b> · sessione effimera, non salvata · testato da <AuthorChip who={TYPOLOGY.author} /></div>
        </div>
        <span className="grow" />
        <div className="pt-headcta">
          <button className="pt-btn link"><span className="ic" aria-hidden="true">←</span> Torna a edit</button>
          <button className="pt-btn sec"><span className="ic" aria-hidden="true">🔄</span> Reset session</button>
          <button className="pt-btn info-out"><span className="ic" aria-hidden="true">📊</span> Confronta versioni</button>
        </div>
      </div>
    </header>
  );
}

/* ─── config sidebar sections ─── */
function TypologySummary() {
  return (
    <div className="pt-cfg-card pt-tysum">
      <h4>Typology in test</h4>
      <div className="row1">
        <div className="pt-tyav" aria-hidden="true">{TYPOLOGY.icon}</div>
        <div><div className="nm">{TYPOLOGY.name}</div><div className="vr">{TYPOLOGY.version} · {TYPOLOGY.id}</div></div>
      </div>
      <div className="pt-cfg-lbl">Capabilities</div>
      <div className="pt-chipstrip">
        {TYPOLOGY.caps.map(([c, on]) => <span key={c} className={'pt-cap' + (on ? '' : ' off')}>{on ? '✓' : '–'} {c}</span>)}
      </div>
      <div className="pt-cfg-lbl">Game scope</div>
      <div className="pt-chipstrip"><button className="pt-gchip" title="Apri scheda gioco">🎲 {TYPOLOGY.game}</button></div>
      <div className="foot">
        <AuthorChip who={TYPOLOGY.author} />
        <button className="pt-cfg-link muted">📝 Modifica config</button>
      </div>
    </div>
  );
}

function SampleInputs({ onFill }) {
  return (
    <div className="pt-cfg-card">
      <h4>Esempi rapidi <span className="ct">({SAMPLES.length} disponibili)</span></h4>
      {SAMPLES.map((s, i) => (
        <div key={i} className="pt-sample" role="button" tabIndex={0} onClick={() => onFill(s.q)}
             onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onFill(s.q); } }}
             title="Click per autofill nell’input (non invia)">
          <div className="q">{s.q}</div>
          <div className="meta"><span className="tmpl">▦ {s.tmpl}</span><span className="fill">↧ autofill</span></div>
        </div>
      ))}
      <button className="pt-cfg-link" style={{ marginTop: 4 }}>+ Crea nuovo esempio</button>
    </div>
  );
}

function SessionConfig({ cfg, setCfg }) {
  return (
    <div className="pt-cfg-card">
      <h4>Config sessione</h4>
      <div className="pt-field">
        <div className="pt-cfg-lbl">Model</div>
        <div className="pt-selectw">
          <select className="pt-select" value={cfg.model} onChange={e => setCfg({ ...cfg, model: e.target.value })} aria-label="Modello">
            <option>gpt-4o-mini</option><option>gpt-4o</option><option>claude-3-5-sonnet</option>
          </select>
        </div>
      </div>
      <div className="pt-field">
        <div className="pt-cfg-lbl">Temperature</div>
        <div className="pt-rangerow">
          <input className="pt-range" type="range" min="0" max="1" step="0.1" value={cfg.temp}
                 onChange={e => setCfg({ ...cfg, temp: parseFloat(e.target.value) })} aria-label="Temperature" />
          <span className="pt-rangeval">{cfg.temp.toFixed(1)}</span>
        </div>
      </div>
      <div className="pt-field">
        <div className="pt-cfg-lbl">Max tokens</div>
        <input className="pt-num" type="number" value={cfg.maxTok} onChange={e => setCfg({ ...cfg, maxTok: e.target.value })} aria-label="Max tokens" />
      </div>
      <div className="pt-togrow">
        <div className="tl">Streaming <div className="ts">token-by-token vs full response</div></div>
        <button className={'pt-toggle' + (cfg.streaming ? ' on' : '')} role="switch" aria-checked={cfg.streaming} aria-label="Streaming"
                onClick={() => setCfg({ ...cfg, streaming: !cfg.streaming })}><i /></button>
      </div>
      <div className="pt-togrow">
        <div className="tl">Show trace panel <div className="ts">apre il drawer destro</div></div>
        <button className={'pt-toggle' + (cfg.trace ? ' on' : '')} role="switch" aria-checked={cfg.trace} aria-label="Mostra pannello trace"
                onClick={() => setCfg({ ...cfg, trace: !cfg.trace })}><i /></button>
      </div>
      <button className="pt-resetlink">↺ Ripristina default</button>
    </div>
  );
}

function TraceSummaryMini({ rounds, onOpen }) {
  const msgs = rounds * 2;
  return (
    <div className="pt-cfg-card" style={{ padding: 0 }}>
      <div className="pt-tracemini">
        <div className="tx">Sessione: <b>{msgs} messaggi</b> · <b>12.4k tokens</b> · <b>$0.18</b> stimato</div>
        <button className="exp" onClick={onOpen} title="Apri trace completo">🐛 Trace</button>
      </div>
    </div>
  );
}

function ConfigSidebar({ cfg, setCfg, rounds, onFill, onOpenTrace }) {
  return (
    <aside className="pt-config" aria-label="Configurazione sessione test">
      <TypologySummary />
      <SampleInputs onFill={onFill} />
      <SessionConfig cfg={cfg} setCfg={setCfg} />
      <TraceSummaryMini rounds={rounds} onOpen={onOpenTrace} />
    </aside>
  );
}

/* ─── messages ─── */
function AgentBlocks({ a }) {
  return a.blocks.map((b, i) => {
    if (b.t === 'p') return <p key={i} dangerouslySetInnerHTML={{ __html: b.html }} />;
    if (b.t === 'quote') return <div className="pt-quote" key={i}>{b.html}<span className="src">{b.src}</span></div>;
    if (b.t === 'pre') return <pre className="pt-pre" key={i}>{b.code.map((s, j) => <span key={j} className={s.cls}>{s.txt}</span>)}</pre>;
    return null;
  });
}

function MessageActions({ fb, setFb, onTrace }) {
  return (
    <div className="pt-mact">
      <button className={'pt-actbtn up' + (fb === 'up' ? ' on' : '')} aria-pressed={fb === 'up'} onClick={() => setFb(fb === 'up' ? null : 'up')}>👍 Buono</button>
      <button className={'pt-actbtn down' + (fb === 'down' ? ' on' : '')} aria-pressed={fb === 'down'} onClick={() => setFb(fb === 'down' ? null : 'down')}>👎 Sbagliato</button>
      <button className="pt-actbtn">📋 Copia</button>
      <button className="pt-actbtn">🔄 Rigenera</button>
      <span className="grow" />
      <button className="pt-actbtn" onClick={onTrace}>🐛 Trace</button>
    </div>
  );
}

function UserMessage({ q }) {
  return (
    <div className="pt-row user">
      <div className="pt-mav" aria-hidden="true">{TYPOLOGY.author.initials}</div>
      <div className="pt-mcol">
        <div className="pt-bubble">{q}</div>
        <div className="pt-ts">{TYPOLOGY.author.name} · 15:42</div>
      </div>
    </div>
  );
}

/* typewriter hook per lo stato streaming */
function useTypewriter(text, active, speed) {
  const [n, setN] = useState(active ? 0 : text.length);
  useEffect(() => {
    if (!active) { setN(text.length); return; }
    setN(0);
    let i = 0;
    const id = setInterval(() => { i += 2; setN(i); if (i >= text.length) clearInterval(id); }, speed || 24);
    return () => clearInterval(id);
  }, [text, active, speed]);
  return active ? text.slice(0, n) : text;
}

function StreamingAgent() {
  const shown = useTypewriter(STREAM_PARTIAL, true, 22);
  return (
    <div className="pt-row agent">
      <div className="pt-mav" aria-hidden="true">{TYPOLOGY.icon}</div>
      <div className="pt-mcol" style={{ width: '100%' }}>
        <div className="pt-bubble" aria-busy="true" aria-live="polite">
          <div className="pt-mhead"><span className="nm">{TYPOLOGY.name}</span><span className="writing"><i aria-hidden="true" />⚡ Sta scrivendo…</span></div>
          <div className="pt-streamhint">TTFT <span className="tk">240ms</span> · <span className="tk">23 tokens/s</span></div>
          <p style={{ marginBottom: 0 }}>{shown}<span className="pt-cursor" aria-hidden="true" /></p>
        </div>
      </div>
    </div>
  );
}

function CompletedAgent({ round, stopped, onTrace }) {
  const [fb, setFb] = useState(null);
  return (
    <div className="pt-row agent">
      <div className="pt-mav" aria-hidden="true">{TYPOLOGY.icon}</div>
      <div className="pt-mcol" style={{ width: '100%' }}>
        <div className="pt-bubble" aria-busy="false">
          <div className="pt-mhead"><span className="nm">{TYPOLOGY.name}</span><span>· {round.ts}</span><span className="dur">📊 {round.dur} · {round.tokens} tokens</span></div>
          <div style={{ marginTop: 7 }}><AgentBlocks a={round.a} /></div>
          {stopped && <span className="pt-interrupt-suffix">[Interrotto dall’utente]</span>}
          <MessageActions fb={fb} setFb={setFb} onTrace={onTrace} />
        </div>
      </div>
    </div>
  );
}

function ErrorAgent({ err, onTrace }) {
  const m = ERR_META[err];
  return (
    <div className="pt-row agent">
      <div className="pt-mav" aria-hidden="true">{TYPOLOGY.icon}</div>
      <div className="pt-mcol" style={{ width: '100%' }}>
        <div className="pt-bubble err" role="alert">
          <div className="pt-mhead"><span className="nm">{TYPOLOGY.name}</span><span className="errt">{m.title}</span></div>
          <p style={{ marginTop: 7 }}>{m.body}{m.countdown && <b> {m.countdown}</b>}{m.countdown && '.'}</p>
          <div className="pt-errfoot">
            <button className="pt-btn danger"><span className="ic" aria-hidden="true">🔄</span> Riprova</button>
            <button className="pt-btn danger-out" onClick={onTrace}><span className="ic" aria-hidden="true">🐛</span> Trace error</button>
            <span className="pt-ts" style={{ marginLeft: 'auto' }}>reason: {m.reason}</span>
          </div>
        </div>
      </div>
    </div>
  );
}

function IdleBody({ onFill }) {
  return (
    <div className="pt-idle" role="status">
      <div className="em" aria-hidden="true">{TYPOLOGY.icon}</div>
      <h3>👋 Invia un messaggio per testare l’agent</h3>
      <p>Usa gli esempi nella sidebar per partire velocemente, oppure scrivi una domanda di test qui sotto.</p>
      <div className="quick">
        <button className="pt-qcta" onClick={() => onFill('Quali sono le regole base di una domanda?')}>Domanda regole</button>
        <button className="pt-qcta" onClick={() => onFill('Dammi un consiglio strategico per l’apertura.')}>Strategy advice</button>
        <button className="pt-qcta" onClick={() => onFill('Come si imposta la partita passo-passo?')}>Setup tutorial</button>
      </div>
    </div>
  );
}

/* ─── chat body ─── */
function ChatHeader({ sc, onFullscreen, onTrace }) {
  const active = sc.chat === 'active';
  const msgs = sc.rounds * 2 + (sc.fsm === 'streaming' || sc.fsm === 'error' ? 1 : 0);
  return (
    <div className="pt-chathead">
      <span className={'pt-session-ind' + (active ? '' : ' idle')}>
        <i aria-hidden="true" />{active ? `🟢 Sessione attiva · ${msgs} messaggi` : 'Sessione non avviata'}
      </span>
      <span className="grow" />
      <button className="pt-icbtn" aria-label="Apri trace" title="Trace" onClick={onTrace}>🐛</button>
      <button className="pt-icbtn pt-fsbtn" aria-label="Schermo intero — nascondi sidebar" title="Schermo intero" onClick={onFullscreen}>⛶</button>
    </div>
  );
}

function MessageList({ sc, onTrace }) {
  if (sc.chat === 'idle') return <div className="pt-msgs"><IdleBody onFill={() => {}} /></div>;
  const visible = ROUNDS.slice(0, sc.rounds);
  return (
    <div className="pt-msgs" role="log" aria-live="polite" aria-relevant="additions" aria-label="Messaggi sessione test">
      {visible.map((r, i) => {
        const isLast = i === visible.length - 1;
        return (
          <React.Fragment key={r.id}>
            <UserMessage q={r.q} />
            {!(isLast && (sc.fsm === 'streaming' || sc.fsm === 'error'))
              ? <CompletedAgent round={r} stopped={isLast && sc.stopped} onTrace={onTrace} />
              : null}
          </React.Fragment>
        );
      })}
      {sc.fsm === 'streaming' && <StreamingAgent />}
      {sc.notice === 'network' && <div className="pt-notice warn" role="status">⚠️ Connessione persa, riconnessione…</div>}
      {sc.fsm === 'error' && <ErrorAgent err={sc.err} onTrace={onTrace} />}
    </div>
  );
}

function Composer({ sc, draft, setDraft, onFullWidthStop }) {
  const disabled = sc.input === 'disabled';
  const streaming = sc.fsm === 'streaming';
  const failed = sc.input === 'failed';
  const count = draft.length;
  const caps = TYPOLOGY.caps;
  const hasImg = caps.find(c => c[0] === 'Image')[1];
  const hasWeb = caps.find(c => c[0] === 'Web')[1];
  return (
    <div className="pt-composer">
      {failed && <div className="pt-comp-fail">⚠ Ultima richiesta fallita</div>}
      <div className="pt-comp-tools">
        <button className="pt-tool" disabled={disabled}>🎴 Sample</button>
        <button className="pt-tool" disabled={disabled || !hasImg} title={hasImg ? 'Allega immagine' : 'Image capability disabilitata'}>📎 Allega</button>
        <button className={'pt-tool' + (hasWeb ? '' : '')} disabled={disabled || !hasWeb} title={hasWeb ? 'Web search' : 'Web capability disabilitata'}>🌐 Web</button>
        <span className="grow" />
        <span className="pt-charcount">{count} / 4000</span>
      </div>
      <div className="pt-comp-row">
        <textarea className="pt-ta" rows={sc.input === 'typing' ? 5 : 3} value={draft} disabled={disabled}
                  onChange={e => setDraft(e.target.value)} aria-label="Messaggio di test" aria-multiline="true"
                  placeholder="Scrivi un messaggio di test… (Ctrl+Enter per inviare)" />
        {streaming
          ? <button className="pt-btn danger-out" onClick={onFullWidthStop} aria-label="Interrompi streaming"><span className="ic" aria-hidden="true">⏹</span> Interrompi</button>
          : <button className="pt-btn agent" disabled={disabled || count === 0} aria-label="Invia messaggio di test">Invia <span className="ic" aria-hidden="true">→</span></button>}
      </div>
      <div className="pt-kbdhint" style={{ marginTop: 7 }}>
        <span className="pt-charcount"><span className="pt-kbd">Ctrl</span> <span className="pt-kbd">↵</span> invia · <span className="pt-kbd">Esc</span> interrompi · <span className="pt-kbd">Ctrl</span> <span className="pt-kbd">T</span> trace</span>
      </div>
    </div>
  );
}

function ChatBody({ sc, draft, setDraft, onTrace, onFullscreen }) {
  return (
    <section className="pt-chat" aria-label="Chat di test">
      <ChatHeader sc={sc} onFullscreen={onFullscreen} onTrace={onTrace} />
      <MessageList sc={sc} onTrace={onTrace} />
      <Composer sc={sc} draft={draft} setDraft={setDraft} onFullWidthStop={() => {}} />
    </section>
  );
}

/* ─── trace drawer ─── */
function TBlock({ title, count, defaultOpen, children }) {
  const [open, setOpen] = useState(defaultOpen !== false);
  return (
    <div className={'pt-tblock' + (open ? ' open' : ' collapsed')}>
      <button className="pt-tblock-h" aria-expanded={open} onClick={() => setOpen(o => !o)}>
        <span className="t">{title}{count != null ? ' · ' + count : ''}</span>
        <span className="cv" aria-hidden="true">▼</span>
      </button>
      {open && <div className="pt-tblock-c">{children}</div>}
    </div>
  );
}

function TraceDrawer({ onClose }) {
  return (
    <aside className="pt-trace" role="complementary" aria-labelledby="pt-trace-title">
      <div className="pt-trace-head">
        <h3 id="pt-trace-title">🐛 Trace</h3>
        <span className="pt-ts">msg #2 · agent</span>
        <span className="grow" />
        <button className="pt-icbtn" aria-label="Chiudi trace" onClick={onClose}>✕</button>
      </div>
      <div className="pt-trace-body">
        <TBlock title="Modello & latenza">
          <div className="pt-tgrid">
            <span className="k">Model</span><span className="v">{TRACE.model}</span>
            <span className="k">TTFT</span><span className="v">{TRACE.ttft}</span>
            <span className="k">Total</span><span className="v">{TRACE.total}</span>
          </div>
        </TBlock>
        <TBlock title="Tokens & costo">
          <div className="pt-tgrid">
            <span className="k">Input</span><span className="v">{TRACE.tokIn}</span>
            <span className="k">Output</span><span className="v">{TRACE.tokOut}</span>
            <span className="k">Total</span><span className="v">{TRACE.tokTot}</span>
            <span className="k">Cost</span><span className="v">{TRACE.cost}</span>
          </div>
        </TBlock>
        <TBlock title="Tool calls" count={TRACE.tools.length}>
          {TRACE.tools.map((t, i) => (
            <div className="pt-trow" key={i}><span className="ti">🔧</span><div className="tc"><div className="l1">{t.name}</div><div className="l2">{t.meta}</div></div></div>
          ))}
        </TBlock>
        <TBlock title="RAG sources" count={TRACE.rag.length}>
          {TRACE.rag.map((r, i) => (
            <div className="pt-trow pt-kbsrc" key={i} role="button" tabIndex={0} title="Apri PDF preview"><span className="ti">📄</span>
              <div className="tc"><div className="l1">{r.doc}</div><div className="l2">{r.meta}</div></div><span className="pt-score">score {r.score}</span></div>
          ))}
        </TBlock>
        <TBlock title="Web searches" count={TRACE.web.length}>
          {TRACE.web.map((w, i) => (
            <div className="pt-trow" key={i}><span className="ti">🌐</span><div className="tc"><div className="l1">{w.q}</div><div className="l2">{w.meta}</div></div></div>
          ))}
        </TBlock>
        <TBlock title="Warnings" defaultOpen={false}>
          <div className="pt-twarn">⚠ Output vicino al limite max_tokens (312/2048 · 15%). Considera un prompt più conciso.</div>
        </TBlock>
      </div>
    </aside>
  );
}

/* ─── compare mode ─── */
function CompareColumn({ title, badgeCls, badge, round, defaultOpen }) {
  const [open, setOpen] = useState(defaultOpen !== false);
  return (
    <div className={'pt-ccol' + (open ? '' : ' collapsed')}>
      <button className="pt-chead" aria-expanded={open} onClick={() => setOpen(o => !o)} title={open ? 'Comprimi' : 'Espandi'}>
        <span className="pt-tyav" style={{ width: 26, height: 26, fontSize: 14 }} aria-hidden="true">{TYPOLOGY.icon}</span>
        <span className="ttl">{title}</span>
        <span className={'ce-badge ' + badgeCls} style={{ marginLeft: 'auto', padding: '2px 8px', fontSize: 11 }}>{badge}</span>
        <span className="pt-cv" aria-hidden="true">▼</span>
      </button>
      {open && (
      <div className="pt-cmsgs">
        <div className="pt-row agent">
          <div className="pt-mav" aria-hidden="true">{TYPOLOGY.icon}</div>
          <div className="pt-mcol" style={{ width: '100%' }}>
            <div className="pt-bubble">
              <div className="pt-mhead"><span className="nm">{title}</span><span className="dur">📊 {round.dur} · {round.tokens} tok</span></div>
              <div style={{ marginTop: 7 }}><AgentBlocks a={round.a} /></div>
            </div>
          </div>
        </div>
      </div>
      )}
    </div>
  );
}

function CompareMode({ draft, setDraft, mobile }) {
  return (
    <>
      <div className="pt-chathead">
        <span className="pt-session-ind"><i aria-hidden="true" />⚖ Compare mode · stesso prompt → 2 versioni</span>
        <span className="grow" />
        <button className="pt-icbtn on" aria-label="Esci da compare" title="Chiudi confronto">✕</button>
      </div>
      <div className="pt-compare">
        <CompareColumn title="v3 (Draft)" badgeCls="draft" badge="✎ Draft" round={ROUNDS[0]} defaultOpen={true} />
        <CompareColumn title="v2 (Approved)" badgeCls="" badge="✓ Live" round={ROUNDS[3]} defaultOpen={!mobile} />
      </div>
      <Composer sc={SC['completed-single']} draft={draft} setDraft={setDraft} onFullWidthStop={() => {}} />
    </>
  );
}

/* ══════════════════════════════════════════════════════════
   TestApp — un'istanza per (state × viewport). Remount via key.
   ══════════════════════════════════════════════════════════ */
function TestApp({ stateId, mobile }) {
  const sc = SC[stateId];
  const [cfg, setCfg] = useState({ model: TYPOLOGY.model, temp: 0.7, maxTok: 2048, streaming: true, trace: sc.trace });
  const [draft, setDraft] = useState(sc.input === 'typing' ? 'Se ho 2 ferite, il malus −2 si cumula con quello del ladro o no?' : '');
  const [traceOpen, setTraceOpen] = useState(sc.trace);
  const [mcfgOpen, setMcfgOpen] = useState(false);
  const [sheet, setSheet] = useState(mobile && sc.trace);

  const openTrace = () => { if (mobile) setSheet(true); else { setTraceOpen(true); setCfg(c => ({ ...c, trace: true })); } };
  const closeTrace = () => { setTraceOpen(false); setCfg(c => ({ ...c, trace: false })); };
  const showTrace = !mobile && traceOpen && !sc.compare;

  const splitCls = 'pt-split' + (sc.compare ? ' compare' : showTrace ? ' has-trace' : '');

  return (
    <div className={'pt-app' + (mobile ? ' is-mobile' : '')}>
      <TestHeader sc={sc} mobile={mobile} />

      {/* mobile: config come bottom-sheet (non spinge/collassa la chat) */}
      <div className="pt-mcfg">
        <button className="pt-mcfg-head" aria-expanded={mcfgOpen} aria-haspopup="dialog" onClick={() => setMcfgOpen(true)}>
          <span aria-hidden="true">⚙️</span><span className="ttl">Config sessione · {TYPOLOGY.model}</span><span className="cv" aria-hidden="true">▸ apri</span>
        </button>
      </div>

      <div className={splitCls}>
        {!mobile && !sc.compare && (
          <ConfigSidebar cfg={cfg} setCfg={setCfg} rounds={Math.max(sc.rounds, 1)} onFill={setDraft} onOpenTrace={openTrace} />
        )}
        {!mobile && sc.compare && (
          <ConfigSidebar cfg={cfg} setCfg={setCfg} rounds={Math.max(sc.rounds, 1)} onFill={setDraft} onOpenTrace={openTrace} />
        )}

        {sc.compare
          ? <section className="pt-chat" aria-label="Confronto versioni"><CompareMode draft={draft} setDraft={setDraft} mobile={mobile} /></section>
          : <ChatBody sc={sc} draft={draft} setDraft={setDraft} onTrace={openTrace} onFullscreen={() => {}} />}

        {showTrace && <TraceDrawer onClose={closeTrace} />}
      </div>

      {mobile && mcfgOpen && (
        <div className="pt-sheet-veil" onClick={() => setMcfgOpen(false)}>
          <div className="pt-sheet" role="dialog" aria-label="Config sessione" onClick={e => e.stopPropagation()}>
            <div className="grab" />
            <div className="pt-sheet-head">
              <h3>⚙️ Config sessione</h3>
              <span className="grow" />
              <button className="pt-icbtn" aria-label="Chiudi config" onClick={() => setMcfgOpen(false)}>✕</button>
            </div>
            <div className="pt-sheet-body">
              <TypologySummary />
              <SampleInputs onFill={(q) => { setDraft(q); setMcfgOpen(false); }} />
              <SessionConfig cfg={cfg} setCfg={setCfg} />
            </div>
          </div>
        </div>
      )}

      {mobile && sheet && (
        <div className="pt-sheet-veil" onClick={() => setSheet(false)}>
          <div className="pt-sheet" role="dialog" aria-label="Trace dettaglio" onClick={e => e.stopPropagation()}>
            <div className="grab" />
            <TraceDrawer onClose={() => setSheet(false)} />
          </div>
        </div>
      )}
    </div>
  );
}

/* ══════════════════════════════════════════════════════════
   Harness — continuity con S1/S2/S3/S4
   ══════════════════════════════════════════════════════════ */
function Harness() {
  const [stateId, setStateId] = useState(() => localStorage.getItem('pt-state') || 'completed-single');
  const [theme, setTheme] = useState(() => localStorage.getItem('mai-theme') || 'light');

  useEffect(() => { document.documentElement.setAttribute('data-theme', theme); localStorage.setItem('mai-theme', theme); }, [theme]);
  useEffect(() => { localStorage.setItem('pt-state', stateId); }, [stateId]);

  return (
    <div className="ed-stage">
      <style dangerouslySetInnerHTML={{ __html: PT_CSS }} />
      <button className="theme-toggle" onClick={() => setTheme(theme === 'light' ? 'dark' : 'light')}>🌗 <span>{theme === 'dark' ? 'Dark' : 'Light'}</span></button>

      <div className="ed-wrap">
        <div className="ed-kicker">SP4 · B14 · #1489 — schermata 5 / 5 · test playground (ultima del cluster)</div>
        <h1>Test <span className="acc">playground</span> — /editor/agent-proposals/[id]/test</h1>
        <p className="ed-lead">
          Playground streaming per testare una <b>typology proposal</b> PRIMA del submit. L’editor invia messaggi di test
          all’agent (configurato con system prompt + capabilities dalla typology) e vede la risposta in tempo reale.
          Split-view asimmetrico: <code>config sidebar 320px</code> a sinistra + <code>chat body</code> a destra, con trace
          drawer opzionale. Sessione <b>effimera</b>, non salvata. FSM streaming a 4 stati con cursor blink animato.
        </p>

        <div className="ed-notes">
          <div className="ed-note">
            <h4>FSM streaming · 4 stati</h4>
            <p><b>idle</b> empty body + CTA · <b>streaming</b> typewriter + cursor <code>▎</code> blink + “Interrompi” · <b>completed</b> footer azioni + trace · <b>error</b> bubble bordo rosso + retry.</p>
          </div>
          <div className="ed-note">
            <h4>12 stati · trace + compare</h4>
            <p>Selettore qui sotto. Trace drawer (380px) con model/latency/tokens/cost/tool/RAG/web. <b>Compare mode</b>: v3 Draft vs v2 Approved, stesso prompt a due colonne.</p>
          </div>
          <div className="ed-note">
            <h4>Mobile & a11y</h4>
            <p>Mobile = config in <b>drawer top collassabile</b>, chat full-width, trace in <b>bottom-sheet</b>. <code>role=log</code> + <code>aria-live</code> sui messaggi, <code>aria-busy</code> sullo streaming, <code>role=alert</code> sugli errori.</p>
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

        <div className="ed-vp-label">Desktop · 1440 — split-view config 320 / chat fluid / trace 380</div>
        <div className="ed-desk">
          <div className="ed-chrome">
            <div className="dots"><i /><i /><i /></div>
            <div className="url">meepleai.app/editor/agent-proposals/{TYPOLOGY.id}/test</div>
          </div>
          <div style={{ flex: 1, minHeight: 0 }}>
            <TestApp key={'d-' + stateId} stateId={stateId} mobile={false} />
          </div>
        </div>

        <div className="ed-vp-label">Mobile · 375 — config in drawer top, chat full-width, trace bottom-sheet</div>
        <div className="ed-phone-row">
          <div className="phone">
            <div className="phone-sbar"><span>9:41</span><span className="ind">●●● 5G ▮</span></div>
            <div style={{ flex: 1, minHeight: 0, display: 'flex' }}>
              <TestApp key={'m-' + stateId} stateId={stateId} mobile={true} />
            </div>
          </div>
          <div className="ed-phone-cap">
            <h4>Layout mobile</h4>
            <p>La config sidebar collassa in un <b>drawer in alto</b> (tocca <code>⚙️ Config sessione</code> per espandere typology, esempi e parametri). La chat occupa tutta la larghezza; il <code>🐛 Trace</code> apre un <b>bottom-sheet</b> invece del drawer laterale.</p>
          </div>
        </div>
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<Harness />);
