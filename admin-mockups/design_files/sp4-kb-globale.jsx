/* MeepleAI SP4 wave 4 — KB GLOBALE (cross-game)
   Route: /kb                       ← QUESTO FILE — Knowledge Base globale dell'utente
   File:  admin-mockups/design_files/sp4-kb-globale.{html,jsx}
   Issue: #488

   ⚠️  DISTINZIONE SEMANTICA — non confondere con sp4-kb-hub.jsx
   ─────────────────────────────────────────────────────────────
   • sp4-kb-globale.jsx (questo)  → KB cross-game · route /kb
       Aggrega tutti i documenti dell'utente attraverso tutti i giochi.
       Primary actor: end-user (qualsiasi utente autenticato).
   • sp4-kb-hub.jsx (#913)        → KB per-game · route /games/[id]/kb
       Hub di gestione PDF di un singolo gioco (admin/curator).
   Screen 5 di questo file è l'unica eccezione: primary actor = curator
   (owner della libreria private del game, NON admin globale).

   7 screen states (stacked verticalmente):
   1. KB Home Desktop globale      — hero search + 3 colonne (shortcuts · docs · agents)
   2. KB Search Results Desktop    — 2-col: risultati + filtri accordion
   3. Document Viewer Desktop      — 3-col: thumbs · PDF · citations panel
   4. Document Viewer Mobile       — full-screen PDF + bottom sheet (Citations/Share)
   5. KB Editor Desktop (curator)  — split-view: doc list + metadata editor
   6. Ask the Agent drawer (SSE)   — 4 sub-states (idle/streaming/completed/error)
   7. Empty state KB globale       — hero + 3 quick start cards

   Variants: light + dark (toggle fixed top-right).
   Entity color: --c-kb (teal) via entityHsl('kb', alpha) ovunque.
   No hard-coded color utilities — solo semantic tokens + entityHsl().

   Playwright handles in Screen 6: data-testid="sse-state-{idle|streaming|completed|error-connection|error-timeout|error-partial}"

   v2 components flagged for impl post-merge:
   - KbGlobalHero          → apps/web/src/components/ui/v2/kb-global-hero/
   - KbSearchResultCard    → apps/web/src/components/ui/v2/kb-search-result-card/
   - KbDocViewerSplit      → apps/web/src/components/ui/v2/kb-doc-viewer-split/
   - KbDocViewerMobile     → apps/web/src/components/ui/v2/kb-doc-viewer-mobile/
   - KbEditorPanel         → apps/web/src/components/ui/v2/kb-editor-panel/
   - AskAgentDrawer (SSE)  → apps/web/src/components/ui/v2/ask-agent-drawer/

   Riusi:
   - EntityChip / StatusBadge      (wave 1, ridefinite qui per self-containment)
   - entityHsl()                   (helper canonico)
   - tokens semantici              (var(--bg-card), var(--text-sec), ecc.)
*/

const { useState, useEffect, useRef } = React;
const DS = window.DS;

// ═══════════════════════════════════════════════════════
// ─── ENTITY HSL HELPER ──────────────────────────────
// ═══════════════════════════════════════════════════════
const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.kb;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};
const kb       = (a) => entityHsl('kb', a);
const agentC   = (a) => entityHsl('agent', a);
const chatC    = (a) => entityHsl('chat', a);
const sessionC = (a) => entityHsl('session', a);
const gameC    = (a) => entityHsl('game', a);
const dangerC  = (a) => a !== undefined ? `hsla(350, 89%, 60%, ${a})` : `hsl(350, 89%, 60%)`;
const warnC    = (a) => a !== undefined ? `hsla(38, 92%, 50%, ${a})`  : `hsl(38, 92%, 50%)`;
const okC      = (a) => a !== undefined ? `hsla(142, 70%, 45%, ${a})` : `hsl(142, 70%, 45%)`;
const goldC    = (a) => a !== undefined ? `hsla(43, 92%, 52%, ${a})`  : `hsl(43, 92%, 52%)`;

// ═══════════════════════════════════════════════════════
// ─── SHARED PRIMITIVES ──────────────────────────────
// ═══════════════════════════════════════════════════════
const EntityChip = ({ type, label, icon, sm }) => (
  <span style={{
    display: 'inline-flex', alignItems: 'center', gap: 5,
    padding: sm ? '2px 8px' : '4px 10px', borderRadius: 'var(--r-pill)',
    background: entityHsl(type, 0.12), color: entityHsl(type),
    border: `1px solid ${entityHsl(type, 0.2)}`,
    fontFamily: 'var(--f-display)', fontSize: sm ? 10 : 11, fontWeight: 700,
    whiteSpace: 'nowrap',
  }}>
    <span aria-hidden="true">{icon || DS.EC[type]?.em}</span>
    <span>{label}</span>
  </span>
);

const STATUS_CFG = {
  ready:    { label: 'Ready',    bg: 'hsl(142,60%,42%)', fg: '#fff' },
  indexing: { label: 'Indexing', bg: 'hsl(38,92%,50%)',  fg: '#fff', pulse: true },
  stale:    { label: 'Stale',    bg: 'hsl(220,15%,55%)', fg: '#fff' },
  failed:   { label: 'Failed',   bg: 'hsl(350,89%,60%)', fg: '#fff' },
};

const StatusBadge = ({ status }) => {
  const cfg = STATUS_CFG[status] || STATUS_CFG.stale;
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 5,
      padding: '3px 9px', borderRadius: 'var(--r-pill)',
      background: cfg.bg, color: cfg.fg,
      fontFamily: 'var(--f-display)', fontSize: 10, fontWeight: 700,
      textTransform: 'uppercase', letterSpacing: '.05em', flexShrink: 0,
    }}>
      <span style={{
        width: 6, height: 6, borderRadius: '50%',
        background: 'rgba(255,255,255,.7)',
        animation: cfg.pulse ? 'pulse 1.4s ease-in-out infinite' : 'none',
      }}/>
      {cfg.label}
    </span>
  );
};

// Tag inline (chip generico)
const Tag = ({ children, color = kb(), bg = kb(0.1), border = kb(0.2), onClick, x }) => (
  <span onClick={onClick} style={{
    display: 'inline-flex', alignItems: 'center', gap: 6,
    padding: '3px 10px', borderRadius: 'var(--r-pill)',
    background: bg, color, border: `1px solid ${border}`,
    fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 600,
    cursor: onClick ? 'pointer' : 'default',
  }}>
    {children}
    {x && <span style={{ opacity: .55, marginLeft: 2, fontSize: 12 }}>×</span>}
  </span>
);

// Pulsante semantico
const Btn = ({ children, variant = 'primary', size = 'md', icon, onClick, fullWidth, disabled, danger, style }) => {
  const sizes = {
    sm: { padding: '6px 12px', fontSize: 11 },
    md: { padding: '9px 16px', fontSize: 12 },
    lg: { padding: '11px 20px', fontSize: 13 },
  };
  const variants = {
    primary: {
      background: danger ? dangerC() : kb(),
      color: '#fff', border: 'none',
      boxShadow: danger ? `0 4px 12px ${dangerC(0.3)}` : `0 4px 12px ${kb(0.25)}`,
    },
    secondary: {
      background: kb(0.08), color: kb(),
      border: `1px solid ${kb(0.25)}`,
    },
    ghost: {
      background: 'transparent', color: 'var(--text-sec)',
      border: `1px solid var(--border)`,
    },
    danger: {
      background: dangerC(), color: '#fff', border: 'none',
      boxShadow: `0 4px 12px ${dangerC(0.3)}`,
    },
    'danger-ghost': {
      background: dangerC(0.08), color: dangerC(),
      border: `1px solid ${dangerC(0.25)}`,
    },
  };
  return (
    <button onClick={(e) => { (onClick)(e); setTimeout(() => { window.location.href = 'sp4-kb-hub.html'; }, 0); /* DEMO-NAV */ }} disabled={disabled} style={{
      ...sizes[size], ...variants[variant],
      width: fullWidth ? '100%' : 'auto',
      display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6,
      borderRadius: 'var(--r-md)',
      fontFamily: 'var(--f-display)', fontWeight: 700,
      cursor: disabled ? 'not-allowed' : 'pointer',
      opacity: disabled ? .5 : 1,
      transition: 'transform var(--dur-xs), box-shadow var(--dur-xs)',
      whiteSpace: 'nowrap',
      ...style,
    }}>
      {icon && <span style={{ fontSize: 13 }}>{icon}</span>}
      {children}
    </button>
  );
};

// Hero search input
const HeroSearch = ({ focused, value = '', placeholder = 'Chiedi al Meeple…' }) => (
  <div style={{
    position: 'relative',
    background: 'var(--bg-card)',
    border: `2px solid ${focused ? kb(0.5) : kb(0.2)}`,
    borderRadius: 'var(--r-2xl)',
    padding: '14px 18px 14px 56px',
    boxShadow: focused
      ? `0 0 0 4px ${kb(0.12)}, var(--shadow-lg)`
      : 'var(--shadow-md)',
    transition: 'all var(--dur-sm) var(--ease-out)',
  }}>
    <span style={{
      position: 'absolute', left: 18, top: '50%', transform: 'translateY(-50%)',
      width: 28, height: 28, borderRadius: 'var(--r-sm)',
      background: kb(0.12), color: kb(),
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontSize: 14, fontWeight: 700,
    }}>🔎</span>
    <input
      value={value}
      placeholder={placeholder}
      readOnly
      style={{
        width: '100%', border: 'none', outline: 'none',
        background: 'transparent', color: 'var(--text)',
        fontFamily: 'var(--f-body)', fontSize: 16, fontWeight: 500,
      }}
    />
    <kbd style={{
      position: 'absolute', right: 18, top: '50%', transform: 'translateY(-50%)',
      padding: '3px 8px', borderRadius: 'var(--r-sm)',
      background: 'var(--bg-muted)', color: 'var(--text-muted)',
      fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 600,
      border: '1px solid var(--border)',
    }}>⌘ K</kbd>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── SAMPLE DATA (cross-game) ───────────────────────
// ═══════════════════════════════════════════════════════
const GAMES = [
  { id: 'g-gloomhaven', title: 'Gloomhaven',     emoji: '⚔️', grad: 'linear-gradient(135deg, hsl(0,35%,28%), hsl(20,30%,20%))' },
  { id: 'g-azul',       title: 'Azul',            emoji: '🔷', grad: 'linear-gradient(135deg, hsl(200,70%,55%), hsl(220,50%,30%))' },
  { id: 'g-wingspan',   title: 'Wingspan',        emoji: '🦜', grad: 'linear-gradient(135deg, hsl(85,45%,55%), hsl(105,25%,30%))' },
  { id: 'g-brass',      title: 'Brass: B.',       emoji: '🏭', grad: 'linear-gradient(135deg, hsl(220,40%,55%), hsl(240,20%,30%))' },
  { id: 'g-arknova',    title: 'Ark Nova',        emoji: '🦒', grad: 'linear-gradient(135deg, hsl(130,50%,55%), hsl(150,30%,30%))' },
  { id: 'g-7wonders',   title: '7 Wonders Duel',  emoji: '🏛️', grad: 'linear-gradient(135deg, hsl(40,60%,55%), hsl(20,40%,30%))' },
];

const KB_DOCS = [
  { id: 'd1',  title: 'Rulebook v2 EN',              game: 'g-gloomhaven', pages: 87,  size: '45 MB', date: '2 gg fa',  status: 'ready' },
  { id: 'd2',  title: 'Scenario Book vol.1',         game: 'g-gloomhaven', pages: 142, size: '62 MB', date: '5 gg fa',  status: 'ready' },
  { id: 'd3',  title: 'Forgotten Circles',           game: 'g-gloomhaven', pages: 48,  size: '28 MB', date: '5 min fa', status: 'indexing' },
  { id: 'd4',  title: 'azul-regole-ita.pdf',         game: 'g-azul',       pages: 12,  size: '2.4 MB',date: '14 gg fa', status: 'ready' },
  { id: 'd5',  title: 'azul-rules-en.pdf',           game: 'g-azul',       pages: 10,  size: '1.8 MB',date: '14 gg fa', status: 'ready' },
  { id: 'd6',  title: 'azul-faq.md',                 game: 'g-azul',       pages: 4,   size: '120 KB',date: '3 gg fa',  status: 'ready' },
  { id: 'd7',  title: 'wingspan-rules.pdf',          game: 'g-wingspan',   pages: 24,  size: '4.8 MB',date: '8 gg fa',  status: 'ready' },
  { id: 'd8',  title: 'wingspan-european-exp.pdf',   game: 'g-wingspan',   pages: 18,  size: '3.2 MB',date: '8 gg fa',  status: 'ready' },
  { id: 'd9',  title: 'brass-rulebook.pdf',          game: 'g-brass',      pages: 32,  size: '6.2 MB',date: '12 gg fa', status: 'indexing' },
  { id: 'd10', title: 'arknova-base.pdf',            game: 'g-arknova',    pages: 28,  size: '5.4 MB',date: '20 gg fa', status: 'ready' },
  { id: 'd11', title: '7wonders-duel.pdf',           game: 'g-7wonders',   pages: 14,  size: '2.8 MB',date: '30 gg fa', status: 'ready' },
  { id: 'd12', title: 'old-rulebook-v1.pdf',         game: 'g-gloomhaven', pages: 0,   size: '38 MB', date: '1 sett fa',status: 'failed' },
];

const SHORTCUTS = {
  recentQueries: [
    'scout class abilities',
    'come si calcola il bonus di riga in Azul?',
    'quanti cubi può posare un giocatore in Brass?',
  ],
  savedAnswers: [
    { q: 'Setup iniziale Wingspan 4p', game: 'g-wingspan', at: 'Ieri' },
    { q: 'Link industry obbligatorio?', game: 'g-brass', at: '3g fa' },
    { q: 'Forgotten Circles solo mode', game: 'g-gloomhaven', at: '5g fa' },
    { q: 'Punteggio finale 7 Wonders Duel', game: 'g-7wonders', at: '1s fa' },
    { q: 'Posa tessere centrali Azul', game: 'g-azul', at: '2s fa' },
  ],
  popularRules: [
    { label: 'Conquista tile centrale (Azul)', plays: 89 },
    { label: 'Movement di Spirit (Spirit Island)', plays: 64 },
    { label: 'Bird power "When played"', plays: 58 },
    { label: 'Loan rules (Brass)', plays: 42 },
    { label: 'Scout perk tree (Gloomhaven)', plays: 31 },
  ],
};

const SUGGESTED_AGENTS = [
  { id: 'a1', title: 'Gloomhaven Rules', game: 'g-gloomhaven', invocations: 342, latency: '1.2s', online: true },
  { id: 'a2', title: 'Azul Rules Expert', game: 'g-azul',       invocations: 187, latency: '0.9s', online: true },
  { id: 'a3', title: 'Wingspan Rules',    game: 'g-wingspan',   invocations: 230, latency: '1.1s', online: true },
  { id: 'a4', title: 'Game Night Host',   game: null,            invocations: 1203,latency: '2.1s', online: false },
];

const gameById = (id) => GAMES.find(g => g.id === id) || GAMES[0];

// ═══════════════════════════════════════════════════════
// ─── 1. KB HOME DESKTOP (globale) ───────────────────
// /* v2: KbGlobalHero / KbDocLibraryGrid → kb-global-hero/, kb-doc-library-grid/ */
// ═══════════════════════════════════════════════════════
const KbHomeDesktop = () => (
  <div style={{
    background: 'var(--bg-card)',
    border: `1px solid ${kb(0.2)}`,
    borderRadius: 'var(--r-xl)',
    padding: 28,
    boxShadow: `0 4px 20px ${kb(0.08)}`,
  }}>
    {/* Header / breadcrumb */}
    <div style={{
      display: 'flex', alignItems: 'center', gap: 12, marginBottom: 24,
      paddingBottom: 16, borderBottom: '1px solid var(--border)',
    }}>
      <span style={{
        width: 36, height: 36, borderRadius: 'var(--r-md)',
        background: kb(0.12), color: kb(),
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 18,
      }}>📚</span>
      <div style={{ flex: 1 }}>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
          textTransform: 'uppercase', letterSpacing: '.06em',
        }}>/kb · Knowledge Base globale</div>
        <h2 style={{
          fontFamily: 'var(--f-display)', fontWeight: 800,
          fontSize: 20, margin: '2px 0 0',
        }}>La tua libreria <span style={{ color: kb() }}>cross-game</span></h2>
      </div>
      <EntityChip type="kb" label="Knowledge Base" sm />
      <Btn variant="secondary" icon="📤" size="sm">Carica PDF</Btn>
    </div>

    {/* Hero search */}
    <div style={{ marginBottom: 28 }}>
      <HeroSearch focused />
      <div style={{
        marginTop: 10, display: 'flex', gap: 8, flexWrap: 'wrap',
        fontSize: 11, color: 'var(--text-muted)',
      }}>
        <span style={{ fontFamily: 'var(--f-mono)', fontWeight: 600 }}>Suggerimenti:</span>
        {['setup iniziale', 'punteggio finale', 'condizione di vittoria', 'simbolo dado'].map(s => (
          <Tag key={s} color="var(--text-sec)" bg="var(--bg-muted)" border="var(--border)">
            {s}
          </Tag>
        ))}
      </div>
    </div>

    {/* 3-column layout */}
    <div style={{
      display: 'grid',
      gridTemplateColumns: '260px 1fr 280px',
      gap: 20,
      alignItems: 'flex-start',
    }}>

      {/* ── Sx: Quick shortcuts ── */}
      <aside style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {/* Last 3 queries */}
        <div style={{
          background: 'var(--bg-muted)', borderRadius: 'var(--r-md)',
          padding: 14, border: '1px solid var(--border-light)',
        }}>
          <div style={{
            display: 'flex', alignItems: 'center', gap: 6, marginBottom: 10,
            fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 800,
            textTransform: 'uppercase', letterSpacing: '.06em', color: 'var(--text-sec)',
          }}>
            <span>🕐</span> Ultime query
          </div>
          {SHORTCUTS.recentQueries.map((q, i) => (
            <div key={i} style={{
              padding: '7px 10px', marginBottom: 6,
              borderRadius: 'var(--r-sm)', background: 'var(--bg-card)',
              border: '1px solid var(--border-light)',
              fontSize: 12, color: 'var(--text-sec)', cursor: 'pointer',
              whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
            }}>
              <span style={{ color: kb(), marginRight: 4 }}>↻</span>{q}
            </div>
          ))}
        </div>

        {/* Last 5 saved answers */}
        <div style={{
          background: 'var(--bg-muted)', borderRadius: 'var(--r-md)',
          padding: 14, border: '1px solid var(--border-light)',
        }}>
          <div style={{
            display: 'flex', alignItems: 'center', gap: 6, marginBottom: 10,
            fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 800,
            textTransform: 'uppercase', letterSpacing: '.06em', color: 'var(--text-sec)',
          }}>
            <span>⭐</span> Risposte salvate
          </div>
          {SHORTCUTS.savedAnswers.map((s, i) => {
            const g = gameById(s.game);
            return (
              <div key={i} style={{
                display: 'flex', alignItems: 'center', gap: 8,
                padding: '6px 4px', borderBottom: i < SHORTCUTS.savedAnswers.length - 1 ? '1px solid var(--border-light)' : 'none',
                cursor: 'pointer',
              }}>
                <span style={{
                  width: 22, height: 22, borderRadius: 'var(--r-xs)',
                  background: g.grad, display: 'flex', alignItems: 'center',
                  justifyContent: 'center', fontSize: 11, flexShrink: 0,
                }}>{g.emoji}</span>
                <div style={{ minWidth: 0, flex: 1 }}>
                  <div style={{
                    fontSize: 11.5, fontWeight: 600, color: 'var(--text)',
                    whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
                  }}>{s.q}</div>
                  <div style={{ fontSize: 10, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>{s.at}</div>
                </div>
              </div>
            );
          })}
        </div>

        {/* Top 5 popular rules */}
        <div style={{
          background: kb(0.06), borderRadius: 'var(--r-md)',
          padding: 14, border: `1px solid ${kb(0.18)}`,
        }}>
          <div style={{
            display: 'flex', alignItems: 'center', gap: 6, marginBottom: 10,
            fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 800,
            textTransform: 'uppercase', letterSpacing: '.06em', color: kb(),
          }}>
            <span>🔥</span> Regole popolari
          </div>
          {SHORTCUTS.popularRules.map((r, i) => (
            <div key={i} style={{
              display: 'flex', alignItems: 'center', justifyContent: 'space-between',
              padding: '6px 0', borderBottom: i < SHORTCUTS.popularRules.length - 1 ? `1px solid ${kb(0.1)}` : 'none',
            }}>
              <span style={{ fontSize: 11.5, color: 'var(--text-sec)', flex: 1, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {i + 1}. {r.label}
              </span>
              <span style={{
                fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
                color: kb(), background: kb(0.1),
                padding: '1px 6px', borderRadius: 'var(--r-pill)',
                marginLeft: 6, flexShrink: 0,
              }}>{r.plays}</span>
            </div>
          ))}
        </div>
      </aside>

      {/* ── Centro: documenti grid ── */}
      <section>
        <div style={{
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          marginBottom: 14,
        }}>
          <div>
            <h3 style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 16, margin: 0 }}>
              Documenti della tua libreria
            </h3>
            <div style={{ fontSize: 11.5, color: 'var(--text-muted)', marginTop: 3 }}>
              <strong style={{ color: kb() }}>{KB_DOCS.length}</strong> documenti · {GAMES.length} giochi · 1.247 chunks indicizzati
            </div>
          </div>
          <div style={{ display: 'flex', gap: 6 }}>
            {['🎮 Tutti', '📅 Recenti', '⭐ Preferiti'].map((l, i) => (
              <Tag key={i}
                color={i === 0 ? '#fff' : 'var(--text-sec)'}
                bg={i === 0 ? kb() : 'var(--bg-muted)'}
                border={i === 0 ? kb() : 'var(--border)'}>
                {l}
              </Tag>
            ))}
          </div>
        </div>

        <div style={{
          display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12,
        }}>
          {KB_DOCS.map(doc => {
            const g = gameById(doc.game);
            return (
              <div key={doc.id} style={{
                background: 'var(--bg-card)',
                border: '1px solid var(--border)',
                borderRadius: 'var(--r-md)',
                overflow: 'hidden',
                cursor: 'pointer',
                transition: 'all var(--dur-sm) var(--ease-out)',
                position: 'relative',
              }}>
                {/* Game cover thumbnail */}
                <div style={{
                  height: 64, background: g.grad,
                  display: 'flex', alignItems: 'flex-end', padding: 8,
                  position: 'relative',
                }}>
                  <span style={{
                    fontSize: 24, filter: 'drop-shadow(0 2px 4px rgba(0,0,0,.3))',
                  }}>{g.emoji}</span>
                  <span style={{
                    position: 'absolute', top: 6, right: 6,
                    padding: '2px 7px', borderRadius: 'var(--r-pill)',
                    background: 'rgba(0,0,0,.45)', color: '#fff',
                    fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 700,
                    backdropFilter: 'blur(4px)',
                  }}>{g.title}</span>
                  {doc.status !== 'ready' && (
                    <div style={{ position: 'absolute', bottom: 6, right: 6 }}>
                      <StatusBadge status={doc.status} />
                    </div>
                  )}
                </div>
                {/* Meta */}
                <div style={{ padding: '10px 12px' }}>
                  <div style={{
                    fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 12.5,
                    whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
                    marginBottom: 4,
                  }}>📄 {doc.title}</div>
                  <div style={{
                    display: 'flex', alignItems: 'center', gap: 6,
                    fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
                  }}>
                    <span>{doc.pages || '—'} pag</span>
                    <span style={{ opacity: .4 }}>·</span>
                    <span>{doc.size}</span>
                    <span style={{ opacity: .4 }}>·</span>
                    <span>{doc.date}</span>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </section>

      {/* ── Dx: Agent suggeriti ── */}
      <aside>
        <div style={{
          display: 'flex', alignItems: 'center', gap: 6, marginBottom: 12,
          fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 800,
          textTransform: 'uppercase', letterSpacing: '.06em', color: agentC(),
        }}>
          <span>🤖</span> Agent suggeriti
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {SUGGESTED_AGENTS.map(a => {
            const g = a.game ? gameById(a.game) : null;
            return (
              <div key={a.id} style={{
                background: 'var(--bg-card)',
                border: `1px solid ${agentC(0.18)}`,
                borderRadius: 'var(--r-md)',
                padding: 12,
                cursor: 'pointer',
                boxShadow: `0 2px 8px ${agentC(0.06)}`,
              }}>
                <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10, marginBottom: 8 }}>
                  <div style={{
                    width: 32, height: 32, borderRadius: 'var(--r-sm)',
                    background: g ? g.grad : agentC(0.15),
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    fontSize: 16, flexShrink: 0,
                  }}>{g ? g.emoji : '🤖'}</div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{
                      display: 'flex', alignItems: 'center', gap: 6,
                      fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 12.5,
                    }}>
                      {a.title}
                      <span style={{
                        width: 6, height: 6, borderRadius: '50%',
                        background: a.online ? okC() : 'var(--text-muted)',
                        boxShadow: a.online ? `0 0 0 2px ${okC(0.2)}` : 'none',
                      }}/>
                    </div>
                    <div style={{
                      fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
                      marginTop: 2,
                    }}>{a.invocations.toLocaleString('it-IT')} chiamate · {a.latency}</div>
                  </div>
                </div>
                <Btn variant="secondary" size="sm" fullWidth icon="💬">
                  Chiedi
                </Btn>
              </div>
            );
          })}
        </div>
      </aside>

    </div>
  </div>
);


// ═══════════════════════════════════════════════════════
// ─── 2. KB SEARCH RESULTS DESKTOP ───────────────────
// /* v2: KbSearchResultCard → kb-search-result-card/ */
// ═══════════════════════════════════════════════════════
const SEARCH_QUERY = 'scout class abilities';
const SEARCH_RESULTS = [
  {
    id: 'r1', confidence: 92, game: 'g-gloomhaven',
    title: 'Scout class — abilities tree',
    citation: 'Rulebook v2 EN · p.14',
    snippet: 'The Scout class begins with three signature abilities: <m>quick step</m>, evasive maneuvers and silent strike. Each ability has a base effect printed on the top half of the card and an enhanced effect on the bottom half, accessible by losing the card.',
    type: 'rule',
  },
  {
    id: 'r2', confidence: 88, game: 'g-gloomhaven',
    title: 'Perk tree progression for Scout',
    citation: 'Rulebook v2 EN · p.21',
    snippet: 'Scouts may select one perk per level-up. Recommended ordering: <m>+1 movement perk</m> first, then ranged damage and finally elemental affinity. Each perk modifies the attack modifier deck composition.',
    type: 'rule',
  },
  {
    id: 'r3', confidence: 81, game: 'g-gloomhaven',
    title: 'Movement-based abilities (Scout subclass)',
    citation: 'Forgotten Circles supplement · p.7',
    snippet: 'Forgotten Circles introduces three movement-augmenting cards specifically for <m>scout class abilities</m>: shadowstep, mistwalk, and parallel evade. Each enables movement through enemy hexes once per scenario.',
    type: 'clarification',
  },
  {
    id: 'r4', confidence: 74, game: 'g-gloomhaven',
    title: 'House rule — Scout solo balancing',
    citation: 'Community FAQ · #scout-solo',
    snippet: 'Popular community house rule for solo Scout play: +1 HP per level and access to one additional level-1 card. This addresses the perceived weakness of <m>scout class abilities</m> against bosses at low levels.',
    type: 'house-rule',
  },
  {
    id: 'r5', confidence: 68, game: 'g-azul',
    title: 'Scout-equivalent in Azul (off-topic match)',
    citation: 'azul-regole-ita.pdf · p.4',
    snippet: 'Non esiste un sistema di classe nelle regole base di Azul. Il match con "scout" è lessicale: la <m>posa anticipata</m> ricorda il pattern di scouting in altri giochi tattici, ma non sono <m>abilities</m> in senso tecnico.',
    type: 'clarification',
  },
];

const KbSearchResultsDesktop = () => {
  const Highlight = ({ children }) => {
    // Simple parser: replace <m>...</m> with span
    const parts = [];
    const re = /<m>(.+?)<\/m>/g;
    let last = 0, m;
    while ((m = re.exec(children)) !== null) {
      if (m.index > last) parts.push(children.slice(last, m.index));
      parts.push(
        <mark key={m.index} style={{
          background: kb(0.18), color: kb(),
          padding: '0 3px', borderRadius: 3,
          fontWeight: 700,
        }}>{m[1]}</mark>
      );
      last = m.index + m[0].length;
    }
    if (last < children.length) parts.push(children.slice(last));
    return <>{parts}</>;
  };

  return (
    <div style={{
      background: 'var(--bg-card)',
      border: `1px solid ${kb(0.2)}`,
      borderRadius: 'var(--r-xl)',
      overflow: 'hidden',
      boxShadow: `0 4px 20px ${kb(0.08)}`,
    }}>
      {/* Top bar: query echo */}
      <div style={{
        padding: '18px 24px 16px',
        borderBottom: `1px solid ${kb(0.15)}`,
        background: `linear-gradient(135deg, ${kb(0.06)}, transparent)`,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
          <span style={{
            padding: '4px 10px', borderRadius: 'var(--r-pill)',
            background: kb(0.12), color: kb(),
            fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
          }}>🔎 Query</span>
          <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>/kb · search</span>
        </div>
        <h2 style={{
          fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 22,
          margin: 0, color: 'var(--text)',
        }}>"{SEARCH_QUERY}"</h2>
        <div style={{
          marginTop: 6, display: 'flex', alignItems: 'center', gap: 14,
          fontFamily: 'var(--f-mono)', fontSize: 12, color: 'var(--text-sec)',
        }}>
          <span><strong style={{ color: kb() }}>12</strong> risultati</span>
          <span style={{ opacity: .4 }}>·</span>
          <span>0.8s</span>
          <span style={{ opacity: .4 }}>·</span>
          <span>3 giochi · hybrid retrieval (BM25 + dense)</span>
        </div>
      </div>

      {/* 2-col body */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: '1fr 320px',
        gap: 0,
        minHeight: 600,
      }}>
        {/* Main results — 70% */}
        <div style={{ padding: '20px 24px', borderRight: '1px solid var(--border)' }}>
          {SEARCH_RESULTS.map((r, i) => {
            const g = gameById(r.game);
            return (
              <div key={r.id} style={{
                background: 'var(--bg-card)',
                border: '1px solid var(--border)',
                borderRadius: 'var(--r-md)',
                padding: 16,
                marginBottom: 12,
                transition: 'all var(--dur-sm)',
              }}>
                {/* Header row */}
                <div style={{
                  display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between',
                  gap: 10, marginBottom: 10,
                }}>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{
                      display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4,
                      flexWrap: 'wrap',
                    }}>
                      <span style={{
                        fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
                        color: 'var(--text-muted)',
                      }}>#{i + 1}</span>
                      <Tag color={kb()} bg={kb(0.1)} border={kb(0.2)}>
                        {r.type === 'rule' ? '📋 rule' : r.type === 'clarification' ? '💡 clarification' : r.type === 'house-rule' ? '🏠 house-rule' : '👥 community-FAQ'}
                      </Tag>
                      <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>
                        📖 <strong style={{ color: 'var(--text-sec)' }}>{r.citation}</strong>
                      </span>
                    </div>
                    <h4 style={{
                      fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 15,
                      margin: 0, color: 'var(--text)', lineHeight: 1.3,
                    }}>{r.title}</h4>
                  </div>
                  {/* Confidence */}
                  <div style={{
                    display: 'flex', flexDirection: 'column', alignItems: 'flex-end',
                    flexShrink: 0,
                  }}>
                    <div style={{
                      padding: '3px 10px', borderRadius: 'var(--r-pill)',
                      background: r.confidence >= 85 ? okC(0.12) : r.confidence >= 70 ? warnC(0.12) : 'var(--bg-muted)',
                      color: r.confidence >= 85 ? okC() : r.confidence >= 70 ? warnC() : 'var(--text-muted)',
                      fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800,
                      border: `1px solid ${r.confidence >= 85 ? okC(0.25) : r.confidence >= 70 ? warnC(0.25) : 'var(--border)'}`,
                    }}>
                      {r.confidence}% match
                    </div>
                    <div style={{
                      width: 60, height: 4, marginTop: 4,
                      background: 'var(--bg-muted)', borderRadius: 2, overflow: 'hidden',
                    }}>
                      <div style={{
                        width: `${r.confidence}%`, height: '100%',
                        background: r.confidence >= 85 ? okC() : r.confidence >= 70 ? warnC() : 'var(--text-muted)',
                      }}/>
                    </div>
                  </div>
                </div>

                {/* Snippet with highlight */}
                <div style={{
                  background: 'var(--bg-muted)',
                  borderLeft: `3px solid ${kb()}`,
                  padding: '10px 14px',
                  borderRadius: 'var(--r-sm)',
                  fontSize: 13, color: 'var(--text-sec)',
                  lineHeight: 1.55,
                  marginBottom: 12,
                }}>
                  <Highlight>{r.snippet}</Highlight>
                </div>

                {/* Actions row */}
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                  <span style={{
                    width: 22, height: 22, borderRadius: 'var(--r-xs)',
                    background: g.grad,
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    fontSize: 11, marginRight: 4,
                  }}>{g.emoji}</span>
                  <span style={{
                    fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 600,
                    color: 'var(--text-muted)',
                    textTransform: 'uppercase', letterSpacing: '.05em',
                  }}>{g.title}</span>
                  <span style={{ flex: 1 }}/>
                  <Btn variant="secondary" size="sm" icon="🤖">Chiedi all'agente</Btn>
                  <Btn variant="ghost" size="sm" icon="⭐">Salva</Btn>
                  <Btn variant="ghost" size="sm" icon="↗">Condividi</Btn>
                </div>
              </div>
            );
          })}

          {/* Pagination hint */}
          <div style={{
            textAlign: 'center', padding: '16px 0',
            fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
          }}>
            mostrati 5 di 12 · <a href="#" style={{ color: kb(), fontWeight: 700 }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-kb-hub.html'; }, 0); /* DEMO-NAV */ }}>carica altri 7 →</a>
          </div>
        </div>

        {/* Filters sidebar — 30% */}
        <aside style={{
          padding: '20px 22px',
          background: 'var(--bg-muted)',
          borderTopRightRadius: 0,
        }}>
          <div style={{
            display: 'flex', alignItems: 'center', justifyContent: 'space-between',
            marginBottom: 14,
          }}>
            <div style={{
              fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800,
              color: 'var(--text)',
            }}>Filtri</div>
            <span style={{
              fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
              padding: '2px 8px', borderRadius: 'var(--r-pill)',
              background: 'var(--bg-card)', border: '1px solid var(--border)',
            }}>Gioco: 1 · Tipo: 1</span>
          </div>

          {/* Per gioco — checkbox */}
          <FilterAccordion title="Per gioco" defaultOpen count="3">
            {GAMES.slice(0, 4).map((g, i) => (
              <label key={g.id} style={{
                display: 'flex', alignItems: 'center', gap: 10,
                padding: '7px 4px', cursor: 'pointer',
                borderBottom: i < 3 ? '1px solid var(--border-light)' : 'none',
              }}>
                <input type="checkbox" defaultChecked={i === 0} readOnly style={{
                  width: 16, height: 16, accentColor: kb(),
                }}/>
                <span style={{ fontSize: 14, marginRight: 2 }}>{g.emoji}</span>
                <span style={{ fontSize: 13, color: 'var(--text-sec)', fontWeight: 600 }}>{g.title}</span>
                <span style={{ flex: 1 }}/>
                <span style={{
                  fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
                }}>{i === 0 ? 4 : i === 1 ? 5 : i === 2 ? 2 : 1}</span>
              </label>
            ))}
          </FilterAccordion>

          {/* Per tipo — radio */}
          <FilterAccordion title="Per tipo" defaultOpen count="4">
            {[
              { v: 'rule', label: '📋 Rule', count: 7, checked: true },
              { v: 'clarification', label: '💡 Clarification', count: 3 },
              { v: 'house-rule', label: '🏠 House-rule', count: 1 },
              { v: 'community-FAQ', label: '👥 Community-FAQ', count: 1 },
            ].map((t, i) => (
              <label key={t.v} style={{
                display: 'flex', alignItems: 'center', gap: 10,
                padding: '7px 4px', cursor: 'pointer',
                borderBottom: i < 3 ? '1px solid var(--border-light)' : 'none',
              }}>
                <input type="radio" name="type" defaultChecked={t.checked} readOnly style={{
                  width: 16, height: 16, accentColor: kb(),
                }}/>
                <span style={{ fontSize: 13, color: 'var(--text-sec)', fontWeight: 600 }}>{t.label}</span>
                <span style={{ flex: 1 }}/>
                <span style={{
                  fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
                }}>{t.count}</span>
              </label>
            ))}
          </FilterAccordion>

          {/* Per rating — stars */}
          <FilterAccordion title="Per rating">
            <div style={{ padding: '4px 0' }}>
              {[5, 4, 3, 2, 1].map(n => (
                <label key={n} style={{
                  display: 'flex', alignItems: 'center', gap: 8,
                  padding: '5px 4px', cursor: 'pointer',
                }}>
                  <input type="checkbox" defaultChecked={n >= 4} readOnly style={{
                    width: 14, height: 14, accentColor: kb(),
                  }}/>
                  <span style={{
                    color: warnC(), fontSize: 13, letterSpacing: 1,
                  }}>{'★'.repeat(n)}<span style={{ color: 'var(--text-muted)', opacity: .4 }}>{'★'.repeat(5 - n)}</span></span>
                  <span style={{ flex: 1 }}/>
                  <span style={{
                    fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
                  }}>{n === 5 ? 3 : n === 4 ? 5 : n === 3 ? 2 : 0}</span>
                </label>
              ))}
            </div>
          </FilterAccordion>

          {/* Per data range */}
          <FilterAccordion title="Per data">
            <div style={{ padding: '4px 0' }}>
              {['Ultimi 7 giorni', 'Ultimi 30 giorni', 'Ultimi 90 giorni', 'Custom range…'].map((l, i) => (
                <label key={l} style={{
                  display: 'flex', alignItems: 'center', gap: 10,
                  padding: '6px 4px', cursor: 'pointer',
                }}>
                  <input type="radio" name="date" defaultChecked={i === 1} readOnly style={{
                    width: 14, height: 14, accentColor: kb(),
                  }}/>
                  <span style={{ fontSize: 12.5, color: 'var(--text-sec)', fontWeight: 500 }}>{l}</span>
                </label>
              ))}
            </div>
          </FilterAccordion>

          {/* Reset */}
          <div style={{ marginTop: 14, paddingTop: 14, borderTop: '1px solid var(--border)' }}>
            <Btn variant="ghost" size="sm" fullWidth icon="↺">Reset filtri</Btn>
          </div>
        </aside>
      </div>
    </div>
  );
};

// Filter accordion (used in screen 2)
const FilterAccordion = ({ title, count, children, defaultOpen }) => {
  const [open, setOpen] = useState(defaultOpen || false);
  return (
    <div style={{
      borderBottom: '1px solid var(--border)',
      paddingBottom: open ? 10 : 0,
    }}>
      <button onClick={() => setOpen(!open)} style={{
        display: 'flex', alignItems: 'center', width: '100%',
        padding: '10px 0', background: 'transparent', border: 'none',
        cursor: 'pointer', textAlign: 'left',
      }}>
        <span style={{
          fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700,
          color: 'var(--text)', textTransform: 'uppercase', letterSpacing: '.05em',
        }}>{title}</span>
        {count && (
          <span style={{
            marginLeft: 8, fontFamily: 'var(--f-mono)', fontSize: 10,
            color: kb(), padding: '1px 6px', borderRadius: 'var(--r-pill)',
            background: kb(0.1),
          }}>{count}</span>
        )}
        <span style={{ flex: 1 }}/>
        <span style={{
          color: 'var(--text-muted)', fontSize: 11,
          transform: open ? 'rotate(180deg)' : 'rotate(0deg)',
          transition: 'transform var(--dur-sm)',
        }}>▾</span>
      </button>
      {open && <div>{children}</div>}
    </div>
  );
};


// ═══════════════════════════════════════════════════════
// ─── 3. DOCUMENT VIEWER DESKTOP (split-view) ────────
// /* v2: KbDocViewerSplit → kb-doc-viewer-split/ */
// ═══════════════════════════════════════════════════════
const VIEWER_DOC = { title: 'Rulebook v2 EN', game: 'g-gloomhaven', page: 14, totalPages: 87 };
const CITATIONS = [
  { id: 'c1', n: 1, page: 12, ref: 'p.12 §3.2', preview: 'Class card composition: signature abilities printed top half…', active: false },
  { id: 'c2', n: 2, page: 14, ref: 'p.14 §4.1', preview: 'Scout class — quick step, evasive maneuvers, silent strike…', active: true },
  { id: 'c3', n: 3, page: 21, ref: 'p.21 §5.6', preview: 'Perk tree progression: +1 movement first, then ranged damage…', active: false },
];

const KbDocViewerDesktop = () => {
  const g = gameById(VIEWER_DOC.game);
  return (
    <div style={{
      background: 'var(--bg-card)',
      border: `1px solid ${kb(0.2)}`,
      borderRadius: 'var(--r-xl)',
      overflow: 'hidden',
      boxShadow: `0 4px 20px ${kb(0.08)}`,
    }}>
      {/* Top toolbar (fixed) */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 12,
        padding: '12px 18px',
        borderBottom: `1px solid ${kb(0.15)}`,
        background: `linear-gradient(135deg, ${kb(0.06)}, transparent)`,
      }}>
        {/* Doc id */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0 }}>
          <span style={{
            width: 32, height: 32, borderRadius: 'var(--r-sm)',
            background: g.grad, display: 'flex', alignItems: 'center',
            justifyContent: 'center', fontSize: 16, flexShrink: 0,
          }}>{g.emoji}</span>
          <div style={{ minWidth: 0 }}>
            <div style={{
              fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
              whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
            }}>📄 {VIEWER_DOC.title}</div>
            <div style={{
              fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
            }}>{g.title} · pagina <strong style={{ color: kb() }}>{VIEWER_DOC.page}</strong> / {VIEWER_DOC.totalPages}</div>
          </div>
        </div>

        <span style={{ flex: 1 }}/>

        {/* Search in doc */}
        <div style={{
          position: 'relative', width: 220,
          background: 'var(--bg-muted)', borderRadius: 'var(--r-md)',
          border: '1px solid var(--border)',
        }}>
          <input placeholder="Cerca nel documento" readOnly style={{
            width: '100%', padding: '6px 10px 6px 30px',
            border: 'none', outline: 'none', background: 'transparent',
            fontFamily: 'var(--f-body)', fontSize: 12, color: 'var(--text)',
          }}/>
          <span style={{
            position: 'absolute', left: 9, top: '50%', transform: 'translateY(-50%)',
            fontSize: 12, color: 'var(--text-muted)',
          }}>🔎</span>
        </div>

        {/* Zoom controls */}
        <div style={{
          display: 'flex', alignItems: 'center',
          background: 'var(--bg-muted)', borderRadius: 'var(--r-md)',
          border: '1px solid var(--border)', overflow: 'hidden',
        }}>
          {['−', '100%', '+'].map((s, i) => (
            <button key={i} style={{
              padding: '6px 12px', background: i === 1 ? 'var(--bg-card)' : 'transparent',
              border: 'none', cursor: 'pointer',
              fontFamily: 'var(--f-mono)', fontSize: 12, fontWeight: 700,
              color: 'var(--text-sec)',
              borderRight: i < 2 ? '1px solid var(--border)' : 'none',
            }}>{s}</button>
          ))}
        </div>

        <Btn variant="ghost" size="sm" icon="⬇">Download</Btn>
        <Btn variant="ghost" size="sm" icon="↗">Share</Btn>
        <Btn variant="ghost" size="sm" icon="🖨">Print</Btn>
      </div>

      {/* 3-col body */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: '12% 55% 33%',
        minHeight: 680,
      }}>
        {/* Sx — page navigator thumbnails */}
        <aside style={{
          background: 'var(--bg-sunken)',
          borderRight: '1px solid var(--border)',
          padding: '14px 10px',
          overflowY: 'auto',
        }}>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
            textTransform: 'uppercase', letterSpacing: '.06em',
            color: 'var(--text-muted)', marginBottom: 10, textAlign: 'center',
          }}>Pagine</div>
          {Array.from({ length: 10 }, (_, i) => i + 10).map(pageN => {
            const isActive = pageN === VIEWER_DOC.page;
            return (
              <div key={pageN} style={{
                position: 'relative', marginBottom: 10, cursor: 'pointer',
              }}>
                <div style={{
                  aspectRatio: '0.71',
                  background: 'var(--bg-card)',
                  border: isActive ? `2px solid ${kb()}` : '1px solid var(--border)',
                  borderRadius: 'var(--r-xs)',
                  boxShadow: isActive ? `0 0 0 3px ${kb(0.18)}, var(--shadow-sm)` : 'var(--shadow-xs)',
                  overflow: 'hidden',
                  position: 'relative',
                }}>
                  {/* Fake text lines */}
                  <div style={{ padding: '8px 6px' }}>
                    <div style={{ height: 3, background: 'var(--text-muted)', opacity: .35, marginBottom: 3, width: '70%', borderRadius: 1 }}/>
                    {[5, 4, 6, 3, 5, 4].map((w, i) => (
                      <div key={i} style={{
                        height: 1.5, background: 'var(--text-muted)', opacity: .25,
                        marginBottom: 2, width: `${w * 12}%`, borderRadius: 1,
                      }}/>
                    ))}
                  </div>
                  {isActive && (
                    <div style={{
                      position: 'absolute', inset: 0,
                      background: kb(0.1),
                      display: 'flex', alignItems: 'center', justifyContent: 'center',
                    }}>
                      <span style={{
                        padding: '2px 6px', borderRadius: 'var(--r-xs)',
                        background: kb(), color: '#fff',
                        fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
                      }}>p.{pageN}</span>
                    </div>
                  )}
                </div>
                <div style={{
                  textAlign: 'center', fontFamily: 'var(--f-mono)',
                  fontSize: 9, fontWeight: 600,
                  color: isActive ? kb() : 'var(--text-muted)',
                  marginTop: 3,
                }}>{pageN}</div>
              </div>
            );
          })}
        </aside>

        {/* Centro — PDF renderer */}
        <div style={{
          background: 'var(--bg-sunken)',
          padding: '24px 24px',
          overflowY: 'auto',
          position: 'relative',
        }}>
          <div style={{
            background: '#fff',
            border: '1px solid var(--border)',
            borderRadius: 'var(--r-sm)',
            boxShadow: '0 4px 20px rgba(0,0,0,.12)',
            aspectRatio: '0.71',
            padding: '40px 50px',
            position: 'relative',
            color: '#2b1f12',
          }}>
            {/* Page header */}
            <div style={{
              fontFamily: 'var(--f-display)', fontSize: 10,
              color: '#999', textAlign: 'right', marginBottom: 20,
              borderBottom: '1px solid #eee', paddingBottom: 8,
            }}>Rulebook v2 EN · Chapter 4 — Classes · p.{VIEWER_DOC.page}</div>

            {/* Title */}
            <h3 style={{
              fontFamily: 'var(--f-display)', fontWeight: 800,
              fontSize: 22, margin: '0 0 14px', color: '#2b1f12',
            }}>4.1 — The Scout Class</h3>

            {/* Body */}
            <p style={{
              fontSize: 13, lineHeight: 1.65, color: '#3a2d1e', marginBottom: 14,
            }}>
              The Scout class is one of six starter classes available to players from the
              beginning of the campaign. Scouts excel at movement-based tactics, hit-and-run
              combat, and battlefield control through positioning.
            </p>

            {/* HIGHLIGHTED passage (the one citation #2 points to) */}
            <div style={{
              outline: `2px solid ${kb()}`,
              boxShadow: `0 0 12px ${kb(0.5)}`,
              borderRadius: 'var(--r-xs)',
              padding: '10px 12px',
              background: kb(0.06),
              marginBottom: 14,
              position: 'relative',
              animation: 'glowPulse 2s ease-out 0.2s 1',
            }}>
              <p style={{ fontSize: 13, lineHeight: 1.65, color: '#2b1f12', margin: 0 }}>
                The Scout class begins with three signature abilities:{' '}
                <strong>quick step</strong>, <strong>evasive maneuvers</strong> and{' '}
                <strong>silent strike</strong>. Each ability has a base effect printed on
                the top half of the card and an enhanced effect on the bottom half,
                accessible by losing the card.
              </p>
              {/* Citation pin */}
              <span style={{
                position: 'absolute', top: -10, left: -10,
                width: 22, height: 22, borderRadius: '50%',
                background: kb(), color: '#fff',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 800,
                boxShadow: `0 2px 8px ${kb(0.4)}`,
              }}>2</span>
            </div>

            <p style={{
              fontSize: 13, lineHeight: 1.65, color: '#3a2d1e', marginBottom: 14,
            }}>
              Players may choose to "lose" any ability card to access enhanced effects.
              Lost cards are placed in the lost pile and cannot be recovered until the
              end of the scenario, even through rest actions.
            </p>

            <h4 style={{
              fontFamily: 'var(--f-display)', fontWeight: 700,
              fontSize: 14, margin: '14px 0 8px', color: '#2b1f12',
            }}>4.1.1 — Starting hand size</h4>
            <p style={{ fontSize: 12, lineHeight: 1.6, color: '#5a4a38', margin: 0 }}>
              Scouts begin scenarios with a hand size of <strong>9 cards</strong>,
              the smallest among starter classes. This is offset by faster initiative
              values (avg. 18–24).
            </p>
          </div>
        </div>

        {/* Dx — Citations panel (sticky scroll) */}
        <aside style={{
          background: 'var(--bg-card)',
          borderLeft: `1px solid ${kb(0.15)}`,
          padding: '18px 18px',
          position: 'relative',
        }}>
          <div style={{
            display: 'flex', alignItems: 'center', gap: 8, marginBottom: 14,
            paddingBottom: 12, borderBottom: `1px solid ${kb(0.15)}`,
          }}>
            <span style={{
              width: 28, height: 28, borderRadius: 'var(--r-sm)',
              background: kb(0.12), color: kb(),
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontSize: 14,
            }}>📌</span>
            <div style={{ flex: 1 }}>
              <div style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 13 }}>
                Citations
              </div>
              <div style={{ fontSize: 10.5, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>
                {CITATIONS.length} pin · da chat corrente
              </div>
            </div>
            <Btn variant="ghost" size="sm">↻</Btn>
          </div>

          {/* Source chat reference */}
          <div style={{
            background: chatC(0.08), border: `1px solid ${chatC(0.2)}`,
            borderRadius: 'var(--r-md)', padding: '8px 12px', marginBottom: 14,
            display: 'flex', alignItems: 'center', gap: 8,
          }}>
            <span style={{ fontSize: 14 }}>💬</span>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontSize: 11.5, fontWeight: 700, color: chatC(), whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                "scout class abilities"
              </div>
              <div style={{ fontSize: 10, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>
                Gloomhaven Rules · 5 min fa
              </div>
            </div>
            <span style={{ color: chatC(), fontSize: 11 }}>↗</span>
          </div>

          {/* Citation pills */}
          {CITATIONS.map(c => (
            <div key={c.id} style={{
              padding: 12, marginBottom: 10,
              background: c.active ? kb(0.08) : 'var(--bg-muted)',
              border: c.active ? `2px solid ${kb()}` : '1px solid var(--border-light)',
              borderRadius: 'var(--r-md)',
              cursor: 'pointer',
              boxShadow: c.active ? `0 0 12px ${kb(0.18)}` : 'none',
              transition: 'all var(--dur-sm)',
            }}>
              <div style={{
                display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6,
              }}>
                <span style={{
                  width: 22, height: 22, borderRadius: '50%',
                  background: c.active ? kb() : kb(0.15),
                  color: c.active ? '#fff' : kb(),
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 800,
                  flexShrink: 0,
                }}>{c.n}</span>
                <span style={{
                  fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
                  color: c.active ? kb() : 'var(--text-sec)',
                }}>📖 {c.ref}</span>
                {c.active && (
                  <span style={{
                    marginLeft: 'auto',
                    padding: '1px 7px', borderRadius: 'var(--r-pill)',
                    background: kb(), color: '#fff',
                    fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
                    textTransform: 'uppercase', letterSpacing: '.05em',
                  }}>active</span>
                )}
              </div>
              <div style={{
                fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.45,
              }}>{c.preview}</div>
              {c.active && (
                <div style={{
                  marginTop: 8, paddingTop: 8, borderTop: `1px solid ${kb(0.15)}`,
                  fontFamily: 'var(--f-mono)', fontSize: 10, color: kb(), fontWeight: 700,
                  display: 'flex', alignItems: 'center', gap: 6,
                }}>
                  <span>✦</span> highlight + scroll → p.{c.page} (glow 2s)
                </div>
              )}
            </div>
          ))}

          {/* Add citation hint */}
          <button style={{
            marginTop: 6, width: '100%',
            padding: '10px', borderRadius: 'var(--r-md)',
            background: 'transparent', border: `1px dashed ${kb(0.3)}`,
            color: kb(), cursor: 'pointer',
            fontFamily: 'var(--f-display)', fontSize: 11.5, fontWeight: 700,
          }}>+ Aggiungi citation manuale</button>
        </aside>
      </div>
    </div>
  );
};


// ═══════════════════════════════════════════════════════
// ─── 4. DOCUMENT VIEWER MOBILE ──────────────────────
// /* v2: KbDocViewerMobile → kb-doc-viewer-mobile/ */
// ═══════════════════════════════════════════════════════
const KbDocViewerMobile = () => {
  const [sheetExpanded, setSheetExpanded] = useState(true);
  const [tab, setTab] = useState('citations');
  const g = gameById(VIEWER_DOC.game);

  return (
    <div style={{
      width: 380, height: 740, margin: '0 auto',
      borderRadius: 32, overflow: 'hidden', position: 'relative',
      background: 'var(--bg-sunken)',
      border: '10px solid #1a1410',
      boxShadow: '0 24px 60px rgba(0,0,0,.4), 0 0 0 1px rgba(255,255,255,.05) inset',
    }}>
      {/* Status bar */}
      <div style={{
        position: 'absolute', top: 0, left: 0, right: 0, height: 36,
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        padding: '0 24px', fontSize: 12, fontFamily: 'var(--f-display)',
        fontWeight: 700, color: 'var(--text)', zIndex: 20,
        background: 'var(--bg-card)',
      }}>
        <span>9:41</span>
        <span style={{ display: 'flex', gap: 4, alignItems: 'center', fontSize: 10 }}>
          <span>📶</span><span>📡</span><span>🔋</span>
        </span>
      </div>

      {/* Sticky header */}
      <div style={{
        position: 'absolute', top: 36, left: 0, right: 0,
        padding: '12px 16px',
        background: 'var(--bg-card)',
        borderBottom: `1px solid ${kb(0.15)}`,
        display: 'flex', alignItems: 'center', gap: 10,
        zIndex: 15,
      }}>
        <button style={{
          width: 32, height: 32, borderRadius: 'var(--r-pill)',
          background: 'var(--bg-muted)', border: 'none',
          fontSize: 16, color: 'var(--text-sec)',
        }}>✕</button>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13,
            whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
          }}>📄 {VIEWER_DOC.title}</div>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
          }}>{g.title}</div>
        </div>
        <div style={{
          padding: '4px 10px', borderRadius: 'var(--r-pill)',
          background: kb(0.12), color: kb(),
          fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 800,
        }}>{VIEWER_DOC.page} / {VIEWER_DOC.totalPages}</div>
      </div>

      {/* PDF full-screen */}
      <div style={{
        position: 'absolute', top: 96, left: 0, right: 0, bottom: 0,
        background: 'var(--bg-sunken)',
        padding: 16,
        overflowY: 'auto',
      }}>
        <div style={{
          background: '#fff', borderRadius: 'var(--r-sm)',
          boxShadow: '0 4px 16px rgba(0,0,0,.15)',
          padding: '24px 22px', color: '#2b1f12',
          aspectRatio: '0.71',
        }}>
          <div style={{
            fontFamily: 'var(--f-display)', fontSize: 8, color: '#999',
            textAlign: 'right', marginBottom: 12, paddingBottom: 6,
            borderBottom: '1px solid #eee',
          }}>Chapter 4 · p.{VIEWER_DOC.page}</div>
          <h4 style={{
            fontFamily: 'var(--f-display)', fontWeight: 800,
            fontSize: 14, margin: '0 0 8px',
          }}>4.1 — The Scout Class</h4>
          <p style={{ fontSize: 10.5, lineHeight: 1.55, color: '#3a2d1e', marginBottom: 8 }}>
            The Scout class is one of six starter classes available to players from the
            beginning of the campaign.
          </p>
          <div style={{
            outline: `1.5px solid ${kb()}`,
            boxShadow: `0 0 8px ${kb(0.45)}`,
            background: kb(0.07),
            padding: '6px 8px', borderRadius: 3, marginBottom: 8,
            position: 'relative',
          }}>
            <p style={{ fontSize: 10.5, lineHeight: 1.55, color: '#2b1f12', margin: 0 }}>
              Scouts begin with <strong>quick step</strong>, evasive maneuvers and silent
              strike. Each has base + enhanced effects.
            </p>
            <span style={{
              position: 'absolute', top: -6, left: -6,
              width: 16, height: 16, borderRadius: '50%',
              background: kb(), color: '#fff',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
            }}>2</span>
          </div>
          <p style={{ fontSize: 10.5, lineHeight: 1.55, color: '#3a2d1e' }}>
            Players may "lose" cards to access enhanced effects, placed in the lost pile
            until end of scenario.
          </p>
        </div>
      </div>

      {/* Bottom sheet */}
      <div style={{
        position: 'absolute', left: 0, right: 0, bottom: 0,
        background: 'var(--bg-card)',
        borderTopLeftRadius: 20, borderTopRightRadius: 20,
        boxShadow: 'var(--shadow-drawer)',
        transform: sheetExpanded ? 'translateY(0)' : `translateY(calc(100% - 64px))`,
        transition: 'transform var(--dur-md) var(--ease-out)',
        height: sheetExpanded ? '60%' : 64,
        zIndex: 25,
        borderTop: `1px solid ${kb(0.15)}`,
      }}>
        {/* Drag handle */}
        <div onClick={() => setSheetExpanded(!sheetExpanded)} style={{
          padding: '10px 0 6px', cursor: 'pointer',
          display: 'flex', justifyContent: 'center',
        }}>
          <div style={{
            width: 40, height: 4, borderRadius: 2,
            background: 'var(--text-muted)', opacity: .35,
          }}/>
        </div>

        {/* Peek header (always visible) */}
        <div style={{
          padding: '0 18px 12px',
          display: 'flex', alignItems: 'center', gap: 10,
        }}>
          <span style={{
            width: 28, height: 28, borderRadius: 'var(--r-sm)',
            background: kb(0.12), color: kb(),
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 13,
          }}>📌</span>
          <div style={{ flex: 1 }}>
            <div style={{ fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13 }}>
              Citations & condivisione
            </div>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)' }}>
              3 pin · trascina per espandere
            </div>
          </div>
          <span style={{
            color: kb(), fontSize: 14,
            transform: sheetExpanded ? 'rotate(180deg)' : 'rotate(0)',
            transition: 'transform var(--dur-sm)',
          }}>▲</span>
        </div>

        {sheetExpanded && (
          <>
            {/* Tab switcher */}
            <div style={{
              display: 'flex', gap: 4, padding: '0 18px', marginBottom: 12,
            }}>
              {[
                { id: 'citations', label: '📌 Citations', count: 3 },
                { id: 'share', label: '↗ Share', count: null },
              ].map(t => (
                <button key={t.id} onClick={() => setTab(t.id)} style={{
                  flex: 1, padding: '8px 12px',
                  background: tab === t.id ? kb(0.1) : 'transparent',
                  color: tab === t.id ? kb() : 'var(--text-muted)',
                  border: tab === t.id ? `1px solid ${kb(0.25)}` : '1px solid transparent',
                  borderRadius: 'var(--r-md)',
                  fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700,
                  cursor: 'pointer',
                  display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6,
                }}>
                  {t.label}
                  {t.count !== null && (
                    <span style={{
                      padding: '0 6px', borderRadius: 'var(--r-pill)',
                      background: tab === t.id ? kb() : 'var(--bg-muted)',
                      color: tab === t.id ? '#fff' : 'var(--text-muted)',
                      fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
                    }}>{t.count}</span>
                  )}
                </button>
              ))}
            </div>

            <div style={{ padding: '0 18px 16px', overflowY: 'auto', maxHeight: 280 }}>
              {tab === 'citations' && CITATIONS.map(c => (
                <div key={c.id} style={{
                  display: 'flex', gap: 10, padding: 10, marginBottom: 8,
                  background: c.active ? kb(0.08) : 'var(--bg-muted)',
                  border: c.active ? `1.5px solid ${kb()}` : '1px solid var(--border-light)',
                  borderRadius: 'var(--r-md)',
                }}>
                  <span style={{
                    width: 24, height: 24, borderRadius: '50%',
                    background: c.active ? kb() : kb(0.15),
                    color: c.active ? '#fff' : kb(),
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 800,
                    flexShrink: 0,
                  }}>{c.n}</span>
                  <div style={{ minWidth: 0, flex: 1 }}>
                    <div style={{
                      fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
                      color: c.active ? kb() : 'var(--text-sec)', marginBottom: 2,
                    }}>📖 {c.ref}</div>
                    <div style={{
                      fontSize: 11.5, color: 'var(--text-sec)', lineHeight: 1.4,
                      display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical',
                      overflow: 'hidden',
                    }}>{c.preview}</div>
                  </div>
                </div>
              ))}

              {tab === 'share' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                  {[
                    { icon: '✉️', label: 'Email', desc: 'Invia link al documento' },
                    { icon: '🔗', label: 'Copia link', desc: 'meeple.ai/d/gloom-rb-v2' },
                    { icon: '◧', label: 'QR Code', desc: 'Apri su altro dispositivo' },
                  ].map(s => (
                    <button key={s.label} style={{
                      display: 'flex', alignItems: 'center', gap: 12,
                      padding: '12px 14px',
                      background: 'var(--bg-muted)', border: '1px solid var(--border-light)',
                      borderRadius: 'var(--r-md)', cursor: 'pointer', textAlign: 'left',
                    }}>
                      <span style={{ fontSize: 20 }}>{s.icon}</span>
                      <div>
                        <div style={{ fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13 }}>{s.label}</div>
                        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)' }}>{s.desc}</div>
                      </div>
                    </button>
                  ))}
                </div>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
};


// ═══════════════════════════════════════════════════════
// ─── 5. KB EDITOR DESKTOP (curator) ─────────────────
// /* v2: KbEditorPanel → kb-editor-panel/ */
// /* Primary actor: curator (owner libreria private del game).
//    NON è admin globale — può editare solo doc dei game che possiede. */
// ═══════════════════════════════════════════════════════
const EDITOR_DOCS = [
  { id: 'e1', title: 'Rulebook v2 EN',         game: 'g-gloomhaven', size: '45 MB', chunks: 312, status: 'ready',    selected: false },
  { id: 'e2', title: 'Scenario Book vol.1',    game: 'g-gloomhaven', size: '62 MB', chunks: 487, status: 'ready',    selected: false },
  { id: 'e3', title: 'Forgotten Circles',      game: 'g-gloomhaven', size: '28 MB', chunks: 156, status: 'indexing', selected: false },
  { id: 'e4', title: 'Old rulebook v1',        game: 'g-gloomhaven', size: '38 MB', chunks: 0,   status: 'failed',   selected: true  },
  { id: 'e5', title: 'azul-regole-ita.pdf',    game: 'g-azul',       size: '2.4 MB',chunks: 47,  status: 'ready',    selected: false },
  { id: 'e6', title: 'wingspan-rules.pdf',     game: 'g-wingspan',   size: '4.8 MB',chunks: 89,  status: 'stale',    selected: false },
];

const KbEditorDesktop = () => {
  const selected = EDITOR_DOCS.find(d => d.selected) || EDITOR_DOCS[3];
  const g = gameById(selected.game);
  const [reindexing, setReindexing] = useState(false);
  const [tags, setTags] = useState(['rulebook', 'campaign', 'v1-legacy', 'failed-parse']);

  return (
    <div style={{
      background: 'var(--bg-card)',
      border: `1px solid ${kb(0.2)}`,
      borderRadius: 'var(--r-xl)',
      overflow: 'hidden',
      boxShadow: `0 4px 20px ${kb(0.08)}`,
    }}>
      {/* Top banner — appears when re-indexing */}
      {reindexing && (
        <div style={{
          padding: '10px 18px',
          background: warnC(0.12),
          borderBottom: `1px solid ${warnC(0.3)}`,
          display: 'flex', alignItems: 'center', gap: 10,
        }}>
          <div style={{
            width: 14, height: 14, borderRadius: '50%',
            border: `2px solid ${warnC(0.25)}`,
            borderTop: `2px solid ${warnC()}`,
            animation: 'spin 1s linear infinite',
          }}/>
          <span style={{ fontSize: 12.5, fontWeight: 700, color: warnC() }}>
            Re-indexing in background
          </span>
          <span style={{ fontSize: 11, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>
            "Old rulebook v1" · job_xk92m3p · ~3 min rimanenti
          </span>
          <span style={{ flex: 1 }}/>
          <Btn variant="ghost" size="sm" onClick={() => setReindexing(false)}>✕ Cancel re-index</Btn>
        </div>
      )}

      {/* Editor header */}
      <div style={{
        padding: '14px 22px',
        borderBottom: `1px solid ${kb(0.15)}`,
        background: `linear-gradient(135deg, ${kb(0.06)}, transparent)`,
        display: 'flex', alignItems: 'center', gap: 12,
      }}>
        <span style={{
          width: 32, height: 32, borderRadius: 'var(--r-md)',
          background: kb(0.12), color: kb(),
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 16,
        }}>✏️</span>
        <div style={{ flex: 1 }}>
          <h3 style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 17, margin: 0 }}>
            KB Editor
          </h3>
          <div style={{ fontSize: 11, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>
            /kb/editor · curator mode · 6 documenti gestiti
          </div>
        </div>
        <Tag color={agentC()} bg={agentC(0.12)} border={agentC(0.25)}>
          👤 curator · Marco R.
        </Tag>
      </div>

      {/* Split 2-col */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: '30% 70%',
        minHeight: 640,
      }}>
        {/* Sx — doc list (30%) */}
        <aside style={{
          background: 'var(--bg-sunken)',
          borderRight: '1px solid var(--border)',
        }}>
          <div style={{
            padding: '12px 16px',
            display: 'flex', alignItems: 'center', justifyContent: 'space-between',
            borderBottom: '1px solid var(--border)',
          }}>
            <span style={{
              fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800,
              textTransform: 'uppercase', letterSpacing: '.06em',
              color: 'var(--text-sec)',
            }}>Documenti</span>
            <Btn variant="secondary" size="sm" icon="+">Nuovo</Btn>
          </div>
          <div>
            {EDITOR_DOCS.map(d => {
              const gg = gameById(d.game);
              const isSel = d.id === selected.id;
              return (
                <div key={d.id} style={{
                  padding: '12px 16px',
                  background: isSel ? kb(0.08) : 'transparent',
                  borderLeft: isSel ? `3px solid ${kb()}` : '3px solid transparent',
                  borderBottom: '1px solid var(--border-light)',
                  cursor: 'pointer',
                }}>
                  <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
                    <span style={{
                      width: 28, height: 28, borderRadius: 'var(--r-sm)',
                      background: gg.grad,
                      display: 'flex', alignItems: 'center', justifyContent: 'center',
                      fontSize: 13, flexShrink: 0,
                    }}>{gg.emoji}</span>
                    <div style={{ minWidth: 0, flex: 1 }}>
                      <div style={{
                        fontFamily: 'var(--f-display)', fontWeight: isSel ? 700 : 600,
                        fontSize: 12.5, color: isSel ? kb() : 'var(--text)',
                        whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
                        marginBottom: 4,
                      }}>📄 {d.title}</div>
                      <div style={{
                        display: 'flex', alignItems: 'center', gap: 6,
                        marginBottom: 6,
                      }}>
                        <StatusBadge status={d.status}/>
                      </div>
                      <div style={{
                        fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
                      }}>{d.size} · {d.chunks || '—'} chunks</div>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </aside>

        {/* Dx — metadata editor (70%) */}
        <div style={{ padding: '20px 24px', overflowY: 'auto' }}>
          {/* Doc header */}
          <div style={{
            display: 'flex', alignItems: 'flex-start', gap: 14,
            paddingBottom: 16, marginBottom: 20,
            borderBottom: '1px solid var(--border)',
          }}>
            <span style={{
              width: 56, height: 56, borderRadius: 'var(--r-md)',
              background: g.grad,
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontSize: 28, flexShrink: 0,
              boxShadow: 'var(--shadow-sm)',
            }}>{g.emoji}</span>
            <div style={{ flex: 1 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                <h3 style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 18, margin: 0 }}>
                  📄 {selected.title}
                </h3>
                <StatusBadge status={selected.status} />
              </div>
              <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)' }}>
                {selected.size} · {selected.chunks || 0} chunks · SHA256 <code style={{ color: kb() }}>a1f2…7c8e</code>
              </div>
            </div>
          </div>

          {/* Fields grid */}
          <div style={{
            display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16,
            marginBottom: 20,
          }}>
            <Field label="Titolo">
              <input value={selected.title} readOnly style={fieldInputStyle}/>
            </Field>
            <Field label="Gioco associato">
              <div style={{ ...fieldInputStyle, display: 'flex', alignItems: 'center', gap: 8 }}>
                <span style={{
                  width: 20, height: 20, borderRadius: 'var(--r-xs)',
                  background: g.grad,
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  fontSize: 11,
                }}>{g.emoji}</span>
                <span style={{ flex: 1, fontWeight: 600 }}>{g.title}</span>
                <span style={{ color: 'var(--text-muted)', fontSize: 11 }}>▾</span>
              </div>
            </Field>

            <Field label="Tags" full>
              <div style={{
                ...fieldInputStyle,
                display: 'flex', flexWrap: 'wrap', gap: 6, padding: '8px 10px',
                minHeight: 38, alignItems: 'center',
              }}>
                {tags.map(t => (
                  <Tag key={t} x>{t}</Tag>
                ))}
                <input placeholder="+ aggiungi tag…" style={{
                  flex: 1, minWidth: 100,
                  border: 'none', outline: 'none', background: 'transparent',
                  fontFamily: 'var(--f-body)', fontSize: 12,
                }}/>
              </div>
            </Field>

            <Field label="Visibilità" full>
              <div style={{ display: 'flex', gap: 8 }}>
                {[
                  { v: 'private', label: '🔒 Private', desc: 'Solo tu' },
                  { v: 'team', label: '👥 Team', desc: 'Membri team' },
                  { v: 'public', label: '🌐 Public', desc: 'Tutti gli utenti', checked: true },
                ].map(opt => (
                  <label key={opt.v} style={{
                    flex: 1, padding: '10px 12px',
                    borderRadius: 'var(--r-md)',
                    background: opt.checked ? kb(0.08) : 'var(--bg-muted)',
                    border: opt.checked ? `2px solid ${kb()}` : '1px solid var(--border)',
                    cursor: 'pointer',
                    display: 'flex', alignItems: 'center', gap: 8,
                  }}>
                    <input type="radio" name="vis" defaultChecked={opt.checked} readOnly style={{
                      accentColor: kb(),
                    }}/>
                    <div>
                      <div style={{ fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 12, color: opt.checked ? kb() : 'var(--text)' }}>
                        {opt.label}
                      </div>
                      <div style={{ fontSize: 10.5, color: 'var(--text-muted)' }}>{opt.desc}</div>
                    </div>
                  </label>
                ))}
              </div>
            </Field>
          </div>

          {/* Chunking preview */}
          <Section title="Chunking preview" badge="3 chunks visibili">
            {[
              {
                idx: 0, range: '0–512 char',
                preview: 'GLOOMHAVEN — RULEBOOK v1\nWelcome to Gloomhaven. This is the original 2017 first-edition rulebook…\n\nSetup: Place the map on the table. Each player chooses a starting class from the six available.',
                tokens: 142,
              },
              {
                idx: 1, range: '512–1024 char',
                preview: 'CHAPTER 1 — SCENARIOS\nA scenario is the basic unit of play. Each scenario has setup conditions, victory conditions, and a series of rounds. Rounds consist of card selection, initiative ordering, and action resolution.',
                tokens: 168, error: 'PARSE_WARN: malformed table extracted',
              },
              {
                idx: 2, range: '1024–1536 char',
                preview: '⚠️ EXTRACTION FAILED for this chunk\n— PDF text layer corrupted around p.7–9\n— Fallback OCR not yet run\n— Recommend re-index with OCR enabled',
                tokens: 0, error: 'EXTRACTION_FAILED',
              },
            ].map(c => (
              <div key={c.idx} style={{
                background: c.error ? dangerC(0.04) : 'var(--bg-muted)',
                border: c.error ? `1px solid ${dangerC(0.3)}` : '1px solid var(--border-light)',
                borderRadius: 'var(--r-md)',
                padding: '10px 14px', marginBottom: 8,
                position: 'relative',
              }}>
                <div style={{
                  display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6,
                }}>
                  <span style={{
                    padding: '1px 8px', borderRadius: 'var(--r-pill)',
                    background: c.error ? dangerC(0.15) : kb(0.12),
                    color: c.error ? dangerC() : kb(),
                    fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800,
                  }}>chunk #{c.idx}</span>
                  <span style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)' }}>{c.range}</span>
                  <span style={{ flex: 1 }}/>
                  <span style={{
                    fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 600,
                    color: c.error ? dangerC() : 'var(--text-muted)',
                  }}>{c.tokens} tokens</span>
                </div>
                <pre style={{
                  fontFamily: 'var(--f-mono)', fontSize: 11,
                  color: c.error ? dangerC() : 'var(--text-sec)',
                  margin: 0, whiteSpace: 'pre-wrap', lineHeight: 1.5,
                }}>{c.preview}</pre>
                {c.error && (
                  <div style={{
                    marginTop: 8, paddingTop: 8, borderTop: `1px dashed ${dangerC(0.3)}`,
                    display: 'flex', alignItems: 'center', gap: 6,
                    fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700, color: dangerC(),
                  }}>
                    <span>⚠</span> {c.error}
                  </div>
                )}
              </div>
            ))}
          </Section>

          {/* Test query inline */}
          <Section title="Test query inline" badge="dry-run · no charge">
            <div style={{ display: 'flex', gap: 10, marginBottom: 12 }}>
              <textarea
                defaultValue="Quali sono le abilità iniziali della classe Scout?"
                style={{
                  flex: 1, padding: '10px 12px',
                  borderRadius: 'var(--r-md)',
                  background: 'var(--bg-card)',
                  border: '1px solid var(--border)',
                  fontFamily: 'var(--f-body)', fontSize: 12, color: 'var(--text)',
                  resize: 'vertical', minHeight: 56,
                }}
              />
              <Btn variant="primary" icon="🧪">Test</Btn>
            </div>
            <div style={{
              background: kb(0.06),
              border: `1px solid ${kb(0.2)}`,
              borderRadius: 'var(--r-md)',
              padding: '12px 14px',
            }}>
              <div style={{
                display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8,
                fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800,
                color: kb(), textTransform: 'uppercase', letterSpacing: '.05em',
              }}>
                <span>📊</span>
                Risultato retrieval
                <span style={{ marginLeft: 'auto', color: 'var(--text-muted)' }}>0.42s</span>
              </div>
              <div style={{
                fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.55, marginBottom: 8,
              }}>
                <strong style={{ color: dangerC() }}>⚠ Nessun chunk recuperato</strong> — questo
                documento è in stato <code style={{ background: dangerC(0.12), color: dangerC(), padding: '1px 5px', borderRadius: 3 }}>failed</code>.
                I chunk non sono stati indicizzati. Esegui <strong>Re-index</strong> per popolarli.
              </div>
              <div style={{
                fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
              }}>top-k=5 · threshold=0.65 · hybrid=BM25+dense</div>
            </div>
          </Section>

          {/* Footer actions */}
          <div style={{
            marginTop: 24, paddingTop: 16,
            borderTop: '1px solid var(--border)',
            display: 'flex', alignItems: 'center', gap: 10,
          }}>
            <Btn variant="danger-ghost" icon="⟳" onClick={() => setReindexing(true)} disabled={reindexing}>
              {reindexing ? 'Cancel re-index' : 'Re-index'}
            </Btn>
            <span style={{ flex: 1 }}/>
            <Btn variant="ghost">Annulla modifiche</Btn>
            <Btn variant="primary" icon="✓">Salva</Btn>
          </div>
        </div>
      </div>
    </div>
  );
};

// Editor helpers
const fieldInputStyle = {
  width: '100%', padding: '8px 12px',
  borderRadius: 'var(--r-md)',
  background: 'var(--bg-card)',
  border: '1px solid var(--border)',
  fontFamily: 'var(--f-body)', fontSize: 12.5,
  color: 'var(--text)',
};
const Field = ({ label, children, full }) => (
  <div style={{ gridColumn: full ? 'span 2' : undefined }}>
    <label style={{
      display: 'block', marginBottom: 6,
      fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 800,
      textTransform: 'uppercase', letterSpacing: '.06em',
      color: 'var(--text-sec)',
    }}>{label}</label>
    {children}
  </div>
);
const Section = ({ title, badge, children }) => (
  <div style={{ marginBottom: 20 }}>
    <div style={{
      display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10,
    }}>
      <h4 style={{
        fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 13,
        margin: 0, color: 'var(--text)',
      }}>{title}</h4>
      {badge && (
        <Tag color="var(--text-muted)" bg="var(--bg-muted)" border="var(--border)">{badge}</Tag>
      )}
    </div>
    {children}
  </div>
);


// ═══════════════════════════════════════════════════════
// ─── 6. ASK THE AGENT DRAWER (SSE streaming) ────────
// /* v2: AskAgentDrawer → ask-agent-drawer/ */
// /* Playwright handles: data-testid="sse-state-{idle|streaming|completed|error-...}" */
// ═══════════════════════════════════════════════════════
const DRAWER_W = 380;

// Shared drawer shell
const DrawerShell = ({ children, testId, label }) => (
  <div style={{
    position: 'relative',
    paddingTop: 14, // room for the floating state pill
    width: DRAWER_W,
  }}>
    {/* State pill (floats above, indicates which sub-state) */}
    <span style={{
      position: 'absolute', top: 0, left: 14, zIndex: 5,
      padding: '3px 10px', borderRadius: 'var(--r-pill)',
      background: kb(), color: '#fff',
      fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800,
      textTransform: 'uppercase', letterSpacing: '.05em',
      boxShadow: `0 2px 8px ${kb(0.35)}`,
    }}>{label}</span>

    <div data-testid={testId} style={{
      width: '100%', height: 620,
      background: 'var(--bg-card)',
      borderRadius: 'var(--r-xl)',
      border: `1px solid ${kb(0.22)}`,
      boxShadow: `var(--shadow-lg), 0 0 0 1px ${kb(0.08)}`,
      overflow: 'hidden',
      display: 'flex', flexDirection: 'column',
    }}>

    {/* Drawer header */}
    <div style={{
      padding: '14px 18px',
      borderBottom: `1px solid ${kb(0.15)}`,
      background: `linear-gradient(135deg, ${kb(0.08)}, transparent)`,
      display: 'flex', alignItems: 'center', gap: 10,
    }}>
      <span style={{
        width: 30, height: 30, borderRadius: 'var(--r-sm)',
        background: agentC(0.15), color: agentC(),
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 15,
      }}>🤖</span>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 13,
        }}>Ask the Meeple</div>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
        }}>Gloomhaven Rules · GPT-4o-mini</div>
      </div>
      <button style={{
        width: 26, height: 26, borderRadius: 'var(--r-pill)',
        background: 'var(--bg-muted)', border: 'none',
        color: 'var(--text-muted)', cursor: 'pointer', fontSize: 13,
      }}>✕</button>
    </div>

    {children}
    </div>
  </div>
);

// 6a — IDLE
const DrawerIdle = () => (
  <DrawerShell testId="sse-state-idle" label="6a · idle">
    <div style={{
      flex: 1, padding: 18,
      display: 'flex', flexDirection: 'column', justifyContent: 'space-between',
    }}>
      <div>
        {/* Welcome */}
        <div style={{
          padding: '20px 16px', borderRadius: 'var(--r-md)',
          background: kb(0.06), border: `1px solid ${kb(0.18)}`,
          textAlign: 'center', marginBottom: 16,
        }}>
          <div style={{ fontSize: 32, marginBottom: 8 }}>👋</div>
          <div style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 14, marginBottom: 4 }}>
            Chiedimi qualsiasi cosa sul tuo gioco
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.5 }}>
            Cerco nei tuoi PDF e cito le pagine esatte.
          </div>
        </div>

        {/* Suggested questions */}
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800,
          textTransform: 'uppercase', letterSpacing: '.06em',
          color: 'var(--text-muted)', marginBottom: 10,
        }}>Suggerimenti</div>
        {[
          'Come funzionano le abilità della classe Scout?',
          'Setup iniziale per 4 giocatori — Gloomhaven',
          'Differenza tra base e enhanced effect?',
        ].map(q => (
          <button key={q} style={{
            display: 'flex', alignItems: 'center', gap: 10, width: '100%',
            padding: '10px 12px', marginBottom: 8,
            borderRadius: 'var(--r-md)',
            background: 'var(--bg-muted)', border: '1px solid var(--border-light)',
            cursor: 'pointer', textAlign: 'left',
            fontFamily: 'var(--f-body)', fontSize: 12, color: 'var(--text-sec)',
            lineHeight: 1.4,
          }}>
            <span style={{ color: kb(), flexShrink: 0 }}>↪</span>
            <span style={{ flex: 1 }}>{q}</span>
            <span style={{ color: kb(), opacity: .5 }}>→</span>
          </button>
        ))}
      </div>

      {/* Input */}
      <div style={{
        background: 'var(--bg-muted)', borderRadius: 'var(--r-md)',
        border: `1px solid ${kb(0.2)}`, padding: '10px 12px',
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <input placeholder="Chiedi al Meeple…" readOnly style={{
          flex: 1, border: 'none', outline: 'none', background: 'transparent',
          fontFamily: 'var(--f-body)', fontSize: 13, color: 'var(--text)',
        }}/>
        <button style={{
          width: 30, height: 30, borderRadius: 'var(--r-sm)',
          background: kb(), color: '#fff', border: 'none',
          fontSize: 14, cursor: 'pointer',
        }}>↑</button>
      </div>
    </div>
  </DrawerShell>
);

// 6b — STREAMING (with 3-dot typing indicator + partial answer + inline citations)
const DrawerStreaming = () => {
  const [dot, setDot] = useState(0);
  useEffect(() => {
    const i = setInterval(() => setDot(d => (d + 1) % 3), 200);
    return () => clearInterval(i);
  }, []);
  return (
    <DrawerShell testId="sse-state-streaming" label="6b · streaming">
      <div style={{
        flex: 1, padding: 18, overflowY: 'auto',
        display: 'flex', flexDirection: 'column', gap: 12,
      }}>
        {/* User msg */}
        <div style={{
          alignSelf: 'flex-end', maxWidth: '80%',
          padding: '10px 14px',
          background: chatC(0.12), color: chatC(),
          border: `1px solid ${chatC(0.22)}`,
          borderRadius: 'var(--r-lg)',
          borderBottomRightRadius: 4,
          fontSize: 13, fontWeight: 600,
        }}>scout class abilities</div>

        {/* Agent partial reply */}
        <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
          <span style={{
            width: 26, height: 26, borderRadius: 'var(--r-sm)',
            background: agentC(0.15), color: agentC(),
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 13, flexShrink: 0,
          }}>🤖</span>
          <div style={{
            background: 'var(--bg-muted)',
            border: '1px solid var(--border-light)',
            borderRadius: 'var(--r-lg)',
            borderBottomLeftRadius: 4,
            padding: '11px 14px',
            fontSize: 13, lineHeight: 1.55, color: 'var(--text-sec)',
            flex: 1,
            animation: 'chunkAppend 600ms cubic-bezier(0.4, 0, 0.2, 1)',
          }}>
            La classe Scout in Gloomhaven inizia con tre abilità signature:{' '}
            <strong style={{ color: 'var(--text)' }}>quick step</strong>,{' '}
            <strong style={{ color: 'var(--text)' }}>evasive maneuvers</strong> e{' '}
            <strong style={{ color: 'var(--text)' }}>silent strike</strong>.{' '}
            <CitationPill n={1} ref_="p.14"/>{' '}
            Ciascuna ha un effetto base sulla parte superiore della carta e
            un effetto enhanced sulla parte inferiore, accessibile{' '}
            <CitationPill n={2} ref_="p.14 §4.1"/>
            <span style={{
              display: 'inline-flex', gap: 3, marginLeft: 6, verticalAlign: 'middle',
            }}>
              {[0, 1, 2].map(i => (
                <span key={i} style={{
                  width: 5, height: 5, borderRadius: '50%',
                  background: dot === i ? kb() : kb(0.3),
                  transition: 'background 200ms',
                }}/>
              ))}
            </span>
          </div>
        </div>

        {/* Stream meta */}
        <div style={{
          display: 'flex', alignItems: 'center', gap: 10, padding: '6px 8px',
          fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
        }}>
          <span style={{
            display: 'inline-flex', alignItems: 'center', gap: 5,
            color: kb(), fontWeight: 700,
          }}>
            <span style={{
              width: 6, height: 6, borderRadius: '50%',
              background: kb(),
              animation: 'pulse 1.4s ease-in-out infinite',
            }}/>
            STREAMING
          </span>
          <span style={{ opacity: .5 }}>·</span>
          <span>247 / ~420 tokens</span>
          <span style={{ opacity: .5 }}>·</span>
          <span>0.8s · 2 citations</span>
        </div>
      </div>

      {/* Footer: stop streaming */}
      <div style={{
        padding: 14, borderTop: '1px solid var(--border)',
        background: 'var(--bg-card)',
      }}>
        <Btn variant="secondary" size="md" fullWidth icon="◼">Stop streaming</Btn>
      </div>
    </DrawerShell>
  );
};

const CitationPill = ({ n, ref_ }) => (
  <span style={{
    display: 'inline-flex', alignItems: 'center', gap: 3,
    padding: '1px 7px 1px 4px', borderRadius: 'var(--r-pill)',
    background: kb(0.12), color: kb(),
    border: `1px solid ${kb(0.25)}`,
    fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
    cursor: 'pointer', verticalAlign: 'baseline',
    animation: 'chunkAppend 400ms cubic-bezier(0.4, 0, 0.2, 1)',
  }}>
    <span style={{
      width: 14, height: 14, borderRadius: '50%',
      background: kb(), color: '#fff',
      display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
      fontSize: 9, fontWeight: 800,
    }}>{n}</span>
    {ref_}
  </span>
);

// 6c — COMPLETED
const DrawerCompleted = () => (
  <DrawerShell testId="sse-state-completed" label="6c · completed">
    <div style={{
      flex: 1, padding: 18, overflowY: 'auto',
      display: 'flex', flexDirection: 'column', gap: 12,
    }}>
      <div style={{
        alignSelf: 'flex-end', maxWidth: '80%',
        padding: '10px 14px',
        background: chatC(0.12), color: chatC(),
        border: `1px solid ${chatC(0.22)}`,
        borderRadius: 'var(--r-lg)', borderBottomRightRadius: 4,
        fontSize: 13, fontWeight: 600,
      }}>scout class abilities</div>

      <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
        <span style={{
          width: 26, height: 26, borderRadius: 'var(--r-sm)',
          background: agentC(0.15), color: agentC(),
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 13, flexShrink: 0,
        }}>🤖</span>
        <div style={{
          background: 'var(--bg-muted)',
          border: '1px solid var(--border-light)',
          borderRadius: 'var(--r-lg)', borderBottomLeftRadius: 4,
          padding: '11px 14px',
          fontSize: 12.5, lineHeight: 1.6, color: 'var(--text-sec)',
          flex: 1,
        }}>
          La classe Scout inizia con tre abilità signature:{' '}
          <strong style={{ color: 'var(--text)' }}>quick step</strong>,{' '}
          <strong style={{ color: 'var(--text)' }}>evasive maneuvers</strong> e{' '}
          <strong style={{ color: 'var(--text)' }}>silent strike</strong>.{' '}
          <CitationPill n={1} ref_="p.14"/> Ciascuna ha un effetto base e uno
          enhanced. <CitationPill n={2} ref_="p.14 §4.1"/>{' '}
          La progressione perk parte da +1 movement <CitationPill n={3} ref_="p.21"/>,
          poi ranged damage <CitationPill n={4} ref_="p.21 §5.6"/> e infine
          elemental affinity. <CitationPill n={5} ref_="p.22"/>
        </div>
      </div>

      {/* Completion meta */}
      <div style={{
        padding: '8px 10px', borderRadius: 'var(--r-sm)',
        background: okC(0.08), border: `1px solid ${okC(0.2)}`,
        display: 'flex', alignItems: 'center', gap: 8,
        fontFamily: 'var(--f-mono)', fontSize: 10, color: okC(),
      }}>
        <span>✓</span> COMPLETED · 412 tokens · 2.1s · 5 citations
      </div>
    </div>

    {/* Actions */}
    <div style={{
      padding: 12, borderTop: '1px solid var(--border)',
      background: 'var(--bg-card)',
      display: 'flex', gap: 6,
    }}>
      <Btn variant="ghost" size="sm" icon="📋">Copy</Btn>
      <Btn variant="ghost" size="sm" icon="↻">Regenerate</Btn>
      <Btn variant="ghost" size="sm" icon="↗">Share</Btn>
      <span style={{ flex: 1 }}/>
      <Btn variant="secondary" size="sm" icon="⭐">Salva</Btn>
    </div>
  </DrawerShell>
);

// 6d — ERROR STATES (3 mini-varianti compatte)
const DrawerError = ({ kind }) => {
  const cfg = {
    connection: {
      testId: 'sse-state-error-connection',
      label: '6d-i · connection',
      icon: '📡',
      title: 'Connessione persa',
      desc: 'Retry automatico in 3s…',
      countdown: 3,
      action: 'Riprova ora',
    },
    timeout: {
      testId: 'sse-state-error-timeout',
      label: '6d-ii · timeout',
      icon: '⏱️',
      title: 'Risposta lenta',
      desc: 'Vuoi aspettare ancora? (>30s)',
      countdown: 18,
      action: 'Continua attesa',
      altAction: 'Cancella',
    },
    partial: {
      testId: 'sse-state-error-partial',
      label: '6d-iii · partial',
      icon: '⚠',
      title: 'Risposta incompleta',
      desc: 'Stream interrotto a metà — ripeti?',
      action: 'Ripeti query',
      showPartial: true,
    },
  }[kind];

  return (
    <DrawerShell testId={cfg.testId} label={cfg.label}>
      <div style={{
        flex: 1, padding: 18, overflowY: 'auto',
        display: 'flex', flexDirection: 'column', gap: 12,
      }}>
        {/* User query (always shown) */}
        <div style={{
          alignSelf: 'flex-end', maxWidth: '80%',
          padding: '10px 14px',
          background: chatC(0.12), color: chatC(),
          border: `1px solid ${chatC(0.22)}`,
          borderRadius: 'var(--r-lg)', borderBottomRightRadius: 4,
          fontSize: 13, fontWeight: 600,
        }}>scout class abilities</div>

        {/* Partial chunk visible (only for kind=partial) */}
        {cfg.showPartial && (
          <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start', opacity: .8 }}>
            <span style={{
              width: 26, height: 26, borderRadius: 'var(--r-sm)',
              background: agentC(0.15), color: agentC(),
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontSize: 13, flexShrink: 0,
            }}>🤖</span>
            <div style={{
              background: 'var(--bg-muted)',
              border: `1px solid ${dangerC(0.2)}`,
              borderRadius: 'var(--r-lg)', borderBottomLeftRadius: 4,
              padding: '11px 14px',
              fontSize: 12, lineHeight: 1.55, color: 'var(--text-muted)',
              flex: 1, fontStyle: 'italic',
            }}>
              La classe Scout inizia con tre abilità signature: quick step, evasive
              maneu—<span style={{ color: dangerC(), fontStyle: 'normal', fontWeight: 700 }}>[stream interrotto]</span>
            </div>
          </div>
        )}

        {/* Error banner */}
        <div style={{
          background: dangerC(0.08),
          border: `1.5px solid ${dangerC(0.3)}`,
          borderLeft: `4px solid ${dangerC()}`,
          borderRadius: 'var(--r-md)',
          padding: '12px 14px',
          display: 'flex', flexDirection: 'column', gap: 10,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <span style={{
              width: 32, height: 32, borderRadius: '50%',
              background: dangerC(0.15), color: dangerC(),
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontSize: 16, flexShrink: 0,
            }}>{cfg.icon}</span>
            <div>
              <div style={{
                fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 13, color: dangerC(),
              }}>{cfg.title}</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-sec)', marginTop: 2 }}>
                {cfg.desc}
              </div>
            </div>
          </div>

          {/* Countdown timer for connection / timeout */}
          {cfg.countdown && (
            <div style={{
              display: 'flex', alignItems: 'center', gap: 10,
              padding: '6px 10px', borderRadius: 'var(--r-sm)',
              background: 'var(--bg-card)', border: `1px solid ${dangerC(0.2)}`,
            }}>
              <span style={{
                width: 24, height: 24, borderRadius: '50%',
                border: `2px solid ${dangerC(0.25)}`,
                borderTop: `2px solid ${dangerC()}`,
                animation: 'spin 1s linear infinite',
              }}/>
              <span style={{
                fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)',
                fontWeight: 600,
              }}>
                {kind === 'connection' ? 'Retry in' : 'Timeout in'}{' '}
                <strong style={{ color: dangerC(), fontSize: 13 }}>{cfg.countdown}s</strong>
              </span>
              <span style={{ flex: 1 }}/>
              <span style={{
                fontFamily: 'var(--f-mono)', fontSize: 9, color: 'var(--text-muted)',
                textTransform: 'uppercase', letterSpacing: '.05em',
              }}>
                tentativo 2/3
              </span>
            </div>
          )}

          {/* Actions */}
          <div style={{ display: 'flex', gap: 8 }}>
            <Btn variant="primary" size="sm" icon="↻" danger fullWidth>{cfg.action}</Btn>
            {cfg.altAction && <Btn variant="ghost" size="sm">{cfg.altAction}</Btn>}
          </div>

          {/* Backoff hint */}
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 9.5, color: 'var(--text-muted)',
            display: 'flex', alignItems: 'center', gap: 4,
          }}>
            <span>ℹ</span>
            backoff exponential · 1s · 3s · 9s · max 3 retry
          </div>
        </div>
      </div>
    </DrawerShell>
  );
};


// ═══════════════════════════════════════════════════════
// ─── 7. EMPTY STATE KB (globale) ────────────────────
// /* No PDF in user's KB across any game · /kb · pre-onboarding */
// ═══════════════════════════════════════════════════════
const KbEmptyState = () => (
  <div style={{
    background: 'var(--bg-card)',
    border: `1px dashed ${kb(0.3)}`,
    borderRadius: 'var(--r-xl)',
    padding: '56px 40px',
    boxShadow: `0 4px 20px ${kb(0.06)}`,
    textAlign: 'center',
  }}>
    {/* Illustration — PDF stack + book (CSS art, no SVG) */}
    <div style={{
      width: 160, height: 140, margin: '0 auto 28px',
      position: 'relative',
    }}>
      {/* Book (background) */}
      <div style={{
        position: 'absolute', top: 24, left: 18, width: 110, height: 90,
        background: `linear-gradient(135deg, ${kb(0.85)}, ${kb()})`,
        borderRadius: '4px 8px 8px 4px',
        boxShadow: `0 8px 24px ${kb(0.3)}`,
        transform: 'rotate(-6deg)',
      }}>
        <div style={{
          position: 'absolute', left: 4, top: 0, bottom: 0,
          width: 4, background: `linear-gradient(180deg, ${kb(0.7)}, ${kb()})`,
          borderRadius: '2px 0 0 2px',
        }}/>
        <div style={{
          position: 'absolute', left: 18, top: '50%', transform: 'translateY(-50%)',
          fontSize: 30,
        }}>📖</div>
      </div>
      {/* PDF stack — 3 sheets */}
      {[0, 1, 2].map(i => (
        <div key={i} style={{
          position: 'absolute',
          top: 8 + i * 6, left: 60 - i * 4,
          width: 64, height: 80,
          background: '#fff',
          border: `1px solid ${kb(0.3)}`,
          borderRadius: 'var(--r-xs)',
          boxShadow: '0 4px 12px rgba(0,0,0,.12)',
          transform: `rotate(${4 + i * 3}deg)`,
          zIndex: 3 - i,
          display: 'flex', alignItems: 'flex-end', justifyContent: 'center',
          paddingBottom: 6,
        }}>
          <span style={{
            fontFamily: 'var(--f-display)', fontSize: 8, fontWeight: 800,
            color: kb(),
          }}>PDF</span>
          <div style={{
            position: 'absolute', top: 8, left: 6, right: 6,
            display: 'flex', flexDirection: 'column', gap: 2,
          }}>
            {[5, 4, 6, 3, 5].map((w, j) => (
              <div key={j} style={{
                height: 2, background: kb(0.2),
                width: `${w * 16}%`, borderRadius: 1,
              }}/>
            ))}
          </div>
        </div>
      ))}
      {/* Sparkle */}
      <span style={{
        position: 'absolute', top: 0, right: 8, fontSize: 18,
        animation: 'pulse 2s ease-in-out infinite',
      }}>✨</span>
    </div>

    <div style={{
      fontFamily: 'var(--f-mono)', fontSize: 11, color: kb(),
      textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 10,
      fontWeight: 700,
    }}>/kb · empty state</div>
    <h2 style={{
      fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 28,
      margin: '0 0 12px', color: 'var(--text)', letterSpacing: '-.01em',
    }}>La tua Knowledge Base è vuota</h2>
    <p style={{
      fontSize: 14, color: 'var(--text-sec)', margin: '0 auto 28px',
      maxWidth: 460, lineHeight: 1.6,
    }}>
      Carica un manuale per iniziare a chiedere all'agente.
      I tuoi PDF restano privati — solo tu e i tuoi agent ne hanno accesso.
    </p>

    {/* CTA row */}
    <div style={{
      display: 'flex', gap: 10, justifyContent: 'center',
      marginBottom: 36,
    }}>
      <Btn variant="primary" size="lg" icon="📄">Carica primo documento</Btn>
      <Btn variant="secondary" size="lg" icon="🌐">Esplora KB pubbliche</Btn>
    </div>

    {/* Quick start cards row */}
    <div style={{
      borderTop: '1px solid var(--border)', paddingTop: 24,
      maxWidth: 780, margin: '0 auto',
    }}>
      <div style={{
        fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800,
        textTransform: 'uppercase', letterSpacing: '.06em',
        color: 'var(--text-muted)', marginBottom: 14,
      }}>Quick start</div>
      <div style={{
        display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12,
      }}>
        {[
          {
            icon: '🎲', title: 'Connetti BGG',
            desc: 'Sincronizza la tua collezione BoardGameGeek e importa i manuali in un click.',
            tag: 'OAuth · 30s',
          },
          {
            icon: '👥', title: 'Importa da community',
            desc: 'Riusa manuali già caricati da altri utenti del gioco. SHA256 dedup.',
            tag: 'Privacy-safe',
          },
          {
            icon: '➕', title: 'Aggiungi manualmente',
            desc: 'Carica un PDF dal tuo dispositivo. Max 200 MB. Indexing async.',
            tag: 'PDF · 200 MB',
          },
        ].map((c, i) => (
          <div key={i} style={{
            background: 'var(--bg-muted)',
            border: '1px solid var(--border)',
            borderRadius: 'var(--r-md)',
            padding: '16px 14px',
            textAlign: 'left',
            cursor: 'pointer',
            transition: 'all var(--dur-sm) var(--ease-out)',
          }}>
            <div style={{
              width: 36, height: 36, borderRadius: 'var(--r-md)',
              background: kb(0.12), color: kb(),
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontSize: 18, marginBottom: 10,
            }}>{c.icon}</div>
            <div style={{
              fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 13,
              marginBottom: 4,
            }}>{c.title}</div>
            <div style={{
              fontSize: 11.5, color: 'var(--text-sec)', lineHeight: 1.45,
              marginBottom: 10,
            }}>{c.desc}</div>
            <Tag color={kb()} bg={kb(0.08)} border={kb(0.2)}>{c.tag}</Tag>
          </div>
        ))}
      </div>
    </div>
  </div>
);


// ═══════════════════════════════════════════════════════
// ─── THEME TOGGLE ────────────────────────────────────
// ═══════════════════════════════════════════════════════
const ThemeToggle = () => {
  const initial = typeof localStorage !== 'undefined'
    ? (localStorage.getItem('mai-theme') || 'light')
    : 'light';
  const [dark, setDark] = useState(initial === 'dark');
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
    try { localStorage.setItem('mai-theme', dark ? 'dark' : 'light'); } catch (e) {}
  }, [dark]);
  return (
    <button className="theme-toggle" onClick={() => setDark(d => !d)}>
      {dark ? '☀️ Light' : '🌙 Dark'}
    </button>
  );
};


// ═══════════════════════════════════════════════════════
// ─── MAIN APP ────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const App = () => (
  <div className="stage">
    <ThemeToggle />
    <div className="stage-wrap">

      {/* Page header */}
      <div>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 11,
          color: kb(), textTransform: 'uppercase', letterSpacing: '.08em',
          marginBottom: 6, fontWeight: 700,
        }}>
          Mockup · sp4-kb-globale · #488 · KB cross-game (route /kb)
        </div>
        <h1>Knowledge Base globale</h1>
        <p className="lead">
          7 screen states per la KB cross-game di MeepleAI: Home desktop · Search results ·
          Document viewer desktop + mobile · Editor (curator) · Ask the Agent drawer (SSE
          streaming, 4 sub-states) · Empty state. Palette <strong>--c-kb</strong> (teal).
          {' '}<a href="sp4-kb-hub.html" style={{ color: kb(), fontWeight: 700 }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-dashboard.html'; }, 0); /* DEMO-NAV */ }}>↗ vs. sp4-kb-hub (per-game)</a>
        </p>
        <div style={{
          marginTop: 14,
          padding: '10px 14px',
          background: 'var(--bg-muted)',
          border: `1px dashed ${kb(0.3)}`,
          borderRadius: 'var(--r-md)',
          fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.55,
        }}>
          <strong style={{ color: kb() }}>⚠ Distinzione semantica:</strong> questo file
          è la KB <strong>globale</strong> dell'utente (route <code style={{ background: kb(0.1), color: kb(), padding: '1px 6px', borderRadius: 3 }}>/kb</code>),
          aggrega documenti cross-game. Non confondere con{' '}
          <code style={{ background: 'var(--bg-card)', padding: '1px 6px', borderRadius: 3, color: 'var(--text-sec)' }}>sp4-kb-hub.jsx</code>{' '}
          (route <code style={{ background: kb(0.1), color: kb(), padding: '1px 6px', borderRadius: 3 }}>/games/[id]/kb</code>),
          KB per-game per gestione admin.
        </div>
      </div>

      {/* ── SCREEN 1: KB Home Desktop globale ── */}
      <div className="section-label">
        // ── SCREEN 1: KB Home Desktop (globale) ──
        <span className="meta">end-user · /kb · 3-col layout</span>
      </div>
      <KbHomeDesktop />

      {/* ── SCREEN 2: KB Search Results Desktop ── */}
      <div className="section-label">
        // ── SCREEN 2: KB Search Results Desktop ──
        <span className="meta">end-user · query echo + filters · 12→4 con filtri</span>
      </div>
      <KbSearchResultsDesktop />

      {/* ── SCREEN 3: Document Viewer Desktop (split-view) ── */}
      <div className="section-label">
        // ── SCREEN 3: Document Viewer Desktop (split-view) ──
        <span className="meta">end-user · thumbs · PDF · citations · click pill = glow 2s</span>
      </div>
      <KbDocViewerDesktop />

      {/* ── SCREEN 4: Document Viewer Mobile ── */}
      <div className="section-label">
        // ── SCREEN 4: Document Viewer Mobile ──
        <span className="meta">end-user · full-screen PDF + bottom sheet collapsable</span>
      </div>
      <div style={{ display: 'flex', justifyContent: 'center', padding: '8px 0 24px' }}>
        <KbDocViewerMobile />
      </div>

      {/* ── SCREEN 5: KB Editor Desktop (curator) ── */}
      <div className="section-label">
        // ── SCREEN 5: KB Editor Desktop (curator) ──
        <span className="meta">curator (owner private lib · NON admin globale) · re-index in background</span>
      </div>
      <KbEditorDesktop />

      {/* ── SCREEN 6: Ask the Agent drawer (SSE streaming) ── CRITICAL */}
      <div className="section-label">
        // ── SCREEN 6: Ask the Agent drawer (SSE streaming) — CRITICAL ──
        <span className="meta">end-user · 4 sub-states · data-testid="sse-state-*"</span>
      </div>

      <div className="annotation">
        <span className="label">SSE</span>
        <div>
          <strong style={{ color: 'var(--text)' }}>SSE stream:</strong> server invia chunks
          via <code style={{ background: kb(0.1), color: kb(), padding: '1px 6px', borderRadius: 3, fontFamily: 'var(--f-mono)' }}>data: &#123;...&#125;</code>{' '}
          events. Frontend appende chunk a state, mantieni connection con auto-reconnect
          (max <strong>3 retry</strong>, backoff <code style={{ background: 'var(--bg-card)', padding: '1px 5px', borderRadius: 3, fontFamily: 'var(--f-mono)' }}>1s · 3s · 9s</code>).{' '}
          Typing indicator visibile <strong>&lt;200ms</strong>, prima citation cliccabile <strong>&lt;1.5s</strong>.
          Animation chunk-append: <code style={{ background: 'var(--bg-card)', padding: '1px 5px', borderRadius: 3, fontFamily: 'var(--f-mono)' }}>600ms cubic-bezier(0.4, 0, 0.2, 1)</code>.
          Handles Playwright: <code style={{ background: 'var(--bg-card)', padding: '1px 5px', borderRadius: 3, fontFamily: 'var(--f-mono)' }}>data-testid="sse-state-*"</code>.
        </div>
      </div>

      {/* 6a + 6b side-by-side */}
      <div>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color: 'var(--text-muted)', textTransform: 'uppercase',
          letterSpacing: '.06em', marginBottom: 16,
        }}>Happy path — idle → streaming → completed</div>
        <div style={{ display: 'flex', gap: 28, flexWrap: 'wrap', justifyContent: 'center', paddingTop: 12 }}>
          <DrawerIdle />
          <DrawerStreaming />
          <DrawerCompleted />
        </div>
      </div>

      {/* 6d errors */}
      <div>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color: dangerC(), textTransform: 'uppercase',
          letterSpacing: '.06em', marginBottom: 16, marginTop: 24,
        }}>Error variants (3 mini)</div>
        <div style={{ display: 'flex', gap: 28, flexWrap: 'wrap', justifyContent: 'center', paddingTop: 12 }}>
          <DrawerError kind="connection" />
          <DrawerError kind="timeout" />
          <DrawerError kind="partial" />
        </div>
      </div>

      {/* ── SCREEN 7: Empty state KB globale ── */}
      <div className="section-label">
        // ── SCREEN 7: Empty state KB (globale) ──
        <span className="meta">end-user · 0 PDF · hero + 3 quick start</span>
      </div>
      <KbEmptyState />

      {/* Footer */}
      <div style={{
        marginTop: 24, paddingTop: 24,
        borderTop: '1px solid var(--border)',
        display: 'flex', alignItems: 'center', gap: 18, flexWrap: 'wrap',
        fontSize: 12, color: 'var(--text-muted)',
        fontFamily: 'var(--f-mono)',
      }}>
        <a href="00-hub.html" style={{ color: kb(), fontWeight: 700 }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-dashboard.html'; }, 0); /* DEMO-NAV */ }}>← 00-hub.html</a>
        <span>·</span>
        <a href="sp4-kb-hub.html" style={{ color: 'var(--text-sec)', fontWeight: 600 }}>sp4-kb-hub.jsx · KB per-game (#913)</a>
        <span>·</span>
        <span>Issue #488 · SP4 wave 4</span>
        <span style={{ flex: 1 }} />
        <span style={{ fontSize: 10, opacity: .6 }}>MeepleAI DS v1 · 2026-05-18</span>
      </div>
    </div>

    {/* ── Global keyframe animations ─── */}
    <style>{`
      @keyframes spin {
        from { transform: rotate(0deg); }
        to   { transform: rotate(360deg); }
      }
      @keyframes pulse {
        0%, 100% { opacity: 1; transform: scale(1); }
        50%      { opacity: .55; transform: scale(1.15); }
      }
      @keyframes glowPulse {
        0%   { box-shadow: 0 0 0 ${kb(0.4)}; outline-offset: 0; }
        30%  { box-shadow: 0 0 18px ${kb(0.6)}; }
        100% { box-shadow: 0 0 12px ${kb(0.5)}; }
      }
      @keyframes chunkAppend {
        from { opacity: 0; transform: translateY(4px); }
        to   { opacity: 1; transform: translateY(0); }
      }

      /* responsive */
      @media (max-width: 1100px) {
        .stage-wrap > div > div[style*="grid-template-columns: 260px 1fr 280px"],
        .stage-wrap > div > div[style*="grid-template-columns: 30% 70%"],
        .stage-wrap > div > div[style*="grid-template-columns: 12% 55% 33%"] {
          grid-template-columns: 1fr !important;
        }
      }
    `}</style>
  </div>
);

// ─── MOUNT ──────────────────────────────────────────────
const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(React.createElement(App));
