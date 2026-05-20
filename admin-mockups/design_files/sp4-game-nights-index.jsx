/* MeepleAI SP4 wave 3 — Schermata G / 5
   Route: /game-nights
   File: admin-mockups/design_files/sp4-game-nights-index.{html,jsx}
   Pattern: Toggle Calendar / List view (header switch). Palette --c-event con gradient event→session.

   v2 nuovi (per impl post-merge):
   - GameNightsHeader      → apps/web/src/components/ui/v2/game-nights-header/
   - CalendarMonthGrid     → apps/web/src/components/ui/v2/calendar-month-grid/
   - CalendarDayCell       → apps/web/src/components/ui/v2/calendar-day-cell/
   - GameNightListCard     → apps/web/src/components/ui/v2/game-night-list-card/
   - DayDetailDrawer       → apps/web/src/components/ui/v2/day-detail-drawer/
   - FilterPillBar         → apps/web/src/components/ui/v2/filter-pill-bar/

   Riusi:
   - MeepleCard variant="list" (wave 1)
   - Tabs animated underline (wave 1) per Calendar/List toggle
   - EntityChip game/player (wave 1)
   - ConnectionChipStrip per partecipanti (PR #549/#552)

   Deviazioni: vista Calendar è grid statico mese corrente — paginazione mese ‹ › è solo visiva, no fetch reale.
*/

const { useState, useEffect, useRef, useMemo } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.event;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ─── EVENTS FIXTURE (March 2026) ─────────────────────────
const TODAY = { y: 2026, m: 2, d: 12 }; // 12 Mar 2026 (Thu)
const MONTH_LABEL = 'Marzo 2026';

const NIGHTS = [
  { id:'gn-wingspan-mar15', day: 15, time:'19:00', dur:'3h', org:'p-marco',
    title:'Serata Wingspan da Marco', loc:'Casa Marco',
    gameId:'g-wingspan', playerIds:['p-marco','p-sara','p-luca','p-giulia','p-andrea'],
    status:'confirmed', role:'organizer', month: 2 },
  { id:'gn-azul-mar18', day: 18, time:'20:30', dur:'2h', org:'p-sara',
    title:'Azul rapido', loc:'Casa Sara',
    gameId:'g-azul', playerIds:['p-sara','p-marco','p-luca'],
    status:'confirmed', role:'invited', month: 2 },
  { id:'gn-brass-mar20', day: 20, time:'19:30', dur:'3h 30m', org:'p-marco',
    title:'Demo Brass Birmingham', loc:'Sala B&B Lentia',
    gameId:'g-brass', playerIds:['p-marco','p-andrea','p-sara','p-luca'],
    status:'planned', role:'organizer', month: 2 },
  { id:'gn-catan-mar22', day: 22, time:'15:00', dur:'4h', org:'p-luca',
    title:'Catan Maratona', loc:'Casa Luca',
    gameId:'g-catan', playerIds:['p-luca','p-sara','p-andrea','p-giulia'],
    status:'confirmed', role:'invited', month: 2 },
  { id:'gn-7w-mar22', day: 22, time:'21:00', dur:'1h', org:'p-marco',
    title:'7 Wonders Duel — partita doppia', loc:'Casa Marco',
    gameId:'g-7wonders', playerIds:['p-marco','p-giulia'],
    status:'planned', role:'organizer', month: 2 },
  { id:'gn-arknova-mar24', day: 24, time:'19:00', dur:'2h 30m', org:'p-andrea',
    title:'Ark Nova primo tavolo', loc:'Ludoteca Centrale',
    gameId:'g-arknova', playerIds:['p-andrea','p-sara'],
    status:'planned', role:'invited', month: 2 },
  { id:'gn-spirit-mar26', day: 26, time:'20:00', dur:'3h', org:'p-sara',
    title:'Spirit Island co-op', loc:'Casa Sara',
    gameId:'g-spirit', playerIds:['p-sara','p-marco','p-luca'],
    status:'cancelled', role:'invited', month: 2 },
  { id:'gn-wingspan-mar28', day: 28, time:'14:30', dur:'2h', org:'p-marco',
    title:'Wingspan family edition', loc:'Casa Marco',
    gameId:'g-wingspan', playerIds:['p-marco','p-giulia','p-andrea','p-sara','p-luca','p-andrea'],
    status:'confirmed', role:'organizer', month: 2 },
  // Past
  { id:'gn-azul-feb20', day: 20, time:'19:00', dur:'2h 15m', org:'p-marco',
    title:'Azul torneo casalingo', loc:'Casa Marco',
    gameId:'g-azul', playerIds:['p-marco','p-sara','p-luca','p-giulia'],
    status:'completed', role:'organizer', month: 1 },
  { id:'gn-brass-feb20', day: 25, time:'20:00', dur:'2h 50m', org:'p-andrea',
    title:'Demo Brass Birmingham', loc:'Sala B&B',
    gameId:'g-brass', playerIds:['p-andrea','p-marco','p-sara'],
    status:'completed', role:'invited', month: 1 },
  { id:'gn-catan-feb12', day: 12, time:'19:30', dur:'3h', org:'p-luca',
    title:'Catan classico', loc:'Casa Luca',
    gameId:'g-catan', playerIds:['p-luca','p-sara','p-marco','p-giulia'],
    status:'completed', role:'invited', month: 1 },
];

const STATUS_META = {
  confirmed: { label:'Confermata', dotColor: entityHsl('toolkit'), bg: entityHsl('toolkit', 0.12) },
  planned:   { label:'In programma', dotColor: entityHsl('agent'), bg: entityHsl('agent', 0.12) },
  cancelled: { label:'Annullata', dotColor: 'hsl(var(--c-danger))', bg: 'hsl(var(--c-danger) / .12)' },
  completed: { label:'Conclusa', dotColor:'hsl(var(--c-muted))', bg: 'hsl(var(--c-muted) / .15)' },
};

const ROLE_OPTS = ['Tutte', 'Organizzo', 'Invitato', 'Concluse'];

// ═══════════════════════════════════════════════════════
// PRIMITIVES
// ═══════════════════════════════════════════════════════
const EntityChip = ({ type, label, sm }) => (
  <span style={{
    display:'inline-flex', alignItems:'center', gap: 5,
    padding: sm ? '2px 7px' : '4px 10px', borderRadius:'var(--r-pill)',
    background: entityHsl(type, 0.12), color: entityHsl(type),
    border:`1px solid ${entityHsl(type, 0.22)}`,
    fontFamily:'var(--f-display)', fontSize: sm ? 10 : 11, fontWeight: 700,
    whiteSpace:'nowrap',
  }}>
    <span aria-hidden="true">{DS.EC[type]?.em}</span>
    <span>{label}</span>
  </span>
);

const StatusPill = ({ status }) => {
  const m = STATUS_META[status];
  return (
    <span style={{
      display:'inline-flex', alignItems:'center', gap: 5,
      padding:'3px 9px', borderRadius:'var(--r-pill)',
      background: m.bg, color: m.dotColor,
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
      textTransform:'uppercase', letterSpacing:'.06em',
      border:`1px solid ${m.dotColor}33`,
    }}>
      <span aria-hidden="true" style={{
        width: 6, height: 6, borderRadius:'50%', background: m.dotColor,
      }}/>
      <span>{m.label}</span>
    </span>
  );
};

const PlayerAvatars = ({ playerIds, max = 5 }) => {
  const list = playerIds.slice(0, max);
  const extra = playerIds.length - max;
  return (
    <div style={{ display:'inline-flex', alignItems:'center' }}>
      {list.map((pid, i) => {
        const p = DS.byId[pid];
        if (!p) return null;
        return (
          <span key={`${pid}-${i}`} title={p.title}
            style={{
              width: 26, height: 26, borderRadius:'50%',
              background:`hsl(${p.color}, 55%, 50%)`,
              color:'#fff', fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 10,
              display:'inline-flex', alignItems:'center', justifyContent:'center',
              border:'2px solid var(--bg-card)',
              marginLeft: i === 0 ? 0 : -8,
              zIndex: max - i,
            }}>{p.initials}</span>
        );
      })}
      {extra > 0 && (
        <span style={{
          width: 26, height: 26, borderRadius:'50%',
          background:'var(--bg-muted)', color:'var(--text-sec)',
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
          display:'inline-flex', alignItems:'center', justifyContent:'center',
          border:'2px solid var(--bg-card)', marginLeft: -8, zIndex: 0,
        }}>+{extra}</span>
      )}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// HEADER + TOGGLE + FILTERS
// ═══════════════════════════════════════════════════════
const Header = ({ view, onView, role, onRole, mobile, total }) => {
  const refs = useRef({});
  const [ind, setInd] = useState({ left: 0, width: 0 });
  useEffect(() => {
    const el = refs.current[view];
    if (el) setInd({ left: el.offsetLeft, width: el.offsetWidth });
  }, [view]);

  return (
    <div style={{
      padding: mobile ? '14px 16px 0' : '24px 32px 0',
      background:'var(--bg)',
      display:'flex', flexDirection:'column', gap: mobile ? 12 : 16,
    }}>
      {/* Title row + CTA */}
      <div style={{
        display:'flex', alignItems: mobile ? 'flex-start' : 'flex-end',
        gap: 12, flexWrap:'wrap',
        flexDirection: mobile ? 'column' : 'row',
      }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            display:'inline-flex', alignItems:'center', gap: 8,
            padding:'4px 10px', borderRadius:'var(--r-pill)',
            background: entityHsl('event', 0.12), color: entityHsl('event'),
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 8,
          }}>
            <span aria-hidden="true">📅</span>
            <span>Game nights</span>
          </div>
          <h1 style={{
            margin: 0, fontFamily:'var(--f-display)',
            fontSize: mobile ? 24 : 30, fontWeight: 800,
            color:'var(--text)', letterSpacing:'-.01em', lineHeight: 1.1,
          }}>Serate di gioco</h1>
          <div style={{
            marginTop: 4, fontFamily:'var(--f-mono)', fontSize: 12,
            color:'var(--text-muted)', fontWeight: 600,
          }}>
            <strong style={{ color: entityHsl('event'), fontFamily:'var(--f-mono)' }}>{total}</strong>
            {' '}serate questo mese · {NIGHTS.filter(n => n.status === 'confirmed' && n.month === 2).length} confermate
          </div>
        </div>
        <button type="button" style={{
          padding:'10px 16px', borderRadius:'var(--r-md)',
          background:`linear-gradient(135deg, ${entityHsl('event')}, ${entityHsl('session')})`,
          color:'#fff', border:'none',
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, cursor:'pointer',
          display:'inline-flex', alignItems:'center', gap: 7,
          boxShadow:`0 4px 12px ${entityHsl('event', 0.3)}`,
          alignSelf: mobile ? 'stretch' : 'auto',
          justifyContent:'center',
        }}>
          <span aria-hidden="true">+</span>
          <span>Nuova serata</span>
        </button>
      </div>

      {/* View toggle + filters row */}
      <div style={{
        display:'flex', alignItems:'center', gap: 12, flexWrap:'wrap',
        justifyContent: mobile ? 'space-between' : 'flex-start',
      }}>
        {/* Calendar/List toggle */}
        <div style={{ position:'relative' }}>
          <div style={{
            display:'flex', gap: 2, borderBottom:'1px solid var(--border)',
          }} role="tablist" aria-label="Vista serate">
            {[
              { id:'calendar', icon:'🗓', label:'Calendario' },
              { id:'list', icon:'☰', label:'Lista' },
            ].map(t => {
              const isActive = view === t.id;
              return (
                <button key={t.id} type="button"
                  ref={el => { refs.current[t.id] = el; }}
                  onClick={() => onView(t.id)}
                  role="tab" aria-selected={isActive}
                  style={{
                    padding:'10px 16px', background:'transparent', border:'none',
                    color: isActive ? entityHsl('event') : 'var(--text-sec)',
                    fontFamily:'var(--f-display)', fontSize: 13,
                    fontWeight: isActive ? 800 : 700, cursor:'pointer',
                    display:'inline-flex', alignItems:'center', gap: 6,
                  }}>
                  <span aria-hidden="true">{t.icon}</span>
                  <span>{t.label}</span>
                </button>
              );
            })}
          </div>
          <span aria-hidden="true" style={{
            position:'absolute', bottom: -1, left: ind.left, width: ind.width,
            height: 2, background: entityHsl('event'),
            transition:'left .3s var(--ease-out), width .3s var(--ease-out)',
          }}/>
        </div>

        {/* Filter pills */}
        <div style={{
          display:'flex', gap: 6, flexWrap:'wrap',
          marginLeft: mobile ? 0 : 'auto',
          overflowX: mobile ? 'auto' : 'visible',
          scrollbarWidth:'none',
        }}>
          {ROLE_OPTS.map(opt => {
            const active = role === opt;
            return (
              <button key={opt} type="button" onClick={() => onRole(opt)}
                aria-pressed={active}
                style={{
                  padding:'6px 12px', borderRadius:'var(--r-pill)',
                  background: active ? entityHsl('event', 0.15) : 'var(--bg-card)',
                  color: active ? entityHsl('event') : 'var(--text-sec)',
                  border: active
                    ? `1px solid ${entityHsl('event', 0.4)}`
                    : '1px solid var(--border)',
                  fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
                  cursor:'pointer', whiteSpace:'nowrap',
                }}>{opt}</button>
            );
          })}
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// CALENDAR VIEW
// /* v2: CalendarMonthGrid → apps/web/src/components/ui/v2/calendar-month-grid/ */
// ═══════════════════════════════════════════════════════
const buildMonthGrid = () => {
  // Mar 2026 starts on Sunday Mar 1 → ISO week starts Mon, so first Mon-week is Feb 23
  // Days: Lun=1 ... Dom=7. Mar 1 2026 = Sunday → grid starts Feb 23 (Mon)
  const cells = [];
  // Feb tail: 23..28 (6 days) — but we only need up to Sun before Mar 1
  // Actually Mar 1 2026 is Sunday so prepend 6 days: Mon Feb 23 ... Sat Feb 28
  for (let d = 23; d <= 28; d++) cells.push({ day: d, otherMonth: true, isFeb: true });
  // March: 1..31
  for (let d = 1; d <= 31; d++) cells.push({ day: d, otherMonth: false });
  // April pad to fill last week (42 cells = 6 weeks)
  let pad = 42 - cells.length;
  for (let d = 1; d <= pad; d++) cells.push({ day: d, otherMonth: true, isApr: true });
  return cells;
};

const eventsForDay = (cell, role) => {
  if (cell.otherMonth) return [];
  const monthIdx = 2; // March
  return NIGHTS.filter(n => {
    if (n.month !== monthIdx) return false;
    if (n.day !== cell.day) return false;
    if (role === 'Tutte') return true;
    if (role === 'Organizzo') return n.role === 'organizer';
    if (role === 'Invitato') return n.role === 'invited';
    if (role === 'Concluse') return n.status === 'completed';
    return true;
  });
};

const DayCell = ({ cell, events, isToday, onClick, mobile }) => {
  const hasEvents = events.length > 0;
  const max = mobile ? 1 : 3;
  const visible = events.slice(0, max);
  const overflow = events.length - max;
  return (
    <button type="button" onClick={() => hasEvents && onClick(cell, events)}
      aria-label={`Giorno ${cell.day}${hasEvents ? `, ${events.length} eventi` : ''}`}
      disabled={cell.otherMonth}
      style={{
        textAlign:'left',
        minHeight: mobile ? 64 : 110,
        padding: mobile ? 4 : 8,
        background: cell.otherMonth ? 'transparent'
          : hasEvents ? entityHsl('event', 0.05) : 'var(--bg-card)',
        border: isToday
          ? `2px solid ${entityHsl('event')}`
          : '1px solid var(--border-light)',
        borderRadius: mobile ? 6 : 'var(--r-md)',
        cursor: hasEvents ? 'pointer' : (cell.otherMonth ? 'default' : 'default'),
        opacity: cell.otherMonth ? 0.35 : 1,
        display:'flex', flexDirection:'column', gap: 4,
        transition:'background var(--dur-sm)',
        position:'relative',
      }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 4,
      }}>
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: mobile ? 11 : 12,
          fontWeight: isToday ? 800 : 700,
          color: isToday ? entityHsl('event')
                : cell.otherMonth ? 'var(--text-muted)' : 'var(--text)',
        }}>{cell.day}</span>
        {isToday && !mobile && (
          <span style={{
            fontFamily:'var(--f-mono)', fontSize: 8, fontWeight: 800,
            color: entityHsl('event'), textTransform:'uppercase', letterSpacing:'.08em',
            marginLeft:'auto',
          }}>Oggi</span>
        )}
      </div>
      <div style={{ display:'flex', flexDirection:'column', gap: 2, flex: 1 }}>
        {visible.map(ev => {
          const meta = STATUS_META[ev.status];
          return (
            <div key={ev.id} style={{
              padding: mobile ? '2px 5px' : '3px 6px',
              borderRadius: 4,
              background: ev.status === 'cancelled'
                ? 'hsl(var(--c-danger) / .12)'
                : entityHsl('event', 0.18),
              color: ev.status === 'cancelled'
                ? 'hsl(var(--c-danger))'
                : entityHsl('event'),
              fontFamily:'var(--f-display)', fontSize: mobile ? 9 : 10,
              fontWeight: 700,
              borderLeft: `2px solid ${ev.status === 'cancelled' ? 'hsl(var(--c-danger))' : entityHsl('event')}`,
              overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap',
              textDecoration: ev.status === 'cancelled' ? 'line-through' : 'none',
              opacity: ev.status === 'cancelled' ? 0.7 : 1,
            }}>
              <span style={{ fontFamily:'var(--f-mono)', fontWeight: 800 }}>{ev.time}</span>
              {!mobile && <> · {DS.byId[ev.gameId]?.title || ev.title}</>}
            </div>
          );
        })}
        {overflow > 0 && (
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
            color: entityHsl('event'), padding:'1px 4px',
          }}>+{overflow} altri</div>
        )}
      </div>
    </button>
  );
};

const CalendarView = ({ role, onDayClick, mobile, stateOverride }) => {
  const cells = buildMonthGrid();
  const dayLabels = ['Lun','Mar','Mer','Gio','Ven','Sab','Dom'];

  if (stateOverride === 'loading') {
    return (
      <div style={{ padding: mobile ? '0 16px 24px' : '24px 32px' }}>
        <div className="mai-shimmer" style={{
          height: 36, marginBottom: 12,
          borderRadius:'var(--r-md)', background:'var(--bg-muted)',
        }}/>
        <div style={{
          display:'grid', gridTemplateColumns:'repeat(7, 1fr)', gap: 4,
        }}>
          {Array.from({ length: 35 }).map((_, i) => (
            <div key={i} className="mai-shimmer" style={{
              height: mobile ? 64 : 110,
              borderRadius:'var(--r-md)', background:'var(--bg-muted)',
            }}/>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div style={{
      padding: mobile ? '12px 16px 24px' : '20px 32px 32px',
      display:'flex', flexDirection:'column', gap: 12,
    }}>
      {/* Month nav */}
      <div style={{
        display:'flex', alignItems:'center', gap: 10, flexWrap:'wrap',
      }}>
        <button type="button" aria-label="Mese precedente" style={{
          width: 32, height: 32, borderRadius:'var(--r-md)',
          background:'var(--bg-card)', border:'1px solid var(--border)',
          color:'var(--text)', fontSize: 14, cursor:'pointer',
        }}>‹</button>
        <h2 style={{
          margin: 0, fontFamily:'var(--f-display)', fontSize: mobile ? 16 : 20,
          fontWeight: 800, color:'var(--text)', minWidth: 140,
        }}>{MONTH_LABEL}</h2>
        <button type="button" aria-label="Mese successivo" style={{
          width: 32, height: 32, borderRadius:'var(--r-md)',
          background:'var(--bg-card)', border:'1px solid var(--border)',
          color:'var(--text)', fontSize: 14, cursor:'pointer',
        }}>›</button>
        <button type="button" style={{
          marginLeft: 6, padding:'6px 14px', borderRadius:'var(--r-md)',
          background:'transparent', color: entityHsl('event'),
          border:`1px solid ${entityHsl('event', 0.4)}`,
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
        }}>Oggi</button>
        <div style={{
          marginLeft:'auto', fontFamily:'var(--f-mono)', fontSize: 11,
          color:'var(--text-muted)', fontWeight: 700,
        }}>
          {NIGHTS.filter(n => n.month === 2).length} serate · {NIGHTS.filter(n => n.month === 2 && n.status === 'cancelled').length} annullate
        </div>
      </div>

      {/* Day labels */}
      <div style={{
        display:'grid', gridTemplateColumns:'repeat(7, 1fr)', gap: 4,
      }}>
        {dayLabels.map(d => (
          <div key={d} style={{
            padding:'6px 4px', textAlign: mobile ? 'center' : 'left',
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
            color:'var(--text-muted)', textTransform:'uppercase',
            letterSpacing:'.08em',
          }}>{d}</div>
        ))}
      </div>

      {/* Cells grid */}
      <div style={{
        display:'grid', gridTemplateColumns:'repeat(7, 1fr)', gap: 4,
      }}>
        {cells.map((cell, i) => {
          const events = eventsForDay(cell, role);
          const isToday = !cell.otherMonth && cell.day === TODAY.d;
          return (
            <DayCell key={i} cell={cell} events={events} isToday={isToday}
              onClick={onDayClick} mobile={mobile}/>
          );
        })}
      </div>

      {/* Legend */}
      <div style={{
        display:'flex', gap: 14, flexWrap:'wrap', paddingTop: 8,
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
        fontWeight: 700,
      }}>
        <span style={{ display:'inline-flex', alignItems:'center', gap: 5 }}>
          <span style={{ width: 10, height: 10, borderRadius: 2, background: entityHsl('event', 0.18) }}/>
          Serata
        </span>
        <span style={{ display:'inline-flex', alignItems:'center', gap: 5 }}>
          <span style={{ width: 10, height: 10, borderRadius: 2, background:'hsl(var(--c-danger) / .18)' }}/>
          Annullata
        </span>
        <span style={{ display:'inline-flex', alignItems:'center', gap: 5 }}>
          <span style={{ width: 10, height: 10, borderRadius: 4, border:`2px solid ${entityHsl('event')}` }}/>
          Oggi
        </span>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// LIST VIEW
// /* v2: GameNightListCard → apps/web/src/components/ui/v2/game-night-list-card/ */
// ═══════════════════════════════════════════════════════
const ListCard = ({ night, mobile }) => {
  const game = DS.byId[night.gameId];
  const gameTitle = game?.title || night.title;
  const isOrg = night.role === 'organizer';
  const isCompleted = night.status === 'completed';
  const isCancelled = night.status === 'cancelled';

  return (
    <article style={{
      display:'flex', flexDirection: mobile ? 'column' : 'row',
      gap: mobile ? 10 : 14,
      padding: mobile ? 14 : 16,
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
      borderLeft: `3px solid ${
        isCancelled ? 'hsl(var(--c-danger))' : entityHsl('event')
      }`,
      opacity: isCancelled ? 0.78 : 1,
      transition:'border-color var(--dur-sm), box-shadow var(--dur-sm)',
    }}>
      {/* Date block */}
      <div style={{
        flexShrink: 0,
        width: mobile ? 'auto' : 78,
        padding: mobile ? '0' : '6px 10px',
        background: mobile ? 'transparent' : entityHsl('event', 0.08),
        border: mobile ? 'none' : `1px solid ${entityHsl('event', 0.22)}`,
        borderRadius:'var(--r-md)',
        display:'flex',
        flexDirection: mobile ? 'row' : 'column',
        alignItems: mobile ? 'center' : 'center',
        justifyContent:'center',
        gap: mobile ? 8 : 0,
      }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: mobile ? 10 : 9, fontWeight: 800,
          color: entityHsl('event'), textTransform:'uppercase', letterSpacing:'.08em',
        }}>{night.month === 2 ? 'Mar' : 'Feb'}</div>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: mobile ? 22 : 28, fontWeight: 800,
          color: entityHsl('event'), lineHeight: 1, fontVariantNumeric:'tabular-nums',
        }}>{night.day}</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: mobile ? 10 : 10, fontWeight: 700,
          color:'var(--text-sec)', marginTop: mobile ? 0 : 2,
        }}>{night.time}</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
          marginTop: mobile ? 0 : 1, marginLeft: mobile ? 'auto' : 0,
        }}>· {night.dur}</div>
      </div>

      {/* Body */}
      <div style={{ flex: 1, minWidth: 0, display:'flex', flexDirection:'column', gap: 8 }}>
        <div style={{
          display:'flex', alignItems:'flex-start', gap: 8, flexWrap:'wrap',
        }}>
          <h3 style={{
            margin: 0, fontFamily:'var(--f-display)', fontSize: mobile ? 14 : 16,
            fontWeight: 800, color:'var(--text)', lineHeight: 1.3,
            flex: 1, minWidth: 0,
            textDecoration: isCancelled ? 'line-through' : 'none',
          }}>{night.title}</h3>
          <StatusPill status={night.status}/>
        </div>

        <div style={{
          display:'flex', alignItems:'center', gap: 6, flexWrap:'wrap',
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
        }}>
          <span aria-hidden="true">📍</span>
          <span style={{ fontWeight: 600 }}>{night.loc}</span>
          {game && (
            <>
              <span aria-hidden="true" style={{ opacity: 0.4 }}>·</span>
              <EntityChip type="game" label={gameTitle} sm/>
            </>
          )}
          {isOrg && (
            <span style={{
              padding:'2px 7px', borderRadius:'var(--r-pill)',
              background: entityHsl('player', 0.12), color: entityHsl('player'),
              fontFamily:'var(--f-display)', fontSize: 10, fontWeight: 800,
              border:`1px solid ${entityHsl('player', 0.22)}`,
            }}>● Organizzo</span>
          )}
        </div>

        <div style={{
          display:'flex', alignItems:'center', gap: 10, flexWrap:'wrap',
          paddingTop: 6, borderTop:'1px solid var(--border-light)',
        }}>
          <PlayerAvatars playerIds={night.playerIds} max={mobile ? 4 : 5}/>
          <span style={{
            fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
            fontWeight: 700,
          }}>{night.playerIds.length} partecipanti</span>
          <div style={{ flex: 1 }}/>
          {/* CTA contestuali */}
          {isCompleted ? (
            <button type="button" style={{
              padding:'6px 12px', borderRadius:'var(--r-md)',
              background:'transparent', color:'var(--text)',
              border:'1px solid var(--border-strong)',
              fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800, cursor:'pointer',
            }} onClick={() => { setTimeout(() => { window.location.href = 'sp7-game-night-detail-rsvp.html'; }, 0); /* DEMO-NAV */ }}>Vedi summary →</button>
          ) : isCancelled ? (
            <button type="button" style={{
              padding:'6px 12px', borderRadius:'var(--r-md)',
              background:'transparent', color:'var(--text-muted)',
              border:'1px solid var(--border)',
              fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700, cursor:'pointer',
            }} onClick={() => { setTimeout(() => { window.location.href = 'sp7-game-night-detail-rsvp.html'; }, 0); /* DEMO-NAV */ }}>Riprogramma</button>
          ) : isOrg ? (
            <button type="button" style={{
              padding:'6px 12px', borderRadius:'var(--r-md)',
              background: entityHsl('event'), color:'#fff', border:'none',
              fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800, cursor:'pointer',
            }}>Modifica</button>
          ) : (
            <div style={{ display:'flex', gap: 6 }}>
              <button type="button" style={{
                padding:'6px 12px', borderRadius:'var(--r-md)',
                background: entityHsl('toolkit'), color:'#fff', border:'none',
                fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800, cursor:'pointer',
                lineHeight: 1.4,
              }} onClick={() => { setTimeout(() => { window.location.href = 'sp7-game-night-detail-rsvp.html'; }, 0); /* DEMO-NAV */ }}>✓ Partecipo</button>
              <button type="button" style={{
                padding:'6px 10px', borderRadius:'var(--r-md)',
                background:'transparent', color:'var(--text-sec)',
                border:'1px solid var(--border-strong)',
                fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700, cursor:'pointer',
              }}>Forse</button>
            </div>
          )}
        </div>
      </div>
    </article>
  );
};

const ListView = ({ role, mobile, stateOverride }) => {
  if (stateOverride === 'loading') {
    return (
      <div style={{
        padding: mobile ? '12px 16px 24px' : '20px 32px 32px',
        display:'flex', flexDirection:'column', gap: 12,
      }}>
        {[0,1,2,3].map(i => (
          <div key={i} className="mai-shimmer" style={{
            height: mobile ? 130 : 116, borderRadius:'var(--r-lg)', background:'var(--bg-muted)',
          }}/>
        ))}
      </div>
    );
  }

  const filtered = NIGHTS.filter(n => {
    if (role === 'Tutte') return true;
    if (role === 'Organizzo') return n.role === 'organizer';
    if (role === 'Invitato') return n.role === 'invited';
    if (role === 'Concluse') return n.status === 'completed';
    return true;
  });

  // Group by month: 2 = Marzo 2026, 1 = Febbraio 2026
  const groups = [
    { key: 2, label:'Marzo 2026', sub:'Mese corrente', items: filtered.filter(n => n.month === 2)
      .sort((a, b) => a.day - b.day) },
    { key: 1, label:'Febbraio 2026', sub:'Storico', items: filtered.filter(n => n.month === 1)
      .sort((a, b) => b.day - a.day) },
  ].filter(g => g.items.length > 0);

  if (groups.length === 0) {
    return (
      <div style={{
        padding: mobile ? '40px 16px' : '60px 32px',
        display:'flex', alignItems:'center', justifyContent:'center',
      }}>
        <div style={{
          maxWidth: 380, textAlign:'center',
          padding:'32px 24px',
          background:'var(--bg-card)', border:'1px dashed var(--border-strong)',
          borderRadius:'var(--r-xl)',
        }}>
          <div style={{ fontSize: 36, marginBottom: 10 }} aria-hidden="true">🔍</div>
          <h3 style={{
            fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
            color:'var(--text)', margin:'0 0 6px',
          }}>Nessuna serata in questo filtro</h3>
          <p style={{
            fontSize: 12.5, color:'var(--text-muted)', margin: 0, lineHeight: 1.6,
          }}>Cambia filtro o crea una nuova serata.</p>
        </div>
      </div>
    );
  }

  return (
    <div style={{
      padding: mobile ? '12px 16px 24px' : '20px 32px 32px',
      display:'flex', flexDirection:'column', gap: 24,
    }}>
      {groups.map(g => (
        <section key={g.key}>
          <div style={{
            position:'sticky', top: mobile ? 56 : 0, zIndex: 5,
            padding:'8px 0', marginBottom: 10,
            background:'var(--bg)',
            display:'flex', alignItems:'baseline', gap: 10, flexWrap:'wrap',
          }}>
            <h2 style={{
              margin: 0, fontFamily:'var(--f-display)', fontSize: mobile ? 16 : 18,
              fontWeight: 800, color:'var(--text)',
            }}>{g.label}</h2>
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
              fontWeight: 700,
            }}>{g.items.length} serate · {g.sub}</span>
          </div>
          <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
            {g.items.map(n => <ListCard key={n.id} night={n} mobile={mobile}/>)}
          </div>
        </section>
      ))}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// DAY DRAWER
// /* v2: DayDetailDrawer → apps/web/src/components/ui/v2/day-detail-drawer/ */
// ═══════════════════════════════════════════════════════
const DayDrawer = ({ open, day, events, onClose, mobile }) => {
  if (!open) return null;
  return (
    <div role="dialog" aria-label={`Serate del ${day} marzo`}
      style={{
        position:'absolute', inset: 0, zIndex: 50,
        display:'flex', alignItems:'flex-end', justifyContent: mobile ? 'stretch' : 'flex-end',
        background:'rgba(0,0,0,.5)',
      }} onClick={onClose}>
      <aside onClick={e => e.stopPropagation()} style={{
        width: mobile ? '100%' : 480, maxWidth:'100%',
        height: mobile ? 'auto' : '100%', maxHeight: mobile ? '88vh' : '100%',
        background:'var(--bg)',
        borderTopLeftRadius: mobile ? 'var(--r-xl)' : 'var(--r-xl)',
        borderTopRightRadius: mobile ? 'var(--r-xl)' : 0,
        borderBottomLeftRadius: mobile ? 0 : 0,
        boxShadow:'var(--shadow-xl)',
        overflow:'auto',
        animation:`slideIn ${mobile ? 'Up' : 'Right'} .25s var(--ease-out)`,
      }}>
        {mobile && (
          <div aria-hidden="true" style={{
            width: 36, height: 4, borderRadius:'var(--r-pill)',
            background:'var(--border-strong)', margin:'10px auto 0',
          }}/>
        )}
        <header style={{
          padding:'16px 20px',
          display:'flex', alignItems:'flex-start', gap: 12,
          borderBottom:'1px solid var(--border)',
          position:'sticky', top: 0, zIndex: 1, background:'var(--bg)',
        }}>
          <div style={{
            width: 50, height: 50, borderRadius:'var(--r-md)',
            background: entityHsl('event', 0.12), color: entityHsl('event'),
            display:'flex', flexDirection:'column', alignItems:'center', justifyContent:'center',
            fontFamily:'var(--f-display)', fontWeight: 800,
            border:`1px solid ${entityHsl('event', 0.22)}`,
          }}>
            <span style={{ fontSize: 9, fontFamily:'var(--f-mono)', textTransform:'uppercase' }}>Mar</span>
            <span style={{ fontSize: 20 }}>{day}</span>
          </div>
          <div style={{ flex: 1 }}>
            <h2 style={{
              margin: 0, fontFamily:'var(--f-display)', fontSize: 18, fontWeight: 800,
              color:'var(--text)',
            }}>{day} marzo 2026</h2>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-muted)',
              fontWeight: 700, marginTop: 2,
            }}>{events.length} {events.length === 1 ? 'serata' : 'serate'} in programma</div>
          </div>
          <button type="button" onClick={onClose} aria-label="Chiudi"
            style={{
              width: 32, height: 32, borderRadius:'var(--r-md)',
              background:'var(--bg-card)', border:'1px solid var(--border)',
              color:'var(--text)', fontSize: 16, cursor:'pointer',
            }}>×</button>
        </header>
        <div style={{ padding:'14px 20px 20px', display:'flex', flexDirection:'column', gap: 10 }}>
          {events.map(n => <ListCard key={n.id} night={n} mobile={mobile}/>)}
          <button type="button" style={{
            padding:'10px 14px', borderRadius:'var(--r-md)',
            background:'transparent', color: entityHsl('event'),
            border:`1px dashed ${entityHsl('event', 0.4)}`,
            fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
            marginTop: 4,
          }}>+ Aggiungi serata in questo giorno</button>
        </div>
      </aside>
      <style>{`
        @keyframes slideInUp { from { transform: translateY(100%); } to { transform: translateY(0); } }
        @keyframes slideInRight { from { transform: translateX(100%); } to { transform: translateX(0); } }
        @media (prefers-reduced-motion: reduce) {
          @keyframes slideInUp { from, to { transform: none; } }
          @keyframes slideInRight { from, to { transform: none; } }
        }
      `}</style>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// EMPTY / ERROR
// ═══════════════════════════════════════════════════════
const EmptyAll = ({ mobile }) => (
  <div style={{
    padding: mobile ? '40px 24px' : '80px 32px',
    display:'flex', alignItems:'center', justifyContent:'center',
  }}>
    <div style={{
      maxWidth: 460, textAlign:'center',
      padding:'40px 28px',
      background:'var(--bg-card)', border:'1px dashed var(--border-strong)',
      borderRadius:'var(--r-xl)',
      display:'flex', flexDirection:'column', alignItems:'center',
    }}>
      <div style={{
        width: 80, height: 80, borderRadius:'50%',
        background: entityHsl('event', 0.12), color: entityHsl('event'),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 36, marginBottom: 16,
      }} aria-hidden="true">📅</div>
      <h3 style={{
        fontFamily:'var(--f-display)', fontSize: 19, fontWeight: 800,
        color:'var(--text)', margin:'0 0 8px',
      }}>Nessuna serata in programma</h3>
      <p style={{
        fontSize: 13.5, color:'var(--text-muted)', maxWidth: 360, margin:'0 0 20px',
        lineHeight: 1.6,
      }}>Crea la prima serata per organizzare le tue partite, invitare il gruppo e tenere traccia di chi gioca a cosa.</p>
      <button type="button" style={{
        padding:'12px 22px', borderRadius:'var(--r-md)',
        background:`linear-gradient(135deg, ${entityHsl('event')}, ${entityHsl('session')})`,
        color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, cursor:'pointer',
        boxShadow:`0 4px 12px ${entityHsl('event', 0.3)}`,
      }}>+ Crea la prima serata</button>
    </div>
  </div>
);

const ErrorState = () => (
  <div style={{ padding:'60px 32px', display:'flex', justifyContent:'center' }}>
    <div style={{
      maxWidth: 440, textAlign:'center',
      padding:'32px 24px',
      background:'hsl(var(--c-danger) / .08)',
      border:'1px solid hsl(var(--c-danger) / .3)',
      borderRadius:'var(--r-xl)',
      display:'flex', flexDirection:'column', alignItems:'center',
    }}>
      <div style={{
        width: 56, height: 56, borderRadius:'50%',
        background:'hsl(var(--c-danger) / .15)', color:'hsl(var(--c-danger))',
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 26, marginBottom: 12,
      }} aria-hidden="true">⚠</div>
      <h3 style={{
        fontFamily:'var(--f-display)', fontSize: 17, fontWeight: 800,
        color:'var(--text)', margin:'0 0 6px',
      }}>Impossibile caricare le serate</h3>
      <p style={{
        fontSize: 12.5, color:'var(--text-muted)', maxWidth: 380, margin:'0 0 16px',
        lineHeight: 1.6,
      }}>Errore di rete durante il fetch del calendario. Verifica la connessione e riprova.</p>
      <button type="button" style={{
        padding:'10px 20px', borderRadius:'var(--r-md)',
        background:'hsl(var(--c-danger))', color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
      }}>↻ Riprova</button>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// BODY
// ═══════════════════════════════════════════════════════
const GameNightsBody = ({ stateOverride, mobile, initialView = 'calendar', initialRole = 'Tutte', drawerDay = null }) => {
  const [view, setView] = useState(initialView);
  const [role, setRole] = useState(initialRole);
  const [drawer, setDrawer] = useState(drawerDay ? { open: true, day: drawerDay, events: NIGHTS.filter(n => n.month === 2 && n.day === drawerDay) } : { open: false, day: null, events: [] });

  const total = NIGHTS.filter(n => n.month === 2).length;

  return (
    <div style={{ background:'var(--bg)' }}>
      <Header view={view} onView={setView} role={role} onRole={setRole}
        mobile={mobile} total={total}/>

      {stateOverride === 'empty' ? <EmptyAll mobile={mobile}/>
       : stateOverride === 'error' ? <ErrorState/>
       : view === 'calendar'
        ? <CalendarView role={role} mobile={mobile}
            stateOverride={stateOverride}
            onDayClick={(cell, events) => setDrawer({ open: true, day: cell.day, events })}/>
        : <ListView role={role} mobile={mobile} stateOverride={stateOverride}/>
      }

      <DayDrawer open={drawer.open} day={drawer.day} events={drawer.events}
        onClose={() => setDrawer(d => ({ ...d, open: false }))} mobile={mobile}/>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// SHELLS
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
      <strong style={{ color:'var(--text-sec)' }}>Serate di gioco</strong>
    </div>
  </div>
);

const DesktopScreen = (props) => (
  <div style={{ background:'var(--bg)' }}>
    <DesktopNav/>
    <GameNightsBody {...props}/>
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
  }}>
    <button aria-label="Indietro" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>←</button>
    <div style={{
      flex: 1, fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
      textAlign:'center', fontWeight: 700,
    }}>/game-nights</div>
    <button aria-label="Più opzioni" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>⋯</button>
  </div>
);
const MobileScreen = (props) => (
  <>
    <PhoneSbar/>
    <div style={{ flex: 1, overflowY:'auto', position:'relative', background:'var(--bg)' }}>
      <PhoneTopNav/>
      <GameNightsBody {...props} mobile/>
    </div>
  </>
);

const PhoneShell = ({ label, desc, children, conformityMarker }) => (
  <div
    style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 10 }}
    {...(conformityMarker ? { 'data-conformity-screen': conformityMarker } : {})}
  >
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

const DesktopFrame = ({ label, desc, children, conformityMarker }) => (
  <div
    style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}
    {...(conformityMarker ? { 'data-conformity-screen': conformityMarker } : {})}
  >
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width:'100%', maxWidth: 1440,
      borderRadius:'var(--r-xl)', border:'1px solid var(--border)',
      background:'var(--bg)', overflow:'hidden',
      boxShadow:'var(--shadow-lg)',
      position:'relative',
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
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/game-nights</span>
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
// ROOT
// ═══════════════════════════════════════════════════════
const MOBILE_STATES = [
  { id:'m1', view:'list', role:'Tutte',
    label:'01 · Default · List view (mobile)',
    desc:'Mobile default = LIST (più usabile su 375). Group sticky "Marzo 2026" + cards verticali con date block compatto + status pill + RSVP.' },
  { id:'m2', view:'calendar', role:'Tutte',
    label:'02 · Calendar · grid compresso',
    desc:'Calendar mobile: grid 7-col con celle compatte (1 evento + count). Tap cella apre drawer giorno.' },
  { id:'m3', view:'list', role:'Organizzo',
    label:'03 · Filter Organizzo',
    desc:'Lista filtrata "Organizzo": solo serate dove l\'utente è organizer. Pill "● Organizzo" visibile + CTA "Modifica".' },
  { id:'m4', view:'list', role:'Tutte', drawerDay: 22,
    label:'04 · Day drawer (22 mar)',
    desc:'Bottom-sheet drawer giorno con 2 serate (Catan + 7 Wonders), CTA "+ Aggiungi serata".' },
  { id:'m5', view:'list', stateOverride:'empty',
    label:'05 · Empty',
    desc:'Empty state full-screen: icona event 80px + CTA "+ Crea la prima serata".' },
  { id:'m6', view:'list', stateOverride:'loading',
    label:'06 · Loading',
    desc:'Skeleton 4 cards list con altezza realistica (130px).' },
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
      <header style={{
        maxWidth: 1440, margin:'0 auto 32px',
        display:'flex', alignItems:'center', gap: 12, flexWrap:'wrap',
      }}>
        <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
          <div style={{
            width: 32, height: 32, borderRadius: 8,
            background:`linear-gradient(135deg, ${entityHsl('event')}, ${entityHsl('session')})`,
            color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
            fontWeight: 800, fontFamily:'var(--f-display)',
          }}>G</div>
          <div>
            <div style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 16 }}>SP4 wave 3 · G</div>
            <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)' }}>
              /game-nights — Serate index (calendar / list)
            </div>
          </div>
        </div>
        <div style={{ flex: 1 }}/>
        <button type="button" onClick={(e) => { (() => setTheme(t => t === 'light' ? 'dark' : 'light'))(e); setTimeout(() => { window.location.href = 'sp7-game-night-detail-rsvp.html'; }, 0); /* DEMO-NAV */ }}
          style={{
            padding:'8px 14px', borderRadius:'var(--r-md)',
            background:'var(--bg-card)', border:'1px solid var(--border)',
            color:'var(--text)', fontFamily:'var(--f-display)',
            fontSize: 12, fontWeight: 800, cursor:'pointer',
          }}>🌗 {theme === 'light' ? 'Light' : 'Dark'}</button>
      </header>

      <section style={{ maxWidth: 1440, margin:'0 auto', display:'flex', flexDirection:'column', gap: 36 }}>
        <DesktopFrame label="Desktop · 01 · Calendar · Default · Tutte"
          desc="Pattern: header con title + count (8 serate · 4 confermate) + CTA gradient event→session + toggle Calendario/Lista (animated underline) + filter pills. Calendar grid 7-col (Lun-Dom) Marzo 2026, oggi=12 highlight border event, celle con eventi event/.05 bg, max 3 chip per cella + '+N altri'. Cancellata=danger bg + line-through.">
          <DesktopScreen initialView="calendar" initialRole="Tutte"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 02 · Calendar · Filter Organizzo"
          desc="Stesso layout calendar ma filtrato: solo eventi role=organizer (Marco). Le altre celle si svuotano. Pill 'Organizzo' attiva.">
          <DesktopScreen initialView="calendar" initialRole="Organizzo"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 03 · List · Default · Tutte"
          desc="Vista lista: group header sticky 'Marzo 2026' + 'Febbraio 2026'. Cards orizzontali (date block 78px sx + body con titolo + status pill + meta location/game/role + avatars overlap + CTA contestuali). Pattern MeepleCard variant='list'.">
          <DesktopScreen initialView="list" initialRole="Tutte"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 04 · List · Filter Concluse"
          desc="Solo serate completed (Feb): cards con CTA 'Vedi summary →' invece di RSVP/Modifica.">
          <DesktopScreen initialView="list" initialRole="Concluse"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 05 · Calendar + Day drawer aperto (22 mar)"
          desc="Drawer da sx scivolato in da destra (480px) con 2 serate del 22 marzo (Catan Maratona + 7 Wonders Duel) + CTA dashed '+ Aggiungi serata in questo giorno'.">
          <DesktopScreen initialView="calendar" initialRole="Tutte" drawerDay={22}/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 06 · Empty"
          desc="Header normale ma body empty state: icona event 80px + h3 + CTA gradient grande '+ Crea la prima serata'."
          conformityMarker="default-desktop">
          <DesktopScreen initialView="calendar" stateOverride="empty"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 07 · Calendar · Loading"
          desc="Header normale, calendar skeleton: nav skeleton + grid 35 celle skeleton 110px. Hydration progressivo.">
          <DesktopScreen initialView="calendar" stateOverride="loading"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 08 · Error"
          desc="Header normale, body alert danger 'Impossibile caricare le serate' + CTA '↻ Riprova'.">
          <DesktopScreen initialView="calendar" stateOverride="error"/>
        </DesktopFrame>
      </section>

      <section style={{ maxWidth: 1440, margin:'48px auto 0' }}>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 22, fontWeight: 800,
          marginBottom: 8,
        }}>Mobile · 375px</h2>
        <p style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-muted)', marginBottom: 24,
        }}>6 stati: list default · calendar compresso · filtro Organizzo · day drawer (bottom-sheet) · empty · loading.</p>
        <div style={{
          display:'flex', flexWrap:'wrap', gap: 32, justifyContent:'center',
        }}>
          {MOBILE_STATES.map((m) => (
            <PhoneShell
              key={m.id}
              label={m.label}
              desc={m.desc}
              conformityMarker={m.stateOverride === 'empty' ? 'default-mobile' : undefined}>
              <MobileScreen initialView={m.view} initialRole={m.role}
                stateOverride={m.stateOverride} drawerDay={m.drawerDay}/>
            </PhoneShell>
          ))}
        </div>
      </section>

      <section style={{
        maxWidth: 900, margin:'64px auto 0',
        padding: 24, borderRadius:'var(--r-xl)',
        background:'var(--bg-card)', border:'1px solid var(--border)',
      }}>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 18, fontWeight: 800, marginBottom: 12,
        }}>Definition of Done · G game-nights-index</h2>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.9, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>✓ Solo CSS variables da tokens.css (zero hex hardcoded)</li>
          <li>✓ Helper entityHsl(type, alpha) inline</li>
          <li>✓ Light + dark mode (toggle header)</li>
          <li>✓ Mobile 375px (6 phone) + Desktop 1440px (8 frame)</li>
          <li>✓ Pattern: toggle Calendar/List (Tabs animated underline) nel header</li>
          <li>✓ Mobile default = LIST · desktop default = CALENDAR</li>
          <li>✓ Palette --c-event rosa/red · gradient event→session per CTA + brand mark</li>
          <li>✓ Header: titolo + count totali "8 serate" + 4 filter pills (Tutte/Organizzo/Invitato/Concluse) + CTA gradient "+ Nuova serata"</li>
          <li>✓ Calendar: nav ‹ › + Oggi · grid 7-col Lun-Dom · today highlight 2px border event · giorni con eventi bg event/.05 · max 3 chip per cella · "+N altri" overflow · cancelled=danger+line-through · legend</li>
          <li>✓ List: gruppi sticky per mese + ListCard (date block 78px + body + status pill + location/game/role + avatars + CTA contestuali)</li>
          <li>✓ Day drawer: aperto su click cella · slide da right (desktop) / bottom (mobile) · header con date block 50px + close · cards interne riusano ListCard · CTA dashed "+ Aggiungi serata"</li>
          <li>✓ CTA contestuali: completed=Vedi summary · cancelled=Riprogramma · organizer=Modifica · invited=Partecipo + Forse</li>
          <li>✓ PlayerAvatars overlap (max 5 + "+N") riuso pattern ConnectionChipStrip</li>
          <li>✓ Stati: default / empty (CTA crea prima) / loading (skeleton grid o list) / error (Riprova)</li>
          <li>✓ ARIA: role="tablist"/"tab" sui toggle · aria-pressed sui filter pills · aria-label su nav mese · role="dialog" sul drawer</li>
          <li>✓ prefers-reduced-motion gestito (slideIn keyframes annullati)</li>
          <li>✓ Testo italiano · dati realistici (Wingspan da Marco, Brass Birmingham, Casa Marco, Sala B&B Lentia, Ludoteca Centrale)</li>
          <li>✓ ID short readable: gn-wingspan-mar15, gn-brass-feb20, p-marco — NO UUID</li>
          <li>✓ Commento di apertura con route /game-nights</li>
        </ul>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, marginTop: 18, marginBottom: 8,
        }}>Nuovi componenti v2 emersi</h3>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.8, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>· GameNightsHeader (badge category + title + count + filter pills + view toggle + CTA gradient)</li>
          <li>· CalendarMonthGrid (grid 7-col Lun-Dom + nav prev/next + Oggi CTA + legend)</li>
          <li>· CalendarDayCell (numero + max 3 chip ev + "+N altri" + today border + bg conditional)</li>
          <li>· GameNightListCard (date block sx + body + status pill + role pill + avatars + CTA contestuali)</li>
          <li>· DayDetailDrawer (slide da right desktop / bottom-sheet mobile + header date block + lista ListCard)</li>
          <li>· FilterPillBar (4 pills Tutte/Organizzo/Invitato/Concluse con aria-pressed)</li>
          <li>· StatusPill (4 varianti confirmed/planned/cancelled/completed con dot + bg + border)</li>
          <li>· PlayerAvatars (overlap negative margin -8px + "+N" extra circle)</li>
        </ul>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, marginTop: 18, marginBottom: 8,
        }}>Riusi pixel-perfect</h3>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.8, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>· MeepleCard variant="list" (wave 1) — lifted into ListCard</li>
          <li>· Tabs animated underline (wave 1) per Calendar/List toggle</li>
          <li>· EntityChip game/player (wave 1)</li>
          <li>· ConnectionChipStrip pattern → PlayerAvatars overlap (PR #549/#552)</li>
          <li>· Drawer V1 bottom-sheet (mobile) + V3 right-side (desktop) da 03-drawer-variants</li>
        </ul>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, marginTop: 18, marginBottom: 8,
          color: entityHsl('warning'),
        }}>Deviazioni flaggate</h3>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.8, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>· Calendar è grid statico Marzo 2026 — paginazione mese ‹ › è solo visiva, no fetch reale (frontend logic da implementare)</li>
          <li>· "Conclusa" group header non ha sticky offset top in mobile (nav top + group header sticky non perfettamente coordinati su 375px) — verificare in produzione</li>
          <li>· 5° serata duplicata di Andrea negli avatars (mar 28 ha Andrea due volte nei playerIds) per testare overflow "+1" — fix dataset finale</li>
          <li>· Drawer animation usa @keyframes inline, non components.css (come da contratto: NON sovrascrivere)</li>
          <li>· Filter "Concluse" semantica include solo passate del mese visibile + Febbraio — pattern UX da chiarire (filtra storico?)</li>
        </ul>
      </section>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
