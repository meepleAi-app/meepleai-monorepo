/* sp4-editor-index.jsx
   Route: /editor — RuleSpec atom editor con PDF preview (split-view)
   B14 (issue #1489) · screen 1 of 5 · Tier S
   Pattern (LOCKED): Split-view — pane sinistro atom-list editor (60%) + pane destro PDF preview (40%).
   Mobile: stack, solo atom-list; il metadata "📄 p.N" apre un bottom-sheet con la pagina PDF.
   Loadable standalone via Babel. Injects own component CSS; relies on tokens.css + components.css.

   Modello: RuleSpec = array flat di RuleAtom { id, n, section, text, page, sec }.
   v2 components surfaced here (annotate at implementation time):
   /* v2: RuleSpecSplitEditor, AtomListPane, AtomCard, SectionAccordion, RuleEditorToolbar,
          SaveStatusPill, ValidationPill, PdfPreviewPane, PdfPageView, AtomHighlightOverlay,
          ConflictResolutionModal, LockBanner, VersionHistoryPanel, MobilePdfSheet */

const { useState, useEffect, useMemo, useRef } = React;

/* ──────────────────────────────────────────────────────────
   Component CSS — solo token da tokens.css / components.css.
   ────────────────────────────────────────────────────────── */
const EDITOR_CSS = `
.ed-stage { min-height:100vh; padding:72px 24px 96px; background:var(--bg); color:var(--text); }
.ed-wrap { max-width:1380px; margin:0 auto; }
.ed-kicker { font-family:var(--f-mono); font-size:var(--fs-xs); letter-spacing:.1em; text-transform:uppercase; color:var(--text-muted); }
.ed-stage h1 { font-size:var(--fs-3xl); margin:8px 0 6px; }
.ed-stage h1 .acc { color:hsl(var(--c-game)); }
.ed-lead { color:var(--text-sec); font-size:var(--fs-md); max-width:780px; line-height:var(--lh-body); }
.ed-notes { display:grid; grid-template-columns:repeat(3,1fr); gap:12px; margin:22px 0 4px; }
.ed-note { background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-lg); padding:14px 16px; }
.ed-note h4 { font-family:var(--f-display); font-size:var(--fs-sm); text-transform:uppercase; letter-spacing:.04em; color:hsl(var(--c-game)); margin-bottom:6px; }
.ed-note p { font-size:var(--fs-sm); color:var(--text-sec); line-height:var(--lh-snug); }
.ed-note p b { color:var(--text); font-weight:var(--fw-bold); }
.ed-note code { background:var(--bg-muted); padding:1px 5px; border-radius:var(--r-xs); font-size:11px; }

/* preview rail */
.ed-rail { position:sticky; top:0; z-index:var(--z-sticky); margin:26px 0 18px; padding:12px 0;
  background:var(--bg); display:flex; align-items:flex-start; gap:14px; flex-wrap:wrap; border-bottom:1px solid var(--border); }
.ed-rail .lab { font-family:var(--f-mono); font-size:var(--fs-xs); text-transform:uppercase; letter-spacing:.08em; color:var(--text-muted); padding-top:8px; }
.ed-states { display:flex; gap:6px; flex-wrap:wrap; flex:1; }
.ed-sbtn { display:inline-flex; align-items:center; gap:7px; padding:7px 12px; border-radius:var(--r-pill);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm);
  background:var(--bg-card); border:1.5px solid var(--border); color:var(--text-sec); cursor:pointer; transition:all var(--dur-sm) var(--ease-out); }
.ed-sbtn:hover { transform:translateY(-1px); border-color:var(--border-strong); }
.ed-sbtn .pip { width:7px; height:7px; border-radius:50%; background:currentColor; opacity:.6; }
.ed-sbtn.on { background:hsl(var(--c-game)); border-color:transparent; color:#fff; }
.ed-sbtn.on .pip { opacity:1; background:#fff; }

/* viewport labels */
.ed-vp-label { font-family:var(--f-mono); font-size:var(--fs-xs); text-transform:uppercase; letter-spacing:.08em;
  color:var(--text-muted); margin:30px 0 12px; display:flex; align-items:center; gap:10px; }
.ed-vp-label::after { content:''; flex:1; height:1px; background:var(--border); }

/* desktop frame */
.ed-desk { width:100%; max-width:1340px; height:792px; border-radius:var(--r-lg); overflow:hidden;
  background:var(--bg-card); border:1px solid var(--border); box-shadow:var(--shadow-lg); display:flex; flex-direction:column; }
.ed-chrome { height:38px; flex-shrink:0; display:flex; align-items:center; gap:8px; padding:0 14px;
  background:var(--bg-muted); border-bottom:1px solid var(--border); font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); }
.ed-chrome .dots { display:flex; gap:6px; }
.ed-chrome .dots i { width:11px; height:11px; border-radius:50%; display:block; }
.ed-chrome .dots i:nth-child(1){ background:#ff5f57; } .ed-chrome .dots i:nth-child(2){ background:#febc2e; } .ed-chrome .dots i:nth-child(3){ background:#28c840; }
.ed-chrome .url { flex:1; text-align:center; background:var(--bg-card); border-radius:var(--r-sm); padding:4px 10px; margin:0 16%; }

/* phone */
.ed-phone-row { display:flex; gap:28px; align-items:flex-start; flex-wrap:wrap; }
.ed-phone-cap { font-size:var(--fs-sm); color:var(--text-sec); max-width:300px; line-height:var(--lh-snug); }
.ed-phone-cap h4 { font-family:var(--f-display); font-size:var(--fs-base); margin-bottom:6px; }

/* ─── editor app ─── */
.ed-app { display:flex; flex-direction:column; height:100%; min-height:0; background:var(--bg); color:var(--text); position:relative; overflow:hidden; }
.ed-app :focus-visible { outline:2px solid hsl(var(--c-game)); outline-offset:2px; border-radius:var(--r-xs); }

/* top banner */
.ed-banner { flex-shrink:0; display:flex; align-items:center; gap:10px; padding:10px 18px; font-size:var(--fs-sm); font-weight:var(--fw-bold); font-family:var(--f-display); }
.ed-banner.pub { background:hsl(var(--c-success) / .14); color:hsl(var(--c-success)); border-bottom:1px solid hsl(var(--c-success) / .3); }
.ed-banner.lock { background:hsl(var(--c-warning) / .15); color:hsl(var(--c-warning)); border-bottom:1px solid hsl(var(--c-warning) / .3); }
.ed-banner .grow { flex:1; }
.ed-banner .bcta { padding:5px 12px; border-radius:var(--r-md); border:none; cursor:pointer; font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); }
.ed-banner.lock .bcta { background:hsl(var(--c-player)); color:#fff; }
.ed-banner.pub .bcta { background:var(--bg-card); color:hsl(var(--c-success)); border:1px solid hsl(var(--c-success) / .4); }
.ed-pchip { display:inline-flex; align-items:center; gap:6px; padding:3px 10px 3px 3px; border-radius:var(--r-pill);
  background:hsl(var(--c-player) / .16); color:hsl(var(--c-player)); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); }
.ed-pchip .av { width:20px; height:20px; border-radius:50%; background:hsl(var(--c-player)); color:#fff; display:flex; align-items:center; justify-content:center; font-size:10px; font-weight:var(--fw-ext); }

/* split */
.ed-split { flex:1; display:grid; grid-template-columns:1.5fr 1fr; overflow:hidden; min-height:0; }

/* panes */
.ed-pane { display:flex; flex-direction:column; min-height:0; overflow:hidden; }
.ed-pane.left { background:var(--bg); }
.ed-pane.right { background:var(--bg-muted); border-left:1px solid var(--border); }

/* left header */
.ed-lhead { flex-shrink:0; background:var(--bg-card); border-bottom:1px solid var(--border);
  display:flex; align-items:center; gap:10px; padding:11px 16px; flex-wrap:wrap; }
.ed-brand { width:28px; height:28px; border-radius:var(--r-sm); flex-shrink:0;
  background:linear-gradient(135deg,hsl(var(--c-game)),hsl(var(--c-event))); color:#fff;
  display:flex; align-items:center; justify-content:center; font-family:var(--f-display); font-weight:var(--fw-ext); font-size:14px; }
.ed-gamesel { display:inline-flex; align-items:center; gap:7px; padding:6px 11px; border-radius:var(--r-pill);
  background:hsl(var(--c-game) / .12); color:hsl(var(--c-game)); border:1px solid hsl(var(--c-game) / .25);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); cursor:pointer; transition:all var(--dur-sm) var(--ease-out); }
.ed-gamesel:hover { background:hsl(var(--c-game) / .18); }
.ed-gamesel .chev { font-size:9px; opacity:.7; }
.ed-title { font-family:var(--f-display); font-size:var(--fs-md); font-weight:var(--fw-bold); white-space:nowrap; }
.ed-lhead .grow { flex:1; min-width:6px; }

/* pills */
.ed-pill { display:inline-flex; align-items:center; gap:6px; padding:6px 11px; border-radius:var(--r-pill);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); white-space:nowrap; border:1px solid transparent; }
.ed-pill .dot { width:8px; height:8px; border-radius:50%; background:currentColor; }
.ed-pill .pulse { width:8px; height:8px; border-radius:50%; background:currentColor; animation:edpulse 1s var(--ease-in-out) infinite; }
@keyframes edpulse { 0%,100%{ opacity:1; transform:scale(1);} 50%{ opacity:.35; transform:scale(.7);} }
.ed-pill.ok    { background:hsl(var(--c-success) / .14); color:hsl(var(--c-success)); }
.ed-pill.warn  { background:hsl(var(--c-warning) / .16); color:hsl(var(--c-warning)); }
.ed-pill.danger{ background:hsl(var(--c-danger) / .14); color:hsl(var(--c-danger)); }
.ed-pill.btn { cursor:pointer; } .ed-pill.btn:hover { filter:brightness(.97); }

.ed-publish { display:inline-flex; align-items:center; gap:7px; padding:8px 15px; border-radius:var(--r-md); border:none;
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); cursor:pointer;
  background:hsl(var(--c-toolkit)); color:#fff; box-shadow:var(--shadow-xs); transition:all var(--dur-sm) var(--ease-out); }
.ed-publish:hover:not(:disabled) { transform:translateY(-1px); box-shadow:var(--shadow-sm); }
.ed-publish:disabled { opacity:.4; cursor:not-allowed; }

/* toolbar */
.ed-toolbar { flex-shrink:0; display:flex; align-items:center; gap:8px; padding:9px 16px; background:var(--bg);
  border-bottom:1px solid var(--border); }
.ed-search { flex:1; min-width:80px; position:relative; }
.ed-search input { width:100%; padding:7px 10px 7px 30px; border-radius:var(--r-md); border:1px solid var(--border);
  background:var(--bg-card); font-family:var(--f-body); font-size:var(--fs-sm); color:var(--text); outline:none; }
.ed-search input:focus { border-color:hsl(var(--c-game) / .5); box-shadow:0 0 0 3px hsl(var(--c-game) / .12); }
.ed-search .ic { position:absolute; left:10px; top:50%; transform:translateY(-50%); font-size:13px; opacity:.6; }
.ed-selectw { position:relative; }
.ed-select { padding:7px 28px 7px 11px; border-radius:var(--r-md); border:1px solid var(--border); background:var(--bg-card);
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); color:var(--text-sec); cursor:pointer; appearance:none; outline:none; }
.ed-selectw::after { content:'▾'; position:absolute; right:11px; top:50%; transform:translateY(-50%); font-size:10px; color:var(--text-muted); pointer-events:none; }
.ed-tbtn { display:inline-flex; align-items:center; gap:6px; padding:7px 11px; border-radius:var(--r-md); border:1px solid hsl(var(--c-game) / .35);
  background:transparent; color:hsl(var(--c-game)); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); cursor:pointer; white-space:nowrap;
  transition:all var(--dur-sm) var(--ease-out); }
.ed-tbtn:hover { background:hsl(var(--c-game) / .1); }
.ed-vtoggle { display:inline-flex; padding:3px; gap:2px; background:var(--bg-muted); border-radius:var(--r-md); border:1px solid var(--border); }
.ed-vtoggle button { width:30px; height:28px; border-radius:var(--r-sm); border:none; background:transparent; color:var(--text-muted);
  font-size:14px; cursor:pointer; display:inline-flex; align-items:center; justify-content:center; }
.ed-vtoggle button[aria-pressed="true"] { background:var(--bg-card); color:hsl(var(--c-game)); box-shadow:var(--shadow-xs); }

/* atom body */
.ed-lbody { flex:1; overflow-y:auto; padding:10px 16px 24px; min-height:0; }

/* section accordion */
.ed-section { margin-bottom:10px; }
.ed-shead { display:flex; align-items:center; gap:9px; padding:9px 10px; border-radius:var(--r-md); cursor:pointer;
  background:var(--bg-card); border:1px solid var(--border); transition:background var(--dur-sm) var(--ease-out); }
.ed-shead:hover { background:var(--bg-hover); }
.ed-caret { font-size:10px; color:var(--text-muted); width:12px; transition:transform var(--dur-sm) var(--ease-out); }
.ed-sname { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-base); color:var(--text); }
.ed-scount { font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); }
.ed-shead .grow { flex:1; }
.ed-saddbtn { display:inline-flex; align-items:center; gap:4px; padding:4px 9px; border-radius:var(--r-sm); border:1px solid hsl(var(--c-game) / .35);
  background:transparent; color:hsl(var(--c-game)); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); cursor:pointer; }
.ed-saddbtn:hover { background:hsl(var(--c-game) / .1); }
.ed-ctx { width:26px; height:26px; border-radius:var(--r-sm); border:none; background:transparent; color:var(--text-muted); cursor:pointer; font-size:15px; }
.ed-ctx:hover { background:var(--bg-muted); color:var(--text); }
.ed-satoms { padding:8px 0 2px 4px; display:flex; flex-direction:column; gap:8px; }

/* atom card */
.ed-atom { position:relative; display:flex; gap:11px; padding:11px 12px; border-radius:var(--r-md);
  background:var(--bg-card); border:1px solid var(--border-light); cursor:pointer; transition:all var(--dur-sm) var(--ease-out); }
.ed-atom:hover { border-color:var(--border-strong); }
.ed-atom.sel { background:hsl(var(--c-game) / .08); border-color:hsl(var(--c-game) / .45); }
.ed-atom.editing { border-color:hsl(var(--c-game) / .6); box-shadow:0 0 0 3px hsl(var(--c-game) / .18); }
.ed-atom.invalid { border-color:hsl(var(--c-danger) / .6); box-shadow:0 0 0 3px hsl(var(--c-danger) / .14); }
.ed-atom .num { font-family:var(--f-mono); font-size:var(--fs-sm); color:var(--text-muted); flex-shrink:0; padding-top:2px; min-width:18px; }
.ed-atom .body { flex:1; min-width:0; }
.ed-atom .text { font-size:var(--fs-md); color:var(--text); line-height:var(--lh-body); }
.ed-atom .ta { width:100%; min-height:62px; resize:vertical; padding:9px 10px; border-radius:var(--r-sm); border:1px solid var(--border);
  background:var(--bg); color:var(--text); font-family:var(--f-body); font-size:var(--fs-md); line-height:var(--lh-body); outline:none; }
.ed-atom .ta:focus { border-color:hsl(var(--c-game) / .5); }
.ed-atom .ta.bad { border-color:hsl(var(--c-danger) / .6); }
.ed-aerr { display:flex; align-items:center; gap:6px; margin-top:6px; font-size:var(--fs-sm); font-weight:var(--fw-bold); color:hsl(var(--c-danger)); }
.ed-afoot { display:flex; align-items:center; gap:10px; margin-top:8px; }
.ed-aref { display:inline-flex; align-items:center; gap:5px; padding:2px 8px; border-radius:var(--r-sm); cursor:pointer;
  font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-muted); background:var(--bg-muted); transition:all var(--dur-sm) var(--ease-out); }
.ed-aref:hover { color:hsl(var(--c-game)); background:hsl(var(--c-game) / .12); }
.ed-afoot .grow { flex:1; }
.ed-aact { width:26px; height:26px; border-radius:var(--r-sm); border:none; background:transparent; color:var(--text-muted); cursor:pointer; font-size:13px; }
.ed-aact:hover { background:var(--bg-muted); color:var(--text); }
.ed-dirty { position:absolute; top:9px; right:9px; width:9px; height:9px; border-radius:50%; background:hsl(var(--c-warning)); box-shadow:0 0 0 3px hsl(var(--c-warning) / .2); }
.ed-asaving { position:absolute; inset:0; border-radius:var(--r-md); background:hsl(var(--c-game) / .06); backdrop-filter:blur(.5px);
  display:flex; align-items:center; justify-content:center; pointer-events:none; }
.ed-asaving .lbl { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); color:hsl(var(--c-warning)); display:inline-flex; gap:7px; align-items:center; }

/* atom save-row (when editing) */
.ed-editrow { display:flex; gap:7px; margin-top:9px; }
.ed-ebtn { padding:7px 13px; border-radius:var(--r-sm); border:none; cursor:pointer; font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); }
.ed-ebtn.pri { background:hsl(var(--c-game)); color:#fff; } .ed-ebtn.pri:disabled { opacity:.4; cursor:not-allowed; }
.ed-ebtn.sec { background:var(--bg-muted); color:var(--text-sec); }

/* empty section */
.ed-sempty { text-align:center; padding:18px 10px; color:var(--text-muted); }
.ed-sempty .tx { font-size:var(--fs-sm); margin-bottom:10px; }

/* right pane (PDF) */
.ed-rhead { flex-shrink:0; background:var(--bg-card); border-bottom:1px solid var(--border);
  display:flex; align-items:center; gap:8px; padding:10px 14px; flex-wrap:wrap; }
.ed-kbpip { display:inline-flex; align-items:center; gap:6px; padding:5px 11px; border-radius:var(--r-pill);
  background:hsl(var(--c-kb) / .14); color:hsl(var(--c-kb)); font-family:var(--f-mono); font-size:var(--fs-xs); font-weight:var(--fw-bold); cursor:pointer; border:1px solid hsl(var(--c-kb) / .25); }
.ed-kbpip:hover { background:hsl(var(--c-kb) / .2); }
.ed-rhead .grow { flex:1; }
.ed-pagenav { display:inline-flex; align-items:center; gap:3px; font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-sec); }
.ed-navbtn { width:24px; height:24px; border-radius:var(--r-sm); border:1px solid var(--border); background:var(--bg); color:var(--text-sec); cursor:pointer; font-size:11px; }
.ed-navbtn:hover { background:var(--bg-muted); }
.ed-pagenum { padding:0 8px; font-weight:var(--fw-bold); }
.ed-zoom { display:inline-flex; align-items:center; gap:3px; font-family:var(--f-mono); font-size:var(--fs-xs); color:var(--text-sec); }
.ed-iconbtn { width:28px; height:28px; border-radius:var(--r-sm); border:1px solid var(--border); background:var(--bg); color:var(--text-sec);
  display:inline-flex; align-items:center; justify-content:center; cursor:pointer; font-size:13px; }
.ed-iconbtn:hover { background:var(--bg-muted); color:var(--text); }
.ed-syncdot { display:inline-flex; align-items:center; gap:5px; padding:4px 9px; border-radius:var(--r-pill); font-family:var(--f-mono); font-size:10px; font-weight:var(--fw-bold); }
.ed-syncdot.on { background:hsl(var(--c-success) / .14); color:hsl(var(--c-success)); }
.ed-syncdot.off { background:var(--bg-muted); color:var(--text-muted); }
.ed-syncdot i { width:7px; height:7px; border-radius:50%; background:currentColor; }

.ed-rbody { flex:1; overflow-y:auto; padding:18px; min-height:0; display:flex; justify-content:center; }
/* PDF page */
.ed-page { width:100%; max-width:420px; background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-sm);
  box-shadow:var(--shadow-md); padding:34px 32px 44px; font-family:Georgia, 'Times New Roman', serif; align-self:flex-start; }
.ed-page .ph { text-align:center; border-bottom:1px solid var(--border); padding-bottom:12px; margin-bottom:16px; }
.ed-page .ph .pt { font-size:17px; font-weight:bold; color:var(--text); letter-spacing:.01em; }
.ed-page .ph .ps { font-size:11px; color:var(--text-muted); font-family:var(--f-mono); margin-top:4px; }
.ed-para { margin-bottom:14px; padding:4px 8px; border-radius:var(--r-xs); transition:background var(--dur-md) var(--ease-out), box-shadow var(--dur-md) var(--ease-out); }
.ed-para h6 { font-size:13px; font-weight:bold; color:var(--text); margin:0 0 4px; font-family:Georgia, serif; }
.ed-para p { font-size:12.5px; line-height:1.62; color:var(--text-sec); margin:0; text-align:justify; }
.ed-para.hl { background:hsl(var(--c-game) / .18); box-shadow:inset 3px 0 0 hsl(var(--c-game)), 0 0 0 1px hsl(var(--c-game) / .3); }
.ed-rempty { display:flex; flex-direction:column; align-items:center; justify-content:center; text-align:center; color:var(--text-muted); padding:40px; gap:10px; align-self:center; }
.ed-rempty .em { font-size:38px; opacity:.6; }
.ed-rempty .tx { font-size:var(--fs-sm); max-width:220px; }

/* versions panel (published) */
.ed-versions { width:100%; max-width:340px; align-self:flex-start; }
.ed-versions h5 { font-family:var(--f-mono); font-size:var(--fs-xs); text-transform:uppercase; letter-spacing:.08em; color:var(--text-muted); margin-bottom:12px; }
.ed-vrow { display:flex; align-items:center; gap:10px; padding:11px 12px; border-radius:var(--r-md); border:1px solid var(--border); background:var(--bg-card); margin-bottom:8px; }
.ed-vrow.cur { border-color:hsl(var(--c-success) / .45); background:hsl(var(--c-success) / .06); }
.ed-vtag { font-family:var(--f-mono); font-weight:var(--fw-bold); font-size:var(--fs-sm); color:var(--text); }
.ed-vrow.cur .ed-vtag { color:hsl(var(--c-success)); }
.ed-vmeta { font-size:var(--fs-xs); color:var(--text-muted); margin-top:2px; }
.ed-vbadge2 { margin-left:auto; font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); padding:2px 8px; border-radius:var(--r-pill); background:hsl(var(--c-success)); color:#fff; }
.ed-vrest { margin-left:auto; padding:4px 9px; border-radius:var(--r-sm); border:1px solid var(--border); background:var(--bg); color:var(--text-sec); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-xs); cursor:pointer; }

/* footer */
.ed-footer { flex-shrink:0; display:flex; align-items:center; gap:14px; padding:10px 18px;
  background:var(--bg-card); border-top:1px solid var(--border); }
.ed-footer .fl { min-width:0; }
.ed-footer .fl .ln1 { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); color:var(--text); }
.ed-footer .fl .ln2 { font-size:var(--fs-xs); color:var(--text-muted); margin-top:1px; }
.ed-footer .grow { flex:1; }
.ed-fbtn { display:inline-flex; align-items:center; gap:6px; padding:8px 13px; border-radius:var(--r-md); border:1px solid var(--border);
  background:var(--bg-muted); color:var(--text-sec); font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); cursor:pointer; white-space:nowrap; }
.ed-fbtn:hover { background:var(--bg-hover); color:var(--text); }
.ed-kbd { font-family:var(--f-mono); font-size:10px; background:var(--bg-muted); border:1px solid var(--border); border-bottom-width:2px; border-radius:var(--r-xs); padding:1px 5px; color:var(--text-sec); }

/* ─── overlays ─── */
.ed-saveveil { position:absolute; inset:0; z-index:var(--z-overlay); background:hsl(var(--c-game) / .04);
  display:flex; align-items:flex-start; justify-content:center; padding-top:90px; pointer-events:none; }
.ed-savebar { position:absolute; top:0; left:0; height:3px; background:hsl(var(--c-warning)); z-index:var(--z-overlay); animation:edsave 1.4s var(--ease-in-out) infinite; }
@keyframes edsave { 0%{ left:-30%; width:30%; } 60%{ left:60%; width:45%; } 100%{ left:100%; width:30%; } }

/* loader overlay (lock-acquiring) */
.ed-loadveil { position:absolute; inset:0; z-index:var(--z-modal); background:rgba(20,12,4,.42); backdrop-filter:blur(2px);
  display:flex; flex-direction:column; align-items:center; justify-content:center; gap:16px; color:#fff; }
.ed-spin { width:38px; height:38px; border-radius:50%; border:3px solid rgba(255,255,255,.3); border-top-color:hsl(var(--c-player)); animation:edspin .8s linear infinite; }
@keyframes edspin { to { transform:rotate(360deg); } }
.ed-loadveil .lt { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-base); }

/* toast */
.ed-toast { position:absolute; right:18px; bottom:74px; z-index:var(--z-toast); max-width:320px;
  display:flex; align-items:center; gap:11px; padding:13px 15px; border-radius:var(--r-lg);
  background:var(--bg-card); border:1px solid hsl(var(--c-success) / .4); border-left:4px solid hsl(var(--c-success));
  box-shadow:var(--shadow-lg); animation:edtoast var(--dur-lg) var(--ease-spring); }
@keyframes edtoast { from{ transform:translateY(20px); opacity:0; } to{ transform:none; opacity:1; } }
.ed-toast .ti { width:28px; height:28px; border-radius:50%; flex-shrink:0; display:flex; align-items:center; justify-content:center; font-size:14px; background:hsl(var(--c-success)); color:#fff; }
.ed-toast .tt { font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-sm); color:var(--text); }

/* conflict modal */
.ed-modal-veil { position:absolute; inset:0; z-index:var(--z-modal); background:rgba(20,12,4,.5); backdrop-filter:blur(3px);
  display:flex; align-items:center; justify-content:center; padding:24px; animation:edfade var(--dur-md) var(--ease-out); }
@keyframes edfade { from{ opacity:0; } to{ opacity:1; } }
.ed-modal { width:100%; max-width:460px; background:var(--bg-card); border:1px solid var(--border); border-radius:var(--r-xl);
  box-shadow:var(--shadow-lg); overflow:hidden; animation:edpop var(--dur-md) var(--ease-spring); }
@keyframes edpop { from{ transform:translateY(12px) scale(.97); opacity:0; } to{ transform:none; opacity:1; } }
.ed-modal .mh { display:flex; align-items:center; gap:12px; padding:18px 20px 14px; border-bottom:1px solid var(--border); }
.ed-modal .mh .ic { width:40px; height:40px; border-radius:50%; flex-shrink:0; display:flex; align-items:center; justify-content:center; font-size:20px; background:hsl(var(--c-danger) / .14); color:hsl(var(--c-danger)); }
.ed-modal .mh h3 { font-family:var(--f-display); font-size:var(--fs-lg); }
.ed-modal .mb { padding:16px 20px; }
.ed-modal .mb p { font-size:var(--fs-base); color:var(--text-sec); line-height:var(--lh-body); }
.ed-modal .mf { display:flex; flex-direction:column; gap:8px; padding:6px 20px 20px; }
.ed-cbtn { padding:11px 14px; border-radius:var(--r-md); border:1px solid var(--border); cursor:pointer; text-align:left;
  font-family:var(--f-display); font-weight:var(--fw-bold); font-size:var(--fs-base); background:var(--bg); color:var(--text);
  display:flex; align-items:center; gap:10px; transition:all var(--dur-sm) var(--ease-out); }
.ed-cbtn:hover { transform:translateY(-1px); border-color:var(--border-strong); box-shadow:var(--shadow-xs); }
.ed-cbtn .csub { font-weight:var(--fw-reg); font-size:var(--fs-xs); color:var(--text-muted); margin-left:auto; }
.ed-cbtn.primary { background:hsl(var(--c-session)); border-color:transparent; color:#fff; }
.ed-cbtn.primary .csub { color:rgba(255,255,255,.8); }

/* skeleton */
@keyframes edshimmer { 0%{ background-position:-400px 0; } 100%{ background-position:400px 0; } }
.ed-sk { border-radius:var(--r-sm); background:linear-gradient(90deg,var(--bg-muted) 25%,var(--bg-hover) 37%,var(--bg-muted) 63%); background-size:800px 100%; animation:edshimmer 1.4s linear infinite; }

/* mobile bottom-sheet PDF */
.ed-sheet-veil { position:absolute; inset:0; z-index:var(--z-drawer); background:rgba(20,12,4,.4); display:flex; align-items:flex-end; animation:edfade var(--dur-md) var(--ease-out); }
.ed-sheet { width:100%; max-height:78%; background:var(--bg-card); border-radius:var(--r-2xl) var(--r-2xl) 0 0; box-shadow:var(--shadow-drawer);
  display:flex; flex-direction:column; overflow:hidden; animation:edsheet var(--dur-md) var(--ease-spring); }
@keyframes edsheet { from{ transform:translateY(100%); } to{ transform:none; } }
.ed-sheet .grab { width:38px; height:4px; border-radius:var(--r-pill); background:var(--border-strong); margin:9px auto 4px; }
.ed-sheet .sh { display:flex; align-items:center; gap:8px; padding:6px 16px 10px; border-bottom:1px solid var(--border); }
.ed-sheet .sb { flex:1; overflow-y:auto; padding:16px; display:flex; justify-content:center; }

/* mobile adaptations */
.ed-app.is-mobile .ed-split { display:flex; }
.ed-app.is-mobile .ed-pane.right { display:none; }
.ed-app.is-mobile .ed-pane.left { flex:1; }
.ed-app.is-mobile .ed-lhead { padding:9px 12px; gap:8px; }
.ed-app.is-mobile .ed-toolbar { flex-wrap:wrap; }
.ed-app.is-mobile .ed-search { flex:1 1 100%; min-width:0; order:-1; }
.ed-app.is-mobile .ed-footer .grow { display:none; }
.ed-app.is-mobile .ed-fbtn.hideM { display:none; }
.ed-app.is-mobile .ed-publish { flex:1; justify-content:center; }
.ed-app.is-mobile .ed-toast { bottom:88px; }

@media (prefers-reduced-motion: reduce) {
  .ed-pill .pulse, .ed-savebar, .ed-sk, .ed-spin, .ed-saveveil { animation:none; }
}
`;

/* ──────────────────────────────────────────────────────────
   RuleSpec di Catan — array flat di RuleAtom (dati realistici IT)
   ────────────────────────────────────────────────────────── */
const SECTIONS = ['Preparazione', 'Produzione risorse', 'Costruzione', 'Commercio', 'Punti vittoria'];
const BASE_ATOMS = [
  { id: 'a1', section: 'Preparazione', text: 'Disponi i 19 esagoni terreno in modo casuale per formare l’isola al centro del tavolo.', page: 4, sec: '1.1' },
  { id: 'a2', section: 'Preparazione', text: 'Posiziona i gettoni numero sugli esagoni seguendo l’ordine alfabetico delle lettere A–R.', page: 4, sec: '1.2' },
  { id: 'a3', section: 'Preparazione', text: 'Ogni giocatore piazza a turno 2 strade e 2 insediamenti iniziali sugli incroci.', page: 4, sec: '1.3' },
  { id: 'a4', section: 'Produzione risorse', text: 'All’inizio del turno tira due dadi: la somma attiva tutti gli esagoni con quel numero.', page: 5, sec: '2.1' },
  { id: 'a5', section: 'Produzione risorse', text: 'Ogni insediamento adiacente a un esagono attivato produce 1 risorsa del suo tipo; le città ne producono 2.', page: 5, sec: '2.2' },
  { id: 'a6', section: 'Produzione risorse', text: 'Se esce 7 nessuno produce: si sposta il ladro e si ruba una carta a un avversario adiacente.', page: 6, sec: '2.3' },
  { id: 'a7', section: 'Costruzione', text: 'Strada: costa 1 legno + 1 mattone. Deve collegarsi a una tua struttura esistente.', page: 7, sec: '3.1' },
  { id: 'a8', section: 'Costruzione', text: 'Insediamento: costa 1 legno + 1 mattone + 1 lana + 1 grano. Vale 1 punto vittoria.', page: 7, sec: '3.2' },
  { id: 'a9', section: 'Costruzione', text: 'Città: costa 2 grano + 3 minerale e sostituisce un insediamento. Vale 2 punti vittoria.', page: 8, sec: '3.3' },
  { id: 'a10', section: 'Commercio', text: 'Commercio marittimo 4:1 con la banca, oppure 3:1 / 2:1 usando i porti.', page: 9, sec: '4.1' },
  { id: 'a11', section: 'Commercio', text: 'Commercio tra giocatori: libero durante la fase di commercio, prima di costruire.', page: 9, sec: '4.2' },
  { id: 'a12', section: 'Punti vittoria', text: 'Vince chi raggiunge per primo 10 punti vittoria nel proprio turno.', page: 10, sec: '5.1' },
];

const PDF_PARAS = [
  { sec: '1.1', h: '1.1 — Preparazione dell’isola', t: 'I diciannove esagoni di terreno vengono disposti in modo casuale a comporre l’isola. La cornice di mare con i porti circonda il tavoliere. Si raccomanda, per le prime partite, la disposizione fissa illustrata nel regolamento.' },
  { sec: '1.2', h: '1.2 — Gettoni numero', t: 'I gettoni numero recano le lettere A–R. Vanno collocati sugli esagoni terra seguendo l’ordine alfabetico in senso antiorario, partendo da un angolo qualsiasi. Il deserto non riceve alcun gettone e ospita inizialmente il ladro.' },
  { sec: '2.1', h: '2.1 — Lancio dei dadi', t: 'All’inizio di ogni turno il giocatore attivo lancia i due dadi. La somma ottenuta determina quali esagoni producono risorse in questo turno: tutti gli esagoni contrassegnati con quel numero si attivano simultaneamente.' },
  { sec: '3.1', h: '3.1 — Costruire strade', t: 'Una strada costa una carta legno e una carta mattone. Ogni strada deve connettersi a una propria strada, a un proprio insediamento o a una propria città. Due strade non possono occupare lo stesso bordo.' },
  { sec: '4.1', h: '4.1 — Commercio con la banca', t: 'Durante la propria fase di commercio è sempre possibile scambiare quattro carte risorsa identiche con una carta risorsa qualsiasi dalla banca. I porti riducono il rapporto a 3:1 o, per i porti specializzati, a 2:1.' },
];

/* ──────────────────────────────────────────────────────────
   Scenari (11 stati)
   ────────────────────────────────────────────────────────── */
const SCENARIOS = {
  'default':            { sel: 'a1',  edit: null, save: 'saved',   valid: true,  banner: null, toast: false, modal: false, loadVeil: false, readonly: false, empty: false, loading: false, versions: false, veil: false },
  'editing':            { sel: 'a2',  edit: 'a2', save: 'unsaved', valid: true,  banner: null, toast: false, modal: false, loadVeil: false, readonly: false, empty: false, loading: false, versions: false, veil: false },
  'saving':             { sel: 'a1',  edit: null, save: 'saving',  valid: true,  banner: null, toast: false, modal: false, loadVeil: false, readonly: false, empty: false, loading: false, versions: false, veil: true },
  'saved':              { sel: 'a1',  edit: null, save: 'saved',   valid: true,  banner: null, toast: true,  modal: false, loadVeil: false, readonly: false, empty: false, loading: false, versions: false, veil: false },
  'validation-error':   { sel: 'aX',  edit: 'aX', save: 'unsaved', valid: false, banner: null, toast: false, modal: false, loadVeil: false, readonly: false, empty: false, loading: false, versions: false, veil: false },
  'conflict-detected':  { sel: 'a1',  edit: null, save: 'conflict',valid: true,  banner: null, toast: false, modal: true,  loadVeil: false, readonly: false, empty: false, loading: false, versions: false, veil: false },
  'published':          { sel: 'a1',  edit: null, save: 'saved',   valid: true,  banner: 'pub',toast: false, modal: false, loadVeil: false, readonly: false, empty: false, loading: false, versions: true,  veil: false },
  'lock-acquiring':     { sel: 'a1',  edit: null, save: 'saved',   valid: true,  banner: null, toast: false, modal: false, loadVeil: true,  readonly: true,  empty: false, loading: false, versions: false, veil: false },
  'lock-held-by-other': { sel: 'a1',  edit: null, save: 'saved',   valid: true,  banner: 'lock',toast: false,modal: false, loadVeil: false, readonly: true,  empty: false, loading: false, versions: false, veil: false },
  'loading':            { sel: null,  edit: null, save: 'saved',   valid: true,  banner: null, toast: false, modal: false, loadVeil: false, readonly: true,  empty: false, loading: true,  versions: false, veil: false },
  'empty':              { sel: null,  edit: null, save: 'saved',   valid: true,  banner: null, toast: false, modal: false, loadVeil: false, readonly: true,  empty: true,  loading: false, versions: false, veil: false },
};
const STATE_LIST = [
  ['default', 'Default'], ['editing', 'Editing'], ['saving', 'Saving'], ['saved', 'Saved'],
  ['validation-error', 'Validation error'], ['conflict-detected', 'Conflict'], ['published', 'Published'],
  ['lock-acquiring', 'Lock acquiring'], ['lock-held-by-other', 'Lock altrui'], ['loading', 'Loading'], ['empty', 'Empty'],
];

/* ──────────────────────────────────────────────────────────
   Sub-components
   ────────────────────────────────────────────────────────── */
function SaveStatus({ save }) {
  const map = {
    saved:    ['ok', <>✓ Salvato 30s fa</>],
    unsaved:  ['warn', <><span className="dot" /> Modifiche non salvate</>],
    saving:   ['warn', <><span className="pulse" /> Salvataggio…</>],
    conflict: ['danger', <><span className="dot" /> Conflitto rilevato</>],
  };
  const [cls, content] = map[save] || map.saved;
  return <span className={'ed-pill ' + cls} role="status" aria-live="polite">{content}</span>;
}

function LeftHeader({ sc, canPublish }) {
  return (
    <header className="ed-lhead">
      <div className="ed-brand">M</div>
      <button className="ed-gamesel" aria-haspopup="listbox" aria-label="Seleziona gioco — attuale: Catan">
        🎲 Catan <span className="chev">▼</span>
      </button>
      <div className="ed-title">Editor regole</div>
      <div className="grow" />
      <SaveStatus save={sc.save} />
      {sc.valid
        ? <span className="ed-pill ok btn" title="Nessun problema">✓ Valido</span>
        : <span className="ed-pill warn btn" title="1 problema — vai all’atom">⚠ 1 problema</span>}
      <button className="ed-publish" disabled={!canPublish} title="Pubblica — Ctrl+Enter">
        🚀 {sc.banner === 'pub' ? 'Pubblicato' : 'Pubblica'}
      </button>
    </header>
  );
}

function Toolbar({ search, setSearch, sectionFilter, setSectionFilter, viewMode, setViewMode }) {
  return (
    <div className="ed-toolbar" role="toolbar" aria-label="Strumenti editor">
      <div className="ed-search">
        <span className="ic">🔍</span>
        <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Cerca in testo e sezioni…" aria-label="Cerca atom (Ctrl+F)" />
      </div>
      <div className="ed-selectw">
        <select className="ed-select" value={sectionFilter} onChange={e => setSectionFilter(e.target.value)} aria-label="Filtra per sezione">
          <option value="">Tutte le sezioni</option>
          {SECTIONS.map(s => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>
      <button className="ed-tbtn" title="Crea nuova sezione (Ctrl+N)">+ Sezione</button>
      <div className="ed-vtoggle" role="group" aria-label="Vista atom">
        <button aria-pressed={viewMode === 'accordion'} onClick={() => setViewMode('accordion')} title="Vista accordion" aria-label="Vista accordion">☰</button>
        <button aria-pressed={viewMode === 'flat'} onClick={() => setViewMode('flat')} title="Vista lista piatta" aria-label="Vista lista piatta">≣</button>
      </div>
    </div>
  );
}

function AtomCard({ atom, n, selected, editing, invalid, dirty, saving, readonly, onSelect, onEdit, onRefClick }) {
  const cls = ['ed-atom', selected && 'sel', editing && 'editing', invalid && 'invalid'].filter(Boolean).join(' ');
  return (
    <div className={cls} tabIndex={0} role="button" aria-pressed={selected}
         onClick={() => !readonly && onSelect(atom.id)}
         onKeyDown={e => { if ((e.key === 'Enter' || e.key === ' ') && !readonly) { e.preventDefault(); onSelect(atom.id); } }}>
      {dirty && <span className="ed-dirty" title="Modifiche non salvate" />}
      <span className="num">{n}.</span>
      <div className="body">
        {editing ? (
          <>
            <textarea className={'ta' + (invalid ? ' bad' : '')} defaultValue={atom.text} autoFocus
                      aria-label={'Testo atom ' + n} aria-invalid={invalid}
                      placeholder="Scrivi il testo della regola…" />
            {invalid && <div className="ed-aerr" role="alert" aria-live="assertive">⚠ Il testo non può essere vuoto</div>}
            <div className="ed-editrow">
              <button className="ed-ebtn pri" disabled={invalid}>Salva</button>
              <button className="ed-ebtn sec">Annulla <span className="ed-kbd" style={{ marginLeft: 4 }}>Esc</span></button>
            </div>
          </>
        ) : (
          <div className="text">{atom.text || <span style={{ color: 'hsl(var(--c-danger))', fontStyle: 'italic' }}>(testo mancante)</span>}</div>
        )}
        {!editing && (
          <div className="ed-afoot">
            <span className="ed-aref" title="Vai al PDF" onClick={(e) => { e.stopPropagation(); onRefClick(atom); }}>📄 p.{atom.page} · §{atom.sec}</span>
            <span className="grow" />
            {!readonly && <>
              <button className="ed-aact" aria-label={'Modifica atom ' + n} title="Modifica" onClick={(e) => { e.stopPropagation(); onEdit(atom.id); }}>✏️</button>
              <button className="ed-aact" aria-label={'Elimina atom ' + n} title="Elimina">🗑</button>
            </>}
          </div>
        )}
      </div>
      {saving && <div className="ed-asaving"><span className="lbl"><span className="ed-pill warn" style={{ padding: 0, background: 'none' }}><span className="pulse" /></span> Salvataggio…</span></div>}
    </div>
  );
}

function AtomListBody({ atoms, viewMode, sc, expanded, toggleSection, selId, editId, savingId, readonly, onSelect, onEdit, onRefClick }) {
  const numberOf = (id) => atoms.findIndex(a => a.id === id) + 1;

  if (viewMode === 'flat') {
    return (
      <div className="ed-lbody">
        <div className="ed-satoms" style={{ paddingLeft: 0 }}>
          {atoms.map((a) => (
            <AtomCard key={a.id} atom={a} n={numberOf(a.id)}
                      selected={a.id === selId} editing={a.id === editId} invalid={a.id === editId && !sc.valid}
                      dirty={a.id === editId && sc.save === 'unsaved'} saving={a.id === savingId} readonly={readonly}
                      onSelect={onSelect} onEdit={onEdit} onRefClick={onRefClick} />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="ed-lbody">
      {SECTIONS.map(sec => {
        const list = atoms.filter(a => a.section === sec);
        if (list.length === 0 && sec !== 'Punti vittoria') return null; // hide empty sections except demo one
        const open = expanded[sec] !== false;
        return (
          <section className="ed-section" key={sec} role="region" aria-label={'Sezione: ' + sec}>
            <div className="ed-shead" role="button" tabIndex={0} aria-expanded={open}
                 onClick={() => toggleSection(sec)}
                 onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggleSection(sec); } }}>
              <span className="ed-caret" style={{ transform: open ? 'none' : 'rotate(-90deg)' }}>▼</span>
              <span className="ed-sname">{sec}</span>
              <span className="ed-scount">({list.length} atom)</span>
              <span className="grow" />
              {!readonly && <button className="ed-saddbtn" onClick={(e) => { e.stopPropagation(); }} title="Aggiungi atom (Ctrl+N)">+ Atom</button>}
              {!readonly && <button className="ed-ctx" aria-label={'Azioni sezione ' + sec} title="Rinomina / sposta / elimina">⋮</button>}
            </div>
            {open && (
              <div className="ed-satoms">
                {list.length === 0 ? (
                  <div className="ed-sempty">
                    <div className="tx">Nessun atom in questa sezione</div>
                    {!readonly && <button className="ed-tbtn" style={{ margin: '0 auto' }}>+ Aggiungi il primo atom</button>}
                  </div>
                ) : list.map(a => (
                  <AtomCard key={a.id} atom={a} n={numberOf(a.id)}
                            selected={a.id === selId} editing={a.id === editId} invalid={a.id === editId && !sc.valid}
                            dirty={a.id === editId && sc.save === 'unsaved'} saving={a.id === savingId} readonly={readonly}
                            onSelect={onSelect} onEdit={onEdit} onRefClick={onRefClick} />
                ))}
              </div>
            )}
          </section>
        );
      })}
    </div>
  );
}

function PdfHeader({ page, sync }) {
  return (
    <header className="ed-rhead">
      <button className="ed-kbpip" title="Apri documento KB" aria-label="Documento sorgente: catan-rules.pdf">📄 catan-rules.pdf</button>
      <div className="grow" />
      <div className="ed-pagenav">
        <button className="ed-navbtn" aria-label="Pagina precedente del PDF" title="PageUp">◀</button>
        <span className="ed-pagenum">p.{page} / 24</span>
        <button className="ed-navbtn" aria-label="Pagina successiva del PDF" title="PageDown">▶</button>
      </div>
      <div className="ed-zoom">
        <button className="ed-navbtn" aria-label="Riduci zoom">−</button>
        <span className="ed-pagenum">100%</span>
        <button className="ed-navbtn" aria-label="Aumenta zoom">+</button>
      </div>
      <button className="ed-iconbtn" aria-label="Apri PDF a schermo intero" title="Schermo intero">⛶</button>
      <span className={'ed-syncdot ' + (sync ? 'on' : 'off')} title={sync ? 'Sincronizzato con l’atom selezionato' : 'Navigazione manuale'}>
        <i />{sync ? 'Sync' : 'Manuale'}
      </span>
    </header>
  );
}

function PdfPage({ page, sec }) {
  return (
    <div className="ed-page">
      <div className="ph">
        <div className="pt">Le regole di Catan</div>
        <div className="ps">pagina {page} di 24 · catan-rules.pdf</div>
      </div>
      {PDF_PARAS.map(p => (
        <div className={'ed-para' + (p.sec === sec ? ' hl' : '')} key={p.sec}>
          <h6>{p.h}</h6>
          <p>{p.t}</p>
        </div>
      ))}
    </div>
  );
}

function VersionsPanel() {
  const rows = [
    { v: 'v2.4', meta: 'Pubblicata adesso · Marco R.', cur: true },
    { v: 'v2.3', meta: '2 ore fa · Marco R. · 12 atom' },
    { v: 'v2.2', meta: 'Ieri · Sara T. · 11 atom' },
    { v: 'v2.1', meta: '3 giorni fa · importata da PDF' },
  ];
  return (
    <div className="ed-versions">
      <h5>🕓 Cronologia versioni</h5>
      {rows.map(r => (
        <div className={'ed-vrow' + (r.cur ? ' cur' : '')} key={r.v}>
          <div>
            <div className="ed-vtag">{r.v}</div>
            <div className="ed-vmeta">{r.meta}</div>
          </div>
          {r.cur ? <span className="ed-vbadge2">Attuale</span> : <button className="ed-vrest">Ripristina</button>}
        </div>
      ))}
    </div>
  );
}

function PdfPane({ sc, selAtom, versions }) {
  return (
    <aside className="ed-pane right" aria-label="Anteprima PDF">
      {versions ? (
        <>
          <header className="ed-rhead"><span style={{ fontFamily: 'var(--f-display)', fontWeight: 700 }}>Versioni</span></header>
          <div className="ed-rbody"><VersionsPanel /></div>
        </>
      ) : (
        <>
          <PdfHeader page={selAtom ? selAtom.page : 4} sync={!!selAtom} />
          <div className="ed-rbody">
            {selAtom
              ? <PdfPage page={selAtom.page} sec={selAtom.sec} />
              : <div className="ed-rempty"><div className="em">📄</div><div className="tx">Seleziona un atom per vedere la pagina corrispondente del PDF.</div></div>}
          </div>
        </>
      )}
    </aside>
  );
}

function Footer({ atomCount, canPublish, mobile }) {
  return (
    <footer className="ed-footer">
      <div className="fl">
        <div className="ln1">{atomCount} atom in {SECTIONS.length} sezioni · Catan v2.3</div>
        <div className="ln2">Modificato 30s fa da Marco · <span className="ed-kbd">Ctrl</span> <span className="ed-kbd">S</span> salva · <span className="ed-kbd">Ctrl</span> <span className="ed-kbd">Z</span> annulla</div>
      </div>
      <div className="grow" />
      {!mobile && <button className="ed-fbtn hideM">Annulla</button>}
      <button className="ed-fbtn hideM">Anteprima diff</button>
      <button className="ed-publish" disabled={!canPublish}>🚀 Pubblica</button>
    </footer>
  );
}

/* overlays */
function ConflictModal() {
  return (
    <div className="ed-modal-veil">
      <div className="ed-modal" role="alertdialog" aria-modal="true" aria-labelledby="cfTitle" aria-describedby="cfDesc">
        <div className="mh">
          <div className="ic">⚠</div>
          <div>
            <h3 id="cfTitle">Conflitto rilevato</h3>
            <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>RuleSpec · Catan</div>
          </div>
        </div>
        <div className="mb">
          <p id="cfDesc">La tua versione diverge da <strong>v2.4</strong>, modificata 2 minuti fa da{' '}
            <span className="ed-pchip"><span className="av">AL</span> Alice V.</span>. Scegli come procedere.</p>
        </div>
        <div className="mf">
          <button className="ed-cbtn primary">✔ Mantieni la mia <span className="csub">scarta v2.4</span></button>
          <button className="ed-cbtn">↧ Usa la sua <span className="csub">scarta le mie modifiche</span></button>
          <button className="ed-cbtn">⤬ Confronta diff <span className="csub">apri confronto a 3 vie</span></button>
        </div>
      </div>
    </div>
  );
}

function MobilePdfSheet({ atom, onClose }) {
  return (
    <div className="ed-sheet-veil" onClick={onClose}>
      <div className="ed-sheet" role="dialog" aria-label="Anteprima PDF" onClick={e => e.stopPropagation()}>
        <div className="grab" />
        <div className="sh">
          <button className="ed-kbpip">📄 catan-rules.pdf</button>
          <div style={{ flex: 1 }} />
          <span className="ed-pagenum" style={{ fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)', color: 'var(--text-sec)' }}>p.{atom.page} / 24</span>
          <button className="ed-iconbtn" aria-label="Chiudi" onClick={onClose}>✕</button>
        </div>
        <div className="sb"><PdfPage page={atom.page} sec={atom.sec} /></div>
      </div>
    </div>
  );
}

/* loading skeleton */
function LoadingSkeleton({ mobile }) {
  return (
    <div className={'ed-app' + (mobile ? ' is-mobile' : '')} style={{ pointerEvents: 'none' }}>
      <header className="ed-lhead">
        <div className="ed-sk" style={{ width: 28, height: 28, borderRadius: 'var(--r-sm)' }} />
        <div className="ed-sk" style={{ width: 78, height: 28, borderRadius: 'var(--r-pill)' }} />
        <div className="ed-sk" style={{ width: 110, height: 16 }} />
        <div className="grow" />
        <div className="ed-sk" style={{ width: 96, height: 28, borderRadius: 'var(--r-pill)' }} />
        <div className="ed-sk" style={{ width: 100, height: 32, borderRadius: 'var(--r-md)' }} />
      </header>
      <div className="ed-toolbar">
        <div className="ed-sk" style={{ flex: 1, height: 32, borderRadius: 'var(--r-md)' }} />
        <div className="ed-sk" style={{ width: 130, height: 32, borderRadius: 'var(--r-md)' }} />
        <div className="ed-sk" style={{ width: 90, height: 32, borderRadius: 'var(--r-md)' }} />
      </div>
      <div className="ed-split">
        <div className="ed-pane left">
          <div className="ed-lbody">
            {[0, 1, 2, 3, 4].map(i => (
              <div key={i} style={{ marginBottom: 12 }}>
                <div className="ed-sk" style={{ width: '100%', height: 38, borderRadius: 'var(--r-md)', marginBottom: 8 }} />
                <div className="ed-sk" style={{ width: '94%', height: 56, borderRadius: 'var(--r-md)', marginLeft: 'auto' }} />
              </div>
            ))}
          </div>
        </div>
        {!mobile && (
          <div className="ed-pane right">
            <div className="ed-rbody"><div className="ed-sk" style={{ width: '100%', maxWidth: 420, height: 540, borderRadius: 'var(--r-sm)' }} /></div>
          </div>
        )}
      </div>
      <footer className="ed-footer"><div className="ed-sk" style={{ width: 240, height: 18 }} /><div className="grow" /><div className="ed-sk" style={{ width: 100, height: 32, borderRadius: 'var(--r-md)' }} /></footer>
    </div>
  );
}

/* empty state */
function LeftHeaderEmpty() {
  return (
    <header className="ed-lhead">
      <div className="ed-brand">M</div>
      <button className="ed-gamesel" aria-label="Gioco: Catan">🎲 Catan <span className="chev">▼</span></button>
      <div className="ed-title">Editor regole</div>
      <div className="grow" />
      <span className="ed-pill warn">Nessun RuleSpec</span>
    </header>
  );
}
function EmptyState({ mobile }) {
  return (
    <div className={'ed-app' + (mobile ? ' is-mobile' : '')}>
      <LeftHeaderEmpty />
      <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 32 }}>
        <div style={{ textAlign: 'center', maxWidth: 360 }}>
          <div style={{ fontSize: 46, marginBottom: 12 }}>📋</div>
          <h3 style={{ fontFamily: 'var(--f-display)', fontSize: 'var(--fs-xl)', marginBottom: 8 }}>Nessuna regola ancora indicizzata</h3>
          <p style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-md)', lineHeight: 'var(--lh-body)', marginBottom: 20 }}>
            Catan non ha ancora un RuleSpec. Carica il PDF delle regole per estrarre gli atom automaticamente, oppure creali a mano.
          </p>
          <div style={{ display: 'flex', gap: 10, justifyContent: 'center', flexWrap: 'wrap' }}>
            <button className="ed-publish" style={{ background: 'hsl(var(--c-kb))' }}>📄 Carica PDF regole</button>
            <button className="ed-tbtn" style={{ padding: '8px 15px' }}>+ Crea manualmente</button>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ──────────────────────────────────────────────────────────
   EditorApp — un'istanza per (state × viewport). Remount via key.
   ────────────────────────────────────────────────────────── */
function EditorApp({ stateId, mobile }) {
  const sc = SCENARIOS[stateId];

  const atoms = useMemo(() => {
    if (stateId === 'validation-error') {
      return [...BASE_ATOMS, { id: 'aX', section: 'Punti vittoria', text: '', page: 10, sec: '5.2' }];
    }
    return BASE_ATOMS;
  }, [stateId]);

  const [selId, setSelId] = useState(sc.sel);
  const [editId, setEditId] = useState(sc.edit);
  const [search, setSearch] = useState('');
  const [sectionFilter, setSectionFilter] = useState('');
  const [viewMode, setViewMode] = useState('accordion');
  const [expanded, setExpanded] = useState({});
  const [sheetAtom, setSheetAtom] = useState(null);

  if (sc.loading) return <LoadingSkeleton mobile={mobile} />;
  if (sc.empty) return <EmptyState mobile={mobile} />;

  const readonly = sc.readonly;
  const savingId = sc.veil ? sc.sel : null;

  const q = search.trim().toLowerCase();
  const filtered = atoms.filter(a =>
    (!sectionFilter || a.section === sectionFilter) &&
    (!q || a.text.toLowerCase().includes(q) || a.section.toLowerCase().includes(q))
  );

  const selAtom = atoms.find(a => a.id === selId) || null;
  const atomCount = atoms.length;
  const canPublish = sc.valid && !readonly && sc.save !== 'saving' && sc.save !== 'conflict' && sc.banner !== 'pub';

  const toggleSection = (s) => setExpanded(e => ({ ...e, [s]: e[s] === false ? true : false }));
  const onSelect = (id) => setSelId(id);
  const onEdit = (id) => { setSelId(id); setEditId(id); };
  const onRefClick = (atom) => { setSelId(atom.id); if (mobile) setSheetAtom(atom); };

  return (
    <div className={'ed-app' + (mobile ? ' is-mobile' : '')}>
      {sc.veil && <div className="ed-savebar" />}

      {sc.banner === 'pub' && (
        <div className="ed-banner pub" role="status">✓ Pubblicato come v2.4 <span className="grow" /><button className="bcta">Vedi changelog</button></div>
      )}
      {sc.banner === 'lock' && (
        <div className="ed-banner lock" role="status">
          🔒 <span className="ed-pchip"><span className="av">MR</span> Marco R.</span> sta modificando — sola lettura
          <span className="grow" /><button className="bcta">Richiedi lock</button>
        </div>
      )}

      <div className="ed-split">
        <div className="ed-pane left">
          <LeftHeader sc={sc} canPublish={canPublish} />
          <Toolbar search={search} setSearch={setSearch} sectionFilter={sectionFilter} setSectionFilter={setSectionFilter}
                   viewMode={viewMode} setViewMode={setViewMode} />
          <AtomListBody atoms={filtered} viewMode={viewMode} sc={sc} expanded={expanded} toggleSection={toggleSection}
                        selId={selId} editId={editId} savingId={savingId} readonly={readonly}
                        onSelect={onSelect} onEdit={onEdit} onRefClick={onRefClick} />
        </div>

        <PdfPane sc={sc} selAtom={selAtom} versions={sc.versions} />
      </div>

      <Footer atomCount={atomCount} canPublish={canPublish} mobile={mobile} />

      {sc.veil && <div className="ed-saveveil" aria-hidden="true" />}
      {sc.toast && <div className="ed-toast" role="status" aria-live="polite"><span className="ti">✓</span><div className="tt">Atom aggiornato</div></div>}
      {sc.modal && <ConflictModal />}
      {sc.loadVeil && (
        <div className="ed-loadveil" role="status" aria-live="polite">
          <div className="ed-spin" />
          <div className="lt">Acquisendo lock per editing collaborativo…</div>
        </div>
      )}
      {sheetAtom && <MobilePdfSheet atom={sheetAtom} onClose={() => setSheetAtom(null)} />}
    </div>
  );
}

/* ──────────────────────────────────────────────────────────
   Harness
   ────────────────────────────────────────────────────────── */
function Harness() {
  const [stateId, setStateId] = useState(() => localStorage.getItem('ed-state2') || 'default');
  const [theme, setTheme] = useState(() => localStorage.getItem('mai-theme') || 'light');

  useEffect(() => { document.documentElement.setAttribute('data-theme', theme); localStorage.setItem('mai-theme', theme); }, [theme]);
  useEffect(() => { localStorage.setItem('ed-state2', stateId); }, [stateId]);

  return (
    <div className="ed-stage">
      <style dangerouslySetInnerHTML={{ __html: EDITOR_CSS }} />
      <button className="theme-toggle" onClick={() => setTheme(theme === 'light' ? 'dark' : 'light')}>🌗 <span>{theme === 'dark' ? 'Dark' : 'Light'}</span></button>

      <div className="ed-wrap">
        <div className="ed-kicker">SP4 · B14 · #1489 — schermata 1 / 5 · REV split-view</div>
        <h1>Editor <span className="acc">regole</span> — /editor</h1>
        <p className="ed-lead">
          Editor del RuleSpec come <b>lista flat di RuleAtom</b> ({'{ id, text, section, page }'}). Niente JSON grezzo:
          un solo editor leggibile a sinistra, anteprima PDF sorgente a destra, sincronizzati. Autosalvataggio, validazione,
          lock collaborativo (#2055), conflitti e pubblicazione versionata.
        </p>

        <div className="ed-notes">
          <div className="ed-note">
            <h4>Pattern (locked)</h4>
            <p><b>Split-view 60/40</b>: pane sinistro = atom-list editor (accordion per <code>section</code>, card editabili inline); pane destro = <b>PDF preview</b> con highlight dell’atom selezionato. Footer sticky con conteggi + Pubblica.</p>
          </div>
          <div className="ed-note">
            <h4>11 stati</h4>
            <p>Selettore qui sotto: default · editing · saving · saved · validation-error · conflict · published · lock-acquiring · lock altrui · loading · empty.</p>
          </div>
          <div className="ed-note">
            <h4>Mobile & a11y</h4>
            <p>Mobile = solo atom-list; il ref <code>📄 p.N</code> apre un <b>bottom-sheet</b> col PDF. <code>role=region</code>/<code>aria-expanded</code> sulle sezioni, <code>aria-live</code> sullo stato, <code>alertdialog</code> sul conflitto, scorciatoie in tooltip.</p>
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

        <div className="ed-vp-label">Desktop · 1440 — split 60 / 40</div>
        <div className="ed-desk">
          <div className="ed-chrome">
            <div className="dots"><i /><i /><i /></div>
            <div className="url">meepleai.app/editor?game=g-catan</div>
          </div>
          <div style={{ flex: 1, minHeight: 0 }}>
            <EditorApp key={'d-' + stateId} stateId={stateId} mobile={false} />
          </div>
        </div>

        <div className="ed-vp-label">Mobile · 375 — stack + drawer PDF</div>
        <div className="ed-phone-row">
          <div className="phone">
            <div className="phone-sbar"><span>9:41</span><span className="ind">●●● 5G ▮</span></div>
            <div style={{ flex: 1, minHeight: 0, display: 'flex' }}>
              <EditorApp key={'m-' + stateId} stateId={stateId} mobile={true} />
            </div>
          </div>
          <div className="ed-phone-cap">
            <h4>Layout mobile</h4>
            <p>Niente split-view: solo l’atom-list. Tocca <code>📄 p.N</code> sotto un atom per aprire un bottom-sheet con la pagina PDF (prova negli stati con atom). La toolbar va a capo su due righe; Pubblica è full-width nel footer.</p>
          </div>
        </div>
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<Harness />);
