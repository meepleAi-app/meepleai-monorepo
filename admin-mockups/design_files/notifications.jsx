// ── notifications.jsx — MeepleAI Design System v1
// Components: TimestampRelative, EntityIcon, NotificationItem,
//             NotificationDetail, FiltersBar, EmptyState,
//             SettingsQuickLink, NotificationsFeed
// Screens 1–5 exported to window for notifications.html

const { useState, useRef, useCallback } = React;

// ── Demo data ────────────────────────────────────────────────────
const NOTIFICATIONS = [
  { id: 1, entity: 'session', icon: '🎲', title: 'Marco ha iniziato una sessione',
    desc: 'Marco Rossi ha avviato una nuova sessione di Catan. 4 giocatori sono già connessi.',
    time: '2h fa', timeAbs: 'Oggi alle 10:32', group: 'oggi', read: false,
    cta: 'Apri sessione' },
  { id: 2, entity: 'agent', icon: '🤖', title: 'Agente Regolamento ha risposto',
    desc: 'Hai chiesto delle regole del porto speciale in Catan. L\'agente ha trovato una risposta dettagliata.',
    time: '3h fa', timeAbs: 'Oggi alle 09:14', group: 'oggi', read: false,
    cta: 'Vedi risposta' },
  { id: 3, entity: 'event', icon: '🎉', title: 'Invito serata gioco ricevuto',
    desc: 'Sei invitato alla serata gioco di sabato 21 aprile alle 21:00. Host: Giulia B. Luogo: Casa sua.',
    time: '5h fa', timeAbs: 'Oggi alle 07:08', group: 'oggi', read: true,
    cta: 'Rispondi all\'invito' },
  { id: 4, entity: 'kb', icon: '📖', title: 'Nuova house rule aggiunta',
    desc: 'Lorenzo ha aggiunto una house rule a Risiko: "Prima mossa — dadi una sola volta per territorio".',
    time: 'Ieri 14:30', timeAbs: 'Ieri alle 14:30', group: 'ieri', read: true,
    cta: 'Vedi regola' },
  { id: 5, entity: 'player', icon: '👤', title: 'Lucia si è unita alla partita',
    desc: 'Lucia Ferrari ha accettato il tuo invito per Carcassonne (Sabato sera). Ora siete in 4.',
    time: 'Ieri 11:05', timeAbs: 'Ieri alle 11:05', group: 'ieri', read: true,
    cta: 'Vedi partita' },
  { id: 6, entity: 'game', icon: '🏆', title: 'Settimana completata!',
    desc: 'Hai giocato 3 partite questa settimana. Nuova striscia attiva: 2 settimane consecutive.',
    time: 'Lun 20:00', timeAbs: 'Lunedì alle 20:00', group: 'settimana', read: true,
    cta: 'Vedi statistiche' },
  { id: 7, entity: 'chat', icon: '💬', title: 'Limite messaggi AI al 90%',
    desc: 'Hai raggiunto il 90% del limite giornaliero di messaggi AI. Il contatore si azzera a mezzanotte.',
    time: 'Mar 09:00', timeAbs: 'Martedì alle 09:00', group: 'settimana', read: true,
    cta: null },
  { id: 8, entity: 'toolkit', icon: '🧰', title: 'Toolkit "Strategie Catan" aggiornato',
    desc: 'Il toolkit è stato aggiornato con 3 nuovi strumenti: Calcolatore probabilità, Mappa commercio, Guida aperture.',
    time: '2 apr', timeAbs: '2 aprile alle 16:22', group: 'precedenti', read: true,
    cta: 'Apri toolkit' },
];

const GROUPS = [
  { key: 'oggi', label: 'Oggi' },
  { key: 'ieri', label: 'Ieri' },
  { key: 'settimana', label: 'Questa settimana' },
  { key: 'precedenti', label: 'Precedenti' },
];

const FILTERS = [
  { key: 'all',      label: 'Tutte',    entity: 'game',    entities: null },
  { key: 'sessions', label: 'Sessioni', entity: 'session', entities: ['session'] },
  { key: 'agents',   label: 'Agenti',   entity: 'agent',   entities: ['agent'] },
  { key: 'events',   label: 'Serate',   entity: 'event',   entities: ['event'] },
  { key: 'system',   label: 'Sistema',  entity: 'toolkit', entities: ['game','chat','toolkit','kb'] },
];

// ── Helpers ──────────────────────────────────────────────────────
const ec = e => `hsl(var(--c-${e}))`;
const ect = (e, a = 0.12) => `hsl(var(--c-${e}) / ${a})`;

// ── PhoneSBar ────────────────────────────────────────────────────
function PhoneSBar({ time = '09:41' }) {
  return (
    <div style={{
      height: 32, flexShrink: 0, display: 'flex', alignItems: 'center',
      justifyContent: 'space-between', padding: '6px 22px 0',
      fontSize: 12, fontWeight: 700, fontFamily: 'var(--f-display)',
      background: 'var(--bg)', color: 'var(--text-sec)',
    }}>
      <span>{time}</span>
      <div style={{ display: 'flex', gap: 4, fontSize: 11 }}>
        <span>●●●</span><span>WiFi</span><span>100%</span>
      </div>
    </div>
  );
}

// ── PhoneTopBar ──────────────────────────────────────────────────
function PhoneTopBar({ title, unreadCount = 0, onBack }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '10px 14px 8px',
      borderBottom: '1px solid var(--border)',
      background: 'var(--bg)', flexShrink: 0,
    }}>
      {onBack && (
        <button onClick={onBack} style={{
          width: 32, height: 32, borderRadius: '50%',
          background: 'var(--bg-muted)', border: 'none', cursor: 'pointer',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          color: 'var(--text-sec)', fontSize: 16,
        }}>←</button>
      )}
      <span style={{
        fontFamily: 'var(--f-display)', fontWeight: 800,
        fontSize: 17, flex: 1, letterSpacing: '-.01em',
      }}>{title}</span>
      {unreadCount > 0 && (
        <div style={{
          minWidth: 20, height: 20, borderRadius: 10,
          background: ec('event'), color: '#fff',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 10, fontWeight: 800, fontFamily: 'var(--f-mono)',
          padding: '0 5px', border: '2px solid var(--bg)',
        }}>{unreadCount > 9 ? '9+' : unreadCount}</div>
      )}
      <button style={{
        width: 32, height: 32, borderRadius: '50%',
        background: 'var(--bg-muted)', border: 'none', cursor: 'pointer',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 14, color: 'var(--text-sec)',
      }}>⋯</button>
    </div>
  );
}

// ── TimestampRelative ────────────────────────────────────────────
function TimestampRelative({ time }) {
  return (
    <span style={{
      fontFamily: 'var(--f-mono)', fontSize: 10,
      color: 'var(--text-muted)', whiteSpace: 'nowrap',
    }}>{time}</span>
  );
}

// ── EntityIcon ───────────────────────────────────────────────────
function EntityIcon({ entity, icon, size = 40 }) {
  return (
    <div style={{
      width: size, height: size, borderRadius: 'var(--r-lg)',
      background: ect(entity), color: ec(entity),
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontSize: Math.round(size * 0.45), flexShrink: 0,
    }}>{icon}</div>
  );
}

// ── UnreadDot ────────────────────────────────────────────────────
function UnreadDot() {
  return (
    <div style={{
      width: 7, height: 7, borderRadius: '50%',
      background: ec('event'), flexShrink: 0,
    }} />
  );
}

// ── NotificationItem ─────────────────────────────────────────────
function NotificationItem({ notif, selected, onClick, onArchive, onMarkRead }) {
  const [hover, setHover] = useState(false);
  const [swipeX, setSwipeX] = useState(0);
  const startX = useRef(null);

  const onTouchStart = e => { startX.current = e.touches[0].clientX; };
  const onTouchMove = e => {
    if (startX.current === null) return;
    const dx = e.touches[0].clientX - startX.current;
    setSwipeX(Math.max(-80, Math.min(80, dx)));
  };
  const onTouchEnd = () => {
    if (swipeX > 60) onArchive?.(notif.id);
    else if (swipeX < -60) onMarkRead?.(notif.id);
    setSwipeX(0); startX.current = null;
  };

  const bgColor = selected
    ? ect(notif.entity, 0.07)
    : hover ? 'var(--bg-hover)' : 'transparent';

  return (
    <div style={{ position: 'relative', overflow: 'hidden' }}>
      {swipeX > 10 && (
        <div style={{
          position: 'absolute', left: 0, top: 0, bottom: 0, width: 70,
          background: ect('toolkit', 0.2),
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 18,
        }}>📦</div>
      )}
      {swipeX < -10 && (
        <div style={{
          position: 'absolute', right: 0, top: 0, bottom: 0, width: 70,
          background: ect('kb', 0.2),
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 18,
        }}>✓</div>
      )}
      <div
        role="listitem"
        onClick={onClick}
        onMouseEnter={() => setHover(true)}
        onMouseLeave={() => setHover(false)}
        onTouchStart={onTouchStart}
        onTouchMove={onTouchMove}
        onTouchEnd={onTouchEnd}
        style={{
          display: 'flex', alignItems: 'flex-start', gap: 12,
          padding: '12px 14px',
          borderLeft: `3px solid ${ec(notif.entity)}`,
          background: bgColor,
          cursor: 'pointer',
          transition: 'background var(--dur-xs) var(--ease-out)',
          transform: `translateX(${swipeX}px)`,
          minHeight: 64,
        }}
      >
        <EntityIcon entity={notif.entity} icon={notif.icon} />
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            display: 'flex', alignItems: 'center',
            justifyContent: 'space-between', gap: 8, marginBottom: 2,
          }}>
            <span style={{
              fontSize: 13, fontWeight: notif.read ? 500 : 700,
              color: notif.read ? 'var(--text-sec)' : 'var(--text)',
              fontFamily: 'var(--f-display)',
              overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
            }}>{notif.title}</span>
            <div style={{ display: 'flex', alignItems: 'center', gap: 5, flexShrink: 0 }}>
              <TimestampRelative time={notif.time} />
              {!notif.read && <UnreadDot />}
            </div>
          </div>
          <div style={{
            fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.45,
            display: '-webkit-box', WebkitLineClamp: 2,
            WebkitBoxOrient: 'vertical', overflow: 'hidden',
          }}>{notif.desc}</div>
          {hover && (
            <div style={{ display: 'flex', gap: 5, marginTop: 8 }}>
              {notif.cta && (
                <button style={{
                  padding: '3px 9px', fontSize: 10, fontWeight: 700,
                  borderRadius: 'var(--r-sm)',
                  background: ect(notif.entity), color: ec(notif.entity),
                  border: 'none', cursor: 'pointer', fontFamily: 'var(--f-display)',
                }}>{notif.cta}</button>
              )}
              <button onClick={e => { e.stopPropagation(); onArchive?.(notif.id); }} style={{
                padding: '3px 9px', fontSize: 10, fontWeight: 700,
                borderRadius: 'var(--r-sm)',
                background: 'var(--bg-muted)', color: 'var(--text-muted)',
                border: 'none', cursor: 'pointer', fontFamily: 'var(--f-display)',
              }}>Archivia</button>
              {!notif.read && (
                <button onClick={e => { e.stopPropagation(); onMarkRead?.(notif.id); }} style={{
                  padding: '3px 9px', fontSize: 10, fontWeight: 700,
                  borderRadius: 'var(--r-sm)',
                  background: 'var(--bg-muted)', color: 'var(--text-muted)',
                  border: 'none', cursor: 'pointer', fontFamily: 'var(--f-display)',
                }}>Segna letto</button>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ── GroupDivider ─────────────────────────────────────────────────
function GroupDivider({ label, count }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: 8,
      padding: '10px 14px 6px',
      fontFamily: 'var(--f-mono)', fontSize: 10,
      color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em',
    }}>
      <span>{label}</span>
      <span style={{
        background: 'var(--bg-muted)', borderRadius: 10,
        padding: '1px 6px', fontSize: 9, fontWeight: 700,
      }}>{count}</span>
      <div style={{ flex: 1, height: 1, background: 'var(--border-light)' }} />
    </div>
  );
}

// ── NotificationDetail ───────────────────────────────────────────
function NotificationDetail({ notif, onClose }) {
  if (!notif) return null;
  return (
    <div style={{
      position: 'absolute', bottom: 0, left: 0, right: 0,
      background: 'var(--bg-card)',
      borderRadius: 'var(--r-2xl) var(--r-2xl) 0 0',
      boxShadow: 'var(--shadow-drawer)', zIndex: 10,
      animation: 'slideUp var(--dur-md) var(--ease-out)',
    }}>
      {/* Handle */}
      <div style={{ display: 'flex', justifyContent: 'center', padding: '10px 0 4px' }}>
        <div style={{ width: 36, height: 4, borderRadius: 2, background: 'var(--border-strong)' }} />
      </div>
      {/* Header */}
      <div style={{ padding: '8px 16px 14px', display: 'flex', gap: 12, alignItems: 'flex-start' }}>
        <EntityIcon entity={notif.entity} icon={notif.icon} size={44} />
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 7, marginBottom: 5, flexWrap: 'wrap' }}>
            <span style={{
              display: 'inline-flex', alignItems: 'center', gap: 4,
              padding: '2px 7px', borderRadius: 'var(--r-sm)',
              background: ect(notif.entity), color: ec(notif.entity),
              fontSize: 9, fontWeight: 700, fontFamily: 'var(--f-display)',
              textTransform: 'uppercase', letterSpacing: '.04em',
            }}>{notif.entity}</span>
            <span style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)' }}>
              {notif.timeAbs}
            </span>
          </div>
          <div style={{
            fontSize: 14, fontWeight: 700,
            fontFamily: 'var(--f-display)', lineHeight: 1.3,
          }}>{notif.title}</div>
        </div>
        <button onClick={onClose} style={{
          width: 28, height: 28, borderRadius: '50%',
          background: 'var(--bg-muted)', border: 'none', cursor: 'pointer',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          color: 'var(--text-muted)', fontSize: 18, lineHeight: 1, flexShrink: 0,
        }}>×</button>
      </div>
      {/* Divider */}
      <div style={{ height: 1, background: 'var(--border-light)', margin: '0 16px' }} />
      {/* Body */}
      <div style={{
        padding: '14px 16px 20px', fontSize: 13,
        lineHeight: 1.65, color: 'var(--text-sec)',
      }}>{notif.desc}</div>
      {/* CTA */}
      {notif.cta && (
        <div style={{ padding: '0 16px 20px' }}>
          <button style={{
            width: '100%', padding: '11px 16px',
            background: ec(notif.entity), color: '#fff',
            border: 'none', borderRadius: 'var(--r-lg)', cursor: 'pointer',
            fontSize: 13, fontWeight: 700, fontFamily: 'var(--f-display)',
            letterSpacing: '.01em',
          }}>{notif.cta}</button>
        </div>
      )}
      <div style={{ padding: '0 16px 28px' }}>
        <button style={{
          width: '100%', padding: '9px 16px',
          background: 'transparent', color: 'var(--text-muted)',
          border: '1px solid var(--border)', borderRadius: 'var(--r-lg)', cursor: 'pointer',
          fontSize: 12, fontWeight: 600, fontFamily: 'var(--f-display)',
        }}>Archivia notifica</button>
      </div>
    </div>
  );
}

// ── FiltersBar ───────────────────────────────────────────────────
function FiltersBar({ active, onChange, counts }) {
  return (
    <div role="tablist" aria-label="Filtra notifiche" style={{
      display: 'flex', gap: 5, padding: '8px 12px',
      overflowX: 'auto', flexShrink: 0,
      borderBottom: '1px solid var(--border)',
      background: 'var(--bg)', scrollbarWidth: 'none',
    }}>
      {FILTERS.map(f => {
        const isActive = active === f.key;
        const count = counts?.[f.key] ?? 0;
        return (
          <button key={f.key} role="tab" aria-selected={isActive}
            onClick={() => onChange(f.key)} style={{
              display: 'flex', alignItems: 'center', gap: 5,
              padding: '5px 10px', borderRadius: 'var(--r-pill)',
              border: 'none', cursor: 'pointer', whiteSpace: 'nowrap',
              fontSize: 11, fontWeight: 700, fontFamily: 'var(--f-display)',
              transition: 'all var(--dur-sm) var(--ease-out)',
              background: isActive ? ect(f.entity) : 'transparent',
              color: isActive ? ec(f.entity) : 'var(--text-sec)',
            }}>
            <span>{f.label}</span>
            {count > 0 && (
              <span style={{
                background: isActive ? ec(f.entity) : 'var(--bg-muted)',
                color: isActive ? '#fff' : 'var(--text-muted)',
                borderRadius: 'var(--r-pill)', fontSize: 9, fontWeight: 800,
                padding: '1px 5px', lineHeight: 1.6, fontFamily: 'var(--f-mono)',
              }}>{count}</span>
            )}
          </button>
        );
      })}
    </div>
  );
}

// ── EmptyState ───────────────────────────────────────────────────
function EmptyState() {
  return (
    <div style={{
      flex: 1, display: 'flex', flexDirection: 'column',
      alignItems: 'center', justifyContent: 'center',
      padding: '40px 24px', gap: 16, textAlign: 'center',
    }}>
      {/* Placeholder illustration */}
      <div style={{
        width: 100, height: 100, borderRadius: '50%',
        background: ect('event', 0.1),
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 44, marginBottom: 4,
      }}>🔔</div>
      <div style={{
        fontFamily: 'var(--f-display)', fontWeight: 700,
        fontSize: 17, color: 'var(--text)', letterSpacing: '-.01em',
      }}>Nessuna notifica</div>
      <div style={{
        fontSize: 13, color: 'var(--text-muted)',
        lineHeight: 1.55, maxWidth: 240,
      }}>
        Qui appariranno aggiornamenti su sessioni, agenti, eventi e altro.
      </div>
      <button style={{
        marginTop: 8, padding: '9px 18px',
        background: 'transparent', color: 'var(--text-sec)',
        border: '1px solid var(--border)', borderRadius: 'var(--r-lg)',
        cursor: 'pointer', fontSize: 12, fontWeight: 700,
        fontFamily: 'var(--f-display)',
      }}>Controlla impostazioni notifiche</button>
    </div>
  );
}

// ── SettingsQuickLink ─────────────────────────────────────────────
function SettingsQuickLink() {
  return (
    <div style={{
      margin: '16px 14px 20px',
      border: '1px solid var(--border)', borderRadius: 'var(--r-xl)',
      background: 'var(--bg-card)', overflow: 'hidden',
    }}>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 12, padding: '14px 16px',
        cursor: 'pointer',
      }}>
        <div style={{
          width: 40, height: 40, borderRadius: 'var(--r-lg)',
          background: ect('game', 0.12), color: ec('game'),
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 18,
        }}>⚙️</div>
        <div style={{ flex: 1 }}>
          <div style={{
            fontSize: 13, fontWeight: 700, fontFamily: 'var(--f-display)',
            color: 'var(--text)', marginBottom: 2,
          }}>Gestisci preferenze notifiche</div>
          <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>
            Scegli cosa ricevere e come
          </div>
        </div>
        <span style={{ color: 'var(--text-muted)', fontSize: 16 }}>›</span>
      </div>
      <div style={{ height: 1, background: 'var(--border-light)', margin: '0 16px' }} />
      <div style={{
        display: 'flex', gap: 0,
      }}>
        {[
          { icon: '🎲', label: 'Sessioni', entity: 'session' },
          { icon: '🤖', label: 'Agenti', entity: 'agent' },
          { icon: '🎉', label: 'Serate', entity: 'event' },
        ].map((item, i) => (
          <div key={i} style={{
            flex: 1, padding: '10px 8px', textAlign: 'center',
            borderRight: i < 2 ? '1px solid var(--border-light)' : 'none',
            cursor: 'pointer',
          }}>
            <div style={{ fontSize: 16, marginBottom: 3 }}>{item.icon}</div>
            <div style={{
              fontSize: 10, fontWeight: 700, fontFamily: 'var(--f-display)',
              color: ec(item.entity),
            }}>{item.label}</div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ── NotificationsFeed (shared logic) ─────────────────────────────
function NotificationsFeed({ notifs, onSelect, selectedId, onArchive, onMarkRead, filter, onFilterChange, counts, showSettingsCard }) {
  const grouped = GROUPS.map(g => ({
    ...g,
    items: notifs.filter(n => n.group === g.key),
  })).filter(g => g.items.length > 0);

  return (
    <div role="feed" aria-label="Notifiche" style={{
      flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column',
    }}>
      {grouped.map(g => (
        <div key={g.key}>
          <GroupDivider label={g.label} count={g.items.length} />
          {g.items.map(n => (
            <NotificationItem
              key={n.id} notif={n}
              selected={n.id === selectedId}
              onClick={() => onSelect?.(n)}
              onArchive={onArchive}
              onMarkRead={onMarkRead}
            />
          ))}
        </div>
      ))}
      {showSettingsCard && <SettingsQuickLink />}
    </div>
  );
}

// ── SCREEN 1 — Feed list ─────────────────────────────────────────
function Screen1Feed() {
  const [notifs, setNotifs] = useState(NOTIFICATIONS);
  const [selected, setSelected] = useState(null);
  const unread = notifs.filter(n => !n.read).length;

  const handleArchive = useCallback(id => setNotifs(ns => ns.filter(n => n.id !== id)), []);
  const handleMarkRead = useCallback(id => setNotifs(ns =>
    ns.map(n => n.id === id ? { ...n, read: true } : n)), []);
  const handleSelect = n => setSelected(n.id === selected?.id ? null : n);

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', position: 'relative' }}>
      <PhoneTopBar title="Notifiche" unreadCount={unread} />
      <NotificationsFeed
        notifs={notifs}
        onSelect={handleSelect}
        selectedId={selected?.id}
        onArchive={handleArchive}
        onMarkRead={handleMarkRead}
      />
    </div>
  );
}

// ── SCREEN 2 — Detail drawer ─────────────────────────────────────
function Screen2Detail() {
  const [notifs, setNotifs] = useState(NOTIFICATIONS);
  const [detail, setDetail] = useState(NOTIFICATIONS[0]);
  const unread = notifs.filter(n => !n.read).length;

  const handleArchive = useCallback(id => setNotifs(ns => ns.filter(n => n.id !== id)), []);
  const handleMarkRead = useCallback(id => setNotifs(ns =>
    ns.map(n => n.id === id ? { ...n, read: true } : n)), []);

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', position: 'relative' }}>
      <PhoneTopBar title="Notifiche" unreadCount={unread} />
      <div style={{ flex: 1, overflow: 'hidden', position: 'relative' }}>
        {/* Dimmed feed behind */}
        <div style={{ opacity: 0.4, overflow: 'hidden', height: '100%' }}>
          <NotificationsFeed notifs={notifs} onArchive={handleArchive} onMarkRead={handleMarkRead} />
        </div>
        {/* Overlay */}
        {detail && (
          <div style={{
            position: 'absolute', inset: 0,
            background: 'rgba(0,0,0,0.22)', backdropFilter: 'blur(1px)',
          }} onClick={() => setDetail(null)} />
        )}
        {detail && (
          <NotificationDetail notif={detail} onClose={() => setDetail(null)} />
        )}
      </div>
      {!detail && (
        <div style={{ padding: '8px 14px', borderTop: '1px solid var(--border)', flexShrink: 0 }}>
          <div style={{ fontSize: 11, color: 'var(--text-muted)', textAlign: 'center', fontFamily: 'var(--f-display)' }}>
            Tocca una notifica per aprire il dettaglio
          </div>
        </div>
      )}
    </div>
  );
}

// ── SCREEN 3 — Empty state ────────────────────────────────────────
function Screen3Empty() {
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <PhoneTopBar title="Notifiche" />
      <EmptyState />
    </div>
  );
}

// ── SCREEN 4 — Filters ───────────────────────────────────────────
function Screen4Filters() {
  const [notifs, setNotifs] = useState(NOTIFICATIONS);
  const [filter, setFilter] = useState('sessions');
  const [selected, setSelected] = useState(null);
  const unread = notifs.filter(n => !n.read).length;

  const handleArchive = useCallback(id => setNotifs(ns => ns.filter(n => n.id !== id)), []);
  const handleMarkRead = useCallback(id => setNotifs(ns =>
    ns.map(n => n.id === id ? { ...n, read: true } : n)), []);

  const counts = {};
  FILTERS.forEach(f => {
    const unreadInFilter = f.entities
      ? notifs.filter(n => f.entities.includes(n.entity) && !n.read).length
      : notifs.filter(n => !n.read).length;
    counts[f.key] = unreadInFilter;
  });

  const filtered = (() => {
    const f = FILTERS.find(f => f.key === filter);
    if (!f || !f.entities) return notifs;
    return notifs.filter(n => f.entities.includes(n.entity));
  })();

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <PhoneTopBar title="Notifiche" unreadCount={unread} />
      <FiltersBar active={filter} onChange={setFilter} counts={counts} />
      {filtered.length === 0
        ? <EmptyState />
        : <div role="feed" style={{ flex: 1, overflowY: 'auto' }}>
            {filtered.map(n => (
              <NotificationItem
                key={n.id} notif={n}
                selected={n.id === selected?.id}
                onClick={() => setSelected(n.id === selected?.id ? null : n)}
                onArchive={handleArchive}
                onMarkRead={handleMarkRead}
              />
            ))}
          </div>
      }
    </div>
  );
}

// ── SCREEN 5 — Feed + Settings quick-link ────────────────────────
function Screen5Settings() {
  const [notifs, setNotifs] = useState(NOTIFICATIONS);
  const unread = notifs.filter(n => !n.read).length;
  const handleArchive = useCallback(id => setNotifs(ns => ns.filter(n => n.id !== id)), []);
  const handleMarkRead = useCallback(id => setNotifs(ns =>
    ns.map(n => n.id === id ? { ...n, read: true } : n)), []);

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <PhoneTopBar title="Notifiche" unreadCount={unread} />
      <NotificationsFeed
        notifs={notifs}
        onArchive={handleArchive}
        onMarkRead={handleMarkRead}
        showSettingsCard
      />
    </div>
  );
}

// ── Export to window ─────────────────────────────────────────────
Object.assign(window, {
  Screen1Feed, Screen2Detail, Screen3Empty, Screen4Filters, Screen5Settings,
});
