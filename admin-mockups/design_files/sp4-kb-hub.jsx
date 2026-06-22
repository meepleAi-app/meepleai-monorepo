/* MeepleAI SP4 wave 4 — KB Hub overview
   Route: /library/private/[gameId]/kb  |  /games/[id]/kb
   File: admin-mockups/design_files/sp4-kb-hub.{html,jsx}
   Issue: #913

   7 screen states (stacked):
   1. Hub default — header + stats strip + PDF list (5 rows, 4 status colors)
   2. Empty state — no indexed PDFs, CTA upload
   3. PDF row actions menu — popover with 5 actions
   4. Re-index modal — cost breakdown + async job polling
   5. RAPTOR rebuild — free tier (locked) + pro tier (active) side-by-side
   6. KB Coverage stats card — 4-5 metrics + mini sparkline
   7. Delete PDF confirmation dialog — cleanup list + destructive CTA

   Variants: light + dark mode (toggle fixed top-right).
   PDF list: desktop table-like rows / mobile stacked cards (responsive via CSS).

   v2 components flagged for impl post-merge:
   - KbHubHeader       → apps/web/src/components/ui/v2/kb-hub-header/
   - KbStatsStrip      → apps/web/src/components/ui/v2/kb-stats-strip/
   - KbPdfRow          → apps/web/src/components/ui/v2/kb-pdf-row/
   - KbStatsCard       → apps/web/src/components/ui/v2/kb-stats-card/  ← reusable primitive
   - RaptorRebuildPanel→ apps/web/src/components/ui/v2/raptor-rebuild-panel/

   Riusi:
   - EntityChip (wave 1 pattern)
   - ConnectionBar (PR #549/#552 pattern)
*/

const { useState, useEffect, useRef } = React;
const DS = window.DS;

// ─── ENTITY HSL HELPER ──────────────────────────────────
const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.kb;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};
const kbColor   = (a) => entityHsl('kb', a);
const dangerColor = (a) => a !== undefined
  ? `hsla(350, 89%, 60%, ${a})`
  : `hsl(350, 89%, 60%)`;
const goldColor = (a) => a !== undefined
  ? `hsla(43, 92%, 52%, ${a})`
  : `hsl(43, 92%, 52%)`;

// ─── SAMPLE DATA ────────────────────────────────────────
const GAME = { id: 'g-gloomhaven', title: 'Gloomhaven', cover: '#5c3a21',
  coverGrad: 'linear-gradient(135deg, hsl(0,35%,28%), hsl(20,30%,20%))',
  emoji: '⚔️' };

const KB_STATS = {
  docs: 12, chunks: 1247, embeddings: 4891,
  lastReindex: '3 gg fa',
  raptorLastRebuild: '12 gg fa',
  lifetimeCost: '$2.84',
  costHistory: [0.12, 0.38, 0.22, 0.45, 0.19, 0.84, 0.64],
};

const PDFS = [
  { id: 'p1', name: 'Rulebook v2 EN',                status: 'ready',    date: '2 gg fa',  size: '45 MB', chunks: 312 },
  { id: 'p2', name: 'Scenario Book vol.1',            status: 'ready',    date: '5 gg fa',  size: '62 MB', chunks: 487 },
  { id: 'p3', name: 'Forgotten Circles supplement',   status: 'indexing', date: '5 min fa', size: '28 MB', chunks: 0   },
  { id: 'p4', name: 'Reference cards (legacy)',       status: 'stale',    date: '45 gg fa', size: '12 MB', chunks: 89  },
  { id: 'p5', name: 'Old rulebook v1 (failed)',       status: 'failed',   date: '1 sett fa',size: '38 MB', chunks: 0   },
];

// ─── STATUS CONFIG ───────────────────────────────────────
const STATUS_CFG = {
  ready:    { label: 'Ready',    bg: 'hsl(142,60%,42%)', fg: '#fff', dot: 'hsl(142,60%,42%)' },
  indexing: { label: 'Indexing', bg: 'hsl(38,92%,50%)',  fg: '#fff', dot: 'hsl(38,92%,50%)'  },
  stale:    { label: 'Stale',    bg: 'hsl(220,15%,55%)', fg: '#fff', dot: 'hsl(220,15%,55%)' },
  failed:   { label: 'Failed',   bg: 'hsl(350,89%,60%)', fg: '#fff', dot: 'hsl(350,89%,60%)' },
};

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
        background: 'rgba(255,255,255,.6)',
        animation: status === 'indexing' ? 'pulse 1.4s ease-in-out infinite' : 'none',
      }}/>
      {cfg.label}
    </span>
  );
};

// ═══════════════════════════════════════════════════════
// ─── KB STATS CARD (reusable primitive) ─────────────
// /* v2: KbStatsCard → apps/web/src/components/ui/v2/kb-stats-card/ */
// ═══════════════════════════════════════════════════════
const KbStatsCard = ({ stats, compact }) => {
  // Mini sparkline: CSS-only bars from fake history data
  const maxVal = Math.max(...stats.costHistory);
  return (
    <div style={{
      background: 'var(--bg-card)', border: `1px solid ${kbColor(0.22)}`,
      borderRadius: 'var(--r-xl)', padding: compact ? '16px 20px' : '20px 24px',
      boxShadow: `0 4px 16px ${kbColor(0.1)}`,
    }}>
      {!compact && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 16 }}>
          <span style={{
            width: 32, height: 32, borderRadius: 'var(--r-md)',
            background: kbColor(0.12), display: 'flex', alignItems: 'center',
            justifyContent: 'center', fontSize: 16,
          }}>📊</span>
          <div>
            <div style={{ fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13 }}>
              KB Coverage Stats
            </div>
            <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>Metriche indicizzazione</div>
          </div>
        </div>
      )}

      {/* 4-metric grid */}
      <div style={{
        display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)',
        gap: 12, marginBottom: compact ? 0 : 16,
      }}>
        {[
          { label: 'Chunks',      value: stats.chunks.toLocaleString('it-IT'), icon: '🧩', color: kbColor() },
          { label: 'Embeddings',  value: stats.embeddings.toLocaleString('it-IT'), icon: '🔗', color: entityHsl('agent') },
          { label: 'Ultima idx.', value: stats.lastReindex, icon: '🕐', color: entityHsl('session') },
          { label: 'RAPTOR last', value: stats.raptorLastRebuild, icon: '🦅', color: goldColor() },
        ].map((m) => (
          <div key={m.label} style={{
            background: 'var(--bg-muted)', borderRadius: 'var(--r-md)',
            padding: '10px 12px', textAlign: 'center',
          }}>
            <div style={{ fontSize: 18, marginBottom: 4 }}>{m.icon}</div>
            <div style={{
              fontFamily: 'var(--f-mono)', fontWeight: 700,
              fontSize: 14, color: m.color, lineHeight: 1.1,
            }}>{m.value}</div>
            <div style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 3 }}>{m.label}</div>
          </div>
        ))}
      </div>

      {!compact && (
        <>
          {/* Lifetime cost */}
          <div style={{
            display: 'flex', alignItems: 'center', justifyContent: 'space-between',
            padding: '10px 12px', background: kbColor(0.06),
            borderRadius: 'var(--r-md)', marginBottom: 12,
            border: `1px solid ${kbColor(0.12)}`,
          }}>
            <span style={{ fontSize: 12, color: 'var(--text-sec)' }}>Costo lifetime token</span>
            <span style={{
              fontFamily: 'var(--f-mono)', fontWeight: 700, fontSize: 14, color: kbColor(),
            }}>{stats.lifetimeCost}</span>
          </div>

          {/* Mini sparkline (CSS bars) */}
          <div>
            <div style={{ fontSize: 10, color: 'var(--text-muted)', marginBottom: 6 }}>
              Consumo token · ultimi 7 gg
            </div>
            <div style={{ display: 'flex', alignItems: 'flex-end', gap: 4, height: 32 }}>
              {stats.costHistory.map((v, i) => (
                <div key={i} style={{
                  flex: 1, borderRadius: '3px 3px 0 0',
                  height: `${Math.max(4, (v / maxVal) * 100)}%`,
                  background: i === stats.costHistory.length - 1
                    ? kbColor()
                    : kbColor(0.35),
                  transition: 'height .3s ease',
                }}/>
              ))}
            </div>
            <div style={{
              display: 'flex', justifyContent: 'space-between',
              fontSize: 9, color: 'var(--text-muted)', marginTop: 4,
              fontFamily: 'var(--f-mono)',
            }}>
              <span>-7gg</span><span>oggi</span>
            </div>
          </div>
        </>
      )}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── PDF ROW ────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const PdfRow = ({ pdf, onAction }) => (
  <div style={{
    display: 'grid',
    gridTemplateColumns: '32px 1fr auto auto auto',
    alignItems: 'center', gap: 12,
    padding: '12px 16px',
    borderBottom: '1px solid var(--border-light)',
    transition: 'background var(--dur-xs)',
  }}
    onMouseEnter={e => e.currentTarget.style.background = 'var(--bg-hover)'}
    onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
  >
    {/* PDF icon */}
    <div style={{
      width: 32, height: 32, borderRadius: 'var(--r-sm)',
      background: kbColor(0.12), display: 'flex', alignItems: 'center',
      justifyContent: 'center', fontSize: 16, flexShrink: 0,
    }}>📄</div>

    {/* Name + meta */}
    <div style={{ minWidth: 0 }}>
      <div style={{
        fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 13,
        whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
      }}>{pdf.name}</div>
      <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>
        {pdf.size}
        {pdf.chunks > 0 && (
          <span style={{
            marginLeft: 8, fontFamily: 'var(--f-mono)', color: kbColor(0.8),
          }}>{pdf.chunks} chunks</span>
        )}
      </div>
    </div>

    {/* Status */}
    <StatusBadge status={pdf.status} />

    {/* Date */}
    <div style={{
      fontSize: 11, color: 'var(--text-muted)',
      whiteSpace: 'nowrap', fontFamily: 'var(--f-mono)',
    }}>
      {pdf.date}
    </div>

    {/* CTA */}
    <button onClick={() => onAction(pdf)} style={{
      display: 'inline-flex', alignItems: 'center', gap: 4,
      padding: '5px 10px', borderRadius: 'var(--r-md)',
      background: kbColor(0.08), color: kbColor(),
      border: `1px solid ${kbColor(0.2)}`,
      fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 700,
      cursor: 'pointer', whiteSpace: 'nowrap',
    }}>
      Open detail →
    </button>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── 1. HUB DEFAULT STATE ────────────────────────────
// ═══════════════════════════════════════════════════════
const HubDefault = ({ onRowAction }) => (
  <div style={{
    background: 'var(--bg-card)', border: `1px solid ${kbColor(0.2)}`,
    borderRadius: 'var(--r-xl)', overflow: 'hidden',
    boxShadow: `0 4px 20px ${kbColor(0.1)}`,
  }}>
    {/* Header */}
    <div style={{
      padding: '20px 24px 16px',
      borderBottom: `1px solid ${kbColor(0.15)}`,
      background: `linear-gradient(135deg, ${kbColor(0.06)}, transparent)`,
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 10 }}>
        {/* Game cover thumbnail */}
        <div style={{
          width: 48, height: 48, borderRadius: 'var(--r-md)',
          background: GAME.coverGrad, display: 'flex', alignItems: 'center',
          justifyContent: 'center', fontSize: 22, flexShrink: 0,
          border: '2px solid rgba(255,255,255,.15)',
          boxShadow: '0 2px 8px rgba(0,0,0,.2)',
        }}>{GAME.emoji}</div>

        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <h2 style={{
              fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 18, margin: 0,
            }}>{GAME.title} · KB</h2>
            <EntityChip type="kb" label="Knowledge Base" sm />
          </div>
          <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 3 }}>
            <a href="#" style={{ color: kbColor(), textDecoration: 'none', fontWeight: 600 }} onClick={() => { setTimeout(() => { window.location.href = 'librogame-runthrough-play-session.html'; }, 0); /* DEMO-NAV */ }}>
              ↗ Apri scheda game
            </a>
            <span style={{ margin: '0 6px', opacity: .4 }}>·</span>
            /library/private/{GAME.id}/kb
          </div>
        </div>

        {/* Actions */}
        <div style={{ display: 'flex', gap: 8, flexShrink: 0 }}>
          <button style={{
            padding: '8px 14px', borderRadius: 'var(--r-md)',
            background: kbColor(0.1), color: kbColor(),
            border: `1px solid ${kbColor(0.25)}`,
            fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700, cursor: 'pointer',
          }}>+ Carica PDF</button>
          <button style={{
            padding: '8px 14px', borderRadius: 'var(--r-md)',
            background: kbColor(), color: '#fff',
            border: 'none',
            fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700, cursor: 'pointer',
          }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-kb-detail.html'; }, 0); /* DEMO-NAV */ }}>⟳ Re-index all</button>
        </div>
      </div>

      {/* Stats strip */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 4, flexWrap: 'wrap',
        padding: '10px 14px', background: kbColor(0.06),
        borderRadius: 'var(--r-md)', border: `1px solid ${kbColor(0.1)}`,
      }}>
        {[
          { icon: '📄', label: `${KB_STATS.docs} documenti` },
          { icon: '🧩', label: `${KB_STATS.chunks.toLocaleString('it-IT')} chunks` },
          { icon: '🔗', label: `${KB_STATS.embeddings.toLocaleString('it-IT')} embeddings` },
          { icon: '🕐', label: `ultima reindex ${KB_STATS.lastReindex}` },
        ].map((s, i, arr) => (
          <span key={s.label} style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
            <span style={{ fontSize: 12 }}>{s.icon}</span>
            <span style={{ fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 600, color: 'var(--text-sec)' }}>
              {s.label}
            </span>
            {i < arr.length - 1 && (
              <span style={{ margin: '0 6px', color: 'var(--text-muted)', opacity: .5 }}>·</span>
            )}
          </span>
        ))}
      </div>
    </div>

    {/* PDF list */}
    <div>
      <div style={{
        display: 'grid', gridTemplateColumns: '32px 1fr auto auto auto',
        gap: 12, padding: '8px 16px 6px',
        borderBottom: '1px solid var(--border)',
      }}>
        {['', 'Documento', 'Stato', 'Caricato', ''].map((h, i) => (
          <div key={i} style={{
            fontSize: 10, fontFamily: 'var(--f-mono)', fontWeight: 700,
            textTransform: 'uppercase', letterSpacing: '.06em', color: 'var(--text-muted)',
          }}>{h}</div>
        ))}
      </div>
      {PDFS.map(pdf => (
        <PdfRow key={pdf.id} pdf={pdf} onAction={onRowAction} />
      ))}
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── 2. EMPTY STATE ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const EmptyState = () => (
  <div style={{
    background: 'var(--bg-card)', border: `1px dashed ${kbColor(0.3)}`,
    borderRadius: 'var(--r-xl)', padding: '60px 40px',
    textAlign: 'center', boxShadow: `0 4px 20px ${kbColor(0.06)}`,
  }}>
    {/* Illustration */}
    <div style={{
      width: 80, height: 80, margin: '0 auto 20px',
      borderRadius: '50%', background: kbColor(0.1),
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontSize: 40, border: `2px dashed ${kbColor(0.25)}`,
    }}>📚</div>

    <h3 style={{
      fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 18,
      margin: '0 0 8px', color: 'var(--text)',
    }}>Nessun PDF indicizzato</h3>
    <p style={{
      fontSize: 13, color: 'var(--text-muted)', margin: '0 auto 24px',
      maxWidth: 340, lineHeight: 1.55,
    }}>
      Carica il primo documento PDF per indicizzare le regole di{' '}
      <strong style={{ color: 'var(--text-sec)' }}>{GAME.title}</strong> e abilitare
      il RAG chat con l'AI agent.
    </p>
    <button style={{
      display: 'inline-flex', alignItems: 'center', gap: 8,
      padding: '12px 24px', borderRadius: 'var(--r-md)',
      background: kbColor(), color: '#fff', border: 'none',
      fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 700,
      cursor: 'pointer', boxShadow: `0 4px 12px ${kbColor(0.3)}`,
    }}>
      <span>📤</span> Carica primo documento
    </button>
    <div style={{
      marginTop: 16, fontSize: 11, color: 'var(--text-muted)',
    }}>
      Formati supportati: PDF · Max 200 MB per file
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── 3. PDF ROW ACTIONS MENU (popover) ──────────────
// ═══════════════════════════════════════════════════════
const ActionsMenu = ({ pdf, onClose }) => {
  const actions = [
    { icon: '↗', label: 'Open detail', desc: 'Visualizza chunks e preview', color: kbColor() },
    { icon: '⟳', label: 'Re-index', desc: 'Rielabora embedding PDF', color: entityHsl('agent') },
    { icon: '📋', label: 'View cost stats', desc: 'Token consumati e costo', color: entityHsl('session') },
    { icon: '📦', label: 'Move to other game', desc: 'Sposta in altra scheda KB', color: entityHsl('toolkit') },
    { icon: '🗑', label: 'Delete', desc: 'Rimuovi PDF e cleanup', color: dangerColor() },
  ];
  return (
    <div style={{
      background: 'var(--bg-card)', border: `1px solid ${kbColor(0.2)}`,
      borderRadius: 'var(--r-xl)', overflow: 'hidden',
      boxShadow: `0 8px 32px ${kbColor(0.15)}, 0 2px 8px rgba(0,0,0,.1)`,
      width: 260, flexShrink: 0,
    }}>
      {/* Header */}
      <div style={{
        padding: '12px 16px 10px',
        borderBottom: `1px solid ${kbColor(0.1)}`,
        background: kbColor(0.05),
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: 14 }}>📄</span>
          <div>
            <div style={{ fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 12 }}>
              {pdf.name}
            </div>
            <div style={{ fontSize: 10, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>
              {pdf.size} · {pdf.date}
            </div>
          </div>
        </div>
      </div>

      {/* Action list */}
      {actions.map((a, i) => (
        <button key={a.label} onClick={() => onClose && onClose(a.label)} style={{
          display: 'flex', alignItems: 'center', gap: 12,
          width: '100%', padding: '11px 16px',
          background: 'transparent', border: 'none',
          borderTop: i > 0 ? '1px solid var(--border-light)' : 'none',
          cursor: 'pointer', textAlign: 'left',
          transition: 'background var(--dur-xs)',
        }}
          onMouseEnter={e => e.currentTarget.style.background = 'var(--bg-hover)'}
          onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
        >
          <span style={{
            width: 28, height: 28, borderRadius: 'var(--r-sm)',
            background: a.label === 'Delete' ? dangerColor(0.1) : `${a.color}1a`,
            color: a.color, display: 'flex', alignItems: 'center',
            justifyContent: 'center', fontSize: 13, flexShrink: 0,
          }}>{a.icon}</span>
          <div>
            <div style={{
              fontSize: 12, fontWeight: 700,
              fontFamily: 'var(--f-display)',
              color: a.label === 'Delete' ? dangerColor() : 'var(--text)',
            }}>{a.label}</div>
            <div style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 1 }}>
              {a.desc}
            </div>
          </div>
        </button>
      ))}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── 4. RE-INDEX MODAL ───────────────────────────────
// ═══════════════════════════════════════════════════════
const ReindexModal = ({ onClose }) => {
  const [phase, setPhase] = useState('confirm'); // confirm | running | done

  const costRows = [
    { label: 'Chunks da re-embed', value: '1,247', unit: '' },
    { label: 'Tokens per chunk (avg)', value: '~320', unit: 'tokens' },
    { label: 'Tokens totali stimati', value: '~398,400', unit: 'tokens' },
    { label: 'Costo per 1M tokens', value: '$0.0001', unit: '/1M' },
    { label: 'Costo stimato', value: '~$0.45', unit: '', bold: true },
    { label: 'Durata stimata', value: '3–5 min', unit: '', bold: true },
  ];

  return (
    <div style={{
      background: 'rgba(0,0,0,.45)', borderRadius: 'var(--r-xl)',
      padding: 32, display: 'flex', alignItems: 'center', justifyContent: 'center',
      backdropFilter: 'blur(4px)',
    }}>
      <div style={{
        background: 'var(--bg-card)', borderRadius: 'var(--r-xl)',
        border: `1px solid ${kbColor(0.25)}`,
        boxShadow: `0 20px 60px rgba(0,0,0,.25), 0 4px 16px ${kbColor(0.15)}`,
        width: 440, overflow: 'hidden',
      }}>
        {/* Title bar */}
        <div style={{
          padding: '18px 24px 14px',
          borderBottom: `1px solid ${kbColor(0.12)}`,
          background: kbColor(0.06),
          display: 'flex', alignItems: 'center', gap: 12,
        }}>
          <span style={{
            width: 36, height: 36, borderRadius: 'var(--r-md)',
            background: kbColor(0.15), display: 'flex', alignItems: 'center',
            justifyContent: 'center', fontSize: 18,
          }}>⟳</span>
          <div>
            <div style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 16 }}>
              Re-index full KB
            </div>
            <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>
              {GAME.title} · 12 documenti · 1,247 chunks
            </div>
          </div>
        </div>

        <div style={{ padding: '20px 24px' }}>
          {phase === 'confirm' && (
            <>
              {/* Cost breakdown table */}
              <div style={{
                background: 'var(--bg-muted)', borderRadius: 'var(--r-md)',
                overflow: 'hidden', marginBottom: 18,
                border: '1px solid var(--border)',
              }}>
                <div style={{
                  padding: '8px 14px', fontSize: 10,
                  fontFamily: 'var(--f-mono)', fontWeight: 700,
                  textTransform: 'uppercase', letterSpacing: '.06em',
                  color: 'var(--text-muted)', borderBottom: '1px solid var(--border)',
                }}>Stima costo operazione</div>
                {costRows.map((r) => (
                  <div key={r.label} style={{
                    display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                    padding: '8px 14px',
                    borderBottom: '1px solid var(--border-light)',
                    background: r.bold ? kbColor(0.06) : 'transparent',
                  }}>
                    <span style={{
                      fontSize: 12, color: r.bold ? 'var(--text)' : 'var(--text-sec)',
                      fontWeight: r.bold ? 700 : 400,
                    }}>{r.label}</span>
                    <span style={{
                      fontFamily: 'var(--f-mono)',
                      fontSize: r.bold ? 13 : 11,
                      fontWeight: r.bold ? 800 : 500,
                      color: r.bold ? kbColor() : 'var(--text-sec)',
                    }}>{r.value}{r.unit && <span style={{ fontSize: 9, opacity: .6, marginLeft: 2 }}>{r.unit}</span>}</span>
                  </div>
                ))}
              </div>

              <p style={{ fontSize: 12, color: 'var(--text-muted)', margin: '0 0 18px', lineHeight: 1.5 }}>
                Questa operazione rielabora tutti i chunk e rigenera gli embedding.
                Le chat esistenti continueranno a funzionare durante il processo.
              </p>

              <div style={{ display: 'flex', gap: 10 }}>
                <button onClick={(e) => { (() => setPhase('running'))(e); setTimeout(() => { window.location.href = 'sp4-kb-detail.html'; }, 0); /* DEMO-NAV */ }} style={{
                  flex: 1, padding: '11px 16px', borderRadius: 'var(--r-md)',
                  background: kbColor(), color: '#fff', border: 'none',
                  fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700,
                  cursor: 'pointer',
                }}>⟳ Re-index</button>
                <button onClick={onClose} style={{
                  flex: 1, padding: '11px 16px', borderRadius: 'var(--r-md)',
                  background: 'var(--bg-muted)', color: 'var(--text-sec)',
                  border: '1px solid var(--border)',
                  fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700,
                  cursor: 'pointer',
                }}>Annulla</button>
              </div>
            </>
          )}

          {phase === 'running' && (
            <div style={{ textAlign: 'center', padding: '12px 0' }}>
              <div style={{
                width: 48, height: 48, borderRadius: '50%',
                border: `3px solid ${kbColor(0.2)}`,
                borderTop: `3px solid ${kbColor()}`,
                margin: '0 auto 16px',
                animation: 'spin 1s linear infinite',
              }}/>
              <div style={{ fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 15, marginBottom: 6 }}>
                Re-index in corso…
              </div>
              <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 18 }}>
                Job ID: <code style={{ fontFamily: 'var(--f-mono)', color: kbColor() }}>job_xk92m3p</code>
              </div>
              {/* Progress bar */}
              <div style={{
                height: 6, background: 'var(--bg-muted)', borderRadius: 3,
                overflow: 'hidden', marginBottom: 8,
              }}>
                <div style={{
                  width: '38%', height: '100%', background: kbColor(),
                  borderRadius: 3, transition: 'width 2s ease',
                }}/>
              </div>
              <div style={{ fontSize: 11, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>
                474 / 1,247 chunks · ~3 min rimanenti
              </div>
              <button onClick={(e) => { (() => setPhase('done'))(e); setTimeout(() => { window.location.href = 'sp4-kb-detail.html'; }, 0); /* DEMO-NAV */ }} style={{
                marginTop: 16, padding: '8px 18px', borderRadius: 'var(--r-md)',
                background: 'var(--bg-muted)', color: 'var(--text-muted)',
                border: '1px solid var(--border)',
                fontFamily: 'var(--f-display)', fontSize: 11, fontWeight: 600,
                cursor: 'pointer',
              }}>Simulate done →</button>
            </div>
          )}

          {phase === 'done' && (
            <div style={{ textAlign: 'center', padding: '12px 0' }}>
              <div style={{
                width: 52, height: 52, borderRadius: '50%',
                background: 'hsl(142,60%,42%)', margin: '0 auto 16px',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                fontSize: 24, color: '#fff',
              }}>✓</div>
              <div style={{ fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 15, marginBottom: 6 }}>
                Re-index completato
              </div>
              <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 18 }}>
                1,247 chunks · 4,891 embeddings · costo reale $0.43
              </div>
              <button onClick={onClose} style={{
                padding: '10px 24px', borderRadius: 'var(--r-md)',
                background: kbColor(), color: '#fff', border: 'none',
                fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700,
                cursor: 'pointer',
              }}>Chiudi</button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── 5. RAPTOR REBUILD PANEL ─────────────────────────
// ═══════════════════════════════════════════════════════
const RaptorPanel = ({ tier }) => {
  const locked = tier === 'free';
  return (
    <div style={{
      background: 'var(--bg-card)',
      border: locked
        ? '1px solid var(--border)'
        : `1px solid ${goldColor(0.35)}`,
      borderRadius: 'var(--r-xl)', overflow: 'hidden',
      boxShadow: locked ? 'none' : `0 4px 20px ${goldColor(0.12)}`,
      opacity: locked ? 0.85 : 1,
      flex: 1,
    }}>
      {/* Header */}
      <div style={{
        padding: '16px 20px 12px',
        borderBottom: `1px solid ${locked ? 'var(--border-light)' : goldColor(0.18)}`,
        background: locked ? 'var(--bg-muted)' : goldColor(0.06),
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <span style={{ fontSize: 22 }}>🦅</span>
        <div style={{ flex: 1 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 14 }}>
              RAPTOR Rebuild
            </span>
            {locked ? (
              <span style={{
                padding: '2px 8px', borderRadius: 'var(--r-pill)',
                background: goldColor(0.15), color: goldColor(),
                fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform: 'uppercase', letterSpacing: '.06em',
                border: `1px solid ${goldColor(0.25)}`,
              }}>🔒 PRO</span>
            ) : (
              <span style={{
                padding: '2px 8px', borderRadius: 'var(--r-pill)',
                background: goldColor(), color: '#fff',
                fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform: 'uppercase', letterSpacing: '.06em',
              }}>PRO ✓</span>
            )}
          </div>
          <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>
            Recursive Abstractive Processing Tree Of Retrieval
          </div>
        </div>
      </div>

      <div style={{ padding: '16px 20px' }}>
        {/* Metrics row */}
        <div style={{
          display: 'grid', gridTemplateColumns: '1fr 1fr',
          gap: 10, marginBottom: 14,
        }}>
          {[
            { label: 'Ultimo rebuild', value: KB_STATS.raptorLastRebuild },
            { label: 'Summaries gen.', value: '147' },
          ].map(m => (
            <div key={m.label} style={{
              background: 'var(--bg-muted)', borderRadius: 'var(--r-sm)',
              padding: '8px 12px',
            }}>
              <div style={{ fontFamily: 'var(--f-mono)', fontWeight: 700, fontSize: 13, color: locked ? 'var(--text-muted)' : goldColor() }}>
                {m.value}
              </div>
              <div style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 2 }}>{m.label}</div>
            </div>
          ))}
        </div>

        {locked ? (
          <>
            {/* Locked state */}
            <div style={{
              padding: '10px 14px', borderRadius: 'var(--r-md)',
              background: 'var(--bg-sunken)', border: '1px dashed var(--border-strong)',
              marginBottom: 14,
              display: 'flex', alignItems: 'flex-start', gap: 10,
            }}>
              <span style={{ fontSize: 16, flexShrink: 0 }}>🔒</span>
              <div style={{ fontSize: 12, color: 'var(--text-muted)', lineHeight: 1.5 }}>
                RAPTOR richiede piano <strong style={{ color: goldColor() }}>Pro</strong>.
                Abilita clustering gerarchico per retrieval Q&amp;A avanzato.
              </div>
            </div>
            <button disabled style={{
              width: '100%', padding: '11px 16px', borderRadius: 'var(--r-md)',
              background: 'var(--bg-muted)', color: 'var(--text-muted)',
              border: '1px solid var(--border)', opacity: 0.5,
              fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700,
              cursor: 'not-allowed',
            }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-kb-detail.html'; }, 0); /* DEMO-NAV */ }}>
              🔒 Rebuild RAPTOR — Upgrade to Pro
            </button>
            <div style={{ textAlign: 'center', marginTop: 10 }}>
              <a href="#" style={{ fontSize: 11, color: goldColor(), fontWeight: 600 }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-discover.html'; }, 0); /* DEMO-NAV */ }}>
                Scopri piano Pro →
              </a>
            </div>
          </>
        ) : (
          <>
            {/* Pro active state */}
            <div style={{
              display: 'flex', alignItems: 'center', justifyContent: 'space-between',
              padding: '10px 14px', borderRadius: 'var(--r-md)',
              background: goldColor(0.08), border: `1px solid ${goldColor(0.2)}`,
              marginBottom: 14,
            }}>
              <div>
                <div style={{ fontSize: 12, fontWeight: 700, color: 'var(--text)' }}>Stima operazione</div>
                <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>~1,247 chunks → summaries gerarchici</div>
              </div>
              <div style={{ textAlign: 'right' }}>
                <div style={{ fontFamily: 'var(--f-mono)', fontWeight: 800, fontSize: 15, color: goldColor() }}>
                  $1.20
                </div>
                <div style={{ fontSize: 10, color: 'var(--text-muted)' }}>+ 6 min</div>
              </div>
            </div>
            <button style={{
              width: '100%', padding: '11px 16px', borderRadius: 'var(--r-md)',
              background: goldColor(), color: '#fff', border: 'none',
              fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700,
              cursor: 'pointer',
              boxShadow: `0 4px 12px ${goldColor(0.3)}`,
            }}>
              🦅 Rebuild RAPTOR — $1.20 · 6 min
            </button>
          </>
        )}
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── 7. DELETE CONFIRMATION DIALOG ──────────────────
// ═══════════════════════════════════════════════════════
const DeleteDialog = ({ pdf, onClose }) => (
  <div style={{
    background: 'rgba(0,0,0,.45)', borderRadius: 'var(--r-xl)',
    padding: 32, display: 'flex', alignItems: 'center', justifyContent: 'center',
    backdropFilter: 'blur(4px)',
  }}>
    <div style={{
      background: 'var(--bg-card)', borderRadius: 'var(--r-xl)',
      border: `1px solid ${dangerColor(0.3)}`,
      boxShadow: `0 20px 60px rgba(0,0,0,.25), 0 4px 16px ${dangerColor(0.15)}`,
      width: 420, overflow: 'hidden',
    }}>
      {/* Header */}
      <div style={{
        padding: '18px 24px 14px',
        borderBottom: `1px solid ${dangerColor(0.12)}`,
        background: dangerColor(0.05),
        display: 'flex', alignItems: 'center', gap: 12,
      }}>
        <span style={{
          width: 40, height: 40, borderRadius: '50%',
          background: dangerColor(0.12), display: 'flex', alignItems: 'center',
          justifyContent: 'center', fontSize: 20,
        }}>⚠️</span>
        <div>
          <div style={{
            fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 16,
            color: dangerColor(),
          }}>Confermi eliminazione PDF?</div>
          <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>
            {pdf ? pdf.name : 'Rulebook v2 EN'}
          </div>
        </div>
      </div>

      <div style={{ padding: '20px 24px' }}>
        {/* Cleanup list */}
        <div style={{
          background: dangerColor(0.05), borderRadius: 'var(--r-md)',
          border: `1px solid ${dangerColor(0.15)}`,
          padding: '12px 16px', marginBottom: 18,
        }}>
          <div style={{
            fontSize: 11, fontFamily: 'var(--f-mono)', fontWeight: 700,
            color: dangerColor(), textTransform: 'uppercase', letterSpacing: '.05em',
            marginBottom: 8,
          }}>Verrà eliminato definitivamente:</div>
          {[
            `📄 PDF file — 45 MB`,
            `🧩 312 chunk embeddings`,
            `🦅 12 raptor_summaries`,
            `🔗 34 graph_edges correlati`,
          ].map((item) => (
            <div key={item} style={{
              display: 'flex', alignItems: 'center', gap: 8,
              fontSize: 12, color: 'var(--text-sec)',
              padding: '4px 0',
              borderBottom: '1px solid var(--border-light)',
            }}>
              <span style={{
                width: 5, height: 5, borderRadius: '50%',
                background: dangerColor(), flexShrink: 0,
              }}/>
              {item}
            </div>
          ))}
          <div style={{
            marginTop: 10, fontSize: 11, fontWeight: 700,
            color: dangerColor(), display: 'flex', alignItems: 'center', gap: 6,
          }}>
            <span>⚠️</span>
            <span>Operazione <strong>irreversibile</strong> — nessun backup disponibile</span>
          </div>
        </div>

        <div style={{ display: 'flex', gap: 10 }}>
          <button onClick={onClose} style={{
            flex: 1, padding: '11px 16px', borderRadius: 'var(--r-md)',
            background: 'var(--bg-muted)', color: 'var(--text-sec)',
            border: '1px solid var(--border)',
            fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700,
            cursor: 'pointer',
          }}>Annulla</button>
          <button onClick={onClose} style={{
            flex: 1, padding: '11px 16px', borderRadius: 'var(--r-md)',
            background: dangerColor(), color: '#fff', border: 'none',
            fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700,
            cursor: 'pointer',
            boxShadow: `0 4px 12px ${dangerColor(0.3)}`,
          }}>🗑 Elimina definitivamente</button>
        </div>
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── THEME TOGGLE ────────────────────────────────────
// ═══════════════════════════════════════════════════════
const ThemeToggle = () => {
  const [dark, setDark] = useState(false);
  const toggle = () => {
    const next = !dark;
    setDark(next);
    document.documentElement.setAttribute('data-theme', next ? 'dark' : 'light');
  };
  return (
    <button className="theme-toggle" onClick={toggle}>
      {dark ? '☀️ Light' : '🌙 Dark'}
    </button>
  );
};

// ═══════════════════════════════════════════════════════
// ─── MAIN APP ────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const App = () => {
  const [selectedPdf, setSelectedPdf] = useState(PDFS[0]);

  return (
    <div className="stage">
      <ThemeToggle />
      <div className="stage-wrap">

        {/* Page header */}
        <div style={{ marginBottom: 'var(--s-3)' }}>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)',
            color: kbColor(), textTransform: 'uppercase', letterSpacing: '.08em',
            marginBottom: 6,
          }}>
            Mockup · sp4-kb-hub · #913
          </div>
          <h1 style={{ marginBottom: 'var(--s-2)' }}>KB Hub Overview</h1>
          <p className="lead">
            7 screen states: Hub default · Empty · Actions menu · Re-index modal ·
            RAPTOR rebuild (free + pro) · Coverage stats card · Delete dialog.
            Palette <strong>--c-kb</strong> (teal). Issue #913.
          </p>
        </div>

        {/* ── STATE 1: Hub Default ───────────────────── */}
        <div className="section-label">State 1 — Hub default (5 PDF rows · 4 status colors)</div>
        <HubDefault onRowAction={(pdf) => setSelectedPdf(pdf)} />

        {/* ── STATE 2: Empty State ───────────────────── */}
        <div className="section-label">State 2 — Empty state (no PDF indexed)</div>
        <EmptyState />

        {/* ── STATE 3: Row Actions Menu ─────────────── */}
        <div className="section-label">State 3 — PDF row actions menu (popover)</div>
        <div style={{ display: 'flex', gap: 32, alignItems: 'flex-start' }}>
          {/* Context: row that triggered popover */}
          <div style={{
            background: 'var(--bg-card)', border: `1px solid ${kbColor(0.15)}`,
            borderRadius: 'var(--r-xl)', overflow: 'hidden', flex: 1,
          }}>
            <PdfRow pdf={PDFS[0]} onAction={() => {}} />
          </div>
          <ActionsMenu pdf={PDFS[0]} onClose={() => {}} />
        </div>

        {/* ── STATE 4: Re-index Modal ───────────────── */}
        <div className="section-label">State 4 — Re-index modal (cost breakdown + async job)</div>
        <div style={{ background: 'var(--bg-sunken)', borderRadius: 'var(--r-xl)', padding: 32 }}>
          <ReindexModal onClose={() => {}} />
        </div>

        {/* ── STATE 5: RAPTOR Rebuild ───────────────── */}
        <div className="section-label">State 5 — RAPTOR rebuild: free tier (locked) + Pro tier (active)</div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 24 }}>
          <div>
            <div style={{
              fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
              textTransform: 'uppercase', letterSpacing: '.06em', marginBottom: 10,
            }}>Free tier — locked</div>
            <RaptorPanel tier="free" />
          </div>
          <div>
            <div style={{
              fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
              textTransform: 'uppercase', letterSpacing: '.06em', marginBottom: 10,
            }}>Pro tier — active</div>
            <RaptorPanel tier="pro" />
          </div>
        </div>

        {/* ── STATE 6: KB Coverage Stats Card ──────── */}
        <div className="section-label">State 6 — KB coverage stats card (reusable primitive)</div>
        <KbStatsCard stats={KB_STATS} />

        {/* ── STATE 7: Delete Dialog ────────────────── */}
        <div className="section-label">State 7 — Delete PDF confirmation dialog</div>
        <div style={{ background: 'var(--bg-sunken)', borderRadius: 'var(--r-xl)', padding: 32 }}>
          <DeleteDialog pdf={selectedPdf} onClose={() => {}} />
        </div>

        {/* ── Footer ───────────────────────────────── */}
        <div style={{
          marginTop: 'var(--s-10)', paddingTop: 'var(--s-6)',
          borderTop: '1px solid var(--border)',
          display: 'flex', alignItems: 'center', gap: 'var(--s-6)',
          fontSize: 'var(--fs-sm)', color: 'var(--text-muted)',
          fontFamily: 'var(--f-mono)',
        }}>
          <a href="00-hub.html" style={{ color: kbColor(), fontWeight: 700 }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-dashboard.html'; }, 0); /* DEMO-NAV */ }}>← 00-hub.html</a>
          <span>·</span>
          <span>sp4-kb-detail.jsx (single doc detail)</span>
          <span>·</span>
          <span>Issue #913 · SP4 wave 4</span>
          <span style={{ flex: 1 }} />
          <span style={{ fontSize: 10, opacity: .6 }}>MeepleAI DS v1 · 2026-05-09</span>
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
          50%       { opacity: .5; transform: scale(1.4); }
        }

        /* ── Responsive: mobile stacked PDF cards ─── */
        @media (max-width: 640px) {
          /* PDF rows become stacked cards */
          .pdf-table-row {
            grid-template-columns: 32px 1fr !important;
            grid-template-rows: auto auto;
          }
          .pdf-table-row .pdf-status,
          .pdf-table-row .pdf-date,
          .pdf-table-row .pdf-cta {
            grid-column: 2;
          }
          /* RAPTOR side-by-side → stacked */
          .raptor-grid {
            grid-template-columns: 1fr !important;
          }
          /* Stats card metrics: 2-col */
          .stats-metrics-grid {
            grid-template-columns: repeat(2, 1fr) !important;
          }
        }
      `}</style>
    </div>
  );
};

// ─── MOUNT ──────────────────────────────────────────────
const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(React.createElement(App));
