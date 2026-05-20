/* MeepleAI SP4 wave 3 — Schermata E / 5
   Route: /toolkits/[id]
   File: admin-mockups/design_files/sp4-toolkit-detail.{html,jsx}
   Pattern desktop: Split-view (sx summary+install CTA · dx tabs Overview/Agente/KB/Tools/Versioni/Recensioni).
   Palette --c-toolkit, cover gradient toolkit→game tint. Riuso pattern wave 1.

   Varianti OBBLIGATORIE: Public (visitatore) · Own (autore con edit + version controls).

   v2 nuovi (per impl post-merge):
   - ToolkitSummaryPanel    → apps/web/src/components/ui/v2/toolkit-summary-panel/
   - ToolkitIncludesGrid    → apps/web/src/components/ui/v2/toolkit-includes-grid/
   - VersionTimeline        → apps/web/src/components/ui/v2/version-timeline/
   - RatingBreakdown        → apps/web/src/components/ui/v2/rating-breakdown/
   - PromptPreviewBlock     → apps/web/src/components/ui/v2/prompt-preview-block/

   Riusi pixel-perfect:
   - ConnectionChipStrip footer (PR #542/#545)
   - PersonaCard (wave 2 agent-detail)
   - Tabs animated underline (wave 1)
   - PeekDrawer pattern (wave 1 KB list)

   Deviazioni flaggate: rating display "4,7" italian comma; "4.7" no per culture-independent → uso "4.7" punto.
*/

const { useState, useEffect, useRef } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.toolkit;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ─── TOOLKIT FIXTURES ────────────────────────────────
const TK_AZUL = {
  ...DS.toolkits.find(t => t.id === 'tk-azul-v2'),
  desc: 'Pacchetto completo per Azul: timer turno per giocatore, calcolo punteggi automatico (righe+colonne+set), mazzo tessere virtuale. Pensato per partite a 2-4 giocatori, riduce a zero il tempo di setup.',
  longDesc: 'Azul Toolkit v2 raccoglie i tre strumenti più usati durante una partita di Azul, integrati con l\'agente Azul Rules Expert per risposte rapide alle regole più ostiche. Il timer è configurabile (default 5:00 con warning a 30s); il counter punteggi applica automaticamente la formula righe+colonne+set di colore; il mazzo virtuale gestisce shuffle e pesca cieca.',
  agentId: 'a-azul-rules',
  toolIds: ['t-timer', 't-score-azul', 't-deck'],
  kbIds: ['kb-azul-ita', 'kb-azul-eng', 'kb-azul-faq'],
  rating: 4.7,
  ratingCount: 28,
  downloads: 142,
  license: 'CC BY-SA 4.0',
  size: '124 KB',
  updatedAt: '8 Mar 2026',
  publishedAt: '12 Gen 2026',
  reviews: [
    { id: 'rv1', authorId: 'p-sara', stars: 5, at: '2 set fa',
      text: 'Setup zero, perfetto per partite veloci la sera. Il calcolo bonus colore è il game-changer.' },
    { id: 'rv2', authorId: 'p-luca', stars: 5, at: '1 mese fa',
      text: 'Il timer per giocatore mi ha cambiato la vita — basta partite infinite con chi pensa troppo.' },
    { id: 'rv3', authorId: 'p-andrea', stars: 4, at: '1 mese fa',
      text: 'Mazzo virtuale ottimo. Manca un undo sul counter punteggi (lo segnalo).' },
    { id: 'rv4', authorId: 'p-giulia', stars: 5, at: '2 mesi fa',
      text: 'Combo agente + tools: ho imparato Azul in una sera. Consigliato a chi inizia.' },
  ],
  ratingHistogram: { 5: 22, 4: 4, 3: 1, 2: 1, 1: 0 },
  versions: [
    { v: '2.0.0', at: '8 Mar 2026', kind: 'major', notes: ['Aggiunto Mazzo Tessere virtuale', 'Counter ora calcola bonus set di colore', 'Refactor config timer (per-player toggle)'] },
    { v: '1.2.1', at: '14 Feb 2026', kind: 'patch', notes: ['Fix warning timer a 30s (era 60s)', 'Stringhe IT corrette nel counter'] },
    { v: '1.2.0', at: '28 Gen 2026', kind: 'minor', notes: ['Nuovo: avviso fine turno acustico', 'Migrazione a e5-base 768d per agente'] },
    { v: '1.0.0', at: '12 Gen 2026', kind: 'major', notes: ['Prima release pubblica', 'Timer + Counter punteggi'] },
  ],
};

// ═══════════════════════════════════════════════════════
// ─── PRIMITIVES ─────────────────────────────────────
// ═══════════════════════════════════════════════════════
const EntityChip = ({ type, label, icon, count, sm }) => (
  <span style={{
    display:'inline-flex', alignItems:'center', gap: 5,
    padding: sm ? '2px 8px' : '4px 10px',
    borderRadius:'var(--r-pill)',
    background: entityHsl(type, 0.12), color: entityHsl(type),
    border:`1px solid ${entityHsl(type, 0.2)}`,
    fontFamily:'var(--f-display)', fontSize: sm ? 10 : 11, fontWeight: 700,
    whiteSpace:'nowrap',
  }}>
    <span aria-hidden="true">{icon || DS.EC[type]?.em}</span>
    {count !== undefined && <span style={{ fontFamily:'var(--f-mono)' }}>{count}</span>}
    <span>{label}</span>
  </span>
);

const Stars = ({ value, size = 12 }) => {
  const full = Math.floor(value);
  const frac = value - full;
  return (
    <span style={{ display:'inline-flex', gap: 1 }} aria-label={`${value} stelle su 5`}>
      {[0,1,2,3,4].map(i => {
        const fill = i < full ? 1 : (i === full ? frac : 0);
        return (
          <span key={i} style={{ position:'relative', fontSize: size, lineHeight: 1 }}>
            <span style={{ color:'var(--border-strong)' }}>★</span>
            {fill > 0 && (
              <span style={{
                position:'absolute', left: 0, top: 0,
                width: `${fill * 100}%`, overflow:'hidden',
                color: entityHsl('agent'),
              }}>★</span>
            )}
          </span>
        );
      })}
    </span>
  );
};

// ═══════════════════════════════════════════════════════
// ─── CONNECTION CHIP STRIP (footer · PR #542/#545) ──
// ═══════════════════════════════════════════════════════
const ConnectionChipStrip = ({ chips, onClick }) => (
  <div role="navigation" aria-label="Contenuti collegati al toolkit" style={{
    display:'flex', alignItems:'center', flexWrap:'wrap', gap: 8,
    padding:'12px 14px',
    background:'var(--bg-card)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)',
  }}>
    <span style={{
      fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 800,
      marginRight: 4,
    }}>Bundled:</span>
    {chips.map((c, i) => {
      const empty = c.count === 0 || c.empty;
      return (
        <button key={i} type="button"
          onClick={() => onClick && onClick(c)}
          aria-label={`${c.label}: ${c.count}`}
          style={{
            display:'inline-flex', alignItems:'center', gap: 6,
            padding:'5px 11px', borderRadius:'var(--r-pill)',
            background: empty ? 'transparent' : entityHsl(c.entity, 0.1),
            color: entityHsl(c.entity),
            border: empty
              ? `1.5px dashed ${entityHsl(c.entity, 0.5)}`
              : `1px solid ${entityHsl(c.entity, 0.22)}`,
            fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
            cursor:'pointer', whiteSpace:'nowrap',
            opacity: empty ? 0.65 : 1,
          }}>
          <span aria-hidden="true">{DS.EC[c.entity].em}</span>
          {!empty && <span style={{ fontFamily:'var(--f-mono)', fontWeight: 800 }}>{c.count}</span>}
          <span>{c.label}</span>
        </button>
      );
    })}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── SUMMARY PANEL (sticky left) ─────────────────────
// /* v2: ToolkitSummaryPanel → apps/web/src/components/ui/v2/toolkit-summary-panel/ */
// ═══════════════════════════════════════════════════════
const SummaryPanel = ({ tk, mine, mobile, loading }) => {
  if (loading) {
    return (
      <div style={{ display:'flex', flexDirection:'column', gap: 12 }}>
        <div className="mai-shimmer" style={{ height: mobile ? 140 : 180,
          borderRadius:'var(--r-xl)', background:'var(--bg-muted)' }}/>
        <div className="mai-shimmer" style={{ height: 28, width:'80%',
          borderRadius: 6, background:'var(--bg-muted)' }}/>
        <div className="mai-shimmer" style={{ height: 16, width:'60%',
          borderRadius: 4, background:'var(--bg-muted)' }}/>
        <div className="mai-shimmer" style={{ height: 44, borderRadius:'var(--r-md)',
          background:'var(--bg-muted)' }}/>
      </div>
    );
  }

  const game = DS.byId[tk.gameId];
  const author = DS.byId[tk.owner];
  const cover = `linear-gradient(135deg, ${entityHsl('toolkit')} 0%, hsl(${DS.EC.toolkit.h - 10}, 60%, 35%) 50%, hsl(${(game?.id === 'g-azul') ? 200 : 30}, 65%, 45%) 100%)`;

  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 14 }}>
      {/* Cover gradient toolkit→game */}
      <div style={{
        height: mobile ? 140 : 180, borderRadius:'var(--r-xl)',
        background: cover, position:'relative', overflow:'hidden',
        border:`1px solid ${entityHsl('toolkit', 0.3)}`,
        boxShadow:`0 6px 20px ${entityHsl('toolkit', 0.25)}`,
      }}>
        <div style={{
          position:'absolute', inset: 0,
          background:'radial-gradient(circle at 75% 30%, rgba(255,255,255,.18), transparent 60%)',
        }}/>
        <div style={{
          position:'absolute', top: 12, left: 12,
          display:'flex', gap: 6,
        }}>
          <span style={{
            padding:'3px 9px', borderRadius:'var(--r-pill)',
            background:'rgba(0,0,0,.35)', color:'#fff',
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.08em',
            backdropFilter:'blur(4px)',
          }}>🧰 Toolkit</span>
          {tk.status === 'active' && (
            <span style={{
              padding:'3px 9px', borderRadius:'var(--r-pill)',
              background:'rgba(34, 187, 80, 0.85)', color:'#fff',
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform:'uppercase', letterSpacing:'.08em',
            }}>● Pubblicato</span>
          )}
        </div>
        <div style={{
          position:'absolute', bottom: 12, right: 14,
          fontFamily:'var(--f-mono)', fontSize: 10, color:'rgba(255,255,255,.85)',
          fontWeight: 700,
        }}>v{tk.version}.0.0</div>
        <div style={{
          position:'absolute', bottom: 12, left: 14, fontSize: 56, lineHeight: 1, opacity: 0.85,
        }} aria-hidden="true">🧰</div>
      </div>

      {/* Title */}
      <div>
        <h1 style={{
          fontFamily:'var(--f-display)', fontWeight: 800,
          fontSize: mobile ? 22 : 28, letterSpacing:'-.02em',
          color:'var(--text)', margin:'0 0 6px', lineHeight: 1.1,
        }}>{tk.title}</h1>
        <p style={{
          fontSize: 13.5, color:'var(--text-sec)', lineHeight: 1.55,
          margin: 0,
        }}>{tk.desc}</p>
      </div>

      {/* Author + game cross-refs */}
      <div style={{ display:'flex', flexWrap:'wrap', gap: 6 }}>
        {author && <EntityChip type="player" label={`Da ${author.title}`}/>}
        {game && <EntityChip type="game" label={game.title}/>}
      </div>

      {/* Stats row */}
      <div style={{
        display:'grid', gridTemplateColumns:'repeat(3, 1fr)', gap: 8,
        padding:'12px 0', borderTop:'1px solid var(--border-light)',
        borderBottom:'1px solid var(--border-light)',
      }}>
        <div>
          <div style={{ display:'flex', alignItems:'center', gap: 4 }}>
            <Stars value={tk.rating} size={11}/>
            <span style={{
              fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 13,
              color:'var(--text)', fontVariantNumeric:'tabular-nums',
            }}>{tk.rating.toFixed(1)}</span>
          </div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
            textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 700, marginTop: 2,
          }}>{tk.ratingCount} recensioni</div>
        </div>
        <div>
          <div style={{
            fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 16,
            color: entityHsl('toolkit'), fontVariantNumeric:'tabular-nums', lineHeight: 1,
          }}>{tk.downloads}</div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
            textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 700, marginTop: 4,
          }}>installazioni</div>
        </div>
        <div>
          <div style={{
            fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 16,
            color:'var(--text)', fontVariantNumeric:'tabular-nums', lineHeight: 1,
          }}>v{tk.version}.0</div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
            textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 700, marginTop: 4,
          }}>versione</div>
        </div>
      </div>

      {/* CTA */}
      <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
        {!mine ? (
          <>
            <button type="button" style={{
              padding:'12px 18px', borderRadius:'var(--r-md)',
              background: entityHsl('toolkit'), color:'#fff', border:'none',
              fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
              cursor:'pointer',
              boxShadow:`0 6px 18px ${entityHsl('toolkit', 0.4)}`,
              display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 8,
            }}><span aria-hidden="true">⬇</span>Installa toolkit</button>
            <button type="button" style={{
              padding:'10px 14px', borderRadius:'var(--r-md)',
              background:'transparent', color:'var(--text)',
              border:'1px solid var(--border-strong)',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
              cursor:'pointer',
              display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 8,
            }}><span aria-hidden="true">↗</span>Condividi</button>
          </>
        ) : (
          <>
            <button type="button" style={{
              padding:'12px 18px', borderRadius:'var(--r-md)',
              background: entityHsl('toolkit'), color:'#fff', border:'none',
              fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
              cursor:'pointer',
              boxShadow:`0 6px 18px ${entityHsl('toolkit', 0.4)}`,
              display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 8,
            }}><span aria-hidden="true">✎</span>Modifica toolkit</button>
            <button type="button" style={{
              padding:'10px 14px', borderRadius:'var(--r-md)',
              background:'var(--bg-card)', color:'var(--text)',
              border:`1px solid ${entityHsl('toolkit', 0.4)}`,
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
              cursor:'pointer',
              display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 8,
            }}><span aria-hidden="true">⬆</span>Pubblica nuova versione</button>
          </>
        )}
      </div>

      {/* Meta */}
      <dl style={{
        margin: 0, padding: 12,
        background:'var(--bg-card)', border:'1px solid var(--border)',
        borderRadius:'var(--r-lg)',
        display:'grid', gridTemplateColumns:'auto 1fr', rowGap: 7, columnGap: 12,
        fontSize: 11.5,
      }}>
        <dt style={{
          fontFamily:'var(--f-mono)', color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 700, fontSize: 9,
          alignSelf:'center',
        }}>Licenza</dt>
        <dd style={{ margin: 0, fontFamily:'var(--f-mono)', color:'var(--text)', fontWeight: 600 }}>{tk.license}</dd>
        <dt style={{
          fontFamily:'var(--f-mono)', color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 700, fontSize: 9,
          alignSelf:'center',
        }}>Aggiornato</dt>
        <dd style={{ margin: 0, fontFamily:'var(--f-mono)', color:'var(--text)', fontWeight: 600 }}>{tk.updatedAt}</dd>
        <dt style={{
          fontFamily:'var(--f-mono)', color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 700, fontSize: 9,
          alignSelf:'center',
        }}>Pubblicato</dt>
        <dd style={{ margin: 0, fontFamily:'var(--f-mono)', color:'var(--text)', fontWeight: 600 }}>{tk.publishedAt}</dd>
        <dt style={{
          fontFamily:'var(--f-mono)', color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 700, fontSize: 9,
          alignSelf:'center',
        }}>Dimensione</dt>
        <dd style={{ margin: 0, fontFamily:'var(--f-mono)', color:'var(--text)', fontWeight: 600 }}>{tk.size}</dd>
      </dl>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TABS BAR ───────────────────────────────────────
// ═══════════════════════════════════════════════════════
const TabsBar = ({ tabs, active, onChange, mine }) => {
  const refs = useRef({});
  const [ind, setInd] = useState({ left: 0, width: 0 });
  useEffect(() => {
    const el = refs.current[active];
    if (el) setInd({ left: el.offsetLeft, width: el.offsetWidth });
  }, [active]);
  return (
    <div className="mai-cb-scroll" style={{
      position:'relative',
      display:'flex', alignItems:'center', gap: 2,
      borderBottom:'1px solid var(--border)',
      overflowX:'auto', scrollbarWidth:'none',
    }} role="tablist" aria-label="Contenuti del toolkit">
      {tabs.map(t => {
        const isActive = t.id === active;
        return (
          <button key={t.id} type="button"
            ref={el => { refs.current[t.id] = el; }}
            onClick={() => onChange(t.id)}
            role="tab"
            aria-selected={isActive}
            aria-controls={`panel-${t.id}`}
            id={`tab-${t.id}`}
            style={{
              display:'inline-flex', alignItems:'center', gap: 7,
              padding:'12px 14px', background:'transparent', border:'none',
              color: isActive ? entityHsl('toolkit') : 'var(--text-sec)',
              fontFamily:'var(--f-display)', fontSize: 13,
              fontWeight: isActive ? 800 : 700,
              cursor:'pointer', whiteSpace:'nowrap', flexShrink: 0,
              transition:'color var(--dur-sm) var(--ease-out)',
            }}>
            <span aria-hidden="true">{t.icon}</span>
            <span>{t.label}{mine && t.id === 'versions' && ' · changelog'}</span>
            {t.count !== undefined && (
              <span style={{
                padding:'1px 7px', borderRadius:'var(--r-pill)',
                background: isActive ? entityHsl('toolkit') : 'var(--bg-muted)',
                color: isActive ? '#fff' : 'var(--text-muted)',
                fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              }}>{t.count}</span>
            )}
          </button>
        );
      })}
      <span aria-hidden="true" style={{
        position:'absolute', bottom: -1, left: ind.left, width: ind.width,
        height: 2, background: entityHsl('toolkit'),
        transition:'left .3s cubic-bezier(.4,0,.2,1), width .3s cubic-bezier(.4,0,.2,1)',
        borderRadius:'2px 2px 0 0',
      }}/>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TAB: OVERVIEW ──────────────────────────────────
// /* v2: ToolkitIncludesGrid → apps/web/src/components/ui/v2/toolkit-includes-grid/ */
// ═══════════════════════════════════════════════════════
const IncludesBox = ({ entity, count, label, sub, icon }) => (
  <div style={{
    padding: 14, background:'var(--bg-card)',
    border:`1px solid ${entityHsl(entity, 0.25)}`,
    borderRadius:'var(--r-lg)',
    display:'flex', flexDirection:'column', gap: 6,
  }}>
    <div style={{
      width: 38, height: 38, borderRadius:'var(--r-md)',
      background: entityHsl(entity, 0.14), color: entityHsl(entity),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 19,
    }} aria-hidden="true">{icon}</div>
    <div style={{
      fontFamily:'var(--f-display)', fontSize: 22, fontWeight: 800,
      color: entityHsl(entity), fontVariantNumeric:'tabular-nums', lineHeight: 1,
    }}>{count}</div>
    <div style={{
      fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
      color:'var(--text)',
    }}>{label}</div>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
      lineHeight: 1.4, fontWeight: 600,
    }}>{sub}</div>
  </div>
);

const OverviewTab = ({ tk }) => {
  const agent = DS.byId[tk.agentId];
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 16 }}>
      <div>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
          color:'var(--text)', margin:'0 0 8px',
        }}>Descrizione</h2>
        <p style={{
          fontSize: 14, lineHeight: 1.65, color:'var(--text)', margin: 0,
          textWrap:'pretty',
        }}>{tk.longDesc}</p>
      </div>

      <div>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
          color:'var(--text)', margin:'0 0 10px',
        }}>Cosa include</h2>
        <div style={{
          display:'grid', gridTemplateColumns:'repeat(auto-fit, minmax(180px, 1fr))', gap: 12,
        }}>
          <IncludesBox entity="agent" count={1} label="Agente"
            sub={agent?.title || 'Nessun agente'} icon="🤖"/>
          <IncludesBox entity="kb" count={tk.kbIds.length} label="KB docs"
            sub="Regole + FAQ indicizzate" icon="📄"/>
          <IncludesBox entity="tool" count={tk.toolIds.length} label="Strumenti"
            sub="Timer · counter · mazzo" icon="🔧"/>
        </div>
      </div>

      <div>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
          color:'var(--text)', margin:'0 0 10px',
        }}>Come usarlo</h2>
        <ol style={{
          margin: 0, paddingLeft: 18, fontSize: 13.5, lineHeight: 1.7,
          color:'var(--text-sec)',
        }}>
          <li>Premi <strong>Installa toolkit</strong> dalla colonna a sinistra.</li>
          <li>Apri una sessione di Azul: gli strumenti appaiono nel pannello laterale.</li>
          <li>Chiedi all'agente <em>Azul Rules Expert</em> dubbi sulle regole, citazioni dal manuale.</li>
          <li>Al termine partita, il counter calcola automaticamente i bonus colore.</li>
        </ol>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TAB: AGENTE ────────────────────────────────────
// /* v2: PromptPreviewBlock → apps/web/src/components/ui/v2/prompt-preview-block/ */
// ═══════════════════════════════════════════════════════
const AgentTab = ({ tk }) => {
  const agent = DS.byId[tk.agentId];
  if (!agent) return null;
  const game = DS.byId[agent.gameId];
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 14 }}>
      {/* PersonaCard riuso */}
      <div style={{
        padding: 16, background:'var(--bg-card)',
        border:`1px solid ${entityHsl('agent', 0.25)}`,
        borderRadius:'var(--r-xl)',
        display:'flex', gap: 14, alignItems:'flex-start',
      }}>
        <div style={{
          width: 64, height: 64, borderRadius:'var(--r-lg)',
          background:`linear-gradient(135deg, ${entityHsl('agent')}, hsl(${DS.EC.agent.h - 10}, 80%, 35%))`,
          color:'#fff',
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: 30, flexShrink: 0,
          boxShadow:`0 4px 14px ${entityHsl('agent', 0.4)}`,
        }} aria-hidden="true">🤖</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display:'flex', alignItems:'center', gap: 8, marginBottom: 4, flexWrap:'wrap' }}>
            <h3 style={{
              fontFamily:'var(--f-display)', fontSize: 18, fontWeight: 800,
              color:'var(--text)', margin: 0,
            }}>{agent.title}</h3>
            <EntityChip type="agent" label={agent.badge} sm/>
          </div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', fontWeight: 600,
            marginBottom: 8,
          }}>{agent.strategy} · {agent.model} · {game?.title}</div>
          <div style={{ display:'flex', flexWrap:'wrap', gap:6 }}>
            <EntityChip type="kb" label={`${agent.docs} KB docs`} sm/>
            <EntityChip type="chat" label={`${agent.invocations} invocazioni`} sm/>
            <span style={{
              padding:'2px 8px', borderRadius:'var(--r-pill)',
              background:'var(--bg-muted)', color:'var(--text-sec)',
              fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
            }}>⚡ {agent.avgLatency}</span>
          </div>
        </div>
        <button type="button" style={{
          padding:'8px 12px', borderRadius:'var(--r-md)',
          background:'transparent', color: entityHsl('agent'),
          border:`1px solid ${entityHsl('agent', 0.4)}`,
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
          flexShrink: 0,
        }}>Apri ↗</button>
      </div>

      {/* System prompt preview */}
      <div>
        <div style={{
          display:'flex', alignItems:'baseline', justifyContent:'space-between', marginBottom: 8,
        }}>
          <h3 style={{
            fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
            color:'var(--text)', margin: 0,
          }}>System prompt (preview)</h3>
          <span style={{
            fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 700,
          }}>342 token</span>
        </div>
        <pre style={{
          margin: 0, padding: 14,
          background:'var(--bg-sunken)',
          border:'1px solid var(--border)',
          borderRadius:'var(--r-lg)',
          fontFamily:'var(--f-mono)', fontSize: 11.5, lineHeight: 1.65,
          color:'var(--text)', whiteSpace:'pre-wrap', overflow:'auto', maxHeight: 220,
        }}>{`Sei "Azul Rules Expert", un assistente per le regole di Azul.
Rispondi sempre in italiano, in modo conciso e citando la pagina del manuale.

# Comportamento
- Quando l'utente fa una domanda sulle regole, recupera il chunk più rilevante
  dal Knowledge Base (azul-regole-ita.pdf, azul-faq.md).
- Cita la pagina con il formato [p. N] alla fine della risposta.
- Se la domanda è ambigua, fai una sola domanda di chiarimento.

# Limiti
- Non inventare regole non presenti nel manuale.
- Non dare consigli strategici a meno che l'utente non li chieda esplicitamente.`}</pre>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TAB: KB ────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const KbTab = ({ tk }) => {
  const [openId, setOpenId] = useState(null);
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
      {tk.kbIds.map(kbId => {
        const kb = DS.byId[kbId];
        if (!kb) return null;
        const isOpen = openId === kbId;
        return (
          <article key={kbId} style={{
            background:'var(--bg-card)', border:'1px solid var(--border)',
            borderRadius:'var(--r-lg)', overflow:'hidden',
          }}>
            <button type="button" onClick={() => setOpenId(isOpen ? null : kbId)}
              aria-expanded={isOpen} aria-controls={`kb-peek-${kbId}`}
              style={{
                width:'100%', padding: 14,
                display:'flex', alignItems:'center', gap: 12,
                background:'transparent', border:'none', cursor:'pointer',
                textAlign:'left',
              }}>
              <div style={{
                width: 38, height: 46, borderRadius:'var(--r-sm)',
                background: entityHsl('kb', 0.16), color: entityHsl('kb'),
                display:'flex', alignItems:'center', justifyContent:'center',
                fontSize: 18, flexShrink: 0,
              }} aria-hidden="true">📄</div>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{
                  fontFamily:'var(--f-mono)', fontSize: 13, fontWeight: 700,
                  color:'var(--text)', marginBottom: 3,
                }}>{kb.title}</div>
                <div style={{
                  fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 600,
                }}>{kb.pages} pag · {kb.size} · {kb.chunks} chunks · {kb.embedding}</div>
              </div>
              <EntityChip type="kb" label={kb.badge} sm/>
              <span aria-hidden="true" style={{
                color:'var(--text-muted)', fontSize: 16,
                transform: isOpen ? 'rotate(180deg)' : 'none',
                transition:'transform var(--dur-sm) var(--ease-out)',
              }}>⌄</span>
            </button>
            {isOpen && (
              <div id={`kb-peek-${kbId}`} style={{
                padding:'0 14px 14px', borderTop:'1px solid var(--border-light)',
                animation:'slideDown .25s var(--ease-out)',
              }}>
                <div style={{
                  fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
                  textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 800,
                  marginTop: 12, marginBottom: 6,
                }}>Anteprima primo chunk</div>
                <p style={{
                  fontSize: 12.5, color:'var(--text-sec)', margin: 0, lineHeight: 1.6,
                  fontStyle:'italic',
                  padding: 10, background:'var(--bg-sunken)', borderRadius:'var(--r-sm)',
                }}>"In Azul, i giocatori si alternano nel raccogliere tessere colorate dalle factory display al loro tabellone personale. Ogni colore raccolto va posizionato in una riga del pattern strip. A fine round, le tessere completate vengono mosse sul muro..."</p>
              </div>
            )}
          </article>
        );
      })}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TAB: TOOLS ─────────────────────────────────────
// ═══════════════════════════════════════════════════════
const ToolsTab = ({ tk }) => {
  const [openId, setOpenId] = useState(null);
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
      {tk.toolIds.map(toolId => {
        const t = DS.byId[toolId];
        if (!t) return null;
        const isOpen = openId === toolId;
        return (
          <article key={toolId} style={{
            background:'var(--bg-card)', border:'1px solid var(--border)',
            borderRadius:'var(--r-lg)', overflow:'hidden',
          }}>
            <button type="button" onClick={() => setOpenId(isOpen ? null : toolId)}
              aria-expanded={isOpen} aria-controls={`tool-peek-${toolId}`}
              style={{
                width:'100%', padding: 14,
                display:'flex', alignItems:'center', gap: 12,
                background:'transparent', border:'none', cursor:'pointer',
                textAlign:'left',
              }}>
              <div style={{
                width: 42, height: 42, borderRadius:'var(--r-md)',
                background: entityHsl('tool', 0.14), color: entityHsl('tool'),
                display:'flex', alignItems:'center', justifyContent:'center',
                fontSize: 20, flexShrink: 0,
              }} aria-hidden="true">{t.coverEmoji}</div>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{
                  fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800,
                  color:'var(--text)', marginBottom: 3,
                }}>{t.title}</div>
                <div style={{
                  fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 600,
                }}>kind={t.kind} · {t.uses} usi totali</div>
              </div>
              <EntityChip type="tool" label={t.kind} sm/>
              <span aria-hidden="true" style={{
                color:'var(--text-muted)', fontSize: 16,
                transform: isOpen ? 'rotate(180deg)' : 'none',
                transition:'transform var(--dur-sm) var(--ease-out)',
              }}>⌄</span>
            </button>
            {isOpen && (
              <div id={`tool-peek-${toolId}`} style={{
                padding:'12px 14px 14px', borderTop:'1px solid var(--border-light)',
              }}>
                <div style={{
                  fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
                  textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 800,
                  marginBottom: 6,
                }}>Configurazione</div>
                <pre style={{
                  margin: 0, padding: 10,
                  background:'var(--bg-sunken)',
                  border:'1px solid var(--border-light)',
                  borderRadius:'var(--r-sm)',
                  fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text)',
                  overflow:'auto',
                }}>{JSON.stringify(t.config, null, 2)}</pre>
              </div>
            )}
          </article>
        );
      })}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TAB: VERSIONS ──────────────────────────────────
// /* v2: VersionTimeline → apps/web/src/components/ui/v2/version-timeline/ */
// ═══════════════════════════════════════════════════════
const VersionsTab = ({ tk, mine }) => {
  const kindColor = {
    major: entityHsl('event'),
    minor: entityHsl('toolkit'),
    patch: entityHsl('text-muted'),
  };
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 0 }}>
      {tk.versions.map((v, i) => {
        const isLatest = i === 0;
        const dotColor = v.kind === 'major' ? entityHsl('event')
          : v.kind === 'minor' ? entityHsl('toolkit')
          : 'var(--text-muted)';
        return (
          <div key={v.v} style={{
            display:'flex', gap: 14, position:'relative',
            paddingBottom: i === tk.versions.length - 1 ? 0 : 14,
          }}>
            {/* Timeline rail */}
            <div style={{
              width: 14, flexShrink: 0, position:'relative',
              display:'flex', justifyContent:'center', paddingTop: 6,
            }}>
              {i < tk.versions.length - 1 && (
                <div style={{
                  position:'absolute', top: 18, bottom: -14, left:'50%',
                  width: 2, background:'var(--border)', transform:'translateX(-50%)',
                }}/>
              )}
              <div style={{
                width: 14, height: 14, borderRadius:'50%',
                background: dotColor, color:'#fff',
                border: isLatest ? `3px solid ${entityHsl('toolkit', 0.3)}` : 'none',
                boxShadow: isLatest ? `0 0 0 4px ${entityHsl('toolkit', 0.1)}` : 'none',
                position:'relative', zIndex: 1,
              }}/>
            </div>

            <div style={{
              flex: 1, padding: 14,
              background:'var(--bg-card)', border:'1px solid var(--border)',
              borderRadius:'var(--r-lg)',
            }}>
              <div style={{
                display:'flex', alignItems:'center', gap: 8, marginBottom: 4,
                flexWrap:'wrap',
              }}>
                <h3 style={{
                  fontFamily:'var(--f-mono)', fontSize: 14, fontWeight: 800,
                  color:'var(--text)', margin: 0,
                }}>v{v.v}</h3>
                <span style={{
                  padding:'2px 8px', borderRadius:'var(--r-pill)',
                  background: `${dotColor}1f`, color: dotColor,
                  fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
                  textTransform:'uppercase', letterSpacing:'.08em',
                }}>{v.kind}</span>
                {isLatest && (
                  <span style={{
                    padding:'2px 8px', borderRadius:'var(--r-pill)',
                    background: entityHsl('toolkit', 0.14), color: entityHsl('toolkit'),
                    fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
                    textTransform:'uppercase', letterSpacing:'.08em',
                  }}>● Corrente</span>
                )}
                <span style={{
                  fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
                  marginLeft:'auto',
                }}>{v.at}</span>
              </div>
              <ul style={{
                margin: 0, paddingLeft: 16, fontSize: 12.5, lineHeight: 1.6,
                color:'var(--text-sec)',
              }}>
                {v.notes.map((n, j) => <li key={j}>{n}</li>)}
              </ul>
              {mine && isLatest && (
                <div style={{ display:'flex', gap: 6, marginTop: 10 }}>
                  <button type="button" style={{
                    padding:'5px 10px', borderRadius:'var(--r-sm)',
                    background:'transparent', color:'var(--text-sec)',
                    border:'1px solid var(--border)',
                    fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700, cursor:'pointer',
                  }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-hub-toolkits.html'; }, 0); /* DEMO-NAV */ }}>✎ Modifica note</button>
                  <button type="button" style={{
                    padding:'5px 10px', borderRadius:'var(--r-sm)',
                    background:'transparent', color: entityHsl('warning'),
                    border:`1px solid ${entityHsl('warning', 0.4)}`,
                    fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700, cursor:'pointer',
                  }}>⊘ Yank versione</button>
                </div>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TAB: REVIEWS ───────────────────────────────────
// /* v2: RatingBreakdown → apps/web/src/components/ui/v2/rating-breakdown/ */
// ═══════════════════════════════════════════════════════
const ReviewsTab = ({ tk }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 16 }}>
    {/* Breakdown */}
    <div style={{
      padding: 16, background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
      display:'grid', gridTemplateColumns:'auto 1fr', gap: 24, alignItems:'center',
    }}>
      <div style={{ textAlign:'center' }}>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 42, fontWeight: 800,
          color:'var(--text)', lineHeight: 1, fontVariantNumeric:'tabular-nums',
        }}>{tk.rating.toFixed(1)}</div>
        <div style={{ marginTop: 4 }}><Stars value={tk.rating} size={14}/></div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          marginTop: 4, fontWeight: 700,
        }}>{tk.ratingCount} recensioni</div>
      </div>
      <div style={{ display:'flex', flexDirection:'column', gap: 5 }}>
        {[5,4,3,2,1].map(s => {
          const n = tk.ratingHistogram[s] || 0;
          const pct = (n / tk.ratingCount) * 100;
          return (
            <div key={s} style={{ display:'flex', alignItems:'center', gap: 8 }}>
              <span style={{
                fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
                width: 20, fontWeight: 700,
              }}>{s}★</span>
              <div style={{
                flex: 1, height: 8, borderRadius:'var(--r-pill)',
                background:'var(--bg-muted)', overflow:'hidden',
              }}>
                <div style={{
                  height:'100%', width: `${pct}%`,
                  background: entityHsl('agent'),
                  borderRadius:'var(--r-pill)',
                }}/>
              </div>
              <span style={{
                fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
                width: 28, textAlign:'right', fontWeight: 700,
              }}>{n}</span>
            </div>
          );
        })}
      </div>
    </div>

    {/* Reviews list */}
    <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
      {tk.reviews.map(r => {
        const author = DS.byId[r.authorId];
        return (
          <article key={r.id} style={{
            padding: 14, background:'var(--bg-card)', border:'1px solid var(--border)',
            borderRadius:'var(--r-lg)',
          }}>
            <div style={{ display:'flex', alignItems:'center', gap: 10, marginBottom: 6 }}>
              <div style={{
                width: 32, height: 32, borderRadius:'50%',
                background: `hsl(${author?.color || 200}, 60%, 55%)`, color:'#fff',
                display:'flex', alignItems:'center', justifyContent:'center',
                fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 11,
              }} aria-hidden="true">{author?.initials || '?'}</div>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{
                  fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
                  color:'var(--text)',
                }}>{author?.title || 'Anonimo'}</div>
                <div style={{
                  display:'flex', alignItems:'center', gap: 8,
                  fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
                  fontWeight: 600,
                }}>
                  <Stars value={r.stars} size={10}/>
                  <span>{r.at}</span>
                </div>
              </div>
            </div>
            <p style={{
              margin: 0, fontSize: 13, lineHeight: 1.6, color:'var(--text)',
            }}>{r.text}</p>
          </article>
        );
      })}
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ERROR / LOADING / EMPTY ────────────────────────
// ═══════════════════════════════════════════════════════
const TabSkeleton = () => (
  <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
    {[0,1,2].map(i => (
      <div key={i} className="mai-shimmer" style={{
        height: 80, borderRadius:'var(--r-lg)', background:'var(--bg-muted)',
      }}/>
    ))}
  </div>
);
const TabError = () => (
  <div style={{
    padding:'32px 24px', textAlign:'center',
    background:'hsl(var(--c-danger) / .08)',
    border:'1px solid hsl(var(--c-danger) / .3)',
    borderRadius:'var(--r-xl)',
    display:'flex', flexDirection:'column', alignItems:'center',
  }}>
    <div style={{
      width: 48, height: 48, borderRadius:'50%',
      background:'hsl(var(--c-danger) / .15)', color:'hsl(var(--c-danger))',
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 22, marginBottom: 10,
    }} aria-hidden="true">⚠</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
      color:'var(--text)', margin:'0 0 4px',
    }}>Errore caricamento toolkit</h3>
    <p style={{ fontSize: 12, color:'var(--text-muted)', margin:'0 0 12px' }}>
      Impossibile recuperare il bundle. Riprova fra qualche secondo.
    </p>
    <button type="button" style={{
      padding:'7px 14px', borderRadius:'var(--r-md)',
      background:'hsl(var(--c-danger))', color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
    }}>↻ Riprova</button>
  </div>
);
const TabEmpty = ({ title, sub, cta, accent = 'toolkit', icon }) => (
  <div style={{
    padding:'40px 24px', textAlign:'center',
    background:'var(--bg-card)', border:'1px dashed var(--border-strong)',
    borderRadius:'var(--r-xl)',
    display:'flex', flexDirection:'column', alignItems:'center',
  }}>
    <div style={{
      width: 60, height: 60, borderRadius:'50%',
      background: entityHsl(accent, 0.12), color: entityHsl(accent),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 26, marginBottom: 12,
    }} aria-hidden="true">{icon}</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
      color:'var(--text)', margin:'0 0 4px',
    }}>{title}</h3>
    <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 14px', maxWidth: 340 }}>{sub}</p>
    {cta && (
      <button type="button" style={{
        padding:'8px 16px', borderRadius:'var(--r-md)',
        background: entityHsl(accent), color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
        boxShadow:`0 3px 10px ${entityHsl(accent, 0.35)}`,
      }}>{cta}</button>
    )}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── BODY ───────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const ToolkitDetailBody = ({ stateOverride, mine = false, mobile, initialTab = 'overview' }) => {
  const [tab, setTab] = useState(initialTab);
  const tk = TK_AZUL;

  const isLoading = stateOverride === 'loading';
  const isError = stateOverride === 'error';
  const isEmpty = stateOverride === 'empty';

  // Empty toolkit fixture
  const emptyTk = isEmpty ? {
    ...tk, agentId: null, toolIds: [], kbIds: [],
    rating: 0, ratingCount: 0, downloads: 0,
    reviews: [], ratingHistogram: { 5:0,4:0,3:0,2:0,1:0 },
    versions: [{ v:'0.1.0', at:'8 Mar 2026', kind:'patch', notes:['Toolkit creato — non ancora popolato'] }],
    desc: 'Toolkit appena creato. Aggiungi agente, KB docs e strumenti per pubblicarlo.',
    longDesc: 'Toolkit appena creato. Aggiungi agente, KB docs e strumenti per pubblicarlo.',
    status: 'setup',
    version: 0,
  } : null;

  const activeTk = isEmpty ? emptyTk : tk;

  const tabs = [
    { id:'overview', icon:'📋', label:'Overview' },
    { id:'agent', icon:'🤖', label:'Agente' },
    { id:'kb', icon:'📄', label:'KB', count: activeTk.kbIds.length },
    { id:'tools', icon:'🔧', label:'Tools', count: activeTk.toolIds.length },
    { id:'versions', icon:'📌', label:'Versioni', count: activeTk.versions.length },
    { id:'reviews', icon:'⭐', label:'Recensioni', count: activeTk.ratingCount },
  ];

  const chips = [
    { entity:'agent', count: activeTk.agentId ? 1 : 0, label: activeTk.agentId ? 'Agente' : 'Nessun agente', empty: !activeTk.agentId },
    { entity:'kb', count: activeTk.kbIds.length, label:'KB docs' },
    { entity:'tool', count: activeTk.toolIds.length, label:'Strumenti' },
    { entity:'game', count: 1, label: DS.byId[activeTk.gameId]?.title || 'Game' },
    { entity:'player', count: 1, label:'Autore' },
    { entity:'session', count: activeTk.useCount, label:'Usi totali' },
  ];

  const renderTab = () => {
    if (isLoading) return <TabSkeleton/>;
    if (isError) return <TabError/>;
    if (isEmpty && tab === 'overview') {
      return (
        <div style={{ display:'flex', flexDirection:'column', gap: 14 }}>
          <p style={{ fontSize: 13.5, color:'var(--text-sec)', lineHeight: 1.6, margin: 0 }}>
            {activeTk.longDesc}
          </p>
          <TabEmpty title="Nessun contenuto bundled"
            sub="Aggiungi un agente, almeno una KB e uno strumento per poter pubblicare la prima versione."
            cta="+ Aggiungi contenuti" icon="🧰"/>
        </div>
      );
    }
    if (isEmpty && tab === 'agent') return <TabEmpty title="Nessun agente collegato"
      sub="Collega un agente esistente o creane uno nuovo per questo toolkit."
      cta="+ Collega agente" accent="agent" icon="🤖"/>;
    if (isEmpty && tab === 'kb') return <TabEmpty title="Nessun documento KB"
      sub="Carica almeno un manuale o FAQ in formato PDF/Markdown."
      cta="+ Carica documento" accent="kb" icon="📄"/>;
    if (isEmpty && tab === 'tools') return <TabEmpty title="Nessuno strumento"
      sub="Aggiungi timer, counter, mazzi o tracker."
      cta="+ Aggiungi strumento" accent="tool" icon="🔧"/>;
    if (isEmpty && tab === 'reviews') return <TabEmpty title="Nessuna recensione"
      sub="Le recensioni appariranno dopo la prima pubblicazione." accent="agent" icon="⭐"/>;

    if (tab === 'overview') return <OverviewTab tk={activeTk}/>;
    if (tab === 'agent') return <AgentTab tk={activeTk}/>;
    if (tab === 'kb') return <KbTab tk={activeTk}/>;
    if (tab === 'tools') return <ToolsTab tk={activeTk}/>;
    if (tab === 'versions') return <VersionsTab tk={activeTk} mine={mine}/>;
    if (tab === 'reviews') return <ReviewsTab tk={activeTk}/>;
    return null;
  };

  if (mobile) {
    // Mobile: stacked, summary first then tabs
    return (
      <>
        <div style={{ padding:'14px 16px', borderBottom:'1px solid var(--border-light)' }}>
          <SummaryPanel tk={activeTk} mine={mine} mobile loading={isLoading}/>
        </div>
        <div style={{
          position:'sticky', top: 0, zIndex: 7,
          background:'var(--glass-bg)', backdropFilter:'blur(12px)',
          padding:'0 16px',
        }}>
          <TabsBar tabs={tabs} active={tab} onChange={setTab} mine={mine}/>
        </div>
        <div role="tabpanel" id={`panel-${tab}`} aria-labelledby={`tab-${tab}`}
          style={{ padding:'14px 16px 14px' }}>
          {renderTab()}
        </div>
        <div style={{ padding:'0 16px 24px' }}>
          <ConnectionChipStrip chips={chips}/>
        </div>
      </>
    );
  }

  // Desktop split-view
  return (
    <div style={{ display:'flex', flexDirection:'column', minHeight: 720 }}>
      <div style={{
        display:'grid', gridTemplateColumns:'380px 1fr', gap: 24,
        padding:'24px 32px', flex: 1,
        background:'var(--bg)',
      }}>
        <aside style={{ position:'sticky', top: 24, alignSelf:'start', maxHeight:'calc(100vh - 48px)', overflowY:'auto' }}>
          <SummaryPanel tk={activeTk} mine={mine} loading={isLoading}/>
        </aside>
        <main style={{ minWidth: 0 }}>
          <div style={{
            position:'sticky', top: 0, zIndex: 7,
            background:'var(--glass-bg)', backdropFilter:'blur(12px)',
            marginBottom: 18,
          }}>
            <TabsBar tabs={tabs} active={tab} onChange={setTab} mine={mine}/>
          </div>
          <div role="tabpanel" id={`panel-${tab}`} aria-labelledby={`tab-${tab}`}>
            {renderTab()}
          </div>
        </main>
      </div>
      {/* ConnectionChipStrip footer */}
      <footer style={{
        padding:'16px 32px 24px', background:'var(--bg)',
        borderTop:'1px solid var(--border)',
      }}>
        <ConnectionChipStrip chips={chips}/>
      </footer>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SHELLS ─────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopNav = ({ tk, mine }) => (
  <div style={{
    display:'flex', alignItems:'center', gap: 14,
    padding:'10px 32px',
    background:'var(--glass-bg)', backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border)',
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
      <div style={{
        width: 26, height: 26, borderRadius: 7,
        background:'linear-gradient(135deg, hsl(var(--c-toolkit)), hsl(var(--c-game)))',
        color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
        fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)',
      }}>M</div>
      <span style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 14 }}>MeepleAI</span>
    </div>
    <div style={{
      flex: 1, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      marginLeft: 22,
    }}>
      <a href="#" style={{ color:'inherit' }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-hub-toolkits.html'; }, 0); /* DEMO-NAV */ }}>Toolkits</a>
      <span aria-hidden="true"> / </span>
      <strong style={{ color:'var(--text-sec)' }}>{tk.title}</strong>
      {mine && (
        <span style={{
          marginLeft: 10, padding:'2px 7px', borderRadius:'var(--r-pill)',
          background: entityHsl('toolkit', 0.14), color: entityHsl('toolkit'),
          fontSize: 9, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em',
        }}>● Tuo</span>
      )}
    </div>
  </div>
);

const DesktopScreen = ({ stateOverride, mine, initialTab }) => (
  <div style={{ background:'var(--bg)' }}>
    <DesktopNav tk={TK_AZUL} mine={mine}/>
    <ToolkitDetailBody stateOverride={stateOverride} mine={mine} initialTab={initialTab}/>
  </div>
);

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);
const PhoneTopNav = ({ tk, mine }) => (
  <div style={{
    display:'flex', alignItems:'center', gap: 9,
    padding:'10px 14px', background:'var(--glass-bg)',
    backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border-light)',
  }}>
    <button aria-label="Indietro" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>←</button>
    <div style={{
      flex: 1, fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
      textAlign:'center', fontWeight: 700,
    }}>toolkits / {tk.id.replace('tk-', '')}{mine && ' · tuo'}</div>
    <button aria-label="Più opzioni" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>⋯</button>
  </div>
);
const MobileScreen = ({ stateOverride, mine, initialTab }) => (
  <>
    <PhoneSbar/>
    <div style={{ flex: 1, overflowY:'auto', position:'relative', background:'var(--bg)' }}>
      <PhoneTopNav tk={TK_AZUL} mine={mine}/>
      <ToolkitDetailBody stateOverride={stateOverride} mine={mine} mobile initialTab={initialTab}/>
    </div>
  </>
);

const PhoneShell = ({ label, desc, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 10 }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div className="phone">{children}</div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 340,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

const DesktopFrame = ({ label, desc, children, mine }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width:'100%', maxWidth: 1440,
      borderRadius:'var(--r-xl)', border:'1px solid var(--border)',
      background:'var(--bg)', overflow:'hidden',
      boxShadow:'var(--shadow-lg)',
    }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        padding:'9px 14px', background:'var(--bg-muted)',
        borderBottom:'1px solid var(--border)',
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      }}>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-event))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-warning))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-toolkit))' }}/>
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/toolkits/{TK_AZUL.id.replace('tk-', '')}{mine ? '/edit' : ''}</span>
      </div>
      {children}
    </div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 720,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ROOT ───────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const MOBILE_STATES = [
  { id:'m1', stateOverride:null, mine:false, tab:'overview',
    label:'01 · Public · Overview',
    desc:'Summary panel stacked: cover gradient toolkit→game (verde→azul-blue) 140px, stats triplet + CTA "Installa". Body Includes 3-box (Agent/KB/Tools).' },
  { id:'m2', stateOverride:null, mine:false, tab:'tools',
    label:'02 · Public · Tools peek',
    desc:'Lista tools con peek drawer. Click chevron espande JSON config in <pre> sunken bg.' },
  { id:'m3', stateOverride:null, mine:false, tab:'reviews',
    label:'03 · Public · Recensioni',
    desc:'Rating breakdown 4.7/5 + histogram bars + lista recensioni con avatar player gradient + Stars component.' },
  { id:'m4', stateOverride:null, mine:true, tab:'versions',
    label:'04 · Own · Versioni · changelog',
    desc:'Variante autore: tab label estesa "Versioni · changelog". Timeline rail con dot color-coded major/minor/patch + CTA "✎ Modifica note" / "⊘ Yank" su corrente.' },
  { id:'m5', stateOverride:'empty', mine:true, tab:'overview',
    label:'05 · Own · Empty (toolkit nuovo)',
    desc:'Toolkit appena creato dall\'autore. Stats 0/0/v0, CTA "+ Aggiungi contenuti" toolkit-green nel body.' },
  { id:'m6', stateOverride:'loading', mine:false, tab:'overview',
    label:'06 · Loading',
    desc:'Summary panel shimmer (cover + 3 lines + CTA placeholder). Body 3 card skeleton.' },
  { id:'m7', stateOverride:'error', mine:false, tab:'overview',
    label:'07 · Error',
    desc:'Summary OK (cache), tab body TabError pattern: alert danger + CTA "↻ Riprova".' },
];

const App = () => {
  const [theme, setTheme] = useState(() => {
    const saved = localStorage.getItem('mai-theme');
    if (saved) return saved;
    return document.documentElement.getAttribute('data-theme') || 'light';
  });
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('mai-theme', theme);
  }, [theme]);

  return (
    <div style={{
      minHeight:'100vh', background:'var(--bg)', color:'var(--text)',
      padding:'24px 24px 80px',
    }}>
      {/* Header */}
      <header style={{
        maxWidth: 1440, margin:'0 auto 32px',
        display:'flex', alignItems:'center', gap: 12, flexWrap:'wrap',
      }}>
        <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
          <div style={{
            width: 32, height: 32, borderRadius: 8,
            background:'linear-gradient(135deg, hsl(var(--c-toolkit)), hsl(var(--c-game)))',
            color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
            fontWeight: 800, fontFamily:'var(--f-display)',
          }}>E</div>
          <div>
            <div style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 16 }}>SP4 wave 3 · E</div>
            <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)' }}>
              /toolkits/[id] — Toolkit detail (split-view)
            </div>
          </div>
        </div>
        <div style={{ flex: 1 }}/>
        <button type="button" onClick={(e) => { (() => setTheme(t => t === 'light' ? 'dark' : 'light'))(e); setTimeout(() => { window.location.href = 'sp4-hub-toolkits.html'; }, 0); /* DEMO-NAV */ }}
          style={{
            padding:'8px 14px', borderRadius:'var(--r-md)',
            background:'var(--bg-card)', border:'1px solid var(--border)',
            color:'var(--text)', fontFamily:'var(--f-display)',
            fontSize: 12, fontWeight: 800, cursor:'pointer',
          }}>🌗 {theme === 'light' ? 'Light' : 'Dark'}</button>
      </header>

      {/* Desktop variants */}
      <section style={{ maxWidth: 1440, margin:'0 auto', display:'flex', flexDirection:'column', gap: 32 }}>
        <DesktopFrame label="Desktop · 01 · Public · Overview"
          desc="Split-view 380px sx (sticky cover gradient + descrizione + autore/game chip + stats triplet rating/downloads/version + CTA primario 'Installa toolkit' + meta) / fluida dx (tabs animated underline → Overview con Includes 3-box). Footer ConnectionChipStrip 6 pip.">
          <DesktopScreen mine={false} initialTab="overview"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 02 · Public · Agente · system prompt"
          desc="Tab Agente: PersonaCard 64px gradient + chip strategy/model + open ↗ + system prompt preview in <pre> mono sunken background con token count.">
          <DesktopScreen mine={false} initialTab="agent"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 03 · Public · KB peek drawer"
          desc="Tab KB: lista 3 doc Azul. Click → peek drawer espande chunk preview corsivo italic. aria-expanded/aria-controls correttamente cablati.">
          <DesktopScreen mine={false} initialTab="kb"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 04 · Public · Tools peek"
          desc="Tab Tools: 3 tool con kind chip (timer/counter/deck) + uses count. Peek mostra config JSON in <pre> mono.">
          <DesktopScreen mine={false} initialTab="tools"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 05 · Public · Versioni"
          desc="Public view: lista release timeline con dot color-coded (major event-pink / minor toolkit-green / patch muted). Pill 'Corrente' su latest. Nessuna CTA edit/yank.">
          <DesktopScreen mine={false} initialTab="versions"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 06 · Public · Recensioni"
          desc="Rating breakdown layout 2-col: numero grande 4.7 + Stars + count / histogram bars 5→1 stelle agent-yellow. Lista 4 recensioni con avatar gradient.">
          <DesktopScreen mine={false} initialTab="reviews"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 07 · Own · Modifica + Pubblica nuova versione" mine
          desc="Variante autore: breadcrumb pill '● Tuo' toolkit-green; CTA primario 'Modifica toolkit', secondario 'Pubblica nuova versione'. Tab Versioni mostra changelog timeline + CTA '✎ Modifica note' / '⊘ Yank' su versione corrente.">
          <DesktopScreen mine={true} initialTab="versions"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 08 · Own · Empty (toolkit appena creato)" mine
          desc="Stats panel: rating 0/0, downloads 0, v0. ConnectionChipStrip mostra agent/kb/tools con stato empty (dashed). Tab Overview body: TabEmpty CTA '+ Aggiungi contenuti'.">
          <DesktopScreen mine={true} initialTab="overview" stateOverride="empty"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 09 · Loading"
          desc="Summary panel shimmer: cover 180px + title + 3 stat skeleton + CTA placeholder. Body 3 card skeleton.">
          <DesktopScreen mine={false} initialTab="overview" stateOverride="loading"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 10 · Error"
          desc="Summary panel mantiene cache. Body tab mostra TabError pattern con CTA '↻ Riprova'. Footer ConnectionChipStrip resta visibile.">
          <DesktopScreen mine={false} initialTab="overview" stateOverride="error"/>
        </DesktopFrame>
      </section>

      {/* Mobile variants */}
      <section style={{ maxWidth: 1440, margin:'48px auto 0' }}>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 22, fontWeight: 800,
          marginBottom: 8,
        }}>Mobile · 375px</h2>
        <p style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-muted)', marginBottom: 24,
        }}>7 stati: public (overview, tools, reviews) · own (versions, empty) · loading · error</p>
        <div style={{
          display:'flex', flexWrap:'wrap', gap: 32, justifyContent:'center',
        }}>
          {MOBILE_STATES.map(m => (
            <PhoneShell key={m.id} label={m.label} desc={m.desc}>
              <MobileScreen stateOverride={m.stateOverride} mine={m.mine} initialTab={m.tab}/>
            </PhoneShell>
          ))}
        </div>
      </section>

      {/* DoD checklist */}
      <section style={{
        maxWidth: 900, margin:'64px auto 0',
        padding: 24, borderRadius:'var(--r-xl)',
        background:'var(--bg-card)', border:'1px solid var(--border)',
      }}>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 18, fontWeight: 800, marginBottom: 12,
        }}>Definition of Done · E toolkit-detail</h2>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.9, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>✓ Solo CSS variables da tokens.css (zero hex hardcoded)</li>
          <li>✓ Helper entityHsl(type, alpha) inline · 9 entity colors</li>
          <li>✓ Light + dark mode entrambi funzionanti (toggle in header)</li>
          <li>✓ Mobile 375px (7 phone) + Desktop 1440px (10 frame)</li>
          <li>✓ Pattern desktop: split-view 380px sticky sx + tabs dx</li>
          <li>✓ Varianti OBBLIGATORIE: Public (Installa+Condividi) + Own (Modifica+Pubblica nuova versione, breadcrumb '● Tuo', tab label "Versioni · changelog", CTA edit/yank su latest)</li>
          <li>✓ ConnectionChipStrip footer 6 pip (agent/kb/tool/game/player/session) · empty=dashed</li>
          <li>✓ EntityChip per cross-ref: agent linkato (PersonaCard), KB docs, autore Player, Game target</li>
          <li>✓ Tabs animated underline: Overview · Agente · KB · Tools · Versioni · Recensioni</li>
          <li>✓ ARIA: role="tablist" + role="tab" + aria-selected + aria-controls + role="tabpanel"</li>
          <li>✓ aria-label icon-only buttons (Indietro, Più, Apri agente)</li>
          <li>✓ Focus visibile (outline via components.css esistente, riusato — NON sovrascritto)</li>
          <li>✓ prefers-reduced-motion (components.css)</li>
          <li>✓ Stati: default / empty / loading / error · 2 varianti</li>
          <li>✓ Rating display "4.7" punto culture-independent</li>
          <li>✓ Testo italiano · dati Azul Toolkit v2 + Marco R. autore</li>
          <li>✓ ID short readable: tk-azul-v2, p-marco, a-azul-rules, t-timer, kb-azul-ita — NO UUID</li>
          <li>✓ Commento di apertura con route /toolkits/[id] + descrizione</li>
        </ul>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, marginTop: 18, marginBottom: 8,
        }}>Nuovi componenti v2 emersi</h3>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.8, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>· ToolkitSummaryPanel (sticky split-view sx · cover gradient + stats + CTA mode-aware)</li>
          <li>· ToolkitIncludesGrid (3-box agent/kb/tools con accent entity-colored)</li>
          <li>· VersionTimeline (rail + dot major/minor/patch + actions own-only)</li>
          <li>· RatingBreakdown (numero grande + Stars + histogram bars)</li>
          <li>· PromptPreviewBlock (system prompt mono sunken con token count)</li>
          <li>· Stars (5-star fractional fill — riusabile per game ratings)</li>
        </ul>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, marginTop: 18, marginBottom: 8,
        }}>Riusi pixel-perfect (NON ridisegnare)</h3>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.8, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>· ConnectionChipStrip footer (PR #542/#545)</li>
          <li>· PersonaCard (wave 2 agent-detail)</li>
          <li>· Tabs animated underline (wave 1 game-detail/player-detail)</li>
          <li>· PeekDrawer pattern (wave 1 KB list) — chevron rotate + aria-expanded</li>
          <li>· TabSkeleton/TabError/TabEmpty (wave 1)</li>
        </ul>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, marginTop: 18, marginBottom: 8,
          color: entityHsl('warning'),
        }}>Deviazioni flaggate</h3>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.8, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>· Rating display "4.7" con punto (no virgola IT) — culture-independent, lezione issue #2593</li>
          <li>· System prompt preview è mock visivo testuale (vera logica RAG citations già in agent-detail)</li>
          <li>· Versions kind/major-minor-patch è semantic versioning standard — verificare con backend se modello dati supporta semver</li>
          <li>· @keyframes slideDown referenziato nei peek drawer richiede definizione in components.css o inline — flaggato per non sovrascrivere components.css come da contratto</li>
          <li>· Empty state autore: tab Versioni mostra v0.1.0 placeholder anziché empty — scelta consapevole per dare contesto storico anche su toolkit nuovo</li>
        </ul>
      </section>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
