/* MeepleAI SP4 wave 1 — Schermata #4 / 5
   Route: /agents/[id]
   File: admin-mockups/design_files/sp4-agent-detail.{html,jsx}
   Pattern desktop: Hero character sheet + body con tabs

   Hero 200-220px (180 mobile): radial gradient entity-agent + avatar 96 + nome + persona + meta + action bar.
   ConnectionBar sticky 1:1 prod (6 pip, toolkit empty dashed).
   5 tabs: Identity / Knowledge / Performance / History / Settings.
   Varianti: draft (setup banner) · active · archived (banner + read-only).

   v2 nuovi:
   - AgentHero            → apps/web/src/components/ui/v2/agent-hero/
   - AgentTabs (riuso GameTabs animated underline)
   - PersonaCard          → apps/web/src/components/ui/v2/persona-card/
   - SystemPromptViewer   → apps/web/src/components/ui/v2/system-prompt-viewer/
   - KbDocList            → apps/web/src/components/ui/v2/kb-doc-list/
   - PerformanceKpi       → riuso GameStatsKpi
   - ChatHistoryTimeline  → apps/web/src/components/ui/v2/chat-history-timeline/
   - AgentSettingsForm    → apps/web/src/components/ui/v2/agent-settings-form/
   - AgentDangerZone      → apps/web/src/components/ui/v2/agent-danger-zone/

   Riusi prod: ConnectionBar (PR #549/#552), MeepleCard.list (kb, chat).
*/

const { useState, useEffect, useRef } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.agent;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

const AGENT_BASE = {
  id:'a-wingspan-mage',
  type:'agent',
  title:'Mago di Wingspan',
  avatar:'🧙‍♂️',
  persona:'Esperto di strategia',
  gameId:'g-wingspan',
  desc:'Risponde a domande su poteri uccelli, combo motore e strategia ottimale per ogni habitat. Tono didattico ma diretto, non spoilera scelte.',
  tone:'Didattico · diretto',
  language:'Italiano',
  responseStyle:'coaching',
  model:'Claude Sonnet 4',
  modelId:'claude-sonnet-4',
  temperature: 0.4,
  maxTokens: 1024,
  customInstructions:'Cita sempre il regolamento ufficiale quando possibile.\nPer domande di scoring usa esempi numerici.\nNon rivelare carte avanzate dell\'espansione Asia.',
  systemPrompt:`Sei "Mago di Wingspan", un agente esperto del gioco da tavolo Wingspan.\n\nTuo ruolo: aiutare i giocatori a capire poteri delle carte uccello, sinergie tra habitat e ottimizzare il loro motore di punti.\n\nLinee guida:\n- Cita SEMPRE il regolamento ufficiale (pagina e versione)\n- Per scoring usa esempi numerici concreti\n- Tono didattico ma diretto\n- Non spoilerare strategie avanzate dell'espansione Asia\n- Se non sei certo, rispondi "non sono sicuro" e suggerisci dove cercare`,
  createdAt:'12 Gen 2026',
  lastUsed:'5 minuti fa',
  invocations: 230,
};

const KB_DOCS = [
  { id:'k1', title:'Wingspan · Regolamento base v1.2.pdf', size:'4.2 MB', pages: 24, chunks: 142, status:'indexed', indexedAt:'8 Gen 2026' },
  { id:'k2', title:'Wingspan · FAQ ufficiale.pdf', size:'1.1 MB', pages: 8, chunks: 38, status:'indexed', indexedAt:'8 Gen 2026' },
  { id:'k3', title:'Wingspan · Espansione Europa.pdf', size:'3.8 MB', pages: 18, chunks: 96, status:'indexed', indexedAt:'15 Gen 2026' },
  { id:'k4', title:'Wingspan · Carte uccello reference.csv', size:'180 KB', pages: 1, chunks: 178, status:'indexed', indexedAt:'18 Gen 2026' },
  { id:'k5', title:'Wingspan · Espansione Oceania.pdf', size:'4.5 MB', pages: 22, chunks: 0, status:'processing', indexedAt:null },
  { id:'k6', title:'Strategy guide community.pdf', size:'2.3 MB', pages: 14, chunks: 0, status:'failed', indexedAt:null, error:'Formato non riconosciuto' },
];

const TOP_QUESTIONS = [
  { q:'Posso usare il potere di un uccello già attivato in un turno precedente?', count: 18 },
  { q:'Come funziona il bonus di fine partita "ornitologo"?', count: 14 },
  { q:'Devo scartare carte se non ho cibo per mangiarle?', count: 11 },
  { q:'Quanti uccelli posso giocare al massimo per habitat?', count: 9 },
  { q:'Cosa succede se finisco i token uovo durante l\'azione?', count: 7 },
];

const CHAT_HISTORY = [
  { id:'c1', firstMsg:'Posso giocare un uccello che richiede 2 cibi se ne ho solo 1 e un jolly?', when:'5m fa', msgCount: 6, resolved: true },
  { id:'c2', firstMsg:'Come si calcolano i punti delle uova bonus a fine partita?', when:'2h fa', msgCount: 4, resolved: true },
  { id:'c3', firstMsg:'Spiega il potere "guadagna cibo da chiunque attivi questo habitat"', when:'ieri', msgCount: 8, resolved: true },
  { id:'c4', firstMsg:'È legale rigiocare un uccello scartato dal mazzo?', when:'2g fa', msgCount: 3, resolved: false },
  { id:'c5', firstMsg:'Combo migliore per habitat foresta in 4 giocatori?', when:'4g fa', msgCount: 12, resolved: true },
  { id:'c6', firstMsg:'Differenza tra azione "guadagna cibo" e "predatore"', when:'1 sett fa', msgCount: 5, resolved: true },
];

// ─── ConnectionBar (1:1 prod) ────────────────────────
const ConnectionBar = ({ connections, loading }) => {
  if (loading) {
    return (
      <div style={{ display:'flex', gap: 8, padding:'8px 0', overflowX:'auto', scrollbarWidth:'none' }}>
        {[0,1,2,3,4,5].map(i => (
          <div key={i} className="mai-shimmer" style={{
            height: 28, width: 92 + i*6, borderRadius: 999,
            background:'var(--bg-muted)', flexShrink: 0,
          }}/>
        ))}
      </div>
    );
  }
  return (
    <div className="mai-cb-scroll" style={{
      display:'flex', alignItems:'center', gap: 8,
      overflowX:'auto', padding:'8px 0', scrollbarWidth:'none',
    }}>
      {connections.map(c => {
        const isEmpty = c.isEmpty || c.count === 0;
        const ec = DS.EC[c.entityType];
        return (
          <button key={c.entityType} type="button"
            aria-label={isEmpty ? c.label : `${c.label}: ${c.count}`}
            style={{
              display:'inline-flex', alignItems:'center', gap: 6,
              padding:'6px 12px', borderRadius: 999,
              fontSize: 12, fontWeight: 700, fontFamily:'var(--f-display)',
              background: isEmpty ? 'transparent' : entityHsl(c.entityType, 0.1),
              color: entityHsl(c.entityType),
              border: isEmpty
                ? `1.5px dashed ${entityHsl(c.entityType, 0.5)}`
                : `1px solid ${entityHsl(c.entityType, 0.2)}`,
              opacity: isEmpty ? 0.6 : 1,
              cursor:'pointer', flexShrink: 0, whiteSpace:'nowrap',
              transition:'transform var(--dur-sm) var(--ease-out), box-shadow var(--dur-sm)',
            }}
            onMouseEnter={e => { e.currentTarget.style.transform='scale(1.03)'; e.currentTarget.style.boxShadow='var(--shadow-md)'; }}
            onMouseLeave={e => { e.currentTarget.style.transform=''; e.currentTarget.style.boxShadow=''; }}
          >
            <span aria-hidden="true" style={{ fontSize: 13 }}>{ec.em}</span>
            {isEmpty ? (
              <svg width="13" height="13" viewBox="0 0 24 24"
                fill="none" stroke="currentColor" strokeWidth="2.5"
                strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <line x1="12" y1="5" x2="12" y2="19"/>
                <line x1="5" y1="12" x2="19" y2="12"/>
              </svg>
            ) : (
              <span style={{ fontFamily:'var(--f-mono)', fontVariantNumeric:'tabular-nums', fontWeight: 700 }}>{c.count}</span>
            )}
            <span>{c.label}</span>
          </button>
        );
      })}
    </div>
  );
};

// ─── EntityChip game ─────────────────────────────────
const EntityChipGame = ({ gameId, compact, dark }) => {
  const g = DS.byId[gameId];
  if (!g) return null;
  const onDark = dark === true;
  return (
    <a href="#" onClick={e => e.preventDefault()} style={{
      display:'inline-flex', alignItems:'center', gap: 5,
      padding: compact ? '2px 8px' : '3px 10px',
      borderRadius:'var(--r-pill)',
      background: onDark ? 'rgba(255,255,255,.18)' : entityHsl('game', 0.1),
      color: onDark ? '#fff' : entityHsl('game'),
      fontFamily:'var(--f-display)',
      fontSize: compact ? 11 : 12, fontWeight: 700,
      textDecoration:'none',
      border: onDark ? '1px solid rgba(255,255,255,.3)' : `1px solid ${entityHsl('game', 0.2)}`,
      whiteSpace:'nowrap',
      backdropFilter: onDark ? 'blur(8px)' : 'none',
    }}>
      <span aria-hidden="true">🎲</span>{g.title}
    </a>
  );
};

// ═══════════════════════════════════════════════════════
// ─── AGENT HERO ─────────────────────────────────────
// /* v2: AgentHero → apps/web/src/components/ui/v2/agent-hero/ */
// ═══════════════════════════════════════════════════════
const AgentHero = ({ agent, variant, compact, loading }) => {
  if (loading) {
    return (
      <div style={{ position:'relative', height: compact ? 180 : 220, padding: compact ? '14px 16px' : '24px 32px' }}>
        <div style={{ display:'flex', gap: compact ? 14 : 22, alignItems:'center', height:'100%' }}>
          <div className="mai-shimmer" style={{
            width: compact ? 72 : 96, height: compact ? 72 : 96,
            borderRadius:'var(--r-lg)', background:'var(--bg-muted)', flexShrink: 0,
          }}/>
          <div style={{ flex: 1, display:'flex', flexDirection:'column', gap: 8 }}>
            <div className="mai-shimmer" style={{ height: 14, width:'30%', background:'var(--bg-muted)', borderRadius: 4 }}/>
            <div className="mai-shimmer" style={{ height: 30, width:'60%', background:'var(--bg-muted)', borderRadius: 6 }}/>
            <div className="mai-shimmer" style={{ height: 12, width:'70%', background:'var(--bg-muted)', borderRadius: 4 }}/>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div style={{ position:'relative' }}>
      {/* Variant banners */}
      {variant === 'draft' && (
        <div style={{
          padding: compact ? '8px 16px' : '10px 32px',
          background:'hsl(38, 92%, 50% / .14)',
          borderBottom:'1px solid hsl(38, 92%, 50% / .3)',
          display:'flex', alignItems:'center', gap: 10, flexWrap:'wrap',
        }}>
          <span aria-hidden="true" style={{ fontSize: 16 }}>⚙</span>
          <span style={{
            flex: 1, fontSize: 12.5, color:'hsl(38, 80%, 28%)', fontWeight: 700,
          }}>Agente in setup — completa la configurazione per attivarlo.</span>
          <button type="button" style={{
            padding:'6px 12px', borderRadius:'var(--r-md)',
            background:'hsl(38, 92%, 50%)', color:'#fff', border:'none',
            fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 800, cursor:'pointer',
          }}>Completa setup →</button>
        </div>
      )}
      {variant === 'archived' && (
        <div style={{
          padding: compact ? '8px 16px' : '10px 32px',
          background:'var(--bg-muted)',
          borderBottom:'1px solid var(--border)',
          display:'flex', alignItems:'center', gap: 10, flexWrap:'wrap',
        }}>
          <span aria-hidden="true" style={{ fontSize: 16, color:'var(--text-muted)' }}>⊘</span>
          <span style={{
            flex: 1, fontSize: 12.5, color:'var(--text-sec)', fontWeight: 600,
          }}>Agente archiviato il <strong>22 Mar 2026</strong>. Le chat e i KB sono in sola lettura.</span>
          <button type="button" style={{
            padding:'6px 12px', borderRadius:'var(--r-md)',
            background: entityHsl('agent'), color:'#fff', border:'none',
            fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 800, cursor:'pointer',
          }}>↻ Riattiva</button>
        </div>
      )}

      {/* Hero body */}
      <div style={{
        position:'relative',
        padding: compact ? '18px 16px' : '28px 32px',
        background:`radial-gradient(circle at 20% 30%, ${entityHsl('agent', 0.18)} 0%, transparent 60%)`,
        borderBottom:'1px solid var(--border-light)',
      }}>
        {/* radial gradient bg fallback */}
        <div aria-hidden="true" style={{
          position:'absolute', inset: 0,
          background:`radial-gradient(circle at 80% 70%, ${entityHsl('agent', 0.08)} 0%, transparent 50%)`,
          pointerEvents:'none',
        }}/>

        <div style={{
          position:'relative', zIndex: 1,
          display:'flex', gap: compact ? 14 : 22,
          flexDirection: compact ? 'column' : 'row',
          alignItems: compact ? 'flex-start' : 'center',
        }}>
          {/* Avatar */}
          <div style={{
            width: compact ? 72 : 96, height: compact ? 72 : 96,
            borderRadius:'var(--r-lg)',
            background: entityHsl('agent', 0.16),
            color: entityHsl('agent'),
            display:'flex', alignItems:'center', justifyContent:'center',
            fontSize: compact ? 38 : 56,
            flexShrink: 0,
            border:`2px solid ${entityHsl('agent', 0.3)}`,
            boxShadow:`0 8px 24px ${entityHsl('agent', 0.25)}`,
          }} aria-hidden="true">{agent.avatar}</div>

          {/* Name + persona + meta */}
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{
              display:'flex', alignItems:'center', gap: 6, marginBottom: 6, flexWrap:'wrap',
            }}>
              <span style={{
                display:'inline-flex', alignItems:'center', gap: 5,
                padding:'3px 9px', borderRadius:'var(--r-pill)',
                background: entityHsl('agent', 0.14),
                color: entityHsl('agent'),
                fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform:'uppercase', letterSpacing:'.08em',
              }}><span aria-hidden="true">🤖</span>Agent</span>
              {variant === 'active' && (
                <span style={{
                  padding:'3px 9px', borderRadius:'var(--r-pill)',
                  background:'hsl(140, 60%, 45% / .14)', color:'hsl(140, 50%, 35%)',
                  fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
                  textTransform:'uppercase', letterSpacing:'.08em',
                }}>● Attivo</span>
              )}
            </div>
            <h1 style={{
              fontFamily:'var(--f-display)', fontWeight: 800,
              fontSize: compact ? 24 : 34, letterSpacing:'-.02em', lineHeight: 1.05,
              color:'var(--text)', margin:'0 0 4px',
            }}>{agent.title}</h1>
            <div style={{
              fontFamily:'var(--f-display)', fontSize: compact ? 13 : 14,
              color: entityHsl('agent'), fontWeight: 700,
              marginBottom: 10,
            }}>{agent.persona} · Wingspan</div>
            <div style={{
              display:'flex', flexWrap:'wrap', gap:'5px 10px',
              fontFamily:'var(--f-mono)', fontSize: compact ? 11 : 12,
              color:'var(--text-muted)', fontWeight: 600,
              alignItems:'center',
            }}>
              <EntityChipGame gameId={agent.gameId} compact={compact}/>
              <span aria-hidden="true">·</span>
              <span>creato {agent.createdAt}</span>
              <span aria-hidden="true">·</span>
              <span>ultimo uso <strong style={{ color:'var(--text-sec)' }}>{agent.lastUsed}</strong></span>
              <span aria-hidden="true">·</span>
              <span>{agent.model}</span>
            </div>
          </div>

          {/* Action bar */}
          <div style={{
            display:'flex', gap: 8, flexShrink: 0,
            alignSelf: compact ? 'stretch' : 'auto',
            flexWrap: compact ? 'wrap' : 'nowrap',
          }}>
            {variant !== 'archived' && (
              <button type="button" style={{
                padding: compact ? '9px 14px' : '10px 18px', borderRadius:'var(--r-md)',
                background: entityHsl('agent'), color:'#fff', border:'none',
                fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
                cursor: variant === 'draft' ? 'not-allowed' : 'pointer',
                opacity: variant === 'draft' ? 0.55 : 1,
                boxShadow:`0 4px 14px ${entityHsl('agent', 0.4)}`,
                display:'inline-flex', alignItems:'center', gap: 6,
                flex: compact ? 1 : 'none',
                justifyContent:'center',
              }}><span aria-hidden="true">💬</span>Avvia chat</button>
            )}
            <button type="button" style={{
              padding: compact ? '9px 12px' : '10px 14px', borderRadius:'var(--r-md)',
              background:'transparent', color:'var(--text)',
              border:'1px solid var(--border-strong)',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
              cursor:'pointer',
              display:'inline-flex', alignItems:'center', gap: 5,
            }}><span aria-hidden="true">✎</span>{compact ? '' : 'Modifica'}</button>
            <button type="button" aria-label="Altre azioni" style={{
              padding: compact ? '9px 12px' : '10px 12px', borderRadius:'var(--r-md)',
              background:'transparent', color:'var(--text)',
              border:'1px solid var(--border-strong)',
              fontSize: 14, cursor:'pointer', minWidth: 38,
            }}>⋯</button>
          </div>
        </div>
      </div>
    </div>
  );
};

// ─── Tabs (riuso pattern animated underline) ────────
const AgentTabs = ({ tabs, active, onChange, compact }) => {
  const refs = useRef({});
  const [ind, setInd] = useState({ left: 0, width: 0 });
  useEffect(() => {
    const el = refs.current[active];
    if (el) setInd({ left: el.offsetLeft, width: el.offsetWidth });
  }, [active, tabs.length, compact]);

  return (
    <div className="mai-cb-scroll" style={{
      position:'relative',
      display:'flex', alignItems:'center', gap: 2,
      borderBottom:'1px solid var(--border)',
      overflowX:'auto', scrollbarWidth:'none',
      padding: compact ? '0 16px' : '0 32px',
      background:'var(--bg)',
    }} role="tablist">
      {tabs.map(t => {
        const isActive = t.id === active;
        return (
          <button key={t.id} type="button"
            ref={el => { refs.current[t.id] = el; }}
            onClick={() => onChange(t.id)}
            role="tab" aria-selected={isActive}
            style={{
              display:'inline-flex', alignItems:'center', gap: 7,
              padding:'12px 14px', background:'transparent', border:'none',
              color: isActive ? entityHsl('agent') : 'var(--text-sec)',
              fontFamily:'var(--f-display)', fontSize: 13,
              fontWeight: isActive ? 800 : 700,
              cursor:'pointer', whiteSpace:'nowrap', flexShrink: 0,
            }}>
            <span aria-hidden="true">{t.icon}</span>{t.label}
            {t.count !== undefined && (
              <span style={{
                padding:'1px 7px', borderRadius:'var(--r-pill)',
                background: isActive ? entityHsl('agent') : 'var(--bg-muted)',
                color: isActive ? '#fff' : 'var(--text-muted)',
                fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              }}>{t.count}</span>
            )}
          </button>
        );
      })}
      <span aria-hidden="true" style={{
        position:'absolute', bottom: -1, left: ind.left, width: ind.width,
        height: 2, background: entityHsl('agent'),
        transition:'left .3s cubic-bezier(.4,0,.2,1), width .3s cubic-bezier(.4,0,.2,1)',
        borderRadius:'2px 2px 0 0',
      }}/>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TAB: IDENTITY ──────────────────────────────────
// ═══════════════════════════════════════════════════════
// /* v2: PersonaCard */
const PersonaCard = ({ agent, readonly }) => {
  const [style, setStyle] = useState(agent.responseStyle);
  return (
    <div style={{
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', padding: 18,
    }}>
      <h3 style={{
        fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
        color:'var(--text)', margin:'0 0 14px',
      }}>Persona</h3>
      <div style={{
        display:'grid', gridTemplateColumns:'repeat(auto-fit, minmax(180px, 1fr))', gap: 14,
        marginBottom: 14,
      }}>
        {[
          { label:'Tono', value: agent.tone },
          { label:'Lingua', value: agent.language },
          { label:'Modello', value: agent.model },
        ].map(s => (
          <div key={s.label}>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
              textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 800, marginBottom: 4,
            }}>{s.label}</div>
            <div style={{
              fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700, color:'var(--text)',
            }}>{s.value}</div>
          </div>
        ))}
      </div>
      <div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 800, marginBottom: 6,
        }}>Stile risposta</div>
        <div role="radiogroup" style={{ display:'flex', gap: 6, flexWrap:'wrap' }}>
          {['concisa','dettagliata','coaching'].map(s => {
            const active = style === s;
            return (
              <button key={s} type="button" disabled={readonly}
                onClick={() => !readonly && setStyle(s)}
                aria-pressed={active}
                style={{
                  padding:'7px 14px',
                  borderRadius:'var(--r-pill)',
                  border: active ? `1px solid ${entityHsl('agent', 0.4)}` : '1px solid var(--border)',
                  background: active ? entityHsl('agent', 0.12) : 'var(--bg-card)',
                  color: active ? entityHsl('agent') : 'var(--text-sec)',
                  fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
                  cursor: readonly ? 'not-allowed' : 'pointer',
                  textTransform:'capitalize',
                  opacity: readonly ? 0.7 : 1,
                }}>{s}</button>
            );
          })}
        </div>
      </div>
    </div>
  );
};

// /* v2: SystemPromptViewer */
const SystemPromptViewer = ({ prompt, readonly }) => {
  const [open, setOpen] = useState(false);
  return (
    <div style={{
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', padding: 18,
    }}>
      <button type="button" onClick={() => setOpen(o => !o)}
        style={{
          width:'100%', display:'flex', alignItems:'center', justifyContent:'space-between',
          padding: 0, background:'transparent', border:'none', cursor:'pointer',
          color:'var(--text)',
        }}>
        <span style={{
          display:'flex', alignItems:'center', gap: 8,
          fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
        }}>
          <span aria-hidden="true">📜</span>System prompt
          <span style={{
            padding:'1px 7px', borderRadius:'var(--r-pill)',
            background:'var(--bg-muted)', color:'var(--text-muted)',
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.06em',
          }}>read-only</span>
        </span>
        <span aria-hidden="true" style={{
          transform: open ? 'rotate(90deg)' : 'rotate(0deg)',
          transition:'transform var(--dur-sm)', color:'var(--text-muted)',
        }}>›</span>
      </button>
      {open && (
        <pre style={{
          marginTop: 14, padding: 14,
          background:'var(--bg-muted)', borderRadius:'var(--r-md)',
          fontFamily:'var(--f-mono)', fontSize: 11.5, lineHeight: 1.6,
          color:'var(--text-sec)',
          whiteSpace:'pre-wrap', wordBreak:'break-word',
          maxHeight: 280, overflowY:'auto',
        }}>{prompt}</pre>
      )}
    </div>
  );
};

const AVATAR_OPTS = ['🧙‍♂️','🤖','🧠','🎯','🎨','🦉','🦊','🐦','🌳','🚀','🧬','⚙','📚','🎲'];
const AvatarSelector = ({ current, readonly }) => {
  const [picked, setPicked] = useState(current);
  return (
    <div style={{
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', padding: 18,
    }}>
      <h3 style={{
        fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
        color:'var(--text)', margin:'0 0 12px',
      }}>Avatar</h3>
      <div style={{
        display:'grid', gridTemplateColumns:'repeat(auto-fill, minmax(48px, 1fr))', gap: 6,
      }}>
        {AVATAR_OPTS.map(a => {
          const active = picked === a;
          return (
            <button key={a} type="button" disabled={readonly}
              onClick={() => !readonly && setPicked(a)}
              aria-label={`Avatar ${a}`} aria-pressed={active}
              style={{
                aspectRatio:'1 / 1', borderRadius:'var(--r-md)',
                border: active ? `2px solid ${entityHsl('agent')}` : '1px solid var(--border)',
                background: active ? entityHsl('agent', 0.12) : 'var(--bg)',
                fontSize: 22,
                cursor: readonly ? 'not-allowed' : 'pointer',
                display:'flex', alignItems:'center', justifyContent:'center',
                opacity: readonly ? 0.6 : 1,
              }}>{a}</button>
          );
        })}
      </div>
    </div>
  );
};

const IdentityTab = ({ agent, readonly }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 14 }}>
    <PersonaCard agent={agent} readonly={readonly}/>
    <SystemPromptViewer prompt={agent.systemPrompt} readonly={readonly}/>
    <AvatarSelector current={agent.avatar} readonly={readonly}/>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── TAB: KNOWLEDGE ─────────────────────────────────
// ═══════════════════════════════════════════════════════
// /* v2: KbDocList */
const KbStatusBadge = ({ status }) => {
  const map = {
    indexed:    { label:'✓ Indexed',    color:'success', bg:'hsl(140, 60%, 45% / .14)', fg:'hsl(140, 50%, 35%)' },
    processing: { label:'⏳ Processing', color:'warn',    bg:'hsl(38, 92%, 50% / .14)',  fg:'hsl(38, 80%, 35%)' },
    failed:     { label:'⚠ Failed',     color:'danger',  bg:'hsl(var(--c-danger) / .14)', fg:'hsl(var(--c-danger))' },
  };
  const m = map[status];
  return (
    <span style={{
      padding:'2px 7px', borderRadius:'var(--r-pill)',
      background: m.bg, color: m.fg,
      fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
      textTransform:'uppercase', letterSpacing:'.06em',
    }}>{m.label}</span>
  );
};

const KbDocItem = ({ doc, readonly }) => (
  <article tabIndex={0} className="mai-card-row" style={{
    display:'flex', alignItems:'center', gap: 14, padding: 12,
    background:'var(--bg-card)', border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)', cursor: readonly ? 'default' : 'pointer',
  }}>
    <div style={{
      width: 42, height: 42, borderRadius:'var(--r-md)',
      background: entityHsl('kb', 0.14), color: entityHsl('kb'),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 20, flexShrink: 0,
    }} aria-hidden="true">📄</div>
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{
        display:'flex', alignItems:'baseline', gap: 8, marginBottom: 4, flexWrap:'wrap',
      }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 13, fontWeight: 700, color:'var(--text)',
          whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
          maxWidth: '100%',
        }}>{doc.title}</div>
        <KbStatusBadge status={doc.status}/>
      </div>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
        fontWeight: 600,
      }}>
        {doc.size} · {doc.pages} pag · {doc.chunks} chunks
        {doc.indexedAt && <> · indicizzato il <strong>{doc.indexedAt}</strong></>}
        {doc.error && <> · <span style={{ color:'hsl(var(--c-danger))' }}>{doc.error}</span></>}
      </div>
    </div>
    {!readonly && (
      <button type="button" aria-label="Azioni" style={{
        padding:'6px 10px', borderRadius:'var(--r-md)',
        background:'transparent', border:'1px solid var(--border)',
        color:'var(--text-sec)', fontSize: 13, cursor:'pointer',
      }}>⋯</button>
    )}
  </article>
);

const KnowledgeTab = ({ readonly, hideUploadCta }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
    <div style={{
      display:'flex', alignItems:'center', justifyContent:'space-between', gap: 8,
    }}>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
        fontWeight: 700, letterSpacing:'.04em',
      }}>{KB_DOCS.length} documenti · {KB_DOCS.filter(d => d.status === 'indexed').length} indicizzati</div>
      {!hideUploadCta && (
        <button type="button" style={{
          padding:'8px 14px', borderRadius:'var(--r-md)',
          background: entityHsl('kb'), color:'#fff', border:'none',
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
          cursor:'pointer',
          boxShadow:`0 3px 10px ${entityHsl('kb', 0.35)}`,
        }}>↑ Aggiungi documento</button>
      )}
    </div>
    {KB_DOCS.map(d => <KbDocItem key={d.id} doc={d} readonly={readonly}/>)}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── TAB: PERFORMANCE ───────────────────────────────
// ═══════════════════════════════════════════════════════
const KpiCard = ({ label, value, unit, accent='agent', icon, trend }) => (
  <div style={{
    padding: 14,
    background:'var(--bg-card)', border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)',
    display:'flex', flexDirection:'column', gap: 6,
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 6 }}>
      <span aria-hidden="true" style={{
        width: 24, height: 24, borderRadius:'var(--r-sm)',
        background: entityHsl(accent, 0.14), color: entityHsl(accent),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 12,
      }}>{icon}</span>
      <span style={{
        fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
        textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 800,
      }}>{label}</span>
    </div>
    <div style={{
      fontFamily:'var(--f-display)', fontSize: 28, fontWeight: 800,
      color:'var(--text)', fontVariantNumeric:'tabular-nums', lineHeight: 1,
    }}>{value}<span style={{ fontSize: 13, color:'var(--text-muted)', fontWeight: 600, marginLeft: 4 }}>{unit}</span></div>
    {trend && (
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10,
        color: trend.startsWith('+') ? 'hsl(140, 50%, 35%)' : 'hsl(var(--c-danger))',
        fontWeight: 700,
      }}>{trend}</div>
    )}
  </div>
);

// Bar chart placeholder
const ChatChart = () => {
  const weeks = [12, 19, 24, 18, 31, 28, 42, 38];
  const max = Math.max(...weeks);
  const labels = ['8s','7s','6s','5s','4s','3s','2s','1s'];
  return (
    <div style={{
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', padding: 18,
    }}>
      <div style={{
        display:'flex', alignItems:'baseline', justifyContent:'space-between', marginBottom: 14,
      }}>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800, color:'var(--text)', margin: 0,
        }}>Chat per settimana</h3>
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', fontWeight: 700,
        }}>ultime 8 settimane</span>
      </div>
      <div style={{ display:'flex', alignItems:'flex-end', gap: 8, height: 140, paddingBottom: 18, position:'relative' }}>
        {weeks.map((w, i) => (
          <div key={i} style={{ flex: 1, display:'flex', flexDirection:'column', alignItems:'center', gap: 4 }}>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 700,
              fontVariantNumeric:'tabular-nums',
            }}>{w}</div>
            <div style={{
              width:'80%', height: `${(w/max)*100}%`,
              background:`linear-gradient(180deg, ${entityHsl('agent')} 0%, ${entityHsl('agent', 0.5)} 100%)`,
              borderRadius:'4px 4px 0 0',
              minHeight: 4,
            }}/>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)', fontWeight: 600,
              position:'absolute', bottom: 0,
            }}>{labels[i]}</div>
          </div>
        ))}
      </div>
    </div>
  );
};

const TopQuestions = () => (
  <div style={{
    background:'var(--bg-card)', border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)', padding: 18,
  }}>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800, color:'var(--text)', margin:'0 0 12px',
    }}>Domande ricorrenti</h3>
    <div style={{ display:'flex', flexDirection:'column', gap: 0 }}>
      {TOP_QUESTIONS.map((q, i) => (
        <div key={i} style={{
          display:'flex', alignItems:'flex-start', gap: 10, padding:'8px 0',
          borderBottom: i < TOP_QUESTIONS.length - 1 ? '1px solid var(--border-light)' : 'none',
        }}>
          <span style={{
            fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800,
            color:'var(--text-muted)', width: 22, flexShrink: 0,
          }}>#{i+1}</span>
          <span style={{ flex: 1, fontSize: 12.5, color:'var(--text-sec)', lineHeight: 1.5 }}>{q.q}</span>
          <span style={{
            padding:'1px 7px', borderRadius:'var(--r-pill)',
            background: entityHsl('chat', 0.12), color: entityHsl('chat'),
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
            flexShrink: 0,
          }}>{q.count}</span>
        </div>
      ))}
    </div>
  </div>
);

const PerformanceTab = () => (
  <div style={{ display:'flex', flexDirection:'column', gap: 14 }}>
    <div style={{
      display:'grid', gridTemplateColumns:'repeat(auto-fit, minmax(160px, 1fr))', gap: 10,
    }}>
      <KpiCard label="Chat totali" value="124" unit="" accent="chat" icon="💬" trend="+12% 30g"/>
      <KpiCard label="Tempo risposta" value="2.3" unit="s" accent="event" icon="⏱" trend="-0.4s"/>
      <KpiCard label="Soddisfazione" value="94" unit="%" accent="player" icon="👍" trend="+2%"/>
      <KpiCard label="Citazioni KB" value="78" unit="%" accent="kb" icon="📚" trend="+5%"/>
    </div>
    <ChatChart/>
    <TopQuestions/>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── TAB: HISTORY ───────────────────────────────────
// ═══════════════════════════════════════════════════════
const ChatTimelineItem = ({ chat, isLast }) => (
  <div style={{ display:'flex', gap: 12, position:'relative' }}>
    {/* Timeline rail */}
    <div style={{
      width: 28, display:'flex', flexDirection:'column', alignItems:'center', flexShrink: 0,
    }}>
      <div style={{
        width: 28, height: 28, borderRadius:'50%',
        background: entityHsl('chat', 0.14), color: entityHsl('chat'),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 12, border:`2px solid ${entityHsl('chat', 0.3)}`,
      }} aria-hidden="true">💬</div>
      {!isLast && (
        <div style={{
          width: 2, flex: 1, background:'var(--border)', marginTop: 2, minHeight: 24,
        }}/>
      )}
    </div>
    {/* Item */}
    <article tabIndex={0} className="mai-card-row" style={{
      flex: 1, padding: 12, marginBottom: isLast ? 0 : 12,
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', cursor:'pointer',
      display:'flex', flexDirection:'column', gap: 6,
    }}>
      <div style={{
        display:'flex', alignItems:'baseline', gap: 8, justifyContent:'space-between', flexWrap:'wrap',
      }}>
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color: entityHsl('chat'),
          fontWeight: 700,
        }}>{chat.when}</span>
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          fontWeight: 700,
        }}>{chat.msgCount} msg · {chat.resolved ? '✓ risolta' : '○ aperta'}</span>
      </div>
      <p style={{
        fontSize: 13, color:'var(--text)', margin: 0, lineHeight: 1.5,
        display:'-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient:'vertical', overflow:'hidden',
      }}>
        <span aria-hidden="true" style={{ color:'var(--text-muted)', marginRight: 6 }}>›</span>
        {chat.firstMsg}
      </p>
    </article>
  </div>
);

const HistoryTab = () => (
  <div style={{ display:'flex', flexDirection:'column' }}>
    {CHAT_HISTORY.map((c, i) => (
      <ChatTimelineItem key={c.id} chat={c} isLast={i === CHAT_HISTORY.length - 1}/>
    ))}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── TAB: SETTINGS ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const SettingsRow = ({ label, hint, children }) => (
  <div style={{
    display:'flex', alignItems:'center', gap: 16,
    padding:'14px 0',
    borderBottom:'1px solid var(--border-light)',
  }}>
    <div style={{ flex: 1 }}>
      <div style={{
        fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 700, color:'var(--text)',
      }}>{label}</div>
      {hint && (
        <div style={{
          fontSize: 11.5, color:'var(--text-muted)', marginTop: 2, lineHeight: 1.4,
        }}>{hint}</div>
      )}
    </div>
    <div style={{ flexShrink: 0 }}>{children}</div>
  </div>
);

const Toggle = ({ checked, onChange, disabled }) => (
  <button type="button" disabled={disabled}
    onClick={() => !disabled && onChange(!checked)}
    aria-pressed={checked}
    style={{
      width: 40, height: 22, borderRadius: 999,
      background: checked ? entityHsl('agent') : 'var(--bg-muted)',
      border:'none', position:'relative',
      cursor: disabled ? 'not-allowed' : 'pointer',
      transition:'background var(--dur-sm)',
      opacity: disabled ? 0.5 : 1,
    }}>
    <span aria-hidden="true" style={{
      position:'absolute', top: 2, left: checked ? 20 : 2,
      width: 18, height: 18, borderRadius:'50%',
      background:'#fff', boxShadow:'0 1px 3px rgba(0,0,0,.2)',
      transition:'left var(--dur-sm) var(--ease-spring)',
    }}/>
  </button>
);

// /* v2: AgentSettingsForm */
const SettingsTab = ({ agent, readonly }) => {
  const [active, setActive] = useState(true);
  const [model, setModel] = useState(agent.modelId);
  const [temp, setTemp] = useState(agent.temperature);
  const [maxT, setMaxT] = useState(agent.maxTokens);
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 14 }}>
      <div style={{
        background:'var(--bg-card)', border:'1px solid var(--border)',
        borderRadius:'var(--r-lg)', padding:'4px 18px 14px',
      }}>
        <SettingsRow label="Attivo" hint="L'agente è disponibile per nuove chat e suggerimenti.">
          <Toggle checked={active} onChange={setActive} disabled={readonly}/>
        </SettingsRow>
        <SettingsRow label="Modello LLM" hint="Cambiare modello può influire su costo e qualità delle risposte.">
          <select value={model} onChange={e=>setModel(e.target.value)} disabled={readonly}
            style={{
              padding:'7px 10px', borderRadius:'var(--r-md)',
              border:'1px solid var(--border)', background:'var(--bg-card)',
              color:'var(--text)', fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
              cursor: readonly ? 'not-allowed' : 'pointer',
              opacity: readonly ? 0.7 : 1,
            }}>
            <option value="claude-sonnet-4">Claude Sonnet 4</option>
            <option value="claude-opus-4">Claude Opus 4</option>
            <option value="claude-haiku-4">Claude Haiku 4</option>
            <option value="gpt-4o">GPT-4o</option>
            <option value="gpt-4o-mini">GPT-4o mini</option>
          </select>
        </SettingsRow>
        <SettingsRow label="Creatività (temperature)" hint="0 = risposte deterministiche · 1 = molto creative.">
          <div style={{ display:'flex', alignItems:'center', gap: 10, minWidth: 200 }}>
            <input type="range" min="0" max="1" step="0.05" value={temp} disabled={readonly}
              onChange={e => setTemp(parseFloat(e.target.value))}
              style={{
                flex: 1, accentColor: entityHsl('agent'),
                opacity: readonly ? 0.6 : 1,
              }}/>
            <span style={{
              minWidth: 36, textAlign:'right',
              fontFamily:'var(--f-mono)', fontSize: 12, fontWeight: 800,
              color: entityHsl('agent'), fontVariantNumeric:'tabular-nums',
            }}>{temp.toFixed(2)}</span>
          </div>
        </SettingsRow>
        <SettingsRow label="Max tokens risposta" hint="Lunghezza massima di una risposta. Default 1024.">
          <input type="number" value={maxT} disabled={readonly}
            onChange={e=>setMaxT(parseInt(e.target.value)||0)}
            min="128" max="4096" step="128"
            style={{
              width: 100, padding:'7px 10px', borderRadius:'var(--r-md)',
              border:'1px solid var(--border)', background:'var(--bg-card)',
              color:'var(--text)', fontFamily:'var(--f-mono)', fontSize: 12, fontWeight: 700,
              textAlign:'right',
            }}/>
        </SettingsRow>
      </div>

      <div style={{
        background:'var(--bg-card)', border:'1px solid var(--border)',
        borderRadius:'var(--r-lg)', padding: 18,
      }}>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800, color:'var(--text)', margin:'0 0 4px',
        }}>Istruzioni custom</h3>
        <p style={{ fontSize: 11.5, color:'var(--text-muted)', margin:'0 0 10px' }}>
          Aggiunte al system prompt (1 per riga).
        </p>
        <textarea defaultValue={agent.customInstructions} disabled={readonly}
          style={{
            width:'100%', minHeight: 100,
            padding: 12, borderRadius:'var(--r-md)',
            border:'1px solid var(--border)', background:'var(--bg)',
            color:'var(--text)', fontFamily:'var(--f-mono)', fontSize: 12, lineHeight: 1.6,
            resize:'vertical',
            opacity: readonly ? 0.7 : 1,
          }}/>
      </div>

      {/* v2: AgentDangerZone */}
      <div style={{
        background:'hsl(var(--c-danger) / .04)',
        border:'1px solid hsl(var(--c-danger) / .25)',
        borderRadius:'var(--r-lg)', padding: 18,
      }}>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
          color:'hsl(var(--c-danger))', margin:'0 0 4px',
          display:'flex', alignItems:'center', gap: 6,
        }}><span aria-hidden="true">⚠</span>Danger zone</h3>
        <p style={{ fontSize: 11.5, color:'var(--text-muted)', margin:'0 0 12px' }}>
          Azioni irreversibili. Le chat e i KB associati non vengono cancellati.
        </p>
        <div style={{ display:'flex', gap: 8, flexWrap:'wrap' }}>
          <button type="button" disabled={readonly} style={{
            padding:'7px 14px', borderRadius:'var(--r-md)',
            background:'transparent', border:'1px solid hsl(var(--c-danger) / .4)',
            color:'hsl(var(--c-danger))', fontFamily:'var(--f-display)',
            fontSize: 12, fontWeight: 800,
            cursor: readonly ? 'not-allowed' : 'pointer',
            opacity: readonly ? 0.5 : 1,
          }}>⊘ Archivia</button>
          <button type="button" disabled={readonly} style={{
            padding:'7px 14px', borderRadius:'var(--r-md)',
            background:'hsl(var(--c-danger))',
            border:'1px solid hsl(var(--c-danger))',
            color:'#fff', fontFamily:'var(--f-display)',
            fontSize: 12, fontWeight: 800,
            cursor: readonly ? 'not-allowed' : 'pointer',
            opacity: readonly ? 0.5 : 1,
          }}>🗑 Elimina (richiede conferma)</button>
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── EMPTY/ERROR/LOADING per tab ────────────────────
// ═══════════════════════════════════════════════════════
const TabSkeleton = () => (
  <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
    {[0,1,2,3].map(i => (
      <div key={i} className="mai-shimmer" style={{
        height: 80 + (i%2)*16, borderRadius:'var(--r-lg)', background:'var(--bg-muted)',
      }}/>
    ))}
  </div>
);

const TabError = ({ onRetry }) => (
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
    }}>Errore caricamento</h3>
    <p style={{ fontSize: 12, color:'var(--text-muted)', margin:'0 0 12px', maxWidth: 320 }}>
      Impossibile recuperare i dati. Verifica la connessione.
    </p>
    <button type="button" onClick={onRetry}
      style={{
        padding:'7px 14px', borderRadius:'var(--r-md)',
        background:'hsl(var(--c-danger))', color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
      }}>↻ Riprova</button>
  </div>
);

const TabEmpty = ({ icon, title, sub, ctaLabel, ctaColor='agent' }) => (
  <div style={{
    padding:'40px 24px', textAlign:'center',
    background:'var(--bg-card)',
    border:'1px dashed var(--border-strong)',
    borderRadius:'var(--r-xl)',
    display:'flex', flexDirection:'column', alignItems:'center',
  }}>
    <div style={{
      width: 60, height: 60, borderRadius:'50%',
      background: entityHsl(ctaColor, 0.12), color: entityHsl(ctaColor),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 28, marginBottom: 12,
    }} aria-hidden="true">{icon}</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
      color:'var(--text)', margin:'0 0 4px',
    }}>{title}</h3>
    <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 14px', maxWidth: 340 }}>
      {sub}
    </p>
    {ctaLabel && (
      <button type="button" style={{
        padding:'8px 16px', borderRadius:'var(--r-md)',
        background: entityHsl(ctaColor), color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
        boxShadow: `0 3px 10px ${entityHsl(ctaColor, 0.35)}`,
      }}>{ctaLabel}</button>
    )}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── BODY ───────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const AgentDetailBody = ({ stateOverride, variant='active', compact, initialTab='identity' }) => {
  const [tab, setTab] = useState(initialTab);
  useEffect(() => { setTab(initialTab); }, [initialTab]);

  const isLoading = stateOverride === 'loading';
  const isError = stateOverride === 'error';
  const readonly = variant === 'archived';

  const agent = AGENT_BASE;

  const tabs = [
    { id:'identity', icon:'🎭', label:'Identity' },
    { id:'knowledge', icon:'📚', label:'Knowledge', count: KB_DOCS.length },
    { id:'performance', icon:'📊', label:'Performance' },
    { id:'history', icon:'💬', label:'History', count: variant === 'draft' ? 0 : CHAT_HISTORY.length },
    { id:'settings', icon:'⚙', label:'Settings' },
  ];

  const connections = [
    { entityType:'game', count: 1, label:'Gioco', isEmpty: false },
    { entityType:'kb', count: 8, label:'KB', isEmpty: false },
    { entityType:'chat', count: variant === 'draft' ? 0 : 124, label:'Chat', isEmpty: variant === 'draft' },
    { entityType:'session', count: variant === 'draft' ? 0 : 12, label:'Partite', isEmpty: variant === 'draft' },
    { entityType:'toolkit', count: 0, label:'Toolkit', isEmpty: true },
    { entityType:'player', count: variant === 'draft' ? 0 : 4, label:'Giocatori', isEmpty: variant === 'draft' },
  ];

  const renderTab = () => {
    if (isLoading) return <TabSkeleton/>;
    if (isError) return <TabError/>;

    if (variant === 'draft' && (tab === 'performance' || tab === 'history')) {
      return <TabEmpty icon="📊" title="Disponibile dopo l'attivazione"
        sub="Le metriche e la cronologia chat sono visibili solo per agenti attivi. Completa il setup per iniziare."
        ctaLabel="Completa setup →"/>;
    }

    if (tab === 'identity') return <IdentityTab agent={agent} readonly={readonly}/>;
    if (tab === 'knowledge') return <KnowledgeTab readonly={readonly} hideUploadCta={readonly}/>;
    if (tab === 'performance') return <PerformanceTab/>;
    if (tab === 'history') {
      if (CHAT_HISTORY.length === 0) {
        return <TabEmpty icon="💬" title="Nessuna chat ancora"
          sub="Avvia la prima conversazione per iniziare a tracciare la cronologia."
          ctaLabel="💬 Avvia prima chat" ctaColor="chat"/>;
      }
      return <HistoryTab/>;
    }
    if (tab === 'settings') return <SettingsTab agent={agent} readonly={readonly}/>;
    return null;
  };

  return (
    <>
      <AgentHero agent={agent} variant={variant} compact={compact} loading={isLoading}/>
      {/* ConnectionBar sticky */}
      <div style={{
        padding: compact ? '0 16px' : '0 32px',
        background:'var(--glass-bg)', backdropFilter:'blur(12px)',
        borderBottom:'1px solid var(--border-light)',
        position:'sticky', top: 0, zIndex: 8,
      }}>
        <ConnectionBar connections={connections} loading={isLoading}/>
      </div>
      <div style={{ position:'sticky', top: 45, zIndex: 7,
        background:'var(--glass-bg)', backdropFilter:'blur(12px)',
      }}>
        <AgentTabs tabs={tabs} active={tab} onChange={setTab} compact={compact}/>
      </div>
      <div style={{
        padding: compact ? '14px 16px 32px' : '20px 32px 64px',
      }}>
        {renderTab()}
      </div>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── DESKTOP / MOBILE SHELLS ─────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopNav = () => (
  <div style={{
    display:'flex', alignItems:'center', gap: 14,
    padding:'10px 32px',
    background:'var(--glass-bg)', backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border)',
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
      <div style={{
        width: 26, height: 26, borderRadius: 7,
        background:'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)))',
        color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
        fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)',
      }}>M</div>
      <span style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 14 }}>MeepleAI</span>
    </div>
    <div style={{
      flex: 1, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      marginLeft: 22,
    }}>
      <a href="#" style={{ color:'inherit' }}>Agenti</a>
      <span aria-hidden="true"> / </span>
      <strong style={{ color:'var(--text-sec)' }}>{AGENT_BASE.title}</strong>
    </div>
  </div>
);

const DesktopScreen = ({ stateOverride, variant, initialTab }) => (
  <div style={{ minHeight: 720, background:'var(--bg)' }}>
    <DesktopNav/>
    <AgentDetailBody stateOverride={stateOverride} variant={variant} initialTab={initialTab}/>
  </div>
);

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);
const PhoneTopNav = () => (
  <div style={{
    display:'flex', alignItems:'center', gap: 9,
    padding:'10px 14px', background:'var(--glass-bg)',
    backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border-light)',
    position:'sticky', top: 0, zIndex: 9,
  }}>
    <button aria-label="Indietro" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>←</button>
    <div style={{
      flex: 1, fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
      textAlign:'center', fontWeight: 700,
    }}>agents / mago-wingspan</div>
    <button aria-label="Più" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>⋯</button>
  </div>
);
const MobileScreen = ({ stateOverride, variant, initialTab }) => (
  <>
    <PhoneSbar/>
    <div style={{ flex: 1, overflowY:'auto', position:'relative', background:'var(--bg)' }}>
      <PhoneTopNav/>
      <AgentDetailBody stateOverride={stateOverride} variant={variant} compact initialTab={initialTab}/>
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

const DesktopFrame = ({ label, desc, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width:'100%', maxWidth: 1280,
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
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/agents/mago-wingspan</span>
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
// ─── ROOT ────────────────────────────────────────────
const MOBILE_STATES = [
  { id:'m1', stateOverride:null, variant:'active', tab:'identity', label:'01 · Active · Identity', desc:'Hero radial gradient + avatar 72 + persona + meta. Tab Identity: persona card + system prompt collapsible + avatar selector.' },
  { id:'m2', stateOverride:null, variant:'active', tab:'knowledge', label:'02 · Active · Knowledge', desc:'6 KB doc list con badge status (indexed / processing / failed). Footer: count indicizzati + CTA upload.' },
  { id:'m3', stateOverride:null, variant:'active', tab:'performance', label:'03 · Active · Performance', desc:'4 KPI cards con trend (chat/tempo/sat/citazioni KB) + bar chart 8 settimane + top 5 domande ricorrenti.' },
  { id:'m4', stateOverride:null, variant:'active', tab:'history', label:'04 · Active · History', desc:'Timeline 6 chat con rail entity-chat + preview prima domanda + status risolta/aperta.' },
  { id:'m5', stateOverride:null, variant:'active', tab:'settings', label:'05 · Active · Settings', desc:'Toggle attivo + dropdown modello + slider temperature + max tokens + textarea custom + Danger zone.' },
  { id:'m6', stateOverride:null, variant:'draft', tab:'performance', label:'06 · Draft · locked tab', desc:'Banner giallo "Agente in setup" + chat/session/player pip empty dashed. Tab Performance/History mostrano empty con CTA setup.' },
  { id:'m7', stateOverride:null, variant:'archived', tab:'identity', label:'07 · Archived · read-only', desc:'Banner grey persistente + CTA Riattiva. Avvia chat nascosto. Tutti i campi disabilitati con opacity .7.' },
  { id:'m8', stateOverride:'loading', variant:'active', tab:'identity', label:'08 · Loading', desc:'Hero skeleton + ConnectionBar shimmer + tabs nav visible + body 4 row shimmer altezze variabili.' },
];

function ThemeToggle() {
  const [dark, setDark] = useState(false);
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle"
      aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span>
      <span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

function App() {
  return (
    <div className="stage">
      <ThemeToggle/>
      <div className="stage-wrap">
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 8,
        }}>SP4 wave 1 · #4/5 · Hero character sheet + tabs</div>
        <h1>Agent detail — /agents/[id]</h1>
        <p className="lead">
          Hero radial gradient entity-agent + avatar 96 + persona + action bar variant-aware.
          <strong> ConnectionBar</strong> sticky 1:1 prod (toolkit empty dashed).
          5 tabs <strong>animated underline</strong>: Identity / Knowledge / Performance / History / Settings.
          Varianti: <strong>active</strong> · <strong>draft</strong> (banner setup, perf/history locked) · <strong>archived</strong> (banner + read-only).
        </p>

        <div className="section-label">Mobile · 375 — 8 stati / tabs</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <MobileScreen stateOverride={s.stateOverride} variant={s.variant} initialTab={s.tab}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1440 — 5 stati chiave</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="09 · Desktop · Active · Performance"
            desc="Tab Performance: 4 KPI grid (chat/tempo/sat/KB%), bar chart 8 settimane gradient agent, top 5 domande ricorrenti con count chip entity-chat.">
            <DesktopScreen stateOverride={null} variant="active" initialTab="performance"/>
          </DesktopFrame>
          <DesktopFrame label="10 · Desktop · Active · Knowledge"
            desc="6 KB doc con badge status (indexed verde / processing giallo / failed danger). Hover row con bordo entity-game accent. CTA upload entity-kb top-right.">
            <DesktopScreen stateOverride={null} variant="active" initialTab="knowledge"/>
          </DesktopFrame>
          <DesktopFrame label="11 · Desktop · Active · Settings"
            desc="Form completo (toggle / select modello / slider temperature accent agent / max tokens / textarea custom) + Danger zone con elimina destructive.">
            <DesktopScreen stateOverride={null} variant="active" initialTab="settings"/>
          </DesktopFrame>
          <DesktopFrame label="12 · Desktop · Draft · setup"
            desc="Banner giallo 'in setup' top hero + Avvia chat disabilitata (opacity .55). ConnectionBar: chat/session/player pip empty dashed. Tabs Performance/History mostrano empty con CTA Completa setup.">
            <DesktopScreen stateOverride={null} variant="draft" initialTab="performance"/>
          </DesktopFrame>
          <DesktopFrame label="13 · Desktop · Archived · read-only"
            desc="Banner grey persistente con CTA Riattiva. Avvia chat nascosto. Identity/Knowledge/Settings in read-only (toggle/slider/textarea con opacity .7 + cursor not-allowed).">
            <DesktopScreen stateOverride={null} variant="archived" initialTab="identity"/>
          </DesktopFrame>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim {
          0%   { background-position: -200% 0; }
          100% { background-position:  200% 0; }
        }
        .mai-shimmer {
          background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important;
          background-size: 200% 100% !important;
          animation: mai-shimmer-anim 1.4s linear infinite;
        }
        .phones-grid {
          display: grid;
          grid-template-columns: repeat(auto-fill, minmax(380px, 1fr));
          gap: var(--s-7);
          align-items: start;
        }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage .lead { line-height: var(--lh-relax); }
        .phone > div::-webkit-scrollbar { display: none; }
        .mai-cb-scroll::-webkit-scrollbar { display: none; }
        .mai-card-row {
          outline: none;
          transition: transform var(--dur-sm) var(--ease-out), border-color var(--dur-sm), box-shadow var(--dur-sm);
        }
        .mai-card-row:hover {
          transform: translateY(-1px);
          border-color: hsl(var(--c-agent) / .35);
          box-shadow: var(--shadow-xs);
        }
        .mai-card-row:focus-visible {
          outline: 2px solid hsl(var(--c-agent));
          outline-offset: 2px;
        }
        button:focus-visible, a:focus-visible, input:focus-visible, select:focus-visible, textarea:focus-visible {
          outline: 2px solid hsl(var(--c-agent));
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
