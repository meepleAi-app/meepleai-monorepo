/* ===================================================================
   SP7 Game Night — Create wizard (sp7-game-night-create.jsx)
   Route: /game-nights/new
   Persona: Marco, 35, host gruppo board game (desktop primary, mobile fallback)
   Coverage: US-31 Game Nights P1 — Sprint N+1
   Pattern desktop: split-form 12-col (left form 8-col + right preview RSVP-card live 4-col)
   Pattern mobile : multistep wizard 4 step (StepIndicator riusato da SP6-B)

   Stati (9 mobile + 1 desktop):
   - state-01-step1-date           Step 1 default (date+time picker)
   - state-02-step1-warning        Step 1 con warning conflict reschedule (var(--c-warning))
   - state-03-step2-location       Step 2 toggle location (4 opzioni)
   - state-04-step3-empty          Step 3 picker empty + suggestions regulars
   - state-05-step3-typing         Step 3 autocomplete dropdown attivo
   - state-06-step3-filled         Step 3 con 6 invitati (mix registered + email)
   - state-07-step4-games          Step 4 game candidates picker (3 selected da library)
   - state-08-step4-decide-group   Step 4 toggle "lascia decidere al gruppo" attivo
   - state-09-mobile-step-flow     Tutti i 4 step affiancati (overview compatto)
   - state-10-desktop-split        Desktop split-form (form + preview RSVP card live)

   Entity color mapping:
   - game-night event       = entityHsl('event')   rose
   - game candidates picker = entityHsl('game')    orange
   - party invitati tags    = entityHsl('player')  violet
   - warnings reschedule    = var(--c-warning)     amber

   Componenti v2 nuovi (per impl post-merge):
   - GameNightCreateWizard       → apps/web/src/components/ui/v2/game-night-create-wizard/
   - GameNightDateTimePicker     → apps/web/src/components/ui/v2/game-night-datetime-picker/
   - GameNightLocationToggle     → apps/web/src/components/ui/v2/game-night-location-toggle/
   - PlayerInviteAutocomplete    → apps/web/src/components/ui/v2/player-invite-autocomplete/
   - GameCandidatesPicker        → apps/web/src/components/ui/v2/game-candidates-picker/
   - RSVPCardLivePreview         → apps/web/src/components/ui/v2/rsvp-card-live-preview/

   Riusi pattern:
   - StepIndicator (SP6-B pu-stepbar) — adattato 3→4 step
   - Search autocomplete pattern (sp4-game-detail Library)
   - EntityChip game/player (wave 1)
   - Tabs animated underline (wave 1)
   =================================================================== */

const { useState, useEffect, useMemo, useRef } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.event;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ─── FIXTURE Marco persona ───────────────────────────────
const MARCO = DS.byId['p-marco'] || { id:'p-marco', initials:'MR', title:'Marco R.', color: 262 };

// Party (pulled from data.js + extended with extras for SP7-A realism)
const REGULARS = [
  { id:'p-marco',   initials:'MR', name:'Marco R.',    sub:'Tu · host',                   color: 262, fav:'Wingspan',   regular:true,  isHost:true },
  { id:'p-giulia',  initials:'GM', name:'Giulia M.',   sub:'Regular · 12 partite',        color: 10,  fav:'Brass',      regular:true },
  { id:'p-luca',    initials:'LB', name:'Luca B.',     sub:'Regular · 54 partite',        color: 180, fav:'Catan',      regular:true },
  { id:'p-sara',    initials:'ST', name:'Sara T.',     sub:'Regular · 142 partite',       color: 320, fav:'Wingspan',   regular:true },
  { id:'p-davide',  initials:'DC', name:'Davide C.',   sub:'Regular · 38 partite',        color: 200, fav:'Spirit Island', regular:true },
  { id:'p-federica',initials:'FN', name:'Federica N.', sub:'Casual · 6 partite',          color: 290, fav:'Azul',       regular:false },
  { id:'p-aaron',   initials:'AK', name:'Aaron K.',    sub:'Nuovo · 2 partite',           color: 140, fav:null,         regular:false },
  { id:'p-matteo',  initials:'MZ', name:'Matteo Z.',   sub:'Casual · 11 partite',         color: 60,  fav:'7 Wonders',  regular:false },
];

// Library (subset filtered by Marco's collection — match brief "Twilight, Brass, Spirit, Wingspan")
const LIBRARY = [
  { id:'g-twilight', title:'Twilight Imperium 4',  publisher:'Fantasy Flight', emoji:'🌌', cover: ['hsl(262 45% 22%)','hsl(220 70% 38%)'], players:'3–6', duration:'4–8h',     weight: 4.31, durMin: 240, plMin: 3, plMax: 6 },
  { id:'g-brass',    title:'Brass: Birmingham',    publisher:'Roxley',         emoji:'🏭', cover: ['hsl(220 35% 28%)','hsl(28 60% 38%)'],  players:'2–4', duration:'120–180m', weight: 3.91, durMin: 120, plMin: 2, plMax: 4 },
  { id:'g-spirit',   title:'Spirit Island',        publisher:'GMT',            emoji:'🌋', cover: ['hsl(210 50% 30%)','hsl(150 50% 38%)'], players:'1–4', duration:'90–120m',  weight: 4.08, durMin: 90,  plMin: 1, plMax: 4 },
  { id:'g-wingspan', title:'Wingspan',             publisher:'Stonemaier',     emoji:'🦜', cover: ['hsl(85 40% 45%)','hsl(35 60% 50%)'],   players:'1–5', duration:'40–70m',   weight: 2.42, durMin: 40,  plMin: 1, plMax: 5 },
  { id:'g-arknova',  title:'Ark Nova',             publisher:'Feuerland',      emoji:'🦒', cover: ['hsl(130 45% 32%)','hsl(85 40% 45%)'], players:'1–4', duration:'90–150m',  weight: 3.72, durMin: 90,  plMin: 1, plMax: 4 },
  { id:'g-azul',     title:'Azul',                 publisher:'Plan B',         emoji:'🔷', cover: ['hsl(200 70% 45%)','hsl(220 60% 30%)'], players:'2–4', duration:'30–45m',   weight: 1.77, durMin: 30,  plMin: 2, plMax: 4 },
];

// ═══════════════════════════════════════════════════════
// PRIMITIVES
// ═══════════════════════════════════════════════════════

// /* v2: StepIndicator (SP6-B reuse, 3→4 step) → apps/web/src/components/ui/v2/step-indicator/ */
const StepIndicator = ({ current, onBack, mobile }) => {
  const steps = [
    { n: 1, label: 'Quando', icon: '📅' },
    { n: 2, label: 'Dove',   icon: '📍' },
    { n: 3, label: 'Chi',    icon: '👥' },
    { n: 4, label: 'Cosa',   icon: '🎲' },
  ];
  return (
    <div style={{
      display:'flex', alignItems:'center', gap: 12,
      padding: mobile ? '12px 14px' : '16px 24px',
      background:'var(--bg)',
      borderBottom:'1px solid var(--border)',
      position:'sticky', top: 0, zIndex: 10,
    }}>
      {onBack && (
        <button type="button" onClick={typeof onBack === 'function' ? onBack : undefined} aria-label="Indietro" style={{
          width: 34, height: 34, borderRadius:'var(--r-md)',
          background:'var(--bg-muted)', border:'1px solid var(--border)',
          color:'var(--text)', fontSize: 16, cursor:'pointer',
          flexShrink: 0,
        }}>←</button>
      )}
      <div style={{
        display:'flex', alignItems:'center', gap: mobile ? 4 : 8, flex: 1,
        minWidth: 0,
      }}>
        {steps.map((s, i) => {
          const done = s.n < current;
          const active = s.n === current;
          const accent = entityHsl('event');
          return (
            <React.Fragment key={s.n}>
              <div style={{
                display:'flex', alignItems:'center', gap: 6,
                minWidth: 0, flexShrink: active ? 0 : 1,
              }}>
                <span style={{
                  width: 24, height: 24, borderRadius:'50%',
                  background: done || active ? accent : 'transparent',
                  color: done || active ? '#fff' : 'var(--text-muted)',
                  border: `2px solid ${done || active ? accent : 'var(--border)'}`,
                  display:'flex', alignItems:'center', justifyContent:'center',
                  fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 11,
                  flexShrink: 0,
                }}>{done ? '✓' : s.n}</span>
                {(!mobile || active) && (
                  <span style={{
                    fontFamily:'var(--f-display)', fontWeight: active ? 800 : 600,
                    fontSize: 12, color: active ? 'var(--text)' : 'var(--text-muted)',
                    whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
                  }}>{s.label}</span>
                )}
              </div>
              {i < steps.length - 1 && (
                <span style={{
                  flex: 1, height: 2, minWidth: mobile ? 8 : 16,
                  background: s.n < current ? accent : 'var(--border)',
                  borderRadius: 1,
                }}/>
              )}
            </React.Fragment>
          );
        })}
      </div>
      {!mobile && (
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 800,
          flexShrink: 0,
        }}>Step {current}/4</span>
      )}
    </div>
  );
};

const StepHeader = ({ title, sub }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 4, marginBottom: 4 }}>
    <h2 style={{
      fontFamily:'var(--f-display)', fontSize: 22, fontWeight: 800,
      color:'var(--text)', margin: 0, letterSpacing:'-.01em',
    }}>{title}</h2>
    <p style={{
      fontSize: 13, color:'var(--text-sec)', margin: 0, lineHeight: 1.5,
    }}>{sub}</p>
  </div>
);

const Avatar = ({ player, size = 32 }) => (
  <span style={{
    width: size, height: size, borderRadius:'50%',
    background: `hsl(${player.color}, 60%, 55%)`,
    color:'#fff', fontFamily:'var(--f-display)',
    fontWeight: 800, fontSize: size <= 24 ? 9 : size <= 32 ? 11 : 13,
    display:'inline-flex', alignItems:'center', justifyContent:'center',
    border:'2px solid var(--bg-card)', flexShrink: 0,
  }}>{player.initials}</span>
);

const EmailAvatar = ({ email, size = 32 }) => (
  <span style={{
    width: size, height: size, borderRadius:'50%',
    background:'var(--bg-muted)',
    color:'var(--text-sec)',
    fontFamily:'var(--f-display)', fontWeight: 800,
    fontSize: size <= 24 ? 9 : 11,
    display:'inline-flex', alignItems:'center', justifyContent:'center',
    border:`2px dashed ${entityHsl('player', 0.4)}`,
    flexShrink: 0,
  }} title={email}>@</span>
);

// ═══════════════════════════════════════════════════════
// STEP 1 — QUANDO?
// /* v2: GameNightDateTimePicker → apps/web/src/components/ui/v2/... */
// ═══════════════════════════════════════════════════════

const MiniCalendar = ({ selectedDay = 17, monthLabel = 'Maggio 2026', warningDay = null }) => {
  // May 2026 starts Friday May 1 → ISO Mon-week starts Apr 27.
  const cells = [];
  for (let d = 27; d <= 30; d++) cells.push({ day: d, otherMonth: true });
  for (let d = 1; d <= 31; d++) cells.push({ day: d, otherMonth: false });
  const pad = 42 - cells.length;
  for (let d = 1; d <= pad; d++) cells.push({ day: d, otherMonth: true, isJun: true });
  const labels = ['L','M','M','G','V','S','D'];
  const accent = entityHsl('event');
  return (
    <div style={{
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
      padding: 14,
      display:'flex', flexDirection:'column', gap: 10,
    }}>
      <div style={{
        display:'flex', alignItems:'center', justifyContent:'space-between',
      }}>
        <button type="button" aria-label="Mese precedente" style={{
          width: 28, height: 28, borderRadius:'var(--r-sm)',
          background:'var(--bg-muted)', border:'1px solid var(--border)',
          color:'var(--text)', fontSize: 13, cursor:'pointer',
        }}>‹</button>
        <h3 style={{
          margin: 0, fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
          color:'var(--text)',
        }}>{monthLabel}</h3>
        <button type="button" aria-label="Mese successivo" style={{
          width: 28, height: 28, borderRadius:'var(--r-sm)',
          background:'var(--bg-muted)', border:'1px solid var(--border)',
          color:'var(--text)', fontSize: 13, cursor:'pointer',
        }}>›</button>
      </div>
      <div style={{
        display:'grid', gridTemplateColumns:'repeat(7, 1fr)', gap: 2,
      }}>
        {labels.map((d, i) => (
          <div key={i} style={{
            padding:'4px 0', textAlign:'center',
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
            color:'var(--text-muted)', textTransform:'uppercase',
          }}>{d}</div>
        ))}
        {cells.map((cell, i) => {
          const isSelected = !cell.otherMonth && cell.day === selectedDay;
          const isWarning = !cell.otherMonth && cell.day === warningDay;
          return (
            <button key={i} type="button" disabled={cell.otherMonth}
              style={{
                aspectRatio:'1/1', minHeight: 28,
                padding: 0, border:'none', cursor: cell.otherMonth ? 'default' : 'pointer',
                borderRadius:'var(--r-sm)',
                background: isSelected ? accent : isWarning ? 'hsl(var(--c-warning) / .15)' : 'transparent',
                color: isSelected ? '#fff'
                     : isWarning ? 'hsl(var(--c-warning))'
                     : cell.otherMonth ? 'var(--text-muted)' : 'var(--text)',
                fontFamily:'var(--f-mono)', fontSize: 11,
                fontWeight: isSelected || isWarning ? 800 : 600,
                opacity: cell.otherMonth ? 0.35 : 1,
                position:'relative',
              }}>
              {cell.day}
              {isWarning && !isSelected && (
                <span aria-hidden="true" style={{
                  position:'absolute', top: 2, right: 3,
                  width: 4, height: 4, borderRadius:'50%',
                  background:'hsl(var(--c-warning))',
                }}/>
              )}
            </button>
          );
        })}
      </div>
    </div>
  );
};

const TimeField = ({ value = '21:00' }) => (
  <div style={{
    display:'flex', alignItems:'center', gap: 8,
    padding:'12px 14px',
    background:'var(--bg-card)',
    border:`1px solid ${entityHsl('event', 0.35)}`,
    borderRadius:'var(--r-lg)',
    boxShadow: `0 0 0 3px ${entityHsl('event', 0.1)}`,
  }}>
    <span aria-hidden="true" style={{ fontSize: 16, color: entityHsl('event') }}>🕘</span>
    <span style={{
      fontFamily:'var(--f-mono)', fontSize: 16, fontWeight: 800,
      color:'var(--text)', flex: 1, fontVariantNumeric:'tabular-nums',
    }}>{value}</span>
    <span style={{
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
      color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
    }}>Inizio</span>
  </div>
);

const DurationToggle = ({ selected = '3h' }) => {
  const options = [
    { id:'2h',   label:'2h',         sub:'Veloce' },
    { id:'3h',   label:'3h',         sub:'Standard' },
    { id:'4h',   label:'4h',         sub:'Lunga' },
    { id:'day',  label:'Giornata',   sub:'8h+' },
  ];
  return (
    <div style={{
      display:'grid', gridTemplateColumns:'repeat(4, 1fr)', gap: 8,
    }}>
      {options.map(o => {
        const active = o.id === selected;
        return (
          <button key={o.id} type="button" aria-pressed={active}
            style={{
              padding:'10px 6px', borderRadius:'var(--r-md)',
              background: active ? entityHsl('event', 0.12) : 'var(--bg-card)',
              color: active ? entityHsl('event') : 'var(--text-sec)',
              border: active
                ? `1.5px solid ${entityHsl('event', 0.5)}`
                : '1px solid var(--border)',
              boxShadow: active ? `0 0 0 3px ${entityHsl('event', 0.08)}` : 'none',
              cursor:'pointer',
              display:'flex', flexDirection:'column', alignItems:'center', gap: 2,
              fontFamily:'var(--f-display)',
            }}>
            <span style={{ fontSize: 14, fontWeight: 800 }}>{o.label}</span>
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
              textTransform:'uppercase', letterSpacing:'.06em',
              color: active ? entityHsl('event') : 'var(--text-muted)',
            }}>{o.sub}</span>
          </button>
        );
      })}
    </div>
  );
};

const Step1Quando = ({ withWarning = false, mobile }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 18, padding: mobile ? '16px 14px 24px' : '20px 24px 24px' }}>
    <StepHeader
      title="Quando vuoi giocare?"
      sub="Scegli data e ora. Avviseremo gli altri giocatori in tempo reale."
    />

    {/* Date row */}
    <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
      <Label>Data</Label>
      <div style={{
        display:'flex', alignItems:'center', gap: 10,
        padding:'12px 14px',
        background:'var(--bg-card)',
        border:`1px solid ${entityHsl('event', 0.35)}`,
        borderRadius:'var(--r-lg)',
        boxShadow: `0 0 0 3px ${entityHsl('event', 0.1)}`,
      }}>
        <span aria-hidden="true" style={{ fontSize: 18, color: entityHsl('event') }}>📅</span>
        <div style={{ flex: 1, display:'flex', flexDirection:'column', gap: 1 }}>
          <span style={{
            fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800, color:'var(--text)',
          }}>Sabato 17 maggio 2026</span>
          <span style={{
            fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
            fontWeight: 700, textTransform:'uppercase', letterSpacing:'.06em',
          }}>10 giorni · weekend</span>
        </div>
        <button type="button" style={{
          padding:'6px 10px', borderRadius:'var(--r-sm)',
          background:'transparent', border:'1px solid var(--border)',
          color:'var(--text-sec)', fontFamily:'var(--f-display)',
          fontSize: 11, fontWeight: 700, cursor:'pointer',
        }}>Cambia</button>
      </div>
      <MiniCalendar selectedDay={17} warningDay={withWarning ? 22 : null}/>
    </div>

    {/* Time + duration */}
    <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
      <Label>Ora di inizio</Label>
      <TimeField value="21:00"/>
    </div>

    <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
      <Label>Durata stimata</Label>
      <DurationToggle selected="3h"/>
    </div>

    {/* WARNING banner — variant state-02 */}
    {withWarning && (
      <div role="alert" style={{
        display:'flex', gap: 10, padding:'12px 14px',
        background:'hsl(var(--c-warning) / .12)',
        border:'1px solid hsl(var(--c-warning) / .35)',
        borderRadius:'var(--r-lg)',
      }}>
        <span aria-hidden="true" style={{
          fontSize: 18, color:'hsl(var(--c-warning))', flexShrink: 0, marginTop: 1,
        }}>⚠</span>
        <div style={{ flex: 1, display:'flex', flexDirection:'column', gap: 8 }}>
          <div>
            <div style={{
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
              color:'var(--text)',
            }}>Conflitto rilevato il <strong>22 maggio</strong></div>
            <div style={{
              fontSize: 12, color:'var(--text-sec)', lineHeight: 1.5, marginTop: 2,
            }}>
              <strong>Catan Maratona</strong> presso Casa Luca · 4 giocatori sovrapposti
              (Giulia, Davide, Sara, Luca). Riprogramma o ignora.
            </div>
          </div>
          <div style={{ display:'flex', gap: 6, flexWrap:'wrap' }}>
            <button type="button" style={{
              padding:'5px 10px', borderRadius:'var(--r-sm)',
              background:'hsl(var(--c-warning))', color:'#fff', border:'none',
              fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800, cursor:'pointer',
            }}>↻ Riprogramma</button>
            <button type="button" style={{
              padding:'5px 10px', borderRadius:'var(--r-sm)',
              background:'transparent', border:'1px solid hsl(var(--c-warning) / .4)',
              color:'hsl(var(--c-warning))',
              fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700, cursor:'pointer',
            }}>Ignora — non si sovrappone</button>
          </div>
        </div>
      </div>
    )}
  </div>
);

const Label = ({ children }) => (
  <label style={{
    fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
    color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
  }}>{children}</label>
);

// ═══════════════════════════════════════════════════════
// STEP 2 — DOVE?
// /* v2: GameNightLocationToggle → apps/web/src/components/ui/v2/... */
// ═══════════════════════════════════════════════════════

const LOCATION_OPTIONS = [
  { id:'host',    icon:'🏠', label:'Casa host',     sub:'Casa Marco · Padova',    detail:'Via Cesare Battisti 12 · 80m²' },
  { id:'member',  icon:'👥', label:'Casa membro',   sub:'Sceglie un partecipante', detail:'Decideremo a maggioranza' },
  { id:'custom',  icon:'📍', label:'Indirizzo',     sub:'Custom location',         detail:'Pub, ludoteca, sala B&B…' },
  { id:'online',  icon:'🌐', label:'Online · TTS',  sub:'Tabletop Simulator',      detail:'Voice chat + dadi virtuali' },
];

const Step2Dove = ({ selected = 'host', mobile }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 18, padding: mobile ? '16px 14px 24px' : '20px 24px 24px' }}>
    <StepHeader
      title="Dove vi vedete?"
      sub="Casa di qualcuno, un locale, o online. Puoi precisare l'indirizzo dopo."
    />

    <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
      {LOCATION_OPTIONS.map(opt => {
        const active = opt.id === selected;
        return (
          <button key={opt.id} type="button" aria-pressed={active}
            style={{
              padding:'14px', borderRadius:'var(--r-lg)',
              background: active ? entityHsl('event', 0.06) : 'var(--bg-card)',
              border: active
                ? `1.5px solid ${entityHsl('event', 0.5)}`
                : '1px solid var(--border)',
              boxShadow: active ? `0 0 0 3px ${entityHsl('event', 0.08)}` : 'none',
              cursor:'pointer', textAlign:'left',
              display:'flex', alignItems:'center', gap: 12,
              transition:'border-color var(--dur-sm), background var(--dur-sm)',
            }}>
            <div style={{
              width: 44, height: 44, borderRadius:'var(--r-md)',
              background: active ? entityHsl('event', 0.14) : 'var(--bg-muted)',
              display:'flex', alignItems:'center', justifyContent:'center',
              fontSize: 20, flexShrink: 0,
            }} aria-hidden="true">{opt.icon}</div>
            <div style={{ flex: 1, minWidth: 0, display:'flex', flexDirection:'column', gap: 2 }}>
              <div style={{
                fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
                color:'var(--text)',
              }}>{opt.label}</div>
              <div style={{
                fontSize: 12, color: active ? entityHsl('event') : 'var(--text-sec)',
                fontWeight: 700,
              }}>{opt.sub}</div>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 600,
                color:'var(--text-muted)',
              }}>{opt.detail}</div>
            </div>
            <span style={{
              width: 22, height: 22, borderRadius:'50%',
              border: active ? `2px solid ${entityHsl('event')}` : '2px solid var(--border-strong)',
              background: active ? entityHsl('event') : 'transparent',
              display:'flex', alignItems:'center', justifyContent:'center',
              color:'#fff', fontSize: 12, fontWeight: 800, flexShrink: 0,
            }}>{active ? '✓' : ''}</span>
          </button>
        );
      })}
    </div>

    {/* Map preview when host */}
    {selected === 'host' && (
      <div style={{
        borderRadius:'var(--r-lg)',
        background:'linear-gradient(135deg, hsl(85 30% 70%), hsl(120 25% 55%))',
        border:'1px solid var(--border)',
        height: 120, position:'relative', overflow:'hidden',
      }}>
        <div aria-hidden="true" style={{
          position:'absolute', inset: 0,
          background: `repeating-linear-gradient(45deg,
            transparent 0 8px,
            rgba(255,255,255,0.06) 8px 9px),
            repeating-linear-gradient(-45deg,
            transparent 0 8px,
            rgba(0,0,0,0.05) 8px 9px)`,
        }}/>
        <div style={{
          position:'absolute', top:'50%', left:'50%',
          transform:'translate(-50%, -50%)',
          width: 28, height: 28, borderRadius:'50% 50% 50% 0',
          background: entityHsl('event'),
          transform:'translate(-50%, -100%) rotate(-45deg)',
          boxShadow:'0 4px 12px rgba(0,0,0,.3)',
        }}/>
        <div style={{
          position:'absolute', bottom: 8, left: 10,
          padding:'4px 8px', borderRadius:'var(--r-sm)',
          background:'rgba(255,255,255,.92)',
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color:'#2b1f12',
        }}>📍 Via Cesare Battisti 12, Padova</div>
      </div>
    )}
  </div>
);

// ═══════════════════════════════════════════════════════
// STEP 3 — CHI?
// /* v2: PlayerInviteAutocomplete → apps/web/src/components/ui/v2/... */
// ═══════════════════════════════════════════════════════

const PlayerChip = ({ player, removable = true, isHost = false }) => (
  <span style={{
    display:'inline-flex', alignItems:'center', gap: 7,
    padding:'4px 4px 4px 8px', borderRadius:'var(--r-pill)',
    background: entityHsl('player', 0.12),
    color: entityHsl('player'),
    border:`1px solid ${entityHsl('player', 0.25)}`,
    fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
    whiteSpace:'nowrap',
  }}>
    <Avatar player={player} size={22}/>
    <span>{player.name}</span>
    {isHost && (
      <span style={{
        fontFamily:'var(--f-mono)', fontSize: 8, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.06em',
        background: entityHsl('player', 0.2),
        padding:'1px 5px', borderRadius:'var(--r-sm)',
      }}>HOST</span>
    )}
    {removable && !isHost && (
      <button type="button" aria-label={`Rimuovi ${player.name}`} style={{
        width: 18, height: 18, borderRadius:'50%',
        background: entityHsl('player', 0.2),
        border:'none', color: entityHsl('player'),
        fontSize: 12, fontWeight: 800, cursor:'pointer',
        display:'inline-flex', alignItems:'center', justifyContent:'center',
        marginLeft: 2,
      }}>×</button>
    )}
  </span>
);

const EmailChip = ({ email }) => (
  <span style={{
    display:'inline-flex', alignItems:'center', gap: 6,
    padding:'4px 4px 4px 8px', borderRadius:'var(--r-pill)',
    background:'var(--bg-muted)',
    color:'var(--text-sec)',
    border:`1px dashed ${entityHsl('player', 0.4)}`,
    fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 600,
    whiteSpace:'nowrap',
  }}>
    <span aria-hidden="true">@</span>
    <span>{email}</span>
    <span style={{
      fontFamily:'var(--f-mono)', fontSize: 8, fontWeight: 800,
      textTransform:'uppercase', letterSpacing:'.06em',
      background: entityHsl('player', 0.15),
      color: entityHsl('player'),
      padding:'1px 5px', borderRadius:'var(--r-sm)',
    }}>NEW</span>
    <button type="button" aria-label={`Rimuovi ${email}`} style={{
      width: 18, height: 18, borderRadius:'50%',
      background:'var(--border)', border:'none', color:'var(--text-sec)',
      fontSize: 12, fontWeight: 800, cursor:'pointer',
      display:'inline-flex', alignItems:'center', justifyContent:'center',
    }}>×</button>
  </span>
);

const NumberStepper = ({ label, value, sub }) => (
  <div style={{
    display:'flex', alignItems:'center', justifyContent:'space-between',
    padding:'12px 14px',
    background:'var(--bg-card)', border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)',
    flex: 1, minWidth: 0,
  }}>
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
        color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
      }}>{label}</div>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
        marginTop: 1,
      }}>{sub}</div>
    </div>
    <div style={{ display:'flex', alignItems:'center', gap: 6 }}>
      <button type="button" aria-label="-1" style={{
        width: 28, height: 28, borderRadius:'var(--r-sm)',
        background:'var(--bg-muted)', border:'1px solid var(--border)',
        color:'var(--text)', fontSize: 14, fontWeight: 800, cursor:'pointer',
      }}>−</button>
      <span style={{
        fontFamily:'var(--f-display)', fontSize: 18, fontWeight: 800,
        color:'var(--text)', minWidth: 24, textAlign:'center',
        fontVariantNumeric:'tabular-nums',
      }}>{value}</span>
      <button type="button" aria-label="+1" style={{
        width: 28, height: 28, borderRadius:'var(--r-sm)',
        background: entityHsl('player', 0.12),
        border:`1px solid ${entityHsl('player', 0.3)}`,
        color: entityHsl('player'), fontSize: 14, fontWeight: 800, cursor:'pointer',
      }}>+</button>
    </div>
  </div>
);

const ToggleRow = ({ enabled, label, sub, color = 'player' }) => (
  <label style={{
    display:'flex', alignItems:'center', gap: 12,
    padding:'12px 14px',
    background:'var(--bg-card)', border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)', cursor:'pointer',
  }}>
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{
        fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
        color:'var(--text)',
      }}>{label}</div>
      <div style={{
        fontSize: 11.5, color:'var(--text-sec)', marginTop: 2, lineHeight: 1.45,
      }}>{sub}</div>
    </div>
    <span style={{
      width: 40, height: 22, borderRadius: 999,
      background: enabled ? entityHsl(color) : 'var(--bg-muted)',
      position:'relative', transition:'background var(--dur-sm)',
      flexShrink: 0,
      border:'1px solid var(--border)',
    }}>
      <span style={{
        position:'absolute', top: 1, left: enabled ? 19 : 1,
        width: 18, height: 18, borderRadius:'50%',
        background:'#fff', boxShadow:'0 1px 3px rgba(0,0,0,.2)',
        transition:'left var(--dur-sm) var(--ease-spring)',
      }}/>
    </span>
    <input type="checkbox" defaultChecked={enabled} style={{ display:'none' }}/>
  </label>
);

const Step3Chi = ({ variant = 'empty', mobile }) => {
  // variants: empty | typing | filled
  const isEmpty = variant === 'empty';
  const isTyping = variant === 'typing';
  const isFilled = variant === 'filled';

  // Filled-state party members (Marco host + 5 invited mix)
  const partyFilled = [
    REGULARS.find(p => p.id === 'p-marco'),
    REGULARS.find(p => p.id === 'p-giulia'),
    REGULARS.find(p => p.id === 'p-davide'),
    REGULARS.find(p => p.id === 'p-luca'),
    REGULARS.find(p => p.id === 'p-sara'),
  ];
  const emailsFilled = ['federica.n@gmail.com'];

  // Typing-state autocomplete results — match "f"
  const typingMatches = REGULARS.filter(p =>
    p.name.toLowerCase().includes('f') && p.id !== 'p-marco'
  ); // Federica
  const typingValue = 'fede';

  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 16, padding: mobile ? '16px 14px 24px' : '20px 24px 24px' }}>
      <StepHeader
        title="Chi inviti?"
        sub="Cerca i tuoi giocatori abituali o invita per email chi non è ancora registrato."
      />

      {/* Search input + chips */}
      <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
        <Label>Invitati ({isEmpty ? '1' : isTyping ? '1' : '6'} / 8)</Label>
        <div style={{
          padding: 10,
          background:'var(--bg-card)',
          border: isTyping
            ? `1.5px solid ${entityHsl('player', 0.5)}`
            : '1px solid var(--border)',
          borderRadius:'var(--r-lg)',
          boxShadow: isTyping ? `0 0 0 3px ${entityHsl('player', 0.1)}` : 'none',
          display:'flex', flexWrap:'wrap', gap: 6, alignItems:'center',
          minHeight: 50,
          position:'relative',
        }}>
          {/* Marco always present as host */}
          <PlayerChip player={partyFilled[0]} isHost removable={false}/>

          {isFilled && (
            <>
              <PlayerChip player={partyFilled[1]}/>
              <PlayerChip player={partyFilled[2]}/>
              <PlayerChip player={partyFilled[3]}/>
              <PlayerChip player={partyFilled[4]}/>
              <EmailChip email={emailsFilled[0]}/>
            </>
          )}

          <input type="text"
            value={isTyping ? typingValue : ''}
            placeholder={isEmpty || isFilled ? 'Cerca nome o email…' : ''}
            readOnly
            style={{
              flex: 1, minWidth: 100,
              border:'none', outline:'none', background:'transparent',
              fontFamily:'var(--f-body)', fontSize: 13, color:'var(--text)',
              padding:'4px 6px',
            }}
          />

          {isTyping && (
            <div style={{
              position:'absolute', top:'calc(100% + 6px)', left: 0, right: 0,
              background:'var(--bg-card)', border:'1px solid var(--border-strong)',
              borderRadius:'var(--r-lg)',
              boxShadow:'var(--shadow-lg)',
              zIndex: 5,
              overflow:'hidden',
            }}>
              {typingMatches.map(p => (
                <button key={p.id} type="button" style={{
                  width:'100%', padding:'10px 12px',
                  background:'transparent', border:'none', cursor:'pointer',
                  display:'flex', alignItems:'center', gap: 10,
                  borderBottom:'1px solid var(--border-light)',
                  textAlign:'left',
                }}>
                  <Avatar player={p} size={28}/>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{
                      fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
                      color:'var(--text)',
                    }}>
                      {p.name.split('').map((ch, i) => {
                        const matchStart = p.name.toLowerCase().indexOf(typingValue.toLowerCase());
                        const inMatch = matchStart >= 0 && i >= matchStart && i < matchStart + typingValue.length;
                        return inMatch ? <mark key={i} style={{
                          background: entityHsl('player', 0.25),
                          color: entityHsl('player'),
                          padding:'0 1px', borderRadius: 2,
                        }}>{ch}</mark> : ch;
                      })}
                    </div>
                    <div style={{
                      fontFamily:'var(--f-mono)', fontSize: 10,
                      color:'var(--text-muted)', fontWeight: 600,
                    }}>{p.sub}</div>
                  </div>
                  <span style={{
                    fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
                    color: entityHsl('player'),
                  }}>+ Invita</span>
                </button>
              ))}
              <button type="button" style={{
                width:'100%', padding:'10px 12px',
                background: entityHsl('player', 0.06),
                border:'none', cursor:'pointer',
                display:'flex', alignItems:'center', gap: 10,
                textAlign:'left',
              }}>
                <span style={{
                  width: 28, height: 28, borderRadius:'50%',
                  background:'var(--bg-muted)',
                  display:'inline-flex', alignItems:'center', justifyContent:'center',
                  border:`2px dashed ${entityHsl('player', 0.4)}`,
                  color: entityHsl('player'),
                  fontSize: 14, fontWeight: 800,
                }}>@</span>
                <div style={{ flex: 1 }}>
                  <div style={{
                    fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
                    color:'var(--text)',
                  }}>Invita per email "<strong>{typingValue}</strong>"</div>
                  <div style={{
                    fontFamily:'var(--f-mono)', fontSize: 10,
                    color:'var(--text-muted)', fontWeight: 600, marginTop: 1,
                  }}>Riceverà link di iscrizione</div>
                </div>
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Suggestions row — only when empty */}
      {isEmpty && (
        <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
          <Label>Suggeriti · I tuoi regulars</Label>
          <div style={{ display:'flex', flexDirection:'column', gap: 6 }}>
            {REGULARS.filter(p => p.regular && !p.isHost).map(p => (
              <button key={p.id} type="button" style={{
                padding:'10px 12px', borderRadius:'var(--r-md)',
                background:'var(--bg-card)', border:'1px solid var(--border)',
                cursor:'pointer', display:'flex', alignItems:'center', gap: 10,
                textAlign:'left',
              }}>
                <Avatar player={p} size={32}/>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{
                    fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
                    color:'var(--text)',
                  }}>{p.name}</div>
                  <div style={{
                    fontFamily:'var(--f-mono)', fontSize: 10,
                    color:'var(--text-muted)', fontWeight: 600,
                  }}>{p.sub} · ama {p.fav}</div>
                </div>
                <span style={{
                  padding:'5px 10px', borderRadius:'var(--r-sm)',
                  background: entityHsl('player', 0.12),
                  color: entityHsl('player'),
                  fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
                  border:`1px solid ${entityHsl('player', 0.25)}`,
                }}>+ Invita</span>
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Min/max + auto-RSVP toggle */}
      <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
        <Label>Capienza</Label>
        <div style={{ display:'flex', gap: 8, flexDirection: mobile ? 'column' : 'row' }}>
          <NumberStepper label="Minimo" value={3} sub="serata cancellata sotto"/>
          <NumberStepper label="Massimo" value={6} sub="extra in waitlist"/>
        </div>
      </div>

      <ToggleRow
        enabled={true}
        label="Auto-RSVP regulars"
        sub="Giulia, Luca, Sara, Davide vengono confermati automaticamente. Possono comunque rifiutare."
        color="player"
      />
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// STEP 4 — COSA?
// /* v2: GameCandidatesPicker → apps/web/src/components/ui/v2/... */
// ═══════════════════════════════════════════════════════

const GameCandidateCard = ({ game, partyCount = 6, durationLimit = 180, selected, slot }) => {
  // Compatibility heuristic: compute match per duration & player count
  const playersOk = partyCount >= game.plMin && partyCount <= game.plMax;
  const durOk = game.durMin <= durationLimit;
  const isPerfect = playersOk && durOk;
  const isWarn = playersOk && !durOk;
  const isBad = !playersOk;
  const accent = isPerfect ? entityHsl('toolkit') : isWarn ? 'hsl(var(--c-warning))' : 'hsl(var(--c-danger))';

  return (
    <div style={{
      position:'relative',
      borderRadius:'var(--r-lg)',
      background:'var(--bg-card)',
      border: selected
        ? `1.5px solid ${entityHsl('game', 0.55)}`
        : '1px solid var(--border)',
      boxShadow: selected ? `0 0 0 3px ${entityHsl('game', 0.1)}` : 'none',
      overflow:'hidden',
      cursor:'pointer',
    }}>
      {selected && (
        <span style={{
          position:'absolute', top: 8, right: 8, zIndex: 2,
          width: 22, height: 22, borderRadius:'50%',
          background: entityHsl('game'),
          color:'#fff', fontSize: 11, fontWeight: 800,
          display:'flex', alignItems:'center', justifyContent:'center',
          fontFamily:'var(--f-display)',
          boxShadow:'0 2px 6px rgba(0,0,0,.25)',
        }}>{slot}</span>
      )}
      <div style={{
        height: 90,
        background: `linear-gradient(135deg, ${game.cover[0]}, ${game.cover[1]})`,
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 36, position:'relative',
      }} aria-hidden="true">
        <span style={{ filter:'drop-shadow(0 4px 12px rgba(0,0,0,.4))' }}>{game.emoji}</span>
      </div>
      <div style={{ padding:'10px 12px 12px', display:'flex', flexDirection:'column', gap: 6 }}>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
          color:'var(--text)', lineHeight: 1.25,
          whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
        }}>{game.title}</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          fontWeight: 600,
        }}>{game.publisher}</div>
        <div style={{ display:'flex', gap: 4, flexWrap:'wrap' }}>
          <span style={{
            padding:'2px 6px', borderRadius:'var(--r-sm)',
            background: entityHsl('game', 0.12),
            color: entityHsl('game'),
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            border:`1px solid ${entityHsl('game', 0.22)}`,
          }}>👥 {game.players}</span>
          <span style={{
            padding:'2px 6px', borderRadius:'var(--r-sm)',
            background: entityHsl('game', 0.12),
            color: entityHsl('game'),
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            border:`1px solid ${entityHsl('game', 0.22)}`,
          }}>⏱ {game.duration}</span>
        </div>
        <div style={{
          display:'flex', alignItems:'center', gap: 5,
          paddingTop: 4, borderTop:'1px solid var(--border-light)',
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color: accent,
        }}>
          <span aria-hidden="true">{isPerfect ? '✓' : isWarn ? '⚠' : '✕'}</span>
          <span>
            {isPerfect && `Perfetto per ${partyCount}`}
            {isWarn && `Lungo per ${durationLimit/60}h`}
            {isBad && `Solo ${game.players} giocatori`}
          </span>
        </div>
      </div>
    </div>
  );
};

const Step4Cosa = ({ decideGroup = false, mobile }) => {
  // Selected slots: Brass, Spirit Island, Wingspan (matching brief data)
  const selectedIds = ['g-brass', 'g-spirit', 'g-wingspan'];
  const slotMap = { 'g-brass': 1, 'g-spirit': 2, 'g-wingspan': 3 };

  const sortedLib = decideGroup
    ? LIBRARY
    : [...LIBRARY].sort((a, b) => {
        const aSel = selectedIds.includes(a.id) ? 0 : 1;
        const bSel = selectedIds.includes(b.id) ? 0 : 1;
        return aSel - bSel;
      });

  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 16, padding: mobile ? '16px 14px 24px' : '20px 24px 24px' }}>
      <StepHeader
        title="Cosa giocate?"
        sub={decideGroup
          ? 'Il gruppo voterà alla serata. Salta la selezione qui sotto.'
          : 'Scegli fino a 3 candidati dalla libreria. Il gruppo voterà al tavolo.'}
      />

      <ToggleRow
        enabled={decideGroup}
        label="Lascia decidere al gruppo"
        sub="Niente candidati ora — alla serata si fa una votazione veloce. Compatibile con qualsiasi gioco della libreria."
        color="event"
      />

      {!decideGroup && (
        <>
          <div style={{
            display:'flex', alignItems:'center', justifyContent:'space-between',
            padding:'10px 14px', background: entityHsl('game', 0.08),
            borderRadius:'var(--r-md)',
            border:`1px solid ${entityHsl('game', 0.22)}`,
          }}>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 700,
              color: entityHsl('game'),
            }}>
              <strong style={{
                fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
                marginRight: 4,
              }}>{selectedIds.length}/3</strong>
              candidati selezionati
            </div>
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
              color:'var(--text-muted)',
            }}>filtro: 6 giocatori · ≤3h</span>
          </div>

          <div style={{
            display:'grid',
            gridTemplateColumns: mobile ? 'repeat(2, 1fr)' : 'repeat(3, 1fr)',
            gap: 10,
          }}>
            {sortedLib.map(g => (
              <GameCandidateCard
                key={g.id}
                game={g}
                partyCount={6}
                durationLimit={180}
                selected={selectedIds.includes(g.id)}
                slot={slotMap[g.id]}
              />
            ))}
          </div>
        </>
      )}

      {decideGroup && (
        <div style={{
          padding:'24px 16px',
          background:'var(--bg-card)',
          border:`1px dashed ${entityHsl('event', 0.4)}`,
          borderRadius:'var(--r-lg)',
          display:'flex', flexDirection:'column', alignItems:'center',
          textAlign:'center', gap: 8,
        }}>
          <div style={{
            width: 56, height: 56, borderRadius:'50%',
            background: entityHsl('event', 0.14),
            color: entityHsl('event'),
            display:'flex', alignItems:'center', justifyContent:'center',
            fontSize: 26,
          }} aria-hidden="true">🗳️</div>
          <h3 style={{
            margin: 0, fontFamily:'var(--f-display)', fontSize: 15,
            fontWeight: 800, color:'var(--text)',
          }}>Votazione al tavolo</h3>
          <p style={{
            margin: 0, fontSize: 12, color:'var(--text-sec)', maxWidth: 320, lineHeight: 1.5,
          }}>
            Il gruppo sceglierà tra i giochi della tua libreria compatibili
            con <strong>6 giocatori</strong> e <strong>3h</strong>.
          </p>
          <span style={{
            padding:'4px 10px', borderRadius:'var(--r-pill)',
            background: entityHsl('game', 0.12),
            color: entityHsl('game'),
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
            border:`1px solid ${entityHsl('game', 0.22)}`,
          }}>{LIBRARY.filter(g => 6 >= g.plMin && 6 <= g.plMax && g.durMin <= 180).length} giochi compatibili</span>
        </div>
      )}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// PRIMARY ACTION BAR (sticky bottom)
// ═══════════════════════════════════════════════════════
const ActionBar = ({ step, mobile, primaryLabel = 'Avanti →' }) => (
  <div style={{
    display:'flex', gap: 10, alignItems:'center',
    padding: mobile ? '12px 14px' : '14px 24px',
    background:'var(--bg)',
    borderTop:'1px solid var(--border)',
    position:'sticky', bottom: 0, zIndex: 8,
  }}>
    {step > 1 && (
      <button type="button" style={{
        padding:'12px 16px', borderRadius:'var(--r-md)',
        background:'transparent', border:'1px solid var(--border-strong)',
        color:'var(--text)', fontFamily:'var(--f-display)',
        fontSize: 13, fontWeight: 800, cursor:'pointer',
      }}>← Indietro</button>
    )}
    <div style={{ flex: 1 }}/>
    <button type="button" style={{
      padding:'12px 22px', borderRadius:'var(--r-md)',
      background: `linear-gradient(135deg, ${entityHsl('event')}, hsl(${DS.EC.event.h - 20}, ${DS.EC.event.s}%, ${DS.EC.event.l - 8}%))`,
      color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
      cursor:'pointer',
      boxShadow:`0 4px 14px ${entityHsl('event', 0.35)}`,
    }}>{primaryLabel}</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// MOBILE SHELL
// ═══════════════════════════════════════════════════════
const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>21:42</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);

const PhoneTopNav = ({ title = 'Nuova serata' }) => (
  <div style={{
    display:'flex', alignItems:'center', gap: 9,
    padding:'10px 14px', background:'var(--glass-bg)',
    backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border-light)',
  }}>
    <button aria-label="Chiudi" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 16, cursor:'pointer',
    }}>×</button>
    <div style={{
      flex: 1, fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
      color:'var(--text)', textAlign:'center',
    }}>{title}</div>
    <button aria-label="Salva bozza" style={{
      width: 'auto', minWidth: 32, padding:'0 10px', height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text-sec)', fontSize: 11, fontWeight: 700,
      fontFamily:'var(--f-display)', cursor:'pointer',
    }}>Bozza</button>
  </div>
);

const MobileScreen = ({ step, variant, anchor, label, gherkin, primaryLabel = 'Avanti →', conformityMarker }) => (
  <section
    id={anchor}
    data-screen-label={`SP7-A · ${label}`}
    className="phone-shell"
    {...(conformityMarker ? { 'data-conformity-screen': conformityMarker } : {})}
  >
    <div className="state-caption">
      <span className="state-id">{anchor.replace('state-', '#')}</span>
      <span className="state-label">📱 {label}</span>
      {gherkin && <span className="gherkin">{gherkin}</span>}
    </div>
    <div className="phone">
      <PhoneSbar/>
      <div style={{ flex: 1, overflowY:'auto', display:'flex', flexDirection:'column', background:'var(--bg)' }}>
        <PhoneTopNav/>
        <StepIndicator current={step} onBack mobile/>
        <div style={{ flex: 1 }}>
          {step === 1 && <Step1Quando withWarning={variant === 'warning'} mobile/>}
          {step === 2 && <Step2Dove selected="host" mobile/>}
          {step === 3 && <Step3Chi variant={variant} mobile/>}
          {step === 4 && <Step4Cosa decideGroup={variant === 'decide-group'} mobile/>}
        </div>
        <ActionBar step={step} mobile primaryLabel={primaryLabel}/>
      </div>
    </div>
  </section>
);

// ═══════════════════════════════════════════════════════
// DESKTOP SPLIT-FORM (state-10)
// /* v2: GameNightCreateWizard desktop split (form 8-col + preview 4-col) */
// ═══════════════════════════════════════════════════════

const RSVPLivePreview = () => {
  const partyFilled = [
    REGULARS.find(p => p.id === 'p-marco'),
    REGULARS.find(p => p.id === 'p-giulia'),
    REGULARS.find(p => p.id === 'p-davide'),
    REGULARS.find(p => p.id === 'p-luca'),
    REGULARS.find(p => p.id === 'p-sara'),
  ];
  return (
    <aside style={{
      position:'sticky', top: 24,
      display:'flex', flexDirection:'column', gap: 14,
    }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 6,
        fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
        color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
      }}>
        <span aria-hidden="true" style={{
          width: 6, height: 6, borderRadius:'50%',
          background: entityHsl('toolkit'),
          boxShadow: `0 0 0 3px ${entityHsl('toolkit', 0.25)}`,
          animation:'sp7Pulse 1.6s var(--ease-out) infinite',
        }}/>
        Anteprima live · RSVP card
      </div>

      <article style={{
        background:'var(--bg-card)',
        border:'1px solid var(--border)',
        borderRadius:'var(--r-xl)',
        overflow:'hidden',
        boxShadow:'var(--shadow-md)',
      }}>
        {/* Cover */}
        <div style={{
          padding:'18px 18px 14px',
          background:`linear-gradient(135deg, ${entityHsl('event')}, hsl(${DS.EC.event.h - 30}, ${DS.EC.event.s}%, ${DS.EC.event.l - 8}%))`,
          color:'#fff', position:'relative',
        }}>
          <div style={{
            display:'flex', alignItems:'center', gap: 6, marginBottom: 10,
          }}>
            <span style={{
              padding:'3px 8px', borderRadius:'var(--r-pill)',
              background:'rgba(255,255,255,.22)', backdropFilter:'blur(8px)',
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform:'uppercase', letterSpacing:'.08em',
            }}>📅 Serata</span>
            <span style={{
              padding:'3px 8px', borderRadius:'var(--r-pill)',
              background:'rgba(255,255,255,.22)', backdropFilter:'blur(8px)',
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform:'uppercase', letterSpacing:'.08em',
            }}>Bozza</span>
          </div>
          <h3 style={{
            margin:'0 0 6px', fontFamily:'var(--f-display)',
            fontSize: 19, fontWeight: 800, lineHeight: 1.15,
            textShadow:'0 2px 8px rgba(0,0,0,.18)',
          }}>Sabato boardgame con i Padovani</h3>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 700,
            display:'flex', flexWrap:'wrap', gap:'2px 8px',
          }}>
            <span>sab 17 mag · 21:00</span>
            <span aria-hidden="true">·</span>
            <span>3h</span>
            <span aria-hidden="true">·</span>
            <span>Casa Marco</span>
          </div>
        </div>

        {/* Body */}
        <div style={{ padding:'14px 18px 18px', display:'flex', flexDirection:'column', gap: 14 }}>
          {/* Party */}
          <div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
              marginBottom: 6,
            }}>Party · 6/6</div>
            <div style={{ display:'flex', alignItems:'center' }}>
              {partyFilled.map((p, i) => (
                <span key={p.id} style={{ marginLeft: i === 0 ? 0 : -6 }}>
                  <Avatar player={p} size={28}/>
                </span>
              ))}
              <EmailAvatar email="federica.n@gmail.com" size={28}/>
            </div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
              marginTop: 6, fontWeight: 600,
            }}>
              <span style={{ color: entityHsl('toolkit'), fontWeight: 800 }}>4 confermati</span>
              {' · '}
              <span style={{ color: entityHsl('agent'), fontWeight: 800 }}>2 invitati</span>
            </div>
          </div>

          {/* Games */}
          <div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
              marginBottom: 6,
            }}>Candidati · 3</div>
            <div style={{ display:'flex', flexDirection:'column', gap: 5 }}>
              {['g-brass','g-spirit','g-wingspan'].map(id => {
                const g = LIBRARY.find(x => x.id === id);
                return (
                  <div key={id} style={{
                    display:'flex', alignItems:'center', gap: 8,
                    padding:'6px 8px', borderRadius:'var(--r-sm)',
                    background: entityHsl('game', 0.08),
                    border:`1px solid ${entityHsl('game', 0.18)}`,
                  }}>
                    <span style={{
                      width: 24, height: 24, borderRadius: 5,
                      background: `linear-gradient(135deg, ${g.cover[0]}, ${g.cover[1]})`,
                      display:'flex', alignItems:'center', justifyContent:'center',
                      fontSize: 13, flexShrink: 0,
                    }} aria-hidden="true">{g.emoji}</span>
                    <span style={{
                      fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
                      color: entityHsl('game'), flex: 1,
                      whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
                    }}>{g.title}</span>
                    <span style={{
                      fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
                      fontWeight: 700,
                    }}>{g.duration}</span>
                  </div>
                );
              })}
            </div>
          </div>

          {/* RSVP buttons (preview, disabled) */}
          <div style={{
            display:'grid', gridTemplateColumns:'1fr 1fr 1fr', gap: 6,
            padding: 8, background:'var(--bg-muted)', borderRadius:'var(--r-md)',
          }}>
            {[
              { label:'Vengo', color: entityHsl('toolkit') },
              { label:'Forse', color: 'hsl(var(--c-warning))' },
              { label:'No', color: 'var(--text-muted)' },
            ].map(b => (
              <button key={b.label} type="button" disabled style={{
                padding:'8px 4px', borderRadius:'var(--r-sm)',
                background:'var(--bg-card)', border:'1px solid var(--border)',
                color: b.color,
                fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
                cursor:'not-allowed', opacity: 0.7,
              }}>{b.label}</button>
            ))}
          </div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
            textAlign:'center', fontWeight: 600,
          }}>I bottoni saranno attivi dopo la creazione</div>
        </div>
      </article>

      {/* Hint */}
      <div style={{
        padding:'10px 12px', borderRadius:'var(--r-md)',
        background: entityHsl('event', 0.06),
        border:`1px dashed ${entityHsl('event', 0.3)}`,
        display:'flex', gap: 8,
      }}>
        <span aria-hidden="true" style={{ fontSize: 14, flexShrink: 0 }}>💡</span>
        <div style={{
          fontSize: 11.5, color:'var(--text-sec)', lineHeight: 1.5,
        }}>
          La card è quello che vedranno i tuoi invitati nelle notifiche. Aggiorna in tempo reale.
        </div>
      </div>
    </aside>
  );
};

const DesktopSplitForm = ({ anchor, label, conformityMarker }) => (
  <section
    id={anchor}
    data-screen-label={`SP7-A · ${label}`}
    className="desktop-shell"
    {...(conformityMarker ? { 'data-conformity-screen': conformityMarker } : {})}
  >
    <div className="state-caption">
      <span className="state-id">{anchor.replace('state-', '#')}</span>
      <span className="state-label">🖥️ {label}</span>
    </div>
    <div className="desktop-frame">
      <div className="desktop-bar">
        <span className="traffic"/><span className="traffic"/><span className="traffic"/>
        <span className="url">meepleai.app/game-nights/new</span>
      </div>
      <div style={{ background:'var(--bg)' }}>
        <DesktopNav/>
        <StepIndicator current={1} mobile={false}/>
        <div style={{
          display:'grid',
          gridTemplateColumns:'minmax(0, 8fr) minmax(0, 4fr)',
          gap: 24,
          padding:'8px 24px 0',
          maxWidth: 1280, margin:'0 auto',
        }}>
          {/* LEFT: form 8-col */}
          <div style={{
            display:'flex', flexDirection:'column', gap: 0,
            paddingBottom: 24,
          }}>
            <Step1Quando withWarning={false} mobile={false}/>
          </div>

          {/* RIGHT: live preview 4-col */}
          <div style={{ paddingTop: 20, paddingBottom: 24 }}>
            <RSVPLivePreview/>
          </div>
        </div>
        <ActionBar step={1} mobile={false}/>
      </div>
    </div>
  </section>
);

const DesktopNav = () => (
  <div style={{
    display:'flex', alignItems:'center', gap: 14,
    padding:'10px 24px',
    background:'var(--glass-bg)', backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border)',
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
      <div style={{
        width: 26, height: 26, borderRadius: 7,
        background:`linear-gradient(135deg, ${entityHsl('event')}, ${entityHsl('session')})`,
        color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
        fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)',
      }}>M</div>
      <span style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 14 }}>MeepleAI</span>
    </div>
    <div style={{
      flex: 1, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      marginLeft: 22,
    }}>
      <span>Serate</span>
      <span aria-hidden="true"> / </span>
      <strong style={{ color:'var(--text-sec)' }}>Nuova serata</strong>
    </div>
    <Avatar player={MARCO} size={28}/>
  </div>
);

// ═══════════════════════════════════════════════════════
// MOBILE STEP-FLOW OVERVIEW (state-09)
// ═══════════════════════════════════════════════════════
const MobileStepFlowOverview = ({ anchor, label }) => {
  const steps = [
    { step: 1, variant: null,            label:'1. Quando',  data: 'sab 17 mag · 21:00 · 3h' },
    { step: 2, variant: null,            label:'2. Dove',    data: 'Casa Marco · Padova' },
    { step: 3, variant: 'filled',        label:'3. Chi',     data: '6/6 · 4 confermati' },
    { step: 4, variant: null,            label:'4. Cosa',    data: '3 candidati · vota al tavolo' },
  ];
  return (
    <section id={anchor} data-screen-label={`SP7-A · ${label}`}>
      <div className="state-caption">
        <span className="state-id">{anchor.replace('state-', '#')}</span>
        <span className="state-label">📱 {label}</span>
      </div>
      <div className="step-flow-row">
        {steps.map(s => (
          <div key={s.step} className="phone phone-mini">
            <PhoneSbar/>
            <div style={{ flex: 1, overflowY:'auto', display:'flex', flexDirection:'column', background:'var(--bg)' }}>
              <PhoneTopNav title={`Step ${s.step}/4`}/>
              <StepIndicator current={s.step} onBack mobile/>
              <div style={{ flex: 1 }}>
                {s.step === 1 && <Step1Quando withWarning={false} mobile/>}
                {s.step === 2 && <Step2Dove selected="host" mobile/>}
                {s.step === 3 && <Step3Chi variant="filled" mobile/>}
                {s.step === 4 && <Step4Cosa decideGroup={false} mobile/>}
              </div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
};

// ═══════════════════════════════════════════════════════
// ROOT
// ═══════════════════════════════════════════════════════
const STATES = [
  { id:'state-01-step1-date',       step: 1, variant: null,              label:'01 · Step 1 — Quando? · default (sab 17 mag · 21:00 · 3h)', gherkin:'US-31.1' },
  { id:'state-02-step1-warning',    step: 1, variant: 'warning',         label:'02 · Step 1 — Conflitto rilevato (warning amber)',          gherkin:'US-31.1.warn' },
  { id:'state-03-step2-location',   step: 2, variant: null,              label:'03 · Step 2 — Dove? · Casa host (mappa + 4 opzioni)',       gherkin:'US-31.2' },
  { id:'state-04-step3-empty',      step: 3, variant: 'empty',           label:'04 · Step 3 — Chi? · empty + suggested regulars',           gherkin:'US-31.3' },
  { id:'state-05-step3-typing',     step: 3, variant: 'typing',          label:'05 · Step 3 — Autocomplete dropdown attivo',                gherkin:'US-31.3.search' },
  { id:'state-06-step3-filled',     step: 3, variant: 'filled',          label:'06 · Step 3 — 6 invitati (5 reg + 1 email NEW)',            gherkin:'US-31.3.invite' },
  { id:'state-07-step4-games',      step: 4, variant: null,              label:'07 · Step 4 — Cosa? · 3 candidati selected',                gherkin:'US-31.4' },
  { id:'state-08-step4-decide-group', step: 4, variant: 'decide-group',  label:'08 · Step 4 — Lascia decidere al gruppo',                   gherkin:'US-31.4.vote' },
];

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "showStateAnchors": true,
  "theme": "light"
}/*EDITMODE-END*/;

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
      {/* Page header */}
      <header style={{
        maxWidth: 1440, margin:'0 auto 32px',
        display:'flex', alignItems:'flex-start', gap: 16, flexWrap:'wrap',
      }}>
        <div style={{ display:'flex', alignItems:'center', gap: 12, flex: 1, minWidth: 0 }}>
          <div style={{
            width: 40, height: 40, borderRadius: 10,
            background:`linear-gradient(135deg, ${entityHsl('event')}, ${entityHsl('session')})`,
            color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
            fontWeight: 800, fontFamily:'var(--f-display)', fontSize: 18,
          }}>A</div>
          <div>
            <div style={{
              display:'flex', alignItems:'center', gap: 8, marginBottom: 2,
            }}>
              <span style={{
                padding:'2px 8px', borderRadius:'var(--r-pill)',
                background: entityHsl('event', 0.12),
                color: entityHsl('event'),
                fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform:'uppercase', letterSpacing:'.08em',
                border:`1px solid ${entityHsl('event', 0.22)}`,
              }}>SP7 · Mockup A</span>
              <span style={{
                fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
                fontWeight: 700,
              }}>US-31 · Sprint N+1 · P1</span>
            </div>
            <div style={{
              fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 22,
              color:'var(--text)', letterSpacing:'-.01em',
            }}>Game Night · Create wizard</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
              fontWeight: 600, marginTop: 2,
            }}>
              <code>/game-nights/new</code> · 4-step wizard mobile + split-form desktop
            </div>
          </div>
        </div>
        <button type="button" onClick={() => setTheme(t => t === 'light' ? 'dark' : 'light')}
          style={{
            padding:'8px 14px', borderRadius:'var(--r-md)',
            background:'var(--bg-card)', border:'1px solid var(--border)',
            color:'var(--text)', fontFamily:'var(--f-display)',
            fontSize: 12, fontWeight: 800, cursor:'pointer',
            display:'inline-flex', alignItems:'center', gap: 6,
          }}>
          <span aria-hidden="true">🌗</span>
          {theme === 'light' ? 'Light' : 'Dark'}
        </button>
      </header>

      {/* States grid */}
      <main style={{
        maxWidth: 1440, margin:'0 auto',
        display:'flex', flexDirection:'column', gap: 56,
      }}>
        {/* Section: Mobile states */}
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: entityHsl('event') }}/>
            <h2 className="section-title">Mobile · 375 — Wizard 4 step</h2>
            <span className="section-meta">8 stati · iPhone 15 viewport</span>
          </div>
          <div className="mobile-grid">
            {STATES.map((s, idx) => (
              <MobileScreen
                key={s.id}
                anchor={s.id}
                step={s.step}
                variant={s.variant}
                label={s.label}
                gherkin={s.gherkin}
                primaryLabel={s.step === 4 ? '✓ Crea serata' : 'Avanti →'}
                conformityMarker={idx === 0 ? 'default-mobile' : undefined}
              />
            ))}
          </div>
        </div>

        {/* Section: Mobile step-flow overview */}
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: entityHsl('event') }}/>
            <h2 className="section-title">Mobile · Step-flow overview</h2>
            <span className="section-meta">tutti i 4 step affiancati per QA visiva</span>
          </div>
          <MobileStepFlowOverview
            anchor="state-09-mobile-step-flow"
            label="09 · Step-flow overview · 1→4 affiancati (compact)"
          />
        </div>

        {/* Section: Desktop split-form */}
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: entityHsl('session') }}/>
            <h2 className="section-title">Desktop · 1280 — Split-form 12-col</h2>
            <span className="section-meta">form 8-col + RSVP card live preview 4-col</span>
          </div>
          <DesktopSplitForm
            anchor="state-10-desktop-split"
            label="10 · Desktop · Step 1 + RSVP live preview (8-col / 4-col)"
            conformityMarker="default-desktop"
          />
        </div>

        {/* Footnote */}
        <footer style={{
          padding:'24px 0', borderTop:'1px solid var(--border)',
          display:'flex', flexDirection:'column', gap: 10, maxWidth: 880, margin:'0 auto',
        }}>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
            color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
          }}>Pattern note</div>
          <ul style={{
            margin: 0, paddingLeft: 18,
            fontSize: 12.5, color:'var(--text-sec)', lineHeight: 1.7,
          }}>
            <li>StepIndicator riusato 1:1 da SP6-B (3→4 step). Stesso pattern back-btn + step-circle + step-line.</li>
            <li>Game picker autocomplete riusa pattern <code>Library section</code> da sp4-game-detail (search-bar focused border + EntityChip game).</li>
            <li>Entity color mapping: <code>event</code> per game-night, <code>game</code> per candidati, <code>player</code> per invitati, <code>--c-warning</code> per conflict.</li>
            <li>Solo helper <code>entityHsl()</code> e token semantic <code>var(--c-*)</code>. Zero <code>hsl(num,...)</code> hardcoded.</li>
            <li>Anchor id <code>state-NN-...</code> su tutti i wrapper come standard SP6-C/D/E.</li>
          </ul>
        </footer>
      </main>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
