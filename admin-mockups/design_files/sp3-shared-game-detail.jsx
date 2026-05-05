/* MeepleAI SP3 — Shared Game Detail (public catalog item)
   Route target: /shared-games/[slug]
   Vetrina pubblica di un singolo gioco condiviso. Hero gioco + ConnectionBar
   (riprodotta 1:1 dal componente prod PR #549/#552) + 3 tabs (toolkit /
   agenti / docs) + contributors strip + sticky CTA "Accedi per installare".

   Componenti v2 nuovi:
   - ConnectionBar (riproduzione fedele del componente prod, vedi specs)
   - ToolkitPublicListItem
   - AgentPublicListItem
   - KBPublicDocItem
   - ContributorsStrip
   - Tabs (rounded-full + animated underline indicator)

   Riusi: Banner (#1, #2, #3), entity-color system da DS.

   Decisioni di design:
   - CTA "Accedi per installare": sticky-bottom su mobile (full-width),
     anchored bottom-right floating su desktop (più CTA-test friendly).
   - Tabs underline: ANIMATED slide (300ms cubic-bezier(.4,0,.2,1)),
     scaleX scale-aware. Più premium, costa 6 LOC.
   - Top contributors: 8 (sweet spot — abbastanza per dare social proof,
     non così tanti da diventare noise; mobile mostra primi 5 + "+3").
*/

const { useState, useEffect, useMemo, useRef } = React;
const DS = window.DS;

// Game = Wingspan (cover gradient verde, 5 toolkit, 2 agenti, 7 docs nel default)
const GAME = DS.games.find(g => g.id === 'g-wingspan') || DS.games[2];

// ─── DATASET PUBBLICO (mockato) ──────────────────────
const PUB_TOOLKITS = [
  { id:'tk-wing-friendly', title:'Wingspan: Friendly', author:'Marco R.', authorId:'p-marco',
    rating:4.8, ratingCount: 142, installs: 2840, official: false, badge:'Top 10',
    desc:'Toolkit completo per partite rilassate: timer 5min/turno, score helper, mazzo uccelli.',
    tools: 5, version:'2.1.0', updated:'3 giorni fa' },
  { id:'tk-wing-tournament', title:'Tournament Pack', author:'Stonemaier (Official)', authorId:'p-official',
    rating:4.9, ratingCount: 287, installs: 5120, official: true,
    desc:'Pacchetto ufficiale per tornei: timer aggressivo, scorekeeper conforme regole 2024.',
    tools: 8, version:'1.0.0', updated:'1 sett. fa' },
  { id:'tk-wing-solo', title:'Solo Mode Tracker', author:'Sara T.', authorId:'p-sara',
    rating:4.6, ratingCount: 89, installs: 1340, official: false,
    desc:'Tracker dedicato modalità solitario con automa Automaton incluso.',
    tools: 3, version:'1.4.2', updated:'2 sett. fa' },
  { id:'tk-wing-asia', title:'Asia Expansion Helper', author:'Luca B.', authorId:'p-luca',
    rating:4.4, ratingCount: 56, installs: 720, official: false,
    desc:'Estensione per Asia expansion: tracker boschi, monsoni, nuovi tipi alimentari.',
    tools: 4, version:'0.9.0-beta', updated:'5 giorni fa' },
];

const PUB_AGENTS = [
  { id:'a-wing-rules', title:'Wingspan Rules Expert', model:'Claude Sonnet', author:'Marco R.',
    convs: 1842, expertise:['Regole base','Bonus carte','Asia expansion'], rating: 4.7,
    desc:'Risponde su regole base + Europe/Oceania/Asia expansion. Cita sempre la pagina del rulebook.' },
  { id:'a-wing-strategy', title:'Wingspan Strategy Coach', model:'GPT-4o', author:'Sara T.',
    convs: 624, expertise:['Apertura','Engine building','Endgame'], rating: 4.5,
    desc:'Coach per migliorare punteggi: analizza la tua mano e suggerisce la mossa ottimale.' },
];

const PUB_KBS = [
  { id:'kb-wing-en', title:'wingspan-rules-en.pdf', kind:'PDF', pages: 24, lang:'EN', size:'4.8 MB' },
  { id:'kb-wing-it', title:'wingspan-regolamento-ita.pdf', kind:'PDF', pages: 22, lang:'IT', size:'4.2 MB' },
  { id:'kb-wing-faq', title:'wingspan-faq-bgg.md', kind:'MD',  pages: 8,  lang:'EN', size:'180 KB' },
  { id:'kb-wing-asia', title:'wingspan-asia-rules.pdf', kind:'PDF', pages: 12, lang:'EN', size:'2.1 MB' },
  { id:'kb-wing-europe', title:'wingspan-europe.pdf', kind:'PDF', pages: 8, lang:'EN', size:'1.4 MB' },
  { id:'kb-wing-bgg', title:'BoardGameGeek FAQ', kind:'URL', pages: null, lang:'EN', size:null,
    url:'boardgamegeek.com/wingspan/faq' },
  { id:'kb-wing-errata', title:'wingspan-errata-2024.pdf', kind:'PDF', pages: 4, lang:'EN', size:'320 KB' },
];

const PUB_CONTRIBUTORS = [
  { id:'p-marco',  name:'Marco R.',  initials:'MR', color: 262, contribs: 12 },
  { id:'p-sara',   name:'Sara T.',   initials:'ST', color: 320, contribs: 9 },
  { id:'p-luca',   name:'Luca B.',   initials:'LB', color: 180, contribs: 6 },
  { id:'p-andrea', name:'Andrea P.', initials:'AP', color: 100, contribs: 5 },
  { id:'p-giulia', name:'Giulia M.', initials:'GM', color: 10,  contribs: 4 },
  { id:'p-elena',  name:'Elena F.',  initials:'EF', color: 200, contribs: 3 },
  { id:'p-nico',   name:'Nico V.',   initials:'NV', color: 35,  contribs: 2 },
  { id:'p-paolo',  name:'Paolo R.',  initials:'PR', color: 290, contribs: 2 },
];

// Datasets per stati
const DATASETS = {
  default:  { toolkits: PUB_TOOLKITS,         agents: PUB_AGENTS,        kbs: PUB_KBS,           contributors: PUB_CONTRIBUTORS },
  minimal:  { toolkits: PUB_TOOLKITS.slice(0,1), agents: [],             kbs: PUB_KBS.slice(0,1),contributors: PUB_CONTRIBUTORS.slice(0,1) },
  'no-content': { toolkits: [], agents: [], kbs: [], contributors: [] },
};

// ═══════════════════════════════════════════════════════
// ─── CONNECTIONBAR (riproduzione 1:1 da PR #549/#552) ─
// ═══════════════════════════════════════════════════════
// Container: flex items-center gap-2 overflow-x-auto py-2
// Pip = button rounded-full px-3 py-1.5 text-xs font-bold
// bg: entityHsl(type, 0.1) — fg: entityHsl(type)
// empty: border-dashed opacity-60 + Plus icon
// hover: scale 1.03 + shadow-md
// Layout pip: [Icon] [count tabular-nums] [label]
// aria-label: "${label}: ${count}" o "${label}" se empty
// returns null se connections.length === 0
const ConnectionBar = ({ connections, loading }) => {
  if (loading) {
    return (
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        overflowX:'auto', padding:'8px 0', scrollbarWidth:'none',
      }}>
        {[0,1,2,3].map(i => (
          <div key={i} className="mai-shimmer" style={{
            height: 28, width: 96 + i*8,
            borderRadius: 999,
            background:'var(--bg-muted)',
            flexShrink: 0,
          }}/>
        ))}
      </div>
    );
  }

  if (!connections || connections.length === 0) return null;

  return (
    <div style={{
      display:'flex', alignItems:'center', gap: 8,
      overflowX:'auto', padding:'8px 0',
      scrollbarWidth:'none',
    }} className="mai-cb-scroll">
      {connections.map(c => {
        const ec = DS.EC[c.entityType] || DS.EC.game;
        const isEmpty = c.isEmpty || c.count === 0;
        const ariaLabel = isEmpty ? c.label : `${c.label}: ${c.count}`;
        return (
          <button
            key={c.entityType}
            type="button"
            aria-label={ariaLabel}
            style={{
              display:'inline-flex', alignItems:'center', gap: 6,
              padding:'6px 12px',  // py-1.5 px-3
              borderRadius: 999,    // rounded-full
              fontSize: 12, fontWeight: 700,  // text-xs font-bold
              fontFamily:'var(--f-display)',
              background: isEmpty ? 'transparent' : `hsl(${ec.h} ${ec.s}% ${ec.l}% / .1)`,
              color: `hsl(${ec.h} ${ec.s}% ${ec.l}%)`,
              border: isEmpty
                ? `1.5px dashed hsl(${ec.h} ${ec.s}% ${ec.l}% / .55)`
                : `1px solid hsl(${ec.h} ${ec.s}% ${ec.l}% / .2)`,
              opacity: isEmpty ? 0.6 : 1,
              cursor:'pointer',
              flexShrink: 0, whiteSpace:'nowrap',
              transition:'transform var(--dur-sm) var(--ease-out), box-shadow var(--dur-sm)',
              outlineOffset: 2,
            }}
            onMouseEnter={e => {
              e.currentTarget.style.transform = 'scale(1.03)';
              e.currentTarget.style.boxShadow = 'var(--shadow-md)';
            }}
            onMouseLeave={e => {
              e.currentTarget.style.transform = '';
              e.currentTarget.style.boxShadow = '';
            }}
          >
            {/* Icon — h-3.5 w-3.5 strokeWidth=2 */}
            <svg width="14" height="14" viewBox="0 0 24 24"
              fill="none" stroke="currentColor" strokeWidth="2"
              strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              {ec.em ? null : <circle cx="12" cy="12" r="9"/>}
            </svg>
            {/* Emoji fallback for visual coherence with rest of system */}
            <span aria-hidden="true" style={{ fontSize: 13, lineHeight: 1 }}>{ec.em}</span>

            {/* Count (tabular-nums) or Plus icon */}
            {isEmpty ? (
              <svg width="14" height="14" viewBox="0 0 24 24"
                fill="none" stroke="currentColor" strokeWidth="2"
                strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <line x1="12" y1="5" x2="12" y2="19"/>
                <line x1="5" y1="12" x2="19" y2="12"/>
              </svg>
            ) : (
              <span style={{
                fontFamily:'var(--f-mono)', fontVariantNumeric:'tabular-nums',
                fontWeight: 700,
              }}>{c.count}</span>
            )}
            <span>{c.label}</span>
          </button>
        );
      })}
    </div>
  );
};

// ─── BANNER (riuso) ──────────────────────────────────
const Banner = ({ tone='info', children, action }) => {
  const colors = { success:'c-toolkit', error:'c-danger', warning:'c-warning', info:'c-info' };
  const c = colors[tone];
  const icon = tone === 'success' ? '✓' : tone === 'error' ? '✕' : tone === 'warning' ? '!' : 'ℹ';
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} style={{
      display:'flex', alignItems:'flex-start', gap: 10,
      padding:'12px 14px', borderRadius:'var(--r-md)',
      background:`hsl(var(--${c}) / .1)`,
      border:`1px solid hsl(var(--${c}) / .25)`,
      color:`hsl(var(--${c}))`,
      fontSize: 13, fontWeight: 600,
    }}>
      <span aria-hidden="true" style={{
        fontSize: 11, lineHeight: 1, flexShrink: 0,
        width: 20, height: 20, borderRadius:'50%',
        background:`hsl(var(--${c}) / .18)`,
        display:'inline-flex', alignItems:'center', justifyContent:'center',
        marginTop: 1,
      }}>{icon}</span>
      <span style={{ flex: 1, lineHeight: 1.55 }}>{children}</span>
      {action}
    </div>
  );
};

// ─── STAR RATING ─────────────────────────────────────
const Stars = ({ value = 0, size = 11 }) => {
  const full = Math.floor(value);
  const half = value - full >= 0.5;
  return (
    <span style={{ display:'inline-flex', gap: 1, color:'hsl(var(--c-agent))', fontSize: size }}>
      {[0,1,2,3,4].map(i => (
        <span key={i} aria-hidden="true">
          {i < full ? '★' : (i === full && half ? '⯨' : '☆')}
        </span>
      ))}
    </span>
  );
};

// ═══════════════════════════════════════════════════════
// ─── HERO ────────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const GameHero = ({ game = GAME, compact, loading }) => {
  if (loading) {
    return (
      <div style={{ position:'relative' }}>
        <div className="mai-shimmer" style={{
          height: compact ? 160 : 220,
          background:'var(--bg-muted)',
        }}/>
        <div style={{
          padding: compact ? '16px 16px 8px' : '20px 32px 8px',
          display:'flex', flexDirection:'column', gap: 8,
        }}>
          <div className="mai-shimmer" style={{ height: 24, width:'50%', background:'var(--bg-muted)', borderRadius: 6 }}/>
          <div className="mai-shimmer" style={{ height: 14, width:'70%', background:'var(--bg-muted)', borderRadius: 4 }}/>
        </div>
      </div>
    );
  }
  return (
    <div style={{ position:'relative' }}>
      {/* Cover */}
      <div style={{
        height: compact ? 160 : 220,
        background: game.cover,
        position:'relative',
        display:'flex', alignItems:'center', justifyContent:'center',
        overflow:'hidden',
      }}>
        <span aria-hidden="true" style={{
          fontSize: compact ? 80 : 110,
          filter:'drop-shadow(0 6px 18px rgba(0,0,0,.3))',
          opacity: .92,
        }}>{game.coverEmoji}</span>
        {/* gradient overlay bottom */}
        <div style={{
          position:'absolute', inset: 0,
          background:'linear-gradient(180deg, transparent 50%, rgba(0,0,0,.25) 100%)',
        }}/>
      </div>
      {/* Title row */}
      <div style={{
        padding: compact ? '14px 16px 6px' : '20px 32px 8px',
      }}>
        <div style={{
          display:'flex', alignItems:'center', gap: 8, marginBottom: 6,
        }}>
          <span style={{
            display:'inline-flex', alignItems:'center', gap: 5,
            padding:'3px 9px', borderRadius:'var(--r-pill)',
            background:'hsl(var(--c-game) / .14)',
            color:'hsl(var(--c-game))',
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
            textTransform:'uppercase', letterSpacing:'.08em',
          }}>
            <span aria-hidden="true">🎲</span>Gioco
          </span>
          <Stars value={game.stars}/>
          <span style={{
            fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
            fontWeight: 700,
          }}>{game.rating}</span>
        </div>
        <h1 style={{
          fontFamily:'var(--f-display)', fontWeight: 800,
          fontSize: compact ? 22 : 30,
          letterSpacing:'-.02em', lineHeight: 1.1,
          color:'var(--text)', margin:'0 0 6px',
        }}>{game.title}</h1>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          letterSpacing:'.04em',
          display:'flex', flexWrap:'wrap', gap:'4px 10px',
        }}>
          <span>{game.author}</span>
          <span aria-hidden="true">·</span>
          <span>{game.year}</span>
          <span aria-hidden="true">·</span>
          <span>{game.players} pl</span>
          <span aria-hidden="true">·</span>
          <span>{game.duration}</span>
          <span aria-hidden="true">·</span>
          <span>complexity {game.weight?.toFixed(1)}/5</span>
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TABS V2 (rounded-full + animated underline) ─────
// ═══════════════════════════════════════════════════════
const Tabs = ({ tabs, active, onChange }) => {
  const refs = useRef({});
  const [ind, setInd] = useState({ left: 0, width: 0 });

  useEffect(() => {
    const el = refs.current[active];
    if (el) {
      setInd({ left: el.offsetLeft, width: el.offsetWidth });
    }
  }, [active, tabs.length]);

  return (
    <div style={{
      position:'relative',
      display:'flex', alignItems:'center', gap: 4,
      borderBottom:'1px solid var(--border)',
      overflowX:'auto', scrollbarWidth:'none',
    }} className="mai-cb-scroll">
      {tabs.map(t => {
        const isActive = t.id === active;
        return (
          <button
            key={t.id}
            type="button"
            ref={el => { refs.current[t.id] = el; }}
            onClick={() => onChange(t.id)}
            role="tab"
            aria-selected={isActive}
            style={{
              display:'inline-flex', alignItems:'center', gap: 7,
              padding:'10px 14px',
              background: isActive ? 'hsl(var(--c-game) / .08)' : 'transparent',
              border:'none',
              borderRadius:'var(--r-pill) var(--r-pill) 0 0',
              color: isActive ? 'hsl(var(--c-game))' : 'var(--text-sec)',
              fontFamily:'var(--f-display)', fontSize: 13,
              fontWeight: isActive ? 700 : 600,
              cursor:'pointer',
              whiteSpace:'nowrap', flexShrink: 0,
              transition:'all var(--dur-sm) var(--ease-out)',
            }}
          >
            <span aria-hidden="true">{t.icon}</span>
            <span>{t.label}</span>
            <span style={{
              padding:'1px 7px', borderRadius:'var(--r-pill)',
              background: isActive ? 'hsl(var(--c-game))' : 'var(--bg-muted)',
              color: isActive ? '#fff' : 'var(--text-muted)',
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              fontVariantNumeric:'tabular-nums',
            }}>{t.count}</span>
          </button>
        );
      })}
      {/* Animated underline */}
      <span aria-hidden="true" style={{
        position:'absolute', bottom: -1, left: ind.left, width: ind.width,
        height: 2, background:'hsl(var(--c-game))',
        transition:'left .3s cubic-bezier(.4,0,.2,1), width .3s cubic-bezier(.4,0,.2,1)',
        borderRadius:'2px 2px 0 0',
      }}/>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TOOLKITPUBLICLISTITEM ───────────────────────────
// ═══════════════════════════════════════════════════════
const ToolkitPublicListItem = ({ tk }) => {
  const ec = DS.EC.toolkit;
  return (
    <article style={{
      display:'flex', gap: 12, padding: 14,
      border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
      background:'var(--bg-card)',
      transition:'border-color var(--dur-sm), box-shadow var(--dur-sm)',
    }}
    onMouseEnter={e => { e.currentTarget.style.borderColor=`hsl(${ec.h} ${ec.s}% ${ec.l}% / .35)`; e.currentTarget.style.boxShadow='var(--shadow-xs)'; }}
    onMouseLeave={e => { e.currentTarget.style.borderColor='var(--border)'; e.currentTarget.style.boxShadow=''; }}
    >
      <div style={{
        width: 44, height: 44, borderRadius:'var(--r-md)',
        background:`hsl(${ec.h} ${ec.s}% ${ec.l}% / .12)`,
        color:`hsl(${ec.h} ${ec.s}% ${ec.l}%)`,
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 22, flexShrink: 0,
      }} aria-hidden="true">🧰</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          display:'flex', alignItems:'baseline', gap: 7, marginBottom: 3,
          flexWrap:'wrap',
        }}>
          <h3 style={{
            fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700,
            color:'var(--text)', margin: 0,
          }}>{tk.title}</h3>
          {tk.official ? (
            <span style={{
              padding:'1px 7px', borderRadius:'var(--r-pill)',
              background:'hsl(var(--c-info) / .14)', color:'hsl(var(--c-info))',
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
              textTransform:'uppercase', letterSpacing:'.06em',
            }}>✓ official</span>
          ) : (
            <span style={{
              padding:'1px 7px', borderRadius:'var(--r-pill)',
              background:'var(--bg-muted)', color:'var(--text-muted)',
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
              textTransform:'uppercase', letterSpacing:'.06em',
            }}>community</span>
          )}
          {tk.badge && (
            <span style={{
              padding:'1px 7px', borderRadius:'var(--r-pill)',
              background:'hsl(var(--c-event) / .14)', color:'hsl(var(--c-event))',
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
              textTransform:'uppercase', letterSpacing:'.06em',
            }}>{tk.badge}</span>
          )}
        </div>
        <div style={{
          fontSize: 11, color:'var(--text-muted)',
          fontFamily:'var(--f-mono)', letterSpacing:'.03em',
          marginBottom: 6,
        }}>
          di <strong style={{ color:'var(--text-sec)' }}>{tk.author}</strong>
          {' · v'}{tk.version}
          {' · '}{tk.tools} strumenti
          {' · '}aggiornato {tk.updated}
        </div>
        <p style={{
          fontSize: 12.5, color:'var(--text-sec)', lineHeight: 1.5,
          margin:'0 0 8px',
        }}>{tk.desc}</p>
        <div style={{
          display:'flex', alignItems:'center', gap: 12, flexWrap:'wrap',
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          fontWeight: 700,
        }}>
          <span style={{ display:'inline-flex', alignItems:'center', gap: 4 }}>
            <Stars value={tk.rating}/>
            <span>{tk.rating}</span>
            <span style={{ color:'var(--text-muted)', fontWeight: 500 }}>({tk.ratingCount})</span>
          </span>
          <span aria-hidden="true">·</span>
          <span>↓ {tk.installs.toLocaleString('it-IT')} install</span>
        </div>
      </div>
      <button type="button" style={{
        alignSelf:'center',
        padding:'8px 14px', borderRadius:'var(--r-md)',
        background:'transparent',
        border:`1.5px solid hsl(${ec.h} ${ec.s}% ${ec.l}%)`,
        color:`hsl(${ec.h} ${ec.s}% ${ec.l}%)`,
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
        cursor:'pointer', flexShrink: 0,
        transition:'all var(--dur-sm) var(--ease-out)',
      }}
      onMouseEnter={e => { e.currentTarget.style.background=`hsl(${ec.h} ${ec.s}% ${ec.l}% / .12)`; }}
      onMouseLeave={e => { e.currentTarget.style.background='transparent'; }}
      >Anteprima</button>
    </article>
  );
};

// ═══════════════════════════════════════════════════════
// ─── AGENTPUBLICLISTITEM ─────────────────────────────
// ═══════════════════════════════════════════════════════
const AgentPublicListItem = ({ ag }) => {
  const ec = DS.EC.agent;
  return (
    <article style={{
      display:'flex', gap: 12, padding: 14,
      border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
      background:'var(--bg-card)',
      transition:'border-color var(--dur-sm), box-shadow var(--dur-sm)',
    }}
    onMouseEnter={e => { e.currentTarget.style.borderColor=`hsl(${ec.h} ${ec.s}% ${ec.l}% / .35)`; e.currentTarget.style.boxShadow='var(--shadow-xs)'; }}
    onMouseLeave={e => { e.currentTarget.style.borderColor='var(--border)'; e.currentTarget.style.boxShadow=''; }}
    >
      <div style={{
        width: 44, height: 44, borderRadius:'var(--r-md)',
        background:`hsl(${ec.h} ${ec.s}% ${ec.l}% / .12)`,
        color:`hsl(${ec.h} ${ec.s}% ${ec.l}%)`,
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 22, flexShrink: 0,
      }} aria-hidden="true">🤖</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display:'flex', alignItems:'baseline', gap: 7, marginBottom: 3, flexWrap:'wrap' }}>
          <h3 style={{
            fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700,
            color:'var(--text)', margin: 0,
          }}>{ag.title}</h3>
          <span style={{
            padding:'1px 7px', borderRadius:'var(--r-pill)',
            background:'hsl(var(--c-agent) / .14)', color:'hsl(var(--c-agent))',
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
          }}>{ag.model}</span>
        </div>
        <div style={{
          fontSize: 11, color:'var(--text-muted)',
          fontFamily:'var(--f-mono)', letterSpacing:'.03em',
          marginBottom: 6,
        }}>di <strong style={{ color:'var(--text-sec)' }}>{ag.author}</strong></div>
        <p style={{
          fontSize: 12.5, color:'var(--text-sec)', lineHeight: 1.5,
          margin:'0 0 8px',
        }}>{ag.desc}</p>
        <div style={{
          display:'flex', flexWrap:'wrap', gap: 5, marginBottom: 8,
        }}>
          {ag.expertise.map(t => (
            <span key={t} style={{
              padding:'2px 8px', borderRadius:'var(--r-pill)',
              background:'var(--bg-muted)',
              color:'var(--text-sec)',
              fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 600,
            }}>{t}</span>
          ))}
        </div>
        <div style={{
          display:'flex', alignItems:'center', gap: 12,
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', fontWeight: 700,
        }}>
          <span style={{ display:'inline-flex', alignItems:'center', gap: 4 }}>
            <Stars value={ag.rating}/><span>{ag.rating}</span>
          </span>
          <span aria-hidden="true">·</span>
          <span>💬 {ag.convs.toLocaleString('it-IT')} conversazioni</span>
        </div>
      </div>
      <button type="button" style={{
        alignSelf:'center',
        padding:'8px 14px', borderRadius:'var(--r-md)',
        background:`hsl(${ec.h} ${ec.s}% ${ec.l}%)`,
        border:'none', color:'#fff',
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
        cursor:'pointer', flexShrink: 0,
        boxShadow:`0 3px 10px hsl(${ec.h} ${ec.s}% ${ec.l}% / .35)`,
      }}>Prova →</button>
    </article>
  );
};

// ═══════════════════════════════════════════════════════
// ─── KBPUBLICDOCITEM ─────────────────────────────────
// ═══════════════════════════════════════════════════════
const KBPublicDocItem = ({ kb }) => {
  const ec = DS.EC.kb;
  const kindIcon = kb.kind === 'PDF' ? '📄' : kb.kind === 'MD' ? '📝' : '🔗';
  return (
    <article style={{
      display:'flex', alignItems:'center', gap: 12, padding:'10px 14px',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
      background:'var(--bg-card)',
      transition:'border-color var(--dur-sm)',
    }}
    onMouseEnter={e => { e.currentTarget.style.borderColor=`hsl(${ec.h} ${ec.s}% ${ec.l}% / .35)`; }}
    onMouseLeave={e => { e.currentTarget.style.borderColor='var(--border)'; }}
    >
      <div style={{
        width: 36, height: 36, borderRadius:'var(--r-md)',
        background:`hsl(${ec.h} ${ec.s}% ${ec.l}% / .12)`,
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 18, flexShrink: 0,
      }} aria-hidden="true">{kindIcon}</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 13, fontWeight: 700,
          color:'var(--text)', marginBottom: 2,
          whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
        }}>{kb.title}</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          letterSpacing:'.04em',
        }}>
          <span style={{
            padding:'1px 5px', borderRadius: 3,
            background:`hsl(${ec.h} ${ec.s}% ${ec.l}% / .14)`,
            color:`hsl(${ec.h} ${ec.s}% ${ec.l}%)`,
            fontWeight: 700, marginRight: 6,
          }}>{kb.kind}</span>
          <span>{kb.lang}</span>
          {kb.pages && <><span aria-hidden="true"> · </span><span>{kb.pages} pag</span></>}
          {kb.size && <><span aria-hidden="true"> · </span><span>{kb.size}</span></>}
          {kb.url && <><span aria-hidden="true"> · </span><span>{kb.url}</span></>}
        </div>
      </div>
      <button type="button" style={{
        padding:'6px 12px', borderRadius:'var(--r-md)',
        background:'transparent', border:'1px solid var(--border)',
        color:'var(--text-sec)',
        fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700,
        cursor:'pointer', flexShrink: 0,
      }}>Apri ↗</button>
    </article>
  );
};

// ═══════════════════════════════════════════════════════
// ─── CONTRIBUTORSSTRIP ───────────────────────────────
// ═══════════════════════════════════════════════════════
const ContributorsStrip = ({ contributors, compact, max = 8 }) => {
  if (!contributors || contributors.length === 0) return null;
  const visible = contributors.slice(0, compact ? 5 : max);
  const overflow = contributors.length - visible.length;

  return (
    <div style={{
      display:'flex', alignItems:'center', gap: 12,
      padding:'14px 16px',
      background:'var(--bg-muted)',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
    }}>
      <div style={{ flexShrink: 0 }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
          marginBottom: 2,
        }}>Top contributors</div>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
          color:'var(--text)',
        }}>{contributors.length} player{contributors.length === 1 ? '' : 's'}</div>
      </div>
      {/* Avatar overlap stack */}
      <div style={{ display:'flex', alignItems:'center', flex: 1, minWidth: 0 }}>
        {visible.map((p, i) => (
          <div key={p.id} title={`${p.name} · ${p.contribs} contributi`} style={{
            width: 34, height: 34, borderRadius:'50%',
            background:`hsl(${p.color} 60% 55%)`,
            color:'#fff',
            display:'inline-flex', alignItems:'center', justifyContent:'center',
            fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 11,
            border:'2px solid var(--bg)',
            marginLeft: i === 0 ? 0 : -8,
            flexShrink: 0,
            position:'relative', zIndex: visible.length - i,
            cursor:'pointer',
            transition:'transform var(--dur-sm)',
          }}
          onMouseEnter={e => { e.currentTarget.style.transform='translateY(-2px)'; e.currentTarget.style.zIndex = 999; }}
          onMouseLeave={e => { e.currentTarget.style.transform=''; e.currentTarget.style.zIndex = visible.length - i; }}
          aria-label={`${p.name}, ${p.contribs} contributi`}
          >{p.initials}</div>
        ))}
        {overflow > 0 && (
          <div style={{
            width: 34, height: 34, borderRadius:'50%',
            background:'var(--bg-card)', border:'2px solid var(--bg)',
            color:'var(--text-muted)',
            display:'inline-flex', alignItems:'center', justifyContent:'center',
            fontFamily:'var(--f-mono)', fontWeight: 800, fontSize: 10,
            marginLeft: -8, flexShrink: 0,
          }}>+{overflow}</div>
        )}
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── EMPTY STATE PER TAB ─────────────────────────────
// ═══════════════════════════════════════════════════════
const TabEmpty = ({ kind }) => {
  const cfg = {
    toolkit:{ icon:'🧰', t:'Nessun toolkit ancora', sub:'Sii il primo a pubblicare un toolkit per questo gioco.' },
    agent:  { icon:'🤖', t:'Nessun agente ancora',  sub:'Pubblica un agente AI esperto per aiutare gli altri giocatori.' },
    kb:     { icon:'📄', t:'Nessun documento KB',   sub:'Carica regolamenti, FAQ o riferimenti per indicizzare la knowledge base.' },
  }[kind];
  return (
    <div style={{
      display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
      padding:'40px 20px',
    }}>
      <div style={{
        width: 64, height: 64, borderRadius:'50%',
        background:'var(--bg-muted)',
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 28, marginBottom: 14,
      }} aria-hidden="true">{cfg.icon}</div>
      <h3 style={{ fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 700, color:'var(--text)', margin:'0 0 4px' }}>{cfg.t}</h3>
      <p style={{ fontSize: 12.5, color:'var(--text-muted)', maxWidth: 320, margin: 0, lineHeight: 1.55 }}>{cfg.sub}</p>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── PAGE BODY ───────────────────────────────────────
// ═══════════════════════════════════════════════════════
const SharedGameDetailBody = ({ stateOverride, compact }) => {
  const [tab, setTab] = useState('toolkits');

  const isLoading = stateOverride === 'loading';
  const isError = stateOverride === 'error';
  const isNoContent = stateOverride === 'no-content';
  const ds = DATASETS[stateOverride === 'minimal' ? 'minimal' : (isNoContent ? 'no-content' : 'default')];

  const connections = useMemo(() => [
    { entityType:'toolkit', count: ds.toolkits.length, label:'Toolkit',  isEmpty: ds.toolkits.length === 0 },
    { entityType:'agent',   count: ds.agents.length,   label:'Agenti',   isEmpty: ds.agents.length === 0 },
    { entityType:'kb',      count: ds.kbs.length,      label:'Documenti',isEmpty: ds.kbs.length === 0 },
    { entityType:'player',  count: ds.contributors.length, label:'Contributors', isEmpty: ds.contributors.length === 0 },
  ], [stateOverride]);

  const tabs = [
    { id:'toolkits', icon:'🧰', label:'Toolkit',   count: ds.toolkits.length },
    { id:'agents',   icon:'🤖', label:'Agenti',    count: ds.agents.length },
    { id:'kbs',      icon:'📄', label:'Documenti', count: ds.kbs.length },
  ];

  return (
    <>
      <GameHero compact={compact} loading={isLoading}/>
      <div style={{
        padding: compact ? '0 16px' : '0 32px',
      }}>
        <ConnectionBar connections={connections} loading={isLoading}/>
      </div>

      <div style={{
        padding: compact ? '12px 16px 100px' : '16px 32px 80px',
        display:'flex', flexDirection:'column', gap: 16,
      }}>
        {isError && (
          <Banner tone="error">
            Impossibile caricare i contenuti pubblici per questo gioco.
            Riprova tra qualche istante o segnala il problema all'host.
          </Banner>
        )}

        {isNoContent && (
          <Banner tone="info" action={
            <a href="#" onClick={e=>e.preventDefault()} style={{
              padding:'5px 10px', borderRadius:'var(--r-md)',
              background:'hsl(var(--c-info))', color:'#fff',
              fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700,
              textDecoration:'none', whiteSpace:'nowrap',
            }}>Docs autore →</a>
          }>
            <strong>Nessun contenuto pubblicato</strong> per <strong>{GAME.title}</strong>.
            Sii il primo a pubblicare un toolkit, agente o KB!
          </Banner>
        )}

        {!isLoading && !isError && !isNoContent && (
          <>
            {/* Tabs */}
            <Tabs tabs={tabs} active={tab} onChange={setTab}/>

            {/* Tab content */}
            <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
              {tab === 'toolkits' && (ds.toolkits.length > 0
                ? ds.toolkits.map(tk => <ToolkitPublicListItem key={tk.id} tk={tk}/>)
                : <TabEmpty kind="toolkit"/>)}
              {tab === 'agents' && (ds.agents.length > 0
                ? ds.agents.map(ag => <AgentPublicListItem key={ag.id} ag={ag}/>)
                : <TabEmpty kind="agent"/>)}
              {tab === 'kbs' && (ds.kbs.length > 0
                ? ds.kbs.map(kb => <KBPublicDocItem key={kb.id} kb={kb}/>)
                : <TabEmpty kind="kb"/>)}
            </div>

            {/* Contributors */}
            {ds.contributors.length > 0 && (
              <ContributorsStrip contributors={ds.contributors} compact={compact}/>
            )}
          </>
        )}

        {isLoading && (
          <>
            {/* Tabs skeleton */}
            <div style={{ display:'flex', gap: 8, padding:'8px 0', borderBottom:'1px solid var(--border)' }}>
              {[0,1,2].map(i => (
                <div key={i} className="mai-shimmer" style={{
                  height: 32, width: 100, borderRadius: 'var(--r-pill)',
                  background:'var(--bg-muted)',
                }}/>
              ))}
            </div>
            {/* Card skeleton */}
            {[0,1,2].map(i => (
              <div key={i} className="mai-shimmer" style={{
                height: 92, borderRadius:'var(--r-lg)',
                background:'var(--bg-muted)',
              }}/>
            ))}
          </>
        )}
      </div>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── STICKY CTA ──────────────────────────────────────
// ═══════════════════════════════════════════════════════
const StickyCTAMobile = () => (
  <div style={{
    position:'absolute', bottom: 0, left: 0, right: 0,
    padding:'10px 14px',
    background:'var(--glass-bg)', backdropFilter:'blur(14px)',
    borderTop:'1px solid var(--border)',
    zIndex: 5,
  }}>
    <button type="button" style={{
      width:'100%', padding:'12px 14px', fontSize: 14,
      borderRadius:'var(--r-md)',
      fontFamily:'var(--f-display)', fontWeight: 700, border:'none',
      background:'hsl(var(--c-game))', color:'#fff',
      boxShadow:'0 6px 20px hsl(var(--c-game) / .4)',
      cursor:'pointer',
    }}>🔒 Accedi per installare</button>
  </div>
);

const FloatingCTADesktop = () => (
  <div style={{
    position:'absolute', bottom: 24, right: 24,
    display:'flex', alignItems:'center', gap: 10,
    padding:'10px 14px',
    background:'var(--bg-card)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-pill)',
    boxShadow:'var(--shadow-lg)',
    zIndex: 5,
  }}>
    <span aria-hidden="true" style={{ fontSize: 16 }}>🔒</span>
    <span style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      fontWeight: 600,
    }}>Accedi per installare</span>
    <button type="button" style={{
      padding:'7px 14px', borderRadius:'var(--r-pill)',
      background:'hsl(var(--c-game))', color:'#fff',
      border:'none', fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
      cursor:'pointer',
      boxShadow:'0 3px 10px hsl(var(--c-game) / .35)',
    }}>Accedi →</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── MOBILE / DESKTOP SHELLS ─────────────────────────
// ═══════════════════════════════════════════════════════
const PhoneSbar = () => (
  <div className="phone-sbar">
    <span style={{ fontFamily:'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);
const PhoneTopNav = () => (
  <div style={{
    display:'flex', alignItems:'center', gap: 9,
    padding:'10px 14px', borderBottom:'1px solid var(--border)',
    background:'var(--bg)',
    position:'sticky', top: 0, zIndex: 4,
  }}>
    <button aria-label="Indietro" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'transparent', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>←</button>
    <div style={{ flex: 1, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', textAlign:'center' }}>
      shared-games / wingspan
    </div>
    <button aria-label="Condividi" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'transparent', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>↗</button>
  </div>
);

const MobileScreen = ({ stateOverride }) => (
  <>
    <PhoneSbar/>
    <div style={{
      flex: 1, overflowY:'auto', scrollbarWidth:'none',
      background:'var(--bg)', position:'relative',
    }}>
      <PhoneTopNav/>
      <SharedGameDetailBody stateOverride={stateOverride} compact/>
      <StickyCTAMobile/>
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

const DesktopNav = () => (
  <div style={{
    display:'flex', alignItems:'center', gap: 14,
    padding:'12px 32px',
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
      <span style={{ fontFamily:'var(--f-display)', fontWeight: 700, fontSize: 14 }}>MeepleAI</span>
    </div>
    <div style={{
      flex: 1, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      marginLeft: 22,
    }}>
      <a href="#" style={{ color:'inherit' }}>Catalogo</a>
      <span aria-hidden="true"> / </span>
      <strong style={{ color:'var(--text-sec)' }}>{GAME.title}</strong>
    </div>
    <a href="#" onClick={e=>e.preventDefault()} style={{
      padding:'7px 14px', borderRadius:'var(--r-md)',
      color:'#fff', fontSize: 13, fontWeight: 700, fontFamily:'var(--f-display)',
      background:'hsl(var(--c-game))',
      boxShadow:'0 3px 10px hsl(var(--c-game) / .3)',
      textDecoration:'none',
    }}>Accedi / Registrati</a>
  </div>
);

const DesktopScreen = ({ stateOverride }) => (
  <div style={{ position:'relative', minHeight: 720 }}>
    <DesktopNav/>
    <SharedGameDetailBody stateOverride={stateOverride}/>
    <FloatingCTADesktop/>
  </div>
);

const DesktopFrame = ({ children, label, desc }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width:'100%', maxWidth: 1280,
      borderRadius:'var(--r-xl)',
      border:'1px solid var(--border)',
      background:'var(--bg)',
      overflow:'hidden',
      boxShadow:'var(--shadow-lg)',
    }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        padding:'9px 14px',
        background:'var(--bg-muted)',
        borderBottom:'1px solid var(--border)',
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      }}>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-event))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-warning))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-toolkit))' }}/>
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/shared-games/wingspan</span>
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
// ═══════════════════════════════════════════════════════
const MOBILE_STATES = [
  { id:'default',    label:'01 · Default (rich)',  desc:'4 toolkit, 2 agenti, 7 docs, 8 contributors. Tab default toolkits. ConnectionBar tutta piena.' },
  { id:'minimal',    label:'02 · Minimal',         desc:'1 toolkit, 0 agenti (pip empty: dashed border + Plus icon, opacity .6), 1 doc, 1 contributor.' },
  { id:'no-content', label:'03 · No content',      desc:'Banner info "Sii il primo" + CTA Docs autore. Niente tabs renderizzati. ConnectionBar mostra TUTTI pip empty.' },
  { id:'loading',    label:'04 · Loading',         desc:'Skeleton hero (cover gray + 2 line) + ConnectionBar (4 pill grigi shimmer) + tabs skeleton + 3 card placeholders.' },
  { id:'error',      label:'05 · Error',           desc:'Banner error full-width + CTA "ritenta" implicito (refresh). Hero rimane visibile.' },
];

function ThemeToggle() {
  const [dark, setDark] = useState(false);
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle"
      aria-label={dark ? 'Passa a tema chiaro' : 'Passa a tema scuro'}>
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
        }}>SP3 · Public Secondary · #4 — CRITICO (ConnectionBar prod)</div>
        <h1>Shared Game Detail — /shared-games/[slug]</h1>
        <p className="lead">
          5 stati: default · minimal · no-content · loading · error.
          <strong> ConnectionBar</strong> riprodotta 1:1 dalla spec PR #549/#552:
          rounded-full px-3 py-1.5, bg entityHsl 10% / fg solid, hover scale 1.03,
          empty pip = dashed border + Plus icon. Componenti v2 nuovi: <strong>Tabs</strong> (animated underline),
          <strong> ToolkitPublicListItem</strong>, <strong>AgentPublicListItem</strong>, <strong>KBPublicDocItem</strong>, <strong>ContributorsStrip</strong>.
        </p>

        <div className="section-label">Mobile · 375 — 5 stati</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <MobileScreen stateOverride={s.id}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1440 — full width</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="06 · Desktop · Default"
            desc="Floating CTA bottom-right: pill compatto con icona lucchetto + accedi. Hero 220px + ConnectionBar full + tabs animated + lista toolkit.">
            <DesktopScreen stateOverride="default"/>
          </DesktopFrame>
          <DesktopFrame label="07 · Desktop · Minimal"
            desc="ConnectionBar mostra agent pip dashed empty (Plus icon + opacity .6), kb pip pieno (1), player pip pieno (1). Tab agenti vuoto = TabEmpty illustrato.">
            <DesktopScreen stateOverride="minimal"/>
          </DesktopFrame>
          <DesktopFrame label="08 · Desktop · No content"
            desc="Banner info + CTA action inline. ConnectionBar tutta empty (4 pip dashed). Niente tabs, niente lista. Hero rimane.">
            <DesktopScreen stateOverride="no-content"/>
          </DesktopFrame>
          <DesktopFrame label="09 · Desktop · Loading"
            desc="Hero skeleton (cover 220px gray + 2 lines) + ConnectionBar 4 pill shimmer + 3 tab pills skeleton + 3 card 92px shimmer.">
            <DesktopScreen stateOverride="loading"/>
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
        button:focus-visible, a:focus-visible {
          outline: 2px solid currentColor;
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
