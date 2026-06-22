/* sp4-editor-proposals-create.jsx
   Route: /editor/agent-proposals/create — Crea nuova typology AI agent proposal
   B14 (issue #1489) · screen 3 of 5 · Tier M
   Pattern: Form full-page (NO drawer) — 5 sezioni accordion stacked verticalmente, max-width 880 centered.
            Single-page (NON wizard): l'editor power-user vede tutto il context insieme, salva draft
            parziale, salta fra sezioni. Riusa pattern field di auth-flow.jsx (label + input + error/hint).
   Continuity con S1+S2: stesso state-picker UI, theme toggle 🌗 (mai-theme), desktop frame chrome
            (.ed-desk), phone-row mobile. Entity primaria = --c-agent. CTA primario --c-agent.
   Loadable standalone via Babel. Injects own component CSS; relies on tokens.css + components.css.

   v2 components surfaced here:
   /* v2: ProposalCreateForm, FormHeader, FormFooter, SaveStatusPill, SectionCard, ValidationPip,
          NameField, UniqueCheck, CharCounter, AutoTextarea, IconPicker, ColorPicker, CapabilityGrid,
          CodeEditor (highlight {var}), PromptToolbar, VarHint, SampleRows, GameScopePicker,
          ScopeSummary, ConfirmCancelModal, SubmitReviewModal, DraftSavedToast, SubmitSuccessBanner,
          AuthorChip, EntityChip (game) */

const { useState, useEffect, useMemo, useRef, useCallback } = React;

/* ──────────────────────────────────────────────────────────
   Component CSS — solo token da tokens.css / components.css.
   .ed-* = harness chrome riusato da S1/S2 (continuity). .cp-* = create-proposal.
   ────────────────────────────────────────────────────────── */
const CP_CSS = `
/* ─── harness (riuso esatto da sp4-editor-proposals-index S2) ─── */
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
.ed-desk { width:100%; max-width:1340px; height:820px; border-radius:var(--r-lg); overflow:hidden;
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

/* ─── form app shell ─── */
.cp-app { display:flex; flex-direction:column; height:100%; min-height:0; background:var(--bg); color:var(--text); position:relative; overflow:hidden; }
.cp-app :focus-visible { outline:2px solid hsl(var(--c-agent)); outline-offset:2px; border-radius:var(--r-xs); }

/* success banner (top) */
.cp-banner { flex-shrink:0; display:flex; align-items:center; gap:12px; padding:13px 22px; font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm);
  background:hsl(var(--c-success) / .15); color:hsl(var(--c-success)); border-bottom:1px solid hsl(var(--c-success) / .35); }
.cp-banner .grow { flex:1; }
.cp-banner .gocta { display:inline-flex; align-items:center; gap:6px; padding:7px 14px; border-radius:var(--r-md); border:none;
  background:hsl(var(--c-success)); color:#06250f; font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); cursor:pointer; }
[data-theme="dark"] .cp-banner .gocta { color:#06250f; }

/* header (sticky) */
.cp-head { flex-shrink:0; position:sticky; top:0; z-index:var(--z-sticky); background:var(--bg-card); border-bottom:1px solid var(--border); }
.cp-head .hrow { display:flex; align-items:flex-end; gap:16px; padding:14px 22px 15px; max-width:980px; margin:0 auto; }
.cp-head .htxt { min-width:0; }
.cp-bread { font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); letter-spacing:.04em; display:flex; align-items:center; gap:6px; margin-bottom:6px; }
.cp-bread .sep { opacity:.5; }
.cp-bread .cur { color:hsl(var(--c-agent)); font-weight:var(--fw-bold); }
.cp-h1 { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-2xl); letter-spacing:-.01em; line-height:var(--lh-tight); }
.cp-sub { font-size:var(--fs-sm); color:var(--text-sec); margin-top:4px; max-width:560px; }
.cp-author { display:inline-flex; align-items:center; gap:6px; margin-top:9px; padding:3px 10px 3px 3px; border-radius:var(--r-pill);
  background:hsl(var(--c-player) / .14); color:hsl(var(--c-player)); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); }
.cp-author .av { width:18px; height:18px; border-radius:50%; background:hsl(var(--c-player)); color:#fff; display:flex; align-items:center; justify-content:center; font-size:9px; font-weight:var(--fw-ext); }
.cp-author .lb { color:var(--text-muted); font-weight:var(--fw-semi); }
.cp-head .grow { flex:1; }
.cp-headcta { display:flex; align-items:center; gap:9px; flex-shrink:0; }

/* generic buttons */
.cp-btn { display:inline-flex; align-items:center; gap:7px; padding:9px 15px; border-radius:var(--r-md); border:1px solid transparent;
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); cursor:pointer; transition:all var(--dur-sm) var(--ease-out); white-space:nowrap; }
.cp-btn .ic { font-size:13px; line-height:1; }
.cp-btn.link { background:transparent; color:var(--text-muted); padding:9px 8px; }
.cp-btn.link:hover { color:var(--text); }
.cp-btn.ghost { background:var(--bg-card); border-color:var(--border); color:var(--text-sec); }
.cp-btn.ghost:hover { border-color:var(--border-strong); color:var(--text); transform:translateY(-1px); }
.cp-btn.primary { background:hsl(var(--c-agent)); color:#3a2400; box-shadow:var(--shadow-xs); }
.cp-btn.primary:hover:not(:disabled) { transform:translateY(-1px); box-shadow:var(--shadow-sm); filter:brightness(1.03); }
.cp-btn.primary:disabled { opacity:.45; cursor:not-allowed; }
.cp-btn.danger { background:hsl(var(--c-danger)); color:#fff; }
.cp-btn.danger:hover { filter:brightness(1.04); transform:translateY(-1px); }

/* save pill bar (above footer) */
.cp-savebar { flex-shrink:0; display:flex; justify-content:flex-end; padding:7px 22px; background:var(--bg); }
.cp-savebar .inner { max-width:980px; width:100%; margin:0 auto; display:flex; justify-content:flex-end; }
.cp-pill { display:inline-flex; align-items:center; gap:6px; padding:4px 11px; border-radius:var(--r-pill);
  font-family:var(--f-mono); font-size:var(--fs-xs); font-weight:var(--fw-bold); }
.cp-pill.unsaved { background:hsl(var(--c-warning) / .14); color:hsl(var(--c-warning)); }
.cp-pill.saved   { background:hsl(var(--c-success) / .14); color:hsl(var(--c-success)); }
.cp-pill.saving  { background:hsl(var(--c-warning) / .14); color:hsl(var(--c-warning)); }
.cp-pill .dot { width:7px; height:7px; border-radius:50%; background:currentColor; }
.cp-pill.saving .dot { animation:cppulse 1s var(--ease-in-out) infinite; }
@keyframes cppulse { 0%,100%{ opacity:1; transform:scale(1);} 50%{ opacity:.3; transform:scale(.55);} }

/* footer (sticky bottom) */
.cp-foot { flex-shrink:0; background:var(--bg-card); border-top:1px solid var(--border); }
.cp-foot .frow { display:flex; align-items:center; gap:10px; padding:13px 22px; max-width:980px; margin:0 auto; }
.cp-foot .grow { flex:1; }

/* scroll body + form column */
.cp-body { flex:1; overflow:auto; min-height:0; padding:24px 22px 32px; }
.cp-form { max-width:880px; margin:0 auto; display:flex; flex-direction:column; gap:16px; }

/* section card (accordion) */
.cp-sec { background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-lg); overflow:hidden; transition:border-color var(--dur-sm) var(--ease-out); }
.cp-sec.has-error { border-color:hsl(var(--c-danger) / .4); }
.cp-sec.is-valid { border-color:hsl(var(--c-toolkit) / .32); }
.cp-sechead { display:flex; align-items:center; gap:13px; width:100%; padding:15px 18px; background:transparent; border:none; text-align:left; cursor:pointer; }
.cp-sechead:hover { background:var(--bg-hover); }
.cp-secnum { flex-shrink:0; width:26px; height:26px; border-radius:var(--r-sm); display:flex; align-items:center; justify-content:center;
  background:hsl(var(--c-agent) / .14); color:hsl(var(--c-agent)); font-family:var(--f-mono); font-weight:var(--fw-ext); font-size:var(--fs-sm); }
.cp-sectitle { min-width:0; flex:1; }
.cp-sectitle .t { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-lg); line-height:var(--lh-tight); display:flex; align-items:center; gap:8px; }
.cp-sectitle .t .opt { font-family:var(--f-mono); font-size:var(--fs-xs); font-weight:var(--fw-bold); color:var(--text-muted); text-transform:uppercase; letter-spacing:.04em;
  background:var(--bg-muted); padding:2px 7px; border-radius:var(--r-pill); }
.cp-sectitle .s { font-size:var(--fs-sm); color:var(--text-sec); margin-top:3px; }
.cp-vpip { flex-shrink:0; display:inline-flex; align-items:center; gap:5px; padding:3px 10px; border-radius:var(--r-pill);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); white-space:nowrap; }
.cp-vpip.valid { background:hsl(var(--c-toolkit) / .14); color:hsl(var(--c-toolkit)); }
.cp-vpip.issues { background:hsl(var(--c-danger) / .14); color:hsl(var(--c-danger)); }
.cp-vpip.incomplete { background:var(--bg-muted); color:var(--text-muted); }
.cp-vpip.optional { background:var(--bg-muted); color:var(--text-muted); }
.cp-caret { flex-shrink:0; color:var(--text-muted); font-size:11px; transition:transform var(--dur-sm) var(--ease-out); width:16px; text-align:center; }
.cp-sec.open .cp-caret { transform:rotate(90deg); }
.cp-secbody { padding:4px 18px 20px; border-top:1px solid var(--border-light); }

/* fields (auth-flow heritage) */
.cp-field { margin-bottom:16px; }
.cp-field:last-child { margin-bottom:0; }
.cp-label { display:flex; align-items:center; gap:6px; font-family:var(--f-display); font-size:var(--fs-xs); font-weight:var(--fw-bold);
  color:var(--text-sec); margin-bottom:7px; text-transform:uppercase; letter-spacing:.05em; }
.cp-label .req { color:hsl(var(--c-danger)); }
.cp-label .grow { flex:1; }
.cp-counter { font-family:var(--f-mono); font-size:10px; font-weight:var(--fw-bold); color:var(--text-muted); text-transform:none; letter-spacing:0; }
.cp-counter.over { color:hsl(var(--c-danger)); }
.cp-inwrap { position:relative; }
.cp-input, .cp-textarea { width:100%; padding:10px 12px; border-radius:var(--r-md); border:1.5px solid var(--border);
  background:var(--bg-card); font-family:var(--f-body); font-size:var(--fs-base); color:var(--text); outline:none; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.cp-input:focus, .cp-textarea:focus { border-color:hsl(var(--c-agent) / .55); box-shadow:0 0 0 3px hsl(var(--c-agent) / .14); }
.cp-input.err, .cp-textarea.err { border-color:hsl(var(--c-danger) / .6); }
.cp-input.err:focus, .cp-textarea.err:focus { box-shadow:0 0 0 3px hsl(var(--c-danger) / .14); }
.cp-input[readonly], .cp-textarea[readonly] { background:var(--bg-muted); color:var(--text-sec); cursor:default; }
.cp-input.haspad { padding-right:110px; }
.cp-textarea { resize:none; line-height:var(--lh-body); min-height:74px; }
.cp-unique { position:absolute; right:11px; top:50%; transform:translateY(-50%); display:inline-flex; align-items:center; gap:5px;
  font-family:var(--f-mono); font-size:10px; font-weight:var(--fw-bold); white-space:nowrap; }
.cp-unique.checking { color:hsl(var(--c-info)); }
.cp-unique.ok { color:hsl(var(--c-toolkit)); }
.cp-unique.dup { color:hsl(var(--c-danger)); }
.cp-unique .sp { width:11px; height:11px; border:2px solid currentColor; border-right-color:transparent; border-radius:50%; animation:cpspin .7s linear infinite; }
@keyframes cpspin { to { transform:rotate(360deg); } }
.cp-err { display:flex; align-items:center; gap:6px; font-size:var(--fs-xs); color:hsl(var(--c-danger)); font-weight:var(--fw-semi); margin-top:6px; }
.cp-hint { font-size:var(--fs-xs); color:var(--text-muted); margin-top:6px; line-height:var(--lh-snug); }

/* tip / help box */
.cp-tip { display:flex; align-items:flex-start; gap:9px; margin-top:14px; padding:10px 13px; border-radius:var(--r-md);
  background:hsl(var(--c-info) / .08); border:1px solid hsl(var(--c-info) / .18); }
.cp-tip .ti { font-size:14px; line-height:1.3; flex-shrink:0; }
.cp-tip .tt { font-size:var(--fs-sm); color:var(--text-sec); line-height:var(--lh-snug); }
.cp-tip .tt b { color:var(--text); font-weight:var(--fw-bold); }
.cp-tip .tt code { font-family:var(--f-mono); font-size:11px; background:var(--bg-muted); padding:1px 5px; border-radius:var(--r-xs); color:hsl(var(--c-game)); }

/* icon picker */
.cp-icons { display:flex; flex-wrap:wrap; gap:8px; }
.cp-icon { width:46px; height:46px; border-radius:var(--r-md); border:1.5px solid var(--border); background:var(--bg-card);
  display:flex; align-items:center; justify-content:center; font-size:21px; cursor:pointer; transition:all var(--dur-sm) var(--ease-out); }
.cp-icon:hover { border-color:var(--border-strong); transform:translateY(-1px); }
.cp-icon.on { border-color:hsl(var(--c-agent)); background:hsl(var(--c-agent) / .12); box-shadow:0 0 0 3px hsl(var(--c-agent) / .22); }
.cp-upload { flex:1; min-width:160px; display:flex; align-items:center; justify-content:center; gap:9px; height:46px; border-radius:var(--r-md);
  border:1.5px dashed var(--border-strong); background:var(--bg); color:var(--text-muted); cursor:pointer; font-family:var(--f-body); font-size:var(--fs-xs); }
.cp-upload:hover { border-color:hsl(var(--c-agent) / .5); color:var(--text-sec); background:var(--bg-hover); }
.cp-upload .ui { font-size:16px; }

/* color picker */
.cp-colors { display:flex; flex-wrap:wrap; gap:9px; align-items:center; }
.cp-swatch { width:30px; height:30px; border-radius:var(--r-sm); cursor:pointer; border:2px solid transparent; position:relative; transition:transform var(--dur-sm) var(--ease-out); }
.cp-swatch:hover { transform:scale(1.08); }
.cp-swatch.on { border-color:var(--text); box-shadow:0 0 0 2px var(--bg-card), 0 0 0 4px var(--border-strong); }
.cp-swatch.on::after { content:'✓'; position:absolute; inset:0; display:flex; align-items:center; justify-content:center; color:#fff; font-size:13px; font-weight:900; text-shadow:0 1px 2px rgba(0,0,0,.4); }
.cp-advtoggle { font-family:var(--f-mono); font-size:11px; font-weight:var(--fw-bold); color:hsl(var(--c-agent)); background:none; border:none; cursor:pointer; padding:4px 6px; }
.cp-hslrow { display:flex; align-items:center; gap:14px; margin-top:12px; padding:12px; border:1px solid var(--border); border-radius:var(--r-md); background:var(--bg); }
.cp-hslrow .prev { width:40px; height:40px; border-radius:var(--r-sm); flex-shrink:0; }
.cp-hsl { flex:1; display:flex; flex-direction:column; gap:6px; }
.cp-hsl label { display:flex; align-items:center; gap:8px; font-family:var(--f-mono); font-size:10px; color:var(--text-muted); }
.cp-hsl label span { width:14px; }
.cp-hsl input[type=range] { flex:1; accent-color:hsl(var(--c-agent)); }
.cp-hsl label b { font-family:var(--f-mono); font-size:10px; color:var(--text-sec); width:34px; text-align:right; }

/* capability grid */
.cp-caps { display:grid; grid-template-columns:repeat(auto-fill, minmax(232px, 1fr)); gap:9px; }
.cp-cap { display:flex; align-items:flex-start; gap:10px; padding:11px 12px; border-radius:var(--r-md); border:1.5px solid var(--border);
  background:var(--bg-card); cursor:pointer; text-align:left; transition:all var(--dur-sm) var(--ease-out); position:relative; }
.cp-cap:hover { background:var(--bg-hover); border-color:var(--border-strong); }
.cp-cap.on { background:hsl(var(--c-agent) / .12); border-color:hsl(var(--c-agent) / .45); }
.cp-cap .cic { font-size:18px; line-height:1.1; flex-shrink:0; }
.cp-cap .cbody { display:flex; flex-direction:column; gap:3px; min-width:0; }
.cp-cap .cbody .ct { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); color:var(--text); }
.cp-cap.on .cbody .ct { color:hsl(var(--c-agent)); }
.cp-cap .cbody .cd { font-size:var(--fs-xs); color:var(--text-sec); line-height:var(--lh-snug); }
.cp-cap .ck { position:absolute; top:9px; right:9px; width:17px; height:17px; border-radius:50%; background:hsl(var(--c-agent)); color:#fff;
  display:flex; align-items:center; justify-content:center; font-size:10px; font-weight:900; opacity:0; transform:scale(.5); transition:all var(--dur-sm) var(--ease-spring); }
.cp-cap.on .ck { opacity:1; transform:scale(1); }
.cp-capwarn { display:flex; align-items:center; gap:6px; font-size:var(--fs-xs); color:hsl(var(--c-danger)); font-weight:var(--fw-semi); margin-top:10px; }

/* prompt toolbar + code editor */
.cp-ptoolbar { display:flex; align-items:center; gap:7px; flex-wrap:wrap; margin-bottom:9px; }
.cp-ptool { display:inline-flex; align-items:center; gap:5px; padding:5px 10px; border-radius:var(--r-sm); border:1px solid var(--border); white-space:nowrap;
  background:var(--bg-card); color:var(--text-sec); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); cursor:pointer; transition:all var(--dur-sm); }
.cp-ptool:hover { background:var(--bg-muted); color:var(--text); border-color:var(--border-strong); }
.cp-ptool.play { color:hsl(var(--c-tool)); border-color:hsl(var(--c-tool) / .35); background:hsl(var(--c-tool) / .08); }
.cp-ptool.play:hover { background:hsl(var(--c-tool) / .15); }
.cp-ptoolbar .grow { flex:1; }
.cp-code-wrap { position:relative; border-radius:var(--r-md); border:1.5px solid var(--border); background:var(--bg-muted); overflow:hidden; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.cp-code-wrap.focus { border-color:hsl(var(--c-agent) / .55); box-shadow:0 0 0 3px hsl(var(--c-agent) / .14); }
.cp-code-wrap.err { border-color:hsl(var(--c-danger) / .6); }
.cp-code-layer { margin:0; padding:13px 15px; font-family:var(--f-mono); font-size:13px; line-height:1.6; white-space:pre-wrap; word-break:break-word; }
.cp-code-hl { position:absolute; inset:0; pointer-events:none; color:var(--text); }
.cp-code-hl .v { color:hsl(var(--c-game)); font-weight:var(--fw-bold); background:hsl(var(--c-game) / .12); border-radius:3px; padding:0 2px; }
.cp-code-hl .bt { color:hsl(var(--c-tool)); }
.cp-code-hl .ph { color:var(--text-muted); }
.cp-code-ta { position:relative; display:block; width:100%; border:none; outline:none; resize:none; background:transparent;
  color:transparent; caret-color:hsl(var(--c-agent)); overflow:hidden; }
.cp-code-ta::selection { background:hsl(var(--c-agent) / .25); }
.cp-codefoot { display:flex; align-items:center; padding:7px 15px; border-top:1px solid var(--border-light); background:var(--bg); }
.cp-codefoot .grow { flex:1; }

/* sample rows */
.cp-samples { display:flex; flex-direction:column; gap:11px; }
.cp-sample { border:1px solid var(--border); border-radius:var(--r-md); padding:12px; background:var(--bg); position:relative; }
.cp-sample .shead { display:flex; align-items:center; margin-bottom:9px; }
.cp-sample .sidx { font-family:var(--f-mono); font-size:11px; font-weight:var(--fw-bold); color:var(--text-muted); flex:1; }
.cp-sample .sdel { width:28px; height:28px; border-radius:var(--r-sm); border:1px solid var(--border); background:var(--bg-card); color:var(--text-muted); cursor:pointer; font-size:13px; }
.cp-sample .sdel:hover { color:hsl(var(--c-danger)); border-color:hsl(var(--c-danger) / .4); background:hsl(var(--c-danger) / .08); }
.cp-sample .sgrid { display:grid; grid-template-columns:1fr 1fr; gap:10px; }
.cp-sample .scol .sl { font-family:var(--f-mono); font-size:10px; text-transform:uppercase; letter-spacing:.05em; color:var(--text-muted); margin-bottom:5px; }
.cp-addbtn { display:inline-flex; align-items:center; gap:6px; align-self:flex-start; padding:8px 13px; border-radius:var(--r-md);
  border:1.5px dashed var(--border-strong); background:transparent; color:var(--text-sec); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); cursor:pointer; }
.cp-addbtn:hover { border-color:hsl(var(--c-agent) / .5); color:hsl(var(--c-agent)); background:hsl(var(--c-agent) / .06); }
.cp-addbtn:disabled { opacity:.45; cursor:not-allowed; }

/* game scope picker */
.cp-scopsearch { position:relative; }
.cp-scopsearch .si { position:absolute; left:11px; top:50%; transform:translateY(-50%); opacity:.6; font-size:13px; pointer-events:none; }
.cp-scopsuggest { margin-top:7px; border:1px solid var(--border); border-radius:var(--r-md); background:var(--bg-card); box-shadow:var(--shadow-md); overflow:hidden; }
.cp-sugg { display:flex; align-items:center; gap:11px; width:100%; padding:9px 12px; border:none; background:transparent; cursor:pointer; text-align:left; }
.cp-sugg:not(:last-child) { border-bottom:1px solid var(--border-light); }
.cp-sugg:hover, .cp-sugg.hi { background:var(--bg-hover); }
.cp-sugg .th { width:34px; height:34px; border-radius:var(--r-sm); display:flex; align-items:center; justify-content:center; font-size:17px; color:#fff; flex-shrink:0; }
.cp-sugg .sm { min-width:0; flex:1; }
.cp-sugg .sm .gn { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); }
.cp-sugg .sm .gd { font-size:var(--fs-xs); color:var(--text-muted); }
.cp-sugg .add { font-family:var(--f-mono); font-size:11px; font-weight:var(--fw-bold); color:hsl(var(--c-game)); }
.cp-sugg.sel .add { color:var(--text-muted); }
.cp-scochips { display:flex; flex-wrap:wrap; gap:7px; margin-top:11px; }
.cp-gchip { display:inline-flex; align-items:center; gap:6px; padding:4px 6px 4px 4px; border-radius:var(--r-pill);
  background:hsl(var(--c-game) / .12); color:hsl(var(--c-game)); border:1px solid hsl(var(--c-game) / .25);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); }
.cp-gchip .ge { font-size:13px; }
.cp-gchip .gx { width:16px; height:16px; border-radius:50%; border:none; background:hsl(var(--c-game) / .2); color:hsl(var(--c-game)); cursor:pointer; font-size:10px; display:inline-flex; align-items:center; justify-content:center; }
.cp-gchip .gx:hover { background:hsl(var(--c-game) / .35); }
.cp-scopsum { display:inline-flex; align-items:center; gap:7px; margin-top:13px; padding:6px 13px; border-radius:var(--r-pill);
  background:var(--bg-muted); color:var(--text-sec); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); }
.cp-scopsum .pin { color:hsl(var(--c-game)); }

/* overlay (saving) */
.cp-dim { position:absolute; inset:0; z-index:var(--z-overlay); background:rgba(20,16,10,.18); backdrop-filter:blur(1px); display:flex; align-items:center; justify-content:center; }
[data-theme="dark"] .cp-dim { background:rgba(0,0,0,.4); }
.cp-savecard { display:flex; align-items:center; gap:11px; padding:14px 20px; border-radius:var(--r-lg); background:var(--bg-card); border:1px solid var(--border); box-shadow:var(--shadow-lg);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-base); color:var(--text); }
.cp-savecard .sp { width:18px; height:18px; border:2.5px solid hsl(var(--c-agent)); border-right-color:transparent; border-radius:50%; animation:cpspin .7s linear infinite; }

/* toast */
.cp-toast { position:absolute; right:18px; bottom:18px; z-index:var(--z-toast); display:flex; align-items:center; gap:10px; padding:12px 16px;
  border-radius:var(--r-md); background:var(--bg-card); border:1px solid hsl(var(--c-success) / .4); box-shadow:var(--shadow-lg);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); color:var(--text); animation:cptoast var(--dur-md) var(--ease-spring); }
.cp-toast .ck { width:22px; height:22px; border-radius:50%; background:hsl(var(--c-success)); color:#fff; display:flex; align-items:center; justify-content:center; font-size:12px; flex-shrink:0; }
@keyframes cptoast { from { transform:translateY(14px); } to { transform:translateY(0); } }

/* modal */
.cp-modal-bd { position:absolute; inset:0; z-index:var(--z-modal); background:rgba(20,16,10,.42); backdrop-filter:blur(2px); display:flex; align-items:center; justify-content:center; padding:24px; }
[data-theme="dark"] .cp-modal-bd { background:rgba(0,0,0,.6); }
.cp-modal { width:100%; max-width:440px; background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-xl); box-shadow:var(--shadow-lg); overflow:hidden; animation:cpmodal var(--dur-md) var(--ease-out); }
@keyframes cpmodal { from { transform:translateY(10px) scale(.98); } to { transform:none; } }
.cp-modal .mhead { display:flex; align-items:center; gap:11px; padding:18px 20px 14px; }
.cp-modal .mhead .mi { width:38px; height:38px; border-radius:var(--r-md); display:flex; align-items:center; justify-content:center; font-size:18px; flex-shrink:0; }
.cp-modal .mhead .mi.agent { background:hsl(var(--c-agent) / .14); }
.cp-modal .mhead .mi.warn { background:hsl(var(--c-warning) / .14); }
.cp-modal .mhead h3 { font-family:var(--f-display); font-size:var(--fs-lg); }
.cp-modal .mbody { padding:0 20px 16px; font-size:var(--fs-sm); color:var(--text-sec); line-height:var(--lh-body); }
.cp-modal .msummary { margin-top:13px; border:1px solid var(--border); border-radius:var(--r-md); overflow:hidden; }
.cp-modal .msrow { display:flex; gap:10px; padding:9px 12px; font-size:var(--fs-sm); }
.cp-modal .msrow:not(:last-child) { border-bottom:1px solid var(--border-light); }
.cp-modal .msrow .k { font-family:var(--f-mono); font-size:11px; text-transform:uppercase; letter-spacing:.04em; color:var(--text-muted); width:108px; flex-shrink:0; }
.cp-modal .msrow .vv { color:var(--text); font-weight:var(--fw-semi); min-width:0; }
.cp-modal .mfoot { display:flex; gap:9px; padding:14px 20px; background:var(--bg); border-top:1px solid var(--border); }
.cp-modal .mfoot .grow { flex:1; }

/* mobile */
.cp-app.is-mobile .cp-head .hrow { flex-wrap:wrap; padding:12px 14px 13px; }
.cp-app.is-mobile .cp-headcta { display:none; }
.cp-app.is-mobile .cp-h1 { font-size:var(--fs-xl); }
.cp-app.is-mobile .cp-body { padding:14px 13px 24px; }
.cp-app.is-mobile .cp-form { gap:12px; }
.cp-app.is-mobile .cp-sechead { padding:13px 14px; }
.cp-app.is-mobile .cp-secbody { padding:4px 14px 16px; }
.cp-app.is-mobile .cp-sectitle .t { font-size:var(--fs-base); }
.cp-app.is-mobile .cp-caps { grid-template-columns:1fr; }
.cp-app.is-mobile .cp-sample .sgrid { grid-template-columns:1fr; }
.cp-app.is-mobile .cp-foot .frow { padding:11px 13px; gap:7px; }
.cp-app.is-mobile .cp-foot .cp-btn { flex:1; justify-content:center; padding:11px 8px; }
.cp-app.is-mobile .cp-foot .cp-btn.link { flex:0 0 auto; }
.cp-app.is-mobile .cp-savebar { padding:6px 13px; }
.cp-app.is-mobile .cp-modal { max-width:none; }

@media (prefers-reduced-motion: reduce) {
  .cp-pill.saving .dot, .cp-unique .sp, .cp-savecard .sp, .cp-toast, .cp-modal { animation:none; }
}
`;

/* ──────────────────────────────────────────────────────────
   Static config
   ────────────────────────────────────────────────────────── */
const AUTHOR = { name: 'Marco R.', initials: 'MR' };

/* nomi typology già esistenti — usati per il check di unicità (case-insensitive) */
const EXISTING_NAMES = [
  'catan rules expert', 'strategy advisor', 'setup tutor', 'wingspan strategy advisor',
  'codenames spymaster', 'power grid auction helper', 'rules arbiter', 'endgame scorer',
];

const ICONS = ['🤖', '📋', '🎯', '🧠', '⚔️', '🛡️', '🎓', '🎨'];

const ENTITY_COLORS = [
  ['game', 25, 95, 45], ['player', 262, 83, 58], ['session', 240, 60, 55],
  ['agent', 38, 92, 50], ['kb', 174, 60, 40], ['chat', 220, 80, 55],
  ['event', 350, 89, 60], ['toolkit', 142, 70, 45], ['tool', 195, 80, 50],
];

const CAPABILITIES = [
  { id: 'qa',     icon: '💬', label: 'Q&A',         desc: 'Risponde a domande sulle regole dei giochi' },
  { id: 'stream', icon: '⚡', label: 'Streaming',    desc: 'Risposta token-by-token in tempo reale' },
  { id: 'tool',   icon: '🔧', label: 'Tool use',     desc: 'Chiama strumenti esterni (punteggi, timer…)' },
  { id: 'image',  icon: '📸', label: 'Image input',  desc: 'Analizza immagini (stato del tavolo, carte)' },
  { id: 'web',    icon: '🌐', label: 'Web search',   desc: 'Cerca regole e FAQ ufficiali online' },
];

const GAMES = (window.DS && window.DS.games) ? window.DS.games : [];

const PROMPT_FULL =
`Sei un esperto delle regole ufficiali di {game}. Rispondi alle domande del {player} citando sempre il manuale e indicando la pagina di riferimento quando possibile.

Se una regola è ambigua, spiega le interpretazioni più diffuse e segnala quella ufficiale. Mantieni un tono chiaro e amichevole, adatto anche a chi gioca per la prima volta. Non inventare regole non presenti in {rules_kb}.`;

const PROMPT_PARTIAL = `Sei un assistente per Ark Nova. Aiuti a`;
const PROMPT_SHORT = `Aiuta il giocatore.`;

/* ──────────────────────────────────────────────────────────
   Scenari (8 stati) — ognuno definisce form precompilato + UI flags
   ────────────────────────────────────────────────────────── */
const EMPTY_FORM = { name: '', desc: '', icon: null, color: 'agent', caps: [], prompt: '', samples: [], scope: [] };

const FORM_PARTIAL = {
  name: 'Ark Nova Zoo Planner', desc: 'Aiuta a pianificare la costruzione dello zoo e l\u2019ordine delle carte azione per massimizzare punti conservazione e appeal.',
  icon: '🎯', color: 'agent', caps: ['qa', 'stream'], prompt: PROMPT_PARTIAL, samples: [], scope: [],
};
const FORM_VALID = {
  name: 'Brass Routes Coach', desc: 'Consiglia la rete di canali e ferrovie ottimale in Brass: Birmingham, valutando mercati, link e tempistica delle ere.',
  icon: '🧠', color: 'agent', caps: ['qa', 'tool', 'stream'], prompt: PROMPT_FULL,
  samples: [
    { in: 'Conviene costruire un canale verso Birmingham al primo turno?', out: 'Dipende dalle tue industrie iniziali: un canale precoce verso Birmingham aumenta la connettività ma spende denaro utile per il primo livello di industria…' },
    { in: 'Quando passo dalle ere canale a quella ferrovia?', out: 'L\u2019era ferrovia inizia quando il mazzo si esaurisce la prima volta. Conviene chiudere l\u2019era canale avendo già piazzato le industrie di basso livello…' },
  ],
  scope: ['g-brass'],
};
const FORM_ERRORS = {
  name: 'Catan Rules Expert', desc: 'Esperto delle regole di Catan per preparazione, commercio e calcolo dei punti vittoria a fine partita.',
  icon: '📋', color: 'agent', caps: ['qa'], prompt: PROMPT_SHORT, samples: [], scope: ['g-catan'],
};

const SCENARIOS = {
  'default-empty':     { form: EMPTY_FORM,   open: [1, 2, 3, 4, 5], showValidation: false, savePill: null,      ui: {} },
  'partial-filled':    { form: FORM_PARTIAL, open: [1, 2, 3],       showValidation: true,  savePill: 'unsaved',  ui: {} },
  'all-valid':         { form: FORM_VALID,   open: [3, 5],          showValidation: true,  savePill: 'saved',    ui: {} },
  'validation-errors': { form: FORM_ERRORS,  open: [1, 3],          showValidation: true,  savePill: 'unsaved',  ui: {} },
  'saving':            { form: FORM_VALID,   open: [],              showValidation: true,  savePill: 'saving',   ui: { overlay: true } },
  'draft-saved':       { form: FORM_PARTIAL, open: [1, 2],          showValidation: true,  savePill: 'saved',    ui: { toast: true } },
  'submitting':        { form: FORM_VALID,   open: [],              showValidation: true,  savePill: 'saved',    ui: { modal: true } },
  'submit-success':    { form: FORM_VALID,   open: [],              showValidation: true,  savePill: 'saved',    ui: { banner: true, readonly: true } },
};
const STATE_LIST = [
  ['default-empty', 'Empty'], ['partial-filled', 'Partial'], ['all-valid', 'Valid'],
  ['validation-errors', 'Errors'], ['saving', 'Saving'], ['draft-saved', 'Draft saved'],
  ['submitting', 'Submitting'], ['submit-success', 'Success'],
];

/* ──────────────────────────────────────────────────────────
   Helpers
   ────────────────────────────────────────────────────────── */
function colorTriplet(key) {
  const c = ENTITY_COLORS.find(x => x[0] === key);
  return c ? `${c[1]} ${c[2]}% ${c[3]}%` : '38 92% 50%';
}
function gameById(id) { return GAMES.find(g => g.id === id); }

/* highlight {var} e `backtick` per il code layer */
function highlightPrompt(text) {
  if (!text) return [<span className="ph" key="ph">Definisci il comportamento dell&rsquo;agente… usa {'{game}'} {'{player}'} {'{turn}'} {'{rules_kb}'}</span>];
  const parts = text.split(/(\{[a-z_]+\}|`[^`]*`)/g);
  return parts.map((p, i) => {
    if (/^\{[a-z_]+\}$/.test(p)) return <span className="v" key={i}>{p}</span>;
    if (/^`[^`]*`$/.test(p)) return <span className="bt" key={i}>{p}</span>;
    return <React.Fragment key={i}>{p}</React.Fragment>;
  });
}

/* ──────────────────────────────────────────────────────────
   Sub-components
   ────────────────────────────────────────────────────────── */
function ValidationPip({ kind, count }) {
  if (kind === 'valid')      return <span className="cp-vpip valid"><span aria-hidden="true">✓</span> Completa</span>;
  if (kind === 'issues')     return <span className="cp-vpip issues" role="status"><span aria-hidden="true">⚠</span> {count} {count === 1 ? 'problema' : 'problemi'}</span>;
  if (kind === 'optional')   return <span className="cp-vpip optional"><span aria-hidden="true">·</span> Opzionale</span>;
  return <span className="cp-vpip incomplete"><span aria-hidden="true">·</span> Da completare</span>;
}

function SectionCard({ num, id, title, subtitle, optional, status, statusCount, open, onToggle, children, bodyRef }) {
  const cls = 'cp-sec' + (open ? ' open' : '') + (status === 'issues' ? ' has-error' : status === 'valid' ? ' is-valid' : '');
  return (
    <section className={cls} role="region" aria-labelledby={'sec-' + id + '-t'}>
      <button className="cp-sechead" aria-expanded={open} aria-controls={'sec-' + id + '-b'} onClick={onToggle}>
        <span className="cp-secnum" aria-hidden="true">{num}</span>
        <span className="cp-sectitle">
          <span className="t" id={'sec-' + id + '-t'}>{title}{optional && <span className="opt">Opzionale</span>}</span>
          <span className="s">{subtitle}</span>
        </span>
        <ValidationPip kind={status} count={statusCount} />
        <span className="cp-caret" aria-hidden="true">▸</span>
      </button>
      {open && <div className="cp-secbody" id={'sec-' + id + '-b'} role="group" ref={bodyRef}>{children}</div>}
    </section>
  );
}

function CharCounter({ value, max }) {
  const over = value > max;
  return <span className={'cp-counter' + (over ? ' over' : '')}>{value} / {max}</span>;
}

function AutoTextarea({ value, onChange, max, minH = 74, readOnly, ...rest }) {
  const ref = useRef(null);
  const fit = useCallback(() => { const el = ref.current; if (!el) return; el.style.height = 'auto'; el.style.height = Math.max(minH, el.scrollHeight) + 'px'; }, [minH]);
  useEffect(() => { fit(); }, [value, fit]);
  return (
    <textarea ref={ref} className="cp-textarea" value={value} readOnly={readOnly}
      onChange={e => { if (!readOnly && (!max || e.target.value.length <= max + 40)) onChange(e.target.value); }} {...rest} />
  );
}

function UniqueCheck({ status }) {
  if (status === 'checking') return <span className="cp-unique checking"><span className="sp" aria-hidden="true" />Verificando…</span>;
  if (status === 'ok')       return <span className="cp-unique ok" role="status"><span aria-hidden="true">✓</span> Disponibile</span>;
  if (status === 'dup')      return <span className="cp-unique dup" role="status"><span aria-hidden="true">✗</span> Già in uso</span>;
  return null;
}

/* ─── Section 1: Identity ─── */
function IdentitySection({ form, set, readOnly, uniqueStatus, showValidation }) {
  const [adv, setAdv] = useState(false);
  const [hsl, setHsl] = useState({ h: 38, s: 92, l: 50 });
  const nameTooShort = showValidation && form.name.trim().length > 0 && form.name.trim().length < 3;
  return (
    <>
      <div className="cp-field">
        <label className="cp-label" htmlFor="f-name">Nome <span className="req">*</span><span className="grow" /><CharCounter value={form.name.length} max={80} /></label>
        <div className="cp-inwrap">
          <input id="f-name" className={'cp-input haspad' + (uniqueStatus === 'dup' || nameTooShort ? ' err' : '')} value={form.name} readOnly={readOnly}
            maxLength={84} placeholder="es. Catan Rules Expert" aria-invalid={uniqueStatus === 'dup'}
            aria-describedby={uniqueStatus === 'dup' ? 'f-name-err' : undefined}
            onChange={e => set('name', e.target.value)} />
          <UniqueCheck status={uniqueStatus} />
        </div>
        {uniqueStatus === 'dup' && <div className="cp-err" id="f-name-err"><span aria-hidden="true">⚠️</span> Esiste già una typology con questo nome. Scegline uno diverso prima di inviare.</div>}
        {nameTooShort && <div className="cp-err"><span aria-hidden="true">⚠️</span> Minimo 3 caratteri.</div>}
      </div>

      <div className="cp-field">
        <label className="cp-label" htmlFor="f-desc">Descrizione <span className="req">*</span><span className="grow" /><CharCounter value={form.desc.length} max={500} /></label>
        <AutoTextarea id="f-desc" value={form.desc} max={500} minH={70} readOnly={readOnly}
          placeholder="Descrivi cosa fa questa typology e quando usarla…" onChange={v => set('desc', v)} />
        {showValidation && form.desc.length > 0 && form.desc.length < 20 && <div className="cp-err"><span aria-hidden="true">⚠️</span> Minimo 20 caratteri ({form.desc.length}/20).</div>}
      </div>

      <div className="cp-field">
        <label className="cp-label">Icona <span className="req">*</span></label>
        <div className="cp-icons" role="radiogroup" aria-label="Scegli icona">
          {ICONS.map(ic => (
            <button key={ic} type="button" role="radio" aria-checked={form.icon === ic} aria-label={'Icona ' + ic}
              className={'cp-icon' + (form.icon === ic ? ' on' : '')} onClick={() => !readOnly && set('icon', ic)}>{ic}</button>
          ))}
          <div className="cp-upload" role="button" tabIndex={0} aria-label="Carica icona personalizzata">
            <span className="ui" aria-hidden="true">⬆</span> Carica PNG/SVG · max 50KB
          </div>
        </div>
      </div>

      <div className="cp-field">
        <label className="cp-label">Colore entity<span className="grow" /><button type="button" className="cp-advtoggle" aria-expanded={adv} onClick={() => setAdv(a => !a)}>{adv ? '− HSL custom' : '+ HSL custom'}</button></label>
        <div className="cp-colors" role="radiogroup" aria-label="Scegli colore entity">
          {ENTITY_COLORS.map(([key, h, s, l]) => (
            <button key={key} type="button" role="radio" aria-checked={form.color === key} aria-label={'Colore ' + key}
              className={'cp-swatch' + (form.color === key ? ' on' : '')} style={{ background: `hsl(${h} ${s}% ${l}%)` }}
              onClick={() => !readOnly && set('color', key)} />
          ))}
        </div>
        {adv && (
          <div className="cp-hslrow">
            <div className="prev" style={{ background: `hsl(${hsl.h} ${hsl.s}% ${hsl.l}%)` }} />
            <div className="cp-hsl">
              <label><span>H</span><input type="range" min="0" max="360" value={hsl.h} onChange={e => setHsl(s => ({ ...s, h: +e.target.value }))} /><b>{hsl.h}</b></label>
              <label><span>S</span><input type="range" min="0" max="100" value={hsl.s} onChange={e => setHsl(s => ({ ...s, s: +e.target.value }))} /><b>{hsl.s}%</b></label>
              <label><span>L</span><input type="range" min="0" max="100" value={hsl.l} onChange={e => setHsl(s => ({ ...s, l: +e.target.value }))} /><b>{hsl.l}%</b></label>
            </div>
          </div>
        )}
      </div>
    </>
  );
}

/* ─── Section 2: Capabilities ─── */
function CapabilitiesSection({ form, set, readOnly, showValidation }) {
  const toggle = id => { if (readOnly) return; set('caps', form.caps.includes(id) ? form.caps.filter(c => c !== id) : [...form.caps, id]); };
  const none = showValidation && form.caps.length === 0;
  return (
    <>
      <div className="cp-caps" role="group" aria-label="Seleziona capability">
        {CAPABILITIES.map(c => {
          const on = form.caps.includes(c.id);
          return (
            <button key={c.id} type="button" className={'cp-cap' + (on ? ' on' : '')} aria-pressed={on} onClick={() => toggle(c.id)}>
              <span className="cic" aria-hidden="true">{c.icon}</span>
              <span className="cbody"><span className="ct">{c.label}</span><span className="cd">{c.desc}</span></span>
              <span className="ck" aria-hidden="true">✓</span>
            </button>
          );
        })}
      </div>
      {none && <div className="cp-capwarn" role="alert"><span aria-hidden="true">⚠️</span> Seleziona almeno una capability.</div>}
      <div className="cp-tip"><span className="ti" aria-hidden="true">💡</span><span className="tt">Le capability sono <b>tassonomia interna</b>: determinano quali tool e modalità l&rsquo;agente potrà usare in chat. Puoi modificarle anche dopo l&rsquo;approvazione.</span></div>
    </>
  );
}

/* ─── Section 3: System prompt ─── */
function CodeEditor({ value, onChange, readOnly, error }) {
  const taRef = useRef(null);
  const hlRef = useRef(null);
  const [focus, setFocus] = useState(false);
  const fit = useCallback(() => {
    const ta = taRef.current; if (!ta) return;
    ta.style.height = 'auto';
    const h = Math.min(Math.max(150, ta.scrollHeight), 460);
    ta.style.height = h + 'px';
  }, []);
  useEffect(() => { fit(); }, [value, fit]);
  const syncScroll = () => { if (hlRef.current && taRef.current) hlRef.current.scrollTop = taRef.current.scrollTop; };
  return (
    <div className={'cp-code-wrap' + (focus ? ' focus' : '') + (error ? ' err' : '')}>
      <pre className="cp-code-layer cp-code-hl" ref={hlRef} aria-hidden="true">{highlightPrompt(value)}</pre>
      <textarea ref={taRef} className="cp-code-layer cp-code-ta" value={value} readOnly={readOnly} spellCheck={false}
        aria-label="System prompt" rows={6} onScroll={syncScroll}
        onFocus={() => setFocus(true)} onBlur={() => setFocus(false)}
        onChange={e => { if (!readOnly && e.target.value.length <= 4000) onChange(e.target.value); }} />
    </div>
  );
}

function SystemPromptSection({ form, set, readOnly, showValidation }) {
  const tooShort = showValidation && form.prompt.trim().length > 0 && form.prompt.trim().length < 50;
  return (
    <>
      <div className="cp-ptoolbar">
        <button type="button" className="cp-ptool" onClick={() => !readOnly && set('prompt', PROMPT_FULL)}><span aria-hidden="true">＋</span> Aggiungi esempio</button>
        <button type="button" className="cp-ptool"><span aria-hidden="true">▤</span> Templates</button>
        <span className="grow" />
        <button type="button" className="cp-ptool play" title="Apri il playground di test (S5)"><span aria-hidden="true">🧪</span> Testa nel playground</button>
      </div>
      <CodeEditor value={form.prompt} onChange={v => set('prompt', v)} readOnly={readOnly} error={tooShort} />
      <div className="cp-codefoot"><span className="grow" /><CharCounter value={form.prompt.length} max={4000} /></div>
      {tooShort && <div className="cp-err"><span aria-hidden="true">⚠️</span> Il system prompt è troppo corto: minimo 50 caratteri ({form.prompt.trim().length}/50).</div>}
      <div className="cp-tip"><span className="ti" aria-hidden="true">💡</span><span className="tt">Variabili disponibili: <code>{'{game}'}</code> <code>{'{player}'}</code> <code>{'{turn}'}</code> <code>{'{rules_kb}'}</code>. Vengono sostituite a runtime. Usa <code>\n</code> per newline literali e i <code>`backtick`</code> per i nomi di tool.</span></div>
    </>
  );
}

/* ─── Section 4: Test config ─── */
function TestConfigSection({ form, set, readOnly }) {
  const addSample = () => { if (readOnly || form.samples.length >= 3) return; set('samples', [...form.samples, { in: '', out: '' }]); };
  const delSample = i => { if (readOnly) return; set('samples', form.samples.filter((_, x) => x !== i)); };
  const editSample = (i, k, v) => { if (readOnly) return; set('samples', form.samples.map((s, x) => x === i ? { ...s, [k]: v } : s)); };
  return (
    <>
      <div className="cp-samples">
        {form.samples.map((s, i) => (
          <div className="cp-sample" key={i}>
            <div className="shead"><span className="sidx">Esempio {i + 1}</span>
              <button type="button" className="sdel" aria-label={'Elimina esempio ' + (i + 1)} onClick={() => delSample(i)}>🗑</button></div>
            <div className="sgrid">
              <div className="scol"><div className="sl">Input utente</div>
                <AutoTextarea value={s.in} minH={62} readOnly={readOnly} placeholder="es. Quante carte si pescano in setup?" onChange={v => editSample(i, 'in', v)} /></div>
              <div className="scol"><div className="sl">Output atteso</div>
                <AutoTextarea value={s.out} minH={62} readOnly={readOnly} placeholder="es. In Catan classico ogni giocatore parte con 2 insediamenti…" onChange={v => editSample(i, 'out', v)} /></div>
            </div>
          </div>
        ))}
        <button type="button" className="cp-addbtn" disabled={readOnly || form.samples.length >= 3} onClick={addSample}>
          <span aria-hidden="true">＋</span> Aggiungi esempio {form.samples.length > 0 && `(${form.samples.length}/3)`}
        </button>
      </div>
      <div className="cp-tip"><span className="ti" aria-hidden="true">💡</span><span className="tt">Questi esempi verranno usati nel <b>playground</b> per validare il comportamento dell&rsquo;agente. <b>Non</b> sono training data e non modificano il modello.</span></div>
    </>
  );
}

/* ─── Section 5: Game scope ─── */
function GameScopeSection({ form, set, readOnly }) {
  const [q, setQ] = useState('');
  const selected = form.scope.map(gameById).filter(Boolean);
  const suggestions = GAMES.filter(g => !q || g.title.toLowerCase().includes(q.toLowerCase()));
  const toggleGame = id => { if (readOnly) return; set('scope', form.scope.includes(id) ? form.scope.filter(s => s !== id) : [...form.scope, id]); };
  return (
    <>
      <div className="cp-scopsearch cp-scopsearch">
        <div className="cp-inwrap">
          <span className="si" aria-hidden="true">🔍</span>
          <input className="cp-input" style={{ paddingLeft: 32 }} value={q} readOnly={readOnly}
            placeholder="Cerca un gioco per nome…" aria-label="Cerca gioco" onChange={e => setQ(e.target.value)} />
        </div>
        {!readOnly && q && (
          <div className="cp-scopsuggest" role="listbox" aria-label="Giochi suggeriti">
            {suggestions.length === 0 && <div className="cp-sugg" aria-disabled="true"><span className="sm"><span className="gd">Nessun gioco trovato per “{q}”</span></span></div>}
            {suggestions.slice(0, 5).map(g => {
              const sel = form.scope.includes(g.id);
              return (
                <button key={g.id} type="button" role="option" aria-selected={sel} className={'cp-sugg' + (sel ? ' sel' : '')} onClick={() => toggleGame(g.id)}>
                  <span className="th" style={{ background: g.cover }} aria-hidden="true">{g.coverEmoji}</span>
                  <span className="sm"><span className="gn">{g.title}</span><span className="gd">{g.author} · {g.year}</span></span>
                  <span className="add">{sel ? '✓ Aggiunto' : '+ Aggiungi'}</span>
                </button>
              );
            })}
          </div>
        )}
      </div>

      {selected.length > 0 && (
        <div className="cp-scochips" aria-label="Giochi in scope">
          {selected.map(g => (
            <span className="cp-gchip" key={g.id}><span className="ge" aria-hidden="true">🎲</span>{g.title}
              {!readOnly && <button type="button" className="gx" aria-label={'Rimuovi ' + g.title} onClick={() => toggleGame(g.id)}>✕</button>}</span>
          ))}
        </div>
      )}

      <div className="cp-scopsum">
        <span className="pin" aria-hidden="true">📍</span>
        {selected.length === 0 ? 'Scope: tutti i giochi (broad)' : `Scope: ${selected.length} ${selected.length === 1 ? 'gioco' : 'giochi'} (${selected.map(g => g.title).join(', ')})`}
      </div>
      {selected.length === 0 && <div className="cp-hint">Lascia vuoto per applicare la typology a tutti i giochi.</div>}
    </>
  );
}

/* ─── Modals / toast / banner ─── */
function SubmitReviewModal({ form, onClose, onConfirm }) {
  const scope = form.scope.map(gameById).filter(Boolean);
  return (
    <div className="cp-modal-bd" role="dialog" aria-modal="true" aria-labelledby="sub-modal-t" onClick={onClose}>
      <div className="cp-modal" onClick={e => e.stopPropagation()}>
        <div className="mhead"><span className="mi agent" aria-hidden="true">⬆️</span><h3 id="sub-modal-t">Conferma invio per review</h3></div>
        <div className="mbody">
          La proposal verrà inviata al team di review e non sarà più modificabile finché non riceve un esito. Di solito entro 24h.
          <div className="msummary">
            <div className="msrow"><span className="k">Nome</span><span className="vv">{form.icon} {form.name}</span></div>
            <div className="msrow"><span className="k">Capabilities</span><span className="vv">{form.caps.map(c => CAPABILITIES.find(x => x.id === c).label).join(', ')}</span></div>
            <div className="msrow"><span className="k">Scope</span><span className="vv">{scope.length ? scope.map(g => g.title).join(', ') : 'Tutti i giochi'}</span></div>
            <div className="msrow"><span className="k">Esempi test</span><span className="vv">{form.samples.length} configurati</span></div>
          </div>
        </div>
        <div className="mfoot"><button className="cp-btn ghost" onClick={onClose}>Annulla</button><span className="grow" /><button className="cp-btn primary" onClick={onConfirm}><span className="ic" aria-hidden="true">⬆</span> Conferma e invia</button></div>
      </div>
    </div>
  );
}

function ConfirmCancelModal({ onClose, onConfirm }) {
  return (
    <div className="cp-modal-bd" role="dialog" aria-modal="true" aria-labelledby="cnc-t" onClick={onClose}>
      <div className="cp-modal" onClick={e => e.stopPropagation()}>
        <div className="mhead"><span className="mi warn" aria-hidden="true">⚠️</span><h3 id="cnc-t">Annullare la creazione?</h3></div>
        <div className="mbody">Le modifiche non salvate andranno perse. Vuoi davvero uscire senza salvare la bozza?</div>
        <div className="mfoot"><button className="cp-btn ghost" onClick={onClose}>Continua a modificare</button><span className="grow" /><button className="cp-btn danger" onClick={onConfirm}>Esci senza salvare</button></div>
      </div>
    </div>
  );
}

/* ──────────────────────────────────────────────────────────
   CreateApp — un'istanza per (state × viewport). Remount via key.
   ────────────────────────────────────────────────────────── */
function CreateApp({ stateId, mobile }) {
  const sc = SCENARIOS[stateId];
  const [form, setForm] = useState(sc.form);
  const [open, setOpen] = useState(() => new Set(sc.open));
  const [savePill, setSavePill] = useState(sc.savePill);
  const [showValidation, setShowValidation] = useState(sc.showValidation);
  const [uniqueStatus, setUniqueStatus] = useState(() => {
    const n = sc.form.name.trim().toLowerCase();
    if (n.length < 3) return 'idle';
    return EXISTING_NAMES.includes(n) ? 'dup' : 'ok';
  });
  const [cancelModal, setCancelModal] = useState(false);
  const [submitModal, setSubmitModal] = useState(!!sc.ui.modal);
  const [toast, setToast] = useState(!!sc.ui.toast);
  const banner = !!sc.ui.banner;
  const overlay = !!sc.ui.overlay;
  const readonly = !!sc.ui.readonly;
  const uniqueTimer = useRef(null);

  const set = useCallback((key, val) => {
    setForm(f => ({ ...f, [key]: val }));
    setShowValidation(true);
    if (savePill !== 'saving') setSavePill('unsaved');
  }, [savePill]);

  // live unique check on name
  useEffect(() => {
    const n = form.name.trim().toLowerCase();
    clearTimeout(uniqueTimer.current);
    if (n.length < 3) { setUniqueStatus('idle'); return; }
    setUniqueStatus('checking');
    uniqueTimer.current = setTimeout(() => setUniqueStatus(EXISTING_NAMES.includes(n) ? 'dup' : 'ok'), 650);
    return () => clearTimeout(uniqueTimer.current);
  }, [form.name]);

  // auto-dismiss toast
  useEffect(() => { if (toast) { const t = setTimeout(() => setToast(false), 3600); return () => clearTimeout(t); } }, [toast]);

  // validation per section
  const v = useMemo(() => {
    const n = form.name.trim();
    const s1issues = [];
    if (n.length < 3) s1issues.push('name'); else if (uniqueStatus === 'dup') s1issues.push('dup');
    if (form.desc.trim().length < 20) s1issues.push('desc');
    if (!form.icon) s1issues.push('icon');
    const s1 = { kind: s1issues.length === 0 ? 'valid' : (showValidation && n.length ? 'issues' : 'incomplete'), count: s1issues.length, ok: s1issues.length === 0 };
    const s2 = { kind: form.caps.length >= 1 ? 'valid' : (showValidation ? 'issues' : 'incomplete'), count: form.caps.length === 0 ? 1 : 0, ok: form.caps.length >= 1 };
    const p = form.prompt.trim();
    const s3 = { kind: p.length >= 50 ? 'valid' : (showValidation && p.length ? 'issues' : 'incomplete'), count: p.length >= 50 ? 0 : 1, ok: p.length >= 50 };
    const s4 = { kind: 'optional', count: 0, ok: true };
    const s5 = { kind: 'optional', count: 0, ok: true };
    return { 1: s1, 2: s2, 3: s3, 4: s4, 5: s5, valid: s1.ok && s2.ok && s3.ok };
  }, [form, uniqueStatus, showValidation]);

  const toggle = id => setOpen(o => { const n = new Set(o); n.has(id) ? n.delete(id) : n.add(id); return n; });

  // keyboard: Ctrl+S salva bozza
  useEffect(() => {
    const h = e => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') { e.preventDefault(); setSavePill('saved'); setToast(true); }
      if (e.key === 'Escape' && submitModal) setSubmitModal(false);
    };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [submitModal]);

  const saveDraft = () => { setSavePill('saving'); setTimeout(() => { setSavePill('saved'); setToast(true); }, 600); };

  const sections = [
    { num: '1', id: 'identity', title: 'Identità', subtitle: 'Nome, descrizione, icona e colore', optional: false, st: v[1],
      body: <IdentitySection form={form} set={set} readOnly={readonly} uniqueStatus={uniqueStatus} showValidation={showValidation} /> },
    { num: '2', id: 'caps', title: 'Capabilities', subtitle: 'Cosa sa fare l\u2019agente (almeno una)', optional: false, st: v[2],
      body: <CapabilitiesSection form={form} set={set} readOnly={readonly} showValidation={showValidation} /> },
    { num: '3', id: 'prompt', title: 'System prompt', subtitle: 'Le istruzioni che guidano l\u2019agente', optional: false, st: v[3],
      body: <SystemPromptSection form={form} set={set} readOnly={readonly} showValidation={showValidation} /> },
    { num: '4', id: 'test', title: 'Configurazione test', subtitle: 'Esempi input/output per il playground', optional: true, st: v[4],
      body: <TestConfigSection form={form} set={set} readOnly={readonly} /> },
    { num: '5', id: 'scope', title: 'Scope giochi', subtitle: 'A quali giochi si applica', optional: true, st: v[5],
      body: <GameScopeSection form={form} set={set} readOnly={readonly} /> },
  ];

  const pillEl = savePill && (
    <span className={'cp-pill ' + savePill} aria-live="polite">
      <span className="dot" aria-hidden="true" />
      {savePill === 'unsaved' && 'Modifiche non salvate'}
      {savePill === 'saved' && '💾 Bozza salvata adesso'}
      {savePill === 'saving' && 'Salvataggio…'}
    </span>
  );

  return (
    <div className={'cp-app' + (mobile ? ' is-mobile' : '')}>
      {banner && (
        <div className="cp-banner" role="status">
          <span aria-hidden="true">✓</span> Proposal inviata — sarà revisionata entro 24h.
          <span className="grow" />
          <button className="gocta"><span aria-hidden="true">→</span> Vai alle mie proposals</button>
        </div>
      )}

      <header className="cp-head">
        <div className="hrow">
          <div className="htxt">
            <div className="cp-bread"><span>Editor</span><span className="sep">›</span><span>Agent proposals</span><span className="sep">›</span><span className="cur">Crea nuova</span></div>
            <h1 className="cp-h1">Crea nuova typology proposal</h1>
            <div className="cp-sub">Definisci una nuova tipologia di agente AI da sottoporre per approvazione</div>
            <span className="cp-author"><span className="av" aria-hidden="true">{AUTHOR.initials}</span><span className="lb">Autore:</span> {AUTHOR.name}</span>
          </div>
          <span className="grow" />
          <div className="cp-headcta">
            <button className="cp-btn link" onClick={() => setCancelModal(true)}>Annulla</button>
            <button className="cp-btn ghost" onClick={saveDraft} disabled={readonly}><span className="ic" aria-hidden="true">💾</span> Salva bozza</button>
            <button className="cp-btn primary" disabled={!v.valid || readonly} onClick={() => setSubmitModal(true)}><span className="ic" aria-hidden="true">⬆</span> Invia per review</button>
          </div>
        </div>
      </header>

      <div className="cp-body">
        <form className="cp-form" aria-label="Form crea typology proposal" onSubmit={e => e.preventDefault()}>
          {sections.map(s => (
            <SectionCard key={s.id} num={s.num} id={s.id} title={s.title} subtitle={s.subtitle} optional={s.optional}
              status={s.st.kind} statusCount={s.st.count} open={open.has(+s.num)} onToggle={() => toggle(+s.num)}>
              {s.body}
            </SectionCard>
          ))}
        </form>
      </div>

      <div className="cp-savebar"><div className="inner">{pillEl}</div></div>

      <footer className="cp-foot">
        <div className="frow">
          <button className="cp-btn link" onClick={() => setCancelModal(true)}>Annulla</button>
          <span className="grow" />
          <button className="cp-btn ghost" onClick={saveDraft} disabled={readonly}><span className="ic" aria-hidden="true">💾</span> Salva bozza</button>
          <button className="cp-btn primary" disabled={!v.valid || readonly} onClick={() => setSubmitModal(true)}><span className="ic" aria-hidden="true">⬆</span> Invia per review</button>
        </div>
      </footer>

      {overlay && <div className="cp-dim" aria-hidden="true"><div className="cp-savecard"><span className="sp" /> Salvataggio in corso…</div></div>}
      {toast && <div className="cp-toast" role="status"><span className="ck" aria-hidden="true">✓</span> Bozza salvata</div>}
      {submitModal && <SubmitReviewModal form={form} onClose={() => setSubmitModal(false)} onConfirm={() => setSubmitModal(false)} />}
      {cancelModal && <ConfirmCancelModal onClose={() => setCancelModal(false)} onConfirm={() => setCancelModal(false)} />}
    </div>
  );
}

/* ──────────────────────────────────────────────────────────
   Harness — continuity con S1/S2
   ────────────────────────────────────────────────────────── */
function Harness() {
  const [stateId, setStateId] = useState(() => localStorage.getItem('cp-state') || 'default-empty');
  const [theme, setTheme] = useState(() => localStorage.getItem('mai-theme') || 'light');

  useEffect(() => { document.documentElement.setAttribute('data-theme', theme); localStorage.setItem('mai-theme', theme); }, [theme]);
  useEffect(() => { localStorage.setItem('cp-state', stateId); }, [stateId]);

  return (
    <div className="ed-stage">
      <style dangerouslySetInnerHTML={{ __html: CP_CSS }} />
      <button className="theme-toggle" onClick={() => setTheme(theme === 'light' ? 'dark' : 'light')}>🌗 <span>{theme === 'dark' ? 'Dark' : 'Light'}</span></button>

      <div className="ed-wrap">
        <div className="ed-kicker">SP4 · B14 · #1489 — schermata 3 / 5 · crea typology proposal</div>
        <h1>Crea <span className="acc">proposal</span> — /editor/agent-proposals/create</h1>
        <p className="ed-lead">
          Form full-page per creare una nuova <b>typology AI agent proposal</b>. Niente wizard: l&rsquo;editor power-user vede
          tutto il context insieme in <b>5 sezioni accordion</b> (Identità · Capabilities · System prompt · Test config · Scope),
          salva una bozza parziale e salta fra sezioni. Submit → entity in stato <b>Draft</b>, poi <b>Invia per review</b>.
          Entity primaria <code>--c-agent</code>, scope giochi via EntityChip <code>--c-game</code>.
        </p>

        <div className="ed-notes">
          <div className="ed-note">
            <h4>Pattern</h4>
            <p><b>Form full-page</b> max-width 880 centered, header + footer sticky con CTA tripla (<b>Annulla · Salva bozza · Invia per review</b>). Ogni sezione è una card collassabile con validation pip realtime.</p>
          </div>
          <div className="ed-note">
            <h4>8 stati</h4>
            <p>Selettore qui sotto: empty · partial · valid · errors · saving · draft-saved · submitting · success. Validation inline (nome duplicato, prompt corto, capability mancante) + save status pill <code>aria-live</code>.</p>
          </div>
          <div className="ed-note">
            <h4>Mobile & a11y</h4>
            <p>Mobile = sezioni full-width, footer sticky con CTA. <code>role=region</code> + <code>aria-expanded</code> per sezione, <code>aria-invalid</code> sui field in errore, <code>Ctrl+S</code> salva bozza, <code>Esc</code> chiude modal.</p>
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

        <div className="ed-vp-label">Desktop · 1440 — form 5-section accordion</div>
        <div className="ed-desk">
          <div className="ed-chrome">
            <div className="dots"><i /><i /><i /></div>
            <div className="url">meepleai.app/editor/agent-proposals/create</div>
          </div>
          <div style={{ flex: 1, minHeight: 0 }}>
            <CreateApp key={'d-' + stateId} stateId={stateId} mobile={false} />
          </div>
        </div>

        <div className="ed-vp-label">Mobile · 375 — sezioni stack full-width</div>
        <div className="ed-phone-row">
          <div className="phone">
            <div className="phone-sbar"><span>9:41</span><span className="ind">●●● 5G ▮</span></div>
            <div style={{ flex: 1, minHeight: 0, display: 'flex' }}>
              <CreateApp key={'m-' + stateId} stateId={stateId} mobile={true} />
            </div>
          </div>
          <div className="ed-phone-cap">
            <h4>Layout mobile</h4>
            <p>Le 5 sezioni accordion vanno a <b>full-width</b>, la capability grid e gli esempi test passano a colonna singola. Header CTA collassano nel <b>footer sticky</b> (Salva bozza · Invia). Modal e toast occupano la larghezza dello schermo.</p>
          </div>
        </div>
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<Harness />);
