/* sp4-editor-proposals-edit.jsx
   Route: /editor/agent-proposals/[id]/edit — Edit typology AI agent proposal (status-variant mode)
   B14 (issue #1489) · screen 4 of 5 · Tier M
   Pattern: Form full-page identico a S3 (5 sezioni accordion) + status-variant header/banner +
            read-only mode (Pending/Approved) + NUOVA sez. 6 Revisions diff + NUOVA sez. 7 Audit trail.
            max-width 880 centered. Single-page (NON wizard).
   Continuity con S1+S2+S3: stesso state-picker UI, theme toggle 🌗 (mai-theme), desktop frame chrome
            (.ed-desk), phone-row mobile. Entity primaria = --c-agent. Status badge riusa pr-badge (S2).
   Loadable standalone via Babel. Injects own component CSS; relies on tokens.css + components.css.

   v2 components surfaced here:
   /* v2: ProposalEditForm, EditHeader, StatusBadge, LiveDot, StatusBanner (Rejected/Pending/Approved),
          EditFooter, SaveStatusPill, SectionCard (locked/changed/resolved), ValidationPip,
          Identity/Capabilities/SystemPrompt/TestConfig/GameScope (readOnly), RevisionsSection, DiffRow,
          AuditTrailSection, AuditEvent, AuthorChip, EntityChip(game), SubmitUpdateModal, ConfirmCancelModal */

const { useState, useEffect, useMemo, useRef, useCallback } = React;

/* ──────────────────────────────────────────────────────────
   Component CSS — solo token da tokens.css / components.css.
   .ed-* = harness chrome (riuso da S1/S2/S3). .cp-* = form (riuso da S3). .ce-* = edit-specific.
   ────────────────────────────────────────────────────────── */
const CE_CSS = `
/* ─── harness (riuso esatto da S3) ─── */
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
.ed-desk { width:100%; max-width:1340px; height:860px; border-radius:var(--r-lg); overflow:hidden;
  background:var(--bg-card); border:1px solid var(--border); box-shadow:var(--shadow-lg); display:flex; flex-direction:column; }
.ed-chrome { height:38px; flex-shrink:0; display:flex; align-items:center; gap:8px; padding:0 14px;
  background:var(--bg-muted); border-bottom:1px solid var(--border); font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); }
.ed-chrome .dots { display:flex; gap:6px; }
.ed-chrome .dots i { width:11px; height:11px; border-radius:50%; display:block; }
.ed-chrome .dots i:nth-child(1){ background:#ff5f57; } .ed-chrome .dots i:nth-child(2){ background:#febc2e; } .ed-chrome .dots i:nth-child(3){ background:#28c840; }
.ed-chrome .url { flex:1; text-align:center; background:var(--bg-card); border-radius:var(--r-sm); padding:4px 10px; margin:0 12%; }
.ed-phone-row { display:flex; gap:28px; align-items:flex-start; flex-wrap:wrap; }
.ed-phone-cap { font-size:var(--fs-sm); color:var(--text-sec); max-width:300px; line-height:var(--lh-snug); }
.ed-phone-cap h4 { font-family:var(--f-display); font-size:var(--fs-base); margin-bottom:6px; }

/* ─── form app shell (riuso da S3) ─── */
.cp-app { display:flex; flex-direction:column; height:100%; min-height:0; background:var(--bg); color:var(--text); position:relative; overflow:hidden; }
.cp-app :focus-visible { outline:2px solid hsl(var(--c-agent)); outline-offset:2px; border-radius:var(--r-xs); }

/* header (sticky) */
.cp-head { flex-shrink:0; position:sticky; top:0; z-index:var(--z-sticky); background:var(--bg-card); border-bottom:1px solid var(--border); }
.cp-head .hrow { display:flex; align-items:flex-start; gap:16px; padding:14px 22px 15px; max-width:980px; margin:0 auto; }
.cp-head .htxt { min-width:0; }
.cp-bread { font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); letter-spacing:.04em; display:flex; align-items:center; gap:6px; margin-bottom:6px; }
.cp-bread .sep { opacity:.5; }
.cp-bread .cur { color:hsl(var(--c-agent)); font-weight:var(--fw-bold); }
.ce-titlerow { display:flex; align-items:center; gap:11px; flex-wrap:wrap; }
.cp-h1 { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-2xl); letter-spacing:-.01em; line-height:var(--lh-tight); }
.cp-sub { font-size:var(--fs-sm); color:var(--text-sec); margin-top:5px; max-width:620px; }
.cp-sub b { color:var(--text); font-weight:var(--fw-bold); }
.ce-metarow { display:flex; align-items:center; gap:9px; flex-wrap:wrap; margin-top:9px; }
.ce-idchip { display:inline-flex; align-items:center; gap:7px; padding:3px 7px 3px 10px; border-radius:var(--r-pill);
  background:var(--bg-muted); color:var(--text-muted); font-family:var(--f-mono); font-size:11px; font-weight:var(--fw-bold); }
.ce-idchip .copy { width:19px; height:19px; border-radius:var(--r-xs); border:none; background:transparent; color:var(--text-muted); cursor:pointer; font-size:11px; display:inline-flex; align-items:center; justify-content:center; }
.ce-idchip .copy:hover { background:var(--border-strong); color:var(--text); }
.cp-head .grow { flex:1; }
.cp-headcta { display:flex; align-items:center; gap:9px; flex-shrink:0; flex-wrap:wrap; justify-content:flex-end; max-width:300px; }

/* author chip (player entity) */
.ce-author { display:inline-flex; align-items:center; gap:6px; padding:2px 10px 2px 2px; border-radius:var(--r-pill);
  background:hsl(var(--c-player) / .14); color:hsl(var(--c-player)); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); }
.ce-author .av { width:18px; height:18px; border-radius:50%; background:hsl(var(--c-player)); color:#fff; display:flex; align-items:center; justify-content:center; font-size:9px; font-weight:var(--fw-ext); }
.ce-author .abadge { font-family:var(--f-mono); font-size:9px; font-weight:var(--fw-ext); text-transform:uppercase; letter-spacing:.04em;
  background:hsl(var(--c-warning) / .2); color:hsl(var(--c-warning)); padding:1px 5px; border-radius:var(--r-pill); }

/* status badge inline (riuso pr-badge S2) */
.ce-badge { display:inline-flex; align-items:center; gap:5px; padding:4px 11px; border-radius:var(--r-pill);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); border:1px solid transparent; white-space:nowrap; }
.ce-badge .bi { font-size:13px; line-height:1; }
.ce-badge.draft    { background:var(--bg-muted); border-color:var(--border); color:var(--text-muted); }
.ce-badge.review   { background:hsl(var(--c-warning) / .14); border-color:hsl(var(--c-warning) / .35); color:hsl(var(--c-warning)); }
.ce-badge.approved { background:hsl(var(--c-toolkit) / .14); border-color:hsl(var(--c-toolkit) / .35); color:hsl(var(--c-toolkit)); }
.ce-badge.rejected { background:hsl(var(--c-danger) / .14); border-color:hsl(var(--c-danger) / .35); color:hsl(var(--c-danger)); }
.ce-live { display:inline-flex; align-items:center; gap:5px; font-family:var(--f-mono); font-size:11px; font-weight:var(--fw-bold); color:hsl(var(--c-success)); }
.ce-live i { width:8px; height:8px; border-radius:50%; background:hsl(var(--c-success)); animation:celive 2s var(--ease-in-out) infinite; }
@keyframes celive { 0%,100%{ opacity:1; box-shadow:0 0 0 0 hsl(var(--c-success) / .5);} 50%{ opacity:.6; box-shadow:0 0 0 5px hsl(var(--c-success) / 0);} }

/* generic buttons (riuso da S3) */
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
.cp-btn.warn { background:hsl(var(--c-warning)); color:#3a2400; }
.cp-btn.warn:hover { filter:brightness(1.04); transform:translateY(-1px); }
.cp-btn.warn-out { background:transparent; border-color:hsl(var(--c-warning) / .5); color:hsl(var(--c-warning)); }
.cp-btn.warn-out:hover { background:hsl(var(--c-warning) / .1); }
.cp-btn.toolkit-out { background:transparent; border-color:hsl(var(--c-toolkit) / .5); color:hsl(var(--c-toolkit)); }
.cp-btn.toolkit-out:hover { background:hsl(var(--c-toolkit) / .1); }
.cp-btn.info-out { background:transparent; border-color:hsl(var(--c-info) / .5); color:hsl(var(--c-info)); }
.cp-btn.info-out:hover { background:hsl(var(--c-info) / .1); }
.cp-btn.danger { background:hsl(var(--c-danger)); color:#fff; }
.cp-btn.danger:hover { filter:brightness(1.04); transform:translateY(-1px); }

/* ─── status banner area (sotto header, condizionale) ─── */
.ce-banwrap { flex-shrink:0; padding:14px 22px 0; }
.ce-banwrap .inner { max-width:980px; margin:0 auto; }
.ce-banner { border-radius:var(--r-lg); padding:15px 18px; }
.ce-banner .bhead { display:flex; align-items:center; gap:10px; }
.ce-banner .bhead .bic { font-size:20px; line-height:1; flex-shrink:0; }
.ce-banner .bhead .bt { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-base); flex:1; }
.ce-banner .bhead .bwhen { font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); }
.ce-bantoggle { flex-shrink:0; width:26px; height:26px; border-radius:var(--r-sm); border:none; background:transparent; color:var(--text-sec);
  cursor:pointer; font-size:12px; display:inline-flex; align-items:center; justify-content:center; opacity:.65; transition:opacity var(--dur-sm), background var(--dur-sm); }
.ce-bantoggle:hover { opacity:1; background:var(--bg-hover); }
.ce-banner .bpeek { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); white-space:nowrap; }
.ce-banner.collapsed { padding:11px 18px; }
.ce-banner.collapsed .bhead .bt { font-size:var(--fs-sm); }
.ce-banner .bbody { font-size:var(--fs-sm); color:var(--text-sec); line-height:var(--lh-body); margin-top:9px; }
.ce-banner .bbody b { color:var(--text); font-weight:var(--fw-bold); }
.ce-banner .bsections { display:flex; align-items:center; flex-wrap:wrap; gap:7px; margin-top:11px; }
.ce-banner .bsec-lab { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); text-transform:uppercase; letter-spacing:.04em; }
.ce-secchip { display:inline-flex; align-items:center; gap:5px; padding:3px 9px; border-radius:var(--r-pill);
  background:hsl(var(--c-danger) / .12); color:hsl(var(--c-danger)); border:1px solid hsl(var(--c-danger) / .3);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:11px; cursor:pointer; }
.ce-secchip:hover { background:hsl(var(--c-danger) / .2); }
.ce-banner .bacts { display:flex; align-items:center; flex-wrap:wrap; gap:8px; margin-top:14px; }
.ce-banner .bstats { display:flex; align-items:center; flex-wrap:wrap; gap:14px; margin-top:11px; padding-top:11px; border-top:1px solid var(--border-light); }
.ce-banner .bstat { display:inline-flex; align-items:center; gap:6px; font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); }
.ce-banner .bstat b { color:var(--text); font-weight:var(--fw-bold); font-size:var(--fs-sm); }
.ce-banner.rejected  { background:hsl(var(--c-danger) / .08);  border-left:4px solid hsl(var(--c-danger)); }
.ce-banner.review    { background:hsl(var(--c-warning) / .08); border-left:4px solid hsl(var(--c-warning)); }
.ce-banner.approved  { background:hsl(var(--c-toolkit) / .08); border-left:4px solid hsl(var(--c-toolkit)); }
.ce-banner.rejected .bhead .bt { color:hsl(var(--c-danger)); }
.ce-banner.review .bhead .bt   { color:hsl(var(--c-warning)); }
.ce-banner.approved .bhead .bt { color:hsl(var(--c-toolkit)); }

/* save pill bar (sopra footer) */
.cp-savebar { flex-shrink:0; display:flex; justify-content:flex-end; padding:7px 22px; background:var(--bg); }
.cp-savebar .inner { max-width:980px; width:100%; margin:0 auto; display:flex; justify-content:flex-end; }
.cp-pill { display:inline-flex; align-items:center; gap:6px; padding:4px 11px; border-radius:var(--r-pill);
  font-family:var(--f-mono); font-size:var(--fs-xs); font-weight:var(--fw-bold); }
.cp-pill.unsaved { background:hsl(var(--c-warning) / .14); color:hsl(var(--c-warning)); }
.cp-pill.saved   { background:hsl(var(--c-success) / .14); color:hsl(var(--c-success)); }
.cp-pill.readonly{ background:var(--bg-muted); color:var(--text-muted); }
.cp-pill.dirty   { background:hsl(var(--c-info) / .14); color:hsl(var(--c-info)); }
.cp-pill .dot { width:7px; height:7px; border-radius:50%; background:currentColor; }
.cp-pill.dirty .dot { animation:cppulse 1.1s var(--ease-in-out) infinite; }
@keyframes cppulse { 0%,100%{ opacity:1; transform:scale(1);} 50%{ opacity:.3; transform:scale(.55);} }

/* footer (sticky bottom) */
.cp-foot { flex-shrink:0; background:var(--bg-card); border-top:1px solid var(--border); }
.cp-foot .frow { display:flex; align-items:center; gap:10px; padding:13px 22px; max-width:980px; margin:0 auto; flex-wrap:wrap; }
.cp-foot .grow { flex:1; }

/* scroll body + form column */
.cp-body { flex:1; overflow:auto; min-height:0; padding:22px 22px 32px; }
.cp-form { max-width:880px; margin:0 auto; display:flex; flex-direction:column; gap:16px; }

/* section card (accordion, riuso da S3 + lock/changed/resolved) */
.cp-sec { background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-lg); overflow:hidden; transition:border-color var(--dur-sm) var(--ease-out); }
.cp-sec.has-error { border-color:hsl(var(--c-danger) / .4); }
.cp-sec.is-valid { border-color:hsl(var(--c-toolkit) / .32); }
.cp-sec.affected { border-color:hsl(var(--c-danger) / .45); box-shadow:0 0 0 1px hsl(var(--c-danger) / .15); }
.cp-sec.resolved { border-color:hsl(var(--c-toolkit) / .4); }
.cp-sec.locked { background:var(--bg); }
.cp-sechead { display:flex; align-items:center; gap:13px; width:100%; padding:15px 18px; background:transparent; border:none; text-align:left; cursor:pointer; }
.cp-sechead:hover { background:var(--bg-hover); }
.cp-secnum { position:relative; flex-shrink:0; width:26px; height:26px; border-radius:var(--r-sm); display:flex; align-items:center; justify-content:center;
  background:hsl(var(--c-agent) / .14); color:hsl(var(--c-agent)); font-family:var(--f-mono); font-weight:var(--fw-ext); font-size:var(--fs-sm); }
.cp-sec.locked .cp-secnum { background:var(--bg-muted); color:var(--text-muted); }
.cp-secnum .lock { position:absolute; right:-6px; bottom:-6px; width:15px; height:15px; border-radius:50%; background:var(--bg-card); border:1px solid var(--border);
  display:flex; align-items:center; justify-content:center; font-size:8px; }
.cp-sectitle { min-width:0; flex:1; }
.cp-sectitle .t { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-lg); line-height:var(--lh-tight); display:flex; align-items:center; gap:8px; }
.cp-sectitle .s { font-size:var(--fs-sm); color:var(--text-sec); margin-top:3px; }
.cp-vpip { flex-shrink:0; display:inline-flex; align-items:center; gap:5px; padding:3px 10px; border-radius:var(--r-pill);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); white-space:nowrap; }
.cp-vpip.valid { background:hsl(var(--c-toolkit) / .14); color:hsl(var(--c-toolkit)); }
.cp-vpip.issues { background:hsl(var(--c-danger) / .14); color:hsl(var(--c-danger)); }
.cp-vpip.incomplete { background:var(--bg-muted); color:var(--text-muted); }
.cp-vpip.optional { background:var(--bg-muted); color:var(--text-muted); }
.cp-vpip.locked { background:var(--bg-muted); color:var(--text-muted); }
.cp-vpip.changed { background:hsl(var(--c-info) / .14); color:hsl(var(--c-info)); }
.cp-vpip.resolved { background:hsl(var(--c-toolkit) / .14); color:hsl(var(--c-toolkit)); }
.cp-vpip.count { background:var(--bg-muted); color:var(--text-muted); font-family:var(--f-mono); }
.cp-caret { flex-shrink:0; color:var(--text-muted); font-size:11px; transition:transform var(--dur-sm) var(--ease-out); width:16px; text-align:center; }
.cp-sec.open .cp-caret { transform:rotate(90deg); }
.cp-secbody { padding:4px 18px 20px; border-top:1px solid var(--border-light); }
.cp-sec.locked .cp-secbody { background:var(--bg-muted); }

/* fields (riuso da S3) */
.cp-field { margin-bottom:16px; }
.cp-field:last-child { margin-bottom:0; }
.cp-label { display:flex; align-items:center; gap:6px; font-family:var(--f-display); font-size:var(--fs-xs); font-weight:var(--fw-bold);
  color:var(--text-sec); margin-bottom:7px; text-transform:uppercase; letter-spacing:.05em; }
.cp-label .req { color:hsl(var(--c-danger)); }
.cp-label .grow { flex:1; }
.cp-label .chgmark { display:inline-flex; align-items:center; gap:4px; font-family:var(--f-mono); font-size:9px; font-weight:var(--fw-ext); text-transform:none; letter-spacing:0;
  color:hsl(var(--c-warning)); background:hsl(var(--c-warning) / .14); padding:1px 7px; border-radius:var(--r-pill); }
.cp-counter { font-family:var(--f-mono); font-size:10px; font-weight:var(--fw-bold); color:var(--text-muted); text-transform:none; letter-spacing:0; }
.cp-counter.over { color:hsl(var(--c-danger)); }
.cp-inwrap { position:relative; }
.cp-input, .cp-textarea { width:100%; padding:10px 12px; border-radius:var(--r-md); border:1.5px solid var(--border);
  background:var(--bg-card); font-family:var(--f-body); font-size:var(--fs-base); color:var(--text); outline:none; transition:border-color var(--dur-sm), box-shadow var(--dur-sm); }
.cp-input:focus, .cp-textarea:focus { border-color:hsl(var(--c-agent) / .55); box-shadow:0 0 0 3px hsl(var(--c-agent) / .14); }
.cp-input.err, .cp-textarea.err { border-color:hsl(var(--c-danger) / .6); }
.cp-input.chg, .cp-textarea.chg { border-color:hsl(var(--c-warning) / .5); }
.cp-input[readonly], .cp-textarea[readonly] { background:var(--bg-muted); color:var(--text-sec); cursor:not-allowed; opacity:.85; }
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

/* icon picker / colors */
.cp-icons { display:flex; flex-wrap:wrap; gap:8px; }
.cp-icon { width:46px; height:46px; border-radius:var(--r-md); border:1.5px solid var(--border); background:var(--bg-card);
  display:flex; align-items:center; justify-content:center; font-size:21px; cursor:pointer; transition:all var(--dur-sm) var(--ease-out); }
.cp-icon:hover { border-color:var(--border-strong); transform:translateY(-1px); }
.cp-icon.on { border-color:hsl(var(--c-agent)); background:hsl(var(--c-agent) / .12); box-shadow:0 0 0 3px hsl(var(--c-agent) / .22); }
.cp-icon:disabled { cursor:not-allowed; opacity:.6; }
.cp-colors { display:flex; flex-wrap:wrap; gap:9px; align-items:center; }
.cp-swatch { width:30px; height:30px; border-radius:var(--r-sm); cursor:pointer; border:2px solid transparent; position:relative; transition:transform var(--dur-sm) var(--ease-out); }
.cp-swatch:hover { transform:scale(1.08); }
.cp-swatch.on { border-color:var(--text); box-shadow:0 0 0 2px var(--bg-card), 0 0 0 4px var(--border-strong); }
.cp-swatch.on::after { content:'✓'; position:absolute; inset:0; display:flex; align-items:center; justify-content:center; color:#fff; font-size:13px; font-weight:900; text-shadow:0 1px 2px rgba(0,0,0,.4); }
.cp-swatch:disabled { cursor:not-allowed; }

/* capability grid */
.cp-caps { display:grid; grid-template-columns:repeat(auto-fill, minmax(232px, 1fr)); gap:9px; }
.cp-cap { display:flex; align-items:flex-start; gap:10px; padding:11px 12px; border-radius:var(--r-md); border:1.5px solid var(--border);
  background:var(--bg-card); cursor:pointer; text-align:left; transition:all var(--dur-sm) var(--ease-out); position:relative; }
.cp-cap:hover { background:var(--bg-hover); border-color:var(--border-strong); }
.cp-cap.on { background:hsl(var(--c-agent) / .12); border-color:hsl(var(--c-agent) / .45); }
.cp-cap:disabled { cursor:not-allowed; }
.cp-cap:disabled:hover { background:var(--bg-card); border-color:var(--border); }
.cp-cap.on:disabled:hover { background:hsl(var(--c-agent) / .12); border-color:hsl(var(--c-agent) / .45); }
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
.cp-code-wrap.chg { border-color:hsl(var(--c-warning) / .5); }
.cp-code-layer { margin:0; padding:13px 15px; font-family:var(--f-mono); font-size:13px; line-height:1.6; white-space:pre-wrap; word-break:break-word; }
.cp-code-hl { position:absolute; inset:0; pointer-events:none; color:var(--text); }
.cp-code-hl .v { color:hsl(var(--c-game)); font-weight:var(--fw-bold); background:hsl(var(--c-game) / .12); border-radius:3px; padding:0 2px; }
.cp-code-hl .bt { color:hsl(var(--c-tool)); }
.cp-code-hl .ph { color:var(--text-muted); }
.cp-code-ta { position:relative; display:block; width:100%; border:none; outline:none; resize:none; background:transparent;
  color:transparent; caret-color:hsl(var(--c-agent)); overflow:hidden; }
.cp-code-ta:read-only { caret-color:transparent; cursor:not-allowed; }
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

/* ─── Sezione 6: Revisions diff ─── */
.ce-comparebar { display:flex; align-items:center; gap:11px; flex-wrap:wrap; padding:10px 13px; border-radius:var(--r-md); background:var(--bg-muted); margin-bottom:14px; }
.ce-comparebar .cl { font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); text-transform:uppercase; letter-spacing:.04em; }
.ce-cmpsel { display:inline-flex; align-items:center; gap:6px; padding:5px 10px; border-radius:var(--r-sm); border:1px solid var(--border); background:var(--bg-card);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); color:var(--text); cursor:pointer; }
.ce-cmpsel .vv { color:var(--text-muted); font-family:var(--f-mono); font-weight:var(--fw-bold); }
.ce-cmpsel .car { font-size:9px; color:var(--text-muted); }
.ce-cmparrow { color:var(--text-muted); font-size:13px; }
.ce-difflist { display:flex; flex-direction:column; gap:13px; }
.ce-diff { border:1px solid var(--border); border-radius:var(--r-md); overflow:hidden; background:var(--bg-card); }
.ce-diffhead { display:flex; align-items:center; gap:9px; padding:10px 13px; background:var(--bg); border-bottom:1px solid var(--border-light); }
.ce-diffhead .df { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); }
.ce-diffhead .dref { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); }
.ce-diffhead .grow { flex:1; }
.ce-diffhead .goto { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); color:hsl(var(--c-agent)); background:none; border:none; cursor:pointer; padding:3px 6px; border-radius:var(--r-xs); }
.ce-diffhead .goto:hover { background:hsl(var(--c-agent) / .1); }
.ce-diffbody { font-family:var(--f-mono); font-size:13px; line-height:1.55; padding:8px 0; }
.ce-dline { display:flex; gap:9px; padding:1px 13px; white-space:pre-wrap; word-break:break-word; }
.ce-dline .pfx { flex-shrink:0; width:11px; text-align:center; font-weight:var(--fw-bold); opacity:.8; user-select:none; }
.ce-dline.ctx { color:var(--text-muted); }
.ce-dline.add { background:hsl(var(--c-toolkit) / .15); border-left:3px solid hsl(var(--c-toolkit)); }
.ce-dline.add .pfx, .ce-dline.add { color:hsl(var(--c-toolkit)); }
.ce-dline.del { background:hsl(var(--c-danger) / .1); border-left:3px solid hsl(var(--c-danger)); }
.ce-dline.del .pfx, .ce-dline.del { color:hsl(var(--c-danger)); }
.ce-diffsum { display:flex; align-items:center; gap:8px; margin-top:14px; padding:11px 13px; border-radius:var(--r-md); background:var(--bg-muted);
  font-size:var(--fs-sm); color:var(--text-sec); }
.ce-diffsum b { color:var(--text); font-weight:var(--fw-bold); }
.ce-difftoggle { display:flex; align-items:center; gap:10px; margin-top:13px; padding:11px 13px; border-radius:var(--r-md); border:1px solid var(--border); }
.ce-difftoggle .dtt { flex:1; }
.ce-difftoggle .dtt .dtl { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); }
.ce-difftoggle .dtt .dts { font-size:var(--fs-xs); color:var(--text-muted); margin-top:2px; }
.ce-switch { flex-shrink:0; width:42px; height:24px; border-radius:var(--r-pill); border:none; background:var(--border-strong); position:relative; cursor:pointer; transition:background var(--dur-sm) var(--ease-out); }
.ce-switch::after { content:''; position:absolute; top:3px; left:3px; width:18px; height:18px; border-radius:50%; background:#fff; box-shadow:var(--shadow-xs); transition:transform var(--dur-sm) var(--ease-spring); }
.ce-switch.on { background:hsl(var(--c-warning)); }
.ce-switch.on::after { transform:translateX(18px); }
.ce-diffempty { text-align:center; padding:24px; color:var(--text-muted); font-size:var(--fs-sm); }
.ce-diffempty .em { font-size:28px; opacity:.7; display:block; margin-bottom:8px; }

/* ─── Sezione 7: Audit trail ─── */
.ce-timeline { display:flex; flex-direction:column; }
.ce-event { display:flex; gap:13px; }
.ce-evcol { display:flex; flex-direction:column; align-items:center; flex-shrink:0; }
.ce-evdot { width:26px; height:26px; border-radius:50%; display:flex; align-items:center; justify-content:center; font-size:12px; flex-shrink:0; z-index:1;
  border:2px solid var(--bg-card); }
.ce-evdot.created  { background:hsl(var(--c-success) / .18); color:hsl(var(--c-success)); }
.ce-evdot.submit   { background:hsl(var(--c-agent) / .18); color:hsl(var(--c-agent)); }
.ce-evdot.review   { background:hsl(var(--c-warning) / .18); color:hsl(var(--c-warning)); }
.ce-evdot.approved { background:hsl(var(--c-toolkit) / .18); color:hsl(var(--c-toolkit)); }
.ce-evdot.rejected { background:hsl(var(--c-danger) / .18); color:hsl(var(--c-danger)); }
.ce-evdot.edited   { background:hsl(var(--c-info) / .18); color:hsl(var(--c-info)); }
.ce-evline { flex:1; width:0; border-left:2px dashed var(--border); margin:2px 0; min-height:14px; }
.ce-event:last-child .ce-evline { display:none; }
.ce-evbody { flex:1; min-width:0; padding-bottom:16px; }
.ce-event:last-child .ce-evbody { padding-bottom:0; }
.ce-evtop { display:flex; align-items:baseline; gap:9px; flex-wrap:wrap; }
.ce-evlabel { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); color:var(--text); }
.ce-evwhen { font-family:var(--f-mono); font-size:11px; color:var(--text-muted); }
.ce-evmeta { display:flex; align-items:center; gap:8px; flex-wrap:wrap; margin-top:7px; }
.ce-evreason { margin-top:8px; padding:8px 11px; border-radius:var(--r-sm); background:hsl(var(--c-danger) / .07); border-left:3px solid hsl(var(--c-danger) / .6);
  font-size:var(--fs-sm); color:var(--text-sec); line-height:var(--lh-snug); }
.ce-evreason b { color:hsl(var(--c-danger)); font-weight:var(--fw-bold); }
.ce-evreason a { color:hsl(var(--c-danger)); text-decoration:underline; font-weight:var(--fw-bold); cursor:pointer; white-space:nowrap; }

/* overlay + modal (riuso da S3) */
.cp-modal-bd { position:absolute; inset:0; z-index:var(--z-modal); background:rgba(20,16,10,.42); backdrop-filter:blur(2px); display:flex; align-items:center; justify-content:center; padding:24px; }
[data-theme="dark"] .cp-modal-bd { background:rgba(0,0,0,.6); }
.cp-modal { width:100%; max-width:460px; background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-xl); box-shadow:var(--shadow-lg); overflow:hidden; animation:cpmodal var(--dur-md) var(--ease-out); }
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
.cp-modal .msrow .k { font-family:var(--f-mono); font-size:11px; text-transform:uppercase; letter-spacing:.04em; color:var(--text-muted); width:120px; flex-shrink:0; }
.cp-modal .msrow .vv { color:var(--text); font-weight:var(--fw-semi); min-width:0; }
.cp-modal .msrow .vv.add { color:hsl(var(--c-toolkit)); }
.cp-modal .mfoot { display:flex; gap:9px; padding:14px 20px; background:var(--bg); border-top:1px solid var(--border); }
.cp-modal .mfoot .grow { flex:1; }

/* ─── mobile ─── */
.cp-app.is-mobile .cp-head .hrow { flex-wrap:wrap; padding:12px 14px 13px; }
.cp-app.is-mobile .cp-headcta { display:none; }
.cp-app.is-mobile .cp-h1 { font-size:var(--fs-xl); }
.cp-app.is-mobile .ce-banwrap { padding:12px 13px 0; }
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
.cp-app.is-mobile .ce-comparebar { gap:7px; }

@media (prefers-reduced-motion: reduce) {
  .ce-live i, .cp-pill.dirty .dot, .cp-unique .sp, .cp-modal, .ce-switch::after { animation:none; transition:none; }
}
`;

/* ──────────────────────────────────────────────────────────
   Static config (riuso da S3)
   ────────────────────────────────────────────────────────── */
const AUTHOR = { name: 'Marco R.', initials: 'MR' };
const ADMIN = { name: 'Sara T.', initials: 'ST' };

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

/* prompt v3 (bozza corrente, generalizzato) */
const PROMPT_V3 =
`Sei un esperto delle regole ufficiali di {game}. Rispondi alle domande del {player} citando sempre il manuale e indicando la pagina di riferimento quando possibile.

Adatta le risposte all'archetipo euro-game: gestione risorse, piazzamento e ottimizzazione del punteggio. Se una regola è ambigua, spiega le interpretazioni più diffuse e segnala quella ufficiale. Mantieni un tono chiaro e amichevole, adatto anche a chi gioca per la prima volta. Non inventare regole non presenti in {rules_kb}.`;

/* ──────────────────────────────────────────────────────────
   Base proposal (Catan Rules Expert · v3) + meta
   ────────────────────────────────────────────────────────── */
const BASE_FORM = {
  name: 'Catan Rules Expert',
  desc: 'Esperto delle regole ufficiali di Catan: risponde su preparazione, commercio, ladrone e calcolo dei punti vittoria citando sempre il manuale.',
  icon: '📋', color: 'agent',
  caps: ['qa', 'stream', 'web'],
  prompt: PROMPT_V3,
  samples: [
    { in: 'Quante carte risorsa si pescano durante il setup?', out: 'Nel setup ogni giocatore riceve risorse dai terreni adiacenti al suo secondo insediamento, una carta per ciascun terreno (escluso il deserto).' },
    { in: 'Il ladrone blocca anche la produzione del 7?', out: 'Il ladrone non blocca un numero: quando esce il 7 nessuno produce, si attiva il ladrone e si scartano le carte oltre le 7 in mano.' },
  ],
  scope: ['g-catan'],
};
const META = {
  id: 'tp-catan-rules-3',
  version: 3,
  createdAgo: '5 giorni fa', createdAbs: '28 mag 2026, 09:12',
  modAgo: '2 ore fa', modAbs: '2 giu 2026, 16:04',
};

/* feedback admin (per Rejected) */
const REJECT_FEEDBACK = {
  ago: '2 giorni fa', abs: '31 mag 2026, 11:40',
  body: 'Il prompt sistema include riferimenti a giochi specifici che non sono nello scope. Generalizza per coprire l\u2019archetipo "euro-game" invece di nominare Catan esplicitamente nelle istruzioni base.',
  sections: [['System prompt', 3, 'prompt'], ['Scope giochi', 5, 'scope']],
};

/* ──────────────────────────────────────────────────────────
   Revisions diff — v3 (bozza) vs v2 (approvata 28 mag)
   ────────────────────────────────────────────────────────── */
const REVISIONS = [
  { id: 'r1', field: 'System prompt', section: 3, sectionId: 'prompt',
    lines: [
      ['del', 'Sei un esperto delle regole di Catan e dei giochi di piazzamento tedeschi.'],
      ['add', 'Sei un esperto delle regole ufficiali di {game}. Rispondi alle domande del {player}'],
      ['add', 'citando sempre il manuale e indicando la pagina di riferimento quando possibile.'],
      ['ctx', 'Se una regola è ambigua, spiega le interpretazioni più diffuse.'],
    ] },
  { id: 'r2', field: 'System prompt', section: 3, sectionId: 'prompt',
    lines: [
      ['ctx', 'Mantieni un tono chiaro e amichevole.'],
      ['del', 'Concentrati sulle meccaniche specifiche di Catan: commercio e ladrone.'],
      ['add', "Adatta le risposte all'archetipo euro-game: gestione risorse, piazzamento"],
      ['add', 'e ottimizzazione del punteggio.'],
    ] },
  { id: 'r3', field: 'Scope giochi', section: 5, sectionId: 'scope',
    lines: [
      ['del', 'Scope: Catan, Power Grid (2 giochi)'],
      ['add', 'Scope: Catan (1 gioco)'],
    ] },
];

/* ──────────────────────────────────────────────────────────
   Audit trail — 8 eventi (storia v2→v3)
   ────────────────────────────────────────────────────────── */
const AUDIT = [
  { id: 'a1', type: 'created',  label: 'Creata',                  ago: '7 giorni fa', iso: '2026-05-26T20:05', who: AUTHOR },
  { id: 'a2', type: 'submit',   label: 'Inviata per review (v2)', ago: '6 giorni fa', iso: '2026-05-27T10:30', who: AUTHOR },
  { id: 'a3', type: 'review',   label: 'In review',               ago: '6 giorni fa', iso: '2026-05-27T10:31', who: { name: 'Sistema', initials: 'SY', system: true } },
  { id: 'a4', type: 'approved', label: 'Approvata · v2 live',     ago: '5 giorni fa', iso: '2026-05-28T09:12', who: ADMIN, admin: true },
  { id: 'a5', type: 'edited',   label: 'Nuova versione v3 (clone)', ago: '3 giorni fa', iso: '2026-05-30T18:48', who: AUTHOR },
  { id: 'a6', type: 'submit',   label: 'Inviata per review (v3)', ago: '3 giorni fa', iso: '2026-05-30T19:02', who: AUTHOR },
  { id: 'a7', type: 'rejected', label: 'Rifiutata',               ago: '2 giorni fa', iso: '2026-05-31T11:40', who: ADMIN, admin: true,
    reason: 'Prompt include riferimenti specifici a Catan fuori scope. Generalizza per l\u2019archetipo euro-game.' },
  { id: 'a8', type: 'edited',   label: 'Edit ripreso',            ago: '1 ora fa',    iso: '2026-06-02T15:10', who: AUTHOR },
];

/* ──────────────────────────────────────────────────────────
   Scenari (10 stati)
   status ∈ Draft · PendingReview · Approved · Rejected
   ────────────────────────────────────────────────────────── */
const SCENARIOS = {
  'draft-edit':          { status: 'Draft',         open: [1, 2, 3, 4, 5], savePill: 'saved',    addressed: [], dirty: false, ui: {} },
  'draft-edit-dirty':    { status: 'Draft',         open: [1, 3],          savePill: 'unsaved',  addressed: [], dirty: true,  ui: {} },
  'pending-review':      { status: 'PendingReview', open: [1, 2, 3],       savePill: 'readonly', addressed: [], dirty: false, ui: {} },
  'approved-readonly':   { status: 'Approved',      open: [1, 3],          savePill: 'readonly', addressed: [], dirty: false, ui: {} },
  'approved-with-stats': { status: 'Approved',      open: [1],             savePill: 'readonly', addressed: [], dirty: false, ui: { stats: true } },
  'rejected-fresh':      { status: 'Rejected',      open: [3, 5],          savePill: 'saved',    addressed: [], dirty: false, ui: {} },
  'rejected-addressing': { status: 'Rejected',      open: [3, 5],          savePill: 'dirty',    addressed: ['prompt'], dirty: true, ui: {} },
  'revisions-expanded':  { status: 'Rejected',      open: [6],             savePill: 'dirty',    addressed: ['prompt'], dirty: true, ui: { diffInline: false } },
  'audit-trail-expanded':{ status: 'Rejected',      open: [7],             savePill: 'saved',    addressed: [], dirty: false, ui: {} },
  'submitting-update':   { status: 'Rejected',      open: [],              savePill: 'dirty',    addressed: ['prompt', 'scope'], dirty: true, ui: { modal: true } },
};
const STATE_LIST = [
  ['draft-edit', 'Draft'], ['draft-edit-dirty', 'Draft · dirty'], ['pending-review', 'Pending'],
  ['approved-readonly', 'Approved'], ['approved-with-stats', 'Approved · stats'],
  ['rejected-fresh', 'Rejected'], ['rejected-addressing', 'Rejected · addressing'],
  ['revisions-expanded', 'Revisions'], ['audit-trail-expanded', 'Audit trail'], ['submitting-update', 'Submitting'],
];

const STATUS_META = {
  Draft:         { cls: 'draft',    icon: '📝', label: 'Draft' },
  PendingReview: { cls: 'review',   icon: '⏳', label: 'In review' },
  Approved:      { cls: 'approved', icon: '✓', label: 'Approvata' },
  Rejected:      { cls: 'rejected', icon: '✗', label: 'Rifiutata' },
};

/* ──────────────────────────────────────────────────────────
   Helpers
   ────────────────────────────────────────────────────────── */
function gameById(id) { return GAMES.find(g => g.id === id); }
function highlightPrompt(text) {
  if (!text) return [<span className="ph" key="ph">Definisci il comportamento dell&rsquo;agente…</span>];
  const parts = text.split(/(\{[a-z_]+\}|`[^`]*`)/g);
  return parts.map((p, i) => {
    if (/^\{[a-z_]+\}$/.test(p)) return <span className="v" key={i}>{p}</span>;
    if (/^`[^`]*`$/.test(p)) return <span className="bt" key={i}>{p}</span>;
    return <React.Fragment key={i}>{p}</React.Fragment>;
  });
}

/* ──────────────────────────────────────────────────────────
   Shared sub-components
   ────────────────────────────────────────────────────────── */
function AuthorChip({ who, admin }) {
  return (
    <span className="ce-author" title={'Autore: ' + who.name}>
      <span className="av" aria-hidden="true">{who.initials}</span>{who.name}
      {admin && <span className="abadge">admin</span>}
    </span>
  );
}

function CharCounter({ value, max }) {
  const over = value > max;
  return <span className={'cp-counter' + (over ? ' over' : '')}>{value} / {max}</span>;
}

function AutoTextarea({ value, onChange, max, minH = 74, readOnly, changed, ...rest }) {
  const ref = useRef(null);
  const fit = useCallback(() => { const el = ref.current; if (!el) return; el.style.height = 'auto'; el.style.height = Math.max(minH, el.scrollHeight) + 'px'; }, [minH]);
  useEffect(() => { fit(); }, [value, fit]);
  return (
    <textarea ref={ref} className={'cp-textarea' + (changed ? ' chg' : '')} value={value} readOnly={readOnly}
      onChange={e => { if (!readOnly && (!max || e.target.value.length <= max + 40)) onChange(e.target.value); }} {...rest} />
  );
}

function UniqueCheck({ status }) {
  if (status === 'ok') return <span className="cp-unique ok" role="status"><span aria-hidden="true">✓</span> Disponibile</span>;
  if (status === 'dup') return <span className="cp-unique dup" role="status"><span aria-hidden="true">✗</span> Già in uso</span>;
  return null;
}

function ChangedMark() { return <span className="chgmark"><span aria-hidden="true">●</span> modificato dopo feedback</span>; }

/* section card (accordion) — locked / changed / resolved / affected */
function SectionCard({ num, id, title, subtitle, locked, status, statusCount, affected, changed, resolved, open, onToggle, children, bodyRef }) {
  let cls = 'cp-sec' + (open ? ' open' : '');
  if (locked) cls += ' locked';
  else if (resolved) cls += ' resolved';
  else if (affected) cls += ' affected';
  else if (status === 'issues') cls += ' has-error';
  else if (status === 'valid') cls += ' is-valid';

  let pip;
  if (locked) pip = <span className="cp-vpip locked"><span aria-hidden="true">🔒</span> Sola lettura</span>;
  else if (resolved) pip = <span className="cp-vpip resolved"><span aria-hidden="true">✓</span> Risolta</span>;
  else if (changed) pip = <span className="cp-vpip changed"><span aria-hidden="true">●</span> Modificata</span>;
  else if (typeof statusCount === 'string') pip = <span className="cp-vpip count">{statusCount}</span>;
  else if (status === 'valid') pip = <span className="cp-vpip valid"><span aria-hidden="true">✓</span> Completa</span>;
  else if (status === 'issues') pip = <span className="cp-vpip issues" role="status"><span aria-hidden="true">⚠</span> {statusCount} {statusCount === 1 ? 'problema' : 'problemi'}</span>;
  else if (status === 'optional') pip = <span className="cp-vpip optional"><span aria-hidden="true">·</span> Opzionale</span>;
  else pip = <span className="cp-vpip incomplete"><span aria-hidden="true">·</span> Da completare</span>;

  return (
    <section className={cls} role="region" aria-labelledby={'sec-' + id + '-t'}>
      <button className="cp-sechead" aria-expanded={open} aria-controls={'sec-' + id + '-b'} onClick={onToggle}>
        <span className="cp-secnum" aria-hidden="true">{num}{locked && <span className="lock">🔒</span>}</span>
        <span className="cp-sectitle">
          <span className="t" id={'sec-' + id + '-t'}>{title}</span>
          <span className="s">{subtitle}</span>
        </span>
        {pip}
        <span className="cp-caret" aria-hidden="true">▸</span>
      </button>
      {open && <div className="cp-secbody" id={'sec-' + id + '-b'} role="group" ref={bodyRef}>{children}</div>}
    </section>
  );
}

/* ─── Section 1: Identity (riuso S3) ─── */
function IdentitySection({ form, set, readOnly, uniqueStatus, showValidation }) {
  return (
    <>
      <div className="cp-field">
        <label className="cp-label" htmlFor="f-name">Nome <span className="req">*</span><span className="grow" /><CharCounter value={form.name.length} max={80} /></label>
        <div className="cp-inwrap">
          <input id="f-name" className="cp-input haspad" value={form.name} readOnly={readOnly}
            maxLength={84} placeholder="es. Catan Rules Expert" onChange={e => set('name', e.target.value)} />
          {!readOnly && <UniqueCheck status={uniqueStatus} />}
        </div>
      </div>
      <div className="cp-field">
        <label className="cp-label" htmlFor="f-desc">Descrizione <span className="req">*</span><span className="grow" /><CharCounter value={form.desc.length} max={500} /></label>
        <AutoTextarea id="f-desc" value={form.desc} max={500} minH={70} readOnly={readOnly}
          placeholder="Descrivi cosa fa questa typology e quando usarla…" onChange={v => set('desc', v)} />
      </div>
      <div className="cp-field">
        <label className="cp-label">Icona <span className="req">*</span></label>
        <div className="cp-icons" role="radiogroup" aria-label="Scegli icona">
          {ICONS.map(ic => (
            <button key={ic} type="button" role="radio" aria-checked={form.icon === ic} aria-label={'Icona ' + ic} disabled={readOnly}
              className={'cp-icon' + (form.icon === ic ? ' on' : '')} onClick={() => !readOnly && set('icon', ic)}>{ic}</button>
          ))}
        </div>
      </div>
      <div className="cp-field">
        <label className="cp-label">Colore entity</label>
        <div className="cp-colors" role="radiogroup" aria-label="Scegli colore entity">
          {ENTITY_COLORS.map(([key, h, s, l]) => (
            <button key={key} type="button" role="radio" aria-checked={form.color === key} aria-label={'Colore ' + key} disabled={readOnly}
              className={'cp-swatch' + (form.color === key ? ' on' : '')} style={{ background: `hsl(${h} ${s}% ${l}%)` }}
              onClick={() => !readOnly && set('color', key)} />
          ))}
        </div>
      </div>
    </>
  );
}

/* ─── Section 2: Capabilities (riuso S3) ─── */
function CapabilitiesSection({ form, set, readOnly }) {
  const toggle = id => { if (readOnly) return; set('caps', form.caps.includes(id) ? form.caps.filter(c => c !== id) : [...form.caps, id]); };
  return (
    <>
      <div className="cp-caps" role="group" aria-label="Seleziona capability">
        {CAPABILITIES.map(c => {
          const on = form.caps.includes(c.id);
          return (
            <button key={c.id} type="button" className={'cp-cap' + (on ? ' on' : '')} aria-pressed={on} disabled={readOnly} onClick={() => toggle(c.id)}>
              <span className="cic" aria-hidden="true">{c.icon}</span>
              <span className="cbody"><span className="ct">{c.label}</span><span className="cd">{c.desc}</span></span>
              <span className="ck" aria-hidden="true">✓</span>
            </button>
          );
        })}
      </div>
      {!readOnly && <div className="cp-tip"><span className="ti" aria-hidden="true">💡</span><span className="tt">Le capability sono <b>tassonomia interna</b>: determinano quali tool e modalità l&rsquo;agente potrà usare in chat.</span></div>}
    </>
  );
}

/* ─── Section 3: System prompt (riuso S3) ─── */
function CodeEditor({ value, onChange, readOnly, changed }) {
  const taRef = useRef(null);
  const hlRef = useRef(null);
  const [focus, setFocus] = useState(false);
  const fit = useCallback(() => {
    const ta = taRef.current; if (!ta) return;
    ta.style.height = 'auto';
    ta.style.height = Math.min(Math.max(150, ta.scrollHeight), 460) + 'px';
  }, []);
  useEffect(() => { fit(); }, [value, fit]);
  const syncScroll = () => { if (hlRef.current && taRef.current) hlRef.current.scrollTop = taRef.current.scrollTop; };
  return (
    <div className={'cp-code-wrap' + (focus ? ' focus' : '') + (changed ? ' chg' : '')}>
      <pre className="cp-code-layer cp-code-hl" ref={hlRef} aria-hidden="true">{highlightPrompt(value)}</pre>
      <textarea ref={taRef} className="cp-code-layer cp-code-ta" value={value} readOnly={readOnly} spellCheck={false}
        aria-label="System prompt" rows={6} onScroll={syncScroll}
        onFocus={() => setFocus(true)} onBlur={() => setFocus(false)}
        onChange={e => { if (!readOnly && e.target.value.length <= 4000) onChange(e.target.value); }} />
    </div>
  );
}

function SystemPromptSection({ form, set, readOnly, changed }) {
  return (
    <>
      {!readOnly && (
        <div className="cp-ptoolbar">
          <button type="button" className="cp-ptool"><span aria-hidden="true">▤</span> Templates</button>
          <span className="grow" />
          <button type="button" className="cp-ptool play" title="Apri il playground di test (S5)"><span aria-hidden="true">🧪</span> Testa nel playground</button>
        </div>
      )}
      {changed && !readOnly && <div className="cp-label" style={{ marginBottom: 8 }}><span className="grow" /><ChangedMark /></div>}
      <CodeEditor value={form.prompt} onChange={v => set('prompt', v)} readOnly={readOnly} changed={changed} />
      <div className="cp-codefoot"><span className="grow" /><CharCounter value={form.prompt.length} max={4000} /></div>
      {!readOnly && <div className="cp-tip"><span className="ti" aria-hidden="true">💡</span><span className="tt">Variabili: <code>{'{game}'}</code> <code>{'{player}'}</code> <code>{'{turn}'}</code> <code>{'{rules_kb}'}</code>. Sostituite a runtime.</span></div>}
    </>
  );
}

/* ─── Section 4: Test config (riuso S3) ─── */
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
              {!readOnly && <button type="button" className="sdel" aria-label={'Elimina esempio ' + (i + 1)} onClick={() => delSample(i)}>🗑</button>}</div>
            <div className="sgrid">
              <div className="scol"><div className="sl">Input utente</div>
                <AutoTextarea value={s.in} minH={62} readOnly={readOnly} placeholder="es. Quante carte si pescano in setup?" onChange={v => editSample(i, 'in', v)} /></div>
              <div className="scol"><div className="sl">Output atteso</div>
                <AutoTextarea value={s.out} minH={62} readOnly={readOnly} placeholder="es. In Catan classico ogni giocatore parte con 2 insediamenti…" onChange={v => editSample(i, 'out', v)} /></div>
            </div>
          </div>
        ))}
        {!readOnly && (
          <button type="button" className="cp-addbtn" disabled={form.samples.length >= 3} onClick={addSample}>
            <span aria-hidden="true">＋</span> Aggiungi esempio {form.samples.length > 0 && `(${form.samples.length}/3)`}
          </button>
        )}
      </div>
    </>
  );
}

/* ─── Section 5: Game scope (riuso S3) ─── */
function GameScopeSection({ form, set, readOnly, changed }) {
  const [q, setQ] = useState('');
  const selected = form.scope.map(gameById).filter(Boolean);
  const suggestions = GAMES.filter(g => !q || g.title.toLowerCase().includes(q.toLowerCase()));
  const toggleGame = id => { if (readOnly) return; set('scope', form.scope.includes(id) ? form.scope.filter(s => s !== id) : [...form.scope, id]); };
  return (
    <>
      {!readOnly && (
        <div className="cp-scopsearch">
          <div className="cp-inwrap">
            <span style={{ position: 'absolute', left: 11, top: '50%', transform: 'translateY(-50%)', opacity: .6, fontSize: 13, pointerEvents: 'none' }} aria-hidden="true">🔍</span>
            <input className="cp-input" style={{ paddingLeft: 32 }} value={q}
              placeholder="Cerca un gioco per nome…" aria-label="Cerca gioco" onChange={e => setQ(e.target.value)} />
          </div>
          {q && (
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
      )}
      {changed && !readOnly && <div className="cp-label" style={{ margin: '4px 0 0' }}><span className="grow" /><ChangedMark /></div>}
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
    </>
  );
}

/* ─── Section 6: Revisions diff ─── */
function DiffRow({ rev, onGoto }) {
  return (
    <div className="ce-diff" role="group" aria-label={'Diff ' + rev.field}>
      <div className="ce-diffhead">
        <span className="df">{rev.field}</span>
        <span className="dref">→ Sezione {rev.section}</span>
        <span className="grow" />
        <button className="goto" onClick={() => onGoto(rev.sectionId)}>Vai alla sezione ↗</button>
      </div>
      <div className="ce-diffbody">
        {rev.lines.map((l, i) => (
          <div className={'ce-dline ' + l[0]} key={i}>
            <span className="pfx" aria-hidden="true">{l[0] === 'add' ? '+' : l[0] === 'del' ? '−' : ' '}</span>
            <span>{l[1]}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

function RevisionsSection({ onGoto, diffInline, setDiffInline }) {
  return (
    <div role="region" aria-label="Differenze rispetto a versione approvata">
      <div className="ce-comparebar">
        <span className="cl">Confronto</span>
        <button className="ce-cmpsel">v3 <span className="vv">bozza</span> <span className="car">▾</span></button>
        <span className="ce-cmparrow" aria-hidden="true">vs</span>
        <button className="ce-cmpsel">v2 <span className="vv">approvata 28 mag</span> <span className="car">▾</span></button>
      </div>
      <div className="ce-difflist">
        {REVISIONS.map(rev => <DiffRow key={rev.id} rev={rev} onGoto={onGoto} />)}
      </div>
      <div className="ce-diffsum">
        <span aria-hidden="true">📊</span>
        <span><b>3 modifiche:</b> 2 in System prompt, 1 in Scope giochi. Capabilities invariate.</span>
      </div>
      <div className="ce-difftoggle">
        <div className="dtt">
          <div className="dtl">Mostra modifiche inline</div>
          <div className="dts">Evidenzia i campi cambiati direttamente nelle sezioni del form</div>
        </div>
        <button className={'ce-switch' + (diffInline ? ' on' : '')} role="switch" aria-checked={diffInline} aria-label="Mostra modifiche inline" onClick={() => setDiffInline(v => !v)} />
      </div>
    </div>
  );
}

/* ─── Section 7: Audit trail ─── */
const AUDIT_ICON = { created: '🟢', submit: '🔵', review: '🟡', approved: '✅', rejected: '❌', edited: '✏️' };
function AuditEvent({ ev }) {
  return (
    <li className="ce-event">
      <div className="ce-evcol">
        <span className={'ce-evdot ' + ev.type} aria-label={ev.label}>{AUDIT_ICON[ev.type]}</span>
        <span className="ce-evline" aria-hidden="true" />
      </div>
      <div className="ce-evbody">
        <div className="ce-evtop">
          <span className="ce-evlabel">{ev.label}</span>
          <time className="ce-evwhen" dateTime={ev.iso} title={ev.iso.replace('T', ' ')}>{ev.ago}</time>
        </div>
        <div className="ce-evmeta">
          <AuthorChip who={ev.who} admin={ev.admin} />
        </div>
        {ev.reason && (
          <div className="ce-evreason"><b>Motivo:</b> “{ev.reason}” <a tabIndex={0} role="button">Vedi</a></div>
        )}
      </div>
    </li>
  );
}

function AuditTrailSection() {
  return (
    <ol className="ce-timeline" role="list" aria-label="Storico azioni">
      {AUDIT.map(ev => <AuditEvent key={ev.id} ev={ev} />)}
    </ol>
  );
}

/* ─── Status banner ─── */
function StatusBanner({ status, stats, onGoto }) {
  const [open, setOpen] = useState(true);
  const toggle = (
    <button className="ce-bantoggle" aria-expanded={open} aria-label={open ? 'Riduci banner' : 'Espandi banner'}
      title={open ? 'Riduci' : 'Espandi'} onClick={() => setOpen(o => !o)}>
      <span aria-hidden="true">{open ? '▾' : '▸'}</span>
    </button>
  );

  if (status === 'Rejected') {
    return (
      <div className={'ce-banner rejected' + (open ? '' : ' collapsed')} role="alert" aria-labelledby="rej-t">
        <div className="bhead">
          <span className="bic" aria-hidden="true">⚠️</span>
          <span className="bt" id="rej-t">Feedback admin</span>
          <span className="bwhen">· {REJECT_FEEDBACK.ago}</span>
          {!open && <span className="bpeek">2 sezioni da rivedere</span>}
          {toggle}
        </div>
        {open && (
          <>
            <div className="bbody">{REJECT_FEEDBACK.body}</div>
            <div className="bsections">
              <span className="bsec-lab">Sezioni da rivedere</span>
              {REJECT_FEEDBACK.sections.map(([label, n, id]) => (
                <button key={id} className="ce-secchip" onClick={() => onGoto(id)}>{label} ({n})</button>
              ))}
            </div>
            <div className="bacts">
              <button className="cp-btn ghost"><span className="ic" aria-hidden="true">👁</span> Vedi feedback completo</button>
              <button className="cp-btn toolkit-out"><span className="ic" aria-hidden="true">✓</span> Marca come risolto</button>
              <button className="cp-btn primary"><span className="ic" aria-hidden="true">↻</span> Riinvia per review</button>
            </div>
          </>
        )}
      </div>
    );
  }
  if (status === 'PendingReview') {
    return (
      <div className={'ce-banner review' + (open ? '' : ' collapsed')} role="status">
        <div className="bhead">
          <span className="bic" aria-hidden="true">⏳</span>
          <span className="bt">In review da 3 giorni</span>
          <span className="bwhen">· Sottomesso il 30 mag 2026</span>
          {toggle}
        </div>
        {open && (
          <>
            <div className="bbody">La proposal è <b>bloccata</b> fino al verdetto admin. Non è modificabile in questo stato.</div>
            <div className="bacts">
              <button className="cp-btn warn-out"><span className="ic" aria-hidden="true">↩</span> Ritira dalla review</button>
            </div>
          </>
        )}
      </div>
    );
  }
  if (status === 'Approved') {
    return (
      <div className={'ce-banner approved' + (open ? '' : ' collapsed')} role="status">
        <div className="bhead">
          <span className="bic" aria-hidden="true">✓</span>
          <span className="bt">Approvata il 28 mag 2026 da Admin</span>
          <span className="bwhen">· Live da 5 giorni</span>
          {toggle}
        </div>
        {open && (
          <>
            <div className="bbody">Questa versione è <b>attiva in produzione</b>. Per modificarla crea una nuova versione (clone in nuovo Draft).</div>
            {stats && (
              <div className="bstats">
                <span className="bstat"><span aria-hidden="true">🎯</span> <b>234</b> conversazioni</span>
                <span className="bstat"><span aria-hidden="true">👍</span> <b>92%</b> feedback positivo</span>
                <span className="bstat"><span aria-hidden="true">⚡</span> <b>0.9s</b> latenza media</span>
              </div>
            )}
            <div className="bacts">
              <button className="cp-btn primary"><span className="ic" aria-hidden="true">⎘</span> Crea versione successiva</button>
            </div>
          </>
        )}
      </div>
    );
  }
  return null;
}

/* ─── Modals ─── */
function SubmitUpdateModal({ form, onClose, onConfirm }) {
  const scope = form.scope.map(gameById).filter(Boolean);
  return (
    <div className="cp-modal-bd" role="dialog" aria-modal="true" aria-labelledby="sub-modal-t" onClick={onClose}>
      <div className="cp-modal" onClick={e => e.stopPropagation()}>
        <div className="mhead"><span className="mi agent" aria-hidden="true">↻</span><h3 id="sub-modal-t">Conferma reinvio per review</h3></div>
        <div className="mbody">
          La versione aggiornata verrà reinviata al team di review. Verranno incluse le modifiche fatte dopo il feedback del <b>{REJECT_FEEDBACK.ago}</b>.
          <div className="msummary">
            <div className="msrow"><span className="k">Modifiche</span><span className="vv add">3 campi cambiati vs v2</span></div>
            <div className="msrow"><span className="k">System prompt</span><span className="vv">Generalizzato per euro-game</span></div>
            <div className="msrow"><span className="k">Scope</span><span className="vv">{scope.map(g => g.title).join(', ')} (ristretto)</span></div>
            <div className="msrow"><span className="k">Feedback</span><span className="vv add">2 sezioni risolte</span></div>
          </div>
        </div>
        <div className="mfoot"><button className="cp-btn ghost" onClick={onClose}>Annulla</button><span className="grow" /><button className="cp-btn primary" onClick={onConfirm}><span className="ic" aria-hidden="true">↻</span> Conferma e reinvia</button></div>
      </div>
    </div>
  );
}

function ConfirmCancelModal({ onClose, onConfirm }) {
  return (
    <div className="cp-modal-bd" role="dialog" aria-modal="true" aria-labelledby="cnc-t" onClick={onClose}>
      <div className="cp-modal" onClick={e => e.stopPropagation()}>
        <div className="mhead"><span className="mi warn" aria-hidden="true">⚠️</span><h3 id="cnc-t">Annullare le modifiche?</h3></div>
        <div className="mbody">Le modifiche non salvate andranno perse. Vuoi davvero uscire senza salvare?</div>
        <div className="mfoot"><button className="cp-btn ghost" onClick={onClose}>Continua a modificare</button><span className="grow" /><button className="cp-btn danger" onClick={onConfirm}>Esci senza salvare</button></div>
      </div>
    </div>
  );
}

/* ──────────────────────────────────────────────────────────
   EditApp — un'istanza per (state × viewport). Remount via key.
   ────────────────────────────────────────────────────────── */
function EditApp({ stateId, mobile }) {
  const sc = SCENARIOS[stateId];
  const status = sc.status;
  const readOnly = status === 'PendingReview' || status === 'Approved';
  const [form, setForm] = useState(BASE_FORM);
  const [open, setOpen] = useState(() => new Set(sc.open));
  const [savePill] = useState(sc.savePill);
  const [diffInline, setDiffInline] = useState(!!sc.ui.diffInline);
  const [submitModal, setSubmitModal] = useState(!!sc.ui.modal);
  const [cancelModal, setCancelModal] = useState(false);
  const bodyRef = useRef(null);
  const sectionRefs = useRef({});

  const addressed = sc.addressed || [];
  const affectedSet = new Set(REJECT_FEEDBACK.sections.map(s => s[2])); // prompt, scope
  const showStats = !!sc.ui.stats;
  const showInlineMarkers = diffInline || addressed.length > 0;

  const set = useCallback((key, val) => { if (readOnly) return; setForm(f => ({ ...f, [key]: val })); }, [readOnly]);
  const toggle = id => setOpen(o => { const n = new Set(o); n.has(id) ? n.delete(id) : n.add(id); return n; });

  const gotoSection = useCallback(id => {
    const num = { identity: 1, caps: 2, prompt: 3, test: 4, scope: 5, revisions: 6, audit: 7 }[id];
    if (!num) return;
    setOpen(o => { const n = new Set(o); n.add(num); return n; });
    requestAnimationFrame(() => {
      const el = sectionRefs.current[num];
      if (el && bodyRef.current) bodyRef.current.scrollTo({ top: el.offsetTop - 12, behavior: 'smooth' });
    });
  }, []);

  // section flags
  const isAffected = id => status === 'Rejected' && affectedSet.has(id) && !addressed.includes(id);
  const isResolved = id => status === 'Rejected' && addressed.includes(id);
  const isChanged = id => showInlineMarkers && affectedSet.has(id) && (diffInline || addressed.includes(id)) && !readOnly;

  const sections = [
    { num: 1, id: 'identity', title: 'Identità', subtitle: 'Nome, descrizione, icona e colore',
      body: <IdentitySection form={form} set={set} readOnly={readOnly} uniqueStatus="ok" /> },
    { num: 2, id: 'caps', title: 'Capabilities', subtitle: 'Cosa sa fare l\u2019agente',
      body: <CapabilitiesSection form={form} set={set} readOnly={readOnly} /> },
    { num: 3, id: 'prompt', title: 'System prompt', subtitle: 'Le istruzioni che guidano l\u2019agente',
      body: <SystemPromptSection form={form} set={set} readOnly={readOnly} changed={isChanged('prompt')} /> },
    { num: 4, id: 'test', title: 'Configurazione test', subtitle: 'Esempi input/output per il playground',
      body: <TestConfigSection form={form} set={set} readOnly={readOnly} /> },
    { num: 5, id: 'scope', title: 'Scope giochi', subtitle: 'A quali giochi si applica',
      body: <GameScopeSection form={form} set={set} readOnly={readOnly} changed={isChanged('scope')} /> },
  ];

  // keyboard shortcuts
  useEffect(() => {
    const h = e => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') { e.preventDefault(); }
      if ((e.ctrlKey || e.metaKey) && e.key === 'Enter' && (status === 'Draft' || status === 'Rejected')) { e.preventDefault(); setSubmitModal(true); }
      if (e.key === 'Escape') { setSubmitModal(false); setCancelModal(false); }
    };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [status]);

  const m = STATUS_META[status];

  /* footer CTAs per status */
  let footerCtas;
  if (status === 'Draft') {
    footerCtas = <>
      <button className="cp-btn link" onClick={() => setCancelModal(true)}>Annulla</button>
      <span className="grow" />
      <button className="cp-btn ghost"><span className="ic" aria-hidden="true">💾</span> Salva bozza</button>
      <button className="cp-btn ghost"><span className="ic" aria-hidden="true">👁</span> Anteprima</button>
      <button className="cp-btn primary" onClick={() => setSubmitModal(true)}><span className="ic" aria-hidden="true">⬆</span> Invia per review</button>
    </>;
  } else if (status === 'Rejected') {
    footerCtas = <>
      <button className="cp-btn link" onClick={() => setCancelModal(true)}>Annulla modifiche</button>
      <span className="grow" />
      <button className="cp-btn ghost"><span className="ic" aria-hidden="true">💾</span> Salva bozza</button>
      <button className="cp-btn warn-out"><span className="ic" aria-hidden="true">⇄</span> Confronta con feedback</button>
      <button className="cp-btn primary" onClick={() => setSubmitModal(true)}><span className="ic" aria-hidden="true">↻</span> Riinvia per review</button>
    </>;
  } else if (status === 'PendingReview') {
    footerCtas = <>
      <span className="grow" />
      <button className="cp-btn toolkit-out"><span className="ic" aria-hidden="true">↗</span> Visualizza in production</button>
      <button className="cp-btn warn"><span className="ic" aria-hidden="true">↩</span> Annulla invio (ritira)</button>
    </>;
  } else { // Approved
    footerCtas = <>
      <span className="grow" />
      <button className="cp-btn ghost"><span className="ic" aria-hidden="true">⇄</span> Diff vs versione precedente</button>
      <button className="cp-btn info-out"><span className="ic" aria-hidden="true">📊</span> Stats production</button>
      <button className="cp-btn primary"><span className="ic" aria-hidden="true">⎘</span> Crea versione successiva</button>
    </>;
  }

  /* header CTAs per status */
  let headerCtas;
  if (status === 'Draft') {
    headerCtas = <>
      <button className="cp-btn ghost"><span className="ic" aria-hidden="true">💾</span> Salva bozza</button>
      <button className="cp-btn primary" onClick={() => setSubmitModal(true)}><span className="ic" aria-hidden="true">⬆</span> Invia per review</button>
    </>;
  } else if (status === 'Rejected') {
    headerCtas = <>
      <button className="cp-btn ghost"><span className="ic" aria-hidden="true">💾</span> Salva bozza</button>
      <button className="cp-btn primary" onClick={() => setSubmitModal(true)}><span className="ic" aria-hidden="true">↻</span> Riinvia per review</button>
    </>;
  } else if (status === 'PendingReview') {
    headerCtas = <button className="cp-btn warn-out"><span className="ic" aria-hidden="true">↩</span> Annulla invio (ritira)</button>;
  } else {
    headerCtas = <>
      <button className="cp-btn ghost"><span className="ic" aria-hidden="true">⇄</span> Visualizza diff</button>
      <button className="cp-btn primary"><span className="ic" aria-hidden="true">⎘</span> Crea versione successiva</button>
    </>;
  }

  const pillEl = (
    <span className={'cp-pill ' + savePill} aria-live="polite">
      <span className="dot" aria-hidden="true" />
      {savePill === 'unsaved' && 'Modifiche non salvate'}
      {savePill === 'saved' && '💾 Bozza salvata 2m fa'}
      {savePill === 'readonly' && '🔒 Read-only'}
      {savePill === 'dirty' && '● Modifiche dopo feedback'}
    </span>
  );

  return (
    <div className={'cp-app' + (mobile ? ' is-mobile' : '')}>
      <header className="cp-head">
        <div className="hrow">
          <div className="htxt">
            <div className="cp-bread"><span>Editor</span><span className="sep">›</span><span>Agent proposals</span><span className="sep">›</span><span className="cur">{form.name}</span></div>
            <div className="ce-titlerow">
              <h1 className="cp-h1">{form.icon} {form.name}</h1>
              <span className={'ce-badge ' + m.cls} aria-label={'Stato: ' + m.label}><span className="bi" aria-hidden="true">{m.icon}</span>{m.label}</span>
              {status === 'Approved' && <span className="ce-live"><i />Live</span>}
            </div>
            <div className="cp-sub">Versione {META.version} · Creata {META.createdAgo} da <b>{AUTHOR.name}</b> · Ultima modifica {META.modAgo}</div>
            <div className="ce-metarow">
              <span className="ce-idchip">ID: {META.id}<button className="copy" aria-label="Copia ID" title="Copia ID">⧉</button></span>
              <AuthorChip who={AUTHOR} />
            </div>
          </div>
          <span className="grow" />
          <div className="cp-headcta">{headerCtas}</div>
        </div>
      </header>

      {status !== 'Draft' && (
        <div className="ce-banwrap"><div className="inner"><StatusBanner status={status} stats={showStats} onGoto={gotoSection} /></div></div>
      )}

      <div className="cp-body" ref={bodyRef}>
        <form className="cp-form" aria-label="Form modifica typology proposal" aria-readonly={readOnly} onSubmit={e => e.preventDefault()}>
          {sections.map(s => (
            <div key={s.id} ref={el => { sectionRefs.current[s.num] = el; }}>
              <SectionCard num={s.num} id={s.id} title={s.title} subtitle={s.subtitle}
                locked={readOnly} affected={isAffected(s.id)} resolved={isResolved(s.id)} changed={isChanged(s.id)}
                status="optional" statusCount={0}
                open={open.has(s.num)} onToggle={() => toggle(s.num)}>
                {s.body}
              </SectionCard>
            </div>
          ))}

          {/* Sezione 6 — Revisions diff */}
          <div ref={el => { sectionRefs.current[6] = el; }}>
            <SectionCard num={6} id="revisions" title="Revisions" subtitle="Differenze rispetto alla versione approvata in produzione"
              statusCount="3 modifiche" open={open.has(6)} onToggle={() => toggle(6)}>
              <RevisionsSection onGoto={gotoSection} diffInline={diffInline} setDiffInline={setDiffInline} />
            </SectionCard>
          </div>

          {/* Sezione 7 — Audit trail */}
          <div ref={el => { sectionRefs.current[7] = el; }}>
            <SectionCard num={7} id="audit" title="Storico azioni" subtitle="Log delle azioni e decisioni"
              statusCount={AUDIT.length + ' eventi'} open={open.has(7)} onToggle={() => toggle(7)}>
              <AuditTrailSection />
            </SectionCard>
          </div>
        </form>
      </div>

      <div className="cp-savebar"><div className="inner">{pillEl}</div></div>

      <footer className="cp-foot">
        <div className="frow">{footerCtas}</div>
      </footer>

      {submitModal && <SubmitUpdateModal form={form} onClose={() => setSubmitModal(false)} onConfirm={() => setSubmitModal(false)} />}
      {cancelModal && <ConfirmCancelModal onClose={() => setCancelModal(false)} onConfirm={() => setCancelModal(false)} />}
    </div>
  );
}

/* ──────────────────────────────────────────────────────────
   Harness — continuity con S1/S2/S3
   ────────────────────────────────────────────────────────── */
function Harness() {
  const [stateId, setStateId] = useState(() => localStorage.getItem('ce-state') || 'rejected-fresh');
  const [theme, setTheme] = useState(() => localStorage.getItem('mai-theme') || 'light');

  useEffect(() => { document.documentElement.setAttribute('data-theme', theme); localStorage.setItem('mai-theme', theme); }, [theme]);
  useEffect(() => { localStorage.setItem('ce-state', stateId); }, [stateId]);

  return (
    <div className="ed-stage">
      <style dangerouslySetInnerHTML={{ __html: CE_CSS }} />
      <button className="theme-toggle" onClick={() => setTheme(theme === 'light' ? 'dark' : 'light')}>🌗 <span>{theme === 'dark' ? 'Dark' : 'Light'}</span></button>

      <div className="ed-wrap">
        <div className="ed-kicker">SP4 · B14 · #1489 — schermata 4 / 5 · edit typology proposal</div>
        <h1>Edit <span className="acc">proposal</span> — /editor/agent-proposals/[id]/edit</h1>
        <p className="ed-lead">
          Form di modifica di una <b>typology proposal</b> esistente, con <b>comportamento variabile per status</b>:
          <b> Draft</b> editable, <b>PendingReview</b> e <b>Approved</b> in read-only (lock), <b>Rejected</b> con alert feedback
          e campi editabili per la risottomissione. Riusa le 5 sezioni di S3 e aggiunge <b>Revisions diff</b> (6) e
          <b> Audit trail</b> (7). Entity primaria <code>--c-agent</code>, autore via EntityChip <code>--c-player</code>.
        </p>

        <div className="ed-notes">
          <div className="ed-note">
            <h4>Status variants</h4>
            <p><b>Draft</b> tutto editable · <b>Pending</b> banner giallo + lock · <b>Approved</b> banner verde + Live + lock · <b>Rejected</b> alert rosso + campi editabili + marker “modifiche dopo feedback”.</p>
          </div>
          <div className="ed-note">
            <h4>10 stati · 7 sezioni</h4>
            <p>Selettore qui sotto. Sezioni 1-5 riusate da S3, <b>6 Revisions</b> (diff inline +/−) e <b>7 Audit trail</b> (timeline 8 eventi entity-colored). Save pill <code>aria-live</code> per status.</p>
          </div>
          <div className="ed-note">
            <h4>Mobile & a11y</h4>
            <p>Mobile = sezioni full-width, banner full-width, timeline compatta. <code>role=alert/status</code> sui banner, <code>aria-readonly</code> sul form lock, <code>role=list</code> sull’audit, <code>Ctrl+S</code> / <code>Ctrl+Enter</code> / <code>Esc</code>.</p>
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

        <div className="ed-vp-label">Desktop · 1440 — form + status banner + revisions/audit</div>
        <div className="ed-desk">
          <div className="ed-chrome">
            <div className="dots"><i /><i /><i /></div>
            <div className="url">meepleai.app/editor/agent-proposals/{META.id}/edit</div>
          </div>
          <div style={{ flex: 1, minHeight: 0 }}>
            <EditApp key={'d-' + stateId} stateId={stateId} mobile={false} />
          </div>
        </div>

        <div className="ed-vp-label">Mobile · 375 — sezioni stack, banner full-width</div>
        <div className="ed-phone-row">
          <div className="phone">
            <div className="phone-sbar"><span>9:41</span><span className="ind">●●● 5G ▮</span></div>
            <div style={{ flex: 1, minHeight: 0, display: 'flex' }}>
              <EditApp key={'m-' + stateId} stateId={stateId} mobile={true} />
            </div>
          </div>
          <div className="ed-phone-cap">
            <h4>Layout mobile</h4>
            <p>Le sezioni vanno a <b>full-width</b>, lo status banner occupa tutta la larghezza, header CTA collassano nel <b>footer sticky</b>. La timeline audit resta verticale ma compatta; il diff scrolla in orizzontale dentro la card.</p>
          </div>
        </div>
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<Harness />);
